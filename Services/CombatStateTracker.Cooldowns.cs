using AllHud.Data;
using AllHud.Models;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaAddon = Lumina.Excel.Sheets.Addon;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaStatus = Lumina.Excel.Sheets.Status;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using StructGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace AllHud.Services;

public sealed unsafe partial class CombatStateTracker : IDisposable {
    public IReadOnlyList<CooldownEntry> GetCooldowns(Configuration config) {
        BeginObjectLookupScope();
        try {
            ProcessRecentActionUses();
            var definitions = GetCooldownDefinitions(config);
            var byStatusId = BuildStatusLookup(definitions);
            var byActionId = BuildActionLookup(definitions);
            // 队伍栏的团减观测走固定全量来源，这里并上它的 actionIds，避免两条路径每帧互相覆盖 trackedActionIds。
            this.trackedActionIds = byActionId.Keys
                .Concat(TrackedActionCatalog.PartyMitigationActionIds)
                .ToHashSet();

            ProcessObservedActionUses(config, byActionId);
            ObserveKnownCooldownStatuses(config, byStatusId);
            ObserveLocalActionCooldowns(config, definitions);
            RemoveExpiredCooldowns(config);

            return this.observedCooldowns.Values
                .Select(value => value.ToEntry())
                .OrderBy(entry => entry.Group)
                .ThenBy(entry => entry.IsReady)
                .ThenBy(entry => entry.RemainingCooldownSeconds)
                .ThenBy(entry => entry.Name)
                .ToList();
        }
        finally {
            EndObjectLookupScope();
        }
    }

    public IReadOnlyList<PartyCooldownGroupEntry> GetPartyCooldownGroups(Configuration config) {
        if (!IsPartyCooldownTrackingActive) {
            return [];
        }
        BeginObjectLookupScope();
        try {
            ProcessRecentActionUses();
            var definitions = TrackedActionCatalog.PartyMitigationDefinitions;
            var byStatusId = BuildStatusLookup(definitions);
            var byActionId = BuildActionLookup(definitions);
            // hook 观测白名单统一为团减 + 队伍追踪四类 + 个人选中的并集，避免多消费方每帧互相覆盖。
            RefreshTrackedActionIds(config);

            ProcessObservedActionUses(config, byActionId);
            ObserveKnownCooldownStatuses(config, byStatusId);
            ObserveLocalActionCooldowns(config, definitions);
            RemoveExpiredCooldowns(config);

            var observedEntries = this.observedCooldowns.Values
                .Select(value => value.ToEntry())
                .ToList();

            return GetPartyMembers()
                .Select(member => CreatePartyCooldownGroup(member, definitions, observedEntries, config))
                .Where(group => group.Cooldowns.Count > 0)
                .ToList();
        }
        finally {
            EndObjectLookupScope();
        }
    }

    // 预览：团减面板示例。按 PartySlot(0..7) 生成各职业团减，部分冷却中、部分就绪，贴在原生队伍栏现有行上。
    public IReadOnlyList<PartyCooldownGroupEntry> GetPartyCooldownGroupsPreview() {
        var definitions = TrackedActionCatalog.PartyMitigationDefinitions;
        var previewClassJobIds = TrackedActionCatalog.Jobs.Select(job => job.ClassJobId).ToArray();
        var groups = new List<PartyCooldownGroupEntry>();
        for (var slot = 0; slot < previewClassJobIds.Length; slot++) {
            var member = BuildPreviewMember(slot, previewClassJobIds[slot]);
            var cooldowns = BuildPreviewCooldownsForMember(member, definitions, slot);
            groups.Add(new PartyCooldownGroupEntry(
                member.Name,
                member.JobName,
                member.ClassJobId,
                GetClassJobIconId(member.ClassJobId),
                member.GameObjectId,
                member.EntityId,
                member.IsLocalPlayer,
                member.SortIndex,
                cooldowns));
        }

        return groups;
    }

    // 预览：食物检查示例。偶数槽位有食物（剩余时间递减），奇数槽位缺食物（红叉提示）。
    public IReadOnlyList<PartyFoodStatusEntry> GetPartyFoodStatusesPreview() {
        var foodIconId = GetStatusIconId(FoodStatusId);
        var previewClassJobIds = TrackedActionCatalog.Jobs.Select(job => job.ClassJobId).ToArray();
        var statuses = new List<PartyFoodStatusEntry>();
        for (var slot = 0; slot < previewClassJobIds.Length; slot++) {
            var member = BuildPreviewMember(slot, previewClassJobIds[slot]);
            var hasFood = slot % 2 == 0;
            statuses.Add(new PartyFoodStatusEntry(
                member.Name,
                member.JobName,
                member.GameObjectId,
                member.EntityId,
                member.IsLocalPlayer,
                member.SortIndex,
                hasFood,
                foodIconId,
                "食物",
                hasFood ? 1200.0f - slot * 120.0f : 0.0f,
                1800.0f));
        }

        return statuses;
    }

    private PartyMemberSnapshot BuildPreviewMember(int slot, uint classJobId) {
        return new PartyMemberSnapshot(
            slot,
            $"预览队员{slot + 1}",
            GetJobName(classJobId),
            classJobId,
            (uint)(PreviewSourceEntityIdBase + slot),
            PreviewSourceObjectIdBase + (ulong)slot,
            slot == 0);
    }

    // 队伍冷却面板：按队友分行，追踪独立监控那四类技能（团辅/爆发/单体减伤/长CD）的真实 CD。
    // 队友 CD 靠 action-effect hook + 队友身上可见 buff 反推；纯个人 CD 观测不到时显示为就绪占位。
    public IReadOnlyList<PartyCooldownGroupEntry> GetPartyCooldownTracking(Configuration config) {
        if (!IsPartyCooldownTrackingActive) {
            return [];
        }

        var now = DateTime.UtcNow;
        if (this.cachedPartyCooldownTracking is not null && now < this.cachedPartyCooldownTrackingExpiresAt) {
            return this.cachedPartyCooldownTracking;
        }

        BeginObjectLookupScope();
        try {
            ProcessRecentActionUses();
            var definitions = GetSelectedPartyDefinitions(config)
                .GroupBy(GetDefinitionIdentity)
                .Select(group => group.First())
                .ToList();
            var byStatusId = BuildStatusLookup(definitions);
            var byActionId = BuildActionLookup(definitions);
            RefreshTrackedActionIds(config);

            ProcessObservedActionUses(config, byActionId, bypassEnabledGate: true);
            ObserveKnownCooldownStatuses(config, byStatusId, bypassEnabledGate: true);
            ObserveLocalActionCooldowns(config, definitions, bypassEnabledGate: true);
            RemoveExpiredCooldowns(config);

            var observedEntries = this.observedCooldowns.Values
                .Select(value => value.ToEntry())
                .ToList();

            var enabledKeys = GetEnabledActionKeySet(config);
            var members = GetPartyMembers();
            if (config.SelfCooldownBarSelfOnly) {
                members = members.Where(member => member.IsLocalPlayer).ToList();
            }
            else if (config.SelfCooldownBarHideSelf) {
                members = members.Where(member => !member.IsLocalPlayer).ToList();
            }

            this.cachedPartyCooldownTracking = members
                .Select(member => CreatePartyTrackedGroup(member, definitions, observedEntries, enabledKeys))
                .Where(group => group.Cooldowns.Count > 0)
                .OrderBy(group => group.PartySlot)
                .ToList();
            this.cachedPartyCooldownTrackingExpiresAt = now + PartyCooldownTrackingCacheDuration;
            return this.cachedPartyCooldownTracking;
        }
        finally {
            EndObjectLookupScope();
        }
    }

    // 预览：构造一组示例队员与各类技能状态（部分冷却中、部分就绪），用于配置界面调位置。
    public IReadOnlyList<PartyCooldownGroupEntry> GetPartyCooldownTrackingPreview(Configuration config) {
        var now = DateTime.UtcNow;
        var cacheKey = GetPartyCooldownTrackingPreviewCacheKey(config);
        if (this.cachedPartyCooldownTrackingPreview is not null
            && now < this.cachedPartyCooldownTrackingPreviewExpiresAt
            && string.Equals(this.cachedPartyCooldownTrackingPreviewKey, cacheKey, StringComparison.Ordinal)) {
            return this.cachedPartyCooldownTrackingPreview;
        }

        var definitions = GetSelectedPartyDefinitions(config)
            .GroupBy(GetDefinitionIdentity)
            .Select(group => group.First())
            .ToList();
        var enabledKeys = GetEnabledActionKeySet(config);
        var previewMembers = GetPreviewPartyMembers(config, definitions, enabledKeys);
        var groups = new List<PartyCooldownGroupEntry>();
        for (var sortIndex = 0; sortIndex < previewMembers.Count; sortIndex++) {
            var member = previewMembers[sortIndex];
            var cooldowns = BuildPreviewCooldownsForMember(member, definitions, sortIndex, enabledKeys);
            if (cooldowns.Count > 0) {
                groups.Add(new PartyCooldownGroupEntry(
                    member.Name,
                    member.JobName,
                    member.ClassJobId,
                    GetClassJobIconId(member.ClassJobId),
                    member.GameObjectId,
                    member.EntityId,
                    member.IsLocalPlayer,
                    member.SortIndex,
                    cooldowns));
            }
        }

        this.cachedPartyCooldownTrackingPreview = groups;
        this.cachedPartyCooldownTrackingPreviewKey = cacheKey;
        this.cachedPartyCooldownTrackingPreviewExpiresAt = now + PartyCooldownTrackingPreviewCacheDuration;
        return this.cachedPartyCooldownTrackingPreview;
    }

    private static string GetPartyCooldownTrackingPreviewCacheKey(Configuration config) {
        var keys = config.EnabledJobActionKeys ?? [];
        return string.Join('|',
            config.SelfCooldownBarSelfOnly ? 1 : 0,
            config.SelfCooldownBarHideSelf ? 1 : 0,
            config.SelectedJobSkillConfigClassJobId,
            string.Join(',', keys.OrderBy(key => key, StringComparer.Ordinal)));
    }

    private IReadOnlyList<PartyMemberSnapshot> GetPreviewPartyMembers(
        Configuration config,
        IReadOnlyList<TrackedStatusDefinition> definitions,
        HashSet<string> enabledKeys) {
        if (config.SelfCooldownBarSelfOnly) {
            return [BuildPreviewMember(0, config.SelectedJobSkillConfigClassJobId)];
        }

        var selectedJobIds = GetPreviewClassJobIds(config, definitions, enabledKeys).ToHashSet();
        var liveMembers = GetPartyMembers(includeUnavailable: true)
            .Where(member => selectedJobIds.Contains(member.ClassJobId))
            .Where(member => !config.SelfCooldownBarHideSelf || !member.IsLocalPlayer)
            .OrderBy(member => member.SortIndex)
            .ToList();
        if (liveMembers.Count > 0) {
            return liveMembers;
        }

        return GetPreviewClassJobIds(config, definitions, enabledKeys)
            .Select((classJobId, index) => BuildPreviewMember(index, classJobId))
            .ToList();
    }

    // 预览代表职业：仅显示自己时取配置界面当前职业；否则取物化定义里实际出现的职业，保持职业目录顺序。
    private static IReadOnlyList<uint> GetPreviewClassJobIds(
        Configuration config,
        IReadOnlyList<TrackedStatusDefinition> definitions,
        HashSet<string> enabledKeys) {
        if (config.SelfCooldownBarSelfOnly) {
            return [config.SelectedJobSkillConfigClassJobId];
        }

        var selectedJobIds = definitions
            .SelectMany(definition => definition.SourceClassJobIds)
            .Where(classJobId => classJobId != 0)
            .ToHashSet();

        return TrackedActionCatalog.Jobs
            .Select(job => job.ClassJobId)
            .Where(selectedJobIds.Contains)
            .Where(classJobId => !config.SelfCooldownBarHideSelf || classJobId != config.SelectedJobSkillConfigClassJobId)
            .ToList();
    }

    private List<CooldownEntry> BuildPreviewCooldownsForMember(
        PartyMemberSnapshot member,
        IReadOnlyList<TrackedStatusDefinition> definitions,
        int memberIndex,
        HashSet<string>? enabledKeys = null) {
        var now = DateTime.UtcNow;
        var cooldownIndex = 0;
        return definitions
            .Where(definition => IsSourceClassJobAllowed(definition, member.ClassJobId))
            .Where(definition => definition.SourceClassJobIds.Count > 0)
            .Where(definition => enabledKeys is null || IsDefinitionSelected(definition, member.ClassJobId, enabledKeys))
            .GroupBy(definition => (definition.Group, definition.StatusId, definition.ActionIds.FirstOrDefault()))
            .Select(group => group.First())
            .Select(definition => {
                // 交错制造"冷却中"与"就绪"两种状态，让预览同时展示遮罩+读秒与亮起图标。
                var onCooldown = (memberIndex + cooldownIndex) % 3 != 0;
                var remaining = onCooldown
                    ? Math.Max(3.0f, definition.CooldownSeconds * (0.25f + 0.12f * ((cooldownIndex % 4) + 1)))
                    : 0.0f;
                cooldownIndex++;
                return new CooldownEntry(
                    definition.StatusId,
                    definition.ActionIds.FirstOrDefault(),
                    GetDefinitionIconId(definition),
                    definition.Name,
                    definition.Group,
                    definition.CooldownSeconds,
                    definition.DurationSeconds,
                    member.Name,
                    member.JobName,
                    member.GameObjectId,
                    now.AddSeconds(remaining),
                    now,
                    false,
                    0.0f,
                    CooldownObservationKind.StatusFallback);
            })
            .OrderBy(entry => entry.Name, StringComparer.CurrentCulture)
            .ThenBy(entry => entry.ActionId)
            .ToList();
    }

    // 队伍面板专用：不经 IsDefinitionEnabled 的减伤门，直接按队员职业过滤，固定按名称排序（位置不跳动）。
    private PartyCooldownGroupEntry CreatePartyTrackedGroup(
        PartyMemberSnapshot member,
        IReadOnlyList<TrackedStatusDefinition> definitions,
        IReadOnlyList<CooldownEntry> observedEntries,
        HashSet<string> enabledKeys) {
        var cooldowns = definitions
            .Where(definition => IsSourceClassJobAllowed(definition, member.ClassJobId))
            .Where(definition => definition.SourceClassJobIds.Count > 0)
            .Where(definition => IsDefinitionSelected(definition, member.ClassJobId, enabledKeys))
            .Select(definition => FindObservedCooldown(member, definition, observedEntries)
                                  ?? CreateReadyCooldown(member, definition))
            .GroupBy(entry => (entry.Group, entry.StatusId, entry.ActionId))
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.CurrentCulture)
            .ThenBy(entry => entry.ActionId)
            .ToList();

        return new PartyCooldownGroupEntry(
            member.Name,
            member.JobName,
            member.ClassJobId,
            GetClassJobIconId(member.ClassJobId),
            member.GameObjectId,
            member.EntityId,
            member.IsLocalPlayer,
            member.SortIndex,
            cooldowns);
    }

    // 统一刷新 hook 观测白名单：团减 + 队伍追踪四类 + 个人选中，所有消费方求并集，避免每帧互相覆盖。
    private void RefreshTrackedActionIds(Configuration config) {
        this.trackedActionIds = TrackedActionCatalog.PartyMitigationActionIds
            .Concat(TrackedActionCatalog.PartyTrackedActionIds)
            .Concat(GetSelectedActionIds(config))
            .Concat(GetSelectedPartyActionIds(config))
            .ToHashSet();
    }

    // 独立监控栏物化定义的 actionIds（含 RaidBuff/Burst/Personal 及 catalog-fallback），
    // 这些不被 IsDefinitionEnabled 收录，必须单独并入 hook 白名单，否则跨缓存窗口刷新会丢事件。
    private IEnumerable<uint> GetSelectedPartyActionIds(Configuration config) {
        return GetSelectedPartyDefinitions(config)
            .SelectMany(definition => definition.ActionIds)
            .Where(actionId => actionId != 0);
    }


    public IReadOnlyList<PartyFoodStatusEntry> GetPartyFoodStatuses(Configuration config) {
        if (!IsPartyStatusTrackingActive) {
            return [];
        }

        var foodIconId = GetStatusIconId(FoodStatusId);
        BeginObjectLookupScope();
        try {
            return GetPartyMembers()
                .Select(member => CreatePartyFoodStatusEntry(member, config, foodIconId))
                .ToList();
        }
        finally {
            EndObjectLookupScope();
        }
    }

    private PartyFoodStatusEntry CreatePartyFoodStatusEntry(PartyMemberSnapshot member, Configuration config, uint foodIconId) {
        var gameObject = ResolveObjectByEntityOrGameObjectId(member.EntityId)
                         ?? ResolveObjectByEntityOrGameObjectId((uint)member.GameObjectId);
        StatusEntry? foodStatus = null;
        if (gameObject is IBattleChara battleChara) {
            foodStatus = ScanBattleChara(battleChara, member.GameObjectId, member.Name, config)
                .Where(status => status.StatusId == FoodStatusId)
                .OrderByDescending(status => status.RemainingSeconds)
                .FirstOrDefault();
        }

        return new PartyFoodStatusEntry(
            member.Name,
            member.JobName,
            member.GameObjectId,
            member.EntityId,
            member.IsLocalPlayer,
            member.SortIndex,
            foodStatus is not null,
            foodStatus?.IconId is > 0 ? foodStatus.IconId : foodIconId,
            foodStatus?.Name ?? "椋熺墿",
            foodStatus?.RemainingSeconds ?? 0.0f,
            foodStatus?.MaxSeconds ?? 0.0f);
    }

    private PartyCooldownGroupEntry CreatePartyCooldownGroup(
        PartyMemberSnapshot member,
        IReadOnlyList<TrackedStatusDefinition> definitions,
        IReadOnlyList<CooldownEntry> observedEntries,
        Configuration config) {
        var cooldowns = definitions
            .Where(definition => IsDefinitionEnabled(definition, config))
            .Where(definition => IsSourceClassJobAllowed(definition, member.ClassJobId))
            .Select(definition => FindObservedCooldown(member, definition, observedEntries)
                                  ?? CreateReadyCooldown(member, definition))
            .OrderBy(entry => entry.Group)
            .ThenBy(entry => entry.IsReady)
            .ThenBy(entry => entry.RemainingCooldownSeconds)
            .ThenBy(entry => entry.Name)
            .ToList();

        return new PartyCooldownGroupEntry(
            member.Name,
            member.JobName,
            member.ClassJobId,
            GetClassJobIconId(member.ClassJobId),
            member.GameObjectId,
            member.EntityId,
            member.IsLocalPlayer,
            member.SortIndex,
            cooldowns);
    }

    private CooldownEntry CreateReadyCooldown(PartyMemberSnapshot member, TrackedStatusDefinition definition) {
        return new CooldownEntry(
            definition.StatusId,
            definition.ActionIds.FirstOrDefault(),
            GetDefinitionIconId(definition),
            definition.Name,
            definition.Group,
            definition.CooldownSeconds,
            definition.DurationSeconds,
            member.Name,
            member.JobName,
            member.GameObjectId != 0 ? member.GameObjectId : member.EntityId,
            DateTime.UtcNow,
            DateTime.MinValue,
            false,
            0.0f,
            CooldownObservationKind.StatusFallback);
    }

    private static CooldownEntry? FindObservedCooldown(
        PartyMemberSnapshot member,
        TrackedStatusDefinition definition,
        IEnumerable<CooldownEntry> observedEntries) {
        return observedEntries.FirstOrDefault(entry =>
            IsSameCooldownDefinition(entry, definition) && IsSameCooldownSource(entry, member));
    }

    private static bool IsSameCooldownDefinition(CooldownEntry entry, TrackedStatusDefinition definition) {
        if (entry.Group != definition.Group) {
            return false;
        }

        if (definition.StatusId != 0) {
            return entry.StatusId == definition.StatusId;
        }

        return entry.ActionId != 0 && definition.ActionIds.Contains(entry.ActionId);
    }

    private static bool IsSameCooldownSource(CooldownEntry entry, PartyMemberSnapshot member) {
        return entry.SourceObjectId != 0
               && (entry.SourceObjectId == member.GameObjectId || entry.SourceObjectId == member.EntityId);
    }

    private IReadOnlyList<PartyMemberSnapshot> GetPartyMembers(bool includeUnavailable = false) {
        var result = new List<PartyMemberSnapshot>();
        var localPlayer = this.objectTable.LocalPlayer;
        var localGameObjectId = localPlayer?.GameObjectId ?? 0;
        var localEntityId = localPlayer?.EntityId ?? 0;

        var index = 0;
        foreach (var member in this.partyList) {
            var gameObject = member.GameObject;
            var entityId = member.EntityId;
            var gameObjectId = gameObject?.GameObjectId ?? entityId;
            var classJobId = member.ClassJob.RowId;
            var isLocalPlayer = (localGameObjectId != 0 && gameObjectId == localGameObjectId)
                                || (localEntityId != 0 && entityId == localEntityId);

            // 原生贴合模式需要可解析对象来对齐图标；定制队伍列表则允许显示跨服/远距离成员。
            if (classJobId != 0 && (includeUnavailable || gameObject is not null)) {
                result.Add(new PartyMemberSnapshot(
                    index,
                    member.Name.ToString(),
                    GetJobName(classJobId),
                    classJobId,
                    entityId,
                    gameObjectId,
                    isLocalPlayer));
            }

            index++;
        }

        if (localPlayer is not null && !result.Any(member => member.IsLocalPlayer)) {
            var classJobId = localPlayer.ClassJob.RowId;
            result.Insert(0, new PartyMemberSnapshot(
                -1,
                localPlayer.Name.ToString(),
                GetJobName(classJobId),
                classJobId,
                localPlayer.EntityId,
                localPlayer.GameObjectId,
                true));
        }

        return result
            .GroupBy(member => member.GameObjectId != 0 ? member.GameObjectId : member.EntityId)
            .Select(group => group.First())
            .OrderByDescending(member => member.IsLocalPlayer)
            .ThenBy(member => member.SortIndex)
            .ToList();
    }

    private void ObserveKnownCooldownStatuses(Configuration config, IReadOnlyDictionary<uint, IReadOnlyList<TrackedStatusDefinition>> byStatusId, bool bypassEnabledGate = false) {
        var player = this.objectTable.LocalPlayer;
        var localPlayerId = player?.GameObjectId ?? 0;

        foreach (var gameObject in this.objectTable) {
            if (gameObject is IBattleChara battleChara) {
                ObserveDefinitionsOnBattleChara(battleChara, localPlayerId, battleChara.Name.ToString(), config, byStatusId, bypassEnabledGate);
            }
        }

        if (this.targetManager.Target is IBattleChara target) {
            ObserveDefinitionsOnBattleChara(target, localPlayerId, target.Name.ToString(), config, byStatusId, bypassEnabledGate);
        }
    }

    private void ObserveDefinitionsOnBattleChara(IBattleChara battleChara, ulong localPlayerId, string holderName, Configuration config, IReadOnlyDictionary<uint, IReadOnlyList<TrackedStatusDefinition>> byStatusId, bool bypassEnabledGate = false) {
        var native = (BattleChara*)battleChara.Address;
        if (native is null) {
            return;
        }

        ref var statusManager = ref native->StatusManager;
        var statusCount = Math.Clamp((int)statusManager.NumValidStatuses, 0, MaxNativeStatusSlots);
        for (var index = 0; index < statusCount; index++) {
            ref var nativeStatus = ref statusManager.Status[index];
            var statusId = nativeStatus.StatusId;
            if (statusId == 0) {
                continue;
            }

            if (!byStatusId.TryGetValue(statusId, out var definitions)) {
                continue;
            }

            var sourceObjectId = nativeStatus.SourceObject.Id;
            if (sourceObjectId == 0 || sourceObjectId == ulong.MaxValue) {
                sourceObjectId = battleChara.GameObjectId;
            }

            var sourceName = ResolveObjectName(sourceObjectId) ?? holderName;
            var sourceJobName = ResolveObjectJobName(sourceObjectId);
            var sourceClassJobId = ResolveObjectClassJobId(sourceObjectId);
            if (string.IsNullOrWhiteSpace(sourceJobName) && sourceObjectId == battleChara.GameObjectId) {
                sourceJobName = GetCharacterJobName(battleChara);
            }

            if (sourceClassJobId == 0 && sourceObjectId == battleChara.GameObjectId) {
                sourceClassJobId = GetCharacterClassJobId(battleChara);
            }

            var remaining = Math.Max(0.0f, nativeStatus.RemainingTime);
            if (remaining <= 0.05f) {
                continue;
            }

            foreach (var definition in definitions) {
                if ((!bypassEnabledGate && !IsDefinitionEnabled(definition, config)) || !IsSourceClassJobAllowed(definition, sourceClassJobId)) {
                    continue;
                }

                var observedDuration = GetMaxDuration(statusId, remaining, false);
                var duration = Math.Max(definition.DurationSeconds, observedDuration);
                var cooldownRemaining = Math.Max(0.0f, definition.CooldownSeconds - duration + remaining);
                var observedAt = DateTime.UtcNow;
                var readyAt = observedAt.AddSeconds(cooldownRemaining);

                UpsertCooldown(
                    definition,
                    sourceName,
                    sourceJobName,
                    sourceObjectId,
                    readyAt,
                    observedAt,
                    true,
                    remaining,
                    CooldownObservationKind.StatusFallback);
            }
        }
    }

    private void ProcessObservedActionUses(Configuration config, IReadOnlyDictionary<uint, IReadOnlyList<TrackedStatusDefinition>> byActionId, bool bypassEnabledGate = false) {
        // 先把队列里的新事件排空进保留缓冲（一次性，不破坏其他栏的消费），再按窗口裁剪过期项。
        while (this.observedActionUses.TryDequeue(out var dequeued)) {
            this.observedActionLog.Add(dequeued);
        }

        var cutoff = DateTime.UtcNow - ObservedActionLogRetention;
        this.observedActionLog.RemoveAll(entry => entry.ObservedAt < cutoff);

        // 各栏只读迭代缓冲，按自己的 byActionId 过滤；UpsertCooldown 对同一事件幂等，重复处理无害。
        foreach (var observedActionUse in this.observedActionLog) {
            if (!byActionId.TryGetValue(observedActionUse.ActionId, out var definitions)) {
                continue;
            }

            var source = ResolveObjectByEntityOrGameObjectId(observedActionUse.CasterEntityId);
            var sourceObjectId = source?.GameObjectId ?? observedActionUse.CasterEntityId;
            var sourceName = source?.Name.ToString() ?? $"#{observedActionUse.CasterEntityId:X8}";
            var sourceJobName = GetCharacterJobName(source);
            var sourceClassJobId = GetCharacterClassJobId(source);

            foreach (var definition in definitions) {
                if ((!bypassEnabledGate && !IsDefinitionEnabled(definition, config)) || !IsSourceClassJobAllowed(definition, sourceClassJobId)) {
                    continue;
                }

                UpsertCooldown(
                    definition,
                    sourceName,
                    sourceJobName,
                    sourceObjectId,
                    observedActionUse.ObservedAt.AddSeconds(definition.CooldownSeconds),
                    observedActionUse.ObservedAt,
                    false,
                    0.0f,
                    CooldownObservationKind.ActionEvent);
            }
        }
    }

    private void ProcessRecentActionUses() {
        while (this.recentActionUses.TryDequeue(out var observedActionUse)) {
            var source = ResolveObjectByEntityOrGameObjectId(observedActionUse.CasterEntityId);
            var sourceObjectId = source?.GameObjectId ?? observedActionUse.CasterEntityId;
            var sourceName = source?.Name.ToString() ?? $"#{observedActionUse.CasterEntityId:X8}";
            var sourceJobName = GetCharacterJobName(source);

            this.recentActions.RemoveAll(action =>
                action.ActionId == observedActionUse.ActionId && action.SourceObjectId == sourceObjectId);
            this.recentActions.Insert(0, new RecentActionEntry(
                observedActionUse.ActionId,
                GetActionIconId(observedActionUse.ActionId),
                GetActionName(observedActionUse.ActionId),
                sourceName,
                sourceJobName,
                sourceObjectId,
                observedActionUse.ObservedAt));

            if (this.recentActions.Count > MaxRecentActions) {
                this.recentActions.RemoveRange(MaxRecentActions, this.recentActions.Count - MaxRecentActions);
            }
        }
    }

    private void ObserveLocalActionCooldowns(Configuration config, IReadOnlyList<TrackedStatusDefinition> definitions, bool bypassEnabledGate = false) {
        var player = this.objectTable.LocalPlayer;
        if (player is null) {
            return;
        }

        var playerClassJobId = GetCharacterClassJobId(player);

        foreach (var definition in definitions.Where(definition => bypassEnabledGate || IsDefinitionEnabled(definition, config))) {
            if (!IsSourceClassJobAllowed(definition, playerClassJobId)) {
                continue;
            }

            foreach (var actionId in definition.ActionIds) {
                var remaining = GetLocalActionCooldownRemaining(actionId);
                if (remaining <= 0.05f) {
                    continue;
                }

                var observedAt = DateTime.UtcNow;
                UpsertCooldown(
                    definition,
                    player.Name.ToString(),
                    GetCharacterJobName(player),
                    player.GameObjectId,
                    observedAt.AddSeconds(remaining),
                    observedAt,
                    false,
                    0.0f,
                    CooldownObservationKind.LocalRecast);
                break;
            }
        }
    }

    private IReadOnlyList<StatusEntry> ScanBattleChara(IBattleChara battleChara, ulong localPlayerId, string holderName, Configuration config) {
        var result = new List<StatusEntry>();
        var native = (BattleChara*)battleChara.Address;
        if (native is null) {
            return result;
        }

        ref var statusManager = ref native->StatusManager;
        var statusCount = Math.Clamp((int)statusManager.NumValidStatuses, 0, MaxNativeStatusSlots);
        for (var index = 0; index < statusCount; index++) {
            ref var nativeStatus = ref statusManager.Status[index];
            if (nativeStatus.StatusId == 0) {
                continue;
            }

            if (!this.statusSheet.TryGetRow(nativeStatus.StatusId, out var row)) {
                continue;
            }

            var remaining = nativeStatus.RemainingTime;
            if (remaining <= 0.0f && !row.IsPermanent) {
                continue;
            }

            var maxSeconds = GetMaxDuration(nativeStatus.StatusId, remaining, row.IsPermanent);
            var sourceObjectId = nativeStatus.SourceObject.Id;
            var sourceName = ResolveObjectName(sourceObjectId) ?? "Unknown";
            var sourceJobName = ResolveObjectJobName(sourceObjectId);
            TryGetBoolProperty(row, out var canDispel, "CanDispel", "CanEsuna");
            TryGetBoolProperty(row, out var partyListPriority, "PartyListPriority", "PartyListPrio", "PriorityPartyList");

            result.Add(new StatusEntry(
                nativeStatus.StatusId,
                row.Icon,
                GetStatusDisplayName(nativeStatus.StatusId, row.Name.ToString(), config),
                holderName,
                sourceName,
                sourceJobName,
                sourceObjectId,
                row.IsPermanent ? 99999.0f : remaining,
                maxSeconds,
                row.StatusCategory == 1,
                localPlayerId != 0 && sourceObjectId == localPlayerId,
                index,
                canDispel,
                partyListPriority));
        }

        return result;
    }

    private float GetMaxDuration(uint statusId, float remaining, bool isPermanent) {
        if (isPermanent) {
            return 99999.0f;
        }

        if (!this.maxDurations.TryGetValue(statusId, out var maxDuration) || remaining > maxDuration) {
            maxDuration = remaining;
            this.maxDurations[statusId] = maxDuration;
        }

        return Math.Max(maxDuration, 1.0f);
    }

    // 建立一次性对象快照：把 objectTable 的一次遍历结果索引成字典，
    // 供本次冷却刷新内的所有 Resolve*/ResolveObjectByEntityOrGameObjectId 做 O(1) 反查，
    // 避免每个匹配状态都重新全表扫描（原先是 O(对象数 × 状态数 × 3) 放大）。
    private void BeginObjectLookupScope() {
        this.objectLookupByGameObjectId.Clear();
        this.objectLookupByEntityOrGameObjectId.Clear();
        foreach (var gameObject in this.objectTable) {
            if (gameObject is null) {
                continue;
            }

            // TryAdd 保留首个对象，与原 foreach 扫描 "前者胜" 一致。
            this.objectLookupByGameObjectId.TryAdd(gameObject.GameObjectId, gameObject);
            // 组合表按 EntityId、GameObjectId 顺序 TryAdd，复刻原 (EntityId==id || GameObjectId==id) 表顺序首个命中。
            this.objectLookupByEntityOrGameObjectId.TryAdd(gameObject.EntityId, gameObject);
            this.objectLookupByEntityOrGameObjectId.TryAdd(gameObject.GameObjectId, gameObject);
        }

        this.objectLookupActive = true;
    }

    private void EndObjectLookupScope() {
        this.objectLookupActive = false;
        this.objectLookupByGameObjectId.Clear();
        this.objectLookupByEntityOrGameObjectId.Clear();
    }

    private IGameObject? LookupByGameObjectId(ulong objectId) {
        if (this.objectLookupActive) {
            return this.objectLookupByGameObjectId.TryGetValue(objectId, out var cached) ? cached : null;
        }

        foreach (var gameObject in this.objectTable) {
            if (gameObject?.GameObjectId == objectId) {
                return gameObject;
            }
        }

        return null;
    }

    private string? ResolveObjectName(ulong objectId) {
        if (objectId == 0 || objectId == ulong.MaxValue) {
            return null;
        }

        return LookupByGameObjectId(objectId)?.Name.ToString();
    }

    private string ResolveObjectJobName(ulong objectId) {
        if (objectId == 0 || objectId == ulong.MaxValue) {
            return string.Empty;
        }

        return GetCharacterJobName(LookupByGameObjectId(objectId));
    }

    private uint ResolveObjectClassJobId(ulong objectId) {
        if (objectId == 0 || objectId == ulong.MaxValue) {
            return 0;
        }

        return GetCharacterClassJobId(LookupByGameObjectId(objectId));
    }

    private IGameObject? ResolveObjectByEntityOrGameObjectId(uint id) {
        if (this.objectLookupActive) {
            return this.objectLookupByEntityOrGameObjectId.TryGetValue(id, out var cached) ? cached : null;
        }

        foreach (var gameObject in this.objectTable) {
            if (gameObject is null) {
                continue;
            }

            if (gameObject.EntityId == id || gameObject.GameObjectId == id) {
                return gameObject;
            }
        }

        return null;
    }

    private static string GetCharacterJobName(IGameObject? gameObject) {
        if (gameObject is not ICharacter character) {
            return string.Empty;
        }

        return GetJobName(character.ClassJob.RowId);
    }

    private static uint GetCharacterClassJobId(IGameObject? gameObject) {
        return gameObject is ICharacter character ? character.ClassJob.RowId : 0;
    }

    private static string GetJobName(uint classJobId) {
        return classJobId switch {
            1 => "剑术师",
            2 => "格斗家",
            3 => "斧术师",
            4 => "枪术师",
            5 => "弓箭手",
            6 => "幻术师",
            7 => "咒术师",
            19 => "骑士",
            20 => "武僧",
            21 => "战士",
            22 => "龙骑士",
            23 => "吟游诗人",
            24 => "白魔法师",
            25 => "黑魔法师",
            26 => "秘术师",
            27 => "召唤师",
            28 => "学者",
            29 => "双剑师",
            30 => "忍者",
            31 => "机工士",
            32 => "暗黑骑士",
            33 => "占星术士",
            34 => "武士",
            35 => "赤魔法师",
            36 => "青魔法师",
            37 => "绝枪战士",
            38 => "舞者",
            39 => "钐镰客",
            40 => "贤者",
            41 => "蝰蛇剑士",
            42 => "绘灵法师",
            _ => string.Empty,
        };
    }

    private IReadOnlyList<TrackedStatusDefinition> GetCooldownDefinitions(Configuration config) {
        TrackedActionCatalog.EnsureActionSelectionInitialized(config);

        return GetSelectedJobActionDefinitions(config)
            .Concat(GetCustomCooldownDefinitions(config))
            .Where(definition => IsDefinitionEnabled(definition, config))
            .GroupBy(GetDefinitionIdentity)
            .Select(group => group.First())
            .ToList();
    }

    // 个人栏选中技能的 actionIds，供队伍栏并入 trackedActionIds，让两条观测路径互不覆盖。
    private IEnumerable<uint> GetSelectedActionIds(Configuration config) {
        return GetCooldownDefinitions(config)
            .SelectMany(definition => definition.ActionIds)
            .Where(actionId => actionId != 0);
    }

    private IEnumerable<TrackedStatusDefinition> GetSelectedJobActionDefinitions(Configuration config) {
        foreach (var key in config.EnabledJobActionKeys ?? Enumerable.Empty<string>()) {
            if (!TryParseActionKey(key, out var classJobId, out var actionId)) {
                continue;
            }

            if (TrackedActionCatalog.PartyMitigationActionIds.Contains(actionId)) {
                continue;
            }

            if (classJobId == 0) {
                var commonSkill = TrackedActionCatalog.FindCommonSkill(actionId);
                if (commonSkill is not null) {
                    yield return commonSkill.Definition;
                }

                continue;
            }

            var knownSkill = TrackedActionCatalog.FindKnownSkill(classJobId, actionId);
            if (knownSkill is not null) {
                if (!IsVisibleCooldownGroup(knownSkill.Definition.Group)) {
                    continue;
                }

                yield return knownSkill.Definition;
                continue;
            }

            var action = GetJobActionCatalog(classJobId).FirstOrDefault(entry => entry.ActionId == actionId);
            if (action is null || action.CooldownSeconds <= 0.05f) {
                continue;
            }

            if (!IsVisibleCooldownGroup(action.Group)) {
                continue;
            }

            yield return new TrackedStatusDefinition(
                0,
                action.Name,
                action.Group,
                action.CooldownSeconds,
                0.0f,
                false,
                new[] { actionId },
                new[] { classJobId });
        }
    }

    // 独立监控冷却栏数据源：从 EnabledJobActionKeys 物化用户实际勾选的技能（含团辅/爆发/单体减伤/长 CD），
    // 用选择器的分组语义过滤（剔除团减），保证"勾什么职业就显示什么"，预览与实战共用同一份。
    private IEnumerable<TrackedStatusDefinition> GetSelectedPartyDefinitions(Configuration config) {
        TrackedActionCatalog.EnsureActionSelectionInitialized(config);

        foreach (var key in config.EnabledJobActionKeys ?? Enumerable.Empty<string>()) {
            if (!TryParseActionKey(key, out var classJobId, out var actionId)) {
                continue;
            }

            if (classJobId == 0) {
                var commonSkill = TrackedActionCatalog.FindCommonSkill(actionId);
                if (commonSkill is not null && IsPartyVisibleDefinition(commonSkill.Definition)) {
                    yield return commonSkill.Definition;
                }

                continue;
            }

            var knownSkill = TrackedActionCatalog.FindKnownSkill(classJobId, actionId);
            if (knownSkill is not null) {
                if (IsPartyVisibleDefinition(knownSkill.Definition)) {
                    yield return knownSkill.Definition;
                }

                continue;
            }

            var action = GetJobActionCatalog(classJobId).FirstOrDefault(entry => entry.ActionId == actionId);
            if (action is null || action.CooldownSeconds <= 0.05f || !IsPartyVisibleAction(action)) {
                continue;
            }

            yield return new TrackedStatusDefinition(
                0, action.Name, action.Group, action.CooldownSeconds, 0.0f, false,
                new[] { actionId }, new[] { classJobId });
        }
    }

    private static IEnumerable<TrackedStatusDefinition> GetCustomCooldownDefinitions(Configuration config) {
        foreach (var custom in config.CustomTrackedDefinitions ?? Enumerable.Empty<CustomTrackedDefinition>()) {
            if (!custom.Enabled || !IsCooldownType(custom.Type) || (custom.StatusId == 0 && custom.ActionId == 0)) {
                continue;
            }

            var group = custom.Type == CustomTrackType.RaidBuffCooldown
                ? CooldownGroup.RaidBuff
                : CooldownGroup.PartyMitigation;
            var name = string.IsNullOrWhiteSpace(custom.Name) ? "定制技能" : custom.Name.Trim();
            IReadOnlyList<uint> actionIds = custom.ActionId == 0 ? Array.Empty<uint>() : new uint[] { custom.ActionId };

            yield return new TrackedStatusDefinition(
                custom.StatusId,
                name,
                group,
                Math.Max(0.0f, custom.CooldownSeconds),
                Math.Max(0.0f, custom.DurationSeconds),
                custom.Type == CustomTrackType.MitigationCooldown,
                actionIds);
        }
    }

    private static string GetDefinitionIdentity(TrackedStatusDefinition definition) {
        var actionId = definition.ActionIds.FirstOrDefault();
        return $"{definition.Group}:{definition.StatusId}:{actionId}:{string.Join(',', definition.SourceClassJobIds)}";
    }

    private static bool TryParseActionKey(string key, out uint classJobId, out uint actionId) {
        classJobId = 0;
        actionId = 0;

        if (string.IsNullOrWhiteSpace(key)) {
            return false;
        }

        var parts = key.Split(':', 2);
        return parts.Length == 2
               && uint.TryParse(parts[0], out classJobId)
               && uint.TryParse(parts[1], out actionId)
               && actionId != 0;
    }

    private static IReadOnlyDictionary<uint, IReadOnlyList<TrackedStatusDefinition>> BuildStatusLookup(IEnumerable<TrackedStatusDefinition> definitions) {
        return definitions
            .Where(definition => definition.StatusId != 0)
            .GroupBy(definition => definition.StatusId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TrackedStatusDefinition>)group.ToList());
    }

    private static IReadOnlyDictionary<uint, IReadOnlyList<TrackedStatusDefinition>> BuildActionLookup(IEnumerable<TrackedStatusDefinition> definitions) {
        return definitions
            .SelectMany(definition => definition.ActionIds.Select(actionId => new { actionId, definition }))
            .Where(pair => pair.actionId != 0)
            .GroupBy(pair => pair.actionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TrackedStatusDefinition>)group.Select(pair => pair.definition).ToList());
    }

    private static bool IsTrackedTargetStatus(uint statusId, Configuration config) {
        return statusId != 0
               && (TrackedActionCatalog.GetEnabledDefinitions(config).Any(definition => definition.StatusId == statusId)
                   || IsCustomStatusType(statusId, config, CustomTrackType.TargetStatus)
                   || IsCustomStatusType(statusId, config, CustomTrackType.RaidBuffCooldown)
                   || IsCustomStatusType(statusId, config, CustomTrackType.MitigationCooldown));
    }

    private static bool IsCustomStatusType(uint statusId, Configuration config, CustomTrackType type) {
        return statusId != 0
               && (config.CustomTrackedDefinitions ?? Enumerable.Empty<CustomTrackedDefinition>())
                   .Any(custom => custom.Enabled && custom.StatusId == statusId && custom.Type == type);
    }

    private static bool IsCooldownType(CustomTrackType type) {
        return type is CustomTrackType.RaidBuffCooldown or CustomTrackType.MitigationCooldown;
    }

    private static string GetStatusDisplayName(uint statusId, string fallback, Configuration config) {
        var customName = (config.CustomTrackedDefinitions ?? Enumerable.Empty<CustomTrackedDefinition>())
            .Where(custom => custom.Enabled && custom.StatusId == statusId && !string.IsNullOrWhiteSpace(custom.Name))
            .Select(custom => custom.Name.Trim())
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(customName)) {
            return customName;
        }

        var catalogName = TrackedActionCatalog.GetEnabledDefinitions(config)
            .Where(definition => definition.StatusId == statusId && !string.IsNullOrWhiteSpace(definition.Name))
            .Select(definition => definition.Name)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(catalogName) ? fallback : catalogName;
    }

    private static bool IsSourceClassJobAllowed(TrackedStatusDefinition definition, uint sourceClassJobId) {
        return definition.SourceClassJobIds.Count == 0
               || sourceClassJobId == 0
               || definition.SourceClassJobIds.Contains(sourceClassJobId);
    }

    // 独立监控冷却栏只显示用户在技能选择器里勾选的技能（EnabledJobActionKeys）。
    private static HashSet<string> GetEnabledActionKeySet(Configuration config) {
        return (config.EnabledJobActionKeys ?? Enumerable.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
    }

    // 定义只要有任一动作命中该职业（含基础职业）或通用键的勾选即视为已选；无动作的纯状态定义保留可见。
    private static bool IsDefinitionSelected(TrackedStatusDefinition definition, uint classJobId, HashSet<string> enabledKeys) {
        var hasActionId = false;
        foreach (var actionId in definition.ActionIds) {
            if (actionId == 0) {
                continue;
            }

            hasActionId = true;
            if (enabledKeys.Contains(TrackedActionCatalog.GetCommonActionKey(actionId))) {
                return true;
            }

            foreach (var familyId in TrackedActionCatalog.GetClassJobFamilyIds(classJobId)) {
                if (enabledKeys.Contains(TrackedActionCatalog.GetActionKey(familyId, actionId))) {
                    return true;
                }
            }
        }

        return !hasActionId;
    }

    private static bool IsDefinitionEnabled(TrackedStatusDefinition definition, Configuration config) {
        var showMitigation = config.ShowPartyMitigationCooldowns
                             || config.ShowTargetMitigationCooldowns
                             || config.ShowPersonalMitigationCooldowns
                             || config.ShowMitigationCooldowns;

        return definition.Group switch {
            CooldownGroup.Common => false,
            CooldownGroup.Burst => false,
            CooldownGroup.PartyMitigation => showMitigation,
            CooldownGroup.TargetMitigation => showMitigation,
            CooldownGroup.PersonalMitigation => showMitigation,
            CooldownGroup.Personal => false,
            CooldownGroup.RaidBuff => false,
            CooldownGroup.Mitigation => showMitigation,
            _ => true,
        };
    }

    private static bool IsVisibleCooldownGroup(CooldownGroup group) {
        return group is CooldownGroup.RaidBuff or CooldownGroup.PartyMitigation or CooldownGroup.TargetMitigation or CooldownGroup.PersonalMitigation or CooldownGroup.Mitigation;
    }

    // 独立监控冷却栏的可见分组（与技能选择器一致）：团辅/爆发/单体减伤/泛减伤/长 CD，剔除团减（含 TargetMitigation 归一）。
    private static bool IsPartyVisibleGroup(CooldownGroup group) {
        var normalized = group == CooldownGroup.TargetMitigation ? CooldownGroup.PartyMitigation : group;
        return normalized is CooldownGroup.RaidBuff
            or CooldownGroup.Burst
            or CooldownGroup.PersonalMitigation
            or CooldownGroup.Mitigation
            or CooldownGroup.Personal;
    }

    private static bool IsPartyVisibleDefinition(TrackedStatusDefinition definition) {
        return IsPartyVisibleGroup(definition.Group)
               || (definition.Group == CooldownGroup.Common
                   && definition.ActionIds.Any(TrackedActionCatalog.IndependentMonitorCommonActionIds.Contains));
    }

    private static bool IsPartyVisibleAction(JobActionCatalogEntry action) {
        return IsPartyVisibleGroup(action.Group)
               || (action.Group == CooldownGroup.Common
                   && TrackedActionCatalog.IndependentMonitorCommonActionIds.Contains(action.ActionId));
    }

    private static float GetLocalActionCooldownRemaining(uint actionId) {
        var actionManager = ActionManager.Instance();
        if (actionManager is null) {
            return 0.0f;
        }

        var adjustedActionId = actionManager->GetAdjustedActionId(actionId);
        var remaining = GetLocalActionCooldownRemaining(actionManager, adjustedActionId);
        if (remaining <= 0.05f && adjustedActionId != actionId) {
            remaining = GetLocalActionCooldownRemaining(actionManager, actionId);
        }

        return remaining;
    }

    private static float GetLocalActionCooldownRemaining(ActionManager* actionManager, uint actionId) {
        if (actionId == 0 || !actionManager->IsRecastTimerActive(ActionType.Action, actionId)) {
            return 0.0f;
        }

        var total = actionManager->GetRecastTime(ActionType.Action, actionId);
        var elapsed = actionManager->GetRecastTimeElapsed(ActionType.Action, actionId);
        return Math.Max(0.0f, total - elapsed);
    }
}

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
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly ITargetManager targetManager;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;

    private readonly ExcelSheet<LuminaStatus> statusSheet;
    private readonly ExcelSheet<LuminaClassJob> classJobSheet;
    private readonly record struct ReflectedPropertyCacheKey(Type Type, string Name, bool IgnoreCase);
    private readonly record struct ReflectedPropertyCacheValue(PropertyInfo? Property);
    private static readonly ConcurrentDictionary<ReflectedPropertyCacheKey, ReflectedPropertyCacheValue> ReflectedPropertyCache = new();
    private readonly Dictionary<uint, float> maxDurations = new();
    private readonly Dictionary<CooldownKey, ObservedCooldown> observedCooldowns = new();
    // 每次冷却刷新期内的对象快照，避免 Resolve* 多次全表扫描（O(对象数 × 状态数) 放大）。
    // 用 TryAdd 保留"首个匹配胜出"，与原全表扫描顺序语义一致。
    private readonly Dictionary<ulong, IGameObject> objectLookupByGameObjectId = new();
    // 复刻 ResolveObjectByEntityOrGameObjectId 的 "EntityId==id || GameObjectId==id 按表顺序首个命中" 语义：
    // 按 objectTable 顺序对每个对象先 TryAdd(EntityId) 再 TryAdd(GameObjectId)。
    private readonly Dictionary<ulong, IGameObject> objectLookupByEntityOrGameObjectId = new();
    private bool objectLookupActive;
    // RemoveExpiredCooldowns 复用，避免每次刷新 observedCooldowns.ToArray() 分配。
    private readonly List<CooldownKey> expiredCooldownScratch = new();
    private readonly Dictionary<uint, IReadOnlyList<JobActionCatalogEntry>> jobActionCatalogCache = new();
    private readonly ConcurrentQueue<ObservedActionUse> observedActionUses = new();
    private readonly ConcurrentQueue<ObservedActionUse> recentActionUses = new();
    // 已观测技能使用的保留缓冲：多条冷却栏（个人/团减/独立监控）各自缓存周期不同，
    // 若直接 Dequeue 会被先刷新的栏抢空，导致其他栏丢事件。改为窗口内保留、各栏只读迭代。
    private readonly List<ObservedActionUse> observedActionLog = [];
    private readonly List<RecentActionEntry> recentActions = [];
    private readonly Hook<ReceiveActionEffectDelegate>? actionEffectHook;
    private readonly ExcelSheet<LuminaAction> actionSheet;
    private readonly ExcelSheet<LuminaAddon> addonSheet;
    private readonly string castFallbackName;
    private IReadOnlySet<uint> trackedActionIds = new HashSet<uint>(TrackedDefinitions.ByActionId.Keys);
    private uint lastLocalClassJobId;
    private bool wasLocalPlayerDead;
    private bool wasInDuty;
    private Vector2 pendingNativeContextMenuPosition;
    private int pendingNativeContextMenuRepositionFrames;
    private TaskBarSnapshot? cachedTaskBarSnapshot;
    private DateTime cachedTaskBarSnapshotExpiresAt;
    private IReadOnlyList<PartyCooldownGroupEntry>? cachedPartyCooldownTracking;
    private DateTime cachedPartyCooldownTrackingExpiresAt;
    private IReadOnlyList<PartyCooldownGroupEntry>? cachedPartyCooldownTrackingPreview;
    private DateTime cachedPartyCooldownTrackingPreviewExpiresAt;
    private string cachedPartyCooldownTrackingPreviewKey = string.Empty;
    private IReadOnlyList<StatusEntry>? cachedSelfHudStatuses;
    private DateTime cachedSelfHudStatusesExpiresAt;
    private IReadOnlyList<StatusEntry>? cachedTargetStatuses;
    private ulong cachedTargetStatusesObjectId;
    private DateTime cachedTargetStatusesExpiresAt;
    private const uint FoodStatusId = 48;
    private const int MaxRecentActions = 40;
    // observedActionLog 的保留窗口：需覆盖各冷却栏缓存周期差（最长 250ms）加余量，确保每栏都能至少处理一次。
    private static readonly TimeSpan ObservedActionLogRetention = TimeSpan.FromSeconds(4.0);
    private const int MaxNativeStatusSlots = 60;
    private const ulong PreviewSourceObjectIdBase = 0xCB8000;
    private const uint PreviewSourceEntityIdBase = 0xCB8100;
    private static readonly TimeSpan TaskBarSnapshotCacheDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan PartyCooldownTrackingCacheDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PartyCooldownTrackingPreviewCacheDuration = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan SelfHudStatusCacheDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan TargetStatusCacheDuration = TimeSpan.FromMilliseconds(100);

    public CombatStateTracker(
        IDataManager dataManager,
        IClientState clientState,
        ICondition condition,
        IFramework framework,
        IObjectTable objectTable,
        IPartyList partyList,
        ITargetManager targetManager,
        IGameGui gameGui,
        IGameInteropProvider gameInteropProvider,
        IPluginLog log) {
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.condition = condition;
        this.framework = framework;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.targetManager = targetManager;
        this.gameGui = gameGui;
        this.log = log;
        this.statusSheet = this.dataManager.GetExcelSheet<LuminaStatus>();
        this.classJobSheet = this.dataManager.GetExcelSheet<LuminaClassJob>();
        this.actionSheet = this.dataManager.GetExcelSheet<LuminaAction>();
        this.addonSheet = this.dataManager.GetExcelSheet<LuminaAddon>();
        this.castFallbackName = this.addonSheet.TryGetRow(1032, out var castReadyingRow) && !string.IsNullOrWhiteSpace(castReadyingRow.Text.ToString())
            ? castReadyingRow.Text.ToString()
            : string.Empty;

        try {
            this.actionEffectHook = gameInteropProvider.HookFromAddress<ReceiveActionEffectDelegate>(
                ActionEffectHandler.Addresses.Receive.Value,
                OnReceiveActionEffect);
            this.actionEffectHook.Enable();
        }
        catch (Exception ex) {
            this.log.Warning(ex, "无法启用技能释放监听，队友 CD 将只使用状态估算", []);
        }

        InitializeLifecycleSnapshot();
        this.clientState.TerritoryChanged += OnTerritoryChanged;
        this.condition.ConditionChange += OnConditionChanged;
        this.framework.Update += OnFrameworkUpdate;
    }

    public void Dispose() {
        this.clientState.TerritoryChanged -= OnTerritoryChanged;
        this.condition.ConditionChange -= OnConditionChanged;
        this.framework.Update -= OnFrameworkUpdate;
        this.actionEffectHook?.Dispose();
    }

    public bool IsPartyCooldownTrackingActive => IsInDuty();

    public bool IsInDutyActive => IsInDuty();

    public bool IsPartyStatusTrackingActive => this.objectTable.LocalPlayer is not null;

    public TaskBarSnapshot? GetTaskBarSnapshot() {
        var now = DateTime.UtcNow;
        if (this.cachedTaskBarSnapshot is not null && now < this.cachedTaskBarSnapshotExpiresAt) {
            return this.cachedTaskBarSnapshot;
        }

        var player = this.objectTable.LocalPlayer;
        if (player is null) {
            this.cachedTaskBarSnapshot = null;
            return null;
        }

        var classJobId = player.ClassJob.RowId;
        this.cachedTaskBarSnapshot = new TaskBarSnapshot(
            player.Name.ToString(),
            ResolveClassJobName(classJobId),
            classJobId,
            player.CurrentHp,
            player.MaxHp,
            player.CurrentMp,
            player.MaxMp,
            this.clientState.TerritoryType,
            $"区域 #{this.clientState.TerritoryType}");
        this.cachedTaskBarSnapshotExpiresAt = now.Add(TaskBarSnapshotCacheDuration);
        return this.cachedTaskBarSnapshot;
    }

    private void InitializeLifecycleSnapshot() {
        var player = this.objectTable.LocalPlayer;
        this.lastLocalClassJobId = player?.ClassJob.RowId ?? 0;
        this.wasLocalPlayerDead = player is not null && player.CurrentHp == 0;
        this.wasInDuty = IsInDuty();
    }

    private void OnTerritoryChanged(uint territoryType) {
        ResetObservedCooldowns($"territory changed: {territoryType}");
        InitializeLifecycleSnapshot();
    }

    private void OnConditionChanged(ConditionFlag flag, bool value) {
        if (flag is ConditionFlag.BoundByDuty or ConditionFlag.BoundByDuty56 or ConditionFlag.InDeepDungeon) {
            ResetObservedCooldowns($"duty condition changed: {flag}={value}");
            this.wasInDuty = IsInDuty();
        }
    }

    private void OnFrameworkUpdate(IFramework framework) {
        ProcessPendingNativeContextMenuPosition();

        var player = this.objectTable.LocalPlayer;
        if (player is null) {
            return;
        }

        var isInDuty = IsInDuty();
        if (isInDuty != this.wasInDuty) {
            ResetObservedCooldowns($"duty state changed: {this.wasInDuty} -> {isInDuty}");
            this.wasInDuty = isInDuty;
        }

        var classJobId = player.ClassJob.RowId;
        if (classJobId != 0 && this.lastLocalClassJobId != 0 && classJobId != this.lastLocalClassJobId) {
            ResetObservedCooldowns($"class/job changed: {this.lastLocalClassJobId} -> {classJobId}");
        }

        if (classJobId != 0) {
            this.lastLocalClassJobId = classJobId;
        }

        var isDead = player.CurrentHp == 0;
        if (isDead && !this.wasLocalPlayerDead) {
            ResetObservedCooldowns("local player died");
        }

        this.wasLocalPlayerDead = isDead;
    }

    private void ResetObservedCooldowns(string reason) {
        this.observedCooldowns.Clear();

        while (this.observedActionUses.TryDequeue(out _)) {
        }

        this.cachedPartyCooldownTracking = null;
        this.cachedSelfHudStatuses = null;
        this.cachedTargetStatuses = null;

        this.log.Debug($"已重置队伍技能冷却记录：{reason}");
    }

    private bool IsInDuty() {
        return this.condition.Any(ConditionFlag.BoundByDuty, ConditionFlag.BoundByDuty56, ConditionFlag.InDeepDungeon);
    }



    readonly record struct ObservedActionUse(uint CasterEntityId, uint ActionId, DateTime ObservedAt);

    private unsafe delegate void ReceiveActionEffectDelegate(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds);

    sealed class ObservedCooldown {
        public ObservedCooldown(
            TrackedStatusDefinition definition,
            uint iconId,
            string sourceName,
            string sourceJobName,
            ulong sourceObjectId,
            DateTime readyAt,
            DateTime lastSeenAt,
            bool isActive,
            float activeRemainingSeconds,
            CooldownObservationKind observationKind) {
            Definition = definition;
            IconId = iconId;
            SourceName = sourceName;
            SourceJobName = sourceJobName;
            SourceObjectId = sourceObjectId;
            ReadyAt = readyAt;
            LastSeenAt = lastSeenAt;
            IsActive = isActive;
            ActiveRemainingSeconds = activeRemainingSeconds;
            ActiveUpdatedAt = lastSeenAt;
            ObservationKind = observationKind;
        }

        public TrackedStatusDefinition Definition { get; set; }
        public uint IconId { get; set; }
        public string SourceName { get; set; }
        public string SourceJobName { get; set; }
        public ulong SourceObjectId { get; set; }
        public DateTime ReadyAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public DateTime ActiveUpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public float ActiveRemainingSeconds { get; set; }
        public CooldownObservationKind ObservationKind { get; set; }

        public CooldownEntry ToEntry() => new(
            Definition.StatusId,
            Definition.ActionIds.FirstOrDefault(),
            IconId,
            Definition.Name,
            Definition.Group,
            Definition.CooldownSeconds,
            Definition.DurationSeconds,
            SourceName,
            SourceJobName,
            SourceObjectId,
            ReadyAt,
            LastSeenAt,
            IsActive,
            ActiveRemainingSeconds,
            ObservationKind);
    }
}

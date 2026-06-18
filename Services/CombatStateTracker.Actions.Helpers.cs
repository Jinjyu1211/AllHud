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
    public uint GetStatusIconId(uint statusId) {
        return this.statusSheet.TryGetRow(statusId, out var row) ? row.Icon : 0;
    }

    private uint GetActionIconId(uint actionId) {
        return this.actionSheet.TryGetRow(actionId, out var row) ? (uint)row.Icon : 0;
    }

    private string ResolveCastActionName(uint actionId) {
        if (actionId == 0) {
            return string.Empty;
        }

        // Try the Action sheet first. Boss/NPC casts live here too, but many of them
        // have an empty Name field on purpose, so a row hit does not guarantee a name.
        if (this.actionSheet.TryGetRow(actionId, out var row)) {
            var name = row.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name)) {
                return name;
            }
        }

        // Name missing (empty Name field or row not present): show the generic
        // "Readying" addon text (Addon 1032) instead of a raw action id, like DailyRoutines.
        return !string.IsNullOrWhiteSpace(this.castFallbackName)
            ? this.castFallbackName
            : $"Action {actionId}";
    }
    private string GetActionName(uint actionId) {
        if (this.actionSheet.TryGetRow(actionId, out var row)) {
            var name = row.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name)) {
                return name;
            }
        }

        return $"Action {actionId}";
    }

    private static bool IsPvPAction(object row) {
        return GetBoolProperty(row, "IsPvP") || GetBoolProperty(row, "IsPvPAction");
    }

    private static bool IsActionAvailableToClassJob(object actionRow, uint classJobId) {
        var category = GetRowReferenceValue(actionRow, "ClassJobCategory");
        if (category is null) {
            return false;
        }

        foreach (var propertyName in GetClassJobCategoryPropertyNames(classJobId)) {
            if (GetBoolProperty(category, propertyName)) {
                return true;
            }
        }

        return false;
    }

    private static object? GetRowReferenceValue(object row, string propertyName) {
        var property = FindProperty(row, propertyName, false);
        var value = property?.GetValue(row);
        if (value is null) {
            return null;
        }

        var type = value.GetType();
        foreach (var nestedPropertyName in new[] { "ValueNullable", "Value" }) {
            var nestedProperty = FindProperty(type, nestedPropertyName, false);
            var nested = nestedProperty?.GetValue(value);
            if (nested is not null && !ReferenceEquals(nested, value)) {
                return nested;
            }
        }

        return value;
    }

    private static IEnumerable<string> GetClassJobCategoryPropertyNames(uint classJobId) {
        foreach (var name in GetSingleClassJobCategoryPropertyNames(classJobId)) {
            yield return name;
        }
    }

    private static IEnumerable<string> GetSingleClassJobCategoryPropertyNames(uint classJobId) {
        var names = classJobId switch {
            1 => new[] { "GLA", "Gladiator" },
            2 => new[] { "PGL", "Pugilist" },
            3 => new[] { "MRD", "Marauder" },
            4 => new[] { "LNC", "Lancer" },
            5 => new[] { "ARC", "Archer" },
            6 => new[] { "CNJ", "Conjurer" },
            7 => new[] { "THM", "Thaumaturge" },
            19 => new[] { "PLD", "Paladin" },
            20 => new[] { "MNK", "Monk" },
            21 => new[] { "WAR", "Warrior" },
            22 => new[] { "DRG", "Dragoon" },
            23 => new[] { "BRD", "Bard" },
            24 => new[] { "WHM", "WhiteMage" },
            25 => new[] { "BLM", "BlackMage" },
            26 => new[] { "ACN", "Arcanist" },
            27 => new[] { "SMN", "Summoner" },
            28 => new[] { "SCH", "Scholar" },
            29 => new[] { "ROG", "Rogue" },
            30 => new[] { "NIN", "Ninja" },
            31 => new[] { "MCH", "Machinist" },
            32 => new[] { "DRK", "DarkKnight" },
            33 => new[] { "AST", "Astrologian" },
            34 => new[] { "SAM", "Samurai" },
            35 => new[] { "RDM", "RedMage" },
            36 => new[] { "BLU", "BlueMage" },
            37 => new[] { "GNB", "Gunbreaker" },
            38 => new[] { "DNC", "Dancer" },
            39 => new[] { "RPR", "Reaper" },
            40 => new[] { "SGE", "Sage" },
            41 => new[] { "VPR", "Viper" },
            42 => new[] { "PCT", "Pictomancer" },
            _ => Array.Empty<string>(),
        };

        foreach (var name in names) {
            yield return name;
        }
    }

    private static float GetActionCooldownSeconds(object row) {
        var recast100Ms = GetNumericProperty(row, "Recast100ms");
        if (recast100Ms > 0) {
            return recast100Ms / 10.0f;
        }

        var recastSeconds = GetFloatProperty(row, "RecastSeconds");
        if (recastSeconds > 0.0f) {
            return recastSeconds;
        }

        var recast = GetFloatProperty(row, "Recast");
        return Math.Max(0.0f, recast);
    }

    private static uint GetActionCooldownGroupId(object row) {
        foreach (var propertyName in new[] { "AdditionalCooldownGroup", "EquivalenceGroup" }) {
            var value = GetNumericProperty(row, propertyName);
            if (value != 0) {
                return value;
            }
        }

        return 0;
    }

    private static uint GetRowReferenceId(object row, string propertyName) {
        var property = FindProperty(row, propertyName, false);
        if (property is null) {
            return 0;
        }

        return ExtractRowId(property.GetValue(row));
    }

    private static uint ExtractRowId(object? value) {
        if (value is null) {
            return 0;
        }

        if (TryConvertUInt(value, out var numeric)) {
            return numeric;
        }

        var type = value.GetType();
        foreach (var propertyName in new[] { "RowId", "Id" }) {
            var property = FindProperty(type, propertyName, false);
            if (property is not null && TryConvertUInt(property.GetValue(value), out numeric)) {
                return numeric;
            }
        }

        foreach (var propertyName in new[] { "ValueNullable", "Value" }) {
            var property = FindProperty(type, propertyName, false);
            var nested = property?.GetValue(value);
            if (nested is not null && !ReferenceEquals(nested, value)) {
                var rowId = ExtractRowId(nested);
                if (rowId != 0) {
                    return rowId;
                }
            }
        }

        return 0;
    }

    private static uint GetNumericProperty(object row, string propertyName) {
        var property = FindProperty(row, propertyName, false);
        return property is not null && TryConvertUInt(property.GetValue(row), out var value) ? value : 0;
    }

    private static float GetFloatProperty(object row, string propertyName) {
        var property = FindProperty(row, propertyName, false);
        var value = property?.GetValue(row);
        return value switch {
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            decimal decimalValue => (float)decimalValue,
            byte byteValue => byteValue,
            sbyte sbyteValue => sbyteValue,
            short shortValue => shortValue,
            ushort ushortValue => ushortValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            long longValue => longValue,
            ulong ulongValue => ulongValue,
            _ => 0.0f,
        };
    }

    private static bool GetBoolProperty(object row, string propertyName) {
        var property = FindProperty(row, propertyName, true);
        return property?.GetValue(row) is true;
    }

    private static bool TryGetBoolProperty(object row, out bool result, params string[] propertyNames) {
        foreach (var propertyName in propertyNames) {
            var property = FindProperty(row, propertyName, true);
            if (property?.GetValue(row) is bool value) {
                result = value;
                return true;
            }
        }

        result = false;
        return false;
    }

    private static bool TryGetUIntProperty(object row, out uint result, params string[] propertyNames) {
        foreach (var propertyName in propertyNames) {
            var property = FindProperty(row, propertyName, true);
            if (property is not null && TryConvertUInt(property.GetValue(row), out result)) {
                return true;
            }
        }

        result = 0;
        return false;
    }

    private static bool TryGetFloatProperty(object row, out float result, params string[] propertyNames) {
        foreach (var propertyName in propertyNames) {
            var property = FindProperty(row, propertyName, true);
            if (property is null) {
                continue;
            }

            var value = property.GetValue(row);
            switch (value) {
                case float floatValue:
                    result = floatValue;
                    return true;
                case double doubleValue:
                    result = (float)doubleValue;
                    return true;
                case decimal decimalValue:
                    result = (float)decimalValue;
                    return true;
                case byte byteValue:
                    result = byteValue;
                    return true;
                case sbyte sbyteValue:
                    result = sbyteValue;
                    return true;
                case short shortValue:
                    result = shortValue;
                    return true;
                case ushort ushortValue:
                    result = ushortValue;
                    return true;
                case int intValue:
                    result = intValue;
                    return true;
                case uint uintValue:
                    result = uintValue;
                    return true;
                case long longValue:
                    result = longValue;
                    return true;
                case ulong ulongValue:
                    result = ulongValue;
                    return true;
            }
        }

        result = 0.0f;
        return false;
    }

    private static PropertyInfo? FindProperty(object row, string propertyName, bool ignoreCase = true) {
        return FindProperty(row.GetType(), propertyName, ignoreCase);
    }

    private static PropertyInfo? FindProperty(Type type, string propertyName, bool ignoreCase = true) {
        var key = new ReflectedPropertyCacheKey(type, propertyName, ignoreCase);
        return ReflectedPropertyCache.GetOrAdd(key, static key => {
            var comparison = key.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var property = key.Type.GetProperty(key.Name, BindingFlags.Instance | BindingFlags.Public)
                           ?? key.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(candidate =>
                               string.Equals(candidate.Name, key.Name, comparison));
            return new ReflectedPropertyCacheValue(property);
        }).Property;
    }

    private static bool TryConvertUInt(object? value, out uint result) {
        result = 0;
        switch (value) {
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue when sbyteValue >= 0:
                result = (uint)sbyteValue;
                return true;
            case short shortValue when shortValue >= 0:
                result = (uint)shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case int intValue when intValue >= 0:
                result = (uint)intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue when longValue >= 0 && longValue <= uint.MaxValue:
                result = (uint)longValue;
                return true;
            case ulong ulongValue when ulongValue <= uint.MaxValue:
                result = (uint)ulongValue;
                return true;
            default:
                return false;
        }
    }

    private uint GetDefinitionIconId(TrackedStatusDefinition definition) {
        foreach (var actionId in definition.ActionIds) {
            var actionIconId = GetActionIconId(actionId);
            if (actionIconId != 0) {
                return actionIconId;
            }
        }

        var statusIconId = GetStatusIconId(definition.StatusId);
        if (statusIconId != 0) {
            return statusIconId;
        }

        return 0;
    }

    private static uint GetDefinitionKeyId(TrackedStatusDefinition definition) {
        return definition.StatusId != 0 ? definition.StatusId : definition.ActionIds.FirstOrDefault();
    }

    private void UpsertCooldown(
        TrackedStatusDefinition definition,
        string sourceName,
        string sourceJobName,
        ulong sourceObjectId,
        DateTime readyAt,
        DateTime observedAt,
        bool isActive,
        float activeRemainingSeconds,
        CooldownObservationKind observationKind) {
        var key = new CooldownKey(GetDefinitionKeyId(definition), sourceObjectId, definition.Group);
        var iconId = GetDefinitionIconId(definition);
        if (!this.observedCooldowns.TryGetValue(key, out var observed)) {
            this.observedCooldowns[key] = new ObservedCooldown(
                definition,
                iconId,
                sourceName,
                sourceJobName,
                sourceObjectId,
                readyAt,
                observedAt,
                isActive && activeRemainingSeconds > 0.05f,
                Math.Max(0.0f, activeRemainingSeconds),
                observationKind);
            return;
        }

        observed.IconId = iconId;
        observed.Definition = definition;
        if (!string.IsNullOrWhiteSpace(sourceJobName)) {
            observed.SourceJobName = sourceJobName;
        }

        observed.LastSeenAt = observedAt;

        if (isActive && activeRemainingSeconds > 0.05f) {
            observed.IsActive = true;
            observed.ActiveRemainingSeconds = activeRemainingSeconds;
            observed.ActiveUpdatedAt = observedAt;
        }
        else if (isActive) {
            observed.IsActive = false;
            observed.ActiveRemainingSeconds = 0.0f;
            observed.ActiveUpdatedAt = observedAt;
        }

        if (GetObservationPriority(observationKind) >= GetObservationPriority(observed.ObservationKind)
            || observed.ReadyAt <= DateTime.UtcNow) {
            observed.SourceName = sourceName;
            observed.SourceJobName = sourceJobName;
            observed.SourceObjectId = sourceObjectId;
            observed.ReadyAt = readyAt;
            observed.ObservationKind = observationKind;
        }
    }

    private static int GetObservationPriority(CooldownObservationKind observationKind) {
        return observationKind switch {
            CooldownObservationKind.LocalRecast => 2,
            CooldownObservationKind.ActionEvent => 1,
            _ => 0,
        };
    }

    private void RemoveExpiredCooldowns(Configuration config) {
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-Math.Max(1.0f, config.ExpiredCooldownGraceSeconds));
        this.expiredCooldownScratch.Clear();
        // 直接遍历字典值更新活跃剩余时间（就地修改引用对象，安全）；
        // 待删除的 key 收集到复用列表，循环后统一删除，避免每帧 ToArray() 分配整表快照。
        foreach (var pair in this.observedCooldowns) {
            var observed = pair.Value;
            if (observed.IsActive) {
                if (observed.ActiveRemainingSeconds > 0.0f) {
                    observed.ActiveRemainingSeconds = Math.Max(
                        0.0f,
                        observed.ActiveRemainingSeconds - (float)(now - observed.ActiveUpdatedAt).TotalSeconds);
                    observed.ActiveUpdatedAt = now;
                }

                if (observed.ActiveRemainingSeconds <= 0.05f) {
                    observed.IsActive = false;
                    observed.ActiveRemainingSeconds = 0.0f;
                }
            }

            if (config.HideExpiredCooldowns && observed.ReadyAt < cutoff) {
                this.expiredCooldownScratch.Add(pair.Key);
            }
        }

        foreach (var key in this.expiredCooldownScratch) {
            this.observedCooldowns.Remove(key);
        }

        this.expiredCooldownScratch.Clear();
    }

    private void OnReceiveActionEffect(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds) {
        try {
            if (header is not null
                && header->ActionType == (byte)ActionType.Action
                && header->ActionId != 0) {
                var observed = new ObservedActionUse(casterEntityId, header->ActionId, DateTime.UtcNow);
                this.recentActionUses.Enqueue(observed);

                if (this.trackedActionIds.Contains(header->ActionId)) {
                    this.observedActionUses.Enqueue(observed);
                }
            }
        }
        catch (Exception ex) {
            this.log.Warning(ex, "处理技能释放监听时发生错误", []);
        }
        finally {
            this.actionEffectHook!.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);
        }
    }

    readonly record struct CooldownKey(uint StatusId, ulong SourceObjectId, CooldownGroup Group);

    readonly record struct PartyMemberSnapshot(
        int SortIndex,
        string Name,
        string JobName,
        uint ClassJobId,
        uint EntityId,
        ulong GameObjectId,
        bool IsLocalPlayer);
}

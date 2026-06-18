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
    public IReadOnlyList<StatusEntry> GetSelfStatuses(Configuration config) {
        var player = this.objectTable.LocalPlayer;
        if (player is null) {
            return [];
        }

        return ScanBattleChara(player, player.GameObjectId, player.Name.ToString(), config)
            .Where(status => status.IsBuff || config.ShowRawStatusIds || IsCustomStatusType(status.StatusId, config, CustomTrackType.SelfStatus))
            .OrderByDescending(status => status.IsSelfApplied)
            .ThenBy(status => status.RemainingSeconds)
            .ToList();
    }

    public IReadOnlyList<StatusEntry> GetSelfHudStatuses(Configuration config) {
        var player = this.objectTable.LocalPlayer;
        if (player is null) {
            this.cachedSelfHudStatuses = null;
            return [];
        }

        var now = DateTime.UtcNow;
        if (this.cachedSelfHudStatuses is not null && now < this.cachedSelfHudStatusesExpiresAt) {
            return this.cachedSelfHudStatuses;
        }

        this.cachedSelfHudStatuses = ScanBattleChara(player, player.GameObjectId, player.Name.ToString(), config)
            .OrderBy(status => status.StatusIndex)
            .ThenBy(status => status.RemainingSeconds)
            .ToList();
        this.cachedSelfHudStatusesExpiresAt = now + SelfHudStatusCacheDuration;
        return this.cachedSelfHudStatuses;
    }

    public IReadOnlyList<StatusEntry> GetSelfStatusCandidates(Configuration config) {
        var player = this.objectTable.LocalPlayer;
        if (player is null) {
            return [];
        }

        return ScanBattleChara(player, player.GameObjectId, player.Name.ToString(), config)
            .Where(status => status.IsBuff)
            .OrderByDescending(status => status.IsSelfApplied)
            .ThenBy(status => status.RemainingSeconds)
            .ToList();
    }

    public IReadOnlyList<StatusEntry> GetTargetStatuses(Configuration config) {
        if (this.targetManager.Target is not IBattleChara target) {
            return [];
        }

        var player = this.objectTable.LocalPlayer;
        var playerId = player?.GameObjectId ?? 0;
        var targetName = target.Name.ToString();

        return ScanBattleChara(target, playerId, targetName, config)
            .Where(status => !status.IsBuff)
            .Where(status => !config.OnlyShowSelfAppliedTargetStatuses || status.IsSelfApplied || IsTrackedTargetStatus(status.StatusId, config))
            .Where(status => config.ShowRawStatusIds || status.MaxSeconds <= config.MaxTargetStatusDurationSeconds || IsTrackedTargetStatus(status.StatusId, config))
            .OrderByDescending(status => status.IsSelfApplied)
            .ThenBy(status => status.RemainingSeconds)
            .ToList();
    }

    public IReadOnlyList<StatusEntry> GetTargetStatusCandidates(Configuration config) {
        if (this.targetManager.Target is not IBattleChara target) {
            return [];
        }

        var player = this.objectTable.LocalPlayer;
        var playerId = player?.GameObjectId ?? 0;

        return ScanBattleChara(target, playerId, target.Name.ToString(), config)
            .Where(status => !status.IsBuff)
            .OrderByDescending(status => status.IsSelfApplied)
            .ThenBy(status => status.RemainingSeconds)
            .ToList();
    }

}

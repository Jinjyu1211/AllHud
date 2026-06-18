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
    public TargetInfoEntry? GetTargetInfo(Configuration config) {
        if (!TryGetAvailableWorldPlayer(out var player)) {
            return null;
        }

        if (this.targetManager.Target is not IBattleChara target) {
            return null;
        }

        if (!IsObjectInCurrentTable(target.GameObjectId)) {
            return null;
        }

        var playerId = player.GameObjectId;
        var statuses = GetCachedTargetStatuses(target, playerId, config);

        var castActionId = target.IsCasting ? target.CastActionId : 0;
        var targetOfTarget = CreateTargetOfTargetEntry(target.TargetObject);
        return new TargetInfoEntry(
            target.GameObjectId,
            target.BaseId,
            target.Name.ToString(),
            target.Level,
            target.CurrentHp,
            target.MaxHp,
            target.IsCasting,
            target.IsCastInterruptible,
            castActionId,
            ResolveCastActionName(castActionId),
            target.IsCasting ? target.CurrentCastTime : 0.0f,
            target.IsCasting ? target.TotalCastTime : 0.0f,
            targetOfTarget,
            statuses);
    }

    private IReadOnlyList<StatusEntry> GetCachedTargetStatuses(IBattleChara target, ulong playerId, Configuration config) {
        var now = DateTime.UtcNow;
        if (this.cachedTargetStatuses is not null
            && this.cachedTargetStatusesObjectId == target.GameObjectId
            && now < this.cachedTargetStatusesExpiresAt) {
            return this.cachedTargetStatuses;
        }

        this.cachedTargetStatuses = ScanBattleChara(target, playerId, target.Name.ToString(), config)
            .Where(status => !config.OnlyShowSelfAppliedTargetStatuses || status.IsSelfApplied || status.IsBuff || IsTrackedTargetStatus(status.StatusId, config))
            .Where(status => config.ShowRawStatusIds
                             || status.MaxSeconds <= config.MaxTargetStatusDurationSeconds
                             || IsTrackedTargetStatus(status.StatusId, config)
                             || status.IsBuff)
            .OrderByDescending(status => status.IsSelfApplied)
            .ThenBy(status => status.IsBuff ? 1 : 0)
            .ThenBy(status => status.StatusIndex)
            .ThenBy(status => status.RemainingSeconds)
            .ToList();
        this.cachedTargetStatusesObjectId = target.GameObjectId;
        this.cachedTargetStatusesExpiresAt = now + TargetStatusCacheDuration;
        return this.cachedTargetStatuses;
    }

    private bool TryGetAvailableWorldPlayer([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IPlayerCharacter? player) {
        player = this.objectTable.LocalPlayer;
        return player is not null
               && player.GameObjectId != 0
               && player.GameObjectId != ulong.MaxValue
               && this.clientState.TerritoryType != 0
               && !this.condition.Any(ConditionFlag.BetweenAreas, ConditionFlag.BetweenAreas51);
    }

    private bool IsObjectInCurrentTable(ulong objectId) {
        if (objectId == 0 || objectId == ulong.MaxValue) {
            return false;
        }

        foreach (var gameObject in this.objectTable) {
            if (gameObject?.GameObjectId == objectId && gameObject.Address != IntPtr.Zero) {
                return true;
            }
        }

        return false;
    }

    public void SelectTargetOfCurrentTarget() {
        var targetOfTarget = this.targetManager.Target?.TargetObject;
        if (targetOfTarget is not null) {
            this.targetManager.Target = targetOfTarget;
        }
    }

    public bool SelectTargetByObjectId(ulong objectId) {
        if (objectId == 0 || objectId == ulong.MaxValue) {
            return false;
        }

        foreach (var gameObject in this.objectTable) {
            if (gameObject?.GameObjectId != objectId) {
                continue;
            }

            this.targetManager.Target = gameObject;
            return true;
        }

        return false;
    }

    public bool OpenNativeContextMenuByObjectId(ulong objectId, Vector2? menuPosition = null) {
        if (objectId == 0 || objectId == ulong.MaxValue) {
            return false;
        }

        var agentHud = AgentModule.Instance()->GetAgentHUD();
        if (agentHud is null) {
            return false;
        }

        foreach (var gameObject in this.objectTable) {
            if (gameObject?.GameObjectId != objectId || gameObject.Address == IntPtr.Zero) {
                continue;
            }

            agentHud->OpenContextMenuFromTarget((StructGameObject*)gameObject.Address);
            if (menuPosition is { } position) {
                this.pendingNativeContextMenuPosition = position;
                this.pendingNativeContextMenuRepositionFrames = 6;
                ProcessPendingNativeContextMenuPosition();
            }

            return true;
        }

        return false;
    }

    private void ProcessPendingNativeContextMenuPosition() {
        if (this.pendingNativeContextMenuRepositionFrames <= 0) {
            return;
        }

        this.pendingNativeContextMenuRepositionFrames--;
        TryMoveNativeContextMenuAddon("ContextMenu", this.pendingNativeContextMenuPosition);
        TryMoveNativeContextMenuAddon("ContextIconMenu", this.pendingNativeContextMenuPosition);
        TryMoveNativeContextMenuAddon("AddonContextSub", this.pendingNativeContextMenuPosition);
    }

    private unsafe void TryMoveNativeContextMenuAddon(string addonName, Vector2 position) {
        var addonPtr = this.gameGui.GetAddonByName(addonName);
        if (addonPtr.IsNull) {
            return;
        }

        var addon = (AtkUnitBase*)addonPtr.Address;
        if (addon is null || !addon->IsVisible) {
            return;
        }

        var x = (short)Math.Clamp(MathF.Round(position.X), short.MinValue, short.MaxValue);
        var y = (short)Math.Clamp(MathF.Round(position.Y), short.MinValue, short.MaxValue);
        addon->SetPosition(x, y);
    }

    private static TargetOfTargetEntry? CreateTargetOfTargetEntry(IGameObject? targetOfTarget) {
        if (targetOfTarget is not ICharacter character || character.MaxHp == 0) {
            return null;
        }

        return new TargetOfTargetEntry(
            character.GameObjectId,
            character.Name.ToString(),
            character.CurrentHp,
            character.MaxHp);
    }
}

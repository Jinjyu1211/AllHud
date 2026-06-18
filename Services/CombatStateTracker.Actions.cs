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
    public IReadOnlyList<RecentActionEntry> GetRecentActionCandidates() {
        ProcessRecentActionUses();
        return this.recentActions.ToList();
    }

    public IReadOnlyList<JobActionCatalogEntry> GetJobActionCatalog(uint classJobId) {
        if (this.jobActionCatalogCache.TryGetValue(classJobId, out var cached)) {
            return cached;
        }

        var result = new List<JobActionCatalogEntry>();
        foreach (var knownSkill in TrackedActionCatalog.GetSkillsForJob(classJobId)) {
            if (knownSkill.Definition.ActionIds.FirstOrDefault() == 0
                || result.Any(action => knownSkill.Definition.ActionIds.Contains(action.ActionId))) {
                continue;
            }

            result.Add(CreateKnownJobActionCatalogEntry(classJobId, knownSkill));
        }

        cached = result
            .GroupBy(action => action.ActionId)
            .Select(group => group.OrderByDescending(action => action.Level).First())
            .OrderBy(action => action.Level)
            .ThenBy(action => action.Name)
            .ToList();
        this.jobActionCatalogCache[classJobId] = cached;
        return cached;
    }

    public IReadOnlyList<JobActionCatalogEntry> GetCommonActionCatalog() {
        return TrackedActionCatalog.CommonSkills
            .Select(skill => CreateKnownJobActionCatalogEntry(0, skill))
            .OrderBy(action => action.Group)
            .ThenBy(action => action.Name)
            .ToList();
    }

    public uint GetClassJobIconId(uint classJobId) {
        if (classJobId == 0 || !this.classJobSheet.TryGetRow(classJobId, out var row)) {
            return 0;
        }

        // Colored class/job icons live in the 062100 icon range and follow the ClassJob row id.
        // Example: Paladin ClassJob 19 => icon 062119.
        return 62100u + classJobId;
    }

    private string ResolveClassJobName(uint classJobId) {
        if (classJobId == 0 || !this.classJobSheet.TryGetRow(classJobId, out var row)) {
            return string.Empty;
        }

        var name = row.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? GetJobName(classJobId) : name;
    }

    private JobActionCatalogEntry CreateKnownJobActionCatalogEntry(uint classJobId, TrackedActionDefinition skill) {
        var actionId = skill.Definition.ActionIds.First();
        var cooldownSeconds = skill.Definition.CooldownSeconds;
        var iconId = GetActionIconId(actionId);
        var level = 0u;

        if (this.actionSheet.TryGetRow(actionId, out var row)) {
            var boxed = (object)row;
            cooldownSeconds = Math.Max(cooldownSeconds, GetActionCooldownSeconds(boxed));
            level = GetNumericProperty(boxed, "ClassJobLevel");
        }

        return new JobActionCatalogEntry(
            TrackedActionCatalog.GetActionKey(classJobId, actionId),
            classJobId,
            actionId,
            iconId,
            skill.Definition.Name,
            level,
            cooldownSeconds,
            0,
            skill.Definition.Group,
            skill.Definition.StatusId > 0,
            skill.IsSharedSkill);
    }

}

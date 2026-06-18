using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Game.Addon.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Config;
using Dalamud.Game.Inventory;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using LuminaGeneralAction = Lumina.Excel.Sheets.GeneralAction;
using LuminaItem = Lumina.Excel.Sheets.Item;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaMainCommand = Lumina.Excel.Sheets.MainCommand;
using LuminaMainCommandCategory = Lumina.Excel.Sheets.MainCommandCategory;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace AllHud.Windows;

public sealed partial class OverlayRenderer {
    private unsafe void DrawGearsetSwitcherPopup(float opacity, float scale) {
        DrawSimpleTaskBarPopup(GearsetPopupId, new Vector2(430.0f * scale, 360.0f * scale), opacity, scale, () => {
            var module = UIModule.Instance()->GetRaptureGearsetModule();
            if (module is null) {
                ImGui.TextDisabled("当前无法读取套装切换模块。请稍后重试。");
                return;
            }

            var currentGearsetIndex = module->CurrentGearsetIndex;
            var popupCache = GetGearsetPopupCache(module, currentGearsetIndex);
            if (popupCache.Entries.Count == 0) {
                ImGui.TextDisabled("没有可用的套装。请先在游戏里创建套装。");
                return;
            }

            using (PushTaskBarPopupScrollbarStyle(scale)) {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1.0f, 0.88f, 0.94f, 0.46f));
                if (ImGui.BeginChild("##AllHudGearsetList", new Vector2(0.0f, 320.0f * scale), true)) {
                    DrawGearsetPopupGroupedList(popupCache.LeftGroups, popupCache.RightGroups, scale, opacity);
                }

                ImGui.EndChild();
                ImGui.PopStyleColor();
            }
        });
    }

    private unsafe GearsetPopupCache GetGearsetPopupCache(RaptureGearsetModule* module, int currentGearsetIndex) {
        var now = DateTime.UtcNow;
        if (this.gearsetPopupCache is { } cached && cached.CurrentGearsetIndex == currentGearsetIndex && now < cached.RefreshAt) {
            return cached;
        }

        var fingerprint = GetGearsetPopupFingerprint(module, currentGearsetIndex);
        if (this.gearsetPopupCache is { } freshCached && freshCached.Fingerprint == fingerprint) {
            freshCached = freshCached with { CurrentGearsetIndex = currentGearsetIndex, RefreshAt = now.Add(GearsetPopupCacheDuration) };
            this.gearsetPopupCache = freshCached;
            return freshCached;
        }

        var entries = BuildGearsetPopupEntries(module, currentGearsetIndex);
        var cache = new GearsetPopupCache(
            fingerprint,
            currentGearsetIndex,
            now.Add(GearsetPopupCacheDuration),
            entries,
            BuildGearsetPopupGroups(entries, GearsetPopupLeftGroups),
            BuildGearsetPopupGroups(entries, GearsetPopupRightGroups));
        this.gearsetPopupCache = cache;
        return cache;
    }

    private unsafe int GetGearsetPopupFingerprint(RaptureGearsetModule* module, int currentGearsetIndex) {
        var hash = new HashCode();
        hash.Add(currentGearsetIndex);
        for (byte id = 0; id < 100; id++) {
            var valid = module->IsValidGearset(id);
            hash.Add(valid);
            if (!valid) {
                continue;
            }

            var gearset = module->Entries[id];
            hash.Add(id);
            hash.Add(gearset.ClassJob);
            hash.Add(gearset.NameString, StringComparer.Ordinal);
            var metadata = GetGearsetJobMetadata(gearset.ClassJob);
            hash.Add(GetGearsetJobLevel(metadata));
        }

        return hash.ToHashCode();
    }

    private unsafe List<GearsetPopupEntry> BuildGearsetPopupEntries(RaptureGearsetModule* module, int currentGearsetIndex) {
        var entries = new List<GearsetPopupEntry>();
        for (byte id = 0; id < 100; id++) {
            if (!module->IsValidGearset(id)) {
                continue;
            }

            var gearset = module->Entries[id];
            var metadata = GetGearsetJobMetadata(gearset.ClassJob);
            var name = gearset.NameString;
            if (string.IsNullOrWhiteSpace(name)) {
                name = string.IsNullOrWhiteSpace(metadata.Name) ? $"套装 {id + 1}" : metadata.Name;
            }

            entries.Add(new GearsetPopupEntry(
                id,
                name,
                gearset.ClassJob,
                GetGearsetJobLevel(metadata),
                metadata.Group,
                metadata.GroupSort,
                metadata.JobSort,
                metadata.IconId,
                currentGearsetIndex == id));
        }

        return entries
            .OrderBy(entry => entry.GroupSort)
            .ThenBy(entry => entry.JobSort)
            .ThenBy(entry => entry.Id)
            .ToList();
    }

    private void DrawGearsetPopupGroupedList(IReadOnlyList<GearsetPopupGroup> leftGroups, IReadOnlyList<GearsetPopupGroup> rightGroups, float scale, float opacity) {
        var contentWidth = ImGui.GetContentRegionAvail().X;
        var columnGap = 18.0f * scale;
        var columnWidth = MathF.Floor((contentWidth - columnGap) * 0.5f);
        var start = ImGui.GetCursorScreenPos();
        var leftHeight = DrawGearsetPopupColumn(leftGroups, start, columnWidth, scale, opacity);
        var rightHeight = DrawGearsetPopupColumn(rightGroups, start + new Vector2(columnWidth + columnGap, 0.0f), columnWidth, scale, opacity);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + Math.Max(leftHeight, rightHeight) + 4.0f * scale));
    }

    private static List<GearsetPopupGroup> BuildGearsetPopupGroups(IReadOnlyList<GearsetPopupEntry> entries, IReadOnlyList<string> desiredGroupOrder) {
        var groups = new Dictionary<string, List<GearsetPopupEntry>>(StringComparer.Ordinal);
        foreach (var entry in entries) {
            if (!groups.TryGetValue(entry.Group, out var groupEntries)) {
                groupEntries = [];
                groups[entry.Group] = groupEntries;
            }

            groupEntries.Add(entry);
        }

        var result = new List<GearsetPopupGroup>();
        foreach (var group in desiredGroupOrder) {
            if (groups.TryGetValue(group, out var groupEntries) && groupEntries.Count > 0) {
                result.Add(new GearsetPopupGroup(group, groupEntries));
            }
        }

        return result;
    }

    private float DrawGearsetPopupColumn(IReadOnlyList<GearsetPopupGroup> groups, Vector2 start, float width, float scale, float opacity) {
        var y = start.Y;
        foreach (var group in groups) {
            y += DrawGearsetPopupGroupHeader(group.Group, new Vector2(start.X, y), width, scale, opacity);
            foreach (var entry in group.Entries) {
                if (DrawGearsetPopupJobRow(entry, new Vector2(start.X, y), width, scale, opacity)) {
                    EquipGearset(entry.Id);
                    this.gearsetPopupCache = null;
                    if (this.config.TaskBarGearsetClosePopupOnSwitch) {
                        ImGui.CloseCurrentPopup();
                    }
                }

                y += 30.0f * scale;
            }

            y += 9.0f * scale;
        }

        return y - start.Y;
    }

    private static float DrawGearsetPopupGroupHeader(string label, Vector2 pos, float width, float scale, float opacity) {
        var drawList = ImGui.GetWindowDrawList();
        var textColor = ImGui.GetColorU32(new Vector4(0.42f, 0.24f, 0.34f, opacity));
        drawList.AddText(pos + new Vector2(2.0f * scale, 0.0f), textColor, label);
        return 21.0f * scale;
    }

    private bool DrawGearsetPopupJobRow(GearsetPopupEntry entry, Vector2 pos, float width, float scale, float opacity) {
        var rowHeight = 26.0f * scale;
        var drawList = ImGui.GetWindowDrawList();
        ImGui.SetCursorScreenPos(pos);
        ImGui.PushID($"gearset_job_{entry.Id}");
        ImGui.InvisibleButton("##row", new Vector2(width, rowHeight));
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        if (entry.Selected || hovered || active) {
            var bg = entry.Selected
                ? new Vector4(0.94f, 0.68f, 0.82f, opacity * 0.72f)
                : new Vector4(1.0f, 0.96f, 0.98f, opacity * (active ? 0.66f : 0.46f));
            drawList.AddRectFilled(pos, pos + new Vector2(width, rowHeight), ImGui.GetColorU32(bg), 4.0f * scale);
        }

        var iconMin = pos + new Vector2(1.0f * scale, 2.0f * scale);
        var iconMax = iconMin + new Vector2(22.0f * scale, 22.0f * scale);
        DrawGameIconImage(drawList, entry.IconId, iconMin, iconMax, true, true);

        var levelColor = entry.JobLevel > 0
            ? ImGui.GetColorU32(new Vector4(1.0f, 0.52f, 0.16f, opacity))
            : ImGui.GetColorU32(new Vector4(0.58f, 0.44f, 0.52f, opacity));
        var textColor = entry.Selected
            ? ImGui.GetColorU32(new Vector4(0.34f, 0.16f, 0.26f, opacity))
            : ImGui.GetColorU32(new Vector4(0.24f, 0.16f, 0.20f, opacity));
        var levelText = entry.JobLevel > 0 ? entry.JobLevel.ToString() : "--";
        var levelSize = ImGui.CalcTextSize(levelText);
        var levelRight = pos.X + 57.0f * scale;
        drawList.AddText(new Vector2(levelRight - levelSize.X, pos.Y + 3.0f * scale), levelColor, levelText);
        drawList.AddText(pos + new Vector2(67.0f * scale, 3.0f * scale), textColor, entry.JobName);

        var lineStart = pos + new Vector2(68.0f * scale, 22.0f * scale);
        var lineEnd = pos + new Vector2(width - 8.0f * scale, 22.0f * scale);
        drawList.AddLine(lineStart, lineEnd, ImGui.GetColorU32(new Vector4(0.74f, 0.56f, 0.66f, opacity * 0.42f)), 2.0f * scale);
        if (entry.Selected) {
            drawList.AddLine(lineStart, new Vector2(lineEnd.X, lineStart.Y), ImGui.GetColorU32(new Vector4(0.96f, 0.42f, 0.68f, opacity * 0.74f)), 2.0f * scale);
        }

        ImGui.PopID();
        return clicked;
    }

    private uint GetGearsetIconId(uint classJobId) {
        return classJobId == 0 ? 0 : 62100u + classJobId;
    }

    private unsafe GearsetJobMetadata GetGearsetJobMetadata(uint classJobId) {
        if (classJobId == 0) {
            return new GearsetJobMetadata(string.Empty, -1, "其他", 7, 100, 0);
        }

        if (this.gearsetJobMetadataCache.TryGetValue(classJobId, out var cached)) {
            return cached;
        }

        var classJobSheet = this.dataManager.GetExcelSheet<LuminaClassJob>();
        if (!classJobSheet.TryGetRow(classJobId, out var row)) {
            var fallback = new GearsetJobMetadata(string.Empty, -1, "其他", 7, 100 + (int)classJobId, GetGearsetIconId(classJobId));
            this.gearsetJobMetadataCache[classJobId] = fallback;
            return fallback;
        }

        var name = row.Name.ToString();
        var expArrayIndex = row.ExpArrayIndex;
        var group = GetGearsetJobGroup(classJobId);
        var metadata = new GearsetJobMetadata(
            string.IsNullOrWhiteSpace(name) ? string.Empty : name,
            expArrayIndex,
            group,
            GetGearsetGroupSort(group),
            GetGearsetJobSort(classJobId),
            GetGearsetIconId(classJobId));
        this.gearsetJobMetadataCache[classJobId] = metadata;
        return metadata;
    }

    private unsafe short GetGearsetJobLevel(uint classJobId) {
        return GetGearsetJobLevel(GetGearsetJobMetadata(classJobId));
    }

    private unsafe short GetGearsetJobLevel(GearsetJobMetadata metadata) {
        if (metadata.ExpArrayIndex < 0) {
            return 0;
        }

        try {
            return (short)PlayerState.Instance()->ClassJobLevels[metadata.ExpArrayIndex];
        } catch {
            return 0;
        }
    }

    private string GetGearsetJobName(uint classJobId, string fallback) {
        if (classJobId != 0) {
            var metadata = GetGearsetJobMetadata(classJobId);
            if (!string.IsNullOrWhiteSpace(metadata.Name)) {
                return metadata.Name;
            }
        }

        return fallback;
    }

    private static string GetGearsetJobGroup(uint classJobId) {
        return classJobId switch {
            TrackedActionCatalog.Paladin or TrackedActionCatalog.Warrior or TrackedActionCatalog.DarkKnight or TrackedActionCatalog.Gunbreaker => "防护职业",
            TrackedActionCatalog.WhiteMage or TrackedActionCatalog.Scholar or TrackedActionCatalog.Astrologian or TrackedActionCatalog.Sage => "治疗职业",
            TrackedActionCatalog.Monk or TrackedActionCatalog.Dragoon or TrackedActionCatalog.Ninja or TrackedActionCatalog.Samurai or TrackedActionCatalog.Reaper or TrackedActionCatalog.Viper => "近战职业",
            TrackedActionCatalog.Bard or TrackedActionCatalog.Machinist or TrackedActionCatalog.Dancer => "远程物理职业",
            TrackedActionCatalog.BlackMage or TrackedActionCatalog.Summoner or TrackedActionCatalog.RedMage or TrackedActionCatalog.Pictomancer or BlueMageClassJobId => "远程魔法职业",
            >= 8 and <= 15 => "生产职业",
            >= 16 and <= 18 => "采集职业",
            _ => "其他",
        };
    }

    private static int GetGearsetGroupSort(string group) {
        return group switch {
            "防护职业" => 0,
            "治疗职业" => 1,
            "近战职业" => 2,
            "远程物理职业" => 3,
            "远程魔法职业" => 4,
            "生产职业" => 5,
            "采集职业" => 6,
            _ => 7,
        };
    }

    private static int GetGearsetJobSort(uint classJobId) {
        return classJobId switch {
            TrackedActionCatalog.Paladin => 0,
            TrackedActionCatalog.Warrior => 1,
            TrackedActionCatalog.DarkKnight => 2,
            TrackedActionCatalog.Gunbreaker => 3,
            TrackedActionCatalog.WhiteMage => 10,
            TrackedActionCatalog.Scholar => 11,
            TrackedActionCatalog.Astrologian => 12,
            TrackedActionCatalog.Sage => 13,
            TrackedActionCatalog.Monk => 20,
            TrackedActionCatalog.Dragoon => 21,
            TrackedActionCatalog.Ninja => 22,
            TrackedActionCatalog.Samurai => 23,
            TrackedActionCatalog.Reaper => 24,
            TrackedActionCatalog.Viper => 25,
            TrackedActionCatalog.Bard => 30,
            TrackedActionCatalog.Machinist => 31,
            TrackedActionCatalog.Dancer => 32,
            TrackedActionCatalog.BlackMage => 40,
            TrackedActionCatalog.Summoner => 41,
            TrackedActionCatalog.RedMage => 42,
            TrackedActionCatalog.Pictomancer => 43,
            BlueMageClassJobId => 44,
            _ => 100 + (int)classJobId,
        };
    }

    private void DrawGearsetPopupHeader(string jobName, string setLabel, uint iconId, float scale, float opacity) {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 56.0f * scale;
        var max = min + new Vector2(width, height);
        var rounding = 10.0f * scale;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1.0f, 0.88f, 0.94f, opacity * 0.88f)), rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.94f, 0.54f, 0.74f, opacity * 0.82f)), rounding, (ImDrawFlags)0, 1.0f * scale);

        var iconMin = min + new Vector2(10.0f * scale, 8.0f * scale);
        var iconMax = iconMin + new Vector2(40.0f * scale, 40.0f * scale);
        drawList.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0.98f, 0.93f, 0.97f, opacity * 0.92f)), 9.0f * scale);
        drawList.AddRect(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0.78f, 0.34f, 0.58f, opacity * 0.80f)), 9.0f * scale, (ImDrawFlags)0, 1.0f * scale);
        if (!DrawGameIconImage(drawList, iconId, iconMin + new Vector2(4.0f * scale, 4.0f * scale), iconMax - new Vector2(4.0f * scale, 4.0f * scale), true, true)) {
            drawList.AddText(iconMin + new Vector2(10.0f * scale, 6.0f * scale), ImGui.GetColorU32(new Vector4(0.60f, 0.24f, 0.42f, opacity)), "⚒");
        }

        var titlePos = min + new Vector2(62.0f * scale, 9.0f * scale);
        var subtitlePos = min + new Vector2(62.0f * scale, 29.0f * scale);
        drawList.AddText(titlePos, ImGui.GetColorU32(new Vector4(0.27f, 0.16f, 0.22f, opacity)), jobName);
        drawList.AddText(subtitlePos, ImGui.GetColorU32(new Vector4(0.50f, 0.31f, 0.39f, opacity * 0.94f)), setLabel);

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y + 8.0f * scale));
    }

    private bool DrawGearsetPopupCard(byte id, string name, uint iconId, bool selected, Vector2 size, float scale, float opacity) {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + size;
        ImGui.PushID($"gearset_{id}");
        ImGui.InvisibleButton("##card", size);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var fill = selected
            ? new Vector4(0.93f, 0.70f, 0.84f, opacity * 0.88f)
            : hovered
                ? new Vector4(1.0f, 0.94f, 0.98f, opacity * 0.92f)
                : new Vector4(1.0f, 0.97f, 0.99f, opacity * 0.76f);
        if (active) {
            fill = new Vector4(0.90f, 0.58f, 0.76f, opacity * 0.92f);
        }

        var border = selected
            ? new Vector4(0.72f, 0.28f, 0.52f, opacity * 0.96f)
            : new Vector4(0.94f, 0.58f, 0.74f, opacity * 0.62f);
        var rounding = 7.0f * scale;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(border), rounding, (ImDrawFlags)0, 1.0f * scale);

        var number = $"{id + 1:00}";
        var numberColor = selected
            ? ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity))
            : ImGui.GetColorU32(new Vector4(0.66f, 0.26f, 0.44f, opacity));
        var textColor = selected
            ? ImGui.GetColorU32(new Vector4(1.0f, 0.98f, 1.0f, opacity))
            : ImGui.GetColorU32(new Vector4(0.28f, 0.17f, 0.22f, opacity));
        var iconMin = min + new Vector2(7.0f * scale, 7.0f * scale);
        var iconMax = iconMin + new Vector2(24.0f * scale, 24.0f * scale);
        if (!DrawGameIconImage(drawList, iconId, iconMin, iconMax, true, true)) {
            drawList.AddText(min + new Vector2(10.0f * scale, 8.0f * scale), numberColor, number);
        }

        drawList.AddText(min + new Vector2(38.0f * scale, 10.0f * scale), numberColor, number);
        drawList.AddText(min + new Vector2(68.0f * scale, 10.0f * scale), textColor, name);
        ImGui.PopID();
        return clicked;
    }
}

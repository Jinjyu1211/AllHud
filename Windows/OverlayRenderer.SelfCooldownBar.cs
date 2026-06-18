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
    private readonly record struct SelfCooldownVisibleGroup(PartyCooldownGroupEntry Entry, IReadOnlyList<CooldownEntry> Cooldowns);
    private sealed record SelfCooldownLayoutCacheKey(
        IReadOnlyList<PartyCooldownGroupEntry> Groups,
        bool HideWhenReady,
        bool Preview,
        int LayoutDirection,
        float IconSize,
        float Spacing,
        float RowGap,
        float Pad,
        float ViewportWidth);

    private sealed record SelfCooldownLayoutCache(IReadOnlyList<SelfCooldownVisibleGroup> VisibleGroups, IReadOnlyList<SelfCooldownHorizontalRow> HorizontalRows);

    private SelfCooldownLayoutCacheKey? cachedSelfCooldownLayoutKey;
    private SelfCooldownLayoutCache? cachedSelfCooldownLayout;

    private void DrawSelfCooldownBarWindow(ImGuiWindowFlags flags) {
        if (!this.config.ShowSelfCooldownBar) {
            return;
        }

        var groups = this.config.ShowSelfCooldownBarPreview
            ? this.combatState.GetPartyCooldownTrackingPreview(this.config)
            : this.combatState.GetPartyCooldownTracking(this.config);
        if (groups.Count == 0) {
            return;
        }

        if (this.config.SelfCooldownBarLocked) {
            flags |= ImGuiWindowFlags.NoMove;
        }

        var scale = Math.Clamp(this.config.SelfCooldownBarScale, 0.6f, 2.0f);
        var opacity = Math.Clamp(this.config.SelfCooldownBarOpacity, 0.15f, 1.0f);
        var iconSize = Math.Clamp(36.0f * scale, 12.0f, 64.0f);
        var spacing = MathF.Round(5.0f * scale);
        var rowGap = MathF.Round(5.0f * scale);
        var headerGap = MathF.Round(7.0f * scale);
        var pad = MathF.Round(7.0f * scale);
        var horizontalLayout = Math.Clamp(this.config.SelfCooldownBarLayoutDirection, 0, 1) == 1;
        var layoutCache = GetSelfCooldownLayoutCache(groups, horizontalLayout, iconSize, spacing, rowGap, pad);
        var visibleGroups = layoutCache.VisibleGroups;
        var horizontalRows = layoutCache.HorizontalRows;
        if (visibleGroups.Count == 0) {
            return;
        }

        var contentWidth = horizontalLayout
            ? horizontalRows.Count > 0 ? horizontalRows.Max(row => row.Width) : 0.0f
            : visibleGroups.Max(entry => iconSize + headerGap + entry.Cooldowns.Count * iconSize + Math.Max(0, entry.Cooldowns.Count - 1) * spacing);
        var contentHeight = horizontalLayout
            ? horizontalRows.Count * (iconSize * 2.0f + headerGap) + Math.Max(0, horizontalRows.Count - 1) * rowGap
            : visibleGroups.Count * iconSize + Math.Max(0, visibleGroups.Count - 1) * rowGap;
        var windowSize = SnapToPixel(new Vector2(contentWidth + pad * 2.0f, contentHeight + pad * 2.0f));
        var windowPosition = GetClampedStatusOverlayPosition(this.config.SelfCooldownBarPosition, windowSize);
        var shouldClampWindow = windowPosition != this.config.SelfCooldownBarPosition;

        ImGui.SetNextWindowPos(windowPosition, shouldClampWindow ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(windowSize);

        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration;
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, opacity);
        if (!ImGui.Begin("AllHud 队伍冷却", flags)) {
            ImGui.End();
            ImGui.PopStyleVar();
            return;
        }

        var currentPosition = ImGui.GetWindowPos();
        var clampedCurrentPosition = GetClampedStatusOverlayPosition(currentPosition, windowSize);
        if (clampedCurrentPosition != currentPosition) {
            ImGui.SetWindowPos(clampedCurrentPosition, ImGuiCond.Always);
        }

        TrackSelfCooldownBarPosition();

        var drawList = ImGui.GetWindowDrawList();
        var windowMin = ImGui.GetWindowPos();
        DrawSelfCooldownBarCard(drawList, windowMin, windowMin + windowSize, scale, opacity);

        if (horizontalLayout) {
            DrawSelfCooldownBarHorizontalLayout(drawList, horizontalRows, pad, iconSize, spacing, rowGap, headerGap);
        } else {
            DrawSelfCooldownBarVerticalLayout(drawList, visibleGroups, pad, iconSize, spacing, rowGap, headerGap);
        }

        ImGui.End();
        ImGui.PopStyleVar();
    }

    private readonly record struct SelfCooldownHorizontalRow(IReadOnlyList<SelfCooldownVisibleGroup> Groups, float Width);

    private SelfCooldownLayoutCache GetSelfCooldownLayoutCache(IReadOnlyList<PartyCooldownGroupEntry> groups, bool horizontalLayout, float iconSize, float spacing, float rowGap, float pad) {
        var viewportWidth = ImGui.GetMainViewport().WorkSize.X;
        var key = new SelfCooldownLayoutCacheKey(
            groups,
            this.config.SelfCooldownBarHideWhenReady,
            this.config.ShowSelfCooldownBarPreview,
            horizontalLayout ? 1 : 0,
            iconSize,
            spacing,
            rowGap,
            pad,
            MathF.Round(viewportWidth));
        if (this.cachedSelfCooldownLayoutKey == key && this.cachedSelfCooldownLayout is not null) {
            return this.cachedSelfCooldownLayout;
        }

        var visibleGroups = new List<SelfCooldownVisibleGroup>(groups.Count);
        foreach (var group in groups) {
            if (this.config.SelfCooldownBarHideWhenReady) {
                List<CooldownEntry>? visibleCooldowns = null;
                foreach (var cooldown in group.Cooldowns) {
                    if (cooldown.IsReady) {
                        continue;
                    }

                    visibleCooldowns ??= new List<CooldownEntry>(group.Cooldowns.Count);
                    visibleCooldowns.Add(cooldown);
                }

                if (visibleCooldowns is not null && visibleCooldowns.Count > 0) {
                    visibleGroups.Add(new SelfCooldownVisibleGroup(group, visibleCooldowns));
                }
            } else if (group.Cooldowns.Count > 0) {
                visibleGroups.Add(new SelfCooldownVisibleGroup(group, group.Cooldowns));
            }
        }

        if (!this.config.ShowSelfCooldownBarPreview) {
            SortSelfCooldownGroupsByNativePartyList(visibleGroups);
        }

        var horizontalRows = horizontalLayout
            ? BuildSelfCooldownHorizontalRows(visibleGroups, iconSize, spacing, rowGap, pad)
            : Array.Empty<SelfCooldownHorizontalRow>();
        this.cachedSelfCooldownLayoutKey = key;
        this.cachedSelfCooldownLayout = new SelfCooldownLayoutCache(visibleGroups, horizontalRows);
        return this.cachedSelfCooldownLayout;
    }

    private static float GetSelfCooldownHorizontalGroupWidth(SelfCooldownVisibleGroup group, float iconSize, float spacing) {
        return Math.Max(iconSize, group.Cooldowns.Count * iconSize + Math.Max(0, group.Cooldowns.Count - 1) * spacing);
    }

    private static IReadOnlyList<SelfCooldownHorizontalRow> BuildSelfCooldownHorizontalRows(IReadOnlyList<SelfCooldownVisibleGroup> visibleGroups, float iconSize, float spacing, float groupGap, float pad) {
        var viewport = ImGui.GetMainViewport();
        var maxContentWidth = Math.Max(iconSize, viewport.WorkSize.X - pad * 2.0f - 32.0f);
        var rows = new List<SelfCooldownHorizontalRow>();
        var currentGroups = new List<SelfCooldownVisibleGroup>();
        var currentWidth = 0.0f;

        foreach (var group in visibleGroups) {
            var groupWidth = GetSelfCooldownHorizontalGroupWidth(group, iconSize, spacing);
            var nextWidth = currentGroups.Count == 0 ? groupWidth : currentWidth + groupGap + groupWidth;
            if (currentGroups.Count > 0 && nextWidth > maxContentWidth) {
                rows.Add(new SelfCooldownHorizontalRow(currentGroups, currentWidth));
                currentGroups = new List<SelfCooldownVisibleGroup>();
                currentWidth = 0.0f;
                nextWidth = groupWidth;
            }

            currentGroups.Add(group);
            currentWidth = nextWidth;
        }

        if (currentGroups.Count > 0) {
            rows.Add(new SelfCooldownHorizontalRow(currentGroups, currentWidth));
        }

        return rows;
    }

    private void DrawSelfCooldownBarVerticalLayout(ImDrawListPtr drawList, IReadOnlyList<SelfCooldownVisibleGroup> visibleGroups, float pad, float iconSize, float spacing, float rowGap, float headerGap) {
        for (var rowIndex = 0; rowIndex < visibleGroups.Count; rowIndex++) {
            var group = visibleGroups[rowIndex];
            var rowY = pad + rowIndex * (iconSize + rowGap);
            ImGui.SetCursorPos(new Vector2(pad, rowY));
            DrawSelfCooldownBarJobHeader(drawList, ImGui.GetCursorScreenPos(), iconSize, group.Entry.SourceJobIconId);

            var cooldownX = pad + iconSize + headerGap;
            for (var cooldownIndex = 0; cooldownIndex < group.Cooldowns.Count; cooldownIndex++) {
                ImGui.SetCursorPos(new Vector2(cooldownX + cooldownIndex * (iconSize + spacing), rowY));
                DrawPartyCooldownBarIcon(group.Entry, group.Cooldowns[cooldownIndex], iconSize, cooldownIndex);
            }
        }
    }

    private void DrawSelfCooldownBarHorizontalLayout(ImDrawListPtr drawList, IReadOnlyList<SelfCooldownHorizontalRow> rows, float pad, float iconSize, float spacing, float groupGap, float headerGap) {
        var rowStride = iconSize * 2.0f + headerGap + groupGap;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
            var row = rows[rowIndex];
            var groupX = pad;
            var headerY = pad + rowIndex * rowStride;
            var cooldownY = headerY + iconSize + headerGap;
            foreach (var group in row.Groups) {
                var groupWidth = GetSelfCooldownHorizontalGroupWidth(group, iconSize, spacing);
                ImGui.SetCursorPos(new Vector2(groupX + Math.Max(0.0f, (groupWidth - iconSize) * 0.5f), headerY));
                DrawSelfCooldownBarJobHeader(drawList, ImGui.GetCursorScreenPos(), iconSize, group.Entry.SourceJobIconId);

                for (var cooldownIndex = 0; cooldownIndex < group.Cooldowns.Count; cooldownIndex++) {
                    ImGui.SetCursorPos(new Vector2(groupX + cooldownIndex * (iconSize + spacing), cooldownY));
                    DrawPartyCooldownBarIcon(group.Entry, group.Cooldowns[cooldownIndex], iconSize, cooldownIndex);
                }

                groupX += groupWidth + groupGap;
            }
        }
    }

    private unsafe void SortSelfCooldownGroupsByNativePartyList(List<SelfCooldownVisibleGroup> groups) {
        var memberOrder = GetNativePartyMemberOrder();
        if (memberOrder.Count == 0) {
            return;
        }

        var indexByEntityId = new Dictionary<uint, int>(memberOrder.Count);
        for (var index = 0; index < memberOrder.Count; index++) {
            indexByEntityId[memberOrder[index]] = index;
        }

        groups.Sort((left, right) => {
            var order = GetNativePartyMemberOrderIndex(left.Entry, indexByEntityId)
                .CompareTo(GetNativePartyMemberOrderIndex(right.Entry, indexByEntityId));
            return order != 0 ? order : left.Entry.PartySlot.CompareTo(right.Entry.PartySlot);
        });
    }

    private static int GetNativePartyMemberOrderIndex(PartyCooldownGroupEntry entry, IReadOnlyDictionary<uint, int> indexByEntityId) {
        if (entry.SourceEntityId != 0 && indexByEntityId.TryGetValue(entry.SourceEntityId, out var entityIndex)) {
            return entityIndex;
        }

        if (entry.SourceObjectId <= uint.MaxValue && indexByEntityId.TryGetValue((uint)entry.SourceObjectId, out var objectIndex)) {
            return objectIndex;
        }

        return int.MaxValue;
    }

    private unsafe IReadOnlyList<uint> GetNativePartyMemberOrder() {
        var addonPtr = this.gameGui.GetAddonByName("_PartyList");
        if (addonPtr.IsNull) {
            return [];
        }

        var addon = (AddonPartyList*)addonPtr.Address;
        if (!IsNativePartyListVisible(addon)) {
            return [];
        }

        var memberCount = Math.Clamp(addon->MemberCount, 0, 8);
        if (memberCount <= 0) {
            return [];
        }

        var partyArray = PartyListNumberArray.Instance();
        if (partyArray is null) {
            return [];
        }

        var entityIds = new List<uint>(memberCount);
        for (var nativeIndex = 0; nativeIndex < memberCount; nativeIndex++) {
            ref var nativeData = ref partyArray->PartyMembers[nativeIndex];
            if (nativeData.MaxHealth > 0 && nativeData.EntityId != 0) {
                entityIds.Add(nativeData.EntityId);
            }
        }

        return entityIds;
    }

    private void DrawPartyCooldownBarIcon(PartyCooldownGroupEntry entry, CooldownEntry cooldown, float size, int index) {
        var drawList = ImGui.GetWindowDrawList();
        var cursorScreenPos = ImGui.GetCursorScreenPos();
        var min = cursorScreenPos;
        var max = min + new Vector2(size, size);

        ImGui.InvisibleButton($"##party_cd_{entry.SourceObjectId}_{cooldown.IconId}_{cooldown.Name}_{index}", new Vector2(size, size));
        DrawNativeAttachedCooldownIcon(drawList, min, cooldown, size);

        if (ImGui.IsItemHovered()) {
            DrawPartyCooldownTooltip(entry, cooldown);
        }
    }

    private void DrawSelfCooldownBarJobHeader(ImDrawListPtr drawList, Vector2 min, float size, uint jobIconId) {
        var max = min + new Vector2(size, size);
        var rounding = Math.Max(3.0f, size * 0.12f);
        var padding = MathF.Max(2.0f, MathF.Round(size * 0.045f));

        drawList.AddRectFilled(
            min - new Vector2(1.0f),
            max + new Vector2(1.0f),
            ImGui.GetColorU32(new Vector4(0.18f, 0.06f, 0.12f, 0.96f)),
            rounding);
        drawList.AddRectFilled(
            min,
            max,
            ImGui.GetColorU32(new Vector4(0.96f, 0.80f, 0.88f, 0.88f)),
            rounding);
        if (jobIconId != 0) {
            DrawGameIconImage(drawList, jobIconId, min + new Vector2(padding), max - new Vector2(padding), true);
        }
    }

    private static void DrawSelfCooldownBarCard(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale, float opacity) {
        var rounding = MathF.Round(8.0f * scale);
        drawList.AddRectFilled(min + new Vector2(1.0f, 2.0f), max + new Vector2(1.0f, 2.0f), ImGui.GetColorU32(new Vector4(0.20f, 0.08f, 0.14f, 0.10f * opacity)), rounding);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1.0f, 0.94f, 0.97f, 0.28f * opacity)), rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.96f, 0.58f, 0.78f, 0.36f * opacity)), rounding, (ImDrawFlags)0, Math.Max(1.0f, 1.0f * scale));
    }

    private void DrawPartyCooldownTooltip(PartyCooldownGroupEntry entry, CooldownEntry cooldown) {
        DrawStyledTooltip(() => {
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(entry.SourceJobName) ? entry.SourceName : $"{entry.SourceName} · {entry.SourceJobName}");
            ImGui.TextUnformatted(cooldown.Name);
            ImGui.Separator();
            if (cooldown.IsReady) {
                ImGui.TextUnformatted("冷却：就绪");
            } else {
                ImGui.TextUnformatted($"冷却剩余：{FormatCooldownIconTime(cooldown.RemainingCooldownSeconds)}");
            }
        });
    }

    private void TrackSelfCooldownBarPosition() {
        if (this.config.SelfCooldownBarLocked) {
            return;
        }

        var currentPosition = ImGui.GetWindowPos();
        if (currentPosition != this.config.SelfCooldownBarPosition) {
            this.config.SelfCooldownBarPosition = currentPosition;
            this.selfCooldownBarPositionSaveDueAt = DateTime.UtcNow.Add(OverlayPositionSaveDelay);
        }

        if (this.selfCooldownBarPositionSaveDueAt is not { } saveDueAt
            || DateTime.UtcNow < saveDueAt
            || this.config.SelfCooldownBarPosition == this.lastSavedSelfCooldownBarPosition) {
            return;
        }

        this.saveConfig();
        this.lastSavedSelfCooldownBarPosition = this.config.SelfCooldownBarPosition;
        this.selfCooldownBarPositionSaveDueAt = null;
    }
}

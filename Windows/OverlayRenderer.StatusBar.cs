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
    private void DrawStatusWindow(ImGuiWindowFlags flags) {
        if (this.config.StatusBarLocked) {
            flags |= ImGuiWindowFlags.NoMove;
        }

        if (this.config.StatusBarMousePassthrough) {
            flags |= ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoMouseInputs;
        }

        var selfStatuses = this.config.ShowStatusPreview
            ? CreatePreviewSelfHudStatuses()
            : this.combatState.GetSelfHudStatuses(this.config);

        if (selfStatuses.Count == 0) {
            return;
        }

        var sections = BuildSelfStatusIconSections(selfStatuses, this.config);

        if (sections.Count == 0) {
            return;
        }

        var scale = Math.Clamp(this.config.Scale, 0.5f, 2.5f);
        var iconSize = GetNativeStatusOverlayIconSize(scale);
        var spacing = GetStatusIconSpacing(scale);
        var timerHeight = GetNativeStatusTimerHeight(iconSize);
        var pad = MathF.Round(8.0f * scale);
        var bottomSafetyPad = MathF.Round(6.0f * scale);
        var sectionGap = MathF.Round((this.config.StatusBarLayoutMode == 1 ? 8.0f : 4.0f) * scale);
        var labelWidth = GetStatusSectionLabelWidth(scale);
        var cardPadding = GetStatusSectionCardPadding(scale);
        var layouts = new List<StatusSectionLayout>(sections.Count);
        var gridWidth = iconSize;
        var gridHeight = Math.Max(0, sections.Count - 1) * sectionGap;
        var splitWidth = iconSize + labelWidth + cardPadding * 2.0f + 18.0f * scale;
        for (var index = 0; index < sections.Count; index++) {
            var section = sections[index];
            var columns = Math.Max(1, Math.Min(NativeStatusIconsPerRow, section.Statuses.Count));
            var gridSize = GetStatusIconGridSize(section.Statuses.Count, columns, iconSize, spacing, timerHeight);
            var size = new Vector2(gridSize.X + labelWidth + cardPadding * 2.0f + 18.0f * scale, gridSize.Y + cardPadding * 2.0f);
            layouts.Add(new StatusSectionLayout(section, columns, gridSize, size));
            gridWidth = Math.Max(gridWidth, gridSize.X);
            gridHeight += size.Y;
            splitWidth = Math.Max(splitWidth, size.X);
        }
        var windowWidth = this.config.StatusBarLayoutMode == 1
            ? splitWidth + pad * 2.0f
            : gridWidth + labelWidth + cardPadding * 2.0f + pad * 2.0f + 18.0f * scale;
        var windowSize = new Vector2(windowWidth, gridHeight + pad * 2.0f + bottomSafetyPad);

        var statusWindowPos = GetClampedStatusOverlayPosition(this.config.StatusOverlayPosition, windowSize);
        var shouldClampStatusWindow = statusWindowPos != this.config.StatusOverlayPosition;
        ImGui.SetNextWindowPos(statusWindowPos, shouldClampStatusWindow ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(windowSize);

        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration;
        if (!ImGui.Begin("AllHud 状态", flags)) {
            ImGui.End();
            return;
        }

        var clampedCurrentPos = GetClampedStatusOverlayPosition(ImGui.GetWindowPos(), windowSize);
        if (clampedCurrentPos != ImGui.GetWindowPos()) {
            ImGui.SetWindowPos(clampedCurrentPos, ImGuiCond.Always);
        }

        TrackStatusOverlayPosition();

        ImGui.SetCursorPos(new Vector2(pad, pad));
        if (this.config.StatusBarLayoutMode == 1) {
            DrawSplitStatusIconSections(layouts, sectionGap, labelWidth, iconSize, timerHeight, spacing, scale);
        } else {
            DrawMergedStatusIconSections(layouts, sectionGap, labelWidth, gridWidth, iconSize, timerHeight, spacing, scale);
        }
        ImGui.Dummy(new Vector2(1.0f, bottomSafetyPad));

        ImGui.End();
    }

    private IReadOnlyList<StatusIconSection> BuildSelfStatusIconSections(
        IReadOnlyList<StatusEntry> selfStatuses,
        Configuration config) {
        var toggles = (config.ShowSelfEnfeeblements ? 1 : 0)
                      | (config.ShowSelfOtherStatuses ? 2 : 0)
                      | (config.ShowSelfBuffs ? 4 : 0);
        if (this.cachedSelfStatusSections is not null
            && ReferenceEquals(this.cachedSelfStatusSectionSource, selfStatuses)
            && this.cachedSelfStatusSectionToggles == toggles) {
            return this.cachedSelfStatusSections;
        }

        var sections = new List<StatusIconSection>();

        void AddSection(string label, Vector4 color, IEnumerable<StatusEntry> statuses, int maxCount, bool enabled) {
            if (!enabled) {
                return;
            }

            var ordered = statuses
                .Take(maxCount)
                .ToList();
            if (ordered.Count == 0) {
                return;
            }

            sections.Add(new StatusIconSection(label, color, ordered));
        }

        var orderedStatuses = OrderSelfHudStatuses(selfStatuses);
        AddSection("弱化", SelfDebuffColor, orderedStatuses.Where(status => !status.IsBuff), MaxSelfStatusEnfeeblements, config.ShowSelfEnfeeblements);
        AddSection("其他", PersonalSkillColor, orderedStatuses.Where(IsOtherSelfStatus), MaxSelfStatusOthers, config.ShowSelfOtherStatuses);
        AddSection("强化", SelfBuffColor, orderedStatuses.Where(status => status.IsBuff && !IsOtherSelfStatus(status)), MaxSelfStatusEnhancements, config.ShowSelfBuffs);

        this.cachedSelfStatusSectionSource = selfStatuses;
        this.cachedSelfStatusSectionToggles = toggles;
        this.cachedSelfStatusSections = sections;
        return sections;
    }

    private static IReadOnlyList<StatusEntry> OrderSelfHudStatuses(IEnumerable<StatusEntry> statuses) {
        return statuses
            .OrderBy(status => status.StatusIndex)
            .ThenByDescending(status => status.PartyListPriority)
            .ThenBy(status => status.CanDispel ? 0 : 1)
            .ThenBy(status => status.RemainingSeconds)
            .ThenBy(status => status.StatusId)
            .ToList();
    }

    private static bool IsOtherSelfStatus(StatusEntry status) {
        return status.StatusId == FoodStatusId;
    }

    // 目标状态按"自身施加优先、其余其次"排序，结果按输入引用记忆。
    // targetInfo.Statuses 来自 100ms TTL 缓存，引用稳定时跳过重复 Where/Concat/ToList。
    private IReadOnlyList<StatusEntry> GetOrderedTargetStatuses(IReadOnlyList<StatusEntry> statuses) {
        if (this.cachedTargetOrderedStatuses is not null
            && ReferenceEquals(this.cachedTargetStatusSplitSource, statuses)) {
            return this.cachedTargetOrderedStatuses;
        }

        var ordered = new List<StatusEntry>(statuses.Count);
        foreach (var status in statuses) {
            if (status.IsSelfApplied) {
                ordered.Add(status);
            }
        }

        foreach (var status in statuses) {
            if (!status.IsSelfApplied) {
                ordered.Add(status);
            }
        }

        this.cachedTargetStatusSplitSource = statuses;
        this.cachedTargetOrderedStatuses = ordered;
        return ordered;
    }

    private static IReadOnlyList<StatusEntry> OrderTargetStatuses(IEnumerable<StatusEntry> statuses) {
        return statuses
            .OrderBy(status => status.IsBuff ? 1 : 0)
            .ThenBy(status => status.StatusIndex)
            .ThenByDescending(status => status.PartyListPriority)
            .ThenBy(status => status.CanDispel ? 0 : 1)
            .ThenBy(status => status.RemainingSeconds)
            .ThenBy(status => status.StatusId)
            .ToList();
    }

    private IReadOnlyList<StatusEntry> CreatePreviewTargetStatuses() {
        var statuses = TrackedDefinitions.All
            .Where(definition => definition.StatusId != 0 && definition.IsTargetDebuff)
            .Select((definition, index) => CreatePreviewStatus(definition, index, false))
            .ToList();

        statuses.AddRange(CreateCustomPreviewStatuses(CustomTrackType.TargetStatus, false, statuses.Count));
        return statuses;
    }

    private TargetInfoEntry CreatePreviewTargetInfo() {
        return new TargetInfoEntry(
            0xCB7A0001,
            901,
            "木人",
            1,
            444321,
            456789,
            true,
            true,
            7549,
            "目标情报预览咏唱",
            1.65f,
            3.00f,
            new TargetOfTargetEntry(
                0xCB7A0002,
                "预览目标的目标",
                62345,
                98765),
            CreatePreviewTargetInfoStatuses());
    }

    private IReadOnlyList<StatusEntry> CreatePreviewTargetInfoStatuses() {
        const int previewStatusCount = 30;
        var sourceStatuses = CreatePreviewTargetStatuses()
            .ToList();

        if (sourceStatuses.Count == 0) {
            return [];
        }

        var statuses = new List<StatusEntry>(previewStatusCount);
        for (var index = 0; index < previewStatusCount; index++) {
            var source = sourceStatuses[index % sourceStatuses.Count];
            statuses.Add(source with {
                IsBuff = false,
                IsSelfApplied = index < 3,
                RemainingSeconds = GetPreviewRemainingSeconds(Math.Max(1.0f, source.MaxSeconds), index),
                StatusIndex = index,
            });
        }

        return statuses;
    }

    private IReadOnlyList<StatusEntry> CreatePreviewSelfStatuses() {
        var statuses = TrackedDefinitions.All
            .Where(definition => definition.StatusId != 0 && !definition.IsTargetDebuff)
            .Select((definition, index) => CreatePreviewStatus(definition, index, true))
            .ToList();

        statuses.AddRange(CreateCustomPreviewStatuses(CustomTrackType.SelfStatus, true, statuses.Count));
        return statuses;
    }

    private IReadOnlyList<StatusEntry> CreatePreviewSelfHudStatuses() {
        var sourceBuffs = CreatePreviewSelfStatuses().ToList();
        var result = new List<StatusEntry>();

        result.AddRange(sourceBuffs.Take(MaxPreviewStatusIconsPerSection).Select((status, index) => status with {
            HolderName = "我",
            IsBuff = true,
            PartyListPriority = false,
            MaxSeconds = Math.Min(status.MaxSeconds, 180.0f),
            StatusIndex = index,
        }));

        result.AddRange(sourceBuffs.Skip(MaxPreviewStatusIconsPerSection).Take(3).Select((status, index) => status with {
            HolderName = "我",
            IsBuff = true,
            PartyListPriority = true,
            MaxSeconds = Math.Min(status.MaxSeconds, 60.0f),
            StatusIndex = 30 + index,
        }));

        result.AddRange(CreatePreviewSelfEnfeeblements(50));

        var foodIconId = this.combatState.GetStatusIconId(FoodStatusId);
        result.Add(new StatusEntry(
            FoodStatusId,
            foodIconId,
            "食物效果",
            "我",
            "系统",
            string.Empty,
            PreviewSourceObjectIdBase + 900,
            1599.0f,
            1800.0f,
            true,
            true,
            70));

        return result;
    }

    private IReadOnlyList<StatusEntry> CreatePreviewSelfEnfeeblements(int startIndex) {
        (uint StatusId, string Name, float Remaining, float Max)[] previewDebuffs = [
            (43, "虚弱", 4.0f, 100.0f),
            (44, "濒死", 9.0f, 100.0f),
            (17, "麻痹", 10.0f, 30.0f),
            (18, "中毒", 8.0f, 30.0f),
            (14, "加重", 3.0f, 20.0f),
        ];

        return previewDebuffs
            .Select((status, index) => CreatePreviewStatus(
                status.StatusId,
                status.Name,
                false,
                false,
                status.Remaining,
                status.Max,
                (ulong)(700 + startIndex + index),
                startIndex + index))
            .ToList();
    }

    private IEnumerable<StatusEntry> CreateCustomPreviewStatuses(CustomTrackType type, bool isBuff, int startIndex) {
        if (this.config.CustomTrackedDefinitions is null) {
            yield break;
        }

        var customItems = this.config.CustomTrackedDefinitions
            .Where(definition => definition.Enabled && definition.Type == type && definition.StatusId != 0)
            .ToList();

        for (var index = 0; index < customItems.Count; index++) {
            var custom = customItems[index];
            var maxSeconds = Math.Max(1.0f, custom.DurationSeconds);
            var remainingSeconds = GetPreviewRemainingSeconds(maxSeconds, startIndex + index);
            yield return CreatePreviewStatus(
                custom.StatusId,
                string.IsNullOrWhiteSpace(custom.Name) ? $"定制状态 {custom.StatusId}" : custom.Name,
                isBuff,
                index % 2 == 0,
                remainingSeconds,
                maxSeconds,
                (ulong)(500 + startIndex + index),
                startIndex + index);
        }
    }

    private StatusEntry CreatePreviewStatus(TrackedStatusDefinition definition, int index, bool isBuff) {
        var maxSeconds = Math.Max(1.0f, definition.DurationSeconds);
        var remainingSeconds = GetPreviewRemainingSeconds(maxSeconds, index);
        return CreatePreviewStatus(
            definition.StatusId,
            definition.Name,
            isBuff,
            index % 2 == 0,
            remainingSeconds,
            maxSeconds,
            (ulong)(index + 1),
            index);
    }

    private StatusEntry CreatePreviewStatus(
        uint statusId,
        string name,
        bool isBuff,
        bool isSelfApplied,
        float remainingSeconds,
        float maxSeconds,
        ulong sourceOffset,
        int statusIndex = int.MaxValue) {
        var iconId = this.combatState.GetStatusIconId(statusId);
        if (iconId == 0) {
            iconId = this.combatState.GetStatusIconId(FoodStatusId);
        }

        return new StatusEntry(
            statusId,
            iconId,
            name,
            "测试目标",
            isSelfApplied ? "我" : "队友",
            isSelfApplied ? "当前职业" : "队友职业",
            PreviewSourceObjectIdBase + sourceOffset,
            remainingSeconds,
            maxSeconds,
            isBuff,
            isSelfApplied,
            statusIndex);
    }

    private static float GetPreviewRemainingSeconds(float maxSeconds, int index) {
        if (maxSeconds >= LongStatusThresholdSeconds) {
            return Math.Max(1.0f, maxSeconds - index * 37.0f);
        }

        var ratio = 0.25f + index % 4 * 0.18f;
        return Math.Clamp(maxSeconds * ratio, 1.0f, maxSeconds);
    }

    private void TrackStatusOverlayPosition() {
        if (this.config.StatusBarLocked) {
            return;
        }

        var currentPosition = ImGui.GetWindowPos();
        if (currentPosition != this.config.StatusOverlayPosition) {
            this.config.StatusOverlayPosition = currentPosition;
            this.statusOverlayPositionSaveDueAt = DateTime.UtcNow.Add(OverlayPositionSaveDelay);
        }

        if (this.statusOverlayPositionSaveDueAt is not { } saveDueAt
            || DateTime.UtcNow < saveDueAt
            || this.config.StatusOverlayPosition == this.lastSavedStatusOverlayPosition) {
            return;
        }

        this.saveConfig();
        this.lastSavedStatusOverlayPosition = this.config.StatusOverlayPosition;
        this.statusOverlayPositionSaveDueAt = null;
    }

    private static Vector2 GetClampedStatusOverlayPosition(Vector2 position, Vector2 windowSize) {
        var viewport = ImGui.GetMainViewport();
        var workMin = viewport.WorkPos;
        var workMax = viewport.WorkPos + viewport.WorkSize;
        var maxX = Math.Max(workMin.X, workMax.X - windowSize.X);
        var maxY = Math.Max(workMin.Y, workMax.Y - windowSize.Y);
        return SnapToPixel(new Vector2(
            Math.Clamp(position.X, workMin.X, maxX),
            Math.Clamp(position.Y, workMin.Y, maxY)));
    }

    private static void DrawNeonBorder(Vector2 min, Vector2 max, Vector4 color, float glow, float rounding, float thickness = 1.2f) {
        var drawList = ImGui.GetWindowDrawList();
        uint ColorWithAlpha(float alpha) => ImGui.GetColorU32(color with { W = alpha });

        if (glow > 0.5f) {
            var outerGlow = glow * 1.4f;
            drawList.AddRect(min - new Vector2(outerGlow, outerGlow), max + new Vector2(outerGlow, outerGlow), ColorWithAlpha(color.W * 0.05f), rounding + outerGlow, (ImDrawFlags)0, thickness + 1.0f);
            var innerGlow = glow * 0.7f;
            drawList.AddRect(min - new Vector2(innerGlow, innerGlow), max + new Vector2(innerGlow, innerGlow), ColorWithAlpha(color.W * 0.15f), rounding + innerGlow, (ImDrawFlags)0, thickness + 0.5f);
        }

        drawList.AddRect(min, max, ColorWithAlpha(color.W * 0.55f), rounding, (ImDrawFlags)0, thickness);
    }

    private static float GetStatusIconSpacing(float scale) {
        return MathF.Round(-8.0f * scale);
    }

    private static float GetStatusSectionLabelWidth(float scale) {
        return MathF.Round(78.0f * scale);
    }

    private static float GetStatusSectionCardPadding(float scale) {
        return MathF.Round(8.0f * scale);
    }

    private void DrawMergedStatusIconSections(
        IReadOnlyList<StatusSectionLayout> layouts,
        float sectionGap,
        float labelWidth,
        float gridWidth,
        float iconSize,
        float timerHeight,
        float spacing,
        float scale) {
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var cardPadding = GetStatusSectionCardPadding(scale);
        var totalHeight = 0.0f;
        var width = labelWidth + gridWidth + cardPadding * 2.0f + 18.0f * scale;
        var panelHeight = Math.Max(0, layouts.Count - 1) * sectionGap;
        for (var index = 0; index < layouts.Count; index++) {
            panelHeight += layouts[index].Size.Y;
        }

        DrawStatusSectionCard(drawList, start, start + new Vector2(width, panelHeight), layouts[0].Section.Color, scale, false);

        for (var index = 0; index < layouts.Count; index++) {
            var layout = layouts[index];
            var section = layout.Section;
            var gridSize = layout.GridSize;
            var sectionHeight = layout.Size.Y;
            var sectionMin = new Vector2(start.X, start.Y + totalHeight);
            var sectionMax = sectionMin + new Vector2(width, sectionHeight);
            if (index > 0) {
                drawList.AddLine(sectionMin + new Vector2(cardPadding, 0.0f), new Vector2(sectionMax.X - cardPadding, sectionMin.Y), ImGui.GetColorU32(StatusSectionDivider), 1.0f * scale);
            }

            ImGui.SetCursorScreenPos(sectionMin + new Vector2(cardPadding, cardPadding));
            this.DrawStatusSectionHeader(section.Label, section.Color, labelWidth, gridSize.Y, scale, false);
            DrawStatusIconGrid(section.Statuses, layout.Columns, iconSize, timerHeight, spacing, false);

            ImGui.SetCursorScreenPos(sectionMin);
            ImGui.Dummy(new Vector2(width, sectionHeight));
            totalHeight += sectionHeight + (index + 1 < layouts.Count ? sectionGap : 0.0f);
        }
    }

    private void DrawSplitStatusIconSections(
        IReadOnlyList<StatusSectionLayout> layouts,
        float sectionGap,
        float labelWidth,
        float iconSize,
        float timerHeight,
        float spacing,
        float scale) {
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var cardPadding = GetStatusSectionCardPadding(scale);
        var totalHeight = 0.0f;

        for (var index = 0; index < layouts.Count; index++) {
            var layout = layouts[index];
            var section = layout.Section;
            var gridSize = layout.GridSize;
            var sectionWidth = layout.Size.X;
            var sectionHeight = layout.Size.Y;
            var sectionMin = new Vector2(start.X, start.Y + totalHeight);
            var sectionMax = sectionMin + new Vector2(sectionWidth, sectionHeight);
            DrawStatusSectionCard(drawList, sectionMin, sectionMax, section.Color, scale, true);

            ImGui.SetCursorScreenPos(sectionMin + new Vector2(cardPadding, cardPadding));
            this.DrawStatusSectionHeader(section.Label, section.Color, labelWidth, gridSize.Y, scale, true);
            DrawStatusIconGrid(section.Statuses, layout.Columns, iconSize, timerHeight, spacing, true);

            ImGui.SetCursorScreenPos(sectionMin);
            ImGui.Dummy(new Vector2(sectionWidth, sectionHeight));
            totalHeight += sectionHeight + (index + 1 < layouts.Count ? sectionGap : 0.0f);
        }
    }

    private static void DrawStatusSectionCard(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector4 accentColor, float scale, bool splitMode) {
        var rounding = splitMode ? 18.0f * scale : 16.0f * scale;
        drawList.AddRectFilled(min + new Vector2(0.0f, 2.0f * scale), max + new Vector2(0.0f, 2.0f * scale), ImGui.GetColorU32(StatusPanelShadow), rounding);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(StatusPanelBackground), rounding);
        var highlightMin = min + new Vector2(1.0f * scale, 1.0f * scale);
        var highlightMax = new Vector2(max.X - 1.0f * scale, min.Y + Math.Max(1.0f, (max.Y - min.Y) * 0.42f));
        drawList.AddRectFilled(highlightMin, highlightMax, ImGui.GetColorU32(new Vector4(1.0f, 0.98f, 1.0f, 0.24f)), rounding, ImDrawFlags.RoundCornersTop);
        drawList.AddRect(min, max, ImGui.GetColorU32(StatusPanelBorder), rounding, (ImDrawFlags)0, 1.0f * scale);
        if (splitMode) {
            drawList.AddRect(min + new Vector2(1.0f, 1.0f), max - new Vector2(1.0f, 1.0f), ImGui.GetColorU32(new Vector4(accentColor.X, accentColor.Y, accentColor.Z, 0.12f)), rounding * 0.85f, (ImDrawFlags)0, 1.0f * scale);
        }
    }

    private void DrawStatusSectionHeader(string label, Vector4 accentColor, float labelWidth, float gridHeight, float scale, bool splitMode) {
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var labelHeight = MathF.Round(24.0f * scale);
        var labelMin = start + new Vector2(0.0f, Math.Max(0.0f, (gridHeight - labelHeight) * 0.5f));
        var labelMax = labelMin + new Vector2(labelWidth, labelHeight);
        drawList.AddRectFilled(labelMin, labelMax, ImGui.GetColorU32(StatusSectionLabelBackground), 999.0f);
        drawList.AddRect(labelMin, labelMax, ImGui.GetColorU32(StatusSectionLabelBorder), 999.0f, (ImDrawFlags)0, 1.0f * scale);
        drawList.AddCircleFilled(labelMin + new Vector2(12.0f * scale, labelHeight * 0.5f), 3.5f * scale, ImGui.GetColorU32(accentColor));
        DrawShadowText(drawList, labelMin + new Vector2(22.0f * scale, Math.Max(0.0f, (labelHeight - ImGui.GetTextLineHeight()) * 0.5f)), ImGui.GetColorU32(new Vector4(0.46f, 0.27f, 0.36f, 0.98f)), label, GetFontSize(scale));
        ImGui.SetCursorScreenPos(new Vector2(labelMax.X + 18.0f * scale, start.Y));
    }

    private void DrawStatusIconGrid(IReadOnlyList<StatusEntry> statuses, int columns, float iconSize, float timerHeight, float spacing, bool splitMode) {
        var gridStart = ImGui.GetCursorPos();
        var pitchX = iconSize + spacing;
        var pitchY = iconSize + spacing + timerHeight;

        for (var index = 0; index < statuses.Count; index++) {
            var row = index / columns;
            var column = index % columns;
            ImGui.SetCursorPos(gridStart + new Vector2(column * pitchX, row * pitchY));
            DrawStatusIcon(statuses[index], iconSize);
        }

        var gridSize = GetStatusIconGridSize(statuses.Count, columns, iconSize, spacing, timerHeight);
        ImGui.SetCursorPos(gridStart);
        ImGui.Dummy(gridSize + new Vector2(0.0f, splitMode ? 1.0f : 0.0f));
    }

    private static Vector2 GetStatusIconGridSize(int count, int columns, float iconSize, float spacing, float timerHeight) {
        if (count <= 0) {
            return Vector2.Zero;
        }

        columns = Math.Max(1, columns);
        var rows = (int)Math.Ceiling(count / (float)columns);
        var width = columns * iconSize + Math.Max(0, columns - 1) * spacing;
        var height = rows * iconSize + Math.Max(0, rows - 1) * (spacing + timerHeight) + timerHeight;
        return new Vector2(width, height);
    }

    private static float GetNativeStatusOverlayIconSize(float scale) {
        return Math.Clamp(NativeStatusIconSize * Math.Clamp(scale, 0.5f, 2.5f), MinimumIconSize, MaximumIconSize);
    }

    private static float GetNativeStatusTimerHeight(float iconSize) {
        var baseFontSize = ImGui.GetFontSize();
        var fontSize = MathF.Round(Math.Clamp(iconSize * 0.5f, baseFontSize * 0.92f, baseFontSize * 1.18f));
        return fontSize + 2.0f;
    }

    private static float GetStatusTimerTextHeight(float iconSize, float fontScale = 1.0f) {
        var baseFontSize = ImGui.GetFontSize() * fontScale;
        var timerFontSize = Math.Clamp(iconSize * 0.52f, baseFontSize * 0.92f, baseFontSize * 1.40f);
        return timerFontSize + 8.0f;
    }

    private void DrawStatusIcon(StatusEntry status, float size) {
        var drawList = ImGui.GetWindowDrawList();

        DrawGameIcon(status.IconId, size);

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        if (ShouldShowStatusTimer(status)) {
            var timerColor = status.IsSelfApplied
                ? ImGui.GetColorU32(this.config.SelfAppliedTimerColor)
                : ImGui.GetColorU32(this.config.OtherAppliedTimerColor);
            DrawStatusTimerText(FormatCooldownIconTime(status.RemainingSeconds), min, max, timerColor, size);
        }

        if (ImGui.IsItemHovered()) {
            DrawStatusTooltip(status, this.config.ShowRawStatusIds);
        }
    }

    private void DrawStatusTooltip(StatusEntry status, bool showRawStatusId) {
        DrawStyledTooltip(() => {
            ImGui.TextUnformatted(GetStatusTooltipTitle(status));
            ImGui.Separator();
            ImGui.TextUnformatted($"来源：{FormatSourceLabel(status.SourceName, status.SourceJobName, this.config.ShowSourceJobNames)}");
            if (showRawStatusId) {
                ImGui.TextUnformatted($"状态ID：{status.StatusId}");
            }
        });
    }

    private static string GetStatusTooltipTitle(StatusEntry status) {
        return $"{status.Name} ({status.StatusId})";
    }

    private void DrawGameIcon(uint iconId, float size) {
        var iconSize = new Vector2(size, size);
        if (iconId == 0) {
            ImGui.Dummy(iconSize);
            return;
        }

        if (!TryGetFrameGameIconWrap(iconId, out var wrap) || wrap is null) {
            ImGui.Dummy(iconSize);
            return;
        }

        var drawSize = GetAspectFitSize(wrap.Size, iconSize);
        var cursorPosition = ImGui.GetCursorPos();
        ImGui.SetCursorPos(cursorPosition + (iconSize - drawSize) * 0.5f);
        ImGui.Image(wrap.Handle, drawSize);
        ImGui.SetCursorPos(cursorPosition + iconSize);
    }

    private bool DrawGameIconImage(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max, bool fillBounds = false, bool bypassMissingRetry = false) {
        if (iconId == 0) {
            return false;
        }

        if (!TryGetFrameGameIconWrap(iconId, out var wrap, bypassMissingRetry) || wrap is null) {
            return false;
        }

        if (fillBounds) {
            var bounds = max - min;
            var sourceAspect = wrap.Size.X / Math.Max(1.0f, wrap.Size.Y);
            var boundsAspect = bounds.X / Math.Max(1.0f, bounds.Y);
            var uvMin = Vector2.Zero;
            var uvMax = Vector2.One;

            if (sourceAspect > boundsAspect) {
                var visibleWidthRatio = boundsAspect / sourceAspect;
                var trim = (1.0f - visibleWidthRatio) * 0.5f;
                uvMin.X = trim;
                uvMax.X = 1.0f - trim;
            } else if (sourceAspect < boundsAspect) {
                var visibleHeightRatio = sourceAspect / boundsAspect;
                var trim = (1.0f - visibleHeightRatio) * 0.5f;
                uvMin.Y = trim;
                uvMax.Y = 1.0f - trim;
            }

            drawList.AddImage(wrap.Handle, min, max, uvMin, uvMax);
        } else {
            var bounds = max - min;
            var drawSize = GetAspectFitSize(wrap.Size, bounds);
            var drawMin = min + (bounds - drawSize) * 0.5f;
            drawList.AddImage(wrap.Handle, drawMin, drawMin + drawSize);
        }

        return true;
    }

    private bool TryGetFrameGameIconWrap(uint iconId, out IDalamudTextureWrap? wrap, bool bypassMissingRetry = false) {
        wrap = null;
        if (iconId == 0) {
            return false;
        }

        var now = DateTime.UtcNow;
        if (!bypassMissingRetry && this.missingGameIconRetryAt.TryGetValue(iconId, out var retryAt) && now < retryAt) {
            return false;
        }

        if (this.frameGameIconWrapCache.TryGetValue(iconId, out wrap)) {
            return wrap is not null;
        }

        if (TryGetGameIconWrap(iconId, true, out wrap) || TryGetGameIconWrap(iconId, false, out wrap)) {
            this.frameGameIconWrapCache[iconId] = wrap;
            this.missingGameIconRetryAt.Remove(iconId);
            return true;
        }

        this.frameGameIconWrapCache[iconId] = null;
        this.missingGameIconRetryAt[iconId] = now + MissingGameIconRetryDelay;
        TrimMissingGameIconRetryCache();
        wrap = null;
        return false;
    }

    private bool TryGetGameIconWrap(uint iconId, bool hiRes, out IDalamudTextureWrap? wrap) {
        wrap = null;
        var cacheKey = new GameIconCacheKey(iconId, hiRes);

        try {
            if (this.gameIconTextureCache.TryGetValue(cacheKey, out var cachedTexture)) {
                if (cachedTexture.TryGetWrap(out wrap, out _) && wrap is not null) {
                    return true;
                }

                this.gameIconTextureCache.Remove(cacheKey);
            }

            var lookup = new GameIconLookup {
                IconId = iconId,
                HiRes = hiRes,
            };
            if (this.textureProvider.TryGetFromGameIcon(lookup, out var texture) && texture is not null) {
                this.gameIconTextureCache[cacheKey] = texture;
                if (texture.TryGetWrap(out wrap, out _) && wrap is not null) {
                    TrimGameIconTextureCache();
                    return true;
                }

                this.gameIconTextureCache.Remove(cacheKey);
            }
        } catch {
            this.gameIconTextureCache.Remove(cacheKey);
        }

        wrap = null;
        return false;
    }

    private void TrimGameIconTextureCache() {
        const int maxCachedGameIcons = 512;
        if (this.gameIconTextureCache.Count <= maxCachedGameIcons) {
            return;
        }

        foreach (var key in this.gameIconTextureCache.Keys.Take(Math.Max(1, this.gameIconTextureCache.Count - maxCachedGameIcons)).ToList()) {
            this.gameIconTextureCache.Remove(key);
        }
    }

    private void TrimMissingGameIconRetryCache() {
        const int maxMissingIconEntries = 256;
        if (this.missingGameIconRetryAt.Count <= maxMissingIconEntries) {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var iconId in this.missingGameIconRetryAt
            .Where(pair => pair.Value <= now)
            .Select(pair => pair.Key)
            .ToList()) {
            this.missingGameIconRetryAt.Remove(iconId);
        }

        if (this.missingGameIconRetryAt.Count > maxMissingIconEntries) {
            this.missingGameIconRetryAt.Clear();
        }
    }

    private static Vector2 GetAspectFitSize(Vector2 sourceSize, Vector2 bounds) {
        if (sourceSize.X <= 0.0f || sourceSize.Y <= 0.0f) {
            return bounds;
        }

        var scale = Math.Min(bounds.X / sourceSize.X, bounds.Y / sourceSize.Y);
        return new Vector2(sourceSize.X * scale, sourceSize.Y * scale);
    }

    private void DrawCenteredIconText(ImDrawListPtr drawList, string text, Vector2 min, Vector2 max, uint color, float iconSize, bool bold = false, float horizontalPadding = 2.0f) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        var baseFontSize = ImGui.GetFontSize();
        var fontSize = Math.Clamp(iconSize * 0.40f, baseFontSize * 0.78f, baseFontSize * 1.08f);
        var fontHandle = GetHudFont(GetNearestHudFontSize(fontSize));
        if (!fontHandle.Available) {
            return;
        }

        var font = fontHandle.Push();
        var textSize = ImGui.CalcTextSize(text);
        var maxTextWidth = Math.Max(1.0f, max.X - min.X - horizontalPadding * 2.0f);
        var maxTextHeight = Math.Max(1.0f, max.Y - min.Y - 2.0f);
        if (textSize.X > maxTextWidth || textSize.Y > maxTextHeight) {
            font.Dispose();
            var shrink = Math.Min(maxTextWidth / Math.Max(1.0f, textSize.X), maxTextHeight / Math.Max(1.0f, textSize.Y));
            fontSize = Math.Max(baseFontSize * 0.52f, fontSize * shrink);
            fontHandle = GetHudFont(GetNearestHudFontSize(fontSize));
            if (!fontHandle.Available) {
                return;
            }

            font = fontHandle.Push();
            textSize = ImGui.CalcTextSize(text);
        }

        var position = SnapToPixel((min + max) * 0.5f - textSize * 0.5f);
        var shadowColor = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.95f));
        drawList.AddText(position + new Vector2(1.0f, 1.0f), shadowColor, text);
        if (bold) {
            drawList.AddText(position + new Vector2(1.0f, 0.0f), color, text);
        }

        drawList.AddText(position, color, text);
        font.Dispose();
    }

    private void DrawStatusTimerText(string text, Vector2 min, Vector2 max, uint textColor, float iconSize) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        // 原生状态栏的倒计时在图标下方居中显示，不压在图标里面。
        var baseFontSize = ImGui.GetFontSize();
        var fontSize = MathF.Round(Math.Clamp(iconSize * 0.5f, baseFontSize * 0.92f, baseFontSize * 1.18f));
        using var font = PushHudFont(fontSize);
        var textSize = ImGui.CalcTextSize(text);
        var visualTightOffsetY = MathF.Round(fontSize * 0.42f);
        var textPos = new Vector2(
            MathF.Round(min.X + ((max.X - min.X) - textSize.X) * 0.5f),
            MathF.Round(max.Y - visualTightOffsetY));
        var edgeColor = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.96f));
        const float outlineOffset = 1.0f;
        drawList.AddText(textPos + new Vector2(-outlineOffset, -outlineOffset), edgeColor, text);
        drawList.AddText(textPos + new Vector2(-outlineOffset, 0.0f), edgeColor, text);
        drawList.AddText(textPos + new Vector2(-outlineOffset, outlineOffset), edgeColor, text);
        drawList.AddText(textPos + new Vector2(0.0f, -outlineOffset), edgeColor, text);
        drawList.AddText(textPos + new Vector2(0.0f, outlineOffset), edgeColor, text);
        drawList.AddText(textPos + new Vector2(outlineOffset, -outlineOffset), edgeColor, text);
        drawList.AddText(textPos + new Vector2(outlineOffset, 0.0f), edgeColor, text);
        drawList.AddText(textPos + new Vector2(outlineOffset, outlineOffset), edgeColor, text);
        drawList.AddText(textPos + new Vector2(1.0f, 0.0f), textColor, text);
        drawList.AddText(textPos, textColor, text);
    }

    private bool IsMitigationCooldownsVisible() {
        return this.config.ShowPartyMitigationCooldowns
               || this.config.ShowTargetMitigationCooldowns
               || this.config.ShowPersonalMitigationCooldowns
               || this.config.ShowMitigationCooldowns;
    }

    private static Vector4 GetCooldownSectionColor(CooldownGroup group) {
        return NormalizeCooldownGroup(group) switch {
            CooldownGroup.Common => PersonalSkillColor,
            CooldownGroup.Personal => PersonalSkillColor,
            CooldownGroup.Burst => PersonalSkillColor,
            CooldownGroup.PartyMitigation => MitigationColor,
            CooldownGroup.PersonalMitigation => SelfBuffColor,
            CooldownGroup.RaidBuff => RaidBuffColor,
            CooldownGroup.Mitigation => MitigationColor,
            _ => HeaderColor,
        };
    }

    private static string FormatStatusTime(StatusEntry status) {
        if (status.RemainingSeconds >= 9999.0f) {
            return "永久";
        }

        if (IsLongOrPermanentStatus(status)) {
            return $"{status.RemainingSeconds / 60.0f:0.0}分";
        }

        return $"{status.RemainingSeconds:0.0}秒";
    }

    private static bool IsLongOrPermanentStatus(StatusEntry status) {
        return status.RemainingSeconds >= 9999.0f
               || status.RemainingSeconds >= LongStatusThresholdSeconds
               || status.MaxSeconds >= LongStatusThresholdSeconds;
    }

    private static bool ShouldShowStatusTimer(StatusEntry status) {
        return status.RemainingSeconds > 0.05f
               && status.RemainingSeconds < 9999.0f
               && status.MaxSeconds > 0.0f;
    }

    private string FormatCooldownIconTime(float seconds) {
        var totalSeconds = Math.Max(0, (int)MathF.Ceiling(seconds));
        if (this.cooldownIconTimeTextCache.TryGetValue(totalSeconds, out var cachedText)) {
            return cachedText;
        }

        string text;
        if (totalSeconds <= 120) {
            text = totalSeconds.ToString();
        } else {
            var minutes = totalSeconds / 60;
            var remainingSeconds = totalSeconds % 60;
            text = $"{minutes}:{remainingSeconds:00}";
        }

        this.cooldownIconTimeTextCache[totalSeconds] = text;
        return text;
    }

    private static CooldownGroup NormalizeCooldownGroup(CooldownGroup group) {
        return group == CooldownGroup.TargetMitigation ? CooldownGroup.PartyMitigation : group;
    }

    private static string FormatSourceLabel(string sourceName, string sourceJobName, bool showJobName) {
        if (string.IsNullOrWhiteSpace(sourceName) || sourceName == "Unknown") {
            return string.Empty;
        }

        return showJobName && !string.IsNullOrWhiteSpace(sourceJobName) ? sourceJobName : sourceName;
    }
}

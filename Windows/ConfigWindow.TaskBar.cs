using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace AllHud.Windows;

public sealed partial class ConfigWindow {
    private void DrawTaskBarPage() {
        DrawTaskBarPageTabs();
        ImGui.Spacing();

        switch (this.selectedTaskBarPage) {
            case TaskBarPage.任务栏:
                DrawTaskBarBasicsPage();
                break;
            case TaskBarPage.辅助栏:
                DrawAuxiliaryBarPage();
                break;
            case TaskBarPage.插件收纳:
                DrawTaskBarPluginDockPage();
                break;
            case TaskBarPage.高级:
                DrawTaskBarAdvancedPage();
                break;
        }
    }

    private void DrawTaskBarBasicsPage() {
        DrawTargetInfoSubsection("主栏设置");
        DrawCheckbox("启动主栏", nameof(this.config.ShowTaskBar), this.config.ShowTaskBar, value => this.config.ShowTaskBar = value);
        if (!this.config.ShowTaskBar) {
            return;
        }

        DrawTaskBarEdgeSelector();
        DrawInlineSegmentedSelector("宽度", "task_bar_width", this.config.TaskBarStretchToEdges ? 1 : 0, value => SetTaskBarStretchToEdges(value == 1), ("自适应", 0), ("铺满", 1));

        DrawInlineHudScaleCombo("缩放", "TaskBarScale", this.config.TaskBarScale, value => this.config.TaskBarScale = value);
        ImGui.SameLine(0.0f, 10.0f);
        DrawTaskBarOpacitySlider();
        if (!this.config.TaskBarStretchToEdges) {
            ImGui.SameLine(0.0f, 10.0f);
            DrawTaskBarHorizontalOffsetSlider();
        }

        ImGui.Spacing();
        DrawTaskBarComponentsPage();
    }

    private void DrawAuxiliaryBarPage() {
        DrawTargetInfoSubsection("辅助栏设置");
        this.config.AuxiliaryBars ??= [new AuxiliaryBarDefinition()];
        if (this.config.AuxiliaryBars.Count == 0) {
            this.config.AuxiliaryBars.Add(new AuxiliaryBarDefinition());
        }

        this.selectedAuxiliaryBarIndex = Math.Clamp(this.selectedAuxiliaryBarIndex, 0, this.config.AuxiliaryBars.Count - 1);
        DrawAuxiliaryBarTabs();
        ImGui.Spacing();
        DrawAuxiliaryBarEditor(this.config.AuxiliaryBars[this.selectedAuxiliaryBarIndex], this.selectedAuxiliaryBarIndex);

        ImGui.Spacing();
        DrawTargetInfoSubsection("辅助栏组件");
        DrawAuxiliaryBarComponentsPage(this.config.AuxiliaryBars[this.selectedAuxiliaryBarIndex], this.selectedAuxiliaryBarIndex);
    }

    private void DrawAuxiliaryBarTabs() {
        var start = ImGui.GetCursorScreenPos();
        for (var index = 0; index < this.config.AuxiliaryBars.Count; index++) {
            if (index > 0) {
                ImGui.SameLine(0.0f, 2.0f);
            }

            DrawAuxiliaryBarTab(this.config.AuxiliaryBars[index], index);
        }

        ImGui.SameLine(0.0f, 2.0f);
        if (DrawAuxiliaryBarAddTab()) {
            this.config.AuxiliaryBars.Add(new AuxiliaryBarDefinition { Name = "辅助栏" });
            this.selectedAuxiliaryBarIndex = this.config.AuxiliaryBars.Count - 1;
            SyncLegacyAuxiliaryBarSettings();
            this.saveConfig();
        }

        var lineY = start.Y + 27.0f;
        var lineBounds = GetSectionCardLineBounds();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(
            new Vector2(lineBounds.Left, lineY),
            new Vector2(lineBounds.Right, lineY),
            ImGui.GetColorU32(new Vector4(0.92f, 0.68f, 0.76f, 0.34f)),
            1.0f);
    }

    private void DrawAuxiliaryBarTab(AuxiliaryBarDefinition bar, int index) {
        var selected = this.selectedAuxiliaryBarIndex == index;
        var label = GetAuxiliaryBarDisplayName(bar, index);
        var accentColor = GetPageAccentColor(ConfigPage.任务栏);
        var canDelete = this.config.AuxiliaryBars.Count > 1;
        var textSize = ImGui.CalcTextSize(label);
        var buttonSize = new Vector2(Math.Clamp(textSize.X + (canDelete ? 40.0f : 22.0f), 72.0f, 138.0f), 28.0f);
        var buttonMin = ImGui.GetCursorScreenPos();
        var buttonMax = buttonMin + buttonSize;
        var deleteSize = new Vector2(16.0f, 16.0f);
        var deleteMin = new Vector2(buttonMax.X - deleteSize.X - 5.0f, buttonMin.Y + 6.0f);
        var deleteMax = deleteMin + deleteSize;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton($"auxiliary_bar_tab_{index}", buttonSize);
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked()) {
            if (ImGui.GetIO().KeyCtrl) {
                this.selectedAuxiliaryBarIndex = index;
                ImGui.OpenPopup("RenameAuxiliaryBar");
            } else {
                this.selectedAuxiliaryBarIndex = index;
            }
        }

        var fillColor = selected
            ? new Vector4(1.0f, 0.975f, 0.980f, 0.96f)
            : hovered
                ? new Vector4(1.0f, 0.945f, 0.960f, 0.82f)
                : new Vector4(1.0f, 0.955f, 0.965f, 0.58f);
        var borderColor = selected ? WithAlpha(accentColor, 0.62f) : WithAlpha(accentColor, hovered ? 0.36f : 0.22f);
        var textColor = selected ? WithAlpha(accentColor, 1.0f) : new Vector4(0.46f, 0.30f, 0.36f, 0.92f);

        drawList.AddRectFilled(buttonMin, buttonMax, ImGui.GetColorU32(fillColor), 5.0f, ImDrawFlags.RoundCornersTop);
        drawList.AddRect(buttonMin, buttonMax, ImGui.GetColorU32(borderColor), 5.0f, ImDrawFlags.RoundCornersTop, selected ? 1.2f : 1.0f);
        if (selected) {
            drawList.AddLine(
                new Vector2(buttonMin.X + 1.0f, buttonMax.Y),
                new Vector2(buttonMax.X - 1.0f, buttonMax.Y),
                ImGui.GetColorU32(new Vector4(1.0f, 0.975f, 0.980f, 1.0f)),
                2.0f);
        }

        var textX = canDelete
            ? buttonMin.X + 11.0f
            : buttonMin.X + (buttonSize.X - textSize.X) * 0.5f;
        drawList.AddText(new Vector2(textX, buttonMin.Y + (buttonSize.Y - textSize.Y) * 0.5f), ImGui.GetColorU32(textColor), label);

        if (canDelete) {
            var deleteHovered = ImGui.IsMouseHoveringRect(deleteMin, deleteMax);
            var deleteColor = deleteHovered
                ? new Vector4(0.78f, 0.24f, 0.36f, 1.0f)
                : new Vector4(0.56f, 0.34f, 0.42f, 0.74f);
            drawList.AddText(deleteMin + new Vector2(3.0f, -1.0f), ImGui.GetColorU32(deleteColor), "×");
            if (deleteHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                this.config.AuxiliaryBars.RemoveAt(index);
                this.selectedAuxiliaryBarIndex = Math.Clamp(this.selectedAuxiliaryBarIndex, 0, this.config.AuxiliaryBars.Count - 1);
                SyncLegacyAuxiliaryBarSettings();
                this.saveConfig();
            }
        }

        if (this.selectedAuxiliaryBarIndex == index) {
            DrawAuxiliaryBarRenamePopup(bar, index);
        }
    }

    private bool DrawAuxiliaryBarAddTab() {
        const string label = "+";
        var accentColor = GetPageAccentColor(ConfigPage.任务栏);
        var textSize = ImGui.CalcTextSize(label);
        var buttonSize = new Vector2(30.0f, 28.0f);
        var buttonMin = ImGui.GetCursorScreenPos();
        var buttonMax = buttonMin + buttonSize;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton("auxiliary_bar_add_tab", buttonSize);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var clicked = ImGui.IsItemClicked();
        var fillColor = active
            ? WithAlpha(accentColor, 0.22f)
            : hovered
                ? WithAlpha(accentColor, 0.14f)
                : new Vector4(1.0f, 0.955f, 0.965f, 0.48f);

        drawList.AddRectFilled(buttonMin, buttonMax, ImGui.GetColorU32(fillColor), 5.0f, ImDrawFlags.RoundCornersTop);
        drawList.AddRect(buttonMin, buttonMax, ImGui.GetColorU32(WithAlpha(accentColor, hovered ? 0.40f : 0.22f)), 5.0f, ImDrawFlags.RoundCornersTop);
        drawList.AddText(buttonMin + (buttonSize - textSize) * 0.5f, ImGui.GetColorU32(new Vector4(0.48f, 0.30f, 0.38f, 0.94f)), label);
        return ImGui.IsItemClicked();
    }

    private void DrawAuxiliaryBarEditor(AuxiliaryBarDefinition bar, int index) {
        ImGui.PushID($"AuxiliaryBarEditor_{index}");

        var showVerticalOffset = bar.Enabled && bar.PositionMode != 2 && !bar.StretchToEdges;
        var drawList = ImGui.GetWindowDrawList();
        var cardMin = ImGui.GetCursorScreenPos();
        var cardWidth = Math.Max(360.0f, ImGui.GetContentRegionAvail().X - 8.0f);
        var cardHeight = bar.Enabled ? bar.PositionMode == 2 || showVerticalOffset ? 154.0f : 126.0f : 42.0f;
        var cardMax = cardMin + new Vector2(cardWidth, cardHeight);
        drawList.AddRectFilled(cardMin, cardMax, ImGui.GetColorU32(new Vector4(1.0f, 0.975f, 0.980f, 0.72f)), 8.0f);
        drawList.AddRect(cardMin, cardMax, ImGui.GetColorU32(new Vector4(0.92f, 0.68f, 0.76f, 0.34f)), 8.0f);
        ImGui.SetCursorScreenPos(cardMin + new Vector2(12.0f, 9.0f));

        DrawCheckbox("启用", nameof(bar.Enabled), bar.Enabled, value => {
            bar.Enabled = value;
            SyncLegacyAuxiliaryBarSettings();
        });

        ImGui.SameLine(0.0f, 12.0f);
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Ctrl + 点击标签改名");

        if (bar.Enabled) {
            ImGui.SetCursorScreenPos(cardMin + new Vector2(12.0f, 40.0f));
            DrawAuxiliaryBarPositionSelector(bar, index);

            ImGui.SetCursorScreenPos(cardMin + new Vector2(12.0f, 68.0f));
            DrawInlineSegmentedSelector(bar.PositionMode == 2 ? "尺寸" : "高度", $"auxiliary_bar_height_{index}", bar.StretchToEdges ? 1 : 0, value => bar.StretchToEdges = value == 1, ("自适应", 0), ("铺满", 1));
            if (bar.PositionMode == 2) {
                ImGui.SameLine(0.0f, 10.0f);
                DrawInlineSegmentedSelector("方向", $"auxiliary_bar_direction_{index}", bar.LayoutDirection, value => bar.LayoutDirection = value, ("竖向", 0), ("横向", 1));
            }

            if (bar.PositionMode == 2) {
                ImGui.SetCursorScreenPos(cardMin + new Vector2(12.0f, 96.0f));
                ImGui.TextDisabled("自定义位置：拖动辅助栏调整位置");
            } else if (showVerticalOffset) {
                ImGui.SetCursorScreenPos(cardMin + new Vector2(12.0f, 96.0f));
                DrawAuxiliaryBarVerticalOffsetSlider(bar, index);
            }

            ImGui.SetCursorScreenPos(cardMin + new Vector2(12.0f, bar.PositionMode == 2 || showVerticalOffset ? 124.0f : 96.0f));
            DrawInlineHudScaleCombo("缩放", $"AuxiliaryBarScale{index}", bar.Scale, value => {
                bar.Scale = value;
                SyncLegacyAuxiliaryBarSettings();
            });
            ImGui.SameLine(0.0f, 10.0f);
            DrawAuxiliaryBarOpacitySlider(bar, index);
        }

        ImGui.SetCursorScreenPos(new Vector2(cardMin.X, cardMax.Y + 4.0f));
        ImGui.PopID();
    }

    private void DrawAuxiliaryBarRenamePopup(AuxiliaryBarDefinition bar, int index) {
        if (!ImGui.BeginPopup("RenameAuxiliaryBar")) {
            return;
        }

        var nameBuffer = CreateUtf8Buffer(GetAuxiliaryBarDisplayName(bar, index), 96);
        ImGui.SetNextItemWidth(180.0f);
        if (ImGui.InputText("##AuxiliaryBarRename", nameBuffer, ImGuiInputTextFlags.None)) {
            var nextName = ReadUtf8Buffer(nameBuffer);
            bar.Name = string.IsNullOrWhiteSpace(nextName) ? "辅助栏" : nextName;
            SyncLegacyAuxiliaryBarSettings();
            this.saveConfig();
        }

        if (ImGui.Button("确定")) {
            var nextName = ReadUtf8Buffer(nameBuffer);
            bar.Name = string.IsNullOrWhiteSpace(nextName) ? "辅助栏" : nextName;
            SyncLegacyAuxiliaryBarSettings();
            this.saveConfig();
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("取消")) {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static string GetAuxiliaryBarDisplayName(AuxiliaryBarDefinition bar, int index) {
        return string.IsNullOrWhiteSpace(bar.Name) ? "辅助栏" : bar.Name.Trim();
    }

    private void DrawAuxiliaryBarComponentsPage(AuxiliaryBarDefinition bar, int barIndex) {
        var selectedOrder = GetSelectedAuxiliaryComponentOrder(bar, barIndex);
        if (selectedOrder is null || !selectedOrder.Any(id => id.Equals(this.selectedAuxiliaryComponentSettingsId, StringComparison.OrdinalIgnoreCase))) {
            ClearSelectedAuxiliaryComponentSettings();
        }

        ImGui.BeginChild("AuxiliaryComponentList", new Vector2(0.0f, 0.0f), false);
        if (bar.StretchToEdges) {
            var horizontal = IsAuxiliaryBarHorizontal(bar);
            DrawAuxiliaryBarComponentSection(bar, barIndex, horizontal ? "左侧" : "上方", bar.SectionStartComponentOrder, "start");
            ImGui.Spacing();
            DrawAuxiliaryBarComponentSection(bar, barIndex, "中间", bar.SectionCenterComponentOrder, "center");
            ImGui.Spacing();
            DrawAuxiliaryBarComponentSection(bar, barIndex, horizontal ? "右侧" : "下方", bar.SectionEndComponentOrder, "end");
            ImGui.EndChild();
            return;
        }

        bar.ComponentOrder = Configuration.NormalizeAuxiliaryComponentOrder(bar.ComponentOrder);
        var activeComponents = bar.ComponentOrder
            .Where(IsPlacedComponentId)
            .Select(GetComponentDefinition)
            .Where(component => !string.IsNullOrWhiteSpace(component.Id))
            .ToList();

        if (activeComponents.Count == 0) {
            ImGui.TextDisabled("还没有添加组件。点击添加组件开始配置辅助栏。");
        }

        for (var index = 0; index < activeComponents.Count; index++) {
            DrawAuxiliaryBarComponentRow(bar, barIndex, activeComponents[index], bar.ComponentOrder, "main");
        }

        DrawAuxiliaryBarDraggedComponentPreview(activeComponents, $"aux:{barIndex}:main:");

        ImGui.Spacing();
        if (ImGui.Button("添加组件")) {
            ImGui.OpenPopup($"AllHud_AuxiliaryBar_AddComponent_{barIndex}");
        }

        DrawAuxiliaryBarAddComponentPopup(bar, barIndex, bar.ComponentOrder, $"AllHud_AuxiliaryBar_AddComponent_{barIndex}", "添加到辅助栏");
        ImGui.EndChild();
    }

    private void DrawAuxiliaryBarComponentSection(AuxiliaryBarDefinition bar, int barIndex, string label, List<string> componentOrder, string sectionKey) {
        componentOrder = NormalizeAuxiliarySectionOrderReference(bar, componentOrder, sectionKey);
        DrawTargetInfoSubsection(label);
        var activeComponents = componentOrder
            .Where(IsPlacedComponentId)
            .Select(GetComponentDefinition)
            .Where(component => !string.IsNullOrWhiteSpace(component.Id))
            .ToList();

        if (activeComponents.Count == 0) {
            ImGui.TextDisabled("还没有添加组件。");
        }

        foreach (var component in activeComponents) {
            DrawAuxiliaryBarComponentRow(bar, barIndex, component, componentOrder, sectionKey);
        }

        DrawAuxiliaryBarDraggedComponentPreview(activeComponents, $"aux:{barIndex}:{sectionKey}:");

        ImGui.Spacing();
        var popupId = $"AllHud_AuxiliaryBar_AddComponent_{barIndex}_{sectionKey}";
        if (ImGui.Button($"添加组件##aux_{barIndex}_{sectionKey}")) {
            ImGui.OpenPopup(popupId);
        }

        DrawAuxiliaryBarAddComponentPopup(bar, barIndex, componentOrder, popupId, $"添加到{label}");
    }

    private static bool IsAuxiliaryBarHorizontal(AuxiliaryBarDefinition bar) {
        return bar.PositionMode == 2 && bar.LayoutDirection == 1;
    }

    private static List<string> NormalizeAuxiliarySectionOrderReference(AuxiliaryBarDefinition bar, List<string> componentOrder, string sectionKey) {
        var normalized = Configuration.NormalizeAuxiliaryComponentOrder(componentOrder);
        switch (sectionKey) {
            case "start":
                bar.SectionStartComponentOrder = normalized;
                return bar.SectionStartComponentOrder;
            case "end":
                bar.SectionEndComponentOrder = normalized;
                return bar.SectionEndComponentOrder;
            default:
                bar.SectionCenterComponentOrder = normalized;
                return bar.SectionCenterComponentOrder;
        }
    }

    private void DrawAuxiliaryBarComponentRow(AuxiliaryBarDefinition bar, int barIndex, TaskBarComponentDefinition component, List<string> componentOrder, string sectionKey) {
        ImGui.PushID($"AuxiliaryBarComponent_{barIndex}_{sectionKey}_{component.Id}");
        var drawList = ImGui.GetWindowDrawList();
        var rowMin = ImGui.GetCursorScreenPos();
        var rowWidth = Math.Max(260.0f, ImGui.GetContentRegionAvail().X - 8.0f);
        const float rowHeight = 40.0f;
        var rowMax = rowMin + new Vector2(rowWidth, rowHeight);
        var hoveredRow = ImGui.IsMouseHoveringRect(rowMin, rowMax);
        var draggingId = $"aux:{barIndex}:{sectionKey}:{component.Id}";
        var draggingThis = this.draggingTaskBarComponentId.Equals(draggingId, StringComparison.OrdinalIgnoreCase);
        var hasSettings = ComponentHasSettings(component.Id);
        var selected = hasSettings
                       && this.selectedAuxiliaryComponentSettingsId.Equals(component.Id, StringComparison.OrdinalIgnoreCase)
                       && this.selectedAuxiliaryComponentSettingsScope.Equals(sectionKey, StringComparison.OrdinalIgnoreCase)
                       && this.selectedAuxiliaryComponentSettingsBarIndex == barIndex;

        var bgColor = draggingThis
            ? new Vector4(0.98f, 0.92f, 0.98f, 0.72f)
            : selected
                ? new Vector4(1.0f, 0.925f, 0.955f, 0.96f)
                : hoveredRow
                    ? new Vector4(1.0f, 0.955f, 0.970f, 0.94f)
                    : new Vector4(1.0f, 0.975f, 0.980f, 0.82f);
        var borderColor = draggingThis
            ? new Vector4(0.66f, 0.36f, 0.78f, 0.72f)
            : selected
                ? new Vector4(0.86f, 0.34f, 0.58f, 0.78f)
                : new Vector4(0.92f, 0.68f, 0.76f, 0.46f);

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(rowMin);
        var rowPressed = ImGui.InvisibleButton("aux_row_drag_area", new Vector2(Math.Max(48.0f, rowWidth - 146.0f), rowHeight));
        var dragAreaHovered = ImGui.IsItemHovered();
        var dragAreaActive = ImGui.IsItemActive();
        if (dragAreaActive && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            this.draggingTaskBarComponentId = draggingId;
        }

        if (hasSettings && rowPressed && !draggingThis) {
            ToggleSelectedAuxiliaryComponentSettings(component.Id, barIndex, sectionKey);
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && draggingThis) {
            this.draggingTaskBarComponentId = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(this.draggingTaskBarComponentId)
            && this.draggingTaskBarComponentId.StartsWith($"aux:{barIndex}:{sectionKey}:", StringComparison.OrdinalIgnoreCase)
            && !draggingThis
            && hoveredRow
            && ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
            var draggedComponentId = GetAuxiliaryDragComponentId(this.draggingTaskBarComponentId, barIndex, sectionKey);
            var insertAfter = ImGui.GetMousePos().Y > rowMin.Y + rowHeight * 0.5f;
            var indicatorY = insertAfter ? rowMax.Y - 1.0f : rowMin.Y + 1.0f;
            drawList.AddLine(new Vector2(rowMin.X + 10.0f, indicatorY), new Vector2(rowMax.X - 10.0f, indicatorY), ImGui.GetColorU32(new Vector4(0.65f, 0.34f, 0.76f, 0.82f)), 2.0f);
            MoveAuxiliaryBarComponentRelativeTo(componentOrder, draggedComponentId, component.Id, insertAfter);
        }

        var handleColor = dragAreaHovered || draggingThis
            ? new Vector4(0.56f, 0.32f, 0.44f, 0.95f)
            : new Vector4(0.50f, 0.36f, 0.42f, 0.70f);
        DrawTaskBarComponentIcon(drawList, component.Id, rowMin + new Vector2(25.0f, rowHeight * 0.5f), handleColor);

        var namePos = rowMin + new Vector2(48.0f, 11.0f);
        drawList.AddText(namePos, ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 1.0f)), component.Name);
        var descriptionX = namePos.X + ImGui.CalcTextSize(component.Name).X + 14.0f;
        drawList.AddText(new Vector2(descriptionX, namePos.Y), ImGui.GetColorU32(new Vector4(0.48f, 0.38f, 0.43f, 0.68f)), component.Description);

        var markerCenter = new Vector2(rowMax.X - 20.0f, rowMin.Y + rowHeight * 0.5f);
        var removeButtonMin = new Vector2(rowMax.X - 132.0f, rowMin.Y + 7.0f);
        if (DrawHeaderRemoveComponentButton($"移除##AuxRemove_{barIndex}_{sectionKey}_{component.Id}", removeButtonMin)) {
            RemoveAuxiliaryComponent(componentOrder, component.Id);
            selected = false;
        }

        if (hasSettings) {
            if (selected) {
                DrawChevronDownGlyph(drawList, markerCenter, 8.0f, ImGui.GetColorU32(new Vector4(0.72f, 0.28f, 0.50f, 0.95f)));
            } else {
                DrawSettingsDotGlyph(drawList, markerCenter, 7.0f, ImGui.GetColorU32(new Vector4(0.56f, 0.38f, 0.46f, hoveredRow ? 0.80f : 0.42f)));
            }
        }

        ImGui.SetCursorScreenPos(rowMin + new Vector2(0.0f, rowHeight + 6.0f));
        if (selected) {
            DrawExpandedComponentSettingsContent(component, rowWidth);
        }

        var cardMax = selected
            ? new Vector2(rowMin.X + rowWidth, ImGui.GetCursorScreenPos().Y - 6.0f)
            : rowMax;
        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(rowMin, cardMax, ImGui.GetColorU32(bgColor), 7.0f);
        drawList.AddRect(rowMin, cardMax, ImGui.GetColorU32(borderColor), 7.0f);
        drawList.ChannelsMerge();
        ImGui.PopID();
    }

    private void DrawAuxiliaryBarDraggedComponentPreview(IReadOnlyList<TaskBarComponentDefinition> activeComponents, string dragPrefix = "aux:") {
        if (string.IsNullOrWhiteSpace(this.draggingTaskBarComponentId) || !this.draggingTaskBarComponentId.StartsWith(dragPrefix, StringComparison.OrdinalIgnoreCase) || !ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
            return;
        }

        var componentId = this.draggingTaskBarComponentId[dragPrefix.Length..];
        var component = activeComponents.FirstOrDefault(item => item.Id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(component.Id)) {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var rowWidth = Math.Max(260.0f, ImGui.GetContentRegionAvail().X - 8.0f);
        const float rowHeight = 40.0f;
        var mousePos = ImGui.GetMousePos();
        var previewMin = new Vector2(ImGui.GetCursorScreenPos().X, mousePos.Y - rowHeight * 0.5f);
        var previewMax = previewMin + new Vector2(rowWidth, rowHeight);

        drawList.AddRectFilled(previewMin + new Vector2(0.0f, 2.0f), previewMax + new Vector2(0.0f, 2.0f), ImGui.GetColorU32(new Vector4(0.34f, 0.18f, 0.28f, 0.18f)), 7.0f);
        drawList.AddRectFilled(previewMin, previewMax, ImGui.GetColorU32(new Vector4(1.0f, 0.93f, 0.97f, 0.92f)), 7.0f);
        drawList.AddRect(previewMin, previewMax, ImGui.GetColorU32(new Vector4(0.62f, 0.34f, 0.72f, 0.88f)), 7.0f, (ImDrawFlags)0, 1.4f);
        DrawTaskBarComponentIcon(drawList, component.Id, previewMin + new Vector2(25.0f, rowHeight * 0.5f), new Vector4(0.56f, 0.32f, 0.44f, 0.95f));
        drawList.AddText(previewMin + new Vector2(48.0f, 11.0f), ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 0.98f)), component.Name);
    }

    private void DrawAuxiliaryBarAddComponentPopup(AuxiliaryBarDefinition bar, int barIndex, List<string> componentOrder, string popupId, string title) {
        if (!ImGui.BeginPopup(popupId)) {
            return;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12.0f, 10.0f));
        ImGui.TextDisabled(title);
        ImGui.Dummy(new Vector2(1.0f, 4.0f));
        var listHeight = Math.Min(440.0f, Math.Max(220.0f, ImGui.GetIO().DisplaySize.Y - ImGui.GetCursorScreenPos().Y - 48.0f));
        ImGui.BeginChild($"##{popupId}_ComponentList", new Vector2(310.0f, listHeight), false);
        foreach (var component in TaskBarComponentDefinitions) {
            if (!Configuration.IsRepeatableComponentId(component.Id) && AuxiliaryBarContainsComponent(bar, component.Id)) {
                continue;
            }

            if (DrawAddComponentCard(component)) {
                componentOrder.Add(CreateComponentInstanceId(component.Id));
                var normalized = Configuration.NormalizeAuxiliaryComponentOrder(componentOrder);
                componentOrder.Clear();
                componentOrder.AddRange(normalized);
                this.saveConfig();
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.EndChild();

        if (TaskBarComponentDefinitions.Where(component => !Configuration.IsRepeatableComponentId(component.Id)).All(component => AuxiliaryBarContainsComponent(bar, component.Id))) {
            ImGui.TextDisabled("所有组件都已添加。可先移除某个组件后再添加。 ");
        }

        ImGui.PopStyleVar();
        ImGui.EndPopup();
    }

    private void MoveAuxiliaryBarComponentRelativeTo(List<string> componentOrder, string componentId, string targetComponentId, bool insertAfter) {
        if (componentId.Equals(targetComponentId, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var normalized = Configuration.NormalizeAuxiliaryComponentOrder(componentOrder);
        componentOrder.Clear();
        componentOrder.AddRange(normalized);
        var index = componentOrder.FindIndex(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        var targetIndex = componentOrder.FindIndex(id => id.Equals(targetComponentId, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || targetIndex < 0) {
            return;
        }

        var item = componentOrder[index];
        componentOrder.RemoveAt(index);
        if (index < targetIndex) {
            targetIndex--;
        }

        var insertIndex = insertAfter ? targetIndex + 1 : targetIndex;
        componentOrder.Insert(Math.Clamp(insertIndex, 0, componentOrder.Count), item);
        this.saveConfig();
    }

    private static bool AuxiliaryBarContainsComponent(AuxiliaryBarDefinition bar, string componentId) {
        return bar.ComponentOrder.Any(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase))
               || bar.SectionStartComponentOrder.Any(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase))
               || bar.SectionCenterComponentOrder.Any(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase))
               || bar.SectionEndComponentOrder.Any(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetAuxiliaryDragComponentId(string dragId, int? barIndex = null, string? sectionKey = null) {
        var prefix = barIndex.HasValue
            ? string.IsNullOrWhiteSpace(sectionKey) ? $"aux:{barIndex.Value}:" : $"aux:{barIndex.Value}:{sectionKey}:"
            : "aux:";
        return dragId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? dragId[prefix.Length..] : string.Empty;
    }

    private void DrawTaskBarComponentsPage() {
        DrawTargetInfoSubsection("主栏组件");

        var selectedOrder = GetSelectedTaskBarComponentOrder();
        if (selectedOrder is null || !selectedOrder.Any(id => id.Equals(this.selectedTaskBarComponentSettingsId, StringComparison.OrdinalIgnoreCase))) {
            ClearSelectedTaskBarComponentSettings();
        }

        ImGui.BeginChild("TaskBarComponentList", new Vector2(0.0f, 0.0f), false);

        if (this.config.TaskBarStretchToEdges) {
            DrawTaskBarSectionComponents("左侧", this.config.TaskBarLeftComponentOrder, "left");
            ImGui.Spacing();
            DrawTaskBarSectionComponents("中间", this.config.TaskBarCenterComponentOrder, "center");
            ImGui.Spacing();
            DrawTaskBarSectionComponents("右侧", this.config.TaskBarRightComponentOrder, "right");
            ImGui.EndChild();
            return;
        }

        var activeComponents = GetActiveTaskBarComponentDefinitions(this.config.TaskBarComponentOrder, true);
        if (activeComponents.Count == 0) {
            ImGui.TextDisabled("还没有添加组件。点击添加组件开始配置主栏。");
        }

        for (var index = 0; index < activeComponents.Count; index++) {
            DrawTaskBarComponentRow(activeComponents[index], this.config.TaskBarComponentOrder, "adaptive");
        }

        DrawTaskBarDraggedComponentPreview(activeComponents, "task:adaptive:");

        ImGui.Spacing();
        if (ImGui.Button("添加组件")) {
            ImGui.OpenPopup("AllHud_TaskBar_AddComponent");
        }

        DrawTaskBarAddComponentPopup();
        ImGui.EndChild();
    }

    private void DrawTaskBarSectionComponents(string label, List<string> componentOrder, string sectionKey) {
        componentOrder = NormalizeTaskBarSectionOrderReference(componentOrder, sectionKey);
        DrawTargetInfoSubsection(label);
        var activeComponents = GetActiveTaskBarComponentDefinitions(componentOrder, false);
        if (activeComponents.Count == 0) {
            ImGui.TextDisabled("还没有添加组件。");
        }

        foreach (var component in activeComponents) {
            DrawTaskBarComponentRow(component, componentOrder, sectionKey);
        }

        DrawTaskBarDraggedComponentPreview(activeComponents, $"task:{sectionKey}:");

        ImGui.Spacing();
        var popupId = $"AllHud_TaskBar_AddComponent_{sectionKey}";
        if (ImGui.Button($"添加组件##{sectionKey}")) {
            ImGui.OpenPopup(popupId);
        }

        DrawTaskBarAddComponentPopup(popupId, componentOrder, $"添加到{label}");
    }

    private List<string> NormalizeTaskBarSectionOrderReference(List<string> componentOrder, string sectionKey) {
        var normalized = Configuration.NormalizeTaskBarSectionComponentOrder(componentOrder);
        switch (sectionKey) {
            case "left":
                this.config.TaskBarLeftComponentOrder = normalized;
                return this.config.TaskBarLeftComponentOrder;
            case "right":
                this.config.TaskBarRightComponentOrder = normalized;
                return this.config.TaskBarRightComponentOrder;
            default:
                this.config.TaskBarCenterComponentOrder = normalized;
                return this.config.TaskBarCenterComponentOrder;
        }
    }

    private TaskBarComponentDefinition GetComponentDefinition(string componentId) {
        var baseId = Configuration.GetComponentBaseId(componentId);
        if (!TaskBarComponentDefinitionLookup.TryGetValue(baseId, out var component)) {
            return default;
        }
        if (Configuration.IsPluginShortcutComponentId(componentId) && !componentId.Equals(Configuration.TaskBarComponentPluginShortcut, StringComparison.OrdinalIgnoreCase)) {
            return component with { Id = componentId, Description = GetPluginShortcutDescription(componentId) };
        }

        if (Configuration.IsCustomShortcutComponentId(componentId) && !componentId.Equals(Configuration.TaskBarComponentCustomShortcut, StringComparison.OrdinalIgnoreCase)) {
            var shortcut = GetCustomShortcut(componentId);
            var name = string.IsNullOrWhiteSpace(shortcut.Name) ? "快捷方式" : shortcut.Name.Trim();
            var commandCount = GetCustomShortcutCommandLines(shortcut).Count;
            var commandStatus = commandCount == 0 ? "未设置命令" : $"{commandCount} 条命令";
            return component with { Id = componentId, Description = $"{name} · {commandStatus}" };
        }

        if (Configuration.IsQuickMenuComponentId(componentId) && !componentId.Equals(Configuration.TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase)) {
            var menu = GetQuickMenu(componentId);
            var name = string.IsNullOrWhiteSpace(menu.Name) ? "快捷菜单" : menu.Name.Trim();
            return component with { Id = componentId, Description = $"{name} · {menu.ComponentOrder.Count} 项" };
        }

        return component;
    }

    private static bool IsPlacedComponentId(string componentId) {
        return !componentId.Equals(Configuration.TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase);
    }

    private void DrawTaskBarComponentRow(TaskBarComponentDefinition component, List<string> componentOrder, string scopeKey) {
        ImGui.PushID($"TaskBarComponent_{scopeKey}_{component.Id}");
        var drawList = ImGui.GetWindowDrawList();
        var rowMin = ImGui.GetCursorScreenPos();
        var rowWidth = Math.Max(260.0f, ImGui.GetContentRegionAvail().X - 8.0f);
        const float rowHeight = 40.0f;
        var rowMax = rowMin + new Vector2(rowWidth, rowHeight);
        var hoveredRow = ImGui.IsMouseHoveringRect(rowMin, rowMax);
        var dragId = $"task:{scopeKey}:{component.Id}";
        var draggingThis = this.draggingTaskBarComponentId.Equals(dragId, StringComparison.OrdinalIgnoreCase);
        var hasSettings = ComponentHasSettings(component.Id);
        var selected = hasSettings
                       && this.selectedTaskBarComponentSettingsId.Equals(component.Id, StringComparison.OrdinalIgnoreCase)
                       && this.selectedTaskBarComponentSettingsScope.Equals(scopeKey, StringComparison.OrdinalIgnoreCase);

        var bgColor = draggingThis
            ? new Vector4(0.98f, 0.92f, 0.98f, 0.72f)
            : selected
                ? new Vector4(1.0f, 0.925f, 0.955f, 0.96f)
                : hoveredRow
                    ? new Vector4(1.0f, 0.955f, 0.970f, 0.94f)
                    : new Vector4(1.0f, 0.975f, 0.980f, 0.82f);
        var borderColor = draggingThis
            ? new Vector4(0.66f, 0.36f, 0.78f, 0.72f)
            : selected
                ? new Vector4(0.86f, 0.34f, 0.58f, 0.78f)
                : new Vector4(0.92f, 0.68f, 0.76f, 0.46f);

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(rowMin);
        var rowPressed = ImGui.InvisibleButton("row_drag_area", new Vector2(Math.Max(48.0f, rowWidth - 146.0f), rowHeight));
        var dragAreaHovered = ImGui.IsItemHovered();
        var dragAreaActive = ImGui.IsItemActive();
        if (dragAreaActive && ImGui.IsMouseDragging(ImGuiMouseButton.Left)) {
            this.draggingTaskBarComponentId = dragId;
        }

        if (hasSettings && rowPressed && !draggingThis) {
            ToggleSelectedTaskBarComponentSettings(component.Id, scopeKey);
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && draggingThis) {
            this.draggingTaskBarComponentId = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(this.draggingTaskBarComponentId)
            && !draggingThis
            && hoveredRow
            && ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
            var insertAfter = ImGui.GetMousePos().Y > rowMin.Y + rowHeight * 0.5f;
            var indicatorY = insertAfter ? rowMax.Y - 1.0f : rowMin.Y + 1.0f;
            drawList.AddLine(new Vector2(rowMin.X + 10.0f, indicatorY), new Vector2(rowMax.X - 10.0f, indicatorY), ImGui.GetColorU32(new Vector4(0.65f, 0.34f, 0.76f, 0.82f)), 2.0f);
            var draggedComponentId = GetTaskBarDragComponentId(this.draggingTaskBarComponentId, scopeKey);
            MoveTaskBarComponentRelativeTo(componentOrder, draggedComponentId, component.Id, insertAfter);
        }

        var handleColor = dragAreaHovered || draggingThis
            ? new Vector4(0.56f, 0.32f, 0.44f, 0.95f)
            : new Vector4(0.50f, 0.36f, 0.42f, 0.70f);
        DrawTaskBarComponentIcon(drawList, component.Id, rowMin + new Vector2(25.0f, rowHeight * 0.5f), handleColor);

        var namePos = rowMin + new Vector2(48.0f, 11.0f);
        drawList.AddText(namePos, ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 1.0f)), component.Name);
        var descriptionX = namePos.X + ImGui.CalcTextSize(component.Name).X + 14.0f;
        drawList.AddText(new Vector2(descriptionX, namePos.Y), ImGui.GetColorU32(new Vector4(0.48f, 0.38f, 0.43f, 0.68f)), component.Description);

        var markerCenter = new Vector2(rowMax.X - 20.0f, rowMin.Y + rowHeight * 0.5f);
        var removeButtonMin = new Vector2(rowMax.X - 132.0f, rowMin.Y + 7.0f);
        if (DrawHeaderRemoveComponentButton($"移除##TaskRemove_{scopeKey}_{component.Id}", removeButtonMin)) {
            RemoveTaskBarComponent(componentOrder, component.Id);
            selected = false;
        }

        if (hasSettings) {
            if (selected) {
                DrawChevronDownGlyph(drawList, markerCenter, 8.0f, ImGui.GetColorU32(new Vector4(0.72f, 0.28f, 0.50f, 0.95f)));
            } else {
                DrawSettingsDotGlyph(drawList, markerCenter, 7.0f, ImGui.GetColorU32(new Vector4(0.56f, 0.38f, 0.46f, hoveredRow ? 0.80f : 0.42f)));
            }
        }

        ImGui.SetCursorScreenPos(rowMin + new Vector2(0.0f, rowHeight + 6.0f));
        if (selected) {
            DrawExpandedComponentSettingsContent(component, rowWidth);
        }

        var cardMax = selected
            ? new Vector2(rowMin.X + rowWidth, ImGui.GetCursorScreenPos().Y - 6.0f)
            : rowMax;
        drawList.ChannelsSetCurrent(0);
        drawList.AddRectFilled(rowMin, cardMax, ImGui.GetColorU32(bgColor), 7.0f);
        drawList.AddRect(rowMin, cardMax, ImGui.GetColorU32(borderColor), 7.0f);
        drawList.ChannelsMerge();
        ImGui.PopID();
    }

    private void DrawTaskBarDraggedComponentPreview(IReadOnlyList<TaskBarComponentDefinition> activeComponents, string dragPrefix = "") {
        if (string.IsNullOrWhiteSpace(this.draggingTaskBarComponentId) || !ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
            return;
        }

        var draggedComponentId = string.IsNullOrWhiteSpace(dragPrefix)
            ? this.draggingTaskBarComponentId
            : this.draggingTaskBarComponentId.StartsWith(dragPrefix, StringComparison.OrdinalIgnoreCase)
                ? this.draggingTaskBarComponentId[dragPrefix.Length..]
                : string.Empty;
        if (string.IsNullOrWhiteSpace(draggedComponentId)) {
            return;
        }

        var component = activeComponents.FirstOrDefault(item => item.Id.Equals(draggedComponentId, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(component.Id)) {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var rowWidth = Math.Max(260.0f, ImGui.GetContentRegionAvail().X - 8.0f);
        const float rowHeight = 40.0f;
        var mousePos = ImGui.GetMousePos();
        var previewMin = new Vector2(ImGui.GetCursorScreenPos().X, mousePos.Y - rowHeight * 0.5f);
        var previewMax = previewMin + new Vector2(rowWidth, rowHeight);

        drawList.AddRectFilled(previewMin + new Vector2(0.0f, 2.0f), previewMax + new Vector2(0.0f, 2.0f), ImGui.GetColorU32(new Vector4(0.34f, 0.18f, 0.28f, 0.18f)), 7.0f);
        drawList.AddRectFilled(previewMin, previewMax, ImGui.GetColorU32(new Vector4(1.0f, 0.93f, 0.97f, 0.92f)), 7.0f);
        drawList.AddRect(previewMin, previewMax, ImGui.GetColorU32(new Vector4(0.62f, 0.34f, 0.72f, 0.88f)), 7.0f, (ImDrawFlags)0, 1.4f);

        DrawTaskBarComponentIcon(drawList, component.Id, previewMin + new Vector2(25.0f, rowHeight * 0.5f), new Vector4(0.56f, 0.32f, 0.44f, 0.95f));

        var namePos = previewMin + new Vector2(48.0f, 11.0f);
        drawList.AddText(namePos, ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 0.98f)), component.Name);
        var descriptionX = namePos.X + ImGui.CalcTextSize(component.Name).X + 14.0f;
        drawList.AddText(new Vector2(descriptionX, namePos.Y), ImGui.GetColorU32(new Vector4(0.48f, 0.38f, 0.43f, 0.70f)), component.Description);
    }

    private static void DrawTaskBarComponentIcon(ImDrawListPtr drawList, string componentId, Vector2 center, Vector4 color) {
        componentId = Configuration.GetComponentBaseId(componentId);
        var iconColor = ImGui.GetColorU32(color);
        var softColor = ImGui.GetColorU32(WithAlpha(color, 0.34f));

        switch (componentId) {
            case Configuration.TaskBarComponentTime:
                drawList.AddCircle(center, 8.0f, iconColor, 24, 1.6f);
                drawList.AddLine(center, center + new Vector2(0.0f, -5.0f), iconColor, 1.6f);
                drawList.AddLine(center, center + new Vector2(4.0f, 2.5f), iconColor, 1.6f);
                break;
            case Configuration.TaskBarComponentFps:
                drawList.AddRect(center - new Vector2(8.0f, 6.0f), center + new Vector2(8.0f, 6.0f), iconColor, 2.0f, (ImDrawFlags)0, 1.5f);
                drawList.AddLine(center + new Vector2(-5.0f, 8.0f), center + new Vector2(5.0f, 8.0f), iconColor, 1.5f);
                drawList.AddLine(center + new Vector2(-2.0f, 6.0f), center + new Vector2(2.0f, 6.0f), iconColor, 1.5f);
                break;
            case Configuration.TaskBarComponentMainMenu:
                DrawMenuPanelGlyph(drawList, center, 20.0f, iconColor);
                break;
            case Configuration.TaskBarComponentVolume:
                DrawSpeakerGlyph(drawList, center, 20.0f, iconColor);
                break;
            case Configuration.TaskBarComponentPluginList:
            case Configuration.TaskBarComponentPluginShortcut:
            case Configuration.TaskBarComponentCustomShortcut:
                if (componentId.Equals(Configuration.TaskBarComponentCustomShortcut, StringComparison.OrdinalIgnoreCase)) {
                    drawList.AddCircle(center, 8.0f, iconColor, 24, 1.5f);
                    drawList.AddLine(center + new Vector2(-5.0f, 0.0f), center + new Vector2(5.0f, 0.0f), iconColor, 1.5f);
                    drawList.AddLine(center + new Vector2(0.0f, -5.0f), center + new Vector2(0.0f, 5.0f), iconColor, 1.5f);
                } else {
                    DrawPluginTileGlyph(drawList, center, 20.0f, iconColor);
                }

                break;
            case Configuration.TaskBarComponentQuickMenu:
                DrawQuickListGlyph(drawList, center, 20.0f, iconColor);
                break;
            case Configuration.TaskBarComponentServerInfo:
                drawList.AddCircle(center, 8.0f, iconColor, 24, 1.5f);
                drawList.AddLine(center + new Vector2(-6.0f, 0.0f), center + new Vector2(6.0f, 0.0f), iconColor, 1.3f);
                drawList.AddLine(center + new Vector2(0.0f, -8.0f), center + new Vector2(0.0f, 8.0f), iconColor, 1.3f);
                drawList.AddBezierCubic(center + new Vector2(-3.0f, -7.0f), center + new Vector2(-6.0f, -2.0f), center + new Vector2(-6.0f, 2.0f), center + new Vector2(-3.0f, 7.0f), iconColor, 1.2f);
                drawList.AddBezierCubic(center + new Vector2(3.0f, -7.0f), center + new Vector2(6.0f, -2.0f), center + new Vector2(6.0f, 2.0f), center + new Vector2(3.0f, 7.0f), iconColor, 1.2f);
                break;
            case Configuration.TaskBarComponentTeleport:
                drawList.AddLine(center + new Vector2(-7.0f, 4.0f), center + new Vector2(4.0f, -7.0f), iconColor, 1.7f);
                drawList.AddLine(center + new Vector2(0.0f, -7.0f), center + new Vector2(6.0f, -7.0f), iconColor, 1.7f);
                drawList.AddLine(center + new Vector2(6.0f, -7.0f), center + new Vector2(6.0f, -1.0f), iconColor, 1.7f);
                drawList.AddCircleFilled(center + new Vector2(-4.5f, 1.5f), 2.2f, iconColor, 10);
                break;
            case Configuration.TaskBarComponentCoordinates:
                drawList.AddCircle(center, 7.5f, iconColor, 24, 1.5f);
                drawList.AddLine(center + new Vector2(-7.0f, 0.0f), center + new Vector2(7.0f, 0.0f), iconColor, 1.2f);
                drawList.AddLine(center + new Vector2(0.0f, -7.0f), center + new Vector2(0.0f, 7.0f), iconColor, 1.2f);
                drawList.AddCircleFilled(center, 1.9f, iconColor, 8);
                break;
            case Configuration.TaskBarComponentGearsetSwitcher:
                drawList.AddCircle(center, 7.0f, iconColor, 24, 1.5f);
                drawList.AddLine(center + new Vector2(-5.0f, 0.0f), center + new Vector2(5.0f, 0.0f), iconColor, 1.3f);
                drawList.AddLine(center + new Vector2(0.0f, -5.0f), center + new Vector2(0.0f, 5.0f), iconColor, 1.3f);
                drawList.AddCircleFilled(center, 2.1f, iconColor, 8);
                break;
            case Configuration.TaskBarComponentCurrency:
                drawList.AddCircle(center, 7.2f, iconColor, 24, 1.5f);
                drawList.AddLine(center + new Vector2(-3.5f, -4.2f), center + new Vector2(3.0f, -4.2f), iconColor, 1.3f);
                drawList.AddLine(center + new Vector2(-3.5f, 0.0f), center + new Vector2(3.5f, 0.0f), iconColor, 1.3f);
                drawList.AddLine(center + new Vector2(-3.5f, 4.2f), center + new Vector2(3.0f, 4.2f), iconColor, 1.3f);
                break;
            case Configuration.TaskBarComponentInventory:
                drawList.AddRect(center + new Vector2(-8.0f, -6.0f), center + new Vector2(8.0f, 7.0f), iconColor, 2.0f, (ImDrawFlags)0, 1.5f);
                drawList.AddLine(center + new Vector2(-5.0f, -6.0f), center + new Vector2(-3.0f, -9.0f), iconColor, 1.4f);
                drawList.AddLine(center + new Vector2(5.0f, -6.0f), center + new Vector2(3.0f, -9.0f), iconColor, 1.4f);
                drawList.AddLine(center + new Vector2(-3.0f, -9.0f), center + new Vector2(3.0f, -9.0f), iconColor, 1.4f);
                break;
            case Configuration.TaskBarComponentSaddlebag:
                drawList.AddRect(center + new Vector2(-8.0f, -5.0f), center + new Vector2(8.0f, 7.0f), iconColor, 3.0f, (ImDrawFlags)0, 1.5f);
                drawList.AddBezierCubic(center + new Vector2(-5.0f, -5.0f), center + new Vector2(-4.0f, -10.0f), center + new Vector2(4.0f, -10.0f), center + new Vector2(5.0f, -5.0f), iconColor, 1.4f);
                drawList.AddCircleFilled(center + new Vector2(-3.0f, 1.0f), 1.0f, iconColor, 8);
                drawList.AddCircleFilled(center + new Vector2(3.0f, 1.0f), 1.0f, iconColor, 8);
                break;
        }
    }

    private static void DrawMenuPanelGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color) {
        // 三条错落圆角短横，中心条略长，保持小尺寸下的清晰菜单语义
        var stroke = Math.Max(1.6f, size * 0.1025f);
        var gap = size * 0.20f;
        var rows = new[] {
            new Vector2(-size * 0.25f, size * 0.20f),
            new Vector2(-size * 0.31f, size * 0.31f),
            new Vector2(-size * 0.20f, size * 0.25f),
        };

        for (var index = 0; index < rows.Length; index++) {
            var y = center.Y + (index - 1) * gap;
            drawList.AddRectFilled(
                new Vector2(center.X + rows[index].X, y - stroke * 0.5f),
                new Vector2(center.X + rows[index].Y, y + stroke * 0.5f),
                color,
                stroke * 0.5f);
        }
    }

    private static void DrawPluginTileGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color) {
        // 轻量 2x2 圆角描边宫格，降低实心块压迫感
        var stroke = Math.Max(1.35f, size * 0.0825f);
        var cell = size * 0.23f;
        var gap = size * 0.14f;
        var rounding = Math.Max(1.1f, cell * 0.30f);
        var start = center - new Vector2(cell + gap * 0.5f, cell + gap * 0.5f);
        for (var row = 0; row < 2; row++) {
            for (var col = 0; col < 2; col++) {
                var min = start + new Vector2(col * (cell + gap), row * (cell + gap));
                drawList.AddRect(min, min + new Vector2(cell, cell), color, rounding, ImDrawFlags.None, stroke);
            }
        }
    }

    private static void DrawQuickListGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color) {
        // 三行胶囊列表，前导点改为圆角小方块以呼应宫格图标
        var stroke = Math.Max(1.6f, size * 0.10f);
        var rowGap = size * 0.24f;
        var bullet = Math.Max(2.2f, size * 0.12f);
        var bulletX = center.X - size * 0.31f;
        var lineStartX = center.X - size * 0.11f;
        var rowEnds = new[] { size * 0.27f, size * 0.34f, size * 0.22f };
        var top = center.Y - rowGap;

        for (var index = 0; index < 3; index++) {
            var y = top + index * rowGap;
            drawList.AddRectFilled(
                new Vector2(bulletX - bullet * 0.5f, y - bullet * 0.5f),
                new Vector2(bulletX + bullet * 0.5f, y + bullet * 0.5f),
                color,
                Math.Max(1.0f, bullet * 0.35f));
            drawList.AddRectFilled(
                new Vector2(lineStartX, y - stroke * 0.5f),
                new Vector2(center.X + rowEnds[index], y + stroke * 0.5f),
                color,
                stroke * 0.5f);
        }
    }

    private static void DrawSpeakerGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color) {
        // 圆角音箱 + 斜切喇叭口，与任务栏音量图标保持一致
        var stroke = Math.Max(1.5f, size * 0.0925f);
        var boxLeft = -size * 0.36f;
        var boxRight = -size * 0.18f;
        var boxH = size * 0.16f;
        var hornInnerH = size * 0.21f;
        var hornX = size * 0.08f;
        var hornH = size * 0.34f;
        var arcCenter = center + new Vector2(size * 0.07f, 0.0f);

        drawList.AddRectFilled(center + new Vector2(boxLeft, -boxH), center + new Vector2(boxRight, boxH), color, Math.Max(1.0f, size * 0.0725f));

        Span<Vector2> horn = stackalloc Vector2[4];
        horn[0] = center + new Vector2(boxRight - size * 0.015f, -hornInnerH);
        horn[1] = center + new Vector2(hornX, -hornH);
        horn[2] = center + new Vector2(hornX, hornH);
        horn[3] = center + new Vector2(boxRight - size * 0.015f, hornInnerH);
        drawList.AddConvexPolyFilled(ref horn[0], horn.Length, color);

        drawList.PathArcTo(arcCenter, size * 0.20f, -0.82f, 0.82f, 12);
        drawList.PathStroke(color, ImDrawFlags.None, stroke);
        drawList.PathArcTo(arcCenter, size * 0.34f, -0.75f, 0.75f, 14);
        drawList.PathStroke(color, ImDrawFlags.None, Math.Max(1.2f, size * 0.0775f));
    }

    private static void DrawChevronGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color) {
        // 朝左的扁平 chevron «
        var stroke = 2.0f;
        var x = size * 0.32f;
        var y = size * 0.42f;
        var tip = new Vector2(center.X - x, center.Y);
        drawList.AddLine(new Vector2(center.X + x, center.Y - y), tip, color, stroke);
        drawList.AddLine(tip, new Vector2(center.X + x, center.Y + y), color, stroke);
        drawList.AddCircleFilled(tip, stroke * 0.5f, color, 8);
    }

    private static void DrawChevronDownGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color) {
        var stroke = 2.0f;
        var x = size * 0.42f;
        var y = size * 0.30f;
        var tip = new Vector2(center.X, center.Y + y);
        drawList.AddLine(new Vector2(center.X - x, center.Y - y), tip, color, stroke);
        drawList.AddLine(tip, new Vector2(center.X + x, center.Y - y), color, stroke);
        drawList.AddCircleFilled(tip, stroke * 0.5f, color, 8);
    }

    private static void DrawSettingsDotGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color) {
        // 扁平齿轮点：环 + 中心实心点
        drawList.AddCircle(center, size * 0.62f, color, 16, 1.8f);
        drawList.AddCircleFilled(center, size * 0.24f, color, 12);
    }

    private void DrawTaskBarTimeSettings() {
        DrawCompactCheckbox("本地时间", "TaskBarShowLocalTime_ComponentPanel", this.config.TaskBarShowLocalTime, showLocal => {
            this.config.TaskBarShowLocalTime = showLocal || !this.config.TaskBarShowEorzeaTime;
        });
        if (GetExpandedSettingsWidth() >= 260.0f) {
            ImGui.SameLine(0.0f, 12.0f);
        }

        DrawCompactCheckbox("艾欧泽亚时间", "TaskBarShowEorzeaTime_ComponentPanel", this.config.TaskBarShowEorzeaTime, showEt => {
            this.config.TaskBarShowEorzeaTime = showEt || !this.config.TaskBarShowLocalTime;
        });
    }

    private void DrawTaskBarCoordinatesSettings() {
        DrawCompactCheckbox("地区", "TaskBarShowCoordinatesTerritory_ComponentPanel", this.config.TaskBarShowCoordinatesTerritory, showTerritory => {
            this.config.TaskBarShowCoordinatesTerritory = showTerritory || !this.config.TaskBarShowCoordinatesPosition;
        });
        if (GetExpandedSettingsWidth() >= 220.0f) {
            ImGui.SameLine(0.0f, 12.0f);
        }

        DrawCompactCheckbox("坐标", "TaskBarShowCoordinatesPosition_ComponentPanel", this.config.TaskBarShowCoordinatesPosition, showPosition => {
            this.config.TaskBarShowCoordinatesPosition = showPosition || !this.config.TaskBarShowCoordinatesTerritory;
        });
    }

    private void DrawTaskBarCurrencySettings() {
        var settingsWidth = GetExpandedSettingsWidth();
        DrawCompactCheckbox("显示货币名称", "TaskBarCurrencyShowName_ComponentPanel", this.config.TaskBarCurrencyShowName, value => this.config.TaskBarCurrencyShowName = value);

        if (settingsWidth >= 340.0f) {
            ImGui.SameLine(0.0f, 18.0f);
        }

        var selected = CurrencyDisplayOptions.FirstOrDefault(option => option.ItemId == this.config.TaskBarCurrencyItemId);
        var selectedName = selected.ItemId == 0 ? "金币" : selected.Name;
        ImGui.SetNextItemWidth(Math.Min(180.0f, Math.Max(140.0f, settingsWidth)));
        if (!ImGui.BeginCombo("##TaskBarCurrencyItem", selectedName)) {
            return;
        }

        foreach (var option in CurrencyDisplayOptions) {
            var isSelected = option.ItemId == this.config.TaskBarCurrencyItemId;
            if (ImGui.Selectable(option.Name, isSelected)) {
                this.config.TaskBarCurrencyItemId = option.ItemId;
                this.saveConfig();
            }

            if (isSelected) {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private void DrawTaskBarGearsetSettings() {
        DrawCompactCheckbox("职业名", "TaskBarGearsetShowName_ComponentPanel", this.config.TaskBarGearsetShowName, value => this.config.TaskBarGearsetShowName = value);
        ImGui.SameLine(0.0f, 12.0f);

        DrawCompactCheckbox("等级", "TaskBarGearsetShowLevel_ComponentPanel", this.config.TaskBarGearsetShowLevel, value => this.config.TaskBarGearsetShowLevel = value);
        ImGui.SameLine(0.0f, 12.0f);

        DrawCompactCheckbox("套装编号", "TaskBarGearsetShowNumber_ComponentPanel", this.config.TaskBarGearsetShowNumber, value => this.config.TaskBarGearsetShowNumber = value);

        ImGui.SameLine(0.0f, 18.0f);
        DrawCompactCheckbox("切换后关闭列表", "TaskBarGearsetClosePopupOnSwitch_ComponentPanel", this.config.TaskBarGearsetClosePopupOnSwitch, value => this.config.TaskBarGearsetClosePopupOnSwitch = value);
    }

    private void DrawTaskBarAddComponentPopup() {
        DrawTaskBarAddComponentPopup("AllHud_TaskBar_AddComponent", this.config.TaskBarComponentOrder, "添加到主栏");
    }

    private void DrawTaskBarAddComponentPopup(string popupId, List<string> targetOrder, string title) {
        if (!ImGui.BeginPopup(popupId)) {
            return;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12.0f, 10.0f));
        ImGui.TextDisabled(title);
        ImGui.Dummy(new Vector2(1.0f, 4.0f));
        var listHeight = Math.Min(440.0f, Math.Max(220.0f, ImGui.GetIO().DisplaySize.Y - ImGui.GetCursorScreenPos().Y - 48.0f));
        ImGui.BeginChild($"##{popupId}_ComponentList", new Vector2(310.0f, listHeight), false);
        var addedAny = false;
        var hasAvailableComponent = false;
        foreach (var component in TaskBarComponentDefinitions) {
            if (!Configuration.IsRepeatableComponentId(component.Id) && IsTaskBarComponentActiveInOrder(targetOrder, component.Id)) {
                continue;
            }

            hasAvailableComponent = true;
            if (DrawAddComponentCard(component)) {
                AddTaskBarComponent(component.Id, targetOrder);
                ImGui.CloseCurrentPopup();
                addedAny = true;
            }
        }
        ImGui.EndChild();

        if (!hasAvailableComponent && !addedAny) {
            ImGui.TextDisabled("所有组件都已添加。可先移除某个组件后再添加。 ");
        }

        ImGui.PopStyleVar();
        ImGui.EndPopup();
    }

    private void ToggleSelectedTaskBarComponentSettings(string componentId, string scopeKey) {
        if (!ComponentHasSettings(componentId)) {
            ClearSelectedTaskBarComponentSettings();
            return;
        }

        if (this.selectedTaskBarComponentSettingsId.Equals(componentId, StringComparison.OrdinalIgnoreCase)
            && this.selectedTaskBarComponentSettingsScope.Equals(scopeKey, StringComparison.OrdinalIgnoreCase)) {
            ClearSelectedTaskBarComponentSettings();
            return;
        }

        this.selectedTaskBarComponentSettingsId = componentId;
        this.selectedTaskBarComponentSettingsScope = scopeKey;
        ClearSelectedAuxiliaryComponentSettings();
    }

    private void ToggleSelectedAuxiliaryComponentSettings(string componentId, int barIndex, string sectionKey) {
        if (!ComponentHasSettings(componentId)) {
            ClearSelectedAuxiliaryComponentSettings();
            return;
        }

        if (this.selectedAuxiliaryComponentSettingsId.Equals(componentId, StringComparison.OrdinalIgnoreCase)
            && this.selectedAuxiliaryComponentSettingsScope.Equals(sectionKey, StringComparison.OrdinalIgnoreCase)
            && this.selectedAuxiliaryComponentSettingsBarIndex == barIndex) {
            ClearSelectedAuxiliaryComponentSettings();
            return;
        }

        this.selectedAuxiliaryComponentSettingsId = componentId;
        this.selectedAuxiliaryComponentSettingsScope = sectionKey;
        this.selectedAuxiliaryComponentSettingsBarIndex = barIndex;
        ClearSelectedTaskBarComponentSettings();
    }

    private void ClearSelectedTaskBarComponentSettings() {
        this.selectedTaskBarComponentSettingsId = string.Empty;
        this.selectedTaskBarComponentSettingsScope = string.Empty;
    }

    private void ClearSelectedAuxiliaryComponentSettings() {
        this.selectedAuxiliaryComponentSettingsId = string.Empty;
        this.selectedAuxiliaryComponentSettingsScope = string.Empty;
        this.selectedAuxiliaryComponentSettingsBarIndex = -1;
    }

    private List<string>? GetSelectedTaskBarComponentOrder() {
        return this.selectedTaskBarComponentSettingsScope switch {
            "adaptive" => this.config.TaskBarComponentOrder,
            "left" => this.config.TaskBarLeftComponentOrder,
            "center" => this.config.TaskBarCenterComponentOrder,
            "right" => this.config.TaskBarRightComponentOrder,
            _ => null,
        };
    }

    private List<string>? GetSelectedAuxiliaryComponentOrder(AuxiliaryBarDefinition bar, int barIndex) {
        if (this.selectedAuxiliaryComponentSettingsBarIndex != barIndex) {
            return null;
        }

        return this.selectedAuxiliaryComponentSettingsScope switch {
            "main" => bar.ComponentOrder,
            "start" => bar.SectionStartComponentOrder,
            "center" => bar.SectionCenterComponentOrder,
            "end" => bar.SectionEndComponentOrder,
            _ => null,
        };
    }

    private void DrawExpandedComponentSettingsContent(TaskBarComponentDefinition component, float rowWidth) {
        if (!ComponentHasSettings(component.Id)) {
            return;
        }

        var rowStart = ImGui.GetCursorScreenPos();
        const float contentOffsetX = 48.0f;
        var settingsWidth = Math.Max(180.0f, rowWidth - 68.0f);

        ImGui.SetCursorScreenPos(rowStart);
        ImGui.Indent(contentOffsetX);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8.0f, 4.0f));
        var contentMin = rowStart + new Vector2(contentOffsetX, 0.0f);
        ImGui.PushTextWrapPos(contentMin.X + settingsWidth);
        var previousExpandedSettingsWidth = this.expandedSettingsWidth;
        this.expandedSettingsWidth = settingsWidth;

        try {
            DrawComponentSettingsContent(component.Id);
        } finally {
            this.expandedSettingsWidth = previousExpandedSettingsWidth;
        }

        ImGui.PopTextWrapPos();
        var settingsEndY = ImGui.GetCursorScreenPos().Y;

        ImGui.PopStyleVar();
        ImGui.Unindent(contentOffsetX);
        var contentEndY = settingsEndY;
        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, contentEndY + 10.0f));
    }

    private float GetExpandedSettingsWidth(float fallback = 180.0f) {
        return this.expandedSettingsWidth > 0.0f
            ? this.expandedSettingsWidth
            : Math.Max(fallback, ImGui.GetContentRegionAvail().X);
    }

    private static void DrawComponentSettingsDivider(float alpha) {
        DrawFullContentWidthDivider(1.0f, new Vector4(0.90f, 0.58f, 0.70f, alpha), height: 7.0f);
    }

    private static void DrawComponentSettingGroupSpacing() {
        var cursorX = ImGui.GetCursorPosX();
        ImGui.Dummy(new Vector2(1.0f, 2.0f));
        ImGui.SetCursorPosX(cursorX);
    }

    private void DrawCompactCheckbox(string label, string id, bool value, Action<bool> setter) {
        DrawCheckbox(label, id, value, setter);
    }

    private static bool DrawRemoveComponentButton(string label, bool alignRight = true) {
        var text = label.Split("##", StringSplitOptions.None)[0];
        var buttonWidth = Math.Max(118.0f, ImGui.CalcTextSize(text).X + 30.0f);
        var buttonSize = new Vector2(buttonWidth, 28.0f);
        if (alignRight) {
            var avail = ImGui.GetContentRegionAvail().X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0.0f, avail - buttonWidth));
        }

        var min = ImGui.GetCursorScreenPos();
        var max = min + buttonSize;
        ImGui.InvisibleButton(label, buttonSize);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var drawList = ImGui.GetWindowDrawList();
        var fill = active ? new Vector4(0.78f, 0.20f, 0.24f, 0.22f) : hovered ? new Vector4(0.78f, 0.20f, 0.24f, 0.15f) : new Vector4(1.0f, 0.965f, 0.975f, 0.56f);
        var border = new Vector4(0.78f, 0.20f, 0.24f, hovered ? 0.72f : 0.44f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), 7.0f);
        drawList.AddRect(min, max, ImGui.GetColorU32(border), 7.0f, (ImDrawFlags)0, hovered ? 1.4f : 1.0f);
        var textSize = ImGui.CalcTextSize(text);
        drawList.AddText(min + (buttonSize - textSize) * 0.5f, ImGui.GetColorU32(new Vector4(0.66f, 0.16f, 0.20f, 0.96f)), text);
        return ImGui.IsItemClicked();
    }

    private static bool DrawHeaderRemoveComponentButton(string label, Vector2 min) {
        const float buttonWidth = 86.0f;
        var buttonSize = new Vector2(buttonWidth, 26.0f);
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(label, buttonSize);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var drawList = ImGui.GetWindowDrawList();
        var max = min + buttonSize;
        var fill = active ? new Vector4(0.78f, 0.20f, 0.24f, 0.20f) : hovered ? new Vector4(0.78f, 0.20f, 0.24f, 0.13f) : new Vector4(1.0f, 0.965f, 0.975f, 0.42f);
        var border = new Vector4(0.78f, 0.20f, 0.24f, hovered ? 0.66f : 0.34f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), 7.0f);
        drawList.AddRect(min, max, ImGui.GetColorU32(border), 7.0f);
        var text = label.Split("##", StringSplitOptions.None)[0];
        var textSize = ImGui.CalcTextSize(text);
        drawList.AddText(min + (buttonSize - textSize) * 0.5f, ImGui.GetColorU32(new Vector4(0.66f, 0.16f, 0.20f, 0.90f)), text);
        return ImGui.IsItemClicked();
    }

    private void RemoveSelectedTaskBarComponent(List<string> componentOrder) {
        var componentId = this.selectedTaskBarComponentSettingsId;
        if (string.IsNullOrWhiteSpace(componentId)) {
            return;
        }

        RemoveTaskBarComponent(componentOrder, componentId);
    }

    private void RemoveTaskBarComponent(List<string> componentOrder, string componentId) {
        if (string.IsNullOrWhiteSpace(componentId)) {
            return;
        }

        componentOrder.RemoveAll(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        if (Configuration.IsRepeatableComponentId(componentId)) {
            RemoveRepeatableComponentData(componentId);
        } else {
            SetTaskBarComponentEnabled(componentId, false);
            RemoveTaskBarComponentFromAllOrders(componentId);
        }

        ClearSelectedTaskBarComponentSettings();
        this.saveConfig();
    }

    private void RemoveSelectedAuxiliaryComponent(List<string> componentOrder) {
        var componentId = this.selectedAuxiliaryComponentSettingsId;
        if (string.IsNullOrWhiteSpace(componentId)) {
            return;
        }

        RemoveAuxiliaryComponent(componentOrder, componentId);
    }

    private void RemoveAuxiliaryComponent(List<string> componentOrder, string componentId) {
        if (string.IsNullOrWhiteSpace(componentId)) {
            return;
        }

        componentOrder.RemoveAll(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        RemoveRepeatableComponentData(componentId);
        ClearSelectedAuxiliaryComponentSettings();
        this.saveConfig();
    }

    private static bool ComponentHasSettings(string componentId) {
        return componentId.Equals(Configuration.TaskBarComponentTime, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(Configuration.TaskBarComponentServerInfo, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(Configuration.TaskBarComponentCoordinates, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(Configuration.TaskBarComponentGearsetSwitcher, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(Configuration.TaskBarComponentCurrency, StringComparison.OrdinalIgnoreCase)
               || componentId.Equals(Configuration.TaskBarComponentPluginList, StringComparison.OrdinalIgnoreCase)
               || Configuration.IsPluginShortcutComponentId(componentId)
               || Configuration.IsCustomShortcutComponentId(componentId)
               || Configuration.IsQuickMenuComponentId(componentId);
    }

    private void DrawComponentSettingsPopup(string popupId, string componentId) {
        if (!ImGui.BeginPopup(popupId)) {
            return;
        }

        DrawComponentSettingsContent(componentId);
        ImGui.EndPopup();
    }

    private void DrawComponentSettingsContent(string componentId) {
        if (componentId.Equals(Configuration.TaskBarComponentTime, StringComparison.OrdinalIgnoreCase)) {
            DrawTaskBarTimeSettings();
        } else if (componentId.Equals(Configuration.TaskBarComponentServerInfo, StringComparison.OrdinalIgnoreCase)) {
            DrawTaskBarServerInfoModeSelector();
        } else if (componentId.Equals(Configuration.TaskBarComponentCoordinates, StringComparison.OrdinalIgnoreCase)) {
            DrawTaskBarCoordinatesSettings();
        } else if (componentId.Equals(Configuration.TaskBarComponentGearsetSwitcher, StringComparison.OrdinalIgnoreCase)) {
            DrawTaskBarGearsetSettings();
        } else if (componentId.Equals(Configuration.TaskBarComponentCurrency, StringComparison.OrdinalIgnoreCase)) {
            DrawTaskBarCurrencySettings();
        } else if (componentId.Equals(Configuration.TaskBarComponentPluginList, StringComparison.OrdinalIgnoreCase)) {
            DrawPluginListSettings();
        } else if (Configuration.IsPluginShortcutComponentId(componentId)) {
            DrawPluginShortcutSettings(componentId);
        } else if (Configuration.IsCustomShortcutComponentId(componentId)) {
            DrawCustomShortcutSettings(componentId);
        } else if (Configuration.IsQuickMenuComponentId(componentId)) {
            DrawQuickMenuSettings(componentId);
        }
    }

    private void DrawPluginListSettings() {
        var plugins = GetInstalledPluginsForSelection();
        this.pluginListSelectedInternalNameCache.Clear();
        foreach (var internalName in this.config.PluginListInternalNames) {
            if (!string.IsNullOrWhiteSpace(internalName)) {
                this.pluginListSelectedInternalNameCache.Add(internalName);
            }
        }

        var selectedInternalNames = this.pluginListSelectedInternalNameCache;
        const float blockInset = 18.0f;
        ImGui.Indent(blockInset);
        try {
            var contentWidth = Math.Max(240.0f, GetExpandedSettingsWidth(240.0f) - blockInset - 8.0f);

            if (plugins.Count == 0) {
                ImGui.TextDisabled("没有可选择的插件。");
                return;
            }

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1.0f, 0.978f, 0.984f, 0.74f));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.90f, 0.58f, 0.70f, 0.30f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16.0f, 10.0f));
            var gridHeight = Math.Clamp(plugins.Count <= 8 ? 112.0f : 178.0f, 112.0f, 220.0f);
            ImGui.BeginChild("##PluginListMultiSelect", new Vector2(contentWidth, gridHeight), true);

            var itemGap = 9.0f;
            var innerWidth = Math.Max(160.0f, ImGui.GetContentRegionAvail().X - 6.0f);
            var columnCount = Math.Clamp((int)Math.Floor((innerWidth + itemGap) / 210.0f), 1, 4);
            var tileWidth = Math.Max(160.0f, (innerWidth - itemGap * (columnCount - 1)) / columnCount);
            for (var index = 0; index < plugins.Count; index++) {
                var plugin = plugins[index];
                if (index > 0 && index % columnCount != 0) {
                    ImGui.SameLine(0.0f, itemGap);
                }

                DrawPluginListToggleTile(plugin, tileWidth, selectedInternalNames);
            }

            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(2);
        } finally {
            ImGui.Unindent(blockInset);
        }
    }

    private void DrawPluginListToggleTile(Dalamud.Plugin.IExposedPlugin plugin, float width, HashSet<string> selectedInternalNames) {
        ImGui.PushID($"PluginListToggle_{plugin.InternalName}");
        var selected = selectedInternalNames.Contains(plugin.InternalName);
        var tileSize = new Vector2(width, 32.0f);
        var tileMin = ImGui.GetCursorScreenPos();
        var tileMax = tileMin + tileSize;
        ImGui.InvisibleButton("##PluginTile", tileSize);
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            if (selected) {
                this.config.PluginListInternalNames.RemoveAll(name => name.Equals(plugin.InternalName, StringComparison.OrdinalIgnoreCase));
                selectedInternalNames.Remove(plugin.InternalName);
            } else if (!this.config.PluginListInternalNames.Contains(plugin.InternalName, StringComparer.OrdinalIgnoreCase)) {
                this.config.PluginListInternalNames.Add(plugin.InternalName.Trim());
                selectedInternalNames.Add(plugin.InternalName);
            }

            selected = !selected;
            this.saveConfig();
        }

        var drawList = ImGui.GetWindowDrawList();
        var accent = new Vector4(0.74f, 0.32f, 0.54f, 1.0f);
        var fill = selected
            ? new Vector4(1.0f, 0.915f, 0.950f, hovered ? 0.96f : 0.84f)
            : hovered
                ? new Vector4(1.0f, 0.950f, 0.965f, 0.90f)
                : new Vector4(1.0f, 0.970f, 0.978f, 0.62f);
        var border = selected ? WithAlpha(accent, 0.70f) : WithAlpha(accent, hovered ? 0.42f : 0.20f);
        drawList.AddRectFilled(tileMin, tileMax, ImGui.GetColorU32(fill), 7.0f);
        drawList.AddRect(tileMin, tileMax, ImGui.GetColorU32(border), 7.0f, ImDrawFlags.None, selected ? 1.25f : 1.0f);

        const float innerPaddingX = 14.0f;
        var boxMin = tileMin + new Vector2(innerPaddingX, 8.0f);
        var boxMax = boxMin + new Vector2(16.0f, 16.0f);
        drawList.AddRect(boxMin, boxMax, ImGui.GetColorU32(WithAlpha(accent, selected ? 0.78f : 0.38f)), 4.0f, ImDrawFlags.None, 1.4f);
        if (selected) {
            drawList.AddLine(boxMin + new Vector2(3.5f, 8.0f), boxMin + new Vector2(7.0f, 12.0f), ImGui.GetColorU32(accent), 2.0f);
            drawList.AddLine(boxMin + new Vector2(7.0f, 12.0f), boxMin + new Vector2(13.0f, 4.0f), ImGui.GetColorU32(accent), 2.0f);
        }

        var textX = innerPaddingX + 24.0f;
        var textMaxWidth = Math.Max(40.0f, width - textX - 10.0f);
        var name = GetPluginListTileText(plugin.Name, textMaxWidth);

        drawList.AddText(tileMin + new Vector2(textX, 8.0f), ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 0.96f)), name);
        if (hovered) {
            DrawStyledTooltip(() => {
                ImGui.TextUnformatted(plugin.Name);
                if (!string.IsNullOrWhiteSpace(plugin.InternalName)) {
                    ImGui.Separator();
                    ImGui.TextUnformatted(plugin.InternalName);
                }
            });
        }

        ImGui.PopID();
    }

    private List<Dalamud.Plugin.IExposedPlugin> GetInstalledPluginsForSelection() {
        var now = Environment.TickCount64;
        if (this.installedPluginSelectionCacheUpdatedAtMs != long.MinValue
            && now - this.installedPluginSelectionCacheUpdatedAtMs < InstalledPluginSelectionCacheTtlMs) {
            return this.installedPluginSelectionCache;
        }

        this.installedPluginSelectionCacheUpdatedAtMs = now;
        this.installedPluginSelectionCache.Clear();
        this.installedPluginSelectionByInternalName.Clear();
        var pluginsByInternalName = new Dictionary<string, Dalamud.Plugin.IExposedPlugin>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in this.pluginInterface.InstalledPlugins) {
            if (!plugin.IsLoaded
                || (!plugin.HasMainUi && !plugin.HasConfigUi)
                || string.IsNullOrWhiteSpace(plugin.InternalName)) {
                continue;
            }

            if (!pluginsByInternalName.TryGetValue(plugin.InternalName, out var existingPlugin)
                || IsPreferredPluginCandidate(plugin, existingPlugin)) {
                pluginsByInternalName[plugin.InternalName] = plugin;
            }
        }

        this.installedPluginSelectionCache.AddRange(pluginsByInternalName.Values
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
        foreach (var plugin in this.installedPluginSelectionCache) {
            this.installedPluginSelectionByInternalName[plugin.InternalName] = plugin;
        }

        return this.installedPluginSelectionCache;
    }

    private string GetPluginListTileText(string name, float maxWidth) {
        var widthBucket = MathF.Round(maxWidth / 8.0f) * 8.0f;
        if (Math.Abs(this.pluginListTileTextCacheWidth - widthBucket) > 0.1f) {
            this.pluginListTileTextCache.Clear();
            this.pluginListTileTextCacheWidth = widthBucket;
        }

        if (this.pluginListTileTextCache.TryGetValue(name, out var cachedName)) {
            return cachedName;
        }

        var trimmedName = TrimTextToWidth(name, maxWidth);
        this.pluginListTileTextCache[name] = trimmedName;
        return trimmedName;
    }

    private static bool IsPreferredPluginCandidate(Dalamud.Plugin.IExposedPlugin candidate, Dalamud.Plugin.IExposedPlugin current) {
        var candidateScore = GetPluginCandidateScore(candidate);
        var currentScore = GetPluginCandidateScore(current);
        if (candidateScore != currentScore) {
            return candidateScore > currentScore;
        }

        return string.Compare(candidate.Name, current.Name, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static int GetPluginCandidateScore(Dalamud.Plugin.IExposedPlugin plugin) {
        var score = 0;
        if (plugin.IsLoaded) {
            score += 100;
        }

        if (plugin.IsDev) {
            score += 50;
        }

        if (plugin.HasMainUi) {
            score += 10;
        }

        if (plugin.HasConfigUi) {
            score += 5;
        }

        return score;
    }

    private void DrawPluginShortcutSettings(string componentId) {
        var plugins = GetInstalledPluginsForSelection();
        if (plugins.Count == 0) {
            ImGui.TextDisabled("没有可选择的插件。");
            return;
        }

        var selectedInternalName = GetPluginShortcutInternalName(componentId);
        var selectedName = plugins.FirstOrDefault(plugin => plugin.InternalName.Equals(selectedInternalName, StringComparison.OrdinalIgnoreCase))?.Name ?? "选择插件";
        DrawComponentSettingGroupSpacing();
        ImGui.SetNextItemWidth(Math.Min(320.0f, GetExpandedSettingsWidth(180.0f)));
        if (!ImGui.BeginCombo($"##PluginShortcutPlugin_{componentId}", selectedName)) {
            return;
        }

        foreach (var plugin in plugins) {
            var selected = plugin.InternalName.Equals(selectedInternalName, StringComparison.OrdinalIgnoreCase);
            if (ImGui.Selectable(plugin.Name, selected)) {
                SetPluginShortcutInternalName(componentId, plugin.InternalName);
                this.saveConfig();
            }

            if (selected) {
                ImGui.SetItemDefaultFocus();
            }
        }

        ImGui.EndCombo();
    }

    private string GetPluginShortcutInternalName(string componentId) {
        return this.config.PluginShortcutInternalNames.TryGetValue(componentId, out var internalName)
            ? internalName
            : this.config.TaskBarPluginShortcutInternalName;
    }

    private void SetPluginShortcutInternalName(string componentId, string internalName) {
        this.config.PluginShortcutInternalNames[componentId] = internalName;
        if (componentId.Equals(Configuration.TaskBarComponentPluginShortcut, StringComparison.OrdinalIgnoreCase)) {
            this.config.TaskBarPluginShortcutInternalName = internalName;
        }
    }

    private string GetPluginShortcutDescription(string componentId) {
        var internalName = GetPluginShortcutInternalName(componentId);
        if (string.IsNullOrWhiteSpace(internalName)) {
            return "未选择插件";
        }

        GetInstalledPluginsForSelection();
        this.installedPluginSelectionByInternalName.TryGetValue(internalName, out var plugin);
        return plugin?.Name ?? $"{internalName}（未安装）";
    }

    private CustomShortcutDefinition GetCustomShortcut(string componentId) {
        if (!this.config.CustomShortcuts.TryGetValue(componentId, out var shortcut) || shortcut is null) {
            shortcut = new CustomShortcutDefinition();
            this.config.CustomShortcuts[componentId] = shortcut;
        }

        return shortcut;
    }

    private QuickMenuDefinition GetQuickMenu(string componentId) {
        if (!this.config.QuickMenus.TryGetValue(componentId, out var menu) || menu is null) {
            menu = new QuickMenuDefinition();
            this.config.QuickMenus[componentId] = menu;
        }

        menu.ComponentOrder = menu.ComponentOrder
            .Where(id => !Configuration.IsQuickMenuComponentId(id))
            .ToList();
        return menu;
    }

    private void DrawQuickMenuSettings(string componentId) {
        var menu = GetQuickMenu(componentId);
        var availableWidth = GetExpandedSettingsWidth(260.0f);
        var contentWidth = Math.Min(520.0f, Math.Max(180.0f, availableWidth - 8.0f));

        DrawEditableTextButton("点击改名", string.IsNullOrWhiteSpace(menu.Name) ? "快捷菜单" : menu.Name.Trim(), $"QuickMenuName_{componentId}", 92.0f, value => menu.Name = value.Trim());
        SameLineOrWrap(108.0f);
        DrawQuickMenuAddItemButton(componentId, menu, 92.0f);
        SameLineOrWrap(192.0f);
        DrawIconPickerButton("更换图标", $"QuickMenuIcon_{componentId}", menu.IconId, value => menu.IconId = value, "使用默认图标", new Vector2(184.0f, 34.0f));

        ImGui.Dummy(new Vector2(1.0f, 8.0f));
        DrawQuickMenuItemPreview(componentId, menu, contentWidth);
    }

    private void DrawQuickMenuAddItemButton(string componentId, QuickMenuDefinition menu, float buttonWidth) {
        var popupId = $"QuickMenuAdd_{componentId}";
        if (ImGui.Button($"添加功能##{componentId}", new Vector2(buttonWidth, 26.0f))) {
            ImGui.OpenPopup(popupId);
        }

        if (!ImGui.BeginPopup(popupId)) {
            return;
        }

        ImGui.TextDisabled("选择要放进菜单的功能");
        ImGui.Separator();
        foreach (var component in TaskBarComponentDefinitions.Where(IsQuickMenuAllowedComponent)) {
            if (DrawAddComponentCard(component)) {
                var itemId = CreateComponentInstanceId(component.Id);
                menu.ComponentOrder.Add(itemId);
                this.saveConfig();
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.EndPopup();
    }

    private void DrawQuickMenuItemPreview(string componentId, QuickMenuDefinition menu, float width) {
        ApplyPendingQuickMenuItemRemoval(componentId, menu);
        var itemCount = menu.ComponentOrder.Count;

        DrawCustomShortcutFieldLabel(itemCount == 0 ? "菜单里还没有功能" : "菜单里的功能");
        var previewWidth = Math.Min(420.0f, Math.Max(180.0f, width));
        var rowHeight = ImGui.GetFrameHeight() + 4.0f;
        if (itemCount == 0) {
            ImGui.TextDisabled("点击“添加功能”把常用项目放进弹出菜单。");
            return;
        }

        for (var index = 0; index < menu.ComponentOrder.Count; index++) {
            var itemId = menu.ComponentOrder[index];
            var definition = GetComponentDefinition(itemId);
            if (string.IsNullOrWhiteSpace(definition.Id)) {
                continue;
            }

            if (DrawQuickMenuItemLine(componentId, definition, itemId, index, previewWidth, rowHeight, out var removeRequested)) {
                if (removeRequested) {
                    // 把移除延迟到下一帧再应用，本帧继续把剩余行画完，避免被点行及其下方行闪烁/残影。
                    this.pendingQuickMenuItemRemovals[componentId] = itemId;
                }
            }
        }
    }

    private bool DrawQuickMenuItemLine(string componentId, TaskBarComponentDefinition definition, string itemId, int index, float previewWidth, float rowHeight, out bool removeRequested) {
        removeRequested = false;
        var rowStart = ImGui.GetCursorScreenPos();

        const float numberWidth = 26.0f;
        const float removeButtonWidth = 48.0f;
        const float settingsButtonWidth = 48.0f;
        const float gap = 8.0f;
        var frameHeight = ImGui.GetFrameHeight();
        var hasSettings = ComponentHasSettings(itemId);
        var trailingWidth = removeButtonWidth + gap + (hasSettings ? settingsButtonWidth + gap : 0.0f);
        var nameWidth = Math.Max(90.0f, previewWidth - numberWidth - trailingWidth);

        // 序号
        var numberText = $"{index + 1}.";
        var numberTextSize = ImGui.CalcTextSize(numberText);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(
            new Vector2(rowStart.X + numberWidth - 4.0f - numberTextSize.X, rowStart.Y + (frameHeight - numberTextSize.Y) * 0.5f + 1.5f),
            ImGui.GetColorU32(new Vector4(0.45f, 0.30f, 0.38f, 0.76f)),
            numberText);

        // 名称框（只读外观，与命令行输入框样式一致）
        var nameMin = new Vector2(rowStart.X + numberWidth, rowStart.Y);
        var nameMax = nameMin + new Vector2(nameWidth, frameHeight);
        drawList.AddRectFilled(nameMin, nameMax, ImGui.GetColorU32(new Vector4(1.0f, 0.985f, 0.990f, 0.86f)), 4.0f);
        drawList.AddRect(nameMin, nameMax, ImGui.GetColorU32(new Vector4(0.86f, 0.62f, 0.72f, 0.40f)), 4.0f);
        var nameTextSize = ImGui.CalcTextSize(definition.Name);
        drawList.AddText(
            new Vector2(nameMin.X + 8.0f, nameMin.Y + (frameHeight - nameTextSize.Y) * 0.5f),
            ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 1.0f)),
            definition.Name);

        // 名称框上的悬浮提示
        ImGui.SetCursorScreenPos(nameMin);
        ImGui.InvisibleButton($"##QuickMenuItemName_{componentId}_{itemId}_{index}", new Vector2(nameWidth, frameHeight));
        if (ImGui.IsItemHovered() && !string.IsNullOrWhiteSpace(definition.Description)) {
            DrawStyledTooltip(definition.Description);
        }

        var cursorX = nameMax.X + gap;

        // 设置按钮
        if (hasSettings) {
            ImGui.SetCursorScreenPos(new Vector2(cursorX, rowStart.Y));
            if (DrawQuickMenuItemSettingsButton($"QuickMenuItemSettings_{componentId}_{itemId}_{index}", new Vector2(settingsButtonWidth, frameHeight))) {
                ImGui.OpenPopup($"QuickMenuItemSettings_{itemId}");
            }

            DrawComponentSettingsPopup($"QuickMenuItemSettings_{itemId}", itemId);
            cursorX += settingsButtonWidth + gap;
        }

        // 移除按钮（复用命令行的同款绘制，点击延迟到下一帧应用）
        ImGui.SetCursorScreenPos(new Vector2(cursorX, rowStart.Y));
        if (DrawCommandLineRemoveButton($"QuickMenuItemRemove_{componentId}_{itemId}_{index}", new Vector2(removeButtonWidth, frameHeight))) {
            removeRequested = true;
        }

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));
        return removeRequested;
    }

    private static bool DrawQuickMenuItemSettingsButton(string id, Vector2 size) {
        var min = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton($"##{id}", size);
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var hovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();
        var max = min + size;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(hovered ? new Vector4(1.0f, 0.92f, 0.96f, 0.82f) : new Vector4(1.0f, 0.975f, 0.985f, 0.74f)), 5.0f);
        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.78f, 0.46f, 0.62f, hovered ? 0.72f : 0.48f)), 5.0f);
        const string label = "设置";
        var textSize = ImGui.CalcTextSize(label);
        drawList.AddText(min + (size - textSize) * 0.5f, ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 0.96f)), label);
        return clicked;
    }

    private void ApplyPendingQuickMenuItemRemoval(string componentId, QuickMenuDefinition menu) {
        if (!this.pendingQuickMenuItemRemovals.Remove(componentId, out var itemId)) {
            return;
        }

        var index = menu.ComponentOrder.IndexOf(itemId);
        if (index < 0) {
            return;
        }

        menu.ComponentOrder.RemoveAt(index);
        RemoveRepeatableComponentData(itemId);
        this.saveConfig();
    }


    private static bool IsQuickMenuAllowedComponent(TaskBarComponentDefinition component) {
        return component.Id is Configuration.TaskBarComponentVolume
            or Configuration.TaskBarComponentPluginList
            or Configuration.TaskBarComponentPluginShortcut
            or Configuration.TaskBarComponentCustomShortcut
            or Configuration.TaskBarComponentInventory
            or Configuration.TaskBarComponentSaddlebag;
    }
}

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
    private void DrawCustomShortcutSettings(string componentId) {
        var shortcut = GetCustomShortcut(componentId);
        var availableWidth = GetExpandedSettingsWidth(260.0f);
        DrawCustomShortcutFormSettings(componentId, shortcut, availableWidth);
    }

    private void DrawCustomShortcutFormSettings(string componentId, CustomShortcutDefinition shortcut, float width) {
        var contentWidth = Math.Min(520.0f, Math.Max(180.0f, width - 8.0f));

        DrawEditableTextButton("点击改名", string.IsNullOrWhiteSpace(shortcut.Name) ? "快捷方式" : shortcut.Name.Trim(), $"CustomShortcutName_{componentId}", 92.0f, value => shortcut.Name = value.Trim());
        SameLineOrWrap(108.0f);
        DrawAddCommandLineButton(componentId, shortcut, 92.0f);
        SameLineOrWrap(134.0f);
        DrawDailyRoutinesPresetButton(componentId, shortcut, 118.0f);
        SameLineOrWrap(192.0f);
        DrawIconPickerButton("更换图标", $"CustomShortcutIcon_{componentId}", shortcut.IconId, value => shortcut.IconId = value, "使用名称文字", new Vector2(184.0f, 34.0f));

        ImGui.Dummy(new Vector2(1.0f, 8.0f));
        DrawCustomShortcutCommandPreview(componentId, shortcut, contentWidth);
    }

    private static void SameLineOrWrap(float nextItemWidth, float spacing = 8.0f) {
        if (ImGui.GetContentRegionAvail().X >= nextItemWidth + spacing) {
            ImGui.SameLine(0.0f, spacing);
        } else {
            ImGui.Dummy(new Vector2(1.0f, 6.0f));
        }
    }

    private void DrawDailyRoutinesPresetButton(string componentId, CustomShortcutDefinition shortcut, float buttonWidth) {
        var popupId = $"DailyRoutinesShortcutPreset_{componentId}";
        if (ImGui.Button($"添加 DR 预设##{componentId}", new Vector2(buttonWidth, 26.0f))) {
            ImGui.OpenPopup(popupId);
        }

        if (!ImGui.BeginPopup(popupId)) {
            return;
        }

        ImGui.TextDisabled("Daily Routines 预设");
        ImGui.Separator();
        foreach (var preset in DailyRoutinesCustomShortcutPresets) {
            if (ImGui.Selectable(preset.DisplayName)) {
                ApplyDailyRoutinesPreset(shortcut, preset);
                this.pendingCustomShortcutCommandLineCounts.Remove(componentId);
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.IsItemHovered()) {
                DrawStyledTooltip(() => {
                    ImGui.TextUnformatted(preset.Description);
                    ImGui.Separator();
                    ImGui.TextUnformatted(preset.Command);
                });
            }
        }

        ImGui.EndPopup();
    }

    private void ApplyDailyRoutinesPreset(CustomShortcutDefinition shortcut, CustomShortcutPreset preset) {
        var commands = GetCustomShortcutCommandLines(shortcut);
        if (!commands.Any(command => command.Equals(preset.Command, StringComparison.OrdinalIgnoreCase))) {
            commands.Add(preset.Command);
        }

        shortcut.Name = preset.Name;
        shortcut.IconId = preset.IconId;
        shortcut.Command = string.Join('\n', commands);
        this.saveConfig();
    }

    private void DrawAddCommandLineButton(string componentId, CustomShortcutDefinition shortcut, float buttonWidth) {
        if (!ImGui.Button($"添加命令##{componentId}", new Vector2(buttonWidth, 26.0f))) {
            return;
        }

        this.pendingCustomShortcutCommandLineCounts.TryGetValue(componentId, out var pendingCount);
        this.pendingCustomShortcutCommandLineCounts[componentId] = pendingCount + 1;
    }

    private void DrawCustomShortcutCommandPreview(string componentId, CustomShortcutDefinition shortcut, float width) {
        ApplyPendingCustomShortcutCommandRemoval(componentId, shortcut);
        var commands = GetCustomShortcutCommandLines(shortcut);
        this.pendingCustomShortcutCommandLineCounts.TryGetValue(componentId, out var pendingCount);
        var rowCount = commands.Count + pendingCount;
        var rowUiIds = GetCustomShortcutCommandLineUiIds(componentId, rowCount);

        DrawCustomShortcutFieldLabel(rowCount == 0 ? "未设置命令" : "已添加命令");
        var previewWidth = Math.Min(420.0f, Math.Max(180.0f, width));
        var rowHeight = ImGui.GetFrameHeight() + 4.0f;
        if (rowCount == 0) {
            ImGui.TextDisabled("点击“添加命令”设置点击后执行的指令。");
            return;
        } else {
            for (var index = 0; index < rowCount; index++) {
                var isPendingLine = index >= commands.Count;
                var rowCountChanged = DrawCustomShortcutCommandLine(componentId, shortcut, commands, rowUiIds[index], index, isPendingLine, previewWidth, rowHeight, out var lineRemoveRequested);
                if (lineRemoveRequested) {
                    // 把移除延迟到下一帧再应用，本帧继续把剩余行画完，避免被点行及其下方行闪烁/残影。
                    this.pendingCustomShortcutCommandRemovals[componentId] = new PendingCustomShortcutCommandRemoval(rowUiIds[index], isPendingLine);
                    continue;
                }

                if (rowCountChanged) {
                    break;
                }
            }
        }
    }

    private void ApplyPendingCustomShortcutCommandRemoval(string componentId, CustomShortcutDefinition shortcut) {
        if (!this.pendingCustomShortcutCommandRemovals.Remove(componentId, out var removal)) {
            return;
        }

        if (!this.customShortcutCommandLineUiIds.TryGetValue(componentId, out var rowUiIds)) {
            return;
        }

        var index = rowUiIds.IndexOf(removal.RowUiId);
        if (index < 0) {
            return;
        }

        var commands = GetCustomShortcutCommandLines(shortcut);
        RemoveCustomShortcutCommandLine(componentId, shortcut, commands, index, removal.IsPendingLine);
    }

    private List<string> GetCustomShortcutCommandLineUiIds(string componentId, int rowCount) {
        if (!this.customShortcutCommandLineUiIds.TryGetValue(componentId, out var rowUiIds)) {
            rowUiIds = [];
            this.customShortcutCommandLineUiIds[componentId] = rowUiIds;
        }

        while (rowUiIds.Count < rowCount) {
            rowUiIds.Add(Guid.NewGuid().ToString("N"));
        }

        while (rowUiIds.Count > rowCount) {
            rowUiIds.RemoveAt(rowUiIds.Count - 1);
        }

        return rowUiIds;
    }

    private bool DrawCustomShortcutCommandLine(string componentId, CustomShortcutDefinition shortcut, List<string> commands, string rowUiId, int index, bool isPendingLine, float previewWidth, float rowHeight, out bool removeRequested) {
        removeRequested = false;
        var rowStart = ImGui.GetCursorScreenPos();
        if (!isPendingLine && (index < 0 || index >= commands.Count)) {
            return true;
        }

        var currentValue = isPendingLine ? string.Empty : commands[index];
        const float numberWidth = 26.0f;
        const float removeButtonWidth = 48.0f;
        const float inputButtonGap = 12.0f;
        var commandWidth = Math.Max(90.0f, previewWidth - numberWidth - removeButtonWidth - inputButtonGap);

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X + numberWidth, rowStart.Y));
        var commandBuffer = CreateUtf8Buffer(currentValue, 256);
        ImGui.SetNextItemWidth(commandWidth);
        var rowCountChanged = false;
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1.0f, 0.985f, 0.990f, 0.86f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1.0f, 0.985f, 0.990f, 0.86f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1.0f, 0.985f, 0.990f, 0.86f));
        if (ImGui.InputText($"##CustomShortcutCommandLine_{rowUiId}", commandBuffer, ImGuiInputTextFlags.None)) {
            rowCountChanged = UpdateCustomShortcutCommandLine(componentId, shortcut, commands, index, isPendingLine, ReadUtf8Buffer(commandBuffer));
        }
        ImGui.PopStyleColor(3);

        var inputMin = ImGui.GetItemRectMin();
        var inputMax = ImGui.GetItemRectMax();
        var inputHeight = inputMax.Y - inputMin.Y;
        var numberText = $"{index + 1}.";
        var numberTextSize = ImGui.CalcTextSize(numberText);
        var drawList = ImGui.GetWindowDrawList();
        var numberPos = new Vector2(
            inputMin.X - 4.0f - numberTextSize.X,
            inputMin.Y + (inputHeight - numberTextSize.Y) * 0.5f + 1.5f);
        drawList.AddText(numberPos, ImGui.GetColorU32(new Vector4(0.45f, 0.30f, 0.38f, 0.76f)), numberText);

        ImGui.SetCursorScreenPos(new Vector2(inputMax.X + inputButtonGap, inputMin.Y));
        if (DrawCommandLineRemoveButton($"CustomShortcutCommandRemove_{rowUiId}", new Vector2(removeButtonWidth, inputHeight))) {
            removeRequested = true;
        }

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));
        return rowCountChanged;
    }

    private static bool DrawCommandLineRemoveButton(string id, Vector2 size) {
        var min = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton($"##{id}", size);
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var drawList = ImGui.GetWindowDrawList();
        var max = min + size;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1.0f, 0.965f, 0.975f, 0.74f)), 5.0f);
        drawList.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.78f, 0.20f, 0.24f, 0.52f)), 5.0f);
        const string label = "移除";
        var textSize = ImGui.CalcTextSize(label);
        drawList.AddText(min + (size - textSize) * 0.5f, ImGui.GetColorU32(new Vector4(0.55f, 0.14f, 0.20f, 0.96f)), label);
        return clicked;
    }

    private void RemoveCustomShortcutCommandLine(string componentId, CustomShortcutDefinition shortcut, List<string> commands, int index, bool isPendingLine) {
        if (isPendingLine) {
            DecrementPendingCustomShortcutCommandLine(componentId);
            RemoveCustomShortcutCommandLineUiId(componentId, index);
            return;
        }

        if (index < 0 || index >= commands.Count) {
            return;
        }

        commands.RemoveAt(index);
        RemoveCustomShortcutCommandLineUiId(componentId, index);
        shortcut.Command = string.Join('\n', commands);
        this.saveConfig();
    }

    private void RemoveCustomShortcutCommandLineUiId(string componentId, int index) {
        if (!this.customShortcutCommandLineUiIds.TryGetValue(componentId, out var rowUiIds)) {
            return;
        }

        if (index < 0 || index >= rowUiIds.Count) {
            return;
        }

        rowUiIds.RemoveAt(index);
    }

    private bool UpdateCustomShortcutCommandLine(string componentId, CustomShortcutDefinition shortcut, List<string> commands, int index, bool isPendingLine, string value) {
        if (isPendingLine) {
            commands.Add(value);
            DecrementPendingCustomShortcutCommandLine(componentId);
        } else {
            commands[index] = value;
        }

        shortcut.Command = string.Join('\n', commands.Where(command => !string.IsNullOrWhiteSpace(command)));
        this.saveConfig();
        return isPendingLine;
    }

    private void DecrementPendingCustomShortcutCommandLine(string componentId) {
        if (!this.pendingCustomShortcutCommandLineCounts.TryGetValue(componentId, out var pendingCount)) {
            return;
        }

        if (pendingCount <= 1) {
            this.pendingCustomShortcutCommandLineCounts.Remove(componentId);
            return;
        }

        this.pendingCustomShortcutCommandLineCounts[componentId] = pendingCount - 1;
    }

    private static List<string> GetCustomShortcutCommandLines(CustomShortcutDefinition shortcut) {
        var commandText = shortcut.Command ?? string.Empty;
        return commandText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .ToList();
    }

    private void DrawEditableTextButton(string actionLabel, string value, string id, float buttonWidth, Action<string> setter) {
        var preview = string.IsNullOrWhiteSpace(value) ? "未命名" : value.Trim();
        var popupId = $"EditText_{id}";
        if (ImGui.Button($"{actionLabel}##{id}_Button", new Vector2(buttonWidth, 26.0f))) {
            ImGui.OpenPopup(popupId);
        }

        ImGui.SetNextWindowSize(new Vector2(320.0f, 0.0f), ImGuiCond.Appearing);
        if (!ImGui.BeginPopup(popupId)) {
            return;
        }

        var buffer = CreateUtf8Buffer(value, 96);
        ImGui.SetNextItemWidth(260.0f);
        if (ImGui.InputText($"##{id}_Input", buffer, ImGuiInputTextFlags.None)) {
            setter(ReadUtf8Buffer(buffer));
            this.saveConfig();
        }

        ImGui.SameLine(0.0f, 6.0f);
        if (ImGui.Button($"确定##{id}_Confirm", new Vector2(52.0f, 24.0f))) {
            setter(ReadUtf8Buffer(buffer));
            this.saveConfig();
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static void DrawCustomShortcutFieldLabel(string label, string hint = "") {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.45f, 0.30f, 0.38f, 0.94f));
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        if (!string.IsNullOrWhiteSpace(hint)) {
            ImGui.SameLine();
            ImGui.TextDisabled(hint);
        }
    }

    private void DrawIconPickerButton(string actionLabel, string id, uint iconId, Action<uint> setter, string emptyText = "默认图标", Vector2? size = null) {
        var buttonSize = size ?? new Vector2(184.0f, 42.0f);
        var buttonMin = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton($"##{id}_Button", buttonSize);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var drawList = ImGui.GetWindowDrawList();
        var buttonMax = buttonMin + buttonSize;
        drawList.AddRectFilled(buttonMin, buttonMax, ImGui.GetColorU32(active ? new Vector4(0.98f, 0.80f, 0.90f, 0.78f) : hovered ? new Vector4(1.0f, 0.89f, 0.95f, 0.82f) : new Vector4(1.0f, 0.965f, 0.975f, 0.78f)), 7.0f);
        drawList.AddRect(buttonMin, buttonMax, ImGui.GetColorU32(new Vector4(0.86f, 0.48f, 0.64f, hovered ? 0.74f : 0.42f)), 7.0f);
        var iconSize = Math.Min(34.0f, Math.Max(22.0f, buttonSize.Y - 4.0f));
        var iconMin = buttonMin + new Vector2(4.0f, (buttonSize.Y - iconSize) * 0.5f);
        DrawActionIconImage(iconId, iconMin, iconMin + new Vector2(iconSize, iconSize), true);
        var text = iconId == 0 ? $"{actionLabel}：{emptyText}" : $"{actionLabel}：{iconId}";
        var textSize = ImGui.CalcTextSize(text);
        drawList.AddText(buttonMin + new Vector2(iconSize + 12.0f, (buttonSize.Y - textSize.Y) * 0.5f), ImGui.GetColorU32(new Vector4(0.42f, 0.25f, 0.34f, 1.0f)), text);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            ImGui.OpenPopup($"IconPicker_{id}");
        }

        DrawIconPickerPopup($"IconPicker_{id}", iconId, setter);
    }

    private void DrawIconPickerPopup(string popupId, uint iconId, Action<uint> setter) {
        ImGui.SetNextWindowSize(new Vector2(340.0f, 0.0f), ImGuiCond.Appearing);
        if (!ImGui.BeginPopup(popupId)) {
            return;
        }

        var previewSize = new Vector2(38.0f, 38.0f);
        var previewMin = ImGui.GetCursorScreenPos();
        var previewMax = previewMin + previewSize;
        ImGui.TextDisabled("当前图标");
        ImGui.InvisibleButton($"##{popupId}_Preview", previewSize);
        DrawActionIconImage(iconId, previewMin, previewMax, true);

        var iconInput = iconId > int.MaxValue ? int.MaxValue : (int)iconId;
        var buttonWidth = 58.0f;
        ImGui.SameLine(0.0f, 8.0f);

        ImGui.SetNextItemWidth(132.0f);
        if (ImGui.InputInt($"##{popupId}_IconId", ref iconInput)) {
            setter((uint)Math.Max(0, iconInput));
            this.saveConfig();
        }

        ImGui.SameLine(0.0f, 6.0f);
        if (ImGui.Button($"清除##{popupId}", new Vector2(buttonWidth, 24.0f))) {
            setter(0);
            this.saveConfig();
        }

        ImGui.Spacing();
        ImGui.TextDisabled("常用图标");
        const int columns = 5;
        var iconButtonSize = new Vector2(54.0f, 54.0f);
        for (var index = 0; index < CommonCustomShortcutIconIds.Length; index++) {
            var commonIconId = CommonCustomShortcutIconIds[index];
            ImGui.PushID($"CustomShortcutIcon_{commonIconId}");
            var min = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton("##Icon", iconButtonSize);
            var hovered = ImGui.IsItemHovered();
            var selected = iconId == commonIconId;
            var max = min + iconButtonSize;
            var drawList = ImGui.GetWindowDrawList();
            if (selected || hovered) {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(selected ? new Vector4(0.96f, 0.74f, 0.86f, 0.62f) : new Vector4(0.96f, 0.80f, 0.90f, 0.34f)), 8.0f);
                drawList.AddRect(min, max, ImGui.GetColorU32(selected ? new Vector4(0.72f, 0.34f, 0.58f, 0.92f) : new Vector4(0.72f, 0.44f, 0.62f, 0.58f)), 8.0f);
            }

            DrawActionIconImage(commonIconId, min + new Vector2(6.0f), max - new Vector2(6.0f), true);
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                setter(commonIconId);
                this.saveConfig();
                ImGui.CloseCurrentPopup();
            }

            if (hovered) {
                DrawStyledTooltip(commonIconId.ToString());
            }

            ImGui.PopID();
            if ((index + 1) % columns != 0) {
                ImGui.SameLine();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("更多图标可直接填写图标 ID。");

        ImGui.EndPopup();
    }

    private List<TaskBarComponentDefinition> GetActiveTaskBarComponentDefinitions(List<string> componentOrder, bool normalizeAdaptiveOrder) {
        if (normalizeAdaptiveOrder) {
            this.config.TaskBarComponentOrder = Configuration.NormalizeTaskBarComponentOrder(componentOrder);
            componentOrder = this.config.TaskBarComponentOrder;
        } else {
            componentOrder = Configuration.NormalizeTaskBarSectionComponentOrder(componentOrder);
        }

        var result = new List<TaskBarComponentDefinition>(componentOrder.Count);
        foreach (var componentId in componentOrder) {
            if (!IsPlacedComponentId(componentId)) {
                continue;
            }

            var component = GetComponentDefinition(componentId);
            if (string.IsNullOrWhiteSpace(component.Id) || !IsTaskBarComponentEnabled(component)) {
                continue;
            }

            result.Add(component);
        }

        return result;
    }

    private List<TaskBarComponentDefinition> GetOrderedTaskBarComponentDefinitions() {
        this.config.TaskBarComponentOrder = Configuration.NormalizeTaskBarComponentOrder(this.config.TaskBarComponentOrder);
        return GetOrderedTaskBarComponentDefinitions(this.config.TaskBarComponentOrder);
    }

    private List<TaskBarComponentDefinition> GetOrderedTaskBarSectionComponentDefinitions(List<string> componentOrder) {
        return GetOrderedTaskBarComponentDefinitions(Configuration.NormalizeTaskBarSectionComponentOrder(componentOrder));
    }

    private List<TaskBarComponentDefinition> GetOrderedTaskBarComponentDefinitions(IReadOnlyList<string> componentOrder) {
        var result = new List<TaskBarComponentDefinition>(componentOrder.Count);
        foreach (var componentId in componentOrder) {
            if (!IsPlacedComponentId(componentId)) {
                continue;
            }

            var component = GetComponentDefinition(componentId);
            if (!string.IsNullOrWhiteSpace(component.Id)) {
                result.Add(component);
            }
        }

        return result;
    }

    private static bool ContainsTaskBarComponent(IEnumerable<string> componentOrder, string componentId) {
        return componentOrder.Any(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsTaskBarComponentActiveInOrder(IEnumerable<string> componentOrder, string componentId) {
        return ContainsTaskBarComponent(componentOrder, componentId) && IsTaskBarComponentEnabled(componentId);
    }

    private bool IsTaskBarComponentEnabled(TaskBarComponentDefinition component) {
        return component.Id switch {
            Configuration.TaskBarComponentTime => this.config.TaskBarShowLocalTime || this.config.TaskBarShowEorzeaTime,
            Configuration.TaskBarComponentFps => this.config.TaskBarShowFps,
            Configuration.TaskBarComponentVolume => this.config.TaskBarShowVolume,
            Configuration.TaskBarComponentMainMenu => this.config.TaskBarShowMainMenu,
            Configuration.TaskBarComponentPluginList => this.config.TaskBarShowPluginList,
            Configuration.TaskBarComponentPluginShortcut => this.config.TaskBarShowPluginShortcut && !string.IsNullOrWhiteSpace(this.config.TaskBarPluginShortcutInternalName),
            _ when Configuration.IsPluginShortcutComponentId(component.Id) => true,
            _ when Configuration.IsCustomShortcutComponentId(component.Id) => true,
            _ when Configuration.IsQuickMenuComponentId(component.Id) => !component.Id.Equals(Configuration.TaskBarComponentQuickMenu, StringComparison.OrdinalIgnoreCase),
            Configuration.TaskBarComponentServerInfo => this.config.TaskBarShowServerInfoBar,
            Configuration.TaskBarComponentInventory => this.config.TaskBarShowInventory,
            Configuration.TaskBarComponentSaddlebag => this.config.TaskBarShowSaddlebag,
            Configuration.TaskBarComponentTeleport => this.config.TaskBarShowTeleport,
            Configuration.TaskBarComponentCoordinates => this.config.TaskBarShowCoordinates,
            Configuration.TaskBarComponentGearsetSwitcher => this.config.TaskBarShowGearsetSwitcher,
            Configuration.TaskBarComponentCurrency => this.config.TaskBarShowCurrency,
            _ => false,
        };
    }

    private void SetTaskBarComponentEnabled(string componentId, bool enabled) {
        switch (componentId) {
            case Configuration.TaskBarComponentTime:
                this.config.TaskBarShowLocalTime = enabled;
                this.config.TaskBarShowEorzeaTime = enabled;
                break;
            case Configuration.TaskBarComponentLocalTime:
                this.config.TaskBarShowLocalTime = enabled;
                break;
            case Configuration.TaskBarComponentEorzeaTime:
                this.config.TaskBarShowEorzeaTime = enabled;
                break;
            case Configuration.TaskBarComponentFps:
                this.config.TaskBarShowFps = enabled;
                break;
            case Configuration.TaskBarComponentVolume:
                this.config.TaskBarShowVolume = enabled;
                break;
            case Configuration.TaskBarComponentMainMenu:
                this.config.TaskBarShowMainMenu = enabled;
                break;
            case Configuration.TaskBarComponentPluginList:
                this.config.TaskBarShowPluginList = enabled;
                break;
            case Configuration.TaskBarComponentPluginShortcut:
                this.config.TaskBarShowPluginShortcut = enabled;
                break;
            case Configuration.TaskBarComponentServerInfo:
                this.config.TaskBarShowServerInfoBar = enabled;
                break;
            case Configuration.TaskBarComponentInventory:
                this.config.TaskBarShowInventory = enabled;
                break;
            case Configuration.TaskBarComponentSaddlebag:
                this.config.TaskBarShowSaddlebag = enabled;
                break;
            case Configuration.TaskBarComponentTeleport:
                this.config.TaskBarShowTeleport = enabled;
                break;
            case Configuration.TaskBarComponentCoordinates:
                this.config.TaskBarShowCoordinates = enabled;
                break;
            case Configuration.TaskBarComponentGearsetSwitcher:
                this.config.TaskBarShowGearsetSwitcher = enabled;
                break;
            case Configuration.TaskBarComponentCurrency:
                this.config.TaskBarShowCurrency = enabled;
                break;
        }
    }

    private void AddTaskBarComponent(string componentId, List<string>? targetOrder = null) {
        componentId = CreateComponentInstanceId(componentId);
        if (Configuration.IsQuickMenuComponentId(componentId)) {
            _ = GetQuickMenu(componentId);
        }

        targetOrder ??= this.config.TaskBarComponentOrder;
        var normalized = targetOrder == this.config.TaskBarComponentOrder
            ? Configuration.NormalizeTaskBarComponentOrder(targetOrder)
            : Configuration.NormalizeTaskBarSectionComponentOrder(targetOrder);
        targetOrder.Clear();
        targetOrder.AddRange(normalized);
        if (!Configuration.IsRepeatableComponentId(componentId)) {
            RemoveTaskBarComponentFromAllOrders(componentId);
        }

        var insertIndex = targetOrder.FindLastIndex(IsTaskBarComponentEnabled);
        if (insertIndex < 0) {
            targetOrder.Insert(0, componentId);
        } else {
            targetOrder.Insert(insertIndex + 1, componentId);
        }

        SetTaskBarComponentEnabled(componentId, true);
        this.saveConfig();
    }

    private static string CreateComponentInstanceId(string componentId) {
        if (Configuration.IsPluginShortcutComponentId(componentId)) {
            return Configuration.CreatePluginShortcutComponentId();
        }

        if (Configuration.IsCustomShortcutComponentId(componentId)) {
            return Configuration.CreateCustomShortcutComponentId();
        }

        return Configuration.IsQuickMenuComponentId(componentId) ? Configuration.CreateQuickMenuComponentId() : componentId;
    }

    private void RemoveRepeatableComponentData(string componentId) {
        this.config.PluginShortcutInternalNames.Remove(componentId);
        this.config.CustomShortcuts.Remove(componentId);
        this.config.QuickMenus.Remove(componentId);
    }

    private void MoveTaskBarComponentRelativeTo(List<string> componentOrder, string componentId, string targetComponentId, bool insertAfter) {
        if (componentId.Equals(targetComponentId, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var normalized = componentOrder == this.config.TaskBarComponentOrder
            ? Configuration.NormalizeTaskBarComponentOrder(componentOrder)
            : Configuration.NormalizeTaskBarSectionComponentOrder(componentOrder);
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

    private static string GetTaskBarDragComponentId(string dragId, string scopeKey) {
        var prefix = $"task:{scopeKey}:";
        return dragId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? dragId[prefix.Length..] : string.Empty;
    }

    private void RemoveTaskBarComponentFromAllOrders(string componentId) {
        this.config.TaskBarComponentOrder.RemoveAll(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        this.config.TaskBarLeftComponentOrder.RemoveAll(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        this.config.TaskBarCenterComponentOrder.RemoveAll(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
        this.config.TaskBarRightComponentOrder.RemoveAll(id => id.Equals(componentId, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsTaskBarComponentEnabled(string componentId) {
        var component = GetComponentDefinition(componentId);
        return !string.IsNullOrWhiteSpace(component.Id) && IsTaskBarComponentEnabled(component);
    }

    private void SetTaskBarStretchToEdges(bool stretchToEdges) {
        if (this.config.TaskBarStretchToEdges == stretchToEdges) {
            return;
        }

        if (stretchToEdges) {
            this.config.TaskBarCenterComponentOrder = Configuration.NormalizeTaskBarSectionComponentOrder(this.config.TaskBarComponentOrder);
            this.config.TaskBarLeftComponentOrder.Clear();
            this.config.TaskBarRightComponentOrder.Clear();
        } else {
            this.config.TaskBarComponentOrder = Configuration.NormalizeTaskBarComponentOrder(this.config.TaskBarLeftComponentOrder
                .Concat(this.config.TaskBarCenterComponentOrder)
                .Concat(this.config.TaskBarRightComponentOrder));
            this.config.TaskBarLeftComponentOrder.Clear();
            this.config.TaskBarCenterComponentOrder.Clear();
            this.config.TaskBarRightComponentOrder.Clear();
        }

        this.config.TaskBarStretchToEdges = stretchToEdges;
        ClearSelectedTaskBarComponentSettings();
        this.saveConfig();
    }

    private void DrawTaskBarPluginDockPage() {
        DrawTargetInfoSubsection("插件入口隐藏");
        ImGui.TextWrapped("隐藏插件悬浮窗。点击添加按钮后，把鼠标移到目标悬浮窗上，左键确认，右键或 Esc 取消。");
        ImGui.Spacing();

        DrawHiddenImGuiWindowPicker();
        DrawHiddenImGuiWindowList();
    }

    private void DrawHiddenImGuiWindowPicker() {
        if (!this.isPickingHiddenImGuiWindow) {
            if (ImGui.Button("添加隐藏窗口")) {
                this.isPickingHiddenImGuiWindow = true;
                this.hiddenImGuiWindowPickerStatus = "选择中：移动到目标悬浮窗，左键确认，右键或 Esc 取消。";
            }

            if (!string.IsNullOrWhiteSpace(this.hiddenImGuiWindowPickerStatus)) {
                ImGui.SameLine();
                ImGui.TextDisabled(this.hiddenImGuiWindowPickerStatus);
            }

            return;
        }

        ImGui.TextDisabled(this.hiddenImGuiWindowPickerStatus);
    }

    private void DrawHiddenImGuiWindowList() {
        if (this.config.HiddenImGuiWindowNames.Count == 0) {
            return;
        }

        ImGui.Spacing();
        DrawTargetInfoSubsection("已添加的隐藏项");
        for (var index = 0; index < this.config.HiddenImGuiWindowNames.Count; index++) {
            var windowName = this.config.HiddenImGuiWindowNames[index];
            ImGui.PushID($"HiddenImGuiWindow{index}{windowName}");
            ImGui.TextUnformatted(windowName);
            ImGui.SameLine();
            if (ImGui.SmallButton("移除")) {
                this.config.HiddenImGuiWindowNames.RemoveAt(index);
                this.saveConfig();
                ImGui.PopID();
                break;
            }

            ImGui.PopID();
        }
    }

    private void AddHiddenImGuiWindow(string windowName) {
        var key = NormalizeImGuiWindowNameKey(windowName);
        if (key.Length == 0) {
            this.hiddenImGuiWindowPickerStatus = "未能识别窗口名。";
            return;
        }

        if (this.config.HiddenImGuiWindowNames.Any(existing => NormalizeImGuiWindowNameKey(existing).Equals(key, StringComparison.OrdinalIgnoreCase))) {
            this.hiddenImGuiWindowPickerStatus = $"已在列表中: {windowName}";
            return;
        }

        this.config.HiddenImGuiWindowNames.Add(windowName.Trim());
        this.saveConfig();
        this.hiddenImGuiWindowPickerStatus = $"已添加: {windowName}。可继续选择，右键或 Esc 结束。";
    }

    private static unsafe PickedImGuiWindow? TryGetHoveredImGuiWindow() {
        try {
            var ctxPtr = ImGuiNative.GetCurrentContext();
            if (ctxPtr == null) {
                return null;
            }

            var ctx = new ImGuiContextPtr(ctxPtr);
            if (ctx.IsNull) {
                return null;
            }

            var mousePos = ImGui.GetMousePos();
            PickedImGuiWindow? bestWindow = null;
            var bestArea = float.MaxValue;
            ref var windows = ref ctx.Windows;
            for (var i = 0; i < windows.Size; i++) {
                var window = windows[i];
                if (window.Handle == null || window.Name == null || window.Hidden) {
                    continue;
                }

                var name = Marshal.PtrToStringUTF8((IntPtr)window.Name)?.Trim() ?? string.Empty;
                var key = NormalizeImGuiWindowNameKey(name);
                if (key.Length == 0 || IsSelfImGuiWindowName(key)) {
                    continue;
                }

                var min = window.Pos;
                var size = window.Size;
                if (size.X <= 1.0f || size.Y <= 1.0f) {
                    continue;
                }

                var max = min + size;
                if (mousePos.X < min.X || mousePos.Y < min.Y || mousePos.X > max.X || mousePos.Y > max.Y) {
                    continue;
                }

                var area = size.X * size.Y;
                if (area >= bestArea) {
                    continue;
                }

                bestArea = area;
                bestWindow = new PickedImGuiWindow(name, key);
            }

            return bestWindow;
        } catch {
            return null;
        }
    }

    private static bool IsSelfImGuiWindowName(string key) {
        return key.Contains("AllHud", StringComparison.OrdinalIgnoreCase)
               || key.Contains("config_content", StringComparison.OrdinalIgnoreCase)
               || key.Contains("config_nav", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeImGuiWindowNameKey(string windowName) {
        var trimmed = (windowName ?? string.Empty).Trim();
        if (trimmed.Length == 0) {
            return string.Empty;
        }

        var idx = trimmed.IndexOf("###", StringComparison.Ordinal);
        return idx >= 0 ? trimmed[idx..] : trimmed;
    }

    private void DrawTaskBarAdvancedPage() {
        DrawTargetInfoSubsection("高级");
        DrawCheckbox("下载并缓存插件图标", nameof(this.config.TaskBarDownloadPluginIcons), this.config.TaskBarDownloadPluginIcons, value => this.config.TaskBarDownloadPluginIcons = value);
        DrawCheckbox("隐藏原生服务器信息栏", nameof(this.config.HideNativeServerInfoBar), this.config.HideNativeServerInfoBar, value => this.config.HideNativeServerInfoBar = value);
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("隐藏游戏顶部的原生服务器信息栏（DtrBar），仅保留自定义任务栏");
        ImGui.PopTextWrapPos();
    }

    private void DrawTaskBarPageTabs() {
        DrawTaskBarPageTab(TaskBarPage.任务栏, "主栏");
        ImGui.SameLine(0.0f, 6.0f);
        DrawTaskBarPageTab(TaskBarPage.辅助栏, "辅助栏");
        ImGui.SameLine(0.0f, 6.0f);
        DrawTaskBarPageTab(TaskBarPage.插件收纳, "插件隐藏");
        ImGui.SameLine(0.0f, 6.0f);
        DrawTaskBarPageTab(TaskBarPage.高级, "高级");
    }

    private void DrawTaskBarPageTab(TaskBarPage page, string label) {
        var selected = this.selectedTaskBarPage == page;
        var accentColor = GetPageAccentColor(ConfigPage.任务栏);
        var textSize = ImGui.CalcTextSize(label);
        var buttonSize = new Vector2(textSize.X + 24.0f, 28.0f);
        var buttonMin = ImGui.GetCursorScreenPos();
        var buttonMax = buttonMin + buttonSize;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton($"task_bar_page_{page}", buttonSize);
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked()) {
            this.selectedTaskBarPage = page;
        }

        var fillColor = selected
            ? WithAlpha(accentColor, 0.78f)
            : hovered
                ? WithAlpha(accentColor, 0.18f)
                : new Vector4(1.0f, 0.970f, 0.965f, 0.76f);
        var borderColor = selected ? WithAlpha(accentColor, 0.92f) : WithAlpha(accentColor, hovered ? 0.48f : 0.26f);
        var textColor = selected ? new Vector4(1.0f, 0.985f, 0.940f, 1.0f) : new Vector4(0.46f, 0.30f, 0.36f, 0.92f);

        drawList.AddRectFilled(buttonMin, buttonMax, ImGui.GetColorU32(fillColor), 999.0f);
        drawList.AddRect(buttonMin, buttonMax, ImGui.GetColorU32(borderColor), 999.0f, (ImDrawFlags)0, selected ? 1.3f : 1.0f);
        drawList.AddText(buttonMin + (buttonSize - textSize) * 0.5f, ImGui.GetColorU32(textColor), label);
    }

    private void DrawTaskBarServerInfoModeSelector() {
        var mode = Math.Clamp(this.config.TaskBarServerInfoBarMode, 0, 1);
        if (mode != this.config.TaskBarServerInfoBarMode) {
            this.config.TaskBarServerInfoBarMode = mode;
        }

        DrawInlineSegmentedSelector("", "task_bar_server_info_mode", mode, value => this.config.TaskBarServerInfoBarMode = Math.Clamp(value, 0, 1), ("独立一行", 0), ("合入主栏", 1));
    }

    private void DrawTaskBarCoordinatesModeSelector() {
        DrawComponentSettingGroupSpacing();
        DrawCheckbox("地区", "TaskBarShowCoordinatesTerritory_ComponentPanel", this.config.TaskBarShowCoordinatesTerritory, showTerritory => {
            this.config.TaskBarShowCoordinatesTerritory = showTerritory || !this.config.TaskBarShowCoordinatesPosition;
        });

        DrawCheckbox("坐标", "TaskBarShowCoordinatesPosition_ComponentPanel", this.config.TaskBarShowCoordinatesPosition, showPosition => {
            this.config.TaskBarShowCoordinatesPosition = showPosition || !this.config.TaskBarShowCoordinatesTerritory;
        });
    }

    private void DrawTaskBarEdgeSelector() {
        var edge = this.config.TaskBarEdge == 1 ? 1 : 0;
        if (this.config.TaskBarEdge != edge) {
            this.config.TaskBarEdge = edge;
        }

        DrawInlineSegmentedSelector("位置", "task_bar_edge", edge, value => this.config.TaskBarEdge = value == 1 ? 1 : 0, ("顶部", 0), ("底部", 1));
    }

    private void DrawAuxiliaryBarPositionSelector(AuxiliaryBarDefinition bar, int index) {
        var position = Math.Clamp(bar.PositionMode, 0, 2);
        if (bar.PositionMode != position) {
            bar.PositionMode = position;
        }

        DrawInlineSegmentedSelector("位置", $"auxiliary_bar_position_{index}", position, value => {
            bar.PositionMode = Math.Clamp(value, 0, 2);
            SyncLegacyAuxiliaryBarSettings();
        }, ("左侧", 0), ("右侧", 1), ("自定义", 2));
    }

    private void DrawTaskBarHorizontalOffsetSlider() {
        var offset = Math.Clamp(this.config.TaskBarHorizontalOffset, 0.0f, 1.0f) * 100.0f;
        if (DrawInlinePercentSlider("水平位置", "TaskBarHorizontalOffset", ref offset)) {
            this.config.TaskBarHorizontalOffset = Math.Clamp(offset / 100.0f, 0.0f, 1.0f);
            this.saveConfig();
        }
    }

    private void DrawAuxiliaryBarVerticalOffsetSlider(AuxiliaryBarDefinition bar, int index) {
        var offset = Math.Clamp(bar.VerticalOffset, 0.0f, 1.0f) * 100.0f;
        if (DrawInlinePercentSlider("垂直位置", $"AuxiliaryBarVerticalOffset{index}", ref offset)) {
            bar.VerticalOffset = Math.Clamp(offset / 100.0f, 0.0f, 1.0f);
            this.saveConfig();
        }
    }

    private void DrawTaskBarOpacitySlider() {
        var opacity = Math.Clamp(this.config.TaskBarOpacity, 0.15f, 1.0f);
        if (DrawInlineOpacitySlider("透明度", "TaskBarOpacity", ref opacity)) {
            this.config.TaskBarOpacity = opacity;
            this.saveConfig();
        }
    }

    private void DrawAuxiliaryBarOpacitySlider(AuxiliaryBarDefinition bar, int index) {
        var opacity = Math.Clamp(bar.Opacity, 0.15f, 1.0f);
        if (DrawInlineOpacitySlider("透明度", $"AuxiliaryBarOpacity{index}", ref opacity)) {
            bar.Opacity = opacity;
            SyncLegacyAuxiliaryBarSettings();
            this.saveConfig();
        }
    }

    private void SyncLegacyAuxiliaryBarSettings() {
        var firstBar = this.config.AuxiliaryBars?.FirstOrDefault();
        if (firstBar is null) {
            this.config.ShowAuxiliaryBar = false;
            this.config.AuxiliaryBarPositionMode = 0;
            this.config.AuxiliaryBarScale = 1.0f;
            this.config.AuxiliaryBarOpacity = 0.72f;
            return;
        }

        this.config.ShowAuxiliaryBar = firstBar.Enabled;
        this.config.AuxiliaryBarPositionMode = Math.Clamp(firstBar.PositionMode, 0, 2);
        this.config.AuxiliaryBarScale = Math.Clamp(firstBar.Scale <= 0.0f ? 1.0f : firstBar.Scale, 0.6f, 2.0f);
        this.config.AuxiliaryBarOpacity = Math.Clamp(firstBar.Opacity, 0.15f, 1.0f);
    }
}

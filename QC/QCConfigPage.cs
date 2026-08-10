using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AllHud;

public sealed class QCConfigPage {
    private readonly QCManager manager;
    private readonly Configuration config;
    private readonly Action saveConfig;
    private string? selectedBarId;
    private string? editingShortcutId;
    private string? newBarName = "新快捷栏";
    private string? newShortcutName = "新快捷方式";
    private string? newConditionSetName = "新条件集";
    private int selectedTab;
    private string? recordingHotkeyShortcutId; // ID of shortcut being recorded for hotkey
    private int recordedVkCode;

    private static readonly string[] ConditionTypeLabels = [
        "无", "战斗中", "非战斗", "副本内", "副本外", "职业ID", "区域ID",
        "骑乘中", "拔武器", "游泳中", "制作中", "采集", "投影台",
        "区域切换中", "排队就绪", "确认就绪", "副本56人", "副本97人",
        "新人频道", "等待进入", "条件集",
    ];

    private static readonly string[] OperatorLabels = ["AND", "OR", "EQUALS", "XOR"];

    public QCConfigPage(QCManager manager, Configuration config, Action saveConfig) {
        this.manager = manager;
        this.config = config;
        this.saveConfig = saveConfig;
    }

    public void Draw() {
        DrawTabs();

        switch (this.selectedTab) {
            case 0: DrawBarList(); break;
            case 1: DrawShortcutList(); break;
            case 2: DrawConditionSetList(); break;
            case 3: DrawImportExport(); break;
        }
    }

    private void DrawTabs() {
        var tabLabels = new[] { "快捷栏", "快捷方式", "条件集", "导入导出" };
        var buttonWidth = 90.0f;
        var buttonHeight = 28.0f;

        for (var i = 0; i < tabLabels.Length; i++) {
            var selected = this.selectedTab == i;
            var min = ImGui.GetCursorScreenPos();
            var max = min + new Vector2(buttonWidth, buttonHeight);

            ImGui.InvisibleButton($"qc_tab_{i}", new Vector2(buttonWidth, buttonHeight));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) this.selectedTab = i;

            var drawList = ImGui.GetWindowDrawList();
            var accentColor = new Vector4(0.94f, 0.50f, 0.70f, 1.0f);
            var fillColor = selected
                ? new Vector4(0.94f, 0.50f, 0.70f, 0.78f)
                : hovered ? new Vector4(0.94f, 0.50f, 0.70f, 0.18f) : new Vector4(1.0f, 0.97f, 0.965f, 0.76f);
            var borderColor = selected ? accentColor : new Vector4(0.94f, 0.50f, 0.70f, hovered ? 0.48f : 0.26f);
            var textColor = selected ? new Vector4(1.0f, 0.985f, 0.94f, 1.0f) : new Vector4(0.46f, 0.30f, 0.36f, 0.92f);
            var textSize = ImGui.CalcTextSize(tabLabels[i]);

            drawList.AddRectFilled(min, max, ImGui.GetColorU32(fillColor), 999.0f);
            drawList.AddRect(min, max, ImGui.GetColorU32(borderColor), 999.0f, ImDrawFlags.None, selected ? 1.3f : 1.0f);
            drawList.AddText(min + (new Vector2(buttonWidth, buttonHeight) - textSize) * 0.5f, ImGui.GetColorU32(textColor), tabLabels[i]);

            if (i < tabLabels.Length - 1) ImGui.SameLine(0.0f, 6.0f);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawBarList() {
        ImGui.TextDisabled("快捷栏管理 — 创建自定义快捷栏，支持热键、条件集、网格布局和圆盘菜单。");

        ImGui.Spacing();
        ImGui.SetNextItemWidth(180.0f);
        ImGui.InputTextWithHint("##qc_new_bar_name", "栏名称", ref this.newBarName, 64);
        ImGui.SameLine();
        if (ImGui.Button("添加快捷栏")) {
            if (!string.IsNullOrWhiteSpace(this.newBarName)) {
                this.manager.AddBar(this.newBarName.Trim());
                this.newBarName = "新快捷栏";
                this.saveConfig();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (this.manager.Bars.Count == 0) {
            ImGui.TextDisabled("暂无快捷栏，请添加。");
            return;
        }

        for (var index = 0; index < this.manager.Bars.Count; index++) {
            var bar = this.manager.Bars[index];
            var isSelected = bar.Id == this.selectedBarId;
            var headerLabel = $"{(bar.Enabled ? "" : "[隐藏] ")}{bar.Name}##qc_bar_{index}";

            if (ImGui.CollapsingHeader(headerLabel, isSelected ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None)) {
                if (ImGui.IsItemClicked()) this.selectedBarId = bar.Id;
                DrawBarEditor(bar, index);
            } else {
                if (ImGui.IsItemClicked()) this.selectedBarId = bar.Id;
            }
        }
    }

    private void DrawBarEditor(QCBarDefinition bar, int index) {
        var changed = false;

        // Enable
        var enabled = bar.Enabled;
        if (ImGui.Checkbox($"启用##qc_bar_enable_{index}", ref enabled)) {
            bar.Enabled = enabled;
            changed = true;
        }

        // Name
        ImGui.SameLine();
        var name = bar.Name;
        ImGui.SetNextItemWidth(160.0f);
        if (ImGui.InputText($"名称##qc_bar_name_{index}", ref name, 64)) {
            bar.Name = name;
            changed = true;
        }

        // Position mode
        var position = bar.PositionMode;
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.Combo($"位置##qc_bar_pos_{index}", ref position, "左侧\0右侧\0自定义\0顶部\0底部\0")) {
            bar.PositionMode = position;
            changed = true;
        }

        // Custom position
        if (bar.PositionMode == 2) {
            var pos = bar.CustomPosition;
            ImGui.SetNextItemWidth(80.0f);
            if (ImGui.InputFloat($"X##qc_bar_posx_{index}", ref pos.X)) { bar.CustomPosition = pos; changed = true; }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(80.0f);
            if (ImGui.InputFloat($"Y##qc_bar_posy_{index}", ref pos.Y)) { bar.CustomPosition = pos; changed = true; }
        }

        // Scale + Opacity
        var scale = bar.Scale;
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.SliderFloat($"缩放##qc_bar_scale_{index}", ref scale, 0.4f, 2.0f)) { bar.Scale = scale; changed = true; }
        ImGui.SameLine();
        var opacity = bar.Opacity;
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.SliderFloat($"透明度##qc_bar_opacity_{index}", ref opacity, 0.15f, 1.0f)) { bar.Opacity = opacity; changed = true; }

        // Font scale
        ImGui.SameLine();
        var fontScale = bar.FontScale;
        ImGui.SetNextItemWidth(80.0f);
        if (ImGui.SliderFloat($"字体缩放##qc_bar_fs_{index}", ref fontScale, 0.5f, 2.0f)) { bar.FontScale = fontScale; changed = true; }

        // Grid layout - Columns
        var columns = bar.Columns;
        ImGui.SetNextItemWidth(60.0f);
        if (ImGui.InputInt($"列数##qc_bar_cols_{index}", ref columns)) {
            bar.Columns = Math.Max(0, columns);
            changed = true;
        }
        ImGui.SameLine();
        var btnWidth = bar.ButtonWidth;
        ImGui.SetNextItemWidth(80.0f);
        if (ImGui.SliderInt($"按钮宽度%##qc_bar_bw_{index}", ref btnWidth, 50, 150)) { bar.ButtonWidth = btnWidth; changed = true; }

        // Spacing
        var spacing = bar.Spacing;
        ImGui.SetNextItemWidth(60.0f);
        if (ImGui.InputFloat($"间距X##qc_bar_spx_{index}", ref spacing.X)) { bar.Spacing = spacing; changed = true; }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(60.0f);
        if (ImGui.InputFloat($"间距Y##qc_bar_spy_{index}", ref spacing.Y)) { bar.Spacing = spacing; changed = true; }

        // Advanced options
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("高级选项");
        ImGui.Spacing();

        // Orientation + Pie menu
        var horizontal = bar.Horizontal;
        if (ImGui.Checkbox($"水平排列##qc_bar_h_{index}", ref horizontal)) {
            bar.Horizontal = horizontal;
            changed = true;
        }

        ImGui.SameLine();
        var isPie = bar.IsPieMenu;
        if (ImGui.Checkbox($"圆盘菜单##qc_bar_pie_{index}", ref isPie)) {
            bar.IsPieMenu = isPie;
            changed = true;
        }

        if (bar.IsPieMenu) {
            var pieRadius = bar.PieRadius;
            ImGui.SetNextItemWidth(120.0f);
            if (ImGui.SliderFloat($"圆盘半径##qc_bar_pie_r_{index}", ref pieRadius, 60.0f, 300.0f)) { bar.PieRadius = pieRadius; changed = true; }
            ImGui.SameLine();
            var hotkey = bar.Hotkey ?? string.Empty;
            ImGui.SetNextItemWidth(120.0f);
            if (ImGui.InputText($"热键##qc_bar_hotkey_{index}", ref hotkey, 32)) {
                bar.Hotkey = string.IsNullOrWhiteSpace(hotkey) ? null : hotkey;
                changed = true;
            }
        }

        // Dock side (QoLBar-style)
        var dockSide = bar.DockSide;
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.Combo($"停靠##qc_bar_dock_{index}", ref dockSide, "顶部\0右侧\0底部\0左侧\0不停靠\0")) {
            bar.DockSide = dockSide;
            changed = true;
        }

        // Alignment (when docked)
        if (bar.DockSide >= 0 && bar.DockSide <= 3) {
            ImGui.SameLine();
            var align = bar.Alignment;
            ImGui.SetNextItemWidth(100.0f);
            if (ImGui.Combo($"对齐##qc_bar_align_{index}", ref align, "左/上\0居中\0右/下\0")) {
                bar.Alignment = align;
                changed = true;
            }
        }

        // Visibility mode
        ImGui.SameLine();
        var visMode = bar.VisibilityMode;
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.Combo($"显示模式##qc_bar_vis_{index}", ref visMode, "滑入\0即时\0始终\0")) {
            bar.VisibilityMode = visMode;
            changed = true;
        }

        // Hint indicator
        if (bar.VisibilityMode == 0) {
            var hint = bar.Hint;
            if (ImGui.Checkbox($"隐藏提示##qc_bar_hint_{index}", ref hint)) {
                bar.Hint = hint;
                changed = true;
            }
            ImGui.SameLine();
            var revealArea = bar.RevealAreaScale;
            ImGui.SetNextItemWidth(80.0f);
            if (ImGui.SliderFloat($"触发区域##qc_bar_reveal_{index}", ref revealArea, 0.5f, 3.0f)) { bar.RevealAreaScale = revealArea; changed = true; }
        }

        // Visibility options
        var clickThrough = bar.ClickThrough;
        if (ImGui.Checkbox($"点击穿透##qc_bar_ct_{index}", ref clickThrough)) {
            bar.ClickThrough = clickThrough;
            changed = true;
        }
        ImGui.SameLine();
        var lockedPos = bar.LockedPosition;
        if (ImGui.Checkbox($"锁定位置##qc_bar_lp_{index}", ref lockedPos)) {
            bar.LockedPosition = lockedPos;
            changed = true;
        }
        ImGui.SameLine();
        var noBg = bar.NoBackground;
        if (ImGui.Checkbox($"无背景##qc_bar_nobg_{index}", ref noBg)) {
            bar.NoBackground = noBg;
            changed = true;
        }
        ImGui.SameLine();
        var hideEmpty = bar.HideWhenEmpty;
        if (ImGui.Checkbox($"空时隐藏##qc_bar_he_{index}", ref hideEmpty)) {
            bar.HideWhenEmpty = hideEmpty;
            changed = true;
        }

        // Condition set
        DrawConditionSetSelector(bar, index, ref changed);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("快捷方式管理");
        ImGui.Spacing();

        // Shortcut list
        DrawShortcutListForBar(bar, index, ref changed);

        // Delete bar
        ImGui.Spacing();
        if (ImGui.Button($"删除快捷栏##qc_bar_del_{index}")) {
            this.manager.RemoveBar(bar.Id);
            this.saveConfig();
            return;
        }

        if (changed) this.saveConfig();
    }

    private void CheckHotkeyRecording(QCShortcutDefinition shortcut, ref bool changed) {
        // Check all keys (1-255) for a newly pressed key
        for (var vk = 1; vk <= 255; vk++) {
            if (ImGui.IsKeyDown((ImGuiKey)vk)) {
                // Wait for key release to record
                continue;
            }
        }

        if (this.recordedVkCode == 0) {
            // Detect the first key press
            for (var vk = 1; vk <= 255; vk++) {
                if (ImGui.IsKeyPressed((ImGuiKey)vk, false)) {
                    // Skip modifier keys
                    if (vk is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5) continue;
                    this.recordedVkCode = vk;
                    break;
                }
            }
        }

        // Check if the recorded key was released
        if (this.recordedVkCode > 0 && !ImGui.IsKeyDown((ImGuiKey)this.recordedVkCode)) {
            shortcut.Hotkey = this.recordedVkCode;
            this.recordingHotkeyShortcutId = null;
            this.recordedVkCode = 0;
            changed = true;
        }

        // Cancel on right-click or Escape
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) ||
            ImGui.IsKeyPressed(ImGuiKey.Escape, false)) {
            this.recordingHotkeyShortcutId = null;
            this.recordedVkCode = 0;
        }
    }

    private void DrawConditionSetSelector(QCBarDefinition bar, int index, ref bool changed) {
        var currentSet = bar.ConditionSetId ?? string.Empty;
        var sets = new List<string> { "无" };
        var setNames = new List<string> { "无" };
        foreach (var cs in this.manager.ConditionSets) {
            sets.Add(cs.Id);
            setNames.Add(cs.Name);
        }

        var selectedIndex = sets.IndexOf(currentSet);
        if (selectedIndex < 0) selectedIndex = 0;

        if (ImGui.Combo($"条件集##qc_bar_cond_{index}", ref selectedIndex, string.Join("\0", setNames) + "\0")) {
            bar.ConditionSetId = selectedIndex == 0 ? null : sets[selectedIndex];
            changed = true;
        }
    }

    private void DrawShortcutListForBar(QCBarDefinition bar, int index, ref bool changed) {
        // Add shortcut to bar
        var availableShortcuts = this.manager.Shortcuts.Values
            .Where(s => !bar.ShortcutIds.Contains(s.Id) && !s.IsCategory)
            .ToList();

        if (availableShortcuts.Count > 0) {
            var shortcutNames = availableShortcuts.Select(s => s.Name).Prepend("选择快捷方式...").ToList();
            var selected = 0;
            if (ImGui.Combo($"##qc_bar_add_shortcut_{index}", ref selected, string.Join("\0", shortcutNames) + "\0")) {
                if (selected > 0) {
                    bar.ShortcutIds.Add(availableShortcuts[selected - 1].Id);
                    changed = true;
                }
            }
        } else {
            ImGui.TextDisabled("暂无可用快捷方式");
        }

        // Existing shortcuts
        ImGui.Spacing();
        var toRemove = -1;
        for (var si = 0; si < bar.ShortcutIds.Count; si++) {
            var sid = bar.ShortcutIds[si];
            if (!this.manager.Shortcuts.TryGetValue(sid, out var shortcut)) {
                toRemove = si;
                continue;
            }

            var label = $"{si + 1}. {shortcut.Name}";
            var modeHint = shortcut.Mode switch {
                QCShortcutMode.Incremental => " [INC]",
                QCShortcutMode.Random => " [RND]",
                _ => "",
            };
            var cooldownHint = shortcut.CooldownActionId != 0 ? $" [CD:{shortcut.CooldownActionId}]" : "";

            ImGui.TextUnformatted($"{label}{modeHint}{cooldownHint}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"编辑##qc_edit_shortcut_{index}_{si}")) {
                this.editingShortcutId = shortcut.Id;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"上移##qc_up_{index}_{si}") && si > 0) {
                (bar.ShortcutIds[si], bar.ShortcutIds[si - 1]) = (bar.ShortcutIds[si - 1], bar.ShortcutIds[si]);
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"下移##qc_down_{index}_{si}") && si < bar.ShortcutIds.Count - 1) {
                (bar.ShortcutIds[si], bar.ShortcutIds[si + 1]) = (bar.ShortcutIds[si + 1], bar.ShortcutIds[si]);
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"移除##qc_remove_{index}_{si}")) {
                toRemove = si;
            }

            // Edit popup
            if (shortcut.Id == this.editingShortcutId) {
                DrawShortcutEditor(shortcut, ref changed);
            }
        }

        if (toRemove >= 0 && toRemove < bar.ShortcutIds.Count) {
            bar.ShortcutIds.RemoveAt(toRemove);
            changed = true;
        }
    }

    private void DrawShortcutList() {
        ImGui.TextDisabled("快捷方式管理 — 创建可复用的快捷方式，支持命令模式（普通/递增/随机）和冷却显示。");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(180.0f);
        if (ImGui.InputTextWithHint("##qc_new_shortcut_name", "快捷方式名称", ref this.newShortcutName, 64)) { }
        ImGui.SameLine();
        if (ImGui.Button("添加快捷方式")) {
            if (!string.IsNullOrWhiteSpace(this.newShortcutName)) {
                this.manager.AddShortcut(this.newShortcutName.Trim());
                this.newShortcutName = "新快捷方式";
                this.saveConfig();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (this.manager.Shortcuts.Count == 0) {
            ImGui.TextDisabled("暂无快捷方式。");
            return;
        }

        var toRemove = string.Empty;
        foreach (var (id, shortcut) in this.manager.Shortcuts) {
            ImGui.PushID($"qc_shortcut_{id}");
            var header = $"{(shortcut.IsCategory ? "[分类] " : "")}{shortcut.Name}";
            if (ImGui.CollapsingHeader(header)) {
                var changed = false;
                DrawShortcutEditor(shortcut, ref changed);

                ImGui.Spacing();
                if (ImGui.SmallButton("删除此快捷方式")) {
                    toRemove = id;
                }

                if (changed) this.saveConfig();
            }
            ImGui.PopID();
        }

        if (!string.IsNullOrEmpty(toRemove)) {
            this.manager.RemoveShortcut(toRemove);
            this.saveConfig();
        }
    }

    private void DrawShortcutEditor(QCShortcutDefinition shortcut, ref bool changed) {
        // Name
        var name = shortcut.Name;
        ImGui.SetNextItemWidth(180.0f);
        if (ImGui.InputText($"名称##qc_shortcut_name_{shortcut.Id}", ref name, 64)) {
            shortcut.Name = name;
            changed = true;
        }

        // Icon ID
        var iconId = (int)shortcut.IconId;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80.0f);
        if (ImGui.InputInt($"图标ID##qc_shortcut_icon_{shortcut.Id}", ref iconId)) {
            shortcut.IconId = (uint)Math.Max(0, iconId);
            changed = true;
        }

        // Type (Command / Category / Spacer)
        var type = (int)shortcut.Type;
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.Combo($"类型##qc_shortcut_type_{shortcut.Id}", ref type, "命令\0分类菜单\0间隔\0")) {
            shortcut.Type = (QCShortcutType)type;
            shortcut.IsCategory = type == 1; // Sync legacy IsCategory
            changed = true;
        }

        // Mode (not applicable for Spacer)
        if (shortcut.Type != QCShortcutType.Spacer) {
            ImGui.SameLine();
            var mode = (int)shortcut.Mode;
            ImGui.SetNextItemWidth(120.0f);
            if (ImGui.Combo($"执行模式##qc_shortcut_mode_{shortcut.Id}", ref mode, "普通\0递增\0随机\0")) {
                shortcut.Mode = (QCShortcutMode)mode;
                shortcut.IncrementalIndex = 0;
                changed = true;
            }
        }

        // For Spacer type, skip command and other non-applicable settings
        if (shortcut.Type == QCShortcutType.Spacer) {
            ImGui.Spacing();
            ImGui.TextDisabled("间隔类型快捷方式仅用于布局占位，不需要命令和图标。");
            return;
        }

        // Tooltip
        var tooltip = shortcut.Tooltip ?? string.Empty;
        ImGui.SetNextItemWidth(300.0f);
        if (ImGui.InputText($"提示##qc_shortcut_tip_{shortcut.Id}", ref tooltip, 128)) {
            shortcut.Tooltip = tooltip;
            changed = true;
        }

        // Command
        var command = shortcut.Command ?? string.Empty;
        ImGui.SetNextItemWidth(400.0f);
        if (ImGui.InputTextMultiline($"命令(每行一条)##qc_shortcut_cmd_{shortcut.Id}", ref command, 1024, new Vector2(400.0f, 80.0f))) {
            shortcut.Command = command;
            changed = true;
        }

        // Hotkey (with recording support)
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("按键与外观");
        ImGui.Spacing();

        var isRecording = this.recordingHotkeyShortcutId == shortcut.Id;
        var hotkeyLabel = isRecording ? "按下按键..." : (shortcut.Hotkey > 0 ? QCManager.GetVkKeyName(shortcut.Hotkey) : "无");
        ImGui.SetNextItemWidth(160.0f);
        ImGui.InputText($"热键##qc_shortcut_hk_{shortcut.Id}", ref hotkeyLabel, 32, ImGuiInputTextFlags.ReadOnly);

        ImGui.SameLine();
        if (ImGui.Button(isRecording ? "取消##qc_hk_rec_{shortcut.Id}" : "录制##qc_hk_rec_{shortcut.Id}")) {
            if (isRecording) {
                this.recordingHotkeyShortcutId = null;
            } else {
                this.recordingHotkeyShortcutId = shortcut.Id;
                this.recordedVkCode = 0;
            }
        }

        // Handle hotkey recording
        if (isRecording) {
            CheckHotkeyRecording(shortcut, ref changed);
        }

        ImGui.SameLine();
        if (shortcut.Hotkey > 0 && ImGui.SmallButton($"清除##qc_hk_clear_{shortcut.Id}")) {
            shortcut.Hotkey = 0;
            changed = true;
        }

        // Key passthrough toggle
        ImGui.SameLine();
        var keyPassthrough = shortcut.KeyPassthrough;
        if (ImGui.Checkbox($"透传按键##qc_shortcut_kp_{shortcut.Id}", ref keyPassthrough)) {
            shortcut.KeyPassthrough = keyPassthrough;
            changed = true;
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("启用后，即使设置了热键，按键也会透传到游戏（不拦截）");
        }

        // Color (ABGR format) with preview
        var color = (int)shortcut.Color;
        ImGui.SetNextItemWidth(100.0f);
        if (ImGui.InputInt($"颜色(ABGR)##qc_shortcut_col_{shortcut.Id}", ref color)) {
            shortcut.Color = (uint)Math.Max(0, color);
            changed = true;
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("ABGR格式颜色值，0xFFFFFFFF=白色(默认)");
        }

        // Color preview swatch
        if (shortcut.Color != 0xFFFFFFFF) {
            ImGui.SameLine();
            var drawList = ImGui.GetWindowDrawList();
            var swatchMin = ImGui.GetCursorScreenPos();
            var swatchSize = 20.0f;
            var swatchMax = swatchMin + new Vector2(swatchSize, swatchSize);
            drawList.AddRectFilled(swatchMin, swatchMax, shortcut.Color, 3.0f);
            drawList.AddRect(swatchMin, swatchMax, ImGui.GetColorU32(new Vector4(0.5f, 0.3f, 0.4f, 0.6f)), 3.0f);
            ImGui.Dummy(new Vector2(swatchSize, swatchSize));
        }

        // Icon zoom
        var iconZoom = shortcut.IconZoom;
        ImGui.SetNextItemWidth(80.0f);
        if (ImGui.SliderFloat($"图标缩放##qc_shortcut_iz_{shortcut.Id}", ref iconZoom, 0.5f, 2.0f)) {
            shortcut.IconZoom = iconZoom;
            changed = true;
        }

        // Icon offset
        ImGui.SameLine();
        var offsetX = shortcut.IconOffset.X;
        ImGui.SetNextItemWidth(60.0f);
        if (ImGui.InputFloat($"偏移X##qc_shortcut_ox_{shortcut.Id}", ref offsetX)) { shortcut.IconOffset = new Vector2(offsetX, shortcut.IconOffset.Y); changed = true; }
        ImGui.SameLine();
        var offsetY = shortcut.IconOffset.Y;
        ImGui.SetNextItemWidth(60.0f);
        if (ImGui.InputFloat($"偏移Y##qc_shortcut_oy_{shortcut.Id}", ref offsetY)) { shortcut.IconOffset = new Vector2(shortcut.IconOffset.X, offsetY); changed = true; }

        // Icon rotation (degrees for easier UI, convert to radians for storage)
        var rotationDeg = shortcut.IconRotation * (180.0f / MathF.PI);
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.SliderFloat($"图标旋转(度)##qc_shortcut_ir_{shortcut.Id}", ref rotationDeg, 0.0f, 360.0f)) {
            shortcut.IconRotation = rotationDeg * (MathF.PI / 180.0f);
            changed = true;
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("图标旋转角度（0-360度）");
        }

        // Cooldown settings
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled("冷却显示");
        ImGui.Spacing();

        var cdActionId = (int)shortcut.CooldownActionId;
        ImGui.SetNextItemWidth(120.0f);
        if (ImGui.InputInt($"冷却动作ID##qc_shortcut_cd_{shortcut.Id}", ref cdActionId)) {
            shortcut.CooldownActionId = (uint)Math.Max(0, cdActionId);
            changed = true;
        }
        ImGui.SameLine();
        var cdStyle = shortcut.CooldownStyle;
        if (ImGui.Combo($"冷却样式##qc_shortcut_cds_{shortcut.Id}", ref cdStyle, "图标覆盖\0仅文字\0")) {
            shortcut.CooldownStyle = cdStyle;
            changed = true;
        }

        // Category children
        if (shortcut.IsCategory) {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("分类子项");
            ImGui.Spacing();

            // Category settings
            var catWidth = shortcut.CategoryWidth;
            ImGui.SetNextItemWidth(80.0f);
            if (ImGui.InputInt($"分类宽度##qc_shortcut_cw_{shortcut.Id}", ref catWidth)) {
                shortcut.CategoryWidth = Math.Max(60, catWidth);
                changed = true;
            }
            ImGui.SameLine();
            var catCols = shortcut.CategoryColumns;
            ImGui.SetNextItemWidth(80.0f);
            if (ImGui.InputInt($"分类列数##qc_shortcut_cc_{shortcut.Id}", ref catCols)) {
                shortcut.CategoryColumns = Math.Max(1, catCols);
                changed = true;
            }
            ImGui.SameLine();
            var catHover = shortcut.CategoryOnHover;
            if (ImGui.Checkbox($"悬停打开##qc_shortcut_ch_{shortcut.Id}", ref catHover)) {
                shortcut.CategoryOnHover = catHover;
                changed = true;
            }
            ImGui.SameLine();
            var catStay = shortcut.CategoryStaysOpen;
            if (ImGui.Checkbox($"保持打开##qc_shortcut_cs_{shortcut.Id}", ref catStay)) {
                shortcut.CategoryStaysOpen = catStay;
                changed = true;
            }

            ImGui.Spacing();

            var availableForChildren = this.manager.Shortcuts.Values
                .Where(s => s.Id != shortcut.Id && !shortcut.ChildShortcutIds.Contains(s.Id))
                .ToList();

            if (availableForChildren.Count > 0) {
                var childNames = availableForChildren.Select(s => s.Name).Prepend("选择...").ToList();
                var childSelected = 0;
                ImGui.SetNextItemWidth(180.0f);
                if (ImGui.Combo($"##qc_shortcut_child_add_{shortcut.Id}", ref childSelected, string.Join("\0", childNames) + "\0")) {
                    if (childSelected > 0) {
                        shortcut.ChildShortcutIds.Add(availableForChildren[childSelected - 1].Id);
                        changed = true;
                    }
                }
            }

            foreach (var childId in shortcut.ChildShortcutIds.ToList()) {
                if (!this.manager.Shortcuts.TryGetValue(childId, out var child)) {
                    shortcut.ChildShortcutIds.Remove(childId);
                    continue;
                }
                ImGui.TextUnformatted($"  - {child.Name}");
                ImGui.SameLine();
                if (ImGui.SmallButton($"移除子项##qc_child_remove_{shortcut.Id}_{childId}")) {
                    shortcut.ChildShortcutIds.Remove(childId);
                    changed = true;
                }
            }
        }
    }

    private void DrawConditionSetList() {
        ImGui.TextDisabled("条件集管理 — 根据游戏状态自动显示/隐藏快捷栏。支持多条件组合（AND/OR/EQUALS/XOR）。");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(180.0f);
        if (ImGui.InputTextWithHint("##qc_new_cond_name", "条件集名称", ref this.newConditionSetName, 64)) { }
        ImGui.SameLine();
        if (ImGui.Button("添加条件集")) {
            if (!string.IsNullOrWhiteSpace(this.newConditionSetName)) {
                this.manager.AddConditionSet(this.newConditionSetName.Trim());
                this.newConditionSetName = "新条件集";
                this.saveConfig();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (this.manager.ConditionSets.Count == 0) {
            ImGui.TextDisabled("暂无条件集。");
            return;
        }

        var toRemove = string.Empty;
        foreach (var cs in this.manager.ConditionSets) {
            ImGui.PushID($"qc_cond_{cs.Id}");
            if (ImGui.CollapsingHeader(cs.Name)) {
                var changed = false;

                var name = cs.Name;
                ImGui.SetNextItemWidth(180.0f);
                if (ImGui.InputText("名称", ref name, 64)) { cs.Name = name; changed = true; }

                ImGui.Spacing();
                ImGui.TextDisabled("条件列表（每个条件独立评估，通过运算符组合）");
                ImGui.Spacing();

                // Draw each condition entry
                var removeIndex = -1;
                for (var i = 0; i < cs.Conditions.Count; i++) {
                    var entry = cs.Conditions[i];
                    ImGui.PushID($"qc_cond_entry_{i}");

                    // Operator (except for first condition)
                    if (i > 0) {
                        var op = (int)entry.Operator;
                        ImGui.SetNextItemWidth(80.0f);
                        if (ImGui.Combo($"##op_{i}", ref op, string.Join("\0", OperatorLabels) + "\0")) {
                            entry.Operator = (QCBinaryOperator)op;
                            changed = true;
                        }
                        ImGui.SameLine();
                    }

                    // Condition type
                    var type = (int)entry.ConditionType;
                    ImGui.SetNextItemWidth(140.0f);
                    if (ImGui.Combo($"##type_{i}", ref type, string.Join("\0", ConditionTypeLabels) + "\0")) {
                        entry.ConditionType = (QCConditionType)type;
                        changed = true;
                    }

                    // Negate
                    ImGui.SameLine();
                    var negate = entry.Negate;
                    if (ImGui.Checkbox($"取反##neg_{i}", ref negate)) {
                        entry.Negate = negate;
                        changed = true;
                    }

                    // Target IDs for ClassJob, Territory, ConditionSet
                    if (entry.ConditionType is QCConditionType.ClassJobId or QCConditionType.TerritoryId or QCConditionType.ConditionSet) {
                        ImGui.SameLine();
                        var targetIds = string.Join(",", entry.TargetIds);
                        ImGui.SetNextItemWidth(160.0f);
                        if (ImGui.InputText($"ID(逗号分隔)##target_{i}", ref targetIds, 128)) {
                            entry.TargetIds = targetIds
                                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Select(s => uint.TryParse(s, out var id) ? id : 0)
                                .Where(id => id > 0)
                                .ToList();
                            changed = true;
                        }
                    }

                    // Remove button
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"删除##del_{i}")) {
                        removeIndex = i;
                    }

                    ImGui.PopID();
                }

                if (removeIndex >= 0) {
                    cs.Conditions.RemoveAt(removeIndex);
                    changed = true;
                }

                // Add condition button
                ImGui.Spacing();
                if (ImGui.SmallButton("添加条件")) {
                    cs.Conditions.Add(new QCConditionEntry {
                        ConditionType = QCConditionType.InCombat,
                        Operator = QCBinaryOperator.AND,
                    });
                    changed = true;
                }

                ImGui.Spacing();
                if (ImGui.SmallButton("删除条件集")) { toRemove = cs.Id; }

                if (changed) this.saveConfig();
            }
            ImGui.PopID();
        }

        if (!string.IsNullOrEmpty(toRemove)) {
            this.manager.RemoveConditionSet(toRemove);
            this.saveConfig();
        }
    }

    private string importResultMessage = string.Empty;
    private double importResultTime;
    private bool clearAllConfirmOpen = true;

    private void DrawImportExport() {
        ImGui.TextDisabled("导入/导出 — 分享和备份快捷栏配置。");
        ImGui.Spacing();

        // Export
        if (ImGui.Button("导出所有快捷栏 (复制到剪贴板)")) {
            var exportText = QCImportExport.ExportBars(this.manager);
            try {
                ImGui.SetClipboardText(exportText);
            } catch {
                // Clipboard set failed silently
            }
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("将当前所有快捷栏配置导出为文本格式，可分享给他人。");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // QoLBar Import
        ImGui.TextDisabled("从 QoLBar 导入");
        ImGui.Spacing();

        if (ImGui.Button("从剪贴板导入 QoLBar 配置")) {
            try {
                var clipboardText = ImGui.GetClipboardText();
                if (!string.IsNullOrWhiteSpace(clipboardText)) {
                    var success = QCQoLBarImport.TryImportFromText(this.manager, clipboardText);
                    if (success) {
                        this.importResultMessage = "QoLBar 配置导入成功！";
                        this.saveConfig();
                    } else {
                        this.importResultMessage = "导入失败：未识别到 QoLBar 格式数据。";
                    }
                } else {
                    this.importResultMessage = "剪贴板为空。";
                }
            } catch (Exception ex) {
                this.importResultMessage = $"导入错误：{ex.Message}";
            }
            this.importResultTime = ImGui.GetTime();
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("从剪贴板读取 QoLBar 导出的配置（支持 GZip+Base64 和纯 JSON 格式）");
        }

        ImGui.SameLine();
        if (ImGui.Button("检测格式")) {
            try {
                var clipboardText = ImGui.GetClipboardText();
                if (!string.IsNullOrWhiteSpace(clipboardText)) {
                    var isQol = QCQoLBarImport.IsQoLBarFormat(clipboardText);
                    this.importResultMessage = isQol
                        ? "剪贴板内容为 QoLBar 格式，可导入。"
                        : "剪贴板内容不是 QoLBar 格式。";
                } else {
                    this.importResultMessage = "剪贴板为空。";
                }
            } catch (Exception ex) {
                this.importResultMessage = $"检测错误：{ex.Message}";
            }
            this.importResultTime = ImGui.GetTime();
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("检测剪贴板内容是否为 QoLBar 导出格式");
        }

        // Show import result message (auto-dismiss after 5 seconds)
        if (!string.IsNullOrEmpty(this.importResultMessage) && ImGui.GetTime() - this.importResultTime < 5.0) {
            ImGui.Spacing();
            ImGui.TextUnformatted(this.importResultMessage);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // QC Format Import
        ImGui.TextDisabled("QC 格式文本导入");
        var importText = string.Empty;
        ImGui.SetNextItemWidth(400.0f);
        if (ImGui.InputTextMultiline("##qc_import_text", ref importText, 4096, new Vector2(400.0f, 120.0f))) { }

        if (ImGui.Button("导入 QC 配置")) {
            if (!string.IsNullOrWhiteSpace(importText)) {
                if (importText.StartsWith("=== QC Bar Export v1 ===")) {
                    QCImportExport.ImportBars(this.manager, importText);
                    this.importResultMessage = "QC 配置导入成功！";
                } else {
                    // 自动尝试 QoLBar 格式
                    var success = QCQoLBarImport.TryImportFromText(this.manager, importText);
                    this.importResultMessage = success
                        ? "QoLBar 配置导入成功！"
                        : "导入失败：无法识别配置格式。";
                }
                this.saveConfig();
                this.importResultTime = ImGui.GetTime();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Clear all data
        ImGui.TextDisabled("危险操作");
        if (ImGui.Button("一键删除导入的预设内容")) {
            ImGui.OpenPopup("确认删除所有预设内容");
        }

        if (ImGui.BeginPopupModal("确认删除所有预设内容", ref this.clearAllConfirmOpen, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.TextUnformatted("确定要删除所有导入的快捷栏、快捷方式和条件集吗？");
            ImGui.TextUnformatted("此操作不可撤销！");
            ImGui.Spacing();
            if (ImGui.Button("确认删除", new Vector2(120.0f, 0.0f))) {
                this.manager.ClearAllData();
                this.saveConfig();
                this.clearAllConfirmOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("取消", new Vector2(120.0f, 0.0f))) {
                this.clearAllConfirmOpen = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}

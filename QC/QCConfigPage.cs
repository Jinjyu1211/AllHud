using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AllHud;

public sealed class QCConfigPage {
    private readonly QCManager manager;
    private readonly Configuration config;
    private readonly Action saveConfig;

    private int selectedTab;
    private bool showClearConfirm;

    private string? importResultMessage;
    private double importResultTime;

    public bool IsOpen { get; set; }

    public QCConfigPage(QCManager manager, Configuration config, Action saveConfig) {
        this.manager = manager;
        this.config = config;
        this.saveConfig = saveConfig;
    }

    public void Draw() {
        if (!this.IsOpen) return;

        ImGui.SetNextWindowSize(new Vector2(600.0f, 400.0f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("QC 快捷栏配置", ref this.IsOpen, ImGuiWindowFlags.NoScrollbar)) {
            ImGui.End();
            return;
        }

        DrawTabs();

        ImGui.Separator();
        ImGui.BeginChild("qc_content", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - 4.0f), false, ImGuiWindowFlags.AlwaysUseWindowPadding);

        switch (this.selectedTab) {
            case 0: DrawBarList(); break;
            case 1: DrawShortcutList(); break;
            case 2: DrawConditionSets(); break;
            case 3: DrawImportExport(); break;
        }

        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawTabs() {
        var tabNames = new[] { "快捷栏", "快捷方式", "条件集", "导入导出" };
        var buttonWidth = 120.0f;
        var buttonHeight = 30.0f;

        for (var i = 0; i < tabNames.Length; i++) {
            if (i > 0) ImGui.SameLine();
            var min = ImGui.GetCursorScreenPos();
            ImGui.InvisibleButton($"qc_tab_{i}", new Vector2(buttonWidth, buttonHeight));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked()) this.selectedTab = i;

            var drawList = ImGui.GetWindowDrawList();
            var max = min + new Vector2(buttonWidth, buttonHeight);
            if (this.selectedTab == i) {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.94f, 0.72f, 0.84f, 0.90f)), 4.0f);
            } else if (hovered) {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.94f, 0.72f, 0.84f, 0.45f)), 4.0f);
            }
            var textSize = ImGui.CalcTextSize(tabNames[i]);
            var textPos = min + (new Vector2(buttonWidth, buttonHeight) - textSize) * 0.5f;
            drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.25f, 0.12f, 0.18f, 1.0f)), tabNames[i]);
        }
    }

    private void DrawBarList() {
        ImGui.TextDisabled("快捷栏列表（待实现）");
        foreach (var bar in this.manager.Bars) {
            ImGui.BulletText($"{bar.Name} ({(bar.Enabled ? "启用" : "禁用")})");
        }
    }

    private void DrawShortcutList() {
        ImGui.TextDisabled("快捷方式列表（待实现）");
        foreach (var kvp in this.manager.Shortcuts) {
            var name = string.IsNullOrWhiteSpace(kvp.Value.Name) ? "(无名称)" : kvp.Value.Name;
            ImGui.BulletText($"{kvp.Key}: {name}");
        }
    }

    private void DrawConditionSets() {
        ImGui.TextDisabled("条件集列表（待实现）");
    }

    private void DrawImportExport() {
        ImGui.TextDisabled("导入 QoLBar 配置");
        ImGui.Spacing();

        if (ImGui.Button("从剪贴板导入 QoLBar 配置", new Vector2(260.0f, 36.0f))) {
            var clipboardText = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardText)) {
                if (QCQoLBarImport.TryImportFromText(this.manager, clipboardText)) {
                    this.importResultMessage = "导入成功！请查看快捷栏和快捷方式列表。";
                } else {
                    this.importResultMessage = "导入失败，剪贴板内容不是有效的 QoLBar 配置格式。";
                }
                this.importResultTime = ImGui.GetTime();
            }
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("先在 QoLBar 中复制配置，然后点击此按钮导入。");
        }

        ImGui.Spacing();

        if (ImGui.Button("检测格式", new Vector2(120.0f, 24.0f))) {
            var clipboardText = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardText)) {
                var format = QCQoLBarImport.DetectFormat(clipboardText);
                this.importResultMessage = $"检测到格式: {format}";
                this.importResultTime = ImGui.GetTime();
            }
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("检测剪贴板中的内容格式。");
        }

        // 导入结果反馈（显示 5 秒）
        if (this.importResultMessage != null && ImGui.GetTime() - this.importResultTime < 5.0) {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.25f, 0.50f, 0.75f, 1.0f), this.importResultMessage);
        }
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 一键删除所有导入内容
        if (ImGui.Button("一键删除所有导入内容", new Vector2(260.0f, 36.0f))) {
            this.showClearConfirm = true;
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("清空所有快捷栏、快捷方式和条件集数据（不可恢复）");
        }

        // 确认弹窗
        if (this.showClearConfirm) {
            ImGui.OpenPopup("确认删除");
        }

        if (ImGui.BeginPopupModal("确认删除", ref this.showClearConfirm, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.TextDisabled("确定要删除所有导入内容吗？");
            ImGui.TextDisabled("此操作将清空所有快捷栏、快捷方式和条件集，且不可恢复。");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("确认删除", new Vector2(120.0f, 0.0f))) {
                this.manager.ClearAllData();
                this.saveConfig();
                this.importResultMessage = "已清空所有导入内容。";
                this.importResultTime = ImGui.GetTime();
                this.showClearConfirm = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("取消", new Vector2(120.0f, 0.0f))) {
                this.showClearConfirm = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}
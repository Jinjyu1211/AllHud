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
        if (!ImGui.Begin("QC \u5FEB\u6377\u680F\u914D\u7F6E", ref this.IsOpen, ImGuiWindowFlags.NoScrollbar)) {
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
        var tabNames = new[] { "\u5FEB\u6377\u680F", "\u5FEB\u6377\u65B9\u5F0F", "\u6761\u4EF6\u96C6", "\u5BFC\u5165\u5BFC\u51FA" };
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
        ImGui.TextDisabled("\u5FEB\u6377\u680F\u5217\u8868\uFF08\u5F85\u5B9E\u73B0\uFF09");
        foreach (var bar in this.manager.Bars) {
            ImGui.BulletText($"{bar.Name} ({(bar.Enabled ? "\u542F\u7528" : "\u7981\u7528")})");
        }
    }

    private void DrawShortcutList() {
        ImGui.TextDisabled("\u5FEB\u6377\u65B9\u5F0F\u5217\u8868\uFF08\u5F85\u5B9E\u73B0\uFF09");
        foreach (var kvp in this.manager.Shortcuts) {
            var name = string.IsNullOrWhiteSpace(kvp.Value.Name) ? "(\u65E0\u540D\u79F0)" : kvp.Value.Name;
            ImGui.BulletText($"{kvp.Key}: {name}");
        }
    }

    private void DrawConditionSets() {
        ImGui.TextDisabled("\u6761\u4EF6\u96C6\u5217\u8868\uFF08\u5F85\u5B9E\u73B0\uFF09");
    }

    private void DrawImportExport() {
        ImGui.TextDisabled("\u5BFC\u5165 QoLBar \u914D\u7F6E");
        ImGui.Spacing();

        if (ImGui.Button("\u4ECE\u526A\u8D34\u677F\u5BFC\u5165 QoLBar \u914D\u7F6E", new Vector2(260.0f, 36.0f))) {
            var clipboardText = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardText)) {
                if (QCQoLBarImport.TryImportFromText(this.manager, clipboardText)) {
                    this.importResultMessage = "\u5BFC\u5165\u6210\u529F\uFF01\u8BF7\u67E5\u770B\u5FEB\u6377\u680F\u548C\u5FEB\u6377\u65B9\u5F0F\u5217\u8868\u3002";
                } else {
                    this.importResultMessage = "\u5BFC\u5165\u5931\u8D25\uFF0C\u526A\u8D34\u677F\u5185\u5BB9\u4E0D\u662F\u6709\u6548\u7684 QoLBar \u914D\u7F6E\u683C\u5F0F\u3002";
                }
                this.importResultTime = ImGui.GetTime();
            }
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("\u5148\u5728 QoLBar \u4E2D\u590D\u5236\u914D\u7F6E\uFF0C\u7136\u540E\u70B9\u51FB\u6B64\u6309\u94AE\u5BFC\u5165\u3002");
        }

        ImGui.Spacing();

        if (ImGui.Button("\u68C0\u6D4B\u683C\u5F0F", new Vector2(120.0f, 24.0f))) {
            var clipboardText = ImGui.GetClipboardText();
            if (!string.IsNullOrWhiteSpace(clipboardText)) {
                var format = QCQoLBarImport.DetectFormat(clipboardText);
                this.importResultMessage = $"\u68C0\u6D4B\u5230\u683C\u5F0F: {format}";
                this.importResultTime = ImGui.GetTime();
            }
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("\u68C0\u6D4B\u526A\u8D34\u677F\u4E2D\u7684\u5185\u5BB9\u683C\u5F0F\u3002");
        }

        // \u5BFC\u5165\u7ED3\u679C\u53CD\u9988（\u663E\u793A 5 \u79D2\u949F\uFF09
        if (this.importResultMessage != null && ImGui.GetTime() - this.importResultTime < 5.0) {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.25f, 0.50f, 0.75f, 1.0f), this.importResultMessage);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // \u4E00\u952E\u5220\u9664\u6240\u6709\u5BFC\u5165\u5185\u5BB9
        if (ImGui.Button("\u4E00\u952E\u5220\u9664\u6240\u6709\u5BFC\u5165\u5185\u5BB9", new Vector2(260.0f, 36.0f))) {
            this.showClearConfirm = true;
        }

        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("\u6E05\u7A7A\u6240\u6709\u5FEB\u6377\u680F\u3001\u5FEB\u6377\u65B9\u5F0F\u548C\u6761\u4EF6\u96C6\u6570\u636E\uFF08\u4E0D\u53EF\u6062\u590D\uFF09");
        }

        // \u786E\u8BA4\u5F39\u7A97
        if (this.showClearConfirm) {
            ImGui.OpenPopup("\u786E\u8BA4\u5220\u9664");
        }

        if (ImGui.BeginPopupModal("\u786E\u8BA4\u5220\u9664", ref this.showClearConfirm, ImGuiWindowFlags.AlwaysAutoResize)) {
            ImGui.TextDisabled("\u786E\u5B9A\u8981\u5220\u9664\u6240\u6709\u5BFC\u5165\u5185\u5BB9\u5417\uFF1F");
            ImGui.TextDisabled("\u6B64\u64CD\u4F5C\u5C06\u6E05\u7A7A\u6240\u6709\u5FEB\u6377\u680F\u3001\u5FEB\u6377\u65B9\u5F0F\u548C\u6761\u4EF6\u96C6\uFF0C\u4E14\u4E0D\u53EF\u6062\u590D\u3002");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("\u786E\u8BA4\u5220\u9664", new Vector2(120.0f, 0.0f))) {
                this.manager.ClearAllData();
                this.saveConfig();
                this.importResultMessage = "\u5DF2\u6E05\u7A7A\u6240\u6709\u5BFC\u5165\u5185\u5BB9\u3002";
                this.importResultTime = ImGui.GetTime();
                this.showClearConfirm = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("\u53D6\u6D88", new Vector2(120.0f, 0.0f))) {
                this.showClearConfirm = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
}

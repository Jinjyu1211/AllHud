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
    private void DrawTargetInfoPage() {
        DrawSectionCard("目标情报", () => {
            DrawCheckbox("启动目标情报", nameof(this.config.ShowCustomTargetInfo), this.config.ShowCustomTargetInfo, value => this.config.ShowCustomTargetInfo = value);
            ImGui.SameLine(0.0f, 10.0f);
            DrawCheckbox("锁定窗口", nameof(this.config.TargetInfoLocked), this.config.TargetInfoLocked, value => this.config.TargetInfoLocked = value);

            DrawTargetInfoSubsection("生命值");
            DrawHudScaleCombo("整体缩放", this.config.CustomTargetInfoScale, value => this.config.CustomTargetInfoScale = value);
            DrawCheckbox("隐藏血量数字", nameof(this.config.CustomTargetInfoHideHpNumbers), this.config.CustomTargetInfoHideHpNumbers, value => this.config.CustomTargetInfoHideHpNumbers = value);
            if (!this.config.CustomTargetInfoHideHpNumbers) {
                DrawCheckbox("不显示总血量", nameof(this.config.CustomTargetInfoHideMaxHp), this.config.CustomTargetInfoHideMaxHp, value => this.config.CustomTargetInfoHideMaxHp = value);
            }

            DrawTargetInfoSubsection("状态栏");
            DrawCheckbox("拆分状态栏", nameof(this.config.CustomTargetInfoSplitStatusBar), this.config.CustomTargetInfoSplitStatusBar, value => this.config.CustomTargetInfoSplitStatusBar = value);
            if (this.config.CustomTargetInfoSplitStatusBar) {
                DrawHudScaleCombo("状态栏缩放", this.config.CustomTargetInfoStatusBarScale, value => this.config.CustomTargetInfoStatusBarScale = value);
            }

            DrawCheckbox("状态栏在生命值上方", nameof(this.config.CustomTargetInfoStatusesAboveHp), this.config.CustomTargetInfoStatusesAboveHp, value => this.config.CustomTargetInfoStatusesAboveHp = value);
            DrawCheckbox("目标状态仅显示我施加的", nameof(this.config.OnlyShowSelfAppliedTargetStatuses), this.config.OnlyShowSelfAppliedTargetStatuses, value => this.config.OnlyShowSelfAppliedTargetStatuses = value);

            var statusRows = Math.Clamp(this.config.CustomTargetInfoStatusRows, 1, 2);
            DrawSegmentedSelector("状态排列", "target_status_rows", statusRows, value => this.config.CustomTargetInfoStatusRows = value, ("单排", 1), ("双排", 2));

            DrawTargetInfoSubsection("咏唱栏");
            DrawCheckbox("拆分咏唱栏", nameof(this.config.CustomTargetInfoSplitCastBar), this.config.CustomTargetInfoSplitCastBar, value => this.config.CustomTargetInfoSplitCastBar = value);
            if (this.config.CustomTargetInfoSplitCastBar) {
                DrawHudScaleCombo("咏唱栏缩放", this.config.CustomTargetInfoCastBarScale, value => this.config.CustomTargetInfoCastBarScale = value);
            } else {
                DrawCastBarPlacementSelector();
            }

        });
    }

    private void DrawStatusBarPage() {
        DrawSectionCard("状态栏", () => {
            DrawCheckbox("启动状态栏", nameof(this.config.ShowStatusOverlay), this.config.ShowStatusOverlay, value => this.config.ShowStatusOverlay = value);
            ImGui.SameLine(0.0f, 10.0f);
            DrawCheckbox("锁定窗口", nameof(this.config.StatusBarLocked), this.config.StatusBarLocked, value => this.config.StatusBarLocked = value);
            ImGui.SameLine(0.0f, 10.0f);
            DrawCheckbox("鼠标穿透", nameof(this.config.StatusBarMousePassthrough), this.config.StatusBarMousePassthrough, value => this.config.StatusBarMousePassthrough = value);
            DrawSegmentedSelector("布局模式", "status_bar_layout_mode", Math.Clamp(this.config.StatusBarLayoutMode, 0, 1), value => this.config.StatusBarLayoutMode = value, ("合并", 0), ("拆分", 1));

            DrawTargetInfoSubsection("内容");
            DrawCheckbox("显示弱化状态", nameof(this.config.ShowSelfEnfeeblements), this.config.ShowSelfEnfeeblements, value => this.config.ShowSelfEnfeeblements = value);
            DrawCheckbox("显示其他状态（食物 / 部队等）", nameof(this.config.ShowSelfOtherStatuses), this.config.ShowSelfOtherStatuses, value => this.config.ShowSelfOtherStatuses = value);
            DrawCheckbox("显示强化状态", nameof(this.config.ShowSelfBuffs), this.config.ShowSelfBuffs, value => this.config.ShowSelfBuffs = value);

            DrawTargetInfoSubsection("来源");
            DrawCheckbox("来源显示职业名", nameof(this.config.ShowSourceJobNames), this.config.ShowSourceJobNames, value => this.config.ShowSourceJobNames = value);

            DrawTargetInfoSubsection("外观");
            DrawCheckbox("使用主题强调色", nameof(this.config.StatusPanelUseAccentColor), this.config.StatusPanelUseAccentColor, value => this.config.StatusPanelUseAccentColor = value);
            if (!this.config.StatusPanelUseAccentColor) {
                DrawColorPicker("背景色", this.config.StatusPanelCustomBackground, value => this.config.StatusPanelCustomBackground = value);
                DrawColorPicker("边框色", this.config.StatusPanelCustomBorder, value => this.config.StatusPanelCustomBorder = value);
                DrawColorPicker("阴影色", this.config.StatusPanelCustomShadow, value => this.config.StatusPanelCustomShadow = value);
                DrawColorPicker("标签背景", this.config.StatusSectionCustomLabelBackground, value => this.config.StatusSectionCustomLabelBackground = value);
                DrawColorPicker("标签边框", this.config.StatusSectionCustomLabelBorder, value => this.config.StatusSectionCustomLabelBorder = value);
                DrawColorPicker("分隔线", this.config.StatusSectionCustomDivider, value => this.config.StatusSectionCustomDivider = value);
            }

            DrawOpacitySlider("背景透明度", "status_bg_opacity", this.config.StatusPanelBackgroundOpacity, value => this.config.StatusPanelBackgroundOpacity = value);
            DrawOpacitySlider("边框透明度", "status_border_opacity", this.config.StatusPanelBorderOpacity, value => this.config.StatusPanelBorderOpacity = value);
            DrawOpacitySlider("阴影透明度", "status_shadow_opacity", this.config.StatusPanelShadowOpacity, value => this.config.StatusPanelShadowOpacity = value);
            DrawOpacitySlider("标签背景透明度", "status_label_bg_opacity", this.config.StatusSectionLabelBackgroundOpacity, value => this.config.StatusSectionLabelBackgroundOpacity = value);
            DrawOpacitySlider("标签边框透明度", "status_label_border_opacity", this.config.StatusSectionLabelBorderOpacity, value => this.config.StatusSectionLabelBorderOpacity = value);
            DrawOpacitySlider("分隔线透明度", "status_divider_opacity", this.config.StatusSectionDividerOpacity, value => this.config.StatusSectionDividerOpacity = value);
        });
    }

    private void DrawCastBarPlacementSelector() {
        var placement = Math.Clamp(this.config.CustomTargetInfoCastBarPlacement, 0, 2);
        DrawSegmentedSelector("咏唱栏位置", "cast_bar_placement", placement, value => this.config.CustomTargetInfoCastBarPlacement = value, ("侧边", 0), ("顶部", 1), ("底部", 2));
    }

    private void DrawPartyInfoPage() {
        DrawSectionCard("队伍信息", () => {
            DrawCheckbox("启动队伍信息", nameof(this.config.ShowPartyInfo), this.config.ShowPartyInfo, value => this.config.ShowPartyInfo = value);
            ImGui.SameLine(0.0f, 10.0f);
            DrawCheckbox("鼠标穿透", nameof(this.config.PartyInfoMousePassthrough), this.config.PartyInfoMousePassthrough, value => this.config.PartyInfoMousePassthrough = value);

            ImGui.TextDisabled("队伍信息会贴在原生队伍列表旁显示，仅副本中启用。");

            DrawTargetInfoSubsection("内容");
            DrawCheckbox("显示减伤", "ShowMergedMitigationCooldowns", IsMergedMitigationCooldownsEnabled(), value => {
                this.config.ShowPartyMitigationCooldowns = value;
                this.config.ShowTargetMitigationCooldowns = value;
                this.config.ShowPersonalMitigationCooldowns = value;
                this.config.ShowMitigationCooldowns = value;
            });
            DrawCheckbox("显示食物检查", nameof(this.config.ShowPartyFoodCheck), this.config.ShowPartyFoodCheck, value => this.config.ShowPartyFoodCheck = value);
            DrawCheckbox("显示极限技槽", nameof(this.config.ShowPartyLimitBreakBar), this.config.ShowPartyLimitBreakBar, value => this.config.ShowPartyLimitBreakBar = value);
            if (this.config.ShowPartyLimitBreakBar) {
                DrawLimitBreakPositionSelector();
            }

            DrawCheckbox("隐藏已结束的队伍冷却", nameof(this.config.HideExpiredCooldowns), this.config.HideExpiredCooldowns, value => this.config.HideExpiredCooldowns = value);
        });
    }

    private void DrawAppearancePage() {
        DrawSectionCard("外观主题", () => {
            DrawTargetInfoSubsection("主题方案");
            var preset = (int)this.config.ActiveThemePreset;
            var hasImported = this.config.ImportedStyleColors is { Count: > 0 };
            DrawSegmentedSelector("当前方案", "theme_preset", preset, v => {
                this.config.ActiveThemePreset = (ThemePreset)v;
                this.saveConfig();
            }, ("默认粉白", 0), ("自定义主题", 1), ("导入样式", 2));

            if (this.config.ActiveThemePreset == ThemePreset.Imported && !hasImported) {
                ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.5f, 1.0f), "尚未导入样式，将回退到默认主题。请先从下方导入。");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawTargetInfoSubsection("样式预设导入");
            if (hasImported) {
                ImGui.TextColored(new Vector4(0.6f, 0.9f, 1.0f, 1.0f), $"已导入: {this.config.ImportedStyleName ?? "未命名"}");
                ImGui.SameLine();
                if (ImGui.Button("清除导入样式")) {
                    this.config.ImportedStyleColors = null;
                    this.config.ImportedStyleVars = null;
                    this.config.ImportedStyleName = null;
                    if (this.config.ActiveThemePreset == ThemePreset.Imported) {
                        this.config.ActiveThemePreset = ThemePreset.Default;
                    }
                    this.saveConfig();
                }
                ImGui.TextDisabled("导入样式可在上方主题方案中切换使用。");
            } else {
                if (ImGui.Button("从剪贴板导入样式")) {
                    var clip = ImGui.GetClipboardText();
                    var preset2 = StylePreset.Decode(clip);
                    if (preset2 != null && preset2.Colors.Count > 0) {
                        this.config.ImportedStyleColors = preset2.Colors;
                        this.config.ImportedStyleVars = preset2.ToStyleVars();
                        this.config.ImportedStyleName = preset2.Name;
                        this.saveConfig();
                    }
                }
                ImGui.TextDisabled("支持导入 Dalamud 样式预设（DS1 开头的压缩码）。");
            }

            ImGui.Spacing();
            DrawTargetInfoSubsection("内置模板");
            ImGui.TextDisabled("选择一个内置模板导入为当前样式预设：");
            ImGui.Spacing();
            for (var i = 0; i < StylePreset.BuiltInPresets.Length; i++) {
                var bp = StylePreset.BuiltInPresets[i];
                if (ImGui.Button($"{bp.Name}##builtin_{i}")) {
                    this.config.ImportedStyleColors = new Dictionary<string, Vector4>(bp.Colors);
                    this.config.ImportedStyleVars = bp.ToStyleVars();
                    this.config.ImportedStyleName = bp.Name;
                    this.config.ActiveThemePreset = ThemePreset.Imported;
                    this.saveConfig();
                }
                if (i < StylePreset.BuiltInPresets.Length - 1) {
                    ImGui.SameLine();
                }
            }

            if (this.config.ActiveThemePreset != ThemePreset.Custom) {
                return;
            }

            ImGui.Spacing();
            ImGui.Separator();

            DrawTargetInfoSubsection("自定义颜色");
            DrawColorPicker("强调色", this.config.CustomThemeAccentColor, value => this.config.CustomThemeAccentColor = value);
            DrawColorPicker("背景色", this.config.CustomThemeBackgroundColor, value => this.config.CustomThemeBackgroundColor = value);
            DrawColorPicker("文字色", this.config.CustomThemeTextColor, value => this.config.CustomThemeTextColor = value);

            ImGui.Spacing();
            if (ImGui.Button("重置为默认")) {
                this.config.CustomThemeAccentColor = new Vector4(0.84f, 0.34f, 0.52f, 1.0f);
                this.config.CustomThemeBackgroundColor = new Vector4(0.992f, 0.940f, 0.948f, 1.0f);
                this.config.CustomThemeTextColor = new Vector4(0.42f, 0.28f, 0.35f, 1.0f);
                this.saveConfig();
            }
        });
    }

    private void DrawColorPicker(string label, Vector4 current, Action<Vector4> setter) {
        var value = current;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(0.0f, 8.0f);
        ImGui.SetNextItemWidth(160.0f);
        if (ImGui.ColorEdit4($"##{label}", ref value, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar)) {
            setter(value);
            this.saveConfig();
        }
    }

    private void DrawOpacitySlider(string label, string id, float current, Action<float> setter, float minValue = 0.0f, float maxValue = 1.0f) {
        var percentValue = current * 100.0f;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(0.0f, 8.0f);
        ImGui.SetNextItemWidth(160.0f);
        if (ImGui.SliderFloat($"##{id}", ref percentValue, minValue * 100.0f, maxValue * 100.0f, $"{percentValue:0}%")) {
            setter(Math.Clamp(percentValue / 100.0f, minValue, maxValue));
            this.saveConfig();
        }
    }

}

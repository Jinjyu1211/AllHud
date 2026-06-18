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
            DrawSegmentedSelector("布局模式", "status_bar_layout_mode", Math.Clamp(this.config.StatusBarLayoutMode, 0, 1), value => this.config.StatusBarLayoutMode = value, ("合并", 0), ("拆分", 1));

            DrawTargetInfoSubsection("内容");
            DrawCheckbox("显示弱化状态", nameof(this.config.ShowSelfEnfeeblements), this.config.ShowSelfEnfeeblements, value => this.config.ShowSelfEnfeeblements = value);
            DrawCheckbox("显示其他状态（食物 / 部队等）", nameof(this.config.ShowSelfOtherStatuses), this.config.ShowSelfOtherStatuses, value => this.config.ShowSelfOtherStatuses = value);
            DrawCheckbox("显示强化状态", nameof(this.config.ShowSelfBuffs), this.config.ShowSelfBuffs, value => this.config.ShowSelfBuffs = value);

            DrawTargetInfoSubsection("来源");
            DrawCheckbox("来源显示职业名", nameof(this.config.ShowSourceJobNames), this.config.ShowSourceJobNames, value => this.config.ShowSourceJobNames = value);
        });
    }

    private void DrawCastBarPlacementSelector() {
        var placement = Math.Clamp(this.config.CustomTargetInfoCastBarPlacement, 0, 2);
        DrawSegmentedSelector("咏唱栏位置", "cast_bar_placement", placement, value => this.config.CustomTargetInfoCastBarPlacement = value, ("侧边", 0), ("顶部", 1), ("底部", 2));
    }

    private void DrawPartyInfoPage() {
        DrawSectionCard("队伍信息", () => {
            DrawCheckbox("启动队伍信息", nameof(this.config.ShowPartyInfo), this.config.ShowPartyInfo, value => this.config.ShowPartyInfo = value);

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

}

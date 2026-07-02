using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace AllHud.Windows;

public sealed partial class ConfigWindow {
    private void DrawWorldMarkersPage() {
        DrawTargetInfoSubsection("世界标记");
        DrawCheckbox("启用世界标记模块", nameof(this.config.ShowWorldMarkers), this.config.ShowWorldMarkers, value => {
            this.config.ShowWorldMarkers = value;
            this.saveConfig();
        });

        if (!this.config.ShowWorldMarkers) return;

        ImGui.Spacing();
        DrawTargetInfoSubsection("标记类型");

        DrawCheckbox("采集点", nameof(this.config.ShowGatheringNodeMarkers), this.config.ShowGatheringNodeMarkers, value => {
            this.config.ShowGatheringNodeMarkers = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("(默认开) 靠近采集点时显示图标、等级与产出物品");
        ImGui.PopTextWrapPos();

        DrawCheckbox("地图标记 (Flag)", nameof(this.config.ShowFlagMarker), this.config.ShowFlagMarker, value => {
            this.config.ShowFlagMarker = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("玩家在地图上 Ctrl+右键设置的临时标记，始终可见");
        ImGui.PopTextWrapPos();

        DrawCheckbox("玩家位置 (Pos)", nameof(this.config.ShowPlayerPositionMarker), this.config.ShowPlayerPositionMarker, value => {
            this.config.ShowPlayerPositionMarker = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("在玩家脚下显示自身位置标记，对应 <pos>");
        ImGui.PopTextWrapPos();

        DrawCheckbox("显示名字与坐标", nameof(this.config.ShowPlayerPositionLabel), this.config.ShowPlayerPositionLabel, value => {
            this.config.ShowPlayerPositionLabel = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("关闭时仅保留指示标，避免遮挡画面");
        ImGui.PopTextWrapPos();

        ImGui.Spacing();
        DrawTargetInfoSubsection("淡出设置");

        ImGui.PushItemWidth(120f);
        var fadeDist = this.config.WorldMarkerFadeDistance;
        if (ImGui.SliderInt($"开始淡出距离##{nameof(this.config.WorldMarkerFadeDistance)}", ref fadeDist, 0, 200)) {
            this.config.WorldMarkerFadeDistance = fadeDist;
            this.saveConfig();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("米");

        var fadeAttn = this.config.WorldMarkerFadeAttenuation;
        if (ImGui.SliderInt($"淡出衰减范围##{nameof(this.config.WorldMarkerFadeAttenuation)}", ref fadeAttn, 1, 100)) {
            this.config.WorldMarkerFadeAttenuation = fadeAttn;
            this.saveConfig();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("米");

        var maxVisDist = this.config.WorldMarkerMaxVisibleDistance;
        if (ImGui.SliderInt($"最大可见距离##{nameof(this.config.WorldMarkerMaxVisibleDistance)}", ref maxVisDist, 0, 500)) {
            this.config.WorldMarkerMaxVisibleDistance = maxVisDist;
            this.saveConfig();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(0=不限制)");
        ImGui.PopItemWidth();

        ImGui.Spacing();
        DrawTargetInfoSubsection("使用说明");
        ImGui.PushTextWrapPos();
        ImGui.BulletText("采集点：产出物品每 2 秒轮播，淡出参数可在上方调整");
        ImGui.BulletText("地图标记：读取 AgentMap 原生 FlagMapMarkers，遵循统一淡出设置");
        ImGui.BulletText("玩家位置：8-18 米半透明渐隐，避免贴脸遮挡");
        ImGui.PopTextWrapPos();
    }
}

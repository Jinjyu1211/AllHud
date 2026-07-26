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

        DrawCheckbox("地图标记 (Flag)", nameof(this.config.ShowFlagMarker), this.config.ShowFlagMarker, value => {
            this.config.ShowFlagMarker = value;
            this.saveConfig();
        });

        DrawCheckbox("玩家位置 (Pos)", nameof(this.config.ShowPlayerPositionMarker), this.config.ShowPlayerPositionMarker, value => {
            this.config.ShowPlayerPositionMarker = value;
            this.saveConfig();
        });

        DrawCheckbox("显示名字与坐标", nameof(this.config.ShowPlayerPositionLabel), this.config.ShowPlayerPositionLabel, value => {
            this.config.ShowPlayerPositionLabel = value;
            this.saveConfig();
        });

        DrawCheckbox("任务标记", nameof(this.config.ShowQuestMarkers), this.config.ShowQuestMarkers, value => {
            this.config.ShowQuestMarkers = value;
            this.saveConfig();
        });

        DrawCheckbox("地图链接 (Aetheryte)", nameof(this.config.ShowMapLinkMarkers), this.config.ShowMapLinkMarkers, value => {
            this.config.ShowMapLinkMarkers = value;
            this.saveConfig();
        });

        ImGui.Spacing();
        DrawTargetInfoSubsection("距离与指南针");

        DrawCheckbox("显示距离", nameof(this.config.ShowMarkerDistance), this.config.ShowMarkerDistance, value => {
            this.config.ShowMarkerDistance = value;
            this.saveConfig();
        });

        DrawCheckbox("指南针", nameof(this.config.ShowCompass), this.config.ShowCompass, value => {
            this.config.ShowCompass = value;
            this.saveConfig();
        });

        ImGui.PushItemWidth(120f);
        var compassRadius = this.config.CompassRadius;
        if (ImGui.SliderInt($"指南针半径##{nameof(this.config.CompassRadius)}", ref compassRadius, 50, 800)) {
            this.config.CompassRadius = compassRadius;
            this.saveConfig();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("px");

        var compassScale = this.config.CompassIconScale;
        if (ImGui.SliderInt($"指南针缩放##{nameof(this.config.CompassIconScale)}", ref compassScale, 50, 200)) {
            this.config.CompassIconScale = compassScale;
            this.saveConfig();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("%");
        ImGui.PopItemWidth();

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
    }
}

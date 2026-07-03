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

        DrawCheckbox("任务标记", nameof(this.config.ShowQuestMarkers), this.config.ShowQuestMarkers, value => {
            this.config.ShowQuestMarkers = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("(默认开) 读取 AgentMap 原生地图标记，显示当前地图的任务图标");
        ImGui.PopTextWrapPos();

        DrawCheckbox("地图链接 (Aetheryte)", nameof(this.config.ShowMapLinkMarkers), this.config.ShowMapLinkMarkers, value => {
            this.config.ShowMapLinkMarkers = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("(默认开) 显示场景内的水晶与以太网碎片位置");
        ImGui.PopTextWrapPos();

        ImGui.Spacing();
        DrawTargetInfoSubsection("距离与指南针");

        DrawCheckbox("显示距离", nameof(this.config.ShowMarkerDistance), this.config.ShowMarkerDistance, value => {
            this.config.ShowMarkerDistance = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("在标记下方显示与玩家的距离 (yalms)");
        ImGui.PopTextWrapPos();

        DrawCheckbox("指南针", nameof(this.config.ShowCompass), this.config.ShowCompass, value => {
            this.config.ShowCompass = value;
            this.saveConfig();
        });
        ImGui.SameLine();
        ImGui.PushTextWrapPos();
        ImGui.TextDisabled("屏幕外标记在屏幕边缘显示方向箭头");
        ImGui.PopTextWrapPos();

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

        ImGui.Spacing();
        DrawTargetInfoSubsection("使用说明");
        ImGui.PushTextWrapPos();
        ImGui.BulletText("采集点：产出物品每 2 秒轮播，淡出参数可在上方调整");
        ImGui.BulletText("地图标记：读取 AgentMap 原生 FlagMapMarkers，遵循统一淡出设置");
        ImGui.BulletText("玩家位置：8-18 米半透明渐隐，避免贴脸遮挡");
        ImGui.BulletText("任务标记：读取 AgentMap.MapMarkers，过滤不可用任务图标");
        ImGui.BulletText("地图链接：扫描 ObjectTable 中的水晶，使用游戏原生图标 60443");
        ImGui.BulletText("指南针：屏幕外标记在玩家屏幕位置 + 方向半径处显示箭头");
        ImGui.BulletText("距离：所有标记下方显示 yalms 距离，可在开关中关闭");
        ImGui.PopTextWrapPos();
    }
}

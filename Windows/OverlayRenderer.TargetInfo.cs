using AllHud.Data;
using AllHud.Models;
using AllHud.Services;
using Dalamud.Game.Addon.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Config;
using Dalamud.Game.Inventory;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using LuminaGeneralAction = Lumina.Excel.Sheets.GeneralAction;
using LuminaItem = Lumina.Excel.Sheets.Item;
using LuminaClassJob = Lumina.Excel.Sheets.ClassJob;
using LuminaMainCommand = Lumina.Excel.Sheets.MainCommand;
using LuminaMainCommandCategory = Lumina.Excel.Sheets.MainCommandCategory;
using LuminaTerritoryType = Lumina.Excel.Sheets.TerritoryType;

namespace AllHud.Windows;

public sealed partial class OverlayRenderer {
    private void DrawCustomTargetInfoWindow(ImGuiWindowFlags flags) {
        if (!this.config.ShowCustomTargetInfo) {
            return;
        }

        if (this.config.TargetInfoLocked) {
            flags |= ImGuiWindowFlags.NoMove;
        }

        if (IsNativeContextMenuVisible()) {
            flags |= ImGuiWindowFlags.NoInputs;
        }

        var targetInfo = this.config.ShowTargetInfoPreview
            ? CreatePreviewTargetInfo()
            : this.combatState.GetTargetInfo(this.config);
        if (targetInfo is null || targetInfo.MaxHp == 0) {
            return;
        }

        var mainScale = Math.Clamp(this.config.CustomTargetInfoScale, 0.6f, 2.0f);
        if (!IsHudFontReady(GetFontSize(mainScale))) {
            return;
        }

        var width = 500.0f * mainScale;
        var padX = 6.0f * mainScale;
        var padY = 6.0f * mainScale;
        var nameRowHeight = 24.0f * mainScale;
        var hpBarHeight = 24.0f * mainScale;
        var targetOfTargetHeight = hpBarHeight;
        var targetOfTargetGap = 18.0f * mainScale;
        var castBarHeight = hpBarHeight;
        var statusTopGap = 4.0f * mainScale;
        var iconSize = NativeStatusIconSize * mainScale;
        var selfAppliedIconSize = 40.0f * mainScale;
        var iconGap = -8.0f * mainScale;
        var statusTimerHeight = 18.0f * mainScale;
        const int statusColumns = 15;

        var contentWidth = width - padX * 2.0f;
        var hasTargetOfTarget = targetInfo.TargetOfTarget is not null;
        var targetOfTargetWidth = hasTargetOfTarget ? Math.Clamp(contentWidth * 0.56f, 120.0f, 180.0f) : 0.0f;
        var totalWindowWidth = width + (hasTargetOfTarget ? targetOfTargetGap + targetOfTargetWidth : 0.0f);
        var maxStatusRows = Math.Clamp(this.config.CustomTargetInfoStatusRows, 1, 2);
        var statusesAboveHp = this.config.CustomTargetInfoStatusesAboveHp;
        var orderedStatuses = GetOrderedTargetStatuses(targetInfo.Statuses);
        var statusLayouts = BuildTargetStatusIconLayouts(orderedStatuses, contentWidth, maxStatusRows, statusColumns, selfAppliedIconSize, iconSize, iconGap, statusTimerHeight, statusesAboveHp, out var statusGridHeight);
        var statusHeight = statusLayouts.Count == 0
            ? 0.0f
            : statusTopGap + statusGridHeight;
        var castBarWidth = Math.Clamp(contentWidth * 0.46f, 190.0f * mainScale, 240.0f * mainScale);
        var drawCastInMain = !this.config.CustomTargetInfoSplitCastBar;
        var drawStatusInMain = !this.config.CustomTargetInfoSplitStatusBar;
        var castPlacement = Math.Clamp(this.config.CustomTargetInfoCastBarPlacement, 0, 2);
        var shouldDrawCastBar = ShouldDrawTargetCastBar(targetInfo);
        var drawCastSide = drawCastInMain && shouldDrawCastBar && castPlacement == 0;
        var drawCastTop = drawCastInMain && shouldDrawCastBar && castPlacement == 1;
        var drawCastBottom = drawCastInMain && shouldDrawCastBar && castPlacement == 2;
        var visibleStatusHeight = drawStatusInMain ? statusHeight : 0.0f;
        var castGap = 5.0f * mainScale;
        var castTopHeight = drawCastTop ? castBarHeight + castGap : 0.0f;
        var castBottomHeight = drawCastBottom ? castGap + castBarHeight : 0.0f;
        var height = statusesAboveHp
            ? padY * 2.0f + castTopHeight + visibleStatusHeight + hpBarHeight + 2.0f * mainScale + Math.Max(nameRowHeight, drawCastSide ? castBarHeight : 0.0f) + castBottomHeight
            : padY * 2.0f + castTopHeight + nameRowHeight + 2.0f * mainScale + hpBarHeight + visibleStatusHeight + castBottomHeight;
        var windowSize = new Vector2(totalWindowWidth, height);

        ImGui.SetNextWindowPos(this.config.CustomTargetInfoPosition, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(windowSize);

        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration;
        if (!ImGui.Begin("AllHud 目标情报", flags)) {
            ImGui.End();
            return;
        }

        TrackTargetInfoPosition();

        var drawList = ImGui.GetWindowDrawList();
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + windowSize;
        var backgroundOpacity = Math.Clamp(this.config.CustomTargetInfoBackgroundOpacity, 0.0f, 0.80f);
        if (backgroundOpacity > 0.001f) {
            drawList.AddRectFilled(windowMin + new Vector2(1.0f, 1.0f), windowMax + new Vector2(1.0f, 1.0f), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, Math.Min(0.45f, backgroundOpacity * 1.7f))), 4.0f);
            drawList.AddRectFilled(windowMin, windowMax, ImGui.GetColorU32(new Vector4(0.035f, 0.030f, 0.028f, backgroundOpacity)), 4.0f);
            drawList.AddRect(windowMin, windowMax, ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, backgroundOpacity * 0.72f)), 4.0f, (ImDrawFlags)0, 1.0f);
        }

        var cursor = windowMin + new Vector2(padX, padY);
        Vector2 mainTargetHitMin;
        Vector2 mainTargetHitSize;

        void DrawMainCastBar(Vector2 rowPos, bool fullWidth) {
            var fullCastBarWidth = Math.Max(120.0f * mainScale, contentWidth - 28.0f * mainScale);
            var widthToDraw = fullWidth ? fullCastBarWidth : castBarWidth;
            var castBarPos = fullWidth
                ? rowPos + new Vector2((contentWidth - widthToDraw) * 0.5f, 0.0f)
                : new Vector2(rowPos.X + contentWidth - widthToDraw, rowPos.Y);
            DrawTargetInfoCastBar(drawList, targetInfo, castBarPos, widthToDraw, castBarHeight, mainScale);
        }

        void DrawTargetOfTarget(Vector2 hpPos) {
            if (targetInfo.TargetOfTarget is null) {
                return;
            }

            var targetOfTargetPos = new Vector2(hpPos.X + contentWidth + targetOfTargetGap, hpPos.Y + (hpBarHeight - targetOfTargetHeight) * 0.5f);
            var arrowPos = new Vector2(hpPos.X + contentWidth + Math.Max(0.0f, targetOfTargetGap - 18.0f) * 0.5f, hpPos.Y);
            DrawTargetConnector(drawList, arrowPos, targetOfTargetPos.Y, targetOfTargetHeight);
            DrawTargetOfTargetBar(drawList, targetInfo.TargetOfTarget, targetOfTargetPos, targetOfTargetWidth, targetOfTargetHeight, mainScale);
        }

        if (drawCastTop) {
            DrawMainCastBar(cursor, true);
            cursor.Y += castBarHeight + castGap;
        }

        if (statusesAboveHp) {
            if (drawStatusInMain && statusLayouts.Count > 0) {
                DrawTargetInfoStatusLayouts(statusLayouts, cursor);
                cursor.Y += statusGridHeight + statusTopGap;
            }

            var hpPos = cursor;
            DrawTargetInfoHpBar(drawList, targetInfo, hpPos, contentWidth, hpBarHeight, mainScale, this.config.CustomTargetInfoHideHpNumbers, this.config.CustomTargetInfoHideMaxHp);
            DrawTargetOfTarget(hpPos);

            cursor.Y += hpBarHeight + 2.0f * mainScale;
            var namePos = cursor;
            DrawTargetInfoHeader(drawList, targetInfo, namePos, contentWidth, nameRowHeight, mainScale);
            if (drawCastSide) {
                DrawMainCastBar(cursor, false);
            }

            cursor.Y += Math.Max(nameRowHeight, drawCastSide ? castBarHeight : 0.0f);

            if (drawCastBottom) {
                cursor.Y += castGap;
                DrawMainCastBar(cursor, true);
                cursor.Y += castBarHeight;
            }

            mainTargetHitMin = hpPos;
            mainTargetHitSize = new Vector2(contentWidth, hpBarHeight + 2.0f + nameRowHeight);
        } else {
            mainTargetHitMin = cursor;
            DrawTargetInfoHeader(drawList, targetInfo, cursor, contentWidth, nameRowHeight, mainScale);

            cursor.Y += nameRowHeight + 2.0f * mainScale;
            var hpPos = cursor;
            DrawTargetInfoHpBar(drawList, targetInfo, hpPos, contentWidth, hpBarHeight, mainScale, this.config.CustomTargetInfoHideHpNumbers, this.config.CustomTargetInfoHideMaxHp);

            if (drawCastSide) {
                var castBarPos = new Vector2(hpPos.X + contentWidth - castBarWidth, hpPos.Y - castBarHeight - 5.0f * mainScale);
                DrawTargetInfoCastBar(drawList, targetInfo, castBarPos, castBarWidth, castBarHeight, mainScale);
            }

            DrawTargetOfTarget(hpPos);

            cursor.Y += hpBarHeight;
            if (drawStatusInMain && statusLayouts.Count > 0) {
                cursor.Y += statusTopGap;
                DrawTargetInfoStatusLayouts(statusLayouts, cursor);
                cursor.Y += statusGridHeight;
            }

            if (drawCastBottom) {
                cursor.Y += castGap;
                DrawMainCastBar(cursor, true);
                cursor.Y += castBarHeight;
            }

            mainTargetHitSize = new Vector2(contentWidth, nameRowHeight + 2.0f * mainScale + hpBarHeight);
        }

        // 主目标只让名字行 + HP 条可点击，不覆盖状态图标。
        HandleTargetSelectionHitArea(targetInfo.ObjectId, mainTargetHitMin, mainTargetHitSize, 3.0f);

        ImGui.End();

        if (this.config.CustomTargetInfoSplitCastBar) {
            DrawSplitTargetInfoCastBar(flags, targetInfo);
        }

        if (this.config.CustomTargetInfoSplitStatusBar) {
            DrawSplitTargetInfoStatusBar(flags, targetInfo);
        }
    }

    private void DrawSplitTargetInfoCastBar(ImGuiWindowFlags flags, TargetInfoEntry targetInfo) {
        if (!ShouldDrawTargetCastBar(targetInfo)) {
            return;
        }

        var scale = Math.Clamp(this.config.CustomTargetInfoCastBarScale, 0.6f, 2.0f);
        var width = 240.0f * scale;
        var height = 24.0f * scale;
        var windowSize = new Vector2(width, height);

        ImGui.SetNextWindowPos(this.config.CustomTargetInfoCastBarPosition, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(windowSize);
        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration;
        if (!ImGui.Begin("AllHud 目标咏唱栏", flags)) {
            ImGui.End();
            return;
        }

        TrackTargetInfoCastBarPosition();
        DrawTargetInfoCastBar(ImGui.GetWindowDrawList(), targetInfo, ImGui.GetWindowPos(), width, height, scale);
        ImGui.End();
    }

    private static bool ShouldDrawTargetCastBar(TargetInfoEntry targetInfo) {
        if (!targetInfo.IsCasting || targetInfo.TotalCastTime <= 0.0f) {
            return false;
        }

        return targetInfo.CurrentCastTime < targetInfo.TotalCastTime;
    }

    private void DrawSplitTargetInfoStatusBar(ImGuiWindowFlags flags, TargetInfoEntry targetInfo) {
        var scale = Math.Clamp(this.config.CustomTargetInfoStatusBarScale, 0.6f, 2.0f);
        var width = 500.0f * scale;
        var iconSize = NativeStatusIconSize * scale;
        var selfAppliedIconSize = 40.0f * scale;
        var iconGap = -8.0f * scale;
        var statusTimerHeight = 18.0f * scale;
        var maxStatusRows = Math.Clamp(this.config.CustomTargetInfoStatusRows, 1, 2);
        const int statusColumns = 15;

        var orderedStatuses = GetOrderedTargetStatuses(targetInfo.Statuses);
        var layouts = BuildTargetStatusIconLayouts(orderedStatuses, width, maxStatusRows, statusColumns, selfAppliedIconSize, iconSize, iconGap, statusTimerHeight, this.config.CustomTargetInfoStatusesAboveHp, out var gridHeight);
        if (layouts.Count == 0) {
            return;
        }

        var windowSize = new Vector2(width, gridHeight);
        ImGui.SetNextWindowPos(this.config.CustomTargetInfoStatusBarPosition, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(windowSize);
        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration;
        if (!ImGui.Begin("AllHud 目标状态栏", flags)) {
            ImGui.End();
            return;
        }

        TrackTargetInfoStatusBarPosition();
        DrawTargetInfoStatusLayouts(layouts, ImGui.GetWindowPos());
        ImGui.End();
    }

    private void HandleTargetSelectionHitArea(ulong objectId, Vector2 pos, Vector2 size, float rounding) {
        if (objectId == 0 || objectId == ulong.MaxValue || size.X <= 1.0f || size.Y <= 1.0f) {
            return;
        }

        if (IsNativeContextMenuVisible()) {
            return;
        }

        if (!ImGui.IsMouseHoveringRect(pos, pos + size, false)) {
            return;
        }

        RequestNativeClickableCursor();

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
            this.combatState.SelectTargetByObjectId(objectId);
        } else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
            this.combatState.OpenNativeContextMenuByObjectId(objectId, GetTargetContextMenuPosition(ImGui.GetMousePos(), pos, size));
        }
    }

    private unsafe bool IsNativeContextMenuVisible() {
        return IsNativeAddonVisible("ContextMenu")
               || IsNativeAddonVisible("ContextIconMenu")
               || IsNativeAddonVisible("AddonContextSub");
    }

    private unsafe bool IsNativeAddonVisible(string addonName) {
        var addonPtr = this.gameGui.GetAddonByName(addonName);
        if (addonPtr.IsNull) {
            return false;
        }

        var addon = (AtkUnitBase*)addonPtr.Address;
        return addon is not null && addon->IsVisible;
    }

    private static Vector2 GetTargetContextMenuPosition(Vector2 mousePos, Vector2 targetMin, Vector2 targetSize) {
        var viewport = ImGui.GetMainViewport();
        var workMin = viewport.WorkPos;
        var workMax = viewport.WorkPos + viewport.WorkSize;
        var estimatedMenuSize = new Vector2(230.0f, 190.0f);
        var margin = 8.0f;
        var targetMax = targetMin + targetSize;
        var x = mousePos.X + margin;
        var y = mousePos.Y + margin;

        if (RectsOverlap(new Vector2(x, y), new Vector2(x + estimatedMenuSize.X, y + estimatedMenuSize.Y), targetMin - new Vector2(margin), targetMax + new Vector2(margin))) {
            var belowY = targetMax.Y + margin;
            var aboveY = targetMin.Y - estimatedMenuSize.Y - margin;
            y = belowY + estimatedMenuSize.Y <= workMax.Y ? belowY : aboveY;
        }

        x = Math.Clamp(x, workMin.X + margin, workMax.X - estimatedMenuSize.X - margin);
        y = Math.Clamp(y, workMin.Y + margin, workMax.Y - estimatedMenuSize.Y - margin);
        return SnapToPixel(new Vector2(x, y));
    }

    private static bool RectsOverlap(Vector2 minA, Vector2 maxA, Vector2 minB, Vector2 maxB) {
        return minA.X < maxB.X && maxA.X > minB.X && minA.Y < maxB.Y && maxA.Y > minB.Y;
    }

    private void RequestNativeClickableCursor() {
        if (!this.nativeCursorForced) {
            this.addonEventManager.SetCursor(AddonCursorType.Clickable);
            this.nativeCursorForced = true;
        }

        this.nativeCursorRequestedThisFrame = true;
    }

    private void ResetNativeCursorIfNeeded() {
        if (!this.nativeCursorForced) {
            return;
        }

        this.addonEventManager.ResetCursor();
        this.nativeCursorForced = false;
    }

    private static void DrawTargetConnector(ImDrawListPtr drawList, Vector2 pos, float targetBarY, float targetBarHeight) {
        var time = (float)ImGui.GetTime();
        var pulse = (MathF.Sin(time * 5.6f) + 1.0f) * 0.5f;
        var slide = pulse * 3.0f;
        var color = ImGui.GetColorU32(new Vector4(0.92f, 0.92f, 0.92f, 0.62f + pulse * 0.28f));
        var shadow = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.58f));
        var centerY = targetBarY + targetBarHeight * 0.5f;
        var first = new Vector2(pos.X + 1.0f + slide, centerY);
        var second = first + new Vector2(7.0f, 0.0f);

        void DrawTriangle(Vector2 center, uint drawColor, Vector2 offset) {
            var top = center + new Vector2(-3.0f, -5.5f) + offset;
            var right = center + new Vector2(4.0f, 0.0f) + offset;
            var bottom = center + new Vector2(-3.0f, 5.5f) + offset;
            drawList.AddTriangleFilled(top, right, bottom, drawColor);
        }

        DrawTriangle(first, shadow, new Vector2(1.0f, 1.0f));
        DrawTriangle(second, shadow, new Vector2(1.0f, 1.0f));
        DrawTriangle(first, color, Vector2.Zero);
        DrawTriangle(second, color, Vector2.Zero);
    }

    private void DrawTargetOfTargetBar(ImDrawListPtr drawList, TargetOfTargetEntry targetOfTarget, Vector2 pos, float width, float height, float textScale) {
        var hpRatio = targetOfTarget.MaxHp > 0 ? Math.Clamp(targetOfTarget.CurrentHp / (float)targetOfTarget.MaxHp, 0.0f, 1.0f) : 0.0f;
        var percentText = targetOfTarget.MaxHp > 0 ? $"{hpRatio * 100.0f:0.0}%" : string.Empty;
        var percentSize = CalcTextSize(percentText, textScale);
        var nameMaxWidth = Math.Max(24.0f, width - percentSize.X - 8.0f);
        var name = TruncateTextToWidth(targetOfTarget.Name, nameMaxWidth, textScale);

        DrawTargetHealthBar(
            drawList,
            pos,
            width,
            height,
            hpRatio,
            new Vector4(0.84f, 0.20f, 0.84f, 0.92f),
            textScale,
            leftText: name,
            centerText: string.Empty,
            rightText: percentText,
            leftInset: 5.0f * textScale,
            rightInset: 6.0f * textScale,
            centerColor: ImGui.GetColorU32(new Vector4(1.0f, 0.90f, 0.86f, 0.96f)),
            leftColor: ImGui.GetColorU32(new Vector4(1.0f, 0.82f, 1.0f, 0.98f)),
            rightColor: ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.96f)));

        HandleTargetSelectionHitArea(targetOfTarget.ObjectId, pos, new Vector2(width, height), 2.0f);
    }

    private void DrawTargetInfoHeader(ImDrawListPtr drawList, TargetInfoEntry targetInfo, Vector2 pos, float width, float height, float textScale) {
        var levelPrefix = targetInfo.Level > 0 ? $"Lv{targetInfo.Level} " : string.Empty;
        var idSuffix = targetInfo.DataId != 0 ? $" [{targetInfo.DataId}]" : string.Empty;
        var textInsetX = 4.0f * textScale;
        var name = TruncateTextToWidth(levelPrefix + targetInfo.Name + idSuffix, width - textInsetX * 2.0f, textScale);
        var nameSize = CalcTextSize(name, textScale);
        var textPos = pos + new Vector2(textInsetX, Math.Max(0.0f, (height - nameSize.Y) * 0.5f));

        DrawShadowText(drawList, textPos, ImGui.GetColorU32(new Vector4(0.96f, 0.90f, 0.82f, 0.98f)), name, GetFontSize(textScale));
    }

    private void DrawTargetInfoHpBar(ImDrawListPtr drawList, TargetInfoEntry targetInfo, Vector2 pos, float width, float height, float textScale, bool hideHpNumbers, bool hideMaxHp) {
        var hpRatio = targetInfo.MaxHp > 0 ? Math.Clamp(targetInfo.CurrentHp / (float)targetInfo.MaxHp, 0.0f, 1.0f) : 0.0f;
        var fillColor = hpRatio < 0.2f
            ? new Vector4(0.95f, 0.18f, 0.16f, 0.92f)
            : hpRatio < 0.5f
                ? new Vector4(0.82f, 0.32f, 0.15f, 0.90f)
                : new Vector4(0.78f, 0.12f, 0.13f, 0.90f);

        var hpPercent = targetInfo.MaxHp > 0 ? targetInfo.CurrentHp / (float)targetInfo.MaxHp * 100.0f : 0.0f;
        var percentText = $"{hpPercent:0.0}%";
        var hpText = string.Empty;
        if (!hideHpNumbers) {
            hpText = hideMaxHp
                ? FormatNumber(targetInfo.CurrentHp)
                : $"{FormatNumber(targetInfo.CurrentHp)} / {FormatNumber(targetInfo.MaxHp)}";
        }

        DrawTargetHealthBar(
            drawList,
            pos,
            width,
            height,
            hpRatio,
            fillColor,
            textScale,
            leftText: string.Empty,
            centerText: hpText,
            rightText: percentText,
            leftInset: 0.0f,
            rightInset: 12.0f * textScale,
            centerColor: ImGui.GetColorU32(new Vector4(1.0f, 0.90f, 0.86f, 0.96f)),
            leftColor: ImGui.GetColorU32(new Vector4(1.0f, 0.90f, 0.86f, 0.96f)),
            rightColor: ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.98f)));
    }

    private void DrawTargetHealthBar(ImDrawListPtr drawList, Vector2 pos, float width, float height, float hpRatio, Vector4 fillColor, float textScale, string leftText, string centerText, string rightText, float leftInset, float rightInset, uint centerColor, uint leftColor, uint rightColor) {
        var max = pos + new Vector2(width, height);
        var fillMax = new Vector2(pos.X + width * hpRatio, max.Y);
        DrawEpicHealthBar(drawList, pos, max, fillMax, fillColor, false);

        var fontSize = GetFontSize(textScale);
        var textVisualOffset = -MathF.Round(Math.Clamp(fontSize * 0.08f, 1.0f, 2.0f));
        if (!string.IsNullOrWhiteSpace(leftText)) {
            var leftSize = CalcTextSize(leftText, textScale);
            DrawShadowText(drawList, pos + new Vector2(leftInset, (height - leftSize.Y) * 0.5f + textVisualOffset), leftColor, leftText, fontSize);
        }

        if (!string.IsNullOrWhiteSpace(centerText)) {
            var centerSize = CalcTextSize(centerText, textScale);
            DrawShadowText(drawList, pos + new Vector2((width - centerSize.X) * 0.5f, (height - centerSize.Y) * 0.5f + textVisualOffset), centerColor, centerText, fontSize);
        }

        if (!string.IsNullOrWhiteSpace(rightText)) {
            var rightSize = CalcTextSize(rightText, textScale);
            DrawShadowText(drawList, new Vector2(max.X - rightSize.X - rightInset, pos.Y + (height - rightSize.Y) * 0.5f + textVisualOffset), rightColor, rightText, fontSize);
        }
    }

    private static void DrawCapsuleBar(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector2 fillMax, Vector4 background, Vector4 fill, Vector4 border) {
        var height = Math.Max(1.0f, max.Y - min.Y);
        var rounding = height * 0.5f;
        var shadowOffset = new Vector2(0.0f, 1.0f);

        drawList.AddRectFilled(min + shadowOffset, max + shadowOffset, ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.24f)), rounding);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(background), rounding);

        if (fillMax.X > min.X + 1.0f) {
            var clampedFillMax = new Vector2(Math.Clamp(fillMax.X, min.X, max.X), max.Y);
            drawList.AddRectFilled(min, clampedFillMax, ImGui.GetColorU32(fill), rounding);

            var fillWidth = clampedFillMax.X - min.X;
            if (fillWidth > 3.0f) {
                var highlightMax = new Vector2(clampedFillMax.X, min.Y + height * 0.42f);
                var shadeMin = new Vector2(min.X, min.Y + height * 0.58f);
                drawList.AddRectFilled(min + new Vector2(1.0f, 1.0f), highlightMax - new Vector2(1.0f, 0.0f), ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.08f)), rounding * 0.75f);
                drawList.AddRectFilled(shadeMin, clampedFillMax - new Vector2(0.0f, 1.0f), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.10f)), rounding * 0.75f);
            }
        }

        drawList.AddLine(min + new Vector2(rounding * 0.55f, 1.0f), new Vector2(max.X - rounding * 0.55f, min.Y + 1.0f), ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.06f)), 1.0f);
        drawList.AddRect(min, max, ImGui.GetColorU32(border with { W = border.W * 0.45f }), rounding, (ImDrawFlags)0, 0.7f);
    }

    private static void DrawEpicHealthBar(ImDrawListPtr drawList, Vector2 min, Vector2 max, Vector2 fillMax, Vector4 fill, bool compact) {
        var height = Math.Max(1.0f, max.Y - min.Y);
        var outerRounding = MathF.Min(4.0f, height * 0.26f);
        var innerInset = compact ? 2.0f : 2.5f;
        var innerMin = min + new Vector2(innerInset, innerInset);
        var innerMax = max - new Vector2(innerInset, innerInset);
        var innerHeight = Math.Max(1.0f, innerMax.Y - innerMin.Y);
        var innerRounding = MathF.Min(2.5f, innerHeight * 0.24f);
        var clampedFillMax = new Vector2(Math.Clamp(fillMax.X - innerInset, innerMin.X, innerMax.X), innerMax.Y);

        drawList.AddRectFilled(min + new Vector2(0.0f, 1.0f), max + new Vector2(0.0f, 1.0f), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.20f)), outerRounding);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.025f, 0.022f, 0.020f, 0.86f)), outerRounding);
        drawList.AddRect(min + new Vector2(0.5f, 0.5f), max - new Vector2(0.5f, 0.5f), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.52f)), outerRounding, (ImDrawFlags)0, 0.9f);
        drawList.AddRect(min + new Vector2(1.5f, 1.5f), max - new Vector2(1.5f, 1.5f), ImGui.GetColorU32(new Vector4(1.0f, 0.95f, 0.82f, 0.10f)), outerRounding * 0.75f, (ImDrawFlags)0, 0.6f);

        drawList.AddRectFilled(innerMin, innerMax, ImGui.GetColorU32(new Vector4(0.075f, 0.018f, 0.014f, 0.96f)), innerRounding);
        if (clampedFillMax.X > innerMin.X + 1.0f) {
            drawList.AddRectFilled(innerMin, clampedFillMax, ImGui.GetColorU32(fill), innerRounding);

            var fillWidth = clampedFillMax.X - innerMin.X;
            if (fillWidth > 4.0f) {
                var highlightMax = new Vector2(clampedFillMax.X, innerMin.Y + innerHeight * 0.34f);
                var midMin = new Vector2(innerMin.X, innerMin.Y + innerHeight * 0.36f);
                var shadeMin = new Vector2(innerMin.X, innerMin.Y + innerHeight * 0.68f);
                drawList.AddRectFilled(innerMin + new Vector2(1.0f, 1.0f), highlightMax - new Vector2(1.0f, 0.0f), ImGui.GetColorU32(new Vector4(1.0f, 0.92f, 0.74f, 0.10f)), innerRounding * 0.7f);
                drawList.AddRectFilled(midMin, new Vector2(clampedFillMax.X, shadeMin.Y), ImGui.GetColorU32(new Vector4(1.0f, 0.16f, 0.12f, compact ? 0.04f : 0.06f)), 0.0f);
                drawList.AddRectFilled(shadeMin, clampedFillMax - new Vector2(0.0f, 1.0f), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.12f)), innerRounding * 0.7f);
            }
        }

        drawList.AddLine(innerMin + new Vector2(innerRounding, 1.0f), new Vector2(innerMax.X - innerRounding, innerMin.Y + 1.0f), ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.06f)), 1.0f);
        drawList.AddLine(new Vector2(innerMin.X + innerRounding, innerMax.Y - 1.0f), new Vector2(innerMax.X - innerRounding, innerMax.Y - 1.0f), ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.22f)), 1.0f);
    }

    private void DrawTargetInfoCastBar(ImDrawListPtr drawList, TargetInfoEntry targetInfo, Vector2 pos, float width, float height, float textScale) {
        var max = pos + new Vector2(width, height);
        var progress = targetInfo.TotalCastTime > 0.0f ? Math.Clamp(targetInfo.CurrentCastTime / targetInfo.TotalCastTime, 0.0f, 1.0f) : 0.0f;
        var fillMax = new Vector2(pos.X + width * progress, max.Y);
        var fill = new Vector4(0.76f, 0.55f, 0.20f, 0.96f);
        var border = new Vector4(0.20f, 0.14f, 0.06f, 0.88f);

        DrawCapsuleBar(drawList, pos, max, fillMax, new Vector4(0.018f, 0.014f, 0.010f, 0.84f), fill, border);

        var remain = Math.Max(0.0f, targetInfo.TotalCastTime - targetInfo.CurrentCastTime);
        var timeText = remain > 0.0f ? $"{remain:00.00}" : string.Empty;
        var timeSize = CalcTextSize(timeText, textScale);
        var nameMaxWidth = Math.Max(30.0f * textScale, width - timeSize.X - 12.0f * textScale);
        var name = TruncateTextToWidth(string.IsNullOrWhiteSpace(targetInfo.CastActionName) ? $"Action {targetInfo.CastActionId}" : targetInfo.CastActionName, nameMaxWidth, textScale);

        var nameSize = CalcTextSize(name, textScale);
        var namePos = new Vector2(pos.X + 6.0f * textScale, pos.Y + (height - nameSize.Y) * 0.5f);
        DrawShadowText(drawList, namePos, ImGui.GetColorU32(new Vector4(0.96f, 0.97f, 1.0f, 0.98f)), name, GetFontSize(textScale));
        if (!string.IsNullOrWhiteSpace(timeText)) {
            var timePos = new Vector2(max.X - timeSize.X - 6.0f * textScale, pos.Y + (height - timeSize.Y) * 0.5f);
            DrawShadowText(drawList, timePos, ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 1.0f, 0.96f)), timeText, GetFontSize(textScale));
        }
    }

    private static IReadOnlyList<TargetStatusIconLayout> BuildTargetStatusIconLayouts(
        IReadOnlyList<StatusEntry> statuses,
        float width,
        int maxRows,
        int maxColumns,
        float selfAppliedIconSize,
        float otherIconSize,
        float iconGap,
        float statusTimerHeight,
        bool bottomUp,
        out float height) {
        var layouts = new List<TargetStatusIconLayout>();
        height = 0.0f;
        if (statuses.Count == 0 || width <= 1.0f || maxRows <= 0) {
            return layouts;
        }

        var rows = new List<(List<TargetStatusIconLayout> Layouts, float Height)>();
        var currentRow = new List<TargetStatusIconLayout>();
        var row = 0;
        var x = 0.0f;
        var rowHeight = 0.0f;
        var column = 0;

        void CommitRow() {
            if (currentRow.Count == 0) {
                return;
            }

            rows.Add((currentRow, rowHeight));
            currentRow = [];
            x = 0.0f;
            rowHeight = 0.0f;
            column = 0;
            row++;
        }

        for (var index = 0; index < statuses.Count; index++) {
            var status = statuses[index];
            var iconSize = status.IsSelfApplied ? selfAppliedIconSize : otherIconSize;
            var visualSize = GetTargetStatusVisualIconSize(status, iconSize);
            var itemHeight = visualSize + statusTimerHeight;
            if (column >= maxColumns || (x > 0.0f && x + visualSize > width)) {
                CommitRow();

                if (row >= maxRows) {
                    break;
                }
            }

            var layoutX = x - (iconSize - visualSize) * 0.5f;
            currentRow.Add(new TargetStatusIconLayout(status, new Vector2(layoutX, 0.0f), iconSize, layouts.Count + currentRow.Count));
            x += visualSize + iconGap;
            rowHeight = Math.Max(rowHeight, itemHeight);
            column++;
        }

        CommitRow();

        var rowGap = Math.Max(0.0f, iconGap);
        height = rows.Sum(rowEntry => rowEntry.Height) + Math.Max(0, rows.Count - 1) * rowGap;

        var y = bottomUp ? height : 0.0f;
        foreach (var rowEntry in rows) {
            y = bottomUp ? y - rowEntry.Height : y;
            layouts.AddRange(rowEntry.Layouts.Select(layout => layout with {
                Offset = new Vector2(layout.Offset.X, y),
            }));
            y = bottomUp ? y - rowGap : y + rowEntry.Height + rowGap;
        }

        return layouts;
    }

    private void DrawTargetInfoStatusLayouts(IReadOnlyList<TargetStatusIconLayout> layouts, Vector2 pos) {
        foreach (var layout in layouts) {
            DrawTargetInfoStatusIcon(layout.Status, pos + layout.Offset, layout.Size, layout.Index);
        }
    }

    private void DrawTargetInfoStatuses(IReadOnlyList<StatusEntry> statuses, Vector2 pos, int columns, float iconSize, float iconGap, float rowHeight, int indexOffset) {
        for (var index = 0; index < statuses.Count; index++) {
            var row = index / columns;
            var column = index % columns;
            var min = pos + new Vector2(column * (iconSize + iconGap), row * (rowHeight + iconGap));
            DrawTargetInfoStatusIcon(statuses[index], min, iconSize, indexOffset + index);
        }
    }

    private void DrawTargetInfoStatusIcon(StatusEntry status, Vector2 min, float size, int index) {
        var max = min + new Vector2(size, size);
        var visualSize = GetTargetStatusVisualIconSize(status, size);
        var visualMin = min + new Vector2((size - visualSize) * 0.5f, 0.0f);
        var visualMax = visualMin + new Vector2(visualSize, visualSize);
        var drawList = ImGui.GetWindowDrawList();
        if (!DrawGameIconImage(drawList, status.IconId, visualMin, visualMax)) {
            drawList.AddRectFilled(visualMin, visualMax, ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 0.82f)), 2.0f);
        }
        if (ShouldShowStatusTimer(status)) {
            var textColor = status.IsSelfApplied
                ? ImGui.GetColorU32(this.config.SelfAppliedTimerColor)
                : ImGui.GetColorU32(this.config.OtherAppliedTimerColor);
            var edgeColor = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.96f));
            DrawTargetStatusTimerText(FormatTargetStatusTime(status.RemainingSeconds), visualMin, visualMax, textColor, edgeColor, visualSize);
        }

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton($"target_status_{index}_{status.StatusId}", new Vector2(size, size));
        if (ImGui.IsItemHovered()) {
            DrawStatusTooltip(status, false);
        }
    }

    private static float GetTargetStatusVisualIconSize(StatusEntry status, float layoutSize) {
        return layoutSize + (status.IsSelfApplied ? 4.0f : 0.0f);
    }

    private void DrawTargetStatusTimerText(string text, Vector2 min, Vector2 max, uint textColor, uint edgeColor, float iconSize) {
        if (string.IsNullOrWhiteSpace(text)) {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var baseFontSize = ImGui.GetFontSize();
        var fontSize = MathF.Round(Math.Clamp(iconSize * 0.5f, baseFontSize * 0.92f, baseFontSize * 1.18f));
        var textScale = fontSize / Math.Max(1.0f, baseFontSize);
        using var font = PushHudFont(fontSize);
        var textSize = ImGui.CalcTextSize(text);
        var pos = new Vector2(
            MathF.Round(min.X + ((max.X - min.X) - textSize.X) * 0.5f),
            MathF.Round(max.Y - fontSize * 0.12f));
        const float outlineOffset = 1.0f;
        drawList.AddText(pos + new Vector2(-outlineOffset, -outlineOffset), edgeColor, text);
        drawList.AddText(pos + new Vector2(-outlineOffset, 0.0f), edgeColor, text);
        drawList.AddText(pos + new Vector2(-outlineOffset, outlineOffset), edgeColor, text);
        drawList.AddText(pos + new Vector2(0.0f, -outlineOffset), edgeColor, text);
        drawList.AddText(pos + new Vector2(0.0f, outlineOffset), edgeColor, text);
        drawList.AddText(pos + new Vector2(outlineOffset, -outlineOffset), edgeColor, text);
        drawList.AddText(pos + new Vector2(outlineOffset, 0.0f), edgeColor, text);
        drawList.AddText(pos + new Vector2(outlineOffset, outlineOffset), edgeColor, text);
        drawList.AddText(pos + new Vector2(1.0f, 0.0f), textColor, text);
        drawList.AddText(pos, textColor, text);
    }

    private string FormatTargetStatusTime(float seconds) {
        var totalSeconds = Math.Max(0, (int)MathF.Ceiling(seconds));
        if (this.targetStatusTimeTextCache.TryGetValue(totalSeconds, out var cachedText)) {
            return cachedText;
        }

        string text;
        if (totalSeconds >= 60) {
            text = $"{Math.Max(1, (int)MathF.Ceiling(totalSeconds / 60.0f))}m";
        } else {
            text = totalSeconds.ToString();
        }

        this.targetStatusTimeTextCache[totalSeconds] = text;
        return text;
    }

    private void TrackTargetInfoPosition() {
        if (this.config.TargetInfoLocked) {
            return;
        }

        var currentPosition = ImGui.GetWindowPos();
        if (currentPosition != this.config.CustomTargetInfoPosition) {
            this.config.CustomTargetInfoPosition = currentPosition;
            this.targetInfoPositionSaveDueAt = DateTime.UtcNow.Add(OverlayPositionSaveDelay);
        }

        if (this.targetInfoPositionSaveDueAt is not { } saveDueAt
            || DateTime.UtcNow < saveDueAt
            || this.config.CustomTargetInfoPosition == this.lastSavedTargetInfoPosition) {
            return;
        }

        this.saveConfig();
        this.lastSavedTargetInfoPosition = this.config.CustomTargetInfoPosition;
        this.targetInfoPositionSaveDueAt = null;
    }

    private void TrackTargetInfoCastBarPosition() {
        if (this.config.TargetInfoLocked) {
            return;
        }

        var currentPosition = ImGui.GetWindowPos();
        if (currentPosition != this.config.CustomTargetInfoCastBarPosition) {
            this.config.CustomTargetInfoCastBarPosition = currentPosition;
            this.targetInfoCastBarPositionSaveDueAt = DateTime.UtcNow.Add(OverlayPositionSaveDelay);
        }

        if (this.targetInfoCastBarPositionSaveDueAt is not { } saveDueAt
            || DateTime.UtcNow < saveDueAt
            || this.config.CustomTargetInfoCastBarPosition == this.lastSavedTargetInfoCastBarPosition) {
            return;
        }

        this.saveConfig();
        this.lastSavedTargetInfoCastBarPosition = this.config.CustomTargetInfoCastBarPosition;
        this.targetInfoCastBarPositionSaveDueAt = null;
    }

    private void TrackTargetInfoStatusBarPosition() {
        if (this.config.TargetInfoLocked) {
            return;
        }

        var currentPosition = ImGui.GetWindowPos();
        if (currentPosition != this.config.CustomTargetInfoStatusBarPosition) {
            this.config.CustomTargetInfoStatusBarPosition = currentPosition;
            this.targetInfoStatusBarPositionSaveDueAt = DateTime.UtcNow.Add(OverlayPositionSaveDelay);
        }

        if (this.targetInfoStatusBarPositionSaveDueAt is not { } saveDueAt
            || DateTime.UtcNow < saveDueAt
            || this.config.CustomTargetInfoStatusBarPosition == this.lastSavedTargetInfoStatusBarPosition) {
            return;
        }

        this.saveConfig();
        this.lastSavedTargetInfoStatusBarPosition = this.config.CustomTargetInfoStatusBarPosition;
        this.targetInfoStatusBarPositionSaveDueAt = null;
    }

    private void DrawShadowText(ImDrawListPtr drawList, Vector2 pos, uint color, string text, float fontSize = 0.0f) {
        var edge = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 0.0f, 0.92f));
        if (fontSize <= 0.0f) {
            pos = SnapToPixel(pos);
            drawList.AddText(pos + new Vector2(-1.0f, 0.0f), edge, text);
            drawList.AddText(pos + new Vector2(1.0f, 0.0f), edge, text);
            drawList.AddText(pos + new Vector2(0.0f, -1.0f), edge, text);
            drawList.AddText(pos + new Vector2(0.0f, 1.0f), edge, text);
            drawList.AddText(pos + new Vector2(1.0f, 1.0f), edge, text);
            drawList.AddText(pos, color, text);
            return;
        }

        using var font = PushHudFont(fontSize);
        pos = SnapToPixel(pos);
        var edgeOffset = Math.Clamp(MathF.Round(fontSize / Math.Max(1.0f, ImGui.GetFontSize())), 1.0f, 2.0f);
        drawList.AddText(pos + new Vector2(-edgeOffset, 0.0f), edge, text);
        drawList.AddText(pos + new Vector2(edgeOffset, 0.0f), edge, text);
        drawList.AddText(pos + new Vector2(0.0f, -edgeOffset), edge, text);
        drawList.AddText(pos + new Vector2(0.0f, edgeOffset), edge, text);
        drawList.AddText(pos + new Vector2(edgeOffset, edgeOffset), edge, text);
        drawList.AddText(pos, color, text);
    }

    private static float GetFontSize(float scale) {
        return ImGui.GetFontSize() * Math.Clamp(scale, 0.6f, 2.0f);
    }

    private Vector2 CalcTextSize(string text, float scale) {
        using var font = PushHudFont(GetFontSize(scale));
        return ImGui.CalcTextSize(text);
    }

    private string TruncateTextToWidth(string text, float maxWidth, float scale = 1.0f) {
        if (CalcTextSize(text, scale).X <= maxWidth) {
            return text;
        }

        const string ellipsis = "...";
        var trimmed = text;
        while (trimmed.Length > 0 && CalcTextSize(trimmed + ellipsis, scale).X > maxWidth) {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length == 0 ? ellipsis : trimmed + ellipsis;
    }
}

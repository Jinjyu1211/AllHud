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
    // 主栏自适应宽度时，按 0~1 的比例在可用空闲空间内放置。0=最左，0.5=居中，1=最右。
    private float GetTaskBarHorizontalOffsetPixels(float workWidth, float width) {
        var freeSpace = Math.Max(0.0f, workWidth - width);
        var offset = Math.Clamp(this.config.TaskBarHorizontalOffset, 0.0f, 1.0f);
        return (offset - 0.5f) * freeSpace;
    }

    private void DrawTaskBarWindow(ImGuiWindowFlags flags) {
        if (!this.config.ShowTaskBar) {
            return;
        }
        var scale = Math.Clamp(this.config.TaskBarScale, 0.6f, 2.0f);
        var opacity = Math.Clamp(this.config.TaskBarOpacity, 0.15f, 1.0f);
        var mainPadding = new Vector2(14.0f * scale, 9.0f * scale);
        var mainItemSpacing = 14.0f * scale;
        var dtrPadding = new Vector2(16.0f * scale, 7.0f * scale);
        var dtrSpacing = 8.0f * scale;
        var dtrItemHeight = 34.0f * scale;
        var dtrOuterPadding = new Vector2(8.0f * scale, 5.0f * scale);
        var rowGap = 7.0f * scale;
        var viewport = ImGui.GetMainViewport();
        var edge = this.config.TaskBarEdge == 1 ? TaskBarEdge.Bottom : TaskBarEdge.Top;
        var stretchToEdges = this.config.TaskBarStretchToEdges;
        var dtrItems = BuildDtrTaskBarItems();
        var leftItems = new List<TaskBarItem>();
        var centerItems = new List<TaskBarItem>();
        var rightItems = new List<TaskBarItem>();
        List<TaskBarItem> localItems = stretchToEdges
            ? []
            : BuildLocalTaskBarItems(dtrItems);
        if (stretchToEdges) {
            leftItems = BuildTaskBarItemsForOrder(this.config.TaskBarLeftComponentOrder, dtrItems);
            centerItems = BuildTaskBarItemsForOrder(this.config.TaskBarCenterComponentOrder, dtrItems);
            rightItems = BuildTaskBarItemsForOrder(this.config.TaskBarRightComponentOrder, dtrItems);
            localItems.AddRange(leftItems);
            localItems.AddRange(centerItems);
            localItems.AddRange(rightItems);
        }
        var taskBarFontSize = TaskBarBaseFontSize * scale;
        if (!IsHudFontReady(taskBarFontSize) || !AreTaskBarItemFontsReady(localItems, taskBarFontSize)) {
            return;
        }

        if (this.config.TaskBarServerInfoBarMode == 1 && dtrItems.Count > 0) {
            dtrItems = [];
        }
        if (localItems.Count == 0 && dtrItems.Count == 0) {
            var emptyItem = new TaskBarItem("主栏组件未启用", string.Empty, null);
            if (stretchToEdges) {
                centerItems.Add(emptyItem);
            }

            localItems.Add(emptyItem);
        }

        using var taskBarFont = PushTaskBarFont(scale);
        var leftMetrics = CalculateTaskBarMainRowMetrics(leftItems, mainPadding, mainItemSpacing, scale);
        var centerMetrics = CalculateTaskBarMainRowMetrics(centerItems, mainPadding, mainItemSpacing, scale);
        var rightMetrics = CalculateTaskBarMainRowMetrics(rightItems, mainPadding, mainItemSpacing, scale);
        var mainMetrics = stretchToEdges
            ? new TaskBarRowMetrics(SnapToPixel(viewport.WorkSize.X), MathF.Max(leftMetrics.Height, MathF.Max(centerMetrics.Height, rightMetrics.Height)))
            : CalculateTaskBarMainRowMetrics(localItems, mainPadding, mainItemSpacing, scale);
        var dtrMetrics = CalculateTaskBarDtrRowMetrics(dtrItems, dtrPadding, dtrSpacing, dtrItemHeight, dtrOuterPadding, scale);
        var contentWidth = Math.Max(mainMetrics.Width, dtrMetrics.Width);
        var visibleRows = (mainMetrics.Height > 0.0f ? 1 : 0) + (dtrMetrics.Width > 0.0f ? 1 : 0);
        var contentHeight = mainMetrics.Height + dtrMetrics.Height + (visibleRows > 1 ? rowGap : 0.0f);
        var windowInset = 0.0f;
        var width = stretchToEdges ? viewport.WorkSize.X : contentWidth + windowInset * 2.0f;
        var height = contentHeight + windowInset * 2.0f;
        var windowSize = SnapToPixel(new Vector2(width, height));
        var windowX = stretchToEdges
            ? viewport.WorkPos.X
            : viewport.WorkPos.X + Math.Max(0.0f, (viewport.WorkSize.X - width) * 0.5f) + GetTaskBarHorizontalOffsetPixels(viewport.WorkSize.X, width);
        var windowPos = SnapToPixel(edge switch {
            TaskBarEdge.Bottom => new Vector2(windowX, viewport.WorkPos.Y + viewport.WorkSize.Y - height),
            _ => new Vector2(windowX, viewport.WorkPos.Y),
        });

        ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        if (!ImGui.Begin("AllHud 主栏", flags)) {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var windowMin = ImGui.GetWindowPos();
        var centeredX = SnapToPixel(windowMin.X + Math.Max(windowInset, (windowSize.X - contentWidth) * 0.5f));
        var contentX = centeredX;
        var topY = SnapToPixel(windowMin.Y + windowInset);
        var mainFirst = edge != TaskBarEdge.Bottom;

        void DrawMainRowBackground(Vector2 rowPos, TaskBarRowMetrics rowMetrics) {
            if (rowMetrics.Width <= 0.0f || rowMetrics.Height <= 0.0f) {
                return;
            }

            rowPos = SnapToPixel(rowPos);
            var rowBgMin = rowPos;
            var rowBgMax = rowPos + new Vector2(rowMetrics.Width, rowMetrics.Height);
            rowBgMin = SnapToPixel(rowBgMin);
            rowBgMax = SnapToPixel(rowBgMax);
            var rounding = 4.0f * scale;
            drawList.AddRectFilled(rowBgMin + new Vector2(0.0f, 1.0f * scale), rowBgMax + new Vector2(0.0f, 1.0f * scale), ImGui.GetColorU32(new Vector4(0.38f, 0.18f, 0.28f, opacity * 0.12f)), rounding);
            drawList.AddRectFilled(rowBgMin, rowBgMax, ImGui.GetColorU32(new Vector4(1.0f, 0.84f, 0.92f, opacity * 0.92f)), rounding);
            drawList.AddRectFilledMultiColor(
                rowBgMin + new Vector2(1.0f * scale, 1.0f * scale),
                rowBgMax - new Vector2(1.0f * scale, Math.Max(1.0f, rowMetrics.Height * 0.42f)),
                ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.42f)),
                ImGui.GetColorU32(new Vector4(0.97f, 0.88f, 1.0f, opacity * 0.30f)),
                ImGui.GetColorU32(new Vector4(0.97f, 0.88f, 1.0f, opacity * 0.06f)),
                ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.08f)));
            drawList.AddRect(rowBgMin, rowBgMax, ImGui.GetColorU32(new Vector4(0.94f, 0.58f, 0.74f, opacity * 0.82f)), rounding, (ImDrawFlags)0, 1.0f * scale);
        }

        void DrawMainRow(Vector2 rowPos, IReadOnlyList<TaskBarItem> rowItems, TaskBarRowMetrics rowMetrics, string idPrefix, bool drawBackground = true) {
            if (rowItems.Count == 0 || rowMetrics.Width <= 0.0f) {
                return;
            }

            rowPos = SnapToPixel(rowPos);
            if (drawBackground) {
                DrawMainRowBackground(rowPos, rowMetrics);
            }

            var textX = rowPos.X + mainPadding.X;
            var textColor = ImGui.GetColorU32(new Vector4(0.20f, 0.13f, 0.16f, opacity));
            for (var index = 0; index < rowItems.Count; index++) {
                var item = rowItems[index];
                var onClick = item.OnClick;
                var textSize = CalcTaskBarItemLayoutSize(item, scale);
                var drawTextSize = CalcTaskBarItemDrawTextSize(item, scale);
                var iconLike = IsTaskBarIconLikeItem(item);
                var textPos = SnapToPixel(new Vector2(textX + Math.Max(0.0f, (textSize.X - drawTextSize.X) * 0.5f), rowPos.Y + Math.Max(0.0f, (rowMetrics.Height - drawTextSize.Y) * 0.5f)));
                var cardHeight = MathF.Round(Math.Min(rowMetrics.Height - 8.0f * scale, TaskBarPluginShortcutIconSize * scale));
                var itemMin = iconLike
                    ? SnapToPixel(new Vector2(textX, rowPos.Y + Math.Max(0.0f, (rowMetrics.Height - textSize.Y) * 0.5f)))
                    : SnapToPixel(new Vector2(textX - 4.0f * scale, rowPos.Y + Math.Max(0.0f, (rowMetrics.Height - cardHeight) * 0.5f)));
                var itemSize = iconLike
                    ? SnapToPixel(textSize)
                    : SnapToPixel(new Vector2(textSize.X + 8.0f * scale, cardHeight));
                ImGui.SetCursorScreenPos(itemMin);
                ImGui.InvisibleButton($"##taskbar_local_item_{idPrefix}_{index}", itemSize);
                var hovered = ImGui.IsItemHovered();
                var active = ImGui.IsItemActive();
                DrawTaskBarItemCard(drawList, itemMin, itemSize, index, hovered, active, onClick is not null, iconLike, opacity, scale);
                if (hovered && onClick is not null) {
                    if (!string.IsNullOrWhiteSpace(item.Tooltip)) {
                        DrawTaskBarTooltip(item.Tooltip);
                    }

                    if (item.AdjustVolumeOnWheel) {
                        var wheel = ImGui.GetIO().MouseWheel;
                        if (Math.Abs(wheel) > 0.001f) {
                            AdjustMasterVolume(wheel > 0.0f ? 5 : -5);
                        }
                    }
                }

                if (onClick is not null && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                    onClick.Invoke(CreateDtrInteractionEvent(MouseClickType.Left));
                    OpenTaskBarItemPopup(item);
                }

                if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                    var rightClick = item.OnRightClick ?? onClick;
                    rightClick?.Invoke(CreateDtrInteractionEvent(MouseClickType.Right));
                }

                if (item.DrawIcon != TaskBarDrawIcon.None) {
                    DrawTaskBarCustomIcon(drawList, textPos, drawTextSize.X, item.DrawIcon, opacity, scale);
                } else if (item.IsDalamudIcon) {
                    DrawTaskBarDalamudIcon(drawList, textPos, drawTextSize.X, opacity, scale);
                } else if (!string.IsNullOrWhiteSpace(item.PluginShortcutInternalName)) {
                    DrawPluginShortcutIcon(item, textPos, drawTextSize.X, scale);
                } else if (item.GameIconId != 0) {
                    DrawTaskBarGameIconItem(drawList, textPos, item, textColor, opacity, scale);
                } else {
                    using (PushTaskBarItemFont(item, scale)) {
                        DrawTaskBarText(drawList, textPos, textColor, item.Text, opacity, scale);
                    }
                }
                textX += textSize.X;
                if (index < rowItems.Count - 1) {
                    textX += mainItemSpacing;
                }
            }
        }

        void DrawDtrItem(TaskBarItem item, int index) {
            var onClick = item.OnClick;
            var isClickable = onClick is not null;
            var textSize = ImGui.CalcTextSize(item.Text);
            var itemSize = SnapToPixel(new Vector2(textSize.X + dtrPadding.X * 2.0f, dtrItemHeight));
            var itemMin = SnapToPixel(ImGui.GetCursorScreenPos());
            ImGui.InvisibleButton($"##taskbar_item_{index}", itemSize);
            var hovered = ImGui.IsItemHovered();
            var active = ImGui.IsItemActive();
            if (hovered) {
                if (!string.IsNullOrWhiteSpace(item.Tooltip)) {
                    DrawTaskBarTooltip(item.Tooltip);
                }
            }

            if (onClick is not null && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                onClick.Invoke(CreateDtrInteractionEvent(MouseClickType.Left));
            }

            if (onClick is not null && ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                onClick.Invoke(CreateDtrInteractionEvent(MouseClickType.Right));
            }

            var itemMax = SnapToPixel(itemMin + itemSize);
            var rounding = 6.0f * scale;
            var backgroundColor = isClickable
                ? new Vector4(0.97f, 0.91f, 1.0f, opacity * 0.88f)
                : new Vector4(1.0f, 0.94f, 0.97f, opacity * 0.86f);
            var borderColor = isClickable
                ? new Vector4(0.62f, 0.50f, 0.92f, opacity * 0.68f)
                : new Vector4(0.96f, 0.58f, 0.74f, opacity * 0.64f);
            if (!isClickable && index % 2 == 1) {
                backgroundColor = new Vector4(1.0f, 0.91f, 0.96f, opacity * 0.86f);
            }

            if (hovered) {
                backgroundColor = isClickable
                    ? new Vector4(0.99f, 0.94f, 1.0f, opacity * 0.96f)
                    : new Vector4(1.0f, 0.97f, 0.99f, opacity * 0.94f);
                borderColor = isClickable
                    ? new Vector4(0.55f, 0.43f, 0.92f, opacity * 0.88f)
                    : new Vector4(0.98f, 0.48f, 0.70f, opacity * 0.80f);
            }

            if (active) {
                backgroundColor = isClickable
                    ? new Vector4(0.90f, 0.82f, 0.98f, opacity * 0.98f)
                    : new Vector4(1.0f, 0.88f, 0.94f, opacity * 0.98f);
            }

            drawList.AddRectFilled(itemMin, itemMax, ImGui.GetColorU32(backgroundColor), rounding);
            drawList.AddRect(itemMin, itemMax, ImGui.GetColorU32(borderColor), rounding, (ImDrawFlags)0, 1.0f * scale);
            var textPos = SnapToPixel(new Vector2(itemMin.X + dtrPadding.X, itemMin.Y + Math.Max(0.0f, (dtrItemHeight - textSize.Y) * 0.5f)));
            var textColor = isClickable
                ? ImGui.GetColorU32(new Vector4(0.24f, 0.16f, 0.30f, opacity))
                : ImGui.GetColorU32(new Vector4(0.22f, 0.14f, 0.16f, opacity));
            DrawTaskBarText(drawList, textPos, textColor, item.Text, opacity, scale);
        }

        void DrawDtrRow(Vector2 rowPos) {
            if (dtrItems.Count == 0 || dtrMetrics.Width <= 0.0f) {
                return;
            }

            rowPos = SnapToPixel(rowPos);
            var drawMetrics = CalculateTaskBarDtrRowMetrics(dtrItems, dtrPadding, dtrSpacing, dtrItemHeight, dtrOuterPadding, scale, false);
            var rowBgMin = SnapToPixel(rowPos + new Vector2(Math.Max(0.0f, (dtrMetrics.Width - drawMetrics.Width) * 0.5f), 0.0f));
            var rowBgMax = SnapToPixel(rowBgMin + new Vector2(drawMetrics.Width, dtrMetrics.Height));
            var panelRounding = 8.0f * scale;
            drawList.AddRectFilled(rowBgMin + new Vector2(0.0f, 2.0f * scale), rowBgMax + new Vector2(0.0f, 2.0f * scale), ImGui.GetColorU32(new Vector4(0.32f, 0.12f, 0.22f, opacity * 0.16f)), panelRounding);
            drawList.AddRectFilled(rowBgMin, rowBgMax, ImGui.GetColorU32(new Vector4(1.0f, 0.84f, 0.92f, opacity * 0.74f)), panelRounding);
            drawList.AddRectFilledMultiColor(
                rowBgMin + new Vector2(1.0f * scale, 1.0f * scale),
                rowBgMax - new Vector2(1.0f * scale, Math.Max(1.0f, dtrMetrics.Height * 0.45f)),
                ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.48f)),
                ImGui.GetColorU32(new Vector4(0.95f, 0.86f, 1.0f, opacity * 0.42f)),
                ImGui.GetColorU32(new Vector4(0.95f, 0.86f, 1.0f, opacity * 0.08f)),
                ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.10f)));
            drawList.AddRect(rowBgMin, rowBgMax, ImGui.GetColorU32(new Vector4(0.94f, 0.54f, 0.74f, opacity * 0.82f)), panelRounding, (ImDrawFlags)0, 1.0f * scale);
            drawList.AddLine(rowBgMin + new Vector2(panelRounding, 1.0f * scale), new Vector2(rowBgMax.X - panelRounding, rowBgMin.Y + 1.0f * scale), ImGui.GetColorU32(new Vector4(1.0f, 0.98f, 1.0f, opacity * 0.54f)), 1.0f * scale);

            ImGui.SetCursorScreenPos(SnapToPixel(rowBgMin + dtrOuterPadding));
            for (var index = 0; index < dtrItems.Count; index++) {
                if (index > 0) {
                    ImGui.SameLine(0.0f, dtrSpacing);
                }

                DrawDtrItem(dtrItems[index], index);
            }
        }

        void DrawMainSections(Vector2 mainPos) {
            if (!stretchToEdges) {
                DrawMainRow(mainPos, localItems, mainMetrics, "adaptive");
                return;
            }

            var rowHeight = mainMetrics.Height;
            DrawMainRowBackground(new Vector2(windowMin.X, mainPos.Y), new TaskBarRowMetrics(windowSize.X, rowHeight));

            if (leftItems.Count > 0 && leftMetrics.Width > 0.0f) {
                DrawMainRow(new Vector2(windowMin.X, mainPos.Y), leftItems, new TaskBarRowMetrics(leftMetrics.Width, rowHeight), "left", false);
            }

            if (centerItems.Count > 0 && centerMetrics.Width > 0.0f) {
                DrawMainRow(new Vector2(windowMin.X + Math.Max(0.0f, (windowSize.X - centerMetrics.Width) * 0.5f), mainPos.Y), centerItems, new TaskBarRowMetrics(centerMetrics.Width, rowHeight), "center", false);
            }

            if (rightItems.Count > 0 && rightMetrics.Width > 0.0f) {
                DrawMainRow(new Vector2(windowMin.X + Math.Max(0.0f, windowSize.X - rightMetrics.Width), mainPos.Y), rightItems, new TaskBarRowMetrics(rightMetrics.Width, rowHeight), "right", false);
            }
        }

        if (mainFirst) {
            var mainPos = SnapToPixel(new Vector2(contentX + Math.Max(0.0f, (contentWidth - mainMetrics.Width) * 0.5f), topY));
            DrawMainSections(mainPos);
            var dtrPos = SnapToPixel(new Vector2(contentX + Math.Max(0.0f, (contentWidth - dtrMetrics.Width) * 0.5f), topY + mainMetrics.Height + (mainMetrics.Width > 0.0f && dtrMetrics.Width > 0.0f ? rowGap : 0.0f)));
            DrawDtrRow(dtrPos);
        } else {
            var dtrPos = SnapToPixel(new Vector2(contentX + Math.Max(0.0f, (contentWidth - dtrMetrics.Width) * 0.5f), topY));
            DrawDtrRow(dtrPos);
            var mainPos = SnapToPixel(new Vector2(contentX + Math.Max(0.0f, (contentWidth - mainMetrics.Width) * 0.5f), topY + dtrMetrics.Height + (mainMetrics.Width > 0.0f && dtrMetrics.Width > 0.0f ? rowGap : 0.0f)));
            DrawMainSections(mainPos);
        }

        DrawMainMenuPopup(opacity, scale);
        DrawQuickMenuPopup(opacity, scale);
        DrawVolumePopup(opacity, scale);
        DrawPluginListPopup(opacity, scale);
        DrawCoordinatesPopup(opacity, scale);
        DrawGearsetSwitcherPopup(opacity, scale);
        DrawCurrencyPopup(opacity, scale);

        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private static void DrawTaskBarItemCard(ImDrawListPtr drawList, Vector2 itemMin, Vector2 itemSize, int index, bool hovered, bool active, bool clickable, bool iconLike, float opacity, float scale) {
        var itemMax = SnapToPixel(itemMin + itemSize);
        var rounding = MathF.Min(6.0f * scale, MathF.Max(3.0f, itemSize.Y * 0.22f));
        var even = index % 2 == 0;
        var backgroundColor = iconLike
            ? new Vector4(0.98f, 0.90f, 1.0f, opacity * 0.48f)
            : even
                ? new Vector4(1.0f, 0.93f, 0.97f, opacity * 0.50f)
                : new Vector4(1.0f, 0.88f, 0.95f, opacity * 0.46f);
        var borderColor = iconLike
            ? new Vector4(0.66f, 0.48f, 0.92f, opacity * 0.42f)
            : even
                ? new Vector4(0.94f, 0.55f, 0.74f, opacity * 0.38f)
                : new Vector4(0.88f, 0.46f, 0.68f, opacity * 0.36f);
        if (hovered) {
            backgroundColor = iconLike
                ? new Vector4(1.0f, 0.94f, 1.0f, opacity * 0.78f)
                : new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.76f);
            borderColor = iconLike
                ? new Vector4(0.58f, 0.38f, 0.90f, opacity * 0.70f)
                : new Vector4(0.96f, 0.45f, 0.70f, opacity * 0.66f);
        }

        if (active) {
            backgroundColor = iconLike
                ? new Vector4(0.92f, 0.84f, 0.98f, opacity * 0.88f)
                : new Vector4(1.0f, 0.86f, 0.94f, opacity * 0.88f);
        }

        drawList.AddRectFilled(itemMin, itemMax, ImGui.GetColorU32(backgroundColor), rounding);
        drawList.AddLine(itemMin + new Vector2(rounding, 1.0f * scale), new Vector2(itemMax.X - rounding, itemMin.Y + 1.0f * scale), ImGui.GetColorU32(new Vector4(1.0f, 0.99f, 1.0f, opacity * 0.36f)), Math.Max(1.0f, scale));
        drawList.AddRect(itemMin, itemMax, ImGui.GetColorU32(borderColor), rounding, (ImDrawFlags)0, Math.Max(1.0f, scale));
    }

    private static void DrawTaskBarText(ImDrawListPtr drawList, Vector2 pos, uint color, string text, float opacity, float scale) {
        var shadow = ImGui.GetColorU32(new Vector4(1.0f, 0.97f, 0.99f, opacity * 0.55f));
        var snappedPos = SnapToPixel(pos);
        var lineHeight = ImGui.GetTextLineHeight();
        if (text.IndexOf('\n', StringComparison.Ordinal) < 0) {
            drawList.AddText(snappedPos + new Vector2(0.0f, Math.Max(1.0f, SnapToPixel(scale * 0.5f))), shadow, text);
            drawList.AddText(snappedPos, color, text);
            return;
        }

        var multilineWidth = CalcMultilineTextSize(text).X;
        var lineIndex = 0;
        var start = 0;
        while (start <= text.Length) {
            var newlineIndex = text.IndexOf('\n', start);
            var end = newlineIndex < 0 ? text.Length : newlineIndex;
            var line = text[start..end];
            var lineWidth = ImGui.CalcTextSize(line).X;
            var linePos = snappedPos + new Vector2(Math.Max(0.0f, (multilineWidth - lineWidth) * 0.5f), lineHeight * lineIndex);
            drawList.AddText(linePos + new Vector2(0.0f, Math.Max(1.0f, SnapToPixel(scale * 0.5f))), shadow, line);
            drawList.AddText(linePos, color, line);
            lineIndex++;
            if (newlineIndex < 0) {
                break;
            }

            start = newlineIndex + 1;
        }
    }

    private void DrawTaskBarGameIconItem(ImDrawListPtr drawList, Vector2 pos, TaskBarItem item, uint textColor, float opacity, float scale) {
        var iconSize = MathF.Round(GetTaskBarGameIconSize(item, scale));
        using var itemFont = PushTaskBarItemFont(item, scale);
        var textSize = string.IsNullOrWhiteSpace(item.Text) ? Vector2.Zero : CalcMultilineTextSize(item.Text);
        var drawHeight = Math.Max(iconSize, textSize.Y);
        var iconMin = SnapToPixel(pos + new Vector2(0.0f, Math.Max(0.0f, (drawHeight - iconSize) * 0.5f)));
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        if (!DrawGameIconImage(drawList, item.GameIconId, iconMin, iconMax, true, true)) {
            drawList.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0.58f, 0.25f, 0.42f, opacity * 0.25f)), 4.0f * scale);
            drawList.AddRect(iconMin, iconMax, ImGui.GetColorU32(new Vector4(0.58f, 0.25f, 0.42f, opacity * 0.55f)), 4.0f * scale);
        }

        if (!string.IsNullOrWhiteSpace(item.Text)) {
            var textPos = SnapToPixel(new Vector2(iconMax.X + 6.0f * scale, pos.Y + Math.Max(0.0f, (drawHeight - textSize.Y) * 0.5f)));
            DrawTaskBarText(drawList, textPos, textColor, item.Text, opacity, scale);
        }
    }

    private void DrawTaskBarCustomIcon(ImDrawListPtr drawList, Vector2 pos, float size, TaskBarDrawIcon icon, float opacity, float scale) {
        var iconMin = SnapToPixel(pos);
        var center = SnapToPixel(iconMin + new Vector2(size * 0.5f));
        var color = ImGui.GetColorU32(new Vector4(0.22f, 0.13f, 0.17f, opacity));
        switch (icon) {
            case TaskBarDrawIcon.MainMenu:
                DrawTaskBarMainMenuGlyph(drawList, center, size, color, scale);
                break;
            case TaskBarDrawIcon.Volume:
                DrawVolumeGlyph(drawList, center, size, color, scale, GetMasterVolume(), IsMasterVolumeMuted());
                break;
            case TaskBarDrawIcon.PluginList:
                DrawTaskBarPluginListGlyph(drawList, center, size, color, scale);
                break;
            case TaskBarDrawIcon.QuickMenu:
                DrawTaskBarQuickMenuGlyph(drawList, center, size, color, scale);
                break;
        }
    }

    private static void DrawTaskBarMainMenuGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color, float scale) {
        var stroke = Math.Max(1.5f, 1.8f * scale);
        var innerRadius = size * 0.09f;
        var bodyRadius = size * 0.18f;
        var toothRadius = size * 0.27f;
        const int ToothCount = 8;

        for (var tooth = 0; tooth < ToothCount; tooth++) {
            var angle = MathF.Tau * tooth / ToothCount;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(
                SnapToPixel(center + direction * bodyRadius),
                SnapToPixel(center + direction * toothRadius),
                color,
                stroke);
        }

        drawList.AddCircle(center, bodyRadius, color, 32, stroke);
        drawList.AddCircle(center, innerRadius, color, 20, stroke);
    }

    private static void DrawVolumeGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color, float scale, uint volume, bool muted) {
        // 极简线艺：经典喇叭与声波（音量）
        var stroke = Math.Max(1.5f, 1.8f * scale);
        
        var speakerPoints = new Vector2[6] {
            center + new Vector2(-size * 0.26f, -size * 0.10f),
            center + new Vector2(-size * 0.12f, -size * 0.10f),
            center + new Vector2(size * 0.04f, -size * 0.24f),
            center + new Vector2(size * 0.04f, size * 0.24f),
            center + new Vector2(-size * 0.12f, size * 0.10f),
            center + new Vector2(-size * 0.26f, size * 0.10f)
        };
        
        drawList.PathClear();
        for (var i = 0; i < 6; i++) {
            drawList.PathLineTo(speakerPoints[i]);
        }
        drawList.PathStroke(color, ImDrawFlags.Closed, stroke);

        if (muted || volume == 0) {
            // 右侧绘制极简 'x'
            var xCenter = center + new Vector2(size * 0.20f, 0.0f);
            var xSize = size * 0.08f;
            drawList.AddLine(xCenter + new Vector2(-xSize, -xSize), xCenter + new Vector2(xSize, xSize), color, stroke);
            drawList.AddLine(xCenter + new Vector2(xSize, -xSize), xCenter + new Vector2(-xSize, xSize), color, stroke);
        } else {
            // 右侧绘制声波弧线
            var arcCenter = center + new Vector2(-size * 0.04f, 0.0f);
            drawList.PathClear();
            drawList.PathArcTo(arcCenter, size * 0.18f, -0.7f, 0.7f, 8);
            drawList.PathStroke(color, ImDrawFlags.None, stroke);
            
            if (volume >= 50) {
                drawList.PathClear();
                drawList.PathArcTo(arcCenter, size * 0.28f, -0.6f, 0.6f, 10);
                drawList.PathStroke(color, ImDrawFlags.None, stroke);
            }
        }
    }

    private static void DrawTaskBarPluginListGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color, float scale) {
        // 极简线艺：插头（插件列表）
        var stroke = Math.Max(1.5f, 1.8f * scale);
        
        // 1. 插头主体
        var bodyMin = center + new Vector2(-size * 0.16f, -size * 0.08f);
        var bodyMax = center + new Vector2(size * 0.16f, size * 0.18f);
        drawList.AddRect(bodyMin, bodyMax, color, size * 0.04f, ImDrawFlags.None, stroke);
        
        // 2. 插头金属片（两个小竖线）
        var prongY = -size * 0.08f;
        var prongTopY = -size * 0.24f;
        drawList.AddLine(center + new Vector2(-size * 0.07f, prongY), center + new Vector2(-size * 0.07f, prongTopY), color, stroke);
        drawList.AddLine(center + new Vector2(size * 0.07f, prongY), center + new Vector2(size * 0.07f, prongTopY), color, stroke);
        
        // 3. 电线
        drawList.AddLine(center + new Vector2(0.0f, size * 0.18f), center + new Vector2(0.0f, size * 0.28f), color, stroke);
    }

    private static void DrawTaskBarQuickMenuGlyph(ImDrawListPtr drawList, Vector2 center, float size, uint color, float scale) {
        // 极简线艺：火箭/快捷启动（快捷菜单）
        var stroke = Math.Max(1.5f, 1.8f * scale);
        
        // 1. 火箭主体（尖头圆柱）
        var bodyPoints = new Vector2[5] {
            center + new Vector2(0.0f, -size * 0.28f), // 尖头
            center + new Vector2(size * 0.12f, -size * 0.10f),
            center + new Vector2(size * 0.12f, size * 0.16f), // 右下
            center + new Vector2(-size * 0.12f, size * 0.16f), // 左下
            center + new Vector2(-size * 0.12f, -size * 0.10f)
        };
        drawList.PathClear();
        for (var i = 0; i < 5; i++) {
            drawList.PathLineTo(bodyPoints[i]);
        }
        drawList.PathStroke(color, ImDrawFlags.Closed, stroke);

        // 2. 左右机翼
        drawList.AddLine(center + new Vector2(-size * 0.12f, size * 0.04f), center + new Vector2(-size * 0.24f, size * 0.16f), color, stroke);
        drawList.AddLine(center + new Vector2(-size * 0.24f, size * 0.16f), center + new Vector2(-size * 0.12f, size * 0.16f), color, stroke);

        drawList.AddLine(center + new Vector2(size * 0.12f, size * 0.04f), center + new Vector2(size * 0.24f, size * 0.16f), color, stroke);
        drawList.AddLine(center + new Vector2(size * 0.24f, size * 0.16f), center + new Vector2(size * 0.12f, size * 0.16f), color, stroke);

        // 3. 尾部火焰/喷气（一个小竖线）
        drawList.AddLine(center + new Vector2(0.0f, size * 0.16f), center + new Vector2(0.0f, size * 0.26f), color, stroke);
    }

    private void DrawTaskBarDalamudIcon(ImDrawListPtr drawList, Vector2 pos, float size, float opacity, float scale) {
        var iconMin = SnapToPixel(pos);
        var center = SnapToPixel(iconMin + new Vector2(size * 0.5f));
        DrawPluginPlugGlyph(drawList, center, size, opacity, scale);
    }

    private bool TryGetEmbeddedDalamudIconTexture(out Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? texture) {
        texture = this.embeddedDalamudIconTexture;
        if (texture is not null) {
            return true;
        }

        if (this.embeddedDalamudIconTextureTask is null) {
            this.embeddedDalamudIconTextureTask = LoadEmbeddedDalamudIconAsync();
            return false;
        }

        if (!this.embeddedDalamudIconTextureTask.IsCompletedSuccessfully) {
            return false;
        }

        this.embeddedDalamudIconTexture = this.embeddedDalamudIconTextureTask.Result;
        texture = this.embeddedDalamudIconTexture;
        return texture is not null;
    }

    private async Task<Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap?> LoadEmbeddedDalamudIconAsync() {
        try {
            var bytes = Convert.FromBase64String(EmbeddedDalamudIconPngBase64);
            return await this.textureProvider.CreateFromImageAsync(bytes, "AllHud Dalamud icon").ConfigureAwait(false);
        } catch {
            return null;
        }
    }

    private static bool TryGetTextureHandle(Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap texture, out ImTextureID handle) {
        try {
            handle = texture.Handle;
            return true;
        } catch {
            handle = default;
            return false;
        }
    }

    private static void DrawPluginPlugGlyph(ImDrawListPtr drawList, Vector2 center, float size, float opacity, float scale) {
        var strokeColor = ImGui.GetColorU32(new Vector4(0.30f, 0.16f, 0.23f, opacity));
        var fillColor = ImGui.GetColorU32(new Vector4(1.0f, 0.95f, 0.98f, opacity * 0.92f));
        var accentColor = ImGui.GetColorU32(new Vector4(0.90f, 0.36f, 0.55f, opacity * 0.96f));
        var stroke = Math.Max(1.3f, 1.9f * scale);
        var bodyMin = center + new Vector2(-size * 0.14f, -size * 0.08f);
        var bodyMax = center + new Vector2(size * 0.14f, size * 0.17f);

        drawList.AddLine(center + new Vector2(-size * 0.07f, -size * 0.08f), center + new Vector2(-size * 0.07f, -size * 0.23f), strokeColor, stroke);
        drawList.AddLine(center + new Vector2(size * 0.07f, -size * 0.08f), center + new Vector2(size * 0.07f, -size * 0.23f), strokeColor, stroke);
        drawList.AddRectFilled(bodyMin, bodyMax, fillColor, 3.0f * scale);
        drawList.AddRect(bodyMin, bodyMax, strokeColor, 3.0f * scale, (ImDrawFlags)0, stroke);
        drawList.AddBezierCubic(center + new Vector2(0.0f, size * 0.17f), center + new Vector2(0.0f, size * 0.31f), center + new Vector2(size * 0.16f, size * 0.30f), center + new Vector2(size * 0.24f, size * 0.30f), strokeColor, stroke);
        drawList.AddCircleFilled(center + new Vector2(size * 0.24f, size * 0.30f), Math.Max(1.2f, size * 0.045f), accentColor, 10);
    }

    private static void DrawTaskBarTooltip(string tooltip) {
        DrawStyledTooltip(() => ImGui.TextUnformatted(tooltip));
    }

    private static void DrawStyledTooltip(Action drawContent) {
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.16f, 0.10f, 0.14f, 0.94f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.92f, 0.96f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1.0f, 0.58f, 0.76f, 0.58f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(1.0f, 0.58f, 0.76f, 0.34f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(11.0f, 8.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 7.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6.0f, 4.0f));
        ImGui.BeginTooltip();
        drawContent();
        ImGui.EndTooltip();
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(4);
    }

    private void UpdateTaskBarFpsText() {
        var now = DateTime.UtcNow;
        if (now < this.nextTaskBarFpsUpdateAt) {
            return;
        }

        this.nextTaskBarFpsUpdateAt = now.AddMilliseconds(500);
        this.taskBarFpsText = $"FPS {Math.Clamp((int)MathF.Round(ImGui.GetIO().Framerate), 0, 999):000}";
    }

    private List<TaskBarItem> BuildLocalTaskBarItems(IReadOnlyList<TaskBarItem> dtrItems) {
        return BuildTaskBarItemsForOrder(this.config.TaskBarComponentOrder, dtrItems);
    }

    private List<TaskBarItem> BuildTaskBarItemsForOrder(IEnumerable<string> componentOrder, IReadOnlyList<TaskBarItem> dtrItems) {
        var items = componentOrder is ICollection<string> collection ? new List<TaskBarItem>(collection.Count) : new List<TaskBarItem>(6);

        foreach (var componentId in componentOrder) {
            switch (componentId) {
                case Configuration.TaskBarComponentTime when this.config.TaskBarShowLocalTime || this.config.TaskBarShowEorzeaTime:
                    var timeText = GetTaskBarTimeText();
                    items.Add(new TaskBarItem(timeText, timeText, null, MeasureText: GetTaskBarTimeMeasureText(), TextScale: GetTaskBarTimeTextScale()));
                    break;
                case Configuration.TaskBarComponentFps when this.config.TaskBarShowFps:
                    UpdateTaskBarFpsText();
                    items.Add(new TaskBarItem(this.taskBarFpsText, this.taskBarFpsText, null, MeasureText: "FPS 000"));
                    break;
                case Configuration.TaskBarComponentVolume when this.config.TaskBarShowVolume:
                    items.Add(new TaskBarItem(string.Empty, GetVolumeTaskBarTooltip(), _ => { }, HandleVolumeTaskBarClick, "AllHud 音量控制", true, DrawIcon: TaskBarDrawIcon.Volume));
                    break;
                case Configuration.TaskBarComponentMainMenu when this.config.TaskBarShowMainMenu:
                    items.Add(new TaskBarItem(string.Empty, string.Empty, _ => { }, PopupId: "AllHud 主菜单", DrawIcon: TaskBarDrawIcon.MainMenu));
                    break;
                case Configuration.TaskBarComponentPluginList when this.config.TaskBarShowPluginList:
                    items.Add(new TaskBarItem(string.Empty, "左键查看已添加插件，右键打开 Dalamud 插件管理器", _ => { }, _ => this.commandManager.ProcessCommand("/xlplugins"), "AllHud 插件列表", DrawIcon: TaskBarDrawIcon.PluginList));
                    break;
                case var pluginShortcutId when Configuration.IsPluginShortcutComponentId(pluginShortcutId):
                    if (TryCreatePluginShortcutTaskBarItem(pluginShortcutId, out var pluginShortcutItem)) {
                        items.Add(pluginShortcutItem);
                    }

                    break;
                case var customShortcutId when Configuration.IsCustomShortcutComponentId(customShortcutId):
                    if (TryCreateCustomShortcutTaskBarItem(customShortcutId, out var customShortcutItem)) {
                        items.Add(customShortcutItem);
                    }

                    break;
                case var quickMenuId when Configuration.IsQuickMenuComponentId(quickMenuId):
                    if (TryCreateQuickMenuTaskBarItem(quickMenuId, out var quickMenuItem)) {
                        items.Add(quickMenuItem);
                    }

                    break;
                case Configuration.TaskBarComponentServerInfo when this.config.TaskBarShowServerInfoBar && this.config.TaskBarServerInfoBarMode == 1:
                    items.AddRange(dtrItems);
                    break;
                case Configuration.TaskBarComponentInventory when this.config.TaskBarShowInventory:
                    items.Add(CreateInventoryTaskBarItem(false));
                    break;
                case Configuration.TaskBarComponentSaddlebag when this.config.TaskBarShowSaddlebag:
                    items.Add(CreateInventoryTaskBarItem(true));
                    break;
                case Configuration.TaskBarComponentTeleport when this.config.TaskBarShowTeleport:
                    items.Add(CreateTeleportTaskBarItem());
                    break;
                case Configuration.TaskBarComponentCoordinates when this.config.TaskBarShowCoordinates:
                    items.Add(CreateCoordinatesTaskBarItem());
                    break;
                case Configuration.TaskBarComponentGearsetSwitcher when this.config.TaskBarShowGearsetSwitcher:
                    items.Add(CreateGearsetSwitcherTaskBarItem());
                    break;
                case Configuration.TaskBarComponentCurrency when this.config.TaskBarShowCurrency:
                    items.Add(CreateCurrencyTaskBarItem());
                    break;
            }
        }

        return items;
    }
}

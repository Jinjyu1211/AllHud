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
    private void DrawAuxiliaryBars(ImGuiWindowFlags flags) {
        if (this.config.AuxiliaryBars.Count == 0) {
            return;
        }

        var viewport = ImGui.GetMainViewport();
        var leftOffset = 0.0f;
        var rightOffset = 0.0f;
        for (var index = 0; index < this.config.AuxiliaryBars.Count; index++) {
            var bar = this.config.AuxiliaryBars[index];
            if (!bar.Enabled) {
                continue;
            }

            var scale = Math.Clamp(bar.Scale, 0.6f, 2.0f);
            var opacity = Math.Clamp(bar.Opacity, 0.15f, 1.0f);
            var stretchSections = bar.StretchToEdges;
            var horizontal = IsAuxiliaryBarHorizontal(bar);
            var startItems = stretchSections ? BuildAuxiliaryBarItemsForOrder(bar.SectionStartComponentOrder) : [];
            var centerItems = stretchSections ? BuildAuxiliaryBarItemsForOrder(bar.SectionCenterComponentOrder) : [];
            var endItems = stretchSections ? BuildAuxiliaryBarItemsForOrder(bar.SectionEndComponentOrder) : [];
            var items = stretchSections ? [] : BuildAuxiliaryBarItems(bar);
            var itemCount = stretchSections ? startItems.Count + centerItems.Count + endItems.Count : items.Count;
            if (itemCount == 0) {
                continue;
            }

            var taskBarFontSize = TaskBarBaseFontSize * scale;
            if (!IsHudFontReady(taskBarFontSize)
                || !AreAuxiliaryItemFontsReady(stretchSections, startItems, centerItems, endItems, items, taskBarFontSize)) {
                continue;
            }

            using var taskBarFont = PushTaskBarFont(scale);
            var itemHeight = MathF.Round(Math.Max(44.0f * scale, ImGui.GetTextLineHeight() + 18.0f * scale));
            var itemSpacing = MathF.Round(4.0f * scale);
            var padding = new Vector2(7.0f * scale, 3.0f * scale);
            var rowSpacing = MathF.Round(18.0f * scale);
            var minimumThickness = MathF.Round(50.0f * scale);
            var width = horizontal
                ? MathF.Round(bar.StretchToEdges ? viewport.Size.X : CalculateAuxiliaryHorizontalContentWidth(stretchSections, startItems, centerItems, endItems, items, padding, rowSpacing, scale))
                : MathF.Max(minimumThickness, MathF.Round(CalculateAuxiliaryVerticalContentWidth(stretchSections, startItems, centerItems, endItems, items, scale) + padding.X * 2.0f));
            var contentHeight = horizontal
                ? MathF.Max(minimumThickness, MathF.Round(itemHeight + padding.Y * 2.0f))
                : itemCount * itemHeight + Math.Max(0, itemCount - 1) * itemSpacing + padding.Y * 2.0f;
            var height = MathF.Round(horizontal ? contentHeight : bar.StretchToEdges ? viewport.Size.Y : contentHeight);
            var customPosition = bar.PositionMode == 2;
            var displaySize = ImGui.GetIO().DisplaySize;
            var screenMin = Vector2.Zero;
            var screenMax = displaySize;
            var x = customPosition && horizontal && bar.StretchToEdges
                ? viewport.Pos.X
                : customPosition
                    ? bar.CustomPosition.X
                : bar.PositionMode == 1
                    ? screenMax.X - width - rightOffset
                    : screenMin.X + leftOffset;
            var y = customPosition && !horizontal && bar.StretchToEdges
                ? viewport.Pos.Y
                : customPosition
                    ? bar.CustomPosition.Y
                : bar.StretchToEdges
                    ? screenMin.Y
                    : screenMin.Y + Math.Max(0.0f, (screenMax.Y - height) * 0.5f) + GetAuxiliaryBarVerticalOffsetPixels(bar, screenMax.Y - screenMin.Y, height);

            if (!customPosition) {
                if (bar.PositionMode == 1) {
                    rightOffset += width + 4.0f;
                } else {
                    leftOffset += width + 4.0f;
                }
            }

            if (stretchSections) {
                DrawAuxiliaryBarSectionWindow(index, bar, startItems, centerItems, endItems, horizontal, new Vector2(x, y), new Vector2(width, height), padding, itemHeight, itemSpacing, rowSpacing, opacity, scale, flags, customPosition);
            } else if (horizontal) {
                DrawAuxiliaryBarSectionWindow(index, bar, items, [], [], true, new Vector2(x, y), new Vector2(width, height), padding, itemHeight, itemSpacing, rowSpacing, opacity, scale, flags, customPosition);
            } else {
                DrawAuxiliaryBarWindow(index, bar, items, new Vector2(x, y), new Vector2(width, height), padding, itemHeight, itemSpacing, opacity, scale, flags, customPosition);
            }
        }
    }

    private static bool IsAuxiliaryBarHorizontal(AuxiliaryBarDefinition bar) {
        return bar.PositionMode == 2 && bar.LayoutDirection == 1;
    }

    // 辅助栏停靠左/右且尺寸自适应时，按 0~1 的比例在可用空闲空间内放置。0=最上，0.5=居中，1=最下。
    private static float GetAuxiliaryBarVerticalOffsetPixels(AuxiliaryBarDefinition bar, float availableHeight, float height) {
        var freeSpace = Math.Max(0.0f, availableHeight - height);
        var offset = Math.Clamp(bar.VerticalOffset, 0.0f, 1.0f);
        return (offset - 0.5f) * freeSpace;
    }
    private static ImDrawFlags GetAuxiliaryPanelRoundingFlags(AuxiliaryBarDefinition bar, bool customPosition, bool horizontal) {
        if (customPosition || horizontal) {
            return (ImDrawFlags)0;
        }

        return bar.PositionMode == 1
            ? ImDrawFlags.RoundCornersLeft
            : ImDrawFlags.RoundCornersRight;
    }

    private bool AreAuxiliaryItemFontsReady(bool stretchSections, IReadOnlyList<TaskBarItem> startItems, IReadOnlyList<TaskBarItem> centerItems, IReadOnlyList<TaskBarItem> endItems, IReadOnlyList<TaskBarItem> items, float taskBarFontSize) {
        return stretchSections
            ? AreTaskBarItemFontsReady(startItems, taskBarFontSize) && AreTaskBarItemFontsReady(centerItems, taskBarFontSize) && AreTaskBarItemFontsReady(endItems, taskBarFontSize)
            : AreTaskBarItemFontsReady(items, taskBarFontSize);
    }

    private bool AreTaskBarItemFontsReady(IReadOnlyList<TaskBarItem> items, float taskBarFontSize) {
        foreach (var item in items) {
            if (!item.IsIcon && Math.Abs(item.TextScale - 1.0f) > 0.001f && !IsHudFontReady(taskBarFontSize * item.TextScale)) {
                return false;
            }

            if (item.IsIcon && item.DrawIcon == TaskBarDrawIcon.None && !IsIconFontReady(taskBarFontSize)) {
                return false;
            }
        }

        return true;
    }

    private float CalculateAuxiliaryHorizontalContentWidth(bool stretchSections, IReadOnlyList<TaskBarItem> startItems, IReadOnlyList<TaskBarItem> centerItems, IReadOnlyList<TaskBarItem> endItems, IReadOnlyList<TaskBarItem> items, Vector2 padding, float spacing, float scale) {
        return stretchSections
            ? Math.Max(CalculateAuxiliaryHorizontalSectionWidth(startItems, padding, spacing, scale), Math.Max(CalculateAuxiliaryHorizontalSectionWidth(centerItems, padding, spacing, scale), CalculateAuxiliaryHorizontalSectionWidth(endItems, padding, spacing, scale)))
            : CalculateAuxiliaryHorizontalSectionWidth(items, padding, spacing, scale);
    }

    private float CalculateAuxiliaryVerticalContentWidth(bool stretchSections, IReadOnlyList<TaskBarItem> startItems, IReadOnlyList<TaskBarItem> centerItems, IReadOnlyList<TaskBarItem> endItems, IReadOnlyList<TaskBarItem> items, float scale) {
        return stretchSections
            ? Math.Max(CalculateAuxiliaryVerticalContentWidth(startItems, scale), Math.Max(CalculateAuxiliaryVerticalContentWidth(centerItems, scale), CalculateAuxiliaryVerticalContentWidth(endItems, scale)))
            : CalculateAuxiliaryVerticalContentWidth(items, scale);
    }

    private float CalculateAuxiliaryVerticalContentWidth(IReadOnlyList<TaskBarItem> items, float scale) {
        var width = 0.0f;
        foreach (var item in items) {
            width = Math.Max(width, CalcTaskBarItemLayoutSize(item, scale).X);
        }

        return width;
    }

    private float CalculateAuxiliaryHorizontalSectionWidth(IReadOnlyList<TaskBarItem> items, Vector2 padding, float spacing, float scale) {
        if (items.Count == 0) {
            return 0.0f;
        }

        var width = 0.0f;
        foreach (var item in items) {
            width += CalcTaskBarItemLayoutSize(item, scale).X;
        }

        width += spacing * Math.Max(0, items.Count - 1);
        return width + padding.X * 2.0f;
    }

    private List<TaskBarItem> BuildAuxiliaryBarItems(AuxiliaryBarDefinition bar) {
        return BuildAuxiliaryBarItemsForOrder(bar.ComponentOrder);
    }

    private List<TaskBarItem> BuildAuxiliaryBarItemsForOrder(IEnumerable<string> componentOrder) {
        var items = new List<TaskBarItem>();
        IReadOnlyList<TaskBarItem>? dtrItems = null;
        foreach (var componentId in componentOrder) {
            switch (componentId) {
                case Configuration.TaskBarComponentTime:
                    var timeText = GetTaskBarTimeText();
                    if (!string.IsNullOrWhiteSpace(timeText)) {
                        items.Add(new TaskBarItem(timeText, timeText, null, MeasureText: GetTaskBarTimeMeasureText(), TextScale: GetTaskBarTimeTextScale()));
                    }
                    break;
                case Configuration.TaskBarComponentFps:
                    UpdateTaskBarFpsText();
                    items.Add(new TaskBarItem(this.taskBarFpsText, this.taskBarFpsText, null, MeasureText: "FPS 000"));
                    break;
                case Configuration.TaskBarComponentVolume:
                    items.Add(new TaskBarItem(string.Empty, GetVolumeTaskBarTooltip(), _ => { }, HandleVolumeTaskBarClick, "AllHud 音量控制", true, DrawIcon: TaskBarDrawIcon.Volume));
                    break;
                case Configuration.TaskBarComponentMainMenu:
                    items.Add(new TaskBarItem(string.Empty, string.Empty, _ => { }, PopupId: "AllHud 主菜单", DrawIcon: TaskBarDrawIcon.MainMenu));
                    break;
                case Configuration.TaskBarComponentPluginList:
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
                case Configuration.TaskBarComponentServerInfo:
                    dtrItems ??= BuildDtrTaskBarItems();
                    items.AddRange(dtrItems);
                    break;
                case Configuration.TaskBarComponentInventory:
                    items.Add(CreateInventoryTaskBarItem(false));
                    break;
                case Configuration.TaskBarComponentSaddlebag:
                    items.Add(CreateInventoryTaskBarItem(true));
                    break;
                case Configuration.TaskBarComponentTeleport:
                    items.Add(CreateTeleportTaskBarItem());
                    break;
                case Configuration.TaskBarComponentCoordinates:
                    items.Add(CreateCoordinatesTaskBarItem());
                    break;
                case Configuration.TaskBarComponentGearsetSwitcher:
                    items.Add(CreateGearsetSwitcherTaskBarItem());
                    break;
                case Configuration.TaskBarComponentCurrency:
                    items.Add(CreateCurrencyTaskBarItem());
                    break;
            }
        }

        return items;
    }

    private void DrawAuxiliaryBarWindow(int index, AuxiliaryBarDefinition bar, IReadOnlyList<TaskBarItem> items, Vector2 pos, Vector2 size, Vector2 padding, float itemHeight, float itemSpacing, float opacity, float scale, ImGuiWindowFlags flags, bool customPosition) {
        ImGui.SetNextWindowPos(SnapToPixel(pos), customPosition ? ImGuiCond.Once : ImGuiCond.Always);
        ImGui.SetNextWindowSize(SnapToPixel(size), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        if (!customPosition) {
            flags |= ImGuiWindowFlags.NoMove;
        }
        if (!ImGui.Begin($"AllHud 辅助栏 {index}", flags)) {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var windowMin = ImGui.GetWindowPos();
        if (customPosition) {
            TrackAuxiliaryBarPosition(index, bar, windowMin);
        }

        var windowMax = windowMin + size;
        var panelRounding = 6.0f * scale;
        var panelRoundingFlags = GetAuxiliaryPanelRoundingFlags(bar, customPosition, false);
        drawList.AddRectFilled(windowMin + new Vector2(0.0f, 2.0f * scale), windowMax + new Vector2(0.0f, 2.0f * scale), ImGui.GetColorU32(new Vector4(0.36f, 0.14f, 0.25f, opacity * 0.14f)), panelRounding, panelRoundingFlags);
        drawList.AddRectFilled(windowMin, windowMax, ImGui.GetColorU32(new Vector4(1.0f, 0.84f, 0.92f, opacity * 0.88f)), panelRounding, panelRoundingFlags);
        drawList.AddRectFilledMultiColor(
            windowMin + new Vector2(1.0f * scale, 1.0f * scale),
            windowMax - new Vector2(1.0f * scale, Math.Max(1.0f, size.Y * 0.46f)),
            ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.40f)),
            ImGui.GetColorU32(new Vector4(0.96f, 0.86f, 1.0f, opacity * 0.26f)),
            ImGui.GetColorU32(new Vector4(0.96f, 0.86f, 1.0f, opacity * 0.06f)),
            ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.08f)));
        drawList.AddRect(windowMin, windowMax, ImGui.GetColorU32(new Vector4(0.94f, 0.58f, 0.74f, opacity * 0.82f)), panelRounding, panelRoundingFlags, 1.0f * scale);

        var cursor = windowMin + padding;
        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++) {
            var item = items[itemIndex];
            var textSize = CalcTaskBarItemLayoutSize(item, scale);
            var drawTextSize = CalcTaskBarItemDrawTextSize(item, scale);
            var itemSlotMin = SnapToPixel(new Vector2(cursor.X, cursor.Y));
            var itemSlotSize = SnapToPixel(new Vector2(Math.Max(textSize.X, size.X - padding.X * 2.0f), itemHeight));
            var iconLike = item.DrawIcon != TaskBarDrawIcon.None
                           || item.IsDalamudIcon
                           || !string.IsNullOrWhiteSpace(item.PluginShortcutInternalName)
                           || (item.GameIconId != 0 && string.IsNullOrWhiteSpace(item.Text));
            var cardWidth = iconLike ? Math.Min(itemSlotSize.X, itemHeight - 8.0f * scale) : itemSlotSize.X + 8.0f * scale;
            var itemMin = SnapToPixel(new Vector2(itemSlotMin.X + Math.Max(0.0f, (itemSlotSize.X - cardWidth) * 0.5f), itemSlotMin.Y + 4.0f * scale));
            var itemSize = SnapToPixel(new Vector2(cardWidth, itemHeight - 8.0f * scale));
            ImGui.SetCursorScreenPos(itemMin);
            ImGui.InvisibleButton($"##aux_item_{index}_{itemIndex}", itemSize);
            var hovered = ImGui.IsItemHovered();
            var active = ImGui.IsItemActive();
            DrawTaskBarItemCard(drawList, itemMin, itemSize, itemIndex, hovered, active, item.OnClick is not null, iconLike, opacity, scale);
            if (hovered && item.OnClick is not null) {
                if (!string.IsNullOrWhiteSpace(item.Tooltip)) {
                    DrawTaskBarTooltip(item.Tooltip);
                }
            }

            if (item.OnClick is not null && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                item.OnClick.Invoke(CreateDtrInteractionEvent(MouseClickType.Left));
                OpenTaskBarItemPopup(item);
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                var rightClick = item.OnRightClick ?? item.OnClick;
                rightClick?.Invoke(CreateDtrInteractionEvent(MouseClickType.Right));
            }

            var textPos = SnapToPixel(new Vector2(itemSlotMin.X + Math.Max(0.0f, (itemSlotSize.X - drawTextSize.X) * 0.5f), itemSlotMin.Y + Math.Max(0.0f, (itemHeight - drawTextSize.Y) * 0.5f)));
            var textColor = ImGui.GetColorU32(new Vector4(0.20f, 0.13f, 0.16f, opacity));
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

            cursor.Y += itemHeight + itemSpacing;
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

    private void DrawAuxiliaryBarSectionWindow(int index, AuxiliaryBarDefinition bar, IReadOnlyList<TaskBarItem> startItems, IReadOnlyList<TaskBarItem> centerItems, IReadOnlyList<TaskBarItem> endItems, bool horizontal, Vector2 pos, Vector2 size, Vector2 padding, float itemHeight, float itemSpacing, float rowSpacing, float opacity, float scale, ImGuiWindowFlags flags, bool customPosition) {
        ImGui.SetNextWindowPos(SnapToPixel(pos), customPosition ? ImGuiCond.Once : ImGuiCond.Always);
        ImGui.SetNextWindowSize(SnapToPixel(size), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        flags |= ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        if (!customPosition) {
            flags |= ImGuiWindowFlags.NoMove;
        }

        if (!ImGui.Begin($"AllHud 辅助栏 {index}", flags)) {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var windowMin = ImGui.GetWindowPos();
        if (customPosition) {
            TrackAuxiliaryBarPosition(index, bar, windowMin);
        }

        var windowMax = windowMin + size;
        var panelRounding = 6.0f * scale;
        var panelRoundingFlags = GetAuxiliaryPanelRoundingFlags(bar, customPosition, horizontal);
        drawList.AddRectFilled(windowMin + new Vector2(0.0f, 2.0f * scale), windowMax + new Vector2(0.0f, 2.0f * scale), ImGui.GetColorU32(new Vector4(0.36f, 0.14f, 0.25f, opacity * 0.14f)), panelRounding, panelRoundingFlags);
        drawList.AddRectFilled(windowMin, windowMax, ImGui.GetColorU32(new Vector4(1.0f, 0.84f, 0.92f, opacity * 0.88f)), panelRounding, panelRoundingFlags);
        drawList.AddRectFilledMultiColor(
            windowMin + new Vector2(1.0f * scale, 1.0f * scale),
            windowMax - new Vector2(1.0f * scale, Math.Max(1.0f, size.Y * 0.46f)),
            ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.40f)),
            ImGui.GetColorU32(new Vector4(0.96f, 0.86f, 1.0f, opacity * 0.26f)),
            ImGui.GetColorU32(new Vector4(0.96f, 0.86f, 1.0f, opacity * 0.06f)),
            ImGui.GetColorU32(new Vector4(1.0f, 0.96f, 0.99f, opacity * 0.08f)));
        drawList.AddRect(windowMin, windowMax, ImGui.GetColorU32(new Vector4(0.94f, 0.58f, 0.74f, opacity * 0.82f)), panelRounding, panelRoundingFlags, 1.0f * scale);

        void DrawItem(TaskBarItem item, Vector2 slotMin, Vector2 slotSize, string id, int visualIndex) {
            var textSize = CalcTaskBarItemLayoutSize(item, scale);
            var drawTextSize = CalcTaskBarItemDrawTextSize(item, scale);
            var iconLike = item.DrawIcon != TaskBarDrawIcon.None
                           || item.IsDalamudIcon
                           || !string.IsNullOrWhiteSpace(item.PluginShortcutInternalName)
                           || (item.GameIconId != 0 && string.IsNullOrWhiteSpace(item.Text));
            var cardWidth = iconLike ? Math.Min(slotSize.X, itemHeight - 8.0f * scale) : slotSize.X + 8.0f * scale;
            var itemMin = SnapToPixel(new Vector2(slotMin.X + Math.Max(0.0f, (slotSize.X - cardWidth) * 0.5f), slotMin.Y + 4.0f * scale));
            var itemSize = SnapToPixel(new Vector2(cardWidth, itemHeight - 8.0f * scale));
            ImGui.SetCursorScreenPos(itemMin);
            ImGui.InvisibleButton(id, itemSize);
            var hovered = ImGui.IsItemHovered();
            var active = ImGui.IsItemActive();
            DrawTaskBarItemCard(drawList, itemMin, itemSize, visualIndex, hovered, active, item.OnClick is not null, iconLike, opacity, scale);
            if (hovered && item.OnClick is not null) {
                if (!string.IsNullOrWhiteSpace(item.Tooltip)) {
                    DrawTaskBarTooltip(item.Tooltip);
                }
            }

            if (item.OnClick is not null && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
                item.OnClick.Invoke(CreateDtrInteractionEvent(MouseClickType.Left));
                OpenTaskBarItemPopup(item);
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
                var rightClick = item.OnRightClick ?? item.OnClick;
                rightClick?.Invoke(CreateDtrInteractionEvent(MouseClickType.Right));
            }

            var textPos = SnapToPixel(new Vector2(slotMin.X + Math.Max(0.0f, (slotSize.X - drawTextSize.X) * 0.5f), slotMin.Y + Math.Max(0.0f, (itemHeight - drawTextSize.Y) * 0.5f)));
            var textColor = ImGui.GetColorU32(new Vector4(0.20f, 0.13f, 0.16f, opacity));
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
        }

        float VerticalGroupHeight(IReadOnlyList<TaskBarItem> group) => group.Count == 0 ? 0.0f : group.Count * itemHeight + Math.Max(0, group.Count - 1) * itemSpacing;
        float HorizontalGroupWidth(IReadOnlyList<TaskBarItem> group) => group.Count == 0 ? 0.0f : group.Sum(item => CalcTaskBarItemLayoutSize(item, scale).X) + Math.Max(0, group.Count - 1) * rowSpacing;

        if (horizontal) {
            void DrawHorizontalGroup(IReadOnlyList<TaskBarItem> group, float x, string section) {
                var cursor = SnapToPixel(new Vector2(x, windowMin.Y + padding.Y));
                for (var itemIndex = 0; itemIndex < group.Count; itemIndex++) {
                    var item = group[itemIndex];
                    var slotWidth = CalcTaskBarItemLayoutSize(item, scale).X;
                    DrawItem(item, cursor, new Vector2(slotWidth, itemHeight), $"##aux_section_item_{index}_{section}_{itemIndex}", itemIndex);
                    cursor.X += slotWidth + rowSpacing;
                }
            }

            var startWidth = HorizontalGroupWidth(startItems);
            var centerWidth = HorizontalGroupWidth(centerItems);
            var endWidth = HorizontalGroupWidth(endItems);
            DrawHorizontalGroup(startItems, windowMin.X + padding.X, "start");
            DrawHorizontalGroup(centerItems, windowMin.X + Math.Max(padding.X, (size.X - centerWidth) * 0.5f), "center");
            DrawHorizontalGroup(endItems, windowMin.X + Math.Max(padding.X, size.X - padding.X - endWidth), "end");
        } else {
            void DrawVerticalGroup(IReadOnlyList<TaskBarItem> group, float y, string section) {
                var cursor = SnapToPixel(new Vector2(windowMin.X + padding.X, y));
                var slotWidth = Math.Max(0.0f, size.X - padding.X * 2.0f);
                for (var itemIndex = 0; itemIndex < group.Count; itemIndex++) {
                    DrawItem(group[itemIndex], cursor, new Vector2(slotWidth, itemHeight), $"##aux_section_item_{index}_{section}_{itemIndex}", itemIndex);
                    cursor.Y += itemHeight + itemSpacing;
                }
            }

            var startHeight = VerticalGroupHeight(startItems);
            var centerHeight = VerticalGroupHeight(centerItems);
            var endHeight = VerticalGroupHeight(endItems);
            DrawVerticalGroup(startItems, windowMin.Y + padding.Y, "start");
            DrawVerticalGroup(centerItems, windowMin.Y + Math.Max(padding.Y, (size.Y - centerHeight) * 0.5f), "center");
            DrawVerticalGroup(endItems, windowMin.Y + Math.Max(padding.Y, size.Y - padding.Y - endHeight), "end");
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

    private void TrackAuxiliaryBarPosition(int index, AuxiliaryBarDefinition bar, Vector2 currentPosition) {
        if (!this.lastSavedAuxiliaryBarPositions.ContainsKey(index)) {
            this.lastSavedAuxiliaryBarPositions[index] = bar.CustomPosition;
        }

        var now = DateTime.UtcNow;
        if (Vector2.DistanceSquared(currentPosition, bar.CustomPosition) > 1.0f) {
            bar.CustomPosition = currentPosition;
            this.auxiliaryBarPositionSaveDueAt[index] = now.Add(OverlayPositionSaveDelay);
        }

        if (!this.auxiliaryBarPositionSaveDueAt.TryGetValue(index, out var saveDueAt) || now < saveDueAt) {
            return;
        }

        if (Vector2.DistanceSquared(bar.CustomPosition, this.lastSavedAuxiliaryBarPositions[index]) > 1.0f) {
            this.saveConfig();
            this.lastSavedAuxiliaryBarPositions[index] = bar.CustomPosition;
        }

        this.auxiliaryBarPositionSaveDueAt.Remove(index);
    }
}

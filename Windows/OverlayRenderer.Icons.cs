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
    private void DrawCurrencyPopup(float opacity, float scale) {
        DrawSimpleTaskBarPopup(CurrencyPopupId, new Vector2(320.0f * scale, 0.0f), opacity, scale, () => {
            var drawList = ImGui.GetWindowDrawList();
            foreach (var currency in GetCurrencyDisplayOptions()) {
                var count = GetCurrencyCount(currency.ItemId);
                var countText = count >= 0 ? $"{count:N0}" : "--";
                var rowMin = ImGui.GetCursorScreenPos();
                var rowHeight = MathF.Round(26.0f * scale);
                var rowMax = rowMin + new Vector2(Math.Max(180.0f * scale, ImGui.GetContentRegionAvail().X), rowHeight);
                var selected = currency.ItemId == this.config.TaskBarCurrencyItemId;
                drawList.AddRectFilled(rowMin, rowMax, ImGui.GetColorU32(selected ? new Vector4(0.96f, 0.74f, 0.86f, 0.34f) : new Vector4(1.0f, 0.96f, 0.98f, 0.18f)), 6.0f * scale);
                if (currency.IconId != 0) {
                    DrawGameIconImage(drawList, currency.IconId, rowMin + new Vector2(4.0f * scale, 3.0f * scale), rowMin + new Vector2(24.0f * scale, 23.0f * scale), true, true);
                }

                ImGui.SetCursorScreenPos(rowMin);
                ImGui.InvisibleButton($"##currency_{currency.ItemId}", rowMax - rowMin);
                if (ImGui.IsItemClicked()) {
                    this.config.TaskBarCurrencyItemId = currency.ItemId;
                    this.saveConfig();
                }

                ImGui.SetCursorScreenPos(rowMin + new Vector2(30.0f * scale, Math.Max(0.0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f)));
                ImGui.TextUnformatted($"{currency.Name}：{countText}");
                ImGui.SetCursorScreenPos(new Vector2(rowMin.X, rowMax.Y + 3.0f * scale));
            }
        });
    }

    private void DrawSimpleTaskBarPopup(string popupId, Vector2 popupSize, float opacity, float scale, Action drawContent) {
        if (!ImGui.IsPopupOpen(popupId)) {
            return;
        }

        if (this.simplePopupAnchor == default) {
            this.simplePopupAnchor = new TaskBarPopupAnchor(ImGui.GetMainViewport().WorkPos, ImGui.GetMainViewport().WorkPos);
        }

        var fitSize = popupSize.Y > 0.0f ? popupSize : new Vector2(popupSize.X, 1.0f);
        ImGui.SetNextWindowPos(GetTaskBarPopupPosition(this.simplePopupAnchor, fitSize, scale, fitSize.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(popupSize, ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(1.0f, 0.90f, 0.95f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.22f, 0.13f, 0.17f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.94f, 0.58f, 0.74f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.88f, 0.66f, 0.80f, 0.38f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.88f, 0.58f, 0.76f, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.82f, 0.50f, 0.70f, 0.54f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10.0f * scale, 9.0f * scale));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 9.0f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6.0f * scale, 5.0f * scale));

        try {
            if (ImGui.BeginPopup(popupId)) {
                drawContent();
                ImGui.EndPopup();
            }
        } finally {
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(6);
        }
    }

    private void DrawVolumeChannelControl(string label, SystemConfigOption volumeOption, SystemConfigOption muteOption, float scale) {
        var volume = GetVolume(volumeOption);
        var muted = IsVolumeMuted(muteOption);
        ImGui.PushID(label);

        var rowStart = ImGui.GetCursorScreenPos();
        var iconColumnWidth = 30.0f * scale;
        var textLineHeight = ImGui.GetTextLineHeight();
        var headerHeight = Math.Max(textLineHeight, 28.0f * scale);
        var sliderWidth = 230.0f * scale;
        var sliderGap = 0.0f;
        var bottomGap = 4.0f * scale;
        var drawList = ImGui.GetWindowDrawList();

        var iconRectMin = rowStart;
        var iconRectSize = new Vector2(iconColumnWidth, headerHeight);
        ImGui.SetCursorScreenPos(iconRectMin);
        ImGui.InvisibleButton("##mute", iconRectSize);
        var iconHovered = ImGui.IsItemHovered();
        var iconActive = ImGui.IsItemActive();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            ToggleVolumeMute(muteOption);
        }

        if (iconHovered) {
            DrawTaskBarTooltip(muted ? "开启声音" : "静音");
        }

        var iconColor = iconActive
            ? new Vector4(0.08f, 0.05f, 0.07f, 1.0f)
            : iconHovered ? new Vector4(0.42f, 0.12f, 0.22f, 1.0f) : new Vector4(0.18f, 0.10f, 0.14f, 1.0f);
        var iconSize = MathF.Round(28.0f * scale);
        DrawVolumeGlyph(
            drawList,
            SnapToPixel(iconRectMin + new Vector2(iconColumnWidth * 0.5f, headerHeight * 0.5f)),
            iconSize,
            ImGui.GetColorU32(iconColor),
            scale,
            volume,
            muted);

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X + iconColumnWidth + 2.0f * scale, rowStart.Y + Math.Max(0.0f, (headerHeight - textLineHeight) * 0.5f)));
        ImGui.TextUnformatted(muted ? $"{label}：静音" : label);
        if (ImGui.IsItemHovered()) {
            DrawTaskBarTooltip("Ctrl+点击滑条可手动输入百分比");
        }

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + headerHeight + sliderGap));
        ImGui.SetNextItemWidth(sliderWidth);
        var inputVolume = (int)volume;
        if (ImGui.SliderInt("##volume", ref inputVolume, 0, 100, "%d%%")) {
            SetVolume(volumeOption, muteOption, (uint)inputVolume);
        }

        if (ImGui.IsItemHovered()) {
            DrawTaskBarTooltip("拖动调整，Ctrl+点击输入百分比");
        }

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, ImGui.GetItemRectMax().Y + bottomGap));
        ImGui.PopID();
    }

    private static FontAwesomeIcon GetVolumeIcon(uint volume, bool muted) {
        if (muted) {
            return FontAwesomeIcon.VolumeMute;
        }

        if (volume == 0) {
            return FontAwesomeIcon.VolumeOff;
        }

        return volume < 50 ? FontAwesomeIcon.VolumeDown : FontAwesomeIcon.VolumeUp;
    }

    private void DrawPluginListPopup(float opacity, float scale) {
        if (this.pluginListPopupDrawnThisFrame) {
            return;
        }

        this.pluginListPopupDrawnThisFrame = true;
        if (this.pendingPluginListPopupOpenFrames > 0) {
            this.pendingPluginListPopupOpenFrames--;
            if (this.pendingPluginListPopupOpenFrames == 0) {
                ImGui.OpenPopup("AllHud 插件列表");
            }
        }

        if (this.pendingPluginListPopupOpenFrames <= 0 && !ImGui.IsPopupOpen("AllHud 插件列表")) {
            return;
        }

        var popupSize = new Vector2(330.0f * scale, 420.0f * scale);
        ImGui.SetNextWindowPos(GetTaskBarPopupPosition(this.pluginPopupAnchor, popupSize, scale), ImGuiCond.Always);
        ImGui.SetNextWindowSize(SnapToPixel(popupSize), ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(1.0f, 0.90f, 0.95f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.22f, 0.13f, 0.17f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.94f, 0.58f, 0.74f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1.0f, 0.88f, 0.95f, 0.86f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1.0f, 0.82f, 0.92f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1.0f, 0.86f, 0.92f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.88f, 0.66f, 0.80f, 0.38f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.88f, 0.58f, 0.76f, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.82f, 0.50f, 0.70f, 0.54f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(1.0f, 0.90f, 0.96f, 0.86f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(0.86f, 0.60f, 0.74f, 0.74f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.80f, 0.50f, 0.68f, 0.88f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, new Vector4(0.72f, 0.40f, 0.60f, 0.94f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10.0f * scale, 9.0f * scale));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 9.0f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5.0f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6.0f * scale, 5.0f * scale));

        try {
            if (ImGui.BeginPopup("AllHud 插件列表")) {
                DrawPluginListHeader(scale);
                DrawPluginListSearchRow(scale);

                var plugins = GetConfiguredPluginListEntries()
                    .Where(entry => string.IsNullOrWhiteSpace(this.pluginListFilter)
                                    || (entry.Plugin?.Name.Contains(this.pluginListFilter, StringComparison.OrdinalIgnoreCase) ?? false)
                                    || entry.InternalName.Contains(this.pluginListFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var listHeight = Math.Max(220.0f * scale, ImGui.GetTextLineHeightWithSpacing() * 14.0f);
                using (PushTaskBarPopupScrollbarStyle(scale)) {
                    if (ImGui.BeginChild("##AllHudPluginListBody", new Vector2(0.0f, listHeight), true)) {
                        if (this.config.PluginListInternalNames.Count == 0) {
                            ImGui.TextDisabled("还没有添加插件。");
                            ImGui.TextDisabled("在 AllHud 设置里，打开“插件列表”组件的“设置”添加插件。");
                        } else if (plugins.Count == 0) {
                            ImGui.TextDisabled("没有匹配的插件。");
                        } else {
                            DrawPluginListSection("已添加", plugins, scale);
                        }
                    }

                    ImGui.EndChild();
                }

                ImGui.EndPopup();
            }
        } finally {
            ImGui.PopStyleVar(4);
            ImGui.PopStyleColor(13);
        }
    }

    private void DrawPluginListHeader(float scale) {
        var rowStart = SnapToPixel(ImGui.GetCursorScreenPos());
        var rowHeight = MathF.Round(22.0f * scale);

        ImGui.SetCursorScreenPos(rowStart + new Vector2(0.0f, Math.Max(0.0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f)));
        ImGui.TextUnformatted("插件列表");
        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));
    }

    private void DrawPluginListSearchRow(float scale) {
        var buttonSize = MathF.Round(28.0f * scale);
        var spacing = 6.0f * scale;
        var rightInset = 4.0f * scale;
        var rowStart = SnapToPixel(ImGui.GetCursorScreenPos());
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var searchWidth = Math.Max(120.0f * scale, availableWidth - buttonSize - spacing - rightInset);

        ImGui.SetCursorScreenPos(rowStart);
        ImGui.SetNextItemWidth(searchWidth);
        ImGui.InputTextWithHint("##AllHudPluginFilter", "搜索插件...", ref this.pluginListFilter, 80);

        ImGui.SameLine(0.0f, spacing);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.96f, 0.74f, 0.86f, 0.48f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.92f, 0.58f, 0.76f, 0.64f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.82f, 0.42f, 0.66f, 0.76f));
        if (ImGui.Button("##AllHudDalamudSettings", new Vector2(buttonSize, buttonSize))) {
            this.commandManager.ProcessCommand("/xlsettings");
            ImGui.CloseCurrentPopup();
        }

        DrawSmallGearIcon(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale, ImGui.IsItemHovered() || ImGui.IsItemActive());

        ImGui.PopStyleColor(3);
        if (ImGui.IsItemHovered()) {
            DrawTaskBarTooltip("Dalamud 设置");
        }
    }

    private static void DrawSmallGearIcon(ImDrawListPtr drawList, Vector2 min, Vector2 max, float scale, bool active) {
        var center = SnapToPixel((min + max) * 0.5f);
        var radius = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.23f;
        var toothInner = radius + 1.6f * scale;
        var toothOuter = radius + 4.0f * scale;
        var color = ImGui.GetColorU32(active ? new Vector4(0.22f, 0.12f, 0.18f, 1.0f) : new Vector4(0.30f, 0.18f, 0.24f, 0.96f));
        var softColor = ImGui.GetColorU32(active ? new Vector4(0.22f, 0.12f, 0.18f, 0.24f) : new Vector4(0.30f, 0.18f, 0.24f, 0.16f));

        drawList.AddCircleFilled(center, radius + 2.2f * scale, softColor, 24);
        for (var tooth = 0; tooth < 8; tooth++) {
            var angle = MathF.PI * 2.0f * tooth / 8.0f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + dir * toothInner, center + dir * toothOuter, color, 2.0f * scale);
        }

        drawList.AddCircle(center, radius, color, 24, 2.0f * scale);
        drawList.AddCircle(center, MathF.Max(2.0f * scale, radius * 0.38f), color, 18, 1.8f * scale);
    }

    private List<PluginListEntry> GetConfiguredPluginListEntries() {
        var result = new List<PluginListEntry>(this.config.PluginListInternalNames.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var internalName in this.config.PluginListInternalNames) {
            var trimmed = internalName.Trim();
            if (trimmed.Length == 0 || !seen.Add(trimmed)) {
                continue;
            }

            var plugin = FindInstalledPlugin(trimmed);
            if (plugin is null || !plugin.IsLoaded || (!plugin.HasMainUi && !plugin.HasConfigUi)) {
                continue;
            }

            result.Add(new PluginListEntry(trimmed, plugin));
        }

        return result;
    }

    private void DrawPluginListSection(string label, IEnumerable<PluginListEntry> plugins, float scale) {
        var pluginList = plugins.ToList();
        if (pluginList.Count == 0) {
            return;
        }

        ImGui.TextDisabled(label);
        foreach (var entry in pluginList) {
            var plugin = entry.Plugin;
            var displayName = plugin?.Name ?? entry.InternalName;
            var isLoaded = plugin?.IsLoaded ?? false;
            var statusSuffix = plugin is null
                ? "（未安装）"
                : !plugin.IsLoaded
                    ? "（未加载）"
                    : string.Empty;
            var rowHeight = MathF.Round(26.0f * scale);
            var rowStart = SnapToPixel(ImGui.GetCursorScreenPos());
            var rowWidth = Math.Max(120.0f * scale, ImGui.GetContentRegionAvail().X);
            var hitMin = rowStart;
            var hitMax = SnapToPixel(rowStart + new Vector2(rowWidth, rowHeight));
            var visualMin = SnapToPixel(rowStart + new Vector2(2.0f * scale, 2.0f * scale));
            var visualMax = SnapToPixel(rowStart + new Vector2(rowWidth - 2.0f * scale, rowHeight - 2.0f * scale));

            var mousePos = ImGui.GetMousePos();
            var hovered = ImGui.IsWindowHovered()
                          && mousePos.X >= hitMin.X
                          && mousePos.X < hitMax.X
                          && mousePos.Y >= hitMin.Y
                          && mousePos.Y < hitMax.Y;
            var active = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            if (hovered || active) {
                var fill = active
                    ? new Vector4(0.90f, 0.58f, 0.76f, 0.38f)
                    : new Vector4(0.96f, 0.76f, 0.87f, 0.24f);
                var border = active
                    ? new Vector4(0.78f, 0.36f, 0.60f, 0.42f)
                    : new Vector4(0.88f, 0.56f, 0.72f, 0.24f);
                var drawList = ImGui.GetWindowDrawList();
                drawList.AddRectFilled(visualMin, visualMax, ImGui.GetColorU32(fill), 5.0f * scale);
                drawList.AddRect(visualMin, visualMax, ImGui.GetColorU32(border), 5.0f * scale, (ImDrawFlags)0, 1.0f * scale);
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                OpenPluginListEntry(plugin);
                ImGui.CloseCurrentPopup();
            }

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) {
                this.commandManager.ProcessCommand("/xlplugins");
                ImGui.CloseCurrentPopup();
            }

            if (plugin is not null) {
                DrawPluginListIconImage(plugin, rowStart, rowHeight, scale);
            } else {
                DrawPluginListIconFallback(ImGui.GetWindowDrawList(), displayName, false, rowStart, rowHeight, scale);
            }

            var textPos = SnapToPixel(new Vector2(rowStart.X + 32.0f * scale, rowStart.Y + Math.Max(0.0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f)));
            ImGui.GetWindowDrawList().AddText(textPos, ImGui.GetColorU32(isLoaded ? new Vector4(0.21f, 0.12f, 0.17f, 1.0f) : new Vector4(0.48f, 0.34f, 0.42f, 0.82f)), displayName + statusSuffix);

            ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));
        }
    }

    private void OpenPluginListEntry(Dalamud.Plugin.IExposedPlugin? plugin) {
        if (plugin is null) {
            this.commandManager.ProcessCommand("/xlplugins");
            return;
        }

        var io = ImGui.GetIO();
        if ((io.KeyShift || io.KeyCtrl) && plugin.HasConfigUi) {
            plugin.OpenConfigUi();
            return;
        }

        OpenPluginShortcut(plugin);
    }

    private void DrawPluginListIcon(Dalamud.Plugin.IExposedPlugin plugin, float scale) {
        var rowHeight = MathF.Round(26.0f * scale);
        DrawPluginListIconImage(plugin, ImGui.GetCursorScreenPos(), rowHeight, scale);
        ImGui.Dummy(new Vector2(MathF.Round(24.0f * scale), rowHeight));
    }

    private void DrawPluginListIconImage(Dalamud.Plugin.IExposedPlugin plugin, Vector2 rowStart, float rowHeight, float scale, float? overrideSize = null) {
        var drawList = ImGui.GetWindowDrawList();
        var size = overrideSize ?? MathF.Round(24.0f * scale);
        var offsetX = overrideSize.HasValue
            ? Math.Max(0.0f, (rowHeight - size) * 0.5f)
            : 2.0f * scale;
        var iconMin = SnapToPixel(rowStart + new Vector2(offsetX, Math.Max(0.0f, (rowHeight - size) * 0.5f)));
        var iconMax = iconMin + new Vector2(size, size);

        if (TryGetPluginIconTexture(plugin, out var texture) && texture is not null && TryGetPluginIconTextureHandle(plugin, texture, out var textureHandle)) {
            const float uvCrop = 0.035f;
            drawList.AddImage(textureHandle, iconMin, iconMax, new Vector2(uvCrop), new Vector2(1.0f - uvCrop), 0xFFFFFFFF);
            return;
        }

        DrawPluginIconFallback(drawList, plugin.Name, plugin.IsLoaded, iconMin, iconMax, scale);
    }

    private static void DrawPluginListIconFallback(ImDrawListPtr drawList, string name, bool isLoaded, Vector2 rowStart, float rowHeight, float scale) {
        var size = MathF.Round(24.0f * scale);
        var iconMin = SnapToPixel(rowStart + new Vector2(2.0f * scale, Math.Max(0.0f, (rowHeight - size) * 0.5f)));
        DrawPluginIconFallback(drawList, name, isLoaded, iconMin, iconMin + new Vector2(size, size), scale);
    }

    private static void DrawPluginIconFallback(ImDrawListPtr drawList, string name, bool isLoaded, Vector2 iconMin, Vector2 iconMax, float scale) {
        var size = iconMax.X - iconMin.X;
        var center = iconMin + new Vector2(size * 0.5f);
        var fill = isLoaded
            ? new Vector4(0.58f, 0.25f, 0.42f, 1.0f)
            : new Vector4(0.98f, 0.90f, 0.96f, 1.0f);
        var border = isLoaded
            ? new Vector4(0.36f, 0.12f, 0.25f, 1.0f)
            : new Vector4(0.58f, 0.25f, 0.42f, 1.0f);
        drawList.AddRectFilled(iconMin, iconMax, ImGui.GetColorU32(fill), 4.0f * scale);
        drawList.AddRect(iconMin, iconMax, ImGui.GetColorU32(border), 4.0f * scale, (ImDrawFlags)0, 1.0f * scale);

        var letter = string.IsNullOrWhiteSpace(name) ? "?" : name[..1].ToUpperInvariant();
        var textSize = ImGui.CalcTextSize(letter);
        var textColor = isLoaded
            ? ImGui.GetColorU32(new Vector4(1.0f, 0.94f, 0.98f, 1.0f))
            : ImGui.GetColorU32(new Vector4(0.34f, 0.14f, 0.24f, 1.0f));
        drawList.AddText(SnapToPixel(center - textSize * 0.5f), textColor, letter);
    }

    private bool TryGetPluginIconTextureHandle(Dalamud.Plugin.IExposedPlugin plugin, Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap texture, out ImTextureID handle) {
        try {
            handle = texture.Handle;
            return true;
        } catch (ObjectDisposedException) {
            ClearPluginIconTextureCache(plugin);
        } catch {
        }

        handle = default;
        return false;
    }

    private void ClearPluginIconTextureCache(Dalamud.Plugin.IExposedPlugin plugin) {
        if (!string.IsNullOrWhiteSpace(plugin.Manifest.IconUrl)) {
            this.pluginRemoteIconCache.TryRemove(plugin.Manifest.IconUrl, out _);
            this.pluginRemoteIconTasks.TryRemove(plugin.Manifest.IconUrl, out _);
            var remoteCachePath = GetRemotePluginIconCachePath(plugin.Manifest.IconUrl);
            this.pluginIconTextureCache.Remove(remoteCachePath);
            this.pluginIconTextureRetryAt.Remove(remoteCachePath);
            this.remotePluginIconRetryAt.Remove(plugin.Manifest.IconUrl);
        }

        var localIconPath = GetLocalPluginIconPath(plugin);
        if (!string.IsNullOrWhiteSpace(localIconPath)) {
            this.pluginIconTextureCache.Remove(localIconPath);
            this.pluginIconTextureRetryAt.Remove(localIconPath);
        }
    }

    private bool TryGetPluginIconTexture(Dalamud.Plugin.IExposedPlugin plugin, out Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? texture) {
        texture = null;
        var iconPath = GetLocalPluginIconPath(plugin);
        if (!string.IsNullOrWhiteSpace(iconPath)) {
            if (this.pluginIconTextureRetryAt.TryGetValue(iconPath, out var retryAt) && DateTime.UtcNow < retryAt) {
                return TryGetRemotePluginIconTexture(plugin, out texture, allowDownload: this.config.TaskBarDownloadPluginIcons);
            }

            if (!this.pluginIconTextureCache.TryGetValue(iconPath, out var sharedTexture)) {
                try {
                    sharedTexture = this.textureProvider.GetFromFile(iconPath);
                    this.pluginIconTextureCache[iconPath] = sharedTexture;
                    this.pluginIconTextureRetryAt.Remove(iconPath);
                } catch {
                    this.pluginIconTextureRetryAt[iconPath] = DateTime.UtcNow + PluginIconTextureRetryDelay;
                    return TryGetRemotePluginIconTexture(plugin, out texture, allowDownload: this.config.TaskBarDownloadPluginIcons);
                }
            }

            try {
                if (sharedTexture.TryGetWrap(out texture, out _) && texture is not null) {
                    this.pluginIconTextureRetryAt.Remove(iconPath);
                    return true;
                }
            } catch (ObjectDisposedException) {
                this.pluginIconTextureCache.Remove(iconPath);
            } catch {
            }

            this.pluginIconTextureRetryAt[iconPath] = DateTime.UtcNow + PluginIconTextureRetryDelay;
        }

        if (TryGetRemotePluginIconTexture(plugin, out texture, allowDownload: this.config.TaskBarDownloadPluginIcons)) {
            return true;
        }

        return false;
    }

    private bool TryGetRemotePluginIconTexture(Dalamud.Plugin.IExposedPlugin plugin, out Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? texture, bool allowDownload) {
        texture = null;
        var iconUrl = plugin.Manifest.IconUrl;
        if (string.IsNullOrWhiteSpace(iconUrl)) {
            return false;
        }

        if (this.pluginRemoteIconCache.TryGetValue(iconUrl, out var cachedTexture)) {
            texture = cachedTexture;
            return texture is not null;
        }

        if (TryGetCachedRemotePluginIconTexture(iconUrl, out texture)) {
            return true;
        }

        if (!allowDownload) {
            return false;
        }

        if (this.remotePluginIconRetryAt.TryGetValue(iconUrl, out var retryAt) && DateTime.UtcNow < retryAt) {
            return false;
        }

        this.remotePluginIconRetryAt[iconUrl] = DateTime.UtcNow + RemotePluginIconRetryDelay;
        this.pluginRemoteIconTasks.GetOrAdd(iconUrl, LoadRemotePluginIconAsync);
        return false;
    }

    private bool TryGetCachedRemotePluginIconTexture(string iconUrl, out Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? texture) {
        texture = null;
        var now = DateTime.UtcNow;
        if (this.missingCachedRemotePluginIconRetryAt.TryGetValue(iconUrl, out var retryAt) && now < retryAt) {
            return false;
        }

        var cachePath = GetRemotePluginIconCachePath(iconUrl);
        if (!File.Exists(cachePath)) {
            this.missingCachedRemotePluginIconRetryAt[iconUrl] = now + RemotePluginIconRetryDelay;
            return false;
        }

        this.missingCachedRemotePluginIconRetryAt.Remove(iconUrl);

        try {
            if (!this.pluginIconTextureCache.TryGetValue(cachePath, out var sharedTexture)) {
                sharedTexture = this.textureProvider.GetFromFile(cachePath);
                this.pluginIconTextureCache[cachePath] = sharedTexture;
            }

            if (sharedTexture.TryGetWrap(out texture, out _) && texture is not null) {
                this.pluginRemoteIconCache[iconUrl] = texture;
                this.remotePluginIconRetryAt.Remove(iconUrl);
                return true;
            }
        } catch {
            this.pluginIconTextureCache.Remove(cachePath);
            this.pluginIconTextureRetryAt[cachePath] = now + PluginIconTextureRetryDelay;
        }

        return false;
    }

    private async Task<Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap?> LoadRemotePluginIconAsync(string iconUrl) {
        try {
            var bytes = await this.httpClient.GetByteArrayAsync(iconUrl).ConfigureAwait(false);
            TryWriteRemotePluginIconCache(iconUrl, bytes);
            var texture = await this.textureProvider.CreateFromImageAsync(bytes, $"AllHud plugin icon {iconUrl}").ConfigureAwait(false);
            this.pluginRemoteIconCache[iconUrl] = texture;
            this.remotePluginIconRetryAt.Remove(iconUrl);
            return texture;
        } catch {
            this.remotePluginIconRetryAt[iconUrl] = DateTime.UtcNow + RemotePluginIconRetryDelay;
            return null;
        } finally {
            this.pluginRemoteIconTasks.TryRemove(iconUrl, out _);
        }
    }

    private void TryWriteRemotePluginIconCache(string iconUrl, byte[] bytes) {
        try {
            Directory.CreateDirectory(this.pluginIconCacheDirectory);
            File.WriteAllBytes(GetRemotePluginIconCachePath(iconUrl), bytes);
            this.missingCachedRemotePluginIconRetryAt.Remove(iconUrl);
        } catch {
        }
    }

    private string GetRemotePluginIconCachePath(string iconUrl) {
        if (this.remotePluginIconCachePathCache.TryGetValue(iconUrl, out var cachedPath)) {
            return cachedPath;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(iconUrl))).ToLowerInvariant();
        var path = Path.Combine(this.pluginIconCacheDirectory, hash + GetRemotePluginIconCacheExtension(iconUrl));
        this.remotePluginIconCachePathCache[iconUrl] = path;
        return path;
    }

    private static string GetRemotePluginIconCacheExtension(string iconUrl) {
        try {
            var extension = Path.GetExtension(new Uri(iconUrl).AbsolutePath);
            return extension.ToLowerInvariant() switch {
                ".png" or ".jpg" or ".jpeg" or ".webp" => extension.ToLowerInvariant(),
                _ => ".png",
            };
        } catch {
            return ".png";
        }
    }

    private string? GetLocalPluginIconPath(Dalamud.Plugin.IExposedPlugin plugin) {
        var now = DateTime.UtcNow;
        if (this.pluginIconPathCache.TryGetValue(plugin.InternalName, out var cachedEntry) && now < cachedEntry.RetryAt) {
            return cachedEntry.Path;
        }

        var iconPath = FindLocalPluginIconPath(plugin);
        this.pluginIconPathCache[plugin.InternalName] = new PluginIconPathCacheEntry(
            string.IsNullOrWhiteSpace(iconPath) ? null : iconPath,
            string.IsNullOrWhiteSpace(iconPath) ? now + PluginIconPathMissRetryDelay : DateTime.MaxValue);
        return iconPath;
    }

    private string? FindLocalPluginIconPath(Dalamud.Plugin.IExposedPlugin plugin) {
        try {
            if (plugin.InternalName.Equals("AllHud", StringComparison.OrdinalIgnoreCase)) {
                var ownIconPath = FindPluginIconInDirectory(Path.GetDirectoryName(this.pluginInterface.AssemblyLocation.FullName) ?? string.Empty, plugin);
                if (!string.IsNullOrWhiteSpace(ownIconPath)) {
                    return ownIconPath;
                }
            }

            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncherCN",
                "installedPlugins",
                plugin.InternalName);
            if (!Directory.Exists(baseDir)) {
                return null;
            }

            var baseIconPath = FindDirectPluginIconInDirectory(baseDir);
            if (!string.IsNullOrWhiteSpace(baseIconPath)) {
                return baseIconPath;
            }

            var versionDir = Directory.EnumerateDirectories(baseDir)
                .Select(directory => {
                    var name = Path.GetFileName(directory);
                    return Version.TryParse(name, out var version)
                        ? (Path: directory, Version: version)
                        : (Path: directory, Version: (Version?)null);
                })
                .Where(entry => entry.Version is not null)
                .OrderByDescending(entry => entry.Version)
                .Select(entry => entry.Path)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(versionDir) || !Directory.Exists(versionDir)) {
                return null;
            }

            return FindPluginIconInDirectory(versionDir, plugin);
        } catch {
            return null;
        }
    }

    private static string? FindPluginIconInDirectory(string directory, Dalamud.Plugin.IExposedPlugin plugin) {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) {
            return null;
        }

        var directIconPath = FindDirectPluginIconInDirectory(directory);
        if (!string.IsNullOrWhiteSpace(directIconPath)) {
            return directIconPath;
        }

        return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(IsSupportedPluginIconFile)
            .Select(path => new { Path = path, Score = GetPluginIconCandidateScore(path, plugin) })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path.Length)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    private static string? FindDirectPluginIconInDirectory(string directory) {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) {
            return null;
        }

        foreach (var file in new[] { "icon.png", "icon.jpg", "icon.jpeg", "icon.webp" }) {
            var directPath = Path.Combine(directory, file);
            if (File.Exists(directPath)) {
                return directPath;
            }

            var imagePath = Path.Combine(directory, "images", file);
            if (File.Exists(imagePath)) {
                return imagePath;
            }

            var assetPath = Path.Combine(directory, "Assets", file);
            if (File.Exists(assetPath)) {
                return assetPath;
            }
        }

        return null;
    }

    private static bool IsSupportedPluginIconFile(string path) {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPluginIconCandidateScore(string path, Dalamud.Plugin.IExposedPlugin plugin) {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var directoryName = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        var relativePath = path.Replace('\\', '/');
        var score = 0;

        if (fileName.Equals("icon", StringComparison.OrdinalIgnoreCase)) {
            score += 100;
        }

        if (fileName.Contains("icon", StringComparison.OrdinalIgnoreCase)) {
            score += 80;
        }

        if (fileName.Contains("logo", StringComparison.OrdinalIgnoreCase)) {
            score += 65;
        }

        if (fileName.Contains("shortcut", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("title", StringComparison.OrdinalIgnoreCase)) {
            score += 45;
        }

        if (fileName.Contains(plugin.InternalName, StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(plugin.Name, StringComparison.OrdinalIgnoreCase)) {
            score += 70;
        }

        if (directoryName.Equals("Assets", StringComparison.OrdinalIgnoreCase)
            || directoryName.Equals("Images", StringComparison.OrdinalIgnoreCase)
            || directoryName.Equals("Resources", StringComparison.OrdinalIgnoreCase)) {
            score += 12;
        }

        if (relativePath.Contains("/Lang", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/Localization", StringComparison.OrdinalIgnoreCase)) {
            score -= 40;
        }

        return score;
    }
}

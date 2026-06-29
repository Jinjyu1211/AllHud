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
using LuminaTomestonesItem = Lumina.Excel.Sheets.TomestonesItem;

namespace AllHud.Windows;

public sealed partial class OverlayRenderer {
    private void OpenTaskBarItemPopup(TaskBarItem item) {
        if (string.IsNullOrWhiteSpace(item.PopupId)) {
            return;
        }

        if (item.PopupId == "AllHud 主菜单") {
            this.mainMenuPopupAnchor = CaptureTaskBarPopupAnchor();
            ImGui.OpenPopup(item.PopupId);
        } else if (!string.IsNullOrWhiteSpace(item.QuickMenuComponentId)) {
            this.quickMenuPopupAnchor = CaptureTaskBarPopupAnchor();
            this.activeQuickMenuComponentId = item.QuickMenuComponentId;
            ImGui.OpenPopup(GetQuickMenuPopupId(item.QuickMenuComponentId));
        } else if (item.PopupId == "AllHud 音量控制") {
            this.volumePopupAnchor = CaptureTaskBarPopupAnchor();
            ImGui.OpenPopup(item.PopupId);
        } else if (item.PopupId == "AllHud 插件列表") {
            this.pluginPopupAnchor = CaptureTaskBarPopupAnchor();
            this.pendingPluginListPopupOpenFrames = 2;
        } else {
            this.simplePopupAnchor = CaptureTaskBarPopupAnchor();
            ImGui.OpenPopup(item.PopupId);
        }
    }

    private static TaskBarPopupAnchor CaptureTaskBarPopupAnchor() {
        return new TaskBarPopupAnchor(ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
    }

    private static Vector2 GetTaskBarPopupPosition(TaskBarPopupAnchor anchor, Vector2 popupSize, float scale, float fallbackHeight = 0.0f) {
        var viewport = ImGui.GetMainViewport();
        var gap = 6.0f * scale;
        var fitSize = new Vector2(
            Math.Max(1.0f, popupSize.X),
            Math.Max(1.0f, popupSize.Y > 0.0f ? popupSize.Y : fallbackHeight));
        var popupPos = new Vector2(anchor.Min.X, anchor.Max.Y + gap);

        if (popupPos.Y + fitSize.Y > viewport.WorkPos.Y + viewport.WorkSize.Y) {
            popupPos.Y = anchor.Min.Y - fitSize.Y - gap;
        }

        if (popupPos.X + fitSize.X > viewport.WorkPos.X + viewport.WorkSize.X) {
            popupPos.X = anchor.Max.X - fitSize.X;
        }

        popupPos.X = Math.Clamp(popupPos.X, viewport.WorkPos.X, viewport.WorkPos.X + viewport.WorkSize.X - fitSize.X);
        popupPos.Y = Math.Clamp(popupPos.Y, viewport.WorkPos.Y, viewport.WorkPos.Y + viewport.WorkSize.Y - fitSize.Y);
        return SnapToPixel(popupPos);
    }

    private static string GetQuickMenuPopupId(string componentId) {
        return $"AllHud 快捷菜单##{componentId}";
    }

    private void DrawQuickMenuPopup(float opacity, float scale) {
        if (string.IsNullOrWhiteSpace(this.activeQuickMenuComponentId)) {
            return;
        }

        var popupId = GetQuickMenuPopupId(this.activeQuickMenuComponentId);
        if (!ImGui.IsPopupOpen(popupId)) {
            return;
        }

        if (!this.config.QuickMenus.TryGetValue(this.activeQuickMenuComponentId, out var menu) || menu is null) {
            return;
        }

        var entries = BuildQuickMenuRuntimeItems(menu);
        var rowHeight = MathF.Round(38.0f * scale);
        var width = MathF.Round(240.0f * scale);
        var windowPadding = MathF.Round(8.0f * scale);
        var itemSpacing = MathF.Round(4.0f * scale);
        var contentHeight = entries.Count > 0
            ? entries.Count * rowHeight + Math.Max(0, entries.Count - 1) * itemSpacing
            : rowHeight;
        var height = MathF.Round(contentHeight + windowPadding * 2.0f);
        ImGui.SetNextWindowPos(GetTaskBarPopupPosition(this.quickMenuPopupAnchor, new Vector2(width, height), scale), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, height), ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(1.0f, 0.90f, 0.95f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.22f, 0.13f, 0.17f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.94f, 0.58f, 0.74f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(9.0f * scale, windowPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 8.0f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(itemSpacing, itemSpacing));

        if (ImGui.BeginPopup(popupId, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
            if (entries.Count == 0) {
                ImGui.TextDisabled("没有项目");
            } else {
                for (var index = 0; index < entries.Count; index++) {
                    DrawQuickMenuEntry(entries[index].Item, entries[index].Label, index, rowHeight, width - 18.0f * scale, opacity, scale);
                }
            }

            ImGui.EndPopup();
        }

        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(3);
    }

    private readonly record struct QuickMenuRuntimeItem(string Label, TaskBarItem Item);

    private List<QuickMenuRuntimeItem> BuildQuickMenuRuntimeItems(QuickMenuDefinition menu) {
        var result = new List<QuickMenuRuntimeItem>();
        foreach (var componentId in menu.ComponentOrder) {
            if (Configuration.IsQuickMenuComponentId(componentId)) {
                continue;
            }

            TaskBarItem item;
            string label;
            switch (Configuration.GetComponentBaseId(componentId)) {
                case Configuration.TaskBarComponentVolume:
                    item = new TaskBarItem(string.Empty, GetVolumeTaskBarTooltip(), _ => { }, HandleVolumeTaskBarClick, "AllHud 音量控制", true, DrawIcon: TaskBarDrawIcon.Volume);
                    label = "音量控制";
                    break;
                case Configuration.TaskBarComponentPluginList:
                    item = new TaskBarItem(string.Empty, "左键查看已添加插件，右键打开 Dalamud 插件管理器", _ => { }, _ => this.commandManager.ProcessCommand("/xlplugins"), "AllHud 插件列表", DrawIcon: TaskBarDrawIcon.PluginList);
                    label = "插件列表";
                    break;
                case Configuration.TaskBarComponentInventory:
                    item = CreateInventoryTaskBarItem(false);
                    label = "背包";
                    break;
                case Configuration.TaskBarComponentSaddlebag:
                    item = CreateInventoryTaskBarItem(true);
                    label = "陆行鸟鞍囊";
                    break;
                case Configuration.TaskBarComponentTeleport:
                    item = CreateTeleportTaskBarItem();
                    label = "传送";
                    break;
                case Configuration.TaskBarComponentCoordinates:
                    item = CreateCoordinatesTaskBarItem();
                    label = "坐标";
                    break;
                case Configuration.TaskBarComponentGearsetSwitcher:
                    item = CreateGearsetSwitcherTaskBarItem();
                    label = "套装切换";
                    break;
                case Configuration.TaskBarComponentCurrency:
                    item = CreateCurrencyTaskBarItem();
                    label = "货币";
                    break;
                case Configuration.TaskBarComponentPluginShortcut:
                    if (!TryCreatePluginShortcutTaskBarItem(componentId, out item)) {
                        continue;
                    }

                    label = item.Tooltip;
                    break;
                case Configuration.TaskBarComponentCustomShortcut:
                    if (!TryCreateCustomShortcutTaskBarItem(componentId, out item)) {
                        continue;
                    }

                    label = item.Tooltip;
                    break;
                default:
                    continue;
            }

            result.Add(new QuickMenuRuntimeItem(string.IsNullOrWhiteSpace(label) ? "项目" : label, item));
        }

        return result;
    }

    private void DrawQuickMenuEntry(TaskBarItem item, string label, int index, float rowHeight, float rowWidth, float opacity, float scale) {
        var drawList = ImGui.GetWindowDrawList();
        var rowMin = SnapToPixel(ImGui.GetCursorScreenPos());
        var rowSize = SnapToPixel(new Vector2(rowWidth, rowHeight));
        ImGui.InvisibleButton($"##QuickMenuEntry_{index}", rowSize);
        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        if (hovered && item.OnClick is not null) {
            if (!string.IsNullOrWhiteSpace(item.Tooltip)) {
                DrawTaskBarTooltip(item.Tooltip);
            }
        }

        DrawTaskBarItemCard(drawList, rowMin, rowSize, index, hovered, active, item.OnClick is not null, false, opacity, scale);
        var iconSize = MathF.Round(28.0f * scale);
        var iconMin = SnapToPixel(rowMin + new Vector2(8.0f * scale, Math.Max(0.0f, (rowHeight - iconSize) * 0.5f)));
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        var iconColor = ImGui.GetColorU32(new Vector4(0.24f, 0.14f, 0.20f, opacity));

        if (item.DrawIcon != TaskBarDrawIcon.None) {
            DrawTaskBarCustomIcon(drawList, iconMin, iconSize, item.DrawIcon, opacity, scale);
        } else if (item.IsDalamudIcon) {
            DrawTaskBarDalamudIcon(drawList, iconMin, iconSize, opacity, scale);
        } else if (!string.IsNullOrWhiteSpace(item.PluginShortcutInternalName)) {
            DrawPluginShortcutIcon(item, iconMin, iconSize, scale);
        } else if (item.GameIconId != 0) {
            if (!DrawGameIconImage(drawList, item.GameIconId, iconMin, iconMax, true, true)) {
                drawList.AddRect(iconMin, iconMax, iconColor, 4.0f * scale);
            }
        } else {
            drawList.AddCircle(iconMin + new Vector2(iconSize * 0.5f), iconSize * 0.32f, iconColor, 18, Math.Max(1.0f, 1.5f * scale));
        }

        var textPos = SnapToPixel(new Vector2(iconMax.X + 8.0f * scale, rowMin.Y + Math.Max(0.0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f)));
        drawList.AddText(textPos, ImGui.GetColorU32(new Vector4(0.24f, 0.14f, 0.20f, opacity)), label);

        if (item.OnClick is not null && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            item.OnClick.Invoke(CreateDtrInteractionEvent(MouseClickType.Left));
            OpenTaskBarItemPopup(item);
            if (string.IsNullOrWhiteSpace(item.PopupId) && string.IsNullOrWhiteSpace(item.QuickMenuComponentId)) {
                ImGui.CloseCurrentPopup();
            }
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            var rightClick = item.OnRightClick ?? item.OnClick;
            rightClick?.Invoke(CreateDtrInteractionEvent(MouseClickType.Right));
            if (item.OnRightClick is not null) {
                ImGui.CloseCurrentPopup();
            }
        }
    }

    private string GetTaskBarTimeText() {
        var showLocal = this.config.TaskBarShowLocalTime;
        var showEt = this.config.TaskBarShowEorzeaTime;
        if (showLocal && showEt) {
            return $"LT {DateTime.Now:HH:mm}\nET {GetEorzeaTimeText()}";
        }

        if (showLocal) {
            return $"LT {DateTime.Now:HH:mm}";
        }

        return showEt ? $"ET {GetEorzeaTimeText()}" : string.Empty;
    }

    private string GetTaskBarTimeMeasureText() {
        var showLocal = this.config.TaskBarShowLocalTime;
        var showEt = this.config.TaskBarShowEorzeaTime;
        if (showLocal && showEt) {
            return "LT 00:00\nET 00:00";
        }

        if (showLocal) {
            return "LT 00:00";
        }

        return showEt ? "ET 00:00" : string.Empty;
    }

    private static string GetDtrMeasureText(string text) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length + 8);
        var digitRunLength = 0;
        foreach (var ch in text) {
            if (char.IsDigit(ch)) {
                digitRunLength++;
                continue;
            }

            if (digitRunLength > 0) {
                builder.Append('0', Math.Max(4, digitRunLength));
                digitRunLength = 0;
            }

            builder.Append(ch);
        }

        if (digitRunLength > 0) {
            builder.Append('0', Math.Max(4, digitRunLength));
        }

        return builder.ToString();
    }

    private float GetTaskBarTimeTextScale() {
        return this.config.TaskBarShowLocalTime && this.config.TaskBarShowEorzeaTime ? 0.82f : 1.0f;
    }

    private TaskBarItem CreateInventoryTaskBarItem(bool saddlebag) {
        var (used, total) = GetCachedInventoryUsage(saddlebag);
        var label = saddlebag ? "鞍囊" : "背包";
        return new TaskBarItem(
            string.Empty,
            $"{label} {used}/{total}",
            _ => OpenInventoryWindow(saddlebag),
            GameIconId: GetInventoryIconId(saddlebag, used, total));
    }

    private TaskBarItem CreateTeleportTaskBarItem() {
        return new TaskBarItem(string.Empty, "打开游戏传送窗口", _ => OpenTeleportWindow(), GameIconId: GetGeneralActionIconId(7));
    }

    private uint GetGeneralActionIconId(uint actionId) {
        try {
            return (uint)this.dataManager.GetExcelSheet<LuminaGeneralAction>().GetRow(actionId).Icon;
        } catch {
            return 0;
        }
    }

    private TaskBarItem CreateCoordinatesTaskBarItem() {
        var text = GetCoordinatesText();
        return new TaskBarItem(text, $"{text}\n左键打开地图", _ => OpenMapWindow(), MeasureText: GetCoordinatesMeasureText(), TextScale: 0.88f);
    }

    private TaskBarItem CreateGearsetSwitcherTaskBarItem() {
        var displayInfo = GetCachedGearsetDisplayInfo();
        var text = GetGearsetTaskBarText(displayInfo);
        return new TaskBarItem(
            text,
            "左键打开套装切换列表",
            _ => { },
            PopupId: GearsetPopupId,
            MeasureText: GetGearsetMeasureText(displayInfo),
            GameIconId: displayInfo is not null ? GetGearsetIconId(displayInfo.Value.ClassJobId) : 0);
    }

    private TaskBarItem CreateCurrencyTaskBarItem() {
        var currency = GetSelectedCurrencyDisplayInfo();
        var count = GetCachedCurrencyCount(currency.ItemId);
        var countText = count >= 0 ? $"{count:N0}" : "--";
        var text = this.config.TaskBarCurrencyShowName ? $"{currency.Name} {countText}" : countText;
        return new TaskBarItem(text, $"{currency.Name}：{countText}", _ => { }, _ => OpenCurrenciesWindow(), PopupId: CurrencyPopupId, MeasureText: this.config.TaskBarCurrencyShowName ? $"{currency.Name} 000,000,000" : "000,000,000", GameIconId: currency.IconId);
    }

    private bool TryCreateQuickMenuTaskBarItem(string componentId, out TaskBarItem item) {
        item = default;
        if (!this.config.QuickMenus.TryGetValue(componentId, out var menu) || menu is null) {
            return false;
        }

        var name = string.IsNullOrWhiteSpace(menu.Name) ? "快捷菜单" : menu.Name.Trim();
        item = new TaskBarItem(
            string.Empty,
            name,
            _ => { },
            PopupId: GetQuickMenuPopupId(componentId),
            GameIconId: menu.IconId,
            DrawIcon: menu.IconId == 0 ? TaskBarDrawIcon.QuickMenu : TaskBarDrawIcon.None,
            QuickMenuComponentId: componentId);
        return true;
    }

    private bool TryCreatePluginShortcutTaskBarItem(string componentId, out TaskBarItem item) {
        item = default;
        var internalName = GetPluginShortcutInternalName(componentId);
        if (string.IsNullOrWhiteSpace(internalName)) {
            return false;
        }

        var plugin = FindInstalledPlugin(internalName);
        if (plugin is null) {
            return false;
        }

        item = new TaskBarItem(
            string.Empty,
            plugin.Name,
            _ => OpenPluginShortcut(plugin),
            _ => this.commandManager.ProcessCommand("/xlplugins"),
            PluginShortcutInternalName: plugin.InternalName,
            PluginShortcutPlugin: plugin);
        return true;
    }

    private bool TryCreateCustomShortcutTaskBarItem(string componentId, out TaskBarItem item) {
        item = default;
        if (!this.config.CustomShortcuts.TryGetValue(componentId, out var shortcut) || shortcut is null) {
            return false;
        }

        var name = string.IsNullOrWhiteSpace(shortcut.Name) ? "快捷方式" : shortcut.Name.Trim();
        var command = shortcut.Command?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command)) {
            return false;
        }

        item = new TaskBarItem(
            shortcut.IconId == 0 ? name : string.Empty,
            name,
            _ => ExecuteCustomShortcut(command),
            GameIconId: shortcut.IconId,
            MeasureText: shortcut.IconId == 0 ? name : string.Empty);
        return true;
    }

    private void ExecuteCustomShortcut(string commandText) {
        foreach (var rawLine in commandText.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')) {
            var line = rawLine.Trim();
            if (line.Length == 0) {
                continue;
            }

            this.commandManager.ProcessCommand(line.StartsWith("/", StringComparison.Ordinal) ? line : "/" + line);
        }
    }

    private string GetPluginShortcutInternalName(string componentId) {
        return this.config.PluginShortcutInternalNames.TryGetValue(componentId, out var internalName)
            ? internalName
            : this.config.TaskBarPluginShortcutInternalName;
    }

    private Dalamud.Plugin.IExposedPlugin? FindInstalledPlugin(string internalName) {
        if (DateTime.UtcNow >= this.nextPluginLookupRefreshAt) {
            this.installedPluginLookupCache.Clear();
            foreach (var plugin in this.pluginInterface.InstalledPlugins) {
                if (string.IsNullOrWhiteSpace(plugin.InternalName)) {
                    continue;
                }

                if (!this.installedPluginLookupCache.TryGetValue(plugin.InternalName, out var existingPlugin)
                    || IsPreferredPluginCandidate(plugin, existingPlugin)) {
                    this.installedPluginLookupCache[plugin.InternalName] = plugin;
                }
            }

            this.nextPluginLookupRefreshAt = DateTime.UtcNow + PluginLookupCacheDuration;
        }

        return this.installedPluginLookupCache.TryGetValue(internalName, out var cachedPlugin)
            ? cachedPlugin
            : null;
    }

    private void OpenPluginShortcut(Dalamud.Plugin.IExposedPlugin plugin) {
        try {
            if (plugin.HasMainUi) {
                plugin.OpenMainUi();
            } else if (plugin.HasConfigUi) {
                plugin.OpenConfigUi();
            } else {
                this.commandManager.ProcessCommand("/xlplugins");
            }
        } catch {
            this.commandManager.ProcessCommand("/xlplugins");
        }
    }

    private static bool IsPreferredPluginCandidate(Dalamud.Plugin.IExposedPlugin candidate, Dalamud.Plugin.IExposedPlugin current) {
        var candidateScore = GetPluginCandidateScore(candidate);
        var currentScore = GetPluginCandidateScore(current);
        if (candidateScore != currentScore) {
            return candidateScore > currentScore;
        }

        return string.Compare(candidate.Name, current.Name, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static int GetPluginCandidateScore(Dalamud.Plugin.IExposedPlugin plugin) {
        var score = 0;
        if (plugin.IsLoaded) {
            score += 100;
        }

        if (plugin.IsDev) {
            score += 50;
        }

        if (plugin.HasMainUi) {
            score += 10;
        }

        if (plugin.HasConfigUi) {
            score += 5;
        }

        return score;
    }

    private void DrawPluginShortcutIcon(TaskBarItem item, Vector2 pos, float size, float scale) {
        var plugin = item.PluginShortcutPlugin ?? FindInstalledPlugin(item.PluginShortcutInternalName);
        if (plugin is null) {
            return;
        }

        DrawPluginListIconImage(plugin, pos, size, scale, MathF.Round(TaskBarPluginShortcutIconSize * scale));
    }

    private static uint GetInventoryIconId(bool saddlebag, int used, int total) {
        if (total > 0) {
            var free = total - used;
            if (free <= 1) {
                return 60074;
            }

            if (free <= 6) {
                return 60073;
            }
        }

        return saddlebag ? 74u : 2u;
    }

    private void OpenInventoryWindow(bool saddlebag) {
        if (!saddlebag) {
            OpenPlayerInventory();
            return;
        }

        var commandId = this.saddlebagMainCommandId ??= FindMainCommandId("陆行鸟鞍囊", "鞍囊", "陆行鸟背包", "Saddlebag");
        if (commandId.HasValue) {
            ExecuteMainCommand(commandId.Value);
        }
    }

    private void OpenMapWindow() {
        var commandId = this.mapMainCommandId ??= FindMainCommandId("地图", "Map");
        if (commandId.HasValue) {
            ExecuteMainCommand(commandId.Value);
        }
    }

    private static unsafe void OpenPlayerInventory() {
        var uiModule = UIModule.Instance();
        if (uiModule is not null) {
            if (uiModule->IsInventoryOpen()) {
                uiModule->CloseInventory();
            } else {
                uiModule->OpenInventory(0);
            }
        }
    }

    private uint? FindMainCommandId(params string[] names) {
        foreach (var command in this.dataManager.GetExcelSheet<LuminaMainCommand>()) {
            var displayName = GetMainCommandDisplayName(command.RowId, GetExcelText(command.Name.ExtractText()));
            if (string.IsNullOrWhiteSpace(displayName)) {
                continue;
            }

            if (names.Any(name => displayName.Contains(name, StringComparison.OrdinalIgnoreCase))) {
                return command.RowId;
            }
        }

        return null;
    }

    private static unsafe void ExecuteMainCommand(uint commandId) {
        var uiModule = UIModule.Instance();
        if (uiModule is not null) {
            uiModule->ExecuteMainCommand(commandId);
        }
    }

    private (int Used, int Total) GetInventoryUsage(ReadOnlySpan<GameInventoryType> inventoryTypes) {
        var used = 0;
        var total = 0;
        foreach (var inventoryType in inventoryTypes) {
            try {
                var items = this.gameInventory.GetInventoryItems(inventoryType);
                foreach (var item in items) {
                    total++;
                    if (!item.IsEmpty) {
                        used++;
                    }
                }
            } catch {
            }
        }

        return (used, total);
    }

    private (int Used, int Total) GetCachedInventoryUsage(bool saddlebag) {
        var now = DateTime.UtcNow;
        if (now >= this.nextInventoryUsageRefreshAt) {
            this.cachedInventoryUsage = GetInventoryUsage([GameInventoryType.Inventory1, GameInventoryType.Inventory2, GameInventoryType.Inventory3, GameInventoryType.Inventory4]);
            this.cachedSaddlebagUsage = GetInventoryUsage([GameInventoryType.SaddleBag1, GameInventoryType.SaddleBag2, GameInventoryType.PremiumSaddleBag1, GameInventoryType.PremiumSaddleBag2]);
            this.nextInventoryUsageRefreshAt = now + InventoryUsageCacheDuration;
        }

        return saddlebag ? this.cachedSaddlebagUsage : this.cachedInventoryUsage;
    }

    private string GetCoordinatesText() {
        var now = DateTime.UtcNow;
        if (this.cachedCoordinatesText is not null && now < this.nextCoordinatesTextRefreshAt) {
            return this.cachedCoordinatesText;
        }

        this.cachedCoordinatesText = BuildCoordinatesText();
        this.nextCoordinatesTextRefreshAt = now + CoordinatesTextCacheDuration;
        return this.cachedCoordinatesText;
    }

    private string BuildCoordinatesText() {
        var showTerritory = this.config.TaskBarShowCoordinatesTerritory || !this.config.TaskBarShowCoordinatesPosition;
        var showPosition = this.config.TaskBarShowCoordinatesPosition || !this.config.TaskBarShowCoordinatesTerritory;
        var territory = GetTerritoryName();
        var player = this.objectTable.LocalPlayer;
        var positionText = player is null
            ? "坐标 --"
            : $"X{player.Position.X:0} · Y{player.Position.Y:0} · Z{player.Position.Z:0}";

        if (showTerritory && showPosition) {
            return $"{territory}\n{positionText}";
        }

        if (showTerritory) {
            return territory;
        }

        if (showPosition) {
            return positionText;
        }

        return territory;
    }

    private string GetCoordinatesMeasureText() {
        var showTerritory = this.config.TaskBarShowCoordinatesTerritory || !this.config.TaskBarShowCoordinatesPosition;
        var showPosition = this.config.TaskBarShowCoordinatesPosition || !this.config.TaskBarShowCoordinatesTerritory;
        var territory = GetTerritoryName();
        if (showTerritory && showPosition) {
            return $"{territory}\nX000 · Y000 · Z000";
        }

        return showTerritory ? territory : "X000 · Y000 · Z000";
    }

    private string GetTerritoryName() {
        var territoryId = this.clientState.TerritoryType;
        if (territoryId == this.cachedTerritoryNameId) {
            return this.cachedTerritoryName;
        }

        this.cachedTerritoryName = ResolveTerritoryName(territoryId);
        this.cachedTerritoryNameId = territoryId;
        return this.cachedTerritoryName;
    }

    private string ResolveTerritoryName(uint territoryId) {
        if (territoryId == 0) {
            return "区域 --";
        }

        try {
            if (this.dataManager.GetExcelSheet<LuminaTerritoryType>().TryGetRow(territoryId, out var row)) {
                var name = row.PlaceName.ValueNullable?.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(name)) {
                    return name;
                }
            }
        } catch {
        }

        return $"区域 #{territoryId}";
    }

    private unsafe string GetGearsetTaskBarText(GearsetDisplayInfo? displayInfo = null) {
        if (displayInfo is not null) {
            var cache = this.gearsetTaskBarCache;
            var metadata = GetGearsetJobMetadata(displayInfo.Value.ClassJobId);
            var jobLevel = GetGearsetJobLevel(metadata);
            if (cache is { } cached
                && cached.GearsetId == displayInfo.Value.GearsetId
                && cached.ClassJobId == displayInfo.Value.ClassJobId
                && cached.JobLevel == jobLevel
                && cached.ShowNumber == this.config.TaskBarGearsetShowNumber
                && cached.ShowName == this.config.TaskBarGearsetShowName
                && cached.ShowLevel == this.config.TaskBarGearsetShowLevel) {
                return cached.Text;
            }

            var text = BuildGearsetTaskBarJobText(displayInfo.Value, metadata, jobLevel);
            var measureText = BuildGearsetTaskBarJobMeasureText(displayInfo.Value, metadata, jobLevel);
            this.gearsetTaskBarCache = new GearsetTaskBarCache(displayInfo.Value.GearsetId, displayInfo.Value.ClassJobId, jobLevel, this.config.TaskBarGearsetShowNumber, this.config.TaskBarGearsetShowName, this.config.TaskBarGearsetShowLevel, text, measureText, metadata.IconId);
            return text;
        }

        return "套装切换";
    }

    private unsafe string GetGearsetMeasureText(GearsetDisplayInfo? displayInfo = null) {
        if (displayInfo is not null) {
            var cache = this.gearsetTaskBarCache;
            var metadata = GetGearsetJobMetadata(displayInfo.Value.ClassJobId);
            var jobLevel = GetGearsetJobLevel(metadata);
            if (cache is { } cached
                && cached.GearsetId == displayInfo.Value.GearsetId
                && cached.ClassJobId == displayInfo.Value.ClassJobId
                && cached.JobLevel == jobLevel
                && cached.ShowNumber == this.config.TaskBarGearsetShowNumber
                && cached.ShowName == this.config.TaskBarGearsetShowName
                && cached.ShowLevel == this.config.TaskBarGearsetShowLevel) {
                return cached.MeasureText;
            }

            var text = BuildGearsetTaskBarJobText(displayInfo.Value, metadata, jobLevel);
            var measureText = BuildGearsetTaskBarJobMeasureText(displayInfo.Value, metadata, jobLevel);
            this.gearsetTaskBarCache = new GearsetTaskBarCache(displayInfo.Value.GearsetId, displayInfo.Value.ClassJobId, jobLevel, this.config.TaskBarGearsetShowNumber, this.config.TaskBarGearsetShowName, this.config.TaskBarGearsetShowLevel, text, measureText, metadata.IconId);
            return measureText;
        }

        return "职业名 套装 00";
    }

    private string BuildGearsetTaskBarJobText(GearsetDisplayInfo displayInfo, GearsetJobMetadata metadata, short jobLevel) {
        var jobName = metadata.Name;
        var levelText = this.config.TaskBarGearsetShowLevel && jobLevel > 0 ? $"Lv {jobLevel}" : string.Empty;
        var showName = this.config.TaskBarGearsetShowName && !string.IsNullOrWhiteSpace(jobName);
        var showNumber = this.config.TaskBarGearsetShowNumber;
        var nameText = showName ? jobName : string.Empty;
        var numberText = showNumber ? $"套装 {displayInfo.GearsetId + 1:00}" : string.Empty;
        return string.Join(' ', new[] { levelText, nameText, numberText }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private string BuildGearsetTaskBarJobMeasureText(GearsetDisplayInfo displayInfo, GearsetJobMetadata metadata, short jobLevel) {
        var jobName = string.IsNullOrWhiteSpace(metadata.Name) ? "职业名" : metadata.Name;
        var levelText = this.config.TaskBarGearsetShowLevel ? "Lv 100" : string.Empty;
        var nameText = this.config.TaskBarGearsetShowName ? jobName : string.Empty;
        var numberText = this.config.TaskBarGearsetShowNumber ? "套装 00" : string.Empty;
        return string.Join(' ', new[] { levelText, nameText, numberText }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private GearsetDisplayInfo? GetCachedGearsetDisplayInfo() {
        var now = DateTime.UtcNow;
        if (now < this.nextGearsetDisplayInfoRefreshAt) {
            return this.cachedGearsetDisplayInfo;
        }

        this.cachedGearsetDisplayInfo = TryGetCurrentGearsetDisplayInfo();
        this.nextGearsetDisplayInfoRefreshAt = now + GearsetDisplayInfoCacheDuration;
        return this.cachedGearsetDisplayInfo;
    }

    private unsafe GearsetDisplayInfo? TryGetCurrentGearsetDisplayInfo() {
        try {
            var module = UIModule.Instance()->GetRaptureGearsetModule();
            if (module is not null) {
                var gearsetId = module->CurrentGearsetIndex;
                if (gearsetId >= 0) {
                    var name = string.Empty;
                    var classJobId = 0u;
                    if (module->IsValidGearset((byte)gearsetId)) {
                        var entry = module->Entries[(byte)gearsetId];
                        name = entry.NameString;
                        classJobId = entry.ClassJob;
                    }

                    return new GearsetDisplayInfo((byte)gearsetId, string.IsNullOrWhiteSpace(name) ? string.Empty : name, classJobId);
                }
            }
        } catch {
        }

        return null;
    }

    private static string BuildGearsetDisplayText(byte gearsetId, string name, bool showNumber, bool showName) {
        var numberText = $"套装 {gearsetId + 1:00}";
        var trimmedName = name.Trim();
        if (showNumber && showName) {
            return string.IsNullOrWhiteSpace(trimmedName) ? numberText : $"{numberText} · {trimmedName}";
        }

        if (showNumber) {
            return numberText;
        }

        return string.IsNullOrWhiteSpace(trimmedName) ? numberText : trimmedName;
    }

    private static string BuildGearsetMeasureText(byte gearsetId, string name, bool showNumber, bool showName) {
        var numberText = $"套装 {gearsetId + 1:00}";
        var trimmedName = name.Trim();
        if (showNumber && showName) {
            return string.IsNullOrWhiteSpace(trimmedName) ? $"{numberText} · 套装名称" : $"{numberText} · {trimmedName}";
        }

        if (showNumber) {
            return numberText;
        }

        return string.IsNullOrWhiteSpace(trimmedName) ? "套装切换" : trimmedName;
    }

    private unsafe long GetGil() {
        try {
            return InventoryManager.Instance()->GetGil();
        } catch {
            return -1;
        }
    }

    private long GetCachedCurrencyCount(uint itemId) {
        var now = DateTime.UtcNow;
        if (this.cachedCurrencyCount >= 0 && this.cachedCurrencyCountItemId == itemId && now < this.nextCurrencyCountRefreshAt) {
            return this.cachedCurrencyCount;
        }

        this.cachedCurrencyCount = GetCurrencyCount(itemId);
        this.cachedCurrencyCountItemId = itemId;
        this.nextCurrencyCountRefreshAt = now + CurrencyCountCacheDuration;
        return this.cachedCurrencyCount;
    }

    private unsafe long GetCurrencyCount(uint itemId) {
        if (itemId == CurrencyItemGil) {
            return GetGil();
        }

        try {
            return InventoryManager.Instance()->GetInventoryItemCount(itemId);
        } catch {
            return -1;
        }
    }

    private unsafe void OpenCurrenciesWindow() {
        try {
            var uiModule = UIModule.Instance();
            if (uiModule is not null) {
                uiModule->ExecuteMainCommand(66);
            }
        } catch {
        }
    }

    private sealed record CurrencyDisplayInfo(uint ItemId, string Name, uint IconId);

    private static readonly CurrencyDisplayInfo[] CurrencyDisplayOptions = [
        new(CurrencyItemGil, "金币", 0),
        new(CurrencyItemMgp, "金碟币", 0),
        new(CurrencyItemVenture, "探险币", 0),
        new(CurrencyItemWolfMarks, "狼印章", 0),
        new(CurrencyItemAlliedSeals, "同盟徽章", 0),
        new(CurrencyItemCenturioSeals, "百战徽章", 0),
        new(CurrencyItemBiColorGemstones, "双色宝石", 0),
        new(CurrencyItemPoetics, "亚拉戈诗学神典石", 0),
    ];

    private List<CurrencyDisplayInfo>? scannedTomestones;

    private IEnumerable<CurrencyDisplayInfo> GetScannedTomestones() {
        if (this.scannedTomestones is not null) {
            return this.scannedTomestones;
        }

        var result = new List<CurrencyDisplayInfo>();
        try {
            var sheet = this.dataManager.GetExcelSheet<LuminaTomestonesItem>();
            if (sheet is not null) {
                foreach (var row in sheet) {
                    var itemRef = row.Item;
                    if (itemRef.IsValid && itemRef.RowId != CurrencyItemPoetics) {
                        if (this.dataManager.GetExcelSheet<LuminaItem>().TryGetRow(itemRef.RowId, out var item)) {
                            var name = GetExcelText(item.Name.ExtractText());
                            if (!string.IsNullOrWhiteSpace(name)) {
                                result.Add(new CurrencyDisplayInfo(itemRef.RowId, name, item.Icon));
                            }
                        }
                    }
                }
            }
        } catch {
        }

        this.scannedTomestones = result;
        return result;
    }

    private CurrencyDisplayInfo GetSelectedCurrencyDisplayInfo() {
        var allOptions = GetAllCurrencyOptions().ToList();
        var itemId = allOptions.Any(option => option.ItemId == this.config.TaskBarCurrencyItemId)
            ? this.config.TaskBarCurrencyItemId
            : CurrencyItemGil;

        var fallback = allOptions.First(option => option.ItemId == itemId);
        try {
            if (this.dataManager.GetExcelSheet<LuminaItem>().TryGetRow(itemId, out var item)) {
                var name = GetExcelText(item.Name.ExtractText());
                return new CurrencyDisplayInfo(itemId, string.IsNullOrWhiteSpace(name) ? fallback.Name : name, item.Icon);
            }
        } catch {
        }

        return fallback;
    }

    private IEnumerable<CurrencyDisplayInfo> GetCurrencyDisplayOptions() {
        foreach (var option in GetAllCurrencyOptions()) {
            var resolved = option;
            try {
                if (this.dataManager.GetExcelSheet<LuminaItem>().TryGetRow(option.ItemId, out var item)) {
                    var name = GetExcelText(item.Name.ExtractText());
                    resolved = new CurrencyDisplayInfo(option.ItemId, string.IsNullOrWhiteSpace(name) ? option.Name : name, item.Icon);
                }
            } catch {
            }

            yield return resolved;
        }
    }

    private IEnumerable<CurrencyDisplayInfo> GetAllCurrencyOptions() {
        foreach (var option in CurrencyDisplayOptions) {
            yield return option;
        }

        foreach (var tomestone in GetScannedTomestones()) {
            yield return tomestone;
        }

        foreach (var custom in this.config.CustomCurrencies) {
            if (custom.Enabled && custom.ItemId != 0) {
                yield return new CurrencyDisplayInfo(custom.ItemId, custom.Name, 0);
            }
        }
    }

    private unsafe void OpenTeleportWindow() {
        try {
            ActionManager.Instance()->UseAction(ActionType.GeneralAction, 7);
        } catch {
        }
    }

    private unsafe void EquipGearset(byte gearsetId) {
        try {
            var module = UIModule.Instance()->GetRaptureGearsetModule();
            if (module is not null) {
                module->EquipGearset(gearsetId, 0);
            }
        } catch {
        }
    }

    private void HandleVolumeTaskBarClick(DtrInteractionEvent interactionEvent) {
        if (interactionEvent.ClickType == MouseClickType.Right) {
            ToggleMasterVolumeMute();
        }
    }

    private string GetVolumeTaskBarTooltip() {
        var muted = IsMasterVolumeMuted();
        var volume = GetMasterVolume();
        var state = muted ? $"静音（{volume}%）" : $"{volume}%";
        return $"音量 {state}；左键打开音量控制，右键静音，滚轮微调";
    }

    private uint GetMasterVolume() {
        return GetVolume(SystemConfigOption.SoundMaster);
    }

    private bool IsMasterVolumeMuted() {
        return IsVolumeMuted(SystemConfigOption.IsSndMaster);
    }

    private void SetMasterVolume(uint volume) {
        SetVolume(SystemConfigOption.SoundMaster, SystemConfigOption.IsSndMaster, volume);
    }

    private void AdjustMasterVolume(int delta) {
        var current = (int)GetMasterVolume();
        SetMasterVolume((uint)Math.Clamp(current + delta, 0, 100));
    }

    private void ToggleMasterVolumeMute() {
        ToggleVolumeMute(SystemConfigOption.IsSndMaster);
    }

    private uint GetVolume(SystemConfigOption volumeOption) {
        return this.gameConfig.TryGet(volumeOption, out uint volume)
            ? Math.Clamp(volume, 0u, 100u)
            : 0u;
    }

    private bool IsVolumeMuted(SystemConfigOption muteOption) {
        return this.gameConfig.TryGet(muteOption, out uint muted) && muted != 0;
    }

    private void SetVolume(SystemConfigOption volumeOption, SystemConfigOption muteOption, uint volume) {
        var clampedVolume = Math.Clamp(volume, 0u, 100u);
        this.gameConfig.Set(volumeOption, clampedVolume);
        if (clampedVolume > 0 && IsVolumeMuted(muteOption)) {
            this.gameConfig.Set(muteOption, 0u);
        }
    }

    private void ToggleVolumeMute(SystemConfigOption muteOption) {
        this.gameConfig.Set(muteOption, IsVolumeMuted(muteOption) ? 0u : 1u);
    }

    private void DrawMainMenuPopup(float opacity, float scale) {
        if (!ImGui.IsPopupOpen("AllHud 主菜单")) {
            return;
        }

        EnsureTaskBarMainMenuLoaded();

        var viewport = ImGui.GetMainViewport();
        var popupSize = new Vector2(620.0f * scale, GetMainMenuPopupHeight(scale, viewport.WorkSize.Y));
        ImGui.SetNextWindowPos(GetTaskBarPopupPosition(this.mainMenuPopupAnchor, popupSize, scale), ImGuiCond.Always);
        ImGui.SetNextWindowSize(popupSize, ImGuiCond.Always);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(1.0f, 0.90f, 0.95f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.22f, 0.13f, 0.17f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.94f, 0.58f, 0.74f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1.0f, 0.86f, 0.92f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.96f, 0.74f, 0.86f, 0.44f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.94f, 0.64f, 0.80f, 0.58f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.90f, 0.56f, 0.74f, 0.66f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(1.0f, 0.86f, 0.92f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(0.86f, 0.52f, 0.68f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.80f, 0.50f, 0.68f, 0.88f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, new Vector4(0.72f, 0.40f, 0.60f, 0.94f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10.0f * scale, 9.0f * scale));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 9.0f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5.0f * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6.0f * scale, 5.0f * scale));

        if (ImGui.BeginPopup("AllHud 主菜单")) {
            DrawTaskBarMainMenuBody(scale);
            ImGui.EndPopup();
        }

        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(11);
    }

    private float GetMainMenuPopupHeight(float scale, float viewportHeight) {
        const float BaseHeight = 610.0f;
        if (this.mainMenuCategories.Count == 0) {
            return BaseHeight * scale;
        }

        var rowHeight = GetMainMenuEntryHeight(scale);
        var separatorHeight = MathF.Round(13.0f * scale);
        var headerHeight = MathF.Round(30.0f * scale);
        var verticalPadding = 38.0f * scale;
        var longestCategoryHeight = this.mainMenuCategories
            .Select(category => category.Entries.Sum(entry => entry.IsSeparator ? separatorHeight : rowHeight))
            .DefaultIfEmpty(0.0f)
            .Max();
        var desiredHeight = Math.Max(BaseHeight * scale, headerHeight + longestCategoryHeight + verticalPadding);
        var maxHeight = Math.Max(BaseHeight * scale, viewportHeight - 48.0f * scale);
        return SnapToPixel(Math.Min(desiredHeight, maxHeight));
    }

    private static float GetMainMenuEntryHeight(float scale) {
        return MathF.Round(26.0f * scale);
    }

    private static IDisposable PushTaskBarPopupScrollbarStyle(float scale) {
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(1.0f, 0.86f, 0.94f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(0.96f, 0.58f, 0.76f, 0.88f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.91f, 0.44f, 0.68f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, new Vector4(0.82f, 0.30f, 0.58f, 1.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, MathF.Max(8.0f, 10.0f * scale));
        return new StyleScope(4, 1);
    }

    private void DrawTaskBarMainMenuBody(float scale) {
        if (this.mainMenuCategories.Count == 0) {
            ImGui.TextDisabled("没有可用菜单项");
            return;
        }

        this.selectedMainMenuCategoryIndex = Math.Clamp(this.selectedMainMenuCategoryIndex, 0, this.mainMenuCategories.Count - 1);
        var categoryWidth = 158.0f * scale;
        var bodyHeight = Math.Max(340.0f * scale, ImGui.GetContentRegionAvail().Y);

        if (ImGui.BeginChild("##AllHudMainMenuCategories", new Vector2(categoryWidth, bodyHeight), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)) {
            for (var index = 0; index < this.mainMenuCategories.Count; index++) {
                DrawMainMenuCategory(this.mainMenuCategories[index], index, categoryWidth, scale);
            }
        }

        ImGui.EndChild();
        ImGui.SameLine(0.0f, 8.0f * scale);

        var selectedCategory = this.mainMenuCategories[this.selectedMainMenuCategoryIndex];
        using (PushTaskBarPopupScrollbarStyle(scale)) {
            if (ImGui.BeginChild("##AllHudMainMenuEntries", new Vector2(0.0f, bodyHeight), true)) {
                ImGui.TextUnformatted(selectedCategory.Name);
                ImGui.Separator();

                foreach (var entry in selectedCategory.Entries) {
                    DrawMainMenuEntry(entry, scale);
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawMainMenuCategory(TaskBarMainMenuCategory category, int index, float categoryWidth, float scale) {
        var selected = index == this.selectedMainMenuCategoryIndex;
        var rowHeight = GetMainMenuEntryHeight(scale);
        var rowStart = SnapToPixel(ImGui.GetCursorScreenPos());
        var rowWidth = Math.Max(80.0f * scale, Math.Min(categoryWidth - 8.0f * scale, ImGui.GetContentRegionAvail().X));
        var highlightMin = SnapToPixel(rowStart + new Vector2(2.0f * scale, 2.0f * scale));
        var highlightMax = SnapToPixel(rowStart + new Vector2(rowWidth - 2.0f * scale, rowHeight - 2.0f * scale));

        ImGui.SetCursorScreenPos(highlightMin);
        ImGui.InvisibleButton($"##AllHudMainMenuCategory{category.RowId}", highlightMax - highlightMin);
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            this.selectedMainMenuCategoryIndex = index;
        }

        var drawList = ImGui.GetWindowDrawList();
        if (selected || hovered) {
            var fill = selected
                ? new Vector4(0.96f, 0.80f, 0.89f, 0.24f)
                : new Vector4(0.96f, 0.76f, 0.87f, 0.14f);
            drawList.AddRectFilled(highlightMin, highlightMax, ImGui.GetColorU32(fill), 4.0f * scale);
        }

        if (selected) {
            var markerMin = SnapToPixel(new Vector2(highlightMin.X, highlightMin.Y + 5.0f * scale));
            var markerMax = SnapToPixel(new Vector2(highlightMin.X + 3.0f * scale, highlightMax.Y - 5.0f * scale));
            drawList.AddRectFilled(markerMin, markerMax, ImGui.GetColorU32(new Vector4(0.82f, 0.36f, 0.60f, 0.88f)), 2.0f * scale);
        }

        DrawMainMenuGameIconImage(category.IconId, rowStart + new Vector2(5.0f * scale, 0.0f), rowHeight, scale);

        var textPos = SnapToPixel(new Vector2(rowStart.X + 36.0f * scale, rowStart.Y + Math.Max(0.0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f)));
        var textColor = selected
            ? ImGui.GetColorU32(new Vector4(0.24f, 0.10f, 0.18f, 1.0f))
            : ImGui.GetColorU32(new Vector4(0.30f, 0.18f, 0.24f, 1.0f));
        drawList.AddText(textPos, textColor, category.Name);

        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));
    }

    private void DrawMainMenuEntry(TaskBarMainMenuEntry entry, float scale) {
        if (entry.IsSeparator) {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            return;
        }

        var enabled = IsMainMenuEntryEnabled(entry);
        var rowHeight = MathF.Round(30.0f * scale);
        var rowStart = SnapToPixel(ImGui.GetCursorScreenPos());
        var rowWidth = Math.Max(120.0f * scale, ImGui.GetContentRegionAvail().X);
        var hitMin = SnapToPixel(rowStart + new Vector2(2.0f * scale, 2.0f * scale));
        var hitMax = SnapToPixel(rowStart + new Vector2(rowWidth - 2.0f * scale, rowHeight - 2.0f * scale));

        ImGui.SetCursorScreenPos(hitMin);
        ImGui.InvisibleButton($"##AllHudMainMenuEntry{entry.CommandId?.ToString() ?? entry.ChatCommand}", hitMax - hitMin);
        var hovered = enabled && ImGui.IsItemHovered();
        var active = enabled && ImGui.IsItemActive();
        if (enabled && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            ExecuteMainMenuEntry(entry);
            ImGui.CloseCurrentPopup();
        }

        var drawList = ImGui.GetWindowDrawList();
        if (hovered || active) {
            var fill = active
                ? new Vector4(0.90f, 0.58f, 0.76f, 0.38f)
                : new Vector4(0.96f, 0.76f, 0.87f, 0.24f);
            var border = active
                ? new Vector4(0.78f, 0.36f, 0.60f, 0.42f)
                : new Vector4(0.88f, 0.56f, 0.72f, 0.24f);
            drawList.AddRectFilled(hitMin, hitMax, ImGui.GetColorU32(fill), 5.0f * scale);
            drawList.AddRect(hitMin, hitMax, ImGui.GetColorU32(border), 5.0f * scale, (ImDrawFlags)0, 1.0f * scale);
        }

        DrawMainMenuEntryIcon(entry, rowStart + new Vector2(5.0f * scale, 0.0f), rowHeight, scale, enabled ? 1.0f : 0.42f);

        var textPos = SnapToPixel(new Vector2(rowStart.X + 36.0f * scale, rowStart.Y + Math.Max(0.0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f)));
        var textColor = enabled
            ? ImGui.GetColorU32(new Vector4(0.24f, 0.13f, 0.18f, 1.0f))
            : ImGui.GetColorU32(new Vector4(0.48f, 0.38f, 0.43f, 0.72f));
        drawList.AddText(textPos, textColor, entry.Name);
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, rowStart.Y + rowHeight));
    }

    private void DrawMainMenuGameIcon(uint iconId, Vector2 rowStart, float rowHeight, float scale, float alpha = 1.0f) {
        DrawMainMenuGameIconImage(iconId, rowStart, rowHeight, scale, alpha);
        var iconSize = MathF.Round(24.0f * scale);
        ImGui.Dummy(new Vector2(iconSize + 2.0f * scale, rowHeight));
    }

    private void DrawMainMenuEntryIcon(TaskBarMainMenuEntry entry, Vector2 rowStart, float rowHeight, float scale, float alpha = 1.0f) {
        if (entry.IconId != 0) {
            DrawMainMenuGameIconImage(entry.IconId, rowStart, rowHeight, scale, alpha);
        }
    }

    private void DrawMainMenuGameIconImage(uint iconId, Vector2 rowStart, float rowHeight, float scale, float alpha = 1.0f) {
        var iconSize = MathF.Round(24.0f * scale);
        var iconMin = SnapToPixel(rowStart + new Vector2(1.0f * scale, Math.Max(0.0f, (rowHeight - iconSize) * 0.5f)));
        var iconMax = iconMin + new Vector2(iconSize, iconSize);
        var drawList = ImGui.GetWindowDrawList();
        DrawGameIconImage(drawList, iconId, iconMin, iconMax, true, true);
    }

    private void EnsureTaskBarMainMenuLoaded() {
        if (this.mainMenuLoaded) {
            return;
        }

        this.mainMenuLoaded = true;
        this.mainMenuCategories.Clear();

        var commands = this.dataManager.GetExcelSheet<LuminaMainCommand>()
            .Where(command => !string.IsNullOrWhiteSpace(GetExcelText(command.Name.ExtractText())))
            .ToLookup(command => command.MainCommandCategory.RowId);

        foreach (var categoryRow in this.dataManager.GetExcelSheet<LuminaMainCommandCategory>().OrderBy(category => category.RowId)) {
            var categoryName = GetExcelText(categoryRow.Name.ExtractText());
            if (string.IsNullOrWhiteSpace(categoryName)) {
                continue;
            }

            var entries = commands[categoryRow.RowId]
                .OrderBy(command => command.SortID)
                .Select(CreateMainMenuEntry)
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();

            if (entries.Count == 0) {
                continue;
            }

            this.mainMenuCategories.Add(new TaskBarMainMenuCategory(categoryRow.RowId, categoryName, GetMainMenuCategoryIconId(categoryRow.RowId), entries));
        }
    }

    private TaskBarMainMenuEntry CreateMainMenuEntry(LuminaMainCommand command) {
        var iconId = command.Icon > 0 ? (uint)command.Icon : 0u;
        var name = GetMainCommandDisplayName(command.RowId, GetExcelText(command.Name.ExtractText()));
        if (name.Contains("许可证", StringComparison.OrdinalIgnoreCase)) {
            return new TaskBarMainMenuEntry(string.Empty, null, string.Empty, 0);
        }

        return new TaskBarMainMenuEntry(name, command.RowId, string.Empty, iconId);
    }

    private static uint GetMainMenuCategoryIconId(uint categoryId) {
        return categoryId switch {
            1 => 1,
            2 => 5,
            3 => 21,
            4 => 7,
            5 => 17,
            6 => 20,
            7 => 14,
            _ => 0,
        };
    }

    private static string GetExcelText(string text) {
        return text.Replace("\u00AD", string.Empty, StringComparison.Ordinal).Trim();
    }

    private unsafe string GetMainCommandDisplayName(uint commandId, string fallback) {
        var agentHud = AgentHUD.Instance();
        if (agentHud is null) {
            return fallback;
        }

        var text = GetExcelText(agentHud->GetMainCommandString(commandId).ExtractText());
        if (string.IsNullOrWhiteSpace(text)) {
            return fallback;
        }

        var shortcutStart = text.IndexOf('[', StringComparison.Ordinal);
        return shortcutStart > 0 ? text[..shortcutStart].Trim() : text;
    }

    private unsafe bool IsMainMenuEntryEnabled(TaskBarMainMenuEntry entry) {
        if (!entry.CommandId.HasValue) {
            return !string.IsNullOrWhiteSpace(entry.ChatCommand);
        }

        var uiModule = UIModule.Instance();
        var agentHud = AgentHUD.Instance();
        return uiModule is not null
               && agentHud is not null
               && agentHud->IsMainCommandEnabled(entry.CommandId.Value)
               && uiModule->IsMainCommandUnlocked(entry.CommandId.Value);
    }

    private unsafe void ExecuteMainMenuEntry(TaskBarMainMenuEntry entry) {
        if (entry.CommandId.HasValue) {
            var uiModule = UIModule.Instance();
            if (uiModule is not null) {
                uiModule->ExecuteMainCommand(entry.CommandId.Value);
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(entry.ChatCommand)) {
            this.commandManager.ProcessCommand(entry.ChatCommand);
        }
    }

    private void DrawVolumePopup(float opacity, float scale) {
        if (!ImGui.IsPopupOpen("AllHud 音量控制")) {
            return;
        }

        var popupSize = new Vector2(268.0f * scale, 0.0f);
        ImGui.SetNextWindowPos(GetTaskBarPopupPosition(this.volumePopupAnchor, popupSize, scale, 280.0f * scale), ImGuiCond.Always);
        ImGui.SetNextWindowSize(popupSize, ImGuiCond.Always);

        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(1.0f, 0.90f, 0.95f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.18f, 0.10f, 0.14f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.94f, 0.58f, 0.74f, Math.Clamp(opacity, 0.0f, 1.0f)));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(1.0f, 0.86f, 0.92f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1.0f, 0.78f, 0.89f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1.0f, 0.72f, 0.86f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, new Vector4(0.18f, 0.55f, 0.88f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new Vector4(0.10f, 0.42f, 0.76f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1.0f, 0.80f, 0.91f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1.0f, 0.72f, 0.86f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.96f, 0.62f, 0.80f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, new Vector4(0.20f, 0.50f, 0.86f, 0.34f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12.0f * scale, 7.0f * scale));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 7.0f * scale);

        if (ImGui.BeginPopup("AllHud 音量控制")) {
            DrawVolumeChannelControl("主音量", SystemConfigOption.SoundMaster, SystemConfigOption.IsSndMaster, scale);
            DrawVolumeChannelControl("背景音乐", SystemConfigOption.SoundBgm, SystemConfigOption.IsSndBgm, scale);
            DrawVolumeChannelControl("音效", SystemConfigOption.SoundSe, SystemConfigOption.IsSndSe, scale);
            DrawVolumeChannelControl("语音", SystemConfigOption.SoundVoice, SystemConfigOption.IsSndVoice, scale);
            DrawVolumeChannelControl("环境", SystemConfigOption.SoundEnv, SystemConfigOption.IsSndEnv, scale);
            DrawVolumeChannelControl("系统", SystemConfigOption.SoundSystem, SystemConfigOption.IsSndSystem, scale);

            ImGui.EndPopup();
        }

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(12);
    }

    private void DrawCoordinatesPopup(float opacity, float scale) {
        DrawSimpleTaskBarPopup(CoordinatesPopupId, new Vector2(300.0f * scale, 0.0f), opacity, scale, () => {
            ImGui.TextUnformatted(GetCoordinatesText());
        });
    }
}

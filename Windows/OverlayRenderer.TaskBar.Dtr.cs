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
    private List<TaskBarItem> BuildDtrTaskBarItems() {
        var now = DateTime.UtcNow;
        if (now < this.nextDtrTaskBarRefreshAt) {
            return this.cachedDtrTaskBarItems;
        }

        this.nextDtrTaskBarRefreshAt = now + DtrTaskBarCacheDuration;
        if (!this.config.TaskBarShowServerInfoBar) {
            if (this.cachedDtrTaskBarItems.Count > 0) {
                this.cachedDtrTaskBarItems = [];
                this.cachedDtrTaskBarSnapshots = [];
            }

            return this.cachedDtrTaskBarItems;
        }

        BuildDtrTaskBarSnapshots(this.dtrTaskBarSnapshotBuffer);
        if (DtrSnapshotsEqual(this.cachedDtrTaskBarSnapshots, this.dtrTaskBarSnapshotBuffer)) {
            return this.cachedDtrTaskBarItems;
        }

        var items = new List<TaskBarItem>(this.dtrTaskBarSnapshotBuffer.Count);
        foreach (var snapshot in this.dtrTaskBarSnapshotBuffer) {
            items.Add(new TaskBarItem(snapshot.Text, snapshot.Tooltip, snapshot.HasClickAction ? snapshot.OnClick : null, MeasureText: GetDtrMeasureText(snapshot.Text)));
        }

        this.cachedDtrTaskBarSnapshots = [.. this.dtrTaskBarSnapshotBuffer];
        this.cachedDtrTaskBarItems = items;
        return this.cachedDtrTaskBarItems;
    }

    private void BuildDtrTaskBarSnapshots(List<DtrTaskBarSnapshot> snapshots) {
        snapshots.Clear();
        var entries = this.dtrBar.Entries;
        if (snapshots.Capacity < Math.Min(entries.Count, 8)) {
            snapshots.Capacity = Math.Min(entries.Count, 8);
        }

        foreach (var entry in entries) {
            if (!entry.Shown || entry.UserHidden) {
                continue;
            }

            var text = entry.Text?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) {
                continue;
            }

            var tooltip = entry.Tooltip?.ToString();
            snapshots.Add(new DtrTaskBarSnapshot(
                entry.Title,
                text,
                string.IsNullOrWhiteSpace(tooltip) ? text : tooltip,
                entry.HasClickAction,
                entry.OnClick));
        }
    }

    private static bool DtrSnapshotsEqual(IReadOnlyList<DtrTaskBarSnapshot> previous, IReadOnlyList<DtrTaskBarSnapshot> current) {
        if (previous.Count != current.Count) {
            return false;
        }

        for (var index = 0; index < previous.Count; index++) {
            if (!previous[index].Equals(current[index])) {
                return false;
            }
        }

        return true;
    }

    private static DtrInteractionEvent CreateDtrInteractionEvent(MouseClickType clickType) {
        return new DtrInteractionEvent {
            ClickType = clickType,
            ModifierKeys = GetDtrModifierKeys(),
            Position = ImGui.GetMousePos(),
            ScrollDirection = MouseScrollDirection.None,
        };
    }

    private static ClickModifierKeys GetDtrModifierKeys() {
        var modifiers = ClickModifierKeys.None;
        var io = ImGui.GetIO();
        if (io.KeyCtrl) {
            modifiers |= ClickModifierKeys.Ctrl;
        }

        if (io.KeyAlt) {
            modifiers |= ClickModifierKeys.Alt;
        }

        if (io.KeyShift) {
            modifiers |= ClickModifierKeys.Shift;
        }

        return modifiers;
    }

    private TaskBarRowMetrics CalculateTaskBarMainRowMetrics(IReadOnlyList<TaskBarItem> items, Vector2 padding, float spacing, float scale) {
        if (items.Count == 0) {
            return new TaskBarRowMetrics(0.0f, 0.0f);
        }

        var textWidth = 0.0f;
        foreach (var item in items) {
            textWidth += CalcTaskBarItemLayoutSize(item, scale).X;
        }

        var totalSpacing = items.Count > 1
            ? spacing * (items.Count - 1)
            : 0.0f;
        var maxItemHeight = ImGui.CalcTextSize("Hg").Y;
        foreach (var item in items) {
            maxItemHeight = Math.Max(maxItemHeight, CalcTaskBarItemLayoutSize(item, scale).Y);
        }

        var minHeight = 50.0f * scale;
        return new TaskBarRowMetrics(SnapToPixel(textWidth + totalSpacing + padding.X * 2.0f), SnapToPixel(Math.Max(minHeight, maxItemHeight + padding.Y * 2.0f)));
    }

    private static TaskBarRowMetrics CalculateTaskBarDtrRowMetrics(IReadOnlyList<TaskBarItem> items, Vector2 itemPadding, float spacing, float itemHeight, Vector2 outerPadding, float scale, bool useMeasureText = true) {
        if (items.Count == 0) {
            return new TaskBarRowMetrics(0.0f, 0.0f);
        }

        var totalItemWidth = 0.0f;
        foreach (var item in items) {
            var text = useMeasureText && !string.IsNullOrWhiteSpace(item.MeasureText) ? item.MeasureText : item.Text;
            totalItemWidth += ImGui.CalcTextSize(text).X + itemPadding.X * 2.0f;
        }

        var totalSpacing = items.Count > 1
            ? spacing * (items.Count - 1)
            : 0.0f;
        return new TaskBarRowMetrics(SnapToPixel(totalItemWidth + totalSpacing + outerPadding.X * 2.0f), SnapToPixel(itemHeight + outerPadding.Y * 2.0f));
    }

    private static string GetEorzeaTimeText() {
        var eorzeaSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 20.5714285714;
        var totalMinutes = (int)(eorzeaSeconds / 60.0) % 1440;
        var hour = totalMinutes / 60;
        var minute = totalMinutes % 60;
        return $"{hour:00}:{minute:00}";
    }
}

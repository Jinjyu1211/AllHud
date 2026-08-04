using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AllHud.Markers;

internal sealed class TreasureMarkerFactory : WorldMarkerFactory {
    public override string Id => "TreasureMarkers";
    public override string Name => "宝箱标记";

    private readonly Configuration _config;
    private readonly IDataManager _dataManager;
    private readonly IObjectTable _objectTable;
    private DateTime _lastScanAt = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(500);

    public TreasureMarkerFactory(Configuration config, IDataManager dataManager, IObjectTable objectTable) {
        _config = config;
        _dataManager = dataManager;
        _objectTable = objectTable;
    }

    protected override void OnTick(DateTime now) {
        if (!_config.ShowTreasureMarkers) {
            RemoveAllMarkers();
            return;
        }

        if (now - _lastScanAt < ScanInterval) return;
        _lastScanAt = now;

        if (_objectTable.LocalPlayer is null) {
            RemoveAllMarkers();
            return;
        }

        ScanTreasures();
    }

    private void ScanTreasures() {
        var eventItemSheet = _dataManager.GetExcelSheet<EventItem>();

        List<string> activeKeys = new();
        foreach (var obj in _objectTable) {
            if (obj is null || !obj.IsTargetable) continue;

            var name = obj.Name.ToString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!IsTreasure(obj, name)) continue;

            var iconId = GetIconForTreasure(obj, eventItemSheet);
            var key = $"TREAS_{obj.EntityId}_{obj.Position.X:F0}_{obj.Position.Z:F0}";
            activeKeys.Add(key);

            if (!ContainsMarker(key)) {
                SetMarker(new WorldMarker {
                    Key = key,
                    Label = name,
                    IconId = iconId,
                    IconSize = 28,
                    Position = obj.Position,
                    FadeNear = _config.WorldMarkerFadeDistance,
                    FadeFar = _config.WorldMarkerFadeDistance + _config.WorldMarkerFadeAttenuation,
                    MaxVisibleDistance = _config.WorldMarkerMaxVisibleDistance,
                    ShowOnCompass = true,
                    ShowDistance = true,
                });
            }
        }

        foreach (var staleKey in ActiveMarkers.Select(m => m.Key).Except(activeKeys).ToList()) {
            RemoveMarker(staleKey);
        }
    }

    private static bool IsTreasure(IGameObject obj, string name) {
        if (obj.ObjectKind == ObjectKind.Treasure) return true;
        if (name.Contains("宝箱") || name.Contains("箱子") || name.Contains("魔法罐") || name.Contains("胡萝卜")) return true;
        return false;
    }

    private static uint GetIconForTreasure(
        IGameObject obj,
        ExcelSheet<EventItem>? eventItemSheet) {
        var baseId = obj.BaseId;

        if (eventItemSheet is not null && eventItemSheet.TryGetRow(baseId, out var eventItem)) {
            var icon = (uint)eventItem.Icon;
            if (icon > 0) return icon;
        }

        return 60561;
    }
}

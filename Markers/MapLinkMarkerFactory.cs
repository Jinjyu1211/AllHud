using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AllHud.Markers;

internal sealed class MapLinkMarkerFactory : WorldMarkerFactory {
    public override string Id => "MapLinkMarkers";
    public override string Name => "地图链接";

    private readonly Configuration _config;
    private readonly IDataManager _dataManager;
    private readonly IObjectTable _objectTable;
    private DateTime _lastScanAt = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(2000);

    public MapLinkMarkerFactory(Configuration config, IDataManager dataManager, IObjectTable objectTable) {
        _config = config;
        _dataManager = dataManager;
        _objectTable = objectTable;
    }

    protected override void OnTick(DateTime now) {
        if (!_config.ShowMapLinkMarkers) {
            RemoveAllMarkers();
            return;
        }

        if (now - _lastScanAt < ScanInterval) return;
        _lastScanAt = now;

        var aetheryteSheet = _dataManager.GetExcelSheet<Aetheryte>();
        List<string> activeKeys = new();

        foreach (var obj in _objectTable) {
            if (obj is null) continue;
            if (obj.ObjectKind != ObjectKind.Aetheryte) continue;

            string name = obj.Name.ToString();
            uint iconId = 60443;

            if (aetheryteSheet is not null && aetheryteSheet.TryGetRow(obj.BaseId, out var aetheryte)) {
                var placeName = aetheryte.PlaceName.ValueNullable;
                if (placeName is not null) {
                    var pn = placeName.Value.Name.ExtractText();
                    if (!string.IsNullOrWhiteSpace(pn)) name = pn;
                }
            }

            var key = $"ML_{obj.EntityId}";
            activeKeys.Add(key);

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
            });
        }

        foreach (var staleKey in ActiveMarkers.Select(m => m.Key).Except(activeKeys).ToList()) {
            RemoveMarker(staleKey);
        }
    }
}

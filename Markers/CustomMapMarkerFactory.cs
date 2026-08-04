using System.Numerics;

namespace AllHud.Markers;

/// <summary>
/// 自定义地图标记工厂。用于在指定地图上显示预设的坐标标记点。
/// </summary>
internal sealed class CustomMapMarkerFactory : WorldMarkerFactory {
    public override string Id => "CustomMapMarkers";
    public override string Name => "自定义地图标记";

    private readonly Configuration _config;
    private DateTime _lastScanAt = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(500);
    private uint _lastMapId = 0;

    public CustomMapMarkerFactory(Configuration config) {
        _config = config;
    }

    protected override unsafe void OnTick(DateTime now) {
        if (!_config.ShowCustomMapMarkers) {
            RemoveAllMarkers();
            _lastMapId = 0;
            return;
        }

        if (now - _lastScanAt < ScanInterval) return;
        _lastScanAt = now;

        var mapId = GetCurrentMapId();
        if (mapId != _lastMapId) {
            RemoveAllMarkers();
            _lastMapId = mapId;
        }

        if (mapId == 0) return;
        if (!_config.CustomMapMarkerMaps.TryGetValue(mapId, out var markers) || markers.Count == 0) {
            var defaults = CustomMapMarkerDefaults.GetDefaults();
            if (!defaults.TryGetValue(mapId, out markers) || markers.Count == 0) return;
        }

        var agentMap = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        var canConvert = agentMap is not null && agentMap->CurrentMapId != 0;

        List<string> activeKeys = new();
        foreach (var marker in markers) {
            var key = $"CMM_{mapId}_{marker.Id}";
            activeKeys.Add(key);
            if (!ContainsMarker(key)) {
                Vector3 position;
                if (canConvert && marker.UseMapCoordinates) {
                    var (worldX, worldZ) = MapCoordinateConverter.MapToWorld(marker.X, marker.Z);
                    position = new Vector3(worldX, 0, worldZ);
                } else {
                    position = marker.Position;
                }

                SetMarker(new WorldMarker {
                    Key = key,
                    Label = marker.Label,
                    SubLabel = marker.SubLabel,
                    IconId = marker.IconId,
                    IconSize = marker.IconSize,
                    Position = position,
                    MapId = mapId,
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

    private unsafe uint GetCurrentMapId() {
        try {
            var agentMap = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
            return agentMap is not null ? agentMap->CurrentMapId : 0;
        } catch {
            return 0;
        }
    }
}

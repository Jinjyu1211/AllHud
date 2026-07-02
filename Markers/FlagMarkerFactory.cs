using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Numerics;

namespace AllHud.Markers;

internal sealed class FlagMarkerFactory : WorldMarkerFactory {
    public override string Id => "FlagMarker";
    public override string Name => "地图标记";

    private readonly Configuration _config;
    private DateTime _lastScanAt = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(500);

    public FlagMarkerFactory(Configuration config) {
        _config = config;
    }

    protected override unsafe void OnTick(DateTime now) {
        if (!_config.ShowFlagMarker) {
            RemoveAllMarkers();
            return;
        }

        if (now - _lastScanAt < ScanInterval) return;
        _lastScanAt = now;

        try {
            var agentMap = AgentMap.Instance();
            if (agentMap is null || agentMap->FlagMarkerCount == 0) {
                RemoveAllMarkers();
                return;
            }

            var flag = agentMap->FlagMapMarkers[0];
            var key = $"Flag_{flag.MapId}";

            var worldPos = new Vector3(flag.XFloat, 0, flag.YFloat);
            var mapCoords = MapCoordinateConverter.WorldToMap(worldPos.X, worldPos.Z);

            SetMarker(new WorldMarker {
                Key = key,
                Label = "Flag",
                SubLabel = $"X:{mapCoords.X:F1} Y:{mapCoords.Y:F1}",
                IconId = flag.MapMarker.IconId,
                IconSize = 36,
                Position = worldPos,
                MapId = flag.MapId,
                FadeNear = _config.WorldMarkerFadeDistance,
                FadeFar = _config.WorldMarkerFadeDistance + _config.WorldMarkerFadeAttenuation,
                MaxVisibleDistance = _config.WorldMarkerMaxVisibleDistance,
                ShowOnCompass = true,
            });
            foreach (var staleKey in ActiveMarkers.Select(m => m.Key).Where(k => k != key).ToList()) {
                RemoveMarker(staleKey);
            }
        } catch {
            RemoveAllMarkers();
        }
    }
}

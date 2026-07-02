using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace AllHud.Markers;

internal sealed class PlayerMarkerFactory : WorldMarkerFactory {
    public override string Id => "PlayerPositionMarker";
    public override string Name => "玩家位置";

    private readonly Configuration _config;
    private readonly IObjectTable _objectTable;
    private DateTime _lastScanAt = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(300);

    public PlayerMarkerFactory(Configuration config, IObjectTable objectTable) {
        _config = config;
        _objectTable = objectTable;
    }

    protected override void OnTick(DateTime now) {
        if (!_config.ShowPlayerPositionMarker) {
            RemoveAllMarkers();
            return;
        }

        if (now - _lastScanAt < ScanInterval) return;
        _lastScanAt = now;

        try {
            var player = _objectTable.LocalPlayer;
            if (player is null) {
                RemoveAllMarkers();
                return;
            }

            var pos = player.Position;
            var key = "PlayerPos";
            var mapCoords = MapCoordinateConverter.WorldToMap(pos.X, pos.Z);

            SetMarker(new WorldMarker {
                Key = key,
                Label = player.Name.ToString(),
                SubLabel = $"X:{mapCoords.X:F1}  Y:{mapCoords.Y:F1}",
                IconSize = 24,
                Position = pos,
                FadeNear = 8f,
                FadeFar = 18f,
                MaxVisibleDistance = 0,
                ShowOnCompass = false,
            });
        } catch {
            RemoveAllMarkers();
        }
    }
}

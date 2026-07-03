using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Numerics;

namespace AllHud.Markers;

internal sealed class QuestMarkerFactory : WorldMarkerFactory {
    public override string Id => "QuestMarkers";
    public override string Name => "任务标记";

    private readonly Configuration _config;
    private DateTime _lastScanAt = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(1000);

    private static readonly HashSet<uint> UnavailableQuestIconIds = new() {
        71151, 71152, 71153, 71154, 71155
    };

    public QuestMarkerFactory(Configuration config) {
        _config = config;
    }

    protected override unsafe void OnTick(DateTime now) {
        if (!_config.ShowQuestMarkers) {
            RemoveAllMarkers();
            return;
        }

        if (now - _lastScanAt < ScanInterval) return;
        _lastScanAt = now;

        try {
            var agentMap = AgentMap.Instance();
            if (agentMap is null) {
                RemoveAllMarkers();
                return;
            }

            var count = agentMap->MapMarkerCount;
            if (count == 0) {
                RemoveAllMarkers();
                return;
            }

            var currentMapId = agentMap->CurrentMapId;
            List<string> activeKeys = new();

            for (int i = 0; i < count; i++) {
                var m = agentMap->MapMarkers[i];
                var iconId = m.MapMarker.IconId;
                if (iconId == 0) continue;
                if (UnavailableQuestIconIds.Contains(iconId)) continue;

                var worldX = m.MapMarker.X / 16.0f;
                var worldZ = m.MapMarker.Y / 16.0f;
                if (float.IsNaN(worldX) || float.IsNaN(worldZ)) continue;

                var key = $"QM_{i}_{iconId}";
                activeKeys.Add(key);

                SetMarker(new WorldMarker {
                    Key = key,
                    Label = string.Empty,
                    IconId = iconId,
                    IconSize = 32,
                    Position = new Vector3(worldX, 0, worldZ),
                    MapId = currentMapId,
                    FadeNear = _config.WorldMarkerFadeDistance,
                    FadeFar = _config.WorldMarkerFadeDistance + _config.WorldMarkerFadeAttenuation,
                    MaxVisibleDistance = _config.WorldMarkerMaxVisibleDistance,
                    ShowOnCompass = true,
                });
            }

            foreach (var staleKey in ActiveMarkers.Select(m => m.Key).Except(activeKeys).ToList()) {
                RemoveMarker(staleKey);
            }
        } catch {
            RemoveAllMarkers();
        }
    }
}

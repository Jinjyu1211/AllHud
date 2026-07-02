using System.Numerics;

namespace AllHud.Markers;

/// <summary>
/// 标记注册表。借鉴 Umbra.WorldMarkerRegistry。
/// 所有工厂共享一个注册表，由 <see cref="WorldMarkerSystem"/> 持有。
/// </summary>
internal sealed class WorldMarkerRegistry : IDisposable {
    private readonly Dictionary<string, WorldMarker> _markers = new();
    private readonly object _lock = new();

    public IReadOnlyCollection<WorldMarker> Markers {
        get {
            lock (_lock) {
                return _markers.Values.ToList();
            }
        }
    }

    public void Register(WorldMarker marker) {
        lock (_lock) {
            _markers[marker.Key] = marker;
        }
    }

    public void Unregister(WorldMarker marker) {
        lock (_lock) {
            _markers.Remove(marker.Key);
        }
    }

    public void Clear() {
        lock (_lock) {
            _markers.Clear();
        }
    }

    public void Dispose() {
        Clear();
        GC.SuppressFinalize(this);
    }
}

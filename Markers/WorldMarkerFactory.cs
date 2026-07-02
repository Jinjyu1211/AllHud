using System.Numerics;

namespace AllHud.Markers;

/// <summary>
/// 世界标记工厂抽象基类。借鉴 Umbra.Markers.System.WorldMarkerFactory。
/// 子类负责扫描游戏数据，调用 <see cref="SetMarker"/> 注册要渲染的标记。
/// 所有异常在调用方被吞掉，避免炸主插件。
/// </summary>
public abstract class WorldMarkerFactory : IDisposable {
    private readonly Dictionary<string, WorldMarker> _markers = new();
    private WorldMarkerRegistry? _registry;

    public abstract string Id { get; }
    public abstract string Name { get; }

    internal void AttachRegistry(WorldMarkerRegistry registry) {
        _registry = registry;
    }

    public void Tick(DateTime now) {
        try {
            OnTick(now);
        } catch {
            // 模块隔离：任何异常都不影响主插件
        }
    }

    protected abstract void OnTick(DateTime now);

    protected void SetMarker(WorldMarker marker) {
        if (_registry is null) return;
        _markers[marker.Key] = marker;
        _registry.Register(marker);
    }

    protected void RemoveMarker(string key) {
        if (_registry is null) return;
        if (_markers.Remove(key, out var marker)) {
            _registry.Unregister(marker);
        }
    }

    protected bool ContainsMarker(string key) => _markers.ContainsKey(key);

    protected IReadOnlyCollection<WorldMarker> ActiveMarkers => _markers.Values;

    protected void RemoveAllMarkers() {
        if (_registry is null) return;
        foreach (var marker in _markers.Values) {
            _registry.Unregister(marker);
        }
        _markers.Clear();
    }

    public virtual void Dispose() {
        RemoveAllMarkers();
        GC.SuppressFinalize(this);
    }

    public virtual string GetDebugInfo() => $"{Id}: {_markers.Count} markers";
}

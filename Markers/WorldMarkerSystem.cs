using Dalamud.Plugin.Services;

namespace AllHud.Markers;

public sealed class WorldMarkerSystem : IDisposable {
    private readonly WorldMarkerRegistry _registry;
    private readonly WorldMarkerRenderer _renderer;
    private readonly List<WorldMarkerFactory> _factories = new();
    private readonly Configuration _config;
    private DateTime _lastTickAt = DateTime.MinValue;
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    public WorldMarkerSystem(
        Configuration config,
        IDataManager dataManager,
        IObjectTable objectTable,
        IGameGui gameGui) {
        _config = config;
        _registry = new WorldMarkerRegistry();
        _renderer = new WorldMarkerRenderer(gameGui, objectTable, _registry);

        RegisterFactory(new GatheringNodeMarkerFactory(config, dataManager, objectTable));
        RegisterFactory(new FlagMarkerFactory(config));
    }

    private void RegisterFactory(WorldMarkerFactory factory) {
        factory.AttachRegistry(_registry);
        _factories.Add(factory);
    }

    public void Tick(DateTime now) {
        if (!_config.ShowWorldMarkers) {
            _registry.Clear();
            return;
        }

        if (now - _lastTickAt < TickInterval) return;
        _lastTickAt = now;

        foreach (var factory in _factories) {
            factory.Tick(now);
        }
    }

    public void Draw() {
        if (!_config.ShowWorldMarkers) return;
        _renderer.DrawWithDebug(_factories);
        _renderer.DrawPlayerPosition(_config);
    }

    public void Dispose() {
        foreach (var factory in _factories) {
            try { factory.Dispose(); } catch { }
        }
        _factories.Clear();
        try { _renderer.Dispose(); } catch { }
        try { _registry.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }
}

using System.Numerics;

namespace AllHud.Markers;

/// <summary>
/// 单个世界标记的数据模型。借鉴 Umbra.Markers.System.WorldMarker。
/// </summary>
public sealed class WorldMarker {
    public string Key { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = string.Empty;
    public string? SubLabel { get; set; }
    public uint IconId { get; set; }
    public int IconSize { get; set; } = 28;
    public Vector3 Position { get; set; }
    public float FadeNear { get; set; } = 32.0f;
    public float FadeFar { get; set; } = 42.0f;
    public float MaxVisibleDistance { get; set; } = 0.0f;
    public bool ShowOnCompass { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public uint MapId { get; set; }

    internal Vector2? LastScreenPos { get; set; }
    internal bool LastIsOnScreen { get; set; }
    internal float LastDistance { get; set; }

    private Vector3 _lastPos;
    private Vector3 _worldPos;

    internal Vector3 GetWorldPosition(WorldMarkerRaycaster? raycaster) {
        if (Position == _lastPos) return _worldPos;

        _lastPos = Position;

        if (Position.Y == 0 && raycaster != null) {
            _worldPos = raycaster.Raycast(Position);
        } else {
            _worldPos = Position;
        }

        return _worldPos;
    }
}

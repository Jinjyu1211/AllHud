using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System.Numerics;

namespace AllHud.Markers;

internal sealed class WorldMarkerRenderer : IDisposable {
    private readonly IGameGui _gameGui;
    private readonly IObjectTable _objectTable;
    private readonly WorldMarkerRegistry _registry;
    private readonly ITextureProvider _textureProvider;
    private readonly Dictionary<uint, IDalamudTextureWrap?> _iconCache = new();

    public WorldMarkerRenderer(
        IGameGui gameGui,
        IObjectTable objectTable,
        WorldMarkerRegistry registry,
        ITextureProvider textureProvider) {
        _gameGui = gameGui;
        _objectTable = objectTable;
        _registry = registry;
        _textureProvider = textureProvider;
    }

    public void Draw(Configuration config) {
        try {
            DrawInternal(null, config);
        } catch {
        }
    }

    public void DrawWithDebug(List<WorldMarkerFactory> factories, Configuration config) {
        try {
            DrawInternal(factories, config);
        } catch {
        }
    }

    private void DrawInternal(List<WorldMarkerFactory>? factories, Configuration config) {
        var localPlayer = _objectTable.LocalPlayer;
        var markers = _registry.Markers;
        var drawList = ImGui.GetForegroundDrawList();

        var camFwd = GetCameraForward();
        Vector2 playerScreenPos = Vector2.Zero;
        bool hasPlayerScreen = false;
        if (localPlayer is not null && config.ShowCompass) {
            hasPlayerScreen = _gameGui.WorldToScreen(localPlayer.Position, out playerScreenPos);
        }

        foreach (var marker in markers) {
            var worldPos = marker.Position;
            if (worldPos.Y == 0 && localPlayer is not null) {
                worldPos = new Vector3(worldPos.X, localPlayer.Position.Y, worldPos.Z);
            }

            float distance = localPlayer is not null
                ? Vector3.Distance(localPlayer.Position, worldPos)
                : 0f;

            if (marker.MaxVisibleDistance > 0 && distance > marker.MaxVisibleDistance) continue;

            if (_gameGui.WorldToScreen(worldPos, out var screenPos)) {
                if (!marker.IsVisible) continue;
                float alpha = 1f;
                if (marker.FadeFar > 0 && distance > marker.FadeNear) {
                    alpha = Math.Clamp(1f - (distance - marker.FadeNear) / Math.Max(0.001f, marker.FadeFar - marker.FadeNear), 0f, 1f);
                }
                marker.ShowDistance = config.ShowMarkerDistance;
                DrawMarker(drawList, screenPos, marker, alpha, distance);
            } else if (config.ShowCompass && marker.ShowOnCompass && hasPlayerScreen) {
                DrawCompassMarker(drawList, screenPos, playerScreenPos, marker, worldPos, distance, camFwd, config);
            }
        }
    }

    public void DrawPlayerPosition(Configuration config) {
        if (!config.ShowWorldMarkers || !config.ShowPlayerPositionMarker) return;

        try {
            var player = _objectTable.LocalPlayer;
            if (player is null) return;

            var pos = player.Position;
            if (!_gameGui.WorldToScreen(pos, out var screenPos)) return;

            float alpha = 1f;

            var mapCoords = MapCoordinateConverter.WorldToMap(pos.X, pos.Z);
            var marker = new WorldMarker {
                IconSize = 24,
                FadeNear = 8f,
                FadeFar = 18f,
            };
            if (config.ShowPlayerPositionLabel) {
                marker.Label = player.Name.ToString();
                marker.SubLabel = $"X:{mapCoords.X:F1}  Y:{mapCoords.Y:F1}";
            }
            DrawMarker(ImGui.GetForegroundDrawList(), screenPos, marker, alpha, 0f);
        } catch {
        }
    }

    private void DrawMarker(ImDrawListPtr drawList, Vector2 pos, WorldMarker marker, float alpha, float distance) {
        var iconSize = (float)marker.IconSize;
        var iconMin = new Vector2(pos.X - iconSize / 2f, pos.Y - iconSize / 2f);
        var iconMax = iconMin + new Vector2(iconSize);

        if (marker.IconId > 0 && TryDrawGameIcon(drawList, marker.IconId, iconMin, iconMax, alpha)) {
        } else {
            DrawFallbackCircle(drawList, iconMin, iconMax, alpha);
        }

        float cursorY = iconMax.Y + 2f;

        if (!string.IsNullOrWhiteSpace(marker.Label)) {
            var labelColor = new Vector4(1f, 1f, 1f, alpha);
            var bgSize = ImGui.CalcTextSize(marker.Label);
            var bgMin = new Vector2(pos.X - bgSize.X / 2f, cursorY);
            var bgMax = bgMin + bgSize + new Vector2(8f, 4f);
            drawList.AddRectFilled(bgMin, bgMax,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f * alpha)), 4f);
            drawList.AddText(new Vector2(bgMin.X + 4f, bgMin.Y + 2f),
                ImGui.GetColorU32(labelColor), marker.Label);
            cursorY = bgMax.Y + 1f;
        }

        if (!string.IsNullOrWhiteSpace(marker.SubLabel)) {
            var subColor = new Vector4(1f, 0.92f, 0.6f, alpha);
            var subSize = ImGui.CalcTextSize(marker.SubLabel);
            var subMin = new Vector2(pos.X - subSize.X / 2f, cursorY);
            var subBgMax = subMin + subSize + new Vector2(8f, 4f);
            drawList.AddRectFilled(subMin, subBgMax,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f * alpha)), 4f);
            drawList.AddText(new Vector2(subMin.X + 4f, subMin.Y + 2f),
                ImGui.GetColorU32(subColor), marker.SubLabel);
            cursorY = subBgMax.Y + 1f;
        }

        if (marker.ShowDistance && distance > 0.1f) {
            var distText = $"{MathF.Round(distance)} yalms";
            var distColor = new Vector4(0.75f, 0.85f, 1f, alpha * 0.85f);
            var distSize = ImGui.CalcTextSize(distText);
            var distMin = new Vector2(pos.X - distSize.X / 2f, cursorY);
            var distMax = distMin + distSize + new Vector2(8f, 4f);
            drawList.AddRectFilled(distMin, distMax,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f * alpha)), 4f);
            drawList.AddText(new Vector2(distMin.X + 4f, distMin.Y + 2f),
                ImGui.GetColorU32(distColor), distText);
        }
    }

    private unsafe (Vector3 Origin, Vector3 Direction) GetCameraForward() {
        try {
            var cameraManager = CameraManager.Instance();
            if (cameraManager == null) return (Vector3.Zero, Vector3.UnitZ);
            var camera = cameraManager->CurrentCamera;
            if (camera == null) return (Vector3.Zero, Vector3.UnitZ);
            Vector3 origin = camera->Position;
            Vector3 lookAt = camera->LookAtVector;
            var dir = lookAt - origin;
            if (dir.LengthSquared() < 0.001f) return (origin, Vector3.UnitZ);
            return (origin, Vector3.Normalize(dir));
        } catch {
            return (Vector3.Zero, Vector3.UnitZ);
        }
    }

    private void DrawCompassMarker(
        ImDrawListPtr drawList, Vector2 markerScreenPos, Vector2 playerScreenPos,
        WorldMarker marker, Vector3 worldPos, float distance,
        (Vector3 Origin, Vector3 Direction) camFwd, Configuration config) {
        var toMarker = worldPos - camFwd.Origin;
        bool isInFront = Vector3.Dot(camFwd.Direction, toMarker) > 0;

        var dir = markerScreenPos - playerScreenPos;
        if (!isInFront) dir = -dir;
        if (dir.LengthSquared() < 1f) return;
        dir = Vector2.Normalize(dir);

        var vp = ImGui.GetMainViewport();
        var vpSize = vp.Size;
        var workPos = vp.WorkPos;

        float iconSize = 24f * (config.CompassIconScale / 100f);
        float clampSize = iconSize * 1.5f;
        var vpClamped = new Vector2(vpSize.X - clampSize, vpSize.Y - clampSize);

        var compassPos = playerScreenPos + dir * config.CompassRadius;
        compassPos.X = Math.Clamp(compassPos.X, clampSize, vpClamped.X);
        compassPos.Y = Math.Clamp(compassPos.Y, clampSize, vpClamped.Y);

        var p1 = compassPos - new Vector2(iconSize / 2f) + workPos;
        var p2 = compassPos + new Vector2(iconSize / 2f) + workPos;

        var bgDrawList = ImGui.GetBackgroundDrawList();
        if (marker.IconId > 0) {
            TryDrawGameIcon(bgDrawList, marker.IconId, p1, p2, 0.8f);
        }

        var arrowColor = ImGui.GetColorU32(new Vector4(1f, 0.85f, 0.3f, 0.9f));
        DrawDirectionArrow(bgDrawList, compassPos + workPos, dir, iconSize, arrowColor);
    }

    private static void DrawDirectionArrow(ImDrawListPtr drawList, Vector2 screenPos, Vector2 direction, float size, uint color) {
        float halfSize = size * 0.35f;
        var perp = new Vector2(-direction.Y, direction.X);

        var tip = screenPos + direction * halfSize;
        var left = screenPos - direction * halfSize * 0.5f + perp * halfSize * 0.5f;
        var right = screenPos - direction * halfSize * 0.5f - perp * halfSize * 0.5f;

        drawList.AddTriangleFilled(tip, left, right, color);
        drawList.AddTriangle(tip, left, right, ImGui.GetColorU32(new Vector4(0, 0, 0, 0.5f)), 1f);
    }

    private static void DrawFallbackCircle(ImDrawListPtr drawList, Vector2 min, Vector2 max, float alpha) {
        var center = (min + max) / 2f;
        var size = max.X - min.X;
        var halfSize = size / 2f;

        var p0 = new Vector2(center.X, center.Y + halfSize);
        var p1 = new Vector2(center.X - halfSize * 0.6f, center.Y - halfSize * 0.3f);
        var p2 = new Vector2(center.X, center.Y - halfSize * 0.8f);
        var p3 = new Vector2(center.X + halfSize * 0.6f, center.Y - halfSize * 0.3f);

        drawList.AddTriangleFilled(p1, p2, p3, ImGui.GetColorU32(new Vector4(1f, 0.4f, 0.4f, alpha)));
        drawList.AddLine(p0, p1, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 2f);
        drawList.AddLine(p0, p3, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 2f);
        drawList.AddLine(p1, p2, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 2f);
        drawList.AddLine(p2, p3, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 2f);

        drawList.AddLine(new(center.X - halfSize * 0.3f, center.Y + halfSize),
            new(center.X + halfSize * 0.3f, center.Y + halfSize),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 2f);
    }

    private bool TryDrawGameIcon(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max, float alpha) {
        if (!_iconCache.TryGetValue(iconId, out var wrap) || wrap is null) {
            try {
                var lookup = new GameIconLookup { IconId = iconId, HiRes = true };
                if (_textureProvider.TryGetFromGameIcon(lookup, out var texture) && texture is not null) {
                    texture.TryGetWrap(out wrap, out _);
                }
                _iconCache[iconId] = wrap;
            } catch {
                _iconCache[iconId] = null;
                return false;
            }
        }

        if (wrap is null) {
            _iconCache.Remove(iconId);
            return false;
        }

        var bounds = max - min;
        var sourceAspect = wrap.Size.X / Math.Max(1.0f, wrap.Size.Y);
        var boundsAspect = bounds.X / Math.Max(1.0f, bounds.Y);
        var uvMin = Vector2.Zero;
        var uvMax = Vector2.One;

        if (sourceAspect > boundsAspect) {
            var visibleWidthRatio = boundsAspect / sourceAspect;
            var trim = (1.0f - visibleWidthRatio) * 0.5f;
            uvMin.X = trim;
            uvMax.X = 1.0f - trim;
        } else if (sourceAspect < boundsAspect) {
            var visibleHeightRatio = sourceAspect / boundsAspect;
            var trim = (1.0f - visibleHeightRatio) * 0.5f;
            uvMin.Y = trim;
            uvMax.Y = 1.0f - trim;
        }

        drawList.AddImage(wrap.Handle, min, max, uvMin, uvMax, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        return true;
    }

    public void Dispose() {
        _iconCache.Clear();
        GC.SuppressFinalize(this);
    }
}

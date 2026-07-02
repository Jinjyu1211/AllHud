using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace AllHud.Markers;

internal sealed class WorldMarkerRenderer : IDisposable {
    private readonly IGameGui _gameGui;
    private readonly IObjectTable _objectTable;
    private readonly WorldMarkerRegistry _registry;

    public WorldMarkerRenderer(
        IGameGui gameGui,
        IObjectTable objectTable,
        WorldMarkerRegistry registry) {
        _gameGui = gameGui;
        _objectTable = objectTable;
        _registry = registry;
    }

    public void Draw() {
        try {
            DrawInternal(null);
        } catch {
        }
    }

    public void DrawWithDebug(List<WorldMarkerFactory> factories) {
        try {
            DrawInternal(factories);
        } catch {
        }
    }

    private void DrawInternal(List<WorldMarkerFactory>? factories = null) {
        var localPlayer = _objectTable.LocalPlayer;
        var markers = _registry.Markers;
        var drawList = ImGui.GetForegroundDrawList();

        foreach (var marker in markers) {
            var worldPos = marker.Position;
            if (worldPos.Y == 0 && localPlayer is not null) {
                worldPos = new Vector3(worldPos.X, localPlayer.Position.Y, worldPos.Z);
            }

            if (!_gameGui.WorldToScreen(worldPos, out var screenPos)) {
                continue;
            }

            if (!marker.IsVisible) continue;

            float distance = 0f;
            if (localPlayer is not null) {
                distance = Vector3.Distance(localPlayer.Position, worldPos);
            }

            float alpha = 1f;
            if (marker.FadeFar > 0 && distance > marker.FadeNear) {
                alpha = Math.Clamp(1f - (distance - marker.FadeNear) / Math.Max(0.001f, marker.FadeFar - marker.FadeNear), 0f, 1f);
            }

            DrawMarker(drawList, screenPos, marker, alpha);
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
            DrawMarker(ImGui.GetForegroundDrawList(), screenPos, marker, alpha);
        } catch {
        }
    }

    private static void DrawMarker(ImDrawListPtr drawList, Vector2 pos, WorldMarker marker, float alpha) {
        var iconSize = (float)marker.IconSize;
        var iconMin = new Vector2(pos.X - iconSize / 2f, pos.Y - iconSize / 2f);
        var iconMax = iconMin + new Vector2(iconSize);

        DrawFallbackCircle(drawList, iconMin, iconMax, alpha);

        if (!string.IsNullOrWhiteSpace(marker.Label)) {
            var labelColor = new Vector4(1f, 1f, 1f, alpha);
            var bgSize = ImGui.CalcTextSize(marker.Label);
            var bgMin = new Vector2(pos.X - bgSize.X / 2f, iconMax.Y + 2f);
            var bgMax = bgMin + bgSize + new Vector2(8f, 4f);
            drawList.AddRectFilled(bgMin, bgMax,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f * alpha)), 4f);
            drawList.AddText(new Vector2(bgMin.X + 4f, bgMin.Y + 2f),
                ImGui.GetColorU32(labelColor), marker.Label);
        }

        if (!string.IsNullOrWhiteSpace(marker.SubLabel)) {
            var subColor = new Vector4(1f, 0.92f, 0.6f, alpha);
            var subSize = ImGui.CalcTextSize(marker.SubLabel);
            var labelH = string.IsNullOrWhiteSpace(marker.Label) ? 0 : ImGui.CalcTextSize(marker.Label).Y + 6f;
            var subMin = new Vector2(pos.X - subSize.X / 2f, iconMax.Y + 2f + labelH);
            var subBgMax = subMin + subSize + new Vector2(8f, 4f);
            drawList.AddRectFilled(subMin, subBgMax,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.45f * alpha)), 4f);
            drawList.AddText(new Vector2(subMin.X + 4f, subMin.Y + 2f),
                ImGui.GetColorU32(subColor), marker.SubLabel);
        }
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

    public void Dispose() {
        GC.SuppressFinalize(this);
    }
}

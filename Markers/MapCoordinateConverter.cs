using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AllHud.Markers;

internal static class MapCoordinateConverter {
    public static unsafe (float X, float Y) WorldToMap(float worldX, float worldZ) {
        var agentMap = AgentMap.Instance();
        if (agentMap is null || agentMap->CurrentMapId == 0) {
            return (worldX, worldZ);
        }

        var scale = (uint)agentMap->CurrentMapSizeFactor;
        var offsetX = -agentMap->CurrentOffsetX;
        var offsetY = -agentMap->CurrentOffsetY;

        var mapX = (0.02f * offsetX) + (2048f / scale) + (0.02f * worldX) + 1.0f;
        var mapY = (0.02f * offsetY) + (2048f / scale) + (0.02f * worldZ) + 1.0f;

        return (mapX, mapY);
    }
}

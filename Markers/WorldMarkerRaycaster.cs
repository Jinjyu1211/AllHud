using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System.Numerics;

namespace AllHud.Markers;

/// <summary>
/// 世界标记射线投射器。借鉴 Umbra.WorldMarkerRaycaster。
/// 当标记 Y=0 时，自动计算地面高度，使标记正确显示在地面上。
/// </summary>
internal sealed class WorldMarkerRaycaster {
    private readonly IObjectTable _objectTable;

    public WorldMarkerRaycaster(IObjectTable objectTable) {
        _objectTable = objectTable;
    }

    public Vector3 Raycast(Vector3 position) {
        if (position.Y != 0) {
            return position;
        }

        var player = _objectTable.LocalPlayer;
        float fallbackY = player?.Position.Y ?? 0f;

        unsafe {
            // 先向上投射找天花板，确定起始高度
            if (BGCollisionModule.RaycastMaterialFilter(position, new(0, 1, 0), out var hitInfo)) {
                position.Y = hitInfo.Point.Y + 1.8f;
            } else {
                position.Y = fallbackY + 250;
            }

            // 再向下投射到地面
            if (BGCollisionModule.RaycastMaterialFilter(position, new(0, -1, 0), out var hitInfo2)) {
                position.Y = hitInfo2.Point.Y + 1f;
            } else {
                position.Y += 500;
                if (BGCollisionModule.RaycastMaterialFilter(position, new(0, -1, 0), out var hitInfo3)) {
                    position.Y = hitInfo3.Point.Y + 1f;
                } else {
                    position.Y = fallbackY;
                }
            }
        }

        return position;
    }
}

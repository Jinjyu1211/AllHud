using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AllHud.Markers;

/// <summary>
/// 采集点标记工厂。借鉴 Umbra.GatheringNodeMarkerFactory。
/// 遍历 ObjectTable 找 GatheringPoint，查 Lumina sheet 取图标、等级、产出。
/// </summary>
internal sealed class GatheringNodeMarkerFactory : WorldMarkerFactory {
    public override string Id => "GatheringNodeMarkers";
    public override string Name => "采集点";

    private readonly Configuration _config;
    private readonly IDataManager _dataManager;
    private readonly IObjectTable _objectTable;
    private DateTime _lastScanAt = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(1000);
    private int _displayIndex;
    private DateTime _lastDisplayFlip = DateTime.MinValue;
    private static readonly TimeSpan DisplayFlipInterval = TimeSpan.FromMilliseconds(2000);

    public GatheringNodeMarkerFactory(
        Configuration config,
        IDataManager dataManager,
        IObjectTable objectTable) {
        _config = config;
        _dataManager = dataManager;
        _objectTable = objectTable;
    }

    protected override void OnTick(DateTime now) {
        if (!_config.ShowGatheringNodeMarkers) {
            RemoveAllMarkers();
            return;
        }

        if (now - _lastScanAt < ScanInterval) return;
        _lastScanAt = now;

        if (now - _lastDisplayFlip > DisplayFlipInterval) {
            _lastDisplayFlip = now;
            _displayIndex++;
            if (_displayIndex > 1000) _displayIndex = 0;
        }

        if (_objectTable.LocalPlayer is null) {
            RemoveAllMarkers();
            return;
        }

        ScanNodes();
    }

    private void ScanNodes() {
        var pointSheet = _dataManager.GetExcelSheet<GatheringPoint>();
        var baseSheet = _dataManager.GetExcelSheet<GatheringPointBase>();
        var gItemSheet = _dataManager.GetExcelSheet<GatheringItem>();
        var itemSheet = _dataManager.GetExcelSheet<Item>();
        var typeSheet = _dataManager.GetExcelSheet<GatheringType>();
        if (pointSheet is null || baseSheet is null || gItemSheet is null || itemSheet is null || typeSheet is null) {
            return;
        }

        List<string> activeKeys = new();
        int totalObjects = 0;
        int gatheringPoints = 0;
        foreach (var obj in _objectTable) {
            totalObjects++;
            if (obj is null || !obj.IsTargetable) continue;
            if (obj.ObjectKind == ObjectKind.GatheringPoint) {
                gatheringPoints++;
                var node = CreateNode(obj, pointSheet, baseSheet, gItemSheet, itemSheet, typeSheet);
                if (node is null) continue;

                activeKeys.Add(node.Key);
                SetMarker(node);
            }
        }

        // 移除已消失的
        foreach (var staleKey in ActiveMarkers.Select(m => m.Key).Except(activeKeys).ToList()) {
            RemoveMarker(staleKey);
        }

        _lastScanStats = (totalObjects, gatheringPoints, activeKeys.Count);
    }

    private (int total, int gathering, int created) _lastScanStats;
    private string _lastDebug = string.Empty;

    public override string GetDebugInfo() {
        var (total, gathering, created) = _lastScanStats;
        return $"{Id}: {total} objs, {gathering} gathering, {created} markers | {_lastDebug}";
    }

    private WorldMarker? CreateNode(
        IGameObject obj,
        ExcelSheet<GatheringPoint> pointSheet,
        ExcelSheet<GatheringPointBase> baseSheet,
        ExcelSheet<GatheringItem> gItemSheet,
        ExcelSheet<Item> itemSheet,
        ExcelSheet<GatheringType> typeSheet) {
        var baseId = obj.BaseId;
        if (!pointSheet.TryGetRow(baseId, out var point)) {
            _lastDebug = $"TryGetRow(GatheringPoint.{baseId}) failed";
            return null;
        }
        if (!baseSheet.TryGetRow(point.GatheringPointBase.RowId, out var pointBase)) {
            _lastDebug = $"TryGetRow(GatheringPointBase.{point.GatheringPointBase.RowId}) failed";
            return null;
        }

        uint iconId = 0;
        if (typeSheet.TryGetRow(pointBase.GatheringType.RowId, out var type)) {
            iconId = (uint)type.IconMain;
        }

        string? subLabel = null;
        var names = new List<string>();
        foreach (var itemRef in pointBase.Item) {
            if (itemRef.RowId == 0) continue;
            if (!gItemSheet.TryGetRow(itemRef.RowId, out var gItem)) continue;
            if (!itemSheet.TryGetRow(gItem.Item.RowId, out var realItem)) continue;
            var name = realItem.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
        }
        if (names.Count > 0) {
            subLabel = $"{point.Count}x {names[_displayIndex % names.Count]}";
        }

        var key = $"GN_{obj.Position.X:F0}_{obj.Position.Y:F0}_{obj.Position.Z:F0}";
        return new WorldMarker {
            Key = key,
            Label = $"Lv.{pointBase.GatheringLevel} {obj.Name}",
            SubLabel = subLabel,
            IconId = iconId,
            IconSize = 28,
            Position = obj.Position,
            FadeNear = _config.WorldMarkerFadeDistance,
            FadeFar = _config.WorldMarkerFadeDistance + _config.WorldMarkerFadeAttenuation,
            MaxVisibleDistance = _config.WorldMarkerMaxVisibleDistance,
            ShowOnCompass = true,
        };
    }
}

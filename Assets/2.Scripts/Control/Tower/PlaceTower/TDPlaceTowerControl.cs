using System;
using TDEnums;
using UnityEngine;

public class TDPlaceTowerControl
{
    public static TDPlaceTowerControl api;

    public Action<bool> onPlaceTowerSuccess;

    public void CheckPlaceTower(Vector3 position, GameObject currentTower, TDTowerSlotInfo slotInfo)
    {
        if (slotInfo.towerType == TowerType.Operator)
            CheckPlaceOperator(position, currentTower, slotInfo);
        else
            CheckPlaceRegularTower(position, currentTower, slotInfo);
    }

    // ── Regular tower ─────────────────────────────────────────────────────────

    private void CheckPlaceRegularTower(Vector3 position, GameObject currentTower, TDTowerSlotInfo slotInfo)
    {
        if (!TDGridMainModel.api.IsValidPlacement(position)) return;

        Vector3 nearestPosition = TDGridMainModel.api.GetNearestGridPosition(position);
        Vector3 placedPosition  = new(nearestPosition.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearestPosition.z);
        Quaternion rotation     = currentTower.transform.rotation;

        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPosition, rotation, slotInfo);
        TDGridMainModel.api.SetOccupiedCell(nearestPosition);
        onPlaceTowerSuccess?.Invoke(true);
    }

    // ── Operator — dispatch qua IOperatorBehavior strategy ────────────────────

    private void CheckPlaceOperator(Vector3 position, GameObject currentTower, TDTowerSlotInfo slotInfo)
    {
        var data = TDFlyweightOperatorDataSettings.api.GetData(slotInfo.operatorType);
        if (data == null) return;

        var behavior = TDControl.CreateOperatorBehavior(data.deployZone);
        if (!behavior.CanPlace(position)) return;

        behavior.Place(position, currentTower.transform.rotation, slotInfo);
        onPlaceTowerSuccess?.Invoke(true);
    }
}

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
        Vector3 placedPosition = new(nearestPosition.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearestPosition.z);
        Quaternion rotation = currentTower.transform.rotation;

        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPosition, rotation, slotInfo);
        TDGridMainModel.api.SetOccupiedCell(nearestPosition);
        onPlaceTowerSuccess?.Invoke(true);
    }

    // ── Operator ──────────────────────────────────────────────────────────────

    private void CheckPlaceOperator(Vector3 position, GameObject currentTower, TDTowerSlotInfo slotInfo)
    {
        if (TDOperatorRegistry.api == null) return;

        Vector3 nearestPos = TDGridMainModel.api.GetNearestGridPosition(position);
        Vector2Int cell = TDGridMainModel.api.WorldToCell(nearestPos);

        if (!TDOperatorRegistry.api.IsValidOperatorCell(cell)) return;
        if (TDOperatorRegistry.api.HasOperatorAt(cell)) return;

        Vector3 placedPos = new(nearestPos.x, TDConstant.CONFIG_OPERATOR_PLACE_Y, nearestPos.z);
        Quaternion rotation = currentTower.transform.rotation;

        // RegisterOperator đã chuyển vào TDOperatorView.Init() — không gọi ở đây nữa
        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPos, rotation, slotInfo);
        onPlaceTowerSuccess?.Invoke(true);
    }
}

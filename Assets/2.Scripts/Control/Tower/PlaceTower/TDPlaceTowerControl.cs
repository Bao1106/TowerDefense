using System;
using TDEnums;
using UnityEngine;

public class TDPlaceTowerControl
{
    public static TDPlaceTowerControl api;

    public Action<bool> onPlaceTowerSuccess;

    public void CheckPlaceTower(Vector3 position, GameObject currentTower, TowerType towerType = TowerType.Cannon)
    {
        if (towerType == TowerType.Melee)
            CheckPlaceMeleeOperator(position, currentTower);
        else
            CheckPlaceRegularTower(position, currentTower);
    }

    // ── Regular tower — đặt lên TowerZone cells ──────────────────────────────
    private void CheckPlaceRegularTower(Vector3 position, GameObject currentTower)
    {
        if (!TDGridMainModel.api.IsValidPlacement(position)) return;

        Vector3    nearestPosition = TDGridMainModel.api.GetNearestGridPosition(position);
        Vector3    placedPosition  = new Vector3(nearestPosition.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearestPosition.z);
        Quaternion rotation        = currentTower.transform.rotation;

        TDTowerFactoryControl.api.CreateTower(currentTower, placedPosition, rotation);
        onPlaceTowerSuccess?.Invoke(true);
        TDGridMainModel.api.SetOccupiedCell(nearestPosition);
    }

    // ── Melee operator — đặt lên path cells ──────────────────────────────────
    private void CheckPlaceMeleeOperator(Vector3 position, GameObject currentTower)
    {
        if (TDMeleeRegistry.api == null) return;

        Vector3    nearestPos = TDGridMainModel.api.GetNearestGridPosition(position);
        Vector2Int cell       = TDGridMainModel.api.WorldToCell(nearestPos);

        // Chỉ cho phép đặt lên valid path cell chưa có operator
        if (!TDMeleeRegistry.api.IsValidMeleeCell(cell))  return;
        if ( TDMeleeRegistry.api.HasOperatorAt(cell))      return;

        // Đặt melee operator tại path cell, cùng độ cao với tower thường
        Vector3    placedPos = new Vector3(nearestPos.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearestPos.z);
        Quaternion rotation  = currentTower.transform.rotation;

        TDTowerFactoryControl.api.CreateTower(currentTower, placedPos, rotation);

        // Đăng ký operator — blockCapacity = maxTargets từ SO
        int blockCapacity = TDFlyweightBulletFactoryModel.api?.Setting?.GetData(TDEnums.TowerType.Melee)?.maxTargets ?? 1;
        TDMeleeRegistry.api.RegisterOperator(cell, blockCapacity);

        onPlaceTowerSuccess?.Invoke(true);
        // Không gọi SetOccupiedCell — path cells đã occupied từ VisualizeAllPaths
        // TDMeleeRegistry.HasOperatorAt() ngăn double-place
    }
}

using System;
using TDEnums;
using UnityEngine;

public class TDTowerMainControl
{
    public static TDTowerMainControl api;

    public Action<string, TowerType> onGetTowerName;
    public Action<int>               onGetCurrentRotationIndex;

    private static readonly (string name, TowerType type)[] k_TowerDefs =
    {
        (TDConstant.PREFAB_FATTY_CANNON_G02,    TowerType.Cannon),
        (TDConstant.PREFAB_FATTY_CATAPULT_G02,  TowerType.Catapult),
        (TDConstant.PREFAB_FATTY_MISSILE_G02,   TowerType.MissileG02),
        (TDConstant.PREFAB_FATTY_MISSILE_G03,   TowerType.MissileG03),
        (TDConstant.PREFAB_FATTY_MORTAR_G02,    TowerType.Mortar),
        (TDConstant.PREFAB_MELEE_OPERATOR,      TowerType.Melee),   // slot 5 — Arknights-style guard
    };

    public void OnSelectTowerHolder(int index)
    {
        if (index < 0 || index >= k_TowerDefs.Length) return;
        var (name, type) = k_TowerDefs[index];
        onGetTowerName?.Invoke(name, type);
    }

    public void OnSelectTower(GameObject currentTower)
    {
        if (currentTower != null)
        {
            if (Camera.main == null) return;
                
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit))
            {
                Vector3 gridPosition = TDGridMainModel.api.GetNearestGridPosition(hit.point);
                currentTower.transform.position = gridPosition;
            }
        }
    }

    public void OnPlaceTower(GameObject currentTower, TowerType towerType = TowerType.Cannon)
    {
        if (currentTower == null) return;

        // Dùng transform.position của ghost tower (đã snap vào grid qua OnSelectTower)
        // Không raycast lại từ Input.mousePosition để tránh sai khi bấm UI button (Confirm)
        TDPlaceTowerControl.api.CheckPlaceTower(currentTower.transform.position, currentTower, towerType);
    }
    
    public void RotateTowerClockwise(GameObject currentTower, int currentRotationIndex)
    {
        if (currentTower != null)
        {
            int currentRotation = (currentRotationIndex + 1) % 4;
            UpdateTowerRotation(currentTower, currentRotation);
            onGetCurrentRotationIndex?.Invoke(currentRotation);
        }
    }

    public void RotateTowerCounterClockwise(GameObject currentTower, int currentRotationIndex)
    {
        if (currentTower != null)
        {
            int currentRotation = (currentRotationIndex - 1 + 4) % 4;
            UpdateTowerRotation(currentTower, currentRotation);
            onGetCurrentRotationIndex?.Invoke(currentRotation);
        }
    }
    
    private void UpdateTowerRotation(GameObject currentTower, int currentRotationIndex)
    {
        currentTower.transform.rotation = Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[currentRotationIndex], 0f);
    }
}

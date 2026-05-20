using System;
using UnityEngine;

public class TDPlaceTowerControl
{
    public static TDPlaceTowerControl api;

    public Action<bool> onPlaceTowerSuccess;
    
    public void CheckPlaceTower(Vector3 position, GameObject currentTower)
    {
        // Kiểm tra xem vị trí có hợp lệ không
        if (TDGridMainModel.api.IsValidPlacement(position))
        {
            Vector3 nearestPosition = TDGridMainModel.api.GetNearestGridPosition(position);
            Quaternion rotation = currentTower.transform.rotation;
            TDTowerFactoryControl.api.CreateTower(currentTower, nearestPosition, rotation);
            onPlaceTowerSuccess?.Invoke(true);
                
            TDGridMainModel.api.SetOccupiedCell(nearestPosition);
        }
    }
}

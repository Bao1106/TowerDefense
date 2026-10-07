using System;
using TDEnums;
using UnityEngine;

public class TDPlaceTowerControl
{
    public static TDPlaceTowerControl api;

    public Action<bool> onPlaceTowerSuccess;

    public void CheckPlaceTower(Vector3 position, GameObject currentTower, TDTowerSlotInfo slotInfo)
    {
        // The last point before a unit exists, and therefore the only place the deploy rules are
        // actually true. Everything upstream is decoration: the deploy bar greys a card by setting
        // Button.interactable, which gates the Button's OWN onClick but does not stop the
        // EventSystem delivering PointerDown to the EventTrigger sitting on the same object — and
        // the gold is not spent until AFTER the unit is built, with SpendGold's false return
        // thrown away. Both gates could be walked straight through.
        if ((TDGoldControl.api?.Gold ?? 0) < slotInfo.cost) return;

        if (TDDeployCap.api != null && TDDeployCap.api.IsFull)
        {
            TDDeployCap.api.NotifyRejected();
            return;
        }

        if (slotInfo.towerType == TowerType.Operator)
            CheckPlaceOperator(position, currentTower, slotInfo);
        else
            CheckPlaceRegularTower(position, currentTower, slotInfo);
    }

    // ── Regular tower ─────────────────────────────────────────────────────────

    private void CheckPlaceRegularTower(Vector3 position, GameObject currentTower, TDTowerSlotInfo slotInfo)
    {
        if (!TDGridMainModel.api.IsValidPlacement(position)) return;
        if (!TDGridMainModel.api.IsInTowerZone(TDGridMainModel.api.WorldToCell(position))) return;

        Vector3 nearestPosition = TDGridMainModel.api.GetNearestGridPosition(position);
        Vector3 placedPosition = new(nearestPosition.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearestPosition.z);
        Quaternion rotation = currentTower.transform.rotation;

        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPosition, rotation, slotInfo);
        TDGridMainModel.api.SetOccupiedCell(nearestPosition);
        onPlaceTowerSuccess?.Invoke(true);
    }

    // ── Operator — dispatch qua IOperatorBehavior strategy ────────────────────

    private void CheckPlaceOperator(Vector3 position, GameObject currentTower, TDTowerSlotInfo slotInfo)
    {
        var data = slotInfo.operatorData;
        if (data == null) return;

        // One copy on the field, and not until the retreat cooldown is done.
        if (TDOperatorRoster.api != null && !TDOperatorRoster.api.CanDeploy(data)) return;

        var behavior = TDControl.CreateOperatorBehavior(data.deployZone);
        if (!behavior.CanPlace(position)) return;

        behavior.Place(position, currentTower.transform.rotation, slotInfo);
        onPlaceTowerSuccess?.Invoke(true);
    }
}

using TDEnums;
using UnityEngine;

/// <summary>
/// Operator đặt trên path cell: block enemy, melee attack.
/// Dùng cho Knight / Defender / Striker và các custom melee variants.
/// </summary>
public class PathCellOperatorBehavior : IOperatorBehavior
{
    // ── Placement ─────────────────────────────────────────────────────────────

    public bool CanPlace(Vector3 worldPos)
    {
        if (TDOperatorRegistry.api == null) return false;
        Vector2Int cell = TDGridMainModel.api.WorldToCell(worldPos);
        return TDOperatorRegistry.api.IsValidOperatorCell(cell)
            && !TDOperatorRegistry.api.HasOperatorAt(cell);
    }

    public void Place(Vector3 worldPos, Quaternion rotation, TDTowerSlotInfo slotInfo)
    {
        Vector3 nearest    = TDGridMainModel.api.GetNearestGridPosition(worldPos);
        Vector3 placedPos  = new(nearest.x, TDConstant.CONFIG_OPERATOR_PLACE_Y, nearest.z);
        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPos, rotation, slotInfo);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void OnInit(Vector2Int cell, OperatorData data, TDOperatorView view)
    {
        int blockCap = Mathf.Clamp(data?.blockCount ?? 1, 1, 3);
        TDOperatorRegistry.api?.RegisterOperator(cell, blockCap);
        TDOperatorRegistry.api?.RegisterOperatorView(cell, view);
    }

    public void OnRemove(Vector2Int cell, Vector3 worldPos)
    {
        TDOperatorRegistry.api?.UnregisterOperator(cell);
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    public bool TryAttack(Vector2Int cell, Vector3 worldPos, OperatorData data)
    {
        if (TDOperatorRegistry.api == null) return false;

        var blocked = TDOperatorRegistry.api.GetBlockedEnemies(cell);
        if (blocked.Count == 0) return false;

        float dmg = data?.damage ?? 0f;

        if (data?.attackType == AttackType.Single)
            blocked[0]?.TakeDamage(dmg);
        else
            foreach (var enemy in blocked)
                enemy?.TakeDamage(dmg);

        TDGameEventBus.OperatorAttacked(worldPos, data?.operatorType ?? OperatorType.Knight);
        TDGameEventBus.OperatorImpacted(worldPos, data?.operatorType ?? OperatorType.Knight);
        return true;
    }

    public void ExecuteHit(Vector2Int cell, Vector3 worldPos, OperatorData data) { }
}

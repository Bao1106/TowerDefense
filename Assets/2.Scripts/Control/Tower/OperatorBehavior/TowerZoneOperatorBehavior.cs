using TDEnums;
using UnityEngine;

/// <summary>
/// Operator đặt trên tower zone: ranged attack, không block enemy.
/// Dùng cho Sniper (Ginger), Caster (Moon) và các TowerZone variants sau này.
/// </summary>
public class TowerZoneOperatorBehavior : IOperatorBehavior
{
    // ── Placement ─────────────────────────────────────────────────────────────

    public bool CanPlace(Vector3 worldPos)
        => TDGridMainModel.api.IsValidPlacement(worldPos);

    public void Place(Vector3 worldPos, Quaternion rotation, TDTowerSlotInfo slotInfo)
    {
        Vector3 nearest   = TDGridMainModel.api.GetNearestGridPosition(worldPos);
        Vector3 placedPos = new(nearest.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearest.z);
        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPos, rotation, slotInfo);
        TDGridMainModel.api.SetOccupiedCell(nearest);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void OnInit(Vector2Int cell, OperatorData data, TDOperatorView view)
    {
        // TowerZone operator không dùng TDOperatorRegistry — không block enemy
    }

    public void OnRemove(Vector2Int cell, Vector3 worldPos)
    {
        TDGridMainModel.api?.UnoccupyCell(worldPos);
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    public bool TryAttack(Vector2Int cell, Vector3 worldPos, OperatorData data)
    {
        if (data?.rangeOffsets == null || data.rangeOffsets.Length == 0) return false;

        float cellSize = TDGridMainModel.api?.cellSize ?? 2f;

        // Range = farthest rangeOffset magnitude + 0.5 cell margin
        float maxOffset = 0f;
        foreach (var offset in data.rangeOffsets)
            maxOffset = Mathf.Max(maxOffset, Mathf.Sqrt(offset.x * offset.x + offset.y * offset.y));
        float range = (maxOffset + 0.5f) * cellSize;

        // Find nearest enemy within sphere
        TDEnemyView nearest     = null;
        float       nearestDist = float.MaxValue;

        foreach (var col in Physics.OverlapSphere(worldPos, range))
        {
            if (!col.TryGetComponent<TDEnemyView>(out var enemy)) continue;
            float dist = Vector3.Distance(worldPos, col.transform.position);
            if (dist < nearestDist) { nearestDist = dist; nearest = enemy; }
        }

        if (nearest == null) return false;

        nearest.TakeDamage(data.damage);
        TDGameEventBus.TowerAttacked(worldPos, TowerType.Operator);
        return true;
    }
}

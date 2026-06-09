using System.Collections.Generic;
using TDEnums;
using UnityEngine;

/// <summary>
/// Operator placed on a tower zone: performs ranged attacks and does not block enemies.
/// Used for Sniper (Ginger), Caster (Moon), and any future TowerZone variants.
/// </summary>
public class TowerZoneOperatorBehavior : IOperatorBehavior
{
    // ── Placement ─────────────────────────────────────────────────────────────

    public bool CanPlace(Vector3 worldPos)
    {
        Vector2Int cell = TDGridMainModel.api.WorldToCell(worldPos);
        return TDGridMainModel.api.IsInTowerZone(cell)
            && TDGridMainModel.api.IsValidPlacement(worldPos);
    }

    public void Place(Vector3 worldPos, Quaternion rotation, TDTowerSlotInfo slotInfo)
    {
        Vector3 nearest   = TDGridMainModel.api.GetNearestGridPosition(worldPos);
        Vector3 placedPos = new(nearest.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearest.z);
        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPos, rotation, slotInfo);
        TDGridMainModel.api.SetOccupiedCell(nearest);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private Transform        m_OperatorTransform;
    private TDOffsetRangeDTO m_RangeDTO;

    public void OnInit(Vector2Int cell, OperatorData data, TDOperatorView view)
    {
        m_OperatorTransform = view.transform;
        m_RangeDTO          = new TDOffsetRangeDTO(data?.rangeOffsets);
    }

    public void OnRemove(Vector2Int cell, Vector3 worldPos)
    {
        TDGridMainModel.api?.UnoccupyCell(worldPos);
        m_OperatorTransform = null;
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    private TDEnemyView m_PendingTarget;

    public bool TryAttack(Vector2Int cell, Vector3 worldPos, OperatorData data)
    {
        if (m_RangeDTO == null || TDEnemyRegistry.api == null) return false;

        var validCells = new HashSet<Vector2Int>(
            m_RangeDTO.GetCellsInRange(cell, m_OperatorTransform.rotation));

        TDEnemyView nearest     = null;
        float       nearestDist = float.MaxValue;

        foreach (var enemy in TDEnemyRegistry.api.GetAll())
        {
            if (enemy == null) continue;
            var enemyCell = TDGridMainModel.api.WorldToCell(enemy.transform.position);
            if (!validCells.Contains(enemyCell)) continue;

            float dist = Vector3.Distance(worldPos, enemy.transform.position);
            if (dist < nearestDist) { nearestDist = dist; nearest = enemy; }
        }

        if (nearest == null) return false;

        m_PendingTarget = nearest;
        TDGameEventBus.OperatorAttacked(worldPos, data.operatorType);
        return true;
    }

    // Called from the OnAttackHit animation event on TDOperatorView
    public void ExecuteHit(Vector2Int cell, Vector3 worldPos, OperatorData data)
    {
        if (m_PendingTarget == null) return;

        Vector3 impactPos = m_PendingTarget.transform.position;
        m_PendingTarget.TakeDamage(data?.damage ?? 0f);
        TDGameEventBus.OperatorImpacted(impactPos, data?.operatorType ?? OperatorType.Ranger);
        m_PendingTarget = null;
    }
}

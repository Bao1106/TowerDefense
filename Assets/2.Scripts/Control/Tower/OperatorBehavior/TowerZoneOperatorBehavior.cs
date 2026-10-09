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
        Vector3 nearest = TDGridMainModel.api.GetNearestGridPosition(worldPos);
        Vector3 placedPos = new(nearest.x, TDConstant.CONFIG_TOWER_PLACE_Y, nearest.z);
        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPos, rotation, slotInfo);
        TDGridMainModel.api.SetOccupiedCell(nearest);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private Transform m_OperatorTransform;
    private TDOffsetRangeDTO m_RangeDTO;

    public void OnInit(Vector2Int cell, OperatorData data, TDOperatorView view)
    {
        // Into the morale roster (spikes, wave relief, Rescue, leaks) — NOT RegisterOperator,
        // which is the blocking list.
        TDOperatorRegistry.api?.RegisterOperatorView(cell, view);
        m_OperatorTransform = view.transform;
        m_RangeDTO = new TDOffsetRangeDTO(data?.rangeOffsets);
    }

    public void OnRemove(Vector2Int cell, Vector3 worldPos)
    {
        TDGridMainModel.api?.UnoccupyCell(worldPos);
        TDOperatorRegistry.api?.UnregisterOperator(cell); // no block list here, so nothing is released
        m_OperatorTransform = null;
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    private TDEnemyView m_PendingTarget;

    public bool TryAttack(Vector2Int cell, Vector3 worldPos, OperatorData data)
    {
        var nearest = FindNearestInRange(cell, worldPos);
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
        float damage = data?.damage ?? 0f;
        int radius = data?.splashRadius ?? 0;

        if (radius > 0 && TDEnemyRegistry.api != null)
        {
            // Full damage to everyone within `radius` cells (Chebyshev) of the target, the target
            // included exactly once. Copy first: a kill unregisters and edits the live list.
            var centre = TDGridMainModel.api.WorldToCell(impactPos);
            foreach (var enemy in new List<TDEnemyView>(TDEnemyRegistry.api.GetAll()))
            {
                if (enemy == null) continue;
                var d = TDGridMainModel.api.WorldToCell(enemy.transform.position) - centre;
                if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) <= radius) enemy.TakeDamage(damage);
            }
        }
        else m_PendingTarget.TakeDamage(damage);

        TDGameEventBus.OperatorImpacted(impactPos, data?.operatorType ?? OperatorType.Ranger);
        m_PendingTarget = null;
    }

    public bool IsEngaged(Vector2Int cell)
        => m_OperatorTransform != null && FindNearestInRange(cell, m_OperatorTransform.position) != null;

    public bool Covers(Vector2Int myCell, Vector2Int target)
        => m_RangeDTO != null && m_OperatorTransform != null
        && m_RangeDTO.GetCellsInRange(myCell, m_OperatorTransform.rotation).Contains(target);

    private TDEnemyView FindNearestInRange(Vector2Int cell, Vector3 worldPos)
    {
        if (m_RangeDTO == null || m_OperatorTransform == null || TDEnemyRegistry.api == null) return null;

        var validCells = new HashSet<Vector2Int>(
            m_RangeDTO.GetCellsInRange(cell, m_OperatorTransform.rotation));

        TDEnemyView nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var enemy in TDEnemyRegistry.api.GetAll())
        {
            if (enemy == null) continue;
            var enemyCell = TDGridMainModel.api.WorldToCell(enemy.transform.position);
            if (!validCells.Contains(enemyCell)) continue;

            float dist = Vector3.Distance(worldPos, enemy.transform.position);
            if (dist < nearestDist) { nearestDist = dist; nearest = enemy; }
        }

        return nearest;
    }
}

using TDEnums;
using UnityEngine;

/// <summary>
/// Operator placed on a path cell: blocks enemies and performs melee attacks.
/// Used for Knight / Defender / Striker and any custom melee variants.
///
/// Targets = enemies this operator BLOCKS, plus enemies standing in its `rangeOffsets`.
/// The second half used to be missing entirely: attacks only ever hit blocked enemies,
/// while TDDeployController and TDOperatorSelectionView both drew the range from
/// `rangeOffsets`. The game was highlighting cells it would never fire into — and for
/// Knight / Striker / Ace / Layla, whose offsets do not even include {0,0}, the drawn
/// range and the real one had no cell in common at all.
///
/// The union matters in both directions: an enemy this operator is holding must stay
/// hittable even when the data forgets to list {0,0}. That is a gameplay invariant, not
/// something to leave depending on four asset rows being right.
///
/// Exactly ONE enemy is struck per swing, whatever `blockCount` or `attackType` say. Those
/// describe how many an operator HOLDS; melee always hits one. Widening the target set
/// without that rule quadrupled Striker's DPS on unchanged tuning numbers.
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
        Vector3 nearest = TDGridMainModel.api.GetNearestGridPosition(worldPos);
        Vector3 placedPos = new(nearest.x, TDConstant.CONFIG_OPERATOR_PLACE_Y, nearest.z);
        TDTowerFactoryControl.api.CreateUnit(slotInfo.prefab, placedPos, rotation, slotInfo);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private Transform m_OperatorTransform;
    private TDOffsetRangeDTO m_RangeDTO;

    public void OnInit(Vector2Int cell, OperatorData data, TDOperatorView view)
    {
        int blockCap = Mathf.Clamp(data?.blockCount ?? 1, 1, 3);
        TDOperatorRegistry.api?.RegisterOperator(cell, blockCap);
        TDOperatorRegistry.api?.RegisterOperatorView(cell, view);

        // Rotation decides which cells the offsets land on, and it can change after
        // placement — so keep the transform, not a snapshot of the cells.
        m_OperatorTransform = view.transform;
        m_RangeDTO = new TDOffsetRangeDTO(data?.rangeOffsets);
    }

    public void OnRemove(Vector2Int cell, Vector3 worldPos)
    {
        TDOperatorRegistry.api?.UnregisterOperator(cell);
        m_OperatorTransform = null;
    }

    // ── Attack ────────────────────────────────────────────────────────────────

    public bool TryAttack(Vector2Int cell, Vector3 worldPos, OperatorData data)
    {
        if (TDOperatorRegistry.api == null) return false;

        // ONE target, always. `blockCount` is how many enemies an operator HOLDS, not how
        // many it strikes — a Defender pins three and still swings at one of them.
        //
        // Whoever it is blocking wins the tie: an operator must never turn its back on the
        // enemy it is holding to swat someone walking past.
        var blocked = TDOperatorRegistry.api.GetBlockedEnemies(cell);
        var target = blocked.Count > 0 ? blocked[0] : FindEnemyInRange(cell);
        if (target == null) return false;

        target.TakeDamage(data?.damage ?? 0f);

        TDGameEventBus.OperatorAttacked(worldPos, data?.operatorType ?? OperatorType.Knight);
        TDGameEventBus.OperatorImpacted(worldPos, data?.operatorType ?? OperatorType.Knight);
        return true;
    }

    /// <summary>
    /// Nearest enemy standing in one of the operator's range cells, or null.
    /// Nearest — not first found — so the target does not flicker between enemies from one
    /// swing to the next. Same tie-break TowerZoneOperatorBehavior already uses.
    /// </summary>
    private TDEnemyView FindEnemyInRange(Vector2Int cell)
    {
        if (m_RangeDTO == null || m_OperatorTransform == null || TDEnemyRegistry.api == null) return null;

        var cells = m_RangeDTO.GetCellsInRange(cell, m_OperatorTransform.rotation);
        if (cells.Count == 0) return null;

        Vector3 origin = m_OperatorTransform.position;
        TDEnemyView nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var enemy in TDEnemyRegistry.api.GetAll())
        {
            if (enemy == null) continue;

            var enemyCell = TDGridMainModel.api.WorldToCell(enemy.transform.position);
            bool inRange = false;
            for (int i = 0; i < cells.Count && !inRange; i++)
                inRange = cells[i] == enemyCell;
            if (!inRange) continue;

            float dist = Vector3.SqrMagnitude(enemy.transform.position - origin);
            if (dist >= nearestDist) continue;
            nearestDist = dist;
            nearest = enemy;
        }

        return nearest;
    }

    public void ExecuteHit(Vector2Int cell, Vector3 worldPos, OperatorData data) { }

    public bool IsEngaged(Vector2Int cell)
        => (TDOperatorRegistry.api != null && TDOperatorRegistry.api.GetBlockedEnemies(cell).Count > 0)
           || FindEnemyInRange(cell) != null;

    public bool Covers(Vector2Int myCell, Vector2Int target) => false;
}

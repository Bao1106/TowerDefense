using TDEnums;
using UnityEngine;

/// <summary>
/// What the world around an operator feeds into morale. The pressure-zone counter that lived
/// here (N1 / N2, spec §03) was removed by the load model (spec 2026-10-06 §5.4): load is now
/// counted as leak events, reported by TDOperatorRegistry.ReportLeak. Only the boss aura is left.
/// </summary>
public static class TDPressureProbe
{
    /// <summary>
    /// N4 — strongest fear aura reaching this cell, in points per second. Auras do not
    /// stack with themselves; the worst one in range wins, so two bosses are frightening
    /// rather than lethal-by-arithmetic.
    ///
    /// ponytail: radius and rate are hardcoded per enemy type here. §08 moves both onto
    /// EnemyData as `fearAuraRadius` / `fearAuraRate` when Herald arrives in Phase 5 — at
    /// that point this reads the fields and the type switch disappears.
    /// </summary>
    public static float AuraRateAt(Vector2Int cell)
    {
        var enemies = TDEnemyRegistry.api?.GetAll();
        if (enemies == null || enemies.Count == 0) return 0f;

        var grid = TDGridMainModel.api;
        float worst = 0f;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.EnemyType != EnemyType.Boss) continue;

            var ec = grid.WorldToCell(enemy.transform.position);
            int dist = Mathf.Max(Mathf.Abs(ec.x - cell.x), Mathf.Abs(ec.y - cell.y));
            if (dist <= 3 && TDConstant.STRESS_AURA_BOSS > worst) worst = TDConstant.STRESS_AURA_BOSS;
        }

        return worst;
    }
}

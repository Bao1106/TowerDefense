using System.Collections.Generic;
using TDEnums;
using UnityEngine;

/// <summary>
/// Measures the PRESSURE ZONE defined in §03 of the morale design: how many enemies are
/// actually inside an operator's zone, sampled once a second while a match runs.
///
/// Written for step 1.6 ("measure the three numbers still guessed at"), but `Count()` is
/// not throwaway instrumentation — it is the exact quantity stress sources N1 and N2 read
/// in Phase 2. The probe wraps it in a counter so the numbers exist BEFORE the tuning
/// constants that depend on them get written.
///
/// Zone, per §03 — the two deploy zones are deliberately different:
///   melee (PathCell)   → the 3x3 block around the operator, UNION whatever `rangeOffsets`
///                        reaches beyond it.
///   ranged (TowerZone) → enemies on one of the 8 neighbouring cells, NOT inside range.
///                        Ginger reaches 9 cells and Moon 8, so range-based counting would
///                        mark them "surrounded" permanently while they stand safe on a
///                        platform shooting down. Archers only tense when something gets close.
///
/// Tolerance, per §03: melee = 1 + blockCount, ranged = 1. N1 only charges the excess.
///
/// The melee zone was `rangeOffsets` alone until this probe measured it: N1 fired 0% of
/// 110 engaged operator-seconds, and not from bad luck. Melee offsets default to {(0,0)},
/// so the zone was the operator's own cell — where only BLOCKED enemies stand. That caps
/// the count at blockCount while tolerance is 1+blockCount, putting N1 one short of firing
/// by construction, forever. Widening to 3x3 also matches what "surrounded" means to the
/// player: the queue backing up around a Defender, not the three it is already hitting.
/// </summary>
public static class TDPressureProbe
{
    private const float SAMPLE_INTERVAL = 1f;

    // Every sample, engaged or not. Without it the report is a raw SUM over operators and
    // match length, so two runs are not comparable: a match twice as long with the same
    // pressure reads the same as a short frantic one. The ratio is the comparable number.
    private static int s_AllSamples;

    private static int s_Samples;      // samples with at least one enemy in the zone
    private static int s_TotalInZone;  // running sum, for the mean
    private static int s_Peak;
    private static int s_OverSamples;  // samples above tolerance — where N1 would charge
    private static int s_TotalOverload;

    static TDPressureProbe()
    {
        TDGameEventBus.OnVictory  += Report;
        TDGameEventBus.OnGameOver += Report;
    }

    /// <summary>Enemies inside this operator's pressure zone right now.</summary>
    public static int Count(Vector2Int cell, Quaternion rotation, OperatorData data)
    {
        var enemies = TDEnemyRegistry.api?.GetAll();
        if (enemies == null || enemies.Count == 0) return 0;

        bool ranged = data != null && data.deployZone == DeployZone.TowerZone;

        // Melee only: cells the operator reaches that lie OUTSIDE the 3x3 block. Nothing
        // in the roster has such a cell today (offsets default to {(0,0)}), but a longer
        // reach must not silently drop out of the zone the day one is added.
        List<Vector2Int> reach = ranged
            ? null
            : new TDOffsetRangeDTO(((IDeployableDTO)data)?.RangeOffsets).GetCellsInRange(cell, rotation);

        var grid = TDGridMainModel.api;
        int n = 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            var ec = grid.WorldToCell(enemy.transform.position);
            bool adjacent = Mathf.Abs(ec.x - cell.x) <= 1 && Mathf.Abs(ec.y - cell.y) <= 1;

            if (ranged)
            {
                // Its own cell is a platform — nothing walks onto it, but skip it explicitly
                // so the rule reads as "something got close", not as an accident of geometry.
                if (adjacent && ec != cell) n++;
                continue;
            }

            if (adjacent) { n++; continue; }

            for (int i = 0; i < reach.Count; i++)
                if (reach[i] == ec) { n++; break; }
        }

        return n;
    }

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

    /// <summary>Tolerance before N1 starts charging (§03).</summary>
    public static int Tolerance(OperatorData data)
        => data != null && data.deployZone == DeployZone.TowerZone ? 1 : 1 + Mathf.Max(0, data?.blockCount ?? 0);

    /// <summary>Called from TDOperatorView.Update; rate-limits itself per operator.</summary>
    public static void Sample(Vector2Int cell, Quaternion rotation, OperatorData data, ref float nextSampleTime)
    {
        if (Time.time < nextSampleTime) return;
        nextSampleTime = Time.time + SAMPLE_INTERVAL;

        s_AllSamples++;

        int n = Count(cell, rotation, data);
        if (n == 0) return; // idle guard duty is free — averaging it in would hide the real load

        s_Samples++;
        s_TotalInZone += n;
        if (n > s_Peak) s_Peak = n;

        int over = n - Tolerance(data);
        if (over <= 0) return;
        s_OverSamples++;
        s_TotalOverload += over;
    }

    private static void Report()
    {
        if (s_Samples == 0)
        {
            Debug.Log("<color=cyan>[Pressure] no operator ever had an enemy in its zone</color>");
            return;
        }

        float mean = (float)s_TotalInZone / s_Samples;
        float overPct = 100f * s_OverSamples / s_Samples;
        float meanOverload = s_OverSamples > 0 ? (float)s_TotalOverload / s_OverSamples : 0f;

        float engagedPct = 100f * s_Samples / Mathf.Max(1, s_AllSamples);

        Debug.Log($"<color=cyan>[Pressure] engaged {s_Samples}/{s_AllSamples} operator-seconds " +
                  $"({engagedPct:F0}% of deployed time) — " +
                  $"mean {mean:F2} enemies in zone, peak {s_Peak}. " +
                  $"Over tolerance {overPct:F0}% of the time, by {meanOverload:F2} on average " +
                  $"— that excess is what N1 charges for.</color>");

        s_AllSamples = s_Samples = s_TotalInZone = s_Peak = s_OverSamples = s_TotalOverload = 0;
    }
}

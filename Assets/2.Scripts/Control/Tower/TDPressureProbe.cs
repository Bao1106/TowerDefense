using System.Collections.Generic;
using System.Text;
using TDEnums;
using UnityEngine;

/// <summary>
/// What the world around an operator feeds into morale, and the recorder the calibration runs
/// read (spec 2026-10-06 §7.2). The pressure-zone counter that lived here (N1 / N2, spec §03)
/// was removed by the load model (§5.4): load is counted as leak events, reported by
/// TDOperatorRegistry.ReportLeak. Of the old probe only the boss aura is left.
///
/// The recorder keeps one row per wave (units on the field, cap, gold earned, leaks, collapses)
/// and one entry per stint — an operator's stay on the field, from deploy to retreat, death or
/// the end of the match — and reports r = Pearson(HP lost/s, stress gained/s) over the MELEE
/// stints: the check that HP and stress are two pressures, not one (§4.3 rule 4, target r &lt; 0.5).
/// One match gives 3–5 melee stints, too few for r, so stints also pool across matches until
/// ResetPool and the report gives both.
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

    // ── Recorder ──────────────────────────────────────────────────────────────

    private const float MIN_STINT_SECONDS = 5f; // shorter stays are noise in a per-second rate

    // Spec §7.3 A4 — "pulled out near full HP because stress was red". The spec gives no number
    // for "near full"; 80% is the plan's call (2026-10-09), and red is the Stressed band.
    public const float A4_MIN_HP_FRAC = 0.8f;

    private struct WaveRow { public int wave, onField, limit, goldEarned, leaks, leakedEnemies, leakedHorde, collapses; }

    /// <summary>One stay on the field. `band` is the band the operator LEFT in — for a retreat,
    /// read before the retreat's own −70 relief, or every red retreat would report Calm.</summary>
    public struct Stint
    {
        public string op; public bool melee; public float seconds, hpLost, stressGained;
        public bool retreated; public float hpFrac; public MoraleState band;
    }

    private static readonly List<WaveRow> s_Waves = new();
    private static readonly List<Stint> s_Stints = new();
    private static readonly List<Stint> s_Pool = new(); // every match since ResetPool
    private static int s_Wave = -1, s_Leaks, s_LeakedEnemies, s_LeakedHorde, s_Collapses, s_TotalCollapses, s_PoolMatches;
    private static string s_Ending, s_Report;

    // Subscribed once per play session: the bus is static and outlives scenes, and with domain
    // reload off a second subscription would double every row. -= first makes it idempotent.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        TDGameEventBus.OnWaveStarted -= OnWaveStarted;
        TDGameEventBus.OnWaveStarted += OnWaveStarted;
        TDGameEventBus.OnVictory -= OnVictory;
        TDGameEventBus.OnVictory += OnVictory;
        TDGameEventBus.OnGameOver -= OnGameOver;
        TDGameEventBus.OnGameOver += OnGameOver;
    }

    // Leak EVENTS (every full blocker passed — what morale feels) vs leaked ENEMIES (each counted
    // once — what calibration measures: one enemy past a line of four is one failure, not four).
    // Horde bodies get their own column: a pack overflows the blockers by design, so calibration
    // counts leaked minus Horde (round 2).
    public static void RecordLeak() => s_Leaks++;
    public static void RecordLeakedEnemy(EnemyType type)
    {
        s_LeakedEnemies++;
        if (type == EnemyType.Horde) s_LeakedHorde++;
    }

    public static void RecordCollapse()
    {
        s_Collapses++;
        s_TotalCollapses++;
    }

    /// <summary>One stay on the field has ended (retreat, death, or the match). `stressGained`
    /// sums the POSITIVE frame-to-frame rises — relief must not cancel the pressure it follows.</summary>
    public static void RecordStint(string op, bool melee, float seconds, float hpLost, float stressGained,
                                   bool retreated, float hpFrac, MoraleState band)
    {
        var s = new Stint
        {
            op = op, melee = melee, seconds = seconds, hpLost = hpLost, stressGained = stressGained,
            retreated = retreated, hpFrac = hpFrac, band = band,
        };
        s_Stints.Add(s);
        s_Pool.Add(s);
    }

    /// <summary>§7.3 A4: retreats made near full HP while Stressed — stress, not HP, made the call.</summary>
    public static int CountA4(IReadOnlyList<Stint> stints)
    {
        int n = 0;
        foreach (var s in stints)
            if (s.retreated && s.hpFrac >= A4_MIN_HP_FRAC && s.band == MoraleState.Stressed) n++;
        return n;
    }

    public static void ResetPool()
    {
        s_Pool.Clear();
        s_PoolMatches = 0;
    }

    /// <summary>
    /// r over the melee stints of at least MIN_STINT_SECONDS. Ranged operators never lose HP, so
    /// counting them made r measure front line vs back line instead of HP vs stress.
    /// </summary>
    public static float MeleeR(IReadOnlyList<Stint> stints) => MeleeR(stints, out _);

    private static float MeleeR(IReadOnlyList<Stint> stints, out int n)
    {
        var hpRate = new List<float>();
        var stressRate = new List<float>();
        foreach (var s in stints)
        {
            if (!s.melee || s.seconds < MIN_STINT_SECONDS) continue;
            hpRate.Add(s.hpLost / s.seconds);
            stressRate.Add(s.stressGained / s.seconds);
        }
        n = hpRate.Count;
        return Pearson(hpRate, stressRate);
    }

    /// <summary>
    /// The match so far. After Victory or GameOver it is built once, on the first read — not in
    /// the end-of-match handler, because the order of those handlers is not defined and the
    /// operators still standing must get to record their stints first.
    /// </summary>
    public static string LastReport => s_Ending != null ? s_Report ??= Build() : Build();

    private static void OnWaveStarted(int waveIdx)
    {
        if (waveIdx == 0)
        {
            s_Waves.Clear();
            s_Stints.Clear();
            s_TotalCollapses = 0;
            s_Ending = s_Report = null;
        }
        else CloseWave();

        s_Wave = waveIdx;
        s_Leaks = s_LeakedEnemies = s_LeakedHorde = s_Collapses = 0;
    }

    private static void OnVictory() => End("Victory");
    private static void OnGameOver() => End("GameOver");

    private static void End(string how)
    {
        if (s_Ending != null) return;
        CloseWave();
        s_Ending = how;
        s_PoolMatches++;

#if UNITY_EDITOR
        // So a human playtest gets the numbers too, not only the bot (§7.3). Printed on the next
        // editor tick, not here: this handler is subscribed at AfterSceneLoad, so it runs BEFORE
        // the CloseStint of every operator still standing, and their stints would be missing.
        UnityEditor.EditorApplication.delayCall += () => Debug.Log(LastReport);
#endif
    }

    private static void CloseWave()
    {
        if (s_Wave < 0) return;
        s_Waves.Add(new WaveRow
        {
            wave = s_Wave + 1,
            onField = TDDeployCap.api?.OnField ?? 0,
            limit = TDDeployCap.api?.Limit ?? 0,
            goldEarned = TDGoldControl.api?.TotalEarned ?? 0,
            leaks = s_Leaks,
            leakedEnemies = s_LeakedEnemies,
            leakedHorde = s_LeakedHorde,
            collapses = s_Collapses,
        });
        s_Wave = -1;
    }

    private static string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pressure report — {s_Ending ?? "in progress"}");
        sb.AppendLine("wave  onField  limit  goldEarned  leaks  leaked  horde  collapses");
        foreach (var w in s_Waves)
            sb.AppendLine($"{w.wave,4}  {w.onField,7}  {w.limit,5}  {w.goldEarned,10}  {w.leaks,5}  {w.leakedEnemies,6}  {w.leakedHorde,5}  {w.collapses,9}");
        if (s_Wave >= 0) sb.AppendLine($"{s_Wave + 1,4}  (open)  leaks {s_Leaks}, leaked {s_LeakedEnemies} ({s_LeakedHorde} Horde), collapses {s_Collapses}");

        float r = MeleeR(s_Stints, out int n);
        float pooled = MeleeR(s_Pool, out int pooledN);
        sb.AppendLine($"stints: {s_Stints.Count}");
        foreach (var s in s_Stints)
            sb.AppendLine($"  {s.op,-9} {(s.melee ? "melee " : "ranged")} {s.seconds,6:F1}s  hp −{s.hpLost,6:F0}  stress +{s.stressGained,6:F1}" +
                          $"  left {(s.retreated ? "retreat  " : "death/end")} hp {s.hpFrac,4:P0} {s.band}");
        sb.AppendLine($"A4 retreats (HP ≥ {A4_MIN_HP_FRAC:P0}, Stressed): {CountA4(s_Stints)}");
        sb.AppendLine($"r melee (HP lost/s vs stress gained/s) = {Format(r)} over {n} stints of ≥ {MIN_STINT_SECONDS:F0}s");
        sb.AppendLine($"r melee pooled over {s_PoolMatches} matches = {Format(pooled)} over {pooledN} stints");
        sb.AppendLine($"collapses: {s_TotalCollapses}");
        return sb.ToString();
    }

    private static string Format(float r) => float.IsNaN(r) ? "n/a" : r.ToString("F2");

    /// <summary>Pearson correlation; NaN when n &lt; 2 or either series has no variance.</summary>
    public static float Pearson(IReadOnlyList<float> xs, IReadOnlyList<float> ys)
    {
        int n = Mathf.Min(xs.Count, ys.Count);
        if (n < 2) return float.NaN;

        double mx = 0, my = 0;
        for (int i = 0; i < n; i++) { mx += xs[i]; my += ys[i]; }
        mx /= n;
        my /= n;

        double sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = xs[i] - mx, dy = ys[i] - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }
        if (sxx <= 0 || syy <= 0) return float.NaN;
        return (float)(sxy / System.Math.Sqrt(sxx * syy));
    }
}

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
/// the end of the match — and reports r = Pearson(HP lost/s, stress gained/s) over the stints:
/// the check that HP and stress are two pressures, not one (§4.3 rule 4, target r &lt; 0.5).
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

    private struct WaveRow { public int wave, onField, limit, goldEarned, leaks, collapses; }
    private struct Stint { public string op; public float seconds, hpLost, stressGained; }

    private static readonly List<WaveRow> s_Waves = new();
    private static readonly List<Stint> s_Stints = new();
    private static int s_Wave = -1, s_Leaks, s_Collapses, s_TotalCollapses;
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

    public static void RecordLeak() => s_Leaks++;

    public static void RecordCollapse()
    {
        s_Collapses++;
        s_TotalCollapses++;
    }

    /// <summary>One stay on the field has ended (retreat, death, or the match). `stressGained`
    /// sums the POSITIVE frame-to-frame rises — relief must not cancel the pressure it follows.</summary>
    public static void RecordStint(string op, float seconds, float hpLost, float stressGained)
        => s_Stints.Add(new Stint { op = op, seconds = seconds, hpLost = hpLost, stressGained = stressGained });

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
        s_Leaks = s_Collapses = 0;
    }

    private static void OnVictory() => End("Victory");
    private static void OnGameOver() => End("GameOver");

    private static void End(string how)
    {
        if (s_Ending != null) return;
        CloseWave();
        s_Ending = how;
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
            collapses = s_Collapses,
        });
        s_Wave = -1;
    }

    private static string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Pressure report — {s_Ending ?? "in progress"}");
        sb.AppendLine("wave  onField  limit  goldEarned  leaks  collapses");
        foreach (var w in s_Waves)
            sb.AppendLine($"{w.wave,4}  {w.onField,7}  {w.limit,5}  {w.goldEarned,10}  {w.leaks,5}  {w.collapses,9}");
        if (s_Wave >= 0) sb.AppendLine($"{s_Wave + 1,4}  (open)  leaks {s_Leaks}, collapses {s_Collapses}");

        var hpRate = new List<float>();
        var stressRate = new List<float>();
        foreach (var s in s_Stints)
        {
            if (s.seconds < MIN_STINT_SECONDS) continue;
            hpRate.Add(s.hpLost / s.seconds);
            stressRate.Add(s.stressGained / s.seconds);
        }
        float r = Pearson(hpRate, stressRate);
        sb.AppendLine($"stints: {s_Stints.Count} ({hpRate.Count} of ≥ {MIN_STINT_SECONDS:F0}s)");
        foreach (var s in s_Stints)
            sb.AppendLine($"  {s.op,-9} {s.seconds,6:F1}s  hp −{s.hpLost,6:F0}  stress +{s.stressGained,6:F1}");
        sb.AppendLine($"r (HP lost/s vs stress gained/s) = {(float.IsNaN(r) ? "n/a" : r.ToString("F2"))}");
        sb.AppendLine($"collapses: {s_TotalCollapses}");
        return sb.ToString();
    }

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

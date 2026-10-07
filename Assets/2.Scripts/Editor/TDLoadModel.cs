using System.Collections.Generic;
using TDEnums;
using UnityEngine;

/// <summary>
/// Load factor ρ per wave (spec 2026-10-06 §4.2):
///
///     ρ = wave HP / (team DPS × T + team block × H)
///
/// ρ &lt; 1: the team keeps up on average; ρ &gt; 1: enemies leak. "Team" is the reference team
/// at the size on the field that wave — the first n names of ReferenceTeam, the same order the
/// calibration bot buys in. The model is deliberately crude (single-target DPS, no travel time)
/// and EfficiencyOf(d) scales it to what bot runs actually leak.
/// </summary>
public static class TDLoadModel
{
    public static readonly string[] ReferenceTeam = { "Knight", "Ginger", "Striker", "Defender", "Moon", "Ace" };

    public struct WaveLoad { public int wave; public bool isBoss; public int onField; public float hp; public int bodies; public float seconds; public float rho; }

    // NaN = not calibrated yet → the validator skips its target assertions. Set per difficulty
    // from bot runs: the uncalibrated ρ of the first regular wave that leaks twice.
    public static float EfficiencyOf(Difficulty d) => float.NaN;

    /// <summary>
    /// One row per wave of `level` played at difficulty `d` — `d`, not level.difficulty, so the
    /// same level can be tabled at all three.
    ///
    /// T is the time the spawner spends releasing the wave — bodies × that wave's own spacing
    /// (SpawnIntervalFor, the function the wave loop uses; D14) — not the pause after it: the
    /// pause is the same for every wave, so it would only flatten the shape.
    /// </summary>
    public static List<WaveLoad> Compute(LevelConfig level, Difficulty d,
        IReadOnlyList<OperatorData> roster, IReadOnlyList<EnemyData> enemies)
    {
        var row = DifficultyRatioTable.Get(d);
        int limit = Mathf.Max(1, level.deployLimit + row.deployLimitDelta);
        float efficiency = EfficiencyOf(d);
        var plan = TDEnemyPathMainControl.BuildWavePlans(d, level.waveCount, level.totalEnemies, level.waveGrowth);

        var rows = new List<WaveLoad>(plan.Count);
        for (int i = 0; i < plan.Count; i++)
        {
            int wave = i + 1;
            int onField = Mathf.Min(limit, wave + 1); // roughly one purchase per wave

            float dps = 0f;
            int block = 0;
            for (int k = 0; k < onField && k < ReferenceTeam.Length; k++)
            {
                var op = Find(roster, ReferenceTeam[k]);
                if (op == null) continue; // RHO_TEAM_RESOLVES reports it
                dps += op.damage * op.attackSpeed;
                block += op.blockCount;
            }

            float hp = 0f;
            foreach (var type in plan[i])
                hp += (Find(enemies, type)?.baseHP ?? 0f) * row.hpMult;

            int bodies = plan[i].Count;
            float seconds = bodies * TDEnemyPathMainControl.SpawnIntervalFor(level.spawnInterval, i, plan.Count, level.waveGrowth);
            float h = bodies > 0 ? hp / bodies : 0f;
            float capacity = dps * seconds + block * h;
            float rho = capacity > 0f ? hp / capacity : hp > 0f ? float.PositiveInfinity : 0f;
            if (!float.IsNaN(efficiency)) rho /= efficiency;

            rows.Add(new WaveLoad
            {
                wave = wave, isBoss = plan[i].Contains(EnemyType.Boss), onField = onField,
                hp = hp, bodies = bodies, seconds = seconds, rho = rho,
            });
        }
        return rows;
    }

    private static OperatorData Find(IReadOnlyList<OperatorData> roster, string name)
    {
        for (int i = 0; i < roster.Count; i++)
            if (roster[i].operatorName == name) return roster[i];
        return null;
    }

    private static EnemyData Find(IReadOnlyList<EnemyData> enemies, EnemyType type)
    {
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i].type == type) return enemies[i];
        return null;
    }
}

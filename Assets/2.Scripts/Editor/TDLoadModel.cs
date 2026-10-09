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
    // from bot runs: the uncalibrated ρ of the first regular wave in which two ENEMIES leaked,
    // averaged over three DEMO-1 runs (docs/superpowers/measurements/2026-10-06-load-model-round-1.md),
    // divided by BotStrength.
    public static float EfficiencyOf(Difficulty d) => d switch
    {
        Difficulty.Normal => 0.51f / BotStrength,
        Difficulty.Hard => 0.58f / BotStrength,
        Difficulty.Nightmare => 0.59f / BotStrength,
        _ => float.NaN,
    };

    // The bot holds one fixed line and never retreats; the ρ targets describe a player. Taken
    // as ~0.6 of one (user ruling, round 1) — 0.58 is the one value near it at which all three
    // difficulties meet their targets. A human playtest at Hard is what confirms or moves it.
    public const float BotStrength = 0.58f;

    /// <summary>
    /// One row per wave of `level` played at difficulty `d` — `d`, not level.difficulty, so the
    /// same level can be tabled at all three.
    ///
    /// T is the time the spawner spends releasing the wave: its busiest gate's share, at that
    /// wave's own spacing (SpawnIntervalFor; D14), a Horde pack in one quick burst (SliceSeconds)
    /// — the functions the wave loop itself uses. Two gates releasing at once halve it — leaving
    /// that out made the first calibration read DEMO-1's second gate as a 2× "inefficiency". Not the pause after the wave: the same for every wave, it would
    /// only flatten the shape.
    ///
    /// `gates` comes in from the caller (GatesFor) so this stays pure arithmetic: looking it up in
    /// here once scanned the asset database on every call, and a tuning loop calling Compute
    /// thousands of times inside one editor tick ran the editor out of native memory.
    /// </summary>
    public static List<WaveLoad> Compute(LevelConfig level, Difficulty d,
        IReadOnlyList<OperatorData> roster, IReadOnlyList<EnemyData> enemies, int gates)
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
            float spacing = TDEnemyPathMainControl.SpawnIntervalFor(level.spawnInterval, i, plan.Count, level.waveGrowth);
            // The whole wave's release time, cut to the busiest gate's share of the bodies. Not
            // SliceWave on this plan: its order is a shuffle, so which gate a pack lands on — and
            // with it ρ — would change from one call to the next.
            float seconds = TDEnemyPathMainControl.SliceSeconds(plan[i], spacing) * LongestSlice(bodies, gates) / Mathf.Max(1, bodies);
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

    /// <summary>
    /// Gates that release a wave at the same time on this level's stage: a Simultaneous stage
    /// splits every wave across all its gates, any other mode sends a wave through one. The level
    /// config does not know its map, so this reads the stages the game plays — TDStageRepository's
    /// list, passed in by the caller, not every TDStageConfig asset lying in the project.
    /// </summary>
    public static int GatesFor(int levelIndex, IReadOnlyList<TDStageConfig> stages)
    {
        if (stages == null) return 1;
        foreach (var stage in stages)
        {
            if (stage == null || stage.LevelIndex != levelIndex) continue;
            return stage.GateMode == GateAssignmentMode.Simultaneous
                ? Mathf.Max(1, Mathf.Max(stage.StartGateCount, stage.EndGateCount)) : 1;
        }
        return 1;
    }

    // The wave loop's split (SliceWave, before it moves cuts past packs): every gate gets
    // n / gates, the last takes the rest.
    private static int LongestSlice(int bodies, int gates)
    {
        if (gates <= 1) return bodies;
        int perGroup = Mathf.Max(1, bodies / gates);
        return Mathf.Max(perGroup, bodies - (gates - 1) * perGroup);
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

using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

[Serializable]
public class LevelConfig
{
    public int levelIndex;
    public Difficulty difficulty;
    public int totalEnemies;
    public int waveCount;
    public float waveInterval; // seconds between waves
    public float spawnInterval; // seconds between enemies in same wave
    public int deployLimit = 5; // units on the field at once — operators and turrets alike (spec §5.1)
    public float waveGrowth = 1f; // last regular wave ÷ first, interpolated linearly; 1 = every wave the same (spec §5.7)
}

[CreateAssetMenu(menuName = "Game Configs/Level Config", fileName = "Level Config", order = 3)]
public class TDLevelConfigSettings : ScriptableObject
{
    private static TDLevelConfigSettings m_api;
    public static TDLevelConfigSettings api
        => m_api ??= TDResourceObject.GetResource<TDLevelConfigSettings>(TDConstant.CONFIG_LEVEL);
    [SerializeField] private List<LevelConfig> levels;

    public List<LevelConfig> GetAllLevels() => levels ?? new List<LevelConfig>();

    public LevelConfig GetLevel(int levelIndex)
    {
        var cfg = levels.Find(l => l.levelIndex == levelIndex);
        if (cfg == null)
            Debug.LogError($"[TDLevelConfigSettings] No config for levelIndex={levelIndex}");
        return cfg;
    }
}

// ── Difficulty Ratio Table ─────────────────────────────────────────────────────
// Spec §5.7: the LEVEL owns rhythm and capacity (waves, total, intervals, deployLimit,
// waveGrowth); the DIFFICULTY owns load — the mix of enemy types, hp/speed, the boss plan —
// and nudges the level's deploy cap and starting gold.
// Distribute(diff, total, progress) → [Normal, Fast, Tank, Horde, Herald]. Bosses are not a
// share: they come from bossWaveCount × bossPerWave, the one place that decides how many.

public static class DifficultyRatioTable
{
    public struct RatioRow
    {
        public float normalPct, fastPct, tankPct, hordePct, heraldPct; // the match average
        public float mixRamp; // D14: heavy shares run from (1 − r)× at the first wave to (1 + r)× at the last
        public float hpMult, speedMult;
        public int bossWaveCount, bossPerWave;
        public float bossWaveMult;
        public int deployLimitDelta, startingGold;
    }

    // mixRamp is capped by Normal going negative late: 1 − fast − heavy × (1 + r) ≥ 0, so
    // Nightmare's 55% heavy (Tank + Horde + Herald) leaves room for 0.45 at most.
    // mixRamp and bossWaveMult are calibrated against bot runs (round 1): boss waves at 2–2.5×
    // leaked 13–19 enemies, and no setting above ~1.4 meets the ρ targets. Round 2 (Horde and
    // Herald in) lowered hpMult and mixRamp: the load model cannot see a pack overflowing the
    // blockers or a Herald's ×2, so the extra pressure is paid for here, checked by bot outcomes.
    private static readonly Dictionary<Difficulty, RatioRow> k_Table =
        new Dictionary<Difficulty, RatioRow>
        {
            { Difficulty.Normal, new RatioRow { normalPct=0.55f, fastPct=0.20f, tankPct=0.10f, hordePct=0.10f, heraldPct=0.05f, mixRamp=1.0f, hpMult=0.85f, speedMult=1.0f,
                                                bossWaveCount=1, bossPerWave=1, bossWaveMult=1.15f, deployLimitDelta=+1, startingGold=40 } },
            { Difficulty.Hard, new RatioRow { normalPct=0.40f, fastPct=0.22f, tankPct=0.15f, hordePct=0.15f, heraldPct=0.08f, mixRamp=0.3f, hpMult=0.9f, speedMult=1.1f,
                                              bossWaveCount=2, bossPerWave=1, bossWaveMult=1.0f, deployLimitDelta=0, startingGold=30 } },
            { Difficulty.Nightmare, new RatioRow { normalPct=0.25f, fastPct=0.20f, tankPct=0.22f, hordePct=0.20f, heraldPct=0.13f, mixRamp=0.0f, hpMult=1.05f, speedMult=1.25f,
                                                   bossWaveCount=3, bossPerWave=1, bossWaveMult=1.15f, deployLimitDelta=-1, startingGold=30 } },
        };

    public static RatioRow Get(Difficulty d) => k_Table[d];

    // The mix at `progress` through the match (0 = first wave, 1 = last), as
    // [Normal, Fast, Tank, Horde, Herald]. Spec §5.7 / D14 — the Arknights shape: open on light
    // enemies, bring the heavy ones in as the match goes on. The ramp is linear and symmetric
    // about mid-match, so the per-wave average is exactly the table; Fast stays flat (its job
    // is stress, not escalation) and Normal takes what is left.
    public static float[] Shares(Difficulty d, float progress)
    {
        RatioRow row = k_Table[d];
        float k = 1f - row.mixRamp + 2f * row.mixRamp * Mathf.Clamp01(progress);
        float tank = row.tankPct * k, horde = row.hordePct * k, herald = row.heraldPct * k;
        return new[] { 1f - row.fastPct - tank - horde - herald, row.fastPct, tank, horde, herald };
    }

    // Returns [Normal, Fast, Tank, Horde, Herald] — guaranteed sum = total.
    public static int[] Distribute(Difficulty d, int total, float progress)
        => Apportion(Shares(d, progress), total);

    // Splits `total` in proportion to `weights` (any scale) — largest-remainder (Hamilton).
    //
    // Replaced "round three types, give the leftover to Boss". That trick broke two ways,
    // both found by actually running TDBalanceValidator:
    //
    //   [SUM] Normal total=6 → [4,2,1,0] sums to 7. Rounding 3.6/1.5/0.72 up gave 7 slots
    //         out of 6, so Boss came out at -1 and Mathf.Max(0, …) quietly turned the
    //         shortfall into an extra enemy. The comment claimed "sum always equals total".
    //   [NO_BOSS] Normal total=38 → Boss wants 1.14 and gets 0, because the other three
    //         had already absorbed the rounding in their favour.
    //
    // Hamilton has no leftover type at all: every count is floor(ideal), then the seats
    // still unfilled go to the largest fractions. Sum is total because seats are handed out
    // one by one; nothing is negative because floor of a non-negative is non-negative; every
    // count is within 1 of its ideal because it is floor or floor+1. The wave sizer uses the
    // same routine, so enemy mix and wave sizes round the same way.
    public static int[] Apportion(float[] weights, int total)
    {
        int n = weights.Length;
        var counts = new int[n];
        float sum = 0f;
        for (int i = 0; i < n; i++) sum += Mathf.Max(0f, weights[i]);
        if (n == 0 || total <= 0) return counts;
        if (sum <= 0f) { counts[0] = total; return counts; }

        var frac = new float[n];
        int assigned = 0;
        for (int i = 0; i < n; i++)
        {
            float ideal = Mathf.Max(0f, weights[i]) / sum * total;
            counts[i] = Mathf.FloorToInt(ideal);
            frac[i] = ideal - counts[i];
            assigned += counts[i];
        }

        // Hand out the remaining seats to the biggest fractions, one each.
        while (assigned < total)
        {
            int best = 0;
            for (int i = 1; i < n; i++)
                if (frac[i] > frac[best]) best = i;
            counts[best]++;
            frac[best] = -1f;
            assigned++;
        }
        return counts;
    }
}

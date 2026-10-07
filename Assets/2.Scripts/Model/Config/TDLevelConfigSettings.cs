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
// Distribute(diff, total) → [Normal, Fast, Tank, Horde, Herald]. Bosses are not a share: they
// come from bossWaveCount × bossPerWave, the one place that decides how many there are.

public static class DifficultyRatioTable
{
    public struct RatioRow
    {
        public float normalPct, fastPct, tankPct, hordePct, heraldPct;
        public float hpMult, speedMult;
        public int bossWaveCount, bossPerWave;
        public float bossWaveMult;
        public int deployLimitDelta, startingGold;
    }

    // Horde and Herald do not exist yet, so the spec's Normal/Fast/Tank shares are renormalised
    // over three types; Tasks 11 and 12 set the final five-way split.
    private static readonly Dictionary<Difficulty, RatioRow> k_Table =
        new Dictionary<Difficulty, RatioRow>
        {
            { Difficulty.Normal, new RatioRow { normalPct=0.647f, fastPct=0.235f, tankPct=0.118f, hpMult=1.0f, speedMult=1.0f,
                                                bossWaveCount=1, bossPerWave=1, bossWaveMult=2.0f, deployLimitDelta=+1, startingGold=40 } },
            { Difficulty.Hard, new RatioRow { normalPct=0.519f, fastPct=0.286f, tankPct=0.195f, hpMult=1.2f, speedMult=1.1f,
                                              bossWaveCount=2, bossPerWave=1, bossWaveMult=2.5f, deployLimitDelta=0, startingGold=30 } },
            { Difficulty.Nightmare, new RatioRow { normalPct=0.373f, fastPct=0.299f, tankPct=0.328f, hpMult=1.5f, speedMult=1.25f,
                                                   bossWaveCount=3, bossPerWave=1, bossWaveMult=2.5f, deployLimitDelta=-1, startingGold=30 } },
        };

    public static RatioRow Get(Difficulty d) => k_Table[d];

    // Returns [Normal, Fast, Tank, Horde, Herald] — guaranteed sum = total.
    public static int[] Distribute(Difficulty d, int total)
    {
        RatioRow row = k_Table[d];
        return Apportion(new[] { row.normalPct, row.fastPct, row.tankPct, row.hordePct, row.heraldPct }, total);
    }

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

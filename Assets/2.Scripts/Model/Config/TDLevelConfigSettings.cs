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
// Defines the percentage distribution of each enemy type and HP/Speed multipliers per difficulty level.
// Distribute(diff, total) → [normalCount, fastCount, tankCount, bossCount]

public static class DifficultyRatioTable
{
    public struct RatioRow
    {
        public float normalPct, fastPct, tankPct, bossPct;
        public float hpMult, speedMult;
    }

    private static readonly Dictionary<Difficulty, RatioRow> k_Table =
        new Dictionary<Difficulty, RatioRow>
        {
            { Difficulty.Normal, new RatioRow { normalPct=0.60f, fastPct=0.25f, tankPct=0.12f, bossPct=0.03f, hpMult=1.0f, speedMult=1.0f } },
            { Difficulty.Hard, new RatioRow { normalPct=0.45f, fastPct=0.30f, tankPct=0.18f, bossPct=0.07f, hpMult=1.2f, speedMult=1.1f } },
            { Difficulty.Nightmare, new RatioRow { normalPct=0.15f, fastPct=0.25f, tankPct=0.35f, bossPct=0.25f, hpMult=2.0f, speedMult=1.5f } },
        };

    public static RatioRow Get(Difficulty d) => k_Table[d];

    // Returns [normalCount, fastCount, tankCount, bossCount] — guaranteed sum = total
    // Largest-remainder (Hamilton) apportionment.
    //
    // Replaces "round three types, give the leftover to Boss". That trick broke two ways,
    // both found by actually running TDBalanceValidator:
    //
    //   [SUM] Normal total=6 → [4,2,1,0] sums to 7. Rounding 3.6/1.5/0.72 up gave 7 slots
    //         out of 6, so Boss came out at -1 and Mathf.Max(0, …) quietly turned the
    //         shortfall into an extra enemy. The comment claimed "sum always equals total".
    //   [NO_BOSS] Normal total=38 → Boss wants 1.14 and gets 0, because the other three
    //         had already absorbed the rounding in their favour.
    //
    // Hamilton has no leftover type at all: every count is floor(ideal), then the seats
    // still unfilled go to the largest fractions. That makes all four assertions hold by
    // construction rather than by luck — sum is total because seats are handed out one by
    // one; nothing is negative because floor of a non-negative is non-negative; every
    // count is within 1 of its ideal because it is floor or floor+1; and any type whose
    // ideal is >= 1 gets at least floor(ideal) >= 1, so a stage that calls for a boss gets one.
    public static int[] Distribute(Difficulty d, int total)
    {
        RatioRow row = k_Table[d];
        float[] pcts = { row.normalPct, row.fastPct, row.tankPct, row.bossPct };

        var counts = new int[4];
        var frac = new float[4];
        int assigned = 0;

        for (int i = 0; i < 4; i++)
        {
            float ideal = Mathf.Max(0f, pcts[i]) * total;
            counts[i] = Mathf.FloorToInt(ideal);
            frac[i] = ideal - counts[i];
            assigned += counts[i];
        }

        // Hand out the remaining seats to the biggest fractions, one each.
        while (assigned < total)
        {
            int best = -1;
            for (int i = 0; i < 4; i++)
                if (frac[i] > 0f && (best < 0 || frac[i] > frac[best])) best = i;

            if (best < 0) best = 0; // percentages did not sum to 1 — RATIO reports that
            counts[best]++;
            frac[best] = 0f;
            assigned++;
        }

        // Only reachable if the percentages sum above 1; take back from the smallest share.
        while (assigned > total)
        {
            int worst = -1;
            for (int i = 0; i < 4; i++)
                if (counts[i] > 0 && (worst < 0 || counts[i] < counts[worst])) worst = i;

            if (worst < 0) break;
            counts[worst]--;
            assigned--;
        }

        return counts;
    }
}

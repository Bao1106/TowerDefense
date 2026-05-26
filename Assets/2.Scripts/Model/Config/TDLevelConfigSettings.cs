using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

[Serializable]
public class LevelConfig
{
    public int   levelIndex;
    public Difficulty difficulty;
    public int   totalEnemies;
    public int   waveCount;
    public float waveInterval;   // seconds between waves
    public float spawnInterval;  // seconds between enemies in same wave
}

[CreateAssetMenu(menuName = "Game Configs/Level Config", fileName = "Level Config", order = 3)]
public class TDLevelConfigSettings : ScriptableObject
{
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
// Định nghĩa % phân phối enemy type + HP/Speed multiplier theo độ khó
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
            { Difficulty.Easy,      new RatioRow { normalPct=0.80f, fastPct=0.15f, tankPct=0.04f, bossPct=0.01f, hpMult=0.8f, speedMult=0.9f } },
            { Difficulty.Normal,    new RatioRow { normalPct=0.60f, fastPct=0.25f, tankPct=0.12f, bossPct=0.03f, hpMult=1.0f, speedMult=1.0f } },
            { Difficulty.Hard,      new RatioRow { normalPct=0.45f, fastPct=0.30f, tankPct=0.18f, bossPct=0.07f, hpMult=1.2f, speedMult=1.1f } },
            { Difficulty.Extreme,   new RatioRow { normalPct=0.30f, fastPct=0.30f, tankPct=0.25f, bossPct=0.15f, hpMult=1.5f, speedMult=1.2f } },
            { Difficulty.Nightmare, new RatioRow { normalPct=0.15f, fastPct=0.25f, tankPct=0.35f, bossPct=0.25f, hpMult=2.0f, speedMult=1.5f } },
        };

    public static RatioRow Get(Difficulty d) => k_Table[d];

    // Returns [normalCount, fastCount, tankCount, bossCount] — guaranteed sum = total
    public static int[] Distribute(Difficulty d, int total)
    {
        RatioRow row    = k_Table[d];
        int      normal = Mathf.RoundToInt(row.normalPct * total);
        int      fast   = Mathf.RoundToInt(row.fastPct   * total);
        int      tank   = Mathf.RoundToInt(row.tankPct   * total);
        int      boss   = total - normal - fast - tank;  // remainder → sum luôn = total
        return new[] { Mathf.Max(0, normal), Mathf.Max(0, fast), Mathf.Max(0, tank), Mathf.Max(0, boss) };
    }
}

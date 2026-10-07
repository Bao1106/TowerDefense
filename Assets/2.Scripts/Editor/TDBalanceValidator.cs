using System.Collections.Generic;
using System.Linq;
using TDEnums;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assertions over the pure balance math that has no scene dependency.
/// Run from  Tools ▸ TD ▸ Validate Balance Tables.
///
/// Why this is an Editor validator and not an NUnit test suite:
/// the Unity Test Framework requires the code under test to live in an .asmdef, and an
/// .asmdef assembly cannot reference the predefined Assembly-CSharp. Until the code is
/// split into TD.Model / TD.Control / TD.View assemblies, real tests cannot see
/// DifficultyRatioTable at all. This file covers the same assertions in the meantime.
///
/// ponytail: port these to [Test] methods once the asmdef split lands; the assertion
/// bodies transfer as-is, only the harness changes.
/// </summary>
public static class TDBalanceValidator
{
    private const int MAX_TOTAL = 200;
    private const int MAX_REPORTED = 25;

    // Hamilton apportionment hands no type a leftover any more; this is just the one share the
    // DRIFT check excuses. Normal: the largest share, so the one that can best absorb a seat.
    private const int REMAINDER_INDEX = 0; // Normal

    private static readonly string[] k_TypeNames = { "Normal", "Fast", "Tank", "Horde", "Herald" };

    [MenuItem("Tools/TD/Validate Balance Tables")]
    public static void RunFromMenu()
    {
        var failures = Run();

        if (failures.Count == 0)
        {
            int cases = MAX_TOTAL * System.Enum.GetValues(typeof(Difficulty)).Length;
            Debug.Log($"<color=#A1CD3A><b>[TDBalanceValidator] PASS</b></color> — {cases} cases, 0 failures.");
            return;
        }

        int shown = Mathf.Min(failures.Count, MAX_REPORTED);
        for (int i = 0; i < shown; i++) Debug.LogError($"[TDBalanceValidator] {failures[i]}");
        if (failures.Count > shown)
            Debug.LogError($"[TDBalanceValidator] …and {failures.Count - shown} more.");
        Debug.LogError($"<b>[TDBalanceValidator] FAIL</b> — {failures.Count} failures.");
    }

    public static List<string> Run()
    {
        var failures = new List<string>();

        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
        {
            var row = DifficultyRatioTable.Get(d);
            float[] pcts = { row.normalPct, row.fastPct, row.tankPct, row.hordePct, row.heraldPct };

            // Ratios must describe a whole distribution, or every count below is meaningless.
            float sumPct = 0f;
            for (int i = 0; i < pcts.Length; i++) sumPct += pcts[i];
            if (Mathf.Abs(sumPct - 1f) > 0.001f)
                failures.Add($"[RATIO] {d}: percentages sum to {sumPct:F3}, expected 1.000");

            for (int total = 1; total <= MAX_TOTAL; total++)
            {
                int[] counts = DifficultyRatioTable.Distribute(d, total);

                // A — the invariant the remainder trick exists to guarantee.
                int sum = 0;
                for (int i = 0; i < counts.Length; i++) sum += counts[i];
                if (sum != total)
                    failures.Add($"[SUM] {d} total={total}: counts sum to {sum} — [{string.Join(",", counts)}]");

                // B — a negative count means the remainder went underwater.
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] < 0)
                        failures.Add($"[NEGATIVE] {d} total={total}: {k_TypeNames[i]}={counts[i]}");
                }

                // C — every explicitly-rounded type stays within one of its ideal share.
                // The remainder index is skipped: absorbing the others' error is its job.
                for (int i = 0; i < counts.Length; i++)
                {
                    if (i == REMAINDER_INDEX) continue;
                    float ideal = pcts[i] * total;
                    if (Mathf.Abs(counts[i] - ideal) > 1f)
                        failures.Add($"[DRIFT] {d} total={total}: {k_TypeNames[i]}={counts[i]}, ideal={ideal:F2}");
                }
            }

            // The maze generator proves wave 1 is affordable against CONFIG_PLAYER_STARTING_GOLD;
            // a difficulty that starts the player below it voids that proof.
            if (row.startingGold < TDConstant.CONFIG_PLAYER_STARTING_GOLD)
                failures.Add($"[STARTING_GOLD_FLOOR] {d}: starts with {row.startingGold} < {TDConstant.CONFIG_PLAYER_STARTING_GOLD}");

            Growth(failures, d, row);
        }

        Dominance(failures);
        return failures;
    }

    // ── Growth — spec 2026-10-06 §5.7: waves grow, the level's total holds ──
    //
    // Boss count has ONE source, the difficulty row; the generator and this check both read it
    // (it used to be bossPct here and GetBossParams there — two answers to one question).
    // A level asking for fewer enemies than its waves can hold (every wave 1, every boss wave
    // its bosses + 1 escort) gets that minimum instead, so `want` is max(total, floor).
    private static void Growth(List<string> f, Difficulty d, DifficultyRatioTable.RatioRow row)
    {
        foreach (float g in new[] { 1f, 2.7f })
            for (int waves = 1; waves <= 10; waves++)
            {
                int floor = waves + Mathf.Min(row.bossWaveCount, waves) * row.bossPerWave;
                for (int total = waves; total <= MAX_TOTAL; total++)
                {
                    var plan = TDEnemyPathMainControl.BuildWavePlans(d, waves, total, g);
                    int sum = plan.Sum(w => w.Count);
                    int want = Mathf.Max(total, floor);
                    if (sum != want) f.Add($"[GROWTH_SUM] {d} w={waves} t={total} g={g}: {sum}, want {want}");
                    if (plan.Any(w => w.Count < 1)) f.Add($"[GROWTH_MIN1] {d} w={waves} t={total} g={g}");
                    // boss waves keep room for at least one escort
                    foreach (var w in plan.Where(w => w.Contains(EnemyType.Boss)))
                        if (w.Count < row.bossPerWave + 1) f.Add($"[GROWTH_BOSS_ROOM] {d} w={waves} t={total}");
                    // regular waves never shrink when g >= 1 (one of slack for rounding)
                    var regular = plan.Where(w => !w.Contains(EnemyType.Boss)).Select(w => w.Count).ToList();
                    for (int i = 1; i < regular.Count; i++)
                        if (regular[i] < regular[i - 1] - 1) f.Add($"[GROWTH_MONOTONE] {d} w={waves} t={total} g={g}");

                    int bosses = plan.Sum(w => w.Count(e => e == EnemyType.Boss));
                    if (waves >= row.bossWaveCount && bosses != row.bossWaveCount * row.bossPerWave)
                        f.Add($"[BOSS_PRESENT] {d} w={waves} t={total}: {bosses} bosses, want {row.bossWaveCount * row.bossPerWave}");
                }
            }
    }

    // ── Dominance — spec 2026-10-06 §5.6: every unit has a role ─────────────
    //
    // Within a group (melee / ranged operators / turrets), no unit may be at least as good as
    // another on every axis and strictly better on one — that other unit would never be worth
    // a slot. Axes are all "higher is better"; cost is negated. Targets per swing is a COUNT,
    // not yes/no: as a flag, Catapult (2 targets, 10 gold) would "beat" MissileG02 (3, 12).
    private static void Dominance(List<string> f)
    {
        var ops = LoadConfig<TDFlyweightOperatorDataSettings>()?.GetAllOperators();
        var towers = LoadConfig<TDFlyweightTowerDataSettings>()?.GetAllTowers();
        if (ops == null || towers == null) { f.Add("[DOMINANCE_CONFIG] operator or tower config not loadable"); return; }

        float Cells(Vector2Int[] r) => Mathf.Max(1, r?.Length ?? 0);
        var melee = new List<(string, float[])>();
        var ranged = new List<(string, float[])>();
        foreach (var o in ops)
        {
            float targets = o.attackType == AttackType.Multiple ? o.blockCount : o.splashRadius > 0 ? 9 : 1;
            var axes = new[] { o.blockCount, o.damage * o.attackSpeed, o.hp, -o.cost, Cells(o.rangeOffsets), targets };
            (o.deployZone == DeployZone.PathCell ? melee : ranged).Add((o.operatorName, axes));
        }

        var turrets = new List<(string, float[])>();
        foreach (var t in towers)
        {
            float targets = t.attackType switch { AttackType.Multiple => t.maxTargets, AttackType.AOE => 99, _ => 1 };
            turrets.Add((t.type.ToString(), new[] { t.damage * t.attackSpeed, -t.cost, Cells(t.rangeOffsets), targets }));
        }

        Dominated(f, melee);
        Dominated(f, ranged);
        Dominated(f, turrets);
    }

    private static void Dominated(List<string> f, List<(string name, float[] axes)> group)
    {
        for (int a = 0; a < group.Count; a++)
            for (int b = 0; b < group.Count; b++)
            {
                if (a == b) continue;
                bool allGe = true, anyGt = false;
                for (int i = 0; i < group[a].axes.Length; i++)
                {
                    allGe &= group[a].axes[i] >= group[b].axes[i];
                    anyGt |= group[a].axes[i] > group[b].axes[i];
                }
                if (allGe && anyGt) f.Add($"[DOMINATED] {group[a].name} ≥ {group[b].name} on every axis");
            }
    }

    // Straight from the asset database: the configs' .api routes through a scene object and
    // is null outside play mode (same reason as TDMoraleValidator.LoadOperatorConfig).
    private static T LoadConfig<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}

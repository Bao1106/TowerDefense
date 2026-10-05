using System.Collections.Generic;
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

    // Distribute() gives this index the leftover so the sum always lands on `total`,
    // which means it absorbs every other type's rounding error. Tolerance checks skip it.
    // NOTE: when Distribute() moves to 6 types this must become the Normal index (0),
    // because Boss is too small to absorb the drift — see MORALE_SYSTEM_DESIGN.md § 11.3.
    private const int REMAINDER_INDEX = 3; // Boss

    private static readonly string[] k_TypeNames = { "Normal", "Fast", "Tank", "Boss" };

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
            float[] pcts = { row.normalPct, row.fastPct, row.tankPct, row.bossPct };

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

                // D — a stage whose ratios call for a boss must actually get one.
                // This is the assertion that fires when Distribute() grows to 6 types and
                // the leftover for Boss is eaten by the extra rounding error.
                float bossIdeal = row.bossPct * total;
                if (bossIdeal >= 1f && counts[3] < 1)
                    failures.Add($"[NO_BOSS] {d} total={total}: bossPct={row.bossPct:F2} wants {bossIdeal:F2} boss, got 0");
            }
        }

        return failures;
    }
}

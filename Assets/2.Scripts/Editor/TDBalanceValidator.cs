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

            // The mix shifts over a match (D14), so every check holds at its start, middle and end.
            foreach (float t in new[] { 0f, 0.5f, 1f })
            {
                float[] pcts = DifficultyRatioTable.Shares(d, t);

                // Ratios must describe a whole distribution, or every count below is meaningless.
                float sumPct = 0f;
                for (int i = 0; i < pcts.Length; i++) sumPct += pcts[i];
                if (Mathf.Abs(sumPct - 1f) > 0.001f)
                    failures.Add($"[RATIO] {d} t={t}: percentages sum to {sumPct:F3}, expected 1.000");

                for (int total = 1; total <= MAX_TOTAL; total++)
                {
                    int[] counts = DifficultyRatioTable.Distribute(d, total, t);

                    // A — the invariant the remainder trick exists to guarantee.
                    int sum = 0;
                    for (int i = 0; i < counts.Length; i++) sum += counts[i];
                    if (sum != total)
                        failures.Add($"[SUM] {d} t={t} total={total}: counts sum to {sum} — [{string.Join(",", counts)}]");

                    // B — a negative count means the remainder went underwater.
                    for (int i = 0; i < counts.Length; i++)
                    {
                        if (counts[i] < 0)
                            failures.Add($"[NEGATIVE] {d} t={t} total={total}: {k_TypeNames[i]}={counts[i]}");
                    }

                    // C — every explicitly-rounded type stays within one of its ideal share.
                    // The remainder index is skipped: absorbing the others' error is its job.
                    for (int i = 0; i < counts.Length; i++)
                    {
                        if (i == REMAINDER_INDEX) continue;
                        float ideal = pcts[i] * total;
                        if (Mathf.Abs(counts[i] - ideal) > 1f)
                            failures.Add($"[DRIFT] {d} t={t} total={total}: {k_TypeNames[i]}={counts[i]}, ideal={ideal:F2}");
                    }
                }
            }

            Mix(failures, d, row);

            // The maze generator proves wave 1 is affordable against CONFIG_PLAYER_STARTING_GOLD;
            // a difficulty that starts the player below it voids that proof.
            if (row.startingGold < TDConstant.CONFIG_PLAYER_STARTING_GOLD)
                failures.Add($"[STARTING_GOLD_FLOOR] {d}: starts with {row.startingGold} < {TDConstant.CONFIG_PLAYER_STARTING_GOLD}");

            Growth(failures, d, row);
            Horde(failures, d);
        }

        HordeTime(failures);
        StartingGoldOneWriter(failures);
        ReusedTarget(failures);

        Density(failures);
        Dominance(failures);
        LoadFactor(failures);
        return failures;
    }

    // ── Mix — spec 2026-10-06 §5.7 / D14: heavy types arrive as the match goes on ──
    //
    // Heavy shares ramp from (1 − r) to (1 + r) of the table, so the match average IS the
    // table; mixRamp only decides when the heavy enemies come.
    private static void Mix(List<string> f, Difficulty d, DifficultyRatioTable.RatioRow row)
    {
        foreach (float t in new[] { 0f, 1f })
        {
            var s = DifficultyRatioTable.Shares(d, t);
            if (s.Any(x => x < 0f) || Mathf.Abs(s.Sum() - 1f) > 0.001f)
                f.Add($"[MIX_FEASIBLE] {d} t={t}: [{string.Join(", ", s.Select(x => x.ToString("F3")))}]");
        }

        float[] table = { row.normalPct, row.fastPct, row.tankPct, row.hordePct, row.heraldPct };
        var mid = DifficultyRatioTable.Shares(d, 0.5f);
        for (int i = 0; i < table.Length; i++)
            if (Mathf.Abs(mid[i] - table[i]) > 0.001f)
                f.Add($"[MIX_AVERAGE] {d}: {k_TypeNames[i]} {mid[i]:F3} mid-match, table says {table[i]:F3}");

        float Heavy(float t) { var s = DifficultyRatioTable.Shares(d, t); return s[2] + s[3] + s[4]; }
        if (Heavy(1f) < Heavy(0f) || (row.mixRamp > 0f && Heavy(1f) <= Heavy(0f)))
            f.Add($"[MIX_RAMPS] {d}: heavy share {Heavy(0f):F3} at the start, {Heavy(1f):F3} at the end");
    }

    // ── Density — spec 2026-10-06 §5.7 / D14: later waves come faster ───────
    //
    // Never slower than the level's own spawnInterval, never denser than the floor — unless
    // the level already asked for denser, which is its call, not ours.
    private static void Density(List<string> f)
    {
        foreach (float s in new[] { 0.5f, 1.5f, 2f })
            foreach (float g in new[] { 0.5f, 1f, 2.7f })
                for (int n = 1; n <= 10; n++)
                {
                    string at = $"s={s} g={g} n={n}";
                    float prev = TDEnemyPathMainControl.SpawnIntervalFor(s, 0, n, g);
                    if (Mathf.Abs(prev - s) > 0.0001f) f.Add($"[DENSITY_FIRST] {at}: wave 1 at {prev:F2}");

                    for (int i = 0; i < n; i++)
                    {
                        float si = TDEnemyPathMainControl.SpawnIntervalFor(s, i, n, g);
                        if (si > s + 0.0001f) f.Add($"[DENSITY_NEVER_SLOWER] {at} wave {i + 1}: {si:F2}");
                        if (si < Mathf.Min(s, TDConstant.CONFIG_SPAWN_INTERVAL_FLOOR) - 0.0001f)
                            f.Add($"[DENSITY_FLOOR] {at} wave {i + 1}: {si:F2}");
                        if (g >= 1f && si > prev + 0.0001f) f.Add($"[DENSITY_MONOTONE] {at} wave {i + 1}: {si:F2} after {prev:F2}");
                        prev = si;
                    }
                }
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
                    // Growth shares out SLOTS: a Horde slot becomes a pack of five afterwards.
                    var plan = TDEnemyPathMainControl.BuildWavePlans(d, waves, total, g);
                    int sum = plan.Sum(TDEnemyPathMainControl.Slots);
                    int want = Mathf.Max(total, floor);
                    if (sum != want) f.Add($"[GROWTH_SUM] {d} w={waves} t={total} g={g}: {sum}, want {want}");
                    if (plan.Any(w => w.Count < 1)) f.Add($"[GROWTH_MIN1] {d} w={waves} t={total} g={g}");
                    // boss waves keep room for at least one escort
                    foreach (var w in plan.Where(w => w.Contains(EnemyType.Boss)))
                        if (TDEnemyPathMainControl.Slots(w) < row.bossPerWave + 1) f.Add($"[GROWTH_BOSS_ROOM] {d} w={waves} t={total}");
                    // regular waves never shrink when g >= 1 (one of slack for rounding)
                    var regular = plan.Where(w => !w.Contains(EnemyType.Boss)).Select(TDEnemyPathMainControl.Slots).ToList();
                    for (int i = 1; i < regular.Count; i++)
                        if (regular[i] < regular[i - 1] - 1) f.Add($"[GROWTH_MONOTONE] {d} w={waves} t={total} g={g}");

                    int bosses = plan.Sum(w => w.Count(e => e == EnemyType.Boss));
                    if (waves >= row.bossWaveCount && bosses != row.bossWaveCount * row.bossPerWave)
                        f.Add($"[BOSS_PRESENT] {d} w={waves} t={total}: {bosses} bosses, want {row.bossWaveCount * row.bossPerWave}");
                }
            }
    }

    // ── Horde — spec 2026-10-06 §5.5: one slot, a pack of five released in a burst ──
    private static void Horde(List<string> f, Difficulty d)
    {
        const int waves = 8;
        int pack = TDConstant.HORDE_PACK_SIZE;
        for (int total = 10; total <= MAX_TOTAL; total++)
        {
            var plan = TDEnemyPathMainControl.BuildWavePlans(d, waves, total, 2.7f);
            for (int w = 0; w < plan.Count; w++)
            {
                var wave = plan[w];
                string at = $"{d} t={total} wave {w + 1}";

                for (int i = 0; i <= wave.Count; i++)
                    if (HordeBefore(wave, i) % pack != 0 && (i == wave.Count || wave[i] != EnemyType.Horde))
                    {
                        f.Add($"[HORDE_PACKS_INTACT] {at}: a run of {HordeBefore(wave, i)} Horde");
                        break;
                    }

                int bosses = wave.Count(e => e == EnemyType.Boss);
                if (wave.Skip(wave.Count - bosses).Any(e => e != EnemyType.Boss))
                    f.Add($"[HORDE_BOSS_LAST] {at}: [{string.Join(",", wave)}]");

                int slots = TDEnemyPathMainControl.Slots(wave);
                int hordeSlots = DifficultyRatioTable.Distribute(d, slots - bosses, (float)w / (waves - 1))[3];
                int want = (slots - bosses - hordeSlots) + pack * hordeSlots + bosses;
                if (wave.Count != want || wave.Count(e => e == EnemyType.Horde) != pack * hordeSlots)
                    f.Add($"[HORDE_TOTAL_EXPANDED] {at}: {wave.Count} enemies, want {want} ({hordeSlots} Horde slots)");

                for (int groups = 1; groups <= 3; groups++)
                {
                    var slices = TDEnemyPathMainControl.SliceWave(wave, groups);
                    if (slices.Count != groups || !slices.SelectMany(s => s).SequenceEqual(wave))
                        f.Add($"[HORDE_SLICE_INTACT] {at} groups={groups}: slices do not add back up to the wave");
                    int start = 0;
                    foreach (var s in slices)
                    {
                        if (start < wave.Count && wave[start] == EnemyType.Horde && HordeBefore(wave, start) % pack != 0)
                            f.Add($"[HORDE_SLICE_INTACT] {at} groups={groups}: a slice starts inside a pack at {start}");
                        start += s.Count;
                    }
                }
            }
        }
    }

    // Horde immediately before index i — i's position inside the run it belongs to.
    private static int HordeBefore(List<EnemyType> wave, int i)
    {
        int n = 0;
        while (i - n - 1 >= 0 && wave[i - n - 1] == EnemyType.Horde) n++;
        return n;
    }

    // A pack lasts 4 × the pack gap + the wave's spacing; everything else one spacing each.
    private static void HordeTime(List<string> f)
    {
        var pack = Enumerable.Repeat(EnemyType.Horde, TDConstant.HORDE_PACK_SIZE).ToList();
        float one = TDEnemyPathMainControl.SliceSeconds(pack, 2f);
        float wantOne = (TDConstant.HORDE_PACK_SIZE - 1) * TDConstant.HORDE_PACK_SPAWN_INTERVAL + 2f;
        if (Mathf.Abs(one - wantOne) > 0.001f) f.Add($"[HORDE_PACK_TIME] one pack at s=2: {one:F2}s, want {wantOne:F2}");

        var mixed = new List<EnemyType> { EnemyType.Normal };
        mixed.AddRange(pack);
        mixed.AddRange(pack);
        mixed.Add(EnemyType.Fast);
        float two = TDEnemyPathMainControl.SliceSeconds(mixed, 2f);
        if (Mathf.Abs(two - (2f + 2f * wantOne + 2f)) > 0.001f)
            f.Add($"[HORDE_PACK_TIME] Normal, two packs, Fast at s=2: {two:F2}s, want {2f + 2f * wantOne + 2f:F2}");
    }

    // A ranged shot is aimed in TryAttack and lands on the animation's hit event. Enemies are
    // pooled: one that dies in between can be handed out again as a NEW enemy before the hit —
    // the shot must not land on (or splash around) the newcomer. Spawn ids, not object identity.
    private static void ReusedTarget(List<string> f)
    {
        var go = new GameObject("ReusedTargetProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var enemy = go.AddComponent<TDEnemyView>();
            var ranged = new TowerZoneOperatorBehavior();
            var data = new OperatorData { damage = 10f };
            bool ok = TDMoraleValidator.SetField(f, enemy, "m_HPBarView", null)
                    & TDMoraleValidator.SetField(f, enemy, "m_EnemyHealth", 100f)
                    & TDMoraleValidator.SetField(f, enemy, "m_MaxHealth", 100f)
                    & TDMoraleValidator.SetField(f, enemy, "m_SpawnId", 2)
                    & TDMoraleValidator.SetField(f, ranged, "m_PendingTarget", enemy)
                    & TDMoraleValidator.SetField(f, ranged, "m_PendingSpawnId", 1);
            if (!ok) return;

            ranged.ExecuteHit(Vector2Int.zero, Vector3.zero, data);
            float hp = TDMoraleValidator.GetField<float>(enemy, "m_EnemyHealth");
            if (hp != 100f) f.Add($"[HIT_SKIPS_REUSED_TARGET] the shot aimed at spawn 1 hit spawn 2 (hp {hp})");

            TDMoraleValidator.SetField(f, ranged, "m_PendingTarget", enemy);
            TDMoraleValidator.SetField(f, ranged, "m_PendingSpawnId", 2);
            ranged.ExecuteHit(Vector2Int.zero, Vector3.zero, data);
            hp = TDMoraleValidator.GetField<float>(enemy, "m_EnemyHealth");
            if (hp != 90f) f.Add($"[HIT_CONTROL] the shot at its own target left hp {hp}, want 90");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Spec §5.7: the difficulty sets the opening purse, and it is the only writer. The HUD's
    // Start and the path view's both run at scene load with no defined order, so a HUD that
    // seeds a default would leave Normal at 30 instead of 40 whenever it happens to run second.
    private static void StartingGoldOneWriter(List<string> f)
    {
        var savedGold = TDGoldControl.api; var savedSpeed = TDSpeedControl.api;
        var go = new GameObject("StartingGoldOneWriter") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            TDGoldControl.api = new TDGoldControl(); TDSpeedControl.api = new TDSpeedControl();
            int purse = DifficultyRatioTable.Get(Difficulty.Normal).startingGold;
            TDGoldControl.api.Initialize(purse); // the path view ran first
            var hud = go.AddComponent<TDGameplayHUDView>();
            typeof(TDGameplayHUDView).GetMethod("InitControls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(hud, null);
            if (TDGoldControl.api.Gold != purse)
                f.Add($"[STARTING_GOLD_ONE_WRITER] the HUD's init rewrote Normal's opening purse {purse} → {TDGoldControl.api.Gold}");
        }
        finally
        {
            TDGoldControl.api = savedGold; TDSpeedControl.api = savedSpeed;
            Object.DestroyImmediate(go);
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

    // ── LoadFactor — spec 2026-10-06 §4.2 / §5.7: ρ per wave, every level × difficulty ──
    //
    // ρ = wave HP / (team DPS × T + team block × H). Structure is always checked; the
    // targets only once EfficiencyOf(d) is calibrated against bot runs — an uncalibrated ρ
    // is a shape, not a number to hold the data to.
    //
    // No early (before the cap) target here: the model over-reads small waves — calibrated ρ
    // 1.1–1.6 at waves 1–2 where 9 of 9 bot runs leaked nothing. The bot runs check that phase
    // instead: fewer than 2 enemies through per wave before the cap (round-1 measurements).
    private static readonly Dictionary<Difficulty, (float atCap, float late, float peak)> k_RhoTargets = new()
    {
        { Difficulty.Normal,    (0.60f, 0.75f, 1.00f) },
        { Difficulty.Hard,      (0.75f, 0.90f, 1.20f) },
        { Difficulty.Nightmare, (0.85f, 1.00f, 1.40f) },
    };

    private static void LoadFactor(List<string> f)
    {
        var ops = LoadConfig<TDFlyweightOperatorDataSettings>()?.GetAllOperators();
        var enemies = LoadConfig<TDFlyweightEnemyDataSettings>()?.GetAllEnemies();
        var levels = LoadConfig<TDLevelConfigSettings>()?.GetAllLevels();
        var stages = LoadConfig<TDStageRepository>()?.GetAll();
        if (ops == null || enemies == null || levels == null || stages == null) { f.Add("[RHO_CONFIG] operator, enemy, level or stage config not loadable"); return; }

        foreach (var name in TDLoadModel.ReferenceTeam)
            if (!ops.Any(o => o.operatorName == name)) f.Add($"[RHO_TEAM_RESOLVES] reference team member '{name}' not in the operator config");

        foreach (var level in levels)
        {
            int gates = TDLoadModel.GatesFor(level.levelIndex, stages);
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                var rows = TDLoadModel.Compute(level, d, ops, enemies, gates);
                string at = $"level {level.levelIndex} {d}";
                foreach (var r in rows)
                    if (float.IsNaN(r.rho) || float.IsInfinity(r.rho)) f.Add($"[RHO_FINITE] {at} wave {r.wave}: ρ = {r.rho}");

                // Wave plans are shuffled; ρ must not depend on the shuffle, or every check below
                // passes or fails by chance.
                var again = TDLoadModel.Compute(level, d, ops, enemies, gates);
                for (int i = 0; i < rows.Count; i++)
                    if (Mathf.Abs(rows[i].rho - again[i].rho) > 0.0001f)
                    {
                        f.Add($"[RHO_DETERMINISTIC] {at} wave {rows[i].wave}: {rows[i].rho:F3} then {again[i].rho:F3}");
                        break;
                    }

                // D14: load keeps rising after the team reaches the cap. A ratio, so it holds
                // calibrated or not — this is the shape the waves exist to produce.
                int limit = Mathf.Max(1, level.deployLimit + DifficultyRatioTable.Get(d).deployLimitDelta);
                var atCap = rows.FirstOrDefault(r => !r.isBoss && r.onField == limit);
                var last = rows.LastOrDefault(r => !r.isBoss);
                if (atCap.wave > 0 && last.wave > atCap.wave && last.rho < 1.1f * atCap.rho)
                    f.Add($"[RHO_RISES] {at}: last regular wave {last.rho:F2} < 1.1 × first at cap {atCap.rho:F2}");

                float e = TDLoadModel.EfficiencyOf(d);
                if (float.IsNaN(e) || rows.Count == 0) continue;

                var want = k_RhoTargets[d];
                if (atCap.wave > 0 && Mathf.Abs(atCap.rho - want.atCap) > 0.1f)
                    f.Add($"[RHO_AT_CAP] {at} wave {atCap.wave}: {atCap.rho:F2}, want {want.atCap:F2} ± 0.1");
                if (last.wave > 0 && Mathf.Abs(last.rho - want.late) > 0.1f)
                    f.Add($"[RHO_LATE] {at} wave {last.wave}: {last.rho:F2}, want {want.late:F2} ± 0.1");
                var boss = rows.Where(r => r.isBoss).ToList();
                if (boss.Count > 0 && Mathf.Abs(boss.Max(r => r.rho) - want.peak) > 0.15f)
                    f.Add($"[RHO_PEAK] {at}: {boss.Max(r => r.rho):F2}, want {want.peak:F2} ± 0.15");
            }
        }
    }

    [MenuItem("Tools/TD/Print Load Table")]
    public static void PrintLoadTableFromMenu() => Debug.Log(LoadTable());

    /// One line per wave for every level × difficulty — the table §5.7 compares against.
    public static string LoadTable()
    {
        var ops = LoadConfig<TDFlyweightOperatorDataSettings>()?.GetAllOperators();
        var enemies = LoadConfig<TDFlyweightEnemyDataSettings>()?.GetAllEnemies();
        var levels = LoadConfig<TDLevelConfigSettings>()?.GetAllLevels();
        var stages = LoadConfig<TDStageRepository>()?.GetAll();
        if (ops == null || enemies == null || levels == null || stages == null) return "[LoadTable] operator, enemy, level or stage config not loadable";

        var sb = new System.Text.StringBuilder();
        foreach (var level in levels)
        {
            int gates = TDLoadModel.GatesFor(level.levelIndex, stages);
            foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
            {
                float e = TDLoadModel.EfficiencyOf(d);
                sb.AppendLine($"── level {level.levelIndex} · {d} · {level.totalEnemies} enemies / {level.waveCount} waves · growth {level.waveGrowth} · " +
                              (float.IsNaN(e) ? "uncalibrated" : $"efficiency {e:F2}") + " ──");
                sb.AppendLine("wave  boss  onField      hp  bodies      T      ρ");
                foreach (var r in TDLoadModel.Compute(level, d, ops, enemies, gates))
                    sb.AppendLine($"{r.wave,4}  {(r.isBoss ? "  B " : "    ")}  {r.onField,7}  {r.hp,6:F0}  {r.bodies,6}  {r.seconds,5:F1}  {r.rho,5:F2}");
            }
        }
        return sb.ToString();
    }

    // Straight from the asset database: the configs' .api routes through a scene object and
    // is null outside play mode (same reason as TDMoraleValidator.LoadOperatorConfig).
    private static T LoadConfig<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}

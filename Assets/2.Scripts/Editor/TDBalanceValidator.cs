using System.Collections.Generic;
using System.Linq;
using TDEnums;
using ToonShadersPro.URP;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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

        // Every growth a shipped level uses, plus 1 (no growth). Read once, here — the loops
        // below call the generator thousands of times and must not touch the AssetDatabase.
        var levels = LoadConfig<TDLevelConfigSettings>()?.GetAllLevels();
        if (levels == null || levels.Count == 0) failures.Add("[GROWTH_NO_LEVELS] no level config to read waveGrowth from");
        var growths = new[] { 1f }.Concat(levels?.Select(l => l.waveGrowth) ?? Enumerable.Empty<float>()).Distinct().ToList();

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

            Growth(failures, d, row, growths);
            Horde(failures, d);
        }

        HordeTime(failures);
        StartingGoldOneWriter(failures);
        ReusedTarget(failures);
        ReusedBolt(failures);
        AmpRingWired(failures, LoadConfig<TDFlyweightEnemyDataSettings>()?.GetAllEnemies());
        MoraleWired(failures);
        HpBarOnTop(failures);
        EndSfxWired(failures);
        ToonUnits(failures);
        ToonMap(failures);
        ToonAlbedoOnce(failures);
        Outline(failures);
        BoardPalette(failures);
        BoardFloor(failures);
        HighlightColors(failures);
        OverlaysClearBlocks(failures);

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
    private static void Growth(List<string> f, Difficulty d, DifficultyRatioTable.RatioRow row, IReadOnlyList<float> growths)
    {
        foreach (float g in growths)
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

    // Same pool hazard for turret bolts, which home every frame: once the target leaves (dies, or
    // reaches the gate) and the object comes back as a new enemy, the bolt must stop following it
    // and finish at the last known position — not cross the map after the newcomer.
    private static void ReusedBolt(List<string> f)
    {
        var enemyGo = new GameObject("ReusedBoltEnemy") { hideFlags = HideFlags.HideAndDontSave };
        var boltGo = new GameObject("ReusedBoltProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            // Both points far from the bolt (at the origin): it must never arrive during the test —
            // OnImpact would release it into the static bullet pool.
            enemyGo.transform.position = new Vector3(100f, 0f, 0f);
            var enemy = enemyGo.AddComponent<TDEnemyView>();
            var bolt = boltGo.AddComponent<TDAttackVFX>();
            var lastKnown = new Vector3(-100f, 0f, 0f);
            var update = typeof(TDAttackVFX).GetMethod("Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            bool ok = TDMoraleValidator.SetField(f, enemy, "m_HPBarView", null)
                    & TDMoraleValidator.SetField(f, enemy, "m_SpawnId", 2)
                    & TDMoraleValidator.SetField(f, bolt, "m_Target", enemy)
                    & TDMoraleValidator.SetField(f, bolt, "m_TargetSpawnId", 1)
                    & TDMoraleValidator.SetField(f, bolt, "m_TargetPos", lastKnown);
            if (!ok || update == null) return;

            update.Invoke(bolt, null);
            var pos = TDMoraleValidator.GetField<Vector3>(bolt, "m_TargetPos");
            if (pos != lastKnown) f.Add($"[BOLT_DROPS_REUSED_TARGET] the bolt fired at spawn 1 now heads for spawn 2 at {pos}");

            if (TDMoraleValidator.GetField<bool>(bolt, "m_HasImpacted")) { f.Add("[BOLT_TEST] the bolt arrived mid-test"); return; }
            TDMoraleValidator.SetField(f, bolt, "m_Target", enemy);
            TDMoraleValidator.SetField(f, bolt, "m_TargetSpawnId", 2);
            TDMoraleValidator.SetField(f, bolt, "m_TargetPos", lastKnown);
            update.Invoke(bolt, null);
            pos = TDMoraleValidator.GetField<Vector3>(bolt, "m_TargetPos");
            if (pos != enemyGo.transform.position) f.Add($"[BOLT_CONTROL] the bolt stopped tracking its own target (heads for {pos})");
        }
        finally
        {
            Object.DestroyImmediate(boltGo);
            Object.DestroyImmediate(enemyGo);
        }
    }

    // Tầng 4b §4.6: every enemy that amplifies leaks wears the amplifier ring and no other does — read
    // from TDOperatorRegistry.AmplifiesLeaks, the rule the stress code itself uses, so a new amplifier
    // type gets its ring for free. It goes out the moment the enemy starts dying (it has left the
    // registry, so it amplifies nothing during the death clip) and comes back when the pool hands the
    // same object out again. Herald's purple disc, which tinted the ground under every badge (Tầng 4
    // A2), is gone. Prefabs are read from the enemy data the game spawns from.
    private static void AmpRingWired(List<string> f, IReadOnlyList<EnemyData> enemies)
    {
        const System.Reflection.BindingFlags I = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        const System.Reflection.BindingFlags S = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;

        if (typeof(TDEnemyView).GetField("m_AuraVisual", I) != null)
            f.Add("[AMP_RING_ON_AMPLIFIERS] TDEnemyView still has m_AuraVisual");
        var herald = enemies?.FirstOrDefault(e => e.type == EnemyType.Herald)?.prefab;
        if (herald != null && herald.GetComponentsInChildren<Transform>(true).Any(t => t.name == "FearRing"))
            f.Add("[AMP_RING_ON_AMPLIFIERS] Herald.prefab still has its FearRing disc");
        if (enemies == null || enemies.Count == 0) { f.Add("[AMP_RING_ON_AMPLIFIERS] no enemy data"); return; }

        var die = typeof(TDEnemyView).GetMethod("Die", I);
        var diedField = typeof(TDGameEventBus).GetField("OnEnemyDied", S);
        if (die == null || diedField == null) { f.Add("[REFLECT] TDEnemyView.Die / TDGameEventBus.OnEnemyDied missing"); return; }

        // Initialize and Die reach controls that only exist in a running match: stand in fresh ones, and
        // stash the bus as Lives does — a live effect manager would play a death from a validator run.
        var savedControl = TDEnemyControl.api;
        var savedRegistry = TDEnemyRegistry.api;
        object savedDied = diedField.GetValue(null);
        TDEnemyControl.api = new TDEnemyControl();
        TDEnemyRegistry.api = new TDEnemyRegistry();
        diedField.SetValue(null, null);
        try
        {
            foreach (var data in enemies)
            {
                if (data?.prefab == null || data.prefab.GetComponent<TDEnemyView>() == null) continue;
                var go = Object.Instantiate(data.prefab);
                go.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var enemy = go.GetComponent<TDEnemyView>();
                    TDMoraleValidator.SetField(f, enemy, "m_HPBarView", null); // Awake does not run in Edit Mode
                    bool want = TDOperatorRegistry.AmplifiesLeaks(data.type);

                    enemy.Initialize("amp-probe", 100f, 1f, 0f, 1f, 1f, 0, data.type);
                    bool spawned = RingShown(go);
                    die.Invoke(enemy, null); // a 1 s death clip, so it waits rather than returning to the pool
                    bool dying = RingShown(go);
                    enemy.Initialize("amp-probe", 100f, 1f, 0f, 1f, 1f, 0, data.type);
                    bool reused = RingShown(go);

                    if (spawned != want || dying || reused != want)
                        f.Add($"[AMP_RING_ON_AMPLIFIERS] {data.type}: ring on spawn {spawned}, while dying {dying}, after pool reuse {reused}; amplifies {want}");
                }
                catch (System.Exception e)
                {
                    f.Add($"[AMP_RING_ON_AMPLIFIERS] {data.type}: {(e.InnerException ?? e).GetType().Name} {(e.InnerException ?? e).Message}");
                }
                finally { Object.DestroyImmediate(go); } // before the controls are put back: OnDestroy reads them
            }
        }
        finally
        {
            TDEnemyControl.api = savedControl;
            TDEnemyRegistry.api = savedRegistry;
            diedField.SetValue(null, savedDied);
        }
    }

    private static bool RingShown(GameObject enemy)
    {
        var ring = enemy.GetComponentInChildren<TDAmpRingView>(true);
        return ring != null && ring.gameObject.activeSelf;
    }

    // Tầng 4 §4.5: the three morale cues are wired in the Effect Config, each with a clip and the
    // priority flag — without the flag a break sting is the first sound a busy fight drops.
    // TDEffectConfig.api resolves through TDResourceObject, which only exists in a running scene,
    // so the asset is loaded the way AuraVisual's enemy data is.
    //
    // §4.1: the BROKEN label is built in code, so the HUD's font has to be reachable from Resources;
    // a miss silently falls back to TMP's default face.
    private static void MoraleWired(List<string> f)
    {
        var font = Resources.Load<TMPro.TMP_FontAsset>(TDMoraleIconView.LABEL_FONT_PATH);
        if (font == null)
            f.Add($"[MORALE_LABEL_FONT] no TMP font at Resources/{TDMoraleIconView.LABEL_FONT_PATH}");

        // The label's overlay material is an asset, not built at runtime: a shader that only code
        // asks for by name is stripped from a build, and the label would fall back to depth-tested.
        var labelMat = Resources.Load<Material>(TDMoraleIconView.LABEL_MATERIAL_PATH);
        if (labelMat == null || !labelMat.shader.name.EndsWith("Overlay")
            || (font != null && labelMat.GetTexture("_MainTex") != font.atlasTexture))
            f.Add($"[MORALE_LABEL_MATERIAL] Resources/{TDMoraleIconView.LABEL_MATERIAL_PATH}: {(labelMat == null ? "missing" : labelMat.shader.name)}, want an Overlay preset of the label font's atlas");

        var all = LoadConfig<TDEffectConfig>()?.GetAll();
        if (all == null) { f.Add("[MORALE_SFX_WIRED] no Effect Config asset"); return; }

        foreach (var key in new[] { GameEventKey.OperatorStressed, GameEventKey.OperatorBroke, GameEventKey.OperatorRecovered })
        {
            var defs = all.Where(d => d.key == key).ToList();
            if (defs.Count != 1) { f.Add($"[MORALE_SFX_WIRED] {key}: {defs.Count} entries, want 1"); continue; }
            if (defs[0].sfxClip == null || !defs[0].sfxPriority)
                f.Add($"[MORALE_SFX_WIRED] {key}: clip {(defs[0].sfxClip == null ? "missing" : defs[0].sfxClip.name)}, priority {defs[0].sfxPriority}");
        }
    }

    // The ProceduralImage twin of Custom/UI_AlwaysOnTop, used by HPBar_ProceduralAlwaysOnTop.mat.
    private const string HPBAR_PROCEDURAL_ON_TOP_SHADER = "UI/Procedural UI Image AlwaysOnTop";

    // Tầng 4b §4.1: every part of every HP bar draws with a ZTest Always shader. One of the three
    // images in each bar prefab was left on the default material, so a model standing in front cut
    // a piece out of the bar. Sorting order cannot fix that — it orders canvases, not 3D depth.
    // Prefabs are read from the data the game spawns from, not found by a project-wide search.
    private static void HpBarOnTop(List<string> f)
    {
        var prefabs = new List<GameObject>();
        var ops = LoadConfig<TDFlyweightOperatorDataSettings>()?.GetAllOperators();
        if (ops != null) prefabs.AddRange(ops.Select(o => o.operatorPrefab));
        var enemies = LoadConfig<TDFlyweightEnemyDataSettings>()?.GetAllEnemies();
        if (enemies != null) prefabs.AddRange(enemies.Select(e => e.prefab));
        if (prefabs.Count == 0) { f.Add("[HPBAR_ON_TOP] no operator or enemy data"); return; }

        foreach (var prefab in prefabs.Where(p => p != null).Distinct())
            foreach (var bar in prefab.GetComponentsInChildren<TDHPBarView>(true))
                foreach (var g in bar.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    string shader = g.material != null ? g.material.shader.name : "none";
                    if (shader != TDMoraleIconView.ON_TOP_SHADER && shader != HPBAR_PROCEDURAL_ON_TOP_SHADER)
                        f.Add($"[HPBAR_ON_TOP] {prefab.name}/{g.name}: {shader}");
                }
    }

    // Tầng 4b §4.2: TDEffectManager already plays these three, but the Effect Config had no entry for
    // them, so losing a life, winning and losing were silent. A life is lost in bursts (a leaking pack,
    // three at once for a death), so that one stays ordinary; the two match endings must never be dropped.
    private static void EndSfxWired(List<string> f)
    {
        var all = LoadConfig<TDEffectConfig>()?.GetAll();
        if (all == null) { f.Add("[END_SFX_WIRED] no Effect Config asset"); return; }

        foreach (var (key, priority) in new[] { (GameEventKey.LifeLost, false), (GameEventKey.Victory, true), (GameEventKey.GameOver, true) })
        {
            var defs = all.Where(d => d.key == key).ToList();
            if (defs.Count != 1) { f.Add($"[END_SFX_WIRED] {key}: {defs.Count} entries, want 1"); continue; }
            var d0 = defs[0];
            if (d0.sfxClip == null || !d0.onlySfx || d0.sfxPriority != priority)
                f.Add($"[END_SFX_WIRED] {key}: clip {(d0.sfxClip == null ? "missing" : d0.sfxClip.name)}, onlySfx {d0.onlySfx}, priority {d0.sfxPriority} (want {priority})");
        }
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
    // Polish A1: every model a match puts on screen shades with the one toon material set, so an
    // operator added with its pack shader fails here instead of on camera.
    private static void ToonUnits(List<string> f)
    {
        var units = TDToonConverter.UnitPrefabs();
        if (units.Count < 22) f.Add($"[TOON_UNITS_SOURCE] UnitPrefabs() returned {units.Count}, want >= 22 (8 operators, 6 enemies, 5 turrets, 3 bullets)");
        foreach (var p in units)
            foreach (var line in TDToonConverter.NonToon(p))
                f.Add($"[TOON_UNITS] {line}");

        var template = AssetDatabase.LoadAssetAtPath<Material>(TDToonConverter.TEMPLATE_PATH);
        if (template == null) { f.Add($"[TOON_TEMPLATE] no material at {TDToonConverter.TEMPLATE_PATH}"); return; }

        // A cut-out leaf turned opaque is a black square, and a second run must reuse what the first made.
        var probe = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "TDToonProbe", hideFlags = HideFlags.HideAndDontSave };
        probe.SetFloat("_AlphaClip", 1f);
        probe.SetFloat("_Cutoff", 0.4f);
        probe.EnableKeyword("_ALPHATEST_ON");
        string made = $"{TDToonConverter.OUTPUT_DIR}/Toon_{probe.name}.mat";
        try
        {
            var toon = TDToonConverter.ToonFor(probe, template);
            if (toon == null || toon.shader.name != TDToonConverter.TOON_SHADER)
            { f.Add($"[TOON_FOR] gave {(toon == null ? "null" : toon.shader.name)}, want {TDToonConverter.TOON_SHADER}"); return; }
            if (toon.GetFloat("_AlphaClip") != 1f || !Mathf.Approximately(toon.GetFloat("_Cutoff"), 0.4f) || !toon.IsKeywordEnabled("_ALPHATEST_ON"))
                f.Add($"[TOON_KEEPS_ALPHA_CLIP] clip {toon.GetFloat("_AlphaClip")}, cutoff {toon.GetFloat("_Cutoff")}, keyword {toon.IsKeywordEnabled("_ALPHATEST_ON")}, want 1 / 0.4 / on");
            if (TDToonConverter.ToonFor(probe, template) != toon) f.Add("[TOON_IDEMPOTENT] a second ToonFor made another material");
            if (TDToonConverter.ToonFor(toon, template) != toon) f.Add("[TOON_IDEMPOTENT] ToonFor of a toon material did not return it");
        }
        finally
        {
            AssetDatabase.DeleteAsset(made);
            Object.DestroyImmediate(probe);
        }

        var enemy = LoadConfig<TDFlyweightEnemyDataSettings>()?.GetAllEnemies().FirstOrDefault()?.prefab;
        if (enemy != null && TDToonConverter.Apply(enemy, template) != 0) f.Add($"[TOON_IDEMPOTENT] Apply changed {enemy.name}, which is already toon");

        // Sidekick keeps its colours in _ColorMap; its _MainTex is the pack's starter map.
        var sidekick = Shader.Find("Shader Graphs/Sidekick_ShaderGraph");
        if (sidekick == null) { f.Add("[TOON_ALBEDO_ORDER] Sidekick shader not found"); return; }
        var colour = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        var main = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        var mat = new Material(sidekick) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            mat.SetTexture("_ColorMap", colour);
            mat.SetTexture("_MainTex", main);
            if (TDToonConverter.AlbedoOf(mat) != colour) f.Add("[TOON_ALBEDO_ORDER] AlbedoOf a Sidekick material did not pick _ColorMap");
        }
        finally
        {
            Object.DestroyImmediate(mat);
            Object.DestroyImmediate(colour);
            Object.DestroyImmediate(main);
        }
    }

    // The map joins the same set: tiles and props, from every stage's visual config and the scene's defaults.
    private static void ToonMap(List<string> f)
    {
        var map = TDToonConverter.MapPrefabs();
        if (map.Count < 18) f.Add($"[TOON_MAP_SOURCE] MapPrefabs() returned {map.Count}, want >= 18 (12 props, 6 tiles)");
        foreach (var p in map)
            foreach (var line in TDToonConverter.NonToon(p))
                f.Add($"[TOON_MAP] {line}");

        // Cliffs blend grass over dirt by world projection; their mesh UVs all sit on one texel.
        var tri = AssetDatabase.LoadAssetAtPath<Material>("Assets/1.Assets/UnityStore Resources/Synty/PolygonGeneric/Materials/Generic_Triplanar_Grass_01.mat");
        if (tri == null || TDToonConverter.ToonFor(tri, AssetDatabase.LoadAssetAtPath<Material>(TDToonConverter.TEMPLATE_PATH)) != tri)
            f.Add("[TOON_TRIPLANAR_KEPT] Generic_Triplanar_Grass_01 gets a toon twin; it would render as one flat colour");
    }

    // The pack's ForwardLit multiplies the colour texture in at the end, so feeding it to the BRDF as well
    // squared it and dark textures rendered near black. A pack reimport would bring that back.
    private static void ToonAlbedoOnce(List<string> f)
    {
        var shader = Shader.Find(TDToonConverter.TOON_SHADER);
        string src = shader == null ? "" : System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(shader));
        if (!src.Contains("ToonBRDFData(half3(1, 1, 1),"))
            f.Add("[TOON_ALBEDO_ONCE] Toon.shader ForwardLit feeds baseColor to ToonBRDFData; albedo is applied twice");
    }

    // Units get a dark line around them; the board does not, so it stays calm under them.
    private static void Outline(List<string> f)
    {
        int layer = LayerMask.NameToLayer(TDToonConverter.OUTLINE_LAYER);
        if (layer < 0) f.Add($"[OUTLINE_PROFILE] layer {TDToonConverter.OUTLINE_LAYER} missing");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/ToonOutlineProfile.asset");
        if (profile == null || !profile.TryGet(out OutlineSettings o)) f.Add("[OUTLINE_PROFILE] ToonOutlineProfile has no OutlineSettings");
        else
        {
            if (!o.active || o.outlineType.value == OutlineType.NoOutlines || o.objectMask.value.value != 1 << layer)
                f.Add($"[OUTLINE_PROFILE] active {o.active}, type {o.outlineType.value}, mask {o.objectMask.value.value}");
            // Composited after transparents, a unit's line is painted over the world-space badge, label and HP bar in front of it.
            if (o.renderPassEvent.value.Convert() > UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingTransparents)
                f.Add($"[OUTLINE_UNDER_UI] outlines composite at {o.renderPassEvent.value.Convert()}, over the transparent badges in front of units");
        }

        foreach (var p in TDToonConverter.UnitPrefabs())
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                if ((r is MeshRenderer || r is SkinnedMeshRenderer) && !r.sharedMaterials.All(TDToonConverter.IsExempt) && r.gameObject.layer != layer)
                    f.Add($"[OUTLINE_UNITS] {p.name}/{r.name} on layer {r.gameObject.layer}");
        if (layer < 0) return;

        // The gameplay camera ticks its layers one by one (407 = 0,1,2,4,7,8); a unit layer it leaves out hides every unit.
        var cam = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText("Assets/3.Scenes/DTGamePlay.unity"),
            @"m_Name: Main Camera[\s\S]*?m_CullingMask:\s*serializedVersion: 2\s*m_Bits: (\d+)");
        if (!cam.Success || (long.Parse(cam.Groups[1].Value) & (1L << layer)) == 0)
            f.Add($"[OUTLINE_CAMERA] DTGamePlay Main Camera culling mask {(cam.Success ? cam.Groups[1].Value : "not found")} leaves out layer {layer}");

        foreach (var p in TDToonConverter.MapPrefabs())
            foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                if (r.gameObject.layer == layer) f.Add($"[OUTLINE_NOT_MAP] {p.name}/{r.name}");
    }

    // Spec §3: the material values that put each board role on its on-screen target under DTGamePlay's light
    // (calibrated 2026-10-23, measurements/raw/2026-10-23-a2/calib-final.png). Road and grass need more gain
    // than a base colour can carry, so the rest rides on the HDR light tint.
    private static readonly (string path, Color baseColor, Color lightTint, bool flat)[] k_BoardPalette =
    {
        (TDToonConverter.OUTPUT_DIR + "/Toon_TowerZone.mat", new Color(0.379f, 0.469f, 0.472f), Color.white, true),
        (TDToonConverter.OUTPUT_DIR + "/Toon_Generic_Road.mat", new Color(0.777f, 0.920f, 1.000f), new Color(1.774f, 1.774f, 1.774f), false),
        (TDToonConverter.OUTPUT_DIR + "/Toon_Generic_Grass.mat", new Color(0.421f, 0.433f, 1.000f), new Color(2.136f, 2.136f, 2.136f), false),
        (TDToonConverter.OUTPUT_DIR + "/Toon_Generic_01_A.mat", new Color(0.712f, 0.799f, 0.799f), Color.white, false),
        (TDToonConverter.OUTPUT_DIR + "/Toon_Floor.mat", new Color(0.372f, 0.456f, 0.499f), Color.white, false),
    };
    private static readonly Color k_BoardShadowTint = new Color(0.22f, 0.27f, 0.36f);
    private const string SIDEWALK_PREFAB = "Assets/1.Assets/UnityStore Resources/Synty/PolygonGeneric/Prefabs/Environment/SM_Gen_Env_Sidewalk_01.prefab";

    private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) <= 0.01f && Mathf.Abs(a.g - b.g) <= 0.01f && Mathf.Abs(a.b - b.b) <= 0.01f;

    private static void BoardPalette(List<string> f)
    {
        foreach (var (path, baseColor, lightTint, flat) in k_BoardPalette)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { f.Add($"[A2_PALETTE] {System.IO.Path.GetFileName(path)} missing"); continue; }
            if (!Near(m.GetColor("_BaseColor"), baseColor)) f.Add($"[A2_PALETTE] {m.name} base {m.GetColor("_BaseColor")} want {baseColor}");
            if (!Near(m.GetColor("_LightTint"), lightTint)) f.Add($"[A2_PALETTE] {m.name} light {m.GetColor("_LightTint")} want {lightTint}");
            if (!Near(m.GetColor("_ShadowTint"), k_BoardShadowTint)) f.Add($"[A2_PALETTE] {m.name} shadow {m.GetColor("_ShadowTint")} want {k_BoardShadowTint}");
            var map = m.GetTexture("_BaseMap");
            if (flat != (map == null)) f.Add($"[A2_PALETTE] {m.name} flat {flat} but base map {(map == null ? "none" : map.name)}");
        }
    }

    // The floor between tiles is CityGround's sidewalk tiles. They shared the road twin, so road and floor
    // could not take different colours; the floor gets its own.
    private static void BoardFloor(List<string> f)
    {
        if (!AssetDatabase.GetDependencies("Assets/3.Scenes/DTGamePlay.unity", false).Contains(SIDEWALK_PREFAB))
            f.Add("[A2_FLOOR] DTGamePlay floor is no longer SM_Gen_Env_Sidewalk_01");
        var sidewalk = AssetDatabase.LoadAssetAtPath<GameObject>(SIDEWALK_PREFAB);
        var mats = sidewalk == null ? new Material[0] : sidewalk.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
        if (!mats.Any(m => m != null && m.name == "Toon_Floor")) f.Add("[A2_FLOOR] SM_Gen_Env_Sidewalk_01 does not use Toon_Floor");
        if (mats.Any(m => m != null && m.name == "Toon_Generic_Road")) f.Add("[A2_FLOOR] SM_Gen_Env_Sidewalk_01 still uses Toon_Generic_Road");
    }

    // Spec §4: one green for every deploy tile, strong enough that a lit TowerZone under it still gains 40 green
    // (measurements/raw/2026-10-23-a2/highlight-calib.png), and the range orange measured from Arknights.
    private static readonly Color k_DeployGreen = new Color(0.369f, 1.000f, 0.495f, 0.95f);
    private static readonly Color k_RangeOrange = new Color(0xCF / 255f, 0x71 / 255f, 0x27 / 255f, 0.85f);

    private static void HighlightColors(List<string> f)
    {
        foreach (var (name, want) in new[] { ("TowerHighlight", k_DeployGreen), ("RangeHighlight", k_RangeOrange) })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/1.Assets/Resources/Materials/{name}.mat");
            var c = m == null ? Color.clear : m.GetColor("_BaseColor");
            if (m == null || !Near(c, want) || Mathf.Abs(c.a - want.a) > 0.01f) f.Add($"[A2_HIGHLIGHT] {name} {c} want {want}");
        }
    }

    // TowerZone and obstacle tiles are 0.5-tall blocks: an overlay laid flat at one height vanished into
    // every block it crossed. The amp ring reads its heights from the grid, as the range tiles do.
    private static void OverlaysClearBlocks(List<string> f)
    {
        const float BLOCK_TOP = 0.5f;
        var prev = TDGridMainModel.api;
        var owner = new GameObject("OverlayProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            TDGridMainModel.Initialize(new Vector3(40f, 0f, 40f), Vector3.zero);
            var grid = TDGridMainModel.api;
            // CreateGrid ends by completing a one-shot task; a second run in the same domain throws there,
            // after the grid is built.
            try { grid.CreateGrid(); } catch (System.InvalidOperationException) { }

            owner.transform.position = grid.CellToWorld(new Vector2Int(10, 10));
            var centre = owner.transform.position;
            var obstacle = grid.WorldToCell(centre + new Vector3(TDAmpRingView.WORLD_RADIUS, 0f, 0f));
            var zone = grid.WorldToCell(centre + new Vector3(0f, 0f, TDAmpRingView.WORLD_RADIUS));
            var low = grid.WorldToCell(centre - new Vector3(TDAmpRingView.WORLD_RADIUS, 0f, 0f));
            grid.RegisterRaisedCell(obstacle);
            grid.RegisterTowerZoneCell(zone);

            foreach (var (cell, raised) in new[] { (obstacle, true), (zone, true), (low, false) })
            {
                float y = grid.OverlayY(cell);
                if (raised ? y <= BLOCK_TOP : Mathf.Abs(y - TDConstant.CONFIG_RANGE_HIGHLIGHT_Y) > 0.001f)
                    f.Add($"[OVERLAY_ON_BLOCKS] range tile over {(raised ? "a block" : "a low cell")} {cell} at y {y:F2}");
            }

            var pts = TDAmpRingView.Attach(owner.transform).WorldPoints();
            int n = TDAmpRingView.SEGMENTS;
            foreach (var (i, raised) in new[] { (0, true), (n / 4, true), (n / 2, false) })
                if (raised ? pts[i].y <= BLOCK_TOP : Mathf.Abs(pts[i].y - TDConstant.CONFIG_RANGE_HIGHLIGHT_Y) > 0.001f)
                    f.Add($"[OVERLAY_ON_BLOCKS] amp ring point {i} over {(raised ? "a block" : "a low cell")} at y {pts[i].y:F2}");
        }
        finally
        {
            Object.DestroyImmediate(owner);
            typeof(TDGridMainModel).GetProperty(nameof(TDGridMainModel.api)).GetSetMethod(true).Invoke(null, new object[] { prev });
        }
    }

    private static T LoadConfig<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}

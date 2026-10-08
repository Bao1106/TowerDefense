using System.Collections.Generic;
using TDEnums;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Step 2.3 — assertions for the morale model, same shape as TDBalanceValidator.
///
/// Written the same day as the model and run before the step was ticked. The earlier
/// validator sat in the repo marked done for weeks and failed on the first press; the
/// lesson taken from that is not "write more tests", it is that **an unrun check is not
/// a check**.
///
/// What makes this possible at all is that TDOperatorMorale is plain C#: no scene, no
/// play mode, no prefab, no singleton. Every rule below is exercised on a bare object.
/// </summary>
public static class TDMoraleValidator
{
    private const int MAX_REPORTED = 20;

    [MenuItem("Tools/TD/Validate Stress Model")]
    public static void RunFromMenu()
    {
        var failures = Run();

        if (failures.Count == 0)
        {
            Debug.Log("<color=#A1CD3A><b>[TDMoraleValidator] PASS</b></color> — 0 failures.");
            return;
        }

        int shown = Mathf.Min(failures.Count, MAX_REPORTED);
        for (int i = 0; i < shown; i++) Debug.LogError($"[TDMoraleValidator] {failures[i]}");
        if (failures.Count > shown)
            Debug.LogError($"[TDMoraleValidator] …and {failures.Count - shown} more.");
    }

    public static List<string> Run()
    {
        var f = new List<string>();

        Rates(f);
        Multipliers(f);
        Recovery(f);
        SkillPoints(f);
        RetreatIsNotAReset(f);
        BrokenState(f);
        BreakEdge(f);
        Leaks(f);
        LeakSplit(f);
        Amplifier(f);
        DeployCap(f);
        Targets(f);
        Pearson(f);
        Resolve(f);
        FocusScale(f);

        return f;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TDMoraleContext Ctx(bool engaged, int calmAllies = 0, float aura = 0f, float sinceHit = 0f)
        => new TDMoraleContext
        {
            engaged = engaged,
            calmAlliesAdjacent = calmAllies,
            auraRate = aura,
            secondsSinceHit = sinceHit,
        };

    private static TDOperatorMorale At(float value)
    {
        var m = new TDOperatorMorale();
        m.AddSpike(value);
        return m;
    }

    private static void Near(List<string> f, string tag, float got, float want, float tol = 0.01f)
    {
        if (Mathf.Abs(got - want) > tol)
            f.Add($"[{tag}] got {got:F3}, want {want:F3}");
    }

    // ── A — rates: no clock any more — only the aura accrues, and idling recovers ──

    private static void Rates(List<string> f)
    {
        Near(f, "ENGAGED_IS_FLAT", new TDOperatorMorale().NetRate(Ctx(true)), 0f);
        Near(f, "IDLE_RELIEF", new TDOperatorMorale().NetRate(Ctx(false)), -TDConstant.STRESS_IDLE_RELIEF);
        Near(f, "N4_AURA", new TDOperatorMorale().NetRate(Ctx(true, aura: TDConstant.STRESS_AURA_BOSS)), TDConstant.STRESS_AURA_BOSS);
        Near(f, "N4_AURA_MULT", At(70f).NetRate(Ctx(true, aura: TDConstant.STRESS_AURA_BOSS)),
             TDConstant.STRESS_AURA_BOSS * TDConstant.STRESS_MULT_STRESSED);
        Near(f, "ALLY_NO_STRESS_EFFECT", new TDOperatorMorale().NetRate(Ctx(true, calmAllies: 5)), 0f);
    }

    // ── B — multipliers apply to the aura and to leaks, never to spikes ─────

    private static void Multipliers(List<string> f)
    {
        // The per-band leak price is LEAK_CALM / STEADY / STRESSED in Leaks().

        // Band edges: 33 is still Calm, 34 is Steady; 66 Steady, 67 Stressed; 100 Broken.
        if (TDOperatorMorale.StateOf(33f) != MoraleState.Calm) f.Add("[BAND] 33 is not Calm");
        if (TDOperatorMorale.StateOf(34f) != MoraleState.Steady) f.Add("[BAND] 34 is not Steady");
        if (TDOperatorMorale.StateOf(66f) != MoraleState.Steady) f.Add("[BAND] 66 is not Steady");
        if (TDOperatorMorale.StateOf(67f) != MoraleState.Stressed) f.Add("[BAND] 67 is not Stressed");
        if (TDOperatorMorale.StateOf(100f) != MoraleState.Broken) f.Add("[BAND] 100 is not Broken");

        // ⭐ N3 must NOT be multiplied. At Stressed a doubled 30-point jolt would kill
        // outright and turn every cascade into an automatic wipe.
        var hot = At(60f);
        hot.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        Near(f, "SPIKE_UNMULTIPLIED", hot.Value, 60f + TDConstant.STRESS_ALLY_BREAK_SPIKE);
    }

    // ── C — recovery, and its ceiling ───────────────────────────────────────

    private static void Recovery(List<string> f)
    {
        // Wave-clear relief is a flat instant subtraction.
        var cleared = At(50f);
        cleared.OnWaveCleared();
        Near(f, "WAVE_CLEAR", cleared.Value, 50f - TDConstant.STRESS_WAVE_CLEAR_RELIEF);
    }

    // ── SP (§06 · 3.2) ──────────────────────────────────────────────────────

    private static void SkillPoints(List<string> f)
    {
        var sp = new TDOperatorSp();
        Near(f, "SP_INITIAL", sp.Current, TDConstant.SP_INITIAL);

        // ⭐ The number that actually matters is TIME TO FIRST RESCUE, not the starting value.
        // A real 85-second match had every reinforcement arrive too late to ever afford one, so
        // this is the assertion that would notice if a tweak to the rate or the cost quietly put
        // the mechanic back out of reach.
        float t = 0f;
        while (!sp.CanAfford(TDConstant.RESCUE_SP_COST) && t < 300f) { sp.Tick(0.1f); t += 0.1f; }
        Near(f, "SP_FIRST_RESCUE_SECONDS", t, 30f, 0.2f);

        // ⭐ Redeploying must not re-grant the initial SP, or deploy-and-retreat becomes the
        // cheapest way to bank rescues. The roster returns the same object, so spending must stick.
        //
        // Asserted as a DELTA, not against a literal 0: the charge loop above advances in 0.1
        // slices and therefore stops at 50.1, not 50. The first version of this check wanted 0.0
        // and failed on the leftover — the code was right and the test was wrong.
        float before = sp.Current;
        sp.Spend(TDConstant.RESCUE_SP_COST);
        Near(f, "SP_SPEND_STICKS", sp.Current, before - TDConstant.RESCUE_SP_COST);
        if (sp.CanAfford(TDConstant.RESCUE_SP_COST))
            f.Add("[SP_SPEND_STICKS] a second rescue was affordable immediately after the first");

        // Charge stops at a skill's cost, not a rescue's — banking two rescues is legitimate.
        for (int i = 0; i < 3000; i++) sp.Tick(0.1f);
        Near(f, "SP_CAP", sp.Current, TDConstant.SP_MAX);
    }

    // ── D — the single rule the whole system's lesson rests on ──────────────

    private static void RetreatIsNotAReset(List<string> f)
    {
        // "Retreating early is a reset, retreating late is a postponement."
        var early = At(TDConstant.STRESS_STEADY_MAX + 4f); // 70, just into Stressed
        early.OnRetreat();
        Near(f, "RETREAT_70_CLEAN", early.Value, 0f);

        var late = At(90f);
        late.OnRetreat();
        Near(f, "RETREAT_90_KEEPS_20", late.Value, 90f - TDConstant.STRESS_RETREAT_RELIEF);

        if (late.RetreatCooldown <= 0f)
            f.Add("[RETREAT_COOLDOWN] retreat did not start the redeploy countdown");

        // ⭐ Death must NOT clear stress, or "let them die and redeploy" becomes optimal
        // and the system teaches the opposite of what it means to.
        var dead = At(80f);
        dead.OnDeath();
        Near(f, "DEATH_KEEPS_STRESS", dead.Value, 80f);
        if (dead.Setbacks != 1) f.Add("[SETBACK_COUNT] setback not recorded");

        // ⭐ Death must be worse than retreat on BOTH axes, points AND tempo. Keeping the stress
        // covers only the first, and only for an operator who died stressed — one killed early
        // and calm paid nothing. With no cooldown they walked straight back on while the
        // retreated operator was still waiting, which made dying the FASTER exit: the same
        // loophole DEATH_KEEPS_STRESS forbids, reopened in the time dimension.
        if (dead.RetreatCooldown < TDConstant.STRESS_RETREAT_COUNTDOWN)
            f.Add($"[DEATH_COSTS_MORE] death countdown {dead.RetreatCooldown}s is shorter than " +
                  $"retreat's {TDConstant.STRESS_RETREAT_COUNTDOWN}s — dying is the faster way off the field");

        // ⭐ Death ends the COLLAPSE but not the stress. Without this the operator redeployed
        // still flagged collapsed — ~84 after the cooldown is above the latch's exit line of 66 —
        // and stood there paid-for, unable to block or swing, taking x3 from the first enemy past.
        var brokeThenDied = At(TDConstant.STRESS_MAX);
        brokeThenDied.OnDeath();
        Near(f, "DEATH_KEEPS_STRESS_TOO", brokeThenDied.Value, TDConstant.STRESS_MAX);
        if (brokeThenDied.IsBroken)
            f.Add("[DEATH_ENDS_COLLAPSE] redeploying after death brings the operator back collapsed");

        // ⭐ One incident, one setback. A break and the death that follows it used to count twice,
        // which took a Knight from 26.8% Resolve to 6.8% off a single fall — LAST STAND stops
        // being offered after one bad moment, and a button that never appears cannot be the
        // deliberate choice §06 builds it to be.
        var chain = At(TDConstant.STRESS_MAX); // the break itself is the first setback
        chain.OnDeath();
        if (chain.Setbacks != 1)
            f.Add($"[SETBACK_ONE_PER_INCIDENT] break-then-death counted {chain.Setbacks}, want 1");

        // ...but a death with nothing before it IS the first incident and must still count.
        var sudden = At(40f);
        sudden.OnDeath();
        if (sudden.Setbacks != 1)
            f.Add($"[SETBACK_ONE_PER_INCIDENT] a plain death counted {sudden.Setbacks}, want 1");

        // And a calm operator who dies must still pay, or early deaths are free.
        var calmDead = new TDOperatorMorale();
        calmDead.OnDeath();
        if (calmDead.RetreatCooldown <= 0f)
            f.Add("[DEATH_COSTS_MORE] an operator killed at zero stress paid nothing");
    }

    // ── E — broken ──────────────────────────────────────────────────────────

    private static void BrokenState(List<string> f)
    {
        var b = At(TDConstant.STRESS_MAX);

        if (!b.IsBroken) f.Add("[BROKEN_FLAG] 100 is not flagged broken");

        // Under fire a broken operator is stuck — that is the dead end Rescue exists for.
        Near(f, "BROKEN_UNDER_FIRE", b.NetRate(Ctx(true, sinceHit: 1f)), 0f);

        Near(f, "BROKEN_LEFT_ALONE",
             b.NetRate(Ctx(false, sinceHit: TDConstant.STRESS_BROKEN_CALM_SECONDS + 1f)),
             -TDConstant.STRESS_BROKEN_RELIEF);

        // ⭐ Collapse LATCHES. As a bare `m_Value >= 100` test the state could not survive its own
        // recovery: the first tick after the calm window put the value at 99.98 and the operator
        // stood back up, then one enemy in the zone re-broke them half a second later. Every
        // consequence of collapse flickered with it, and §05's "100 -> 66 in 34 quiet seconds"
        // described a state that never lasted 34 seconds.
        var latched = At(TDConstant.STRESS_MAX);
        latched.Tick(1f, Ctx(false, sinceHit: TDConstant.STRESS_BROKEN_CALM_SECONDS + 1f));
        if (!latched.IsBroken)
            f.Add($"[BROKEN_LATCH] one tick of recovery ({latched.Value:F1}) already lifted the collapse");

        // ...and releases only at the Steady line, which is what makes Rescue (instantly to 50)
        // worth 50 SP against simply waiting (66 after 39 seconds of standing there useless).
        var waited = At(TDConstant.STRESS_MAX);
        for (int i = 0; i < 400 && waited.IsBroken; i++)
            waited.Tick(0.1f, Ctx(false, sinceHit: TDConstant.STRESS_BROKEN_CALM_SECONDS + 1f));
        Near(f, "BROKEN_EXIT_AT_STEADY", waited.Value, TDConstant.STRESS_STEADY_MAX, 0.2f);

        // ⭐ The wave-clear valve must reach the operators who HELD, and must not reach the ones
        // who collapsed. The first half is what makes it a valve at all: it used to be withheld
        // from the entire squad the moment anybody broke, so §06's "van chống chết chùm" was
        // guaranteed shut during precisely the cascade it is named after, and the third operator
        // tipped over because the first two had.
        var held = At(70f);
        held.OnWaveCleared();
        Near(f, "WAVE_RELIEF_REACHES_SURVIVORS", held.Value, 70f - TDConstant.STRESS_WAVE_CLEAR_RELIEF);

        var collapsed = At(TDConstant.STRESS_MAX);
        collapsed.OnWaveCleared();
        Near(f, "WAVE_RELIEF_SKIPS_BROKEN", collapsed.Value, TDConstant.STRESS_MAX);
        if (!collapsed.IsBroken)
            f.Add("[WAVE_RELIEF_SKIPS_BROKEN] a wave ending lifted a collapse — only Rescue may do that");

        // Rescue lifts them out of the broken band in one step.
        var rescued = At(TDConstant.STRESS_MAX);
        rescued.OnRescue();
        if (rescued.IsBroken) f.Add("[RESCUE] rescue left the operator broken");
        Near(f, "RESCUE_AMOUNT", rescued.Value,
             TDConstant.STRESS_MAX - TDConstant.STRESS_RESCUE_RELIEF);

        // Value can never leave [0, 100] whatever the caller does.
        var over = At(500f);
        Near(f, "CLAMP_HIGH", over.Value, TDConstant.STRESS_MAX);
        var under = new TDOperatorMorale();
        under.OnRetreat();
        Near(f, "CLAMP_LOW", under.Value, 0f);
    }

    // ── J — a break is an EVENT, whoever pushed the value over ──────────────
    //
    // The view used to detect the break by reading IsBroken before Tick and comparing after.
    // An ally's N3 spike latches the neighbour OUTSIDE that window, so a break caused by a
    // spike never fired its own spike, setback or release — every cascade stopped at link two.

    private static void BreakEdge(List<string> f)
    {
        var spiked = At(80f);
        spiked.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        if (!spiked.ConsumeBreak()) f.Add("[EDGE_FROM_SPIKE] a spike-induced break raised no edge");
        if (spiked.ConsumeBreak()) f.Add("[EDGE_ONCE] the same break was reported twice");
        if (spiked.Setbacks != 1) f.Add($"[EDGE_SETBACK] spike break counted {spiked.Setbacks}, want 1");

        spiked.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE); // already latched
        if (spiked.ConsumeBreak()) f.Add("[EDGE_WHILE_LATCHED] a spike on a broken operator raised a second edge");

        var ticked = new TDOperatorMorale();
        // The aura is the only time-based source left, so it is what drives a tick-induced break.
        for (int i = 0; i < 2000 && !ticked.IsBroken; i++) ticked.Tick(0.05f, Ctx(true, aura: TDConstant.STRESS_AURA_BOSS));
        if (!ticked.ConsumeBreak()) f.Add("[EDGE_FROM_TICK] a tick-induced break raised no edge");

        // ⭐ An edge nobody handled must not outlive the state it announces — otherwise it fires
        // on the next deploy, spiking and releasing around an operator who is perfectly fine.
        var rescued = At(TDConstant.STRESS_MAX);
        rescued.OnRescue();
        if (rescued.ConsumeBreak()) f.Add("[EDGE_CLEARED_BY_RESCUE] an unconsumed edge survived the rescue");

        var died = At(TDConstant.STRESS_MAX);
        died.OnDeath();
        if (died.ConsumeBreak()) f.Add("[EDGE_CLEARED_BY_DEATH] an unconsumed edge would fire on redeploy");

        var again = At(TDConstant.STRESS_MAX);
        again.ConsumeBreak();
        again.OnRescue();
        again.AddSpike(TDConstant.STRESS_MAX);
        if (!again.ConsumeBreak() || again.Setbacks != 2)
            f.Add($"[EDGE_REBREAK] second break after rescue: setbacks {again.Setbacks}, want 2");
    }

    // ── H — focus slow-mo: one formula, pause always wins ───────────────────
    //
    // Not a morale rule, but it rides here because this is the one runnable check for gameplay
    // rules — and the slow-down exists for morale: rotating a squad in real time on a phone,
    // with a 2.4-second Stressed window under siege, is not a decision anyone can make at 1x.

    private static void FocusScale(List<string> f)
    {
        Near(f, "FOCUS_NORMAL", TDSpeedControl.ScaleFor(false, TDConstant.SPEED_NORMAL, true), TDConstant.SPEED_FOCUS);
        Near(f, "FOCUS_FAST", TDSpeedControl.ScaleFor(false, TDConstant.SPEED_FAST, true),
             TDConstant.SPEED_FAST * TDConstant.SPEED_FOCUS);
        Near(f, "PAUSE_WINS", TDSpeedControl.ScaleFor(true, TDConstant.SPEED_FAST, true), 0f);
        Near(f, "NO_FOCUS", TDSpeedControl.ScaleFor(false, TDConstant.SPEED_FAST, false), TDConstant.SPEED_FAST);
    }

    // ── F — leaks: the load source, priced per event (spec §5.2) ────────────

    private static void Leaks(List<string> f)
    {
        // One leak alone = 10 / 15 / 20 by band.
        Near(f, "LEAK_CALM", At(10f).OnLeak(1f, 1f), 10f);
        Near(f, "LEAK_STEADY", At(50f).OnLeak(1f, 1f), 15f);
        Near(f, "LEAK_STRESSED", At(70f).OnLeak(1f, 1f), 20f);
        Near(f, "LEAK_AMPLIFIED", At(0f).OnLeak(1f, TDConstant.HERALD_LEAK_MULT), 20f);
        var broken = At(TDConstant.STRESS_MAX); int sb = broken.Setbacks;
        Near(f, "LEAK_IGNORED_WHEN_BROKEN", broken.OnLeak(1f, 1f), 0f);
        if (broken.Setbacks != sb) f.Add("[LEAK_IGNORED_WHEN_BROKEN] a leak on a collapsed operator counted a setback");

        // Spec §5.2 table: 8 / 11 / 25 leaks from zero to collapse.
        foreach (var (tag, share, want) in new[] { ("LEAKS_ALONE", 1f, 8), ("LEAKS_SUPPORTED", 0.7f, 11), ("LEAKS_RANGED", 0.3f, 25) })
        {
            var m = new TDOperatorMorale(); int n = 0;
            while (!m.IsBroken && n < 100) { m.OnLeak(share, 1f); n++; }
            if (n != want) f.Add($"[{tag}] {n} leaks to collapse, want {want}");
            if (new TDOperatorMorale().LeaksToBreak(share) != want) f.Add($"[{tag}_PROJECTION] LeaksToBreak disagrees with the walk");
            if (!m.ConsumeBreak()) f.Add($"[{tag}_EDGE] a leak-induced collapse raised no edge");
        }
        if (new TDOperatorMorale().LeaksToBreak(0f) != int.MaxValue) f.Add("[LEAKS_NO_SHARE] share 0 reported a finite count");
    }

    // Spec §5.2 split table: melee 70%, covering ranged share 30%; broken melee hands it all over.
    private static void LeakSplit(List<string> f)
    {
        void Is(string tag, (float m, float r) got, float m, float r)
        { Near(f, tag + "_MELEE", got.m, m); Near(f, tag + "_RANGED", got.r, r); }

        Is("SPLIT_ALONE",        TDLeakShare.Split(true, 0),  1f,   0f);
        Is("SPLIT_SUPPORTED",    TDLeakShare.Split(true, 1),  0.7f, 0.3f);
        Is("SPLIT_TWO_RANGED",   TDLeakShare.Split(true, 2),  0.7f, 0.15f);
        Is("SPLIT_MELEE_BROKEN", TDLeakShare.Split(false, 2), 0f,   0.5f);
        Is("SPLIT_NOBODY",       TDLeakShare.Split(false, 0), 0f,   0f);

        for (int k = 0; k <= 4; k++)
        {
            var s = TDLeakShare.Split(true, k);
            Near(f, $"SPLIT_SUMS_{k}", s.melee + k * s.eachRanged, 1f);
        }
    }

    // Spec §5.1: one hard per-level cap shared by melee, ranged and turrets.
    private static void DeployCap(List<string> f)
    {
        var cap = new TDDeployCap();
        cap.Initialize(2);
        if (cap.OnField != 0 || cap.IsFull) f.Add("[CAP_STARTS_EMPTY]");
        cap.OnUnitPlaced(); cap.OnUnitPlaced();
        if (!cap.IsFull) f.Add("[CAP_FULL_AT_LIMIT] 2/2 not full");
        cap.OnUnitRemoved();
        if (cap.IsFull || cap.OnField != 1) f.Add("[CAP_FREES] removing one did not free a slot");
        cap.OnUnitRemoved(); cap.OnUnitRemoved();
        if (cap.OnField != 0) f.Add($"[CAP_NEVER_NEGATIVE] OnField {cap.OnField}");
        cap.Initialize(0);
        if (cap.Limit != TDConstant.CONFIG_MAX_SLOTS) f.Add("[CAP_BAD_LIMIT] limit 0 was accepted");
        cap.OnUnitPlaced(); cap.Initialize(3);
        if (cap.OnField != 0) f.Add("[CAP_RESET_ON_INIT] a new match inherited units");
    }

    // Spec §5.5: a Herald within HERALD_RADIUS cells (Euclid) of a leak doubles it; two do not make it ×4.
    private static void Amplifier(List<string> f)
    {
        var o = new Vector2Int(0, 0);
        Near(f, "AMP_NONE", TDOperatorRegistry.LeakAmplifier(o, new Vector2Int[0]), 1f);
        Near(f, "AMP_INSIDE", TDOperatorRegistry.LeakAmplifier(o, new[] { new Vector2Int(4, 0) }), TDConstant.HERALD_LEAK_MULT);
        Near(f, "AMP_OUTSIDE", TDOperatorRegistry.LeakAmplifier(o, new[] { new Vector2Int(4, 1) }), 1f); // √17 > 4
        Near(f, "AMP_NO_STACK", TDOperatorRegistry.LeakAmplifier(o, new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) }), TDConstant.HERALD_LEAK_MULT);
    }

    // Spec §7.2: the HP ↔ stress correlation the probe reports per stint (target r < 0.5).
    private static void Pearson(List<string> f)
    {
        Near(f, "PEARSON_PERFECT", TDPressureProbe.Pearson(new[] { 1f, 2f, 3f }, new[] { 2f, 4f, 6f }), 1f, 0.001f);
        Near(f, "PEARSON_INVERSE", TDPressureProbe.Pearson(new[] { 1f, 2f, 3f }, new[] { 3f, 2f, 1f }), -1f, 0.001f);
        if (!float.IsNaN(TDPressureProbe.Pearson(new[] { 1f }, new[] { 1f }))) f.Add("[PEARSON_N1] n<2 gave a number");
        if (!float.IsNaN(TDPressureProbe.Pearson(new[] { 1f, 2f }, new[] { 5f, 5f }))) f.Add("[PEARSON_FLAT] zero variance gave a number");

        // Melee stints only: ranged never lose HP, so mixing them in measured front line vs back
        // line, not HP vs stress (round 1: all stints 0.60–0.77, melee pooled 0.48).
        var stints = new[]
        {
            new TDPressureProbe.Stint { melee = true, seconds = 10f, hpLost = 100f, stressGained = 40f },
            new TDPressureProbe.Stint { melee = true, seconds = 10f, hpLost = 200f, stressGained = 30f },
            new TDPressureProbe.Stint { melee = true, seconds = 10f, hpLost = 300f, stressGained = 20f },
            new TDPressureProbe.Stint { melee = true, seconds = 2f, hpLost = 900f, stressGained = 90f }, // < 5 s: noise
            new TDPressureProbe.Stint { melee = false, seconds = 10f, hpLost = 0f, stressGained = 5f },
            new TDPressureProbe.Stint { melee = false, seconds = 10f, hpLost = 0f, stressGained = 6f },
        };
        float r = TDPressureProbe.MeleeR(stints);
        if (!(Mathf.Abs(r + 1f) <= 0.001f)) f.Add($"[STINT_R_MELEE_ONLY] got {r:F3}, want -1 (ranged or short stints counted)");
    }

    // Spec §5.6: blockCount is how many a melee HOLDS, attackType is how many it STRIKES.
    private static void Targets(List<string> f)
    {
        var two = new List<string> { "a", "b" };
        var none = new List<string>();
        Seq(f, "TARGETS_SINGLE_BLOCKED", PathCellOperatorBehavior.SelectTargets(two, "c", AttackType.Single), "a");
        Seq(f, "TARGETS_MULTI_BLOCKED",  PathCellOperatorBehavior.SelectTargets(two, "c", AttackType.Multiple), "a", "b");
        Seq(f, "TARGETS_FREE_IN_RANGE",  PathCellOperatorBehavior.SelectTargets(none, "c", AttackType.Multiple), "c");
        Seq(f, "TARGETS_NONE",           PathCellOperatorBehavior.SelectTargets(none, null, AttackType.Single));
    }

    private static void Seq(List<string> f, string tag, List<string> got, params string[] want)
    {
        string g = string.Join(",", got), w = string.Join(",", want);
        if (g != w) f.Add($"[{tag}] got [{g}] want [{w}]");
    }

    // ── G — Resolve is derived, and lands where §07 says ────────────────────

    // The table §07 publishes. Checked against the LIVE config, so this fails the day
    // somebody retunes an operator's hp or damage — which is the point: Resolve is derived,
    // so a stat change is a Resolve change and the doc must not quietly go stale.
    private static readonly (string name, float resolve)[] k_ResolveTable =
    {
        // 2026-10-07: retuned to spec 2026-10-06 §5.6 — every operator and turret given a role
        // (Ace sweeps 3 blocked, Moon splashes, Layla is the cheap opener, Tart the sponge).
        // Resolve is DERIVED, so the retune moved every row; this table failing on a config
        // edit is the feature — the numbers may not drift away from the design doc quietly.
        ("Tart", 40.7f), ("Defender", 38.0f), ("Ace", 34.8f), ("Knight", 28.5f),
        ("Striker", 24.7f), ("Layla", 23.2f), ("Moon", 18.4f), ("Ginger", 13.4f),
    };

    /// <summary>
    /// Loads the operator config straight from the asset database.
    ///
    /// NOT via TDFlyweightOperatorDataSettings.api — that routes through TDResourceObject,
    /// a MonoBehaviour whose list is wired up in a scene, so it returns null outside play
    /// mode. This is the config-layer coupling already recorded under step 0.4 ("all six SO
    /// configs call TDResourceObject"), meeting us in practice: the morale MODEL is pure and
    /// tests anywhere, and the only thing that needed a workaround was reading the numbers.
    /// </summary>
    private static TDFlyweightOperatorDataSettings LoadOperatorConfig()
    {
        var guids = AssetDatabase.FindAssets("t:TDFlyweightOperatorDataSettings");
        if (guids.Length == 0) return null;

        return AssetDatabase.LoadAssetAtPath<TDFlyweightOperatorDataSettings>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void Resolve(List<string> f)
    {
        var settings = LoadOperatorConfig();
        var roster = settings?.GetAllOperators();
        if (roster == null || roster.Count == 0)
        {
            f.Add("[RESOLVE_CONFIG] operator config not loadable");
            return;
        }

        // ⭐ Identity must be unique — operatorName is what this validator, the logs and the
        // Resolve table all address an operator by. OperatorType deliberately is NOT unique
        // (Striker, Ace and Layla share the "melee reach" archetype), which is exactly why
        // nothing may resolve stats from it. That door is shut in code: GetData(OperatorType)
        // is deleted and TDTowerSlotInfo carries the row. This guards the key that replaced it.
        for (int i = 0; i < roster.Count; i++)
            for (int j = i + 1; j < roster.Count; j++)
                if (roster[i].operatorName == roster[j].operatorName)
                    f.Add($"[UNIQUE_OPERATOR_IDENTITY] two roster rows both named '{roster[i].operatorName}'");

        // ⭐ Archetype still decides ONE thing — deployZone — so it has to agree with the row's
        // own numbers. A melee operator typed as Mage would deploy onto tower zones and block
        // nothing, and no other check would notice.
        foreach (var op in roster)
        {
            bool ranged = op.deployZone == DeployZone.TowerZone;
            if (ranged != (op.blockCount == 0))
                f.Add($"[ARCHETYPE_ZONE] {op.operatorName} is {op.operatorType} → {op.deployZone}, " +
                      $"but blockCount is {op.blockCount}");
        }

        foreach (var (name, want) in k_ResolveTable)
        {
            var op = roster.Find(o => o.operatorName == name);
            if (op == null) { f.Add($"[RESOLVE_MISSING] {name} not in config"); continue; }

            // ±1 covers rounding in the published table, nothing more.
            Near(f, $"RESOLVE_{name}", op.baseResolve, want, 1f);
        }

        // ⭐ Orthogonality — the property the whole ratio approach exists to buy.
        // Defender (20 gold) must out-resolve Striker (18) DESPITE costing barely more, and
        // Moon (20) must sit below Knight (18) despite costing more. If Resolve ever tracks
        // cost, it has collapsed into "expensive is better" written in different letters.
        var def = roster.Find(o => o.operatorName == "Defender");
        var strk = roster.Find(o => o.operatorName == "Striker");
        var moon = roster.Find(o => o.operatorName == "Moon");
        var kni = roster.Find(o => o.operatorName == "Knight");

        if (def != null && strk != null && def.baseResolve <= strk.baseResolve)
            f.Add("[RESOLVE_ORTHOGONAL] Defender does not out-resolve Striker");
        if (moon != null && kni != null && moon.baseResolve >= kni.baseResolve)
            f.Add("[RESOLVE_ORTHOGONAL] Moon (20 gold) out-resolves Knight (18 gold)");

        // Nobody may pin to either end of the derived band — a roster where everyone reads
        // 10% or 45% carries no information at all.
        foreach (var op in roster)
        {
            if (op.baseResolve <= TDConstant.RESOLVE_BASE_MIN + 0.01f)
                f.Add($"[RESOLVE_FLOOR] {op.operatorName} pinned at the bottom of the band");
            if (op.baseResolve >= TDConstant.RESOLVE_BASE_MIN + TDConstant.RESOLVE_BASE_SPAN - 0.01f)
                f.Add($"[RESOLVE_CEIL] {op.operatorName} pinned at the top of the band");
        }

        // Guards: a zero-damage support must not read as infinitely stubborn.
        var support = new OperatorData { hp = 1000f, damage = 0f, attackSpeed = 0f, blockCount = 1 };
        if (float.IsNaN(support.baseResolve) || float.IsInfinity(support.baseResolve))
            f.Add("[RESOLVE_GUARD] zero-damage operator produced NaN/Infinity");

        // The three in-match adjustments, and the 5..60 clamp around them.
        var tart = roster.Find(o => o.operatorName == "Tart");
        if (tart == null) return;

        var m = new TDOperatorMorale();
        Near(f, "RESOLVE_NO_ADJ", m.ResolveChance(tart, Ctx(true)), tart.baseResolve);

        Near(f, "RESOLVE_CALM_ALLY", m.ResolveChance(tart, Ctx(true, calmAllies: 1)),
             tart.baseResolve + TDConstant.RESOLVE_ADJ_CALM_ALLY);

        var withBreak = Ctx(true);
        withBreak.allyBrokeThisWave = true;
        Near(f, "RESOLVE_ALLY_BROKE", m.ResolveChance(tart, withBreak),
             tart.baseResolve + TDConstant.RESOLVE_ADJ_ALLY_BROKE);

        // ⭐ Setbacks compound: an operator worn down by the match is worth less each time.
        var worn = new TDOperatorMorale();
        worn.OnSetback();
        worn.OnSetback();
        Near(f, "RESOLVE_SETBACKS", worn.ResolveChance(tart, Ctx(true)),
             tart.baseResolve + 2f * TDConstant.RESOLVE_ADJ_PER_SETBACK);

        // Clamp holds at both ends however the adjustments stack.
        var ruined = new TDOperatorMorale();
        for (int i = 0; i < 10; i++) ruined.OnSetback();
        float low = ruined.ResolveChance(tart, withBreak);
        if (low < TDConstant.RESOLVE_MIN - 0.01f)
            f.Add($"[RESOLVE_CLAMP_LOW] {low:F1} fell below {TDConstant.RESOLVE_MIN}");

        var ginger = roster.Find(o => o.operatorName == "Ginger");
        if (ginger != null)
        {
            float high = new TDOperatorMorale().ResolveChance(tart, Ctx(true, calmAllies: 9));
            if (high > TDConstant.RESOLVE_MAX + 0.01f)
                f.Add($"[RESOLVE_CLAMP_HIGH] {high:F1} rose above {TDConstant.RESOLVE_MAX}");
        }
    }
}

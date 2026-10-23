using System.Collections.Generic;
using System.Reflection;
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
        if (k_SetValue == null) { f.Add("[REFLECT] TDOperatorMorale.SetValue missing"); return f; }

        Rates(f);
        Bands(f);
        Spikes(f);
        Recovery(f);
        SkillPoints(f);
        RetreatIsNotAReset(f);
        BrokenState(f);
        BreakEdge(f);
        Leaks(f);
        LeakSplit(f);
        Amplifier(f);
        AmplifierSources(f);
        DeployCap(f);
        Targets(f);
        Pearson(f);
        A4(f);
        ReportContents(f);
        Lives(f);
        ZeroDamage(f);
        FallAnnouncesBreak(f);
        MoraleCues(f);
        SfxVoices(f);
        IconRendering(f);
        BadgeLayers(f);
        GreyLevels(f);
        CardBadge(f);
        AmpRing(f);
        BreakTiming(f);
        RescueArc(f);
        RescuerPick(f);
        RescueReach(f);
        RescueHint(f);
        LeakLog(f);
        Resolve(f);
        FocusScale(f);

        return f;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TDMoraleContext Ctx(bool engaged, int calmAllies = 0, float sinceHit = 0f)
        => new TDMoraleContext
        {
            engaged = engaged,
            calmAlliesAdjacent = calmAllies,
            secondsSinceHit = sinceHit,
        };

    // Private-field access for the tests that drive a live component or behavior. A field that
    // is not there is a failure, not an exception — the run must still report everything else.
    // Shared with TDBalanceValidator (same Editor assembly).
    internal static bool SetField(List<string> f, object o, string name, object value)
    {
        var fi = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (fi == null) { f.Add($"[REFLECT] {o.GetType().Name}.{name} missing"); return false; }
        fi.SetValue(o, value);
        return true;
    }

    internal static T GetField<T>(object o, string name)
    {
        var fi = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return fi == null ? default : (T)fi.GetValue(o);
    }

    // Sets the value through SetValue itself, so the collapse latch behaves exactly as in play.
    // Not through AddSpike any more: spikes stop at STRESS_SPIKE_CEILING, and At(100) must still break.
    private static readonly MethodInfo k_SetValue =
        typeof(TDOperatorMorale).GetMethod("SetValue", BindingFlags.Instance | BindingFlags.NonPublic);

    private static TDOperatorMorale At(float value)
    {
        var m = new TDOperatorMorale();
        k_SetValue.Invoke(m, new object[] { value });
        return m;
    }

    private static void Near(List<string> f, string tag, float got, float want, float tol = 0.01f)
    {
        if (Mathf.Abs(got - want) > tol)
            f.Add($"[{tag}] got {got:F3}, want {want:F3}");
    }

    // ── A — rates: no clock any more — nothing accrues over time, idling recovers ──

    private static void Rates(List<string> f)
    {
        Near(f, "ENGAGED_IS_FLAT", new TDOperatorMorale().NetRate(Ctx(true)), 0f);
        Near(f, "IDLE_RELIEF", new TDOperatorMorale().NetRate(Ctx(false)), -TDConstant.STRESS_IDLE_RELIEF);
        Near(f, "NO_TIME_STRESS", At(70f).NetRate(Ctx(true)), 0f);
        Near(f, "ALLY_NO_STRESS_EFFECT", new TDOperatorMorale().NetRate(Ctx(true, calmAllies: 5)), 0f);
    }

    // ── B — bands: a state for the icon, A4 and Resolve — no longer a price ─

    private static void Bands(List<string> f)
    {
        // Band edges: 33 is still Calm, 34 is Steady; 66 Steady, 67 Stressed; 100 Broken.
        if (TDOperatorMorale.StateOf(33f) != MoraleState.Calm) f.Add("[BAND] 33 is not Calm");
        if (TDOperatorMorale.StateOf(34f) != MoraleState.Steady) f.Add("[BAND] 34 is not Steady");
        if (TDOperatorMorale.StateOf(66f) != MoraleState.Steady) f.Add("[BAND] 66 is not Steady");
        if (TDOperatorMorale.StateOf(67f) != MoraleState.Stressed) f.Add("[BAND] 67 is not Stressed");
        if (TDOperatorMorale.StateOf(100f) != MoraleState.Broken) f.Add("[BAND] 100 is not Broken");
    }

    // ── C — recovery, and its ceiling ───────────────────────────────────────

    private static void Recovery(List<string> f)
    {
        // Wave-clear relief is a flat instant subtraction.
        var cleared = At(50f);
        cleared.OnWaveCleared();
        Near(f, "WAVE_CLEAR", cleared.Value, 50f - TDConstant.STRESS_WAVE_CLEAR_RELIEF);

        // Tầng 2 T4: the wave valve is for the operators nobody got past since the last relief.
        var leaked = At(50f); leaked.OnLeak(0.3f); float v = leaked.Value;
        leaked.OnWaveCleared();
        Near(f, "RELIEF_SKIPS_LEAKED", leaked.Value, v);
        leaked.OnWaveCleared(); // held the next wave clean: the mark went with the last relief
        Near(f, "RELIEF_NEXT_WAVE", leaked.Value, v - TDConstant.STRESS_WAVE_CLEAR_RELIEF);

        // Answerable but already charged in full by this enemy: still leaked past.
        var zero = At(50f); zero.OnLeak(0f); zero.OnWaveCleared();
        Near(f, "RELIEF_MARK_ON_ZERO_TOPUP", zero.Value, 50f);

        // Off the field nobody calls OnWaveCleared, so leaving must take the mark along.
        var away = At(90f); away.OnLeak(0.3f); away.OnRetreat(); float a = away.Value;
        away.OnWaveCleared();
        Near(f, "RELIEF_MARK_LEAVES_WITH_RETREAT", away.Value, a - TDConstant.STRESS_WAVE_CLEAR_RELIEF);
        var fell = At(50f); fell.OnLeak(0.3f); fell.OnDeath(); float d = fell.Value;
        fell.OnWaveCleared();
        Near(f, "RELIEF_MARK_LEAVES_WITH_DEATH", fell.Value, d - TDConstant.STRESS_WAVE_CLEAR_RELIEF);
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
    // A leak latches the operator OUTSIDE that window (it arrives as an event from the enemy), so a
    // break caused by one never fired its own spike, setback or release. Spikes no longer break
    // anyone (Tầng 2 §4.2), but leaks still arrive between ticks.

    private static void BreakEdge(List<string> f)
    {
        var leaked = At(95f);
        leaked.OnLeak(1f);
        if (!leaked.ConsumeBreak()) f.Add("[EDGE_FROM_LEAK] a leak-induced break raised no edge");
        if (leaked.ConsumeBreak()) f.Add("[EDGE_ONCE] the same break was reported twice");
        if (leaked.Setbacks != 1) f.Add($"[EDGE_SETBACK] leak break counted {leaked.Setbacks}, want 1");

        leaked.OnLeak(1f); // already latched
        if (leaked.ConsumeBreak()) f.Add("[EDGE_WHILE_LATCHED] a leak on a broken operator raised a second edge");

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
        while (!again.IsBroken) again.OnLeak(1f);
        if (!again.ConsumeBreak() || again.Setbacks != 2)
            f.Add($"[EDGE_REBREAK] second break after rescue: setbacks {again.Setbacks}, want 2");
    }

    // ── K — spikes push toward the edge, never over it; one fall, one spike (Tầng 2 §4.2) ──

    private static void Spikes(List<string> f)
    {
        var s = At(60f); s.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        Near(f, "SPIKE_BREAK_15", s.Value, 75f);
        var c = At(80f); c.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        Near(f, "SPIKE_CEILING", c.Value, 90f);
        var h = At(92f); h.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        Near(f, "SPIKE_ABOVE_CEILING", h.Value, 92f);

        var many = new TDOperatorMorale();
        for (int i = 0; i < 20; i++) many.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        if (many.IsBroken || many.ConsumeBreak()) f.Add("[SPIKE_NEVER_BREAKS] twenty break spikes collapsed an operator");

        Near(f, "DEATH_SPIKE_STANDING", At(40f).DeathSpike, 10f);
        Near(f, "DEATH_SPIKE_AFTER_BREAK", At(TDConstant.STRESS_MAX).DeathSpike, 0f);

        // Collapsed and recovering under the latch (100 → ~80): a spike lifts to 90 at most and
        // must neither lift the collapse nor announce it a second time.
        var latched = At(TDConstant.STRESS_MAX); latched.ConsumeBreak();
        for (int i = 0; i < 200; i++) latched.Tick(0.1f, Ctx(false, sinceHit: TDConstant.STRESS_BROKEN_CALM_SECONDS + 1f));
        latched.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        Near(f, "SPIKE_ON_LATCHED", latched.Value, 90f);
        if (!latched.IsBroken || latched.ConsumeBreak()) f.Add("[SPIKE_ON_LATCHED] a spike lifted or re-announced the collapse");

        // Spec Tầng 2 §7.2 step 1: stress by source, counting only what actually landed.
        var src = At(85f); src.OnLeak(1f); src.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE); // 92: the spike adds nothing
        Near(f, "SOURCE_LEAK", src.LeakStress, 7f);
        Near(f, "SOURCE_SPIKE_CLAMPED", src.SpikeStress, 0f);
        var sp = At(50f); sp.AddSpike(TDConstant.STRESS_ALLY_BREAK_SPIKE);
        Near(f, "SOURCE_SPIKE", sp.SpikeStress, 15f);
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
        // Tầng 2 T2: a leak costs the same in every band (round 2: 10 → 7, Herald ×2 → ×1.5).
        foreach (float start in new[] { 10f, 50f, 70f })
            Near(f, $"LEAK_FLAT_{start:F0}", At(start).OnLeak(1f), 7f);
        Near(f, "LEAK_AMPLIFIED", At(0f).OnLeak(TDConstant.LEAK_AMP_MULT), 10.5f);

        // Spec §7.2 forced case: a Calm melee alone, a Herald beside it, a whole pack walks past —
        // at most 60 stress and still standing (round 2 measured 100 and a collapse at 10 / ×2).
        // "Calm" is the whole band, so the case runs from its top as well as from zero: from 0 is
        // the easiest start, and 7 / ×1.5 broke from 23 up when the Herald amplified the pack.
        float packMult = TDOperatorRegistry.LeakAmplifier(EnemyType.Horde, Vector2Int.zero, new[] { Vector2Int.right });
        foreach (float start in new[] { 0f, TDConstant.STRESS_CALM_MAX })
        {
            var lone = At(start); float packGain = 0f;
            for (int i = 0; i < TDConstant.HORDE_PACK_SIZE; i++) packGain += lone.OnLeak(packMult);
            if (packGain > 60f || lone.IsBroken) f.Add($"[NIGHTMARE_PACK] a pack past a lone melee at {start} beside a Herald: +{packGain:F1}, broken {lone.IsBroken} (want ≤ 60, standing)");
        }
        var broken = At(TDConstant.STRESS_MAX); int sb = broken.Setbacks;
        Near(f, "LEAK_IGNORED_WHEN_BROKEN", broken.OnLeak(1f), 0f);
        if (broken.Setbacks != sb) f.Add("[LEAK_IGNORED_WHEN_BROKEN] a leak on a collapsed operator counted a setback");

        // Spec Tầng 2 §5: 15 / 21 / 48 leaks from zero to collapse (11 / 15 / 35 with the band
        // multiplier, 8 / 11 / 25 before round 2).
        foreach (var (tag, share, want) in new[] { ("LEAKS_ALONE", 1f, 15), ("LEAKS_SUPPORTED", 0.7f, 21), ("LEAKS_RANGED", 0.3f, 48) })
        {
            var m = new TDOperatorMorale(); int n = 0;
            while (!m.IsBroken && n < 100) { m.OnLeak(share); n++; }
            if (n != want) f.Add($"[{tag}] {n} leaks to collapse, want {want}");
            if (new TDOperatorMorale().LeaksToBreak(share) != want) f.Add($"[{tag}_PROJECTION] LeaksToBreak disagrees with the walk");
            if (!m.ConsumeBreak()) f.Add($"[{tag}_EDGE] a leak-induced collapse raised no edge");
        }
        if (new TDOperatorMorale().LeaksToBreak(0f) != int.MaxValue) f.Add("[LEAKS_NO_SHARE] share 0 reported a finite count");

        // After a spike the most an operator can carry is 90 (Tầng 2 §5).
        if (At(90f).LeaksToBreak(0.7f) != 3) f.Add($"[LEAKS_FROM_90_SUPPORTED] {At(90f).LeaksToBreak(0.7f)}, want 3");
        if (At(90f).LeaksToBreak(1f) != 2) f.Add($"[LEAKS_FROM_90_ALONE] {At(90f).LeaksToBreak(1f)}, want 2");

        // The overlay's count must match the real walk from every start, not just from zero.
        foreach (float share in new[] { 1f, 0.7f, 0.3f })
            for (int start = 0; start < 100; start++)
            {
                var walk = At(start); int n = 0;
                while (!walk.IsBroken && n < 200) { walk.OnLeak(share); n++; }
                int projected = At(start).LeaksToBreak(share);
                if (projected != n) { f.Add($"[LEAKS_PROJECTION_ALL_STARTS] share {share} from {start}: projected {projected}, walked {n}"); break; }
            }
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

        // Tầng 2 §4.1: one enemy charges each operator its heaviest weight along the path, once.
        var ledger = new Dictionary<string, float>();
        Near(f, "TOPUP_FIRST",   TDLeakShare.TopUp(ledger, "Moon", 0.3f), 0.3f);
        Near(f, "TOPUP_SAME",    TDLeakShare.TopUp(ledger, "Moon", 0.3f), 0f);    // second cell, same share
        Near(f, "TOPUP_LIGHTER", TDLeakShare.TopUp(ledger, "Moon", 0.15f), 0f);  // a lighter share adds nothing
        Near(f, "TOPUP_HEAVIER", TDLeakShare.TopUp(ledger, "Moon", 1f), 0.7f);   // the melee at the next cell is broken
        Near(f, "TOPUP_PER_OPERATOR", TDLeakShare.TopUp(ledger, "Knight", 0.7f), 0.7f);
        Near(f, "TOPUP_NEW_ENEMY", TDLeakShare.TopUp(new Dictionary<string, float>(), "Moon", 0.3f), 0.3f);

        // The spec's worked example: A (melee standing) then B (melee broken) → 2.1 + 4.9 = 7, not 9.1 (or 21 before).
        var path = new Dictionary<string, float>(); var moon = new TDOperatorMorale();
        float total = moon.OnLeak(TDLeakShare.TopUp(path, "Moon", TDLeakShare.Split(true, 1).eachRanged))
                    + moon.OnLeak(TDLeakShare.TopUp(path, "Moon", TDLeakShare.Split(false, 1).eachRanged));
        Near(f, "TOPUP_WORKED_EXAMPLE", total, 7f);
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

    // Tầng 2 §4.1 / T5: the boss amplifies leaks exactly like a Herald; nothing else does.
    private static void AmplifierSources(List<string> f)
    {
        foreach (EnemyType t in System.Enum.GetValues(typeof(EnemyType)))
        {
            bool want = t == EnemyType.Herald || t == EnemyType.Boss;
            if (TDOperatorRegistry.AmplifiesLeaks(t) != want) f.Add($"[AMP_SOURCE] {t}: {!want}, want {want}");
        }
    }

    // Spec §5.5 / Tầng 2 §4.1: a Herald or boss within LEAK_AMP_RADIUS cells (Euclid) of a leak amplifies it
    // ×LEAK_AMP_MULT; two sources do not stack, whatever their types.
    // A Horde body is never amplified (spec §7.2 forced case).
    private static void Amplifier(List<string> f)
    {
        var o = new Vector2Int(0, 0);
        var n = EnemyType.Normal;
        Near(f, "AMP_NONE", TDOperatorRegistry.LeakAmplifier(n, o, new Vector2Int[0]), 1f);
        Near(f, "AMP_INSIDE", TDOperatorRegistry.LeakAmplifier(n, o, new[] { new Vector2Int(4, 0) }), TDConstant.LEAK_AMP_MULT);
        Near(f, "AMP_OUTSIDE", TDOperatorRegistry.LeakAmplifier(n, o, new[] { new Vector2Int(4, 1) }), 1f); // √17 > 4
        Near(f, "AMP_NO_STACK", TDOperatorRegistry.LeakAmplifier(n, o, new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) }), TDConstant.LEAK_AMP_MULT);
        Near(f, "AMP_HORDE_EXEMPT", TDOperatorRegistry.LeakAmplifier(EnemyType.Horde, o, new[] { new Vector2Int(1, 0) }), 1f);
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

    // §7.3 A4: a near-full-HP operator pulled out because stress was red. Death and match end
    // are not retreats; a Steady retreat is not stress-driven.
    private static void A4(List<string> f)
    {
        TDPressureProbe.Stint S(bool retreated, float hp, MoraleState band)
            => new TDPressureProbe.Stint { op = "x", melee = true, seconds = 10f, retreated = retreated, hpFrac = hp, band = band };
        var stints = new List<TDPressureProbe.Stint>
        {
            S(true, 1.00f, MoraleState.Stressed),  // counts
            S(true, 0.80f, MoraleState.Stressed),  // counts: the boundary is inclusive
            S(true, 0.79f, MoraleState.Stressed),  // HP too low
            S(true, 1.00f, MoraleState.Steady),    // not red
            S(false, 1.00f, MoraleState.Stressed), // died / match ended
        };
        int got = TDPressureProbe.CountA4(stints);
        if (got != 2) f.Add($"[A4_COUNT] {got}, want 2");
    }

    // A pack walking past a full line is a burst of leaks; a Debug.Log for each floods the
    // console a playtester reads (and costs a stack trace each in the Editor). Opt-in only.
    private static void LeakLog(List<string> f)
    {
        var saved = TDOperatorRegistry.api;
        int logged = 0;
        Application.LogCallback count = (msg, _, _) => { if (msg.StartsWith("[Leak]")) logged++; };
        Application.logMessageReceived += count;
        try
        {
            TDOperatorRegistry.api = new TDOperatorRegistry();

            TDOperatorRegistry.LogLeaks = false;
            TDOperatorRegistry.api.ReportLeak(Vector2Int.zero, EnemyType.Normal, new Dictionary<TDOperatorView, float>());
            if (logged != 0) f.Add($"[LEAK_LOG_GATED] {logged} [Leak] line(s) with LogLeaks off");

            logged = 0;
            TDOperatorRegistry.LogLeaks = true;
            TDOperatorRegistry.api.ReportLeak(Vector2Int.zero, EnemyType.Normal, new Dictionary<TDOperatorView, float>());
            if (logged != 1) f.Add($"[LEAK_LOG_ON] {logged} [Leak] line(s) with LogLeaks on, want 1");
        }
        finally
        {
            TDOperatorRegistry.LogLeaks = false;
            Application.logMessageReceived -= count;
            TDOperatorRegistry.api = saved;
        }
    }

    // §7.3 playtest: several reports get compared side by side, so each must say which match it
    // is, what each wave cost in lives (a leak is not a life), which waves were boss waves, and
    // how often the player rotated. Driven through the probe's own event handlers.
    private static void ReportContents(List<string> f)
    {
        const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
        var waveStarted = typeof(TDPressureProbe).GetMethod("OnWaveStarted", S);
        var lifeLost = typeof(TDPressureProbe).GetMethod("OnLifeLost", S);
        if (waveStarted == null || lifeLost == null) { f.Add("[REFLECT] TDPressureProbe.OnWaveStarted / OnLifeLost missing"); return; }

        var plans = new List<List<EnemyType>>
        {
            new() { EnemyType.Normal },
            new() { EnemyType.Normal, EnemyType.Boss },
        };
        TDPressureProbe.BeginMatch("DEMO-1", 0, Difficulty.Hard, plans);
        waveStarted.Invoke(null, new object[] { 0 });
        TDPressureProbe.RecordStint("Knight", true, 10f, 0f, 22f, 7f, 15f, false, 1f, MoraleState.Steady);
        TDPressureProbe.RecordDeath();
        lifeLost.Invoke(null, new object[] { Vector3.zero });
        lifeLost.Invoke(null, new object[] { Vector3.zero });
        waveStarted.Invoke(null, new object[] { 1 });
        waveStarted.Invoke(null, new object[] { 2 }); // closes wave 2; a wave past the plan is not a boss
        string report = TDPressureProbe.LastReport;

        string header = report.Split('\n')[0];
        if (!header.Contains("DEMO-1") || !header.Contains("level 0") || !header.Contains("Hard"))
            f.Add($"[REPORT_HEADER] '{header.Trim()}' does not name the stage, level and difficulty");

        // wave  boss  onField  limit  goldEarned  leaks  leaked  horde  livesLost  collapses  deaths
        string[] Row(int wave)
        {
            foreach (var line in report.Split('\n'))
            {
                var t = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (t.Length == 11 && t[0] == wave.ToString()) return t;
            }
            return null;
        }
        var w1 = Row(1); var w2 = Row(2);
        if (w1 == null || w2 == null) { f.Add("[REPORT_ROWS] wave rows 1 and 2 not found with 11 columns"); return; }
        if (w1[1] != "-" || w2[1] != "B") f.Add($"[REPORT_BOSS] boss column: wave 1 '{w1[1]}', wave 2 '{w2[1]}', want '-' and 'B'");
        if (w1[8] != "2" || w2[8] != "0") f.Add($"[REPORT_LIVES] lives lost: wave 1 {w1[8]}, wave 2 {w2[8]}, want 2 and 0");
        if (w1[10] != "1" || w2[10] != "0") f.Add($"[REPORT_DEATHS] deaths: wave 1 {w1[10]}, wave 2 {w2[10]}, want 1 and 0");
        if (!report.Contains("retreats: 0")) f.Add("[REPORT_RETREATS] no 'retreats: N' line");
        if (!report.Contains("leak +7.0 · spike +15.0")) f.Add("[REPORT_SOURCES] stint line does not split leak and spike stress");
    }

    // Tầng 3 §4.1: every lost life goes through LoseLives: never below zero, and the match ends once.
    // Edit Mode after a Play session can still have the probe and the effect manager subscribed, so
    // the bus is stashed: a real GameOver would write a fake report, a real LifeLost spawns effects.
    private static void Lives(List<string> f)
    {
        const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
        var lostField = typeof(TDGameEventBus).GetField("OnLifeLost", S);
        var overField = typeof(TDGameEventBus).GetField("OnGameOver", S);
        if (lostField == null || overField == null) { f.Add("[REFLECT] TDGameEventBus.OnLifeLost / OnGameOver backing fields missing"); return; }

        object savedLost = lostField.GetValue(null), savedOver = overField.GetValue(null);
        int lost = 0, over = 0, overCb = 0, changed = 0;
        try
        {
            lostField.SetValue(null, (System.Action<Vector3>)(_ => lost++));
            overField.SetValue(null, (System.Action)(() => over++));

            var life = new TDPlayerLifeControl();
            life.onGameOver += () => overCb++;
            life.onLifeChanged += _ => changed++;
            life.Initialize();
            int start = TDConstant.CONFIG_PLAYER_STARTING_LIVES;

            changed = 0;
            life.LoseLives(0, Vector3.zero);
            life.LoseLives(-2, Vector3.zero);
            if (life.CurrentLives != start || lost != 0 || changed != 0)
                f.Add($"[LIVES_ZERO_COUNT] count ≤ 0 changed something: lives {life.CurrentLives}, LifeLost {lost}, onLifeChanged {changed}");

            life.LoseLives(3, Vector3.zero);
            if (life.CurrentLives != start - 3 || lost != 3 || over != 0)
                f.Add($"[LIVES_DEATH_COST] lives {life.CurrentLives}, LifeLost {lost}, GameOver {over}; want {start - 3}, 3, 0");

            life.LoseLives(life.CurrentLives - 2, Vector3.zero); // down to 2
            lost = 0;
            life.LoseLives(3, Vector3.zero);
            if (life.CurrentLives != 0 || lost != 2 || over != 1 || overCb != 1)
                f.Add($"[LIVES_FLOOR_ZERO] lives {life.CurrentLives}, LifeLost {lost}, GameOver {over}/{overCb}; want 0, 2, 1/1");

            lost = 0;
            life.LoseLives(1, Vector3.zero);
            if (life.CurrentLives != 0 || lost != 0 || over != 1 || overCb != 1)
                f.Add($"[LIVES_AFTER_GAMEOVER] lives {life.CurrentLives}, LifeLost {lost}, GameOver {over}/{overCb}; want 0, 0, 1/1");
        }
        finally
        {
            lostField.SetValue(null, savedLost);
            overField.SetValue(null, savedOver);
        }
    }

    // §06 · 3.1: a collapsed operator calms down after STRESS_BROKEN_CALM_SECONDS without a hit.
    // A 0-damage enemy (a Herald walking past) is not a hit and must not restart that clock.
    private static void ZeroDamage(List<string> f)
    {
        var go = new GameObject("ZeroDamageProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var op = go.AddComponent<TDOperatorView>();
            bool ok = SetField(f, op, "m_Initialized", true) & SetField(f, op, "m_CurrentHp", 100f)
                    & SetField(f, op, "m_MaxHp", 100f) & SetField(f, op, "m_LastHitTime", -100f)
                    & SetField(f, op, "m_HPBarView", null);
            if (!ok) return;

            op.TakeDamage(0f);
            if (GetField<float>(op, "m_LastHitTime") != -100f) f.Add("[ZERO_DAMAGE_NOT_A_HIT] TakeDamage(0) restarted the calm clock");

            op.TakeDamage(1f);
            if (GetField<float>(op, "m_LastHitTime") == -100f) f.Add("[ZERO_DAMAGE_CONTROL] a real hit did not register");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // One fall, one spike (Tầng 2 §4.2). A leak breaks a melee inside the ENEMY's Update, and the
    // walk-over blow (x3) can kill them the same frame, before their own TickMorale announces the
    // break. Dying broken spikes nothing, so the fall itself must announce it, or the break goes
    // unspiked and the collapse uncounted.
    private static void FallAnnouncesBreak(List<string> f)
    {
        var announce = typeof(TDOperatorView).GetMethod("AnnounceFall", BindingFlags.Instance | BindingFlags.NonPublic);
        var total = typeof(TDPressureProbe).GetField("s_TotalCollapses", BindingFlags.Static | BindingFlags.NonPublic);
        var brokeField = typeof(TDGameEventBus).GetField("OnOperatorBroke", BindingFlags.Static | BindingFlags.NonPublic);
        if (announce == null || total == null || brokeField == null) { f.Add("[REFLECT] TDOperatorView.AnnounceFall / TDPressureProbe.s_TotalCollapses / TDGameEventBus.OnOperatorBroke missing"); return; }

        // Stashed as in Lives: a live effect manager would play the break sting from a validator run.
        object savedBroke = brokeField.GetValue(null);
        int broke = 0;
        var go = new GameObject("FallProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            brokeField.SetValue(null, (System.Action<Vector3>)(_ => broke++));
            var op = go.AddComponent<TDOperatorView>();
            var broken = At(TDConstant.STRESS_MAX); // collapsed, and nobody has reacted to it yet
            if (!SetField(f, op, "<Morale>k__BackingField", broken)) return;

            int before = (int)total.GetValue(null);
            announce.Invoke(op, null);
            if ((int)total.GetValue(null) != before + 1) f.Add("[FALL_ANNOUNCES_BREAK] dying with an unannounced break recorded no collapse");
            if (broken.ConsumeBreak()) f.Add("[FALL_CONSUMES_EDGE] the fall left the break pending");
            if (broke != 1) f.Add($"[FALL_RAISES_BROKE] the fall raised OnOperatorBroke {broke} times, want 1");
        }
        finally
        {
            brokeField.SetValue(null, savedBroke);
            Object.DestroyImmediate(go);
        }
    }

    // Tầng 4 §4.5: which transitions deserve a cue. The break has its own edge (AnnounceBreak), so
    // CueFor only answers "warning on the way up" and "relief on the way out of a collapse".
    private static void MoraleCues(List<string> f)
    {
        Cue(f, "CUE_STEADY_STRESSED", MoraleState.Steady, MoraleState.Stressed, MoraleCue.Stressed);
        Cue(f, "CUE_CALM_STRESSED", MoraleState.Calm, MoraleState.Stressed, MoraleCue.Stressed);
        Cue(f, "CUE_STRESSED_BROKEN", MoraleState.Stressed, MoraleState.Broken, MoraleCue.None);
        Cue(f, "CUE_STEADY_BROKEN", MoraleState.Steady, MoraleState.Broken, MoraleCue.None);
        Cue(f, "CUE_BROKEN_STEADY", MoraleState.Broken, MoraleState.Steady, MoraleCue.Recovered);
        Cue(f, "CUE_BROKEN_STRESSED", MoraleState.Broken, MoraleState.Stressed, MoraleCue.Recovered);
        Cue(f, "CUE_STRESSED_STEADY", MoraleState.Stressed, MoraleState.Steady, MoraleCue.None);
        Cue(f, "CUE_CALM_CALM", MoraleState.Calm, MoraleState.Calm, MoraleCue.None);
        Cue(f, "CUE_BROKEN_BROKEN", MoraleState.Broken, MoraleState.Broken, MoraleCue.None);

        // Effect Config stores these as ints: a key that moves silently plays the wrong sound.
        if ((int)GameEventKey.OperatorStressed != 16 || (int)GameEventKey.OperatorBroke != 17 || (int)GameEventKey.OperatorRecovered != 18)
            f.Add($"[EVENT_KEY_VALUES] {(int)GameEventKey.OperatorStressed}/{(int)GameEventKey.OperatorBroke}/{(int)GameEventKey.OperatorRecovered}, want 16/17/18");

        TickCues(f);
    }

    private static void Cue(List<string> f, string tag, MoraleState prev, MoraleState now, MoraleCue want)
    {
        var got = TDOperatorMorale.CueFor(prev, now);
        if (got != want) f.Add($"[{tag}] {prev}→{now} gave {got}, want {want}");
    }

    // The view raises the cues frame to frame, whatever moved the stress in between.
    private static void TickCues(List<string> f)
    {
        const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
        var stressedField = typeof(TDGameEventBus).GetField("OnOperatorStressed", S);
        var recoveredField = typeof(TDGameEventBus).GetField("OnOperatorRecovered", S);
        var tick = typeof(TDOperatorView).GetMethod("TickMorale", BindingFlags.Instance | BindingFlags.NonPublic);
        if (stressedField == null || recoveredField == null || tick == null) { f.Add("[REFLECT] TDGameEventBus.OnOperatorStressed / OnOperatorRecovered / TDOperatorView.TickMorale missing"); return; }

        object savedStressed = stressedField.GetValue(null), savedRecovered = recoveredField.GetValue(null);
        int stressed = 0, recovered = 0;
        var go = new GameObject("CueProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            stressedField.SetValue(null, (System.Action<Vector3>)(_ => stressed++));
            recoveredField.SetValue(null, (System.Action<Vector3>)(_ => recovered++));
            var op = go.AddComponent<TDOperatorView>();
            object[] frame = { 0.016f };

            if (!Arm(f, op, At(70f), MoraleState.Steady)) return;
            tick.Invoke(op, frame);
            tick.Invoke(op, frame);
            if (stressed != 1) f.Add($"[TICK_RAISES_STRESSED] Steady→Stressed raised {stressed} cues over two ticks, want 1");

            var rescued = At(TDConstant.STRESS_MAX);
            rescued.ConsumeBreak();
            rescued.OnRescue();
            if (rescued.IsBroken) { f.Add("[TICK_SETUP] OnRescue did not release the collapse"); return; }
            stressed = 0;
            if (!Arm(f, op, rescued, MoraleState.Broken)) return;
            tick.Invoke(op, frame);
            if (recovered != 1 || stressed != 0) f.Add($"[TICK_RAISES_RECOVERED] leaving Broken raised {recovered} recovered / {stressed} stressed, want 1 / 0");

            recovered = 0;
            if (!Arm(f, op, At(50f), MoraleState.Stressed)) return;
            tick.Invoke(op, frame);
            if (stressed != 0 || recovered != 0) f.Add($"[TICK_SILENT_DOWN] Stressed→Steady raised {stressed} / {recovered}, want 0 / 0");
        }
        finally
        {
            stressedField.SetValue(null, savedStressed);
            recoveredField.SetValue(null, savedRecovered);
            Object.DestroyImmediate(go);
        }
    }

    private static bool Arm(List<string> f, TDOperatorView op, TDOperatorMorale morale, MoraleState last)
        => SetField(f, op, "<Morale>k__BackingField", morale) & SetField(f, op, "m_LastMoraleState", last);

    // Tầng 4 C6: with all voices busy a morale cue takes the oldest ordinary sound's voice. It never
    // takes another priority sound's voice, and an ordinary sound is still dropped as before.
    private static void SfxVoices(List<string> f)
    {
        const bool T = true, F = false;
        Voice(f, "SFX_PICK_FREE", new[] { T, F, T }, new[] { 0f, 0f, 0f }, new[] { F, F, F }, false, 1);
        Voice(f, "SFX_PICK_STEAL_OLDEST", new[] { T, T, T }, new[] { 3f, 1f, 2f }, new[] { F, F, F }, true, 1);
        Voice(f, "SFX_PICK_NORMAL_DROPPED", new[] { T, T, T }, new[] { 3f, 1f, 2f }, new[] { F, F, F }, false, -1);
        Voice(f, "SFX_PICK_KEEPS_PRIORITY", new[] { T, T, T }, new[] { 0f, 5f, 1f }, new[] { T, F, T }, true, 1);
        Voice(f, "SFX_PICK_ALL_PRIORITY", new[] { T, T, T }, new[] { 0f, 5f, 1f }, new[] { T, T, T }, true, -1);
    }

    // Tầng 4 §4.2, §4.4: the badge draws over 3D models — a boss standing on the line hid it
    // completely — and its scale lives on a "Badge" child, so what sits beside it is not scaled too.
    private static void IconRendering(List<string> f)
    {
        var go = new GameObject("IconProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var icon = TDMoraleIconView.Attach(go.transform);
            if (icon == null) { f.Add("[ICON_ATTACH] TDMoraleIconView.Attach returned null — sprites missing?"); return; }

            // Checks the SHADER, not a material value: the canvas sets unity_GUIZTestMode itself on
            // every draw, so a material's own value never reached the GPU (the first version of
            // this test read that value and passed while the boss still hid the badge in play).
            foreach (var g in icon.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                bool onTop = g is TMPro.TMP_Text t
                    ? t.fontSharedMaterial != null && t.fontSharedMaterial.shader.name.EndsWith("Overlay")
                    : g.material != null && g.material.shader.name == TDMoraleIconView.ON_TOP_SHADER;
                if (!onTop) f.Add($"[ICON_ON_TOP] {g.name} draws with a depth-tested shader: a model standing in front hides it");
            }

            icon.Refresh(At(10f));
            var badge = icon.transform.Find("Badge");
            float baseScale = GetField<float>(icon, "m_BaseScale");
            if (badge == null || Mathf.Abs(icon.transform.localScale.x - baseScale) > 1e-6f)
                f.Add($"[ICON_BADGE_SCALED] root scale {icon.transform.localScale.x:F5} vs base {baseScale:F5}, Badge child {(badge != null ? "present" : "missing")}");

            var outline = badge != null ? badge.Find("Outline")?.GetComponent<UnityEngine.UI.Image>() : null;
            ColorUtility.TryParseHtmlString(TDConstant.COLOR_MORALE_BROKEN, out var indigo);
            icon.Refresh(At(TDConstant.STRESS_MAX));
            bool shownBroken = outline != null && outline.enabled;
            bool rightColour = outline != null && Mathf.Abs(outline.color.r - indigo.r) + Mathf.Abs(outline.color.g - indigo.g) + Mathf.Abs(outline.color.b - indigo.b) < 0.03f;
            icon.Refresh(At(10f));
            bool shownCalm = outline != null && outline.enabled;
            if (!shownBroken || !rightColour || shownCalm)
                f.Add($"[BROKEN_OUTLINE] outline {(outline != null ? "present" : "missing")}: on when Broken {shownBroken}, indigo {rightColour}, on when Calm {shownCalm}");

            // A rescue inside the label's 1.5 s left "BROKEN" over a badge already back to Steady.
            icon.PlayBreak();
            icon.Refresh(At(TDConstant.STRESS_MAX));
            bool keptWhileBroken = !float.IsNegativeInfinity(GetField<float>(icon, "m_BreakStart"));
            icon.Refresh(At(10f));
            bool clearedOnRecovery = float.IsNegativeInfinity(GetField<float>(icon, "m_BreakStart"));
            if (!keptWhileBroken || !clearedOnRecovery)
                f.Add($"[LABEL_CLEARS_ON_RECOVERY] kept while Broken {keptWhileBroken}, cleared once not Broken {clearedOnRecovery}");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Tầng 4b §4.3: the badge's four layers come from TDMoraleBadge, the one piece the floating icon
    // and the slot-bar card share. Order matters — each layer sits on the one before, and the break's
    // flash goes right over the disc — and no layer may take a tap: on the card it lies over the button.
    private static void BadgeLayers(List<string> f)
    {
        var go = new GameObject("BadgeProbe", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var badge = TDMoraleBadge.Build((RectTransform)go.transform, null);
            if (badge == null) { f.Add("[BADGE_LAYERS] Build returned null"); return; }

            string built = ChildNames(go.transform);
            if (built != "Disc,Outline,Backdrop,Face") f.Add($"[BADGE_LAYERS] built {built}, want Disc,Outline,Backdrop,Face");
            foreach (var g in go.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                if (g.raycastTarget) f.Add($"[BADGE_LAYERS] {g.name} takes raycasts");

            var icon = TDMoraleIconView.Attach(go.transform);
            var inIcon = icon != null ? icon.transform.Find("Badge") : null;
            string iconLayers = inIcon != null ? ChildNames(inIcon) : "none";
            if (iconLayers != "Disc,Flash,Outline,Backdrop,Face")
                f.Add($"[BADGE_LAYERS] icon Badge has {iconLayers}, want Disc,Flash,Outline,Backdrop,Face");
        }
        finally { Object.DestroyImmediate(go); }
    }

    // Tầng 4b §4.4 (C8): in greyscale Steady and Stressed both read as "dark disc with a face" (Tầng 4
    // A5). Their discs now take their own colour, opaque, so the four discs sit at four grey levels
    // whatever the ground under them: Calm < Stressed < Steady < Broken, each step at least 40 apart.
    private static void GreyLevels(List<string> f)
    {
        float Luma(Color c) => 255f * (0.299f * c.r + 0.587f * c.g + 0.114f * c.b);

        float calm = Luma(TDMoraleBadge.DiscColour(MoraleState.Calm, 0f));
        float stressedLow = Luma(TDMoraleBadge.DiscColour(MoraleState.Stressed, 1f)); // darkest point of the pulse
        float stressedHigh = Luma(TDMoraleBadge.DiscColour(MoraleState.Stressed, 0f));
        float steady = Luma(TDMoraleBadge.DiscColour(MoraleState.Steady, 0f));
        float broken = Luma(TDMoraleBadge.DiscColour(MoraleState.Broken, 0f));

        Near(f, "C8_GREY_CALM", calm, 13f, 3f);
        Near(f, "C8_GREY_STRESSED_LOW", stressedLow, 70f, 3f);
        Near(f, "C8_GREY_STRESSED_HIGH", stressedHigh, 100f, 3f);
        Near(f, "C8_GREY_STEADY", steady, 148f, 3f);
        Near(f, "C8_GREY_BROKEN", broken, 205f, 3f);
        if (stressedLow - calm < 40f || steady - stressedHigh < 40f || broken - steady < 40f)
            f.Add($"[C8_GREY_LEVELS] gaps calm→stressed {stressedLow - calm:F0}, stressed→steady {steady - stressedHigh:F0}, steady→broken {broken - steady:F0}; want ≥ 40 each");

        if (TDMoraleBadge.DiscColour(MoraleState.Steady, 0f).a < 0.999f || TDMoraleBadge.DiscColour(MoraleState.Stressed, 0.5f).a < 0.999f)
            f.Add("[C8_GREY_LEVELS] Steady/Stressed disc is not opaque, so its grey depends on the ground");

        ColorUtility.TryParseHtmlString(TDMoraleBadge.COLOR_MORALE_FACE_ON_FILL, out var dark);
        if (TDMoraleBadge.FaceColour(MoraleState.Steady) != dark || TDMoraleBadge.FaceColour(MoraleState.Stressed) != dark)
            f.Add($"[C8_GREY_LEVELS] face on a filled disc is {TDMoraleBadge.FaceColour(MoraleState.Steady)} / {TDMoraleBadge.FaceColour(MoraleState.Stressed)}, want {TDMoraleBadge.COLOR_MORALE_FACE_ON_FILL}");
    }

    // Tầng 4b §4.5: an operator's card in the slot bar wears the same badge as their head, while they
    // are on the field or still carry stress or a cooldown — the roster keeps stress when they leave,
    // so a Stressed badge on a card says "don't drop them back in yet". The badge lies over the card's
    // corner, so it must never take the tap that lifts the operator.
    private static void CardBadge(List<string> f)
    {
        if (TDSlotHolderItemView.ShowMoraleOnCard(false, 0f, 0f) || !TDSlotHolderItemView.ShowMoraleOnCard(true, 0f, 0f)
            || !TDSlotHolderItemView.ShowMoraleOnCard(false, 40f, 0f) || !TDSlotHolderItemView.ShowMoraleOnCard(false, 0f, 5f))
            f.Add($"[CARD_BADGE_VISIBILITY] fresh {TDSlotHolderItemView.ShowMoraleOnCard(false, 0f, 0f)} (want false), deployed {TDSlotHolderItemView.ShowMoraleOnCard(true, 0f, 0f)}, off-field stressed {TDSlotHolderItemView.ShowMoraleOnCard(false, 40f, 0f)}, cooling down {TDSlotHolderItemView.ShowMoraleOnCard(false, 0f, 5f)} (want true)");

        var prefab = Resources.Load<GameObject>("Prefabs/SlotHolder");
        if (prefab == null) { f.Add("[CARD_BADGE] no Resources/Prefabs/SlotHolder"); return; }

        var turret = NewCard(prefab, TowerType.Cannon);
        var card = NewCard(prefab, TowerType.Operator);
        var probe = new GameObject("CardIconProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            if (turret.transform.Find("MoraleBadge") != null) f.Add("[CARD_BADGE_VISIBILITY] a turret card has a morale badge");

            var holder = card.GetComponent<TDSlotHolderItemView>();
            var badge = card.transform.Find("MoraleBadge");
            var icon = TDMoraleIconView.Attach(probe.transform);
            if (badge == null || icon == null) { f.Add($"[BADGE_SAME_ON_CARD] card badge {(badge != null ? "present" : "missing")}, icon {(icon != null ? "present" : "missing")}"); return; }

            var cardDisc = badge.Find("Disc").GetComponent<UnityEngine.UI.Image>();
            var cardFace = badge.Find("Face").GetComponent<UnityEngine.UI.Image>();
            var iconDisc = icon.transform.Find("Badge/Disc").GetComponent<UnityEngine.UI.Image>();
            var iconFace = icon.transform.Find("Badge/Face").GetComponent<UnityEngine.UI.Image>();
            foreach (float v in new[] { 10f, 50f, 80f, 100f })
            {
                holder.ShowMorale(At(v), true);
                icon.Refresh(At(v));
                if (!badge.gameObject.activeSelf || !Same(cardDisc.color, iconDisc.color) || !Same(cardFace.color, iconFace.color))
                    f.Add($"[BADGE_SAME_ON_CARD] stress {v}: shown {badge.gameObject.activeSelf}, disc {cardDisc.color} vs {iconDisc.color}, face {cardFace.color} vs {iconFace.color}");
            }

            holder.ShowMorale(At(0f), false);
            if (badge.gameObject.activeSelf) f.Add("[BADGE_SAME_ON_CARD] still shown for a rested operator off the field");

            foreach (var g in badge.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                if (g.raycastTarget) f.Add($"[CARD_BADGE_NO_RAYCAST] {g.name} takes raycasts: a press on the card's corner would not lift the operator");
        }
        finally
        {
            Object.DestroyImmediate(turret);
            Object.DestroyImmediate(card);
            Object.DestroyImmediate(probe);
        }
    }

    // Tầng 4b §4.6: the amplifier's ring is exactly the reach the stress code uses — LEAK_AMP_RADIUS
    // cells — flat at the range-highlight height, however the boss is scaled or turned. Its material
    // is an asset: a shader only code asks for by name can be stripped from a build.
    private static void AmpRing(List<string> f)
    {
        var mat = Resources.Load<Material>(TDAmpRingView.MATERIAL_PATH);
        if (mat == null || mat.shader.name != "Sprites/Default")
            f.Add($"[AMP_RING_MATERIAL] Resources/{TDAmpRingView.MATERIAL_PATH}: {(mat == null ? "missing" : mat.shader.name)}, want Sprites/Default");

        var go = new GameObject("AmpRingProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var centre = new Vector3(3f, 0f, -5f);
            go.transform.position = centre;
            go.transform.localScale = Vector3.one * 3f;
            go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var ring = TDAmpRingView.Attach(go.transform);
            var points = ring != null ? ring.WorldPoints() : new Vector3[0];
            float want = TDConstant.LEAK_AMP_RADIUS * TDConstant.CONFIG_GRID_CELL_SIZE;
            if (points.Length != TDAmpRingView.SEGMENTS)
                f.Add($"[AMP_RING_RADIUS] {points.Length} points, want {TDAmpRingView.SEGMENTS}");
            foreach (var p in points)
            {
                float r = new Vector2(p.x - centre.x, p.z - centre.z).magnitude;
                if (Mathf.Abs(r - want) > 0.01f || Mathf.Abs(p.y - TDConstant.CONFIG_RANGE_HIGHLIGHT_Y) > 0.001f)
                {
                    f.Add($"[AMP_RING_RADIUS] point {p}: radius {r:F3} (want {want}), height {p.y:F3} (want {TDConstant.CONFIG_RANGE_HIGHLIGHT_Y}) under a parent scaled 3 and turned 90°");
                    break;
                }
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static GameObject NewCard(GameObject prefab, TowerType type)
    {
        var go = Object.Instantiate(prefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        var holder = go.GetComponent<TDSlotHolderItemView>();
        holder.SetupSlotHolderVariables();
        holder.SetupSlot(new TDTowerSlotInfo
        {
            towerType = type,
            operatorData = type == TowerType.Operator ? new OperatorData() : null,
            cost = 10,
        });
        return go;
    }

    private static bool Same(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

    private static string ChildNames(Transform parent)
    {
        var names = new List<string>();
        foreach (Transform child in parent) names.Add(child.name);
        return string.Join(",", names);
    }

    // Tầng 4 §4.1: the break moment's rhythm, as pure functions so the numbers the spec pins are
    // checked here rather than only by eye in a screenshot.
    private static void BreakTiming(List<string> f)
    {
        Near(f, "PUNCH_START", TDMoraleIconView.BreakPunch(0f), 1f);
        Near(f, "PUNCH_PEAK", TDMoraleIconView.BreakPunch(0.175f), 1.5f); // ~81 px badge → ~121 px, under S1's 125
        Near(f, "PUNCH_END", TDMoraleIconView.BreakPunch(0.35f), 1f);
        Near(f, "PUNCH_AFTER", TDMoraleIconView.BreakPunch(1f), 1f);
        Near(f, "PUNCH_BEFORE", TDMoraleIconView.BreakPunch(-0.1f), 1f);

        if (!TDMoraleIconView.BreakFlash(0.1f)) f.Add("[FLASH_ON] no white flash at 0.1 s");
        if (TDMoraleIconView.BreakFlash(0.15f)) f.Add("[FLASH_OFF] still flashing at 0.15 s");

        Near(f, "LABEL_ALPHA_FIRST_ON", TDMoraleIconView.LabelAlpha(0.05f), 1f);
        Near(f, "LABEL_ALPHA_FIRST_OFF", TDMoraleIconView.LabelAlpha(0.2f), 0f);
        Near(f, "LABEL_ALPHA_SECOND_ON", TDMoraleIconView.LabelAlpha(0.35f), 1f);
        Near(f, "LABEL_ALPHA_SECOND_OFF", TDMoraleIconView.LabelAlpha(0.5f), 0f);
        Near(f, "LABEL_ALPHA_HOLD", TDMoraleIconView.LabelAlpha(0.9f), 1f);
        Near(f, "LABEL_ALPHA_FADE", TDMoraleIconView.LabelAlpha(1.35f), 0.5f);
        Near(f, "LABEL_ALPHA_GONE", TDMoraleIconView.LabelAlpha(1.5f), 0f);
        Near(f, "LABEL_ALPHA_AFTER", TDMoraleIconView.LabelAlpha(2f), 0f);

        Near(f, "RING_RADIUS_MID", TDMoraleIconView.RingRadius(0.2f, 0.4f, 67f, 200f), 133.5f);
        Near(f, "RING_RADIUS_CLAMP", TDMoraleIconView.RingRadius(1f, 0.4f, 67f, 200f), 200f);
        Near(f, "RING_ALPHA_MID", TDMoraleIconView.RingAlpha(0.2f, 0.4f), 0.5f);
        Near(f, "RING_ALPHA_END", TDMoraleIconView.RingAlpha(0.4f, 0.4f), 0f);

        // §4.3: entering Stressed is a warning, so its ring must stay clearly under the break's.
        Near(f, "STRESS_RING_RADIUS_MID", TDMoraleIconView.RingRadius(0.15f, 0.3f, 67f, 148f), 107.5f);
        Near(f, "STRESS_RING_ALPHA_END", TDMoraleIconView.RingAlpha(0.3f, 0.3f), 0f);
        float stressRadius = TDMoraleIconView.STRESS_RING_MAX_RADIUS, haloRadius = TDMoraleIconView.HALO_MAX_RADIUS;
        float stressSeconds = TDMoraleIconView.STRESS_RING_SECONDS, haloSeconds = TDMoraleIconView.HALO_SECONDS;
        if (stressRadius >= haloRadius || stressSeconds >= haloSeconds)
            f.Add($"[STRESS_WEAKER_THAN_BREAK] Stressed ring {stressRadius}/{stressSeconds}s vs break halo {haloRadius}/{haloSeconds}s");
    }

    // Tầng 4 §4.6: the arc leaves the rescuer's badge, lands on the rescued one's, and peaks
    // `height` above the midpoint — the shape that says who answered whom.
    private static void RescueArc(List<string> f)
    {
        var a = Vector3.zero;
        var b = new Vector3(4f, 0f, 2f);
        if (Vector3.Distance(TDRescueArcView.ArcPoint(a, b, 1f, 0f), a) > 0.001f
            || Vector3.Distance(TDRescueArcView.ArcPoint(a, b, 1f, 1f), b) > 0.001f)
            f.Add($"[ARC_ENDS] ends at {TDRescueArcView.ArcPoint(a, b, 1f, 0f)} / {TDRescueArcView.ArcPoint(a, b, 1f, 1f)}, want {a} / {b}");

        var peak = (a + b) * 0.5f + Vector3.up;
        var got = TDRescueArcView.ArcPoint(a, b, 1f, 0.5f);
        if (Vector3.Distance(got, peak) > 0.001f) f.Add($"[ARC_PEAK] midpoint {got}, want {peak}");
    }

    private static void Voice(List<string> f, string tag, bool[] playing, float[] startedAt, bool[] priority, bool want, int expect)
    {
        int got = TDSFXPlayer.PickVoice(playing, startedAt, priority, want);
        if (got != expect) f.Add($"[{tag}] picked voice {got}, want {expect}");
    }

    // Rescue pressed on the collapsed operator picks the rescuer for the player (playtest
    // 2026-10-22): the calmest neighbour who can pay, since they take RESCUE_RESCUER_STRESS; more
    // SP on a tie; nobody if nobody can pay; never someone who is collapsed themselves.
    private static void RescuerPick(List<string> f)
    {
        var made = new List<GameObject>();
        TDOperatorView Op(string name, float stress, float sp)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            made.Add(go);
            var op = go.AddComponent<TDOperatorView>();
            var pool = new TDOperatorSp();
            pool.Tick(sp - TDConstant.SP_INITIAL);
            SetField(f, op, "<Morale>k__BackingField", At(stress));
            SetField(f, op, "<Sp>k__BackingField", pool);
            return op;
        }

        try
        {
            var calm = Op("calm", 20f, TDConstant.RESCUE_SP_COST);
            var tense = Op("tense", 60f, 100f);
            var poor = Op("poor", 0f, TDConstant.RESCUE_SP_COST - 1f);
            var twin = Op("twin", 20f, 80f);
            var edgy = Op("edgy", 90f, 100f);

            // Still collapsed while calming down on their own, and CALMER than edgy: only the
            // collapsed check, not the stress order, keeps them out.
            var broken = Op("broken", TDConstant.STRESS_MAX, 100f);
            k_SetValue.Invoke(broken.Morale, new object[] { TDConstant.STRESS_STEADY_MAX + 1f });

            Pick(f, "RESCUER_PICK_CALMEST", new() { tense, calm, poor }, calm);
            Pick(f, "RESCUER_PICK_TIE_MORE_SP", new() { calm, twin }, twin);
            Pick(f, "RESCUER_PICK_NONE_READY", new() { poor }, null);
            if (!broken.IsCollapsed) f.Add("[RESCUER_PICK_SETUP] the calming operator is not collapsed");
            Pick(f, "RESCUER_PICK_SKIPS_COLLAPSED", new() { broken, edgy }, edgy);
        }
        finally { foreach (var go in made) Object.DestroyImmediate(go); }
    }

    // Rescue reach follows the TARGET (playtest 2026-10-22): a ranged operator stands on a tower
    // platform off the road, so with reach 1 nobody ever stood close enough to rescue Ginger.
    // A collapsed ranged is reachable from RESCUE_REACH_RANGED cells, a collapsed melee from 1.
    private static void RescueReach(List<string> f)
    {
        var made = new List<GameObject>();
        var reg = new TDOperatorRegistry();
        TDOperatorView Put(string name, Vector2Int cell, OperatorType type, bool collapsed)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            made.Add(go);
            var op = go.AddComponent<TDOperatorView>();
            SetField(f, op, "m_Data", new OperatorData { operatorName = name, operatorType = type });
            SetField(f, op, "<Morale>k__BackingField", At(collapsed ? TDConstant.STRESS_MAX : 0f));
            reg.RegisterOperatorView(cell, op);
            return op;
        }
        List<string> Names(List<TDOperatorView> ops)
        {
            var names = ops.ConvertAll(o => o.name);
            names.Sort();
            return names;
        }

        try
        {
            if (TDConstant.RESCUE_REACH_RANGED != 2) f.Add($"[RESCUE_REACH_TABLE] cells below assume reach 2, got {TDConstant.RESCUE_REACH_RANGED}");

            var ranged = Put("ranged", new Vector2Int(0, 0), OperatorType.Ranger, collapsed: true);
            var melee = Put("melee", new Vector2Int(5, 0), OperatorType.Knight, collapsed: true);
            Put("two", new Vector2Int(2, 1), OperatorType.Knight, collapsed: false);      // 2 from ranged, 3 from melee
            Put("three", new Vector2Int(0, 3), OperatorType.Knight, collapsed: false);    // 3 from ranged
            Put("near", new Vector2Int(6, 1), OperatorType.Knight, collapsed: false);     // 1 from melee
            Put("twoAway", new Vector2Int(7, 2), OperatorType.Knight, collapsed: false);  // 2 from melee

            Seq(f, "RESCUE_REACH_RANGED_RESCUERS", Names(reg.GetRescuersFor(new Vector2Int(0, 0), ranged)), "two");
            Seq(f, "RESCUE_REACH_MELEE_RESCUERS", Names(reg.GetRescuersFor(new Vector2Int(5, 0), melee)), "near");
            Seq(f, "RESCUE_REACH_TARGETS_RANGED_AT_2", Names(reg.GetRescueTargetsFor(new Vector2Int(2, 1))), "ranged");
            Seq(f, "RESCUE_REACH_TARGETS_MELEE_AT_2", Names(reg.GetRescueTargetsFor(new Vector2Int(7, 2))));
            Seq(f, "RESCUE_REACH_TARGETS_MELEE_AT_1", Names(reg.GetRescueTargetsFor(new Vector2Int(6, 1))), "melee");
        }
        finally { foreach (var go in made) Object.DestroyImmediate(go); }
    }

    // Playtests 2026-10-22: a new player does not know a broken unit can be tapped for a rescue,
    // so the tutorial says so on the first two breaks, counted in PlayerPrefs like its other hints.
    private static void RescueHint(List<string> f)
    {
        const BindingFlags I = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
        var brokeField = typeof(TDGameEventBus).GetField("OnOperatorBroke", S);
        var hintField = typeof(TDFloatingHint).GetField("m_Instance", S);
        var keyField = typeof(TDTutorialView).GetField("PREFS_HINT_RESCUE", S);
        var start = typeof(TDTutorialView).GetMethod("Start", I);
        var destroy = typeof(TDTutorialView).GetMethod("OnDestroy", I);
        if (brokeField == null || hintField == null || keyField == null || start == null || destroy == null)
        { f.Add("[REFLECT] TDGameEventBus.OnOperatorBroke / TDFloatingHint.m_Instance / TDTutorialView.PREFS_HINT_RESCUE / Start / OnDestroy missing"); return; }

        // The player's real counter is put back afterwards, and the hint is a bare component with no
        // text (Show returns early), so the run neither builds a hint into the open scene nor plays one.
        string key = (string)keyField.GetValue(null);
        bool hadKey = PlayerPrefs.HasKey(key);
        int savedCount = PlayerPrefs.GetInt(key, 0);
        object savedBroke = brokeField.GetValue(null);
        object savedHint = hintField.GetValue(null);
        var go = new GameObject("RescueHintProbe") { hideFlags = HideFlags.HideAndDontSave };
        TDTutorialView tutorial = null;
        try
        {
            brokeField.SetValue(null, null);
            hintField.SetValue(null, go.AddComponent<TDFloatingHint>());
            PlayerPrefs.DeleteKey(key);
            tutorial = go.AddComponent<TDTutorialView>();
            start.Invoke(tutorial, null);

            var raise = brokeField.GetValue(null) as System.Action<Vector3>;
            if (raise == null) { f.Add("[RESCUE_HINT_LISTENS] the tutorial does not listen to OnOperatorBroke"); return; }
            int[] want = { 1, 2, 2 };
            for (int i = 0; i < want.Length; i++)
            {
                raise(Vector3.zero);
                int got = PlayerPrefs.GetInt(key, 0);
                if (got != want[i]) f.Add($"[RESCUE_HINT_COUNT] break {i + 1} left the counter at {got}, want {want[i]}");
            }

            destroy.Invoke(tutorial, null);
            if (brokeField.GetValue(null) != null) f.Add("[RESCUE_HINT_UNSUBSCRIBES] the tutorial still listens to OnOperatorBroke after OnDestroy");
        }
        finally
        {
            if (tutorial != null) destroy.Invoke(tutorial, null); // also drops the other events Start hooked
            brokeField.SetValue(null, savedBroke);
            hintField.SetValue(null, savedHint);
            if (hadKey) PlayerPrefs.SetInt(key, savedCount); else PlayerPrefs.DeleteKey(key);
            Object.DestroyImmediate(go);
        }
    }

    private static void Pick(List<string> f, string tag, List<TDOperatorView> standing, TDOperatorView want)
    {
        var got = TDOperatorSelectionView.PickRescuer(standing);
        if (got != want) f.Add($"[{tag}] picked {(got != null ? got.name : "nobody")}, want {(want != null ? want.name : "nobody")}");
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

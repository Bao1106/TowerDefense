using TDEnums;
using UnityEngine;

/// <summary>
/// Everything the world has to tell morale for one tick. A struct so the caller fills it
/// from whatever it already knows and no lookup happens in here — this class does not
/// reach for a registry, a singleton or a Transform, which is what keeps it testable
/// from an editor menu with no scene loaded.
/// </summary>
public struct TDMoraleContext
{
    /// <summary>Blocking someone or holding a target in range. Idle relief only runs while false.</summary>
    public bool engaged;

    /// <summary>Adjacent allies currently Calm. Feeds Resolve only — it no longer touches stress.</summary>
    public int calmAlliesAdjacent;

    /// <summary>Fear-aura rate from the strongest source in range: boss 2.0, else 0. A Herald has
    /// no aura — it amplifies leaks instead (spec 2026-10-06 §5.5).</summary>
    public float auraRate;

    /// <summary>Seconds since anything last damaged this operator — gates the broken-state recovery.</summary>
    public float secondsSinceHit;

    /// <summary>An ally broke within N3 radius during THIS wave. Lowers Resolve (§07).</summary>
    public bool allyBrokeThisWave;
}

/// <summary>
/// Morale for a single operator. Stress comes from leaks (OnLeak), N3 spikes and the N4 boss
/// aura; recovery is §05. The old N1 / N2 clocks — stress for enemies merely being near — are
/// gone (spec 2026-10-06 §5.2): holding the line well now costs nothing.
///
/// Plain C#. No MonoBehaviour, no `static api`, no Update — the owner ticks it. That is a
/// deliberate constraint from the plan and it buys the thing this project has been short
/// of all along: the whole rule set can be exercised by an editor validator with no scene,
/// no play mode and no prefab, so the numbers get checked before the UI exists.
///
/// Multiplier discipline (§02): the state multiplier applies to the aura rate and to each leak
/// — a leak is an event, but it keeps the band multiplier so the "point of no return" holds
/// until Tier 2 decides otherwise (spec §5.2). Never to N3 spikes (a 30-point jolt doubled at
/// Stressed almost always kills outright, which turns a cascade into an automatic wipe) and
/// never to recovery (or the operator who most needs relief heals slowest — punished twice).
/// </summary>
public sealed class TDOperatorMorale
{
    private float m_Value;
    private float m_RetreatCooldown;
    private bool m_Broken;
    private bool m_BreakPending; // entered collapse, and nobody has reacted to it yet

    public float Value => m_Value;

    /// <summary>
    /// Latched, with hysteresis — enters at 100, leaves at 66. NOT `m_Value >= 100`.
    ///
    /// As a bare threshold the state was unusable: recovery runs at 1.0/s, so the first tick
    /// after the 5-second calm window dropped the value to 99.98 and the operator stood straight
    /// back up — then a single enemy in the zone put them back at 100 within half a second.
    /// Collapse flickered on and off instead of lasting, and every consequence built on it
    /// (releasing blocked enemies, x3 damage, refusing retreat) flickered with it.
    ///
    /// §05 prices the recovery as "100 -> 66 takes 34 quiet seconds", which only means anything
    /// if the operator is still collapsed for those 34 seconds. 66 is also exactly where Rescue
    /// and self-recovery differ: Rescue lands them at 50 instantly, waiting it out lands them at
    /// 66 after 39 seconds of standing there useless. That gap is what makes Rescue worth 50 SP.
    /// </summary>
    public bool IsBroken => m_Broken;

    /// <summary>Seconds left before this operator may be deployed again after a retreat.</summary>
    public float RetreatCooldown => m_RetreatCooldown;

    /// <summary>
    /// Times this operator has broken or died this match — feeds Resolve in step 2.2. A break is
    /// counted where it happens, in SetValue, so it cannot depend on who pushed the value over.
    /// </summary>
    public int Setbacks { get; private set; }

    /// <summary>Reports Broken while the latch holds, even though the value has fallen below 100.</summary>
    public MoraleState State => m_Broken ? MoraleState.Broken : StateOf(m_Value);

    public static MoraleState StateOf(float value)
    {
        if (value >= TDConstant.STRESS_MAX) return MoraleState.Broken;
        if (value > TDConstant.STRESS_STEADY_MAX) return MoraleState.Stressed;
        if (value > TDConstant.STRESS_CALM_MAX) return MoraleState.Steady;
        return MoraleState.Calm;
    }

    public static float MultiplierOf(MoraleState state) => state switch
    {
        MoraleState.Steady => TDConstant.STRESS_MULT_STEADY,
        MoraleState.Stressed => TDConstant.STRESS_MULT_STRESSED,
        _ => TDConstant.STRESS_MULT_CALM, // Broken no longer accrues — it is already at the top
    };

    // ── Rates ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Net points per second at this instant, state multiplier included. Positive means
    /// climbing toward a break. Exposed because the debug overlay of step 2.5 and the
    /// validator both want the number, not just its effect.
    /// </summary>
    public float NetRate(in TDMoraleContext ctx)
    {
        if (IsBroken)
        {
            // Broken operators stop accruing and only ever recover, and only while nothing
            // is hitting them. Being cornered while broken is meant to be a dead end the
            // player has to spend a Rescue on.
            return ctx.secondsSinceHit >= TDConstant.STRESS_BROKEN_CALM_SECONDS
                ? -TDConstant.STRESS_BROKEN_RELIEF
                : 0f;
        }

        // The aura is the only time-based source left; leaks arrive through OnLeak. The lull is
        // worth something — but only while blocking nobody and with no target in range.
        float n4 = Mathf.Max(0f, ctx.auraRate) * MultiplierOf(State);
        return n4 - (ctx.engaged ? 0f : TDConstant.STRESS_IDLE_RELIEF);
    }

    // ── Tick ─────────────────────────────────────────────────────────────────

    public void Tick(float deltaTime, in TDMoraleContext ctx)
    {
        if (deltaTime <= 0f) return;

        m_RetreatCooldown = Mathf.Max(0f, m_RetreatCooldown - deltaTime);
        SetValue(m_Value + NetRate(ctx) * deltaTime);
    }

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>N3 — an ally died or broke nearby. Never multiplied by state (§02).</summary>
    public void AddSpike(float amount) => SetValue(m_Value + Mathf.Max(0f, amount));

    /// <summary>
    /// An enemy walked past a cell this operator answers for. `share` is this operator's part of
    /// the leak (TDLeakShare), `amplifier` the Herald multiplier. Returns the points actually
    /// added after the clamp, so the caller can log and pulse on what really happened.
    ///
    /// Ignored while collapsed: the latch already holds them at the top, and a second break
    /// from the same incident would count a second setback.
    /// </summary>
    public float OnLeak(float share, float amplifier)
    {
        if (m_Broken || share <= 0f) return 0f;

        float before = m_Value;
        SetValue(m_Value + TDConstant.STRESS_PER_LEAK * share * amplifier * MultiplierOf(State));
        return m_Value - before;
    }

    /// <summary>
    /// A wave ended — everyone still STANDING steadies a little. The collapsed get nothing.
    ///
    /// It used to be withheld from the whole squad whenever anybody broke, which made it the one
    /// thing §06 calls a "van chống chết chùm" and yet guaranteed to be shut during exactly the
    /// cascade it was named for. A valve that closes under pressure is not a valve.
    ///
    /// The filter moved from "did anyone break" to "are YOU broken", so the relief now lands on
    /// the operators who held — which is both the fairer read and the one that actually stops a
    /// chain: the third operator no longer tips over because the first two did. The collapsed
    /// still pay in full; a wave ending does not lift a collapse, only a Rescue or 39 quiet
    /// seconds does.
    ///
    /// The rule lives here, next to the state it depends on, rather than in the registry that
    /// calls it — so it is reachable from the editor validator instead of needing a live scene.
    /// </summary>
    public void OnWaveCleared()
    {
        if (m_Broken) return;
        Relieve(TDConstant.STRESS_WAVE_CLEAR_RELIEF);
    }

    /// <summary>
    /// Voluntary retreat: -70 and a cooldown before redeploy.
    ///
    /// Not a reset, and that single choice is the whole lesson: leave at 70 and you come
    /// back clean, leave at 90 and you never will. It also lets an operator's career
    /// degrade across a match instead of resetting every time they step off the field.
    /// </summary>
    public void OnRetreat()
    {
        Relieve(TDConstant.STRESS_RETREAT_RELIEF);
        m_RetreatCooldown = TDConstant.STRESS_RETREAT_COUNTDOWN;
    }

    /// <summary>Rescue: -50 and the broken state is lifted (the caller pays the SP).</summary>
    public void OnRescue() => Relieve(TDConstant.STRESS_RESCUE_RELIEF);

    /// <summary>
    /// The rescuer's share of the cost — nerve, not SP. Pairs with TDOperatorSp.Spend: each object
    /// charges for what it owns, so a change to how skills charge can never quietly alter §04.
    ///
    /// Clamped at the ceiling rather than refused, and that distinction matters: a rescuer at 88
    /// still gets to act, they simply cannot be tipped over by the act itself. Making the one
    /// unambiguously right play in the game also the thing that breaks you teaches the player not
    /// to make it.
    /// </summary>
    public void PayRescueStress()
        => SetValue(Mathf.Min(m_Value + TDConstant.RESCUE_RESCUER_STRESS,
                              Mathf.Max(m_Value, TDConstant.RESCUE_RESCUER_CEILING)));

    /// <summary>
    /// Killed. Keeps every stress point AND pays a longer cooldown than a retreat.
    ///
    /// Keeping the stress alone was not enough of a cost: it punishes an operator who died
    /// while stressed and nobody else, so one killed early and calm walked back onto the field
    /// immediately while the retreated one was still waiting. Dying was the faster exit, which
    /// is the exact incentive DEATH_KEEPS_STRESS exists to forbid — the loophole was just in
    /// tempo rather than in points.
    /// </summary>
    public void OnDeath()
    {
        // A break and the death that follows it are ONE incident, not two accidents. Counting
        // both cost a Knight twenty Resolve points from a single fall — 26.8% down to 6.8% —
        // which all but retires LAST STAND after one bad moment. That is the wrong thing to make
        // rare: §06 builds it so the player signs the death warrant themselves, and a button that
        // never appears cannot be signed.
        //
        // A death with no break before it still counts, because that IS the first incident.
        if (!m_Broken) OnSetback();

        m_RetreatCooldown = TDConstant.STRESS_DEATH_COUNTDOWN;

        // The COLLAPSE ends here; the stress does not. Dying at 100 leaves ~84 after the 16s
        // cooldown ticks off-field, which is above the latch's exit line of 66 — so without this
        // the operator redeployed still flagged collapsed: paid for, standing there unable to
        // block or swing, taking triple damage from the first enemy past. That is a state-machine
        // leak, not a punishment. "Collapsed" describes someone standing on the field, and they
        // are not on it any more.
        //
        // DEATH_KEEPS_STRESS is untouched and still does the punishing: coming back at ~84 is the
        // Stressed band at a x2.0 multiplier, seconds from breaking again under any pressure.
        m_Broken = false;

        // Nor may an unannounced break outlive them: it would fire on the next deploy, spiking
        // and releasing around an operator who just walked on fresh.
        m_BreakPending = false;
    }

    /// <summary>
    /// Death or a break. Stress is deliberately NOT cleared: otherwise "let them die to
    /// wipe the stress and redeploy" would be the optimal play, and the system would be
    /// teaching the opposite of what it means to.
    /// </summary>
    public void OnSetback() => Setbacks++;

    /// <summary>
    /// True exactly once per entry into collapse, then cleared. The owner reacts to the break
    /// here — spike the neighbours, release the held enemies — instead of comparing IsBroken
    /// around its own Tick: an ally's N3 spike latches this operator OUTSIDE that Tick, so the
    /// before/after comparison never saw it and every cascade stopped at the second link.
    /// </summary>
    public bool ConsumeBreak()
    {
        bool pending = m_BreakPending;
        m_BreakPending = false;
        return pending;
    }

    private void Relieve(float amount) => SetValue(m_Value - Mathf.Max(0f, amount));

    /// <summary>
    /// The only place the value is written, so the collapse latch cannot be forgotten at one of
    /// the three call sites. Enter collapse at 100, leave it at 66 — never at 99.99.
    ///
    /// Rescue needs no special case: -50 from 100 lands at 50, which is under the exit line, so
    /// the same single rule lifts the state.
    /// </summary>
    private void SetValue(float raw)
    {
        m_Value = Mathf.Clamp(raw, 0f, TDConstant.STRESS_MAX);

        if (!m_Broken && m_Value >= TDConstant.STRESS_MAX)
        {
            m_Broken = true;
            m_BreakPending = true;
            Setbacks++;
        }
        else if (m_Value <= TDConstant.STRESS_STEADY_MAX)
        {
            m_Broken = false;
            m_BreakPending = false; // rescued before anyone handled the break: nothing left to announce
        }
    }

    // ── Resolve (§07) ────────────────────────────────────────────────────────

    /// <summary>
    /// Live chance that LAST STAND is offered instead of this operator simply breaking.
    ///
    /// Two layers deliberately kept apart: `data.baseResolve` is who the operator IS
    /// (derived from their stat ratio, fixed for the match), and the adjustments are what
    /// is HAPPENING to them. The first two adjustments are things the player controls —
    /// which is what turns a dice roll into something good play can buy.
    ///
    /// Shown to the player, never hidden: a 21% chance the player can see and plan around
    /// is information; the same 21% concealed is a rigged coin they will count and resent.
    /// </summary>
    public float ResolveChance(OperatorData data, in TDMoraleContext ctx)
    {
        if (data == null) return TDConstant.RESOLVE_MIN;

        float resolve = data.baseResolve;

        if (ctx.calmAlliesAdjacent > 0) resolve += TDConstant.RESOLVE_ADJ_CALM_ALLY;
        if (ctx.allyBrokeThisWave) resolve += TDConstant.RESOLVE_ADJ_ALLY_BROKE;
        resolve += TDConstant.RESOLVE_ADJ_PER_SETBACK * Setbacks;

        return Mathf.Clamp(resolve, TDConstant.RESOLVE_MIN, TDConstant.RESOLVE_MAX);
    }

    // ── Projection ───────────────────────────────────────────────────────────

    /// <summary>
    /// Leaks at this share (amplifier 1, no recovery in between) until this operator breaks —
    /// the number tuning happens on. 0 if already collapsed, int.MaxValue if share is 0.
    ///
    /// Each leak is priced by the band it LANDS in, exactly like OnLeak, so this walks the same
    /// steps rather than dividing per band: a leak that starts at 32 is a Calm leak even though
    /// it carries the value deep into Steady.
    /// </summary>
    public int LeaksToBreak(float share)
    {
        if (IsBroken) return 0;
        if (share <= 0f) return int.MaxValue;

        int n = 0;
        for (float v = m_Value; v < TDConstant.STRESS_MAX; n++)
            v += TDConstant.STRESS_PER_LEAK * share * MultiplierOf(StateOf(v));
        return n;
    }
}

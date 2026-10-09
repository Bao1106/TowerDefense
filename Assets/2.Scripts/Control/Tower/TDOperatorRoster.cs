using System;
using System.Collections.Generic;


/// <summary>
/// Who is on the field, who is resting, and the morale each of them carries between the two.
///
/// Before this existed, TDOperatorView built a `new TDOperatorMorale()` in Init and let it die
/// with the GameObject on retreat. Pulling someone out therefore wiped their stress completely,
/// for free, and `OnRetreat()` — the -70 and the 8s cooldown that §05 is built on — was never
/// once called in play. Only the editor validator ever reached it.
///
/// That is not a cosmetic gap. It would have let 🛑 Chốt B pass for the wrong reason: the test
/// asks whether a player ever pulls a near-full-health operator, and with retreat as a free
/// stress wipe the answer is trivially yes — proving nothing about whether stress is a real
/// second axis.
///
/// Keyed by the OperatorData row itself, by reference. Not by OperatorType — that is an
/// ARCHETYPE, shared by Striker, Ace and Layla, so keying on it would have merged three
/// separate people into one morale entry. Not by name either: the row IS the identity, and a
/// key that is the thing itself cannot drift from it.
/// </summary>
public sealed class TDOperatorRoster
{
    public static TDOperatorRoster api;

    /// <summary>A card's deployability changed — deploy, retreat, death, or a cooldown expiring.</summary>
    public event Action OnAvailabilityChanged;

    private readonly Dictionary<OperatorData, TDOperatorMorale> m_Morale
        = new Dictionary<OperatorData, TDOperatorMorale>();

    private readonly Dictionary<OperatorData, TDOperatorSp> m_Sp
        = new Dictionary<OperatorData, TDOperatorSp>();

    private readonly HashSet<OperatorData> m_Deployed = new HashSet<OperatorData>();

    // Off the field nothing is happening: no enemies, no aura, and — the part that matters —
    // nothing has hit them in a very long time. Leaving secondsSinceHit at its default 0 would
    // strand a broken operator broken forever, since broken recovery is gated on exactly that.
    private static readonly TDMoraleContext k_OffField
        = new TDMoraleContext { secondsSinceHit = float.MaxValue };

    public TDOperatorMorale MoraleOf(OperatorData op)
    {
        if (op == null) return new TDOperatorMorale(); // never key a dictionary on null
        if (!m_Morale.TryGetValue(op, out var morale))
            m_Morale[op] = morale = new TDOperatorMorale();
        return morale;
    }

    /// <summary>
    /// Skill points, kept beside morale rather than inside it. Two dictionaries on the same key
    /// instead of one record: they are two unrelated concerns that merely share a lifetime, and
    /// SP is about to grow skill charge modes that stress must never see.
    /// </summary>
    public TDOperatorSp SpOf(OperatorData op)
    {
        if (op == null) return new TDOperatorSp();
        if (!m_Sp.TryGetValue(op, out var sp))
            m_Sp[op] = sp = new TDOperatorSp();
        return sp;
    }

    public bool IsDeployed(OperatorData op) => m_Deployed.Contains(op);

    /// <summary>One copy on the field at a time, and only once any retreat cooldown has run out.</summary>
    public bool CanDeploy(OperatorData op)
        => op != null && !m_Deployed.Contains(op) && MoraleOf(op).RetreatCooldown <= 0f;

    public void OnDeployed(OperatorData op)
    {
        if (op == null) return;
        m_Deployed.Add(op);
        OnAvailabilityChanged?.Invoke();
    }

    /// <summary>
    /// Left the field. `voluntary` separates a retreat from a death, and death must be worse on
    /// BOTH axes: a retreat pays -70 and waits 8s, a death keeps every point and waits 16s.
    ///
    /// The first version gave death no cooldown at all, on the theory that keeping the stress
    /// was punishment enough. It is not — that only bites an operator who died stressed, so one
    /// killed early and calm returned instantly while the retreated one was still waiting.
    /// Dying was the fast way off the field.
    /// </summary>
    public void OnLeftField(OperatorData op, bool voluntary)
    {
        if (op == null) return;
        m_Deployed.Remove(op);

        if (voluntary) MoraleOf(op).OnRetreat();
        else MoraleOf(op).OnDeath();

        OnAvailabilityChanged?.Invoke();
    }

    /// <summary>
    /// Ticks everyone NOT on the field. Deployed operators are ticked by their own view; these
    /// have no view, and their retreat cooldown has to run down somewhere or it never expires.
    /// </summary>
    public void TickOffField(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        bool freed = false;
        foreach (var pair in m_Morale)
        {
            if (m_Deployed.Contains(pair.Key)) continue;

            bool wasWaiting = pair.Value.RetreatCooldown > 0f;
            pair.Value.Tick(deltaTime, k_OffField);
            if (wasWaiting && pair.Value.RetreatCooldown <= 0f) freed = true;
        }

        if (freed) OnAvailabilityChanged?.Invoke();
    }

    // No Reset(): TDControl.ReinitControls() builds a new roster per match, so stress already
    // stops at the match boundary. A Reset that nothing calls is a method someone calls wrongly later.
}

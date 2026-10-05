using UnityEngine;

/// <summary>
/// Skill points for one operator — the Arknights resource, not a morale field.
///
/// It briefly lived on TDOperatorMorale, for a bad reason: morale was the only per-operator state
/// that already survived a retreat, so it was the convenient place to hang this. But SP is a
/// COMBAT resource. Skills will read and spend it, and skill code has no business reaching into
/// a stress model to do that — nor should a change to how skills charge ever touch §04's rates.
///
/// Rescue is simply the first thing that spends it. §06 prices a rescue at 50 against a normal
/// skill's 100, which is the whole point: pulling a teammate out of collapse costs you half a
/// skill activation. The two draw on the same pool, so saving someone is always paid for out of
/// what you would otherwise have fired.
/// </summary>
public sealed class TDOperatorSp
{
    // Granted at CONSTRUCTION, not on deploy. The roster hands back the same TDOperatorSp every
    // time an operator goes out, so seeding it here means a first deployment starts with 20 while
    // a retreat-and-redeploy keeps whatever is left — otherwise the cheapest way to bank SP would
    // be to put someone down and immediately pull them back.
    private float m_Current = TDConstant.SP_INITIAL;

    public float Current => m_Current;

    /// <summary>Ceiling is a normal skill's cost, not the rescue cost — SP banks past a rescue.</summary>
    public float Max => TDConstant.SP_MAX;

    /// <summary>
    /// Charges only while the operator is deployed — the Arknights rule, and the reason it is a
    /// separate call rather than something the roster ticks for everyone. If reserves accrued SP,
    /// parking the squad at home would be the cheapest way to bank rescues, and the resource that
    /// saves a life would no longer be earned by risking one.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        m_Current = Mathf.Min(Max, m_Current + TDConstant.SP_PER_SECOND * deltaTime);
    }

    public bool CanAfford(float cost) => m_Current >= cost;

    public bool Spend(float cost)
    {
        if (!CanAfford(cost)) return false;
        m_Current -= cost;
        return true;
    }
}

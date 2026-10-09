using TDEnums;
using UnityEngine;

/// <summary>
/// MonoBehaviour attached to the operator prefab.
/// Implements IPlacedUnit — TDTowerFactoryControl calls Init(key, slotInfo) after Instantiation.
///
/// Behavior (PathCell / TowerZone) is resolved by TDControl.CreateOperatorBehavior()
/// based on OperatorData.deployZone — no direct if/else branching here.
/// </summary>
public class TDOperatorView : MonoBehaviour, IPlacedUnit
{
    private static readonly int HASH_ATTACK = Animator.StringToHash("Attack");

    private Animator m_Animator;
    private TDHPBarView m_HPBarView;
    private Transform m_SelectionIndicator;

    private IOperatorBehavior m_Behavior;
    private OperatorData m_Data; // cached at Init - avoids List.Find every frame
    private Vector2Int m_MyCell;
    private float m_CurrentHp;
    private float m_MaxHp;
    private float m_LastAttackTime = -999f;
    private float m_LastHitTime = -999f;
    private TDMoraleIconView m_MoraleIcon;
    private TDBlockFullMarker m_BlockFullMarker; // melee only

    // The current stint — this stay on the field — for TDPressureProbe (spec §7.2).
    private bool m_StintOpen;
    private float m_StintStart, m_StintStartHp, m_StintStress, m_LastStress;
    private bool m_Retreated;            // left by DoRetreat, not by death or the match ending
    private MoraleState m_RetreatBand;

    /// <summary>Morale state for this operator (§02). Null before Init.</summary>
    public TDOperatorMorale Morale { get; private set; }

    /// <summary>Skill points (§06 · 3.2). A combat resource of its own — deliberately NOT a field
    /// on Morale, because skills will spend it and skill code must not reach into the stress model.</summary>
    public TDOperatorSp Sp { get; private set; }

    private bool m_Initialized;
    private bool m_IsDying;

    public int Cost { get; private set; }
    /// <summary>
    /// The roster row this operator was deployed from — their identity and their real stats.
    /// This replaced a public OperatorType accessor, whose only reader (the selection panel's
    /// range preview) was using it to look the row back up by archetype and drawing Striker's
    /// reach over Ace as a result.
    /// </summary>
    public OperatorData Data => m_Data;

    // ── IPlacedUnit ───────────────────────────────────────────────────────────

    TowerType IPlacedUnit.UnitType => TowerType.Operator;

    void IPlacedUnit.Init(string instanceKey, TDTowerSlotInfo slotInfo)
    {
        m_Animator = GetComponentInChildren<Animator>();
        m_HPBarView = GetComponentInChildren<TDHPBarView>(includeInactive: true);
        m_SelectionIndicator = transform.Find(TDConstant.SELECTION_INDICATOR_NAME);

        // The slot already carries the roster row. Looking it back up from an archetype enum is
        // what made Ace and Layla deploy as Striker.
        m_Data = slotInfo.operatorData;

        Cost = slotInfo.cost;
        m_MyCell = TDGridMainModel.api.WorldToCell(transform.position);
        m_LastAttackTime = -999f;

        m_SelectionIndicator?.gameObject.SetActive(false);

        var data = m_Data;   // local alias, used by the behavior + HP setup below
        m_CurrentHp = data?.hp ?? 100f;
        m_MaxHp = m_CurrentHp;

        m_Behavior = TDControl.CreateOperatorBehavior(data?.deployZone ?? DeployZone.PathCell);
        m_Behavior.OnInit(m_MyCell, data, this);

        m_HPBarView?.Show();
        m_HPBarView?.UpdateHP(m_CurrentHp, m_MaxHp);

        // Morale belongs to the OPERATOR, not to this GameObject. Pulling it from the roster is
        // what makes a career degrade across a match instead of resetting every time they step
        // off the field — and it is the only reason OnRetreat's -70 means anything.
        Morale = TDOperatorRoster.api?.MoraleOf(m_Data) ?? new TDOperatorMorale();
        Sp = TDOperatorRoster.api?.SpOf(m_Data) ?? new TDOperatorSp();
        TDOperatorRoster.api?.OnDeployed(m_Data);

        m_MoraleIcon = TDMoraleIconView.Attach(transform);
        if (data != null && data.deployZone == DeployZone.PathCell) m_BlockFullMarker = TDBlockFullMarker.Attach(transform);

        m_Initialized = true;
        OpenStint();
        Debug.Log($"[TDOperatorView] Init cell={m_MyCell}, name={m_Data?.operatorName}, zone={data?.deployZone}, HP={m_CurrentHp}");
    }

    void IPlacedUnit.OnRemove()
    {
        // Die and DoRetreat both route here; the cap frees exactly one slot per unit. Freed the
        // moment the Die clip starts, not when the corpse is destroyed — same as the cell.
        if (m_Initialized) TDDeployCap.api?.OnUnitRemoved();
        m_Initialized = false;
        CloseStint();
        m_Behavior?.OnRemove(m_MyCell, transform.position);
        m_BlockFullMarker?.SetVisible(false); // Update stops here, so the corpse would keep it
    }

    // ── Stint (TDPressureProbe) ───────────────────────────────────────────────

    private void OpenStint()
    {
        m_StintOpen = true;
        m_StintStart = Time.time;
        m_StintStartHp = m_CurrentHp;
        m_StintStress = 0f;
        m_LastStress = Morale.Value;
        // Still standing when the match ends is a stint ending too.
        TDGameEventBus.OnVictory += CloseStint;
        TDGameEventBus.OnGameOver += CloseStint;
    }

    // Positive rises only: relief must not cancel the pressure it follows. Sampled once a frame
    // because stress also arrives outside TickMorale (leaks, spikes from allies).
    private void SampleStress()
    {
        float v = Morale.Value;
        if (v > m_LastStress) m_StintStress += v - m_LastStress;
        m_LastStress = v;
    }

    private void CloseStint()
    {
        TDGameEventBus.OnVictory -= CloseStint;
        TDGameEventBus.OnGameOver -= CloseStint;
        if (!m_StintOpen) return;
        m_StintOpen = false;

        SampleStress();
        TDPressureProbe.RecordStint(m_Data?.operatorName, m_Data != null && m_Data.deployZone == DeployZone.PathCell,
                                    Time.time - m_StintStart,
                                    Mathf.Max(0f, m_StintStartHp - Mathf.Max(0f, m_CurrentHp)), m_StintStress,
                                    m_Retreated, m_MaxHp > 0f ? Mathf.Max(0f, m_CurrentHp) / m_MaxHp : 0f,
                                    m_Retreated ? m_RetreatBand : (Morale?.State ?? MoraleState.Calm));
    }

    private void OnDestroy()
    {
        TDGameEventBus.OnVictory -= CloseStint;
        TDGameEventBus.OnGameOver -= CloseStint;
    }

    // ── Damage / Death ────────────────────────────────────────────────────────

    /// <summary>
    /// Feeds morale from what this operator can already see. No new Update() and no new
    /// singleton: engagement comes from the behavior, the allies from the operator registry,
    /// the aura from the enemy registry. Leaks arrive separately, as events (ReceiveLeak).
    /// </summary>
    private void TickMorale(float deltaTime)
    {
        if (Morale == null) return;

        var ctx = new TDMoraleContext
        {
            engaged = m_Behavior != null && m_Behavior.IsEngaged(m_MyCell),
            calmAlliesAdjacent = TDOperatorRegistry.api?.CountCalmAlliesAdjacent(m_MyCell) ?? 0,
            auraRate = TDPressureProbe.AuraRateAt(m_MyCell),
            secondsSinceHit = Time.time - m_LastHitTime,
            allyBrokeThisWave = TDOperatorRegistry.api?.AnyBrokeThisWave ?? false,
        };

        Morale.Tick(deltaTime, ctx);
        m_MoraleIcon?.Refresh(Morale);

        // Breaking is an event, not just a value crossing 100: it spikes everyone nearby (§04 N3).
        //
        // Asked of the model rather than detected by comparing IsBroken before and after Tick —
        // an ally's spike latches this operator OUTSIDE that window, so the comparison never saw
        // a spike-induced break and every cascade died at the second link. Each link now fires on
        // its victim's next frame, one frame apart, which is the intended pace of a chain.
        if (Morale.ConsumeBreak())
        {
            Debug.Log($"[Morale] {m_Data?.operatorName} suy sụp tại {m_MyCell}");
            TDPressureProbe.RecordCollapse();
            TDOperatorRegistry.api?.BroadcastSpike(m_MyCell, TDConstant.STRESS_ALLY_BREAK_SPIKE, wasBreak: true);

            // Collapse (§06 · 3.1). The one thing that has to happen ON the transition — everyone
            // they were holding walks free the instant they stop holding. Everything else about
            // being collapsed (no blocking, no attacking, no self-retreat, x3 damage) is derived
            // live from IsBroken, so recovering needs no matching "un-collapse" step.
            TDOperatorRegistry.api?.ReleaseBlockedEnemies(m_MyCell);
        }
    }

    /// <summary>
    /// Collapsed (§06): holds nothing, swings at nothing, cannot walk off under its own power,
    /// and takes triple damage. Derived from morale rather than latched, because an operator left
    /// alone recovers slowly on their own and must come back to work without a second code path.
    /// </summary>
    public bool IsCollapsed => Morale != null && Morale.IsBroken;

    /// <summary>This operator's share of a leak (TDOperatorRegistry.ReportLeak). Returns the points added.</summary>
    public float ReceiveLeak(float share, float amplifier)
    {
        float added = Morale?.OnLeak(share, amplifier) ?? 0f;
        if (added > 0f) m_MoraleIcon?.Pulse();
        return added;
    }

    /// <summary>True if this operator's current range reaches `cell` (ranged only; melee never covers).</summary>
    public bool Covers(Vector2Int cell) => m_Initialized && m_Behavior != null && m_Behavior.Covers(m_MyCell, cell);

    /// <summary>
    /// False while collapsed — that dead end is exactly what Rescue (3.2) exists to open. Read by
    /// both DoRetreat and TDOperatorRetreatControl: one expression, so the refund and the action
    /// can never disagree about whether the retreat happened.
    /// </summary>
    public bool CanRetreat => !m_IsDying && m_Initialized && !IsCollapsed;

    public void TakeDamage(float damage)
    {
        // A 0-damage blow (a Herald walking past) is not a hit: it must not restart the
        // collapsed operator's calm clock (secondsSinceHit, §06 · 3.1).
        if (!m_Initialized || m_IsDying || m_CurrentHp <= 0 || damage <= 0f) return;
        m_LastHitTime = Time.time;

        if (IsCollapsed) damage *= TDConstant.STRESS_BROKEN_DAMAGE_MULT;

        m_CurrentHp -= damage;
        m_HPBarView?.UpdateHP(m_CurrentHp, m_MaxHp);
        if (m_CurrentHp <= 0) Die();
    }

    public Transform SelectionIndicatorTransform => m_SelectionIndicator;

    public void SetSelected(bool selected) => m_SelectionIndicator?.gameObject.SetActive(selected);

    public void DoRetreat()
    {
        // Covers the corpse still playing its Die clip, and the collapsed operator who cannot
        // walk off by themselves. Guarded here and not only in the UI: a button is one caller,
        // not the rule.
        if (!CanRetreat) return;

        // For the playtest report (§7.3 A4): the band they left in, read BEFORE OnLeftField's
        // −70 relief — after it, every red retreat would read Calm.
        m_Retreated = true;
        m_RetreatBand = Morale?.State ?? MoraleState.Calm;

        // The price of pulling out: -70 stress and 8 seconds before this operator can go back.
        // Leave at 70 and you come back clean; leave at 90 and you never will.
        TDOperatorRoster.api?.OnLeftField(m_Data, voluntary: true);

        ((IPlacedUnit)this).OnRemove();
        TDGameEventBus.OperatorDied(m_MyCell);
        Destroy(gameObject);
    }

    private void Die()
    {
        if (m_IsDying) return;
        m_IsDying = true;

        // N3 must fire BEFORE OnRemove, or the registry has already dropped this cell and
        // the neighbours never learn anyone fell. A death spikes less than a break (§04):
        // death is final and acceptable, a broken teammate is still standing there.
        TDOperatorRegistry.api?.BroadcastSpike(m_MyCell, TDConstant.STRESS_ALLY_DEATH_SPIKE, wasBreak: false);

        // Frees the card again but keeps every stress point and grants no cooldown relief —
        // dying must never be the cheap way to reset someone.
        TDOperatorRoster.api?.OnLeftField(m_Data, voluntary: false);

        // Free the cell and notify listeners immediately — same order as TDEnemyView.Die():
        // the logical slot must open now, only the visual is allowed to linger.
        ((IPlacedUnit)this).OnRemove();
        TDGameEventBus.OperatorDied(m_MyCell);
        m_HPBarView?.Hide();

        // No Die trigger on this prefab's controller → nothing to wait for.
        if (!HasAnimatorTrigger(TDConstant.ANIM_TRIGGER_DIE))
        {
            Destroy(gameObject);
            return;
        }

        m_Animator.SetTrigger(TDConstant.ANIM_TRIGGER_DIE);
        Destroy(gameObject, TDConstant.OPERATOR_DIE_DURATION);
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        if (m_Animator == null || m_Animator.runtimeAnimatorController == null) return false;
        var parameters = m_Animator.parameters; // allocates, but this runs once per death
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger
                && parameters[i].name == triggerName) return true;
        }
        return false;
    }

    // ── Animation event callback ──────────────────────────────────────────────

    // Called from the "OnAttackHit" AnimationEvent in the attack clip for Ranger/Mage operators
    public void OnAttackHit()
    {
        if (!m_Initialized || m_Behavior == null) return;
        m_Behavior.ExecuteHit(m_MyCell, transform.position, m_Data);
    }

    // ── Update — fully delegated to the behavior strategy ─────────────────────

    private void Update()
    {
        if (!m_Initialized || m_Behavior == null) return;

        TickMorale(Time.deltaTime);
        SampleStress();
        m_BlockFullMarker?.SetVisible(TDOperatorRegistry.api != null && TDOperatorRegistry.api.IsFullAt(m_MyCell));

        // SP only accrues on the field, and not while collapsed: an operator who cannot hold or
        // swing is in no state to be banking the resource that rescues someone else.
        if (!IsCollapsed) Sp?.Tick(Time.deltaTime);

        // Collapsed operators stop swinging (§06). After TickMorale, so they keep accruing and
        // keep recovering — standing there useless is the state, not being switched off.
        if (IsCollapsed) return;

        float attackInterval = 1f / (m_Data?.attackSpeed ?? 1f);
        if (Time.time - m_LastAttackTime < attackInterval) return;

        if (m_Behavior.TryAttack(m_MyCell, transform.position, m_Data))
        {
            m_LastAttackTime = Time.time;
            m_Animator?.SetTrigger(HASH_ATTACK);
        }
    }
}

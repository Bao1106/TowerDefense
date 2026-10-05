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
    private float m_NextPressureSample; // step 1.6 measurement — becomes the N1/N2 tick in Phase 2
    private float m_LastHitTime = -999f;

    /// <summary>Morale state for this operator (§02). Null before Init.</summary>
    public TDOperatorMorale Morale { get; private set; }

    /// <summary>Last context fed to morale — reused by the debug overlay so it does not
    /// rebuild one (and re-scan every enemy) just to print a projection.</summary>
    public TDMoraleContext MoraleContext { get; private set; }
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
        TDOperatorRoster.api?.OnDeployed(m_Data);

        m_Initialized = true;
        Debug.Log($"[TDOperatorView] Init cell={m_MyCell}, name={m_Data?.operatorName}, zone={data?.deployZone}, HP={m_CurrentHp}");
    }

    void IPlacedUnit.OnRemove()
    {
        m_Initialized = false;
        m_Behavior?.OnRemove(m_MyCell, transform.position);
    }

    // ── Damage / Death ────────────────────────────────────────────────────────

    /// <summary>
    /// Feeds morale from what this operator can already see. No new Update() and no new
    /// singleton: the pressure zone comes from the probe written for step 1.6, the allies
    /// from the operator registry, the aura from the enemy registry.
    /// </summary>
    private void TickMorale(float deltaTime)
    {
        if (Morale == null) return;

        var ctx = new TDMoraleContext
        {
            enemiesInZone = TDPressureProbe.Count(m_MyCell, transform.rotation, m_Data),
            tolerance = TDPressureProbe.Tolerance(m_Data),
            calmAlliesAdjacent = TDOperatorRegistry.api?.CountCalmAlliesAdjacent(m_MyCell) ?? 0,
            auraRate = TDPressureProbe.AuraRateAt(m_MyCell),
            secondsSinceHit = Time.time - m_LastHitTime,
            allyBrokeThisWave = TDOperatorRegistry.api?.AnyBrokeThisWave ?? false,
        };

        MoraleContext = ctx;

        bool wasBroken = Morale.IsBroken;
        Morale.Tick(deltaTime, ctx);

        // Breaking is an event, not just a value crossing 100: it spikes everyone nearby
        // (§04 N3) and costs the whole team its wave-clear relief.
        if (!wasBroken && Morale.IsBroken)
        {
            Morale.OnSetback();
            TDOperatorRegistry.api?.BroadcastSpike(m_MyCell, TDConstant.STRESS_ALLY_BREAK_SPIKE, wasBreak: true);
        }
    }

    public void TakeDamage(float damage)
    {
        if (!m_Initialized || m_IsDying || m_CurrentHp <= 0) return;
        m_LastHitTime = Time.time;

        m_CurrentHp -= damage;
        m_HPBarView?.UpdateHP(m_CurrentHp, m_MaxHp);
        if (m_CurrentHp <= 0) Die();
    }

    public Transform SelectionIndicatorTransform => m_SelectionIndicator;

    public void SetSelected(bool selected) => m_SelectionIndicator?.gameObject.SetActive(selected);

    public void DoRetreat()
    {
        // The corpse still playing its Die clip is not a unit that can walk off.
        if (m_IsDying) return;

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

        TDPressureProbe.Sample(m_MyCell, transform.rotation, m_Data, ref m_NextPressureSample);
        TickMorale(Time.deltaTime);

        float attackInterval = 1f / (m_Data?.attackSpeed ?? 1f);
        if (Time.time - m_LastAttackTime < attackInterval) return;

        if (m_Behavior.TryAttack(m_MyCell, transform.position, m_Data))
        {
            m_LastAttackTime = Time.time;
            m_Animator?.SetTrigger(HASH_ATTACK);
        }
    }
}

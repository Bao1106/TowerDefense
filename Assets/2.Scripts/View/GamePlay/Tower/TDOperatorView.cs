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
    private OperatorType m_OperatorType;
    private IOperatorBehavior m_Behavior;
    private OperatorData m_Data; // cached at Init - avoids List.Find every frame
    private Vector2Int m_MyCell;
    private float m_CurrentHp;
    private float m_MaxHp;
    private float m_LastAttackTime = -999f;
    private float m_NextPressureSample; // step 1.6 measurement — becomes the N1/N2 tick in Phase 2
    private bool m_Initialized;
    private bool m_IsDying;

    public int Cost { get; private set; }
    public OperatorType OperatorType => m_OperatorType;

    // ── IPlacedUnit ───────────────────────────────────────────────────────────

    TowerType IPlacedUnit.UnitType => TowerType.Operator;

    void IPlacedUnit.Init(string instanceKey, TDTowerSlotInfo slotInfo)
    {
        m_Animator = GetComponentInChildren<Animator>();
        m_HPBarView = GetComponentInChildren<TDHPBarView>(includeInactive: true);
        m_SelectionIndicator = transform.Find(TDConstant.SELECTION_INDICATOR_NAME);

        m_OperatorType = slotInfo.operatorType;
        Cost = slotInfo.cost;
        m_MyCell = TDGridMainModel.api.WorldToCell(transform.position);
        m_LastAttackTime = -999f;

        m_SelectionIndicator?.gameObject.SetActive(false);

        var data = TDFlyweightOperatorDataSettings.api.GetData(m_OperatorType);
        m_Data = data;
        m_CurrentHp = data?.hp ?? 100f;
        m_MaxHp = m_CurrentHp;

        m_Behavior = TDControl.CreateOperatorBehavior(data?.deployZone ?? DeployZone.PathCell);
        m_Behavior.OnInit(m_MyCell, data, this);

        m_HPBarView?.Show();
        m_HPBarView?.UpdateHP(m_CurrentHp, m_MaxHp);

        m_Initialized = true;
        Debug.Log($"[TDOperatorView] Init cell={m_MyCell}, type={m_OperatorType}, zone={data?.deployZone}, HP={m_CurrentHp}");
    }

    void IPlacedUnit.OnRemove()
    {
        m_Initialized = false;
        m_Behavior?.OnRemove(m_MyCell, transform.position);
    }

    // ── Damage / Death ────────────────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (!m_Initialized || m_IsDying || m_CurrentHp <= 0) return;
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

        ((IPlacedUnit)this).OnRemove();
        TDGameEventBus.OperatorDied(m_MyCell);
        Destroy(gameObject);
    }

    private void Die()
    {
        if (m_IsDying) return;
        m_IsDying = true;

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

        float attackInterval = 1f / (m_Data?.attackSpeed ?? 1f);
        if (Time.time - m_LastAttackTime < attackInterval) return;

        if (m_Behavior.TryAttack(m_MyCell, transform.position, m_Data))
        {
            m_LastAttackTime = Time.time;
            m_Animator?.SetTrigger(HASH_ATTACK);
        }
    }
}

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
    private const string SELECTION_INDICATOR_NAME = "SelectionIndicator";

    private static readonly int HASH_ATTACK = Animator.StringToHash("Attack");

    private Animator          m_Animator;
    private TDHPBarView       m_HPBarView;
    private Transform         m_SelectionIndicator;
    private OperatorType      m_OperatorType;
    private IOperatorBehavior m_Behavior;
    private Vector2Int        m_MyCell;
    private float             m_CurrentHp;
    private float             m_MaxHp;
    private float             m_LastAttackTime = -999f;
    private bool              m_Initialized;

    public int          Cost         { get; private set; }
    public OperatorType OperatorType => m_OperatorType;

    // ── IPlacedUnit ───────────────────────────────────────────────────────────

    TowerType IPlacedUnit.UnitType => TowerType.Operator;

    void IPlacedUnit.Init(string instanceKey, TDTowerSlotInfo slotInfo)
    {
        m_Animator           = GetComponentInChildren<Animator>();
        m_HPBarView          = GetComponentInChildren<TDHPBarView>(includeInactive: true);
        m_SelectionIndicator = transform.Find(SELECTION_INDICATOR_NAME);

        m_OperatorType   = slotInfo.operatorType;
        Cost             = slotInfo.cost;
        m_MyCell         = TDGridMainModel.api.WorldToCell(transform.position);
        m_LastAttackTime = -999f;

        m_SelectionIndicator?.gameObject.SetActive(false);

        var data    = TDFlyweightOperatorDataSettings.api.GetData(m_OperatorType);
        m_CurrentHp = data?.hp ?? 100f;
        m_MaxHp     = m_CurrentHp;

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
        if (!m_Initialized || m_CurrentHp <= 0) return;
        m_CurrentHp -= damage;
        m_HPBarView?.UpdateHP(m_CurrentHp, m_MaxHp);
        if (m_CurrentHp <= 0) Die();
    }

    public Transform SelectionIndicatorTransform => m_SelectionIndicator;

    public void SetSelected(bool selected) => m_SelectionIndicator?.gameObject.SetActive(selected);

    public void DoRetreat()
    {
        ((IPlacedUnit)this).OnRemove();
        TDGameEventBus.OperatorDied(m_MyCell);
        Destroy(gameObject);
    }

    private void Die()
    {
        ((IPlacedUnit)this).OnRemove();
        TDGameEventBus.OperatorDied(m_MyCell);
        Destroy(gameObject);
    }

    // ── Animation event callback ──────────────────────────────────────────────

    // Called from the "OnAttackHit" AnimationEvent in the attack clip for Ranger/Mage operators
    public void OnAttackHit()
    {
        if (!m_Initialized || m_Behavior == null) return;
        var data = TDFlyweightOperatorDataSettings.api.GetData(m_OperatorType);
        m_Behavior.ExecuteHit(m_MyCell, transform.position, data);
    }

    // ── Update — fully delegated to the behavior strategy ─────────────────────

    private void Update()
    {
        if (!m_Initialized || m_Behavior == null) return;

        var data = TDFlyweightOperatorDataSettings.api.GetData(m_OperatorType);
        float attackInterval = 1f / (data?.attackSpeed ?? 1f);
        if (Time.time - m_LastAttackTime < attackInterval) return;

        if (m_Behavior.TryAttack(m_MyCell, transform.position, data))
        {
            m_LastAttackTime = Time.time;
            m_Animator?.SetTrigger(HASH_ATTACK);
        }
    }
}

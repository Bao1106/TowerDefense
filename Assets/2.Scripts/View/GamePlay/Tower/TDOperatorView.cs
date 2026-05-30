using TDEnums;
using UnityEngine;

/// <summary>
/// MonoBehaviour gắn trên operator prefab.
/// Implements IPlacedUnit — TDTowerFactoryControl gọi Init(key, slotInfo) sau khi Instantiate.
/// Tự đăng ký vào TDOperatorRegistry trong Init (không còn phụ thuộc TDPlaceTowerControl).
/// </summary>
public class TDOperatorView : MonoBehaviour, IPlacedUnit
{
    [SerializeField] private TDHPBarView m_HPBarView;

    private static readonly int HASH_ATTACK = Animator.StringToHash("Attack");

    private Animator m_Animator;
    private OperatorType m_OperatorType;
    private Vector2Int m_MyCell;
    private float m_CurrentHp;
    private float m_MaxHp;
    private float m_LastAttackTime = -999f;
    private bool m_Initialized;

    // ── IPlacedUnit ───────────────────────────────────────────────────────────

    TowerType IPlacedUnit.UnitType => TowerType.Operator;

    void IPlacedUnit.Init(string instanceKey, TDTowerSlotInfo slotInfo)
    {
        m_Animator = GetComponentInChildren<Animator>();
        m_OperatorType = slotInfo.operatorType;
        m_MyCell = TDGridMainModel.api.WorldToCell(transform.position);

        var data = TDFlyweightOperatorDataSettings.api.GetData(m_OperatorType);
        m_CurrentHp = data?.hp ?? 100f;
        m_MaxHp     = m_CurrentHp;
        m_LastAttackTime = -999f;
        m_Initialized = true;

        m_HPBarView?.Show();
        m_HPBarView?.UpdateHP(m_CurrentHp, m_MaxHp);

        int blockCap = Mathf.Clamp(data?.blockCount ?? 1, 1, 3);
        TDOperatorRegistry.api?.RegisterOperator(m_MyCell, blockCap);
        TDOperatorRegistry.api?.RegisterOperatorView(m_MyCell, this);

        Debug.Log($"[TDOperatorView] Init cell={m_MyCell}, type={m_OperatorType}, HP={m_CurrentHp}");
    }

    void IPlacedUnit.OnRemove()
    {
        m_Initialized = false;
        TDOperatorRegistry.api?.UnregisterOperator(m_MyCell);
    }

    // ── Damage / Death ────────────────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (!m_Initialized || m_CurrentHp <= 0) return;
        m_CurrentHp -= damage;
        m_HPBarView?.UpdateHP(m_CurrentHp, m_MaxHp);
        if (m_CurrentHp <= 0) Die();
    }

    private void Die()
    {
        ((IPlacedUnit)this).OnRemove();
        TDGameEventBus.OperatorDied(m_MyCell);
        Destroy(gameObject);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!m_Initialized) return;
        if (TDOperatorRegistry.api == null) return;

        var data = TDFlyweightOperatorDataSettings.api.GetData(m_OperatorType);
        float attackInterval = 1f / (data?.attackSpeed ?? 1f);
        if (Time.time - m_LastAttackTime < attackInterval) return;

        var blocked = TDOperatorRegistry.api.GetBlockedEnemies(m_MyCell);
        if (blocked.Count == 0) return;

        float damage = data?.damage ?? 0f;
        foreach (var enemy in blocked)
        {
            if (enemy != null)
                enemy.TakeDamage(damage);
        }

        m_LastAttackTime = Time.time;
        if (m_Animator != null)
            m_Animator.SetTrigger(HASH_ATTACK);
    }
}

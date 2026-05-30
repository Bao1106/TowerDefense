using System.Collections;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDEnemyView : MonoBehaviour
{
    private const string TRIGGER_WALK    = "Walk";
    private const string TRIGGER_ATTACK  = "Attack";
    private const string TRIGGER_GET_HIT = "GetHit";
    private const string TRIGGER_DIE     = "Die";

    [SerializeField] private TDHPBarView m_HPBarView;

    private Animator      m_Animator;
    private List<Vector3> m_PathsPosition = new List<Vector3>();
    private float  m_MoveSpeed, m_EnemyHealth, m_MaxHealth, m_DieDuration;
    private float  m_AttackDamage, m_AttackSpeed;
    private float  m_LastAttackTime = -999f;
    private int    m_GoldReward;
    private int    m_CurrentPathIndex;
    private string m_EnemyKey;
    private bool   m_HasReachedEnd;
    private bool   m_HasBeenReturned;
    private bool   m_IsDying;

    // Operator blocking state
    private bool       m_IsBlocked;
    private Vector2Int m_BlockerCell;

    public EnemyType EnemyType { get; private set; }

    // Progress 0→1 (0 = vừa spawn, 1 = đến GateEnd)
    // Tower dùng để ưu tiên enemy gần GateEnd nhất
    public float PathProgress => m_PathsPosition.Count == 0 ? 0f
        : (float)m_CurrentPathIndex / m_PathsPosition.Count;

    private void Awake()
    {
        m_Animator = GetComponent<Animator>();
    }

    public void Initialize(string key, float hp, float speed,
        float attackDamage, float attackSpeed,
        float dieDuration, int goldReward, EnemyType enemyType)
    {
        m_HasBeenReturned  = false;
        m_HasReachedEnd    = false;
        m_IsDying          = false;
        m_IsBlocked        = false;
        m_BlockerCell      = Vector2Int.zero;
        m_CurrentPathIndex = 0;
        m_PathsPosition.Clear();
        m_EnemyHealth  = hp;
        m_MaxHealth    = hp;
        m_MoveSpeed    = speed;
        m_AttackDamage = attackDamage;
        m_AttackSpeed  = attackSpeed;
        m_LastAttackTime = -999f;
        m_DieDuration  = dieDuration;
        m_GoldReward   = goldReward;
        EnemyType      = enemyType;
        m_EnemyKey     = key;

        // Reset animator về Idle (quan trọng khi tái dùng từ pool)
        if (m_Animator != null)
        {
            m_Animator.Rebind();
            m_Animator.Update(0f);
        }
        m_HPBarView?.ResetBar();
        TriggerSafe(TRIGGER_WALK);

        TDEnemyControl.api.onGetEnemyPathPos += OnGetEnemyPathPos;
        TDEnemyRegistry.api.Register(this);
    }

    private void OnGetEnemyPathPos(string key, List<Vector3> pathsPos, int index)
    {
        if (!key.Equals(m_EnemyKey)) return;
        m_PathsPosition    = pathsPos;
        m_CurrentPathIndex = index;
    }

    private void OnDestroy()
    {
        if (!m_HasBeenReturned)
        {
            TDEnemyControl.api.onGetEnemyPathPos -= OnGetEnemyPathPos;
            TDEnemyRegistry.api.Unregister(this);
        }
    }

    public void TakeDamage(float damage)
    {
        if (m_IsDying) return;
        m_EnemyHealth -= damage;
        m_HPBarView?.Show();
        m_HPBarView?.UpdateHP(m_EnemyHealth, m_MaxHealth);
        if (m_EnemyHealth <= 0)
            Die();
        else
            TriggerSafe(TRIGGER_GET_HIT);
    }

    // Giải phóng blocking slot nếu enemy đang bị chặn bởi melee operator
    private void UnblockFromMelee()
    {
        if (!m_IsBlocked) return;
        TDOperatorRegistry.api?.OnEnemyUnblocked(m_BlockerCell, this);
        m_IsBlocked   = false;
        m_BlockerCell = Vector2Int.zero;
    }

    /// Gọi bởi TDOperatorRegistry khi operator tại ô bị chết → enemy tiếp tục di chuyển.
    public void ForceUnblock()
    {
        if (!m_IsBlocked) return;
        m_IsBlocked   = false;
        m_BlockerCell = Vector2Int.zero;
        m_CurrentPathIndex++; // bỏ qua ô operator vừa chết, đi tiếp
    }

    private void Die()
    {
        if (m_HasBeenReturned) return;
        m_IsDying = true;

        UnblockFromMelee(); // giải phóng operator slot trước để enemy kế tiếp vào được
        TDEnemyControl.api.onGetEnemyPathPos -= OnGetEnemyPathPos;
        TDEnemyRegistry.api.Unregister(this);

        TDGoldControl.api?.AddGold(m_GoldReward);
        TDGameStateControl.api?.OnEnemyRemoved();

        TriggerSafe(TRIGGER_DIE);

        if (m_DieDuration > 0f)
            StartCoroutine(ReturnAfterDelay(m_DieDuration));
        else
            PoolReturn();
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PoolReturn();
    }

    private void PoolReturn()
    {
        if (m_HasBeenReturned) return;
        m_HasBeenReturned = true;
        m_HPBarView?.ResetBar();
        TDEnemyPathMainControl.api.ReturnEnemy(this);
    }

    private void ReturnToPool()
    {
        if (m_HasBeenReturned) return;
        m_HasBeenReturned = true;
        UnblockFromMelee();
        TDEnemyControl.api.onGetEnemyPathPos -= OnGetEnemyPathPos;
        TDEnemyRegistry.api.Unregister(this);
        TDEnemyPathMainControl.api.ReturnEnemy(this);
    }

    public void SetPath(List<IGridCellDTO> path)
    {
        TDEnemyControl.api.SetEnemyPath(m_EnemyKey, path);
    }

    private void TriggerSafe(string triggerName)
    {
        if (m_Animator == null) return;
        m_Animator.SetTrigger(triggerName);
    }

    private void Update()
    {
        if (m_PathsPosition == null || m_PathsPosition.Count == 0 || m_HasReachedEnd || m_IsDying) return;

        // Nếu đang bị chặn bởi melee operator → tấn công lại operator
        if (m_IsBlocked)
        {
            if (m_AttackSpeed > 0f && Time.time - m_LastAttackTime >= 1f / m_AttackSpeed)
            {
                TDOperatorRegistry.api?.GetOperatorView(m_BlockerCell)?.TakeDamage(m_AttackDamage);
                m_LastAttackTime = Time.time;
                TriggerSafe(TRIGGER_ATTACK);
            }
            return;
        }

        if (m_CurrentPathIndex < m_PathsPosition.Count)
        {
            Vector3 targetPosition = m_PathsPosition[m_CurrentPathIndex];
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, m_MoveSpeed * Time.deltaTime);

            Vector3 dir = targetPosition - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                // Khi vừa đến waypoint — kiểm tra xem có melee operator đang chặn không
                Vector2Int arrivedCell = TDGridMainModel.api.WorldToCell(targetPosition);
                if (TDOperatorRegistry.api != null && TDOperatorRegistry.api.CanBlock(arrivedCell))
                {
                    // Snap đúng vào trung tâm ô để IsInRange của operator hoạt động chính xác
                    transform.position = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
                    m_IsBlocked   = true;
                    m_BlockerCell = arrivedCell;
                    TDOperatorRegistry.api.OnEnemyBlocked(arrivedCell, this);
                }
                else
                {
                    m_CurrentPathIndex++;
                }
            }
        }
        else
        {
            m_HasReachedEnd = true;
            TDPlayerLifeControl.api.LoseLife();
            TDGameStateControl.api?.OnEnemyRemoved(); // enemy thoát cũng tính là removed
            ReturnToPool();
        }
    }
}

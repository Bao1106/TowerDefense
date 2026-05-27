using System.Collections;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDEnemyView : MonoBehaviour
{
    private const string TRIGGER_WALK   = "Walk";
    private const string TRIGGER_GET_HIT = "GetHit";
    private const string TRIGGER_DIE    = "Die";

    private Animator      m_Animator;
    private List<Vector3> m_PathsPosition = new List<Vector3>();
    private float  m_MoveSpeed, m_EnemyHealth, m_DieDuration;
    private int    m_GoldReward;
    private int    m_CurrentPathIndex;
    private string m_EnemyKey;
    private bool   m_HasReachedEnd;
    private bool   m_HasBeenReturned;
    private bool   m_IsDying;

    public EnemyType EnemyType { get; private set; }

    // Progress 0→1 (0 = vừa spawn, 1 = đến GateEnd)
    // Tower dùng để ưu tiên enemy gần GateEnd nhất
    public float PathProgress => m_PathsPosition.Count == 0 ? 0f
        : (float)m_CurrentPathIndex / m_PathsPosition.Count;

    private void Awake()
    {
        m_Animator = GetComponent<Animator>();
    }

    public void Initialize(string key, float hp, float speed, float dieDuration, int goldReward, EnemyType enemyType)
    {
        m_HasBeenReturned  = false;
        m_HasReachedEnd    = false;
        m_IsDying          = false;
        m_CurrentPathIndex = 0;
        m_PathsPosition.Clear();
        m_EnemyHealth  = hp;
        m_MoveSpeed    = speed;
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
        if (m_EnemyHealth <= 0)
            Die();
        else
            TriggerSafe(TRIGGER_GET_HIT);
    }

    private void Die()
    {
        if (m_HasBeenReturned) return;
        m_IsDying = true;

        TDEnemyControl.api.onGetEnemyPathPos -= OnGetEnemyPathPos;
        TDEnemyRegistry.api.Unregister(this);

        TDGoldControl.api?.AddGold(m_GoldReward);
        TDGameStateControl.api?.OnEnemyKilled();

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
        TDEnemyPathMainControl.api.ReturnEnemy(this);
    }

    private void ReturnToPool()
    {
        if (m_HasBeenReturned) return;
        m_HasBeenReturned = true;
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

        if (m_CurrentPathIndex < m_PathsPosition.Count)
        {
            Vector3 targetPosition = m_PathsPosition[m_CurrentPathIndex];
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, m_MoveSpeed * Time.deltaTime);

            Vector3 dir = targetPosition - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
                m_CurrentPathIndex++;
        }
        else
        {
            m_HasReachedEnd = true;
            TDPlayerLifeControl.api.LoseLife();
            ReturnToPool();
        }
    }
}

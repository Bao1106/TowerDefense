using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDEnemyView : MonoBehaviour
{
    private List<Vector3> m_PathsPosition = new List<Vector3>();
    private float  m_MoveSpeed, m_EnemyHealth;
    private int    m_CurrentPathIndex;
    private string m_EnemyKey;
    private bool   m_HasReachedEnd;
    private bool   m_HasBeenReturned;

    public EnemyType EnemyType { get; private set; }

    // Progress 0→1 (0 = vừa spawn, 1 = đến GateEnd)
    // Tower dùng để ưu tiên enemy gần GateEnd nhất
    public float PathProgress => m_PathsPosition.Count == 0 ? 0f
        : (float)m_CurrentPathIndex / m_PathsPosition.Count;

    public void Initialize(string key, float hp, float speed, EnemyType enemyType)
    {
        m_HasBeenReturned  = false;
        m_HasReachedEnd    = false;
        m_CurrentPathIndex = 0;
        m_PathsPosition.Clear();
        m_EnemyHealth = hp;
        m_MoveSpeed   = speed;
        EnemyType     = enemyType;
        m_EnemyKey    = key;

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
        m_EnemyHealth -= damage;
        if (m_EnemyHealth <= 0)
            ReturnToPool();
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

    private void Update()
    {
        if (m_PathsPosition == null || m_PathsPosition.Count == 0 || m_HasReachedEnd) return;

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

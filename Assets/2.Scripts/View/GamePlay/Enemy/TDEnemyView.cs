using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDEnemyView : MonoBehaviour
{
    private List<Vector3> m_PathsPosition = new List<Vector3>();
    private float m_MoveSpeed, m_EnemyHealth;
    private int m_CurrentPathIndex;
    private string m_EnemyKey;
    private bool m_HasReachedEnd;

    public void Initialize(string key)
    {
        m_EnemyHealth = TDConstant.CONFIG_ENEMY_HEALTH;
        m_MoveSpeed = TDConstant.CONFIG_ENEMY_MOVE_SPEED;
        m_EnemyKey = key;
        
        TDEnemyControl.api.onGetEnemyPathPos += OnGetEnemyPathPos;
    }
    
    private void OnGetEnemyPathPos(string key, List<Vector3> pathsPos, int index)
    {
        if (!key.Equals(m_EnemyKey)) return;
        
        m_PathsPosition = pathsPos;
        m_CurrentPathIndex = index;
    }

    private void OnDestroy()
    {
        TDEnemyControl.api.onGetEnemyPathPos -= OnGetEnemyPathPos;
    }

    public void TakeDamage(float damage)
    {
        m_EnemyHealth -= damage;
        if (m_EnemyHealth <= 0)
        {
            Destroy(gameObject);
        }
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
            Destroy(gameObject);
        }
    }
}
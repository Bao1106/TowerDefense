﻿using System;
using System.Collections.Generic;
using TDEnums;
using Services.DependencyInjection;
using UnityEngine;

public class TDEnemyView : MonoBehaviour
{
    [SerializeField] private EnemyAiType aiType;
        
    private List<Vector3> m_PathsPosition = new List<Vector3>();
    private TDEnemyControl m_EnemyControl;
    private float m_MoveSpeed, m_EnemyHealth;
    private int m_CurrentPathIndex;

    public EnemyAiType AiType
    {
        get
        {
            return aiType;
        }
    }

    public void Initialize()
    {
        m_EnemyHealth = TDConstant.CONFIG_ENEMY_HEALTH;
        m_MoveSpeed = TDConstant.CONFIG_ENEMY_MOVE_SPEED;
        
        m_EnemyControl = new TDEnemyControl();
        m_EnemyControl.onGetEnemyPathPos += OnGetEnemyPathPos;
    }
    
    private void OnGetEnemyPathPos(List<Vector3> pathsPos, int index)
    {
        m_PathsPosition = pathsPos;
        m_CurrentPathIndex = index;
        transform.TransformDirection(m_PathsPosition[0]);
    }

    private void OnDestroy()
    {
        if (m_EnemyControl != null)
        {
            m_EnemyControl.onGetEnemyPathPos -= OnGetEnemyPathPos;
        }
    }

    public void TakeDamage(float damage)
    {
        m_EnemyHealth -= damage;
        if (m_EnemyHealth <= 0)
        {
            Destroy(gameObject);
        }
    }
        
    public void SetPath(List<IGridCellModel> path)
    {
        m_EnemyControl.SetEnemyPath(path);
    }

    private void Update()
    {
        if (m_PathsPosition == null) return;
            
        if (m_CurrentPathIndex < m_PathsPosition.Count)
        {
            Vector3 targetPosition = m_PathsPosition[m_CurrentPathIndex];
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, m_MoveSpeed * Time.deltaTime);
            //Missing rotate for enemy

            if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
            {
                m_CurrentPathIndex++;
            }
        }
    }
}
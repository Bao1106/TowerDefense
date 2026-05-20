using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Services.DependencyInjection;
using UnityEngine;

public class TDEnemyPathMainView : MonoBehaviour
{
    private Vector2Int m_StartPoint, m_EndPoint;
    private Transform m_SpawnPos;
    
    private readonly List<IGridCellDTO> m_AStarPaths = new List<IGridCellDTO>();
    private List<TDEnemyView> m_EnemiesView = new List<TDEnemyView>();
    private IGridDTO m_GridDTO;
    private IGridCellDTO m_CurrentWaypointDTO;
    private TDEnemyPathView m_EnemyPathView;
    private TDEnemyView m_Slime; //prefab slime
    private int m_WaypointIndex;
    
    public void Initialize(IGridDTO initGridDTO)
    {
        m_StartPoint = TDConstant.CONFIG_ENEMY_START_POINT;
        m_EndPoint = TDConstant.CONFIG_ENEMY_END_POINT;
        m_SpawnPos = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_CREATE_POINT).transform;
        m_EnemyPathView = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_VIEW).GetComponent<TDEnemyPathView>();
        m_Slime = RepResourceObject.GetResource<GameObject>(TDConstant.PREFAB_SLIME).GetComponent<TDEnemyView>();
        
        m_GridDTO = initGridDTO;

        ImplementPath();
    }

    private async void ImplementPath()
    {
        await TDInitializeModel.api.createGridCompletion.Task;
        m_EnemyPathView.RegistryValues();
        RegistryEvents();
        
        TDEnemyPathMainControl.api.InitEnemyPath(m_GridDTO, m_StartPoint, m_EndPoint);
        TDEnemyPathMainControl.api.GenerateEnemyStartPath(m_GridDTO, m_StartPoint);
        TDEnemyPathMainControl.api.SpawnEnemies(m_Slime, m_SpawnPos);
        TDEnemyPathMainControl.api.SetEnemyPath(m_EnemiesView, m_AStarPaths);
    }

    private void RegistryEvents()
    {
        TDaStarPathControl.api.onGetPath += OnFindAStarPath;
        TDaStarPathControl.api.onGetFinalPath += OnFindFinalPath;
        TDaStarPathControl.api.onGetWaypointIndex += OnGetWaypointIndex;
        
        TDEnemyPathMainControl.api.onGetEnemyPos += OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetEnemies += OnGetEnemies;
    }
    
    private void OnDestroy()
    {
        TDaStarPathControl.api.onGetPath -= OnFindAStarPath;
        TDaStarPathControl.api.onGetFinalPath -= OnFindFinalPath;
        TDaStarPathControl.api.onGetWaypointIndex -= OnGetWaypointIndex;
        
        TDEnemyPathMainControl.api.onGetEnemyPos -= OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetEnemies -= OnGetEnemies;
    }

    private void OnFindAStarPath(List<IGridCellDTO> pathCells, IGridCellDTO end)
    {
        if (pathCells != null)
        {
            m_AStarPaths.AddRange(pathCells);
            m_CurrentWaypointDTO = end;
            TDEnemyPathMainControl.api.GenerateEnemyNextPath(m_GridDTO, m_CurrentWaypointDTO, m_EndPoint, m_WaypointIndex);
        }
    }
    
    private void OnFindFinalPath(List<IGridCellDTO> finalPath)
    {
        if (finalPath != null)
        {
            m_AStarPaths.AddRange(finalPath);
            TDEnemyPathMainControl.api.VisualizeFinalPath(m_AStarPaths, m_EnemyPathView);
        }
    }
    
    private void OnGetWaypointIndex(int id)
    {
        m_WaypointIndex = id;
    }
    
    private void OnGetEnemyPos(Vector2Int startPoint, Vector2Int endPoint)
    {
        m_StartPoint = startPoint;
        m_EndPoint = endPoint;
    }
    
    private void OnGetEnemies(List<TDEnemyView> enemies)
    {
        m_EnemiesView = enemies;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TDEnums;
using UnityEngine;
using Object = UnityEngine.Object;

public class TDEnemyPathMainControl
{
    public static TDEnemyPathMainControl api;

    public Action<Vector2Int, Vector2Int> onGetEnemyPos;
    public Action<List<TDEnemyView>> onGetEnemies;
    
    public void InitEnemyPath(IGridDTO gridDTO, Vector2Int startPoint, Vector2Int endPoint)
    {
        Vector3 startWorldPos = TDGridMainModel.api.GetNearestGridPosition(new Vector3(startPoint.x * TDGridMainModel.api.cellSize, 0, startPoint.y * TDGridMainModel.api.cellSize));
        Vector3 endWorldPos = TDGridMainModel.api.GetNearestGridPosition(new Vector3(endPoint.x * TDGridMainModel.api.cellSize, 0, endPoint.y * TDGridMainModel.api.cellSize));
        
        startPoint = new Vector2Int(Mathf.RoundToInt(startWorldPos.x / TDGridMainModel.api.cellSize), Mathf.RoundToInt(startWorldPos.z / TDGridMainModel.api.cellSize));
        endPoint = new Vector2Int(Mathf.RoundToInt(endWorldPos.x / TDGridMainModel.api.cellSize), Mathf.RoundToInt(endWorldPos.z / TDGridMainModel.api.cellSize));
        
        gridDTO.SetCell(startPoint.x, startPoint.y, new TDGridCellDTO(startPoint.x, startPoint.y, CellType.Start));
        gridDTO.SetCell(endPoint.x, endPoint.y, new TDGridCellDTO(endPoint.x, endPoint.y, CellType.End));
        
        onGetEnemyPos?.Invoke(startPoint, endPoint);
    }

    public void GenerateEnemyStartPath(IGridDTO gridDTO, Vector2Int startPoint)
    {
        IGridCellDTO start = gridDTO.GetCell(startPoint.x, startPoint.y);
        
        Vector2Int firstWaypoint = TDConstant.CONFIG_ENEMY_WAYPOINTS.First();
        IGridCellDTO waypointCellDto = gridDTO.GetCell(firstWaypoint.x, firstWaypoint.y);
        TDaStarPathControl.api.SetIndex(Array.IndexOf(TDConstant.CONFIG_ENEMY_WAYPOINTS, firstWaypoint));
        TDaStarPathControl.api.FindPath(gridDTO, start, waypointCellDto, false);
    }

    public void GenerateEnemyNextPath(IGridDTO gridDTO, IGridCellDTO current, Vector2Int endPoint, int currentWaypointID)
    {
        int length = TDConstant.CONFIG_ENEMY_WAYPOINTS.Length;
        if (length - 1 == currentWaypointID)
        {
            IGridCellDTO end = gridDTO.GetCell(endPoint.x, endPoint.y);
            TDaStarPathControl.api.FindPath(gridDTO, current, end, true);
            return;
        }
        
        Vector2Int waypoint = TDConstant.CONFIG_ENEMY_WAYPOINTS[currentWaypointID + 1];
        IGridCellDTO waypointCellDto = gridDTO.GetCell(waypoint.x, waypoint.y);
        TDaStarPathControl.api.SetIndex(currentWaypointID + 1);
        TDaStarPathControl.api.FindPath(gridDTO, current, waypointCellDto, false);
    }
    
    public void VisualizeFinalPath(List<IGridCellDTO> lstCellFinal, TDEnemyPathView enemyPathView)
    {
        if (lstCellFinal is { Count: > 0 })
        {
            enemyPathView.VisualizePath(lstCellFinal);
        }
        else
        {
            Debug.Log("<color=red>Cannot find path</color>");
        }
    }

    public void SpawnEnemies(TDEnemyView prefab, Transform spawnPos)
    {
        List<TDEnemyView> enemies = new List<TDEnemyView>();
        for (int i = 0; i < TDConstant.CONFIG_ENEMIES_NUMBER; i++)
        {
            TDEnemyView enemy = Object.Instantiate(prefab, spawnPos.position, Quaternion.identity);
            string key = $"{i}-{enemy.gameObject.name}";
            enemy.Initialize(key);
            enemies.Add(enemy);
        }
        
        onGetEnemies?.Invoke(enemies);
    }

    public async void SetEnemyPath(List<TDEnemyView> enemies, List<IGridCellDTO> paths)
    {
        await Task.Delay(TDConstant.CONFIG_ENEMY_SPAWN_INTERVAL * 1000);
        foreach (TDEnemyView enemy in enemies)
        {
            if (paths is { Count: > 0 })
            {
                if (enemy != null)
                {
                    enemy.SetPath(paths);
                }
                else
                {
                    if(Application.isPlaying)
                        Debug.Log("<color=red>EnemyController component not found on enemy prefab!</color>");
                }
            }

            await Task.Delay(TDConstant.CONFIG_ENEMY_SPAWN_INTERVAL * 1000);
        }
    }
}

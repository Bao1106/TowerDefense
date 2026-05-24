using System.Collections.Generic;
using Services.DependencyInjection;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;

public class TDEnemyPathMainView : MonoBehaviour
{
    [SerializeField] private GameObject[] m_ObstaclePrefabs;
    [SerializeField] private GameObject   m_GateStartPrefab;
    [SerializeField] private GameObject   m_GateEndPrefab;

    private Vector2Int m_StartPoint, m_EndPoint;
    private Transform m_SpawnPos;
    private TDGateView m_GateStartView;

    private List<List<IGridCellDTO>> m_AllPaths = new List<List<IGridCellDTO>>();
    private List<TDEnemyView> m_EnemiesView = new List<TDEnemyView>();
    private readonly List<GameObject> m_SpawnedObstacles = new List<GameObject>();
    private IGridDTO m_GridDTO;
    private TDEnemyPathView m_EnemyPathView;
    private TDEnemyView m_Slime;

    public void Initialize(IGridDTO initGridDTO)
    {
        m_StartPoint    = TDConstant.CONFIG_ENEMY_START_POINT;
        m_EndPoint      = TDConstant.CONFIG_ENEMY_END_POINT;
        m_SpawnPos      = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_CREATE_POINT).transform;
        m_EnemyPathView = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_VIEW).GetComponent<TDEnemyPathView>();
        m_Slime         = RepResourceObject.GetResource<GameObject>(TDConstant.PREFAB_SLIME).GetComponent<TDEnemyView>();

        m_GridDTO = initGridDTO;
        ImplementPath();
    }

    private async void ImplementPath()
    {
        await TDInitializeModel.api.createGridCompletion.Task;
        m_EnemyPathView.RegistryValues();
        RegistryEvents();

        // 1. Set start/end cells trên grid
        TDEnemyPathMainControl.api.InitEnemyPath(m_GridDTO, m_StartPoint, m_EndPoint);
        SpawnGates();

        // 2. Build 3 defined paths theo waypoints (không dùng A*)
        // → onGetAllPaths fires → OnGetAllPaths lưu m_AllPaths
        TDEnemyPathMainControl.api.GenerateAllPaths(m_GridDTO, m_StartPoint, m_EndPoint);

        // 3. Gom tất cả path cells vào HashSet để obstacle tránh ra
        HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>(
            m_AllPaths.SelectMany(path => path.Select(cell => cell.position))
        );

        // 4. Đặt obstacles NGOÀI path cells (visual decoration, không ảnh hưởng route)
        TDObstacleControl.api.PlaceObstacles(m_GridDTO, m_StartPoint, m_EndPoint, pathCells);
    }

    private void SpawnGates()
    {
        Vector3[,] grid = TDGridMainModel.api.GetGrid();

        if (m_GateStartPrefab != null)
        {
            Vector3 startWorld = grid[m_StartPoint.x, m_StartPoint.y];
            var go = Object.Instantiate(m_GateStartPrefab, startWorld, Quaternion.identity, transform);
            m_GateStartView = go.GetComponent<TDGateView>();
        }
        else
            Debug.LogWarning("<color=orange>TDEnemyPathMainView: m_GateStartPrefab not assigned</color>");

        if (m_GateEndPrefab != null)
        {
            Vector3 endWorld = grid[m_EndPoint.x, m_EndPoint.y];
            Object.Instantiate(m_GateEndPrefab, endWorld, Quaternion.identity, transform);
        }
        else
            Debug.LogWarning("<color=orange>TDEnemyPathMainView: m_GateEndPrefab not assigned</color>");
    }

    private void RegistryEvents()
    {
        TDEnemyPathMainControl.api.onGetEnemyPos    += OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetAllPaths    += OnGetAllPaths;
        TDEnemyPathMainControl.api.onGetEnemies     += OnGetEnemies;
        TDEnemyPathMainControl.api.onWaveStart      += OnWaveStart;
        TDObstacleControl.api.onObstaclesPlaced     += OnObstaclesPlaced;
    }

    private void OnDestroy()
    {
        TDEnemyPathMainControl.api.onGetEnemyPos    -= OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetAllPaths    -= OnGetAllPaths;
        TDEnemyPathMainControl.api.onGetEnemies     -= OnGetEnemies;
        TDEnemyPathMainControl.api.onWaveStart      -= OnWaveStart;
        TDObstacleControl.api.onObstaclesPlaced     -= OnObstaclesPlaced;
    }

    private void OnWaveStart(int waveIdx)
    {
        m_GateStartView?.PlaySpawnEffect();
    }

    private void OnObstaclesPlaced(List<Vector2Int> gridPositions)
    {
        if (m_ObstaclePrefabs == null || m_ObstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("<color=orange>TDEnemyPathMainView: m_ObstaclePrefabs is empty — assign prefabs in Inspector</color>");
        }
        else
        {
            foreach (Vector2Int gp in gridPositions)
            {
                // Chọn random prefab từ array
                GameObject prefab = m_ObstaclePrefabs[Random.Range(0, m_ObstaclePrefabs.Length)];
                if (prefab == null) continue;

                // Lấy world position chính xác từ grid (đã có offset tích hợp)
                Vector3 worldPos = TDGridMainModel.api.GetGrid()[gp.x, gp.y];

                // Random rotation quanh trục Y cho tự nhiên hơn
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject obj = Object.Instantiate(prefab, worldPos, rotation, transform);
                m_SpawnedObstacles.Add(obj);
                TDGridMainModel.api.SetOccupiedCell(worldPos);
            }
        }

        // Tính valid tower cells sau khi obstacles đã xác định
        TDEnemyPathMainControl.api.ComputeValidTowerCells(
            m_GridDTO, m_AllPaths, gridPositions, m_StartPoint, m_EndPoint);
    }

    private void OnGetAllPaths(List<List<IGridCellDTO>> allPaths)
    {
        m_AllPaths = allPaths;
        if (m_AllPaths.Count == 0) return;

        // Visualize tất cả paths để player thấy toàn bộ maze (3 corridors)
        m_EnemyPathView.VisualizeAllPaths(m_AllPaths);

        // Start wave loop — mỗi wave random pick 1 corridor từ m_AllPaths
        TDEnemyPathMainControl.api.StartWaveLoop(m_Slime, m_SpawnPos, m_AllPaths);
    }

    private void OnGetEnemyPos(Vector2Int startPoint, Vector2Int endPoint)
    {
        m_StartPoint = startPoint;
        m_EndPoint   = endPoint;
    }

    private void OnGetEnemies(List<TDEnemyView> enemies)
    {
        m_EnemiesView = enemies;
    }
}

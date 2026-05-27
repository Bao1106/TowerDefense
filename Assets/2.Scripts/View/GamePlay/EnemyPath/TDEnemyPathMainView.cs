using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;

public class TDEnemyPathMainView : MonoBehaviour
{
    [SerializeField] private GameObject[]         m_ObstaclePrefabs;
    [SerializeField] private GameObject           m_GateStartPrefab;
    [SerializeField] private GameObject           m_GateEndPrefab;
    [SerializeField] private TDEnemyDataSettings  m_EnemyDataSettings;
    [SerializeField] private TDLevelConfigSettings m_LevelConfigSettings;

    private const int k_CurrentLevelIndex = 0;

    private Vector2Int m_StartPoint, m_EndPoint;
    private Transform  m_SpawnPos;
    private TDGateView m_GateStartView;
    private CancellationTokenSource m_WaveCts;

    private List<List<IGridCellDTO>> m_AllPaths = new List<List<IGridCellDTO>>();
    private readonly List<GameObject> m_SpawnedObstacles = new List<GameObject>();
    private IGridDTO       m_GridDTO;
    private TDEnemyPathView m_EnemyPathView;

    public void Initialize(IGridDTO initGridDTO)
    {
        m_StartPoint    = TDConstant.CONFIG_ENEMY_START_POINT;
        m_EndPoint      = TDConstant.CONFIG_ENEMY_END_POINT;
        m_SpawnPos      = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_CREATE_POINT).transform;
        m_EnemyPathView = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_VIEW).GetComponent<TDEnemyPathView>();

        m_GridDTO = initGridDTO;
        TDEnemyPathMainControl.api.InitEnemyPools(m_EnemyDataSettings, transform);
        ImplementPath();
    }

    private async void ImplementPath()
    {
        await TDInitializeModel.api.createGridCompletion.Task;
        m_EnemyPathView.RegistryValues();
        RegistryEvents();

        TDEnemyPathMainControl.api.InitEnemyPath(m_GridDTO, m_StartPoint, m_EndPoint);
        SpawnGates();

        TDEnemyPathMainControl.api.GenerateAllPaths(m_GridDTO, m_StartPoint, m_EndPoint);

        HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>(
            m_AllPaths.SelectMany(path => path.Select(cell => cell.position))
        );

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
            TDGridMainModel.api.SetOccupiedCell(startWorld);
        }
        else
            Debug.LogWarning("<color=orange>TDEnemyPathMainView: m_GateStartPrefab not assigned</color>");

        if (m_GateEndPrefab != null)
        {
            Vector3 endWorld = grid[m_EndPoint.x, m_EndPoint.y];
            Object.Instantiate(m_GateEndPrefab, endWorld, Quaternion.identity, transform);
            TDGridMainModel.api.SetOccupiedCell(endWorld);
        }
        else
            Debug.LogWarning("<color=orange>TDEnemyPathMainView: m_GateEndPrefab not assigned</color>");
    }

    private void RegistryEvents()
    {
        TDEnemyPathMainControl.api.onGetEnemyPos        += OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetAllPaths        += OnGetAllPaths;
        TDEnemyPathMainControl.api.onWaveStart          += OnWaveStart;
        TDObstacleControl.api.onObstaclesPlaced         += OnObstaclesPlaced;
    }

    private void OnDestroy()
    {
        m_WaveCts?.Cancel();
        m_WaveCts?.Dispose();

        TDEnemyPathMainControl.api.onGetEnemyPos        -= OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetAllPaths        -= OnGetAllPaths;
        TDEnemyPathMainControl.api.onWaveStart          -= OnWaveStart;
        TDObstacleControl.api.onObstaclesPlaced         -= OnObstaclesPlaced;
    }

    private void OnWaveStart(int waveIdx) => m_GateStartView?.PlaySpawnEffect();

    private void OnObstaclesPlaced(List<Vector2Int> gridPositions)
    {
        if (m_ObstaclePrefabs == null || m_ObstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("<color=orange>TDEnemyPathMainView: m_ObstaclePrefabs is empty</color>");
        }
        else
        {
            foreach (Vector2Int gp in gridPositions)
            {
                GameObject prefab = m_ObstaclePrefabs[Random.Range(0, m_ObstaclePrefabs.Length)];
                if (prefab == null) continue;

                Vector3    worldPos = TDGridMainModel.api.GetGrid()[gp.x, gp.y];
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject obj = Object.Instantiate(prefab, worldPos, rotation, transform);
                m_SpawnedObstacles.Add(obj);
                TDGridMainModel.api.SetOccupiedCell(worldPos);
            }
        }

        TDEnemyPathMainControl.api.ComputeValidTowerCells(
            m_GridDTO, m_AllPaths, gridPositions, m_StartPoint, m_EndPoint);
    }

    private void OnGetAllPaths(List<List<IGridCellDTO>> allPaths)
    {
        m_AllPaths = allPaths;
        if (m_AllPaths.Count == 0) return;

        m_EnemyPathView.VisualizeAllPaths(m_AllPaths);

        if (m_LevelConfigSettings == null)
        {
            Debug.LogError("<color=red>TDEnemyPathMainView: m_LevelConfigSettings not assigned in Inspector!</color>");
            return;
        }

        LevelConfig config = m_LevelConfigSettings.GetLevel(k_CurrentLevelIndex);
        if (config == null) return;

        TDGameStateControl.api?.Initialize(config.totalEnemies);

        m_WaveCts = new CancellationTokenSource();
        TDEnemyPathMainControl.api.StartWaveLoop(m_SpawnPos, m_AllPaths, config, m_WaveCts.Token);
    }

    private void OnGetEnemyPos(Vector2Int startPoint, Vector2Int endPoint)
    {
        m_StartPoint = startPoint;
        m_EndPoint   = endPoint;
    }
}

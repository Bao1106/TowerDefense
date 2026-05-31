using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Serialization;

public class TDEnemyPathMainView : MonoBehaviour
{
    [SerializeField] private GameObject[]          m_ObstaclePrefabs;
    [SerializeField] private GameObject            m_GateStartPrefab;
    [SerializeField] private GameObject            m_GateEndPrefab;
    [FormerlySerializedAs("m_EnemyDataSettings")] [SerializeField] private TDFlyweightEnemyDataSettings   flyweightEnemyDataSettings;
    [SerializeField] private TDLevelConfigSettings m_LevelConfigSettings;
    [SerializeField] private GameObject            m_TowerZonePrefab;

    private const int k_CurrentLevelIndex = 0;

    private Vector2Int m_StartPoint, m_EndPoint;
    private TDGateView m_GateStartView;
    private CancellationTokenSource m_WaveCts;

    private List<List<IGridCellDTO>> m_AllPaths = new List<List<IGridCellDTO>>();
    private readonly List<GameObject> m_TowerZoneTiles    = new List<GameObject>();
    private IGridDTO       m_GridDTO;
    private TDEnemyPathView m_EnemyPathView;

    public void Initialize(IGridDTO initGridDTO)
    {
        // Dynamic: left/right edge tại center row
        // Snap về even coordinate — maze Recursive Backtracker chỉ carve room cells tại even (x,y)
        int centerY = TDGridMainModel.api.height / 2;
        if (centerY % 2 != 0) centerY--;

        int lastCol = TDGridMainModel.api.width - 1;
        if (lastCol % 2 != 0) lastCol--;

        m_StartPoint = new Vector2Int(0,       centerY);
        m_EndPoint   = new Vector2Int(lastCol, centerY);
        m_EnemyPathView = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_VIEW).GetComponent<TDEnemyPathView>();

        m_GridDTO = initGridDTO;
        TDEnemyPathMainControl.api.InitEnemyPools(flyweightEnemyDataSettings, transform);
        ImplementPath();
    }

    private async void ImplementPath()
    {
        await TDInitializeModel.api.createGridCompletion.Task;
        m_EnemyPathView.RegistryValues();
        RegistryEvents();

        TDEnemyPathMainControl.api.InitEnemyPath(m_GridDTO, m_StartPoint, m_EndPoint);
        SpawnGates();

        // Maze: GenerateAllPaths → onGetAllPaths → OnGetAllPaths → ComputeValidTowerCells → tower tiles
        TDEnemyPathMainControl.api.GenerateAllPaths(m_GridDTO, m_StartPoint, m_EndPoint);
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
        TDEnemyPathMainControl.api.onGetEnemyPos          += OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetAllPaths          += OnGetAllPaths;
        TDEnemyPathMainControl.api.onWaveStart            += OnWaveStart;
        TDEnemyPathMainControl.api.onValidTowerCellsReady += OnValidTowerCellsReady;
    }

    private void OnDestroy()
    {
        m_WaveCts?.Cancel();
        m_WaveCts?.Dispose();

        TDEnemyPathMainControl.api.onGetEnemyPos          -= OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetAllPaths          -= OnGetAllPaths;
        TDEnemyPathMainControl.api.onWaveStart            -= OnWaveStart;
        TDEnemyPathMainControl.api.onValidTowerCellsReady -= OnValidTowerCellsReady;
    }

    private void OnWaveStart(int waveIdx) => m_GateStartView?.PlaySpawnEffect();

    private void OnGetAllPaths(List<List<IGridCellDTO>> allPaths)
    {
        m_AllPaths = allPaths;
        if (m_AllPaths.Count == 0) return;

        m_EnemyPathView.VisualizeAllPaths(m_AllPaths);

        // Maze B1: wall cells = valid tower spots — compute and spawn tower zone tiles
        TDEnemyPathMainControl.api.ComputeValidTowerCells(m_GridDTO, m_StartPoint, m_EndPoint);

        if (m_LevelConfigSettings == null)
        {
            Debug.LogError("<color=red>TDEnemyPathMainView: m_LevelConfigSettings not assigned in Inspector!</color>");
            return;
        }

        LevelConfig config = m_LevelConfigSettings.GetLevel(k_CurrentLevelIndex);
        if (config == null) return;

        TDGameStateControl.api?.Initialize(config.totalEnemies);

        // Spawn tại world pos của start cell — không dùng CreatePoint cố định
        Vector3 spawnWorldPos = TDGridMainModel.api.CellToWorld(m_StartPoint);
        m_WaveCts = new CancellationTokenSource();
        TDEnemyPathMainControl.api.StartWaveLoop(spawnWorldPos, m_AllPaths, config, m_WaveCts.Token);
    }

    private void OnGetEnemyPos(Vector2Int startPoint, Vector2Int endPoint)
    {
        m_StartPoint = startPoint;
        m_EndPoint   = endPoint;
    }

    private void OnValidTowerCellsReady(List<Vector3> validPositions)
    {
        if (m_TowerZonePrefab == null)
        {
            Debug.LogWarning("[TDEnemyPathMainView] m_TowerZonePrefab not assigned — tower zone tiles skipped");
            return;
        }

        bool hasObstacles    = m_ObstaclePrefabs != null && m_ObstaclePrefabs.Length > 0;
        int[] footprintRadii = hasObstacles ? ComputeFootprintRadii() : null;

        // Shuffle để random hoá vị trí obstacle
        var positions = new List<Vector3>(validPositions);
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        int obstacleCount = Mathf.RoundToInt(positions.Count * TDConstant.CONFIG_MAZE_OBSTACLE_WALL_RATIO);

        // Phase 1: place obstacles + block toàn bộ footprint cells
        var blockedCells = new HashSet<Vector2Int>();

        for (int i = 0; i < obstacleCount && hasObstacles; i++)
        {
            Vector3    center     = new Vector3(positions[i].x, 0f, positions[i].z);
            int        pIdx       = Random.Range(0, m_ObstaclePrefabs.Length);
            int        radius     = footprintRadii != null ? footprintRadii[pIdx] : 0;
            Quaternion rotation   = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Object.Instantiate(m_ObstaclePrefabs[pIdx], center, rotation, transform);

            // Block center + tất cả cells trong footprint (2*radius+1)²
            Vector2Int centerCell = TDGridMainModel.api.WorldToCell(center);
            for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    var cell = new Vector2Int(centerCell.x + dx, centerCell.y + dz);
                    if (!TDGridMainModel.api.IsInBounds(cell)) continue;
                    blockedCells.Add(cell);
                    TDGridMainModel.api.SetOccupiedCell(TDGridMainModel.api.CellToWorld(cell));
                }
        }

        // Phase 2: tower zone tiles chỉ trên cells KHÔNG bị obstacle footprint block
        foreach (var pos in positions)
        {
            var cell = TDGridMainModel.api.WorldToCell(pos);
            if (blockedCells.Contains(cell)) continue;

            var tile = Object.Instantiate(m_TowerZonePrefab,
                new Vector3(pos.x, 0f, pos.z), Quaternion.identity, transform);
            m_TowerZoneTiles.Add(tile);
        }

        Debug.Log($"[TDEnemyPathMainView] {m_TowerZoneTiles.Count} tower tiles | {obstacleCount} obstacles | {blockedCells.Count} footprint cells blocked");
    }

    // Tính footprint radius từ Renderer.bounds thực tế của prefab
    // radius=0 → 1×1 cell | radius=1 → 3×3 cells
    private int[] ComputeFootprintRadii()
    {
        float cellSize = TDConstant.CONFIG_GRID_CELL_SIZE;
        int[] radii    = new int[m_ObstaclePrefabs.Length];

        for (int i = 0; i < m_ObstaclePrefabs.Length; i++)
        {
            if (m_ObstaclePrefabs[i] == null) continue;

            var inst = Object.Instantiate(m_ObstaclePrefabs[i]);
            inst.SetActive(false);

            Bounds b     = new Bounds();
            bool   first = true;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            Object.Destroy(inst);

            if (!first)
            {
                float halfMax = Mathf.Max(b.size.x, b.size.z) * 0.5f;
                radii[i] = Mathf.Max(0, Mathf.CeilToInt(halfMax / cellSize - 0.5f));
            }
        }

        Debug.Log($"[EnemyPathMainView] FootprintRadii: [{string.Join(", ", radii)}]");
        return radii;
    }
}

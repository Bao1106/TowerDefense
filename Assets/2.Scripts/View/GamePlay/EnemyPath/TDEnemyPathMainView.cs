using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Linq;
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
    private Transform  m_SpawnPos;
    private TDGateView m_GateStartView;
    private CancellationTokenSource m_WaveCts;

    private List<List<IGridCellDTO>> m_AllPaths = new List<List<IGridCellDTO>>();
    private readonly List<GameObject> m_SpawnedObstacles  = new List<GameObject>();
    private readonly List<GameObject> m_TowerZoneTiles    = new List<GameObject>();
    private IGridDTO       m_GridDTO;
    private TDEnemyPathView m_EnemyPathView;

    public void Initialize(IGridDTO initGridDTO)
    {
        m_StartPoint    = TDConstant.CONFIG_ENEMY_START_POINT;
        m_EndPoint      = TDConstant.CONFIG_ENEMY_END_POINT;
        m_SpawnPos      = GameObject.Find(TDConstant.GAMEPLAY_ENEMY_PATH_CREATE_POINT).transform;
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

        TDEnemyPathMainControl.api.GenerateAllPaths(m_GridDTO, m_StartPoint, m_EndPoint);

        HashSet<Vector2Int> pathCells = new HashSet<Vector2Int>(
            m_AllPaths.SelectMany(path => path.Select(cell => cell.position))
        );

        int[] footprintRadii = ComputeFootprintRadii();
        TDObstacleControl.api.PlaceObstacles(m_GridDTO, m_StartPoint, m_EndPoint, pathCells,
            m_ObstaclePrefabs?.Length ?? 0, footprintRadii);
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
        TDObstacleControl.api.onObstaclesPlaced           += OnObstaclesPlaced;
    }

    private void OnDestroy()
    {
        m_WaveCts?.Cancel();
        m_WaveCts?.Dispose();

        TDEnemyPathMainControl.api.onGetEnemyPos          -= OnGetEnemyPos;
        TDEnemyPathMainControl.api.onGetAllPaths          -= OnGetAllPaths;
        TDEnemyPathMainControl.api.onWaveStart            -= OnWaveStart;
        TDEnemyPathMainControl.api.onValidTowerCellsReady -= OnValidTowerCellsReady;
        TDObstacleControl.api.onObstaclesPlaced           -= OnObstaclesPlaced;
    }

    private void OnWaveStart(int waveIdx) => m_GateStartView?.PlaySpawnEffect();

    private void OnObstaclesPlaced(List<Vector2Int> centers, List<int> prefabIndices, List<Vector2Int> allBlocked)
    {
        if (m_ObstaclePrefabs == null || m_ObstaclePrefabs.Length == 0)
        {
            Debug.LogWarning("<color=orange>TDEnemyPathMainView: m_ObstaclePrefabs is empty</color>");
        }
        else
        {
            // Spawn visual tại center, dùng pre-assigned prefab index để khớp đúng footprint
            Vector3[,] grid = TDGridMainModel.api.GetGrid();
            for (int i = 0; i < centers.Count; i++)
            {
                int        pIdx    = (prefabIndices != null && i < prefabIndices.Count)
                    ? prefabIndices[i] : Random.Range(0, m_ObstaclePrefabs.Length);
                GameObject prefab  = m_ObstaclePrefabs[pIdx];
                if (prefab == null) continue;

                Vector3    worldPos = grid[centers[i].x, centers[i].y];
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                m_SpawnedObstacles.Add(Object.Instantiate(prefab, worldPos, rotation, transform));
            }

            // Mark TẤT CẢ footprint cells là occupied (center + vùng lân cận của prefab lớn)
            foreach (Vector2Int bc in allBlocked)
                TDGridMainModel.api.SetOccupiedCell(grid[bc.x, bc.y]);
        }

        // ComputeValidTowerCells nhận allBlocked để loại đúng toàn bộ vùng obstacle ra khỏi valid zone
        TDEnemyPathMainControl.api.ComputeValidTowerCells(
            m_GridDTO, m_AllPaths, allBlocked, m_StartPoint, m_EndPoint);
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

    // Spawn tower zone tile (cube mỏng, màu sáng) trên mỗi valid tower cell
    // Tạo hiệu ứng "gờ" Arknights: ground tối = path, cube sáng = nơi đặt tower
    // Tự tính footprint radius cho mỗi prefab dựa trên Renderer bounds thực tế.
    // radius = max(0, ceil(max(size.x, size.z) / (2 * cellSize) - 0.5))
    //   → radius=0 khi prefab vừa khớp trong 1 cell (ví dụ Shrub nhỏ)
    //   → radius=1 khi prefab tràn ra cell lân cận (Rock lớn, Tree)
    // Không cần mảng hardcode trong Inspector — tự cập nhật khi thay prefab.
    private int[] ComputeFootprintRadii()
    {
        if (m_ObstaclePrefabs == null) return System.Array.Empty<int>();
        float cellSize = TDConstant.CONFIG_GRID_CELL_SIZE;
        int[] radii = new int[m_ObstaclePrefabs.Length];

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

    private void OnValidTowerCellsReady(List<Vector3> validPositions)
    {
        if (m_TowerZonePrefab == null)
        {
            Debug.LogWarning("[TDEnemyPathMainView] m_TowerZonePrefab not assigned — tower zone tiles skipped");
            return;
        }

        foreach (Vector3 pos in validPositions)
        {
            // y=0: cube cao 0.12 → top face ở y=0.06, front face visible từ camera 30° = "gờ"
            Vector3 tilePos = new Vector3(pos.x, 0f, pos.z);
            var tile = Object.Instantiate(m_TowerZonePrefab, tilePos, Quaternion.identity, transform);
            m_TowerZoneTiles.Add(tile);
        }

        Debug.Log($"[TDEnemyPathMainView] Spawned {m_TowerZoneTiles.Count} tower zone tiles");
    }
}

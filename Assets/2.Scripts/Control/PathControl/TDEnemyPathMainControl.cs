using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDEnums;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

public class TDEnemyPathMainControl
{
    public static TDEnemyPathMainControl api;

    public Action<Vector2Int, Vector2Int> onGetEnemyPos;
    public Action<List<List<IGridCellDTO>>> onGetAllPaths;
    public Action<List<TDEnemyView>> onGetEnemies;
    public Action<int> onWaveStart;

    // ── Enemy Object Pool ────────────────────────────────────────────────────
    private IObjectPool<TDEnemyView> m_EnemyPool;

    public void InitEnemyPool(TDEnemyView prefab, Transform parent, int defaultCapacity = 20, int maxSize = 50)
    {
        m_EnemyPool = new ObjectPool<TDEnemyView>(
            createFunc:      () =>
            {
                var e = Object.Instantiate(prefab, parent);
                e.transform.localScale = Vector3.one * TDConstant.CONFIG_ENEMY_VISUAL_SCALE;
                return e;
            },
            actionOnGet:     e => e.gameObject.SetActive(true),
            actionOnRelease: e => e.gameObject.SetActive(false),
            actionOnDestroy: e => Object.Destroy(e.gameObject),
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize:         maxSize
        );
    }

    public void ReturnEnemy(TDEnemyView enemy)
    {
        m_EnemyPool?.Release(enemy);
    }

    // Delay nhận biết pause: dùng Time.deltaTime (= 0 khi timeScale=0) thay vì wall-clock
    private static async Task PauseAwareDelay(int ms, CancellationToken ct)
    {
        float target = ms / 1000f;
        float elapsed = 0f;
        while (elapsed < target)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            elapsed += Time.deltaTime;
        }
    }

    public void InitEnemyPath(IGridDTO gridDTO, Vector2Int startPoint, Vector2Int endPoint)
    {
        // startPoint/endPoint đã là GRID coords (từ TDConstant) — không cần round-trip qua world
        // Old code convert (0,7) → world (0,0,14) MÀ QUÊN cộng offset → GetNearestGridPosition clamp sai → START dịch sang giữa grid
        // Chỉ cần clamp vào bounds để defensive
        startPoint.x = Mathf.Clamp(startPoint.x, 0, gridDTO.width  - 1);
        startPoint.y = Mathf.Clamp(startPoint.y, 0, gridDTO.height - 1);
        endPoint.x   = Mathf.Clamp(endPoint.x,   0, gridDTO.width  - 1);
        endPoint.y   = Mathf.Clamp(endPoint.y,   0, gridDTO.height - 1);

        gridDTO.SetCell(startPoint.x, startPoint.y, new TDGridCellDTO(startPoint.x, startPoint.y, CellType.Start));
        gridDTO.SetCell(endPoint.x,   endPoint.y,   new TDGridCellDTO(endPoint.x,   endPoint.y,   CellType.End));

        onGetEnemyPos?.Invoke(startPoint, endPoint);
    }

    // Gen random waypoints mỗi path trong Y-zone riêng → BuildDefinedPath vẽ straight segments
    // → path shape random mỗi game, vẫn guarantee không chaos vì zone-separated
    public void GenerateAllPaths(IGridDTO gridDTO, Vector2Int startPoint, Vector2Int endPoint)
    {
        var allPaths = new List<List<IGridCellDTO>>();

        for (int i = 0; i < TDConstant.CONFIG_PATH_Y_ZONES.Length; i++)
        {
            Vector2Int zone = TDConstant.CONFIG_PATH_Y_ZONES[i];

            // 1. Sinh random waypoints trong zone Y=[zone.x..zone.y]
            Vector2Int[] waypoints = TDPathGeneratorControl.api.GenerateRandomWaypoints(
                startPoint, endPoint, zone.x, zone.y);

            // 2. Vẽ straight segments giữa waypoints (không A*)
            List<IGridCellDTO> path = TDaStarPathControl.api.BuildDefinedPath(
                gridDTO, startPoint, endPoint, waypoints);

            if (path != null && path.Count > 0)
            {
                allPaths.Add(path);
                Debug.Log($"<color=cyan>GenerateAllPaths: Path {i} (zone Y={zone.x}..{zone.y}) → {waypoints.Length} waypoints, {path.Count} cells</color>");
            }
            else
            {
                Debug.Log($"<color=red>GenerateAllPaths: Path {i} failed to build</color>");
            }
        }

        Debug.Log($"<color=cyan>GenerateAllPaths: built {allPaths.Count}/{TDConstant.CONFIG_PATH_Y_ZONES.Length} random paths</color>");
        onGetAllPaths?.Invoke(allPaths);
    }

    // Tính tập hợp các ô hợp lệ để đặt tower:
    // valid = tất cả cells − pathCells − obstacleCells − start − end
    // Kết quả là world positions (đã có offset) để view dùng trực tiếp
    public void ComputeValidTowerCells(IGridDTO gridDTO, List<List<IGridCellDTO>> allPaths,
        List<Vector2Int> obstacleCells, Vector2Int start, Vector2Int end)
    {
        var forbidden = new HashSet<Vector2Int> { start, end };
        foreach (var path in allPaths)
            foreach (var cell in path)
                forbidden.Add(cell.position);
        foreach (var obs in obstacleCells)
            forbidden.Add(obs);

        Vector3[,] grid = TDGridMainModel.api.GetGrid();
        var validPositions = new List<Vector3>();
        for (int x = 0; x < gridDTO.width; x++)
            for (int y = 0; y < gridDTO.height; y++)
                if (!forbidden.Contains(new Vector2Int(x, y)))
                    validPositions.Add(grid[x, y]);

        Debug.Log($"<color=cyan>ComputeValidTowerCells: {validPositions.Count} valid cells</color>");
        onValidTowerCellsReady?.Invoke(validPositions);
    }

    public Action<List<Vector3>> onValidTowerCellsReady;

    // Wave loop chính — mỗi wave pick random 1 corridor từ allPaths → spawn N enemies trên đó
    // Maze cố định (allPaths gen 1 lần lúc start), variety đến từ wave-level corridor switching
    // CancellationToken để dừng sạch khi scene unload (tránh exception trên destroyed objects)
    public async void StartWaveLoop(Transform spawnPos,
        List<List<IGridCellDTO>> allPaths, CancellationToken ct)
    {
        if (allPaths == null || allPaths.Count == 0)
        {
            Debug.LogError("<color=red>StartWaveLoop: no paths available</color>");
            return;
        }

        int maxWaves = TDConstant.CONFIG_MAX_WAVES > 0
            ? TDConstant.CONFIG_MAX_WAVES
            : int.MaxValue;

        try
        {
            for (int waveIdx = 0; waveIdx < maxWaves; waveIdx++)
            {
                ct.ThrowIfCancellationRequested();

                int pathIdx = UnityEngine.Random.Range(0, allPaths.Count);
                List<IGridCellDTO> wavePath = allPaths[pathIdx];

                Debug.Log($"<color=green>Wave {waveIdx + 1}/{maxWaves} starting → corridor {pathIdx} ({wavePath.Count} cells)</color>");
                onWaveStart?.Invoke(waveIdx);

                await SpawnWave(spawnPos, wavePath, waveIdx, ct);

                await PauseAwareDelay(TDConstant.CONFIG_WAVE_INTERVAL_MS, ct);
            }

            Debug.Log($"<color=green>All {maxWaves} waves completed!</color>");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("<color=yellow>StartWaveLoop: cancelled (scene unloaded)</color>");
        }
    }

    // Spawn N enemies stagger (1 enemy mỗi CONFIG_ENEMY_SPAWN_DELAY_MS) → assign path ngay khi spawn
    private async Task SpawnWave(Transform spawnPos,
        List<IGridCellDTO> path, int waveIdx, CancellationToken ct)
    {
        List<TDEnemyView> waveEnemies = new List<TDEnemyView>();

        for (int i = 0; i < TDConstant.CONFIG_ENEMIES_NUMBER; i++)
        {
            ct.ThrowIfCancellationRequested();

            TDEnemyView enemy = m_EnemyPool.Get();
            enemy.transform.position = spawnPos.position;
            enemy.transform.rotation = Quaternion.identity;

            string key = $"w{waveIdx}-e{i}-{enemy.gameObject.GetInstanceID()}";
            enemy.Initialize(key);
            enemy.SetPath(path);
            waveEnemies.Add(enemy);

            await PauseAwareDelay(TDConstant.CONFIG_ENEMY_SPAWN_DELAY_MS, ct);
        }

        onGetEnemies?.Invoke(waveEnemies);
    }
}

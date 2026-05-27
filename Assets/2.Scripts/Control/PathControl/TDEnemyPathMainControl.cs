using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TDEnums;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class TDEnemyPathMainControl
{
    public static TDEnemyPathMainControl api;

    public Action<Vector2Int, Vector2Int>      onGetEnemyPos;
    public Action<List<List<IGridCellDTO>>>    onGetAllPaths;
    public Action<int>                         onWaveStart;
    public Action<List<Vector3>>               onValidTowerCellsReady;

    // ── Enemy Object Pools (1 per EnemyType) ─────────────────────────────────
    private readonly Dictionary<EnemyType, IObjectPool<TDEnemyView>> m_EnemyPools
        = new Dictionary<EnemyType, IObjectPool<TDEnemyView>>();
    private TDEnemyDataSettings m_EnemyDataSettings;

    public void InitEnemyPools(TDEnemyDataSettings settings, Transform parent,
        int defaultCapacity = 10, int maxSize = 30)
    {
        m_EnemyDataSettings = settings;

        foreach (EnemyType type in Enum.GetValues(typeof(EnemyType)))
        {
            var data = settings.GetData(type);
            if (data?.prefab == null)
            {
                Debug.LogWarning($"[TDEnemyPathMainControl] No prefab for EnemyType.{type} — pool skipped");
                continue;
            }

            var capturedData   = data;
            var capturedParent = parent;

            m_EnemyPools[type] = new ObjectPool<TDEnemyView>(
                createFunc: () =>
                {
                    var go   = Object.Instantiate(capturedData.prefab, capturedParent);
                    var view = go.GetComponent<TDEnemyView>() ?? go.AddComponent<TDEnemyView>();
                    return view;
                },
                actionOnGet:     e => e.gameObject.SetActive(true),
                actionOnRelease: e => e.gameObject.SetActive(false),
                actionOnDestroy: e => Object.Destroy(e.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize:         maxSize
            );
        }
    }

    public void ReturnEnemy(TDEnemyView enemy)
    {
        if (m_EnemyPools.TryGetValue(enemy.EnemyType, out var pool))
            pool.Release(enemy);
        else
            Debug.LogError($"[TDEnemyPathMainControl] No pool for EnemyType.{enemy.EnemyType}");
    }

    // ── Path / Grid Setup ────────────────────────────────────────────────────

    public void InitEnemyPath(IGridDTO gridDTO, Vector2Int startPoint, Vector2Int endPoint)
    {
        startPoint.x = Mathf.Clamp(startPoint.x, 0, gridDTO.width  - 1);
        startPoint.y = Mathf.Clamp(startPoint.y, 0, gridDTO.height - 1);
        endPoint.x   = Mathf.Clamp(endPoint.x,   0, gridDTO.width  - 1);
        endPoint.y   = Mathf.Clamp(endPoint.y,   0, gridDTO.height - 1);

        gridDTO.SetCell(startPoint.x, startPoint.y, new TDGridCellDTO(startPoint.x, startPoint.y, CellType.Start));
        gridDTO.SetCell(endPoint.x,   endPoint.y,   new TDGridCellDTO(endPoint.x,   endPoint.y,   CellType.End));

        onGetEnemyPos?.Invoke(startPoint, endPoint);
    }

    public void GenerateAllPaths(IGridDTO gridDTO, Vector2Int startPoint, Vector2Int endPoint)
    {
        var allPaths = new List<List<IGridCellDTO>>();

        for (int i = 0; i < TDConstant.CONFIG_PATH_Y_ZONES.Length; i++)
        {
            Vector2Int zone = TDConstant.CONFIG_PATH_Y_ZONES[i];
            Vector2Int[] waypoints = TDPathGeneratorControl.api.GenerateRandomWaypoints(
                startPoint, endPoint, zone.x, zone.y);
            List<IGridCellDTO> path = TDaStarPathControl.api.BuildDefinedPath(
                gridDTO, startPoint, endPoint, waypoints);

            if (path != null && path.Count > 0)
            {
                allPaths.Add(path);
                Debug.Log($"<color=cyan>GenerateAllPaths: Path {i} (zone Y={zone.x}..{zone.y}) → {waypoints.Length} waypoints, {path.Count} cells</color>");
            }
            else
                Debug.LogWarning($"<color=orange>GenerateAllPaths: Path {i} failed</color>");
        }

        Debug.Log($"<color=cyan>GenerateAllPaths: built {allPaths.Count}/{TDConstant.CONFIG_PATH_Y_ZONES.Length} paths</color>");
        onGetAllPaths?.Invoke(allPaths);
    }

    public void ComputeValidTowerCells(IGridDTO gridDTO, List<List<IGridCellDTO>> allPaths,
        List<Vector2Int> obstacleCells, Vector2Int start, Vector2Int end)
    {
        var forbidden = new HashSet<Vector2Int> { start, end };
        foreach (var path in allPaths)
            foreach (var cell in path)
                forbidden.Add(cell.position);
        foreach (var obs in obstacleCells)
            forbidden.Add(obs);

        Vector3[,] grid         = TDGridMainModel.api.GetGrid();
        var        validPositions = new List<Vector3>();
        for (int x = 0; x < gridDTO.width; x++)
            for (int y = 0; y < gridDTO.height; y++)
                if (!forbidden.Contains(new Vector2Int(x, y)))
                    validPositions.Add(grid[x, y]);

        Debug.Log($"<color=cyan>ComputeValidTowerCells: {validPositions.Count} valid cells</color>");
        onValidTowerCellsReady?.Invoke(validPositions);
    }

    // ── Wave Loop (LevelConfig-driven) ───────────────────────────────────────
    // allPaths[0] = corridor 1, allPaths[1] = corridor 2 (fallback to [0] nếu chỉ có 1)
    public async void StartWaveLoop(Transform spawnPos,
        List<List<IGridCellDTO>> allPaths, LevelConfig config, CancellationToken ct)
    {
        if (allPaths == null || allPaths.Count == 0)
        {
            Debug.LogError("<color=red>StartWaveLoop: no paths available</color>");
            return;
        }

        List<List<EnemyType>>         waveBatches = BuildWavePlans(config.difficulty, config.waveCount, config.totalEnemies);
        DifficultyRatioTable.RatioRow ratio        = DifficultyRatioTable.Get(config.difficulty);

        Debug.Log($"<color=green>StartWaveLoop: {waveBatches.Count} waves, difficulty={config.difficulty}</color>");

        try
        {
            for (int waveIdx = 0; waveIdx < waveBatches.Count; waveIdx++)
            {
                ct.ThrowIfCancellationRequested();

                List<IGridCellDTO> corridor = allPaths[Random.Range(0, allPaths.Count)];

                Debug.Log($"<color=green>Wave {waveIdx + 1}/{waveBatches.Count} — {waveBatches[waveIdx].Count} enemies</color>");
                onWaveStart?.Invoke(waveIdx);

                await SpawnBatch(spawnPos, corridor, waveBatches[waveIdx], ratio, waveIdx, config.spawnInterval, ct);

                await PauseAwareDelay(config.waveInterval, ct);
            }

            Debug.Log($"<color=green>All {waveBatches.Count} waves completed!</color>");
            TDGameStateControl.api?.OnAllWavesSpawned();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("<color=yellow>StartWaveLoop: cancelled (scene unloaded)</color>");
        }
    }

    private async Task SpawnBatch(Transform spawnPos, List<IGridCellDTO> path,
        List<EnemyType> batch, DifficultyRatioTable.RatioRow ratio,
        int waveIdx, float spawnInterval, CancellationToken ct)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            EnemyType type  = batch[i];
            EnemyData data  = m_EnemyDataSettings?.GetData(type);
            float     hp    = (data?.baseHP    ?? 300f) * ratio.hpMult;
            float     speed = (data?.baseSpeed ?? 3f)   * ratio.speedMult;

            if (!m_EnemyPools.TryGetValue(type, out var pool))
            {
                Debug.LogWarning($"[SpawnBatch] Pool missing for {type}, skipping");
                continue;
            }

            TDEnemyView enemy = pool.Get();
            enemy.transform.position = spawnPos.position;
            enemy.transform.rotation = Quaternion.identity;

            string key = $"w{waveIdx}-{type}-e{i}-{enemy.gameObject.GetInstanceID()}";
            enemy.Initialize(key, hp, speed, data.dieDuration, data?.goldReward ?? 0, type);
            enemy.SetPath(path);

            await PauseAwareDelay(spawnInterval, ct);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Builds per-wave enemy lists.
    // Extreme/Nightmare: clamps waveCount≥15 and totalEnemies≥75.
    // Boss waves have 2.5× more enemies than regular waves and spawn boss(es) last.
    private static List<List<EnemyType>> BuildWavePlans(Difficulty difficulty, int waveCount, int totalEnemies)
    {
        if (difficulty >= Difficulty.Extreme)
        {
            waveCount    = Mathf.Max(waveCount, 15);
            totalEnemies = Mathf.Max(totalEnemies, 75);
        }

        var (bossWaveCount, bossPerWave, bossWaveMult) = GetBossParams(difficulty);
        bossWaveCount = Mathf.Min(bossWaveCount, waveCount);

        int   regularWaveCount = waveCount - bossWaveCount;
        // Solve: regularWaveCount*r + bossWaveCount*(r*mult) = totalEnemies
        float r                = totalEnemies / (regularWaveCount + bossWaveCount * bossWaveMult);
        int   regularSize      = Mathf.Max(1, Mathf.RoundToInt(r));
        int   bossWaveSize     = Mathf.Max(bossPerWave + 1, Mathf.RoundToInt(r * bossWaveMult));

        // Boss wave positions: boss k → wave ceil(W*k/B)-1 (0-indexed)
        var bossWaveIndices = new HashSet<int>();
        for (int k = 1; k <= bossWaveCount; k++)
        {
            int idx = Mathf.Clamp(Mathf.CeilToInt((float)waveCount * k / bossWaveCount) - 1, 0, waveCount - 1);
            bossWaveIndices.Add(idx);
        }

        var   ratio        = DifficultyRatioTable.Get(difficulty);
        float nonBossTotal = ratio.normalPct + ratio.fastPct + ratio.tankPct;

        var plans = new List<List<EnemyType>>(waveCount);
        for (int w = 0; w < waveCount; w++)
        {
            bool isBossWave   = bossWaveIndices.Contains(w);
            int  nonBossCount = isBossWave ? bossWaveSize - bossPerWave : regularSize;

            int normalCount = Mathf.RoundToInt(ratio.normalPct / nonBossTotal * nonBossCount);
            int fastCount   = Mathf.RoundToInt(ratio.fastPct   / nonBossTotal * nonBossCount);
            int tankCount   = Mathf.Max(0, nonBossCount - normalCount - fastCount);

            var wave = new List<EnemyType>(nonBossCount + (isBossWave ? bossPerWave : 0));
            for (int i = 0; i < normalCount; i++) wave.Add(EnemyType.Normal);
            for (int i = 0; i < fastCount;   i++) wave.Add(EnemyType.Fast);
            for (int i = 0; i < tankCount;   i++) wave.Add(EnemyType.Tank);

            // Shuffle non-boss enemies
            for (int i = wave.Count - 1; i > 0; i--)
            {
                int rIdx = Random.Range(0, i + 1);
                (wave[i], wave[rIdx]) = (wave[rIdx], wave[i]);
            }

            // Boss(es) always spawn last in their wave
            if (isBossWave)
                for (int i = 0; i < bossPerWave; i++)
                    wave.Add(EnemyType.Boss);

            plans.Add(wave);
        }

        Debug.Log($"<color=cyan>BuildWavePlans: {waveCount} waves | regular={regularSize} enemy | " +
                  $"bossWaves={bossWaveCount} ({bossWaveSize} enemy, {bossPerWave} boss/wave)</color>");
        return plans;
    }

    private static (int bossWaves, int bossPerWave, float bossWaveMult) GetBossParams(Difficulty d) => d switch
    {
        Difficulty.Easy      => (1, 1, 2.0f),
        Difficulty.Normal    => (1, 1, 2.0f),
        Difficulty.Hard      => (2, 1, 2.5f),
        Difficulty.Extreme   => (3, 1, 2.5f),
        Difficulty.Nightmare => (5, 2, 2.5f),
        _                    => (1, 1, 2.0f),
    };

    // Pause-aware delay: Time.deltaTime = 0 khi timeScale=0 → tự dừng khi pause
    private static async Task PauseAwareDelay(float seconds, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            elapsed += Time.deltaTime;
        }
    }
}

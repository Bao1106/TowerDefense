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
    private TDFlyweightEnemyDataSettings m_FlyweightEnemyDataSettings;

    public void InitEnemyPools(TDFlyweightEnemyDataSettings settings, Transform parent,
        int defaultCapacity = 10, int maxSize = 30)
    {
        m_FlyweightEnemyDataSettings = settings;

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

    // Maze Extraction: gen N mazes độc lập, mỗi maze cho 1 path, combine thành unified grid
    public void GenerateAllPaths(IGridDTO gridDTO, Vector2Int startPoint, Vector2Int endPoint)
    {
        List<List<IGridCellDTO>> allPaths =
            TDMazePathGenerator.api.GenerateMultiplePaths(gridDTO, startPoint, endPoint, TDConstant.CONFIG_NUM_PATHS);

        Debug.Log($"<color=cyan>GenerateAllPaths (Maze): {allPaths.Count}/{TDConstant.CONFIG_NUM_PATHS} paths</color>");
        onGetAllPaths?.Invoke(allPaths);
    }

    // Maze B1: valid tower cells = wall cells (non-walkable) excluding start/end buffer + occupied cells
    public void ComputeValidTowerCells(IGridDTO gridDTO, Vector2Int start, Vector2Int end)
    {
        // Exclusion zone: start/end + 2-cell radius — prevent obstacles spawning at gate entrances
        const int gateBuffer = 2;
        var excluded = new HashSet<Vector2Int>();
        for (int dx = -gateBuffer; dx <= gateBuffer; dx++)
            for (int dy = -gateBuffer; dy <= gateBuffer; dy++)
            {
                var s = new Vector2Int(start.x + dx, start.y + dy);
                var e = new Vector2Int(end.x   + dx, end.y   + dy);
                if (s.x >= 0 && s.x < gridDTO.width && s.y >= 0 && s.y < gridDTO.height) excluded.Add(s);
                if (e.x >= 0 && e.x < gridDTO.width && e.y >= 0 && e.y < gridDTO.height) excluded.Add(e);
            }

        Vector3[,] grid          = TDGridMainModel.api.GetGrid();
        var        validPositions = new List<Vector3>();

        for (int x = 0; x < gridDTO.width; x++)
            for (int y = 0; y < gridDTO.height; y++)
            {
                if (gridDTO.GetCell(x, y).isWalkable) continue;           // skip path cells
                if (excluded.Contains(new Vector2Int(x, y))) continue;    // skip gate buffer
                if (!TDGridMainModel.api.IsValidPlacement(grid[x, y])) continue; // skip occupied (path tiles already SetOccupied)
                validPositions.Add(grid[x, y]);
            }

        Debug.Log($"<color=cyan>ComputeValidTowerCells (Maze): {validPositions.Count} wall cells = tower spots</color>");
        onValidTowerCellsReady?.Invoke(validPositions);
    }

    // ── Wave Loop (LevelConfig-driven) ───────────────────────────────────────
    // allPaths[0] = corridor 1, allPaths[1] = corridor 2 (fallback to [0] nếu chỉ có 1)
    public async void StartWaveLoop(Vector3 spawnWorldPos,
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

                await SpawnBatch(spawnWorldPos, corridor, waveBatches[waveIdx], ratio, waveIdx, config.spawnInterval, ct);

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

    private async Task SpawnBatch(Vector3 spawnWorldPos, List<IGridCellDTO> path,
        List<EnemyType> batch, DifficultyRatioTable.RatioRow ratio,
        int waveIdx, float spawnInterval, CancellationToken ct)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            EnemyType type  = batch[i];
            EnemyData data  = m_FlyweightEnemyDataSettings?.GetData(type);
            float     hp          = (data?.baseHP    ?? 300f) * ratio.hpMult;
            float     speed       = (data?.baseSpeed ?? 3f)   * ratio.speedMult;
            float     atkDamage   = data?.baseAttackDamage ?? 10f;
            float     atkSpeed    = data?.baseAttackSpeed  ?? 1f;

            if (!m_EnemyPools.TryGetValue(type, out var pool))
            {
                Debug.LogWarning($"[SpawnBatch] Pool missing for {type}, skipping");
                continue;
            }

            TDEnemyView enemy = pool.Get();
            enemy.transform.position = spawnWorldPos;
            enemy.transform.rotation = Quaternion.identity;

            string key = $"w{waveIdx}-{type}-e{i}-{enemy.gameObject.GetInstanceID()}";
            enemy.Initialize(key, hp, speed, atkDamage, atkSpeed, data.dieDuration, data?.goldReward ?? 0, type);
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

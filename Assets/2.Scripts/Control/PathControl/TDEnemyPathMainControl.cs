using System;
using System.Collections.Generic;
using System.Linq;
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

    public Action<List<TDPathGroup>>  onGroupsReady;
    public Action<int, int>           onWaveGroupStart;   // (waveIdx, groupIdx)
    public Action<List<Vector3>>      onValidTowerCellsReady;

    private IGateAssignmentStrategy m_Strategy = new RoundRobinStrategy();

    public void SetStrategy(IGateAssignmentStrategy strategy)
        => m_Strategy = strategy ?? new RoundRobinStrategy();

    // ── Enemy Object Pools ────────────────────────────────────────────────────

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
                createFunc:      () =>
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

    // ── Gate Setup ────────────────────────────────────────────────────────────

    // Mark tất cả start/end cells trong gridDTO theo groups
    public void InitEnemyPath(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        foreach (var g in groups)
        {
            var sc = g.StartCell;
            var ec = g.EndCell;
            sc.x = Mathf.Clamp(sc.x, 0, gridDTO.width  - 1);
            sc.y = Mathf.Clamp(sc.y, 0, gridDTO.height - 1);
            ec.x = Mathf.Clamp(ec.x, 0, gridDTO.width  - 1);
            ec.y = Mathf.Clamp(ec.y, 0, gridDTO.height - 1);
            gridDTO.SetCell(sc.x, sc.y, new TDGridCellDTO(sc.x, sc.y, CellType.Start));
            gridDTO.SetCell(ec.x, ec.y, new TDGridCellDTO(ec.x, ec.y, CellType.End));
        }
    }

    // ── Path Groups ───────────────────────────────────────────────────────────

    // Universal pairing: groupCount = max(startCount, endCount)
    // start[i % startCount] → end[i % endCount]
    // Cross pairing khi 2s2e để tăng Y displacement (windiness)
    public List<TDPathGroup> BuildPathGroups(TDStageConfig stage, IGridDTO gridDTO)
    {
        // Fallback defaults khi stage null (e.g. stageId không tìm thấy)
        int               startCount = stage?.StartGateCount ?? 1;
        int               endCount   = stage?.EndGateCount   ?? 1;
        TDEnums.MapLayout layout     = stage?.Layout         ?? TDEnums.MapLayout.LeftToRight;

        var (startBorder, endBorder) = TDGatePlacer.GetBorders(layout);

        var startCells = TDGatePlacer.Place(startCount, startBorder, gridDTO.width, gridDTO.height);
        var endCells   = TDGatePlacer.Place(endCount,   endBorder,   gridDTO.width, gridDTO.height);

        // Cross pairing: đảo endCells khi 2s2e — tăng Y displacement
        if (startCount == 2 && endCount == 2)
            endCells.Reverse();

        int groupCount    = Mathf.Max(startCells.Count, endCells.Count);
        int pathsPerGroup = TDConstant.CONFIG_NUM_PATHS; // mỗi group luôn có đủ corridors

        var groups = new List<TDPathGroup>(groupCount);
        for (int i = 0; i < groupCount; i++)
        {
            var sc = startCells[i % startCells.Count];
            var ec = endCells  [i % endCells.Count];
            groups.Add(new TDPathGroup
            {
                StartCell     = sc,
                EndCell       = ec,
                StartBorder   = startBorder,
                SpawnWorldPos = TDGridMainModel.api.CellToWorld(sc),
                PathCount     = pathsPerGroup,
                Corridors     = new List<List<IGridCellDTO>>(),
            });
        }

        Debug.Log($"<color=cyan>[BuildPathGroups] {groupCount} groups, {pathsPerGroup} paths/group</color>");
        return groups;
    }

    // ── Path Generation ───────────────────────────────────────────────────────

    public void GenerateAllPaths(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        TDMazePathGenerator.api.GenerateForGroups(gridDTO, groups);

        int total = groups.Sum(g => g.Corridors.Count);
        Debug.Log($"<color=cyan>[GenerateAllPaths] {total} corridors across {groups.Count} groups</color>");

        onGroupsReady?.Invoke(groups);
    }

    // Valid tower cells = wall cells, excluding gate buffers và occupied cells
    public void ComputeValidTowerCells(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        const int gateBuffer = 2;
        var excluded = new HashSet<Vector2Int>();

        foreach (var group in groups)
        {
            for (int dx = -gateBuffer; dx <= gateBuffer; dx++)
                for (int dy = -gateBuffer; dy <= gateBuffer; dy++)
                {
                    var s = new Vector2Int(group.StartCell.x + dx, group.StartCell.y + dy);
                    var e = new Vector2Int(group.EndCell.x   + dx, group.EndCell.y   + dy);
                    if (s.x >= 0 && s.x < gridDTO.width && s.y >= 0 && s.y < gridDTO.height) excluded.Add(s);
                    if (e.x >= 0 && e.x < gridDTO.width && e.y >= 0 && e.y < gridDTO.height) excluded.Add(e);
                }
        }

        Vector3[,] grid          = TDGridMainModel.api.GetGrid();
        var        validPositions = new List<Vector3>();

        for (int x = 0; x < gridDTO.width; x++)
            for (int y = 0; y < gridDTO.height; y++)
            {
                if (gridDTO.GetCell(x, y).isWalkable)                      continue;
                if (excluded.Contains(new Vector2Int(x, y)))                continue;
                if (!TDGridMainModel.api.IsValidPlacement(grid[x, y]))      continue;
                validPositions.Add(grid[x, y]);
            }

        Debug.Log($"<color=cyan>[ComputeValidTowerCells] {validPositions.Count} wall cells = tower spots</color>");
        onValidTowerCellsReady?.Invoke(validPositions);
    }

    // ── Wave Loop ────────────────────────────────────────────────────────────

    public async void StartWaveLoop(List<TDPathGroup> groups,
        List<List<EnemyType>> wavePlans, LevelConfig config, CancellationToken ct)
    {
        if (groups == null || groups.Count == 0 || groups.All(g => g.Corridors.Count == 0))
        {
            Debug.LogError("<color=red>StartWaveLoop: no groups/corridors available</color>");
            return;
        }

        DifficultyRatioTable.RatioRow ratio = DifficultyRatioTable.Get(config.difficulty);
        Debug.Log($"<color=green>StartWaveLoop: {wavePlans.Count} waves, {groups.Count} groups</color>");

        m_Strategy ??= new RoundRobinStrategy();

        try
        {
            for (int waveIdx = 0; waveIdx < wavePlans.Count; waveIdx++)
            {
                ct.ThrowIfCancellationRequested();
                TDGameStateControl.api?.OnWaveStarted(waveIdx + 1, wavePlans.Count);
                TDGameEventBus.WaveStarted(waveIdx);

                var assignments = m_Strategy.SelectForWave(groups, waveIdx);

                // Chia đều enemies cho mỗi assignment trong wave
                var batch     = wavePlans[waveIdx];
                int perGroup  = Mathf.Max(1, batch.Count / assignments.Count);

                var spawnTasks = new List<Task>();

                for (int a = 0; a < assignments.Count; a++)
                {
                    var (group, corridor) = assignments[a];
                    int start = a * perGroup;
                    int end   = (a == assignments.Count - 1) ? batch.Count : start + perGroup;
                    var slice = batch.GetRange(start, end - start);

                    onWaveGroupStart?.Invoke(waveIdx, groups.IndexOf(group));
                    spawnTasks.Add(SpawnBatch(group.SpawnWorldPos, corridor, slice, ratio,
                        waveIdx, config.spawnInterval, ct));
                }

                await Task.WhenAll(spawnTasks);
                await PauseAwareDelay(config.waveInterval, ct);
            }

            Debug.Log($"<color=green>All {wavePlans.Count} waves completed!</color>");
            TDGameStateControl.api?.OnAllWavesSpawned();
        }
        catch (OperationCanceledException)
        {
            Debug.Log("<color=yellow>StartWaveLoop: cancelled</color>");
        }
    }

    // ── Wave Planning ─────────────────────────────────────────────────────────

    public List<List<EnemyType>> BuildWavePlans(LevelConfig config)
    {
        return BuildWavePlansInternal(config.difficulty, config.waveCount, config.totalEnemies);
    }

    public int GetActualEnemyCount(List<List<EnemyType>> wavePlans)
        => wavePlans.Sum(w => w.Count);

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task SpawnBatch(Vector3 spawnWorldPos, List<IGridCellDTO> path,
        List<EnemyType> batch, DifficultyRatioTable.RatioRow ratio,
        int waveIdx, float spawnInterval, CancellationToken ct)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            EnemyType type      = batch[i];
            EnemyData data      = m_FlyweightEnemyDataSettings?.GetData(type);
            float     hp        = (data?.baseHP          ?? 300f) * ratio.hpMult;
            float     speed     = (data?.baseSpeed       ?? 3f)   * ratio.speedMult;
            float     atkDamage = data?.baseAttackDamage ?? 10f;
            float     atkSpeed  = data?.baseAttackSpeed  ?? 1f;

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
            TDGameEventBus.EnemySpawned(spawnWorldPos, type);

            await PauseAwareDelay(spawnInterval, ct);
        }
    }

    private static List<List<EnemyType>> BuildWavePlansInternal(Difficulty difficulty, int waveCount, int totalEnemies)
    {
        if (difficulty >= Difficulty.Extreme)
        {
            waveCount    = Mathf.Max(waveCount,    15);
            totalEnemies = Mathf.Max(totalEnemies, 75);
        }

        var (bossWaveCount, bossPerWave, bossWaveMult) = GetBossParams(difficulty);
        bossWaveCount = Mathf.Min(bossWaveCount, waveCount);

        int   regularWaveCount = waveCount - bossWaveCount;
        float r                = totalEnemies / (regularWaveCount + bossWaveCount * bossWaveMult);
        int   regularSize      = Mathf.Max(1, Mathf.RoundToInt(r));
        int   bossWaveSize     = Mathf.Max(bossPerWave + 1, Mathf.RoundToInt(r * bossWaveMult));

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

            for (int i = wave.Count - 1; i > 0; i--)
            {
                int rIdx = Random.Range(0, i + 1);
                (wave[i], wave[rIdx]) = (wave[rIdx], wave[i]);
            }

            if (isBossWave)
                for (int i = 0; i < bossPerWave; i++)
                    wave.Add(EnemyType.Boss);

            plans.Add(wave);
        }

        Debug.Log($"<color=cyan>BuildWavePlans: {waveCount} waves | regular={regularSize} | bossWaves={bossWaveCount}</color>");
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

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

    public Action<List<TDPathGroup>> onGroupsReady;
    public Action<int, int> onWaveGroupStart; // (waveIdx, groupIdx)
    public Action<List<Vector3>> onValidTowerCellsReady;

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

            var capturedData = data;
            var capturedParent = parent;

            m_EnemyPools[type] = new ObjectPool<TDEnemyView>(
                createFunc: () =>
                {
                    var go = Object.Instantiate(capturedData.prefab, capturedParent);
                    var view = go.GetComponent<TDEnemyView>() ?? go.AddComponent<TDEnemyView>();
                    return view;
                },
                actionOnGet: e => e.gameObject.SetActive(true),
                actionOnRelease: e => e.gameObject.SetActive(false),
                actionOnDestroy: e => Object.Destroy(e.gameObject),
                collectionCheck: true,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
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

    // Marks all start/end cells in the gridDTO based on the provided groups
    public void InitEnemyPath(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        foreach (var g in groups)
        {
            var sc = g.StartCell;
            var ec = g.EndCell;
            sc.x = Mathf.Clamp(sc.x, 0, gridDTO.width - 1);
            sc.y = Mathf.Clamp(sc.y, 0, gridDTO.height - 1);
            ec.x = Mathf.Clamp(ec.x, 0, gridDTO.width - 1);
            ec.y = Mathf.Clamp(ec.y, 0, gridDTO.height - 1);
            gridDTO.SetCell(sc.x, sc.y, new TDGridCellDTO(sc.x, sc.y, CellType.Start));
            gridDTO.SetCell(ec.x, ec.y, new TDGridCellDTO(ec.x, ec.y, CellType.End));
        }
    }

    // ── Path Groups ───────────────────────────────────────────────────────────

    // Universal pairing: groupCount = max(startCount, endCount)
    // start[i % startCount] → end[i % endCount]
    // Cross pairing when 2 starts and 2 ends to increase Y displacement (windiness)
    public List<TDPathGroup> BuildPathGroups(TDStageConfig stage, IGridDTO gridDTO)
    {
        // Fallback defaults when stage is null (e.g. stageId not found)
        int startCount = stage?.StartGateCount ?? 1;
        int endCount = stage?.EndGateCount ?? 1;
        TDEnums.MapLayout layout = stage?.Layout ?? TDEnums.MapLayout.LeftToRight;

        var (startBorder, endBorder) = TDGatePlacer.GetBorders(layout);

        var startCells = TDGatePlacer.Place(startCount, startBorder, gridDTO.width, gridDTO.height);
        var endCells = TDGatePlacer.Place(endCount, endBorder, gridDTO.width, gridDTO.height);

        // Cross pairing: reverse endCells when 2 starts and 2 ends — increases Y displacement
        if (startCount == 2 && endCount == 2)
            endCells.Reverse();

        int groupCount = Mathf.Max(startCells.Count, endCells.Count);
        int pathsPerGroup = TDConstant.CONFIG_NUM_PATHS; // each group always has a full set of corridors

        var groups = new List<TDPathGroup>(groupCount);
        for (int i = 0; i < groupCount; i++)
        {
            var sc = startCells[i % startCells.Count];
            var ec = endCells [i % endCells.Count];
            groups.Add(new TDPathGroup
            {
                StartCell = sc,
                EndCell = ec,
                StartBorder = startBorder,
                SpawnWorldPos = TDGridMainModel.api.CellToWorld(sc),
                PathCount = pathsPerGroup,
                Corridors = new List<List<IGridCellDTO>>(),
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

    // Valid tower cells = wall cells, excluding gate buffers and occupied cells
    /// <summary>
    /// The subset of wall cells that are actually buildable. Everything else is scenery.
    /// The view reads this to decide which cells get a tower-zone tile.
    /// </summary>
    public List<Vector3> TowerSlots { get; private set; } = new List<Vector3>();

    /// <summary>
    /// Every wall cell, slots included. Only the view wants this, to scatter scenery over
    /// the cells that did not win a slot. Nothing that decides placement may read it.
    /// </summary>
    public List<Vector3> WallCells { get; private set; } = new List<Vector3>();

    /// <summary>
    /// How many buildable slots this stage gets.
    ///
    ///   chambers x PER_CHAMBER x frontFactor x modeFactor x diffFactor, clamped 8..20
    ///
    /// diffFactor is deliberately gentle. Difficulty already lives in DifficultyRatioTable
    /// (composition, hpMult, speedMult) — Kingdom Rush ships the same map at every
    /// difficulty. It is non-zero only because totalEnemies runs 30 → 75+, so gold income
    /// roughly quintuples and the player needs somewhere to spend it. Scaling slots with
    /// income outright would need ~47 at Nightmare, which throws the scarcity away again;
    /// hence the hard ceiling.
    /// </summary>
    public static int ComputeTowerSlotBudget(TDStageConfig stage, int chokepointCount, int frontCount)
    {
        int chambers = Mathf.Max(1, chokepointCount);
        int fronts = Mathf.Max(1, frontCount);

        float frontFactor = 1f + TDConstant.TOWER_SLOT_FRONT_BONUS * (fronts - 1);

        float modeFactor = stage != null && stage.GateMode == TDEnums.GateAssignmentMode.Simultaneous
            ? TDConstant.TOWER_SLOT_SIMULTANEOUS_BONUS
            : 1f;

        var level = stage != null ? TDLevelConfigSettings.api?.GetLevel(stage.LevelIndex) : null;
        float diffFactor = DifficultyslotFactor(level?.difficulty ?? TDEnums.Difficulty.Normal);

        int raw = Mathf.RoundToInt(chambers * TDConstant.TOWER_SLOTS_PER_CHAMBER
                                   * frontFactor * modeFactor * diffFactor);

        return Mathf.Clamp(raw, TDConstant.TOWER_SLOTS_MIN, TDConstant.TOWER_SLOTS_MAX);
    }

    private static float DifficultyslotFactor(TDEnums.Difficulty d) => d switch
    {
        TDEnums.Difficulty.Normal => 1.05f,
        TDEnums.Difficulty.Hard => 1.15f,
        TDEnums.Difficulty.Nightmare => 1.35f,
        _ => 1.00f,
    };

    public void ComputeValidTowerCells(IGridDTO gridDTO, List<TDPathGroup> groups, TDStageConfig stage = null)
    {
        var excluded = new HashSet<Vector2Int>();

        foreach (var group in groups)
        {
            for (int dx = -TDConstant.CONFIG_GATE_BUFFER; dx <= TDConstant.CONFIG_GATE_BUFFER; dx++)
                for (int dy = -TDConstant.CONFIG_GATE_BUFFER; dy <= TDConstant.CONFIG_GATE_BUFFER; dy++)
                {
                    var s = new Vector2Int(group.StartCell.x + dx, group.StartCell.y + dy);
                    var e = new Vector2Int(group.EndCell.x + dx, group.EndCell.y + dy);
                    if (s.x >= 0 && s.x < gridDTO.width && s.y >= 0 && s.y < gridDTO.height) excluded.Add(s);
                    if (e.x >= 0 && e.x < gridDTO.width && e.y >= 0 && e.y < gridDTO.height) excluded.Add(e);
                }
        }

        Vector3[,] grid = TDGridMainModel.api.GetGrid();
        var validPositions = new List<Vector3>();

        for (int x = 0; x < gridDTO.width; x++)
            for (int y = 0; y < gridDTO.height; y++)
            {
                if (gridDTO.GetCell(x, y).isWalkable) continue;
                if (excluded.Contains(new Vector2Int(x, y))) continue;
                if (!TDGridMainModel.api.IsValidPlacement(grid[x, y])) continue;
                validPositions.Add(grid[x, y]);
            }

        // Every wall cell used to become a buildable tower spot. That gave ~110 options
        // against ~8 affordable units, so no placement was a real decision. Ration them.
        int budget = ComputeTowerSlotBudget(stage, TDMazePathGenerator.api?.ChokepointCount ?? 3, groups.Count);
        TowerSlots = BuildTowerPlatforms(gridDTO, groups, validPositions, budget);

        Debug.Log($"<color=cyan>[TowerSlots] budget {budget} cells from {validPositions.Count} wall cells " +
                  $"(chambers {TDMazePathGenerator.api?.ChokepointCount ?? 0}, fronts {groups.Count}, " +
                  $"mode {stage?.GateMode}, level {stage?.LevelIndex})</color>");

        WallCells = validPositions;

        foreach (var pos in TowerSlots)
            TDGridMainModel.api.RegisterTowerZoneCell(TDGridMainModel.api.WorldToCell(pos));

        // The event means "these are the buildable cells" and TDDeployController lights up
        // exactly what it receives. Handing it every wall cell — as this did while the view
        // needed them for scenery — made the drag highlight show ~129 placeable cells while
        // only 12 were registered. Scenery now reads WallCells instead.
        onValidTowerCellsReady?.Invoke(TowerSlots);
    }

    private static readonly Vector2Int[] k_Dirs =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    /// <summary>
    /// Places buildable ground as PLATFORMS by walking the enemy route.
    ///
    /// The pipeline is grid → enemy path → tower zones, and the middle step is the input
    /// to the last. Wall cells only say where a platform could physically stand; the route
    /// says which of them are worth anything. So this iterates the ROUTE and searches
    /// outward, rather than iterating wall cells and scoring each by nearby road.
    ///
    /// Inverting it removed two rules that had been added as patches:
    ///   min spacing       — route samples are spaced by construction, so platforms are too
    ///   max path distance — a search starting AT the road cannot strand a platform in the
    ///                       scenery, so nothing needs rejecting after the fact
    /// </summary>
    private static List<Vector3> BuildTowerPlatforms(IGridDTO grid, List<TDPathGroup> groups,
                                                     List<Vector3> wallCells, int cellBudget)
    {
        int r = TDConstant.TOWER_SLOT_COVERAGE_RADIUS;

        // Where a platform may physically stand: wall cells the camera actually shows.
        var buildable = new HashSet<Vector2Int>();
        foreach (var pos in wallCells)
        {
            var c = TDGridMainModel.api.WorldToCell(pos);
            if (TDGatePlacer.IsOnScreen(c, grid.width, grid.height)) buildable.Add(c);
        }

        // The enemy route, in travel order.
        var route = new List<Vector2Int>();
        var onRoute = new HashSet<Vector2Int>();
        foreach (var g in groups)
            foreach (var corridor in g.Corridors)
                foreach (var cell in corridor)
                    if (onRoute.Add(cell.position)) route.Add(cell.position);

        var slots = new List<Vector3>();
        if (route.Count == 0 || buildable.Count == 0) return slots;

        int platformCount = Mathf.Max(1, Mathf.CeilToInt((float)cellBudget / TDConstant.TOWER_PLATFORM_MAX_CELLS));

        var used = new HashSet<Vector2Int>();
        var covered = new HashSet<Vector2Int>();

        for (int p = 0; p < platformCount && slots.Count < cellBudget; p++)
        {
            // Sample the route at even intervals so cover spreads over the whole journey
            // instead of piling up wherever the road happens to be densest.
            Vector2Int sample = route[p * route.Count / platformCount];

            // Best seed near this stretch: the one reaching the most route cells nobody
            // covers yet. Counting only NEW cells is what stops two platforms doubling up
            // on the same corner — no spacing rule required.
            Vector2Int seed = default;
            int bestGain = 0;

            foreach (var cand in buildable)
            {
                if (used.Contains(cand)) continue;
                if (Mathf.Max(Mathf.Abs(cand.x - sample.x), Mathf.Abs(cand.y - sample.y)) > r) continue;

                int gain = 0;
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var t = new Vector2Int(cand.x + dx, cand.y + dy);
                        if (onRoute.Contains(t) && !covered.Contains(t)) gain++;
                    }

                if (gain <= bestGain) continue;
                bestGain = gain;
                seed = cand;
            }

            if (bestGain == 0) continue; // this stretch is already covered — move along

            int size = Mathf.Min(UnityEngine.Random.Range(TDConstant.TOWER_PLATFORM_MIN_CELLS,
                                                          TDConstant.TOWER_PLATFORM_MAX_CELLS + 1),
                                 cellBudget - slots.Count);

            foreach (var cell in GrowPlatform(buildable, used, seed, size))
            {
                used.Add(cell);
                slots.Add(TDGridMainModel.api.CellToWorld(cell));

                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        var t = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (onRoute.Contains(t)) covered.Add(t);
                    }
            }
        }

        float pct = route.Count > 0 ? 100f * covered.Count / route.Count : 0f;
        Debug.Log($"<color=cyan>[TowerPlatforms] {slots.Count} cells over ~{platformCount} platforms, " +
                  $"covering {covered.Count}/{route.Count} route cells ({pct:F0}%)</color>");

        LogRealRangeCoverage(used, onRoute);
        return slots;
    }

    /// <summary>
    /// Step 1.6 — coverage measured with the REAL firing pattern, not the planning radius.
    ///
    /// `covered` above uses TOWER_SLOT_COVERAGE_RADIUS, which exists to pick seeds; it is a
    /// square and the actual `rangeOffsets` are not. Reporting that number as "coverage"
    /// would be measuring the tool instead of the result — the same mistake that produced
    /// a 12-second runway constant derived from a countdown it had nothing to do with.
    ///
    /// Measures the ceiling: the player owns fewer units than there are platforms, so this
    /// is what perfect placement and perfect facing would reach, never what a real match
    /// achieves. A low ceiling means the platforms are in the wrong places.
    /// </summary>
    private static void LogRealRangeCoverage(HashSet<Vector2Int> slots, HashSet<Vector2Int> onRoute)
    {
        var roster = TDFlyweightOperatorDataSettings.api?.GetAllOperators();
        if (roster == null || onRoute.Count == 0) return;

        OperatorData narrowest = null;
        foreach (var op in roster)
        {
            if (op.deployZone != TDEnums.DeployZone.TowerZone) continue;
            int n = op.rangeOffsets?.Length ?? 0;
            if (n == 0) continue;
            if (narrowest == null || n < narrowest.rangeOffsets.Length) narrowest = op;
        }
        if (narrowest == null) return;

        var range = new TDOffsetRangeDTO(narrowest.rangeOffsets);
        var reached = new HashSet<Vector2Int>();

        foreach (var slot in slots)
        {
            // Best of the four facings — the player rotates on deploy, so anything less
            // would under-report by however unlucky the default rotation happens to be.
            List<Vector2Int> best = null;
            for (int a = 0; a < 360; a += 90)
            {
                var cells = range.GetCellsInRange(slot, Quaternion.Euler(0, a, 0));
                int hits = 0;
                foreach (var c in cells) if (onRoute.Contains(c)) hits++;

                if (best != null && hits <= CountOnRoute(best, onRoute)) continue;
                best = cells;
            }

            if (best == null) continue;
            foreach (var c in best) if (onRoute.Contains(c)) reached.Add(c);
        }

        Debug.Log($"<color=cyan>[TowerPlatforms] real range ({narrowest.operatorName}, " +
                  $"{narrowest.rangeOffsets.Length} cells): best-case cover " +
                  $"{reached.Count}/{onRoute.Count} route cells " +
                  $"({100f * reached.Count / onRoute.Count:F0}%)</color>");
    }

    private static int CountOnRoute(List<Vector2Int> cells, HashSet<Vector2Int> onRoute)
    {
        int n = 0;
        foreach (var c in cells) if (onRoute.Contains(c)) n++;
        return n;
    }

    /// <summary>Flood-fills a contiguous block of up to `size` buildable cells out from `seed`.</summary>
    private static List<Vector2Int> GrowPlatform(HashSet<Vector2Int> buildable, HashSet<Vector2Int> used,
                                                 Vector2Int seed, int size)
    {
        var block = new List<Vector2Int> { seed };
        var claimed = new HashSet<Vector2Int> { seed };
        var frontier = new Queue<Vector2Int>();
        frontier.Enqueue(seed);

        while (frontier.Count > 0 && block.Count < size)
        {
            var cur = frontier.Dequeue();

            for (int d = 0; d < 4 && block.Count < size; d++)
            {
                var nb = cur + k_Dirs[d];
                if (!buildable.Contains(nb) || used.Contains(nb) || !claimed.Add(nb)) continue;

                block.Add(nb);
                frontier.Enqueue(nb);
            }
        }

        return block;
    }

    // ── Wave Loop ────────────────────────────────────────────────────────────

    public async Task StartWaveLoop(List<TDPathGroup> groups,
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
            // Prep time before wave 1. The delay below sits at the END of the loop, so
            // until now the first wave spawned the instant the map finished generating.
            //
            // Normal hid it: 30 gold buys one Striker and one Striker holds. Nightmare made
            // it fatal — a second body costs 9 seconds of passive income, and at 2x enemy HP
            // the line is already gone by then. That is not a difficulty spike, it is the
            // player being asked to answer before the question is readable.
            //
            // Reuses waveInterval rather than adding a constant: "the pause between waves"
            // is exactly what this is, and wave 1 has as much right to one as wave 2.
            await PauseAwareDelay(config.waveInterval, ct);

            for (int waveIdx = 0; waveIdx < wavePlans.Count; waveIdx++)
            {
                ct.ThrowIfCancellationRequested();
                TDGameStateControl.api?.OnWaveStarted(waveIdx + 1, wavePlans.Count);
                TDGameEventBus.WaveStarted(waveIdx);

                var assignments = m_Strategy.SelectForWave(groups, waveIdx);

                // Distribute enemies evenly across each assignment in the wave
                var batch = wavePlans[waveIdx];
                int perGroup = Mathf.Max(1, batch.Count / assignments.Count);

                var spawnTasks = new List<Task>();
                float interval = SpawnIntervalFor(config.spawnInterval, waveIdx, wavePlans.Count, config.waveGrowth);

                for (int a = 0; a < assignments.Count; a++)
                {
                    var (group, corridor) = assignments[a];
                    int start = a * perGroup;
                    int end = (a == assignments.Count - 1) ? batch.Count : start + perGroup;
                    var slice = batch.GetRange(start, end - start);

                    onWaveGroupStart?.Invoke(waveIdx, groups.IndexOf(group));
                    spawnTasks.Add(SpawnBatch(group.SpawnWorldPos, corridor, slice, ratio,
                        waveIdx, interval, ct));
                }

                await Task.WhenAll(spawnTasks);
                await PauseAwareDelay(config.waveInterval, ct);

                // §05 wave relief. Raised here rather than through a new bus event: the loop
                // already owns "a wave is over", and the registry already owns "who is still
                // standing" — a third party to carry the message between them would be one
                // more place for the two to disagree.
                TDOperatorRegistry.api?.OnWaveCleared();
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
        var plans = BuildWavePlans(config.difficulty, config.waveCount, config.totalEnemies, config.waveGrowth);
        int actual = plans.Sum(w => w.Count);
        if (actual != config.totalEnemies)
            Debug.LogWarning($"[BuildWavePlans] level {config.levelIndex} asks for {config.totalEnemies} enemies; " +
                             $"{config.waveCount} waves and their bosses need at least {actual}");
        var intervals = plans.Select((_, i) => SpawnIntervalFor(config.spawnInterval, i, plans.Count, config.waveGrowth).ToString("F2"));
        Debug.Log($"<color=cyan>BuildWavePlans: {plans.Count} waves, {actual} enemies, sizes [{string.Join(", ", plans.Select(w => w.Count))}], " +
                  $"intervals [{string.Join(", ", intervals)}]</color>");
        return plans;
    }

    public int GetActualEnemyCount(List<List<EnemyType>> wavePlans)
        => wavePlans.Sum(w => w.Count);

    // Spec §5.7: wave i of n weighs 1 + (g − 1)·i/(n − 1) — g is the last wave's weight over the first's.
    public static float GrowthWeight(int waveIdx, int waveCount, float waveGrowth)
        => waveCount <= 1 ? 1f : 1f + (waveGrowth - 1f) * waveIdx / (waveCount - 1);

    // D14: a heavier wave also spawns denser, so the load per second rises with it — a bigger
    // wave at the same spacing only lasts longer, and the ρ table stayed flat after the cap.
    // Never slower than the level's own spawnInterval; never under the floor unless the level
    // already asked for that. The wave loop and TDLoadModel both read this one function.
    public static float SpawnIntervalFor(float spawnInterval, int waveIdx, int waveCount, float waveGrowth)
    {
        float w = GrowthWeight(waveIdx, waveCount, waveGrowth);
        if (w <= 0f) return spawnInterval;
        return Mathf.Min(spawnInterval, Mathf.Max(TDConstant.CONFIG_SPAWN_INTERVAL_FLOOR, spawnInterval / w));
    }

    private static readonly EnemyType[] k_MixTypes = { EnemyType.Normal, EnemyType.Fast, EnemyType.Tank };

    // Spec §5.7. Wave i weighs GrowthWeight, boss waves × bossWaveMult on top, and the level's
    // total is apportioned by weight — growth reshapes a match without lengthening it. The mix
    // follows the match's progress (D14): light enemies open, heavy ones arrive later.
    // Public and static so TDBalanceValidator checks this exact code, not a copy of it.
    public static List<List<EnemyType>> BuildWavePlans(Difficulty difficulty, int waveCount, int totalEnemies, float waveGrowth)
    {
        var row = DifficultyRatioTable.Get(difficulty);
        waveCount = Mathf.Max(1, waveCount);
        int bossWaveCount = Mathf.Min(row.bossWaveCount, waveCount);

        var isBoss = new bool[waveCount];
        for (int k = 1; k <= bossWaveCount; k++)
            isBoss[Mathf.Clamp(Mathf.CeilToInt((float)waveCount * k / bossWaveCount) - 1, 0, waveCount - 1)] = true;

        var weights = new float[waveCount];
        var min = new int[waveCount];
        int floor = 0;
        for (int i = 0; i < waveCount; i++)
        {
            weights[i] = GrowthWeight(i, waveCount, waveGrowth);
            if (isBoss[i]) weights[i] *= row.bossWaveMult;
            min[i] = isBoss[i] ? row.bossPerWave + 1 : 1; // a boss never walks in alone
            floor += min[i];
        }

        // A level asking for fewer enemies than its waves can hold gets the minimum, not a
        // missing boss: the boss plan is the difficulty's promise.
        int[] sizes = DifficultyRatioTable.Apportion(weights, Mathf.Max(totalEnemies, floor));

        // Rounding can starve a light early wave; feed it from the biggest wave that can spare
        // one. A donor always exists because the total is at least the sum of the minimums.
        for (int i = 0; i < waveCount; i++)
            while (sizes[i] < min[i])
            {
                int donor = -1;
                for (int j = 0; j < waveCount; j++)
                    if (sizes[j] > min[j] && (donor < 0 || sizes[j] > sizes[donor])) donor = j;
                sizes[donor]--;
                sizes[i]++;
            }

        var plans = new List<List<EnemyType>>(waveCount);
        for (int w = 0; w < waveCount; w++)
        {
            int bosses = isBoss[w] ? row.bossPerWave : 0;
            float progress = waveCount == 1 ? 0f : (float)w / (waveCount - 1);
            int[] mix = DifficultyRatioTable.Distribute(difficulty, sizes[w] - bosses, progress);

            // Horde and Herald (mix[3], mix[4]) have zero share until those enemy types exist.
            var wave = new List<EnemyType>(sizes[w]);
            for (int t = 0; t < k_MixTypes.Length; t++)
                for (int i = 0; i < mix[t]; i++) wave.Add(k_MixTypes[t]);

            for (int i = wave.Count - 1; i > 0; i--)
            {
                int rIdx = Random.Range(0, i + 1);
                (wave[i], wave[rIdx]) = (wave[rIdx], wave[i]);
            }

            for (int i = 0; i < bosses; i++) wave.Add(EnemyType.Boss);
            plans.Add(wave);
        }
        return plans;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task SpawnBatch(Vector3 spawnWorldPos, List<IGridCellDTO> path,
        List<EnemyType> batch, DifficultyRatioTable.RatioRow ratio,
        int waveIdx, float spawnInterval, CancellationToken ct)
    {
        for (int i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            EnemyType type = batch[i];
            EnemyData data = m_FlyweightEnemyDataSettings?.GetData(type);
            float hp = (data?.baseHP ?? 300f) * ratio.hpMult;
            float speed = (data?.baseSpeed ?? 3f) * ratio.speedMult;
            float atkDamage = data?.baseAttackDamage ?? 10f;
            float atkSpeed = data?.baseAttackSpeed ?? 1f;

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

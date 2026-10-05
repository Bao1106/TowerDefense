using System.Collections.Generic;
using UnityEngine;

// Spine-first maze generator — convergent topology.
//
// GenerateForGroups():
//   1. BuildSpine     — resolve the archetype for this grid, carve chokepoints and the
//                       passages between them, cache the cells.
//   2. per corridor   — SetAllWalls → ReopenSpine → seed `visited` with the spine's even
//                       cells and push them as the carver's frontier → CarveFrom grows
//                       branches AROUND the skeleton → A* extracts the route, which now
//                       has a real choice to make and takes the spine because it is direct.
//   3. final pass     — SetAllWalls, then restore the A* path cells AND the spine.
//                       Restoring only the paths would collapse a 4-wide chokepoint to the
//                       1-cell line A* walked through it.
//
// Why convergent instead of the previous N independent start→end mazes: parallel corridors
// make attrition unplayable. Lose the one operator holding corridor B and B is open to the
// goal with no way to compensate. Convergence gives the player somewhere to fall back to.
//
// CarveFrom: iterative Stack instead of recursion → no StackOverflow on large grids, and
// multi-source so the whole spine acts as the starting frontier.
public class TDMazePathGenerator
{
    public static TDMazePathGenerator api;

    private readonly IPathFinder m_PathFinder;

    // The spine is deterministic given (archetype, gates), so it is built once and then
    // re-applied after every SetAllWalls instead of being recomputed per attempt.
    private HashSet<Vector2Int> m_SpineCells;
    private List<Vector2Int> m_SpineSeeds; // even cells only — the maze's starting frontier
    private List<Vector2Int> m_NodeCenters; // chokepoint centres, for the C2 runway check

    /// <summary>
    /// Cells the spine opened. These are walkable but belong to no corridor, so the view
    /// has to be told about them separately — otherwise a chokepoint renders as bare
    /// ground and, worse, never registers as an operator cell.
    /// </summary>
    public HashSet<Vector2Int> SpineCells => m_SpineCells;

    /// <summary>Chokepoints the resolved archetype actually placed — drives the tower slot budget.</summary>
    public int ChokepointCount { get; private set; }

    public TDMazePathGenerator(IPathFinder pathFinder)
    {
        m_PathFinder = pathFinder;
    }

    // Entry point: generate, then check the result against the acceptance rules and
    // regenerate if it fails. Only rules a fresh roll can actually fix are retried —
    // a map that is simply too small is reported instead, since rerolling it forever
    // would just hide the real problem.
    public void GenerateForGroups(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        for (int attempt = 1; attempt <= TDConstant.MAZE_MAX_ATTEMPTS; attempt++)
        {
            GenerateOnce(gridDTO, groups);

            if (Accept(gridDTO, groups, out string reason))
            {
                Debug.Log($"<color=cyan>[MazeGen] accepted on attempt {attempt} — " +
                          $"widest front {WidestFront(groups, RouteCells(groups))} lanes</color>");

                WarnOnGoalRunway(groups);
                return;
            }

            Debug.LogWarning($"<color=orange>[MazeGen] attempt {attempt} rejected — {reason}</color>");
        }

        Debug.LogError($"<color=orange>[MazeGen] no layout passed acceptance in " +
                       $"{TDConstant.MAZE_MAX_ATTEMPTS} attempts — shipping the last one. " +
                       $"The map shape itself is likely the constraint, not the roll.</color>");
        WarnOnGoalRunway(groups);
    }

    private void GenerateOnce(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        var combinedPathCells = new HashSet<Vector2Int>();

        BuildSpine(gridDTO, groups);

        foreach (var group in groups)
        {
            group.Corridors = new List<List<IGridCellDTO>>();

            for (int i = 0; i < group.PathCount; i++)
            {
                var path = Carve(gridDTO, group, groups);
                if (path == null || path.Count == 0)
                {
                    Debug.LogWarning($"<color=orange>[MazeGen] Group {groups.IndexOf(group)} path {i + 1} failed</color>");
                    continue;
                }

                // With a spine every corridor of a group runs start → spine → end, so the
                // second and third attempts return the same route. Stop instead of storing
                // duplicates: they would be drawn on top of each other and give enemies no
                // real choice. If a later archetype does produce distinct routes this loop
                // keeps working unchanged.
                if (IsDuplicate(group.Corridors, path))
                {
                    Debug.Log($"<color=cyan>[MazeGen] Group {groups.IndexOf(group)}: corridor {i + 1} duplicates an earlier one — converged, stopping at {group.Corridors.Count}</color>");
                    break;
                }

                group.Corridors.Add(path);
                foreach (var cell in path)
                    combinedPathCells.Add(cell.position);

                Debug.Log($"<color=cyan>[MazeGen] Group {groups.IndexOf(group)} path {i + 1}/{group.PathCount}: {path.Count} cells</color>");
            }
        }

        // A group with no corridor kills the whole run — the wave loop aborts with
        // "no groups/corridors available", which says nothing about the cause. Name the
        // endpoints here so the next failure of this kind is one log line to diagnose.
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].Corridors.Count > 0) continue;

            var g = groups[i];
            bool startOpen = gridDTO.GetCell(g.StartCell.x, g.StartCell.y).isWalkable;
            bool endOpen = gridDTO.GetCell(g.EndCell.x, g.EndCell.y).isWalkable;

            Debug.LogError($"<color=orange>[MazeGen] Group {i} produced NO corridor: " +
                           $"{g.StartCell}(walkable={startOpen}) → {g.EndCell}(walkable={endOpen}). " +
                           $"Shared endpoints across groups are the usual cause.</color>");
        }

        // Final pass: restore all path cells into the grid as walkable
        SetAllWalls(gridDTO);
        foreach (var pos in combinedPathCells)
            gridDTO.GetCell(pos.x, pos.y).isWalkable = true;

        // The spine must survive this pass too. A* returns a 1-cell-wide route, so
        // restoring only its cells would collapse a 4-wide chokepoint down to a single
        // line — deleting the very thing the node exists to provide: room to stand.
        ReopenSpine(gridDTO);

        // Safety: start/end cells are always kept walkable
        foreach (var group in groups)
        {
            gridDTO.GetCell(group.StartCell.x, group.StartCell.y).isWalkable = true;
            gridDTO.GetCell(group.EndCell.x, group.EndCell.y).isWalkable = true;
        }

        LogRoadRatio(gridDTO, combinedPathCells.Count);
    }

    // ── Acceptance (step 1.5) ────────────────────────────────────────────────

    /// <summary>
    /// Rules a regeneration can fix. Runs on the finished grid, after the final pass.
    /// </summary>
    private bool Accept(IGridDTO grid, List<TDPathGroup> groups, out string reason)
    {
        // C4 — every gate pair actually connected. A group whose route was salvaged from
        // the fallback branch can end up with a corridor that no longer holds on the final
        // grid, and enemies would simply never arrive.
        foreach (var g in groups)
        {
            if (Reachable(grid, g.StartCell, g.EndCell)) continue;
            reason = $"C4: {g.StartCell} → {g.EndCell} not connected";
            return false;
        }

        // C5 — every route cell in ONE walkable region. A stranded pocket is somewhere an
        // operator can be deployed and then contribute nothing for the rest of the match.
        var route = RouteCells(groups);
        if (route.Count > 0)
        {
            var reach = FloodFrom(grid, groups[0].StartCell);
            foreach (var cell in route)
            {
                if (reach.Contains(cell)) continue;
                reason = $"C5: route cell {cell} is cut off from the start gate";
                return false;
            }
        }

        // C1 — the widest simultaneous front. This is the one Chốt A rests on: if the map
        // demands more defenders at once than the player can field, there is nowhere to
        // give ground, and "trade space for lives" stops being possible at all.
        int front = WidestFront(groups, route);
        int cap = Mathf.Max(1, Mathf.FloorToInt(TDConstant.CONFIG_MAX_SLOTS *
                                                TDConstant.ACCEPT_MAX_FRONT_ROSTER_RATIO));
        if (front > cap)
        {
            reason = $"C1: widest front {front} lanes > {cap} ({TDConstant.ACCEPT_MAX_FRONT_ROSTER_RATIO:P0} of {TDConstant.CONFIG_MAX_SLOTS} slots)";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// C2 — runway from the last chokepoint to the goal. Reported, never retried: it is a
    /// property of how big the map is, so rerolling cannot help and looping on it would
    /// only bury the finding.
    /// </summary>
    private void WarnOnGoalRunway(List<TDPathGroup> groups)
    {
        if (m_NodeCenters == null || m_NodeCenters.Count == 0 || groups.Count == 0) return;

        var last = m_NodeCenters[m_NodeCenters.Count - 1];

        int cells = int.MaxValue;
        foreach (var g in groups)
            cells = Mathf.Min(cells, Mathf.Abs(last.x - g.EndCell.x) + Mathf.Abs(last.y - g.EndCell.y));

        float speed = TDFlyweightEnemyDataSettings.api?.GetData(TDEnums.EnemyType.Normal)?.baseSpeed ?? 3f;
        float seconds = cells * TDConstant.CONFIG_GRID_CELL_SIZE / Mathf.Max(0.01f, speed);

        if (seconds >= TDConstant.ACCEPT_MIN_GOAL_RUNWAY_SECONDS)
        {
            Debug.Log($"<color=cyan>[MazeGen] goal runway {seconds:F1}s ({cells} cells) — ok</color>");
            return;
        }

        Debug.LogWarning($"<color=orange>[MazeGen] C2: goal runway only {seconds:F1}s ({cells} cells at " +
                         $"speed {speed}) — needs {TDConstant.ACCEPT_MIN_GOAL_RUNWAY_SECONDS}s. Pulling a " +
                         $"defender off the last chokepoint leaves no time to replace them, so the rotation " +
                         $"the morale system runs on is not possible on a map this size.</color>");
    }

    /// <summary>
    /// C6 — a chokepoint must be wider than the number of melee operators the player can
    /// buy before the first wave lands. Reported, never retried: the width comes from the
    /// archetype and the affordability from the economy, so no reroll can change either.
    ///
    /// Why this is worth a check at all: melee strike ONE target each and enemies do not
    /// queue — a blocked cell just gets walked past. So a line of bodies across the full
    /// width of a chokepoint stops the flow completely, and stress source N1 (which
    /// measures enemies leaking through) can never fire. Measured on the Hard run: the
    /// line was processing 0.36 enemies/second against 0.40 arriving — one extra Striker
    /// would have flipped the ratio to 0.74 and removed the pressure permanently.
    ///
    /// So the chokepoint is not a difficulty knob, it is the thing that keeps the player's
    /// answer from being "buy one more body".
    /// </summary>
    private static void WarnOnSealableChokepoint(int narrowestWidth)
    {
        var roster = TDFlyweightOperatorDataSettings.api?.GetAllOperators();
        if (roster == null || roster.Count == 0 || narrowestWidth == int.MaxValue) return;

        int cheapest = int.MaxValue;
        string who = null;
        foreach (var op in roster)
        {
            if (op.deployZone != TDEnums.DeployZone.PathCell) continue;
            if (op.cost <= 0 || op.cost >= cheapest) continue;
            cheapest = op.cost;
            who = op.operatorName;
        }
        if (cheapest == int.MaxValue) return;

        int affordable = TDConstant.CONFIG_PLAYER_STARTING_GOLD / cheapest;

        if (narrowestWidth > affordable)
        {
            Debug.Log($"<color=cyan>[MazeGen] C6 chokepoint width {narrowestWidth} > {affordable} " +
                      $"melee affordable at wave 1 ({TDConstant.CONFIG_PLAYER_STARTING_GOLD} gold / " +
                      $"{who} at {cheapest}) — ok</color>");
            return;
        }

        Debug.LogWarning($"<color=orange>[MazeGen] C6: narrowest chokepoint is {narrowestWidth} cells and " +
                         $"the player can afford {affordable} melee at wave 1 " +
                         $"({TDConstant.CONFIG_PLAYER_STARTING_GOLD} gold / {who} at {cheapest}). " +
                         $"A wall of bodies seals it outright, so nothing ever leaks past and N1 " +
                         $"cannot fire. Widen the archetype's chokepoints or raise melee cost.</color>");
    }

    private static List<Vector2Int> RouteCells(List<TDPathGroup> groups)
    {
        var route = new List<Vector2Int>();
        var seen = new HashSet<Vector2Int>();

        foreach (var g in groups)
            foreach (var corridor in g.Corridors)
                foreach (var cell in corridor)
                    if (seen.Add(cell.position)) route.Add(cell.position);

        return route;
    }

    /// <summary>
    /// How many separate LANES cross the same section of the flow — the number of places
    /// the player has to hold at once at the map's worst point.
    ///
    /// Counts contiguous runs, not cells. Counting cells was wrong and wrong every time:
    /// the vertical leg of an L-shaped corridor lies entirely inside one cross-section, so
    /// a single lane doubling back read as five parallel fronts. The number came out 5 on
    /// all ten attempts — identical results are the tell that the metric, not the roll, is
    /// the problem.
    /// </summary>
    private static int WidestFront(List<TDPathGroup> groups, List<Vector2Int> route)
    {
        if (route.Count == 0 || groups.Count == 0) return 0;

        Vector2 start = Vector2.zero, end = Vector2.zero;
        foreach (var g in groups) { start += g.StartCell; end += g.EndCell; }
        start /= groups.Count;
        end /= groups.Count;

        Vector2 axis = end - start;
        if (axis.sqrMagnitude < 0.01f) axis = Vector2.right;

        Vector2 dir = axis.normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        var slices = new Dictionary<int, List<int>>();

        foreach (var cell in route)
        {
            Vector2 rel = (Vector2)cell - start;
            int slice = Mathf.RoundToInt(Vector2.Dot(rel, dir));
            int lane = Mathf.RoundToInt(Vector2.Dot(rel, perp));

            if (!slices.TryGetValue(slice, out var lanes))
                slices[slice] = lanes = new List<int>();

            lanes.Add(lane);
        }

        int widest = 0;

        foreach (var kv in slices)
        {
            var lanes = kv.Value;
            lanes.Sort();

            // Adjacent lane indices belong to the same run — one corridor, however thick.
            // A gap of more than one cell means a genuinely separate approach.
            int runs = 1;
            for (int i = 1; i < lanes.Count; i++)
                if (lanes[i] - lanes[i - 1] > 1) runs++;

            if (runs > widest) widest = runs;
        }

        return widest;
    }

    private static bool Reachable(IGridDTO grid, Vector2Int from, Vector2Int to)
        => FloodFrom(grid, from).Contains(to);

    private static HashSet<Vector2Int> FloodFrom(IGridDTO grid, Vector2Int origin)
    {
        var seen = new HashSet<Vector2Int> { origin };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(origin);

        int[] dx = { 0, 1, 0, -1 };
        int[] dy = { 1, 0, -1, 0 };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();

            for (int d = 0; d < 4; d++)
            {
                var nb = new Vector2Int(cur.x + dx[d], cur.y + dy[d]);
                if (nb.x < 0 || nb.x >= grid.width || nb.y < 0 || nb.y >= grid.height) continue;
                if (!grid.GetCell(nb.x, nb.y).isWalkable) continue;
                if (!seen.Add(nb)) continue;

                queue.Enqueue(nb);
            }
        }

        return seen;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // Reset → re-apply spine → carve branches → A* extract. Retries up to
    // TDConstant.MAZE_MAX_ATTEMPTS times, falling back to the longest path found.
    // allGroups: blocks the gate cells of other groups before A* runs
    // → prevents this group's path from routing through another group's gate.
    //
    // ACCEPTANCE CRITERION — changed with the spine. The old test was
    // `path.Count >= gridDTO.width`, i.e. "the route must be long". A spine route is
    // deliberately DIRECT, so that test rejected exactly the result we want, burned all
    // ten attempts and fell through to the longest of ten random failures — quietly, with
    // only an orange warning to show for it.
    //
    // Interim rule: any complete route is accepted, because the spine already guarantees
    // the structure. Step 1.5 replaces this with the real check — "the route passes
    // through every chokepoint of the archetype" — which tests what we actually care
    // about instead of using length as a proxy for it.
    private List<IGridCellDTO> Carve(IGridDTO gridDTO, TDPathGroup group, List<TDPathGroup> allGroups)
    {
        List<IGridCellDTO> best = null;

        for (int attempt = 0; attempt < TDConstant.MAZE_MAX_ATTEMPTS; attempt++)
        {
            SetAllWalls(gridDTO);
            ReopenSpine(gridDTO);

            // Seed the maze with the spine: those cells are marked visited so the carver
            // cannot tunnel THROUGH the skeleton, and pushed as seeds so it grows OUT of
            // it. Both halves are needed — marking alone leaves the spine as a detached
            // slab sitting in the middle of an unrelated maze.
            var visited = new bool[gridDTO.width, gridDTO.height];
            for (int i = 0; i < m_SpineSeeds.Count; i++)
                visited[m_SpineSeeds[i].x, m_SpineSeeds[i].y] = true;

            CarveFrom(gridDTO, visited, m_SpineSeeds);
            gridDTO.GetCell(group.EndCell.x, group.EndCell.y).isWalkable = true;

            // Block gate cells of other groups — A* must not route through them.
            //
            // Compared by VALUE, not by reference. groupCount is max(startGates, endGates),
            // so whichever side has fewer gates gets shared between groups: with 2 starts
            // and 1 end, every group owns the same EndCell. The old reference check let a
            // group wall off its own destination, A* returned nothing, and the run died
            // with "no groups/corridors available".
            //
            // The 1→2 case had the same flaw but survived it: there the shared cell was the
            // START, and A* expands outward from the start without testing its walkability.
            foreach (var other in allGroups)
            {
                if (other == group) continue;

                if (other.StartCell != group.StartCell && other.StartCell != group.EndCell)
                    gridDTO.GetCell(other.StartCell.x, other.StartCell.y).isWalkable = false;

                if (other.EndCell != group.EndCell && other.EndCell != group.StartCell)
                    gridDTO.GetCell(other.EndCell.x, other.EndCell.y).isWalkable = false;
            }

            var startCell = gridDTO.GetCell(group.StartCell.x, group.StartCell.y);
            var endCell = gridDTO.GetCell(group.EndCell.x, group.EndCell.y);
            var path = m_PathFinder.ComputePath(gridDTO, startCell, endCell);

            if (path != null && path.Count > 0)
            {
                Debug.Log($"<color=cyan>[MazeGen] path ok attempt {attempt + 1}: {path.Count} cells</color>");
                return path;
            }

            if (best == null || (path != null && path.Count > best.Count))
                best = path;
        }

        Debug.LogWarning($"<color=orange>[MazeGen] fallback after {TDConstant.MAZE_MAX_ATTEMPTS} attempts: {best?.Count ?? 0} cells</color>");
        return best;
    }

    // Builds the skeleton once for the whole map: resolve the archetype for this grid
    // size, carve it, and cache the cells so every later attempt can re-apply it cheaply.
    private void BuildSpine(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        var startGates = new List<Vector2Int>(groups.Count);
        var endGates = new List<Vector2Int>(groups.Count);

        foreach (var g in groups)
        {
            if (!startGates.Contains(g.StartCell)) startGates.Add(g.StartCell);
            if (!endGates.Contains(g.EndCell)) endGates.Add(g.EndCell);
        }

        float axisLength = TDSpineCarver.FlowAxisLength(startGates, endGates);
        float lateralHalf = TDSpineCarver.LateralHalfExtent(gridDTO, startGates, endGates);

        var arch = TDSpineLibrary.Resolve(TDConstant.CONFIG_SPINE_ARCHETYPE,
                                          gridDTO.width, gridDTO.height,
                                          startGates.Count, endGates.Count,
                                          axisLength, lateralHalf);

        // Even the terminal fallback can be too big for a very short axis. Say so loudly:
        // the symptom is chokepoints merging into one slab, which reads like a carving
        // bug when it is really a map of the wrong shape.
        float spacing = arch.TierSpacing(axisLength);
        if (spacing < arch.RequiredTierSpacing)
        {
            Debug.LogError($"<color=orange>[MazeGen] '{arch.name}' tiers land {spacing:F1} cells apart " +
                           $"but need {arch.RequiredTierSpacing:F1} (node halves + gap 2) — its " +
                           $"chokepoints WILL overlap into one block. Its {arch.chokepoints.Length} nodes " +
                           $"span {arch.SpanU:P0} of a {axisLength:F1}-cell axis. Spread the nodes' u " +
                           $"values, drop a tier, or lengthen the map along the gate→gate axis.</color>");
        }

        ChokepointCount = arch.chokepoints.Length;

        int narrowest = int.MaxValue;
        for (int i = 0; i < arch.chokepoints.Length; i++)
            narrowest = Mathf.Min(narrowest, arch.chokepoints[i].width);
        WarnOnSealableChokepoint(narrowest);

        SetAllWalls(gridDTO);
        var spine = TDSpineCarver.Carve(gridDTO, arch, startGates, endGates);

        m_SpineCells = spine.cells;
        m_NodeCenters = spine.nodeCenters;
        m_SpineSeeds = new List<Vector2Int>();

        // Only even cells are maze rooms — odd spine cells are walkable but the carver
        // never targets them, so they make no sense as seeds.
        foreach (var c in m_SpineCells)
        {
            if (c.x % 2 == 0 && c.y % 2 == 0) m_SpineSeeds.Add(c);
        }

        // An empty seed list would leave CarveFrom with an empty stack and produce no
        // maze at all — silently, since nothing throws. Fall back to the gates, which
        // TDGatePlacer guarantees are on even coordinates.
        if (m_SpineSeeds.Count == 0)
        {
            Debug.LogError("<color=orange>[MazeGen] spine produced no even seed cells — falling back to gates</color>");
            m_SpineSeeds.AddRange(startGates);
        }

        if (arch.name != TDConstant.CONFIG_SPINE_ARCHETYPE)
        {
            Debug.LogWarning($"<color=orange>[MazeGen] '{TDConstant.CONFIG_SPINE_ARCHETYPE}' does not fit " +
                             $"grid {gridDTO.width}x{gridDTO.height} with {startGates.Count} start / " +
                             $"{endGates.Count} end gates — fell back to '{arch.name}'</color>");
        }

        Debug.Log($"<color=cyan>[MazeGen] spine '{arch.name}' ({arch.chokepoints.Length} chokepoints) " +
                  $"on grid {gridDTO.width}x{gridDTO.height}, gates {startGates.Count}→{endGates.Count}, " +
                  $"axis {axisLength:F1}, tier spacing {spacing:F1}/{arch.RequiredTierSpacing:F1}, " +
                  $"lateral {lateralHalf:F1}/{arch.minLateralHalf:F1}: " +
                  $"{m_SpineCells.Count} cells, {m_SpineSeeds.Count} seeds</color>");
    }

    // Counts the grid as it actually ended up, not as the pieces intended. Road is what
    // melee operators can stand on; wall is what towers and ranged operators get. Tuning
    // node widths and SPINE_CORRIDOR_WIDTH by eye is guesswork — tune against this number.
    private void LogRoadRatio(IGridDTO grid, int corridorCells)
    {
        int total = grid.width * grid.height;
        int road = 0;

        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
                if (grid.GetCell(x, y).isWalkable) road++;

        float ratio = total > 0 ? (float)road / total : 0f;
        int spine = m_SpineCells?.Count ?? 0;

        string msg = $"[MazeGen] Final grid: road {road}/{total} ({ratio:P0}), wall {total - road} " +
                     $"— spine {spine}, A* corridors {corridorCells}";

        if (ratio < TDConstant.ROAD_RATIO_MIN)
            Debug.LogWarning($"<color=orange>{msg} — TOO NARROW, melee has nowhere to stand. Widen the nodes.</color>");
        else if (ratio > TDConstant.ROAD_RATIO_MAX)
            Debug.LogWarning($"<color=orange>{msg} — TOO OPEN, not enough wall for towers/ranged. Narrow the nodes or the gate feeds.</color>");
        else
            Debug.Log($"<color=cyan>{msg}</color>");
    }

    private void ReopenSpine(IGridDTO grid)
    {
        if (m_SpineCells == null) return;

        foreach (var c in m_SpineCells)
            grid.GetCell(c.x, c.y).isWalkable = true;
    }

    private static bool IsDuplicate(List<List<IGridCellDTO>> existing, List<IGridCellDTO> candidate)
    {
        for (int i = 0; i < existing.Count; i++)
        {
            var other = existing[i];
            if (other.Count != candidate.Count) continue;

            bool same = true;
            for (int c = 0; c < other.Count; c++)
            {
                if (other[c].position == candidate[c].position) continue;
                same = false;
                break;
            }

            if (same) return true;
        }

        return false;
    }

    private void SetAllWalls(IGridDTO grid)
    {
        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
                grid.GetCell(x, y).isWalkable = false;
    }

    // Iterative Recursive Backtracker — avoids StackOverflow on large grids.
    // Room cells are at even (x,y) coordinates; corridor cells (walls between two rooms) are at odd coordinates.
    //
    // Multi-source: `seeds` is the whole starting frontier, not a single cell. Pass the
    // spine's even cells (with `visited` pre-marked for them) and branches grow outward
    // from every point along the skeleton at once.
    private void CarveFrom(IGridDTO grid, bool[,] visited, List<Vector2Int> seeds)
    {
        int[] dx = { 0, 2, 0, -2 };
        int[] dy = { 2, 0, -2, 0 };
        int[] dirs = { 0, 1, 2, 3 };

        var stack = new Stack<Vector2Int>();
        for (int i = 0; i < seeds.Count; i++)
            stack.Push(seeds[i]);

        while (stack.Count > 0)
        {
            var cur = stack.Peek();
            grid.GetCell(cur.x, cur.y).isWalkable = true;
            visited[cur.x, cur.y] = true;

            Shuffle(dirs);
            bool moved = false;

            foreach (int d in dirs)
            {
                int nx = cur.x + dx[d];
                int ny = cur.y + dy[d];

                if (!IsValidRoomCell(nx, ny, grid)) continue;
                if (visited[nx, ny]) continue;

                grid.GetCell(cur.x + dx[d] / 2, cur.y + dy[d] / 2).isWalkable = true;
                stack.Push(new Vector2Int(nx, ny));
                moved = true;
                break;
            }

            if (!moved) stack.Pop();
        }
    }

    private bool IsValidRoomCell(int x, int y, IGridDTO grid)
        => x >= 0 && x < grid.width && y >= 0 && y < grid.height
           && x % 2 == 0 && y % 2 == 0;

    private void Shuffle(int[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }
}

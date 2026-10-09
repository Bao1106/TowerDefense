using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step 2 of map generation: carve the spine before the maze runs.
///
/// Order of the whole pipeline:
///   1. TDGatePlacer     → gates on the border (unchanged)
///   2. TDSpineCarver    → THIS: chokepoints + the passages between them
///   3. TDMazePathGenerator.CarveFrom, seeded with the spine cells as already-visited
///                       → branches grow AROUND the spine instead of through it
///   4. A*               → naturally routes along the spine, it is the direct way through
///
/// Only steps 2 and 3 are new. The maze carver and A* are untouched.
/// </summary>
public static class TDSpineCarver
{
    public struct Result
    {
        /// <summary>Every cell the spine opened. Feeds the visited-seeding in step 3.</summary>
        public HashSet<Vector2Int> cells;

        /// <summary>Chokepoint centres in grid coordinates, in archetype order.</summary>
        public List<Vector2Int> nodeCenters;
    }

    /// <summary>
    /// Opens the spine on the grid and returns the cells it touched.
    /// Assumes the grid arrives fully walled (SetAllWalls) — same as the maze carver.
    /// </summary>
    public static Result Carve(IGridDTO grid, TDSpineArchetype arch,
                               IList<Vector2Int> startGates, IList<Vector2Int> endGates)
    {
        var result = new Result
        {
            cells = new HashSet<Vector2Int>(),
            nodeCenters = new List<Vector2Int>(arch.chokepoints.Length),
        };

        // ── Build the flow frame ─────────────────────────────────────────────
        // The archetype is written in flow space, so the axis has to come from where the
        // gates actually are. Deriving it from the gate centroids handles every MapLayout
        // — the straight four and both diagonals — with no per-layout branching, and it
        // follows automatically if StageConfig changes the gate counts.
        Vector2 startAnchor = Centroid(startGates);
        Vector2 endAnchor = Centroid(endGates);

        Vector2 axis = endAnchor - startAnchor;
        if (axis.sqrMagnitude < 0.01f) axis = Vector2.right; // degenerate: gates on top of each other

        Vector2 axisDir = axis.normalized;
        Vector2 perpDir = new Vector2(-axisDir.y, axisDir.x);

        float lateralHalf = LateralHalfExtent(grid, startGates, endGates);

        // ── Place the chokepoints ────────────────────────────────────────────
        // Centres snap to EVEN coordinates. The maze convention is that even (x,y) are
        // room cells and odd ones are the walls between them; a node centred on an odd
        // cell would leave the maze unable to attach branches to it.
        for (int i = 0; i < arch.chokepoints.Length; i++)
        {
            var def = arch.chokepoints[i];

            Vector2 p = startAnchor
                      + axisDir * (def.flowPos.x * axis.magnitude)
                      + perpDir * ((def.flowPos.y - 0.5f) * 2f * lateralHalf);

            var center = SnapEven(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), grid);

            result.nodeCenters.Add(center);
            OpenBlock(grid, center, def.width, result.cells);
        }

        // Gates on a vertical border (Left/Right) share a column, so their feeds must turn
        // early and finish along each gate's own row; gates on a horizontal border are the
        // mirror case. Reading it off the flow axis covers both, and the diagonals too.
        bool gatesShareColumn = Mathf.Abs(axisDir.x) >= Mathf.Abs(axisDir.y);

        // ── Wire the chokepoints together ────────────────────────────────────
        foreach (var edge in arch.edges)
        {
            var from = result.nodeCenters[edge.from];

            if (edge.to == TDSpineEdge.GOAL)
            {
                // Last tier runs out to every end gate — each on its own approach.
                for (int i = 0; i < endGates.Count; i++)
                    OpenCorridor(grid, from, endGates[i], result.cells, gatesShareColumn);
            }
            else
            {
                OpenCorridor(grid, from, result.nodeCenters[edge.to], result.cells);
            }
        }

        // ── Feed the start gates in ──────────────────────────────────────────
        // One rule for every archetype: a gate attaches to its nearest chokepoint.
        // In FUNNEL that groups gates onto the two outer nodes; in CASCADE every gate
        // reaches the same first node, so the feeds merge before the fighting starts.
        // Same rule, two very different maps.
        var fedNodes = new HashSet<int>();

        for (int i = 0; i < startGates.Count; i++)
        {
            int idx = NearestNodeIndex(result.nodeCenters, startGates[i]);
            // Corner at the node end here too, for the same reason: several start gates on
            // one border must not thread their feeds through each other.
            OpenCorridor(grid, result.nodeCenters[idx], startGates[i], result.cells, gatesShareColumn);
            fedNodes.Add(idx);
        }

        // With fewer gates than entry nodes — one start gate against FUNNEL's two outer
        // nodes, say — "nearest wins" leaves the others carved but unreachable: dead
        // blobs of open ground with nothing routing into them. Give every entry-tier node
        // a feed from whichever gate is closest to it.
        int entryTier = int.MaxValue;
        for (int i = 0; i < arch.chokepoints.Length; i++)
            if (arch.chokepoints[i].tier < entryTier) entryTier = arch.chokepoints[i].tier;

        for (int i = 0; i < arch.chokepoints.Length; i++)
        {
            if (arch.chokepoints[i].tier != entryTier || fedNodes.Contains(i)) continue;

            var gate = NearestCell(startGates, result.nodeCenters[i]);
            OpenCorridor(grid, result.nodeCenters[i], gate, result.cells, gatesShareColumn);
        }

        return result;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>Opens a width × width block centred on `center`.</summary>
    private static void OpenBlock(IGridDTO grid, Vector2Int center, int width, HashSet<Vector2Int> opened)
    {
        int half = width / 2;
        int lo = -half;
        int hi = width - half - 1;

        for (int dx = lo; dx <= hi; dx++)
            for (int dy = lo; dy <= hi; dy++)
                OpenCell(grid, center.x + dx, center.y + dy, opened);
    }

    /// <summary>
    /// L-shaped passage between two points. Thickness comes from
    /// TDConstant.SPINE_CORRIDOR_WIDTH — gate feeds are the longest part of the spine, so
    /// that width dominates the road/wall balance.
    ///
    /// `verticalFirst` decides WHERE THE CORNER GOES, and it matters more than it looks.
    /// Horizontal-first turns at b's column: with several gates sharing a border column,
    /// the leg to the far gate runs down that column and passes straight THROUGH the
    /// nearer gate — so the route to end gate 2 entered by way of end gate 1, and gate 2
    /// had no approach of its own. Turning at a's end instead gives every gate a final
    /// leg along its own row.
    /// </summary>
    private static void OpenCorridor(IGridDTO grid, Vector2Int a, Vector2Int b,
                                     HashSet<Vector2Int> opened, bool verticalFirst = false)
    {
        if (verticalFirst)
        {
            OpenVerticalRun(grid, a.x, a.y, b.y, opened);
            OpenHorizontalRun(grid, b.y, a.x, b.x, opened);
            return;
        }

        OpenHorizontalRun(grid, a.y, a.x, b.x, opened);
        OpenVerticalRun(grid, b.x, a.y, b.y, opened);
    }

    private static void OpenHorizontalRun(IGridDTO grid, int row, int fromX, int toX, HashSet<Vector2Int> opened)
    {
        Band(out int lo, out int hi);
        int step = fromX <= toX ? 1 : -1;

        for (int x = fromX; x != toX + step; x += step)
            for (int t = lo; t <= hi; t++)
                OpenCell(grid, x, row + t, opened);
    }

    private static void OpenVerticalRun(IGridDTO grid, int col, int fromY, int toY, HashSet<Vector2Int> opened)
    {
        Band(out int lo, out int hi);
        int step = fromY <= toY ? 1 : -1;

        for (int y = fromY; y != toY + step; y += step)
            for (int t = lo; t <= hi; t++)
                OpenCell(grid, col + t, y, opened);
    }

    private static void Band(out int lo, out int hi)
    {
        int w = Mathf.Max(1, TDConstant.SPINE_CORRIDOR_WIDTH);
        int half = w / 2;
        lo = -half;
        hi = w - half - 1;
    }

    private static void OpenCell(IGridDTO grid, int x, int y, HashSet<Vector2Int> opened)
    {
        if (x < 0 || x >= grid.width || y < 0 || y >= grid.height) return;

        grid.GetCell(x, y).isWalkable = true;
        opened.Add(new Vector2Int(x, y));
    }

    /// <summary>
    /// Distance from the start-gate centroid to the end-gate centroid — the depth an
    /// archetype has to fit its tiers into. Compare against
    /// TDSpineArchetype.RequiredTierSpacing before carving.
    /// </summary>
    public static float FlowAxisLength(IList<Vector2Int> startGates, IList<Vector2Int> endGates)
        => Vector2.Distance(Centroid(startGates), Centroid(endGates));

    /// <summary>
    /// How far a node may sit off the centre line before it runs off the grid. Compare
    /// against TDSpineArchetype.minLateralHalf before carving: on a map that is short
    /// across the flow, two same-tier nodes fuse into one.
    /// </summary>
    public static float LateralHalfExtent(IGridDTO grid, IList<Vector2Int> startGates, IList<Vector2Int> endGates)
    {
        Vector2 axis = Centroid(endGates) - Centroid(startGates);
        if (axis.sqrMagnitude < 0.01f) axis = Vector2.right;

        Vector2 axisDir = axis.normalized;
        Vector2 perpDir = new Vector2(-axisDir.y, axisDir.x);

        // LATERAL_FILL keeps the outer nodes clear of the border so the maze still has
        // room to grow behind them.
        return LATERAL_FILL * 0.5f *
            (Mathf.Abs(perpDir.x) * (grid.width - 1) + Mathf.Abs(perpDir.y) * (grid.height - 1));
    }

    private const float LATERAL_FILL = 0.7f;

    private static Vector2 Centroid(IList<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return Vector2.zero;

        float x = 0f, y = 0f;
        for (int i = 0; i < cells.Count; i++)
        {
            x += cells[i].x;
            y += cells[i].y;
        }

        return new Vector2(x / cells.Count, y / cells.Count);
    }

    /// <summary>Nearest by Manhattan distance — the grid is 4-directional, so that is the true metric.</summary>
    private static int NearestNodeIndex(List<Vector2Int> nodes, Vector2Int from)
    {
        int best = 0;
        int bestDist = int.MaxValue;

        for (int i = 0; i < nodes.Count; i++)
        {
            int d = Mathf.Abs(nodes[i].x - from.x) + Mathf.Abs(nodes[i].y - from.y);
            if (d >= bestDist) continue;
            bestDist = d;
            best = i;
        }

        return best;
    }

    private static Vector2Int NearestCell(IList<Vector2Int> cells, Vector2Int from)
        => cells[NearestNodeIndex(new List<Vector2Int>(cells), from)];

    /// <summary>Snaps to the nearest even cell inside the grid — see the note in Carve().</summary>
    private static Vector2Int SnapEven(int x, int y, IGridDTO grid)
    {
        if (x % 2 != 0) x--;
        if (y % 2 != 0) y--;

        return new Vector2Int(
            Mathf.Clamp(x, 0, LastEven(grid.width)),
            Mathf.Clamp(y, 0, LastEven(grid.height)));
    }

    private static int LastEven(int size)
    {
        int last = size - 1;
        return last % 2 == 0 ? last : last - 1;
    }
}

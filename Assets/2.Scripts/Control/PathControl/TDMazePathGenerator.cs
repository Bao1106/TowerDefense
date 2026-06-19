using System.Collections.Generic;
using UnityEngine;

// Recursive Backtracker maze generator — multi-group variant.
//
// GenerateForGroups():
// For each TDPathGroup, generates PathCount independent mazes.
// Each maze run: reset the grid → CarveFrom(group.StartCell) → extract 1 path via A*.
// Collects all path cells into a combinedPathCells HashSet → restores all at the end in one pass.
// → Prevents SetAllWalls() from wiping the results of previously processed groups.
//
// CarveFrom: uses an iterative Stack instead of recursion → avoids StackOverflow on large grids.
public class TDMazePathGenerator
{
    public static TDMazePathGenerator api;

    private readonly IPathFinder m_PathFinder;

    public TDMazePathGenerator(IPathFinder pathFinder)
    {
        m_PathFinder = pathFinder;
    }

    // Entry point: generates paths for all groups and populates group.Corridors
    public void GenerateForGroups(IGridDTO gridDTO, List<TDPathGroup> groups)
    {
        var combinedPathCells = new HashSet<Vector2Int>();

        foreach (var group in groups)
        {
            group.Corridors = new List<List<IGridCellDTO>>();

            for (int i = 0; i < group.PathCount; i++)
            {
                var path = Carve(gridDTO, group, groups);
                if (path != null && path.Count > 0)
                {
                    group.Corridors.Add(path);
                    foreach (var cell in path)
                        combinedPathCells.Add(cell.position);

                    Debug.Log($"<color=cyan>[MazeGen] Group {groups.IndexOf(group)} path {i + 1}/{group.PathCount}: {path.Count} cells</color>");
                }
                else
                {
                    Debug.LogWarning($"<color=orange>[MazeGen] Group {groups.IndexOf(group)} path {i + 1} failed</color>");
                }
            }
        }

        // Final pass: restore all path cells into the grid as walkable
        SetAllWalls(gridDTO);
        foreach (var pos in combinedPathCells)
            gridDTO.GetCell(pos.x, pos.y).isWalkable = true;

        // Safety: start/end cells are always kept walkable
        foreach (var group in groups)
        {
            gridDTO.GetCell(group.StartCell.x, group.StartCell.y).isWalkable = true;
            gridDTO.GetCell(group.EndCell.x, group.EndCell.y).isWalkable = true;
        }

        int corridorCount = combinedPathCells.Count;
        int wallCount = gridDTO.width * gridDTO.height - corridorCount;
        Debug.Log($"<color=cyan>[MazeGen] Final grid: {corridorCount} corridor cells, {wallCount} wall cells</color>");
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // Reset → carve → A* extract. Retries up to TDConstant.MAZE_MAX_ATTEMPTS times until a
    // path of sufficient length (>= gridDTO.width) is found. Falls back to the longest path found.
    // allGroups: blocks the gate cells of other groups before A* runs
    // → prevents this group's path from routing through another group's gate.
    private List<IGridCellDTO> Carve(IGridDTO gridDTO, TDPathGroup group, List<TDPathGroup> allGroups)
    {
        int minLength = gridDTO.width;

        List<IGridCellDTO> best = null;

        for (int attempt = 0; attempt < TDConstant.MAZE_MAX_ATTEMPTS; attempt++)
        {
            SetAllWalls(gridDTO);
            var visited = new bool[gridDTO.width, gridDTO.height];
            CarveFrom(gridDTO, visited, group.StartCell.x, group.StartCell.y);
            gridDTO.GetCell(group.EndCell.x, group.EndCell.y).isWalkable = true;

            // Block gate cells of other groups — A* must not route through them
            foreach (var other in allGroups)
            {
                if (other == group) continue;
                gridDTO.GetCell(other.StartCell.x, other.StartCell.y).isWalkable = false;
                gridDTO.GetCell(other.EndCell.x, other.EndCell.y).isWalkable = false;
            }

            var startCell = gridDTO.GetCell(group.StartCell.x, group.StartCell.y);
            var endCell = gridDTO.GetCell(group.EndCell.x, group.EndCell.y);
            var path = m_PathFinder.ComputePath(gridDTO, startCell, endCell);

            if (path != null && path.Count >= minLength)
            {
                Debug.Log($"<color=cyan>[MazeGen] path ok attempt {attempt + 1}: {path.Count} cells (min={minLength})</color>");
                return path;
            }

            if (best == null || (path != null && path.Count > best.Count))
                best = path;
        }

        Debug.LogWarning($"<color=orange>[MazeGen] fallback after {TDConstant.MAZE_MAX_ATTEMPTS} attempts: {best?.Count ?? 0} cells</color>");
        return best;
    }

    private void SetAllWalls(IGridDTO grid)
    {
        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
                grid.GetCell(x, y).isWalkable = false;
    }

    // Iterative Recursive Backtracker — avoids StackOverflow on large grids.
    // Room cells are at even (x,y) coordinates; corridor cells (walls between two rooms) are at odd coordinates.
    private void CarveFrom(IGridDTO grid, bool[,] visited, int startX, int startY)
    {
        int[] dx = { 0, 2, 0, -2 };
        int[] dy = { 2, 0, -2, 0 };
        int[] dirs = { 0, 1, 2, 3 };

        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));

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

using System.Collections.Generic;
using UnityEngine;

// Recursive Backtracker maze generator — multi-path variant.
//
// GenerateMultiplePaths():
//   Gen maze N lần độc lập, mỗi lần extract 1 path (A* shortest).
//   Combine tất cả path cells → final grid walkable layout.
//   → N corridors khác nhau, tower spots = mọi cell KHÔNG thuộc bất kỳ path nào.
//
// Grid layout (cellSize=2):
//   Room cells tại even (x,y) — carved by backtracker
//   Wall cells tại odd coordinates — carved khi connect 2 rooms
public class TDMazePathGenerator
{
    public static TDMazePathGenerator api;

    // Entry point: gen N mazes, extract 1 path each, combine into unified grid
    public List<List<IGridCellDTO>> GenerateMultiplePaths(IGridDTO gridDTO,
        Vector2Int start, Vector2Int end, int pathCount)
    {
        var allPaths         = new List<List<IGridCellDTO>>();
        var combinedPathCells = new HashSet<Vector2Int>();

        for (int i = 0; i < pathCount; i++)
        {
            // Reset grid → all walls
            SetAllWalls(gridDTO);

            // Gen new maze (Random shuffle → different layout each run)
            var visited = new bool[gridDTO.width, gridDTO.height];
            CarveFrom(gridDTO, visited, start.x, start.y);
            gridDTO.GetCell(end.x, end.y).isWalkable = true;

            // Extract shortest path qua maze corridor
            var startCell = gridDTO.GetCell(start.x, start.y);
            var endCell   = gridDTO.GetCell(end.x,   end.y);
            var path      = TDaStarPathControl.api.ComputePath(gridDTO, startCell, endCell);

            if (path != null && path.Count > 0)
            {
                allPaths.Add(path);
                foreach (var cell in path)
                    combinedPathCells.Add(cell.position);
                Debug.Log($"<color=cyan>[MazeGen] Path {i + 1}/{pathCount}: {path.Count} cells</color>");
            }
            else
            {
                Debug.LogWarning($"<color=orange>[MazeGen] Path {i + 1} failed — no route found</color>");
            }
        }

        // Build final grid: union of all path cells = walkable, rest = wall (tower spots)
        SetAllWalls(gridDTO);
        foreach (var pos in combinedPathCells)
            gridDTO.GetCell(pos.x, pos.y).isWalkable = true;

        // Safety: start & end luôn walkable
        gridDTO.GetCell(start.x, start.y).isWalkable = true;
        gridDTO.GetCell(end.x,   end.y).isWalkable   = true;

        int corridorCount = combinedPathCells.Count;
        int wallCount     = gridDTO.width * gridDTO.height - corridorCount;
        Debug.Log($"<color=cyan>[MazeGen] Final grid: {corridorCount} corridor cells, {wallCount} wall cells (tower spots)</color>");

        return allPaths;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SetAllWalls(IGridDTO grid)
    {
        for (int x = 0; x < grid.width; x++)
            for (int y = 0; y < grid.height; y++)
                grid.GetCell(x, y).isWalkable = false;
    }

    // Depth-first recursive backtracker
    // Room cells tại even (x,y), wall cell giữa 2 rooms được carved khi connect
    private void CarveFrom(IGridDTO grid, bool[,] visited, int cx, int cy)
    {
        visited[cx, cy] = true;
        grid.GetCell(cx, cy).isWalkable = true;

        int[] dx   = {  0,  2,  0, -2 };
        int[] dy   = {  2,  0, -2,  0 };
        int[] dirs = {  0,  1,  2,  3 };
        Shuffle(dirs);

        foreach (int d in dirs)
        {
            int nx = cx + dx[d];
            int ny = cy + dy[d];

            if (!IsValidRoomCell(nx, ny, grid)) continue;
            if (visited[nx, ny]) continue;

            grid.GetCell(cx + dx[d] / 2, cy + dy[d] / 2).isWalkable = true;
            CarveFrom(grid, visited, nx, ny);
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

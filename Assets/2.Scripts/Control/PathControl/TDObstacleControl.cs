using System;
using System.Collections.Generic;
using UnityEngine;

// Đặt random obstacles lên grid — visual decoration only
// Path cells được loại trừ hoàn toàn (obstacles không đặt lên path)
// isWalkable=false trên obstacle cell giúp tower placement biết cell bị chiếm
public class TDObstacleControl
{
    public static TDObstacleControl api;

    // Trả về list vị trí grid (x, y) các cell đã được đặt obstacle
    public Action<List<Vector2Int>> onObstaclesPlaced;

    // pathCells: tập hợp tất cả cells thuộc bất kỳ path nào → obstacles không được đặt lên đây
    public void PlaceObstacles(IGridDTO gridDTO, Vector2Int start, Vector2Int end, HashSet<Vector2Int> pathCells)
    {
        List<Vector2Int> candidates = GetCandidateCells(gridDTO, start, end, pathCells);
        Shuffle(candidates);

        List<Vector2Int> placed = new List<Vector2Int>();
        int spacing = TDConstant.CONFIG_OBSTACLE_SPACING;

        foreach (Vector2Int cell in candidates)
        {
            if (placed.Count >= TDConstant.CONFIG_OBSTACLE_COUNT) break;

            // Check minimum spacing so large prefabs don't visually overlap
            if (IsTooCloseToExisting(cell, placed, spacing)) continue;

            // Mark cell non-walkable (tower placement sẽ check isWalkable)
            IGridCellDTO gridCell = gridDTO.GetCell(cell.x, cell.y);
            gridCell.isWalkable = false;

            placed.Add(cell);
        }

        Debug.Log($"<color=yellow>PlaceObstacles: placed {placed.Count}/{TDConstant.CONFIG_OBSTACLE_COUNT} obstacles</color>");
        onObstaclesPlaced?.Invoke(placed);
    }

    // Kiểm tra cell có quá gần bất kỳ obstacle nào đã đặt không (Manhattan distance)
    private bool IsTooCloseToExisting(Vector2Int candidate, List<Vector2Int> placed, int minSpacing)
    {
        foreach (Vector2Int p in placed)
        {
            if (Mathf.Abs(candidate.x - p.x) + Mathf.Abs(candidate.y - p.y) < minSpacing)
                return true;
        }
        return false;
    }

    // Lấy candidates: trong grid, trên HUD zone, không quá gần start/end, không phải path cell
    private List<Vector2Int> GetCandidateCells(IGridDTO gridDTO, Vector2Int start, Vector2Int end, HashSet<Vector2Int> pathCells)
    {
        int radius = TDConstant.CONFIG_OBSTACLE_EXCLUSION_RADIUS;
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < gridDTO.width; x++)
        {
            for (int y = TDConstant.CONFIG_PATH_MIN_GRID_Y; y < gridDTO.height; y++)
            {
                // Skip buffer quanh start và end
                bool nearStart = Mathf.Abs(x - start.x) <= radius && Mathf.Abs(y - start.y) <= radius;
                bool nearEnd   = Mathf.Abs(x - end.x)   <= radius && Mathf.Abs(y - end.y)   <= radius;
                if (nearStart || nearEnd) continue;

                // Skip cells thuộc path → obstacles không chặn enemy route
                if (pathCells != null && pathCells.Contains(new Vector2Int(x, y))) continue;

                if (gridDTO.GetCell(x, y).isWalkable)
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        return candidates;
    }

    // Fisher-Yates shuffle
    private void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

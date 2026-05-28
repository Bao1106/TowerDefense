using System;
using System.Collections.Generic;
using UnityEngine;

// Đặt obstacles lên grid với multi-cell footprint
// Visual decoration + grid blocking (isWalkable=false trên toàn footprint)
// footprintRadii[i] = radius của prefab thứ i:
//   radius=0 → 1×1 (1 cell)
//   radius=1 → 3×3 (9 cells) symmetric quanh center — dùng cho prefab lớn có random rotation
public class TDObstacleControl
{
    public static TDObstacleControl api;

    // centers      : vị trí grid center mỗi obstacle (dùng để spawn visual)
    // prefabIndices: index trong m_ObstaclePrefabs tương ứng với mỗi center
    // allBlocked   : TẤT CẢ cells bị chiếm (center + footprint) — dùng để mark grid + exclude valid tower cells
    public Action<List<Vector2Int>, List<int>, List<Vector2Int>> onObstaclesPlaced;

    // prefabCount     : số lượng prefab trong m_ObstaclePrefabs (để random index)
    // footprintRadii  : mảng radius theo prefab index (length = prefabCount)
    public void PlaceObstacles(IGridDTO gridDTO, Vector2Int start, Vector2Int end,
                                HashSet<Vector2Int> pathCells, int prefabCount, int[] footprintRadii)
    {
        List<Vector2Int> candidates = GetCandidateCells(gridDTO, start, end, pathCells);
        Shuffle(candidates);

        List<Vector2Int>    centers       = new List<Vector2Int>();
        List<int>           prefabIndices = new List<int>();
        HashSet<Vector2Int> blockedSet    = new HashSet<Vector2Int>();
        int spacing = TDConstant.CONFIG_OBSTACLE_SPACING;

        foreach (Vector2Int cell in candidates)
        {
            if (centers.Count >= TDConstant.CONFIG_OBSTACLE_COUNT) break;
            if (IsTooCloseToExisting(cell, centers, spacing)) continue;

            // Chọn ngẫu nhiên prefab, lấy footprint radius tương ứng
            int pIdx   = prefabCount > 0 ? UnityEngine.Random.Range(0, prefabCount) : 0;
            int radius = (footprintRadii != null && pIdx < footprintRadii.Length)
                ? footprintRadii[pIdx] : 0;

            // Mark toàn bộ footprint cells là non-walkable
            foreach (Vector2Int fc in GetFootprintCells(cell, radius, gridDTO))
            {
                gridDTO.GetCell(fc.x, fc.y).isWalkable = false;
                blockedSet.Add(fc);
            }

            centers.Add(cell);
            prefabIndices.Add(pIdx);
        }

        Debug.Log($"<color=yellow>PlaceObstacles: {centers.Count} obstacles, {blockedSet.Count} blocked cells</color>");
        onObstaclesPlaced?.Invoke(centers, prefabIndices, new List<Vector2Int>(blockedSet));
    }

    // Trả về tất cả cells trong footprint (2*radius+1)×(2*radius+1) quanh center, clamp vào bounds
    private List<Vector2Int> GetFootprintCells(Vector2Int center, int radius, IGridDTO gridDTO)
    {
        var cells = new List<Vector2Int>();
        for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                int nx = center.x + dx;
                int nz = center.y + dz;
                if (nx >= 0 && nx < gridDTO.width && nz >= 0 && nz < gridDTO.height)
                    cells.Add(new Vector2Int(nx, nz));
            }
        return cells;
    }

    // Manhattan distance spacing giữa các obstacle centers
    private bool IsTooCloseToExisting(Vector2Int candidate, List<Vector2Int> placed, int minSpacing)
    {
        foreach (Vector2Int p in placed)
            if (Mathf.Abs(candidate.x - p.x) + Mathf.Abs(candidate.y - p.y) < minSpacing)
                return true;
        return false;
    }

    private List<Vector2Int> GetCandidateCells(IGridDTO gridDTO, Vector2Int start, Vector2Int end,
                                                HashSet<Vector2Int> pathCells)
    {
        int radius = TDConstant.CONFIG_OBSTACLE_EXCLUSION_RADIUS;
        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < gridDTO.width; x++)
        {
            for (int y = TDConstant.CONFIG_PATH_MIN_GRID_Y; y < gridDTO.height; y++)
            {
                bool nearStart = Mathf.Abs(x - start.x) <= radius && Mathf.Abs(y - start.y) <= radius;
                bool nearEnd   = Mathf.Abs(x - end.x)   <= radius && Mathf.Abs(y - end.y)   <= radius;
                if (nearStart || nearEnd) continue;
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

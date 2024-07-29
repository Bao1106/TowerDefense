﻿using System.Collections.Generic;
using System.Linq;
using Enums;
using Interfaces.Grid;
using Interfaces.PathFinder;
using UnityEngine;

public class AStarPathControl : IPathFinder
{
    public List<IGridCell> FindPath(IGrid grid, IGridCell start, IGridCell end)
    {
        List<IGridCell> openSet = new List<IGridCell> { start };
        List<IGridCell> closedSet = new List<IGridCell>();
        Dictionary<IGridCell, IGridCell> cameFrom = new Dictionary<IGridCell, IGridCell>();
        Dictionary<IGridCell, float> gScore = new Dictionary<IGridCell, float>();
        Dictionary<IGridCell, float> fScore = new Dictionary<IGridCell, float>();

        gScore[start] = 0;
        fScore[start] = HeuristicCostEstimate(start, end);

        while (openSet.Count > 0)
        {
            var current = openSet.OrderBy(node => fScore[node]).First();

            if (current.Position == end.Position)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (var neighbor in GetNeighbors(grid, current))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                var tentativeGScore = gScore[current] + GetMovementCost(current, neighbor);

                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
                else if (tentativeGScore >= gScore[neighbor])
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + HeuristicCostEstimate(neighbor, end);
            }
        }

        return null;
    }

    private float HeuristicCostEstimate(IGridCell start, IGridCell goal)
    {
        Vector2 startPos = start.Position;
        Vector2 goalPos = goal.Position;
        float dx = Mathf.Abs(startPos.x - goalPos.x);
        float dy = Mathf.Abs(startPos.y - goalPos.y);
        return (Mathf.Max(dx, dy) + Random.Range(0f, 0.1f)) * (1 + Random.Range(0f, 0.1f));
    }

    private float GetMovementCost(IGridCell from, IGridCell to)
    {
        // Chỉ cho phép di chuyển ngang hoặc dọc
        return 1.0f;
    }
    
    private List<IGridCell> GetNeighbors(IGrid grid, IGridCell cell)
    {
        List<IGridCell> neighbors = new List<IGridCell>();
        int[] dx = { 0, 1, 0, -1 }; // Chỉ cho phép di chuyển lên, phải, xuống, trái
        int[] dy = { 1, 0, -1, 0 };

        for (int i = 0; i < 4; i++)
        {
            var checkX = cell.Position.x + dx[i];
            var checkY = cell.Position.y + dy[i];

            if (checkX >= 0 && checkX < grid.width && checkY >= 0 && checkY < grid.height)
            {
                var neighbor = grid.GetCell(checkX, checkY);
                if (neighbor.IsWalkable && neighbor.Type != CellType.Obstacle)
                {
                    neighbors.Add(neighbor);
                }
            }
        }
        return neighbors;
    }

    private List<IGridCell> ReconstructPath(Dictionary<IGridCell, IGridCell> cameFrom, IGridCell current)
    {
        List<IGridCell> path = new List<IGridCell> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private void MarkPathAsNonWalkable(IGrid grid, List<IGridCell> path)
    {
        foreach (IGridCell cell in path)
        {
            cell.IsWalkable = false; // Mark the path cells as non-walkable
        }
    }

    private void ResetPathAsWalkable(IGrid grid, List<IGridCell> path)
    {
        foreach (IGridCell cell in path)
        {
            cell.IsWalkable = true; // Reset the path cells to walkable
        }
    }

    public List<List<IGridCell>> FindMultiplePaths(IGrid grid, IGridCell start, IGridCell end, int numberOfPaths)
    {
        List<List<IGridCell>> paths = new List<List<IGridCell>>();
        for (int i = 0; i < numberOfPaths; i++)
        {
            var path = FindPath(grid, start, end);
            if (path != null)
            {
                paths.Add(path);
                MarkPathAsNonWalkable(grid, path);
            }
            else
            {
                break;
            }
        }

        // Reset all paths to walkable after finding all paths
        foreach (List<IGridCell> path in paths)
        {
            ResetPathAsWalkable(grid, path);
        }

        return paths;
    }
}

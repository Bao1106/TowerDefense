using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ReSharper disable once InconsistentNaming
public class TDaStarPathControl : IPathFinderDTO
{
    public static TDaStarPathControl api;

    public Action<List<IGridCellDTO>, IGridCellDTO> onGetPath;
    public Action<List<IGridCellDTO>> onGetFinalPath;
    public Action<int> onGetWaypointIndex;

    public void SetIndex(int index)
    {
        onGetWaypointIndex?.Invoke(index);
    }

    // Legacy event wrapper — dùng cho waypoint chain hiện tại (Phase 1)
    public void FindPath(IGridDTO gridDTO, IGridCellDTO start, IGridCellDTO end, bool isFinal)
    {
        var path = ComputePath(gridDTO, start, end);
        if (isFinal)
            onGetFinalPath?.Invoke(path);
        else
            onGetPath?.Invoke(path, end);
    }

    // Sync internal — trả về List<IGridCellDTO> hoặc null nếu không tìm được path
    public List<IGridCellDTO> ComputePath(IGridDTO gridDTO, IGridCellDTO start, IGridCellDTO end)
    {
        if (start == null || end == null) return null;

        List<IGridCellDTO> openSet = new List<IGridCellDTO> { start };
        HashSet<IGridCellDTO> closedSet = new HashSet<IGridCellDTO>();
        Dictionary<IGridCellDTO, IGridCellDTO> cameFrom = new Dictionary<IGridCellDTO, IGridCellDTO>();
        Dictionary<IGridCellDTO, float> gScore = new Dictionary<IGridCellDTO, float>();
        Dictionary<IGridCellDTO, float> fScore = new Dictionary<IGridCellDTO, float>();

        gScore[start] = 0;
        fScore[start] = TDaStarPathModel.api.HeuristicCostEstimate(start, end);

        while (openSet.Count > 0)
        {
            IGridCellDTO current = openSet
                .OrderBy(node => fScore.ContainsKey(node) ? fScore[node] : float.MaxValue)
                .First();

            if (current.position == end.position)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (IGridCellDTO neighbor in TDaStarPathModel.api.GetNeighbors(gridDTO, current))
            {
                if (closedSet.Contains(neighbor)) continue;

                float tentativeGScore = (gScore.ContainsKey(current) ? gScore[current] : float.MaxValue)
                                        + TDaStarPathModel.api.GetMovementCost(current, neighbor);

                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
                else if (tentativeGScore >= (gScore.ContainsKey(neighbor) ? gScore[neighbor] : float.MaxValue))
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + TDaStarPathModel.api.HeuristicCostEstimate(neighbor, end);
            }
        }

        return null; // No path found
    }

    // Phase 2 — tính path qua chuỗi waypoints cố định (mỗi path có waypoints riêng)
    // Ghép các đoạn A* start→wp[0]→wp[1]→...→end thành 1 path hoàn chỉnh
    public List<IGridCellDTO> ComputeWaypointPath(IGridDTO gridDTO, IGridCellDTO start, IGridCellDTO end, Vector2Int[] waypoints)
    {
        List<IGridCellDTO> fullPath = new List<IGridCellDTO>();
        IGridCellDTO current = start;

        foreach (Vector2Int wp in waypoints)
        {
            IGridCellDTO wpCell = gridDTO.GetCell(wp.x, wp.y);
            List<IGridCellDTO> segment = ComputePath(gridDTO, current, wpCell);
            if (segment == null || segment.Count == 0) return null;

            // Thêm tất cả trừ cell cuối (tránh duplicate tại điểm nối)
            for (int i = 0; i < segment.Count - 1; i++)
                fullPath.Add(segment[i]);

            current = wpCell;
        }

        // Đoạn cuối đến END
        List<IGridCellDTO> lastSegment = ComputePath(gridDTO, current, end);
        if (lastSegment == null || lastSegment.Count == 0) return null;
        fullPath.AddRange(lastSegment);

        return fullPath;
    }

    // Phase 2 — tìm N path khác nhau bằng path-blocking (interior cells)
    public List<List<IGridCellDTO>> FindMultiplePaths(IGridDTO gridDTO, IGridCellDTO start, IGridCellDTO end, int numberOfPaths)
    {
        List<List<IGridCellDTO>> paths = new List<List<IGridCellDTO>>();
        List<List<IGridCellDTO>> blockedPaths = new List<List<IGridCellDTO>>();

        for (int i = 0; i < numberOfPaths; i++)
        {
            var path = ComputePath(gridDTO, start, end);
            if (path != null && path.Count > 0)
            {
                paths.Add(path);
                blockedPaths.Add(path);
                MarkPathInteriorAsNonWalkable(path);
            }
            else
            {
                break;
            }
        }

        // Reset tất cả cells về walkable sau khi tìm xong
        foreach (var path in blockedPaths)
            ResetPathInteriorAsWalkable(path);

        return paths;
    }

    // BuildDefinedPath — vẽ straight horizontal/vertical segments giữa các waypoints
    // KHÔNG dùng A* → path shape 100% do CONFIG_MULTI_PATH_WAYPOINTS quyết định
    // Rule: mỗi cặp waypoint liên tiếp phải cùng X hoặc cùng Y
    public List<IGridCellDTO> BuildDefinedPath(IGridDTO gridDTO, Vector2Int start, Vector2Int end, Vector2Int[] waypoints)
    {
        var path = new List<IGridCellDTO>();
        Vector2Int cur = start;

        foreach (Vector2Int wp in waypoints)
        {
            AppendStraightSegment(gridDTO, path, cur, wp);
            cur = wp;
        }

        // Đoạn cuối → end
        AppendStraightSegment(gridDTO, path, cur, end);
        // Thêm cell end (AppendStraightSegment không thêm cell đích)
        path.Add(gridDTO.GetCell(end.x, end.y));

        return path;
    }

    // Vẽ đoạn thẳng từ 'from' đến 'to' (cùng X hoặc cùng Y)
    // Thêm tất cả cells của đoạn KHÔNG bao gồm cell 'to' (tránh duplicate tại điểm nối)
    private void AppendStraightSegment(IGridDTO gridDTO, List<IGridCellDTO> path, Vector2Int from, Vector2Int to)
    {
        int x = from.x, y = from.y;

        // Di chuyển theo X trước (nếu khác X)
        if (from.x != to.x)
        {
            int dx = to.x > from.x ? 1 : -1;
            while (x != to.x)
            {
                path.Add(gridDTO.GetCell(x, y));
                x += dx;
            }
        }

        // Di chuyển theo Y (nếu khác Y)
        if (from.y != to.y)
        {
            int dy = to.y > from.y ? 1 : -1;
            while (y != to.y)
            {
                path.Add(gridDTO.GetCell(x, y));
                y += dy;
            }
        }

        // Nếu from == to (waypoint trùng điểm hiện tại) → không thêm gì
    }

    private List<IGridCellDTO> ReconstructPath(Dictionary<IGridCellDTO, IGridCellDTO> cameFrom, IGridCellDTO current)
    {
        List<IGridCellDTO> path = new List<IGridCellDTO> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    // Chỉ block interior cells (index 1..n-2), giữ nguyên start/end để path sau vẫn connect
    private void MarkPathInteriorAsNonWalkable(List<IGridCellDTO> path)
    {
        for (int i = 1; i < path.Count - 1; i++)
            path[i].isWalkable = false;
    }

    private void ResetPathInteriorAsWalkable(List<IGridCellDTO> path)
    {
        for (int i = 1; i < path.Count - 1; i++)
            path[i].isWalkable = true;
    }
}

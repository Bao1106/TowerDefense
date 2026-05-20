using System;
using System.Collections.Generic;
using System.Linq;

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
    
    public void FindPath(IGridDTO gridDTO, IGridCellDTO start, IGridCellDTO end, bool isFinal)
    {
        List<IGridCellDTO> openSet = new List<IGridCellDTO> { start };
        List<IGridCellDTO> closedSet = new List<IGridCellDTO>();
        Dictionary<IGridCellDTO, IGridCellDTO> cameFrom = new Dictionary<IGridCellDTO, IGridCellDTO>();
        Dictionary<IGridCellDTO, float> gScore = new Dictionary<IGridCellDTO, float>();
        Dictionary<IGridCellDTO, float> fScore = new Dictionary<IGridCellDTO, float>();

        if (start == null) return;
        
        gScore[start] = 0;
        fScore[start] = TDaStarPathModel.api.HeuristicCostEstimate(start, end);

        while (openSet.Count > 0)
        {
            IGridCellDTO current = openSet.OrderBy(node => fScore[node]).First();

            if (current.position == end.position)
            {
                ReconstructPath(cameFrom, current, end, isFinal);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (IGridCellDTO neighbor in TDaStarPathModel.api.GetNeighbors(gridDTO, current))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeGScore = gScore[current] + TDaStarPathModel.api.GetMovementCost(current, neighbor);

                if (!openSet.Contains(neighbor))
                    openSet.Add(neighbor);
                else if (tentativeGScore >= gScore[neighbor])
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + TDaStarPathModel.api.HeuristicCostEstimate(neighbor, end);
            }
        }

        if(isFinal)
            onGetFinalPath?.Invoke(null);
        else
            onGetPath?.Invoke(null, null);
    }

    private void ReconstructPath(Dictionary<IGridCellDTO, IGridCellDTO> cameFrom, 
        IGridCellDTO current, IGridCellDTO end, bool isFinal)
    {
        List<IGridCellDTO> path = new List<IGridCellDTO> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        
        if(isFinal)
            onGetFinalPath?.Invoke(path);
        else
            onGetPath?.Invoke(path, end);
    }

    private void MarkPathAsNonWalkable(IGridDTO gridDTO, List<IGridCellDTO> path)
    {
        foreach (IGridCellDTO cell in path)
        {
            cell.isWalkable = false; // Mark the path cells as non-walkable
        }
    }

    private void ResetPathAsWalkable(IGridDTO gridDTO, List<IGridCellDTO> path)
    {
        foreach (IGridCellDTO cell in path)
        {
            cell.isWalkable = true; // Reset the path cells to walkable
        }
    }

    /*public List<List<IGridCellModel>> FindMultiplePaths(IGridModel gridModel, IGridCellModel start, IGridCellModel end, int numberOfPaths)
    {
        List<List<IGridCellModel>> paths = new List<List<IGridCellModel>>();
        for (int i = 0; i < numberOfPaths; i++)
        {
            var path = FindPath(gridModel, start, end);
            if (path != null)
            {
                paths.Add(path);
                MarkPathAsNonWalkable(gridModel, path);
            }
            else
            {
                break;
            }
        }

        // Reset all paths to walkable after finding all paths
        foreach (List<IGridCellModel> path in paths)
        {
            ResetPathAsWalkable(gridModel, path);
        }

        return paths;
    }*/
}

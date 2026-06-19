using System.Collections.Generic;
using UnityEngine;

// Each wave spawns simultaneously from all groups — each group uses one of its own random corridors
public class SimultaneousStrategy : IGateAssignmentStrategy
{
    public List<(TDPathGroup, List<IGridCellDTO>)> SelectForWave(List<TDPathGroup> groups, int waveIndex)
    {
        var result = new List<(TDPathGroup, List<IGridCellDTO>)>(groups.Count);
        foreach (var group in groups)
        {
            var corridor = group.Corridors[Random.Range(0, group.Corridors.Count)];
            result.Add((group, corridor));
        }
        return result;
    }
}

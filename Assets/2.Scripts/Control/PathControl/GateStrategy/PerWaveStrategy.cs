using System.Collections.Generic;
using UnityEngine;

// Wave i maps to group i (wraps around if i >= groupCount). All enemies in a wave share the same group.
public class PerWaveStrategy : IGateAssignmentStrategy
{
    public List<(TDPathGroup, List<IGridCellDTO>)> SelectForWave(List<TDPathGroup> groups, int waveIndex)
    {
        var group    = groups[waveIndex % groups.Count];
        var corridor = group.Corridors[Random.Range(0, group.Corridors.Count)];
        return new List<(TDPathGroup, List<IGridCellDTO>)> { (group, corridor) };
    }
}

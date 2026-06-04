using System.Collections.Generic;
using UnityEngine;

// Wave i → group i (nếu i >= groupCount thì wrap). Toàn bộ enemies trong wave đi cùng 1 group.
public class PerWaveStrategy : IGateAssignmentStrategy
{
    public List<(TDPathGroup, List<IGridCellDTO>)> SelectForWave(List<TDPathGroup> groups, int waveIndex)
    {
        var group    = groups[waveIndex % groups.Count];
        var corridor = group.Corridors[Random.Range(0, group.Corridors.Count)];
        return new List<(TDPathGroup, List<IGridCellDTO>)> { (group, corridor) };
    }
}

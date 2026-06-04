using System.Collections.Generic;
using UnityEngine;

// Random group và random corridor mỗi wave
public class RandomStrategy : IGateAssignmentStrategy
{
    public List<(TDPathGroup, List<IGridCellDTO>)> SelectForWave(List<TDPathGroup> groups, int waveIndex)
    {
        var group    = groups[Random.Range(0, groups.Count)];
        var corridor = group.Corridors[Random.Range(0, group.Corridors.Count)];
        return new List<(TDPathGroup, List<IGridCellDTO>)> { (group, corridor) };
    }
}

using System.Collections.Generic;
using UnityEngine;

// Mỗi wave spawn song song từ tất cả groups — mỗi group dùng 1 random corridor của mình
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

using System.Collections.Generic;
using TDEnums;
using UnityEngine;

// Pure DTO — holds data for one start/end gate pair and all corridors belonging to it.
// Holds no View references. The View layer manages its own index-based mapping.
public class TDPathGroup
{
    public Vector2Int StartCell;
    public Vector2Int EndCell;
    public BorderSide StartBorder;
    public Vector3 SpawnWorldPos;
    public List<List<IGridCellDTO>> Corridors;
    public int PathCount;
}

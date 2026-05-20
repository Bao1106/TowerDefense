using TDEnums;
using UnityEngine;

public interface IGridCellDTO
{
    Vector2Int position { get; }
    bool isWalkable { get; set; }
    CellType type { get; set; }
}
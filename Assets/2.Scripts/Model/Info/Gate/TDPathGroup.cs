using System.Collections.Generic;
using TDEnums;
using UnityEngine;

// Pure DTO — dữ liệu của 1 cặp start/end gate và các corridors thuộc về nó.
// Không giữ bất kỳ View reference nào. View layer tự quản lý mapping theo index.
public class TDPathGroup
{
    public Vector2Int              StartCell;
    public Vector2Int              EndCell;
    public BorderSide              StartBorder;
    public Vector3                 SpawnWorldPos;
    public List<List<IGridCellDTO>> Corridors;
    public int                     PathCount;
}

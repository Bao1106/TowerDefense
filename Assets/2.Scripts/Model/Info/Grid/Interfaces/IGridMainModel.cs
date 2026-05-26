using UnityEngine;

public interface IGridMainModel
{
    Vector3 GetNearestGridPosition(Vector3 worldPosition);
    Vector3[,] GetGrid();
    void SetOccupiedCell(Vector3 position);
    bool IsValidPlacement(Vector3 position);
    int width { get; }
    int height { get; }
    float cellSize { get; }

    Vector3 CellToWorld(Vector2Int cell);
    Vector2Int WorldToCell(Vector3 worldPosition);
    bool IsInBounds(Vector2Int cell);
}
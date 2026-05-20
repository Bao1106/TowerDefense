using System.Collections.Generic;
using TDEnums;
using UnityEngine;

// ReSharper disable InconsistentNaming
public class TDaStarPathModel
{
    private static TDaStarPathModel m_api;
    public static TDaStarPathModel api
    {
        get
        {
            return m_api ??= new TDaStarPathModel();
        }
    }
    
    public float HeuristicCostEstimate(IGridCellDTO start, IGridCellDTO goal)
    {
        Vector2 startPos = start.position;
        Vector2 goalPos = goal.position;
        float dx = Mathf.Abs(startPos.x - goalPos.x);
        float dy = Mathf.Abs(startPos.y - goalPos.y);
        return (Mathf.Max(dx, dy) + Random.Range(0f, 0.1f)) * (1 + Random.Range(0f, 0.1f));
    }
    
    public float GetMovementCost(IGridCellDTO from, IGridCellDTO to)
    {
        // Chỉ cho phép di chuyển ngang hoặc dọc
        return 1.0f;
    }
    
    public List<IGridCellDTO> GetNeighbors(IGridDTO gridDTO, IGridCellDTO cellDto)
    {
        List<IGridCellDTO> neighbors = new List<IGridCellDTO>();
        int[] dx = { 0, 1, 0, -1 }; // Chỉ cho phép di chuyển lên, phải, xuống, trái
        int[] dy = { 1, 0, -1, 0 };

        for (int i = 0; i < 4; i++)
        {
            var checkX = cellDto.position.x + dx[i];
            var checkY = cellDto.position.y + dy[i];

            if (checkX >= 0 && checkX < gridDTO.width && checkY >= 0 && checkY < gridDTO.height)
            {
                var neighbor = gridDTO.GetCell(checkX, checkY);
                if (neighbor.isWalkable && neighbor.type != CellType.Obstacle)
                {
                    neighbors.Add(neighbor);
                }
            }
        }
        return neighbors;
    }
}

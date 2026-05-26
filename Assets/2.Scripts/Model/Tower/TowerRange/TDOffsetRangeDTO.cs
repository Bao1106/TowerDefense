using System.Collections.Generic;
using UnityEngine;

// Range dựa trên danh sách ô tương đối (offset) so với tower.
// Offsets được định nghĩa khi tower facing +X (Y=90°).
// Tự động rotate theo 4 hướng cardinal khi tower quay.
public class TDOffsetRangeDTO : TDRangeDTO
{
    private readonly Vector2Int[] m_Offsets;
    private readonly float        m_DetectionRadius;

    public TDOffsetRangeDTO(Vector2Int[] offsets)
    {
        m_Offsets = offsets ?? System.Array.Empty<Vector2Int>();
        float maxDist = 0f;
        foreach (var o in m_Offsets)
            maxDist = Mathf.Max(maxDist, o.magnitude);
        m_DetectionRadius = maxDist * TDConstant.CONFIG_GRID_CELL_SIZE;
    }

    public override float DetectionRadius => m_DetectionRadius;

    // O(numOffsets) — không alloc, dùng cho tick-scan mỗi frame
    public override bool IsInRange(Vector3 towerPosition, Vector3 enemyPosition, Quaternion towerRotation)
    {
        var grid       = TDGridMainModel.api;
        var towerCell  = grid.WorldToCell(towerPosition);
        var enemyCell  = grid.WorldToCell(enemyPosition);
        foreach (var offset in m_Offsets)
        {
            if (towerCell + RotateOffset(offset, towerRotation) == enemyCell)
                return true;
        }
        return false;
    }

    // Override base — trực tiếp từ offsets, không cần bounding-box scan
    public override List<Vector2Int> GetCellsInRange(Vector2Int towerCell, Quaternion towerRotation)
    {
        var grid  = TDGridMainModel.api;
        var cells = new List<Vector2Int>(m_Offsets.Length);
        foreach (var offset in m_Offsets)
        {
            var cell = towerCell + RotateOffset(offset, towerRotation);
            if (grid.IsInBounds(cell))
                cells.Add(cell);
        }
        return cells;
    }

    // Rotate offset từ local space (facing +X) sang grid space theo hướng tower.
    // Offset convention: x = forward, y = right-of-facing
    // Y=  0° (facing +Z): (dx,dy) → ( dy,  dx)
    // Y= 90° (facing +X): (dx,dy) → ( dx,  dy)  ← definition space
    // Y=180° (facing -Z): (dx,dy) → (-dy, -dx)
    // Y=270° (facing -X): (dx,dy) → (-dx, -dy)
    private static Vector2Int RotateOffset(Vector2Int offset, Quaternion rotation)
    {
        int dx    = offset.x;
        int dy    = offset.y;
        int angle = Mathf.RoundToInt(rotation.eulerAngles.y) % 360;
        if (angle < 0) angle += 360;

        return angle switch
        {
            0   => new Vector2Int( dy,  dx),
            90  => new Vector2Int( dx,  dy),
            180 => new Vector2Int(-dy, -dx),
            270 => new Vector2Int(-dx, -dy),
            _   => new Vector2Int( dx,  dy)
        };
    }
}

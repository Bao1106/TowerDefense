using System.Collections.Generic;
using UnityEngine;

public interface ITowerRangeDTO
{
    // Bán kính tối đa để scan candidates từ Registry — mỗi DTO tự report
    float DetectionRadius { get; }

    bool IsInRange(Vector3 towerPosition, Vector3 enemyPosition, Quaternion towerRotation);

    // Trả về danh sách grid cells nằm trong range pattern — dùng cho area damage
    List<Vector2Int> GetCellsInRange(Vector2Int towerCell, Quaternion towerRotation);
}
using UnityEngine;

public interface ITowerRangeDTO
{
    // Bán kính tối đa để scan candidates từ Registry — mỗi DTO tự report
    float DetectionRadius { get; }

    bool IsInRange(Vector3 towerPosition, Vector3 enemyPosition, Quaternion towerRotation);
}
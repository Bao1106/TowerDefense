using UnityEngine;

public interface ITowerRangeDTO
{
    bool IsInRange(Vector3 towerPosition, Vector3 enemyPosition, Quaternion towerRotation);
}
using UnityEngine;

public class TDVerticalRangeDTO : ITowerRangeDTO
{
    private readonly int m_Range;

    public TDVerticalRangeDTO(int getRange)
    {
        m_Range = getRange;
    }

    public float DetectionRadius => m_Range;

    public bool IsInRange(Vector3 towerPosition, Vector3 enemyPosition, Quaternion towerRotation)
    {
        // Tấn công theo trục NGANG (vuông góc với hướng nhìn) — bao phủ cả trái lẫn phải
        // Catapult đặt ở cạnh đường → bắn ngang qua địch đang đi thẳng
        Vector3 right   = towerRotation * Vector3.right;
        Vector3 toEnemy = enemyPosition - towerPosition;
        float distance  = Vector3.Distance(towerPosition, enemyPosition);
        return Mathf.Abs(Vector3.Dot(right, toEnemy.normalized)) > 0.7f && distance <= m_Range;
    }
}
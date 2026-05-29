using TDEnums;
using UnityEngine;

/// <summary>
/// Stats chung cho mọi unit có thể đặt lên map và chiến đấu — tower lẫn operator.
/// TowerData và OperatorData đều implement interface này.
/// Dùng bởi TDTowerBehaviorModel để tra cứu qua Dictionary thay vì if/else.
/// </summary>
public interface IDeployableDTO
{
    TowerType TowerType { get; }
    float Damage { get; }
    float AttackSpeed { get; }
    AttackType AttackType { get; }
    int MaxTargets { get; }
    Vector2Int[] RangeOffsets { get; }
}

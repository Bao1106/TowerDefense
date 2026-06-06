using TDEnums;
using UnityEngine;

/// <summary>
/// Strategy interface phân biệt hành vi operator theo DeployZone.
///
/// PathCell  → PathCellOperatorBehavior  (block enemy, melee attack)
/// TowerZone → TowerZoneOperatorBehavior (ranged attack, no blocking)
///
/// Factory: TDControl.CreateOperatorBehavior(DeployZone)
/// </summary>
public interface IOperatorBehavior
{
    /// Kiểm tra vị trí có hợp lệ để đặt operator không.
    bool CanPlace(Vector3 worldPos);

    /// Thực hiện đặt operator (snap, spawn, occupy).
    void Place(Vector3 worldPos, Quaternion rotation, TDTowerSlotInfo slotInfo);

    /// Gọi từ TDOperatorView.Init() — đăng ký vào registry tương ứng.
    void OnInit(Vector2Int cell, OperatorData data, TDOperatorView view);

    /// Tìm mục tiêu, lưu pending target, fire attack VFX. Trả về true để trigger anim + reset timer.
    bool TryAttack(Vector2Int cell, Vector3 worldPos, OperatorData data);

    /// Gọi từ animation event OnAttackHit — apply damage + fire impact VFX tại enemy pos.
    void ExecuteHit(Vector2Int cell, Vector3 worldPos, OperatorData data);

    /// Cleanup khi operator bị remove (die hoặc retreat).
    void OnRemove(Vector2Int cell, Vector3 worldPos);
}

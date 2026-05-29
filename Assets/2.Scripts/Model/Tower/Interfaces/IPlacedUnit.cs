using TDEnums;

/// <summary>
/// Interface chung cho mọi unit đang được đặt trên map — TDTowerWeaponView và TDOperatorView.
/// TDTowerFactoryControl dùng GetComponent<IPlacedUnit> thay vì branch riêng.
/// </summary>
public interface IPlacedUnit
{
    TowerType UnitType { get; }
    void Init(string instanceKey, TDTowerSlotInfo slotInfo);
    void OnRemove();
}

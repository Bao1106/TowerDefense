using TDEnums;

/// <summary>
/// Common interface for any unit currently placed on the map — TDTowerWeaponView and TDOperatorView.
/// TDTowerFactoryControl uses GetComponent&lt;IPlacedUnit&gt; instead of separate type branches.
/// </summary>
public interface IPlacedUnit
{
    TowerType UnitType { get; }
    void Init(string instanceKey, TDTowerSlotInfo slotInfo);
    void OnRemove();
}

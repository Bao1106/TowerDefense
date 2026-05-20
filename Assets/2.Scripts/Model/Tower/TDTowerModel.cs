using TDEnums;

public class TDTowerModel
{
    public TowerType type { get; private set; }
    public int cost { get; private set; }
    public ITowerRangeDTO TowerRangeDTO { get; private set; }
    public IWeaponBehaviorDTO WeaponBehaviorDTO { get; private set; }
    
    public TDTowerModel(TowerType towerType, int towerCost, ITowerRangeDTO towerRange, IWeaponBehaviorDTO weaponBehavior)
    {
        type = towerType;
        cost = towerCost;
        TowerRangeDTO = towerRange;
        WeaponBehaviorDTO = weaponBehavior;
    }
}

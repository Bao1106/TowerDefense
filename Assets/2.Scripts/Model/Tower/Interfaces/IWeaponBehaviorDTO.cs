using TDEnums;

public interface IWeaponBehaviorDTO
{
    float GetDamage(TowerType type);
    float GetAttackSpeed(TowerType type);
    ITowerRangeDTO GetTowerRange(TowerType type);
}
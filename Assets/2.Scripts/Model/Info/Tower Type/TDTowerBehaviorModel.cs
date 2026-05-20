using System;
using TDEnums;

public class TDTowerBehaviorModel : IWeaponBehaviorDTO
{
    private static TDTowerBehaviorModel m_api;
    public static TDTowerBehaviorModel api
    {
        get
        {
            return m_api ??= new TDTowerBehaviorModel();
        }
    }

    public TDFlyweightTowerDataSettings settings { get; set; }
    
    public float GetDamage(TowerType type)
    {
        return type switch
        {
            TowerType.Cannon => 5f,
            TowerType.Catapult => 20f,
            TowerType.MissileG02 => 10f,
            TowerType.MissileG03 => 20f,
            TowerType.Mortar => 15f,
            _ => 0
        };
    }
    
    public float GetAttackSpeed(TowerType type)
    {
        return type switch
        {
            TowerType.Cannon => 3f,
            TowerType.Catapult => 1f,
            TowerType.MissileG02 => 1f,
            TowerType.MissileG03 => 1f,
            TowerType.Mortar => 2f,
            _ => 0
        };
    }
    
    public ITowerRangeDTO GetTowerRange(TowerType type)
    {
        return type switch
        {
            TowerType.Cannon => new TDHorizontalRangeDTO(3),
            TowerType.Catapult => new TDVerticalRangeDTO(2),
            TowerType.MissileG02 => new TDHorizontalRangeDTO(5),
            TowerType.MissileG03 => new TDAreaRangeDTO(6),
            TowerType.Mortar => new TDHorizontalRangeDTO(3),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type,  $"Not expected tower type value: {type}")
        };
    }
}

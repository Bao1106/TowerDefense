using TDEnums;
using UnityEngine;

public class TDTowerBehaviorModel : IWeaponBehaviorDTO
{
    private static TDTowerBehaviorModel m_api;
    public static TDTowerBehaviorModel api
        => m_api ??= new TDTowerBehaviorModel();

    private TDFlyweightTowerDataSettings Setting
        => TDFlyweightBulletFactoryModel.api.Setting;

    public float GetDamage(TowerType type)
        => Setting.GetData(type)?.damage ?? 0f;

    public float GetAttackSpeed(TowerType type)
        => Setting.GetData(type)?.attackSpeed ?? 1f;

    public AttackType GetAttackType(TowerType type)
        => Setting.GetData(type)?.attackType ?? AttackType.Single;

    public int GetMaxTargets(TowerType type)
    {
        var data = Setting.GetData(type);
        if (data == null) return 1;
        return Mathf.Clamp(data.maxTargets, 2, 5);
    }

    public ITowerRangeDTO GetTowerRange(TowerType type)
    {
        var data = Setting.GetData(type);
        return new TDOffsetRangeDTO(data?.rangeOffsets);
    }
}

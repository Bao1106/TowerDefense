using TDEnums;

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

    public ITowerRangeDTO GetTowerRange(TowerType type)
    {
        var data = Setting.GetData(type);
        if (data == null) return new TDAreaRangeDTO(0);

        return data.rangeType switch
        {
            RangeType.Area           => new TDAreaRangeDTO(data.rangeValue),
            RangeType.HorizontalCone => new TDHorizontalRangeDTO(data.rangeValue),
            RangeType.VerticalCone   => new TDVerticalRangeDTO(data.rangeValue),
            _                        => new TDAreaRangeDTO(data.rangeValue)
        };
    }
}

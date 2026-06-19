public class TDTowerRetreatControl
{
    public static TDTowerRetreatControl api;

    public void Retreat(TDTowerWeaponView view)
    {
        if (view == null) return;
        TDGoldControl.api?.AddGold(view.Cost / 2);
        view.DoRetreat();
    }
}

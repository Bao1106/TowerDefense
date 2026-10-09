using System;

public class TDOperatorRetreatControl
{
    public static TDOperatorRetreatControl api;

    public Action<TDOperatorView> onRetreatSuccess;

    public void Retreat(TDOperatorView view)
    {
        // The refund used to be paid BEFORE asking whether the retreat could happen, and nothing
        // ever said no — so it never mattered. Now a collapsed operator refuses (§06), and paying
        // first would have turned that refusal into a gold printer: click retreat, get half the
        // cost, operator stays, click again.
        if (view == null || !view.CanRetreat) return;

        TDGoldControl.api?.AddGold(view.Cost / 2);
        view.DoRetreat();
        onRetreatSuccess?.Invoke(view);
    }
}

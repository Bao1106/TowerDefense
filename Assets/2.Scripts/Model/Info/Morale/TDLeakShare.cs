/// <summary>
/// Who pays for one leak, and how much (spec 2026-10-06 §5.2). The melee standing on the cell
/// takes LEAK_SHARE_MELEE and the ranged covering it split the rest; a collapsed melee hands
/// the whole leak to the ranged; with nobody answerable the leak is simply lost.
/// </summary>
public static class TDLeakShare
{
    public static (float melee, float eachRanged) Split(bool meleeStanding, int rangedCount)
    {
        if (rangedCount <= 0) return (meleeStanding ? 1f : 0f, 0f);
        if (!meleeStanding) return (0f, 1f / rangedCount);
        return (TDConstant.LEAK_SHARE_MELEE, (1f - TDConstant.LEAK_SHARE_MELEE) / rangedCount);
    }
}

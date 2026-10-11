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

    /// <summary>
    /// What `who` still owes for this enemy (spec Tầng 2 §4.1): the part of `weight` above the
    /// heaviest weight already charged to them by the same enemy, recorded in its ledger. One
    /// enemy walking a whole line used to charge a ranged operator once per covered cell — 7
    /// points per cell once the melee broke, 21 for a single enemy past three — so a ranged
    /// operator collapsed at full HP. Now each operator pays at most their heaviest share once.
    /// </summary>
    public static float TopUp<T>(System.Collections.Generic.Dictionary<T, float> ledger, T who, float weight)
    {
        ledger.TryGetValue(who, out float charged);
        if (weight <= charged) return 0f;
        ledger[who] = weight;
        return weight - charged;
    }
}

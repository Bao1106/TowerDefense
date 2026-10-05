using TDEnums;
using UnityEngine;

/// <summary>
/// Data for a single slot in the unit selection bar — shared by both Regular towers and Operators.
/// Built at runtime by TDTowerMainControl.BuildSlots() from both config ScriptableObjects.
/// </summary>
public struct TDTowerSlotInfo
{
    public GameObject prefab;
    public TowerType towerType;

    /// <summary>
    /// The roster row this slot was built from — the operator's IDENTITY, not a key to look one
    /// up with. Null unless towerType == Operator.
    ///
    /// This field exists because the slot used to keep only `operatorType` and throw the row
    /// away, after which five separate places called GetData(operatorType) to get it back. That
    /// lookup is Find(o => o.operatorType == type) — first match wins — and OperatorType is an
    /// archetype, so Ace and Layla both resolved to Striker and Tart resolved to Defender. Three
    /// of eight operators played entire matches on another operator's hp, damage, blockCount and
    /// Resolve, and the deploy preview drew a range the deployed unit did not have.
    ///
    /// Carrying the reference does not fix that lookup — it removes the need for it.
    /// </summary>
    public OperatorData operatorData;

    public int cost;
    public Sprite icon;
}

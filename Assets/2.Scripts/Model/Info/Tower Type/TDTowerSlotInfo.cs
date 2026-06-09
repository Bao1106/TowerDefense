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
    public OperatorType operatorType; // only meaningful when towerType == Operator
    public int cost;
    public Sprite icon;
}

using TDEnums;
using UnityEngine;

/// <summary>
/// Dữ liệu một slot trong thanh chọn unit — dùng chung cho Regular tower lẫn Operator.
/// Build runtime bởi TDTowerMainControl.BuildSlots() từ cả 2 config SO.
/// </summary>
public struct TDTowerSlotInfo
{
    public GameObject prefab;
    public TowerType towerType;
    public OperatorType operatorType; // chỉ meaningful khi towerType == Operator
    public int cost;
    public Sprite icon;
}

using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class TowerData
{
    public TowerType  type;
    [FormerlySerializedAs("prefab")]
    public GameObject bulletPrefab;
    public int        cost;
    public float      damage;
    public float      attackSpeed;
    public RangeType  rangeType;
    public int        rangeValue;
}

[CreateAssetMenu(menuName = "Game Configs/Tower Bullet Config", fileName = "Tower Bullet Config", order = 1)]
public class TDFlyweightTowerDataSettings : ScriptableObject
{
    [FormerlySerializedAs("bullets")]
    [SerializeField] private List<TowerData> towers;

    public TowerData GetData(TowerType type)
    {
        var data = towers.Find(t => t.type == type);
        if (data == null)
            Debug.LogError($"[TDFlyweightTowerDataSettings] No data found for TowerType.{type}");
        return data;
    }

    public GameObject GetPrefab(TowerType type) => GetData(type)?.bulletPrefab;

    public int GetCost(TowerType type) => GetData(type)?.cost ?? 0;
}
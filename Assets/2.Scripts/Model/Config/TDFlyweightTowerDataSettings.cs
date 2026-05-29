using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TDEnums;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class TowerData : IDeployableDTO
{
    public TowerType type;
    [FormerlySerializedAs("bulletPrefab")] [FormerlySerializedAs("prefab")]
    public GameObject towerPrefab;
    public GameObject ammoPrefab;
    public int cost;
    public float damage;
    public float attackSpeed;
    public AttackType attackType;
    public int maxTargets;
    public Vector2Int[] rangeOffsets;
    [JsonIgnore]
    public Sprite icon;

    // ── IDeployableDTO ────────────────────────────────────────────────────────
    TowerType IDeployableDTO.TowerType => type;
    float IDeployableDTO.Damage => damage;
    float IDeployableDTO.AttackSpeed => attackSpeed;
    AttackType IDeployableDTO.AttackType => attackType;
    int IDeployableDTO.MaxTargets => Mathf.Clamp(maxTargets, 2, 5);
    Vector2Int[] IDeployableDTO.RangeOffsets => rangeOffsets;
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

    public List<TowerData> GetAllTowers() => towers ?? new List<TowerData>();

    public GameObject GetAmmoPrefab(TowerType type) => GetData(type)?.ammoPrefab;

    public int GetCost(TowerType type) => GetData(type)?.cost ?? 0;
}

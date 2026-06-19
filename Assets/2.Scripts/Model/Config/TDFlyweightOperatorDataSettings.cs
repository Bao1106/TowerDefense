using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TDEnums;
using UnityEngine;

[Serializable]
public class OperatorData : IDeployableDTO
{
    [Tooltip("Specific character name (Ace, Ginger, Layla…) — used for UI display")]
    public string operatorName;

    [Tooltip("Class archetype — determines the deploy zone and block/attack style")]
    public OperatorType operatorType;

    [Tooltip("Single = 1 target, Multiple = all blocked enemies / all enemies in range")]
    public AttackType attackType = AttackType.Multiple;

    public int cost;
    public float hp;
    public float damage;
    public float attackSpeed;

    [Range(0, 3)]
    [Tooltip("Number of enemies that can be blocked simultaneously. Ranger/Mage = 0 (set automatically)")]
    public int blockCount;

    [Tooltip("Prefab for this operator")]
    public GameObject operatorPrefab;

    [Tooltip("Icon shown in the slot UI")]
    [JsonIgnore]
    public Sprite icon;

    [Tooltip("Cells within attack range when facing +X — default {(0,0)} = same cell as the operator")]
    public Vector2Int[] rangeOffsets;

    // deployZone is derived from the class archetype — no manual configuration needed
    [JsonIgnore]
    public DeployZone deployZone => operatorType is OperatorType.Ranger or OperatorType.Mage
        ? DeployZone.TowerZone
        : DeployZone.PathCell;

    // ── IDeployableDTO ────────────────────────────────────────────────────────
    TowerType IDeployableDTO.TowerType => TowerType.Operator;
    float IDeployableDTO.Damage => damage;
    float IDeployableDTO.AttackSpeed => attackSpeed;
    AttackType IDeployableDTO.AttackType => attackType;
    int IDeployableDTO.MaxTargets => deployZone == DeployZone.TowerZone ? 0 : Mathf.Clamp(blockCount, 1, 3);
    Vector2Int[] IDeployableDTO.RangeOffsets => rangeOffsets?.Length > 0
        ? rangeOffsets
        : new[] { Vector2Int.zero };
}

[CreateAssetMenu(menuName = "Game Configs/Melee Operator Config", fileName = "Melee Operator Config", order = 2)]
public class TDFlyweightOperatorDataSettings : ScriptableObject
{
    private static TDFlyweightOperatorDataSettings m_api;
    public static TDFlyweightOperatorDataSettings api
        => m_api ??= TDResourceObject.GetResource<TDFlyweightOperatorDataSettings>(TDConstant.CONFIG_OPERATOR);

    [SerializeField] private List<OperatorData> m_Operators = new List<OperatorData>();

    // ── Lookups ───────────────────────────────────────────────────────────────

    public List<OperatorData> GetAllOperators() => m_Operators ?? new List<OperatorData>();

    public OperatorData GetData(OperatorType type)
        => m_Operators?.Find(o => o.operatorType == type);

    public OperatorData GetData(int index)
    {
        if (m_Operators == null || index < 0 || index >= m_Operators.Count) return null;
        return m_Operators[index];
    }

    // ── Convenience (backward-compat) ────────────────────────────────────────

    public GameObject GetPrefab(int index = 0) => GetData(index)?.operatorPrefab;

    public Vector2Int[] GetRangeOffsets(int index = 0)
    {
        var offsets = GetData(index)?.rangeOffsets;
        return (offsets?.Length > 0) ? offsets : new[] { Vector2Int.zero };
    }
}

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TDEnums;
using UnityEngine;

[Serializable]
public class OperatorData : IDeployableDTO
{
    public OperatorType operatorType;
    public DeployZone   deployZone = DeployZone.PathCell;
    public int cost;
    public float hp;
    public float damage;
    public float attackSpeed;

    [Range(0, 3)]
    [Tooltip("Bao nhiêu enemy bị chặn cùng lúc. TowerZone operator = 0")]
    public int blockCount;

    [Tooltip("Prefab của operator này")]
    public GameObject operatorPrefab;

    [Tooltip("Icon hiển thị trong slot UI")]
    [JsonIgnore]
    public Sprite icon;

    [Tooltip("Ô trong range khi facing +X — mặc định {(0,0)} = cùng ô với operator")]
    public Vector2Int[] rangeOffsets;

    // ── IDeployableDTO ────────────────────────────────────────────────────────
    TowerType IDeployableDTO.TowerType => TowerType.Operator;
    float IDeployableDTO.Damage => damage;
    float IDeployableDTO.AttackSpeed => attackSpeed;
    AttackType IDeployableDTO.AttackType => AttackType.Multiple;
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

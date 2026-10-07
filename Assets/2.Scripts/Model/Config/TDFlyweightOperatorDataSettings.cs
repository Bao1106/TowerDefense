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

    [Tooltip("Melee: Single = 1 target, Multiple = every enemy it is blocking (1 in range when blocking none). Ranged ignores it — see splashRadius")]
    public AttackType attackType = AttackType.Single;

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

    [Min(0)]
    [Tooltip("0 = single target; N = also hits every enemy within N cells (Chebyshev) of the target")]
    public int splashRadius;

    // deployZone is derived from the class archetype — no manual configuration needed
    [JsonIgnore]
    public DeployZone deployZone => operatorType is OperatorType.Ranger or OperatorType.Mage
        ? DeployZone.TowerZone
        : DeployZone.PathCell;

    /// <summary>
    /// Endurance-to-damage ratio (§07). The input to Resolve, exposed on its own because it
    /// is the number a designer reads when asking "why is this operator stubborn?".
    ///
    /// `1 + blockCount` rather than `blockCount` so Ranger/Mage (block 0) do not zero the
    /// numerator. The denominator is floored at epsilon: a support with 0 damage would
    /// otherwise divide by zero and read as infinitely stubborn.
    /// </summary>
    [JsonIgnore]
    public float enduranceRatio
    {
        get
        {
            int cells = rangeOffsets?.Length > 0 ? rangeOffsets.Length : 1;
            float endurance = hp * (1 + Mathf.Max(0, blockCount));
            float output = Mathf.Max(0.0001f, damage * attackSpeed * cells);
            return endurance / output;
        }
    }

    /// <summary>
    /// Base Resolve as a percentage (§07): the chance LAST STAND is offered instead of the
    /// operator simply breaking. DERIVED, never authored — same rule as deployZone.
    ///
    /// Normalised against fixed bounds, not against the current roster. A relative scale
    /// would mean adding one operator silently moves everyone else's Resolve, and player
    /// knowledge that shifts under them without explanation feels like cheating.
    /// </summary>
    [JsonIgnore]
    public float baseResolve
    {
        get
        {
            float e = Mathf.Log10(Mathf.Max(0.0001f, enduranceRatio));
            float t = (e - TDConstant.RESOLVE_E_LO) / (TDConstant.RESOLVE_E_HI - TDConstant.RESOLVE_E_LO);
            return Mathf.Clamp(TDConstant.RESOLVE_BASE_MIN + TDConstant.RESOLVE_BASE_SPAN * t,
                               TDConstant.RESOLVE_BASE_MIN,
                               TDConstant.RESOLVE_BASE_MIN + TDConstant.RESOLVE_BASE_SPAN);
        }
    }

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

    // DELETED: GetData(OperatorType). It was Find(o => o.operatorType == type) — first match
    // wins — over a field that is an ARCHETYPE, not an identity. Striker, Ace and Layla all
    // declare Striker; Defender and Tart both declare Defender. So the lookup handed back the
    // wrong roster member for three of eight operators, and did it silently: they deployed with
    // someone else's hp, damage and blockCount, and the deploy preview drew a range the
    // deployed unit did not have.
    //
    // Five call sites used it, every one of them starting from a slot that had already been
    // built from the correct row and thrown it away. TDTowerSlotInfo carries the row now, so
    // there is nothing left to look up. Removed rather than fixed: a footgun that still
    // compiles is one somebody reaches for again.

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

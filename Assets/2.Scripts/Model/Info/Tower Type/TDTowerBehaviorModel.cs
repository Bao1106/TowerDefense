using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDTowerBehaviorModel : IWeaponBehaviorDTO
{
    private static TDTowerBehaviorModel m_api;
    public static TDTowerBehaviorModel api
        => m_api ??= new TDTowerBehaviorModel();

    private Dictionary<TowerType, IDeployableDTO> m_DataMap;

    // ── Lookup ────────────────────────────────────────────────────────────────

    private IDeployableDTO Get(TowerType type)
    {
        if (m_DataMap == null) BuildDataMap();
        return m_DataMap.TryGetValue(type, out var d) ? d : null;
    }

    private void BuildDataMap()
    {
        m_DataMap = new Dictionary<TowerType, IDeployableDTO>();
        var towers = TDFlyweightTowerDataSettings.api?.GetAllTowers();
        if (towers == null) return;
        foreach (var t in towers)
            m_DataMap[t.type] = t;
        // TowerType.Operator NOT included — TDOperatorView reads its SO directly
    }

    // Called on scene reload to force a full rebuild of the data map from the updated ScriptableObject
    public void InvalidateCache() => m_DataMap = null;

    // ── IWeaponBehaviorDTO ────────────────────────────────────────────────────

    public float GetDamage(TowerType type)
        => Get(type)?.Damage ?? 0f;

    public float GetAttackSpeed(TowerType type)
        => Get(type)?.AttackSpeed ?? 1f;

    public AttackType GetAttackType(TowerType type)
        => Get(type)?.AttackType ?? AttackType.Single;

    public int GetMaxTargets(TowerType type)
    {
        var d = Get(type);
        return d == null ? 1 : Mathf.Clamp(d.MaxTargets, 2, 5);
    }

    public ITowerRangeDTO GetTowerRange(TowerType type)
        => new TDOffsetRangeDTO(Get(type)?.RangeOffsets);
}

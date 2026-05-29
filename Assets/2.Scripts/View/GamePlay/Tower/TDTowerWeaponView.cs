using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDTowerWeaponView : MonoBehaviour, IPlacedUnit
{
    [SerializeField] private TowerType type;

    private ITowerRangeDTO m_TowerRangeDTO;
    private Quaternion m_OriQuaternion;
    private Transform m_PosSpawnBullet;
    private float m_LastAttackTime;
    private string m_TowerKey;
    private int m_MaxTargets;

    private float m_LastScanTime = -999f;
    private const float SCAN_INTERVAL = 0.2f;

    private readonly List<TDEnemyView> m_Targets = new List<TDEnemyView>();
    private readonly List<TDEnemyView> m_CandidateBuffer = new List<TDEnemyView>();

    public TowerType towerType => type;

    // ── IPlacedUnit ───────────────────────────────────────────────────────────

    TowerType IPlacedUnit.UnitType => type;

    void IPlacedUnit.Init(string instanceKey, TDTowerSlotInfo slotInfo)
        => Init(instanceKey);

    void IPlacedUnit.OnRemove()
    {
        if (TDTowerBehaviorMainControl.api != null)
            TDTowerBehaviorMainControl.api.onGetLastAttackTime -= OnGetLastAttackTime;
    }

    // ── Init ──────────────────────────────────────────────────────────────────

    public void Init(string key)
    {
        m_TowerKey = key;
        m_TowerRangeDTO = TDTowerBehaviorModel.api.GetTowerRange(towerType);
        m_OriQuaternion = transform.rotation;
        m_PosSpawnBullet = transform.Find(TDConstant.GAMEPLAY_TOWER_BULLET_SPAWN);

        var attackType = TDTowerBehaviorModel.api.GetAttackType(towerType);
        m_MaxTargets = attackType == AttackType.Multiple
            ? TDTowerBehaviorModel.api.GetMaxTargets(towerType)
            : 1;

        TDTowerBehaviorMainControl.api.onGetLastAttackTime += OnGetLastAttackTime;
    }

    private void OnDestroy()
    {
        if (TDTowerBehaviorMainControl.api != null)
            TDTowerBehaviorMainControl.api.onGetLastAttackTime -= OnGetLastAttackTime;
    }

    private void OnGetLastAttackTime(string key, float time)
    {
        if (!key.Equals(m_TowerKey)) return;
        m_LastAttackTime = time;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (string.IsNullOrEmpty(m_TowerKey)) return;

        if (Time.time - m_LastScanTime >= SCAN_INTERVAL)
        {
            m_LastScanTime = Time.time;
            ScanForTargets();
        }

        for (int i = m_Targets.Count - 1; i >= 0; i--)
        {
            var t = m_Targets[i];
            if (t == null || !m_TowerRangeDTO.IsInRange(transform.position, t.transform.position, m_OriQuaternion))
                m_Targets.RemoveAt(i);
        }

        if (m_Targets.Count > 0)
        {
            RotateTowardsPrimary();
            TDTowerBehaviorMainControl.api.AttackTargets(
                m_LastAttackTime, m_Targets, m_PosSpawnBullet,
                m_TowerKey, towerType, m_TowerRangeDTO, m_OriQuaternion);
        }
        else
        {
            ResetRotation();
        }
    }

    private void ScanForTargets()
    {
        m_Targets.Clear();
        m_CandidateBuffer.Clear();

        var enemies = TDEnemyRegistry.api.GetAll();
        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null) continue;
            if (!m_TowerRangeDTO.IsInRange(transform.position, enemy.transform.position, m_OriQuaternion)) continue;
            m_CandidateBuffer.Add(enemy);
        }

        m_CandidateBuffer.Sort((a, b) => b.PathProgress.CompareTo(a.PathProgress));

        for (int i = 0; i < m_CandidateBuffer.Count && m_Targets.Count < m_MaxTargets; i++)
            m_Targets.Add(m_CandidateBuffer[i]);
    }

    private void RotateTowardsPrimary()
    {
        Vector3 dir = m_Targets[0].transform.position - transform.position;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
    }

    private void ResetRotation()
    {
        transform.rotation = Quaternion.Slerp(
            transform.rotation, m_OriQuaternion, Time.deltaTime * 5f);
    }
}

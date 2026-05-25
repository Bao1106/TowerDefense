using TDEnums;
using UnityEngine;

public class TDTowerWeaponView : MonoBehaviour
{
    [SerializeField] private TowerType type;

    private ITowerRangeDTO m_TowerRangeDTO;
    private Quaternion     m_OriQuaternion;
    private Transform      m_Target, m_PosSpawnBullet;
    private float          m_LastAttackTime;
    private string         m_TowerKey;

    // Tick-based scan — tìm target mới 5 lần/giây thay vì mỗi frame
    // Không cần TDEnemyDetector hay DetectionArea child GO trên prefab
    private float          m_LastScanTime = -999f;
    private const float    k_ScanInterval = 0.2f;

    public TowerType towerType => type;

    public void Init(string key)
    {
        m_TowerKey       = key;
        m_TowerRangeDTO  = TDTowerBehaviorModel.api.GetTowerRange(towerType);
        m_OriQuaternion  = transform.rotation;
        m_PosSpawnBullet = transform.Find(TDConstant.GAMEPLAY_TOWER_BULLET_SPAWN);

        TDTowerBehaviorMainControl.api.onGetLastAttackTime += OnGetLastAttackTime;
    }

    private void OnDestroy()
    {
        TDTowerBehaviorMainControl.api.onGetLastAttackTime -= OnGetLastAttackTime;
    }

    private void OnGetLastAttackTime(string key, float time)
    {
        if (!key.Equals(m_TowerKey)) return;
        m_LastAttackTime = time;
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(m_TowerKey)) return;

        // Tick scan: tìm/cập nhật target mỗi k_ScanInterval giây
        if (Time.time - m_LastScanTime >= k_ScanInterval)
        {
            m_LastScanTime = Time.time;
            ScanForTarget();
        }

        // Attack loop: mỗi frame nếu target còn hợp lệ
        if (m_Target != null)
        {
            // Validate mỗi frame: target có thể ra khỏi range giữa 2 lần scan
            if (!m_TowerRangeDTO.IsInRange(transform.position, m_Target.position, m_OriQuaternion))
            {
                m_Target = null;
                ResetRotation();
                return;
            }

            RotateTowardsTarget();
            TDTowerBehaviorMainControl.api.AttackTarget(
                m_LastAttackTime, m_Target, m_PosSpawnBullet, m_TowerKey, towerType);
        }
        else
        {
            ResetRotation();
        }
    }

    // Chọn enemy gần GateEnd nhất (PathProgress cao nhất) trong range
    // Scale tốt với nhiều enemy: O(n) iteration, không dùng Physics
    private void ScanForTarget()
    {
        var  enemies     = TDEnemyRegistry.api.GetAll();
        TDEnemyView best = null;
        float bestProgress = -1f;

        for (int i = 0; i < enemies.Count; i++)
        {
            TDEnemyView enemy = enemies[i];

            // Guard: enemy có thể bị Destroy nhưng chưa kịp Unregister (same frame)
            if (enemy == null) continue;

            if (!m_TowerRangeDTO.IsInRange(transform.position, enemy.transform.position, m_OriQuaternion))
                continue;

            if (enemy.PathProgress > bestProgress)
            {
                bestProgress = enemy.PathProgress;
                best         = enemy;
            }
        }

        m_Target = best != null ? best.transform : null;
    }

    private void RotateTowardsTarget()
    {
        Vector3 dir = m_Target.position - transform.position;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
    }

    private void ResetRotation()
    {
        transform.rotation = Quaternion.Slerp(
            transform.rotation, m_OriQuaternion, Time.deltaTime * 5f);
    }
}
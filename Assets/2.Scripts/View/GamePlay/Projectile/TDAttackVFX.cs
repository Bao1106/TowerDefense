using System.Collections.Generic;
using TDEnums;
using UnityEngine;

// Thay thế TDBulletsView — xử lý tất cả attack types (Single/Multiple/AOE).
// Homing: track target mỗi frame; nếu target chết → bay đến vị trí cuối cùng biết được.
// AOE: khi đến nơi → GetCellsInRange → damage tất cả enemy trong cells.
public class TDAttackVFX : MonoBehaviour
{
    public TowerType OwnerType { get; set; }

    private TDEnemyView    m_Target;
    private Vector3        m_TargetPos;
    private float          m_Damage;
    private AttackType     m_AttackType;
    private ITowerRangeDTO m_RangeDTO;
    private Quaternion     m_TowerRotation;
    private bool           m_HasImpacted;

    private const float MOVE_SPEED       = 15f;
    private const float ARRIVE_THRESHOLD = 0.25f;

    public void Init(TDEnemyView target, float damage, AttackType attackType, ITowerRangeDTO rangeDTO, Quaternion towerRotation)
    {
        m_Target        = target;
        m_TargetPos     = target.transform.position;
        m_Damage        = damage;
        m_AttackType    = attackType;
        m_RangeDTO      = rangeDTO;
        m_TowerRotation = towerRotation;
        m_HasImpacted   = false;
    }

    private void Update()
    {
        if (m_HasImpacted) return;

        // Track target nếu còn sống → homing; nếu chết → giữ vị trí cuối
        if (m_Target != null)
            m_TargetPos = m_Target.transform.position;

        transform.position = Vector3.MoveTowards(
            transform.position, m_TargetPos, MOVE_SPEED * Time.deltaTime);

        Vector3 dir = m_TargetPos - transform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);

        if (Vector3.Distance(transform.position, m_TargetPos) < ARRIVE_THRESHOLD)
            OnImpact();
    }

    private void OnImpact()
    {
        m_HasImpacted = true;

        switch (m_AttackType)
        {
            case AttackType.Single:
            case AttackType.Multiple:
                m_Target?.TakeDamage(m_Damage);
                break;

            case AttackType.AOE:
                ApplyAOEDamage();
                break;
        }

        TDGameEventBus.TowerAttacked(transform.position, OwnerType);
        TDFlyweightBulletFactoryModel.ReturnToPool(this);
    }

    private void ApplyAOEDamage()
    {
        var hitCell  = TDGridMainModel.api.WorldToCell(transform.position);
        var cells    = m_RangeDTO.GetCellsInRange(hitCell, m_TowerRotation);
        var cellSet  = new HashSet<Vector2Int>(cells);

        // Copy list để tránh invalidation khi TakeDamage → Unregister modify gốc
        var enemies = new List<TDEnemyView>(TDEnemyRegistry.api.GetAll());
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            var enemyCell = TDGridMainModel.api.WorldToCell(enemy.transform.position);
            if (cellSet.Contains(enemyCell))
                enemy.TakeDamage(m_Damage);
        }
    }
}

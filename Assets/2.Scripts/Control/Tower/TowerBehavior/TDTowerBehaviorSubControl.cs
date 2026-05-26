using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDTowerBehaviorSubControl
{
    public static TDTowerBehaviorSubControl api;

    // Spawn 1 projectile per target — targets đã được ScanForTargets lọc & sort
    // Single: targets.Count == 1
    // Multiple: targets.Count == maxTargets (2–5)
    // AOE: targets.Count == 1, damage splash tại OnImpact
    public void Attack(List<TDEnemyView> targets, Transform spawnProjectile,
                       TowerType type, ITowerRangeDTO rangeDTO, Quaternion towerRotation)
    {
        if (targets == null || targets.Count == 0) return;

        float      damage     = TDTowerBehaviorModel.api.GetDamage(type);
        AttackType attackType = TDTowerBehaviorModel.api.GetAttackType(type);

        foreach (var target in targets)
        {
            if (target == null) continue;

            TDAttackVFX vfx = TDFlyweightBulletFactoryModel.Spawn(type);
            if (vfx == null) continue;

            vfx.transform.position = spawnProjectile.position;
            vfx.Init(target, damage, attackType, rangeDTO, towerRotation);
        }
    }
}

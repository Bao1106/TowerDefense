using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

public class TDTowerBehaviorMainControl
{
    public static TDTowerBehaviorMainControl api;

    public Action<string, float> onGetLastAttackTime;

    public void AttackTargets(float lastAttackTime, List<TDEnemyView> targets,
                              Transform spawnProjectile, string key, TowerType type,
                              ITowerRangeDTO rangeDTO, Quaternion towerRotation)
    {
        if (targets == null || targets.Count == 0) return;
        if (Time.time - lastAttackTime < 1f / TDTowerBehaviorModel.api.GetAttackSpeed(type)) return;

        TDTowerBehaviorSubControl.api.Attack(targets, spawnProjectile, type, rangeDTO, towerRotation);
        onGetLastAttackTime?.Invoke(key, Time.time);
    }
}

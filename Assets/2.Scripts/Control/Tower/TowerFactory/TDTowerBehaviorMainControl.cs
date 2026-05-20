using System;
using TDEnums;
using UnityEngine;

public class TDTowerBehaviorMainControl
{
    public static TDTowerBehaviorMainControl api;
    
    public Action<string, float> onGetLastAttackTime;
    
    public void AttackTarget(float lastAttackTime, Transform target, Transform projectile, string key, TowerType type)
    {
        if (Time.time - lastAttackTime >= 1f / TDTowerBehaviorModel.api.GetAttackSpeed(type))
        {
            TDTowerBehaviorSubControl.api.Attack(target, projectile, type);
            lastAttackTime = Time.time;
            onGetLastAttackTime?.Invoke(key, lastAttackTime);
        }
    }
}

using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

[Serializable]
public class EnemyData
{
    public EnemyType type;
    public float baseHP;
    public float baseSpeed;
    public float dieDuration; // seconds to wait for the Die animation before returning to pool
    public int goldReward; // gold awarded to the player when this enemy type is killed
    public float baseAttackDamage; // damage dealt per hit when attacking an operator
    public float baseAttackSpeed; // hits per second while blocked (0 = does not attack)
    public GameObject prefab;
}

[CreateAssetMenu(menuName = "Game Configs/Enemy Data Config", fileName = "Enemy Data Config", order = 2)]
public class TDFlyweightEnemyDataSettings : ScriptableObject
{
    private static TDFlyweightEnemyDataSettings m_api;
    public static TDFlyweightEnemyDataSettings api
        => m_api ??= TDResourceObject.GetResource<TDFlyweightEnemyDataSettings>(TDConstant.CONFIG_ENEMY);
    [SerializeField] private List<EnemyData> enemies;

    public EnemyData GetData(EnemyType type)
    {
        var data = enemies.Find(e => e.type == type);
        if (data == null)
            Debug.LogError($"[TDEnemyDataSettings] No data for EnemyType.{type}");
        return data;
    }
}

using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;

[Serializable]
public class EnemyData
{
    public EnemyType  type;
    public float      baseHP;
    public float      baseSpeed;
    public float      dieDuration;        // giây chờ animation Die trước khi return pool
    public int        goldReward;         // gold thưởng khi kill enemy loại này
    public float      baseAttackDamage;   // damage mỗi đòn khi tấn công operator
    public float      baseAttackSpeed;    // số đòn/giây khi bị chặn (0 = không attack)
    public GameObject prefab;
}

[CreateAssetMenu(menuName = "Game Configs/Enemy Data Config", fileName = "Enemy Data Config", order = 2)]
public class TDFlyweightEnemyDataSettings : ScriptableObject
{
    [SerializeField] private List<EnemyData> enemies;

    public EnemyData GetData(EnemyType type)
    {
        var data = enemies.Find(e => e.type == type);
        if (data == null)
            Debug.LogError($"[TDEnemyDataSettings] No data for EnemyType.{type}");
        return data;
    }
}

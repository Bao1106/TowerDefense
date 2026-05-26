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
    public GameObject prefab;
}

[CreateAssetMenu(menuName = "Game Configs/Enemy Data Config", fileName = "Enemy Data Config", order = 2)]
public class TDEnemyDataSettings : ScriptableObject
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

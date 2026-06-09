using System.Collections.Generic;
using TDEnums;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

public class TDFlyweightBulletFactoryModel
{
    private static TDFlyweightBulletFactoryModel m_api;
    public static TDFlyweightBulletFactoryModel api
        => m_api ??= new TDFlyweightBulletFactoryModel();

    private TDFlyweightTowerDataSettings m_Setting;
    private readonly bool m_CollectionCheck = true;
    private readonly int  m_DefaultCapacity = 10;
    private readonly int  m_MaxCapacity     = 100;

    // 1 pool per TowerType — lazily created the first time a shot is fired
    private readonly Dictionary<TowerType, IObjectPool<TDAttackVFX>> m_Pools
        = new Dictionary<TowerType, IObjectPool<TDAttackVFX>>();

    public TDFlyweightTowerDataSettings Setting
        => m_Setting ??= TDResourceObject.GetResource<TDFlyweightTowerDataSettings>(TDConstant.CONFIG_TOWER);

    // ── Public API ────────────────────────────────────────────────────────────

    public static TDAttackVFX Spawn(TowerType type)
    {
        var pool = api.GetPoolFor(type);
        if (pool == null) return null;

        TDAttackVFX vfx = pool.Get();
        vfx.OwnerType = type;
        return vfx;
    }

    public static void ReturnToPool(TDAttackVFX vfx)
    {
        if (vfx == null) return;
        api.GetPoolFor(vfx.OwnerType)?.Release(vfx);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private IObjectPool<TDAttackVFX> GetPoolFor(TowerType type)
    {
        if (m_Pools.TryGetValue(type, out var pool))
            return pool;

        GameObject prefab = Setting.GetAmmoPrefab(type);

        if (prefab == null)
        {
            Debug.LogError($"[TDFlyweightBulletFactoryModel] No prefab for TowerType.{type}");
            return null;
        }

        pool = new ObjectPool<TDAttackVFX>(
            createFunc:      () =>
            {
                var go = Object.Instantiate(prefab);
                // Ensure the prefab has a TDAttackVFX component — adds one automatically if missing
                var vfx = go.GetComponent<TDAttackVFX>() ?? go.AddComponent<TDAttackVFX>();
                return vfx;
            },
            actionOnGet:     v => v.gameObject.SetActive(true),
            actionOnRelease: v => v.gameObject.SetActive(false),
            actionOnDestroy: v => Object.Destroy(v.gameObject),
            collectionCheck: m_CollectionCheck,
            defaultCapacity: m_DefaultCapacity,
            maxSize:         m_MaxCapacity
        );

        m_Pools.Add(type, pool);
        return pool;
    }
}

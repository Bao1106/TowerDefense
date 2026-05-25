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

    // 1 pool per TowerType — lazy-created khi bắn lần đầu
    // Không có global m_TowerType state — pool lookup hoàn toàn stateless
    private readonly Dictionary<TowerType, IObjectPool<TDBulletsView>> m_Pools
        = new Dictionary<TowerType, IObjectPool<TDBulletsView>>();

    public TDFlyweightTowerDataSettings Setting
        => m_Setting ??= RepResourceObject.GetResource<TDFlyweightTowerDataSettings>(TDConstant.CONFIG_TOWER);

    // ── Public API ────────────────────────────────────────────────────────────

    // Spawn bullet đúng type, ghi OwnerType để ReturnToPool biết trả về pool nào
    public static TDBulletsView Spawn(TowerType type)
    {
        var pool = api.GetPoolFor(type);
        if (pool == null) return null;

        TDBulletsView bullet = pool.Get();
        bullet.OwnerType = type;
        return bullet;
    }

    // Return về đúng pool dựa vào OwnerType đã ghi lúc Spawn
    public static void ReturnToPool(TDBulletsView bullet)
    {
        if (bullet == null) return;
        api.GetPoolFor(bullet.OwnerType)?.Release(bullet);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    // Lazy-create pool cho type — closure captures đúng prefab, không bị cross-contaminate
    private IObjectPool<TDBulletsView> GetPoolFor(TowerType type)
    {
        if (m_Pools.TryGetValue(type, out var pool))
            return pool;

        GameObject prefab = Setting.GetPrefab(type);
        if (prefab == null)
        {
            Debug.LogError($"[TDFlyweightBulletFactoryModel] Cannot create pool: no prefab for TowerType.{type}");
            return null;
        }

        // Closure capture 'prefab' tại thời điểm tạo pool → đúng prefab cho type này mãi mãi
        pool = new ObjectPool<TDBulletsView>(
            createFunc:      () => Object.Instantiate(prefab).GetComponent<TDBulletsView>(),
            actionOnGet:     b  => b.gameObject.SetActive(true),
            actionOnRelease: b  => b.gameObject.SetActive(false),
            actionOnDestroy: b  => Object.Destroy(b.gameObject),
            collectionCheck: m_CollectionCheck,
            defaultCapacity: m_DefaultCapacity,
            maxSize:         m_MaxCapacity
        );

        m_Pools.Add(type, pool);
        return pool;
    }
}
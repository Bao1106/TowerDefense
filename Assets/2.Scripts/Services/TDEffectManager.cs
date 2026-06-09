using System.Collections.Generic;
using DG.Tweening;
using TDEnums;
using UnityEngine;

/// <summary>
/// Static effect system — no MonoBehaviour on the scene.
/// Loads the TDEffectConfig ScriptableObject from Resources and maps GameEventKey → EffectDef.
/// Init() is called from TDControl.InitOtherControl() after TDEnemyPathMainControl has finished setup.
/// </summary>
public static class TDEffectManager
{
    private static readonly Dictionary<GameEventKey, EffectDef> s_Effects  = new();
    private static readonly Dictionary<GameObject, Queue<GameObject>> s_Pools = new();
    private static readonly List<Vector3> s_GatePositions = new();
    private static Transform s_Root;

    // ── SFX Pool (P1 + P2 fix) ───────────────────────────────────────────────
    private const int   SFX_POOL_SIZE    = 8;
    private const float SFX_MIN_INTERVAL = 0.05f;   // per-key rate limit
    private static AudioSource[]                     s_SfxPool    = new AudioSource[SFX_POOL_SIZE];
    private static readonly Dictionary<GameEventKey, float> s_LastSfxTime = new();

    // ── Init / Cleanup ────────────────────────────────────────────────────────

    public static void Init()
    {
        Cleanup();

        var config = TDEffectConfig.api;
        if (config == null)
        {
            Debug.LogWarning($"[TDEffectManager] SO not found at Resources/{TDConstant.CONFIG_EFFECT}");
            return;
        }

        foreach (var def in config.GetAll())
        {
            if (!s_Effects.ContainsKey(def.key))
                s_Effects[def.key] = def;
            else
                Debug.LogWarning($"[TDEffectManager] Duplicate key: {def.key} — only the first entry will be used.");
        }

        TDGameEventBus.OnEnemyDied        += OnEnemyDied;
        TDGameEventBus.OnOperatorAttacked += OnOperatorAttacked;
        TDGameEventBus.OnOperatorImpacted += OnOperatorImpacted;
        TDGameEventBus.OnTowerAttacked    += OnTowerAttacked;
        TDGameEventBus.OnLifeLost         += OnLifeLost;
        TDGameEventBus.OnWaveStarted      += OnWaveStarted;
        TDGameEventBus.OnVictory          += OnVictory;
        TDGameEventBus.OnGameOver         += OnGameOver;
        TDGameEventBus.OnUnitPickup       += OnUnitPickup;
        TDGameEventBus.OnTowerPlaced      += OnTowerPlaced;

        if (TDEnemyPathMainControl.api != null)
            TDEnemyPathMainControl.api.onGroupsReady += CacheGatePositions;

        InitSfxPool();
    }

    public static void Cleanup()
    {
        TDGameEventBus.OnEnemyDied        -= OnEnemyDied;
        TDGameEventBus.OnOperatorAttacked -= OnOperatorAttacked;
        TDGameEventBus.OnOperatorImpacted -= OnOperatorImpacted;
        TDGameEventBus.OnTowerAttacked    -= OnTowerAttacked;
        TDGameEventBus.OnLifeLost         -= OnLifeLost;
        TDGameEventBus.OnWaveStarted      -= OnWaveStarted;
        TDGameEventBus.OnVictory          -= OnVictory;
        TDGameEventBus.OnGameOver         -= OnGameOver;
        TDGameEventBus.OnUnitPickup       -= OnUnitPickup;
        TDGameEventBus.OnTowerPlaced      -= OnTowerPlaced;

        if (TDEnemyPathMainControl.api != null)
            TDEnemyPathMainControl.api.onGroupsReady -= CacheGatePositions;

        if (s_Root != null) { Object.Destroy(s_Root.gameObject); s_Root = null; }
        s_Pools.Clear();
        s_GatePositions.Clear();
        s_Effects.Clear();
        s_LastSfxTime.Clear();
        s_SfxPool = new AudioSource[SFX_POOL_SIZE]; // refs destroyed with s_Root
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private static void OnEnemyDied(Vector3 pos, EnemyType type)
        => Play(type switch
        {
            EnemyType.Fast => GameEventKey.EnemyDied_Fast,
            EnemyType.Tank => GameEventKey.EnemyDied_Tank,
            EnemyType.Boss => GameEventKey.EnemyDied_Boss,
            _              => GameEventKey.EnemyDied_Normal,
        }, pos);

    private static void OnOperatorAttacked(Vector3 pos, OperatorType type)
        => Play(type switch
        {
            OperatorType.Defender => GameEventKey.OperatorAttacked_Defender,
            OperatorType.Striker  => GameEventKey.OperatorAttacked_Striker,
            OperatorType.Ranger   => GameEventKey.OperatorAttacked_Ranger,
            OperatorType.Mage     => GameEventKey.OperatorAttacked_Mage,
            _                     => GameEventKey.OperatorAttacked_Knight,
        }, pos);

    private static void OnOperatorImpacted(Vector3 impactPos, OperatorType type)
        => PlayImpact(type switch
        {
            OperatorType.Defender => GameEventKey.OperatorAttacked_Defender,
            OperatorType.Striker  => GameEventKey.OperatorAttacked_Striker,
            OperatorType.Ranger   => GameEventKey.OperatorAttacked_Ranger,
            OperatorType.Mage     => GameEventKey.OperatorAttacked_Mage,
            _                     => GameEventKey.OperatorAttacked_Knight,
        }, impactPos);

    private static void OnTowerAttacked(Vector3 pos, TowerType type)
    {
        if (type == TowerType.Operator) return;
        Play(GameEventKey.TowerAttacked, pos);
    }

    private static void OnLifeLost(Vector3 pos) => Play(GameEventKey.LifeLost, pos);
    private static void OnVictory()              => Play(GameEventKey.Victory, GetSceneCenter());
    private static void OnGameOver()             => Play(GameEventKey.GameOver, GetSceneCenter());
    private static void OnUnitPickup()           => PlaySfxOnly(GameEventKey.UnitPickup);
    private static void OnTowerPlaced()          => PlaySfxOnly(GameEventKey.TowerPlaced);

    private static void OnWaveStarted(int _)
    {
        foreach (var gatePos in s_GatePositions)
            Play(GameEventKey.WaveStarted, gatePos);
    }

    // ── Core play ─────────────────────────────────────────────────────────────

    private static void Play(GameEventKey key, Vector3 pos)
    {
        if (!s_Effects.TryGetValue(key, out var def)) return;

        SpawnVFX(def.vfxPrefab,  pos);
        SpawnVFX(def.vfxPrefab2, pos);
        PlaySFX(key, def);

        if (def.cameraShake)
            Camera.main?.transform
                .DOShakePosition(def.shakeDuration, def.shakeStrength, 10, 90f, false)
                .SetUpdate(true);
    }

    // UI events (pickup, place) — SFX only, no VFX, no camera shake
    private static void PlaySfxOnly(GameEventKey key)
    {
        if (!s_Effects.TryGetValue(key, out var def)) return;
        PlaySFX(key, def);
    }

    // Spawns only the impactVfxPrefab at the enemy's position (does not replay the attack VFX or SFX)
    private static void PlayImpact(GameEventKey key, Vector3 impactPos)
    {
        if (!s_Effects.TryGetValue(key, out var def)) return;
        SpawnVFX(def.impactVfxPrefab, impactPos);
    }

    // ── VFX pool ──────────────────────────────────────────────────────────────

    private static void SpawnVFX(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;

        if (!s_Pools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            s_Pools[prefab] = pool;
        }

        var instance = pool.Count > 0 ? pool.Dequeue() : Object.Instantiate(prefab, GetRoot());
        instance.transform.position = pos;
        instance.SetActive(true);

        float duration = 2f;
        if (instance.TryGetComponent<ParticleSystem>(out var ps))
        {
            var main = ps.main;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
            duration = main.duration + main.startLifetime.constantMax;
        }

        DOVirtual.DelayedCall(duration, () =>
        {
            if (instance == null) return;
            instance.SetActive(false);
            if (s_Pools.TryGetValue(prefab, out var p)) p.Enqueue(instance);
        });
    }

    // ── SFX pool ──────────────────────────────────────────────────────────────

    private static void InitSfxPool()
    {
        var root = GetRoot();
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var go  = new GameObject($"SFX_{i}");
            go.transform.SetParent(root);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 0f; // 2D audio — camera is fixed, spatial blending is not needed
            s_SfxPool[i]     = src;
        }
    }

    /// <summary>
    /// P1: reuses a pooled AudioSource instead of creating a new GameObject each time.
    /// P2: applies a per-key cooldown of SFX_MIN_INTERVAL to prevent sound stacking.
    /// </summary>
    private static void PlaySFX(GameEventKey key, EffectDef def)
    {
        if (def.sfxClip == null) return;

        // P2 — rate limit per key
        float now = Time.time;
        if (s_LastSfxTime.TryGetValue(key, out float last) && now - last < SFX_MIN_INTERVAL)
            return;
        s_LastSfxTime[key] = now;

        // P1 — find free AudioSource in pool
        for (int i = 0; i < SFX_POOL_SIZE; i++)
        {
            var src = s_SfxPool[i];
            if (src == null || src.isPlaying) continue;

            src.clip   = def.sfxClip;
            src.volume = def.sfxVolume;
            src.Play();
            return;
        }
        // pool exhausted — skip rather than allocate (P2 side effect: no sound spam)
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void CacheGatePositions(List<TDPathGroup> groups)
    {
        s_GatePositions.Clear();
        foreach (var g in groups) s_GatePositions.Add(g.SpawnWorldPos);
    }

    private static Transform GetRoot()
    {
        if (s_Root == null) s_Root = new GameObject("[Effect Pool]").transform;
        return s_Root;
    }

    private static Vector3 GetSceneCenter()
    {
        var cam = Camera.main;
        return cam != null ? cam.transform.position + cam.transform.forward * 6f : Vector3.zero;
    }
}

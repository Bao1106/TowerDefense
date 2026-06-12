using System.Collections.Generic;
using DG.Tweening;
using TDEnums;
using UnityEngine;

/// VFX + camera shake system — SFX delegated to TDAudioService.SFX.
/// Init() is called from TDControl.InitOtherControl().
public static class TDEffectManager
{    // Cached main camera (Camera.main does a tag search per call); re-resolved after scene change    private static Camera s_MainCam;    private static Camera MainCam => s_MainCam != null ? s_MainCam : (s_MainCam = Camera.main);
    private static readonly Dictionary<GameEventKey, EffectDef> s_Effects = new();
    private static readonly Dictionary<GameObject, Queue<GameObject>> s_Pools = new();
    private static readonly List<Vector3> s_GatePositions = new();
    private static Transform s_Root;

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

        TDGameEventBus.OnEnemyDied += OnEnemyDied;
        TDGameEventBus.OnOperatorAttacked += OnOperatorAttacked;
        TDGameEventBus.OnOperatorImpacted += OnOperatorImpacted;
        TDGameEventBus.OnTowerAttacked += OnTowerAttacked;
        TDGameEventBus.OnLifeLost += OnLifeLost;
        TDGameEventBus.OnWaveStarted += OnWaveStarted;
        TDGameEventBus.OnVictory += OnVictory;
        TDGameEventBus.OnGameOver += OnGameOver;
        TDGameEventBus.OnUnitPickup += OnUnitPickup;
        TDGameEventBus.OnTowerPlaced += OnTowerPlaced;

        if (TDEnemyPathMainControl.api != null)
            TDEnemyPathMainControl.api.onGroupsReady += CacheGatePositions;
    }

    public static void Cleanup()
    {
        TDGameEventBus.OnEnemyDied -= OnEnemyDied;
        TDGameEventBus.OnOperatorAttacked -= OnOperatorAttacked;
        TDGameEventBus.OnOperatorImpacted -= OnOperatorImpacted;
        TDGameEventBus.OnTowerAttacked -= OnTowerAttacked;
        TDGameEventBus.OnLifeLost -= OnLifeLost;
        TDGameEventBus.OnWaveStarted -= OnWaveStarted;
        TDGameEventBus.OnVictory -= OnVictory;
        TDGameEventBus.OnGameOver -= OnGameOver;
        TDGameEventBus.OnUnitPickup -= OnUnitPickup;
        TDGameEventBus.OnTowerPlaced -= OnTowerPlaced;

        if (TDEnemyPathMainControl.api != null)
            TDEnemyPathMainControl.api.onGroupsReady -= CacheGatePositions;

        if (s_Root != null) { Object.Destroy(s_Root.gameObject); s_Root = null; }
        s_Pools.Clear();
        s_GatePositions.Clear();
        s_Effects.Clear();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private static void OnEnemyDied(Vector3 pos, EnemyType type)
        => Play(type switch
        {
            EnemyType.Fast => GameEventKey.EnemyDied_Fast,
            EnemyType.Tank => GameEventKey.EnemyDied_Tank,
            EnemyType.Boss => GameEventKey.EnemyDied_Boss,
            _ => GameEventKey.EnemyDied_Normal,
        }, pos);

    private static void OnOperatorAttacked(Vector3 pos, OperatorType type)
        => Play(type switch
        {
            OperatorType.Defender => GameEventKey.OperatorAttacked_Defender,
            OperatorType.Striker => GameEventKey.OperatorAttacked_Striker,
            OperatorType.Ranger => GameEventKey.OperatorAttacked_Ranger,
            OperatorType.Mage => GameEventKey.OperatorAttacked_Mage,
            _ => GameEventKey.OperatorAttacked_Knight,
        }, pos);

    private static void OnOperatorImpacted(Vector3 impactPos, OperatorType type)
        => PlayImpact(type switch
        {
            OperatorType.Defender => GameEventKey.OperatorAttacked_Defender,
            OperatorType.Striker => GameEventKey.OperatorAttacked_Striker,
            OperatorType.Ranger => GameEventKey.OperatorAttacked_Ranger,
            OperatorType.Mage => GameEventKey.OperatorAttacked_Mage,
            _ => GameEventKey.OperatorAttacked_Knight,
        }, impactPos);

    private static void OnTowerAttacked(Vector3 pos, TowerType type)
    {
        if (type == TowerType.Operator) return;
        Play(GameEventKey.TowerAttacked, pos);
    }

    private static void OnLifeLost(Vector3 pos) => Play(GameEventKey.LifeLost, pos);
    private static void OnVictory() => Play(GameEventKey.Victory, GetSceneCenter());
    private static void OnGameOver() => Play(GameEventKey.GameOver, GetSceneCenter());
    private static void OnUnitPickup() => PlaySfxOnly(GameEventKey.UnitPickup);
    private static void OnTowerPlaced() => PlaySfxOnly(GameEventKey.TowerPlaced);

    private static void OnWaveStarted(int _)
    {
        foreach (var gatePos in s_GatePositions)
            Play(GameEventKey.WaveStarted, gatePos);
    }

    // ── Core play ─────────────────────────────────────────────────────────────

    private static void Play(GameEventKey key, Vector3 pos)
    {
        if (!s_Effects.TryGetValue(key, out var def)) return;

        SpawnVFX(def.vfxPrefab, pos);
        SpawnVFX(def.vfxPrefab2, pos);
        TDAudioService.SFX.Play(def.sfxClip, def.sfxVolume);

        if (def.cameraShake)
            MainCam?.transform
                .DOShakePosition(def.shakeDuration, def.shakeStrength, 10, 90f, false)
                .SetUpdate(true);
    }

    private static void PlaySfxOnly(GameEventKey key)
    {
        if (!s_Effects.TryGetValue(key, out var def)) return;
        TDAudioService.SFX.Play(def.sfxClip, def.sfxVolume);
    }

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
        var cam = MainCam;
        return cam != null ? cam.transform.position + cam.transform.forward * 6f : Vector3.zero;
    }
}

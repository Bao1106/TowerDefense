using System;
using TDEnums;
using UnityEngine;

/// <summary>
/// Centralized event bus for cross-cutting concerns: VFX, Audio, and camera shake.
/// Gameplay code raises events — audio/VFX systems subscribe independently with no reverse coupling.
/// </summary>
public static class TDGameEventBus
{
    // ── Enemy ─────────────────────────────────────────────────────────────────
    public static event Action<Vector3, EnemyType> OnEnemyDied;
    public static event Action<Vector3, EnemyType> OnEnemySpawned;

    // ── Tower / Operator ──────────────────────────────────────────────────────
    public static event Action<Vector3, TowerType> OnTowerAttacked;
    public static event Action<Vector3, OperatorType> OnOperatorAttacked;
    public static event Action<Vector3, OperatorType> OnOperatorImpacted;
    public static event Action<Vector2Int> OnOperatorDied;

    // ── Player ────────────────────────────────────────────────────────────────
    public static event Action<Vector3> OnLifeLost;
    public static event Action OnVictory;
    public static event Action OnGameOver;

    // ── Wave ──────────────────────────────────────────────────────────────────
    public static event Action<int> OnWaveStarted;

    // ── UI interactions ───────────────────────────────────────────────────────
    public static event Action OnUnitPickup;
    public static event Action OnTowerPlaced;
    // Deploy gesture transitioned from Phase 1 (Dragging) to Phase 2 (DirectionSelect)
    // — fired the moment the player releases on a valid cell and the diamond direction
    // picker appears. Tutorial system uses this to show the "drag direction" hint.
    public static event Action OnDeployDrop;

    // ── Gameplay lifecycle ────────────────────────────────────────────────────
    public static event Action OnGameplayStarted;

    // ── Raise helpers ─────────────────────────────────────────────────────────

    public static void EnemyDied(Vector3 pos, EnemyType type)
        => OnEnemyDied?.Invoke(pos, type);

    public static void EnemySpawned(Vector3 pos, EnemyType type)
        => OnEnemySpawned?.Invoke(pos, type);

    public static void TowerAttacked(Vector3 pos, TowerType type)
        => OnTowerAttacked?.Invoke(pos, type);

    public static void OperatorAttacked(Vector3 pos, OperatorType type)
        => OnOperatorAttacked?.Invoke(pos, type);

    public static void OperatorImpacted(Vector3 pos, OperatorType type)
        => OnOperatorImpacted?.Invoke(pos, type);

    public static void OperatorDied(Vector2Int cell)
        => OnOperatorDied?.Invoke(cell);

    public static void LifeLost(Vector3 pos)
        => OnLifeLost?.Invoke(pos);

    public static void Victory()
        => OnVictory?.Invoke();

    public static void GameOver()
        => OnGameOver?.Invoke();

    public static void WaveStarted(int waveIndex)
        => OnWaveStarted?.Invoke(waveIndex);

    public static void GameplayStarted()
        => OnGameplayStarted?.Invoke();

    public static void UnitPickup()
        => OnUnitPickup?.Invoke();

    public static void TowerPlaced()
        => OnTowerPlaced?.Invoke();

    public static void DeployDrop()
        => OnDeployDrop?.Invoke();
}

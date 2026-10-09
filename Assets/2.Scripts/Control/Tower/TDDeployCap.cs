using System;
using UnityEngine;

/// <summary>
/// The per-level deploy cap (spec 2026-10-06 §5.1): one hard limit on units standing on the field,
/// shared by melee, ranged and turrets. A collapsed operator still holds their place — the cap is
/// what turns rotating a tired operator out into a decision instead of a free action.
///
/// Counted at the two ends every unit passes through: the factory (placed) and IPlacedUnit.OnRemove
/// (retreated, sold or killed), so no placement path can slip past it.
/// </summary>
public sealed class TDDeployCap
{
    public static TDDeployCap api;

    /// <summary>OnField or Limit changed.</summary>
    public event Action OnChanged;

    /// <summary>A placement was refused because the cap is full.</summary>
    public event Action OnRejected;

    public int Limit { get; private set; } = TDConstant.CONFIG_MAX_SLOTS; // until Initialize
    public int OnField { get; private set; }
    public bool IsFull => OnField >= Limit;

    /// <summary>A new match. Resets the count — units from the last attempt are gone with its scene.</summary>
    public void Initialize(int limit)
    {
        if (limit <= 0)
        {
            Debug.LogWarning($"[TDDeployCap] deploy limit {limit} is not usable, falling back to {TDConstant.CONFIG_MAX_SLOTS}");
            limit = TDConstant.CONFIG_MAX_SLOTS;
        }

        Limit = limit;
        OnField = 0;
        OnChanged?.Invoke();
    }

    public void OnUnitPlaced()
    {
        OnField++;
        OnChanged?.Invoke();
    }

    public void OnUnitRemoved()
    {
        OnField = Mathf.Max(0, OnField - 1);
        OnChanged?.Invoke();
    }

    public void NotifyRejected() => OnRejected?.Invoke();

    // Level owns capacity, difficulty nudges it (spec §5.7). Never below 1: a match nobody can deploy into is not a match.
    public static int LimitFor(LevelConfig level) => level != null
        ? Mathf.Max(1, level.deployLimit + DifficultyRatioTable.Get(level.difficulty).deployLimitDelta)
        : TDConstant.CONFIG_MAX_SLOTS;
}

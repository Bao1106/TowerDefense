using System;
using System.Collections.Generic;
using TDEnums;
using UnityEngine;
using Random = UnityEngine.Random;

public class TDTowerMainControl
{
    public static TDTowerMainControl api;

    public Action<TDTowerSlotInfo> onGetTowerPrefab;
    public Action<int> onGetCurrentRotationIndex;

    private readonly List<TDTowerSlotInfo> m_ActiveSlots = new List<TDTowerSlotInfo>();
    private TDTowerSlotInfo m_CurrentSlot;

    public IReadOnlyList<TDTowerSlotInfo> ActiveSlots => m_ActiveSlots;

    // ── Slot Building ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fills the deploy bar with a FIXED melee/ranged composition (5/3), not a random draw.
    ///
    /// It used to shuffle towers and operators into one pool and take the first 8, so the split
    /// changed every match. That is not variety, it is an unreliable contract: melee stands on the
    /// path and holds the line, ranged stands beside it and cannot, so a bar that some matches
    /// offers two melee is a bar the player cannot plan a defence around before seeing it.
    /// </summary>
    public void BuildSlots()
    {
        m_ActiveSlots.Clear();

        var melee = new List<TDTowerSlotInfo>();
        var ranged = new List<TDTowerSlotInfo>();

        // Turrets are ranged by definition — they only ever go on tower zones.
        var towerSetting = TDFlyweightTowerDataSettings.api;
        if (towerSetting != null)
        {
            foreach (var data in towerSetting.GetAllTowers())
            {
                if (data.towerPrefab == null) continue;
                ranged.Add(new TDTowerSlotInfo
                {
                    prefab = data.towerPrefab,
                    towerType = data.type,
                    cost = data.cost,
                    icon = data.icon
                });
            }
        }

        var opSetting = TDFlyweightOperatorDataSettings.api;
        if (opSetting != null)
        {
            foreach (var op in opSetting.GetAllOperators())
            {
                if (op.operatorPrefab == null) continue;
                var slot = new TDTowerSlotInfo
                {
                    prefab = op.operatorPrefab,
                    towerType = TowerType.Operator,
                    operatorData = op,
                    cost = op.cost,
                    icon = op.icon
                };

                // The split is by DEPLOY ZONE, so Ginger and Moon count as ranged alongside the
                // turrets — they occupy the same cells and solve the same half of the problem.
                if (op.deployZone == DeployZone.PathCell) melee.Add(slot);
                else ranged.Add(slot);
            }
        }

        Shuffle(melee);
        Shuffle(ranged);

        int meleeTaken = TakeInto(melee, TDConstant.CONFIG_SLOTS_MELEE);
        int rangedTaken = TakeInto(ranged, TDConstant.CONFIG_SLOTS_RANGED);

        // If either pool came up short, fill from whatever is left rather than shipping a bar
        // with holes in it — a missing card reads as a bug, an off-ratio bar reads as a roster.
        int shortfall = TDConstant.CONFIG_MAX_SLOTS - m_ActiveSlots.Count;
        if (shortfall > 0)
        {
            shortfall -= TakeInto(melee, shortfall);
            if (shortfall > 0) TakeInto(ranged, shortfall);

            Debug.LogWarning($"<color=orange>[TDTowerMainControl] BuildSlots wanted " +
                             $"{TDConstant.CONFIG_SLOTS_MELEE} melee / {TDConstant.CONFIG_SLOTS_RANGED} ranged, " +
                             $"config only had {meleeTaken} / {rangedTaken} — bar topped up off-ratio</color>");
        }

        // Shuffled again so the two groups interleave. Left-to-right they are just cards; the
        // player reads their role from the portrait, not from a position that would only be
        // stable within a single match anyway.
        Shuffle(m_ActiveSlots);

        Debug.Log($"[TDTowerMainControl] BuildSlots: {m_ActiveSlots.Count} slots " +
                  $"({meleeTaken} melee / {rangedTaken} ranged)");
    }

    /// <summary>Moves up to `count` entries from `pool` into the active bar. Returns how many moved.</summary>
    private int TakeInto(List<TDTowerSlotInfo> pool, int count)
    {
        int n = Mathf.Min(count, pool.Count);
        for (int i = 0; i < n; i++) m_ActiveSlots.Add(pool[i]);
        pool.RemoveRange(0, n);
        return n;
    }

    private static void Shuffle(List<TDTowerSlotInfo> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    public void OnSelectTowerHolder(int index)
    {
        if (index < 0 || index >= m_ActiveSlots.Count) return;
        m_CurrentSlot = m_ActiveSlots[index];
        onGetTowerPrefab?.Invoke(m_CurrentSlot);
    }

    // ── Placement ─────────────────────────────────────────────────────────────

    public void OnPlaceTower(GameObject currentTower)
    {
        if (currentTower == null) return;
        TDPlaceTowerControl.api.CheckPlaceTower(
            currentTower.transform.position, currentTower, m_CurrentSlot);
    }

    // ── Rotation ──────────────────────────────────────────────────────────────

    public void RotateTowerClockwise(GameObject currentTower, int currentRotationIndex)
    {
        if (currentTower == null) return;
        int next = (currentRotationIndex + 1) % 4;
        UpdateTowerRotation(currentTower, next);
        onGetCurrentRotationIndex?.Invoke(next);
    }

    public void RotateTowerCounterClockwise(GameObject currentTower, int currentRotationIndex)
    {
        if (currentTower == null) return;
        int next = (currentRotationIndex - 1 + 4) % 4;
        UpdateTowerRotation(currentTower, next);
        onGetCurrentRotationIndex?.Invoke(next);
    }

    private void UpdateTowerRotation(GameObject currentTower, int index)
        => currentTower.transform.rotation = Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[index], 0f);
}

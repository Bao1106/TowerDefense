using System;
using System.Collections.Generic;
using System.Linq;
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

    public void BuildSlots()
    {
        m_ActiveSlots.Clear();

        var all = new List<TDTowerSlotInfo>();

        var towerSetting = TDFlyweightTowerDataSettings.api;
        if (towerSetting != null)
        {
            foreach (var data in towerSetting.GetAllTowers())
            {
                if (data.towerPrefab == null) continue;
                all.Add(new TDTowerSlotInfo
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
                all.Add(new TDTowerSlotInfo
                {
                    prefab = op.operatorPrefab,
                    towerType = TowerType.Operator,
                    operatorType = op.operatorType,
                    cost = op.cost,
                    icon = op.icon
                });
            }
        }

        if (all.Count > TDConstant.CONFIG_MAX_SLOTS)
        {
            for (int i = all.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            m_ActiveSlots.AddRange(all.Take(TDConstant.CONFIG_MAX_SLOTS));
        }
        else
        {
            m_ActiveSlots.AddRange(all);
        }

        Debug.Log($"[TDTowerMainControl] BuildSlots: {m_ActiveSlots.Count} slots");
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

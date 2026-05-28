using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton registry cho melee operators đặt trên path cells.
///
/// Chức năng:
///   1. Track valid melee cells (path cells, trừ gate cells) — đăng ký từ TDEnemyPathView.VisualizeAllPaths
///   2. Track operator vị trí + block count / current blocked count
///   3. CanBlock(cell) → dùng bởi TDEnemyView để kiểm tra trước khi bước vào ô
/// </summary>
public class TDMeleeRegistry
{
    public static TDMeleeRegistry api;

    // Path cells hợp lệ để đặt melee (không tính gate cells)
    private readonly HashSet<Vector2Int> m_ValidMeleeCells = new HashSet<Vector2Int>();

    // cell → (blockCapacity, currentBlockedCount)
    private readonly Dictionary<Vector2Int, (int capacity, int count)> m_Operators
        = new Dictionary<Vector2Int, (int, int)>();

    // ─── Path cell registration (từ VisualizeAllPaths) ───────────────────────

    public void RegisterPathCell(Vector2Int cell)
        => m_ValidMeleeCells.Add(cell);

    public bool IsValidMeleeCell(Vector2Int cell)
        => m_ValidMeleeCells.Contains(cell);

    public List<Vector2Int> GetValidMeleeCells()
        => new List<Vector2Int>(m_ValidMeleeCells);

    // ─── Operator management (đặt / remove melee operator) ───────────────────

    /// <summary>
    /// Gọi khi đặt melee operator lên path cell.
    /// blockCapacity = maxTargets từ SO (bao nhiêu enemy có thể bị chặn cùng lúc).
    /// </summary>
    public void RegisterOperator(Vector2Int cell, int blockCapacity)
    {
        m_Operators[cell] = (Mathf.Max(1, blockCapacity), 0);
        Debug.Log($"[MeleeRegistry] Operator registered at {cell}, blockCapacity={blockCapacity}");
    }

    public void UnregisterOperator(Vector2Int cell)
        => m_Operators.Remove(cell);

    public bool HasOperatorAt(Vector2Int cell)
        => m_Operators.ContainsKey(cell);

    // ─── Blocking state (gọi bởi TDEnemyView) ────────────────────────────────

    /// <summary>
    /// True nếu ô có operator VÀ còn chỗ chặn enemy.
    /// </summary>
    public bool CanBlock(Vector2Int cell)
    {
        if (!m_Operators.TryGetValue(cell, out var info)) return false;
        return info.count < info.capacity;
    }

    /// <summary>
    /// Enemy bắt đầu bị chặn tại cell — tăng counter.
    /// </summary>
    public void OnEnemyBlocked(Vector2Int cell)
    {
        if (m_Operators.TryGetValue(cell, out var info))
            m_Operators[cell] = (info.capacity, info.count + 1);
    }

    /// <summary>
    /// Enemy rời khỏi cell (chết / returned to pool) — giảm counter.
    /// </summary>
    public void OnEnemyUnblocked(Vector2Int cell)
    {
        if (m_Operators.TryGetValue(cell, out var info))
            m_Operators[cell] = (info.capacity, Mathf.Max(0, info.count - 1));
    }
}

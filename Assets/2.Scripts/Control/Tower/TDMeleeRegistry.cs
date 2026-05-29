using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton registry cho operators đặt trên path cells.
///
/// Chức năng:
///   1. Track valid operator cells (path cells, trừ gate cells)
///   2. Track operator vị trí + block capacity / blocked enemy references
///   3. CanBlock(cell) → dùng bởi TDEnemyView khi bước vào ô
///   4. GetBlockedEnemies(cell) → dùng bởi TDOperatorView để attack
/// </summary>
public class TDOperatorRegistry
{
    public static TDOperatorRegistry api;

    // Path cells hợp lệ để đặt operator (không tính gate cells)
    private readonly HashSet<Vector2Int> m_ValidOperatorCells = new HashSet<Vector2Int>();

    // cell → (blockCapacity, currentBlockedCount)
    private readonly Dictionary<Vector2Int, (int capacity, int count)> m_Operators
        = new Dictionary<Vector2Int, (int, int)>();

    // cell → danh sách enemy đang bị chặn tại ô đó
    private readonly Dictionary<Vector2Int, List<TDEnemyView>> m_BlockedEnemies
        = new Dictionary<Vector2Int, List<TDEnemyView>>();

    // cell → TDOperatorView đang đứng tại ô đó (để enemy gọi TakeDamage)
    private readonly Dictionary<Vector2Int, TDOperatorView> m_OperatorViews
        = new Dictionary<Vector2Int, TDOperatorView>();

    // ─── Path cell registration ───────────────────────────────────────────────

    public void RegisterPathCell(Vector2Int cell)
        => m_ValidOperatorCells.Add(cell);

    public bool IsValidOperatorCell(Vector2Int cell)
        => m_ValidOperatorCells.Contains(cell);

    public List<Vector2Int> GetValidOperatorCells()
        => new List<Vector2Int>(m_ValidOperatorCells);

    // ─── Operator management ──────────────────────────────────────────────────

    public void RegisterOperator(Vector2Int cell, int blockCapacity)
    {
        m_Operators[cell]      = (Mathf.Max(1, blockCapacity), 0);
        m_BlockedEnemies[cell] = new List<TDEnemyView>();
        Debug.Log($"[OperatorRegistry] Operator registered at {cell}, blockCapacity={blockCapacity}");
    }

    /// Gọi từ TDOperatorView.Init() để lưu reference view (dùng cho enemy tấn công lại).
    public void RegisterOperatorView(Vector2Int cell, TDOperatorView view)
        => m_OperatorViews[cell] = view;

    /// Trả về TDOperatorView tại cell (null nếu không có).
    public TDOperatorView GetOperatorView(Vector2Int cell)
        => m_OperatorViews.TryGetValue(cell, out var v) ? v : null;

    /// Unregister operator — tự động ForceUnblock tất cả enemy đang bị chặn.
    public void UnregisterOperator(Vector2Int cell)
    {
        if (m_BlockedEnemies.TryGetValue(cell, out var list))
        {
            // Snapshot để tránh modify-while-iterate
            var snapshot = new List<TDEnemyView>(list);
            list.Clear();
            foreach (var enemy in snapshot)
                enemy?.ForceUnblock();
        }
        m_Operators.Remove(cell);
        m_BlockedEnemies.Remove(cell);
        m_OperatorViews.Remove(cell);
    }

    public bool HasOperatorAt(Vector2Int cell)
        => m_Operators.ContainsKey(cell);

    // ─── Blocking state ───────────────────────────────────────────────────────

    /// True nếu ô có operator VÀ còn slot chặn.
    public bool CanBlock(Vector2Int cell)
    {
        if (!m_Operators.TryGetValue(cell, out var info)) return false;
        return info.count < info.capacity;
    }

    /// Enemy bắt đầu bị chặn — tăng counter, lưu reference.
    public void OnEnemyBlocked(Vector2Int cell, TDEnemyView enemy)
    {
        if (m_Operators.TryGetValue(cell, out var info))
            m_Operators[cell] = (info.capacity, info.count + 1);

        if (!m_BlockedEnemies.TryGetValue(cell, out var list))
            m_BlockedEnemies[cell] = list = new List<TDEnemyView>();
        if (enemy != null && !list.Contains(enemy))
            list.Add(enemy);
    }

    /// Enemy rời khỏi cell (chết / pool) — giảm counter, xóa reference.
    public void OnEnemyUnblocked(Vector2Int cell, TDEnemyView enemy)
    {
        if (m_Operators.TryGetValue(cell, out var info))
            m_Operators[cell] = (info.capacity, Mathf.Max(0, info.count - 1));

        if (m_BlockedEnemies.TryGetValue(cell, out var list))
            list.Remove(enemy);
    }

    /// Trả về snapshot danh sách enemy đang bị chặn tại cell (không null, có thể empty).
    public List<TDEnemyView> GetBlockedEnemies(Vector2Int cell)
    {
        if (m_BlockedEnemies.TryGetValue(cell, out var list))
            return new List<TDEnemyView>(list); // snapshot tránh modify-while-iterate
        return new List<TDEnemyView>();
    }
}

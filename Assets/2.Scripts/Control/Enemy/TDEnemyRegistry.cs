using System.Collections.Generic;

// Centralized registry của tất cả enemy đang sống trên scene.
// Tower query registry thay vì dùng trigger collider — không cần DetectionArea child GO.
// Thread-safe với wave nhiều enemy: list thay đổi chỉ qua Register/Unregister.
public class TDEnemyRegistry
{
    public static TDEnemyRegistry api;

    private readonly List<TDEnemyView> m_ActiveEnemies = new List<TDEnemyView>();

    public void Register(TDEnemyView enemy)
    {
        if (enemy != null && !m_ActiveEnemies.Contains(enemy))
            m_ActiveEnemies.Add(enemy);
    }

    public void Unregister(TDEnemyView enemy)
    {
        m_ActiveEnemies.Remove(enemy);
    }

    // Trả về read-only view — caller không thể modify list gốc
    public IReadOnlyList<TDEnemyView> GetAll() => m_ActiveEnemies;
}

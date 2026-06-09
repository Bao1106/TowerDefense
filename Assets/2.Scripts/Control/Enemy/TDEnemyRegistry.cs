using System.Collections.Generic;

// Centralized registry of all enemies currently alive in the scene.
// Towers query the registry instead of using trigger colliders — no DetectionArea child GO needed.
// Thread-safe across waves with many enemies: the list is only modified through Register/Unregister.
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

    // Returns a read-only view — callers cannot modify the underlying list
    public IReadOnlyList<TDEnemyView> GetAll() => m_ActiveEnemies;
}

using UnityEngine;

/// <summary>
/// Centralized cache for materials used within a stage.
/// - TowerHighlight : loaded from Resources (pre-existing asset, never destroyed); every deploy tile,
///   path cell or TowerZone, lights with it
/// Call Clear() when the stage or scene is reset.
/// </summary>
public static class TDStageMaterialCache
{
    private static Material m_TowerHighlight; // Resources asset, green — every deploy tile

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>Loads from Resources/Materials/TowerHighlight.mat (pre-existing asset).</summary>
    public static Material TowerHighlight
    {
        get
        {
            if (m_TowerHighlight == null)
                m_TowerHighlight = Resources.Load<Material>(TDConstant.RES_TOWER_HIGHLIGHT_MAT);
            if (m_TowerHighlight == null)
                Debug.LogError($"[TDStageMaterialCache] Material not found at Resources/{TDConstant.RES_TOWER_HIGHLIGHT_MAT}");
            return m_TowerHighlight;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public static void Clear()
    {
        m_TowerHighlight = null; // only nulls the reference; the asset remains in Resources
    }
}

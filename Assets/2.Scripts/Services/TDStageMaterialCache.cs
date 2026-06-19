using UnityEngine;

/// <summary>
/// Centralized cache for materials used within a stage.
/// - TowerHighlight : loaded from Resources (pre-existing asset, never destroyed)
/// - OperatorHighlight: created at runtime once, destroyed when Clear() is called
/// Call Clear() when the stage or scene is reset to free memory.
/// </summary>
public static class TDStageMaterialCache
{
    private static Material m_OperatorHighlight; // runtime-created, blue — PathCell
    private static Material m_TowerHighlight; // Resources asset, green — TowerZone

    // ── Accessors ─────────────────────────────────────────────────────────────

    public static Material OperatorHighlight
        => m_OperatorHighlight ??= CreateRuntime(new Color(0.1f, 0.45f, 1f, 0.75f));

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
        // Only destroy the runtime-created material — the Resources asset must NOT be destroyed
        DestroyRef(ref m_OperatorHighlight);
        m_TowerHighlight = null; // only nulls the reference; the asset remains in Resources
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static Material CreateRuntime(Color color)
    {
        var shader = Shader.Find(TDConstant.SHADER_URP_UNLIT);
        if (shader == null)
        {
            Debug.LogError($"[TDStageMaterialCache] Shader '{TDConstant.SHADER_URP_UNLIT}' not found");
            return null;
        }
        return new Material(shader) { color = color };
    }

    private static void DestroyRef(ref Material mat)
    {
        if (mat == null) return;
        Object.Destroy(mat);
        mat = null;
    }
}

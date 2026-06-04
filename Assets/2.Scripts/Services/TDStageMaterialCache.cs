using UnityEngine;

/// <summary>
/// Cache tập trung cho các Material dùng trong stage.
/// - TowerHighlight  : load từ Resources (asset có sẵn, không destroy)
/// - OperatorHighlight: tạo runtime 1 lần, destroy khi Clear()
/// Gọi Clear() khi stage/scene reset để giải phóng memory.
/// </summary>
public static class TDStageMaterialCache
{
    private const string SHADER_URP_UNLIT        = "Universal Render Pipeline/Unlit";
    private const string RES_TOWER_HIGHLIGHT_MAT = "Materials/TowerHighlight";

    private static Material m_OperatorHighlight; // runtime-created, xanh dương — PathCell
    private static Material m_TowerHighlight;    // Resources asset, xanh lá   — TowerZone

    // ── Accessors ─────────────────────────────────────────────────────────────

    public static Material OperatorHighlight
        => m_OperatorHighlight ??= CreateRuntime(new Color(0.1f, 0.45f, 1f, 0.75f));

    /// <summary>Load từ Resources/Materials/TowerHighlight.mat (asset có sẵn).</summary>
    public static Material TowerHighlight
    {
        get
        {
            if (m_TowerHighlight == null)
                m_TowerHighlight = Resources.Load<Material>(RES_TOWER_HIGHLIGHT_MAT);
            if (m_TowerHighlight == null)
                Debug.LogError($"[TDStageMaterialCache] Material not found at Resources/{RES_TOWER_HIGHLIGHT_MAT}");
            return m_TowerHighlight;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public static void Clear()
    {
        // Chỉ destroy material được tạo runtime — Resources asset KHÔNG destroy
        DestroyRef(ref m_OperatorHighlight);
        m_TowerHighlight = null; // chỉ null ref, asset vẫn còn trong Resources
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static Material CreateRuntime(Color color)
    {
        var shader = Shader.Find(SHADER_URP_UNLIT);
        if (shader == null)
        {
            Debug.LogError($"[TDStageMaterialCache] Shader '{SHADER_URP_UNLIT}' not found");
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

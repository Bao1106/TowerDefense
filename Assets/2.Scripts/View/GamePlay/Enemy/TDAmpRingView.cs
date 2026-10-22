using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The ring around an enemy that amplifies leaks (Tầng 4b §4.6): a thin white line with a dark lining,
/// flat on the ground, at exactly the reach the stress code uses — LEAK_AMP_RADIUS cells. Every
/// amplifier wears the same one, the boss and the Herald alike, so one look teaches both.
///
/// It replaces the Herald's purple disc, which filled the whole area and tinted the ground under every
/// badge inside it the same hue as Broken (Tầng 4 A2). A line says where the reach ends and leaves the
/// ground alone. White because every hue already means something here — green/orange/red the bands,
/// indigo Broken, cyan Rescue, gold money and selection — and the dark lining keeps it on pale tiles.
/// </summary>
public class TDAmpRingView : MonoBehaviour
{
    // A material asset, not a shader found by name: a shader only code asks for is stripped from a build.
    public const string MATERIAL_PATH = "Materials/AmpRing";
    public const int SEGMENTS = 64;
    public const float WORLD_RADIUS = TDConstant.LEAK_AMP_RADIUS * TDConstant.CONFIG_GRID_CELL_SIZE;

    // World units at 54 px each on a 1080p screen: ~3 px of white over ~6 px of lining.
    public const float WHITE_WIDTH = 0.055f, LINING_WIDTH = 0.11f, LINING_ALPHA = 0.6f;

    private static Material s_Material;

    private Transform m_Owner;
    private LineRenderer m_Lining;
    private LineRenderer m_White;
    private readonly Vector3[] m_Points = new Vector3[SEGMENTS];

    /// <summary>Creates the ring under `owner`, laid out at once rather than on the first LateUpdate.</summary>
    public static TDAmpRingView Attach(Transform owner)
    {
        var go = new GameObject("AmpRing");
        go.transform.SetParent(owner, worldPositionStays: false);

        var view = go.AddComponent<TDAmpRingView>();
        view.m_Owner = owner;
        view.m_Lining = view.NewLine("Lining", new Color(0f, 0f, 0f, LINING_ALPHA), LINING_WIDTH, 0);
        view.m_White = view.NewLine("White", Color.white, WHITE_WIDTH, 1);
        view.Layout();
        return view;
    }

    public void Show(bool on)
    {
        if (gameObject.activeSelf != on) gameObject.SetActive(on);
        if (on) Layout();
    }

    /// <summary>The white line's points in world space.</summary>
    public Vector3[] WorldPoints()
    {
        var points = new Vector3[m_White.positionCount];
        m_White.GetPositions(points);
        return points;
    }

    private void LateUpdate() => Layout();

    // World-space points around the owner, so the boss's own scale and its turn toward the path change
    // nothing: the reach is measured in cells on the ground, not in the model's local space.
    private void Layout()
    {
        if (m_Owner == null) return;

        var centre = m_Owner.position;
        for (int i = 0; i < SEGMENTS; i++)
        {
            float a = 2f * Mathf.PI * i / SEGMENTS;
            m_Points[i] = new Vector3(centre.x + WORLD_RADIUS * Mathf.Cos(a), TDConstant.CONFIG_RANGE_HIGHLIGHT_Y,
                                      centre.z + WORLD_RADIUS * Mathf.Sin(a));
        }

        m_Lining.SetPositions(m_Points);
        m_White.SetPositions(m_Points);
    }

    private LineRenderer NewLine(string name, Color colour, float width, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, worldPositionStays: false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = SEGMENTS;
        line.widthMultiplier = width;
        line.startColor = line.endColor = colour;
        line.sharedMaterial = RingMaterial;
        line.sortingOrder = order; // the white drawn over its own lining
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private static Material RingMaterial
    {
        get
        {
            if (s_Material != null) return s_Material;
            s_Material = Resources.Load<Material>(MATERIAL_PATH);
            if (s_Material == null)
            {
                Debug.LogWarning($"[AmpRing] no material at Resources/{MATERIAL_PATH}; using a runtime Sprites/Default copy");
                s_Material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            }
            return s_Material;
        }
    }
}

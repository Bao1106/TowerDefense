using UnityEngine;

/// <summary>
/// The cyan arc from the rescuer's badge to the rescued one's (Tầng 4 §4.6).
///
/// The game picks the rescuer for the player — the calmest neighbour who can pay — and that
/// operator pays 50 SP and takes stress for it. Until this arc the player never saw who had
/// answered. Built in code, gone after 0.4 s; deleting this file removes the feature.
/// </summary>
public sealed class TDRescueArcView : MonoBehaviour
{
    private const float HEIGHT = 1f;         // half a cell: cells are 2 world units
    private const float WIDTH = 0.074f;      // ~4 px at 1080p, 54 px per world unit
    private const int SEGMENTS = 16;
    private const float DRAW_SECONDS = 0.2f; // grows from rescuer to rescued...
    private const float FADE_SECONDS = 0.2f; // ...then fades
    private const int SORTING_ORDER = 7;     // above the morale icons' canvas (6)

    private LineRenderer m_Line;
    private Vector3 m_From;
    private Vector3 m_To;
    private float m_Start;
    private Color m_Colour;

    /// <summary>
    /// Draws the arc between two badge positions. The positions are taken now, so either operator
    /// dying mid-arc changes nothing.
    /// </summary>
    public static void Play(Vector3 from, Vector3 to)
    {
        var go = new GameObject("RescueArc");
        var arc = go.AddComponent<TDRescueArcView>();
        arc.m_From = from;
        arc.m_To = to;
        arc.m_Start = Time.time;
        ColorUtility.TryParseHtmlString(TDConstant.COLOR_RESCUE, out arc.m_Colour);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = TDMoraleIconView.OnTopMaterial; // drawn over the models, like the badges
        line.widthMultiplier = WIDTH;
        line.numCapVertices = 4;
        line.sortingOrder = SORTING_ORDER;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.positionCount = 0;
        arc.m_Line = line;
    }

    /// <summary>
    /// Quadratic Bézier with its control point 2·height above the midpoint, which puts the
    /// curve's own peak exactly `height` above it.
    /// </summary>
    public static Vector3 ArcPoint(Vector3 from, Vector3 to, float height, float t)
    {
        var control = (from + to) * 0.5f + Vector3.up * (2f * height);
        float u = 1f - t;
        return u * u * from + 2f * u * t * control + t * t * to;
    }

    private void Update()
    {
        float t = Time.time - m_Start;
        if (t >= DRAW_SECONDS + FADE_SECONDS) { Destroy(gameObject); return; }

        float drawn = Mathf.Clamp01(t / DRAW_SECONDS);
        int count = Mathf.Max(2, Mathf.CeilToInt(drawn * SEGMENTS) + 1);
        m_Line.positionCount = count;
        for (int i = 0; i < count; i++)
            m_Line.SetPosition(i, ArcPoint(m_From, m_To, HEIGHT, drawn * i / (count - 1)));

        float alpha = t < DRAW_SECONDS ? 1f : 1f - (t - DRAW_SECONDS) / FADE_SECONDS;
        var c = new Color(m_Colour.r, m_Colour.g, m_Colour.b, alpha);
        m_Line.startColor = c;
        m_Line.endColor = c;
    }
}

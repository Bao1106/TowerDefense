using TDEnums;
using UnityEngine;

/// <summary>
/// Step 2.5 — raw morale numbers floating over each operator. Ugly on purpose.
///
/// The four tuning knobs have to be adjusted against READABLE NUMBERS before Phase 4 spends
/// four or five days on icons, sound and feedback. Shipping the pretty version first and
/// only then discovering STRESS_PER_LEAK is wrong pays for the work twice.
///
/// Self-installing: no prefab, no scene wiring, nothing to remember to add. Flip
/// MORALE_DEBUG_OVERLAY off to hide it, delete this file when Phase 4 lands.
/// </summary>
public sealed class TDMoraleDebugOverlay : MonoBehaviour
{
    private const float REFRESH_INTERVAL = 0.5f; // FindObjectsOfType is slow; debug-only cadence

    // Font is a FRACTION of screen height, not a fixed pixel size. OnGUI draws in the game's
    // own resolution (1920x1080 here) while the Game View shows it scaled to fit — at 0.37x
    // an 11px label lands at about 4px on screen, which is why the first version was
    // unreadable. 3% of height survives any zoom the editor picks.
    private const float FONT_SCREEN_FRACTION = 0.030f;

    private TDOperatorView[] m_Operators = System.Array.Empty<TDOperatorView>();
    private float m_NextRefresh;
    private GUIStyle m_Style;
    private Texture2D m_Backdrop;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!TDConstant.MORALE_DEBUG_OVERLAY) return;

        var go = new GameObject("~MoraleDebugOverlay") { hideFlags = HideFlags.HideAndDontSave };
        go.AddComponent<TDMoraleDebugOverlay>();
        DontDestroyOnLoad(go);
    }

    private void OnGUI()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Victory / GameOver / pause all set timeScale to 0, and the popups they raise are
        // exactly what these labels were drawing on top of. One check covers all three.
        if (Time.timeScale <= 0f) return;

        int fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * FONT_SCREEN_FRACTION));

        m_Style ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        m_Style.fontSize = fontSize;

        if (m_Backdrop == null)
        {
            m_Backdrop = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            m_Backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            m_Backdrop.Apply();
        }

        if (Time.unscaledTime >= m_NextRefresh)
        {
            m_NextRefresh = Time.unscaledTime + REFRESH_INTERVAL;
            m_Operators = FindObjectsOfType<TDOperatorView>();
        }

        float w = fontSize * 5f;
        float h = fontSize * 1.5f;

        foreach (var op in m_Operators)
        {
            if (op == null || op.Morale == null) continue;

            Vector3 screen = cam.WorldToScreenPoint(op.transform.position + Vector3.up * 2.2f);
            if (screen.z <= 0f) continue; // behind the camera

            // Operators stand shoulder to shoulder in a chokepoint, and two labels at the
            // same height merge into one unreadable line. Stagger by cell parity: any two
            // adjacent cells differ in (x+y), so neighbours never share a row.
            var cell = TDGridMainModel.api != null
                ? TDGridMainModel.api.WorldToCell(op.transform.position)
                : Vector2Int.zero;
            float stagger = ((cell.x + cell.y) & 1) == 0 ? 0f : -h;

            var rect = new Rect(screen.x - w * 0.5f, Screen.height - screen.y - h + stagger, w, h);

            // The map runs light grey to dark asphalt to green, so a bare label is legible
            // over roughly none of it. A backdrop is cheaper than picking a clever colour.
            GUI.DrawTexture(rect, m_Backdrop);

            m_Style.normal.textColor = ColorFor(op.Morale.State);
            GUI.Label(rect, Describe(op), m_Style);
        }
    }

    private void OnDestroy()
    {
        if (m_Backdrop != null) Destroy(m_Backdrop);
    }

    private static string Describe(TDOperatorView op)
    {
        var morale = op.Morale;
        if (morale.IsBroken) return "BROKEN";

        // The state name used to be printed here and it was pure noise: the COLOUR already
        // says Calm / Steady / Stressed, and spelling it out doubled the label width, which
        // is what made two neighbours overlap into "8.2 CALM CALM".
        string text = $"{morale.Value:F0}";

        // Leaks to break is the number tuning happens on. Melee priced as holding the cell
        // alone (share 1), ranged as the single shooter beside a melee (share 0.3).
        float share = op.Data != null && op.Data.deployZone == DeployZone.TowerZone
            ? 1f - TDConstant.LEAK_SHARE_MELEE
            : 1f;
        text += $" · {morale.LeaksToBreak(share)} leaks→break";

        return text;
    }

    private static Color ColorFor(MoraleState state)
    {
        string hex = state switch
        {
            MoraleState.Calm => TDConstant.COLOR_MORALE_CALM,
            MoraleState.Steady => TDConstant.COLOR_MORALE_STEADY,
            _ => TDConstant.COLOR_MORALE_STRESSED,
        };

        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }
}

using TDEnums;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The morale icon that floats over a deployed operator (§02).
///
/// Built entirely in code and attached at runtime — no prefab edits, nothing to wire in a
/// scene, and eight operator prefabs left untouched. Deleting this file removes the feature.
///
/// Shape carries the state, not colour: ☺ / ─ / ><. The design constraint is that the three
/// must stay apart in greyscale, because on a 6-inch screen in daylight with fifteen enemies
/// and VFX everywhere, colour is the first thing to go — and it is also the minimum bar for
/// the ~8% of men who are colourblind.
/// </summary>
public sealed class TDMoraleIconView : MonoBehaviour
{
    private const string SPRITE_PATH = "Sprites/Morale/icon_{0}_white";

    // Placed by hand in the Scene view and read back off the live transform. The offset is in
    // WORLD space, not local: the icon billboards to a fixed rotation while the operator turns
    // to face its target, so a local offset would swing the icon around them on every turn.
    //
    // Up-and-right of the operator rather than centred over the head — the retreat panel opens
    // centred, so the thing telling the player WHETHER to pull out would sit under the thing
    // used to DO it. On screen (camera pitched 30°) that lands ~0.55 up and ~0.47 right of the
    // operator's feet; the -0.855 on z is what carries it 1.3 units toward the camera, clear of
    // the model it was otherwise buried in.
    private static readonly Vector3 OFFSET = new Vector3(0.465f, 1.125f, -0.855f);

    // A WORLD measurement, so it has to be divided back out by the parent's scale. Every
    // operator prefab root is 1.5, which silently rendered the icon half again as large as this
    // constant claimed — the same trap as the Canvas sortingOrder: code-built UI inherits none
    // of the prefab's context. Small enough that adjacent cells (2 units apart) never touch.
    private const float WORLD_SIZE = 1.275f;
    private const int PIXELS = 128;

    // Dark disc behind the face. Without it the icon competes with whatever tile happens to
    // be under it; with it, every icon sits on the same background whatever the map does.
    private const float DISC_SCALE = 1.05f;

    // Stressed pulses. 2.4-5.5 seconds is all the warning that band gives under heavy
    // pressure (§10), so a static icon is not a signal — it has to move to be noticed.
    private const float PULSE_HZ = 3f;
    private const float PULSE_DEPTH = 0.18f;

    // Collapse throbs SLOWLY where Stressed flashes fast — a different kind of alarm, not a
    // louder one, which is what the state is. It briefly also tilted the badge 28 degrees, but
    // that was a workaround for having three sprites and four states: `icon_corrupt` carries the
    // silhouette properly now, so the tilt is gone rather than kept as decoration.
    private const float BROKEN_PULSE_HZ = 0.8f;
    private const float BROKEN_PULSE_DEPTH = 0.28f;

    // Visual weight tracks how much attention the band actually deserves. Calm is the common
    // case, so at full size the icon spends most of a match shouting "nothing is wrong" —
    // measured at 1080p it is 69px wide against a 76px operator, louder than the enemies it
    // exists to warn about. Calm stays visible (the drain inside Calm is the early warning
    // that lets a player act BEFORE Stressed), just quiet.
    //
    // The scale ramp also pays for the "nhịp phóng to" §02 requires on entering Stressed: the
    // icon grows into the alarm instead of needing a separate one-shot tween. And it fixes
    // attribution — two Calm icons in adjacent cells go from a 39px gap to 70px, so they stop
    // reading as one two-eyed widget.
    private static readonly (float scale, float alpha)[] k_Weight =
    {
        (0.55f, 0.45f), // Calm
        (0.80f, 1.00f), // Steady
        (1.00f, 1.00f), // Stressed
        (1.00f, 1.00f), // Broken
    };

    // One beat per leak (spec §5.2): +25% and back in 0.2s on top of the band's scale.
    private const float LEAK_PULSE_GAIN = 0.25f;
    private const float LEAK_PULSE_SECONDS = 0.2f;

    private static Sprite[] s_Sprites;
    private static Sprite s_Disc;

    private float m_LeakPulseStart = float.NegativeInfinity;

    private CanvasGroup m_Group;
    private float m_BaseScale;
    private Transform m_Owner;
    private Image m_Disc;
    private Image m_Backdrop;
    private Image m_Face;
    private MoraleState m_Shown = (MoraleState)(-1);

    /// <summary>
    /// Solid circle, generated once rather than shipped as an asset — it is one shape with
    /// no art direction in it, and a generated texture cannot go missing from a folder.
    /// </summary>
    private static Sprite DiscSprite()
    {
        if (s_Disc != null) return s_Disc;

        const int size = 64;
        const float r = size * 0.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
        };

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                // One pixel of feather on the rim, otherwise the circle reads as jagged
                // once it is scaled up to three quarters of a cell.
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        s_Disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        return s_Disc;
    }

    /// <summary>Creates the icon under `owner` and returns it. Null if the art is missing.</summary>
    public static TDMoraleIconView Attach(Transform owner)
    {
        if (!LoadSprites()) return null;

        var go = new GameObject("MoraleIcon");
        go.transform.SetParent(owner, worldPositionStays: false);

        var view = go.AddComponent<TDMoraleIconView>();
        view.Build(owner);
        return view;
    }

    private static bool LoadSprites()
    {
        if (s_Sprites != null) return s_Sprites[0] != null;

        s_Sprites = new Sprite[4];
        s_Sprites[(int)MoraleState.Calm] = Resources.Load<Sprite>(string.Format(SPRITE_PATH, "calm"));
        s_Sprites[(int)MoraleState.Steady] = Resources.Load<Sprite>(string.Format(SPRITE_PATH, "steady"));
        s_Sprites[(int)MoraleState.Stressed] = Resources.Load<Sprite>(string.Format(SPRITE_PATH, "stressed"));
        s_Sprites[(int)MoraleState.Broken] = Resources.Load<Sprite>(string.Format(SPRITE_PATH, "corrupt"));

        if (s_Sprites[0] == null)
            Debug.LogWarning($"<color=orange>[MoraleIcon] sprites not found under Resources/{string.Format(SPRITE_PATH, "calm")}</color>");

        return s_Sprites[0] != null;
    }

    private void Build(Transform owner)
    {
        m_Owner = owner;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // HPBar_Operator.prefab sits at sortingOrder 5. A fresh Canvas defaults to 0, which
        // put the icon behind the ground tiles and the operator model — it was being drawn,
        // just never seen. One above the HP bar keeps the pair together and both on top.
        canvas.overrideSorting = true;
        canvas.sortingOrder = 6;

        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(PIXELS, PIXELS);
        m_BaseScale = WORLD_SIZE / PIXELS / Mathf.Max(0.0001f, owner.lossyScale.x);
        rect.localScale = Vector3.one * m_BaseScale;

        // One CanvasGroup instead of threading an alpha through five colour assignments —
        // the Stressed disc pulse keeps modulating its own alpha underneath, untouched.
        m_Group = gameObject.AddComponent<CanvasGroup>();

        // Disc first, so everything else sits on a known background instead of on the map.
        m_Disc = NewImage("Disc", rect);
        m_Disc.sprite = DiscSprite();
        m_Disc.color = new Color(0.05f, 0.05f, 0.07f, 0.72f);
        ((RectTransform)m_Disc.transform).localScale = Vector3.one * DISC_SCALE;

        // Backdrop: the same face at low alpha. It keeps the silhouette readable while the
        // coloured layer drains away, so an operator near the end of a band still shows
        // WHICH band rather than fading to nothing.
        m_Backdrop = NewImage("Backdrop", rect);
        m_Backdrop.color = new Color(1f, 1f, 1f, 0.25f);

        // Face: the coloured layer, drained top-down as stress fills the band.
        m_Face = NewImage("Face", rect);
        m_Face.type = Image.Type.Filled;
        m_Face.fillMethod = Image.FillMethod.Vertical;
        m_Face.fillOrigin = (int)Image.OriginVertical.Bottom; // drains from the top down
    }

    private static Image NewImage(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, worldPositionStays: false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Feeds the icon from morale. Called once per frame by TDOperatorView.</summary>
    public void Refresh(TDOperatorMorale morale)
    {
        if (morale == null || m_Face == null) return;

        var state = morale.State;

        if (state != m_Shown)
        {
            m_Shown = state;
            var sprite = s_Sprites[(int)state];
            m_Face.sprite = sprite;
            m_Backdrop.sprite = sprite;
        }

        var weight = k_Weight[Mathf.Clamp((int)state, 0, k_Weight.Length - 1)];
        float beat = (Time.time - m_LeakPulseStart) / LEAK_PULSE_SECONDS;
        float leak = beat >= 0f && beat < 1f ? LEAK_PULSE_GAIN * Mathf.Sin(beat * Mathf.PI) : 0f;
        transform.localScale = Vector3.one * (m_BaseScale * weight.scale * (1f + leak));
        if (m_Group != null) m_Group.alpha = weight.alpha;

        Color colour = ColourFor(state);

        if (state == MoraleState.Broken)
        {
            // The corrupt sprite now carries the state on its own, so the fill goes back to full:
            // this is not progress through a band, it is a symbol that either applies or does not.
            m_Face.fillAmount = 1f;

            // Inverted badge — the symbol takes #3C3180 and the disc goes pale. Measured, not
            // chosen: dark indigo on the asphalt operators stand on scores 1.23:1, and no darker
            // shade escapes it either, because Calm/Steady/Stressed already hold greyscale
            // 175/148/100 and everything below Stressed lands on the road at 74.
            m_Face.color = colour;
            m_Backdrop.color = new Color(colour.r, colour.g, colour.b, 0.35f);

            // A SLOW deep throb — not silence. The first version simply stopped pulsing, so the
            // one channel shouting "urgent" all through Stressed switched OFF at the exact moment
            // things became worst. Stillness after motion reads as calming down; that is an
            // inverted signal, which is worse than none. Motion must never stop as things get
            // worse — only change its rhythm. 0.8 Hz against Stressed's 3 Hz reads as a different
            // kind of alarm rather than a louder one, which is what this state is.
            float slow = 0.5f + 0.5f * Mathf.Sin(Time.time * BROKEN_PULSE_HZ * Mathf.PI * 2f);
            var pale = Hex(TDConstant.COLOR_MORALE_BROKEN_DISC);
            m_Disc.color = new Color(pale.r, pale.g, pale.b, 0.80f + BROKEN_PULSE_DEPTH * slow);
            return;
        }

        // Fill shows progress THROUGH the current band, not overall stress. That is what
        // makes the icon useful at a glance: full means "just arrived here", empty means
        // "about to get worse", regardless of which band you are in.
        m_Face.fillAmount = 1f - BandProgress(morale.Value, state);

        m_Face.color = colour;
        m_Backdrop.color = new Color(1f, 1f, 1f, 0.25f);

        if (state == MoraleState.Stressed)
        {
            // The one band that colours the DISC itself, at a deeper shade than the face,
            // and pulses. Two channels change at once — hue and motion — because this band
            // lasts 2.4-5.5 seconds under pressure and a single cue is not enough warning.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PULSE_HZ * Mathf.PI * 2f);
            m_Disc.color = new Color(colour.r * 0.45f, colour.g * 0.25f, colour.b * 0.25f,
                                     0.78f + PULSE_DEPTH * pulse);
        }
        else
        {
            m_Disc.color = new Color(0.05f, 0.05f, 0.07f, 0.72f);
        }
    }

    /// <summary>A leak just landed on this operator. Restarts the beat rather than stacking it.</summary>
    public void Pulse() => m_LeakPulseStart = Time.time;

    /// <summary>0 at the start of the current band, 1 at its top edge.</summary>
    private static float BandProgress(float value, MoraleState state)
    {
        float lo = state switch
        {
            MoraleState.Steady => TDConstant.STRESS_CALM_MAX,
            MoraleState.Stressed => TDConstant.STRESS_STEADY_MAX,
            _ => 0f,
        };
        float hi = state switch
        {
            MoraleState.Calm => TDConstant.STRESS_CALM_MAX,
            MoraleState.Steady => TDConstant.STRESS_STEADY_MAX,
            _ => TDConstant.STRESS_MAX,
        };

        return Mathf.Clamp01((value - lo) / Mathf.Max(0.01f, hi - lo));
    }

    private static Color ColourFor(MoraleState state) => Hex(state switch
    {
        MoraleState.Calm => TDConstant.COLOR_MORALE_CALM,
        MoraleState.Steady => TDConstant.COLOR_MORALE_STEADY,
        MoraleState.Broken => TDConstant.COLOR_MORALE_BROKEN,
        _ => TDConstant.COLOR_MORALE_STRESSED,
    });

    private static Color Hex(string hex)
        => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

    private void LateUpdate()
    {
        if (m_Owner == null) return;

        // Same billboard treatment as TDHPBarView: the operator turns to face its target,
        // so the icon has to re-assert its own world rotation every frame or it turns with them.
        transform.position = m_Owner.position + OFFSET;
        transform.rotation = Quaternion.Euler(30f, 0f, 0f);
    }
}

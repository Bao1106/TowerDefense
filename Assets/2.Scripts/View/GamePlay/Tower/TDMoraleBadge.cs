using System.Collections.Generic;
using TDEnums;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The four layers every morale badge is made of — disc, Broken's rim, the face's faint backdrop and
/// the face itself — and the one place that decides their sprite, fill and colour for a morale value.
///
/// The icon floating over an operator and that operator's card in the slot bar both draw through this
/// (Tầng 4b §4.3), so the two can never disagree about what a state looks like. What is particular to
/// the world — billboarding, band scale, the break moment — stays in TDMoraleIconView.
/// </summary>
public sealed class TDMoraleBadge
{
    private const string SPRITE_PATH = "Sprites/Morale/icon_{0}_white";

    // Dark disc behind the face. Without it the icon competes with whatever tile happens to
    // be under it; with it, every icon sits on the same background whatever the map does.
    public const float DISC_SCALE = 1.05f;

    // Broken's edge on pale ground (Tầng 4 §4.2): 5.6 units of the 128-unit icon, ~3 px at 1080p.
    // A tower platform measured #C6DAFC — the pale disc melted into it and only the symbol was left.
    // Kept as a fraction of the disc's radius, so the rim scales with the badge wherever it is drawn.
    private const float OUTLINE_THICKNESS = 5.6f / (128f * DISC_SCALE * 0.5f);

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

    private static readonly Color k_DarkDisc = new Color(0.05f, 0.05f, 0.07f, 0.72f);

    private static Sprite[] s_Sprites;
    private static Sprite s_Disc;
    private static readonly Dictionary<float, Sprite> s_Rings = new();

    private Image m_Disc;
    private Image m_Outline;
    private Image m_Backdrop;
    private Image m_Face;
    private MoraleState m_Shown = (MoraleState)(-1);

    /// <summary>The disc layer; the icon slips its break flash in right above it.</summary>
    public RectTransform Disc => m_Disc.rectTransform;

    /// <summary>Loads the four face sprites once. False, with one warning, if the art is missing.</summary>
    public static bool LoadSprites()
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

    /// <summary>
    /// Builds the layers under `parent`, each stretched to fill it. `material` null keeps the canvas
    /// default — right for the HUD; the world icon passes its depth-ignoring material.
    /// </summary>
    public static TDMoraleBadge Build(RectTransform parent, Material material)
    {
        if (!LoadSprites()) return null;

        var badge = new TDMoraleBadge();

        // Disc first, so everything else sits on a known background instead of on the map.
        badge.m_Disc = NewImage("Disc", parent, material);
        badge.m_Disc.sprite = DiscSprite();
        badge.m_Disc.color = k_DarkDisc;
        badge.m_Disc.rectTransform.localScale = Vector3.one * DISC_SCALE;

        // Broken's rim, the disc's size exactly, shown only in that state.
        badge.m_Outline = NewImage("Outline", parent, material);
        badge.m_Outline.sprite = RingSprite(OUTLINE_THICKNESS);
        badge.m_Outline.color = Hex(TDConstant.COLOR_MORALE_BROKEN);
        badge.m_Outline.enabled = false;
        badge.m_Outline.rectTransform.localScale = Vector3.one * DISC_SCALE;

        // Backdrop: the same face at low alpha. It keeps the silhouette readable while the
        // coloured layer drains away, so an operator near the end of a band still shows
        // WHICH band rather than fading to nothing.
        badge.m_Backdrop = NewImage("Backdrop", parent, material);
        badge.m_Backdrop.color = new Color(1f, 1f, 1f, 0.25f);

        // Face: the coloured layer, drained top-down as stress fills the band.
        badge.m_Face = NewImage("Face", parent, material);
        badge.m_Face.type = Image.Type.Filled;
        badge.m_Face.fillMethod = Image.FillMethod.Vertical;
        badge.m_Face.fillOrigin = (int)Image.OriginVertical.Bottom; // drains from the top down

        return badge;
    }

    /// <summary>Sprite, fill and colours for `morale` right now. Call once per frame.</summary>
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

        m_Outline.enabled = state == MoraleState.Broken;
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
            m_Disc.color = k_DarkDisc;
        }
    }

    /// <summary>
    /// Solid circle, generated once rather than shipped as an asset — it is one shape with
    /// no art direction in it, and a generated texture cannot go missing from a folder.
    /// </summary>
    public static Sprite DiscSprite()
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

    /// <summary>
    /// A ring whose band is `thickness` of its radius, generated once per thickness for the same
    /// reason as the disc. 256 px so the thinnest band still has a few texels to feather.
    /// </summary>
    public static Sprite RingSprite(float thickness)
    {
        if (s_Rings.TryGetValue(thickness, out var cached) && cached != null) return cached;

        const int size = 256;
        const float r = size * 0.5f;
        float inner = r * (1f - thickness);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
        };

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01(r - d) * Mathf.Clamp01(d - inner); // feathered on both rims
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        s_Rings[thickness] = sprite;
        return sprite;
    }

    public static Color Hex(string hex)
        => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

    private static Image NewImage(string name, RectTransform parent, Material material)
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
        if (material != null) img.material = material;
        return img;
    }

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
}

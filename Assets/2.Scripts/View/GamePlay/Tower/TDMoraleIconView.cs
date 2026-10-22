using System.Collections.Generic;
using TDEnums;
using TMPro;
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
    // used to DO it. On screen (camera pitched 40°) that lands ~0.55 up and ~0.47 right of the
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

    // Broken's edge on pale ground (Tầng 4 §4.2): ~3 px at 1080p, where one canvas unit is ~0.54 px.
    // A tower platform measured #C6DAFC — the pale disc melted into it and only the symbol was left.
    private const float OUTLINE_UNITS = 5.6f;

    // ── Break moment (Tầng 4 §4.1) ──────────────────────────────────────────
    // Twice the leak beat in both size and length, so the two never read as the same event. A
    // fresh break looked exactly like one ten seconds old (screenshot 3) — this is what tells them apart.
    // 0.5, not the 0.6 first specced: the badge measured ~81 px at rest in play, not the 69 px
    // computed, so 1.6 peaked at ~130 px; 1.5 lands at ~121 px, under the 125 px the screenshots set.
    private const float PUNCH_GAIN = 0.5f;
    private const float PUNCH_SECONDS = 0.35f;
    private const float FLASH_SECONDS = 0.15f;
    private const float LABEL_BLINK = 0.15f;
    private const float LABEL_HOLD_UNTIL = 1.2f;
    private const float LABEL_SECONDS = 1.5f;

    // Canvas units, one 1080p pixel ≈ 1.86 of them: text 36 px (Android's 12sp floor is ~33),
    // plate 165 × 48 px, its bottom 6 px above the badge.
    public const string LABEL_FONT_PATH = "Fonts/OpenSans-Bold SDF"; // the HUD's own face
    public const string LABEL_MATERIAL_PATH = "Fonts/OpenSans-Bold SDF Overlay";
    public const string ON_TOP_SHADER = "Custom/UI_AlwaysOnTop";
    private const float LABEL_FONT_UNITS = 67f;
    private static readonly Vector2 k_LabelPlate = new Vector2(307f, 89f);
    private const float LABEL_GAP_UNITS = 11f;

    // The halo leaves the badge rim and is spent by one cell (108 px), where the neighbour stands.
    private const float RING_FROM_UNITS = 67f;
    public const float HALO_MAX_RADIUS = 200f;
    public const float HALO_SECONDS = 0.4f;
    private const float HALO_THICKNESS_UNITS = 7.4f;

    // Entering Stressed (§4.3) is a warning, not an alarm: thinner, smaller and shorter than the
    // break's halo — 80 px, 2 px, 0.3 s — and the badge itself does not punch.
    public const float STRESS_RING_MAX_RADIUS = 148f;
    public const float STRESS_RING_SECONDS = 0.3f;
    private const float STRESS_RING_THICKNESS_UNITS = 3.7f;

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
    private static readonly Dictionary<float, Sprite> s_Rings = new();
    private static Material s_OnTop;
    private static Material s_LabelMaterial;
    private static Sprite s_Pill;

    private float m_LeakPulseStart = float.NegativeInfinity;
    private float m_BreakStart = float.NegativeInfinity;
    private float m_StressedStart = float.NegativeInfinity;
    private Image m_StressRing;
    private float m_BadgeScale = 1f;
    private Image m_Flash;
    private Image m_Halo;
    private Image m_LabelPlate;
    private TextMeshProUGUI m_Label;

    private CanvasGroup m_Group;
    private float m_BaseScale;
    private Transform m_Owner;
    private RectTransform m_Badge;
    private Image m_Disc;
    private Image m_Backdrop;
    private Image m_Face;
    private Image m_Outline;

    /// <summary>
    /// The UI material that ignores depth. A world-space canvas is depth-tested like any mesh, so a
    /// boss standing on the line hid the badge completely (Tầng 4 §1.2 A1) — the one moment it
    /// mattered most. Shared by everything morale draws, the rescue arc included.
    ///
    /// The shader has ZTest Always written into it; setting unity_GUIZTestMode on a material does
    /// nothing, because the canvas writes that value itself on every draw. It is the shader the HP
    /// bar already uses (HPBar_AlwaysOnTop.mat), which is also what keeps it in a build.
    /// </summary>
    public static Material OnTopMaterial
    {
        get
        {
            if (s_OnTop != null) return s_OnTop;
            var shader = Shader.Find(ON_TOP_SHADER);
            if (shader == null) Debug.LogWarning($"[MoraleIcon] shader {ON_TOP_SHADER} missing; badges will be hidden behind models");
            s_OnTop = shader != null
                ? new Material(shader) { name = "MoraleOnTop", hideFlags = HideFlags.HideAndDontSave }
                : Canvas.GetDefaultCanvasMaterial();
            return s_OnTop;
        }
    }
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

    /// <summary>
    /// A ring whose band is `thickness` of its radius, generated once per thickness for the same
    /// reason as the disc. 256 px so the thinnest band still has a few texels to feather.
    /// </summary>
    private static Sprite RingSprite(float thickness)
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

        // The badge's own scale (band weight, leak beat) lives on this child, not on the root,
        // so what sits beside the badge — the BROKEN label, the rings — is not scaled with it.
        var badgeGo = new GameObject("Badge", typeof(RectTransform));
        m_Badge = (RectTransform)badgeGo.transform;
        m_Badge.SetParent(rect, worldPositionStays: false);
        m_Badge.anchorMin = Vector2.zero;
        m_Badge.anchorMax = Vector2.one;
        m_Badge.offsetMin = Vector2.zero;
        m_Badge.offsetMax = Vector2.zero;

        // Disc first, so everything else sits on a known background instead of on the map.
        m_Disc = NewImage("Disc", m_Badge);
        m_Disc.sprite = DiscSprite();
        m_Disc.color = new Color(0.05f, 0.05f, 0.07f, 0.72f);
        ((RectTransform)m_Disc.transform).localScale = Vector3.one * DISC_SCALE;

        // The break's white flash: a layer over the disc rather than a write to its colour, so
        // the flash ending never has to know what Refresh would have put back.
        m_Flash = NewImage("Flash", m_Badge);
        m_Flash.sprite = DiscSprite();
        m_Flash.color = Color.white;
        m_Flash.enabled = false;
        ((RectTransform)m_Flash.transform).localScale = Vector3.one * DISC_SCALE;

        // Broken's rim, the disc's size exactly, shown only in that state.
        m_Outline = NewImage("Outline", m_Badge);
        m_Outline.sprite = RingSprite(OUTLINE_UNITS / (PIXELS * DISC_SCALE * 0.5f));
        m_Outline.color = Hex(TDConstant.COLOR_MORALE_BROKEN);
        m_Outline.enabled = false;
        ((RectTransform)m_Outline.transform).localScale = Vector3.one * DISC_SCALE;

        // Backdrop: the same face at low alpha. It keeps the silhouette readable while the
        // coloured layer drains away, so an operator near the end of a band still shows
        // WHICH band rather than fading to nothing.
        m_Backdrop = NewImage("Backdrop", m_Badge);
        m_Backdrop.color = new Color(1f, 1f, 1f, 0.25f);

        // Face: the coloured layer, drained top-down as stress fills the band.
        m_Face = NewImage("Face", m_Badge);
        m_Face.type = Image.Type.Filled;
        m_Face.fillMethod = Image.FillMethod.Vertical;
        m_Face.fillOrigin = (int)Image.OriginVertical.Bottom; // drains from the top down

        BuildBreakParts(rect);
    }

    // Halo and label sit on the root, beside the badge: neither grows with its punch, and the
    // label stays the size the 12sp floor asked for.
    private void BuildBreakParts(RectTransform root)
    {
        m_Halo = NewImage("Halo", root);
        // ponytail: the band scales with the ring — thinner at the rim, 4 px at full size. A fixed
        // pixel width means rebuilding a mesh every frame, for a ring that lives 0.4 s.
        m_Halo.sprite = RingSprite(HALO_THICKNESS_UNITS / HALO_MAX_RADIUS);
        Centre(m_Halo.rectTransform, Vector2.zero, Vector2.one * (2f * RING_FROM_UNITS));
        m_Halo.enabled = false;

        m_StressRing = NewImage("StressRing", root);
        m_StressRing.sprite = RingSprite(STRESS_RING_THICKNESS_UNITS / STRESS_RING_MAX_RADIUS);
        Centre(m_StressRing.rectTransform, Vector2.zero, Vector2.one * (2f * RING_FROM_UNITS));
        m_StressRing.enabled = false;

        var platePos = new Vector2(0f, PIXELS * 0.5f + LABEL_GAP_UNITS + k_LabelPlate.y * 0.5f);
        m_LabelPlate = NewImage("LabelPlate", root);
        m_LabelPlate.sprite = PillSprite();
        m_LabelPlate.color = Hex(TDConstant.COLOR_MORALE_BROKEN_DISC);
        Centre(m_LabelPlate.rectTransform, platePos, k_LabelPlate);
        m_LabelPlate.enabled = false;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.SetParent(root, worldPositionStays: false);
        Centre(labelRect, platePos, k_LabelPlate);

        m_Label = labelGo.GetComponent<TextMeshProUGUI>();
        var font = Resources.Load<TMP_FontAsset>(LABEL_FONT_PATH);
        if (font != null) m_Label.font = font;
        m_Label.fontStyle = font != null ? FontStyles.Normal : FontStyles.Bold; // the HUD face is already bold
        m_Label.text = "BROKEN";
        m_Label.fontSize = LABEL_FONT_UNITS;
        m_Label.alignment = TextAlignmentOptions.Center;
        m_Label.enableWordWrapping = false;
        m_Label.color = Hex(TDConstant.COLOR_MORALE_BROKEN);
        m_Label.raycastTarget = false;
        m_Label.fontSharedMaterial = LabelMaterial(m_Label.fontSharedMaterial);
        m_Label.enabled = false;
    }

    // The label font's Overlay preset (TMP's ZTest Always shader). An asset under Resources rather
    // than a copy made here: a shader only code asks for by name is stripped from a build. Not the
    // font's own material — the HUD draws with that and must stay depth-tested as it is.
    private static Material LabelMaterial(Material fontMaterial)
    {
        if (s_LabelMaterial != null) return s_LabelMaterial;
        s_LabelMaterial = Resources.Load<Material>(LABEL_MATERIAL_PATH);
        if (s_LabelMaterial == null)
        {
            Debug.LogWarning($"[MoraleIcon] no label material at Resources/{LABEL_MATERIAL_PATH}; the label can be hidden behind models");
            s_LabelMaterial = fontMaterial;
        }
        return s_LabelMaterial;
    }

    private static void Centre(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>A capsule at the plate's own aspect (307:89), so it is drawn unsliced and undistorted.</summary>
    private static Sprite PillSprite()
    {
        if (s_Pill != null) return s_Pill;

        const int w = 256, h = 74;
        const float r = h * 0.5f;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
        };

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Distance to the capsule's spine: the segment between the two end-cap centres.
                float cx = Mathf.Clamp(x + 0.5f, r, w - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
            }

        tex.Apply();
        s_Pill = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        return s_Pill;
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
        img.material = OnTopMaterial;
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
        m_BadgeScale = weight.scale * (1f + leak); // applied in LateUpdate, under the break's punch
        m_Badge.localScale = Vector3.one * m_BadgeScale;
        if (m_Group != null) m_Group.alpha = weight.alpha;
        m_Outline.enabled = state == MoraleState.Broken;

        // Rescued inside the label's 1.5 s: "BROKEN" over a badge already back to Steady says two
        // things at once. The moment ends with the state it announced.
        if (state != MoraleState.Broken) m_BreakStart = float.NegativeInfinity;

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

    /// <summary>Where `owner`'s badge floats, for things drawn to or from it (the rescue arc).</summary>
    public static Vector3 BadgeAnchor(Transform owner) => owner.position + OFFSET;

    /// <summary>Starts the break moment (§4.1). Runs from LateUpdate, so it finishes even if they die.</summary>
    public void PlayBreak() => m_BreakStart = Time.time;

    /// <summary>One thin red ring for the climb into Stressed (§4.3).</summary>
    public void PlayStressed() => m_StressedStart = Time.time;

    private void AnimateStressRing(float t)
    {
        bool on = t >= 0f && t < STRESS_RING_SECONDS;
        if (m_StressRing.enabled != on) m_StressRing.enabled = on;
        if (!on) return;

        m_StressRing.rectTransform.sizeDelta = Vector2.one * (2f * RingRadius(t, STRESS_RING_SECONDS, RING_FROM_UNITS, STRESS_RING_MAX_RADIUS));
        var red = Hex(TDConstant.COLOR_MORALE_STRESSED);
        m_StressRing.color = new Color(red.r, red.g, red.b, RingAlpha(t, STRESS_RING_SECONDS));
    }

    /// <summary>The badge's extra scale at `t` seconds into a break: one sine bump to 1 + PUNCH_GAIN.</summary>
    public static float BreakPunch(float t)
        => t >= 0f && t < PUNCH_SECONDS ? 1f + PUNCH_GAIN * Mathf.Sin(Mathf.PI * t / PUNCH_SECONDS) : 1f;

    public static bool BreakFlash(float t) => t >= 0f && t < FLASH_SECONDS;

    /// <summary>Two blinks at the flash's rhythm, steady until LABEL_HOLD_UNTIL, then a linear fade.</summary>
    public static float LabelAlpha(float t)
    {
        if (t < 0f || t >= LABEL_SECONDS) return 0f;
        if (t < 4f * LABEL_BLINK) return (int)(t / LABEL_BLINK) % 2 == 0 ? 1f : 0f;
        if (t < LABEL_HOLD_UNTIL) return 1f;
        return 1f - (t - LABEL_HOLD_UNTIL) / (LABEL_SECONDS - LABEL_HOLD_UNTIL);
    }

    public static float RingRadius(float t, float duration, float from, float to)
        => Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));

    public static float RingAlpha(float t, float duration) => Mathf.Clamp01(1f - t / duration);

    private void AnimateBreak(float t)
    {
        m_Badge.localScale = Vector3.one * (m_BadgeScale * BreakPunch(t));

        bool flash = BreakFlash(t);
        if (m_Flash.enabled != flash) m_Flash.enabled = flash;

        bool halo = t >= 0f && t < HALO_SECONDS;
        if (m_Halo.enabled != halo) m_Halo.enabled = halo;
        if (halo)
        {
            m_Halo.rectTransform.sizeDelta = Vector2.one * (2f * RingRadius(t, HALO_SECONDS, RING_FROM_UNITS, HALO_MAX_RADIUS));
            m_Halo.color = new Color(1f, 1f, 1f, RingAlpha(t, HALO_SECONDS));
        }

        float a = LabelAlpha(t);
        bool label = a > 0f;
        if (m_Label.enabled != label) { m_Label.enabled = label; m_LabelPlate.enabled = label; }
        if (label)
        {
            m_Label.alpha = a;
            var plate = m_LabelPlate.color;
            m_LabelPlate.color = new Color(plate.r, plate.g, plate.b, a);
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

        // Here, not in Refresh: Refresh stops with TickMorale while the operator is dying, and a
        // break announced by the fatal blow must still play out.
        if (m_Badge == null) return;
        AnimateBreak(Time.time - m_BreakStart);
        AnimateStressRing(Time.time - m_StressedStart);
    }
}

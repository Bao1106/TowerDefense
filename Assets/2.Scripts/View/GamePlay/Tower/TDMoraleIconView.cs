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
    private TDMoraleBadge m_Parts; // disc, rim, backdrop, face — shared with the slot-bar card

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
    /// <summary>Creates the icon under `owner` and returns it. Null if the art is missing.</summary>
    public static TDMoraleIconView Attach(Transform owner)
    {
        if (!TDMoraleBadge.LoadSprites()) return null;

        var go = new GameObject("MoraleIcon");
        go.transform.SetParent(owner, worldPositionStays: false);

        var view = go.AddComponent<TDMoraleIconView>();
        view.Build(owner);
        return view;
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

        m_Parts = TDMoraleBadge.Build(m_Badge, OnTopMaterial);

        // The break's white flash: a layer over the disc rather than a write to its colour, so
        // the flash ending never has to know what Refresh would have put back.
        m_Flash = NewImage("Flash", m_Badge);
        m_Flash.sprite = TDMoraleBadge.DiscSprite();
        m_Flash.color = Color.white;
        m_Flash.enabled = false;
        m_Flash.rectTransform.localScale = Vector3.one * TDMoraleBadge.DISC_SCALE;
        m_Flash.rectTransform.SetSiblingIndex(m_Parts.Disc.GetSiblingIndex() + 1);

        BuildBreakParts(rect);
    }

    // Halo and label sit on the root, beside the badge: neither grows with its punch, and the
    // label stays the size the 12sp floor asked for.
    private void BuildBreakParts(RectTransform root)
    {
        m_Halo = NewImage("Halo", root);
        // ponytail: the band scales with the ring — thinner at the rim, 4 px at full size. A fixed
        // pixel width means rebuilding a mesh every frame, for a ring that lives 0.4 s.
        m_Halo.sprite = TDMoraleBadge.RingSprite(HALO_THICKNESS_UNITS / HALO_MAX_RADIUS);
        Centre(m_Halo.rectTransform, Vector2.zero, Vector2.one * (2f * RING_FROM_UNITS));
        m_Halo.enabled = false;

        m_StressRing = NewImage("StressRing", root);
        m_StressRing.sprite = TDMoraleBadge.RingSprite(STRESS_RING_THICKNESS_UNITS / STRESS_RING_MAX_RADIUS);
        Centre(m_StressRing.rectTransform, Vector2.zero, Vector2.one * (2f * RING_FROM_UNITS));
        m_StressRing.enabled = false;

        var platePos = new Vector2(0f, PIXELS * 0.5f + LABEL_GAP_UNITS + k_LabelPlate.y * 0.5f);
        m_LabelPlate = NewImage("LabelPlate", root);
        m_LabelPlate.sprite = PillSprite();
        m_LabelPlate.color = TDMoraleBadge.Hex(TDConstant.COLOR_MORALE_BROKEN_DISC);
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
        m_Label.color = TDMoraleBadge.Hex(TDConstant.COLOR_MORALE_BROKEN);
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
        if (morale == null || m_Parts == null) return;

        var state = morale.State;
        m_Parts.Refresh(morale); // sprite, fill, colours, rim — the same on the slot-bar card

        var weight = k_Weight[Mathf.Clamp((int)state, 0, k_Weight.Length - 1)];
        float beat = (Time.time - m_LeakPulseStart) / LEAK_PULSE_SECONDS;
        float leak = beat >= 0f && beat < 1f ? LEAK_PULSE_GAIN * Mathf.Sin(beat * Mathf.PI) : 0f;
        m_BadgeScale = weight.scale * (1f + leak); // applied in LateUpdate, under the break's punch
        m_Badge.localScale = Vector3.one * m_BadgeScale;
        if (m_Group != null) m_Group.alpha = weight.alpha;

        // Rescued inside the label's 1.5 s: "BROKEN" over a badge already back to Steady says two
        // things at once. The moment ends with the state it announced.
        if (state != MoraleState.Broken) m_BreakStart = float.NegativeInfinity;
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
        var red = TDMoraleBadge.Hex(TDConstant.COLOR_MORALE_STRESSED);
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

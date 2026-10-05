using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared Arknights-style diamond overlay — a single Canvas UI prefab used in two modes:
///  • Deploy   — direction picker: frame + 4 facing arrows + red CancelEdge glow on the upper-left
///               edge + Cancel button (Image + Outline + Icon + Label).
///  • Retreat  — action panel: frame + Retreat button at the upper-left.
///
/// Pure Canvas UI (no SpriteRenderer, no LineRenderer, no billboarding). Every frame the panel
/// follows the operator's screen position via Camera.WorldToScreenPoint(cellCenter + Up * Y_OFFSET).
/// Buttons are normal Unity Buttons — clicks work natively without any custom raycast.
///
/// Animations (DOTween, all SetUpdate(true) so they survive the pause set by Victory/GameOver):
///   • Show     — root scale 0.6 → 1 OutBack + alpha 0 → 1 OutCubic
///   • Hide     — root scale 1 → 0.5 InBack + alpha 1 → 0 InCubic, then SetActive(false)
///   • Drop     — Cancel button slides in from outside the upper-left tip + glow fades in
///   • Retreat  — Retreat button slides in from upper-left
///   • Facing   — active arrow gets DOPunchScale on switch
///
/// Public API unchanged: ShowDeploy / ShowRetreat / Hide / SetActiveDirection
///                       / IsInDeadZone / CenterWorld / DeadZoneRadius.
/// Direction index (matches CONFIG_TOWER_ROTATIONS): 0=+Z, 1=+X, 2=-Z, 3=-X.
/// </summary>
public class TDDiamondPanelView : MonoBehaviour
{
    // Single live diamond per scene; resolved lazily so callers don't depend on Awake order.
    private static TDDiamondPanelView s_Instance;
    public static TDDiamondPanelView Instance
    {
        get
        {
            if (s_Instance == null) s_Instance = FindObjectOfType<TDDiamondPanelView>(true);
            return s_Instance;
        }
    }

    private void Awake()
    {
        s_Instance = this;

        // Parent Container may have a LayoutGroup that fights with LateUpdate's
        // WorldAnchoredUI.PositionAt — layout rebuild snaps us to the layout slot
        // (bottom-left), then LateUpdate corrects to the world-projected position,
        // producing visible jitter every frame. Opt out of the layout entirely.
        var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        // Prefab ships ACTIVE so designers can see it in Edit mode, but at runtime the
        // diamond must always start hidden — only ShowDeploy/ShowRetreat make it visible.
        // Doing this in Awake (not relying on a follow-up Hide() call) closes every race
        // where the GO could be observed active for one frame at canvas (0,0).
        gameObject.SetActive(false);
        m_Mode = Mode.Hidden;
    }

    private static Camera s_Cam;
    private static Camera Cam => s_Cam != null ? s_Cam : (s_Cam = Camera.main);

    // Authored children
    private RectTransform m_Rect;
    private CanvasGroup m_CanvasGroup;
    private readonly RectTransform[] m_Arrows = new RectTransform[4];
    private GameObject m_CancelEdgeGlow;
    private Image m_CancelGlowImg;
    private Button m_CancelBtn;
    private Button m_RetreatBtn;
    private GameObject m_CancelGo;
    private GameObject m_RetreatGo;
    private RectTransform m_CancelRect;
    private RectTransform m_RetreatRect;
    private Vector2 m_CancelRestPos;
    private Vector2 m_RetreatRestPos;
    private float m_GlowRestAlpha;

    // Retreat can be refused (a collapsed operator cannot walk off) and has to SAY so.
    private CanvasGroup m_RetreatGroup;
    private TMPro.TMP_Text m_RetreatLabel;
    private string m_RetreatLabelDefault;

    private Canvas m_Canvas;
    private Camera m_CanvasCam; // null for Overlay canvases

    // Rescue corner — cloned from RetreatButton in BuildRescueButton, never authored in the prefab.
    private GameObject m_RescueGo;
    private RectTransform m_RescueRect;
    private Vector2 m_RescueRestPos;
    private Button m_RescueBtn;
    private CanvasGroup m_RescueGroup;
    private TMPro.TMP_Text m_RescueLabel;
    private Image m_RescueFill;
    private System.Func<float> m_RescueProgress;

    private Action m_OnCancel;
    private Action m_OnRetreat;
    private Action m_OnRescue;

    private Vector3 m_CenterWorld; // ground cell center (touch math)
    private enum Mode { Hidden, Deploy, Retreat }
    private Mode m_Mode = Mode.Hidden;
    private int m_CurrentDirIndex = -1;

    public Vector3 CenterWorld => m_CenterWorld;
    public float DeadZoneRadius { get; private set; }

    // ── Animation knobs ─────────────────────────────────────────────────────────
    private const float SHOW_DUR        = 0.22f;
    private const float HIDE_DUR        = 0.14f;
    private const float SHOW_FROM_SCALE = 0.6f;
    private const float HIDE_TO_SCALE   = 0.5f;
    private const float BTN_SLIDE_PX    = 60f;   // how far the button slides in from outside
    private const float ARROW_PUNCH_AMT = 0.30f; // additive scale on punch
    private const float ARROW_PUNCH_DUR = 0.22f;
    private const float RESCUE_DIM_ALPHA = 0.6f;
    private const string RESCUE_ICON_PATH = "Sprites/Morale/icon_rescue_white";

    private Tween m_ScaleTween;
    private Tween m_AlphaTween;
    private Tween m_BtnSlideTween;
    private Tween m_GlowTween;
    private Tween m_ArrowPunchTween;
    private Tween m_RescueSlideTween;

    private bool m_Resolved;

    // ── Resolve authored children ────────────────────────────────────────────────

    private void EnsureResolved()
    {
        if (m_Resolved) return;
        m_Resolved = true;

        m_Rect = (RectTransform)transform;
        m_CanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        m_Canvas = GetComponentInParent<Canvas>();
        if (m_Canvas != null && m_Canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            m_CanvasCam = m_Canvas.worldCamera;

        for (int i = 0; i < 4; i++)
            m_Arrows[i] = transform.Find($"Arrow_{i}") as RectTransform;

        m_CancelEdgeGlow = transform.Find("CancelEdgeGlow")?.gameObject;
        m_CancelGlowImg = m_CancelEdgeGlow != null ? m_CancelEdgeGlow.GetComponent<Image>() : null;
        m_GlowRestAlpha = m_CancelGlowImg != null ? m_CancelGlowImg.color.a : 0.55f;

        var cancelT = transform.Find("CancelButton");
        if (cancelT != null)
        {
            m_CancelGo = cancelT.gameObject;
            m_CancelRect = (RectTransform)cancelT;
            m_CancelRestPos = m_CancelRect.anchoredPosition;
            m_CancelBtn = cancelT.GetComponent<Button>();
            m_CancelBtn?.onClick.AddListener(() => m_OnCancel?.Invoke());
        }
        var retreatT = transform.Find("RetreatButton");
        if (retreatT != null)
        {
            m_RetreatGo = retreatT.gameObject;
            m_RetreatRect = (RectTransform)retreatT;

            // Scaled off the authored position — see DIAMOND_ACTION_SPREAD for the trade-off
            // between reading as part of the diamond and not sitting on a neighbouring operator.
            m_RetreatRestPos = m_RetreatRect.anchoredPosition * TDConstant.DIAMOND_ACTION_SPREAD;
            m_RetreatRect.anchoredPosition = m_RetreatRestPos;
            m_RetreatBtn = retreatT.GetComponent<Button>();
            m_RetreatBtn?.onClick.AddListener(() => m_OnRetreat?.Invoke());

            BuildRescueButton(retreatT);

            // After the clone, so the Rescue copy does not inherit a group it never asked for.
            var retreatGo = retreatT.gameObject;
            m_RetreatGroup = retreatGo.TryGetComponent(out CanvasGroup group) ? group : retreatGo.AddComponent<CanvasGroup>();
            m_RetreatLabel = retreatGo.GetComponentInChildren<TMPro.TMP_Text>(true);
            m_RetreatLabelDefault = m_RetreatLabel != null ? m_RetreatLabel.text : null;
        }

        float cell = TDGridMainModel.api != null ? TDGridMainModel.api.cellSize : TDConstant.CONFIG_GRID_CELL_SIZE;
        DeadZoneRadius = cell * TDConstant.DIAMOND_DEADZONE_RATIO;
    }

    // ── Rescue button (§06 · 3.2) ───────────────────────────────────────────────

    /// <summary>
    /// Cloned from RetreatButton at runtime and mirrored to the opposite vertical tip.
    ///
    /// Cloned rather than authored because §06 asks for a second button on the SAME panel and no
    /// new prefab: a copy inherits the frame, outline, icon and label styling for free and cannot
    /// drift from its sibling when someone restyles the diamond. Everything but position and text
    /// is whatever RetreatButton is.
    ///
    /// Position is not a layout choice. Retreat sits at the upper-left tip, so Rescue takes the
    /// LOWER-left one: opposed along the vertical axis is the furthest two touch targets can be
    /// from each other on this panel. In a real-time game the player presses from muscle memory,
    /// and one mis-tap retreats the operator they meant to save.
    /// </summary>
    private void BuildRescueButton(Transform retreatT)
    {
        var go = Instantiate(retreatT.gameObject, retreatT.parent);
        go.name = "RescueButton";

        m_RescueGo = go;
        m_RescueRect = (RectTransform)go.transform;
        m_RescueRestPos = new Vector2(m_RetreatRestPos.x, -m_RetreatRestPos.y);
        m_RescueRect.anchoredPosition = m_RescueRestPos;

        m_RescueBtn = go.GetComponent<Button>();
        m_RescueBtn?.onClick.RemoveAllListeners();
        m_RescueBtn?.onClick.AddListener(() => m_OnRescue?.Invoke());

        // TryGetComponent, not `GetComponent() ?? AddComponent()`: in the Editor a missing component
        // comes back as a fake-null object that `??` does not catch. RetreatButton has no CanvasGroup,
        // so the old form left this field fake-null and the Rescue corner was never dimmed or
        // disabled in Play Mode — "40/50" stayed fully clickable.
        m_RescueGroup = go.TryGetComponent(out CanvasGroup group) ? group : go.AddComponent<CanvasGroup>();
        m_RescueLabel = go.GetComponentInChildren<TMPro.TMP_Text>(true);

        BuildRescueFill(go);
        ApplyRescueIcon(go);

        go.SetActive(false);
    }

    /// <summary>
    /// A second copy of the button's own background, drawn on top and filled bottom-up by SP.
    ///
    /// The charge IS the button, not a number beside it. "35/50" is a fact the player has to read
    /// and convert; a bar rising toward the brim is the same fact at a glance, and it updates
    /// every frame instead of freezing at whatever it said when the panel opened. It also uses
    /// the drain/fill language the morale icon already taught.
    ///
    /// Cloned from the base image so it matches the button's silhouette exactly — a rectangle
    /// would spill outside the diamond tip's shape.
    /// </summary>
    private void BuildRescueFill(GameObject buttonGo)
    {
        var baseImg = buttonGo.GetComponent<Image>();
        if (baseImg == null) return;

        var fillGo = new GameObject("SpFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)fillGo.transform;
        rt.SetParent(buttonGo.transform, worldPositionStays: false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // First child: drawn after the button's own background, before its icon and label — so
        // the charge rises behind the symbol rather than over it.
        rt.SetSiblingIndex(0);

        m_RescueFill = fillGo.GetComponent<Image>();
        m_RescueFill.sprite = baseImg.sprite;
        m_RescueFill.type = Image.Type.Filled;
        m_RescueFill.fillMethod = Image.FillMethod.Vertical;
        m_RescueFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        m_RescueFill.raycastTarget = false;
        m_RescueFill.color = Hex(TDConstant.COLOR_RESCUE);
        m_RescueFill.fillAmount = 0f;
    }

    /// <summary>
    /// Swaps in a rescue icon if one exists, and tints it so the two buttons never read as the
    /// same control. Optional on purpose: the button is usable the moment the code lands, and
    /// dropping the PNG in later needs no code change.
    /// </summary>
    private void ApplyRescueIcon(GameObject buttonGo)
    {
        var icon = buttonGo.transform.Find("Icon")?.GetComponent<Image>();
        if (icon == null) return;

        var sprite = Resources.Load<Sprite>(RESCUE_ICON_PATH);
        if (sprite != null) icon.sprite = sprite;
        icon.color = Hex(TDConstant.COLOR_RESCUE);
    }

    private static Color Hex(string hex)
        => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

    /// <summary>
    /// Applies §06's three display rules. They are rules about ATTENTION, not about permission:
    ///
    ///   no target        → hidden outright. A button that is permanently greyed teaches the
    ///                      player to stop looking at that corner, and then they miss it on the
    ///                      one occasion it matters.
    ///   target, can't    → visible, dimmed, and SAYING WHY ("35/50"). The player has to know the
    ///                      option exists and what would unlock it, or they cannot plan for it.
    ///   ready            → visible, bright.
    /// </summary>
    private void ApplyRescueState(RescueOption? option)
    {
        if (m_RescueGo == null) return;

        if (option == null)
        {
            m_RescueGo.SetActive(false);
            return;
        }

        var opt = option.Value;
        m_RescueProgress = opt.progress;
        m_RescueGo.SetActive(true);

        if (m_RescueGroup != null)
        {
            // Not dimmed to near-invisible any more — the FILL now carries "not yet", so the
            // button can stay legible while still reading as unavailable.
            m_RescueGroup.alpha = opt.ready ? 1f : RESCUE_DIM_ALPHA;
            m_RescueGroup.interactable = opt.ready;
            m_RescueGroup.blocksRaycasts = opt.ready;
        }

        if (m_RescueLabel != null) m_RescueLabel.text = opt.label;
        RefreshRescueFill();
    }

    /// <summary>
    /// Re-evaluates the Rescue corner on an already-open panel, with no show animation.
    ///
    /// Needed because the panel is built once at selection time and everything it describes keeps
    /// moving: a neighbour collapses, SP crosses the threshold, the target gets rescued by someone
    /// else. Without this the button that should have appeared stays hidden until the player
    /// deselects and taps again — which looks exactly like the feature not working.
    /// </summary>
    public void UpdateRescue(RescueOption? rescue)
    {
        if (m_Mode != Mode.Retreat || !m_Resolved) return;

        bool wasShown = m_RescueGo != null && m_RescueGo.activeSelf;

        m_OnRescue = rescue?.onRescue;
        ApplyRescueState(rescue);

        // Slide it in only when it has just appeared, so it does not re-animate every frame.
        if (!wasShown && m_RescueGo != null && m_RescueGo.activeSelf)
        {
            m_RescueSlideTween?.Kill();
            m_RescueSlideTween = AnimateButtonSlideIn(m_RescueRect, m_RescueRestPos);
        }
    }

    /// <summary>
    /// Refuses the Retreat corner and says why, or (null) restores it. A collapsed operator cannot
    /// walk off (§06), and the button used to stay bright and simply do nothing when pressed — the
    /// panel closed and the player was left guessing whether the tap had even registered.
    /// Same dimming as an unaffordable Rescue, so "not now, and here is why" reads one way.
    /// </summary>
    public void SetRetreatBlocked(string reason)
    {
        if (m_Mode != Mode.Retreat) return;

        bool blocked = reason != null;
        if (m_RetreatGroup != null)
        {
            m_RetreatGroup.alpha = blocked ? RESCUE_DIM_ALPHA : 1f;
            m_RetreatGroup.interactable = !blocked;
            m_RetreatGroup.blocksRaycasts = !blocked;
        }
        if (m_RetreatLabel != null) m_RetreatLabel.text = blocked ? reason : m_RetreatLabelDefault;
    }

    /// <summary>Driven every frame from LateUpdate — SP charges while the panel is open.</summary>
    private void RefreshRescueFill()
    {
        if (m_RescueFill == null) return;
        m_RescueFill.fillAmount = m_RescueProgress != null ? Mathf.Clamp01(m_RescueProgress()) : 0f;
    }

    /// <summary>What the panel needs to know to draw the Rescue corner. Built by the caller.</summary>
    public struct RescueOption
    {
        public bool ready;
        public string label;
        public Action onRescue;

        /// <summary>0-1 toward affording a rescue. A callback, not a value: SP climbs while the
        /// panel is open, and a number captured at open time would sit frozen and lie.</summary>
        public Func<float> progress;
    }

    // ── Modes ─────────────────────────────────────────────────────────────────

    public void ShowDeploy(Vector3 cellCenterGround, Action onCancel = null)
    {
        EnsureResolved();
        m_CenterWorld = cellCenterGround;
        m_Mode = Mode.Deploy;
        m_OnCancel = onCancel;
        m_OnRetreat = null;

        if (m_CancelEdgeGlow != null) m_CancelEdgeGlow.SetActive(true);
        if (m_CancelGo != null) m_CancelGo.SetActive(true);
        if (m_RetreatGo != null) m_RetreatGo.SetActive(false);
        if (m_RescueGo != null) m_RescueGo.SetActive(false); // deploy mode never offers Rescue
        ClearAllArrows();
        m_CurrentDirIndex = -1;

        gameObject.SetActive(true);
        UpdateScreenPosition();
        RefreshRescueFill();

        PlayShowRootAnim();
        m_BtnSlideTween?.Kill();
        m_BtnSlideTween = AnimateButtonSlideIn(m_CancelRect, m_CancelRestPos);
        AnimateGlowFadeIn();
    }

    public void ShowRetreat(Vector3 cellCenterGround, Action onRetreat)
        => ShowRetreat(cellCenterGround, onRetreat, null);

    /// <summary>
    /// Retreat corner always, Rescue corner only when `rescue` says there is something to rescue.
    /// Still Mode.Retreat — §06 is explicit that Rescue must not become a third mode: the diamond
    /// has four tips and this one uses two of them, so a new state would only widen the surface
    /// that produced the "ghost diamond" bugs already fixed here.
    /// </summary>
    public void ShowRetreat(Vector3 cellCenterGround, Action onRetreat, RescueOption? rescue)
    {
        EnsureResolved();
        m_CenterWorld = cellCenterGround;
        m_Mode = Mode.Retreat;

        m_OnRetreat = onRetreat;
        m_OnCancel = null;
        m_OnRescue = rescue?.onRescue;
        ApplyRescueState(rescue);

        // Every show starts unrefused; the caller refuses it afterwards if it must. Resetting here
        // rather than in each caller means a tower — or any future caller — can never inherit the
        // "BROKEN" label left over from the operator selected before it.
        SetRetreatBlocked(null);

        if (m_CancelEdgeGlow != null) m_CancelEdgeGlow.SetActive(false);
        ClearAllArrows();
        m_CurrentDirIndex = -1;
        if (m_CancelGo != null) m_CancelGo.SetActive(false);
        if (m_RetreatGo != null) m_RetreatGo.SetActive(true);

        gameObject.SetActive(true);
        UpdateScreenPosition();
        RefreshRescueFill();

        PlayShowRootAnim();
        m_BtnSlideTween?.Kill();
        m_BtnSlideTween = AnimateButtonSlideIn(m_RetreatRect, m_RetreatRestPos);

        // Its own tween handle: sharing m_BtnSlideTween would kill the retreat slide mid-flight
        // and strand that button between its entry position and its rest position.
        m_RescueSlideTween?.Kill();
        if (m_RescueGo != null && m_RescueGo.activeSelf)
            m_RescueSlideTween = AnimateButtonSlideIn(m_RescueRect, m_RescueRestPos);
    }

    public void Hide()
    {
        bool wasShowing = m_Mode != Mode.Hidden;
        m_Mode = Mode.Hidden;
        m_OnCancel = null;
        m_OnRetreat = null;
        m_OnRescue = null;
        m_RescueProgress = null;

        if (!gameObject.activeSelf) return; // already inactive — nothing to do

        if (!m_Resolved)
        {
            // First Hide() runs before any Show() (called from TDDeployController.Init):
            // children haven't been resolved yet and there's nothing to animate away.
            // Disable the root directly so the prefab's authored children don't flash on screen.
            gameObject.SetActive(false);
            return;
        }

        if (!wasShowing) return; // already hiding — don't restart the tween
        PlayHideRootAnim();
    }

    public void SetActiveDirection(int dirIndex)
    {
        for (int i = 0; i < 4; i++)
            if (m_Arrows[i] != null) m_Arrows[i].gameObject.SetActive(i == dirIndex);

        if (dirIndex >= 0 && dirIndex != m_CurrentDirIndex && m_Arrows[dirIndex] != null)
            PlayArrowPunch(m_Arrows[dirIndex]);
        m_CurrentDirIndex = dirIndex;
    }

    public bool IsInDeadZone(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - m_CenterWorld; d.y = 0f;
        return d.sqrMagnitude <= DeadZoneRadius * DeadZoneRadius;
    }

    // ── Animation primitives ────────────────────────────────────────────────────

    private void PlayShowRootAnim()
    {
        KillRootTweens();
        m_Rect.localScale = Vector3.one * SHOW_FROM_SCALE;
        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.blocksRaycasts = true;
            m_CanvasGroup.interactable = true;
        }
        m_ScaleTween = m_Rect
            .DOScale(1f, SHOW_DUR).SetEase(Ease.OutBack, 1.6f)
            .SetUpdate(true);
        if (m_CanvasGroup != null)
            m_AlphaTween = m_CanvasGroup
                .DOFade(1f, SHOW_DUR * 0.7f).SetEase(Ease.OutCubic)
                .SetUpdate(true);
    }

    private void PlayHideRootAnim()
    {
        KillRootTweens();
        if (!gameObject.activeSelf) return;
        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.blocksRaycasts = false;
            m_CanvasGroup.interactable = false;
        }
        // Use DOTween.Sequence so SetActive(false) runs OnKill too — not only OnComplete.
        // Earlier bug: a ShowDeploy/ShowRetreat racing in mid-hide would call KillRootTweens(),
        // which prevented OnComplete from firing → diamond stayed active at canvas (0,0)
        // (a stuck ghost at bottom-left of the screen). OnKill guarantees cleanup either way.
        // The new Show call re-activates the GO anyway, so disabling here is safe.
        if (m_CanvasGroup != null)
        {
            m_AlphaTween = m_CanvasGroup
                .DOFade(0f, HIDE_DUR).SetEase(Ease.InCubic)
                .SetUpdate(true);
        }
        m_ScaleTween = m_Rect
            .DOScale(HIDE_TO_SCALE, HIDE_DUR).SetEase(Ease.InBack, 1.4f)
            .SetUpdate(true)
            .OnComplete(FinishHide);
    }

    private void FinishHide()
    {
        // Only disable if we're still in Hidden mode — a Show may have raced in already.
        if (m_Mode == Mode.Hidden) gameObject.SetActive(false);
    }

    // Slide the button in from outside the diamond toward its authored rest position.
    private Tween AnimateButtonSlideIn(RectTransform rect, Vector2 restPos)
    {
        if (rect == null) return null;
        Vector2 outward = restPos.sqrMagnitude > 0.01f ? restPos.normalized : Vector2.up;
        rect.anchoredPosition = restPos + outward * BTN_SLIDE_PX;
        return rect
            .DOAnchorPos(restPos, SHOW_DUR).SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void AnimateGlowFadeIn()
    {
        if (m_CancelGlowImg == null) return;
        m_GlowTween?.Kill();
        var c = m_CancelGlowImg.color;
        m_CancelGlowImg.color = new Color(c.r, c.g, c.b, 0f);
        m_GlowTween = m_CancelGlowImg
            .DOFade(m_GlowRestAlpha, SHOW_DUR * 1.3f).SetEase(Ease.OutCubic)
            .SetUpdate(true);
    }

    private void PlayArrowPunch(RectTransform arrow)
    {
        m_ArrowPunchTween?.Kill();
        m_RescueSlideTween?.Kill();
        arrow.localScale = Vector3.one;
        m_ArrowPunchTween = arrow
            .DOPunchScale(Vector3.one * ARROW_PUNCH_AMT, ARROW_PUNCH_DUR, 5, 0.6f)
            .SetUpdate(true);
    }

    private void KillRootTweens()
    {
        m_ScaleTween?.Kill();
        m_AlphaTween?.Kill();
    }

    private void ClearAllArrows()
    {
        for (int i = 0; i < 4; i++)
            if (m_Arrows[i] != null)
            {
                m_Arrows[i].gameObject.SetActive(false);
                m_Arrows[i].localScale = Vector3.one;
            }
    }

    private void OnDestroy()
    {
        KillRootTweens();
        m_BtnSlideTween?.Kill();
        m_GlowTween?.Kill();
        m_ArrowPunchTween?.Kill();
        m_RescueSlideTween?.Kill();
    }

    // ── Screen positioning ──────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (m_Mode == Mode.Hidden)
        {
            // Safety net: if the hide tween's OnComplete was skipped (because a Show
            // raced in and killed the tween, or any other DOTween edge case), the
            // diamond would stay active forever at a frozen screen position. As soon
            // as the alpha is essentially zero, force-disable so it can't ghost.
            if (gameObject.activeSelf && m_CanvasGroup != null && m_CanvasGroup.alpha <= 0.01f)
                gameObject.SetActive(false);
            return;
        }
        UpdateScreenPosition();
        RefreshRescueFill();
    }

    // Project the operator's body-height anchor onto the screen and move the diamond there.
    private void UpdateScreenPosition()
    {
        var cam = Cam;
        if (cam == null || m_Canvas == null) return;
        Vector3 worldAnchor = m_CenterWorld + Vector3.up * TDConstant.DIAMOND_CENTER_Y_OFFSET;
        WorldAnchoredUI.PositionAt(m_Rect, worldAnchor, m_Canvas, cam);
    }
}

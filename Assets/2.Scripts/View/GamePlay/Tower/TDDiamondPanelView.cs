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

    private Canvas m_Canvas;
    private Camera m_CanvasCam; // null for Overlay canvases

    private Action m_OnCancel;
    private Action m_OnRetreat;

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

    private Tween m_ScaleTween;
    private Tween m_AlphaTween;
    private Tween m_BtnSlideTween;
    private Tween m_GlowTween;
    private Tween m_ArrowPunchTween;

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
            m_RetreatRestPos = m_RetreatRect.anchoredPosition;
            m_RetreatBtn = retreatT.GetComponent<Button>();
            m_RetreatBtn?.onClick.AddListener(() => m_OnRetreat?.Invoke());
        }

        float cell = TDGridMainModel.api != null ? TDGridMainModel.api.cellSize : TDConstant.CONFIG_GRID_CELL_SIZE;
        DeadZoneRadius = cell * TDConstant.DIAMOND_DEADZONE_RATIO;
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
        ClearAllArrows();
        m_CurrentDirIndex = -1;

        gameObject.SetActive(true);
        UpdateScreenPosition();

        PlayShowRootAnim();
        AnimateButtonSlideIn(m_CancelRect, m_CancelRestPos);
        AnimateGlowFadeIn();
    }

    public void ShowRetreat(Vector3 cellCenterGround, Action onRetreat)
    {
        EnsureResolved();
        m_CenterWorld = cellCenterGround;
        m_Mode = Mode.Retreat;
        
        m_OnRetreat = onRetreat;
        m_OnCancel = null;

        if (m_CancelEdgeGlow != null) m_CancelEdgeGlow.SetActive(false);
        ClearAllArrows();
        m_CurrentDirIndex = -1;
        if (m_CancelGo != null) m_CancelGo.SetActive(false);
        if (m_RetreatGo != null) m_RetreatGo.SetActive(true);

        gameObject.SetActive(true);
        UpdateScreenPosition();

        PlayShowRootAnim();
        AnimateButtonSlideIn(m_RetreatRect, m_RetreatRestPos);
    }

    public void Hide()
    {
        bool wasShowing = m_Mode != Mode.Hidden;
        m_Mode = Mode.Hidden;
        m_OnCancel = null;
        m_OnRetreat = null;

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
    private void AnimateButtonSlideIn(RectTransform rect, Vector2 restPos)
    {
        if (rect == null) return;
        m_BtnSlideTween?.Kill();
        Vector2 outward = restPos.sqrMagnitude > 0.01f ? restPos.normalized : Vector2.up;
        rect.anchoredPosition = restPos + outward * BTN_SLIDE_PX;
        m_BtnSlideTween = rect
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

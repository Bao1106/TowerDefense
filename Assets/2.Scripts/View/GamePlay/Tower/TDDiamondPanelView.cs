using System;
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
/// Public API unchanged from earlier hybrid version so the deploy controller doesn't change:
///   ShowDeploy / ShowRetreat / Hide / SetActiveDirection / IsInDeadZone / CenterWorld / DeadZoneRadius.
///
/// Direction index (matches CONFIG_TOWER_ROTATIONS): 0=+Z, 1=+X, 2=-Z, 3=-X.
/// </summary>
public class TDDiamondPanelView : MonoBehaviour
{
    // Single live diamond per scene; resolved lazily so callers don't depend on Awake order.
    private static TDDiamondPanelView m_Instance;
    public static TDDiamondPanelView Instance
    {
        get
        {
            if (m_Instance == null) m_Instance = FindObjectOfType<TDDiamondPanelView>(true);
            return m_Instance;
        }
    }

    private void Awake() => m_Instance = this;

    private static Camera m_Camera;
    private static Camera Cam => m_Camera != null ? m_Camera : (m_Camera = Camera.main);

    // Authored children
    private RectTransform m_Rect;
    private CanvasGroup m_CanvasGroup;
    private readonly RectTransform[] m_Arrows = new RectTransform[4];
    private GameObject m_CancelEdgeGlow;
    private Button m_CancelBtn;
    private Button m_RetreatBtn;
    private GameObject m_CancelGo;
    private GameObject m_RetreatGo;

    private Canvas m_Canvas;
    private Camera m_CanvasCam; // null for Overlay canvases

    private Action m_OnCancel;
    private Action m_OnRetreat;

    private Vector3 m_CenterWorld; // ground cell center (touch math)
    private enum Mode { Hidden, Deploy, Retreat }
    private Mode m_Mode = Mode.Hidden;

    public Vector3 CenterWorld => m_CenterWorld;
    public float DeadZoneRadius { get; private set; }

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

        var cancelT = transform.Find("CancelButton");
        if (cancelT != null)
        {
            m_CancelGo = cancelT.gameObject;
            m_CancelBtn = cancelT.GetComponent<Button>();
            m_CancelBtn?.onClick.AddListener(() => m_OnCancel?.Invoke());
        }
        var retreatT = transform.Find("RetreatButton");
        if (retreatT != null)
        {
            m_RetreatGo = retreatT.gameObject;
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
        SetActiveDirection(-1);
        gameObject.SetActive(true);
        UpdateScreenPosition();
    }

    public void ShowRetreat(Vector3 cellCenterGround, Action onRetreat)
    {
        EnsureResolved();
        m_CenterWorld = cellCenterGround;
        m_Mode = Mode.Retreat;
        m_OnRetreat = onRetreat;
        m_OnCancel = null;
        if (m_CancelEdgeGlow != null) m_CancelEdgeGlow.SetActive(false);
        for (int i = 0; i < 4; i++)
            if (m_Arrows[i] != null) m_Arrows[i].gameObject.SetActive(false);
        if (m_CancelGo != null) m_CancelGo.SetActive(false);
        if (m_RetreatGo != null) m_RetreatGo.SetActive(true);
        gameObject.SetActive(true);
        UpdateScreenPosition();
    }

    public void Hide()
    {
        m_Mode = Mode.Hidden;
        m_OnCancel = null;
        m_OnRetreat = null;
        gameObject.SetActive(false);
    }

    public void SetActiveDirection(int dirIndex)
    {
        for (int i = 0; i < 4; i++)
            if (m_Arrows[i] != null) m_Arrows[i].gameObject.SetActive(i == dirIndex);
    }

    public bool IsInDeadZone(Vector3 worldPoint)
    {
        Vector3 d = worldPoint - m_CenterWorld; d.y = 0f;
        return d.sqrMagnitude <= DeadZoneRadius * DeadZoneRadius;
    }

    // ── Screen positioning ──────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (m_Mode == Mode.Hidden) return;
        UpdateScreenPosition();
    }

    // Project the operator's body-height anchor onto the screen and move the diamond there.
    private void UpdateScreenPosition()
    {
        var cam = Cam;
        if (cam == null || m_Canvas == null) return;

        Vector3 worldAnchor = m_CenterWorld + Vector3.up * TDConstant.DIAMOND_CENTER_Y_OFFSET;
        Vector2 screenPt = cam.WorldToScreenPoint(worldAnchor);
        WorldAnchoredUI.PositionAt(m_Rect, worldAnchor, m_Canvas, cam);
    }
}

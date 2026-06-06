using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Gắn trên Canvas/SafeArea/Container — cùng cấp với TDGameplayHUDView.
/// Detect tap vào operator đã đặt → hiện OperatorActionPanel tại vị trí operator.
/// Tap ra ngoài → ẩn panel và bỏ chọn.
/// </summary>
public class TDOperatorSelectionView : MonoBehaviour
{
    private TDOperatorView    m_SelectedOperator;
    private TDTowerWeaponView m_SelectedTower;
    private RectTransform     m_ActionPanel;
    private CanvasGroup       m_PanelCanvasGroup;
    private Sequence          m_PanelTween;
    private Vector3           m_PanelBaseScale;
    private Button            m_BtnRetreat;
    private Canvas            m_Canvas;

    // Range highlight pool
    private GameObject        m_RangeHighlightPrefab;
    private readonly List<GameObject> m_RangeHighlightTiles = new List<GameObject>();

    // Helper: có unit nào đang được chọn không
    private bool HasSelected => m_SelectedOperator != null || m_SelectedTower != null;

    private void Start()
    {
        m_Canvas      = GetComponentInParent<Canvas>();
        m_ActionPanel = transform.Find(TDConstant.PATH_OPERATOR_ACTION_PANEL)?.GetComponent<RectTransform>();
        m_BtnRetreat  = transform.Find(TDConstant.PATH_OPERATOR_BTN_RETREAT)?.GetComponent<Button>();

        if (m_ActionPanel != null)
        {
            m_PanelBaseScale   = m_ActionPanel.localScale;
            m_PanelCanvasGroup = m_ActionPanel.GetComponent<CanvasGroup>()
                              ?? m_ActionPanel.gameObject.AddComponent<CanvasGroup>();
            m_ActionPanel.gameObject.SetActive(false);
        }

        m_BtnRetreat?.onClick.AddListener(OnRetreatClicked);
        m_RangeHighlightPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_RANGE_HIGH_LIGHT);
    }

    private void Update()
    {
        if (TDGameStateControl.api != null && TDGameStateControl.api.IsGameEnded) return;

        HandleTapInput();

        if (HasSelected && m_ActionPanel != null)
            UpdatePanelPosition();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void HandleTapInput()
    {
        bool tapped = false;
        Vector2 screenPos = Vector2.zero;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            tapped    = true;
            screenPos = Input.GetTouch(0).position;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            tapped    = true;
            screenPos = Input.mousePosition;
        }
#endif

        if (!tapped) return;
        if (TDSlotHolderMainView.IsPlacingUnit) return;
        if (IsPointerOverUI(screenPos)) return;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var op = hit.collider.GetComponentInParent<TDOperatorView>();
            if (op != null) { SelectOperator(op); return; }

            var tower = hit.collider.GetComponentInParent<TDTowerWeaponView>();
            if (tower != null) { SelectTower(tower); return; }
        }

        Deselect();
    }

    // ── Select / Deselect ─────────────────────────────────────────────────────

    private void SelectOperator(TDOperatorView op)
    {
        if (m_SelectedOperator == op) return;
        DeselectImmediate();
        m_SelectedOperator = op;
        m_SelectedOperator.SetSelected(true);
        UpdatePanelPosition();
        ShowPanel();
        ShowOperatorRange(op);
    }

    private void SelectTower(TDTowerWeaponView tower)
    {
        if (m_SelectedTower == tower) return;
        DeselectImmediate();
        m_SelectedTower = tower;
        UpdatePanelPosition();
        ShowPanel();
    }

    public void Deselect()
    {
        if (m_SelectedOperator != null)
        {
            m_SelectedOperator.SetSelected(false);
            m_SelectedOperator = null;
        }
        m_SelectedTower = null;
        HidePanel();
        HideRangeHighlights();
    }

    // Deselect không animation — dùng khi ngay sau đó sẽ show selection mới
    private void DeselectImmediate()
    {
        if (m_SelectedOperator != null)
        {
            m_SelectedOperator.SetSelected(false);
            m_SelectedOperator = null;
        }
        m_SelectedTower = null;
        HideRangeHighlights();
    }

    // ── Panel transitions ─────────────────────────────────────────────────────

    private void ShowPanel()
    {
        if (m_ActionPanel == null) return;
        m_PanelTween?.Kill();
        m_ActionPanel.gameObject.SetActive(true);
        m_ActionPanel.localScale = Vector3.zero;

        if (m_PanelCanvasGroup != null)
        {
            m_PanelCanvasGroup.alpha          = 0f;
            m_PanelCanvasGroup.interactable   = false;
            m_PanelCanvasGroup.blocksRaycasts = false;
        }

        m_PanelTween = DOTween.Sequence().SetUpdate(true);
        m_PanelTween.Join(m_ActionPanel.DOScale(m_PanelBaseScale, 0.25f).SetEase(Ease.OutBack));
        if (m_PanelCanvasGroup != null)
            m_PanelTween.Join(m_PanelCanvasGroup.DOFade(1f, 0.16f).SetEase(Ease.OutCubic));
        m_PanelTween.OnComplete(() =>
        {
            if (m_PanelCanvasGroup != null)
            {
                m_PanelCanvasGroup.interactable   = true;
                m_PanelCanvasGroup.blocksRaycasts = true;
            }
        });
    }

    private void HidePanel()
    {
        if (m_ActionPanel == null || !m_ActionPanel.gameObject.activeSelf) return;
        m_PanelTween?.Kill();

        if (m_PanelCanvasGroup != null)
        {
            m_PanelCanvasGroup.interactable   = false;
            m_PanelCanvasGroup.blocksRaycasts = false;
        }

        m_PanelTween = DOTween.Sequence().SetUpdate(true);
        m_PanelTween.Join(m_ActionPanel.DOScale(Vector3.zero, 0.16f).SetEase(Ease.InBack));
        if (m_PanelCanvasGroup != null)
            m_PanelTween.Join(m_PanelCanvasGroup.DOFade(0f, 0.12f).SetEase(Ease.InCubic));
        m_PanelTween.OnComplete(() => m_ActionPanel.gameObject.SetActive(false));
    }

    // ── Range Highlights ──────────────────────────────────────────────────────

    private void ShowOperatorRange(TDOperatorView op)
    {
        HideRangeHighlights();
        if (m_RangeHighlightPrefab == null || TDGridMainModel.api == null) return;

        var data = TDFlyweightOperatorDataSettings.api.GetData(op.OperatorType);
        if (data?.rangeOffsets == null || data.rangeOffsets.Length == 0) return;

        var        rangeDto = new TDOffsetRangeDTO(data.rangeOffsets);
        Vector2Int cell     = TDGridMainModel.api.WorldToCell(op.transform.position);
        var        cells    = rangeDto.GetCellsInRange(cell, op.transform.rotation);

        // Grow pool on demand
        while (m_RangeHighlightTiles.Count < cells.Count)
        {
            var tile = Instantiate(m_RangeHighlightPrefab);
            tile.SetActive(false);
            m_RangeHighlightTiles.Add(tile);
        }

        for (int i = 0; i < m_RangeHighlightTiles.Count; i++)
        {
            if (i < cells.Count)
            {
                Vector3 world = TDGridMainModel.api.CellToWorld(cells[i]);
                m_RangeHighlightTiles[i].transform.position = new Vector3(world.x, TDConstant.CONFIG_RANGE_HIGHLIGHT_Y, world.z);
                m_RangeHighlightTiles[i].SetActive(true);
            }
            else
            {
                m_RangeHighlightTiles[i].SetActive(false);
            }
        }
    }

    private void HideRangeHighlights()
    {
        foreach (var tile in m_RangeHighlightTiles)
            if (tile != null) tile.SetActive(false);
    }

    // ── Panel position ────────────────────────────────────────────────────────

    private void UpdatePanelPosition()
    {
        if (!HasSelected || Camera.main == null) return;

        Vector3 worldAnchor = m_SelectedOperator != null
            ? GetIndicatorUpperLeftEdge(m_SelectedOperator)
            : m_SelectedTower.transform.position + Vector3.up * TDConstant.OPERATOR_PANEL_WORLD_Y_OFFSET;

        Vector2 screenPt = Camera.main.WorldToScreenPoint(worldAnchor);

        if (m_Canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            m_ActionPanel.position = screenPt;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_Canvas.GetComponent<RectTransform>(), screenPt,
                m_Canvas.worldCamera, out Vector2 localPt);
            m_ActionPanel.localPosition = localPt;
        }
    }

    // Tìm midpoint của cạnh upper-left của diamond indicator trong world space.
    // Diamond có 4 tips = 4 midpoint của cạnh Quad → dùng transform.right và transform.up.
    // Project 4 tips lên screen → tip cao nhất (topIdx) và trái nhất (leftIdx) → midpoint = upper-left edge center.
    private static Vector3 GetIndicatorUpperLeftEdge(TDOperatorView op)
    {
        Transform t = op.SelectionIndicatorTransform;
        if (t == null)
            return op.transform.position + Vector3.up * TDConstant.OPERATOR_PANEL_WORLD_Y_OFFSET;

        Vector3 center = t.position;
        float   halfS  = t.lossyScale.x * 0.5f;

        Vector3[] tips =
        {
            center + t.right * halfS,
            center + t.up    * halfS,
            center - t.right * halfS,
            center - t.up    * halfS,
        };

        Camera cam     = Camera.main;
        int    topIdx  = 0;
        int    leftIdx = 0;
        for (int i = 1; i < tips.Length; i++)
        {
            if (cam.WorldToScreenPoint(tips[i]).y > cam.WorldToScreenPoint(tips[topIdx]).y)  topIdx  = i;
            if (cam.WorldToScreenPoint(tips[i]).x < cam.WorldToScreenPoint(tips[leftIdx]).x) leftIdx = i;
        }

        return (tips[topIdx] + tips[leftIdx]) * 0.5f;
    }

    // ── Retreat ───────────────────────────────────────────────────────────────

    private void OnRetreatClicked()
    {
        if (m_SelectedOperator != null)
            TDOperatorRetreatControl.api?.Retreat(m_SelectedOperator);
        else if (m_SelectedTower != null)
            TDTowerRetreatControl.api?.Retreat(m_SelectedTower);
        Deselect();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // RaycastAll tại screenPos — đáng tin hơn IsPointerOverGameObject(fingerId) trên Android
    private static readonly List<RaycastResult> s_RaycastResults = new List<RaycastResult>();
    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        var eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        s_RaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, s_RaycastResults);
        return s_RaycastResults.Count > 0;
    }
}

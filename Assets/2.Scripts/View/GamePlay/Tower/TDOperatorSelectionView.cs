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
    private Button            m_BtnRetreat;
    private Canvas            m_Canvas;

    // Helper: có unit nào đang được chọn không
    private bool HasSelected => m_SelectedOperator != null || m_SelectedTower != null;

    private void Start()
    {
        m_Canvas      = GetComponentInParent<Canvas>();
        m_ActionPanel = transform.Find(TDConstant.PATH_OPERATOR_ACTION_PANEL)?.GetComponent<RectTransform>();
        m_BtnRetreat  = transform.Find(TDConstant.PATH_OPERATOR_BTN_RETREAT)?.GetComponent<Button>();

        m_BtnRetreat?.onClick.AddListener(OnRetreatClicked);
        m_ActionPanel?.gameObject.SetActive(false);
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
        Deselect();
        m_SelectedOperator = op;
        m_SelectedOperator.SetSelected(true);
        m_ActionPanel?.gameObject.SetActive(true);
        UpdatePanelPosition();
    }

    private void SelectTower(TDTowerWeaponView tower)
    {
        if (m_SelectedTower == tower) return;
        Deselect();
        m_SelectedTower = tower;
        m_ActionPanel?.gameObject.SetActive(true);
        UpdatePanelPosition();
    }

    public void Deselect()
    {
        if (m_SelectedOperator != null)
        {
            m_SelectedOperator.SetSelected(false);
            m_SelectedOperator = null;
        }
        m_SelectedTower = null;
        m_ActionPanel?.gameObject.SetActive(false);
    }

    // ── Panel position ────────────────────────────────────────────────────────

    private void UpdatePanelPosition()
    {
        if (!HasSelected || Camera.main == null) return;

        Transform anchor = m_SelectedOperator != null
            ? m_SelectedOperator.transform
            : m_SelectedTower.transform;

        Vector3 worldAnchor = anchor.position
                              + Vector3.up * TDConstant.OPERATOR_PANEL_WORLD_Y_OFFSET;
        Vector2 screenPt    = Camera.main.WorldToScreenPoint(worldAnchor);

        // Shift sang trái và lên để không che khuất operator
        screenPt += new Vector2(-70f, 30f);

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

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (Input.touchCount > 0)
            return EventSystem.current != null &&
                   EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Attached to Canvas/SafeArea/Container. Detects taps on a placed operator or tower → shows
/// the shared diamond panel (TDDiamondPanelView) in Retreat mode at the unit's position.
/// Tapping outside, or starting a new deploy (TDGameEventBus.OnUnitPickup), clears the selection.
///
/// Pha 4 replaced the dedicated OperatorActionPanel chip with the same diamond used by deploy —
/// one visual language, one positioning path, no overlap edge cases.
/// </summary>
public class TDOperatorSelectionView : MonoBehaviour
{
    private static Camera s_MainCam;
    private static Camera MainCam => s_MainCam != null ? s_MainCam : (s_MainCam = Camera.main);

    private TDOperatorView m_SelectedOperator;
    private TDTowerWeaponView m_SelectedTower;

    // Range highlight pool
    private GameObject m_RangeHighlightPrefab;
    private readonly List<GameObject> m_RangeHighlightTiles = new List<GameObject>();

    private bool HasSelected => m_SelectedOperator != null || m_SelectedTower != null;

    private void Start()
    {
        EnhancedTouchSupport.Enable();
        m_RangeHighlightPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_RANGE_HIGH_LIGHT);

        // Drop the current selection whenever the player picks a new unit to deploy
        // → the diamond cannot overlap itself in two different modes.
        TDGameEventBus.OnUnitPickup += Deselect;
        // Also drop selection when the run ends so the diamond doesn't linger over popups.
        TDGameEventBus.OnVictory  += Deselect;
        TDGameEventBus.OnGameOver += Deselect;
    }

    private void OnDestroy()
    {
        EnhancedTouchSupport.Disable();
        TDGameEventBus.OnUnitPickup -= Deselect;
        TDGameEventBus.OnVictory  -= Deselect;
        TDGameEventBus.OnGameOver -= Deselect;
    }

    private void Update()
    {
        if (TDGameStateControl.api != null && TDGameStateControl.api.IsGameEnded) return;
        HandleTapInput();
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void HandleTapInput()
    {
        bool tapped = false;
        Vector2 screenPos = Vector2.zero;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Touch.activeTouches.Count > 0 && Touch.activeTouches[0].phase == TouchPhase.Began)
        {
            tapped = true;
            screenPos = Touch.activeTouches[0].screenPosition;
        }
#else
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            tapped = true;
            screenPos = Mouse.current.position.ReadValue();
        }
#endif

        if (!tapped) return;
        if (TDDeployController.IsPlacingUnit) return;
        if (IsPointerOverUI(screenPos)) return;

        Ray ray = MainCam.ScreenPointToRay(screenPos);
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
        // The diamond panel IS the selection indicator now — the operator's own gold
        // SelectionIndicator quad is no longer needed and would visually clash with the diamond.
        ShowOperatorRange(op);
        TDDiamondPanelView.Instance?.ShowRetreat(op.transform.position, OnRetreatClicked);
    }

    private void SelectTower(TDTowerWeaponView tower)
    {
        if (m_SelectedTower == tower) return;
        DeselectImmediate();
        m_SelectedTower = tower;
        TDDiamondPanelView.Instance?.ShowRetreat(tower.transform.position, OnRetreatClicked);
    }

    public void Deselect()
    {
        m_SelectedOperator = null;
        m_SelectedTower = null;
        TDDiamondPanelView.Instance?.Hide();
        HideRangeHighlights();
    }

    // Used when a new selection is about to replace the current one — no diamond hide,
    // the upcoming ShowRetreat call repositions/re-shows it in one step.
    private void DeselectImmediate()
    {
        m_SelectedOperator = null;
        m_SelectedTower = null;
        HideRangeHighlights();
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

    // ── Range Highlights ──────────────────────────────────────────────────────

    private void ShowOperatorRange(TDOperatorView op)
    {
        HideRangeHighlights();
        if (m_RangeHighlightPrefab == null || TDGridMainModel.api == null) return;

        var data = TDFlyweightOperatorDataSettings.api.GetData(op.OperatorType);
        if (data?.rangeOffsets == null || data.rangeOffsets.Length == 0) return;

        var rangeDto = new TDOffsetRangeDTO(data.rangeOffsets);
        Vector2Int cell = TDGridMainModel.api.WorldToCell(op.transform.position);
        var cells = rangeDto.GetCellsInRange(cell, op.transform.rotation);

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
            else m_RangeHighlightTiles[i].SetActive(false);
        }
    }

    private void HideRangeHighlights()
    {
        foreach (var tile in m_RangeHighlightTiles)
            if (tile != null) tile.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

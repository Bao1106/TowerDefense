using System.Collections.Generic;
using TDEnums;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Object = UnityEngine.Object;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Owns the deploy-by-drag flow (Arknights style). Extracted out of TDSlotHolderMainView
/// so the slot bar only builds buttons; this drives ghost + highlights + the diamond picker.
///
/// MOBILE — explicit two-gesture model (no hidden hold/micro-lift timers):
///   Dragging:        ghost follows finger, snaps to cell.
///   ↳ release on a valid cell  → DROP: cell locked, finger lifts, diamond shown (DirectionSelect).
///   ↳ release on invalid cell  → cancel.
///   DirectionSelect: a NEW touch drags/taps from the cell to choose facing (live arrow + range).
///   ↳ release outside dead-zone → commit that facing.
///   ↳ release inside  dead-zone → cancel.
///
/// DESKTOP — unchanged: ghost follows mouse, facing follows mouse offset, LMB places,
///   E/Q rotate, RMB cancels.
/// </summary>
public class TDDeployController : MonoBehaviour
{
    public static bool IsPlacingUnit { get; private set; }

    private static Camera s_MainCam;
    private static Camera MainCam => s_MainCam != null ? s_MainCam : (s_MainCam = Camera.main);

    private DeployState m_State = DeployState.Idle;

    private GameObject m_Ghost;
    private TowerType m_TowerType;
    private OperatorType m_OperatorType;
    private int m_SlotIndex = -1;
    private int m_RotationIndex;

    private Vector3 m_LastSnappedPos = Vector3.negativeInfinity;
    private Vector3 m_DroppedCenter; // locked cell center (DirectionSelect)

    private int m_DragFingerId = -1; // Dragging phase finger
    private int m_DirFingerId = -1;  // DirectionSelect phase finger (a NEW touch)

    // Highlights
    private GameObject m_TowerHighlightPrefab;
    private GameObject m_RangeHighlightPrefab;
    private List<Vector3> m_ValidTowerPositions = new List<Vector3>();
    private readonly List<GameObject> m_HighlightTiles = new List<GameObject>();
    private readonly List<GameObject> m_RangeHighlightTiles = new List<GameObject>();
    private Vector2Int m_LastRangeCell = new Vector2Int(int.MinValue, int.MinValue);
    private int m_LastRangeRotIndex = -1;

    private TDDiamondPanelView m_Diamond;
    private GameObject m_PlacementPanel; // legacy Cancel button panel (kept as fallback)

    // ── Lifecycle ───────────────────────────────────────────────────────────────

    public void Init(GameObject placementPanel)
    {
        EnhancedTouchSupport.Enable();
        m_PlacementPanel = placementPanel;
        m_RangeHighlightPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_RANGE_HIGH_LIGHT);

        // Diamond panel is full Canvas UI now — parent it to the gameplay Canvas container
        // so RectTransform sizing / billboarding work with the canvas scaler.
        var diamondPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_DIAMOND_PANEL);
        Transform diamondParent = null;
        foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
            {
                diamondParent = c.transform.Find("SafeArea/Container");
                break;
            }
        }
        if (diamondPrefab != null && diamondParent != null)
            m_Diamond = Instantiate(diamondPrefab, diamondParent).GetComponent<TDDiamondPanelView>();
        else if (diamondPrefab != null)
            Debug.LogError("[TDDeployController] Canvas/SafeArea/Container not found — diamond not instantiated");
        m_Diamond?.Hide();

        TDTowerMainControl.api.onGetTowerPrefab += OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex += OnGetCurrentRotationIndex;
        TDPlaceTowerControl.api.onPlaceTowerSuccess += OnPlaceTowerSuccess;
        TDUserInputControl.api.onMouseButton0Clicked += OnPlaceClicked;
        TDUserInputControl.api.onMouseButton1Clicked += OnCancelClicked;
        TDUserInputControl.api.onMouseButtonEClicked += OnRotateCW;
        TDUserInputControl.api.onMouseButtonQClicked += OnRotateCCW;
        TDEnemyPathMainControl.api.onValidTowerCellsReady += OnValidTowerCellsReady;

        // Auto-cancel any in-progress deploy when the run ends — otherwise the diamond
        // would float over the Victory / GameOver popup.
        TDGameEventBus.OnVictory  += CancelOnGameEnd;
        TDGameEventBus.OnGameOver += CancelOnGameEnd;
        if (TDPauseControl.api != null) TDPauseControl.api.onPauseChanged += OnPauseChanged;
    }

    private void OnDestroy()
    {
        EnhancedTouchSupport.Disable();
        TDTowerMainControl.api.onGetTowerPrefab -= OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex -= OnGetCurrentRotationIndex;
        TDPlaceTowerControl.api.onPlaceTowerSuccess -= OnPlaceTowerSuccess;
        TDUserInputControl.api.onMouseButton0Clicked -= OnPlaceClicked;
        TDUserInputControl.api.onMouseButton1Clicked -= OnCancelClicked;
        TDUserInputControl.api.onMouseButtonEClicked -= OnRotateCW;
        TDUserInputControl.api.onMouseButtonQClicked -= OnRotateCCW;
        TDEnemyPathMainControl.api.onValidTowerCellsReady -= OnValidTowerCellsReady;
        TDGameEventBus.OnVictory  -= CancelOnGameEnd;
        TDGameEventBus.OnGameOver -= CancelOnGameEnd;
        if (TDPauseControl.api != null) TDPauseControl.api.onPauseChanged -= OnPauseChanged;
    }

    private void CancelOnGameEnd()
    {
        if (m_State != DeployState.Idle) CancelPlacement();
    }

    private void OnPauseChanged(bool paused)
    {
        if (paused && m_State != DeployState.Idle) CancelPlacement();
    }

    private void OnValidTowerCellsReady(List<Vector3> positions) => m_ValidTowerPositions = positions;

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (m_State == DeployState.Idle || m_Ghost == null) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        UpdateMobile();
#else
        UpdateDesktop();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void UpdateMobile()
    {
        switch (m_State)
        {
            case DeployState.Dragging: UpdateDragging(); break;
            case DeployState.DirectionSelect: UpdateDirectionSelect(); break;
        }
    }

    // Phase 1: ghost follows finger, snaps to cell. Release on valid cell = DROP.
    private void UpdateDragging()
    {
        Touch? t = FindTouch(m_DragFingerId);
        if (m_DragFingerId < 0 && Touch.activeTouches.Count > 0)
        {
            m_DragFingerId = Touch.activeTouches[0].touchId;
            t = Touch.activeTouches[0];
        }
        if (!t.HasValue) return;

        Vector3 fingerWorld = TouchToWorld(t.Value.screenPosition);
        if (fingerWorld != Vector3.negativeInfinity)
        {
            Vector3 snapped = TDGridMainModel.api.GetNearestGridPosition(fingerWorld);
            m_LastSnappedPos = snapped;
            m_Ghost.transform.position = new Vector3(snapped.x, TDConstant.CONFIG_TOWER_PLACE_Y, snapped.z);
            RefreshRangeHighlights();
        }

        if (t.Value.phase == TouchPhase.Ended)
        {
            if (IsCellValid(m_LastSnappedPos)) Drop();
            else CancelPlacement();
        }
    }

    // DROP: lock the cell, lift finger, show the diamond, wait for a new touch.
    private void Drop()
    {
        m_DroppedCenter = new Vector3(m_LastSnappedPos.x, TDConstant.DIAMOND_GROUND_Y, m_LastSnappedPos.z);
        m_DragFingerId = -1;
        m_DirFingerId = -1;
        m_RotationIndex = 0;
        m_Ghost.transform.rotation = Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[0], 0f);
        HideHighlights(); // valid-cell tiles off after drop (Arknights)
        m_Diamond?.ShowDeploy(m_DroppedCenter, onCancel: CancelPlacement);
        m_State = DeployState.DirectionSelect;
        TDGameEventBus.DeployDrop();
    }

    // Phase 2: a NEW touch drags/taps to pick facing. Release: outside dead-zone = commit, inside = cancel.
    private void UpdateDirectionSelect()
    {
        if (m_DirFingerId < 0)
        {
            foreach (var nt in Touch.activeTouches)
                if (nt.phase == TouchPhase.Began) { m_DirFingerId = nt.touchId; break; }
        }

        Touch? t = FindTouch(m_DirFingerId);
        if (!t.HasValue) return;

        Vector3 world = TouchToWorld(t.Value.screenPosition);
        if (world == Vector3.negativeInfinity) return;

        bool inDeadZone = m_Diamond != null && m_Diamond.IsInDeadZone(world);
        if (inDeadZone)
        {
            m_Diamond.SetActiveDirection(-1);
        }
        else
        {
            Vector3 d = world - m_DroppedCenter; d.y = 0f;
            m_RotationIndex = ComputeRotationIndex(d);
            m_Ghost.transform.rotation = Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[m_RotationIndex], 0f);
            m_Diamond?.SetActiveDirection(m_RotationIndex);
            RefreshRangeHighlights();
        }

        if (t.Value.phase == TouchPhase.Ended)
        {
            if (inDeadZone) CancelPlacement();
            else CommitPlacement();
        }
    }

    private Touch? FindTouch(int fingerId)
    {
        if (fingerId < 0) return null;
        foreach (var t in Touch.activeTouches)
            if (t.touchId == fingerId) return t;
        return null;
    }
#endif

    private void UpdateDesktop()
    {
        // Ghost follows mouse; facing follows mouse offset from cell center.
        Vector3 mouseWorld = MouseToWorld();
        if (mouseWorld == Vector3.negativeInfinity) return;
        if (IsPointerOverUI()) return;

        Vector3 snapped = TDGridMainModel.api.GetNearestGridPosition(mouseWorld);
        m_LastSnappedPos = snapped;
        m_Ghost.transform.position = new Vector3(snapped.x, TDConstant.CONFIG_TOWER_PLACE_Y, snapped.z);

        Vector3 dir = mouseWorld - snapped; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            int idx = ComputeRotationIndex(dir);
            if (idx != m_RotationIndex)
            {
                m_RotationIndex = idx;
                m_Ghost.transform.rotation = Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[idx], 0f);
            }
        }
        RefreshRangeHighlights();

        if (Mouse.current.leftButton.wasPressedThisFrame) TDUserInputControl.api.OnMouseButton0Clicked();
        else if (Keyboard.current.eKey.wasPressedThisFrame) TDUserInputControl.api.OnMouseButtonEClicked();
        else if (Keyboard.current.qKey.wasPressedThisFrame) TDUserInputControl.api.OnMouseButtonQClicked();
        else if (Mouse.current.rightButton.wasPressedThisFrame) TDUserInputControl.api.OnMouseButton1Clicked();
    }

    // ── Ghost lifecycle ─────────────────────────────────────────────────────────

    private void OnCreateTower(TDTowerSlotInfo slot)
    {
        if (m_Ghost != null) Destroy(m_Ghost);

        m_Ghost = Instantiate(slot.prefab, new Vector3(0f, -999f, 0f), Quaternion.identity);
        if (m_Ghost.GetComponent<IPlacedUnit>() is MonoBehaviour mb) mb.enabled = false;
        foreach (var col in m_Ghost.GetComponentsInChildren<Collider>()) col.enabled = false;
        // Operator prefabs ship with SelectionIndicator active by default; hide it on the ghost
        // so it doesn't clash with the diamond outline during deployment.
        var ghostIndicator = m_Ghost.transform.Find(TDConstant.SELECTION_INDICATOR_NAME);
        if (ghostIndicator != null) ghostIndicator.gameObject.SetActive(false);

        m_Ghost.transform.localScale = slot.towerType == TowerType.Operator
            ? new Vector3(1.5f, 1.5f, 1.5f) : Vector3.one;

        IsPlacingUnit = true;
        TDGameEventBus.UnitPickup();
        m_TowerType = slot.towerType;
        m_OperatorType = slot.operatorType;
        m_RotationIndex = 0;
        m_LastSnappedPos = Vector3.negativeInfinity;
        m_DragFingerId = -1;
        m_DirFingerId = -1;
        m_LastRangeCell = new Vector2Int(int.MinValue, int.MinValue);
        m_LastRangeRotIndex = -1;
        m_State = DeployState.Dragging;

        if (slot.towerType == TowerType.Operator)
        {
            var opData = TDFlyweightOperatorDataSettings.api?.GetData(m_OperatorType);
            if (opData?.deployZone == DeployZone.TowerZone) ShowHighlights();
            else ShowOperatorHighlights();
        }
        else ShowHighlights();

        m_PlacementPanel?.SetActive(true);
    }

    private void CommitPlacement() => TDUserInputControl.api.OnMouseButton0Clicked();

    public void CancelPlacement()
    {
        if (m_Ghost != null) { Destroy(m_Ghost); m_Ghost = null; }
        IsPlacingUnit = false;
        m_State = DeployState.Idle;
        m_LastSnappedPos = Vector3.negativeInfinity;
        m_DragFingerId = -1;
        m_DirFingerId = -1;
        m_Diamond?.Hide();
        HideHighlights();
        HideRangeHighlights();
        m_PlacementPanel?.SetActive(false);
    }

    private void OnPlaceTowerSuccess(bool isPlaced)
    {
        if (!isPlaced) return;
        int cost = (m_SlotIndex >= 0 && m_SlotIndex < TDTowerMainControl.api.ActiveSlots.Count)
            ? TDTowerMainControl.api.ActiveSlots[m_SlotIndex].cost : 0;
        TDGoldControl.api.SpendGold(cost);
        TDGameEventBus.TowerPlaced();

        Destroy(m_Ghost);
        m_Ghost = null;
        IsPlacingUnit = false;
        m_State = DeployState.Idle;
        m_DragFingerId = -1;
        m_DirFingerId = -1;
        TDPlaceTowerControl.api.onPlaceTowerSuccess?.Invoke(false);
        m_Diamond?.Hide();
        HideHighlights();
        HideRangeHighlights();
        m_PlacementPanel?.SetActive(false);
    }

    public void SetSlotIndex(int index) => m_SlotIndex = index;

    // ── Input event handlers ──────────────────────────────────────────────────

    private void OnGetCurrentRotationIndex(int index) => m_RotationIndex = index;

    private void OnPlaceClicked(bool clicked)
    {
        if (!clicked) return;
        m_State = DeployState.Committing;
        TDTowerMainControl.api.OnPlaceTower(m_Ghost);
        TDUserInputControl.api.onMouseButton0Clicked(false);
    }

    private void OnCancelClicked(bool clicked)
    {
        if (!clicked) return;
        CancelPlacement();
        TDUserInputControl.api.onMouseButton1Clicked(false);
    }

    private void OnRotateCW(bool clicked)
    {
        if (!clicked) return;
        TDTowerMainControl.api.RotateTowerClockwise(m_Ghost, m_RotationIndex);
        TDUserInputControl.api.onMouseButtonEClicked(false);
    }

    private void OnRotateCCW(bool clicked)
    {
        if (!clicked) return;
        TDTowerMainControl.api.RotateTowerCounterClockwise(m_Ghost, m_RotationIndex);
        TDUserInputControl.api.onMouseButtonQClicked(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool IsCellValid(Vector3 worldPos)
        => m_TowerType == TowerType.Operator
            ? IsValidOperatorPlacement(worldPos)
            : TDGridMainModel.api.IsValidPlacement(worldPos);

    private bool IsValidOperatorPlacement(Vector3 worldPos)
    {
        var data = TDFlyweightOperatorDataSettings.api?.GetData(m_OperatorType);
        if (data == null) return false;
        return TDControl.CreateOperatorBehavior(data.deployZone).CanPlace(worldPos);
    }

    private static int ComputeRotationIndex(Vector3 delta)
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z)) return delta.x > 0f ? 1 : 3;
        return delta.z > 0f ? 0 : 2;
    }

    private Vector3 MouseToWorld()
    {
        if (MainCam == null) return Vector3.negativeInfinity;
        Ray ray = MainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
        return Physics.Raycast(ray, out RaycastHit hit) ? hit.point : Vector3.negativeInfinity;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private Vector3 TouchToWorld(Vector2 screenPos)
    {
        if (MainCam == null) return Vector3.negativeInfinity;
        screenPos.y += Screen.height * TDConstant.TOUCH_SCREEN_Y_OFFSET_RATIO;
        Ray ray = MainCam.ScreenPointToRay(screenPos);
        var plane = new Plane(Vector3.up, Vector3.zero);
        return plane.Raycast(ray, out float dist) ? ray.GetPoint(dist) : Vector3.negativeInfinity;
    }
#endif

    private static readonly List<RaycastResult> s_RaycastResults = new List<RaycastResult>();
    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    // ── Highlights (moved verbatim from TDSlotHolderMainView) ──────────────────

    private void RefreshRangeHighlights()
    {
        if (m_Ghost == null || m_RangeHighlightPrefab == null) return;

        var data = m_TowerType == TowerType.Operator
            ? (object)TDFlyweightOperatorDataSettings.api?.GetData(m_OperatorType)
            : TDFlyweightTowerDataSettings.api?.GetData(m_TowerType);

        Vector2Int[] offsets = m_TowerType == TowerType.Operator
            ? (data as OperatorData)?.rangeOffsets
            : (data as TowerData)?.rangeOffsets;

        if (offsets == null || offsets.Length == 0) { HideRangeHighlights(); return; }

        Vector2Int cell = TDGridMainModel.api.WorldToCell(m_Ghost.transform.position);
        if (cell == m_LastRangeCell && m_RotationIndex == m_LastRangeRotIndex) return;
        m_LastRangeCell = cell;
        m_LastRangeRotIndex = m_RotationIndex;

        var rangeDto = new TDOffsetRangeDTO(offsets);
        ApplyRangeHighlights(rangeDto.GetCellsInRange(cell, m_Ghost.transform.rotation));
    }

    private void ApplyRangeHighlights(List<Vector2Int> cells)
    {
        while (m_RangeHighlightTiles.Count < cells.Count)
        {
            var tile = Object.Instantiate(m_RangeHighlightPrefab);
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
        m_LastRangeCell = new Vector2Int(int.MinValue, int.MinValue);
        m_LastRangeRotIndex = -1;
    }

    private void ShowHighlights()
    {
        HideHighlights();
        foreach (Vector3 pos in m_ValidTowerPositions)
        {
            if (!TDGridMainModel.api.IsValidPlacement(pos)) continue;
            var adjustedPos = new Vector3(pos.x, 0.06f, pos.z);
            GameObject tile = m_TowerHighlightPrefab != null
                ? Instantiate(m_TowerHighlightPrefab, adjustedPos, Quaternion.identity)
                : CreateHighlightTile(pos);
            m_HighlightTiles.Add(tile);
        }
    }

    private void ShowOperatorHighlights()
    {
        HideHighlights();
        if (TDOperatorRegistry.api == null) return;
        foreach (var cell in TDOperatorRegistry.api.GetValidOperatorCells())
        {
            if (TDOperatorRegistry.api.HasOperatorAt(cell)) continue;
            m_HighlightTiles.Add(CreateOperatorHighlightTile(TDGridMainModel.api.CellToWorld(cell)));
        }
    }

    private GameObject CreateOperatorHighlightTile(Vector3 worldPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.transform.position = new Vector3(worldPos.x, 0.15f, worldPos.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        float size = TDGridMainModel.api.cellSize * 0.88f;
        go.transform.localScale = new Vector3(size, size, 1f);
        Object.Destroy(go.GetComponent<MeshCollider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = TDStageMaterialCache.OperatorHighlight;
        return go;
    }

    private GameObject CreateHighlightTile(Vector3 worldPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.transform.position = new Vector3(worldPos.x, 0.51f, worldPos.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        float size = TDGridMainModel.api.cellSize * 0.88f;
        go.transform.localScale = new Vector3(size, size, 1f);
        Destroy(go.GetComponent<MeshCollider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = TDStageMaterialCache.TowerHighlight;
        return go;
    }

    private void HideHighlights()
    {
        foreach (var tile in m_HighlightTiles) Destroy(tile);
        m_HighlightTiles.Clear();
    }
}

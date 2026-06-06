using System.Collections.Generic;
using TDEnums;
using Services.DependencyInjection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class TDSlotHolderMainView : MonoBehaviour
{
    private GameObject m_TowerHighlightPrefab;
    private GameObject m_RangeHighlightPrefab;
    private GameObject m_TowerHolderPrefab;    // prefab UI button cho mỗi slot
    private Transform  m_TowerHolderContainer; // parent chứa các holder

    // Tỉ lệ cellSize để trigger direction selection (35% = 0.7f khi cellSize=2)
    // Phải < 50% để trigger TRƯỚC khi GetNearestGridPosition snap sang ô mới
    private const float DIRECTION_THRESHOLD_RATIO = 0.35f;

    private readonly List<TDSlotHolderItemView> m_SlotHolders = new List<TDSlotHolderItemView>();
    public static bool IsPlacingUnit { get; private set; }

    private int m_CurrentSlotIndex = -1;
    private GameObject m_CurrentTower;
    private TowerType    m_CurrentTowerType;
    private OperatorType m_CurrentOperatorType;

    private List<Vector3> m_ValidTowerPositions = new List<Vector3>();
    private readonly List<GameObject> m_HighlightTiles      = new List<GameObject>();
    private readonly List<GameObject> m_RangeHighlightTiles = new List<GameObject>();
    private Vector2Int m_LastRangeCell     = new Vector2Int(int.MinValue, int.MinValue);
    private int        m_LastRangeRotIndex = -1;
    private int m_CurrentRotationIndex;

    // Phase 1: drag đến cell (position)
    // Phase 2: swipe ra ngoài threshold → chọn hướng (direction)
    // Chỉ buông tay = place khi đã qua Phase 2
    // Reset về false khi ghost di chuyển sang cell mới
    private bool m_IsDirectionSelected;
    private Vector3 m_LastSnappedPos = Vector3.negativeInfinity;

    // Panel chỉ chứa nút Cancel — hiện khi đang hold ghost tower
    private GameObject m_PlacementPanel;

    private void Start()
    {
        RegistryTowerControlEvents();
        InitViews();
        InitTowerHolder();
        InitPlacementPanel();
    }

    // ─── Update ──────────────────────────────────────────────────────────────

    private void Update()
    {
        if (m_CurrentTower == null) return;

        // Khi ngón đang đè lên UI (Cancel button, v.v.) → không di/rotate ghost
        if (IsPointerOverUI()) return;

        UpdateGhostTransform();
        RefreshRangeHighlights();
        HandlePlacementInput();
    }

    // MOBILE: Phase 1 ghost follows → Phase 2 ghost đóng băng khi ngón tay vào zone lân cận
    //         threshold = 35% cellSize (< snap boundary 50%) → trigger trước khi snap đổi ô
    //         direction giữ nguyên đến khi place hoặc cancel
    // DESKTOP: ghost luôn follow chuột; direction cập nhật visual theo chuột (không đóng băng)
    //          E/Q rotate thủ công; LMB đặt bất kỳ lúc nào
    private void UpdateGhostTransform()
    {
        Vector3 fingerWorld = GetFingerWorldPosition();
        if (fingerWorld == Vector3.negativeInfinity) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        // ── Mobile ──────────────────────────────────────────────────────────
        if (m_IsDirectionSelected)
        {
            Vector3 delta = fingerWorld - m_LastSnappedPos;
            delta.y = 0f;

            // Kéo ngón về gần center → thoát Phase 2, quay lại Phase 1
            float threshold = TDGridMainModel.api.cellSize * DIRECTION_THRESHOLD_RATIO;
            if (delta.sqrMagnitude <= threshold * threshold)
            {
                m_IsDirectionSelected = false;
                return;
            }

            int rotIndex = ComputeRotationIndex(delta);
            if (rotIndex != m_CurrentRotationIndex)
            {
                m_CurrentRotationIndex = rotIndex;
                m_CurrentTower.transform.rotation =
                    Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[m_CurrentRotationIndex], 0f);
            }
            return;
        }

        Vector3 snappedPosMobile = TDGridMainModel.api.GetNearestGridPosition(fingerWorld);
        if (snappedPosMobile != m_LastSnappedPos)
            m_LastSnappedPos = snappedPosMobile;
        m_CurrentTower.transform.position = new Vector3(m_LastSnappedPos.x, TDConstant.CONFIG_TOWER_PLACE_Y, m_LastSnappedPos.z);

        Vector3 dirMobile = fingerWorld - m_LastSnappedPos;
        dirMobile.y = 0f;
        float thresholdMobile = TDGridMainModel.api.cellSize * DIRECTION_THRESHOLD_RATIO;
        bool cellOk = m_CurrentTowerType == TowerType.Operator
            ? IsValidOperatorPlacement(m_LastSnappedPos)
            : TDGridMainModel.api.IsValidPlacement(m_LastSnappedPos);
        if (dirMobile.sqrMagnitude > thresholdMobile * thresholdMobile && cellOk)
        {
            m_IsDirectionSelected = true;
            m_CurrentRotationIndex = ComputeRotationIndex(dirMobile);
            m_CurrentTower.transform.rotation =
                Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[m_CurrentRotationIndex], 0f);
        }
#else
        // ── Desktop / Editor ─────────────────────────────────────────────────
        // Ghost luôn follow chuột — không đóng băng
        Vector3 snappedPos = TDGridMainModel.api.GetNearestGridPosition(fingerWorld);
        if (snappedPos != m_LastSnappedPos)
            m_LastSnappedPos = snappedPos;
        m_CurrentTower.transform.position = new Vector3(m_LastSnappedPos.x, TDConstant.CONFIG_TOWER_PLACE_Y, m_LastSnappedPos.z);

        // Direction luôn follow chuột — không cần threshold
        // Chuột ở phía nào của cell center thì tower quay về phía đó
        Vector3 dir = fingerWorld - m_LastSnappedPos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            int rotIndex = ComputeRotationIndex(dir);
            if (rotIndex != m_CurrentRotationIndex)
            {
                m_CurrentRotationIndex = rotIndex;
                m_CurrentTower.transform.rotation =
                    Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[m_CurrentRotationIndex], 0f);
            }
        }
#endif
    }

    private void HandlePlacementInput()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Mobile (Arknights-style 2-phase):
        // Phase 1 — drag đến cell: buông tay KHÔNG place (m_IsDirectionSelected = false)
        // Phase 2 — swipe ra ngoài threshold: m_IsDirectionSelected = true → buông tay = place
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended)
        {
            if (m_IsDirectionSelected)
                TDUserInputControl.api.OnMouseButton0Clicked();
            else
                CancelPlacement(); // nhấc ngón chưa chọn direction = auto cancel
        }
#else
        // Desktop: LMB click = đặt, E/Q = rotate thủ công, RMB = cancel
        if (Input.GetMouseButtonDown(0))
            TDUserInputControl.api.OnMouseButton0Clicked();
        else if (Input.GetKeyDown(KeyCode.E))
            TDUserInputControl.api.OnMouseButtonEClicked();
        else if (Input.GetKeyDown(KeyCode.Q))
            TDUserInputControl.api.OnMouseButtonQClicked();
        else if (Input.GetMouseButtonDown(1))
            TDUserInputControl.api.OnMouseButton1Clicked();
#endif
    }

    // ─── Input helpers ───────────────────────────────────────────────────────

    // Lấy world position của ngón tay / chuột bằng raycast xuống ground
    private Vector3 GetFingerWorldPosition()
    {
        if (Camera.main == null) return Vector3.negativeInfinity;

        Vector3 screenPos = Input.touchCount > 0
            ? (Vector3)Input.GetTouch(0).position
            : Input.mousePosition;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        return Physics.Raycast(ray, out RaycastHit hit) ? hit.point : Vector3.negativeInfinity;
    }

    // delta = fingerWorld - cellCenter (Y=0)
    // So sánh |dx| vs |dz| → xác định hướng dominant → map sang rotation index
    //   index 0 = 0°   (+Z, forward)
    //   index 1 = 90°  (+X, right)
    //   index 2 = 180° (-Z, backward)
    //   index 3 = 270° (-X, left)
    private int ComputeRotationIndex(Vector3 delta)
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            return delta.x > 0f ? 1 : 3;
        else
            return delta.z > 0f ? 0 : 2;
    }

    // Mobile cần fingerId, desktop dùng pointer id -1 (mouse)
    private bool IsPointerOverUI()
    {
        if (Input.touchCount > 0)
            return EventSystem.current != null &&
                   EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    // ─── Placement Panel ─────────────────────────────────────────────────────

    private void InitPlacementPanel()
    {
        var panelT = transform.Find(TDConstant.GAMEPLAY_PLACEMENT_PANEL);
        if (panelT == null)
        {
            Debug.LogWarning("<color=orange>TDTowerMainView: PlacementPanel not found in hierarchy</color>");
            return;
        }

        m_PlacementPanel = panelT.gameObject;

        // Chỉ wire Cancel — rotate & confirm được xử lý bằng cơ chế Arknights + TouchPhase.Ended
        WireButton(TDConstant.GAMEPLAY_BTN_CANCEL_PLACE, () => TDUserInputControl.api.OnMouseButton1Clicked());

        m_PlacementPanel.SetActive(false);
    }

    private void WireButton(string path, UnityEngine.Events.UnityAction action)
    {
        var btn = transform.Find(path)?.GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(action);
        else
            Debug.LogWarning($"<color=orange>TDTowerMainView: Button not found at '{path}'</color>");
    }

    private void ShowPlacementPanel() => m_PlacementPanel?.SetActive(true);
    private void HidePlacementPanel()  => m_PlacementPanel?.SetActive(false);

    // ─── Events ──────────────────────────────────────────────────────────────

    private void RegistryTowerControlEvents()
    {
        TDTowerMainControl.api.onGetTowerPrefab += OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex += OnGetCurrentRotationIndex;
        TDPlaceTowerControl.api.onPlaceTowerSuccess += OnPlaceTowerSuccess;
        TDUserInputControl.api.onMouseButton0Clicked += OnMouseButton0Clicked;
        TDUserInputControl.api.onMouseButton1Clicked += OnMouseButton1Clicked;
        TDUserInputControl.api.onMouseButtonEClicked += OnMouseButtonEClicked;
        TDUserInputControl.api.onMouseButtonQClicked += OnMouseButtonQClicked;
        TDEnemyPathMainControl.api.onValidTowerCellsReady += OnValidTowerCellsReady;
        TDGoldControl.api.onGoldChanged += RefreshHolderInteractability;
    }

    private void OnDestroy()
    {
        TDTowerMainControl.api.onGetTowerPrefab -= OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex -= OnGetCurrentRotationIndex;
        TDPlaceTowerControl.api.onPlaceTowerSuccess -= OnPlaceTowerSuccess;
        TDUserInputControl.api.onMouseButton0Clicked -= OnMouseButton0Clicked;
        TDUserInputControl.api.onMouseButton1Clicked -= OnMouseButton1Clicked;
        TDUserInputControl.api.onMouseButtonEClicked -= OnMouseButtonEClicked;
        TDUserInputControl.api.onMouseButtonQClicked -= OnMouseButtonQClicked;
        TDEnemyPathMainControl.api.onValidTowerCellsReady -= OnValidTowerCellsReady;

        if (TDGoldControl.api != null)
            TDGoldControl.api.onGoldChanged -= RefreshHolderInteractability;
    }

    // ─── Tower Holders ───────────────────────────────────────────────────────

    private void InitViews()
    {
        m_TowerHolderPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_SLOT_HOLDER);
        m_RangeHighlightPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_RANGE_HIGH_LIGHT);
        m_TowerHolderContainer = transform;
    }
    
    private void InitTowerHolder()
    {
        if (m_TowerHolderPrefab == null)
        {
            Debug.LogError("<color=red>TDTowerMainView: m_TowerHolderPrefab chưa được assign!</color>");
            return;
        }
        if (m_TowerHolderContainer == null)
        {
            Debug.LogError("<color=red>TDTowerMainView: m_TowerHolderContainer chưa được assign!</color>");
            return;
        }

        // Build danh sách slot từ config (random nếu > 8)
        TDTowerMainControl.api.BuildSlots();

        foreach (var slot in TDTowerMainControl.api.ActiveSlots)
        {
            var go     = Instantiate(m_TowerHolderPrefab, m_TowerHolderContainer);
            var holder = go.GetComponent<TDSlotHolderItemView>();
            holder.SetupSlotHolderVariables();
            holder.SetupSlotCost(slot.cost);
            holder.SetupIcon(slot.icon);
            m_SlotHolders.Add(holder);
        }

        SetupOnSelectSlot();
        RefreshHolderInteractability(TDGoldControl.api.Gold);
    }

    private void RefreshHolderInteractability(int gold)
    {
        foreach (var holder in m_SlotHolders)
            holder.SetInteractable(gold >= holder.Cost);
    }

    private void SetupOnSelectSlot()
    {
        for (int i = 0; i < m_SlotHolders.Count; i++)
        {
            int index = i; // capture
            m_SlotHolders[i].towerSelectButton.onClick.AddListener(() =>
            {
                m_CurrentSlotIndex = index;
                TDTowerMainControl.api.OnSelectTowerHolder(index);
            });
        }
    }

    // ─── Valid Positions & Highlights ────────────────────────────────────────

    private void OnValidTowerCellsReady(List<Vector3> positions)
    {
        m_ValidTowerPositions = positions;
    }

    private void OnCreateTower(TDTowerSlotInfo slot)
    {
        if (m_CurrentTower != null)
            Destroy(m_CurrentTower);

        m_CurrentTower  = Instantiate(slot.prefab, Vector3.zero, Quaternion.identity);
        IsPlacingUnit   = true;
        m_CurrentTower.transform.localScale = slot.towerType == TowerType.Operator
            ? new Vector3(1.5f, 1.5f, 1.5f)
            : new Vector3(1.0f, 1.0f, 1.0f);
        m_CurrentRotationIndex = 0;
        m_IsDirectionSelected = false;
        m_LastSnappedPos = Vector3.negativeInfinity;
        m_CurrentTowerType    = slot.towerType;
        m_CurrentOperatorType = slot.operatorType;
        m_LastRangeCell = new Vector2Int(int.MinValue, int.MinValue);
        m_LastRangeRotIndex = -1;

        if (slot.towerType == TowerType.Operator)
        {
            var opData = TDFlyweightOperatorDataSettings.api?.GetData(m_CurrentOperatorType);
            if (opData?.deployZone == DeployZone.TowerZone)
                ShowHighlights();       // TowerZone operator — highlight tower zone tiles (green)
            else
                ShowOperatorHighlights(); // PathCell operator — highlight path cells (blue)
        }
        else
            ShowHighlights();

        ShowPlacementPanel();
    }

    // ─── Range Highlights ────────────────────────────────────────────────────

    private void RefreshRangeHighlights()
    {
        if (m_CurrentTower == null || m_RangeHighlightPrefab == null) return;

        if (m_CurrentTowerType == TowerType.Operator)
        {
            RefreshOperatorRangeHighlights();
            return;
        }

        Vector2Int cell   = TDGridMainModel.api.WorldToCell(m_CurrentTower.transform.position);
        int        rotIdx = m_CurrentRotationIndex;

        if (cell == m_LastRangeCell && rotIdx == m_LastRangeRotIndex) return;
        m_LastRangeCell     = cell;
        m_LastRangeRotIndex = rotIdx;

        var data = TDFlyweightBulletFactoryModel.api.Setting.GetData(m_CurrentTowerType);
        if (data?.rangeOffsets == null || data.rangeOffsets.Length == 0)
        {
            HideRangeHighlights();
            return;
        }

        var              rangeDto = new TDOffsetRangeDTO(data.rangeOffsets);
        List<Vector2Int> cells    = rangeDto.GetCellsInRange(cell, m_CurrentTower.transform.rotation);

        ApplyRangeHighlights(cells);
    }

    private void RefreshOperatorRangeHighlights()
    {
        var data = TDFlyweightOperatorDataSettings.api?.GetData(m_CurrentOperatorType);
        if (data?.rangeOffsets == null || data.rangeOffsets.Length == 0)
        {
            HideRangeHighlights();
            return;
        }

        Vector2Int cell   = TDGridMainModel.api.WorldToCell(m_CurrentTower.transform.position);
        int        rotIdx = m_CurrentRotationIndex;

        if (cell == m_LastRangeCell && rotIdx == m_LastRangeRotIndex) return;
        m_LastRangeCell     = cell;
        m_LastRangeRotIndex = rotIdx;

        var rangeDto = new TDOffsetRangeDTO(data.rangeOffsets);
        var cells    = rangeDto.GetCellsInRange(cell, m_CurrentTower.transform.rotation);

        ApplyRangeHighlights(cells);
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
        m_LastRangeCell     = new Vector2Int(int.MinValue, int.MinValue);
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

    // ─── Operator Highlights (path cells — màu xanh dương) ──────────────────

    private void ShowOperatorHighlights()
    {
        HideHighlights();
        if (TDOperatorRegistry.api == null) return;

        var cells = TDOperatorRegistry.api.GetValidOperatorCells();
        foreach (var cell in cells)
        {
            // Skip cells đã có operator
            if (TDOperatorRegistry.api.HasOperatorAt(cell)) continue;

            Vector3    world = TDGridMainModel.api.CellToWorld(cell);
            GameObject go    = CreateOperatorHighlightTile(world);
            m_HighlightTiles.Add(go);
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

        var rend = go.GetComponent<MeshRenderer>();
        rend.sharedMaterial = TDStageMaterialCache.OperatorHighlight;

        return go;
    }

    // Fallback tile (nếu chưa assign prefab) — Quad xanh lá nằm ngang
    private GameObject CreateHighlightTile(Vector3 worldPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.transform.position = new Vector3(worldPos.x, 0.51f, worldPos.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float size = TDGridMainModel.api.cellSize * 0.88f;
        go.transform.localScale = new Vector3(size, size, 1f);

        Destroy(go.GetComponent<MeshCollider>());

        var rend = go.GetComponent<MeshRenderer>();
        rend.sharedMaterial = TDStageMaterialCache.TowerHighlight;

        return go;
    }

    private void HideHighlights()
    {
        foreach (var tile in m_HighlightTiles)
            Destroy(tile);
        m_HighlightTiles.Clear();
    }

    // ─── Input Handlers ──────────────────────────────────────────────────────

    private void OnGetCurrentRotationIndex(int index)
    {
        m_CurrentRotationIndex = index;
    }

    private void OnPlaceTowerSuccess(bool isPlaced)
    {
        if (isPlaced)
        {
            // Lấy cost trực tiếp từ slot đang active (không hardcode check TowerType)
            int cost = (m_CurrentSlotIndex >= 0 && m_CurrentSlotIndex < TDTowerMainControl.api.ActiveSlots.Count)
                ? TDTowerMainControl.api.ActiveSlots[m_CurrentSlotIndex].cost
                : 0;
            TDGoldControl.api.SpendGold(cost);

            Destroy(m_CurrentTower);
            m_CurrentTower = null;
            IsPlacingUnit  = false;
            TDPlaceTowerControl.api.onPlaceTowerSuccess?.Invoke(false);
            HideHighlights();
            HideRangeHighlights();
            HidePlacementPanel();
        }
    }

    // Desktop keyboard: Q = rotate counter-clockwise
    private void OnMouseButtonQClicked(bool isClicked)
    {
        if (isClicked)
        {
            TDTowerMainControl.api.RotateTowerCounterClockwise(m_CurrentTower, m_CurrentRotationIndex);
            TDUserInputControl.api.onMouseButtonQClicked(false);
        }
    }

    // Desktop keyboard: E = rotate clockwise
    private void OnMouseButtonEClicked(bool isClicked)
    {
        if (isClicked)
        {
            TDTowerMainControl.api.RotateTowerClockwise(m_CurrentTower, m_CurrentRotationIndex);
            TDUserInputControl.api.onMouseButtonEClicked(false);
        }
    }

    // RMB (desktop) hoặc Cancel button (mobile)
    private void OnMouseButton1Clicked(bool isClicked)
    {
        if (isClicked)
        {
            CancelPlacement();
            TDUserInputControl.api.onMouseButton1Clicked(false);
        }
    }

    // LMB (desktop) hoặc TouchPhase.Ended (mobile)
    private void OnMouseButton0Clicked(bool isClicked)
    {
        if (isClicked)
        {
            TDTowerMainControl.api.OnPlaceTower(m_CurrentTower);
            TDUserInputControl.api.onMouseButton0Clicked(false);
        }
    }

    public void CancelPlacement()
    {
        if (m_CurrentTower != null)
        {
            Destroy(m_CurrentTower);
            m_CurrentTower = null;
        }
        IsPlacingUnit = false;
        m_IsDirectionSelected = false;
        m_LastSnappedPos      = Vector3.negativeInfinity;
        HideHighlights();
        HideRangeHighlights();
        HidePlacementPanel();
    }

    private bool IsValidOperatorPlacement(Vector3 worldPos)
    {
        var data = TDFlyweightOperatorDataSettings.api?.GetData(m_CurrentOperatorType);
        if (data == null) return false;
        var behavior = TDControl.CreateOperatorBehavior(data.deployZone);
        return behavior.CanPlace(worldPos);
    }
}

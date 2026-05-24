using System.Collections.Generic;
using TDEnums;
using Services.DependencyInjection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TDTowerMainView : MonoBehaviour
{
    [SerializeField] private GameObject m_TowerHighlightPrefab;

    // Ngưỡng (mét) từ cell center → ngón tay phải kéo xa hơn thì mới cập nhật hướng
    // Tránh hướng "giật" khi ngón tay gần center
    [SerializeField] private float m_DirectionThreshold = 1.0f;

    private readonly List<TDTowerHolderView> m_TowerHolders = new List<TDTowerHolderView>();
    private TDTowerHolderView m_TdTowerHolderView0, m_TdTowerHolderView1, m_TdTowerHolderView2, m_TdTowerHolderView3, m_TdTowerHolderView4;
    private GameObject m_CurrentTower;

    private List<Vector3> m_ValidTowerPositions = new List<Vector3>();
    private readonly List<GameObject> m_HighlightTiles = new List<GameObject>();
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
        HandlePlacementInput();
    }

    // Raycast → snap ghost đến ô grid gần nhất + auto-rotate theo hướng kéo (Arknights-style)
    // Phase 1: ngón tay di chuyển đến cell → ghost snap theo
    // Phase 2: ngón tay kéo ra khỏi center cell vượt threshold → chọn hướng, set m_IsDirectionSelected
    // Đổi sang cell mới → reset m_IsDirectionSelected (buộc chọn hướng lại)
    private void UpdateGhostTransform()
    {
        Vector3 fingerWorld = GetFingerWorldPosition();
        if (fingerWorld == Vector3.negativeInfinity) return;

        Vector3 snappedPos = TDGridMainModel.api.GetNearestGridPosition(fingerWorld);

        // Phase 1 reset: di chuyển sang cell mới → hủy direction đã chọn
        if (snappedPos != m_LastSnappedPos)
        {
            m_LastSnappedPos = snappedPos;
            m_IsDirectionSelected = false;
        }

        m_CurrentTower.transform.position = snappedPos;

        // Phase 2: ngón tay đủ xa center → xác định hướng
        Vector3 delta = fingerWorld - snappedPos;
        delta.y = 0f;

        if (delta.sqrMagnitude > m_DirectionThreshold * m_DirectionThreshold)
        {
            m_IsDirectionSelected = true;

            int rotIndex = ComputeRotationIndex(delta);
            if (rotIndex != m_CurrentRotationIndex)
            {
                m_CurrentRotationIndex = rotIndex;
                m_CurrentTower.transform.rotation =
                    Quaternion.Euler(0f, TDConstant.CONFIG_TOWER_ROTATIONS[m_CurrentRotationIndex], 0f);
            }
        }
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
            // else: bỏ qua — user chưa chọn hướng, giữ ghost ở đây
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
        TDTowerMainControl.api.onGetTowerName             += OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex  += OnGetCurrentRotationIndex;

        TDPlaceTowerControl.api.onPlaceTowerSuccess       += OnPlaceTowerSuccess;

        TDUserInputControl.api.onMouseButton0Clicked      += OnMouseButton0Clicked;
        TDUserInputControl.api.onMouseButton1Clicked      += OnMouseButton1Clicked;
        TDUserInputControl.api.onMouseButtonEClicked      += OnMouseButtonEClicked;
        TDUserInputControl.api.onMouseButtonQClicked      += OnMouseButtonQClicked;

        TDEnemyPathMainControl.api.onValidTowerCellsReady += OnValidTowerCellsReady;
    }

    private void OnDestroy()
    {
        TDTowerMainControl.api.onGetTowerName             -= OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex  -= OnGetCurrentRotationIndex;

        TDPlaceTowerControl.api.onPlaceTowerSuccess       -= OnPlaceTowerSuccess;

        TDUserInputControl.api.onMouseButton0Clicked      -= OnMouseButton0Clicked;
        TDUserInputControl.api.onMouseButton1Clicked      -= OnMouseButton1Clicked;
        TDUserInputControl.api.onMouseButtonEClicked      -= OnMouseButtonEClicked;
        TDUserInputControl.api.onMouseButtonQClicked      -= OnMouseButtonQClicked;

        TDEnemyPathMainControl.api.onValidTowerCellsReady -= OnValidTowerCellsReady;
    }

    // ─── Tower Holders ───────────────────────────────────────────────────────

    private void InitTowerHolder()
    {
        m_TdTowerHolderView0 = transform.Find(TDConstant.GAMEPLAY_TOWER_HOLDER_0).GetComponent<TDTowerHolderView>();
        m_TdTowerHolderView0.SetupTowerHolderVariables();
        m_TdTowerHolderView0.SetupTowerCost(TDFlyweightBulletFactoryModel.api.Setting.GetCost(TowerType.Cannon));

        m_TdTowerHolderView1 = transform.Find(TDConstant.GAMEPLAY_TOWER_HOLDER_1).GetComponent<TDTowerHolderView>();
        m_TdTowerHolderView1.SetupTowerHolderVariables();
        m_TdTowerHolderView1.SetupTowerCost(TDFlyweightBulletFactoryModel.api.Setting.GetCost(TowerType.Catapult));

        m_TdTowerHolderView2 = transform.Find(TDConstant.GAMEPLAY_TOWER_HOLDER_2).GetComponent<TDTowerHolderView>();
        m_TdTowerHolderView2.SetupTowerHolderVariables();
        m_TdTowerHolderView2.SetupTowerCost(TDFlyweightBulletFactoryModel.api.Setting.GetCost(TowerType.MissileG02));

        m_TdTowerHolderView3 = transform.Find(TDConstant.GAMEPLAY_TOWER_HOLDER_3).GetComponent<TDTowerHolderView>();
        m_TdTowerHolderView3.SetupTowerHolderVariables();
        m_TdTowerHolderView3.SetupTowerCost(TDFlyweightBulletFactoryModel.api.Setting.GetCost(TowerType.MissileG03));

        m_TdTowerHolderView4 = transform.Find(TDConstant.GAMEPLAY_TOWER_HOLDER_4).GetComponent<TDTowerHolderView>();
        m_TdTowerHolderView4.SetupTowerHolderVariables();
        m_TdTowerHolderView4.SetupTowerCost(TDFlyweightBulletFactoryModel.api.Setting.GetCost(TowerType.Mortar));

        m_TowerHolders.AddRange(new List<TDTowerHolderView>
        {
            m_TdTowerHolderView0, m_TdTowerHolderView1, m_TdTowerHolderView2, m_TdTowerHolderView3, m_TdTowerHolderView4
        });

        SetupOnSelectTower();
    }

    private void SetupOnSelectTower()
    {
        foreach (var holder in m_TowerHolders)
        {
            var index = m_TowerHolders.IndexOf(holder);
            holder.towerSelectButton.onClick.AddListener(() =>
            {
                Debug.Log($"TowerHolder clicked: {index}");
                TDTowerMainControl.api.OnSelectTowerHolder(index);
            });
        }
    }

    // ─── Valid Positions & Highlights ────────────────────────────────────────

    private void OnValidTowerCellsReady(List<Vector3> positions)
    {
        m_ValidTowerPositions = positions;
    }

    private void OnCreateTower(string towerName)
    {
        if (m_CurrentTower != null)
            Destroy(m_CurrentTower);

        GameObject prefab = RepResourceObject.GetResource<GameObject>(towerName);
        m_CurrentTower = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        m_CurrentTower.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        m_CurrentRotationIndex = 0;
        m_IsDirectionSelected = false;
        m_LastSnappedPos = Vector3.negativeInfinity;

        ShowHighlights();
        ShowPlacementPanel();
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

    // Fallback tile (nếu chưa assign prefab) — Quad xanh lá nằm ngang
    private GameObject CreateHighlightTile(Vector3 worldPos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.transform.position = new Vector3(worldPos.x, 0.06f, worldPos.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float size = TDGridMainModel.api.cellSize * 0.88f;
        go.transform.localScale = new Vector3(size, size, 1f);

        Destroy(go.GetComponent<MeshCollider>());

        var rend = go.GetComponent<MeshRenderer>();
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = new Color(0.15f, 0.85f, 0.25f, 1f);
        rend.material = mat;

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
            Destroy(m_CurrentTower);
            m_CurrentTower = null;
            TDPlaceTowerControl.api.onPlaceTowerSuccess?.Invoke(false);
            HideHighlights();
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
        m_IsDirectionSelected = false;
        m_LastSnappedPos = Vector3.negativeInfinity;
        HideHighlights();
        HidePlacementPanel();
    }
}

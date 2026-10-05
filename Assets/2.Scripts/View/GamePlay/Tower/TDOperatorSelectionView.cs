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

    // ── Rescue targeting (§06 · 3.3) ──────────────────────────────────────────
    //
    // Only reached when there are TWO OR MORE collapsed neighbours. One target rescues straight
    // away with no picking step, because a confirmation screen for a decision with one possible
    // answer is a tax on the player's attention at the exact moment they have none to spare.
    private TDOperatorView m_RescuePayer;
    private readonly List<TDOperatorView> m_RescueCandidates = new List<TDOperatorView>();
    private bool IsPickingRescue => m_RescuePayer != null;

    // Unscaled: the blink has to read the same at x1 and x2, and it is a UI rhythm, not an
    // in-world event. 0.5s is fast enough to say "choose" without becoming a strobe.
    private const float RESCUE_BLINK_PERIOD = 0.5f;

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
        if (TDPauseControl.api != null) TDPauseControl.api.onPauseChanged += OnPauseChanged;
    }

    private void OnDestroy()
    {
        EnhancedTouchSupport.Disable();
        TDGameEventBus.OnUnitPickup -= Deselect;
        TDGameEventBus.OnVictory  -= Deselect;
        TDGameEventBus.OnGameOver -= Deselect;
        if (TDPauseControl.api != null) TDPauseControl.api.onPauseChanged -= OnPauseChanged;
    }

    private void OnPauseChanged(bool paused)
    {
        if (paused) Deselect();
    }

    private void Update()
    {
        if (TDGameStateControl.api != null && TDGameStateControl.api.IsGameEnded) return;
        HandleTapInput();

        if (IsPickingRescue) { BlinkRescueCandidates(); return; }

        // The panel is built once on selection, but everything it describes keeps moving —
        // a neighbour collapses, SP crosses 50, someone else gets there first. Re-push it while
        // it is open, silently: eight dictionary lookups, and only while something is selected.
        if (m_SelectedOperator != null)
            TDDiamondPanelView.Instance?.UpdateRescue(BuildRescueOption(m_SelectedOperator, log: false));
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

        // Picking a rescue target owns the next tap outright — including taps on UI, which cancel.
        // Checked BEFORE IsPointerOverUI so a stray press on a leftover button cannot leave the
        // player stuck in a mode with blinking operators and no visible way out.
        if (IsPickingRescue) { ResolveRescueTap(screenPos); return; }

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
        TDDiamondPanelView.Instance?.ShowRetreat(op.transform.position, OnRetreatClicked, BuildRescueOption(op, log: true));
    }

    /// <summary>
    /// The Rescue corner for whoever is selected (§06 · 3.2), or null to hide it outright.
    ///
    /// Three rules, and the first is the one that matters most: with no collapsed neighbour the
    /// button does not exist. A control that is permanently greyed out trains the player to stop
    /// looking at that corner of the panel, and then they miss it on the one occasion it appears.
    /// When it CAN'T be used but could be, it stays visible and says why — "35/50" is a plan; a
    /// dead button is not.
    /// </summary>
    private TDDiamondPanelView.RescueOption? BuildRescueOption(TDOperatorView rescuer, bool log = true)
    {
        if (rescuer == null || TDOperatorRegistry.api == null || TDGridMainModel.api == null) return null;

        // A collapsed operator cannot rescue anyone — they cannot even walk off by themselves.
        // Worth logging rather than silently hiding: tapping the operator who NEEDS rescuing is
        // the obvious thing to try, and the button appearing on the NEIGHBOUR instead is the
        // least discoverable rule in this whole panel.
        if (rescuer.IsCollapsed)
        {
            if (log) Debug.Log($"<color=cyan>[Rescue] {rescuer.Data?.operatorName} đang suy sụp — " +
                      "bấm vào ĐỒNG ĐỘI TỈNH TÁO đứng kề, không phải vào chính nó</color>");
            return null;
        }

        var cell = TDGridMainModel.api.WorldToCell(rescuer.transform.position);
        var targets = TDOperatorRegistry.api.GetRescueTargetsAround(cell);
        if (targets.Count == 0)
        {
            if (log) Debug.Log($"<color=cyan>[Rescue] {rescuer.Data?.operatorName} tại {cell} — " +
                      "không có ai suy sụp ở 8 ô kề → nút ẩn</color>");
            return null;
        }

        var sp = rescuer.Sp;
        bool ready = sp != null && sp.CanAfford(TDConstant.RESCUE_SP_COST);

        if (log) Debug.Log($"<color=cyan>[Rescue] {rescuer.Data?.operatorName} · {targets.Count} mục tiêu · " +
                  $"SP {sp?.Current ?? 0f:F1}/{TDConstant.RESCUE_SP_COST:F0} → " +
                  $"{(ready ? "SẴN SÀNG" : "chưa đủ SP, nút mờ")}</color>");

        return new TDDiamondPanelView.RescueOption
        {
            ready = ready,
            // Counts toward the RESCUE cost, not toward SP_MAX: at this moment the player is
            // deciding whether they can save someone, not how close a skill is.
            //
            // FLOORED, not rounded. SP accumulates in deltaTime slices, so it sits at 49.99997
            // for a frame — which rounds to "50/50" on a button that is still greyed out. The
            // display must never claim a threshold the gate has not actually accepted.
            label = ready ? "RESCUE"
                          : $"{Mathf.FloorToInt(sp?.Current ?? 0f)}/{TDConstant.RESCUE_SP_COST:F0}",
            onRescue = () => OnRescuePressed(rescuer),

            // Read live, not captured: SP keeps charging while the panel sits open, so the fill
            // rises under the player's thumb and they can see how long the wait actually is.
            progress = () => (rescuer.Sp?.Current ?? 0f) / TDConstant.RESCUE_SP_COST,
        };
    }

    /// <summary>
    /// Rescue pressed (§06 · 3.3). One valid target rescues immediately; two or more hand the
    /// choice to the player, because which teammate you save is the decision — picking it for
    /// them would quietly make the most interesting moment in the system automatic.
    /// </summary>
    private void OnRescuePressed(TDOperatorView rescuer)
    {
        if (rescuer == null || TDOperatorRegistry.api == null || TDGridMainModel.api == null) return;

        var cell = TDGridMainModel.api.WorldToCell(rescuer.transform.position);
        var targets = TDOperatorRegistry.api.GetRescueTargetsAround(cell);

        if (targets.Count == 0) return;
        if (targets.Count == 1) { PerformRescue(rescuer, targets[0]); return; }

        BeginRescuePick(rescuer, targets);
    }

    private void PerformRescue(TDOperatorView rescuer, TDOperatorView target)
    {
        if (rescuer == null || target == null) return;
        if (rescuer.Sp == null || rescuer.Morale == null || target.Morale == null) return;
        if (!target.IsCollapsed) return;

        // Spend() is the gate, not a separate affordability check followed by a deduction — one
        // call that either takes the SP or refuses, so the two can never disagree.
        if (!rescuer.Sp.Spend(TDConstant.RESCUE_SP_COST)) return;

        rescuer.Morale.PayRescueStress();
        target.Morale.OnRescue();

        Debug.Log($"<color=cyan>[Rescue] {rescuer.Data?.operatorName} cứu {target.Data?.operatorName} — " +
                  $"người cứu {rescuer.Morale.Value:F0} stress / {rescuer.Sp.Current:F0} SP · " +
                  $"người được cứu {target.Morale.Value:F0}</color>");

        Deselect();
    }

    // ── Target picking ────────────────────────────────────────────────────────

    private void BeginRescuePick(TDOperatorView rescuer, List<TDOperatorView> targets)
    {
        m_RescuePayer = rescuer;
        m_RescueCandidates.Clear();
        m_RescueCandidates.AddRange(targets);

        // The diamond goes away for the duration. Its two buttons sit roughly where the adjacent
        // operators are on screen, so leaving it up would put the panel on top of the very
        // candidates the player now has to tap.
        TDDiamondPanelView.Instance?.Hide();
        HideRangeHighlights();

        Debug.Log($"<color=cyan>[Rescue] {targets.Count} người cần cứu quanh " +
                  $"{rescuer.Data?.operatorName} — chọn một</color>");
    }

    /// <summary>
    /// Blinks the candidates, and quietly drops any that stop being valid — one dies, one recovers
    /// on its own, or the rescuer itself collapses while the player is deciding. The world does not
    /// pause for this choice, so the offer has to keep matching it.
    /// </summary>
    private void BlinkRescueCandidates()
    {
        if (m_RescuePayer == null || m_RescuePayer.IsCollapsed) { EndRescuePick(); return; }

        bool on = Mathf.Repeat(Time.unscaledTime, RESCUE_BLINK_PERIOD) < RESCUE_BLINK_PERIOD * 0.5f;

        for (int i = m_RescueCandidates.Count - 1; i >= 0; i--)
        {
            var c = m_RescueCandidates[i];
            // `== null` and not `?.` — a destroyed Unity object is not real null, so the
            // null-conditional operator would sail straight past it and throw.
            if (c == null || !c.IsCollapsed)
            {
                c?.SetSelected(false);
                m_RescueCandidates.RemoveAt(i);
                continue;
            }
            c.SetSelected(on);
        }

        if (m_RescueCandidates.Count == 0) EndRescuePick();
    }

    private void ResolveRescueTap(Vector2 screenPos)
    {
        var payer = m_RescuePayer;

        Ray ray = MainCam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var op = hit.collider.GetComponentInParent<TDOperatorView>();
            if (op != null && m_RescueCandidates.Contains(op))
            {
                EndRescuePick();
                PerformRescue(payer, op);
                return;
            }
        }

        // Anything else backs out. Tapping away from a decision must always be an exit, or the
        // player who opened this by accident is trapped in it while the wave keeps coming.
        EndRescuePick();
    }

    private void EndRescuePick()
    {
        foreach (var c in m_RescueCandidates)
            if (c != null) c.SetSelected(false);

        m_RescueCandidates.Clear();
        m_RescuePayer = null;
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
        // Also ends any target pick in progress. Deselect is what pause, victory, game over and
        // starting a new deploy all call, and every one of those must clear the blinking
        // indicators — otherwise they keep flashing over a paused game with no way to stop them.
        EndRescuePick();

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

        // The deployed operator's own row, not one resolved from their archetype — otherwise
        // this panel draws Striker's reach over Ace and the player is shown a lie.
        var data = op.Data;
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

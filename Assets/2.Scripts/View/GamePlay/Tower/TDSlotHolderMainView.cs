using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The deploy slot bar only: builds slot buttons, drives cost/icon/interactability,
/// and on PointerDown hands off to TDDeployController (which owns the ghost + gesture + diamond).
/// </summary>
public class TDSlotHolderMainView : MonoBehaviour
{
    private GameObject m_TowerHolderPrefab;
    private Transform m_TowerHolderContainer;
    private readonly List<TDSlotHolderItemView> m_SlotHolders = new List<TDSlotHolderItemView>();

    private TDDeployController m_Deploy;
    private GameObject m_PlacementPanel;

    private void Start()
    {
        m_Deploy = gameObject.AddComponent<TDDeployController>();
        InitViews();
        InitTowerHolder();
        InitPlacementPanel();
        m_Deploy.Init(m_PlacementPanel);

        // Tutorial orchestrator — zero scene setup, auto-disables itself once
        // the player completes their first deploy (PlayerPrefs flag).
        gameObject.AddComponent<TDTutorialView>();

        TDGoldControl.api.onGoldChanged += RefreshHolderInteractability;

        if (TDOperatorRoster.api != null)
            TDOperatorRoster.api.OnAvailabilityChanged += RefreshAvailability;

        if (TDDeployCap.api != null)
            TDDeployCap.api.OnChanged += RefreshAvailability;
    }

    private void OnDestroy()
    {
        if (TDGoldControl.api != null)
            TDGoldControl.api.onGoldChanged -= RefreshHolderInteractability;

        if (TDOperatorRoster.api != null)
            TDOperatorRoster.api.OnAvailabilityChanged -= RefreshAvailability;

        if (TDDeployCap.api != null)
            TDDeployCap.api.OnChanged -= RefreshAvailability;
    }

    // ── Slot bar ────────────────────────────────────────────────────────────────

    private void InitViews()
    {
        m_TowerHolderPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_SLOT_HOLDER);
        m_TowerHolderContainer = transform;
    }

    private void InitTowerHolder()
    {
        if (m_TowerHolderPrefab == null)
        {
            Debug.LogError("<color=red>TDSlotHolderMainView: m_TowerHolderPrefab is not assigned!</color>");
            return;
        }

        TDTowerMainControl.api.BuildSlots();

        foreach (var slot in TDTowerMainControl.api.ActiveSlots)
        {
            var go = Instantiate(m_TowerHolderPrefab, m_TowerHolderContainer);
            var holder = go.GetComponent<TDSlotHolderItemView>();
            holder.SetupSlotHolderVariables();
            holder.SetupSlot(slot);
            m_SlotHolders.Add(holder);
        }

        SetupOnSelectSlot();
        RefreshHolderInteractability(TDGoldControl.api.Gold);
    }

    private void RefreshHolderInteractability(int gold)
    {
        foreach (var holder in m_SlotHolders)
            holder.SetInteractable(holder.CanSelect(gold));
    }

    /// <summary>An operator deployed, retreated, died, or came off cooldown — re-read every card.</summary>
    private void RefreshAvailability()
        => RefreshHolderInteractability(TDGoldControl.api != null ? TDGoldControl.api.Gold : 0);

    private void SetupOnSelectSlot()
    {
        for (int i = 0; i < m_SlotHolders.Count; i++)
        {
            int index = i;
            var btn = m_SlotHolders[i].towerSelectButton;

            // PointerDown (not onClick): the ghost is created the instant the finger presses,
            // so the player can drag from the slot straight onto the map without lifting.
            var trigger = btn.gameObject.GetComponent<EventTrigger>()
                       ?? btn.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ =>
            {
                // Button.interactable does NOT reach this. It gates the Button's own onClick and
                // tints the graphic; the EventSystem still delivers PointerDown to every handler
                // on the object, and EventTrigger is a separate one. Without this line the greyed
                // card is decoration — which is why a deployed operator could be picked again.
                int gold = TDGoldControl.api?.Gold ?? 0;
                if (!m_SlotHolders[index].CanSelect(gold))
                {
                    if (m_SlotHolders[index].BlockedOnlyByCap(gold)) TDDeployCap.api.NotifyRejected();
                    return;
                }

                m_Deploy.SetSlotIndex(index);
                TDTowerMainControl.api.OnSelectTowerHolder(index);
            });
            trigger.triggers.Add(entry);
        }
    }

    // ── Placement panel (legacy Cancel button — kept as fallback) ────────────────

    private void InitPlacementPanel()
    {
        var panelT = transform.Find(TDConstant.GAMEPLAY_PLACEMENT_PANEL);
        if (panelT == null)
        {
            Debug.LogWarning("<color=orange>TDSlotHolderMainView: PlacementPanel not found in hierarchy</color>");
            return;
        }
        m_PlacementPanel = panelT.gameObject;

        var cancelBtn = transform.Find(TDConstant.GAMEPLAY_BTN_CANCEL_PLACE)?.GetComponent<Button>();
        cancelBtn?.onClick.AddListener(() => TDUserInputControl.api.OnMouseButton1Clicked());

        m_PlacementPanel.SetActive(false);
    }
}

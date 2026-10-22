using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TDSlotHolderItemView : MonoBehaviour
{
    private TMP_Text m_TxtTowerCost;
    private Image m_IconImage;
    private GameObject m_DisabledOverlay;

    private TDEnums.TowerType m_UnitType;
    private OperatorData m_OperatorData;

    public Button towerSelectButton { get; private set; }
    public int Cost { get; private set; }

    /// <summary>
    /// False while this operator is already on the field or still inside their retreat cooldown.
    /// Turrets are unaffected — the one-at-a-time rule is about people, not emplacements.
    /// </summary>
    public bool IsAvailable
        => m_UnitType != TDEnums.TowerType.Operator
        || TDOperatorRoster.api == null
        || TDOperatorRoster.api.CanDeploy(m_OperatorData);

    /// <summary>
    /// The single condition behind both the greyed-out look AND the click guard. They were two
    /// expressions in two files once, and the pair drifted the moment one of them stopped being
    /// enforcement: `Button.interactable = false` greys the card and blocks the Button's own
    /// onClick, but the EventSystem still delivers PointerDown to the EventTrigger on the same
    /// object, so the card stayed fully clickable while looking disabled.
    /// </summary>
    public bool CanSelect(int gold) => gold >= Cost && IsAvailable && !(TDDeployCap.api?.IsFull ?? false);

    /// <summary>Selectable but for the deploy cap — the one refusal the cap counter should flash for.</summary>
    public bool BlockedOnlyByCap(int gold) => gold >= Cost && IsAvailable && (TDDeployCap.api?.IsFull ?? false);

    // Canvas units against the card icon's 144: big enough to read at a glance, tucked into the top-left
    // corner so it never covers the price tag in the bottom-right one (Tầng 4b §4.5).
    public const float MORALE_BADGE_SIZE = 56f;
    private static readonly Vector2 k_BadgeInset = new Vector2(8f, -8f);

    private TDMoraleBadge m_Badge;
    private GameObject m_BadgeRoot;

    /// <summary>
    /// Whether an operator's card wears their morale badge: on the field, or still carrying stress or a
    /// cooldown from their last stint. The roster keeps stress when they leave, so a Stressed badge on a
    /// card says "don't drop them back in yet". A fresh, rested operator's card stays clean.
    /// </summary>
    public static bool ShowMoraleOnCard(bool deployed, float value, float retreatCooldown)
        => deployed || value > 0f || retreatCooldown > 0f;

    /// <summary>Shows or hides the badge for `morale` and feeds it. Update calls this with roster data.</summary>
    public void ShowMorale(TDOperatorMorale morale, bool deployed)
    {
        if (m_Badge == null || morale == null) return;

        bool show = ShowMoraleOnCard(deployed, morale.Value, morale.RetreatCooldown);
        if (m_BadgeRoot.activeSelf != show) m_BadgeRoot.SetActive(show);
        if (show) m_Badge.Refresh(morale);
    }

    public void SetupSlot(TDTowerSlotInfo slot)
    {
        m_UnitType = slot.towerType;
        m_OperatorData = slot.operatorData;
        SetupSlotCost(slot.cost);
        SetupIcon(slot.icon);
        if (m_UnitType == TDEnums.TowerType.Operator) BuildMoraleBadge();
    }

    private void Update()
    {
        // Read straight from the roster every frame rather than on events: stress moves continuously
        // and off-field recovery has no event at all. One dictionary lookup per card, eight cards at most.
        var roster = TDOperatorRoster.api;
        if (m_Badge == null || roster == null) return;
        ShowMorale(roster.MoraleOf(m_OperatorData), roster.IsDeployed(m_OperatorData));
    }

    private void BuildMoraleBadge()
    {
        if (m_BadgeRoot != null) return;

        var go = new GameObject("MoraleBadge", typeof(RectTransform), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = k_BadgeInset;
        rt.sizeDelta = Vector2.one * MORALE_BADGE_SIZE;
        // Last child, over the DisabledOverlay: the card greys out while its operator is on the field,
        // which is exactly when the badge has to be read in full colour.
        rt.SetAsLastSibling();

        // The HUD is a screen-space overlay, so nothing 3D can cover it: the canvas default material.
        m_Badge = TDMoraleBadge.Build(rt, null);
        m_BadgeRoot = go;
        go.SetActive(false);
    }

    public void SetupSlotHolderVariables()
    {
        m_TxtTowerCost = transform.Find(TDConstant.GAMEPLAY_TEXT_COST_TOWER_HOLDER).GetComponent<TMP_Text>();
        towerSelectButton = transform.Find(TDConstant.GAMEPLAY_BUTTON_TOWER_HOLDER).GetComponent<Button>();
        m_DisabledOverlay = transform.Find(TDConstant.PATH_SLOT_DISABLED_OVERLAY)?.gameObject;
        m_IconImage = transform.Find(TDConstant.PATH_SLOT_ICON)?.GetComponent<Image>();
    }

    public void SetupSlotCost(int cost)
    {
        Cost = cost;
        m_TxtTowerCost.text = $"{cost}$";
    }

    /// Assigns the icon sprite — null-safe (if no icon is provided, the default image is preserved).
    public void SetupIcon(Sprite icon)
    {
        if (m_IconImage == null || icon == null) return;
        m_IconImage.sprite = icon;
        m_IconImage.enabled = true;
    }

    public void SetInteractable(bool interactable)
    {
        if (towerSelectButton != null)
            towerSelectButton.interactable = interactable;

        m_DisabledOverlay?.SetActive(!interactable);
    }
}

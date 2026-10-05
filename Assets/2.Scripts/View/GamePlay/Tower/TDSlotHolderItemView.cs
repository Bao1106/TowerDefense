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
    public bool CanSelect(int gold) => gold >= Cost && IsAvailable;

    public void SetupSlot(TDTowerSlotInfo slot)
    {
        m_UnitType = slot.towerType;
        m_OperatorData = slot.operatorData;
        SetupSlotCost(slot.cost);
        SetupIcon(slot.icon);
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

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TDSlotHolderItemView : MonoBehaviour
{
    private const string PATH_DISABLED_OVERLAY = "DisabledOverlay";
    private const string PATH_ICON             = "SlotIcon";

    private TMP_Text    m_TxtTowerCost;
    private Image       m_IconImage;
    private GameObject  m_DisabledOverlay;

    public Button towerSelectButton { get; private set; }
    public int    Cost              { get; private set; }

    public void SetupSlotHolderVariables()
    {
        m_TxtTowerCost    = transform.Find(TDConstant.GAMEPLAY_TEXT_COST_TOWER_HOLDER).GetComponent<TMP_Text>();
        towerSelectButton = transform.Find(TDConstant.GAMEPLAY_BUTTON_TOWER_HOLDER).GetComponent<Button>();
        m_DisabledOverlay = transform.Find(PATH_DISABLED_OVERLAY)?.gameObject;
        m_IconImage       = transform.Find(PATH_ICON)?.GetComponent<Image>();
    }

    public void SetupSlotCost(int cost)
    {
        Cost                = cost;
        m_TxtTowerCost.text = $"{cost}$";
    }

    /// Assign icon sprite — null-safe (nếu chưa gán icon thì giữ nguyên image mặc định).
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

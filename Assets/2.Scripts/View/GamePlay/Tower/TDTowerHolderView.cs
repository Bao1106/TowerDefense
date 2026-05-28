using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TDTowerHolderView : MonoBehaviour
{
    private const string PATH_DISABLED_OVERLAY = "DisabledOverlay";

    private TMP_Text    m_TxtTowerCost;
    private GameObject  m_DisabledOverlay;

    public Button towerSelectButton { get; private set; }
    public int    Cost              { get; private set; }

    public void SetupTowerCost(int cost)
    {
        Cost                = cost;
        m_TxtTowerCost.text = $"{cost}$";
    }

    public void SetInteractable(bool interactable)
    {
        if (towerSelectButton != null)
            towerSelectButton.interactable = interactable;

        m_DisabledOverlay?.SetActive(!interactable);
    }

    public void SetupTowerHolderVariables()
    {
        m_TxtTowerCost    = transform.Find(TDConstant.GAMEPLAY_TEXT_COST_TOWER_HOLDER).GetComponent<TMP_Text>();
        towerSelectButton = transform.Find(TDConstant.GAMEPLAY_BUTTON_TOWER_HOLDER).GetComponent<Button>();
        m_DisabledOverlay = transform.Find(PATH_DISABLED_OVERLAY)?.gameObject;
    }
}

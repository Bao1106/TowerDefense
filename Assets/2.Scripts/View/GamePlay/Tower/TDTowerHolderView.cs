using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TDTowerHolderView : MonoBehaviour
{
    private TMP_Text m_TxtTowerCost;

    public Button towerSelectButton { get; private set; }
    
    public void SetupTowerCost(int cost)
    {
        m_TxtTowerCost.text = $"{cost}$";
    }
        
    public void SetupTowerHolderVariables()
    {
        m_TxtTowerCost = transform.Find(TDConstant.GAMEPLAY_TEXT_COST_TOWER_HOLDER).GetComponent<TMP_Text>();
        towerSelectButton = transform.Find(TDConstant.GAMEPLAY_BUTTON_TOWER_HOLDER).GetComponent<Button>();
    }
}

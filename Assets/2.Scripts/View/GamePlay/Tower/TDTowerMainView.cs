using System.Collections.Generic;
using TDEnums;
using Services.DependencyInjection;
using UnityEngine;

public class TDTowerMainView : MonoBehaviour
{
    private readonly List<TDTowerHolderView> m_TowerHolders = new List<TDTowerHolderView>();
    private TDTowerHolderView m_TdTowerHolderView0, m_TdTowerHolderView1, m_TdTowerHolderView2, m_TdTowerHolderView3, m_TdTowerHolderView4;
    private GameObject m_CurrentTower;
    
    private int m_CurrentRotationIndex;
    
    private void Start()
    {
        RegistryTowerControlEvents();
        InitTowerHolder();
    }

    private void Update()
    {
        TDTowerMainControl.api.OnSelectTower(m_CurrentTower);
        
        if (Input.GetMouseButtonDown(0))
        {
            TDUserInputControl.api.OnMouseButton0Clicked();
        }
        else if (Input.GetKeyDown(KeyCode.E)) // Press E to rotate clockwise
        {
            TDUserInputControl.api.OnMouseButtonEClicked();
        }
        else if (Input.GetKeyDown(KeyCode.Q)) // Press Q to rotate counterclockwise
        {
            TDUserInputControl.api.OnMouseButtonQClicked();
        }
        else if (Input.GetMouseButtonDown(1)) // Right click to cancel placement
        {
            TDUserInputControl.api.OnMouseButton1Clicked();
        }
    }

    private void RegistryTowerControlEvents()
    {
        TDTowerMainControl.api.onGetTowerName += OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex += OnGetCurrentRotationIndex;
        
        TDPlaceTowerControl.api.onPlaceTowerSuccess += OnPlaceTowerSuccess;
        
        TDUserInputControl.api.onMouseButton0Clicked += OnMouseButton0Clicked;
        TDUserInputControl.api.onMouseButton1Clicked += OnMouseButton1Clicked;
        TDUserInputControl.api.onMouseButtonEClicked += OnMouseButtonEClicked;
        TDUserInputControl.api.onMouseButtonQClicked += OnMouseButtonQClicked;
    }

    private void OnDestroy()
    {
        TDTowerMainControl.api.onGetTowerName -= OnCreateTower;
        TDTowerMainControl.api.onGetCurrentRotationIndex -= OnGetCurrentRotationIndex;
        
        TDPlaceTowerControl.api.onPlaceTowerSuccess -= OnPlaceTowerSuccess;
        
        TDUserInputControl.api.onMouseButton0Clicked -= OnMouseButton0Clicked;
        TDUserInputControl.api.onMouseButton1Clicked -= OnMouseButton1Clicked;
        TDUserInputControl.api.onMouseButtonEClicked -= OnMouseButtonEClicked;
        TDUserInputControl.api.onMouseButtonQClicked -= OnMouseButtonQClicked;
    }
    
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
        foreach (var tower in m_TowerHolders)
        {
            var index = m_TowerHolders.IndexOf(tower);
            tower.towerSelectButton.onClick.AddListener(() => TDTowerMainControl.api.OnSelectTowerHolder(index));
        }
    }
    
    private void OnCreateTower(string towerName)
    {
        if (m_CurrentTower != null)
        {
            Destroy(m_CurrentTower);
        }

        GameObject prefab = RepResourceObject.GetResource<GameObject>(towerName);
        m_CurrentTower = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        m_CurrentTower.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
    }

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
        }
    }
    
    private void OnMouseButtonQClicked(bool isClicked)
    {
        if (isClicked)
        {
            TDTowerMainControl.api.RotateTowerCounterClockwise(m_CurrentTower, m_CurrentRotationIndex);
            TDUserInputControl.api.onMouseButtonQClicked(false);
        }
    }
    
    private void OnMouseButtonEClicked(bool isClicked)
    {
        if (isClicked)
        {
            TDTowerMainControl.api.RotateTowerClockwise(m_CurrentTower, m_CurrentRotationIndex);
            TDUserInputControl.api.onMouseButtonEClicked(false);
        }
    }
    
    private void OnMouseButton1Clicked(bool isClicked)
    {
        if (isClicked)
        {
            CancelPlacement();
            TDUserInputControl.api.onMouseButton1Clicked(false);
        }
    }
    
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
    }
}
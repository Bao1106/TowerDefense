using System;
using TDEnums;
using UnityEngine;
using Random = UnityEngine.Random;

public class TDTowerFactoryView : MonoBehaviour
    {
        private void Start()
        {
            RegistryTowerFactoryEvents();
        }

        private void RegistryTowerFactoryEvents()
        {
            TDTowerFactoryControl.api.onCreateTowerSuccess += OnCreateTowerSuccess;
        }

        private void OnDestroy()
        {
            TDTowerFactoryControl.api.onCreateTowerSuccess -= OnCreateTowerSuccess;
        }

        private void OnCreateTowerSuccess(TDTowerWeaponView tower)
        {
            float randomID = Random.Range(1000, 9999);
            string key = $"{randomID} - {tower.gameObject.name}";
            TDTowerBehaviorSubControl.api.SetupSubControl(tower.towerType);
            
            tower.Init(key);
        }
    }
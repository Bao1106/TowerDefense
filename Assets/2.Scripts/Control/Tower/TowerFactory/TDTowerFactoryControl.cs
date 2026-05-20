using System;
using UnityEngine;
using Object = UnityEngine.Object;

public class TDTowerFactoryControl : ITowerFactoryControl
{
    public static TDTowerFactoryControl api;

    public Action<TDTowerWeaponView> onCreateTowerSuccess;
    
    public void CreateTower(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        TDTowerWeaponView tower = Object
            .Instantiate(prefab, position, rotation)
            .GetComponent<TDTowerWeaponView>();
        onCreateTowerSuccess?.Invoke(tower);
    }
}

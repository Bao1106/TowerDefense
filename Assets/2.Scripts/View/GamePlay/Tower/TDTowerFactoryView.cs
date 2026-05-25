using UnityEngine;
using Random = UnityEngine.Random;

public class TDTowerFactoryView : MonoBehaviour
{
    private void Start()
    {
        TDTowerFactoryControl.api.onCreateTowerSuccess += OnCreateTowerSuccess;
    }

    private void OnDestroy()
    {
        TDTowerFactoryControl.api.onCreateTowerSuccess -= OnCreateTowerSuccess;
    }

    private void OnCreateTowerSuccess(TDTowerWeaponView tower)
    {
        // SetupSubControl removed — không còn global state cần setup
        // Pool tạo lazy trong TDFlyweightBulletFactoryModel khi tower bắn lần đầu
        string key = $"{Random.Range(1000, 9999)} - {tower.gameObject.name}";
        tower.Init(key);
    }
}
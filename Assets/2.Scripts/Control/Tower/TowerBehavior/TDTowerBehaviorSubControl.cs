using TDEnums;
using UnityEngine;

public class TDTowerBehaviorSubControl
{
    public static TDTowerBehaviorSubControl api;

    public void SetupSubControl(TowerType type)
    {
        TDTowerBehaviorModel.api.settings = TDFlyweightBulletFactoryModel.api.Setting;
        TDTowerBehaviorModel.api.settings.SetPrefab(type);
        TDFlyweightBulletFactoryModel.api.SetTowerType(type);
    }
    
    public void Attack(Transform target, Transform spawnProjectile, TowerType type)
    {
        TDBulletsView projectile = TDFlyweightBulletFactoryModel.Spawn(TDTowerBehaviorModel.api.settings);
        if (projectile != null)
        {
            projectile.transform.position = spawnProjectile.position;
            projectile.Damage = TDTowerBehaviorModel.api.GetDamage(type);
                
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = (target.position - projectile.transform.position).normalized * 20f;
            }
        }
    }
}

using TDEnums;
using UnityEngine;

public class TDTowerBehaviorSubControl
{
    public static TDTowerBehaviorSubControl api;

    // SetupSubControl removed — không còn global state cần setup trước khi bắn
    // Pool được tạo lazy trong TDFlyweightBulletFactoryModel.GetPoolFor(type) khi bắn lần đầu

    public void Attack(Transform target, Transform spawnProjectile, TowerType type)
    {
        // Spawn đúng bullet type — stateless, không phụ thuộc bất kỳ global field nào
        TDBulletsView bullet = TDFlyweightBulletFactoryModel.Spawn(type);
        if (bullet == null) return;

        bullet.transform.position = spawnProjectile.position;
        bullet.Damage = TDTowerBehaviorModel.api.GetDamage(type);

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
            rb.velocity = (target.position - bullet.transform.position).normalized * 20f;
    }
}

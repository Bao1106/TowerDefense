using TDEnums;
using UnityEngine;

public class TDBulletsView : MonoBehaviour
{
    // Set bởi TDFlyweightBulletFactoryModel.Spawn() — dùng bởi ReturnToPool để trả đúng pool
    public TowerType OwnerType { get; set; }
    public float     Damage    { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out TDEnemyView enemy))
        {
            enemy.TakeDamage(Damage);
            TDFlyweightBulletFactoryModel.ReturnToPool(this);
        }
    }
}
using UnityEngine;

public class TDBulletsView : MonoBehaviour
{
    public float Damage { get; set; }
        
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out TDEnemyView enemy))
        {
            //var enemyController = other.gameObject.GetComponent<TDEnemyView>();
            enemy.TakeDamage(Damage);
            TDFlyweightBulletFactoryModel.ReturnToPool(this);
        }
    }
}
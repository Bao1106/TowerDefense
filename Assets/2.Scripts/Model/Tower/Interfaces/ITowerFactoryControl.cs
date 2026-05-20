using UnityEngine;

public interface ITowerFactoryControl
{
    void CreateTower(GameObject prefab, Vector3 position, Quaternion rotation);
}
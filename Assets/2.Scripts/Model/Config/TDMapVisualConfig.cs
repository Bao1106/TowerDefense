using UnityEngine;

[CreateAssetMenu(menuName = "Game Configs/Map Visual Config", fileName = "MapVisualConfig")]
public class TDMapVisualConfig : ScriptableObject
{
    [Header("Terrain")]
    public Material MapGroundMaterial;
    public GameObject PathTilePrefab;
    public GameObject TowerZonePrefab;
    public GameObject ObstacleTilePrefab;
    public GameObject[] ObstaclePrefabs;

    [Header("Lighting")]
    public Material SkyboxMaterial;
    public Color AmbientColor = Color.white;
}

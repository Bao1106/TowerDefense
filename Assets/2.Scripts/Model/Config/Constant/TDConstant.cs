using UnityEngine;

public static class TDConstant
{
    #region Config Scene

    public const string SCENE_INIT = "Init";
    public const string SCENE_LOAD_FIRST = "DTLoadFirst";
    public const string SCENE_GAMEPLAY = "DTGamePlay";

    #endregion

    #region Miniapp Variables Config

    //GamePlay Scene
    public const string GAMEPLAY_TOWER_HOLDER_0 = "TowerHolder";
    public const string GAMEPLAY_TOWER_HOLDER_1 = "TowerHolder (1)";
    public const string GAMEPLAY_TOWER_HOLDER_2 = "TowerHolder (2)";
    public const string GAMEPLAY_TOWER_HOLDER_3 = "TowerHolder (3)";
    public const string GAMEPLAY_TOWER_HOLDER_4 = "TowerHolder (4)";

    public const string GAMEPLAY_TEXT_COST_TOWER_HOLDER = "Tower/Cost";
    public const string GAMEPLAY_BUTTON_TOWER_HOLDER = "ButtonSelector";
    public const string GAMEPLAY_TOWER_BULLET_SPAWN = "SpawnBullet";
    public const string GAMEPLAY_ENEMY_PATH_MAIN_VIEW = "EnemyPathMainView";
    public const string GAMEPLAY_ENEMY_PATH_CREATE_POINT = "PathView/CreatePoint";
    public const string GAMEPLAY_ENEMY_PATH_VIEW = "PathView";
    public const string GAMEPLAY_MAP_VISUALIZE = "GameMapVisualize";
    
    //Config Values
    public static readonly float[] CONFIG_TOWER_ROTATIONS = { 0f, 90f, 180f, 270f };
    public static readonly Vector2Int[] CONFIG_ENEMY_WAYPOINTS =
    {
        new Vector2Int(2, 1), new Vector2Int(3, 5),
        new Vector2Int(7, 2), new Vector2Int(9, 6),
        new Vector2Int(8, 0), new Vector2Int(5, 4),
        new Vector2Int(6, 3), new Vector2Int(4, 0)
    };
    public static readonly Vector2Int CONFIG_ENEMY_START_POINT = new Vector2Int(0, 5);
    public static readonly Vector2Int CONFIG_ENEMY_END_POINT = new Vector2Int(10, 0);
    public const int CONFIG_ENEMIES_NUMBER = 5;
    public const int CONFIG_ENEMY_SPAWN_INTERVAL = 5;
    public const float CONFIG_PATH_OFFSET_Y = 0.1f;
    public const float CONFIG_ENEMY_MOVE_SPEED = 2f;
    public const float CONFIG_ENEMY_HEALTH = 300f;
    public const float CONFIG_GRID_CELL_SIZE = 2f;
    public const string CONFIG_TOWER = "Tower Bullet Config";

    #endregion

    #region Prefab

    public const string PREFAB_FATTY_CANNON_G02 = "FattyCannonG02";
    public const string PREFAB_FATTY_CATAPULT_G02 = "FattyCatapultG02";
    public const string PREFAB_FATTY_MISSILE_G02 = "FattyMissileG02";
    public const string PREFAB_FATTY_MISSILE_G03 = "FattyMissileG03";
    public const string PREFAB_FATTY_MORTAR_G02 = "FattyMortarG02";
    public const string PREFAB_SLIME = "Slime";
    public const string PREFAB_PATH = "Plane";

  #endregion
}

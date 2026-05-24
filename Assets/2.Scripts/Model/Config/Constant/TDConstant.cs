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

    public const string GAMEPLAY_TEXT_COST_TOWER_HOLDER = "CostBg/Cost";
    public const string GAMEPLAY_BUTTON_TOWER_HOLDER = "ButtonSelector";

    // Placement panel — hiện khi đang chọn tower, ẩn khi đặt xong / cancel
    // Mobile: drag direction = hướng tower (Arknights-style), nhấc ngón = đặt tower
    // Desktop: E/Q rotate, LMB place, RMB cancel (keyboard shortcuts vẫn hoạt động)
    public const string GAMEPLAY_PLACEMENT_PANEL   = "PlacementPanel";
    public const string GAMEPLAY_BTN_CANCEL_PLACE  = "PlacementPanel/BtnCancel";
    public const string GAMEPLAY_TOWER_BULLET_SPAWN = "SpawnBullet";
    public const string GAMEPLAY_ENEMY_PATH_MAIN_VIEW = "EnemyPathMainView";
    public const string GAMEPLAY_ENEMY_PATH_CREATE_POINT = "PathView/CreatePoint";
    public const string GAMEPLAY_ENEMY_PATH_VIEW = "PathView";
    public const string GAMEPLAY_MAP_VISUALIZE = "GameMapVisualize";
    
    //Config Values
    public static readonly float[] CONFIG_TOWER_ROTATIONS = { 0f, 90f, 180f, 270f };
    // Grid 20×12 (plane scale 4×2.4, cellSize 2 → world 40×24, position Z=28)
    // Plane Z range 16..40 → bottom edge above HUD-clear line ~16.4 → toàn bộ grid visible
    // → không cần buffer Y → PATH_MIN_GRID_Y = 0
    public const int CONFIG_PATH_MIN_GRID_Y = 0;

    // START/END cùng Y=4 (giữa grid 8 tall) → 3 paths đi vòng trên/dưới đối xứng
    public static readonly Vector2Int CONFIG_ENEMY_START_POINT = new Vector2Int(0,  6);
    public static readonly Vector2Int CONFIG_ENEMY_END_POINT   = new Vector2Int(20, 6);

    // Paths được gen RANDOM mỗi game qua TDPathGeneratorControl
    // → mỗi path nằm trong 1 Y-zone riêng để 3 paths không đè chaos
    //
    // Grid 21×12, start/end tại Y=6. Zones (yMin, yMax inclusive):
    //   Path 0 — Top zone     Y= 7..11 (5 rows trên start.y=6)
    //   Path 1 — Bottom zone  Y= 0..5  (6 rows dưới start.y=6)
    //   Path 2 — Mid sweep    Y= 2..9  (spans most rows)
    public static readonly Vector2Int[] CONFIG_PATH_Y_ZONES =
    {
        new Vector2Int(7, 11), // Path 0: top
        new Vector2Int(0, 5),  // Path 1: bottom
        new Vector2Int(2, 9),  // Path 2: mid sweep
    };

    // Tunables cho random waypoint generator (grid 21 wide)
    public const int CONFIG_PATH_MIN_STEP_X         = 2;  // horizontal segment ngắn nhất
    public const int CONFIG_PATH_MAX_STEP_X         = 5;  // horizontal segment dài nhất
    public const int CONFIG_PATH_MIN_VERTICAL_DELTA = 2;  // |dY| tối thiểu giữa 2 vertical strokes liên tiếp

    // --- OBSTACLE CONFIG ---
    // Obstacles là visual decoration (trees/rocks) — không ảnh hưởng path shape
    // Đặt vào các cells KHÔNG phải path cell và KHÔNG quá gần start/end
    public const int CONFIG_OBSTACLE_COUNT            = 12;
    // Buffer (số cell) quanh START và END không đặt obstacle
    public const int CONFIG_OBSTACLE_EXCLUSION_RADIUS = 2;
    // Khoảng cách tối thiểu (Manhattan) giữa 2 obstacle centers
    public const int CONFIG_OBSTACLE_SPACING          = 3;
    public const int CONFIG_PLAYER_STARTING_LIVES  = 20;
    public const int CONFIG_ENEMIES_NUMBER        = 5;
    public const int CONFIG_ENEMY_SPAWN_DELAY_MS  = 2000;
    public const int CONFIG_WAVE_INTERVAL_MS      = 10000;
    public const int CONFIG_NUM_PATHS             = 3;
    public const int CONFIG_MAX_WAVES             = 10;
    public const float CONFIG_PATH_OFFSET_Y       = 0.1f;
    public const float CONFIG_ENEMY_MOVE_SPEED = 3f;
    public const float CONFIG_ENEMY_HEALTH = 300f;
    public const float CONFIG_GRID_CELL_SIZE = 2f;
    public const float CONFIG_ENEMY_VISUAL_SCALE = 1.5f;
    public const string CONFIG_TOWER = "Tower Bullet Config";

    #endregion

    #region Prefab

    public const string PREFAB_GATE_START        = "GateStart";
    public const string PREFAB_GATE_END          = "GateEnd";
    public const string PREFAB_FATTY_CANNON_G02 = "FattyCannonG02";
    public const string PREFAB_FATTY_CATAPULT_G02 = "FattyCatapultG02";
    public const string PREFAB_FATTY_MISSILE_G02 = "FattyMissileG02";
    public const string PREFAB_FATTY_MISSILE_G03 = "FattyMissileG03";
    public const string PREFAB_FATTY_MORTAR_G02 = "FattyMortarG02";
    public const string PREFAB_SLIME = "Slime";
    public const string PREFAB_PATH = "Plane";

  #endregion
}

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
    public const string GAMEPLAY_TOWER_HOLDER_5 = "TowerHolder (5)"; // Melee operator slot

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

    // HUD paths — relative to Canvas/SafeArea/Container (dùng bởi TDGameplayHUDView)
    public const string PATH_GAMEPLAY_HUD_BACK_BUTTON       = "Header/HUDButtonRight/BackButton";
    public const string PATH_GAMEPLAY_HUD_SETTING_BUTTON    = "Header/HUDButtonRight/SettingButton";
    public const string PATH_GAMEPLAY_HUD_ENEMY_COUNT       = "Header/MapInfo/EnemyCount/TxtValue";
    public const string PATH_GAMEPLAY_HUD_LIFE_POINT        = "Header/MapInfo/LifePoint/TxtValue";
    public const string PATH_GAMEPLAY_HUD_SPEED_BUTTON      = "Header/HUDButtonLeft/SpeedButton";
    public const string PATH_GAMEPLAY_HUD_SPEED_VALUE       = "Header/HUDButtonLeft/SpeedButton/TxtValue";
    public const string PATH_GAMEPLAY_HUD_SPEED_ICON_NORMAL = "Header/HUDButtonLeft/SpeedButton/SpeedIcon/Normal";
    public const string PATH_GAMEPLAY_HUD_SPEED_ICON_X2     = "Header/HUDButtonLeft/SpeedButton/SpeedIcon/SpeedX2";
    public const string PATH_GAMEPLAY_HUD_PAUSE_BUTTON      = "Header/HUDButtonLeft/PauseButton";
    public const string PATH_GAMEPLAY_HUD_CURRENCY          = "Bottom/Currency/TxtValue";
    public const string PATH_GAMEPLAY_HUD_PAUSE_PANEL       = "PausePanel";
    public const string PATH_GAMEPLAY_HUD_RESUME_BUTTON     = "PausePanel/ResumeButton";
    // FlashScreen nằm ở SafeArea (cha của Container) → dùng transform.parent.Find()
    public const string PATH_GAMEPLAY_SCREEN_FLASH          = "FlashScreen";

    // Result popups — children of Container (inactive by default, shown by TDGameplayHUDView)
    public const string PATH_GAMEPLAY_GAMEOVER_PANEL        = "GameOverPanel";
    public const string PATH_GAMEPLAY_GAMEOVER_RETRY_BTN    = "GameOverPanel/PopupWindow/RetryButton";
    public const string PATH_GAMEPLAY_VICTORY_PANEL         = "VictoryPanel";
    public const string PATH_GAMEPLAY_VICTORY_RETRY_BTN     = "VictoryPanel/PopupWindow/RetryButton";
    public const string PATH_GAMEPLAY_VICTORY_NEXT_BTN      = "VictoryPanel/PopupWindow/NextButton";

    //Config Values
    public static readonly float[] CONFIG_TOWER_ROTATIONS = { 0f, 90f, 180f, 270f };
    // Grid 22×13 (plane scale 4.4×2.6, cellSize 2 → world 44×26, position Z=27.7)
    // Plane X range 1.3..45.3, Z range 14.7..40.7 → aligns perfectly with CityGround 2u tiles [s20]
    // → không cần buffer Y → PATH_MIN_GRID_Y = 0
    public const int CONFIG_PATH_MIN_GRID_Y = 0;

    // START/END tại Y=6 (giữa grid 13 tall, rows 0-12)
    // Grid 22 wide (0..21): start col=0, end col=21
    public static readonly Vector2Int CONFIG_ENEMY_START_POINT = new Vector2Int(0,  6);
    public static readonly Vector2Int CONFIG_ENEMY_END_POINT   = new Vector2Int(21, 6);

    // Paths được gen RANDOM mỗi game qua TDPathGeneratorControl
    // → mỗi path nằm trong 1 Y-zone riêng để 3 paths không đè chaos
    //
    // Grid 22×13, start/end tại Y=6. Zones (yMin, yMax inclusive):
    //   Path 0 — Top zone     Y= 7..12 (6 rows trên start.y=6, dùng hết row mới)
    //   Path 1 — Bottom zone  Y= 0..5  (6 rows dưới start.y=6)
    //   Path 2 — Mid sweep    Y= 2..10 (spans most rows)
    public static readonly Vector2Int[] CONFIG_PATH_Y_ZONES =
    {
        new Vector2Int(7, 12), // Path 0: top   (extended to row 12 — new row [s20])
        new Vector2Int(0, 5),  // Path 1: bottom
        new Vector2Int(2, 10), // Path 2: mid sweep (extended from 9→10 [s20])
    };

    // Tunables cho random waypoint generator (grid 22 wide)
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
    public const int   CONFIG_PLAYER_STARTING_LIVES  = 20;
    public const int   CONFIG_PLAYER_STARTING_GOLD   = 10;
    public const float CONFIG_GOLD_PASSIVE_RATE       = 5f;   // giây/+1 gold passive
    public const int   CONFIG_LIFE_LOW_THRESHOLD      = 5;    // <= 5 life → text đỏ
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
    // TowerZone tile: scale.y = 0.12, center tại y=0 → top face tại y=0.06
    // Tower ghost và placed tower đều dùng offset này để ngồi trên zone tile
    public const float CONFIG_TOWER_ZONE_TILE_HEIGHT = 0.12f;
    public const float CONFIG_TOWER_PLACE_Y          = 0.6f;  // tower base placed 0.6u above ground (above TowerZone top face 0.06)
    public const string CONFIG_TOWER = "Tower Bullet Config";

    #endregion

    #region Prefab

    public const string PREFAB_GATE_START        = "GateStart";
    public const string PREFAB_GATE_END          = "GateEnd";
    public const string PREFAB_MELEE_OPERATOR    = "MeleeOperator"; // Arknights-style melee guard
    public const string PREFAB_FATTY_CANNON_G02 = "FattyCannonG02";
    public const string PREFAB_FATTY_CATAPULT_G02 = "FattyCatapultG02";
    public const string PREFAB_FATTY_MISSILE_G02 = "FattyMissileG02";
    public const string PREFAB_FATTY_MISSILE_G03 = "FattyMissileG03";
    public const string PREFAB_FATTY_MORTAR_G02 = "FattyMortarG02";
    public const string PREFAB_SLIME = "Slime";
    public const string PREFAB_PATH = "Plane";

  #endregion
}

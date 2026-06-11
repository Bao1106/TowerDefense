using UnityEngine;

public static class TDConstant
{
    #region Config Scene

    public const string SCENE_INIT = "Init";
    public const string SCENE_LOAD_FIRST = "DTLoadFirst";
    public const string SCENE_MAIN_MENU = "DTMainMenu";
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

    // Placement panel — shown while a tower is selected, hidden after placement or cancellation
    // Mobile: drag direction determines tower facing (Arknights-style), lifting finger confirms placement
    // Desktop: E/Q to rotate, LMB to place, RMB to cancel (keyboard shortcuts remain active)
    public const string GAMEPLAY_PLACEMENT_PANEL   = "PlacementPanel";
    public const string GAMEPLAY_BTN_CANCEL_PLACE  = "PlacementPanel/BtnCancel";
    public const string GAMEPLAY_TOWER_BULLET_SPAWN = "SpawnBullet";
    public const string GAMEPLAY_ENEMY_PATH_MAIN_VIEW = "EnemyPathMainView";
    public const string GAMEPLAY_ENEMY_PATH_CREATE_POINT = "PathView/CreatePoint";
    public const string GAMEPLAY_ENEMY_PATH_VIEW = "PathView";
    public const string GAMEPLAY_MAP_VISUALIZE = "GameMapVisualize";

    // HUD paths — relative to Canvas/SafeArea/Container (used by TDGameplayHUDView)
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
    // FlashScreen lives under SafeArea (the parent of Container) → use transform.parent.Find()
    public const string PATH_GAMEPLAY_SCREEN_FLASH          = "FlashScreen";
    // Full-screen black overlay — starts active+black so scene opens from darkness
    public const string PATH_GAMEPLAY_TRANSITION_OVERLAY    = "TransitionOverlay";

    // Result popups — children of Container (inactive by default, shown by TDGameplayHUDView)
    public const string PATH_GAMEPLAY_GAMEOVER_PANEL        = "GameOverPanel";
    public const string PATH_GAMEPLAY_GAMEOVER_RETRY_BTN    = "GameOverPanel/PopupWindow/Bottom/BtnRetry";
    public const string PATH_GAMEPLAY_VICTORY_PANEL         = "VictoryPanel";
    public const string PATH_GAMEPLAY_VICTORY_RETRY_BTN     = "VictoryPanel/PopupWindow/Bottom/BtnRetry";
    public const string PATH_GAMEPLAY_VICTORY_NEXT_BTN      = "VictoryPanel/PopupWindow/Bottom/BtnNext";

    // Stat value labels inside popups (TMP that HUDView fills at show-time)
    // Victory popup (new design: Header/Stage + Middle/Info/<RowName>/TxtValue)
    public const string PATH_VICTORY_STAGE_NAME     = "VictoryPanel/PopupWindow/Header/Stage";
    public const string PATH_VICTORY_STAT_ENEMIES   = "VictoryPanel/PopupWindow/Middle/Info/EnemiesDefeated/TxtValue";
    public const string PATH_VICTORY_STAT_LIVES     = "VictoryPanel/PopupWindow/Middle/Info/Lives Remaining/TxtValue";
    public const string PATH_VICTORY_STAT_GOLD      = "VictoryPanel/PopupWindow/Middle/Info/Gold Remaining/TxtValue";

    // GameOver popup (same new hierarchy as Victory but red-themed + different stats)
    public const string PATH_GAMEOVER_STAGE_NAME    = "GameOverPanel/PopupWindow/Header/Stage";
    public const string PATH_GAMEOVER_STAT_ENEMIES  = "GameOverPanel/PopupWindow/Middle/Info/EnemiesDefeated/TxtValue";
    public const string PATH_GAMEOVER_STAT_LIVES    = "GameOverPanel/PopupWindow/Middle/Info/LivesLost/TxtValue";
    public const string PATH_GAMEOVER_STAT_GOLD     = "GameOverPanel/PopupWindow/Middle/Info/Gold Remaining/TxtValue";
    public const string PATH_GAMEOVER_STAT_WAVE     = "GameOverPanel/PopupWindow/Middle/Info/WaveReached/TxtValue";

    // Operator action panel — relative to Canvas/SafeArea/Container (used by TDOperatorSelectionView)
    public const string PATH_OPERATOR_ACTION_PANEL  = "OperatorActionPanel";
    public const string PATH_OPERATOR_BTN_RETREAT   = "OperatorActionPanel/BtnRetreat";
    public const string PATH_OPERATOR_BTN_RETREAT_ICON  = "OperatorActionPanel/BtnRetreat/Icon";

    // Panel sizes (multiples of 12)
    public const int OPERATOR_PANEL_W      = 96;
    public const int OPERATOR_PANEL_H      = 108;
    public const int OPERATOR_BTN_SIZE     = 72;
    public const int OPERATOR_ICON_SIZE    = 48;
    public const int OPERATOR_LABEL_H      = 24;
    // World-space offset above operator for panel anchor point
    public const float OPERATOR_PANEL_WORLD_Y_OFFSET = 2.4f;

    //Config Values
    public static readonly float[] CONFIG_TOWER_ROTATIONS = { 0f, 90f, 180f, 270f };
    // Grid size is derived from GameMapVisualize plane bounds / cellSize — not hardcoded
    // Start/End are computed dynamically: start=(0, height/2), end=(width-1, height/2)

    // --- MAZE CONFIG ---
    // Extra passage rate: percentage of wall cells that are opened to create extra loops in the maze
    public const float CONFIG_MAZE_EXTRA_PASSAGE_RATE = 0.45f;
    // Obstacle ratio: percentage of wall cells used as obstacle decorations (towers cannot be placed here)
    // Remaining (1 - ratio) = tower zone tiles (towers can be placed here)
    public const float CONFIG_MAZE_OBSTACLE_WALL_RATIO = 0.15f;
    public const int   CONFIG_PLAYER_STARTING_LIVES  = 20;
    public const int   CONFIG_PLAYER_STARTING_GOLD   = 10;
    public const float CONFIG_GOLD_PASSIVE_RATE       = 3f;   // seconds per +1 passive gold tick
    public const int   CONFIG_LIFE_LOW_THRESHOLD      = 5;    // <= 5 lives → life text turns red
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
    // TowerZone tile: scale.y = 0.12, center at y=0 → top face at y=0.06
    // Both the tower ghost and the placed tower use this offset to sit on top of the zone tile
    public const float CONFIG_TOWER_ZONE_TILE_HEIGHT = 0.12f;
    // Range highlight must sit above the tower zone top face (0.12) and path tiles (0.10) to avoid z-fighting
    public const float CONFIG_RANGE_HIGHLIGHT_Y = 0.3f;
    public const float CONFIG_OPERATOR_PLACE_Y          = 0.02f;  // tower base placed 0.6u above ground (above TowerZone top face 0.06)
    public const float CONFIG_TOWER_PLACE_Y          = 0.6f;  // tower base placed 0.6u above ground (above TowerZone top face 0.06)
    public const string CONFIG_TOWER          = "Tower Bullet Config";
    public const string CONFIG_OPERATOR = "Melee Operator Config"; // dedicated ScriptableObject for melee operators
    public const string CONFIG_EFFECT   = "Configs/Effect Config"; // ScriptableObject mapping GameEventKey → EffectDef

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
    public const string PREFAB_PATH = "PathTile";
    public const string PREFAB_SLOT_HOLDER = "SlotHolder";
    public const string PREFAB_RANGE_HIGH_LIGHT = "RangeHighlight";
    public const string PREFAB_STAGE_CARD = "StagePrefab";

  #endregion
}

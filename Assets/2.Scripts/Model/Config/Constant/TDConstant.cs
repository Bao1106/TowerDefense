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
    public const string GAMEPLAY_PLACEMENT_PANEL = "PlacementPanel";
    public const string GAMEPLAY_BTN_CANCEL_PLACE = "PlacementPanel/BtnCancel";
    public const string GAMEPLAY_TOWER_BULLET_SPAWN = "SpawnBullet";
    public const string GAMEPLAY_ENEMY_PATH_MAIN_VIEW = "EnemyPathMainView";
    public const string GAMEPLAY_ENEMY_PATH_CREATE_POINT = "PathView/CreatePoint";
    public const string GAMEPLAY_ENEMY_PATH_VIEW = "PathView";
    public const string GAMEPLAY_MAP_VISUALIZE = "GameMapVisualize";

    // HUD paths — relative to Canvas/SafeArea/Container (used by TDGameplayHUDView)
    public const string PATH_GAMEPLAY_HUD_BACK_BUTTON = "Header/HUDButtonRight/BackButton";
    public const string PATH_GAMEPLAY_HUD_SETTING_BUTTON = "Header/HUDButtonRight/SettingButton";
    public const string PATH_GAMEPLAY_HUD_ENEMY_COUNT = "Header/MapInfo/EnemyCount/TxtValue";
    public const string PATH_GAMEPLAY_HUD_LIFE_POINT = "Header/MapInfo/LifePoint/TxtValue";
    public const string PATH_GAMEPLAY_HUD_SPEED_BUTTON = "Header/HUDButtonLeft/SpeedButton";
    public const string PATH_GAMEPLAY_HUD_SPEED_VALUE = "Header/HUDButtonLeft/SpeedButton/TxtValue";
    public const string PATH_GAMEPLAY_HUD_SPEED_ICON_NORMAL = "Header/HUDButtonLeft/SpeedButton/SpeedIcon/Normal";
    public const string PATH_GAMEPLAY_HUD_SPEED_ICON_X2 = "Header/HUDButtonLeft/SpeedButton/SpeedIcon/SpeedX2";
    public const string PATH_GAMEPLAY_HUD_PAUSE_BUTTON = "Header/HUDButtonLeft/PauseButton";
    public const string PATH_GAMEPLAY_HUD_CURRENCY = "Bottom/Currency/TxtValue";
    public const string PATH_GAMEPLAY_HUD_PAUSE_PANEL = "PausePanel";
    public const string PATH_GAMEPLAY_HUD_RESUME_BUTTON = "PausePanel/ResumeButton";
    // FlashScreen lives under SafeArea (the parent of Container) → use transform.parent.Find()
    public const string PATH_GAMEPLAY_SCREEN_FLASH = "FlashScreen";
    // Full-screen black overlay — starts active+black so scene opens from darkness
    public const string PATH_GAMEPLAY_TRANSITION_OVERLAY = "TransitionOverlay";

    // Result popups — children of Container (inactive by default, shown by TDGameplayHUDView)
    public const string PATH_GAMEPLAY_GAMEOVER_PANEL = "GameOverPanel";
    public const string PATH_GAMEPLAY_GAMEOVER_RETRY_BTN = "GameOverPanel/PopupWindow/Bottom/BtnRetry";
    public const string PATH_GAMEPLAY_VICTORY_PANEL = "VictoryPanel";
    public const string PATH_GAMEPLAY_VICTORY_RETRY_BTN = "VictoryPanel/PopupWindow/Bottom/BtnRetry";
    public const string PATH_GAMEPLAY_VICTORY_NEXT_BTN = "VictoryPanel/PopupWindow/Bottom/BtnNext";

    // Stat value labels inside popups (TMP that HUDView fills at show-time)
    // Victory popup (new design: Header/Stage + Middle/Info/<RowName>/TxtValue)
    public const string PATH_VICTORY_STAGE_NAME = "VictoryPanel/PopupWindow/Header/Stage";
    public const string PATH_VICTORY_STAT_ENEMIES = "VictoryPanel/PopupWindow/Middle/Info/EnemiesDefeated/TxtValue";
    public const string PATH_VICTORY_STAT_LIVES = "VictoryPanel/PopupWindow/Middle/Info/Lives Remaining/TxtValue";
    public const string PATH_VICTORY_STAT_GOLD = "VictoryPanel/PopupWindow/Middle/Info/Gold Remaining/TxtValue";

    // GameOver popup (same new hierarchy as Victory but red-themed + different stats)
    public const string PATH_GAMEOVER_STAGE_NAME = "GameOverPanel/PopupWindow/Header/Stage";
    public const string PATH_GAMEOVER_STAT_ENEMIES = "GameOverPanel/PopupWindow/Middle/Info/EnemiesDefeated/TxtValue";
    public const string PATH_GAMEOVER_STAT_LIVES = "GameOverPanel/PopupWindow/Middle/Info/LivesLost/TxtValue";
    public const string PATH_GAMEOVER_STAT_GOLD = "GameOverPanel/PopupWindow/Middle/Info/Gold Remaining/TxtValue";
    public const string PATH_GAMEOVER_STAT_WAVE = "GameOverPanel/PopupWindow/Middle/Info/WaveReached/TxtValue";

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
    public const int CONFIG_PLAYER_STARTING_LIVES = 20;
    public const int CONFIG_PLAYER_STARTING_GOLD = 10;
    public const float CONFIG_GOLD_PASSIVE_RATE = 3f; // seconds per +1 passive gold tick
    public const int CONFIG_LIFE_LOW_THRESHOLD = 5; // <= 5 lives → life text turns red
    public const int CONFIG_ENEMIES_NUMBER = 5;
    public const int CONFIG_ENEMY_SPAWN_DELAY_MS = 2000;
    public const int CONFIG_WAVE_INTERVAL_MS = 10000;
    public const int CONFIG_NUM_PATHS = 3;
    public const int CONFIG_MAX_WAVES = 10;
    public const float CONFIG_PATH_OFFSET_Y = 0.1f;
    public const float CONFIG_ENEMY_MOVE_SPEED = 3f;
    public const float CONFIG_ENEMY_HEALTH = 300f;
    public const float CONFIG_GRID_CELL_SIZE = 2f;
    public const float CONFIG_ENEMY_VISUAL_SCALE = 1.5f;
    // TowerZone tile: scale.y = 0.12, center at y=0 → top face at y=0.06
    // Both the tower ghost and the placed tower use this offset to sit on top of the zone tile
    public const float CONFIG_TOWER_ZONE_TILE_HEIGHT = 0.12f;
    // Range highlight must sit above the tower zone top face (0.12) and path tiles (0.10) to avoid z-fighting
    public const float CONFIG_RANGE_HIGHLIGHT_Y = 0.3f;
    public const float CONFIG_OPERATOR_PLACE_Y = 0.02f; // tower base placed 0.6u above ground (above TowerZone top face 0.06)
    public const float CONFIG_TOWER_PLACE_Y = 0.6f; // tower base placed 0.6u above ground (above TowerZone top face 0.06)
    public const string CONFIG_TOWER = "Tower Bullet Config";
    public const string CONFIG_OPERATOR = "Melee Operator Config"; // dedicated ScriptableObject for melee operators
    public const string CONFIG_EFFECT = "Effect Config";
    public const string CONFIG_ENEMY = "Enemy Data Config";
    public const string CONFIG_LEVEL = "Level Config";
    public const string CONFIG_STAGE_REPO = "Stage Repository";

    #endregion

    #region Prefab

    public const string PREFAB_GATE_START = "GateStart";
    public const string PREFAB_GATE_END = "GateEnd";
    public const string PREFAB_MELEE_OPERATOR = "MeleeOperator"; // Arknights-style melee guard
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
    public const string PREFAB_DIAMOND_PANEL = "DiamondPanel"; // shared deploy direction-picker / retreat panel

    #endregion

    #region Diamond Panel (deploy direction-picker + retreat — TDDiamondPanelView)

    // Fraction of cellSize used as the center dead-zone radius. Releasing the direction
    // drag inside this radius = cancel (no facing chosen). Matches DIRECTION_THRESHOLD_RATIO scale.
    public const float DIAMOND_DEADZONE_RATIO = 0.35f;
    // Ground cell reference height (touch math); visuals are full Canvas UI.
    public const float DIAMOND_GROUND_Y = 0.2f;
    // World-up offset added to the cell center when projecting to the screen anchor
    // so the diamond is centered on the operator's chest, not above the head.
    // Operator/tower mesh roughly 0–2 world units tall → 1.0 = chest height.
    public const float DIAMOND_CENTER_Y_OFFSET = 1.0f;

    #endregion
    #region Main Menu Scene (TDMainMenuView / TDStageSelectView - relative to own root)
    public const string NAME_MAIN_MENU = "MainMenu";
    public const string PATH_MENU_BACKGROUND = "Background";
    public const string PATH_MENU_LEFT_PANEL = "Middle/LeftPanel";
    public const string PATH_MENU_RIGHT_PANEL = "Middle/RightPanel";
    public const string PATH_MENU_BOTTOM = "Bottom";
    public const string PATH_STAGESEL_TXT_NUM = "Middle/LeftPanel/TxtStageNum";
    public const string PATH_STAGESEL_TXT_TITLE = "Middle/LeftPanel/TxtStageTitle";
    public const string PATH_STAGESEL_TXT_NAME = "Middle/LeftPanel/TxtStageName";
    public const string PATH_STAGESEL_TXT_DESC = "Middle/LeftPanel/TxtDescription";
    public const string PATH_STAGESEL_THEME_BG = "Middle/LeftPanel/ThemeBackground";
    public const string PATH_STAGESEL_CARD_CONTAINER = "Middle/RightPanel/StageScroll/Viewport/Content";
    public const string PATH_STAGESEL_BTN_PLAY = "Bottom/BtnPlay";
    public const string PATH_STAGESEL_BTN_BACK = "Bottom/BtnBack";
    #endregion
    #region Class Constants (moved from individual scripts - S64)
    // Scene transition (TDSceneController)
    public const float SCENE_FADE_OUT_DUR = 0.25f;
    public const float SCENE_FADE_IN_DUR = 0.4f;
    // Game speed (TDSpeedControl)
    public const float SPEED_NORMAL = 1f;
    public const float SPEED_FAST = 2f;
    // Path/maze generation
    public const int CONFIG_GATE_BUFFER = 2;
    public const int MAZE_MAX_ATTEMPTS = 10;
    // Tower slots (TDTowerMainControl)
    public const int CONFIG_MAX_SLOTS = 8;
    // Audio (TDAudioPrefs / TDBGMPlayer / TDSFXPlayer)
    public const string AUDIO_KEY_BGM = "audio_bgm_muted";
    public const string AUDIO_KEY_SFX = "audio_sfx_muted";
    public const float BGM_FADE_IN = 1f;
    public const float BGM_FADE_OUT = 1.5f;
    public const float BGM_MUTE_FADE = 0.3f;
    public const int SFX_POOL_SIZE = 8;
    public const float SFX_MIN_INTERVAL = 0.05f;
    // Materials (TDStageMaterialCache)
    public const string SHADER_URP_UNLIT = "Universal Render Pipeline/Unlit";
    public const string RES_TOWER_HIGHLIGHT_MAT = "Materials/TowerHighlight";
    // Result popups (TDVictoryPanelView + TDGameOverPanelView - shared)
    public const float POPUP_SHOW_DELAY = 0.5f;
    public const float POPUP_SHOW_BG_DURATION = 0.25f;
    public const float POPUP_SHOW_DURATION = 0.38f;
    public const float POPUP_HIDE_DURATION = 0.20f;
    public const float POPUP_HIDE_BG_DURATION = 0.15f;
    public const float GAMEOVER_POPUP_SCALE_FROM = 1.15f;
    public const float VICTORY_POPUP_SCALE_FROM = 0.75f;
    public const float STAR_DELAY_AFTER_POPUP = 0.18f;
    public const float STAR_STAGGER = 0.20f;
    public const float STAR_POP_DURATION = 0.32f;
    public const float STAR_PUNCH_STRENGTH = 0.30f;
    public const float STAR_PUNCH_DURATION = 0.25f;
    // Gameplay HUD (TDGameplayHUDView)
    public const float HUD_PUNCH_GOLD = 0.25f;
    public const float HUD_PUNCH_LIFE = 0.35f;
    public const float HUD_PUNCH_ENEMY = 0.18f;
    public const float HUD_PUNCH_SPEED_BTN = 0.22f;
    public const float HUD_PUNCH_DURATION = 0.30f;
    public const int HUD_PUNCH_VIBRATO = 8;
    public const float HUD_PUNCH_ELASTICITY = 0.5f;
    public const float HUD_PAUSE_FADE_IN = 0.20f;
    public const float HUD_PAUSE_FADE_OUT = 0.15f;
    public const float HUD_SCENE_FADE_IN = 0.40f;
    public const float HUD_SCENE_FADE_OUT = 0.30f;
    // HP bar (TDHPBarView)
    public const float HPBAR_CANVAS_PIXEL_WIDTH = 200f;
    public const float HPBAR_DESIRED_WORLD_WIDTH = 1.6f;
    // Enemy animator triggers (TDEnemyView)
    public const string ANIM_TRIGGER_WALK = "Walk";
    public const string ANIM_TRIGGER_ATTACK = "Attack";
    public const string ANIM_TRIGGER_GET_HIT = "GetHit";
    public const string ANIM_TRIGGER_DIE = "Die";
    // Attack VFX (TDAttackVFX)
    public const float VFX_MOVE_SPEED = 15f;
    public const float VFX_ARRIVE_THRESHOLD = 0.25f;
    // Tower/operator views
    public const string SELECTION_INDICATOR_NAME = "SelectionIndicator";
    public const string PATH_SLOT_DISABLED_OVERLAY = "DisabledOverlay";
    public const string PATH_SLOT_ICON = "SlotIcon";
    public const float DIRECTION_THRESHOLD_RATIO = 0.35f;
    public const float TOUCH_SCREEN_Y_OFFSET_RATIO = 0.10f;
    public const float MIN_PHASE2_DURATION = 0.20f;
    public const float MIN_CELL_HOLD_DURATION = 0.35f;
    public const float TOWER_SCAN_INTERVAL = 0.2f;
    // Stage select (TDStageCardView)
    public const float STAGE_CARD_LOCKED_ALPHA = 0.25f;
    #endregion
}

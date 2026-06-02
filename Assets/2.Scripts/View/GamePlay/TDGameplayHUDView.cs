using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Quản lý toàn bộ HUD gameplay (trừ TowerMainView).
/// Gắn vào: Canvas/SafeArea/Container
/// Cache tất cả item qua transform.Find() — không dùng [SerializeField].
/// </summary>
public class TDGameplayHUDView : MonoBehaviour
{
    // ── Cached references ─────────────────────────────────────────────────────
    private TextMeshProUGUI m_EnemyCountText;
    private TextMeshProUGUI m_LifeText;
    private TextMeshProUGUI m_CurrencyText;
    private TextMeshProUGUI m_SpeedText;

    private Button     m_BackButton;
    private Button     m_SettingButton;
    private Button     m_SpeedButton;
    private Button     m_PauseButton;
    private Button     m_ResumeButton;
    private GameObject m_PausePanel;

    private GameObject m_GameOverPanel;
    private Button     m_GameOverRetryBtn;
    private TextMeshProUGUI m_GameOverStatEnemies;
    private TextMeshProUGUI m_GameOverStatLives;
    private TextMeshProUGUI m_GameOverStatGold;
    private TextMeshProUGUI m_GameOverStageName;

    private GameObject m_VictoryPanel;
    private Button     m_VictoryRetryBtn;
    private Button     m_VictoryNextBtn;
    private TextMeshProUGUI m_VictoryStatEnemies;
    private TextMeshProUGUI m_VictoryStatLives;
    private TextMeshProUGUI m_VictoryStatGold;
    private TextMeshProUGUI m_VictoryStageName;

    private GameObject m_SpeedIconNormal;
    private GameObject m_SpeedIconX2;

    [SerializeField] private TDStageRepository m_StageRepository;

    private Image m_ScreenFlash;

    private Color m_LifeColorNormal;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        SubscribeEvents(); // subscribe trước để nhận event từ InitControls
        InitControls();    // Initialize fire onSpeedChanged/onGoldChanged → UI sync ngay
        SetupButtons();
    }

    private void Update()
    {
        TDGoldControl.api?.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // ── Cache ──────────────────────────────────────────────────────────────────
    private void CacheReferences()
    {
        m_EnemyCountText  = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_ENEMY_COUNT) ?.GetComponent<TextMeshProUGUI>();
        m_LifeText        = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_LIFE_POINT)  ?.GetComponent<TextMeshProUGUI>();
        m_CurrencyText    = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_CURRENCY)    ?.GetComponent<TextMeshProUGUI>();
        m_SpeedText       = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_VALUE) ?.GetComponent<TextMeshProUGUI>();

        m_BackButton      = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_BACK_BUTTON)    ?.GetComponent<Button>();
        m_SettingButton   = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SETTING_BUTTON) ?.GetComponent<Button>();
        m_SpeedButton     = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_BUTTON)   ?.GetComponent<Button>();
        m_PauseButton     = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_PAUSE_BUTTON)   ?.GetComponent<Button>();
        m_ResumeButton    = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_RESUME_BUTTON)  ?.GetComponent<Button>();
        m_PausePanel      = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_PAUSE_PANEL)    ?.gameObject;
        m_PausePanel?.SetActive(false);

        m_SpeedIconNormal = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_ICON_NORMAL)?.gameObject;
        m_SpeedIconX2     = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_ICON_X2)   ?.gameObject;

        m_ScreenFlash = transform.Find(TDConstant.PATH_GAMEPLAY_SCREEN_FLASH)?.GetComponent<Image>();

        m_GameOverPanel       = transform.Find(TDConstant.PATH_GAMEPLAY_GAMEOVER_PANEL)        ?.gameObject;
        m_GameOverRetryBtn    = transform.Find(TDConstant.PATH_GAMEPLAY_GAMEOVER_RETRY_BTN)    ?.GetComponent<Button>();
        m_GameOverStatEnemies = transform.Find(TDConstant.PATH_GAMEOVER_STAT_ENEMIES)          ?.GetComponent<TextMeshProUGUI>();
        m_GameOverStatLives   = transform.Find(TDConstant.PATH_GAMEOVER_STAT_LIVES)            ?.GetComponent<TextMeshProUGUI>();
        m_GameOverStatGold    = transform.Find(TDConstant.PATH_GAMEOVER_STAT_GOLD)             ?.GetComponent<TextMeshProUGUI>();
        m_GameOverStageName   = transform.Find(TDConstant.PATH_GAMEOVER_STAGE_NAME)            ?.GetComponent<TextMeshProUGUI>();
        m_GameOverPanel?.SetActive(false);

        m_VictoryPanel        = transform.Find(TDConstant.PATH_GAMEPLAY_VICTORY_PANEL)         ?.gameObject;
        m_VictoryRetryBtn     = transform.Find(TDConstant.PATH_GAMEPLAY_VICTORY_RETRY_BTN)     ?.GetComponent<Button>();
        m_VictoryNextBtn      = transform.Find(TDConstant.PATH_GAMEPLAY_VICTORY_NEXT_BTN)      ?.GetComponent<Button>();
        m_VictoryStatEnemies  = transform.Find(TDConstant.PATH_VICTORY_STAT_ENEMIES)           ?.GetComponent<TextMeshProUGUI>();
        m_VictoryStatLives    = transform.Find(TDConstant.PATH_VICTORY_STAT_LIVES)             ?.GetComponent<TextMeshProUGUI>();
        m_VictoryStatGold     = transform.Find(TDConstant.PATH_VICTORY_STAT_GOLD)              ?.GetComponent<TextMeshProUGUI>();
        m_VictoryStageName    = transform.Find(TDConstant.PATH_VICTORY_STAGE_NAME)             ?.GetComponent<TextMeshProUGUI>();
        m_VictoryPanel?.SetActive(false);

        if (m_LifeText != null)
            m_LifeColorNormal = m_LifeText.color;
    }

    // ── Init controls ──────────────────────────────────────────────────────────
    private void InitControls()
    {
        TDGoldControl.api.Initialize(TDConstant.CONFIG_PLAYER_STARTING_GOLD);
        TDSpeedControl.api.Initialize();
    }

    // ── Subscribe / Unsubscribe ───────────────────────────────────────────────
    private void SubscribeEvents()
    {
        TDPlayerLifeControl.api.onLifeChanged       += OnLifeChanged;
        TDPlayerLifeControl.api.onGameOver           += OnGameOver;
        TDGoldControl.api.onGoldChanged              += OnGoldChanged;
        TDGameStateControl.api.onEnemyCountChanged   += OnEnemyCountChanged;
        TDGameStateControl.api.onVictory             += OnVictory;
        TDPauseControl.api.onPauseChanged            += OnPauseChanged;
        TDSpeedControl.api.onSpeedChanged            += OnSpeedChanged;
    }

    private void UnsubscribeEvents()
    {
        if (TDPlayerLifeControl.api != null)
        {
            TDPlayerLifeControl.api.onLifeChanged     -= OnLifeChanged;
            TDPlayerLifeControl.api.onGameOver         -= OnGameOver;
        }
        if (TDGoldControl.api    != null) TDGoldControl.api.onGoldChanged            -= OnGoldChanged;
        if (TDGameStateControl.api != null)
        {
            TDGameStateControl.api.onEnemyCountChanged -= OnEnemyCountChanged;
            TDGameStateControl.api.onVictory           -= OnVictory;
        }
        if (TDPauseControl.api   != null) TDPauseControl.api.onPauseChanged          -= OnPauseChanged;
        if (TDSpeedControl.api   != null) TDSpeedControl.api.onSpeedChanged          -= OnSpeedChanged;
    }

    // ── Button wiring ─────────────────────────────────────────────────────────
    private void SetupButtons()
    {
        m_BackButton      ?.onClick.AddListener(OnBackClicked);
        m_SettingButton   ?.onClick.AddListener(OnSettingClicked);
        m_SpeedButton     ?.onClick.AddListener(OnSpeedClicked);
        m_PauseButton     ?.onClick.AddListener(OnPauseClicked);
        m_ResumeButton    ?.onClick.AddListener(OnResumeClicked);
        m_GameOverRetryBtn?.onClick.AddListener(OnBackClicked);
        m_VictoryRetryBtn ?.onClick.AddListener(OnBackClicked);
        m_VictoryNextBtn  ?.onClick.AddListener(OnNextClicked);
    }

    // ── Event handlers ────────────────────────────────────────────────────────
    private void OnLifeChanged(int lives)
    {
        if (m_LifeText != null)
        {
            m_LifeText.text  = lives.ToString();
            m_LifeText.color = lives <= TDConstant.CONFIG_LIFE_LOW_THRESHOLD
                ? Color.red
                : m_LifeColorNormal;
        }
        StartCoroutine(FlashScreen(lives));
    }

    private void OnGameOver()
    {
        TDGameStateControl.api?.OnGameOver();
        TDPauseControl.api?.Pause();

        int killed = TDGameStateControl.api?.KilledEnemies ?? 0;
        int total  = TDGameStateControl.api?.TotalEnemies  ?? 0;
        int lives  = TDPlayerLifeControl.api?.CurrentLives ?? 0;
        int livesLost = TDConstant.CONFIG_PLAYER_STARTING_LIVES - lives;
        int gold   = TDGoldControl.api?.Gold ?? 0;

        if (m_GameOverStatEnemies != null) m_GameOverStatEnemies.text = $"{killed} / {total}";
        if (m_GameOverStatLives   != null) m_GameOverStatLives.text   = livesLost.ToString();
        if (m_GameOverStatGold    != null) m_GameOverStatGold.text    = gold.ToString();
        if (m_GameOverStageName   != null) m_GameOverStageName.text   = $"Stage {TDGameStateControl.api?.SelectedStageId}";

        m_GameOverPanel?.SetActive(true);
    }

    private void OnGoldChanged(int gold)
    {
        if (m_CurrencyText != null)
            m_CurrencyText.text = gold.ToString();
    }

    private void OnEnemyCountChanged(int killed, int total)
    {
        if (m_EnemyCountText != null)
            m_EnemyCountText.text = $"{killed}/{total}";
    }

    private void OnVictory()
    {
        TDPauseControl.api?.Pause();

        int killed = TDGameStateControl.api?.KilledEnemies ?? 0;
        int total  = TDGameStateControl.api?.TotalEnemies  ?? 0;
        int lives  = TDPlayerLifeControl.api?.CurrentLives ?? 0;
        int gold   = TDGoldControl.api?.Gold ?? 0;

        if (m_VictoryStatEnemies != null) m_VictoryStatEnemies.text = $"{killed} / {total}";
        if (m_VictoryStatLives   != null) m_VictoryStatLives.text   = $"{lives} / {TDConstant.CONFIG_PLAYER_STARTING_LIVES}";
        if (m_VictoryStatGold    != null) m_VictoryStatGold.text    = gold.ToString();
        if (m_VictoryStageName   != null) m_VictoryStageName.text   = $"Stage {TDGameStateControl.api?.SelectedStageId}";

        m_VictoryPanel?.SetActive(true);
    }

    private void OnPauseChanged(bool isPaused)
    {
        m_PausePanel?.SetActive(isPaused);
    }

    private void OnSpeedChanged(float multiplier)
    {
        bool isFast = multiplier > 1f;

        if (m_SpeedIconNormal != null) m_SpeedIconNormal.SetActive(!isFast);
        if (m_SpeedIconX2     != null) m_SpeedIconX2    .SetActive( isFast);
        if (m_SpeedText       != null) m_SpeedText.text  = isFast ? "x2" : "x1";
    }

    // ── Button callbacks ──────────────────────────────────────────────────────
    private void OnBackClicked()
    {
        SceneManager.LoadScene(TDConstant.SCENE_LOAD_FIRST);
    }

    private void OnNextClicked()
    {
        string        currentId = TDGameStateControl.api.SelectedStageId;
        TDStageConfig next      = m_StageRepository?.GetNextStage(currentId);

        if (next != null)
            TDGameStateControl.api.SelectStage(next.StageId);

        SceneManager.LoadScene(TDConstant.SCENE_LOAD_FIRST);
    }

    private void OnSettingClicked()
    {
        // TODO Phase 9: mở Settings panel
        Debug.Log("[HUD] Setting clicked");
    }

    private void OnSpeedClicked()
    {
        TDSpeedControl.api.ToggleSpeed();
    }

    private void OnPauseClicked()
    {
        TDPauseControl.api.Toggle();
    }

    private void OnResumeClicked()
    {
        TDPauseControl.api.Resume();
    }

    // ── Screen flash ──────────────────────────────────────────────────────────
    private IEnumerator FlashScreen(int lives)
    {
        if (m_ScreenFlash == null || lives == TDConstant.CONFIG_PLAYER_STARTING_LIVES) 
            yield break;
        m_ScreenFlash.color = new Color(1f, 0f, 0f, 0.35f);
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed            += Time.unscaledDeltaTime; // unscaled: flash hiện cả khi pause
            float alpha         = Mathf.Lerp(0.35f, 0f, elapsed / 0.35f);
            m_ScreenFlash.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }
        m_ScreenFlash.color = Color.clear;
    }
}

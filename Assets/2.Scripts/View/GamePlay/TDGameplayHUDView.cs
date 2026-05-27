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

    private Button m_BackButton;
    private Button m_SettingButton;
    private Button m_SpeedButton;
    private Button m_PauseButton;

    private GameObject m_SpeedIconNormal;
    private GameObject m_SpeedIconX2;

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

        m_SpeedIconNormal = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_ICON_NORMAL)?.gameObject;
        m_SpeedIconX2     = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_ICON_X2)   ?.gameObject;

        // FlashScreen là con của SafeArea (cha của Container) → đi lên 1 cấp
        m_ScreenFlash = transform.Find(TDConstant.PATH_GAMEPLAY_SCREEN_FLASH)?.GetComponent<Image>();

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
        m_BackButton   ?.onClick.AddListener(OnBackClicked);
        m_SettingButton?.onClick.AddListener(OnSettingClicked);
        m_SpeedButton  ?.onClick.AddListener(OnSpeedClicked);
        m_PauseButton  ?.onClick.AddListener(OnPauseClicked);
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
        StartCoroutine(FlashScreen());
    }

    private void OnGameOver()
    {
        Debug.Log("<color=red>GAME OVER</color>");
        // TODO Phase 8: show GameOverPanel
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
        Debug.Log("<color=yellow>VICTORY</color>");
        // TODO Phase 8: show VictoryPanel
    }

    private void OnPauseChanged(bool isPaused)
    {
        // TODO: show/hide PausePanel khi có
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
        // Quit gameplay → về main menu / load first scene
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

    // ── Screen flash ──────────────────────────────────────────────────────────
    private IEnumerator FlashScreen()
    {
        if (m_ScreenFlash == null) yield break;
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

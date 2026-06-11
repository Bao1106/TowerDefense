using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the entire gameplay HUD (excluding TowerMainView).
/// Attached to: Canvas/SafeArea/Container
/// Caches all references via transform.Find() — no [SerializeField] needed.
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
    private CanvasGroup m_PausePanelCG;

    private TDGameOverPanelView m_GameOverPanel;
    private TDVictoryPanelView  m_VictoryPanel;

    private bool m_IsGameEnding;

    private GameObject m_SpeedIconNormal;
    private GameObject m_SpeedIconX2;

    [SerializeField] private TDStageRepository m_StageRepository;

    private Image m_ScreenFlash;
    private Image m_TransitionOverlay;

    private Color m_LifeColorNormal;

    // ── Tween tuning ──────────────────────────────────────────────────────────
    private const float PUNCH_GOLD       = 0.25f;
    private const float PUNCH_LIFE       = 0.35f;
    private const float PUNCH_ENEMY      = 0.18f;
    private const float PUNCH_SPEED_BTN  = 0.22f;
    private const float PUNCH_DURATION   = 0.30f;
    private const int   PUNCH_VIBRATO    = 8;
    private const float PUNCH_ELASTICITY = 0.5f;

    private const float PAUSE_FADE_IN    = 0.20f;
    private const float PAUSE_FADE_OUT   = 0.15f;
    private const float SCENE_FADE_IN    = 0.40f;
    private const float SCENE_FADE_OUT   = 0.30f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        CacheReferences();
    }

    private void Start()
    {
        SubscribeEvents(); // subscribe first so events from InitControls are received
        InitControls();    // fires onSpeedChanged/onGoldChanged → immediately syncs the UI
        SetupButtons();
        PlaySceneFadeIn();
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

        var pauseT = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_PAUSE_PANEL);
        if (pauseT != null)
        {
            m_PausePanel   = pauseT.gameObject;
            m_PausePanelCG = m_PausePanel.GetComponent<CanvasGroup>()
                          ?? m_PausePanel.AddComponent<CanvasGroup>();
            m_PausePanel.SetActive(false);
        }

        m_SpeedIconNormal = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_ICON_NORMAL)?.gameObject;
        m_SpeedIconX2     = transform.Find(TDConstant.PATH_GAMEPLAY_HUD_SPEED_ICON_X2)   ?.gameObject;

        m_ScreenFlash       = transform.Find(TDConstant.PATH_GAMEPLAY_SCREEN_FLASH)      ?.GetComponent<Image>();
        m_TransitionOverlay = transform.Find(TDConstant.PATH_GAMEPLAY_TRANSITION_OVERLAY) ?.GetComponent<Image>();

        m_GameOverPanel = transform.Find(TDConstant.PATH_GAMEPLAY_GAMEOVER_PANEL)?.GetComponent<TDGameOverPanelView>();
        m_VictoryPanel  = transform.Find(TDConstant.PATH_GAMEPLAY_VICTORY_PANEL) ?.GetComponent<TDVictoryPanelView>();
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
            TDPlayerLifeControl.api.onLifeChanged -= OnLifeChanged;
            TDPlayerLifeControl.api.onGameOver     -= OnGameOver;
        }
        if (TDGoldControl.api      != null) TDGoldControl.api.onGoldChanged            -= OnGoldChanged;
        if (TDGameStateControl.api != null)
        {
            TDGameStateControl.api.onEnemyCountChanged -= OnEnemyCountChanged;
            TDGameStateControl.api.onVictory           -= OnVictory;
        }
        if (TDPauseControl.api != null) TDPauseControl.api.onPauseChanged -= OnPauseChanged;
        if (TDSpeedControl.api != null) TDSpeedControl.api.onSpeedChanged  -= OnSpeedChanged;
    }

    // ── Button wiring ─────────────────────────────────────────────────────────
    private void SetupButtons()
    {
        m_BackButton   ?.onClick.AddListener(OnBackClicked);
        m_SettingButton?.onClick.AddListener(OnSettingClicked);
        m_SpeedButton  ?.onClick.AddListener(OnSpeedClicked);
        m_PauseButton  ?.onClick.AddListener(OnPauseClicked);
        m_ResumeButton ?.onClick.AddListener(OnResumeClicked);
        m_GameOverPanel?.BindButtons(onRetry: OnRetryClicked);
        m_VictoryPanel ?.BindButtons(onRetry: OnRetryClicked, onNext: OnNextClicked);
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

            DOTween.Kill(m_LifeText.transform);
            m_LifeText.transform
                .DOPunchScale(Vector3.one * PUNCH_LIFE, PUNCH_DURATION, PUNCH_VIBRATO, PUNCH_ELASTICITY)
                .SetTarget(m_LifeText.transform).SetUpdate(true);
        }
        StartCoroutine(FlashScreen(lives));
    }

    private void OnGameOver()
    {
        m_IsGameEnding = true;
        TDGameStateControl.api?.OnGameOver();
        TDPauseControl.api?.Pause();

        int killed    = TDGameStateControl.api?.KilledEnemies ?? 0;
        int total     = TDGameStateControl.api?.TotalEnemies  ?? 0;
        int lives     = TDPlayerLifeControl.api?.CurrentLives ?? 0;
        int gold      = TDGoldControl.api?.Gold ?? 0;
        int curWave   = TDGameStateControl.api?.CurrentWave ?? 0;
        int totWaves  = TDGameStateControl.api?.TotalWaves  ?? 0;
        string stageId = TDGameStateControl.api?.SelectedStageId ?? "";

        m_GameOverPanel?.Show(stageId, killed, total, lives, gold, curWave, totWaves);
    }

    private void OnGoldChanged(int gold)
    {
        if (m_CurrencyText == null) return;
        m_CurrencyText.text = gold.ToString();
        DOTween.Kill(m_CurrencyText.transform);
        m_CurrencyText.transform
            .DOPunchScale(Vector3.one * PUNCH_GOLD, PUNCH_DURATION, PUNCH_VIBRATO, PUNCH_ELASTICITY)
            .SetTarget(m_CurrencyText.transform).SetUpdate(true);
    }

    private void OnEnemyCountChanged(int killed, int total)
    {
        if (m_EnemyCountText == null) return;
        m_EnemyCountText.text = $"{killed}/{total}";
        DOTween.Kill(m_EnemyCountText.transform);
        m_EnemyCountText.transform
            .DOPunchScale(Vector3.one * PUNCH_ENEMY, PUNCH_DURATION, 5, PUNCH_ELASTICITY)
            .SetTarget(m_EnemyCountText.transform).SetUpdate(true);
    }

    private void OnVictory()
    {
        m_IsGameEnding = true;
        TDPauseControl.api?.Pause();

        int killed  = TDGameStateControl.api?.KilledEnemies ?? 0;
        int total   = TDGameStateControl.api?.TotalEnemies  ?? 0;
        int lives   = TDPlayerLifeControl.api?.CurrentLives ?? 0;
        int gold    = TDGoldControl.api?.Gold ?? 0;
        int stars   = CalculateStars(lives);
        string stageId = TDGameStateControl.api?.SelectedStageId ?? "";

        m_VictoryPanel?.Show(stageId, killed, total, lives, gold, stars);
    }

    // 3 ⭐ lives ≥ 30  (lost ≤ 10)
    // 2 ⭐ lives ≥ 15  (lost ≤ 25)
    // 1 ⭐ lives > 0   (won but barely)
    private static int CalculateStars(int lives)
    {
        if (lives >= 30) return 3;
        if (lives >= 15) return 2;
        return 1;
    }

    private void OnPauseChanged(bool isPaused)
    {
        if (m_IsGameEnding) return;
        if (isPaused) ShowPausePanel();
        else          HidePausePanel();
    }

    private void OnSpeedChanged(float multiplier)
    {
        bool isFast = multiplier > 1f;

        if (m_SpeedIconNormal != null) m_SpeedIconNormal.SetActive(!isFast);
        if (m_SpeedIconX2     != null) m_SpeedIconX2    .SetActive( isFast);
        if (m_SpeedText       != null) m_SpeedText.text  = isFast ? "x2" : "x1";

        if (m_SpeedButton != null)
        {
            DOTween.Kill(m_SpeedButton.transform);
            m_SpeedButton.transform
                .DOPunchScale(Vector3.one * PUNCH_SPEED_BTN, PUNCH_DURATION, 6, PUNCH_ELASTICITY)
                .SetTarget(m_SpeedButton.transform).SetUpdate(true);
        }
    }

    // ── Button callbacks ──────────────────────────────────────────────────────
    private void OnBackClicked()
    {
        PlaySceneFadeOut(TDSceneController.api.GoToMainMenu);
    }

    private void OnRetryClicked()
    {
        PlaySceneFadeOut(TDSceneController.api.RetryGameplay);
    }

    private void OnNextClicked()
    {
        string        currentId = TDGameStateControl.api.SelectedStageId;
        TDStageConfig next      = m_StageRepository?.GetNextStage(currentId);
        if (next != null) TDGameStateControl.api.SelectStage(next.StageId);
        PlaySceneFadeOut(TDSceneController.api.RetryGameplay);
    }

    private void OnSettingClicked()
    {
        // TODO Phase 9: open Settings panel
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

    // ── Pause panel ───────────────────────────────────────────────────────────
    private void ShowPausePanel()
    {
        if (m_PausePanel == null) return;
        DOTween.Kill(m_PausePanel);
        m_PausePanel.SetActive(true);
        if (m_PausePanelCG != null)
        {
            m_PausePanelCG.alpha          = 0f;
            m_PausePanelCG.interactable   = false;
            m_PausePanelCG.blocksRaycasts = false;
            m_PausePanelCG.DOFade(1f, PAUSE_FADE_IN).SetEase(Ease.OutCubic)
                .SetTarget(m_PausePanel).SetUpdate(true)
                .OnComplete(() =>
                {
                    m_PausePanelCG.interactable   = true;
                    m_PausePanelCG.blocksRaycasts = true;
                });
        }
    }

    private void HidePausePanel()
    {
        if (m_PausePanel == null) return;
        DOTween.Kill(m_PausePanel);
        if (m_PausePanelCG != null)
        {
            m_PausePanelCG.interactable   = false;
            m_PausePanelCG.blocksRaycasts = false;
            m_PausePanelCG.DOFade(0f, PAUSE_FADE_OUT).SetEase(Ease.InCubic)
                .SetTarget(m_PausePanel).SetUpdate(true)
                .OnComplete(() => m_PausePanel.SetActive(false));
        }
        else
        {
            m_PausePanel.SetActive(false);
        }
    }

    // ── Scene transitions ─────────────────────────────────────────────────────
    private void PlaySceneFadeIn()
    {
        if (m_TransitionOverlay == null) return;
        DOTween.Kill(m_TransitionOverlay);
        m_TransitionOverlay.gameObject.SetActive(true);
        m_TransitionOverlay.color = Color.black;
        m_TransitionOverlay.DOFade(0f, SCENE_FADE_IN).SetEase(Ease.OutCubic).SetUpdate(true)
            .SetTarget(m_TransitionOverlay)
            .OnComplete(() => m_TransitionOverlay.gameObject.SetActive(false));
    }

    private void PlaySceneFadeOut(Action onComplete)
    {
        if (m_TransitionOverlay == null) { onComplete?.Invoke(); return; }
        DOTween.Kill(m_TransitionOverlay);
        m_TransitionOverlay.gameObject.SetActive(true);
        m_TransitionOverlay.color = Color.clear;
        m_TransitionOverlay.DOFade(1f, SCENE_FADE_OUT).SetEase(Ease.InCubic).SetUpdate(true)
            .SetTarget(m_TransitionOverlay)
            .OnComplete(() => onComplete?.Invoke());
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
            elapsed            += Time.unscaledDeltaTime;
            float alpha         = Mathf.Lerp(0.35f, 0f, elapsed / 0.35f);
            m_ScreenFlash.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }
        m_ScreenFlash.color = Color.clear;
    }
}

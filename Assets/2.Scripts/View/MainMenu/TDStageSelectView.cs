using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TDStageSelectView : MonoBehaviour
{
    // Scene hierarchy:
    //  StageSelect
    //    ├── Header / TxtTitle
    //    ├── Middle / LeftPanel
    //    │   ├── ThemeBackground (Image)
    //    │   ├── TxtStageNum
    //    │   ├── TxtStageTitle
    //    │   ├── TxtStageName
    //    │   └── TxtDescription
    //    ├── Middle / RightPanel / StageScroll / Viewport / Content  ← m_CardContainer
    //    └── Bottom / BtnBack, BtnPlay

    [Header("Left Panel")]
    [SerializeField] private TMP_Text m_TxtStageNum;
    [SerializeField] private TMP_Text m_TxtStageTitle;
    [SerializeField] private TMP_Text m_TxtStageName;
    [SerializeField] private TMP_Text m_TxtDescription;
    [SerializeField] private Image    m_ThemeBackground;

    [Header("Right Panel")]
    [SerializeField] private Transform m_CardContainer;   // Content inside StageScroll

    [Header("Bottom")]
    [SerializeField] private Button       m_BtnPlay;
    [SerializeField] private CanvasGroup  m_BtnPlayGroup;
    [SerializeField] private Button       m_BtnBack;

    [Header("Data")]
    [SerializeField] private TDStageRepository m_StageRepository;
    [SerializeField] private TDMainMenuView    m_MainMenuView;

    private CanvasGroup                 m_CanvasGroup;
    private readonly List<TDStageCardView> m_Cards = new();
    private TDStageConfig               m_SelectedStage;
    private Tween                       m_BgTween;
    private Tween                       m_BtnPlayTween;

    private void Awake()
    {
        m_CanvasGroup = GetComponent<CanvasGroup>();
        m_CanvasGroup.alpha          = 0f;
        m_CanvasGroup.interactable   = false;
        m_CanvasGroup.blocksRaycasts = false;

        m_BtnPlay.onClick.AddListener(OnPlayClicked);
        m_BtnBack.onClick.AddListener(OnBackClicked);
        SetBtnPlayState(enabled: false, animate: false);
    }

    public void Show()
    {
        m_MainMenuView?.Hide();

        BuildCards();
        if (m_Cards.Count > 0)
            SelectCard(m_Cards[0], animate: false);

        m_CanvasGroup.blocksRaycasts = true;
        m_CanvasGroup.interactable   = true;
        m_CanvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void Hide()
    {
        m_CanvasGroup.interactable   = false;
        m_CanvasGroup.blocksRaycasts = false;
        m_CanvasGroup.DOFade(0f, 0.2f).SetEase(Ease.OutCubic).SetUpdate(true);
        m_MainMenuView?.Show();
    }

    // ── Cards ─────────────────────────────────────────────────────────────────

    private void BuildCards()
    {
        foreach (var card in m_Cards)
            if (card != null) Destroy(card.gameObject);
        m_Cards.Clear();

        var stages = m_StageRepository.GetAll();
        if (stages == null) return;

        var prefabGo = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_STAGE_CARD);
        if (prefabGo == null)
        {
            Debug.LogError("[TDStageSelectView] PREFAB_STAGE_CARD not found in TDResourceObject");
            return;
        }
        var prefab = prefabGo.GetComponent<TDStageCardView>();
        if (prefab == null)
        {
            Debug.LogError("[TDStageSelectView] TDStageCardView component missing on StagePrefab");
            return;
        }

        foreach (var stage in stages)
        {
            var card = Instantiate(prefab, m_CardContainer);
            card.Setup(stage);
            card.OnSelected += OnCardSelected;
            m_Cards.Add(card);
        }
    }

    private void OnCardSelected(TDStageConfig stage)
    {
        var card = m_Cards.Find(c => c != null && c.Stage == stage);
        if (card != null) SelectCard(card);
    }

    private void SelectCard(TDStageCardView target, bool animate = true)
    {
        foreach (var c in m_Cards)
            c.SetSelected(c == target, animate);

        m_SelectedStage = target.Stage;
        if (m_SelectedStage == null) return;

        UpdateLeftPanel(m_SelectedStage, animate);
        SetBtnPlayState(!m_SelectedStage.isLocked, animate);
    }

    // ── Left Panel ────────────────────────────────────────────────────────────

    private void UpdateLeftPanel(TDStageConfig stage, bool animate)
    {
        m_TxtStageNum.text   = $"STAGE {(stage.LevelIndex + 1):D2}";
        m_TxtStageTitle.text = stage.DisplayName;
        m_TxtStageName.text  = stage.StageId;
        m_TxtDescription.text = stage.stageDescription;

        if (stage.previewSprite == null || m_ThemeBackground == null) return;

        if (!animate)
        {
            m_ThemeBackground.sprite = stage.previewSprite;
            return;
        }

        m_BgTween?.Kill();
        m_BgTween = m_ThemeBackground
            .DOFade(0f, 0.15f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                m_ThemeBackground.sprite = stage.previewSprite;
                m_ThemeBackground.DOFade(1f, 0.2f).SetUpdate(true);
            });
    }

    // ── BtnPlay state ─────────────────────────────────────────────────────────

    private void SetBtnPlayState(bool enabled, bool animate)
    {
        m_BtnPlay.interactable        = enabled;
        m_BtnPlayGroup.blocksRaycasts = enabled;
        m_BtnPlayTween?.Kill();

        float targetAlpha = enabled ? 1f : 0.35f;
        if (animate)
        {
            m_BtnPlayTween = m_BtnPlayGroup.DOFade(targetAlpha, 0.2f).SetUpdate(true);
            if (enabled)
                m_BtnPlay.transform
                    .DOPunchScale(Vector3.one * 0.12f, 0.3f, 5, 0.5f)
                    .SetUpdate(true);
        }
        else
        {
            m_BtnPlayGroup.alpha = targetAlpha;
        }
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    private void OnPlayClicked()
    {
        if (m_SelectedStage == null) return;
        TDGameStateControl.api.SelectStage(m_SelectedStage.StageId);
        TDSceneController.api.GoToGameplay();
    }

    private void OnBackClicked() => Hide();

    private void OnDestroy()
    {
        m_BgTween?.Kill();
        m_BtnPlayTween?.Kill();
        foreach (var c in m_Cards)
            if (c != null) c.OnSelected -= OnCardSelected;
    }
}

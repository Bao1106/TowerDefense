using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TDStageSelectView : MonoBehaviour
{
    // Scene hierarchy (paths in TDConstant.PATH_STAGESEL_*):
    // StageSelect
    // ├── Header / TxtTitle
    // ├── Middle / LeftPanel
    // │ ├── ThemeBackground (Image)
    // │ ├── TxtStageNum
    // │ ├── TxtStageTitle
    // │ ├── TxtStageName
    // │ └── TxtDescription
    // ├── Middle / RightPanel / StageScroll / Viewport / Content ← m_CardContainer
    // └── Bottom / BtnBack, BtnPlay

    private TMP_Text m_TxtStageNum;
    private TMP_Text m_TxtStageTitle;
    private TMP_Text m_TxtStageName;
    private TMP_Text m_TxtDescription;
    private Image m_ThemeBackground;
    private Transform m_CardContainer;
    private Button m_BtnPlay;
    private CanvasGroup m_BtnPlayGroup;
    private Button m_BtnBack;
    private TDMainMenuView m_MainMenuView;

    private CanvasGroup m_CanvasGroup;
    private readonly List<TDStageCardView> m_Cards = new();
    private TDStageConfig m_SelectedStage;
    private Tween m_BgTween;
    private Tween m_BtnPlayTween;

    private void Awake()
    {
        ResolveReferences();

        m_CanvasGroup = GetComponent<CanvasGroup>();
        m_CanvasGroup.alpha = 0f;
        m_CanvasGroup.interactable = false;
        m_CanvasGroup.blocksRaycasts = false;

        m_BtnPlay?.onClick.AddListener(OnPlayClicked);
        m_BtnBack?.onClick.AddListener(OnBackClicked);
        SetBtnPlayState(enabled: false, animate: false);
    }

    private void ResolveReferences()
    {
        m_TxtStageNum = transform.Find(TDConstant.PATH_STAGESEL_TXT_NUM)?.GetComponent<TMP_Text>();
        m_TxtStageTitle = transform.Find(TDConstant.PATH_STAGESEL_TXT_TITLE)?.GetComponent<TMP_Text>();
        m_TxtStageName = transform.Find(TDConstant.PATH_STAGESEL_TXT_NAME)?.GetComponent<TMP_Text>();
        m_TxtDescription = transform.Find(TDConstant.PATH_STAGESEL_TXT_DESC)?.GetComponent<TMP_Text>();
        m_ThemeBackground = transform.Find(TDConstant.PATH_STAGESEL_THEME_BG)?.GetComponent<Image>();
        m_CardContainer = transform.Find(TDConstant.PATH_STAGESEL_CARD_CONTAINER);

        var btnPlayTf = transform.Find(TDConstant.PATH_STAGESEL_BTN_PLAY);
        m_BtnPlay = btnPlayTf?.GetComponent<Button>();
        m_BtnPlayGroup = btnPlayTf?.GetComponent<CanvasGroup>();
        m_BtnBack = transform.Find(TDConstant.PATH_STAGESEL_BTN_BACK)?.GetComponent<Button>();

        // MainMenu is a sibling under the same Canvas
        m_MainMenuView = transform.parent?.Find(TDConstant.NAME_MAIN_MENU)?.GetComponent<TDMainMenuView>();

        if (m_BtnPlay == null || m_BtnBack == null || m_CardContainer == null)
            Debug.LogError("[TDStageSelectView] ResolveReferences failed — check TDConstant.PATH_STAGESEL_* against scene hierarchy");
    }

    public void Show()
    {
        m_MainMenuView?.Hide();

        BuildCards();
        if (m_Cards.Count > 0)
            SelectCard(m_Cards[0], animate: false);

        m_CanvasGroup.blocksRaycasts = true;
        m_CanvasGroup.interactable = true;
        m_CanvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void Hide()
    {
        m_CanvasGroup.interactable = false;
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

        var stages = TDStageRepository.api?.GetAll();
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
        m_TxtStageNum.text = $"STAGE {(stage.LevelIndex + 1):D2}";
        m_TxtStageTitle.text = stage.DisplayName;
        m_TxtStageName.text = stage.StageId;
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
        if (m_BtnPlay == null || m_BtnPlayGroup == null) return;
        m_BtnPlay.interactable = enabled;
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
        TDSceneController.api.GoToGameplay(m_SelectedStage.StageId);
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

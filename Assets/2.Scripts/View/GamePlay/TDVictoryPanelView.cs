using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gắn vào: Canvas/SafeArea/Container/VictoryPanel
/// Tự cache references qua transform.Find() — không dùng SerializeField.
/// Quản lý hiển thị và DOTween transition cho Victory popup.
/// </summary>
public class TDVictoryPanelView : MonoBehaviour
{
    private CanvasGroup      m_CanvasGroup;
    private Transform        m_PopupWindow;
    private Button           m_BtnRetry;
    private Button           m_BtnNext;
    private TextMeshProUGUI  m_StageName;
    private TextMeshProUGUI  m_StatEnemies;
    private TextMeshProUGUI  m_StatLives;
    private TextMeshProUGUI  m_StatGold;

    private const float SHOW_BG_DURATION     = 0.25f;
    private const float SHOW_POPUP_DURATION  = 0.38f;
    private const float HIDE_POPUP_DURATION  = 0.20f;
    private const float HIDE_BG_DURATION     = 0.15f;
    private const float SHOW_POPUP_SCALE_FROM = 0.75f;

    private void Awake()
    {
        m_PopupWindow = transform.Find("PopupWindow");

        // CanvasGroup gắn trên PopupWindow để animate — không phải root panel
        if (m_PopupWindow != null)
        {
            m_CanvasGroup = m_PopupWindow.GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
                m_CanvasGroup = m_PopupWindow.gameObject.AddComponent<CanvasGroup>();
        }

        m_BtnRetry    = transform.Find("PopupWindow/Bottom/BtnRetry") ?.GetComponent<Button>();
        m_BtnNext     = transform.Find("PopupWindow/Bottom/BtnNext")  ?.GetComponent<Button>();
        m_StageName   = transform.Find("PopupWindow/Header/Stage")    ?.GetComponent<TextMeshProUGUI>();
        m_StatEnemies = transform.Find("PopupWindow/Middle/Info/EnemiesDefeated/TxtValue")?.GetComponent<TextMeshProUGUI>();
        m_StatLives   = transform.Find("PopupWindow/Middle/Info/LivesRemaining/TxtValue") ?.GetComponent<TextMeshProUGUI>();
        m_StatGold    = transform.Find("PopupWindow/Middle/Info/GoldRemaining/TxtValue")  ?.GetComponent<TextMeshProUGUI>();

        gameObject.SetActive(false);
    }

    /// <summary>Gọi một lần từ TDGameplayHUDView.SetupButtons để wire button callbacks.</summary>
    public void BindButtons(Action onRetry, Action onNext)
    {
        m_BtnRetry?.onClick.RemoveAllListeners();
        m_BtnNext ?.onClick.RemoveAllListeners();
        m_BtnRetry?.onClick.AddListener(() => onRetry?.Invoke());
        m_BtnNext ?.onClick.AddListener(() => onNext?.Invoke());
    }

    public void Show(string stageId, int killed, int total, int lives, int gold)
    {
        FillStats(stageId, killed, total, lives, gold);
        PlayShowAnim();
    }

    public void Hide(Action onComplete = null)
    {
        PlayHideAnim(onComplete);
    }

    // ── Data fill ──────────────────────────────────────────────────────────────
    private void FillStats(string stageId, int killed, int total, int lives, int gold)
    {
        if (m_StageName   != null) m_StageName.text   = $"STAGE  {stageId}";
        if (m_StatEnemies != null) m_StatEnemies.text  = $"{killed} / {total}";
        if (m_StatLives   != null) m_StatLives.text    = $"{lives} / {TDConstant.CONFIG_PLAYER_STARTING_LIVES}";
        if (m_StatGold    != null) m_StatGold.text     = gold.ToString();
    }

    // ── Animations ─────────────────────────────────────────────────────────────
    private void PlayShowAnim()
    {
        DOTween.Kill(gameObject);

        // VictoryPanel root: SetActive ngay để backdrop hiện
        gameObject.SetActive(true);

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha          = 0f;
            m_CanvasGroup.interactable   = false;
            m_CanvasGroup.blocksRaycasts = false;
        }

        if (m_PopupWindow != null)
            m_PopupWindow.localScale = Vector3.one * SHOW_POPUP_SCALE_FROM;

        var seq = DOTween.Sequence().SetTarget(gameObject).SetUpdate(true);

        if (m_CanvasGroup != null)
            seq.Append(m_CanvasGroup.DOFade(1f, SHOW_BG_DURATION).SetEase(Ease.OutCubic));

        if (m_PopupWindow != null)
            seq.Join(m_PopupWindow.DOScale(Vector3.one, SHOW_POPUP_DURATION).SetEase(Ease.OutBack));

        seq.OnComplete(() =>
        {
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.interactable   = true;
                m_CanvasGroup.blocksRaycasts = true;
            }
        });
    }

    private void PlayHideAnim(Action onComplete)
    {
        DOTween.Kill(gameObject);

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.interactable   = false;
            m_CanvasGroup.blocksRaycasts = false;
        }

        var seq = DOTween.Sequence().SetTarget(gameObject).SetUpdate(true);

        if (m_PopupWindow != null)
            seq.Append(m_PopupWindow.DOScale(Vector3.one * 0.9f, HIDE_POPUP_DURATION).SetEase(Ease.InCubic));

        if (m_CanvasGroup != null)
            seq.Append(m_CanvasGroup.DOFade(0f, HIDE_BG_DURATION).SetEase(Ease.InCubic));

        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }
}

using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attached to: Canvas/SafeArea/Container/GameOverPanel
/// Self-caches references via transform.Find() — no SerializeField needed.
/// Manages the display and DOTween transitions for the GameOver popup.
/// </summary>
public class TDGameOverPanelView : MonoBehaviour
{
    private CanvasGroup m_CanvasGroup;
    private Transform m_PopupWindow;
    private Button m_BtnRetry;
    private TextMeshProUGUI m_StageName;
    private TextMeshProUGUI m_StatEnemies;
    private TextMeshProUGUI m_StatLives;
    private TextMeshProUGUI m_StatGold;
    private TextMeshProUGUI m_StatWave;

    // brief delay before the popup appears
    // GameOver: scales down instead of up for a "falling" feel

    private void Awake()
    {
        m_PopupWindow = transform.Find("PopupWindow");

        // CanvasGroup is attached to PopupWindow for animation — not the root panel
        if (m_PopupWindow != null)
        {
            m_CanvasGroup = m_PopupWindow.GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null)
                m_CanvasGroup = m_PopupWindow.gameObject.AddComponent<CanvasGroup>();
        }

        m_BtnRetry = transform.Find("PopupWindow/Bottom/BtnRetry")?.GetComponent<Button>();
        m_StageName = transform.Find("PopupWindow/Header/Stage") ?.GetComponent<TextMeshProUGUI>();
        m_StatEnemies = transform.Find("PopupWindow/Middle/Info/EnemiesDefeated/TxtValue")?.GetComponent<TextMeshProUGUI>();
        m_StatLives = transform.Find("PopupWindow/Middle/Info/LivesRemaining/TxtValue") ?.GetComponent<TextMeshProUGUI>();
        m_StatGold = transform.Find("PopupWindow/Middle/Info/GoldRemaining/TxtValue") ?.GetComponent<TextMeshProUGUI>();
        m_StatWave = transform.Find("PopupWindow/Middle/Info/WaveReached/TxtValue") ?.GetComponent<TextMeshProUGUI>();

        gameObject.SetActive(false);
    }

    /// <summary>Called once from TDGameplayHUDView.SetupButtons to wire button callbacks.</summary>
    public void BindButtons(Action onRetry)
    {
        m_BtnRetry?.onClick.RemoveAllListeners();
        m_BtnRetry?.onClick.AddListener(() => Hide(() => onRetry?.Invoke()));
    }

    public void Show(string stageId, int killed, int total, int lives, int gold, int curWave, int totWaves)
    {
        FillStats(stageId, killed, total, lives, gold, curWave, totWaves);
        PlayShowAnim();
    }

    public void Hide(Action onComplete = null)
    {
        PlayHideAnim(onComplete);
    }

    // ── Data fill ──────────────────────────────────────────────────────────────
    private void FillStats(string stageId, int killed, int total, int lives, int gold, int curWave, int totWaves)
    {
        if (m_StageName != null) m_StageName.text = $"STAGE {stageId}";
        if (m_StatEnemies != null) m_StatEnemies.text = $"{killed} / {total}";
        if (m_StatLives != null) m_StatLives.text = $"{lives} / {TDConstant.CONFIG_PLAYER_STARTING_LIVES}";
        if (m_StatGold != null) m_StatGold.text = gold.ToString();
        if (m_StatWave != null) m_StatWave.text = $"{curWave} / {totWaves}";
    }

    // ── Animations ─────────────────────────────────────────────────────────────
    private void PlayShowAnim()
    {
        DOTween.Kill(gameObject);

        // GameOverPanel root: activate immediately so the backdrop appears
        gameObject.SetActive(true);

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.alpha = 0f;
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;
        }

        if (m_PopupWindow != null)
            m_PopupWindow.localScale = Vector3.one * TDConstant.GAMEOVER_POPUP_SCALE_FROM;

        var seq = DOTween.Sequence().SetTarget(gameObject).SetUpdate(true);
        seq.AppendInterval(TDConstant.POPUP_SHOW_DELAY);

        if (m_CanvasGroup != null)
            seq.Append(m_CanvasGroup.DOFade(1f, TDConstant.POPUP_SHOW_BG_DURATION).SetEase(Ease.OutCubic));

        // Scale down and settle — gives a "falling into place" feeling appropriate for defeat
        if (m_PopupWindow != null)
            seq.Join(m_PopupWindow.DOScale(Vector3.one, TDConstant.POPUP_SHOW_DURATION).SetEase(Ease.OutElastic));

        seq.OnComplete(() =>
        {
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.interactable = true;
                m_CanvasGroup.blocksRaycasts = true;
            }
        });
    }

    private void PlayHideAnim(Action onComplete)
    {
        DOTween.Kill(gameObject);

        if (m_CanvasGroup != null)
        {
            m_CanvasGroup.interactable = false;
            m_CanvasGroup.blocksRaycasts = false;
        }

        var seq = DOTween.Sequence().SetTarget(gameObject).SetUpdate(true);

        if (m_PopupWindow != null)
            seq.Append(m_PopupWindow.DOScale(Vector3.one * 0.9f, TDConstant.POPUP_HIDE_DURATION).SetEase(Ease.InCubic));

        if (m_CanvasGroup != null)
            seq.Append(m_CanvasGroup.DOFade(0f, TDConstant.POPUP_HIDE_BG_DURATION).SetEase(Ease.InCubic));

        seq.OnComplete(() =>
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }
}

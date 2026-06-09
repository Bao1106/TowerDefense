using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Attached to: Canvas/SafeArea/Container/VictoryPanel
/// Self-caches references via transform.Find() — no SerializeField needed.
/// Manages the display and DOTween transitions for the Victory popup.
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

    // Stars — 3 Image components inside StarRow
    private readonly List<Image> m_Stars = new();
    private static readonly string[] STAR_PATHS =
    {
        "PopupWindow/Middle/StarRow/IconStar",
        "PopupWindow/Middle/StarRow/IconStar (1)",
        "PopupWindow/Middle/StarRow/IconStar (2)",
    };
    private static readonly Color COLOR_STAR_EARNED   = new(1.00f, 0.85f, 0.10f, 1f); // gold
    private static readonly Color COLOR_STAR_UNEARNED = new(0.25f, 0.25f, 0.25f, 0.5f); // grey dim

    // Star animation timing
    private const float STAR_DELAY_AFTER_POPUP = 0.18f; // wait for the popup to settle
    private const float STAR_STAGGER           = 0.20f; // delay between each star
    private const float STAR_POP_DURATION      = 0.32f; // scale 0→1 OutBack
    private const float STAR_PUNCH_STRENGTH    = 0.30f; // punch strength after landing
    private const float STAR_PUNCH_DURATION    = 0.25f;

    private const float SHOW_BG_DURATION     = 0.25f;
    private const float SHOW_POPUP_DURATION  = 0.38f;
    private const float HIDE_POPUP_DURATION  = 0.20f;
    private const float HIDE_BG_DURATION     = 0.15f;
    private const float SHOW_POPUP_SCALE_FROM = 0.75f;

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

        m_BtnRetry    = transform.Find("PopupWindow/Bottom/BtnRetry") ?.GetComponent<Button>();
        m_BtnNext     = transform.Find("PopupWindow/Bottom/BtnNext")  ?.GetComponent<Button>();
        m_StageName   = transform.Find("PopupWindow/Header/Stage")    ?.GetComponent<TextMeshProUGUI>();
        m_StatEnemies = transform.Find("PopupWindow/Middle/Info/EnemiesDefeated/TxtValue")?.GetComponent<TextMeshProUGUI>();
        m_StatLives   = transform.Find("PopupWindow/Middle/Info/LivesRemaining/TxtValue") ?.GetComponent<TextMeshProUGUI>();
        m_StatGold    = transform.Find("PopupWindow/Middle/Info/GoldRemaining/TxtValue")  ?.GetComponent<TextMeshProUGUI>();

        // Cache star Images
        m_Stars.Clear();
        foreach (var path in STAR_PATHS)
        {
            var img = transform.Find(path)?.GetComponent<Image>();
            if (img != null) m_Stars.Add(img);
            else Debug.LogWarning($"[VictoryPanel] Star not found: {path}");
        }

        gameObject.SetActive(false);
    }

    /// <summary>Called once from TDGameplayHUDView.SetupButtons to wire button callbacks.</summary>
    public void BindButtons(Action onRetry, Action onNext)
    {
        m_BtnRetry?.onClick.RemoveAllListeners();
        m_BtnNext ?.onClick.RemoveAllListeners();
        m_BtnRetry?.onClick.AddListener(() => Hide(() => onRetry?.Invoke()));
        m_BtnNext ?.onClick.AddListener(() => Hide(() => onNext?.Invoke()));
    }

    public void Show(string stageId, int killed, int total, int lives, int gold, int stars = 1)
    {
        FillStats(stageId, killed, total, lives, gold);
        PrepareStars(stars);
        PlayShowAnim(stars);
    }

    public void Hide(Action onComplete = null)
    {
        PlayHideAnim(onComplete);
    }

    // ── Data fill ──────────────────────────────────────────────────────────────
    private void FillStats(string stageId, int killed, int total, int lives, int gold)
    {
        if (m_StageName   != null) m_StageName.text  = $"STAGE  {stageId}";
        if (m_StatEnemies != null) m_StatEnemies.text = $"{killed} / {total}";
        if (m_StatLives   != null) m_StatLives.text   = $"{lives} / {TDConstant.CONFIG_PLAYER_STARTING_LIVES}";
        if (m_StatGold    != null) m_StatGold.text    = gold.ToString();
    }

    // Sets the initial state of stars before animation:
    // Earned → hidden (scale=0) to pop in later; Unearned → dimmed, scale=1 (no animation)
    private void PrepareStars(int earnedCount)
    {
        for (int i = 0; i < m_Stars.Count; i++)
        {
            bool earned = i < earnedCount;
            m_Stars[i].color = earned ? new Color(1f, 1f, 1f, 0f) : COLOR_STAR_UNEARNED;
            m_Stars[i].transform.localScale = earned ? Vector3.zero : Vector3.one;
            DOTween.Kill(m_Stars[i].transform);
            DOTween.Kill(m_Stars[i]);
        }
    }

    // ── Animations ─────────────────────────────────────────────────────────────
    private void PlayShowAnim(int earnedStars)
    {
        DOTween.Kill(gameObject);

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

        // Popup fade + scale in
        if (m_CanvasGroup != null)
            seq.Append(m_CanvasGroup.DOFade(1f, SHOW_BG_DURATION).SetEase(Ease.OutCubic));
        if (m_PopupWindow != null)
            seq.Join(m_PopupWindow.DOScale(Vector3.one, SHOW_POPUP_DURATION).SetEase(Ease.OutBack));

        // Enable interaction after the popup animation completes
        seq.AppendCallback(() =>
        {
            if (m_CanvasGroup != null)
            {
                m_CanvasGroup.interactable   = true;
                m_CanvasGroup.blocksRaycasts = true;
            }
        });

        // Delay then pop stars one by one
        seq.AppendInterval(STAR_DELAY_AFTER_POPUP);
        for (int i = 0; i < m_Stars.Count; i++)
        {
            if (i >= earnedStars) break; // only animate earned stars

            var star = m_Stars[i];
            seq.AppendCallback(() =>
            {
                // Scale 0 → 1 OutBack
                star.transform.DOScale(Vector3.one, STAR_POP_DURATION)
                    .SetEase(Ease.OutBack).SetUpdate(true);
                // Fade in to gold color
                star.DOColor(COLOR_STAR_EARNED, STAR_POP_DURATION * 0.6f)
                    .SetEase(Ease.OutCubic).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        // Small punch after landing
                        star.transform
                            .DOPunchScale(Vector3.one * STAR_PUNCH_STRENGTH, STAR_PUNCH_DURATION, 5, 0.5f)
                            .SetUpdate(true);
                    });
            });
            seq.AppendInterval(STAR_STAGGER);
        }
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

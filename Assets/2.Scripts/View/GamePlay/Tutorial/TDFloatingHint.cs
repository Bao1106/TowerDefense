using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable just-in-time hint shown at the top of the gameplay HUD.
/// Single-instance, lazy-instantiated into Canvas/SafeArea/Container.
///
/// Two usage modes:
///   • Show(text, duration) — always show
///   • ShowLimited(prefsKey, maxShows, text, duration) — show only until the
///     PlayerPrefs counter hits maxShows; increments on each successful show.
///
/// All tweens SetUpdate(true) so the hint still fades correctly while the game
/// is paused (e.g. during the diamond panel direction-select phase doesn't pause
/// — but the pattern is consistent with the rest of the project).
/// </summary>
public class TDFloatingHint : MonoBehaviour
{
    private static TDFloatingHint m_Instance;
    public static TDFloatingHint Instance
    {
        get
        {
            if (m_Instance == null) Create();
            return m_Instance;
        }
    }

    private CanvasGroup m_CanvasGroup;
    private RectTransform m_Rect;
    private TextMeshProUGUI m_Text;
    private Tween m_FadeTween;
    private Coroutine m_HideRoutine;

    // ── Build ───────────────────────────────────────────────────────────────────

    private static void Create()
    {
        var canvas = FindHudCanvas();
        if (canvas == null) { Debug.LogWarning("[TDFloatingHint] HUD Canvas not found"); return; }
        var parent = canvas.transform.Find("SafeArea/Container");
        if (parent == null) parent = canvas.transform;

        var go = new GameObject("FloatingHint", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        m_Instance = go.AddComponent<TDFloatingHint>();
        m_Instance.BuildVisuals();
        go.SetActive(false);
    }

    private static Canvas FindHudCanvas()
    {
        foreach (var c in FindObjectsOfType<Canvas>(true))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
                return c;
        return null;
    }

    private void BuildVisuals()
    {
        // Parent may have a LayoutGroup that would force this hint into the layout
        // flow (squashing it into a 1-char-wide vertical column). Opt out so our
        // anchor/pivot/sizeDelta below are respected.
        var le = gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        m_Rect = (RectTransform)transform;
        m_Rect.anchorMin = new Vector2(0.5f, 1f);
        m_Rect.anchorMax = new Vector2(0.5f, 1f);
        m_Rect.pivot = new Vector2(0.5f, 1f);
        m_Rect.sizeDelta = new Vector2(820f, 70f);
        m_Rect.anchoredPosition = new Vector2(0f, -130f); // below HUD bar

        m_CanvasGroup = GetComponent<CanvasGroup>();
        m_CanvasGroup.alpha = 0f;
        m_CanvasGroup.blocksRaycasts = false;
        m_CanvasGroup.interactable = false;

        // Dark rounded pill background
        var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.7f);
        bgImg.raycastTarget = false;

        // Text on top
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = new Vector2(-30f, 0f);
        m_Text = textGo.GetComponent<TextMeshProUGUI>();
        m_Text.fontSize = 32f;
        m_Text.alignment = TextAlignmentOptions.Center;
        m_Text.color = Color.white;
        m_Text.fontStyle = FontStyles.Bold;
        m_Text.raycastTarget = false;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Show(string text, float duration = 3f)
    {
        if (m_Text == null) return;
        m_Text.text = text;
        gameObject.SetActive(true);

        m_FadeTween?.Kill();
        if (m_HideRoutine != null) StopCoroutine(m_HideRoutine);

        m_CanvasGroup.alpha = 0f;
        m_FadeTween = m_CanvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
        m_HideRoutine = StartCoroutine(HideAfter(duration));
    }

    public void HideNow()
    {
        m_FadeTween?.Kill();
        if (m_HideRoutine != null) StopCoroutine(m_HideRoutine);
        gameObject.SetActive(false);
    }

    /// Show only if PlayerPrefs counter is below maxShows. Increments counter on each show.
    public static void ShowLimited(string prefsKey, int maxShows, string text, float duration = 3f)
    {
        int shown = PlayerPrefs.GetInt(prefsKey, 0);
        if (shown >= maxShows) return;
        PlayerPrefs.SetInt(prefsKey, shown + 1);
        Instance?.Show(text, duration);
    }

    /// Reset the per-key counter (useful for testing).
    public static void Reset(string prefsKey) => PlayerPrefs.DeleteKey(prefsKey);

    // ── Internals ──────────────────────────────────────────────────────────────

    private IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        m_FadeTween?.Kill();
        m_FadeTween = m_CanvasGroup
            .DOFade(0f, 0.3f).SetEase(Ease.InCubic).SetUpdate(true)
            .OnComplete(() => gameObject.SetActive(false));
    }

    private void OnDestroy()
    {
        if (m_Instance == this) m_Instance = null;
        m_FadeTween?.Kill();
    }
}

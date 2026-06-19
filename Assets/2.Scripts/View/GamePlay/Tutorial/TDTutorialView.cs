using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// First-launch onboarding orchestrator. Auto-added by TDSlotHolderMainView at Start
/// so the tutorial lives on the same GameObject as the slot bar (zero scene setup).
///
/// Behaviours implemented:
///   #1 — Animated hand demo (PictoIcon_Hand) when player is idle for 5s on first launch
///   #2 — Floating hint "Drag to choose facing direction" after the drop (Phase 2)
///   #4 — Floating hint "Drag onto a highlighted cell" when ghost spawned (Phase 1)
///   #5 — Floating hint "Tap a placed unit to retreat" after first unit placed
///
/// PlayerPrefs flags namespaced td_tutorial_v1_* so a future "v2" can re-trigger.
/// </summary>
public class TDTutorialView : MonoBehaviour
{
    private const string PREFS_TUTORIAL_DONE = "td_tutorial_v1_done";
    private const string PREFS_HINT_PHASE1   = "td_tutorial_v1_phase1";
    private const string PREFS_HINT_PHASE2   = "td_tutorial_v1_phase2";
    private const string PREFS_HINT_RETREAT  = "td_tutorial_v1_retreat";

    private const int HINT_PHASE_MAX_SHOW   = 3;
    private const int HINT_RETREAT_MAX_SHOW = 2;
    private const float IDLE_WAIT_FOR_HAND  = 5f;
    private const float HAND_LOOP_DURATION  = 3f;

    private bool m_TutorialDone;
    private float m_LastInteraction;
    private Coroutine m_IdleWatchRoutine;

    private RectTransform m_HandRect;
    private CanvasGroup m_HandCanvasGroup;
    private Sequence m_HandSequence;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        m_TutorialDone = PlayerPrefs.GetInt(PREFS_TUTORIAL_DONE, 0) == 1;

        TDGameEventBus.OnUnitPickup  += OnUnitPickup;
        TDGameEventBus.OnDeployDrop  += OnDeployDrop;
        TDGameEventBus.OnTowerPlaced += OnTowerPlaced;

        m_LastInteraction = Time.unscaledTime;
        if (!m_TutorialDone)
            m_IdleWatchRoutine = StartCoroutine(WatchIdleForHand());
    }

    private void OnDestroy()
    {
        TDGameEventBus.OnUnitPickup  -= OnUnitPickup;
        TDGameEventBus.OnDeployDrop  -= OnDeployDrop;
        TDGameEventBus.OnTowerPlaced -= OnTowerPlaced;
        m_HandSequence?.Kill();
    }

    private void Update()
    {
        // Any tap / mouse click resets the idle timer and dismisses the hand.
        bool tappedNow = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            tappedNow = true;
#else
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            tappedNow = true;
#endif

        if (tappedNow)
        {
            m_LastInteraction = Time.unscaledTime;
            HideHand();
        }
    }

    // ── Game event handlers ────────────────────────────────────────────────────

    private void OnUnitPickup()
    {
        HideHand(); // player engaged — hand no longer needed
        TDFloatingHint.ShowLimited(PREFS_HINT_PHASE1, HINT_PHASE_MAX_SHOW,
                                   "Drag the unit onto a highlighted cell", 3f);
    }

    private void OnDeployDrop()
    {
        TDFloatingHint.ShowLimited(PREFS_HINT_PHASE2, HINT_PHASE_MAX_SHOW,
                                   "Drag outward to choose facing direction", 3f);
    }

    private void OnTowerPlaced()
    {
        if (!m_TutorialDone)
        {
            m_TutorialDone = true;
            PlayerPrefs.SetInt(PREFS_TUTORIAL_DONE, 1);
        }
        TDFloatingHint.ShowLimited(PREFS_HINT_RETREAT, HINT_RETREAT_MAX_SHOW,
                                   "Tap a placed unit to retreat", 3f);
    }

    // ── Hand demo (#1) ─────────────────────────────────────────────────────────

    private IEnumerator WatchIdleForHand()
    {
        while (!m_TutorialDone)
        {
            if (Time.unscaledTime - m_LastInteraction >= IDLE_WAIT_FOR_HAND)
            {
                ShowHand();
                yield break;
            }
            yield return null;
        }
    }

    private void ShowHand()
    {
        if (m_HandRect == null) BuildHand();
        if (m_HandRect == null) return; // build failed

        m_HandRect.gameObject.SetActive(true);
        PlayHandLoop();
    }

    private void HideHand()
    {
        if (m_HandRect == null) return;
        m_HandSequence?.Kill();
        m_HandRect.gameObject.SetActive(false);
        if (m_IdleWatchRoutine != null) StopCoroutine(m_IdleWatchRoutine);
        m_IdleWatchRoutine = null;
    }

    private void BuildHand()
    {
        var canvas = FindHudCanvas();
        if (canvas == null) return;
        var parent = canvas.transform.Find("SafeArea/Container");
        if (parent == null) parent = canvas.transform;

        var sprite = LoadHandSprite();
        if (sprite == null) { Debug.LogWarning("[TDTutorialView] Hand sprite not loaded"); return; }

        var go = new GameObject("TutorialHand", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().ignoreLayout = true;

        m_HandRect = (RectTransform)go.transform;
        m_HandRect.anchorMin = new Vector2(0.5f, 0f);
        m_HandRect.anchorMax = new Vector2(0.5f, 0f);
        m_HandRect.pivot = new Vector2(0.5f, 0.5f);
        m_HandRect.sizeDelta = new Vector2(96f, 96f);
        m_HandRect.anchoredPosition = GetSlotBarScreenAnchor();

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0.85f);

        m_HandCanvasGroup = go.GetComponent<CanvasGroup>();
        m_HandCanvasGroup.alpha = 0f;
        m_HandCanvasGroup.blocksRaycasts = false;
        m_HandCanvasGroup.interactable = false;
    }

    // Loops a simple "tap and drag" gesture: hand pulses on the slot bar, then drifts
    // up toward the map and fades, then resets. Conveys "press the slot and drag into
    // the play area" without precise screen-coords or asset wiring.
    private void PlayHandLoop()
    {
        if (m_HandRect == null || m_HandCanvasGroup == null) return;
        m_HandSequence?.Kill();

        Vector2 startPos = GetSlotBarScreenAnchor();
        Vector2 endPos = startPos + new Vector2(0f, 320f); // drift up toward map

        m_HandRect.anchoredPosition = startPos;
        m_HandRect.localScale = Vector3.one;
        m_HandCanvasGroup.alpha = 0f;

        m_HandSequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1);
        m_HandSequence.Append(m_HandCanvasGroup.DOFade(0.85f, 0.35f).SetEase(Ease.OutCubic));
        // Pulse on the slot bar — "tap me"
        m_HandSequence.Append(m_HandRect.DOScale(0.85f, 0.30f).SetEase(Ease.OutQuad));
        m_HandSequence.Append(m_HandRect.DOScale(1.0f, 0.30f).SetEase(Ease.InQuad));
        m_HandSequence.AppendInterval(0.10f);
        // Drag upward toward the map
        m_HandSequence.Append(m_HandRect.DOAnchorPos(endPos, HAND_LOOP_DURATION * 0.5f).SetEase(Ease.InOutSine));
        m_HandSequence.Join(m_HandCanvasGroup.DOFade(0f, HAND_LOOP_DURATION * 0.5f).SetEase(Ease.InCubic));
        // Reset for next loop
        m_HandSequence.AppendCallback(() =>
        {
            m_HandRect.anchoredPosition = startPos;
            m_HandRect.localScale = Vector3.one;
        });
        m_HandSequence.AppendInterval(0.4f);
    }

    // Slot bar sits flush with the bottom of the screen; place the hand a bit above so
    // it doesn't overlap the buttons. Returns canvas-space anchoredPosition.
    private Vector2 GetSlotBarScreenAnchor() => new Vector2(0f, 200f);

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Canvas FindHudCanvas()
    {
        foreach (var c in FindObjectsOfType<Canvas>(true))
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.name == "Canvas")
                return c;
        return null;
    }

    private static Sprite LoadHandSprite()
        => TDResourceObject.GetResource<Sprite>(TDConstant.SPRITE_TUTORIAL_HAND);
}

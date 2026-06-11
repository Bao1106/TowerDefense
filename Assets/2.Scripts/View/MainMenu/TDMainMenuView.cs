using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class TDMainMenuView : MonoBehaviour
{
    [SerializeField] private Canvas        m_Canvas;
    [SerializeField] private CanvasGroup   m_BgGroup;
    [SerializeField] private RectTransform m_LeftPanel;
    [SerializeField] private RectTransform m_RightPanel;
    [SerializeField] private CanvasGroup   m_BottomGroup;

    private CanvasGroup m_CanvasGroup;

    private void Awake()
    {
        m_CanvasGroup = GetComponent<CanvasGroup>();
        if (m_Canvas != null && TDSceneController.api != null)
            m_Canvas.worldCamera = TDSceneController.api.MenuCamera;

        // Hide all panels before first frame to prevent flash
        if (m_BgGroup     != null) m_BgGroup.alpha     = 0f;
        if (m_BottomGroup != null) m_BottomGroup.alpha  = 0f;
        if (m_LeftPanel   != null) GetOrAddCG(m_LeftPanel.gameObject).alpha  = 0f;
        if (m_RightPanel  != null)
        {
            GetOrAddCG(m_RightPanel.gameObject).alpha = 0f;
            for (int i = 0; i < m_RightPanel.childCount; i++)
                GetOrAddCG(m_RightPanel.GetChild(i).gameObject).alpha = 0f;
        }
    }

    private void Start()
    {
        // Force layout to compute final positions before PlayIntro reads anchoredPosition
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        PlayIntro();
    }

    public void Show()
    {
        if (m_CanvasGroup == null) return;
        m_CanvasGroup.blocksRaycasts = true;
        m_CanvasGroup.interactable   = true;
        m_CanvasGroup.DOFade(1f, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    public void Hide()
    {
        if (m_CanvasGroup == null) return;
        m_CanvasGroup.interactable   = false;
        m_CanvasGroup.blocksRaycasts = false;
        m_CanvasGroup.DOFade(0f, 0.2f).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private void PlayIntro()
    {
        var seq = DOTween.Sequence();

        if (m_BgGroup != null)
            seq.Insert(0f, m_BgGroup.DOFade(1f, 0.35f));

        if (m_LeftPanel != null)
        {
            var leftCg  = GetOrAddCG(m_LeftPanel.gameObject);
            var origPos = m_LeftPanel.anchoredPosition;
            m_LeftPanel.anchoredPosition = origPos + new Vector2(-280f, 0f);
            seq.Insert(0.1f, m_LeftPanel.DOAnchorPos(origPos, 0.45f).SetEase(Ease.OutQuart));
            seq.Insert(0.1f, leftCg.DOFade(1f, 0.3f));
        }

        if (m_RightPanel != null)
        {
            var rightCg = GetOrAddCG(m_RightPanel.gameObject);
            var origPos = m_RightPanel.anchoredPosition;
            m_RightPanel.anchoredPosition = origPos + new Vector2(220f, 0f);
            seq.Insert(0.22f, m_RightPanel.DOAnchorPos(origPos, 0.45f).SetEase(Ease.OutQuart));
            seq.Insert(0.22f, rightCg.DOFade(1f, 0.2f));

            for (int i = 0; i < m_RightPanel.childCount; i++)
            {
                var cg  = GetOrAddCG(m_RightPanel.GetChild(i).gameObject);
                float d = 0.3f + i * 0.08f;
                seq.Insert(d, cg.DOFade(1f, 0.3f));
            }
        }

        if (m_BottomGroup != null)
            seq.Insert(0.6f, m_BottomGroup.DOFade(1f, 0.3f));

        seq.SetAutoKill(true);
    }

    private CanvasGroup GetOrAddCG(GameObject go) => go.GetOrAddComponent<CanvasGroup>();
}

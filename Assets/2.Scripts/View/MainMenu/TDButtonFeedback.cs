using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TDButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform m_ScaleTarget; // GroupPlay (parent row)
    [SerializeField] private Image m_Border;
    [SerializeField] private AudioClip m_ClickSfx;

    private static readonly Color k_BorderNormal = new Color(0.290f, 0.247f, 0.157f, 1f); // #4A3F28
    private static readonly Color k_BorderPress = new Color(0.769f, 0.596f, 0.137f, 1f); // #C49830
    private static readonly Vector3 k_ScaleDown = new Vector3(0.94f, 0.94f, 1f);

    private Tween m_ScaleTween;
    private Tween m_ColorTween;

    private void Awake()
    {
        if (m_ScaleTarget == null)
            m_ScaleTarget = transform.parent as RectTransform;
    }

    public void OnPointerDown(PointerEventData _)
    {
        m_ScaleTween?.Kill();
        m_ColorTween?.Kill();

        m_ScaleTween = m_ScaleTarget.DOScale(k_ScaleDown, 0.1f).SetEase(Ease.OutQuad);

        if (m_Border != null)
            m_ColorTween = m_Border.DOColor(k_BorderPress, 0.08f);

        if (m_ClickSfx != null)
            TDAudioService.SFX.Play(m_ClickSfx, volume: 0.65f);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();
    public void OnPointerExit(PointerEventData eventData) => Release();

    private void Release()
    {
        m_ScaleTween?.Kill();
        m_ColorTween?.Kill();

        m_ScaleTween = m_ScaleTarget.DOScale(Vector3.one, 0.18f).SetEase(Ease.OutQuart);

        if (m_Border != null)
            m_ColorTween = m_Border.DOColor(k_BorderNormal, 0.2f);
    }

    private void OnDestroy()
    {
        m_ScaleTween?.Kill();
        m_ColorTween?.Kill();
    }
}

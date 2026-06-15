using DG.Tweening;
using UnityEngine;

public class TDBGMToggleView : MonoBehaviour
{
    [SerializeField] private GameObject m_IconNormal; // child "Icon"
    [SerializeField] private GameObject m_IconMute; // child "IconMute"

    private void Awake()
    {
        TDAudioPrefs.OnBgmMuteChanged += OnMuteChanged;
        ApplyState(TDAudioPrefs.IsBgmMuted, animate: false);
    }

    private void OnDestroy()
    {
        TDAudioPrefs.OnBgmMuteChanged -= OnMuteChanged;
    }

    public void OnClick() => TDAudioPrefs.ToggleBgm();

    private void OnMuteChanged(bool muted) => ApplyState(muted, animate: true);

    private void ApplyState(bool muted, bool animate)
    {
        if (m_IconNormal != null) m_IconNormal.SetActive(!muted);
        if (m_IconMute != null) m_IconMute.SetActive(muted);

        if (!animate) return;
        var active = muted ? m_IconMute : m_IconNormal;
        if (active != null)
            active.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 1).SetUpdate(true);
    }
}

using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

public class TDBGMToggleView : MonoBehaviour
{
    [FormerlySerializedAs("m_IconNormal")]
    [SerializeField] private GameObject iconNormal; // child "Icon"
    [FormerlySerializedAs("m_IconMute")]
    [SerializeField] private GameObject iconMute; // child "IconMute"

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
        if (iconNormal != null) iconNormal.SetActive(!muted);
        if (iconMute != null) iconMute.SetActive(muted);

        if (!animate) return;
        var active = muted ? iconMute : iconNormal;
        if (active != null)
            active.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 1).SetUpdate(true);
    }
}

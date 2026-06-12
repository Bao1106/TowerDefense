using DG.Tweening;
using UnityEngine;

public class TDBGMPlayer
{
    private AudioSource m_Source;
    private Tweener m_Fade;
    private float m_TargetVolume = 0.45f;

    public void Play(AudioClip clip, float volume = 0.45f)
    {
        if (clip == null) return;
        EnsureSource();

        m_Fade?.Kill();
        m_Source.clip = clip;
        m_Source.loop = true;
        m_Source.volume = 0f;
        m_Source.Play();

        m_TargetVolume = volume;
        m_Fade = m_Source.DOFade(TDAudioPrefs.IsBgmMuted ? 0f : volume, TDConstant.BGM_FADE_IN).SetUpdate(true);
    }

    // Skip if the same clip is already playing (e.g. returning to main menu)
    public void PlayIfDifferent(AudioClip clip, float volume = 0.45f)
    {
        if (clip == null) return;
        if (m_Source != null && m_Source.isPlaying && m_Source.clip == clip) return;
        Play(clip, volume);
    }

    public void Stop(float fadeDuration = TDConstant.BGM_FADE_OUT)
    {
        if (m_Source == null || !m_Source.isPlaying) return;
        m_Fade?.Kill();
        m_Fade = m_Source.DOFade(0f, fadeDuration).SetUpdate(true)
                         .OnComplete(() => m_Source?.Stop());
    }

    public void ApplyMute(bool muted)
    {
        if (m_Source == null || !m_Source.isPlaying) return;
        m_Fade?.Kill();
        m_Fade = m_Source.DOFade(muted ? 0f : m_TargetVolume, TDConstant.BGM_MUTE_FADE).SetUpdate(true);
    }

    private void EnsureSource()
    {
        if (m_Source != null) return;
        var go = new GameObject("[BGM]");
        Object.DontDestroyOnLoad(go);
        m_Source = go.AddComponent<AudioSource>();
        m_Source.playOnAwake = false;
    }
}

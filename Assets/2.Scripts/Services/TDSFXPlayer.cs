using System.Collections.Generic;
using UnityEngine;

public class TDSFXPlayer
{
    private AudioSource[] m_Pool = new AudioSource[TDConstant.SFX_POOL_SIZE];
    private readonly Dictionary<int, float> m_LastTime = new();
    private Transform m_Root;

    public void Init()
    {
        if (m_Root != null) return;
        var go = new GameObject("[SFX Pool]");
        Object.DontDestroyOnLoad(go);
        m_Root = go.transform;

        for (int i = 0; i < TDConstant.SFX_POOL_SIZE; i++)
        {
            var src = new GameObject($"SFX_{i}").AddComponent<AudioSource>();
            src.transform.SetParent(m_Root);
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            m_Pool[i] = src;
        }
    }

    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null || TDAudioPrefs.IsSfxMuted) return;

        int key = clip.GetInstanceID();
        float now = Time.time;
        if (m_LastTime.TryGetValue(key, out float last) && now - last < TDConstant.SFX_MIN_INTERVAL) return;
        m_LastTime[key] = now;

        for (int i = 0; i < TDConstant.SFX_POOL_SIZE; i++)
        {
            var src = m_Pool[i];
            if (src == null || src.isPlaying) continue;
            src.clip = clip;
            src.volume = volume;
            src.Play();
            return;
        }
    }
}

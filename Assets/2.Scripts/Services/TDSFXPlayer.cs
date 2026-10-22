using System.Collections.Generic;
using UnityEngine;

public class TDSFXPlayer
{
    private AudioSource[] m_Pool = new AudioSource[TDConstant.SFX_POOL_SIZE];
    private readonly Dictionary<int, float> m_LastTime = new();
    private Transform m_Root;

    // Per voice, kept across calls so picking one allocates nothing — Play runs on every swing.
    private readonly bool[] m_Playing = new bool[TDConstant.SFX_POOL_SIZE];
    private readonly float[] m_StartedAt = new float[TDConstant.SFX_POOL_SIZE];
    private readonly bool[] m_IsPriority = new bool[TDConstant.SFX_POOL_SIZE];

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

    /// <summary>
    /// The voice a sound should take: the first free one; failing that, a priority sound takes the
    /// ordinary sound that started earliest. -1 means drop it. A priority sound never takes another
    /// priority sound's voice — two break stings cutting each other off would lose both.
    /// </summary>
    public static int PickVoice(bool[] playing, float[] startedAt, bool[] isPriority, bool wantPriority)
    {
        for (int i = 0; i < playing.Length; i++)
            if (!playing[i]) return i;
        if (!wantPriority) return -1;

        int oldest = -1;
        for (int i = 0; i < playing.Length; i++)
            if (!isPriority[i] && (oldest < 0 || startedAt[i] < startedAt[oldest])) oldest = i;
        return oldest;
    }

    public void Play(AudioClip clip, float volume = 1f, bool priority = false)
    {
        if (clip == null || TDAudioPrefs.IsSfxMuted) return;

        int key = clip.GetInstanceID();
        float now = Time.time;
        if (m_LastTime.TryGetValue(key, out float last) && now - last < TDConstant.SFX_MIN_INTERVAL) return;
        m_LastTime[key] = now;

        for (int i = 0; i < TDConstant.SFX_POOL_SIZE; i++)
        {
            var src = m_Pool[i];
            m_Playing[i] = src == null || src.isPlaying;
            if (src == null) m_IsPriority[i] = true; // a missing source is never picked and never taken
        }

        int v = PickVoice(m_Playing, m_StartedAt, m_IsPriority, priority);
        if (v < 0) return;

        var voice = m_Pool[v];
        voice.Stop(); // a taken voice cuts its old sound off rather than mixing into it
        voice.clip = clip;
        voice.volume = volume;
        voice.Play();
        m_StartedAt[v] = now;
        m_IsPriority[v] = priority;
    }
}

using DG.Tweening;
using UnityEngine;

/// <summary>
/// Static BGM system — consistent với TDEffectManager pattern.
/// Tạo AudioSource GameObject động, dùng DOTween fade in/out.
/// Init() gọi từ TDControl.InitOtherControl().
/// </summary>
public static class TDBGMManager
{
    private const float FADE_IN_DURATION  = 1f;
    private const float FADE_OUT_DURATION = 1.5f;

    private static AudioSource s_Source;
    private static Tweener     s_FadeTween;

    // ── Init / Cleanup ────────────────────────────────────────────────────────

    public static void Init()
    {
        Cleanup();
        TDGameEventBus.OnGameplayStarted += OnGameplayStarted;
        TDGameEventBus.OnVictory         += OnGameEnd;
        TDGameEventBus.OnGameOver        += OnGameEnd;
    }

    public static void Cleanup()
    {
        TDGameEventBus.OnGameplayStarted -= OnGameplayStarted;
        TDGameEventBus.OnVictory         -= OnGameEnd;
        TDGameEventBus.OnGameOver        -= OnGameEnd;

        StopImmediate();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public static void Play(AudioClip clip, float targetVolume = 0.45f)
    {
        if (clip == null) return;

        EnsureSource();

        s_FadeTween?.Kill();
        s_Source.clip   = clip;
        s_Source.loop   = true;
        s_Source.volume = 0f;
        s_Source.Play();

        s_FadeTween = s_Source.DOFade(targetVolume, FADE_IN_DURATION).SetUpdate(true);
    }

    public static void Stop(float fadeDuration = FADE_OUT_DURATION)
    {
        if (s_Source == null || !s_Source.isPlaying) return;

        s_FadeTween?.Kill();
        s_FadeTween = s_Source
            .DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .OnComplete(() => s_Source?.Stop());
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void OnGameplayStarted()
    {
        var stageId = TDGameStateControl.api?.SelectedStageId;
        if (string.IsNullOrEmpty(stageId)) return;

        var repo = Resources.Load<TDStageRepository>("Configs/Stage Repository");
        if (repo == null)
        {
            Debug.LogWarning("[TDBGMManager] Stage Repository not found in Resources");
            return;
        }

        var stage = repo.GetStage(stageId);
        if (stage == null) return;

        Play(stage.bgmClip, stage.bgmVolume);
    }

    private static void OnGameEnd() => Stop();

    private static void StopImmediate()
    {
        s_FadeTween?.Kill();
        s_FadeTween = null;

        if (s_Source != null)
        {
            s_Source.Stop();
            Object.Destroy(s_Source.gameObject);
            s_Source = null;
        }
    }

    private static void EnsureSource()
    {
        if (s_Source != null) return;
        var go = new GameObject("[BGM]");
        Object.DontDestroyOnLoad(go);
        s_Source = go.AddComponent<AudioSource>();
        s_Source.playOnAwake = false;
    }
}

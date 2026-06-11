using System;
using UnityEngine;

public static class TDAudioPrefs
{
    private const string KEY_BGM = "audio_bgm_muted";
    private const string KEY_SFX = "audio_sfx_muted";

    public static event Action<bool> OnBgmMuteChanged;
    public static event Action<bool> OnSfxMuteChanged;

    public static bool IsBgmMuted => PlayerPrefs.GetInt(KEY_BGM, 0) == 1;
    public static bool IsSfxMuted => PlayerPrefs.GetInt(KEY_SFX, 0) == 1;

    public static void ToggleBgm()
    {
        bool muted = !IsBgmMuted;
        PlayerPrefs.SetInt(KEY_BGM, muted ? 1 : 0);
        PlayerPrefs.Save();
        OnBgmMuteChanged?.Invoke(muted);
    }

    public static void ToggleSfx()
    {
        bool muted = !IsSfxMuted;
        PlayerPrefs.SetInt(KEY_SFX, muted ? 1 : 0);
        PlayerPrefs.Save();
        OnSfxMuteChanged?.Invoke(muted);
    }
}

using System;
using UnityEngine;

public static class TDAudioPrefs
{
    public static event Action<bool> OnBgmMuteChanged;
    public static event Action<bool> OnSfxMuteChanged;

    public static bool IsBgmMuted => PlayerPrefs.GetInt(TDConstant.AUDIO_KEY_BGM, 0) == 1;
    public static bool IsSfxMuted => PlayerPrefs.GetInt(TDConstant.AUDIO_KEY_SFX, 0) == 1;

    public static void ToggleBgm()
    {
        bool muted = !IsBgmMuted;
        PlayerPrefs.SetInt(TDConstant.AUDIO_KEY_BGM, muted ? 1 : 0);
        PlayerPrefs.Save();
        OnBgmMuteChanged?.Invoke(muted);
    }

    public static void ToggleSfx()
    {
        bool muted = !IsSfxMuted;
        PlayerPrefs.SetInt(TDConstant.AUDIO_KEY_SFX, muted ? 1 : 0);
        PlayerPrefs.Save();
        OnSfxMuteChanged?.Invoke(muted);
    }
}

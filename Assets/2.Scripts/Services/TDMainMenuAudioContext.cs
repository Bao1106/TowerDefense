using UnityEngine;

/// Wires MainMenu scene → TDAudioService.BGM.
/// Called from TDSceneController.OnSceneLoaded when DTMainMenu is loaded.
public static class TDMainMenuAudioContext
{
    public static void Activate(AudioClip bgmClip)
    {
        TDAudioService.BGM.PlayIfDifferent(bgmClip);
    }
}

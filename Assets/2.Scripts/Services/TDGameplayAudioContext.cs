using UnityEngine;

/// Wires gameplay events → TDAudioService.BGM.
/// Init/Cleanup called from TDControl.InitOtherControl() alongside gameplay controls.
public static class TDGameplayAudioContext
{
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
    }

    private static void OnGameplayStarted()
    {
        var stageId = TDGameStateControl.api?.SelectedStageId;
        if (string.IsNullOrEmpty(stageId)) return;

        var repo = Resources.Load<TDStageRepository>("Configs/Stage Repository");
        if (repo == null)
        {
            Debug.LogWarning("[TDGameplayAudioContext] Stage Repository not found in Resources");
            return;
        }

        var stage = repo.GetStage(stageId);
        if (stage == null) return;

        TDAudioService.BGM.Play(stage.bgmClip, stage.bgmVolume);
    }

    private static void OnGameEnd() => TDAudioService.BGM.Stop();
}

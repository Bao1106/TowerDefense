using System;
using UnityEngine;

public class TDSpeedControl
{
    public static TDSpeedControl api;

    public float SpeedMultiplier { get; private set; }
    public bool IsFast => SpeedMultiplier == TDConstant.SPEED_FAST;

    public Action<float> onSpeedChanged;

    public void Initialize()
    {
        SpeedMultiplier = TDConstant.SPEED_NORMAL;
        Time.timeScale = TDConstant.SPEED_NORMAL;
        onSpeedChanged?.Invoke(SpeedMultiplier);
    }

    public void ToggleSpeed()
    {
        SpeedMultiplier = IsFast ? TDConstant.SPEED_NORMAL : TDConstant.SPEED_FAST;

        // Only apply if not currently paused
        if (!TDPauseControl.api.IsPaused)
            Time.timeScale = SpeedMultiplier;

        onSpeedChanged?.Invoke(SpeedMultiplier);
    }
}

using System;
using UnityEngine;

public class TDSpeedControl
{
    public static TDSpeedControl api;

    private const float SPEED_NORMAL = 1f;
    private const float SPEED_FAST   = 2f;

    public float SpeedMultiplier { get; private set; }
    public bool  IsFast          => SpeedMultiplier == SPEED_FAST;

    public Action<float> onSpeedChanged;

    public void Initialize()
    {
        SpeedMultiplier    = SPEED_NORMAL;
        Time.timeScale     = SPEED_NORMAL;
        onSpeedChanged?.Invoke(SpeedMultiplier);
    }

    public void ToggleSpeed()
    {
        SpeedMultiplier = IsFast ? SPEED_NORMAL : SPEED_FAST;

        // Chỉ apply nếu không đang pause
        if (!TDPauseControl.api.IsPaused)
            Time.timeScale = SpeedMultiplier;

        onSpeedChanged?.Invoke(SpeedMultiplier);
    }
}

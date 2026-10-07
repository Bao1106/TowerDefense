using System;
using UnityEngine;

public class TDPauseControl
{
    public static TDPauseControl api;

    public bool IsPaused { get; private set; }

    public Action<bool> onPauseChanged;

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        Time.timeScale = 0f;
        onPauseChanged?.Invoke(true);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        // Speed control owns the formula (speed x focus); restoring SpeedMultiplier alone would
        // drop the selection slow-down.
        if (TDSpeedControl.api != null) TDSpeedControl.api.Apply();
        else Time.timeScale = 1f;
        onPauseChanged?.Invoke(false);
    }

    public void Toggle()
    {
        if (IsPaused) Resume(); else Pause();
    }
}

using System;
using UnityEngine;

public class TDSpeedControl
{
    public static TDSpeedControl api;

    public float SpeedMultiplier { get; private set; }
    public bool IsFast => SpeedMultiplier == TDConstant.SPEED_FAST;

    /// <summary>True while an operator is selected — time runs at SPEED_FOCUS of the chosen speed.</summary>
    public bool IsFocused { get; private set; }

    public Action<float> onSpeedChanged;

    /// <summary>
    /// The one formula for Time.timeScale. Pause, x2 and the selection slow-down used to be separate
    /// writers, each restoring what it thought the scale should be — Resume put back x2 and silently
    /// dropped any other factor. Pause always wins; the rest multiply.
    /// </summary>
    public static float ScaleFor(bool paused, float speed, bool focused)
        => paused ? 0f : speed * (focused ? TDConstant.SPEED_FOCUS : 1f);

    public void Initialize()
    {
        SpeedMultiplier = TDConstant.SPEED_NORMAL;
        IsFocused = false;
        Time.timeScale = TDConstant.SPEED_NORMAL;
        onSpeedChanged?.Invoke(SpeedMultiplier);
    }

    public void ToggleSpeed()
    {
        SpeedMultiplier = IsFast ? TDConstant.SPEED_NORMAL : TDConstant.SPEED_FAST;
        Apply();
        onSpeedChanged?.Invoke(SpeedMultiplier);
    }

    public void SetFocus(bool focused)
    {
        if (IsFocused == focused) return;
        IsFocused = focused;
        Apply();
    }

    /// <summary>Writes the scale this control's state implies. Safe while paused — it stays 0.</summary>
    public void Apply()
        => Time.timeScale = ScaleFor(TDPauseControl.api?.IsPaused ?? false, SpeedMultiplier, IsFocused);
}

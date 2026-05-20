using System;

public class TDUserInputControl
{
    public static TDUserInputControl api;

    public Action<bool> onMouseButton0Clicked;
    public Action<bool> onMouseButton1Clicked;
    public Action<bool> onMouseButtonEClicked;
    public Action<bool> onMouseButtonQClicked;

    public void OnMouseButton0Clicked() => onMouseButton0Clicked?.Invoke(true);
    public void OnMouseButton1Clicked() => onMouseButton1Clicked?.Invoke(true);
    public void OnMouseButtonEClicked() => onMouseButtonEClicked?.Invoke(true);
    public void OnMouseButtonQClicked() => onMouseButtonQClicked?.Invoke(true);
}
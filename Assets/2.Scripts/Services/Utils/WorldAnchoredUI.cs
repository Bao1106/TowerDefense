using UnityEngine;

/// <summary>
/// Positions a Canvas RectTransform so it tracks a world-space anchor point.
/// Shared by floating UI that hovers over battlefield units
/// (operator action panel, diamond direction picker buttons, etc.).
/// </summary>
public static class WorldAnchoredUI
{
    /// <summary>
    /// Moves <paramref name="rect"/> to overlap <paramref name="worldPos"/> on screen.
    /// Handles both ScreenSpaceOverlay and ScreenSpaceCamera/WorldSpace canvases.
    /// </summary>
    public static void PositionAt(RectTransform rect, Vector3 worldPos, Canvas canvas, Camera cam)
    {
        if (rect == null || canvas == null || cam == null) return;

        Vector2 screenPt = cam.WorldToScreenPoint(worldPos);

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            rect.position = screenPt;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(), screenPt, canvas.worldCamera, out Vector2 localPt);
            rect.localPosition = localPt;
        }
    }
}

using System.Collections.Generic;
using TDEnums;
using UnityEngine;

// Section-based random gate placement.
// Ensures gates are evenly distributed along the border and always placed at even coordinates (maze room cells).
public static class TDGatePlacer
{
    // Derives the BorderSide for start and end from the given MapLayout
    public static (BorderSide start, BorderSide end) GetBorders(MapLayout layout) => layout switch
    {
        MapLayout.LeftToRight => (BorderSide.Left, BorderSide.Right),
        MapLayout.RightToLeft => (BorderSide.Right, BorderSide.Left),
        MapLayout.TopToBottom => (BorderSide.Top, BorderSide.Bottom),
        MapLayout.BottomToTop => (BorderSide.Bottom, BorderSide.Top),
        MapLayout.Diagonal_TL_BR => (BorderSide.Top, BorderSide.Right),
        MapLayout.Diagonal_BL_TR => (BorderSide.Bottom, BorderSide.Right),
        _ => (BorderSide.Left, BorderSide.Right),
    };

    // Places `count` gates on the specified border.
    // Left/Right: sections run along the Y axis, with a fixed X coordinate.
    // Top/Bottom: sections run along the X axis, with a fixed Y coordinate.
    // Each section picks one random even coordinate → guarantees a maze room cell.
    public static List<Vector2Int> Place(int count, BorderSide border, int gridWidth, int gridHeight)
    {
        var result = new List<Vector2Int>(count);

        bool isVerticalBorder = border == BorderSide.Left || border == BorderSide.Right;
        int fixedAxis = GetFixedAxis(border, gridWidth, gridHeight);

        // Sections span only the band the viewport leaves visible — on both axes.
        int rangeMin = isVerticalBorder ? SafeMinY() : SafeMinX();
        int rangeMax = isVerticalBorder ? SafeMaxY(gridHeight) : SafeMaxX(gridWidth);

        // More gates than even coordinates in the band would place two on the same cell.
        int evenSlots = (rangeMax - rangeMin) / 2 + 1;
        if (count > evenSlots)
        {
            Debug.LogWarning($"<color=orange>[GatePlacer] {count} gates requested on {border} but only " +
                             $"{evenSlots} even cells fit the UI-safe band [{rangeMin}..{rangeMax}] — clamping</color>");
            count = Mathf.Max(1, evenSlots);
        }

        int span = rangeMax - rangeMin + 1;

        for (int i = 0; i < count; i++)
        {
            int sectionMin = rangeMin + i * span / count;
            int sectionMax = rangeMin + (i + 1) * span / count - 1;

            int picked = PickEvenInRange(sectionMin, sectionMax);

            result.Add(isVerticalBorder
                ? new Vector2Int(fixedAxis, picked)
                : new Vector2Int(picked, fixedAxis));
        }

        return result;
    }

    /// <summary>
    /// Is this cell inside the band the fixed ortho camera actually shows?
    ///
    /// Shared viewport rule, not a gate-only one: a tower slot outside this band is just
    /// as useless as an invisible gate — the player cannot see it to use it. It lives
    /// here because the safe-band arithmetic does.
    /// </summary>
    public static bool IsOnScreen(Vector2Int cell, int gridWidth, int gridHeight)
        => cell.x >= TDConstant.UI_SAFE_SIDE_COLS
        && cell.x <= gridWidth - 1 - TDConstant.UI_SAFE_SIDE_COLS
        && cell.y >= TDConstant.UI_SAFE_BOTTOM_ROWS
        && cell.y <= gridHeight - 1 - TDConstant.UI_SAFE_TOP_ROWS;

    // Lowest / highest even cell the viewport does not clip, on each axis.
    private static int SafeMinY() => EvenAtOrAbove(TDConstant.UI_SAFE_BOTTOM_ROWS);
    private static int SafeMaxY(int gridHeight) => EvenAtOrBelow(gridHeight - 1 - TDConstant.UI_SAFE_TOP_ROWS);
    private static int SafeMinX() => EvenAtOrAbove(TDConstant.UI_SAFE_SIDE_COLS);
    private static int SafeMaxX(int gridWidth) => EvenAtOrBelow(gridWidth - 1 - TDConstant.UI_SAFE_SIDE_COLS);

    private static int EvenAtOrAbove(int v) => v % 2 == 0 ? v : v + 1;
    private static int EvenAtOrBelow(int v) => v % 2 == 0 ? v : v - 1;

    // Rotation so the gate visual faces inward toward the map
    public static Quaternion FacingRotation(BorderSide border) => border switch
    {
        BorderSide.Left => Quaternion.Euler(0, 90, 0),
        BorderSide.Right => Quaternion.Euler(0, 270, 0),
        BorderSide.Top => Quaternion.Euler(0, 180, 0),
        BorderSide.Bottom => Quaternion.Euler(0, 0, 0),
        _ => Quaternion.identity,
    };

    // ── Private ───────────────────────────────────────────────────────────────

    // Every border is inset out of the clipped band: row 0 sits under the deploy bar, the
    // last row under the HUD, and the outermost columns run off the left and right edges.
    // A gate the player cannot see is a gate they cannot plan against.
    private static int GetFixedAxis(BorderSide border, int gridWidth, int gridHeight)
        => border switch
        {
            BorderSide.Left => SafeMinX(),
            BorderSide.Right => SafeMaxX(gridWidth),
            BorderSide.Bottom => SafeMinY(),
            BorderSide.Top => SafeMaxY(gridHeight),
            _ => SafeMinX(),
        };

    // Picks a random even number in [min, max]. Falls back to the nearest even number if the section is empty.
    private static int PickEvenInRange(int min, int max)
    {
        int startEven = min % 2 == 0 ? min : min + 1;

        var evens = new List<int>();
        for (int v = startEven; v <= max; v += 2)
            evens.Add(v);

        if (evens.Count > 0)
            return evens[Random.Range(0, evens.Count)];

        // Fallback: clamp to the nearest even number
        return min % 2 == 0 ? min : Mathf.Max(0, min - 1);
    }
}

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
        MapLayout.LeftToRight    => (BorderSide.Left,   BorderSide.Right),
        MapLayout.RightToLeft    => (BorderSide.Right,  BorderSide.Left),
        MapLayout.TopToBottom    => (BorderSide.Top,    BorderSide.Bottom),
        MapLayout.BottomToTop    => (BorderSide.Bottom, BorderSide.Top),
        MapLayout.Diagonal_TL_BR => (BorderSide.Top,    BorderSide.Right),
        MapLayout.Diagonal_BL_TR => (BorderSide.Bottom, BorderSide.Right),
        _                        => (BorderSide.Left,   BorderSide.Right),
    };

    // Places `count` gates on the specified border.
    // Left/Right: sections run along the Y axis, with a fixed X coordinate.
    // Top/Bottom: sections run along the X axis, with a fixed Y coordinate.
    // Each section picks one random even coordinate → guarantees a maze room cell.
    public static List<Vector2Int> Place(int count, BorderSide border, int gridWidth, int gridHeight)
    {
        var result = new List<Vector2Int>(count);

        bool isVerticalBorder = border == BorderSide.Left || border == BorderSide.Right;
        int  fixedAxis        = GetFixedAxis(border, gridWidth, gridHeight);
        int  sectionLength    = isVerticalBorder ? gridHeight : gridWidth;

        for (int i = 0; i < count; i++)
        {
            int sectionMin = i       * sectionLength / count;
            int sectionMax = (i + 1) * sectionLength / count - 1;

            int picked = PickEvenInRange(sectionMin, sectionMax);

            result.Add(isVerticalBorder
                ? new Vector2Int(fixedAxis, picked)
                : new Vector2Int(picked,    fixedAxis));
        }

        return result;
    }

    // Rotation so the gate visual faces inward toward the map
    public static Quaternion FacingRotation(BorderSide border) => border switch
    {
        BorderSide.Left   => Quaternion.Euler(0,  90, 0),
        BorderSide.Right  => Quaternion.Euler(0, 270, 0),
        BorderSide.Top    => Quaternion.Euler(0, 180, 0),
        BorderSide.Bottom => Quaternion.Euler(0,   0, 0),
        _                 => Quaternion.identity,
    };

    // ── Private ───────────────────────────────────────────────────────────────

    private static int GetFixedAxis(BorderSide border, int gridWidth, int gridHeight)
    {
        int lastCol = gridWidth  - 1; if (lastCol % 2 != 0) lastCol--;
        int lastRow = gridHeight - 1; if (lastRow % 2 != 0) lastRow--;

        return border switch
        {
            BorderSide.Left   => 0,
            BorderSide.Right  => lastCol,
            BorderSide.Bottom => 0,
            BorderSide.Top    => lastRow,
            _                 => 0,
        };
    }

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

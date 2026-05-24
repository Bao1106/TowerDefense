using System.Collections.Generic;
using UnityEngine;

// Sinh waypoints random cho 1 path trong 1 Y-zone
// Output: array waypoints với consecutive cùng X hoặc cùng Y → feed thẳng cho BuildDefinedPath
//
// Algorithm:
//   1. Enter zone: vertical từ start.y vào Y random trong zone
//   2. Zigzag: lặp [horizontal step random → vertical pick Y khác trong zone]
//   3. Exit zone: horizontal đến end.x → BuildDefinedPath xử lý vertical cuối tới end.y
public class TDPathGeneratorControl
{
    public static TDPathGeneratorControl api;

    public Vector2Int[] GenerateRandomWaypoints(Vector2Int start, Vector2Int end, int yMin, int yMax)
    {
        List<Vector2Int> waypoints = new List<Vector2Int>();

        int minStep = TDConstant.CONFIG_PATH_MIN_STEP_X;
        int maxStep = TDConstant.CONFIG_PATH_MAX_STEP_X;
        int minDy   = TDConstant.CONFIG_PATH_MIN_VERTICAL_DELTA;

        // 1. Enter zone — vertical waypoint tại X=start.x, Y random trong zone
        int enterY = Random.Range(yMin, yMax + 1);
        if (enterY != start.y)
            waypoints.Add(new Vector2Int(start.x, enterY));

        int curX = start.x;
        int curY = enterY;

        // 2. Zigzag horizontal+vertical pairs cho tới khi gần end.x
        // Để chừa room cho exit segment → dừng khi curX + minStep > end.x
        while (curX + minStep <= end.x - 1)
        {
            // --- Horizontal step ---
            int stepX = Random.Range(minStep, maxStep + 1);
            int nextX = Mathf.Min(curX + stepX, end.x - 1); // không vượt end.x

            // Nếu nextX cùng curX (edge case khi clamp) → break để tránh waypoint trùng
            if (nextX == curX) break;

            waypoints.Add(new Vector2Int(nextX, curY)); // horizontal waypoint
            curX = nextX;

            // --- Vertical step (khác Y hiện tại, ≥ minDy) ---
            int nextY = PickDifferentY(curY, yMin, yMax, minDy);
            waypoints.Add(new Vector2Int(curX, nextY));  // vertical waypoint
            curY = nextY;
        }

        // 3. Exit zone — horizontal tới end.x tại curY
        // (BuildDefinedPath sẽ tự append vertical cuối từ curY → end.y)
        if (curX != end.x)
            waypoints.Add(new Vector2Int(end.x, curY));

        return waypoints.ToArray();
    }

    // Chọn Y mới trong [yMin..yMax], khác curY tối thiểu minDelta
    // Fallback: nếu zone quá hẹp → trả về yMin/yMax (xa curY nhất có thể)
    private int PickDifferentY(int curY, int yMin, int yMax, int minDelta)
    {
        // Build candidate list: tất cả Y có |Y - curY| >= minDelta
        List<int> candidates = new List<int>();
        for (int y = yMin; y <= yMax; y++)
        {
            if (Mathf.Abs(y - curY) >= minDelta)
                candidates.Add(y);
        }

        // Zone quá hẹp → fallback chọn extreme
        if (candidates.Count == 0)
            return curY > (yMin + yMax) / 2 ? yMin : yMax;

        return candidates[Random.Range(0, candidates.Count)];
    }
}

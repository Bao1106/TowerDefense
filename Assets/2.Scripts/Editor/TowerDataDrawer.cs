#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Custom Inspector cho TowerData — hiển thị range dạng lưới click-to-toggle.
// Trục: x = forward (→), y = right-of-facing (↑ = phải khi nhìn từ tower)
// Tower luôn ở ô (0,0) — màu xanh dương, không toggle được.
[CustomPropertyDrawer(typeof(TowerData))]
public class TowerDataDrawer : PropertyDrawer
{
    private const int MIN_X = -1, MAX_X = 6;
    private const int MIN_Y = -4, MAX_Y = 4;
    private const float CELL = 20f, GAP  = 1f;

    private int Cols => MAX_X - MIN_X + 1;  // 8
    private int Rows => MAX_Y - MIN_Y + 1;  // 9

    private static readonly Color C_TOWER   = new(0.20f, 0.55f, 1.00f);
    private static readonly Color C_ON      = new(0.20f, 0.78f, 0.35f);
    private static readonly Color C_OFF     = new(0.15f, 0.15f, 0.15f);
    private static readonly Color C_OUTLINE = new(0.32f, 0.32f, 0.32f);

    private static float LH => EditorGUIUtility.singleLineHeight;

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        if (!prop.isExpanded) return LH + 2f;

        return LH + 4f                   // foldout header
             + (LH + 2f) * 5f           // 5 standard fields
             + 4f                        // spacer
             + LH + 4f                   // grid label
             + Rows * (CELL + GAP)       // grid rows
             + 10f;                      // bottom padding
    }

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);
        float y = pos.y;

        // ── Foldout ─────────────────────────────────────────────────────────
        prop.isExpanded = EditorGUI.Foldout(
            new Rect(pos.x, y, pos.width, LH), prop.isExpanded, label, true);
        y += LH + 4f;
        if (!prop.isExpanded) { EditorGUI.EndProperty(); return; }

        float x = pos.x + 12f;
        float w = pos.width - 12f;

        // ── Standard fields ──────────────────────────────────────────────────
        DrawField(ref y, x, w, prop, "type");
        DrawField(ref y, x, w, prop, "bulletPrefab");
        DrawField(ref y, x, w, prop, "cost");
        DrawField(ref y, x, w, prop, "damage");
        DrawField(ref y, x, w, prop, "attackSpeed");
        y += 4f;

        // ── Grid header ──────────────────────────────────────────────────────
        var typeProp = prop.FindPropertyRelative("type");
        string title = $"Range — {typeProp.enumNames[typeProp.enumValueIndex]}  (T = tower, → = facing)";
        EditorGUI.LabelField(new Rect(x, y, w, LH), title, EditorStyles.boldLabel);
        y += LH + 4f;

        // ── Read current offsets ─────────────────────────────────────────────
        var arrProp = prop.FindPropertyRelative("rangeOffsets");
        var current = ReadOffsets(arrProp);
        var toggled = new HashSet<Vector2Int>(current);
        bool dirty  = false;

        // ── Draw grid (top row = MAX_Y, bottom = MIN_Y) ──────────────────────
        for (int gy = MAX_Y; gy >= MIN_Y; gy--)
        {
            for (int gx = MIN_X; gx <= MAX_X; gx++)
            {
                float cx   = x + (gx - MIN_X) * (CELL + GAP);
                var   rect = new Rect(cx, y, CELL, CELL);
                bool  own  = gx == 0 && gy == 0;
                bool  on   = current.Contains(new Vector2Int(gx, gy));

                // Background
                EditorGUI.DrawRect(rect, own ? C_TOWER : on ? C_ON : C_OFF);

                // Outline on inactive cells
                if (!own && !on) DrawOutline(rect, C_OUTLINE);

                // Label
                if (own)
                    GUI.Label(rect, "T", Centered(Color.white, FontStyle.Bold, 11));
                else if (!on)
                    GUI.Label(rect, $"{gx},{gy}", Centered(new Color(0.38f, 0.38f, 0.38f), FontStyle.Normal, 7));

                // Toggle on click
                if (!own && Event.current.type == EventType.MouseDown
                    && rect.Contains(Event.current.mousePosition))
                {
                    var cell = new Vector2Int(gx, gy);
                    if (on) toggled.Remove(cell); else toggled.Add(cell);
                    dirty = true;
                    Event.current.Use();
                    GUI.changed = true;
                }
            }
            y += CELL + GAP;
        }

        if (dirty) WriteOffsets(arrProp, toggled);

        EditorGUI.EndProperty();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HashSet<Vector2Int> ReadOffsets(SerializedProperty arr)
    {
        var set = new HashSet<Vector2Int>();
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            set.Add(new Vector2Int(
                e.FindPropertyRelative("x").intValue,
                e.FindPropertyRelative("y").intValue));
        }
        return set;
    }

    private static void WriteOffsets(SerializedProperty arr, HashSet<Vector2Int> set)
    {
        var list = new List<Vector2Int>(set);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        arr.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("x").intValue = list[i].x;
            e.FindPropertyRelative("y").intValue = list[i].y;
        }
    }

    private static void DrawField(ref float y, float x, float w, SerializedProperty parent, string name)
    {
        EditorGUI.PropertyField(new Rect(x, y, w, LH), parent.FindPropertyRelative(name));
        y += LH + 2f;
    }

    private static void DrawOutline(Rect r, Color c)
    {
        float t = 1f;
        EditorGUI.DrawRect(new Rect(r.x,        r.y,        r.width, t),        c);
        EditorGUI.DrawRect(new Rect(r.x,        r.yMax - t, r.width, t),        c);
        EditorGUI.DrawRect(new Rect(r.x,        r.y,        t,       r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y,        t,       r.height), c);
    }

    private static GUIStyle Centered(Color color, FontStyle style, int size) => new(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontStyle = style,
        fontSize  = size,
        normal    = { textColor = color }
    };
}
#endif

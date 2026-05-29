#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Custom Inspector cho OperatorData — hiển thị range dạng lưới click-to-toggle.
// Trục: x = forward (→), y = right-of-facing (↑ = phải khi nhìn từ operator).
// Operator luôn ở ô (0,0) — màu cam, không toggle được.
[CustomPropertyDrawer(typeof(OperatorData))]
public class OperatorDataDrawer : PropertyDrawer
{
    private const int   MIN_X = -1, MAX_X = 3;
    private const int   MIN_Y = -2, MAX_Y = 2;
    private const float CELL  = 24f, GAP  = 1f;

    private int Cols => MAX_X - MIN_X + 1;  // 5
    private int Rows => MAX_Y - MIN_Y + 1;  // 5

    private static readonly Color C_OPERATOR = new(0.95f, 0.55f, 0.10f);  // cam — operator cell
    private static readonly Color C_ON       = new(0.20f, 0.78f, 0.35f);  // xanh — active
    private static readonly Color C_OFF      = new(0.15f, 0.15f, 0.15f);  // tối — inactive
    private static readonly Color C_OUTLINE  = new(0.32f, 0.32f, 0.32f);

    private static float LH => EditorGUIUtility.singleLineHeight;

    // Fields: operatorPrefab, icon, cost, hp, damage, attackSpeed, blockCount = 7 fields
    private const int FIELD_COUNT = 7;

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        if (!prop.isExpanded) return LH + 2f;

        return LH + 4f                          // foldout header
             + (LH + 2f) * FIELD_COUNT          // standard fields
             + 4f                               // spacer
             + LH + 4f                          // grid label
             + Rows * (CELL + GAP)              // grid rows
             + 10f;                             // bottom padding
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
        DrawField(ref y, x, w, prop, "operatorPrefab");
        DrawField(ref y, x, w, prop, "icon");
        DrawField(ref y, x, w, prop, "cost");
        DrawField(ref y, x, w, prop, "hp");
        DrawField(ref y, x, w, prop, "damage");
        DrawField(ref y, x, w, prop, "attackSpeed");
        DrawField(ref y, x, w, prop, "blockCount");
        y += 4f;

        // ── Grid header ──────────────────────────────────────────────────────
        EditorGUI.LabelField(new Rect(x, y, w, LH),
            "Range — Operator  (T = operator, → = facing)", EditorStyles.boldLabel);
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
                EditorGUI.DrawRect(rect, own ? C_OPERATOR : on ? C_ON : C_OFF);

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

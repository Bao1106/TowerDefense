#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom PropertyDrawer for EffectDef.
/// When onlySfx = true: hides all VFX fields and Camera Shake fields.
/// Only shows: key | onlySfx | sfxClip | sfxVolume.
/// </summary>
[CustomPropertyDrawer(typeof(EffectDef))]
public class EffectDefDrawer : PropertyDrawer
{
    private static float LineHeight => EditorGUIUtility.singleLineHeight;
    private const float PADDING = 2f;

    // ── Height ────────────────────────────────────────────────────────────────

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        if (!prop.isExpanded) return LineHeight + PADDING;

        bool onlySfx = prop.FindPropertyRelative("onlySfx").boolValue;
        bool shakeOn = !onlySfx && prop.FindPropertyRelative("cameraShake").boolValue;

        // always visible: foldout + key + onlySfx + [SFX label] + sfxClip + sfxVolume
        float h = (LineHeight + PADDING) * 6f;

        if (!onlySfx)
        {
            // [VFX label] + vfxPrefab + vfxPrefab2 + impactVfxPrefab
            // [Shake label] + cameraShake
            h += (LineHeight + PADDING) * 6f;

            if (shakeOn)
                h += (LineHeight + PADDING) * 2f; // shakeStrength + shakeDuration
        }

        return h + 4f; // bottom padding
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);
        float y = pos.y;

        // Foldout header
        prop.isExpanded = EditorGUI.Foldout(
            new Rect(pos.x, y, pos.width, LineHeight), prop.isExpanded, label, true);
        y += LineHeight + PADDING;

        if (!prop.isExpanded) { EditorGUI.EndProperty(); return; }

        float x = pos.x + 12f;
        float w = pos.width - 12f;

        // ── Always visible ────────────────────────────────────────────────────
        Field(ref y, x, w, prop, "key");
        Field(ref y, x, w, prop, "onlySfx");

        bool onlySfx = prop.FindPropertyRelative("onlySfx").boolValue;

        // ── VFX section (hidden when onlySfx) ────────────────────────────────
        if (!onlySfx)
        {
            SectionLabel(ref y, x, w, "VFX");
            Field(ref y, x, w, prop, "vfxPrefab");
            Field(ref y, x, w, prop, "vfxPrefab2");
            Field(ref y, x, w, prop, "impactVfxPrefab");
        }

        // ── SFX section (always visible) ──────────────────────────────────────
        SectionLabel(ref y, x, w, "SFX");
        Field(ref y, x, w, prop, "sfxClip");
        Field(ref y, x, w, prop, "sfxVolume");

        // ── Camera Shake section (hidden when onlySfx) ───────────────────────
        if (!onlySfx)
        {
            SectionLabel(ref y, x, w, "Camera Shake");
            Field(ref y, x, w, prop, "cameraShake");

            if (prop.FindPropertyRelative("cameraShake").boolValue)
            {
                Field(ref y, x, w, prop, "shakeStrength");
                Field(ref y, x, w, prop, "shakeDuration");
            }
        }

        EditorGUI.EndProperty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Field(ref float y, float x, float w, SerializedProperty parent, string name)
    {
        var child = parent.FindPropertyRelative(name);
        EditorGUI.PropertyField(new Rect(x, y, w, LineHeight), child);
        y += LineHeight + PADDING;
    }

    private static void SectionLabel(ref float y, float x, float w, string title)
    {
        var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 };
        EditorGUI.LabelField(new Rect(x, y, w, LineHeight), title, style);
        y += LineHeight + PADDING;
    }
}
#endif

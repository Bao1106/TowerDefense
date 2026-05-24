#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class CopyPath
{
    [MenuItem("GameObject/2D Object/Copy Path")]
    private static void CopyPathItem()
    {
        GameObject go = Selection.activeGameObject;

        if (go == null)
        {
            return;
        }

        string path = go.name;

        while (go.transform.parent != null)
        {
            go = go.transform.parent.gameObject;
            path = string.Format("{0}/{1}", go.name, path);
        }

        EditorGUIUtility.systemCopyBuffer = path;
    }

    [MenuItem("GameObject/2D Object/Copy Path", true)]
    private static bool CopyPathValidation()
    {
        // We can only copy the path in case 1 object is selected
        return Selection.gameObjects.Length == 1;
    }
}
#endif
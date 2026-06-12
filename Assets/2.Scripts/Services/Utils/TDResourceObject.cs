using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

public class TDResourceObject : MonoBehaviour
{
    [SerializeField] private List<Object> objects;

    public static TDResourceObject Instance;

    private void Awake()
    {
        Instance = this;
    }

    public static T GetResource<T>(string name) where T : Object
    {
        if (Instance == null || Instance.objects == null)
        {
            Debug.LogError($"[TDResourceObject] Instance not ready — cannot resolve '{name}'");
            return null;
        }

        var realName = Path.GetFileNameWithoutExtension(name);
        foreach (var obj in Instance.objects)
        {
            if (obj != null && obj.name.Equals(realName))
                return obj as T;
        }

        Debug.LogError($"[TDResourceObject] Resource not found: '{realName}'");
        return null;
    }
}

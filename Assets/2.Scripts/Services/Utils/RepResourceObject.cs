using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class RepResourceObject : MonoBehaviour
{
    [SerializeField] private List<Object> objects;
    public static RepResourceObject Instance;

    private void Awake()
    {
        Instance = this;
    }
    
    public static T GetResource<T>(string name) where T : Object
    {
        if (Instance.objects != null)
        {
            Debug.Log("ResourceObject GetResource:" + Instance.objects.Count);
        }
        else
        {
            Debug.Log("ResourceObject GetResource null");

        }
        
        var realName = Path.GetFileNameWithoutExtension(name);
        foreach (var prefab in Instance.objects!)
        {
            if (prefab.name.Equals(realName))
            {
                return prefab as T;
            }
        }

        return default(T);
    }
}

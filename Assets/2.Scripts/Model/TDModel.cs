using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class TDModel
{
    private static TDModel m_api;
    public static TDModel api
    {
        get
        {
            return m_api ??= new TDModel();
        }
    }

    public void Init(JObject data)
    {
        if (data == null)
        {
            return;
        }
        Debug.Log($"data: {data}");
    }
}

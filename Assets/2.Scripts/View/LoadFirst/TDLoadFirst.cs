using UnityEngine;

public class TDLoadFirst : MonoBehaviour
{
    private void Start()
    {
        TDControl.api.Init();
    }
}

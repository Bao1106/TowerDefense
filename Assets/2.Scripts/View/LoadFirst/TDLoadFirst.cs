using UnityEngine;

public class TDLoadFirst : MonoBehaviour
{
    private void Start()
    {
        Application.targetFrameRate = 60;
        TDControl.api.Init();
    }
}

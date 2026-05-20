using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class InitView : MonoBehaviour
{
    private void Start()
    {
        Init();
    }

    private void Init()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        
        SceneManager.sceneLoaded += SceneManagerOnSceneLoaded;
        
        SceneManager.LoadSceneAsync(TDConstant.SCENE_LOAD_FIRST, LoadSceneMode.Additive);
        SceneManager.UnloadSceneAsync(TDConstant.SCENE_INIT);
    }
    
    private void SceneManagerOnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == TDConstant.SCENE_GAMEPLAY)
        {
            DisableCamera();
        }
    }

    private void DisableCamera()
    {
        if (Camera.main != null)
            Camera.main.enabled = false;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneManagerOnSceneLoaded;
    }
}

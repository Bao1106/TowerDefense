using UnityEngine;
using UnityEngine.SceneManagement;

public class InitView : MonoBehaviour
{
    private void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadSceneAsync(TDConstant.SCENE_LOAD_FIRST, LoadSceneMode.Additive);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TDConstant.SCENE_LOAD_FIRST) return;
        SceneManager.SetActiveScene(scene);
        SceneManager.UnloadSceneAsync(TDConstant.SCENE_INIT);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}

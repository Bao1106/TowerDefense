using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Persistent MonoBehaviour in DTLoadFirst.
/// Owns the MenuCamera, fade overlay, and drives all scene transitions.
public class TDSceneController : MonoBehaviour
{
    public static TDSceneController api;

    [SerializeField] private Camera menuCamera;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private AudioClip mainMenuBgm;

    public Camera MenuCamera => menuCamera;

    private void Awake()
    {
        api = this;
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case TDConstant.SCENE_GAMEPLAY:
                SetMenuCamera(false);
                break;
            case TDConstant.SCENE_MAIN_MENU:
                SetMenuCamera(true);
                TDMainMenuAudioContext.Activate(mainMenuBgm);
                break;
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == TDConstant.SCENE_GAMEPLAY) SetMenuCamera(true);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void GoToMainMenu() { EnsureUnpaused(); GoToMainMenuAsync().Forget("GoToMainMenu"); }
    public void RetryGameplay() { EnsureUnpaused(); RetryGameplayAsync().Forget("RetryGameplay"); }
    public void GoToGameplay() { EnsureUnpaused(); GoToGameplayAsync().Forget("GoToGameplay"); }

    // Always exit pause before a scene transition: gameplay popups call TDPauseControl.Pause()
    // on Victory/GameOver → Time.timeScale = 0 freezes any tween/awaiter not flagged SetUpdate(true).
    private static void EnsureUnpaused()
    {
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        TDPauseControl.api?.Resume();
    }

    // ── Private Async ─────────────────────────────────────────────────────────

    private async Task GoToMainMenuAsync()
    {
        await FadeToBlack();
        await WaitForOp(SceneManager.UnloadSceneAsync(TDConstant.SCENE_GAMEPLAY));
        await WaitForOp(SceneManager.LoadSceneAsync(TDConstant.SCENE_MAIN_MENU, LoadSceneMode.Additive));
        await FadeFromBlack();
    }

    private async Task RetryGameplayAsync()
    {
        await FadeToBlack();
        await WaitForOp(SceneManager.UnloadSceneAsync(TDConstant.SCENE_GAMEPLAY));
        TDControl.api.ReinitControls();
        await WaitForOp(SceneManager.LoadSceneAsync(TDConstant.SCENE_GAMEPLAY, LoadSceneMode.Additive));
        await FadeFromBlack();
    }

    private async Task GoToGameplayAsync()
    {
        await FadeToBlack();
        await WaitForOp(SceneManager.UnloadSceneAsync(TDConstant.SCENE_MAIN_MENU));
        TDControl.api.ReinitControls();
        await WaitForOp(SceneManager.LoadSceneAsync(TDConstant.SCENE_GAMEPLAY, LoadSceneMode.Additive));
        await FadeFromBlack();
    }

    // ── Fade helpers ──────────────────────────────────────────────────────────

    private Task FadeToBlack() => FadeAsync(1f, TDConstant.SCENE_FADE_OUT_DUR);
    private Task FadeFromBlack() => FadeAsync(0f, TDConstant.SCENE_FADE_IN_DUR);

    private Task FadeAsync(float target, float duration)
    {
        if (fadeGroup == null) return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>();
        fadeGroup.blocksRaycasts = true;
        fadeGroup.DOFade(target, duration)
                 .SetEase(Ease.InOutSine)
                 .SetUpdate(true) // ignore Time.timeScale — survives the pause set by Victory/GameOver
                 .OnComplete(() =>
                 {
                     if (target <= 0f) fadeGroup.blocksRaycasts = false;
                     tcs.SetResult(true);
                 });
        return tcs.Task;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetMenuCamera(bool active)
    {
        if (menuCamera != null) menuCamera.enabled = active;
    }

    private static async Task WaitForOp(AsyncOperation op)
    {
        while (!op.isDone) await Task.Yield();
    }
}

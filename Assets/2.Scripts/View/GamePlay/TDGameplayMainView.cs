using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TDGameplayMainView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_LifeText;
    [SerializeField] private Image           m_ScreenFlash;
    [SerializeField] private GameObject      m_PausePanel;

    private TDEnemyPathMainView m_TDEnemyPathMainView;
    private GameObject m_MapVisualize;

    private void Start()
    {
        CheckSceneLoaded();

        TDPlayerLifeControl.api.Initialize();
        TDPlayerLifeControl.api.onLifeChanged += OnLifeChanged;
        TDPlayerLifeControl.api.onGameOver    += OnGameOver;

        TDPauseControl.api.onPauseChanged += OnPauseChanged;
        if (m_PausePanel != null) m_PausePanel.SetActive(false);

        InitGameplay();
    }

    private void OnDestroy()
    {
        TDPlayerLifeControl.api.onLifeChanged -= OnLifeChanged;
        TDPlayerLifeControl.api.onGameOver    -= OnGameOver;
        TDPauseControl.api.onPauseChanged     -= OnPauseChanged;
    }

    private void OnPauseChanged(bool isPaused)
    {
        if (m_PausePanel != null) m_PausePanel.SetActive(isPaused);
    }

    public void OnPauseButtonClicked()
    {
        TDPauseControl.api.Toggle();
    }

    public void OnResumeButtonClicked()
    {
        TDPauseControl.api.Resume();
    }

    private void OnLifeChanged(int lives)
    {
        if (m_LifeText != null)
            m_LifeText.text = $"♥ {lives}";
        StartCoroutine(FlashScreen());
    }

    private void OnGameOver()
    {
        Debug.Log("<color=red>GAME OVER</color>");
        // TODO Phase 8: show GameOver panel
    }

    private IEnumerator FlashScreen()
    {
        if (m_ScreenFlash == null) yield break;
        m_ScreenFlash.color = new Color(1f, 0f, 0f, 0.35f);
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.35f, 0f, elapsed / 0.35f);
            m_ScreenFlash.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }
        m_ScreenFlash.color = Color.clear;
    }
    
    private void InitGameplay()
    {
        //Init grid for enemy path
        m_MapVisualize = GameObject.Find(TDConstant.GAMEPLAY_MAP_VISUALIZE);
        Vector3 mapSize = m_MapVisualize.GetComponent<Renderer>().bounds.size;
        Vector3 planePosition = m_MapVisualize.transform.position;
        TDGridMainModel.Initialize(mapSize, planePosition);
        TDGridMainModel.api.CreateGrid();

        IGridDTO gridDTO = new TDGridDTO(TDGridMainModel.api.width, TDGridMainModel.api.height);

        // Mark vùng bị HUD che (grid Y < CONFIG_PATH_MIN_GRID_Y) là non-walkable
        // để A* không bao giờ route path qua vùng đó
        MarkHUDZoneAsNonWalkable(gridDTO);

        //Init enemy path
        m_TDEnemyPathMainView = transform.Find(TDConstant.GAMEPLAY_ENEMY_PATH_MAIN_VIEW).GetComponent<TDEnemyPathMainView>();
        TDGameplayMainControl.api.InitEnemyPath(m_TDEnemyPathMainView, gridDTO);
    }
    
    private void MarkHUDZoneAsNonWalkable(IGridDTO gridDTO)
    {
        for (int x = 0; x < gridDTO.width; x++)
        {
            for (int y = 0; y < TDConstant.CONFIG_PATH_MIN_GRID_Y; y++)
            {
                gridDTO.GetCell(x, y).isWalkable = false;
            }
        }
    }

    private void CheckSceneLoaded()
    {
        Scene gameplayScene = SceneManager.GetSceneByName(TDConstant.SCENE_GAMEPLAY);
        if (gameplayScene.isLoaded)
        {
            SceneManager.SetActiveScene(gameplayScene);
            Debug.Log("<color=green>DTGamePlay scene is now active</color>");
        }
        else
        {
            Debug.LogError("<color=red>Failed to load DTGamePlay scene</color>");
        }
    }
}

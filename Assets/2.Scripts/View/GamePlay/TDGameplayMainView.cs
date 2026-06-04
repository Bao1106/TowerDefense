using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chịu trách nhiệm khởi tạo gameplay core: grid, enemy path.
/// Toàn bộ HUD được xử lý bởi TDGameplayHUDView (Canvas/SafeArea/Container).
/// </summary>
public class TDGameplayMainView : MonoBehaviour
{
    [SerializeField] private TDStageRepository m_StageRepository;

    private TDEnemyPathMainView m_TDEnemyPathMainView;
    private GameObject          m_MapVisualize;

    private void Start()
    {
        CheckSceneLoaded();
        TDPlayerLifeControl.api.Initialize();
        InitGameplay();
    }

    private void InitGameplay()
    {
        TDInitializeModel.Reset();
        TDPauseControl.api?.Resume();

        string        stageId = TDGameStateControl.api.SelectedStageId;
        TDStageConfig stage   = m_StageRepository.GetStage(stageId);

        m_MapVisualize = GameObject.Find(TDConstant.GAMEPLAY_MAP_VISUALIZE);
        ApplyMapVisual(stage?.VisualConfig);

        Vector3 mapSize       = m_MapVisualize.GetComponent<Renderer>().bounds.size;
        Vector3 planePosition = m_MapVisualize.transform.position;
        TDGridMainModel.Initialize(mapSize, planePosition);
        TDGridMainModel.api.CreateGrid();

        IGridDTO gridDTO = new TDGridDTO(TDGridMainModel.api.width, TDGridMainModel.api.height);

        m_TDEnemyPathMainView = transform.Find(TDConstant.GAMEPLAY_ENEMY_PATH_MAIN_VIEW)
                                         .GetComponent<TDEnemyPathMainView>();
        m_TDEnemyPathMainView.ApplyVisual(stage?.VisualConfig);
        m_TDEnemyPathMainView.ApplyStageConfig(stage);
        m_TDEnemyPathMainView.SetLevelIndex(stage?.LevelIndex ?? 0);

        // Wire gate assignment strategy theo stage config
        var strategy = TDControl.CreateStrategy(stage?.GateMode ?? TDEnums.GateAssignmentMode.RoundRobin);
        TDEnemyPathMainControl.api.SetStrategy(strategy);

        TDGameplayMainControl.api.InitEnemyPath(m_TDEnemyPathMainView, gridDTO);
    }

    private void ApplyMapVisual(TDMapVisualConfig config)
    {
        if (config == null) return;
        if (config.MapGroundMaterial != null)
            m_MapVisualize.GetComponent<Renderer>().material = config.MapGroundMaterial;
        if (config.SkyboxMaterial != null)
            RenderSettings.skybox = config.SkyboxMaterial;
        if (config.AmbientColor != Color.white)
            RenderSettings.ambientLight = config.AmbientColor;
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

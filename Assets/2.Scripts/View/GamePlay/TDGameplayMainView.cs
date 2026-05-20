using UnityEngine;
using UnityEngine.SceneManagement;

public class TDGameplayMainView : MonoBehaviour
{
    private TDEnemyPathMainView m_TDEnemyPathMainView;
    private GameObject m_MapVisualize;
    
    private void Start()
    {
        CheckSceneLoaded();
        InitGameplay();
    }
    
    private void InitGameplay()
    {
        IGridDTO gridDTO = new TDGridDTO(25, 25);

        //Init grid for enemy path
        m_MapVisualize = GameObject.Find(TDConstant.GAMEPLAY_MAP_VISUALIZE);
        Vector3 mapSize = m_MapVisualize.GetComponent<Renderer>().bounds.size;
        Vector3 planePosition = m_MapVisualize.transform.position;
        TDGridMainModel.Initialize(mapSize, planePosition);
        TDGridMainModel.api.CreateGrid();
        
        //Init enemy path
        m_TDEnemyPathMainView = transform.Find(TDConstant.GAMEPLAY_ENEMY_PATH_MAIN_VIEW).GetComponent<TDEnemyPathMainView>();
        TDGameplayMainControl.api.InitEnemyPath(m_TDEnemyPathMainView, gridDTO);
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

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TDControl
{
    private static TDControl m_api;
    public static TDControl api
    {
        get
        {
            return m_api ??= new TDControl();
        }
    }
    
    public void Init()
    {
        Debug.Log("Init mini app main control");
        InitOtherControl();

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            return;
        }
        
        LoadGameplayScene();
        //AppBridge.Instance.CallOnMiniAPIReady(OnApiMiniAppReady);
    }

    private void InitOtherControl()
    {
        //Init main control
        TDPauseControl.api            = new TDPauseControl();
        TDPlayerLifeControl.api       = new TDPlayerLifeControl();
        TDGoldControl.api             = new TDGoldControl();
        TDGameStateControl.api        = new TDGameStateControl();
        TDSpeedControl.api            = new TDSpeedControl();
        TDGameplayMainControl.api     = new TDGameplayMainControl();
        TDEnemyPathMainControl.api    = new TDEnemyPathMainControl();
        TDTowerMainControl.api        = new TDTowerMainControl();
        TDaStarPathControl.api        = new TDaStarPathControl();
        TDObstacleControl.api         = new TDObstacleControl();
        TDPathGeneratorControl.api    = new TDPathGeneratorControl();
        
        //Init sub control
        TDEnemyRegistry.api    = new TDEnemyRegistry();
        TDEnemyPathControl.api = new TDEnemyPathControl();
        TDEnemyControl.api     = new TDEnemyControl();
        TDPlaceTowerControl.api = new TDPlaceTowerControl();
        TDTowerFactoryControl.api = new TDTowerFactoryControl();
        TDTowerBehaviorMainControl.api = new TDTowerBehaviorMainControl();
        TDTowerBehaviorSubControl.api = new TDTowerBehaviorSubControl();
        TDUserInputControl.api = new TDUserInputControl();
    }
    
    // private void OnApiMiniAppReady(APIUnity apiUnity)
    // {
    //     TDWebService.api = apiUnity;
    //     JObject configData = null; //update later
    //     
    //     TDModel.api.Init(configData);
    //     OnFinishLoadConfigData(LoadGameplayScene);
    // }

    // private void OnFinishLoadConfigData(Action callback)
    // {
    //     CoroutineHelper.Call(
    //         TDWebService.api
    //             .GetAppDataList(new ModelV2.AppDataGetListRequest(), (result) =>
    //     {
    //         if (result.Success)
    //         {
    //             if (result.Data.Items.Any(x => x.AppDataSchema.Name.Equals(TDServiceKey.PARAM_USER_PROFILE)))
    //             {
    //                 //TO DO SOMETHING
    //             }
    //         }
    //         callback.Invoke();
    //     }));
    // }
    
    private void LoadGameplayScene()
    {
        SceneManager.LoadSceneAsync(TDConstant.SCENE_GAMEPLAY, LoadSceneMode.Additive);
    }
}

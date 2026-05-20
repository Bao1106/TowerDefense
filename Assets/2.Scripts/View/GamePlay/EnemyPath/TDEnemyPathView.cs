using System.Collections.Generic;
using Services.DependencyInjection;
using UnityEngine;

public class TDEnemyPathView : MonoBehaviour
{
    private GameObject m_PathPrefab;
    private float m_PathOffsetY;
    
    private List<GameObject> m_InstantiatedTiles = new List<GameObject>();

    public void RegistryValues()
    {
        m_PathPrefab = RepResourceObject.GetResource<GameObject>(TDConstant.PREFAB_PATH);
        TDEnemyPathControl.api.onGetPaths += OnGetPaths;
    }

    private void OnDestroy()
    {
        TDEnemyPathControl.api.onGetPaths -= OnGetPaths;
    }

    private void OnGetPaths(List<GameObject> paths)
    {
        m_InstantiatedTiles = paths;
    }

    public void VisualizePath(List<IGridCellDTO> path)
    {
        ClearPreviousPath();
        TDEnemyPathControl.api.CreatePath(path, m_PathPrefab);
    }

    private void ClearPreviousPath()
    {
        foreach (var tile in m_InstantiatedTiles)
        {
            Destroy(tile);
        }
        m_InstantiatedTiles.Clear();
    }
}
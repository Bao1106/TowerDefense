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
        // AddRange thay vì = để accumulate tiles từ nhiều lần CreatePath
        m_InstantiatedTiles.AddRange(paths);
    }

    // Visualize 1 path duy nhất (clear trước)
    public void VisualizePath(List<IGridCellDTO> path)
    {
        ClearPreviousPath();
        TDEnemyPathControl.api.CreatePath(path, m_PathPrefab);
    }

    // Visualize tất cả paths cùng lúc (clear 1 lần, tạo tất cả)
    // Bỏ qua cell đầu (GateStart) và cell cuối (GateEnd) — 2 ô đó có gate prefab,
    // không cần path tile phủ bên dưới.
    public void VisualizeAllPaths(List<List<IGridCellDTO>> allPaths)
    {
        ClearPreviousPath();
        foreach (var path in allPaths)
        {
            if (path.Count <= 2)
            {
                TDEnemyPathControl.api.CreatePath(path, m_PathPrefab);
                continue;
            }
            var trimmed = path.GetRange(1, path.Count - 2);
            TDEnemyPathControl.api.CreatePath(trimmed, m_PathPrefab);
        }
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
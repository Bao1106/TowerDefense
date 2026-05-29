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
        m_PathPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_PATH);
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

    // Path tile đã bị bỏ — enemy đi trực tiếp trên GameMapVisualize (ground).
    // Tower zone tile (cube sáng) sẽ được spawn trên non-path cells trong TDEnemyPathMainView.
    // Chỉ cần đánh dấu các path cells là occupied để grid system hoạt động đúng.
    // Đồng thời đăng ký non-gate path cells vào TDOperatorRegistry để operator có thể đặt ở đó.
    public void VisualizeAllPaths(List<List<IGridCellDTO>> allPaths)
    {
        // Dùng HashSet để tránh đăng ký cell trùng lặp (3 paths có thể share cells)
        var registeredOperatorCells = new HashSet<Vector2Int>();

        foreach (var path in allPaths)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var cell = path[i];
                Vector3 worldPos = TDGridMainModel.api.GetGrid()[cell.position.x, cell.position.y];
                TDGridMainModel.api.SetOccupiedCell(worldPos);

                // Bỏ qua gate cells (index 0 = GateStart, index cuối = GateEnd)
                // → chỉ register cells giữa path cho melee placement
                if (i > 0 && i < path.Count - 1)
                {
                    var gridCell = new Vector2Int(cell.position.x, cell.position.y);
                    if (registeredOperatorCells.Add(gridCell))
                        TDOperatorRegistry.api?.RegisterPathCell(gridCell);
                }
            }
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
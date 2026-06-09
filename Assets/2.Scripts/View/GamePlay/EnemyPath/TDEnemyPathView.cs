using System.Collections.Generic;
using Services.DependencyInjection;
using UnityEngine;
using Object = UnityEngine.Object;

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

    public void OverridePathTilePrefab(GameObject prefab)
    {
        if (prefab != null) m_PathPrefab = prefab;
    }

    private void OnDestroy()
    {
        TDEnemyPathControl.api.onGetPaths -= OnGetPaths;
    }

    private void OnGetPaths(List<GameObject> paths)
    {
        // Use AddRange instead of = to accumulate tiles across multiple CreatePath calls
        m_InstantiatedTiles.AddRange(paths);
    }

    // Visualize a single path (clears any previously rendered tiles first)
    public void VisualizePath(List<IGridCellDTO> path)
    {
        ClearPreviousPath();
        TDEnemyPathControl.api.CreatePath(path, m_PathPrefab);
    }

    // Spawns PathTile.prefab on all path cells (excluding gate cells).
    // Tile scale = cellSize / 10f (Unity Plane native size = 10u → cellSize is read from TDGridMainModel, derived from GameMapVisualize bounds).
    // Uses a HashSet to deduplicate: multiple paths can share cells.
    public void VisualizeAllPaths(List<List<IGridCellDTO>> allPaths)
    {
        if (m_PathPrefab == null)
            m_PathPrefab = TDResourceObject.GetResource<GameObject>(TDConstant.PREFAB_PATH);

        float cellSize = TDGridMainModel.api.cellSize;
        float tileScale = cellSize / 10f; // Plane mesh native = 10u

        var visitedCells = new HashSet<Vector2Int>();

        foreach (var path in allPaths)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var cell = path[i];
                Vector3 worldPos = TDGridMainModel.api.GetGrid()[cell.position.x, cell.position.y];
                TDGridMainModel.api.SetOccupiedCell(worldPos);

                // Skip gate cells (index 0 = GateStart, last index = GateEnd)
                if (i == 0 || i == path.Count - 1)
                    continue;

                var gridCell = new Vector2Int(cell.position.x, cell.position.y);
                if (!visitedCells.Add(gridCell))
                    continue;

                // Register this as a valid operator cell
                TDOperatorRegistry.api?.RegisterPathCell(gridCell);

                // Spawn path tile
                Vector3 tilePos = new Vector3(worldPos.x, TDConstant.CONFIG_PATH_OFFSET_Y, worldPos.z);
                var tile = Object.Instantiate(m_PathPrefab, tilePos, Quaternion.identity, transform);
                tile.transform.localScale = new Vector3(tileScale, tile.transform.localScale.y, tileScale);
                m_InstantiatedTiles.Add(tile);
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
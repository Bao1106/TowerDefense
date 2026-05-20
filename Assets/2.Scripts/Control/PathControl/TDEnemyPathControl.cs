using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public class TDEnemyPathControl
{
    public static TDEnemyPathControl api;

    public Action<List<GameObject>> onGetPaths;
    
    public void CreatePath(List<IGridCellDTO> paths, GameObject pathPrefab)
    {
        List<GameObject> tiles = new List<GameObject>();
        
        foreach (IGridCellDTO cell in paths)
        {
            Vector3 worldPosition = TDGridMainModel.api.GetGrid()[cell.position.x, cell.position.y];
            GameObject tile = Object.Instantiate(pathPrefab, worldPosition, Quaternion.identity);
            tile.transform.position =
                new Vector3(tile.transform.position.x, TDConstant.CONFIG_PATH_OFFSET_Y, tile.transform.position.z);
            tiles.Add(tile);
                
            TDGridMainModel.api.SetOccupiedCell(worldPosition);

            /*// Điều chỉnh rotation nếu cần
            if (i < path.Count - 1)
            {
                Vector3 nextPosition = new Vector3(path[i + 1].Position.x, tileOffsetY, path[i + 1].Position.y);
                Vector3 direction = nextPosition - tilePosition;
                tile.transform.forward = direction.normalized;
            }*/
        }
        
        onGetPaths?.Invoke(tiles);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public class TDEnemyControl
{
    public static TDEnemyControl api;
    
    public Action<string, List<Vector3>, int> onGetEnemyPathPos;
    
    public void SetEnemyPath(string key, List<IGridCellDTO> path)
    {
        List<Vector3> pos = new List<Vector3>();
        foreach (IGridCellDTO cell in path)
        {
            Vector3 worldPosition = TDGridMainModel.api.GetGrid()[cell.position.x, cell.position.y];
            //pathPositions.Add(new Vector3(cell.Position.x, transform.position.y, cell.Position.y));
            pos.Add(worldPosition);
        }
        
        int curPathIndex = 0;
        onGetEnemyPathPos?.Invoke(key, pos, curPathIndex);
    }
}

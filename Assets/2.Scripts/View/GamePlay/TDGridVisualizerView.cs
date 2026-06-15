using System;
using UnityEngine;
using UnityEngine.Serialization;

public class TDGridVisualizerView : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        if (TDGridMainModel.api == null) return;
        
        Vector3[,] grid = TDGridMainModel.api.GetGrid();
        if (grid == null) return;

        for (int x = 0; x < TDGridMainModel.api.width; x++)
        {
            for (int z = 0; z < TDGridMainModel.api.height; z++)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(grid[x, z], new Vector3(TDGridMainModel.api.cellSize, 0.1f, TDGridMainModel.api.cellSize));
            }
        }
    }
}
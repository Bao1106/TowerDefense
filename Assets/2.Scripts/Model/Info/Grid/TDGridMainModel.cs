using UnityEngine;

public class TDGridMainModel : IGridMainModel
{
    public static TDGridMainModel api { get; private set; }

    public static void Initialize(Vector3 mapSize, Vector3 planePosition)
    {
        api ??= new TDGridMainModel(mapSize, planePosition);
    }

    private TDGridMainModel(Vector3 mapSize, Vector3 planePosition)
    {
        m_MapSize = mapSize;
        m_PlanePosition = planePosition;
    }
    
    private readonly Vector3 m_MapSize, m_PlanePosition;
    private Vector3[,] m_Grid;
    private bool[,] m_OccupiedCell;
    private float m_OffsetX, m_OffsetZ;

    public int width { get; private set; }
    public int height { get; private set; }
    public float cellSize
    {
        get
        {
            return TDConstant.CONFIG_GRID_CELL_SIZE;
        }
    }
    
    public void CreateGrid()
    {
        width = Mathf.FloorToInt(m_MapSize.x / cellSize);
        height = Mathf.FloorToInt(m_MapSize.z / cellSize);
        
        m_OffsetX = m_PlanePosition.x - (m_MapSize.x / 2) + (cellSize / 2);
        m_OffsetZ = m_PlanePosition.z - (m_MapSize.z / 2) + (cellSize / 2);
        
        m_Grid = new Vector3[width, height];
        m_OccupiedCell = new bool[width, height];
        
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                var xPos = x * cellSize + m_OffsetX;
                var zPos = z * cellSize + m_OffsetZ;
                m_Grid[x, z] = new Vector3(xPos, 0, zPos);
                m_OccupiedCell[x, z] = false;
            }
        }
        
        TDInitializeModel.api.createGridCompletion.SetResult(true);
    }

    public Vector3 GetNearestGridPosition(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt((worldPosition.x - m_OffsetX) / cellSize);
        int z = Mathf.RoundToInt((worldPosition.z - m_OffsetZ) / cellSize);
        x = Mathf.Clamp(x, 0, width - 1);
        z = Mathf.Clamp(z, 0, height - 1);
        
        return m_Grid[x, z];
    }
    
    public Vector3[,] GetGrid()
    {
        return m_Grid;
    }

    public void SetOccupiedCell(Vector3 position)
    {
        int x = Mathf.RoundToInt((position.x - m_OffsetX) / cellSize);
        int z = Mathf.RoundToInt((position.z - m_OffsetZ) / cellSize);
        
        m_OccupiedCell[x, z] = true;
    }

    public bool IsValidPlacement(Vector3 position)
    {
        int x = Mathf.RoundToInt((position.x - m_OffsetX) / cellSize);
        int z = Mathf.RoundToInt((position.z - m_OffsetZ) / cellSize);
        
        if (x < 0 || x >= width || z < 0 || z >= height)
        {
            return false;
        }
        
        if (m_OccupiedCell[x, z])
        {
            return false;
        }
        
        /*if (grid[x, z].IsPath)
        {
            return false;
        }
        */

        return true;
    } 
}

using UnityEngine;

public class TDGridDTO : IGridDTO
{
    private readonly IGridCellDTO[,] m_Cells;
        
    public int width { get; }
    public int height { get; }
        
    public TDGridDTO(int w, int h)
    {
        width = w;
        height = h;
        m_Cells = new IGridCellDTO[w, h];
        InitializeGrid();
    }
        
    private void InitializeGrid()
    {
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                m_Cells[x, y] = new TDGridCellDTO(x, y);
            }
        }
    }
        
    public IGridCellDTO GetCell(int x, int y)
    {
        return m_Cells[x, y];
    }

    public void SetCell(int x, int y, IGridCellDTO cellDto)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            Debug.LogError($"Attempted to set cell outside grid bounds: ({x}, {y})");
            return;
        }
        m_Cells[x, y] = cellDto;
    }
}
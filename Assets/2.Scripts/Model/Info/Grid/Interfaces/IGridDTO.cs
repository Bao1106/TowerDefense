public interface IGridDTO
{
    int width { get; }
    int height { get; }
    IGridCellDTO GetCell(int x, int y);
    void SetCell(int x, int y, IGridCellDTO cellDto);
}

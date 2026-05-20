public interface IPathFinderDTO
{
    void FindPath(IGridDTO gridDTO, IGridCellDTO start, IGridCellDTO end, bool isFinal);
}
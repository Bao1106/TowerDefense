using System.Collections.Generic;

// Slim interface cho A* — chỉ ComputePath, không có events hay legacy methods.
// TDMazePathGenerator inject qua constructor để tránh direct static call (DIP).
public interface IPathFinder
{
    List<IGridCellDTO> ComputePath(IGridDTO grid, IGridCellDTO start, IGridCellDTO end);
}

using System.Collections.Generic;

// Strategy interface cho gate assignment — OCP: thêm mode mới = thêm class mới,
// không sửa StartWaveLoop.
public interface IGateAssignmentStrategy
{
    // Trả về danh sách (group, corridor) cần spawn cho wave này.
    // Simultaneous trả về nhiều entries; các mode khác trả về đúng 1.
    List<(TDPathGroup group, List<IGridCellDTO> corridor)>
        SelectForWave(List<TDPathGroup> groups, int waveIndex);
}

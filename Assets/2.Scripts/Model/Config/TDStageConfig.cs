using UnityEngine;

[CreateAssetMenu(menuName = "Game Configs/Stage Config", fileName = "Stage Config")]
public class TDStageConfig : ScriptableObject
{
    public string            StageId;      // "DEMO-1", "DEMO-2"
    public string            DisplayName;  // tên hiển thị trong popup
    public int               LevelIndex;
    public TDMapVisualConfig VisualConfig;
}

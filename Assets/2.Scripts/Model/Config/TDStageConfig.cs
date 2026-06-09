using TDEnums;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Configs/Stage Config", fileName = "Stage Config")]
public class TDStageConfig : ScriptableObject
{
    public string            StageId;      // "DEMO-1", "DEMO-2"
    public string            DisplayName;
    public int               LevelIndex;
    public TDMapVisualConfig VisualConfig;

    [Header("Gates")]
    [Range(1, 3)] public int          StartGateCount = 1;
    [Range(1, 2)] public int          EndGateCount   = 1;
    public MapLayout                  Layout         = MapLayout.LeftToRight;
    public GateAssignmentMode         GateMode       = GateAssignmentMode.RoundRobin;

    [Header("Audio")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float bgmVolume = 0.45f;
}

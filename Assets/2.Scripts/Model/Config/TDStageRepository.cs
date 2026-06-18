using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Configs/Stage Repository", fileName = "Stage Repository")]
public class TDStageRepository : ScriptableObject
{
    private static TDStageRepository m_api;
    public static TDStageRepository api
        => m_api ??= TDResourceObject.GetResource<TDStageRepository>(TDConstant.CONFIG_STAGE_REPO);
    [SerializeField] private List<TDStageConfig> m_Stages;

    public string DefaultStageId => m_Stages != null && m_Stages.Count > 0
        ? m_Stages[0].StageId
        : "DEMO-1";

    public TDStageConfig GetStage(string stageId)
    {
        var stage = m_Stages?.Find(s => s.StageId == stageId);
        if (stage == null)
            Debug.LogError($"[TDStageRepository] Stage not found: {stageId}");
        return stage;
    }

    public IReadOnlyList<TDStageConfig> GetAll() => m_Stages;

    public TDStageConfig GetNextStage(string currentStageId)
    {
        if (m_Stages == null) return null;
        int idx = m_Stages.FindIndex(s => s.StageId == currentStageId);
        if (idx < 0 || idx >= m_Stages.Count - 1) return null;
        return m_Stages[idx + 1];
    }

    /// <summary>
    /// Returns the next stage in the list IF it's playable (not locked).
    /// Used by Victory popup to decide whether to show the "Next" button.
    /// Returns null when the current stage is the last unlocked one (end of demo content).
    /// </summary>
    public TDStageConfig GetNextUnlockedStage(string currentStageId)
    {
        var next = GetNextStage(currentStageId);
        return (next != null && !next.isLocked) ? next : null;
    }
}

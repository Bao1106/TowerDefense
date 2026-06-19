using System;

public class TDGameStateControl
{
    public static TDGameStateControl api;

    public string SelectedStageId { get; private set; } = TDStageRepository.api?.DefaultStageId ?? "DEMO-1";

    public void SelectStage(string stageId) => SelectedStageId = stageId;

    public int TotalEnemies { get; private set; }
    public int KilledEnemies { get; private set; }
    public bool AllWavesSpawned { get; private set; }
    public bool IsGameEnded { get; private set; }
    public int CurrentWave { get; private set; }
    public int TotalWaves { get; private set; }

    public Action<int, int> onEnemyCountChanged; // (killed, total)
    public Action onVictory;

    public void Initialize(int totalEnemies)
    {
        TotalEnemies = totalEnemies;
        KilledEnemies = 0;
        AllWavesSpawned = false;
        IsGameEnded = false;
        CurrentWave = 0;
        onEnemyCountChanged?.Invoke(KilledEnemies, TotalEnemies);
        TDGameEventBus.GameplayStarted();
    }

    public void OnWaveStarted(int waveNumber, int totalWaves)
    {
        CurrentWave = waveNumber;
        TotalWaves = totalWaves;
    }

    // Called when an enemy is killed (by damage) or escapes through the gate
    public void OnEnemyRemoved()
    {
        if (IsGameEnded) return;
        KilledEnemies++;
        onEnemyCountChanged?.Invoke(KilledEnemies, TotalEnemies);
        CheckVictory();
    }

    public void OnAllWavesSpawned()
    {
        if (IsGameEnded) return;
        AllWavesSpawned = true;
        CheckVictory();
    }

    // Called when the player loses (life = 0) to prevent victory from firing afterwards
    public void OnGameOver()
    {
        IsGameEnded = true;
    }

    private void CheckVictory()
    {
        if (IsGameEnded) return;
        if (AllWavesSpawned && KilledEnemies >= TotalEnemies)
        {
            IsGameEnded = true;
            onVictory?.Invoke();
            TDGameEventBus.Victory();
        }
    }
}

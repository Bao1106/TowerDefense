using System;

public class TDGameStateControl
{
    public static TDGameStateControl api;

    public string SelectedStageId { get; private set; } = "DEMO-1";

    public void SelectStage(string stageId) => SelectedStageId = stageId;

    public int  TotalEnemies    { get; private set; }
    public int  KilledEnemies   { get; private set; }
    public bool AllWavesSpawned { get; private set; }
    public bool IsGameEnded     { get; private set; }

    public Action<int, int> onEnemyCountChanged; // (killed, total)
    public Action           onVictory;

    public void Initialize(int totalEnemies)
    {
        TotalEnemies    = totalEnemies;
        KilledEnemies   = 0;
        AllWavesSpawned = false;
        IsGameEnded     = false;
        onEnemyCountChanged?.Invoke(KilledEnemies, TotalEnemies);
    }

    // Gọi khi enemy bị kill (damage) hoặc thoát vào gate
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

    // Gọi khi player thua (life = 0) để ngăn victory fire sau đó
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

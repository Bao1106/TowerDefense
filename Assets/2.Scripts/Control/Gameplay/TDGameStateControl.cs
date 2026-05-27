using System;

public class TDGameStateControl
{
    public static TDGameStateControl api;

    public int  TotalEnemies    { get; private set; }
    public int  KilledEnemies   { get; private set; }
    public bool AllWavesSpawned { get; private set; }

    public Action<int, int> onEnemyCountChanged; // (killed, total)
    public Action           onVictory;

    public void Initialize(int totalEnemies)
    {
        TotalEnemies    = totalEnemies;
        KilledEnemies   = 0;
        AllWavesSpawned = false;
        onEnemyCountChanged?.Invoke(KilledEnemies, TotalEnemies);
    }

    public void OnEnemyKilled()
    {
        KilledEnemies++;
        onEnemyCountChanged?.Invoke(KilledEnemies, TotalEnemies);
        CheckVictory();
    }

    public void OnAllWavesSpawned()
    {
        AllWavesSpawned = true;
        CheckVictory();
    }

    private void CheckVictory()
    {
        if (AllWavesSpawned && KilledEnemies >= TotalEnemies)
            onVictory?.Invoke();
    }
}

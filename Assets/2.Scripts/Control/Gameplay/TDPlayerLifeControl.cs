using System;
using UnityEngine;

public class TDPlayerLifeControl
{
    public static TDPlayerLifeControl api;

    public int CurrentLives { get; private set; }

    public Action<int> onLifeChanged;
    public Action onGameOver;

    public void Initialize()
    {
        CurrentLives = TDConstant.CONFIG_PLAYER_STARTING_LIVES;
        onLifeChanged?.Invoke(CurrentLives);
    }

    /// <summary>
    /// The one path for every lost life: an enemy reaching the base (1) or an operator dying
    /// (LIVES_PER_OPERATOR_DEATH, Tầng 3). Each life raises LifeLost at `where`, so the effect
    /// plays where the life was lost. Floored at zero, and the match ends exactly once — two
    /// enemies arriving on the last life used to raise GameOver twice.
    /// </summary>
    public void LoseLives(int count, Vector3 where)
    {
        if (count <= 0 || CurrentLives <= 0) return;

        for (int i = 0; i < count && CurrentLives > 0; i++)
        {
            TDGameEventBus.LifeLost(where);
            CurrentLives--;
        }

        onLifeChanged?.Invoke(CurrentLives);
        if (CurrentLives <= 0)
        {
            onGameOver?.Invoke();
            TDGameEventBus.GameOver();
        }
    }
}

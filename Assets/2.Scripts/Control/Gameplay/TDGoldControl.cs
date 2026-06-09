using System;

public class TDGoldControl
{
    public static TDGoldControl api;

    public int Gold { get; private set; }

    public Action<int> onGoldChanged;

    private float m_PassiveAccumulator;

    public void Initialize(int startingGold)
    {
        Gold                 = startingGold;
        m_PassiveAccumulator = 0f;
        onGoldChanged?.Invoke(Gold);
    }

    // Called every frame from TDGameplayHUDView.Update()
    // Pause-aware: Time.deltaTime = 0 when paused, so accumulation stops automatically
    public void Tick(float deltaTime)
    {
        m_PassiveAccumulator += deltaTime;
        if (m_PassiveAccumulator >= TDConstant.CONFIG_GOLD_PASSIVE_RATE)
        {
            int earned            = (int)(m_PassiveAccumulator / TDConstant.CONFIG_GOLD_PASSIVE_RATE);
            m_PassiveAccumulator -= earned * TDConstant.CONFIG_GOLD_PASSIVE_RATE;
            AddGold(earned);
        }
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        onGoldChanged?.Invoke(Gold);
    }

    public bool SpendGold(int amount)
    {
        if (amount > Gold) return false;
        Gold -= amount;
        onGoldChanged?.Invoke(Gold);
        return true;
    }
}

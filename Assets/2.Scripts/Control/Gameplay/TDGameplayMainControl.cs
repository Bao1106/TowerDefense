public class TDGameplayMainControl
{
    public static TDGameplayMainControl api;

    public void InitEnemyPath(TDEnemyPathMainView enemyPath, IGridDTO gridDTO)
    {
        enemyPath.Initialize(gridDTO);
    }
}

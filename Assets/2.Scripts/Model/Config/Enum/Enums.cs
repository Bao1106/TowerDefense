namespace TDEnums
{
    public enum CellType
    {
        Empty,
        Obstacle,
        Start,
        End
    }

    public enum EnemyAiType
    {
        Waypoint,
        Random
    }

    public enum TowerType
    {
        Cannon,
        Catapult,
        MissileG02,
        MissileG03,
        Mortar,
        Operator   // Arknights-style: đặt trên path, chặn enemy, đánh cận chiến
    }

    public enum AttackType
    {
        Single,    // 1 projectile → 1 target (homing)
        Multiple,  // N projectiles → top N targets by PathProgress (homing each)
        AOE        // 1 projectile flies to target pos → splash all enemies in rangeOffsets
    }

    public enum EnemyType
    {
        Normal,  // 300 HP, speed 3.0 — Slime
        Fast,    // 150 HP, speed 6.0 — Swarm insect
        Tank,    // 900 HP, speed 1.5 — TurtleShell
        Boss     // 3000 HP, speed 1.0 — DragonBoar
    }

    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
        Extreme,
        Nightmare
    }

    public enum OperatorType
    {
        Knight
    }

}
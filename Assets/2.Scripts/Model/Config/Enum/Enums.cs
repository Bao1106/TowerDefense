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

    // Class archetype — xác định behavior (deploy zone, block, attack style).
    // Identity cụ thể của từng operator được lưu trong OperatorData.operatorName.
    public enum OperatorType
    {
        Knight,    // PathCell, block 2 — melee balanced
        Defender,  // PathCell, block 3 — melee high HP tank
        Striker,   // PathCell, block 1 — melee reach
        Ranger,    // TowerZone, block 0 — ranged physical
        Mage,      // TowerZone, block 0 — ranged arts
    }

    public enum DeployZone
    {
        PathCell,   // đặt trên path cell, block enemy
        TowerZone,  // đặt trên tower zone, ranged attack
    }

    // Key định danh từng game event → tra cứu EffectDef trong TDEffectConfig SO.
    public enum GameEventKey
    {
        EnemyDied_Normal,
        EnemyDied_Fast,
        EnemyDied_Tank,
        EnemyDied_Boss,
        OperatorAttacked_Knight,
        OperatorAttacked_Defender,
        OperatorAttacked_Striker,
        OperatorAttacked_Ranger,
        OperatorAttacked_Mage,
        TowerAttacked,
        LifeLost,
        WaveStarted,
        Victory,
        GameOver,
    }

    public enum BorderSide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    public enum MapLayout
    {
        LeftToRight,     // S: Left  → E: Right  (default)
        RightToLeft,     // S: Right → E: Left
        TopToBottom,     // S: Top   → E: Bottom
        BottomToTop,     // S: Bottom→ E: Top
        Diagonal_TL_BR,  // S: Top   → E: Right
        Diagonal_BL_TR,  // S: Bottom→ E: Right
    }

    public enum GateAssignmentMode
    {
        RoundRobin,    // wave i → group i % groupCount
        Random,        // random group mỗi wave
        PerWave,       // group cố định suốt 1 wave, đổi theo wave index
        Simultaneous,  // tất cả groups spawn song song mỗi wave
    }

}
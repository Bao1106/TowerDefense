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
        Knight,        // close range ≤2, block 2 — balanced melee
        Defender,      // close range ≤2, block 3 — high HP tank
        Striker,       // long range ≥3, block 1 — reach melee
        KnightTiny,    // MC01 variant — same stats as Knight
        DefenderTiny,  // MC04 variant — same stats as Defender
        StrikerTiny,   // MC07 variant — same stats as Striker
        // Custom operators
        Ace,           // DoubleSword Striker, PathCell — fast dual-wield, block 1
        Ginger,        // Sniper, TowerZone — BowAndArrow, ranged physical
        Layla,         // Spear Striker, PathCell — reach melee, block 1
        Moon,          // Caster, TowerZone — MagicWand, ranged arts
        Tart,          // Heavy Defender, PathCell — hammer+shield, block 3
    }

    public enum DeployZone
    {
        PathCell,   // đặt trên path cell, block enemy
        TowerZone,  // đặt trên tower zone, ranged attack
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
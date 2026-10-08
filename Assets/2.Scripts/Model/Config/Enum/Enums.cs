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
        Operator // Arknights-style: placed on a path cell, blocks enemies, deals melee damage
    }

    public enum AttackType
    {
        Single, // 1 projectile → 1 target (homing)
        Multiple, // N projectiles → top N targets sorted by PathProgress (each homing)
        AOE // 1 projectile flies to target position → splashes all enemies within rangeOffsets
    }

    public enum EnemyType
    {
        Normal, // 300 HP, speed 3.0 — Slime
        Fast, // 150 HP, speed 6.0 — Swarm insect
        Tank, // 900 HP, speed 1.5 — TurtleShell
        Boss, // 3000 HP, speed 1.0 — DragonBoar
        Horde = 4, // 70 HP, speed 4.0 — one wave slot, spawned as a pack of HORDE_PACK_SIZE
        Herald = 5, // 500 HP, speed 2.0, no attack — amplifies every leak within HERALD_RADIUS cells (×HERALD_LEAK_MULT)
    }

    // Three rungs, not five. Easy was dropped because measurement showed the game was
    // already winnable with two operators and no retreat — a rung below that taught the
    // player nothing. Extreme went with it: five rungs meant neighbouring ones differed by
    // ~15% HP, a gap nobody feels. What is left is spaced far enough apart to read as
    // different games: hpMult 1.0 → 1.2 → 2.0.
    //
    // Values are 0/1/2, so every LevelConfig entry shifts UP one rung — Easy levels become
    // Normal, Normal becomes Hard. That is the intent, not a migration accident.
    public enum Difficulty
    {
        Normal,
        Hard,
        Nightmare
    }

    // Morale bands. Three discrete states, not a continuous bar: the player reads an icon,
    // and the icon changing shape is also the moment the accrual multiplier changes.
    public enum MoraleState
    {
        Calm,      // 0-33   x1.0
        Steady,    // 34-66  x1.5
        Stressed,  // 67-99  x2.0
        Broken     // 100 — cannot block, attack or retreat; takes triple damage
    }

    // Class archetype — defines behavior (deploy zone, block count, attack style).
    // The specific identity of each operator is stored in OperatorData.operatorName.
    // ARCHETYPE, deliberately not identity. Several operators legitimately share one — Striker,
    // Ace and Layla are all "melee reach" — and that is the whole point: this enum answers
    // "what shape of unit is it" for VFX and deploy zone, nothing else.
    //
    // WHO an operator is comes from their OperatorData row, and code must carry that reference
    // rather than looking it up from this enum. GetData(OperatorType) is Find(first match), so
    // any lookup keyed on an archetype silently hands back the wrong roster member's stats.
    public enum OperatorType
    {
        Knight, // PathCell, block 2 — melee balanced
        Defender, // PathCell, block 3 — melee high HP tank
        Striker, // PathCell, block 1 — melee reach
        Ranger, // TowerZone, block 0 — ranged physical
        Mage, // TowerZone, block 0 — ranged arts
    }

    public enum DeployZone
    {
        PathCell, // placed on a path cell, blocks enemies
        TowerZone, // placed on a tower zone, performs ranged attacks
    }

    // Key that identifies each game event → used to look up the corresponding EffectDef in TDEffectConfig SO.
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
        UnitPickup, // unit lifted from the slot bar (PointerDown)
        TowerPlaced, // tower / operator successfully placed
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
        LeftToRight, // S: Left → E: Right (default)
        RightToLeft, // S: Right → E: Left
        TopToBottom, // S: Top → E: Bottom
        BottomToTop, // S: Bottom→ E: Top
        Diagonal_TL_BR, // S: Top → E: Right
        Diagonal_BL_TR, // S: Bottom→ E: Right
    }

    public enum GateAssignmentMode
    {
        RoundRobin, // wave i → group i % groupCount
        Random, // random group each wave
        PerWave, // one fixed group per wave, rotates by wave index
        Simultaneous, // all groups spawn in parallel each wave
    }

    // Deploy-by-drag (Arknights style) finite-state machine.
    // Idle → Dragging → DirectionSelect → Committing (or back to Idle on cancel).
    public enum DeployState
    {
        Idle, // nothing being deployed
        Dragging, // ghost follows finger, snaps to cell (Phase 1)
        DirectionSelect, // cell locked, diamond shown, picking facing (Phase 2) — finger may be lifted
        Committing, // placement confirmed, spawning
    }

}
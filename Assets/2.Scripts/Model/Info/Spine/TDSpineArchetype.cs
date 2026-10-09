using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One chokepoint on the spine — an open area the enemy flow is forced through.
/// Position is normalised (0..1) so a single archetype fits any grid size.
/// </summary>
public struct TDChokepointDef
{
    /// <summary>
    /// FLOW SPACE, not screen space.
    ///   u (x) = progress along the start-gates → end-gates axis. 0 = at the start
    ///           gates, 1 = at the end gates.
    ///   v (y) = lateral offset across that axis. 0.5 = dead centre.
    ///
    /// This is what makes an archetype orientation-independent. Written in screen space
    /// the same numbers produced a spine running left→right across a map whose gates sat
    /// top and bottom — the skeleton cut ACROSS the enemy flow instead of guiding it.
    /// </summary>
    public Vector2 flowPos;

    public int tier;  // 1 = outermost (gates feed into it), higher = closer to the goal
    public int width; // open block size in cells — the 2..4 constraint lives here

    public TDChokepointDef(float u, float v, int tier, int width)
    {
        flowPos = new Vector2(u, v);
        this.tier = tier;
        this.width = width;
    }
}

/// <summary>
/// A connection between two chokepoints, or from a chokepoint out to the end gates.
/// </summary>
public struct TDSpineEdge
{
    public const int GOAL = -1; // `to == GOAL` means "run this node out to every end gate"

    public int from;
    public int to;

    public TDSpineEdge(int from, int to)
    {
        this.from = from;
        this.to = to;
    }
}

/// <summary>
/// Macro-structure of a map: where the chokepoints sit and how they are wired together.
///
/// Deliberately does NOT describe gates. Gate placement stays with MapLayout +
/// TDGatePlacer; the archetype only owns the interior, and gates attach to whichever
/// chokepoint is nearest. That one rule covers every archetype shape without a
/// per-shape special case.
///
/// The point of the whole thing: parallel corridors make attrition unplayable — losing
/// one operator leaves a third of the map open with no way to compensate. Convergent
/// topology lets the player trade space for lives: give up the outer line, fall back to
/// the inner chokepoint, hold with fewer people.
/// </summary>
public sealed class TDSpineArchetype
{
    public string name;
    public TDChokepointDef[] chokepoints;
    public TDSpineEdge[] edges;

    // Smallest grid this shape still reads as itself on. Below this the carver hands
    // over to `fallback` — see the fallback chain in TDSpineLibrary.
    public int minGridWidth;
    public int minGridHeight;

    // Gate counts this shape is built for. StageConfig owns StartGateCount /
    // EndGateCount, so the archetype has to agree with them or the result is nonsense:
    // FUNNEL with a single start gate has nothing to funnel.
    public int minStartGates = 1;
    public int minEndGates = 1;

    // Fallback chain runs from the most demanding shape to the least:
    //   FUNNEL → CASCADE (terminal).
    // CASCADE ends it because it is single-file on the centre line: it needs depth and
    // nothing else, so it survives any map shape the other cannot.
    public string fallback; // null only on CASCADE

    private const int GAP = 2; // breathing room so two nodes never touch

    /// <summary>
    /// Cells that must separate the CENTRES of two consecutive tiers: half of each node,
    /// plus the gap. Walks the actual consecutive pairs rather than using the widest node
    /// twice — FUNNEL's tiers are 3 and 4 wide and need 5,5 cells, but "widest twice" asks
    /// for 6 and would reject a shape that has been generating fine all along.
    /// </summary>
    public float RequiredTierSpacing
    {
        get
        {
            int maxTier = MaxTier;
            if (maxTier <= 1) return 0f;

            var widthPerTier = new int[maxTier + 1];
            for (int i = 0; i < chokepoints.Length; i++)
            {
                int t = chokepoints[i].tier;
                if (t >= 1 && t <= maxTier && chokepoints[i].width > widthPerTier[t])
                    widthPerTier[t] = chokepoints[i].width;
            }

            float worst = 0f;
            for (int t = 1; t < maxTier; t++)
            {
                float need = (widthPerTier[t] + widthPerTier[t + 1]) * 0.5f + GAP;
                if (need > worst) worst = need;
            }
            return worst;
        }
    }

    /// <summary>
    /// Fraction of the flow axis the chokepoints actually occupy (u_max − u_min).
    ///
    /// This is the number the earlier check was missing, and missing it made the check
    /// pass when it should have failed. It compared the requirement against the WHOLE
    /// axis, while the nodes only ever live on a slice of it: CASCADE at u = 0,30/0,55/0,80
    /// spans half the axis, so a 16-cell axis gave the tiers 8 cells to share, not 16.
    /// The three chokepoints overlapped and the check reported everything fine.
    /// </summary>
    public float SpanU
    {
        get
        {
            float lo = 1f, hi = 0f;
            for (int i = 0; i < chokepoints.Length; i++)
            {
                float u = chokepoints[i].flowPos.x;
                if (u < lo) lo = u;
                if (u > hi) hi = u;
            }
            return Mathf.Max(0f, hi - lo);
        }
    }

    public int MaxTier
    {
        get
        {
            int t = 1;
            for (int i = 0; i < chokepoints.Length; i++)
                if (chokepoints[i].tier > t) t = chokepoints[i].tier;
            return t;
        }
    }

    public int MaxWidth
    {
        get
        {
            int w = 1;
            for (int i = 0; i < chokepoints.Length; i++)
                if (chokepoints[i].width > w) w = chokepoints[i].width;
            return w;
        }
    }

    /// <summary>Cells that will actually separate consecutive tier centres on this axis.</summary>
    public float TierSpacing(float axisLength)
        => MaxTier <= 1 ? float.MaxValue : SpanU * axisLength / (MaxTier - 1);

    /// <summary>
    /// Half-width needed ACROSS the flow, in cells. Two nodes sharing a tier sit side by
    /// side, and the map has to be wide enough across the axis for them not to fuse.
    /// Zero for single-file shapes — that is what makes them the safe fallback.
    ///
    /// Derivation: two nodes at v = a and b land (b−a)·2·lateralHalf apart, so not
    /// overlapping needs lateralHalf >= width / (2·(b−a)).
    /// </summary>
    public float minLateralHalf;

    public bool Fits(int gridWidth, int gridHeight, int startGates, int endGates,
                     float axisLength, float lateralHalf)
        => gridWidth >= minGridWidth
        && gridHeight >= minGridHeight
        && startGates >= minStartGates
        && endGates >= minEndGates
        && TierSpacing(axisLength) >= RequiredTierSpacing
        && lateralHalf >= minLateralHalf;
}

/// <summary>
/// The archetype catalogue. Adding a shape means adding an entry here — the carver
/// never changes.
///
/// ponytail: plain static data, not a ScriptableObject. Promote to SO only if a
/// designer actually needs to nudge chokepoint positions without a recompile;
/// until then an asset per archetype is setup work that buys nothing.
/// </summary>
public static class TDSpineLibrary
{
    public const string FUNNEL = "PHỄU";
    public const string CASCADE = "THÁC";

    // XƯƠNG SỐNG (a lateral staircase, 3 tiers) lived here and was deleted, not disabled.
    // Its three tiers spanned 40% of the flow axis, giving 3,2 cells between centres where
    // 5,0 are needed — it could never resolve on the fixed-camera 21x9 grid and always fell
    // back to FUNNEL. Keeping unreachable data around only invites someone to "fix" the
    // fallback later. It needs a TALL map; bring it back the day one exists.

    private static readonly Dictionary<string, TDSpineArchetype> k_Archetypes = new()
    {
        // ── PHỄU — two outer nodes converge into one inner node, then the goal ──────
        // Concentrated: one big fight at the inner node. Easiest shape to read, so it
        // belongs on the early stages.
        {
            FUNNEL, new TDSpineArchetype
            {
                name = FUNNEL,
                chokepoints = new[]
                {
                    // v pushed out to 0.15 / 0.85. At 0.30 / 0.70 the two outer nodes were
                    // only 0.4·2·lateralHalf apart, which on a map that is short across the
                    // flow (21x9 gives lateralHalf 2.8) is 2.2 cells — less than their own
                    // width, so they fused and the funnel resolved away to CASCADE.
                    //
                    // u pulled back 0.35 → 0.30. At 0.35 the tiers landed 5.6 cells apart
                    // against 5.5 required — a 0.1-cell margin on a number that moves with
                    // every gate roll, so a slightly shorter axis would silently drop the
                    // whole shape to CASCADE. At 0.30 the margin is ~0.9 cells.
                    new TDChokepointDef(0.30f, 0.15f, tier: 1, width: 3), // 0 — outer south
                    new TDChokepointDef(0.30f, 0.85f, tier: 1, width: 3), // 1 — outer north
                    new TDChokepointDef(0.70f, 0.50f, tier: 2, width: 4), // 2 — inner, the fight
                },
                edges = new[]
                {
                    new TDSpineEdge(0, 2),
                    new TDSpineEdge(1, 2),
                    new TDSpineEdge(2, TDSpineEdge.GOAL),
                },
                minGridWidth = 10,
                minGridHeight = 8,
                minStartGates = 2, // nothing to funnel with a single entry
                minEndGates = 1,
                minLateralHalf = 2.2f,  // two width-3 nodes at v=0.15/0.85 => 3/(2*0.70)
                fallback = CASCADE,
            }
        },

        // ── THÁC — chokepoints in series, each one behind the last ────────────────
        // The clearest fallback line of any shape: lose node 0, retreat to node 1.
        // Friendliest to the morale system, so it fits a high-difficulty stage where
        // the player needs somewhere to give ground.
        {
            CASCADE, new TDSpineArchetype
            {
                name = CASCADE,
                chokepoints = new[]
                {
                    // TWO tiers, not three. Three needed 5 cells between each pair of
                    // centres — 10 cells of axis — and a 21x9 grid only offers 16 once the
                    // 6-cell goal runway (C2) and the gate buffer are taken out. The third
                    // tier did not shrink the others, it overlapped them: measured spacing
                    // was 4,0 against 5,0 required, and the last node sat so close to the
                    // goal that C2 fell to 2,7s.
                    //
                    // The second node is WIDER, and that is measured, not decorative.
                    // Two equal 3-wide nodes dropped the road ratio to 19% (below the 20%
                    // floor) and pressure with it: 97% engaged / 34% over tolerance fell to
                    // 65% / 1%. OpenBlock carves width x width, so a node is 9 cells at
                    // width 3 and 16 at width 4 — dropping a tier costs far more floor than
                    // it looks, and the floor is where melee stands.
                    //
                    // It also reads right for what CASCADE is for: the deeper room is the
                    // last place to make a stand, so it should hold more people than the
                    // one being given up.
                    new TDChokepointDef(0.26f, 0.50f, tier: 1, width: 3),
                    new TDChokepointDef(0.62f, 0.50f, tier: 2, width: 4),
                },
                edges = new[]
                {
                    new TDSpineEdge(0, 1),
                    new TDSpineEdge(1, TDSpineEdge.GOAL),
                },
                minGridWidth = 10,
                minGridHeight = 8,
                minStartGates = 1,
                minEndGates = 1,
                minLateralHalf = 0f,   // single file on the centre line - needs no width
                fallback = null,       // terminal: the least demanding shape there is
            }
        },

    };

    public static TDSpineArchetype Get(string name)
        => k_Archetypes.TryGetValue(name, out var a) ? a : k_Archetypes[FUNNEL];

    public static IEnumerable<string> AllNames => k_Archetypes.Keys;

    /// <summary>
    /// Walks the fallback chain until it finds a shape that fits both the grid size and
    /// the gate counts the stage is configured for. FUNNEL terminates every chain, so
    /// this never returns null — the same "never fail" contract TDMazePathGenerator
    /// already keeps.
    /// </summary>
    public static TDSpineArchetype Resolve(string preferred, int gridWidth, int gridHeight,
                                           int startGates, int endGates,
                                           float axisLength, float lateralHalf)
    {
        var arch = Get(preferred);
        int guard = 0;

        while (!arch.Fits(gridWidth, gridHeight, startGates, endGates, axisLength, lateralHalf)
               && arch.fallback != null && guard++ < 8)
        {
            arch = Get(arch.fallback);
        }

        return arch;
    }
}

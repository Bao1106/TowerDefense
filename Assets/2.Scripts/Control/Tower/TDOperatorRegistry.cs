using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton registry for operators placed on path cells.
///
/// Responsibilities:
/// 1. Track valid operator cells (path cells, excluding gate cells)
/// 2. Track operator positions + block capacity / blocked enemy references
/// 3. CanBlock(cell) → used by TDEnemyView when stepping onto a cell
/// 4. GetBlockedEnemies(cell) → used by TDOperatorView when attacking
/// </summary>
public class TDOperatorRegistry
{
    public static TDOperatorRegistry api;

    /// Off by default: a pack past a full line is a burst of leaks, and a log line each floods
    /// the console a playtester reads. Measurement scripts switch it on when they need "[Leak]".
    public static bool LogLeaks;

    // Valid path cells where operators can be placed (gate cells excluded)
    private readonly HashSet<Vector2Int> m_ValidOperatorCells = new HashSet<Vector2Int>();

    // cell → (blockCapacity, currentBlockedCount)
    private readonly Dictionary<Vector2Int, (int capacity, int count)> m_Operators
        = new Dictionary<Vector2Int, (int, int)>();

    // cell → list of enemies currently blocked at that cell
    private readonly Dictionary<Vector2Int, List<TDEnemyView>> m_BlockedEnemies
        = new Dictionary<Vector2Int, List<TDEnemyView>>();

    // cell → TDOperatorView standing at that cell (so enemies can call TakeDamage on it)
    private readonly Dictionary<Vector2Int, TDOperatorView> m_OperatorViews
        = new Dictionary<Vector2Int, TDOperatorView>();

    // ─── Path cell registration ───────────────────────────────────────────────

    public void RegisterPathCell(Vector2Int cell)
        => m_ValidOperatorCells.Add(cell);

    public bool IsValidOperatorCell(Vector2Int cell)
        => m_ValidOperatorCells.Contains(cell);

    public List<Vector2Int> GetValidOperatorCells()
        => new List<Vector2Int>(m_ValidOperatorCells);

    // ─── Operator management ──────────────────────────────────────────────────

    public void RegisterOperator(Vector2Int cell, int blockCapacity)
    {
        m_Operators[cell] = (Mathf.Max(1, blockCapacity), 0);
        m_BlockedEnemies[cell] = new List<TDEnemyView>();
        Debug.Log($"[OperatorRegistry] Operator registered at {cell}, blockCapacity={blockCapacity}");
    }

    /// Called from TDOperatorView.Init() to store the view reference (used so enemies can counter-attack).
    public void RegisterOperatorView(Vector2Int cell, TDOperatorView view)
        => m_OperatorViews[cell] = view;

    /// Returns the TDOperatorView at the given cell (null if none is registered).
    public TDOperatorView GetOperatorView(Vector2Int cell)
        => m_OperatorViews.TryGetValue(cell, out var v) ? v : null;

    /// Unregisters the operator — automatically force-unblocks all enemies currently held at that cell.
    public void UnregisterOperator(Vector2Int cell)
    {
        ReleaseBlockedEnemies(cell);
        m_Operators.Remove(cell);
        m_BlockedEnemies.Remove(cell);
        m_OperatorViews.Remove(cell);
    }

    /// <summary>
    /// Everyone this cell was holding walks free, while the cell keeps its operator (§06 collapse).
    ///
    /// Deliberately the same release path death uses: an enemy must not care WHY the wall in
    /// front of it stopped being a wall. The count has to be zeroed by hand here because
    /// ForceUnblock() does not call OnEnemyUnblocked — with the entry surviving (unlike death,
    /// which removes it), a stale count would keep the cell permanently "full".
    /// </summary>
    public void ReleaseBlockedEnemies(Vector2Int cell)
    {
        if (!m_BlockedEnemies.TryGetValue(cell, out var list)) return;

        // Snapshot to avoid modifying the list while iterating
        var snapshot = new List<TDEnemyView>(list);
        list.Clear();

        if (m_Operators.TryGetValue(cell, out var info))
            m_Operators[cell] = (info.capacity, 0);

        foreach (var enemy in snapshot)
            enemy?.ForceUnblock();
    }

    public bool HasOperatorAt(Vector2Int cell)
        => m_Operators.ContainsKey(cell);

    // ─── Blocking state ───────────────────────────────────────────────────────

    /// True if the cell has a registered operator AND still has available block slots.
    public bool CanBlock(Vector2Int cell)
    {
        if (!m_Operators.TryGetValue(cell, out var info)) return false;

        // A collapsed operator is not a wall (§06). They are still standing on the cell — still
        // targetable, still occupying it, still someone the player has to deal with — but enemies
        // walk straight through. Read live from morale rather than latched into a flag, because
        // a broken operator left alone recovers on their own and must start holding again.
        if (IsBrokenAt(cell)) return false;

        return info.count < info.capacity;
    }

    /// <summary>
    /// Collapsed operators standing next to `cell` — the rescue candidates for whoever is there.
    /// Chebyshev adjacency, matching how the pressure zone and the N3 spike already measure
    /// "next to", so a player who has learned one range has learned all three.
    /// </summary>
    public List<TDOperatorView> GetRescueTargetsAround(Vector2Int cell)
    {
        var found = new List<TDOperatorView>();

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var view = GetOperatorView(new Vector2Int(cell.x + dx, cell.y + dy));
                if (view != null && view.IsCollapsed) found.Add(view);
            }

        return found;
    }

    /// <summary>A standing melee at `cell` with every block slot taken — the next arrival leaks.</summary>
    public bool IsFullAt(Vector2Int cell)
        => m_Operators.TryGetValue(cell, out var info) && info.count >= info.capacity && !IsBrokenAt(cell);

    /// <summary>True if the operator standing at `cell` has collapsed.</summary>
    public bool IsBrokenAt(Vector2Int cell)
        => m_OperatorViews.TryGetValue(cell, out var view)
        && view != null && view.Morale != null && view.Morale.IsBroken;

    /// An enemy starts being blocked — increments the counter and stores the reference.
    public void OnEnemyBlocked(Vector2Int cell, TDEnemyView enemy)
    {
        if (m_Operators.TryGetValue(cell, out var info))
            m_Operators[cell] = (info.capacity, info.count + 1);

        if (!m_BlockedEnemies.TryGetValue(cell, out var list))
            m_BlockedEnemies[cell] = list = new List<TDEnemyView>();
        if (enemy != null && !list.Contains(enemy))
            list.Add(enemy);
    }

    /// An enemy leaves the cell (killed or returned to pool) — decrements the counter and removes the reference.
    public void OnEnemyUnblocked(Vector2Int cell, TDEnemyView enemy)
    {
        if (m_Operators.TryGetValue(cell, out var info))
            m_Operators[cell] = (info.capacity, Mathf.Max(0, info.count - 1));

        if (m_BlockedEnemies.TryGetValue(cell, out var list))
            list.Remove(enemy);
    }

    /// Returns a snapshot of all enemies currently blocked at the given cell (never null, may be empty).
    public List<TDEnemyView> GetBlockedEnemies(Vector2Int cell)
    {
        if (m_BlockedEnemies.TryGetValue(cell, out var list))
            return new List<TDEnemyView>(list); // snapshot to avoid modifying while iterating
        return new List<TDEnemyView>();
    }

    // ─── Morale (§04 N3, §05 wave relief) ─────────────────────────────────────
    //
    // These live here because the registry is the only thing that knows where everyone is
    // standing. Putting them anywhere else would mean a second place tracking operator
    // positions, and this project has already paid for "two sources of truth" more than once.

    /// <summary>True if any operator broke during the current wave — lowers Resolve (§07).</summary>
    public bool AnyBrokeThisWave { get; private set; }

    /// <summary>Number of Calm allies orthogonally or diagonally adjacent to `cell`.</summary>
    public int CountCalmAlliesAdjacent(Vector2Int cell)
    {
        int n = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var view = GetOperatorView(new Vector2Int(cell.x + dx, cell.y + dy));
                if (view != null && view.Morale != null && view.Morale.State == TDEnums.MoraleState.Calm) n++;
            }
        return n;
    }

    /// <summary>
    /// N3 — an ally died or broke at `origin`. Everyone within the spike radius takes it,
    /// the casualty excluded. Distance is Chebyshev, matching how the pressure zone counts.
    /// </summary>
    public void BroadcastSpike(Vector2Int origin, float amount, bool wasBreak)
    {
        if (wasBreak) AnyBrokeThisWave = true;

        int r = Mathf.CeilToInt(TDConstant.STRESS_SPIKE_RADIUS);
        foreach (var kv in m_OperatorViews)
        {
            var cell = kv.Key;
            if (cell == origin) continue;
            if (Mathf.Max(Mathf.Abs(cell.x - origin.x), Mathf.Abs(cell.y - origin.y)) > r) continue;
            kv.Value?.Morale?.AddSpike(amount);
        }
    }

    /// <summary>
    /// An enemy walked through `cell` past a full or collapsed melee (spec §5.2). The melee there,
    /// if still standing, and every standing ranged operator whose current range covers the cell
    /// split the leak by TDLeakShare. Turrets have no morale and never take a share.
    /// </summary>
    public void ReportLeak(Vector2Int cell, TDEnums.EnemyType leaker)
    {
        TDPressureProbe.RecordLeak(); // every leak counts, including one nobody is left to feel

        var melee = GetOperatorView(cell);
        bool meleeStanding = melee != null && !melee.IsCollapsed;

        var ranged = new List<TDOperatorView>();
        foreach (var view in m_OperatorViews.Values)
            if (view != null && view.Data != null && view.Data.deployZone == TDEnums.DeployZone.TowerZone
                && !view.IsCollapsed && view.Covers(cell))
                ranged.Add(view);

        var (meleeShare, eachRanged) = TDLeakShare.Split(meleeStanding, ranged.Count);
        float amplifier = LeakAmplifierAt(cell, leaker);

        if (!LogLeaks)
        {
            if (meleeStanding) melee.ReceiveLeak(meleeShare, amplifier);
            foreach (var r in ranged) r.ReceiveLeak(eachRanged, amplifier);
            return;
        }

        var parts = new List<string>();
        if (meleeStanding) parts.Add($"{melee.Data.operatorName} +{melee.ReceiveLeak(meleeShare, amplifier):F1}");
        foreach (var r in ranged) parts.Add($"{r.Data.operatorName} +{r.ReceiveLeak(eachRanged, amplifier):F1}");
        Debug.Log($"[Leak] {cell} → {(parts.Count > 0 ? string.Join(", ", parts) : "nobody")}");
    }

    // The live Heralds' cells, from the enemy registry — a dying enemy leaves it at once, so a
    // Herald stops amplifying the moment it is killed.
    private float LeakAmplifierAt(Vector2Int cell, TDEnums.EnemyType leaker)
    {
        var heralds = new List<Vector2Int>();
        var enemies = TDEnemyRegistry.api?.GetAll();
        if (enemies != null && TDGridMainModel.api != null)
            foreach (var e in enemies)
                if (e != null && e.EnemyType == TDEnums.EnemyType.Herald)
                    heralds.Add(TDGridMainModel.api.WorldToCell(e.transform.position));
        return LeakAmplifier(leaker, cell, heralds);
    }

    /// <summary>
    /// Spec §5.5: HERALD_LEAK_MULT when any Herald stands within HERALD_RADIUS cells (Euclid) of
    /// the leak, otherwise 1. Two Heralds in range amplify no more than one — it does not stack.
    /// A Horde body is never amplified: the pack is already the overflow threat, and amplified it
    /// broke a lone melee from the top of Calm with one pack (spec §7.2 forced case).
    /// </summary>
    public static float LeakAmplifier(TDEnums.EnemyType leaker, Vector2Int leakCell, IReadOnlyList<Vector2Int> heraldCells)
    {
        if (leaker == TDEnums.EnemyType.Horde) return 1f;
        float r2 = TDConstant.HERALD_RADIUS * TDConstant.HERALD_RADIUS;
        foreach (var h in heraldCells)
            if ((h - leakCell).sqrMagnitude <= r2) return TDConstant.HERALD_LEAK_MULT;
        return 1f;
    }

    /// <summary>
    /// A wave finished. Everyone still deployed steadies a little — but only if nobody
    /// broke, which is what makes holding the line without casualties worth something
    /// beyond simply surviving it.
    /// </summary>
    public void OnWaveCleared()
    {
        // No squad-wide gate any more. Morale.OnWaveCleared() skips anyone who is collapsed, so
        // the relief reaches exactly the operators who held — which is what makes it a valve
        // instead of a bonus that switches off the moment it is needed.
        //
        // AnyBrokeThisWave survives because §07 still reads it: a teammate breaking lowers
        // everyone's Resolve for the rest of the wave. That is a separate rule from recovery.
        foreach (var kv in m_OperatorViews)
            kv.Value?.Morale?.OnWaveCleared();

        AnyBrokeThisWave = false;
    }
}

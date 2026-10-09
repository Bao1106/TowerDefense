using System.Collections.Generic;
using System.Linq;
using TDEnums;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Calibration bot (spec 2026-10-06 §7.2): buys TDLoadModel.ReferenceTeam in order whenever
/// gold and the deploy cap allow, on cells picked by fixed rules, and never retreats. A player
/// with no tactics on purpose — the leaks it suffers measure the load, not the skill — so its
/// runs can calibrate TDLoadModel.EfficiencyOf. Read the result from TDPressureProbe.LastReport.
///
/// Cells:
/// - melee anchor: the valid path cell most corridors run through; ties go to the one nearer
///   the goal. Later melee take the next valid cells on that corridor toward the goal, then,
///   once those run out, the cells behind the anchor.
/// - ranged: the tower-zone cell and facing whose range covers the most bot melee cells.
/// </summary>
public static class TDCalibrationBot
{
    private const double TICK_SECONDS = 0.5; // real time, so a sped-up match is still watched often enough

    private static readonly Quaternion[] k_Facings =
        { Quaternion.Euler(0f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 180f, 0f), Quaternion.Euler(0f, 270f, 0f) };

    private static int s_MaxUnits;
    private static double s_NextTick;
    private static IReadOnlyList<TDPathGroup> s_PlannedFor; // a new list means a new match
    private static List<IGridCellDTO> s_Corridor;
    private static List<Vector2Int> s_MeleeLine;

    [MenuItem("Tools/TD/Calibration Bot/Start Full Team")] public static void StartFull() => Start(int.MaxValue);
    [MenuItem("Tools/TD/Calibration Bot/Start Three")] public static void StartThree() => Start(3);

    [MenuItem("Tools/TD/Calibration Bot/Stop")]
    public static void Stop()
    {
        EditorApplication.update -= Tick;
        Debug.Log("[Bot] stopped");
    }

    public static void Start(int maxUnits)
    {
        s_MaxUnits = maxUnits;
        s_PlannedFor = null;
        s_NextTick = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log($"[Bot] started — team of {Mathf.Min(maxUnits, TDLoadModel.ReferenceTeam.Length)}");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Stop(); return; }
        if (EditorApplication.timeSinceStartup < s_NextTick) return;
        s_NextTick = EditorApplication.timeSinceStartup + TICK_SECONDS;

        var groups = TDEnemyPathMainControl.api?.Groups;
        if (groups == null || TDOperatorRegistry.api == null || TDGoldControl.api == null
            || TDDeployCap.api == null || TDOperatorRoster.api == null) return;
        if (TDGameStateControl.api != null && TDGameStateControl.api.IsGameEnded) return;
        if (!ReferenceEquals(groups, s_PlannedFor) && !Plan(groups)) return;
        if (TDDeployCap.api.IsFull) return;

        var roster = TDFlyweightOperatorDataSettings.api?.GetAllOperators();
        if (roster == null) return;

        int team = Mathf.Min(s_MaxUnits, TDLoadModel.ReferenceTeam.Length);
        for (int k = 0; k < team; k++)
        {
            var op = roster.Find(o => o.operatorName == TDLoadModel.ReferenceTeam[k]);
            if (op == null || !TDOperatorRoster.api.CanDeploy(op)) continue; // on the field, or cooling down
            if (TDGoldControl.api.Gold < op.cost) return; // strictly in order: save up for this one
            Deploy(op);
            return; // one purchase per tick
        }
    }

    private static bool Plan(IReadOnlyList<TDPathGroup> groups)
    {
        var valid = new HashSet<Vector2Int>(TDOperatorRegistry.api.GetValidOperatorCells());
        var through = new Dictionary<Vector2Int, int>();
        foreach (var g in groups)
            foreach (var corridor in g.Corridors)
                foreach (var c in corridor.Select(x => x.position).Distinct())
                    if (valid.Contains(c)) through[c] = through.TryGetValue(c, out int n) ? n + 1 : 1;
        if (through.Count == 0) return false; // map not ready yet

        var goals = groups.Select(g => g.EndCell).ToList();
        float ToGoal(Vector2Int c) => goals.Min(e => Vector2Int.Distance(c, e));
        var anchor = through.OrderByDescending(kv => kv.Value).ThenBy(kv => ToGoal(kv.Key)).First().Key;

        s_Corridor = groups.SelectMany(g => g.Corridors).First(cr => cr.Any(x => x.position == anchor));
        int at = s_Corridor.FindIndex(x => x.position == anchor);
        var onward = s_Corridor.Skip(at).Select(x => x.position);
        var behind = s_Corridor.Take(at).Select(x => x.position).Reverse();
        s_MeleeLine = onward.Concat(behind).Where(valid.Contains).Distinct().ToList();
        s_PlannedFor = groups;

        Debug.Log($"[Bot] plan: anchor {anchor} ({through[anchor]} corridors), melee line {string.Join(" ", s_MeleeLine)}");
        return true;
    }

    private static void Deploy(OperatorData op)
    {
        var grid = TDGridMainModel.api;
        var behavior = TDControl.CreateOperatorBehavior(op.deployZone);
        var range = new TDOffsetRangeDTO(op.rangeOffsets);

        Vector2Int? best = null;
        Quaternion facing = Quaternion.identity;
        float bestScore = float.MinValue;

        if (op.deployZone == DeployZone.PathCell)
        {
            // First free cell on the line; face whichever way covers the most of the corridor.
            var corridor = new HashSet<Vector2Int>(s_Corridor.Select(x => x.position));
            foreach (var c in s_MeleeLine)
            {
                if (!behavior.CanPlace(grid.CellToWorld(c))) continue;
                best = c;
                foreach (var f in k_Facings)
                {
                    int score = range.GetCellsInRange(c, f).Count(corridor.Contains);
                    if (score > bestScore) { bestScore = score; facing = f; }
                }
                break;
            }
        }
        else
        {
            // Cover the most bot melee; ties go to the cell nearer the anchor.
            var melee = s_MeleeLine.Where(TDOperatorRegistry.api.HasOperatorAt).ToHashSet();
            var anchor = s_MeleeLine[0];
            for (int x = 0; x < grid.width; x++)
                for (int y = 0; y < grid.height; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (!grid.IsInTowerZone(c) || !behavior.CanPlace(grid.CellToWorld(c))) continue;
                    foreach (var f in k_Facings)
                    {
                        float score = range.GetCellsInRange(c, f).Count(melee.Contains) - 0.001f * Vector2Int.Distance(c, anchor);
                        if (score > bestScore) { bestScore = score; best = c; facing = f; }
                    }
                }
        }

        if (best == null) { Debug.Log($"[Bot] no cell left for {op.operatorName}"); return; }
        if (!TDGoldControl.api.SpendGold(op.cost)) return;

        var slot = new TDTowerSlotInfo
        {
            prefab = op.operatorPrefab, towerType = TowerType.Operator, operatorData = op, cost = op.cost, icon = op.icon,
        };
        behavior.Place(grid.CellToWorld(best.Value), facing, slot);
        Debug.Log($"[Bot] {op.operatorName} @{best.Value} facing {facing.eulerAngles.y:F0} (score {bestScore:F2}), gold left {TDGoldControl.api.Gold}");
    }
}

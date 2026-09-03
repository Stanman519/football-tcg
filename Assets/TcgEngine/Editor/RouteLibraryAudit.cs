using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.Gameplay;

/// <summary>
/// Editor tool: enumerates all valid (position, OL slot, context, direction) combos
/// and reports which ones have RouteLibrary coverage vs gaps.
/// Menu: First&Long > Audit Route Library Coverage
/// </summary>
public class RouteLibraryAudit
{
    [MenuItem("First&Long/Audit Route Library Coverage")]
    static void AuditCoverage()
    {
        RouteLibrary.Initialize();

        int total = 0, covered = 0, missing = 0;
        var gaps = new List<string>();
        var hits = new List<string>();

        // Non-OL positions
        var nonOL = new (PlayerPositionGrp pos, RouteContext[] contexts)[]
        {
            (PlayerPositionGrp.QB, new[] { RouteContext.QBDropShort, RouteContext.QBDropDeep, RouteContext.QBRush }),
            (PlayerPositionGrp.WR, new[] { RouteContext.Receiving, RouteContext.RunBlock }),
            (PlayerPositionGrp.RB_TE, new[] { RouteContext.RunCarry, RouteContext.PassBlockShort, RouteContext.PassBlockDeep, RouteContext.Receiving }),
        };

        int[] directions = { -1, 1 };

        foreach (var entry in nonOL)
        {
            foreach (var ctx in entry.contexts)
            {
                foreach (int dir in directions)
                {
                    total++;
                    var route = RouteLibrary.Pick(entry.pos, -1, ctx, dir);
                    string label = $"{entry.pos}  ctx={ctx}  dir={dir}";
                    if (route != null)
                    {
                        covered++;
                        hits.Add($"  OK  {label} -> {route.name}");
                    }
                    else
                    {
                        missing++;
                        gaps.Add($"  GAP {label}");
                    }
                }
            }
        }

        // OL positions (0-4: LT, LG, C, RG, RT)
        var olContexts = new[] { RouteContext.RunBlock, RouteContext.PassBlockShort, RouteContext.PassBlockDeep };
        string[] olNames = { "LT", "LG", "C", "RG", "RT" };

        for (int olSlot = 0; olSlot < 5; olSlot++)
        {
            foreach (var ctx in olContexts)
            {
                foreach (int dir in directions)
                {
                    total++;
                    var route = RouteLibrary.Pick(PlayerPositionGrp.OL, olSlot, ctx, dir);
                    string label = $"OL[{olNames[olSlot]}]  ctx={ctx}  dir={dir}";
                    if (route != null)
                    {
                        covered++;
                        hits.Add($"  OK  {label} -> {route.name}");
                    }
                    else
                    {
                        missing++;
                        gaps.Add($"  GAP {label}");
                    }
                }
            }
        }

        // Report
        Debug.Log($"=== RouteLibrary Audit: {covered}/{total} covered, {missing} gaps ===");
        foreach (var h in hits) Debug.Log(h);
        if (gaps.Count > 0)
        {
            Debug.LogWarning($"--- {gaps.Count} GAPS ---");
            foreach (var g in gaps) Debug.LogWarning(g);
        }
        else
        {
            Debug.Log("No gaps found — full coverage!");
        }
    }
}

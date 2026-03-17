using System.Collections.Generic;
using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace TcgEngine.Client
{
    /// <summary>
    /// OL sub-positions mapped to slot indices in the standard formation.
    /// </summary>
    public enum OLSubPosition { LT = 0, LG = 1, C = 2, RG = 3, RT = 4 }

    /// <summary>
    /// Context bucket for route selection — derived from play type + player role.
    /// </summary>
    public enum RouteContext
    {
        RunBlock,         // OL/WR/TE blocking on run plays
        RunCarry,         // RB dive/gap routes on run plays
        PassBlockShort,   // OL/RB pass protection on short pass
        PassBlockDeep,    // OL/RB pass protection on deep pass
        Receiving,        // WR/TE/RB receiving routes (non-target, for visual variety)
        QBDropShort,      // QB short dropback
        QBDropDeep,       // QB deep dropback
        QBRush,           // QB rush/scramble
    }

    /// <summary>
    /// Loads all 57 hand-authored RouteData assets from Resources/Routes/,
    /// indexes them by (position, OL sub-pos, context, lateral direction),
    /// and picks routes dynamically per-slot.
    /// </summary>
    public static class RouteLibrary
    {
        // Key: (posGroup, olSubPos (-1 if N/A), context, direction (0=any, -1=left, 1=right))
        private static Dictionary<(PlayerPositionGrp, int, RouteContext, int), List<RouteData>> index;
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized) return;
            index = new Dictionary<(PlayerPositionGrp, int, RouteContext, int), List<RouteData>>();

            // Load from TcgEngine/Resources path — Unity strips "Assets/TcgEngine/Resources/" prefix
            var allRoutes = Resources.LoadAll<RouteData>("Routes");
            foreach (var route in allRoutes)
                IndexRoute(route);

            initialized = true;
            Debug.Log($"[RouteLibrary] Loaded {allRoutes.Length} routes into {index.Count} buckets");
        }

        /// <summary>
        /// Pick a random matching route, or null if none found.
        /// olSlotIndex: 0-4 for OL, -1 for non-OL positions.
        /// lateralDir: -1=left, 1=right, 0=any.
        /// </summary>
        public static RouteData Pick(PlayerPositionGrp pos, int olSlotIndex, RouteContext ctx, int lateralDir)
        {
            if (!initialized) Initialize();

            int olSub = (pos == PlayerPositionGrp.OL) ? olSlotIndex : -1;

            // Try exact direction match first
            if (lateralDir != 0)
            {
                var exact = Lookup(pos, olSub, ctx, lateralDir);
                if (exact != null && exact.Count > 0)
                    return exact[Random.Range(0, exact.Count)];
            }

            // Try direction-agnostic (0)
            var any = Lookup(pos, olSub, ctx, 0);
            if (any != null && any.Count > 0)
                return any[Random.Range(0, any.Count)];

            // Try opposite direction as last resort
            if (lateralDir != 0)
            {
                var opp = Lookup(pos, olSub, ctx, -lateralDir);
                if (opp != null && opp.Count > 0)
                    return opp[Random.Range(0, opp.Count)];
            }

            return null;
        }

        private static List<RouteData> Lookup(PlayerPositionGrp pos, int olSub, RouteContext ctx, int dir)
        {
            var key = (pos, olSub, ctx, dir);
            index.TryGetValue(key, out var list);
            return list;
        }

        private static void Add(PlayerPositionGrp pos, int olSub, RouteContext ctx, int dir, RouteData route)
        {
            var key = (pos, olSub, ctx, dir);
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<RouteData>();
                index[key] = list;
            }
            list.Add(route);
        }

        // ── Name Parsing ──────────────────────────────────────────

        private static void IndexRoute(RouteData route)
        {
            string name = route.name; // e.g. "LG_rush_1", "WR_slant_L", "TE_pass_B_R"
            string lower = name.ToLowerInvariant();
            string[] parts = lower.Split('_');
            if (parts.Length < 2) return;

            string posToken = parts[0];

            // Parse position + OL sub-position
            PlayerPositionGrp posGroup;
            int olSub = -1;

            switch (posToken)
            {
                case "qb": posGroup = PlayerPositionGrp.QB; break;
                case "wr": posGroup = PlayerPositionGrp.WR; break;
                case "rb": posGroup = PlayerPositionGrp.RB_TE; break;
                case "te": posGroup = PlayerPositionGrp.RB_TE; break;
                case "c":  posGroup = PlayerPositionGrp.OL; olSub = (int)OLSubPosition.C; break;
                case "lg": posGroup = PlayerPositionGrp.OL; olSub = (int)OLSubPosition.LG; break;
                case "rg": posGroup = PlayerPositionGrp.OL; olSub = (int)OLSubPosition.RG; break;
                case "lt": posGroup = PlayerPositionGrp.OL; olSub = (int)OLSubPosition.LT; break;
                case "rt": posGroup = PlayerPositionGrp.OL; olSub = (int)OLSubPosition.RT; break;
                default: return; // unknown position
            }

            // Parse context + direction from remaining tokens
            string rest = lower.Substring(posToken.Length + 1); // everything after "LG_"
            RouteContext ctx = ParseContext(posToken, rest);
            int dir = ParseDirection(rest);

            Add(posGroup, olSub, ctx, dir, route);
        }

        private static RouteContext ParseContext(string posToken, string rest)
        {
            // QB-specific
            if (posToken == "qb")
            {
                if (rest.Contains("rush")) return RouteContext.QBRush;
                if (rest.Contains("deep")) return RouteContext.QBDropDeep;
                if (rest.Contains("short")) return RouteContext.QBDropShort;
                return RouteContext.QBDropShort;
            }

            // Blocking routes (run or pass protection)
            if (rest.Contains("run_b") || rest.Contains("run_B"))
                return RouteContext.RunBlock;
            if (rest.Contains("pass_b") || rest.Contains("pass_B"))
            {
                // Distinguish short vs deep pass block if possible
                if (rest.Contains("deep")) return RouteContext.PassBlockDeep;
                // Default pass block = short
                return RouteContext.PassBlockShort;
            }

            // OL rush/run blocking
            if (posToken == "c" || posToken == "lg" || posToken == "rg" || posToken == "lt" || posToken == "rt")
            {
                if (rest.Contains("rush")) return RouteContext.RunBlock;
                if (rest.Contains("deep")) return RouteContext.PassBlockDeep;
                if (rest.Contains("short")) return RouteContext.PassBlockShort;
                return RouteContext.RunBlock; // fallback
            }

            // RB carry routes (dive, gap-based like RG/LG/RT/LT names)
            if (posToken == "rb")
            {
                if (rest.Contains("dive")) return RouteContext.RunCarry;
                if (rest.Contains("pass")) return RouteContext.PassBlockShort;
                // RB_RG, RB_LG, etc. = gap-based run carries
                if (rest == "rg" || rest == "lg" || rest == "rt" || rest == "lt")
                    return RouteContext.RunCarry;
                if (rest.Contains("flare")) return RouteContext.Receiving;
                return RouteContext.RunCarry;
            }

            // WR/TE receiving route names
            if (posToken == "wr" || posToken == "te")
            {
                // Receiving routes: slant, out, curl, go, post, deep_out, hook, flare
                string[] recvPatterns = { "slant", "out", "curl", "go", "post", "deep_out", "hook", "flare", "seam" };
                foreach (var p in recvPatterns)
                {
                    if (rest.Contains(p)) return RouteContext.Receiving;
                }
                // TE/WR blocking
                if (rest.Contains("block") || rest.Contains("run_b"))
                    return RouteContext.RunBlock;
            }

            return RouteContext.RunBlock; // fallback
        }

        private static int ParseDirection(string rest)
        {
            // Check for trailing _L or _R (ignoring variant numbers)
            // e.g. "slant_l" → -1, "rush_r" → 1, "rush_1" → 0
            string[] parts = rest.Split('_');
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (parts[i] == "l" || parts[i] == "left") return -1;
                if (parts[i] == "r" || parts[i] == "right") return 1;
            }
            return 0; // no direction specified
        }

        // ── Convenience: determine RouteContext from game state ──

        /// <summary>
        /// Determine the appropriate RouteContext for a non-carrier, non-target slot.
        /// </summary>
        public static RouteContext GetContext(PlayerPositionGrp posGroup, PlayType playType, bool isTE)
        {
            bool isRun = (playType == PlayType.Run);
            bool isDeep = (playType == PlayType.LongPass);

            if (posGroup == PlayerPositionGrp.QB)
            {
                if (isRun) return RouteContext.QBRush;
                return isDeep ? RouteContext.QBDropDeep : RouteContext.QBDropShort;
            }

            if (posGroup == PlayerPositionGrp.OL)
            {
                if (isRun) return RouteContext.RunBlock;
                return isDeep ? RouteContext.PassBlockDeep : RouteContext.PassBlockShort;
            }

            if (isRun)
            {
                // WR run-blocks, RB that isn't carrying also blocks
                if (posGroup == PlayerPositionGrp.WR) return RouteContext.RunBlock;
                if (posGroup == PlayerPositionGrp.RB_TE)
                    return isTE ? RouteContext.RunBlock : RouteContext.RunCarry;
                return RouteContext.RunBlock;
            }

            // Pass play — WR/TE/RB run receiving routes (non-target, just for visuals)
            if (posGroup == PlayerPositionGrp.WR || posGroup == PlayerPositionGrp.RB_TE)
                return RouteContext.Receiving;

            return RouteContext.RunBlock; // fallback
        }

        /// <summary>
        /// Determine lateral direction from a slot's field position relative to center.
        /// Negative x = left side → -1, positive x = right side → 1.
        /// </summary>
        public static int LateralFromSlot(BoardSlot slot, float fieldWidth)
        {
            if (slot == null) return 0;
            float xPos = slot.transform.localPosition.x;
            float center = 0f; // assume field is centered at 0
            return xPos < center ? -1 : 1;
        }
    }
}

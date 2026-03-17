using System;
using System.Collections.Generic;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    public enum RouteShape
    {
        // Receiver routes
        Slant,
        Out,
        Curl,
        Hitch,
        Go,
        Post,
        Corner,
        Flat,
        Drag,
        Seam,
        Screen,
        Swing,
        Wheel,
        Hook,
        DeepOut,
        Checkdown,

        // Non-receiver
        Block,
        DropBack,
        Scramble,

        // Run play
        RunGap,
    }

    /// <summary>
    /// Static data about each route shape: vertex percentage and lateral direction.
    /// Vertex = the break point where the receiver changes direction.
    /// Catchable zone is from vertex to end of route.
    /// </summary>
    public static class RouteShapeData
    {
        /// <summary>
        /// Vertex as fraction of total route depth (0.0 = catch anywhere, 1.0 = catch only at end).
        /// </summary>
        public static float VertexFraction(RouteShape shape) => shape switch
        {
            RouteShape.Hitch    => 1.00f,
            RouteShape.Go       => 0.00f,
            RouteShape.Slant    => 0.20f,
            RouteShape.Out      => 0.60f,
            RouteShape.Curl     => 0.70f,
            RouteShape.Post     => 0.55f,
            RouteShape.Corner   => 0.55f,
            RouteShape.Flat     => 0.10f,
            RouteShape.Seam     => 0.00f,
            RouteShape.Screen   => 0.00f,
            RouteShape.Drag     => 0.15f,
            RouteShape.Swing    => 0.10f,
            RouteShape.Wheel    => 0.30f,
            RouteShape.Hook     => 0.70f,
            RouteShape.DeepOut  => 0.60f,
            RouteShape.Checkdown=> 0.10f,
            _                   => 0.50f,
        };

        /// <summary>
        /// Base depth in yards for this route shape (scaled by player bonus).
        /// </summary>
        public static float BaseDepth(RouteShape shape) => shape switch
        {
            RouteShape.Screen   => -2f,
            RouteShape.Flat     => 2f,
            RouteShape.Hitch    => 5f,
            RouteShape.Checkdown=> 4f,
            RouteShape.Slant    => 8f,
            RouteShape.Drag     => 6f,
            RouteShape.Hook     => 10f,
            RouteShape.Curl     => 12f,
            RouteShape.Out      => 10f,
            RouteShape.Swing    => 5f,
            RouteShape.DeepOut  => 18f,
            RouteShape.Seam     => 20f,
            RouteShape.Post     => 20f,
            RouteShape.Corner   => 20f,
            RouteShape.Go       => 30f,
            RouteShape.Wheel    => 18f,
            RouteShape.DropBack => -7f,
            RouteShape.Block    => 1f,
            RouteShape.Scramble => 0f,
            RouteShape.RunGap   => 0f,
            _                   => 10f,
        };

        // ── Route pools per (positionGroup, playType) ──────────────

        private static readonly Dictionary<(PlayerPositionGrp, PlayType), RouteShape[]> Pools = new()
        {
            // WR
            { (PlayerPositionGrp.WR, PlayType.ShortPass), new[] { RouteShape.Slant, RouteShape.Out, RouteShape.Curl, RouteShape.Hitch, RouteShape.Drag } },
            { (PlayerPositionGrp.WR, PlayType.LongPass),  new[] { RouteShape.Post, RouteShape.Go, RouteShape.Corner, RouteShape.DeepOut } },
            { (PlayerPositionGrp.WR, PlayType.Run),       new[] { RouteShape.Block } },

            // RB_TE
            { (PlayerPositionGrp.RB_TE, PlayType.ShortPass), new[] { RouteShape.Swing, RouteShape.Screen, RouteShape.Flat, RouteShape.Checkdown } },
            { (PlayerPositionGrp.RB_TE, PlayType.LongPass),  new[] { RouteShape.Swing, RouteShape.Wheel, RouteShape.Seam } },
            { (PlayerPositionGrp.RB_TE, PlayType.Run),       new[] { RouteShape.RunGap } },

            // OL — always block
            { (PlayerPositionGrp.OL, PlayType.ShortPass), new[] { RouteShape.Block } },
            { (PlayerPositionGrp.OL, PlayType.LongPass),  new[] { RouteShape.Block } },
            { (PlayerPositionGrp.OL, PlayType.Run),       new[] { RouteShape.Block } },

            // QB
            { (PlayerPositionGrp.QB, PlayType.ShortPass), new[] { RouteShape.DropBack } },
            { (PlayerPositionGrp.QB, PlayType.LongPass),  new[] { RouteShape.DropBack } },
            { (PlayerPositionGrp.QB, PlayType.Run),       new[] { RouteShape.DropBack } },
        };

        /// <summary>
        /// Get the route shape pool for a position group + play type.
        /// Returns null if no pool defined (defense, special teams, etc).
        /// </summary>
        public static RouteShape[] GetPool(PlayerPositionGrp posGroup, PlayType playType)
        {
            Pools.TryGetValue((posGroup, playType), out var pool);
            return pool;
        }

        /// <summary>
        /// Pick a random route from the pool. Returns null if no pool.
        /// </summary>
        public static RouteShape? PickRandom(PlayerPositionGrp posGroup, PlayType playType, System.Random rng = null)
        {
            var pool = GetPool(posGroup, playType);
            if (pool == null || pool.Length == 0) return null;
            int idx = rng != null ? rng.Next(pool.Length) : UnityEngine.Random.Range(0, pool.Length);
            return pool[idx];
        }

        /// <summary>
        /// Pick the best fitting shorter route for a hot-route audible.
        /// Returns a shape whose vertex depth fits within netYardage.
        /// </summary>
        public static RouteShape? PickHotRoute(PlayerPositionGrp posGroup, PlayType playType, float netYardage, System.Random rng = null)
        {
            var pool = GetPool(posGroup, playType);
            if (pool == null) return null;

            // Collect shapes whose vertex fits within netYardage
            var candidates = new List<RouteShape>();
            foreach (var shape in pool)
            {
                float depth = BaseDepth(shape);
                float vertex = depth * VertexFraction(shape);
                if (vertex <= netYardage && depth > 0)
                    candidates.Add(shape);
            }

            // If nothing fits, try the shortest available
            if (candidates.Count == 0)
            {
                RouteShape shortest = pool[0];
                float shortestVertex = float.MaxValue;
                foreach (var shape in pool)
                {
                    float v = BaseDepth(shape) * VertexFraction(shape);
                    if (v < shortestVertex) { shortestVertex = v; shortest = shape; }
                }
                return shortest;
            }

            int idx = rng != null ? rng.Next(candidates.Count) : UnityEngine.Random.Range(0, candidates.Count);
            return candidates[idx];
        }
    }
}

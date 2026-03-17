using System.Collections.Generic;
using Assets.TcgEngine.Scripts.Gameplay;
using DG.Tweening;
using TcgEngine;
using TcgEngine.Client;
using UnityEngine;

/// <summary>
/// Generates movement segments procedurally from a RouteShape + depth + start position.
/// Replaces hand-authored RouteData for post-snap movement.
/// All coordinates are in football coords (xFraction, yardsFromLOS).
/// </summary>
public static class ProceduralRouteGenerator
{
    /// <summary>
    /// Result of route generation — segments to enqueue plus metadata for audible checks.
    /// </summary>
    public struct GeneratedRoute
    {
        public List<SlotMovementSegment> segments;
        public RouteShape shape;
        public float totalDepthYards;
        public float vertexDepthYards;
        public bool wasHotRouted;
    }

    /// <summary>
    /// Generate a full route for one player slot.
    /// </summary>
    /// <param name="shape">Which route pattern to run</param>
    /// <param name="depthYards">How deep the route goes (positive = downfield)</param>
    /// <param name="lateralSign">-1 = break left, +1 = break right, 0 = random</param>
    /// <param name="fieldWidth">Field width for xFraction conversion</param>
    public static GeneratedRoute Generate(RouteShape shape, float depthYards, int lateralSign = 0, float fieldWidth = 53.3f)
    {
        if (lateralSign == 0)
            lateralSign = Random.value > 0.5f ? 1 : -1;

        var waypoints = BuildWaypoints(shape, depthYards, lateralSign);
        var segments = WaypointsToSegments(waypoints, fieldWidth);

        float vertexFrac = RouteShapeData.VertexFraction(shape);

        return new GeneratedRoute
        {
            segments = segments,
            shape = shape,
            totalDepthYards = depthYards,
            vertexDepthYards = depthYards * vertexFrac,
            wasHotRouted = false,
        };
    }

    /// <summary>
    /// Generate route for a target receiver, incorporating net yardage.
    /// If net yardage falls before the vertex, returns a hot-routed shorter shape.
    /// If net yardage exceeds route depth, appends a YAC segment.
    /// </summary>
    public static GeneratedRoute GenerateForTarget(
        RouteShape initialShape,
        float routeDepth,
        float netYardage,
        PlayerPositionGrp posGroup,
        PlayType playType,
        int lateralSign = 0,
        float fieldWidth = 53.3f)
    {
        if (lateralSign == 0)
            lateralSign = Random.value > 0.5f ? 1 : -1;

        // Broken play / sack — no real route
        if (netYardage <= 0)
        {
            var scramble = Generate(RouteShape.Scramble, netYardage, lateralSign, fieldWidth);
            scramble.wasHotRouted = true;
            return scramble;
        }

        float vertexDepth = routeDepth * RouteShapeData.VertexFraction(initialShape);

        // Check if net yardage is before the vertex → need hot route
        if (netYardage < vertexDepth)
        {
            var hotShape = RouteShapeData.PickHotRoute(posGroup, playType, netYardage);
            if (hotShape.HasValue && hotShape.Value != initialShape)
            {
                float hotDepth = Mathf.Min(netYardage, RouteShapeData.BaseDepth(hotShape.Value));
                var route = Generate(hotShape.Value, hotDepth, lateralSign, fieldWidth);
                route.wasHotRouted = true;
                return route;
            }
        }

        // Route fits — generate at normal depth
        var result = Generate(initialShape, routeDepth, lateralSign, fieldWidth);

        // YAC: if net yardage exceeds route depth, append a sprint segment
        if (netYardage > routeDepth)
        {
            float yac = netYardage - routeDepth;
            result.segments.Add(new SlotMovementSegment
            {
                type = SegmentType.MoveBy,
                deltaFootballCoord = new Vector2(0, yac),
                speedTier = SpeedTier.Sprint,
                ease = Ease.InOutQuad,
                sourceTag = "yac",
            });
        }
        // Net yardage falls within catchable zone (between vertex and route end)
        // — truncate route depth to net yardage
        else if (netYardage < routeDepth)
        {
            float scale = netYardage / Mathf.Max(routeDepth, 0.1f);
            result = Generate(initialShape, netYardage, lateralSign, fieldWidth);
        }

        return result;
    }

    /// <summary>
    /// Generate a run play route for the ball carrier.
    /// </summary>
    public static GeneratedRoute GenerateRunRoute(float netYardage, int gapDirection, float fieldWidth = 53.3f)
    {
        var waypoints = new List<Vector2>();

        // Slight lateral shift toward gap, then straight ahead
        float lateralYards = gapDirection * 2f; // small lateral move to gap
        if (netYardage > 0)
        {
            waypoints.Add(new Vector2(lateralYards, 1f)); // hit the hole
            waypoints.Add(new Vector2(lateralYards, netYardage)); // run downfield
        }
        else if (netYardage < 0)
        {
            waypoints.Add(new Vector2(lateralYards * 0.5f, netYardage)); // tackled in backfield
        }
        else
        {
            waypoints.Add(new Vector2(0, 0)); // no gain
        }

        var segments = WaypointsToSegments(waypoints, fieldWidth);

        return new GeneratedRoute
        {
            segments = segments,
            shape = RouteShape.RunGap,
            totalDepthYards = netYardage,
            vertexDepthYards = 0,
            wasHotRouted = false,
        };
    }

    // ── Waypoint Generation ──────────────────────────────────

    /// <summary>
    /// Build waypoint list (in yards, relative to start position) for a route shape.
    /// Each Vector2 = (lateral yards, downfield yards) cumulative from start.
    /// </summary>
    private static List<Vector2> BuildWaypoints(RouteShape shape, float depth, int latSign)
    {
        var pts = new List<Vector2>();
        float d = Mathf.Abs(depth);
        float lat = latSign; // multiplier for left/right

        switch (shape)
        {
            case RouteShape.Go:
                // Straight downfield
                pts.Add(new Vector2(0, d));
                break;

            case RouteShape.Slant:
                // Quick 2yd stem, then diagonal inside
                pts.Add(new Vector2(0, d * 0.2f));
                pts.Add(new Vector2(-lat * d * 0.3f, d));
                break;

            case RouteShape.Out:
                // Run downfield to 60%, then cut outside
                pts.Add(new Vector2(0, d * 0.6f));
                pts.Add(new Vector2(lat * d * 0.4f, d * 0.6f));
                break;

            case RouteShape.Curl:
                // Run downfield to 100%, then come back to 70%
                pts.Add(new Vector2(0, d));
                pts.Add(new Vector2(0, d * 0.7f));
                break;

            case RouteShape.Hitch:
                // Run to depth, stop (catch at the spot)
                pts.Add(new Vector2(0, d));
                break;

            case RouteShape.Post:
                // Run to 55%, then angle inside
                pts.Add(new Vector2(0, d * 0.55f));
                pts.Add(new Vector2(-lat * d * 0.25f, d));
                break;

            case RouteShape.Corner:
                // Run to 55%, then angle outside
                pts.Add(new Vector2(0, d * 0.55f));
                pts.Add(new Vector2(lat * d * 0.35f, d));
                break;

            case RouteShape.Flat:
                // Quick lateral move, barely any depth
                pts.Add(new Vector2(lat * d * 0.5f, d * 0.1f));
                pts.Add(new Vector2(lat * d, d * 0.1f));
                break;

            case RouteShape.Drag:
                // Short stem, then horizontal across field
                pts.Add(new Vector2(0, d * 0.15f));
                pts.Add(new Vector2(-lat * d * 0.5f, d * 0.15f));
                break;

            case RouteShape.Seam:
                // Straight up the seam (like Go but for TEs)
                pts.Add(new Vector2(0, d));
                break;

            case RouteShape.Screen:
                // Move backward behind LOS
                pts.Add(new Vector2(lat * 3f, -2f));
                break;

            case RouteShape.Swing:
                // Lateral out of backfield, then upfield
                pts.Add(new Vector2(lat * 4f, 0));
                pts.Add(new Vector2(lat * 5f, d));
                break;

            case RouteShape.Wheel:
                // Flat out, then wheel upfield
                pts.Add(new Vector2(lat * 4f, 0));
                pts.Add(new Vector2(lat * 5f, d));
                break;

            case RouteShape.Hook:
                // Run deep, then stop and turn back
                pts.Add(new Vector2(0, d));
                pts.Add(new Vector2(0, d * 0.7f));
                break;

            case RouteShape.DeepOut:
                // Long run downfield, then out
                pts.Add(new Vector2(0, d * 0.6f));
                pts.Add(new Vector2(lat * d * 0.3f, d));
                break;

            case RouteShape.Checkdown:
                // Short dump to RB
                pts.Add(new Vector2(lat * 2f, d * 0.1f));
                pts.Add(new Vector2(lat * 3f, d));
                break;

            case RouteShape.Block:
                // Step forward slightly
                pts.Add(new Vector2(0, Mathf.Min(d, 1f)));
                break;

            case RouteShape.DropBack:
                // QB drops back
                pts.Add(new Vector2(0, -Mathf.Abs(d)));
                break;

            case RouteShape.Scramble:
                // QB scramble — lateral + backward/forward
                pts.Add(new Vector2(lat * 3f, -3f));
                if (depth > 0)
                    pts.Add(new Vector2(lat * 2f, depth));
                break;

            case RouteShape.RunGap:
                // Handled separately in GenerateRunRoute
                pts.Add(new Vector2(0, depth));
                break;

            default:
                pts.Add(new Vector2(0, d));
                break;
        }

        return pts;
    }

    // ── Segment Conversion ───────────────────────────────────

    /// <summary>
    /// Convert cumulative waypoints (in yards) to SlotMovementSegments (in football coords).
    /// </summary>
    private static List<SlotMovementSegment> WaypointsToSegments(List<Vector2> waypoints, float fieldWidth)
    {
        var segments = new List<SlotMovementSegment>();
        if (waypoints.Count == 0) return segments;

        Vector2 prevPos = Vector2.zero;
        Vector2 prevDir = Vector2.zero;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector2 cumPos = waypoints[i];
            Vector2 legDelta = cumPos - prevPos;
            bool isFirst = (i == 0);
            bool isLast = (i == waypoints.Count - 1);

            float turnAngleIn = PlayerSpeed.AngleBetween(prevDir, legDelta);
            float turnAngleOut = 0f;
            if (!isLast)
            {
                Vector2 nextDir = waypoints[i + 1] - cumPos;
                turnAngleOut = PlayerSpeed.AngleBetween(legDelta, nextDir);
            }

            float turnFactor = PlayerSpeed.TurnFactor(prevDir, legDelta);
            float legDist = legDelta.magnitude;
            float effectiveSpeed = PlayerSpeed.SprintYps * turnFactor;
            float dur = Mathf.Max(0.15f, legDist / effectiveSpeed);

            Ease ease = PlayerSpeed.RouteLegEase(isFirst, isLast, turnAngleIn, turnAngleOut);

            // Convert yards to football coords (x = xFraction, y = yards)
            segments.Add(new SlotMovementSegment
            {
                type = SegmentType.MoveBy,
                deltaFootballCoord = new Vector2(legDelta.x / fieldWidth, legDelta.y),
                duration = dur,
                ease = ease,
                sourceTag = "route",
            });

            prevPos = cumPos;
            prevDir = legDelta;
        }

        return segments;
    }

    // ── Convenience: full route plan for all offensive slots ──

    /// <summary>
    /// Plan for a single slot — shape, depth, segments, and whether it was hot-routed.
    /// </summary>
    public struct SlotRoutePlan
    {
        public string cardUid;
        public RouteShape shape;
        public float depthYards;
        public GeneratedRoute route;
        public bool isTargetReceiver;
        public bool isCovered;
    }

    /// <summary>
    /// Generate routes for all offensive slots based on game state after yardage calc.
    /// </summary>
    public static List<SlotRoutePlan> PlanOffenseRoutes(
        Game game,
        Player offPlayer,
        PlayType playType,
        float fieldWidth = 53.3f)
    {
        var plans = new List<SlotRoutePlan>();
        if (offPlayer == null) return plans;

        int netYardage = game.yardage_this_play;
        string targetUid = game.target_receiver_uid;
        string carrierUid = game.ball_carrier_uid;
        bool isPass = (playType == PlayType.ShortPass || playType == PlayType.LongPass);
        bool isRun = (playType == PlayType.Run);

        // Gather covered receiver UIDs from defensive CTR/CNR
        var coveredUids = new HashSet<string>();
        var defPlayer = game.GetCurrentDefensivePlayer();
        if (defPlayer != null && isPass)
        {
            var rrs = new ReceiverRankingSystem(
                offPlayer.cards_board.FindAll(c => IsReceiver(c.CardData.playerPosition)),
                playType == PlayType.LongPass);
            // The target receiver from ranking already accounts for coverage,
            // but we want to know WHO is covered for visual purposes.
            // Count covering defenders and mark that many top receivers as covered.
            int coverCount = 0;
            foreach (var dc in defPlayer.cards_board)
            {
                if (dc.HasAbility(AbilityTrigger.CoverTopReceiver) || dc.HasAbility(AbilityTrigger.CoverNextReceiver))
                    coverCount++;
            }
            // Mark top N receivers as covered
            var ranked = isPass
                ? (playType == PlayType.LongPass
                    ? offPlayer.cards_board.FindAll(c => IsReceiver(c.CardData.playerPosition))
                    : offPlayer.cards_board.FindAll(c => IsReceiver(c.CardData.playerPosition)))
                : new List<Card>();
            ranked.Sort((a, b) =>
            {
                float aBonus = playType == PlayType.LongPass ? a.CardData.deep_pass_bonus : a.CardData.short_pass_bonus;
                float bBonus = playType == PlayType.LongPass ? b.CardData.deep_pass_bonus : b.CardData.short_pass_bonus;
                return bBonus.CompareTo(aBonus);
            });
            for (int i = 0; i < Mathf.Min(coverCount, ranked.Count); i++)
                coveredUids.Add(ranked[i].uid);
        }

        foreach (Card card in offPlayer.cards_board)
        {
            var posGroup = card.CardData.playerPosition;
            var plan = new SlotRoutePlan { cardUid = card.uid };

            bool isTarget = (card.uid == targetUid);
            bool isBallCarrier = (card.uid == carrierUid);
            bool isCovered = coveredUids.Contains(card.uid);
            plan.isTargetReceiver = isTarget;
            plan.isCovered = isCovered;

            // Run plays: ball carrier gets RunGap, everyone else blocks
            if (isRun)
            {
                if (isBallCarrier)
                {
                    int gapDir = Random.value > 0.5f ? 1 : -1;
                    plan.route = GenerateRunRoute(netYardage, gapDir, fieldWidth);
                    plan.shape = RouteShape.RunGap;
                    plan.depthYards = netYardage;
                }
                else
                {
                    plan.route = Generate(RouteShape.Block, 1f, 0, fieldWidth);
                    plan.shape = RouteShape.Block;
                    plan.depthYards = 1f;
                }
                plans.Add(plan);
                continue;
            }

            // Pass plays
            if (posGroup == PlayerPositionGrp.QB)
            {
                plan.route = Generate(RouteShape.DropBack, 7f, 0, fieldWidth);
                plan.shape = RouteShape.DropBack;
                plan.depthYards = 7f;
                plans.Add(plan);
                continue;
            }

            if (posGroup == PlayerPositionGrp.OL)
            {
                plan.route = Generate(RouteShape.Block, 1f, 0, fieldWidth);
                plan.shape = RouteShape.Block;
                plan.depthYards = 1f;
                plans.Add(plan);
                continue;
            }

            // Receivers (WR, RB_TE)
            if (IsReceiver(posGroup))
            {
                RouteShape? picked = RouteShapeData.PickRandom(posGroup, playType);
                if (!picked.HasValue)
                {
                    plans.Add(plan);
                    continue;
                }

                float passBonus = playType == PlayType.LongPass
                    ? card.CardData.deep_pass_bonus
                    : card.CardData.short_pass_bonus;
                // Route depth = base shape depth scaled by player's pass bonus (min 3 yards)
                float baseDepth = RouteShapeData.BaseDepth(picked.Value);
                float routeDepth = Mathf.Max(3f, baseDepth + passBonus);

                plan.shape = picked.Value;
                plan.depthYards = routeDepth;

                if (isTarget)
                {
                    plan.route = GenerateForTarget(picked.Value, routeDepth, netYardage, posGroup, playType, 0, fieldWidth);
                }
                else
                {
                    // Non-target: run their route at their ambition depth, ball just doesn't come
                    plan.route = Generate(picked.Value, routeDepth, 0, fieldWidth);
                }

                plans.Add(plan);
                continue;
            }

            // Fallback for other position groups — hold position
            plan.route = Generate(RouteShape.Block, 0.5f, 0, fieldWidth);
            plan.shape = RouteShape.Block;
            plan.depthYards = 0.5f;
            plans.Add(plan);
        }

        return plans;
    }

    private static bool IsReceiver(PlayerPositionGrp pos)
    {
        return pos == PlayerPositionGrp.WR || pos == PlayerPositionGrp.RB_TE;
    }
}

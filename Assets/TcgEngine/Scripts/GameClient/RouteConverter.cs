using System.Collections.Generic;
using DG.Tweening;
using TcgEngine;
using UnityEngine;

namespace TcgEngine.Client
{
    public static class RouteConverter
    {
        public static List<SlotMovementSegment> ToSegments(RouteData route, BoardSlot slot, string sourceTag = "route")
        {
            var segments = new List<SlotMovementSegment>();
            if (route == null || slot == null || route.keyframes.Count == 0) return segments;

            float fieldWidth = FieldSlotManager.Instance != null ? FieldSlotManager.Instance.fieldWidth : 53.3f;

            // First pass: collect matching waypoints with cumulative positions
            var legs = new List<(Vector2 cumDelta, SlotWaypoint wp)>();
            foreach (RouteKeyframe frame in route.keyframes)
            {
                SlotWaypoint match = null;
                foreach (SlotWaypoint wp in frame.waypoints)
                {
                    if (wp.posGroup == slot.player_position_type)
                    {
                        match = wp;
                        break;
                    }
                }
                if (match == null) continue;
                legs.Add((new Vector2(match.deltaX, match.deltaYards), match));
            }

            if (legs.Count == 0) return segments;

            // Second pass: compute per-leg directions, turn angles, and build segments
            Vector2 prevPos = Vector2.zero;     // cumulative position tracker
            Vector2 prevDir = Vector2.zero;     // direction of previous leg

            for (int i = 0; i < legs.Count; i++)
            {
                Vector2 cumDelta = legs[i].cumDelta;
                Vector2 legDelta = cumDelta - prevPos; // per-leg displacement (in yards)
                Vector2 curDir = legDelta;

                bool isFirst = (i == 0);
                bool isLast = (i == legs.Count - 1);

                // Turn angle entering this leg (angle between previous direction and this direction)
                float turnAngleIn = PlayerSpeed.AngleBetween(prevDir, curDir);

                // Turn angle exiting this leg (angle between this direction and next direction)
                float turnAngleOut = 0f;
                if (!isLast)
                {
                    Vector2 nextCum = legs[i + 1].cumDelta;
                    Vector2 nextDir = nextCum - cumDelta;
                    turnAngleOut = PlayerSpeed.AngleBetween(curDir, nextDir);
                }

                // Speed: apply turn factor based on the sharper of entry/exit angles
                float turnFactor = PlayerSpeed.TurnFactor(prevDir, curDir);
                float legDistYards = legDelta.magnitude;
                float effectiveSpeed = PlayerSpeed.SprintYps * turnFactor;
                float dur = Mathf.Max(0.15f, legDistYards / effectiveSpeed);

                // Ease based on leg context
                Ease ease = PlayerSpeed.RouteLegEase(isFirst, isLast, turnAngleIn, turnAngleOut);

                // Convert delta to football coords (x = xFraction, y = yards)
                segments.Add(new SlotMovementSegment
                {
                    type = SegmentType.MoveBy,
                    deltaFootballCoord = new Vector2(legDelta.x / fieldWidth, legDelta.y),
                    duration = dur,
                    ease = ease,
                    sourceTag = sourceTag
                });

                prevPos = cumDelta;
                prevDir = curDir;
            }

            return segments;
        }

        /// <summary>
        /// Compute the total duration of route segments (for timing the resolution sequence).
        /// </summary>
        public static float TotalDuration(List<SlotMovementSegment> segments)
        {
            float total = 0f;
            foreach (var seg in segments)
            {
                if (seg.type == SegmentType.MoveTo || seg.type == SegmentType.MoveBy)
                    total += seg.duration;
            }
            return total;
        }
    }
}

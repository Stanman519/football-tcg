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

            // First pass: collect matching waypoints with cumulative positions and authored timing
            var legs = new List<(Vector2 cumDelta, float timeOffset, SlotWaypoint wp)>();
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
                legs.Add((new Vector2(match.deltaX, match.deltaYards), frame.timeOffset, match));
            }

            if (legs.Count == 0) return segments;

            // Second pass: use authored timeOffset deltas for duration, keep turn-aware easing
            Vector2 prevPos = Vector2.zero;     // cumulative position tracker
            Vector2 prevDir = Vector2.zero;     // direction of previous leg
            float prevTime = 0f;                // previous keyframe's timeOffset

            for (int i = 0; i < legs.Count; i++)
            {
                Vector2 cumDelta = legs[i].cumDelta;
                Vector2 legDelta = cumDelta - prevPos; // per-leg displacement (in yards)
                float authoredDur = legs[i].timeOffset - prevTime;

                // Skip zero-displacement origin keyframe (t=0, pos=0,0)
                if (legDelta.sqrMagnitude < 0.001f)
                {
                    prevTime = legs[i].timeOffset;
                    prevPos = cumDelta;
                    continue;
                }

                Vector2 curDir = legDelta;

                bool isFirst = (i == 0 || prevDir == Vector2.zero);
                bool isLast = (i == legs.Count - 1);

                // Turn angle entering this leg
                float turnAngleIn = PlayerSpeed.AngleBetween(prevDir, curDir);

                // Turn angle exiting this leg
                float turnAngleOut = 0f;
                if (!isLast)
                {
                    Vector2 nextCum = legs[i + 1].cumDelta;
                    Vector2 nextDir = nextCum - cumDelta;
                    turnAngleOut = PlayerSpeed.AngleBetween(curDir, nextDir);
                }

                // Use authored timing with a floor so nothing is instant
                float dur = Mathf.Max(0.15f, authoredDur);

                // Ease based on leg context (turn-aware)
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
                prevTime = legs[i].timeOffset;
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

using System;
using System.Collections.Generic;
using TcgEngine.Gameplay;
using TcgEngine;
using UnityEngine;

namespace TcgEngine.Client
{
    /// <summary>
    /// Executes a queue of SlotMovementSegments on a BoardSlot.
    /// Supports pause/resume via WaitForPhase and WaitForSignal segments.
    /// Movement is driven by one TokenMotion per token (speed carries across segments, tokens can
    /// chase a moving target) -- not a tween per segment. Speeds come from FieldMotionProfile.
    /// </summary>
    public class SlotMovementQueue
    {
        private readonly List<SlotMovementSegment> segments = new List<SlotMovementSegment>();
        private int currentIndex = -1; // -1 = not yet running
        private bool paused;
        private bool started; // armed by Start(); an armed queue self-starts when segments arrive
        private readonly BoardSlot owner;

        private readonly TokenMotion motion = new TokenMotion();
        private enum Leg { None, Delay, Moving, Holding, Following }
        private Leg leg = Leg.None;
        private float timer; // remaining delay/hold, or elapsed follow time

        private bool seeded;
        private float variance = 1f;
        private float reaction;

        private const float MaxFollowSeconds = 12f;
        private Vector2 followOffset;
        private bool followOffsetSet;

        public bool IsIdle => currentIndex < 0 || currentIndex >= segments.Count;
        public bool IsPaused => paused;
        public bool IsActive => !IsIdle && !paused;
        public bool HasWork => started && !paused && (currentIndex < 0 ? segments.Count > 0 : currentIndex < segments.Count);
        /// <summary>True while this queue is actually driving the token (not idle, not parked on a wait).</summary>
        public bool IsDriving => !paused && !IsIdle;
        public int Count => segments.Count;
        /// <summary>sourceTag of the segment currently executing (null when idle) — lets choreography sync to route phases like "yac".</summary>
        public string CurrentSourceTag => IsIdle ? null : segments[currentIndex].sourceTag;

        /// <summary>This token's personal reaction time (seconds), stable for the whole game.</summary>
        public float ReactionDelay { get { EnsureSeed(); return reaction; } }
        /// <summary>This token's personal speed multiplier (around 1), stable for the whole game.</summary>
        public float SpeedVariance { get { EnsureSeed(); return variance; } }

        public SlotMovementQueue(BoardSlot owner)
        {
            this.owner = owner;
        }

        private void EnsureSeed()
        {
            if (seeded) return;
            seeded = true;
            FieldMotionProfile p = FieldMotionProfile.Current;
            int seed = 17;
            if (owner != null)
                seed += owner.player_id * 1000 + (int)owner.player_position_type * 10 + owner.slotIndex;
            var rng = new System.Random(seed);
            variance = 1f + ((float)rng.NextDouble() * 2f - 1f) * p.speedVariance;
            reaction = Mathf.Lerp(p.reactionMin, p.reactionMax, (float)rng.NextDouble());
        }

        /// <summary>Legacy convenience: set speed via TimeController. Use TimeController.Instance.SetBulletTime() instead.</summary>
        public static void SetAllSpeed(float multiplier)
        {
            if (TimeController.Instance != null)
                TimeController.Instance.SetBulletTime(1f / Mathf.Max(0.01f, multiplier), 0f);
        }

        // ── Queue manipulation ──────────────────────────────

        public void Enqueue(SlotMovementSegment seg)
        {
            segments.Add(seg);
        }

        public void EnqueueRange(List<SlotMovementSegment> segs)
        {
            segments.AddRange(segs);
        }

        /// <summary>Insert a segment right after the current one.</summary>
        public void InsertNext(SlotMovementSegment seg)
        {
            int insertAt = Mathf.Max(0, currentIndex + 1);
            segments.Insert(insertAt, seg);
        }

        /// <summary>Clear all segments (or only those with matching sourceTag).</summary>
        public void Clear(string sourceTag = null)
        {
            leg = Leg.None;
            motion.Speed = 0f;

            if (sourceTag == null)
            {
                segments.Clear();
                currentIndex = -1;
                paused = false;
                started = false; // de-arm so cleared queues stay quiet until next play's Start()
            }
            else
            {
                // Remove matching segments; adjust currentIndex
                for (int i = segments.Count - 1; i >= 0; i--)
                {
                    if (segments[i].sourceTag == sourceTag)
                    {
                        segments.RemoveAt(i);
                        if (i <= currentIndex) currentIndex--;
                    }
                }
            }
        }

        /// <summary>Whistle: stop where you stand and drop everything queued. Stays armed (unlike Clear).</summary>
        public void Halt()
        {
            segments.Clear();
            currentIndex = -1;
            paused = false;
            leg = Leg.None;
            motion.Speed = 0f;
        }

        /// <summary>Discard all remaining segments from current position and append new ones.</summary>
        public void ReplaceFromCurrent(List<SlotMovementSegment> newSegments)
        {
            leg = Leg.None;

            int keepCount = Mathf.Max(0, currentIndex);
            if (keepCount < segments.Count)
                segments.RemoveRange(keepCount, segments.Count - keepCount);

            segments.AddRange(newSegments);
            currentIndex = keepCount;
            paused = false;
        }

        /// <summary>
        /// Find the last segment with the given sourceTag and adjust its delta.
        /// Used to modify yardage mid-play (also retargets the leg if it is already running).
        /// </summary>
        public bool ModifyLastMoveBy(string sourceTag, Vector2 additionalDelta)
        {
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                if (segments[i].sourceTag == sourceTag && segments[i].type == SegmentType.MoveBy)
                {
                    segments[i].deltaFootballCoord += additionalDelta;
                    if (i == currentIndex && leg == Leg.Moving)
                        motion.Target = ClampToField(motion.Target + ToLocalDelta(additionalDelta));
                    return true;
                }
            }
            return false;
        }

        // ── Execution control ───────────────────────────────

        public void Start()
        {
            // Arm even when empty — segments enqueued later (pursuit, live-ball deltas) self-start via Tick()
            started = true;
            paused = false;
            if (segments.Count > 0 && currentIndex < 0)
            {
                currentIndex = 0;
                leg = Leg.None;
            }
        }

        public void Resume()
        {
            if (!paused) return;
            paused = false;
            // Advance past the wait segment
            currentIndex++;
            leg = Leg.None;
        }

        /// <summary>Called from BoardSlot.Update() every frame.</summary>
        public void Tick()
        {
            float scale = TimeController.Instance != null ? TimeController.Instance.FieldTimeScale : 1f;
            Tick(Time.deltaTime * scale);
        }

        public void Tick(float dt)
        {
            if (paused) return;

            if (currentIndex < 0)
            {
                // Armed but never ran — self-start when work arrives
                if (!started || segments.Count == 0) return;
                currentIndex = 0;
                leg = Leg.None;
            }

            int guard = 0;
            while (!paused && !IsIdle && guard++ < 64)
            {
                if (leg == Leg.None)
                {
                    BeginCurrent();
                    continue;
                }
                if (dt <= 0f) break;

                SlotMovementSegment seg = segments[currentIndex];
                switch (leg)
                {
                    case Leg.Delay:
                        timer -= dt;
                        if (timer > 0f) dt = 0f;
                        else { dt = -timer; StartLeg(seg); }
                        break;

                    case Leg.Holding:
                        timer -= dt;
                        if (timer > 0f) dt = 0f;
                        else { dt = -timer; Advance(); }
                        break;

                    case Leg.Moving:
                    {
                        motion.Pos = LocalPos();
                        dt = motion.Step(dt, out bool arrived);
                        ApplyPos();
                        if (arrived) Advance();
                        else dt = 0f;
                        break;
                    }

                    case Leg.Following:
                        StepFollow(seg, dt);
                        dt = 0f;
                        break;
                }
            }

            if (IsIdle) motion.Speed = 0f;
        }

        /// <summary>Seconds until this queue's remaining moves finish (stops at the next wait). Also returns where it ends.</summary>
        public float EstimateRemainingTime(out Vector2 endLocal)
        {
            FieldMotionProfile p = FieldMotionProfile.Current;
            Vector2 pos = LocalPos();
            float v = motion.Speed;
            float total = 0f;
            int start = Mathf.Max(0, currentIndex);

            for (int i = start; i < segments.Count; i++)
            {
                SlotMovementSegment seg = segments[i];
                bool isCurrent = i == currentIndex;
                if (seg.type == SegmentType.WaitForPhase || seg.type == SegmentType.WaitForSignal)
                {
                    if (isCurrent) continue; // parked here now; assume it is about to release
                    break;
                }
                if (seg.type != SegmentType.MoveTo && seg.type != SegmentType.MoveBy) continue;

                if (isCurrent && leg == Leg.Delay) total += Mathf.Max(0f, timer);
                else if (!isCurrent || leg == Leg.None) total += seg.startDelay;

                if (isCurrent && leg == Leg.Holding) { total += Mathf.Max(0f, timer); v = 0f; continue; }

                Vector2 target = isCurrent && leg == Leg.Moving ? motion.Target : ResolveTarget(seg, pos);
                float dist = Vector2.Distance(pos, target);
                if (dist < 0.02f)
                {
                    if (!seg.speedTier.HasValue) total += seg.duration;
                    v = 0f;
                    continue;
                }

                float cruise = CruiseFor(seg, dist);
                float exit = ExitSpeedFor(i, target, target - pos, cruise);
                if (v < 0.05f && exit < 0.05f)
                    cruise = Mathf.Min(cruise, dist / Mathf.Max(0.05f, p.minMoveDuration));
                total += TokenMotion.EstimateTime(dist, v, cruise, exit, p.accel, p.decel);
                v = exit;
                pos = target;
            }

            endLocal = pos;
            return total;
        }

        /// <summary>Remaining move time (for timing estimates).</summary>
        public float EstimateTotalMoveDuration() => EstimateRemainingTime(out _);

        // ── Signal/phase handlers ───────────────────────────

        public void OnPhaseChanged(GamePhase newPhase)
        {
            if (!paused || IsIdle) return;
            var seg = segments[currentIndex];
            if (seg.type == SegmentType.WaitForPhase && seg.waitPhase == newPhase)
                Resume();
        }

        public void OnSignal(string signal)
        {
            if (!paused || IsIdle) return;
            var seg = segments[currentIndex];
            if (seg.type == SegmentType.WaitForSignal && seg.waitSignal == signal)
                Resume();
        }

        // ── Segment execution ───────────────────────────────

        private void Advance()
        {
            currentIndex++;
            leg = Leg.None;
        }

        private void BeginCurrent()
        {
            SlotMovementSegment seg = segments[currentIndex];
            switch (seg.type)
            {
                case SegmentType.WaitForPhase:
                case SegmentType.WaitForSignal:
                    paused = true;
                    motion.Speed = 0f;
                    break;

                case SegmentType.Callback:
                {
                    int idx = currentIndex;
                    seg.callback?.Invoke();
                    if (currentIndex == idx) currentIndex++; // callback may have cleared/replaced the queue
                    break;
                }

                default:
                    if (seg.startDelay > 0f)
                    {
                        leg = Leg.Delay;
                        timer = seg.startDelay;
                        motion.Speed = 0f;
                    }
                    else StartLeg(seg);
                    break;
            }
        }

        private void StartLeg(SlotMovementSegment seg)
        {
            FieldMotionProfile p = FieldMotionProfile.Current;
            motion.Accel = p.accel;
            motion.Decel = p.decel;
            Vector2 pos = LocalPos();
            motion.Pos = pos;

            if (seg.type == SegmentType.Follow)
            {
                leg = Leg.Following;
                timer = 0f;
                followOffsetSet = false;
                return;
            }

            if (seg.type == SegmentType.MoveTo && FieldSlotManager.Instance == null)
            {
                Advance(); // no field to resolve coords against
                return;
            }

            Vector2 target = ResolveTarget(seg, pos);
            float dist = Vector2.Distance(pos, target);
            if (dist < 0.02f)
            {
                // Zero-distance, duration-based move = a timed hold ("read" beat)
                float hold = seg.speedTier.HasValue ? 0f : seg.duration;
                motion.Speed = 0f;
                if (hold > 0f) { leg = Leg.Holding; timer = hold; }
                else Advance();
                return;
            }

            float cruise = CruiseFor(seg, dist);
            float exit = ExitSpeedFor(currentIndex, target, target - pos, cruise);
            if (motion.Speed < 0.05f && exit < 0.05f)
                cruise = Mathf.Min(cruise, dist / Mathf.Max(0.05f, p.minMoveDuration)); // isolated move: never a blink

            motion.SetLeg(target, cruise, exit);
            leg = Leg.Moving;
        }

        private void StepFollow(SlotMovementSegment seg, float dt)
        {
            BoardSlot t = seg.followTarget;
            if (t == null) { Advance(); return; }

            timer += dt;
            Vector2 pos = LocalPos();
            motion.Pos = pos;
            Vector2 tp = t.transform.localPosition;
            Vector2 to = tp - pos;
            float d = to.magnitude;
            bool targetBusy = t.MoveQueue != null && t.MoveQueue.HasWork;

            // Fixed offset from the target, chosen once: pursuers fan out around him instead of stacking
            if (!followOffsetSet)
            {
                Vector2 away = d > 0.001f ? -to / d : Vector2.up;
                float a = seg.followLateral * Mathf.Deg2Rad;
                followOffset = new Vector2(
                    away.x * Mathf.Cos(a) - away.y * Mathf.Sin(a),
                    away.x * Mathf.Sin(a) + away.y * Mathf.Cos(a)) * seg.stopRadius;
                followOffsetSet = true;
            }
            Vector2 goal = tp + followOffset;
            if (Vector2.Distance(pos, goal) <= 0.15f)
            {
                motion.Speed = 0f;
                if (!targetBusy || timer > MaxFollowSeconds) Advance();
                return;
            }

            float cruise = PlayerSpeed.YardsPerSecond(seg.speedTier ?? SpeedTier.Sprint) * SpeedVariance * seg.speedScale;
            motion.SetLeg(goal, cruise, targetBusy ? cruise * 0.6f : 0f);
            motion.Step(dt, out _);
            ApplyPos();

            if (timer > MaxFollowSeconds) Advance();
        }

        // ── Helpers ─────────────────────────────────────────

        private Vector2 LocalPos()
        {
            Vector3 lp = owner.transform.localPosition;
            return new Vector2(lp.x, lp.y);
        }

        private void ApplyPos()
        {
            owner.transform.localPosition = new Vector3(motion.Pos.x, motion.Pos.y, -1f);
        }

        private static Vector2 ToLocalDelta(Vector2 footballDelta)
        {
            float fw = FieldSlotManager.Instance != null ? FieldSlotManager.Instance.fieldWidth : 53.3f;
            return new Vector2(footballDelta.x * fw, footballDelta.y);
        }

        private Vector2 ResolveTarget(SlotMovementSegment seg, Vector2 from)
        {
            Vector2 target;
            if (seg.type == SegmentType.MoveTo)
            {
                if (FieldSlotManager.Instance == null) return from;
                Vector3 t = FieldSlotManager.Instance.ToLocalPosPublic(seg.footballCoord);
                target = new Vector2(t.x, t.y);
            }
            else target = from + ToLocalDelta(seg.deltaFootballCoord);
            return ClampToField(target);
        }

        /// <summary>
        /// Cruise speed for a segment. Tiered segments use the profile speed; duration-based
        /// segments use their implied speed, never faster than a sprint.
        /// </summary>
        private float CruiseFor(SlotMovementSegment seg, float dist)
        {
            float baseSpeed = seg.speedTier.HasValue
                ? PlayerSpeed.YardsPerSecond(seg.speedTier.Value)
                : Mathf.Min(dist / Mathf.Max(0.05f, seg.duration), PlayerSpeed.SprintYps * 1.15f);
            return Mathf.Max(0.3f, baseSpeed * SpeedVariance * seg.speedScale);
        }

        /// <summary>Speed to carry out of this leg: full through a straight waypoint, less through a cut, 0 into a stop.</summary>
        private float ExitSpeedFor(int index, Vector2 target, Vector2 curDir, float cruise)
        {
            if (index + 1 >= segments.Count) return 0f;
            SlotMovementSegment next = segments[index + 1];
            if (next.type != SegmentType.MoveTo && next.type != SegmentType.MoveBy) return 0f;
            if (next.startDelay > 0f) return 0f;

            Vector2 nextTarget = ResolveTarget(next, target);
            Vector2 nextDir = nextTarget - target;
            float nextDist = nextDir.magnitude;
            if (nextDist < 0.02f) return 0f;

            float turn = PlayerSpeed.TurnFactor(curDir, nextDir);
            return Mathf.Min(cruise, CruiseFor(next, nextDist)) * turn * turn;
        }

        /// <summary>
        /// Keep on-field players inside the sidelines (e.g. an out route from a wide alignment).
        /// Slots already parked out of bounds (bench at ±1.1) are left alone.
        /// </summary>
        private Vector2 ClampToField(Vector2 target)
        {
            float fw = FieldSlotManager.Instance != null ? FieldSlotManager.Instance.fieldWidth : 53.3f;
            float half = fw * 0.5f;
            if (Mathf.Abs(owner.transform.localPosition.x) > half) return target;
            float limit = half - 1.5f; // ~1.5 yd inside the sideline
            target.x = Mathf.Clamp(target.x, -limit, limit);
            return target;
        }
    }
}

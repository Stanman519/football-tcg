using System;
using System.Collections.Generic;
using TcgEngine.Gameplay;
using DG.Tweening;
using TcgEngine;
using UnityEngine;

namespace TcgEngine.Client
{
    /// <summary>
    /// Executes a queue of SlotMovementSegments on a BoardSlot.
    /// Supports pause/resume via WaitForPhase and WaitForSignal segments.
    /// When idle, the slot falls back to its normal lerp behavior.
    /// </summary>
    public class SlotMovementQueue
    {
        private readonly List<SlotMovementSegment> segments = new List<SlotMovementSegment>();
        private int currentIndex = -1; // -1 = not yet running
        private Tween activeTween;
        private bool paused;
        private bool started; // armed by Start(); an armed queue self-starts when segments arrive
        private readonly BoardSlot owner;

        public bool IsIdle => currentIndex < 0 || currentIndex >= segments.Count;
        public bool IsPaused => paused;
        public bool IsActive => !IsIdle && !paused;
        public bool HasWork => started && !paused && (currentIndex < 0 ? segments.Count > 0 : currentIndex < segments.Count);
        public int Count => segments.Count;
        /// <summary>sourceTag of the segment currently executing (null when idle) — lets choreography sync to route phases like "yac".</summary>
        public string CurrentSourceTag => IsIdle ? null : segments[currentIndex].sourceTag;

        /// <summary>Current speed multiplier from TimeController (1/FieldTimeScale). Longer = slower.</summary>
        private float SpeedMultiplier =>
            TimeController.Instance != null ? TimeController.Instance.Scale(1f) : 1f;

        public SlotMovementQueue(BoardSlot owner)
        {
            this.owner = owner;
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
            activeTween?.Kill();
            activeTween = null;

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

        /// <summary>Discard all remaining segments from current position and append new ones.</summary>
        public void ReplaceFromCurrent(List<SlotMovementSegment> newSegments)
        {
            activeTween?.Kill();
            activeTween = null;

            int keepCount = Mathf.Max(0, currentIndex);
            if (keepCount < segments.Count)
                segments.RemoveRange(keepCount, segments.Count - keepCount);

            segments.AddRange(newSegments);
            currentIndex = keepCount;
            paused = false;
        }

        /// <summary>
        /// Find the last segment with the given sourceTag and adjust its delta.
        /// Used to modify yardage mid-play.
        /// </summary>
        public bool ModifyLastMoveBy(string sourceTag, Vector2 additionalDelta)
        {
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                if (segments[i].sourceTag == sourceTag && segments[i].type == SegmentType.MoveBy)
                {
                    segments[i].deltaFootballCoord += additionalDelta;
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
                ExecuteCurrentSegment();
            }
        }

        public void Resume()
        {
            if (!paused) return;
            paused = false;
            // Advance past the wait segment
            currentIndex++;
            if (!IsIdle)
                ExecuteCurrentSegment();
        }

        /// <summary>Called from BoardSlot.Update() every frame.</summary>
        public void Tick()
        {
            if (paused) return;

            if (activeTween != null)
            {
                if (activeTween.IsActive() && activeTween.IsPlaying())
                    return;
                // Advance ONLY on real tween completion (never skips a freshly-enqueued segment)
                activeTween = null;
                currentIndex++;
            }
            else if (currentIndex < 0)
            {
                // Armed but never ran — self-start when work arrives
                if (!started || segments.Count == 0) return;
                currentIndex = 0;
            }

            if (IsIdle) return;

            ExecuteCurrentSegment();
        }

        /// <summary>Sum of all remaining move segment durations (for timing estimates).</summary>
        public float EstimateTotalMoveDuration()
        {
            float total = 0f;
            int start = Mathf.Max(0, currentIndex);
            for (int i = start; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg.type == SegmentType.MoveTo || seg.type == SegmentType.MoveBy)
                    total += seg.duration;
            }
            return total;
        }

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

        private void ExecuteCurrentSegment()
        {
            if (IsIdle) return;
            var seg = segments[currentIndex];

            switch (seg.type)
            {
                case SegmentType.MoveTo:
                    ExecuteMoveTo(seg);
                    break;

                case SegmentType.MoveBy:
                    ExecuteMoveBy(seg);
                    break;

                case SegmentType.WaitForPhase:
                    paused = true;
                    break;

                case SegmentType.WaitForSignal:
                    paused = true;
                    break;

                case SegmentType.Callback:
                    seg.callback?.Invoke();
                    // Immediately advance to next segment
                    currentIndex++;
                    if (!IsIdle)
                        ExecuteCurrentSegment();
                    break;
            }
        }

        /// <summary>
        /// Keep on-field players inside the sidelines (e.g. an out route from a wide alignment).
        /// Slots already parked out of bounds (bench at ±1.1) are left alone.
        /// </summary>
        private Vector3 ClampToField(Vector3 target)
        {
            float fw = FieldSlotManager.Instance != null ? FieldSlotManager.Instance.fieldWidth : 53.3f;
            float half = fw * 0.5f;
            if (Mathf.Abs(owner.transform.localPosition.x) > half) return target;
            float limit = half - 1.5f; // ~1.5 yd inside the sideline
            target.x = Mathf.Clamp(target.x, -limit, limit);
            return target;
        }

        private void ExecuteMoveTo(SlotMovementSegment seg)
        {
            if (FieldSlotManager.Instance == null)
            {
                // Skip segment — returning without a tween would re-execute every frame
                currentIndex++;
                if (!IsIdle) ExecuteCurrentSegment();
                return;
            }

            Vector3 target = FieldSlotManager.Instance.ToLocalPosPublic(seg.footballCoord);
            target.z = -1f;
            target = ClampToField(target);

            float dur = seg.duration;
            if (seg.speedTier.HasValue)
            {
                float dist = Vector3.Distance(owner.transform.localPosition, target);
                dur = PlayerSpeed.Duration(dist, seg.speedTier.Value);
            }

            dur *= SpeedMultiplier;

            if (dur <= 0f)
            {
                owner.transform.localPosition = target;
                currentIndex++;
                if (!IsIdle) ExecuteCurrentSegment();
                return;
            }

            activeTween = owner.transform
                .DOLocalMove(target, dur)
                .SetEase(seg.ease)
                .SetLink(owner.gameObject);
        }

        private void ExecuteMoveBy(SlotMovementSegment seg)
        {
            Vector3 current = owner.transform.localPosition;
            float fieldWidth = FieldSlotManager.Instance != null ? FieldSlotManager.Instance.fieldWidth : 53.3f;
            Vector3 delta = new Vector3(seg.deltaFootballCoord.x * fieldWidth, seg.deltaFootballCoord.y, 0);
            Vector3 target = current + delta;
            target.z = -1f;
            target = ClampToField(target);

            float dur = seg.duration;
            if (seg.speedTier.HasValue)
            {
                dur = PlayerSpeed.Duration(delta.magnitude, seg.speedTier.Value);
            }

            dur *= SpeedMultiplier;

            if (dur <= 0f)
            {
                owner.transform.localPosition = target;
                currentIndex++;
                if (!IsIdle) ExecuteCurrentSegment();
                return;
            }

            activeTween = owner.transform
                .DOLocalMove(target, dur)
                .SetEase(seg.ease)
                .SetLink(owner.gameObject);
        }
    }
}

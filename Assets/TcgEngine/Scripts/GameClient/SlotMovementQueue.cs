using System;
using System.Collections.Generic;
using Assets.TcgEngine.Scripts.Gameplay;
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
        private int currentIndex = -1; // -1 = idle
        private Tween activeTween;
        private bool paused;
        private readonly BoardSlot owner;

        public bool IsIdle => currentIndex < 0 || currentIndex >= segments.Count;
        public bool IsPaused => paused;
        public bool IsActive => !IsIdle && !paused;
        public int Count => segments.Count;

        public SlotMovementQueue(BoardSlot owner)
        {
            this.owner = owner;
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
            if (segments.Count == 0) return;
            currentIndex = 0;
            paused = false;
            ExecuteCurrentSegment();
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
            if (IsIdle || paused) return;

            // If tween is still running, wait
            if (activeTween != null && activeTween.IsActive() && activeTween.IsPlaying())
                return;

            // Current segment finished — advance
            activeTween = null;
            currentIndex++;

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

        private void ExecuteMoveTo(SlotMovementSegment seg)
        {
            if (FieldSlotManager.Instance == null) return;

            Vector3 target = FieldSlotManager.Instance.ToLocalPosPublic(seg.footballCoord);
            target.z = -1f;

            float dur = seg.duration;
            if (seg.speedTier.HasValue)
            {
                float dist = Vector3.Distance(owner.transform.localPosition, target);
                dur = PlayerSpeed.Duration(dist, seg.speedTier.Value);
            }

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

            float dur = seg.duration;
            if (seg.speedTier.HasValue)
            {
                dur = PlayerSpeed.Duration(delta.magnitude, seg.speedTier.Value);
            }

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

using System;
using TcgEngine.Gameplay;
using DG.Tweening;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine
{
    public enum SegmentType
    {
        MoveTo,         // tween to absolute football coord
        MoveBy,         // tween by relative yards
        WaitForPhase,   // pause until game reaches target phase
        WaitForSignal,  // pause until named signal fires
        Callback,       // fire an Action when reached
        Follow,         // chase another slot (moving target) until within stopRadius and it has stopped
    }

    [Serializable]
    public class SlotMovementSegment
    {
        public SegmentType type;

        // MoveTo — absolute football coord (xFraction, yardsFromLOS)
        public Vector2 footballCoord;

        // MoveBy — relative offset in football coords (x = xFraction delta, y = yards delta)
        public Vector2 deltaFootballCoord;

        // Tween params
        public float duration = 0.8f;
        public Ease ease = Ease.OutQuad;

        // WaitForPhase
        public GamePhase waitPhase = GamePhase.None;

        // WaitForSignal
        public string waitSignal;

        // Callback
        [NonSerialized] public Action callback;

        // Speed-based duration (overrides duration field when set)
        public SpeedTier? speedTier = null;

        // Follow -- chase this slot; done when within stopRadius and the target has no more moves
        [NonSerialized] public BoardSlot followTarget;
        public float stopRadius = 1.2f;
        // Follow -- angle (degrees) off the straight chase line, so pursuers fan out instead of stacking
        public float followLateral = 0f;

        // Seconds to stand still before this segment starts (reaction time, stagger)
        public float startDelay = 0f;

        // Multiplier on the segment's cruise speed (e.g. 0.9 = trailing pursuer)
        public float speedScale = 1f;

        // Who enqueued this (for debugging / selective removal)
        public string sourceTag;

        // ── Factory methods ────────────────────────────────

        public static SlotMovementSegment MoveTo(Vector2 footballCoord, float duration = 0.8f, Ease ease = Ease.OutQuad, string tag = null)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.MoveTo,
                footballCoord = footballCoord,
                duration = duration,
                ease = ease,
                sourceTag = tag
            };
        }

        public static SlotMovementSegment MoveBy(Vector2 delta, float duration = 0.8f, Ease ease = Ease.OutQuad, string tag = null)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.MoveBy,
                deltaFootballCoord = delta,
                duration = duration,
                ease = ease,
                sourceTag = tag
            };
        }

        public static SlotMovementSegment MoveTo(Vector2 footballCoord, SpeedTier speed, string tag = null)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.MoveTo,
                footballCoord = footballCoord,
                speedTier = speed,
                ease = PlayerSpeed.DefaultEase(speed),
                sourceTag = tag
            };
        }

        public static SlotMovementSegment MoveBy(Vector2 delta, SpeedTier speed, string tag = null)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.MoveBy,
                deltaFootballCoord = delta,
                speedTier = speed,
                ease = PlayerSpeed.DefaultEase(speed),
                sourceTag = tag
            };
        }

        /// <summary>Chase a moving slot at the given tier. Ends once close AND the target has stopped.</summary>
        public static SlotMovementSegment Follow(BoardSlot target, SpeedTier speed, float stopRadius, string tag = null)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.Follow,
                followTarget = target,
                speedTier = speed,
                stopRadius = stopRadius,
                sourceTag = tag
            };
        }

        /// <summary>Stand still for a fixed time.</summary>
        public static SlotMovementSegment Hold(float seconds, string tag = null)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.MoveBy,
                deltaFootballCoord = Vector2.zero,
                duration = seconds,
                sourceTag = tag
            };
        }

        public static SlotMovementSegment WaitForPhase(GamePhase phase)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.WaitForPhase,
                waitPhase = phase,
                sourceTag = "wait"
            };
        }

        public static SlotMovementSegment WaitForSignal(string signal)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.WaitForSignal,
                waitSignal = signal,
                sourceTag = "wait"
            };
        }

        public static SlotMovementSegment DoCallback(Action cb, string tag = null)
        {
            return new SlotMovementSegment
            {
                type = SegmentType.Callback,
                callback = cb,
                sourceTag = tag
            };
        }
    }
}

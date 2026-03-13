using System;
using Assets.TcgEngine.Scripts.Gameplay;
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

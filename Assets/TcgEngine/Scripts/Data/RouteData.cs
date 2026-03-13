using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Which slot(s) a route / animation targets.
    /// </summary>
    public enum AnimTargetType
    {
        Self        = 0,  // The card that triggered the ability
        SlotIndex   = 1,  // A specific slot identified by posGroup + slotIndex
        AllInGroup  = 2,  // Every slot in posGroup
        TopReceiver = 3,  // The top-ranked receiver (ReceiverRankingSystem) — future
    }

    /// <summary>
    /// A sequence of positional keyframes that animate field slots after a play resolves.
    /// Assign to AbilityData.animationData to trigger on ability resolution.
    /// </summary>
    [CreateAssetMenu(menuName = "FirstAndLong/RouteData", fileName = "route")]
    public class RouteData : ScriptableObject
    {
        public List<RouteKeyframe> keyframes = new List<RouteKeyframe>();
    }

    /// <summary>
    /// One moment in time during a route animation.
    /// </summary>
    [Serializable]
    public class RouteKeyframe
    {
        public float timeOffset;                // seconds from route start when this frame applies
        public List<SlotWaypoint> waypoints = new List<SlotWaypoint>();
    }

    /// <summary>
    /// A single slot's movement delta at a given keyframe.
    /// All values are RELATIVE to the slot's position when the route started.
    /// deltaYards: positive = toward opponent end zone, negative = backward
    /// deltaX: positive = right, negative = left (in yards, not fraction)
    /// </summary>
    [Serializable]
    public class SlotWaypoint
    {
        public PlayerPositionGrp posGroup;
        public int slotIndex;
        public float deltaX;       // yards left/right from starting position
        public float deltaYards;   // yards forward/backward from starting position
    }
}

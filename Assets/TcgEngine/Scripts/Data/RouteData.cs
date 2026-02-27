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
    /// A single slot's target position at a given keyframe.
    /// </summary>
    [Serializable]
    public class SlotWaypoint
    {
        public PlayerPositionGrp posGroup;
        public int slotIndex;
        public float xFraction;     // -0.5 … +0.5
        public float yardsFromLOS;  // negative = own backfield
    }
}

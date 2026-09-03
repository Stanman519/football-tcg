using System;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

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

        [Tooltip("Depth in yards where the route changes direction (the 'break'). -1 = auto-compute from keyframes.")]
        public float vertexDepthYards = -1f;

        [Tooltip("Total downfield depth of the route in yards. -1 = auto-compute from keyframes.")]
        public float totalDepthYards = -1f;

        /// <summary>
        /// Get vertex depth: manual override or auto-detect from keyframes.
        /// Vertex = the keyframe where forward (deltaYards) direction reverses or lateral changes sharply.
        /// </summary>
        public float GetVertexDepth()
        {
            if (vertexDepthYards >= 0f) return vertexDepthYards;
            return ComputeVertexDepth();
        }

        /// <summary>Get total downfield depth: manual override or max deltaYards from keyframes.</summary>
        public float GetTotalDepth()
        {
            if (totalDepthYards >= 0f) return totalDepthYards;
            return ComputeTotalDepth();
        }

        private float ComputeVertexDepth()
        {
            if (keyframes == null || keyframes.Count < 2) return 0f;

            // Find the keyframe where forward movement stops or reverses
            float prevDeltaY = 0f;
            float vertexY = 0f;
            for (int i = 0; i < keyframes.Count; i++)
            {
                foreach (var wp in keyframes[i].waypoints)
                {
                    float curDeltaY = wp.deltaYards;
                    if (i > 0 && prevDeltaY > 0f && curDeltaY <= prevDeltaY)
                    {
                        // Forward movement stopped or reversed — this is the vertex
                        vertexY = prevDeltaY;
                        return Mathf.Abs(vertexY);
                    }
                    prevDeltaY = curDeltaY;
                }
            }
            // No direction change found — vertex is at the end (straight route)
            return ComputeTotalDepth();
        }

        private float ComputeTotalDepth()
        {
            if (keyframes == null || keyframes.Count == 0) return 0f;
            float maxY = 0f;
            foreach (var kf in keyframes)
                foreach (var wp in kf.waypoints)
                    maxY = Mathf.Max(maxY, Mathf.Abs(wp.deltaYards));
            return maxY;
        }
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

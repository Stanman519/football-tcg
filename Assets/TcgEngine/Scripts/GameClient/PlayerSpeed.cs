using DG.Tweening;
using UnityEngine;

namespace TcgEngine.Client
{
    public enum SpeedTier { Walk, Jog, Sprint }

    public static class PlayerSpeed
    {
        public const float WalkYps = 1.5f;   // yards per second
        public const float JogYps = 4.5f;
        public const float SprintYps = 9.0f;

        public static float YardsPerSecond(SpeedTier tier) => tier switch
        {
            SpeedTier.Walk => WalkYps,
            SpeedTier.Jog => JogYps,
            SpeedTier.Sprint => SprintYps,
            _ => JogYps,
        };

        /// <summary>Duration to cover a distance at a given speed tier.</summary>
        public static float Duration(float distanceYards, SpeedTier tier)
        {
            float speed = YardsPerSecond(tier);
            return Mathf.Max(0.15f, Mathf.Abs(distanceYards) / speed);
        }

        /// <summary>Duration from world-space distance (magnitude).</summary>
        public static float Duration(Vector3 from, Vector3 to, SpeedTier tier)
        {
            float dist = Vector3.Distance(from, to);
            return Duration(dist, tier);
        }

        /// <summary>Default ease for single-segment movements.</summary>
        public static Ease DefaultEase(SpeedTier tier) => tier switch
        {
            SpeedTier.Walk => Ease.Linear,
            SpeedTier.Jog => Ease.InOutSine,
            SpeedTier.Sprint => Ease.InOutQuad,
            _ => Ease.InOutSine,
        };

        // ── Route-specific eases ──────────────────────────────

        public static readonly Ease FirstLegEase = Ease.InQuad;    // explode off the line
        public static readonly Ease MidLegEase = Ease.Linear;      // at top speed
        public static readonly Ease FinalLegEase = Ease.OutQuad;   // decel into cut/stop

        /// <summary>
        /// Compute speed multiplier for a route turn.
        /// Straight (0°) = 1.0, 90° cut = ~0.7, U-turn (180°) = 0.4.
        /// prevDir/curDir are direction vectors (not normalized required — we normalize internally).
        /// Returns 1.0 if either direction is zero-length (first leg or stationary).
        /// </summary>
        public static float TurnFactor(Vector2 prevDir, Vector2 curDir)
        {
            if (prevDir.sqrMagnitude < 0.001f || curDir.sqrMagnitude < 0.001f)
                return 1.0f;

            float cosAngle = Vector2.Dot(prevDir.normalized, curDir.normalized);
            cosAngle = Mathf.Clamp(cosAngle, -1f, 1f);
            // Maps: cos=1 (straight) → 1.0, cos=0 (90°) → 0.7, cos=-1 (180°) → 0.4
            return 0.4f + 0.6f * ((1f + cosAngle) / 2f);
        }

        /// <summary>
        /// Pick ease for a route leg based on turn angles entering and exiting this leg.
        /// </summary>
        public static Ease RouteLegEase(bool isFirstLeg, bool isLastLeg, float turnAngleIn, float turnAngleOut)
        {
            if (isFirstLeg) return FirstLegEase;
            if (isLastLeg) return FinalLegEase;
            // Approaching a sharp turn (>60°): decelerate into the break
            if (turnAngleOut > 60f) return Ease.OutQuad;
            // Exiting a sharp turn: accelerate out
            if (turnAngleIn > 60f) return Ease.InQuad;
            return MidLegEase;
        }

        /// <summary>Angle in degrees between two direction vectors (0–180).</summary>
        public static float AngleBetween(Vector2 a, Vector2 b)
        {
            if (a.sqrMagnitude < 0.001f || b.sqrMagnitude < 0.001f)
                return 0f;
            float dot = Vector2.Dot(a.normalized, b.normalized);
            dot = Mathf.Clamp(dot, -1f, 1f);
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }
    }
}

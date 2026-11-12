using UnityEngine;

namespace TcgEngine.Client
{
    /// <summary>
    /// Kinematic mover for one field token. Unlike a tween per segment, speed is CARRIED between
    /// legs: accelerate toward the leg's cruise speed, brake only as much as needed to hit the
    /// exit speed at the target (0 = full stop, >0 = running through a waypoint or cut).
    /// Plain C# (no Unity objects) so it is unit-testable. Units: yards, seconds.
    /// </summary>
    public class TokenMotion
    {
        public Vector2 Pos;
        public float Speed;              // current scalar speed (yd/s)
        public Vector2 Target { get; set; }
        public float Cruise { get; private set; }
        public float ExitSpeed { get; private set; }

        public float Accel = 9f;
        public float Decel = 12f;

        private const float MinCrawl = 0.35f; // never asymptotically creep into a stop

        public void SetLeg(Vector2 target, float cruise, float exitSpeed)
        {
            Target = target;
            Cruise = Mathf.Max(0.05f, cruise);
            ExitSpeed = Mathf.Clamp(exitSpeed, 0f, Cruise);
        }

        /// <summary>
        /// Advance by dt. Returns unused time (>0 only when the target was reached mid-step),
        /// so the caller can spend it on the next leg without a hitch.
        /// </summary>
        public float Step(float dt, out bool arrived)
        {
            arrived = false;
            if (dt <= 0f) return 0f;

            Vector2 to = Target - Pos;
            float rem = to.magnitude;
            if (rem < 0.0005f)
            {
                Pos = Target;
                Speed = Mathf.Min(Speed, ExitSpeed);
                arrived = true;
                return dt;
            }

            float v = Speed < Cruise
                ? Mathf.Min(Cruise, Speed + Accel * dt)
                : Mathf.Max(Cruise, Speed - Decel * dt);
            float brakeLimit = Mathf.Sqrt(ExitSpeed * ExitSpeed + 2f * Decel * rem);
            v = Mathf.Min(v, brakeLimit);
            v = Mathf.Max(v, Mathf.Min(MinCrawl, Cruise));

            float step = v * dt;
            if (step >= rem)
            {
                float used = rem / v;
                Pos = Target;
                Speed = Mathf.Min(v, ExitSpeed);
                arrived = true;
                return Mathf.Max(0f, dt - used);
            }

            Pos += to / rem * step;
            Speed = v;
            return 0f;
        }

        /// <summary>Closed-form time to cover dist starting at v0, cruising at cruise, arriving at vExit.</summary>
        public static float EstimateTime(float dist, float v0, float cruise, float vExit, float accel, float decel)
        {
            if (dist <= 0.0005f) return 0f;
            cruise = Mathf.Max(0.05f, cruise);
            v0 = Mathf.Clamp(v0, 0f, cruise);
            vExit = Mathf.Clamp(vExit, 0f, cruise);

            float dAcc = (cruise * cruise - v0 * v0) / (2f * accel);
            float dDec = (cruise * cruise - vExit * vExit) / (2f * decel);
            if (dAcc + dDec <= dist)
                return (cruise - v0) / accel + (cruise - vExit) / decel + (dist - dAcc - dDec) / cruise;

            // Never reaches cruise: triangular profile
            float vp2 = (2f * accel * decel * dist + decel * v0 * v0 + accel * vExit * vExit) / (accel + decel);
            float vp = Mathf.Sqrt(Mathf.Max(vp2, 0f));
            vp = Mathf.Max(vp, Mathf.Max(v0, vExit));
            return (vp - v0) / accel + (vp - vExit) / decel;
        }
    }
}

using NUnit.Framework;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.Tests
{
    /// <summary>Kinematics of the per-token field mover (pure math, no scene).</summary>
    public class TokenMotionTests
    {
        private const float Dt = 1f / 60f;

        private static TokenMotion Make(float accel = 9f, float decel = 12f)
        {
            return new TokenMotion { Accel = accel, Decel = decel, Pos = Vector2.zero, Speed = 0f };
        }

        /// <summary>Run one leg to completion; returns elapsed seconds and the peak speed seen.</summary>
        private static float Run(TokenMotion m, out float peak, float maxSeconds = 60f)
        {
            float t = 0f;
            peak = m.Speed;
            while (t < maxSeconds)
            {
                float left = m.Step(Dt, out bool arrived);
                peak = Mathf.Max(peak, m.Speed);
                t += Dt - left;
                if (arrived) return t;
            }
            Assert.Fail("mover never arrived");
            return t;
        }

        [Test]
        public void ArrivesExactlyAtTarget_AndStops()
        {
            TokenMotion m = Make();
            m.SetLeg(new Vector2(3f, 10f), 5.5f, 0f);
            Run(m, out _);

            Assert.AreEqual(3f, m.Pos.x, 1e-4f);
            Assert.AreEqual(10f, m.Pos.y, 1e-4f);
            Assert.AreEqual(0f, m.Speed, 1e-4f);
        }

        [Test]
        public void NeverExceedsCruiseSpeed()
        {
            TokenMotion m = Make();
            m.SetLeg(new Vector2(0f, 30f), 5.5f, 0f);
            Run(m, out float peak);

            Assert.LessOrEqual(peak, 5.5f + 1e-3f);
            Assert.Greater(peak, 5.4f); // long enough to actually reach it
        }

        [Test]
        public void SprintIsReadable_NotInstant()
        {
            // The old engine covered 4 yards in 0.44s. At presentation speed it must take real time.
            TokenMotion m = Make();
            m.SetLeg(new Vector2(0f, 4f), 5.5f, 0f);
            float t = Run(m, out _);

            Assert.Greater(t, 0.9f);
            Assert.Less(t, 2.0f);
        }

        [Test]
        public void SpeedCarriesThroughStraightWaypoint()
        {
            TokenMotion m = Make();
            m.SetLeg(new Vector2(0f, 10f), 5.5f, 5.5f); // run THROUGH the waypoint
            Run(m, out _);
            Assert.Greater(m.Speed, 5f, "should still be at speed at the waypoint");

            float before = m.Speed;
            m.SetLeg(new Vector2(0f, 20f), 5.5f, 0f);
            m.Step(Dt, out _);
            Assert.GreaterOrEqual(m.Speed, before - 0.5f, "no stop-and-go at the waypoint");
        }

        [Test]
        public void BrakesToExitSpeedForACut()
        {
            TokenMotion m = Make();
            m.SetLeg(new Vector2(0f, 12f), 5.5f, 2f); // sharp cut coming: carry only 2 yd/s out
            Run(m, out float peak);

            Assert.Greater(peak, 5f);
            Assert.LessOrEqual(m.Speed, 2f + 1e-3f);
        }

        [Test]
        public void LeftoverTimeIsReturnedOnArrival()
        {
            TokenMotion m = Make();
            m.Speed = 5f;
            m.SetLeg(new Vector2(0f, 0.01f), 5f, 5f); // reached almost immediately
            float left = m.Step(Dt, out bool arrived);

            Assert.IsTrue(arrived);
            Assert.Greater(left, Dt * 0.5f, "most of the frame should be handed to the next leg");
        }

        [Test]
        public void EstimateTime_MatchesSimulation()
        {
            foreach (float dist in new[] { 1.5f, 4f, 12f, 40f })
            {
                TokenMotion m = Make();
                m.SetLeg(new Vector2(0f, dist), 5.5f, 0f);
                float simulated = Run(m, out _);
                float estimated = TokenMotion.EstimateTime(dist, 0f, 5.5f, 0f, 9f, 12f);

                Assert.AreEqual(simulated, estimated, 0.15f, $"dist {dist}");
            }
        }

        [Test]
        public void EstimateTime_ZeroDistanceIsZero()
        {
            Assert.AreEqual(0f, TokenMotion.EstimateTime(0f, 0f, 5.5f, 0f, 9f, 12f));
        }

        [Test]
        public void ProfileDefaults_AreSlowerThanRealFootball()
        {
            FieldMotionProfile p = ScriptableObject.CreateInstance<FieldMotionProfile>();
            Assert.Less(p.sprintYps, 9f);
            Assert.Less(p.walkYps, p.jogYps);
            Assert.Less(p.jogYps, p.sprintYps);
            Assert.Greater(p.minMoveDuration, 0.15f);
            Object.DestroyImmediate(p);
        }
    }
}

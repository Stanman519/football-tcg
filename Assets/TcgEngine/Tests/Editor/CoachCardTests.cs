using NUnit.Framework;
using System;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine.Tests
{
    public class CoachCardTests
    {
        // ── HeadCoachCard defaults ────────────────────────────────────────────

        [Test]
        public void HeadCoachCard_Default_QB_Limit_Is_One()
        {
            var coach = new HeadCoachCard();
            Assert.AreEqual(1, coach.positional_Scheme[PlayerPositionGrp.QB].pos_max);
        }

        [Test]
        public void HeadCoachCard_InitFromData_Overrides_PositionalLimit()
        {
            var data = ScriptableObject.CreateInstance<CoachCardData>();
            data.positionalScheme = new CoachSchemeEntry[]
            {
                new CoachSchemeEntry { position = PlayerPositionGrp.QB, maxCards = 2 }
            };
            var coach = new HeadCoachCard();
            coach.InitFromData(data);
            Assert.AreEqual(2, coach.positional_Scheme[PlayerPositionGrp.QB].pos_max);
        }

        [Test]
        public void HeadCoachCard_InitFromData_SetsCoachType()
        {
            var profile = ScriptableObject.CreateInstance<CoachData>();
            profile.coachType = CoachType.Aggressive;

            var data = ScriptableObject.CreateInstance<CoachCardData>();
            data.coachProfile = profile;

            var coach = new HeadCoachCard();
            coach.InitFromData(data);

            Assert.AreEqual(CoachType.Aggressive, coach.coachType);
        }

        // ── CoachManager coverage modifier ───────────────────────────────────

        [Test]
        public void CoverageModifier_CorrectGuess_ReturnsBonus()
        {
            var coachData = ScriptableObject.CreateInstance<CoachData>();
            coachData.coverageBonusCorrect = 2;
            coachData.coveragePenaltyWrong = 3;
            var mgr = new CoachManager(coachData, null, null, null);

            Assert.AreEqual(2, mgr.GetCoverageModifier(true));
        }

        [Test]
        public void CoverageModifier_WrongGuess_ReturnsPenalty()
        {
            var coachData = ScriptableObject.CreateInstance<CoachData>();
            coachData.coverageBonusCorrect = 2;
            coachData.coveragePenaltyWrong = 3;
            var mgr = new CoachManager(coachData, null, null, null);

            Assert.AreEqual(-3, mgr.GetCoverageModifier(false));
        }

        [Test]
        public void CoverageModifier_Aggressive_HigherPenalty_Than_Balanced()
        {
            var balancedData = ScriptableObject.CreateInstance<CoachData>();
            balancedData.coachType = CoachType.Balanced;
            balancedData.coveragePenaltyWrong = 1;

            var aggressiveData = ScriptableObject.CreateInstance<CoachData>();
            aggressiveData.coachType = CoachType.Aggressive;
            aggressiveData.coveragePenaltyWrong = 4;

            var balanced = new CoachManager(balancedData, null, null, null);
            var aggressive = new CoachManager(aggressiveData, null, null, null);

            Assert.Less(aggressive.GetCoverageModifier(false), balanced.GetCoverageModifier(false));
        }

        // ── Coverage guess effect on yardage ─────────────────────────────────

        [Test]
        public void Coverage_CorrectGuess_IncreasesEffectiveDefense()
        {
            var coachData = ScriptableObject.CreateInstance<CoachData>();
            coachData.coverageBonusCorrect = 3;
            coachData.coveragePenaltyWrong = 4;
            var mgr = new CoachManager(coachData, null, null, null);

            int baseDefCoverage = 5;
            int effective = baseDefCoverage + mgr.GetCoverageModifier(true); // 5 + 3
            Assert.AreEqual(8, effective);
        }

        [Test]
        public void Coverage_WrongGuess_DecreasesEffectiveDefense()
        {
            var coachData = ScriptableObject.CreateInstance<CoachData>();
            coachData.coverageBonusCorrect = 3;
            coachData.coveragePenaltyWrong = 4;
            var mgr = new CoachManager(coachData, null, null, null);

            int baseDefCoverage = 5;
            int effective = Math.Max(0, baseDefCoverage + mgr.GetCoverageModifier(false)); // max(0, 5 - 4)
            Assert.AreEqual(1, effective);
        }

        [Test]
        public void Coverage_WrongGuess_AggressiveCoach_CanReduceCoverageToZero()
        {
            var coachData = ScriptableObject.CreateInstance<CoachData>();
            coachData.coveragePenaltyWrong = 10;
            var mgr = new CoachManager(coachData, null, null, null);

            int baseDefCoverage = 5;
            int effective = Math.Max(0, baseDefCoverage + mgr.GetCoverageModifier(false)); // max(0, 5 - 10)
            Assert.AreEqual(0, effective);
        }
    }
}

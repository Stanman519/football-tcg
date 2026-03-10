using NUnit.Framework;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.Tests
{
    public class FootballMathTests
    {
        [Test]
        public void CalcRunYardage_Standard()
        {
            // (3+6+2) - (4+1) = 11 - 5 = 6
            int result = FootballMath.CalcRunYardage(
                coachBase: 3, playerRunBase: 6, playerStatusBonus: 2,
                defCoverageBase: 4, defStatusBonus: 1);
            Assert.AreEqual(6, result);
        }

        [Test]
        public void CalcPassYardage_Standard()
        {
            // 8+2+3+3-5 = 11
            int result = FootballMath.CalcPassYardage(
                receiverBase: 8, receiverStatus: 2, otherOffYardage: 3,
                coachYardage: 3, defYardage: 5);
            Assert.AreEqual(11, result);
        }

        [Test]
        public void CalcGritContest_DefWins_Turnover()
        {
            var (turnover, yardage) = FootballMath.CalcGritContest(offGrit: 4, defGrit: 7);
            Assert.IsTrue(turnover);
            Assert.AreEqual(6, yardage); // 2 * |7-4|
        }

        [Test]
        public void CalcGritContest_OffWins_NoTurnover()
        {
            var (turnover, yardage) = FootballMath.CalcGritContest(offGrit: 5, defGrit: 3);
            Assert.IsFalse(turnover);
            Assert.AreEqual(2, yardage); // 1 * |5-3|
        }

        [Test]
        public void CalcGritContest_Tie_NoTurnoverZeroYards()
        {
            // Tie goes to offense — no turnover, no return yards
            var (turnover, yardage) = FootballMath.CalcGritContest(offGrit: 5, defGrit: 5);
            Assert.IsFalse(turnover);
            Assert.AreEqual(0, yardage);
        }

        [Test]
        public void ApplyPreventLoss_PartialClamp()
        {
            int result = FootballMath.ApplyPreventLoss(-8, 5);
            Assert.AreEqual(-3, result);
        }

        [Test]
        public void ApplyPreventLoss_FullClamp_Magic999()
        {
            int result = FootballMath.ApplyPreventLoss(-3, 999);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void ApplyPreventLoss_NoOp_WhenPositive()
        {
            int result = FootballMath.ApplyPreventLoss(4, 5);
            Assert.AreEqual(4, result);
        }
    }
}

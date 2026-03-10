using NUnit.Framework;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.Tests
{
    public class ScoringTests
    {
        private class TestableGLS : GameLogicService
        {
            public TestableGLS() : base(false) { }
            public override void StartTurn() { }
            public override void RefreshData() { }
        }

        private TestableGLS MakeGLS(int ballOn, int yardage, out Player offense, out Player defense)
        {
            var game = new Game();
            offense = new Player(0);
            defense = new Player(1);
            game.players = new Player[] { offense, defense };
            game.current_offensive_player = offense;
            game.raw_ball_on = ballOn;
            game.yardage_this_play = yardage;
            game.current_down = 2;       // not >4 — won't trigger turnover-on-downs
            game.plays_left_in_half = 5; // not 0 — won't trigger half/game end
            game.turnover_pending = false;
            var gls = new TestableGLS();
            gls.game_data = game;
            return gls;
        }

        // ── Touchdown ─────────────────────────────────────────────────────────

        [Test]
        public void Touchdown_Awards7Points()
        {
            var gls = MakeGLS(ballOn: 80, yardage: 25, out var offense, out _);
            gls.EndPlayPhase();
            Assert.AreEqual(7, offense.points);
        }

        [Test]
        public void Touchdown_SwitchesPossession()
        {
            var gls = MakeGLS(ballOn: 80, yardage: 25, out _, out var defense);
            gls.EndPlayPhase();
            Assert.AreEqual(defense, gls.game_data.current_offensive_player);
        }

        [Test]
        public void Touchdown_ResetsBallTo25()
        {
            var gls = MakeGLS(ballOn: 80, yardage: 25, out _, out _);
            gls.EndPlayPhase();
            Assert.AreEqual(25, gls.game_data.raw_ball_on);
        }

        // ── Safety ────────────────────────────────────────────────────────────

        [Test]
        public void Safety_Awards2PointsToDefense()
        {
            var gls = MakeGLS(ballOn: 5, yardage: -10, out _, out var defense);
            gls.EndPlayPhase();
            Assert.AreEqual(2, defense.points);
        }

        [Test]
        public void Safety_SwitchesPossessionToDefense()
        {
            var gls = MakeGLS(ballOn: 5, yardage: -10, out _, out var defense);
            gls.EndPlayPhase();
            Assert.AreEqual(defense, gls.game_data.current_offensive_player);
        }

        [Test]
        public void Safety_ResetsBallTo40()
        {
            var gls = MakeGLS(ballOn: 5, yardage: -10, out _, out _);
            gls.EndPlayPhase();
            Assert.AreEqual(40, gls.game_data.raw_ball_on);
        }
    }
}

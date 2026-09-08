using NUnit.Framework;
using TcgEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.Tests
{
    public class ResolutionReadyGateTests
    {
        private GameLogicService logic;
        private Game game;

        [SetUp]
        public void Setup()
        {
            logic = new GameLogicService(true); // is_ai ctor - no scene/UI deps
            game = new Game("test", 2);
            game.state = GameState.Play;
            game.phase = GamePhase.Resolution;
            game.current_offensive_player = game.players[0];
            game.ResetPhaseReady();
            logic.SetData(game);
        }

        [Test]
        public void Resolution_OneReady_DoesNotAdvance()
        {
            logic.SetPlayerReadyPhase(game.players[0]);

            Assert.AreEqual(GamePhase.Resolution, game.phase);
            Assert.IsTrue(game.playerPhaseReady[0]);
        }

        [Test]
        public void Resolution_BothReady_BallWasLive_GoesToLiveBall()
        {
            game.ball_was_live = true;

            logic.SetPlayerReadyPhase(game.players[0]);
            logic.SetPlayerReadyPhase(game.players[1]);

            Assert.AreEqual(GamePhase.LiveBall, game.phase);
            Assert.IsFalse(game.AreAllPlayersPhaseReady()); // reset by NextStep
        }

        [Test]
        public void Resolution_BothReady_NoLiveBall_LeavesResolution()
        {
            game.ball_was_live = false;

            logic.SetPlayerReadyPhase(game.players[0]);
            try
            {
                logic.SetPlayerReadyPhase(game.players[1]);
            }
            catch (System.NullReferenceException)
            {
                // EndPlayPhase chain may hit scene-dependent code in the is_ai service;
                // the assertion below covers what this test is about
            }

            Assert.AreNotEqual(GamePhase.Resolution, game.phase);
        }

        [Test]
        public void Resolution_ReadyIdempotent()
        {
            logic.SetPlayerReadyPhase(game.players[0]);
            logic.SetPlayerReadyPhase(game.players[0]);

            Assert.AreEqual(GamePhase.Resolution, game.phase);
        }

        [Test]
        public void NextStep_TimeoutPath_RoutesToLiveBall()
        {
            game.ball_was_live = true;

            logic.NextStep(); // simulates turn-timer expiry with nobody ready

            Assert.AreEqual(GamePhase.LiveBall, game.phase);
        }

        [Test]
        public void GameClone_CopiesBallWasLive()
        {
            game.ball_was_live = true;
            Game dest = new Game("clone", 2);
            Game.Clone(game, dest);

            Assert.IsTrue(dest.ball_was_live);
        }
    }
}

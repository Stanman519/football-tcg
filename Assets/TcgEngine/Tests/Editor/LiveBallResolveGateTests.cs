using NUnit.Framework;
using TcgEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.Tests
{
    /// <summary>
    /// LiveBall -> LiveBallResolve (ready-gated while clients animate) -> EndTurn.
    /// Runs the non-predict path: the gate is skipped in AI simulations.
    /// </summary>
    public class LiveBallResolveGateTests
    {
        private GameLogicService logic;
        private Game game;

        [SetUp]
        public void Setup()
        {
            game = new Game("test", 2);
            game.state = GameState.Play;
            game.phase = GamePhase.LiveBall;
            game.current_offensive_player = game.players[0];
            game.ResetPhaseReady();
            // is_ai ctor = no scene deps + immediate resolve queue; flip the predict flag off so the
            // real (client-facing) path runs. The full ctor needs scene data.
            logic = new GameLogicService(true);
            typeof(GameLogicService).GetField("is_ai_predict",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(logic, false);
            logic.SetData(game);
        }

        private void Pump(int frames = 5)
        {
            // immediate queue: nothing to pump (kept so tests read like the server flow)
        }

        [Test]
        public void LiveBall_BothPass_WaitsInLiveBallResolve()
        {
            logic.NextStep();
            Pump();

            Assert.AreEqual(GamePhase.LiveBallResolve, game.phase);
            Assert.IsFalse(game.AreAllPlayersPhaseReady());
            Assert.Greater(game.turn_timer, 0f); // timeout fallback armed
        }

        [Test]
        public void LiveBallResolve_OneReady_DoesNotAdvance()
        {
            logic.NextStep();
            Pump();

            logic.SetPlayerReadyPhase(game.players[0]);
            Pump();

            Assert.AreEqual(GamePhase.LiveBallResolve, game.phase);
        }

        [Test]
        public void LiveBallResolve_BothReady_EndsTurn()
        {
            bool turnEnded = false;
            logic.onTurnEnd += () => turnEnded = true;
            logic.NextStep();
            Pump();

            logic.SetPlayerReadyPhase(game.players[0]);
            try { logic.SetPlayerReadyPhase(game.players[1]); Pump(); }
            catch (System.NullReferenceException) { } // scene-dependent tail after EndTurn

            Assert.IsTrue(turnEnded);
        }

        [Test]
        public void LiveBallResolve_Timeout_EndsTurn()
        {
            bool turnEnded = false;
            logic.onTurnEnd += () => turnEnded = true;
            logic.NextStep();
            Pump();

            try { logic.NextStep(); Pump(); } // server turn-timer expiry
            catch (System.NullReferenceException) { }

            Assert.IsTrue(turnEnded);
        }

        [Test]
        public void AiPredict_SkipsGate()
        {
            var sim = new GameLogicService(true);
            sim.SetData(game);
            bool turnEnded = false;
            sim.onTurnEnd += () => turnEnded = true;

            try { sim.NextStep(); }
            catch (System.NullReferenceException) { }

            Assert.IsTrue(turnEnded);
            Assert.AreNotEqual(GamePhase.LiveBallResolve, game.phase);
        }
    }
}

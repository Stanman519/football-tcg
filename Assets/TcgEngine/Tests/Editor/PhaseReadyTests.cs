using NUnit.Framework;
using TcgEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.Tests
{
    public class PhaseReadyTests
    {
        private GameLogicService logic;
        private Game game;

        [SetUp]
        public void Setup()
        {
            logic = new GameLogicService(true); // is_ai ctor - no scene/UI dependencies
            game = new Game("test", 2);
            game.state = GameState.Play;
            game.phase = GamePhase.ChoosePlayers;
            game.ResetPhaseReady();
            logic.SetData(game);
        }

        [Test]
        public void SetPlayerReadyPhase_OnePlayerReady_DoesNotAdvance()
        {
            logic.SetPlayerReadyPhase(game.players[0]);

            Assert.AreEqual(GamePhase.ChoosePlayers, game.phase);
            Assert.IsTrue(game.players[0].IsReadyForPhase(GamePhase.ChoosePlayers));
            Assert.IsTrue(game.playerPhaseReady[0]);
            Assert.IsFalse(game.playerPhaseReady[1]);
        }

        [Test]
        public void SetPlayerReadyPhase_BothReady_AdvancesFromChoosePlayers()
        {
            logic.SetPlayerReadyPhase(game.players[0]);
            logic.SetPlayerReadyPhase(game.players[1]);

            Assert.AreNotEqual(GamePhase.ChoosePlayers, game.phase);
            Assert.IsFalse(game.AreAllPlayersPhaseReady()); // reset by NextStep
        }

        [Test]
        public void SetPlayerReadyPhase_Idempotent()
        {
            logic.SetPlayerReadyPhase(game.players[0]);
            logic.SetPlayerReadyPhase(game.players[0]); // duplicate - no-op

            Assert.AreEqual(GamePhase.ChoosePlayers, game.phase);

            logic.SetPlayerReadyPhase(game.players[1]);
            Assert.AreNotEqual(GamePhase.ChoosePlayers, game.phase);
        }

        [Test]
        public void SetPlayerReadyPhase_IgnoredInNonWaitingPhase()
        {
            game.phase = GamePhase.SlotSpin;

            logic.SetPlayerReadyPhase(game.players[0]);

            Assert.AreEqual(GamePhase.SlotSpin, game.phase);
            Assert.IsFalse(game.playerPhaseReady[0]);
        }

        [Test]
        public void SelectPlay_BothSelected_AdvancesAndClearsReady()
        {
            game.phase = GamePhase.ChoosePlay;
            game.playerPhaseReady[0] = true; // stale flag from earlier phase

            logic.SelectPlay(game.players[0], PlayType.Run, null);
            Assert.AreEqual(GamePhase.ChoosePlay, game.phase); // only one selected

            try
            {
                logic.SelectPlay(game.players[1], PlayType.ShortPass, null);
            }
            catch (System.NullReferenceException)
            {
                // downstream StartSlotSpinPhase touches slot machine UI (null in test ctor);
                // the assertions below cover what this test is about
            }

            Assert.AreNotEqual(GamePhase.ChoosePlay, game.phase);
            Assert.IsFalse(game.AreAllPlayersPhaseReady()); // stale flag cleared by NextStep
        }

        [Test]
        public void NextStep_ResetsPhaseReady()
        {
            game.playerPhaseReady[0] = true;
            game.playerPhaseReady[1] = true;

            logic.NextStep();

            Assert.IsFalse(game.AreAllPlayersPhaseReady());
        }

        [Test]
        public void PlayerClone_CopiesReadyForPhase()
        {
            game.players[0].SetReadyForPhase(GamePhase.ChoosePlayers, true);

            Player dest = new Player(0);
            Player.Clone(game.players[0], dest);

            Assert.IsTrue(dest.IsReadyForPhase(GamePhase.ChoosePlayers));
            Assert.IsFalse(dest.IsReadyForPhase(GamePhase.ChoosePlay));
        }
    }
}

using NUnit.Framework;
using TcgEngine;
using TcgEngine.Gameplay;
using UnityEngine;

namespace TcgEngine.Tests
{
    public class TacklerSelectorTests
    {
        private static VariantData _variant;
        private static VariantData SharedVariant =>
            _variant != null ? _variant : (_variant = ScriptableObject.CreateInstance<VariantData>());

        private Game game;
        private Player offense;
        private Player defense;

        [SetUp]
        public void Setup()
        {
            offense = new Player(0);
            defense = new Player(1);
            game = new Game();
            game.players = new Player[] { offense, defense };
            game.current_offensive_player = offense;
            game.raw_ball_on = 50;
            game.yardage_this_play = 0;
            game.last_fail_event = FailPlayEventType.None;
            game.state = GameState.Play;
        }

        private Card AddDefCard(PlayerPositionGrp pos, int grit)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.type = CardType.DefensivePlayer;
            data.playerPosition = pos;
            data.grit = grit;
            var card = Card.Create(data, SharedVariant, defense);
            defense.cards_board.Add(card);
            return card;
        }

        [Test]
        public void Touchdown_ReturnsNull()
        {
            AddDefCard(PlayerPositionGrp.DB, 5);
            game.raw_ball_on = 95;
            game.yardage_this_play = 10;

            Assert.IsNull(TacklerSelector.Choose(game));
        }

        [Test]
        public void Sack_ReturnsFailCaster()
        {
            game.last_fail_event = FailPlayEventType.Sack;
            game.fail_event_caster_uid = "sacker-uid";

            Assert.AreEqual("sacker-uid", TacklerSelector.Choose(game));
        }

        [Test]
        public void TackleForLoss_ReturnsFailCaster()
        {
            game.last_fail_event = FailPlayEventType.TackleForLoss;
            game.fail_event_caster_uid = "tfl-uid";
            game.yardage_this_play = -4;

            Assert.AreEqual("tfl-uid", TacklerSelector.Choose(game));
        }

        [Test]
        public void Incomplete_ReturnsNull()
        {
            AddDefCard(PlayerPositionGrp.DB, 5);
            game.last_fail_event = FailPlayEventType.IncompletePass;

            Assert.IsNull(TacklerSelector.Choose(game));
        }

        [Test]
        public void ShortGain_PrefersDL()
        {
            var dl = AddDefCard(PlayerPositionGrp.DL, 3);
            AddDefCard(PlayerPositionGrp.DB, 9);
            game.yardage_this_play = 2;

            Assert.AreEqual(dl.uid, TacklerSelector.Choose(game));
        }

        [Test]
        public void MidGain_PrefersLB()
        {
            AddDefCard(PlayerPositionGrp.DL, 9);
            var lb = AddDefCard(PlayerPositionGrp.LB, 3);
            game.yardage_this_play = 5;

            Assert.AreEqual(lb.uid, TacklerSelector.Choose(game));
        }

        [Test]
        public void LongGain_PrefersDB()
        {
            AddDefCard(PlayerPositionGrp.DL, 9);
            var db = AddDefCard(PlayerPositionGrp.DB, 3);
            game.yardage_this_play = 12;

            Assert.AreEqual(db.uid, TacklerSelector.Choose(game));
        }

        [Test]
        public void MissingGroup_FallsThrough()
        {
            var lb = AddDefCard(PlayerPositionGrp.LB, 3);
            game.yardage_this_play = 1; // wants DL, none on board

            Assert.AreEqual(lb.uid, TacklerSelector.Choose(game));
        }

        [Test]
        public void GritTieBreak_HighestWins()
        {
            AddDefCard(PlayerPositionGrp.LB, 2);
            var strong = AddDefCard(PlayerPositionGrp.LB, 8);
            game.yardage_this_play = 5;

            Assert.AreEqual(strong.uid, TacklerSelector.Choose(game));
        }

        [Test]
        public void Deterministic_SameInputSamePick()
        {
            AddDefCard(PlayerPositionGrp.DB, 4);
            AddDefCard(PlayerPositionGrp.DB, 4);
            game.yardage_this_play = 10;

            Assert.AreEqual(TacklerSelector.Choose(game), TacklerSelector.Choose(game));
        }

        [Test]
        public void EmptyDefBoard_ReturnsNull()
        {
            game.yardage_this_play = 5;

            Assert.IsNull(TacklerSelector.Choose(game));
        }

        [Test]
        public void GameClone_CopiesOutcomeFacts()
        {
            game.fail_event_caster_uid = "a";
            game.tackler_uid = "b";
            game.ctr_defender_uid = "c";
            game.ctr_covered_receiver_uid = "d";
            game.last_fail_event = FailPlayEventType.Sack;
            game.turnover_pending = true;

            Game dest = new Game("clone", 2);
            Game.Clone(game, dest);

            Assert.AreEqual("a", dest.fail_event_caster_uid);
            Assert.AreEqual("b", dest.tackler_uid);
            Assert.AreEqual("c", dest.ctr_defender_uid);
            Assert.AreEqual("d", dest.ctr_covered_receiver_uid);
            Assert.AreEqual(FailPlayEventType.Sack, dest.last_fail_event);
            Assert.IsTrue(dest.turnover_pending);
        }
    }
}

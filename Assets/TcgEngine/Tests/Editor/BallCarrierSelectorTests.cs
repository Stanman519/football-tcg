using NUnit.Framework;
using TcgEngine;
using TcgEngine.Gameplay;
using UnityEngine;

namespace TcgEngine.Tests
{
    /// <summary>RBs and TEs share the RB_TE group; the handoff goes to a running back.</summary>
    public class BallCarrierSelectorTests
    {
        private Game game;
        private Player offense;
        private VariantData variant;

        [SetUp]
        public void Setup()
        {
            game = new Game("test", 2);
            offense = game.players[0];
            variant = ScriptableObject.CreateInstance<VariantData>();
        }

        private Card Add(PlayerPositionGrp pos, int runBonus, bool tightEnd = false)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.type = CardType.OffensivePlayer;
            data.playerPosition = pos;
            data.run_bonus = runBonus;
            data.isTightEnd = tightEnd;
            var card = Card.Create(data, variant, offense);
            offense.cards_board.Add(card);
            return card;
        }

        [Test]
        public void RunningBack_BeatsTightEnd_EvenWithLowerRunBonus()
        {
            Add(PlayerPositionGrp.RB_TE, 5, tightEnd: true);
            Card rb = Add(PlayerPositionGrp.RB_TE, 1);

            Assert.AreSame(rb, BallCarrierSelector.Pick(offense));
        }

        [Test]
        public void BestRunBonus_AmongRunningBacks()
        {
            Add(PlayerPositionGrp.RB_TE, 2);
            Card best = Add(PlayerPositionGrp.RB_TE, 4);

            Assert.AreSame(best, BallCarrierSelector.Pick(offense));
        }

        [Test]
        public void TightEnd_OnlyWhenNoRunningBack()
        {
            Card te = Add(PlayerPositionGrp.RB_TE, 1, tightEnd: true);
            Add(PlayerPositionGrp.WR, 9);

            Assert.AreSame(te, BallCarrierSelector.Pick(offense));
        }

        [Test]
        public void NoBackfield_ReturnsNull()
        {
            Add(PlayerPositionGrp.WR, 3);
            Add(PlayerPositionGrp.QB, 3);

            Assert.IsNull(BallCarrierSelector.Pick(offense));
        }
    }
}

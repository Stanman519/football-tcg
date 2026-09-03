using NUnit.Framework;
using TcgEngine;
using TcgEngine.Gameplay;
using UnityEngine;

namespace TcgEngine.Tests
{
    /// <summary>
    /// Phase x CardType x side-of-ball eligibility. Player 0 = offense, player 1 = defense.
    /// </summary>
    public class CardEligibilityTests
    {
        private GameLogicService logic;
        private Game game;
        private Player off;
        private Player def;

        private static VariantData _variant;
        private static VariantData Variant =>
            _variant != null ? _variant : (_variant = ScriptableObject.CreateInstance<VariantData>());

        [SetUp]
        public void Setup()
        {
            logic = new GameLogicService(true);
            game = new Game("test", 2);
            game.state = GameState.Play;
            game.phase = GamePhase.ChoosePlayers;
            off = game.players[0];
            def = game.players[1];
            game.current_offensive_player = off;
            game.ResetPhaseReady();
            logic.SetData(game);
        }

        private Card Make(Player p, CardType type, PlayerPositionGrp pos = PlayerPositionGrp.NONE,
                          CardSuit suit = CardSuit.None, PlayType[] requiredPlays = null, SlotRequirement[] slotReqs = null)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.id = type + "_" + pos + "_" + GameTool.GenerateRandomID(4, 6);
            data.type = type;
            data.playerPosition = pos;
            data.suit = suit;
            data.required_plays = requiredPlays ?? new PlayType[0];
            data.slotRequirements = slotReqs ?? new SlotRequirement[0];
            var card = Card.Create(data, Variant, p);
            p.cards_hand.Add(card);
            return card;
        }

        private static CardPositionSlot Slot(Player p, PlayerPositionGrp pos, int idx = 1)
            => new CardPositionSlot(p.player_id, idx, pos);

        // ── Player cards ───────────────────────────────────────────────────────

        [Test]
        public void Defense_CanPlayDefensivePlayer_InChoosePlayers()
        {
            var lb = Make(def, CardType.DefensivePlayer, PlayerPositionGrp.LB);
            Assert.AreEqual(CardPlayError.None, game.GetCardPlayError(lb, Slot(def, PlayerPositionGrp.LB)));
            Assert.IsTrue(game.CanPlayCard(lb, Slot(def, PlayerPositionGrp.LB)));
        }

        [Test]
        public void Offense_CanPlayOffensivePlayer_InChoosePlayers()
        {
            var wr = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            Assert.IsTrue(game.CanPlayCard(wr, Slot(off, PlayerPositionGrp.WR)));
        }

        [Test]
        public void Offense_CannotPlayDefensivePlayer()
        {
            var lb = Make(off, CardType.DefensivePlayer, PlayerPositionGrp.LB);
            Assert.AreEqual(CardPlayError.WrongSide, game.GetCardPlayError(lb, Slot(off, PlayerPositionGrp.LB)));
        }

        [Test]
        public void Defense_CannotPlayOffensivePlayer()
        {
            var wr = Make(def, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            Assert.AreEqual(CardPlayError.WrongSide, game.GetCardPlayError(wr, Slot(def, PlayerPositionGrp.WR)));
        }

        [Test]
        public void PlayerCard_RejectedOutsideChoosePlayers()
        {
            var wr = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            game.phase = GamePhase.ChoosePlay;
            Assert.AreEqual(CardPlayError.WrongPhase, game.GetCardPlayError(wr, Slot(off, PlayerPositionGrp.WR)));
            game.phase = GamePhase.LiveBall;
            Assert.AreEqual(CardPlayError.WrongPhase, game.GetCardPlayError(wr, Slot(off, PlayerPositionGrp.WR)));
        }

        [Test]
        public void PlayerCard_WrongSlotType_Rejected()
        {
            var wr = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            Assert.AreEqual(CardPlayError.InvalidSlot, game.GetCardPlayError(wr, Slot(off, PlayerPositionGrp.QB)));
        }

        [Test]
        public void PlayerCard_SuitDuplicate_Rejected()
        {
            var a = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR, CardSuit.Hearts);
            var b = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR, CardSuit.Hearts);
            logic.PlayCard(a, Slot(off, PlayerPositionGrp.WR, 1));
            Assert.Contains(a, off.cards_board);
            Assert.AreEqual(CardPlayError.SuitUsed, game.GetCardPlayError(b, Slot(off, PlayerPositionGrp.WR, 2)));
        }

        [Test]
        public void PlayerCard_SlotOccupied_Rejected()
        {
            var a = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            var b = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            logic.PlayCard(a, Slot(off, PlayerPositionGrp.WR, 1));
            Assert.AreEqual(CardPlayError.SlotOccupied, game.GetCardPlayError(b, Slot(off, PlayerPositionGrp.WR, 1)));
            Assert.AreEqual(CardPlayError.None, game.GetCardPlayError(b, Slot(off, PlayerPositionGrp.WR, 2)));
        }

        [Test]
        public void PlayerCard_PositionMax_Rejected()
        {
            // Default coach: QB pos_max = 1
            var q1 = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.QB);
            var q2 = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.QB);
            logic.PlayCard(q1, Slot(off, PlayerPositionGrp.QB, 1));
            Assert.AreEqual(CardPlayError.PositionFull, game.GetCardPlayError(q2, Slot(off, PlayerPositionGrp.QB, 2)));
        }

        [Test]
        public void PlayerCard_MissingSchemeKey_DoesNotThrow()
        {
            off.head_coach.positional_Scheme.Remove(PlayerPositionGrp.WR);
            var wr = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            Assert.DoesNotThrow(() => game.GetCardPlayError(wr, Slot(off, PlayerPositionGrp.WR)));
        }

        [Test]
        public void GetEmptySlotsForPosition_UsesCoachScheme()
        {
            // Default coach: WR field_slots = 3
            Assert.AreEqual(3, off.GetEmptySlotsForPosition(PlayerPositionGrp.WR).Count);
            var wr = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            logic.PlayCard(wr, Slot(off, PlayerPositionGrp.WR, 2));
            Assert.AreEqual(2, off.GetEmptySlotsForPosition(PlayerPositionGrp.WR).Count);
        }

        // ── Enhancers ──────────────────────────────────────────────────────────

        [Test]
        public void Defense_CannotPlayOffensiveEnhancer()
        {
            game.phase = GamePhase.ChoosePlay;
            def.SelectedPlay = PlayType.Run;
            var enh = Make(def, CardType.OffensivePlayEnhancer);
            Assert.AreEqual(CardPlayError.WrongSide, game.GetCardPlayError(enh, CardPositionSlot.None));
        }

        [Test]
        public void Offense_CannotPlayDefensiveEnhancer()
        {
            game.phase = GamePhase.ChoosePlay;
            off.SelectedPlay = PlayType.Run;
            var enh = Make(off, CardType.DefensivePlayEnhancer);
            Assert.AreEqual(CardPlayError.WrongSide, game.GetCardPlayError(enh, CardPositionSlot.None));
        }

        [Test]
        public void Enhancer_RejectedOutsideChoosePlay()
        {
            var enh = Make(off, CardType.OffensivePlayEnhancer);
            Assert.AreEqual(CardPlayError.WrongPhase, game.GetCardPlayError(enh, CardPositionSlot.None));
        }

        [Test]
        public void Enhancer_PlayOverride_ChecksRequiredPlays()
        {
            game.phase = GamePhase.ChoosePlay;
            var enh = Make(off, CardType.OffensivePlayEnhancer, requiredPlays: new[] { PlayType.Run });
            Assert.AreEqual(CardPlayError.None, game.GetCardPlayError(enh, CardPositionSlot.None, PlayType.Run));
            Assert.AreEqual(CardPlayError.PlayMismatch, game.GetCardPlayError(enh, CardPositionSlot.None, PlayType.LongPass));
        }

        [Test]
        public void SelectPlay_WithEnhancer_MovesCardToDiscard()
        {
            game.phase = GamePhase.ChoosePlay;
            var enh = Make(off, CardType.OffensivePlayEnhancer, requiredPlays: new[] { PlayType.Run });

            logic.SelectPlay(off, PlayType.Run, enh);

            Assert.AreEqual(PlayType.Run, off.SelectedPlay);
            Assert.AreSame(enh, off.PlayEnhancer);
            Assert.IsFalse(off.cards_hand.Contains(enh));
            Assert.Contains(enh, off.cards_discard);
            Assert.AreEqual(GamePhase.ChoosePlay, game.phase); // defense hasn't picked
        }

        [Test]
        public void SelectPlay_WrongSideEnhancer_IgnoredButPlayStands()
        {
            game.phase = GamePhase.ChoosePlay;
            var enh = Make(def, CardType.OffensivePlayEnhancer);

            logic.SelectPlay(def, PlayType.ShortPass, enh);

            Assert.AreEqual(PlayType.ShortPass, def.SelectedPlay);
            Assert.IsNull(def.PlayEnhancer);
            Assert.Contains(enh, def.cards_hand);
        }

        [Test]
        public void SelectPlay_SecondEnhancer_Rejected()
        {
            game.phase = GamePhase.ChoosePlay;
            var a = Make(off, CardType.OffensivePlayEnhancer);
            var b = Make(off, CardType.OffensivePlayEnhancer);
            logic.SelectPlay(off, PlayType.Run, a);
            Assert.AreEqual(CardPlayError.AlreadyPlayedOne, game.GetCardPlayError(b, CardPositionSlot.None));
        }

        [Test]
        public void SelectPlay_OpponentsCard_Rejected()
        {
            game.phase = GamePhase.ChoosePlay;
            var enh = Make(def, CardType.DefensivePlayEnhancer);
            logic.SelectPlay(off, PlayType.Run, enh); // offense tries to submit defense's card
            Assert.IsNull(off.PlayEnhancer);
            Assert.Contains(enh, def.cards_hand);
        }

        // ── Defense guess wiring ───────────────────────────────────────────────

        [Test]
        public void SelectPlay_Defense_SetsDefenseGuess()
        {
            game.phase = GamePhase.ChoosePlay;
            logic.SelectPlay(def, PlayType.LongPass, null);
            Assert.AreEqual(PlayType.LongPass, game.defense_guess);
        }

        [Test]
        public void SelectPlay_Offense_DoesNotSetDefenseGuess()
        {
            game.phase = GamePhase.ChoosePlay;
            logic.SelectPlay(off, PlayType.Run, null);
            Assert.AreEqual(PlayType.Huddle, game.defense_guess);
        }

        [Test]
        public void WasDefenseGuessCorrect_ReflectsCalls()
        {
            game.phase = GamePhase.ChoosePlay;
            logic.SelectPlay(def, PlayType.Run, null);
            off.SelectedPlay = PlayType.Run;
            Assert.IsTrue(game.WasDefenseGuessCorrect());
            off.SelectedPlay = PlayType.ShortPass;
            Assert.IsFalse(game.WasDefenseGuessCorrect());
        }

        [Test]
        public void GameClone_CopiesDefenseGuess()
        {
            game.SetDefenseGuess(PlayType.ShortPass);
            var dest = new Game("clone", 2);
            Game.Clone(game, dest);
            Assert.AreEqual(PlayType.ShortPass, dest.defense_guess);
        }

        // ── Live ball ──────────────────────────────────────────────────────────

        [Test]
        public void Defense_CanPlayDefLiveBall_InLiveBall()
        {
            game.phase = GamePhase.LiveBall;
            var lb = Make(def, CardType.DefLiveBall);
            Assert.AreEqual(CardPlayError.None, game.GetCardPlayError(lb, CardPositionSlot.None));
        }

        [Test]
        public void Offense_CannotPlayDefLiveBall()
        {
            game.phase = GamePhase.LiveBall;
            var lb = Make(off, CardType.DefLiveBall);
            Assert.AreEqual(CardPlayError.WrongSide, game.GetCardPlayError(lb, CardPositionSlot.None));
        }

        [Test]
        public void Defense_CannotPlayOffLiveBall()
        {
            game.phase = GamePhase.LiveBall;
            var lb = Make(def, CardType.OffLiveBall);
            Assert.AreEqual(CardPlayError.WrongSide, game.GetCardPlayError(lb, CardPositionSlot.None));
        }

        [Test]
        public void LiveBall_RejectedOutsideLiveBallPhase()
        {
            var lb = Make(off, CardType.OffLiveBall);
            Assert.AreEqual(CardPlayError.WrongPhase, game.GetCardPlayError(lb, CardPositionSlot.None));
        }

        [Test]
        public void LiveBall_SecondCard_Rejected()
        {
            game.phase = GamePhase.LiveBall;
            var a = Make(off, CardType.OffLiveBall);
            var b = Make(off, CardType.OffLiveBall);
            logic.PlayCard(a, CardPositionSlot.None);
            Assert.AreSame(a, off.LiveBallCard);
            Assert.AreEqual(CardPlayError.AlreadyPlayedOne, game.GetCardPlayError(b, CardPositionSlot.None));
        }

        [Test]
        public void LiveBall_SlotRequirementNotMet_Rejected()
        {
            game.phase = GamePhase.LiveBall;
            var req = new SlotRequirement { icon = SlotMachineIconType.Star, requiredCount = 1 };
            var lb = Make(off, CardType.OffLiveBall, slotReqs: new[] { req });
            Assert.AreEqual(CardPlayError.SlotRequirements, game.GetCardPlayError(lb, CardPositionSlot.None));
        }

        // ── Legacy types ───────────────────────────────────────────────────────

        [Test]
        public void LegacyTypes_AlwaysRejected()
        {
            foreach (var t in new[] { CardType.Hero, CardType.Artifact, CardType.Secret, CardType.Equipment })
            {
                var c = Make(off, t);
                foreach (var ph in new[] { GamePhase.ChoosePlayers, GamePhase.ChoosePlay, GamePhase.LiveBall })
                {
                    game.phase = ph;
                    Assert.AreEqual(CardPlayError.UnsupportedType, game.GetCardPlayError(c, CardPositionSlot.None), $"{t} in {ph}");
                }
            }
        }

        [Test]
        public void NoOffensivePlayer_Rejected()
        {
            game.current_offensive_player = null;
            var wr = Make(off, CardType.OffensivePlayer, PlayerPositionGrp.WR);
            Assert.AreEqual(CardPlayError.NoOffense, game.GetCardPlayError(wr, Slot(off, PlayerPositionGrp.WR)));
        }
    }
}

using NUnit.Framework;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using Assets.TcgEngine.Scripts.Effects;
using UnityEngine;

namespace TcgEngine.Tests
{
    public class PlayEnhancerTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private Game MakeMinimalGame(out Player offPlayer)
        {
            var game = new Game();
            game.phase = GamePhase.ChoosePlay;

            offPlayer = new Player(0);
            var defPlayer = new Player(1);

            game.players = new Player[] { offPlayer, defPlayer };
            game.current_offensive_player = offPlayer;
            // current_slot_data left null:
            //   AreSlotRequirementsMet → true for empty requirements
            //   AreSlotRequirementsMet → false for non-empty requirements (line 196-197 in CardData.cs)
            return game;
        }

        private static VariantData _sharedVariant;
        private static VariantData SharedVariant =>
            _sharedVariant != null ? _sharedVariant : (_sharedVariant = ScriptableObject.CreateInstance<VariantData>());

        private Card MakeEnhancerCard(Player player, PlayType[] requiredPlays, SlotRequirement[] slotReqs = null)
        {
            var cardData = ScriptableObject.CreateInstance<CardData>();
            cardData.type = CardType.OffensivePlayEnhancer;
            cardData.playerPosition = PlayerPositionGrp.NONE;
            cardData.required_plays = requiredPlays ?? new PlayType[0];
            cardData.slotRequirements = slotReqs ?? new SlotRequirement[0];

            var card = Card.Create(cardData, SharedVariant, player);
            player.cards_hand.Add(card);
            return card;
        }

        // ── Phase enforcement ──────────────────────────────────────────────────

        [Test]
        public void Enhancer_WrongPhase_ReturnsFalse()
        {
            var game = MakeMinimalGame(out var player);
            game.phase = GamePhase.ChoosePlayers;

            var card = MakeEnhancerCard(player, new PlayType[0]);
            player.SelectedPlay = PlayType.Run;

            Assert.IsFalse(game.CanPlayCard(card, CardPositionSlot.None));
        }

        // ── One-per-turn enforcement ───────────────────────────────────────────

        [Test]
        public void Enhancer_DoublePlay_ReturnsFalse()
        {
            var game = MakeMinimalGame(out var player);
            var alreadyPlayed = MakeEnhancerCard(player, new PlayType[0]);
            player.PlayEnhancer = alreadyPlayed;

            var second = MakeEnhancerCard(player, new PlayType[0]);
            player.SelectedPlay = PlayType.Run;

            Assert.IsFalse(game.CanPlayCard(second, CardPositionSlot.None));
        }

        // ── PlayType enforcement (Bug 1 — fails until Game.cs fix) ─────────────

        [Test]
        public void RunEnhancer_WhenRunSelected_ReturnsTrue()
        {
            var game = MakeMinimalGame(out var player);
            var card = MakeEnhancerCard(player, new PlayType[] { PlayType.Run });
            player.SelectedPlay = PlayType.Run;

            Assert.IsTrue(game.CanPlayCard(card, CardPositionSlot.None));
        }

        [Test]
        public void RunEnhancer_WhenPassSelected_ReturnsFalse()
        {
            // Exposed bug: required_plays not enforced in CanPlayCard
            var game = MakeMinimalGame(out var player);
            var card = MakeEnhancerCard(player, new PlayType[] { PlayType.Run });
            player.SelectedPlay = PlayType.ShortPass;

            Assert.IsFalse(game.CanPlayCard(card, CardPositionSlot.None));
        }

        [Test]
        public void Enhancer_NoRequiredPlays_AnyPlay_ReturnsTrue()
        {
            var game = MakeMinimalGame(out var player);
            var card = MakeEnhancerCard(player, new PlayType[0]);
            player.SelectedPlay = PlayType.LongPass;

            Assert.IsTrue(game.CanPlayCard(card, CardPositionSlot.None));
        }

        // ── SlotRequirements enforcement (Bug 2 — fails until Game.cs fix) ─────

        [Test]
        public void Enhancer_WithSlotReq_WhenNotMet_ReturnsFalse()
        {
            // Exposed gap: AreSlotRequirementsMet not checked for enhancers
            // current_slot_data = null → no icons present → 1-Star requirement cannot be met
            var game = MakeMinimalGame(out var player);
            var slotReq = new SlotRequirement { icon = SlotMachineIconType.Star, requiredCount = 1 };
            var card = MakeEnhancerCard(player, new PlayType[0], new SlotRequirement[] { slotReq });
            player.SelectedPlay = PlayType.Run;

            Assert.IsFalse(game.CanPlayCard(card, CardPositionSlot.None));
        }

        // ── EffectAddStat unit tests ───────────────────────────────────────────

        [Test]
        public void EffectAddStat_RunBonus_AddsStatusToCard()
        {
            var targetData = ScriptableObject.CreateInstance<CardData>();
            targetData.type = CardType.OffensivePlayer;
            var player = new Player(0);
            var target = Card.Create(targetData, SharedVariant, player);

            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.affected_stat = StatusTypePrintedStats.AddedRunBonus;
            ability.stat_bonus_amount = 5;
            ability.duration = 1;

            var effect = ScriptableObject.CreateInstance<EffectAddStat>();
            effect.DoEffect(null, ability, target, target);

            Assert.AreEqual(5, target.GetStatusValue(StatusType.AddedRunBonus));
        }

        [Test]
        public void EffectAddStat_AddGrit_AddsGritStatus()
        {
            var targetData = ScriptableObject.CreateInstance<CardData>();
            targetData.type = CardType.OffensivePlayer;
            var player = new Player(0);
            var target = Card.Create(targetData, SharedVariant, player);

            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.affected_stat = StatusTypePrintedStats.AddGrit;
            ability.stat_bonus_amount = 3;
            ability.duration = 1;

            var effect = ScriptableObject.CreateInstance<EffectAddStat>();
            effect.DoEffect(null, ability, target, target);

            Assert.AreEqual(3, target.GetStatusValue(StatusType.AddGrit));
        }
    }
}

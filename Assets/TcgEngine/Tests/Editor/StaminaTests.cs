using NUnit.Framework;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using Assets.TcgEngine.Scripts.Effects;
using UnityEngine;

namespace TcgEngine.Tests
{
    public class StaminaTests
    {
        private class TestableGLS : GameLogicService
        {
            public TestableGLS() : base(false) { }
            public override void StartTurn() { }
            public override void RefreshData() { }
        }

        // Creates a CardData+Card pair for a board player card
        private Card MakePlayerCard(Player player, int stamina, int grit = 2)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.id = "test_player_" + player.player_id;
            cd.type = CardType.OffensivePlayer;
            cd.stamina = stamina;
            cd.grit = grit;

            var variant = ScriptableObject.CreateInstance<VariantData>();
            variant.id = "default";

            var card = Card.Create(cd, variant, player);
            player.cards_board.Add(card);
            return card;
        }

        private TestableGLS MakeGLS(out Player offense, out Player defense)
        {
            var game = new Game();
            offense = new Player(0);
            defense = new Player(1);
            game.players = new Player[] { offense, defense };
            game.current_offensive_player = offense;
            game.raw_ball_on = 25;
            game.yardage_this_play = 5;
            game.current_down = 1;
            game.plays_left_in_half = 5;
            game.turnover_pending = false;
            var gls = new TestableGLS();
            gls.game_data = game;
            return gls;
        }

        // ── Per-play drain ────────────────────────────────────────────────────

        [Test]
        public void StaminaDecrementsAfterPlay()
        {
            var gls = MakeGLS(out var offense, out _);
            var card = MakePlayerCard(offense, stamina: 3);

            gls.EndPlayPhase();

            Assert.AreEqual(2, card.current_stamina);
        }

        [Test]
        public void CardDiscardedWhenStaminaReachesZero()
        {
            var gls = MakeGLS(out var offense, out _);
            var card = MakePlayerCard(offense, stamina: 1);

            gls.EndPlayPhase();

            Assert.IsFalse(offense.cards_board.Contains(card), "Card should leave the board");
            Assert.IsTrue(offense.cards_discard.Contains(card), "Card should be in discard");
        }

        [Test]
        public void SidelineCardNotDrained()
        {
            var gls = MakeGLS(out var offense, out _);

            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.id = "sideline_player";
            cd.type = CardType.OffensivePlayer;
            cd.stamina = 3;
            cd.grit = 2;
            var variant = ScriptableObject.CreateInstance<VariantData>();
            variant.id = "default";
            var card = Card.Create(cd, variant, offense);
            offense.cards_sideline.Add(card); // on sideline, not board

            gls.EndPlayPhase();

            Assert.AreEqual(3, card.current_stamina, "Sideline card stamina unchanged");
        }

        [Test]
        public void NonPlayerBoardCardNotDrained()
        {
            var gls = MakeGLS(out var offense, out _);

            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.id = "enhancer";
            cd.type = CardType.OffensivePlayEnhancer;
            cd.stamina = 2;
            var variant = ScriptableObject.CreateInstance<VariantData>();
            variant.id = "default";
            var card = Card.Create(cd, variant, offense);
            offense.cards_board.Add(card);

            gls.EndPlayPhase();

            Assert.AreEqual(2, card.current_stamina, "Non-player card stamina unchanged");
        }

        // ── EffectModifyStamina ───────────────────────────────────────────────

        [Test]
        public void EffectDrainsStamina()
        {
            var effect = ScriptableObject.CreateInstance<EffectModifyStamina>();
            effect.value = 2;
            effect.removeStamina = true;
            effect.target = EffectTarget.Self;

            var gls = MakeGLS(out var offense, out _);
            var card = MakePlayerCard(offense, stamina: 5);

            effect.DoEffect(gls, null, card);

            Assert.AreEqual(3, card.current_stamina);
        }

        [Test]
        public void EffectHealsStamina_ClampedToMax()
        {
            var effect = ScriptableObject.CreateInstance<EffectModifyStamina>();
            effect.value = 10;
            effect.removeStamina = false;
            effect.target = EffectTarget.Self;

            var gls = MakeGLS(out var offense, out _);
            var card = MakePlayerCard(offense, stamina: 3);
            card.current_stamina = 1; // manually deplete

            effect.DoEffect(gls, null, card);

            Assert.AreEqual(3, card.current_stamina, "Healed stamina capped at max");
        }

        [Test]
        public void EffectDrainsTeamStamina()
        {
            var effect = ScriptableObject.CreateInstance<EffectModifyStamina>();
            effect.value = 1;
            effect.removeStamina = true;
            effect.target = EffectTarget.Team;

            var gls = MakeGLS(out var offense, out _);
            var c1 = MakePlayerCard(offense, stamina: 4);
            var c2 = MakePlayerCard(offense, stamina: 3);

            effect.DoEffect(gls, null, c1);

            Assert.AreEqual(3, c1.current_stamina);
            Assert.AreEqual(2, c2.current_stamina);
        }

        [Test]
        public void EffectDrainsOpponentStamina()
        {
            var effect = ScriptableObject.CreateInstance<EffectModifyStamina>();
            effect.value = 2;
            effect.removeStamina = true;
            effect.target = EffectTarget.Opponent;

            var gls = MakeGLS(out var offense, out var defense);
            var offCard = MakePlayerCard(offense, stamina: 4);
            var defCard = MakePlayerCard(defense, stamina: 3);

            effect.DoEffect(gls, null, offCard);

            Assert.AreEqual(4, offCard.current_stamina, "Caster untouched");
            Assert.AreEqual(1, defCard.current_stamina, "Opponent drained");
        }

        // ── GetTotalStamina reads current (not max) ───────────────────────────

        [Test]
        public void GetTotalStamina_ReflectsCurrentNotMax()
        {
            var gls = MakeGLS(out var offense, out _);
            var card = MakePlayerCard(offense, stamina: 5);
            card.current_stamina = 2; // deplete manually

            int total = offense.GetTotalStamina();

            Assert.AreEqual(2, total, "GetTotalStamina should return current_stamina, not max");
        }

        [Test]
        public void ConditionStaminaCompare_ReflectsCurrentStamina()
        {
            var cond = ScriptableObject.CreateInstance<ConditionStaminaCompare>();
            cond.compareToOpponent = false;
            cond.oper = ConditionOperatorInt.GreaterEqual;
            cond.value = 3;
            cond.teamStamina = true;

            var gls = MakeGLS(out var offense, out _);
            var card = MakePlayerCard(offense, stamina: 5);

            // Full stamina: 5 >= 3 → true
            Assert.IsTrue(cond.IsTriggerConditionMet(gls.game_data, null, card));

            // Depleted: 1 >= 3 → false
            card.current_stamina = 1;
            Assert.IsFalse(cond.IsTriggerConditionMet(gls.game_data, null, card));
        }

        // ── LowestStaminaAlly targeting ───────────────────────────────────────

        private AbilityData MakeAbilityWithTarget(AbilityTarget abilityTarget)
        {
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.id = "test_ability";
            ability.target = abilityTarget;
            ability.conditions_target = new ConditionData[0];
            ability.conditions_trigger = new ConditionData[0];
            ability.effects = new EffectData[0];
            ability.status = new StatusData[0];
            ability.chain_abilities = new AbilityData[0];
            return ability;
        }

        [Test]
        public void LowestStaminaAlly_FindsCorrectTarget()
        {
            var gls = MakeGLS(out var offense, out _);
            var c1 = MakePlayerCard(offense, stamina: 5); // caster
            var c2 = MakePlayerCard(offense, stamina: 1); // lowest
            var c3 = MakePlayerCard(offense, stamina: 3);
            c1.current_stamina = 5;
            c2.current_stamina = 1;
            c3.current_stamina = 3;

            var ability = MakeAbilityWithTarget(AbilityTarget.LowestStaminaAlly);
            var targets = ability.GetCardTargets(gls.game_data, c1);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(c2, targets[0], "Should return card with lowest current_stamina");
        }

        [Test]
        public void LowestStaminaAlly_ExcludesCaster()
        {
            var gls = MakeGLS(out var offense, out _);
            var caster = MakePlayerCard(offense, stamina: 1); // lowest stamina but is the caster
            var ally   = MakePlayerCard(offense, stamina: 3);
            caster.current_stamina = 1;
            ally.current_stamina = 3;

            var ability = MakeAbilityWithTarget(AbilityTarget.LowestStaminaAlly);
            var targets = ability.GetCardTargets(gls.game_data, caster);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(ally, targets[0], "Caster excluded even if it has lowest stamina");
        }

        [Test]
        public void LowestStaminaAlly_ReturnsNone_IfNoAllies()
        {
            var gls = MakeGLS(out var offense, out _);
            var caster = MakePlayerCard(offense, stamina: 3); // only card on board

            var ability = MakeAbilityWithTarget(AbilityTarget.LowestStaminaAlly);
            var targets = ability.GetCardTargets(gls.game_data, caster);

            Assert.AreEqual(0, targets.Count, "No allies on board — empty target list");
        }

        [Test]
        public void OnKnockout_TriggerValue_Is41()
        {
            // Sanity check enum value doesn't collide
            Assert.AreEqual(41, (int)AbilityTrigger.OnKnockout);
        }
    }
}

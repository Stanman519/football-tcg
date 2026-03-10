using NUnit.Framework;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using Assets.TcgEngine.Scripts.Effects;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Tests
{
    /// <summary>
    /// Tests for fumble resolution bugs B1–B4.
    /// All tests use TestableGLS to avoid real-game dependencies (networking, Unity scene).
    /// </summary>
    public class FumbleTests
    {
        // ── TestableGLS ───────────────────────────────────────────────────────

        private class TestableGLS : GameLogicService
        {
            public TestableGLS() : base(false) { }
            public override void StartTurn() { }
            public override void RefreshData() { }

            // Expose protected methods for direct testing
            public PlayResolution CallHandleFumble(AbilityQueueElement e) => HandleFumble(e);
            public PlayResolution CallHandleQbFumble(AbilityQueueElement e) => HandleQbFumble(e);
            public PlayResolution CallResolvePassFailEvents() => ResolvePassFailEvents();
        }

        // ── Shared helpers ────────────────────────────────────────────────────

        private static VariantData _variant;
        private static VariantData SharedVariant =>
            _variant != null ? _variant : (_variant = ScriptableObject.CreateInstance<VariantData>());

        /// <summary>Sets up a minimal two-player game with the GLS wired to it.</summary>
        private TestableGLS MakeGLS(out Player offense, out Player defense, int ballOn = 50)
        {
            offense = new Player(0);
            defense = new Player(1);

            var game = new Game();
            game.players = new Player[] { offense, defense };
            game.current_offensive_player = offense;
            game.raw_ball_on = ballOn;
            game.yardage_this_play = 0;
            game.current_down = 2;
            game.plays_left_in_half = 5;
            game.turnover_pending = false;
            game.phase = GamePhase.LiveBall;
            game.state = GameState.Play;

            var gls = new TestableGLS();
            gls.game_data = game;
            gls.ResolveQueue.SetData(game);
            return gls;
        }

        /// <summary>Creates a player card with the given grit value and adds it to the board.</summary>
        private Card AddBoardCard(Player player, int grit)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.type = CardType.OffensivePlayer;
            data.grit = grit;
            var card = Card.Create(data, SharedVariant, player);
            player.cards_board.Add(card);
            return card;
        }

        /// <summary>Creates a live-ball card with the given effect and sets it as the player's LiveBallCard.</summary>
        private Card MakeLiveBallCard(Player player, EffectData effect, bool isOffense)
        {
            var abilityData = ScriptableObject.CreateInstance<AbilityData>();
            abilityData.trigger = AbilityTrigger.OnLiveBallResolution;
            abilityData.effects = new EffectData[] { effect };
            abilityData.conditions_trigger = new ConditionData[0];
            abilityData.chain_abilities = new AbilityData[0];
            abilityData.slotRequirements = new SlotRequirement[0];

            var cardType = isOffense ? CardType.OffLiveBall : CardType.DefLiveBall;
            var cardData = ScriptableObject.CreateInstance<CardData>();
            cardData.type = cardType;

            var card = Card.Create(cardData, SharedVariant, player);
            // Force abilities_data to init, then add our ability
            card.GetAbilities();
            card.AddAbility(abilityData);

            player.LiveBallCard = card;
            return card;
        }

        /// <summary>Creates a fumble ability queue element (mimics a RunnerFumble fail event).</summary>
        private AbilityQueueElement MakeFumbleEvent()
        {
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.trigger = AbilityTrigger.OnRunResolution;
            ability.failEventType = FailPlayEventType.RunnerFumble;
            ability.conditions_trigger = new ConditionData[0];
            ability.effects = new EffectData[0];

            var elem = new AbilityQueueElement();
            elem.ability = ability;
            elem.caster = null;
            elem.triggerer = null;
            return elem;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // B3 — HandleFumble / HandleQbFumble must NOT pre-set Turnover
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void HandleFumble_BallIsLive_TurnoverAlwaysFalse()
        {
            // EXPOSES B3: even when defGrit > offGrit, HandleFumble must return Turnover=false.
            // Before B3 fix: CalcGritContest(off=2, def=8) would return turnover=true.
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 2);
            AddBoardCard(defense, grit: 8);

            var result = gls.CallHandleFumble(MakeFumbleEvent());

            Assert.IsTrue(result.BallIsLive, "Fumble must send play to live ball");
            Assert.IsFalse(result.Turnover, "B3: HandleFumble must never pre-set Turnover=true");
        }

        [Test]
        public void HandleQbFumble_BallIsLive_TurnoverAlwaysFalse()
        {
            // EXPOSES B3 for QB path
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 2);
            AddBoardCard(defense, grit: 8);

            var elem = MakeFumbleEvent();
            elem.ability.trigger = AbilityTrigger.OnPassResolution;
            elem.ability.failEventType = FailPlayEventType.QBFumble;
            var result = gls.CallHandleQbFumble(elem);

            Assert.IsTrue(result.BallIsLive);
            Assert.IsFalse(result.Turnover, "B3: HandleQbFumble must never pre-set Turnover=true");
        }

        [Test]
        public void HandleFumble_YardageGainedIsZero()
        {
            // After B3 fix, subtotal before fumble is 0 (live ball determines final outcome)
            var gls = MakeGLS(out _, out _);
            var result = gls.CallHandleFumble(MakeFumbleEvent());
            Assert.AreEqual(0, result.YardageGained);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // B2 — Ball Security must protect receiver fumble in pass plays
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void PassFail_RunnerFumble_NoBallSecurity_BallIsLive()
        {
            // Control: without Ball Security a receiver fumble sends play to live ball
            var gls = MakeGLS(out var offense, out _);

            var casterData = ScriptableObject.CreateInstance<CardData>();
            var caster = Card.Create(casterData, SharedVariant, offense);

            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.trigger = AbilityTrigger.OnPassResolution;
            ability.failEventType = FailPlayEventType.RunnerFumble;
            ability.conditions_trigger = new ConditionData[0];
            ability.effects = new EffectData[0];
            ability.chain_abilities = new AbilityData[0];
            gls.ResolveQueue.AddAbility(ability, caster, caster, null);

            var result = gls.CallResolvePassFailEvents();

            Assert.IsNotNull(result, "RunnerFumble event must produce a PlayResolution");
            Assert.IsTrue(result.BallIsLive, "Fumble should be live ball without Ball Security");
        }

        [Test]
        public void PassFail_RunnerFumble_BallSecurityPrevents()
        {
            // EXPOSES B2: Ball Security enhancer must prevent receiver fumble in pass plays.
            // Before B2 fix: ResolvePassFailEvents had no Ball Security check for RunnerFumble.
            var gls = MakeGLS(out var offense, out _);

            // Set Ball Security enhancer
            var enhData = ScriptableObject.CreateInstance<CardData>();
            enhData.id = "00308_enh_ball_security";
            var enhCard = Card.Create(enhData, SharedVariant, offense);
            offense.PlayEnhancer = enhCard;

            var casterData = ScriptableObject.CreateInstance<CardData>();
            var caster = Card.Create(casterData, SharedVariant, offense);

            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.trigger = AbilityTrigger.OnPassResolution;
            ability.failEventType = FailPlayEventType.RunnerFumble;
            ability.conditions_trigger = new ConditionData[0];
            ability.effects = new EffectData[0];
            ability.chain_abilities = new AbilityData[0];
            gls.ResolveQueue.AddAbility(ability, caster, caster, null);

            var result = gls.CallResolvePassFailEvents();

            Assert.IsNull(result, "B2: Ball Security must prevent receiver fumble (return null = no fail event)");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // B4 — EndPlayPhase applies live_ball_return_yards
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void EndPlayPhase_LiveBallTurnover_AppliesReturnYards()
        {
            // C2 fix: both teams drive 0→100; fumble at raw_ball_on+yardage_this_play flips to 100-fumbleSpot for new offense
            // offense at 60, run gains 5 → fumble at 65; def returns 4 → new offense starts at 100-65+4=39
            var gls = MakeGLS(out var offense, out var defense, ballOn: 60);
            gls.game_data.yardage_this_play = 5;
            gls.game_data.turnover_pending = true;
            gls.game_data.live_ball_return_yards = 4;

            gls.EndPlayPhase();

            Assert.AreEqual(39, gls.game_data.raw_ball_on);
        }

        [Test]
        public void EndPlayPhase_LiveBallTurnover_NoReturnYards_DefaultsTo25()
        {
            // Control: without live ball return yards, drive resets to default 25
            var gls = MakeGLS(out _, out _, ballOn: 40);
            gls.game_data.turnover_pending = true;
            gls.game_data.live_ball_return_yards = 0;

            gls.EndPlayPhase();

            Assert.AreEqual(25, gls.game_data.raw_ball_on);
        }

        [Test]
        public void EndPlayPhase_LiveBallTurnover_SwitchesPossession()
        {
            var gls = MakeGLS(out _, out var defense);
            gls.game_data.turnover_pending = true;
            gls.EndPlayPhase();
            Assert.AreEqual(defense, gls.game_data.current_offensive_player);
        }

        [Test]
        public void EndPlayPhase_LiveBallTurnover_ClearsReturnYards()
        {
            var gls = MakeGLS(out _, out _);
            gls.game_data.turnover_pending = true;
            gls.game_data.live_ball_return_yards = 15;
            gls.EndPlayPhase();
            Assert.AreEqual(0, gls.game_data.live_ball_return_yards, "live_ball_return_yards must be consumed");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // B1 — EffectForceTurnover must include live_ball_grit_bonus in return yards
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void EffectForceTurnover_IncludesGritBonus_ReducesReturnYards()
        {
            // EXPOSES B1: with bonus, effective offGrit = 5+3=8; defGrit=10
            // Correct returnYards = 2*(10-8) = 4
            // Before B1 fix: returnYards = 2*(10-5) = 10 (bonus ignored)
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 5);
            AddBoardCard(defense, grit: 10);
            gls.game_data.live_ball_grit_bonus = 3; // bonus from off live ball card

            var effect = ScriptableObject.CreateInstance<EffectForceTurnover>();
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.effects = new EffectData[] { effect };
            var casterData = ScriptableObject.CreateInstance<CardData>();
            var caster = Card.Create(casterData, SharedVariant, defense);

            effect.DoEffect(gls, ability, caster);

            // B4 fix also needed: HandleLiveBallTurnover stores to live_ball_return_yards
            Assert.AreEqual(4, gls.game_data.live_ball_return_yards,
                "B1: return yards must use offGrit+bonus, not just offGrit");
        }

        [Test]
        public void EffectForceTurnover_WithoutBonus_BaseReturnYards()
        {
            // Control: no bonus, offGrit=5, defGrit=10 → returnYards = 2*(10-5) = 10
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 5);
            AddBoardCard(defense, grit: 10);
            gls.game_data.live_ball_grit_bonus = 0;

            var effect = ScriptableObject.CreateInstance<EffectForceTurnover>();
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.effects = new EffectData[] { effect };
            var casterData = ScriptableObject.CreateInstance<CardData>();
            var caster = Card.Create(casterData, SharedVariant, defense);

            effect.DoEffect(gls, ability, caster);

            Assert.AreEqual(10, gls.game_data.live_ball_return_yards);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Live ball integration tests
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void LiveBall_BothPass_NoTurnover()
        {
            // Both players pass on live ball → subtotal stands, no turnover
            var gls = MakeGLS(out var offense, out var defense);
            // No LiveBallCard for either player (null = pass)

            gls.ResolveLiveBallEffects();
            gls.ResolveQueue.ResolveAll();

            Assert.IsFalse(gls.game_data.turnover_pending, "Both pass: no turnover");
        }

        [Test]
        public void LiveBall_FumbleCard_OffWins_NoTurnover()
        {
            // Defense plays EffectForceTurnover, but offGrit >= defGrit → no turnover
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 10);
            AddBoardCard(defense, grit: 3);

            MakeLiveBallCard(defense, ScriptableObject.CreateInstance<EffectForceTurnover>(), isOffense: false);

            gls.ResolveLiveBallEffects();
            gls.ResolveQueue.ResolveAll();

            Assert.IsFalse(gls.game_data.turnover_pending, "Offense wins grit — no turnover");
        }

        [Test]
        public void LiveBall_FumbleCard_DefWins_SetsTurnoverPending()
        {
            // Defense plays EffectForceTurnover, defGrit > offGrit → turnover
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 3);
            AddBoardCard(defense, grit: 10);

            MakeLiveBallCard(defense, ScriptableObject.CreateInstance<EffectForceTurnover>(), isOffense: false);

            gls.ResolveLiveBallEffects();
            gls.ResolveQueue.ResolveAll();

            // After B4 fix: HandleLiveBallTurnover sets turnover_pending instead of calling SwitchPossession directly
            Assert.IsTrue(gls.game_data.turnover_pending, "Defense wins grit — turnover_pending must be true");
        }

        [Test]
        public void LiveBall_BallSecurity_PreventsFumble()
        {
            // Offense plays EffectPreventTurnover → fumble auto-denied regardless of grit
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 1);   // would lose grit contest
            AddBoardCard(defense, grit: 20);

            MakeLiveBallCard(defense, ScriptableObject.CreateInstance<EffectForceTurnover>(), isOffense: false);
            MakeLiveBallCard(offense, ScriptableObject.CreateInstance<EffectPreventTurnover>(), isOffense: true);

            gls.ResolveLiveBallEffects();
            gls.ResolveQueue.ResolveAll();

            Assert.IsFalse(gls.game_data.turnover_pending, "Ball Security must prevent fumble turnover");
        }

        [Test]
        public void LiveBall_BlankeCoverage_SkipsFumbleCheck()
        {
            // Defense plays EffectNegateCard (Blanket Coverage) → offense card negated,
            // but also defNegated=true (Negate card consumed), so fumble ability is skipped
            var gls = MakeGLS(out var offense, out var defense);
            AddBoardCard(offense, grit: 1);
            AddBoardCard(defense, grit: 20);

            // Offense plays any card (target for negate)
            MakeLiveBallCard(offense, ScriptableObject.CreateInstance<EffectImmunity>(), isOffense: true);
            // Defense plays EffectNegateCard (Blanket Coverage)
            // After negate fires, both offNegated and defNegated = true → fumble check skipped
            MakeLiveBallCard(defense, ScriptableObject.CreateInstance<EffectNegateCard>(), isOffense: false);

            gls.ResolveLiveBallEffects();
            gls.ResolveQueue.ResolveAll();

            // Blanket Coverage negates off card AND marks def consumed — fumble step is skipped
            Assert.IsFalse(gls.game_data.turnover_pending);
        }
    }
}

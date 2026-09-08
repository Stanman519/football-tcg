using NUnit.Framework;
using System.Collections.Generic;
using TcgEngine;
using TcgEngine.Effects;
using TcgEngine.Gameplay;
using UnityEngine;

namespace TcgEngine.Tests
{
    /// <summary>
    /// Impact-record capture + pass-coverage status bugfix (P8) regression tests.
    /// TestableGLS pattern per FumbleTests.
    /// </summary>
    public class PlayImpactTests
    {
        private class TestableGLS : GameLogicService
        {
            public TestableGLS() : base(false) { }
            public override void StartTurn() { }
            public override void RefreshData() { }

            public void CallTriggerBoardSlotAbilities(PlayType pt) => TriggerBoardSlotAbilities(pt);
            public PlayResolution CallResolvePassOutcome() => ResolvePassOutcome();
        }

        private static VariantData _variant;
        private static VariantData SharedVariant =>
            _variant != null ? _variant : (_variant = ScriptableObject.CreateInstance<VariantData>());

        private TestableGLS gls;
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
            game.current_down = 2;
            game.state = GameState.Play;
            game.phase = GamePhase.SlotSpin;
            game.current_slot_data = new SlotMachineResultDTO { Results = new List<ReelSpriteData>() };
            game.play_impacts = new List<PlayImpact>();

            gls = new TestableGLS();
            gls.game_data = game;
            gls.ResolveQueue.SetData(game);
        }

        private Card AddBoardCard(Player player, PlayerPositionGrp pos, bool off,
            int shortPass = 0, int shortCov = 0)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.type = off ? CardType.OffensivePlayer : CardType.DefensivePlayer;
            data.playerPosition = pos;
            data.short_pass_bonus = shortPass;
            data.short_pass_coverage_bonus = shortCov;
            var card = Card.Create(data, SharedVariant, player);
            player.cards_board.Add(card);
            return card;
        }

        private void AddStatAbility(Card card, StatusTypePrintedStats stat, int amount)
        {
            var ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.id = $"test_{stat}_{amount}";
            ability.trigger = card.player_id == 0 && game.current_offensive_player.player_id == 0
                ? AbilityTrigger.OnRunResolution
                : AbilityTrigger.OnRunResolution;
            ability.target = AbilityTarget.Self;
            ability.affected_stat = stat;
            ability.stat_bonus_amount = amount;
            ability.duration = 1;
            ability.effects = new EffectData[] { ScriptableObject.CreateInstance<EffectAddStat>() };
            ability.conditions_trigger = new ConditionData[0];
            ability.conditions_target = new ConditionData[0];
            ability.chain_abilities = new AbilityData[0];
            ability.slotRequirements = new SlotRequirement[0];
            card.GetAbilities();
            card.AddAbility(ability);
        }

        // ── Impact records ────────────────────────────────────

        [Test]
        public void PlayImpacts_RecordedForStatAbility()
        {
            var runner = AddBoardCard(offense, PlayerPositionGrp.RB_TE, true);
            AddStatAbility(runner, StatusTypePrintedStats.AddedRunBonus, 5);

            gls.CallTriggerBoardSlotAbilities(PlayType.Run);

            Assert.AreEqual(1, game.play_impacts.Count);
            var impact = game.play_impacts[0];
            Assert.AreEqual(runner.uid, impact.caster_uid);
            Assert.AreEqual((int)StatusType.AddedRunBonus, impact.status_type);
            Assert.AreEqual(5, impact.value);
        }

        [Test]
        public void PlayImpacts_NoAbilities_Empty()
        {
            AddBoardCard(offense, PlayerPositionGrp.RB_TE, true);

            gls.CallTriggerBoardSlotAbilities(PlayType.Run);

            Assert.AreEqual(0, game.play_impacts.Count);
        }

        [Test]
        public void Clone_CopiesPlayImpacts()
        {
            game.play_impacts.Add(new PlayImpact { caster_uid = "x", ability_id = "a", status_type = 1, value = 3 });

            Game dest = new Game("clone", 2);
            Game.Clone(game, dest);

            Assert.AreEqual(1, dest.play_impacts.Count);
            Assert.AreEqual("x", dest.play_impacts[0].caster_uid);
        }

        // ── P8 coverage bugfix regressions ────────────────────

        private int RunShortPass()
        {
            offense.SelectedPlay = PlayType.ShortPass;
            var res = gls.CallResolvePassOutcome();
            return res.YardageGained;
        }

        [Test]
        public void PassCoverage_DefCoverageStatus_ReducesYardage()
        {
            AddBoardCard(offense, PlayerPositionGrp.WR, true, shortPass: 6);
            var db = AddBoardCard(defense, PlayerPositionGrp.DB, false, shortCov: 2);

            int baseline = RunShortPass();
            db.AddStatus(StatusType.AddedShortPassCoverageBonus, 5, 1);
            int buffed = RunShortPass();

            Assert.AreEqual(baseline - 5, buffed);
        }

        [Test]
        public void PassCoverage_OffensiveStatusOnDefCard_Ignored()
        {
            AddBoardCard(offense, PlayerPositionGrp.WR, true, shortPass: 6);
            var db = AddBoardCard(defense, PlayerPositionGrp.DB, false, shortCov: 2);

            int baseline = RunShortPass();
            db.AddStatus(StatusType.AddedShortPassBonus, 5, 1); // offensive type on def card — must do nothing
            int after = RunShortPass();

            Assert.AreEqual(baseline, after);
        }

        [Test]
        public void PassCoverage_BreakdownRecordsDefStatusBonus()
        {
            AddBoardCard(offense, PlayerPositionGrp.WR, true, shortPass: 6);
            var db = AddBoardCard(defense, PlayerPositionGrp.DB, false, shortCov: 2);
            db.AddStatus(StatusType.AddedShortPassCoverageBonus, 4, 1);

            RunShortPass();

            Assert.AreEqual(4, game.yardage_breakdown.defStatusBonuses);
            Assert.AreEqual(2, game.yardage_breakdown.defCoverageBase);
        }
    }
}

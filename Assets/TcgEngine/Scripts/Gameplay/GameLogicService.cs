using Assets.TcgEngine.Scripts.Effects;
using Assets.TcgEngine.Scripts.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    /// <summary>
    /// Execute and resolves game rules and logic.
    /// Partial class — see:
    ///   GameLogicService.Lifecycle.cs  — phase/turn flow, scoring, drive management
    ///   GameLogicService.CardAction.cs — card mutation, combat, draw/summon/discard
    ///   GameLogicService.Abilities.cs  — ability trigger/resolve chain, selectors
    ///   GameLogicService.Stats.cs      — ongoing stat recalculation
    ///   GameLogicService.Yardage.cs    — play outcome, run/pass math, fail events
    /// </summary>

    public partial class GameLogicService
    {
        public UnityAction onGameStart;
        public UnityAction<Player> onGameEnd;          //Winner

        public UnityAction onTurnStart;
        public UnityAction onTurnPlay;
        public UnityAction onTurnEnd;
        public UnityAction onSlotSpinStart;
        public UnityAction onSlotSpinEnd;
        public UnityAction onChoosePlay;
        public UnityAction onChangeDown;
        public UnityAction onPlaycallReveal;
        public UnityAction onNewPlayerReveal;


        public UnityAction<Card, CardPositionSlot> onCardPlayed;
        public UnityAction<Card, CardPositionSlot> onCardSummoned;
        public UnityAction<Card, CardPositionSlot> onCardMoved;
        public UnityAction<Card> onCardTransformed;
        public UnityAction<Card> onCardDiscarded;
        public UnityAction<int> onCardDrawn;
        public UnityAction<int> onRollValue;

        public UnityAction<AbilityData, Card> onAbilityStart;
        public UnityAction<AbilityData, Card, Card> onAbilityTargetCard;  //Ability, Caster, Target
        public UnityAction<AbilityData, Card, Player> onAbilityTargetPlayer;
        public UnityAction<AbilityData, Card, CardPositionSlot> onAbilityTargetSlot;
        public UnityAction<AbilityData, Card> onAbilityEnd;

        public UnityAction<Card, Card> onAttackStart;  //Attacker, Defender
        public UnityAction<Card, Card> onAttackEnd;     //Attacker, Defender
        public UnityAction<Card, Player> onAttackPlayerStart;
        public UnityAction<Card, Player> onAttackPlayerEnd;

        public UnityAction<Card, int> onCardDamaged;
        public UnityAction<Card, int> onCardHealed;
        public UnityAction<Player, int> onPlayerDamaged;
        public UnityAction<Player, int> onPlayerHealed;

        public UnityAction<Card, Card> onSecretTrigger;    //Secret, Triggerer
        public UnityAction<Card, Card> onSecretResolve;    //Secret, Triggerer

        public UnityAction onRefresh;

        public Game game_data;


        private SlotMachineManager slotMachineManager;
        private SlotMachineUI slotMachineUI;

        private ResolveQueue resolve_queue;
        private bool is_ai_predict = false;

        private System.Random random = new System.Random();

        private ListSwap<Card> card_array = new ListSwap<Card>();
        private ListSwap<Player> player_array = new ListSwap<Player>();
        private ListSwap<CardPositionSlot> slot_array = new ListSwap<CardPositionSlot>();
        private ListSwap<CardData> card_data_array = new ListSwap<CardData>();
        private List<Card> cards_to_clear = new List<Card>();

        private List<SlotData> default_slot_data;
        private FieldSlotManager fieldSlotManager;

        public GameLogicService(bool is_ai)
        {
            resolve_queue = new ResolveQueue(null, is_ai);
            is_ai_predict = is_ai;
        }

        public GameLogicService(Game game)
        {
            // ── Reel Configuration ───────────────────────────────────────────
            // All reels are 8 symbols (uniform). Wrench appears on outer reels
            // only (L and R) so it is never in the center of a clean pass read.
            // Probabilities (middle row, 512 combos):
            //   Football ≥1  ≈ 80.5%   Football ≥2  ≈ 37.5%   Football ≥3 ≈  7.0%
            //   Helmet   ≥1  ≈ 70.7%   Helmet   ≥2  ≈ 25.8%   Helmet   ≥3 ≈  3.5%
            //   Star     ≥1  ≈ 33.0%   Star     ≥2  ≈  4.3%
            //   Wrench   ≥1  ≈ 23.4%   Wrench   ≥2  ≈  1.6%  (L+R only, never center)
            //   Wild     ≥1  ≈ 12.5%                          (center reel only)
            // Multi-icon AND conditions:
            //   Football+Helmet ≥1 each ≈ 52.7%   Star+Football ≥1 each ≈ 22.9%
            //   Star+Helmet     ≥1 each ≈ 19.3%
            default_slot_data = new List<SlotData>
            {
                new SlotData()  // Reel 0 — Left (8 symbols, has Wrench, no Wild)
                {
                    id = 0,
                    reelIconInventory = new List<SlotIconData>
                    {
                        new SlotIconData(SlotMachineIconType.Football, 4),
                        new SlotIconData(SlotMachineIconType.Helmet, 2),
                        new SlotIconData(SlotMachineIconType.Star, 1),
                        new SlotIconData(SlotMachineIconType.Wrench, 1),
                    },
                    stopDelay = 1.5f
                },
                new SlotData()  // Reel 1 — Center (8 symbols, Wild only, no Wrench)
                {
                    id = 1,
                    reelIconInventory = new List<SlotIconData>
                    {
                        new SlotIconData(SlotMachineIconType.Football, 3),
                        new SlotIconData(SlotMachineIconType.Helmet, 3),
                        new SlotIconData(SlotMachineIconType.Star, 1),
                        new SlotIconData(SlotMachineIconType.WildCard, 1),
                    },
                    stopDelay = 2.0f
                },
                new SlotData()  // Reel 2 — Right (8 symbols, has Wrench, no Wild)
                {
                    id = 2,
                    reelIconInventory = new List<SlotIconData>
                    {
                        new SlotIconData(SlotMachineIconType.Football, 3),
                        new SlotIconData(SlotMachineIconType.Helmet, 3),
                        new SlotIconData(SlotMachineIconType.Star, 1),
                        new SlotIconData(SlotMachineIconType.Wrench, 1),
                    },
                    stopDelay = 2.5f
                },
            };
            game_data = game;
            resolve_queue = new ResolveQueue(game, false);
            slotMachineManager = new SlotMachineManager(default_slot_data);
            slotMachineUI = UnityEngine.Object.FindFirstObjectByType<SlotMachineUI>();
            slotMachineUI.InitializeReels(3);

            fieldSlotManager = UnityEngine.Object.FindFirstObjectByType<FieldSlotManager>();

            foreach (var player in game.players)
            {
                if (player.head_coach?.coachData != null)
                    player.coachManager = new CoachManager(player.head_coach.coachData, player, game, this);
            }
        }

        public virtual void SetData(Game game)
        {
            game_data = game;
            resolve_queue.SetData(game);
        }

        public virtual void Update(float delta)
        {
            resolve_queue.Update(delta);
        }

        // Phase/turn lifecycle (StartGame → ClearLiveBallCards) → GameLogicService.Lifecycle.cs

        // Card actions (SelectPlayerCardForBoard → RollRandomValue) → GameLogicService.CardAction.cs

        // Ability system (TriggerCardAbilityType → GoToMulligan) → GameLogicService.Abilities.cs

        // Ongoing stats (UpdateOngoing → AddOngoingStatusBonus) → GameLogicService.Stats.cs

        // Play outcome / yardage (ResolvePlayOutcome → ResolveRunOutcome) → GameLogicService.Yardage.cs

        public void PlayTriggeredSlotSpin()
        {
            SlotMachineResultDTO finalResults = slotMachineManager.CalculateSpinResults();
            slotMachineUI.FireReelUI(finalResults.Results, finalResults.SlotDataCopy);
        }

        //-------------

        public virtual void RefreshData()
        {
            onRefresh?.Invoke();
        }

        public virtual void ClearResolve()
        {
            resolve_queue.Clear();
        }

        public virtual bool IsResolving()
        {
            return resolve_queue.IsResolving();
        }

        public virtual bool IsGameStarted()
        {
            return game_data.HasStarted();
        }

        public virtual bool IsGameEnded()
        {
            return game_data.HasEnded();
        }

        public virtual Game GetGameData()
        {
            return game_data;
        }

        public System.Random GetRandom()
        {
            return random;
        }

        public Game GameData { get { return game_data; } }
        public ResolveQueue ResolveQueue { get { return resolve_queue; } }
    }
}

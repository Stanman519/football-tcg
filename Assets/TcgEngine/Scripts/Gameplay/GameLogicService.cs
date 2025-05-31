using Assets.TcgEngine.Scripts.Gameplay;
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
    /// Execute and resolves game rules and logic
    /// </summary>

    public class GameLogicService
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
            //is_instant ignores all gameplay delays and process everything immediately, needed for AI prediction
            resolve_queue = new ResolveQueue(null, is_ai);
            is_ai_predict = is_ai;
        }

        public GameLogicService(Game game)
        {

            default_slot_data = new List<SlotData>
            {
                new SlotData()
                {
                    id = 0,
                    reelIconInventory = new List<SlotIconData>
                    {
                        new SlotIconData(SlotMachineIconType.Football, 3),
                        new SlotIconData(SlotMachineIconType.Helmet, 2),
                        new SlotIconData(SlotMachineIconType.Star, 1),
                        new SlotIconData(SlotMachineIconType.Wrench, 1),
                    },
                    stopDelay = 1.5f
                },
                new SlotData()
                {
                    id = 1,
                    reelIconInventory = new List<SlotIconData>
                    {
                        new SlotIconData(SlotMachineIconType.Football, 3),
                        new SlotIconData(SlotMachineIconType.Helmet, 2),
                        new SlotIconData(SlotMachineIconType.Star, 1),
                        new SlotIconData(SlotMachineIconType.Wrench, 1),
                        new SlotIconData(SlotMachineIconType.WildCard, 1),
                    },
                    stopDelay = 2.0f
                },
                new SlotData()
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

        //----- Turn Phases ----------

        public virtual void StartGame()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data != null && game_data.players.Count() >= 2)
            {
                fieldSlotManager.GenerateSlotsForPlayer(game_data.players[0]); // Generate slots for player 1
                fieldSlotManager.GenerateSlotsForPlayer(game_data.players[1]); // Generate slots for player 2
            }
            //Choose first player
            game_data.state = GameState.Play;
            game_data.current_offensive_player = random.NextDouble() < 0.5 ? game_data.players[0] : game_data.players[1];

            game_data.turn_count = 1;

            //Adventure settings
            /*            bool should_mulligan = GameplayData.Get().mulligan;
                        LevelData level = game_data.settings.GetLevel();
                        if (level != null)
                        {
                            if (level != null && level.first_player == LevelFirst.Player)
                                game_data.first_offense_player = 0;
                            if (level != null && level.first_player == LevelFirst.AI)
                                game_data.first_offense_player = 1;
                            game_data.current_offsense_player = game_data.first_offense_player;
                            should_mulligan = level.mulligan;
                        }*/

            //Init each players
            foreach (Player player in game_data.players)
            {


                //Hp / mana
                /*                player.hp_max = GameplayData.Get().hp_start;
                                player.hp = player.hp_max;
                                player.mana_max = GameplayData.Get().mana_start;
                                player.mana = player.mana_max;*/

                //Draw starting cards
                int dcards = GameplayData.Get().cards_start;
                DrawCard(player, dcards);

                //Add coin second player
                bool is_random = true; //level == null || level.first_player == LevelFirst.Random;
                if (is_random && player != game_data.current_offensive_player)
                {
                    Card card = Card.Create(GameplayData.Get().second_bonus, VariantData.GetDefault(), player);
                    player.cards_hand.Add(card);
                }
            }

            //Start state
            RefreshData();
            onGameStart?.Invoke();

            if (false) //should_mulligan)
                GoToMulligan();
            else
                StartTurn();
        }

        public virtual void StartTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            ClearTurnData();
            game_data.phase = GamePhase.StartTurn;
            onTurnStart?.Invoke();

            // Reset readiness at the start of each play
            foreach (Player player in game_data.players)
            {
                player.ResetReadyState();
            }

            foreach (Player player in game_data.players)
            {

                //Cards draw
                if (game_data.turn_count > 1 || player != game_data.current_offensive_player)
                {
                    DrawCard(player, GameplayData.Get().cards_per_turn);
                }
                // TODO: I dont know why this was here
                // player.history_list.Clear();

                //Refresh Cards and Status Effects
                for (int i = player.cards_board.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_board[i];

                    if (!card.HasStatus(StatusType.Sleep))
                        card.Refresh();
                }

                //Turn timer and history
                game_data.turn_timer = GameplayData.Get().turn_duration;


                //TODO: ? Ongoing Abilities
                // UpdateOngoing();

                //StartTurn Abilities
                TriggerPlayerCardsAbilityType(player, AbilityTrigger.StartOfTurn);
            }


            StartChoosePlayersPhase();

        }

        private void StartChoosePlayersPhase()
        {

            game_data.phase = GamePhase.ChoosePlayers;
            RefreshData();
            // Wait for both players to choose their player cards
            resolve_queue.AddCallback(WaitForPlayerSelection);
            resolve_queue.ResolveAll();
            //TransitionToPlaycall(); this might be being done already above.
        }

        private void TransitionToPlaycall()
        {
            resolve_queue.AddCallback(RevealNewPlayers);
            resolve_queue.ResolveAll(0.2f);

            resolve_queue.AddCallback(StartPlayCallPhase);
        }

        public virtual void StartNextTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            if (game_data.current_offensive_player == game_data.current_offensive_player)
                game_data.turn_count++;

            CheckForWinner();
            StartTurn();
        }

        public virtual void SummonNewPlayers()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.ChoosePlayers;

            onNewPlayerReveal?.Invoke();
            RefreshData();
        }

        public virtual void RevealNewPlayers()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.RevealPlayers;

            game_data.players.SelectMany(p => p.cards_board).ToList().ForEach(c => c.is_hidden = false);

            onNewPlayerReveal?.Invoke();
            RefreshData();
        }

        public virtual void StartPlayCallPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.ChoosePlay;
            RefreshData();

            onChoosePlay?.Invoke();

            resolve_queue.AddCallback(WaitForPlayCallSelection);
            resolve_queue.ResolveAll();
        }

        private void WaitForPlayCallSelection()
        {
            if (AllPlayersReadyForPhase(GamePhase.ChoosePlay))
            {
                RevealPlayCalls();
                resolve_queue.AddCallback(StartSlotSpinPhase);
                resolve_queue.ResolveAll();
            }
        }
        public virtual void RevealPlayCalls()
        {
            game_data.phase = GamePhase.RevealPlayCalls;
            onPlaycallReveal?.Invoke();
            RefreshData();
        }
        public virtual void StartSlotSpinPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.SlotSpin;
            onSlotSpinStart?.Invoke();
            RefreshData();

            PlayTriggeredSlotSpin();


            resolve_queue.AddCallback(ResolvePlayOutcome);
            resolve_queue.ResolveAll(1.5f);
        }
        public virtual void StartLiveBallPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.LiveBall;
            RefreshData();

            // Wait for both players to play Live Ball cards
            resolve_queue.AddCallback(WaitForLiveBallSelection);
            resolve_queue.ResolveAll();
        }
        public virtual void EndPlayPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            // Check yardage, downs, turnovers
            game_data.current_down++;
            game_data.raw_ball_on += game_data.yardage_this_play; // Update ball position

            if (game_data.current_down > 4 || game_data.raw_ball_on >= 100)
            {
                HandleTurnoverOrScore();
            }
            else
            {
                StartTurn();
            }
        }

        private void WaitForLiveBallSelection()
        {
            if (AllPlayersReadyForPhase(GamePhase.LiveBall))
            {
                ResolveLiveBallEffects();
                resolve_queue.AddCallback(EndPlayPhase);
                resolve_queue.ResolveAll();
            }
        }
/*        public virtual void StartMainPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.Main;
            onTurnPlay?.Invoke();
            RefreshData();
        }*/
        private bool AllPlayersReadyForPhase(GamePhase phase)
        {
            foreach (Player player in game_data.players)
            {
                if (!player.IsReadyForPhase(phase))
                    return false;  // At least one player is not ready
            }
            return true;  // All players have confirmed
        }
        public virtual void EndTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data.phase != GamePhase.LiveBall)
                return;

            game_data.selector = SelectorType.None;
            game_data.phase = GamePhase.EndTurn;

            //Reduce status effects with duration
            foreach (Player aplayer in game_data.players)
            {
                aplayer.ReduceStatusDurations();
                foreach (Card card in aplayer.cards_board)
                    card.ReduceStatusDurations();
                foreach (Card card in aplayer.cards_equip)
                    card.ReduceStatusDurations();
            }

            TriggerPlayerCardsAbilityType(game_data.current_offensive_player, AbilityTrigger.EndOfTurn);
            TriggerPlayerCardsAbilityType(game_data.GetCurrentDefensivePlayer(), AbilityTrigger.EndOfTurn);

            onTurnEnd?.Invoke();
            RefreshData();

            resolve_queue.AddCallback(StartNextTurn);
            resolve_queue.ResolveAll(0.2f);
        }

        //End game with winner
        public virtual void EndGame(int winner)
        {
            if (game_data.state != GameState.GameEnded)
            {
                game_data.state = GameState.GameEnded;
                game_data.phase = GamePhase.None;
                game_data.selector = SelectorType.None;
                resolve_queue.Clear();
                Player player = game_data.GetPlayer(winner);
                onGameEnd?.Invoke(player);
                RefreshData();
            }
        }

        //Progress to the next step/phase 
        public virtual void NextStep()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            if (game_data.phase == GamePhase.Mulligan)
            {
                StartTurn();
                return;
            }

            CancelSelection();

            //Add to resolve queue in case its still resolving
            resolve_queue.AddCallback(EndTurn);
            resolve_queue.ResolveAll();
        }

        //Check if a player is winning the game, if so end the game
        //Change or edit this function for a new win condition
        protected virtual void CheckForWinner()
        {
            int count_alive = 0;
            Player alive = null;
            foreach (Player player in game_data.players)
            {
                if (!player.IsDead())
                {
                    alive = player;
                    count_alive++;
                }
            }

/*            if (count_alive == 0)
            {
                EndGame(-1); //Everyone is dead, Draw
            }
            else if (count_alive == 1)
            {
                EndGame(alive.player_id); //Player win
            }*/
        }

        protected virtual void ClearTurnData()
        {
            game_data.selector = SelectorType.None;
            resolve_queue.Clear();
            card_array.Clear();
            player_array.Clear();
            slot_array.Clear();
            card_data_array.Clear();
            game_data.last_played = null;
            game_data.last_destroyed = null;
            game_data.last_target = null;
            game_data.last_summoned = null;
            game_data.ability_triggerer = null;
            game_data.selected_value = 0;
            game_data.ability_played.Clear();
            game_data.cards_attacked.Clear();
        }

        //--- Setup ------

        //Set deck using a Deck in Resources
        public virtual void SetPlayerDeck(Player player, DeckData deck)
        {
            player.cards_all.Clear();
            player.cards_deck.Clear();
            player.deck = deck.id;
            player.hero = null;

            VariantData variant = VariantData.GetDefault();
            if (deck.hero != null)
            {
                player.hero = Card.Create(deck.hero, variant, player);
            }

            foreach (CardData card in deck.cards)
            {
                if (card != null)
                {
                    Card acard = Card.Create(card, variant, player);
                    player.cards_deck.Add(acard);
                }
            }

            DeckPuzzleData puzzle = deck as DeckPuzzleData;

            //Board cards

            //TURNED OFF BECAUSE I DONT CARE ABOUT PUZZLE ATM

/*            if (puzzle != null)
            {
                foreach (DeckCardSlot card in puzzle.board_cards)
                {
                    Card acard = Card.Create(card.card, variant, player);
                    acard.slot = new CardPositionSlot(card.slot, CardPositionSlot.GetP(player.player_id));
                    player.cards_board.Add(acard);
                }
            }*/

            //Shuffle deck
            if (puzzle == null || !puzzle.dont_shuffle_deck)
                ShuffleDeck(player.cards_deck);
        }

        private void WaitForPlayerSelection()
        {
            if (AllPlayersReadyForPhase(GamePhase.ChoosePlay))
            {
                RevealNewPlayers();
                resolve_queue.AddCallback(StartPlayCallPhase);
                resolve_queue.ResolveAll();
            }
        }
        //Set deck using custom deck in save file or database
        public virtual void SetPlayerDeck(Player player, UserDeckData deck)
        {
            player.cards_all.Clear();
            player.cards_deck.Clear();
            player.deck = deck.tid;
            player.hero = null;

            if (deck.hero != null)
            {
                CardData hdata = CardData.Get(deck.hero.tid);
                VariantData hvariant = VariantData.Get(deck.hero.variant);
                if (hdata != null && hvariant != null)
                    player.hero = Card.Create(hdata, hvariant, player);
            }

            foreach (UserCardData card in deck.cards)
            {
                CardData icard = CardData.Get(card.tid);
                VariantData variant = VariantData.Get(card.variant);
                if (icard != null && variant != null)
                {
                    for (int i = 0; i < card.quantity; i++)
                    {
                        Card acard = Card.Create(icard, variant, player);
                        player.cards_deck.Add(acard);
                    }
                }
            }

            //Shuffle deck
            ShuffleDeck(player.cards_deck);
        }

        //---- Gameplay Actions --------------

        public virtual void SelectPlayerCardForBoard(Card card)
        {
            // check l
        }


        public virtual void PlayCard(Card card, CardPositionSlot slot, bool skip_cost = false)
        {
            if (game_data.CanPlayCard(card, slot, skip_cost))
            {
                Player player = game_data.GetPlayer(card.player_id);


                //Play card
                player.RemoveCardFromAllGroups(card);

                //Add to board
                CardData icard = card.CardData;
                if (icard.IsBoardCard())
                {
                    player.cards_board.Add(card);
                    card.slot = slot;
                    card.exhausted = true; //Cant attack first turn
                }
                else if (icard.IsEquipment())
                {
                    List<Card> bearer = game_data.GetSlotCards(slot);
                    EquipCard(bearer[0], card); // TODO: just doing to first card to make it work
                    card.exhausted = true;
                }
                else if (icard.IsSecret())
                {
                    player.cards_secret.Add(card);
                }
                else
                {
                    player.cards_discard.Add(card);
                    card.slot = slot; //Save slot in case spell has PlayTarget
                }

                //History
                if (!is_ai_predict && !icard.IsSecret())
                    player.AddHistory(GameAction.PlayCard, card);

                //Update ongoing effects
                game_data.last_played = card.uid;
                UpdateOngoing();

                //Trigger abilities
                if (card.CardData.IsDynamicManaCost())
                {
                    GoToSelectorCost(card);
                }
                else
                {
                    TriggerSecrets(AbilityTrigger.OnPlayOther, card); //After playing card
                    TriggerCardAbilityType(AbilityTrigger.OnPlay, card);
                    TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, card);
                }

                RefreshData();

                onCardPlayed?.Invoke(card, slot);
                resolve_queue.ResolveAll(0.3f);
            }
        }

        public virtual void MoveCard(Card card, CardPositionSlot slot, bool skip_cost = false)
        {
            if (game_data.CanMoveCard(card, slot, skip_cost))
            {
                card.slot = FindNextOpenPosition(slot);

                GameClient.Get().Move(card, card.slot);

                RefreshData();
            }
            /*            if (game_data.CanMoveCard(card, slot, skip_cost))
                        {
                            card.slot = slot;

                            //Moving doesn't really have any effect in demo so can be done indefinitely
                            //if(!skip_cost)
                            //card.exhausted = true;
                            //card.RemoveStatus(StatusEffect.Stealth);
                            //player.AddHistory(GameAction.Move, card);

                            //Also move the equipment
                            Card equip = game_data.GetEquipCard(card.equipped_uid);
                            if (equip != null)
                                equip.slot = slot;

                            UpdateOngoing();
                            RefreshData();

                            onCardMoved?.Invoke(card, slot);
                            resolve_queue.ResolveAll(0.2f);
                        }*/

        }

        // Finds the next open position in the group
        private CardPositionSlot FindNextOpenPosition(CardPositionSlot slot)
        {
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();

            // Get all slots in this position group
            List<Card> existingPlayers = gdata.GetSlotCards(slot, player.player_id);

            if (existingPlayers.Count < slot.max_cards)
            {
                return new CardPositionSlot(player.player_id, 1, slot.posGroupType);
            }

            return CardPositionSlot.None; // No open positions
        }
        public virtual void CastAbility(Card card, AbilityData iability)
        {
            if (game_data.CanCastAbility(card, iability))
            {
                Player player = game_data.GetPlayer(card.player_id);
                if (!is_ai_predict && iability.target != AbilityTarget.SelectTarget)
                    player.AddHistory(GameAction.CastAbility, card, iability);
                card.RemoveStatus(StatusType.Stealth);
                TriggerCardAbility(iability, card);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void AttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            if (game_data.CanAttackTarget(attacker, target, skip_cost))
            {
                Player player = game_data.GetPlayer(attacker.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.Attack, attacker, target);

                game_data.last_target = target.uid;

                //Trigger before attack abilities
                TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);
                TriggerCardAbilityType(AbilityTrigger.OnBeforeDefend, target, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeDefend, target);

                //Resolve attack
                resolve_queue.AddAttack(attacker, target, ResolveAttack, skip_cost);
                resolve_queue.ResolveAll();
            }
        }

        protected virtual void ResolveAttack(Card attacker, Card target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker) || !game_data.IsOnBoard(target))
                return;

            onAttackStart?.Invoke(attacker, target);

            attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackHit, skip_cost);
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackHit(Card attacker, Card target, bool skip_cost)
        {
            //Count attack damage
            int datt1 = attacker.GetAttack();
            int datt2 = target.GetAttack();

            //Damage Cards
            DamageCard(attacker, target, datt1);

            //Counter Damage
            if (!attacker.HasStatus(StatusType.Intimidate))
                DamageCard(target, attacker, datt2);

            //Save attack and exhaust
            if (!skip_cost)
                ExhaustBattle(attacker);

            //Recalculate bonus
            UpdateOngoing();

            //Abilities
            bool att_board = game_data.IsOnBoard(attacker);
            bool def_board = game_data.IsOnBoard(target);
            if (att_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);
            if (def_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDefend, target, attacker);
            if (att_board)
                TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);
            if (def_board)
                TriggerSecrets(AbilityTrigger.OnAfterDefend, target);

            onAttackEnd?.Invoke(attacker, target);
            RefreshData();
            CheckForWinner();

            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void AttackPlayer(Card attacker, Player target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return;

            if (!game_data.CanAttackTarget(attacker, target, skip_cost))
                return;

            Player player = game_data.GetPlayer(attacker.player_id);
            if (!is_ai_predict)
                player.AddHistory(GameAction.AttackPlayer, attacker, target);

            //Resolve abilities
            TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
            TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);

            //Resolve attack
            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayer, skip_cost);
            resolve_queue.ResolveAll();
        }

        protected virtual void ResolveAttackPlayer(Card attacker, Player target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker))
                return;

            onAttackPlayerStart?.Invoke(attacker, target);

            attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayerHit, skip_cost);
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackPlayerHit(Card attacker, Player target, bool skip_cost)
        {
            DamagePlayer(attacker, target, attacker.GetAttack());

            //Save attack and exhaust
            if (!skip_cost)
                ExhaustBattle(attacker);

            //Recalculate bonus
            UpdateOngoing();

            if (game_data.IsOnBoard(attacker))
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);

            TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);

            onAttackPlayerEnd?.Invoke(attacker, target);
            RefreshData();
            CheckForWinner();

            resolve_queue.ResolveAll(0.2f);
        }

        //Exhaust after battle
        public virtual void ExhaustBattle(Card attacker)
        {
            bool attacked_before = game_data.cards_attacked.Contains(attacker.uid);
            game_data.cards_attacked.Add(attacker.uid);
            bool attack_again = attacker.HasStatus(StatusType.Fury) && !attacked_before;
            attacker.exhausted = !attack_again;
        }

        //Redirect attack to a new target
        public virtual void RedirectAttack(Card attacker, Card new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.target = new_target;
                    att.ptarget = null;
                    att.callback = ResolveAttack;
                    att.pcallback = null;
                }
            }
        }

        public virtual void RedirectAttack(Card attacker, Player new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.ptarget = new_target;
                    att.target = null;
                    att.pcallback = ResolveAttackPlayer;
                    att.callback = null;
                }
            }
        }

        public virtual void ShuffleDeck(List<Card> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card temp = cards[i];
                int randomIndex = random.Next(i, cards.Count);
                cards[i] = cards[randomIndex];
                cards[randomIndex] = temp;
            }
        }

        public virtual void DrawCard(Player player, int nb = 1)
        {
            for (int i = 0; i < nb; i++)
            {
                if (player.cards_deck.Count > 0 && player.cards_hand.Count < GameplayData.Get().cards_max)
                {
                    Card card = player.cards_deck[0];
                    player.cards_deck.RemoveAt(0);
                    player.cards_hand.Add(card);
                }
            }

            onCardDrawn?.Invoke(nb);
        }

        //Put a card from deck into discard
        public virtual void DrawDiscardCard(Player player, int nb = 1)
        {
            for (int i = 0; i < nb; i++)
            {
                if (player.cards_deck.Count > 0)
                {
                    Card card = player.cards_deck[0];
                    player.cards_deck.RemoveAt(0);
                    player.cards_discard.Add(card);
                }
            }
        }

        //Summon copy of an exiting card
        public virtual Card SummonCopy(Player player, Card copy, CardPositionSlot slot)
        {
            CardData icard = copy.CardData;
            return SummonCard(player, icard, copy.VariantData, slot);
        }

        //Summon copy of an exiting card into hand
        public virtual Card SummonCopyHand(Player player, Card copy)
        {
            CardData icard = copy.CardData;
            return SummonCardHand(player, icard, copy.VariantData);
        }

        //Create a new card and send it to the board
        public virtual Card SummonCard(Player player, CardData card, VariantData variant, CardPositionSlot slot)
        {
            if (!slot.IsValid())
                return null;

            if (game_data.GetSlotCards(slot).Count > 0) //TODO: fix for multi card slots
                return null;

            Card acard = SummonCardHand(player, card, variant);
            PlayCard(acard, slot, true);

            onCardSummoned?.Invoke(acard, slot);

            return acard;
        }

        //Create a new card and send it to your hand
        public virtual Card SummonCardHand(Player player, CardData card, VariantData variant)
        {
            Card acard = Card.Create(card, variant, player);
            player.cards_hand.Add(acard);
            game_data.last_summoned = acard.uid;
            return acard;
        }

        //Transform card into another one
        public virtual Card TransformCard(Card card, CardData transform_to)
        {
            card.SetCard(transform_to, card.VariantData);

            onCardTransformed?.Invoke(card);

            return card;
        }

        public virtual void EquipCard(Card card, Card equipment)
        {
            if (card != null && equipment != null && card.player_id == equipment.player_id)
            {
                if (!card.CardData.IsEquipment() && equipment.CardData.IsEquipment())
                {
                    UnequipAll(card); //Unequip previous cards, only 1 equip at a time

                    Player player = game_data.GetPlayer(card.player_id);
                    player.RemoveCardFromAllGroups(equipment);
                    player.cards_equip.Add(equipment);
                    card.equipped_uid = equipment.uid;
                    equipment.slot = card.slot;
                }
            }
        }

        public virtual void UnequipAll(Card card)
        {
            if (card != null && card.equipped_uid != null)
            {
                Player player = game_data.GetPlayer(card.player_id);
                Card equip = player.GetEquipCard(card.equipped_uid);
                if (equip != null)
                {
                    card.equipped_uid = null;
                    DiscardCard(equip);
                }
            }
        }

        //Change owner of a card
        public virtual void ChangeOwner(Card card, Player owner)
        {
            if (card.player_id != owner.player_id)
            {
                Player powner = game_data.GetPlayer(card.player_id);
                powner.RemoveCardFromAllGroups(card);
                powner.cards_all.Remove(card.uid);
                owner.cards_all[card.uid] = card;
                card.player_id = owner.player_id;
            }
        }

        //Damage a player
        public virtual void DamagePlayer(Card attacker, Player target, int value)
        {
            //Damage player
            target.hp -= value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);

            //Lifesteal
            Player aplayer = game_data.GetPlayer(attacker.player_id);
            if (attacker.HasStatus(StatusType.LifeSteal))
                aplayer.hp += value;

            onPlayerDamaged?.Invoke(target, value);
        }

        //Heal a card
        public virtual void HealCard(Card target, int value)
        {
            if (target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return;

            target.damage -= value;
            target.damage = Mathf.Max(target.damage, 0);

            onCardHealed?.Invoke(target, value);
        }

        public virtual void HealPlayer(Player target, int value)
        {
            if (target == null)
                return;

            target.hp += value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);

            onPlayerHealed?.Invoke(target, value);
        }

        //Generic damage that doesnt come from another card
        public virtual void DamageCard(Card target, int value)
        {
            if (target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity))
                return; //Spell immunity

            target.damage += value;

            onCardDamaged?.Invoke(target, value);

            if (target.GetHP() <= 0)
                DiscardCard(target);
        }

        //Damage a card with attacker/caster
        public virtual void DamageCard(Card attacker, Card target, int value, bool spell_damage = false)
        {
            if (attacker == null || target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity) && attacker.CardData.type != CardType.OffensivePlayer)
                return; //Spell immunity

            //Shell
            bool doublelife = target.HasStatus(StatusType.Shell);
            if (doublelife && value > 0)
            {
                target.RemoveStatus(StatusType.Shell);
                return;
            }

            //Armor
            if (!spell_damage && target.HasStatus(StatusType.Armor))
                value = Mathf.Max(value - target.GetStatusValue(StatusType.Armor), 0);

            //Damage
            int damage_max = Mathf.Min(value, target.GetHP());
            int extra = value - target.GetHP();
            target.damage += value;

            //Trample
            Player tplayer = game_data.GetPlayer(target.player_id);
            if (!spell_damage && extra > 0 && attacker.player_id == game_data.current_offensive_player.player_id && attacker.HasStatus(StatusType.Trample))
                tplayer.hp -= extra;

            //Lifesteal
            Player player = game_data.GetPlayer(attacker.player_id);
            if (!spell_damage && attacker.HasStatus(StatusType.LifeSteal))
                player.hp += damage_max;

            //Remove sleep on damage
            target.RemoveStatus(StatusType.Sleep);

            //Callback
            onCardDamaged?.Invoke(target, value);

            //Deathtouch
            if (value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.type == CardType.OffensivePlayer)
                KillCard(attacker, target);

            //Kill card if no hp
            if (target.GetHP() <= 0)
                KillCard(attacker, target);
        }

        //A card that kills another card
        public virtual void KillCard(Card attacker, Card target)
        {
            if (attacker == null || target == null)
                return;

            if (!game_data.IsOnBoard(target) && !game_data.IsEquipped(target))
                return; //Already killed

            if (target.HasStatus(StatusType.Invincibility))
                return; //Cant be killed

            Player pattacker = game_data.GetPlayer(attacker.player_id);
            if (attacker.player_id != target.player_id)
                pattacker.kill_count++;

            DiscardCard(target);

            TriggerCardAbilityType(AbilityTrigger.OnKill, attacker, target);
        }

        //Send card into discard
        public virtual void DiscardCard(Card card)
        {
            if (card == null)
                return;

            if (game_data.IsInDiscard(card))
                return; //Already discarded

            CardData icard = card.CardData;
            Player player = game_data.GetPlayer(card.player_id);
            bool was_on_board = game_data.IsOnBoard(card) || game_data.IsEquipped(card);

            //Unequip card
            UnequipAll(card);

            //Remove card from board and add to discard
            player.RemoveCardFromAllGroups(card);
            player.cards_discard.Add(card);
            game_data.last_destroyed = card.uid;

            //Remove from bearer
            Card bearer = player.GetBearerCard(card);
            if (bearer != null)
                bearer.equipped_uid = null;

            if (was_on_board)
            {
                //Trigger on death abilities
                TriggerCardAbilityType(AbilityTrigger.OnDeath, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnDeathOther, card);
                TriggerSecrets(AbilityTrigger.OnDeathOther, card);
                UpdateOngoingCards(); //Not UpdateOngoing() here to avoid recursive calls in UpdateOngoingKills
            }

            cards_to_clear.Add(card); //Will be Clear() in the next UpdateOngoing, so that simultaneous damage effects work
            onCardDiscarded?.Invoke(card);
        }

        public int RollRandomValue(int dice)
        {
            return RollRandomValue(1, dice + 1);
        }

        public virtual int RollRandomValue(int min, int max)
        {
            game_data.rolled_value = random.Next(min, max);
            onRollValue?.Invoke(game_data.rolled_value);
            resolve_queue.SetDelay(1f);
            return game_data.rolled_value;
        }

        //--- Abilities --

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Card triggerer = null)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Player triggerer)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerOtherCardsAbilityType(AbilityTrigger type, Card triggerer)
        {
            foreach (Player oplayer in game_data.players)
            {
                if (oplayer.hero != null)
                    TriggerCardAbilityType(type, oplayer.hero, triggerer);

                foreach (Card card in oplayer.cards_board)
                    TriggerCardAbilityType(type, card, triggerer);
            }
        }

        public virtual void TriggerPlayerCardsAbilityType(Player player, AbilityTrigger type)
        {
            if (player.hero != null)
                TriggerCardAbilityType(type, player.hero, player.hero);

            foreach (Card card in player.cards_board)
                TriggerCardAbilityType(type, card, card);
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster)
        {
            TriggerCardAbility(iability, caster, caster);
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Card triggerer)
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, trigger_card))
            {
                resolve_queue.AddAbility(iability, caster, trigger_card, ResolveCardAbility);
            }
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Player triggerer)
        {
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, triggerer))
            {
                resolve_queue.AddAbility(iability, caster, caster, ResolveCardAbility);
            }
        }

        public virtual void TriggerAbilityDelayed(AbilityData iability, Card caster)
        {
            resolve_queue.AddAbility(iability, caster, caster, TriggerCardAbility);
        }

        public virtual void TriggerAbilityDelayed(AbilityData iability, Card caster, Card triggerer)
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set
            resolve_queue.AddAbility(iability, caster, trigger_card, TriggerCardAbility);
        }

        //Resolve a card ability, may stop to ask for target
        protected virtual void ResolveCardAbility(AbilityData iability, Card caster, Card triggerer)
        {
            if (!caster.CanDoAbilities())
                return; //Silenced card cant cast

            //Debug.Log("Trigger Ability " + iability.id + " : " + caster.card_id);

            onAbilityStart?.Invoke(iability, caster);
            game_data.ability_triggerer = triggerer.uid;
            game_data.ability_played.Add(iability.id);

            bool is_selector = ResolveCardAbilitySelector(iability, caster);
            if (is_selector)
                return; //Wait for player to select

            ResolveCardAbilityPlayTarget(iability, caster);
            ResolveCardAbilityPlayers(iability, caster);
            ResolveCardAbilityCards(iability, caster);
            ResolveCardAbilitySlots(iability, caster);
            ResolveCardAbilityCardData(iability, caster);
            ResolveCardAbilityNoTarget(iability, caster);
            AfterAbilityResolved(iability, caster);
        }

        protected virtual bool ResolveCardAbilitySelector(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.SelectTarget)
            {
                //Wait for target
                GoToSelectTarget(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.CardSelector)
            {
                GoToSelectorCard(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.ChoiceSelector)
            {
                GoToSelectorChoice(iability, caster);
                return true;
            }
            return false;
        }

        protected virtual void ResolveCardAbilityPlayTarget(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.PlayTarget)
            {
                CardPositionSlot slot = caster.slot;
                List<Card> slot_cards = game_data.GetSlotCards(slot);
                if (slot.IsPlayerSlot())
                {
                    Player tplayer = game_data.GetPlayer(slot.p);
                    if (iability.CanTarget(game_data, caster, tplayer))
                        ResolveEffectTarget(iability, caster, tplayer);
                }
                else if (slot_cards.Count > 0)
                {
                    if (iability.CanTarget(game_data, caster, slot_cards[0])) // TODO: fix this, just working on multi card slots.
                    {
                        game_data.last_target = slot_cards[0].uid;
                        ResolveEffectTarget(iability, caster, slot_cards[0]);
                    }
                }
                else
                {
                    if (iability.CanTarget(game_data, caster, slot))
                        ResolveEffectTarget(iability, caster, slot);
                }
            }
        }

        protected virtual void ResolveCardAbilityPlayers(AbilityData iability, Card caster)
        {
            //Get Player Targets based on conditions
            List<Player> targets = iability.GetPlayerTargets(game_data, caster, player_array);

            //Resolve effects
            foreach (Player target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCards(AbilityData iability, Card caster)
        {
            //Get Cards Targets based on conditions
            List<Card> targets = iability.GetCardTargets(game_data, caster, card_array);

            //Resolve effects
            foreach (Card target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilitySlots(AbilityData iability, Card caster)
        {
            //Get Slot Targets based on conditions
            List<CardPositionSlot> targets = iability.GetSlotTargets(game_data, caster, slot_array);

            //Resolve effects
            foreach (CardPositionSlot target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCardData(AbilityData iability, Card caster)
        {
            //Get Cards Targets based on conditions
            List<CardData> targets = iability.GetCardDataTargets(game_data, caster, card_data_array);

            //Resolve effects
            foreach (CardData target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityNoTarget(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.None)
                iability.DoEffects(this, caster);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Player target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetPlayer?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Card target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetCard?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, CardPositionSlot target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetSlot?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, CardData target)
        {
            iability.DoEffects(this, caster, target);
        }

        protected virtual void AfterAbilityResolved(AbilityData iability, Card caster)
        {
            Player player = game_data.GetPlayer(caster.player_id);

            //Pay cost
            if (iability.trigger == AbilityTrigger.Activate || iability.trigger == AbilityTrigger.None)
            {
                caster.exhausted = caster.exhausted || iability.exhaust;
            }

            //Recalculate and clear
            UpdateOngoing();
            CheckForWinner();

            //Chain ability
            if (iability.target != AbilityTarget.ChoiceSelector && game_data.state != GameState.GameEnded)
            {
                foreach (AbilityData chain_ability in iability.chain_abilities)
                {
                    if (chain_ability != null)
                    {
                        TriggerCardAbility(chain_ability, caster);
                    }
                }
            }

            onAbilityEnd?.Invoke(iability, caster);
            resolve_queue.ResolveAll(0.5f);
            RefreshData();
        }

        //This function is called often to update status/stats affected by ongoing abilities
        //It basically first reset the bonus to 0 (CleanOngoing) and then recalculate it to make sure it it still present
        //Only cards in hand and on board are updated in this way
        public virtual void UpdateOngoing()
        {
            Profiler.BeginSample("Update Ongoing");
            UpdateOngoingCards(); //Update status and stats
            UpdateOngoingKills(); //Kill cards with 0 HP
            Profiler.EndSample();
        }

        protected virtual void UpdateOngoingCards()
        {
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                player.ClearOngoing();

                for (int c = 0; c < player.cards_board.Count; c++)
                    player.cards_board[c].ClearOngoing();

                for (int c = 0; c < player.cards_equip.Count; c++)
                    player.cards_equip[c].ClearOngoing();

                for (int c = 0; c < player.cards_hand.Count; c++)
                    player.cards_hand[c].ClearOngoing();
            }

            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                UpdateOngoingAbilities(player, player.hero);  //Remove this line if hero is on the board

                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];
                    UpdateOngoingAbilities(player, card);
                }

                for (int c = 0; c < player.cards_equip.Count; c++)
                {
                    Card card = player.cards_equip[c];
                    UpdateOngoingAbilities(player, card);
                }
            }

            //Stats bonus
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];

                    //Taunt effect
                    if (card.HasStatus(StatusType.Protection) && !card.HasStatus(StatusType.Stealth))
                    {
                        player.AddOngoingStatus(StatusType.Protected, 0);

                        for (int tc = 0; tc < player.cards_board.Count; tc++)
                        {
                            Card tcard = player.cards_board[tc];
                            if (!tcard.HasStatus(StatusType.Protection) && !tcard.HasStatus(StatusType.Protected))
                            {
                                tcard.AddOngoingStatus(StatusType.Protected, 0);
                            }
                        }
                    }

                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }

                for (int c = 0; c < player.cards_hand.Count; c++)
                {
                    Card card = player.cards_hand[c];
                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }
            }
        }
        private void ResolvePlayOutcome()
        {
            var playResolution = new PlayResolution();
            // TODO: Implement logic for resolving what happens after a play (e.g., determining success/failure)

            // TODO: order of potential play enders / turnover event priority, (TFL, Sack,tipped pass, batted down pass, interception/fumble)?
            // at this point we will have all the slot based bonuses and events.
            // need a way to look at all the events and action on them in order of operations
            // probably split into two streams here, run or pass.




            // PASS logic
            // look for sack, tipped pass, batted down pass, interception
            // action on those

            // if none, calculate if pass is completed.
            // rank all potential receivers based on bonuses
            // decipher coverage on top receivers to determine who is the best eligible receiver.
            // tabulate net yardage
            // look for fumbles
            // move to live ball phase

            //STEP 0: check if pass or run then split off
            if (game_data.current_offensive_player.SelectedPlay == PlayType.Run)
                playResolution = ResolveRunOutcome();
            else
                playResolution = ResolvePassOutcome();


            // check if play is over..

            if (!playResolution.BallIsLive)
            {
                // go to end play 
                ResolvePlay(playResolution);
            }

            // if yardage gained at this point gives you a safety or a touchdown, play is also over
            if (game_data.raw_ball_on + playResolution.YardageGained >= 100 || game_data.raw_ball_on + playResolution.YardageGained <= 0)
            {
                playResolution.BallIsLive = false;
                ResolvePlay(playResolution);
            }


            // Step 8: Move to Live Ball Phase
            StartLiveBallPhase();

        }

        private void HandleTurnoverOrScore()
        {
            Debug.Log("Handling turnover or score...");
            // TODO: Implement logic for turnovers or scoring (e.g., changing possession, updating score)
        }

        private void ResolveLiveBallEffects()
        {
            Debug.Log("Resolving live ball effects...");
            // TODO: Implement logic for handling loose ball effects (e.g., fumbles, deflections)
        }


        protected virtual void UpdateOngoingKills()
        {
            //Kill stuff with 0 hp
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int i = player.cards_board.Count - 1; i >= 0; i--)
                {
                    if (i < player.cards_board.Count)
                    {
                        Card card = player.cards_board[i];
                        if (card.GetHP() <= 0)
                            DiscardCard(card);
                    }
                }
                for (int i = player.cards_equip.Count - 1; i >= 0; i--)
                {
                    if (i < player.cards_equip.Count)
                    {
                        Card card = player.cards_equip[i];
                        if (card.GetHP() <= 0)
                            DiscardCard(card);
                        Card bearer = player.GetBearerCard(card);
                        if (bearer == null)
                            DiscardCard(card);
                    }
                }
            }

            //Clear cards
            for (int c = 0; c < cards_to_clear.Count; c++)
                cards_to_clear[c].Clear();
            cards_to_clear.Clear();
        }

        protected virtual void UpdateOngoingAbilities(Player player, Card card)
        {
            if (card == null || !card.CanDoAbilities())
                return;

            List<AbilityData> cabilities = card.GetAbilities();
            for (int a = 0; a < cabilities.Count; a++)
            {
                AbilityData ability = cabilities[a];
                if (ability != null && ability.trigger == AbilityTrigger.Ongoing && ability.AreTriggerConditionsMet(game_data, card))
                {
                    if (ability.target == AbilityTarget.Self)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, card))
                        {
                            ability.DoOngoingEffects(this, card, card);
                        }
                    }

                    if (ability.target == AbilityTarget.PlayerSelf)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, player))
                        {
                            ability.DoOngoingEffects(this, card, player);
                        }
                    }

                    if (ability.target == AbilityTarget.AllPlayers || ability.target == AbilityTarget.PlayerOpponent)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            if (ability.target == AbilityTarget.AllPlayers || tp != player.player_id)
                            {
                                Player oplayer = game_data.players[tp];
                                if (ability.AreTargetConditionsMet(game_data, card, oplayer))
                                {
                                    ability.DoOngoingEffects(this, card, oplayer);
                                }
                            }
                        }
                    }

                    if (ability.target == AbilityTarget.EquippedCard)
                    {
                        if (card.CardData.IsEquipment())
                        {
                            //Get bearer of the equipment
                            Card target = player.GetBearerCard(card);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                        else if (card.equipped_uid != null)
                        {
                            //Get equipped card
                            Card target = game_data.GetCard(card.equipped_uid);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                    }

                    if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand || ability.target == AbilityTarget.AllCardsBoard)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            //Looping on all cards is very slow, since there are no ongoing effects that works out of board/hand we loop on those only
                            Player tplayer = game_data.players[tp];

                            //Hand Cards
                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand)
                            {
                                for (int tc = 0; tc < tplayer.cards_hand.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_hand[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            //Board Cards
                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsBoard)
                            {
                                for (int tc = 0; tc < tplayer.cards_board.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_board[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            //Equip Cards
                            if (ability.target == AbilityTarget.AllCardsAllPiles)
                            {
                                for (int tc = 0; tc < tplayer.cards_equip.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_equip[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected virtual void AddOngoingStatusBonus(Card card, CardStatus status)
        {
            if (status.type == StatusType.AddAttack)
                card.attack_ongoing += status.value;
            if (status.type == StatusType.AddHP)
                card.hp_ongoing += status.value;
            if (status.type == StatusType.AddManaCost)
                card.mana_ongoing += status.value;
        }


        public void PlayTriggeredSlotSpin()
        {
            SlotMachineResultDTO finalResults = slotMachineManager.CalculateSpinResults();
            slotMachineUI.FireReelUI(finalResults.Results, finalResults.SlotDataCopy);
        }
        public virtual bool TriggerSecrets(AbilityTrigger secret_trigger, Card trigger_card)
        {
            if (trigger_card != null && trigger_card.HasStatus(StatusType.SpellImmunity))
                return false; //Spell Immunity, triggerer is the one that trigger the trap, target is the one attacked, so usually the player who played the trap, so we dont check the target

            for (int p = 0; p < game_data.players.Length; p++)
            {
                var loopPlayer = game_data.players[p];
                if (loopPlayer != game_data.current_offensive_player)
                {
                    Player other_player = game_data.players[p];
                    for (int i = other_player.cards_secret.Count - 1; i >= 0; i--)
                    {
                        Card card = other_player.cards_secret[i];
                        CardData icard = card.CardData;
                        if (icard.type == CardType.Secret && !card.exhausted)
                        {
                            Card trigger = trigger_card != null ? trigger_card : card;
                            if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, trigger))
                            {
                                resolve_queue.AddSecret(secret_trigger, card, trigger, ResolveSecret);
                                resolve_queue.SetDelay(0.5f);
                                card.exhausted = true;

                                if (onSecretTrigger != null)
                                    onSecretTrigger.Invoke(card, trigger);

                                return true; //Trigger only 1 secret per trigger
                            }
                        }
                    }
                }
            }
            return false;
        }

        protected virtual void ResolveSecret(AbilityTrigger secret_trigger, Card secret_card, Card trigger)
        {
            CardData icard = secret_card.CardData;
            Player player = game_data.GetPlayer(secret_card.player_id);
            if (icard.type == CardType.Secret)
            {
                Player tplayer = game_data.GetPlayer(trigger.player_id);
                if (!is_ai_predict)
                    tplayer.AddHistory(GameAction.SecretTriggered, secret_card, trigger);

                TriggerCardAbilityType(secret_trigger, secret_card, trigger);
                DiscardCard(secret_card);

                if (onSecretResolve != null)
                    onSecretResolve.Invoke(secret_card, trigger);
            }
        }

        //---- Resolve Selector -----

        public virtual void SelectCard(Card target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Can't target that target

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                game_data.last_target = target.uid;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }

            if (game_data.selector == SelectorType.SelectorCard)
            {
                if (!ability.IsCardSelectionValid(game_data, caster, target, card_array))
                    return; //Supports conditions and filters

                game_data.selector = SelectorType.None;
                game_data.last_target = target.uid;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectPlayer(Player target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Can't target that target

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectSlot(CardPositionSlot target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || !target.IsValid())
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Conditions not met

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectChoice(int choice)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || choice < 0)
                return;

            if (game_data.selector == SelectorType.SelectorChoice && ability.target == AbilityTarget.ChoiceSelector)
            {
                if (choice >= 0 && choice < ability.chain_abilities.Length)
                {
                    AbilityData achoice = ability.chain_abilities[choice];
                    if (achoice != null && game_data.CanSelectAbility(caster, achoice))
                    {
                        game_data.selector = SelectorType.None;
                        AfterAbilityResolved(ability, caster);
                        ResolveCardAbility(achoice, caster, caster);
                        resolve_queue.ResolveAll();
                    }
                }
            }
        }

        public virtual void SelectCost(int select_cost)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Player player = game_data.GetPlayer(game_data.selector_player_id);
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            if (player == null || caster == null || select_cost < 0)
                return;

            if (game_data.selector == SelectorType.SelectorCost)
            {
                if (select_cost >= 0 && select_cost < 10)
                {
                    game_data.selector = SelectorType.None;
                    game_data.selected_value = select_cost;
                    RefreshData();

                    TriggerSecrets(AbilityTrigger.OnPlayOther, caster);
                    TriggerCardAbilityType(AbilityTrigger.OnPlay, caster);
                    TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, caster);
                    resolve_queue.ResolveAll();
                }
            }
        }

        public virtual void CancelSelection()
        {
            if (game_data.selector != SelectorType.None)
            {
                //Return card to hand if was selecting cost
                if (game_data.selector == SelectorType.SelectorCost)
                    CancelPlayCard();

                //End selection
                game_data.selector = SelectorType.None;
                RefreshData();
            }
        }

        public void CancelPlayCard()
        {
            Card card = game_data.GetCard(game_data.selector_caster_uid);
            if (card != null)
            {
                Player player = game_data.GetPlayer(card.player_id);

                player.RemoveCardFromAllGroups(card);
                player.AddCard(player.cards_hand, card);
                card.Clear();
            }
        }

        public virtual void Mulligan(Player player, string[] cards)
        {
            if (game_data.phase == GamePhase.Mulligan && !player.ready)
            {
                int count = 0;
                List<Card> remove_list = new List<Card>();
                foreach (Card card in player.cards_hand)
                {
                    if (cards.Contains(card.uid))
                    {
                        remove_list.Add(card);
                        count++;
                    }
                }

                foreach (Card card in remove_list)
                {
                    player.RemoveCardFromAllGroups(card);
                    player.cards_discard.Add(card);
                }

                player.ready = true;
                DrawCard(player, count);
                RefreshData();

                if (game_data.AreAllPlayersReady())
                {
                    StartTurn();
                }
            }
        }

/*        //Pass play and run play outcomes
        private List<AbilityQueueElement> CheckForPassFailEvents()
        { // just return the abilities that are relevant and deal with them elsewhere. 
            return resolve_queue.GetAbilityQueue()
                .Where(a => a.ability.trigger == AbilityTrigger.OnPassResolution &&
                            a.ability.failEventType != FailPlayEventType.None)
                .OrderBy(a => a.ability.failEventType) // Sort by priority
                .ToList();
        }*/

        private PlayResolution ResolvePassOutcome()
        {

            var passCompleteViaSlotMachine = CheckPassCompletion(game_data.current_offensive_player.SelectedPlay, game_data.current_slot_data.Results.Select(r => r.Middle.IconId).ToList());

            var failEvent = ResolvePassFailEvents(passCompleteViaSlotMachine);
           
            if (failEvent != null)
            {
                return failEvent;
            }

            List<Card> availableReceivers = game_data.current_offensive_player.GetAvailableReceivers();

            ReceiverRankingSystem rankingSystem = new ReceiverRankingSystem(availableReceivers, game_data.current_offensive_player.SelectedPlay == PlayType.LongPass);

            var targetReceiver = rankingSystem.ApplyCoverage(game_data, rankingSystem);
            if (targetReceiver == null)
            {
                // not sure what we do, is it incomplete? or does it just not get any player bonuses?
            }
            var currPlayType = game_data.current_offensive_player.SelectedPlay;
            var currentPlayStatus = currPlayType == PlayType.LongPass ? StatusType.AddedDeepPassBonus : StatusType.AddedShortPassBonus;

            var statusYardage = targetReceiver.status
                .FirstOrDefault(_ => _.type == currentPlayStatus)?.value ?? 0;

            var baseYardage = currentPlayStatus == StatusType.AddedDeepPassBonus ? targetReceiver.Data.deep_pass_bonus : targetReceiver.Data.short_pass_bonus;

            var coachYardage = game_data.current_offensive_player.head_coach.baseOffenseYardage[game_data.current_offensive_player.SelectedPlay];
            var otherPositionGroupsToCount = new PlayerPositionGrp[2]
            {
                PlayerPositionGrp.QB,
                PlayerPositionGrp.OL
            };


            var otherOffPlayerYardage = game_data.current_offensive_player.cards_board
                .Where(c => otherPositionGroupsToCount.Contains(c.Data.playerPosition))
                .Sum(c => (currPlayType == PlayType.LongPass ? c.Data.deep_pass_bonus : c.Data.short_pass_bonus) +
                    c.GetStatusValue(currentPlayStatus));

            var otherDefPlayerYardage = game_data.GetCurrentDefensivePlayer().cards_board
                .Sum(c => (currPlayType == PlayType.LongPass ? c.Data.deep_pass_coverage_bonus : c.Data.short_pass_coverage_bonus) +
                    c.GetStatusValue(currentPlayStatus));

            return new PlayResolution
            {
                BallIsLive = true,
                Turnover = false,
                YardageGained = baseYardage + statusYardage + otherOffPlayerYardage + coachYardage,
                ContributingAbilities = new List<AbilityQueueElement>()
            };

        }
        private bool CheckPassCompletion(PlayType playType, List<SlotMachineIconType> slotResults)
        {

            var requirements = game_data.current_offensive_player.head_coach.completionRequirements[playType];
            if (requirements == null) return false;

            // Count how many of each icon type appeared in the slots
            var slotCounts = new Dictionary<SlotMachineIconType, int>();
            foreach (var result in slotResults)
            {
                if (slotCounts.ContainsKey(result))
                    slotCounts[result]++;
                else
                    slotCounts[result] = 1;
            }
            var isComplete = false;
            // Check if slot counts meet the minimum requirements
            foreach (var requirement in requirements)
            {
                if (slotCounts.ContainsKey(requirement.icon) && slotCounts[requirement.icon] >= requirement.minCount)
                    isComplete = true;
            }

            return isComplete;
        }
        // basically returning a resolution that could have multiple abilities to playback in order
        private PlayResolution ResolvePassFailEvents(bool passCompleteViaSlotMachine)
        { 
            if (!passCompleteViaSlotMachine)
            {
                var basicIncompletePass = new AbilityData
                {
                    trigger = AbilityTrigger.OnPassResolution,
                    failEventType = FailPlayEventType.IncompletePass
                };
                
                resolve_queue.AddAbility(basicIncompletePass, null, null, null);
            }

            var failList = resolve_queue.GetAbilityQueue()
                .Where(a => a.ability.trigger == AbilityTrigger.OnPassResolution &&
                            a.ability.failEventType != FailPlayEventType.None)
                .OrderBy(a => a.ability.failEventType) // Priority-based order
                .ToList();

            foreach (var failEvent in failList)
            {
                switch (failEvent.ability.failEventType)
                {
                    case FailPlayEventType.Sack:
                        return HandleSack(failEvent, failList.ToList());

                    case FailPlayEventType.Interception:
                        return HandleInterception(failEvent);

                    case FailPlayEventType.BattedPass:
                        return HandleBattedPass(failEvent);

                    case FailPlayEventType.TippedPass:
                        return TryToInterceptTippedPass(failEvent);

                    case FailPlayEventType.IncompletePass:
                        return HandleIncompletePass(failEvent);

                    case FailPlayEventType.RunnerFumble:
                        if (passCompleteViaSlotMachine)
                            return HandleFumble(failEvent);
                        else break;

                    default:
                        break;
                }
            }
            return null; // No fail events resolved → proceed to completion check
        }

        private PlayResolution ResolveRunFailEvents()
        {

            var failList = resolve_queue.GetAbilityQueue()
                .Where(a => a.ability.trigger == AbilityTrigger.OnPassResolution &&
                            a.ability.failEventType != FailPlayEventType.None)
                .OrderBy(a => a.ability.failEventType) // Priority-based order
                .ToList();

            foreach (var failEvent in failList)
            {
                switch (failEvent.ability.failEventType)
                {
                    case FailPlayEventType.RunnerFumble:
                        return HandleFumble(failEvent);
                    case FailPlayEventType.TackleForLoss:
                        return HandleTackleForLoss(failEvent);
                    default:
                        break;
                }
            }
            return null;
        }

        private PlayResolution HandleTackleForLoss(AbilityQueueElement failEvent)
        {
            var casterGrit = failEvent.caster.Data.grit;
            return new PlayResolution
            {
                BallIsLive =false,
                YardageGained = failEvent.caster.Data.grit < 3 ? -3 : -failEvent.caster.Data.grit,
                ContributingAbilities = new List<AbilityQueueElement>
                {
                    failEvent
                },
                Turnover = false

            };
            // lose yardage, end of play
        }

        private PlayResolution HandleSack(AbilityQueueElement failEvent, List<AbilityQueueElement> otherEvents)
        {
            var fumble = otherEvents.FirstOrDefault(e => e.ability.failEventType == FailPlayEventType.QBFumble);

            if (fumble != null) 
            {
                var playFailRes = HandleQbFumble(fumble);
                playFailRes.ContributingAbilities.Prepend(failEvent);
                return playFailRes;
            }
            else
            {
                return new PlayResolution
                {
                    BallIsLive = false,
                    ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                    Turnover = false,
                    YardageGained = game_data.current_offensive_player.SelectedPlay == PlayType.LongPass ? -8 : -4
                };
            }
            // remove yardage. 
            // check if theres a qb fumble somehow?
            // if not end the play


        }

        private PlayResolution HandleQbFumble(AbilityQueueElement fumble)
        {
            var offGrit = game_data.current_offensive_player.GetCurrentBoardCardGrit();
            var defGrit = game_data.GetCurrentDefensivePlayer().GetCurrentBoardCardGrit();

            var turnover = defGrit > offGrit; //tie goes to offense???!? TODO:

            var diff = (int)Math.Abs(defGrit - offGrit);


            return new PlayResolution
            {
                BallIsLive = true,
                ContributingAbilities = new List<AbilityQueueElement> { fumble },
                Turnover = turnover,
                YardageGained = turnover ? (2 * diff) : (1 * diff),
            };
        }

        private PlayResolution HandleInterception(AbilityQueueElement failEvent)
        {
            var offGrit = game_data.current_offensive_player.GetCurrentBoardCardGrit();
            var defGrit = game_data.GetCurrentDefensivePlayer().GetCurrentBoardCardGrit();

            var playType = game_data.current_offensive_player.SelectedPlay;


            var netYardageInterceptionPoint =
                game_data.current_offensive_player.GetBoardCardsBaseYardageForPlayType(playType, true) -
                game_data.GetCurrentDefensivePlayer().GetBoardCardsBaseYardageForPlayType(playType, false);

            var hasDefSurplus = defGrit > offGrit;

            return new PlayResolution
            {
                BallIsLive = true,
                Turnover = true,
                ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                YardageGained = hasDefSurplus ? netYardageInterceptionPoint - (defGrit - offGrit) : netYardageInterceptionPoint
            };
        }


        private PlayResolution TryToInterceptTippedPass(AbilityQueueElement failEvent)
        {
            var offGrit = game_data.current_offensive_player.GetCurrentBoardCardGrit();
            var defGrit = game_data.GetCurrentDefensivePlayer().GetCurrentBoardCardGrit();

            if (defGrit + 3 >= offGrit && defGrit >= offGrit * 2) // interception!
            {
                return new PlayResolution
                {
                    BallIsLive = true,
                    Turnover = true,
                    ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                    YardageGained = (defGrit - offGrit) * 2
                };
            } else // incomplete!
            {
                return new PlayResolution
                {
                    BallIsLive = false,
                    Turnover = game_data.current_down == 4 ? true : false,
                    ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                    YardageGained = 0
                };
            }
        }

        private PlayResolution HandleBattedPass(AbilityQueueElement failEvent)
        {
            return new PlayResolution
            {
                BallIsLive = false,
                Turnover = game_data.current_down == 4 ? true : false,
                ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                YardageGained = 0
            };
        }

        private PlayResolution HandleFumble(AbilityQueueElement failEvent)
        {
            var offGrit = game_data.current_offensive_player.GetCurrentBoardCardGrit();
            var defGrit = game_data.GetCurrentDefensivePlayer().GetCurrentBoardCardGrit();

            var turnover = defGrit > offGrit; //tie goes to offense???!? TODO:

            var diff = (int)Math.Abs(defGrit - offGrit);


            return new PlayResolution
            {
                BallIsLive = true,
                ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                Turnover = turnover,
                YardageGained = turnover ? (2 * diff) : (1 * diff),
            };
        }
        private PlayResolution HandleIncompletePass(AbilityQueueElement failEvent)
        {
            return new PlayResolution
            {
                BallIsLive = false,
                Turnover = game_data.current_down == 4 ? true : false,
                ContributingAbilities = new List<AbilityQueueElement>(),
                YardageGained = 0
            };
        }

        private void ResolvePlay(PlayResolution playRes)
        {
            
            //ADD current play to play history
            // play history ideas (anything that could be used by an ability!)
            // slot
            // playcalls
            // cards in play
            // hand sizes at end of play?
            // yardage
            // ball on at start of play
            // ball on at end of play
            //turnover?
            // player stats (reception, yardage)
            // plays left
            //score
            //pass completion
            // cards drawn / discarded
            // abilities used



            game_data.plays_left_in_half--;
            // check if there are no plays left in the half.

            if (game_data.plays_left_in_half < 1)
            {
                if (game_data.current_half == 1)
                {
                    //go to second half
                }
                else
                {
                    //end game
                }
            }
            if (game_data.current_down == 4)
            {
                game_data.current_offensive_player = GameData.GetCurrentDefensivePlayer();
                game_data.current_down = 1;
                //start turn?
            }
            else
            {
                game_data.current_down++;
                game_data.raw_ball_on =
                    ((game_data.fieldDirection == FieldDirection.Player0GoesUp && game_data.current_offensive_player.player_id == 0) ||
                    (game_data.fieldDirection == FieldDirection.Player0GoesDown && game_data.current_offensive_player.player_id == 1)) ?
                    game_data.raw_ball_on + game_data.yardage_this_play :
                    game_data.raw_ball_on - game_data.yardage_this_play;   //TODO: this might be incredibly wrong.

            }

            //Clear stuff?
        }


        private PlayResolution ResolveRunOutcome()
        {
            var failEvent = ResolveRunFailEvents();
            if (failEvent != null)
            {
                return failEvent;
            }


            int baseYardage = game_data.current_offensive_player.head_coach.baseOffenseYardage[PlayType.Run];

            int playerRunBase = game_data.current_offensive_player.cards_board.Sum(p => p.Data.run_bonus);
            int playerAddedBonuses = game_data.current_offensive_player.cards_board
                .Sum(c => c.GetStatusValue(StatusType.AddedRunBonus));

            var defPlayerCoverageBase = game_data.GetCurrentDefensivePlayer().cards_board.Sum(p => p.Data.run_coverage_bonus);
            var defAddedBonuses = game_data.GetCurrentDefensivePlayer().cards_board
                .Sum(c => c.GetStatusValue(StatusType.AddedRunCoverageBonus));


            return new PlayResolution
            {
                BallIsLive = true,
                ContributingAbilities = new List<AbilityQueueElement>(),
                Turnover = false,
                YardageGained = (baseYardage + playerAddedBonuses) - (defPlayerCoverageBase + defAddedBonuses),
            };
            
               
        }


        //-----Trigger Selector-----

        protected virtual void GoToSelectTarget(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectTarget;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        protected virtual void GoToSelectorCard(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectorCard;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        protected virtual void GoToSelectorChoice(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectorChoice;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        protected virtual void GoToSelectorCost(Card caster)
        {
            game_data.selector = SelectorType.SelectorCost;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = "";
            game_data.selector_caster_uid = caster.uid;
            game_data.selected_value = 0;
            RefreshData();
        }

        protected virtual void GoToMulligan()
        {
            game_data.phase = GamePhase.Mulligan;
            game_data.turn_timer = GameplayData.Get().turn_duration;
            foreach (Player player in game_data.players)
                player.ready = false;
            RefreshData();
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
using Assets.TcgEngine.Scripts.Effects;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    public partial class GameLogicService
    {
        //----- Turn Phases ----------

        public virtual void StartGame()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data != null && game_data.players.Count() >= 2)
            {
                fieldSlotManager.GenerateSlotsForPlayer(game_data.players[0]);
                fieldSlotManager.GenerateSlotsForPlayer(game_data.players[1]);
            }
            //Choose first player
            game_data.state = GameState.Play;
            game_data.current_offensive_player = game_data.players[0];
            game_data.first_half_offense_player_id = game_data.players[0].player_id;

            game_data.turn_count = 1;

            //Init each players
            foreach (Player player in game_data.players)
            {
                //Draw starting cards
                int dcards = GameplayData.Get().cards_start;
                DrawCard(player, dcards);
            }

            //Start state
            RefreshData();
            onGameStart?.Invoke();

            if (false)
                GoToMulligan();
            else
                StartPreGameSpin();
        }

        public virtual void StartTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            ClearTurnData();
            game_data.phase = GamePhase.StartTurn;
            onTurnStart?.Invoke();

            // Reset readiness and play selection at the start of each play
            foreach (Player player in game_data.players)
            {
                player.ResetReadyState();
                player.SelectedPlay = PlayType.Huddle;
                player.PlayEnhancer = null;
                player.LiveBallCard = null;
                player.suits_played_this_turn.Clear();
                player.coachManager?.ResetTurnState();
            }

            // ALSO reset the shared readiness dictionary used by the server
            game_data.ResetPhaseReady();

            foreach (Player player in game_data.players)
            {
                //Cards draw
                if (game_data.turn_count > 1 || player != game_data.current_offensive_player)
                {
                    DrawCard(player, GameplayData.Get().cards_per_turn);
                }

                //Refresh Cards and Status Effects
                for (int i = player.cards_board.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_board[i];

                    if (!card.HasStatus(StatusType.Sleep))
                        card.Refresh();
                }

                //Turn timer and history
                game_data.turn_timer = GameplayData.Get().turn_duration;

                //StartTurn Abilities
                TriggerPlayerCardsAbilityType(player, AbilityTrigger.StartOfTurn);
            }

            // Down-specific coach triggers
            if (game_data.current_down == 3)
                game_data.current_offensive_player.coachManager?.OnCoachTrigger(CoachTrigger.On3rdDown);
            else if (game_data.current_down == 4)
                game_data.current_offensive_player.coachManager?.OnCoachTrigger(CoachTrigger.On4thDown);

            StartChoosePlayersPhase();
        }

        private void StartChoosePlayersPhase()
        {
            game_data.phase = GamePhase.ChoosePlayers;
            RefreshData();
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

            game_data.turn_count++;
            CheckForWinner();
            EndPlayPhase();
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
        }

        private void WaitForPlayCallSelection()
        {
            if (game_data.AreAllPlayersPhaseReady())
            {
                Debug.Log("All players ready for ChoosePlay, transitioning to RevealPlayCalls");
                RevealPlayCalls();
                resolve_queue.AddCallback(StartSlotSpinPhase);
                resolve_queue.ResolveAll(FormationTransitionDelay);
            }
            else
            {
                bool bothSelectedPlay = false;
                if (game_data.players != null && game_data.players.Length >= 2)
                {
                    bool p0Selected = game_data.players[0]?.SelectedPlay != PlayType.Huddle;
                    bool p1Selected = game_data.players[1]?.SelectedPlay != PlayType.Huddle;
                    bothSelectedPlay = p0Selected && p1Selected;
                }

                if (bothSelectedPlay)
                {
                    Debug.Log("Both players selected plays, progressing from ChoosePlay");
                    RevealPlayCalls();
                    resolve_queue.AddCallback(StartSlotSpinPhase);
                    resolve_queue.ResolveAll(FormationTransitionDelay);
                }
                else
                {
                    Debug.Log("Waiting for play selections. P0: " + game_data.players[0]?.SelectedPlay + ", P1: " + game_data.players[1]?.SelectedPlay);
                    resolve_queue.AddCallback(WaitForPlayCallSelection);
                    resolve_queue.ResolveAll(0.5f);
                }
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

            game_data.current_slot_data = SpinSlotsWithModifiers();

            while (game_data.pending_respins > 0)
            {
                game_data.pending_respins--;
                Debug.Log($"[Respin] Using respin. {game_data.pending_respins} remaining.");
                game_data.current_slot_data = SpinSlotsWithModifiers();
            }

            slotMachineUI.FireReelUI(game_data.current_slot_data.Results, slotMachineManager.slot_data);

            resolve_queue.AddCallback(ResolvePlayOutcome);
            resolve_queue.ResolveAll(SlotSpinDelay);
        }

        /// <summary>
        /// Decorative pre-game spin — populates reels before turn 1, does not affect play outcome.
        /// </summary>
        public virtual void StartPreGameSpin()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.SlotSpin;
            onSlotSpinStart?.Invoke();
            RefreshData();

            game_data.current_slot_data = SpinSlotsWithModifiers(recordHistory: false);
            slotMachineUI.FireReelUI(game_data.current_slot_data.Results, slotMachineManager.slot_data);

            resolve_queue.AddCallback(StartTurn);
            resolve_queue.ResolveAll(SlotSpinDelay);
        }

        private SlotMachineResultDTO SpinSlotsWithModifiers(bool recordHistory = true)
        {
            List<SlotModifier> modifiers = game_data.temp_slot_modifiers;

            var results = slotMachineManager.CalculateSpinResults(modifiers);

            game_data.current_slot_data = results;

            if (recordHistory)
            {
                if (game_data.slot_history == null)
                    game_data.slot_history = new List<SlotHistory>();
                game_data.slot_history.Add(new SlotHistory
                {
                    slots = new List<SlotMachineResultDTO> { results },
                    turnId = game_data.turn_count,
                    offensivePlayerId = game_data.current_offensive_player?.player_id ?? -1
                });
            }

            if (modifiers != null && modifiers.Count > 0)
            {
                for (int i = modifiers.Count - 1; i >= 0; i--)
                {
                    // duration >= 0: includes duration=0 ("this play") which was previously never cleaned up
                    if (!modifiers[i].isPermanent && modifiers[i].duration >= 0)
                    {
                        modifiers[i].duration--;
                        if (modifiers[i].duration <= 0)
                        {
                            modifiers.RemoveAt(i);
                        }
                    }
                }
            }

            return results;
        }

        // ── Animation timing constants ──────────────────────
        // Huddle→formation at jog (~4.5 yd/s), max ~15yd = ~3.3s + 0.5s dramatic beat
        private const float FormationTransitionDelay = 4.0f;
        // ~2s spin + 1s results display + 0.5s beat
        private const float SlotSpinDelay = 3.5f;
        // settle(0.5) + minimize(0.7) + routes(~2s) + beats(0.6) + yardage(~1s) + pursuit(~1.5s)
        private const float RouteAnimationDelay = 7.0f;

        public virtual void StartLiveBallPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            // Set Resolution phase so client fires route animations (snap → routes + yardage movement)
            game_data.phase = GamePhase.Resolution;
            RefreshData();

            // After routes finish, transition to LiveBall
            resolve_queue.AddCallback(StartLiveBallPhaseInternal);
            resolve_queue.ResolveAll(RouteAnimationDelay);
        }

        private void StartLiveBallPhaseInternal()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            game_data.phase = GamePhase.LiveBall;
            RefreshData();
        }

        public virtual void EndPlayPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.plays_left_in_half--;

            PlayResult playResult = game_data.turnover_pending ? PlayResult.Turnover : PlayResult.None;
            game_data.RecordPlayResult(playResult, game_data.yardage_this_play);

            // Update per-card counters and history
            PlayType offPlay = game_data.current_offensive_player.SelectedPlay;
            bool wasTD = game_data.IsTouchdown();
            foreach (Player aplayer in game_data.players)
            {
                bool isOff = aplayer.player_id == game_data.current_offensive_player.player_id;
                foreach (Card card in aplayer.cards_board)
                {
                    card.IncrementCounter(Card.CTR_PLAYS_ON_FIELD);
                    if (isOff && offPlay == PlayType.Run)      card.IncrementCounter(Card.CTR_RUNS_ON_FIELD);
                    if (isOff && offPlay != PlayType.Run)      card.IncrementCounter(Card.CTR_PASSES_ON_FIELD);
                    if (!isOff)                                card.IncrementCounter(Card.CTR_PLAYS_COVERED);
                    if (!isOff && game_data.last_coverage_guess_correct) card.IncrementCounter(Card.CTR_CORRECT_GUESSES);
                    if (wasTD)                                 card.IncrementCounter(Card.CTR_TOUCHDOWNS_ON_FIELD);
                    if (playResult == PlayResult.Turnover)     card.IncrementCounter(Card.CTR_TURNOVERS_ON_FIELD);

                    var history = new CardHistory(game_data, card);
                    history.YardsGained = game_data.yardage_this_play;
                    history.PlayResult = playResult;
                    card.on_field_history.Add(history);
                    if (card.on_field_history.Count > 30)
                        card.on_field_history.RemoveAt(0);
                }
            }

            // Drain stamina from every player card on the board.
            foreach (Player aplayer in game_data.players)
            {
                for (int i = aplayer.cards_board.Count - 1; i >= 0; i--)
                {
                    Card card = aplayer.cards_board[i];
                    if (!card.CardData.IsPlayer()) continue;
                    card.current_stamina--;
                    if (card.current_stamina <= 0)
                    {
                        Debug.Log($"[Stamina] {card.card_id} knocked out.");
                        TriggerCardAbilityType(AbilityTrigger.OnKnockout, card);
                        DiscardCard(card);
                    }
                }
            }

            if (game_data.turnover_pending)
            {
                game_data.turnover_pending = false;
                Debug.Log("[EndPlayPhase] Turnover flag set — switching possession.");
                foreach (var p in game_data.players)
                    p.coachManager?.OnCoachTrigger(CoachTrigger.OnTurnover);

                // B4 fix: apply live-ball fumble return yards when determining new drive start
                int liveBallReturn = game_data.live_ball_return_yards;
                game_data.live_ball_return_yards = 0;

                SwitchPossession();
                if (game_data.plays_left_in_half <= 0)
                    HandleHalfOrGameEnd();
                else if (liveBallReturn > 0)
                {
                    // C2 fix: both teams drive 0→100; fumble spot from old offense = 100-spot for new offense
                    int fumbleSpot = game_data.raw_ball_on + game_data.yardage_this_play;
                    int newStart = 100 - fumbleSpot + liveBallReturn;
                    ResetDrive(ballOn: Mathf.Clamp(newStart, 1, 99));
                }
                else
                    ResetDrive();
                return;
            }

            game_data.current_down++;
            game_data.raw_ball_on += game_data.yardage_this_play;

            if (game_data.raw_ball_on <= 0)
                HandleSafety();
            else if (game_data.current_down > 4 || game_data.raw_ball_on >= 100)
                HandleTurnoverOrScore();
            else if (game_data.plays_left_in_half <= 0)
                HandleHalfOrGameEnd();
            else
                StartTurn();
        }

        private void WaitForLiveBallSelection()
        {
            if (game_data.AreAllPlayersPhaseReady())
            {
                ResolveLiveBallEffects();
                resolve_queue.AddCallback(EndPlayPhase);
                resolve_queue.ResolveAll();
            }
        }

        private bool AllPlayersReadyForPhase(GamePhase phase)
        {
            return game_data.AreAllPlayersPhaseReady();
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

            switch (game_data.phase)
            {
                case GamePhase.ChoosePlayers:
                    RevealNewPlayers();
                    resolve_queue.AddCallback(StartPlayCallPhase);
                    resolve_queue.ResolveAll();
                    break;

                case GamePhase.ChoosePlay:
                    foreach (Player p in game_data.players)
                    {
                        if (p.SelectedPlay == PlayType.Huddle)
                            p.SelectedPlay = PlayType.Run;
                    }
                    RevealPlayCalls();
                    resolve_queue.AddCallback(StartSlotSpinPhase);
                    resolve_queue.ResolveAll();
                    break;

                case GamePhase.LiveBall:
                    ResolveLiveBallEffects();
                    resolve_queue.ResolveAll();
                    break;

                case GamePhase.SlotSpin:
                    resolve_queue.AddCallback(ResolvePlayOutcome);
                    resolve_queue.ResolveAll();
                    break;

                default:
                    resolve_queue.AddCallback(EndTurn);
                    resolve_queue.ResolveAll();
                    break;
            }
        }

        // No-op: First & Long has no HP win condition. Real end-of-game logic is in HandleHalfOrGameEnd().
        protected virtual void CheckForWinner() { }

        protected virtual void ClearTurnData()
        {
            game_data.selector = SelectorType.None;
            game_data.live_ball_grit_bonus = 0;
            game_data.live_ball_return_yards = 0;
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
                    if (card.playerPosition == PlayerPositionGrp.QB)
                        player.cards_sideline.Add(acard);
                    else
                        player.cards_deck.Add(acard);
                }
            }

            DeckPuzzleData puzzle = deck as DeckPuzzleData;

            if (puzzle == null || !puzzle.dont_shuffle_deck)
                ShuffleDeck(player.cards_deck);
        }

        private void WaitForPlayerSelection()
        {
            if (game_data.AreAllPlayersPhaseReady())
            {
                Debug.Log("All players ready for ChoosePlayers, transitioning to RevealPlayers");
                RevealNewPlayers();
                resolve_queue.AddCallback(StartPlayCallPhase);
                resolve_queue.ResolveAll();
            }
            else
            {
                bool bothPlacedCards = false;
                if (game_data.players != null && game_data.players.Length >= 2)
                {
                    int p0Cards = game_data.players[0]?.cards_board?.Count ?? 0;
                    int p1Cards = game_data.players[1]?.cards_board?.Count ?? 0;
                    bothPlacedCards = (p0Cards > 0 && p1Cards > 0);
                }

                if (bothPlacedCards)
                {
                    Debug.Log("Players have placed cards, progressing from ChoosePlayers");
                    RevealNewPlayers();
                    resolve_queue.AddCallback(StartPlayCallPhase);
                    resolve_queue.ResolveAll();
                }
                else
                {
                    resolve_queue.AddCallback(WaitForPlayerSelection);
                    resolve_queue.ResolveAll(0.5f);
                }
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
                        if (icard.playerPosition == PlayerPositionGrp.QB)
                            player.cards_sideline.Add(acard);
                        else
                            player.cards_deck.Add(acard);
                    }
                }
            }

            ShuffleDeck(player.cards_deck);
        }

        //----- Post-Resolution Lifecycle -----

        private void HandleTurnoverOrScore()
        {
            Debug.Log("Handling turnover or score...");

            if (game_data.raw_ball_on >= 100)
            {
                HandleTouchdown();
                return;
            }

            if (game_data.current_down > 4)
            {
                HandleTurnoverOnDowns();
                return;
            }

            Debug.LogWarning("HandleTurnoverOrScore: Unexpected state - defaulting to turnover");
            SwitchPossession();
        }

        private void HandleTouchdown()
        {
            Debug.Log("TOUCHDOWN! Scoring 7 points...");
            game_data.current_offensive_player.coachManager?.OnCoachTrigger(CoachTrigger.OnScore);

            game_data.current_offensive_player.points += 7;
            Debug.Log($"Player {game_data.current_offensive_player.player_id} scores! Total: {game_data.current_offensive_player.points}");

            SwitchPossession();

            if (game_data.plays_left_in_half <= 0)
            {
                HandleHalfOrGameEnd();
                return;
            }

            ResetDrive();
        }

        private void HandleSafety()
        {
            Debug.Log("SAFETY! Defense scores 2 points...");
            Player defense = game_data.GetCurrentDefensivePlayer();
            defense.points += 2;
            defense.coachManager?.OnCoachTrigger(CoachTrigger.OnScore);
            SwitchPossession();
            if (game_data.plays_left_in_half <= 0) { HandleHalfOrGameEnd(); return; }
            ResetDrive(ballOn: 40);
        }

        private void HandleTurnoverOnDowns()
        {
            Debug.Log("Turnover on downs! Defense holds...");
            SwitchPossession();
            if (game_data.plays_left_in_half <= 0) { HandleHalfOrGameEnd(); return; }
            ResetDrive();
        }

        private void HandleHalfOrGameEnd()
        {
            if (game_data.current_half >= 2)
            {
                Player p0 = game_data.players[0];
                Player p1 = game_data.players[1];
                if (p0.points > p1.points)      EndGame(0);
                else if (p1.points > p0.points) EndGame(1);
                else                            EndGame(-1);
            }
            else
            {
                game_data.current_half = 2;
                game_data.plays_left_in_half = 11;
                game_data.current_offensive_player = game_data.GetOpponentPlayer(game_data.first_half_offense_player_id);
                ResetDrive();
            }
        }

        /// <summary>
        /// Called by EffectForceTurnover during live ball resolution.
        /// Switches possession and starts the new offense at their 25 + return yards.
        /// </summary>
        public void HandleLiveBallTurnover(int returnYards)
        {
            // B4 fix: don't call SwitchPossession here — EndPlayPhase (via EndTurn → StartNextTurn) handles it.
            // Store state; EndPlayPhase turnover path applies return yards and resets drive.
            game_data.live_ball_return_yards = returnYards;
            game_data.turnover_pending = true;
            game_data.live_ball_grit_bonus = 0; // consumed by DoEffect; clear now
            Debug.Log($"[LiveBall] Fumble turnover — {returnYards} return yards. Possession switches in EndPlayPhase.");
        }

        private void SwitchPossession()
        {
            Player new_offense = game_data.GetOpponentPlayer(game_data.current_offensive_player.player_id);
            Debug.Log($"Switching possession: Player {game_data.current_offensive_player.player_id} → Player {new_offense.player_id}");
            game_data.current_offensive_player = new_offense;
        }

        private void ResetDrive(int ballOn = 25)
        {
            game_data.current_down = 1;
            game_data.raw_ball_on = ballOn;
            game_data.yardage_this_play = 0;
            game_data.yardage_to_go = 100 - ballOn;

            foreach (Player aplayer in game_data.players)
            {
                for (int i = aplayer.cards_board.Count - 1; i >= 0; i--)
                {
                    Card card = aplayer.cards_board[i];
                    aplayer.cards_board.RemoveAt(i);
                    aplayer.cards_sideline.Add(card);
                    Debug.Log($"[Sideline] {card.card_id} moved to sideline for player {aplayer.player_id}.");
                }
            }

            Debug.Log($"Drive reset — Down: {game_data.current_down}, Ball on: {game_data.raw_ball_on}");
            StartTurn();
        }

        private void CheckGameOver() { HandleHalfOrGameEnd(); }

        public virtual void ResolveLiveBallEffects()
        {
            Card offCard = game_data.current_offensive_player.LiveBallCard;
            Card defCard = game_data.GetCurrentDefensivePlayer().LiveBallCard;

            Debug.Log($"[LiveBall] Resolving — OFF: {offCard?.card_id ?? "pass"} | DEF: {defCard?.card_id ?? "pass"}");

            // Both passed — subtotal stands
            if (offCard == null && defCard == null)
            {
                Debug.Log("[LiveBall] Both players passed. Subtotal stands.");
                ClearLiveBallCards();
                resolve_queue.AddCallback(EndTurn);
                return;
            }

            // --- STEP 2: NEGATE CHECK ---
            bool defNegated = false;
            bool offNegated = false;

            // Defense negates offense's card (Blanket Coverage)
            if (defCard != null && HasLiveBallEffect<EffectNegateCard>(defCard))
            {
                if (offCard != null)
                {
                    Debug.Log($"[LiveBall] Blanket Coverage! {defCard.card_id} negates {offCard.card_id}");
                    offNegated = true;
                }
                else
                {
                    Debug.Log($"[LiveBall] Blanket Coverage by {defCard.card_id} but offense passed — wasted.");
                }
                // Negate card consumed its effect; skip def's other abilities too
                defNegated = true;
            }

            // Offense immune to all defense effects (In the Zone)
            if (!offNegated && offCard != null && HasLiveBallEffect<EffectImmunity>(offCard))
            {
                Debug.Log($"[LiveBall] In the Zone! {offCard.card_id} — immune to all defensive effects.");
                defNegated = true;
                // Immunity card consumed; offense still gets no other effects from this card
                offNegated = true;
            }

            // --- STEP 3: FUMBLE CHECK ---
            if (!defNegated && defCard != null)
            {
                AbilityData fumbleAbility = FindFumbleAbility(defCard);
                if (fumbleAbility != null)
                {
                    // Ball Security auto-prevents (if not negated)
                    if (!offNegated && offCard != null && HasLiveBallEffect<EffectPreventTurnover>(offCard))
                    {
                        Debug.Log($"[LiveBall] Ball Security! {offCard.card_id} auto-prevents fumble. Subtotal stands.");
                        ClearLiveBallCards();
                        resolve_queue.AddCallback(EndTurn);
                        return;
                    }

                    // Grit-based fumble resolution
                    int offGritBonus = (!offNegated && offCard != null) ? GetLiveBallGritBonus(offCard) : 0;
                    int offGrit = SumBoardGrit(game_data.current_offensive_player) + offGritBonus;
                    int defGrit = SumBoardGrit(game_data.GetCurrentDefensivePlayer());

                    if (defGrit > offGrit)
                    {
                        // B1 fix: store bonus so EffectForceTurnover.DoEffect reads correct offGrit for return yards
                        game_data.live_ball_grit_bonus = offGritBonus;
                        // FUMBLE — defense recovers, turnover
                        Debug.Log($"[LiveBall] Fumble! defGrit({defGrit}) > offGrit({offGrit}). Turnover.");
                        ResolveCardAbility(fumbleAbility, defCard, defCard);
                        ClearLiveBallCards();
                        // B4 fix: ensure EndTurn fires after EffectForceTurnover resolves; without this the queue is empty and game hangs in LiveBall
                        resolve_queue.AddCallback(EndTurn);
                        return;
                    }
                    else
                    {
                        // RECOVERY — offense keeps ball, play ends at subtotal
                        Debug.Log($"[LiveBall] Fumble recovered! offGrit({offGrit}) >= defGrit({defGrit}). Subtotal stands.");
                        ClearLiveBallCards();
                        resolve_queue.AddCallback(EndTurn);
                        return;
                    }
                }
            }

            // --- STEP 4: Defensive effects (yardage mods, silence, stamina drain) ---
            if (!defNegated && defCard != null)
            {
                foreach (var ability in defCard.GetAbilities())
                {
                    if (ability == null) continue;
                    if (ability.HasEffect<EffectForceTurnover>()) continue; // Already handled
                    if (ability.HasEffect<EffectNegateCard>()) continue;     // Already handled
                    if (!ability.AreTriggerConditionsMet(game_data, defCard)) continue;
                    ResolveCardAbility(ability, defCard, defCard);
                }
            }

            // --- STEP 5: Offensive effects (yardage mods, draw, stamina) ---
            if (!offNegated && offCard != null)
            {
                foreach (var ability in offCard.GetAbilities())
                {
                    if (ability == null) continue;
                    if (ability.HasEffect<EffectImmunity>()) continue;         // Already handled
                    if (ability.HasEffect<EffectPreventTurnover>()) continue;   // Already handled
                    if (!ability.AreTriggerConditionsMet(game_data, offCard)) continue;
                    ResolveCardAbility(ability, offCard, offCard);
                }
            }

            // --- STEP 6: Cleanup ---
            ClearLiveBallCards();
            resolve_queue.AddCallback(EndTurn);
        }

        /// <summary>
        /// Checks if any ability on a live ball card has the given effect type.
        /// </summary>
        private bool HasLiveBallEffect<T>(Card card) where T : EffectData
        {
            foreach (var ability in card.GetAbilities())
            {
                if (ability != null && ability.HasEffect<T>())
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the first fumble ability on a def card whose trigger conditions are met.
        /// </summary>
        private AbilityData FindFumbleAbility(Card defCard)
        {
            foreach (var ability in defCard.GetAbilities())
            {
                if (ability == null) continue;
                if (!ability.HasEffect<EffectForceTurnover>()) continue;
                if (!ability.AreTriggerConditionsMet(game_data, defCard)) continue;
                return ability;
            }
            return null;
        }

        /// <summary>
        /// Extracts grit bonus from an offensive live ball card (synchronous, no resolve queue).
        /// Used to factor grit boosts into fumble resolution before the comparison.
        /// </summary>
        private int GetLiveBallGritBonus(Card card)
        {
            int bonus = 0;
            foreach (var ability in card.GetAbilities())
            {
                if (ability == null) continue;
                foreach (var effect in ability.effects)
                {
                    if (effect is EffectModifyGrit gritEffect && !gritEffect.removeGrit)
                        bonus += gritEffect.value;
                }
            }
            if (bonus > 0)
                Debug.Log($"[LiveBall] Grit bonus +{bonus} from {card.card_id}");
            return bonus;
        }

        /// <summary>
        /// Sums total board grit for a player (base + status bonuses).
        /// </summary>
        private int SumBoardGrit(Player player)
        {
            int total = 0;
            foreach (Card c in player.cards_board)
            {
                total += c.Data.grit + c.GetStatusValue(StatusType.AddGrit);
            }
            return total;
        }

        private void ClearLiveBallCards()
        {
            foreach (var player in game_data.players)
            {
                if (player.LiveBallCard != null)
                {
                    DiscardCard(player.LiveBallCard);
                    player.LiveBallCard = null;
                }
            }
        }
    }
}

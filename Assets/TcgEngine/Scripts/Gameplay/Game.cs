using Assets.TcgEngine.Scripts.Effects;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    /// <summary>
    /// Type of offensive play called
    /// </summary>
    public enum PlayType
    {
        Huddle,
        Run,
        ShortPass,
        LongPass
    }

    /// <summary>
    /// Result of a play for ability triggers
    /// </summary>
    public enum PlayResult
    {
        None = 0,
        Complete = 1,      // Pass complete
        Incomplete = 2,     // Pass incomplete
        Touchdown = 3,      // Scored
        Turnover = 4,       // Turnover (INT or Fumble)
        FirstDown = 5,      // First down gained
        Sack = 6,           // QB sacked
        Fumble = 7,         // Fumble occurred
        Interception = 8    // Pass intercepted
    }

    //Contains all gameplay state data that is sync across network

    /// <summary>
    /// Snapshot of a single play's outcome and state
    /// </summary>
    [System.Serializable]
    public class PlayHistory
    {
        public int turn_number;                 // Which turn this play occurred on
        public int play_number_in_drive;        // Nth play in the current drive
        public PlayType offensive_play;         // What the offense called
        public PlayType defensive_play;         // What the defense guessed
        public PlayResult play_result;          // How the play ended
        public int yards_gained;                // Net yardage (can be negative for sacks/TFLs)
        public bool defense_guess_correct;      // Did defense guess the play correctly
        public int current_down;                // Down when play occurred
        public int current_half;                // Half when play occurred
        public int ball_position;               // Ball position after play
        public bool was_touchdown;              // Did this play result in TD
        public bool was_field_goal;             // Did this play result in FG

        // Can be extended to include:
        // public List<string> offensive_cards_played;
        // public List<string> defensive_cards_played;
        // public int defensive_yards;
        // etc.
    }

    [System.Serializable]
    public class Game
    {
        public string game_uid;
        public GameSettings settings;
        public PlayerPositionGrp[] offensive_pos_grps = new PlayerPositionGrp[4] { PlayerPositionGrp.WR, PlayerPositionGrp.QB, PlayerPositionGrp.OL, PlayerPositionGrp.RB_TE };
        public PlayerPositionGrp[] defensive_pos_grps = new PlayerPositionGrp[3] { PlayerPositionGrp.DB, PlayerPositionGrp.DL, PlayerPositionGrp.LB };

        //Game state
        public int turn_count = 0;
        public float turn_timer = 0f;
        public int current_down = 1;
        public int current_half = 1;
        public int raw_ball_on = 25;
        public FieldDirection fieldDirection;
        public int plays_left_in_half = 11;
        public int yardage_this_play;
        public int yardage_to_go;
        
        // Slot machine modifiers (from abilities)
        public List<SlotModifier> temp_slot_modifiers;

        // Play history
        public List<PlayHistory> play_history = new List<PlayHistory>();

        //Players
        public Player[] players;
        public Dictionary<int, bool> playerPhaseReady = new Dictionary<int, bool>();

        public Player current_offensive_player;

        public GameState state = GameState.Connecting;
        public GamePhase phase = GamePhase.None;

        /// <summary>
        /// Check if all players are ready for the next phase
        /// </summary>
        public bool AreAllPlayersPhaseReady()
        {
            // Must have entries for all players
            if (players == null || players.Length < 2)
                return false;
            
            // Check if both players have set their ready status to true
            return playerPhaseReady.ContainsKey(0) && playerPhaseReady.ContainsKey(1) &&
                   playerPhaseReady[0] && playerPhaseReady[1];
        }

        /// <summary>
        /// Set a player's ready status
        /// </summary>
        public void SetPlayerReady(int playerId, bool ready)
        {
            playerPhaseReady[playerId] = ready;
        }

        /// <summary>
        /// Clear all player ready states (for new phase)
        /// </summary>
        public void ClearPlayerReady()
        {
            playerPhaseReady.Clear();
        }

        //SlotMaching
        public SlotMachineResultDTO current_slot_data;
        public List<SlotHistory> slot_history;

        //Selector
        public SelectorType selector = SelectorType.None;
        public int selector_player_id = 0;
        public string selector_ability_id;
        public string selector_caster_uid;

        //Other reference values
        public string last_played;
        public string last_target;
        public string last_destroyed;
        public string last_summoned;
        public string ability_triggerer;
        public int rolled_value;
        public int selected_value;

        //clawdbot variables
        public PlayType last_play_type;
        public bool after_touchdown = false;
        public bool after_field_goal = false;
        public int first_downs_this_half = 0;
        
        // Game state tracking for abilities
        public PlayType defense_guess = PlayType.Huddle; // What defense guessed (run/pass)
        public PlayResult last_play_result = PlayResult.None; // Result of last play
        public int last_play_yardage = 0; // Yards gained on last play
        public int drive_count = 1; // Which drive number
        public int play_count_this_drive = 0; // Plays in current drive
        
        // Charge tracking (card_id -> charge_value)
        public Dictionary<string, int> charge_tracker = new Dictionary<string, int>();
        
        // Respin tracking (player_id -> uses remaining)
        public Dictionary<int, int> respin_available = new Dictionary<int, int>();
        
        // First snap tracking
        public bool first_snap_taken = false;
        
        // Coverage guess correct tracking
        public bool last_coverage_guess_correct = false;
        
        // Once-per-game ability tracking (ability_id -> uses remaining)
        public Dictionary<string, int> once_per_game_abilities = new Dictionary<string, int>();

        //Other reference arrays 
        public HashSet<string> ability_played = new HashSet<string>();
        public HashSet<string> cards_attacked = new HashSet<string>();

        // ===== Play History Helper Methods =====
        
        /// <summary>
        /// Get player by ID
        /// </summary>
        public Player GetPlayer(int playerId)
        {
            if (players == null || playerId < 0 || playerId >= players.Length)
                return null;
            return players[playerId];
        }
        
        /// <summary>
        /// Get opponent player
        /// </summary>
        public Player GetOpponentPlayer(int playerId)
        {
            int opponentId = 1 - playerId; // Assumes 2 players, 0 and 1
            return GetPlayer(opponentId);
        }
        
        /// <summary>
        /// Get the most recent play
        /// </summary>
        public PlayHistory GetLastPlay()
        {
            if (play_history == null || play_history.Count == 0)
                return null;
            return play_history[play_history.Count - 1];
        }
        
        /// <summary>
        /// Get a play by index from the end (0 = most recent)
        /// </summary>
        public PlayHistory GetPlay(int indexFromEnd)
        {
            if (play_history == null || indexFromEnd < 0 || indexFromEnd >= play_history.Count)
                return null;
            return play_history[play_history.Count - 1 - indexFromEnd];
        }
        
        /// <summary>
        /// Get total number of plays
        /// </summary>
        public int GetPlayCount()
        {
            return play_history != null ? play_history.Count : 0;
        }
        
        /// <summary>
        /// Check if a result occurred in the last N plays
        /// </summary>
        public bool WasPlayResultInLastPlays(PlayResult result, int count)
        {
            if (play_history == null || count <= 0)
                return false;
            
            int start = Mathf.Max(0, play_history.Count - count);
            for (int i = play_history.Count - 1; i >= start; i--)
            {
                if (play_history[i].play_result == result)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Count how many times a result occurred in the current half
        /// </summary>
        public int CountPlayResultThisHalf(PlayResult result)
        {
            if (play_history == null)
                return 0;
            
            int count = 0;
            foreach (var play in play_history)
            {
                if (play.current_half == current_half && play.play_result == result)
                    count++;
            }
            return count;
        }

        public Game() { }
        
        public Game(string uid, int nb_players)
        {
            this.game_uid = uid;
            players = new Player[nb_players];
            for (int i = 0; i < nb_players; i++)
                players[i] = new Player(i);
            settings = GameSettings.Default;
            InitializeTestGame();
        }
        //REMOVE THIS ONCE WE LEARN HOW TO MAKE HEAD COACHES FOR REAL
        void InitializeTestGame()
        {
            HeadCoachCard coach1 = new HeadCoachCard
            {
                positional_Scheme = new Dictionary<PlayerPositionGrp, HCPlayerSchemeData>
                {
                    { PlayerPositionGrp.QB, new HCPlayerSchemeData { pos_max = 1 } },
                    { PlayerPositionGrp.WR, new HCPlayerSchemeData { pos_max = 3 } },
                    { PlayerPositionGrp.RB_TE, new HCPlayerSchemeData { pos_max = 2 } },
                    { PlayerPositionGrp.OL, new HCPlayerSchemeData { pos_max = 5 } },
                    { PlayerPositionGrp.DL, new HCPlayerSchemeData { pos_max = 2 } },
                    { PlayerPositionGrp.LB, new HCPlayerSchemeData { pos_max = 2 } },
                    { PlayerPositionGrp.DB, new HCPlayerSchemeData { pos_max = 3 } }
                },
                baseOffenseYardage = new Dictionary<PlayType, int>
                {
                    { PlayType.ShortPass, 5 },
                    { PlayType.LongPass, 10 },
                    { PlayType.Run, 3 }
                },
                baseDefenseYardage = new Dictionary<PlayType, int>
                {
                    { PlayType.ShortPass, 3 },
                    { PlayType.LongPass, 7 },
                    { PlayType.Run, 2 }
                },
                completionRequirements = new Dictionary<PlayType, List<CompletionRequirement>>
                {
                    {
                        PlayType.ShortPass, new List<CompletionRequirement>
                        {
                            new CompletionRequirement { icon = SlotMachineIconType.Football, minCount = 1 },
                            new CompletionRequirement { icon = SlotMachineIconType.Helmet, minCount = 1 }
                        }
                    },
                    {
                        PlayType.LongPass, new List<CompletionRequirement>
                        {
                            new CompletionRequirement { icon = SlotMachineIconType.Star, minCount = 1 }
                        }
                    }
                }

            };

            HeadCoachCard coach2 = new HeadCoachCard
            {
                positional_Scheme = new Dictionary<PlayerPositionGrp, HCPlayerSchemeData>
                {
                    { PlayerPositionGrp.QB, new HCPlayerSchemeData { pos_max = 1 } },
                    { PlayerPositionGrp.WR, new HCPlayerSchemeData { pos_max = 3 } },
                    { PlayerPositionGrp.RB_TE, new HCPlayerSchemeData { pos_max = 2 } },
                    { PlayerPositionGrp.OL, new HCPlayerSchemeData { pos_max = 5 } },
                    { PlayerPositionGrp.DL, new HCPlayerSchemeData { pos_max = 2 } },
                    { PlayerPositionGrp.LB, new HCPlayerSchemeData { pos_max = 2 } },
                    { PlayerPositionGrp.DB, new HCPlayerSchemeData { pos_max = 3 } }
                },
                baseOffenseYardage = new Dictionary<PlayType, int>
                {
                    { PlayType.ShortPass, 5 },
                    { PlayType.LongPass, 10 },
                    { PlayType.Run, 3 }
                },
                baseDefenseYardage = new Dictionary<PlayType, int>
                {
                    { PlayType.ShortPass, 3 },
                    { PlayType.LongPass, 7 },
                    { PlayType.Run, 2 }
                },
                completionRequirements = new Dictionary<PlayType, List<CompletionRequirement>>
                {
                    {
                        PlayType.ShortPass, new List<CompletionRequirement>
                        {
                            new CompletionRequirement { icon = SlotMachineIconType.Football, minCount = 1 },
                            new CompletionRequirement { icon = SlotMachineIconType.Helmet, minCount = 1 }
                        }
                    },
                    {
                        PlayType.LongPass, new List<CompletionRequirement>
                        {
                            new CompletionRequirement { icon = SlotMachineIconType.Star, minCount = 1 }
                        }
                    }
                }
            };

            players[0].head_coach = coach1;
            players[1].head_coach = coach2;
        }




        public virtual bool AreAllPlayersReady()
        {
            int ready = 0;
            foreach (Player player in players)
            {
                if (player.IsReady())
                    ready++;
            }
            return ready >= settings.nb_players;
        }

        public virtual bool AreAllPlayersConnected()
        {
            int ready = 0;
            foreach (Player player in players)
            {
                if (player.IsConnected())
                    ready++;
            }
            return ready >= settings.nb_players;
        }

        //Check if its player's turn
        public virtual bool IsPlayerTurn(Player player)
        {
            return IsPlayerActionTurn(player);
        }


        public Player GetDefensePlayer()
        {
            return players.FirstOrDefault(p => p != current_offensive_player);
        }
        public void SetOffensivePlayer(Player player)
        {
            current_offensive_player = player;
        }

        public virtual bool IsPlayerActionTurn(Player player)
        {
            if (player == null)
                return false;
            
            // Only true during phases where the player can take action
            bool is_action_phase = phase == GamePhase.ChoosePlayers || 
                                   phase == GamePhase.ChoosePlay || 
                                   phase == GamePhase.LiveBall;
            
            return is_action_phase && !player.IsReadyForPhase(phase);
        }

        public virtual bool IsPlayerSelectorTurn(Player player)
        {
            return player != null && selector_player_id == player.player_id 
                && state == GameState.Play && phase == GamePhase.LiveBall && selector != SelectorType.None;
        }

        public virtual bool IsPlayerMulliganTurn(Player player)
        {
            return phase == GamePhase.Mulligan && !player.ready;
        }
        
        //Check if a card is allowed to be played on slot
        public virtual bool CanPlayCard(Card card, CardPositionSlot slot, bool skip_cost = false)
        {
            if (card == null) 
                return false;

            Player player = GetPlayer(card.player_id);
            if (player == null)
                return false;

            // Phase-based card type restrictions
            CardType cardType = card.CardData.type;
            if (card.CardData.IsPlayer() && phase != GamePhase.ChoosePlayers)
                return false;
            if (card.CardData.IsPlayEnhancer())
            {
                if (phase != GamePhase.ChoosePlay)
                    return false;
                if (player.PlayEnhancer != null)
                    return false; // Already played one enhancer this turn
            }
            if ((cardType == CardType.OffLiveBall || cardType == CardType.DefLiveBall) && phase != GamePhase.LiveBall)
                return false;

            // Check if player is trying to play a card for the wrong side of the ball
            bool is_player_offensive = (player.player_id == current_offensive_player.player_id);
            bool is_offensive_card = card.CardData.playerPosition != PlayerPositionGrp.NONE && 
                                      offensive_pos_grps.Contains(card.CardData.playerPosition);
            bool is_defensive_card = card.CardData.playerPosition != PlayerPositionGrp.NONE && 
                                      defensive_pos_grps.Contains(card.CardData.playerPosition);

            // If player is on offense, they can't play defensive cards
            if (is_player_offensive && is_defensive_card)
            {
                Debug.LogWarning($"Offensive player tried to play defensive card: {card.card_id}");
                return false;
            }

            // If player is on defense, they can't play offensive cards
            if (!is_player_offensive && is_offensive_card)
            {
                Debug.LogWarning($"Defensive player tried to play offensive card: {card.card_id}");
                return false;
            }

            if (card.CardData.playerPosition != PlayerPositionGrp.NONE // is a player card and slot is maxed out for position OR is incorrect slot type
                    && (slot.posGroupType != card.CardData.playerPosition
                    ||
                    player.cards_board
                        .Where(cd => cd.CardData.playerPosition == card.CardData.playerPosition).Count() == player.head_coach.positional_Scheme[card.CardData.playerPosition].pos_max))
            {
                    return false;
            }
            if (!player.HasCard(player.cards_hand, card))
                return false; // Card not in hand




            if (card.CardData.IsBoardCard())
            {
                if (!slot.IsValid() || IsCardOnSlot(slot))
                    return false;   //Slot already occupied
                if (CardPositionSlot.GetP(card.player_id) != slot.p)
                    return false; //Cant play on opponent side
                return true;
            }

            if (card.CardData.IsEquipment())
            {
                //TODO: removed to get it to work
/*                if (!slot.IsValid())
                    return false;

                List<Card> targets = GetSlotCards(slot);
                if (target == null || target.CardData.type != CardType.Character || target.player_id != card.player_id)
                    return false; //Target must be an allied character

                return true;*/
            }
            if (card.CardData.IsRequireTargetSpell())
            {
                return IsPlayTargetValid(card, slot); //Check play target on slot
            }
            if (card.CardData.type == CardType.DefensivePlayer)
            {
                return CanAnyPlayAbilityTrigger(card); //Check if spell will have abilities
            }
            return true;
        }

        //Check if a card is allowed to move to slot
        public virtual bool CanMoveCard(Card card, CardPositionSlot slot, bool skip_cost = false)
        {
            if (card == null || !slot.IsValid())
                return false;

            if (!IsOnBoard(card))
                return false; //Only cards in play can move

            if (!card.CanMove(skip_cost))
                return false; //Card cant move

            if (CardPositionSlot.GetP(card.player_id) != slot.p)
                return false; //Card played wrong side

            if (card.slot == slot)
                return false; //Cant move to same slot

            List<Card> slot_cards = GetSlotCards(slot);
            if (slot_cards.Count > 0) //TODO: this is where i change the slot position limits. check the head coach card.
                return false; //Already a card there

            return true;
        }

        //Check if a card is allowed to attack a player
        public virtual bool CanAttackTarget(Card attacker, Player target, bool skip_cost = false)
        {
            if(attacker == null || target == null)
                return false;

            if (!attacker.CanAttack(skip_cost))
                return false; //Card cant attack

            if (attacker.player_id == target.player_id)
                return false; //Cant attack same player

            if (!IsOnBoard(attacker) || !attacker.CardData.IsPlayer())
                return false; //Cards not on board

            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; //Protected by taunt

            return true;
        }

        //Check if a card is allowed to attack another one
        public virtual bool CanAttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return false;

            if (!attacker.CanAttack(skip_cost))
                return false; //Card cant attack

            if (attacker.player_id == target.player_id)
                return false; //Cant attack same player

            if (!IsOnBoard(attacker) || !IsOnBoard(target))
                return false; //Cards not on board

            if (!attacker.CardData.IsPlayer() || !target.CardData.IsBoardCard())
                return false; //Only character can attack

            if (target.HasStatus(StatusType.Stealth))
                return false; //Stealth cant be attacked

            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; //Protected by adjacent card

            return true;
        }

        public virtual bool CanCastAbility(Card card, AbilityData ability)
        {
            if (ability == null || card == null || !card.CanDoActivatedAbilities())
                return false; //This card cant cast

            if (ability.trigger != AbilityTrigger.Activate)
                return false; //Not an activated ability

            Player player = GetPlayer(card.player_id);
            if (!player.CanPayAbility(card, ability))
                return false; //Cant pay for ability

            if (!ability.AreTriggerConditionsMet(this, card))
                return false; //Conditions not met

            return true;
        }

        //For choice selector
        public virtual bool CanSelectAbility(Card card, AbilityData ability)
        {
            if (ability == null || card == null || !card.CanDoAbilities())
                return false; //This card cant cast

            Player player = GetPlayer(card.player_id);
            if (!player.CanPayAbility(card, ability))
                return false; //Cant pay for ability

            if (!ability.AreTriggerConditionsMet(this, card))
                return false; //Conditions not met

            return true;
        }

        public virtual bool CanAnyPlayAbilityTrigger(Card card)
        {
            if (card == null)
                return false;
            if (card.CardData.IsDynamicManaCost())
                return true; //Cost not decided so condition could be false

            foreach (AbilityData ability in card.GetAbilities())
            {
                if (ability.trigger == AbilityTrigger.OnPlay && ability.AreTriggerConditionsMet(this, card))
                    return true;
            }
            return false;
        }

        //Check if Player play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Player target)
        {
            if (caster == null || target == null)
                return false;

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        //Check if Card play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Card target)
        {
            if (caster == null || target == null)
                return false;

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        //Check if Slot play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, CardPositionSlot target)
        {
            if (caster == null)
                return false;

            List<Card> slot_cards = GetSlotCards(target);
            if (slot_cards.Count > 0) //TODO: Just taking the first card to check, to get multi card slots to work.
                return IsPlayTargetValid(caster, slot_cards[0]); //Slot has card, check play target on that card

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }



        public Player GetCurrentDefensivePlayer()
        {
            return players[0] == current_offensive_player ? players[0] : players[1];
        }


        public Card GetCard(string card_uid)
        {
            foreach (Player player in players)
            {
                Card acard = player.GetCard(card_uid);
                if (acard != null)
                    return acard;
            }
            return null;
        }

        public Card GetBoardCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetEquipCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_equip)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetHandCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_hand)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetDeckCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_deck)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetDiscardCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_discard)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetSecretCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_secret)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetTempCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_temp)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public List<Card> GetSlotCards(CardPositionSlot slot, int playerId)
        {
            var result = new List<Card>();

            foreach (Card card in players.First(p => p.player_id == playerId).cards_board)
            {
                if (card != null && card.slot == slot)
                    result.Add(card);
            }
            return result;
        }
        public List<Card> GetSlotCards(CardPositionSlot slot)
        {
            List<Card> result = new List<Card>();

            foreach (Player player in players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (card != null && card.slot == slot)
                        result.Add(card);
                }
            }
            return result;
        }


        /*        public bool IsSlotPositionIsMaxed(CardPositionSlot slot)
                {

                    var cards_in_slot = GetSlotCards(slot);
                    slot.
                    var posGroup = cards_in_slot[0]
                    //TODO: tie this into coach positions... which lives in ... gamedata?
                    return false;
                }*/

        public virtual Player GetRandomPlayer(System.Random rand)
        {
            Player player = GetPlayer(rand.NextDouble() < 0.5 ? 1 : 0);
            return player;
        }

        public virtual Card GetRandomBoardCard(System.Random rand)
        {
            Player player = GetRandomPlayer(rand);
            return player.GetRandomCard(player.cards_board, rand);
        }


        public bool IsInHand(Card card)
        {
            return card != null && GetHandCard(card.uid) != null;
        }

        public bool IsOnBoard(Card card)
        {
            return card != null && GetBoardCard(card.uid) != null;
        }

        public bool IsEquipped(Card card)
        {
            return card != null && GetEquipCard(card.uid) != null;
        }

        public bool IsInDeck(Card card)
        {
            return card != null && GetDeckCard(card.uid) != null;
        }

        public bool IsInDiscard(Card card)
        {
            return card != null && GetDiscardCard(card.uid) != null;
        }

        public bool IsInSecret(Card card)
        {
            return card != null && GetSecretCard(card.uid) != null;
        }

        public bool IsInTemp(Card card)
        {
            return card != null && GetTempCard(card.uid) != null;
        }

        public bool IsCardOnSlot(CardPositionSlot slot)
        {
            return GetSlotCards(slot).Count > 0;
        }

        public bool HasStarted()
        {
            return state != GameState.Connecting;
        }

        public bool HasEnded()
        {
            return state == GameState.GameEnded;
        }

        //Same as clone, but also instantiates the variable (much slower)
        public static Game CloneNew(Game source)
        {
            Game game = new Game();
            Clone(source, game);
            return game;
        }


/*        public void AdjustBallPosition(int yardsGained)
        {
            ballManager.MoveBall(yardsGained);
        }*/

        public bool IsTouchdown()
        {
            return raw_ball_on >= 100;
        }

        public bool IsSafety()
        {
            return raw_ball_on <= 0;
        }

        //public bool AreAllPlayersPhaseReady()
        //{
        //    foreach (Player p in players)
        //    {
        //        if (!playerPhaseReady.TryGetValue(p.player_id, out bool ready) || !ready)
        //            return false;
        //    }
        //    return true;
        //}

/*        public void ResetPhaseReady()
        {
            foreach (Player p in players)
            {
                playerPhaseReady[p.player_id] = false;
            }
        }*/
        public bool AllPlayersReadyForPlay()
        {
            return players[0].SelectedPlay != PlayType.Huddle && players[1].SelectedPlay != PlayType.Huddle;
        }
        public int GetSlotIconCount(SlotMachineIconType iconType) // TODO: you could add an optional param here to also count all showing icons, not just middle ones
        {
            return current_slot_data.Results.Where(reel => reel.Middle.IconId.Equals(iconType)).Count();
        }


        //Clone all variables into another var, used mostly by the AI when building a prediction tree
        public static void Clone(Game source, Game dest)
        {
            dest.game_uid = source.game_uid;
            dest.settings = source.settings;

            dest.current_offensive_player = source.current_offensive_player;
            dest.turn_count = source.turn_count;
            dest.turn_timer = source.turn_timer;
            dest.state = source.state;
            dest.phase = source.phase;

            if (dest.players == null)
            {
                dest.players = new Player[source.players.Length];
                for(int i=0; i< source.players.Length; i++)
                    dest.players[i] = new Player(i);
            }

            for (int i = 0; i < source.players.Length; i++)
                Player.Clone(source.players[i], dest.players[i]);

            dest.selector = source.selector;
            dest.selector_player_id = source.selector_player_id;
            dest.selector_caster_uid = source.selector_caster_uid;
            dest.selector_ability_id = source.selector_ability_id;

            dest.last_destroyed = source.last_destroyed;
            dest.last_played = source.last_played;
            dest.last_target = source.last_target;
            dest.last_summoned = source.last_summoned;
            dest.ability_triggerer = source.ability_triggerer;
            dest.rolled_value = source.rolled_value;
            dest.selected_value = source.selected_value;

            CloneHash(source.ability_played, dest.ability_played);
            CloneHash(source.cards_attacked, dest.cards_attacked);
        }

        public static void CloneHash(HashSet<string> source, HashSet<string> dest)
        {
            dest.Clear();
            foreach (string str in source)
                dest.Add(str);
        }
        // ----- Helper methods for ability/state tracking -----

        /// <summary>
        /// Set the defense's play guess
        /// </summary>
        public void SetDefenseGuess(PlayType guess)
        {
            defense_guess = guess;
        }

        /// <summary>
        /// Check if defense guessed correctly (compares to offense play)
        /// </summary>
        public bool WasDefenseGuessCorrect()
        {
            if (current_offensive_player == null) return false;
            return defense_guess == current_offensive_player.SelectedPlay;
        }

        /// <summary>
        /// Record the result of a play for ability triggers and history tracking
        /// Call this at the END of play resolution
        /// </summary>
        public void RecordPlayResult(PlayResult result, int yardage)
        {
            last_play_result = result;
            last_play_yardage = yardage;
            last_coverage_guess_correct = WasDefenseGuessCorrect();

            // Create and store play history snapshot
            PlayHistory history = new PlayHistory
            {
                turn_number = turn_count,
                play_number_in_drive = play_count_this_drive,
                offensive_play = current_offensive_player.SelectedPlay,
                defensive_play = defense_guess,
                play_result = result,
                yards_gained = yardage,
                defense_guess_correct = last_coverage_guess_correct,
                current_down = current_down,
                current_half = current_half,
                ball_position = raw_ball_on,
                was_touchdown = IsTouchdown(),
                was_field_goal = after_field_goal
            };

            play_history.Add(history);
        }

        /// <summary>
        /// Add charge to a card
        /// </summary>
        public void AddCharge(string cardId, int amount)
        {
            if (!charge_tracker.ContainsKey(cardId))
                charge_tracker[cardId] = 0;
            charge_tracker[cardId] += amount;
        }

        /// <summary>
        /// Get charge value for a card
        /// </summary>
        public int GetCharge(string cardId)
        {
            return charge_tracker.ContainsKey(cardId) ? charge_tracker[cardId] : 0;
        }

        /// <summary>
        /// Reset charge for a card
        /// </summary>
        public void ResetCharge(string cardId)
        {
            if (charge_tracker.ContainsKey(cardId))
                charge_tracker[cardId] = 0;
        }

        /// <summary>
        /// Use a once-per-game ability
        /// </summary>
        public bool UseOncePerGameAbility(string abilityId)
        {
            if (!once_per_game_abilities.ContainsKey(abilityId))
                once_per_game_abilities[abilityId] = 1;
            else if (once_per_game_abilities[abilityId] > 0)
            {
                once_per_game_abilities[abilityId]--;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if once-per-game ability is available
        /// </summary>
        public bool IsOncePerGameAbilityAvailable(string abilityId)
        {
            return !once_per_game_abilities.ContainsKey(abilityId) || once_per_game_abilities[abilityId] > 0;
        }

        /// <summary>
        /// Grant a respin to a player
        /// </summary>
        public void GrantRespin(int playerId, int count = 1)
        {
            if (!respin_available.ContainsKey(playerId))
                respin_available[playerId] = 0;
            respin_available[playerId] += count;
        }

        /// <summary>
        /// Use a respin
        /// </summary>
        public bool UseRespin(int playerId)
        {
            if (respin_available.ContainsKey(playerId) && respin_available[playerId] > 0)
            {
                respin_available[playerId]--;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if respin available
        /// </summary>
        public bool HasRespin(int playerId)
        {
            return respin_available.ContainsKey(playerId) && respin_available[playerId] > 0;
        }

        /// <summary>
        /// Set player ready for current phase
        /// </summary>
        public void SetPhaseReady(int playerId, bool ready)
        {
            if (players != null && players.Length >= 2)
            {
                playerPhaseReady[playerId] = ready;
            }
        }

        /// <summary>
        /// Reset phase ready for new turn/phase
        /// </summary>
        public void ResetPhaseReady()
        {
            playerPhaseReady.Clear();
            // Initialize both players as not ready
            if (players != null && players.Length >= 2)
            {
                playerPhaseReady[0] = false;
                playerPhaseReady[1] = false;
            }
        }





        /// <summary>
        /// Check if defense guessed correctly in the last play
        /// </summary>
        public bool WasLastDefenseGuessCorrect()
        {
            PlayHistory last = GetLastPlay();
            return last != null && last.defense_guess_correct;
        }



        /// <summary>
        /// Get total yards gained in the current drive
        /// </summary>
        public int GetDriveYardage()
        {
            int totalYards = 0;
            foreach (PlayHistory play in play_history)
            {
                if (play.play_number_in_drive > 0 && play.turn_number >= (turn_count - play_count_this_drive))
                {
                    totalYards += play.yards_gained;
                }
            }
            return totalYards;
        }

        /// <summary>
        /// Check if this is the first play of the game
        /// </summary>
        public bool IsFirstPlayOfGame()
        {
            return play_history.Count == 0;
        }
    }

    [System.Serializable]
    public enum GameState
    {
        Connecting = 0, //Players are not connected
        Play = 20,      //Game is being played
        GameEnded = 99,
        TitleScreen = 1,
    }

    [System.Serializable]
    public enum GamePhase
    {
        None = 0,
        Mulligan = 5,
        StartTurn = 10, //Start of turn resolution - Play player cards simultaneously?
        ChoosePlayers = 11, // pick player cards - not revealed to other player.
        RevealPlayers = 12, //Reveal new players to board.
        ChoosePlay = 13, //Choose play/ coverage and any applicable enhancer cards
        RevealPlayCalls = 14,
        SlotSpin = 15, // slot machine spins
        Resolution = 20,
        LiveBall = 30,      //Main play phase - ball is live!
        EndTurn = 40,   //End of turn resolutions
    }

    [System.Serializable]
    public enum SelectorType
    {
        None = 0,
        SelectTarget = 10,
        SelectorCard = 20,
        SelectorChoice = 30,
        SelectorCost = 40,
    }
    [System.Serializable]
    public enum FailPlayEventType
    {
        None = 0,
        TackleForLoss= 1,
        Sack = 2,
        QBFumble  = 3,       
        BattedPass = 4,
        TippedPass = 5,       
        Interception = 6,
        IncompletePass = 7,          
        RunnerFumble = 8,   
    }
    public enum FieldDirection
    {
        Player0GoesUp = 0,
        Player0GoesDown = 1
    }
    /*    [System.Serializable]
        public enum CardStatType
        { //use insdead of strings.
            ShortPassBonus,
            RunBonus,
            DeepPassBonus,
            ShortPassCoverageBonus,
            RunCoverageBonus,
            DeepPassCoverageBonus,
            Stamina,
            Grit,
            None // Default
        }*/


}
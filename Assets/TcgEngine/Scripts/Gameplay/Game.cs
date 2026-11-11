using TcgEngine.Effects;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace TcgEngine.Gameplay
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

    /// <summary>Why a card can't be played right now. None = playable.</summary>
    public enum CardPlayError
    {
        None = 0,
        NullCard,
        NoPlayer,
        NoOffense,
        UnsupportedType,
        WrongPhase,
        WrongSide,
        NotInHand,
        SuitUsed,
        PositionFull,
        InvalidSlot,
        SlotOccupied,
        AlreadyPlayedOne,
        PlayMismatch,
        SlotRequirements,
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

    public enum LiveBallEventType
    {
        BothPass = 0,
        NegateOffense = 1,
        NegateDefense = 2,
        FumbleTurnover = 3,
        FumbleRecovery = 4,
        BallSecurity = 5,
        Reveal = 6,
        YardageDelta = 7,    // payload: offGrit=delta, defGrit=newTotal, offCard=source
        PostLBResolved = 8   // payload: offGrit=preLB, defGrit=finalLB, turnover flag in game state
    }

    /// <summary>
    /// LiveBall resolves in one server frame; the client replays its events one beat at a time.
    /// Server holds EndTurn for the summed beat time so nothing is cut off. Single source of truth for both.
    /// </summary>
    public static class LiveBallBeats
    {
        public const float EndPad = 0.6f;

        public static float Duration(LiveBallEventType type, bool anyCard)
        {
            switch (type)
            {
                case LiveBallEventType.Reveal: return anyCard ? 4.2f : 0f; // LiveBallRevealPanel 2-frame reveal
                case LiveBallEventType.BothPass: return 1.2f;
                case LiveBallEventType.NegateOffense:
                case LiveBallEventType.NegateDefense:
                case LiveBallEventType.BallSecurity: return 2.4f;
                case LiveBallEventType.FumbleRecovery: return 2.8f;
                case LiveBallEventType.FumbleTurnover: return 3.0f;
                case LiveBallEventType.YardageDelta: return 2.6f;
                case LiveBallEventType.PostLBResolved: return anyCard ? 1.8f : 0f;
                default: return 1.5f;
            }
        }
    }

    /// <summary>
    /// One ability's contribution to the current play — recorded in TriggerBoardSlotAbilities,
    /// consumed by the client's pre-snap impact showcases.
    /// </summary>
    [System.Serializable]
    public class PlayImpact
    {
        public string caster_uid;
        public string ability_id;
        public int status_type;   // (int)StatusType; 0 = non-stat ability (respin, slot modifier...)
        public int value;         // status delta attributed to this ability; 0 for non-stat
    }

    /// <summary>
    /// Breakdown of yardage calculation for UI display.
    /// Populated during ResolveRunOutcome / ResolvePassOutcome.
    /// </summary>
    [System.Serializable]
    public class YardageBreakdown
    {
        public int offCoachBase;        // coach base offense yards
        public int offPlayerBase;       // player card base stat (run_bonus or receiver pass bonus)
        public int offStatusBonuses;    // ability/slot status bonuses
        public int offOtherPositions;   // QB/OL pass bonuses (pass only)
        public int defCoverageBase;     // defensive card coverage
        public int defCoachBase;        // coach base defense
        public int defCoverageMod;      // coverage guess modifier
        public int defStatusBonuses;    // defensive status bonuses
        public int netYardage;          // final result
        public string receiverName;     // target receiver card title (pass) or ball carrier (run)
        public string ctrDefenderName;  // who CTR covered (pass only)
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
        public List<string> offensive_card_uids = new List<string>();
        public List<string> defensive_card_uids = new List<string>();
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
        public float phase_timer = 0f;  // Countdown for timed phases (ChoosePlay etc.)
        public int current_down = 1;
        public int current_half = 1;
        public int first_half_offense_player_id = 0; // who started half 1 on offense; opponent gets ball in half 2
        public int raw_ball_on = 25;
        public FieldDirection fieldDirection;
        public int plays_left_in_half = 11;
        public int yardage_this_play;
        public int yardage_to_go;
        public bool turnover_pending = false; // Set by ResolvePlayOutcome when a fail-event turnover fires; consumed by EndPlayPhase
        public int live_ball_grit_bonus = 0;    // grit bonus from off live-ball card; set before fumble ability fires; read by EffectForceTurnover.DoEffect
        public int live_ball_return_yards = 0;  // return yards from live-ball fumble; set in HandleLiveBallTurnover; applied in EndPlayPhase turnover path
        public string ball_carrier_uid;          // uid of card carrying the ball (RB for runs, receiver for passes) — drives client animation
        public string target_receiver_uid;       // uid of receiver who caught the pass — drives client animation
        public FailPlayEventType last_fail_event = FailPlayEventType.None; // which fail event occurred this play (Sack/INT/Fumble/etc) — drives ball animation
        public YardageBreakdown yardage_breakdown; // populated during ResolveRunOutcome/ResolvePassOutcome for UI display
        public string interceptor_uid;           // uid of defensive card that intercepted — drives ball animation
        public string fumble_recoverer_uid;      // uid of card that recovered fumble — drives ball animation
        public bool ball_was_live;               // this play went to LiveBall phase — routes Resolution NextStep, drives post-LB re-animation
        public string fail_event_caster_uid;     // uid of card that caused last_fail_event (sacker/TFL man/tipper) — drives defender animation
        public string tackler_uid;               // uid of defender credited with ending the play (null = TD/incomplete) — drives tackle animation
        public string ctr_defender_uid;          // uid of CTR card that shifted coverage this pass — drives coverage-shift banner
        public string ctr_covered_receiver_uid;  // uid of top receiver taken away by CTR
        public List<PlayImpact> play_impacts = new List<PlayImpact>(); // per-ability contributions this play — drives pre-snap impact showcases

        // Slot machine modifiers (from abilities)
        public List<SlotModifier> temp_slot_modifiers;
        public int pending_respins = 0;

        // Play history
        public List<PlayHistory> play_history = new List<PlayHistory>();

        //Players
        public Player[] players;
        public Dictionary<int, bool> playerPhaseReady = new Dictionary<int, bool>();

        public Player current_offensive_player;

        public int winner_id = -2; // -2=undecided, -1=tie, 0/1=player

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
            InitCoachFromAsset(players[0], "TestCoach_Offense");
            InitCoachFromAsset(players[1], "TestCoachProfile_Aggressive");

            // Fallback: if assets not found, keep default constructor values but set completion reqs
            EnsureCompletionRequirements(players[0].head_coach);
            EnsureCompletionRequirements(players[1].head_coach);
        }

        private static void InitCoachFromAsset(Player player, string assetName)
        {
            // Try CoachCardData first (full card wrapper)
            CoachCardData cardData = Resources.Load<CoachCardData>("Coaches/" + assetName);
            if (cardData != null)
            {
                player.coach_card_id = assetName;
                player.head_coach.InitFromData(cardData);
                return;
            }

            // Fallback: raw CoachData asset (not wrapped in CoachCardData)
            CoachData coachData = Resources.Load<CoachData>("Coaches/" + assetName);
            if (coachData != null)
            {
                player.coach_card_id = assetName;
                player.head_coach.coachData = coachData;
                player.head_coach.coachType = coachData.coachType;
                return;
            }

            Debug.LogWarning($"[Game] No coach asset found: Coaches/{assetName}. Using defaults.");
            player.head_coach.baseOffenseYardage[PlayType.Run]       = 3;
            player.head_coach.baseOffenseYardage[PlayType.ShortPass] = 5;
            player.head_coach.baseOffenseYardage[PlayType.LongPass]  = 10;
            player.head_coach.baseDefenseYardage[PlayType.Run]       = 2;
            player.head_coach.baseDefenseYardage[PlayType.ShortPass] = 3;
            player.head_coach.baseDefenseYardage[PlayType.LongPass]  = 7;
        }

        private static void EnsureCompletionRequirements(HeadCoachCard coach)
        {
            if (!coach.completionRequirements.ContainsKey(PlayType.ShortPass))
                coach.completionRequirements[PlayType.ShortPass] = new List<CompletionRequirement>
                {
                    new CompletionRequirement { icon = SlotMachineIconType.Football, minCount = 1 },
                    new CompletionRequirement { icon = SlotMachineIconType.Helmet,   minCount = 1 }
                };
            if (!coach.completionRequirements.ContainsKey(PlayType.LongPass))
                coach.completionRequirements[PlayType.LongPass] = new List<CompletionRequirement>
                {
                    new CompletionRequirement { icon = SlotMachineIconType.Star, minCount = 1 }
                };
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
            return GetCardPlayError(card, slot) == CardPlayError.None;
        }

        /// <summary>
        /// True if this CardType belongs to the offense side of the ball. Enhancers/live-ball cards
        /// have playerPosition NONE, so the side must come from CardType, not position group.
        /// </summary>
        public static bool IsOffenseCardType(CardType t)
        {
            return t == CardType.OffensivePlayer || t == CardType.OffensivePlayEnhancer || t == CardType.OffLiveBall;
        }

        public static bool IsDefenseCardType(CardType t)
        {
            return t == CardType.DefensivePlayer || t == CardType.DefensivePlayEnhancer || t == CardType.DefLiveBall;
        }

        /// <summary>
        /// Single source of truth for card eligibility. Used by CanPlayCard (server), AI action generation,
        /// and client-side glow/drag feedback. `playOverride` lets callers test an enhancer against a play
        /// the player hasn't committed to yet (AI candidate generation, client hover).
        /// </summary>
        public virtual CardPlayError GetCardPlayError(Card card, CardPositionSlot slot, PlayType? playOverride = null)
        {
            if (card == null || card.CardData == null)
                return CardPlayError.NullCard;

            Player player = GetPlayer(card.player_id);
            if (player == null)
                return CardPlayError.NoPlayer;

            CardData data = card.CardData;
            CardType cardType = data.type;

            // Only football card types participate in the play loop
            if (!IsOffenseCardType(cardType) && !IsDefenseCardType(cardType))
                return CardPlayError.UnsupportedType;

            // Phase lock per CardType
            if (data.IsPlayer() && phase != GamePhase.ChoosePlayers)
                return CardPlayError.WrongPhase;
            if (data.IsPlayEnhancer() && phase != GamePhase.ChoosePlay)
                return CardPlayError.WrongPhase;
            if (data.IsLiveBall() && phase != GamePhase.LiveBall)
                return CardPlayError.WrongPhase;

            // Side of ball, from CardType
            if (current_offensive_player == null)
                return CardPlayError.NoOffense;
            bool is_player_offensive = player.player_id == current_offensive_player.player_id;
            if (is_player_offensive && IsDefenseCardType(cardType))
                return CardPlayError.WrongSide;
            if (!is_player_offensive && IsOffenseCardType(cardType))
                return CardPlayError.WrongSide;

            // Secondary side check via position group (catches mis-tagged assets)
            PlayerPositionGrp pos = data.playerPosition;
            if (pos != PlayerPositionGrp.NONE)
            {
                if (is_player_offensive && defensive_pos_grps.Contains(pos))
                    return CardPlayError.WrongSide;
                if (!is_player_offensive && offensive_pos_grps.Contains(pos))
                    return CardPlayError.WrongSide;
            }

            // Must own the card in hand or sideline
            if (!player.HasCard(player.cards_hand, card) && !player.HasCard(player.cards_sideline, card))
                return CardPlayError.NotInHand;

            if (data.IsPlayer())
            {
                CardSuit suit = data.suit;
                if (suit != CardSuit.None && player.suits_played_this_turn.Contains(suit))
                    return CardPlayError.SuitUsed;

                if (pos == PlayerPositionGrp.NONE)
                    return CardPlayError.InvalidSlot;

                // Positional max from head coach scheme (skip if scheme missing the group)
                var scheme = player.head_coach?.positional_Scheme;
                if (scheme != null && scheme.TryGetValue(pos, out HCPlayerSchemeData schemeData))
                {
                    int onField = player.cards_board.Count(cd => cd.CardData != null && cd.CardData.playerPosition == pos);
                    if (onField >= schemeData.pos_max)
                        return CardPlayError.PositionFull;
                }

                if (!slot.IsValid() || slot.posGroupType != pos)
                    return CardPlayError.InvalidSlot;
                if (CardPositionSlot.GetP(card.player_id) != slot.p)
                    return CardPlayError.InvalidSlot;
                if (IsCardOnSlot(slot))
                    return CardPlayError.SlotOccupied;
                return CardPlayError.None;
            }

            if (data.IsPlayEnhancer())
            {
                if (player.PlayEnhancer != null)
                    return CardPlayError.AlreadyPlayedOne;
                PlayType play = playOverride ?? player.SelectedPlay;
                if (data.required_plays != null && data.required_plays.Length > 0
                    && System.Array.IndexOf(data.required_plays, play) < 0)
                    return CardPlayError.PlayMismatch;
                if (!data.AreSlotRequirementsMet(current_slot_data))
                    return CardPlayError.SlotRequirements;
                return CardPlayError.None;
            }

            if (data.IsLiveBall())
            {
                if (player.LiveBallCard != null)
                    return CardPlayError.AlreadyPlayedOne;
                if (!data.AreSlotRequirementsMet(current_slot_data))
                    return CardPlayError.SlotRequirements;
                return CardPlayError.None;
            }

            return CardPlayError.UnsupportedType;
        }

        public static string GetCardPlayErrorText(CardPlayError err)
        {
            switch (err)
            {
                case CardPlayError.None: return "";
                case CardPlayError.WrongPhase: return "Not this phase";
                case CardPlayError.WrongSide: return "Wrong side of ball";
                case CardPlayError.SuitUsed: return "Suit already played";
                case CardPlayError.AlreadyPlayedOne: return "Already played one";
                case CardPlayError.PlayMismatch: return "Doesn't match play call";
                case CardPlayError.SlotRequirements: return "Slot icons not met";
                case CardPlayError.PositionFull: return "Position full";
                case CardPlayError.SlotOccupied: return "Slot occupied";
                case CardPlayError.InvalidSlot: return "Invalid slot";
                case CardPlayError.NotInHand: return "Not in hand";
                case CardPlayError.UnsupportedType: return "Unplayable card type";
                default: return err.ToString();
            }
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
            return GetDefensePlayer();
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

            dest.turn_count = source.turn_count;
            dest.turn_timer = source.turn_timer;
            dest.state = source.state;
            dest.phase = source.phase;

            // Football fields
            dest.raw_ball_on = source.raw_ball_on;
            dest.current_down = source.current_down;
            dest.plays_left_in_half = source.plays_left_in_half;
            dest.yardage_this_play = source.yardage_this_play;
            dest.yardage_breakdown = source.yardage_breakdown;
            dest.current_half = source.current_half;
            dest.play_count_this_drive = source.play_count_this_drive;
            dest.after_field_goal = source.after_field_goal;
            dest.defense_guess = source.defense_guess;

            if (dest.players == null)
            {
                dest.players = new Player[source.players.Length];
                for(int i=0; i< source.players.Length; i++)
                    dest.players[i] = new Player(i);
            }

            for (int i = 0; i < source.players.Length; i++)
                Player.Clone(source.players[i], dest.players[i]);

            // Remap player reference to cloned instance
            dest.current_offensive_player = source.current_offensive_player != null
                ? dest.players[source.current_offensive_player.player_id] : null;

            dest.selector = source.selector;
            dest.selector_player_id = source.selector_player_id;
            dest.selector_caster_uid = source.selector_caster_uid;
            dest.selector_ability_id = source.selector_ability_id;

            dest.ball_carrier_uid = source.ball_carrier_uid;
            dest.target_receiver_uid = source.target_receiver_uid;
            dest.ball_was_live = source.ball_was_live;
            dest.fail_event_caster_uid = source.fail_event_caster_uid;
            dest.tackler_uid = source.tackler_uid;
            dest.ctr_defender_uid = source.ctr_defender_uid;
            dest.ctr_covered_receiver_uid = source.ctr_covered_receiver_uid;
            dest.play_impacts = source.play_impacts; // shallow, same pattern as yardage_breakdown
            dest.last_fail_event = source.last_fail_event;
            dest.interceptor_uid = source.interceptor_uid;
            dest.fumble_recoverer_uid = source.fumble_recoverer_uid;
            dest.turnover_pending = source.turnover_pending;
            dest.live_ball_grit_bonus = source.live_ball_grit_bonus;
            dest.live_ball_return_yards = source.live_ball_return_yards;
            dest.last_play_type = source.last_play_type;
            dest.last_play_result = source.last_play_result;
            dest.last_play_yardage = source.last_play_yardage;
            dest.last_coverage_guess_correct = source.last_coverage_guess_correct;

            dest.last_destroyed = source.last_destroyed;
            dest.last_played = source.last_played;
            dest.last_target = source.last_target;
            dest.last_summoned = source.last_summoned;
            dest.ability_triggerer = source.ability_triggerer;
            dest.rolled_value = source.rolled_value;
            dest.selected_value = source.selected_value;

            CloneHash(source.ability_played, dest.ability_played);
            CloneHash(source.cards_attacked, dest.cards_attacked);

            dest.playerPhaseReady.Clear();
            foreach (var kvp in source.playerPhaseReady)
                dest.playerPhaseReady[kvp.Key] = kvp.Value;
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
            Player defPlayer = GetCurrentDefensivePlayer();
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
                was_field_goal = after_field_goal,
                offensive_card_uids = current_offensive_player.cards_board.Select(c => c.uid).ToList(),
                defensive_card_uids = defPlayer != null ? defPlayer.cards_board.Select(c => c.uid).ToList() : new List<string>()
            };

            play_history.Add(history);
            if (play_history.Count > 50)
                play_history.RemoveAt(0);
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
    /// <summary>
    /// Fail events ordered by chronological football priority.
    /// Pass: Sack(pre-throw) → QBFumble(pre-throw) → BattedPass(at-line) → TippedPass(in-flight)
    ///       → Interception(in-flight) → IncompletePass(in-flight) → RunnerFumble(post-catch)
    /// Run:  TackleForLoss(at-contact) → RunnerFumble(post-contact)
    /// First-match-wins via OrderBy(enum value) in ResolvePassFailEvents / ResolveRunFailEvents.
    /// </summary>
    [System.Serializable]
    public enum FailPlayEventType
    {
        None = 0,
        TackleForLoss = 1,   // Run: at-contact
        Sack = 2,            // Pass: pre-throw — chains to QBFumble if both present
        QBFumble = 3,        // Pass: pre-throw — ball loose at LOS
        BattedPass = 4,      // Pass: at-line — knocked down at scrimmage
        TippedPass = 5,      // Pass: in-flight — deflected, grit-based INT check
        Interception = 6,    // Pass: in-flight — clean INT
        IncompletePass = 7,  // Pass: in-flight — ball hits ground
        RunnerFumble = 8,    // Both: post-contact/post-catch
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
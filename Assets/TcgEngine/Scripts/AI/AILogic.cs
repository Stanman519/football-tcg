using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
using System.Linq;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.AI
{
    /// <summary>
    /// Minimax algorithm for AI.
    /// </summary>

    public class AILogic
    {
        //-------- AI Logic Params ------------------

        public int ai_depth = 3;                //How many turns in advance does it check
        public int ai_depth_wide = 1;           //For first few turns, consider more options
        public int actions_per_turn = 2;        //Max sequential actions per turn
        public int actions_per_turn_wide = 3;   //Same but in wide depth
        public int nodes_per_action = 4;        //Max child nodes per action
        public int nodes_per_action_wide = 7;   //Same but in wide depth

        //-----

        public int ai_player_id;
        public int ai_level;

        private GameLogicService game_logic;
        private Game original_data;
        private AIHeuristic heuristic;
        private Thread ai_thread;

        private NodeState first_node = null;
        private NodeState best_move = null;

        private bool running = false;
        private int nb_calculated = 0;
        private int reached_depth = 0;

        private System.Random random_gen;

        private Pool<NodeState> node_pool = new Pool<NodeState>();
        private Pool<Game> data_pool = new Pool<Game>();
        private Pool<AIAction> action_pool = new Pool<AIAction>();
        private Pool<List<AIAction>> list_pool = new Pool<List<AIAction>>();
        private ListSwap<Card> card_array = new ListSwap<Card>();
        private ListSwap<CardPositionSlot> slot_array = new ListSwap<CardPositionSlot>();

        public static AILogic Create(int player_id, int level)
        {
            AILogic job = new AILogic();
            job.ai_player_id = player_id;
            job.ai_level = level;

            job.heuristic = new AIHeuristic(player_id, level);
            job.game_logic = new GameLogicService(true);

            return job;
        }

        public void RunAI(Game data)
        {
            if (running)
                return;

            original_data = Game.CloneNew(data);
            game_logic.ClearResolve();
            game_logic.SetData(original_data);
            random_gen = new System.Random();

            first_node = null;
            reached_depth = 0;
            nb_calculated = 0;
            running = true;

            //Uncomment for threaded execution (production):
            //ai_thread = new Thread(Execute);
            //ai_thread.Start();

            Execute();
        }

        public void Stop()
        {
            running = false;
            if (ai_thread != null && ai_thread.IsAlive)
                ai_thread.Abort();
        }

        private void Execute()
        {
            first_node = CreateNode(null, null, ai_player_id, 0, 0);
            first_node.hvalue = heuristic.CalculateHeuristic(original_data, first_node);
            first_node.alpha = int.MinValue;
            first_node.beta = int.MaxValue;

            Profiler.BeginSample("AI");
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

            CalculateNode(original_data, first_node);

            Debug.Log("AI: Time " + watch.ElapsedMilliseconds + "ms Depth " + reached_depth + " Nodes " + nb_calculated);
            Profiler.EndSample();

            best_move = first_node.best_child;
            running = false;
        }

        private void CalculateNode(Game data, NodeState node)
        {
            Profiler.BeginSample("Add Actions");
            Player offensivePlayer = data.current_offensive_player;
            var player = data.GetPlayer(node.current_player);
            var isOffense = offensivePlayer != null && offensivePlayer == player;
            List<AIAction> action_list = list_pool.Create();

            int max_actions = node.tdepth < ai_depth_wide ? actions_per_turn_wide : actions_per_turn;
            if (node.taction < max_actions)
            {
                if (data.selector == SelectorType.None && player != null)
                {
                    // --- ChoosePlayers: play player cards from hand (matching side) ---
                    if (original_data.phase == GamePhase.ChoosePlayers)
                    {
                        var playersInHand = player.cards_hand
                            .Where(c => {
                                var pos = c.CardData.playerPosition;
                                if (pos == PlayerPositionGrp.NONE) return false;
                                if (isOffense) return data.offensive_pos_grps.Contains(pos);
                                return data.defensive_pos_grps.Contains(pos);
                            })
                            .ToList();
                        for (int c = 0; c < playersInHand.Count; c++)
                        {
                            Card card = playersInHand[c];
                            AddActions(action_list, data, node, GameAction.PlayCard, card);
                        }
                    }

                    // --- ChoosePlay: select play type + optional enhancer ---
                    if (original_data.phase == GamePhase.ChoosePlay)
                    {
                        var enhancers = player.cards_hand
                            .Where(c => c.Data.type == (isOffense ? CardType.OffensivePlayEnhancer : CardType.DefensivePlayEnhancer))
                            .ToList();

                        // Generate one action per play type (no enhancer)
                        AddActions(action_list, data, node, GameAction.SelectPlay, null);

                        // Generate enhancer actions
                        for (int c = 0; c < enhancers.Count; c++)
                        {
                            Card card = enhancers[c];
                            AddActions(action_list, data, node, GameAction.SelectPlay, card);
                        }
                    }

                    // --- LiveBall: play live ball cards ---
                    if (original_data.phase == GamePhase.LiveBall)
                    {
                        CardType liveType = isOffense ? CardType.OffLiveBall : CardType.DefLiveBall;
                        var liveCards = player.cards_hand
                            .Where(c => c.Data.type == liveType)
                            .ToList();
                        for (int c = 0; c < liveCards.Count; c++)
                        {
                            Card card = liveCards[c];
                            if (data.CanPlayCard(card, CardPositionSlot.None))
                            {
                                AIAction action = CreateAction(GameAction.PlayCard, card);
                                action_list.Add(action);
                            }
                        }
                    }
                }
                else if (data.selector != SelectorType.None)
                {
                    AddSelectActions(action_list, data, node);
                }
            }

            // Phase-locked modes use PlayerReadyPhase, not EndTurn
            bool is_phase_locked = original_data.phase == GamePhase.ChoosePlayers
                                || original_data.phase == GamePhase.ChoosePlay
                                || original_data.phase == GamePhase.LiveBall;

            // In ChoosePlay, SelectPlay is the terminal action
            bool skip_ready_action = original_data.phase == GamePhase.ChoosePlay;

            bool can_end = data.selector == SelectorType.None;
            if ((action_list.Count == 0 || can_end) && !skip_ready_action)
            {
                ushort action_type = is_phase_locked ? GameAction.PlayerReadyPhase : GameAction.EndTurn;
                AIAction actiont = CreateAction(action_type);
                action_list.Add(actiont);
            }

            FilterActions(data, node, action_list);
            Profiler.EndSample();

            for (int o = 0; o < action_list.Count; o++)
            {
                AIAction action = action_list[o];
                if (action.valid && node.alpha < node.beta)
                {
                    CalculateChildNode(data, node, action);
                }
            }

            action_list.Clear();
            list_pool.Dispose(action_list);
        }

        private void FilterActions(Game data, NodeState node, List<AIAction> action_list)
        {
            int count_valid = 0;
            for (int o = 0; o < action_list.Count; o++)
            {
                AIAction action = action_list[o];
                action.sort = heuristic.CalculateActionSort(data, action);
                action.valid = action.sort <= 0 || action.sort >= node.sort_min;
                if (action.valid)
                    count_valid++;
            }

            int max_actions = node.tdepth < ai_depth_wide ? nodes_per_action_wide : nodes_per_action;
            int max_actions_skip = max_actions + 2;
            if (count_valid <= max_actions_skip)
                return;

            for (int o = 0; o < action_list.Count; o++)
            {
                AIAction action = action_list[o];
                if (action.valid)
                {
                    action.score = heuristic.CalculateActionScore(data, action);
                }
            }

            action_list.Sort((AIAction a, AIAction b) => { return b.score.CompareTo(a.score); });
            for (int o = 0; o < action_list.Count; o++)
            {
                AIAction action = action_list[o];
                action.valid = action.valid && o < max_actions;
            }
        }

        private void CalculateChildNode(Game data, NodeState parent, AIAction action)
        {
            if (action.type == GameAction.None)
                return;

            int player_id = parent.current_player;

            Profiler.BeginSample("Clone Data");
            Game ndata = data_pool.Create();
            Game.Clone(data, ndata);
            game_logic.ClearResolve();
            game_logic.SetData(ndata);
            Profiler.EndSample();

            Profiler.BeginSample("Execute AIAction");
            DoAIAction(ndata, action, player_id);
            Profiler.EndSample();

            bool new_turn = action.type == GameAction.EndTurn;
            bool phase_transition = action.type == GameAction.PlayerReadyPhase;
            int next_tdepth = parent.tdepth;
            int next_taction = parent.taction + 1;

            if (new_turn || phase_transition)
            {
                next_tdepth = parent.tdepth + 1;
                next_taction = 0;
            }

            Profiler.BeginSample("Create Node");
            NodeState child_node = CreateNode(parent, action, player_id, next_tdepth, next_taction);
            parent.childs.Add(child_node);
            Profiler.EndSample();

            child_node.sort_min = (new_turn || phase_transition) ? 0 : Mathf.Max(action.sort, child_node.sort_min);

            if (!ndata.HasEnded() && child_node.tdepth < ai_depth)
            {
                CalculateNode(ndata, child_node);
            }
            else
            {
                child_node.hvalue = heuristic.CalculateHeuristic(ndata, child_node);
            }

            if (player_id == ai_player_id)
            {
                if (parent.best_child == null || child_node.hvalue > parent.hvalue)
                {
                    parent.best_child = child_node;
                    parent.hvalue = child_node.hvalue;
                    parent.alpha = Mathf.Max(parent.alpha, parent.hvalue);
                }
            }
            else
            {
                if (parent.best_child == null || child_node.hvalue < parent.hvalue)
                {
                    parent.best_child = child_node;
                    parent.hvalue = child_node.hvalue;
                    parent.beta = Mathf.Min(parent.beta, parent.hvalue);
                }
            }

            nb_calculated++;
            if (child_node.tdepth > reached_depth)
                reached_depth = child_node.tdepth;

            data_pool.Dispose(ndata);
        }

        private NodeState CreateNode(NodeState parent, AIAction action, int player_id, int turn_depth, int turn_action)
        {
            NodeState nnode = node_pool.Create();
            nnode.current_player = player_id;
            nnode.tdepth = turn_depth;
            nnode.taction = turn_action;
            nnode.parent = parent;
            nnode.last_action = action;
            nnode.alpha = parent != null ? parent.alpha : int.MinValue;
            nnode.beta = parent != null ? parent.beta : int.MaxValue;
            nnode.hvalue = 0;
            nnode.sort_min = 0;
            return nnode;
        }

        private void AddActions(List<AIAction> actions, Game data, NodeState node, ushort type, Card card)
        {
            Player player = data.GetPlayer(node.current_player);

            if (data.selector != SelectorType.None)
                return;

            if (card != null && card.HasStatus(StatusType.Paralysed))
                return;

            if (type == GameAction.PlayCard)
            {
                if (card.CardData.IsPlayer())
                {
                    var slotsForPosition = slot_array.Get()
                        .Where(s => s.posGroupType == card.CardData.playerPosition).ToList();
                    CardPositionSlot slot = player.GetRandomEmptySlotForPosition(random_gen, slotsForPosition);

                    if (data.CanPlayCard(card, slot))
                    {
                        AIAction action = CreateAction(type, card);
                        action.slot = slot;
                        actions.Add(action);
                    }
                }
                else if (data.CanPlayCard(card, CardPositionSlot.None))
                {
                    AIAction action = CreateAction(type, card);
                    actions.Add(action);
                }
            }

            if (type == GameAction.SelectPlay)
            {
                if (card == null)
                {
                    // Generate one action per play type so MiniMax evaluates each
                    PlayType[] validPlays = { PlayType.Run, PlayType.ShortPass, PlayType.LongPass };
                    foreach (var play in validPlays)
                    {
                        AIAction action = CreateAction(type, play);
                        actions.Add(action);
                    }
                }
                else
                {
                    // Enhancer — generate one action per required play
                    if (card.Data.required_plays != null && card.Data.required_plays.Length > 0)
                    {
                        foreach (var play in card.Data.required_plays)
                        {
                            AIAction action = CreateAction(type, card);
                            action.selectedPlay = play;
                            actions.Add(action);
                        }
                    }
                    else
                    {
                        // Enhancer with no play restriction — pair with each play type
                        PlayType[] validPlays = { PlayType.Run, PlayType.ShortPass, PlayType.LongPass };
                        foreach (var play in validPlays)
                        {
                            AIAction action = CreateAction(type, card);
                            action.selectedPlay = play;
                            actions.Add(action);
                        }
                    }
                }
            }
        }

        private void AddSelectActions(List<AIAction> actions, Game data, NodeState node)
        {
            if (data.selector == SelectorType.None)
                return;

            Player player = data.GetPlayer(data.selector_player_id);
            Card caster = data.GetCard(data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(data.selector_ability_id);
            if (player == null || caster == null)
                return;

            if (data.selector == SelectorType.SelectTarget && ability != null)
            {
                for (int p = 0; p < data.players.Length; p++)
                {
                    Player tplayer = data.players[p];
                    if (ability.CanTarget(data, caster, tplayer))
                    {
                        AIAction action = CreateAction(GameAction.SelectPlayer, caster);
                        action.target_player_id = tplayer.player_id;
                        actions.Add(action);
                    }
                }

                foreach (CardPositionSlot slot in CardPositionSlot.GetAll())
                {
                    List<Card> tcards = data.GetSlotCards(slot);
                    foreach (Card tcard in tcards)
                    {
                        if (tcard != null && ability.CanTarget(data, caster, tcard))
                        {
                            AIAction action = CreateAction(GameAction.SelectCard, caster);
                            action.target_uid = tcard.uid;
                            actions.Add(action);
                        }
                        else if (tcard == null && ability.CanTarget(data, caster, slot))
                        {
                            AIAction action = CreateAction(GameAction.SelectSlot, caster);
                            action.slot = slot;
                            actions.Add(action);
                        }
                    }
                }
            }

            if (data.selector == SelectorType.SelectorCard && ability != null)
            {
                List<Card> cards = ability.GetCardTargets(data, caster, card_array);
                foreach (Card tcard in cards)
                {
                    AIAction action = CreateAction(GameAction.SelectCard, caster);
                    action.target_uid = tcard.uid;
                    actions.Add(action);
                }
            }

            if (data.selector == SelectorType.SelectorChoice && ability != null)
            {
                for (int i = 0; i < ability.chain_abilities.Length; i++)
                {
                    AbilityData choice = ability.chain_abilities[i];
                    if (choice != null && data.CanSelectAbility(caster, choice))
                    {
                        AIAction action = CreateAction(GameAction.SelectChoice, caster);
                        action.value = i;
                        actions.Add(action);
                    }
                }
            }

            if (actions.Count == 0)
            {
                AIAction caction = CreateAction(GameAction.CancelSelect, caster);
                actions.Add(caction);
            }
        }

        private AIAction CreateAction(ushort type)
        {
            AIAction action = action_pool.Create();
            action.Clear();
            action.type = type;
            action.valid = true;
            return action;
        }

        private AIAction CreateAction(ushort type, PlayType playCall)
        {
            AIAction action = action_pool.Create();
            action.Clear();
            action.type = type;
            action.valid = true;
            action.selectedPlay = playCall;
            return action;
        }

        private AIAction CreateAction(ushort type, Card card)
        {
            AIAction action = action_pool.Create();
            action.Clear();
            action.type = type;
            action.card_uid = card.uid;
            action.valid = true;
            return action;
        }

        private void DoAIAction(Game data, AIAction action, int player_id)
        {
            Player player = data.GetPlayer(player_id);

            if (action.type == GameAction.PlayCard)
            {
                Card card = player.GetHandCard(action.card_uid);
                game_logic.PlayCard(card, action.slot);
            }

            if (action.type == GameAction.CastAbility)
            {
                Card card = player.GetCard(action.card_uid);
                AbilityData ability = AbilityData.Get(action.ability_id);
                game_logic.CastAbility(card, ability);
            }

            if (action.type == GameAction.SelectCard)
            {
                Card target = data.GetCard(action.target_uid);
                game_logic.SelectCard(target);
            }

            if (action.type == GameAction.SelectPlayer)
            {
                Player target = data.GetPlayer(action.target_player_id);
                game_logic.SelectPlayer(target);
            }

            if (action.type == GameAction.SelectSlot)
            {
                game_logic.SelectSlot(action.slot);
            }

            if (action.type == GameAction.SelectChoice)
            {
                game_logic.SelectChoice(action.value);
            }

            if (action.type == GameAction.CancelSelect)
            {
                game_logic.CancelSelection();
            }

            if (action.type == GameAction.PlayerReadyPhase)
            {
                player.SetReadyForPhase(data.phase, true);
            }

            if (action.type == GameAction.SelectPlay)
            {
                player.SelectedPlay = action.selectedPlay;
                player.PlayEnhancer = action.card_uid != null ? player.GetHandCard(action.card_uid) : null;
            }

            if (action.type == GameAction.EndTurn)
            {
                game_logic.EndTurn();
            }
        }

        private bool HasAction(List<AIAction> list, ushort type)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].type == type)
                    return true;
            }
            return false;
        }

        //----Return values----

        public bool IsRunning()
        {
            return running;
        }

        public string GetNodePath()
        {
            return GetNodePath(first_node);
        }

        public string GetNodePath(NodeState node)
        {
            string path = "Prediction: HValue: " + node.hvalue + "\n";
            NodeState current = node;
            AIAction move;

            while (current != null)
            {
                move = current.last_action;
                if (move != null)
                    path += "Player " + current.current_player + ": " + move.GetText(original_data) + "\n";
                current = current.best_child;
            }
            return path;
        }

        public void ClearMemory()
        {
            original_data = null;
            first_node = null;
            best_move = null;

            foreach (NodeState node in node_pool.GetAllActive())
                node.Clear();
            foreach (AIAction order in action_pool.GetAllActive())
                order.Clear();

            data_pool.DisposeAll();
            node_pool.DisposeAll();
            action_pool.DisposeAll();
            list_pool.DisposeAll();

            System.GC.Collect();
        }

        public int GetNbNodesCalculated()
        {
            return nb_calculated;
        }

        public int GetDepthReached()
        {
            return reached_depth;
        }

        public NodeState GetBest()
        {
            return best_move;
        }

        public NodeState GetFirst()
        {
            return first_node;
        }

        public AIAction GetBestAction()
        {
            return best_move != null ? best_move.last_action : null;
        }

        public bool IsBestFound()
        {
            return best_move != null;
        }
    }

    public class NodeState
    {
        public int tdepth;
        public int taction;
        public int sort_min;
        public int hvalue;
        public int alpha;
        public int beta;

        public AIAction last_action = null;
        public int current_player;

        public NodeState parent;
        public NodeState best_child = null;
        public List<NodeState> childs = new List<NodeState>();

        public NodeState() { }

        public NodeState(NodeState parent, int player_id, int turn_depth, int turn_action, int turn_sort)
        {
            this.parent = parent;
            this.current_player = player_id;
            this.tdepth = turn_depth;
            this.taction = turn_action;
            this.sort_min = turn_sort;
        }

        public void Clear()
        {
            last_action = null;
            best_child = null;
            parent = null;
            childs.Clear();
        }
    }

    public class AIAction
    {
        public ushort type;

        public string card_uid;
        public string target_uid;
        public int target_player_id;
        public string ability_id;
        public CardPositionSlot slot;
        public PlayType selectedPlay;
        public int value;

        public int score;
        public int sort;
        public bool valid;

        public AIAction() { }
        public AIAction(ushort t) { type = t; }

        public string GetText(Game data)
        {
            string txt = GameAction.GetString(type);
            Card card = data.GetCard(card_uid);
            Card target = data.GetCard(target_uid);
            if (card != null)
                txt += " card " + card.card_id;
            if (target != null)
                txt += " target " + target.card_id;
            if (slot != CardPositionSlot.None)
                txt += " slot " + slot.posGroupType + "-" + slot.p;
            if (ability_id != null)
                txt += " ability " + ability_id;
            if (type == GameAction.SelectPlay)
                txt += " play " + selectedPlay;
            if (value > 0)
                txt += " value " + value;
            return txt;
        }

        public void Clear()
        {
            type = 0;
            valid = false;
            card_uid = null;
            target_uid = null;
            ability_id = null;
            target_player_id = -1;
            slot = CardPositionSlot.None;
            selectedPlay = PlayType.Huddle;
            value = -1;
            score = 0;
            sort = 0;
        }

        public static AIAction None { get { AIAction a = new AIAction(); a.type = 0; return a; } }
    }
}

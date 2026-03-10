using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.AI
{
    /// <summary>
    /// Football-specific AI heuristic for First & Long.
    /// Heuristic: board state score. High = favors AI, Low = favors opponent.
    /// Action Score: prioritize actions when too many in a single node.
    /// Action Sort: execution order within a turn to prune duplicates.
    /// </summary>

    public class AIHeuristic
    {
        //---------- Heuristic PARAMS -------------

        public int score_value = 500;           // per point of score differential
        public int ball_position_value = 3;     // per yard toward end zone (offense)
        public int down_value = 15;             // bonus per downs remaining
        public int hand_card_value = 3;         // per card in hand
        public int board_card_value = 8;        // per card on board
        public int stamina_value = 2;           // per stamina point on board cards
        public int card_stat_value = 2;         // per point of relevant stat
        public int card_status_value = 15;      // per status effect (multiplied by hvalue)

        //-----------

        private int ai_player_id;
        private int ai_level;
        private int heuristic_modifier;
        private System.Random random_gen;

        public AIHeuristic(int player_id, int level)
        {
            ai_player_id = player_id;
            ai_level = level;
            heuristic_modifier = GetHeuristicModifier();
            random_gen = new System.Random();
        }

        public int CalculateHeuristic(Game data, NodeState node)
        {
            Player aiplayer = data.GetPlayer(ai_player_id);
            Player oplayer = data.GetOpponentPlayer(ai_player_id);
            return CalculateHeuristic(data, node, aiplayer, oplayer);
        }

        public int CalculateHeuristic(Game data, NodeState node, Player aiplayer, Player oplayer)
        {
            int score = 0;
            bool aiIsOffense = data.current_offensive_player != null
                && data.current_offensive_player.player_id == ai_player_id;

            // Win/loss
            if (data.HasEnded())
            {
                if (aiplayer.points > oplayer.points)
                    score += 100000 - node.tdepth * 1000;
                else if (oplayer.points > aiplayer.points)
                    score += -100000 + node.tdepth * 1000;
            }

            // Score differential
            score += (aiplayer.points - oplayer.points) * score_value;

            // Ball position (offense wants high, defense wants low)
            if (aiIsOffense)
                score += data.raw_ball_on * ball_position_value;
            else
                score -= data.raw_ball_on * ball_position_value;

            // Down — more downs remaining = better for offense
            if (aiIsOffense)
                score += (5 - data.current_down) * down_value;
            else
                score -= (5 - data.current_down) * down_value;

            // Hand size
            score += aiplayer.cards_hand.Count * hand_card_value;
            score -= oplayer.cards_hand.Count * hand_card_value;

            // Board cards + stats + stamina
            score += EvaluateBoard(aiplayer, aiIsOffense, 1);
            score += EvaluateBoard(oplayer, !aiIsOffense, -1);

            // Noise for lower-level AI
            if (heuristic_modifier > 0)
                score += random_gen.Next(-heuristic_modifier, heuristic_modifier);

            return score;
        }

        private int EvaluateBoard(Player player, bool isOffense, int sign)
        {
            int val = 0;
            val += player.cards_board.Count * board_card_value * sign;

            foreach (Card card in player.cards_board)
            {
                val += card.current_stamina * stamina_value * sign;

                // Sum relevant stats
                CardData cd = card.CardData;
                if (isOffense)
                    val += (cd.run_bonus + cd.short_pass_bonus + cd.deep_pass_bonus) * card_stat_value * sign;
                else
                    val += (cd.run_coverage_bonus + cd.short_pass_coverage_bonus + cd.deep_pass_coverage_bonus) * card_stat_value * sign;

                // Status effects
                foreach (CardStatus status in card.status)
                    if (status.StatusData != null)
                        val += status.StatusData.hvalue * card_status_value * sign;
                foreach (CardStatus status in card.ongoing_status)
                    if (status.StatusData != null)
                        val += status.StatusData.hvalue * card_status_value * sign;
            }
            return val;
        }

        // Action score — prioritize when too many actions in a node
        public int CalculateActionScore(Game data, AIAction order)
        {
            if (order.type == GameAction.EndTurn || order.type == GameAction.PlayerReadyPhase)
                return 0;

            if (order.type == GameAction.CancelSelect)
                return 0;

            if (order.type == GameAction.SelectPlay)
            {
                // Enhancer actions are more valuable
                return order.card_uid != null ? 200 : 100;
            }

            if (order.type == GameAction.PlayCard)
            {
                Card card = data.GetCard(order.card_uid);
                if (card == null) return 100;

                CardData cd = card.CardData;
                if (cd.IsLiveBall())
                    return 180;

                // Player card — sum best stats
                int statSum = cd.run_bonus + cd.short_pass_bonus + cd.deep_pass_bonus
                    + cd.run_coverage_bonus + cd.short_pass_coverage_bonus + cd.deep_pass_coverage_bonus;
                return 150 + statSum * 3;
            }

            return 100;
        }

        // Sort order for actions within a turn to avoid duplicate paths
        public int CalculateActionSort(Game data, AIAction order)
        {
            if (order.type == GameAction.EndTurn || order.type == GameAction.PlayerReadyPhase)
                return 0;
            if (data.selector != SelectorType.None)
                return 0;

            int type_sort = 0;
            if (order.type == GameAction.PlayCard)
                type_sort = 1;
            if (order.type == GameAction.SelectPlay)
                type_sort = 2;

            Card card = data.GetCard(order.card_uid);
            int card_sort = card != null ? (card.Hash % 100) : 0;
            return type_sort * 10000 + card_sort * 100 + 1;
        }

        private int GetHeuristicModifier()
        {
            if (ai_level >= 10) return 0;
            if (ai_level == 9) return 5;
            if (ai_level == 8) return 10;
            if (ai_level == 7) return 20;
            if (ai_level == 6) return 30;
            if (ai_level == 5) return 40;
            if (ai_level == 4) return 50;
            if (ai_level == 3) return 75;
            if (ai_level == 2) return 100;
            if (ai_level <= 1) return 200;
            return 0;
        }

        public bool IsWin(NodeState node)
        {
            return node.hvalue > 50000 || node.hvalue < -50000;
        }
    }
}

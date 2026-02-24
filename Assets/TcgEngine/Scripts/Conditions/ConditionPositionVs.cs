using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check if a specific position matchup exists
    /// Use case: "If WR is covered by DB", "If QB is being protected by OL"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionPositionVs", menuName = "TcgEngine/Condition/Position Matchup")]
    public class ConditionPositionVs : ConditionData
    {
        public enum MatchupType
        {
            OffensiveVsDefensive,    // Any offensive vs defensive
            SpecificPosition,        // Specific position matchup
            PositionVsPosition,     // e.g., WR vs DB
        }

        public MatchupType matchupType;
        
        [Header("For SpecificPosition")]
        public PlayerPositionGrp offensePosition;
        public PlayerPositionGrp defensePosition;

        [Header("For PositionVsPosition - check if offense has more of position than defense")]
        public PlayerPositionGrp countPosition;

        [Header("Comparison operator")]
        public ConditionOperatorInt oper = ConditionOperatorInt.Greater;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player player = data.GetPlayer(caster.player_id);
            Player opponent = data.GetOpponentPlayer(caster.player_id);

            if (player == null || opponent == null)
                return false;

            switch (matchupType)
            {
                case MatchupType.OffensiveVsDefensive:
                    // Check if opponent has any defensive cards covering
                    return opponent.cards_board.Count > 0;

                case MatchupType.SpecificPosition:
                    // Check if specific matchup exists
                    bool hasOffense = HasPosition(player, offensePosition);
                    bool hasDefense = HasPosition(opponent, defensePosition);
                    return hasOffense && hasDefense;

                case MatchupType.PositionVsPosition:
                    int playerCount = GetPositionCount(player, countPosition);
                    int opponentCount = GetPositionCount(opponent, countPosition);
                    return CompareInt(playerCount, oper, opponentCount);

                default:
                    return false;
            }
        }

        private bool HasPosition(Player p, PlayerPositionGrp pos)
        {
            foreach (Card c in p.cards_board)
            {
                if (c.CardData.playerPosition == pos)
                    return true;
            }
            return false;
        }

        private int GetPositionCount(Player p, PlayerPositionGrp pos)
        {
            int count = 0;
            foreach (Card c in p.cards_board)
            {
                if (c.CardData.playerPosition == pos)
                    count++;
            }
            return count;
        }
    }
}

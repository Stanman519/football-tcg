using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Compare count of specific position between player and opponent
    /// Use case: "If I have more WR than opponent has DB"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionPositionCountDiff", menuName = "TcgEngine/Condition/Position Count Difference")]
    public class ConditionPositionCountDiff : ConditionData
    {
        [Header("Position to count for both player and opponent")]
        public PlayerPositionGrp positionToCount;

        [Header("Difference comparison")]
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int diffThreshold = 0;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player player = data.GetPlayer(caster.player_id);
            Player opponent = data.GetOpponentPlayer(caster.player_id);

            if (player == null || opponent == null)
                return false;

            int playerCount = GetPositionCount(player, positionToCount);
            int opponentCount = GetPositionCount(opponent, positionToCount);

            // diff = player's count - opponent's count
            int diff = playerCount - opponentCount;

            return CompareInt(diff, oper, diffThreshold);
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

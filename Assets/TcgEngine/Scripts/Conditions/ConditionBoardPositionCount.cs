using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;

namespace TcgEngine
{
    /// <summary>
    /// Counts players on the board by position group (OL, DL, LB, DB, etc.)
    /// Useful for abilities like "if you have more OL than DL"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Position/BoardCount", order = 10)]
    public class ConditionBoardPositionCount : ConditionData
    {
        [Header("Board Position Count")]
        public ConditionPlayerType target = ConditionPlayerType.Self;
        public PlayerPositionGrp positionGroup;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value = 1;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            int count = 0;
            
            Player targetPlayer = null;
            
            if (target == ConditionPlayerType.Self)
                targetPlayer = data.GetPlayer(caster.player_id);
            else if (target == ConditionPlayerType.Opponent)
                targetPlayer = data.GetOpponentPlayer(caster.player_id);
            else if (target == ConditionPlayerType.Both)
            {
                // Returns true if BOTH self and opponent meet the condition
                int selfCount = CountPosition(data.GetPlayer(caster.player_id), positionGroup);
                int oppCount = CountPosition(data.GetOpponentPlayer(caster.player_id), positionGroup);
                return CompareInt(selfCount, oper, value) && CompareInt(oppCount, oper, value);
            }
            
            if (targetPlayer != null)
            {
                count = CountPosition(targetPlayer, positionGroup);
            }
            
            return CompareInt(count, oper, value);
        }

        private int CountPosition(Player player, PlayerPositionGrp posGroup)
        {
            if (player == null) return 0;
            
            return player.cards_board.Count(c => 
                c.slot != null && c.slot.posGroupType == posGroup);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }
    }
}

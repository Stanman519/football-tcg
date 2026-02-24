using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;

namespace TcgEngine
{
    /// <summary>
    /// Compares position counts between offense and defense
    /// Example: "If you have more OL than the defense has DL"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Position/CompareTwo", order = 10)]
    public class ConditionCompareTwoBoardPositionCounts : ConditionData
    {
        [Header("Compare Two Position Counts")]
        // The caster's position to count
        public PlayerPositionGrp casterPosition;
        // The target's position to count  
        public PlayerPositionGrp targetPosition;
        
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player offensePlayer = data.GetPlayer(caster.player_id);
            Player defensePlayer = data.GetOpponentPlayer(caster.player_id);
            
            int offenseCount = CountPosition(offensePlayer, casterPosition);
            int defenseCount = CountPosition(defensePlayer, targetPosition);
            
            return CompareInt(offenseCount, oper, defenseCount);
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

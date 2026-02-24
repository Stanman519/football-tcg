using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks the number of star players on the board
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/StarCount", order = 10)]
    public class ConditionStarCount : ConditionData
    {
        [Header("Star Count Check")]
        public ConditionPlayerType target = ConditionPlayerType.Self;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value = 1;
        
        // Optional: check specific position group
        public PlayerPositionGrp positionFilter = PlayerPositionGrp.NONE;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            int starCount = 0;
            
            Player targetPlayer = null;
            if (target == ConditionPlayerType.Self || target == ConditionPlayerType.Both)
                targetPlayer = data.GetPlayer(caster.player_id);
            if (target == ConditionPlayerType.Opponent || target == ConditionPlayerType.Both)
                targetPlayer = data.GetOpponentPlayer(caster.player_id);
                
            if (targetPlayer != null)
            {
                starCount = CountStars(targetPlayer, positionFilter);
            }
            
            return CompareInt(starCount, oper, value);
        }

        private int CountStars(Player player, PlayerPositionGrp filter)
        {
            int count = 0;
            foreach (Card card in player.cards_board)
            {
                if (card.CardData.isSuperstar)
                {
                    if (filter == PlayerPositionGrp.NONE || 
                        (card.slot != null && card.slot.posGroupType == filter))
                    {
                        count++;
                    }
                }
            }
            return count;
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

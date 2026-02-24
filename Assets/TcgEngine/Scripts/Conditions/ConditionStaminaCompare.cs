using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;

namespace TcgEngine
{
    /// <summary>
    /// Compares total stamina between players
    /// "If I have more stamina than opponent" or "If stamina > X"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Player/StaminaCompare", order = 10)]
    public class ConditionStaminaCompare : ConditionData
    {
        [Header("Stamina Comparison")]
        // Self = compare to opponent, or absolute value
        public bool compareToOpponent = true;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value = 0;
        
        // If true, compare total team stamina. If false, compare individual card stamina (caster only)
        public bool teamStamina = true;
        
        // Filter by position group
        public PlayerPositionGrp positionFilter = PlayerPositionGrp.NONE;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player casterPlayer = data.GetPlayer(caster.player_id);
            Player opponentPlayer = data.GetOpponentPlayer(caster.player_id);
            
            int casterStamina = GetStamina(casterPlayer, positionFilter);
            
            if (compareToOpponent && opponentPlayer != null)
            {
                int opponentStamina = GetStamina(opponentPlayer, positionFilter);
                return CompareInt(casterStamina, oper, opponentStamina + value);
            }
            else
            {
                return CompareInt(casterStamina, oper, value);
            }
        }

        private int GetStamina(Player player, PlayerPositionGrp filter)
        {
            if (player == null) return 0;
            
            if (filter == PlayerPositionGrp.NONE)
            {
                // Total team stamina
                return player.cards_board.Sum(c => c.current_stamina + c.GetStatusValue(StatusType.AddStamina));
            }
            
            // Filter by position
            return player.cards_board
                .Where(c => c.slot != null && c.slot.posGroupType == filter)
                .Sum(c => c.current_stamina + c.GetStatusValue(StatusType.AddStamina));
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

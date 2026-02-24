using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;

namespace TcgEngine
{
    /// <summary>
    /// Compares grit between offense and defense
    /// "If defense grit > offense grit" or "If my grit > opponent grit"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Player/GritCompare", order = 10)]
    public class ConditionGritCompare : ConditionData
    {
        [Header("Grit Comparison")]
        // Compare: Self (offense vs defense) or Defense vs Offense
        public bool compareToOpponent = true;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value = 0;
        
        // Optional: filter by position group (DL, LB, DB)
        public PlayerPositionGrp positionFilter = PlayerPositionGrp.NONE;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player offense = data.current_offensive_player;
            Player defense = data.GetOpponentPlayer(offense.player_id);
            
            int offenseGrit = GetTeamGrit(offense, positionFilter);
            int defenseGrit = GetTeamGrit(defense, positionFilter);
            
            if (compareToOpponent)
            {
                // Compare defense to offense (defense wants > offense)
                return CompareInt(defenseGrit, oper, offenseGrit + value);
            }
            else
            {
                // Compare offense to defense
                return CompareInt(offenseGrit, oper, defenseGrit + value);
            }
        }

        private int GetTeamGrit(Player player, PlayerPositionGrp filter)
        {
            if (player == null) return 0;
            
            if (filter == PlayerPositionGrp.NONE)
            {
                return player.GetCurrentBoardCardGrit();
            }
            
            // Filter by position group
            return player.cards_board
                .Where(c => c.slot != null && c.slot.posGroupType == filter)
                .Sum(c => c.Data.grit + c.GetStatusValue(StatusType.AddGrit));
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

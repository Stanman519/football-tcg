using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks position matchup - specific offensive position vs defensive position
    /// "If WR is covered by DB" or "If OL is matched against DL"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Position/Matchup", order = 10)]
    public class ConditionPositionMatchup : ConditionData
    {
        [Header("Position Matchup")]
        public PlayerPositionGrp offensePosition;
        public PlayerPositionGrp defensePosition;
        
        // If true, checks if ANY card of offensePosition is being blocked by defensePosition
        // If false, checks if a specific matchup exists
        public bool anyMatchup = true;
        
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value = 1; // Number of matchups needed

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player offense = data.current_offensive_player;
            Player defense = data.GetOpponentPlayer(offense.player_id);
            
            int matchupCount = CountMatchups(offense, defense);
            return CompareInt(matchupCount, oper, value);
        }

        private int CountMatchups(Player offense, Player defense)
        {
            // This is simplified - in reality you'd check the specific matchup system
            // For now, count if both positions exist on field
            int offenseCount = 0;
            int defenseCount = 0;
            
            foreach (Card c in offense.cards_board)
            {
                if (c.slot != null && c.slot.posGroupType == offensePosition)
                    offenseCount++;
            }
            
            foreach (Card c in defense.cards_board)
            {
                if (c.slot != null && c.slot.posGroupType == defensePosition)
                    defenseCount++;
            }
            
            if (anyMatchup)
            {
                return System.Math.Min(offenseCount, defenseCount);
            }
            
            // Exact match
            return offenseCount == defenseCount ? offenseCount : 0;
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

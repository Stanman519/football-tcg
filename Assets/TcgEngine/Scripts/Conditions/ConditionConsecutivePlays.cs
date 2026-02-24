using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks consecutive play count (for cards that trigger on consecutive runs/passes)
    /// Uses PlayHistory system to track consecutive plays
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/ConsecutivePlays", order = 10)]
    public class ConditionConsecutivePlays : ConditionData
    {
        [Header("Consecutive Play Check")]
        public PlayType playType;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int requiredCount = 2;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            if (data.play_history == null || data.play_history.Count == 0)
                return false;
            
            // Count consecutive plays of the specified type working backwards
            int consecutiveCount = 0;
            
            // Start from the most recent play and work backwards
            for (int i = data.play_history.Count - 1; i >= 0; i--)
            {
                PlayHistory play = data.play_history[i];
                
                // Only count plays from current half
                if (play.current_half != data.current_half)
                    break;
                
                if (play.offensive_play == playType)
                    consecutiveCount++;
                else
                    break; // Stop at first different play
            }
            
            return CompareInt(consecutiveCount, oper, requiredCount);
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

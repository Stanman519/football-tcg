using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks if this is the first snap of the game
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/FirstSnap", order = 10)]
    public class ConditionFirstSnap : ConditionData
    {
        [Header("First Snap Check")]
        public ConditionOperatorBool oper = ConditionOperatorBool.IsTrue;
        
        // Can also check if it's first snap of half
        public bool firstSnapOfHalf = false;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            bool isFirstSnap;
            
            if (firstSnapOfHalf)
            {
                // Check if this is first play of the half
                isFirstSnap = data.plays_left_in_half >= 11; // Full half = 11 plays
            }
            else
            {
                // Check if this is first snap of the game (turn_count = 1 and first down)
                isFirstSnap = data.turn_count == 1 && data.current_down == 1;
            }
            
            return CompareBool(isFirstSnap, oper);
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

using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks the current down (1st, 2nd, 3rd, 4th)
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/Down", order = 10)]
    public class ConditionGameDown : ConditionData
    {
        [Header("Required Down")]
        public int required_down = 1;
        public ConditionOperatorInt oper = ConditionOperatorInt.Equal;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            int currentDown = data.current_down;
            return CompareInt(currentDown, oper, required_down);
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

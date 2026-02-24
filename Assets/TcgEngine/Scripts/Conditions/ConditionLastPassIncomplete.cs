using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks if the last pass was incomplete
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/LastPassIncomplete", order = 10)]
    public class ConditionLastPassIncomplete : ConditionData
    {
        [Header("Last Pass Check")]
        public ConditionOperatorBool oper = ConditionOperatorBool.IsTrue;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            // Check if the last play was an incomplete pass
            // This would need game state tracking - assume last play type was pass and resulted in 0 yards
            bool wasIncomplete = data.yardage_this_play == 0 && 
                                 data.GetPlayer(caster.player_id).SelectedPlay == PlayType.LongPass ||
                                 data.GetPlayer(caster.player_id).SelectedPlay == PlayType.ShortPass;
            
            return CompareBool(wasIncomplete, oper);
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

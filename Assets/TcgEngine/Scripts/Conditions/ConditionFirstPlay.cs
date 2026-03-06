using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Conditions
{
    /// <summary>
    /// True on the first play a card participates in after being placed on the field.
    /// Use as a trigger condition on OnPlay or OnRunResolution/OnPassResolution abilities.
    /// </summary>
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/GamePhase/FirstPlay", order = 10)]
    public class ConditionFirstPlay : ConditionData
    {
        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return caster.on_field_history.Count == 0;
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

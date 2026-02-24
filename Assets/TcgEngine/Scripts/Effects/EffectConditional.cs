using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine.Effects
{
    /// <summary>
    /// Apply different effects based on a condition
    /// Use case: "If star player, +3 stamina, else +1"
    /// </summary>
    [CreateAssetMenu(fileName = "EffectConditional", menuName = "TcgEngine/Effect/Conditional Effect")]
    public class EffectConditional : EffectData
    {
        [Header("Condition to check")]
        public ConditionData condition;

        [Header("Effect if condition is TRUE")]
        public EffectData effectIfTrue;

        [Header("Effect if condition is FALSE")]
        public EffectData effectIfFalse;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            // Check trigger condition (no target)
            bool met = condition.IsTriggerConditionMet(logic.GetGameData(), ability, caster);

            if (met && effectIfTrue != null)
            {
                effectIfTrue.DoEffect(logic, ability, caster);
            }
            else if (!met && effectIfFalse != null)
            {
                effectIfFalse.DoEffect(logic, ability, caster);
            }
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            // Check target condition
            bool met = condition.IsTargetConditionMet(logic.GetGameData(), ability, caster, target);

            if (met && effectIfTrue != null)
            {
                effectIfTrue.DoEffect(logic, ability, caster, target);
            }
            else if (!met && effectIfFalse != null)
            {
                effectIfFalse.DoEffect(logic, ability, caster, target);
            }
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            // Check target condition for player
            bool met = condition.IsTargetConditionMet(logic.GetGameData(), ability, caster, target);

            if (met && effectIfTrue != null)
            {
                effectIfTrue.DoEffect(logic, ability, caster, target);
            }
            else if (!met && effectIfFalse != null)
            {
                effectIfFalse.DoEffect(logic, ability, caster, target);
            }
        }
    }
}

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Conditions
{
    /// <summary>
    /// Passes with the given percentage chance (0-100).
    /// Use for "X% chance to..." abilities.
    /// NOTE: evaluated once per ability resolution — all targets see the same roll.
    /// </summary>
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Random")]
    public class ConditionRandom : ConditionData
    {
        [Range(0, 100)]
        public int chance = 50; // Percent chance this condition passes (0 = never, 100 = always)

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return Random.value * 100f < chance;
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

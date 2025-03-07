using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Effect that adds card/player custom stats or traits
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/AddTrait", order = 10)]
    public class EffectAddTrait : EffectData
    {
        public TraitData trait;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            target.AddTrait(trait.id, ability.value);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            target.AddTrait(trait.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            target.AddOngoingTrait(trait.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            target.AddOngoingTrait(trait.id, ability.value);
        }
    }
}
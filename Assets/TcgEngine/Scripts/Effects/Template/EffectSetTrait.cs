using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that sets custom stats to a specific value
    /// </summary>
    
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/SetStatCustom", order = 10)]
    public class EffectSetTrait : EffectData
    {
        public TraitData trait;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            target.SetTrait(trait.id, ability.value);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            target.SetTrait(trait.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            target.SetTrait(trait.id, ability.value);
        }

        public override void DoOngoingEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            target.SetTrait(trait.id, ability.value);
        }
    }
}
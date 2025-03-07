using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Effect that adds an ability to a card
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/AddAbility", order = 10)]
    public class EffectAddAbility : EffectData
    {
        public AbilityData gain_ability;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            target.AddAbility(gain_ability);
        }

        public override void DoOngoingEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            target.AddOngoingAbility(gain_ability);
        }
    }
}
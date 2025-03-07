using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Effect to discard cards from hand
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Discard", order = 10)]
    public class EffectDiscard : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            logic.DrawDiscardCard(target, ability.value); //Discard first card of deck
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            logic.DiscardCard(target);
        }

    }
}
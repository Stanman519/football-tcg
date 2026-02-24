using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine.Effects
{
    /// <summary>
    /// Add grit/durability boost to a card (card stats improvement)
    /// This adds to card's hp as a temporary boost
    /// </summary>
    [CreateAssetMenu(fileName = "EffectAddGrit", menuName = "TcgEngine/Effect/Add Grit")]
    public class EffectAddGrit : EffectData
    {
        [Header("HP/Durability boost amount")]
        public int gritAmount = 1;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            if (target == null)
                return;

            // Add to card's hp as a stat boost (can be persistent or ongoing depending on implementation)
            target.hp += gritAmount;
            Debug.Log($"Added {gritAmount} grit to {target.card_id}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            if (caster == null)
                return;

            caster.hp += gritAmount;
            Debug.Log($"Added {gritAmount} grit to caster {caster.card_id}");
        }
    }
}

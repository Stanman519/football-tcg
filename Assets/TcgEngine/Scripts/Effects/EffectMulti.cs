using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Effects
{
    /// <summary>
    /// Container that applies multiple effects in sequence
    /// Use case: "Draw 2 AND +2 run bonus"
    /// </summary>
    [CreateAssetMenu(fileName = "EffectMulti", menuName = "TcgEngine/Effect/Multiple Effects")]
    public class EffectMulti : EffectData
    {
        [Header("Effects to apply in order")]
        public List<EffectData> effects;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            if (effects == null || effects.Count == 0)
                return;

            foreach (EffectData effect in effects)
            {
                if (effect != null)
                {
                    effect.DoEffect(logic, ability, caster);
                }
            }
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            if (effects == null || effects.Count == 0)
                return;

            foreach (EffectData effect in effects)
            {
                if (effect != null)
                {
                    effect.DoEffect(logic, ability, caster, target);
                }
            }
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            if (effects == null || effects.Count == 0)
                return;

            foreach (EffectData effect in effects)
            {
                if (effect != null)
                {
                    effect.DoEffect(logic, ability, caster, target);
                }
            }
        }
    }


}

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// In the Zone — makes the caster's side immune to all opponent live ball effects.
    /// Detected via HasEffect&lt;EffectImmunity&gt;() in ResolveLiveBallEffects.
    /// Blocks yardage mods, fumbles, silence, stamina drain — everything.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectImmunity", menuName = "TcgEngine/Effect/Immunity", order = 10)]
    public class EffectImmunity : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Debug.Log($"[LiveBall] In the Zone — immune to opponent effects via {caster?.card_id}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}

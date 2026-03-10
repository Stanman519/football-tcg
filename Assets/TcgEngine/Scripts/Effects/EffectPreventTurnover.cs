using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Ball Security — auto-prevents a forced fumble during live ball resolution.
    /// Detected via HasEffect&lt;EffectPreventTurnover&gt;() in ResolveLiveBallEffects.
    /// When active, fumble is denied regardless of grit comparison.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectPreventTurnover", menuName = "TcgEngine/Effect/PreventTurnover", order = 10)]
    public class EffectPreventTurnover : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Debug.Log($"[LiveBall] Ball Security active — fumble prevented by {caster?.card_id}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}

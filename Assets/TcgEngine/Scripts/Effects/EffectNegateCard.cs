using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Blanket Coverage — negates the opponent's live ball card entirely.
    /// Detected via HasEffect&lt;EffectNegateCard&gt;() in ResolveLiveBallEffects.
    /// Cancels everything including fumble attempts.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectNegateCard", menuName = "TcgEngine/Effect/NegateCard", order = 10)]
    public class EffectNegateCard : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Debug.Log($"[LiveBall] Blanket Coverage — opponent's live ball card negated by {caster?.card_id}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}

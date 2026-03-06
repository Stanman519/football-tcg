using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Removes a target card from the board (sends to discard).
    /// Respects Invincibility status. Fires OnKill/OnDeath triggers.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectKnockout", menuName = "TcgEngine/Effect/Knockout", order = 10)]
    public class EffectKnockout : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            if (target == null)
                return;

            logic.KillCard(caster, target);
            Debug.Log($"[Knockout] {caster?.card_id} knocked out {target.card_id}");
        }
    }
}

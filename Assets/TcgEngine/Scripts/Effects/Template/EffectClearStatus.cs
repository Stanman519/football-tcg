using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that removes a status,
    /// Will remove all status if the public field is empty
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/ClearStatus", order = 10)]
    public class EffectClearStatus : EffectData
    {
        public StatusData status;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            if (status != null)
                target.RemoveStatus(status.effect);
            else
                target.status.Clear();
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            if (status != null)
                target.RemoveStatus(status.effect);
            else
                target.status.Clear();
        }
    }
}
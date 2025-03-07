using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Effect to draw cards
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Draw", order = 10)]
    public class EffectDraw : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            logic.DrawCard(target, ability.value);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            Player player = logic.GameData.GetPlayer(target.player_id);
            logic.DrawCard(player, ability.value);
        }

    }
}
using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Draws cards for the caster's player (or an explicitly targeted player).
    /// </summary>
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/DrawCard")]
    public class EffectDrawCard : EffectData
    {
        public int count = 1;

        // When caster is the source (most common: "draw 1 card")
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            if (caster == null) return;
            Player player = logic.GetGameData().GetPlayer(caster.player_id);
            if (player != null)
                logic.DrawCard(player, count);
        }

        // When a specific card is targeted — draw for that card's owner
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }

        // When a player is explicitly targeted
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            if (target != null)
                logic.DrawCard(target, count);
        }
    }
}

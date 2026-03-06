using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Modifies yardage_this_play during live ball resolution.
    /// Positive value = gain yards (juke, spin move, stiff arm).
    /// Negative value = lose yards (tackle, shoestring).
    ///
    /// Uses ability.value as the yard modifier.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectYardageModifier", menuName = "TcgEngine/Effect/YardageModifier", order = 10)]
    public class EffectYardageModifier : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            game.yardage_this_play += ability.value;
            Debug.Log($"[LiveBall] Yardage modifier {ability.value:+#;-#;0} from {caster?.card_id} → new total: {game.yardage_this_play}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}

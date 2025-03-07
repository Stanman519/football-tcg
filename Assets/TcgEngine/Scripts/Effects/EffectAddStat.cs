
using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    public class EffectAddStat : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            if ()
            {
                target.hp += ability.value;
                target.hp_max += ability.value;
            }

        }
    }
}

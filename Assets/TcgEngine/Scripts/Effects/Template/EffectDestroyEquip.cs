using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/DestroyEquip", order = 10)]
    public class EffectDestroyEquip : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            if (target.CardData.IsEquipment())
            {
                logic.DiscardCard(target);
            }
            else
            {
                Card etarget = logic.GameData.GetCard(target.equipped_uid);
                logic.DiscardCard(etarget);
            }
        }

    }
}
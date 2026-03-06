using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Returns the caster card from the board back to its owner's hand.
    /// Used for cards that "sit out" on specific downs (e.g. Deion McCall).
    ///
    /// Trigger with StartOfTurn + ConditionGameDown to auto-return each time that down arrives.
    /// </summary>
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/ReturnToHand")]
    public class EffectReturnToHand : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            if (caster == null) return;
            Game data = logic.GetGameData();
            Player player = data.GetPlayer(caster.player_id);
            if (player == null) return;

            if (!data.IsOnBoard(caster))
                return; // Already not on board

            player.RemoveCardFromAllGroups(caster);
            player.cards_hand.Add(caster);
            Debug.Log($"[ReturnToHand] {caster.card_id} returned to hand for player {caster.player_id}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}

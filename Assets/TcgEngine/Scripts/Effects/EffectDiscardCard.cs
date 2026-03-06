using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Forces a player to discard a random card from their hand automatically (no player choice).
    /// Intended for triggered opponent-discard effects such as Nash Thornton
    /// ("opponent discards when you throw an incomplete pass").
    ///
    /// discardFromOpponent = true  → the caster's OPPONENT loses a random hand card
    /// discardFromOpponent = false → the caster's OWN player loses a random hand card
    ///
    /// Importer shorthand (effect_val):
    ///   "Opponent"  → discardFromOpponent = true  (most common)
    ///   "Self"      → discardFromOpponent = false
    /// </summary>
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/DiscardCard")]
    public class EffectDiscardCard : EffectData
    {
        [Tooltip("If true, the caster's opponent discards; if false, the caster's own player discards.")]
        public bool discardFromOpponent = true;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            if (caster == null) return;
            Game data = logic.GetGameData();

            Player target = discardFromOpponent
                ? data.GetOpponentPlayer(caster.player_id)
                : data.GetPlayer(caster.player_id);

            if (target == null || target.cards_hand.Count == 0)
                return;

            int idx = UnityEngine.Random.Range(0, target.cards_hand.Count);
            Card card = target.cards_hand[idx];
            logic.DiscardCard(card);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            // This overload fires when the player chose a specific card (CardSelectorHand flow).
            // Discard the chosen card, then optionally apply a stat bonus to the caster.
            if (target != null)
                logic.DiscardCard(target);

            if (ability.affected_stat != StatusTypePrintedStats.None)
            {
                StatusType statType = (StatusType)(int)ability.affected_stat;
                caster?.AddStatus(statType, ability.stat_bonus_amount, ability.duration);
            }
        }
    }
}

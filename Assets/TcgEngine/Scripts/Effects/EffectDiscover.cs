using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Discover mechanic: pulls random cards matching a filter from the player's deck,
    /// shows them in a CardSelector UI, player picks 1 → goes to hand, rest shuffled back to deck.
    ///
    /// The actual pulling of cards from deck to cards_temp happens in
    /// GameLogicService.PopulateDiscoverCards() BEFORE the selector opens.
    /// This effect handles what happens AFTER the player picks a card.
    ///
    /// Used by Film Study (play enhancer): "Draw 3 play enhancers from your deck, keep 1."
    ///
    /// Importer shorthand (effect_val):
    ///   "OffensivePlayEnhancer|3"  → filterType=OffensivePlayEnhancer, drawCount=3
    ///   "DefensivePlayer|2"       → filterType=DefensivePlayer, drawCount=2
    /// </summary>
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Discover")]
    public class EffectDiscover : EffectData
    {
        [Tooltip("Only show cards of this type. None = any card type.")]
        public CardType filterType = CardType.OffensivePlayEnhancer;

        [Tooltip("How many random matching cards to pull from the deck.")]
        public int drawCount = 3;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            // No-target overload: nothing to do here.
            // PopulateDiscoverCards already pulled cards to temp before the selector opened.
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            // Player picked a card from the discover selector.
            // Move the chosen card to hand, return the rest to deck.
            Game data = logic.GetGameData();
            Player player = data.GetPlayer(caster.player_id);
            if (player == null || target == null) return;

            // Move chosen card to hand
            player.cards_temp.Remove(target);
            player.cards_hand.Add(target);
            Debug.Log($"[Discover] Player picked {target.card_id} → hand");

            // Return remaining temp cards to deck
            foreach (Card remaining in player.cards_temp.ToArray())
            {
                player.cards_temp.Remove(remaining);
                player.cards_deck.Add(remaining);
            }

            // Shuffle the deck
            for (int i = player.cards_deck.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (player.cards_deck[i], player.cards_deck[j]) = (player.cards_deck[j], player.cards_deck[i]);
            }
        }
    }
}

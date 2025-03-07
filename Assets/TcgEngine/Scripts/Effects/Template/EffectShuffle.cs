using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Shuffle Deck
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Shuffle", order = 10)]
    public class EffectShuffle : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            logic.ShuffleDeck(target.cards_deck);
        }
    }
}
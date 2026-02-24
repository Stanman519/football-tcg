using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks hand size or deck/discard pile count
    /// "If hand has 5+ cards" or "If deck is empty"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/PileCount", order = 10)]
    public class ConditionPileCount : ConditionData
    {
        [Header("Pile Count Check")]
        public PileType pile;
        public ConditionPlayerType target = ConditionPlayerType.Self;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value = 3;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player targetPlayer = null;
            
            if (target == ConditionPlayerType.Self)
                targetPlayer = data.GetPlayer(caster.player_id);
            else if (target == ConditionPlayerType.Opponent)
                targetPlayer = data.GetOpponentPlayer(caster.player_id);
            else if (target == ConditionPlayerType.Both)
            {
                // Returns true if BOTH meet the condition
                Player self = data.GetPlayer(caster.player_id);
                Player opp = data.GetOpponentPlayer(caster.player_id);
                return CompareInt(CountPile(self), oper, value) && 
                       CompareInt(CountPile(opp), oper, value);
            }
            
            if (targetPlayer == null) return false;
            
            int count = CountPile(targetPlayer);
            return CompareInt(count, oper, value);
        }

        private int CountPile(Player player)
        {
            switch (pile)
            {
                case PileType.Hand:
                    return player.cards_hand.Count;
                case PileType.Deck:
                    return player.cards_deck.Count;
                case PileType.Discard:
                    return player.cards_discard.Count;
                case PileType.Board:
                    return player.cards_board.Count;
                default:
                    return 0;
            }
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }
    }
}

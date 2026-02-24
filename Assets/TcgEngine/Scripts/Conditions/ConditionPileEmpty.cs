using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks if hand (or other pile) is empty
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/PileEmpty", order = 10)]
    public class ConditionPileEmpty : ConditionData
    {
        [Header("Pile Empty Check")]
        public ConditionPlayerType target = ConditionPlayerType.Self;
        public PileType pile = PileType.Hand;
        public ConditionOperatorBool oper = ConditionOperatorBool.IsTrue;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player player = null;
            
            if (target == ConditionPlayerType.Self)
                player = data.GetPlayer(caster.player_id);
            else if (target == ConditionPlayerType.Opponent)
                player = data.GetOpponentPlayer(caster.player_id);
                
            if (player == null) return false;
            
            int count = GetPileCount(player);
            bool isEmpty = count == 0;
            
            return CompareBool(isEmpty, oper);
        }

        private int GetPileCount(Player player)
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

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Checks the number of cards in the caster's hand.
    /// Examples:
    ///   oper=Equal,    required_count=0  → "if hand is empty"
    ///   oper=GreaterEqualThan, required_count=6 → "if you have 6 or more cards in hand"
    ///   oper=LessEqualThan,   required_count=3  → "if you have 3 or fewer cards in hand"
    /// </summary>
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Player/HandCount", order = 10)]
    public class ConditionHandCount : ConditionData
    {
        [Header("Hand Count Check")]
        public int required_count = 0;
        public ConditionOperatorInt oper = ConditionOperatorInt.Equal;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player player = data.GetPlayer(caster.player_id);
            return CompareInt(player.cards_hand.Count, oper, required_count);
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

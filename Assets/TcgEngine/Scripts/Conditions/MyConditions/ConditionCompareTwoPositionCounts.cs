using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Conditions
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Position/CompareBoardPositionCounts", order = 10)]
    public class ConditionCompareTwoBoardPositionCounts : ConditionData
    {

        public PlayerPositionGrp casterType;
        public PlayerPositionGrp targetType;
        public ConditionOperatorInt oper;
        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Player caster, Player target)
        {
            return CompareInt(caster.cards_board.Where(c => c.CardData.playerPosition == casterType).Count(), oper, target.cards_board.Where(c => c.CardData.playerPosition == targetType).Count());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TcgEngine;
using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;
namespace Assets.TcgEngine.Scripts.Conditions
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Position/Compare", order = 10)]
    public class ConditionPositionTypeCompare : ConditionData
    {
        public CardPositionSlot compare_postion;
        public ConditionOperatorInt oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, CardPositionSlot position)
        {
            return CompareInt( data.GetPlayer(position.p).cards_board.Where(c => c.slot == position).Count(), oper, data.GetPlayer(compare_postion.p).cards_board.Where(c => c.slot == compare_postion).Count());
        }
    }
}

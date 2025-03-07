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
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Position/Quantity", order = 10)]
    public class ConditionPositionTypeQuantity : ConditionData
    {
        public int target_amount;
        public ConditionOperatorInt oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, CardPositionSlot position)
        {
            return CompareInt(data.GetPlayer(position.p).cards_board.Where(c => c.slot == position).Count(), oper, target_amount);
        }
    }

}

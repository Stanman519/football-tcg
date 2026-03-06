using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Conditions
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Slot/IconCount", order = 10)]
    public class ConditionSlotIconCountTrigger : ConditionData
    {
        public int required_icon_count;
        public SlotMachineIconType icon_type;
        public ConditionOperatorInt oper;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return CompareInt(data.GetSlotIconCount(icon_type), oper, required_icon_count);
        }
    }
}

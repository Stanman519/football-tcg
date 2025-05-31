using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;
namespace Assets.TcgEngine.Scripts.Conditions
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/GamePhase/Down", order = 10)]
    public class ConditionGameDown : ConditionData
    {
        public int required_down;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return CompareInt(data.current_down, ConditionOperatorInt.Equal, required_down);
        }
    }
}


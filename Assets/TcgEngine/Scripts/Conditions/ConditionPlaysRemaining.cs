using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check how many plays remain in the current half
    /// Use case: "Last play of half", "2-minute drill"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionPlaysRemaining", menuName = "TcgEngine/Condition/Plays Remaining")]
    public class ConditionPlaysRemaining : ConditionData
    {
        [Header("Plays remaining threshold")]
        public ConditionOperatorInt oper = ConditionOperatorInt.LessEqual;
        public int playsThreshold = 3;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            // plays_left_in_half is tracked in Game.cs
            int remaining = data.plays_left_in_half;
            return CompareInt(remaining, oper, playsThreshold);
        }
    }
}

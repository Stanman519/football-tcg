using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check time remaining in the half
    /// Use case: "2-minute warning", "end of half", "last 2 plays of half"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionTimeRemaining", menuName = "TcgEngine/Condition/Time Remaining")]
    public class ConditionTimeRemaining : ConditionData
    {
        public enum TimeCheck
        {
            LessThan,      // Time < X seconds
            GreaterThan,   // Time > X seconds  
            LastNPlays,    // Within last N plays of half
        }

        public TimeCheck checkType;
        public int value; // seconds for LessThan/GreaterThan, play count for LastNPlays

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            switch (checkType)
            {
                case TimeCheck.LessThan:
                    // Check if turn_timer is less than threshold
                    return data.turn_timer < value;

                case TimeCheck.GreaterThan:
                    // Check if turn_timer is greater than threshold
                    return data.turn_timer > value;

                case TimeCheck.LastNPlays:
                    // Check if we're within last N plays of half
                    return data.plays_left_in_half <= value;

                default:
                    return false;
            }
        }
    }
}

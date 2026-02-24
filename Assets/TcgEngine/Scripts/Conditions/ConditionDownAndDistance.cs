using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check if current down & distance meets criteria
    /// Use case: "3rd & short", "4th & goal", "3rd & long"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionDownAndDistance", menuName = "TcgEngine/Condition/Down & Distance")]
    public class ConditionDownAndDistance : ConditionData
    {
        public enum DistanceType
        {
            Exactly,      // Exact yards to go
            AtLeast,      // X or more yards
            AtMost,       // X or fewer yards
            LessThan,     // Fewer than X yards
            MoreThan,     // More than X yards
        }

        [Header("Down (1-4)")]
        public int down = 1;

        [Header("Yards to go")]
        public DistanceType distanceType = DistanceType.AtLeast;
        public int yardsToGo = 3;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            // Check down
            if (data.current_down != down)
                return false;

            // Check distance
            int actualYards = data.yardage_to_go;
            
            switch (distanceType)
            {
                case DistanceType.Exactly:
                    return actualYards == yardsToGo;
                case DistanceType.AtLeast:
                    return actualYards >= yardsToGo;
                case DistanceType.AtMost:
                    return actualYards <= yardsToGo;
                case DistanceType.LessThan:
                    return actualYards < yardsToGo;
                case DistanceType.MoreThan:
                    return actualYards > yardsToGo;
                default:
                    return false;
            }
        }
    }
}

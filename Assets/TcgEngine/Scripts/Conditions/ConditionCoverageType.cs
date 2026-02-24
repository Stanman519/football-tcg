using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check coverage type - man or zone
    /// Use case: "Better vs man coverage", "Bonus when opponent in zone"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionCoverageType", menuName = "TcgEngine/Condition/Coverage Type")]
    public class ConditionCoverageType : ConditionData
    {
        public enum CoverageType
        {
            ManCoverage,        // Opponent is playing man
            ZoneCoverage,       // Opponent is playing zone
            Any,                // Doesn't matter
        }

        [Header("Coverage type to check")]
        public CoverageType coverageCheck = CoverageType.ManCoverage;

        [Header("Is this for the attacking player or defending?")]
        public bool isAttackingPlayer = true;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            // Would need to track current coverage type in game state
            // For now, return false as we don't have this tracked

            // This would need a new field in Game.cs like:
            // public bool isOpponentInManCoverage;

            Debug.LogWarning("ConditionCoverageType: coverage tracking not yet implemented");
            return false;
        }
    }
}

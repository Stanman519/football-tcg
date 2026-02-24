using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check specific field position scenarios
    /// Use case: "Backed up (own 10 or less)", "Goal line (opp 10+)", " opponents 40"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionFieldPositionScenario", menuName = "TcgEngine/Condition/Field Position Scenario")]
    public class ConditionFieldPositionScenario : ConditionData
    {
        public enum PositionScenario
        {
            BackedUp,           // Own 10 or less (very deep)
            OwnTerritory,       // Own 1-50
            OpponentTerritory,  // Opponent's side (50-100)
            RedZone,            // Inside 20
            GoalLine,           // Inside 5
            Opponent40,         // Opponent's 40 or closer
            midfield,           // Around 50
            Within10,           // Within 10 yards of LOS
        }

        [Header("Field position scenario")]
        public PositionScenario scenario = PositionScenario.RedZone;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            int ballOn = data.raw_ball_on; // 0-100, 0 = own endzone, 100 = opponent endzone

            switch (scenario)
            {
                case PositionScenario.BackedUp:
                    return ballOn <= 10;

                case PositionScenario.OwnTerritory:
                    return ballOn < 50;

                case PositionScenario.OpponentTerritory:
                    return ballOn >= 50;

                case PositionScenario.RedZone:
                    return ballOn >= 80; // Inside opponent's 20

                case PositionScenario.GoalLine:
                    return ballOn >= 95; // Inside 5

                case PositionScenario.Opponent40:
                    return ballOn >= 60; // Past midfield, inside 40

                case PositionScenario.midfield:
                    return ballOn >= 45 && ballOn <= 55;

                case PositionScenario.Within10:
                    // Would need LOS tracking - placeholder
                    return ballOn >= 0; // Simplified

                default:
                    return false;
            }
        }
    }
}

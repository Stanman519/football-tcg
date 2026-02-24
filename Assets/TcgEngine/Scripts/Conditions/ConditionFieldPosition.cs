using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks field position - red zone, opponent territory, etc.
    /// Uses raw_ball_on: 0 = own endzone, 100 = opponent endzone
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/FieldPosition", order = 10)]
    public class ConditionFieldPosition : ConditionData
    {
        [Header("Field Position Check")]
        public FieldPositionCheck positionType;
        
        // For custom yard line checks
        public ConditionOperatorInt oper = ConditionOperatorInt.Equal;
        public int yardLine = 20;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            int ballOn = data.raw_ball_on;
            
            switch (positionType)
            {
                case FieldPositionCheck.InRedZone:
                    // Red zone: inside opponent's 20 (ball_on >= 80)
                    return ballOn >= 80;
                    
                case FieldPositionCheck.InOpponentTerritory:
                    // Past 50 yard line (ball_on >= 50)
                    return ballOn >= 50;
                    
                case FieldPositionCheck.InOwnTerritory:
                    // Own side of the field (ball_on < 50)
                    return ballOn < 50;
                    
                case FieldPositionCheck.GoalToGo:
                    // Inside 10 yard line (ball_on >= 90)
                    return ballOn >= 90;
                    
                case FieldPositionCheck.BackedUp:
                    // Own side, inside own 20 (ball_on <= 20)
                    return ballOn <= 20;
                    
                case FieldPositionCheck.Custom:
                    return CompareInt(ballOn, oper, yardLine);
                    
                default:
                    return false;
            }
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Card target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return IsTriggerConditionMet(data, ability, caster);
        }
    }

    public enum FieldPositionCheck
    {
        InRedZone,           // Inside opponent's 20 yard line (ball_on >= 80)
        InOpponentTerritory, // Past 50 yard line (ball_on >= 50)
        InOwnTerritory,      // Own side of field (ball_on < 50)
        GoalToGo,           // Inside 10 yard line (ball_on >= 90)
        BackedUp,           // Inside own 20 (ball_on <= 20)
        Custom               // Custom yard line check
    }
}

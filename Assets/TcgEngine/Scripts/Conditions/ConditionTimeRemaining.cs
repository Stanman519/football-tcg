using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks plays remaining in half or other game state
    /// "If 2 or fewer plays left" or "If last play of half"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/PlaysRemaining", order = 10)]
    public class ConditionTimeRemaining : ConditionData
    {
        [Header("Plays/State Check")]
        public PlayStateCheck checkType;
        
        public ConditionOperatorInt oper = ConditionOperatorInt.LessEqual;
        public int value = 2;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            switch (checkType)
            {
                case PlayStateCheck.PlaysLeftInHalf:
                    return CompareInt(data.plays_left_in_half, oper, value);
                    
                case PlayStateCheck.FirstPlayOfHalf:
                    return data.plays_left_in_half >= 11;
                    
                case PlayStateCheck.LastPlayOfHalf:
                    return data.plays_left_in_half <= 1;
                    
                case PlayStateCheck.CurrentHalf:
                    return CompareInt(data.current_half, oper, value);
                    
                case PlayStateCheck.CurrentDown:
                    return CompareInt(data.current_down, oper, value);
                    
                case PlayStateCheck.YardsToGo:
                    return CompareInt(data.yardage_to_go, oper, value);
                    
                case PlayStateCheck.BallOn:
                    // raw_ball_on: 0 = own endzone, 100 = opponent endzone
                    return CompareInt(data.raw_ball_on, oper, value);
                    
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
    
    public enum PlayStateCheck
    {
        PlaysLeftInHalf,   // X plays remaining in half
        FirstPlayOfHalf,   // First play of the half
        LastPlayOfHalf,    // Last play of the half
        CurrentHalf,       // Which half (1 or 2)
        CurrentDown,       // Current down (1-4)
        YardsToGo,         // Yards to go for first down
        BallOn             // Ball position (yard line: 0-100)
    }
}

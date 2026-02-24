using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check game clock state - 2-minute drill, prevent defense, hurry-up offense
    /// Use case: "2-minute warning", "when opponent is in prevent", "hurry-up mode"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionClockMode", menuName = "TcgEngine/Condition/Clock Mode")]
    public class ConditionClockMode : ConditionData
    {
        public enum ClockMode
        {
            TwoMinuteDrill,     // Final 2 minutes of half
            TwoMinuteWarning,   // Exactly at 2:00
            PreventDefense,     // Opponent backed up, playing conservatively
            HurryUp,            // Offense no-huddle
            ClockRunning,       // Clock is running (vs stopped)
            FinalPlay,          // Last play of half/game
        }

        [Header("Clock mode to check")]
        public ClockMode clockMode = ClockMode.TwoMinuteDrill;
        
        [Header("Seconds threshold for 2-minute modes")]
        public int secondsThreshold = 120;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            switch (clockMode)
            {
                case ClockMode.TwoMinuteDrill:
                    // Check if turn_timer or game timer is under threshold
                    // Assuming game tracks seconds remaining in half
                    return data.turn_timer <= secondsThreshold;

                case ClockMode.TwoMinuteWarning:
                    // Check if timer is around 2:00 (allow small margin)
                    float margin = 10f;
                    return data.turn_timer <= (secondsThreshold + margin) && 
                           data.turn_timer >= (secondsThreshold - margin);

                case ClockMode.PreventDefense:
                    // Prevent defense = opponent is in own territory and playing conservatively
                    // Simplified: opponent within their own 20
                    Player opponent = data.GetOpponentPlayer(caster.player_id);
                    return data.raw_ball_on <= 20;

                case ClockMode.HurryUp:
                    // Hurry-up = no-huddle, essentially means quick plays
                    // Could track if player skipped huddle
                    // For now, check if few plays remain
                    return data.plays_left_in_half <= 3;

                case ClockMode.ClockRunning:
                    // Would need game state to track if clock is running
                    return true; // Placeholder

                case ClockMode.FinalPlay:
                    return data.plays_left_in_half <= 1;

                default:
                    return false;
            }
        }
    }
}

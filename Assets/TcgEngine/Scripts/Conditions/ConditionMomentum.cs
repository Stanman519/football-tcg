using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Check momentum state - consecutive successes or failures
    /// Use case: "If 3+ consecutive completions", "After back-to-back runs"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionMomentum", menuName = "TcgEngine/Condition/Momentum")]
    public class ConditionMomentum : ConditionData
    {
        public enum MomentumType
        {
            ConsecutivePlaysPositive,  // X+ positive plays in a row
            ConsecutivePlaysNegative,  // X+ negative plays in a row
            ConsecutiveSameType,       // X+ of same play type
            HeatingUp,                 // Getting better each play (streak)
            CoolingDown,               // Getting worse each play
        }

        public MomentumType momentumType = MomentumType.ConsecutivePlaysPositive;
        
        [Header("Number of consecutive plays")]
        public int streakCount = 3;

        [Header("For HeatingUp/CoolingDown - what counts as positive/negative")]
        public int positiveYardage = 5; // Yards needed to be "positive"

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            // Use caster's player for momentum checks
            Player player = data.GetPlayer(caster.player_id);
            if (player == null)
                return false;

            switch (momentumType)
            {
                case MomentumType.ConsecutivePlaysPositive:
                    return GetPositiveStreak(player) >= streakCount;

                case MomentumType.ConsecutivePlaysNegative:
                    return GetNegativeStreak(player) >= streakCount;

                case MomentumType.ConsecutiveSameType:
                    return GetConsecutiveSameTypeCount(player) >= streakCount;

                case MomentumType.HeatingUp:
                    return IsHeatingUp(player);

                case MomentumType.CoolingDown:
                    return IsCoolingDown(player);

                default:
                    return false;
            }
        }

        private int GetPositiveStreak(Player p)
        {
            // Would need play history tracking
            // For now, placeholder
            return 0;
        }

        private int GetNegativeStreak(Player p)
        {
            return 0;
        }

        private int GetConsecutiveSameTypeCount(Player p)
        {
            // Check consecutive_play_count
            foreach (var kvp in p.consecutive_play_count)
            {
                if (kvp.Value >= streakCount)
                    return kvp.Value;
            }
            return 0;
        }

        private bool IsHeatingUp(Player p)
        {
            // Track if last N plays were getting better
            return false;
        }

        private bool IsCoolingDown(Player p)
        {
            // Track if last N plays were getting worse
            return false;
        }
    }
}

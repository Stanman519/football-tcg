using System;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    /// <summary>
    /// Pure arithmetic helpers for football yardage calculations.
    /// All methods are stateless — they take numbers and return numbers.
    /// Called from GameLogicService.Yardage.cs; kept separate so they can be
    /// unit-tested without a Game instance.
    /// </summary>
    public static class FootballMath
    {
        /// <summary>
        /// Net yardage for a run play.
        /// Formula: (coachBase + playerRunBase + playerStatusBonus) - (defCoverageBase + defStatusBonus)
        /// </summary>
        public static int CalcRunYardage(int coachBase, int playerRunBase, int playerStatusBonus,
                                         int defCoverageBase, int defStatusBonus)
            => (coachBase + playerRunBase + playerStatusBonus) - (defCoverageBase + defStatusBonus);

        /// <summary>
        /// Net yardage for a completed pass play.
        /// Formula: receiverBase + receiverStatus + otherOffYardage + coachYardage - defYardage
        /// </summary>
        public static int CalcPassYardage(int receiverBase, int receiverStatus, int otherOffYardage,
                                          int coachYardage, int defYardage)
            => receiverBase + receiverStatus + otherOffYardage + coachYardage - defYardage;

        /// <summary>
        /// Clamps negative yardage upward by preventLossValue.
        /// preventLossValue >= 999 is treated as "no yard loss" (clamp to 0).
        /// No-ops if yardage >= 0 or preventLossValue <= 0.
        /// </summary>
        public static int ApplyPreventLoss(int yardage, int preventLossValue)
        {
            if (preventLossValue <= 0 || yardage >= 0) return yardage;
            if (preventLossValue >= 999) return 0;
            return Mathf.Min(0, yardage + preventLossValue);
        }

        /// <summary>
        /// Resolves a grit contest between offense and defense (fumble recovery, QB fumble).
        /// Tie goes to offense. Returns whether it's a turnover and the return yardage.
        /// </summary>
        public static (bool turnover, int yardage) CalcGritContest(int offGrit, int defGrit)
        {
            bool turnover = defGrit > offGrit;
            int diff = Math.Abs(defGrit - offGrit);
            return (turnover, turnover ? 2 * diff : diff);
        }

        /// <summary>
        /// Yardage gained/lost on an interception return.
        /// If defense has a grit surplus, they return it further (negative for offense).
        /// </summary>
        public static int CalcInterceptionYardage(int netYardagePoint, int offGrit, int defGrit)
            => defGrit > offGrit ? netYardagePoint - (defGrit - offGrit) : netYardagePoint;
    }
}

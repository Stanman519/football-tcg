using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Compare score difference between player and opponent
    /// Use case: "If trailing by 7+", "If ahead by 10+"
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionScoreDiff", menuName = "TcgEngine/Condition/Score Difference")]
    public class ConditionScoreDiff : ConditionData
    {
        [Header("Score diff comparison")]
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int scoreDiffThreshold = 0;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            Player opponent = data.GetOpponentPlayer(target.player_id);
            if (opponent == null)
                return false;

            int playerScore = target.points;
            int opponentScore = opponent.points;
            int diff = playerScore - opponentScore;

            return CompareInt(diff, oper, scoreDiffThreshold);
        }
    }
}

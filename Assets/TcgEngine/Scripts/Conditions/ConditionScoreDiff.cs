using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Compares score between players
    /// "If trailing by X" or "If leading by Y" or "If score > Z"
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/ScoreDiff", order = 10)]
    public class ConditionScoreDiff : ConditionData
    {
        [Header("Score Comparison")]
        public bool compareToOpponent = true;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;
        public int value = 0;
        
        // Alternative: absolute score check (not differential)
        public bool absoluteScore = false;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            Player casterPlayer = data.GetPlayer(caster.player_id);
            Player opponentPlayer = data.GetOpponentPlayer(caster.player_id);
            
            if (opponentPlayer == null)
                return false;
            
            int casterScore = casterPlayer.points;
            int opponentScore = opponentPlayer.points;
            
            if (absoluteScore)
            {
                // Check absolute score: "If I have X+ points"
                return CompareInt(casterScore, oper, value);
            }
            else if (compareToOpponent)
            {
                // Score difference: "If I lead by X" or "If trailing by X"
                // Positive value = lead, negative = trail
                int diff = casterScore - opponentScore;
                return CompareInt(diff, oper, value);
            }
            else
            {
                // Just compare to absolute value
                return CompareInt(casterScore, oper, value);
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
}

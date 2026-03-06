using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks if defense correctly/incorrectly guessed the play
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Defense/GuessCorrect", order = 10)]
    public class ConditionCoverageGuess : ConditionData
    {
        [Header("Coverage Guess Check")]
        public bool guessCorrect = true; // true = defense guessed right, false = guessed wrong

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return data.WasDefenseGuessCorrect() == guessCorrect;
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

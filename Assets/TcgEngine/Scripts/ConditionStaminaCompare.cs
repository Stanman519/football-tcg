using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Compare player's stamina to opponent's stamina
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionStaminaCompare", menuName = "TcgEngine/Condition/Stamina Compare")]
    public class ConditionStaminaCompare : ConditionData
    {
        [Header("Stamina comparison")]
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            Player opponent = data.GetOpponentPlayer(target.player_id);
            if (opponent == null)
                return false;

            int playerStamina = target.GetTotalStamina();
            int opponentStamina = opponent.GetTotalStamina();

            return CompareInt(playerStamina, oper, opponentStamina);
        }
    }
}


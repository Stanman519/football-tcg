using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

namespace TcgEngine.Conditions
{
    /// <summary>
    /// Compare player's grit to opponent's grit
    /// </summary>
    [CreateAssetMenu(fileName = "ConditionGritCompare", menuName = "TcgEngine/Condition/Grit Compare")]
    public class ConditionGritCompare : ConditionData 
    {
        [Header("Grit comparison")]
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            Player opponent = data.GetOpponentPlayer(target.player_id);
            if (opponent == null)
                return false;

            int playerGrit = target.GetTotalGrit();
            int opponentGrit = opponent.GetTotalGrit();

            return CompareInt(playerGrit, oper, opponentGrit);
        }
    }
}

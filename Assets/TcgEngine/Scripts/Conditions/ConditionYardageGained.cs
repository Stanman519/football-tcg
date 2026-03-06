using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks yardage gained on the current play (or the previous play).
    /// Inspector params:
    ///   oper      - comparison operator (GreaterEqual, LessEqual, Less, Greater, Equal)
    ///   threshold - yardage value to compare against
    ///   checkLastPlay - if true, checks last_play_yardage; if false, checks yardage_this_play
    ///
    /// Importer shorthand (cond_val):
    ///   ">=15"        → current play yardage >= 15
    ///   "<=7"         → current play yardage <= 7
    ///   "<0"          → current play yardage < 0 (lost yardage)
    ///   "Last|>=10"   → last play yardage >= 10
    ///   "Last|<0"     → last play lost yardage
    /// </summary>
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/YardageGained", order = 10)]
    public class ConditionYardageGained : ConditionData
    {
        [Header("Yardage Check")]
        public int threshold = 0;
        public ConditionOperatorInt oper = ConditionOperatorInt.GreaterEqual;

        [Tooltip("If true, checks the PREVIOUS play's yardage (last_play_yardage). " +
                 "If false, checks the CURRENT play's yardage (yardage_this_play).")]
        public bool checkLastPlay = false;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            int yardage = checkLastPlay ? data.last_play_yardage : data.yardage_this_play;
            return CompareInt(yardage, oper, threshold);
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

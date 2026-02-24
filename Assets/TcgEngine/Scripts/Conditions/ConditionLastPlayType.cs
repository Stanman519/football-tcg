using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Checks the last play type (for sequence/pattern abilities)
    /// Uses PlayType from PlayCallManager: Run, ShortPass, LongPass
    /// </summary>
    
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/Game/LastPlayType", order = 10)]
    public class ConditionLastPlayType : ConditionData
    {
        [Header("Last Play Type Check")]
        public PlayType requiredPlayType;
        public ConditionOperatorBool oper = ConditionOperatorBool.IsTrue;
        
        // Check if current play is DIFFERENT from last play (for Balanced Approach)
        public bool requireDifferent = false;

        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            // Get the last play from play history
            PlayHistory lastPlay = data.GetLastPlay();
            
            if (lastPlay == null)
                return false;
            
            PlayType lastPlayType = lastPlay.offensive_play;
            
            if (requireDifferent)
            {
                // For Balanced Approach - true if different from last
                Player casterPlayer = data.GetPlayer(caster.player_id);
                bool isDifferent = lastPlayType != casterPlayer.SelectedPlay;
                return isDifferent;
            }
            
            return CompareBool(lastPlayType == requiredPlayType, oper);
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

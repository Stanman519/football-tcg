using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TcgEngine;
using UnityEngine;
using TcgEngine.Gameplay;
namespace TcgEngine.Conditions
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/GamePhase/PlayType", order = 10)]
    public class ConditionPlayType : ConditionData
    {
        public PlayType required_play;
        
        public ConditionOperatorBool oper;

        public override bool IsTargetConditionMet(Game data, AbilityData ability, Card caster, Player target)
        {
            return CompareBool(data.GetPlayer(target.player_id).SelectedPlay == required_play, oper);
        }
    }
}

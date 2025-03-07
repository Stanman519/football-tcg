using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Conditions
{
    [CreateAssetMenu(fileName = "condition", menuName = "TcgEngine/Condition/GamePhase/FirstPlay", order = 10)]
    public class ConditionFirstPlay : ConditionData
    {
        public override bool IsTriggerConditionMet(Game data, AbilityData ability, Card caster)
        {
            return caster.on_field_history.Count == 0;
        }
    }
}

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Prevents loss of yardage (nullifies sacks, tackles for loss)
    /// </summary>
    public class EffectPreventLoss : EffectData
    {
        public int value = 0; // Amount to prevent
        
        // Alternative: completely negate negative yardage
        public bool negateCompletely = true;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            // This would need to be applied during play resolution
            // Mark that this card prevents loss for this play
            // The actual prevention happens in ResolveRun/ResolvePass
            
            Game game = logic.GetGameData();
            
            // Add a status or flag indicating loss prevention is active
            // This would be checked in the resolution code
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}

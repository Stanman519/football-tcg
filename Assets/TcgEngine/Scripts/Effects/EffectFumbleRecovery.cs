using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Recovers a fumble (offense keeps the ball)
    /// </summary>
    public class EffectFumbleRecovery : EffectData
    {
        // Can be: first fumble only, or every fumble
        public bool firstFumbleOnly = true;
        
        // Can specify percentage chance
        public int chancePercent = 100;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            
            // Store fumble recovery ability on the card
            // When fumble occurs, this is checked
            // If successful, turnover is negated
            
            caster.SetTrait("fumble_recovery", chancePercent);
            
            if (firstFumbleOnly)
            {
                caster.SetTrait("fumble_recovery_used", 0); // Track if used
            }
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

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Guarantees a score (touchdown) when in certain position
    /// </summary>
    public class EffectAlwaysScore : EffectData
    {
        // Can specify yard line - inside this line = guaranteed score
        public int yardLine = 5; // Inside 5 yard line
        public int points = 7; // How many points (7 = TD, 3 = FG)
        
        // Or can be automatic TD
        public bool automaticTD = true;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            
            // Check if in scoring position
            // raw_ball_on >= (100 - yardLine) means we're inside the yard line
            bool inPosition = game.raw_ball_on >= (100 - yardLine);
            
            if (inPosition && automaticTD)
            {
                // Force touchdown - would need game state flag
                // This affects play resolution
                caster.SetTrait("guaranteed_score", points);
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

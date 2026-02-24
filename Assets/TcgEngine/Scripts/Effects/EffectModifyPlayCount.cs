using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Modifies play count (for Play Fast! coach - first downs don't count)
    /// </summary>
    public class EffectModifyPlayCount : EffectData
    {
        public PlayCountModifier modifier = PlayCountModifier.ExemptThisPlay;
        public int value = 1;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            
            switch (modifier)
            {
                case PlayCountModifier.ExemptThisPlay:
                    // Don't count this play toward plays_left_in_half
                    game.plays_left_in_half += value; // Adding because it will be decremented elsewhere
                    break;
                    
                case PlayCountModifier.AddPlays:
                    game.plays_left_in_half += value;
                    break;
                    
                case PlayCountModifier.RemovePlays:
                    game.plays_left_in_half = System.Math.Max(0, game.plays_left_in_half - value);
                    break;
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
    
    public enum PlayCountModifier
    {
        ExemptThisPlay,  // First down - doesn't count against total
        AddPlays,        // Add plays to the half
        RemovePlays      // Remove plays from the half
    }
}

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Gives a free slot respin
    /// </summary>
    public class EffectRespin : EffectData
    {
        public int count = 1; // Number of free respins
        
        // Can target specific reel or all reels
        public bool allReels = true;
        public int targetReel = -1;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            
            // Store respin info - would need game state field
            // This allows the player to respin the slots
            // Implementation depends on UI system
            
            // For now, just trigger an event that UI can listen to
            // The actual respin logic would be in the slot machine manager
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

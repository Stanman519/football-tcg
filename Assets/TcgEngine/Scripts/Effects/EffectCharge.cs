using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Builds up charge for next activation
    /// Trigger condition checks if charge >= threshold
    /// </summary>
    public class EffectCharge : EffectData
    {
        public int value = 1; // How much charge to add
        public int chargeTarget = 20; // Threshold to trigger
        public bool resetAfterTrigger = true;
        
        // What happens when fully charged
        public EffectData chargedEffect;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            Player player = game.GetPlayer(caster.player_id);
            
            // Get current charge (stored as trait or status)
            int currentCharge = player.GetTraitValue("charge_" + caster.card_id);
            
            // Add charge
            int newCharge = currentCharge + value;
            caster.SetTrait("charge_" + caster.card_id, newCharge);
            
            // Check if reached threshold
            if (newCharge >= chargeTarget && chargedEffect != null)
            {
                // Trigger the charged effect
                chargedEffect.DoEffect(logic, ability, caster);
                
                // Reset if configured
                if (resetAfterTrigger)
                {
                    caster.SetTrait("charge_" + caster.card_id, 0);
                }
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

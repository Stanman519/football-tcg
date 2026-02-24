using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Adds slot machine symbols to the reel (for Risk-Taker coach ability)
    /// Uses SlotMachineIconType from SlotMachineManager: Football, Helmet, WildCard, Wrench, Star, None
    /// </summary>
    public class EffectAddSlotSymbol : EffectData
    {
        public SlotMachineIconType symbolType = SlotMachineIconType.WildCard;
        public int count = 1;
        
        // -1 = all reels, 0-2 = specific reel
        public int targetReel = -1;
        
        // -1 = random position, 0-2 = specific position
        public int slotPosition = -1;
        
        // If true, adds a new reel instead of symbols
        public bool addReel = false;
        
        // Duration: 0 = this play only, 1+ = number of plays, -1 = permanent
        public int duration = 0;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            
            if (game.temp_slot_modifiers == null)
                game.temp_slot_modifiers = new List<SlotModifier>();
            
            SlotModifier mod = new SlotModifier
            {
                symbolType = symbolType,
                count = count,
                targetReel = targetReel,
                slotPosition = slotPosition,
                sourceCard = caster.card_id,
                addReel = addReel,
                duration = duration,
                isPermanent = (duration == -1)
            };
            
            game.temp_slot_modifiers.Add(mod);
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
    
    [System.Serializable]
    public class SlotModifier
    {
        public SlotMachineIconType symbolType;
        public int count;
        public int targetReel; // -1 for all
        public int slotPosition; // -1 for random
        public string sourceCard;
        
        // For adding reels
        public bool addReel = false;
        public int duration = 0;
        public bool isPermanent = false;
    }
}

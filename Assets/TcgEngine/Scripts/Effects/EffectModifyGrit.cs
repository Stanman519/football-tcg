using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Modifies grit of defensive cards
    /// Can add/remove grit, or set temporary grit for a play
    /// </summary>
    public class EffectModifyGrit : EffectData
    {
        public int value = 1;
        public bool removeGrit = false; // If true, removes grit instead of adding
        
        // Target: Self (caster), Team (all defensive cards), Position (DL/LB/DB)
        public GritTarget target = GritTarget.Self;
        
        // If true, grit only lasts for this play (resets after)
        public bool temporary = false;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Player player = logic.GetGameData().GetPlayer(caster.player_id);
            
            switch (target)
            {
                case GritTarget.Self:
                    ModifyGrit(caster, value);
                    break;
                    
                case GritTarget.TeamDefense:
                    ModifyTeamGrit(player, value);
                    break;
                    
                case GritTarget.DL:
                    ModifyPositionGrit(player, PlayerPositionGrp.DL, value);
                    break;
                    
                case GritTarget.LB:
                    ModifyPositionGrit(player, PlayerPositionGrp.LB, value);
                    break;
                    
                case GritTarget.DB:
                    ModifyPositionGrit(player, PlayerPositionGrp.DB, value);
                    break;
            }
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            ModifyGrit(target, value);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            ModifyTeamGrit(target, value);
        }

        private void ModifyGrit(Card card, int amount)
        {
            int finalAmount = removeGrit ? -amount : amount;
            
            if (temporary)
            {
                // Add as status that expires after play
                card.AddStatus(StatusType.AddGrit, finalAmount, 1);
            }
            else
            {
                // Permanent modification - would need different handling
                // For now, use status
                card.AddStatus(StatusType.AddGrit, finalAmount, 0); // 0 = permanent
            }
        }

        private void ModifyTeamGrit(Player player, int amount)
        {
            foreach (Card card in player.cards_board)
            {
                if (card.slot != null && card.slot.posGroupType.IsDefense())
                {
                    ModifyGrit(card, amount);
                }
            }
        }

        private void ModifyPositionGrit(Player player, PlayerPositionGrp pos, int amount)
        {
            foreach (Card card in player.cards_board)
            {
                if (card.slot != null && card.slot.posGroupType == pos)
                {
                    ModifyGrit(card, amount);
                }
            }
        }
    }
    
    public enum GritTarget
    {
        Self,
        TeamDefense,
        DL,
        LB,
        DB
    }
}

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Modifies stamina of a card or team
    /// </summary>
    public class EffectModifyStamina : EffectData
    {
        public int value = 1;
        public bool removeStamina = false; // If true, removes stamina instead of adding
        
        // Target: Self (caster), Team (all cards), Card (specific)
        public EffectTarget target = EffectTarget.Self;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            switch (target)
            {
                case EffectTarget.Self:
                    ModifyStamina(caster, value);
                    break;
                    
                case EffectTarget.Team:
                    ModifyTeamStamina(logic.GetGameData().GetPlayer(caster.player_id), value);
                    break;
                    
                case EffectTarget.Opponent:
                    ModifyTeamStamina(logic.GetGameData().GetOpponentPlayer(caster.player_id), value);
                    break;
            }
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            ModifyStamina(target, value);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            ModifyTeamStamina(target, value);
        }

        private void ModifyStamina(Card card, int amount)
        {
            int finalAmount = removeStamina ? -amount : amount;
            card.current_stamina = System.Math.Max(0, card.current_stamina + finalAmount);
        }

        private void ModifyTeamStamina(Player player, int amount)
        {
            foreach (Card card in player.cards_board)
            {
                ModifyStamina(card, amount);
            }
        }
    }
    
    public enum EffectTarget
    {
        Self,
        Team,
        Opponent,
        Card
    }
}

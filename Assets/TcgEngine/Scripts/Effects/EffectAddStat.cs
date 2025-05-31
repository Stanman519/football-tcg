
using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    public class EffectAddStat : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            switch (ability.affected_stat)
            {
                case StatusTypePrintedStats.AddedRunBonus:
                    target.AddStatus(StatusType.AddedRunBonus, ability.stat_bonus_amount, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedShortPassBonus:
                    target.AddStatus(StatusType.AddedShortPassBonus, ability.stat_bonus_amount, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedDeepPassBonus:
                    target.AddStatus(StatusType.AddedDeepPassBonus, ability.stat_bonus_amount, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedRunCoverageBonus:
                    target.AddStatus(StatusType.AddedRunCoverageBonus, ability.stat_bonus_amount, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedShortPassCoverageBonus:
                    target.AddStatus(StatusType.AddedShortPassCoverageBonus, ability.stat_bonus_amount, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedDeepPassCoverageBonus:
                    target.AddStatus(StatusType.AddedDeepPassCoverageBonus, ability.stat_bonus_amount, ability.duration);
                    break;
                case StatusTypePrintedStats.AddGrit:
                    target.AddStatus(StatusType.AddGrit, ability.stat_bonus_amount, ability.duration);
                    break;
                case StatusTypePrintedStats.AddStamina:
                    target.AddStatus(StatusType.AddStamina, ability.stat_bonus_amount, ability.duration);
                    break;
            }

        }
    }
}

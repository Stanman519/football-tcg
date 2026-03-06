using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Adds a stat bonus to the target card scaled by how many of a specific
    /// slot icon appeared in the middle row this spin.
    ///
    /// Inspector fields:
    ///   iconToCount    — which icon to count (Football, Helmet, Star, etc.)
    ///   countWilds     — if true, Wild icons also count toward the total
    ///
    /// Reads from AbilityData:
    ///   affected_stat      — which stat to boost (same options as EffectAddStat)
    ///   stat_bonus_amount  — yards / coverage per icon
    ///   duration           — how many turns the status lasts (0 = permanent)
    ///
    /// Example: iconToCount=Star, stat_bonus_amount=3, affected_stat=AddedDeepPassBonus
    ///   → 2 stars in middle row → +6 deep pass bonus this play
    /// </summary>
    [CreateAssetMenu(fileName = "EffectAddStatPerSlotIcon", menuName = "TcgEngine/Effect/AddStatPerSlotIcon", order = 10)]
    public class EffectAddStatPerSlotIcon : EffectData
    {
        [Header("Slot Icon Multiplier")]
        public SlotMachineIconType iconToCount = SlotMachineIconType.Star;
        public bool countWilds = false;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            Game game = logic.GetGameData();

            if (game.current_slot_data?.Results == null || game.current_slot_data.Results.Count == 0)
                return;

            int count = game.GetSlotIconCount(iconToCount);
            if (countWilds)
                count += game.GetSlotIconCount(SlotMachineIconType.WildCard);

            if (count == 0)
                return;

            int totalBonus = count * ability.stat_bonus_amount;

            switch (ability.affected_stat)
            {
                case StatusTypePrintedStats.AddedRunBonus:
                    target.AddStatus(StatusType.AddedRunBonus, totalBonus, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedShortPassBonus:
                    target.AddStatus(StatusType.AddedShortPassBonus, totalBonus, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedDeepPassBonus:
                    target.AddStatus(StatusType.AddedDeepPassBonus, totalBonus, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedRunCoverageBonus:
                    target.AddStatus(StatusType.AddedRunCoverageBonus, totalBonus, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedShortPassCoverageBonus:
                    target.AddStatus(StatusType.AddedShortPassCoverageBonus, totalBonus, ability.duration);
                    break;
                case StatusTypePrintedStats.AddedDeepPassCoverageBonus:
                    target.AddStatus(StatusType.AddedDeepPassCoverageBonus, totalBonus, ability.duration);
                    break;
                case StatusTypePrintedStats.AddGrit:
                    target.AddStatus(StatusType.AddGrit, totalBonus, ability.duration);
                    break;
                case StatusTypePrintedStats.AddStamina:
                    target.AddStatus(StatusType.AddStamina, totalBonus, ability.duration);
                    break;
            }

            Debug.Log($"[SlotMultiplier] {count}x {iconToCount} → +{totalBonus} {ability.affected_stat} on {target.card_id}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            DoEffect(logic, ability, caster, caster);
        }
    }
}

using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Accumulates charge each time it fires and applies a stat bonus when the threshold is reached.
    /// Charge is stored per card-instance in Game.charge_tracker using caster.uid as the key.
    ///
    /// Two charging modes:
    ///   Slot-icon mode  — chargeIcon != None: counts matching icons in the middle row each spin.
    ///                     Maxwell Payne: Helmet icons toward 20 → +20 run.
    ///                     Tre Rostic:   Football icons toward 8 → +5 run.
    ///   Event mode      — chargeIcon == None: adds fixedChargePerActivation (usually 1) each time
    ///                     the ability fires. Gate with a condition (e.g. LastPassIncomplete).
    ///                     William Ford: 3 incomplete passes → +6 run coverage next play.
    ///
    /// When charge >= threshold:
    ///   - Applies ability.affected_stat (+ability.stat_bonus_amount, duration ability.duration) to caster.
    ///   - Resets charge to 0.
    ///
    /// For cards that need a bonus PLUS a secondary effect (draw card, heal stamina) or a non-stat
    /// effect (prevent sack), keep those as MANUAL — wire via Inspector with multiple effects.
    ///
    /// Importer shorthand (effect_val):
    ///   "Helmet|20|RunBonus|20|1"       → slot-icon mode: helmet icons → threshold 20 → +20 run
    ///   "Football|8|RunBonus|5|1"       → football icons → threshold 8 → +5 run
    ///   "Star|5|RunCoverage|6|1"        → star icons → threshold 5 → +6 run coverage
    ///   "None|3|RunCoverage|6|2"        → event mode: threshold 3 → +6 run coverage, duration 2
    /// </summary>
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Charge")]
    public class EffectCharge : EffectData
    {
        [Header("Charge Accumulation")]
        [Tooltip("Icon to count from the slot middle row each spin. Set to None for event-based charging.")]
        public SlotMachineIconType chargeIcon = SlotMachineIconType.None;
        [Tooltip("Whether Wild icons also count toward slot-icon charge. Ignored in event mode.")]
        public bool countWilds = false;
        [Tooltip("Used in event mode (chargeIcon==None): amount added to charge each time ability fires.")]
        public int fixedChargePerActivation = 1;

        [Header("Threshold")]
        [Tooltip("Charge needed to trigger the effect. Resets to 0 on trigger.")]
        public int threshold = 20;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            if (caster == null) return;
            Game game = logic.GetGameData();
            string key = caster.uid;

            // Accumulate charge
            int toAdd;
            if (chargeIcon != SlotMachineIconType.None)
            {
                // Slot-icon mode: count matching middle-row icons this spin
                toAdd = (game.current_slot_data?.Results != null)
                    ? game.GetSlotIconCount(chargeIcon)
                    : 0;
                if (countWilds && game.current_slot_data?.Results != null)
                    toAdd += game.GetSlotIconCount(SlotMachineIconType.WildCard);
            }
            else
            {
                // Event mode: fixed increment each activation
                toAdd = fixedChargePerActivation;
            }

            game.AddCharge(key, toAdd);
            int current = game.GetCharge(key);
            Debug.Log($"[Charge] {caster.card_id} charge: {current}/{threshold}");

            if (current < threshold)
                return;

            // Threshold reached — apply stat bonus and reset
            if (ability.affected_stat != StatusTypePrintedStats.None)
            {
                StatusType statType = (StatusType)(int)ability.affected_stat;
                caster.AddStatus(statType, ability.stat_bonus_amount, ability.duration);
                Debug.Log($"[Charge] {caster.card_id} TRIGGERED — +{ability.stat_bonus_amount} {ability.affected_stat} (dur {ability.duration})");
            }

            game.ResetCharge(key);
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

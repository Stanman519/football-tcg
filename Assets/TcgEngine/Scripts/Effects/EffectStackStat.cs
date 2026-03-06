using System;
using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Applies a stat bonus that scales with the current consecutive-play streak.
    /// Each time this fires, bonus = bonusPerStack × (streak length), capped at maxBonus (0 = uncapped).
    ///
    /// Wire with OnRunResolution or OnPassResolution trigger.
    /// The play_history snapshot does NOT yet include the current play when this fires,
    /// so +1 is added internally to count the current play as part of the streak.
    ///
    /// The stat type comes from ability.affected_stat (same inspector field AddStat uses).
    /// Status is applied with duration=1 so it expires at end of turn (affects this play's resolution).
    ///
    /// Importer shorthand (effect_val):
    ///   "RunBonus|Run|2|0"   → affected_stat=RunBonus, stackPlayType=Run, bonusPerStack=2, uncapped
    ///   "RunBonus|Run|1|5"   → affected_stat=RunBonus, stackPlayType=Run, +1 per run, max +5
    ///   "RunBonus|Run|1|3"   → same, max +3
    /// </summary>
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/StackStat")]
    public class EffectStackStat : EffectData
    {
        [Header("Stacking Configuration")]
        [Tooltip("Which play type's consecutive streak is counted.")]
        public PlayType stackPlayType = PlayType.Run;
        [Tooltip("Stat bonus added per play in the streak (including the current play).")]
        public int bonusPerStack = 1;
        [Tooltip("Maximum total bonus that can be applied. 0 = uncapped.")]
        public int maxBonus = 0;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game data = logic.GetGameData();

            // play_history has not yet recorded the current play; count previous streak and add 1
            int prevStreak = CountConsecutiveFromHistory(data);
            int totalStreak = prevStreak + 1;

            int bonus = bonusPerStack * totalStreak;
            if (maxBonus > 0)
                bonus = Math.Min(bonus, maxBonus);

            // ability.affected_stat shares numeric values with StatusType
            StatusType statType = (StatusType)(int)ability.affected_stat;
            caster.AddStatus(statType, bonus, 1);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }

        private int CountConsecutiveFromHistory(Game data)
        {
            if (data.play_history == null || data.play_history.Count == 0)
                return 0;

            int count = 0;
            for (int i = data.play_history.Count - 1; i >= 0; i--)
            {
                PlayHistory play = data.play_history[i];
                if (play.current_half != data.current_half)
                    break;
                if (play.offensive_play == stackPlayType)
                    count++;
                else
                    break;
            }
            return count;
        }
    }
}

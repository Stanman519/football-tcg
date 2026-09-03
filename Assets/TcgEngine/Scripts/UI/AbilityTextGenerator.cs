using System.Text;
using TcgEngine;
using TcgEngine.Conditions;
using TcgEngine.Effects;
using TcgEngine.Gameplay;

namespace TcgEngine.UI
{
    /// <summary>
    /// Converts AbilityData into human-readable (triggerText, effectText) pairs
    /// for the play resolution showcase banners.
    /// If ability.desc is non-empty, uses it as the effect line verbatim.
    /// </summary>
    public static class AbilityTextGenerator
    {
        public struct DisplayText
        {
            public string trigger;
            public string effect;
        }

        public static DisplayText Generate(AbilityData ability, bool useInlineIcons = false)
        {
            var result = new DisplayText();
            if (ability == null) return result;

            result.trigger = BuildTriggerText(ability, useInlineIcons);
            result.effect = BuildEffectText(ability, useInlineIcons);
            return result;
        }

        // ── Trigger Text ──────────────────────────────────────

        private static string BuildTriggerText(AbilityData ability, bool useInlineIcons = false)
        {
            var sb = new StringBuilder();

            // Slot requirements first
            if (ability.slotRequirements != null)
            {
                foreach (var req in ability.slotRequirements)
                {
                    if (req == null || req.icon == SlotMachineIconType.None) continue;
                    if (sb.Length > 0) sb.Append(" + ");
                    string icon = useInlineIcons
                        ? SlotIconRegistry.GetSpriteTag(req.icon)
                        : SlotIconRegistry.GetDisplayName(req.icon);
                    sb.Append($"{req.requiredCount}+ {icon}");
                }
            }

            // Trigger conditions
            if (ability.conditions_trigger != null)
            {
                foreach (var cond in ability.conditions_trigger)
                {
                    if (cond == null) continue;
                    string text = ConditionToText(cond);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(text);
                }
            }

            return sb.ToString();
        }

        private static string ConditionToText(ConditionData cond)
        {
            // Skip internal-only filters
            if (cond is ConditionOwner) return null;
            if (cond is ConditionSelf) return null;
            if (cond is ConditionTarget) return null;

            if (cond is ConditionSlotIconCountTrigger slot)
                return $"{slot.required_icon_count}+ {SlotIconRegistry.GetDisplayName(slot.icon_type)}";

            if (cond is ConditionDownAndDistance dd)
            {
                string ordinal = dd.down switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{dd.down}th" };
                return $"{ordinal} down";
            }

            if (cond is ConditionGameDown gd)
            {
                string ordinal = gd.required_down switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{gd.required_down}th" };
                return $"{ordinal} down";
            }

            if (cond is ConditionPlayType pt)
            {
                return pt.required_play switch
                {
                    PlayType.Run => "Run only",
                    PlayType.ShortPass => "Short pass only",
                    PlayType.LongPass => "Long pass only",
                    _ => null
                };
            }

            if (cond is ConditionConsecutivePlays cp)
                return $"{cp.requiredCount}+ straight {cp.playType.ToString().ToLower()}s";

            if (cond is ConditionFieldPosition) return "Field position";
            if (cond is ConditionFieldPositionScenario) return "Field position";

            if (cond is ConditionPlaysRemaining pr)
                return $"{pr.playsThreshold} plays left";

            if (cond is ConditionFirstPlay) return "First play";
            if (cond is ConditionFirstSnap) return "First snap";
            if (cond is ConditionLastPassIncomplete) return "After incomplete";

            if (cond is ConditionHandCount hc)
                return $"{hc.required_count}+ in hand";

            if (cond is ConditionStarCount sc)
                return $"{sc.value}+ {SlotIconRegistry.GetDisplayName(SlotMachineIconType.Star)}";

            if (cond is ConditionScoreDiff) return "Score diff";
            if (cond is ConditionMomentum) return "Momentum";
            if (cond is ConditionYardageGained) return "Yardage check";
            if (cond is ConditionGritCompare) return "Grit check";
            if (cond is ConditionStaminaCompare) return "Stamina check";

            // Fallback: strip "Condition" prefix from class name
            string name = cond.GetType().Name;
            if (name.StartsWith("Condition"))
                name = name.Substring(9);
            return name;
        }

        // ── Effect Text ───────────────────────────────────────

        private static string BuildEffectText(AbilityData ability, bool useInlineIcons = false)
        {
            // Priority: use desc if available
            if (!string.IsNullOrEmpty(ability.desc))
                return useInlineIcons ? SlotIconRegistry.ResolveTokens(ability.desc) : ability.desc;

            if (ability.effects == null || ability.effects.Length == 0)
                return "";

            var sb = new StringBuilder();
            foreach (var eff in ability.effects)
            {
                if (eff == null) continue;
                string text = EffectToText(eff, ability);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(text);
            }
            return sb.ToString();
        }

        private static string EffectToText(EffectData eff, AbilityData ability)
        {
            if (eff is EffectAddStat)
                return $"+{ability.stat_bonus_amount} {StatName(ability.affected_stat)}";

            if (eff is EffectAddStatPerSlotIcon perIcon)
                return $"+{ability.stat_bonus_amount}/{SlotIconRegistry.GetDisplayName(perIcon.iconToCount)} {StatName(ability.affected_stat)}";

            if (eff is EffectStackStat)
                return $"+{ability.stat_bonus_amount} {StatName(ability.affected_stat)} (stacks)";

            if (eff is EffectDrawCard) return $"Draw {ability.value}";
            if (eff is EffectDiscardCard) return $"Discard {ability.value}";
            if (eff is EffectKnockout) return "Knockout";
            if (eff is EffectModifyStamina) return $"{(ability.value >= 0 ? "+" : "")}{ability.value} stamina";
            if (eff is EffectModifyGrit) return $"{(ability.value >= 0 ? "+" : "")}{ability.value} grit";
            if (eff is EffectAddGrit) return $"+{ability.value} grit";
            if (eff is EffectPreventLoss) return "No yard loss";
            if (eff is EffectPreventTurnover) return "Ball secured";
            if (eff is EffectNegateCard) return "Negated!";
            if (eff is EffectImmunity) return "Immune";
            if (eff is EffectForceTurnover) return "Forced turnover!";
            if (eff is EffectReturnToHand) return "Return to hand";
            if (eff is EffectYardageModifier) return $"{(ability.value >= 0 ? "+" : "")}{ability.value} yards";
            if (eff is EffectModifyPlayCount) return "Extra play";
            if (eff is EffectCharge) return "Charge!";
            if (eff is EffectRespin) return "Respin!";
            if (eff is EffectDiscover) return "Discover";
            if (eff is EffectAlwaysScore) return "Guaranteed score";
            if (eff is EffectFumbleRecovery) return "Fumble recovery";

            // Fallback
            string name = eff.GetType().Name;
            if (name.StartsWith("Effect"))
                name = name.Substring(6);
            return name;
        }

        // ── Helpers ───────────────────────────────────────────

        private static string StatName(StatusTypePrintedStats stat)
        {
            return stat switch
            {
                StatusTypePrintedStats.AddedRunBonus => "run",
                StatusTypePrintedStats.AddedShortPassBonus => "short",
                StatusTypePrintedStats.AddedDeepPassBonus => "deep",
                StatusTypePrintedStats.AddedRunCoverageBonus => "run cov",
                StatusTypePrintedStats.AddedShortPassCoverageBonus => "short cov",
                StatusTypePrintedStats.AddedDeepPassCoverageBonus => "deep cov",
                StatusTypePrintedStats.AddStamina => "stamina",
                StatusTypePrintedStats.AddGrit => "grit",
                _ => stat.ToString()
            };
        }

    }
}

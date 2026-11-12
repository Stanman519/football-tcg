using TcgEngine.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace TcgEngine.Gameplay
{
    public partial class GameLogicService
    {
        // ── Play Outcome Resolution ──────────────────────────────────────────
        // All 14 methods that form the yardage/resolution pipeline.
        // Pure arithmetic is delegated to FootballMath (static, testable).

        private void ResolvePlayOutcome()
        {
            var playResolution = new PlayResolution();

            // Reset fail event / turnover actor fields for this play
            game_data.last_fail_event = FailPlayEventType.None;
            game_data.interceptor_uid = null;
            game_data.fumble_recoverer_uid = null;
            game_data.fail_event_caster_uid = null;
            game_data.tackler_uid = null;
            game_data.ctr_defender_uid = null;
            game_data.ctr_covered_receiver_uid = null;
            game_data.play_impacts = new List<PlayImpact>();

            // Fail events are resolved in chronological football order via
            // OrderBy(FailPlayEventType enum value), first-match-wins.
            // See FailPlayEventType XML doc for the priority tiers.

            // Fire all slot-triggered abilities (stat buffs sync, fail events queued)
            TriggerBoardSlotAbilities(game_data.current_offensive_player.SelectedPlay);

            //STEP 0: check if pass or run then split off
            if (game_data.current_offensive_player.SelectedPlay == PlayType.Run)
                playResolution = ResolveRunOutcome();
            else
                playResolution = ResolvePassOutcome();

            // Check guaranteed score
            if (game_data.current_offensive_player.cards_board
                    .Any(c => c.GetTraitValue("guaranteed_score") > 0))
            {
                Debug.Log("[AlwaysScore] Guaranteed score activated — forcing touchdown yardage.");
                playResolution.YardageGained = 100 - game_data.raw_ball_on;
                playResolution.BallIsLive = false;
                playResolution.Turnover = false;
            }

            // Store yardage and turnover flag so EndPlayPhase can apply them
            game_data.yardage_this_play = playResolution.YardageGained;
            game_data.last_play_yardage = playResolution.YardageGained;
            game_data.turnover_pending = playResolution.Turnover;
            game_data.last_play_type = game_data.current_offensive_player.SelectedPlay;

            // If yardage takes ball to end zone or safety, end play immediately
            if (game_data.raw_ball_on + playResolution.YardageGained >= 100 || game_data.raw_ball_on + playResolution.YardageGained <= 0)
                playResolution.BallIsLive = false;

            // Credit a tackler now that the full outcome is known (drives client animation)
            game_data.tackler_uid = TacklerSelector.Choose(game_data);

            // Always go through Resolution phase so client fires snap + route animations
            StartPlayResolutionPhase(playResolution.BallIsLive);

        }

        /// <summary>
        /// Called at the start of ResolvePlayOutcome. Scans all board cards for
        /// OnRunResolution / OnPassResolution abilities and fires them if slot
        /// requirements (if any) are satisfied.
        ///
        /// Stat buff abilities (failEventType == None): fired synchronously via
        ///   ResolveCardAbility so their effects are visible to the yardage calc.
        /// Fail event abilities (failEventType != None): added to the resolve queue
        ///   (null callback) so ResolveRunFailEvents / ResolvePassFailEvents pick
        ///   them up through GetAbilityQueue().
        /// </summary>
        // Status types tracked for per-ability impact attribution (pre-snap showcases)
        private static readonly StatusType[] ImpactStatuses =
        {
            StatusType.AddedRunBonus, StatusType.AddedShortPassBonus, StatusType.AddedDeepPassBonus,
            StatusType.AddedRunCoverageBonus, StatusType.AddedShortPassCoverageBonus, StatusType.AddedDeepPassCoverageBonus,
        };

        private int[] SnapshotStatusSums()
        {
            var sums = new int[ImpactStatuses.Length];
            for (int i = 0; i < ImpactStatuses.Length; i++)
            {
                int total = 0;
                foreach (var p in game_data.players)
                    foreach (var c in p.cards_board)
                        total += c.GetStatusValue(ImpactStatuses[i]);
                sums[i] = total;
            }
            return sums;
        }

        protected void TriggerBoardSlotAbilities(PlayType playType)
        {
            var triggerType = playType == PlayType.Run
                ? AbilityTrigger.OnRunResolution
                : AbilityTrigger.OnPassResolution;

            foreach (var player in game_data.players)
            {
                foreach (var card in player.cards_board.ToList())
                {
                    foreach (var ability in card.GetAbilities())
                    {
                        if (ability == null || ability.trigger != triggerType) continue;

                        // Slot gate
                        bool slotOk = ability.AreSlotRequirementsMet(game_data.current_slot_data);
#if UNITY_EDITOR
                        // Dev outcome forcing (DevState): guarantee procs / a specific fail event for playtests
                        bool isFail = ability.failEventType != FailPlayEventType.None;
                        if (!isFail && TcgEngine.Dev.DevState.ForceAbilityProcs) slotOk = true;
                        if (isFail && TcgEngine.Dev.DevState.ForceFailEvent != FailPlayEventType.None)
                            slotOk = ability.failEventType == TcgEngine.Dev.DevState.ForceFailEvent;
#endif
                        if (!slotOk)
                        {
                            Debug.Log($"[SlotDraw] {card.card_id} '{ability.id}' — req not met, skipped.");
                            continue;
                        }

                        // Other trigger conditions (e.g. ConditionOwner, ConditionLastPlayType)
                        bool condOk = ability.AreTriggerConditionsMet(game_data, card);
#if UNITY_EDITOR
                        // Card abilities gate on slot icons via conditions_trigger (not slotRequirements) - bypass those too
                        if (!isFail && TcgEngine.Dev.DevState.ForceAbilityProcs) condOk = true;
                        if (isFail && ability.failEventType == TcgEngine.Dev.DevState.ForceFailEvent) condOk = true;
#endif
                        if (!condOk) continue;

                        // Data gap: shell ability with no effects and no fail event does nothing, so don't showcase it
                        if (ability.failEventType == FailPlayEventType.None
                            && (ability.effects == null || ability.effects.Length == 0))
                        {
                            Debug.LogWarning($"[SlotDraw] {card.card_id} '{ability.id}' has no effects, skipped (fix card data).");
                            continue;
                        }

                        if (ability.failEventType != FailPlayEventType.None)
                        {
                            // Fail event: enqueue for ResolveRunFailEvents / ResolvePassFailEvents
                            resolve_queue.AddAbility(ability, card, card, null);
                            Debug.Log($"[SlotDraw] Queued fail event: {ability.failEventType} from {card.card_id}");
                        }
                        else
                        {
                            // Stat buff: apply synchronously before yardage is calculated.
                            // Snapshot status sums around it so the contribution can be attributed
                            // to this caster (abilities resolve serially here — unambiguous).
                            Debug.Log($"[SlotDraw] Firing stat ability '{ability.id}' from {card.card_id}");
                            int[] before = SnapshotStatusSums();
                            ResolveCardAbility(ability, card, card);
                            int[] after = SnapshotStatusSums();

                            bool anyDelta = false;
                            for (int i = 0; i < ImpactStatuses.Length; i++)
                            {
                                if (after[i] != before[i])
                                {
                                    anyDelta = true;
                                    game_data.play_impacts.Add(new PlayImpact
                                    {
                                        caster_uid = card.uid,
                                        ability_id = ability.id,
                                        status_type = (int)ImpactStatuses[i],
                                        value = after[i] - before[i],
                                    });
                                }
                            }
                            if (!anyDelta)
                            {
                                // Non-stat ability (respin, slot modifier...) — still worth a showcase
                                game_data.play_impacts.Add(new PlayImpact
                                {
                                    caster_uid = card.uid,
                                    ability_id = ability.id,
                                    status_type = 0,
                                    value = 0,
                                });
                            }
                        }
                    }
                }
            }
        }

        protected PlayResolution ResolvePassOutcome()
        {
            var playType = game_data.current_offensive_player.SelectedPlay;
            var middleIcons = game_data.current_slot_data.Results
                .Select(r => r.Middle?.IconId ?? SlotMachineIconType.None)
                .ToList();

            bool hasQB = game_data.current_offensive_player.cards_board
                .Any(c => c.CardData.playerPosition == PlayerPositionGrp.QB);

            if (!hasQB)
            {
                // Replacement-level QB: any wrench fails both pass types;
                // long passes also fail if no football shows in the middle row.
                bool hasWrench  = middleIcons.Any(i => i == SlotMachineIconType.Wrench);
                bool hasFootball = middleIcons.Any(i => i == SlotMachineIconType.Football
                                                      || i == SlotMachineIconType.WildCard);
                bool incomplete = hasWrench || (playType == PlayType.LongPass && !hasFootball);
                if (incomplete)
                {
                    // Direct short-circuit: AddAbility silently drops null casters, so the
                    // queued version never fired (replacement-QB passes could never go incomplete)
                    Debug.Log($"[ReplacementQB] Pass incomplete ({(hasWrench ? "wrench" : "no football on long")}).");
                    return HandleIncompletePass(null);
                }
            }
            // Named QB's fail conditions (e.g. ConditionSlotIconCount(Wrench,>=1), ConditionBoardPositionCount(WR,<3))
            // are abilities on the QB card with trigger=OnPassResolution and failEventType=IncompletePass.
            // They were already queued by TriggerBoardSlotAbilities() before we got here.

            var failEvent = ResolvePassFailEvents();

            if (failEvent != null)
            {
                return failEvent;
            }

            List<Card> availableReceivers = game_data.current_offensive_player.GetAvailableReceivers();

            ReceiverRankingSystem rankingSystem = new ReceiverRankingSystem(availableReceivers, game_data.current_offensive_player.SelectedPlay == PlayType.LongPass);

            CoverageResult coverage = rankingSystem.ApplyCoverage(game_data);
            Card targetReceiver = coverage.receiver;
            game_data.ctr_defender_uid = coverage.ctrDefender?.uid;
            game_data.ctr_covered_receiver_uid = coverage.coveredTopReceiver?.uid;
            var currPlayType = game_data.current_offensive_player.SelectedPlay;
            var currentPlayStatus = currPlayType == PlayType.LongPass ? StatusType.AddedDeepPassBonus : StatusType.AddedShortPassBonus;

            // If no eligible receiver, pass still gains coach/OL/QB yardage but zero receiver bonus
            int statusYardage = 0;
            int baseYardage = 0;
            if (targetReceiver != null)
            {
                statusYardage = targetReceiver.status
                    .FirstOrDefault(_ => _.type == currentPlayStatus)?.value ?? 0;
                baseYardage = currentPlayStatus == StatusType.AddedDeepPassBonus
                    ? targetReceiver.Data.deep_pass_bonus
                    : targetReceiver.Data.short_pass_bonus;
            }

            var coachYardage = game_data.current_offensive_player.head_coach.baseOffenseYardage[game_data.current_offensive_player.SelectedPlay];
            var otherPositionGroupsToCount = new PlayerPositionGrp[2]
            {
                PlayerPositionGrp.QB,
                PlayerPositionGrp.OL
            };


            var otherOffPlayerYardage = game_data.current_offensive_player.cards_board
                .Where(c => otherPositionGroupsToCount.Contains(c.Data.playerPosition))
                .Sum(c => (currPlayType == PlayType.LongPass ? c.Data.deep_pass_bonus : c.Data.short_pass_bonus) +
                    c.GetStatusValue(currentPlayStatus));

            var defPlayerPass = game_data.GetCurrentDefensivePlayer();
            // BUGFIX: was summing def cards w/ the OFFENSIVE status type — defensive coverage buffs never counted
            var coverageStatus = currPlayType == PlayType.LongPass
                ? StatusType.AddedDeepPassCoverageBonus
                : StatusType.AddedShortPassCoverageBonus;
            int defStatusBonus = defPlayerPass.cards_board.Sum(c => c.GetStatusValue(coverageStatus));
            var otherDefPlayerYardage = defPlayerPass.cards_board
                .Sum(c => currPlayType == PlayType.LongPass ? c.Data.deep_pass_coverage_bonus : c.Data.short_pass_coverage_bonus)
                + defStatusBonus;
            int defCoachPassBase = defPlayerPass.head_coach.baseDefenseYardage[currPlayType];

            bool defGuessedPass = defPlayerPass.SelectedPlay == currPlayType;
            int passCoverageMod = defPlayerPass.coachManager?.GetCoverageModifier(defGuessedPass) ?? 0;
            int effectivePassDefYardage = Math.Max(0, otherDefPlayerYardage + defCoachPassBase + passCoverageMod);

            Debug.Log($"[CoverageGuess] DefGuess={defPlayerPass.SelectedPlay} Actual={currPlayType} Correct={defGuessedPass} CoachBase={defCoachPassBase} Modifier={passCoverageMod} Effective={effectivePassDefYardage}");
            int passTotal = FootballMath.CalcPassYardage(baseYardage, statusYardage, otherOffPlayerYardage, coachYardage, effectivePassDefYardage);
            Debug.Log($"[PassResolution] ReceiverBase={baseYardage} ReceiverStatus={statusYardage} OtherOff={otherOffPlayerYardage} Coach={coachYardage} DefCoverage={effectivePassDefYardage} => Total={passTotal} (Receiver={targetReceiver?.card_id ?? "none"})");

            int preventLoss = game_data.current_offensive_player.cards_board
                .Select(c => c.GetTraitValue("prevent_loss")).DefaultIfEmpty(0).Max();
            if (preventLoss > 0 && passTotal < 0)
            {
                passTotal = FootballMath.ApplyPreventLoss(passTotal, preventLoss);
                Debug.Log($"[PreventLoss] Pass yardage clamped to {passTotal}");
            }

            // Stash breakdown for UI
            game_data.yardage_breakdown = new YardageBreakdown
            {
                offCoachBase = coachYardage,
                offPlayerBase = baseYardage,
                offStatusBonuses = statusYardage,
                offOtherPositions = otherOffPlayerYardage,
                defCoverageBase = otherDefPlayerYardage - defStatusBonus,
                defCoachBase = defCoachPassBase,
                defCoverageMod = passCoverageMod,
                defStatusBonuses = defStatusBonus,
                netYardage = passTotal,
                receiverName = targetReceiver?.CardData?.title ?? "",
                ctrDefenderName = coverage.ctrDefender?.CardData?.title ?? "",
            };

            // Set receiver uids for client animation
            game_data.target_receiver_uid = targetReceiver?.uid;
            game_data.ball_carrier_uid = targetReceiver?.uid;

            foreach (var p in game_data.players)
                p.coachManager?.OnCoachTrigger(CoachTrigger.OnPassPlay);

            return new PlayResolution
            {
                BallIsLive = true,
                Turnover = false,
                YardageGained = passTotal,
                ContributingAbilities = new List<AbilityQueueElement>()
            };

        }

        // basically returning a resolution that could have multiple abilities to playback in order
        protected virtual PlayResolution ResolvePassFailEvents()
        {
            var failList = resolve_queue.GetAbilityQueue()
                .Where(a => a.ability.trigger == AbilityTrigger.OnPassResolution &&
                            a.ability.failEventType != FailPlayEventType.None)
                .OrderBy(a => a.ability.failEventType) // Chronological football priority (enum value)
                .ToList();

            foreach (var failEvent in failList)
            {
                switch (failEvent.ability.failEventType)
                {
                    case FailPlayEventType.Sack:
                        // "Throw It Away" enhancer converts sack to an incomplete pass
                        if (game_data.current_offensive_player.PlayEnhancer?.card_id == "00307_enh_throw_it_away")
                        {
                            Debug.Log("[ThrowItAway] Sack converted to incomplete pass.");
                            return HandleIncompletePass(failEvent);
                        }
                        if (game_data.current_offensive_player.cards_board
                                .Any(c => c.GetTraitValue("prevent_loss") > 0))
                        {
                            Debug.Log("[PreventLoss] Sack converted to incomplete pass.");
                            return HandleIncompletePass(failEvent);
                        }
                        return HandleSack(failEvent, failList.ToList());

                    case FailPlayEventType.QBFumble:
                        // Pre-throw fumble — ball loose at LOS, live ball resolves turnover
                        if (game_data.current_offensive_player.PlayEnhancer?.card_id == "00308_enh_ball_security")
                        {
                            Debug.Log("[BallSecurity] QB fumble prevented.");
                            break;
                        }
                        return HandleQbFumble(failEvent);

                    case FailPlayEventType.BattedPass:
                        return HandleBattedPass(failEvent);

                    case FailPlayEventType.TippedPass:
                        return TryToInterceptTippedPass(failEvent);

                    case FailPlayEventType.Interception:
                        return HandleInterception(failEvent);

                    case FailPlayEventType.IncompletePass:
                        return HandleIncompletePass(failEvent);

                    case FailPlayEventType.RunnerFumble:
                        // B2 fix: Ball Security also protects receiver fumbles (same as run fumbles)
                        if (game_data.current_offensive_player.PlayEnhancer?.card_id == "00308_enh_ball_security")
                        {
                            Debug.Log("[BallSecurity] Receiver fumble prevented by Ball Security.");
                            break;
                        }
                        // Receiver fumble — only reachable if no IncompletePass fired first (sorted by priority)
                        return HandleFumble(failEvent);

                    default:
                        break;
                }
            }
            return null; // No fail events resolved → proceed to completion check
        }

        private PlayResolution ResolveRunFailEvents()
        {

            var failList = resolve_queue.GetAbilityQueue()
                .Where(a => a.ability.trigger == AbilityTrigger.OnRunResolution &&
                            a.ability.failEventType != FailPlayEventType.None)
                .OrderBy(a => a.ability.failEventType) // Chronological football priority (enum value)
                .ToList();

            foreach (var failEvent in failList)
            {
                switch (failEvent.ability.failEventType)
                {
                    case FailPlayEventType.RunnerFumble:
                        // "Ball Security" enhancer protects against fumbles
                        if (game_data.current_offensive_player.PlayEnhancer?.card_id == "00308_enh_ball_security")
                        {
                            Debug.Log("[BallSecurity] Fumble prevented by Ball Security.");
                            break; // Skip fumble, continue to next fail event
                        }
                        return HandleFumble(failEvent);
                    case FailPlayEventType.TackleForLoss:
                        if (game_data.current_offensive_player.cards_board
                                .Any(c => c.GetTraitValue("prevent_loss") > 0))
                        {
                            Debug.Log("[PreventLoss] Tackle for loss negated.");
                            return new PlayResolution { BallIsLive = false, YardageGained = 0,
                                Turnover = false, ContributingAbilities = new List<AbilityQueueElement>() };
                        }
                        return HandleTackleForLoss(failEvent);
                    default:
                        break;
                }
            }
            return null;
        }

        private PlayResolution HandleTackleForLoss(AbilityQueueElement failEvent)
        {
            game_data.last_fail_event = FailPlayEventType.TackleForLoss;
            game_data.fail_event_caster_uid = failEvent.caster?.uid;
            var casterGrit = failEvent.caster.Data.grit;
            return new PlayResolution
            {
                BallIsLive =false,
                YardageGained = failEvent.caster.Data.grit < 3 ? -3 : -failEvent.caster.Data.grit,
                ContributingAbilities = new List<AbilityQueueElement>
                {
                    failEvent
                },
                Turnover = false

            };
            // lose yardage, end of play
        }

        private PlayResolution HandleSack(AbilityQueueElement failEvent, List<AbilityQueueElement> otherEvents)
        {
            game_data.last_fail_event = FailPlayEventType.Sack;
            game_data.fail_event_caster_uid = failEvent.caster?.uid;
            var fumble = otherEvents.FirstOrDefault(e => e.ability.failEventType == FailPlayEventType.QBFumble);

            if (fumble != null)
            {
                var playFailRes = HandleQbFumble(fumble);
                playFailRes.ContributingAbilities.Prepend(failEvent);
                return playFailRes;
            }
            else
            {
                return new PlayResolution
                {
                    BallIsLive = false,
                    ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                    Turnover = false,
                    YardageGained = game_data.current_offensive_player.SelectedPlay == PlayType.LongPass ? -8 : -4
                };
            }
            // remove yardage.
            // check if theres a qb fumble somehow?
            // if not end the play


        }

        protected virtual PlayResolution HandleQbFumble(AbilityQueueElement fumble)
        {
            game_data.last_fail_event = FailPlayEventType.QBFumble;
            game_data.fail_event_caster_uid = fumble.caster?.uid;
            var recoveryCard = game_data.current_offensive_player.cards_board
                .FirstOrDefault(c => c.GetTraitValue("fumble_recovery") > 0
                                  && c.GetTraitValue("fumble_recovery_used") == 0);
            if (recoveryCard != null)
            {
                int chance = recoveryCard.GetTraitValue("fumble_recovery");
                recoveryCard.SetTrait("fumble_recovery_used", 1);
                if (UnityEngine.Random.Range(0, 100) < chance)
                {
                    Debug.Log("[FumbleRecovery] QB fumble recovered! Offense keeps the ball.");
                    return new PlayResolution { BallIsLive = false, YardageGained = 0,
                        Turnover = false, ContributingAbilities = new List<AbilityQueueElement> { fumble } };
                }
                Debug.Log("[FumbleRecovery] Recovery attempted but failed.");
            }

            // B3 fix: do NOT pre-compute grit contest — live ball is the sole fumble arbiter.
            // Turnover is set by ResolveLiveBallEffects → EffectForceTurnover → HandleLiveBallTurnover.
            Debug.Log("[HandleQbFumble] Ball live — live ball phase resolves fumble.");
            return new PlayResolution
            {
                BallIsLive = true,
                ContributingAbilities = new List<AbilityQueueElement> { fumble },
                Turnover = false,
                YardageGained = 0,
            };
        }

        private PlayResolution HandleInterception(AbilityQueueElement failEvent)
        {
            game_data.last_fail_event = FailPlayEventType.Interception;
            game_data.interceptor_uid = failEvent.caster?.uid;
            game_data.fail_event_caster_uid = failEvent.caster?.uid;

            var offGrit = game_data.current_offensive_player.GetCurrentBoardCardGrit();
            var defGrit = game_data.GetCurrentDefensivePlayer().GetCurrentBoardCardGrit();

            var playType = game_data.current_offensive_player.SelectedPlay;

            var netYardageInterceptionPoint =
                game_data.current_offensive_player.GetBoardCardsBaseYardageForPlayType(playType, true) -
                game_data.GetCurrentDefensivePlayer().GetBoardCardsBaseYardageForPlayType(playType, false);

            return new PlayResolution
            {
                BallIsLive = true,
                Turnover = true,
                ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                YardageGained = FootballMath.CalcInterceptionYardage(netYardageInterceptionPoint, offGrit, defGrit)
            };
        }


        private PlayResolution TryToInterceptTippedPass(AbilityQueueElement failEvent)
        {
            var offGrit = game_data.current_offensive_player.GetCurrentBoardCardGrit();
            var defGrit = game_data.GetCurrentDefensivePlayer().GetCurrentBoardCardGrit();

            if (defGrit + 3 >= offGrit && defGrit >= offGrit * 2) // interception!
            {
                game_data.last_fail_event = FailPlayEventType.TippedPass;
                game_data.interceptor_uid = failEvent.caster?.uid;
                game_data.fail_event_caster_uid = failEvent.caster?.uid;
                return new PlayResolution
                {
                    BallIsLive = true,
                    Turnover = true,
                    ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                    YardageGained = (defGrit - offGrit) * 2
                };
            } else // incomplete!
            {
                game_data.last_fail_event = FailPlayEventType.TippedPass;
                game_data.fail_event_caster_uid = failEvent.caster?.uid;
                return new PlayResolution
                {
                    BallIsLive = false,
                    Turnover = game_data.current_down == 4 ? true : false,
                    ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                    YardageGained = 0
                };
            }
        }

        private PlayResolution HandleBattedPass(AbilityQueueElement failEvent)
        {
            game_data.last_fail_event = FailPlayEventType.BattedPass;
            game_data.fail_event_caster_uid = failEvent.caster?.uid;
            return new PlayResolution
            {
                BallIsLive = false,
                Turnover = game_data.current_down == 4 ? true : false,
                ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                YardageGained = 0
            };
        }

        protected virtual PlayResolution HandleFumble(AbilityQueueElement failEvent)
        {
            game_data.last_fail_event = FailPlayEventType.RunnerFumble;
            game_data.fail_event_caster_uid = failEvent.caster?.uid;
            var recoveryCard = game_data.current_offensive_player.cards_board
                .FirstOrDefault(c => c.GetTraitValue("fumble_recovery") > 0
                                  && c.GetTraitValue("fumble_recovery_used") == 0);
            if (recoveryCard != null)
            {
                int chance = recoveryCard.GetTraitValue("fumble_recovery");
                recoveryCard.SetTrait("fumble_recovery_used", 1);
                if (UnityEngine.Random.Range(0, 100) < chance)
                {
                    Debug.Log("[FumbleRecovery] Fumble recovered! Offense keeps the ball.");
                    game_data.fumble_recoverer_uid = recoveryCard.uid;
                    return new PlayResolution { BallIsLive = false, YardageGained = 0,
                        Turnover = false, ContributingAbilities = new List<AbilityQueueElement> { failEvent } };
                }
                Debug.Log("[FumbleRecovery] Recovery attempted but failed.");
            }

            // B3 fix: do NOT pre-compute grit contest — live ball is the sole fumble arbiter.
            // Turnover is set by ResolveLiveBallEffects → EffectForceTurnover → HandleLiveBallTurnover.
            Debug.Log("[HandleFumble] Ball live — live ball phase resolves fumble.");
            return new PlayResolution
            {
                BallIsLive = true,
                ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                Turnover = false,
                YardageGained = 0,
            };
        }

        private PlayResolution HandleIncompletePass(AbilityQueueElement failEvent)
        {
            game_data.last_fail_event = FailPlayEventType.IncompletePass;
            game_data.fail_event_caster_uid = failEvent?.caster?.uid;
            return new PlayResolution
            {
                BallIsLive = false,
                Turnover = game_data.current_down == 4 ? true : false,
                ContributingAbilities = new List<AbilityQueueElement>(),
                YardageGained = 0
            };
        }

        private PlayResolution ResolveRunOutcome()
        {
            // C1 fix: calculate yardage before fail events so fumble gets correct YardageGained
            int baseYardage = game_data.current_offensive_player.head_coach.baseOffenseYardage[PlayType.Run];

            int playerRunBase = game_data.current_offensive_player.cards_board.Sum(p => p.Data.run_bonus);
            int playerAddedBonuses = game_data.current_offensive_player.cards_board
                .Sum(c => c.GetStatusValue(StatusType.AddedRunBonus));

            var defPlayer = game_data.GetCurrentDefensivePlayer();
            var defPlayerCoverageBase = defPlayer.cards_board.Sum(p => p.Data.run_coverage_bonus);
            var defAddedBonuses = defPlayer.cards_board.Sum(c => c.GetStatusValue(StatusType.AddedRunCoverageBonus));
            int defCoachBase = defPlayer.head_coach.baseDefenseYardage[PlayType.Run];

            bool defGuessedRun = defPlayer.SelectedPlay == PlayType.Run;
            int coverageMod = defPlayer.coachManager?.GetCoverageModifier(defGuessedRun) ?? 0;
            int effectiveDefCoverage = Math.Max(0, defPlayerCoverageBase + defCoachBase + coverageMod);

            Debug.Log($"[CoverageGuess] DefGuess={defPlayer.SelectedPlay} Correct={defGuessedRun} CoachBase={defCoachBase} Modifier={coverageMod} Effective={effectiveDefCoverage}");
            int totalYardage = FootballMath.CalcRunYardage(baseYardage, playerRunBase, playerAddedBonuses, effectiveDefCoverage, defAddedBonuses);
            Debug.Log($"[RunResolution] CoachBase={baseYardage} PlayerRunBase={playerRunBase} StatusBonus={playerAddedBonuses} DefCoverage={effectiveDefCoverage} DefStatus={defAddedBonuses} => Total={totalYardage}");

            int preventLoss = game_data.current_offensive_player.cards_board
                .Select(c => c.GetTraitValue("prevent_loss")).DefaultIfEmpty(0).Max();
            if (preventLoss > 0 && totalYardage < 0)
            {
                totalYardage = FootballMath.ApplyPreventLoss(totalYardage, preventLoss);
                Debug.Log($"[PreventLoss] Run yardage clamped to {totalYardage}");
            }

            // Set ball carrier for client animation (best RB; a TE only if no RB is on the field)
            var ballCarrier = BallCarrierSelector.Pick(game_data.current_offensive_player);
            game_data.ball_carrier_uid = ballCarrier?.uid;

            // Stash breakdown for UI
            game_data.yardage_breakdown = new YardageBreakdown
            {
                offCoachBase = baseYardage,
                offPlayerBase = playerRunBase,
                offStatusBonuses = playerAddedBonuses,
                offOtherPositions = 0,
                defCoverageBase = defPlayerCoverageBase,
                defCoachBase = defCoachBase,
                defCoverageMod = coverageMod,
                defStatusBonuses = defAddedBonuses,
                netYardage = totalYardage,
                receiverName = ballCarrier?.CardData?.title ?? "",
            };
            game_data.target_receiver_uid = null;

            var failEvent = ResolveRunFailEvents();
            if (failEvent != null)
            {
                // C1 fix: fumble position = net play yardage (where ball was when it came loose)
                if (failEvent.BallIsLive)
                    failEvent.YardageGained = totalYardage;
                return failEvent;
            }

            foreach (var p in game_data.players)
                p.coachManager?.OnCoachTrigger(CoachTrigger.OnRunPlay);

            return new PlayResolution
            {
                BallIsLive = true,
                ContributingAbilities = new List<AbilityQueueElement>(),
                Turnover = false,
                YardageGained = totalYardage,
            };
        }
    }
}

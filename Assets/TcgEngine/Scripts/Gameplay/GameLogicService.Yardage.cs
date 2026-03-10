using Assets.TcgEngine.Scripts.Effects;
using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    public partial class GameLogicService
    {
        // ── Play Outcome Resolution ──────────────────────────────────────────
        // All 14 methods that form the yardage/resolution pipeline.
        // Pure arithmetic is delegated to FootballMath (static, testable).

        private void ResolvePlayOutcome()
        {
            var playResolution = new PlayResolution();
            // TODO: Implement logic for resolving what happens after a play (e.g., determining success/failure)

            // TODO: order of potential play enders / turnover event priority, (TFL, Sack,tipped pass, batted down pass, interception/fumble)?
            // at this point we will have all the slot based bonuses and events.
            // need a way to look at all the events and action on them in order of operations
            // probably split into two streams here, run or pass.




            // PASS logic
            // look for sack, tipped pass, batted down pass, interception
            // action on those

            // if none, calculate if pass is completed.
            // rank all potential receivers based on bonuses
            // decipher coverage on top receivers to determine who is the best eligible receiver.
            // tabulate net yardage
            // look for fumbles
            // move to live ball phase

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

            if (!playResolution.BallIsLive)
            {
                EndPlayPhase();
                return;
            }

            // Step 8: Move to Live Ball Phase
            StartLiveBallPhase();

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
        private void TriggerBoardSlotAbilities(PlayType playType)
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
                        if (!ability.AreSlotRequirementsMet(game_data.current_slot_data))
                        {
                            Debug.Log($"[SlotDraw] {card.card_id} '{ability.id}' — req not met, skipped.");
                            continue;
                        }

                        // Other trigger conditions (e.g. ConditionOwner, ConditionLastPlayType)
                        if (!ability.AreTriggerConditionsMet(game_data, card)) continue;

                        if (ability.failEventType != FailPlayEventType.None)
                        {
                            // Fail event: enqueue for ResolveRunFailEvents / ResolvePassFailEvents
                            resolve_queue.AddAbility(ability, card, card, null);
                            Debug.Log($"[SlotDraw] Queued fail event: {ability.failEventType} from {card.card_id}");
                        }
                        else
                        {
                            // Stat buff: apply synchronously before yardage is calculated
                            Debug.Log($"[SlotDraw] Firing stat ability '{ability.id}' from {card.card_id}");
                            ResolveCardAbility(ability, card, card);
                        }
                    }
                }
            }
        }

        private PlayResolution ResolvePassOutcome()
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
                    var replQBFail = ScriptableObject.CreateInstance<AbilityData>();
                    replQBFail.trigger = AbilityTrigger.OnPassResolution;
                    replQBFail.failEventType = FailPlayEventType.IncompletePass;
                    resolve_queue.AddAbility(replQBFail, null, null, null);
                    Debug.Log($"[ReplacementQB] Pass incomplete ({(hasWrench ? "wrench" : "no football on long")}).");
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

            var targetReceiver = rankingSystem.ApplyCoverage(game_data);
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
            var otherDefPlayerYardage = defPlayerPass.cards_board
                .Sum(c => (currPlayType == PlayType.LongPass ? c.Data.deep_pass_coverage_bonus : c.Data.short_pass_coverage_bonus) +
                    c.GetStatusValue(currentPlayStatus));
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
                .OrderBy(a => a.ability.failEventType) // Priority-based order
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

                    case FailPlayEventType.Interception:
                        return HandleInterception(failEvent);

                    case FailPlayEventType.BattedPass:
                        return HandleBattedPass(failEvent);

                    case FailPlayEventType.TippedPass:
                        return TryToInterceptTippedPass(failEvent);

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

                    case FailPlayEventType.QBFumble:
                        // Standalone pre-pass fumble (no sack) — ball loose at LOS, live ball resolves turnover
                        if (game_data.current_offensive_player.PlayEnhancer?.card_id == "00308_enh_ball_security")
                        {
                            Debug.Log("[BallSecurity] QB fumble prevented.");
                            break;
                        }
                        return HandleQbFumble(failEvent);

                    case FailPlayEventType.SackFumble:
                        // Sack + fumble in one ability slot
                        if (game_data.current_offensive_player.PlayEnhancer?.card_id == "00307_enh_throw_it_away")
                        {
                            Debug.Log("[ThrowItAway] SackFumble converted to incomplete.");
                            return HandleIncompletePass(failEvent);
                        }
                        if (game_data.current_offensive_player.cards_board.Any(c => c.GetTraitValue("prevent_loss") > 0))
                        {
                            Debug.Log("[PreventLoss] SackFumble converted to incomplete.");
                            return HandleIncompletePass(failEvent);
                        }
                        return HandleSackFumble(failEvent);

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
                .OrderBy(a => a.ability.failEventType) // Priority-based order
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

        private PlayResolution HandleSackFumble(AbilityQueueElement failEvent)
        {
            int sackYards = game_data.current_offensive_player.SelectedPlay == PlayType.LongPass ? -8 : -4;

            var recoveryCard = game_data.current_offensive_player.cards_board
                .FirstOrDefault(c => c.GetTraitValue("fumble_recovery") > 0
                                  && c.GetTraitValue("fumble_recovery_used") == 0);
            if (recoveryCard != null)
            {
                int chance = recoveryCard.GetTraitValue("fumble_recovery");
                recoveryCard.SetTrait("fumble_recovery_used", 1);
                if (UnityEngine.Random.Range(0, 100) < chance)
                {
                    Debug.Log("[FumbleRecovery] SackFumble recovered. Sack yardage applied, no turnover.");
                    return new PlayResolution { BallIsLive = false, YardageGained = sackYards,
                        Turnover = false, ContributingAbilities = new List<AbilityQueueElement> { failEvent } };
                }
                Debug.Log("[FumbleRecovery] SackFumble recovery attempted but failed.");
            }

            Debug.Log($"[SackFumble] Ball live at sack position ({sackYards} yds). Live ball resolves turnover.");
            return new PlayResolution
            {
                BallIsLive = true,
                Turnover = false,
                YardageGained = sackYards,
                ContributingAbilities = new List<AbilityQueueElement> { failEvent }
            };
        }

        protected virtual PlayResolution HandleQbFumble(AbilityQueueElement fumble)
        {
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
                return new PlayResolution
                {
                    BallIsLive = true,
                    Turnover = true,
                    ContributingAbilities = new List<AbilityQueueElement> { failEvent },
                    YardageGained = (defGrit - offGrit) * 2
                };
            } else // incomplete!
            {
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

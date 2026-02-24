using UnityEngine;
using System.Collections.Generic;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Manages coach bonuses and abilities for a player during the game
    /// Coaches are persistent (not cards) and provide passive bonuses + triggered abilities
    /// </summary>
    public class CoachManager
    {
        private CoachData coach;
        private Player player;
        private Game game;
        private GameLogicService gameLogic;
        
        // Track triggered abilities to prevent duplicate triggers in same turn
        private HashSet<string> triggeredThisTurn = new HashSet<string>();

        public CoachManager(CoachData coachData, Player owningPlayer, Game gameData, GameLogicService logic)
        {
            this.coach = coachData;
            this.player = owningPlayer;
            this.game = gameData;
            this.gameLogic = logic;
        }

        /// <summary>
        /// Get offensive bonus yards for a play type
        /// Called DURING play resolution to add to base yards
        /// </summary>
        public int GetOffensiveBonus(PlayType playType)
        {
            if (coach == null) return 0;
            return coach.GetOffensiveBonus(playType);
        }

        /// <summary>
        /// Get defensive coverage penalty to opponent's yards
        /// Called DURING play resolution to subtract from offense yards
        /// </summary>
        public int GetDefensiveCoverage(PlayType offensePlayType)
        {
            if (coach == null) return 0;
            return coach.GetDefensiveCoverage(offensePlayType);
        }

        /// <summary>
        /// Get modifier based on whether defense guessed coverage correctly
        /// Called DURING play resolution AFTER the play result is known
        /// </summary>
        public int GetCoverageModifier(bool defenseGuessedCorrectly)
        {
            if (coach == null) return 0;

            if (defenseGuessedCorrectly)
                return coach.coverageBonusCorrect; // Positive = more yards prevented
            else
                return -coach.coveragePenaltyWrong; // Negative = fewer yards prevented (penalty)
        }

        /// <summary>
        /// Get star limit for a position group
        /// </summary>
        public int GetStarLimit(PlayerPositionGrp posGroup)
        {
            if (coach == null || coach.positionalLimits == null)
                return 99; // No limit

            if (coach.positionalLimits.ContainsKey(posGroup))
                return coach.positionalLimits[posGroup];
            
            return 99; // No limit if not specified
        }

        /// <summary>
        /// Trigger coach ability when an event occurs
        /// Called from GameLogicService at appropriate points in play resolution
        /// </summary>
        public void OnCoachTrigger(CoachTrigger trigger)
        {
            if (coach == null || coach.abilities == null || coach.abilities.Length == 0)
                return;

            foreach (var ability in coach.abilities)
            {
                if (ability.trigger == trigger && ability.effects != null && ability.effects.Length > 0)
                {
                    // Check conditions (if any)
                    bool conditionsMet = true;
                    if (ability.conditions != null && ability.conditions.Length > 0)
                    {
                        foreach (var condition in ability.conditions)
                        {
                            // Coach abilities don't have a card context, so check at game level
                            if (!condition.IsTriggerConditionMet(game, null, null))
                            {
                                conditionsMet = false;
                                break;
                            }
                        }
                    }

                    // Execute effects if conditions met
                    if (conditionsMet)
                    {
                        foreach (var effect in ability.effects)
                        {
                            if (effect != null)
                            {
                                // Coach abilities execute on the player, not a card
                                effect.DoEffect(gameLogic, null, null, player);
                            }
                        }
                        
                        Debug.Log($"Coach ability triggered: {ability.id} for player {player.player_id}");
                    }
                }
            }
        }

        /// <summary>
        /// Reset per-turn tracking (call at start of each turn)
        /// </summary>
        public void ResetTurnState()
        {
            triggeredThisTurn.Clear();
        }
    }
}

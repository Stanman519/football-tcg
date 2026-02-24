using UnityEngine;
using System.Collections.Generic;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Coach risk profile for defensive guessing strategy
    /// Displayed on play call screen to help players understand coach tendencies
    /// </summary>
    public enum CoachType
    {
        Balanced,      // Moderate bonus and penalty - balanced risk/reward
        Aggressive,    // High bonus, high penalty - swingy, risky plays
        Conservative   // Low bonus, low penalty - safe, consistent plays
    }

    /// <summary>
    /// Defines WHEN during play resolution a coach ability can trigger
    /// </summary>
    public enum CoachAbilityPhase
    {
        OnGameStart,          // Game initialization
        OnFirstSnap,          // First play of each drive
        OnPlayResolution,     // During play yards calculation
        OnPlayResult,         // After determining play outcome
        OnEndOfTurn,          // End of turn
    }

    /// <summary>
    /// Coach ability - triggers on game events
    /// </summary>
    [System.Serializable]
    public class CoachAbility
    {
        public string id;
        public CoachTrigger trigger;
        public CoachAbilityPhase phase;  // WHEN this ability fires during game resolution
        public ConditionData[] conditions;
        public EffectData[] effects;
    }

    public enum CoachTrigger
    {
        OnGameStart,      // At start of game
        OnFirstSnap,      // First play of each drive
        OnScore,          // After scoring (TD or FG)
        OnTurnover,       // After turnover (INT or Fumble)
        OnFirstDown,      // After gaining first down
        On3rdDown,        // When facing 3rd down
        On4thDown,        // When facing 4th down
        OnRedZone,        // When entering red zone (ball >= 80)
        OnRunPlay,        // When Run play completes
        OnPassPlay,       // When Pass play completes
        OnDefenseGuess,   // After defense picks coverage
        OnPlayResult,     // After play resolves
    }

    /// <summary>
    /// Coach data asset - create in Unity for each coach
    /// Each player has ONE coach for the entire game
    /// </summary>
    [CreateAssetMenu(fileName = "coach", menuName = "TcgEngine/Coach", order = 5)]
    public class CoachData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Header("Coach Profile")]
        [Tooltip("Risk profile for defensive guessing - shown on play call screen")]
        public CoachType coachType = CoachType.Balanced;

        [Header("Offensive Bonuses (Yards Added to Plays)")]
        [Tooltip("Bonus yards added to all Run plays")]
        public int baseRunBonus = 0;
        [Tooltip("Bonus yards added to all Short Pass plays")]
        public int baseShortPassBonus = 0;
        [Tooltip("Bonus yards added to all Long Pass plays")]
        public int baseLongPassBonus = 0;
        
        [Header("Defensive Coverage (Yards Subtracted from Opponent)")]
        [Tooltip("Yards subtracted from opponent's Run gain")]
        public int coverageRun = 0;
        [Tooltip("Yards subtracted from opponent's Short Pass gain")]
        public int coverageShortPass = 0;
        [Tooltip("Yards subtracted from opponent's Long Pass gain")]
        public int coverageLongPass = 0;
        
        [Header("Coverage Guessing Modifiers")]
        [Tooltip("Additional yards prevented when guess is CORRECT")]
        public int coverageBonusCorrect = 0;
        [Tooltip("Additional yards given up when guess is WRONG")]
        public int coveragePenaltyWrong = 0;
        
        [Header("Positional Star Limits")]
        [Tooltip("Max star players allowed for each position")]
        public Dictionary<PlayerPositionGrp, int> positionalLimits = new Dictionary<PlayerPositionGrp, int>();
        
        [Header("Coach Abilities")]
        [Tooltip("Special abilities that trigger on game events")]
        public CoachAbility[] abilities = new CoachAbility[0];

        public int GetOffensiveBonus(PlayType playType)
        {
            switch (playType)
            {
                case PlayType.Run: return baseRunBonus;
                case PlayType.ShortPass: return baseShortPassBonus;
                case PlayType.LongPass: return baseLongPassBonus;
                default: return 0;
            }
        }

        public int GetDefensiveCoverage(PlayType offensePlayType)
        {
            switch (offensePlayType)
            {
                case PlayType.Run: return coverageRun;
                case PlayType.ShortPass: return coverageShortPass;
                case PlayType.LongPass: return coverageLongPass;
                default: return 0;
            }
        }
    }
}

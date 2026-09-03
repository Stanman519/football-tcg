# Coach System Architecture

## Overview
Coaches are not cards - they're persistent game entities that provide passive bonuses to the player. Each player has one coach for the entire game.

---

## Coach Data Structure

### HeadCoachCard (per player):
```
- baseOffenseYardage: { Run, ShortPass, LongPass } → bonus yards
- baseDefenseYardage: { Run, ShortPass, LongPass } → subtracted from opponent's gain
- positional_Scheme: { PositionGroup → max_stars } → star player limits
- abilities: CoachAbility[] → triggered effects
- coverageBonus: int → bonus when guess correct
- coveragePenalty: int → penalty when guess wrong
```

---

## New Files to Create

### 1. CoachData.cs (ScriptableObject)
Location: `Assets/TcgEngine/Scripts/`

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace TcgEngine
{
    /// <summary>
    /// Coach ability - triggers on game events
    /// </summary>
    [System.Serializable]
    public class CoachAbility
    {
        public string id;
        public CoachTrigger trigger;
        public ConditionData[] conditions;
        public EffectData[] effects;
        public int value;
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
        OnRedZone,        // When entering red zone
        OnRunPlay,        // When Run is called (offense)
        OnPassPlay,       // When Pass is called (offense)
        OnDefenseGuess,   // After defense picks coverage
        OnPlayResult,    // After play resolves
    }

    /// <summary>
    /// Coach data asset - create in Unity for each coach
    /// </summary>
    [CreateAssetMenu(fileName = "coach", menuName = "TcgEngine/Coach", order = 5)]
    public class CoachData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        
        [Header("Offensive Bonuses")]
        [Tooltip("Bonus yards added to Run plays")]
        public int baseRunBonus = 0;
        [Tooltip("Bonus yards added to Short Pass plays")]
        public int baseShortPassBonus = 0;
        [Tooltip("Bonus yards added to Long Pass plays")]
        public int baseLongPassBonus = 0;
        
        [Header("Defensive Coverage")]
        [Tooltip("Yards subtracted from opponent's Run gain")]
        public int coverageRun = 0;
        [Tooltip("Yards subtracted from opponent's Short Pass gain")]
        public int coverageShortPass = 0;
        [Tooltip("Yards subtracted from opponent's Long Pass gain")]
        public int coverageLongPass = 0;
        
        [Header("Coverage Guessing")]
        [Tooltip("Bonus yards prevented when guess is CORRECT")]
        public int coverageBonusCorrect = 0;
        [Tooltip("Extra yards given up when guess is WRONG")]
        public int coveragePenaltyWrong = 0;
        
        [Header("Star Position Limits")]
        [Tooltip("Max stars allowed for each position group")]
        public Dictionary<PlayerPositionGrp, int> positionalLimits = new Dictionary<PlayerPositionGrp, int>();
        
        [Header("Coach Abilities")]
        public CoachAbility[] abilities;
    }
}
```

### 2. CoachManager.cs
Location: `Assets/TcgEngine/Scripts/Gameplay/`

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace TcgEngine
{
    /// <summary>
    /// Manages coach bonuses and abilities for a player
    /// </summary>
    public class CoachManager
    {
        private CoachData coach;
        private Player player;
        private Game game;
        
        // Track one-time abilities
        private HashSet<string> usedOneTimeAbilities = new HashSet<string>();

        public CoachManager(CoachData coach, Player player, Game game)
        {
            this.coach = coach;
            this.player = player;
            this.game = game;
        }

        /// <summary>
        /// Get offensive bonus for a play type (used in play resolution)
        /// </summary>
        public int GetOffensiveBonus(PlayType playType)
        {
            switch (playType)
            {
                case PlayType.Run: return coach.baseRunBonus;
                case PlayType.ShortPass: return coach.baseShortPassBonus;
                case PlayType.LongPass: return coach.baseLongPassBonus;
                default: return 0;
            }
        }

        /// <summary>
        /// Get defensive coverage for opponent's play type
        /// </summary>
        public int GetDefensiveCoverage(PlayType offensePlayType)
        {
            switch (offensePlayType)
            {
                case PlayType.Run: return coach.coverageRun;
                case PlayType.ShortPass: return coach.coverageShortPass;
                case PlayType.LongPass: return coach.coverageLongPass;
                default: return 0;
            }
        }

        /// <summary>
        /// Get coverage modifier based on guess accuracy
        /// </summary>
        public int GetCoverageModifier(bool defenseGuessedCorrectly)
        {
            if (defenseGuessedCorrectly)
                return coach.coverageBonusCorrect; // Prevent more yards
            else
                return -coach.coveragePenaltyWrong; // Give up more yards
        }

        /// <summary>
        /// Get star limit for a position group
        /// </summary>
        public int GetStarLimit(PlayerPositionGrp posGroup)
        {
            if (coach.positionalLimits != null && coach.positionalLimits.ContainsKey(posGroup))
                return coach.positionalLimits[posGroup];
            return 99; // No limit
        }

        /// <summary>
        /// Called when a coach trigger event happens
        /// </summary>
        public void OnTrigger(CoachTrigger trigger)
        {
            if (coach.abilities == null) return;

            foreach (var ability in coach.abilities)
            {
                if (ability.trigger == trigger)
                {
                    // Check conditions
                    if (AbilityData.CheckConditions(ability.conditions, game, player))
                    {
                        // Execute effects
                        AbilityData.ExecuteEffects(ability.effects, game, player);
                    }
                }
            }
        }
    }
}
```

### 3. Modify Player.cs
Add to Player class:

```csharp
public class Player
{
    // Add these fields:
    public CoachData coach;
    public CoachManager coachManager;
    
    // Add this method:
    public void SetCoach(CoachData coachData)
    {
        this.coach = coachData;
        this.coachManager = new CoachManager(coachData, this, null);
    }
}
```

### 4. Modify Game.cs (play resolution)

When resolving a play, apply coach bonuses:

```csharp
// In play resolution (where yards are calculated):
int offensiveBonus = offensivePlayer.coachManager.GetOffensiveBonus(playType);
int defensiveCoverage = defensivePlayer.coachManager.GetDefensiveCoverage(playType);
bool guessedCorrectly = (play.offensive_play == play.defensive_play);
int coverageModifier = defensivePlayer.coachManager.GetCoverageModifier(guessedCorrectly);

// Final yards:
int finalYards = baseYards + offensiveBonus - defensiveCoverage + coverageModifier;
```

---

## Example Coaches (To Create in Unity)

### Risk-Taker
```
Display Name: Risk-Taker
Description: "Aggressive playcalling with high risk, high reward"

Offensive Bonuses:
- Run: +1
- Short Pass: +0
- Long Pass: +2

Defensive Coverage:
- Run: -1
- Short Pass: -1  
- Long Pass: -2

Coverage Guessing:
- Correct: +1 (prevents 1 extra yard)
- Wrong: +0

Positional Limits: (none)

Abilities:
- OnFirstSnap: EffectAddSlotSymbol (WildCard)
```

### Play Fast!
```
Display Name: Play Fast!
Description: "Exploit defenses before they can react"

Offensive Bonuses:
- Run: +2
- Short Pass: +1
- Long Pass: +0

Defensive Coverage: (default)

Abilities:
- OnFirstDown: EffectModifyPlayCount (exempt this play from countdown)
```

### Red Zone Guru
```
Display Name: Red Zone Guru
Description: "Master of close-field offense"

Offensive Bonuses:
- Run: +3 (always)
- Short Pass: +1
- Long Pass: +0

Abilities:
- Trigger: OnRedZone (ball_on >= 80)
- Effect: +2 additional run bonus
```

---

## Integration Points

1. **Game Start** → Initialize coaches, call `coachManager.OnTrigger(CoachTrigger.OnGameStart)`
2. **Play Resolution** → Apply `GetOffensiveBonus()` and `GetDefensiveCoverage()` to yardage
3. **After Play** → Check guess, apply `GetCoverageModifier()`
4. **On First Snap** → `offensivePlayer.coachManager.OnTrigger(CoachTrigger.OnFirstSnap)`
5. **After Score** → `scoringPlayer.coachManager.OnTrigger(CoachTrigger.OnScore)`
6. **After First Down** → `coachManager.OnTrigger(CoachTrigger.OnFirstDown)`

---

## GitHub Copilot / Claude Prompt

```
I need to implement a coach system for my football card game in Unity.

Context:
- Each player has ONE coach for the entire game (not a card you draw)
- Coaches provide offensive bonuses (run/pass yardage) AND defensive coverage (reduce opponent gains)
- Coaches also have triggered abilities (draw cards on 4th down, add slots, etc.)
- Coaches have positional star limits

Please help me:
1. Create CoachData.cs - ScriptableObject with:
   - Offensive bonuses (baseRunBonus, baseShortPassBonus, baseLongPassBonus)
   - Defensive coverage (coverageRun, coverageShortPass, coverageLongPass)
   - Coverage guessing (coverageBonusCorrect, coveragePenaltyWrong)
   - Positional limits (Dictionary<PlayerPositionGrp, int>)
   - Abilities array

2. Create CoachManager.cs - handles:
   - GetOffensiveBonus(PlayType) - returns bonus yards
   - GetDefensiveCoverage(PlayType) - returns coverage yards to subtract
   - GetCoverageModifier(bool guessedCorrectly) - extra bonus/penalty
   - GetStarLimit(PlayerPositionGrp) - max stars allowed
   - OnTrigger(CoachTrigger) - fires coach abilities

3. Modify Player.cs to add coach and coachManager fields

4. In Game.cs play resolution, apply coach bonuses to yardage calculation

Use TcgEngine namespace. Follow existing patterns from the codebase.
```

---

## Files to Modify Summary
- `Assets/TcgEngine/Scripts/CoachData.cs` (NEW)
- `Assets/TcgEngine/Scripts/Gameplay/CoachManager.cs` (NEW)
- `Assets/TcgEngine/Scripts/Gameplay/Player.cs` (ADD fields)
- `Assets/TcgEngine/Scripts/Gameplay/Game.cs` (ADD coach initialization + apply bonuses in play resolution)

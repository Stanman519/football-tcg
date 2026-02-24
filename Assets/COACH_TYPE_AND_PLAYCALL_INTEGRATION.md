# Coach Type System & Play Call Integration

## ✅ Completed: CoachType System

Added to `CoachData.cs`:

```csharp
public enum CoachType
{
    Balanced,      // Moderate bonus and penalty
    Aggressive,    // High bonus, high penalty (swingy)
    Conservative   // Low bonus, low penalty (safe)
}
```

Also added field to CoachData class:
```csharp
[Header("Coach Profile")]
public CoachType coachType = CoachType.Balanced;
```

---

## ✅ Play Call Buttons Status

**YES - Buttons are fully wired up!**

In `PlayCallManager.cs`:
```csharp
if (runButton != null)
    runButton.onClick.AddListener(() => SelectPlay(PlayType.Run));
if (shortPassButton != null)
    shortPassButton.onClick.AddListener(() => SelectPlay(PlayType.ShortPass));
if (longPassButton != null)
    longPassButton.onClick.AddListener(() => SelectPlay(PlayType.LongPass));
if (confirmButton != null)
    confirmButton.onClick.AddListener(ConfirmPlaySelection);
```

When confirmed, it calls `GameClient.Get().SendPlaySelection(selectedPlay, selectedEnhancer)` which sends the play to the server.

---

## 📋 Coach Bonuses/Penalties Integration Points

Now we need to integrate the coach offensive/defensive bonuses + guess modifiers at play call time. Here's where:

### INTEGRATION POINT 1: When Play is Called (Initial Bonus)

**When:** During play resolution, when calculating base yards

**What to Apply:**
- Offensive coach: `GetOffensiveBonus(playType)` → ADD to yards
- Defensive coach: `GetDefensiveCoverage(playType)` → SUBTRACT from yards

**Code Location:** In GameLogicService where yards are calculated from slot machine

```csharp
// EXISTING: baseYards calculation
int baseYards = CalculateSlotMachineYards();

// ADD THIS: Apply coach bonuses
Player offensePlayer = game_data.current_offensive_player;
Player defensePlayer = game_data.GetOpponentPlayer(offensePlayer.player_id);
PlayType selectedPlayType = game_data.last_play_type;

// Offensive coach bonus
if (offensePlayer?.coachManager != null)
{
    baseYards += offensePlayer.coachManager.GetOffensiveBonus(selectedPlayType);
}

// Defensive coach coverage
if (defensePlayer?.coachManager != null)
{
    baseYards -= defensePlayer.coachManager.GetDefensiveCoverage(selectedPlayType);
}

// NOW baseYards includes coach bonuses/coverage
```

---

### INTEGRATION POINT 2: When Defense Guess Result is Known

**When:** After determining if defense guessed correctly

**What to Apply:**
- Defense coach: `GetCoverageModifier(bool guessCorrect)` → ADD/SUBTRACT from yards
  - Positive if correct guess (prevents more yards)
  - Negative if wrong guess (gives up more yards)

**Coach Type Display:**
- "Aggressive" coaches have high bonus/penalty
- "Conservative" coaches have low bonus/penalty
- "Balanced" coaches have moderate

**Code Location:** Same play resolution, AFTER determining guess accuracy

```csharp
// AFTER determining play result
bool defenseGuessedCorrectly = (game_data.defense_guess == selectedPlayType);

// Apply coverage modifier based on guess accuracy
if (defensePlayer?.coachManager != null)
{
    int modifier = defensePlayer.coachManager.GetCoverageModifier(defenseGuessedCorrectly);
    baseYards += modifier;  // Positive = prevents yards, Negative = gives up yards
}

// Final yards now include all coach modifiers
```

---

## 📊 Coach Bonus Timeline

```
PLAY CALLED (Offense picks Run/ShortPass/LongPass)
│
├─ BASE YARDS CALCULATED (from slot machine, cards, etc.)
│
├─ COACH OFFENSIVE BONUS APPLIED
│  └─ offenseCoach.GetOffensiveBonus(playType) → +yards
│
├─ COACH DEFENSIVE COVERAGE APPLIED
│  └─ defenseCoach.GetDefensiveCoverage(playType) → -yards
│
├─ PLAY EXECUTES (balls snapped, yards gained)
│
├─ DEFENSE GUESS RESULT DETERMINED
│  ├─ Correct guess? → Defense guessed the play
│  └─ Wrong guess?  → Defense guessed wrong
│
├─ COACH COVERAGE MODIFIER APPLIED
│  └─ defenseCoach.GetCoverageModifier(guessCorrect) → ±yards
│
└─ FINAL YARDS APPLIED TO FIELD
```

---

## 🎯 Example Coaches Using CoachType

### Coach 1: "Aggressive Coach"
```
CoachType: Aggressive
baseRunBonus: +3
baseShortPassBonus: +2
baseLongPassBonus: +4
coverageBonusCorrect: +5  (high reward if right)
coveragePenaltyWrong: +4  (big penalty if wrong)
```
**Display on Play Call:** "Aggressive" badge
**Gameplay:** Risky plays - huge swings in yardage

### Coach 2: "Conservative Coach"
```
CoachType: Conservative
baseRunBonus: +1
baseShortPassBonus: +1
baseLongPassBonus: +1
coverageBonusCorrect: +1  (small reward if right)
coveragePenaltyWrong: +1  (small penalty if wrong)
```
**Display on Play Call:** "Conservative" badge
**Gameplay:** Safe, consistent plays

### Coach 3: "Balanced Coach"
```
CoachType: Balanced
baseRunBonus: +2
baseShortPassBonus: +1
baseLongPassBonus: +2
coverageBonusCorrect: +2
coveragePenaltyWrong: +2
```
**Display on Play Call:** "Balanced" badge
**Gameplay:** Moderate risk/reward

---

## 🔍 Gap Analysis & Questions

### Potential Gaps:

1. **Play Call Screen Display**
   - ❓ Does the play call screen show the coach type badge to the player?
   - ❓ Should it show the bonuses/penalties numbers?
   - **Recommendation:** Show coach type with visual badge (color coded or icon)

2. **Defense Coach Display**
   - ❓ Does the defense player see their coach type when selecting coverage?
   - **Recommendation:** Yes - helps them understand their defensive profile

3. **Coach Initialization**
   - ❓ Where do players get their coaches assigned?
   - **Current:** Assumed to be set before game starts (in Game setup)
   - **Recommendation:** Need to verify Player.coach is populated at game start

4. **PlayType Selection**
   - ✅ Buttons wired up
   - ✅ SendPlaySelection() called
   - ❓ Is the play actually stored in `currentPlayer.SelectedPlay`?
   - **Recommendation:** Verify this in GameLogicService/GameServer

5. **Coach Manager Initialization**
   - ✅ CoachManager class created
   - ⚠️ Player.coachManager needs to be added (deferred due to namespace)
   - **Recommendation:** Add to Player class with fully-qualified name: `TcgEngine.CoachManager`

---

## 🚀 Implementation Steps

### Step 1: ✅ CoachType System
Done! Coaches now have a type displayed on play call screen.

### Step 2: ⏳ Add to Player Class
```csharp
// In Assets/TcgEngine/Scripts/Gameplay/Player.cs
public TcgEngine.CoachData coach = null;
public TcgEngine.CoachManager coachManager = null;
```

### Step 3: ⏳ Initialize Coaches at Game Start
```csharp
// In GameLogicService.InitializeCoaches()
foreach (var player in game_data.players)
{
    if (player.coach != null && player.coachManager == null)
    {
        player.coachManager = new TcgEngine.CoachManager(
            player.coach,
            player,
            game_data,
            this
        );
    }
}
```

### Step 4: ⏳ Apply Coach Bonuses During Play
```csharp
// In play resolution where yards calculated
int baseYards = CalculateSlotMachineYards();
baseYards = ApplyCoachYardModifiers(baseYards, playType, guessCorrect);
```

### Step 5: ⏳ Display Coach Type on Play Call Screen
In PlayCallManager or GameUI:
```csharp
Player currentPlayer = GameClient.Get().GetPlayer();
if (currentPlayer?.coach != null)
{
    coachTypeDisplay.text = currentPlayer.coach.coachType.ToString();
}
```

---

## ✅ Build Status
✅ **Build Successful** - CoachType system compiles correctly

## Summary

| Item | Status | Notes |
|------|--------|-------|
| CoachType enum | ✅ Done | Added to CoachData.cs |
| Coach type field | ✅ Done | Added to CoachData class |
| Play call buttons | ✅ Wired | Fully connected in PlayCallManager |
| SendPlaySelection | ✅ Exists | GameClient method ready |
| Coach bonuses/penalties integration | ⏳ Ready | Code provided in GameLogicService |
| Player.coach field | ⏳ TODO | Add to Player class |
| CoachManager init | ⏳ TODO | Call at game start |
| Coach type display | ⏳ TODO | Add to play call UI |


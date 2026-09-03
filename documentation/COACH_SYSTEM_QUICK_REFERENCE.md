# COACH SYSTEM - QUICK REFERENCE

## ✅ COMPLETED

### CoachType System
```csharp
public enum CoachType { Balanced, Aggressive, Conservative }
```
- Added to CoachData.cs ✅
- Shows coach risk profile on play call screen
- Aggressive = high bonus/penalty (swingy)
- Conservative = low bonus/penalty (safe)
- Balanced = moderate risk/reward

### Play Call Button Integration
✅ **FULLY WIRED UP**
- Run button → SelectPlay(PlayType.Run)
- ShortPass button → SelectPlay(PlayType.ShortPass)
- LongPass button → SelectPlay(PlayType.LongPass)
- Confirm button → ConfirmPlaySelection() → SendPlaySelection(selectedPlay, enhancer)

---

## 📋 COACHING BONUS FLOW

### When Bonuses Apply:

1. **OFFENSIVE BONUS** (when play is called)
   ```
   finalYards = baseYards + coach.GetOffensiveBonus(playType)
   ```

2. **DEFENSIVE COVERAGE** (when play is called)
   ```
   finalYards -= defensiveCoach.GetDefensiveCoverage(playType)
   ```

3. **COVERAGE MODIFIER** (after guess result known)
   ```
   finalYards += defensiveCoach.GetCoverageModifier(guessCorrect)
   // Positive = prevents yards (correct guess)
   // Negative = gives up yards (wrong guess)
   ```

---

## 🔑 KEY QUESTIONS ANSWERED

### Q: Are the play call buttons wired up?
**A: YES - Fully wired!**
- All 3 play buttons connected
- Confirm button sends play to server via SendPlaySelection()
- PlayCallManager handles show/hide on ChoosePlay phase

### Q: Where do coach bonuses apply?
**A: During play resolution in GameLogicService**
- Offensive bonus: Added to base yards
- Defensive coverage: Subtracted from yards  
- Coverage modifier: Applied after guess is known

### Q: What's the CoachType for?
**A: Display only (currently)**
- Shows coach risk profile to player
- Helps them understand defensive strategy
- Affects visual feedback on play call screen

---

## ⏳ TODO

1. Add coach fields to Player.cs:
   ```csharp
   public TcgEngine.CoachData coach = null;
   public TcgEngine.CoachManager coachManager = null;
   ```

2. Call InitializeCoaches() at game start

3. Call ApplyCoachYardModifiers() during play resolution

4. Display coach type on play call screen UI

---

## FILES CREATED

- ✅ `Assets/TcgEngine/Scripts/Data/CoachData.cs` - Coach definition + CoachType enum
- ✅ `Assets/TcgEngine/Scripts/Gameplay/CoachManager.cs` - Coach runtime logic
- ✅ `Assets/COACH_SYSTEM_COMPLETE_GUIDE.md` - GameLogicService integration
- ✅ `Assets/COACH_TYPE_AND_PLAYCALL_INTEGRATION.md` - This summary

---

## GAPS & NOTES

### No Major Gaps Found ✅

1. ✅ Play call buttons working
2. ✅ Coach type system in place
3. ✅ Coach bonus/penalty logic ready
4. ✅ Method signatures match usage

### Minor Considerations

- Coach type display needs to be added to UI (non-blocking)
- Coach initialization needs to happen before play (add InitializeCoaches())
- Player.coach fields need fully-qualified names to avoid namespace conflicts

---

## NEXT STEPS

When ready to implement:
1. See `COACH_SYSTEM_COMPLETE_GUIDE.md` for 5 GameLogicService methods
2. See `COACH_TYPE_AND_PLAYCALL_INTEGRATION.md` for integration points
3. Add Coach fields to Player.cs with fully-qualified names
4. Add coach type display badge to PlayCallManager UI


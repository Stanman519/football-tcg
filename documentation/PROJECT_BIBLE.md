# PROJECT BIBLE - First & Long TCG

> Last Updated: 2026-02-24 (codebase walkthrough complete)

---

## 1. ARCHITECTURE

### Core Services (The 3 Pillars)
| Class | Responsibility |
|-------|---------------|
| **GameClient** | Client-side: sends actions, receives state refreshes, UI events |
| **GameServer** | Server-side: receives actions, validates, runs AI |
| **GameLogicService** | Core engine: all game rules, phase transitions, ability triggers |

### Data Flow
```
Player Input → GameClient.SendAction() → Network → GameServer.ReceiveAction() 
    → GameLogicService.PlayCard() → onCardPlayed Event → Client Refresh
```

### How Abilities Work
- **Trigger System**: `TriggerCardAbilityType(AbilityTrigger type, Card caster)` iterates cards, fires matching abilities
- **Conditions**: Checked via `AreTargetConditionsMet()` before effect executes  
- **Effects**: Executed via `DoEffect()` after conditions pass
- **Registration**: Unity Create → TcgEngine menu registers them as ScriptableObjects

### Patterns Used
- **Events**: UnityAction delegates (onCardPlayed, onTurnStart, etc.)
- **Phase System**: GamePhase enum controls valid actions
- **ResolveQueue**: Handles delayed/action-queued execution

---

## 2. GAME FLOW

### Phase Progression
```
Mulligan → ChoosePlayers → RevealPlayers → ChoosePlay → RevealPlayCalls 
    → SlotSpin → ResolveOutcome → LiveBall → EndPlay → (next play)
```

### Key State Variables (Game.cs)
- `current_down` (1-4)
- `raw_ball_on` (0-100)
- `current_offensive_player`
- `phase` (GamePhase enum)
- `temp_slot_modifiers` (List<SlotModifier>)

---

## 2. CORE RULES (From Original Google Doc)

### What Every Developer Must Know

**POSSESSION & DOWNS**
- Each possession is exactly 4 plays max. No first downs. If you don't score in 4 tries, you punt, kick FG, or turn it over on downs.
- Each drive starts at the 25 yard line (your own 25).

**TURNOVERS**
- Fumble: Team with more grit recovers. Return yardage = 2x grit difference.
- Interception: Defense returns the ball. Return yardage = 2x defender's grit surplus.
- Grit is a stat on every player card that determines turnover outcomes.

**SUPERSTARS**
- You can only have ONE superstar on the field at a time. If you play another, the first one is removed (or you can't play until you bench the current one).

**STAMINA = PLAYS BEFORE EXHAUSTION**
- Every player card has a stamina value.
- Each time that player is on the field for a play, stamina decreases by 1.
- When stamina hits 0, the player is benched (removed from field, must be replaced).
- Some abilities can increase or decrease stamina.

**PASSING & RECEIVERS**
- On a pass play, only ONE receiver's bonus applies: the one with the highest bonus for that pass type (short/deep).
- Example: WR has +4 deep pass, TE has +2 deep pass, RB has +1 deep pass. You call deep pass → WR is primary (+4 applies).
- This matters because defensive cards can target "top receiver" and negate their bonus.

**PERFECT COVERAGE (Key Defensive Mechanic)**
- Defensive cards can have "perfect coverage on [receiver type] for [pass type]"
- If perfect coverage hits the primary receiver, that receiver's bonus is NEGATED.
- The next-highest bonus receiver becomes primary instead.
- Example: Defense plays "perfect coverage on top receiver for deep passes." You have WR (+4 deep) and TE (+2 deep). WR's bonus is negated → TE becomes primary with +2.

**COACH COVERAGE BONUS/PENALTY**
- Head coach card has: coverage bonus (for correct guess) and coverage penalty (for wrong guess).
- When defense guesses correctly → subtract extra yards from offense.
- When defense guesses wrong → offense gets extra yards.

**CARD DRAW**
- After each play, both players draw cards. Default: 1 card each per play.
- Some abilities can draw more or make opponent discard.

---

## 3. CARD DATABASE

### Player Cards (~130 total)
| Position | Count |
|----------|-------|
| OL | 47 |
| WR | 27 |
| RB | 17 |
| DL | 20 |
| LB | 14 |
| DB | 7 |
| QB | 5 |
| TE | 3 |

### Play Enhancers (~28)
- Run (11), Pass (9), Short Pass (5), Deep Pass (3), General (5)

---

## 4. CONDITIONS (What We Have)

| Condition | Purpose |
|-----------|---------|
| ConditionGameDown | Check current down (1-4) |
| ConditionFieldPosition | Red zone, opponent territory |
| ConditionFieldPositionScenario | Specific field positions (backed up, goal line, etc.) |
| ConditionLastPlayType | Previous play was run/pass |
| ConditionConsecutivePlays | Same play type multiple times |
| ConditionStarCount | Number of star players on field |
| ConditionBoardPositionCount | OL/DL/etc count on board |
| ConditionCompareTwoBoardPositionCounts | "If OL > DL" |
| ConditionGritCompare | Compare grit to opponent |
| ConditionStaminaCompare | Compare stamina to opponent |
| ConditionScoreDiff | Points behind/ahead |
| ConditionTimeRemaining | End of half, 2-minute |
| ConditionPlaysRemaining | Plays left in half |
| ConditionPositionVs | Specific position matchup |
| ConditionPositionCountDiff | Position count difference |
| ConditionDownAndDistance | 3rd & short, 4th & goal |
| ConditionClockMode | 2-minute drill, prevent, hurry-up |
| ConditionMomentum | Consecutive plays, heating up |
| ConditionCoverageType | Man vs zone (not yet implemented) |

---

## 5. EFFECTS (What We Have)

| Effect | Purpose |
|--------|---------|
| EffectAddSlotSymbol | Add wild/reel symbols |
| EffectModifyPlayCount | Exempt plays from consecutive counter |
| EffectModifyStamina | Modify card stamina |
| EffectModifyGrit | Modify card grit |
| EffectPreventLoss | Prevent yardage loss |
| EffectRespin | Trigger slot respin |
| EffectCharge | Charge up for next play |
| EffectAlwaysScore | Guarantee positive yardage |
| EffectFumbleRecovery | Fumble recovery chance |
| EffectAddGrit | Add grit to card |
| EffectMulti | Multiple effects in sequence |
| EffectConditional | If X then A else B |

---

## 6. COACHES (8 Total)

| Coach | Trigger | Status |
|-------|---------|--------|
| **Risk-Taker** | 4th down | Needs EffectAddSlotSymbol |
| **Turnover Tactician** | INT/fumble | Ready |
| **Sideline Motivator** | Star players | Ready |
| **Balanced Approach** | Different play each down | Ready |
| **Red Zone Guru** | Inside 20 | Needs ConditionFieldPosition |
| **Play Fast!** | 1st down gained | Needs EffectModifyPlayCount |
| **Momentum Shifter** | After TD | Needs `after_touchdown` flag |
| **High-Octane Offense** | After FG | Needs `after_field_goal` flag |

---

## 7. SLOT MACHINE

### Default Reels
| Reel | Symbols |
|------|---------|
| Left | 3 Football, 2 Helmet, 1 Star, 1 Wrench |
| Middle | 3 Football, 2 Helmet, 1 Star, 1 Wrench, 1 Wild |
| Right | 3 Football, 3 Helmet, 1 Star, 1 Wrench |

### Probabilities (approximate)
- 1 Star ≈ 36%
- 1+ Football ≈ 80%
- 1+ Helmet ≈ 75%

---

## 8. KEY BUGS FIXED

1. **HeadCoachCard.positional_Scheme was null** - Added constructor to initialize dictionary
2. **Server not starting** - Added null checks in ServerManagerLocal
3. **PlayCard validation** - Cards rejected due to uninitialized position scheme

---

## 9. KNOWN BUGS / TODOS (Active)

| Bug | File | Description |
|-----|------|-------------|
| `playerRunBase` not used | GameLogicService.cs:2700 | Run bonus from cards_board summed but not added to YardageGained |
| Defense yardage not subtracted | GameLogicService.cs:2370 | Defensive coverage computed but not subtracted from pass YardageGained |
| `GameData.` compile error | GameLogicService.cs:2670 | `ResolvePlay()` calls undefined static `GameData.GetCurrentDefensivePlayer()` |
| `PlayerPositionGrp.P = 2` | Card.cs:47 | P and RB_TE both equal 2, enum conflict |
| Test coach hard-coded | Game.cs:InitializeTestGame | Hard-coded HeadCoachCard data; no real coach asset system yet |
| `WaitForPlayerSelection` | GameLogicService.cs:692 | Progresses if either player placed a card (should be both) |
| `CheckForWinner` placeholder | GameLogicService.cs:596 | Wins awarded to current offensive player; no real score comparison |
| Pass completion reads middle only | GameLogicService.cs:2327 | Only `.Middle.IconId` checked, not top/bottom rows |

---

## 10. FILES REFERENCE

| File | Path | Purpose |
|------|------|---------|
| GameLogicService.cs | Scripts/Gameplay/ | Core game logic, ~2720 lines |
| Game.cs | Scripts/Gameplay/ | Game state (enums, PlayHistory, ball position) |
| Player.cs | Scripts/Gameplay/ | Player data + head_coach + cards collections |
| HeadCoachCard.cs | Scripts/Gameplay/ | Coach scheme (positional_Scheme, baseYardage, completionReqs) |
| Card.cs | Scripts/Gameplay/ | Runtime card state + PlayerPositionGrp enum |
| CardData.cs | Scripts/Data/ | ScriptableObject card definition (all stats) |
| ReceiverRankingSystem.cs | Scripts/Gameplay/ | Ranks receivers, applies CoverTopReceiver coverage |
| PlayResolution.cs | Scripts/Gameplay/ | Simple DTO: BallIsLive, YardageGained, Turnover |
| SlotMachineManager.cs | Scripts/Gameplay/ | Slot spin math + modifier application |
| GameServer.cs | Scripts/GameServer/ | Server action handling, owns GameLogicService |
| GameClient.cs | Scripts/GameClient/ | Client singleton, sends actions, receives refreshes |
| AIPlayerMM.cs | Scripts/AI/ | MinMax AI (acts in ChoosePlay/ChoosePlayers/LiveBall) |
| PlayCallManager.cs | Scripts/UI/ | Run/ShortPass/LongPass button UI |
| FieldSlotManager.cs | Scripts/GameClient/ | Spawns BoardSlot GameObjects per coach scheme |

---

## 11. ENUMS QUICK REFERENCE

```
PlayType:       Huddle | Run | ShortPass | LongPass
                NOTE: bible previously said "Deep Pass" but code uses LongPass

GamePhase:      None=0, Mulligan=5, StartTurn=10, ChoosePlayers=11, RevealPlayers=12,
                ChoosePlay=13, RevealPlayCalls=14, SlotSpin=15, Resolution=20,
                LiveBall=30, EndTurn=40

PlayerPositionGrp: NONE=0, QB=1, RB_TE=2, WR=3, OL=5, DL=10, LB=11, DB=12, K=20, P=2
                   WARNING: P and RB_TE both = 2 (enum conflict!)

FailPlayEventType: None=0, TackleForLoss=1, Sack=2, QBFumble=3, BattedPass=4,
                   TippedPass=5, Interception=6, IncompletePass=7, RunnerFumble=8
```

---

*This bible is the single source of truth for the First & Long project.*

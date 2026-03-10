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

**CTR/CNR COVERAGE SYSTEM (Key Defensive Mechanic)**
- **Cover Top Receiver (CTR)**: removes the bonus of the highest net receiver this play.
- **Cover Next Receiver (CNR)**: removes one additional receiver's bonus, but only if at least one receiver is already being covered (by CTR or a prior CNR chain).
- Resolution: count all CTR+CNR players on field. If 0 CTRs → no receivers covered. If ≥1 CTR → skip the top N receivers (N = total CTR+CNR count), return the next uncovered receiver as primary.
- If all receivers are covered → zero receiver bonus (other OL/QB bonuses still apply).
- Examples:
  - 1 CTR: covers #1, #2 gets bonus.
  - 1 CNR alone: covers nobody, #1 gets bonus.
  - 1 CTR + 1 CNR: covers #1 and #2, #3 gets bonus.
  - 2 CTRs: covers #1 and #2, #3 gets bonus.
  - 2 CNRs: covers nobody, #1 gets bonus.
- A DB playing CTR/CNR still contributes their raw coverage stat to yardage reduction regardless.

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

## 7. LIVE BALL PHASE

### Overview
Live ball starts after `ResolvePlayOutcome` returns `BallIsLive = true` (no sack/TFL/INT from fail events, no score/safety). Both players secretly select one live ball card (or pass), reveal simultaneously, resolve by priority. Live ball cards are in the main deck/hand (like MTG artifacts).

### When Live Ball Does NOT Happen
- Incompletion → dead ball, next down
- Interception (from fail events) → possession already switched
- Sack / TFL → ball carrier stopped behind line
- QB Fumble → already resolved as turnover
- Yardage reaches end zone (TD) or ≤0 (Safety)

### Play Eligibility ("Public Mana")
- Slot result is **public** — both players see it before choosing
- `CardData.slotRequirements[]` = slot icons required to play (like mana cost)
- `CardData.AreSlotRequirementsMet(current_slot_data)` checked in `CanPlayCard`
- Max **1 live ball card per player per turn** (`player.LiveBallCard`)
- `OffLiveBall` = offense only, `DefLiveBall` = defense only

### Card Storage
- Played live ball cards go to `player.cards_temp` and `player.LiveBallCard` tracks the reference
- After resolution, `ClearLiveBallCards()` discards them and nulls the reference
- `player.LiveBallCard` reset to null at start of each `StartTurn`

### Resolution Order (6-Step Priority)
```
1. BOTH PASS CHECK      Both null → subtotal stands, EndTurn

2. NEGATE CHECK         DefLiveBall with EffectNegateCard (Blanket Coverage)
                        → Cancels opponent's card entirely (including fumbles)
                        OffLiveBall with EffectImmunity (In the Zone)
                        → Blocks ALL defensive effects (including fumbles)

3. FUMBLE CHECK         DefLiveBall with EffectForceTurnover
                        a. Ball Security (EffectPreventTurnover)? → DENIED. Subtotal stands. END.
                        b. Apply grit boosts from off card (read-only, not async)
                        c. Compare board grit: off total vs def total
                           → Def > Off → FUMBLE. Turnover. Return = 2×(diff). END.
                           → Off ≥ Def → RECOVERY. Subtotal stands. END.
                        ANY fumble outcome = play ends. No further effects fire.

4. DEF EFFECTS          Yardage mods, silence, stamina drain (non-fumble abilities)

5. OFF EFFECTS          Yardage mods, draw cards, stamina recovery

6. CLEANUP              ClearLiveBallCards() → EndTurn
```

### Fumble Design Rules
- Live ball turnovers = **fumbles only** (never INTs — pass was already caught/run is in progress)
- Grit-based resolution: defense needs higher board grit to recover the fumble
- `EffectPreventTurnover` (Ball Security) = auto-prevent regardless of grit (rare, 1 Star cost)
- Grit-boost cards (Grit Up, +5 grit) = softer protection, cheaper slot cost
- `EffectNegateCard` (Blanket Coverage) also blocks fumble attempts
- `EffectImmunity` (In the Zone) also blocks fumble attempts
- Return yardage = 2× grit difference (same formula as existing turnovers)

### Card Lineup (20 Cards)

**Offense (OffLiveBall, 00400–00409):**
| ID | Name | Effect | Slot Cost |
|----|------|--------|-----------|
| 00400 | Juke Move | +3 yards | Free |
| 00401 | Stiff Arm | +5 yards | 1 Helmet |
| 00402 | Speed Burst | +7 yards | 1 Star |
| 00403 | Ball Security | Auto-prevent fumble | 1 Star |
| 00404 | Grit Up | +5 grit this play | 1 Football |
| 00405 | Second Wind | +1 stamina to carrier | 1 Football |
| 00406 | In the Zone | Immune to def effects | 1 Star + 1 Helmet |
| 00407 | Smart Play | Draw 1 card | Free |
| 00408 | Momentum | +4 yards; if 8+ total: draw 1 | 1 Helmet |
| 00409 | Second Effort | +3 yards, -1 stamina | 1 Football |

**Defense (DefLiveBall, 00410–00419):**
| ID | Name | Effect | Slot Cost |
|----|------|--------|-----------|
| 00410 | Ankle Tackle | -2 yards | Free |
| 00411 | Form Tackle | -4 yards | 1 Football |
| 00412 | Gang Tackle | -6 yards | 1 Helmet + 1 Football |
| 00413 | Strip the Ball | Force fumble (grit check) | 1 Star |
| 00414 | Ball Hawk | Fumble if gaining 5+ | 1 Helmet |
| 00415 | Big Ol Lick | Silence off player 1 turn | 1 Helmet |
| 00416 | Lights Out | KO player (stamina to 0) | 2 Stars |
| 00417 | Blanket Coverage | Negate opp's live ball card | 1 Football |
| 00418 | Wear Them Down | -1 stamina all off players | 1 Star |
| 00419 | Disruption | Add Wrench to outer reels | 1 Helmet |

### Effects Reference
| Effect | Purpose |
|--------|---------|
| `EffectYardageModifier` | +/- yards via `ability.value` |
| `EffectForceTurnover` | Fumble (grit-based resolution) |
| `EffectPreventTurnover` | Ball Security auto-prevent |
| `EffectNegateCard` | Cancel opponent's live ball card |
| `EffectImmunity` | Immune to all opponent effects |

### Builder
`Assets/TcgEngine/Editor/LiveBallCardBuilder.cs` → menu `First&Long > Create Live Ball Cards`

---

## 8. SLOT MACHINE

### Default Reels (all 8 symbols each — 512 middle-row combos)
| Reel | Symbols | Notes |
|------|---------|-------|
| Left (R0) | 4 Football, 2 Helmet, 1 Star, 1 Wrench | Has Wrench, no Wild |
| Center (R1) | 3 Football, 3 Helmet, 1 Star, 1 Wild | Has Wild, **no Wrench** |
| Right (R2) | 3 Football, 3 Helmet, 1 Star, 1 Wrench | Has Wrench, no Wild |

Wrench only appears on outer reels — the center reel never shows a wrench.
Wild only appears on the center reel.

### Single-Icon Probabilities (middle row)
| Condition | Probability |
|-----------|-------------|
| Football ≥1 | 80.5% |
| Helmet ≥1 | 70.7% |
| Football ≥2 | 37.5% |
| Star ≥1 | 33.0% |
| Helmet ≥2 | 25.8% |
| Wrench ≥1 | 23.4% |
| Wild ≥1 | 12.5% |
| Football ≥3 | 7.0% |
| Star ≥2 | 4.3% |
| Helmet ≥3 | 3.5% |
| Wrench ≥2 | 1.6% |

### Multi-Icon AND Probabilities
| Condition | Probability | Notes |
|-----------|-------------|-------|
| Football ≥1 AND Helmet ≥1 | 52.7% | Near coin-flip |
| Star ≥1 AND Football ≥1 | 22.9% | |
| Star ≥1 AND Helmet ≥1 | 19.3% | |

### Design Rules
- **Wrench = bad outcomes only** (incompletions, fumbles, TFL, sacks). No positive ability should require Wrench.
- **Star = rare big plays** (explosive gains, special triggers)
- **Football = baseline run/pass success**
- **Helmet = physical/defensive plays**
- **Wild = lucky/special plays** (~12.5%, center reel only)

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

# ROADMAP

## STATUS (2026-11-11)
Play-animation overhaul (plan in memory `play-animation-overhaul.md`). Committed 2026-09-08, **NONE PLAYTESTED** (Unity MCP was offline):
- [x] M1 Resolution ready-gate, self-starting move queues, defense moves (9b7a01a)
- [x] M2 outcome facts: fail caster, tackler, CTR defender (e66be2a)
- [x] M3 play choreographer, per-outcome scripts + defense roles (463a562)
- [x] M4 code half: parabolic throw arc, facing, looseFrames (007af96)
- [x] Presentation A-C: reveal/spin split, AbilitySpotlight pre-snap, single recap panel left, BigPlay at moment (a2c2ab3, b744476, 482e771)

**NEXT, in order:**
1. PLAYTEST all above in editor. Verify: defense deep-direction sign (`Mathf.Sign(slot.localPosition.y)` in AssignDefenseRoles), spotlight runtime creation, recap panel vs mini slots, ball sprite wiring.
2. M5 post-LiveBall re-animation (see P0 below). Break into sub-tasks first.
3. M6 timing knobs in DevTuningPanel + CTR "Coverage Shift!" banner (uses `ctr_defender_uid`). Subsumes P1.5.
4. LiveBallCardZone rebuild (P0).

---


## TODO — Ball Animation Setup (do these before next session)
Code is written and compiles. These are manual Unity Editor steps:

1. **Verify sprite slicing** — click each PNG in `Resources/football/`. Should see 12 sub-sprites (fumble) and 10 (spiral). If not, open Sprite Editor → Slice → Grid by Cell Count (fumble: 4x3, spiral: 5x2)
2. **Run `Tools > Setup Ball Sprite Sheet`** from Unity menu — creates `BallSpriteSheet.asset` and wires sliced sprites. If menu item missing, restart Unity for domain reload
3. **Wire BallManager** — on `BallMarker` GameObject (child of `LOSMarker`), drag `Resources/football/BallSpriteSheet.asset` onto `BallManager.spriteSheet` field
4. **Test a play** — hit Play, run a play, verify ball appears at snap, follows carrier (run) or spirals to receiver (pass)

Files changed: `BallManager.cs`, `BallSpriteSheet.cs`, `PlayAnimationController.cs`, `Game.cs`, `GameLogicService.Yardage.cs`, `FieldSlotManager.cs`, `BallSpriteSheetSetup.cs`, both `.meta` files

---

## P0 — Blockers
- [x] Fail event chronological priority + remove SackFumble
- [x] Route animation: hybrid implemented (library-first, procedural-fallback). 57 assets loaded, carrier/target always procedural
- [x] BoardSlot.MoveQueue wiring verified — initialized in Awake(), Tick() called from Update(), queues Start/Clear/BroadcastSignal via BoardSlot.GetAll()
- [x] **Ball always visible** — `ResetToLOS()` added to `BallManager`; called from `Awake`, `OnStartTurn`, `OnEndTurn`. Ball sits at LOS as OnGround sprite between plays via LOSMarker parent tracking.
- [x] **AI always picks Huddle** — verified already implemented. `AIPlayerMM.ExecuteAction` handles `GameAction.SelectPlay`; `AILogic.AddActions` generates Run/ShortPass/LongPass actions in ChoosePlay. Doc was lagging.
- [ ] **Post-LiveBall animation missing** — after LiveBall card resolution (e.g. Juke Move +3), jumps straight to recap. No player movement, no ball travel, no yardage animation. Should: re-run route/carrier animation w/ final adjusted yardage, then show recap. Files: `PlayAnimationController.cs`, `LiveBallResolutionUI.cs`, `GameLogicService` live ball resolve path. **Big undertaking — break into sub-tasks before starting.**
- [ ] **LiveBallCardZone rebuild** — rebuilding from new Figma design. Old overlay (sortingOrder=200) blocked pointer events to hand cards. Note: `PlayCardDropZone.cs` already routes by phase (ChoosePlayers→board, ChoosePlay→enhancer, LiveBall→LiveBallCardZone) but still delegates to separate singletons. May have been partially lost in crash — verify consolidation state before rebuilding.

## P0.5 — Ball Sprite Wiring ✅ DONE
`BallSpriteSheet.asset` created + populated (all frame arrays + onGroundSprite). Ball animates through snap/throw/fumble states in play. Sliced frame sheets TODO if we want richer anim (currently using `basic_football_sprite` for all states).

## P1 — Core Gaps
- [x] Live ball phase visual feedback (no animation/UI currently)
- [x] Audible UI callout (hot-route detection works, no visual)
- [x] CheckForWinner() — real win condition + sudden death OT
- [x] WaitForPlayerSelection() auto-progresses w/ only 1 card placed
- [ ] Review RouteLibrary bucket coverage gaps (check [RouteAssign] FALLBACK logs in console)
- [x] **Phase indicator text** — `PhaseIndicatorUI.cs`, GameObject at `Scene/PhaseIndicatorUI`, slide-in bar on phase change (CHOOSE PLAYERS / SELECT PLAY / LIVE BALL).
- [x] **Play call reveal sequence** — `PlayCallRevealPanel.cs` present + instantiated. (Verify timing/behavior if not yet validated.)
- [x] **Live ball card reveal** — `LiveBallRevealPanel.cs` present + instantiated.
- [x] **Yardage math display** — `YardageSummaryPanel.cs` present + instantiated. (Verify live-running-totals sub-feature separately.)
- [ ] **CoverTopReceiver dynamic shift animation** — CTR always covers TRUE best receiver post-slot-buffs. When top receiver changes after slot abilities: "Coverage Shift!" banner → DB animates to new target (~1s dramatic cut). Sequence: pre-spin lineup → slot abilities fire → re-rank → if changed, banner + DB move → CTR nullifies new top. Files: `ReceiverRankingSystem.cs`, `PlayAnimationController.cs`, `FieldSlotManager.cs`

## P1.5 — Animation Timing Pass (global)
**Problem**: Everything happens too fast — players can't digest info. Phases flash by, reveals are instant, UI panels appear/disappear before you can read them.
- [ ] **Audit all animation durations** — catalog every DOTween duration, panel show/hide time, reveal hold, and inter-phase delay across all UI panels and animation controllers
- [ ] **Add configurable hold/dwell times** between phases — especially after reveals (play call, enhancer, slot result, yardage summary) so players can read what happened
- [ ] **Slow down menu/panel transitions** — fade-in/out durations, slide animations, scale pops all need breathing room
- [ ] **Tunable via DevTuningPanel** — expose key timing knobs (reveal hold duration, phase transition delay, panel fade speed) so we can iterate without recompiling
- **Key files**: `PlayAnimationController.cs`, `PlayCallRevealPanel.cs`, `LiveBallRevealPanel.cs`, `AbilitySpotlight.cs`, `YardageSummaryPanel.cs`, `BoardStatOverlay.cs`, `SlotMachineUI.cs`, `TimeController.cs`, `DevTuningPanel.cs`
- **Scope**: UI panel timing (ScreenSpaceOverlay canvases) + field animation pacing + inter-phase delays

## P2 — Polish
- [ ] AbilitySpotlight banner styling/fonts
- [ ] Slot machine full↔mini transition timing
- [ ] Defense pursuit animation verification
- [x] HandleTouchdown double-StartTurn bug (#13) — verified fixed; old `CheckGameOver` refactored into inline if/else chain in `EndPlayPhase`/`HandleTouchdown`, no double-fire path exists
- [ ] ~~LiveBallResolutionUI layout cleanup~~ — subsumed by yardage math display (P1)

## P3 — Nice-to-have
- [ ] Route shape QA across all formations
- [ ] Coach coverage bonus testing under procedural routes
- [ ] Speed tier constants verification

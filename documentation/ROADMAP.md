# ROADMAP

## STATUS (2026-11-11)
Play-animation overhaul (plan in memory `play-animation-overhaul.md`). **First playtest done 2026-11-11** (UnityMCP, scripted driver vs AI, ~15 plays; 124/124 EditMode tests pass):
- [x] M1 Resolution ready-gate, self-starting move queues, defense moves (9b7a01a)
- [x] M2 outcome facts: fail caster, tackler, CTR defender (e66be2a)
- [x] M3 play choreographer, per-outcome scripts + defense roles (463a562)
- [x] M4 code half: parabolic throw arc, facing, looseFrames (007af96)
- [x] Presentation A-C: reveal/spin split, AbilitySpotlight pre-snap, single recap panel left, BigPlay at moment (a2c2ab3, b744476, 482e771)

### Playtest results 2026-11-11
Verified in play mode:
- Run, short pass, long pass, TD: Resolution plays 10-13s, snap -> routes -> throw -> pursuit -> recap. TOUCHDOWN overlay fires.
- Defense deep sign correct (slot positions logged: DL/LB step toward LOS on run, defense on +y).
- Recap panel left, mini slots right, no overlap. Hot-route banner shows.
- Ball visible, snaps, follows carrier, flies on throws. Still single-frame sprite (sheets sliced but unassigned).
- LiveBall gets a fresh 60s timer.

Fixed during playtest:
- fd876e9 LiveBall inherited Resolution's spent timer
- 0efaa1b ready queued during a reveal delay leaked into Resolution and skipped the whole animation
- ff62501 targeted WR out route ran off the sideline, ball followed off-screen
- bda40b4 after a dead-ball play (TD/incomplete) field stayed locked: next play animated from the benches, 19s
- 53b1262 PlayImpactTests fixture

NOT covered yet (never occurred vs this AI):
- Sack / INT / TFL / fumble / batted scripts (need defender fail-event cards + slot icons)
- AbilitySpotlight showcases (`play_impacts` was 0 every play)
- LiveBall card effects (both sides passed every time)

Open issues seen:
- Dead-ball plays skip server `EndTurn`: `turn_count` not incremented, status durations not ticked, EndOfTurn abilities not fired. Rules question.
- AI offense only ever called Run with no QB/RB on board (carrier empty, ball goes to empty QB slot).
- After possession change, slots are still jogging in from the bench when Resolution starts (formation tween not finished).
- Several hand cards render as black rectangles.
- Console: "referenced script (Unknown) is missing" spam at boot (no missing components on scene objects; likely a prefab).

**NEXT, in order:**
1. Dev scenario to force sack / INT / fumble + a slot-ability card, then playtest those scripts and the spotlight.
2. Assign sliced frames to `BallSpriteSheet.asset` (`Tools > Setup Ball Sprite Sheet`).
3. M5 post-LiveBall re-animation — sub-tasks below.
4. M6 timing knobs in DevTuningPanel + CTR "Coverage Shift!" banner (uses `ctr_defender_uid`). Subsumes P1.5.
5. LiveBallCardZone rebuild (P0).

### M5 sub-tasks (post-LiveBall re-animation)
Problem: `ResolveLiveBallEffects` fires all events in one frame, then `EndTurn` after a single 1.5s delay; client `OnEndTurn` clears queues + resets ball -> deltas/turnover never visibly play. Recap shows pre-LB yardage.
- [ ] 5a server: new ready-gated phase `LiveBallResolve` (after LiveBall, before EndTurn). AI auto-ready, fresh timer as timeout. `EndTurn` guard updated. Tests.
- [ ] 5b server: `Game.live_ball_log` (ordered: type, off/def card uid, p1, p2) + `pre_lb_yardage`; cloned + serialized. Client animates from state, not from racing `MsgLiveBallEvent`s. Tests.
- [ ] 5c client: `PlayAnimationController.LiveBall.cs` — `LiveBallSequence`: wait reveal panel -> per-entry beats (negate / ball security popups, yardage delta carrier move + re-pursuit + tackle, fumble loose + scramble + recover, turnover return) -> TD celebrate if final crosses goal -> ready.
- [ ] 5d recap: skip pre-LB recap when `ball_was_live`; show final recap after 5c with "Live Ball: pre -> final" line.
- [ ] 5e cleanup: remove old `OnLiveBallEventReceived` immediate animation; fix stale `LiveBallResolutionUI` refs (file does not exist) in CLAUDE.md.

---

## P0 — Blockers
- [x] Fail event chronological priority + remove SackFumble
- [x] Route animation: hybrid implemented (library-first, procedural-fallback). 57 assets loaded, carrier/target always procedural
- [x] BoardSlot.MoveQueue wiring verified — initialized in Awake(), Tick() called from Update(), queues Start/Clear/BroadcastSignal via BoardSlot.GetAll()
- [x] **Ball always visible** — `ResetToLOS()` added to `BallManager`; called from `Awake`, `OnStartTurn`, `OnEndTurn`. Ball sits at LOS as OnGround sprite between plays via LOSMarker parent tracking.
- [x] **AI always picks Huddle** — verified already implemented. `AIPlayerMM.ExecuteAction` handles `GameAction.SelectPlay`; `AILogic.AddActions` generates Run/ShortPass/LongPass actions in ChoosePlay. Doc was lagging.
- [ ] **Post-LiveBall animation missing** — after LiveBall card resolution (e.g. Juke Move +3), jumps straight to recap. No player movement, no ball travel, no yardage animation. Should: re-run route/carrier animation w/ final adjusted yardage, then show recap. Files: `PlayAnimationController.cs`, `LiveBallRevealPanel.cs`, `GameLogicService.Lifecycle.cs` (`ResolveLiveBallEffects`). **= M5, sub-tasks in STATUS.**
- [ ] **LiveBallCardZone rebuild** — rebuilding from new Figma design. Old overlay (sortingOrder=200) blocked pointer events to hand cards. Note: `PlayCardDropZone.cs` already routes by phase (ChoosePlayers→board, ChoosePlay→enhancer, LiveBall→LiveBallCardZone) but still delegates to separate singletons. May have been partially lost in crash — verify consolidation state before rebuilding.

## P0.5 — Ball Sprite Wiring (wired; frames TODO)
`BallSpriteSheet.asset` created + wired on `BallManager` in scene. All states use single `basic_football_sprite` frame. Sheets sliced (fumble 12 / spiral 10) but unassigned — run `Tools > Setup Ball Sprite Sheet` in editor, then check spiral/tumble in play.

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

## P1.5 — Animation Timing Pass (global) — folded into M6
**Problem**: Everything happens too fast — players can't digest info. Phases flash by, reveals are instant, UI panels appear/disappear before you can read them.
- [ ] **Audit all animation durations** — catalog every DOTween duration, panel show/hide time, reveal hold, and inter-phase delay across all UI panels and animation controllers
- [ ] **Add configurable hold/dwell times** between phases — especially after reveals (play call, enhancer, slot result, yardage summary) so players can read what happened
- [ ] **Slow down menu/panel transitions** — fade-in/out durations, slide animations, scale pops all need breathing room
- [ ] **Tunable via DevTuningPanel** — expose key timing knobs (reveal hold duration, phase transition delay, panel fade speed) so we can iterate without recompiling
- **Key files**: `PlayAnimationController.cs`, `PlayCallRevealPanel.cs`, `LiveBallRevealPanel.cs`, `AbilitySpotlight.cs`, `YardageSummaryPanel.cs`, `BoardStatOverlay.cs`, `SlotMachineUI.cs`, `TimeController.cs`, `DevTuningPanel.cs`
- **Scope**: UI panel timing (ScreenSpaceOverlay canvases) + field animation pacing + inter-phase delays

## P2 — Polish
- [ ] AbilitySpotlight banner styling/fonts (10/14pt too small — bump w/ M6)
- [ ] Slot machine full↔mini transition timing
- [ ] Defense pursuit animation verification (code done M1/M3; needs playtest)
- [x] HandleTouchdown double-StartTurn bug (#13) — verified fixed; old `CheckGameOver` refactored into inline if/else chain in `EndPlayPhase`/`HandleTouchdown`, no double-fire path exists
- [ ] ~~LiveBallResolutionUI layout cleanup~~ — subsumed by yardage math display (P1)

## P3 — Nice-to-have
- [ ] Route shape QA across all formations
- [ ] Coach coverage bonus testing under procedural routes
- [ ] Speed tier constants verification

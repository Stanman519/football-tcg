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

### Session 2 (2026-11-11, later) - abilities + spotlight + M5 v1
- **ROOT CAUSE of "nothing procs"**: `Card.AddAbility` had `abilities.Add` commented out since prototype -> every card had ZERO runtime abilities. Fixed 21dc2ed. ~140 abilities live for the first time: expect balance/behaviour shifts, watch for effect NREs.
- Spotlight redesigned 779b593: big fixed impact panel (left-middle; art / name / value / text), 2.0s hold. Verified in play mode (+3 RUN COV etc.). Last width tweak not re-screenshotted.
- Dev forcing 099219f: `First&Long > Dev > Force` (play mode) or `DevScenario.forceAbilityProcs / forceFailEvent`. Bypasses slot reqs AND trigger conditions (cards gate on slot icons via `conditions_trigger`, none use `slotRequirements`).
- Dead-ball plays route through `EndTurn` 6d3ce40. AI offense random play call 8793dc2 (temp).
- M5 v1: LiveBall beat sequencer. Server sums `LiveBallBeats.Duration` per event and holds EndTurn that long; client queues events and plays one beat at a time on the impact panel (+ carrier delta / turnover anim). PLAYTESTED: Juke Move -> reveal, "+3 YDS", "Result" beats over ~9s (was 1.5s), EndTurn waits.
- Snap waits up to 3s for slots still lining up (not visually confirmed).
- Non-player cards were blank black (CardUI hid whole PlayerSection incl. name/art area). Now name + type-colored art area show. Verified.
- Outcome beat: fail events get a panel ("SACK / caused by <card>", "INCOMPLETE") before the recap. Verified for Incomplete; forced Sack (00113) verified firing + animating earlier.

Data gaps (card content, not code):
- Only 2 fail-event abilities exist: Sack (00113), QBFumble (00132). INT / TFL / runner fumble / batted pass cannot come from cards.
- 9 abilities have `effects: []` (e.g. 00105 "force a fumble on run") - skipped at runtime with a warning.
- 30/30 non-player cards have no art; no DefensivePlayEnhancer (type 70) assets exist.

Open issues:
- AI fields no QB/RB on offense (not investigated).
- Console: "referenced script (Unknown) is missing" spam at boot (likely a prefab).
- Coach abilities fire AFTER yardage is computed (`OnCoachTrigger` post-calc) and leave no trace for the client -> no beat possible yet.

Session 3 (2026-11-11):
- LiveBall branches verified in play mode: negate (Blanket Coverage), ball security, fumble recovery by grit, def yardage card (Ankle Tackle). Fumble TURNOVER verified too ("FUMBLE! Grit 0 vs 3" -> "TURNOVER, returned 6 yds", possession flips). Not seen: In The Zone (immune).
- Recap = ledger: rows tick in one at a time (zero rows hidden, +green/-red), total pops last; no rows on fail events. Fonts ~2x. Verified.
- Play length (measured, Run, 2 ability beats): reveal 3.9s + spin 5.0s + Resolution ~14-15.5s (was 17.6 before trims) = ~24s call-to-end; LiveBall adds 2s (both pass) to ~10s (cards).
- Trims applied: pre-showcase waits, ability beat 2.0s each (cap 3/play), ledger stagger 0.3s, recap hold 2.2s (scene value). New DevTuningPanel sliders: Ability Beat Hold, Max Ability Beats, Recap Row Stagger, Recap Hold.
- Noted: Run with no runner on board = -10 to -12 yds every play (def coverage vs 0 offense). Balance question, now that abilities fire.

Decisions (user, 2026-11-11):
- Beats AUTO-ADVANCE (no click-to-advance).
- Fail events: come from card abilities whose `conditions_trigger` read the slot icons (card = what, slots = when). Only slot-only case in code: Incomplete with no QB on board (wrench / no football on long). Card data currently wires only Sack (00113) + QBFumble (00132).
- The 9 empty-effect abilities: deferred (too bespoke). Note 00105 "force a fumble on run" is likely just `failEventType: 8`.
- Generated csprojs: committed as-is.

Session 4 (2026-11-11):
- LiveBall reveal now driven by the Reveal event (CardData overload; GameFeedbackUI polling removed). Panel fonts ~2x, verified readable.
- LiveBall yardage delta: defense re-pursues to where the carrier ENDS, tackler pops. LiveBall TD (delta crosses goal) gets a TOUCHDOWN result beat. Ran clean in play mode (+3 Juke Move); pursuit too small to judge from stills, TD branch unseen.
- TD in Resolution: scorer beat ("SCORE / name / TOUCHDOWN / N-yd run") after the flash. Unseen.
- EditMode 130/130.
- LiveBall is READY-GATED (03a8c14): new `GamePhase.LiveBallResolve = 31`. Server fires LB events, waits for client ready (AI instant, 30s timeout, skipped in AI sims). `LiveBallBeats.Duration` is client-only now. Verified in play: cards 9.1s, both pass 1.7s.
- `UITheme` (b2e0d83): one palette + type scale + MakeTMP/SetAnchors/BuildOverlayCanvas. Reveal panels, recap, spotlight, PAC popups use it. Verified in play (pre label fix).
- UNVERIFIED (MCP dropped): play-call reveal label width/overlap fix; empty-slot markers fading to 6% outside ChoosePlayers.
- Tried `FieldSlotManager.slotScale` 0.5 -> 0.75 for bigger tokens; reverted unseen. Needs a look in editor (overlap risk).
- WARNING: `.gitignore` has `*.unity` -> `GameClCo.unity` (the game scene) is NOT in git. Scene-serialized values (recap hold 2.2, slotScale) are unversioned.

**NEXT, in order:**
1. User feel pass with the DevTuningPanel sliders (target ~20s/play); then bake values. Server-side reveal 3.9s / spin 5.0s are the next cut if still long.
2. Presentation architecture, remaining: play_log, PAC split, rest of panels onto UITheme (BigPlayOverlay, DevTuning, LiveBallCardZone, play picker). Bigger field tokens.
3. Coach beats: needs server change (record coach procs in `play_impacts`; fire before yardage calc).
4. Card data: more fail-event abilities.
5. Ball sheet frames. 6. M6 timing knobs + CTR banner. 7. LiveBallCardZone rebuild (P0).

### Presentation debt (flagged 2026-11-11)
Architecture:
- [x] Ready-gate LiveBall like Resolution.
- One ordered `Game.play_log` of typed events (ability proc, fail event, LB event, score) instead of 3 channels (phase diffs, messages, derived fields). Client = one beat queue over it. Reconnect/replay-safe.
- `PlayAnimationController` (1450 lines, 37 hardcoded waits) -> director + beat classes; timings in one ScriptableObject.
- [~] Shared theme done (`UITheme`), 4 panels migrated. Still: base class for show/hide + canvas, then prefabs (so layout is tunable in editor).
Visual:
- Field tokens tiny (~80px of 1920); empty-slot X/circle markers are louder than the players.
- Three unrelated panel styles (reveal mint/coral, beat navy, recap gray, play-picker black).
- Play-call reveal: full-screen dim with small text (same fix as LiveBall reveal needed).
- Beat panel art area empty for the 30 art-less cards; recap opens as an empty box.
- Hand fan clipped at screen bottom, black card faces.

### M5 status
- [x] v1 beat sequencer from `MsgLiveBallEvent`s (replaces 5c/5e immediate anim; no new phase - server delay = sum of beats).
- [ ] 5b `Game.live_ball_log` so beats come from state, not messages (reconnect-safe). Only if messages prove racy.
- [x] Final yardage shown by the LiveBall "Result" beat ("Was X, live ball +Y").
- [x] Re-pursuit + tackle after yardage delta; TD result beat if final crosses goal.
- [x] Tie reveal panel to the Reveal event.

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

# ROADMAP

## P0 — Blockers
- [x] Fail event chronological priority + remove SackFumble
- [ ] Route animation: decide procedural-only vs RouteLibrary (57 missing assets)
- [ ] Verify BoardSlot.MoveQueue wiring for PlayAnimationController

## P1 — Core Gaps
- [ ] Live ball phase visual feedback (no animation/UI currently)
- [ ] Audible UI callout (hot-route detection works, no visual)
- [ ] CheckForWinner() — win condition is placeholder
- [ ] WaitForPlayerSelection() auto-progresses w/ only 1 card placed

## P2 — Polish
- [ ] AbilitySpotlight banner styling/fonts
- [ ] Slot machine full↔mini transition timing
- [ ] Defense pursuit animation verification
- [ ] HandleTouchdown double-StartTurn bug (#13)

## P3 — Nice-to-have
- [ ] Route shape QA across all formations
- [ ] Coach coverage bonus testing under procedural routes
- [ ] Speed tier constants verification

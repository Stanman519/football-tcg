# UI Design Queue — First & Long

Priority order: P0 blockers → P1 core game flow → P2 polish → P3 menus.
Each entry has a Google Stitch prompt. Copy-paste directly into Stitch.

---

## P0 — Blockers

### 1. LiveBallCardZone
**What it is**: Drop zone overlay appearing during the LiveBall phase. Players drag a live ball card from their hand into this zone (or click Pass). Appears as a screen-space overlay over the game board.

**Status**: Needs full rebuild — old implementation blocked pointer events.

**Stitch Prompt**:
```
Football card game UI, dark sports aesthetic, 1920x1080 screen-space overlay.

"LIVE BALL" phase panel — a wide horizontal band that slides up from the bottom edge of screen. Height ~220px.

Background: #0D0D0D at 92% opacity with a mint green (#AAFFDC) top-border line (2px) and scanline texture overlay (horizontal lines, 8% opacity).

LEFT ZONE (~900px wide): Player's hand. 5 cards displayed horizontally in a fan. Cards are dark rectangles (140x196px) with rounded corners, orange (#FF8C00) border on hover. Label above: "YOUR HAND" in Noto Sans Condensed, uppercase, mint green.

CENTER DROP ZONE (~500px wide): Large dashed-border rectangle (2px dashed mint #AAFFDC, 8px corner radius). Inside: football icon + text "DROP LIVE BALL CARD" in Inter Bold, white. On hover: mint glow pulse.

RIGHT ZONE (~180px): A big coral red (#FF716C) button reading "PASS" in Space Grotesk Bold. Below it, small italic text: "Skip this phase".

Bottom edge: row of slot machine reel icons (football, helmet, star, wrench) as decorative trim in 20% opacity white.

Retro-tech feel, neon accents, minimal chrome.
```

---

## P1 — Core Game Flow

### 2. Play Call Panel
**What it is**: Shown during ChoosePlay phase. Player picks Run / Short Pass / Long Pass. Shows coach info, scouting defense, play enhancer slot, 15s timer.

**Status**: Plan exists (bubbly-coalescing-wall). Needs implementation after design confirmed.

**Stitch Prompt**:
```
Football card game play-selection panel, dark sports UI, 850x640px centered modal.

Background: #131313, 4px left border strip in mint (#AAFFDC). Scanline texture overlay 12% opacity.

HEADER BAR (full width, 48px): Text "CHOOSE YOUR PLAY" in Space Grotesk Bold Italic, white, uppercase. Far right: timer badge — dark bg with rounded bottom corners, text "14S" in mint #AAFFDC Bold.

LEFT COLUMN (128px wide, full height):
- Top half: opponent coach card. 128x112px portrait (grayscale), red accent strip (#FF716C) at bottom with coach name in Inter Bold 11px. Below: 3 small rows "RUN DEF +8", "SHORT DEF +6", "LONG DEF +12" — stat in coral red.
- Bottom half: my coach card. Same size portrait (full color), mint accent strip (#AAFFDC), same stat rows in mint.

RIGHT COLUMN (remainder, ~706px):
- SCOUTING ROW (full width, 56px): 3 columns labeled "RUN DEFENSE / SHORT DEFENSE / LONG DEFENSE" in Noto Sans Condensed. Background rgba(255,113,108,0.08), bottom border rgba(255,113,108,0.3). Big numbers below each label in #FF716C.
- PLAY CARDS ROW (full width, ~294px): 3 equal cards side by side, 12px gap.
  - Each card: #1A1A1A bg, 1px border rgba(72,72,71,0.3), 8px radius.
  - Top: formation diagram (mini X/O routes preview, white on dark).
  - Middle: play name "RUN" / "SHORT PASS" / "LONG PASS" in Space Grotesk Bold.
  - Bottom: yardage estimate "+11 YDS" and incomplete condition "1+ Wrench: Incomplete".
  - SELECTED STATE: 2px orange (#FF8C00) border + subtle orange outer glow.
- ENHANCER SLOT (full width, 64px): dashed 2px mint border rectangle. Text "PLAY ENHANCER (optional)" centered in Inter Medium italic, dim white.
- CONFIRM BUTTON (full width, 52px): solid mint (#AAFFDC) bg, text "CONFIRM SELECTION" in #00654B, Space Grotesk Bold. Rounded corners.

Footer: 128px mint gradient strip in bottom-right corner (decorative).

Retro-tech, scanline, clean dark sports UI.
```

---

### 3. Phase Indicator Banners
**What it is**: Brief animated text banner shown at phase transitions (appears for ~1.5s, then fades). Shows current phase name. Reuses BigPlayOverlay style.

**Stitch Prompt**:
```
Football card game phase transition banner, 1920x1080, full-screen overlay.

Show 4 variants stacked as examples:
1. "CHOOSE YOUR PLAYERS" — mint green (#AAFFDC)
2. "SELECT YOUR PLAY" — orange (#FF8C00)
3. "LIVE BALL!" — coral red (#FF716C), larger font, slight pulse glow
4. "PLAY RESOLVED" — white, smaller

Each banner: centered horizontally, vertically centered. Font: "Press Start 2P" (pixel retro) or closest monospace/retro alternative. Size: 48–64px.

Background: full-screen black at 60% opacity (game board visible underneath as dark silhouette).

Accent: thin horizontal rule lines extending left and right from the text (like a broadcast graphic). Scanline texture over entire overlay at 15% opacity.

Each variant also has a small italic subtitle below in Inter:
(1) "Place offensive and defensive players"
(2) "Select Run, Short Pass, or Long Pass"
(3) "Play your Live Ball card!"
(4) "Yardage resolved"

Clean, dramatic, broadcast-style.
```

---

### 4. Play Call Reveal Sequence
**What it is**: Full-screen spotlight overlay that fires after both players confirm their play. Reveals: (1) your play, (2) opponent's play + coverage guess, (3) enhancers if any. Sequential reveal with suspense hold.

**Stitch Prompt**:
```
Football card game play reveal overlay, full-screen 1920x1080, dramatic broadcast style.

Show the full sequence as 3 stacked frames:

FRAME 1 — "YOUR PLAY" reveal (0.5s hold):
- Black bg, 60% opacity over game board
- Large center spotlight: glowing rectangle revealing a play call card
- Card reads "SHORT PASS" in Space Grotesk Bold, mint (#AAFFDC), huge font (80px)
- Small label above: "YOUR PLAY" in Inter Medium, dim white, 18px
- Formation route diagram below the text (simple X/O lines)
- Scanline overlay, subtle mint vignette

FRAME 2 — "OPPONENT'S PLAY" reveal (1.5s suspense hold):
- Same black overlay
- LEFT side: your play "SHORT PASS" shrunk to 30% opacity (your card faded back)
- RIGHT side: spotlight zooms in — opponent's play "RUN" in #FF716C coral, huge font
- Opponent label: "OPPONENT PLAY" in dim white
- Coverage guess label below: "Coverage Guess: SHORT PASS" in small yellow text with checkmark icon if correct
- Dramatic red glow if opponent guessed correctly

FRAME 3 — Enhancer reveals (if applicable):
- Both plays visible in corners (small, faded)
- Center: "PLAY ENHANCERS" header
- Two spotlight cards side by side: player's enhancer card on left (mint border), opponent's on right (red border)
- Each card shows card name + key effect text

Font hierarchy: Space Grotesk Bold for play names, Inter for labels, Noto Sans for small text.
```

---

### 5. Live Ball Card Reveal
**What it is**: Same zoom spotlight style as play call reveal but for live ball cards. Sequential: your card quick, opponent's with suspense hold.

**Stitch Prompt**:
```
Football card game live ball card reveal overlay, full-screen 1920x1080, sports broadcast style.

Show 2 frames:

FRAME 1 — "YOUR LIVE BALL CARD" (0.5s):
- Full black overlay 65% opacity
- Center: spotlight illuminating a card (180x252px dark card with orange border)
- Card text: "BALL SECURITY" (example) in Space Grotesk Bold, white
- Card effect text below in Inter Medium: "Prevent fumble this play."
- Slot requirement icons below (football, star symbols)
- Label above: "YOUR LIVE BALL CARD" in dim mint, Inter 16px
- Subtle mint glow behind card

FRAME 2 — "OPPONENT'S CARD" with suspense:
- Your card slides to left side, shrinks 40%, desaturated
- Center spotlight empty with spinning glow / loading dots (suspense beat)
- Then: opponent's card slams in from right with coral red glow
- "OPPONENT'S CARD" in dim coral label
- Card reads "BLANKET COVERAGE" (example), coral border
- Effect: "Negate opponent's Live Ball card." in smaller text
- Resolution indicator below: starburst badge "NEGATE!" or "FUMBLE CHECK" in orange

Dramatic, high contrast, cinematic reveal energy.
```

---

### 6. Yardage Math Display — Phase 1 (Live Overlay)
**What it is**: Numbers overlaid on the field DURING route animation showing running totals as each component adds in. Think broadcast score ticker, not a panel.

**Stitch Prompt**:
```
Football card game live yardage calculation overlay, 1920x1080, broadcast ticker style.

CONTEXT: Overlaid on a top-down football field view during play resolution animation. Players/routes visible underneath.

Layout: Fixed to left edge, 280px wide, vertically centered. Semi-transparent dark bg (#0D0D0D, 80% opacity), left edge border in mint.

Stacked rows — each row appears with a quick count-up animation:

Row 1 (mint): "OFF BASE  +14"  (QB + coach yardage)
Row 2 (mint): "RECEIVER  +8"   (top receiver bonus)
Row 3 (mint): "SLOT BONUS  +3" (slot machine result)
Row 4 (coral red): "DEF BASE  −9"   (defense coverage)
Row 5 (coral red): "CTR SHIFT  −5"  (cover-top-receiver)
──────────────────────────────
Row 6 (white, large): "NET  +11 YDS"  (final)

Running total counter on far right updates with each row reveal.

Font: Noto Sans Condensed for labels, Space Grotesk Bold for numbers.
Rows appear one by one (stagger animation implied). Active row glows brighter.
Final row: larger, white, bold, with animated +/- indicator.

Below the tally: small ball-on-field progress indicator (thin 100px bar, 0=own endzone, 100=opponent endzone, dot showing current position).

Minimal, data-focused, no chrome.
```

---

### 7. Yardage Math Display — Phase 2 (Post-Play Summary Panel)
**What it is**: Full breakdown panel shown AFTER play resolves. Shows complete math, slot results, any events (turnover, big play, etc.).

**Stitch Prompt**:
```
Football card game post-play summary panel, 520x640px, centered modal, dark sports UI.

Background: #131313, rounded corners 12px, mint left border strip 4px. Subtle scanline overlay.

HEADER (56px): "PLAY SUMMARY" in Space Grotesk Bold, white. Right side: play type badge "SHORT PASS" in orange pill.

SECTION 1 — Yardage Breakdown (180px):
Label "OFFENSE" in mint caps (Inter 11px, tracking 0.15em)
- "QB Short Pass  +8"
- "Receiver Bonus  +6"
- "Coach Bonus  +3"
- "Slot Wild  +2"
Subtotal line: "OFF TOTAL  +19" in mint bold

Label "DEFENSE" in coral red
- "DB Coverage  −7"
- "Coach Defense  −4"
- "Coverage Guess correct  −2"
Subtotal: "DEF TOTAL  −13" in coral

Divider line.
FINAL: "NET YARDS  +6" in large white Space Grotesk Bold (48px)

SECTION 2 — Play Events (if any, 120px):
Colored badge rows:
- Green badge: "1ST DOWN" if applicable
- Orange badge: "BIG PLAY  20+ YDS" if applicable
- Red badge: "FUMBLE — TURNOVER" if turnover

SECTION 3 — Field State (56px):
Mini field bar (full width). Ball position dot with "Ball on OPP 34" label. Possession arrow.

FOOTER: dim text "Down 2 · 4 YDS to go · 18 plays left" in Noto Sans.

Clean, stat-sheet aesthetic, broadcast summary card feel.
```

---

### 8. Coverage Shift Animation Banner
**What it is**: Appears mid-resolution when slot abilities change who the top receiver is. "Coverage Shift!" banner fires, then a DB avatar visually snaps to the new receiver.

**Stitch Prompt**:
```
Football card game "Coverage Shift" moment banner, 1920x1080 overlay.

Show a dramatic 2-state sequence:

STATE 1 — "COVERAGE SHIFT!" banner (1s):
- Dark overlay 50% opacity
- Center: wide horizontal band (~120px tall), bg rgba(255,113,108,0.15) with coral red top/bottom borders
- Text: "COVERAGE SHIFT!" in Space Grotesk Bold Italic, coral red (#FF716C), 52px
- Left annotation: "Previous target: WR#1" (struck-through, gray)
- Right annotation: "New target: WR#2" (highlighted mint, bold)
- Scanline + subtle shake implied

STATE 2 — Receiver swap visual (post-banner):
- Split panels showing two WR cards side by side
- Left card: "WR#1 — Davis" — grayed out, "COVERED" badge stamped in red diagonal
- Right card: "WR#2 — Torres" — glowing mint border, "NEW TARGET" badge in mint
- Arrow between them pointing right
- Below: "DB Hayes reassigned" in small italic white

Dramatic, high-energy, broadcast alert style.
```

---

## P2 — Polish

### 9. Ability Spotlight Banner
**What it is**: Shows when a card ability triggers — card glows, ability name + effect appear in a spotlight.

**Stitch Prompt**:
```
Football card game ability trigger spotlight, 1920x1080 screen-space overlay.

Show 2 variants:

VARIANT A — Quick ability (immediate, non-resolution):
- Semi-transparent dark strip (480x120px) appears near triggered card
- Left: card art thumbnail (80x80) with orange glow ring
- Right: ability name "BULL RUSH" in Space Grotesk Bold, orange
- Below: effect text "+5 Run Coverage" in Inter Medium, white
- Duration: ~1s then fades

VARIANT B — Showcase ability (during resolution, slow-mo):
- Full center spotlight: 640x360px dark panel, mint border
- Card floats center at 1.3x scale with glow halo
- Above card: player name "Marcus Davis · WR" in Inter 14px dim white
- Large: "SLOT BONUS ACTIVATED" in Space Grotesk Bold, mint, 32px
- Below: "Wild on center reel: +8 Deep Pass this play" in Inter 18px
- Scanline overlay, vignette edges
- Implied camera zoom (slight scale-up)

Clean, satisfying, sports card reveal energy.
```

---

### 10. Scoreboard HUD Polish
**What it is**: Always-visible top HUD bar. Row 1 permanent (scores, down, field position). Row 2 appears during Resolution/LiveBall (yardage tallies).

**Stitch Prompt**:
```
Football card game scoreboard HUD, 1920x64px strip pinned to top of screen, always-visible.

Dark bar: #0A0A0A at 95% opacity, 1px bottom border #AAFFDC (mint).

ROW 1 layout (left to right, 1920px):
[TEAM A SCORE: "14"] | [half indicator: "1ST HALF"] | [down: "2ND DOWN"] | [distance: "4 YDS TO GO"] | [plays-left: "12 PLAYS LEFT"] | [possession arrow pointing right] | [field-position: "OPP 34"] | [TEAM B SCORE: "7"]

Scores: large white, Space Grotesk Bold 32px.
Game info (half/down/distance): Noto Sans Condensed, 14px, dim white.
Possession arrow: glowing orange pointing toward scoring end.
Field position: mint, small.

ROW 2 (only visible during resolution, slides down — 1920x32px below Row 1):
Three segments: [OFF +19 YDS] [NET +6 YDS] [DEF -13 YDS]
Mint for offense, white for net, coral for defense.
Appears with slide-down animation, disappears after resolution ends.

Scanline texture across full bar at 10% opacity.
Clean, broadcast-style, data-dense but readable.
```

---

## P3 — Menus (Lower Priority)

### 11. Mulligan Screen
**What it is**: Initial hand shown before game starts. Player selects cards to redraw. Shown once per game.

**Stitch Prompt**:
```
Football card game mulligan screen, 1920x1080, full-screen modal over dark game board.

Center panel (960x480px): #131313 bg, mint top border.
Header: "OPENING HAND" in Space Grotesk Bold, white. Subtext: "Select cards to redraw (tap to select)" in Inter Medium dim.

7 cards displayed horizontally in a row (140x196px each, 16px gap).
- Default state: normal dark card face, 1px gray border
- Selected state: coral red (#FF716C) overlay at 30% + "REDRAW" stamp + card scales down slightly
- Hover: slight orange glow

Below cards: "REDRAW SELECTED (3)" button in orange, Space Grotesk Bold.
Next to it: "KEEP ALL" button in dim white outline style.

Bottom: small text "You may redraw up to 4 cards once" in italic dim white.

Clean, minimal, unobtrusive.
```

---

### 12. Main Menu
**What it is**: First screen after login. Game title, play buttons, collection, settings.

**Stitch Prompt**:
```
Football card game main menu, 1920x1080, dark stadium aesthetic.

Background: blurred stadium aerial view, dark overlay 70%, scanline texture.

TOP: Game logo "FIRST & LONG" — large retro-sport font (Space Grotesk Black Italic), white with mint (#AAFFDC) drop shadow. Subtitle: "The Football Card Game" in Noto Sans Condensed, dim white.

CENTER: 4 main action buttons stacked vertically, 420px wide, 64px tall each, 12px gap:
1. "PLAY SOLO" — solid mint (#AAFFDC) bg, dark text — primary CTA
2. "PLAY ONLINE" — dark bg, mint border, mint text
3. "MY COLLECTION" — dark bg, orange border, orange text
4. "OPEN PACKS" — dark bg, gray border, white text

RIGHT SIDE: player profile card (320px wide). Dark panel, player avatar (80px circle), username, current record "W12 L4", rank badge.

BOTTOM LEFT: version string, small. BOTTOM RIGHT: settings gear icon.

Sports-card-collection aesthetic. Retro but clean.
```

---

### 13. Collection / Deck Builder
**What it is**: Full card database browser + deck editor. Filters + card grid + deck list.

**Stitch Prompt**:
```
Football card game deck builder / collection screen, 1920x1080, full-screen.

Background: #0D0D0D.

LEFT PANEL (340px): Filters sidebar.
- Header: "FILTERS" in Space Grotesk Bold, mint
- Card type toggles: [Offensive] [Defensive] [Play Enhancer] [Live Ball] — pill buttons, selected = mint fill, unselected = dark outline
- Position group filter: QB | RB | WR | OL | DL | LB | DB (icon + label, toggle)
- Suit filter: Clubs Diamonds Hearts Spades (colored icons)
- Sort: dropdown "Sort by Name / Stamina / Grit"
- Search: dark input, placeholder "Search cards..."

CENTER PANEL (900px): Card grid.
- 4-column grid of card thumbnails (200x280px each, 8px gap)
- Each card: dark bg, card name in Space Grotesk Bold, position badge, key stat in mint
- Hover: orange border glow
- Selected: orange fill badge "IN DECK x2"

RIGHT PANEL (340px): Current Deck.
- Header: "MY DECK" + card count "24/30" in mint
- Deck name input field
- Scrollable list of added cards: card name + qty + small remove X
- Bottom: "SAVE DECK" button (mint), "CLEAR" button (coral outline)

Top nav: breadcrumb "COLLECTION > DECK BUILDER" + back arrow.

Dense, data-rich, sports management game aesthetic.
```

# Minigame ideas: the judged shortlist (23 September 2026)

The owner asked for more minigames that fit the tone and theme of the ones already in the game. Four
lanes brainstormed 24 concepts: web parity, reality-show formats, the season's own data, and the
unused competition art in the UI packs. A judge then checked each concept's load-bearing claims
against this repo, the web source at `D:/gamesim-main/gamesim-main/src`, the art packs, the
captures, mockup-05 and five local season saves, and merged and ranked what survived.

**Nothing here is built yet.** The one decision only the owner can make is in section 3
(reachability): new games either arrive as variants inside Skill, Mental and Endurance under a
frozen rules-version-4 selector (no season re-rolls), or the category rotation is widened (every
seeded season re-rolls).

## 0. Corrections found while checking

These change the concepts as they were pitched:

- **Selection must not depend on the event log.** A selector fallback like "fewer than 6 askable pairs keeps the v3 game" reads the event log, which keeps only 256 lines (`EpisodeEngine.cs:1855`). Conversations during the phase can push old lines out between the briefing and the commit. The briefing and the logged `competition-definition` line could then name different games, and a cancelled ranked board would not come back the same. Selection has to gate on week and phase only, and quiz questions have to come from records that do not roll off.
- **How fast the log rolls.** The saves show 17 to 48 events per week, for 6- and 8-person casts. That means the 256-line log holds roughly 5 to 8 weeks, so week-1 lines are only at risk late in a season.
- **The eviction week is not stored.** Every evicted houseguest becomes a juror, and jurors are added in eviction order (`EpisodeEngine.cs:313-315`, `664-665`). The entry week is saved as 0 ("Entered jury", `WebJurySentiment.cs:49`). "Evicted third" is safe to ask; "evicted in week N" has to be worked out.
- **Some helpers live in the Runtime assembly.** `VoteRecords` and `WeeklyRecap` are in `Runtime/Presentation`, and so is `MiniGameRun`. `Tools/SimulationTests` only compiles `Assets/Gamesim/Simulation/**` (`SimulationTests.csproj:37`). All three are plain C#, so they can move into Simulation if their rules need `dotnet test`. Otherwise their tests run in the headless EditMode harness.
- **A 2D key icon already exists:** `Resources/GamesimIcons/key.png`. The Kit6 room icons and `ic_search` exist too.
- **Only two quiz answer tiles exist.** `quiz_answer_A_9slice` and `quiz_answer_B_9slice` are there; the C and D tiles are not. `answer_badge_A` through `D` all exist.
- **Stack the Tower's colour guarantee is 7 discs, not 4.** With a shuffled bag of four colours per cycle, the colour you need can be up to 7 discs away.
- **One capture detail is not a bug.** The capture test passes "Head of Household" as the title (`EpisodePlayModeTests.MiniGameCaptures.cs:41`), so the missing game name in the captures is expected.
- **The "Progress" label has a test.** `EpisodePlayModeTests.CompetitionInput.cs:103` asserts its text contains "pairs", "hits" or "Effort". The web's "Matches 3/8" wording would fail it.

## 1. Ranked shortlist

Each dimension is scored 1 to 5. For Effort, 5 means cheapest. Rank uses the total plus tone and fun counted a second time.

| # | Concept | Tone | Fun | Honesty | Determinism | Access | Reuse | Effort | Total/35 | Rank score |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Ready, Set, Whoa! | 5 | 5 | 5 | 5 | 4 | 5 | 4 | 33 | 43 |
| 2 | Finish beats + surface restyle | 5 | 4 | 5 | 5 | 5 | 5 | 4 | 33 | 42 |
| 3 | Spotlight Sequence | 4 | 4 | 5 | 5 | 4 | 5 | 4 | 31 | 39 |
| 4 | Stack the Tower | 5 | 4 | 5 | 5 | 4 | 4 | 3 | 30 | 39 |
| 5 | Jury Questions | 5 | 4 | 5 | 4 | 5 | 3 | 3 | 29 | 38 |
| 6 | Grip Check | 4 | 3 | 5 | 5 | 3 | 5 | 5 | 30 | 37 |
| 7 | Before or After | 5 | 4 | 4 | 4 | 5 | 3 | 2 | 27 | 36 |
| 8 | Hide and Seek: House Keys | 3 | 3 | 5 | 5 | 5 | 5 | 4 | 30 | 36 |
| 9 | Hang in There | 4 | 4 | 5 | 4 | 3 | 4 | 3 | 27 | 35 |
| 10 | Count the House | 4 | 3 | 5 | 5 | 3 | 4 | 3 | 27 | 34 |
| 11 | Word Scramble | 4 | 4 | 4 | 5 | 3 | 3 | 2 | 25 | 33 |

"Rules v4" below means a new rules version 4 with its own frozen selector. None of these games re-rolls an existing season: the category rotation and the `Roll(s)` draws stay the same, so NPC placement is unchanged.

### 1. Ready, Set, Whoa! (merges Freeze Frame and Ready, Set, Whoa!)
- **Pitch:** A red-light / green-light dash to the HoH key. You move on GO and freeze when the cameras roll. It uses the web's own Physical title, and it is the most readable 25 seconds on the list.
- **How it plays:**
  - A lane runs from START to a key; your portrait is the marker.
  - A signal cycles GO (circle), STEADY (triangle, a 0.6 s warning, still safe) and FREEZE (square).
  - Holding advances you during GO and STEADY. Still holding more than 0.25 s into FREEZE means CAUGHT: you slide back 8% and are stunned for 1 s.
  - Reaching the key banks a lap and restarts the marker.
  - The 25 s schedule is seeded: GO lasts 1.0 to 3.0 s, FREEZE 1.0 to 2.0 s.
- **Controls:**
  - Hold Space or the right trigger.
  - The on-screen "Run" button toggles on press, not on release, so toggle users can stop instantly. Enter, A or a click toggle it.
  - Tab/shoulders, P/Start and Esc/B work as today.
  - New names: "Run", "Signal", "Lane".
- **Score:** `Round(min(10, 10 × distance / (0.85 × distance available in this attempt's schedule)))`. Dividing by the schedule makes every seed equally winnable. Ready-Set-Whoa's raw progress let the seed decide.
- **Where it lives:** a Skill variant under rules v4; no re-roll. If the rotation is ever widened, this is the Physical game.
- **Art:** Pack2 `progress_lane_9slice`, `leader_marker_9slice`, `competition_podium_label_9slice` (FINISH) and `elimination_badge_9slice` (CAUGHT); Pack5 `camera_identifier_blank_9slice`; Kit6 `portrait_ring`; GamesimIcons `key`.
  - Reduced motion: a static red border replaces the Pack4 `transition_red_overlay` wash, which must stay under about 0.25 alpha anyway.
- **Effort:** M. It is a new game type, but the hold input is reused.
- **Verified:**
  - Space/right-trigger press and release handling exists, but only for Endurance (`EpisodeDirector.Challenge.cs:278-284`); it needs to cover any hold-driven game.
  - Targets are scheduled at exact times (`MiniGameRun.cs:169-186`) and the clock runs in 1/120 s steps (`MiniGameRun.cs:122-127`).
  - The name is in the web list (`models/competition.ts:95`).
  - All the art files are present.

### 2. Finish beats + competition surface restyle (presentation only)
- **Pitch:** This is the UI/UX quality pass. It brings back what the web shows and the port dropped, and it puts the unused competition art on the three live games. It needs no rules version.
- **What changes:**
  - **(a) Finish beat.** Commit first, then keep the finished surface up for at least 1.0 s of unscaled time. Show the web's closing line: "N/M targets hit! (X% accuracy)", "Held for N seconds!", or "All matched!" / "Time's up! N matches". Put it in the timer card or a centred banner. Keep it out of "Attempt feedback", which runs off-screen at 4:3. Enter, A or a click on "Competition result action" skips it.
  - **(b) Memory cards.** Swap the words for the web's symbols: GamesimIcons house, key, trophy, target, eye and crown (crown stands in for the tent), plus Kit6 `ic_shield` and Pack4 `spark`. The icon sits above the word, and the label keeps the "Card N: WORD" / "\nMATCHED" shape. Cards move to `answer_tile_9slice` / `answer_tile_selected_9slice`; matched pairs add `ic_check`.
  - **(c) Reaction target.** Draw it on `answer_tile_selected` with a chevron (Kit6 `ic_chevron_right`, rotated) and a draining window ring. Add the web's 150 ms check-mark pop and "Get ready…" between targets.
  - **(d) Grip meter.** Use Pack2 `meter_track_empty` and `meter_stamina_fill` inside `endurance_stamina_panel_9slice`. Add the web's status words ("Holding strong!", "Slipping...", "About to fall!") in their own label, leaving the "Grip value" label as it is.
  - **(e) Clock warning.** At 5 s or less, change colour and add "Final 5 seconds" as text; add the `timer_ring` decoration.
  - **(f) Board opacity.** At 0.72 alpha the stage's lettering shows through the reaction board and the endurance column in the captures. Raise it to about 0.9, or add an inner scrim.
  - **(g) Re-layout.** Move the geometry in `Show()` into a `Layout()` that re-runs when the screen size changes. This also fixes the 4:3 captures.
  - **(h) Reduced motion.** Pass the reduced-motion setting to the surface; today only the assembly step receives it.
- **Controls:** nothing new. Progress text must keep "pairs", "hits" or "Effort", e.g. "3 / 8 pairs · 2 misses".
- **Score:** unchanged. The memory flip-back stays 1.0 s under v3 (the web uses 0.8 s); use 0.8 s only inside v4 definitions.
- **Where it lives:** all games; no rules version; no re-roll.
- **Effort:** S-M.
- **Verified:**
  - The memory faces are STAR…MOON (`CompetitionGameScreen.cs:16`); the web uses its own symbol list (`MemoryMatch.tsx:7`).
  - A ranked commit hides the surface on the same frame (`Challenge.cs:348`); only practice gets `ShowFinished` (`:293`).
  - The layout is computed once, from `FrameSize()` inside `Show()` (`CompetitionGameScreen.cs:86-91`).
  - No runtime code uses Pack2 Competition, Pack2 Meters or Pack3 CompetitionHUD; only `Editor/UiPackCatalogue.cs:283-360` lists them.
  - Cards, target and grip bar are flat fills (`CompetitionGameScreen.cs:177-180`, `375`, `472`).
  - The web's finish lines, 5-second pulse and 1000 ms completion delay: `ReactionTap.tsx:132,175`, `MemoryMatch.tsx:169,210`, `EnduranceHold.tsx:223,236,285`.
  - The season walk parses "Card " and "MATCHED" (`PortVerification.Season.Systems.cs:175-179`).

### 3. Spotlight Sequence
- **Pitch:** The studio's four spotlights play a light cue and you play it back. A Simon-style Mental game with no data dependency.
- **How it plays:**
  - Four pads (Up/Right/Down/Left), each with a direction word and a glyph (crown, key, star, eye).
  - A round starts with a 0.8 s "Watch", then each cue lights for 0.45 s with a 0.15 s gap, then "Your turn".
  - A clean round adds a cue: 3, 4, 5, 6, then 7, staying at 7.
  - A wrong press gives a new sequence of the same length ("Missed cue 3: new sequence"). Presses during the show do nothing ("Watch first").
  - 30 s on the clock.
- **Controls:**
  - Arrows or D-pad press the matching pad; a click works too.
  - Enter or A presses the focused pad, using the memory grid's explicit navigation.
  - New names: "Spotlight Up/Right/Down/Left", "Sequence tray".
- **Score:** `Round(max(0, 10·min(1, correct/25) − min(3, 0.3·wrong)))`. Every correct cue counts, so more progress never lowers the score.
- **Where it lives:** a Mental variant under rules v4; no re-roll.
- **Art:** Pack2 answer tiles; Kit6 `selection_stripe`, `timeline_dot` and `timeline_ring`; Pack3 `timer_ring`.
- **Effort:** M.
- **Verified:**
  - The Direction enum and `Tap(Direction)` exist (`MiniGameRun.cs:62,232`), but `Tap` returns early for anything except Reaction (`:234`), so this game needs its own input method.
  - The constructor works out the category from the game type (`:95-98`), so a second Mental game type needs the type to come from the definition.
  - The explicit navigation ring exists (`CompetitionGameScreen.cs:217-220`).
  - Photosensitivity: at most 1.7 cues a second, each confined to one pad.

### 4. Stack the Tower
- **Pitch:** The mockup's own HoH competition made playable. Discs ride a conveyor and one well-timed press drops the right colour onto your tower.
- **How it plays:**
  - An order strip shows the target sequence as badges: A → B → C → D.
  - Discs slide through a drop zone; pressing Drop while the needed colour is in the zone stacks it.
  - An empty zone or the wrong colour counts as a wobble (a mistake).
  - Stages of 4, 6 and 8 discs; the conveyor speeds up each stage; three stage tiles show progress.
  - The second authored pattern is "Reverse stack".
- **Controls:** one input: Space, Enter, gamepad South or right trigger, the "Drop disc" button, or a click on the zone. New names: "Drop disc", "Conveyor", "Tower".
- **Score:** `Round(clamp(9·stacked/18 + (all stacked ? remaining/limit : 0) − min(0.2·wrong, 3), 0, 10))`.
- **Where it lives:** a Skill variant under rules v4; no re-roll.
- **Art:** `answer_badge_A`–`D`, `progress_lane_9slice`, Pack1 `selection_ring_cyan`, `veto_player_active_9slice` and `competition_score_tile_9slice`.
- **Effort:** M. The conveyor and tower need new layout and animation.
- **Verified:**
  - mockup-05 shows exactly this competition: "Stack the discs in the correct color order", red → yellow → green → blue, "Complete all 3 stages first!".
  - The yard set already has the stacking prop (`VISUAL-TARGET.md:324`).
  - Discs carry letters, so colour is never the only cue.
  - The mockup's live per-houseguest progress strip cannot be honest: NPCs have no per-stage state before the commit. Their rows should read "Competing".
  - Size the drop window so the 7-disc worst case costs at most about 2 s.

### 5. Jury Questions (merges Jury Questions and Know Your Jury)
- **Pitch:** Final HoH Part 3, the web's own described format: the finalists answer questions about the jury. Every statement is template text built from saved fields.
- **How it plays:**
  - 10 statements. Each shows two juror portraits, A and B.
  - A statement is only asked when exactly one of the pair makes it true, so no answer is ambiguous.
  - Kinds: "was evicted Nth", "was nominated N times", "won a Head of Household / Veto", and card lines (occupation, hometown) for the Regular roster.
  - Fewer than 2 jurors: play First Impressions instead.
- **Controls:** Left/Right, 1/2 or D-pad select; Enter/A confirms; a click commits. Names "Answer A", "Answer B", "Question"; the caption is the juror's name.
- **Score:** `Round(10·max(0, right − wrong)/10)`, so random guessing is worth about 0.
- **Where it lives:** under rules v4, `FinalHoHPart3` maps to `jury-questions-v4`. The v3 selector still maps it to First Impressions, so there is no re-roll.
- **Art:** Pack4 FinalThree `memory_wall_thumbnail_frame_9slice`, `finalist_slot` and `finalist_slot_selected`; Pack3 `quiz_question_panel_9slice` and `answer_badge_A`/`B`; Kit6 `ic_check` and `ic_cross`.
- **Effort:** M, after the shared QuizRun exists.
- **Verified:**
  - The web describes Part 3 as "Jury Questions" (`competition.ts:148-151`).
  - Juror order is eviction order (`EpisodeEngine.cs:313-315`, `664-665`).
  - `nominationWeeks` is appended at every nomination (`:571`), and `hohWins`, `vetoWins` and `timesNominated` are saved counts (`EpisodeState.cs:58`).
  - All-Stars cards use real people's names, and some ages and occupations were written in this project (`CastTemplates.cs` provenance note). All-Stars boards must skip those two fields.
  - Juror sentiment is private and is never read.
  - Reach is limited: only the Part 1 and Part 2 winners play Part 3 (`EpisodeEngine.cs:152`).
  - Leave out ballot statements ("voted to evict X"): the vote-reveal lines roll off the log.

### 6. Grip Check (Swing Shift kept as a possible second pattern)
- **Pitch:** Restores the web's "RELEASE & RE-GRAB!" prompts to the endurance game, which is the least interactive of the three. It has web parity and is the cheapest new variant.
- **How it plays:**
  - The existing effort/recovery hold, plus GRIP CHECKs: the first at 4-6 s, each later one 5-8 s after the last window closes.
  - Each check gives a 2.0 s window to let go and grab again. Success shows "Nice!"; a miss shows "Too slow!" and costs 25 grip.
  - All checks are scheduled when the run is created, and none opens if its window would cross the bell.
  - Flavour banners like "Sprinklers on!" are cosmetic. Never attribute one to a houseguest.
- **Controls:** unchanged. A check needs two toggles, or one grab if you were already released.
- **Score:** unchanged: `Round(min(10, held/19.5·10))`.
- **Where it lives:** an Endurance variant under rules v4, added as a new pattern next to PressureWaves; no re-roll.
- **Art:** `timer_ring`, `endurance_stamina_panel_9slice`, Pack4 `spark`.
- **Effort:** S. It stays an Endurance game, so it needs no new game type.
- **Verified:**
  - The web rules: `EnduranceHold.tsx:19-23,134-167`.
  - Web bug not to port: a successful re-grab cancels the window timer, and only that timer schedules the next check (`:146-158,177`), so checks stop after the first success.
  - The meaning is inverted. In the web, holding fills the meter (`:86-94`); in Unity, holding drains grip (`CompetitionMiniGames.cs:41`). A check therefore costs effort seconds, and the rule disclosure must say so.
  - "Grip value" is parsed with `Split('%')`, and the walk reads the "Hold to earn effort" / "Release to recover" captions (`PortVerification.Season.Systems.cs:133-137`). Put the check in its own label.
  - The pattern mechanism exists (`CompetitionDefinitions.cs:7,26-32`).
  - The 2 s double toggle is tight for switch access; the accessible alternative covers it.

### 7. Before or After (merges The House Remembers and both Before or After pitches)
- **Pitch:** Two moments from this season: did one happen before or after the other? It is the first name in the web's Mental list, built only from saved records.
- **How it plays:**
  - 8 questions, each with a fixed 3.5 s window (28 s in all).
  - Facts come only from saved records:
    - nominations, from `nominationWeeks`;
    - evictions, from the juror order;
    - house events with a week earlier than the current one, from `houseEvents[].week` and `title`;
    - HoH-of-week, from the public "Nominated me in week N." memory (`EpisodeEngine.cs:585`).
  - That memory is capped at 30 per owner (`:1785`), so skip a HoH fact when its memory has gone.
  - Facts are only paired across different weeks.
  - In ranked play, the correct answers are shown only after the attempt ends.
- **Controls:** Left/Right, 1/2 or D-pad answer directly; a click works; Enter/A commits the focused tile. Names "Answer before", "Answer after", "Quiz prompt".
- **Score:** `Round(10·max(0, right − wrong)/8)`.
- **Where it lives:** a Mental variant under rules v4. The selector offers it only from week 3, a pure week test. No re-roll.
- **Art:** Pack3 `quiz_question_panel_9slice`, `quiz_answer_A_9slice` / `quiz_answer_B_9slice`, `answer_badge_A` / `B`, `timer_ring`; Pack3 SeasonRecap `recap_*_card_9slice` for the fact chips.
- **Effort:** M-L. It needs a new QuizRun and a fact builder in Simulation, so it can be tested with dotnet.
- **Verified:**
  - Competition, nomination, veto and eviction lines are all logged publicly (`EpisodeEngine.cs:475,565,608,612,316`), and each event records its phase (`:1854`).
  - "Before or After" is in the web's Mental list (`competition.ts:102`).
  - Cancelling a ranked quiz lets you retry the same questions, the same trade-off the current memory boards accept. Disclose it in the briefing.

### 8. Hide and Seek: House Keys
- **Pitch:** Three HoH keys hidden across a 4×4 grid of the house's real rooms, found with hot-and-cold clues.
- **How it plays:**
  - 16 spots, two in each room of `HouseRooms.All`. Three keys, never all in one room.
  - A search takes 0.4 s and reveals KEY or a clue: the grid-step distance to the nearest unfound key, shown as "1 – HOT", "2 – WARM" or "3+ – COLD".
  - Finding all three ends the game early. 25 s.
- **Controls:** identical to Memory. New names: "Hiding spot 1" through "Hiding spot 16".
- **Score:** `Round(clamp(3·keys + (keys = 3 ? remaining/limit : 0) − min(3, 0.3·empty), 0, 10))`.
- **Where it lives:** a Mental variant under rules v4; no re-roll.
- **Art:** answer tiles; Kit6 `ic_kitchen`, `ic_sofa`, `ic_bed`, `ic_home` and `ic_search`; GamesimIcons `key`.
- **Effort:** S-M. It is almost entirely the memory grid.
- **Verified:** the 8 rooms (`HouseRooms.cs:25-34`) and `Flip`'s input lock (`MiniGameRun.cs:262-287`). Its weight tuning runs in the Unity harness, not in `Tools/SimulationTests` (see section 0).
- **Why it ranks lower:** the tone is weaker (a grid, not the floor plan), and it is less fun than the others.

### 9. Hang in There (merges Stay Level and Hang in There)
- **Pitch:** Balance on the neon wall. Keep the needle in a shrinking steady band against a seeded swell.
- **How it plays:**
  - One input: holding leans right against a leftward swell.
  - The band narrows at 10 s and 20 s (30%, 24%, then 18% of the gauge), each time after a 2 s written warning.
  - Hitting either end is a slip: back on after 1.5 s, and no time counts during the slip.
  - A three-slip knockout variant can wait.
  - 30 s. It uses the web's Endurance name.
- **Controls:** Space or right trigger holds; the "Toggle lean" button toggles, and pointer down/up can also act as a hold.
- **Score:** `Round(min(10, 10·inBand/21))`.
- **Where it lives:** an Endurance variant under rules v4; no re-roll. It could be a v4 Part 1 option; placement there is still decided by the stats-and-rolls survival run.
- **Art:** `endurance_stamina_panel_9slice`, Pack2 meter track and fill, Kit6 `meter_zero_tick`.
- **Effort:** M.
- **Risk:** the swell must never outrun the band's recovery. Test that a scripted perfect player can reach 21 s on every seed.

### 10. Count the House
- **Pitch:** The house cameras cut between houseguests on a live feed. Keep count of one face.
- **How it plays:**
  - 3 rounds. Each has a 6 s feed of 10 cards at 2 Hz with a dim blank between cards, then a 3 s answer window.
  - Tally during the feed and adjust afterwards; the count locks automatically when the window ends.
  - The target appears 3 to 6 times per round.
- **Controls:** add during the feed; Up/Down adjust; Enter/A locks. New names: "Count up", "Count down", "Lock in count", "Count value", "Live feed".
- **Score:** exact = 1, off by one = 0.5 per round; `Round(10·points/3)`.
- **Where it lives:** a Mental variant under rules v4; no re-roll.
- **Art:** Pack5 `camera_identifier_blank_9slice` and `live_card`, Pack4 `memory_portrait_active_9slice`, Kit6 `portrait_ring`.
- **Effort:** M.
- **Verified:** portraits come from `CharacterPortraits.Get` (`Challenge.cs:37`). Portrait renders (UMA) can lag, so they must be ready before the countdown ends. The name must always show under the portrait.

### 11. Word Scramble
- **Pitch:** A web-parity port with the web's rules, timings and word values, but using house vocabulary and this season's cast names. The web's word list is Survivor vocabulary and cannot ship.
- **How it plays:**
  - 30 s. Pick letter tiles in order to build the word.
  - Points per solved word: 3.0 for 9+ letters, 2.5 for 7-8, 2.0 for 5-6.
  - A 0.5 s feedback beat after each check (input is ignored during it). Skip is free.
- **Word pool:**
  - Rooms, after dropping "the" and two-word rooms: KITCHEN, BACKYARD, BEDROOM, BATHROOM, HALLWAY.
  - Game words: HOUSEGUEST, NOMINATION, EVICTION, CEREMONY, ALLIANCE, BACKDOOR, FLOATER, BLINDSIDE, STRATEGY, ENDURANCE, HOUSEHOLD.
  - First names from the cast that are A-Z only and 5+ letters.
- **Controls:**
  - Letter keys pick tiles, Backspace clears, the D-pad moves and A picks.
  - P is the global pause key (`CompetitionGameScreen.cs:286`), so while the board has focus pause must move to Start or Tab. The alternative is dropping words that contain P.
  - Names: "Letter tile N", "Clear letters", "Skip word", "Built word".
- **Score:** `Round(min(10, total))`.
- **Where it lives:** a Mental variant under rules v4; no re-roll.
- **Art:** answer tiles, `quiz_question_panel_9slice`, `timer_ring`.
- **Effort:** L.
- **Verified:**
  - The web list (`WordScramble.tsx:8-13`) includes "Tribal Council", "Castaway" twice and "Target" twice, and its intro says "Unscramble Survivor words" (`:143`).
  - Scoring (`:39-44`) and the beats (`:113-122`) match the pitch.
- **Why it ranks last:** it is web parity, but keyboard typists outscore gamepad players.

## 2. Build order for the first three

1. **Finish beats + surface restyle.** It needs no rules version, no fixtures and no re-roll. It fixes defects the user can see in every current game: flat blocks, unused art, a ranked result the player never sees, no re-layout. Every later game also inherits the re-layout, the finish beat and the tile, timer and meter pieces.
2. **Ready, Set, Whoa!, with Grip Check in the same wave.** This is the best fun and tone per unit of effort, and it is the first new game type, so it forces the v4 plumbing to be done properly:
   - a `ScheduledRules = 3` constant replacing the `CurrentRules` checks (`MiniGameRun.cs:118,238,249,316`; `CompetitionMiniGames.cs:67`) before `CurrentRules` becomes 4;
   - `EpisodeValidation.cs:23` accepting 4, and the V13 migration test updated (`PersistenceV13MigrationTests.cs:62`);
   - the game type read from the definition instead of `For(category)` (`Challenge.cs:101,165,239`; `PortVerification.Season.Systems.cs:108`; `PortVerification.LookSheet.cs:461`; the constructor check at `MiniGameRun.cs:95-98`);
   - a season-walk driver, since the walk's default branch today plays memory cards.

   Grip Check is only a pattern, so it adds almost nothing and gives Endurance its third game.
3. **Spotlight Sequence.** It completes one new game per category, and it is pure mechanics with no season data.

**Freeze rule:** the v4 selector is frozen from the day `SeasonBuilder` first creates a v4 season (`SeasonBuilder.cs:96`, `EpisodeDirector.Season.cs:211`). Keep both emitting 3 until the whole wave is done, and let tests build v4 states directly. Anything added after that ships as v5.

The natural second wave (v5) is the shared QuizRun with Before or After and Jury Questions, plus Stack the Tower.

## 3. The reachability decision (the owner's call)

**Option A (recommended): variants inside Skill, Mental and Endurance under a frozen Version4 selector.**
- **Rotation unchanged:** HoH category is week % 3 and Veto is (week + 1) % 3 (`EpisodeEngine.cs:360-361`).
- **Selector shape:** `variant = catalogue[category][(offset + k) % n]`, where k is the category's count of earlier regular appearances. That is a pure function of week and phase, so every variant gets seen during a season. Week gates are allowed, e.g. Before or After from week 3. Final HoH rounds stay authored.
- **Consequences:**
  - NPC scoring and `Roll(s)` consumption are unchanged (`EpisodeEngine.cs:446-458`), and v1-v3 seasons stay exactly as they are.
  - The ranked seed includes the rules version (`CompetitionMiniGames.cs:24`), so a v4 board differs from a v3 board with the same seed. Fixtures that expect a specific board must pin rules 3.
  - The `competition-definition` log line changes.
  - No schema migration: `competitionRulesVersion` is already saved.
  - The briefing still shows SKILL, MENTAL or ENDURANCE, so every new game must honestly test that discipline. That rules out Dice as a Skill game.

**Option B: widen `CompetitionCategory` under a new version to produce Physical and/or Crapshoot.**
- WebRules already has the weights (`WebRules.cs:59,62`).
- Crapshoot needs a second draw per competitor, but the engine passes 0 for it today (`EpisodeEngine.cs:454`; comment at `WebRules.cs:48`). Every later `Roll(s)` in a new season therefore shifts.
- The guarantee that HoH and Veto use different categories (`CompetitionRulesV2Tests.cs:92-95`) must be proven again for a five-way rotation.
- Placement becomes more physical- and luck-weighted in those weeks, each category comes round less often, and the result card and journal show new category names.
- Every test seeded through `SeasonBuilder` needs a sweep.
- **What it unlocks:** honest homes for Dice Roll Derby (Crapshoot) and Ready, Set, Whoa! (Physical).

**Recommendation:** choose A for the first two waves. Take B only as a deliberate MASTER-PLAN decision, in its own rules version with nothing else bundled in.

## 4. Rejected, deferred and merged

| Concept | Verdict | Why |
|---|---|---|
| Dice Roll Derby | Rejected under A; reconsider with Crapshoot (B) | The choice is fake. Keep Best is offered whenever the current roll is worse, and Submit Best takes the maximum (`DiceRoll.tsx:140-156`), so rolling three times then submitting always wins. The expected bonus of about 1.34 beats the accessible alternative's 1.0 with zero skill. Labelling it SKILL would be dishonest. If B happens, give it a real push-your-luck decision. |
| Slide for Safety | Rejected | A third press-at-the-right-time Skill game alongside Stack the Tower and the reaction games. Keep it as the possible modern replacement for the old Stop the Marker game. |
| Pecking Order | Rejected | New drag-to-reorder interaction (L); the trust rounds test reading the notebook, not memory; inbound trust is hidden by design; thin before week 4. |
| Majority Rules: Read the Ballots | Deferred; a later question type | Honest, but ballots exist only as rolling vote-reveal lines, and there are none in week 1. Add it as a question kind once QuizRun is built from saved records. |
| Who Did It? | Deferred | Too much depends on log lines. `HouseDialogue.Pick` has distinct voices for only five houseguests, so quote questions would have several right answers. Private-audience leak risk. |
| Face the Facts (card match; and the 4-answer quiz) | Folded in | The card match is House Memory with different faces. The quiz's frame belongs to QuizRun. Both need the All-Stars restriction. |
| Competition reveal | Deferred (optional polish in wave 1) | Only meaningful once there are three variants per category. It must not look like a live draw, must be skippable and one-shot, and must keep the reel separate from "Competition title". Web timing: `CompetitionInitial.tsx:37-47`. |
| Freeze Frame | Merged into Ready, Set, Whoa! | Same game; its schedule-normalised scoring was kept. |
| Stay Level | Merged into Hang in There | Same game; its single-input lean was kept. |
| Swing Shift | Merged into Grip Check | Same "prompt during a hold" idea. Its second input (a direction while holding) is harder for mouse users. |
| The House Remembers, Before or After (house lane), Before or After (art lane) | Merged into Before or After | Same format. The house-lane fallback that reads log content was dropped. |
| Know Your Jury | Merged into Jury Questions | Same finale round. |

Key files:
- `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/Runtime/Presentation/CompetitionGameScreen.cs`
- `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/Runtime/Presentation/MiniGameRun.cs`
- `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/Runtime/Presentation/CompetitionMiniGames.cs`
- `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/Simulation/CompetitionDefinitions.cs`
- `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/Simulation/EpisodeEngine.cs`
- `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/Runtime/Episode/EpisodeDirector.Challenge.cs`
- `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/Runtime/Episode/PortVerification.Season.Systems.cs`
- `C:/Users/kelli/Gamesim Big Brother/ArtSource/reference/mockups/mockup-05.webp`
- `D:/gamesim-main/gamesim-main/src/components/mini-games/`
- `D:/gamesim-main/gamesim-main/src/models/competition.ts`
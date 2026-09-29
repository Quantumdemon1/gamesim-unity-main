# The endgame: Final 3 to the winner

Written 2026-09-29 from the owner's outline and thirteen mockups (the Final 3 house mode, the
three-part Final HOH, the Final 2 decision, jury preparation, the jury house, jury questioning
with receipts, the pleas, the hidden vote and the reveal, the winner and season results, and
the player as a juror), and from a map of what the game has today: seven readers over the
engine, the HUD, the intelligence, the strategy windows, the arena, the finale's cut scenes
and the frame, each verified against the code. The outline's principle governs every slice:
the interface gets simpler and more cinematic as the stakes rise, and the game gives evidence,
never a recommendation and never a percentage.

## 0. What exists, in one place

**The engine runs the whole endgame already, and presents it thinly.** The phases are frozen
and complete: after the final-four eviction an ordinary Social window at three, then
`FinalHoHPart1` (endurance, all three), `FinalHoHPart2` (skill, the two who lost part one),
`FinalHoHPart3` (mental, the two part winners; the winner becomes HoH), `FinalEviction` (the
HoH's `FinalEvict`, or the engine's choice for an NPC HoH), `JuryQuestioning`, `FinalSpeeches`,
`Jury`, `Finished`. The bracket is state (`finalPart1WinnerId`, `finalPart2WinnerId`, then
`hohId`). The final eviction settles Final 2 deals and promises, records the juror's standing
toward the player, and makes the third a juror. Questioning is the web's trait-keyed A/B quiz,
one question per juror, plus or minus ten on the juror's view; the speeches are generated for
NPCs and free text for the player; the jury is resolved in one commit from a scored model with
a ten-point roll per finalist and a written reason per ballot; the reveal card reads the ballots
one at a time in a seed-dealt order with a beat before any deciding vote, the host's line and
confetti. A player evicted earlier becomes a juror, spectates, puts two questions and casts one
vote the engine waits for. Placements are derived. There is no day counter: the simulation keeps
weeks, and the frame says so.

**The HUD has no mode.** One docked panel rebuilt every render; the top strip's week chip,
objective line and house pill; a six-row rail whose count tests pin; the cast strip, already
wide at three; a memory wall that lights the active and dims everyone else, the winner included
once the season ends. The finale's screens are the same panel: an A/B question, a
two-thousand-character speech editor, a paired vote, a text summary with four ways on, and the
season report, a scrolling card with the winner, the road to the end, the player's season,
Game Sense, the career, the ballots with reasons, standings, week by week and the cast table.

**The intelligence is there, and it is honest.** The ledger keeps competitions with placement,
the week's power (HoH, veto, nominees, evictee, backdoor), the player's ballots, claims kept
or lied, alliances with why they ended, opportunities (pleas, deals, the read), replies to the
house's cards and bloc calls, capped at 512 rows a kind; deals and promises keep their status;
Game Sense scores it into moments and misses. What the game knows about a juror's mind is
exactly two things: the juror's view of the player frozen as they left (a standing row with the
source `juror`), and a per-juror sentiment ledger the diary room moves and nothing reads at
the vote. The real vote model can be computed for any juror and finalist from saved state, and
that is precisely what no screen may show: the project keeps private leanings off every
player-visible surface, and the outline agrees ("that kills tension").

**The rules that shape every slice.** Captions are contracts and many finale ones are pinned:
`Evict {name}`, `Vote for {name} to win`, `Season report`, `Start a new season`, `Review the
season`, `Main menu`, `Continue jury questioning`, `Skip remaining questions`, `Submit final
speech`, `Skip my final speech`, `Continue to jury voting`, the host's "By a vote of {a} to {b},
{name}, you are the winner of Gamesim: The House!", the rail's six rows, the chrome panels'
names, and the finale's next-stop line. Any new saved field is schema 21, claimed with the story
session, with a migration and a frozen validator; command kinds are append-only; the finale's
seeded draws are pinned by count. The story session owns `Simulation/Story`, the story term in
the jury score, the threads' endings at the jury and the "final-three" close. Seating furniture
is authored and baked; runtime props have no colliders; bodies move only by routing; reduced
motion and batch runs skip stages and walk-outs, and the audited season walk must keep passing
through the finale on the cards.

## 1. Principles

1. **Derive before you save.** Every number on these screens comes from the ledger, the deals,
   the relationships, the statuses and the phase, unless the owner's design needs a fact the
   game does not keep. Four things do: the chosen final argument and its effect on the jury,
   questions drawn from history with receipts and five answers, a message to a juror, and an
   opening statement. Each is named below with its schema cost; nothing else touches a save.
2. **Evidence, not a recommendation, and never the model's own leaning.** A jury read is
   built only from what the player could know: the juror's standing as they left, the diary's
   sentiment events, the deals and promises they share, the replies the player gave them, the
   questioning's outcome, and the story's public grudges and bonds. Where none of that exists
   the word is *Unknown*. Percentages never appear.
3. **No made-up facts.** The simulation counts weeks, so the strip says `FINAL 3 · WEEK 9`,
   not "Day 28"; competitions are counted from the ledger; "episodes" are weeks. If the owner
   wants days, they are derived from the week and never saved (decision 1).
4. **Decorate a pinned caption, never rename it.** A card may say "Take Alex to the Final 2"
   above a button whose caption is `Evict Jordan`; the season results may head its ways on
   with "View final stats" as long as the button still reads `Season report`.
5. **The cards keep their order and timing.** The finale's beats are presentation over a
   commit that has already happened: the votes are cast in one step and the reveal reads
   them; a "has voted" beat, a bracket, a crowning, an exit and a walk-in are all stages and
   cards over saved state. Reduced motion and batch runs see the cards as today.
6. **The same authored sets.** The finale's set is the living room the cut-scenes plan §8
   authors for eviction night: the U of couches, the two red wingbacks facing the room, the
   screen on the south wall, the exit door on the west wall. The Final 2 decision ceremony,
   the jury walking in, the questioning, the pleas, the vote and the reveal all play on it.
   Nothing here dresses a room twice.

## 2. The screens and beats, mapped to the mockups

| # | Mockup | Slice |
|---|---|---|
| 40 | The Final 3 overview map: `You (Emma)`, the three big cards, the objectives, the jury card | F1 |
| 34 | Storyboard: Final 3 mode begins, trust talks, Part 1, Part 1 winner, Part 2, Part 3 announced | F1, F3, F7 |
| 38 | Final HOH Part 2 of 3: the bracket strip, the two stations, the challenge, "winner advances" | F3 |
| 31 | Storyboard: Part 3, the crowning, the bedroom reflection, the decision ceremony, the finalist, the exit to the jury | F3, F2, F7 |
| 39 | Choose who joins you in the Final 2 | F2 |
| 37 | Prepare your final case: the narrative, the résumé, three signature moments, lock | F4 |
| 36 | The jury house: seven jurors with qualitative states, knows and missing, what matters to this jury | F4 |
| 35 | Jury questioning: the juror speaking, the question, four responses, the season receipt | F5 |
| 32 | Storyboard: the jurors take their seats, statements, questioning, pleas, votes into the box, the winner | F7 |
| 30 | The player as a juror: two finalist cases, review the speeches, the private vote | F6 |
| 33 | Finale live show: the winner with the vote, the two others, the final jury vote strip | F6 |
| 29 | The cinematic winner: the throne, the crown, "by a vote of", the jury strip, three ways on | F6 |
| 28 | Season results: placements, the hero, the jury vote, the timeline, statistics with ranks, legacy | F6 |

### F1 Final 3 mode: the frame (HUD, derivable)

From the commit that leaves three active until the final eviction:

*Built (the F1 commit). What landed differs from the first draft in three places, marked
"as built".*

- **The top strip.** The week chip leads with `FINAL 3` (`FINAL 2` at two, `FINALE` when the
  season is over) and reads `Week n · phase` beside it; the objective chip's title reads
  "Final Head of Household ahead" until the final Head of Household is crowned, then "One
  decision remains" for the player or "The final Head of Household decides", "The jury
  decides" at two and "Season complete" after; the house pill's third cell is the jury's size
  while nobody holds the house. Chrome panel names and the next-stop line are untouched.
- **The objectives card.** A right-column card headed `FINAL 3 OBJECTIVES` in the vibe
  card's place, three rows with a mark each and where each stands under it: "Win the Final
  HoH" (Part n of 3; Won or Lost once the final eviction is reached), "Decide who to
  trust" (the partner's name when a Final 2 deal or promise binds the player), "Prepare your
  case" (At the Final 2, F4). At two the rows are "Answer the jury's questions"
  (n of m), "Deliver your final speech", "Await the jury vote"; F4 swaps in "Prepare your
  final case". A player on the jury sees the juror's three instead: follow the final Head of
  Household, weigh the finalists, cast your vote. *As built:* the jurors' faces are a
  `The jury (n)` strip at the foot of this card rather than a card of their own. The column
  clears the status band by 24 units with the vibe card in it and the two-row feed buys 58;
  two cards and the gap between them did not fit, one card with a strip does. The strip is
  where F4's jury house opens from.
- **The cast strip at three.** *As built:* the rail folds the departed (jury, evicted,
  expelled) at the endgame and keeps the three; the winner and runner-up keep their chips
  after the reveal. The trait row is deferred: the wide chip's rows are full (mood face,
  badge, name, standing track, mood word, standing tag) and a seventh does not fit the 96.
- **Fewer feed events.** The recent-events card shows two rows at the endgame instead of
  three. The live feed's room preference is not built.
- **The memory wall** already lights the three and dims the rest. Nothing to do until F6.
- **Endgame Preparation.** The final-four eviction's Advance opens a Social window at three
  like any other week's (`EpisodeEngine.cs`, the Eviction case goes to Social with no size
  check; validation allows Social at three), and leaving that window is what starts the first
  part of the final Head of Household (the Social case: three active means `FinalHoHPart1`,
  not `HoH`). The window deals one seat under the week rules (two under the old pool). *As
  built:* the free-time screen's head at three reads `ENDGAME PREPARATION` over the same
  action count, "Three remain. Study in the Diary Room, settle who you trust, and get ready:
  the final Head of Household is next.", and the frame reads FINAL 3 from the window on.
  The tiles are the week's own for now. Study is the Diary Room's study command (it feeds
  the Part 1 endurance bonus under competition rules 3 and later). A Final 2 deal is pitched
  from a houseguest's screen. *Compare the finalists* comes with F2 and *Prepare your case*
  with F4, as tiles on this screen. *Repair a jury relationship* stays F8's message-to-a-juror
  command.
  *Correction:* the first F1 commit (fb0f86b) said there was no window at three and left the
  head unchanged, with a decision 11 for an engine window. That read the Social case's
  `Advance` as the Eviction case's. The window exists; decision 11 is withdrawn.
- **Tests.** `EpisodePlayModeTests.Endgame.cs`: a fixture at Part 1 asserts the chip's
  words, the objective's title, the jury cell, the card's rows and marks and faces, the
  two-row feed, the card's foot above the status band and the rail of three; a fixture in
  the window at three asserts FINAL 3, "Ahead" and the `ENDGAME PREPARATION` head over the
  same action count; a fixture at the Final 2 asserts the same frame at two; a check reads
  the frame's words from cloned states. A juror is counted as the engine's jury vote counts
  one (Jury, or Evicted on an older save: `EpisodeHud.IsJuror`). The chrome overlap and
  compact suites re-run unchanged.

### F2 The comparison and the Final 2 decision (HUD, derivable, knowledge-gated)

- **Final 3 comparison** (from the preparation tile): one column per other finalist with the
  gated evidence: *Competition résumé* (HoH and veto wins, times nominated, from state; ranked
  Strong / Moderate / Light by the house's spread), *Social respect* (the finalist's alliances
  the player knows of and the public bonds), *Jury bitterness* (public grudges aimed at them,
  and jurors they evicted as HoH from the power rows), *Known jury support* (jurors who shared
  a known alliance or a public bond with them: a count), *Uncertain jurors* (the rest),
  *Final 2 deal* (Confirmed when a Final 2 deal or promise binds them to the player, the deal's
  status word; Unknown otherwise), *Trust* (the player's outbound score as a band). Each value
  carries the word *Confirmed*, *Suspected* or *Unknown*: Confirmed is a row the player is
  party to or a public fact, Suspected a visible alliance, bond or grudge, Unknown nothing.
- **The decision screen** (the final eviction with the player as HoH): the two candidates as
  cards with portrait, archetype, traits, a line of theirs (the persona's quote, or their last
  eviction-speech line), the game résumé, *Your relationship* (the trust band, the Final 2
  agreement's status), *Jury read* (the gated counts: supportive, leaning, unknown), and *If
  you take them* bullets derived from the same rows ("Honours your Final 2 deal", "Strong
  competition résumé", "Two jurors they evicted"). Below: "This decision cannot be undone" and
  the two paired actions with their pinned captions `Evict {name}` under a headline "Take
  {other} to the Final 2". The panel's paragraph stays for the tests that read it.
- **Tests.** EditMode on the gate (a Final 2 deal shows Confirmed, a known alliance shows a
  supporter, nothing shows Unknown, and the vote model's own numbers never appear in the
  screen's text); PlayMode: the decision screen's cards and the paired actions by caption.

### F3 The Final HOH as an event (presentation over the engine's three parts)

- **The bracket card.** On each part's resolution a takeover card of a new kind, `FinalHoHPart`,
  plays before the standings: three faces, the part winner badged `PART n WINNER`, the bracket
  drawn from the fields ("Emma → Part 3 · Alex vs Casey → Part 2", then "Part 3: Emma vs
  Alex"). The competition screen's eyebrow reads `FINAL HEAD OF HOUSEHOLD · Part n of 3` and
  its foot "Winner advances to Part 3" or "Winner becomes the final Head of Household".
- **The crowning beat.** When part 3's commit lands in the final eviction, a card of a new kind,
  `FinalHoHCrowned`: "YOU ARE THE FINAL HEAD OF HOUSEHOLD / One decision remains" for the
  player, "{name} is the final Head of Household" otherwise; the winner's `Won`, heads turned
  (all three are still active, so they turn). Then the decision screen (F2).
- **The yard.** The runtime sign reads `FINAL HOH · PART n · {category}`; the station markers
  say "Final HoH station"; the audience seat for the finalist sitting out is the lounger as
  today. The three authored games stay (Pressure Cooker, Switchback Signals, First
  Impressions); the mockups' "press your luck" and "buzzers" are a rules version 5 with new
  mini-games, a decision (3), not this round.
- **Tests.** The card kinds by name in a PlayMode fixture at three; the captions of the
  competition flow unchanged (`Begin the next competition`, `Review competition results`,
  `Continue to the next ceremony`).

### F4 Jury preparation and the jury house (HUD; one saved fact)

- **Prepare your final case** (mockup 37), from the Final 2's first render: (1) *Choose your
  narrative*, five themes; (2) *Your season résumé*, derived: wins, nominations survived, weeks,
  major moves (Game Sense moments and the power rows where the player held power), key
  alliances (alliance rows with why they ended), betrayals (deals and promises broken, by and
  against the player), key weeks; (3) *Select signature moments*, three from a list built from
  Game Sense moments, the player's power rows, kept promises, won opportunities and the
  ledger's bloc calls, each with its week and a line; (4) *Lock final argument*. The lock
  writes the player's final speech: a templated speech from the theme and the three moments,
  saved as today's `finalSpeeches` entry (the editor's `Submit final speech` and `Skip my final
  speech` remain as the alternative for a player who would rather write). **The saved fact:**
  the theme and the three moment references persist and reach the jury, so they are schema 21
  fields on the speech record under a `finaleRulesStartWeek` (0 for old seasons, which keep
  today's behaviour). The jury effect is small and named: a theme that matches what a juror
  values (the score model's own weights: respect for a strategic theme, loyalty for a loyal
  one, relationship for a social one) adds a few points to that juror's view, capped, before
  the roll; the questioning's responses are ordered by the theme (F5). Old saves: no theme,
  no effect.
- **The jury house** (mockup 36), a screen from the jury card: the seven as portrait cards
  round the living room's U, each with a qualitative state and one line. The state is gated
  as in §1: *Supportive* (their standing as they left was warm and nothing since has cooled
  it), *Leaning* (warm with a broken promise or a refused plea since), *Open* (neither warm
  nor cold), *Skeptical* (cold), *Bitter* (a public grudge aimed at the player, or the player
  evicted them as HoH and no deal bound them), *Unknown* (no standing row: the player never
  read them). *Knows*: the facts the player and they share (alliances, deals, the player's
  votes against them that the reveal made public). *Missing*: what the player did that they
  left before (moves after their week). *May be swayed by*: the juror's traits mapped to the
  response kinds (F5). *What matters to this jury*: the traits most jurors share, worded.
  *Discussion highlights*: derived lines from the jurors' sentiment events and standing weeks
  ("Week 6 · Avery left cold on you"), never invented chatter. **Observe only**: nothing on
  the screen acts.
- **Tests.** EditMode on the bands from fixtures (a warm standing with a kept deal is
  Supportive; a cold one Skeptical; a juror the player evicted with no deal Bitter; no row
  Unknown); the theme's effect under the new rules and its absence under the old; the schema
  21 migration and frozen validator sweeps.

### F5 Jury questioning as a live event (engine under new rules; the HUD; the set)

- **Questions from history.** Under `finaleRulesStartWeek`, each juror's question is built from
  a receipt the ledger holds about the player and them, by category: *accountability* (a deal
  or promise the player broke with them), *ownership* (the player's HoH nominated or evicted
  them, or the player's ballot went against them), *jury management* (a plea of theirs the
  player refused, a reply card answered coldly), *strategy* (the player's biggest move), *social*
  (the relationship that kept the player safe), *comparison* (why the player over the other
  finalist), *mistake* (a Game Sense miss), *personal* (a promise kept or a bond). The juror's
  traits pick the category among those with a receipt; a juror with no receipt asks a
  comparison. The exchange record gains the category, the receipt reference and the response
  key (schema 21); the validator branches on the rules week and old saves keep the catalogue.
- **Five responses**, a static catalogue by category with a label, a line and a risk word: own
  the move, explain the strategy, appeal to loyalty, deflect, and reveal the truth (offered only
  when the player holds a private fact the juror lacks: a bloc call they never saw, a ballot
  cast against their expectation). The effect keeps today's magnitude and draw count: one roll
  as now, the sign decided by the fit between the response and the juror's traits and the
  receipt (owning to a direct juror lands, deflecting to a bitter one costs), so the seeded
  parity tests keep their counts.
- **The reaction.** One line after the commit, from the engine's note: "{juror} considers your
  answer" or "{juror} seems unconvinced", in the outcome chips the conversation already draws;
  never a number.
- **The HUD.** The docked panel becomes the live layout of mockup 35: the juror's portrait and
  question left, the responses centre with their risk words, the *Season receipt* right (the
  week, the row's line, a quote from the record), the hint "The jury is listening". The player
  as a juror keeps their three toned questions. The captions `Continue jury questioning` and
  `Skip remaining questions` stay on their buttons.
- **The set** (with F7): the jury seated on the U, the finalists in the wingbacks facing them,
  the camera cutting to the speaking juror on each exchange and to the finalist on the answer.
- **Tests.** EditMode: a broken deal yields an accountability question with its receipt; no
  receipt yields a comparison; the response's sign by fit; draw counts unchanged; old saves
  validate. PlayMode: the live layout's controls by caption.

### F6 The pleas, the vote, the reveal, the winner, the results (HUD and cards)

- **The pleas.** The player's locked argument is their speech; a player who wrote one keeps
  it. The world beat: the speaker's talk loop in the wingback, the jury listening (F7).
- **The player as a juror** (mockup 30): a dedicated screen with the two finalists' gated
  cases (résumé, alliances the juror knew, rivalries from public grudges, betrayals from broken
  deals the juror saw), `Review final speeches` opening the saved speeches, the paired actions
  with the pinned caption `Vote for {name} to win`, and "Your vote is private. It will be
  revealed at the finale." *What matters to you* shows the player's diary persona, read-only.
- **The hidden vote.** A stage sequence over the commit (F7): each juror rises, walks to the
  crown box on the stage's end, drops a card, the strip says "{name} has voted.", sits. Under
  reduced motion and batch runs, a card lists "{name} has voted." lines instead.
- **The reveal on the screen.** The jury reveal gains the screen path and the beats the vote
  reveal has (a `ScreenSurface`, `BeatReached`), so it plays on the living room's board with
  the finalists in the wingbacks; the stage cuts to each juror as their vote is read and to
  the count; the three-line result block (BY A VOTE OF / 5 TO 2 / EMMA WINS GAMESIM) on the
  screen frame with the pinned host line kept under it; gold: the memory wall's winner tint
  (status-aware, so the winner and runner-up are lit after the season), the world confetti, the
  winner's `Won`, the room's `Cheered`, and heads turned to the winner (the head-turn learns
  that the winner and runner-up count on finale night).
- **The winner screen** (mockups 29 and 33): a full-frame card after the reveal with the
  winner's portrait in a gold frame, "WINNER · {name} · by a vote of {a} to {b}", the runner-up
  (and the third for a Final 3 season), the jury strip with each vote, the season's numbers
  (houseguests, weeks, competitions from the ledger, twists from the story's count if it
  offers one), and three ways on decorated over the pinned four: "View final stats" (`Season
  report`), "Watch season recap" (the weekly recaps replayed in order, `Review the season`),
  "Continue" (`Main menu`), with `Start a new season` kept.
- **Season results** (mockup 28): the season report reorganised to the mockup's grid:
  placements (derived), the winner hero with a quote (the winner's speech excerpt) and traits,
  the final jury vote row and tally bar, the season timeline (the power rows week by week
  with the recap's headline per week; portraits in place of thumbnails), player statistics
  with ranks among the cast (competition wins, HoH wins, veto wins, alliances formed,
  betrayals, nominations survived; ties as T-1st) and the four scores as Game Sense's faces
  plus a legacy score from the career, relationships and legacy (closest ally = highest
  mutual score, biggest rival = lowest, betrayed = the deal they broke, lasting respect = the
  warmest juror standing), the player's diary reflection (the last diary memory of the season),
  signature moments (the locked three, else Game Sense's), and the analysts' line (Game
  Sense's verdict). "Episode archive" and "Export share card" are out of scope (decision 8).
- **Tests.** The reveal's existing tests unchanged on the HUD path; a screen-path test as the
  vote reveal's; the winner card's ways on by caption; the report's sections by name.

### F7 The cut scenes (world; after the cut-scenes plan's S1 to S4)

All on the authored living room and the west-wall door:

1. **Final 3 mode begins** (storyboard 34, frame 1): the `The Final Three` card on the living
   room's screen instead of the HUD when the house can stage it; the three seated on the U;
   the memory wall as is.
2. **Trust talks** (frame 2): the story session's, if they want a Final 3 scene; otherwise the
   free-time conversations on the U.
3. **The parts in the yard** (frames 3 to 6, storyboard 38): the per-part sign and the audience
   as F3 says; the yard screen and stations are the cut-scenes plan's S7.
4. **The crowning** (storyboard 31, frame 2): the card on the yard's screen when S7 lands, the
   HUD until then.
5. **The reflection** (frame 3): a camera beat on the HoH at the memory wall for six seconds
   before the decision screen opens, skippable; no walk.
6. **The decision ceremony** (frames 4 and 5): a stage kind for the final eviction: the HoH at
   the mark beside the screen, the other two standing before them at two marks in front of
   the wingbacks; the card `FINALIST` on the screen; the chosen sits in a wingback, the third
   plays `Evicted`, the HoH looks at them. The finale-night gate is narrowed so this one
   stage may play although the commit has entered questioning.
7. **The exit to the jury** (frame 6): the third walks out through the west-wall door under a
   `TO THE JURY` sign variant, the house watching, the door shut and held, as the eviction's
   exit does; then their body joins the bench for the finale.
8. **The finale** (storyboard 32): the jurors walk IN through the same door and take the U
   (the roster rule widened for finale night as it was for the evicted, bodies bound, the
   bench yielding to the stage); the finalists in the wingbacks; the pleas with a talk loop;
   the questioning cuts (F5); the hidden vote at the crown box (F6); the reveal on the screen;
   the celebration.
9. **Reduced motion and batch runs** see the cards; the audited walk stays on the cards.

### F8 Later, and decisions

- **A message to a juror** ("repair a jury relationship"): a new command appended after
  `CallTheVote`, legal only in the Social window at three under the finale rules, costing the
  seat, moving the juror's standing toward the player by a bounded amount once per juror and
  writing a sentiment event with the reason. A decision (6).
- **Opening statements** before questioning: a second speech record per finalist, schema 21;
  the storyboard's frame 2. A decision (7); without it the pleas are the statements and the
  finalists' persona lines open the finale.
- **New Final HOH games** (rules version 5): decision 3.
- **The share card and the episode archive**: decision 8.

## 3. Engine changes, all under schema 21 and `finaleRulesStartWeek`

| Change | Fields | Effect | Old saves |
|---|---|---|---|
| The final argument | `finalSpeeches[].theme`, `finalSpeeches[].momentRefs` (3) | a capped bonus to jurors whose values match the theme; the responses' order | none |
| History questions | `juryExchanges[].category`, `receiptKind`, `receiptId`, `responseKey`; five responses | the sign of today's ±10 by fit; one draw as now | the catalogue A/B as today |
| A message to a juror (F8) | a command kind; no field | the juror's standing, bounded, once | refused |
| Opening statements (F8) | `openingStatements[]` | none on the vote | none |

The rules week is set by the season builder for new seasons and by nothing else; the
migration adds it as 0. Both the frozen validator and the live one branch on it. The seeded
draw counts per question and per answer are pinned and kept. The story session is asked for
schema 21 before the first of these lands, and told what the jury effect reads of theirs
(nothing: the story term stays theirs and untouched).

## 4. Build order

| | Slice | Done when |
|---|---|---|
| F1 | The Final 3 frame: the strip, the objectives card with the jury strip, the folded rail, the two-row feed and the ENDGAME PREPARATION head on the window at three (built; the trait row deferred, see F1) | PlayMode in the window at three, at Part 1 and at two asserts the words, the marks and the card's foot; the overlap and compact suites green |
| F2 | The comparison and the decision screen, gated | The gate's EditMode tests; the decision by caption |
| F3 | The Final HOH: the bracket card, the crowning card, the yard's signs, the eyebrows | Both card kinds in a fixture at three |
| F4 | Preparation and the jury house; schema 21 with the argument | Bands and the effect under both rules; migrations green |
| F5 | Questioning from history with receipts and five responses; the live layout | Receipts, categories, the sign by fit, draw counts pinned |
| F6 | The juror's screen, the hidden vote card, the reveal on the screen, the winner screen, the results grid | The reveal's tests on both paths; the ways on by caption |
| F7 | The cut scenes on the authored set and the door | Captures of the ceremony, the exit, the walk-in, the reveal; the audited pipeline |
| F8 | The message to a juror, opening statements, new games, the share card | As decided |

F1 to F3 need no schema and no set; F4 and F5 need schema 21; F6's screen reveal needs the
living room's authored screen; F7 needs S1 to S4 of the cut-scenes plan. Each lands as before:
the offline compile, the Unity-free subset, the filtered suites on the D: copy, the audited
pipeline alone from the main checkout, a PR. Effort: F1 2 days, F2 3, F3 2, F4 4, F5 6, F6 5,
F7 5; about four weeks before F8.

## 5. Decisions for the owner

1. **Days.** (A, recommended) weeks on every strip, as the simulation keeps them. (B) a
   derived day (seven a week, never saved) on the strips only.
2. **"Take Alex" wording.** (A, recommended) the pinned `Evict {name}` stays on the button
   under a "Take {other} to the Final 2" headline. (B) rename the caption and every pin.
3. **Final HOH games.** (A, recommended) dress the three authored games with the bracket and
   the crowning now. (B) a rules version 5 with the mockups' press-your-luck and head-to-head.
4. **The final argument's reach.** (A, recommended) the theme and moments are saved and give
   matching jurors a small capped bonus. (B) the argument only writes the speech.
5. **The jury read's source.** (A, recommended) what the player could know, with Unknown where
   nothing is known. (B) the model's own leanings, softened into words.
6. **A message to a juror.** (A, recommended) F8, after the screens. (B) in F1, as a command.
7. **Opening statements.** (A, recommended) none; the pleas are the statements. (B) a second
   speech record.
8. **The share card and the episode archive.** (A, recommended) out of scope. (B) a share card
   as an exported PNG of the winner screen.
9. **The player as a juror's "What matters to you".** (A, recommended) the diary persona,
   read-only. (B) sliders that do nothing. (C) omitted.
10. **Watch season recap.** (A, recommended) the weekly recaps replayed in order behind the
    pinned `Review the season`. (B) omitted.

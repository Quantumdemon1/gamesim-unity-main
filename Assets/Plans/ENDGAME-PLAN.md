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
vote the engine waits for. Placements are derived, though two readers disagree today (F6).
There is no day counter: the simulation keeps weeks, and the frame says so.

**The HUD has one mode, the endgame, built in F1.** From the window at three to the finished
season the rail folds the departed, the week chip leads with `FINAL 3`, `FINAL 2` or `FINALE`,
and the objective chip's title follows the phase. The pill counts the jury while nobody holds
the house. At three and at two the Objectives card takes the vibe card's place and the feed
keeps two rows. The rest is as it was: one docked panel rebuilt every render; the top strip's
week chip, objective line and house pill; a six-row rail whose count tests pin; the cast strip,
already wide at three; a memory wall that lights the active and dims everyone else, the winner
included once the season ends. The finale's screens are the same panel: an A/B question, a
two-thousand-character speech editor, a paired vote, a text summary with four ways on, and the
season report, a scrolling card with the winner, the road to the end, the player's season,
Game Sense, the career, the ballots with reasons, standings, week by week and the cast table.

**The intelligence is there, and it is honest.** The ledger keeps competitions with placement,
the week's power (HoH, veto, nominees, evictee, backdoor), the player's ballots, claims kept
or lied, alliances with why they ended, opportunities (pleas, deals, the read), replies to the
house's cards and bloc calls, capped at 512 rows a kind; deals and promises keep their status;
Game Sense scores it into moments and misses. What the game keeps about a juror's mind is two
things, and neither is something the player learned. The engine writes a standing row with the
source `juror` for every evictee as they leave, whatever the player did, holding the juror's
real score for the player: that is the vote model's own relationship term, and only Game Sense
reads it, after the season. The sentiment ledger is the diary's record of the whole jury, not
evidence about one juror: each juror enters at half the player's own score for them, with one
Entered jury event dated week 0, and the diary's shifts move every juror by the same amount for
one reason. The Diary Room shows its mean and the career reads the order it filled. What the
player learned about a juror's view of them is their `read` hits, the only learned rows that
carry it: told and overheard rows are always between two others. The real vote model can be
computed for any juror and finalist from saved state, and the jury model's raw leaning stays
off every screen; the outline agrees ("that kills tension"). The eviction model's lean is
already shown, through VoteRead's filter: the evaluator's own terms, stripped of the ones the
player has not learned, in the words firm, leaning, torn and unknown (decision 5).

**The rules that shape every slice.** Captions are contracts and many finale ones are pinned:
`Evict {name}`, `Vote for {name} to win`, `Season report`, `Start a new season`, `Review the
season`, `Main menu`, `Continue jury questioning`, `Skip remaining questions`, `Submit final
speech`, `Skip my final speech`, `Continue to jury voting`, `Continue episode`, `Close`, the
finalist's answers `A · {option}` and `B · {option}` (F5), the final parts' `Accessible
alternative: steady 1.5-point bonus` (1-point before rules 4), `Watch eligible housemates
compete` and the result card's `Continue from competition results`, the host's "By a vote of
{a} to {b}, {name}, you are the winner of Gamesim: The House!", the finished panel's "Winner:
{name}. Runner-up: {name}.", the report's "{winner} beat {runner-up} in the jury vote.", the
rail's six rows, the chrome panels' names, and the finale's next-stop line. Any new saved
field is schema 21, claimed with the story session, with a migration and a frozen validator of
today's shape (§3); command kinds are append-only; the finale's seeded draws are pinned by
count. The story session owns `Simulation/Story`, the story term in the jury score, the
threads' endings at the jury and the "final-three" close. Seating furniture is authored and
baked; runtime props have no colliders; bodies move only by routing; reduced motion and batch
runs skip stages and walk-outs, and the audited season walk must keep passing through the
finale on the cards. The walk presses a caption only when exactly one live, interactable
button carries it, and after the reveal it opens the station and presses `Review the season`.
So a new screen never shows a pinned caption while the panel under it shows the same one, and
never hides the next caption the walk presses: each new screen in the walk's path (the
decision, the case, the live questioning, the winner screen) leaves that caption on one button.

**Words and sounds.** Screens drawn with the HUD's helpers are localised already: the HUD's
text sink asks the table, and the control keeps its English caption as its name. The takeover,
the sting and the jury reveal have no sink. So the bracket and crowning cards, the has-voted
card, the winner screen and the result block ask `Localisation.Text` at their text, and
composed lines are `Localisation.Format` patterns with the English as the key. The host's line
stays English-keyed, as its tests pin. A card raises no sound of its own; the commit's cue
does. A part's commit already sounds `CompetitionWin`, and the reveal sounds `Vote` per juror
and `Finale` at the result. The final eviction's commit logs `final-eviction`, which the commit
cue does not map, so the Final 2 decision sounds like a button press today; it gets a mapped
cue. A new cue is appended to `HouseAudio.Cue` with a recorded WAV and a full importer meta.

## 1. Principles

1. **Derive before you save.** Every number on these screens comes from the ledger, the deals,
   the relationships, the statuses and the phase, unless the owner's design needs a fact the
   game does not keep. Four things do: the chosen final argument and its effect on the jury,
   questions drawn from history with receipts and five answers, a message to a juror, and an
   opening statement. Each is named below with its schema cost; nothing else touches a save.
2. **Evidence, not a recommendation, and never the model's own leaning.** A jury read is
   built only from what the player could know: the player's last `read` of the juror before
   they left (never the `juror` standing row, which holds every juror's real score for the
   player and is the vote's own term), the deals and promises they share, the replies the
   player gave them, the questioning's outcome, the public record of the house's power and of
   ballots a reveal proved, public grudges, and bonds the player could see. A grudge has no
   visibility field: public means what the profile's mood line already shows, a grudge whose
   cause is nominated, replacement or replacement-veto; every other grudge is private and never
   counts. F2's gate reads no grudge rows at all (F2). A bond counts only when the player is in
   it or a public couple fact names the pair (F2). The diary's sentiment events are not
   evidence about one juror, since every shift moves the whole jury. Where none of that exists
   the word is *Unknown*. Percentages never appear, and the one jury number already on a
   screen, the Diary Room's Recorded jury impression, is decision 12.
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
   screen on the south wall, the exit door on the west wall. None of it is authored yet: the
   set is what the cut-scenes plan's decisions 2 and 3 settle and its S1 to S4 build. The
   west-wall door and the south-wall screen are their recommended options, not yet chosen or
   built. If the owner takes the yard's front door, F7's exit and walk-in use it; if the north
   wall, the screen and the wingbacks stand there. The Final 2 decision ceremony, the jury
   walking in, the questioning, the pleas, the vote and the reveal all play on it; until S3
   lands, F6's reveal plays on today's runtime screen and hot seats (F6). Nothing here dresses
   a room twice.

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
  Household, weigh the finalists, cast your vote. The juror's rows are for a player whose
  status is Jury, or Evicted on an older save (`EpisodeHud.IsJuror`). The built card also
  gives a player production removed "Question the finalists" and "Cast your vote"; it takes
  F6's gate, and a removed player's rows say they watch as a spectator. *As built:* the
  jurors' faces are a `The jury (n)` strip at the foot of this card rather than a card of
  their own. The column clears the status band by 24 units with the vibe card in it and the
  two-row feed buys 58; two cards and the gap between them did not fit, one card with a strip
  does. The strip is one of the doors to F4's jury house. It is not drawn under the compact
  HUD, since the right column returns before the card, and it stands down under every stage
  layout, which the window at three and the Final 2's questioning and speeches are, so F4
  gives the jury house two more doors.
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
  The free-time screen's YOUR CONTEXT card does not follow yet: it prints the literal
  "Current Objective" and reads the final-four week's roles, which stay in state until the
  window closes, so the final-four Head of Household reads "You are HOH" and the surviving
  nominee "You are on the block". At three the card takes the chip's words: its title line is
  `EpisodeHud.ObjectiveTitle`, and its role line says the final Head of Household is next,
  not the final-four week's role. The tiles are the week's own for now. Study is the Diary
  Room's study command, in free time. Under competition rules 3 and later its banked
  preparation goes to the player in all three parts of the final Head of Household: as
  endurance in Part 1, capped at 10, and in the score bonus in Parts 2 and 3. No part spends
  it. The Diary Room's copy says it does not boost the final HoH, in three places in
  `EpisodeDirector.DiaryRoom.cs`, and `EpisodePlayModeTests.DiaryRecord.cs` pins that
  sentence; the copy is corrected by rules version, with its pin. The context card fix and
  the copy fix land with F2. A Final 2 deal is pitched from a houseguest's screen. *Compare
  the finalists* comes with F2, and *Prepare your case* and *The jury house* with F4, as
  tiles on this screen; the case can be read from here, but its lock waits for the Final 2.
  *Repair a jury relationship* stays F8's message-to-a-juror command.
  *Correction:* the first F1 commit (fb0f86b) said there was no window at three and left the
  head unchanged, with a decision 11 for an engine window. That read the Social case's
  `Advance` as the Eviction case's. The window exists; decision 11 is withdrawn.
- **Tests.** `EpisodePlayModeTests.Endgame.cs`: a fixture at Part 1 asserts the chip's
  words, the objective's title, the jury cell, the card's rows and marks and faces, the
  two-row feed, the card's foot above the status band and the rail of three; a fixture in
  the window at three asserts FINAL 3, "Ahead" and the `ENDGAME PREPARATION` head over the
  same action count, and asserts the context card's words when that fix lands with F2; a
  fixture at the Final 2 asserts the same frame at two; a check reads the frame's words from
  cloned states. A juror is counted as the engine's jury vote counts one (Jury, or Evicted on
  an older save: `EpisodeHud.IsJuror`). The chrome overlap and compact suites re-run
  unchanged, but their overlap and clipping sweeps draw the opening week, never the Final 3
  frame, so they do not cover the objectives card: the Final 3 fixture adds the clip and
  overlap checks at both text sizes. Each new finale screen (the comparison, the decision,
  the case, the jury house, the live questioning, the juror's screen, the winner screen, the
  results grid) joins the openers of NoPanelClipsItsCopyAtEitherTextSize from a finale
  fixture.

### F2 The comparison and the Final 2 decision (HUD, derivable, knowledge-gated)

- **Final 3 comparison** (from the preparation tile): one column per other finalist with the
  gated evidence: *Competition résumé* (from state: HoH and veto wins plus a Final HoH Part 1
  or Part 2 win, which state keeps after the finale; the Part 3 win is already an HoH win, so
  it counts once; times nominated from the contestant's own count, never the power rows;
  ranked Strong / Moderate / Light by the house's spread), *Social respect* (the finalist's
  alliances the player knows of, and bonds the player is in or a public couple fact names),
  *Jury bitterness* (from the public record only: the power rows where the finalist as Head
  of Household nominated the juror, a nominee the veto saved or a replacement nominee
  included, and a vote against the juror proven at a reveal, a kept claim; never from grudge
  rows, which are the vote model's input), *Known jury support* (jurors who shared a known
  alliance with them, from the ledger's alliance rows the player can see: a count),
  *Uncertain jurors* (the rest), *Final 2 deal* (Confirmed when a Final 2 deal or promise
  binds them to the player, the deal's status word; Unknown otherwise), *Trust* (the
  player's outbound score as a band). Each value carries the word *Confirmed*, *Suspected* or
  *Unknown*: Confirmed is a row the player is party to or a public fact, Suspected a visible
  alliance or a bond the player can see, Unknown nothing. A juror's bond is always apart:
  leaving moves every bond of theirs there, public or private, so the save cannot say whether
  the house knew it. Bonds form private, and the only public one is the player's showmance,
  which also spreads a public couple fact. So a bond counts as evidence only when the player
  is in it or a public couple fact names the pair, and an apart bond between two others
  counts as nothing.
- **The decision screen** (the final eviction with the player as HoH): F2's own layout, one
  column per candidate. In each column a candidate card: the portrait, the name and card
  line, the traits, their own intro line (the persona's quote, or their last eviction-speech
  line), the evidence rows each marked *Confirmed*, *Suspected* or *Unknown* (the game
  résumé, with a part win beside the HoH and veto wins; *Your relationship*: the trust band
  and the Final 2 agreement's status; *Jury read*: the gated counts, supportive, leaning,
  unknown), and *If you take them* lines derived from the same rows ("Honours your Final 2
  deal", "Strong competition résumé", "Two jurors they nominated"). Under the card the
  headline "Take {name} to the Final 2", then the other finalist's pinned `Evict {name}`
  button. Below both columns: "This decision cannot be undone". The candidates are not
  BallotCards, as F6's finalists are: the cards carry the evidence rows and principle 4's
  headline, and BallotCards' fixed 196 by 250 cards cannot hold them; and a card that is
  itself `Evict {name}` would make the card of the person you keep evict someone else, or
  reverse the plan's "take" framing. Each `Evict {name}` stays on one live button, so the
  panel under the screen never shows it twice (§0). The panel's paragraph stays for the tests
  that read it. The final eviction's commit gets its mapped cue (§0).
- **Portraits.** Every finale portrait is the UMA studio headshot, the only source the owner
  allows; the cast screen's glamour photos are not a fallback. A face not yet built, or a
  clone without UMA, draws the initial in the same slot. So tests count the portrait slot per
  person, as the jury strip's `Juror` discs are counted, never a face. F4 and F6 draw theirs
  the same way.
- **Tests.** EditMode on the gate (a Final 2 deal shows Confirmed, a known alliance shows a
  supporter, nothing shows Unknown, a nomination of the juror by the finalist as Head of
  Household and a kept claim count toward Jury bitterness while a grudge row alone does not,
  an apart bond between two others counts as nothing, a Part 1 or Part 2 win counts in the
  résumé and the Part 3 win once, and the vote model's own numbers never appear in the
  screen's text); PlayMode: the decision screen's columns, the portrait slot per person and
  the paired actions by caption; the comparison and the decision join the panel sweep's
  openers (F1).
- **As built** (the F2 commit). The gate is `FinalistRead` in the simulation, pure and
  Unity-free, so the dotnet subset runs its tests. What differs from the text above:
  - *The rows.* Each card carries seven, each with Confirmed, Suspected or Unknown: Competition
    record (the grade, then HoH, Veto, Nominated and a part won), Your relationship (the
    player's standing word, the relationship web's thresholds, held together by an EditMode
    test), Final 2 agreement, Known alliances, Known jury support, Jury bitterness and
    Uncertain jurors ("n of m"). The jury is counted as support, bitterness and uncertain, not
    supportive, leaning and unknown: Leaning already names the vote read's lean.
  - *The Final 2 agreement* shows what binds first, in the order the final eviction settles
    them: a binding deal, the player's own standing promise, an offer waiting, then the latest
    of any standing. "None with you" is Unknown; any agreement the player was party to is
    Confirmed.
  - *A shared alliance* counts as support however it ended. Why an alliance ended (the
    ledger's `turned` and `soured`) is worked out from scores the player never sees, so it
    cannot be the player's evidence.
  - *Bitterness* is the finalist nominating the juror as Head of Household (a veto save or a
    replacement included) and a vote proven at a reveal: a kept claim, or a lie the reveal
    exposed when the ballot went to the other nominee on that week's block.
  - *The line of theirs* is their meet-and-greet line (`WebIntroductions.IntroLine`, seeded,
    no draw). A last eviction speech does not survive the week turn, so there is none to use.
  - *If you take them* counts the jury that will vote: today's jurors and the finalist cut,
    whose own lean toward the one kept gets its line ("Taylor joins the jury with reason to
    hold a grudge against Maya (nominated them)").
  - *The warning* sits in each column between the headline and the control, not below both
    columns: the screen opens scrolled to its first control, and a warning at the head of a
    tall screen was out of sight. Above the columns, when there are any, a line names the
    Final 2 agreements with jurors that the eviction breaks either way.
  - *The comparison* is a free tile, first among the moves on Endgame Preparation ("Costs no
    action"), with `Back to endgame preparation`; its cards say "If you win and take".
  - *With them:* the context card at three reads "You are in the Final 3" under the
    objective chip's title, and the Diary Room's study copy follows the competition rules
    (from rules 3 preparation counts in every competition, the final Head of Household's
    three parts included); its pin reads the same function. The week chip's endgame line may
    shrink to 11 points rather than lose its end.
  - *Tests:* sixteen on the gate (the dotnet subset runs fifteen; the standing-word match is
    Unity-only); the decision screen at both text sizes (the columns, the portrait slot per
    card, headline over warning over control, the warning in sight as the screen opens, the
    copy fitting its card, the week chip fitting); the comparison opening and closing with
    nothing committed.

### F3 The Final HOH as an event (presentation over the engine's three parts)

- **The bracket card.** On each part's resolution a takeover card of a new kind, `FinalHoHPart`,
  plays before the standings: three faces, the part winner badged `PART n WINNER`, the bracket
  drawn from the fields ("Emma → Part 3 · Alex vs Casey → Part 2", then "Part 3: Emma vs
  Alex"). The card carries no button and never covers the part's `Accessible alternative`
  caption, `Watch eligible housemates compete` or the result card's `Continue from
  competition results`, which the audited walk presses at every final part. The competition
  screen and the result card both read the director's title, `Final HoH · Part n · {game}`,
  and split it once at the first separator: today the screen's eyebrow reads FINAL HOH and
  loses the part, and the result card heads itself `PART n · {GAME}` under a FINAL HOH week
  line. F3 changes the award title and the split together, so the eyebrow reads `FINAL HEAD
  OF HOUSEHOLD · Part n of 3` and the card's heading is the game's name again. The screen's
  foot reads "Winner advances to Part 3" or "Winner becomes the final Head of Household".
- **The crowning beat.** Part 3's own commit crowns the Head of Household in state but stays
  in Part 3 for the standings. The crowning card, a new kind, `FinalHoHCrowned`, plays on the
  commit that enters the final eviction: the Advance behind `Continue to the next ceremony`
  after Part 3's standings. It keys on the phase change, as the Final Three card does, in the
  same branch after the fallout check, so a story fallout card on that commit takes the
  screen instead. It reads "YOU ARE THE FINAL HEAD OF HOUSEHOLD / One decision remains" for
  the player, "{name} is the final Head of Household" otherwise; the winner's `Won`, heads
  turned (all three are still active, so they turn). Then the decision screen (F2).
- **The yard.** The runtime sign keeps its three lines: `FINAL HOH · PART n` where HEAD OF
  HOUSEHOLD stands today, then the category, then the game's title the arena already
  appends. The station markers say "Final HoH station"; the audience seat for the finalist
  sitting out is the lounger as today. The three authored games stay (Pressure Cooker,
  Switchback Signals, First Impressions). A press-your-luck game already ships (Roll the
  Dice, rules 4's luck game); dealing it to a final part and adding the mockups' buzzers is a
  rules version 5, a decision (3), not this round.
- **Tests.** The card kinds by name in a PlayMode fixture at three; the captions of the
  competition flow unchanged (`Begin the next competition`, `Review competition results`,
  `Continue to the next ceremony`), and the final parts' `Accessible alternative: steady
  1.5-point bonus`, `Watch eligible housemates compete` and `Continue from competition
  results` each on one live button with the bracket card up; a test that pins the eyebrow
  and the result card's heading for a final part, since none does today.
- **As built** (the F3 commit). What differs from the text above:
  - *The bracket card plays as each part opens, not as the part before resolves.* A part's
    own commit plays its standings card at once, and that card sorts above every takeover
    (105 against 100), so a card on that commit would sit under the standings. The Advance
    behind `Continue to the next ceremony` logs no competition, so a card keyed on the phase it
    arrives in plays alone, as the Final Three card does. Part 2 opens with "Final HoH · Part
    2": the Part 1 winner badged `WON PART 1`, the two who play badged `PART 2`, and "{A} won
    Part 1 and waits in Part 3. {B} and {C} play for the other seat." Part 3 opens with the two
    part winners badged `WON PART 1` and `WON PART 2` and the third `WATCHING`. The kind is
    `FinalHoHPart`; the takeover gained a title and a line per call, and `PlayingKind`.
  - *The panel closes as a bracket card plays.* The takeover takes no input, and the click
    that moves it on would otherwise land on the next part's briefing beneath it, where
    `Accessible alternative` commits. Every walk and every player reopens the station for the
    next decision, as after the Final Three card.
  - *The crowning* (`FinalHoHCrowned`) plays on the commit into the final eviction: "Final
    Head of Household", the winner badged `FINAL HOH` and the other two `FINAL 3`, the line
    the player's ("You won it. One decision remains: who sits beside you in the Final 2.") or
    the winner's; the winner plays Won and the room turns to them. The decision screen does
    not open by itself: nothing reopened the station after that commit, and an automatic open
    would race every walk's own. The objective chip already says "One decision remains".
  - *The award title* is "Final HoH, Part n of 3", with no separator inside it: the game
    screen's eyebrow and the result card split the title at its first one. The eyebrow reads
    `FINAL HOH, PART 1 OF 3`, the result card heads itself with the game and carries the part
    in its week line (which may now shrink to fit), and before rules 3 the title names the
    mini game so the card still has one. `EpisodeDirector.FinalHoHPartLabel` is the one
    source of "Part n of 3", the objectives card included.
  - *The player is spoken to.* Every line that can hold the player says "you" ("You won Part
    1 and wait in Part 3.", "You and Will play for the final Head of Household.", "You
    watch.", "At stake: who joins you in Part 3."): the default player is called "You", and a
    name with a third-person verb read "You waits". Names on the endgame's lines skip a title
    ("Dr. Will Kirby" is Will), through `FinalistRead.FirstName`.
  - *The stakes line* on the briefing says what each part is for (Part 1's winner goes
    straight to Part 3; Part 2 is for the other seat, against the Part 1 winner; Part 3 is the
    final Head of Household and the choice). The old line said the same for all three.
  - *The yard sign* reads `FINAL HOH · PART n` where `HEAD OF HOUSEHOLD` stood; the markers
    are named `Final HoH station` (a name only: the discs carry no text). Whether the sign
    reads mirrored from the house is untested, as it was before.
  - *With it:* the week chip's endgame line gives up the week when "Week 4 · Final eviction"
    is wider than its slot at the smallest size; F2's fit check had missed it, because TMP
    does not report a line cut short in its box as overflowing. The endgame tests now count
    the characters drawn against those held.
  - *Tests:* the bracket, standings and crowning walked through a real Part 1 to the final
    eviction (the cards' kinds, words and badges, the panel closed, every word fitting, the
    winner's Won); the titles, labels and stakes from cloned states under rules 2 and 4.

### F4 Jury preparation and the jury house (HUD; one saved fact)

- **Prepare your final case** (mockup 37), from the Final 2's first render: (1) *Choose your
  narrative*, five themes; (2) *Your season résumé*, derived: wins (HoH and veto wins plus a
  Final HoH part win, as F2 counts them), nominations survived (from the contestant's
  `timesNominated` and `nominationWeeks`, never the power rows), weeks, major moves (Game
  Sense moments and the power rows where the player held power), key alliances (alliance
  rows with why they ended), betrayals (deals and promises broken, by and against the
  player), key weeks; (3) *Select signature moments*, three from a list built from Game Sense
  moments, the player's power rows, kept promises, won opportunities and the ledger's bloc
  calls, each with its week and a line; (4) *Lock final argument*. The final eviction's power
  row lists the two the last Head of Household chose between as its nominees, although it
  counts as no nomination: a reader of the power rows skips it for nominations and keeps it
  as the HoH's eviction. The screen never stands between the audited walk and the
  questioning's buttons.
- **The lock and the saved fact.** The Final 2's first render is questioning: the final
  eviction draws the first question in its own commit, and the live validator refuses any
  speech record while questioning is open. So the lock is a command of its own, appended to
  the command kinds, legal once for a player finalist from the final eviction's commit until
  their speech is in. It saves the theme and the three moment references on a schema 21
  record of its own (`finalArgument.theme`, `finalArgument.momentRefs`), not on
  `finalSpeeches`, under a `finaleRulesStartWeek` (0 for old seasons, which keep today's
  behaviour). The command carries the keys, not the text. When the speeches open, the
  templated speech from the theme and the three moments fills the editor, inside the 2,000
  characters every command's text is held to, and goes in through `Submit final speech` as
  today, so `Skip my final speech` keeps its meaning and a player who would rather write
  still can. The first question is drawn before any lock, so ordering the responses by the
  theme (F5) is a render-time read and is never saved. A tile in the window at three may open
  the screen to read, but the lock waits for the Final 2.
- **The jury effect** is small and named. What a juror values comes from their traits, read
  as the questioning reads them (`WebJuryQuestioning.GetPrimaryTrait`), through a fixed table
  from trait to theme; the jury house's *May be swayed by* shows the same table. The bonus is
  a named, capped constant: its own term in `WebJuryVoting.Score` beside the story term, under
  the rules week, read at the vote. It writes no relationship and spends no draw, so the
  vote's two rolls per juror stay as pinned. The model's four weights stay as they are: they
  are shared by every juror and cannot say what one juror values. Old saves: no theme, no
  effect.
- **The jury house** (mockup 36), a screen that opens from the jury strip, from a row on the
  Final 2's panel beside *Prepare your final case*, and from a tile on the window at three,
  because the strip is not drawn under the compact HUD and stands down under every stage, and
  those panels are stages. It shows every juror, counted as `EpisodeHud.IsJuror` counts them:
  the cast less the two finalists and anyone production removed. That is one to ten from the
  cast screen (six in the default house of eight), and up to fourteen in a house of sixteen
  from an imported save; the mockup's seven is one case. They are portrait cards round the
  living room's U (portraits as F2), wrapping past what the U seats, each with a qualitative
  state and one line. The state is gated as in §1 and never bands from the `juror` standing
  row: the engine writes one for every juror as they leave, with their real score for the
  player, so it is the vote's own term, and it stays for Game Sense after the season. The
  band starts from the player's last `read` row on the juror before they left, banded as the
  read's own line bands it (25 and over warm, -25 and under cold), with its week; then the
  deals, promises, replies and public grudges move it. *Supportive* (the last read was warm
  and nothing since has cooled it), *Leaning* (warm with a broken promise or a refused plea
  since; it takes another word, because Leaning already names VoteRead's lean on the
  notebook's vote page, decision 5), *Open* (neither warm nor cold), *Skeptical* (the last
  read was cold), *Bitter* (a public grudge aimed at the player, or the player evicted them
  as HoH and no deal bound them), *Unknown* (no read hit: none, or only a miss or a
  deflection). A grudge is public when its cause is nominated, replacement or
  replacement-veto, as the profile's mood line shows it (`EpisodeEngine.MoodTarget`). A
  grudge for a lie found out, a broken deal, promise or oath, a betrayed alliance, a pile-on
  or a story beat is private and never counts toward Bitter. A grudge keeps aging two a week
  while its holder is on the jury, so the read uses its severity now, and a forgiven grudge is
  gone. *Knows*: the facts the player and they share (alliances, deals, the player's votes
  against them that the reveal made public). *Missing*: what the player did that they left
  before (moves after their week). *May be swayed by*: the trait-to-theme table and the
  juror's traits mapped to the response kinds (F5). *What matters to this jury*: the traits
  most jurors share, worded. *Discussion highlights*: derived lines from the ledger's rows:
  the power row of the week the juror left (who held the house, who nominated them), the
  player's reads of them, and the deals, promises, ballots and replies between them ("Week 6
  · Avery left on your nomination"), never invented chatter. A diary shift shows once, as a
  line about the whole jury, and nothing reads a sentiment event's week. **Observe only**:
  nothing on the screen acts, until F8's message to a juror if the owner takes it (decision
  6). The Diary Room's Recorded jury impression, the sentiment ledger's mean seeded from the
  player's own view of each juror, would disagree with these bands; F4 settles it with the
  jury house (decision 12).
- **Tests.** EditMode on the bands from fixtures (a warm read with a kept deal is
  Supportive; a cold one Skeptical; a juror the player evicted with no deal Bitter; no read
  Unknown, and so is a juror with a `juror` row and only a missed read; a private grudge
  never makes Bitter); the trait-to-theme table pinned, and a juror whose trait misses the
  theme gets nothing; the theme's effect under the new rules and its absence under the old;
  the jury house at eight and at the largest house; a compact-HUD test that opens the jury
  house from the panel; the schema 21 migration, the new frozen validator and the load pins
  moved to 21 (§3).
- **As built, F4a** (the jury house; no saved field). The story session could not be reached
  to claim schema 21, so F4 is split. F4a builds what the saved rows already hold; F4b keeps
  *Prepare your final case*, the lock, the theme's saved fact and its jury effect, and the
  schema. What differs from the text above:
  - *The read* is `JuryHouseRead` in the simulation, pure and Unity-free. A juror's band
    starts from the player's last `read` row on them (their view of the player, 25 and -25),
    never the `juror` row, a miss or a deflection.
  - *Bitter* is read from the public record, not from grudge rows: the player nominated them
    as Head of Household (a veto save included), named them the replacement, used the veto to
    put them up, broke the tie that evicted them, or evicted them at the final eviction. A
    grudge's cause is last-writer-wins (a public `nominated` turns private when a broken deal
    stacks on it) and its removal turns on a score the player cannot see, so the rows cannot
    say what the house saw. The freshest evidence speaks: a read after the player put them up
    is their view after it, and a read in the same week came after that week's ceremonies
    (except the opening week, whose free time comes first). An eviction by the player is
    always Bitter.
  - *Leaning* is **Wavering**: a warm read cooled since, by a promise the player broke them, a
    plea the player refused, a vote to evict them, or a deal between them that broke. "Since"
    counts only what provably came after the read. The record holds no break week and no
    phase, so a same-week campaign or reveal cannot be placed against it. A Final 2 agreement,
    broken only at the final eviction, always counts.
  - *Knows* lists the alliances they shared (ended or not), the deals and promises between
    them with their standing, and the player's public votes against them. *Missed* lists the
    weeks the player held the house or used the veto after they left, the competitions the
    player won, and the deals the player struck. *Between you* is the dated lines: the week they
    left and whose week it was, what the player did to them, the player's reads, votes and
    replies. *What matters to this jury* counts the traits the jurors lead with (the trait the
    questioning reads). *May be swayed by* waits for F4b's theme table.
  - *The doors*: a free tile at three beside the comparison, and a row after the Final 2's
    questioning and speech controls, so the opening focus stays on the panel's own controls.
    Both open a view over the station's panel, with `Leave the jury house`. The door on the
    jury strip is not built: the strip is chrome in free roam, and a screen that opens from
    anywhere needs a panel flag of its own, as the notebook has. The Jury phase has no door:
    the vote is underway.
  - *The cards*: one per juror with a photo slot, the band in its colour, the reason, and up to
    three lines of each section, in rows of three, two or one by width and text size. Nothing on
    a card is a control.
  - *Tests*: ten on the read (Unity-free), and the jury house opened from both doors at both
    text sizes, with nothing committed and every line fitting its card.

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
  key (schema 21); the live finale validator branches on the rules week (§3) and old saves
  keep the catalogue.
- **Five responses**, a static catalogue by category with a label, a line and a risk word,
  drawn as EventChoices tiles (caption, line, risk word), as house events and reply cards
  draw theirs: own the move, explain the strategy, appeal to loyalty, deflect, and reveal the
  truth (offered only when the player holds a private fact the juror lacks: a bloc call they
  never saw, a ballot cast against their expectation). The effect keeps today's magnitude and
  today's draws. A question takes two when it is prepared: today its tone and its A/B order,
  under the finale rules the category tie-break and the order of the five responses, a
  history question included. The sign takes no draw: today it is whether the pick is the
  trait-keyed answer, under the finale rules the fit with the traits and the receipt (owning
  to a direct juror lands, deflecting to a bitter one costs). The Change that applies the ±10
  takes its two, the reciprocal event and score, and still moves the player's view and the
  arc. A player juror's answer keeps its one fallback draw. `AnswerJury`'s A or B check and
  the validator's answer check branch on the rules week.
- **The reaction.** One line after the commit, the engine's note as it reads: "{juror} was
  impressed by your response during jury questioning." or "{juror} was unconvinced by your
  response during jury questioning.", in the outcome chips the conversation already draws;
  never a number. A softer wording under the finale rules is a new note, and
  EpisodeFinaleTests' check for "impressed" and "unconvinced" keeps the old rules' note.
- **The HUD.** The docked panel becomes the live layout of mockup 35: the juror's portrait and
  question left, the responses centre with their risk words, the *Season receipt* right (the
  week, the row's line, a quote from the record), the hint "The jury is listening". The player
  as a juror keeps their two questions, one to each finalist, each picked from three tones
  (neutral, bitter, supportive), with no effect on the vote. The captions `Continue jury
  questioning` and `Skip remaining questions` stay on their buttons. The finalist's answers
  `A · {option}` and `B · {option}` are pinned, and so are the player juror's `{tone} ·
  {question}`; old seasons keep them. Under the finale rules the five responses get captions
  of their own, and the labels `Jury question` and `Jury answer` keep their names. The
  audited walk starts its season from the cast screen through the director, which switches
  the rules on, so it reaches the five responses: its finale route (`PortVerification.Season.cs`)
  learns the new captions in the same slice and keeps its `jury-finalist-question` capture,
  or the audited pipeline stops at the first question.
- **The set** (with F7): the jury seated on the U (which overrides the cut-scenes plan's
  standing jury, F7), the finalists in the wingbacks facing them, the camera cutting to the
  speaking juror on each exchange and to the finalist on the answer.
- **Tests.** EditMode: a broken deal yields an accountability question with its receipt; no
  receipt yields a comparison; the response's sign by fit; EpisodeFinaleTests pins the two
  draws per question, the two per answer and the player juror's one; old saves validate.
  PlayMode: the caption map, the answer test and the keyboard walk play seasons with no rules
  week, so they stay on A and B; a new test of the live layout uses a fixture with the rules
  week set and finds its controls by caption.
- **As built, F5a** (the live layout; no saved field). History questions and the five
  responses change the saved exchange record, so they wait for schema 21 with F4b (F5b). What
  F5a builds, and where it differs from the text above:
  - *The live layout* inside the questioning panel: the juror asking (their photo, name and the
    trait they lead with) and the question on the left; the answers in the centre, the A/B
    answers, the player juror's tone questions, the recorded answers and Continue, under their
    own captions and in their own order, so the opening focus, the keyboard ring and every walk
    are unchanged; and the *Season receipt* on the right. The hint "The jury is listening."
    sits over them, and `Skip remaining questions` stays after them in the panel's own column,
    with the jury house door after that. At the larger text the three stack.
  - *The receipt* is the jury house's read of the juror asking (F4a): where they stand with the
    player and why, the latest dated lines between them and the deals and promises they share.
    It quotes the record, not the juror: no saved row holds a juror's words (the reply rows keep
    keys, the eviction speeches are cleared each week, and a reply card's line is picked by a
    score the player cannot see).
  - *The trait* the juror leads with is shown beside the question. It is the key the catalogue's
    right answer is written to, and it was already public (the conversation header, the
    finalist cards, the jury house); the five responses of F5b make fit with the traits the
    point.
  - *The reaction* is the engine's own note, rebuilt from the saved exchange so it survives a
    reload, in the allied green or the conflict red, never a number, in the answers' column.
    Nothing reads the answer key before the commit.
  - *The player as a juror* sees the finalist they ask on the left and what they know of them on
    the right (the finalist cards' first facts). The lines speak to the player: "You ask
    {finalist}", "{juror} asks you".
  - *The set* is F7's.
  - *Tests*: the live layout at both text sizes (the columns and what each holds, Skip after
    them, nothing about the answer before it), the reaction line equal to the engine's note
    and kept through a reload, and the player juror's columns.

### F6 The pleas, the vote, the reveal, the winner, the results (HUD and cards)

- **The pleas.** The player's locked argument is their speech; a player who wrote one keeps
  it. The world beat: the speaker's talk loop in the wingback, the jury listening (F7).
- **The player as a juror** (mockup 30): a dedicated screen for a player whose status is Jury,
  or Evicted on an older save (`EpisodeHud.IsJuror`), with the two finalists' gated cases
  (résumé, alliances the juror knew, rivalries from public grudges, betrayals from broken
  deals the juror saw), `Review final speeches` opening the saved speeches, the two finalists
  as BallotCards, each card the control with the pinned caption `Vote for {name} to win`
  (BallotCards' heading and line become parameters, since today they read EVICTION VOTE), and
  "Your vote is private. It will be revealed at the finale." *What matters to you* shows the
  player's diary persona, read-only. Portraits as F2. Today the director draws the juror's
  ballot for any player who is not a finalist, a removed player included, although the engine
  takes a jury vote only from Jury or Evicted and neither questions nor waits for a removed
  player. A player production removed watches the finale as a spectator: no ballot, no
  questions, rows that say so. The spectator banner moves into the frame for a player who is
  out of the game, so a juror sees it with the panel closed, beside the juror's objectives:
  today it is a row of the open panel, and with no panel open its labels are parented to
  nothing and pile up.
- **The hidden vote.** A stage sequence over the commit (F7), starting from `Continue
  episode`, the finalist's press into the jury's vote: each juror rises, walks to the crown
  box on the stage's end, drops a card, the strip says "{name} has voted.", sits. Under
  reduced motion and batch runs, a card lists "{name} has voted." lines instead.
- **The reveal on the screen.** The jury reveal gains the screen path and the beats the vote
  reveal has (a `ScreenSurface`, `BeatReached`), so it plays on the living room's screen. It
  needs no authored screen: it plays on today's runtime screen, as the vote reveal does, with
  the finalists in the two hot seats, once the finale-night gate narrows and the stage room
  gains the winner (F7.6, which this rides); it moves to the wingbacks and the authored
  screen when the cut-scenes plan's S3 lands. The stage cuts to each juror as their vote is
  read and to the count, inside a juror's beat: the jury's pacing stays as CeremonyTruthTests
  pins it. The result block on the screen frame has the host's three shapes. A count: BY A
  VOTE OF / 5 TO 2 / EMMA WINS GAMESIM. A tie, which the default jury of six makes ordinary:
  "The jury is tied, 3 to 3.", then "Under the house's tie rule, the win goes to {name}.",
  never "by a vote of". A jury of one: "With the jury's only vote". The block is new labels
  around the pinned Result, Progress and Host texts, never a rewrite of them, with the pinned
  host line kept under it. Gold: the memory wall's winner tint (status-aware, so the winner
  and runner-up are lit after the season); the confetti, which today is a canvas burst across
  the reveal card, seeded, three seconds on the wall clock, never under reduced motion, with
  the winner's hold pinned to outlast it: on the screen path it rides the card's world-space
  canvas, and confetti in the living room itself is new work under the same seed, length and
  reduced-motion rule; the winner's `Won`, which already plays on the winner commit, kept;
  the room's `Cheered`; and heads turned to the winner. Today the head-turn skips every body
  that is not Active, and after the jury's commit nobody is: on finale night it counts the
  runner-up and the jurors on the bench, and the winner is the target and never turns.
- **The winner screen** (mockups 29 and 33): a full-frame card after the reveal with the
  winner's portrait in a gold frame, "WINNER · {name} · by a vote of {a} to {b}" (or the
  tie's and the jury of one's words, as the result block), the runner-up (and the third for
  a Final 3 season), the jury strip with each vote, the season's numbers (houseguests, weeks,
  competitions from the ledger, twists from the story's count if it offers one), and ways on
  decorated over the pinned four: "View final stats" (`Season report`), "Continue" (`Main
  menu`), with `Start a new season` and `Review the season` (the notebook, as today) kept,
  and "Watch season recap" as decision 10 settles: under (A), a control of its own. The
  winner screen sits over the finished panel and leaves its "Winner: {name}. Runner-up:
  {name}." in place for the whole-season and career tests.
- **Season results** (mockup 28): the season report reorganised to the mockup's grid:
  placements (below), the winner hero with a quote (the winner's speech excerpt) and traits,
  the final jury vote row and tally bar with the report's "{winner} beat {runner-up} in the
  jury vote." kept, the season timeline (below), player statistics with ranks among the cast
  (below), the four scores as Game Sense's faces and the career card the report already has
  (seasons, wins, best and median finish), relationships and legacy (closest ally = highest
  mutual score, biggest rival = lowest, betrayed = the deal they broke, lasting respect = the
  warmest juror standing), the player's diary reflection, signature moments (the locked
  three, else Game Sense's), and the analysts' line (Game Sense's verdict). No legacy score is
  invented: the career keeps none, and a single legacy number would be a new formula for the
  owner to decide. The diary reflection is the last entry of the persona history: the
  persona and its week, worded, beside the season's persona; the diary keeps no memory and no
  answer text. "Episode archive" and "Export share card" are out of scope (decision 8).
- **Placements.** Two readers disagree today and both miss. The report's own placement counts
  Evicted, which the engine never writes, so every juror's FINISHED card reads last place.
  `CareerLedger.Placement` reads the jury ledger's order, which is right, but merges
  production's removals by a join week read from the first sentiment event, which is always
  0, and the report's standings order jurors from the event log, which keeps 256 rows.
  Placements come from one function, `CareerLedger.Placement`, for every houseguest: the
  order the jury ledger filled, with production's removals merged by the week each juror
  left, read from their power row (evictee and week) or their `juror` standing row. The
  results grid, the report's FINISHED stat, its standings and the career all read it.
- **The timeline** is built from the ledger's power and competition rows, which keep every
  week, with portraits in place of thumbnails. A week's recap headline shows only where the
  event log still holds that week. The final week gets its own line: the three parts'
  winners (`finalPart1WinnerId`, `finalPart2WinnerId`, `hohId`) and the final eviction's
  power row, because the recap reads a Head of Household only from an HoH-phase competition
  and none opened for that week in play.
- **Statistics.** Competition wins, HoH wins, veto wins, alliances formed, betrayals,
  nominations survived; ties as T-1st. Competition wins count as F2's résumé counts them, a
  Part 1 or Part 2 win included, and the report's COMP WINS stat moves to the same count. The
  ledger's competition rows hold only the player's placement, so ranks among the cast read
  state. Nominations survived come from `timesNominated` and `nominationWeeks`, never the
  power rows (F4).
- **Tests.** JuryRevealPlayModeTests stays green on the HUD path, and CeremonyTruthTests' jury
  pacing is unchanged; a screen-path test as the vote reveal's; the screen path and the
  winner card each get a tie case; the winner card's ways on by caption. The results grid
  keeps what the report's suites pin, or moves each pin in the same commit: the road to the
  end, the `HOH WINS` and `COMP WINS` counts, the house table's sort and filter chips with the
  reader's place kept, each juror's reason with "voted for" and "Your ballot", the career
  card, Game Sense's four faces, and the four ways on, each once, on screen, outside the
  scroll. A statistics block with ranks adds labels those counts see, so it uses words of its
  own. EditMode on placements: a juror in a house of eight finishes by eviction order, not
  eighth; a removal between two evictions places both sides right; the report and the career
  agree; the fallback pin for a state with no jury ledger stays. PlayMode: a fixture with the
  player removed asserts no `Vote for {name} to win` and no `Cast your vote` row; a check at
  the Final 2 with the player on the jury asserts the spectator banner with no panel open and
  no unparented text left behind.
- **As built, F6a** (the player on the jury; no saved field). What differs from the text above:
  - *The ballot* is a screen of its own (`EpisodeDirector.JurorVote.cs`) for a juror as the
    engine counts one: THE JURY VOTES and the privacy line, the two finalists' cases
    (`FinalistRead.JurorCase`: the record, the player's standing with them, any Final 2
    agreement, the alliances the player knew, and what they did to the player), the two as
    ballot cards under the pinned `Vote for {name} to win`, *What matters to you* (the diary
    persona and the reflections recorded, decision 9 A), and `Review final speeches`.
  - *What they did to you* is the public record: the finalist's ceremonies against the player,
    the final eviction, the promises they broke to the player, and the deals the record shows
    they broke (`FinalistRead.DealBreaker`: the first ceremony or veto in the deal's term, the
    vote the player's own ballot kept, the final Head of Household's choice). A voting block
    that fell apart was both of theirs and is said so; a deal the player broke is never held
    against the finalist. Rivalries from grudges are not on the case: grudge rows are private.
  - *A player production removed* gets no ballot: a line that says so and Continue. Their
    objectives read Watch the Final 2, Watch the questions and Watch the jury vote, and the
    season walks key their vote on the same juror check.
  - *The spectator banner* draws only into an open panel; with none open, the objectives
    card's spectator line carries it. The seat line says the vote is cast once it is.
  - *Not built this round*: the hidden vote (F7), the reveal on the screen, the winner screen,
    the results grid, placements, the timeline and the statistics.
  - *Tests*: the juror's screen at both text sizes (the cases, the ballot cards, the privacy
    line, what matters, the speeches opened and put away), the vote from its card, the frame
    with no panel open; EditMode on the deals a finalist broke.

### F7 The cut scenes (world; after the cut-scenes plan's S1 to S4)

All on the authored living room and the exit door the cut-scenes plan settles (the west
wall, recommended; principle 6):

1. **Final 3 mode begins** (storyboard 34, frame 1): the `The Final Three` card on the living
   room's screen instead of the HUD when the house can stage it; the three seated on the U;
   the memory wall as is. The card plays as the window at three closes and Part 1 begins,
   after the trust talks, not before them. F7 either keeps it there and maps storyboard
   frame 1 to the start of Part 1, or opens the window with it, after the final-four
   eviction's card and the week's recap, and re-pins
   Finale_TheHouseDownToThreeOpensWithTheFinalThreeCard. Either way it still yields to a
   removal's card, as the StoryFallout test pins, and it needs the screen path the takeover
   lacks (item 6).
2. **Trust talks** (frame 2): the story session's, if they want a Final 3 scene; otherwise the
   free-time conversations on the U.
3. **The parts in the yard** (frames 3 to 6, storyboard 38): the per-part sign and the audience
   as F3 says; the yard screen and stations are the cut-scenes plan's S7.
4. **The crowning** (storyboard 31, frame 2): the card on the yard's screen when S7 lands, the
   HUD until then.
5. **The reflection** (frame 3): a camera beat on the HoH at the memory wall for six seconds
   before the decision screen opens, skippable; no walk.
6. **The decision ceremony** (frames 4 and 5): the final eviction already has its card: `The
   Final Two`, badged FINAL HOH, FINAL 2 and JURY, with its sting, the juror's `Evicted` and
   the room's heads turned. F7.6 stages that card on the living room's screen and adds the
   marks (the HoH at the mark beside the screen, the other two standing before them at two
   marks in front of the wingbacks), the chosen's seat in a wingback and the HoH's look at the
   third, not a new kind. Showing `FINALIST` is a badge change that re-pins
   Finale_TheFinalHoHsChoiceIsACardAndTheNewJurorStaysForIt. To stage it, the takeover gains
   a screen path as the vote reveal has, the ceremony code gains a staged branch for a kind
   with no reveal, CeremonyStageRoom gains the final eviction and the winner (the living
   room), and the finale-night gate narrows for the decision ceremony, the jury reveal and the
   finale, not for one stage. F6's reveal on the screen rides the same change.
7. **The exit to the jury** (frame 6): the third walks out through the exit door under a
   `TO THE JURY` sign variant, the house watching, the door shut and held, as the eviction's
   exit does; then their body joins the bench for the finale. The walk-out refuses finale
   night today, by design: the new juror joins the jury in the living room rather than
   walking out to come straight back in, and the walk-out's doc says the final eviction has
   none. F7.7 narrows that gate too, for the final eviction's juror only, beside the stage's
   gate. Finale_TheFinalHoHsChoiceIsACardAndTheNewJurorStaysForIt asserts no walk-out and the
   juror in the living room: F7 rewrites it for the exit and the return to the bench, and
   keeps a reduced-motion case with the straight move to the bench. The walk-out's doc
   changes with it.
8. **The finale** (storyboard 32): the jurors walk IN through the same door and take the U
   (the roster rule widened for finale night as it was for the evicted, bodies bound, the
   bench yielding to the stage, the gate narrowed as item 6 says); the U seats as many as it
   holds and the bench takes the rest (F4's count); the finalists in the wingbacks; the pleas
   with a talk loop; the questioning cuts (F5); the hidden vote at the crown box (F6); the
   reveal on the screen; the celebration. Seating the jury on the U overrides the cut-scenes
   plan's rule that finale night keeps the jury standing, and this plan says so.
   Jury_FinaleNightHasTheJuryInTheLivingRoom is re-pinned for the staged, seated jury and
   stays as the bench's test under reduced motion and batch runs. A player on the jury is
   never benched today, so F7.8 settles where the player's body goes: on the U with the jury,
   or at the station.
9. **Reduced motion and batch runs** see the cards; the audited walk stays on the cards.

### F8 Later, and decisions

- **A message to a juror** ("repair a jury relationship"): a new command appended after
  `CallTheVote`, legal only in the Social window at three under the finale rules, costing the
  seat. It needs a surface of its own, because a juror cannot be approached: the
  conversation card and the cast chip's menu open only for an active houseguest. It is sent
  from a juror's face on the objectives card's jury strip or in the jury house, as a small
  card with the juror and one action; the jury house then stops being observe-only for that
  one action. The command has its own handler and target rule, a juror of status Jury, as
  Eavesdrop has its own, because the Social handler refuses anyone who is not active. It
  moves the juror's score for the player, the term the vote reads (30 per cent of it), by a
  bounded amount once per juror, the way the story's View effect already moves a juror's
  score; the `juror` standing row stays frozen as they left. It writes its sentiment event,
  with the reason, through `WebJurySentiment.UpdateJurorSentiment`, which keeps the average
  the validator checks. It only appends: it never re-adds or reorders a juror, because the
  jurors' order is the eviction order the career's placement reads. The story session
  already sends a message to a new juror: the Goodbye Message on eviction night
  (`StoryCatalog.Staging.cs`), which moves that juror's view of the player by -8 to +6. F8's
  message is a second and later one, to a juror already on the jury. A decision (6).
- **Opening statements** before questioning: a second speech record per finalist, schema 21;
  the storyboard's frame 2. A decision (7); without it the pleas are the statements and the
  finalists' persona lines open the finale.
- **New Final HOH games** (rules version 5): decision 3.
- **The share card and the episode archive**: decision 8.

## 3. Engine changes, all under schema 21 and `finaleRulesStartWeek`

| Change | Fields | Effect | Old saves |
|---|---|---|---|
| The final argument | `finalArgument.theme`, `finalArgument.momentRefs` (3), a record of its own; the lock, a command kind | a named, capped term in the jury score for jurors whose traits match the theme, no draw; the responses' order at render | none |
| History questions | `juryExchanges[].category`, `receiptKind`, `receiptId`, `responseKey`; five responses | the sign of today's ±10 by fit; two draws per question and two per answer, as now | the catalogue A/B as today |
| A message to a juror (F8) | a command kind; no field | the juror's score for the player, bounded, once | refused |
| Opening statements (F8) | `openingStatements[]` | none on the vote | none |

The rules week is switched on by an `EpisodeEngine.EnableFinale` beside `EnableWeek` and
`EnableAgency`. The director's fresh season calls it on both paths, the quick start and the
built cast, and the web importer calls it from the week after the import, as it does the
others. Seasons that tests build directly stay off unless they enable it, so today's finale
pins keep their fixtures. The migration adds it as 0. Only the live finale validator
(`EpisodeFinaleValidation`) branches on it; the frozen validators stay as they are. Schema 21
adds a `FrozenEpisodeV20` of today's shape, which `UpgradeV20ToV21` checks before it adds the
fields, and `PrepareCurrentPayload`'s current version moves to 21. Every load pin that asserts
20 moves to 21 in the same commit (24 across 13 EditMode files).
LegacyJuryAndFinished_LoadWithoutReopeningQuestioningOrRewritingAwards also asserts a rules
week of 0 and no final argument. The seeded draw counts per question and per answer are
pinned and kept. The story session is asked for schema 21 before the first of these lands,
and told what the jury effect reads of theirs (nothing: the story term stays theirs and
untouched).

## 4. Build order

| | Slice | Done when |
|---|---|---|
| F1 | The Final 3 frame: the strip, the objectives card with the jury strip, the folded rail, the two-row feed and the ENDGAME PREPARATION head on the window at three (built; the trait row deferred, see F1) | PlayMode in the window at three, at Part 1 and at two asserts the words, the marks and the card's foot; the overlap and compact suites green |
| F2 | The comparison and the decision screen, gated; with them F1's context card at three and the Diary Room's study copy (built; see F2's note) | The gate's EditMode tests; the decision by caption; the window-at-three fixture asserts the context card's words |
| F3 | The Final HOH: the bracket card, the crowning card, the yard's signs, the eyebrows (built; see F3's note) | Both card kinds in a fixture at three; the eyebrow and heading pinned |
| F4 | Preparation, the lock and the jury house; schema 21 with the argument (F4a built: the jury house; see F4's note) | Bands and the effect under both rules; migrations green, the load pins at 21 |
| F5 | Questioning from history with receipts and five responses; the live layout; the audited walk through the new captions (F5a built: the live layout; see F5's note) | Receipts, categories, the sign by fit, draw counts pinned |
| F6 | The juror's screen, the hidden vote card, the reveal on the screen, the winner screen, the results grid, one placement function (F6a built: the juror's screen; see F6's note) | The reveal's tests on both paths, a tie on each; the ways on by caption; the report's pins kept or moved |
| F7 | The cut scenes on the authored set and the door | Captures of the ceremony, the exit, the walk-in, the reveal; the audited pipeline |
| F8 | The message to a juror, opening statements, new games, the share card | As decided |

F1 to F3 need no schema and no set; F4 and F5 need schema 21. F6's screen reveal needs no
authored screen: it plays on today's living-room screen, as the vote reveal does, with the
finalists in the two hot seats, once the finale-night gate narrows and the stage room gains
the winner (the change F7.6 describes, which F6 rides). It moves to the wingbacks and the
authored screen when the cut-scenes plan's S3 lands, and F6 does not wait for S1 to S4 or F7.
F7 needs S1 to S4 of the cut-scenes plan. Each lands as before: the offline compile, the
Unity-free subset, the filtered suites on the D: copy, the audited pipeline alone from the
main checkout, a PR. The Unity-free subset compiles EpisodeFinaleTests and JuryVotingTests, so
F5's mutation loops can use it. WebJuryQuestioningParityTests (the web catalogue F5 keeps for
old saves), CareerLedgerTests (F6's placements) and the persistence migration tests (F4's
schema 21) are not in it, so F4, F5 and F6 run the EditMode suite on the D: copy before they
land. Effort: F1 2 days, F2 3, F3 2, F4 4, F5 6, F6 5, F7 5; about four weeks before F8.

## 5. Decisions for the owner

1. **Days.** (A, recommended) weeks on every strip, as the simulation keeps them. (B) a
   derived day (seven a week, never saved) on the strips only.
2. **"Take Alex" wording.** (A, recommended) the pinned `Evict {name}` stays on the button
   under a "Take {other} to the Final 2" headline. (B) rename the caption and every pin.
3. **Final HOH games.** (A, recommended) dress the three authored games with the bracket and
   the crowning now. (B) a rules version 5 that deals Roll the Dice, rules 4's press-your-luck
   game that already ships, to a final part, and adds one new game: the head-to-head buzzers,
   or the judged shortlist's Jury Questions for Part 3 (MINIGAME-IDEAS §1, item 5). The engine
   fixes each part's category before any rules lookup and the NPCs' scores follow it, so a
   luck part changes the category rule and every final-part score in new seasons. Old seasons
   keep the three authored games.
4. **The final argument's reach.** (A, recommended) the theme and moments are saved and give
   jurors whose traits match the theme a small capped term in the jury score. (B) the
   argument only writes the speech.
5. **The jury read's source.** The jury model's raw leaning stays off every screen; the
   eviction model's lean is already shown through VoteRead's filter. (A, recommended) facts
   the player could know (the last read, deals, promises, replies, public grudges), with
   Unknown where nothing is known. (B) VoteRead's filter on the jury score: the relationship,
   respect, loyalty and obligation terms the player has learned, the rest counted unknown, in
   VoteRead's words. (C) the model's own leanings, softened into words. Whichever wins, F4's
   band called Leaning takes another word, because Leaning already names VoteRead's lean on
   the notebook's vote page.
6. **A message to a juror.** The story session's Goodbye Message already moves a new juror's
   view of the player on eviction night, so this decision first asks the story session
   whether the two stack, and sizes F8's move on the same scale. (A, recommended) F8, after
   the screens. (B) in F1, as a command.
7. **Opening statements.** (A, recommended) none; the pleas are the statements. (B) a second
   speech record.
8. **The share card and the episode archive.** (A, recommended) out of scope. (B) a share card
   as an exported PNG of the winner screen. It would be the game's first screen capture and
   file write outside the port verification: it captures once in a frame, after that frame's
   portrait blits, with the active render target restored, and writes somewhere other than
   the save slot.
9. **The player as a juror's "What matters to you".** (A, recommended) the diary persona,
   read-only. (B) sliders that do nothing. (C) omitted.
10. **Watch season recap.** `Review the season` opens the notebook on your notes today, from
    the finished panel and the report, and FinaleExits, SeasonReportNavigation and the
    audited walk's `finale-notebook` capture pin it. (A, recommended) `Watch season recap` is
    a control of its own with its own caption, beside the pinned ways on. It opens week 1's
    recap by a path that skips the season-ending gate and steps forward a week at a time. The
    recap screen gains a next-week control, since a review today shows only Back, and a week
    the log has lost shows the ledger's power line. `Review the season` keeps the notebook,
    whose week rows already open each recap. (B) the recaps behind `Review the season`,
    re-pinning those two suites and the walk. (C) omitted: the notebook's week rows are the
    recap. F6's winner screen follows the choice.

12\. **The Diary Room's jury number.** One jury number is already on a screen: the Diary
    Room's Recorded jury impression and its record line, the sentiment ledger's mean, seeded
    from the player's own view of each juror. F4 settles it with the jury house. (A,
    recommended) retire the number and point the row at the jury house, re-pinning
    `EpisodePlayModeTests.DiaryRecord`'s row, because it would disagree with the bands. (B)
    keep it as today, named as the whole jury's mood beside the bands.

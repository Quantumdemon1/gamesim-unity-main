# The strategy loop: reading the vote, moving it, the week's windows, and the season verdict

Status: **decided** (2026-09-27; the owner took every recommendation in §9). This is the game-flow side of the owner's
direction: "it's hard to interact and strategize with the NPCs, it's hard to figure out which way the
vote is leaning, and it's hard to influence votes one way or the other. I'm not sure having one phase
for strategizing and interactions is correct. [...] This should culminate at the end of the game where
the AI can evaluate how they navigated the game and opportunities, and give them a score for how well
they were able to compete comp wise, strategically and socially." The story side is
`StoryArcs/30-plays-and-threads.md` (decided 2026-09-27); §6 and §8 here are the contracts between the two.

## 0. TL;DR

The vote is decided by numbers the player never sees and cannot reach. An NPC's ballot is their view of
the nominee (up to ±27), a shared alliance (about +10), bloc pressure (−40) and a threat read (to −23).
Every lever the player is offered moves about 4 points, under the evaluator's own 10-point "leaning"
threshold; a promise made to a voter is not read at all; a vote deal an NPC breaks is never judged. The
whole week's strategy shares one pool of four actions, spent mostly at the one time nothing is at stake.
Nothing grades a season.

Four changes, each measured:

- **The Read.** A leaning readout per voter, built from the vote model's own terms and filtered through
  what the player has learned. Information becomes the resource: ask, overhear, be told by an ally, read
  a person. NPCs can lie, and a lie is a receipt later.
- **The Levers.** A promise or deal with a voter enters their ballot, scaled by how they see you; an
  alliance the player leads can be steered; a plea to a voter exists; every lever prints what it moved.
  Broken vote deals are judged, both ways.
- **The Week.** Four windows with their own small budgets, sitting between the story's anchors, instead
  of one pool: after the HoH, after the nominations, after the veto, and the free time after the
  eviction. NPC campaigning is spread across them, where the player can see and intercept it.
- **The Verdict.** A deterministic season grade from a durable ledger of opportunities offered, taken
  and won, with three faces (competitions, strategy, social), proven to separate a reader from a random
  player. Working name: **Game Sense**. Not all wins are equal because the ledger says what the winner
  actually did.

## 1. What is there today, measured

From four read-only maps of the code (2026-09-27), file:line in the maps' reports.

**The vote.** All NPC ballots are computed at one instant, two Advances after "Close campaigning"
(`EpisodeEngine.cs:343-351`). Weighted terms (`WebEvictionVoting.cs:233-254,400-421`): the voter's
score toward the nominee ×0.27 (±27); threat ×0.23 (0 to −23); a shared alliance about +9 to +11;
deals and promises between voter and nominee, clamped and ×0.09 (about ±4.5). Unweighted: personality
±20, memory ±25, bloc pressure −40 (`WebVotingBlocs.cs:76-128`), story grudge to −25 and bond ±25-30.
The score every NPC decides with is *their* view of the player, rolled at 0.8-1.2× of the player's own
(`EpisodeEngine.cs:1922-1923`), and it is never shown; every number on screen is the player's view.

**The levers.** Vote deals move about ±3.2 (`EpisodeEngine.cs:1737-1764`) and are never judged kept or
broken (`DealResolution.cs:22-25`). Promises the player makes to a voter are not read by the evaluator,
which reads only voter-nominee promises. Lying nets about −1 because the memory it writes gives the
target +2 (`:1168-1172`). A call-out hits the first two or three houseguests by id (`:1618-1625`). The
lobby has no eviction ask (`StrategyRules.cs:96-107`). "Listen in" overhears a random pair, not the one
on screen (`EpisodeHud.Chrome.cs:562-566`). Nothing reads the speech from the block. Nominees' vote
promises never settle (`:379`). `boughtActionPoints` never resets, so one purchase raises every later
week's budget (`:616,1718`).

**The read.** The evaluator computes `margin`, `confidence`, per-nominee evaluations and private reason
codes (`WebEvictionVoting.cs:199-206`); only tests read them. The Votes page shows results and revealed
ballots; during a vote, only the player's own (`VoteRecords.cs:80-129`). Stated reasons are filtered to
threat or history, so a bloc- or grudge-driven vote reads "the bigger threat" (`:430-433`). NPCs never
say how they intend to vote (`HouseDialogue.cs:5-9`).

**The week.** Social → HoH → Nomination → VetoSelection → Veto → VetoMeeting → Campaign → Eviction →
Social. Free roam only in Social and Campaign, ended only by Advance. One action pool,
ceil(active/2) + bought + story − Have-Not, resets once a week (`EpisodeEngine.cs:200,609-623`): four
actions in the default eight-seat house, shared by the HoH window, the veto window, the campaign and
the free time. No interaction in HoH, VetoSelection, Veto, Eviction, or Nomination when the player is
HoH: a player HoH hears no pitches. NPC agency lands in two instants: three turns each at
Eviction→Social (`NpcSocialActions.cs:137-165`) and one campaign pitch each at VetoMeeting→Campaign
(`:176-205`). Deal offers (three a week) are visible only inside that person's conversation
(`NpcDeals.cs:221-268`). The dial's seven petals are all warmth rolls (`WebSocialVocabulary.cs:26-62`);
about forty rows sit under it.

**The story.** Wired in and consequential (grudges, bonds, deals and the backdoor move nominations, the
veto, votes and the jury), but about four choices reach the player a season, nothing says what a choice
moved, and `SocialBonus` (63 options) writes a field nothing reads. The story side's plan covers this;
two items land here: a vote a story term swung is never explained because those terms are "private" in
the reveal (`WebEvictionVoting.cs:425-433`), and the one beat that tells the player they are in the HoH's
bottom two (`hoh-room`) never fires in a strategy-window season.

**The finale.** Nothing grades the player. The jury scores relationship 0.3, gameplay respect 0.4,
alliance 0.15, obligations 0.15, a story term and a ±10 roll (`WebJuryVoting.cs:128-141`). The project
makes no language-model or network calls by rule (`EpisodeEngine.cs:1316`), so "the AI evaluates" is the
game's own referee: deterministic rules over recorded facts. The record is thin: the event log is capped
at 256 entries (`EpisodeValidation.cs:79`), ballots are cleared weekly, competition placements and
throws survive only as log lines, and relationships keep no history but the arcs' weekly one.

## 2. The Read

**Principle.** The player sees the vote model's own terms, never a parallel estimate, and sees them only
as far as they know them. A read is as good as the intel behind it, so gathering intel is play.

**The leaning readout.** On the Votes page and the campaign's voter grid, one line per eligible voter:
*Leaning: evict Jo · firm* / *leaning* / *torn* / *unknown*. It is the evaluator run on the current
state (a projection; the real ballot is still cast at the vote), with the terms the player does not know
removed:

| Term | Known when |
|---|---|
| Their view of you (the hidden 0.8-1.2× score) | You have read them (below), or they told you in a conversation band; goes stale after two weeks |
| Their view of the other nominee | Read, or witnessed (a story receipt, an overheard line) |
| A shared alliance | The alliance is known to you (`Knowledge`; the story's Intel payoffs) |
| A deal or promise they hold | You are party to it, were told, or overheard it |
| Bloc pressure | You know the alliance and who leads it |
| Grudge or bond | You caused it, witnessed it, or were told |
| Threat, personality, memory | Never shown as numbers; they are the "unknowns" chip |

A readout with terms missing carries a chip: *2 things you don't know could change this*. Exact margins
("by 9") unlock for a voter whose view of you and whose alliances you know; otherwise words only. This
mirrors the story side's D3.

**Whip count.** Above the lines: *Evict Jo 3 · Evict Sam 2 · Unknown 1 · you need 1 more*, from known
leanings only.

**Ways to learn.**
- **Ask them straight** (a conversation row, one per voter per window): "Where's your head at on the
  vote?" They answer with their true leaning at odds from their view of you and their traits (Loyal and
  Honest tell the truth; Sneaky lies at the web's deception rate; Strategic deflects). The answer is a
  *claim* on the Votes page with its source and week: *Riley told you: evicting Jo*. A claim
  contradicted by the ballot becomes a receipt at the reveal ("Riley lied to you about the vote"), which
  is trust, a grudge and jury material; a claim kept is a bond.
- **Read them** (the addendum's G3 option, trait-gated Intuitive or Analytical, one per person per
  week): on success, their view of you becomes known, and their agenda if they have one.
- **Listen in** overhears the pair on screen (bug fix) and, when they discuss the vote, yields their
  stated leanings as overheard claims.
- **An ally reports**: a voter in an alliance with the player shares what they have heard at the
  alliance meeting (the existing room act), one claim per meeting.
- **The reveal explains itself**: story and alliance terms count as player-known when the player
  caused, witnessed or was told them, so the revealed reason can say "I haven't forgotten what you
  did" or "Taylor is in my alliance". A bloc's plan is never spoken, known or not: coordination is
  the bloc's business and a voter who followed it gives another reason (the bloc tests' contract).
  For the player an alliance is known when they are in it or hold its fact (`VoteRead.AllianceKnown`);
  the evaluators' legacy rule (no fact, everyone sees it) is the voters', not the player's.

## 3. The Levers

Every lever prints what it moved, in the model's terms: *Riley: torn → leaning evict Jo (+9, your deal)*.

- **A promise or deal with a voter enters their ballot.** New obligation term for the voter toward the
  player's ask, base 8, scaled by their view of the player (0 at a view ≤ 0, full at ≥ 50), Loyal ×1.5,
  Sneaky ×0. A friend keeps their word; a stranger does not. Sized so a deal plus standing flips a
  *torn* voter and never a *firm* one: relationship ±27 and alliance +10 still dominate, which is the
  point. Tuned by the harness (§7), not by hand.
- **Vote deals are judged.** At the reveal, a `vote_save` or `vote_evict` deal is kept or broken.
  Broken by the NPC: a receipt, trust, a grudge the player holds, and jury material. Broken by the
  player: the same against them, and the story's `Reckoning`. Nominees' vote promises settle the same
  way (today they never do).
- **Steering an alliance.** The bloc's shot-caller is today the member with the highest outbound trust.
  At an alliance meeting the player can *call the target*; if their standing with the members is above
  the compliance bar, the directive is theirs and the −40 lands where they point it; below it, members
  defect, visibly, and the player learns who. This turns the biggest hidden term into a lever the
  player can hold, and lose.
- **A plea to a voter** when the player is on the block, with the four approaches and odds the HoH
  lobby already has (`StrategyRules.cs:158-273`), one per voter per window, moving the voter's view of
  the player. The speech from the block moves every voter a little by its approach.
- **Fixes on the way:** the lying memory keyword; the call-out's id ordering; `boughtActionPoints`
  resetting weekly; the bloc's empty grudge, founder and stability inputs; the lobby gains an eviction
  ask ("put X up" already exists; "keep X off" and "use it on me" complete it).
- **Web parity** holds as decision 3 of the story plan did: native terms are labelled in code and
  reproduce today's output on an empty store, so the parity fixtures stay.
- **As built (R2, 2026-09-28).** The obligation is a `playerKnown` term (`obligation`, base 8, view-
  scaled, Loyal x1.5, Sneaky x0) on top of the web's own deal term, which a vote deal's target now
  reaches; vote deals are judged at the reveal (`DealResolution.VoteDeal`); the plea to a voter is
  the lobby's fifth ask (`LobbyAsk.Vote`) and its answer a `plea` term (influence x 0.2, so a hostile
  hearing counts against you); an all-in plea that lands writes the voter's `vote_save` naming the
  player; the call is `EpisodeCommandKind.CallTheVote`, decided at call time by the round's own
  `Loyalty`/`Complies` and honoured by the round; the bloc's stability and grudges are filled under
  the levers; "keep X off" and the call-out shuffle landed. Everything is keyed to
  `leverRulesStartWeek`. Deferred: the speech from the block moving every voter by its approach
  (the speech has no approach yet), and NPC pact formation, which is the owner's call (§9).

## 4. The Week

Four windows, each with its own budget, free roam, and the actions that make sense in it. They sit
between the story's anchors, which keep their meaning and order.

| Window | From → to (anchors) | What it is for | Actions (eight seats) |
|---|---|---|---|
| **After the HoH** | HohCrowned → NomsSet | Lobby the HoH; pitches to a player HoH (NPC agendas as walk-ins); the HoH-room invite; a plan with allies | 2 |
| **After the nominations** | NomsSet → VetoWon | Veto talk; comfort or plan with a nominee; the backdoor plan; "who's playing" | 1 |
| **After the veto** | BlockSet → EvictionEve | The campaign: pleas, deals, reads, whip; reply cards | 2 |
| **After the eviction** | EvictionNight → HohCrowned | Bonds, intel, alliances, the story's own beats | 1-2 |

- **Budgets** scale with the house as today's pool does (total ≈ ceil(active/2) + 2), but each
  window's is its own, so the campaign is never spent in the free time. Unspent actions do not carry.
- **A player HoH hears the house.** After the HoH, NPCs come to a player HoH with their agendas
  (assess-threat, coordinate, campaign-subtly), the way the addendum's G3 and G5 describe, as walk-ins
  the player can take or wave off. Today the player HoH goes straight to the nomination screen.
- **NPC agency is spread**, not resolved in two instants: nominees campaign in the third window, allies
  coordinate in the first and third, gossip travels in the fourth. The player can overhear, intercept
  or be told.
- **Offers and cards are visible where the player is.** Deal offers, reply cards and open windows get a
  badge on the cast strip and a line in the objective chip ("Nominations tonight · 1 action · Riley
  wants a word"), instead of appearing only inside the right panel.
- **The dial** keeps its warmth petals, and its rows are grouped by intent (build, read, ask, deal,
  risk) so forty rows read as five choices. No row is removed.
- **What stays.** The ceremonies, the anchors, the story's asks-a-week cap, the windows' reluctance
  and willingness model, and Advance ending a window. A save from before carries one window's worth
  of budget into the current phase.

The engine change is in `Advance` and the budget; the story's anchors fire where they fire today, and
the story side reviews the sequence before it lands.

**As built (R5, 2026-09-28; reviewed by the story side).** `EpisodeEngine.Window(state)` maps the
phases: Nomination → after the HoH (2 seats); VetoSelection, Veto and VetoMeeting → after the
nominations (1); Campaign → after the veto (2); Social → after the eviction (ceil(active/2) + 2 − 5,
floored at 1); competitions, ceremonies and eviction night → none. Four counters
(`EpisodeState.windowActions`, schema 19) reset at the week turn; `weekRulesStartWeek` is the
boundary. The extras (bought time, `Storylines.SocialActions`, less a Have-Not's conversation) are one
weekly pool spent after a window's own seats by whichever window overspends first; a negative pool
comes off the window after the HoH. `SocialActionBudget`/`SocialActionsSpent` keep their signatures
and return the open window's figures. Free roam and every conversation said to somebody are open in
all four windows; `IsWindowConversation` still keeps listening in, rumours and scheming for the free
time. Deferred to R5b: walk-ins for a player HoH, badges and the objective chip's line, the dial's
grouping.

### 4.5 As built: free time as a screen, and the notebook's own page (2026-09-28)

The owner's playtest: the free-time screen ("Make your next move") was too cluttered and wordy,
and "Notebook [J]" opened the relationships, which the rail already had a row for.

- **Free time** (`EpisodeDirector.FreeTimeScreen.cs`) is drawn as the campaign is: the location and
  the meter, one line of the window's rule under it, then the house as cards (`EpisodeHud.HouseCards`:
  those in the room with you first, the week's role on the photo, where you stand, the latest thing
  you have on them, and "Talk to X" walks over), then the moves that name nobody as tiles
  (`EpisodeHud.MoveTiles`, the house event's tiles generalised): the two meetings, listening in and
  the two ways of buying time, each saying in a line what it does and costs, its category in the
  corner. The paragraphs are gone; every rule they stated is on the tile it belongs to. The
  campaign's "More ways to campaign" shows the same tiles.
- **The notebook** opens on its own page, **Your notes** (`EpisodeDirector.Notes.cs`,
  `HouseguestNotes` in the simulation): a card a houseguest with what your character has on them
  from the season's own records, newest first: their word (promises, offers, your calls, shared
  pacts), what they told you about the vote and whether it held, what you read of them (with what
  they are up to, from this week's read), what they put to you and how you answered, and what you
  remember. Filters: everyone, in the house, their word, the vote, your reads. Nothing you have not
  learned. The rail's rows are unchanged; "Relationships" is still the web.

## 5. The Verdict

**Name.** The owner asked for a game-IQ score under another name. Recommended: **Game Sense**, scored
0-100 with three faces. Alternatives: *The Read*, *Season Grade*, *Houseguest Rating*. "Moves"
collides with the dance moves.

**Who evaluates.** The game's own referee, deterministic and seeded: no language model, by the
project's rule. Every point traces to a ledger row the player can open.

**The three faces.**

| Face | What earns it | What loses it |
|---|---|---|
| **Competitions** | Wins against the stat-expected odds the engine already computes; clutch wins (on the block, the final HoH); a throw that served a purpose (not nominated after it) | Losing when nominated with a full effort; a throw that put you on the block |
| **Strategy** | Nominations avoided relative to your threat rank; the veto used well (self or ally saved, a target replaced in); a backdoor made or dodged; **read accuracy** (your whip count against the ballots); deals and promises kept; votes with the majority when it mattered; never blindsided | Blindsided; a broken deal; a whip count that was wrong; power held and wasted |
| **Social** | Their view of you (not yours of them) across the house; each juror's view when they left; alliances that held; bonds; grudges resolved | Grudges left standing; an alliance that turned on you; a lie caught |

**Opportunities.** The story side's plays and this side's windows both write rows: offered, taken or
not, and what came of it. The Strategy face weighs outcomes *against what was offered*, so a quiet
winner who took nothing scores below a loser who took and won most of their chances. That is the
"not all wins are equal" the owner asked for.

**On screen.** A GAME SENSE section on the season report after YOUR JOURNEY: the number, the three
faces, the three moments that made the difference and the three chances missed, each a ledger row in
words. One line on the Finished panel. The career ledger gains the number (its file needs its own
migration, a second step).

## 6. The ledger and schema 17

One bump, bundled with the story side's play state if it needs a field. Proposed shape, `season.ledger`:

```
Opportunity { id, week, anchor, kind: play|lobby|plea|deal|alliance|vote|comp|read,
              source, offeredAt, response: taken|declined|ignored|expired,
              steps: [ { at, choice, matchedRead } ], outcome: won|part|lost|na,
              payoff: { currency: trust|intel|alliance|power, amount }, note }
Competition { week, kind, field, placement, performance, entry: played|assist|simulated|thrown, expectedWin }
Power       { week, hohId, vetoHolderId, vetoUsed, savedId, replacementId, nominees, evicteeId, tally, backdoorTarget, backdoorResult }
Ballot      { week, voterId, target, readBefore, correct }          // the player's ballots and read accuracy
Claim       { week, voterId, statedLeaning, source: told|overheard|ally|read, truthful }  // resolved at the reveal
AllianceRow { id, startedWeek, endedWeek, why }
Standing    { week, npcId, theirViewOfYou }                         // a snapshot at each eviction, and for each juror as they leave
```

Bounded: rows per kind capped like the event log, oldest dropped with a count kept. Exact-shape
validation as every saved field has. `FrozenEpisodeV16` and `UpgradeV16ToV17` follow the V15→V16
pattern; the importer defaults everything empty; `StripSchema17` and the migration tests follow.

## 7. Proving skill matters

Shared with the story side. The Unity-free harness (`Tools/SimulationTests`) gains two scripted players
over the 80-season sweep: a **reader**, who gathers reads and acts on them, and a **random** player.
Game Sense must separate them clearly (a 20-point gap is the working target), winners must spread
widely (a winning random player scores low), and the reader's win rate must not run away (the game is
not solved by reading). Every lever's weight is tuned against this, not by hand.

## 8. Contracts with the story side

- **Observable steps** (story D6): a lobby resolved (decider, ask, subject, response), a plea
  answered, a reply card answered, a vote cast, a read made, all as typed events the story's plays can
  listen for at the anchors.
- **Anchors** unchanged: HohCrowned, NomsSet, VetoWon, BlockSet, EvictionNight, SocialClose. Windows
  sit between them.
- **Vote display** counts an alliance or secret the moment the story makes it known to the player.
- **The notebook's Story section and the Pull** are the story side's; the Votes page, the campaign
  grid, the cast strip badges and the objective chip are this side's.

## 9. Decisions for the owner

Taken 2026-09-27, each as recommended.

| # | Decision | Decided |
|---|---|---|
| E1 | The week: four windows with their own budgets, or keep one pool and only add visibility | Four windows |
| E2 | The read: filtered by what you know, and NPCs may lie when asked; or an always-true whip count | Filtered, and they may lie |
| E3 | Lever strength: a deal plus standing flips a torn voter but never a firm one; or stronger | Torn only (tuned by the harness) |
| E4 | The name | Game Sense |
| E5 | Order of work | The Read and the lever fixes first; then the ledger and the Verdict; then the Week. The schema bump lands with the Read, because claims and standings must survive a reload; the story side keeps its play state off the schema (its P1) |

## 10. Milestones

| | Milestone | Done when |
|---|---|---|
| R0 | Measure: read accuracy and lever effect sizes in the sweep; a random player's Game Sense | Baseline recorded |
| R1 | The Read: leaning readout, whip count, claims (ask, overhear, ally, read), the reveal explaining itself | A reader's whip count beats random's in the sweep |
| R2 | The Levers: voter obligations, judged vote deals, alliance steering, the plea to a voter, the fixes | A deal flips a torn voter and never a firm one, in tests |
| R3 | Schema 17: the ledger, claims and standings; the story side's play state bundled | Migration tests green; the sweep writes every row kind |
| R4 | The Verdict: the evaluator, the report section, the Finished line | Reader beats random by the target gap; winners spread |
| R5 | The Week: four windows, spread NPC agency, a player HoH who hears the house, visible offers | Full suites and the standalone season walk green; the story side has reviewed the sequence |

Each milestone runs the full suites, and R2 onward runs the standalone season walk.

# 30 · Plays and threads: the story as gameplay

Status: **decided** (2026-09-27): the owner took all seven recommendations in §8. This covers the story
side of the owner's direction. The other session owns vote intelligence and influence, the week's phases and the
end-of-season evaluation; §7 lists what this side hands it.

## 0. TL;DR

The story system is wired into the simulation. Its choices already move eviction and jury votes,
nominations, the veto, alliances, deals, leverage and competition bonuses. It does not *play* like it is:

- about four story choices reach the player in a season;
- nothing says what a choice achieved;
- nothing says what to do next;
- the long arcs almost never fire.

The proposal turns arcs into **plays**, mini-quests with a stated goal, a deadline, a payoff and a way to
fail, and strings them into **threads** that run through the season. A player who reads people and
situations well wins more of them, and the season's record of plays taken and won feeds the
evaluation.

## 1. What is there today, measured

- **Wired in.**
  - Eviction votes weigh story grudges and bonds (`VoteGrudge`, `VoteBond`), and jury votes add a
    story term with reasons (`JuryStory`).
  - Nominations, replacements and the veto add the story's preferences to the strategy windows'
    terms (`NominationPreference`, `SavePreference`, `WillNotSave`).
  - An NPC counts an alliance in its vote and its threat read only if it knows about it (`Knowledge`).
  - Choices can:
    - form and end alliances, make deals and promises;
    - give the player hooks (leverage) or competition and social bonuses;
    - cause Have-Not weeks and production strikes.
- **Sparse.** An 80-season sweep of new seasons (strategy windows on), with a random player:
  - 350 story choices answered in total, about 4 a season;
  - 24 of the 65 arcs never fired, including every long one: `late-nights` (7 beats), `staged-feud`
    (4) and `on-notice` (3).

  The original plan meant it to be sparse, targeting 4-6 asks a season ("tease, don't nag"). The
  owner's direction reverses that.
- **Short.** 43 of the 51 arcs the source scan could read are a single beat. Endings are almost all
  `done`, so an arc never records success or failure.
- **Invisible.**
  - A choice's result is a line of outcome text; the vote, trust or leverage it moved is never named.
  - The notebook's Story section is a chronological log of the last three weeks. The STORYLINES block
    lists only running arcs as "waiting on you" or "still playing out".
  - The planned stakes chips, show-the-odds toggle and receipt lines (plan 20 §5.3) were not built.
- **Crowded out.** Where the strategy windows play, their reply cards own confrontation, gossip and a
  nominee's plea, and their lobby owns the Head of Household's ear. So those arcs never reach the
  player (`confronted`, `campaign-pitch`, `caught-talking`, `after-the-comp`, `hoh-room`,
  `veto-dilemma`).

## 2. Plays

A **play** is an arc with a goal the player can win or lose. It is built from the existing catalogue,
cast and beats, and keeps the plan-20 rules: no timers, anchors as deadlines, seeded and keyed rolls.

| Part | What it is | Example ("Who's in the Pact?") |
|---|---|---|
| Goal | One sentence, checked against engine state | Find out who is in the alliance Riley keeps hinting at |
| Payoff | One of four currencies: **Trust**, **Intel**, **Alliance**, **Power** | Intel: the Pact's members become known to you |
| Deadline | An anchor | Before eviction night |
| Steps | Beats, **or** ordinary actions that count toward the goal | Talk to two suspected members; eavesdrop; trade a secret |
| Read | What decides success: the person's traits, lore you have learned, the relationship | A Loyal member will not talk; a Gossip will, for a secret |
| Outcomes | Won, partly won or lost, each with its consequence | Lost: Riley learns you were asking, and the Pact tightens against you |

**How the currencies pay out through systems that already exist:**

| Currency | Pays out as | Where it matters |
|---|---|---|
| Trust | Relationship, receipts, a promise or their word | Nominations, the veto, votes, the jury |
| Intel | An alliance or secret becomes known to you, or a lore facet is revealed | The vote display (other session), your threat read, better odds on later plays |
| Alliance | An alliance formed or strengthened, a bloc you can steer | Votes, nominations |
| Power | A hook (leverage), a deal, safety, a competition edge, a Have-Not pass | The week's decisions |

**Steps can be ordinary play.** A play listens for engine events as well as its own beats, so it
sets a purpose for things the player already does:
- a conversation, a room act or an eavesdrop;
- the windows' lobby or a reply card;
- a vote.

"Get the Head of Household to put Jo up" completes when the lobby works and Jo is nominated. The
story no longer duplicates the windows; it gives them a reason to be used (decision D6).

**Skill means reading people.** Success turns on things the player can learn: traits, lore facets,
standing, and who knows what. Information gathered in one play improves the odds of the next, so
Intel has lasting value. How visible the odds are is decision D3.

**Examples built from existing arcs and systems:**

| Play | Currency | Built on |
|---|---|---|
| Who's in the Pact? | Intel | `Knowledge`, `LeakAlliance`, eavesdrop |
| Stay off the block | Power | the HoH's ranking (`InBottomTwo`), the lobby, deals |
| Their word | Trust | `Promise`, `TheirWord`, room acts |
| Call in the favour | Power | `call-in-the-favour`, hooks |
| Build the numbers | Alliance | `Alliance`, the vote model (other session) |
| The backdoor | Power | `the-backdoor` |
| Settle it | Trust / jury | `staged-feud`, grudges, reckonings |
| Late nights | Trust / Alliance | `late-nights` (7 beats) |
| Clean weeks | Power | production strikes, `on-notice` |

## 3. Threads

A **thread** is a season-long narrative the player is inside. Its chapters are plays, and how each
chapter ends picks the next. Every season seeds two or three threads from its cast at the first
eviction night:

- **A bond:** a friend who could become a ride-or-die or a showmance. (Built from `late-nights`,
  ride-or-die and the bond effects.)
- **A rival:** someone whose grudge escalates. It ends in reconciliation or a showdown, and that
  ending reaches the jury.
- **A power line:** an alliance to build, hold together, or betray at the right moment, down to the
  final four.
- **Production:** only if the player earns it, through conduct.

Chapters span weeks, and threads cross: the rival can be in the Pact. Each thread ends with a climax
at a fixed point: the final four for the power line, the jury for the rival and the bond. The recap's
"Previously on / Next time on" already reads storylines; it will read threads (decision D5).

## 4. The player sees it

- **The play card.** The Pull becomes an offer card showing the goal, the currency, the deadline and
  the stake, with **Take it on** and **Not now**. A declined play can come back once.
- **The plays page.** The notebook's Story section opens with the active plays: goal, deadline,
  progress, what you have learned and what is at stake. The threads follow, then the log.
- **The receipt.** Winning or losing a play shows what changed, in words:
  - "You learned: Riley, Jo and Sam are the Pact."
  - "Quinn owes you a favour."
  - "Jo will not nominate you this week."

  Each line comes from the effect that ran, never a paraphrase.
- **The tracked play** has a chip on the HUD and a beacon on the room where its next step is.
- **Before you advance,** the existing warning line names the plays you would let expire.

## 5. Density and pacing

- **Target:** most weeks the player has **one to three active plays** and about **10-15 story
  decisions a season**, up from about 4.
- **Airtime:** the weekly ask cap (`AsksAWeek` = 2) limits new offers. Steps of an accepted play,
  which the player chose to chase, do not count against it.
- **Reach:** every arc must be reachable. The coverage sweep gains a skilled-player policy, and each
  arc must fire in it. The arcs the windows crowded out come back as plays that use the windows as
  their steps.

## 6. Proving skill matters

The harness gains two scripted players:
- a **reader**, who picks the option its knowledge favours;
- a **random** player.

Over the 80-season sweep, the reader must win clearly more plays, for example 20 points more.
Otherwise the plays are luck, and the evaluation would be scoring noise. This is the test behind the
owner's "not all wins are equal".

## 7. What this side hands the other session

- **Ledger rows.** One per play: offered (week, anchor, currency, cast); taken, declined or ignored;
  each step's choice and whether it matched the read; the outcome; what it paid. The shape is theirs
  to propose; this side writes to it.
- **Schema 17**, bundled: the plays' saved state (active plays, progress, outcomes) goes in the same
  bump as the ledger.
- **Vote display:** Intel payoffs reveal what the other session's model uses. A revealed alliance
  counts in the vote display the moment it is known to you.
- **Anchors** stay as agreed (HohCrowned, NomsSet, VetoWon, BlockSet, EvictionNight, SocialClose).
  Play deadlines are anchors. If the week gains windows, plays may use them as steps.
- **Windows as steps:** plays complete through the lobby, reply cards and votes, which needs the
  other session's events to be observable (a lobby resolved, a reply answered).

## 8. Decisions for the owner

All seven were taken as recommended on 2026-09-27.

| # | Decision | Recommendation (taken) |
|---|---|---|
| D1 | Density: from about 4 story choices a season to plays running most weeks (1-3 active, 10-15 decisions) | Yes |
| D2 | Plays are offered and taken on (opt-in), or tracked automatically | Offered; declined once, it may come back once |
| D3 | Odds: exact ("about 6 in 10"), or qualitative reads plus the clues you have learned | Qualitative by default. Exact odds unlock for people you know well, so Intel pays |
| D4 | Failure: real consequences (a grudge, exposure, lost trust), or soft | Real but bounded: a lost play never ends your game on its own |
| D5 | Threads: seeded (2-3 a season from the cast), or purely emergent | Seeded, so every season has narrative arcs |
| D6 | Plays use the windows' actions (lobby, replies) as steps; the step-asides stay | Yes |
| D7 | The name. "Mini quests" in-game could be **Plays**, Missions, Schemes or Moves ("Moves" collides with the dance moves) | Plays |

## 9. Milestones

| | Milestone | Done when |
|---|---|---|
| P0 | Measure: story decisions per season, arcs fired, the reader-vs-random gap | Baseline recorded |
| P1 | The play model on existing arcs (goal, currency, deadline, outcomes), the plays page, the receipt | The first 8 plays are winnable and losable, with tests |
| P2 | Steps from ordinary actions and the windows (after D6) | "Stay off the block" completes through the lobby |
| P3 | Threads: seeding, chapters, climaxes, the recap | Every season has 2-3 threads; each reaches its climax or a stated end |
| P4 | Density and reach: airtime rework, every arc reachable | 10-15 decisions a season; no arc at 0 in the reader sweep |
| P5 | Ledger writes and schema 17, with the other session | The evaluation reads a season's plays |

Each milestone runs the full suites. P1 onward adds the reader-vs-random gap to the sweep, and it must
hold.

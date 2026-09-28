# Story-arc system: implementation log

User request (2026-09-26): "All of this seems correct, implement" — i.e. docs 20 (plan) + 21 (BB addendum),
all milestones M0-M6, owner decisions = the recommended defaults in 20 §9 and 21 §7.

## Setup (done)
- Peer session "Claude code Unity MCP integration" [aa02a3] is actively editing the MAIN tree
  (C:/Users/kelli/Gamesim Big Brother, branch port/game-flow-v2-pass, uncommitted ceremony work)
  and runs tests on D:/GamesimAcceptance.
- My isolated checkout: **D:/GamesimStory**, branch **port/story-arcs** from 3420747 (git worktree add).
  - ProjectSettings.asset copied from main tree (GAMESIM_UMA define) — local only, NEVER commit it.
  - Nothing committed (user has not asked). Report branch + worktree at the end.
- My test copy: **D:/StoryAcceptance** (cloned from D:/GamesimAcceptance2). Name deliberately avoids the
  substring "GamesimAcceptance" (both harness busy-checks regex-match it; a run on *Acceptance2 blocks
  the peer's runs).
  - Needs `m_EnterPlayModeOptionsEnabled: 0` in its ProjectSettings/EditorSettings.asset after each
    sync (clone quirk, see memory headless-unity-test-harness).
- Runner: scratchpad/story-events/run.sh (to write): sync from D:/GamesimStory with
  GAMESIM_ACCEPTANCE=D:/StoryAcceptance, patch EditorSettings, run suites, floor check.
- Floors (Tools/baseline.txt): EditMode 1506, PlayMode 516, Uma.PlayMode 64, SimulationTests 782.

## Milestones and status
(see bottom for detail as work proceeds)

- [ ] Baseline run on the fresh checkout (sim / EditMode / PlayMode)
- [ ] M0 presentation-only
- [ ] M1 bundle + spine
- [ ] M2 the house remembers
- [ ] M3 staging and reach
- [ ] M4 getting to know them
- [ ] M5 bonds, showmances, secrets
- [ ] M6 production

## Progress (2026-09-26, later)
- Engine wiring done: conversation hook, sit-out, backdoor (NPC plan + player plan), proximity via story,
  consumers (vote grudge/bond factors, knowledge-gated alliances in vote + threat, jury story term + reasons,
  Confront grudge-first, Have-Not NPC turns), finale for an expelled player, NPC-initiated moments
  (confronted / campaign-pitch / caught-talking, keyed chance), strikes -> diary-room-calls / on-notice.
- Content done: 8 catalogue files, 67 arcs (spine, phase beats, ported legacy x9 + emergent/crisis/walk-ins,
  house remembers incl. 5 web branching stories, staging, know-them, bonds & secrets, production).
- Lore: 12 authored sheets (Jamie/Quinn from plan) + 12 legacy alumni sheets (game register, wonSeason flags).
- Engine additions while writing content: Moment lane, oncePerHeadliner, group cooldowns (branching 3-week gap),
  urgent, Waits + pulse "end:", chained beats = one ask, showIf, backfireExtra, departed roles, Recast,
  effects Trust/PhaseBonus/HookSpend/Snub/AllianceLeave/PushBack, ConductState.pushedBack.
- Ordering change past the boundary: StoryAnchor(EvictionNight) now runs BEFORE NpcSocialActions.Settle and
  StoryAnchor(BlockSet) before NpcSocialActions.Campaign, so houseguest-initiated moments fill gaps instead
  of starving the pool (found by the coverage report).
- dotnet: 782 existing + 19 story tests pass (StorySeasonTests, StoryCatalogLintTests). ArcCoverageReport
  (Explicit) prints per-arc counts; 49/67 arcs fire in random 80-season sweeps.

## Notes
- Baseline on the untouched checkout (2026-09-26 ~14:55): sim 782/782; EditMode 1505/1506
  (AuthoredTextureTests.EveryRoomFloorWearsItsAuthoredMaterialTiledToTheFloor fails - env?);
  PlayMode 107/516 because EpisodeHouse/LightingData.asset failed to load (see below). Re-run pending.
- Local-only fixes in D:/GamesimStory (NEVER commit as part of this work):
  - Assets/UMA + UMA.meta copied from main tree (gitignored anyway).
  - ProjectSettings.asset copied from main tree (GAMESIM_UMA define).
  - Assets/Gamesim/Scenes/EpisodeHouse/LightingData.asset: original bytes copied from the main tree.
    REPO BUG to report: .gitattributes `*.asset text eol=lf` normalises this BINARY file; a fresh
    clone gets a 4-byte-short, unloadable copy. NavMesh assets have a `binary` exception; this one doesn't.
- Design decisions taken while implementing:
  - Story system OFF by default (story.rulesStartWeek = 0) for states built by SeasonBuilder/
    ContentCatalog directly (tests); EpisodeDirector.StartSeason switches it on (week 1);
    v13->v14 migration switches on from week+1. Default scene engine (ContentCatalog 20260910) stays off.
  - Triggers/casting are C# predicates (not a closed Cond AST); beats/options/effects are data.
  - Past the boundary the legacy producers (BeginStoryline/OfferHouseEvent) do not run; their
    templates are ported into the story catalogue as one-beat arcs. NarrateHouse uses keyed draws.

## Progress (2026-09-26, evening)
- Unity compile verified (offline csc replay, negative control caught) + EditMode 1552/1554 -> fixed the last two
  (schema-14 dispatch list). PlayMode 515/516 before presentation work; the one red is
  DiaryRoom_EPrioritizesRoomOverNearbyNpc...: an NPC-clock tick (fraction carried over from set-up; slow frames)
  -> isolated with SuspendNpcAutonomyForDiagnostics (existing precedent). UMA 64/64.
- Fixes found by new tests: WebLoyaltyOaths threw on an Expelled actor (snapshot maps Expelled -> Evicted);
  BackdoorPlanned made public (Unity test asm can't see internals); budget leak: an NPC-held opener's
  follow-on fired as "chained" and skipped the weekly budget (confronted) -> chained only after a player answer,
  Castable checks airtime for the first player-facing beat; week-one gate (plan §5.2): nothing but first-night
  before week 1's eviction night; NPC showmance floor 45 was unreachable (warmest pair ~8-33) -> 5
  (0.64/season in the eight, 0.99 in twelve); prank-war could never cast (no roster card is Funny/Impulsive)
  -> playful axes; Pull card rebuilt with stale key -> overlapping buttons (caught by PlayMode).
- M3 presentation: Pull card (Step in / Stay out, Hear them out / Not now, Join the meeting / Not now),
  scene card (sceneCardOpen modal, follows its cycle, "Back to the house"), proximity auto-submit retired
  (walk-in offer + E = Step in), BeginSceneStage (HouseMeetingCoordinator.SceneStaging, idle actors only,
  ends on answer/decline/timeout/phase), live feed prefers the scene, drama mark on the room beacon,
  conversation beats in the conversation panel, Advance warning line, Fallout ceremonies (blowup, penalty,
  expulsion, house-meeting) in every table + StoryFalloutTests, Reactions appended with stand-ins,
  feeds holding card, episode recap (previously on / acts I-IV / next time).
- D-E room acts: PillowTalk, Cook, InviteUp, PublicDefense, AllianceMeet, CompPractice, PlayAGame (engine
  commands, offered in the conversation panel only in their room); pillow talk feeds late-nights.
- Ringer: veto punishment (last veto finisher joins the Have-Nots).
- New tests: StoryMechanicsTests, StoryStagingTests, StoryRoomActTests, StoryPacingTests (+ explicit
  PacingReport over 1,920 seasons), StoryFalloutTests (Unity), EpisodePlayModeTests.StoryPull (3).
- dotnet 839/839.

## Final pass (2026-09-26, night)
- Reactions: the five story reactions appended to both rigs' wiring tables as take-less rows
  (triggers undeclared; the director plays stand-ins); AuthoredClipWiring skips take-less rows.
- Diagnostic reports (ArcCoverageReport, PacingReport) compiled out of Unity (#if !UNITY_5_3_OR_NEWER):
  Unity's batch runner executes [Explicit] tests (PacingReport timed out at 180 s).
- Memory wall marks a removal (BorderRemoved). MemoryWall_CarriesEveryHouseguestAndDarkensTheEvicted fails
  when run ALONE with or without that change (cold portrait cache; the wall publishes a frame late) - it
  passes in the full suite. Pre-existing isolation-only timing, not touched.
- M6 sweep: EveryLegalWindowsRemovalPlaysToTheFinale (7, 8, 12).
- Floors raised: EditMode 1579, PlayMode 519, Uma 64, Sim 842.
- EditMode 1579/1579; dotnet 842/842; PlayMode (previous full run) 518/518 + keyboard Pull test passing.

## Integration with port/game-flow-v2-pass (2026-09-26/27)
- 76654f3: the story bundle is schema 16, after upstream's 14 (Have-Nots) and 15 (strategy windows),
  through FrozenEpisodeV15. There is one Have-Not list, the house's. Where StrategyRules.Apply, the reply
  cards own confrontation, gossip and a nominee's plea, and veto-dilemma steps aside. Nomination,
  replacement and veto weights sum the windows' terms with the story's. Safety deals and veto commitments
  are weighed by the windows alone. The room acts' command kinds follow upstream's.
- e040ae5 (finale, first part): ResolveJury's early refusal exempts an Expelled player. 6212ee3: a
  removal at four keeps the screen from the final-three card
  (StoryFallout_ARemovalThatLeavesThreeKeepsTheScreenFromTheFinalThreeCard).
- c300dc9: Render re-applies the Pull right after hud.Begin. Update's body-assembly render hid it for a
  frame, which made the keyboard Pull test fail about one full run in three.
- 3b108c9 (walk-out): the scene stage and the departure both own motion, and a staged houseguest is
  refused a walk-out. Everything reached main via PR #2 (99372b6).

## Owner decisions (2026-09-27)
- A word with whoever decides belongs to the strategy windows where they play: after-the-comp and hoh-room
  step aside as veto-dilemma already did, so the HoH room's letter from home goes with them in those
  seasons. Without the windows, the story's moments stand in. Covered by
  WhereTheWindowsPlayAWordWithWhoeverDecidesIsTheirs, and removing any one of the three guards fails it.
- Migrated saves keep the story on from week + 1, the plan's default, unchanged.
- The walk-out's length is unchanged: it can be skipped, and it is off under reduced motion.
- Floors: EditMode 1711, PlayMode 602, Uma 64, Sim 926.

## Plays: P0 and P1 (plan 30, 2026-09-27)
- The model (StoryPlays.cs, EpisodeEngine.Plays.cs), off the schema:
  - an arc's PlayTemplate holds the currency, the goal, the anchor deadline, progress read from the
    season, and won/part/lost effects and lines;
  - "taken on" is the offer's take-it-on step in the cycle's path, and the outcome is the cycle's
    endingId, in the ledger's own words (won/part/lost, plus declined);
  - DecidePlays runs first at every anchor and after every answer. A play is won the moment its goal
    is met, however it was met, and never lost before its deadline;
  - a first refusal cools one week, so it may come back once (D2);
  - the play lane holds three at once;
  - StoryRules.Plays = 7 = Current, so seasons stamped earlier never see a play.
- Receipts (StoryLog.Receipt, the player's alone) come from the effects a play's steps and outcome ran,
  never a paraphrase. Promises, deals, alliances and lore keep their own lines.
- Eight plays (StoryCatalog.Plays.cs):
  - The Secret Alliance and Know Them (Intel);
  - Stay Off the Block, The Favour and Stir the Pot (Power);
  - Their Word and Settle It (Trust);
  - Build the Numbers (Alliance).

  Stay Off the Block is decided at the final block, so a backdoor or the veto can still change it. Its
  two Head of Household conversations show only where the strategy windows do not play; there, the
  lobby is the way in and counts.
- In the house:
  - the Pull offers a play with Take it on, eyebrow "INTEL · A PLAY · UNTIL EVICTION NIGHT", and the
    goal as its stakes. Take it on opens the first step on the scene card;
  - the card ends with the play won, part-won, lost or still in play, the last step's line, and that
    card's receipts;
  - PLAYS heads the free-time panel's story block and the notebook's Story section.
- Tests: StoryPlayTests (13) and EpisodePlayModeTests.Plays (the whole loop through the real HUD).
- P0 baseline, from PlaysReport (explicit, dotnet only; 40 seasons of 8 and 12):

  | Player | Story decisions a season | Plays offered a season | Won / part / lost | Win rate |
  |---|---|---|---|---|
  | Random | 5.4 | 0.9 | 4 / 3 / 9 | 25% |
  | Reader | 7.8 | 1.2 | 25 / 2 / 12 | 64% |

  The reader, who takes every play and picks the best odds shown, leads by 39 points (§6 asks for 20).
  Arcs fired: 43 of 73.
- Gaps the baseline shows:
  - plays are sparse: about one a season against D1's one to three most weeks. That is P4: airtime for
    offers, and steps outside the ask cap;
  - The Secret Alliance never cast: NPC-formed alliances have no knowledge fact yet. The other session's
    read rules add one (schema 17);
  - Build the Numbers never cast: the harness player forms alliances with anyone it can.
- Floors: EditMode 1724, PlayMode 605, Uma 64, Sim 939.

## Plays: P4, density and skill (plan 30, 2026-09-27/28)
- Airtime (plan 30 §5):
  - plays keep their own: one play card open at a time beside the arcs' one, and
    `PlayOffersAWeek` = 2 offers a week outside the arcs' two asks;
  - a step of a play the player took on is held back by no weekly count;
  - plays draw from their own pool (`TryStartPlayFromPool`, nothing-weight 20, roll key ":plays"),
    so they no longer crowd the arcs' draw or wait behind it.
- The social bonus pays (owner's decision, "extra actions"): every ten points banked
  (`StoryModifiers.PointsPerAction`) buy Good Standing, one more conversation this week and next,
  through the modifiers' existing term. `SocialActionBudget` is untouched, for the other session's
  per-window budgets. Only story choices pay points today: the native post-eviction diary's choices
  carry none.
- Skill is the point (§6). The first P4 sweep had the density but a 5-point gap, because the options
  were not reads:
  - unequal bases, so the best odds were the same option whoever it was aimed at;
  - passive wins: Stay Off the Block had no option at all where the windows play, and was won 65% of
    the time by waiting;
  - steps that could not win: a second good conversation in Know Them re-asked what the first had
    answered, and one apology could never settle a nomination's 70-84 grudge.

  Fixed with three rules for every step: the same base (50) for every option; a different approach
  for each, so the person's traits and what you know about them decide which lands; and the same
  payoff for any that lands. Then per play:
  - Stay Off the Block:
    - it is offered only when the Head of Household's real ranking, `NominationWeight`, has you in
      the bottom two;
    - it is lost the moment you are named, at the nominations or by a backdoor
      (`PlayTemplate.failed`), whatever the veto does after;
    - where the windows play it needs somebody to work through, anybody with nothing against you or
      the HoH, and offers three ways in (vouch Warm, go to bat Candid, play you down Calculated), each
      +20 in the HoH's eyes;
    - "point at somebody else" is gone: from the very bottom one name dropping past you still leaves
      you in the two.
  - Know Them needs three things, and a conversation that lands always teaches two: the asked facet
    or, failing that, the next unknown (`Fx.RevealMore`).
  - Settle It has two conversations (the second on eviction eve), each easing 50.
  - The Secret Alliance, The Favour and Stir the Pot gained a third approach.
- PlaysReport, now 80 seasons (8 and 12, seeds 1-40, as §6 asks):

  | Player | Story decisions a season | Plays offered a season | Won / part / lost | Win rate |
  |---|---|---|---|---|
  | Random | 8.5 | 3.0 | 27 / 5 / 44 | 36% |
  | Reader | 14.1 | 3.2 | 139 / 17 / 64 | 63% |

  The reader leads by 27 points, at D1's density (10-15 decisions).
  PlaysByOption, a new explicit diagnostic, shows each play's endings follow its steps: a step that
  lands wins or part-wins, a pass or a backfire loses.
- PacingReport (1,920 seasons) against c4941ae: the arcs keep their airtime and plays come on top.
  StoryPacingTests.Measure now counts play offers apart from the arcs' asks, so "asks" is the arcs'
  alone, where at c4941ae it held the plays too. In the default eight: stories finished rose from 2.3
  to 3.7 a season, arc asks are 3.0, a card shows in 35% of weeks, and 78% of seasons are pariah-free
  (79% at c4941ae). The twelve-house's 43% pariah-free and the default eight's asks below 4 predate P4.
- On 58f8623, the merge of c4941ae with the other session's read (schema 17): PlaysReport is unchanged,
  because SeasonBuilder never switches the read rules on (only the director does), so no sweep yet has
  a hidden NPC alliance for The Secret Alliance to find.
- Floors: EditMode 1746, PlayMode 609, Uma 64, Sim 956.

## Plays: P4, reach (plan 30, 2026-09-28)
- Why arcs never came round, from an audit of the 22 the harness never saw:
  - six step aside where the strategy windows play (after-the-comp, hoh-room, veto-dilemma, and
    confronted, campaign-pitch and caught-talking, which the windows' reply cards replaced). They come
    back as plays that use the windows as steps (P2);
  - the rest needed things that never happened (an NPC alliance of three, NPC warmth of sixty, a
    deep conversation), had thresholds a skilled player rarely reached, or starved at eviction night,
    where most arcs start and the pool draws one;
  - an engine bug: a beat put off at eviction night waited for SocialClose, which fires nothing,
    and held its lane until it went stale.
- `StoryRules.Reach` = 8 = Current. Every change below to an arc that existed before holds from it
  on, so a season stamped earlier plays exactly as it did. No schema change.
  - `EpisodeEngine.RetryAnchor`: a beat put off at eviction night tries again at the next HoH.
  - The pool's pause is shorter (nothing 25 at eviction night, 40 elsewhere; was 40/60): the windows
    took six arcs out of the pool and left the default eight at three asks against §5.2's four to six.
  - power-shift: a pair is a bloc, and where none exists the discovery can be the bloc forming (a
    leader with a reason, and whoever they get on with best; the scene makes the pact). Either way the
    player saw who is in it, so the read counts it.
  - Leaking an alliance with no fact (known to everyone) no longer makes it a secret from the rest.
  - walked-in-whispering: two allies, or two who like each other (20 both ways), whisper.
  - legacy house events name the nominee still in the house at eviction night, not the evictee.
  - the-house-turns may also come at the final block.
  - ride-or-die at mutual 35 (was 50), unspoken-pair at 45 (was 60), both with the final block as a
    second chance, as the emergent friendship and rivalry have.
  - staged-feud: bold and social between them (three, neither at zero) and mutual 10, not one card.
  - what-they-left-out: knowing somebody well (three things, one of depth two) is trust enough.
  - on-notice: a hothead of volatility 4 may escalate (was 5, which only one roster card reached).
- On the plays rules (not yet played by anyone, so changed in place):
  - Build the Numbers is won only with all three in one alliance, and casts over pacts of two;
  - Know Them's fallback reveal stops at depth two and never reaches a secret.
- The harness:
  - StorySeasonTests.StorySeason switches the read rules on, as the director does for every season
    (never SeasonBuilder, whose recorded seasons would shift). The Secret Alliance can now come round.
  - SkilledNext, a skilled player: the reader's answers, a circle of three, alliances only at mutual
    20 and two at most, a final two at eight left, loyalty sworn, the circle's offers taken, pillow talk
    with somebody open to it, and a rule bent a quarter of the times one is on offer.
  - ReachReport: 480 seasons (the regular eight, twelve and sixteen, and the All-Stars eight).
- Reach over 480 seasons: every arc but two comes round. The two are on-notice (an NPC's escalated
  blow-up) and the-accounting (an NPC voting out their own ally). Both wait on houseguests' dealings
  with each other, which the simulation does not play: the director's NPC world runs NPC
  conversations in real time, so in the sweep NPC pacts are 0.04 an eviction night and NPC warmth
  rarely reaches 20. Rare (1-3 in 480): the-secret-alliance, walked-in-whispering, loose-lips,
  power-shift, the-house-turns, the-reckoning-yours, unspoken-pair. StoryReachTests proves each fix
  casts where its conditions hold.
- PlaysReport on reach: the reader wins 60% against random's 36% (24 points) at 13.5 decisions a
  season; the reader's seasons now see 54 of 73 arcs (51 before).
- PacingReport on reach (1,920 seasons): arc asks rise to 3.5 in the default eight (3.0), 5.3 in
  All-Stars and 4.5 in the twelve, the ceilings hold, removals stay at 0%, and 77% of the default
  eight's seasons are pariah-free (78%).
- Floors: EditMode 1763, PlayMode 609, Uma 64, Sim 973.

## Threads: P3 (plan 31, 2026-09-28)
- Integration: port/story-arcs was fast-forwarded to 9a46c1f, the other session's R2-R4 (the levers,
  the ledger's writers and Game Sense), before this work began.
- The design is 31-threads.md. A thread is a season-long story in its own lane (`StoryLanes.Thread`,
  three at most), carried on the story cycle saves already hold, so there is no schema change:
  - its cast is who it is about;
  - its path has one step per chapter, with the chapter cycle's id and how it went, rewritten when the
    chapter ends;
  - its ending is how the season came out for it.

  `StoryRules.Threads` = 9 = Current: seasons stamped earlier seed none.
- Seeding happens at the first eviction nights (weeks 1-3), each kind at most once:
  - the bond: your warmest houseguest both ways who holds nothing much against you;
  - the rivalry: whoever holds the most against you, or else the coldest both ways, never the bond's
    friend;
  - the numbers: your own alliance, or else the two warmest who could be one.
- Chapters are the catalogue's own plays and arcs, aimed at the thread's people through
  `StoryContext.focus` (`StoryCatalog.Among`: a cast chooses its lead only from them and keeps every
  condition of its own). How one ends picks the next:
  - the bond: Settle It if they hold a grudge; Know Them; Their Word; Ride or Die or Late Nights;
  - the rivalry: Settle It, then Stir the Pot if that fails;
  - the numbers: Build the Numbers until the allies are one alliance, then The Secret Alliance, Stay
    Off the Block and The Favour, week to week.

  A chapter starts only at its arc's own anchors, and it spends the airtime like any story. A play the
  pool casts for a thread's person is taken as that thread's chapter.
- Climaxes and stated ends:
  - the numbers end at the final four: held (you and an ally), or broken;
  - the rivalry ends when either of you leaves: won, made peace (if Settle It landed), or lost;
  - the bond ends at the finale, read from the jury (`StoryFinale`, one call in `ResolveJury`): to the
    end, for you, against you, or parted;
  - a thread with nothing it could play for a month fades (the numbers wait for their final four
    instead);
  - one whose player left ends as they left it.

  The climax comes at its point even with a chapter under way.
- Two things silently ended threads, and neither may touch one now:
  - the legacy `Storylines.AbandonStale` gave up on any story three weeks old whose card was not
    answered, and a thread has no card;
  - the final-three sweep in `StorySocialClose` ended every running story before the jury.
- In the house:
  - THREADS (`EpisodeDirector.ThreadsHeading`) follows PLAYS in the free-time panel and on the notebook's
    plays page. Each thread shows its label with names ("Your bond with Alex"), then the chapter it is on
    and how that went, or how it ended;
  - the recap's "Previously on" and "Next time on" lead with the threads, by name.
- Tests: StoryThreadTests (14) cover seeding, focus, chapter records, every climax, the player's exit,
  both sweeps and the rules gate; EpisodePlayModeTests.Threads covers the plays page. Five mutants are
  each caught.
- ThreadsReport (explicit; 120 skilled seasons of 8, 12 and 16):
  - every season whose player saw week two has two or three threads (96 of 96; the other 24 lost their
    player at the first eviction);
  - every thread reaches its climax or a stated end;
  - chapters average 1.2 a thread, most often Their Word, Settle It, Stay Off the Block and The Favour.
- PlaysReport with threads: 15.4 story decisions a season for the reader (at the top of D1's 10-15),
  9.4 for random; win rates 61% against 37%. ReachReport: unchanged but for the-house-turns' usual
  flicker.
- Floors: EditMode 1818, PlayMode 614, Uma 64, Sim 1021.

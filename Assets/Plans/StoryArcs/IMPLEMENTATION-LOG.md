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

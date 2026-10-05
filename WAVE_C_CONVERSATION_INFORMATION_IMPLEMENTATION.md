# Wave C E2 — information from open game talk and public airing

Implemented above isolated gameplay commit `4dd48a0d8dd591705aef3538f130c0f276b3fd99`.
This completes these two E2 behaviors in source; reply-card trade-offs and the
all-verb balance gate remain open. Nothing here is merged into the frozen QA
candidate or live project. No native gameplay, build, visual or human pass is claimed.

## Source, authority and compatibility

`Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md:454-460` requires open game talk to
carry information tactics does not, and public airing to reveal sides as reads.
Original web `src/contexts/reducers/reducers/player-action-reducer.ts:98-109`
provides the open-game threshold and warmth; `:798-834` provides house-meeting
relationship consequences. The existing native `WebSocialVocabulary` retains
those values. The information payoff is the approved native extension, not a
claim that the web already stores these native records.

Both additions require the unreleased schema23 `EconomyRulesOn` fresh-season
boundary. Legacy and delayed rules keep their original behavior. No schema,
saved field, command kind, claim-source vocabulary or migration change. Existing
command actor, phase, revision, duplicate and action-budget gates own authority.
The director still saves the detached accepted state before publishing it.

## Material changes

- `EpisodeEngine.ConversationIntent.cs`: successful open game talk records up to
  two distinct opinions belonging to the actual speaker: their strongest and
  weakest directed standing toward other active NPCs. Ordinal IDs break ties;
  the weakest excludes the already chosen strongest, even if all scores tie.
  A three-person house can supply only one opinion. The player, speaker and
  evicted people never become opinion subjects.
- These are existing `Told` standing rows plus player-owned private memories
  attributed to the speaker, with player/speaker-only information events. Their
  words describe warm/cold/uncertain bands. Selection occurs inside the accepted
  command, never a hidden-ranking preview. No ballot or alliance fact is copied.
- A successful airing records each active NPC's witnessed backing/opposition
  and their resulting NPC-to-player standing. The side comes from the source
  reaction even at clamped relationship scores; the standing is the actual
  resulting score, never the +8/-10 impact substituted for a score.
- Reactions use bounded existing `Overheard` standing rows and new event kinds
  `airing-backed` / `airing-opposed` in `ConversationIntentRules`. These events
  explicitly say that no vote is promised. They are the player's observations,
  not new NPC memories or copied private plans/ballots.
- `EpisodeEngine.cs` invokes these helpers only after the original successful
  rolls and relationship changes. Tactics, rally and failed actions receive no
  new information. Existing warmth, costs and season RNG draw counts/order
  remain unchanged. Extra fresh-season log sequences can change later keyed
  story outcomes; this is not historical-season drift.
- `HouseguestNotes.cs` maps each bounded, two-person reaction event to the
  notebook's Read category, retaining its exact captured words. It does not
  call the live agenda reader for that observation. It does not consume the
  free ReadPerson or AskThisWeek opportunity. Original meeting contact effects
  still apply: this is not a promise that meeting someone cannot establish
  contact under existing allegiance rules. An Overheard row alone grants no
  deliberate-read or hidden-agenda privilege.
- `EpisodeDirector.ConversationGroups.cs` advertises fresh open game talk as
  `learn · risk`; both `FreeTimeBoard.cs` and `FreeTimeScreen.cs` show airing as
  `Read · high risk`. The detailed tile explains the conditional read payoff
  and absence of a promised vote. Existing captions, commands and old-season
  labels remain unchanged.
- Existing standing512/event256/player-memory30 caps remain; observed sides
  age out with the event log, and learned bands keep the existing shelf life.
  The stored standing never follows later hidden relationship changes.
- `ConversationInformationTests.cs`:77 new pure/Edit cases, covering casts3-16,
  success/backfire, tie order, scopes, source RNG/relationship/cost equivalence,
  privacy, clamping, capacity, expiration, invalid/stale/duplicate commands,
  compatibility, expected payoffs and actual scripted season continuations.
- New PlayMode partial:9 tests for both UI adapters, success/backfire, old tags,
  locked saves, stale clicks, exact durable command replay/reload, campaign
  seats, both text sizes/keyboard rings, and the actual notebook Read filter.
  Shared `AssertIntentCommand` gains an optional text argument for exact meeting
  replay. These tests are authored and compiled, NOT executed in Unity.
- Registered the pure test file and raised floors to Edit2828 / Play948 /
  UMA77 / pure1836. Integration must also retain eda's11 QA Edit tests:2839.
  Three new meta GUIDs are unique; no portable GAMESIM_UMA define is committed.

## Evidence

Root: `D:/CodexGamesimEvidence/integration-20261004`.

- Focused pure01:68/68; pure02:72/72. Both pass, but predate final season coverage.
- Full pure03:1831/1831; final pure04:1836/1836 executed results, zero failures.
  Its discovery count1849 includes13 explicit reports not executed in this run.
  `wave-c-information-pure-04/wave-c-information-full.trx`, SHA256
  `7b4362639137af51fe2ac311b483ce3f9a6f0162d3fe8a4743db622f8f755c4a`.
- Five new scripted seasons with story/agency/commitment rules active finish
  at stored cast sizes3/6/8/12/16. They actually choose open game50 times and
  airing46 times, with56 exact save-shaped managed replay checkpoints. This
  exercises the new paths; it is not Unity persistence/UI or pacing evidence.
- The narrow payoff harness pins base expected warmth open game3.4 versus
  tactics3.5; airing-1.875 versus rally1.560714 per NPC in an eight-person house.
  Expected first-opportunity information is1.4 opinions /4.55 reactions versus
  zero from tactics/rally. Source failure gates remain30%/35%. This excludes
  social-stat scaling, story effects, repeated already-known facts and win rates;
  it does not prove all verbs/reply choices are balanced or non-dominated.
- Offline01 and final offline02 each compile5/5 fresh NoUMA assemblies, zero
  errors. They read only the idle `D:/GamesimNoUma` cache and write fresh external
  outputs, not the active UMA cache. Final output:
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/f1d341d6bbe94e0a951eb5b99d664c85`.
  `wave-c-information-offline-02.log`, SHA256
  `ce79ff5297a13672fd0dd7edce10a79f07375345b651969000aa44e003e29e48`.
- Full UMA compilation and native Play/Edit execution for this gameplay branch
  remain pending. Root manual review only; independent agents are unavailable.
  No expected source fixtures/goldens were changed, no checks were disabled,
  and no before/after source-drift attestation is implied by these test outputs.

## Integration and remaining gates

The separate eda schema22 baseline has closed NoUMA Edit2540/Play903 passes.
UMA g22u1 Edit2540 passes, Play is still running. That candidate contains NONE
of this branch's E1/E2/E3 work. Keep its source/controllers frozen until closure.

Next gameplay: E2 reply-card payoffs and full action comparison; E4 HoH pitches;
E5 bounded block-speech influence; all four owner-required Wave D systems.
Combine and run full suites, migration and desktop verification against the
actual final candidate. Real-asset completion, actual1920x1080 at60FPS, visual/
accessibility review and three-first-time-player human acceptance stay separate.
No live edits, recovery files, scenes, user saves, prior builds or remotes changed.

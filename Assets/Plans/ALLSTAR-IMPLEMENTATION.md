# All-Star implementation and integration handoff

Status: implemented and tested as an isolated candidate; NOT activated in the shared integration or open Unity project.

## Source and ownership

- Private checkout: `D:\GamesimAllStar20261012`, branch `codex/allstar-characterization-20261012`.
- Shared source HEAD: `515246bfeb8633a99630e8088cb92ca60ba54ce6`.
- Frozen private starting snapshot: `0a1e00b9` (201 current integration edits preserved). Do not cherry-pick this snapshot: it contains GameSim Dev's existing work.
- Source-copy manifest: `C:\Users\kelli\Documents\Codex\2026-09-20\re\work\allstar-characterization-20261012\snapshot.json`.
- GameSim Dev's Phase/dialogue integration, live C: project, D: play/acceptance copies, scenes, assets, editor states, ProjectSettings and purchased packs were not edited by this feature.

## Implemented behavior

- Immutable research catalog: twelve alumni, thirteen directed historical pair entries, twenty-six source records; public game events through BB26 (2024), including earlier reality shows. Original authored speech is labelled fictional, not a historical quotation. See ALLSTAR-RESEARCH.md.
- Shared honorific-aware UI naming displays Will, retains full Dr. Will Kirby and all identity/model/save keys. Existing persisted pact names and competition word normalization remain unchanged.
- Candidate-only default-on Player histories choice survives cast, creator and slot-edit handoffs. Off retains career reputations, distinctive NPC repertoires and ordinary conversation voices while removing inherited pair facts, standing and opening deals.
- Initialization follows canonical first impressions, runs once before the first save, excludes renamed fictional copies and ambiguous duplicate alumni, and does not consume season randomness. Past events seed provenance and separate prior trust, never current-week grudges or relationship acts.
- NPC Derrick/Cody start with one canonical final two. A human partner receives an ordinary proposed deal and chooses whether to renew it. The offer reserves one of three weekly slots; answered offers cannot refill the round. No automatic alliance or ride-or-die protection is stacked.
- Current known direct Safety, promise, deal or alliance betrayal clears positive inherited trust through the accepted-command writer. Hidden ballot consequences wait for genuine knowledge. Current penalties are not duplicated.
- Public pair risk is separate from knowledge of a current private agreement. Career reputation stays within the current threat cap. Trusted larger threats can become early nomination/vote shields; that value ends at six remaining.
- NPC Will can deliberately throw when protected, and competes when nominated, when a binding partner needs protection or during final HoH. The candidate caps his own raw score share at .5, preserves every other competitor's rolls/scores, and keeps effort decisions private. No permanent stat reduction or guaranteed loss.
- Learn and share -> past games -> connection -> approach uses the existing navigation and transaction path. An accepted request publishes one spoken receipt bound to its exact command/fact/subject/approach. Playback does not reroll replies or inspect hidden current deals. Original ordinary dialogue gives each alumnus a distinct voice.
- Existing NPC invitations/approach/bubble ownership handles the opening offer; no parallel movement or conversation manager.

## Compilation boundary

The future mechanics and added DTO fields exist ONLY under `GAMESIM_ALLSTAR_CANDIDATE`. No Unity project define was added. The normal build keeps the exact schema-28 field shape and original schema tripwire; the catalog and name fixes compile without the candidate.

The candidate uses proposed schema 29 for fresh-state testing only. That number is not allocated or released for shared integration. Candidate save readers deliberately refuse schemas 1-28 pending a qualified whole `FrozenEpisodeV28` reader. They never normalize or reconstruct historical state. Changing the selected catalog edition cannot rewrite an existing season; retain immutable editions when adding future content.

Example isolated test invocation:

```
dotnet test Tools/SimulationTests/SimulationTests.csproj -p:AlumniCandidate=true --artifacts-path Tools/SimulationTests/obj/allstar-candidate --filter "FullyQualifiedName~Alumni|FullyQualifiedName~CharacterNames"
```

Do not enable the candidate symbol in an editor containing existing season saves. Integration must allocate the next schema jointly after GameSim Dev's all17 historical reader/corpus/real-save gates close. The original tripwire was not weakened, moved or skipped.

## Validation and retained evidence

- Normal-build Unity-free suite: all 5,381 executed cases passed, including the unchanged schema pin; 143 explicit long-running cases were not executed by this default invocation. Evidence: `D:\CodexGamesimEvidence\allstar-normal-20261012\results\allstar-normal-verified.trx`.
- Final default-build schema, reader-inventory, catalog and naming guards: 62/62 passed. Evidence: `D:\CodexGamesimEvidence\allstar-normal-20261012\results\allstar-default-final-guards.trx`.
- Candidate feature selection: 99/99 passed. Evidence: `D:\CodexGamesimEvidence\allstar-candidate-20261012\results\allstar-candidate-final-source.trx`.
- The candidate selection includes sixteen complete public-command seasons: four fixed seeds, eight and twelve contestants, histories on/off. These prove bounded season completion and valid state, not balance or character authenticity acceptance.
- Additional cases cover selected spoken words, stale/duplicate/spoofed requests, no cost/RNG on refusal, private-deal independence, copy isolation, missing/renamed/duplicate identities, catalog provenance, evidence pruning, actual selective score suppression and current known betrayal.
- Candidate NoUMA source compilation: five/five assemblies without errors, using cached Unity dependencies and current declared precompiled references. Evidence: `D:\CodexGamesimEvidence\allstar-candidate-20261012\offline-candidate-final-source.log`. This is not an editor import or native test run. The first cache replay failed because its old PlayMode response lacked the already-declared Newtonsoft reference; failed evidence remains retained.
- Original off-tree full test output is retained separately; nine source-scanner failures came from its binary having no source tree above it. The source-bound rerun above resolves those. An intermediate candidate test caught a missing canonical Safety invalidation; the accepted-command hook was corrected and rerun.
- Normal suite floors increased by 45 EditMode/pure cases and one PlayMode case. Candidate-only cases are not counted as normal discovery. Eleven real SaveStore cases and native UI/setup/nameplate cases compile but were NOT executed in Unity.

## Remaining acceptance

- GameSim Dev's current all17 schema-28 reader/corpus/real-save gate remains OPEN.
- Allocate and integrate the future schema and retain qualified historical readers before activating the mechanics.
- Run actual SaveStore, editor import, native EditMode/PlayMode and UMA validation on the combined final source.
- Review rendered setup/navigation/character speech/NPC approach at supported screen and text sizes.
- Run paired balance cohorts before accepting provisional trust/reputation/shield/throw coefficients; the small completion smoke is not calibration.
- Human playtests must judge whether the characters feel recognizable. No shipping, performance or human acceptance is claimed here.

The normal default fixes and guarded candidate are reviewable together. Apply only the feature delta after the frozen starting snapshot, reconcile GameSim Dev's newer edits, and leave the active shared worktree under its current owner's control.

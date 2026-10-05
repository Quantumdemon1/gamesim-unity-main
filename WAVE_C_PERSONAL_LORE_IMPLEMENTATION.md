# Wave C E2 — personal lore and a less redundant dial

Implemented above isolated gameplay commit `39a379f109a91bb8395d81e34bb51a227ccf7976`.
This is the personal-chat and plain-Talk portion of E2, NOT all of E2. No merge,
live promotion, native gameplay test, desktop build or final acceptance is claimed.

## Source and compatibility

`Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md:454-460` requires more lore and less
warmth from personal chat, distinct information from open game talk, plain "Spend
time together" off the dial, public-airing reads and differentiated reply cards.
The original web `player-action-reducer.ts:94-96` gives personal chat5-8 warmth;
the existing native `WebSocialVocabulary` faithfully retains it. This increment
is an approved native design extension, not a rewrite of that source fixture.

The unreleased schema23 `economyRulesVersion` boundary selects the new behavior.
Personal chat's trade-off also requires the story system's lore milestone to be
active this week: a lore-less/delayed season does not pay less warmth for a benefit
it cannot receive. Old saves retain their warmth, facet order, single reveal and
seven-petal dial. No schema, migration, saved field, command kind or save limit changed.

## Material changes

- New pure `ConversationIntentRules`: fresh personal chat uses its existing draw
  for2-4 base warmth (expected3), compared with small talk3-5 (expected4) and
  relationship building5-12 (expected8.5). No additional season RNG draw.
- Fresh personal chat can learn up to two distinct reachable facts, instead of
  one. Its facets additionally include work, hot buttons, conflict style, legacy
  game history and tendencies. Existing facets retain their relative priority.
  The game goal remains a game-conversation topic; secrets/depth4 remain arc-only.
- Existing rapport and personal-beat depth gates, snapshotted fact IDs, authored/
  legacy/derived sheet boundaries and200-known-fact capacity are unchanged. More
  facts do not invent biography or expose another contestant's private knowledge.
- Story hooks re-evaluate the next eligible fact after each successful learn and
  stop at the existing cap. There is still one contact increment, rapport gain,
  social action and story-trigger opportunity. Extra lore log sequence numbers
  may change keyed fresh-season story outcomes; historical seasons are untouched.
- The fresh personal-chat petal is tagged "learn". "Build the bond" remains its
  own warmth petal. The distinct plain `Talk` command, captioned "Spend time
  together", moves to an ordinary BOND row after "Talk game openly". Fresh dials
  have six petals; legacy dials retain seven. All captions, Talk effects, durable
  command paths and More's first-row keyboard destination are preserved.
-38 new pure/Edit cases cover warmth buckets/bounds, cast sizes3-16, regular and
  All Stars sheets, per-recipient private lore, depth/secret/goal boundaries,
  detached readers, legacy and delayed rules, replay, duplicate delivery, invalid
  input, capacity and the personal-versus-bond payoff trade-off.
- Seven new Play tests cover fresh/legacy layouts, both text sizes, a16-person
  keyboard ring, nonoverlap, More focus, real personal/Talk buttons, complete
  replayed/durable outcomes, duplicate clicks, closing, locked saves and campaign
  action accounting. These are authored and compiled, NOT executed in Unity.
- New-file metadata is unique. Floors: Edit2751 / Play939 / UMA77 / pure1759.
  Combining eda's QA fix must retain its11 additional Edit cases: combined2762.

## Evidence

Root: `D:/CodexGamesimEvidence/integration-20261004`.

- Focused pure01:38/38 passed.
- Full pure02:1759/1759 executed results passed, zero failed/skipped. The discovery
  count1772 includes13 explicit reports that are not silently counted as executed.
  `wave-c-intent-pure-02/wave-c-intent-full.trx`, SHA256
  `5d921ca1a618f3fdb4f281c45d5e84f01a77e3c01f8d41b1054004bfe4b9f158`.
- Offline01 failed because the new runtime test referenced a nonexistent `Cmd`
  helper. It is retained. The test now reconstructs the actual accepted command
  identity and compares the complete engine replay, director and saved outcome.
  No production assertion/validation was disabled to make compilation pass.
- Final offline02:8/8 fresh assemblies, zero errors. Output
  `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/c7ab28bffd284dd483628fc1a26d3f3f`;
  `wave-c-intent-offline-02.log`, SHA256
  `7952bfcb44d0dd78298111602fa13795d87f28f1911ffde2c4fe7812d64227e8`.
  Read-only references came from the idle UMA cache before the separate UMA run began.
- Explicit reports3/3:54 unchanged legacy-golden seasons,54 commitment-rule walks,
 480 paired-policy seasons. Artifact
  `wave-c-intent-measurement-01/wave-c-intent-season-regressions.trx`, SHA256
  `ed0207eb43eb3a8071776d5a1d9b1cf162869d4d15b6e4b9ec71d56ff65454c8`.
  The existing policy drivers do not exercise this new personal-lore trade-off;
  the38 dedicated cases do. Their Game Sense gaps11.14/9.96 remain unchanged,
  not evidence of final balance. No expected goldens were changed.
- Root manual review only; independent agents remain unavailable. No before/after
  source-drift attestation, native gameplay pass or visual-quality approval is implied.

## Separate integration run and remaining work

The frozen schema22 QA candidate at eda, containing NONE of E1/E2/E3, now has a
closed no-UMA full pass: g22n3 Edit2540/2540 and Play903/903, natural exit0,
zero source drift, cleanup errors, unowned descendants or timeout. Visual review
is pending. Same-pin full UMA g22u1 is in progress; a matching shipping build and
desktop execution still follow. This evidence does not accept the gameplay branch.

Finish E2's open-game-talk information, public-airing reads, and reply-card
claim/promise/deal trade-offs, then evaluate all verbs together for dominance.
E4 HoH pitch cards, E5 bounded block-speech influence, all four required Wave D
systems, final real assets, actual1080p60 profiling and human acceptance remain.
Current live edits, scenes, recovery, user saves, art/vendor files and previous
builds are unchanged. No publishing, account connection or paid service.

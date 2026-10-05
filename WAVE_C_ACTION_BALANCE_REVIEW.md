# Wave C E2 - measured action trade-offs

2026-10-05. Implemented above gameplay `dda012a61338a55b56a3b91d1b0e334d89ed0f79`.
This increment adds a reproducible measurement/regression harness. It does not
change production rules, scenes, saves, UI, RNG, odds, thresholds or source goldens.
It is isolated from the currently running c272 UMA candidate and from the live C:
project. The whole E2 balance/acceptance gate is NOT closed by this document.

## Material changes

- New `Assets/Gamesim/Tests/EditMode/ActionBalanceHarnessTests.cs` and unique Unity
  metadata: 86 cases, registered in `Tools/SimulationTests/SimulationTests.csproj`.
- `Tools/baseline.txt`: Edit floor2917 ->3003; pure1925 ->2011. Play958 / UMA77
  unchanged. Combining with c272 must retain its11 QA Edit and4 Play additions:
  combined floors3014 /962 /77 /2011, not a claim that those suites ran together.
- No production/model/save-schema change. No preview reads of private rankings,
  command authority changes, rewritten parity fixtures or weakened old assertions.

The harness measures the NPC's directed view of the player, separately from total
house warmth, rapport, newly learned lore and goal facts, new opinion bands,
witnessed reactions, new story starts and frequency of warmth loss. It never
invents an exchange rate between those outcomes. Repeated ledger rows, the same
opinion band from a different source, and somebody else's private memory are not
counted as newly learned information. This deliberately conservative novelty
metric does not value refreshing an old belief or revisiting a previously known
band; those are distinct possible utilities, not proven new information.

## Measurements and findings

1. **64,512 committed conversation/meeting counterfactuals.** Seven core E2 verbs,
   72 contexts and128 fixed hashed seeds per verb/context. Casts3/8/16, player
   social0/10, directed views-50/0/95, starting rapport0/3, and previously unknown
   versus known lore/opinions. Each action gets the same starting state and saved
   seed in a sample. Valid state and one action spent are checked after every
   command; the input fixture must remain unchanged. Focus is the default Maya
   identity, not every possible cast/personality. Each JSON row and each sampled
   mean-dominance finding is retained in the TRX instead of discarded on failure.
2. **Exact source-bucket mechanical frontier.** Under the documented independent
   uniform-draw probability model, all seven verbs have a trade-off in an
   eight-person, neutral-view, rapport3, unknown-information situation. The
   warmth expectations are4 small talk,3 personal,3.5 tactics,3.4 open game and
   3.85 sharing a secret; rally1.560714 per NPC /10.925 house total; airing-1.875
   per NPC /-13.125 total. Lore, goal and hit/miss information benefits are also
   checked through actual commands. No componentwise strictly dominated action
   exists in that stated mechanical vector. This is NOT exhaustive integration
   over every finite PRNG state or a proof about whole-season/story utility.
3. **2,304 committed replies.** All nine answers across the three card families,
   both unknown/known-information contexts,128 seeds each. Correctly counts new
   opinion bands instead of new rows; all replies remain free. First-information
   outcomes have distinct sampled mechanical trade-offs. When the assessment is
   already known, deflect can lose to apologize, confronting gossip to slide or
   gossip-back, and refusing a plea to noncommittal. The finding is retained, not
   called a green all-context balance gate. Deal caps/unavailability and future
   strategic utility are not modeled by these nominal reply contexts.
4. **Demand is not a universal replacement for threaten.** The web reference
   `src/systems/contextual-action-generator.ts:511-540` supplies60/50/40 base odds
   and0/-5/-15 costs. Native `Negotiation` supplies1/1.5/2 successful hold weights;
   `StrategyRules` and the actual nomination reducer consume them. Expected
   nomination-reluctance changes in a neutral plain-trait case are21,21.25,13.
   That expectation is NOT nomination survival probability. Actual counterfactual
   nominations against other-candidate reluctance30 yield survival chances
   .6/.5/.4; at40,0/.5/.4; at50,0/0/.4. Each approach therefore has a tested niche.
   Both success/refusal branches execute and validate actual nominations. Broken
   safety promises settle against their maker; promises not broken remain active
   through their existing expiry. No arbitrary numerical rebalance was justified.

The matrix exposes context-specific weaknesses: tactics before its goal-lore
depth, exhausted information, and a rally with only two NPCs. It does not prove
that every such choice should be removed: possible refreshed information, future
rapport, story paths and deliberately antagonistic play are separate questions.
Nor does it grant final approval to them. E2 still needs the broader contextual
balance review, room/bargain/targeted-action coverage and human play evidence.

## Closed evidence

Artifacts under `D:/CodexGamesimEvidence/integration-20261004`:

- `wave-c-balance-01/actions.trx`:40/43. Three NEW harness expectations incorrectly
  called a spared safety promise fulfilled at initial nominations. Production
  keeps it active until its expiry; fixed the new expectation and also checked
  refused-branch settlement and the breaker. No production rule/old test changed.
  SHA256`53ec572a0d05337c1b901629d1ee2aea408b3efd24560cce2f4038e2bc137b15`.
- `wave-c-balance-02/actions.trx`:79/79 after adding the rapport context. SHA256
  `fde0a8df1a20344da7d4aecc9ef7cd57e297d1b41f6eb53d2bf453885f8a5aa7`.
- `wave-c-balance-full-03/full.trx`:2010 executed passes,2023 discovery (13 explicit
  reports unexecuted). Predates the exact frontier regression. SHA256
  `6e6549c0a7a8cc2a88e98d1ba770ff39e5c08bf1312c3e5516b22dd611d894b6`.
- Final `wave-c-balance-full-05/full.trx`:**2011/2011 executed passed**, zero
  failed;2024 discovery includes13 unexecuted explicit reports, not2024 passes.
  Started10:22:22.3041035UTC, finished10:23:46.4534243UTC. Root session73857 closed0.
  SHA256`95c5453aa66e75b6b33e7c80c51897350cb562ce6b7398b60e7458b452c433a1`.
- Final `wave-c-balance-offline-02.log`:**5/5 fresh NoUMA assemblies compiled**,
  zero errors; explicit WindowsPowerShell5.1, idleNoUMA references only. Root54876
  closed0. SHA256`085426b985292be17e53cbd8891a89dbf0c1ffba203ca082e71eaa4e596896e1`.
  Output `C:/Users/kelli/AppData/Local/Gamesim/offline-compile/7d270b60ceb640b7ad84cbbd246eda78`.

Pure execution and metadata compilation are not native Edit/Play execution,
standalone correctness,1080p60 performance, final art or human acceptance. Root
manual review only; independent reviewers remain unavailable. No live promotion,
native-cache write, remote operation, normal save mutation or cleanup occurred.

## Next dependencies

- Retain the contextual E2 findings and extend/fix only behavior justified by the
  complete action contract; do not tune away a real call-in niche.
- E4 player-HoH pitch cards/free Feel them out, E5 bounded speech influence;
  all four required Wave D systems, coherent real assets and preservation-aware
  final integration remain required.
- Finish the separate c272 native run, then same-source NoUMA/build verification.
  Later final merged-candidate runtime/build/profile acceptance must be fresh.
  Actual1920x1080 at60FPS GTX1060 and human E1-E5 remain open.

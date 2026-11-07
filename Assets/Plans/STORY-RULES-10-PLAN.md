# Story rules 10: who the house blames (the pariah fix)

Design note SR10-0, 2026-11-07. It sets out the pariah fix (Q5): what causes it, the rule change, how it is versioned so
that every existing season plays exactly as it does today, how it is measured and accepted, and what it hands on.

**The owner's words, 2026-11-07.** Verbatim: "Work on story rules 10 while the UMA run waits". Story rules 10 was
reserved for the story session's pariah fix (BALANCE_BASELINE.md:367, ROLE-MOMENTS-PLAN decision 2). No story session
is active, so the lead builds it. Every open question below takes the recommended default as a delegated call, on the
owner's standing "Yes your judgment on questions and proceed". The owner signs off the numbers in the PR before it
merges.

**Evidence base.** Branch `claude/story-rules-10`, cut from `claude/b9-balance` at `23b04e46` (B9, PR #46, still a
draft). Nothing here has been run yet: Codex holds the machine. Every figure for a changed rule is an estimate until the
lab runs (§5). The figures for today's seasons are counted from the archived 100-seed tuning-0 headline at `26b65f09`
(`D:/ClaudeScratchArchive/2026-10-27-session-4edb7311/lanes/arcs2-lab/after/part-*.json`: 12 policies, 100 seeds, at 8
and at 12) and from B9's studier runs (`D:/ClaudeScratch/b9/r3/runs/M8F-*`). Two independent readers recounted the
load-bearing ones.

Paths are relative to `Assets/Gamesim/` unless they start at the repository root.

**Names.**
- *A pariah run (D0, the acceptance measure)*: some houseguest, not the player and not the reigning Head of Household,
  is held a grudge of 40 or more by three or more houseguests still in the house, at two Advances running
  (`Tests/EditMode/BalanceLab.cs:348-366`, `Tests/EditMode/StoryPacingTests.cs:84-91`). A *pariah season* has one. The
  target is at most 20% of seasons (`Assets/Plans/StoryArcs/20-story-arcs-brainstorm.md:1025`; the pacing table's
  column, `Tests/EditMode/BalanceLabReports.cs:754`).
- *DP*: the same rule, counted only while the player is still in the house.
- *A fresh 40*: a replacement-veto grudge written this week. The replacement resents the veto holder who forced them
  onto the block, at exactly 40 (`Simulation/Story/EpisodeEngine.StoryHooks.cs:42-43`). It sits on the line for one
  week, then fades to 38.

---

## 0. The short version

- **Today.** 16.3% of seasons at 8 and 45.1% at 12 have a pariah (all policies, 100 seeds, tuning 0). The studier at
  B9's tuning 1: 14.6% and 47.2%. The twelve misses the 20% target by far.
- **The cause** (§1).
  - A Head of Household's two nominees, and the replacement if the veto is used, resent them at 70 (84 between allies).
    Those grudges stay above 40 for the rest of the season.
  - The usual third holder is a fresh 40: next week the past Head of Household wins the veto, uses it, and the
    replacement resents them. That route is 61% of first pariah runs at 8 and 53% at 12.
  - The twelve is worse because it has twice the chances (5.4 used NPC vetoes a season against 2.6), not because the
    rule ignores house size.
- **The fix, story rules 10 (`StoryRules.Blame`)** (§3).
  - A houseguest who saves themselves with the veto forced nobody onto the block. The Head of Household chose the
    replacement, and the replacement already resents the Head of Household. So the replacement writes no grudge against
    the veto holder.
  - A veto used to save somebody else still stings, at 35: half the Head of Household's 70, and under the 40 line the
    house's gang-ups read.
  - Only if the screen shows it is needed: two "settling scores" rules between houseguests (C2, §3.2).
- **Versions** (§4). Every change is keyed to story rules 10. Seasons at 0-9, every save and every import replay byte
  for byte. No schema bump, no new field, no new value outside the closed vocabulary schema 30 already admits.
- **Measurement** (§5). A control set at story rules 10 with today's numbers must replay today exactly. Then C1, then C2
  only if C1 is not enough. The control pass doubles as B9's deferred tuning-1 headline.
- **A finding the owner should see** (§1, decision 1). Three quarters of first pariah runs come after the player has
  been evicted, when the story system has already stopped telling the house. While the player is in the house, today's
  rates are 3.6% at 8 and 12.0% at 12, already under 20%. This note keeps D0, the plans' own measure, as the target, and
  reports DP beside it.

## 1. What makes a pariah

**When the first run comes, relative to the player's eviction** (today's headline, 1,200 seasons per house):

| house | first runs | after the player's eviction | in the player's eviction week | while the player is in the house | DP rate (Wilson 95%) |
|---|---|---|---|---|---|
| 8 | 195 (16.3%) | 145 | 7 | 43 | 3.6% [2.7, 4.8] |
| 12 | 541 (45.1%) | 391 | 6 | 144 | 12.0% [10.3, 13.9] |

- After the player's eviction the story system tells nothing more: `AirtimeFor` returns false
  (`Simulation/Story/EpisodeEngine.Story.cs:241`), nothing new is cast (`:484`), and open stories end "left-house"
  (`:1020-1023`). The pile-on arc, the one story that reads a pariah, cannot fire then either.
- DP by policy, out of 100 seasons: at 8 no policy is above 7; at 12 oracle-skilled 23, oracle-reader 19, exploit 16,
  social 15, the rest 14 or below.
- The studier: 5.5% and 28.5% at tuning 0, but 0.5% and 1.5% with the studier in the house; at tuning 1, 14.6% and
  47.2%, and 4.2% and 7.9% in the house. Most of B9's rise is the studier being evicted earlier.

**The route.**
- Every pariah target had won a Head of Household (BALANCE_BASELINE.md:1225-1232). A nomination or replacement grudge of
  70 or 84 never falls below 40 inside a season: it loses 2 a week (`Simulation/Story/Grudges.cs:102-112`), and a season
  has at most 9 week turns.
- NPC veto holders who are not the Head of Household use the veto 91% of the time. So nearly every NPC reign leaves two
  lasting holders: the saved nominee and whoever survives the block.
- 159 of 195 first runs at 8 (82%) and 400 of 541 at 12 (74%) include a replacement-veto holder. In 119 (61%) and 286
  (53%) that row is a single write of exactly 40: a fresh 40. The other two rows are mostly 68 + 68, written one week
  turn earlier, so the run follows the target's own reign by a week. With the player in the house the mix is the same
  (36 of 43 at 8; 88 of 144 at 12).
- The twelve has twice the chances: 6.76 NPC reigns a season against 3.45, and 5.40 used NPC vetoes against 2.63. Per
  chance the rate is about the same (5.0% of used vetoes at 8, 6.2% at 12).
- The other runs: at 12, 141 have no replacement-veto row. About 45 of them are second reigns. The rest have a third
  holder from an allied vote against (40 or 48), stir-the-pot's 45 or a broken word.

**What this rules out.**
- Scaling the holder count by house size: the twelve falls only to about 34-38%, and the eight does not move.
- Faster decay or smaller grudges scaled by house size: the lasting pair is recent (3 week turns old or less in 271 of
  286 fresh-40 runs at 12), so these levers reach only the tail. They also undo the store's stated design, that "you put
  me up in week one" is a season-long memory (`Simulation/Story/Grudges.cs:96-101`).

## 2. Decisions

Each row is the default adopted under the delegated call. Each stays open to the owner in the PR.

| # | Decision | Default adopted | If the owner changes it |
|---|---|---|---|
| 1 | The acceptance measure | **D0, unchanged**: the plans' own rule, at most 20% at 8 and at 12, on all 12 policies at tuning 1, budget 0. DP, D1 (two eviction nights running) and D2 (holders scaled by house size) are printed beside it, never instead. | Judge on DP: today's seasons already meet it (3.6% and 12.0%), and C1 only lowers it further. C2 would not be needed. |
| 2 | The twelve's bar | 20%, as the pacing table says. The alumni report's "pariah-free at least 45%" for the twelve (`Tests/EditMode/BalanceLabAlumniReport.cs:173`) is not used. | A bar scaled to the weeks a pariah can happen in is about 36% at 12. |
| 3 | Whose veto the rule covers | Everyone's, the player's included: one rule. | C1n: NPC holders only. It is the fallback if B4 rises above 28%. |
| 4 | The value for a veto used on somebody else | 35, "half the seventy". | 38 or 39 also sits under the line; they keep more of the old weight in the jury and vote terms. |
| 5 | C2, settling scores | Built only if the screen shows C1 alone leaves the twelve above 18% (§5). Houseguest targets only: the player's grudges already settle through the player's own stories. | Ship it anyway, for the story, or never. |
| 6 | C3, faster fading of ceremony grudges between houseguests | Not built. It reverses season-long memory, so it needs the owner's word. | Taken only if C2 still misses at 12. |
| 7 | The pile-on arc becomes rarer | Accepted and reported. | - |
| 8 | The name | `StoryRules.Blame`. | Any name; it is one constant. |
| 9 | Define and enrol | One PR, two commits: define with `Current` held at Threads, then enrol by moving `Current`, as R7's plan does for Roles. | Keep 10 defined but unenrolled until R7. |
| 10 | The ledger's -15 after a veto (`Simulation/EpisodeEngine.cs:1080`) | Left as it is. It belongs to the relationship ledger, not the story system, and no pariah reads it. | - |
| 11 | The control pass | It doubles as B9's deferred 100-seed tuning-1 headline (B9 owner call 4), once the leak proofs pass. | Run the headline separately. |

## 3. The rules

### 3.1 C1: the veto's grudges (built)

`StoryReplacement(s, replacement, saved)` in `Simulation/Story/EpisodeEngine.StoryHooks.cs`. `ResolveVeto` already knows
who was saved when it writes the grudge (`Simulation/EpisodeEngine.cs:1058-1075`; the NPC holder saves themselves when
they are on the block, `:880`; the player's command names the saved houseguest).

- The replacement's 70 (84 between allies) against the Head of Household is unchanged.
- Below story rules 10: the replacement resents the veto holder at 40, as today.
- At story rules 10 or above:
  - when the veto holder saved themselves, the replacement writes no grudge against them;
  - when the veto saved somebody else, the grudge is 35.

What 35 keeps and loses:
- Below 40, so it no longer counts for the pariah, the pile-on cast, `GrudgeRefusesAlliance`, `NpcAlliances`'
  grudge check, the Confront target or the bad-blood cast.
- Still read where the line is lower: the jury term (30 or more) for 3 week turns, the Game Sense note and `HasRival`
  (20 or more).
- The vote term moves from -10 to -8.75, the nomination and save terms from -12 to -10.5; a second grudge stacks 17.5
  instead of 20.

### 3.2 C2: settling scores (only if the screen calls for it)

Two story-true settles between houseguests. Neither raises a grudge, touches its cause, week or count, draws a roll or
writes a log line. `Grudges.Settle(state, holder, target, atMost)` sets the severity to at most the given value.
- **Even.** A Head of Household who nominates, or names as the replacement, somebody they resent settles to 30: they
  have had their revenge.
- **Saved.** A houseguest pulled off the block by another houseguest's veto settles their grudge against the saver to
  10. The store's forgiveness rule then removes it at the next week turn when they like the saver (score above 40,
  which the veto's +25 usually gives).
- The houseguest's mood line ("Angry at X", `Simulation/EpisodeEngine.cs:1021-1041`) skips ceremony grudges under 40 at
  story rules 10, so it never names somebody the houseguest has settled with. Text only.

### 3.3 Not adopted

- Kept ("a grudge eases once acted on" through the vote) and Old news: a large blast radius for a small gain.
- Holder counts or grudge sizes scaled by house size: no reason in the story, and weak (§1).
- The replacement's grudge against the Head of Household from 70 to 40: the replacement resents the one who chose them
  most (21 §7, D-B).

## 4. Versions

- **The constant.** `StoryRules.Blame = 10` (`Simulation/Story/StoryState.cs`). `IsDefined` becomes
  `0 <= version <= Latest`: the ladder has no gap any more. `Latest` stays `Roles` (11).
- **The gates.** Every change reads `StoryAt(s, StoryRules.Blame)`, a `>=` test. So:
  - seasons at 0-9 replay byte for byte, and so does every save: the migration writes a literal 9
    (`Runtime/Persistence/EpisodeSaveMigrations.cs:542`), and so does the web importer (`Runtime/Persistence/WebSaveImporter.cs:211`);
  - Roles (11) plays story rules 10 as well, as the ladder requires.
- **Schema 30 only, automatically.** The live validator accepts only schema 30 (`Simulation/EpisodeValidation.cs:88`).
  The frozen schema-29 reader keeps refusing 10 with "Unsupported story rules version." No frozen reader is edited
  (ALL-STAR-HISTORIES-PLAN 8.2).
- **No schema bump, no new field.** The causes stay `replacement-veto` and so on; 35, 30 and 10 sit inside the
  validator's 0-100.
- **Held, then enrolled.** The define commit leaves `Current` at Threads, so no shipped path reaches 10. The enrol commit
  (§8) moves `Current` to `Blame`, so every new season starts on it.

## 5. Measurement

**The scratch copy.** As B9 did: a copy of the branch on D: (`D:/ClaudeScratch/sr10/snap`), never committed, always
run through `cpu-lock2.sh`.
- `Current = 10` there, so every lab season plays 10.
- Knobs read once per process: `SR10_SELF` (a self-save writes nothing), `SR10_RV` (the value for a veto used on somebody
  else), `SR10_EVEN`, `SR10_SAVED` (C2), `SR10_VETO_SCOPE` (all or NPC holders), `SR10_HEAT` (C3). Their description is
  appended to the parts stamp, so parts from different sets never merge.
- Counterfactuals recorded on the control pass, outside the hashed rows: CF1 ignores a fresh 40; CF2 also ignores a
  holder who has had their revenge or been saved by the target. They estimate C1 and C2 before either is played.

**The committed lab** (built in the define commit) can play any cell at a defined story rules version from Threads
on, other than Current (`BALANCE_STORY`, a `/story10` suffix on the house label; after enrolment the same switch holds
a cell at 9), and records DP, D1, D2, the holders' rows and the run week's veto. None of it enters a hashed row: `BudgetNoughtRows` (`5481eec784acd1ed`), `BudgetNoughtTuning1Rows`
(`1ee55c2a92571363`) and the 48 goldens do not move.

**The sets, in screen order** (estimates unverified):

| Set | Knobs | 8 (D0) | 12 (D0) | Role |
|---|---|---|---|---|
| C0, the control | rules 10, every knob at today's value | 16-18% | 45-48% | Must replay today's seasons exactly (the leak proofs). Records D0, DP, D1, D2, CF1 and CF2. Doubles as B9's tuning-1 headline. |
| C1 | self-save writes nothing; 35 otherwise | 5-8% | 16-22% | The rule built here. |
| C2 | C1 + Even 30 + Saved 10 | 4-7% | 12-18% | Only if CF1 or C1's screen is above 18% at 12. |
| C1n | C1, NPC veto holders only | about C1 | about C1 | Only if B4 rises above 28%. |
| C3 | C2 + ceremony grudges fade 6 more a week | 4-6% | 9-15% | Only with the owner's word, if C2 misses. |

**The decision rule.**
1. In the control pass, read CF1 and CF2 at 12.
2. If CF1 is at most 16%, screen C1 only; otherwise screen C1 and C2.
3. Adopt the smallest set whose paired screen at 12 is at most 18% (headline seeds 0-39, McNemar against C0), and whose
   studier check moves B1-B4 by less than 2 points.
4. If C2 is still above 20% at 12, stop: C3 or the measure goes to the owner.

**The order of runs**, one background job at a time per slot:
1. The build (82 s), then the two leak proofs at rules 10 with every knob at today's value: `BudgetNoughtTuning1Rows`
   must give `1ee55c2a92571363`, and `BudgetNoughtRows` `5481eec784acd1ed` (about 5 minutes each).
2. C0: the headline, seeds 0-99, in five parts of 20 on both slots (B9's `headline.sh` pattern), and the studier at a
   fixed 0.5, competition tier, seeds 0-199.
3. The screen: C1 (and C2 if called for) on headline seeds 0-39, with the studier check.
4. The finalist on seeds 40-99, then the acceptance on seeds 100-199; the competition tier at 800 seeds (B1-B4) and the
   war rooms at 480 (B5-B7); `StoryPacingTests.PacingReport` at 9 and 10.

About 10-14 hours of one core, about half on both slots. **Nothing starts while Codex holds the machine**, and the lab
gives way to the UMA PlayMode run: B9's round 9 goes first, and a lab part never starts while a Unity test run holds the
lane, because CPU jobs beside it stretch PlayMode frames.

**Analysis.** `sr10.py`, on B9's `bands.py` (`load`, `wilson`, `mcnemar`, `side`). Each set against C0, paired by
policy, size and seed, on: Q5 under D0, DP, D1 and D2, by house and by policy; the decomposition (the fresh-40 share,
self-saves, causes); the pacing table; B1-B7, Q4, bitter jurors, NPC pacts and deals, the pile-on rate; and the share
of paired seasons whose NPC outcomes changed.

## 6. Acceptance

- **Q5 (D0) at most 20% at 8 and at 12**, on headline seeds 0-199 (2,400 seasons a house), quoted with its Wilson
  interval, and a significant McNemar against C0. DP, D1 and D2 printed beside it.
- **B1-B7 still in their bands** (B9-BALANCE-PLAN, As built). B4 at most 28%, or the switch to C1n.
- **The pace at 8 still in its bands:** budgeted asks 4-6, weeks with a card 30-60%, stories finished about 3, NPC
  removals at most 10%, showmances 0.5-1.5, pile-ons at most 25%.
- **Byte for byte:** the committed constants replay the scratch rows of the chosen set (headline seeds 0-19), and both
  BudgetNought digests are unchanged by the define commit.
- **The suites:** the dotnet subset against the floors in `Tools/baseline.txt`; the offline EditMode compile and the
  Mono kit for the Role, Frozen, Persistence and StoryMechanics fixtures; the UMA PlayMode set as the only Unity gate.

## 7. Tests and pins

**Define commit.**
- `Tests/EditMode/StoryMechanicsTests.cs`, each with a rules-9 twin: a self-save forces nobody; a veto for somebody else
  stings at 35 (out of the 40 count, still read by the jury term); the player's veto follows the same rule; a past Head
  of Household's self-save makes no third holder (3 holders at 9, 2 at 10); Roles plays the Blame rules; a save at
  story rules 10 round-trips at schema 30.
- The pins that named 10 as undefined: `RoleRulesTests` (the reserved-10 message, the doc, the `IsDefined` cases),
  `FrozenEpisodeV29IntegrationTests` (10 stays refused by Frozen29 in its own words, while the live validator accepts
  it), `DialogueCampaignRulesTests` (every defined version).
- The lab: the part round trip keeps the new fields; DP is false once the player is out.

**Enrol commit.** §8.

## 8. Enrolment (the second commit, after acceptance)

- `StoryRules.Current = Blame`; `RoleRulesTests` and `ShippedRulesTests` (9 becomes 10).
- Held at Threads: `CommitmentRulesSeasonDigests`, `PinnedVoteSeason`, the below-30 list (the frozen 28 and 29
  integration tests, the schema-29 and schema-30 observers, `DialoguePreviewWitness`, `UnifiedVotePublicCommandTests`,
  `FrozenEpisodeV25ContractTests`, `FreshSeasonStateDigestTests`), `RoleStoryPools.OldSeasons` and `StoryMechanicsTests`'
  rules-9 helpers.
- The lab: `BalanceLabGoldens` and the 48 recorded digests re-recorded, with the reason; the BudgetNought cells held at
  story rules 9 through the cell's story field, with a rules-10 twin; `TuningRulesTests.StudierTuning1Rows` held or
  re-recorded. The parts stamp needs no second bump: the define commit moved it to v9, and the season's story rules
  version is part of every cell's stamp entry already.
- A new old-save cell at story rules 10 (`Tools/OldSaves`).
- BALANCE_BASELINE's Q5 row and the story IMPLEMENTATION-LOG updated. The role-moments session is told: R7 can follow.

## 9. What moves for the player

Under C1 (all estimates until the screen):
- A player who saves themselves with the veto is no longer resented by the replacement. There is no Game Sense "-3"
  note, no jury bitterness from that row, and no one-week refusal of an alliance.
- A veto used on somebody else costs 35: it falls under the jury's 30 after 3 week turns instead of 5, and under the
  note's 20 after 8 instead of 10.
- Past Heads of Household are ganged up on less often after a veto week, and the pile-on arc becomes rarer.
- Expected: B3 and B4 +0 to +2 points, B1 and B2 about 0, Q4 +0 to +0.02.

C2 would add only change between houseguests. Past NPC Heads of Household survive a little longer, so the player is a
little more often a nomination pick (Q4 up a little), and NPC pacts are a little more common.

## 10. Risks

- **The twelve may land near the line under C1.** Hence the counterfactuals and C2.
- **Feedback.** Settled houseguests vote and nominate more softly, so past Heads of Household survive longer, and some
  reign twice. More NPC pacts mean more allied votes against.
- **B4 has 3.2 points of headroom.** The fallback is C1n.
- **The pile-on arc may become near-dead content.**
- **Roles (11) seasons change at define.** Only tests opt into 11, but several walk past a used veto: the week-two
  walks of `RoleVetoBoardTests.WeekTwoVetoes` (seeds 7101-7160) and everything built on them (`RoleKeepTheBlockTests`,
  the two Role persistence suites, the keep-the-block seasons of `FrozenEpisodeV29IntegrationTests`), and the 11 full
  seasons of `RoleStoryMomentsTests.SeasonsUnderTheRoleMomentsKeepTheirMomentsInBounds`. Their seed searches and floors
  are re-run first; any re-pin is recorded as a delegated call.
- **The runs wait for the machine:** Codex's hold, B9's round 9 and C:'s free space.
- **The self-save share is unmeasured** until the control pass records it.

## 11. For the owner

1. **The measure.** D0 (whole season) stays the target. DP (while you are in the house) is 3.6% and 12.0% today,
   already under 20%. Say if you would rather judge on DP.
2. **The rule.** A self-saving veto holder gets no grudge from the replacement; a veto used on somebody else costs 35,
   under the line.
3. **C2 and C3,** only if the screen needs them.
4. **The pile-on arc** becomes rarer.

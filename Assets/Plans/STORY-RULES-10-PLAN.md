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
load-bearing ones. Since then the lab has run (2026-11-08): the screen adopted C1n, and C1n passed acceptance on the
held-out seeds (§6, As accepted). The estimates further down are kept as they were written, and say so.

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
  - **As accepted, C1n** (decision 3): both rules cover houseguests who hold the veto. The player's own veto is
    unchanged: the replacement resents the player at 40, whoever the player saved. The screen chose C1n because C1, the
    same rule for every holder, moved the studier's B4 to 29.0%, above its 28% bar.
  - Only if the screen shows it is needed: two "settling scores" rules between houseguests (C2, §3.2). Not built: the
    smaller set passed the screen, so C2 was never adopted (§6, As accepted).
- **Versions** (§4). Every change is keyed to story rules 10. Seasons at 0-9, every save and every import replay byte
  for byte. No schema bump, no new field, no new value outside the closed vocabulary schema 30 already admits.
- **Measurement** (§5). Seeds 0-99 are for development, seeds 100-199 for acceptance (the owner's decision, §11).
  - Development: a control set, C0, at story rules 10 with today's numbers must replay B9's seasons exactly (the leak
    proofs, against digests printed by B9's own head). C0 plays all 12 policies on seeds 0-99. The screen tries C1 on
    seeds 0-39, and C2 when rule 2 (CF1 above 16% at 12) or rule 3 (C1 above 18% at 12, or its studier moving B1-B4
    2 points or more) calls for it. The set it picks plays seeds 40-99 as the finalist.
  - Acceptance: seeds 100-199 are frozen and held out. The unchanged control and the adopted set both play them, all
    12 policies, at 8 and at 12, paired by seed. Only the set the screen adopted plays them; another set needs the
    owner's word.
  - C0 on seeds 0-99 doubles as B9's 100-seed headline only when the three conditions of §5 hold: this branch holds
    B9's final head, the leak proofs match the digests B9's own head printed, and B9's own rows match C0's.
- **Acceptance** (§6). D0 at most 20% at 8 and, separately, at most 20% at 12, pooled across the 12 policies, on seeds
  100-199 only, each with its Wilson interval and every policy's rate beside it. DP, D1 and D2 are reported, not gating.
  - **Passed, 2026-11-08, by C1n** (§6, As accepted): 3.9% [3.0, 5.2] at 8 and 17.6% [15.5, 19.8] at 12, against the
    control's 19.1% and 49.0% on the same seasons. B1-B7 and the pace at 8 are in their bands.
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
| 3 | Whose veto the rule covers | Everyone's, the player's included: one rule. **Resolved to C1n by the screen (2026-11-08):** C1 met the screen's Q5 rule (14.2% at 12 on seeds 0-39) and moved B1-B4 by under 2 points, but its studier's B4 was 29.0% on competition seeds 0-199, above 28%, which is this row's fallback. C1n's B4 there was 28.0%, and C1n passed acceptance (§6, As accepted). So the rule covers houseguest holders; the player's veto keeps the 40. | C1, the same rule for the player: it would need the owner's word, and it was not played on the acceptance seeds. |
| 4 | The value for a veto used on somebody else | 35, "half the seventy". | 38 or 39 also sits under the line; they keep more of the old weight in the jury and vote terms. |
| 5 | C2, settling scores | Screened when rule 2 (CF1 above 16% at 12) or rule 3 (C1 above 18% at 12, or its studier moving B1-B4 2 points or more) calls for it (§5). Built into the committed rules only if adopted: C1 misses rule 3 and C2 passes it. Houseguest targets only: the player's grudges already settle through the player's own stories. | Ship it anyway, for the story, or never. |
| 6 | C3, faster fading of ceremony grudges between houseguests | Not built. It reverses season-long memory, so it needs the owner's word. | Taken only if C2 still misses at 12. |
| 7 | The pile-on arc becomes rarer | Accepted and reported. | - |
| 8 | The name | `StoryRules.Blame`. | Any name; it is one constant. |
| 9 | Define and enrol | One PR, two commits: define with `Current` held at Threads, then enrol by moving `Current`, as R7's plan does for Roles. | Keep 10 defined but unenrolled until R7. |
| 10 | The ledger's -15 after a veto (`Simulation/EpisodeEngine.cs:1080`) | Left as it is. It belongs to the relationship ledger, not the story system, and no pariah reads it. | - |
| 11 | The control pass | C0 on development seeds 0-99 may stand in for B9's 100-seed, all-policy headline (B9 owner call 4) only when this branch holds B9's final head and the three conditions of §5 hold. Otherwise B9's headline runs on B9's branch. | Run the headline separately. |

## 3. The rules

### 3.1 C1n: the veto's grudges (built)

`StoryReplacement(s, replacement, saved)` in `Simulation/Story/EpisodeEngine.StoryHooks.cs`. `ResolveVeto` already knows
who was saved when it writes the grudge (`Simulation/EpisodeEngine.cs:1058-1075`; the NPC holder saves themselves when
they are on the block, `:880`; the player's command names the saved houseguest).

- The replacement's 70 (84 between allies) against the Head of Household is unchanged.
- Below story rules 10: the replacement resents the veto holder at 40, as today.
- At story rules 10 or above, for a houseguest veto holder:
  - when the veto holder saved themselves, the replacement writes no grudge against them;
  - when the veto saved somebody else, the grudge is 35.
- At story rules 10 or above, for the player as veto holder: 40, whoever the player saved, as below 10.

The define commit (`36ca4055`) built C1, the same rule for every holder, the player included. The screen adopted C1n
(decision 3), and the C1n commit narrowed the rule to houseguest holders: the player's branch writes the old 40 again,
exactly as the lab's `SR10_VETO_SCOPE=npc` knob played it on the acceptance seeds. It draws nothing, mints nothing and
logs nothing new.

What 35 keeps and loses (a houseguest holder's veto for somebody else; the player's veto is not changed):
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
- **No existing season is at 11 (checked 2026-11-07, at the owner's request).** A `>=` gate would change a saved season
  at Roles, so this was counted, not assumed:
  - no shipped path writes a version above `Current`: a new season takes `StoryRules.Current`, which was Threads at this
    check and is Blame since the enrolment (`5863d964`, §8) (`Simulation/Story/EpisodeEngine.Story.cs:75`), and the
    migration and the web importer write Threads (above);
  - the 132 episode files on the owner's machine (66 saves and their backups; Play writes to the same folder) were read
    without printing any name: every story rules value is 6 or 9, and none is 10 or 11;
  - no repository fixture holds a season at 11.
  So the only seasons at Roles are the ones tests build, and Roles is not enrolled. The Role walks that change at
  define are re-pinned as tests, not as saves (§10). If a season at 11 ever turned up before Blame enrols, Blame's
  gate would need to leave 11 out, and that would go to the owner first.
- **Schema 30 only, automatically.** The live validator accepts only schema 30 (`Simulation/EpisodeValidation.cs:88`).
  The frozen schema-29 reader keeps refusing 10 with "Unsupported story rules version." No frozen reader is edited
  (ALL-STAR-HISTORIES-PLAN 8.2).
- **No schema bump, no new field.** The causes stay `replacement-veto` and so on; 35, 30 and 10 sit inside the
  validator's 0-100.
- **Held, then enrolled.** The define commit leaves `Current` at Threads, so no shipped path reaches 10. The enrol commit
  (§8) moves `Current` to `Blame`, so every new season starts on it.

## 5. Measurement

**The seeds** (the owner's decision of 2026-11-07, §11). A season's seed is its house and its seed index.
- *Development seeds 0-99.* C0's full headline (all 12 policies, at 8 and at 12), the screen (C1, and C2 if called
  for, on seeds 0-39) and the finalist (the adopted set on seeds 40-99). Everything that is tuned or chosen is chosen
  here. The screen's decision rule below is a development rule.
- *Acceptance seeds 100-199.* Frozen and held out: nothing is tuned on them, and nothing is read on them before the
  screen has chosen. The unchanged control C0 and the adopted set both play them, all 12 policies, at 8 and at 12. The
  comparison is paired by seed, policy and size: McNemar per size, pooled across the policies. They are read for one
  set: `acceptance.sh` refuses a set the SCREEN line did not adopt, and a second set besides C0, unless its third
  argument is `owner` (on the owner's word only; lab.log records it). The VERDICT then names any other set that has read
  them, and says it needs the owner's word.
- *The combined 0-199 summary* may be printed. It is labelled as combined and is never the verdict.
- The studier and the war rooms keep their own tiers' seeds (the order of runs, below, and §6).

**The scratch copy.** As B9 did: a copy of the branch on D: (`D:/ClaudeScratch/sr10/snap`), never committed, always
run through `cpu-lock2.sh`. It is made from a named head of this branch (`make_snap.sh <head>`; the define commit
`36ca4055` when none is named), and every launcher takes the same head. The verification refuses a dirty tree or
another head, so the plan's edits are committed first and the new head is named to every launcher. A head that differs
from `36ca4055` only in `Assets/Plans/` keeps the define commit's digest literals and exact counts
(`tools/lab-head.sh`'s `code_as_old`); its snapshot id is its own. The snapshot id is in every part's `.ok` marker, and
the analysis reads only parts of the lab's current snapshot.
- `Current = 10` there, so every lab season plays 10.
- Knobs read once per process: `SR10_SELF` (a self-save writes nothing), `SR10_RV` (the value for a veto used on somebody
  else), `SR10_EVEN`, `SR10_SAVED` (C2), `SR10_VETO_SCOPE` (all or NPC holders), `SR10_HEAT` (C3). Their description is
  appended to the parts stamp, so parts from different sets never merge.
- Counterfactuals recorded on the control pass, outside the hashed rows: CF1 ignores a fresh 40; CF2 also ignores a
  holder who has had their revenge or been saved by the target. They estimate C1 and C2 before either is played.

**The committed lab** (built in the define commit) can play any cell at a defined story rules version from Threads
on, other than Current (`BALANCE_STORY`, a `/story10` suffix on the house label; after enrolment the same switch holds
a cell at 9), and records DP, D1, D2, the holders' rows and the run week's veto. None of it enters a hashed row: at the
define commit `BudgetNoughtRows`, `BudgetNoughtTuning1Rows` and the 48 goldens did not move (`5481eec784acd1ed` and
`1ee55c2a92571363` at `23b04e46`, the define commit's base). Since then B9's round 4 re-pinned the tuning-1 pin to
`a89a8120050e9460` (B9's final head `7ab18ab0`, `runs/expected-digests.txt`), and the enrolment re-recorded the 48
goldens and holds both budget-nought pins at story rules 9 (§8).

**The sets, in screen order** (estimates unverified):

| Set | Knobs | 8 (D0) | 12 (D0) | Role |
|---|---|---|---|---|
| C0, the control | rules 10, every knob at today's value | 16-18% | 45-48% | Must replay B9's seasons exactly (the leak proofs). Records D0, DP, D1, D2, CF1 and CF2. On seeds 0-99 it may stand in for B9's headline (below); on seeds 100-199 it is the acceptance control. |
| C1 | self-save writes nothing; 35 otherwise | 5-8% | 16-22% | The rule built here. |
| C2 | C1 + Even 30 + Saved 10 | 4-7% | 12-18% | Screened when rule 2 or rule 3 below calls for it. |
| C1n | C1, NPC veto holders only | about C1 | about C1 | Only if B4 rises above 28%. |
| C3 | C2 + ceremony grudges fade 6 more a week | 4-6% | 9-15% | Only with the owner's word, if C2 misses. |

**The decision rule** (development seeds only; it picks the set that goes to acceptance and is never the verdict).
1. In the control pass, read CF1 and CF2 at 12 (seeds 0-99).
2. If CF1 is at most 16%, screen C1 first; otherwise screen C1 and C2.
3. Adopt the smallest set whose paired screen at 12 is at most 18% (headline seeds 0-39, McNemar against C0), and whose
   studier check moves B1-B4 by less than 2 points (competition seeds 0-199, on the seasons both played). If C1 misses
   this, screen C2.
4. If C2 is still above 20% at 12, stop: C3 or the measure goes to the owner.

The screen reads only parts of the lab's snapshot: a set whose screen parts are of another snapshot counts as not
screened, and C0's development parts of another snapshot leave the SCREEN line "not decided".

**B9's 100-seed headline** (B9's owner call 4). C0 on seeds 0-99 doubles as B9's 100-seed, all-policy headline only
when all three hold:
- this branch has merged B9's final head, and the lab plays a snapshot of that merge;
- the leak proofs pass against the digests B9's own head prints. `runs/b9-head.sh <B9's head>` plays
  `BudgetNoughtPlaysTheSeasonsItPlayedBeforeTheDriver` and `BudgetNoughtPlaysTheSeasonsB9Measured` on a snapshot of
  B9's head and writes them to `runs/expected-digests.txt`. The digest tests must include one that plays the rules B9's
  final head ships: if that head ships a change under a new rule version (the jury tiebreak, for one) with its own
  digest test, that test joins the list. `b9-head.sh` refuses a B9 head that has a `BudgetNought*` test the list does
  not run. The literals in this note are B9's at `23b04e46` and serve the define commit, and heads that differ from it
  only in `Assets/Plans/`;
- headline seeds 0-29 are also played on B9's head, in three parts of 10 (the merge needs parts from seed 0), and
  compared with C0's rows, row for row (`runs/b9rows.py`; the row is what the digests hash). Seeds 20-29 are the new
  evidence: `BudgetNoughtPlaysTheSeasonsB9Measured` already hashes the headline grid on seeds 0-19 at B9's shipped
  tuning, so on 0-19 the rows add only the parts round trip.

Otherwise C0 is a development control only, and B9's headline is run on B9's own branch.

**The order of runs.** Every CPU part is its own step, under 30 minutes, through the gate (below).
0. Commit the plan's edits first: the verification refuses a dirty tree. Then name the new head to every launcher, in
   this order: `make_snap.sh <head>`; `verify/launch.sh <head>` (job sr10v); `lab-launch.sh <head>` (job sr10lab),
   which waits for the verification's build. A head that differs from `36ca4055` only in `Assets/Plans/` needs nothing
   more: its leak proofs want the define commit's literals and the verification its exact counts, 16630 and 9794.
1. For a head whose code differs from the define commit's (B9's final head merged, for one), first on B9's head
   (`b9-head.sh <B9's head>`): a snapshot, the check that the lab runs every `BudgetNought*` test it has, its build,
   the two digest tests (one step each) and headline seeds 0-29 in three parts of 10 with its merge.
2. The build (82 s), then the two leak proofs at rules 10 with every knob at today's value: each must pass and print
   the digest B9's head printed (one step each).
3. C0 on development seeds 0-99, in ten parts of 10 seeds, and the studier at a fixed 0.5, competition tier, seeds
   0-199, on both slots (`lab-launch.sh <head>`); then the B9 row comparison.
4. The screen: C1 (and C2 if called for, `screen-c2.sh`) on seeds 0-39 in four parts of 10, with the studier check.
5. Acceptance, only when the lead launches it after the screen (`acceptance.sh <set> <head>`, for the set the SCREEN
   line adopted): the finalist (the set on seeds 40-99); C0 and the set on the acceptance seeds 100-199, in parts of
   10, side by side; the set's competition tier on seeds 0-799 in parts of 200 (B1-B4); its war rooms on seeds 0-479 in
   parts of 40 (B5-B7). A new head or snapshot replays the development parts (`lab-launch.sh`) but never these: run
   `acceptance.sh` again on it, or the VERDICT stays incomplete.
6. `StoryPacingTests.PacingReport` at 9 and 10 is in no launcher: it cannot be cut into parts, and no timing of it
   exists. It runs as its own step once one is measured under 30 minutes. Until then the pace is read from the
   headline parts (§6).

**Steps and timings.** The owner's machine rules: every CPU part is one step under 30 minutes, and every step enters
the gate on its own. `yield-to-unity.sh` runs before each `cpu-lock2` slot (a Unity test run, Codex's request file,
`claude-held`, a stale gate alive file, Codex engine work) and again inside the slot. Nothing chains past a Codex
request. The stop file ends a job between steps. A part the same snapshot already played is skipped. Every launcher
first waits for Codex's window (`after-codex.sh`). Part sizes come from slot timings measured on B9's runs, with a
wide margin:

| Step | Size | Measured in one slot | Estimate |
|---|---|---|---|
| Headline part | 10 seeds, 240 seasons | BudgetNought's 564 seasons in 8 min 46 s to 14 min 26 s (0.9-1.5 s a season) | 4-6 min |
| Studier part | 200 seeds, at 8 and 12 | 800 seeds at 12 in 20.4 min, at 8 in 4.5 min | about 6 min |
| War-room part | 40 seeds | 8.7 and 12.3 min | at most about 12 min |
| Leak proof or digest test | one test | 8 min 46 s to 14 min 26 s | under 15 min |
| Build | - | 82 s | - |
| Verification: the dotnet subset | one run | 7 min 6 s to 10 min 46 s (B9 rounds 3-8) | 7-11 min |
| Verification: the by-name fixtures | ten fixtures, no reports | B9's by-name run 32.5 s; a part of the subset | under 11 min |
| Verification: the BudgetNought pins | one test each | 8 min 46 s to 14 min 26 s | 9-14.5 min |
| Verification: Goldens0 | two tests | 7.9 min (round 8) | about 8 min |
| Verification: Goldens300Size8First | one test | 3.5 min (round 8) | about 4 min |
| Mono kit, no-UMA part | compile, harness build, listers | 3.6 min (round 8) | about 4 min |
| Mono kit, fixtures part | nine fixtures | FrozenEpisodeV29Integration 23-32 s, PersistenceV30Migration 5-7 s, the two Role persistence fixtures 3-5 s each, StoryMechanics and DialogueCampaignRules 2 s each. RoleRules, RoleVetoBoard and RoleKeepTheBlock: no Mono timing; 0.2, 0.7 and 2.4 s of test time in round 8's dotnet subset | a few minutes |
| Mono kit, UMA part | compile, listers, warnings, integrity | 4.1 min (round 8) | about 4 min |

The headline parts were 20 seeds. At 10 the slowest measured rate stays five times under 30 minutes. B9's round-8 Mono
kit ran as one step for 37.5 minutes, 29 of them `OldSaveAcceptanceTests`, which is not in this list. This kit runs as
three steps (`mono-nouma`, `mono-fixtures`, `mono-uma`), each entering the gate on its own, so no step relies on the
three unmeasured Role fixtures staying short. In all about 7.5 hours of one core (development about 2, B9's head about
45 minutes, acceptance about 4.5), about half that on both slots. **Nothing starts while Codex holds the machine**,
and the lab gives way to the UMA PlayMode run: B9's round 9 goes first, and a lab part never starts while a Unity test
run holds the lane, because CPU jobs beside it stretch PlayMode frames.

**Analysis.** `sr10.py`, on B9's `bands.py` (`load`, `wilson`, `mcnemar`, `side`). It prints three labelled blocks:
the development seeds 0-99 (the screen's RULE lines and one SCREEN line), the acceptance seeds 100-199 (the pass and
one VERDICT line), and the combined seeds 0-199 last. In each, every set against C0, paired by policy, size and seed:
Q5 under D0, DP, D1 and D2, by house and by policy; the decomposition (the fresh-40 share, self-saves, causes); the
pacing table; Q4, bitter jurors, NPC pacts and deals, the pile-on rate; and the share of paired seasons whose NPC
outcomes changed. B1-B4 come from the studier parts. B5-B7 are a manual read of the war rooms' merged tables: the
war-room parts are listed by name, never loaded, and no band is checked there. The VERDICT reads "incomplete" until C0
and the set each have every acceptance season, played by the lab's current snapshot and one build, each with its own
knob line (C0 exactly today's values). The VERDICT line names the snapshot and head. A missing control is never a pass.
The report waits in the gate like any step (it reads about 10,000 seasons at acceptance), and `lab.done` quotes only a
report the same run wrote. A merge's `.from` (whose rows they are) is written only when every part merged carries the
snapshot's pass marker.

## 6. Acceptance

- **The pass: Q5 (D0) at most 20% at 8, and separately at most 20% at 12**, pooled across the 12 policies, on the
  acceptance seeds 100-199 only (1,200 seasons a house). Each is quoted with its Wilson 95% interval, and every
  policy's own rate and interval is reported beside it. C0 plays the same seeds, policies and sizes, and the paired
  McNemar per size, pooled across the policies, is reported with the verdict. It does not gate: the pass mark is the
  rate. Without C0's acceptance seasons the verdict is incomplete, never a pass.
- **Secondary, reported, not gating:** DP (while the player is in the house), D1 and D2, on the same seeds, with C0
  beside them.
- **Never the verdict:** the screen on seeds 0-39, the finalist on seeds 40-99 and the combined 0-199 summary.
- **B1-B7 still in their bands** (B9-BALANCE-PLAN, As built). B1-B4: the adopted set's studier at a fixed 0.5 on
  competition seeds 0-799, the seeds B9 read its bands on (seeds 0-199 were also the screen's side check). B5-B7: its
  war rooms on seeds 0-479, read by hand from the merged tables (`runs/<set>/results/warrooms.md`); the report checks no
  band there. B4 at most 28%, or the switch to C1n.
- **The pace at 8 still in its bands:** budgeted asks 4-6, weeks with a card 30-60%, stories finished about 3, NPC
  removals at most 10% (of seasons with a removal window), showmances 0.5-1.5, pile-ons at most 25%. Read on the
  adopted set's headline seasons at 8 on the acceptance seeds 100-199, with C0's on the same seasons beside each. The
  bands were set on `StoryPacingTests.PacingReport`'s population (StorySeason's default eight on both rosters, plus six
  and twelve), not the headline's twelve policies, so a miss C0 shares is the population's, not Blame's.
  `StoryPacingTests.PacingReport` at 9 and 10 (its own fixed 1,920 seasons) is the cross-check, once it can run as one
  step under 30 minutes (§5).
- **Byte for byte:** the committed constants replay the scratch rows of the chosen set on headline seeds 0-19 (the
  development seeds), and both BudgetNought digests are unchanged by the define commit: the digests B9's own head
  prints, `5481eec784acd1ed` and, since B9's round 4, `a89a8120050e9460` at B9's final head `7ab18ab0`
  (`runs/expected-digests.txt`; `1ee55c2a92571363` at `23b04e46`, before round 4). After the enrolment both pins are
  held at story rules 9 and print these two (§8).
- **The suites:** the dotnet subset against the floors in `Tools/baseline.txt`; the offline EditMode compile and the
  Mono kit for the Role, Frozen, Persistence and StoryMechanics fixtures; the UMA PlayMode set as the only Unity gate.

### As accepted (2026-11-08)

**The set: C1n** (`SR10_SELF=1 SR10_RV=35 SR10_VETO_SCOPE=npc`, every other knob at today's value): C1 for houseguest
veto holders, the player's veto at the old 40. Played by the lab's snapshot `f2a5bdc59dbf8655` of head `1685df59`, with
C0 on the same snapshot and build. Sources, all in the scratch lab (`D:/ClaudeScratch/sr10/runs/`, never committed):
`sr10-report.txt` (its SCREEN, ACCEPT and VERDICT lines and the tables quoted below), `acceptance.done` (complete, 9102 s,
2026-11-08 14:15), and `C1n/results/warrooms.md` (B5-B7, read by hand).

**The screen** (development seeds 0-39; it chose the set, it is never the verdict).
- C1: 14.2% [11.3, 17.6] at 12 (68/480; C0 44.2% on the same 480, McNemar 149/5), 3.5% at 8; B1-B4 moved +0.1, +0.5,
  -1.0, +0.5, but its B4 was 29.0%, above 28%.
- C1n: 13.8% [11.0, 17.1] at 12 (66/480; McNemar 149/3, p 2.05e-40), 3.5% at 8; B1-B4 moved +0.0, +0.4, -1.5, -0.5,
  B4 28.0%.
- The SCREEN line: "adopt C1n (13.8% at 12 on seeds 0-39, B1-B4 within 2 points, B4 28.0%): C1 passed rule 3 but its B4
  is 29.0%, above 28%, and C1n is the plan's fallback".
- C2 was not screened. Rule 2 asked for its screen beside C1's (CF1 at 12 was 17.6% on seeds 0-99, above 16%), but rule
  3 adopts the smallest set that passes, and C1, then C1n, passed it; so C2's screen could not change the adoption (the
  lab's log, `runs/lab.log`, at the first SCREEN line).
- The finalist (seeds 40-99, informational): 3.1% [2.0, 4.6] at 8 (C0 16.9%) and 16.1% [13.6, 19.0] at 12 (C0 46.7%).

**The verdict** (acceptance seeds 100-199, frozen and held out), as the report prints it:
> VERDICT: PASS C1n, Q5 under D0 on the held-out acceptance seeds 100-199 [snapshot f2a5bdc59dbf8655, the lab's; head
> 1685df59] (the 12 policies pooled, 1200 seasons a house): at 8 3.9% [3.0, 5.2] (47/1200), at most 20%; at 12 17.6%
> [15.5, 19.8] (211/1200), at most 20%.

Paired with C0 on the same seasons (McNemar per size, pooled across the policies; reported, not gating):
- at 8: C0 19.1% on the same 1200, b/c 182/0, p 3.26e-55;
- at 12: C0 49.0% on the same 1200, b/c 384/7, p 1.06e-103.

Every policy's own rate is in the report beside the pass. At 8 they run from 1.0% to 9.0% (oracle-skilled). At 12 they
run from 10.0% (beast) to 27.0% [19.3, 36.4] (social), and two policies are above 20% on their own 100 seasons: social
and random (24.0%). The pass is pooled, as decided (§11); these are reported, not gating.

**Secondary, reported, not gating** (the same seasons, C1n against C0): DP 0.8% [0.4, 1.4] at 8 and 4.7% [3.6, 6.0] at
12 (C0 4.8% and 16.7%); D1 0.0% and 0.5% (C0 0.0% and 1.2%); D2 3.9% and 14.4% (C0 19.1% and 39.8%). No first run of
C1n's has a fresh 40 (0 of 47 at 8, 0 of 211 at 12; C0's 82.1% and 69.4%). The self-save share §10 called unmeasured:
98.5% of the NPC vetoes used at 8 and 98.0% at 12 are self-saves (C0).

**B1-B4** (the studier at a fixed 0.5, competition seeds 0-799), as the ACCEPT line prints them: B1 62.4% [61.0, 63.8]
(3018/4836), B2 61.5% [60.3, 62.7] (3863/6277), B3 32.1% [29.0, 35.4] (257/800), B4 26.2% [23.3, 29.4] (210/800), all
in band (B4's is 18-28%).

**B5-B7** (war-room seeds 0-479, a manual read of `C1n/results/warrooms.md`; the report checks no band there).
- B5-B6, the Game Sense gap, gated reader minus random with B9's weights 0.2 / 0.6 / 0.2: 14.5 [12.9, 16.0] at 8 and
  12.6 [11.5, 13.7] at 12. Both meet: at least 10, the low end above 5.
- B7, accepted counters that carry (35-50%, pooled, the owner's call): 26 of 60 = 43.3% [31.6, 55.9], in band. At 8
  alone 14/25 = 56.0%, at 12 alone 12/35 = 34.3% (reported; the criterion is pooled). The interval is wide: there were
  only 60 accepted counters, and it runs past the band at both ends, so the band is met by the rate, not by the whole
  interval.

**The pace at 8** (acceptance seeds 100-199, the 12 policies, 1,200 seasons; C0 on the same seasons beside each): budgeted
asks 4.8 (C0 4.9; band 4-6); weeks with a card 51.5% (51.6%; 30-60%); seasons with an NPC removal 0.0% (0.0%; at most
10%); showmances 1.03 (1.03; 0.5-1.5); seasons with a pile-on 0.0% (0.4%; at most 25%); stories finished 4.5 (4.5;
"about 3", no band). The last is a miss C0 shares, so it belongs to the headline's population, not to Blame (above).
Side effects (reported): the Q4 nomination ratio is 0.84 at 8 and 0.86 at 12 under both sets; pile-on seasons at 12
are 0.4% (C0 2.2%), as decision 7 expected.

**Still open at this record.**
- The byte-for-byte item above, the committed rule replaying C1n's scratch rows on headline seeds 0-19, has not been
  run against the C1n commit. Its hunk is the lab's npc scope with the knobs at C1n's values. (Run since, on the
  enrolled head: all 480 seasons equal, §8, As built.)
- `StoryPacingTests.PacingReport` at 9 and 10 has not run (§5, step 6).
- The UMA PlayMode set (the suites, above) has not run on the C1n commit.
- The enrolment (§8) is the next commit. The owner signs off these numbers in the PR before it merges. (Made on
  2026-11-08: §8, As enrolled.)

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

**The C1n commit** (after acceptance, before the enrolment). The player's-veto test becomes
`ThePlayersVetoKeepsTheFortyUnderBlame`, the same two cases (a self-save, a save for somebody else) against the same
rules-9 twin: under Blame the replacement resents the player at 40 with the same cause, on the 40 line, and the twins'
streams, sequences and logs agree. The define commit's C1 branch for the player fails both cases. No other test pinned
the player's veto at Blame; the count is unchanged.

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

### As enrolled (2026-11-08)

**What `Current` reads.** A grep of `StoryRules.Current`, `Threads` and `Blame` in `Simulation`, `Runtime` and the tests
before the move (the rule that a `Current` constant is a bound, never a gate):
- Live code reads `Current` in one place, `EpisodeEngine.EnableStory`: the value a season switched on today starts at.
  Every rule reads the milestone that introduced it (`StoryAt(s, StoryRules.Threads)`, `StoryAt(s, StoryRules.Blame)`,
  `>= StoryRules.Roles`, the catalogue's `template.rulesVersion` floors, the dialogue pilot's `>= Threads`), so the move
  changes which rules a new season plays and nothing about a saved one.
- The frozen readers' `StoryRules.Current` (schema 24 and 25's validators, the schema-28 and 29 read-only copies) is
  their own frozen ladder, `Current = Threads`, not the live constant: they keep refusing 10. The save migration writes a
  literal 9 and the web importer `StoryRules.Threads`, as before.
- In the tests, `Current` is a bound where it appears (`ShippedRulesTests`, `StorySeasonTests`, the lab's
  `CheckedStoryRules`, which now admits threads and refuses Blame); the pins of 9 were `RoleRulesTests`,
  `ShippedRulesTests`' map, and the shipped-rules asserts listed below.

**Moved.** `StoryRules.Current = Blame`; its doc says it is a new season's value. `RoleRulesTests` and `ShippedRulesTests`:
9 becomes 10.

**Held at threads by name**, each through the mechanism it already held B9's tuning with (the field written after
`ShippedRules.ApplyFresh` or `EnableStory`, or the lab cell's story field):
- `CommitmentRulesSeasonDigests.Season` (the director and harness sets), and with it every tier that plays its seasons
  (mode one, the leaks, the war rooms, `AllWeekSeasonDigests`' 54 records); `PinnedVoteSeason.Fresh`, and with it the
  mode-two sweeps;
- the below-30 list: `FrozenEpisodeV28IntegrationTests`' lie walk, `FrozenEpisodeV29IntegrationTests`' dialogue route,
  `LegacyDigestSchema29ObserverTests.Fresh` (both modes), `DialoguePreviewWitness.Start` (`ShippedRulesTests`' chip
  comparison takes the int back, as it takes B9's), `UnifiedVotePublicCommandTests.Play(held: true)` (was `tuningHeld`),
  `FrozenEpisodeV25ContractTests.Fresh`, and `FreshSeasonStateDigestTests` (it asserts 10, then takes the int back to 9
  before it hashes, as it does B9's tuning);
- not on the list above, for the same reason: `LegacyDigestSchema27TestStates.Fresh`, the lawful witness the schema-30
  observer's tests and `RoleRulesTests`' frozen-reader controls take to their schema-27 and 28 views;
- `RoleStoryPools.OldSeasons`, all four seasons. `StoryMechanicsTests`' rules-9 twins were already held by name.

**Re-pinned**, because a shipped season's story rules moved from 9 to 10 and the test asserts them: the dialogue fixtures
`DialogueCampaignEngineTests`, `DialogueCampaignDealClosureTests`, `DialogueSchema29ShapeTests`, `DialoguePendingSaveTests`,
and in PlayMode `EpisodeDurableTransactionTests.DialogueCampaign` and `OfferBoard_WithoutRolesTheMeetingDrawsTheRowsItAlwaysDrew`
(their week-one meetings come before any veto is decided, so nothing else they read moves). `DialogueCampaignRulesTests`'
"story-version" case took one below a shipped season's version, threads until now; it names the milestone below the
pilot's floor, `Threads - 1`. `ASeasonAtStoryRulesTenRoundTripsThroughTheRealStoreAtThirty` asserts that the shipped
season starts at 10 where it opted in.

**The lab.**
- `BudgetNoughtPlaysTheSeasonsItPlayedBeforeTheDriver` and `BudgetNoughtPlaysTheSeasonsB9Measured` hold every cell at
  story rules 9 through the cell's story field (`Cell.AtStoryRules(StoryRules.Threads)`), and each asserts that every
  season it plays starts there. They print exactly the digests B9's own head `7ab18ab0` printed
  (`runs/expected-digests.txt`): `5481eec784acd1ed` at tuning 0 and `a89a8120050e9460` at tuning 1.
- Their rules-10 twins, recorded at the enrolment: `BudgetNoughtPlaysTheDriversSeasonsAtStoryRulesTen` (tuning 0),
  `63356f631a00ed8a`, 192 of its 564 rows moved from the held run; `BudgetNoughtPlaysTheShippedSeasonsAtStoryRulesTen`
  (tuning 1, the seasons the game ships), `ad1ca9f42d182824`, 209 moved.
- `TuningRulesTests.StudierTuning1Rows` is held, not re-recorded, and so is `StudierTuning0Rows`: both are B9's numbers,
  which B9 measured at story rules 9, so both arms are held there through the cell's story field and keep
  `7eb55ed0d28b2d5e` and `9b9bf8a15644b745`. Story rules 10's own studier seasons are measured in §6 (B1-B4).
- `BalanceLabGoldens`: the `Rules` tuple ends `story.rulesVersion=10`, and the 48 cells were re-recorded at the
  enrolment, because every fresh season plays Blame: all 48 moved (budget 0 and 300, at 8 and 12, the twelve players).
- The parts' stamp stays v9 (above).

**Not in this commit.** The old-save cell at story rules 10: from OLD-SAVE-ACCEPTANCE 2.3 on a cell is written and
recorded by the build that writes it, so its writer is this commit, and it is the next step. The PlayMode re-pins above
have not run (Unity only); the UMA PlayMode set is the gate.

### As built (2026-11-08)

**The commits** on `claude/story-rules-10` (not on main): the define commit `36ca4055` and its fixture fix `1685df59`;
C1n, `5dcd2b17`; the enrolment, `5863d964`; the old-save cell, `0d538392`; and this record. B9's head `7ab18ab0` is
merged below them.

**Held, re-recorded and re-pinned at the enrolment** (above, As enrolled; the enrolment's own runs):
- held at threads by name: the season digest tiers built by hand and every tier that plays their seasons, the below-30
  list and `LegacyDigestSchema27TestStates.Fresh`, `RoleStoryPools.OldSeasons`, both budget-nought pins (which printed
  B9's digests exactly) and `TuningRulesTests`' studier pins;
- re-recorded: `BalanceLabGoldens`' 48 cells, and the two new rules-10 budget-nought twins (`63356f631a00ed8a`, 192 of
  564 rows moved; `ad1ca9f42d182824`, 209 moved);
- re-pinned, 9 to 10: `RoleRulesTests`, `ShippedRulesTests`, the four dialogue fixtures, `DialogueCampaignRulesTests`'
  story-version case (to `Threads - 1`), the schema-30 round trip at 10, and in PlayMode two tests that have not run.
  Before the re-pins the whole subset failed 43 of 16657, every one a shipped-rules pin of 9; after them, and on the
  enrolment as committed, 16657 of 16657.

**The replay** (§6's byte-for-byte item, run on the enrolled head `5863d964`, the scratch lab's harness at
`D:/ClaudeScratch/sr10/enrol/replay`): the committed build plays the headline tier on development seeds 0-19 (24 cells,
480 seasons) exactly as the lab's C1n rows, every committed field equal, with no seasons missing on either side; only
the lab's own counters and the wall-clock stopwatches are left out. The same comparer against C0's rows parts 254 of
the 480 seasons and against C1's (every holder) 232, so the committed build plays C1n, not C1. The two budget-nought
pins, re-run on the committed bytes, print `5481eec784acd1ed` and `a89a8120050e9460`.

**The old-save cell** (`0d538392`, OLD-SAVE-ACCEPTANCE 2.12; that plan's 2.10 and 2.11 are the dialogue line's, on
another branch).
`s30-mode2-allweek-story10-tuning1-w2-hoh`, written and recorded by the enrolled head: the shipped Regular season of
eight at schema 30 on story rules 10 and tuning rules 1, saved at the first state of week 2's Head of Household (seed
3102, the first from 3101 with a veto the Blame rule decides before the save point and another after the load). In week 1
a houseguest who was not the Head of Household saved herself with the veto and a houseguest was named, who holds no
grudge against her; after the load the script plays a houseguest's self-save in week 3 (no grudge) and the player's three
self-saves (a forty each, C1n). 93 commands, season digest `892724e0bc1e4efd`. The control: the same commands with the
season forced to story rules 9 are all accepted and give another digest (`87a0539f5b2b4755`; `84f812dbf65bfe6a` with the
story rules put back to 10, parting first at checkpoint 26, the houseguest's veto in week 3). Two new
`OldSaveAcceptanceTests` cases hold the file to its stop point and rules and keep the control.

**The floors** (`Tools/baseline.txt`): `Gamesim.SimulationTests` 16657, as the C1n commit set it (16642 and the define
commit's 15); the cell adds no subset test, and no subset test enumerates the old-save folder. `Gamesim.EditModeTests`
9837: 9831 and the cell's six (its four per-fixture cases and its two own).

**Still open.** The UMA PlayMode set (the only Unity gate) and the two PlayMode re-pins; `StoryPacingTests.PacingReport`
at 9 and 10 (§5, step 6); a re-run of the 48 goldens with their pasted values (each was checked by script against the
run that printed it; the two rules-10 budget-nought twins were re-run after the review, below, and pass); the season-digest tiers of
`CommitmentRulesSeasonDigests`, which are `[Explicit]` and held at threads by name. The owner signs off the numbers in
the PR before it merges.

**R7 can follow now.** ROLE-MOMENTS-PLAN has R7's enrolment wait "for every milestone below Roles to be finished";
story rules 10 is defined, accepted, enrolled and has its old-save cell, and the ladder has no gap (`IsDefined` is
`0 <= version <= Latest`). Roles (11) plays the Blame rule too, as the ladder requires (§4), so when R7 moves `Current`
from `Blame` to `Roles` its seasons keep C1n. The role-moments session is to be told (§8, above), with this list.

**What R7 inherits** (written for R7's builder; ROLE-MOMENTS-PLAN is that session's to edit). ROLE-MOMENTS-PLAN still
says `Current` stays Threads until R7, that fresh seasons enrol only when R7 moves it, and that R7 re-records
`CommitmentRulesSeasonDigests`; all three predate this enrolment.
- `StoryRules.Current` is `Blame` (`Simulation/Story/StoryState.cs`), so R7 moves it from Blame, not from Threads.
- Pins of a new season's story rules at Blame or 10, which R7's move breaks and which move to Roles or 11 with it:
  `RoleRulesTests` (`Current` is Blame; the shipped season's version), `ShippedRulesTests` (the map's
  `story.rulesVersion 10` and the chip's Blame), `FreshSeasonStateDigestTests` (it asserts Blame, then hashes at 9), the
  dialogue fixtures `DialogueCampaignEngineTests`, `DialogueCampaignDealClosureTests`, `DialogueSchema29ShapeTests` and
  `DialoguePendingSaveTests` (`Is.EqualTo(10)`), in PlayMode `EpisodeDurableTransactionTests.DialogueCampaign` and
  `OfferBoard_WithoutRolesTheMeetingDrawsTheRowsItAlwaysDrew`, and `BalanceLabGoldens`' `Rules` tuple with its 48 cells
  (re-recorded). `FrozenEpisodeV29IntegrationTests.ASeasonAtStoryRulesTenRoundTripsThroughTheRealStoreAtThirty` is about
  a season at 10: it goes back to opting in at Blame by assignment, as it did before the enrolment.
- Held by name, never re-recorded: everything held at threads above (`CommitmentRulesSeasonDigests` and every tier that
  plays its seasons, `PinnedVoteSeason`, the below-30 list, `LegacyDigestSchema27TestStates.Fresh`,
  `RoleStoryPools.OldSeasons`, both rules-9 budget-nought pins, `TuningRulesTests`' studier pins), and the story-rules-10
  old-save cell, a saved season at 10.
- The two rules-10 budget-nought twins name Blame: once `Current` is past it they are held there through the cell's
  story field (`CheckedStoryRules` then admits Blame and refuses Roles) and keep `63356f631a00ed8a` and
  `ad1ca9f42d182824`, unless R7's own code moves the seasons they play, which is what they are there to show. A
  rules-11 budget-nought pin, if R7 wants one, is a twin of its own.
- The bounds keyed to `Current` (`ShippedRulesTests`, `StorySeasonTests`, `AllianceLeakTests`, the lab's
  `CheckedStoryRules`) move with it on their own.

**After the review** (2026-11-09). The two rules-10 budget-nought twins named no story rules (0, EnableStory's own), so
at R7 they would have played 11 under names that say ten and failed only on the hash. They now name Blame, and
`BudgetNought` plays a named version through EnableStory while it is Current and through the cell's story field once
it is not, asserting that every season starts on it. Re-run on that code with nothing else changed, both pass and
print the recorded digests (`63356f631a00ed8a`, `ad1ca9f42d182824`). §4, §5 and §6 now date the statements the
enrolment left stale (Current was Threads; the 48 goldens did not move; the tuning-1 pin before B9's round 4), §7's
define-commit list is whole again, and the list above is the R7 handoff.

## 9. What moves for the player

**As accepted, C1n.**
- The player's own veto is unchanged: the replacement resents the player at 40 whether the player saved themselves or
  somebody else, as below story rules 10. The Game Sense "-3" note, the jury bitterness from that row and the one-week
  refusal of an alliance are as they were.
- What moves is the house: a houseguest who saves themselves with the veto is no longer resented by the replacement,
  and one who saves somebody else costs 35. Past Heads of Household are ganged up on less often after a veto week, and
  the pile-on arc becomes rarer (pile-on seasons at 8 0.0%, C0 0.4%; at 12 0.4%, C0 2.2%; §6, As accepted).
- Measured (§6, As accepted): B1 62.4%, B2 61.5%, B3 32.1%, B4 26.2% on competition seeds 0-799; at the screen,
  against C0 on seeds 0-199, B1 +0.0, B2 +0.4, B3 -1.5, B4 -0.5 points. Q4's nomination ratio is 0.84 at 8 and 0.86 at
  12, as C0's.

Under C1, the define commit's rule (estimates written before the screen, kept as they were):
- A player who saves themselves with the veto is no longer resented by the replacement. There is no Game Sense "-3"
  note, no jury bitterness from that row, and no one-week refusal of an alliance.
- A veto used on somebody else costs 35: it falls under the jury's 30 after 3 week turns instead of 5, and under the
  note's 20 after 8 instead of 10.
- Past Heads of Household are ganged up on less often after a veto week, and the pile-on arc becomes rarer.
- Expected: B3 and B4 +0 to +2 points, B1 and B2 about 0, Q4 +0 to +0.02. At the screen, C1's B4 was 29.0%, above its
  28% bar, so C1n was adopted instead.

C2 would add only change between houseguests. Past NPC Heads of Household survive a little longer, so the player is a
little more often a nomination pick (Q4 up a little), and NPC pacts are a little more common.

## 10. Risks

- **The twelve may land near the line under C1.** Hence the counterfactuals and C2. As accepted, C1n's twelve is 17.6%
  [15.5, 19.8]: under the line, with the interval's top 0.2 points below it, and two policies above 20% on their own.
- **Feedback.** Settled houseguests vote and nominate more softly, so past Heads of Household survive longer, and some
  reign twice. More NPC pacts mean more allied votes against.
- **B4 had 3.2 points of headroom.** C1 took it to 29.0% at the screen, so the fallback was taken. Under C1n, B4 is
  26.2% [23.3, 29.4] on competition seeds 0-799: 1.8 points under the 28% bar, with the interval's top above it.
- **B7's interval is wide.** 43.3% [31.6, 55.9] on 60 accepted counters: in band by its rate, while the interval runs
  past 35% and 50% both.
- **The pile-on arc may become near-dead content.**
- **Roles (11) seasons change at define.** Only tests opt into 11, but several walk past a used veto: the week-two
  walks of `RoleVetoBoardTests.WeekTwoVetoes` (seeds 7101-7160) and everything built on them (`RoleKeepTheBlockTests`,
  the two Role persistence suites, the keep-the-block seasons of `FrozenEpisodeV29IntegrationTests`), and the 11 full
  seasons of `RoleStoryMomentsTests.SeasonsUnderTheRoleMomentsKeepTheirMomentsInBounds`. Their seed searches and floors
  are re-run first; any re-pin is recorded as a delegated call.
- **The runs wait for the machine:** Codex's hold, B9's round 9 and C:'s free space.
- **The self-save share is unmeasured** until the control pass records it. Measured since: 98.5% of the NPC vetoes
  used at 8 and 98.0% at 12 are self-saves (C0, acceptance seeds).

## 11. For the owner

**Answered on 2026-11-07.** The owner's words, condensed:
- **The measure is D0**, the whole season. The pass mark stays at most 20%, separately at 8 and at 12, pooled across
  the twelve policies. Each policy's figure and the confidence intervals are reported beside it. DP, while the player
  is in the house, is a secondary measure.
- **Before the acceptance runs, two fixes to the measurement:**
  - seeds 100-199 are frozen as the held-out acceptance set, and the development seeds and the combined 0-199 summary
    are kept clearly apart from it;
  - the unchanged control is run on those same acceptance seeds, policies and house sizes, because the paired
    comparison cannot use a control measured only on seeds 0-99.
- **Check the saves at version 11:** done, §4. None exists.
- These are design and acceptance decisions. B9's PR stays a draft, and no pass or merge is recorded.

The questions as they were put:

1. **The measure.** D0 (whole season) stays the target. DP (while you are in the house) is 3.6% and 12.0% today,
   already under 20%. Say if you would rather judge on DP.
2. **The rule.** A self-saving veto holder gets no grudge from the replacement; a veto used on somebody else costs 35,
   under the line.
3. **C2 and C3,** only if the screen needs them.
4. **The pile-on arc** becomes rarer.

**Status, 2026-11-08** (the lead's record, not the owner's words).
- The lab ran on the owner's measure. The screen adopted C1n under the plan's own fallback (decision 3): C1's B4 was
  29.0%, above 28%. So question 2's rule now covers houseguests who hold the veto, and the player's own veto keeps the
  40, self-save or not. That is a delegated call; it stays open to the owner in the PR.
- C1n passed acceptance on the held-out seeds 100-199: 3.9% at 8 and 17.6% at 12, pooled, each at most 20% (§6, As
  accepted). B1-B7 and the pace at 8 are in their bands; B7's interval is wide.
- C2 and C3 were not needed. The pile-on arc is rarer (question 4).
- The C1n commit makes the committed rule C1n. The enrolment (§8) comes next; then the owner signs off the numbers in
  the PR before it merges.
- Enrolled (§8, As enrolled): every new season starts on story rules 10. Saved seasons keep their version, and the save
  migration and the web importer still write threads. The old-save cell at 10 is the next step.
- Built (§8, As built): the enrolled head replays the lab's C1n rows exactly, and its own season at story rules 10 is an
  old-save cell, with the control that story rules 9 plays its commands otherwise. R7 (Roles, story rules 11) can
  follow. Open: the UMA PlayMode set and `StoryPacingTests.PacingReport`; the owner's sign-off in the PR.

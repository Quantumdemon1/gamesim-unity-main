# Balance baseline (B6b)

**Status: the B6 baseline, with the war rooms on and the NPC world measured.** The headline is 480 seasons per
policy and size at NPC budget 0 - not the plan's 800, which would take about 2.3 hours on this machine; 480 is
the largest count that fit in the 90 minutes the lead allowed (84 minutes in twelve parts). The NPC budget's
sensitivity is measured on a cut grid (see *The NPC world*), because every NPC operation validates the whole
season twice. The per-policy performance distributions are assumptions until human data exists (B8). Every
number here is reproducible from the commands below; treat differences inside the intervals as noise.

Measured on 2026-10-09 on `claude/balance-b5-b7` at `3f31fcd9` (base `claude/lead-integration` at `a74a261a`):
schema 28, competition rules 4, story rules 9, the economy, agency, the finale, the commitment rules, the unified
commitment and hearing version 1, D4's leaks and **D3's war rooms** - exactly what the director starts, because
every lab season is built by `SeasonBuilder.Create` and `ShippedRules.ApplyFresh` (B0). The shipped rules' tuple
is pinned in `BalanceLabGoldens.Rules`.

## What changed since the first baseline (`6b18d59e`, measured at `95e99bd2`)

- **The war rooms are in play (B6a).** Since D3 went into `ApplyFresh` a pact of three or more calls only in its
  war room, and before B6a nothing in the lab convened one: the reader's and loyalist's calls were refused.
  Now the five engaged players (random, reader, schemer, loyalist, floater) convene a pact of three's war room
  once the block is set and answer its plan as each would (`GatedPolicy.WarRoom`, `AnswerPlan`): the loyalist
  goes with it, the schemer pushes against a plan that names an ally, the reader against a plan somebody who
  said it is torn or leaning on, the floater lies low, the random player tosses a coin. Nobody is refused a call
  in a smoke season any more.
- **D3's counter was amended in place** (the lead's decision 6, no boundary since no saved season has played the
  war rooms): its reach is twenty at a view of ten or more (Loyal thirty), not eight at fifty (Loyal twelve).
  See *The war rooms*.
- **The NPC world is driven (B5).** `NpcPairing` is the director's pairing loop, moved into the simulation (B5a);
  `BalanceLabNpcWorld` drives the 1 Hz world through `PrepareNpcOperation` alone at a budget of ticks a week
  (B5b). The headline stays at budget 0, byte for byte the seasons it played before the driver.
- **Seeds pair across budgets**, so each budget plays budget 0's seasons, and the coins a policy draws never see
  the NPC world's operations.
- **New measurements:** the war rooms (autopsy and lab), the first commitment the player saw settle, the
  competitions by week (B7), and the T0 diagnostics for the five tuning questions.
- **Goldens:** the 48 headline cells at budgets 0 and 300 are pinned (`BalanceLabGoldens`), and budget 0's smoke
  and 20-seed headline rows by `BalanceLabTests.BudgetNoughtRows`.

## How it was measured

- **Seasons.** Regular roster, 8 and 12. Each step the policy proposes up to three times, told each refusal;
  otherwise the lab's walker takes the phase's own step (a veto holder on the block saves themselves). Every
  competition's performance comes from the performance model. The state is validated after every Advance, at
  every phase change and at the end. **0 of 11,520 headline seasons had an error**, and 0 of every other tier's.
- **Policies** see only a `PlayerView`: the screens' readers and the controls they offer, now including a war
  room's plan card (`OpenPlanCard`: who said whom, the player's whip word for each, the members' plan or a split).
  Ten are knowledge-gated; two are labelled oracles (`oracle-reader`, `oracle-skilled`).
- **Performance**: a draw about each policy's mean - beast .8, novice .35, everyone else .5 - keyed to the
  season, week and phase, never the season's generator.
- **The NPC world** (`BalanceLabNpcWorld`, the lead's decisions 1-4): the budget's ticks a week, half in each
  free-time phase as it is played (the move-in night is a social phase of its own), spent in 60-tick slices
  before each decision and the rest before the Advance that closes the phase; on a scan due, `NpcPairing.Plan`
  with a lease cap of two; an approach arrives in five ticks and starts at the first free rendezvous. Ticks run
  only while the house is eligible (the player in the house, free time, no diary). Walk-ins stay off.
- **Metrics** from `SeasonAutopsy` (B1, with the war rooms and the first settle added), the story's pace from
  `StoryPacingTests.Pace`, the ceremonies from `CeremonyPacing`, and the T0 records (Game Sense's notes by face
  and row kind, each NPC nomination's weight in its terms, each season's first pariah, each member a counter
  could reach).

Reproduce (Unity-free, from the repository root; a tier longer than ten minutes is played in parts and merged):

```
dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabTests"                      # smoke and the lab's own tests
BALANCE_TIER=headline BALANCE_FROM=0 BALANCE_COUNT=40 BALANCE_PARTS=<dir> dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabParts.PartReport"
#   ... parts 40, 80, ... 440, then:
BALANCE_TIER=headline BALANCE_PARTS=<dir> dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabParts.MergeReport"
# the other tiers, the same way (BALANCE_TIER = npc, projection, competition, warrooms, performance, full):
BALANCE_NPC_POLICIES=passive,novice,social,reader,beast BALANCE_NPC_BUDGETS=0,300   # npc, 100 seeds
BALANCE_NPC_POLICIES=passive,reader BALANCE_NPC_BUDGETS=0,900                       # npc, 50 seeds
BALANCE_NPC_POLICIES=reader BALANCE_NPC_BUDGETS=0,1800                              # npc, 15 seeds
BALANCE_PROJECTION_BUDGETS=0 (400 seeds), =300 (200), =900 (50)                     # projection
dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabGoldens.Budget300Size12First"   # one golden slice
BALANCE_PROBE_TICKS=300 dotnet test Tools/SimulationTests --filter "FullyQualifiedName~NpcWorldCostProbe"
```

Rows land as JSONL, and the tables as Markdown, in `Tools/SimulationTests/bin/Debug/net10.0/balance/`.

## Runtime

Measured on six threads shared with three other lanes' test hosts.

| tier | seasons | wall time |
|---|---|---|
| smoke (in the subset) | 91 (84 at budget 0, 7 at 300) | about 60 s |
| headline, budget 0 | 11,520 (12 policies x 8, 12 x 480) | 84 min in 12 parts |
| war rooms (B6a) | 1,000 (5 players x 8, 12 x 100) | 7-12 min |
| NPC budget 0 and 300 | 2,000 (5 players x 8, 12 x 2 x 100) | 60 min in 10 parts |
| NPC budget 0 and 900 | 400 (passive, reader x 8, 12 x 2 x 50) | 31 min in 5 parts |
| NPC budget 0 and 1800 | 60 (reader x 8, 12 x 2 x 15) | 10 min in 2 parts |
| projections (novice x 6 houses) | 2,400 at 0, 1,200 at 300, 300 at 900 | 10 + 39 + 28 min |
| competition (T0 Q3) | 1,600 | 7.5 min |
| goldens | 960 (48 cells x 20) | about 35 min in 8 tests |

**The NPC world's cost (risk 1).** An operation costs about 7 ms of engine time (`PrepareNpcOperation` validates
the candidate and installing it as a new engine validates it again, as the director does). A season at 300 a week
is about 1,650 operations (12 s of engine), at 900 about 5,240 (36 s), at 1800 about 10,700 (72 s), against about
2.5 thread-seconds for the whole season at budget 0. The brief's grid (five players, two sizes, four budgets, 200
seeds) would have taken about 30 hours, so the grid was cut, never the engine: 100 seeds at 300, two players at 50
seeds at 900, one player at 15 seeds at 1800.

## Findings F1-F7

**F1. Strategy moves win rate at 12, barely at 8.**

- **At 8** (base rate 12.5%) the passive player wins 11.7% [9.1, 14.8]. Against it, on the same seasons:
  - the floater wins 24.8% (McNemar p < 0.001) and social 17.1% (p = 0.021);
  - loyalist 16.3% (p = 0.053), reader 15.2% (p = 0.12), schemer 10.8% and random 10.4% do not separate;
  - the novice wins 8.1% (p = 0.072) and the exploit hunter 5.2% (p = 0.001), worse than doing nothing.
- **At 12** (base rate 8.3%) the passive player wins 9.8% [7.4, 12.8] and every engaged gated style beats it
  (p <= 0.01): reader 28.5%, floater 26.9%, schemer 25.2%, loyalist 22.7%, social 21.7%, random 15.6%; the novice
  14.4% (p = 0.039); the exploit hunter's 9.8% does not (p = 0.91).
- **Between styles**: reader beats random at both sizes (p = 0.035 and < 0.001) and social at 12 (p = 0.017);
  social and loyalist beat random at both (p <= 0.019).
- **The B-1 band** (a skilled gated policy wins at least 1.5x the base rate): at 12 all five skilled policies
  clear it with their whole interval (21.7-28.5%, low ends 18.2-24.7%); **at 8 only the floater does** (24.8%
  [21.1, 28.8]); social, loyalist, reader and schemer fall short of 18.75%.

**F2. The oracles read hidden state; they bound skill only weakly.**

- The oracle reader wins 19.8% at 8 and 25.6% at 12; the gated reader 15.2% and 28.5% (McNemar 53/75, p = 0.063
  at 8; 110/96, p = 0.37 at 12). Reading the hidden state is worth perhaps five points at 8 and nothing at 12.
- The skilled oracle wins 14.4% and 17.1%, below the gated reader at 12 (p < 0.001).

**F3. Competitions dominate, and preparation dominates competitions (T0 Q3).**

- At a performance of .5 a passive player wins 51.9% and 52.3% of the weekly competitions it plays. The beast
  (about .8, studying until prepared) wins 97.6% and 99.0% of them and **82.5% and 61.3% of seasons**.
- **The studier** - the passive player who studies the house in each social week until its preparation is five,
  and does nothing else - at the same fixed performance, on the same seasons:

  | size | performance | passive: weekly wins / seasons | studier: weekly wins / seasons | studier vs passive (McNemar) |
  |---|---|---|---|---|
  | 8 | .5 | 50.8% / 9.5% | 95.2% / 64.0% | 115/6, p < 0.001 |
  | 8 | .8 | 68.0% / 23.5% | 98.4% / 66.5% | 105/19, p < 0.001 |
  | 12 | .5 | 52.9% / 9.0% | 95.3% / 45.5% | 85/12, p < 0.001 |
  | 12 | .8 | 68.9% / 15.5% | 98.0% / 44.5% | 77/19, p < 0.001 |

  Five free-time seats of study (preparation +5, permanent) are worth far more than playing the minigames well:
  they take an average player from about half the weekly competitions to 95%, and from a 9% season to 45-64%.
  Full marks alone (B4, below) reach only 79-80%.
- The field's strongest on paper wins 45% of weekly competitions at both sizes (a uniform draw is 19% and 16%).

**F4. The passive newcomer is nominated more; engaged play neutralises it (T0 Q4).**

- Nominations per week in the house, player against the houseguests: passive 1.13 at 8 and 1.43 at 12; the
  novice 1.03 and 1.03; the engaged gated policies 0.82-1.06 (the exploit hunter 1.21 at 12); the beast 0.43
  and 0.67.
- **Why**, from every NPC Head of Household's nominations (the weight in its terms, the player against the mean
  other candidate; lower is put up first): for the passive player at 8 the Head of Household's view of them is 21
  points lower (-5.8 against +15.1), the strategy windows' terms 32 lower (-26.5 against +5.6: the Head of
  Household's target deals and the pleas against the player outweigh any ally shield, deal or protection - the
  diagnostic does not split this term further), the story's 9 lower (grudges),
  the threat 3 lower. At 12 the same: view -22.5, strategy terms -36.0, story -6.5, threat -4.3. Every policy's
  strategy terms are negative (-20 to -60): the house's own deals and pleas single the player out more than any
  houseguest, and engaged play claws it back through the view and its own deals. The passive player is ranked
  first or second for the block (mean rank 1.3 of 5.4 at 8) and goes up at 53% of NPC nominations.
- Default (the lead's decision 7): no tuning; the engaged novice sits at 1.03.

**F5. The NPC world, measured (B5).** At 300 ticks a week (the budget that stands for a human) the house deals
and pairs off more, and the player's outcomes do not move detectably; at 900 the house of eight changes.

- **At 300** (five players, 8 and 12, 100 seeds, every metric paired with budget 0): no win rate, final-two rate
  or early-eviction rate moves (every McNemar p >= 0.10). NPC-only deals rise by 3.8-8.0 a season (every interval
  excludes 0), NPC-only pacts by 0.1-0.4 (significant for passive, social and novice at 8 and passive at 12); the
  warmest pair warms by 14-27 at 8 (the beast's +6 aside); pairs within ten of the bound go from about 1% to 2%; NPC conversations: 61-86
  a season at 8 and 108-147 at 12. Two nomination rates move: social at 8 (0.38 to 0.46 a week) and reader at 12
  (0.37 to 0.42). Game Sense does not move.
- **At 900** (passive and reader, 50 seeds): at 8 the player survives longer - out by week 3 drops from 56% to
  34% for passive (p = 0.035) and from 38% to 12% for the reader (p = 0.004), the reader's final-two rate rises
  from 34% to 62% (p = 0.007), nominations per week fall by a tenth, Game Sense rises (reader +6.3). NPC-only
  deals +9-22 and pacts +0.5 a season. At 12 nothing in the player's outcomes moves detectably.
- **Saturation (risk 8)**: the share of houseguest pairs within ten of ±200 is about 1% at budget 0, 2% at 300,
  3-5% at 900 and 5.6-5.7% at 1800 (at ±200 itself: up to 5.4%); the warmest pair's mean reaches 191-198 at 900
  and 1800. 1800 measures a house beginning to saturate; 300 and 900 do not.
- **A finding for the lead, not the lab:** an NPC conversation's completion moves the pair through
  `ChangeWithRoll`, which writes the player's relationship arc with each houseguest of the pair (and with a
  gossip target) - arcs are meant to be the player's. The driver only calls `PrepareNpcOperation`; the director's
  world does the same in every played season (64 of 1,738 operations of a watched season at 300).
- The decision to normalise NPC activity per week (B-4) can now be read against this table: at the human budget
  the NPC world adds dealing and warmth inside the house without moving the player's odds.

**F6. Harness rules drift from what ships - resolved for the lab.** Every lab season starts through
`ShippedRules.ApplyFresh`; the 48 golden cells and the shipped rules' tuple are pinned, and a commit that changes a
`Current` constant or `ApplyFresh` re-records them with its reason. The older harnesses (GameSense's default arm,
the story sweeps) still build their own seasons.

**F7. E4 at 90% needs five or six weeks with three testers; E3 as worded is out of reach for a novice-like
tester (B7).** See *The projections* and the proposed *Session length* in `PLAYTEST_PROTOCOL.md`.

## The tuning questions (BALANCE plan §4), with what T0 measured

| question | measured | what it decides | boundary |
|---|---|---|---|
| **Q1. D3's counter rarely moves anyone** (1 of 13 came round, mean odds 0.17) | Amended in place to twenty at a view of ten (Loyal thirty). Digests' war rooms: mean odds 0.49 (6.43/13), 3 of 13 came round, 7 of 9 counters carried. The lab's headline: mean odds 0.36 over the 854 members a counter could have faced at every plan answered, 0.28 at the 64 counters played, 20 members came round, 43 of 64 counters carried. The two samples straddle the band - the lab's members view the player less warmly (median 24) and lean harder (median margin 11) than the digests' constructed trio - and no reach constants put both inside 0.35-0.50 by more than 0.01. | Done (decision 6). If the owner wants more room, the shape (`ComeRoundOdds`) is the lever, not the constants. | none (in place) |
| **Q2. Game Sense gap** (target re-set to at least 10 at both sizes, low end over 5) | Today 7.8 [6.5, 9.2] at 8 and 9.9 [9.1, 10.7] at 12: **not met** at either size. Replayed from each season's notes on the same seasons: weights 0.2/0.6/0.2 gives 11.1 [9.4, 12.8] and 13.9 [13.0, 14.9]; dropping the competitions face (0/0.7/0.3) 13.1 and 15.4; unbounded faces 11.4 and 38.0; strategy notes x1.5 alone lowers it (the clamp). The gap is strategy: unclaimed and claimed opportunities (+24.6 and +45.8 points a season), calls (+1.8 and +15.6), ballots (+1.4 and +9.5); the competitions face is at 88-97 for both and the clamp hides +31.5 at 12. | Re-weighting the faces meets the re-set target at both sizes; the weights are the cheapest lever. | B9 (`tuningRulesVersion`): any formula change moves the rules-off digests |
| **Q3. Competitions dominate** (beast 82.5% and 61.3% of seasons) | Preparation, not performance: the studier at .5 wins 95% of weekly competitions and 64% / 45.5% of seasons against passive's 51% and 9.5% / 9.0%; at .8 the passive player wins only 68-69% weekly. | The v5 candidate (preparation spent on use, cap 3) is aimed at the right lever; prototype it against the studier cells. | competitionRulesVersion 5 (B9 per the decisions) |
| **Q4. Passive newcomer nominated more** (1.13 and 1.43) | The strategy windows' terms (target deals and pleas against the player, against others' shields and deals: -32 and -36 against the mean candidate) and a lower view (-21, -23); the novice is at 1.03. At 300 ticks nothing changes for passive (0.61 to 0.59 a week at 8, 0.60 to 0.61 at 12). | Default: no tuning (decision 7). | B9 if tuned |
| **Q5. Story pariah rate** (17.2% at 8, 43.6% at 12; target at most 20%) | Every pariah target had won a Head of Household (100% and 99.9%; two or more 14% and 29%). The holders' grudges: nominated 52-55%, the veto's replacement 26-29%, a replacement 10-12%, voted against 3%. At 12 the run comes at week 6.1 with 6.8 in the house. The NPC world does not change it (11.4% and 42.0% at 300). | The pariah is a past Head of Household's nomination grudges, not repeat HoHs alone; for the story session: the nomination grudge (70, x1.2 allied), the replacement grudges, and a definition scaled by house size. | story rules 10 (story session) |

## The war rooms (D3)

Over the headline's 11,520 seasons (only the five engaged players convene one):

| policy | size | seasons with a plan | plans | agreed / countered / low | members a counter faced (mean odds) | at counters: reachable, mean odds, came round | counters carried | target evicted |
|---|---|---|---|---|---|---|---|---|
| random | 8 | 8 of 480 | 13 | 3 / 4 / 6 | 17 (0.42) | 7, 0.54, 5 | 4 of 4 | 8 of 13 |
| reader | 8 | 40 of 480 | 69 | 62 / 7 / 0 | 69 (0.40) | 5, 0.25, 1 | 6 of 7 | 53 of 69 |
| schemer | 8 | 20 of 480 | 32 | 28 / 4 / 0 | 45 (0.32) | 5, 0.00, 0 | 1 of 4 | 27 of 32 |
| loyalist | 8 | 23 of 480 | 37 | 33 / 4 / 0 | 34 (0.42) | 4, 0.24, 1 | 3 of 4 | 29 of 37 |
| random | 12 | 8 of 480 | 16 | 5 / 8 / 3 | 15 (0.31) | 8, 0.24, 2 | 4 of 8 | 9 of 16 |
| reader | 12 | 89 of 480 | 222 | 210 / 12 / 0 | 258 (0.36) | 11, 0.29, 3 | 9 of 12 | 166 of 222 |
| schemer | 12 | 117 of 480 | 253 | 240 / 13 / 0 | 278 (0.34) | 15, 0.34, 6 | 9 of 13 | 156 of 253 |
| loyalist | 12 | 54 of 480 | 135 | 123 / 12 / 0 | 138 (0.34) | 12, 0.23, 2 | 7 of 12 | 107 of 135 |

No plan lapsed or came to nothing; the player called 747 of the 777 plans. The floater, in no pact, never
convened one. Plans the player backs are the week's call; 71% of plans' targets went home that week.

## The projections (B7)

From the novice (the first-timer model) in every house, at budget 0 (400 seeds), 300 (200) and 900 (50).
S(k) is the chance a tester is out by week k; C(k) the chance they saw a commitment of theirs settle (a deal or
promise kept or broken whose ending they know, or a war-room plan they answered) while still in the house.

| house | S(1) / S(3) / S(5) | C(2) / C(4) / C(plateau) | E4 (at least one of n out, 90%): n = 3 / 4 / 5 | E3 (all n saw one, 90%) | joint |
|---|---|---|---|---|---|
| 6 | 27% / 46% / - | 58% / 63% / 63% | week 4 / 3 / 2 | never | never |
| 8 | 24% / 46% / 53% | 47% / 59% / 61% | week 6 / 3 / 3 | never | never |
| 10 | 4% / 31% / 56% | 45% / 67% / 71% | week 5 / 4 / 4 | never | never |
| 12 | 2% / 23% / 46% | 42% / 72% / 79% | week 6 / 5 / 5 | never | never |
| All-Stars 8 | 11% / 32% / 42% | 45% / 69% / 72% | never / 6 / 5 | never | never |
| 8 at 300 ticks | 25% / 45% / 55% | 46% / 60% / 62% | week 5 / 3 / 3 | never | never |

- The best three testers can do on E3 as worded is 22% at 8 and 48% at 12: about four in ten novices at 8 (two in
  ten at 12) never see a commitment of theirs settle while they are in the house.
- The lab's own minutes a week in the house - ceremonies, the competitions played, decisions at 30 s each, and
  the NPC budget as free roam - are 8.7 at 8 (13.7 at 300 ticks, 23.6 at 900), well under E1's 30-45 minutes an
  episode: the time a person takes over a decision is the unknown, and B8 measures it. At E1's own pace an E4
  session of three testers at 8 (six episode-weeks) is three to four and a half hours.

## The NPC world: key metrics against the budget

Each budget's seasons paired with budget 0's (the same seeds). A flag: rate, McNemar b/c and p. A number: mean,
and the mean difference with a bootstrap 95% interval (a star: the interval excludes 0).

| metric | policy, size | 0 | 300 (vs 0) |
|---|---|---|---|
| win | reader 8 / 12 | 14% / 24% | 16% (13/11, p 0.84) / 22% (17/19, p 0.87) |
| win | social 8 / 12 | 19% / 23% | 12% (7/14, p 0.19) / 24% (21/20, p 1.0) |
| win | beast 8 / 12 | 81% / 58% | 84% (16/13, p 0.71) / 52% (16/22, p 0.42) |
| out by week 3 | passive 8 / 12 | 55% / 47% | 55% (18/18) / 39% (11/19, p 0.20) |
| out by week 3 | reader 8 / 12 | 36% / 11% | 27% (8/17, p 0.11) / 15% (11/7, p 0.48) |
| nominations a week | social 8 | 0.379 | 0.458 (+0.079 [+0.028, +0.124] *) |
| nominations a week | reader 12 | 0.367 | 0.417 (+0.050 [+0.012, +0.089] *) |
| NPC-only deals | passive 8 / 12 | 14.8 / 59.2 | 21.2 (+6.4 *) / 63.0 (+3.9 *) |
| NPC-only deals | reader 8 / 12 | 14.4 / 48.9 | 18.4 (+4.0 *) / 56.9 (+8.0 *) |
| NPC-only pacts | passive 8 / 12 | 1.21 / 2.78 | 1.63 (+0.42 *) / 3.12 (+0.34 *) |
| warmest pair | passive 8 / reader 8 | 134 / 151 | 161 (+27 *) / 175 (+23 *) |
| pairs within ten of ±200 | reader 8 / 12 | 1.0% / 1.3% | 2.3% * / 1.9% * |
| story: a pariah season | passive 8 / 12 | 16% / 58% | 27% (24/13, p 0.10) / 58% (25/25) |
| NPC conversations | reader 8 / 12 | 0 | 75 / 146 |

| metric | policy, size | 0 | 900 (vs 0) |
|---|---|---|---|
| final two | reader 8 | 34% | 62% (19/5, p 0.007) |
| out by week 3 | passive 8 / reader 8 | 56% / 38% | 34% (6/17, p 0.035) / 12% (3/16, p 0.004) |
| nominations a week | passive 8 / reader 8 | 0.627 / 0.506 | 0.532 (-0.095 *) / 0.407 (-0.100 *) |
| Game Sense | reader 8 | 70.7 | 76.9 (+6.3 [+2.1, +10.6] *) |
| NPC-only deals | reader 8 / 12 | 13.1 / 46.7 | 22.4 (+9.3 *) / 69.1 (+22.4 *) |
| pairs within ten of ±200 | passive 8 / reader 8 | 0.4% / 0.8% | 4.8% * / 5.5% * |
| win | reader 8 / 12 | 12% / 30% | 20% (8/4, p 0.39) / 26% (10/12, p 0.83) |

At 1800 (reader, 15 seeds): pairs within ten of the bound 5.6-5.7% (at ±200: 3.9-5.4%), the warmest pair's
mean 194-195, NPC-only deals +16 at 8 and +35 at 12.

## The headline tables (budget 0, 480 seasons a cell)

All 11,520 seasons: the same 480 seasons for every policy at each size.

### Win rate by policy and size

Base rate is 1/size. McNemar pairs each policy with the passive player on the same seasons (b: only this policy won, c: only passive won).

| policy | size | n | wins | win rate | Wilson 95% | final two | mean placement | vs passive b/c | p |
|---|---|---|---|---|---|---|---|---|---|
| passive | 8 | 480 | 56 | 11.7% | [9.1%, 14.8%] | 23.8% | 5.14 | - | - |
| random | 8 | 480 | 50 | 10.4% | [8.0%, 13.5%] | 35.4% | 4.24 | 43/49 | 0.602 |
| social | 8 | 480 | 82 | 17.1% | [14.0%, 20.7%] | 42.7% | 3.82 | 72/46 | 0.021 |
| reader | 8 | 480 | 73 | 15.2% | [12.3%, 18.7%] | 40.6% | 4.13 | 62/45 | 0.122 |
| schemer | 8 | 480 | 52 | 10.8% | [8.4%, 13.9%] | 39.4% | 4.15 | 45/49 | 0.757 |
| loyalist | 8 | 480 | 78 | 16.3% | [13.2%, 19.8%] | 33.8% | 4.60 | 70/48 | 0.053 |
| floater | 8 | 480 | 119 | 24.8% | [21.1%, 28.8%] | 40.2% | 3.55 | 107/44 | 0.000 |
| beast | 8 | 480 | 396 | 82.5% | [78.8%, 85.6%] | 92.3% | 1.56 | 354/14 | 0.000 |
| novice | 8 | 480 | 39 | 8.1% | [6.0%, 10.9%] | 34.0% | 4.64 | 31/48 | 0.072 |
| exploit | 8 | 480 | 25 | 5.2% | [3.6%, 7.6%] | 34.6% | 4.61 | 24/55 | 0.001 |
| oracle-reader | 8 | 480 | 95 | 19.8% | [16.5%, 23.6%] | 32.3% | 4.29 | 82/43 | 0.001 |
| oracle-skilled | 8 | 480 | 69 | 14.4% | [11.5%, 17.8%] | 32.9% | 4.59 | 57/44 | 0.232 |
| passive | 12 | 480 | 47 | 9.8% | [7.4%, 12.8%] | 13.3% | 8.44 | - | - |
| random | 12 | 480 | 75 | 15.6% | [12.7%, 19.1%] | 22.3% | 6.67 | 69/41 | 0.010 |
| social | 12 | 480 | 104 | 21.7% | [18.2%, 25.6%] | 27.7% | 6.18 | 96/39 | 0.000 |
| reader | 12 | 480 | 137 | 28.5% | [24.7%, 32.7%] | 38.1% | 5.25 | 119/29 | 0.000 |
| schemer | 12 | 480 | 121 | 25.2% | [21.5%, 29.3%] | 32.3% | 5.76 | 112/38 | 0.000 |
| loyalist | 12 | 480 | 109 | 22.7% | [19.2%, 26.7%] | 25.8% | 6.61 | 97/35 | 0.000 |
| floater | 12 | 480 | 129 | 26.9% | [23.1%, 31.0%] | 29.0% | 5.35 | 115/33 | 0.000 |
| beast | 12 | 480 | 294 | 61.3% | [56.8%, 65.5%] | 65.6% | 3.96 | 264/17 | 0.000 |
| novice | 12 | 480 | 69 | 14.4% | [11.5%, 17.8%] | 24.6% | 6.42 | 63/41 | 0.039 |
| exploit | 12 | 480 | 47 | 9.8% | [7.4%, 12.8%] | 21.9% | 7.21 | 42/42 | 0.913 |
| oracle-reader | 12 | 480 | 123 | 25.6% | [21.9%, 29.7%] | 29.8% | 6.32 | 109/33 | 0.000 |
| oracle-skilled | 12 | 480 | 82 | 17.1% | [14.0%, 20.7%] | 20.4% | 7.16 | 69/34 | 0.001 |

### Further pairs

McNemar on the same seasons: b where only the first won, c where only the second did.

| first | second | size | first wins | second wins | b/c | p |
|---|---|---|---|---|---|---|
| reader | oracle-reader | 8 | 73 | 95 | 53/75 | 0.063 |
| reader | oracle-skilled | 8 | 73 | 69 | 65/61 | 0.789 |
| social | random | 8 | 82 | 50 | 71/39 | 0.003 |
| reader | random | 8 | 73 | 50 | 66/43 | 0.035 |
| loyalist | random | 8 | 78 | 50 | 68/40 | 0.009 |
| reader | social | 8 | 73 | 82 | 58/67 | 0.474 |
| novice | passive | 8 | 39 | 56 | 31/48 | 0.072 |
| exploit | random | 8 | 25 | 50 | 22/47 | 0.004 |
| reader | oracle-reader | 12 | 137 | 123 | 110/96 | 0.365 |
| reader | oracle-skilled | 12 | 137 | 82 | 117/62 | 0.000 |
| social | random | 12 | 104 | 75 | 86/57 | 0.019 |
| reader | random | 12 | 137 | 75 | 117/55 | 0.000 |
| loyalist | random | 12 | 109 | 75 | 96/62 | 0.009 |
| reader | social | 12 | 137 | 104 | 107/74 | 0.017 |
| novice | passive | 12 | 69 | 47 | 63/41 | 0.039 |
| exploit | random | 12 | 47 | 75 | 37/65 | 0.008 |

### The proposed B-1 band: a knowledge-gated skilled policy wins at least 1.5x the base rate

The bar is 1.5 / size. Clears: the win rate is at or over the bar; surely: the Wilson interval's low end is too.

| policy | size | bar | win rate [Wilson] | clears | surely |
|---|---|---|---|---|---|
| social | 8 | 18.8% | 17.1% [14.0%, 20.7%] | no | no |
| reader | 8 | 18.8% | 15.2% [12.3%, 18.7%] | no | no |
| schemer | 8 | 18.8% | 10.8% [8.4%, 13.9%] | no | no |
| loyalist | 8 | 18.8% | 16.3% [13.2%, 19.8%] | no | no |
| floater | 8 | 18.8% | 24.8% [21.1%, 28.8%] | yes | yes |
| social | 12 | 12.5% | 21.7% [18.2%, 25.6%] | yes | yes |
| reader | 12 | 12.5% | 28.5% [24.7%, 32.7%] | yes | yes |
| schemer | 12 | 12.5% | 25.2% [21.5%, 29.3%] | yes | yes |
| loyalist | 12 | 12.5% | 22.7% [19.2%, 26.7%] | yes | yes |
| floater | 12 | 12.5% | 26.9% [23.1%, 31.0%] | yes | yes |

### Survival by week: the player still in the house after week k

| policy (8) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 |
|---|---|---|---|---|---|---|
| passive | 77.3% | 51.9% | 42.7% | 40.4% | 38.5% | 23.8% |
| random | 85.8% | 70.2% | 60.8% | 58.5% | 54.4% | 35.4% |
| social | 89.0% | 76.3% | 68.3% | 64.4% | 60.6% | 42.7% |
| reader | 84.0% | 70.4% | 61.7% | 59.4% | 55.8% | 40.6% |
| schemer | 89.4% | 74.0% | 61.7% | 57.5% | 52.1% | 39.4% |
| loyalist | 77.1% | 65.2% | 52.5% | 49.2% | 45.6% | 33.8% |
| floater | 91.9% | 80.2% | 73.1% | 69.4% | 65.6% | 40.2% |
| beast | 99.8% | 92.3% | 92.3% | 92.3% | 92.3% | 92.3% |
| novice | 76.3% | 64.6% | 54.4% | 51.0% | 47.3% | 34.0% |
| exploit | 78.3% | 63.8% | 54.4% | 52.1% | 50.2% | 34.6% |
| oracle-reader | 85.2% | 69.2% | 59.2% | 54.4% | 51.0% | 32.3% |
| oracle-skilled | 80.0% | 63.8% | 53.3% | 49.6% | 46.7% | 32.9% |

| policy (12) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 | wk 7 | wk 8 | wk 9 | wk 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| passive | 87.7% | 66.3% | 50.6% | 36.3% | 26.0% | 19.6% | 15.8% | 15.2% | 15.0% | 13.3% |
| random | 93.3% | 82.7% | 74.2% | 61.9% | 50.0% | 39.4% | 32.7% | 31.3% | 30.0% | 22.3% |
| social | 93.8% | 85.6% | 76.3% | 66.0% | 55.4% | 44.6% | 38.5% | 37.1% | 35.6% | 27.7% |
| reader | 99.6% | 92.7% | 85.2% | 73.8% | 65.8% | 53.5% | 47.1% | 46.3% | 44.2% | 38.1% |
| schemer | 97.3% | 89.2% | 80.0% | 68.8% | 58.5% | 48.3% | 42.9% | 41.5% | 39.8% | 32.3% |
| loyalist | 90.2% | 84.4% | 74.8% | 63.3% | 49.6% | 37.7% | 31.9% | 30.8% | 27.9% | 25.8% |
| floater | 99.6% | 95.0% | 88.3% | 78.3% | 66.0% | 53.8% | 44.2% | 42.1% | 41.5% | 29.0% |
| beast | 100.0% | 85.0% | 84.6% | 72.7% | 72.5% | 65.6% | 65.6% | 65.6% | 65.6% | 65.6% |
| novice | 98.5% | 85.8% | 77.7% | 66.9% | 54.2% | 41.9% | 33.1% | 31.7% | 29.2% | 24.6% |
| exploit | 90.6% | 81.7% | 67.5% | 54.8% | 42.9% | 31.9% | 26.7% | 25.6% | 25.2% | 21.9% |
| oracle-reader | 93.8% | 82.9% | 74.2% | 62.1% | 51.0% | 42.5% | 36.3% | 35.6% | 34.2% | 29.8% |
| oracle-skilled | 92.1% | 78.5% | 67.7% | 53.8% | 42.5% | 32.1% | 26.9% | 26.7% | 25.8% | 20.4% |

### Early risk: nominated and evicted in week one, out by week k, and the chance at least one of three testers is out

| policy | size | nominated wk 1 | evicted wk 1 [Wilson] | out by wk 2 | out by wk 3 | P(≥1 of 3 out) wk 1 / 2 / 3 |
|---|---|---|---|---|---|---|
| passive | 8 | 38.3% | 22.7% [19.2%, 26.7%] | 48.1% | 57.3% | 53.8% / 86.0% / 92.2% |
| random | 8 | 30.2% | 14.2% [11.3%, 17.6%] | 29.8% | 39.2% | 36.8% / 65.4% / 77.5% |
| social | 8 | 35.2% | 11.0% [8.5%, 14.2%] | 23.8% | 31.7% | 29.6% / 55.7% / 68.1% |
| reader | 8 | 36.0% | 16.0% [13.0%, 19.6%] | 29.6% | 38.3% | 40.8% / 65.1% / 76.5% |
| schemer | 8 | 41.3% | 10.6% [8.2%, 13.7%] | 26.0% | 38.3% | 28.6% / 59.5% / 76.5% |
| loyalist | 8 | 37.1% | 22.9% [19.4%, 26.9%] | 34.8% | 47.5% | 54.2% / 72.3% / 85.5% |
| floater | 8 | 40.2% | 8.1% [6.0%, 10.9%] | 19.8% | 26.9% | 22.4% / 48.4% / 60.9% |
| beast | 8 | 1.3% | 0.2% [0.0%, 1.2%] | 7.7% | 7.7% | 0.6% / 21.4% / 21.4% |
| novice | 8 | 45.4% | 23.8% [20.2%, 27.8%] | 35.4% | 45.6% | 55.7% / 73.1% / 83.9% |
| exploit | 8 | 33.8% | 21.7% [18.2%, 25.6%] | 36.3% | 45.6% | 51.9% / 74.1% / 83.9% |
| oracle-reader | 8 | 39.4% | 14.8% [11.9%, 18.2%] | 30.8% | 40.8% | 38.1% / 66.9% / 79.3% |
| oracle-skilled | 8 | 36.3% | 20.0% [16.7%, 23.8%] | 36.3% | 46.7% | 48.8% / 74.1% / 84.8% |
| passive | 12 | 52.9% | 12.3% [9.7%, 15.5%] | 33.8% | 49.4% | 32.5% / 70.9% / 87.0% |
| random | 12 | 34.4% | 6.7% [4.8%, 9.3%] | 17.3% | 25.8% | 18.7% / 43.4% / 59.2% |
| social | 12 | 39.0% | 6.3% [4.4%, 8.8%] | 14.4% | 23.8% | 17.6% / 37.2% / 55.7% |
| reader | 12 | 26.7% | 0.4% [0.1%, 1.5%] | 7.3% | 14.8% | 1.2% / 20.3% / 38.1% |
| schemer | 12 | 37.1% | 2.7% [1.6%, 4.6%] | 10.8% | 20.0% | 7.9% / 29.1% / 48.8% |
| loyalist | 12 | 44.8% | 9.8% [7.4%, 12.8%] | 15.6% | 25.2% | 26.6% / 39.9% / 58.2% |
| floater | 12 | 27.1% | 0.4% [0.1%, 1.5%] | 5.0% | 11.7% | 1.2% / 14.3% / 31.1% |
| beast | 12 | 1.0% | 0.0% [0.0%, 0.8%] | 15.0% | 15.4% | 0.0% / 38.6% / 39.5% |
| novice | 12 | 40.6% | 1.5% [0.7%, 3.0%] | 14.2% | 22.3% | 4.3% / 36.8% / 53.1% |
| exploit | 12 | 41.5% | 9.4% [7.1%, 12.3%] | 18.3% | 32.5% | 25.6% / 45.5% / 69.2% |
| oracle-reader | 12 | 40.8% | 6.3% [4.4%, 8.8%] | 17.1% | 25.8% | 17.6% / 43.0% / 59.2% |
| oracle-skilled | 12 | 51.5% | 7.9% [5.8%, 10.7%] | 21.5% | 32.3% | 21.9% / 51.5% / 69.0% |

### Nominations per week in the house (F4): the player against the houseguests of the same seasons

| policy | size | player | houseguests | ratio |
|---|---|---|---|---|
| passive | 8 | 0.587 | 0.522 | 1.13 |
| random | 8 | 0.472 | 0.530 | 0.89 |
| social | 8 | 0.429 | 0.523 | 0.82 |
| reader | 8 | 0.482 | 0.518 | 0.93 |
| schemer | 8 | 0.455 | 0.520 | 0.88 |
| loyalist | 8 | 0.502 | 0.514 | 0.98 |
| floater | 8 | 0.451 | 0.519 | 0.87 |
| beast | 8 | 0.205 | 0.480 | 0.43 |
| novice | 8 | 0.531 | 0.514 | 1.03 |
| exploit | 8 | 0.517 | 0.511 | 1.01 |
| oracle-reader | 8 | 0.500 | 0.535 | 0.93 |
| oracle-skilled | 8 | 0.500 | 0.533 | 0.94 |
| passive | 12 | 0.605 | 0.422 | 1.43 |
| random | 12 | 0.451 | 0.427 | 1.06 |
| social | 12 | 0.430 | 0.422 | 1.02 |
| reader | 12 | 0.366 | 0.434 | 0.84 |
| schemer | 12 | 0.382 | 0.433 | 0.88 |
| loyalist | 12 | 0.445 | 0.424 | 1.05 |
| floater | 12 | 0.388 | 0.429 | 0.90 |
| beast | 12 | 0.260 | 0.389 | 0.67 |
| novice | 12 | 0.444 | 0.429 | 1.03 |
| exploit | 12 | 0.508 | 0.421 | 1.21 |
| oracle-reader | 12 | 0.455 | 0.435 | 1.05 |
| oracle-skilled | 12 | 0.492 | 0.431 | 1.14 |

### The player's competitions by policy (weekly HoH and veto they played)

| policy | size | played / season | won | win rate [Wilson] | mean performance | HoH wins / season | veto wins / season |
|---|---|---|---|---|---|---|---|
| passive | 8 | 5.09 | 1267 | 51.9% [49.9%, 53.9%] | 0.51 | 1.33 | 1.54 |
| random | 8 | 6.07 | 1525 | 52.4% [50.5%, 54.2%] | 0.50 | 1.73 | 1.76 |
| social | 8 | 6.43 | 1565 | 50.7% [48.9%, 52.5%] | 0.51 | 1.87 | 1.76 |
| reader | 8 | 6.06 | 1498 | 51.5% [49.6%, 53.3%] | 0.51 | 1.73 | 1.73 |
| schemer | 8 | 6.26 | 1533 | 51.0% [49.2%, 52.8%] | 0.51 | 1.76 | 1.76 |
| loyalist | 8 | 5.53 | 1337 | 50.4% [48.5%, 52.3%] | 0.51 | 1.61 | 1.49 |
| floater | 8 | 6.77 | 1701 | 52.4% [50.7%, 54.1%] | 0.51 | 1.93 | 2.00 |
| beast | 8 | 7.39 | 3465 | 97.6% [97.1%, 98.1%] | 0.80 | 3.74 | 4.40 |
| novice | 8 | 5.68 | 1235 | 45.3% [43.5%, 47.2%] | 0.36 | 1.46 | 1.40 |
| exploit | 8 | 5.61 | 1372 | 51.0% [49.1%, 52.9%] | 0.51 | 1.59 | 1.59 |
| oracle-reader | 8 | 5.95 | 1455 | 51.0% [49.1%, 52.8%] | 0.51 | 1.64 | 1.68 |
| oracle-skilled | 8 | 5.63 | 1378 | 51.0% [49.1%, 52.9%] | 0.51 | 1.61 | 1.55 |
| passive | 12 | 6.23 | 1564 | 52.3% [50.5%, 54.1%] | 0.51 | 1.50 | 1.89 |
| random | 12 | 8.37 | 2050 | 51.0% [49.5%, 52.6%] | 0.51 | 2.16 | 2.33 |
| social | 12 | 8.77 | 2183 | 51.9% [50.4%, 53.4%] | 0.51 | 2.38 | 2.45 |
| reader | 12 | 9.77 | 2515 | 53.6% [52.2%, 55.1%] | 0.51 | 2.70 | 2.90 |
| schemer | 12 | 9.15 | 2345 | 53.4% [51.9%, 54.8%] | 0.51 | 2.54 | 2.65 |
| loyalist | 12 | 8.23 | 2074 | 52.5% [50.9%, 54.1%] | 0.51 | 2.25 | 2.33 |
| floater | 12 | 9.77 | 2355 | 50.2% [48.8%, 51.6%] | 0.51 | 2.63 | 2.56 |
| beast | 12 | 10.08 | 4794 | 99.0% [98.7%, 99.3%] | 0.80 | 4.52 | 6.12 |
| novice | 12 | 8.84 | 1843 | 43.5% [42.0%, 45.0%] | 0.37 | 1.92 | 2.13 |
| exploit | 12 | 7.69 | 1971 | 53.4% [51.8%, 55.0%] | 0.51 | 2.01 | 2.31 |
| oracle-reader | 12 | 8.59 | 2144 | 52.0% [50.5%, 53.5%] | 0.51 | 2.33 | 2.43 |
| oracle-skilled | 12 | 7.73 | 1934 | 52.2% [50.5%, 53.8%] | 0.51 | 1.99 | 2.24 |

### Competition by fixed performance (B4: the passive player, 60 seasons a level and size)

Carried from the first baseline (measured at `95e99bd2`): the passive player never convenes a war room or calls a
vote, so D3 does not change its seasons (its 480-season headline rates, 11.7% and 9.8%, sit on the first
baseline's 12% and 7% within their intervals).

| size | performance | played | won | win rate [Wilson] | field 3-5 | field 6-8 | field 9-11 | season win rate [Wilson] |
|---|---|---|---|---|---|---|---|---|
| 8 | 0.00 | 194 | 44 | 22.7% [17.4%, 29.1%] | 31.4% (35) | 20.8% (159) | - | 0.0% [0.0%, 6.0%] |
| 8 | 0.25 | 250 | 93 | 37.2% [31.4%, 43.3%] | 50.7% (67) | 32.2% (183) | - | 3.3% [0.9%, 11.4%] |
| 8 | 0.50 | 295 | 143 | 48.5% [42.8%, 54.2%] | 53.2% (94) | 46.3% (201) | - | 11.7% [5.8%, 22.2%] |
| 8 | 0.75 | 386 | 253 | 65.5% [60.7%, 70.1%] | 72.2% (158) | 61.0% (228) | - | 28.3% [18.5%, 40.8%] |
| 8 | 1.00 | 442 | 352 | 79.6% [75.6%, 83.1%] | 82.0% (206) | 77.5% (236) | - | 36.7% [25.6%, 49.3%] |
| 12 | 0.00 | 345 | 93 | 27.0% [22.5%, 31.9%] | 27.8% (18) | 35.1% (194) | 19.2% (73) | 3.3% [0.9%, 11.4%] |
| 12 | 0.25 | 328 | 122 | 37.2% [32.1%, 42.5%] | 66.7% (9) | 44.0% (191) | 26.5% (68) | 1.7% [0.3%, 8.9%] |
| 12 | 0.50 | 395 | 208 | 52.7% [47.7%, 57.5%] | 61.1% (36) | 57.4% (237) | 48.4% (62) | 8.3% [3.6%, 18.1%] |
| 12 | 0.75 | 489 | 337 | 68.9% [64.7%, 72.9%] | 79.2% (77) | 68.2% (296) | 75.0% (56) | 16.7% [9.3%, 28.0%] |
| 12 | 1.00 | 560 | 441 | 78.8% [75.2%, 81.9%] | 90.9% (110) | 74.4% (336) | 83.3% (54) | 40.0% [28.6%, 52.6%] |

### Competition concentration (all policies' seasons; NPC rows from the passive player's)

| size | Spearman NPC wins vs stat sum | top NPC's share of NPC wins | weekly comps won by the strongest on paper | mean winner rank on paper | field size |
|---|---|---|---|---|---|
| 8 | 0.126 | 37.8% | 45.4% | 2.13 | 5.3 |
| 12 | 0.119 | 28.2% | 45.3% | 2.18 | 6.4 |

### Pacts and deals per season

| policy | size | player pacts | NPC-only pacts | player deals (kept/broken) | NPC-only deals (kept/broken) | player promises (kept/broken) | oaths | weeks at the deal ceiling |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 0.00 | 1.35 | 0.39 (0.00/0.00) | 16.18 (4.46/0.22) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| random | 8 | 0.06 | 1.27 | 2.75 (0.25/0.33) | 16.24 (4.52/0.20) | 0.83 (0.29/0.33) | 0.01 | 0.00 |
| social | 8 | 0.38 | 1.12 | 3.80 (0.51/0.97) | 14.95 (4.23/0.21) | 3.04 (1.00/0.98) | 0.19 | 0.00 |
| reader | 8 | 0.25 | 1.16 | 4.96 (2.61/1.25) | 14.46 (4.10/0.20) | 0.46 (0.01/0.04) | 0.00 | 0.00 |
| schemer | 8 | 0.48 | 1.01 | 6.83 (2.76/1.80) | 11.11 (3.06/0.18) | 2.52 (1.02/0.83) | 0.00 | 0.00 |
| loyalist | 8 | 0.94 | 1.17 | 4.35 (0.80/0.08) | 14.59 (4.15/0.21) | 1.69 (0.18/0.01) | 0.86 | 0.00 |
| floater | 8 | 0.00 | 1.18 | 2.57 (0.00/0.00) | 15.91 (4.25/0.22) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| beast | 8 | 0.20 | 1.13 | 5.11 (0.03/0.12) | 18.54 (3.87/0.31) | 1.04 (0.02/0.04) | 0.00 | 0.00 |
| novice | 8 | 0.30 | 1.19 | 2.57 (0.67/0.20) | 15.12 (4.48/0.28) | 2.76 (1.11/0.79) | 0.19 | 0.00 |
| exploit | 8 | 0.13 | 1.19 | 1.05 (0.14/0.26) | 15.28 (4.46/0.19) | 2.40 (1.08/1.06) | 0.00 | 0.00 |
| oracle-reader | 8 | 0.51 | 1.19 | 5.55 (2.14/0.75) | 13.78 (3.78/0.21) | 0.43 (0.01/0.02) | 0.00 | 0.00 |
| oracle-skilled | 8 | 0.24 | 1.30 | 1.74 (0.30/0.40) | 15.94 (4.54/0.19) | 1.25 (0.03/0.16) | 0.02 | 0.00 |
| passive | 12 | 0.00 | 2.80 | 0.42 (0.00/0.00) | 57.30 (12.88/0.73) | 0.00 (0.00/0.00) | 0.00 | 0.76 |
| random | 12 | 0.13 | 2.65 | 4.01 (0.46/0.38) | 53.66 (12.20/0.70) | 1.04 (0.29/0.33) | 0.03 | 1.55 |
| social | 12 | 0.66 | 2.66 | 5.90 (0.98/0.83) | 53.74 (11.79/0.73) | 5.09 (1.55/1.32) | 0.47 | 1.94 |
| reader | 12 | 1.17 | 2.51 | 8.91 (4.02/1.98) | 47.83 (10.34/0.75) | 1.50 (0.02/0.08) | 0.00 | 2.22 |
| schemer | 12 | 1.07 | 2.39 | 11.73 (4.96/2.30) | 46.18 (9.98/0.56) | 4.31 (1.28/1.00) | 0.00 | 2.20 |
| loyalist | 12 | 1.19 | 2.65 | 4.92 (1.20/0.12) | 53.12 (11.53/0.87) | 3.15 (0.22/0.03) | 0.83 | 1.46 |
| floater | 12 | 0.00 | 2.59 | 4.05 (0.00/0.00) | 55.12 (11.86/0.76) | 0.00 (0.00/0.00) | 0.00 | 2.07 |
| beast | 12 | 0.24 | 2.64 | 7.79 (0.10/0.24) | 55.52 (9.96/0.77) | 1.29 (0.01/0.03) | 0.00 | 3.50 |
| novice | 12 | 0.67 | 2.66 | 5.33 (1.45/0.29) | 54.48 (12.37/0.73) | 4.04 (1.22/0.72) | 0.44 | 1.66 |
| exploit | 12 | 0.17 | 2.71 | 1.23 (0.23/0.23) | 56.81 (12.47/0.69) | 2.53 (0.87/1.01) | 0.04 | 1.33 |
| oracle-reader | 12 | 0.75 | 2.49 | 8.57 (2.73/1.15) | 51.87 (10.87/0.70) | 1.03 (0.00/0.01) | 0.00 | 1.92 |
| oracle-skilled | 12 | 0.42 | 2.76 | 3.19 (0.70/0.56) | 55.39 (12.40/0.72) | 2.03 (0.02/0.17) | 0.04 | 1.35 |

### Economy per season: window seats, bought time, Have-Nots, free actions

| policy | size | seats offered | spent | wasted | purchases | goodwill paid | Have-Not weeks | free actions |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 21.5 | 0.0 | 21.5 | 0.00 | 0.0 | 0.12 | 0.0 |
| random | 8 | 26.1 | 15.8 | 10.3 | 0.00 | 0.0 | 0.14 | 13.3 |
| social | 8 | 27.5 | 27.5 | 0.0 | 0.00 | 0.0 | 0.15 | 14.6 |
| reader | 8 | 26.5 | 13.9 | 12.6 | 0.00 | 0.0 | 0.17 | 29.6 |
| schemer | 8 | 28.6 | 28.6 | 0.0 | 3.45 | 27.6 | 0.13 | 16.8 |
| loyalist | 8 | 23.6 | 23.6 | 0.0 | 0.00 | 0.0 | 0.15 | 14.6 |
| floater | 8 | 28.7 | 28.7 | 0.0 | 0.00 | 0.0 | 0.18 | 13.4 |
| beast | 8 | 33.3 | 33.3 | 0.0 | 0.00 | 0.0 | 0.00 | 7.0 |
| novice | 8 | 23.8 | 12.2 | 11.6 | 0.00 | 0.0 | 0.23 | 13.1 |
| exploit | 8 | 40.1 | 40.1 | 0.0 | 20.91 | 167.3 | 0.12 | 61.8 |
| oracle-reader | 8 | 25.7 | 13.6 | 12.2 | 0.00 | 0.0 | 0.16 | 21.0 |
| oracle-skilled | 8 | 24.4 | 7.8 | 16.6 | 0.00 | 0.0 | 0.14 | 1.1 |
| passive | 12 | 29.8 | 0.0 | 29.8 | 0.00 | 0.0 | 0.06 | 0.0 |
| random | 12 | 40.0 | 23.8 | 16.3 | 0.00 | 0.0 | 0.09 | 21.6 |
| social | 12 | 42.1 | 42.1 | 0.0 | 0.00 | 0.0 | 0.11 | 25.5 |
| reader | 12 | 47.9 | 25.3 | 22.6 | 0.00 | 0.0 | 0.14 | 84.2 |
| schemer | 12 | 48.6 | 48.6 | 0.0 | 5.69 | 45.5 | 0.08 | 30.9 |
| loyalist | 12 | 39.5 | 39.5 | 0.0 | 0.00 | 0.0 | 0.08 | 23.5 |
| floater | 12 | 47.1 | 47.1 | 0.0 | 0.00 | 0.0 | 0.11 | 23.0 |
| beast | 12 | 50.2 | 50.2 | 0.0 | 0.00 | 0.0 | 0.00 | 11.0 |
| novice | 12 | 41.4 | 21.2 | 20.3 | 0.00 | 0.0 | 0.29 | 22.6 |
| exploit | 12 | 62.3 | 62.3 | 0.0 | 31.30 | 250.4 | 0.07 | 123.7 |
| oracle-reader | 12 | 42.1 | 21.3 | 20.8 | 0.00 | 0.0 | 0.08 | 62.7 |
| oracle-skilled | 12 | 37.1 | 11.7 | 25.4 | 0.00 | 0.0 | 0.09 | 2.1 |

### Game Sense by policy (bootstrap 95% for the mean)

| policy | size | Game Sense | competitions | strategy | social |
|---|---|---|---|---|---|
| passive | 8 | 56.1 [55.3, 56.9] | 82.2 | 52.7 | 34.4 |
| random | 8 | 63.0 [62.0, 63.8] | 88.5 | 64.5 | 35.3 |
| social | 8 | 68.7 [67.8, 69.6] | 90.5 | 72.7 | 41.5 |
| reader | 8 | 70.8 [69.8, 71.8] | 88.4 | 82.0 | 38.3 |
| schemer | 8 | 66.4 [65.4, 67.4] | 90.3 | 77.4 | 27.7 |
| loyalist | 8 | 68.6 [67.3, 69.9] | 84.1 | 73.7 | 46.4 |
| floater | 8 | 64.8 [64.1, 65.5] | 92.7 | 62.8 | 39.6 |
| beast | 8 | 73.5 [73.1, 74.0] | 99.7 | 79.4 | 39.4 |
| novice | 8 | 64.5 [63.3, 65.7] | 83.1 | 69.2 | 39.7 |
| exploit | 8 | 64.9 [63.9, 66.1] | 85.3 | 73.4 | 33.3 |
| oracle-reader | 8 | 70.5 [69.4, 71.6] | 88.6 | 81.2 | 38.2 |
| oracle-skilled | 8 | 64.3 [63.2, 65.6] | 86.0 | 69.2 | 36.3 |
| passive | 12 | 59.9 [59.4, 60.5] | 88.3 | 60.3 | 31.0 |
| random | 12 | 66.5 [65.8, 67.1] | 93.1 | 74.6 | 29.0 |
| social | 12 | 73.2 [72.5, 74.0] | 94.0 | 85.4 | 36.2 |
| reader | 12 | 76.3 [75.8, 76.8] | 96.8 | 96.7 | 28.7 |
| schemer | 12 | 70.9 [70.2, 71.5] | 95.2 | 91.3 | 19.3 |
| loyalist | 12 | 76.3 [75.5, 77.2] | 92.3 | 90.7 | 41.3 |
| floater | 12 | 67.2 [66.7, 67.7] | 97.6 | 71.1 | 31.7 |
| beast | 12 | 73.9 [73.5, 74.3] | 100.0 | 88.2 | 28.8 |
| novice | 12 | 72.0 [71.1, 72.7] | 91.9 | 84.6 | 35.1 |
| exploit | 12 | 68.9 [68.2, 69.6] | 91.6 | 83.8 | 26.5 |
| oracle-reader | 12 | 73.5 [72.8, 74.2] | 93.0 | 90.7 | 30.8 |
| oracle-skilled | 12 | 69.7 [69.0, 70.5] | 92.2 | 81.1 | 32.0 |

### Who goes out, NPC Heads of Household, and the jury (all policies' seasons)

| size | evictee's threat rank (HoH's view) | evictees in a pact | evictee's competition wins | NPC HoH nominated the top threat | jury margin | bitter jurors |
|---|---|---|---|---|---|---|
| 8 | 3.15 | 17.3% | 0.71 | 58.2% | 2.23 | 56.2% of jurors whose evictor reached the final two |
| 12 | 3.76 | 27.5% | 1.10 | 46.3% | 3.57 | 53.8% of jurors whose evictor reached the final two |

### The house: agendas and pairs, and the story's pace (all policies' seasons)

| house | NPC agendas as each social week closed | warmest pair (mutual, mean / max) | coldest pair (mutual, mean / min) |
|---|---|---|---|
| 8 | build 77.1%, hunt 15.3%, hold 6.8%, drift 0.8% | 155 / 200 | -85 / -200 |
| 12 | build 69.1%, hunt 24.8%, hold 5.6%, drift 0.5% | 191 / 200 | -129 / -200 |

| house | budgeted asks / season (target 4-6) | + must-fires | summons | play offers | weeks with a card (target 30-60%) | stories finished (target about 3) | moments | seasons with a pariah (target at most 20%) | NPC showmances | seasons with an NPC removal | seasons with a pile-on | story house events / season (autopsy) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 8 | 4.6 (max 9) | 0.2 | 0.5 | 3.2 | 48.9% | 4.4 | 8.1 | 17.2% | 1.02 | 0.0% | 0.1% | 12.5 |
| 12 | 8.2 (max 17) | 0.2 | 0.8 | 5.2 | 49.4% | 7.6 | 11.9 | 43.6% | 1.17 | 0.0% | 0.6% | 20.5 |

### Pacing: decisions and ceremony seconds a week

| policy | size | decisions / week (weeks in the house) | ceremony s / regular week | finale reveal s |
|---|---|---|---|---|
| passive | 8 | 4.0 | 44.8 | 21.8 |
| random | 8 | 14.4 | 44.7 | 21.8 |
| social | 8 | 17.6 | 44.4 | 21.8 |
| reader | 8 | 20.0 | 44.4 | 21.8 |
| schemer | 8 | 19.2 | 44.5 | 21.8 |
| loyalist | 8 | 18.3 | 44.5 | 21.8 |
| floater | 8 | 16.5 | 44.4 | 21.8 |
| beast | 8 | 15.7 | 42.9 | 21.8 |
| novice | 8 | 14.6 | 44.5 | 21.8 |
| exploit | 8 | 37.2 | 44.4 | 21.8 |
| oracle-reader | 8 | 16.7 | 44.9 | 21.8 |
| oracle-skilled | 8 | 9.6 | 44.9 | 21.8 |
| passive | 12 | 3.8 | 50.4 | 24.5 |
| random | 12 | 15.4 | 50.3 | 24.5 |
| social | 12 | 19.4 | 50.1 | 24.5 |
| reader | 12 | 25.5 | 49.9 | 24.5 |
| schemer | 12 | 20.6 | 50.0 | 24.5 |
| loyalist | 12 | 19.4 | 50.0 | 24.5 |
| floater | 12 | 17.3 | 50.0 | 24.5 |
| beast | 12 | 16.8 | 48.9 | 24.5 |
| novice | 12 | 15.3 | 50.1 | 24.5 |
| exploit | 12 | 45.8 | 50.1 | 24.5 |
| oracle-reader | 12 | 23.2 | 50.4 | 24.5 |
| oracle-skilled | 12 | 10.1 | 50.4 | 24.5 |

### Refusals and the walker

| policy | size | own commands / season | walker steps / season | refusals / season | most common refusal |
|---|---|---|---|---|---|
| passive | 8 | 0.0 | 91.7 | 0.00 | - |
| random | 8 | 47.1 | 87.4 | 0.00 | - |
| social | 8 | 64.0 | 88.2 | 0.00 | - |
| reader | 8 | 64.7 | 88.0 | 0.00 | - |
| schemer | 8 | 68.2 | 87.7 | 0.00 | - |
| loyalist | 8 | 56.8 | 87.4 | 0.01 | PromiseFinalTwo: This promise is already active. (6) |
| floater | 8 | 62.3 | 87.8 | 0.00 | - |
| beast | 8 | 68.3 | 88.9 | 0.00 | - |
| novice | 8 | 43.7 | 87.8 | 0.00 | - |
| exploit | 8 | 126.6 | 87.5 | 0.20 | RenameAlliance: The Outsiders has had its new name this week. (46) |
| oracle-reader | 8 | 50.0 | 91.6 | 0.01 | CallTheVote: The You, Emma and Riley Pact settles its call when it meets. (2) |
| oracle-skilled | 8 | 113.0 | 2.0 | 2.00 | AnswerJury: Choose one of the responses offered. (948) |
| passive | 12 | 0.0 | 149.5 | 0.00 | - |
| random | 12 | 71.0 | 145.1 | 0.00 | - |
| social | 12 | 100.5 | 145.9 | 0.00 | - |
| reader | 12 | 146.7 | 147.6 | 0.00 | - |
| schemer | 12 | 114.8 | 146.6 | 0.00 | - |
| loyalist | 12 | 94.0 | 145.6 | 0.33 | PromiseFinalTwo: This promise is already active. (157) |
| floater | 12 | 99.9 | 146.4 | 0.00 | - |
| beast | 12 | 101.6 | 148.0 | 0.00 | - |
| novice | 12 | 74.3 | 146.3 | 0.00 | - |
| exploit | 12 | 220.0 | 144.8 | 0.38 | RenameAlliance: The Outsiders has had its new name this week. (78) |
| oracle-reader | 12 | 108.6 | 151.9 | 0.02 | CallTheVote: The You, Alex and Casey Pact settles its call when it meets. (3) |
| oracle-skilled | 12 | 184.6 | 2.1 | 2.09 | AnswerJury: Choose one of the responses offered. (980) |

The oracles are the harnesses' own players and keep their own habits: the reader oracle still calls the vote in a
pact of three now and then, and the skilled oracle's first jury answer is one the finale does not offer (the lab's
walker answers with an offered one).

## What this baseline does not cover

- **Seed counts.** The headline is 480 a cell, not 800; the NPC budgets 100 (300), 50 (900, two players) and 15
  (1800, one player); the projections 400, 200 and 50. The full tier (every size and the All-Stars) has not run.
- **D2's all-week beats** are storage only (`allWeekRulesStartWeek` 0): the NPC world here is the 1 Hz
  conversation world alone. Walk-ins stay off (decision 4).
- **Human performance and time (B8).** The performance distributions and the 30 seconds a decision are
  assumptions.
- **Policy strength.** Each policy is one plausible script of its style; the oracles are not strong bounds.
- **Tuning (T1+).** Nothing here is tuned but D3's counter; the bands are the owner's.

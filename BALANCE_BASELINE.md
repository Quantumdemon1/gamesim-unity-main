# Balance baseline (B6c: every shipped rule on)

**Status: the baseline with everything a fresh season ships, and with the NPC world's arc fault fixed.** D2's all-week
NPC strategy is on from week one, D3's war rooms are played by the lab's policies at the amended reach, D4's leaks are
on, and a completed NPC conversation moves its pair through the ledger and writes no arc (the balance review's
finding 3, fixed behind D2's rule). The headline is 480 seasons per policy and size at NPC budget 0 (92 minutes in
twelve parts); the NPC budget is measured on B6b's cut grid (five players at 0 and 300 on 100 seeds; passive and
reader at 0 and 900 on 50; the reader at 0 and 1800 on 15); the projections at 0, 300 and 900 ticks; B4's
fixed-performance grid on 200 seeds a level and size. The per-policy performance distributions are assumptions until
human data exists (B8).
Every number here is reproducible from the commands below; treat differences inside the intervals as noise.

Measured on 2026-10-09 on `claude/arcs-balance-rerun` at `a91462b4` (base `claude/lead-integration` at `27b21b28`):
schema 28, competition rules 4, story rules 9, the economy, agency, the finale, the commitment rules, the unified
commitment and hearing version 1, D4's leaks, D3's war rooms and **D2's all-week rules** - exactly what the director
starts, because every lab season is built by `SeasonBuilder.Create` and `ShippedRules.ApplyFresh` (B0). The shipped
rules' tuple is pinned in `BalanceLabGoldens.Rules` (now `allWeekRulesStartWeek=1`). The three tiers the first pass
carried - NPC budget 1800, the projections at 900 and B4's fixed-performance grid - were measured later the same day
at `43af48c5`, whose code is `a91462b4`'s (the commit between them changed only this document and
`PLAYTEST_PROTOCOL.md`), with the parts' stamp v4. The review's two code fixes after them (`c64f4672`: the autonomy
QA allows the talk records their sequence ids; `818b36a1`: validation refuses a talk record in a season without D2's
rules or dated before them) change no season the lab plays: the budget-300 goldens' first parts at 8 and 12 pass
unchanged on `818b36a1`.

**Mode 1, then mode 2.** These tables were measured in mode 1 (unified commitment version 1). Since vote family V6
fresh seasons play mode 2; V6's paired 100-seed run at budget 0 (*History: vote family V6* below) found 2,233 of
2,400 seasons byte-identical and no finding's reading changed, so the tables stand for mode 2 until the next rerun.

## Whose numbers are these

Every table and finding below is this run's unless it says otherwise. Where an older number is quoted beside a new
one it is marked **B6b** (the previous baseline). What each earlier measurement predates:

| measurement | measured at | the lab's war-room policies (B6a) and D3's amended reach | D2's all-week rules | the arcs fix |
|---|---|---|---|---|
| **this baseline**: headline, NPC budget 0/300 and 0/900, projections at 0 and 300, studier, Q1-Q5 | `a91462b4` | yes | yes | yes |
| **this baseline, second pass**: NPC budget 0/1800, the projections at 900, *Competition by fixed performance* (B4) | `43af48c5` (the same code as `a91462b4`; only the documents moved) | yes | yes | yes |
| B6b: the previous headline, NPC grid (0/300, 0/900, 0/1800), projections and T0 diagnostics (quoted as "B6b") | `3f31fcd9` | yes | **no** | **no** |
| *D2's enable* (kept below as history) | `f047db13` / `c0090f07` | **no** (old reach, no war room answered) | on and off | not reached (budget 0 only) |
| B4's first fixed-performance grid (quoted as "first baseline" under its table) | `95e99bd2` | **no** | **no** | not reached (budget 0) |
| the first baseline (`6b18d59e`) | `95e99bd2` | **no** | **no** | **no** |

Nothing in this document is carried any more: every table is this baseline's, measured with every shipped rule on.
An older number appears only beside a new one, marked with where it came from.

The arcs fix only reaches seasons with an NPC world (a budget above 0): **every player outcome B6b measured at 300,
900 or 1800 ticks includes the arc artefact**, and the B6b caveat on F5 and B-4 is answered under F5 below. At
budget 0 the fix changes nothing; the budget-0 numbers move from B6b only by D2.

## What changed since B6b (`3f31fcd9`)

- **D2 is on in every lab season.** `ShippedRules.ApplyFresh` switches the all-week rules on from week one, so the
  house's turns spread over the week's four windows in every season the lab plays, at every budget.
- **The arcs fix** (`1c6ccc1f`). Under D2's rules a completed NPC conversation moves the pair through
  `RelationshipLedger.Move` and `Record` (fading records typed `npc-conversation` and `npc-gossip`) and writes no
  `relationshipArcs`; before them, and in every season saved before them, it went through `ChangeWithRoll`, which
  wrote the player's arc with both houseguests. The NPC world's stream is consumed exactly as before (the reciprocal's
  draw is drawn and set aside), and the season's own stream is never touched. The season's sequence counter does
  move: each record takes an id (two for a pair's talk that moved them, four for a gossip), so what is keyed to `nextSequence` -
  the talk story hooks, a refused deal's counter coin, the ids of house events, deals and promises - lands differently
  in a season with an NPC world (budget 0 plays none, so nothing moves there). The watched-season test now holds
  every NPC operation to no arc moved and to the sequence ids its talk records took, and nothing more.
- **Goldens re-recorded** (`a91462b4`): every one of the 48 cells moved with D2, and the budget-300 cells again with
  the arcs fix; `BudgetNoughtRows` is `fa8d4d420fad8953` (D2 alone: budget 0 plays no NPC world). The parts' stamp is
  v4, so no part played before the fix merges with one played after.

## History: what B6b changed since the first baseline (`6b18d59e`, measured at `95e99bd2`)

- **The war rooms are in play (B6a).** Since D3 went into `ApplyFresh` a pact of three or more calls only in its
  war room, and before B6a nothing in the lab convened one: the reader's and loyalist's calls were refused.
  Now the five engaged players (random, reader, schemer, loyalist, floater) convene a pact of three's war room
  once the block is set and answer its plan as each would (`GatedPolicy.WarRoom`, `AnswerPlan`): the loyalist
  goes with it, the schemer pushes against a plan that names an ally, the reader against a plan somebody who
  said it is torn or leaning on, the floater lies low, the random player tosses a coin. Nobody is refused a call
  in a smoke season any more.
- **D3's counter was amended in place** (the lead's decision 6): its reach is twenty at a view of ten or more
  (Loyal thirty), not eight at fifty (Loyal twelve). See *The war rooms*. No season outside the tests played the
  war rooms before main took D3 with the old reach (3f23e76b, PR #24, 2026-10-09 07:26). The odds are reckoned at
  the answer and never saved, so saves load either way, but a season started from main since then would change
  rules partway: the amendment needs no boundary only if no such season was saved (the owner confirms at
  landing); if one was, the constants move behind B9's tuning boundary instead.
- **The NPC world is driven (B5).** `NpcPairing` is the director's pairing loop, moved into the simulation (B5a);
  `BalanceLabNpcWorld` drives the 1 Hz world through `PrepareNpcOperation` alone at a budget of ticks a week
  (B5b). The headline stays at budget 0.
- **Seeds pair across budgets**, so each budget plays budget 0's seasons, and the coins a policy draws never see
  the NPC world's operations.
- **New measurements:** the war rooms (autopsy and lab), the first commitment the player saw settle, the
  competitions by week (B7), and the T0 diagnostics for the five tuning questions.
- **Goldens:** the 48 headline cells at budgets 0 and 300 are pinned (`BalanceLabGoldens`), and budget 0's smoke
  and 20-seed headline rows by `BalanceLabTests.BudgetNoughtRows`. Both are `[Explicit]` (minutes each), so no CI
  run checks them: the lead runs `BudgetNoughtPlaysTheSeasonsItPlayedBeforeTheDriver` and a golden slice by name
  after each merge that can move a lab season.

## How it was measured

- **Seasons.** Regular roster, 8 and 12. Each step the policy proposes up to three times, told each refusal;
  otherwise the lab's walker takes the phase's own step (a veto holder on the block saves themselves). Every
  competition's performance comes from the performance model. The state is validated after every Advance, at
  every phase change and at the end. **0 of 11,520 headline seasons had an error**, and 0 of every other tier's.
- **Policies** see only a `PlayerView`: the screens' readers and the controls they offer, including a war room's
  plan card (`OpenPlanCard`). Ten are knowledge-gated; two are labelled oracles (`oracle-reader`, `oracle-skilled`).
- **Performance**: a draw about each policy's mean - beast .8, novice .35, everyone else .5 - keyed to the
  season, week and phase, never the season's generator.
- **The house's turns** are D2's: every season plays the all-week beats on the season's own steps, whatever the
  NPC budget.
- **The NPC world** (`BalanceLabNpcWorld`, the lead's decisions 1-4): the budget's ticks a week, half in each
  free-time phase as it is played (the move-in night is a social phase of its own), spent in 60-tick slices
  before each decision and the rest before the Advance that closes the phase; on a scan due, `NpcPairing.Plan`
  with a lease cap of two; an approach arrives in five ticks and starts at the first free rendezvous. Ticks run
  only while the house is eligible (the player in the house, free time, no diary). Walk-ins stay off.
- **Metrics** from `SeasonAutopsy`, the story's pace from `StoryPacingTests.Pace`, the ceremonies from
  `CeremonyPacing`, and the T0 records (Game Sense's notes by face and row kind, each NPC nomination's weight in its
  terms, each season's first pariah, each member a counter could reach).

Reproduce (Unity-free, from the repository root; a tier longer than ten minutes is played in parts and merged):

```
dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabTests"                      # smoke and the lab's own tests
BALANCE_TIER=headline BALANCE_FROM=0 BALANCE_COUNT=40 BALANCE_PARTS=<dir> dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabParts.PartReport"
#   ... parts 40, 80, ... 440, then:
BALANCE_TIER=headline BALANCE_PARTS=<dir> dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabParts.MergeReport"
# the other tiers, the same way (BALANCE_TIER = npc, projection, competition, performance), each into its own BALANCE_PARTS:
BALANCE_NPC_POLICIES=passive,novice,social,reader,beast BALANCE_NPC_BUDGETS=0,300   # npc, 100 seeds, parts of 10
BALANCE_NPC_POLICIES=passive,reader BALANCE_NPC_BUDGETS=0,900                       # npc, 50 seeds, parts of 10
BALANCE_NPC_POLICIES=reader BALANCE_NPC_BUDGETS=0,1800                              # npc, 15 seeds, parts of 5
BALANCE_PROJECTION_BUDGETS=0 (400 seeds), =300 (200), =900 (50)                     # projection
BALANCE_TIER=competition, 200 seeds                                                 # the studier (T0 Q3)
BALANCE_TIER=performance, 200 seeds, parts of 50                                    # B4: the passive player at fixed performance
dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabGoldens.Budget300Size12First"   # one golden slice
dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BudgetNoughtPlaysTheSeasonsItPlayedBeforeTheDriver"   # budget 0's rows, about 5 min
```

Rows land as JSONL, and the tables as Markdown, in `Tools/SimulationTests/bin/Debug/net10.0/balance/`.

## Runtime

Measured on six threads shared with two or three other lanes' test hosts (the second pass beside another lane's
Unity editor).

| tier | seasons | wall time |
|---|---|---|
| the lab's tests in the subset (smoke and the watched season) | 91 smoke seasons and the lab's checks | about 80 s |
| headline, budget 0 | 11,520 (12 policies x 8, 12 x 480) | 92 min in 12 parts |
| NPC budget 0 and 300 | 2,000 (5 players x 8, 12 x 2 x 100) | 68 min in 10 parts |
| NPC budget 0 and 900 | 400 (passive, reader x 8, 12 x 2 x 50) | 31.5 min in 5 parts |
| the 900 control (the old completion path) | 400 | 38 min in 5 parts, beside the projections |
| NPC budget 0 and 1800 | 60 (reader x 8, 12 x 2 x 15) | 13.1 min in 3 parts |
| projections (novice x 6 houses) | 2,400 at 0, 1,200 at 300, 300 at 900 | 11.4 + 38.4 + 25.7 min |
| competition (T0 Q3) | 1,600 | 9.7 min in 4 parts |
| fixed performance (B4) | 2,000 (passive x 5 levels x 8, 12 x 200) | 9.8 min in 4 parts |
| goldens | 960 (48 cells x 20) | about 45 min in 8 tests |
| budget 0's rows (`BudgetNoughtRows`) | 564 (the smoke grid's 84 and the headline grid's 480) | about 5 min |

## Findings F1-F7

Each finding is this run's; **B6b**'s number (before D2 and the arcs fix) is in parentheses beside it.

**F1. Strategy moves win rate at both sizes now; at 12 even the passive player sits well above the base rate.**

- **At 8** (base rate 12.5%) the passive player wins 12.5% [9.8, 15.8] (B6b 11.7%). Against it, on the same seasons:
  - the floater wins 26.0% (McNemar p < 0.001; B6b 24.8%), the reader 19.6% (p = 0.003; B6b 15.2%, p = 0.12) and
    the loyalist 18.5% (p = 0.011; B6b 16.3%, p = 0.053);
  - social 15.6% (p = 0.18; B6b 17.1%, p = 0.021), random 14.2% and schemer 11.7% do not separate;
  - the novice wins 8.8% (p = 0.070; B6b 8.1%) and the exploit hunter 7.5% (p = 0.011; B6b 5.2%), still worse than
    doing nothing.
- **At 12** (base rate 8.3%) the passive player wins 14.8% [11.9, 18.2] (B6b 9.8% [7.4, 12.8]): D2's house seeks the
  newcomer out before the first nominations (F4), and doing nothing now lasts longer (still in the house after week
  four 55% of the time, B6b 36%). Every
  engaged gated style but random still beats it (p <= 0.001): reader 29.2%, schemer 27.3%, loyalist 27.1%, floater
  24.4%, social 22.9% (B6b 21.7-28.5%). Random's 15.6% no longer separates (p = 0.79; B6b p = 0.010), nor do the
  novice's 19.4% (p = 0.070; B6b 14.4%, p = 0.039) and the exploit hunter's 13.3%.
- **Between styles**: reader beats random at both sizes (p = 0.028 and < 0.001; B6b 0.035 and < 0.001) and social at
  12 (p = 0.034; B6b 0.017); social and loyalist beat random at 12 (p = 0.003 and < 0.001) but no longer at 8
  (p = 0.59 and 0.064; B6b 0.003 and 0.009).
- **The B-1 band** (a skilled gated policy wins at least 1.5x the base rate): at 12 all five skilled policies
  clear it with their whole interval (22.9-29.2%, low ends 19.4-25.3%; B6b 21.7-28.5%). **At 8 the floater clears
  it surely (26.0% [22.3, 30.1]) and the reader clears it, not surely (19.6% [16.3, 23.4])**; the loyalist (18.5%)
  sits just under 18.75%, social (15.6%) and the schemer (11.7%) fall short (B6b: the floater alone).

**F2. The oracles read hidden state; they no longer bound skill at all.**

- The oracle reader wins 19.4% at 8 and 33.3% at 12; the gated reader 19.6% and 29.2% (McNemar 75/74, p = 1.0 at
  8; 91/111, p = 0.18 at 12). B6b: 19.8% and 25.6% against 15.2% and 28.5% (p = 0.063 and 0.37). Reading the hidden
  state shows no edge at either size.
- The skilled oracle wins 17.7% and 28.5% (B6b 14.4% and 17.1%), level with the gated reader at both sizes (p = 0.52
  and 0.89; B6b below it at 12, p < 0.001). D2's own enable run saw the same rise at 12 (15% to 30%).

**F3. Competitions dominate, and preparation dominates competitions (T0 Q3).**

- At a performance of .5 a passive player wins 52.5% and 50.0% of the weekly competitions it plays (B6b 51.9% and
  52.3%). The beast (about .8, studying until prepared) wins 97.8% and 99.1% of them (B6b 97.6% and 99.0%) and
  **83.1% and 59.2% of seasons** (B6b 82.5% and 61.3%).
- **The studier** - the passive player who studies the house in each social week until its preparation is five,
  and does nothing else - at the same fixed performance, on the same seasons:

  | size | performance | passive: weekly wins / seasons | studier: weekly wins / seasons | studier vs passive (McNemar) | B6b: passive / studier seasons |
  |---|---|---|---|---|---|
  | 8 | .5 | 50.2% / 13.0% | 95.3% / 73.5% | 130/9, p < 0.001 | 9.5% / 64.0% |
  | 8 | .8 | 67.6% / 28.0% | 98.4% / 77.0% | 110/12, p < 0.001 | 23.5% / 66.5% |
  | 12 | .5 | 48.4% / 16.0% | 94.8% / 49.5% | 83/16, p < 0.001 | 9.0% / 45.5% |
  | 12 | .8 | 66.2% / 22.5% | 98.0% / 47.5% | 68/18, p < 0.001 | 15.5% / 44.5% |

  Five free-time seats of study (preparation +5, permanent) are still worth far more than playing the minigames
  well: they take an average player from about half the weekly competitions to 95%, and from a 13-16% season to
  50-74% (B6b 9% to 45-64%). Full marks alone (B4, now measured on 200 seeds with every rule on) win 78% of the
  weekly competitions at both sizes and 39% / 33% of seasons (first baseline, 60 seeds: 79-80% and 37% / 40%): an
  average player who studies beats a perfect one who does not, by 17 points of weekly wins and by 34.5 and 16.5
  points of seasons. With D2 the studier's seasons rose more at 8 (+9.5 points at .5) than the passive player's
  (+3.5).

- The field's strongest on paper wins 45.9% and 45.2% of weekly competitions (B6b 45.4% and 45.3%; a uniform draw
  is 19% and 16%).

**F4. The passive newcomer is no longer nominated more at 12, and only a little at 8 (T0 Q4).**

- Nominations per week in the house, player against the houseguests: passive 1.08 at 8 and **1.00 at 12** (B6b 1.13
  and 1.43); the novice 0.94 and 0.89 (B6b 1.03 and 1.03); the engaged gated policies 0.78-0.89 at 8 and 0.71-0.88
  at 12 (B6b 0.82-1.06); the exploit hunter 0.93 and 0.95 (B6b 1.01 and 1.21); the beast 0.43 and 0.69 (B6b 0.43
  and 0.67).
- **At 12 the change is the first week.** The passive player is nominated in week one 9.2% of the time (B6b 52.9%)
  and evicted in it 2.7% (B6b 12.3%): under D2 a close after the Head of Household's competition can reach the
  player, so the house comes to the newcomer before the first nominations. At 8 week one is unchanged (nominated
  40.8%, B6b 38.3%).
- **Why**, from every NPC Head of Household's nominations (the weight in its terms, the player against the mean
  other candidate; lower is put up first): for the passive player at 8 the Head of Household's view of them is 16
  points lower (-2.6 against +13.5; B6b 21 lower), the strategy windows' terms 28 lower (-23.3 against +5.1; B6b 32),
  the story's 9 lower (B6b 9), the threat 4 lower (B6b 3). At 12: view -15.6 (B6b -22.5), strategy terms -31.9
  (B6b -36.0), story -6.8 (B6b -6.5), threat -4.4 (B6b -4.3). The passive player at 8 is still ranked first or second
  for the block (mean rank 1.4 of 5.4; B6b 1.3) and goes up at 55% of NPC nominations (B6b 53%); at 12 it is ranked
  2.1 of 8.4 and goes up at 37%. Every policy's strategy terms are still negative (-13 to -59; B6b -20 to -60): the
  house's own deals and pleas single the player out more than any houseguest, and engaged play claws it back
  through the view and its own deals.
- Default (the lead's decision 7): no tuning; D2 has done most of what tuning would have at 12.

**F5. The NPC world, measured again with the arcs fixed (B5): the house deals and pairs off more, and the player's
outcomes do not move detectably at 300, 900 or 1800. B6b's 900-tick gains were the arc artefact.**

- **At 300** (the budget that stands for a human; five players, 8 and 12, 100 seeds, every metric paired with budget
  0): no win, final-two or out-by-week-3 rate moves (every McNemar p >= 0.06; B6b >= 0.10). NPC-only deals rise by
  3.0-8.5 a season (every interval excludes 0; B6b 3.8-8.0), NPC-only pacts by 0.13-0.48 (significant for all five
  players at 8, the reader and the beast at 12; B6b 0.1-0.4); the warmest pair warms by 8-28 at 8 and 3-9 at 12 (B6b
  14-27 at 8); pairs within ten of the bound go from about 1% to 2%; NPC conversations: 59-85 a season at 8 and
  125-148 at 12 (B6b 61-86 and 108-147). **The two nomination rates B6b saw move at 300 no longer do** (social at 8:
  0.432 to 0.422 a week, B6b 0.379 to 0.458*; reader at 12: 0.339 to 0.332, B6b 0.367 to 0.417*). What does move is
  small and specific: the beast places 0.29 lower and wins 0.31 fewer competitions (out by week 3, 3% to 8%,
  p = 0.063), the social player at 12 spends 0.9 weeks less in the house and 5.7 fewer seats, the reader at 12 gains
  1.5 Game Sense.
- **At 900** (passive and reader, 50 seeds): the house changes as before - NPC-only deals +8 a season at 8 and +17-22
  at 12 (B6b +9-22), pacts +0.4-0.7 (B6b +0.5), the warmest pair +42 at 8 and +16-17 at 12 - but **the player's
  outcomes no longer move detectably**: out by week 3 at 8, 60% to 54% for passive (p = 0.55) and 30% to 24% for the
  reader (p = 0.65) (B6b 56% to 34%, p = 0.035, and 38% to 12%, p = 0.004); the reader's final two at 8, 46% to 60%
  (p = 0.23; B6b 34% to 62%, p = 0.007); nominations a week -0.05 and -0.04, intervals over 0 (B6b -0.095* and
  -0.100*); Game Sense +0.1 for the reader at 8 (B6b +6.3*).
- **The control, to separate the fix from D2.** B6b had neither D2 nor the fix, so the same 900 seasons were played
  once more with D2 on and the old completion path (the fix mutated off in a build of its own, never committed). With
  the artefact back the B6b pattern returns: out by week 3 at 8, 60% to 42% for passive (p = 0.064) and 30% to 20% for
  the reader; the reader's final two 46% to 70% (p = 0.017); placement about a place better for both at 8 (-0.98* and
  -0.88*); the passive player's nominations a week -0.129*; Game Sense +2.6* (passive at 8) and +2.3* (reader at
  12). Paired season for season against the fixed run, the artefact keeps the player in the house longer in every
  cell (placement 0.3-0.7 better; passive at 12 out by week 3 in 11 of 50 seasons against 19, p = 0.021). The house's
  own numbers (deals, pacts, the warmest pair, saturation) are the same in both. So **F5's 900 result was the arc
  artefact, not NPC agency**: NPC chatter wrote friendship arcs with the player, which lowered the threat the house
  read on them and raised its votes for them. The fix removes it; the reader's final-two lean at 8 (46% to 60%) is
  inside the noise of 50 seeds, so a smaller real effect is not excluded.
- **At 1800** (the reader, 15 seeds, saturation's tier as in B6b): the house deals and pacts most - NPC-only deals
  +11 a season at 8 and +28 at 12 (B6b +16 and +35), pacts +0.9 and +1.2, broken NPC-only deals +0.5 and +0.9 -
  and no player outcome moves detectably (win 20% to 13% at 8 and 13% to 33% at 12, p = 1.0 and 0.38; final two
  40% to 47% and 20% to 47%, p = 1.0 and 0.22; Game Sense +2.1 and +3.9, intervals spanning 0). At 15 seeds only a
  large effect would show; the reader's lean at 12 (final two 20% to 47%, placement 2.2 better, neither significant)
  is the one to watch if 1800 is ever played on more seeds.
- **Saturation (risk 8)**: the share of houseguest pairs within ten of ±200 is about 1% at budget 0, 2% at 300,
  3-4% at 900 (B6b 3-5%) and 5.2-5.4% at 1800 (B6b 5.6-5.7%); at ±200 itself 2.5-2.8% at 900 and 4.0-5.1% at 1800
  (B6b 3.9-5.4%). The warmest pair's mean reaches 183 at 8 and 198 at 12 at 900, 191 and 199 at 1800 (B6b 194-195
  at 1800). As in B6b, 1800 measures a house beginning to saturate and 300 and 900 do not. The arcs fix was not
  expected to change it: for two houseguests the ledger moves each by the engine's own forward amount, where the
  engine moved the reciprocal by 0.8-1.2 of it, and arcs never fed the pair's scores.
- **B-4** (normalising NPC activity per week) can now be read without B6b's caveat: at the human budget the NPC world
  adds dealing and warmth inside the house and does not move the player's odds, and at 900 it still does not; 1800
  saturates the house a little more (5% of pairs near the bound) and shows no player effect on its 15 seeds.

**F6. Harness rules drift from what ships - resolved for the lab.** Every lab season starts through
`ShippedRules.ApplyFresh`; the 48 golden cells and the shipped rules' tuple are pinned, and this run re-recorded them
for D2 (and the budget-300 cells for the arcs fix) with the reason in the commit, as the rule asks. The older
harnesses (GameSense's default arm, the story sweeps) still build their own seasons.

**F7. E4 at 90% still needs six weeks with three testers at 8 (four with four, three with five); E3 as worded is
still out of reach for a novice-like tester (B7).**

- Under D2 fewer novices go out in week one at 8 (S(1) 11% against B6b's 24%): the house comes to the newcomer before
  the first nominations. By week three the rate is close to B6b's (41% against 46%), so the 90% weeks barely move:
  three testers week 6 (B6b 6), four week 4 (B6b 3), five week 3 (B6b 3). At 12, weeks 7, 6 and 5 (B6b 6, 5, 5).
- More novices see a commitment of theirs settle while in the house (the plateau 69% at 8 and 85% at 12; B6b 61% and
  79%), but every one of three testers seeing one never passes 33% at 8 and 61% at 12 (B6b 22% and 48%).
- The NPC world at 300 changes none of this (E4 weeks 6 / 4 / 3 at 8), nor does 900: its 50-seed rows read earlier
  (4 / 3 / 2 at 8), but the same 50 seeds at budget 0 read 5 / 3 / 3 and no house's survival or settle rate moves
  between them (every paired p >= 0.11). See *The projections* and the proposed *Session length* in
  `PLAYTEST_PROTOCOL.md`.

## The tuning questions (BALANCE plan §4), with what T0 measured

| question | measured (this run; B6b in parentheses) | what it decides | boundary |
|---|---|---|---|
| **Q1. D3's counter rarely moves anyone** | The lab's headline: mean odds 0.33 over the 875 members a counter could have faced at every plan answered (B6b 0.36 over 854), 0.16 over the 44 members at the 52 counters played (B6b 0.28 over 64); 9 came round (B6b 20), 35 of 52 counters carried (B6b 43 of 64). The digests' constructed trio (0.49, 13 members at counters played) is unchanged: the PactPlan digests passed as recorded. The lab's members view the player at a median of 27 (B6b 24) and lean by a median 10.8 (B6b 11). No candidate constants put the lab's counters inside 0.35-0.50; which population the band means is still the lead's to confirm. | Done (decision 6). If the owner wants more room, the shape (`ComeRoundOdds`) is the lever, not the constants. | none (in place) |
| **Q2. Game Sense gap** (target at least 10 at both sizes, low end over 5) | Today **9.6 [8.3, 10.8] at 8 and 8.5 [7.6, 9.2] at 12** (B6b 7.8 [6.5, 9.2] and 9.9 [9.1, 10.7]): **still not met at either size**, closer at 8 and further at 12. Replayed from each season's notes on the same seasons: weights 0.2/0.6/0.2 give 13.7 [12.1, 15.2] and 12.4 [11.4, 13.4] (B6b 11.1 and 13.9), both meet; dropping the competitions face (0/0.7/0.3) 15.7 and 14.3 (B6b 13.1 and 15.4); unbounded faces 17.4 and 35.8 (B6b 11.4 and 38.0); strategy notes x1.5 alone lowers it (8.8 and 6.5, the clamp). The gap is still strategy: unclaimed and claimed opportunities (+27.6 and +45.8 points a season; B6b +24.6 and +45.8), calls (+2.8 and +17.1; B6b +1.8 and +15.6), ballots (+2.6 and +9.4; B6b +1.4 and +9.5); the competitions face is at 90-96 for both and the clamp hides +19.8 at 12 (B6b +31.5). | Re-weighting the faces still meets the re-set target at both sizes; the weights are the cheapest lever. | B9 (`tuningRulesVersion`): any formula change moves the rules-off digests |
| **Q3. Competitions dominate** (beast 83.1% and 59.2% of seasons; B6b 82.5% and 61.3%) | Preparation, not performance: the studier at .5 wins 95% of weekly competitions and 73.5% / 49.5% of seasons against passive's 50% and 13.0% / 16.0% (B6b 64% / 45.5% against 9.5% / 9.0%); at .8 the passive player wins only 66-68% weekly. | The v5 candidate (preparation spent on use, cap 3) is aimed at the right lever; prototype it against the studier cells. | competitionRulesVersion 5 (B9 per the decisions) |
| **Q4. Passive newcomer nominated more** (now 1.08 and 1.00; B6b 1.13 and 1.43) | At 12 no longer: D2's close after the HoH reaches the newcomer before the first nominations (nominated in week one 9% against B6b's 53%). At 8 a little: the strategy windows' terms (-28 against the mean candidate; B6b -32) and a lower view (-16; B6b -21); the novice is at 0.94 and 0.89 (B6b 1.03). At 300 ticks nothing changes for passive (0.618 to 0.611 a week at 8, 0.456 to 0.442 at 12; B6b 0.61 to 0.59 and 0.60 to 0.61), and the two rates B6b saw move at 300 (social at 8, reader at 12) no longer do (F5). B6b had neither D2 nor the arcs fix and no control was run at 300, so this may be either of them, or B6b's noise; only at 900 does the control show the arc artefact. | Default: no tuning (decision 7); at 12 there is nothing left to tune. | B9 if tuned |
| **Q5. Story pariah rate** (16.8% at 8, 45.5% at 12; B6b 17.2% and 43.6%; target at most 20%) | Every pariah target had won a Head of Household (100% at both sizes; two or more 15% and 28%). The holders' grudges: nominated 52-53%, the veto's replacement 25-29%, a replacement 12%, voted against 3%. At 12 the run comes at week 5.9 with 7.1 in the house. The NPC world does not change it (15.0% and 44.6% at 300 against 14.4% and 43.2% at 0 on the same 500 seasons; B6b 11.4% and 42.0% at 300). | The pariah is a past Head of Household's nomination grudges, not repeat HoHs alone; for the story session: the nomination grudge (70, x1.2 allied), the replacement grudges, and a definition scaled by house size. | story rules 10 (story session) |

## The war rooms (D3)

Over the headline's 11,520 seasons (only the five engaged players convene one). "Members a counter would face" is
the reach at every plan the player answered, a counter or not; "at counters" is the counters actually played.

| policy | size | seasons with a plan | plans | agreed / countered / low | members a counter would face (mean odds) | at counters: reachable, mean odds, came round | counters carried | target evicted |
|---|---|---|---|---|---|---|---|---|
| random | 8 | 8 of 480 | 15 | 5 / 6 / 4 | 14 (0.23) | 6, 0.19, 2 | 5 of 6 | 10 of 15 |
| reader | 8 | 48 of 480 | 94 | 88 / 6 / 0 | 105 (0.36) | 5, 0.24, 1 | 4 of 6 | 82 of 94 |
| schemer | 8 | 30 of 480 | 47 | 40 / 7 / 0 | 37 (0.34) | 5, 0.00, 0 | 4 of 7 | 32 of 47 |
| loyalist | 8 | 26 of 480 | 38 | 35 / 3 / 0 | 38 (0.25) | 3, 0.18, 1 | 3 of 3 | 30 of 38 |
| random | 12 | 8 of 480 | 15 | 6 / 4 / 5 | 20 (0.28) | 5, 0.01, 0 | 2 of 4 | 10 of 15 |
| reader | 12 | 86 of 480 | 221 | 217 / 4 / 0 | 248 (0.39) | 2, 0.00, 0 | 3 of 4 | 152 of 221 |
| schemer | 12 | 100 of 480 | 235 | 221 / 14 / 0 | 255 (0.34) | 13, 0.22, 4 | 7 of 14 | 159 of 235 |
| loyalist | 12 | 51 of 480 | 139 | 131 / 8 / 0 | 158 (0.25) | 5, 0.23, 1 | 7 of 8 | 115 of 139 |

No plan lapsed or came to nothing; the player called 778 of the 804 plans (B6b 747 of 777). The floater, in no
pact, never convened one. Plans the player backs are the week's call; 73% of plans' targets went home that week
(B6b 71%). The reader convenes more at 8 (48 seasons, B6b 40) and the schemer fewer at 12 (100, B6b 117).

## The projections (B7)

From the novice (the first-timer model) in every house, at budget 0 (400 seeds), 300 (200) and 900 (50, the second
pass); B6b's figures in parentheses (B6b published no 900 row). S(k) is the chance a tester is out by week k; C(k)
the chance they saw a commitment of theirs settle (a deal or promise kept or broken whose ending they know, or a
war-room plan they answered) while still in the house.
The 4-house lasts two weeks (S(2) 19%, C 43%) and reaches no criterion.

| house | S(1) / S(3) / S(5) | C(2) / C(4) / C(plateau) | E4 (at least one of n out, 90%): n = 3 / 4 / 5 | E3 (all n saw one, 90%) | joint |
|---|---|---|---|---|---|
| 6 | 17% / 40% / - (27% / 46% / -) | 63% / 70% / 70% (58% / 63% / 63%) | week 4 / 4 / 3 (4 / 3 / 2) | never | never |
| 8 | 11% / 41% / 50% (24% / 46% / 53%) | 50% / 68% / 69% (47% / 59% / 61%) | week 6 / 4 / 3 (6 / 3 / 3) | never | never |
| 10 | 5% / 28% / 50% (4% / 31% / 56%) | 59% / 79% / 82% (45% / 67% / 71%) | week 6 / 5 / 4 (5 / 4 / 4) | never | never |
| 12 | 0.5% / 20% / 42% (2% / 23% / 46%) | 63% / 81% / 85% (42% / 72% / 79%) | week 7 / 6 / 5 (6 / 5 / 5) | never | never |
| All-Stars 8 | 4% / 29% / 41% (11% / 32% / 42%) | 57% / 78% / 80% (45% / 69% / 72%) | never / 6 / 5 (never / 6 / 5) | never | never |
| 8 at 300 ticks | 14% / 42% / 52% (25% / 45% / 55%) | 48% / 68.5% / 69.5% (46% / 60% / 62%) | week 6 / 4 / 3 (5 / 3 / 3) | never | never |
| 8 at 900 ticks (50 seeds) | 12% / 50% / 60% | 52% / 62% / 62% | week 4 / 3 / 2 | never | never |
| 8 at 0, the same 50 seeds | 10% / 52% / 60% | 48% / 66% / 68% | week 5 / 3 / 3 | never | never |
| 12 at 900 ticks (50 seeds) | 0% / 24% / 52% | 68% / 78% / 82% | week 6 / 5 / 4 | never | never |
| 12 at 0, the same 50 seeds | 0% / 20% / 46% | 72% / 88% / 88% | week 6 / 5 / 5 | never | never |

**At 900 ticks the projection does not move; its earlier weeks are its seeds.** The 900 rows play the first 50
seeds of the 400, and those 50 are a harsher sample: at budget 0 they put 52% of novices at 8 out by week 3, against
41% over all 400. Paired season for season with the same seeds at 0, no house's S(3), S(5), C(3) or C(5) moves at
900 (every McNemar p >= 0.11, at 4, 6, 8, 10, 12 and the All-Stars eight). Read E4 from the 400-seed rows; neither
budget moves the eight-house's survival (300 against 0 on the same 200 seeds: S(3) p = 0.90, S(5) p = 0.49). What
the NPC world does move, at 300 on 200 seeds, is the settle rate in the larger houses: by week five fewer novices at
10 and 12 have seen a commitment of theirs settle (C(5) 86% to 77.5% and 85% to 78.5%, p = 0.008 and 0.035; the
plateau 86% to 77.5% and 85.5% to 80%), and 900 leans the same way on its 50 seeds. That lowers E3's odds, which are
already out of reach as worded, and changes no E4 week.

The chance at least one tester is out by week k in the eight-house (budget 0; at 300 ticks in parentheses where it
matters):

| testers | week 1 | week 2 | week 3 | week 4 | week 5 | week 6 |
|---|---|---|---|---|---|---|
| 3 | 29.5% | 63.4% | 78.9% | 84.0% | 87.7% (300: 88.9%) | 95.5% |
| 4 | 37% | 74% | 87.5% | 91.4% | 94% | 98% |
| 5 | 44% | 81% | 92.5% | 95% | 97% | 99% |

- The best three testers can do on E3 as worded is 33% at 8 and 61% at 12 (B6b 22% and 48%): about three in ten
  novices at 8 (one in six at 12) never see a commitment of theirs settle while they are in the house (B6b four in
  ten and two in ten).
- The lab's own minutes a week in the house - ceremonies, the competitions played, decisions at 30 s each, and
  the NPC budget as free roam - are 8.5 at 8 (13.6 at 300 ticks, 23.7 at 900; B6b 8.7, 13.7 and 23.6), and
  22.2-24.0 across the houses at 900, so even fifteen minutes of free roam a week stays under E1's 30-45 minutes an
  episode: the time a person takes over a decision is the unknown, and B8 measures it. At E1's own pace an E4 session
  of three testers at 8 (six episode-weeks) is three to four and a half hours.

## The NPC world: key metrics against the budget

Each budget's seasons paired with budget 0's (the same seeds). A flag: rate, McNemar b/c and p. A number: mean,
and the mean difference with a bootstrap 95% interval (a star: the interval excludes 0). The last column is B6b's
(before D2 and the arcs fix), each against its own budget 0.

**At 300** (five players, 100 seeds a cell):

| metric | policy, size | 0 | 300 (vs 0) | B6b: 0 to 300 |
|---|---|---|---|---|
| win | reader 8 / 12 | 19% / 23% | 18% (16/17, p 1.0) / 26% (22/19, p 0.76) | 14% to 16% / 24% to 22% |
| win | social 8 / 12 | 14% / 18% | 14% (11/11, p 1.0) / 21% (18/15, p 0.73) | 19% to 12% / 23% to 24% |
| win | beast 8 / 12 | 86% / 55% | 82% (8/12, p 0.50) / 50% (12/17, p 0.46) | 81% to 84% / 58% to 52% |
| out by week 3 | passive 8 / 12 | 56% / 31% | 59% (10/7, p 0.63) / 33% (12/10, p 0.83) | 55% to 55% / 47% to 39% |
| out by week 3 | reader 8 / 12 | 32% / 20% | 29% (14/17, p 0.72) / 17% (9/12, p 0.66) | 36% to 27% / 11% to 15% |
| nominations a week | social 8 | 0.432 | 0.422 (-0.011 [-0.058, +0.032]) | 0.379 to 0.458 * |
| nominations a week | reader 12 | 0.339 | 0.332 (-0.007 [-0.041, +0.030]) | 0.367 to 0.417 * |
| mean placement | beast 8 | 1.29 | 1.58 (+0.29 [+0.05, +0.59] *) | - |
| weeks in the house | social 12 | 6.8 | 5.9 (-0.9 [-1.6, -0.1] *) | - |
| Game Sense | reader 12 | 74.9 | 76.4 (+1.5 [+0.3, +2.8] *) | - |
| NPC-only deals | passive 8 / 12 | 14.5 / 50.4 | 19.1 (+4.6 *) / 56.9 (+6.5 *) | 14.8 to 21.2 / 59.2 to 63.0 |
| NPC-only deals | reader 8 / 12 | 12.6 / 44.0 | 16.1 (+3.5 *) / 51.9 (+7.9 *) | 14.4 to 18.4 / 48.9 to 56.9 |
| NPC-only pacts | passive 8 / 12 | 1.46 / 2.81 | 1.79 (+0.33 *) / 3.00 (+0.19) | 1.21 to 1.63 / 2.78 to 3.12 |
| warmest pair | passive 8 / reader 8 | 132 / 141 | 160 (+28 *) / 163 (+22 *) | 134 to 161 / 151 to 175 |
| pairs within ten of ±200 | reader 8 / 12 | 0.7% / 1.1% | 1.4% * / 1.9% * | 1.0% to 2.3% / 1.3% to 1.9% |
| story: a pariah season | passive 8 / 12 | 19% / 49% | 24% (12/7, p 0.36) / 49% (20/20, p 0.87) | 16% to 27% / 58% to 58% |
| NPC conversations | reader 8 / 12 | 0 | 75 / 148 | 75 / 146 |

**At 900** (passive and reader, 50 seeds a cell), with the control: the same seasons with D2 on and the old
completion path that writes the player's arcs (the fix mutated off in a build of its own).

| metric | policy, size | 0 | 900, the fix (vs 0) | 900, the arc artefact (vs 0) | B6b: 0 to 900 |
|---|---|---|---|---|---|
| out by week 3 | passive 8 | 60% | 54% (4/7, p 0.55) | 42% (5/14, p 0.064) | 56% to 34% (p 0.035) |
| out by week 3 | reader 8 | 30% | 24% (8/11, p 0.65) | 20% (5/10, p 0.30) | 38% to 12% (p 0.004) |
| out by week 3 | passive 12 | 30% | 38% (9/5, p 0.42) | 22% (6/10, p 0.45) | - |
| final two | reader 8 | 46% | 60% (16/9, p 0.23) | 70% (17/5, p 0.017) | 34% to 62% (p 0.007) |
| mean placement | passive 8 / reader 8 | 5.40 / 3.76 | 5.08 (-0.32) / 3.22 (-0.54) | 4.42 (-0.98 *) / 2.88 (-0.88 *) | - |
| nominations a week | passive 8 | 0.684 | 0.629 (-0.054 [-0.117, +0.006]) | 0.555 (-0.129 [-0.197, -0.059] *) | 0.627 to 0.532 * |
| nominations a week | reader 8 | 0.463 | 0.428 (-0.036 [-0.110, +0.045]) | 0.394 (-0.069 [-0.137, +0.003]) | 0.506 to 0.407 * |
| Game Sense | passive 8 / reader 8 | 55.2 / 72.4 | 55.5 (+0.3) / 72.5 (+0.1) | 57.8 (+2.6 *) / 74.9 (+2.5) | - / 70.7 to 76.9 (+6.3 *) |
| win | reader 8 / 12 | 20% / 28% | 26% (12/9, p 0.66) / 20% (8/12, p 0.50) | 22% (6/5, p 1.0) / 26% (13/14, p 1.0) | 12% to 20% / 30% to 26% |
| NPC-only deals | reader 8 / 12 | 12.3 / 44.2 | 20.4 (+8.1 *) / 65.7 (+21.5 *) | 19.7 (+7.4 *) / 66.8 (+22.7 *) | 13.1 to 22.4 / 46.7 to 69.1 |
| NPC-only pacts | passive 8 / reader 8 | 1.38 / 1.26 | 2.10 (+0.72 *) / 1.80 (+0.54 *) | 2.04 (+0.66 *) / 1.66 (+0.40 *) | +0.5 |
| warmest pair | passive 8 / reader 8 | 140 / 143 | 182 (+42 *) / 185 (+42 *) | 183 (+44 *) / 185 (+42 *) | - |
| pairs within ten of ±200 | passive 8 / reader 8 | 0.7% / 1.0% | 3.5% * / 4.2% * | 3.7% * / 3.1% * | 0.4% to 4.8% / 0.8% to 5.5% |
| NPC conversations | reader 8 / 12 | 0 | 218 / 410 | 231 / 419 | - |

Paired season for season, the artefact against the fix at 900: out by week 3 for passive at 8, 21 of 50 against 27
(p = 0.15), at 12, 11 against 19 (p = 0.021); the reader's final two at 8, 35 against 30 (p = 0.38); mean placement
better under the artefact by 0.66 and 0.58 (passive at 8 and 12) and 0.34 (the reader at both).

**At 1800** (the reader, 15 seeds a cell: saturation's tier, as B6b cut it). B6b's column is what its text quoted;
it published no other 1800 figure.

| metric | size | 0 | 1800 (vs 0) | B6b: 0 to 1800 |
|---|---|---|---|---|
| win | 8 / 12 | 20.0% / 13.3% | 13.3% (1/2, p 1.0) / 33.3% (4/1, p 0.38) | - |
| final two | 8 / 12 | 40.0% / 20.0% | 46.7% (3/2, p 1.0) / 46.7% (5/1, p 0.22) | - |
| out by week 3 | 8 / 12 | 20.0% / 26.7% | 26.7% (2/1, p 1.0) / 20.0% (2/3, p 1.0) | - |
| mean placement | 8 / 12 | 3.60 / 7.40 | 3.73 (+0.13 [-0.80, +1.13]) / 5.20 (-2.20 [-4.53, +0.27]) | - |
| nominations a week | 8 / 12 | 0.417 / 0.350 | 0.506 (+0.089 [-0.028, +0.194]) / 0.345 (-0.005 [-0.096, +0.088]) | - |
| Game Sense | 8 / 12 | 72.9 / 72.1 | 75.1 (+2.1 [-2.1, +6.3]) / 75.9 (+3.9 [-1.5, +9.9]) | - |
| NPC-only deals | 8 / 12 | 12.0 / 45.4 | 23.2 (+11.2 *) / 73.4 (+28.0 *) | +16 / +35 |
| NPC-only pacts | 8 / 12 | 1.47 / 2.47 | 2.40 (+0.93 *) / 3.67 (+1.20 *) | - |
| warmest pair | 8 / 12 | 140 / 180 | 191 (+52 *) / 199 (+19 *) | 194-195 at 1800 |
| pairs within ten of ±200 | 8 / 12 | 0.6% / 1.1% | 5.4% * / 5.2% * | 5.6-5.7% at 1800 |
| pairs at ±200 | 8 / 12 | 0.6% / 0.7% | 5.1% / 4.0% | 3.9-5.4% at 1800 |
| NPC conversations | 8 / 12 | 0 | 415 / 793 | - |

**The cost.** An operation costs about 7.2-7.5 ms of engine time (`PrepareNpcOperation` validates the candidate and
installing it validates it again, as the director does). A season at 300 a week is about 1,650 operations (12.4 s of
engine), at 900 about 4,670 (33.5 s; 5,070 and 40.9 s with the artefact, which keeps more conversations going), at
1800 about 10,620 (88.6 s at 8.3 ms an operation, measured beside another lane's editor; B6b 10,700 and 72 s),
against about 2.8 thread-seconds for the whole season at budget 0.

## History: D2's enable, the same 2,400 seasons with the house's turns all week (2026-10-09)

*Measured on D2's lane, before it merged with the balance lane: at D3's old counter reach (8 at a view of 50), before
the lab's policies answered war rooms (B6a), at budget 0 (so before the arcs fix could matter), and on 100 seeds. Its
D2-off half is that lane's own 100-seed baseline. This run's headline replaces it as the measure of the game with D2
on; it is kept as the paired on/off evidence of what D2 alone moved.*

Measured at `f047db13` (branch `claude/d2-all-week`, on `claude/lead-integration` at `a74a261a`): the
headline tier at 100 seeds with D2 on, as `ShippedRules.ApplyFresh` ships it, and with D2's enable line taken
out (played at `c0090f07`; every change after it sits behind `EpisodeEngine.AllWeekOn`, so a season without
the rules plays the same at both). A lab season is a function of its house and index alone, so the halves pair
season for season: McNemar's b counts the seasons only the D2 player won, c those only the D2-off player won.
No season had an error in either half (0 of 2,400 each). Both halves were played in shards that each fit a
ten-minute tool limit, merged in the single run's order; on 96 seasons the merged rows and tables were
byte-identical to one run's, so `BALANCE_SEEDS=100 ... HeadlineReport` reproduces the D2-on half and writes its
full tables.

**As first built, D2 moved the balance; it was corrected before this measurement.** The first build let a
court of the Head of Household spend the week's building, holding or hunting, and kept every window's close
away from the player. On these seasons the house cooled (warmest pair 154 to 121 at 8, 191 to 156 at 12),
NPC-only deals halved, and the player won more: 236 to 285 of 1,200 at 8 (p = 0.006) and 288 to 348 at 12
(p = 0.005), the passive player at 12 from 7 to 21. The three corrections (a court no longer spends the week's
building; the close after the HoH or the nominations may reach the player; the pact rung is tried at a
houseguest's first beat of the week) are in ACTIONS-DEALS-ALLIANCES-PLAN's build log; the numbers below include
them.

**Win rate, D2 off to on, on the same seasons** (wins of 100; b/c; McNemar p):

| policy | at 8 | at 12 |
|---|---|---|
| passive | 12 to 15 (15/12, 0.700) | 7 to 17 (17/7, 0.064) |
| random | 7 to 13 (11/5, 0.210) | 20 to 13 (8/15, 0.210) |
| social | 19 to 14 (11/16, 0.441) | 23 to 18 (14/19, 0.486) |
| reader | 17 to 17 (16/16, 0.860) | 25 to 28 (16/13, 0.710) |
| schemer | 14 to 12 (9/11, 0.824) | 25 to 23 (18/20, 0.871) |
| loyalist | 17 to 21 (17/13, 0.584) | 31 to 26 (24/29, 0.583) |
| floater | 19 to 22 (20/17, 0.742) | 29 to 26 (21/24, 0.766) |
| beast | 81 to 86 (17/12, 0.458) | 58 to 55 (25/28, 0.784) |
| novice | 11 to 10 (8/9, 1.000) | 18 to 18 (15/15, 0.855) |
| exploit | 2 to 12 (12/2, 0.013) | 10 to 12 (10/8, 0.815) |
| oracle-reader | 22 to 16 (11/17, 0.345) | 27 to 30 (22/19, 0.755) |
| oracle-skilled | 15 to 15 (10/10, 1.000) | 15 to 30 (26/11, 0.021) |
| all twelve (of 1,200) | 236 to 253 (157/140, 0.353) | 288 to 296 (216/208, 0.734) |

**What moved, finding by finding:**
- **F1 holds.** The player's win rate over all policies does not move (p = 0.35 and 0.73). Two of the 24 cells
  move at p < 0.05, about what chance gives: the exploit hunter at 8, 2% to 12% (p = 0.013), no longer worse
  than doing nothing - it buys and spends more (seats 38.5 to 42.1 a season at 8 and 56.9 to 72.0 at 12,
  purchases 20 to 22 and 29 to 36) - and the skilled oracle at 12, 15% to 30% (p = 0.021). The passive player
  at 12 leans up, 7% to 17% (p = 0.064).
- **F2 and F3.** The oracle reader wins 16% and 30% against the gated reader's 17% and 28%: reading the hidden
  state shows no edge at either size (off, 22% against 17% at 8). Competitions are unchanged: the strongest on
  paper wins 46% and 45% of weekly competitions, the top NPC takes 40% and 28% of the NPC wins.
- **F4 is weaker at 12.** The passive player is on the block in week one 10% of the time instead of 55%, its
  nominations ratio is 1.05 instead of 1.42, and it is still in the house after week four 55% of the time
  against 34%. A close after the HoH can now reach the player (D2's decision 2), so the house seeks the
  newcomer out before the first nominations. At 8 it is unchanged (1.20 against 1.17).
- **F5.** The NPC world is still not driven, but D2's beats are the engine's, so every lab season plays them.
- **F7 is lower at 8.** The novice is evicted in week one 11% [6.3, 18.6] of the time instead of 19%; out by
  week three 43% against 44%; the chance that one of three testers is out by week one is 30% instead of 47%.
  At 12, 0% against 2%.
- **B-1.** At 12 four of the five skilled policies still clear 12.5% surely (23-28%) and the social player
  clears it at 18%, not surely. At 8 the loyalist (21%) and the floater (22%) clear 18.75%, not surely; the
  social player (14%), the reader (17%) and the schemer (12%) fall short: the band still fails at 8.
- **The house.** NPC-only pacts 1.11-1.49 a season at 8 (0.97-1.29 off) and 2.42-2.81 at 12 (2.46-2.82);
  NPC-only deals 9.3-15.9 at 8 (11.1-18.3) and 42.4-52.0 at 12 (46.7-59.2); warmest pair 152 and 189 (154,
  191), coldest -86 and -129 (-84, -128); evictees in a pact 19% and 28% (17%, 28%); the player's weeks at the
  deal ceiling at 12, 0.8-2.8 (0.7-3.4).
- **Game Sense.** Gated reader minus random 7.5 and 7.9 (7.7 and 9.8 off), against the target of 20.
- **The story's pace.** Budgeted asks 4.9 and 8.8 a season (4.8, 8.3); weeks with a card 52% and 53% (50%,
  50%); seasons with a pariah 17.7% and 44.5% (14.4%, 44.9%), so the twelve's rate is still more than twice its
  target; pile-ons 0.1% and 1.2% (0.2%, 0.5%).
- **Pacing** is unchanged: decisions a week within 0.7 for every policy but the exploit hunter (37.7 to 36.8 at
  8, 46.6 to 44.2 at 12), and the ceremonies within 0.2 s.

## History: vote family V6, the same 2,400 seasons under the unified vote rules (2026-10-09)

*Measured on V6's lane at 100 seeds, as D2's enable was, not the 480-seed tables below. No constant was tuned
(the lead's decision 9).*

Before is the lead's integration tip `27b21b28` (fresh seasons in mode 1: canonical Safety, raw vote promises
and deals); after is V6's `1c08f66d`, where `ShippedRules.ApplyFresh` sets `unifiedCommitmentRulesVersion` 2
(canonical Vote and Safety rows, the Rule2 overlap policy, D1's counting, the knowledge gate). Both halves are the
headline tier's first 100 seasons of every policy and house (indices 0-99), played in five parts of 20 and
merged (`BALANCE_TIER=headline BALANCE_FROM BALANCE_COUNT BALANCE_PARTS`, then `MergeReport`), so they pair
season for season; b counts the seasons only the mode-2 player won, c those only the mode-1 player won. No
season had an error in either half (0 of 2,400 each); 22.4 and 21.6 minutes on six threads, on a machine other
lanes shared, so the cost a season (2.9-3.9 s before, 3.1-3.4 s after, on one thread) shows no difference.

**Two harness gaps came first.** The first mode-2 run moved the player's deals because the lab could not see
them: `BalanceLabView.Offers` showed canonical offers only under `RulesOn` (mode 1), so in mode 2 a lab player
never saw a vote or Safety offer to answer (player deals kept 0.94 to 0.68 a season at 8 and 1.51 to 1.19 at 12,
broken 0.55 to 0.41 and 0.79 to 0.60, Game Sense's strategy -0.7 and -0.5, each beyond its interval), and the skilled oracle
(`StorySeasonTests.SkilledNext`) looked for offers in the raw deal list, which holds no Vote offer in mode 2.
The view now reads them wherever Safety is canonical (`SafetyAuthorityOn`), as the game's screens do, and the
oracle reads `CommitmentReferences.RawDeals`; modes 0 and 1 read exactly as before. The numbers below are after
both fixes.

**Win rate, mode 1 to mode 2, on the same seasons** (wins of 100; b/c; McNemar p):

| policy | at 8 | at 12 |
|---|---|---|
| passive | 15 to 15 (0/0, 1.000) | 17 to 17 (0/0, 1.000) |
| random | 14 to 15 (1/0, 1.000) | 13 to 13 (0/0, 1.000) |
| social | 14 to 16 (2/0, 0.500) | 18 to 21 (4/1, 0.375) |
| reader | 19 to 19 (0/0, 1.000) | 23 to 23 (0/0, 1.000) |
| schemer | 11 to 12 (1/0, 1.000) | 28 to 28 (3/3, 1.000) |
| loyalist | 20 to 20 (0/0, 1.000) | 26 to 26 (0/0, 1.000) |
| floater | 22 to 22 (0/0, 1.000) | 26 to 26 (0/0, 1.000) |
| beast | 86 to 86 (0/0, 1.000) | 55 to 55 (0/0, 1.000) |
| novice | 10 to 10 (0/0, 1.000) | 18 to 19 (3/2, 1.000) |
| exploit | 12 to 12 (0/0, 1.000) | 12 to 11 (0/1, 1.000) |
| oracle-reader | 16 to 16 (0/0, 1.000) | 30 to 30 (0/0, 1.000) |
| oracle-skilled | 15 to 15 (0/0, 1.000) | 30 to 30 (0/0, 1.000) |
| all twelve (of 1,200) | 254 to 258 (4/0, 0.125) | 296 to 299 (10/7, 0.629) |

**What moved.** 2,233 of the 2,400 seasons are byte-identical rows; the other 167 are the social player's (31 at
8, 30 at 12), the schemer's (20, 22), the novice's (22, 11), the exploit hunter's (13, 7) and the random
player's (6, 5). Seven of the twelve players play every season as before. One metric moves beyond its interval:
the player's broken deals, 0.55 to 0.54 a season at 8 (-0.006 [-0.012, -0.001]) and 0.79 to 0.78 at 12 (-0.009
[-0.018, -0.001]) - D1 and the Rule2 overlap policy: where one of the player's ballots breaks a vote promise and a
vote deal with the same houseguest, the group is one incident, so the seasons that met it count one breach
fewer. Every other paired metric is within its interval at both sizes (bootstrap 95% of the paired mean): final
two, placement, out by week 3, nominations a week, Game Sense and its strategy part (+0.002 and +0.009), the
player's deals and promises and their kept and broken counts, NPC-only deals (13.16 to 13.17, 49.15 to 49.17),
their kept and broken, NPC-only pacts (1.27, 2.66), the warmest and coldest pairs (within 0.1), the jury's
margin, bitter jurors, storylines, pariah seasons, war-room plans and the NPCs' nominations of the player.

**Why the seasons that differ differ.** A Rule2 group's non-owner rows write no line, memory or ledger record,
so a reveal that decided the player's vote promise and vote deal with one houseguest by one ballot writes fewer
records than mode 1 did. The season's `nextSequence` runs that many behind from then on, and the
story and house-event ids drawn after it move with it, so a keyed story chance (`StoryRandom`, keyed by those
ids, never the stream) can land the other way. Six of the 167 were traced command by command against the same
season in mode 1 (at 8: social 1 and 5, schemer 1, novice 2, exploit 12, random 32). Social 1 and 5 and schemer
1 part at such a reveal (their sequence three behind after it); in exploit 12 and random 32 the first different
command is a storyline beat whose house-event id is three lower in mode 2, the same shift, not traced back to its
reveal; novice 2 plays the same commands to the end and its row differs in the warmest pair (200 to 194.7) and
Game Sense's social part (36 to 35), as one consequence per Rule2 group and D1's scoring would move them (not
traced further). That is the designed overlap policy, not a stream leak; the other 161 were not traced one by
one.

**For the findings:** F1 holds (p = 0.13 and 0.63), and no finding's reading changes.

**Goldens.** `BalanceLabGoldens` (the rules tuple and all 48 cells, budgets 0 and 300) and
`BalanceLabTests.BudgetNoughtRows` are re-recorded at V6's tip with its harness fixes. They had not moved for D2's
enable either (the tuple still read `allWeekRulesStartWeek=0`), so the recording covers both.

**At the merge with the arcs fix** (on `claude/lead-integration`), the budget-0 goldens and `BudgetNoughtRows` are
V6's (budget 0 runs no NPC world, so the arcs fix cannot reach it), and the 24 budget-300 cells are re-recorded on
the merged tip, the first recording with both the unified vote rules and the arcs fix.

## The headline tables (budget 0, 480 seasons a cell)

All 11,520 seasons: the same 480 seasons for every policy at each size.

### Win rate by policy and size

Base rate is 1/size. McNemar pairs each policy with the passive player on the same seasons (b: only this policy won, c: only passive won).

| policy | size | n | wins | win rate | Wilson 95% | final two | mean placement | vs passive b/c | p |
|---|---|---|---|---|---|---|---|---|---|
| passive | 8 | 480 | 60 | 12.5% | [9.8%, 15.8%] | 24.0% | 4.95 | - | - |
| random | 8 | 480 | 68 | 14.2% | [11.3%, 17.6%] | 41.7% | 4.01 | 58/50 | 0.501 |
| social | 8 | 480 | 75 | 15.6% | [12.7%, 19.1%] | 42.7% | 3.74 | 63/48 | 0.184 |
| reader | 8 | 480 | 94 | 19.6% | [16.3%, 23.4%] | 50.8% | 3.49 | 78/44 | 0.003 |
| schemer | 8 | 480 | 56 | 11.7% | [9.1%, 14.8%] | 44.0% | 3.94 | 47/51 | 0.762 |
| loyalist | 8 | 480 | 89 | 18.5% | [15.3%, 22.3%] | 38.5% | 4.19 | 75/46 | 0.011 |
| floater | 8 | 480 | 125 | 26.0% | [22.3%, 30.1%] | 39.8% | 3.53 | 107/42 | 0.000 |
| beast | 8 | 480 | 399 | 83.1% | [79.5%, 86.2%] | 95.2% | 1.41 | 349/10 | 0.000 |
| novice | 8 | 480 | 42 | 8.8% | [6.5%, 11.6%] | 36.3% | 4.31 | 35/53 | 0.070 |
| exploit | 8 | 480 | 36 | 7.5% | [5.5%, 10.2%] | 37.1% | 4.23 | 29/53 | 0.011 |
| oracle-reader | 8 | 480 | 93 | 19.4% | [16.1%, 23.1%] | 36.5% | 4.11 | 79/46 | 0.004 |
| oracle-skilled | 8 | 480 | 85 | 17.7% | [14.6%, 21.4%] | 37.7% | 4.21 | 77/52 | 0.035 |
| passive | 12 | 480 | 71 | 14.8% | [11.9%, 18.2%] | 15.8% | 7.29 | - | - |
| random | 12 | 480 | 75 | 15.6% | [12.7%, 19.1%] | 22.3% | 6.30 | 66/62 | 0.791 |
| social | 12 | 480 | 110 | 22.9% | [19.4%, 26.9%] | 28.8% | 5.69 | 91/52 | 0.001 |
| reader | 12 | 480 | 140 | 29.2% | [25.3%, 33.4%] | 38.1% | 5.33 | 120/51 | 0.000 |
| schemer | 12 | 480 | 131 | 27.3% | [23.5%, 31.4%] | 36.7% | 5.41 | 111/51 | 0.000 |
| loyalist | 12 | 480 | 130 | 27.1% | [23.3%, 31.2%] | 30.6% | 6.06 | 114/55 | 0.000 |
| floater | 12 | 480 | 117 | 24.4% | [20.7%, 28.4%] | 27.1% | 5.53 | 102/56 | 0.000 |
| beast | 12 | 480 | 284 | 59.2% | [54.7%, 63.5%] | 62.3% | 4.25 | 238/25 | 0.000 |
| novice | 12 | 480 | 93 | 19.4% | [16.1%, 23.1%] | 31.7% | 5.81 | 78/56 | 0.070 |
| exploit | 12 | 480 | 64 | 13.3% | [10.6%, 16.7%] | 27.7% | 6.42 | 51/58 | 0.565 |
| oracle-reader | 12 | 480 | 160 | 33.3% | [29.3%, 37.7%] | 34.6% | 5.60 | 135/46 | 0.000 |
| oracle-skilled | 12 | 480 | 137 | 28.5% | [24.7%, 32.7%] | 33.3% | 5.89 | 112/46 | 0.000 |

### Further pairs

McNemar on the same seasons: b where only the first won, c where only the second did.

| first | second | size | first wins | second wins | b/c | p |
|---|---|---|---|---|---|---|
| reader | oracle-reader | 8 | 94 | 93 | 75/74 | 1.000 |
| reader | oracle-skilled | 8 | 94 | 85 | 81/72 | 0.518 |
| social | random | 8 | 75 | 68 | 64/57 | 0.585 |
| reader | random | 8 | 94 | 68 | 78/52 | 0.028 |
| loyalist | random | 8 | 89 | 68 | 69/48 | 0.064 |
| reader | social | 8 | 94 | 75 | 78/59 | 0.124 |
| novice | passive | 8 | 42 | 60 | 35/53 | 0.070 |
| exploit | random | 8 | 36 | 68 | 32/64 | 0.002 |
| reader | oracle-reader | 12 | 140 | 160 | 91/111 | 0.181 |
| reader | oracle-skilled | 12 | 140 | 137 | 102/99 | 0.888 |
| social | random | 12 | 110 | 75 | 84/49 | 0.003 |
| reader | random | 12 | 140 | 75 | 114/49 | 0.000 |
| loyalist | random | 12 | 130 | 75 | 119/64 | 0.000 |
| reader | social | 12 | 140 | 110 | 109/79 | 0.034 |
| novice | passive | 12 | 93 | 71 | 78/56 | 0.070 |
| exploit | random | 12 | 64 | 75 | 50/61 | 0.343 |

### The proposed B-1 band: a knowledge-gated skilled policy wins at least 1.5x the base rate

The bar is 1.5 / size. Clears: the win rate is at or over the bar; surely: the Wilson interval's low end is too.

| policy | size | bar | win rate [Wilson] | clears | surely |
|---|---|---|---|---|---|
| social | 8 | 18.8% | 15.6% [12.7%, 19.1%] | no | no |
| reader | 8 | 18.8% | 19.6% [16.3%, 23.4%] | yes | no |
| schemer | 8 | 18.8% | 11.7% [9.1%, 14.8%] | no | no |
| loyalist | 8 | 18.8% | 18.5% [15.3%, 22.3%] | no | no |
| floater | 8 | 18.8% | 26.0% [22.3%, 30.1%] | yes | yes |
| social | 12 | 12.5% | 22.9% [19.4%, 26.9%] | yes | yes |
| reader | 12 | 12.5% | 29.2% [25.3%, 33.4%] | yes | yes |
| schemer | 12 | 12.5% | 27.3% [23.5%, 31.4%] | yes | yes |
| loyalist | 12 | 12.5% | 27.1% [23.3%, 31.2%] | yes | yes |
| floater | 12 | 12.5% | 24.4% [20.7%, 28.4%] | yes | yes |

### Survival by week: the player still in the house after week k

| policy (8) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 |
|---|---|---|---|---|---|---|
| passive | 82.7% | 57.7% | 45.6% | 42.1% | 40.4% | 24.0% |
| random | 90.8% | 73.5% | 63.1% | 59.6% | 56.5% | 41.7% |
| social | 92.3% | 78.1% | 69.8% | 66.5% | 61.3% | 42.7% |
| reader | 92.5% | 80.0% | 74.2% | 69.2% | 65.2% | 50.8% |
| schemer | 91.3% | 76.5% | 64.8% | 61.5% | 56.0% | 44.0% |
| loyalist | 85.4% | 71.5% | 60.8% | 56.5% | 49.4% | 38.5% |
| floater | 94.8% | 81.7% | 72.1% | 68.8% | 64.2% | 39.8% |
| beast | 99.8% | 95.2% | 95.2% | 95.2% | 95.2% | 95.2% |
| novice | 88.5% | 71.5% | 59.6% | 54.0% | 50.0% | 36.3% |
| exploit | 85.4% | 72.5% | 62.1% | 57.9% | 54.4% | 37.1% |
| oracle-reader | 87.9% | 73.3% | 63.1% | 56.7% | 52.5% | 36.5% |
| oracle-skilled | 86.5% | 70.6% | 60.0% | 54.4% | 52.1% | 37.7% |

| policy (12) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 | wk 7 | wk 8 | wk 9 | wk 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| passive | 97.3% | 80.8% | 68.8% | 55.2% | 42.5% | 29.8% | 22.5% | 22.1% | 21.5% | 15.8% |
| random | 98.8% | 87.9% | 80.0% | 67.7% | 55.6% | 44.0% | 34.6% | 32.1% | 31.0% | 22.3% |
| social | 98.3% | 92.5% | 84.2% | 73.8% | 60.4% | 49.4% | 42.5% | 40.8% | 37.9% | 28.8% |
| reader | 99.8% | 90.6% | 83.5% | 73.1% | 64.0% | 54.8% | 47.1% | 45.2% | 41.7% | 38.1% |
| schemer | 100.0% | 91.3% | 83.5% | 73.1% | 62.9% | 51.5% | 46.0% | 45.0% | 41.5% | 36.7% |
| loyalist | 97.5% | 85.4% | 77.9% | 67.9% | 55.2% | 44.0% | 37.9% | 36.3% | 34.2% | 30.6% |
| floater | 99.6% | 92.7% | 86.3% | 75.8% | 66.3% | 53.5% | 41.9% | 40.2% | 39.0% | 27.1% |
| beast | 100.0% | 82.5% | 82.1% | 70.2% | 69.8% | 62.3% | 62.3% | 62.3% | 62.3% | 62.3% |
| novice | 99.4% | 88.8% | 80.8% | 70.4% | 60.0% | 51.5% | 40.8% | 39.6% | 36.9% | 31.7% |
| exploit | 97.5% | 86.0% | 77.1% | 64.8% | 52.1% | 40.8% | 33.5% | 32.7% | 32.1% | 27.7% |
| oracle-reader | 99.4% | 85.4% | 78.5% | 69.4% | 60.4% | 50.0% | 44.4% | 43.3% | 41.7% | 34.6% |
| oracle-skilled | 98.3% | 85.6% | 76.3% | 67.9% | 57.1% | 45.6% | 40.8% | 39.6% | 38.1% | 33.3% |

### Early risk: nominated and evicted in week one, out by week k, and the chance at least one of three testers is out

| policy | size | nominated wk 1 | evicted wk 1 [Wilson] | out by wk 2 | out by wk 3 | P(≥1 of 3 out) wk 1 / 2 / 3 |
|---|---|---|---|---|---|---|
| passive | 8 | 40.8% | 17.3% [14.2%, 20.9%] | 42.3% | 54.4% | 43.4% / 80.8% / 90.5% |
| random | 8 | 25.6% | 9.2% [6.9%, 12.1%] | 26.5% | 36.9% | 25.1% / 60.2% / 74.8% |
| social | 8 | 29.4% | 7.7% [5.6%, 10.4%] | 21.9% | 30.2% | 21.4% / 52.3% / 66.0% |
| reader | 8 | 26.3% | 7.5% [5.5%, 10.2%] | 20.0% | 25.8% | 20.9% / 48.8% / 59.2% |
| schemer | 8 | 29.2% | 8.8% [6.5%, 11.6%] | 23.5% | 35.2% | 24.0% / 55.3% / 72.8% |
| loyalist | 8 | 35.8% | 14.6% [11.7%, 18.0%] | 28.5% | 39.2% | 37.7% / 63.5% / 77.5% |
| floater | 8 | 29.0% | 5.2% [3.6%, 7.6%] | 18.3% | 27.9% | 14.8% / 45.5% / 62.5% |
| beast | 8 | 1.0% | 0.2% [0.0%, 1.2%] | 4.8% | 4.8% | 0.6% / 13.7% / 13.7% |
| novice | 8 | 40.4% | 11.5% [8.9%, 14.6%] | 28.5% | 40.4% | 30.6% / 63.5% / 78.8% |
| exploit | 8 | 33.3% | 14.6% [11.7%, 18.0%] | 27.5% | 37.9% | 37.7% / 61.9% / 76.1% |
| oracle-reader | 8 | 32.7% | 12.1% [9.5%, 15.3%] | 26.7% | 36.9% | 32.0% / 60.6% / 74.8% |
| oracle-skilled | 8 | 35.4% | 13.5% [10.8%, 16.9%] | 29.4% | 40.0% | 35.4% / 64.8% / 78.4% |
| passive | 12 | 9.2% | 2.7% [1.6%, 4.6%] | 19.2% | 31.3% | 7.9% / 47.2% / 67.5% |
| random | 12 | 7.1% | 1.3% [0.6%, 2.7%] | 12.1% | 20.0% | 3.7% / 32.0% / 48.8% |
| social | 12 | 10.0% | 1.7% [0.8%, 3.3%] | 7.5% | 15.8% | 4.9% / 20.9% / 40.4% |
| reader | 12 | 1.0% | 0.2% [0.0%, 1.2%] | 9.4% | 16.5% | 0.6% / 25.6% / 41.7% |
| schemer | 12 | 1.5% | 0.0% [0.0%, 0.8%] | 8.8% | 16.5% | 0.0% / 24.0% / 41.7% |
| loyalist | 12 | 16.3% | 2.5% [1.4%, 4.3%] | 14.6% | 22.1% | 7.3% / 37.7% / 52.7% |
| floater | 12 | 11.9% | 0.4% [0.1%, 1.5%] | 7.3% | 13.8% | 1.2% / 20.3% / 35.8% |
| beast | 12 | 0.0% | 0.0% [0.0%, 0.8%] | 17.5% | 17.9% | 0.0% / 43.8% / 44.7% |
| novice | 12 | 15.4% | 0.6% [0.2%, 1.8%] | 11.3% | 19.2% | 1.9% / 30.1% / 47.2% |
| exploit | 12 | 15.4% | 2.5% [1.4%, 4.3%] | 14.0% | 22.9% | 7.3% / 36.3% / 54.2% |
| oracle-reader | 12 | 4.0% | 0.6% [0.2%, 1.8%] | 14.6% | 21.5% | 1.9% / 37.7% / 51.5% |
| oracle-skilled | 12 | 8.5% | 1.7% [0.8%, 3.3%] | 14.4% | 23.8% | 4.9% / 37.2% / 55.7% |

### Nominations per week in the house (F4): the player against the houseguests of the same seasons

| policy | size | player | houseguests | ratio |
|---|---|---|---|---|
| passive | 8 | 0.564 | 0.522 | 1.08 |
| random | 8 | 0.436 | 0.534 | 0.82 |
| social | 8 | 0.416 | 0.522 | 0.80 |
| reader | 8 | 0.409 | 0.524 | 0.78 |
| schemer | 8 | 0.410 | 0.523 | 0.78 |
| loyalist | 8 | 0.464 | 0.520 | 0.89 |
| floater | 8 | 0.408 | 0.525 | 0.78 |
| beast | 8 | 0.204 | 0.477 | 0.43 |
| novice | 8 | 0.489 | 0.519 | 0.94 |
| exploit | 8 | 0.476 | 0.513 | 0.93 |
| oracle-reader | 8 | 0.455 | 0.540 | 0.84 |
| oracle-skilled | 8 | 0.470 | 0.537 | 0.88 |
| passive | 12 | 0.432 | 0.434 | 1.00 |
| random | 12 | 0.379 | 0.430 | 0.88 |
| social | 12 | 0.339 | 0.422 | 0.80 |
| reader | 12 | 0.322 | 0.435 | 0.74 |
| schemer | 12 | 0.309 | 0.434 | 0.71 |
| loyalist | 12 | 0.367 | 0.424 | 0.86 |
| floater | 12 | 0.364 | 0.428 | 0.85 |
| beast | 12 | 0.270 | 0.389 | 0.69 |
| novice | 12 | 0.380 | 0.426 | 0.89 |
| exploit | 12 | 0.403 | 0.425 | 0.95 |
| oracle-reader | 12 | 0.346 | 0.442 | 0.78 |
| oracle-skilled | 12 | 0.352 | 0.442 | 0.80 |

### The player's competitions by policy (weekly HoH and veto they played)

| policy | size | played / season | won | win rate [Wilson] | mean performance | HoH wins / season | veto wins / season |
|---|---|---|---|---|---|---|---|
| passive | 8 | 5.34 | 1346 | 52.5% [50.5%, 54.4%] | 0.50 | 1.43 | 1.61 |
| random | 8 | 6.23 | 1558 | 52.1% [50.3%, 53.9%] | 0.51 | 1.85 | 1.76 |
| social | 8 | 6.66 | 1621 | 50.7% [49.0%, 52.4%] | 0.51 | 1.92 | 1.85 |
| reader | 8 | 6.74 | 1706 | 52.7% [51.0%, 54.4%] | 0.51 | 2.00 | 1.98 |
| schemer | 8 | 6.41 | 1548 | 50.3% [48.5%, 52.0%] | 0.51 | 1.86 | 1.72 |
| loyalist | 8 | 6.11 | 1501 | 51.2% [49.3%, 53.0%] | 0.51 | 1.71 | 1.75 |
| floater | 8 | 6.80 | 1693 | 51.9% [50.2%, 53.6%] | 0.51 | 1.97 | 1.94 |
| beast | 8 | 7.59 | 3563 | 97.8% [97.3%, 98.3%] | 0.80 | 3.83 | 4.54 |
| novice | 8 | 6.17 | 1344 | 45.4% [43.6%, 47.2%] | 0.36 | 1.52 | 1.55 |
| exploit | 8 | 6.15 | 1551 | 52.5% [50.7%, 54.3%] | 0.51 | 1.74 | 1.83 |
| oracle-reader | 8 | 6.15 | 1505 | 51.0% [49.2%, 52.8%] | 0.51 | 1.77 | 1.70 |
| oracle-skilled | 8 | 6.03 | 1541 | 53.2% [51.4%, 55.0%] | 0.51 | 1.79 | 1.77 |
| passive | 12 | 7.56 | 1813 | 50.0% [48.3%, 51.6%] | 0.51 | 1.90 | 2.03 |
| random | 12 | 8.76 | 2189 | 52.0% [50.5%, 53.5%] | 0.51 | 2.26 | 2.51 |
| social | 12 | 9.44 | 2290 | 50.6% [49.1%, 52.0%] | 0.51 | 2.48 | 2.58 |
| reader | 12 | 9.71 | 2471 | 53.0% [51.6%, 54.4%] | 0.51 | 2.71 | 2.78 |
| schemer | 12 | 9.51 | 2406 | 52.7% [51.3%, 54.2%] | 0.51 | 2.65 | 2.70 |
| loyalist | 12 | 8.76 | 2137 | 50.8% [49.3%, 52.3%] | 0.51 | 2.38 | 2.38 |
| floater | 12 | 9.61 | 2350 | 50.9% [49.5%, 52.4%] | 0.51 | 2.57 | 2.59 |
| beast | 12 | 9.77 | 4647 | 99.1% [98.7%, 99.3%] | 0.80 | 4.37 | 5.93 |
| novice | 12 | 9.50 | 2091 | 45.9% [44.4%, 47.3%] | 0.36 | 2.19 | 2.46 |
| exploit | 12 | 8.59 | 2233 | 54.1% [52.6%, 55.7%] | 0.51 | 2.33 | 2.59 |
| oracle-reader | 12 | 9.27 | 2262 | 50.9% [49.4%, 52.3%] | 0.51 | 2.58 | 2.48 |
| oracle-skilled | 12 | 8.85 | 2170 | 51.1% [49.6%, 52.6%] | 0.51 | 2.50 | 2.35 |

### Competition by fixed performance (B4: the passive player, 200 seasons a level and size)

Measured in this baseline's second pass (`43af48c5`, every shipped rule on; 2,000 seasons in 9.8 minutes), on 200
seeds where the first baseline (`95e99bd2`, before the war rooms, D2 and the arcs fix) had 60. The passive player
never convenes a war room or calls a vote, and the NPC budget is 0, so of this baseline's changes only D2 reaches
these seasons. The .5 rows are the same seasons as the studier table's passive rows (F3) and match them exactly.
The last column is the first baseline's weekly and season win rates, on its 60 seasons.

| size | performance | played | won | win rate [Wilson] | field 3-5 | field 6-8 | field 9-11 | season win rate [Wilson] | first baseline: weekly / season |
|---|---|---|---|---|---|---|---|---|---|
| 8 | 0.00 | 742 | 185 | 24.9% [22.0%, 28.2%] | 36.4% (132) | 22.5% (610) | - | 1.0% [0.3%, 3.6%] | 22.7% / 0.0% |
| 8 | 0.25 | 859 | 308 | 35.9% [32.7%, 39.1%] | 47.2% (212) | 32.1% (647) | - | 5.5% [3.1%, 9.6%] | 37.2% / 3.3% |
| 8 | 0.50 | 1027 | 516 | 50.2% [47.2%, 53.3%] | 61.7% (339) | 44.5% (687) | - | 13.0% [9.0%, 18.4%] | 48.5% / 11.7% |
| 8 | 0.75 | 1204 | 793 | 65.9% [63.1%, 68.5%] | 74.6% (460) | 60.5% (744) | - | 26.0% [20.4%, 32.5%] | 65.5% / 28.3% |
| 8 | 1.00 | 1303 | 1015 | 77.9% [75.6%, 80.1%] | 84.5% (563) | 72.8% (740) | - | 39.0% [32.5%, 45.9%] | 79.6% / 36.7% |
| 12 | 0.00 | 1352 | 314 | 23.2% [21.1%, 25.5%] | 39.6% (91) | 29.4% (754) | 11.7% (307) | 1.0% [0.3%, 3.6%] | 27.0% / 3.3% |
| 12 | 0.25 | 1464 | 521 | 35.6% [33.2%, 38.1%] | 58.7% (138) | 38.8% (846) | 26.4% (280) | 10.0% [6.6%, 14.9%] | 37.2% / 1.7% |
| 12 | 0.50 | 1516 | 734 | 48.4% [45.9%, 50.9%] | 68.2% (173) | 49.3% (902) | 46.1% (241) | 16.0% [11.6%, 21.7%] | 52.7% / 8.3% |
| 12 | 0.75 | 1425 | 878 | 61.6% [59.1%, 64.1%] | 76.2% (189) | 60.9% (846) | 62.1% (190) | 19.5% [14.6%, 25.5%] | 68.9% / 16.7% |
| 12 | 1.00 | 1574 | 1228 | 78.0% [75.9%, 80.0%] | 90.2% (286) | 74.5% (929) | 84.3% (159) | 33.0% [26.9%, 39.8%] | 78.8% / 40.0% |

The weekly curve keeps its shape - about 24% at nought, 50% at .5 and 78% at full marks, at both sizes - and sits
inside the first baseline's intervals everywhere but 12 at .75 (61.6% against 68.9% [64.7%, 72.9%]). The season curve is steeper at 8 than at 12: at 12 the passive player gains little from .5 to
.75 (16.0% to 19.5%) and full marks win a third of seasons, against the studier's 49.5% at .5 (F3). At 12 the season
rates sit above the first baseline's at .25 and .5 (10.0% and 16.0% against 1.7% and 8.3%, each one to five seasons
of 60 there), in line with the passive player's headline rise at 12 under D2 (F1, F4); this grid was not played
with D2 off, so it does not separate D2 from the first baseline's small samples.

### Competition concentration (all policies' seasons; NPC rows from the passive player's)

| size | Spearman NPC wins vs stat sum | top NPC's share of NPC wins | weekly comps won by the strongest on paper | mean winner rank on paper | field size |
|---|---|---|---|---|---|
| 8 | 0.068 | 39.5% | 45.9% | 2.12 | 5.3 |
| 12 | 0.149 | 28.8% | 45.2% | 2.18 | 6.4 |

### Pacts and deals per season

| policy | size | player pacts | NPC-only pacts | player deals (kept/broken) | NPC-only deals (kept/broken) | player promises (kept/broken) | oaths | weeks at the deal ceiling |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 0.00 | 1.45 | 0.57 (0.00/0.00) | 14.15 (4.01/0.19) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| random | 8 | 0.06 | 1.37 | 2.86 (0.29/0.30) | 13.39 (3.66/0.21) | 0.94 (0.35/0.38) | 0.03 | 0.00 |
| social | 8 | 0.38 | 1.27 | 4.04 (0.50/1.00) | 13.77 (3.93/0.17) | 3.39 (1.13/1.13) | 0.19 | 0.00 |
| reader | 8 | 0.34 | 1.28 | 5.87 (2.94/1.40) | 12.31 (3.53/0.17) | 0.58 (0.02/0.06) | 0.00 | 0.00 |
| schemer | 8 | 0.55 | 1.10 | 7.42 (3.06/1.76) | 8.84 (2.34/0.16) | 2.84 (1.14/1.02) | 0.00 | 0.00 |
| loyalist | 8 | 0.94 | 1.30 | 4.68 (0.87/0.09) | 12.78 (3.56/0.16) | 1.80 (0.22/0.05) | 0.85 | 0.00 |
| floater | 8 | 0.00 | 1.31 | 2.84 (0.00/0.00) | 14.02 (3.95/0.21) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| beast | 8 | 0.19 | 1.32 | 5.29 (0.03/0.12) | 16.32 (3.69/0.25) | 0.99 (0.01/0.04) | 0.00 | 0.00 |
| novice | 8 | 0.31 | 1.30 | 2.89 (0.82/0.20) | 13.16 (3.89/0.19) | 3.11 (1.24/0.98) | 0.26 | 0.00 |
| exploit | 8 | 0.13 | 1.26 | 1.18 (0.16/0.26) | 13.59 (3.77/0.21) | 2.76 (1.21/1.23) | 0.04 | 0.00 |
| oracle-reader | 8 | 0.51 | 1.26 | 6.19 (2.18/0.85) | 11.31 (3.27/0.18) | 0.50 (0.01/0.03) | 0.00 | 0.00 |
| oracle-skilled | 8 | 0.28 | 1.42 | 2.24 (0.35/0.48) | 13.12 (3.58/0.19) | 1.36 (0.04/0.18) | 0.02 | 0.00 |
| passive | 12 | 0.00 | 2.75 | 0.93 (0.00/0.00) | 51.09 (11.52/0.78) | 0.00 (0.00/0.00) | 0.00 | 0.78 |
| random | 12 | 0.11 | 2.74 | 4.59 (0.45/0.46) | 50.39 (11.65/0.65) | 1.28 (0.40/0.41) | 0.04 | 1.45 |
| social | 12 | 0.63 | 2.67 | 6.51 (1.10/1.00) | 50.50 (11.54/0.64) | 5.87 (1.77/1.80) | 0.50 | 1.89 |
| reader | 12 | 1.19 | 2.54 | 9.45 (4.18/2.11) | 43.96 (9.76/0.64) | 1.44 (0.02/0.09) | 0.00 | 1.99 |
| schemer | 12 | 1.06 | 2.50 | 12.72 (5.35/2.62) | 42.08 (9.13/0.50) | 5.23 (1.70/1.36) | 0.00 | 2.17 |
| loyalist | 12 | 1.28 | 2.58 | 5.42 (1.48/0.14) | 48.85 (10.91/0.80) | 3.36 (0.26/0.05) | 0.87 | 1.52 |
| floater | 12 | 0.00 | 2.71 | 4.09 (0.00/0.00) | 51.34 (11.14/0.74) | 0.00 (0.00/0.00) | 0.00 | 1.77 |
| beast | 12 | 0.24 | 2.59 | 7.46 (0.08/0.19) | 50.69 (9.96/0.85) | 1.25 (0.01/0.03) | 0.00 | 2.98 |
| novice | 12 | 0.73 | 2.72 | 6.03 (1.73/0.34) | 49.56 (11.52/0.67) | 5.08 (1.83/1.17) | 0.51 | 1.83 |
| exploit | 12 | 0.23 | 2.71 | 2.12 (0.40/0.35) | 51.75 (11.54/0.70) | 3.54 (1.30/1.40) | 0.08 | 1.38 |
| oracle-reader | 12 | 0.90 | 2.48 | 10.21 (3.23/1.30) | 47.09 (10.00/0.63) | 1.24 (0.00/0.02) | 0.00 | 2.13 |
| oracle-skilled | 12 | 0.60 | 2.68 | 4.61 (1.02/0.87) | 49.24 (10.86/0.72) | 2.61 (0.03/0.21) | 0.08 | 1.60 |

### Economy per season: window seats, bought time, Have-Nots, free actions

| policy | size | seats offered | spent | wasted | purchases | goodwill paid | Have-Not weeks | free actions |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 22.8 | 0.0 | 22.8 | 0.00 | 0.0 | 0.12 | 0.0 |
| random | 8 | 26.8 | 16.0 | 10.8 | 0.00 | 0.0 | 0.15 | 11.9 |
| social | 8 | 28.1 | 28.1 | 0.0 | 0.00 | 0.0 | 0.18 | 13.8 |
| reader | 8 | 29.4 | 15.2 | 14.2 | 0.00 | 0.0 | 0.14 | 31.4 |
| schemer | 8 | 29.6 | 29.6 | 0.0 | 3.59 | 28.7 | 0.14 | 16.3 |
| loyalist | 8 | 25.6 | 25.6 | 0.0 | 0.00 | 0.0 | 0.15 | 14.5 |
| floater | 8 | 28.9 | 28.9 | 0.0 | 0.00 | 0.0 | 0.17 | 12.7 |
| beast | 8 | 34.0 | 34.0 | 0.0 | 0.00 | 0.0 | 0.00 | 7.0 |
| novice | 8 | 25.6 | 13.2 | 12.4 | 0.00 | 0.0 | 0.27 | 12.6 |
| exploit | 8 | 43.5 | 43.5 | 0.0 | 22.68 | 181.4 | 0.13 | 64.8 |
| oracle-reader | 8 | 26.7 | 14.0 | 12.7 | 0.00 | 0.0 | 0.15 | 21.5 |
| oracle-skilled | 8 | 26.0 | 8.4 | 17.7 | 0.00 | 0.0 | 0.11 | 1.4 |
| passive | 12 | 37.0 | 0.0 | 37.0 | 0.00 | 0.0 | 0.09 | 0.0 |
| random | 12 | 42.4 | 25.2 | 17.1 | 0.00 | 0.0 | 0.11 | 19.4 |
| social | 12 | 45.2 | 45.2 | 0.0 | 0.00 | 0.0 | 0.13 | 24.6 |
| reader | 12 | 47.6 | 25.2 | 22.4 | 0.00 | 0.0 | 0.10 | 83.7 |
| schemer | 12 | 50.8 | 50.8 | 0.0 | 5.95 | 47.6 | 0.09 | 29.5 |
| loyalist | 12 | 42.4 | 42.4 | 0.0 | 0.00 | 0.0 | 0.11 | 22.0 |
| floater | 12 | 46.3 | 46.3 | 0.0 | 0.00 | 0.0 | 0.11 | 20.8 |
| beast | 12 | 48.8 | 48.8 | 0.0 | 0.00 | 0.0 | 0.00 | 11.0 |
| novice | 12 | 44.5 | 22.7 | 21.8 | 0.00 | 0.0 | 0.26 | 21.8 |
| exploit | 12 | 70.2 | 70.2 | 0.0 | 35.08 | 280.6 | 0.10 | 133.3 |
| oracle-reader | 12 | 45.9 | 23.3 | 22.6 | 0.00 | 0.0 | 0.08 | 65.9 |
| oracle-skilled | 12 | 44.3 | 14.2 | 30.0 | 0.00 | 0.0 | 0.08 | 3.1 |

### Game Sense by policy (bootstrap 95% for the mean)

| policy | size | Game Sense | competitions | strategy | social |
|---|---|---|---|---|---|
| passive | 8 | 57.2 [56.4, 57.9] | 85.6 | 52.8 | 34.5 |
| random | 8 | 64.0 [63.2, 64.9] | 90.8 | 64.9 | 36.1 |
| social | 8 | 68.7 [67.8, 69.5] | 91.8 | 72.4 | 40.8 |
| reader | 8 | 73.6 [72.7, 74.5] | 92.1 | 86.7 | 37.7 |
| schemer | 8 | 67.5 [66.5, 68.4] | 90.5 | 79.6 | 28.4 |
| loyalist | 8 | 71.4 [70.1, 72.5] | 88.6 | 77.7 | 45.6 |
| floater | 8 | 65.0 [64.3, 65.6] | 93.5 | 62.4 | 39.9 |
| beast | 8 | 72.8 [72.4, 73.2] | 99.7 | 78.6 | 38.3 |
| novice | 8 | 67.1 [66.1, 68.3] | 87.7 | 72.3 | 39.7 |
| exploit | 8 | 66.2 [65.2, 67.2] | 89.0 | 75.4 | 31.1 |
| oracle-reader | 8 | 71.7 [70.6, 72.7] | 89.4 | 83.2 | 38.5 |
| oracle-skilled | 8 | 66.1 [65.1, 67.2] | 88.5 | 72.1 | 35.7 |
| passive | 12 | 60.6 [60.0, 61.1] | 91.5 | 60.0 | 30.3 |
| random | 12 | 67.9 [67.3, 68.6] | 95.6 | 76.1 | 29.3 |
| social | 12 | 74.2 [73.6, 74.7] | 96.2 | 87.8 | 33.9 |
| reader | 12 | 76.3 [75.8, 76.9] | 96.3 | 96.4 | 29.7 |
| schemer | 12 | 71.8 [71.2, 72.4] | 96.6 | 92.7 | 19.0 |
| loyalist | 12 | 77.7 [77.0, 78.4] | 94.0 | 92.9 | 41.1 |
| floater | 12 | 68.1 [67.6, 68.5] | 97.7 | 72.4 | 32.7 |
| beast | 12 | 73.4 [73.0, 73.8] | 99.9 | 87.0 | 28.8 |
| novice | 12 | 73.7 [72.9, 74.4] | 93.3 | 88.0 | 35.0 |
| exploit | 12 | 70.9 [70.3, 71.5] | 94.8 | 88.6 | 23.5 |
| oracle-reader | 12 | 75.5 [74.9, 76.1] | 94.8 | 94.3 | 31.1 |
| oracle-skilled | 12 | 71.6 [70.9, 72.4] | 93.5 | 85.4 | 31.3 |

Size 8: gated reader minus random 9.6; oracle reader minus random 7.6 (target 20).

Size 12: gated reader minus random 8.5; oracle reader minus random 7.6 (target 20).

### Who goes out, NPC Heads of Household, and the jury (all policies' seasons)

| size | evictee's threat rank (HoH's view) | evictees in a pact | evictee's competition wins | NPC HoH nominated the top threat | jury margin | bitter jurors |
|---|---|---|---|---|---|---|
| 8 | 3.16 | 19.8% | 0.69 | 56.6% | 2.19 | 57.3% of jurors whose evictor reached the final two |
| 12 | 3.80 | 28.2% | 1.08 | 45.4% | 3.55 | 55.0% of jurors whose evictor reached the final two |

### The house: agendas and pairs, and the story's pace (all policies' seasons)

| house | NPC agendas as each social week closed | warmest pair (mutual, mean / max) | coldest pair (mutual, mean / min) |
|---|---|---|---|
| 8 | build 80.3%, hunt 13.1%, hold 5.8%, drift 0.8% | 150 / 200 | -87 / -200 |
| 12 | build 72.5%, hunt 21.8%, hold 5.3%, drift 0.4% | 188 / 200 | -129 / -200 |

| house | budgeted asks / season (target 4-6) | + must-fires | summons | play offers | weeks with a card (target 30-60%) | stories finished (target about 3) | moments | seasons with a pariah (target at most 20%) | NPC showmances | seasons with an NPC removal | seasons with a pile-on (target at most 25%) | story house events / season (autopsy) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 8 | 4.9 (max 9) | 0.2 | 0.6 | 3.4 | 52.1% | 4.6 | 8.6 | 16.8% | 1.03 | 0.0% | 0.2% | 13.3 |
| 12 | 8.9 (max 17) | 0.3 | 0.9 | 5.6 | 53.6% | 8.1 | 12.7 | 45.5% | 1.31 | 0.0% | 1.4% | 22.0 |

### Pacing: decisions and ceremony seconds a week

| policy | size | decisions / week (weeks in the house) | ceremony s / regular week | finale reveal s |
|---|---|---|---|---|
| passive | 8 | 4.1 | 44.9 | 21.8 |
| random | 8 | 14.0 | 44.7 | 21.8 |
| social | 8 | 17.4 | 44.4 | 21.8 |
| reader | 8 | 19.2 | 44.4 | 21.8 |
| schemer | 8 | 19.0 | 44.5 | 21.8 |
| loyalist | 8 | 18.1 | 44.5 | 21.8 |
| floater | 8 | 16.3 | 44.5 | 21.8 |
| beast | 8 | 15.7 | 43.0 | 21.8 |
| novice | 8 | 14.4 | 44.5 | 21.8 |
| exploit | 8 | 36.5 | 44.4 | 21.8 |
| oracle-reader | 8 | 16.7 | 44.9 | 21.8 |
| oracle-skilled | 8 | 9.9 | 44.9 | 21.8 |
| passive | 12 | 3.9 | 50.4 | 24.5 |
| random | 12 | 14.6 | 50.3 | 24.5 |
| social | 12 | 18.8 | 50.0 | 24.5 |
| reader | 12 | 25.7 | 49.9 | 24.5 |
| schemer | 12 | 20.1 | 49.9 | 24.5 |
| loyalist | 12 | 18.8 | 50.1 | 24.5 |
| floater | 12 | 17.1 | 50.0 | 24.5 |
| beast | 12 | 16.9 | 48.9 | 24.5 |
| novice | 12 | 15.0 | 50.1 | 24.5 |
| exploit | 12 | 44.5 | 50.0 | 24.5 |
| oracle-reader | 12 | 22.8 | 50.4 | 24.5 |
| oracle-skilled | 12 | 10.4 | 50.4 | 24.5 |

### Refusals and the walker

| policy | size | own commands / season | walker steps / season | refusals / season | most common refusal |
|---|---|---|---|---|---|
| passive | 8 | 0.0 | 92.0 | 0.00 | - |
| random | 8 | 46.4 | 87.8 | 0.00 | - |
| social | 8 | 64.7 | 88.0 | 0.00 | - |
| reader | 8 | 70.7 | 88.6 | 0.00 | - |
| schemer | 8 | 69.3 | 88.1 | 0.00 | - |
| loyalist | 8 | 60.8 | 87.8 | 0.02 | PromiseFinalTwo: This promise is already active. (9) |
| floater | 8 | 61.8 | 87.7 | 0.00 | - |
| beast | 8 | 69.6 | 89.0 | 0.00 | - |
| novice | 8 | 46.1 | 88.0 | 0.00 | - |
| exploit | 8 | 135.5 | 87.5 | 0.17 | RenameAlliance: The Outsiders has had its new name this week. (37) |
| oracle-reader | 8 | 51.6 | 92.0 | 0.01 | CallTheVote: The You, Casey and Alex Pact settles its call when it meets. (2) |
| oracle-skilled | 8 | 115.7 | 2.3 | 2.29 | AnswerJury: Choose one of the responses offered. (1086) |
| passive | 12 | 0.0 | 151.8 | 0.00 | - |
| random | 12 | 72.2 | 145.0 | 0.00 | - |
| social | 12 | 105.6 | 146.1 | 0.00 | - |
| reader | 12 | 146.1 | 147.3 | 0.00 | - |
| schemer | 12 | 117.2 | 147.2 | 0.00 | - |
| loyalist | 12 | 98.0 | 146.4 | 0.36 | PromiseFinalTwo: This promise is already active. (175) |
| floater | 12 | 96.9 | 145.8 | 0.00 | - |
| beast | 12 | 99.1 | 147.6 | 0.00 | - |
| novice | 12 | 78.0 | 147.1 | 0.00 | - |
| exploit | 12 | 242.3 | 145.6 | 0.57 | RenameAlliance: The Outsiders has had its new name this week. (134) |
| oracle-reader | 12 | 116.6 | 153.1 | 0.04 | CallTheVote: The You, Alex and Casey Pact settles its call when it meets. (3) |
| oracle-skilled | 12 | 194.0 | 3.4 | 3.39 | AnswerJury: Choose one of the responses offered. (1600) |

Seasons with an error (walker refused, invalid state, unfinished): 0 of 11520.

The oracles are the harnesses' own players and keep their own habits: the reader oracle still calls the vote in a
pact of three now and then, and the skilled oracle's first jury answer is one the finale does not offer (the lab's
walker answers with an offered one).

### T0 diagnostics: Q2's Game Sense what-if

Today's formula replayed from the notes differs from the autopsy's score by at most 0. The interval is a bootstrap 95% of the paired difference.

| formula (reader minus random) | size 8 | size 12 |
|---|---|---|
| today (0.3 / 0.4 / 0.3, faces 0-100) | 9.6 [8.3, 10.8] | 8.5 [7.6, 9.2] |
| faces unbounded | 17.4 [14.1, 20.8] meets | 35.8 [31.2, 40.9] meets |
| weights 0.2 / 0.6 / 0.2 | 13.7 [12.1, 15.2] meets | 12.4 [11.4, 13.4] meets |
| strategy notes x1.5 | 8.8 [7.3, 10.1] | 6.5 [5.7, 7.3] |
| weights 0.2 / 0.6 / 0.2, strategy notes x1.5 | 12.4 [10.6, 14.1] meets | 9.5 [8.4, 10.5] |
| weights 0 / 0.7 / 0.3 (no competitions face) | 15.7 [14.3, 17.1] meets | 14.3 [13.3, 15.4] meets |

| formula (oracle-reader minus random) | size 8 | size 12 |
|---|---|---|
| today (0.3 / 0.4 / 0.3, faces 0-100) | 7.6 [6.2, 8.9] | 7.6 [6.7, 8.4] |
| faces unbounded | 10.1 [6.7, 13.4] meets | 24.6 [20.4, 29.1] meets |
| weights 0.2 / 0.6 / 0.2 | 11.2 [9.5, 12.8] meets | 11.2 [10.0, 12.2] meets |
| strategy notes x1.5 | 6.4 [4.9, 7.8] | 6.0 [5.1, 6.8] |
| weights 0.2 / 0.6 / 0.2, strategy notes x1.5 | 9.4 [7.4, 11.1] | 8.8 [7.6, 9.9] |
| weights 0 / 0.7 / 0.3 (no competitions face) | 13.5 [12.0, 14.9] meets | 13.3 [12.1, 14.3] meets |

"meets": a gap of at least 10 with the interval's low end over 5 (the lead's decision 8).

Where today's reader-minus-random gap comes from: the mean points a season, by face and row kind (before each face is held to 0-100).

| face/row kind | reader 8 | random 8 | gap 8 | reader 12 | random 12 | gap 12 |
|---|---|---|---|---|---|---|
| competitions/competition | 112.4 | 101.1 | 11.3 | 148.4 | 128.6 | 19.8 |
| social/alliance | 0.4 | 0.0 | 0.4 | 0.4 | 0.1 | 0.4 |
| social/bond | 0.4 | -0.4 | 0.8 | 1.2 | -0.5 | 1.8 |
| social/grudge | -12.4 | -12.6 | 0.2 | -16.9 | -16.9 | 0.0 |
| social/memory | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| social/standing | -0.8 | -1.0 | 0.2 | -5.1 | -3.3 | -1.8 |
| strategy/ballot | 5.6 | 3.0 | 2.6 | 14.0 | 4.6 | 9.4 |
| strategy/call | 2.9 | 0.1 | 2.8 | 17.2 | 0.1 | 17.1 |
| strategy/opportunity | 19.7 | -8.0 | 27.6 | 32.2 | -13.7 | 45.8 |
| strategy/power | 20.8 | 19.9 | 0.8 | 38.8 | 36.6 | 2.1 |

### T0 diagnostics: Q4's nominations, the weight in its terms

Each term as it enters an NPC Head of Household's weight (lower is put up first): the HoH's view; the strategy
windows' own terms (ally shield, deals, pleas, protection held); the story's own terms (grudges, their word, bonds);
minus the overlapping protection; minus the threat. Player minus the mean other candidate; negative means the term
pushes the player up.

| policy | house | NPC nominations | player put up | mean rank (1 first) of candidates | view | strategy terms | story terms | protection | threat | total |
|---|---|---|---|---|---|---|---|---|---|---|
| passive | 8 | 1000 | 55.0% | 1.4 of 5.4 | -2.6 - 13.5 = -16.1 | -23.3 - 5.1 = -28.4 | -7.2 - 1.9 = -9.1 | -0.0 - -0.0 = 0.0 | -13.1 - -9.5 = -3.6 | -46.1 - 11.1 = -57.2 |
| random | 8 | 1145 | 47.9% | 2.1 of 5.2 | 3.2 - 14.6 = -11.4 | -24.3 - 6.6 = -30.9 | -6.3 - 2.1 = -8.4 | -0.8 - -0.0 = -0.8 | -14.3 - -10.5 = -3.8 | -42.6 - 12.8 = -55.4 |
| social | 8 | 1220 | 47.1% | 2.4 of 5.2 | 14.0 - 14.9 = -0.9 | -22.9 - 6.8 = -29.7 | -5.1 - 2.7 = -7.8 | -0.6 - -0.0 = -0.6 | -15.5 - -11.1 = -4.4 | -30.0 - 13.3 = -43.3 |
| reader | 8 | 1240 | 46.1% | 2.3 of 5.2 | 10.2 - 15.7 = -5.6 | -24.1 - 6.2 = -30.3 | -5.2 - 2.7 = -8.0 | -0.3 - -0.0 = -0.3 | -14.4 - -10.7 = -3.7 | -33.9 - 13.9 = -47.8 |
| schemer | 8 | 1168 | 40.4% | 2.5 of 5.3 | 10.1 - 10.7 = -0.6 | -9.1 - 3.7 = -12.8 | -6.3 - 1.6 = -7.9 | -0.6 - -0.0 = -0.6 | -14.4 - -10.4 = -3.9 | -20.3 - 5.6 = -25.9 |
| loyalist | 8 | 1136 | 44.7% | 2.3 of 5.3 | 14.1 - 14.2 = -0.1 | -20.0 - 6.4 = -26.5 | -3.9 - 2.3 = -6.2 | -0.4 - -0.0 = -0.4 | -15.7 - -11.3 = -4.4 | -26.0 - 11.6 = -37.6 |
| floater | 8 | 1240 | 49.6% | 2.1 of 5.2 | 5.6 - 15.1 = -9.6 | -28.9 - 7.7 = -36.6 | -6.7 - 2.8 = -9.5 | -0.0 - -0.0 = 0.0 | -14.6 - -10.7 = -3.9 | -44.7 - 14.9 = -59.6 |
| beast | 8 | 948 | 54.4% | 2.3 of 5.0 | 15.9 - 17.2 = -1.3 | -37.9 - 8.9 = -46.8 | -3.6 - 2.7 = -6.4 | -0.6 - -0.0 = -0.6 | -20.9 - -10.3 = -10.6 | -47.1 - 18.5 = -65.6 |
| novice | 8 | 1193 | 45.8% | 2.2 of 5.3 | 9.7 - 14.4 = -4.7 | -18.2 - 5.5 = -23.8 | -3.1 - 2.8 = -5.9 | -0.8 - -0.0 = -0.8 | -14.1 - -10.6 = -3.6 | -26.6 - 12.2 = -38.8 |
| exploit | 8 | 1141 | 50.7% | 1.9 of 5.3 | -0.8 - 14.7 = -15.5 | -27.1 - 7.2 = -34.4 | -5.8 - 2.5 = -8.3 | -0.2 - -0.0 = -0.2 | -14.8 - -10.8 = -4.0 | -48.8 - 13.6 = -62.5 |
| oracle-reader | 8 | 1142 | 44.9% | 2.2 of 5.3 | 10.5 - 14.2 = -3.7 | -14.9 - 5.0 = -19.9 | -5.8 - 2.1 = -7.9 | -0.5 - -0.0 = -0.5 | -13.8 - -10.4 = -3.4 | -24.5 - 10.9 = -35.3 |
| oracle-skilled | 8 | 1093 | 47.6% | 2.1 of 5.3 | 5.1 - 13.9 = -8.9 | -16.2 - 5.3 = -21.4 | -3.3 - 1.7 = -5.0 | -3.4 - -0.0 = -3.4 | -13.7 - -10.0 = -3.7 | -31.5 - 10.8 = -42.4 |
| passive | 12 | 1654 | 37.3% | 2.1 of 8.4 | -3.5 - 12.0 = -15.6 | -28.1 - 3.7 = -31.9 | -6.6 - 0.2 = -6.8 | -0.0 - -0.0 = 0.0 | -14.2 - -9.8 = -4.4 | -52.5 - 6.2 = -58.7 |
| random | 12 | 1897 | 36.1% | 2.9 of 8.0 | 0.3 - 13.4 = -13.1 | -34.5 - 4.8 = -39.3 | -4.6 - -0.1 = -4.5 | -1.2 - -0.0 = -1.2 | -15.9 - -10.8 = -5.1 | -56.0 - 7.2 = -63.2 |
| social | 12 | 2027 | 31.4% | 3.2 of 7.9 | 11.4 - 14.0 = -2.6 | -33.0 - 5.0 = -38.0 | -3.0 - 0.4 = -3.4 | -0.9 - -0.0 = -0.9 | -18.2 - -11.7 = -6.5 | -43.7 - 7.7 = -51.4 |
| reader | 12 | 2024 | 33.0% | 3.2 of 7.8 | 8.4 - 14.3 = -5.9 | -23.0 - 3.6 = -26.6 | -2.2 - 0.1 = -2.3 | -0.5 - -0.0 = -0.5 | -16.6 - -11.3 = -5.3 | -33.9 - 6.7 = -40.6 |
| schemer | 12 | 2026 | 29.8% | 3.5 of 7.8 | 9.8 - 12.5 = -2.7 | -23.3 - 4.1 = -27.4 | -2.8 - 0.2 = -3.0 | -0.5 - -0.0 = -0.5 | -17.5 - -11.3 = -6.3 | -34.4 - 5.5 = -39.8 |
| loyalist | 12 | 1895 | 31.0% | 3.3 of 8.0 | 12.1 - 13.4 = -1.4 | -27.8 - 4.4 = -32.2 | -0.9 - 0.0 = -0.9 | -0.5 - -0.0 = -0.5 | -17.3 - -11.4 = -5.9 | -34.5 - 6.4 = -40.9 |
| floater | 12 | 2044 | 35.3% | 2.8 of 7.9 | 3.0 - 14.2 = -11.2 | -36.9 - 5.0 = -41.9 | -6.2 - 0.3 = -6.5 | -0.0 - -0.0 = 0.0 | -16.3 - -10.8 = -5.5 | -56.4 - 8.7 = -65.1 |
| beast | 12 | 1520 | 40.9% | 2.8 of 7.4 | 6.7 - 17.4 = -10.7 | -51.5 - 7.0 = -58.5 | -2.9 - -0.4 = -2.5 | -0.3 - -0.0 = -0.3 | -23.0 - -10.9 = -12.2 | -71.2 - 13.1 = -84.3 |
| novice | 12 | 2119 | 34.6% | 3.2 of 8.0 | 9.7 - 13.5 = -3.7 | -27.8 - 4.3 = -32.1 | -0.9 - 0.4 = -1.3 | -0.6 - -0.0 = -0.6 | -16.2 - -11.2 = -5.1 | -35.8 - 7.0 = -42.8 |
| exploit | 12 | 1817 | 38.6% | 2.7 of 8.1 | -4.1 - 14.2 = -18.3 | -36.4 - 5.1 = -41.5 | -4.0 - 0.1 = -4.1 | -0.2 - -0.0 = -0.2 | -16.7 - -11.0 = -5.6 | -61.4 - 8.3 = -69.7 |
| oracle-reader | 12 | 1955 | 35.4% | 3.1 of 7.9 | 5.3 - 13.6 = -8.2 | -24.1 - 4.0 = -28.1 | -5.2 - -0.3 = -4.9 | -0.5 - -0.0 = -0.5 | -15.5 - -11.2 = -4.3 | -39.9 - 6.1 = -46.0 |
| oracle-skilled | 12 | 1892 | 31.5% | 3.2 of 8.0 | 3.4 - 12.6 = -9.2 | -23.1 - 3.8 = -27.0 | -1.4 - -0.4 = -1.0 | -4.8 - -0.0 = -4.8 | -15.2 - -10.8 = -4.4 | -41.2 - 5.2 = -46.4 |

### T0 diagnostics: Q5's pariah runs and Q1's counter

| house | seasons | with a pariah | mean week | in the house then | holders | target's HoH wins (house's most) | target won a HoH | target won two | causes of the holders' grudges |
|---|---|---|---|---|---|---|---|---|---|
| 8 | 5760 | 16.8% | 3.7 | 5.2 | 3.0 | 1.15 (1.29) | 100.0% | 15.1% | nominated 51.5%, replacement-veto 28.5%, replacement 12.2%, story 2.6%, voted-against 2.5%, promise-broken 2.5%, deal-broken 0.1% |
| 12 | 5760 | 45.5% | 5.9 | 7.1 | 3.0 | 1.31 (1.57) | 100.0% | 28.3% | nominated 53.1%, replacement-veto 25.1%, replacement 11.8%, story 3.5%, voted-against 3.0%, promise-broken 2.6%, deal-broken 1.0% |

Members a counter could reach (said the plan, off the block) at every plan the players answered: 875, of them at
the counters 44; known to have turned 8. Today's constants (base 20, full view 10, Loyal x1.5, cap 30) recomputed
from the records differ from the odds the lab read by at most 0. Their view of the player: p10 -1.7, median 26.9,
p90 97.1; the vote margin: p10 2.1, median 10.8, p90 47.5. Loyal 15, Sneaky 16 of 875.

| base | full view | cap | mean odds (every answer) | mean odds (counters) | reachable at all |
|---|---|---|---|---|---|
| 20 (today) | 10 | 30 | 0.33 | 0.16 | 53.6% |
| 8 | 50 | 12 | 0.10 | 0.07 | 19.8% |
| 20 | 50 | 30 | 0.22 | 0.10 | 39.7% |
| 40 | 50 | 60 | 0.36 | 0.20 | 56.6% |
| 60 | 50 | 90 | 0.45 | 0.26 | 66.9% |
| 20 | 25 | 30 | 0.29 | 0.13 | 48.3% |
| 40 | 25 | 60 | 0.43 | 0.25 | 63.8% |

(The lab writes eleven more candidate rows between these; the counters' mean peaks at 0.26, so none puts it inside
0.35-0.50.)

## What this baseline does not cover

- **Seed counts.** The headline is 480 a cell, not 800; the NPC budgets 100 (300), 50 (900, two players) and 15
  (1800, the reader alone); the projections 400, 200 and 50 (900). The full tier (every size and the All-Stars) has
  not run.
- **Walk-ins** stay off (decision 4): the NPC world here is the 1 Hz conversation world and D2's beats.
- **Human performance and time (B8).** The performance distributions and the 30 seconds a decision are
  assumptions.
- **Policy strength.** Each policy is one plausible script of its style; the oracles are not strong bounds.
- **Tuning (T1+).** Nothing here is tuned but D3's counter; the bands are the owner's.

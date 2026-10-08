# Balance baseline (first, partial)

**Status: a first, partial baseline (BALANCE plan B2-B4's headline run), not B6's.** It is 100 seasons per
policy and size, not the plan's 800 (see *Runtime*), the NPC world is not driven in any of these seasons
(B5 is out of scope), and the per-policy performance distributions are assumptions until human data exists
(B8). Every number here is reproducible from the commands below; treat differences under the Wilson
intervals as noise.

Measured on 2026-10-08 at `a81ab572` (branch `claude/balance-lab`, on `claude/lead-integration` at
`ca7f4da6`): schema 28, competition rules 4, story rules 9, the economy, agency, the finale, the commitment
rules, D4's leaks and the unified commitment and hearing version 1 - exactly what the director starts,
because every lab season is built by `SeasonBuilder.Create` and `ShippedRules.ApplyFresh` (B0).

## How it was measured

- **Seasons.** Regular roster, 8 and 12 houseguests. Each step the policy proposes a command (up to three
  times, told each refusal); otherwise the engine tests' walker takes the phase's own step. Every competition's
  performance comes from the performance model. The state is validated at every phase change and at the end.
  0 of 2,400 headline seasons and 0 of 600 performance seasons had an error (walker refused, invalid state or
  unfinished).
- **Policies** (`BalanceLabPolicies.cs`) see only a `PlayerView` (`BalanceLabView.cs`): the screens' readers
  (KnownOdds, the vote read, AllianceRead, KnownBallots, the player's own view, HouseguestNotes, WaitingOnYou,
  CampaignBrief, the story's shown odds, the player's commitments and breach warnings) and the controls they
  offer. Ten are knowledge-gated: passive, random, social, reader, schemer, loyalist, floater, competition
  beast, novice, exploit hunter. Two are labelled **oracles** that read the season itself: GameSense's
  reader (`oracle-reader`) and the story sweep's skilled player (`oracle-skilled`).
- **Performance** (`BalanceLabPerformance.cs`): a normal draw cut to [0, 1] about each policy's mean - beast
  .8 (spread .12), novice .35 (.18), everyone else .5 (.15) - keyed by a hash of the season's seed, week and
  phase, never the season's generator, so every policy meets the same luck. The performance tier holds it
  fixed at 0, .25, .5, .75 and 1.
- **Seeds** are a hash of the house and the index, never of the policy, so every policy plays the same 100
  seasons at each size and McNemar's test pairs them.
- **Metrics** come from `SeasonAutopsy` (B1); decisions a week and ceremony seconds a week from the lab
  (`CeremonyPacing`, suspenseful pace, unskipped).

Reproduce (Unity-free, from the repository root):

```
dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabTests"                                  # smoke tier
BALANCE_SEEDS=100 dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabReports.HeadlineReport"
BALANCE_PERFORMANCE_SEEDS=60 dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabReports.PerformanceReport"
```

Rows land as JSONL, and these tables as Markdown, in `Tools/SimulationTests/bin/Debug/net10.0/balance/`.

## Runtime

| tier | seasons | wall time | note |
|---|---|---|---|
| smoke (not Explicit, in the subset) | 36 (12 policies x 6, 8, 12) | about 13 s on 6 threads | |
| headline | 2,400 (12 policies x 8, 12 x 100) | 46.9 min on 6 threads | shared the machine with two Unity batch runs and other lanes' test hosts |
| performance (B4) | 600 (passive x 5 levels x 8, 12 x 60) | 3.3 min | |
| full (overnight) | not run | | |

The smoke tier spends about 2 s of CPU a season averaged over 6, 8 and 12 (12 is the costliest). By that
estimate the plan's 800 seeds a cell would take about two hours at the headline's grid on an idle six-thread
machine; a first attempt at 300 seeds was stopped when contention put it at three hours or more, and this run
uses 100.

## Findings F1-F7

**F1. Strategy barely moves win rate - refuted as stated; how the player plays matters less than whether they
play, and competitions decide most.** At 8 (base rate 12.5%) the passive player wins 6% [2.8, 12.5], the
social and floater players 19% [12.5, 27.8] (McNemar against passive p = 0.016 each), the loyalist 17%
(p = 0.019), the schemer 14%, the reader 13%, random 7%, the novice 4% and the exploit hunter 2%. At 12 (base
8.3%) every engaged policy beats passive's 5%: random 20%, social 23%, schemer 25%, reader 26%, floater 29%,
loyalist 32% (all p <= 0.005 against passive). Between engaged social styles the differences are inside the
noise at 100 seeds: reader against random p = 0.21 at 8 and 0.40 at 12, reader against social p = 0.33 and
0.75, social against random p = 0.017 at 8 but 0.73 at 12. The competition beast wins 81% at 8 and 58% at 12
(see F3). The Game Sense gap, gated reader minus random, is 7.6 at 8 and 9.5 at 12 against the target of 20.

**F2. The skilled policies are oracles - confirmed that they read hidden state, refuted that they bound
skill.** The gated reader, which never sees a hidden view, wins as often as the oracle reader at 8 (13% against
15%, McNemar p = 0.84) and more often at 12 (26% against 12%, p = 0.026); the oracle skilled player wins 12% at
both sizes. The oracles are weak players rather than upper bounds: the oracle reader spends about half its seats
(11.9 of 22.2 at 8), the skilled player about a third. What hidden reading is worth is not visible in these
numbers; a stronger oracle would be needed to measure it.

**F3. Competitions run on the performance input - confirmed, and preparation matters more.** In real seasons a
passive player wins 22-25% of the weekly competitions it plays at no performance, 44-50% at half marks (the
accessible alternative's value), 64-67% at .75 and 78% at full marks, against a base rate of one in the field
(about 19% at 8, 16% at 12). Half marks alone wins about half of all weekly competitions. The competition beast
(performance about .8, and it studies the house in free time until its preparation is 5) wins 97.5% and 99.1% of
the weekly competitions it plays and 81% and 58% of seasons; a passive player at full marks wins 21.7% and 16.7%
of seasons. Preparation is permanent and caps at +5 competition points (`WebStudyHouse.ApplyBonus`, set only by
`StudyHouse`), more than full marks are worth (+3): with a performance about .8, five free-time seats bought
near-certain competitions for the rest of the season. The preparation's own share is not isolated here (a
studying player at half marks would separate it), but full marks alone reach only 78%. The NPCs' own wins track their statistic sums only weakly (Spearman 0.165 at 8, 0.170 at
12); the strongest on paper wins 45% of weekly competitions; the top NPC takes 36% and 28% of NPC wins.

**F4. The newcomer has built-in penalties - confirmed for the passive newcomer, neutralised by play.** The
passive player is nominated 0.66 times a week in the house against the houseguests' 0.51 at 8 (ratio 1.28) and
0.65 against 0.42 at 12 (1.56), and is on the block in week one 41% and 55% of the time (against 29% and 18% for
a uniform draw over the non-HoH house). Engaged policies bring the ratio to 0.73-1.0; the novice sits at 1.09
and 0.98, the exploit hunter at 1.07 and 1.24.

**F5. Sweeps never run the NPC world - confirmed, and true of this lab too.** Every lab season has 0 NPC ticks
(the smoke tier asserts it). No number here includes NPC-to-NPC conversation or the D2 beats; B5 adds the driver.

**F6. Harness rules drift from what ships - resolved for the lab.** Every lab season starts through
`ShippedRules.ApplyFresh`, the method the director now calls (B0), pinned field by field and by the whole
season's JSON against the director's inline lines at `ca7f4da6`; a source scan fails if the director sets a
rule itself. The older harnesses (GameSense's default arm, the story sweeps) still build their own seasons.

**F7. E4 may not be observable - depends on size; at 8 a loss is likely in the first session.** The novice
(the first-timer model) is evicted in week one 33% [24.6, 42.7] of the time at 8 and 2% [0.6, 7.0] at 12; it is
out by week two 50% and 17%, by week three 60% and 28%. For three testers, at least one natural loss by week one
/ two / three is 70% / 88% / 94% at 8 and 6% / 43% / 63% at 12 (independence assumed). The plan's rough 35% guess
is low for an 8-house and high for week one of a 12-house; a 90% chance needs between two and three weeks at 8
and more than three at 12 (B7 to refine).

## Other findings

- **Buying time does not pay.** The exploit hunter buys 20 and 30 conversations a season, paying 161 and 236
  goodwill, spends every seat and every free action it can find (60 and 120 a season), and has the worst win
  rates of the engaged policies (2% and 10%; against random p = 0.18 and 0.052). No repeated free action showed
  up as an exploit: its refusals stay under 0.3 a season, almost all a pact's weekly rename.
- **The house deals among itself far more than with the player.** NPC-only deals: about 15 a season at 8 and
  55 at 12 (about a quarter kept, under 1 broken), against 0.4-12 player deals. NPC-only pacts: 1.0-1.4 at 8,
  2.4-2.9 at 12. The player's deal ceiling binds only at 12 (0.45-3.4 weeks a season, by policy).
- **Pairs saturate.** The season's warmest pair ends at a mean mutual 153 and 191 (max 200, both views at
  +100); the coldest at -86 and -130 (min -200).
- **NPC Heads of Household** nominate their top threat 57% and 46% of the time; the evictee averages third or
  fourth in the HoH's threat ranking and is in a pact 17% and 28% of the time.
- **Jury.** Mean margin 2.3 and 3.5 votes; among jurors whose evictor reached the final two, 56% and 53% vote
  against that evictor.
- **Story.** 12 and 19.5 beats asked a season, a card in 3.5 and 5.5 weeks, no NPC removal at all in 2,400
  seasons, about one showmance a season, and at least one pariah week in 4.9% and 16.3% of seasons (0.05 and
  0.19 such weeks a season).
- **Pacing.** Decisions a week in the house: 3.7 for the passive player, 14-25 for engaged policies, 38-47 for
  the exploit hunter. Ceremonies: about 44 s a regular week at 8 and 50 s at 12, the finale's jury reveal 22 s
  and 25 s.

## The headline tables

All 2,400 seasons: 100 per policy and size, the same 100 seasons for every policy.

### Win rate by policy and size

Base rate is 1/size. McNemar pairs each policy with the passive player on the same seasons (b: only this policy won, c: only passive won).

| policy | size | n | wins | win rate | Wilson 95% | final two | mean placement | vs passive b/c | p |
|---|---|---|---|---|---|---|---|---|---|
| passive | 8 | 100 | 6 | 6.0% | [2.8%, 12.5%] | 11.0% | 6.18 | - | - |
| random | 8 | 100 | 7 | 7.0% | [3.4%, 13.7%] | 40.0% | 4.11 | 7/6 | 1.000 |
| social | 8 | 100 | 19 | 19.0% | [12.5%, 27.8%] | 42.0% | 3.53 | 19/6 | 0.016 |
| reader | 8 | 100 | 13 | 13.0% | [7.8%, 21.0%] | 38.0% | 4.03 | 13/6 | 0.167 |
| schemer | 8 | 100 | 14 | 14.0% | [8.5%, 22.1%] | 38.0% | 3.88 | 12/4 | 0.077 |
| loyalist | 8 | 100 | 17 | 17.0% | [10.9%, 25.5%] | 33.0% | 4.69 | 15/4 | 0.019 |
| floater | 8 | 100 | 19 | 19.0% | [12.5%, 27.8%] | 36.0% | 3.62 | 19/6 | 0.016 |
| beast | 8 | 100 | 81 | 81.0% | [72.2%, 87.5%] | 95.0% | 1.45 | 77/2 | 0.000 |
| novice | 8 | 100 | 4 | 4.0% | [1.6%, 9.8%] | 16.0% | 5.61 | 4/6 | 0.754 |
| exploit | 8 | 100 | 2 | 2.0% | [0.6%, 7.0%] | 30.0% | 4.86 | 2/6 | 0.289 |
| oracle-reader | 8 | 100 | 15 | 15.0% | [9.3%, 23.3%] | 20.0% | 5.09 | 15/6 | 0.078 |
| oracle-skilled | 8 | 100 | 12 | 12.0% | [7.0%, 19.8%] | 24.0% | 5.58 | 10/4 | 0.180 |
| passive | 12 | 100 | 5 | 5.0% | [2.2%, 11.2%] | 8.0% | 9.66 | - | - |
| random | 12 | 100 | 20 | 20.0% | [13.3%, 28.9%] | 27.0% | 6.62 | 19/4 | 0.003 |
| social | 12 | 100 | 23 | 23.0% | [15.8%, 32.2%] | 28.0% | 6.38 | 22/4 | 0.001 |
| reader | 12 | 100 | 26 | 26.0% | [18.4%, 35.4%] | 34.0% | 5.42 | 25/4 | 0.000 |
| schemer | 12 | 100 | 25 | 25.0% | [17.5%, 34.3%] | 35.0% | 5.42 | 24/4 | 0.000 |
| loyalist | 12 | 100 | 32 | 32.0% | [23.7%, 41.7%] | 35.0% | 5.94 | 31/4 | 0.000 |
| floater | 12 | 100 | 29 | 29.0% | [21.0%, 38.5%] | 31.0% | 5.41 | 28/4 | 0.000 |
| beast | 12 | 100 | 58 | 58.0% | [48.2%, 67.2%] | 63.0% | 4.11 | 54/1 | 0.000 |
| novice | 12 | 100 | 20 | 20.0% | [13.3%, 28.9%] | 21.0% | 6.94 | 20/5 | 0.005 |
| exploit | 12 | 100 | 10 | 10.0% | [5.5%, 17.4%] | 15.0% | 7.65 | 10/5 | 0.302 |
| oracle-reader | 12 | 100 | 12 | 12.0% | [7.0%, 19.8%] | 14.0% | 8.09 | 11/4 | 0.118 |
| oracle-skilled | 12 | 100 | 12 | 12.0% | [7.0%, 19.8%] | 15.0% | 8.51 | 10/3 | 0.092 |

Further pairs (McNemar on the same seasons, b/c, p): gated reader against oracle reader 12/14, 0.84 at 8 and
24/10, 0.026 at 12; social against random 17/5, 0.017 and 18/15, 0.73; reader against random 11/5, 0.21 and
21/15, 0.40; loyalist against random 15/5, 0.041 and 26/14, 0.082; reader against social 10/16, 0.33 and 21/18,
0.75; novice against passive 4/6, 0.75 and 20/5, 0.005; exploit against random 2/7, 0.18 and 6/16, 0.052.

### Survival by week: the player still in the house after week k

| policy (8) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 |
|---|---|---|---|---|---|---|
| passive | 59.0% | 36.0% | 29.0% | 23.0% | 18.0% | 11.0% |
| random | 85.0% | 72.0% | 67.0% | 61.0% | 57.0% | 40.0% |
| social | 92.0% | 81.0% | 75.0% | 70.0% | 68.0% | 42.0% |
| reader | 85.0% | 73.0% | 66.0% | 63.0% | 59.0% | 38.0% |
| schemer | 91.0% | 83.0% | 68.0% | 63.0% | 55.0% | 38.0% |
| loyalist | 76.0% | 61.0% | 51.0% | 48.0% | 45.0% | 33.0% |
| floater | 94.0% | 81.0% | 74.0% | 72.0% | 62.0% | 36.0% |
| beast | 99.0% | 95.0% | 95.0% | 95.0% | 95.0% | 95.0% |
| novice | 67.0% | 50.0% | 40.0% | 33.0% | 29.0% | 16.0% |
| exploit | 72.0% | 62.0% | 51.0% | 50.0% | 47.0% | 30.0% |
| oracle-reader | 72.0% | 61.0% | 48.0% | 41.0% | 34.0% | 20.0% |
| oracle-skilled | 62.0% | 47.0% | 35.0% | 31.0% | 31.0% | 24.0% |

| policy (12) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 | wk 7 | wk 8 | wk 9 | wk 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| passive | 68.0% | 43.0% | 35.0% | 22.0% | 17.0% | 10.0% | 9.0% | 9.0% | 8.0% | 8.0% |
| random | 94.0% | 77.0% | 67.0% | 54.0% | 47.0% | 43.0% | 38.0% | 36.0% | 35.0% | 27.0% |
| social | 94.0% | 85.0% | 74.0% | 64.0% | 54.0% | 42.0% | 34.0% | 32.0% | 32.0% | 28.0% |
| reader | 100.0% | 93.0% | 88.0% | 74.0% | 67.0% | 53.0% | 42.0% | 41.0% | 40.0% | 34.0% |
| schemer | 97.0% | 92.0% | 84.0% | 72.0% | 66.0% | 52.0% | 47.0% | 45.0% | 43.0% | 35.0% |
| loyalist | 95.0% | 87.0% | 77.0% | 65.0% | 52.0% | 49.0% | 40.0% | 38.0% | 36.0% | 35.0% |
| floater | 100.0% | 93.0% | 90.0% | 81.0% | 65.0% | 49.0% | 41.0% | 40.0% | 40.0% | 31.0% |
| beast | 100.0% | 85.0% | 85.0% | 73.0% | 73.0% | 63.0% | 63.0% | 63.0% | 63.0% | 63.0% |
| novice | 98.0% | 83.0% | 72.0% | 55.0% | 44.0% | 32.0% | 30.0% | 27.0% | 24.0% | 21.0% |
| exploit | 88.0% | 81.0% | 66.0% | 49.0% | 42.0% | 27.0% | 20.0% | 19.0% | 18.0% | 15.0% |
| oracle-reader | 91.0% | 70.0% | 55.0% | 41.0% | 29.0% | 25.0% | 20.0% | 19.0% | 15.0% | 14.0% |
| oracle-skilled | 73.0% | 53.0% | 47.0% | 37.0% | 29.0% | 24.0% | 21.0% | 19.0% | 19.0% | 15.0% |

### Early risk (F7): nominated and evicted in week one, out by week k, and the chance at least one of three testers is out

| policy | size | nominated wk 1 | evicted wk 1 [Wilson] | out by wk 2 | out by wk 3 | P(≥1 of 3 out) wk 1 / 2 / 3 |
|---|---|---|---|---|---|---|
| passive | 8 | 41.0% | 41.0% [31.9%, 50.8%] | 64.0% | 71.0% | 79.5% / 95.3% / 97.6% |
| random | 8 | 29.0% | 15.0% [9.3%, 23.3%] | 28.0% | 33.0% | 38.6% / 62.7% / 69.9% |
| social | 8 | 30.0% | 8.0% [4.1%, 15.0%] | 19.0% | 25.0% | 22.1% / 46.9% / 57.8% |
| reader | 8 | 38.0% | 15.0% [9.3%, 23.3%] | 27.0% | 34.0% | 38.6% / 61.1% / 71.3% |
| schemer | 8 | 49.0% | 9.0% [4.8%, 16.2%] | 17.0% | 32.0% | 24.6% / 42.8% / 68.6% |
| loyalist | 8 | 32.0% | 24.0% [16.7%, 33.2%] | 39.0% | 49.0% | 56.1% / 77.3% / 86.7% |
| floater | 8 | 36.0% | 6.0% [2.8%, 12.5%] | 19.0% | 26.0% | 16.9% / 46.9% / 59.5% |
| beast | 8 | 4.0% | 1.0% [0.2%, 5.4%] | 5.0% | 5.0% | 3.0% / 14.3% / 14.3% |
| novice | 8 | 39.0% | 33.0% [24.6%, 42.7%] | 50.0% | 60.0% | 69.9% / 87.5% / 93.6% |
| exploit | 8 | 37.0% | 28.0% [20.1%, 37.5%] | 38.0% | 49.0% | 62.7% / 76.2% / 86.7% |
| oracle-reader | 8 | 43.0% | 28.0% [20.1%, 37.5%] | 39.0% | 52.0% | 62.7% / 77.3% / 88.9% |
| oracle-skilled | 8 | 38.0% | 38.0% [29.1%, 47.8%] | 53.0% | 65.0% | 76.2% / 89.6% / 95.7% |
| passive | 12 | 55.0% | 32.0% [23.7%, 41.7%] | 57.0% | 65.0% | 68.6% / 92.0% / 95.7% |
| random | 12 | 39.0% | 6.0% [2.8%, 12.5%] | 23.0% | 33.0% | 16.9% / 54.3% / 69.9% |
| social | 12 | 30.0% | 6.0% [2.8%, 12.5%] | 15.0% | 26.0% | 16.9% / 38.6% / 59.5% |
| reader | 12 | 21.0% | 0.0% [0.0%, 3.7%] | 7.0% | 12.0% | 0.0% / 19.6% / 31.9% |
| schemer | 12 | 37.0% | 3.0% [1.0%, 8.5%] | 8.0% | 16.0% | 8.7% / 22.1% / 40.7% |
| loyalist | 12 | 33.0% | 5.0% [2.2%, 11.2%] | 13.0% | 23.0% | 14.3% / 34.1% / 54.3% |
| floater | 12 | 25.0% | 0.0% [0.0%, 3.7%] | 7.0% | 10.0% | 0.0% / 19.6% / 27.1% |
| beast | 12 | 1.0% | 0.0% [0.0%, 3.7%] | 15.0% | 15.0% | 0.0% / 38.6% / 38.6% |
| novice | 12 | 42.0% | 2.0% [0.6%, 7.0%] | 17.0% | 28.0% | 5.9% / 42.8% / 62.7% |
| exploit | 12 | 43.0% | 12.0% [7.0%, 19.8%] | 19.0% | 34.0% | 31.9% / 46.9% / 71.3% |
| oracle-reader | 12 | 37.0% | 9.0% [4.8%, 16.2%] | 30.0% | 45.0% | 24.6% / 65.7% / 83.4% |
| oracle-skilled | 12 | 53.0% | 27.0% [19.3%, 36.4%] | 47.0% | 53.0% | 61.1% / 85.1% / 89.6% |

### Nominations per week in the house (F4): the player against the houseguests of the same seasons

| policy | size | player | houseguests | ratio |
|---|---|---|---|---|
| passive | 8 | 0.659 | 0.514 | 1.28 |
| random | 8 | 0.450 | 0.530 | 0.85 |
| social | 8 | 0.379 | 0.522 | 0.73 |
| reader | 8 | 0.477 | 0.516 | 0.92 |
| schemer | 8 | 0.428 | 0.517 | 0.83 |
| loyalist | 8 | 0.502 | 0.514 | 0.98 |
| floater | 8 | 0.440 | 0.515 | 0.85 |
| beast | 8 | 0.203 | 0.483 | 0.42 |
| novice | 8 | 0.566 | 0.519 | 1.09 |
| exploit | 8 | 0.549 | 0.513 | 1.07 |
| oracle-reader | 8 | 0.565 | 0.530 | 1.07 |
| oracle-skilled | 8 | 0.575 | 0.522 | 1.10 |
| passive | 12 | 0.653 | 0.420 | 1.56 |
| random | 12 | 0.472 | 0.427 | 1.11 |
| social | 12 | 0.428 | 0.423 | 1.01 |
| reader | 12 | 0.369 | 0.436 | 0.85 |
| schemer | 12 | 0.368 | 0.431 | 0.85 |
| loyalist | 12 | 0.421 | 0.421 | 1.00 |
| floater | 12 | 0.405 | 0.420 | 0.96 |
| beast | 12 | 0.260 | 0.388 | 0.67 |
| novice | 12 | 0.432 | 0.441 | 0.98 |
| exploit | 12 | 0.520 | 0.420 | 1.24 |
| oracle-reader | 12 | 0.470 | 0.431 | 1.09 |
| oracle-skilled | 12 | 0.583 | 0.424 | 1.37 |

### The player's competitions by policy (weekly HoH and veto they played)

| policy | size | played / season | won | win rate [Wilson] | mean performance | HoH wins / season | veto wins / season |
|---|---|---|---|---|---|---|---|
| passive | 8 | 3.93 | 195 | 49.6% [44.7%, 54.5%] | 0.51 | 0.93 | 1.13 |
| random | 8 | 6.18 | 323 | 52.3% [48.3%, 56.2%] | 0.51 | 1.80 | 1.77 |
| social | 8 | 6.76 | 345 | 51.0% [47.3%, 54.8%] | 0.51 | 1.93 | 1.88 |
| reader | 8 | 6.28 | 324 | 51.6% [47.7%, 55.5%] | 0.51 | 1.80 | 1.75 |
| schemer | 8 | 6.65 | 346 | 52.0% [48.2%, 55.8%] | 0.51 | 1.90 | 1.88 |
| loyalist | 8 | 5.32 | 257 | 48.3% [44.1%, 52.6%] | 0.51 | 1.52 | 1.37 |
| floater | 8 | 6.85 | 357 | 52.1% [48.4%, 55.8%] | 0.50 | 1.93 | 1.97 |
| beast | 8 | 7.47 | 728 | 97.5% [96.1%, 98.4%] | 0.80 | 3.80 | 4.43 |
| novice | 8 | 4.70 | 204 | 43.4% [39.0%, 47.9%] | 0.36 | 1.07 | 1.09 |
| exploit | 8 | 5.45 | 266 | 48.8% [44.6%, 53.0%] | 0.51 | 1.46 | 1.49 |
| oracle-reader | 8 | 5.15 | 262 | 50.9% [46.6%, 55.2%] | 0.52 | 1.30 | 1.51 |
| oracle-skilled | 8 | 4.39 | 215 | 49.0% [44.3%, 53.6%] | 0.51 | 1.30 | 1.09 |
| passive | 12 | 4.67 | 240 | 51.4% [46.9%, 55.9%] | 0.51 | 1.06 | 1.42 |
| random | 12 | 8.27 | 431 | 52.1% [48.7%, 55.5%] | 0.51 | 2.19 | 2.39 |
| social | 12 | 8.37 | 440 | 52.6% [49.2%, 55.9%] | 0.50 | 2.36 | 2.32 |
| reader | 12 | 9.66 | 522 | 54.0% [50.9%, 57.2%] | 0.50 | 2.68 | 2.85 |
| schemer | 12 | 9.51 | 527 | 55.4% [52.2%, 58.5%] | 0.51 | 2.68 | 2.92 |
| loyalist | 12 | 8.89 | 487 | 54.8% [51.5%, 58.0%] | 0.51 | 2.52 | 2.70 |
| floater | 12 | 9.71 | 518 | 53.3% [50.2%, 56.5%] | 0.51 | 2.62 | 2.87 |
| beast | 12 | 9.90 | 981 | 99.1% [98.3%, 99.5%] | 0.80 | 4.44 | 6.00 |
| novice | 12 | 7.95 | 339 | 42.6% [39.2%, 46.1%] | 0.36 | 1.72 | 1.88 |
| exploit | 12 | 7.18 | 371 | 51.7% [48.0%, 55.3%] | 0.51 | 1.81 | 2.05 |
| oracle-reader | 12 | 6.64 | 336 | 50.6% [46.8%, 54.4%] | 0.51 | 1.66 | 1.84 |
| oracle-skilled | 12 | 5.93 | 306 | 51.6% [47.6%, 55.6%] | 0.52 | 1.56 | 1.65 |

### Competition by fixed performance (B4: the passive player, 60 seasons a level and size)

| size | performance | played | won | win rate [Wilson] | field 3-5 | field 6-8 | field 9-11 | season win rate [Wilson] |
|---|---|---|---|---|---|---|---|---|
| 8 | 0.00 | 153 | 34 | 22.2% [16.4%, 29.4%] | 28.6% (14) | 21.6% (139) | - | 0.0% [0.0%, 6.0%] |
| 8 | 0.25 | 187 | 64 | 34.2% [27.8%, 41.3%] | 48.4% (31) | 31.4% (156) | - | 1.7% [0.3%, 8.9%] |
| 8 | 0.50 | 207 | 92 | 44.4% [37.8%, 51.3%] | 50.0% (42) | 43.0% (165) | - | 1.7% [0.3%, 8.9%] |
| 8 | 0.75 | 286 | 182 | 63.6% [57.9%, 69.0%] | 74.2% (89) | 58.9% (197) | - | 10.0% [4.7%, 20.1%] |
| 8 | 1.00 | 367 | 288 | 78.5% [74.0%, 82.4%] | 81.5% (146) | 76.5% (221) | - | 21.7% [13.1%, 33.6%] |
| 12 | 0.00 | 276 | 69 | 25.0% [20.3%, 30.4%] | 26.7% (15) | 35.6% (146) | 12.7% (55) | 1.7% [0.3%, 8.9%] |
| 12 | 0.25 | 229 | 88 | 38.4% [32.4%, 44.9%] | 75.0% (4) | 49.6% (125) | 22.5% (40) | 1.7% [0.3%, 8.9%] |
| 12 | 0.50 | 256 | 129 | 50.4% [44.3%, 56.5%] | 55.6% (9) | 56.3% (151) | 52.8% (36) | 3.3% [0.9%, 11.4%] |
| 12 | 0.75 | 304 | 205 | 67.4% [62.0%, 72.5%] | 84.0% (25) | 66.5% (188) | 87.1% (31) | 5.0% [1.7%, 13.7%] |
| 12 | 1.00 | 389 | 303 | 77.9% [73.5%, 81.7%] | 89.7% (58) | 73.9% (238) | 87.9% (33) | 16.7% [9.3%, 28.0%] |

The field buckets count the competitions the player played at that field size (in brackets).

### Competition concentration (all policies' seasons; NPC rows from the passive player's)

| size | Spearman NPC wins vs stat sum | top NPC's share of NPC wins | weekly comps won by the strongest on paper | mean winner rank on paper | field size |
|---|---|---|---|---|---|
| 8 | 0.165 | 36.4% | 44.9% | 2.16 | 5.3 |
| 12 | 0.170 | 27.5% | 45.4% | 2.17 | 6.4 |

### Pacts and deals per season

Player deals count every deal the player is party to, the house's offers to them included, in any status.

| policy | size | player pacts | NPC-only pacts | player deals (kept/broken) | NPC-only deals (kept/broken) | player promises (kept/broken) | oaths | weeks at the deal ceiling |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 0.00 | 1.24 | 0.38 (0.00/0.00) | 14.46 (3.95/0.20) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| random | 8 | 0.07 | 1.19 | 3.08 (0.29/0.41) | 15.38 (4.31/0.22) | 0.97 (0.38/0.39) | 0.02 | 0.00 |
| social | 8 | 0.38 | 1.16 | 4.21 (0.57/1.09) | 15.03 (4.05/0.22) | 3.39 (1.17/1.10) | 0.20 | 0.00 |
| reader | 8 | 0.23 | 1.12 | 5.42 (2.84/1.24) | 14.40 (4.03/0.13) | 0.44 (0.01/0.05) | 0.00 | 0.00 |
| schemer | 8 | 0.53 | 0.97 | 7.13 (2.79/2.17) | 11.08 (3.05/0.20) | 2.33 (0.97/0.78) | 0.00 | 0.00 |
| loyalist | 8 | 0.92 | 1.24 | 4.37 (0.81/0.07) | 14.28 (4.16/0.17) | 1.59 (0.20/0.01) | 0.83 | 0.00 |
| floater | 8 | 0.00 | 1.13 | 2.53 (0.00/0.00) | 16.14 (4.15/0.27) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| beast | 8 | 0.24 | 1.14 | 5.21 (0.00/0.12) | 18.31 (3.81/0.32) | 0.98 (0.01/0.03) | 0.00 | 0.00 |
| novice | 8 | 0.31 | 1.17 | 2.28 (0.58/0.15) | 14.72 (4.07/0.33) | 1.60 (0.66/0.45) | 0.07 | 0.00 |
| exploit | 8 | 0.14 | 1.18 | 1.12 (0.19/0.30) | 15.38 (4.48/0.21) | 2.36 (1.08/1.01) | 0.03 | 0.00 |
| oracle-reader | 8 | 0.47 | 1.22 | 4.64 (1.65/0.59) | 13.83 (4.07/0.20) | 0.32 (0.01/0.01) | 0.00 | 0.00 |
| oracle-skilled | 8 | 0.21 | 1.36 | 1.34 (0.26/0.33) | 15.75 (4.34/0.26) | 1.07 (0.03/0.15) | 0.01 | 0.00 |
| passive | 12 | 0.00 | 2.87 | 0.63 (0.00/0.00) | 56.04 (12.62/0.77) | 0.00 (0.00/0.00) | 0.00 | 0.45 |
| random | 12 | 0.10 | 2.69 | 4.05 (0.57/0.33) | 55.27 (12.49/0.69) | 1.02 (0.35/0.29) | 0.02 | 1.83 |
| social | 12 | 0.68 | 2.70 | 5.69 (0.99/0.70) | 54.20 (12.24/0.81) | 4.70 (1.42/1.22) | 0.40 | 1.84 |
| reader | 12 | 1.13 | 2.43 | 8.73 (3.96/2.00) | 49.25 (10.63/0.77) | 1.61 (0.03/0.05) | 0.00 | 2.14 |
| schemer | 12 | 1.13 | 2.49 | 12.23 (5.28/2.44) | 46.71 (9.81/0.56) | 4.80 (1.38/1.08) | 0.00 | 2.46 |
| loyalist | 12 | 1.25 | 2.76 | 4.96 (1.23/0.14) | 54.04 (11.48/0.79) | 3.40 (0.29/0.03) | 0.86 | 1.91 |
| floater | 12 | 0.00 | 2.59 | 3.32 (0.00/0.00) | 57.63 (12.30/0.87) | 0.00 (0.00/0.00) | 0.00 | 2.07 |
| beast | 12 | 0.28 | 2.60 | 7.85 (0.07/0.29) | 56.85 (10.76/0.72) | 1.38 (0.00/0.05) | 0.00 | 3.39 |
| novice | 12 | 0.54 | 2.53 | 4.96 (1.24/0.37) | 54.00 (12.06/0.83) | 3.25 (0.99/0.65) | 0.43 | 1.40 |
| exploit | 12 | 0.17 | 2.76 | 1.39 (0.32/0.29) | 57.21 (12.16/0.70) | 2.36 (0.78/0.92) | 0.05 | 1.12 |
| oracle-reader | 12 | 0.61 | 2.70 | 6.51 (2.11/0.72) | 54.01 (12.35/0.67) | 0.74 (0.00/0.00) | 0.00 | 1.14 |
| oracle-skilled | 12 | 0.44 | 2.74 | 2.64 (0.57/0.59) | 54.51 (12.68/0.62) | 1.52 (0.02/0.09) | 0.03 | 0.97 |

### Economy per season: window seats, bought time, Have-Nots, free actions

| policy | size | seats offered | spent | wasted | purchases | goodwill paid | Have-Not weeks | free actions |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 16.8 | 0.0 | 16.8 | 0.00 | 0.0 | 0.10 | 0.0 |
| random | 8 | 26.8 | 16.2 | 10.6 | 0.00 | 0.0 | 0.16 | 13.4 |
| social | 8 | 28.9 | 28.9 | 0.0 | 0.00 | 0.0 | 0.22 | 15.3 |
| reader | 8 | 27.5 | 14.5 | 13.0 | 0.00 | 0.0 | 0.25 | 30.0 |
| schemer | 8 | 30.5 | 30.5 | 0.0 | 3.69 | 29.5 | 0.14 | 17.2 |
| loyalist | 8 | 23.2 | 23.2 | 0.0 | 0.00 | 0.0 | 0.16 | 14.5 |
| floater | 8 | 29.0 | 29.0 | 0.0 | 0.00 | 0.0 | 0.22 | 13.2 |
| beast | 8 | 34.0 | 34.0 | 0.0 | 0.00 | 0.0 | 0.00 | 7.0 |
| novice | 8 | 19.7 | 10.4 | 9.3 | 0.00 | 0.0 | 0.17 | 11.6 |
| exploit | 8 | 38.7 | 38.7 | 0.0 | 20.10 | 160.8 | 0.14 | 60.5 |
| oracle-reader | 8 | 22.2 | 11.9 | 10.3 | 0.00 | 0.0 | 0.15 | 19.7 |
| oracle-skilled | 8 | 19.3 | 6.0 | 13.3 | 0.00 | 0.0 | 0.11 | 0.8 |
| passive | 12 | 22.5 | 0.0 | 22.5 | 0.00 | 0.0 | 0.03 | 0.0 |
| random | 12 | 39.5 | 23.1 | 16.4 | 0.00 | 0.0 | 0.09 | 21.7 |
| social | 12 | 40.8 | 40.8 | 0.0 | 0.00 | 0.0 | 0.07 | 25.1 |
| reader | 12 | 47.6 | 25.1 | 22.6 | 0.00 | 0.0 | 0.13 | 83.9 |
| schemer | 12 | 51.0 | 51.0 | 0.0 | 6.01 | 48.1 | 0.09 | 31.1 |
| loyalist | 12 | 42.3 | 42.3 | 0.0 | 0.00 | 0.0 | 0.08 | 24.5 |
| floater | 12 | 46.3 | 46.3 | 0.0 | 0.00 | 0.0 | 0.13 | 22.7 |
| beast | 12 | 49.7 | 49.7 | 0.0 | 0.00 | 0.0 | 0.00 | 11.0 |
| novice | 12 | 38.0 | 19.5 | 18.4 | 0.00 | 0.0 | 0.20 | 21.2 |
| exploit | 12 | 58.4 | 58.4 | 0.0 | 29.52 | 236.2 | 0.07 | 119.6 |
| oracle-reader | 12 | 31.9 | 16.4 | 15.5 | 0.00 | 0.0 | 0.06 | 54.5 |
| oracle-skilled | 12 | 28.8 | 8.8 | 19.9 | 0.00 | 0.0 | 0.02 | 1.7 |

Seats offered depend on how long the player stays and on what they buy, so they differ by policy.

### Game Sense by policy (bootstrap 95% for the mean)

| policy | size | Game Sense | competitions | strategy | social |
|---|---|---|---|---|---|
| passive | 8 | 56.3 [54.4, 58.1] | 78.8 | 52.7 | 38.8 |
| random | 8 | 63.8 [61.7, 65.8] | 89.3 | 65.8 | 35.5 |
| social | 8 | 69.6 [67.4, 71.8] | 91.5 | 74.9 | 40.5 |
| reader | 8 | 71.3 [68.9, 73.5] | 88.7 | 83.5 | 37.8 |
| schemer | 8 | 65.8 [63.8, 67.8] | 91.8 | 76.6 | 25.2 |
| loyalist | 8 | 67.7 [64.7, 70.6] | 82.4 | 72.4 | 46.8 |
| floater | 8 | 64.9 [63.5, 66.2] | 94.0 | 62.4 | 39.2 |
| beast | 8 | 73.6 [72.6, 74.5] | 99.5 | 80.1 | 38.9 |
| novice | 8 | 64.3 [61.6, 66.9] | 82.1 | 67.4 | 42.4 |
| exploit | 8 | 62.9 [60.1, 65.5] | 82.1 | 70.6 | 33.6 |
| oracle-reader | 8 | 70.2 [67.6, 72.6] | 87.9 | 79.4 | 40.1 |
| oracle-skilled | 8 | 61.5 [58.6, 64.0] | 81.2 | 63.8 | 38.8 |
| passive | 12 | 61.3 [59.9, 62.4] | 85.6 | 61.5 | 36.8 |
| random | 12 | 66.9 [65.5, 68.4] | 92.3 | 75.7 | 29.7 |
| social | 12 | 73.2 [71.5, 74.7] | 93.6 | 85.7 | 36.0 |
| reader | 12 | 76.4 [75.5, 77.3] | 97.3 | 97.1 | 28.1 |
| schemer | 12 | 71.1 [69.8, 72.3] | 96.1 | 92.6 | 17.1 |
| loyalist | 12 | 77.3 [75.8, 78.6] | 94.6 | 92.6 | 39.6 |
| floater | 12 | 66.9 [65.8, 68.0] | 97.5 | 71.1 | 30.8 |
| beast | 12 | 73.7 [72.8, 74.5] | 100.0 | 87.0 | 29.5 |
| novice | 12 | 72.4 [70.6, 74.0] | 92.2 | 85.0 | 35.6 |
| exploit | 12 | 69.4 [67.7, 71.2] | 90.6 | 84.0 | 28.6 |
| oracle-reader | 12 | 74.5 [73.2, 76.0] | 93.2 | 90.1 | 35.1 |
| oracle-skilled | 12 | 68.5 [66.9, 69.9] | 88.7 | 77.1 | 36.6 |

Size 8: gated reader minus random 7.6; oracle reader minus random 6.4 (target 20).

Size 12: gated reader minus random 9.5; oracle reader minus random 7.6 (target 20).

### Who goes out, NPC Heads of Household, and the jury (all policies' seasons)

| size | evictee's threat rank (HoH's view) | evictees in a pact | evictee's competition wins | NPC HoH nominated the top threat | jury margin | bitter jurors |
|---|---|---|---|---|---|---|
| 8 | 3.14 | 17.0% | 0.73 | 57.0% | 2.26 | 56.0% of jurors whose evictor reached the final two |
| 12 | 3.69 | 27.6% | 1.12 | 45.7% | 3.54 | 53.4% of jurors whose evictor reached the final two |

### The house: agendas, pairs and the story's pace (all policies' seasons)

| size | NPC agendas as each social week closed | warmest pair (mutual, mean / max) | coldest pair (mutual, mean / min) | story asks / season | weeks with a card | storylines finished | NPC removals | showmances | pile-ons | pariah weeks |
|---|---|---|---|---|---|---|---|---|---|---|
| 8 | build 77.1%, hunt 15.0%, hold 7.0%, drift 0.9% | 153 / 200 | -86 / -200 | 12.0 | 3.5 | 12.0 | 0.00 | 1.02 | 0.00 | 0.05 |
| 12 | build 68.6%, hunt 25.1%, hold 5.8%, drift 0.5% | 191 / 200 | -130 / -200 | 19.5 | 5.5 | 18.7 | 0.00 | 1.18 | 0.01 | 0.19 |

### Pacing: decisions and ceremony seconds a week

Decisions are the player's accepted commands other than Advance. Ceremony seconds are the key ceremony, veto meeting and live eviction at the suspenseful pace, fade to fade, unskipped (CeremonyPacing); the finale's jury reveal apart.

| policy | size | decisions / week (weeks in the house) | ceremony s / regular week | finale reveal s |
|---|---|---|---|---|
| passive | 8 | 3.7 | 44.8 | 21.8 |
| random | 8 | 14.3 | 44.6 | 21.8 |
| social | 8 | 17.4 | 44.2 | 21.8 |
| reader | 8 | 19.8 | 44.3 | 21.8 |
| schemer | 8 | 19.0 | 44.4 | 21.8 |
| loyalist | 8 | 18.3 | 44.5 | 21.8 |
| floater | 8 | 16.4 | 44.4 | 21.8 |
| beast | 8 | 15.6 | 42.9 | 21.8 |
| novice | 8 | 14.9 | 44.8 | 21.8 |
| exploit | 8 | 37.8 | 44.5 | 21.8 |
| oracle-reader | 8 | 17.1 | 44.8 | 21.8 |
| oracle-skilled | 8 | 9.1 | 44.8 | 21.8 |
| passive | 12 | 3.7 | 50.4 | 24.6 |
| random | 12 | 15.5 | 50.2 | 24.6 |
| social | 12 | 19.4 | 50.1 | 24.6 |
| reader | 12 | 25.5 | 50.0 | 24.6 |
| schemer | 12 | 20.5 | 49.9 | 24.6 |
| loyalist | 12 | 19.3 | 49.9 | 24.6 |
| floater | 12 | 17.3 | 50.0 | 24.6 |
| beast | 12 | 16.8 | 48.9 | 24.6 |
| novice | 12 | 15.6 | 50.4 | 24.6 |
| exploit | 12 | 46.8 | 50.1 | 24.6 |
| oracle-reader | 12 | 24.9 | 50.4 | 24.6 |
| oracle-skilled | 12 | 9.6 | 50.4 | 24.6 |

### Refusals and the walker

| policy | size | own commands / season | walker steps / season | refusals / season | most common refusal |
|---|---|---|---|---|---|
| passive | 8 | 0.0 | 89.3 | 0.00 | - |
| random | 8 | 47.9 | 87.8 | 0.00 | - |
| social | 8 | 67.2 | 88.3 | 0.00 | - |
| reader | 8 | 66.6 | 87.8 | 0.00 | - |
| schemer | 8 | 71.7 | 87.6 | 0.00 | - |
| loyalist | 8 | 55.5 | 87.5 | 0.01 | PromiseFinalTwo: This promise is already active. (1) |
| floater | 8 | 62.7 | 87.6 | 0.00 | - |
| beast | 8 | 69.4 | 89.1 | 0.00 | - |
| novice | 8 | 35.0 | 88.3 | 0.00 | - |
| exploit | 8 | 123.2 | 87.1 | 0.18 | RenameAlliance: The Outsiders has had its new name this week. (11) |
| oracle-reader | 8 | 44.4 | 90.2 | 0.00 | - |
| oracle-skilled | 8 | 106.3 | 1.5 | 1.47 | AnswerJury: Choose one of the responses offered. (144) |
| passive | 12 | 0.0 | 146.7 | 0.00 | - |
| random | 12 | 70.4 | 145.8 | 0.00 | - |
| social | 12 | 98.1 | 145.9 | 0.00 | - |
| reader | 12 | 146.0 | 146.7 | 0.00 | - |
| schemer | 12 | 119.3 | 147.0 | 0.00 | - |
| loyalist | 12 | 100.5 | 146.8 | 0.45 | PromiseFinalTwo: This promise is already active. (45) |
| floater | 12 | 98.7 | 146.5 | 0.00 | - |
| beast | 12 | 100.7 | 147.7 | 0.00 | - |
| novice | 12 | 65.7 | 148.8 | 0.00 | - |
| exploit | 12 | 211.4 | 144.0 | 0.25 | RenameAlliance: The Outsiders has had its new name this week. (14) |
| oracle-reader | 12 | 89.9 | 148.1 | 0.00 | - |
| oracle-skilled | 12 | 173.6 | 1.6 | 1.56 | AnswerJury: Choose one of the responses offered. (150) |

Seasons with an error (walker refused, invalid state, unfinished): 0 of 2400. The oracle skilled player's
refusals are its own walker answering the finale's questions with the old catalogue's "A"; the lab's walker then
answers with an offered response.

## What this baseline does not cover

- **The NPC world (B5).** No NPC-to-NPC conversation, no all-week beats; the plan's sensitivity table against
  the tick budget comes with the driver.
- **Seed counts (B6).** 100 a cell; the plan's 800 is about two hours of an idle machine. Rows and goldens per
  cell are not recorded yet.
- **Human performance (B8).** The per-policy distributions are assumptions; the fixed-level tier bounds them.
- **Sizes and rosters.** 8 and 12, regular roster only: no 4, 6 or 10, no All-Stars, no 13-16.
- **Policy strength.** Each policy is one plausible script of its style, not an optimum; the oracles in
  particular are not upper bounds (F2). Comparisons are between these scripts on the same seasons.

# Balance baseline (first, partial)

**Status: a first, partial baseline (BALANCE plan B2-B4's headline run), not B6's.** It is 100 seasons per
policy and size, not the plan's 800 (see *Runtime*), the NPC world is not driven in any of these seasons
(B5 is out of scope), and the per-policy performance distributions are assumptions until human data exists
(B8). Every number here is reproducible from the commands below; treat differences under the Wilson
intervals as noise.

Measured on 2026-10-08 at `95e99bd2` (branch `claude/balance-lab`, on `claude/lead-integration` at
`ca7f4da6`): schema 28, competition rules 4, story rules 9, the economy, agency, the finale, the commitment
rules, D4's leaks and the unified commitment and hearing version 1 - exactly what the director started
then, because every lab season is built by `SeasonBuilder.Create` and `ShippedRules.ApplyFresh` (B0).

**Not yet the shipped game: the war rooms.** D3 landed beside this lab, and `ShippedRules.ApplyFresh` now
switches its war rooms on, but none of the lab's policies answers a war room's plan yet: a player in a
pact of three falls through to Advance, and every open plan lapses as if they lay low. So these numbers
leave out going with a plan and pushing back on one. B6a gives each policy a war-room answer and B6b
reruns this page; until then read every row as measured before the war rooms. The pact counts in the
first version also read a pact's party from the end alone, so a pact of three the player left standing
counted as the house's; the autopsy now reads it from every phase change it saw.

## What changed since the first version (`a81ab572`)

The first version of this page was measured with a lab defect that review found; its F1, F2, F4 and F7
were wrong because of it, and they are restated below.

- **A veto holder on the block now saves themselves.** The lab fell back on the engine tests' walker at
  the veto meeting, and that walker uses the veto on the first nominee, whoever that is: a player who won
  the veto from the block's second chair stayed up. The passive player, the novice and both oracles fell
  back on it, and in the first run 16 to 42 seasons per cell of 100 ended with the player evicted in a week
  they held the veto (at eight, every passive and every novice player who won the week-one veto while
  nominated went home that week). The lab's walker and the oracles now save the player
  (`BalanceLab.SavesThePlayer`), and the novice saves itself and nobody else. A test plays such a meeting
  with every policy, and the smoke tier asserts it never happens. In this run no season has the player
  evicted in a week they won the veto.
- **No walk-ins.** The exploit hunter could test every pair of houseguests for a walk-in, but the director
  offers one only for a pair the NPC world puts together, and the lab does not drive that world. The view
  no longer offers walk-ins (until B5). The story sweep's skilled player, an oracle, still walks in as that
  sweep does.
- **The story's pace is counted as `StoryPacingTests.PacingReport` counts it** (the shared `Pace.Watch`).
  The first version's "12 and 19.5 beats asked a season" were every story house event, which cannot be
  read against the plan's 4-6 asks.
- **What moved.** The rows of random, social, reader, schemer, loyalist, floater and the beast are the same
  in every table: their seasons never reached the walker at a veto meeting they held. Only their McNemar
  against the passive player moved. The passive player, the novice, the exploit hunter and both oracles
  moved.
- **Also changed:**
  - The knowledge gate's test now hides the season's whole state: the type closure of `EpisodeState`,
    with the vote read's claims allowed.
  - The smoke tier covers 4, 6, 8, 10 and 12 and the All-Stars eight and twelve.
  - The full tier exists, but has not been run.
  - The state is validated after every Advance.
  - The report itself now emits the pairs and the B-1 band below, so they are no longer worked out by hand.

## How it was measured

- **Seasons.** Regular roster, 8 and 12 houseguests. Each step the policy proposes a command (up to three
  times, told each refusal). Otherwise the lab's walker takes the phase's own step: the engine tests'
  walker, except that a veto holder on the block saves themselves. Every competition's performance comes
  from the performance model. The state is validated after every Advance, at every phase change and at the
  end. 0 of 2,400 headline seasons and 0 of 600 performance seasons had an error (walker refused, invalid
  state or unfinished).
- **Policies** (`BalanceLabPolicies.cs`) see only a `PlayerView` (`BalanceLabView.cs`): the screens'
  readers and the controls they offer. The readers are KnownOdds, the vote read, AllianceRead, KnownBallots,
  the player's own view, HouseguestNotes, WaitingOnYou, CampaignBrief, the story's shown odds, and the
  player's commitments and breach warnings. Walk-ins are not offered.
  - Ten policies are knowledge-gated: passive, random, social, reader, schemer, loyalist, floater,
    competition beast, novice and exploit hunter.
  - Two are labelled **oracles**, because they read the season itself: GameSense's reader
    (`oracle-reader`) and the story sweep's skilled player (`oracle-skilled`).
- **Performance** (`BalanceLabPerformance.cs`): a normal draw cut to [0, 1] about each policy's mean - beast
  .8 (spread .12), novice .35 (.18), everyone else .5 (.15) - keyed by a hash of the season's seed, week and
  phase, never the season's generator, so every policy meets the same luck. The performance tier holds it
  fixed at 0, .25, .5, .75 and 1.
- **Seeds** are a hash of the house and the index, never of the policy, so every policy plays the same 100
  seasons at each size and McNemar's test pairs them.
- **Metrics** come from three places:
  - `SeasonAutopsy` (B1);
  - the story's pace from `StoryPacingTests.Pace`;
  - decisions a week and ceremony seconds a week from the lab (`CeremonyPacing`, suspenseful pace,
    unskipped).

Reproduce (Unity-free, from the repository root):

```
dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabTests"                                  # smoke tier
BALANCE_SEEDS=100 dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabReports.HeadlineReport"
BALANCE_PERFORMANCE_SEEDS=60 dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabReports.PerformanceReport"
BALANCE_FULL_SEEDS=800 dotnet test Tools/SimulationTests --filter "FullyQualifiedName~BalanceLabReports.FullReport"   # overnight; not run
```

Rows land as JSONL, and these tables as Markdown, in `Tools/SimulationTests/bin/Debug/net10.0/balance/`.

## Runtime

| tier | seasons | wall time | note |
|---|---|---|---|
| smoke (not Explicit, in the subset) | 84 (12 policies x 4, 6, 8, 10, 12 and All-Stars 8, 12) | about 27 s on 6 threads | shared the machine with other lanes' test hosts |
| headline | 2,400 (12 policies x 8, 12 x 100) | 16.9 min on 6 threads | shared the machine with other lanes' test hosts |
| performance (B4) | 600 (passive x 5 levels x 8, 12 x 60) | 2.5 min | |
| full (overnight) | not run (67,200 at 800 seeds) | | |

At the headline's rate, about 0.42 s of wall time a season on six threads, the plan's 800 seeds a cell would
take about 2.3 hours for the headline grid and about eight hours for the full grid (estimates). The first
version's 46.9 minutes for the same 2,400 seasons was measured while two Unity batch runs shared the machine.

## Findings F1-F7

**F1. Strategy barely moves win rate - holds at eight; at twelve, playing matters but the style does not,
within the noise.**

- **At 8** (base rate 12.5%) the passive player wins 12% [7.0, 19.8], about the base rate, and no gated
  style separates from it at 100 seeds:
  - social and floater win 19% (McNemar against passive p = 0.23 and 0.27);
  - loyalist 17% (p = 0.42), schemer 14%, reader 13% and the novice 11%;
  - the random player wins 7% (p = 0.36) and the exploit hunter 2% (p = 0.013), worse than doing nothing.
- **At 12** (base rate 8.3%) the passive player wins 7% [3.4, 13.7] and every engaged gated style beats it:
  - random 20% (p = 0.021), social 23% (0.006), schemer 25% (0.003) and reader 26% (0.001);
  - floater 29% and loyalist 32% (both p < 0.001);
  - the novice 18% (p = 0.046);
  - the exploit hunter's 10% does not (p = 0.63).
- **Between styles** the differences stay inside the noise. Reader against random is p = 0.21 at 8 and
  0.41 at 12; reader against social is 0.33 and 0.75. Social beats random at 8 (19% against 7%,
  p = 0.017) but not at 12 (0.73).
- The competition beast wins 81% and 58% (see F3).
- The Game Sense gap, gated reader minus random, is 7.6 at 8 and 9.5 at 12, against the target of 20.

**F2. The skilled policies are oracles - confirmed that they read hidden state; not settled whether they
bound skill.**

- **Reader against oracle reader.** The oracle reader wins 23% at 8 and 27% at 12. The gated reader, which
  never sees a hidden view, wins 13% and 26% (McNemar 11/21, p = 0.11 at 8; 20/21, p = 1.0 at 12).
  Reading the hidden state may be worth about ten points at 8, and nothing visible at 12. 100 seeds cannot
  separate it.
- **The skilled oracle.** The story sweep's skilled player wins 15% at both sizes, below the gated reader
  at 12 (p = 0.091).
- **Neither oracle is a strong bound.** The oracle reader spends about half its seats (14.4 of 27.2 at 8,
  21.1 of 42.5 at 12), and the skilled player under a third (7.0 of 22.5, 11.0 of 34.9).

**F3. Competitions run on the performance input - confirmed, and preparation matters more.**

- **The passive player's weekly competitions in real seasons** (at 8 / at 12), against a base rate of one
  in the field (about 19% at 8 and 16% at 12):
  - at no performance it wins 23% / 27%;
  - at half marks, the accessible alternative's value, 49% / 53% - about half of all weekly competitions;
  - at .75, 66% / 69%;
  - at full marks, 80% / 79%.
- **Whole seasons.** A passive player at full marks wins 37% and 40% of seasons, and at half marks 12% and
  8%.
- **The beast.** The competition beast has a performance of about .8 and studies the house in free time
  until its preparation is 5. It wins 97.5% and 99.1% of the weekly competitions it plays, and 81% and 58% of
  seasons.
- **Preparation.** Preparation is permanent and caps at +5 competition points
  (`WebStudyHouse.ApplyBonus`, set only by `StudyHouse`), more than full marks are worth (+3). With a
  performance of about .8, five free-time seats bought near-certain competitions for the rest of the season.
  The preparation's own share is not isolated here (a studying player at half marks would separate it), but
  full marks alone reach only 79-80%.
- **The houseguests.** Statistics decide a good deal:
  - the field's strongest on paper (`ExpectedWin`, which weighs the competition's category) wins 45% of
    weekly competitions at both sizes, against about 19% and 16% for a uniform draw;
  - the top NPC takes 38% and 29% of the NPC wins;
  - the Spearman correlation of an NPC's wins with its statistic sum is weak (0.121 at 8, 0.113 at 12), but
    it is a poor measure here, because the sums span only 29-36 (sd 2.0) and ignore the category weights.

**F4. The newcomer has built-in penalties - confirmed for the passive newcomer, neutralised by play.**

- **The passive player** is nominated more often than the houseguests per week in the house:

  | size | passive player | houseguests | ratio |
  |---|---|---|---|
  | 8 | 0.61 | 0.52 | 1.17 |
  | 12 | 0.60 | 0.43 | 1.42 |

  It is on the block in week one 41% and 55% of the time, against 29% and 18% for a uniform draw over the
  non-HoH house.
- **The other policies' ratios:**
  - the engaged gated policies other than the beast run from 0.73 (social at 8) to 1.11 (random at 12);
  - the novice sits at 1.02 and 0.99, and the exploit hunter at 1.05 and 1.21;
  - the beast sits at 0.42 and 0.67.

**F5. Sweeps never run the NPC world - confirmed, and true of this lab too.** Every lab season has 0 NPC ticks
(the smoke tier asserts it). No number here includes NPC-to-NPC conversation or the D2 beats; B5 adds the driver.

**F6. Harness rules drift from what ships - resolved for the lab.**

- Every lab season starts through `ShippedRules.ApplyFresh`, the method the director now calls (B0).
- That method is pinned field by field, and by the whole season's JSON, against the director's inline
  lines at `ca7f4da6`.
- A source scan fails if the director sets a rule itself.
- The older harnesses (GameSense's default arm, the story sweeps) still build their own seasons.

**F7. E4 may not be observable - depends on size; at eight a loss in the first sessions is likely, though
not in week one.**

- **The novice** (the first-timer model) leaves early more often at 8 than at 12:

  | size | evicted in week one [Wilson] | out by week two | out by week three |
  |---|---|---|---|
  | 8 | 19% [12.5, 27.8] | 34% | 44% |
  | 12 | 2% [0.6, 7.0] | 13% | 20% |

- **Three testers.** The chance of at least one natural loss among three testers (independence assumed):

  | size | by week one | by week two | by week three |
  |---|---|---|---|
  | 8 | 47% | 71% | 82% |
  | 12 | 6% | 34% | 49% |

- **A 90% chance.** From the survival table, a 90% chance of at least one loss needs about five to six
  weeks at 8 (90% by week five, 95% by week six) and six to seven at 12 (89% by week six, 94% by week seven).
- The plan's rough 35% guess is low for an 8-house and high for week one of a 12-house (B7 to refine).

**B-1, the lead's proposed band: a knowledge-gated skilled policy wins at least 1.5x the base rate.** The
skilled policies are social, reader, schemer, loyalist and floater.

- **At 12** (bar 12.5%) all five clear it with their whole Wilson interval: 23-32%, low ends 15.8-23.7%.
- **At 8** (bar 18.75%) only social and floater reach it (19%), and not surely (low ends 12.5%); reader
  13%, schemer 14% and loyalist 17% fall short.
- As measured, the band holds at 12 and fails at 8.

## Other findings

- **Buying time does not pay.** The exploit hunter:
  - buys 20 and 29 conversations a season, paying 160 and 230 goodwill;
  - spends every seat and every free action it can find (60 and 116 a season);
  - has the worst win rates of the engaged policies: 2% and 10% (against random p = 0.18 and 0.031,
    against passive 0.013 and 0.63).

  No repeated free action showed up as an exploit: its refusals stay under 0.7 a season, almost all a pact's
  weekly rename. Walk-ins are no longer on offer to it.
- **The house deals among itself far more than with the player.**
  - NPC-only deals: about 11-18 a season at 8 and 47-59 at 12 (about a quarter kept, under 1 broken),
    against 0.4-12 deals with the player.
  - NPC-only pacts: 1.0-1.3 at 8, and 2.4-2.8 at 12.
  - The player's deal ceiling binds only at 12, for 0.7-3.4 weeks a season depending on the policy.
- **Pairs saturate.** The season's warmest pair ends at a mean mutual 154 and 191 (max 200, both views at
  +100); the coldest at -84 and -128 (min -200).
- **NPC Heads of Household** nominate their top threat 58% and 46% of the time. The evictee averages third
  or fourth in the HoH's threat ranking (3.18 and 3.76) and is in a pact 17% and 28% of the time.
- **Jury.** Mean margin 2.3 and 3.6 votes; among jurors whose evictor reached the final two, 56% and 53% vote
  against that evictor.
- **Story, in PacingReport's terms:**

  | measure | at 8 | at 12 | target |
  |---|---|---|---|
  | budgeted asks a season | 4.8 (most in one season: 9, the eight's ceiling) | 8.3 (most: 17) | 4-6 |
  | must-fires a season | 0.2 | 0.2 | |
  | weeks with a card | 50% | 50% | 30-60% |
  | stories finished | 4.5 | 7.6 | about 3 |
  | seasons with a pariah | 14% | 45% | at most 20% |
  | NPC showmances a season | about one | about one | |
  | seasons with a pile-on | 0.2% | 0.6% | |

  - The pariah rate at 12 is more than twice its target: a candidate for the story session, measured
    without the NPC world.
  - There was no NPC removal at all in 2,400 seasons.
  - The autopsy's own count of story house events is 12.7 and 20.6 a season. The first version called that
    figure "beats asked".
- **Pacing.**
  - Decisions a week in the house: 3.8-3.9 for the passive player, 14-25 for the engaged gated policies
    (9-10 for the skilled oracle), and 38-47 for the exploit hunter.
  - Ceremonies: about 44 s a regular week at 8 and 50 s at 12; the finale's jury reveal takes 22 s and 25 s.

## The headline tables

All 2,400 seasons: 100 per policy and size, the same 100 seasons for every policy.

### Win rate by policy and size

Base rate is 1/size. McNemar pairs each policy with the passive player on the same seasons (b: only this policy won, c: only passive won).

| policy | size | n | wins | win rate | Wilson 95% | final two | mean placement | vs passive b/c | p |
|---|---|---|---|---|---|---|---|---|---|
| passive | 8 | 100 | 12 | 12.0% | [7.0%, 19.8%] | 22.0% | 5.19 | - | - |
| random | 8 | 100 | 7 | 7.0% | [3.4%, 13.7%] | 40.0% | 4.11 | 7/12 | 0.359 |
| social | 8 | 100 | 19 | 19.0% | [12.5%, 27.8%] | 42.0% | 3.53 | 16/9 | 0.230 |
| reader | 8 | 100 | 13 | 13.0% | [7.8%, 21.0%] | 38.0% | 4.03 | 13/12 | 1.000 |
| schemer | 8 | 100 | 14 | 14.0% | [8.5%, 22.1%] | 38.0% | 3.88 | 11/9 | 0.824 |
| loyalist | 8 | 100 | 17 | 17.0% | [10.9%, 25.5%] | 33.0% | 4.69 | 15/10 | 0.424 |
| floater | 8 | 100 | 19 | 19.0% | [12.5%, 27.8%] | 36.0% | 3.62 | 18/11 | 0.265 |
| beast | 8 | 100 | 81 | 81.0% | [72.2%, 87.5%] | 95.0% | 1.45 | 75/6 | 0.000 |
| novice | 8 | 100 | 11 | 11.0% | [6.3%, 18.6%] | 36.0% | 4.50 | 9/10 | 1.000 |
| exploit | 8 | 100 | 2 | 2.0% | [0.6%, 7.0%] | 35.0% | 4.84 | 2/12 | 0.013 |
| oracle-reader | 8 | 100 | 23 | 23.0% | [15.8%, 32.2%] | 35.0% | 3.98 | 22/11 | 0.082 |
| oracle-skilled | 8 | 100 | 15 | 15.0% | [9.3%, 23.3%] | 32.0% | 4.93 | 12/9 | 0.664 |
| passive | 12 | 100 | 7 | 7.0% | [3.4%, 13.7%] | 10.0% | 8.52 | - | - |
| random | 12 | 100 | 20 | 20.0% | [13.3%, 28.9%] | 27.0% | 6.62 | 20/7 | 0.021 |
| social | 12 | 100 | 23 | 23.0% | [15.8%, 32.2%] | 28.0% | 6.38 | 23/7 | 0.006 |
| reader | 12 | 100 | 26 | 26.0% | [18.4%, 35.4%] | 34.0% | 5.42 | 24/5 | 0.001 |
| schemer | 12 | 100 | 25 | 25.0% | [17.5%, 34.3%] | 35.0% | 5.42 | 25/7 | 0.003 |
| loyalist | 12 | 100 | 32 | 32.0% | [23.7%, 41.7%] | 35.0% | 5.94 | 31/6 | 0.000 |
| floater | 12 | 100 | 29 | 29.0% | [21.0%, 38.5%] | 31.0% | 5.41 | 29/7 | 0.000 |
| beast | 12 | 100 | 58 | 58.0% | [48.2%, 67.2%] | 63.0% | 4.11 | 53/2 | 0.000 |
| novice | 12 | 100 | 18 | 18.0% | [11.7%, 26.7%] | 28.0% | 6.04 | 18/7 | 0.046 |
| exploit | 12 | 100 | 10 | 10.0% | [5.5%, 17.4%] | 21.0% | 7.69 | 10/7 | 0.629 |
| oracle-reader | 12 | 100 | 27 | 27.0% | [19.3%, 36.4%] | 31.0% | 6.23 | 25/5 | 0.001 |
| oracle-skilled | 12 | 100 | 15 | 15.0% | [9.3%, 23.3%] | 18.0% | 7.55 | 12/4 | 0.077 |

### Further pairs

McNemar on the same seasons: b where only the first won, c where only the second did.

| first | second | size | first wins | second wins | b/c | p |
|---|---|---|---|---|---|---|
| reader | oracle-reader | 8 | 13 | 23 | 11/21 | 0.112 |
| reader | oracle-skilled | 8 | 13 | 15 | 12/14 | 0.845 |
| social | random | 8 | 19 | 7 | 17/5 | 0.017 |
| reader | random | 8 | 13 | 7 | 11/5 | 0.210 |
| loyalist | random | 8 | 17 | 7 | 15/5 | 0.041 |
| reader | social | 8 | 13 | 19 | 10/16 | 0.327 |
| novice | passive | 8 | 11 | 12 | 9/10 | 1.000 |
| exploit | random | 8 | 2 | 7 | 2/7 | 0.180 |
| reader | oracle-reader | 12 | 26 | 27 | 20/21 | 1.000 |
| reader | oracle-skilled | 12 | 26 | 15 | 23/12 | 0.091 |
| social | random | 12 | 23 | 20 | 18/15 | 0.728 |
| reader | random | 12 | 26 | 20 | 21/15 | 0.405 |
| loyalist | random | 12 | 32 | 20 | 26/14 | 0.082 |
| reader | social | 12 | 26 | 23 | 21/18 | 0.749 |
| novice | passive | 12 | 18 | 7 | 18/7 | 0.046 |
| exploit | random | 12 | 10 | 20 | 4/14 | 0.031 |

### The proposed B-1 band: a knowledge-gated skilled policy wins at least 1.5x the base rate

The bar is 1.5 / size. Clears: the win rate is at or over the bar; surely: the Wilson interval's low end is too.

| policy | size | bar | win rate [Wilson] | clears | surely |
|---|---|---|---|---|---|
| social | 8 | 18.8% | 19.0% [12.5%, 27.8%] | yes | no |
| reader | 8 | 18.8% | 13.0% [7.8%, 21.0%] | no | no |
| schemer | 8 | 18.8% | 14.0% [8.5%, 22.1%] | no | no |
| loyalist | 8 | 18.8% | 17.0% [10.9%, 25.5%] | no | no |
| floater | 8 | 18.8% | 19.0% [12.5%, 27.8%] | yes | no |
| social | 12 | 12.5% | 23.0% [15.8%, 32.2%] | yes | yes |
| reader | 12 | 12.5% | 26.0% [18.4%, 35.4%] | yes | yes |
| schemer | 12 | 12.5% | 25.0% [17.5%, 34.3%] | yes | yes |
| loyalist | 12 | 12.5% | 32.0% [23.7%, 41.7%] | yes | yes |
| floater | 12 | 12.5% | 29.0% [21.0%, 38.5%] | yes | yes |

### Survival by week: the player still in the house after week k

| policy (8) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 |
|---|---|---|---|---|---|---|
| passive | 73.0% | 54.0% | 45.0% | 39.0% | 36.0% | 22.0% |
| random | 85.0% | 72.0% | 67.0% | 61.0% | 57.0% | 40.0% |
| social | 92.0% | 81.0% | 75.0% | 70.0% | 68.0% | 42.0% |
| reader | 85.0% | 73.0% | 66.0% | 63.0% | 59.0% | 38.0% |
| schemer | 91.0% | 83.0% | 68.0% | 63.0% | 55.0% | 38.0% |
| loyalist | 76.0% | 61.0% | 51.0% | 48.0% | 45.0% | 33.0% |
| floater | 94.0% | 81.0% | 74.0% | 72.0% | 62.0% | 36.0% |
| beast | 99.0% | 95.0% | 95.0% | 95.0% | 95.0% | 95.0% |
| novice | 81.0% | 66.0% | 56.0% | 53.0% | 47.0% | 36.0% |
| exploit | 72.0% | 63.0% | 51.0% | 47.0% | 46.0% | 35.0% |
| oracle-reader | 84.0% | 78.0% | 65.0% | 60.0% | 57.0% | 35.0% |
| oracle-skilled | 74.0% | 59.0% | 45.0% | 42.0% | 40.0% | 32.0% |

| policy (12) | wk 1 | wk 2 | wk 3 | wk 4 | wk 5 | wk 6 | wk 7 | wk 8 | wk 9 | wk 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| passive | 88.0% | 74.0% | 53.0% | 34.0% | 26.0% | 19.0% | 13.0% | 12.0% | 12.0% | 10.0% |
| random | 94.0% | 77.0% | 67.0% | 54.0% | 47.0% | 43.0% | 38.0% | 36.0% | 35.0% | 27.0% |
| social | 94.0% | 85.0% | 74.0% | 64.0% | 54.0% | 42.0% | 34.0% | 32.0% | 32.0% | 28.0% |
| reader | 100.0% | 93.0% | 88.0% | 74.0% | 67.0% | 53.0% | 42.0% | 41.0% | 40.0% | 34.0% |
| schemer | 97.0% | 92.0% | 84.0% | 72.0% | 66.0% | 52.0% | 47.0% | 45.0% | 43.0% | 35.0% |
| loyalist | 95.0% | 87.0% | 77.0% | 65.0% | 52.0% | 49.0% | 40.0% | 38.0% | 36.0% | 35.0% |
| floater | 100.0% | 93.0% | 90.0% | 81.0% | 65.0% | 49.0% | 41.0% | 40.0% | 40.0% | 31.0% |
| beast | 100.0% | 85.0% | 85.0% | 73.0% | 73.0% | 63.0% | 63.0% | 63.0% | 63.0% | 63.0% |
| novice | 98.0% | 87.0% | 80.0% | 71.0% | 56.0% | 48.0% | 40.0% | 36.0% | 34.0% | 28.0% |
| exploit | 88.0% | 77.0% | 63.0% | 45.0% | 36.0% | 25.0% | 23.0% | 22.0% | 21.0% | 21.0% |
| oracle-reader | 96.0% | 84.0% | 76.0% | 61.0% | 50.0% | 43.0% | 37.0% | 37.0% | 35.0% | 31.0% |
| oracle-skilled | 92.0% | 77.0% | 66.0% | 49.0% | 39.0% | 24.0% | 22.0% | 22.0% | 21.0% | 18.0% |

### Early risk (F7): nominated and evicted in week one, out by week k, and the chance at least one of three testers is out

| policy | size | nominated wk 1 | evicted wk 1 [Wilson] | out by wk 2 | out by wk 3 | P(≥1 of 3 out) wk 1 / 2 / 3 |
|---|---|---|---|---|---|---|
| passive | 8 | 41.0% | 27.0% [19.3%, 36.4%] | 46.0% | 55.0% | 61.1% / 84.3% / 90.9% |
| random | 8 | 29.0% | 15.0% [9.3%, 23.3%] | 28.0% | 33.0% | 38.6% / 62.7% / 69.9% |
| social | 8 | 30.0% | 8.0% [4.1%, 15.0%] | 19.0% | 25.0% | 22.1% / 46.9% / 57.8% |
| reader | 8 | 38.0% | 15.0% [9.3%, 23.3%] | 27.0% | 34.0% | 38.6% / 61.1% / 71.3% |
| schemer | 8 | 49.0% | 9.0% [4.8%, 16.2%] | 17.0% | 32.0% | 24.6% / 42.8% / 68.6% |
| loyalist | 8 | 32.0% | 24.0% [16.7%, 33.2%] | 39.0% | 49.0% | 56.1% / 77.3% / 86.7% |
| floater | 8 | 36.0% | 6.0% [2.8%, 12.5%] | 19.0% | 26.0% | 16.9% / 46.9% / 59.5% |
| beast | 8 | 4.0% | 1.0% [0.2%, 5.4%] | 5.0% | 5.0% | 3.0% / 14.3% / 14.3% |
| novice | 8 | 39.0% | 19.0% [12.5%, 27.8%] | 34.0% | 44.0% | 46.9% / 71.3% / 82.4% |
| exploit | 8 | 37.0% | 28.0% [20.1%, 37.5%] | 37.0% | 49.0% | 62.7% / 75.0% / 86.7% |
| oracle-reader | 8 | 43.0% | 16.0% [10.1%, 24.4%] | 22.0% | 35.0% | 40.7% / 52.5% / 72.5% |
| oracle-skilled | 8 | 38.0% | 26.0% [18.4%, 35.4%] | 41.0% | 55.0% | 59.5% / 79.5% / 90.9% |
| passive | 12 | 55.0% | 12.0% [7.0%, 19.8%] | 26.0% | 47.0% | 31.9% / 59.5% / 85.1% |
| random | 12 | 39.0% | 6.0% [2.8%, 12.5%] | 23.0% | 33.0% | 16.9% / 54.3% / 69.9% |
| social | 12 | 30.0% | 6.0% [2.8%, 12.5%] | 15.0% | 26.0% | 16.9% / 38.6% / 59.5% |
| reader | 12 | 21.0% | 0.0% [0.0%, 3.7%] | 7.0% | 12.0% | 0.0% / 19.6% / 31.9% |
| schemer | 12 | 37.0% | 3.0% [1.0%, 8.5%] | 8.0% | 16.0% | 8.7% / 22.1% / 40.7% |
| loyalist | 12 | 33.0% | 5.0% [2.2%, 11.2%] | 13.0% | 23.0% | 14.3% / 34.1% / 54.3% |
| floater | 12 | 25.0% | 0.0% [0.0%, 3.7%] | 7.0% | 10.0% | 0.0% / 19.6% / 27.1% |
| beast | 12 | 1.0% | 0.0% [0.0%, 3.7%] | 15.0% | 15.0% | 0.0% / 38.6% / 38.6% |
| novice | 12 | 42.0% | 2.0% [0.6%, 7.0%] | 13.0% | 20.0% | 5.9% / 34.1% / 48.8% |
| exploit | 12 | 43.0% | 12.0% [7.0%, 19.8%] | 23.0% | 37.0% | 31.9% / 54.3% / 75.0% |
| oracle-reader | 12 | 37.0% | 4.0% [1.6%, 9.8%] | 16.0% | 24.0% | 11.5% / 40.7% / 56.1% |
| oracle-skilled | 12 | 53.0% | 8.0% [4.1%, 15.0%] | 23.0% | 34.0% | 22.1% / 54.3% / 71.3% |

### Nominations per week in the house (F4): the player against the houseguests of the same seasons

| policy | size | player | houseguests | ratio |
|---|---|---|---|---|
| passive | 8 | 0.606 | 0.519 | 1.17 |
| random | 8 | 0.450 | 0.530 | 0.85 |
| social | 8 | 0.379 | 0.522 | 0.73 |
| reader | 8 | 0.477 | 0.516 | 0.92 |
| schemer | 8 | 0.428 | 0.517 | 0.83 |
| loyalist | 8 | 0.502 | 0.514 | 0.98 |
| floater | 8 | 0.440 | 0.515 | 0.85 |
| beast | 8 | 0.203 | 0.483 | 0.42 |
| novice | 8 | 0.523 | 0.512 | 1.02 |
| exploit | 8 | 0.539 | 0.513 | 1.05 |
| oracle-reader | 8 | 0.491 | 0.541 | 0.91 |
| oracle-skilled | 8 | 0.528 | 0.529 | 1.00 |
| passive | 12 | 0.604 | 0.425 | 1.42 |
| random | 12 | 0.472 | 0.427 | 1.11 |
| social | 12 | 0.428 | 0.423 | 1.01 |
| reader | 12 | 0.369 | 0.436 | 0.85 |
| schemer | 12 | 0.368 | 0.431 | 0.85 |
| loyalist | 12 | 0.421 | 0.421 | 1.00 |
| floater | 12 | 0.405 | 0.420 | 0.96 |
| beast | 12 | 0.260 | 0.388 | 0.67 |
| novice | 12 | 0.427 | 0.430 | 0.99 |
| exploit | 12 | 0.505 | 0.417 | 1.21 |
| oracle-reader | 12 | 0.449 | 0.435 | 1.03 |
| oracle-skilled | 12 | 0.499 | 0.430 | 1.16 |

### The player's competitions by policy (weekly HoH and veto they played)

| policy | size | played / season | won | win rate [Wilson] | mean performance | HoH wins / season | veto wins / season |
|---|---|---|---|---|---|---|---|
| passive | 8 | 5.05 | 258 | 51.1% [46.7%, 55.4%] | 0.51 | 1.28 | 1.52 |
| random | 8 | 6.18 | 323 | 52.3% [48.3%, 56.2%] | 0.51 | 1.80 | 1.77 |
| social | 8 | 6.76 | 345 | 51.0% [47.3%, 54.8%] | 0.51 | 1.93 | 1.88 |
| reader | 8 | 6.28 | 324 | 51.6% [47.7%, 55.5%] | 0.51 | 1.80 | 1.75 |
| schemer | 8 | 6.65 | 346 | 52.0% [48.2%, 55.8%] | 0.51 | 1.90 | 1.88 |
| loyalist | 8 | 5.32 | 257 | 48.3% [44.1%, 52.6%] | 0.51 | 1.52 | 1.37 |
| floater | 8 | 6.85 | 357 | 52.1% [48.4%, 55.8%] | 0.50 | 1.93 | 1.97 |
| beast | 8 | 7.47 | 728 | 97.5% [96.1%, 98.4%] | 0.80 | 3.80 | 4.43 |
| novice | 8 | 5.91 | 266 | 45.0% [41.0%, 49.0%] | 0.36 | 1.48 | 1.49 |
| exploit | 8 | 5.37 | 262 | 48.8% [44.6%, 53.0%] | 0.51 | 1.50 | 1.47 |
| oracle-reader | 8 | 6.26 | 319 | 51.0% [47.0%, 54.9%] | 0.52 | 1.76 | 1.76 |
| oracle-skilled | 8 | 5.15 | 256 | 49.7% [45.4%, 54.0%] | 0.51 | 1.55 | 1.32 |
| passive | 12 | 6.22 | 327 | 52.6% [48.6%, 56.5%] | 0.51 | 1.46 | 1.91 |
| random | 12 | 8.27 | 431 | 52.1% [48.7%, 55.5%] | 0.51 | 2.19 | 2.39 |
| social | 12 | 8.37 | 440 | 52.6% [49.2%, 55.9%] | 0.50 | 2.36 | 2.32 |
| reader | 12 | 9.66 | 522 | 54.0% [50.9%, 57.2%] | 0.50 | 2.68 | 2.85 |
| schemer | 12 | 9.51 | 527 | 55.4% [52.2%, 58.5%] | 0.51 | 2.68 | 2.92 |
| loyalist | 12 | 8.89 | 487 | 54.8% [51.5%, 58.0%] | 0.51 | 2.52 | 2.70 |
| floater | 12 | 9.71 | 518 | 53.3% [50.2%, 56.5%] | 0.51 | 2.62 | 2.87 |
| beast | 12 | 9.90 | 981 | 99.1% [98.3%, 99.5%] | 0.80 | 4.44 | 6.00 |
| novice | 12 | 9.19 | 382 | 41.6% [38.4%, 44.8%] | 0.37 | 1.90 | 2.16 |
| exploit | 12 | 6.90 | 363 | 52.6% [48.9%, 56.3%] | 0.50 | 1.91 | 1.92 |
| oracle-reader | 12 | 8.71 | 472 | 54.2% [50.9%, 57.5%] | 0.52 | 2.42 | 2.61 |
| oracle-skilled | 12 | 7.14 | 363 | 50.8% [47.2%, 54.5%] | 0.51 | 1.86 | 1.95 |

### Competition by fixed performance (B4: the passive player, 60 seasons a level and size)

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

The field buckets count the competitions the player played at that field size (in brackets).

### Competition concentration (all policies' seasons; NPC rows from the passive player's)

| size | Spearman NPC wins vs stat sum | top NPC's share of NPC wins | weekly comps won by the strongest on paper | mean winner rank on paper | field size |
|---|---|---|---|---|---|
| 8 | 0.121 | 37.7% | 45.3% | 2.16 | 5.3 |
| 12 | 0.113 | 28.9% | 45.1% | 2.18 | 6.4 |

### Pacts and deals per season

Player deals count every deal the player is party to, the house's offers to them included, in any status.

| policy | size | player pacts | NPC-only pacts | player deals (kept/broken) | NPC-only deals (kept/broken) | player promises (kept/broken) | oaths | weeks at the deal ceiling |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 0.00 | 1.21 | 0.48 (0.00/0.00) | 14.83 (3.81/0.19) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| random | 8 | 0.07 | 1.19 | 3.08 (0.29/0.41) | 15.38 (4.31/0.22) | 0.97 (0.38/0.39) | 0.02 | 0.00 |
| social | 8 | 0.38 | 1.16 | 4.21 (0.57/1.09) | 15.03 (4.05/0.22) | 3.39 (1.17/1.10) | 0.20 | 0.00 |
| reader | 8 | 0.23 | 1.12 | 5.42 (2.84/1.24) | 14.40 (4.03/0.13) | 0.44 (0.01/0.05) | 0.00 | 0.00 |
| schemer | 8 | 0.53 | 0.97 | 7.13 (2.79/2.17) | 11.08 (3.05/0.20) | 2.33 (0.97/0.78) | 0.00 | 0.00 |
| loyalist | 8 | 0.92 | 1.24 | 4.37 (0.81/0.07) | 14.28 (4.16/0.17) | 1.59 (0.20/0.01) | 0.83 | 0.00 |
| floater | 8 | 0.00 | 1.13 | 2.53 (0.00/0.00) | 16.14 (4.15/0.27) | 0.00 (0.00/0.00) | 0.00 | 0.00 |
| beast | 8 | 0.24 | 1.14 | 5.21 (0.00/0.12) | 18.31 (3.81/0.32) | 0.98 (0.01/0.03) | 0.00 | 0.00 |
| novice | 8 | 0.37 | 1.15 | 2.68 (0.74/0.18) | 15.06 (4.53/0.32) | 2.93 (1.22/0.75) | 0.17 | 0.00 |
| exploit | 8 | 0.15 | 1.20 | 1.11 (0.18/0.27) | 15.39 (4.39/0.19) | 2.36 (1.06/1.04) | 0.00 | 0.00 |
| oracle-reader | 8 | 0.59 | 1.11 | 5.88 (2.21/0.75) | 13.62 (3.84/0.17) | 0.47 (0.01/0.01) | 0.00 | 0.00 |
| oracle-skilled | 8 | 0.21 | 1.29 | 1.42 (0.28/0.37) | 15.79 (4.34/0.16) | 1.21 (0.02/0.18) | 0.01 | 0.00 |
| passive | 12 | 0.00 | 2.78 | 0.41 (0.00/0.00) | 59.15 (13.40/0.70) | 0.00 (0.00/0.00) | 0.00 | 0.71 |
| random | 12 | 0.10 | 2.69 | 4.05 (0.57/0.33) | 55.27 (12.49/0.69) | 1.02 (0.35/0.29) | 0.02 | 1.83 |
| social | 12 | 0.68 | 2.70 | 5.69 (0.99/0.70) | 54.20 (12.24/0.81) | 4.70 (1.42/1.22) | 0.40 | 1.84 |
| reader | 12 | 1.13 | 2.43 | 8.73 (3.96/2.00) | 49.25 (10.63/0.77) | 1.61 (0.03/0.05) | 0.00 | 2.14 |
| schemer | 12 | 1.13 | 2.49 | 12.23 (5.28/2.44) | 46.71 (9.81/0.56) | 4.80 (1.38/1.08) | 0.00 | 2.46 |
| loyalist | 12 | 1.25 | 2.76 | 4.96 (1.23/0.14) | 54.04 (11.48/0.79) | 3.40 (0.29/0.03) | 0.86 | 1.91 |
| floater | 12 | 0.00 | 2.59 | 3.32 (0.00/0.00) | 57.63 (12.30/0.87) | 0.00 (0.00/0.00) | 0.00 | 2.07 |
| beast | 12 | 0.28 | 2.60 | 7.85 (0.07/0.29) | 56.85 (10.76/0.72) | 1.38 (0.00/0.05) | 0.00 | 3.39 |
| novice | 12 | 0.68 | 2.67 | 5.99 (1.70/0.31) | 54.98 (11.99/0.81) | 4.40 (1.36/0.83) | 0.44 | 2.01 |
| exploit | 12 | 0.25 | 2.77 | 1.29 (0.23/0.24) | 56.22 (12.47/0.64) | 2.28 (0.79/0.90) | 0.07 | 1.14 |
| oracle-reader | 12 | 0.70 | 2.62 | 8.82 (2.86/1.24) | 53.96 (11.91/0.65) | 0.87 (0.00/0.01) | 0.00 | 2.14 |
| oracle-skilled | 12 | 0.38 | 2.82 | 3.15 (0.73/0.59) | 55.70 (12.89/0.62) | 1.89 (0.03/0.16) | 0.02 | 1.12 |

### Economy per season: window seats, bought time, Have-Nots, free actions

| policy | size | seats offered | spent | wasted | purchases | goodwill paid | Have-Not weeks | free actions |
|---|---|---|---|---|---|---|---|---|
| passive | 8 | 21.3 | 0.0 | 21.3 | 0.00 | 0.0 | 0.18 | 0.0 |
| random | 8 | 26.8 | 16.2 | 10.6 | 0.00 | 0.0 | 0.16 | 13.4 |
| social | 8 | 28.9 | 28.9 | 0.0 | 0.00 | 0.0 | 0.22 | 15.3 |
| reader | 8 | 27.5 | 14.5 | 13.0 | 0.00 | 0.0 | 0.25 | 30.0 |
| schemer | 8 | 30.5 | 30.5 | 0.0 | 3.69 | 29.5 | 0.14 | 17.2 |
| loyalist | 8 | 23.2 | 23.2 | 0.0 | 0.00 | 0.0 | 0.16 | 14.5 |
| floater | 8 | 29.0 | 29.0 | 0.0 | 0.00 | 0.0 | 0.22 | 13.2 |
| beast | 8 | 34.0 | 34.0 | 0.0 | 0.00 | 0.0 | 0.00 | 7.0 |
| novice | 8 | 24.3 | 12.4 | 11.9 | 0.00 | 0.0 | 0.26 | 13.2 |
| exploit | 8 | 38.5 | 38.5 | 0.0 | 19.98 | 159.8 | 0.12 | 59.7 |
| oracle-reader | 8 | 27.2 | 14.4 | 12.8 | 0.00 | 0.0 | 0.18 | 21.5 |
| oracle-skilled | 8 | 22.5 | 7.0 | 15.5 | 0.00 | 0.0 | 0.15 | 0.8 |
| passive | 12 | 29.9 | 0.0 | 29.9 | 0.00 | 0.0 | 0.06 | 0.0 |
| random | 12 | 39.5 | 23.1 | 16.4 | 0.00 | 0.0 | 0.09 | 21.7 |
| social | 12 | 40.8 | 40.8 | 0.0 | 0.00 | 0.0 | 0.07 | 25.1 |
| reader | 12 | 47.6 | 25.1 | 22.6 | 0.00 | 0.0 | 0.13 | 83.9 |
| schemer | 12 | 51.0 | 51.0 | 0.0 | 6.01 | 48.1 | 0.09 | 31.1 |
| loyalist | 12 | 42.3 | 42.3 | 0.0 | 0.00 | 0.0 | 0.08 | 24.5 |
| floater | 12 | 46.3 | 46.3 | 0.0 | 0.00 | 0.0 | 0.13 | 22.7 |
| beast | 12 | 49.7 | 49.7 | 0.0 | 0.00 | 0.0 | 0.00 | 11.0 |
| novice | 12 | 43.5 | 22.0 | 21.5 | 0.00 | 0.0 | 0.31 | 23.3 |
| exploit | 12 | 56.9 | 56.9 | 0.0 | 28.74 | 229.9 | 0.04 | 116.4 |
| oracle-reader | 12 | 42.5 | 21.1 | 21.4 | 0.00 | 0.0 | 0.04 | 63.3 |
| oracle-skilled | 12 | 34.9 | 11.0 | 24.0 | 0.00 | 0.0 | 0.07 | 2.0 |

Seats offered depend on how long the player stays and on what they buy, so they differ by policy.

### Game Sense by policy (bootstrap 95% for the mean)

| policy | size | Game Sense | competitions | strategy | social |
|---|---|---|---|---|---|
| passive | 8 | 56.2 [54.2, 58.0] | 81.3 | 52.8 | 35.4 |
| random | 8 | 63.8 [61.7, 65.8] | 89.3 | 65.8 | 35.5 |
| social | 8 | 69.6 [67.4, 71.8] | 91.5 | 74.9 | 40.5 |
| reader | 8 | 71.3 [68.9, 73.5] | 88.7 | 83.5 | 37.8 |
| schemer | 8 | 65.8 [63.8, 67.8] | 91.8 | 76.6 | 25.2 |
| loyalist | 8 | 67.7 [64.7, 70.6] | 82.4 | 72.4 | 46.8 |
| floater | 8 | 64.9 [63.5, 66.2] | 94.0 | 62.4 | 39.2 |
| beast | 8 | 73.6 [72.6, 74.5] | 99.5 | 80.1 | 38.9 |
| novice | 8 | 65.6 [62.8, 68.1] | 84.7 | 70.3 | 40.6 |
| exploit | 8 | 63.7 [60.9, 66.4] | 82.5 | 71.5 | 34.7 |
| oracle-reader | 8 | 72.0 [69.3, 74.4] | 90.3 | 83.7 | 37.9 |
| oracle-skilled | 8 | 62.0 [59.2, 64.5] | 83.2 | 65.2 | 36.7 |
| passive | 12 | 60.3 [59.1, 61.5] | 90.1 | 60.2 | 30.5 |
| random | 12 | 66.9 [65.5, 68.4] | 92.3 | 75.7 | 29.7 |
| social | 12 | 73.2 [71.5, 74.7] | 93.6 | 85.7 | 36.0 |
| reader | 12 | 76.4 [75.5, 77.3] | 97.3 | 97.1 | 28.1 |
| schemer | 12 | 71.1 [69.8, 72.3] | 96.1 | 92.6 | 17.1 |
| loyalist | 12 | 77.3 [75.8, 78.6] | 94.6 | 92.6 | 39.6 |
| floater | 12 | 66.9 [65.8, 68.0] | 97.5 | 71.1 | 30.8 |
| beast | 12 | 73.7 [72.8, 74.5] | 100.0 | 87.0 | 29.5 |
| novice | 12 | 72.5 [70.7, 74.3] | 91.4 | 86.6 | 34.8 |
| exploit | 12 | 68.6 [66.9, 70.4] | 89.7 | 82.2 | 29.2 |
| oracle-reader | 12 | 73.9 [72.6, 75.3] | 94.3 | 92.2 | 29.3 |
| oracle-skilled | 12 | 70.1 [68.4, 71.6] | 92.3 | 80.6 | 33.7 |

Size 8: gated reader minus random 7.6; oracle reader minus random 8.2 (target 20).

Size 12: gated reader minus random 9.5; oracle reader minus random 7.0 (target 20).

### Who goes out, NPC Heads of Household, and the jury (all policies' seasons)

| size | evictee's threat rank (HoH's view) | evictees in a pact | evictee's competition wins | NPC HoH nominated the top threat | jury margin | bitter jurors |
|---|---|---|---|---|---|---|
| 8 | 3.18 | 17.0% | 0.71 | 57.9% | 2.29 | 55.6% of jurors whose evictor reached the final two |
| 12 | 3.76 | 27.8% | 1.10 | 46.3% | 3.59 | 52.8% of jurors whose evictor reached the final two |

### The house: agendas and pairs (all policies' seasons)

| house | NPC agendas as each social week closed | warmest pair (mutual, mean / max) | coldest pair (mutual, mean / min) |
|---|---|---|---|
| 8 | build 77.2%, hunt 15.3%, hold 6.7%, drift 0.8% | 154 / 200 | -84 / -200 |
| 12 | build 68.6%, hunt 25.3%, hold 5.6%, drift 0.5% | 191 / 200 | -128 / -200 |

### The story's pace (all policies' seasons), counted as StoryPacingTests.PacingReport counts it

Asks are the beats put to the player, a storyline's beats closing at one anchor one ask; the first night, summons and plays apart; budgeted asks leave out production's must-fires and urgent moments. A pariah season has some houseguest (not the reigning Head of Household) with three in the house at forty against them after two Advances running. The last column is the autopsy's own count, every story house event in the final state, which is not comparable with the plan's targets.

| house | budgeted asks / season (target 4-6) | + must-fires | summons | play offers | weeks with a card (target 30-60%) | stories finished (target about 3) | moments | seasons with a pariah (target at most 20%) | NPC showmances | seasons with an NPC removal | seasons with a pile-on (target at most 25%) | story house events / season (autopsy) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 8 | 4.8 (max 9) | 0.2 | 0.5 | 3.2 | 50.3% | 4.5 | 8.2 | 14.3% | 1.02 | 0.0% | 0.2% | 12.7 |
| 12 | 8.3 (max 17) | 0.2 | 0.8 | 5.2 | 49.8% | 7.6 | 12.0 | 44.8% | 1.16 | 0.0% | 0.6% | 20.6 |

### Pacing: decisions and ceremony seconds a week

Decisions are the player's accepted commands other than Advance. Ceremony seconds are the key ceremony, veto meeting and live eviction at the suspenseful pace, fade to fade, unskipped (CeremonyPacing); the finale's jury reveal apart.

| policy | size | decisions / week (weeks in the house) | ceremony s / regular week | finale reveal s |
|---|---|---|---|---|
| passive | 8 | 3.9 | 44.8 | 21.8 |
| random | 8 | 14.3 | 44.6 | 21.8 |
| social | 8 | 17.4 | 44.2 | 21.8 |
| reader | 8 | 19.8 | 44.3 | 21.8 |
| schemer | 8 | 19.0 | 44.4 | 21.8 |
| loyalist | 8 | 18.3 | 44.5 | 21.8 |
| floater | 8 | 16.4 | 44.4 | 21.8 |
| beast | 8 | 15.6 | 42.9 | 21.8 |
| novice | 8 | 14.5 | 44.4 | 21.8 |
| exploit | 8 | 37.7 | 44.5 | 21.8 |
| oracle-reader | 8 | 16.4 | 44.9 | 21.8 |
| oracle-skilled | 8 | 9.4 | 44.9 | 21.8 |
| passive | 12 | 3.8 | 50.5 | 24.6 |
| random | 12 | 15.5 | 50.2 | 24.6 |
| social | 12 | 19.4 | 50.1 | 24.6 |
| reader | 12 | 25.5 | 50.0 | 24.6 |
| schemer | 12 | 20.5 | 49.9 | 24.6 |
| loyalist | 12 | 19.3 | 49.9 | 24.6 |
| floater | 12 | 17.3 | 50.0 | 24.6 |
| beast | 12 | 16.8 | 48.9 | 24.6 |
| novice | 12 | 15.2 | 50.1 | 24.6 |
| exploit | 12 | 46.6 | 50.0 | 24.6 |
| oracle-reader | 12 | 23.3 | 50.4 | 24.6 |
| oracle-skilled | 12 | 10.1 | 50.4 | 24.6 |

### Refusals and the walker

| policy | size | own commands / season | walker steps / season | refusals / season | most common refusal |
|---|---|---|---|---|---|
| passive | 8 | 0.0 | 91.5 | 0.00 | - |
| random | 8 | 47.9 | 87.8 | 0.00 | - |
| social | 8 | 67.2 | 88.3 | 0.00 | - |
| reader | 8 | 66.6 | 87.8 | 0.00 | - |
| schemer | 8 | 71.7 | 87.6 | 0.00 | - |
| loyalist | 8 | 55.5 | 87.5 | 0.01 | PromiseFinalTwo: This promise is already active. (1) |
| floater | 8 | 62.7 | 87.6 | 0.00 | - |
| beast | 8 | 69.4 | 89.1 | 0.00 | - |
| novice | 8 | 44.5 | 88.0 | 0.00 | - |
| exploit | 8 | 121.6 | 87.6 | 0.24 | RenameAlliance: The Outsiders has had its new name this week. (13) |
| oracle-reader | 8 | 52.3 | 92.1 | 0.00 | - |
| oracle-skilled | 8 | 110.3 | 2.0 | 1.95 | AnswerJury: Choose one of the responses offered. (192) |
| passive | 12 | 0.0 | 148.9 | 0.00 | - |
| random | 12 | 70.4 | 145.8 | 0.00 | - |
| social | 12 | 98.1 | 145.9 | 0.00 | - |
| reader | 12 | 146.0 | 146.7 | 0.00 | - |
| schemer | 12 | 119.3 | 147.0 | 0.00 | - |
| loyalist | 12 | 100.5 | 146.8 | 0.45 | PromiseFinalTwo: This promise is already active. (45) |
| floater | 12 | 98.7 | 146.5 | 0.00 | - |
| beast | 12 | 100.7 | 147.7 | 0.00 | - |
| novice | 12 | 77.2 | 147.2 | 0.00 | - |
| exploit | 12 | 204.6 | 144.8 | 0.63 | RenameAlliance: The Outsiders has had its new name this week. (26) |
| oracle-reader | 12 | 109.6 | 152.0 | 0.00 | - |
| oracle-skilled | 12 | 182.0 | 1.9 | 1.88 | AnswerJury: Choose one of the responses offered. (180) |

Seasons with an error (walker refused, invalid state, unfinished): 0 of 2400. The oracle skilled player's
refusals are its own walker answering the finale's questions with the old catalogue's "A"; the lab's walker then
answers with an offered response.

## What this baseline does not cover

- **The NPC world (B5).** No NPC-to-NPC conversation, no all-week beats, no walk-ins; the plan's sensitivity
  table against the tick budget comes with the driver.
- **Seed counts (B6).** 100 a cell; the plan's 800 is about 2.3 hours of the headline grid on this machine.
  Rows and goldens per cell are not recorded yet.
- **Human performance (B8).** The per-policy distributions are assumptions; the fixed-level tier bounds them.
- **Sizes and rosters.** The tables cover 8 and 12, regular roster only.
  - The smoke tier proves every policy plays a legal season at 4, 6, 8, 10 and 12, and in the All-Stars
    eight and twelve.
  - The full tier measures them, but has not been run.
  - 13-16 are verification-only houses.
- **Policy strength.** Each policy is one plausible script of its style, not an optimum, and comparisons are
  between these scripts on the same seasons. The oracles in particular are not strong bounds (F2), and the
  skilled oracle still takes walk-ins that the view does not offer the gated players.

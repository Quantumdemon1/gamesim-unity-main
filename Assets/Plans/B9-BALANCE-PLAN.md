# B9: the balance bands (tuning rules 1)

Design note B9-0, 2026-11-03. Docs only: it changes no code, save, test or rule. It does five things:
- sets B9's bands;
- names the levers, and the settings to measure for each;
- says how B9 is versioned, so that every existing season plays exactly as it does today;
- says how B9 is measured and accepted;
- says what B9 hands on.

**The owner's words, 2026-11-03.** Verbatim: "Set the B9 balance bands, so role moments, the dialogue stack and the All-Star work can follow." The lead set the bands in §1 as a delegated call. That call rests on those words and on the owner's standing "Yes your judgment on questions and proceed". Every band comes from a target the documents already state; none is new. The owner signs off B9's final numbers in its PR, before it merges.

**Evidence base.** Branch `claude/b9-balance`, cut from main `26b65f09` (PR #44, dialogue D2). Two measurements match today's code:
- **The competition tier**, re-run in full at `26b65f09`: 200 seeds, 1,600 seasons. The merged rows' SHA-256 begins `92c74b608cc446a0`. Count for count, it is Codex's retained Source19 baseline (REMAINING_WORK:65-69).
- **The change-paths lane's 100-seed headline**, archived as `lanes/arcs2-lab/after-headline.md`. Today's code still plays it:
  - `BalanceLabTests.BudgetNoughtRows` still hashes to `5481eec784acd1ed`.
  - `ShippedRules.cs` has not changed since `1f199651`.
  - 20 reader and random seasons replayed at `26b65f09` match the archived rows on every field compared.

BALANCE_BASELINE.md's 480-seed tables are older. They were measured at `a91462b4` (2026-10-09), before vote family V6 and before the change paths, and the change paths alone changed 2,321 of their 2,400 paired rows. Below they are quoted as history, marked "at a91462b4", and never as today.

Paths are relative to `Assets/Gamesim/`, except those that start at the repository root. "The ROLE doc" is `ROLE_DIALOGUE_ECONOMY_20261012.md`. "REMAINING_WORK" is `REMAINING_WORK_20261012.md`.

**Names.**
- *Tuning rules 0* is every season today. *Tuning rules 1* (`TuningRules.Bands`) is B9.
- *The studier* is the lab's passive player that studies the house until its preparation is full (`Tests/EditMode/BalanceLabPolicies.cs:299-310`). It plays at a fixed performance of 0.5, with an NPC budget of 0.
- *Weekly wins* count the Head of Household and veto competitions the player was in the field for: won, out of played. The final Head of Household's three parts are not counted (`BalanceLabDiagnostics.Preparation`).
- *The gap* is the knowledge-gated reader's Game Sense minus the random player's, on the same seasons. Its interval is a paired bootstrap 95% (`BalanceLabDiagnostics.GameSenseWhatIf`).
- *Carried* means the war room settled on the player's counter. The rate is counters carried over every accepted Countered transaction (REMAINING_WORK:61).

---

## 0. The short version

- **The bands (§1).**
  - The studier wins 60-70% of weekly competitions, at 8 and at 12.
  - The studier wins 25-35% of seasons at 8 and 18-28% at 12.
  - The Game Sense gap is at least 10, with its low end above 5, at both sizes.
  - 35-50% of counters carry.
  - Today: about 95% of weekly competitions; 73.5% and 48% of seasons; a gap of 8.7 and 8.6; and almost every counter carries.
- **The levers (§3).**
  - For the competitions: preparation is spent when it is used, and capped at 3 (PC5's candidate).
  - For the gap: the Game Sense weights, 0.2 / 0.6 / 0.2.
  - For the counter: the war room's tie rule, and only if a count on today's seasons shows that it lands inside the band.
  - Q4 is reported, not tuned. Q5 belongs to story rules 10.
- **Versions (§4).**
  - Everything B9 changes is keyed to `tuningRulesVersion` 1. That field is already saved in schema 30, and is 0 today.
  - `ShippedRules.ApplyFresh` writes 1, so every new season starts on B9.
  - Every save, migration, import and test-built season stays at 0, and plays exactly as today.
  - No schema bump, and no competition rules bump.
- **Measurement (§5).**
  - Six candidate sets are screened in a scratch copy at 200 seeds.
  - The chosen set is accepted at 800 seeds for the competitions and 480 for the war rooms.
  - About 10-13 hours of one core in all.
- **Hand-off (§7).** R7 enrols role moments on B9's seasons and reruns B9's cohorts. D6, H1d and plan 32's S9 pair against B9's tables.

## 1. The bands

| # | Band | Today (`26b65f09`, Wilson 95%) | Source |
|---|---|---|---|
| B1 | The studier wins **60-70%** of weekly competitions with **8** in the house | 1,339 of 1,406 = 95.2% [94.0, 96.2] | The owner's band |
| B2 | The same with **12** | 1,651 of 1,743 = 94.7% [93.6, 95.7] | REMAINING_WORK:62 ("for each cast size"); the ROLE doc :156 |
| B3 | The studier wins **25-35%** of seasons of **8** | 147 of 200 = 73.5% [67.0, 79.1] | The owner's band |
| B4 | The studier wins **18-28%** of seasons of **12** | 96 of 200 = 48.0% [41.2, 54.9] | REMAINING_WORK:63; the ROLE doc :156; UNITY_PORT_ROADMAP.md:1094 |
| B5 | The gap is **at least 10, with its low end above 5**, at **8** | 8.7 [6.3, 11.0] on 100 seeds (9.6 [8.3, 10.8] on 480 at a91462b4) | The owner's band (BALANCE_BASELINE, Q2) |
| B6 | The same at **12** | 8.6 [6.5, 10.7] on 100 seeds (8.5 [7.6, 9.2] on 480 at a91462b4) | The owner's band |
| B7 | **35-50%** of accepted Countered transactions carry | Not measured on 480 seeds at today's code (see below) | REMAINING_WORK:61 |

B7 has two older readings. On the headline's 480 seeds at a91462b4, 35 of 52 carried: 67.3% [53.8, 78.5]. In Codex's retained 100-seed war-room population, 14 of 14 carried (REMAINING_WORK:69).

- **The owner's four.** B1, B3, B5 and B6 are the targets the owner's band call names:
  - a studying player wins 60-70% of weekly competitions (95% today);
  - that player wins 25-35% of 8-player seasons (73.5% today);
  - the gap is at least 10, with its low end above 5, at both sizes.
- **The plans' three.** The same documents state B2, B4 and B7 for the same questions, as targets "selected by the owner or delegated judgment" (REMAINING_WORK:59). The lead's rule for this call makes any tuning question with a target stated in the plans a band, so these are bands too (decision 1).
- **B7's population (decision 3).**
  - Accepted attempts that reach no member stay in the denominator. Unanswered and lapsed proposals do not.
  - BALANCE_BASELINE reads Q1 another way: mean come-round odds against 0.35-0.50, marked "Done (decision 6)". That reading stays a diagnostic.
  - Member conversions, lapses and subgroups are diagnostics too. Each is reported separately, and none replaces the band's measure (REMAINING_WORK:71; the ROLE doc :153-156).
  - A subgroup with no attempts is unavailable, not 0%.

**Reported, not banded.**
- **Q4, the passive newcomer nominated more.**
  - The ratio of the player's nominations to a houseguest's is 1.18 at 8 and 1.08 at 12 on 100 seeds (1.08 and 1.00 on 480 at a91462b4).
  - No plan states a target, and BALANCE_BASELINE's decision 7 is no tuning.
  - B9 cannot move it for the passive player (§3.4).
- **Q5, the story pariah rate.** 16.3% at 8 and 45.1% at 12 on 100 seeds, against a target of at most 20%. It belongs to story rules 10 (§8).
- **Win rates.** The beast (87.0% and 56.0% of seasons on 100 seeds today), the studier at 0.8, the passive player, and every headline policy.
- **The proposed B-1 band.** A skilled policy wins at least 1.5 times the base rate (BALANCE_BASELINE.md:753).
- **The 45-minute season at 8.** The lab reads about 51 minutes.
- **The oracle reader's gap.** The Game Sense report still prints "target 20" for it. That target is stale against B5-B6, and the build corrects the text.

## 2. Decisions

Each row is the default adopted under the delegated call. Each stays open to the owner, and the last column says what changing it costs.

| # | Decision | Default adopted | If the owner changes it |
|---|---|---|---|
| 1 | The bands | B1-B7 as in §1, including the three plan-stated bands. | Without B2 and B4 the 12-house is unbounded: settings that meet the bands at 8 can leave 12 anywhere. Without B7, Q1 stays BALANCE_BASELINE's "Done" and is only reported. |
| 2 | The version | **Tuning rules 1 for everything.** Competition rules stay at 4 (`Widened`), and B9 does not take competition rules 5 (§4.6). | Preparation goes under competition rules 5, as DIALOGUE-PLAN decision 13, ALL-STAR-HISTORIES-PLAN §9 and BALANCE_BASELINE's Q3 row planned. That costs three things. First, `SeasonBuilder.cs:181` writes `CompetitionRules.Current` into every built season (166 test files call it), so moving `Current` re-rules every test-built season unless SeasonBuilder is pinned to `Widened`. Second, about 30 test sites write `Current`, and the frozen readers refuse 5 there. Third, `CompetitionMiniGames.CurrentRules` moves with it, so every new season's minigame boards re-roll (the attempt seed hashes the rules version). None of this serves the lever: preparation is capped by the study and spent at the Advance, not by the scoring. |
| 3 | Q1's population | Counters carried over accepted Countered transactions (REMAINING_WORK:61). | BALANCE_BASELINE's mean come-round odds. No reach constants bring those inside 0.35-0.50. |
| 4 | Q1's evidence | B7 is judged on its point estimate, with its interval quoted. The lever is the shape of the settlement. A new shape is played only if the count on today's seasons (§3.3) puts it inside the band. | An interval inside 35-50 needs about 170 counters. That is about 1,500 war-room seeds: est. 8 hours of one core for each run. |
| 5 | When preparation is spent | At the Advance out of a weekly Head of Household or veto the player entered (played, assisted or simulated). It is kept when the player throws (a throw gives up every bonus) or watches. The final Head of Household's three parts all count it, because no competition follows them. | If it is spent at the end of each final part, parts 2 and 3 get nothing. If it is spent when the competition resolves, inside `ResolveCompetition`, the result card's "Your preparation counted." (`Runtime/Presentation/CompetitionWords.cs:79`) reads 0, and the card and the explanations need rework. |
| 6 | A study at the cap | Refused under tuning 1, with no action spent. Today a study at the cap spends the action for nothing, and a sneak-peek there can only lose. | Keep today's behaviour, and the player can waste a seat. The lab's policies never study at the cap either way, so the bands do not move. |
| 7 | The Game Sense weights | **0.2 / 0.6 / 0.2** at tuning 1, BALANCE_BASELINE's candidate. 0.25 / 0.5 / 0.25 is reported beside it. | Take the smaller change, if it meets B5-B6 at the acceptance run. Either way, every tuning-1 season's verdict reads differently. |
| 8 | The lab's controls and pins | A `tuningHeld` cell field holds a fresh season at 0. On held cells, `BudgetNoughtRows` keeps `5481eec784acd1ed`, and it gains a tuning-1 twin. `AllWeekSeasonDigests` keep their records on held seasons and gain twins. `BalanceLabGoldens` (the 48 cells and the `Rules` tuple) are re-recorded at tuning 1, with the reason. The parts stamp moves to `balance-lab-parts/v8`. | Re-recording everything in place drops the only pins on what old seasons play. |
| 9 | Acceptance | As in §6: for B1-B2 the interval lies inside the band; for B3-B4 and B7 the estimate does; B5-B6 use their own rule. If no set meets them all, the PR shows the nearest, and the owner decides. B9 widens no band itself. | - |
| 10 | Q4, and the reach history | Q4 is not tuned (BALANCE_BASELINE decision 7). The war rooms' reach constants stay as they are. They move behind tuning, as their own note says (PactPlans.cs:44-49), only if the owner confirms a season was saved between `3f23e76b` and the reach amendment. | - |

## 3. The levers

### 3.1 Preparation (Q3: B1-B4)

**Today.**
- `StudyHouse` (`Simulation/EpisodePreparation.cs:7-29`) is a free-time action only. It costs one window seat and draws one roll.
- Memorize always gives +1. Sneak-peek gives +2 on a 45% chance, or -1 (the native cast passes no source traits, so the Strategic bonus never applies).
- `WebStudyHouse.ApplyBonus` holds the total to 0-5 (`Simulation/WebStudyHouse.cs:56-57`), and validation holds it there (`Simulation/EpisodeValidation.cs:175`).
- It is never spent. From competition rules 3, the whole of it is added, one point for one point, to every competition the player enters, on every entry route (`CommonCompetitionBonus`, `Simulation/EpisodeEngine.cs:627-629`).
- So the studier reaches 5 within three free times and keeps it all season. That is how it wins 95% of weekly competitions.
- Free time has max(2, ceil(n/2) - 3) seats (`Simulation/EpisodeEngine.Week.cs:68-79`): 2 with 8 to 10 in the house, and 3 with 11 or 12.

**PC5's candidate: spent when used, capped at 3.**
- With the spend, the seats do the work. The studier comes to each Head of Household with +2 (+3 with 11 or 12 in the house), and to the veto with nothing, unless it sat the Head of Household out.
- The cap binds only when preparation carries over (an outgoing Head of Household) or after a lucky sneak-peek.
- A rough reading, not a measurement. B4's fixed-performance curve (BALANCE_BASELINE.md:889-916) suggests about 80-88% at the Head of Household and about 50% at the veto: roughly 65-69% of weekly competitions.
- At a91462b4 the passive player at a fixed 0.75 won 65.9% (8) and 61.6% (12) of weekly competitions, and 26.0% and 19.5% of seasons. Those season rates are inside B3 and B4, so the bands look consistent with each other.

**The settings to try.** These are sets A to D in §5.2.

| Set | Cap | When used | Expected direction |
|---|---|---|---|
| A | 3 | spent to 0 | PC5 as stated |
| B | 2 | spent to 0 | tighter at 12, and on carry-over |
| C | 1 | spent to 0 | tighter still: +1 at every Head of Household |
| D | 3 | halved, rounded down | looser: the veto keeps +1 |

The choice is the set whose screen puts B1-B4 nearest the middle of their bands, at both sizes. On a tie, take the smaller change from PC5 as stated: A, then B, D and C.

**How it is keyed.**
- `TuningRules.PreparationCap(s)` is 5 at tuning 0 and the chosen cap at 1. `WebStudyHouse.PlanStudy` and `ApplyBonus` take the cap, and the one roll is drawn exactly as today.
- The spend runs at the Advance out of `HoH` or `Veto` (`Simulation/EpisodeEngine.cs:339-366`, before `s.competitionResolved = false; Phase(s, next)`). It applies under tuning 1, when this week's ledger competition row for the phase shows `played`, `assist` or `simulated`.
- The spend draws nothing. It logs a line only when something was spent, so a tuning-1 season with no preparation plays exactly as it would at tuning 0.
- The lab's view gets `PreparationCap`. The studier (`BalanceLabPolicies.cs:307`) and the beast (`:714`) study up to it instead of up to 5; otherwise, at a cap of 3, the beast would study for ever.

### 3.2 The Game Sense weights (Q2: B5-B6)

- **Today.** The weights are 0.3 / 0.4 / 0.3 (`Simulation/GameSense.cs:21`). They are read once, at `:64`. Each face is held to 0-100.
- **What the weights touch.**
  - Game Sense is read from the ledger at the end of a season. It writes nothing and draws nothing.
  - Its readers are the finale page, the season report, the autopsy and the digest traces.
  - The weekly recap reads the faces, not the weights, so it does not move. No copy names the weights.
- **How it is keyed.**
  - At tuning 1, `Evaluate` takes its weights from `TuningRules` (it already has the state). The constants stay as tuning 0's weights.
  - The digests that hash the score (`ModeOneSeasonDigests`, `CommitmentRulesSeasonDigests`) build their seasons by hand at tuning 0, so they do not move.
- **Candidates.**
  - 0.2 / 0.6 / 0.2, the default. On today's 100 seeds it gives 13.2 [10.2, 16.2] at 8 and 12.5 [9.9, 15.3] at 12. On 480 seeds at a91462b4 it gave 13.7 [12.1, 15.2] and 12.4 [11.4, 13.4]. Both meet the band.
  - 0.25 / 0.5 / 0.25, the smaller change. Not yet measured.
  - 0 / 0.7 / 0.3 also meets the band, but it takes the competitions out of the verdict, and STRATEGY-LOOP-PLAN §5 gives the verdict three faces. It is reported only.
- **No season is played per weight set.**
  - The weights only re-read each season's notes, so every set is replayed on the same seasons (`BalanceLabDiagnostics.GameSenseWhatIf`).
  - Preparation moves neither the reader's seasons nor the random player's, because neither studies.
  - A counter shape does move them, so the gap is read on the final set's seasons.

### 3.3 The counter (Q1: B7)

- **Today.**
  - `PactPlans.Settle`'s counter branch (`Simulation/PactPlans.cs:310-321`) tallies the two sides:
    - for the counter: the player's own say, the members who came round, and the members who had already named the counter's target;
    - for the plan: its says, less the members who came round.
  - More says wins.
  - A tie goes to the founder's side (WAVE-D-NPC-PACTS-PLAN, D3-L4): to the player when `PlayerFounded`, otherwise to the NPC founder's say.
  - Members come round on keyed coins, never the season's stream. Their odds are reckoned when the player answers, and never saved.
- **Why the reach is not the lever.**
  - At a91462b4, only 9 members came round across 52 counters, so at least 26 counters carried with no conversion.
  - In Codex's 14 counters, 12 carried with no conversion (REMAINING_WORK:73).
  - Even a reach of 0 would leave at least 50% and 85.7% carrying.
- **The shapes.**
  - **K1: a tie carries the counter only when a member came round.**
    - Under tuning 1, the `PlayerFounded` branch also needs `cameRound.Count > 0`.
    - With nobody come round, the tie falls to the founder rule, which leaves the members' plan standing in the player's own pact. This relies on the player's answer not being one of the row's says; the build checks that.
  - **K2: the player's own say is not counted.**
    - The tally's leading 1 is dropped, so the counter carries only on members' says.
    - A tie still goes by the tie rule.
- **A count first, then a run.**
  - The lab already records each counter's members (`SeasonRun.counterMembers`, `Tests/EditMode/BalanceLab.cs:1005`). A test-only table records each counter's tally in the same place: the standing says, the plan, the counter, who came round and the founder.
  - From those, the table prints the share carried under today's rule, under K1, under K2 and under both, on the same pre-answer states.
  - A counter whose tally cannot be read is listed as unavailable, never guessed.
  - B9 plays a shape only if its share lands inside B7: K1 first, then K1 with K2.
  - If today's share on 480 seeds is already inside B7, no shape is played.
- **What a shape moves.**
  - It changes the seasons of the war-room players: random, reader, schemer, loyalist and floater. So B5-B6 are read again on those seasons.
  - It changes no studier or passive season, since neither convenes a war room.

### 3.4 Q4: reported, not tuned

- The nomination weight is `NominationWeight` (`Simulation/EpisodeEngine.cs:865-872`), together with `StrategyRules.NominationReluctance` (`Simulation/StrategyRules.cs:425-468`), the story's terms and the threat.
- The passive player neither studies nor convenes a war room. Its tuning-1 seasons are therefore its tuning-0 seasons, field for field, and B9 cannot move Q4 for it. The hand-off headline reports Q4 for every policy.
- If the owner ever wants Q4 tuned, it is keyed the same way, as tuning 2.

## 4. Versions: old seasons unchanged, new seasons on B9

### 4.1 The rules class

`Simulation/TuningRules.cs`, on the pattern of `AlumniRules` (`Simulation/AlumniHistories.cs:35-100`) and `DialogueRules`:
- `Off = 0`, `Bands = 1`.
- `Latest = Bands` is the validation bound only. `Shipped = Bands` is the pinned value `ApplyFresh` writes, and it is moved by hand. Neither is read for behaviour.
- `IsDefined(v)` holds from 0 to `Latest`. `At(s, v)` holds when `s.tuningRulesVersion >= v`.
- B9's numbers live in this class: `PreparationCap(s)`, what is left of preparation after use, the Game Sense weights, and the counter's shape. Every one of them reads `At(s, Bands)`.
- **Freezing.** Tuning 1 freezes the first time `ApplyFresh` writes it. A later retune is tuning 2, keyed `At(s, 2)`, and tuning-1 seasons keep B9's numbers.

### 4.2 Validation

- `Simulation/EpisodeValidation.cs:108` refuses every tuning value but 0. In the slice that defines 1, the check becomes `!TuningRules.IsDefined(...)`. The message stays the same, so the refusals of other values keep their text.
- Tuning 1 needs competition rules 4. No path writes any other combination, and a season may not hold a value no code reads.
- At tuning 1, preparation must be within the cap: 0 to 3 under set A.

### 4.3 How new seasons enrol

- `ShippedRules.ApplyFresh` writes `fresh.tuningRulesVersion = TuningRules.Shipped;` as a plain int, beside the competition rules line (`Simulation/ShippedRules.cs:56`). It draws nothing, logs nothing and mints no id.
- Its only shipped caller is the director's `StartSeason` (`Runtime/Episode/EpisodeDirector.Season.cs:326`). So every new season starts on tuning 1: the Regular roster and the All-Stars, with the cast screen's chip or without it.
- `ShippedRules.Fields` already lists the field (`Simulation/ShippedRules.cs:139`).

### 4.4 What stays at 0, and plays as today

- Every save on disk.
- Every migrated save. `UpgradeV29ToV30` writes a literal 0 (`Runtime/Persistence/EpisodeSaveMigrations.cs:39-48`), and the frozen readers know no schema-30 field.
- Load, recovery and the web importer never call `ApplyFresh`, and never infer the field.
- `SeasonBuilder.Create` never writes the field, so every test-built season stays at 0.
- The old-save fixtures replay their recorded commands from the loaded state (`Tests/EditMode/OldSaveAcceptanceTests.cs:297`). They read 0 from the save and replay to their digests.
- OLD-SAVE-ACCEPTANCE.md names `s28-` and `s29-mode2-allweek-w3-campaign` as the canaries for a retune without a version.

### 4.5 No schema bump

B9 changes only how existing fields are used: `playerStudyBonus`, the ledger's competition rows and the war-room rows, plus code. It saves nothing that is not saved today. If any lever turns out to need something new on disk, B9 stops and reports, because that is schema 31. One example would be recording, on `CompetitionRow`, the preparation each competition used.

### 4.6 Competition rules

- They stay at 4 (decision 2).
- Competition rules 5 was claimed for PC5 (DIALOGUE-PLAN decision 13 and §9). PC5's preparation lands as tuning 1 instead, so B9 releases 5.
- The next slice that changes how a competition is scored takes 5: Will's throw in H1d or PC6's minigames, whichever lands first.
- DIALOGUE-PLAN §9 and ALL-STAR-HISTORIES-PLAN §9 are updated when B9 lands.

### 4.7 What moves with tuning 1 (the build's list)

- **Pins that say 1 is refused.** They move to the first undefined value:
  - `PooledSchema30ShapeTests.cs:76, 80`;
  - `AlumniHistoriesTests.cs:523`;
  - `LegacyDigestSchema30ObserverTests.cs:51-52, 124, 161, 166`, and the observer itself (`LegacyDigestSchema30Observer.cs:75, 132`);
  - `PersistenceMigrationTests.cs:305, 423, 469`;
  - `PersistenceV30MigrationTests.cs:142`.
- **Pins that say "off until B9":** `ShippedRulesTests.cs:69, 191`, and `BalanceLabGoldens.Rules` (`:41`).
- **Fresh-state digests.** `FreshSeasonStateDigestTests` strips schema 30 and refuses a nonzero tuning. It gains a hold for the tuning field, as it already has one for the histories. It stays exact, because tuning 1 writes only the int.
- **Re-recorded with the reason, or twinned (decision 8):** the goldens, `BudgetNoughtRows`, `AllWeekSeasonDigests` and the parts stamp. The held cell is named `/tuning0` only when it is set, so every earlier cell keeps its name and its seed.
- **Copy, keyed to tuning 1.** A tuning-0 season keeps every string, byte for byte. The known readers:
  - `Simulation/EpisodePreparation.cs:28` ("Preparation: n/5");
  - `Runtime/Episode/EpisodeDirector.DiaryRoom.cs:262, 511, 613, 627`;
  - `EpisodeDirector.Challenge.cs:233`, with `Runtime/Presentation/CompetitionWords.cs:79, 98, 119` ("carries over");
  - `EpisodeDirector.FreeTimeScreen.cs:160`;
  - `EpisodeDirector.FreeTimeBoard.cs:374`;
  - `EpisodeDirector.CampaignScreen.cs:182`.

  The build greps for every reader of `playerStudyBonus` before it starts. New strings are new strings; nothing is appended to an existing caption.
- **The standalone verify** expects 5 legal memorizes (`Runtime/Episode/PortVerification.Study.cs:68, 101, 110, 156, 222`). It reads the cap instead.
- **PlayMode tests on director seasons:** `EpisodeStudyPlayModeTests.cs:163`, `EpisodePlayModeTests.DiaryRecord.cs:68` and `EpisodePlayModeTests.FreeTimeMetrics11.cs:480`. They are in the lead's UMA PlayMode set; the subset never runs them.
- **EditMode-only pins**, such as the refusal messages and the study's new refusal at the cap. They run under Unity's Mono harness, which the subset does not cover.
- **Old-save acceptance.** A tuning-1 cell, `s30-mode2-allweek-tuning1-w3-campaign`, written with Tools/OldSaves' `generate` by the first build that writes tuning 1 (OLD-SAVE-ACCEPTANCE.md: one fixture per save version and rules combination).

## 5. Measurement

### 5.1 The grid

- **Seeds** are `HashSeed("balance-lab/v1/<roster>/<size>/npc0/<index>")`.
  - They do not depend on the policy, the performance, the budget or a held field, so every arm plays the same seasons and the arms pair.
  - Season wins are paired with McNemar's test, and the gap with a paired bootstrap.
- **The competition tier** (`BalanceLabReports.cs:57-58`):
  - 8 cells: the passive player and the studier, at fixed 0.5 and 0.8, at 8 and 12.
  - Its table is `BalanceLabDiagnostics.Preparation`.
  - It gives B1-B4.
- **The war-room tier** (`BalanceLabReports.cs:51-52`):
  - Random, reader, schemer, loyalist and floater, at 8 and 12, at headline performance and budget 0: the headline's seasons for those cells.
  - Its tables today are `WarRooms` and `CounterWhatIf`. For B9 it gains `Sense`, `GameSenseWhatIf` (with 0.25 / 0.5 / 0.25 added) and the counter's tally table (§3.3).
  - It gives B5-B7.
- **The headline tier:** all 12 policies at 8 and 12. B9 runs it once, at 100 seeds, for the hand-off (§7).

### 5.2 The candidate sets

**How the scratch copy works.**
- The measurement runs in a scratch copy, a `git archive` snapshot on D:, never in the working tree, and always through `cpu-lock2.sh`.
- One build serves every set. The scratch copy's `TuningRules` reads its numbers from environment variables, once per process, so each set is a run with `--no-build`.
- The committed build has constants only.
- In every set `ApplyFresh` writes 1. In sets A-F the Game Sense weights are 0.2 / 0.6 / 0.2; set 0 keeps today's.

| Set | Preparation | Counter | Tier | Seeds |
|---|---|---|---|---|
| 0 (the control, not a candidate) | cap 5, never spent | today | competition | 0-199 |
| A | cap 3, spent to 0 | today | competition | 0-199 |
| B | cap 2, spent to 0 | today | competition | 0-199 |
| C | cap 1, spent to 0 | today | competition | 0-199 |
| D | cap 3, halved | today | competition | 0-199 |
| E | the chosen one of A-D | K1 | war rooms | 0-479 |
| F | the chosen one of A-D | K1 and K2 | war rooms | 0-479 |

- **Set 0** must replay today's competition tier exactly. Its rows must equal the reference rows (`92c74b608cc446a0`) on every field `compare_rows.py` compares. That proves the keying leaks nothing.
- **Sets A to D** are screened in turn, at 23 minutes each. A goes first, then whichever direction A points.
- **The war rooms are played once, at the chosen preparation, on 480 seeds, before E or F.**
  - Preparation does not move those policies, so this run is today's war rooms.
  - It gives the first 480-seed gap at today's code, Q1 today, and the tally table that decides whether E or F is played at all.
- **E or F** is played only if the tally table puts it inside B7.

### 5.3 Acceptance runs

- **The competition tier at 800 seeds** (0-799), under the chosen set. Seeds 0-199 pair with the retained untuned population.
- **The war-room tier at 480 seeds** (0-479), under the chosen set. This is the run from §5.2 if no counter shape was chosen, and otherwise E's or F's run.
- **The committed build replays the scratch rows.** The committed build's competition tier at seeds 0-199 and war rooms at seeds 0-39 must equal the scratch copy's rows. Then the 800-seed and 480-seed results stand for the build.

### 5.4 Run time

On one core; measured unless marked est.

| Run | Seasons | Time |
|---|---|---|
| The build | - | 82 s, plus 15-20 s for each test-host start |
| Competition tier, 200 seeds (set 0, and each of A-D) | 1,600 | 22.7 min (0.85 s a season) |
| War rooms, 480 seeds | 4,800 | est. 2.5-3 h (the reader takes 1.22 s at 8 and 3.69 s at 12; the random player 0.59 s and 1.49 s) |
| E or F, 480 seeds | 4,800 | est. 2.5-3 h |
| Competition tier, 800 seeds | 6,400 | est. 1.5 h |
| The committed build's replay | 1,600 + 400 | est. 35 min |
| Hand-off headline, 100 seeds | 2,400 | est. 1-1.6 h |
| Goldens, `BudgetNoughtRows` and its twin, the lab's own tests | - | est. 1 h |

That is about 10-13 hours of one core, and about half that when both slots are free.

### 5.5 Commands

Run them from the snapshot's root, through `sh <scratchpad>/tools/cpu-lock2.sh`, one background job at a time. Keep lab output on D: (`D:/ClaudeScratch/b9/`), not C:.

- **A competition part:**
  `B9_PREP_CAP=3 B9_PREP_SPEND=all B9_GS=0.2/0.6/0.2 B9_COUNTER=today BALANCE_TIER=competition BALANCE_FROM=0 BALANCE_COUNT=200 BALANCE_PARTS=D:/ClaudeScratch/b9/runs/A/competition dotnet test Tools/SimulationTests --no-build --nologo --filter "FullyQualifiedName~BalanceLabParts.PartReport"`

  Then run the same with `--filter "FullyQualifiedName~BalanceLabParts.MergeReport"`.
- **A war-room part:** `BALANCE_TIER=warrooms BALANCE_FROM=<k> BALANCE_COUNT=40 BALANCE_PARTS=D:/ClaudeScratch/b9/runs/<set>/warrooms`, then the same `PartReport` command, for k = 0, 40, ... 440. Then the merge.
- **The hand-off headline:** `BALANCE_TIER=headline BALANCE_FROM=<k> BALANCE_COUNT=20`, for k = 0 to 80.
- **The tuning-0 pins:**
  - `--filter "FullyQualifiedName~BudgetNoughtPlaysTheSeasonsItPlayedBeforeTheDriver"`: on held cells, it must stay `5481eec784acd1ed`;
  - `--filter "FullyQualifiedName~BalanceLabGoldens.Budget0Size8"` and its siblings;
  - `--filter "FullyQualifiedName~BalanceLabTests"`, which takes about 80 s.

## 6. Acceptance

B9 is accepted when all of the following hold under the chosen set.
- **B1 and B2.**
  - The weekly estimate, at 8 and at 12, is inside 60-70%, and so is its Wilson interval (about ±1.5 points at 800 seeds).
  - Wilson treats each competition as independent. Competitions in the same season are not independent, so the interval is slightly narrow, and the PR says so.
- **B3 and B4.**
  - The season estimate is inside 25-35% at 8, and inside 18-28% at 12.
  - Its interval is quoted: about ±3.2 points at 8 and ±2.9 at 12, at 800 seeds.
  - An interval that crosses a band's edge is flagged in the PR.
- **B5 and B6.** On 480 seeds, at both sizes, the gap is at least 10 and its bootstrap low end is above 5.
- **B7.** On 480 seeds the share carried is inside 35-50%. Its interval is quoted, with the number of counters it rests on.
- **Nothing old moves.**
  - Set 0 replays today's competition rows.
  - On held cells, `BudgetNoughtRows` stays `5481eec784acd1ed`.
  - The passive player's tuning-1 rows equal its tuning-0 rows.
  - Every digest held at 0 is unchanged, and every old-save fixture replays to its record.
- **Tests.** Each at its floor:
  - the dotnet subset;
  - the EditMode run, with the Mono harness for the EditMode-only pins;
  - the lead's UMA PlayMode set.
- **The owner signs off** the numbers in the PR.

If no set meets every band, B9 reports the nearest set to the owner, with each band's numbers. B9 widens no band itself.

## 7. What B9 hands on

**What B9 publishes with the PR:**
- the competition and war-room tables under tuning 1;
- the 100-seed headline under tuning 1: all 12 policies at budget 0, with Q4 and the pacing tables;
- the counter's tally table;
- the part files, kept on D:.

**What each later stage takes from it:**
- **R7, role moments' enrolment** (ROLE-MOMENTS-PLAN.md:687, 698).
  - It needs settled rules for fresh seasons, and the bands.
  - R7 then moves `StoryRules.Current` to Roles, and re-records the goldens and `CommitmentRulesSeasonDigests`.
  - It reruns B9's cohorts with Roles on: weekly wins, the Winner rate and counters carried, against B1-B4 and B7.
  - It runs the pacing lab, and asks the owner to sign off.
  - It also waits for story rules 10.
  - Role moments' own later tuning is keyed to story rules, not to tuning (plan 32, from an owner answer of 2026-10-30).
- **R8, the holder's offer.** It waits for B9's numbers and for a second owner sign-off. Its tests include a B9 rerun with R8 on.
- **L2, role grants and time.** It needs the B9 measurement the ROLE doc requires (the ROLE doc :153, :158).
- **D6, dialogue's measurement** (DIALOGUE-PLAN decision 6 and §10).
  - It pairs 100-seed seasons with dialogue on and off, after R7's B9 run and with NPC ticks on. Its off arm is its own, at tuning 1.
  - It needs lab policies that issue `StartDialogue` and `ChooseDialogue`. No lab policy does that today.
  - D7, dialogue on by default, waits for D6.
  - The plans have R7's enrolment and D7 land together, but D6 runs after R7's measurement. So either R7's enrolment commit waits for D6, or the two are separated. That is the lead's to settle with R7.
- **H1d, the All-Star parts that move balance** (ALL-STAR-HISTORIES-PLAN §9).
  - It needs the formulas settled: tuning 1, and, under decision 2, competition rules 5 left free for Will's throw.
  - It is measured on the lab's All-Stars grids at 8 and 12, with paired seeds, against Q3, Q4 and the weekly wins.
  - It brings a new alumni rules value, for fresh seasons only.
- **Plan 32's S9.** The plan is on branch `claude/storyline-house-talks-back`, not on main.
  - S9 uses B9's tuned season as its story-rules-11 arm.
  - It edits the lab's files only after B9's edits to them have landed.
- **Story rules 10**, the pariah fix, comes next after B9 (§8).

## 8. What B9 does not do

- **Q5.** The story pariah rate (target at most 20%; 16.3% and 45.1% today) belongs to story rules 10, and R7's enrolment waits for it. B9 reports it as the next dependency and measures nothing for it.
- **Q4.** Reported, not tuned (§3.4).
- **Role moments, dialogue and the All-Star histories.** B9 tunes the seasons they will join, with all three switched off.
- **The 45-minute season and B-1.** Reported, not tuned.
- **The reach history.** The war rooms' reach constants stay as they are, unless the owner confirms the season named in decision 10.
- **Competition rules 5.** Not taken (decision 2).
- **A schema.** None. Anything that needs a new field stops B9 (§4.5).

## 9. Slices

- **B9-0, this note.** Docs only.
- **B9-1, the measurement.** All in scratch copies: set 0 and sets A-D, the war rooms, E or F if the tally table calls for one, and the acceptance runs. Nothing is committed except the numbers, which go into this note's as-built section.
- **B9-2, the rules.**
  - `TuningRules` with the chosen numbers, the validation, and the levers keyed to tuning 1.
  - The copy, and the lab's held cell, view and tables.
  - The moved pins.
  - `ApplyFresh` still writes 0. So tuning 1 is defined and tested on held seasons, but no shipped path reaches it yet.
- **B9-3, the enrolment.**
  - `ApplyFresh` writes `TuningRules.Shipped`.
  - The goldens and twins are recorded, and the old-save cell is written.
  - The committed build replays the scratch rows.
  - The PR goes to the owner for sign-off. Tuning 1 freezes when it merges.

B9-2 and B9-3 travel in one PR.

## 10. Risks

- **The bands may not all fit one set.** Preparation moves the weekly bands and the season bands together. If a set meets B1-B2 but misses B3-B4, the owner chooses (decision 9).
- **B7 is sparse.** It rests on about 50 counters on 480 seeds, so its interval is about ±13 points.
- **A counter shape changes D3's tie rule** (D3-L4) for new seasons. That is the owner's call, through the PR.
- **Every tuning-1 verdict reads differently.** The weights move every new season's Game Sense, including the player's finale page.
- **Copy and pins.** The study's copy, the standalone verify and three PlayMode tests read 5 today. A reader the build misses would show a wrong number, but only in tuning-1 seasons.
- **Two sessions share the lab's files.** Plan 32's S9 and R7 edit `BalanceLab*.cs` after B9, so B9's edits land first.
- **What B9 claims.** Tuning rules 1, in its own field. It claims no schema number, no command kind and no story rules value, and it releases competition rules 5.

## As built

B9-1 (the measurement, three rounds in scratch copies) and B9-2/B9-3 (the rules and the enrolment, one PR), 2026-11-03;
the proofs' follow-ups (a frozen schema-28 walk held, the diary's rules line, `StudyWords`), 2026-11-05.
The owner signs off the numbers below in the PR before it merges; tuning 1 freezes then.

### The numbers against the bands

The rule set is round 3's **S8F**, with **K1 and K5** for the counter and the **0.2 / 0.6 / 0.2** weights. Weekly and
season wins are the studier at fixed 0.5 on competition seeds 0-799 (Wilson 95%); B5-B7 are the war rooms on seeds 0-479.

| # | Band | Today (`26b65f09`) | Tuning 1 | Verdict |
|---|---|---|---|---|
| B1 | weekly wins at 8: 60-70% | 95.2% [94.0, 96.2] | 3,309 of 5,132 = **64.5% [63.2, 65.8]** (HoH 76.9%, veto 56.3%) | met, interval inside |
| B2 | weekly wins at 12: 60-70% | 94.7% [93.6, 95.7] | 3,879 of 6,299 = **61.6% [60.4, 62.8]** (HoH 75.4%, veto 51.9%) | met, interval inside |
| B3 | season wins at 8: 25-35% | 73.5% [67.0, 79.1] | 229 of 800 = **28.6% [25.6, 31.9]** | met, interval inside |
| B4 | season wins at 12: 18-28% | 48.0% [41.2, 54.9] | 198 of 800 = **24.8% [21.9, 27.9]** | met, interval inside |
| B5 | the gap at 8: at least 10, low end above 5 | 10.2 [8.9, 11.5] (today's weights, 480 seeds) | **14.6 [13.1, 16.2]** | met |
| B6 | the gap at 12 | 8.7 [7.8, 9.5] (fails) | **12.7 [11.6, 13.7]** | met |
| B7 | 35-50% of accepted counters carry | 46 of 61 = 75.4% [63.3, 84.5] | 27 of 61 = **44.3% [32.5, 56.7]** | met on the estimate (decision 4) |

- The weekly intervals are Wilson's, which treats each competition as independent. A season-clustered bootstrap is
  narrower ([63.6, 65.4] at 8, [60.5, 62.6] at 12): competitions in one season are negatively correlated, so §6's
  "slightly narrow" is the wrong way round, and Wilson is the conservative reading.
- 0.25 / 0.5 / 0.25, the smaller change, also meets B5-B6 on the same seasons (12.4 [11.0, 13.8] and 10.7 [9.8, 11.6]).
  The default stays 0.2 / 0.6 / 0.2 (decision 7); the war-room tier prints both.
- B7 by house: 15 of 26 (57.7%) at 8 and 12 of 35 (34.3%) at 12. The band is on the total.

### What tuning 1 is (`Simulation/TuningRules.cs`)

- **Preparation.**
  - A study fills preparation to 1 (`TuningRules.StudyLimit`); a study at or past the limit is refused before it spends
    anything (decision 6).
  - It is spent at the Advance out of a weekly Head of Household or veto the player entered - played, assisted or
    simulated (`EpisodeEngine.SpendPreparation`). A throw keeps it; the final Head of Household's three parts all count it.
  - **In a house that moved in eight or fewer** (`TuningRules.SmallHouse`): a player who has studied and sits a Head of
    Household out learns from watching it, one more up to 2 (`AfterWatchedHoH`); and the final week's study fills to 2.
    Casts of 9 to 12 get neither.
  - Validation holds preparation to 2 in a house of eight or fewer and to 1 otherwise; 5 at tuning 0.
- **Game Sense** weighs its faces 0.2 / 0.6 / 0.2 (`SenseWeights`); the faces and their notes are unchanged.
- **The counter** (`PactPlans.Settle`): a nominee's standing say counts on neither side of the tally (K5), and a tie in
  a pact the player founded carries the counter only where a member came round (K1); otherwise the founder rule runs
  on, and the player's own pact keeps the members' plan.
- **Versions.** `TuningRules.Off = 0`, `Bands = 1`, `Latest = 1` (validation's bound), `Shipped = 1` (the pinned value
  `ShippedRules.ApplyFresh` writes). Validation admits 0 and 1, and 1 only on competition rules 4 or later. No schema
  bump and no competition rules bump: competition rules stay at 4 and B9 releases 5 (decision 2).
- **Copy, keyed to tuning 1.** Every "n/5" reads `TuningRules.PreparationOutOf` (5 at tuning 0, so every tuning-0 string
  is byte for byte as it was): the study's log line, the diary's study section, record row and review, the free-time
  board and screen, the campaign screen and the competition brief (`CompetitionWords.EarnedRule(state)`). Under tuning 1
  the diary offers no study at the limit and says why; `StudyUse` says the next weekly competition uses it up unless the
  player throws it; and the record's rules line (at tuning 0 its old sentence) names the season's study limit and, in a
  house of eight or fewer, the watch. Those three sentences live in `Runtime/Presentation/StudyWords.cs`, Unity-free like
  `CompetitionWords`, and the director's `StudyUse` and `StudyUseShort` read them there. Two new log lines, both private to the player: "Your preparation was used in the competition." and "You watched the Head of
  Household competition and learned from it." The standalone verify's opt-in study workload reads the season's limit.

### Where the build departs from the plan's text

- **§3.1's candidate is not what shipped.** Round 1 screened A-D: no single cap with the spend met B3 and B4 together
  (at 8 every spend-all cap left season wins near 24-26%; only keeping a veto bonus lifted them, at 79% weekly). Round 2's
  watch (M8) met B3 on its estimate; round 3 added the final week's study (S8F) and confined both to houses of eight or
  fewer, which put all four intervals inside.
- **§3.3's shapes.** The count on today's seasons put K1, K2 and both at 29.5%, under the band, so neither E nor F was
  played. K5 (a nominee's say is not counted; a nominee cannot vote) is round 2's; with K1 it measured 44.3%.
- **§4.2's bound** is 2 or 1, not 0-3.
- **The lab's tally table** replays today's rule, K1, K5 and both from each counter's tally, not K2.
- **§4.7's PlayMode tests need no change.** The three it names (`EpisodeStudyPlayModeTests`, the diary record's and
  `FreeTimeMetrics11`'s) and every other PlayMode test that reads preparation run on seasons that stay at tuning 0: the
  director's own season when no save exists is `ContentCatalog.Create(20260910)`, which never passes through
  `ApplyFresh`; `InstallDiaryFixture` installs `ContentCatalog` seasons, and the free-time and war-room fixtures
  (`FullHouse`) `SeasonBuilder` ones. The PlayMode tests that do start a season
  through `StartSeason` (tuning 1) read no preparation, no Game Sense weight and no counter's tally; the one that reads
  `ShippedRules.Fields` compares `ApplyFresh` with itself. So their tuning-0 literals ("up to the limit of 5.",
  "Study preparation: 1/5", "1 / 5", `Math.Min(5, budget)`) stay as tuning 0's copy pins, and `FreeTimeMetrics11` reads the
  count's denominator from `TuningRules.PreparationOutOf` (exactly 5 there). PlayMode does not run in this PR's proofs;
  the lead's UMA PlayMode set is the gate.
- **Not done in this PR** (§4.7 and §7): the tuning-1 old-save cell `s30-mode2-allweek-tuning1-w3-campaign` (and with it
  a native-30 route for a Regular season on the tuning rules in `CurrentOldSaveTrace`: today only the alumni rules take
  that route, and the observers' own test seasons are held at tuning 0); the 100-seed tuning-1 headline for the hand-off.
  Both are named for the owner in the PR.

### What moved, and what was kept

- **Kept at tuning 0, byte for byte:** `BalanceLabTests.BudgetNoughtRows` (`5481eec784acd1ed`, every cell held at tuning
  0); every season digest tier (they build their seasons by hand at tuning 0); `FreshSeasonStateDigestTests`' 21 records
  (with the one tuning int taken back to 0, as the alumni histories are); the dialogue preview witness and the legacy
  observers' mode-2 season (held at tuning 0: a season on the tuning rules has no schema-29 form).
- **Held at tuning 0 by the tests that take a fresh season below schema 30** (decision 8; a season on the tuning rules has
  no schema-29 form): `FrozenEpisodeV29IntegrationTests`' role-moment and dialogue seasons (`00d10b5c`) and
  `FrozenEpisodeV28IntegrationTests`' lie walk (`9c88b434`), each from its start, so each walks exactly as before B9.
- **New pins:** `TuningRulesTests.StudierTuning0Rows` (`9b9bf8a15644b745`: four studier seasons held at tuning 0 hash to
  the reference competition tier's own lines) and `StudierTuning1Rows`; `BalanceLabTests.BudgetNoughtTuning1Rows`;
  `CompetitionWordsTests.TheEarnedLineCountsPreparationOutOfTheSeasonsOwnLimit` (the briefing's earned line: at tuning 0
  the old sentence for 0, 1, 3 and 5 banked; at tuning 1 out of the season's limit, used up rather than carried over);
  `TuningCopyTests` (2: `StudyWords` at tuning 0, word for word, and at tuning 1 in a house of 8, its final week, and a
  house of 12). Both run in the subset and in the editor.
- **Re-recorded with the reason:** `BalanceLabGoldens` (all 48 cells and the `Rules` tuple, `tuningRulesVersion=1`); the
  parts' stamp is `balance-lab-parts/v8`.
- **Moved to the first undefined value** (`TuningRules.Latest + 1`): the refusal pins in `PooledSchema30ShapeTests`,
  `AlumniHistoriesTests`, `LegacyDigestSchema30ObserverTests` and `PersistenceV30MigrationTests`. `ShippedRulesTests`
  expects tuning 1, and the native-30 observer admits a season's own tuning value.

### Proofs

All through cpu-lock2, in snapshots on D: (`D:/ClaudeScratch/b9/build/`), never in the tree. Round 1 ran on a snapshot
whose Simulation, Runtime and balance-lab sources are the commit's own (four test files differed, none of them the
lab's or a tier's):
- **The committed build replays B9-1.** Its competition tier at tuning 1, seeds 0-199 (1,600 seasons), equals the
  scratch records season by season: the studier at 8 (0.5 and 0.8) round 3's M8F, the studier at 12 round 1's C, the
  passive player round 1's A, and 80 of round 3's directly played S8F seasons. Its war rooms on seeds 0-39 (400
  seasons) equal round 2's E2. Whole records, timing and the tally's own shape left out.
- **Tuning 0 leaks nothing.** The same tier held at tuning 0 (`BALANCE_TUNING=0`) merges to rows byte-identical to the
  reference, SHA-256 `92c74b608cc446a0`. Budget nought held passes at `5481eec784acd1ed`.
- **The digest tiers**, by name and exact count, all passed: commitment rules 2, mode one 2, the leaks 2, the war rooms 2,
  all week 4, the mode-two sweep's rest 58, the unified vote's rest 14, the readers' parity rest 28.
- **Re-recorded:** the goldens' `Rules` tuple matched; all 48 cells moved and are recorded from that run. Budget nought's
  twin is `1ee55c2a92571363`.
- The war-room tier's tally table on seeds 0-39: 7 accepted counters, the season's own rule replays every one (0
  mismatches), the player never among the says.

Round 2 ran on the commit itself (`03b61ec7`; a git-archive snapshot for dotnet, the tree read only for the rest, its
files hashed and its status compared before and after, both unchanged):
- **The dotnet subset:** 16,589 of 16,589 passed (the floor, 16,578 + TuningRulesTests' 11). Budget nought's tuning-1
  twin and both budget-0 goldens pass as recorded.
- **The offline compiles** without UMA (`GAMESIM_ACCEPTANCE=D:\GamesimNoUma`) and with it (`D:\GamesimPlay`, whose dag
  defines `GAMESIM_UMA`): 0 errors in every assembly, and no warning in a file B9 changed. The listers count EditMode
  9,751 (the new floor), PlayMode 1,480, the UMA build's PlayMode 1,522 and `Gamesim.Uma.PlayModeTests` 134.
- **Under Unity's Mono** (the editor's mono.exe, Harness2), every fixture B9 touches that the subset does not run, all
  green but one: TuningRulesTests 10, ShippedRulesTests 36, PooledSchema30ShapeTests 22, AlumniHistoriesTests 31, the
  legacy observers 62 and 41, CompetitionWordsTests 8, CompetitionExplanationTests 4, CompetitionRulesV3Tests 8,
  EpisodePreparationTests 9, FreshSeasonStateDigestTests 22, PersistenceMigrationTests 59, PersistenceV30MigrationTests 32,
  the V29, V3-V8 migrations and the serializer cache (40, 62, 34, 45, 55, 17, 19, 3), FrozenEpisodeV25ContractTests 167,
  StrategicOldControls05ParityTests 28, the dialogue save fixtures (4, 1, 15, 69), AlumniHistoriesSaveTests 4,
  PersistenceTests 30 and **OldSaveAcceptanceTests 266 of 266**: every old save replays to its record.
- The one: FrozenEpisodeV29IntegrationTests, 63 of 130 cases, because its seasons are fresh and a schema-29 season
  cannot hold tuning 1. Held at tuning 0 in `00d10b5c` (decision 8).

Round 3 ran the Mono kit on `00d10b5c` (the frozen schema-29 fix) and the fixtures round 2 had not reached. The compiles
and listers matched again (EditMode 9,751, PlayMode 1,480, the UMA build's 1,522, Uma 134), and FrozenEpisodeV29IntegrationTests
130 of 130, DialogueCampaignV1RetainedReaderTests 11, DialogueRules2PublicRescueTests 2, PersistenceV13-V15 (12, 7, 14),
the three role-moment persistence fixtures, UnifiedVoteSaveStoreTests 16 and TuningRulesTests 10 passed. Four were red:
- **FrozenEpisodeV28IntegrationTests, 1 of 23: real.** Its lie walk starts a fresh shipped season (tuning 1 since B9-3)
  and takes it to schema 28, where `StripSchema30` refuses ("Never erase enabled tuning rules"). Held at tuning 0 from
  its start in `9c88b434`.
- **PersistenceV26, V27 and V28MigrationTests (53, 98 and 64 cases): the harness, not B9.** Their frozen corpus is found
  from the working directory ("Exactly one bounded package required; missing/ambiguous is failure, never a skip"), and
  the harness ran from its own folder. Round 4 runs them, and the two corpus fixtures, from a snapshot's root.

Round 4 ran on `9c88b434` (a git-archive snapshot for dotnet; the tree read only for the Mono kit, its files hashed and
its status compared before and after, both unchanged):
- **The dotnet subset:** 16,590 of 16,590 passed. The commitment rules' digests passed (2 of 2).
- **The offline compiles** without UMA and with it: 0 errors, no asmdef difference from either copy, and no warning in a
  file B9 changed. The listers count EditMode 9,754 (the floor), PlayMode 1,480, the UMA build's 1,522 and Uma 134.
- **Under Unity's Mono**, 71 fixtures and 2,695 cases: every fixture that takes a season below schema 30, every user of
  a fresh-season helper outside the subset, every persistence and migration fixture (V3 to V30), the frozen V23 to V26
  contracts and corpora, the legacy observers and **OldSaveAcceptanceTests 266 of 266**. All passed but TuningCopyTests
  (2), which could not run there: the director's type initializer makes an engine call, so any test of its statics is
  Unity-only. Its sentences moved to `StudyWords` in `9dbb3677`. FrozenEpisodeV28IntegrationTests passed 23 of 23, and
  PersistenceV26, V27 and V28MigrationTests (78, 127, 96) and the two corpus fixtures (34, 329) passed from the snapshot's
  root.
- Its later dotnet steps were held, before they started, by the machine gate that gives Codex the machine
  (`D:/GamesimQuiet`, from 08:06), and were stopped while held; round 5 runs them on the head.

**Still to run on the head (round 5).** `9dbb3677` moves the diary's three study sentences into `StudyWords` and adds
`StudyWords.cs` and TuningCopyTests to the subset. Simulation and the lab are unchanged since `03b61ec7`, so round 1's
digest tiers and lab pins and round 4's Mono fixtures stand for them. Round 5 was queued at 08:55 on 2026-11-05 and
waited behind Codex's machine gate until it was stopped at 13:00, before any step had started. It still has to run:
- the subset at its new floor (16,592), with TuningCopyTests and CompetitionWordsTests by name;
- both offline compiles and the listers (EditMode 9,754);
- the four copy fixtures under Unity's Mono;
- the digest tiers by name, budget nought and its twin, and the budget-0 goldens.

### For the owner

- **Sign off the numbers** above (decision 9), and two rule calls inside them:
  - K1 and K5 change D3's tally rule (D3-L4) for new seasons: a nominee's say no longer counts, and the player as founder
    no longer takes a tie on their own say alone.
  - S8F depends on the cast that moved in: the watch and the final week's 2 apply in houses of eight or fewer.
- **Found while measuring, not changed:** the player loses every jury tie (`ResolveJury`'s source tie rule gives a tie
  to the second finalist in cast order, and the player won none of the ties measured). About a third of the studier's
  final twos at 8 end 3-3, which is why B3 was hard at 8. A fair keyed coin would let plain set C meet B1-B4 with no cast
  switch (about 32.7% at 8 and 26.9% at 12). It is a finale rule with source parity, so it is the owner's call.
- **Casts between and below the bands** are not banded. Under tuning 1 every weekly estimate from 4 to 12 is inside
  60-70%; season wins at 9, 10 and 11 are 35.3%, 31.3% and 26.7% (300 seeds), and at 4-7 between 8.0% and 29.7%.
- **Q4** moves only for players who study (0.54 to 0.75 at 8, 0.80 to 0.91 at 12); **Q5** stays out of range (14.6% at
  8, 47.2% at 12): story rules 10 is the next dependency.
- **R7 and D7.** R7's enrolment and D7 are planned to land together, but D6 runs after R7's measurement: the lead
  settles the order with R7.
- **The words a tuning-1 season shows** are new sentences (tuning 0's are unchanged): the study's limit ("up to the
  limit of 1", or 2 in a small house's final week), "Your preparation is full. The next weekly HoH or veto competition
  you enter uses it up.", the record's "Used up by your next weekly competition.", the rules line's watch, the briefing's
  "A weekly Head of Household or veto you enter uses your preparation up", and the two private log lines. They are
  read best in play.
- **Not done, and why** (§4.7, §7): the tuning-1 old-save cell, which needs a native-30 route for a Regular season on the
  tuning rules in `CurrentOldSaveTrace` and a fixture written by `Tools/OldSaves`' recorder at this build; and the
  100-seed hand-off headline (about an hour of both slots), left while the machine is Codex's. R7 and D6 run their own
  tuning-1 arms either way.

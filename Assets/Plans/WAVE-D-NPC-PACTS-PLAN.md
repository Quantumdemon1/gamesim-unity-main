# Wave D: NPC strategy, war rooms and leaks (D2, D3, D4)

*This is the single corrected plan for ACTIONS-DEALS-ALLIANCES-PLAN B2–B4 (`Assets/Plans/ACTIONS-DEALS-ALLIANCES-PLAN.md:489-503`). The Wave D foundation requires all three (`WAVE_D_FOUNDATION_IMPLEMENTATION.md:147-149`). It replaces the three separate designs and applies every correction from their critique; §8 lists where each one landed.*

**Source.** `claude/lead-integration` at 7b80f1de, which is 9044da53 plus a README/.gitignore commit, at schema 26. Schema 27 is Codex's fec50004 on `claude/schema27-import` (worktree D:\GamesimWaveC, parent c15eb54f, an ancestor of 9044da53). Line numbers are at 9044da53. fec50004 moves only these lines:
- `EpisodeState.cs:247`;
- `EpisodeValidation.cs:48`;
- `EpisodeSaveValidation.cs:13`;
- the digest projection after `CommitmentRulesSeasonDigests.cs:374`.

**Paths.** `Sim/` = `Assets/Gamesim/Simulation/`, `Story/` = `Sim/Story/`, `Run/` = `Assets/Gamesim/Runtime/`, `Tests/` = `Assets/Gamesim/Tests/EditMode/`. A bare `:N` refers to the file named last.

---

## 0. Preface

### 0.1 Owner decisions in force
- **Claude has taken over the whole project from Codex.** The lead reviews and merges schema 27 (fec50004) and owns D1 as well. No Codex hand-off step is left in this plan. The story session is still told of every schema, kind and vocabulary claim, because the two sessions share the branch.
- **D2: NPC strategy pauses while the game is closed.** There is no offline catch-up, and nothing reads the wall clock.
- **D3: majority with one counter.** An alliance's agreed target is chosen by a majority of the members present. The player gets one counter.
- **D4: juggling costs exposure.** Each player pact beyond the first adds **+5 percentage points** to each secret pact's weekly leak odds. This plan reads that as each secret pact *the player is in*; NPC-only pacts take the base odds (§6 Q4).
- **Evidence standard.** Constructed fixtures are allowed for unit and whole-state proof. Genuine-season evidence is required only in the final acceptance walk.
- **Performance thresholds.** p95 ≤ 16.7 ms and p99 ≤ 33.3 ms at 1920×1080 on a GTX 1060.
- **The 16-person stress run uses a test-only combined roster.** Each roster has 12 templates, so `SeasonBuilder.LargestHouse` caps a single roster at 12.
- **Decisions Codex built without sign-off are ratified as built.** That includes B1's policies kept separate under one record, which D1's alliance-call family will follow when it absorbs D3's plan row.

### 0.2 Project rules every slice obeys
- **Rule start weeks.** A rule change sits behind a saved start week, and behaviour is keyed to that week, never to `schemaVersion`. Recorded seasons digest byte for byte.
- **Draws and ids.** `Roll(s)` and `Log` consume the season's stream and ids (`Sim/EpisodeEngine.cs:2377`, `:2462`). Anything new that must not touch them draws from a keyed `StoryRandom` stream (`Story/StoryRandom.cs:21-48`).
- **Knowledge gate.** Nothing hidden moves anything shown.
- **NPC-to-NPC changes never go through `EpisodeEngine.Change`.** They use `RelationshipLedger.Move/Record` (`Sim/NpcSocialActions.cs:527-537`).
- **Captions are contracts.** No existing caption changes. New captions are unique.
- **A saved field needs a schema bump.** That means a frozen previous version, a literal-only upgrade, and every pin moved: migration tests, frozen contracts, message strings, AddedSince lists, dispatch keys and upgrade chains. The dotnet subset never runs the EditMode-only pins.
- **Test constraints.** Unity's NUnit is older than the subset's: no `Is.AnyOf`, no `Assert.Multiple`. Internal members are invisible to Unity test assemblies, so every new entry point a test calls is public.
- **The audited pipeline runs alone.**

### 0.3 One shared schema bump: W28 (storage only, before any rule)

**Precondition.** fec50004 is reviewed and merged onto the lead. Since c15eb54f, the only file both sides changed is `Tools/baseline.txt`, so the merge's single conflict is the floors (verified with `git diff --name-only`). Precedent for one version carrying several systems: `Sim/EpisodeState.cs:384-389`, schema 11.

| Owner | Field | Inert value | For |
|---|---|---|---|
| `EpisodeState` | `schemaVersion` | 28 | all |
| `EpisodeState` | `allianceLeakRulesStartWeek`, `pactPlanRulesStartWeek`, `allWeekRulesStartWeek` | 0, 0, 0 | D4, D3, D2 |
| `NpcSocialState` | `beatWeek`, `beatWindow`, `beatsFired`, `beatSeats` | 0, −1, 0, 0 | D2 |
| `NpcSocialState` | `beatPlan : List<string>`, `acts : List<NpcActState>` | [], [] | D2 |
| `SeasonLedger` | `plans : List<PactPlanRow>` (`PlanSay` inside) | [] | D3 |
| `EpisodeCommandKind` | `AnswerPactPlan = 62`, `WitnessNpcAct = 63`, appended after `Negotiate = 61` | refused | D3, D2 |

- **D2's start week is on `EpisodeState`.** It sits beside the other two so that enabling, the checks, `PortVerification` and the digest strip treat all three alike. D2's cadence state lives in `NpcSocialState` (`Sim/NpcSocialState.cs:10-17`).
- **Clone.**
  - `NpcSocialState.Clone` (`:57-64`) deep-copies `beatPlan` and `acts`. `NpcActState` holds only scalars and strings.
  - `SeasonLedger.Clone` (`Sim/SeasonLedger.cs:35-48`) deep-copies `plans`, and each row's lists.
- **Kinds.** Each new kind gets an explicit `Execute` case beside `RenameAlliance` (`Sim/EpisodeEngine.cs:188`) that refuses before anything is spent, drawn or logged ("Not available in this season."). Without that case the kind would fall to `Social` (`:191`) and refuse with the wrong reason. Each rule slice later replaces its kind's refusal with the gated handler.
  - The pin `Tests/NegotiationTests.cs:29` (`Negotiate == 61`) stays. Add pins for 62 and 63.
  - Two kind sweeps must pass: `Tests/HouseDialogueTests.cs:188` and `Tests/ConversationIntentTests.cs:172`.
- **Vocabulary (constants only).**
  - `StoryReceipts.DoubleDealt = "story:double-dealt"`: permanent, impact −10, with a `Describe` entry. It is added to `StoryReceipts.Permanent` (`Story/StoryOdds.cs:150`) and `RelationshipLedger.Permanent` (`Sim/RelationshipLedger.cs:46`).
  - Event kinds: `sighting`, `overheard`, `pact-plan`, `double-dealing`.
- **Validation.**
  - One new partial, `Sim/EpisodeValidation.WaveD.cs`, is called once from `EpisodeValidation.cs`. It checks each start week is in 0..101 and at most `week + 1`.
  - D2's fields are checked in `Sim/EpisodeNpcSocialValidation.cs`. `plans` are checked in `Sim/EpisodeLedgerValidation.cs` beside the calls (`:65-68`).
  - While a design's start week is 0, its fields hold their inert values, and none of its kinds' events or receipts exist.
- **Migration.**
  - `PrepareCurrentPayload` returns a clone at 28. For 1–27 it runs `UpgradeV27ToV28(version == 27 ? original : PrepareV27Payload(original, out _))`, where `PrepareV27Payload` is 27's dispatcher body, retained as Codex retained `PrepareV26Payload`.
  - `UpgradeV27ToV28` first runs `FrozenEpisodeV27.Validate(original)`. It then appends literals only: three root zeros; on `npcSocial`, `0, −1, 0, 0, [], []`; and `ledger.plans: []`. It sets the header to 28 and never infers anything.
- **`Run/Persistence/FrozenEpisodeV27.cs`.**
  - It validates literal 27 independently of the growing DTO, projects to literal 26 (dropping `voteBindingWeek`/`voteFirstRevealWeek`, header 26) and calls Codex's `FrozenEpisodeV26`.
  - That chain already pins `NpcSocialState`, `SeasonLedger` and the story vocabulary (`Run/Persistence/FrozenV25Validator.NpcSocial.cs`, `.Ledger.cs`, `.Vocabulary.cs:393-421`). So any 28 field in a 27 payload is refused.
- **Digest (X3).** `Checkpoint` serialises the whole state (`Tests/CommitmentRulesSeasonDigests.cs:374`).
  - Codex's 27 sends exact-27 traces to `LegacyDigestSchema27Observer` by reflection. That observer requires literal 27, and requires the trace to *equal the state's own JSON*.
  - W28 therefore adds `LegacyDigestSchema28Observer`, which does five things in order:
    1. runs the real validator;
    2. asserts every 28 field is present and inert ("must be 0/empty/−1", as `:387-426` does for older fields);
    3. removes those fields and sets the header to 27;
    4. proves `FrozenEpisodeV27` accepts the projection (the neutral inverse);
    5. hands the projection to the 27 observer's field checks.
  - This needs a test-only split of the 27 observer: its binding of trace to state, and its field checks. The goldens do not change.
- **Pins moved 27→28.**
  - `EpisodeState.cs:247`, `EpisodeValidation.cs:48`, `EpisodeSaveValidation.cs:13`.
  - Every `PersistenceV*MigrationTests` current-schema pin, the `FrozenEpisodeV23-26` contract tests, and the `UnifiedVoteInert*` header checks.
  - New: `PersistenceV28MigrationTests` (a v27 fixture written by the 27 build loads through the chain) and `FrozenEpisodeV27ContractTests`.
  - The `SimulationTests.csproj` Compile list and `Tools/baseline.txt`.
- **Nothing is enabled anywhere in W28.** Each design's director, importer and `PortVerification` enable lands with its last rule slice (§5), so no build plays half a design.
- **Start-week policy (X4).**
  - Director: `StartSeason` enables all three at week 1, beside `EnableCommitments` (`Run/Episode/EpisodeDirector.Season.cs:289`).
  - Importer: enables all three at `week + 1`, beside `Run/Persistence/WebSaveImporter.cs:209-213` (confirm, Q1).
  - Migration and recovery write 0. Tests leave them off.
  - `Run/Episode/PortVerification.Season.cs:139` gains asserts that all three are 1.
- **Fallback if the owner wants separate bumps.** 28 = D4, 29 = D3, 30 = D2, in landing order. The kinds stay 62 (D3) and 63 (D2).
- **Tell the story session:**
  - schema 28 and kinds 62/63;
  - `story:double-dealt` and the four event kinds;
  - the resolver now decides `LeakAlliance`/`SpreadAlliance`, and the whisper's text changes;
  - Staged Feud (`AllianceKnowers ≥ 3`, `StoryCatalog.BondsAndSecrets.cs:584`) fires sooner, The Secret Alliance casts less often, and the emergency meeting may cast more;
  - D2's beats fire `StoryConfronted`/`StoryGossipedAbout`/`StoryCampaignedTo` mid-window.

### 0.4 Build order
0. **Now, touching no schema-27 file:**
   - D4-0: `AllianceLeaks.cs` and `Knowledge.PactOfPair`, pure and unwired;
   - D3-S0 and D3-S1: the measure, and pure `PactPlans.cs`;
   - D2-S0: the measure.

   All of them are tested through the Mono reflection harness.
1. **Review and merge fec50004**, then W28.
2. **D4**, which owns the shared eavesdrop line builder and the HUD event-kind edit.
3. **D3.**
4. **D2.**
5. **Combined evidence** (§5).

---

## 1. The week's order of operations (X7)

Under the rules, D2's **catch-up** runs inside `Apply` after `Execute` (`Sim/EpisodeEngine.cs:39`) and before `CancelInvalidNpcConversations` (`:40`) and `ReconcileAllianceRows` (`:41`), so a pact a beat forms is reconciled and validated (`:45`) in the same candidate. **Close** fires a window's remaining beats before that window's decision.

| Step | Order inside the step |
|---|---|
| HoH → Nomination (Advance) | HohCrowned lapse → phase → `CourtTheHoH` (`:285`; D2 records `court` acts) → HohCrowned anchor (`:286`; D4 gossip and double-dealing) → catch-up: D2's AfterHoH tick 0 |
| Player HoH: `Nominate` (`:63`) | D2 Close(AfterHoH) → NomsSet lapse → nominations → NomsSet anchor |
| NPC HoH: first Nomination Advance | D2 Close(AfterHoH) → NomsSet lapse → NPC nominations (`:289-305`) → NomsSet anchor. The window stays AfterHoH with no beats left (D2-L1). |
| Second Nomination Advance (`:307`) | Close is a no-op → VetoSelection → catch-up: AfterNominations tick 0 |
| VetoSelection, Veto | Window AfterNominations (`Sim/EpisodeEngine.Week.cs:54-56`). `Compete` and the other free commands do not tick, so nothing new fires. |
| Veto → VetoMeeting | VetoWon lapse → `AskForTheVeto` (`:282`) → VetoWon anchor (`:286`) |
| First VetoMeeting Advance, or the player's `ResolveVeto` (`:74`) | D2 Close(AfterNominations) → the veto (`:315-321`) |
| VetoMeeting → Campaign | BlockSet lapse → Campaign → `NpcPromises.Settle` (`:328`) → `NpcDeals.Settle/Propose` (`:332-333`) → BlockSet anchor (`:335`; D4 gossip) → campaign pass (`:336`; D2 records `campaign` acts) → catch-up: AfterVeto tick 0 |
| A campaign social action, including D3's war room | the action → catch-up: D2 beats due at the new tick. D3's answer reads the state after them. |
| Campaign → Eviction | D2 Close(AfterVeto) → EvictionEve lapse → `replyCards.Clear()` (`:344`) → frozen `campaign-close` line (`:348`) → Eviction → EvictionEve anchor (`:350`; D4 gossip) → `PassTheReadings` (`:353`) → D3 lapse of open plans |
| Eviction night stages | No window, no beats |
| Eviction → Social | `EndWithTheEvictee` (`:362`) → EvictionNight lapse → Social → EvictionNight anchor (`:382`: showmances, then **D4's leak pass**, then gossip and double-dealing) → `:383` (Settle; under D2, `Dissolve` only) → deals (`:384-385`) → ageing and narration → `TellThePlayerWhichAlliancesEnded` (`:396`) → catch-up: AfterEviction tick 0 |
| Social → HoH | diary `Require` (`:226`) → D2 Close(AfterEviction) → `StorySocialClose` (`:234`) → week turn (`:235-251`; D2 `acts` cleared) → `replyCards.Clear()` (`:255`) |

- **Close is idempotent.** An exhausted plan (`beatsFired == beatPlan.Count`) fires nothing, so the doubled Nomination and VetoMeeting steps are safe.
- **Close beats never involve the player (D2-H3).** The Campaign and Social Closes run before the clears at `:344` and `:255`, which would discard any reply card they offered.
- **D2-L4.** Under D2, `TellThePlayerWhichAlliancesEnded` is no longer "Logged LAST in the step" (`:2433-2437`). Its contract becomes "last before the house's first beats".

---

## 2. D4: leaks and double-dealing (builds first)

### 2.1 Goal
B4, carrying out decision 12A (`ACTIONS-DEALS-ALLIANCES-PLAN.md:232-234`):
- secret pact facts become whispered on keyed odds, and the house's gossip then carries them;
- an ally who learns of the player's *other* pact gives a double-dealing receipt or holds a grudge;
- a listen-in on an allied pair makes the player a suspected knower of their pact, which V3 shows;
- the C8 defect is fixed: a pair-named story `Spread` grants every pact holding the pair (pinned today at `Tests/AllianceReadTests.cs:183-210`).

### 2.2 Existing code reused
- **Alliance fact** (`Story/StorySystems.cs:132-145`): private, its members as knowers, actor `members[0]`, subject `members[1]`, id minted from `nextSequence`.
- **Legacy visibility.** A pact with no fact is known to all (`:235-242`).
- **Knowledge helpers.** `AddKnower` and `MakeKnown` (`:244-260`); `MakeKnown` runs the hearing checks. `MakeRoom` keeps alliance facts only under the commitment rules (`:209-227`).
- **`Knowledge.Spread`** (`:297-339`). Under hearing rules it runs on a staged clone (`:299-309`). Odds are at `:327-331`; the key is `"w{week}:{anchor}:rumour:{factId}:{teller}"`.
- **The gossip pass and the whisper.** `StorySystemsAt` and `Whisper` (`Story/EpisodeEngine.StoryHooks.cs:366-387`); C8's branch is at `:376`.
- **The over-grant.** `Story/EpisodeEngine.StoryEffects.cs:161-184` (loop `:166`, Reach skip `:170`, the fact recreated with members as knowers at `:172`).
- **Play receipt and progress.** `Story/StoryPlays.cs:140`; `AllianceOf` at `Story/StoryCatalog.Plays.cs:49-52`.
- **Eavesdrop** (`Sim/EpisodeEngine.cs:1313-1354`): `Roll` at `:1334`, `AddStanding` at `:1348`, `OverheardVote` at `:1349`, the memory at `:1350`, the `Log` at `:1352`.
- **V3 and the page.**
  - `Sim/AllianceRead.cs:73-116, 452-512`; `WhisperLine` at `:538`; `IsLeftGoesOnLine` matching at `:469`.
  - Certainty: `FinalistRead.cs:231-239`.
  - Page: `Run/Episode/EpisodeDirector.Alliances.cs:56-124`. Dashed pairs: `Run/Presentation/RelationshipWeb.cs:490`.
- **Grudges and receipts.**
  - Grudges: `Story/Grudges.cs:33, 58-84`, with the cause `AllianceBetrayed`.
  - Receipts: `Story/StoryOdds.cs:137-205`, with `PermanentBadBlood` at `:109-112`.
  - `HeardAbout` (`Sim/EpisodeEngine.Commitments.cs:77-89`) scales its delta by the listener's social stat (`:81`).
- **Pact counts and lines.** `PlayerPactsHeld` (`Sim/EpisodeEngine.Pacts.cs:68-69`); cap 3 (`:39`); the 40 grudge line (`:41`, `:109-111`).
- **Boundary pattern.** `EnableCommitments`/`CommitmentRulesOn` (`Sim/EpisodeEngine.Commitments.cs:35-43`).
- **Web numbers.** `D:\gamesim-web\src\systems\alliance-system.ts:226-279`. The web's event layer records intent, not behaviour, so only its numbers are copied.

### 2.3 Rules
**Gate (X8, D4-M3).** `AllianceLeaks.On(s)` = `allianceLeakRulesStartWeek ≥ 1 && week ≥ it && StoryAt(s, Bonds) && CommitmentRulesOn(s)`.
- Under the commitment rules, alliance facts are never pruned. A pruned fact would read as known to everyone and then be recreated with members-only knowers (`StoryEffects.cs:172`), which breaks "once per (X, P2)".
- The same rules make `Allied` require both people in the house (`Sim/EpisodeState.cs:505-506`).
- The cap of three pacts means the 0.5 cap never binds. It stays as a guard.
- Every rule below checks `On`. With it off, everything is today's: the over-grant, the two-name whisper and the eavesdrop text.

**Weekly leak.** It runs at the EvictionNight anchor inside `StorySystemsAt`, after `NpcShowmancePass` and before the Spread loop. Pacts are taken in `s.alliances` order, and each must meet all of these:
- it is active;
- its fact exists and is Private;
- `fact.week < s.week`, so a pact formed this week waits a week (D4-L1; under D2 pacts form all week);
- at least 2 of its members are Active;
- at least 1 Active houseguest is not a member;
- at least 4 houseguests are Active.

**Odds** = `min(0.5, 0.05 + 0.03·max(0, m−2) + J)`, where `m` is the number of Active members.
- `J = 0.05·max(0, k−1)` with `k = PlayerPactsHeld(s)`, only for a pact the Active player is in.
- `J = 0` for an NPC-only pact.

| Members | Player holds 1 | 2 | 3 | NPC-only |
|---|---|---|---|---|
| 2 | 5% | 10% | 15% | 5% |
| 3 | 8% | 13% | 18% | 8% |
| 4 | 11% | 16% | 21% | 11% |

- **The draw** is `StoryRandom.Chance(s, "w{week}:leak:{allianceId}", odds)`.
- **A leak** is `Knowledge.MakeKnown(s, fact, Whispered)` and nothing else: no knower, no `Log`, no id. The same anchor's gossip, and five more passes a week, carry it. A whispered fact never rolls again.

**One pair, one pact.** `Knowledge.PactOfPair(s, a, b, listenerId)` picks one pact from those holding both people:
1. active before ended;
2. with a listener named, one the listener does not yet know of;
3. then `s.alliances` order.

Under `On` the resolver is used by:
- the `Spread` effect, which grants and widens exactly one pact. The Reach skip at `:170` stays;
- the play receipt (`StoryPlays.cs:140`);
- `AllianceOf` (`StoryCatalog.Plays.cs:49`).

**The whisper names everyone.** Under `On`, an alliance whisper reads "Word in the house: Riley Chen, Jo Park and Sam Lee are working together.", with members in `members` order. `AllianceRead.WhisperLine` matches both forms, so older lines still date their cards.

**Listen-in (D4-M2).** After a successful overhear (`:1348`), take `P = PactOfPair(s, first, second, player)`. The player is added as a knower only if all of these hold:
- P is active and has a fact;
- the player is not a member and not already a knower;
- **P's in-house members are exactly {first, second}.** Otherwise nothing is granted (§6 Q5).

Visibility stays Private, because overhearing does not start the house's gossip. The certainty becomes Suspected. The player's memory text (`:1350`) is unchanged, so `ShareInformation` cannot pass the pact on. A caught listen-in grants nothing.

**The shared line builder (X5, owned by D4).** `EpisodeEngine.EavesdropLine` builds the single `Log` at `:1352`. It adds no extra id and keeps the same audience. Clause order:
1. today's "You overheard A and B. They {reading}.";
2. the vote clause (`OverheardVote`);
3. D2's act clause (§4.3);
4. D4's sentence: " From the way they talked, A and B are working together." It names exactly the members the V3 card will show.

D2's pact/meet clause is suppressed whenever D4's sentence fires. With every rule off, the text is byte-identical to today's.

**Double-dealing.** It is triggered when houseguest X becomes a knower of the fact for pact P2, and all of these hold:
- `On`;
- P2 is active, and the player is a member and Active;
- X is Active and not in P2;
- `s.Allied(X, player)`.

Knowers only grow, so a reaction fires at most once per (X, P2) with no saved record. It is caught in two places:
- the gossip loop in `StorySystemsAt`, beside `HeardOfYourWord`;
- the `Spread` effect, which compares the resolved fact's knowers before and after.

**The reaction** is deterministic, with no draw. X has a rival in P2 if some other member has `Grudges.Severity(X, m) ≥ 20` or `Score(X, m) ≤ −10`.
- **Rival:** `Grudges.Add(X, player, 40, AllianceBetrayed, alliedMultiplier: true)`. That is 48 while allied, past C4's refusal line of 40, and it stacks at half.
- **Otherwise:** `HeardAbout(s, X, −10, "Found out you are also working with {names}.", StoryReceipts.DoubleDealt)`.
- **One line for both paths (D4-M1)**, kind `double-dealing`, audience (player, X): "{X} found out about {P2}, your alliance with {names}. They won't forget you kept it from them."
- Nothing moves between two NPCs, nothing goes through `Change`, and P1 is left alone.

**Knowledge gate.**
- **The player sees:**
  - the whisper when another pact reaches them;
  - the listen-in sentence;
  - each double-dealing line;
  - on their own card, `Pact.exposures` ("Week 5 · Riley found out about it."), read from the double-dealing lines they were shown and matched by pact name;
  - optionally a risk word, computed only from members in the house and the player's own pact count, never from the fact's visibility.
- **Never shown:** the odds, whether a pact leaked, who knows beyond those lines, or NPC reactions to NPC pacts.
- **Accepted inference (as C8's m9):** a double-dealing line implies that somebody talked.

**Saved state.**
- `allianceLeakRulesStartWeek` and the vocabulary, both in W28.
- Validation: no `double-dealing` event and no `story:double-dealt` ledger event while the rules are off; none dated before the start week.

**Determinism.**
- The leak is one keyed coin per (week, pact). It is order-independent and survives a reload.
- The reaction and the listen-in draw nothing.
- New ids come only from the double-dealing `Log` and `HeardAbout`'s ledger event. The listen-in and the whisper change text inside existing `Log` calls.

### 2.4 Integration points
- **Engine:**
  - new `Sim/AllianceLeaks.cs`: `On`, `Odds`, `RiskWord`, `IsCaughtOut`, `Reaction`, `Line`, `IsDoubleDealingLine`;
  - new `Sim/EpisodeEngine.AllianceLeaks.cs`: `EnableAllianceLeaks(s, fromWeek = 1)`, clamped like `EnableCommitments`; `LeakPass`; `CaughtDoubleDealing`; `EavesdropLine`;
  - `PactOfPair` in `StorySystems.cs`;
  - hooks at `StoryHooks.cs:366-387`, `StoryEffects.cs:161-184`, `StoryPlays.cs:140`, `StoryCatalog.Plays.cs:49` and `EpisodeEngine.cs:1344-1352`.
- **Readers:** `AllianceRead` (listen-in evidence, both whisper forms, `Pact.exposures`, the risk word).
- **HUD:**
  - **One edit for all four new kinds (X6).** `EventTint`/`EventGlyph` (`Run/Episode/EpisodeHud.Chrome.cs:1400-1431`) gain `double-dealing` (conflict colour), `sighting`, `overheard` and `pact-plan`.
  - **Alliance card order (X6):** members → `CallLine` → [D3 `PlanLine`] → exposures → risk word. These are lines, not controls.
- **Enable, in D4-4:** the director, importer and `PortVerification` lines (§0.3).

### 2.5 Edge cases
- **Reload.** Leaks and reactions land in one eviction-night commit, and a listen-in is one command. A reload before the commit replays the same coin. The NPC clock path (`Sim/EpisodeNpcSocial.cs`) touches no facts.
- **Membership.**
  - A bring-in becomes a member and knower, and next week's `m` and `k` change.
  - A leaver never reacts, because they learned as a member.
  - If the player leaves P2, later learners do not react.
  - After a rename the old line no longer matches the card, the same gap `IsLeftGoesOnLine` accepts.
  - Ended pacts stop rolling and trigger nothing, but their whispered facts still spread.
- **Evictions.**
  - `m` counts Active members only.
  - Evicted knowers never tell (`StorySystems.cs:323-324`).
  - Once the player is out there is no juggling term and no double-dealing, while NPC pacts keep leaking.
- **House size.**
  - Small houses need at least 4 Active and an outsider. Stories end at the final three.
  - At 16 the pass is linear in pacts, adds no facts, and the 128-fact ceiling feels no pressure.
- **A pair in two pacts.** The resolver grants one and the other stays dark. "Turn it on them" (`StoryCatalog.Staging.cs:170`) ends both first, so the ended-pact fallback picks the oldest, and the receipt names the same pact.
- **Story-made pacts** roll like any other, including the Staged Feud's (Q6).

### 2.6 Tests and acceptance
**Pure (`AllianceLeakTests`, also in the subset):**
- the odds table, the cap, and NPC pacts never taking J;
- a leak equals `StoryRandom.Unit(key) < odds`, with `randomState` and `nextSequence` unchanged;
- only Private, eligible facts roll, and `fact.week == s.week` never rolls;
- rules off: nothing flips, both lines keep their old text, and the `AllianceReadTests.cs:183-210` pin stays as the rules-off case. A rules-on twin flips it to one pact;
- with the commitment rules off but the start week set: `On` is false;
- the whisper names everyone, and V3 dates both forms;
- **listen-in:**
  - grants for a pair-only pact;
  - grants **nothing for two members of a trio**, for the player's own pact, or when caught;
  - the same `randomState` and id count as with the rules off;
- **double-dealing:**
  - both paths produce byte-identical player-audience text;
  - once per (X, P2);
  - nothing for a member, an ended P2, an evicted player, or an X who knew before allying;
  - no NPC↔NPC relationship row changes;
  - the receipt is asserted on the ledger (−10), not on the scaled view (D4-L3);
- the risk word is equal for two pacts that differ only in visibility;
- **a hearing-rules-on run** (`unifiedHearingRulesVersion` set, as fresh seasons are, `EpisodeDirector.Season.cs:293`), so `Spread` runs on its staged clone (D4-L2). `House()` fixtures run with hearing off.

**Persistence:** W28's tests, plus validation refusals for records with the rules off and for records dated before the start week.

**Harness:** `AllianceLeakSeasonDigests` (`[Explicit]`, compiled out of Unity, over the 54 seasons `CommitmentRulesSeasonDigests` uses).
- With the rules off, every digest is byte-identical.
- With the rules on, it counts and asserts:
  - every leak recomputed from the eviction-night state;
  - spreads, receipts and grudges, each with one line;
  - listen-in grants;
  - zero NPC↔NPC changes from D4.
- It reports rates by house size (6, 8, 12, and 16 on the combined roster) and for a scripted three-pact player.

**PlayMode:** `Alliances_DoubleDealingAndListenIn` at FontScale 1.0 and 1.2, 16:9 and 4:3, with labels at least 1.3× their font. Captures: `alliances-double-dealt`, `alliances-overheard`, plus `-large`.

**Acceptance:**
- rules-off digests are identical;
- every rules-on leak matches its coin;
- every NPC view or grudge change from D4 comes with a line to the player;
- V3 never shows a pact without a line or fact;
- the over-grant flips under the rules;
- EditMode, persistence, PlayMode and the offline compile are green, and the floors are raised.

### 2.7 Slices
| # | Slice | Effort |
|---|---|---|
| D4-0 | Pure `AllianceLeaks.cs` and `PactOfPair`, unwired, Mono harness (now) | S |
| D4-1 | One pair one pact; the whisper names everyone; the AllianceReadTests pin split | S |
| D4-2 | Listen-in grant (exact-pair rule) and the shared `EavesdropLine` | S |
| D4-3 | Weekly leak | S-M |
| D4-4 | Double-dealing at both knower paths, the receipt, the one sentence, the HUD kinds edit, `Pact.exposures`; the enable lines | M |
| D4-5 | Evidence: digests, PlayMode, captures, floors, build log, tell the story session | M |
| D4-6 | Optional, last: the risk word, YourWeek's double-dealing lines | S |

Total M-L, about 6–8 days.

---

## 3. D3: negotiated alliance meetings (the war room)

### 3.1 Goal
B3: a pact of three or more meets once a week. Members suggest targets from their own projections; the player agrees, counters once, or lies low. The agreed plan becomes the week's call, and follow-through shows on the alliances page.

### 3.2 Existing code reused
- **C6 meeting** (`Sim/EpisodeEngine.AllyIntel.cs`):
  - `HoldAllianceMeeting` (`:193-226`) takes the pact from `c.text` (`:195-198`) and guards it with `MetThisWeek` (`:202`). The player's `Change` rolls are at `:207`, the claim at `:219-222`, the cooldown at `:224` and the `Log` at `:225`.
  - Helpers: `SharesIntel` (`:92-98`), `MeetingKey`/`MetThisWeek` (`:111-115`), `MeetingPact` (`:123-127`), `AtTheMeeting` (`:130-132`), `MeetingSpeaker` (`:154-165`).
  - The memory never names a nominee (`:181-185`).
- **Director:** `AllianceMeetingRow` sends `text: pactId` (`Run/Episode/EpisodeDirector.RoomActs.cs:88-96`, commit at `:94`); the pill is at `:102-103`.
- **Levers** (`Sim/EpisodeEngine.Levers.cs`):
  - obligation sizing (`:17-20`, `:55-73`);
  - `CallThisWeek` (`:96-97`);
  - `CallTheVote` (`:107-140`): it requires Campaign and `VoteRead.Available`, and decides followers with `!Allegiance.Lapsed && Complies(…, Roll)` (`:131`).
- **Allegiance** (`Sim/Allegiance.cs`): `Lapsed` reads the true state (`:138-142`); `LapsedMembers`/`Following` (`:218-235`); `AsThePlayerKnows` (`:300-309`).
- **Bloc round** (`Sim/WebVotingBlocs.cs`): `Resolve`'s called-pact guard (`:89-118`, guard `:91-92`); `FromNative` builds members from `Following` and the call from `CallThisWeek` (`:216-252`, call `:228-235`).
- **Evaluator:** `WebEvictionVoting.EvaluateNative` (`:289`) returns `selectedNomineeId` and `margin` (`:317-322`) and draws nothing; its tie-break is keyed.
- **Call records:** `BlocCallRow` and `ledger.calls` (`Sim/SeasonLedger.cs:31`, capped at `MostRows` 512); its readers are `GameSense.cs:213-218`, `CommitmentsRead.cs:325`, `FinalArgument.cs:154` and `Betrayal.cs:57-72`.
- **C7 counter card:** `Run/Episode/EpisodeDirector.Negotiation.cs:49-65`. It lapses at the next line the player hears (`Sim/Negotiation.cs:125-131`).

### 3.3 Rules
**Gate (X8).** `PactPlanRulesOn(s)` = `pactPlanRulesStartWeek ≥ 1 && week ≥ it && CommitmentRulesOn && LeverRulesOn`. With it off, C6, `CallTheVote`, the bloc round and every page are unchanged.

**When a war room is held.** It is C6's `AllianceMeet`, through a member, in a private room, spending the window's action. It is a war room when all of these hold:
- (a) `VoteRead.Available(s)`, which means the Campaign with two nominees;
- (b) at least 3 of the pact are in the house, the player included;
- (c) at least one NPC member votes this week;
- (d) the pact has no plan row and no `BlocCallRow` this week;
- (e) at least one member present has a say.

Otherwise the meeting is C6's.

**Engine-side once-a-week guard (D3-M1).**
- Under the rule, `HoldAllianceMeeting` refuses a pact of 3+ unless `phase == Campaign && VoteRead.Available`, whatever `c.text` names: "{Pact} meets once the block is set." The candidate is discarded, so nothing is spent.
- `MeetingPact` also skips such pacts, so the director never offers them.
- The plan row is the attempt row ("once-a-week guards count attempts"). The C6 cooldown stays as well.

**No unilateral call for 3+ pacts** (pending Q2). `CallTheVote` refuses a pact of three or more: "{Pact} settles its call when it meets." Its rows are not offered. Pairs keep it.

**The says.** For each member present (`AtTheMeeting`), in pact order:
- members who answer as allies (`SharesIntel`) have a say:
  - a voter or the HoH says `EvaluateNative(s, id).selectedNomineeId`, their own lean without the bloc;
  - a nominee says the other nominee;
- the rest keep quiet.

Nothing is drawn. C6's single ally claim is not written at a war room (Q8).

**The agreed target (owner decision; one wording, D3-L4).** It is the nominee backed by a majority of the says cast by the members present. Quiet members, and a player lying low, cast none. With two nominees that means more says.
- **The members' plan M** is the nominee with more NPC says.
- **A split** is equal NPC says.

**The answer: `AnswerPactPlan` (62).** `targetId` is a member in the house; `text` is the pact id; `secondTargetId` is a nominee, or empty to lie low.
- **Cost:** free.
- **Requires:** the rule, `RequireConversationWindow` (`Sim/EpisodeEngine.cs:1215`), `phase == Campaign`, `VoteRead.Available`, an open plan for that pact this week, and the player in the pact and in the house.
- **Refused before anything is spent:** a second answer ("{Pact} has settled this week's plan.").
- **Agree** (M, or either nominee on a split): target set, caller = player. Never offered when the target is the player.
- **Counter** (once; the nominee M did not pick; only when M exists and that nominee is not the player). Each member who said M comes round iff all of these hold:
  - they are not a nominee;
  - `!Allegiance.Lapsed(Allegiance.AsThePlayerKnows(s), id)` (**D3-H2**). This gate reads only what the player can know, so a hidden-ballot betrayer behaves like anyone else; `Following` still drops the truly lapsed at the reveal (`WebVotingBlocs.cs:231`);
  - `StoryRandom.Unit(s, "w{week}:pact-counter:{pactId}:{id}") < ComeRoundOdds`, never `Roll(s)`.

  The odds:
  - `ComeRoundOdds = reach ≤ 0 ? 0 : clamp01((reach − margin) / reach)`;
  - `reach = 8 × clamp(Score(id→player)/50, 0, 1) × (Loyal 1.5 | Sneaky 0 | 1)`, at most 12;
  - `margin = EvaluateNative(s, id).margin` at answer time.

  **The tally:** the player, plus those who came round, plus those who already said C, against what is left for M. More says wins.
  **A tie** goes to the founder's side: the player when `PlayerFounded`, else the NPC founder's final say. With no founder present, M stands.
- **Lie low:** the target is M. On a split it is the NPC founder's say, else the first say in pact order. The caller is an NPC.

**Bound members and dissenters.**
- **Bound:** every NPC whose final say is the target. They go into `followed` with no draw. Bound means they get the call's directive (`blocPressure −40`, `WebEvictionVoting.cs:568, 581`), not a fixed ballot.
- **Dissenters** go along iff `!Lapsed(AsThePlayerKnows(s), id) && Complies(Loyalty(FromNative(s), pact, id, caller, target, ProxyTrust), traits, coin)`. The coin is `"w{week}:pact-plan:{pactId}:{id}"`. Using the same known-view gate for dissenters extends D3-H2 for the same reason.
- Dissenters who don't go along are written only to the plan row, **never to `defected`**, which C2's betrayal check, C3 and Your word read.
- The player is never bound.

**The week's call.**
- **Player-backed plan:** `BlocCallRow {callerId = player, followed, defected = []}`.
- **NPC-led plan:** no `BlocCallRow`, because readers assume every row is the player's. `FromNative` reads `PlanCallThisWeek` when `CallThisWeek` is null. The caller is the founder if bound, else the first bound member. If the caller is not in `Following` at the reveal, or the target has left the block, `Resolve`'s guard falls back to the founder's round.
- **GameSense (D3-L1).** A plan-backed row scores `followed×2 − (present voters not in followed) + 6 if the target went`. It ships with the engine slice, so plan rows never inflate the score.

**Lapse at campaign close.**
- After `PassTheReadings` (`:353`), every open plan settles as lie-low with the same coins. One `pact-plan` line goes to the pact: "You let {Pact}'s plan stand: evict {Name}, as {names} wanted."
- If no NPC say remains, or the pact has ended, the plan is void: no target, no call.
- The frozen `campaign-close` sentence is untouched.

**Knowledge gate.**
- **The meeting line** (kind `conversation`) is C6's prefix plus the says, with no numbers: "Riley Chen and Sam Ortiz want Maya Hassan out; Jo Park wants Alex Moore out; Kim Lee kept quiet."
- **The plan card** opens any conversation with a member who was at the meeting. It is drawn **after** C7's counter card (D3-L3), because the counter lapses at the next line the player hears, including this answer, while the plan stands until the close. It shows the heading "{PACT}'S PLAN", each say, and each member's whip word from `VoteRead.ReadVoter` on `AsThePlayerKnows` (`Levers.cs:169-174`): firm, leaning, torn or unknown.
- **The settlement line** (kind `pact-plan`): "You pushed for Alex Moore. Sam came round; Riley held to Maya Hassan."
- **No outcome is certain.** Every outcome rests on a coin over a known-view gate, so no single refusal tells the player anything for certain.
- **Follow-through** is judged only by ballots the player can place (`KnownBallots.cs:162, 175`).

**Saved state.** `pactPlanRulesStartWeek` and `ledger.plans` (W28):

```csharp
[Serializable] public sealed class PactPlanRow {
    public int week; public string allianceId, throughId;
    public string stance = "open";            // open | agreed | countered | low | lapsed | void
    public List<string> present, cameRound, followed; public List<PlanSay> says;
    public string counterId, targetId, callerId; }
[Serializable] public sealed class PlanSay { public string memberId, targetId; }
```

**Validation:**
- ids and the pact `Text(…,160)` are well formed, and the stance comes from the fixed set;
- a member says at most once, and `followed` and `cameRound` are subsets of `present`;
- at most one row per (week, pact), and only in weeks at or after the start week;
- **an `open` row exists only when `phase == Campaign` and `week == s.week` (D3-M3).** Social is phase 0, so "phase ≤ Campaign" would have admitted the post-eviction Social;
- each player-backed row has exactly one matching `BlocCallRow`, and every `BlocCallRow` in a planned pact-week belongs to its plan.

**Determinism.**
- D3 adds zero `Roll(s)` calls. A war room draws exactly what C6 draws: the player's `Change` rolls and `StoryConversation`.
- Agree, counter and lie low leave identical `randomState`.
- One line is logged per war room, per answer and per lapse.
- With D2 on, the beats fired after a war-room tick move the margins the answer reads. That is deterministic, and tested.

### 3.4 Integration points
- **Engine:**
  - new `Sim/PactPlans.cs` (pure): `Says`, `MembersPlan`, `CounterReach`, `ComeRoundOdds`, `Settle`, `CardFacts`, the lines;
  - new `Sim/EpisodeEngine.PactPlans.cs`: `EnablePactPlans`, `PactPlanRulesOn`, `AnswerPactPlan`, `PlanCallThisWeek`, the lapse;
  - `AllyIntel.cs` (the war room branch and the guard), `Levers.cs` (the `CallTheVote` refusal), `EpisodeEngine.cs` (the kind-62 case and the lapse at `:353`), `WebVotingBlocs.cs:228-235`, and `GameSense.cs:213-218`.
- **HUD:**
  - the pill `WarmthTag + " · plan"`;
  - new `Run/Episode/EpisodeDirector.PactPlans.cs` (the card, after `CounterCard`);
  - new unique captions: "Go with the pact: evict {Name}", "Settle it: evict {Name}", "Push for {Name} instead", "Lie low on the plan";
  - no call rows for 3+ pacts (`EpisodeDirector.ConversationGroups.cs:223-229`);
  - `PlanLine` after `CallLine`, and the copy "Meets once the block is set." for 3+ pacts.
- **Readers in B3's scope:** `AllianceRead.Pact.plans` and the alliances page. YourWeek, CommitmentsRead, HouseguestNotes and the GameSense copy are deferred to D3-S7 (D3-L2).
- **D1 (D3-M4).** D3 adds an NPC-caller path through `FromNative`. The D1 alliance-call design must record before S3 that it absorbs `PactPlanRow` as provenance (Q7).
- **Story:** no beat reads plans; kind 62 skips `StoryConversation`; there is no new memory; NPC pairs move only through C6's existing `Move/Record`.

### 3.5 Edge cases
- **Reload.** The open row is saved with the meeting's command, and the card redraws from it. Save → load → answer equals answer without the reload.
- **Cancellation.** Closing the card leaves the plan open until the lapse. A meeting cannot be undone.
- **Membership before the answer.**
  - A bring-in has no say and decides as a dissenter.
  - A member cut out by the free exit loses their say.
  - If the player leaves, the answer is refused and the plan lapses NPC-led.
  - If the pact ends, the plan is void.
  - `Dissolve` runs only as the social week opens.
- **Evictions.**
  - Rows stay as history.
  - A player evicted at this vote raises no `BallotBetrayals`.
  - A player evicted earlier is refused by `Social` and by the answer ("Evicted players…").
- **The player on the block with M = player:** only "Push for {other} instead" and lying low are offered.
- **The player as HoH or nominee:** they have a say. A nominee player can only back the other nominee.
- **A production removal mid-campaign** fails `VoteRead.Available`, so the answer is refused and the plan is void at the lapse.
- **Guards.** The card lives only in the conversation panel, which ceremonies and competitions never show.
- **House size.**
  - Final four: one voter, so a plan needs that voter in the pact.
  - Final three: no campaign, no plans.
  - Pacts are capped at 4 (`GrowPacts.cs:83-85`), and the player at three pacts, so at most three war rooms a week.

### 3.6 Tests and acceptance
**Pure (`PactPlanTests`):**
- rules off: C6 unchanged, 62 refused before cost, draw or log, `CallTheVote` unchanged;
- the says equal `EvaluateNative`; a nominee member says the other nominee; a cold member or a known betrayer keeps quiet;
- majority and splits, and lie-low on a split;
- agree writes the row; bound members carry −40 in `ProjectBallot`; a dissenter is not flagged by `BallotBetrayals`;
- **counter:**
  - its odds;
  - Sneaky members and members the player knows have lapsed never come round;
  - **a hidden betrayer gets the same odds and gate result as an identical non-betrayer (D3-H2)**;
  - the tie rule; a second answer refused;
- **an engine-side refusal of a named 3+ pact before the block is set, with `c.text` = its id (D3-M1)**;
- lie low and the lapse; **the lapse runs after `PassTheReadings`**;
- **`ProjectBallot` with an NPC-led plan, in the campaign and at the eviction**;
- determinism: equal `randomState` across all three answers; one `Log` each; save/load/answer;
- **D2 beats between the meeting and the answer** (with D2 on);
- once a week with 256 cooldowns full;
- validation of every invariant, including open rows outside the Campaign;
- no memory names a nominee;
- the card and page facts hold no number;
- GameSense points for a plan-backed row;
- the kind sweeps.

**Harness:** `PactPlanSeasonDigests`. With the rules off, digests are byte-identical. With them on, a busy player on a fixed policy, and constructed trio fixtures (trios are rare: C5 saw 8 joins in 43 askings, plan `:788`), check legality, the counts and zero extra season draws.

**PlayMode:** `EpisodePlayModeTests.WarRoom`.
- the pill in the campaign, and no meeting row for a trio before the block is set;
- the controls, found by caption;
- counter → line → the card is gone;
- the page line;
- reload with an open plan;
- captures `conversation-war-room`, `-large` and `-4x3` at FontScale 1.0 and 1.2, with the longest names, in houses of 6, 8 and 12.

**Acceptance:**
- in a fresh season, a trio's war room yields a plan whose bound members carry the directive at the reveal;
- dissenters are never flagged;
- the page shows the says, the stance and follow-through by known ballot;
- rules-off digests are identical, and D3 adds zero season draws.

### 3.7 Slices
| # | Slice | Effort |
|---|---|---|
| D3-S0 | Measure trios per campaign week (scratch harness, now) | S |
| D3-S1 | Pure `PactPlans` and Mono-harness tests (now) | M |
| D3-S3 | Engine: war room branch, guard, kind 62, counter (known-view gate and coin), dissent coins, lapse, the `CallTheVote` guard, `FromNative`, `MeetingPact`, GameSense points | M |
| D3-S4 | On screen: pill, card after the counter card, captions, call rows hidden, fit tests | M |
| D3-S5 | Follow-through: `AllianceRead.plans`, `PlanLine`; the enable lines | S-M |
| D3-S6 | Evidence: digests, PlayMode, captures, floors, tell the story session | S-M |
| D3-S7 | Optional, deferred: YourWeek, CommitmentsRead, HouseguestNotes, GameSense copy | S |

Total M-L.

---

## 4. D2: all-week NPC strategy

### 4.1 Goal
B2: spread the house's turns over all four windows of every week, fire them on the season's own steps, put each in a room, and give the player a feed of what they witnessed: sightings, overheard lines and walk-ins. Under the owner's decision there is no offline catch-up. Every beat fires inside a committed command.

### 4.2 Existing code reused
- **The turn pass.** `NpcSocialActions.Settle` (`Sim/NpcSocialActions.cs:137-167`):
  - autonomy gate `:139`, `Dissolve` `:140`, the Have-Not test `:151`, `ActionsPerSocialPhase` = 3 (`:43`).
  - It is called once, at `EpisodeEngine.cs:383`.
- **The verbs.** `Talk` `:244`, `Pursue` `:263`, `Meet` `:336` (which skips the player's pacts, `:340`), `SpreadInfo` `:370`, `Confront` `:419`, NPC `Eavesdrop` `:459`.
  - Season draws: `:299`, `:401`, `:466-470`, `:506`, `:516`.
  - Reply cards: `:211`, `:303`, `:405`, `:440`.
- **Writes.** `Act` (`:527-537`). `Change`/`ChangeWithRoll` are at `EpisodeEngine.cs:2257-2291`.
- **Roll-free passes.** `NpcAlliances.TryPropose` (`Sim/NpcAlliances.cs:230-250`, never the player), `NpcPromises.TryGive` (`Sim/NpcPromises.cs:113-127`, never the player), `Dissolve` (`NpcAlliances.cs:304`), `CourtTheHoH` (`Sim/EpisodeEngine.Agency.cs:128-138`), and the campaign pass (`NpcSocialActions.cs:177-215`, at most 2 visits × 2 nominees).
- **Windows** (`Sim/EpisodeEngine.Week.cs`):
  - `Windows` `:7-12`, `EnableWeek`/`WeekRulesOn` `:26-33`, `Window` `:49-61`;
  - `WindowSeats` `:68-81`; AfterEviction's seats depend on the active count (`:75-78`);
  - `SpendInWindow` `:120-126`.
- **Conversation windows.** Under the week rules, every word said to somebody is open in every window. **Listening in is open in Social and Campaign** (`Sim/EpisodeEngine.Negotiation.cs:32`; `CanListenIn`, `Run/Episode/EpisodeDirector.NpcSocial.cs:55-66`).
- **Witnessing shape.** `WitnessProximity` (`EpisodeEngine.cs:145`, `:1662`); walk-in geometry (`Run/Episode/EpisodeDirector.Conversation.cs:422-457`); scene staging (`HouseMeetingCoordinator.SceneStaging.cs:25-75`); wander guards (`EpisodeDirector.Wander.cs:37-43`).
- **Feed readers.** Recent events (`EpisodeHud.Chrome.cs:1302-1341`), the story page (3 weeks, `EpisodeDirector.Journal.cs:170`), the recap.

### 4.3 Rules
**Gate (X8, D2-M2).** `AllWeekOn(s)` = `allWeekRulesStartWeek ≥ 1 && week ≥ it && WeekRulesOn(s) && NpcSocialState.AutonomyHasBegun(s)`.
- `EnableAllWeek(s, fromWeek = 1)` clamps to `[1, week+1]` and requires `1 ≤ weekRulesStartWeek ≤` that value. It does not require `WeekRulesOn`, because the importer's `EnableWeek(week+1)` (`WebSaveImporter.cs:209`) leaves that false at import time.
- With D2 off, `Settle` runs at `:383` exactly as before.

**Quota and rest.**
- Each active NPC gets 3 beats a week, or 2 as a Have-Not.
- With `i` its index in `s.contestants`, an NPC rests in window `(i + week) mod 4`. A Have-Not also rests in `(i + week + 2) mod 4`.
- **No beats:**
  - on move-in night (`IsFirstNight`);
  - where `Window(s) == None`: the HoH competition, Eviction, the finale phases and the Jury;
  - with fewer than 2 active NPCs.
- **The Veto competition sits inside the AfterNominations window (D2-M1)**, but its commands are free, so it fires nothing new.

**The plan.** When a window first needs one, the engine saves:
- the non-resting active NPC ids, shuffled by Fisher-Yates on `StoryRandom.Stream(s, "allweek:order:{week}:{window}")`;
- `(beatWeek, beatWindow)`;
- `beatSeats = WindowSeats(s, W)` as a snapshot (D2-L2).

A slot whose NPC is gone, or does nothing, still counts.

**Schedule.** Tick `t = windowActions[W]`. `Due(t) = t ≥ beatSeats ? B : ceil(B·(t+1)/(beatSeats+1))`.
- Catch-up fires while `beatsFired < Due(t)`.
- Free commands do not tick: replies, reads, AskVote, the witness, the answer, `Compete`.
- **Example, with 8 in the house and about 5 beats a window:**
  - AfterNominations (1 seat): 3 beats at the opening, then 2 more after the player's conversation or at the Close.
  - AfterEviction (2 seats): 2, 2, 1.

**Close** sits at the §1 placements (`:63`, `:74`, `:225-234`, `:288`, `:314`, `:338`). It is idempotent (D2-L1). Its beats **never involve the player (D2-H3)**:
- the player is excluded from every draw and target choice;
- any rung that would aim at, or be about, the player is skipped, and the NPC moves down its ladder;
- no reply card is offered, and no gossip-discovery draw is made.

Player-involving beats fire only on ticks, where their cards live for the whole window. That matches Settle, which offered cards at the window's opening (`:383`).

**The ladder** (Settle's order, spread out). This week's `acts` is the memory:
1. **Pact:** `TryPropose`, one new pact per NPC per week.
2. **Promise:** `TryGive`.
3. **Pursue:** at most once a week, so a fixed agenda target cannot compound. A recorded `court` act fills it.
4. **Repertoire:** each kind once a week; Meet once per pact, skipping the player's pacts.

Successes are at most 3 per NPC per week (2 for a Have-Not). Standing business stays at its moment:
- `Dissolve`: under D2, `:383` becomes autonomy-gated `NpcAlliances.Dissolve` only;
- the deals;
- `NpcPromises.Settle`, `AskForTheVeto`, `CourtTheHoH`;
- the campaign pass.

Court and campaign visits are recorded as acts, with no draws.

**Randomness and ids.**
- Every draw in beat `k` comes from `StoryRandom.Stream(s, "allweek:beat:{week}:{window}:{k}")`.
- Player-involved acts pass that stream to `ChangeWithRoll` through a new internal `Change(…, Func<double> roll)` overload.
- `Log` and ledger events mint ids, which is allowed under the rule.
- NPC↔NPC moves stay on `Move/Record`.
- **D2-L5:** under the rule, `StoryConfronted`, `StoryGossipedAbout` and `StoryCampaignedTo` (`Story/EpisodeEngine.StoryHooks.cs:565-591`) pass `CurrentAnchor(s)` instead of their hard-coded anchors. Where `StrategyRules.Apply` holds they stay no-ops.

**Acts and rooms (D2-L3).**
- **Only successful acts are stored**, never `none`. Each holds `id "{week}-{window}-{k}"`, kind, actor, partner, subject, room, week, window, `firedTick`, `sighted` and `overheard`.
- **The cap is derived from the cast:** `MostActs(s) = 4·npcs + 2·CampaignVisits`, which is 64 at 16. Beats are at most `3·npcs`, court at most `npcs`, campaign at most 4, so a 16-house cannot exceed it and the validation refusal can never stall an Advance.
- **Rooms** are chosen by a keyed coin `"allweek:room:{id}"`:
  - talk, build, hold and campaign: Kitchen or Living;
  - court: HoH;
  - pact, promise, meet, rumour and hunt: Bedroom, Yard or Games (HoH when a party is the HoH);
  - confront: loud, in Living or Kitchen;
  - NPC eavesdrop and player-aimed acts: not staged.
- **An act is open** while its window and week are current, both parties are Active, and `t < firedTick + 2`.

**Witnessing: `WitnessNpcAct` (63).** The director detects it and the engine commits it, the same shape as `WitnessProximity`.
- **Detection:** same room; the pair within 3 m of each other; the player within 6 m; held for 2 s of free roam; checked at 2 Hz.
- **Command:** `targetId` actor, `secondTargetId` partner, `text` act id.
- **Cost:** free. No tick, no draw, no `Change`.
- **Requires:** `AllWeekOn`; the player Active; `Window != None`; the act open, staged and not yet sighted; matching ids; fewer than 3 sightings this window.
- **Effect:** it sets `sighted` and logs one line with audience `[player]`:
  - quiet/common, kind `sighting`: "You saw {A} and {B} talking in the {room}.";
  - quiet/private, kind `sighting`: "You saw {A} and {B} with their heads together in the {room}.";
  - loud, kind `overheard`, which sets both flags: "You heard {A} have words with {B} in the {room}."

**Paid overheard (D2-M1).** This is the existing Listen in, open in the **AfterVeto and AfterEviction windows** (Campaign and Social).
- The draws are today's exactly.
- On success against an open sighted act's pair, D2 adds the act's clause through D4's builder:
  - hunt: "{A} told {B} that {Z} has to go.";
  - rumour: "…{Z} is the biggest threat in this house.";
  - pact: "{A} and {B} agreed to work together.";
  - meet: "…were going over a plan.";
  - promise: "{A} gave {B} their word.";
  - court: "{A} was making their case to {B}."
- The pact and meet clauses are suppressed whenever D4's sentence fires.
- **D2 grants no knowledge (D2-M4, X5).**

**Walk-ins:** the existing engine, unchanged.

**Knowledge gate (D2-M3).**
- A sighting teaches who, where, which window and the class. It never teaches which act, that a pact exists, its name, a rumour's subject, or any number.
- **Co-location is already public.** "Who is where" (`EpisodeDirector.Journal.cs:214, 244`) and the live feed's room captions (`EpisodeDirector.LiveFeed.cs:126-133`) show who stands where. Neither may label an activity.
- `AllianceRead.Suspected` never reads sightings.
- An unwitnessed act produces no line, note or recap row.
- **A sighting line lapses an open C7 counter**, as any line the player hears does (`Negotiation.cs:125-131`). The watch never runs with a panel open, and a counter is answered in the conversation that raised it.

**Saved state.** `allWeekRulesStartWeek` on `EpisodeState`; `beatWeek`, `beatWindow`, `beatsFired`, `beatSeats`, `beatPlan`, `acts` on `NpcSocialState`.
- **Lifecycle:** `acts` is cleared at the week's turn. Close leaves the plan exhausted.
- **Validation, rule off:** everything is 0, empty or −1.
- **Validation, rule on:**
  - `beatWindow` is in −1..3, and `beatsFired ≤ beatPlan.Count ≤ MaximumCast − 1`;
  - plan ids are distinct non-player contestants;
  - acts: at most `MostActs`, unique ids, known kinds, parties are contestants, `room` is null or a known room, `week == s.week`;
  - flags only on staged kinds, and at most 3 sighted per window.

**Determinism and pause.**
- A beat belongs to its step's candidate, so a failed save loses it and leaves its keys unspent.
- Loading fires nothing, and nothing reads the clock.
- The season's stream never moves for a beat, so the ceremonies' draws and the player's odds do not depend on how busy the house was.
- **Caveat:** real-time NPC conversations still move scores between commands on their own stream (`Sim/EpisodeNpcSocial.cs:174-178`), and beats read those scores.

### 4.4 Integration points
- **Engine:**
  - new `Sim/EpisodeEngine.AllWeek.cs`: `EnableAllWeek`, `AllWeekOn`, `Plan`, `Catchup`, `Close`, `Ladder`, `RecordAct`, `WitnessNpcAct`;
  - `EpisodeEngine.cs`: the catch-up between `:39` and `:40`; the Close calls; Dissolve-only at `:383`; acts cleared at `:235-251`; the kind-63 case; the `Change` overload;
  - the act clause in D4's `EavesdropLine`;
  - `NpcSocialActions.cs`: the roll seam, the single-beat entry point, the `closing` flag, the court and campaign records;
  - `EpisodeEngine.Agency.cs:128-138`;
  - `EpisodeNpcSocialValidation.cs`.
- **Runtime:**
  - new `Run/Episode/EpisodeDirector.AllWeek.cs`, ticked beside `TickNpcSocialRuntime` (`EpisodeDirector.cs:413`): staging (at most 2 acts at once), the watch, guards, submit, and restaging after a load;
  - staged actors are excluded from conversation pairing, wander and the hot-tub companion;
  - new `HouseMeetingCoordinator.ActStaging.cs`, modelled on `SceneStaging`.
- **HUD:** the kinds are already tinted by D4-4. No caption changes.
- **Deferred (D2-L6, optional D2-S6):** the "Seen this week" block, the HouseguestNotes read note, the walk-in preference for staged pairs, and re-aiming the Nearby card's "Listen in" at a staged pair. Until S6 lands, paid overheard content fires only when the pair that is overheard, named or drawn, matches an open sighted act.
- **Removed:** "a war room can be a meet beat". That contradicts D3, and Meet skips the player's pacts.

### 4.5 Edge cases
- **Reload mid-window.** The plan, cursor and acts are saved, so the rest fire on the same ticks with the same keys. Stages are rebuilt, the 2 s hold restarts, and a detected but uncommitted witness is lost.
- **Stale witness.** A beat or ceremony that bumps the revision rejects a pending witness (`EpisodeEngine.cs:28-29`). Opening a panel commits an NPC tick, so revisions are read after it opens. `sighted` and the 256 accepted ids stop duplicates.
- **Cancellation.** A stage is released to ceremonies, the arena, the opening, walk-outs, talk spots and story scenes. The act restages while it is still open.
- **Guards.** The watch runs only when all of these hold:
  - no panel, diary, challenge, arena, card, tour, opening, staged ceremony or walk-out;
  - the player is Active;
  - no durable commit is in progress and recovery is not blocked;
  - `Window != None`.

  The free-time overview map gives no sightings.
- **Membership.** Verbs read the current state, so a mid-window pact change is seen at fire time. A departing party closes the act. A production removal happens at `StorySocialClose`, after the Close.
- **Evictions.** Plans never span one, and the AfterEviction plan excludes the evictee.
- **Player evicted.** No witness. Beats still fire at tick 0 and at Close.
- **House size.** With fewer than 2 NPCs there is no plan. The final three has no windows. At 16, about 11 beats fire per window, at most 3 sightings a window reach the feed, and at most 12 lines a week reach the 256-event log.
- **Week 1.** NPCs act from week 1's AfterHoH (Q11). Imported seasons play `Settle` in their import week.

### 4.6 Tests and acceptance
**Pure (`AllWeekCadenceTests`):**
- off: Settle is unchanged;
- **rule on but autonomy not begun: no beats**;
- **the importer enable at week+1 succeeds with `WeekRulesOn` false**;
- no beats on move-in night; **`Compete` in the Veto phase fires none**;
- quota and rest;
- `Due(t)` for seats of 1, 2 and 5;
- each Close before its decision; **Close idempotent across the doubled Nomination and VetoMeeting steps**;
- **no reply card, player target or player subject from a Close beat, and a tick beat's card survives to the window's end**;
- `randomState` unchanged by any beat;
- the ladder limits;
- no `relationshipArcs` from NPC-only acts;
- acts cleared at the week's turn;
- **the 16-house acts bound on the combined roster**;
- **a fresh-season configuration with hearing rules on**;
- the kind sweeps;
- replaying the same commands gives identical state JSON, including serialising after every command.

**`WitnessNpcActTests`:**
- refusals: rule off, unknown, closed, already sighted, aimed at the player, cap reached;
- the exact line words; a sighting holds no content;
- a loud act sets both flags;
- the witness is free;
- Listen in keeps today's draws and appends the clause; the D4 sentence suppresses it.

**Privacy:**
- a season with no witness has zero `sighting`/`overheard` lines;
- no D2 line names a pact;
- the directory and the feed label no activity.

**Harness:** `AllWeekSeasonDigests` (`[Explicit]`, houses of 6, 8, 12 and 16).
- With the rule off, digests are identical, through the 28 observer.
- With it on: every command stays legal, plus per-window counts, acts by kind, pacts per season, and drift.
- `NpcAgencyHarnessTests` is re-measured; NPC-only pacts should stay near 1.2 a season.

**PlayMode:** pin `Time.captureDeltaTime` and `runInBackground`; call `BuildNpcWorldForDiagnostics` after a later install; captures run on a screen of their own.
- one witness, one row, `sighted` set;
- none from another room, under a panel, or during a ceremony;
- a reload restages with no second line;
- "Listen in", found by caption, overhears.

**Acceptance:**
- **(a)** With the rule off, every recorded digest is identical.
- **(b)** With it on, in houses of 6–16:
  - every window after move-in night with 2 or more NPCs fires at least one beat;
  - weekly successes stay within 3 per NPC (2 for a Have-Not);
  - `randomState` is untouched by beats;
  - the mean absolute score at week 4 is within ±15% of Settle's.
- **(c) (D2-M5)** In harness seasons with no NPC world (Sim sweeps never drive it), the committed command list replays to the same hash across a save and reload at every command. With an NPC world, the claim holds only when the committed NPC operations are replayed too.
- **(d)** In PlayMode a sighting is logged exactly once with the player present, and never when absent.
- **(e)** No D2 line reaches the player for an act they did not witness.

### 4.7 Slices
| # | Slice | Effort |
|---|---|---|
| D2-S0 | Measure today's Settle by house size (now; no product code) | S |
| D2-S1 | Engine cadence: gate, plan, catch-up, Close (§1, idempotent, player-free), ladder, keyed streams, act records and cap, `Change` overload, Dissolve-only, `CurrentAnchor`, the contract note | L |
| D2-S2 | Witness and feed: kind 63, lines, act clauses in D4's builder | M |
| D2-S3 | Director staging: ActStaging, watch, guards, restaging, exclusions. Risk: act leases while conversations are paused | L |
| D2-S4 | Digests and balance; the enable lines | M |
| D2-S5 | PlayMode | M |
| D2-S6 | Optional, deferred: Seen this week, read note, walk-in preference, Nearby re-aim | S-M |

Total L.

---

## 5. Combined evidence
1. **Per-design rules-off digests.** Byte-identical recorded goldens through the 28 observer, at each design's landing.
2. **One combined digest with all three rules on**, in fresh-season configuration (story, hearing, commitment, lever and week rules on). It checks legality, zero season-stream draws from D2, D3 or D4, and no NPC↔NPC `Change`.
3. **Re-measure after D2:**
   - D4's leak and caught rates, since pacts now form all week and `fact.week < s.week` holds them back a week;
   - D3's war-room counts and its counters carried;
   - D2's drift against Settle.
4. **Event-log volume against the 256 FIFO** in a 16-house on the combined roster. The alliance evidence must survive the story page's 3 weeks (`EpisodeDirector.Journal.cs:170`). Re-tune the sighting cap from the counts.
5. **Performance.** D2's staging and the watch, on a 300 s run with 6 and 12 people plus the 16-person stress run on the combined roster. p95 ≤ 16.7 ms and p99 ≤ 33.3 ms at 1920×1080 on the GTX 1060; startup and UMA hitches are reported separately.
6. **New floors** in each merge commit: Edit, Play, UMA and pure, in `Tools/baseline.txt`. The audited pipeline runs alone, on a D: copy no other job uses.
7. **The final acceptance walk** is a genuine season, run by hand in PlayMode. It shows a sighting, a war room, a leak reaching the player, and a double-dealing line. Everything before it uses constructed fixtures (`InstallDiaryFixture`-style) for rare outcomes.

---

## 6. Open questions (each with a recommended default)
1. **Importer policy (X4).** Enable D2, D3 and D4 at `week + 1`, as the importer does commitments (`WebSaveImporter.cs:213`); migration and recovery write 0. *Default: yes.*
2. **Pacts of 3+ lose the unilateral "Call the vote" and their free-time meetings (D3-L2).** These are behaviour cuts. *Default: yes; the war room is their call and their weekly meeting.* If the owner declines, free-time meetings stay C6 under a separate cooldown key, and `CallTheVote` stays open until a plan row exists.
3. **"Majority of members present" (D3-L4).** Quiet members and a player lying low cast no say; ties go to the founder's side. *Default: as written.*
4. **The juggling term's reach.** *Default: it applies to each secret pact the player is in; NPC-only pacts take the base odds.*
5. **A listen-in on two members of a bigger pact (D4-M2).** *Default: grant nothing unless the in-house members are exactly the pair.* Widening it would name and grant everyone.
6. **D4 details.**
   - Exempt the Staged Feud pact? *No.*
   - Copy the web's stability term? *No.*
   - Instant public exposure? *No; whisper.*
   - Do NPC pacts leak? *Yes, at base odds.*
   - Numbers: receipt −10 permanent; grudge 40 (48 while allied).
   - Does X resent P2's other members? *No.*
   - Does double-dealing count in "Your word"? *No.*
   - A risk word on the player's cards? *Yes, optional, last.*
   - Keep the 0.5 cap? *Yes, as a guard.*
   - The boundary is its own root field, not `StoryRules.Leaks`.
7. **D3 lands before D1's alliance-call family (D3-M4).** *Default: proceed. The D1 design records before D3-S3 that it absorbs `PactPlanRow` and the NPC-caller path, under B1's ratified "separate policies, one record".*
8. **D3 details.**
   - Is a bound member voting against a C2 betrayal? *No; tracked on the page.*
   - Dissenter rule: *known-view gate plus `Complies` on a keyed coin.*
   - Counter odds: *`clamp01((reach − margin)/reach)`, re-measured in D3-S6.*
   - Does the player's own ballot cost anything? *No.*
   - Do NPC-only pacts convene themselves? *No.*
   - Nomination-week planning? *Out of scope.*
   - Does the C6 claim survive at a war room? *No.*
   - NPC-only war rooms? *No.*
   - Who may answer the card? *Any member who was at the meeting and is still in the pact and the house.*
9. **Schema.** *One shared bump, 28 (§0.3)*, with the fallback of 28/29/30.
10. **The C7 counter lapsed by a sighting line.** *Accept, as with any line the player hears.*
11. **D2 details.**
    - Beats from week 1's AfterHoH? *Yes.*
    - Campaign visits stay at the opening, recorded? *Yes.*
    - NPC deal-making as beats? *No.*
    - The live-feed camera on staged acts? *No.*
    - Volume: *3 sightings a window, 2 staged acts at once.*
    - Sightings from the overview map? *No.*
    - Free commands tick? *No.*
    - Rest rotation: *`(i + week) mod 4`.*
    - Keyed streams? *Yes.*
    - Keep the real-time scheduler and measure the drift? *Yes.*
    - Exclude the player's conversation partner from that step's beat? *No.*

---

## 7. Who owns which shared file
| File | Owner | Others |
|---|---|---|
| `EpisodeEngine.cs:1352` eavesdrop line | D4 (`EavesdropLine`) | D2 adds its act clause |
| `EpisodeHud.Chrome.cs:1400-1431` tint and glyph | D4-4 (all four kinds) | none |
| `EpisodeDirector.Alliances.cs:56-124` | D4 (exposures, risk) | D3's `PlanLine` slots before them |
| `EpisodeState.cs`, migrations, frozen, digests | W28 | none after it |
| `EpisodeEngine.cs` Advance (`:225-397`) | D2 (Close, catch-up, `:383`) | D3 lapse at `:353`; D4 at the anchors |

## 8. Corrections applied (critique → section)
- **X1:** kinds 62/63 (§0.3).
- **X2:** one schema, 28 (§0.3).
- **X3:** the digest moves through a 28 observer (§0.3).
- **X4:** the importer enables at week+1 (§0.3, Q1).
- **X5:** D4 owns the line builder and the grant (§2.3, §4.3).
- **X6:** card order and one HUD edit (§2.4, §7).
- **X7:** the week's order (§1).
- **X8:** the gates (§2.3, §3.3, §4.3).
- **D2:**
  - M1: Listen-in windows; the Veto sits in a window (§4.2, §4.3).
  - M2: enable precondition (§4.3).
  - H3: Close beats never involve the player (§1, §4.3).
  - M3: co-location is public (§4.3).
  - M4: no grant (§4.3).
  - M5: acceptance (c) scoped (§4.6).
  - L1: idempotent Close (§1).
  - L2: `beatSeats` (§4.3).
  - L3: acts cap (§4.3).
  - L4: the "Logged LAST" contract (§1).
  - L5: `CurrentAnchor` (§4.3).
  - L6: the meet-beat claim removed and S6 deferred (§4.4).
  - Missing tests (§4.6).
- **D3:**
  - H2: known-view gate and coin (§3.3).
  - H3: the digest (§0.3).
  - M1: engine guard (§3.3).
  - M2: importer (Q1).
  - M3: open rows only in the Campaign (§3.3).
  - M4: D1 (§3.4, Q7).
  - L1: GameSense (§3.3).
  - L2: deferred readers (§3.4, Q2).
  - L3: card order (§3.3).
  - L4: one wording (§3.3, Q3).
  - Missing tests (§3.6).
- **D4:**
  - M1: one sentence (§2.3).
  - M2: exact pair (§2.3, Q5).
  - M3: the gate (§2.3).
  - M4: the builder (§2.3).
  - L1: `fact.week < s.week`, re-measured (§2.3, §5).
  - L2: hearing-on test (§2.6).
  - L3: ledger assert (§2.6).
  - Scope: risk word and YourWeek last (D4-6).

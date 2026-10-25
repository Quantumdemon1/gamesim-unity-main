# Dialogue: conversations that matter (dialogue rules 2)

Design note D0, 2026-10-25. Docs only: it changes no code, save, test or rule.

**The owner's instruction, 2026-10-25.** About dialogue V2 the owner said: "Fix them so they work and implement these changes now." This plan therefore goes ahead on the defaults in §1, recorded here as the owner's instruction to implement now with these defaults. Each default stays open to the owner's change, and §1 says what changing it costs. Shipped seasons stay on dialogue rules 0 until D7.

**Evidence base.** Branch `claude/dialogue-v2`, cut from main `b208d46d`. That commit includes role moments R0 to R6 and R5b: `StoryRules.Roles` is 11 and `RolesOn` exists, while `StoryRules.Current` stays at Threads (9). Code paths are relative to `Assets/Gamesim/`. "The ROLE doc" means `ROLE_DIALOGUE_ECONOMY_20261012.md` at the repository root, and "role moments" means `Assets/Plans/ROLE-MOMENTS-PLAN.md`.

**Names.**
- *Dialogue rules 1* is the pilot merged in PR #28. Its two content versions are V1 (a personal appeal) and V2 (citing a public rescue). Earlier notes call this pilot "dialogue V2".
- *Dialogue rules 2* is what this plan builds: the ROLE doc's consequences, conditional veto, role grants and conversation time.

---

## 0. The short version

- **Today.** Rules 1 is one three-step conversation.
  - When the player is nominated and the campaign is on, the Emotional "Ask for their vote" opens "Make your case". The next step is "Appeal personally", or "Cite their public rescue". The last is "Make the plea", "Keep their vote open" or "Leave it there".
  - It costs one action. Its plea is the ordinary plea's roll, and every ending gives zero time (Simulation/EpisodeEngine.Dialogue.cs:177, 199; Simulation/DialogueRules.cs:274-280).
  - No shipped season can reach it, because `ShippedRules.ApplyFresh` never sets `dialogueRulesVersion` (Simulation/ShippedRules.cs:26-64).
  - Schema 29 already holds empty storage for everything else the ROLE doc asks for (Simulation/DialogueState.cs).
- **D1, now.** A toggle on the cast screen, off by default, starts a season on dialogue rules 1. To be plain about it: rules 1 adds no new consequence. It is a three-step form of a plea the game already has.
- **D2 to D5.** Rules 2, built inside the schema-29 store. Every field it needs is already there (§5), so there is no schema 30. Grants, consultations and time form one economy with role moments (§3).
- **D6 and D7.** Measurement, run after role moments' B9, and then rules 2 on by default in new seasons.

## 1. Decisions

The table below records the owner's 2026-10-25 instruction ("implement now") with these defaults. Each one is open to the owner's change.

| # | Decision | Default adopted | If the owner changes it |
|---|---|---|---|
| 1 | How the merged pilot becomes playable | An opt-in on the cast screen, **off by default** (D1). The same toggle later selects rules 2. The shipped default stays 0 until D7. | Turning it on by default now reverses OWNER-DECISIONS-2026-10-22 §5 ("stay on dialogue rules 0"), and the lab goldens' rules tuple must be re-recorded. |
| 2 | Grants and role moments | **One economy.** A dialogue role grant replaces a paid entry and never stacks with a paid plea (the owner's AP-0 rule, OWNER-DECISIONS §4). Grants are entered from the role-moment surfaces (§3). | A separate grant menu would be a second economy beside role moments. |
| 3 | What records a conditional veto promise | `conditionalVeto` on a unified commitment row. The fields are already in schema 29, so there is **no schema 30**. Every reader of a veto word counts the row as one word (§3.1). | A conditional `veto_use` deal in `state.deals` probably needs a new field, which means schema 30. |
| 4 | The ROLE doc's numbers | Unmeasured starting values (§4.4), tuned only through measurement (D6). | - |
| 5 | Randomness | New dialogue outcomes draw **keyed coins** (`StoryRandom`), never the season's main stream, so starting or abandoning a conversation never re-rolls the season. The final plea keeps its existing main-stream roll. | Main-stream draws would re-roll every later event in a season whenever a conversation is added or dropped. |
| 6 | B9 | Dialogue's B9 is a **successor** to role moments' R7, not a joint run. | A joint run would hold back the built role moments until dialogue is built. |
| 7 | Schema | Rules 2 stays inside the schema-29 `DialogueState` store. If a needed field truly does not fit, the work **stops and reports**. It does not add schema 30. | Schema 30 means a large frozen schema-29 reader, a number claimed together with the story session, and a set qualified on its own. |

This note also makes the calls below under those defaults. They are open to the owner too.

| # | Call | Recommended | Alternative |
|---|---|---|---|
| 8 | When the toggle moves from rules 1 to rules 2 | When D5 lands and rules 2 is complete. D3 to D5 each qualify as usual, and until then they play only in tests. A saved rules-2 season therefore never holds a half-built rules 2. | Move it at D3, so each slice is playable as it lands. Every save of a half-built rules 2 must then keep loading as later slices land: grants are written when used, not when the role is won, and every later change gets a new content version. That means more validator branches. |
| 9 | Rules 2 needs role moments | Rules 2 requires `RolesOn` (story rules at Roles or later), because its grants hang on the role-moment boards. A season the toggle starts on rules 2 also plays the role moments. Shipped seasons are not affected, and R7 still enrols them. | Rules 2 without role moments. The grants would then need entries on the older rows as well, so the entries get built twice. |
| 10 | What the veto question costs | "If you win the veto, will you use it on me?" costs the window's action, and no grant pays for it. The ROLE doc's Nominee Appeal covers the plea to the actual holder and the campaign, not this question. | Let the Nominee Appeal grant pay for it. |

## 2. The ROLE doc against today's code

| ROLE doc delivery step (:148-154) | Today | Slice |
|---|---|---|
| 1. Presentation: six categories, speaker adapters | Shipped (Runtime/Episode/EpisodeDirector.ConversationNavigation.cs:10-15) | - |
| 2a. A small campaign tree | Rules 1, with closed V1 and V2 content (Simulation/Story/DialogueCampaignV1.cs, DialogueCampaignV2.cs). Only the plea roll; zero time. | D1 makes it playable; D3 adds consequences |
| 2b. A conditional-veto statement tree | Storage only: `DialogueStatementState`, `DialogueVetoOccasionState` and `conditionalVeto`. Nothing writes them, and rules 1 refuses any row (Simulation/DialogueRules.cs:111-112, 190). | D4 |
| 3. Role grants and +1/0/-1 time | Storage only: `DialogueRoleGrantState` and `DialogueTimeReceiptState`. Rules 1 refuses grants (:120-122) and any time other than zero (:274-280). `SocialActionBudget` reads no time receipt (Simulation/EpisodeEngine.cs:796). | D5 |
| 4. Measurement after B9 | None. The balance lab never issues `StartDialogue` or `ChooseDialogue`. | D6 |
| 5. Combined native validation | Only the rules-1 PlayMode suites. There is no season-walk route and no fixture of a save with dialogue on (OLD-SAVE-ACCEPTANCE.md §7). | D1 and D7 |

D2 generalises what rules 1 hard-codes: the V1 and V2 beat ids, the single `campaign` occasion, the after-veto window and an entry paid with an action (Simulation/EpisodeEngine.Dialogue.cs:66-70, 183-199; Simulation/DialogueRules.cs:164-183, 318-321).

## 3. One economy with role moments

Role moments (story rules 11) let the player answer what comes to them at the role points of a week. Rules 2 adds the conversations the player starts at those same points. Both draw on one budget, the window's seats.

| The player is | Role moments: the answers, always free | Rules 2 adds (a conversation the player starts) | Paid by |
|---|---|---|---|
| Head of Household, before nominations | The pitch board: at most 3 pitches, answered with Promise safety, Hear them, Turn them down or Feel them out (R5) | **HoH Consultation**: one conversation with an active houseguest, opened from the pitch board or from the person's menu. A promise made in it is the pitch's own promise row, never a second one. | The HoH grant, or else the window's action |
| Veto holder, before the decision | The offer board, with one veto word a week (R2 to R4) | **Veto Consultation**: one conversation with a nominee or the HoH, opened from the offer board. It ends in the answers or price that already exist (`RespondToDeal`, or a veto for a price) and cannot give a second veto word. | The veto grant, or else the action |
| Nominated | Just a Pawn and quiet tears (R6) | **Nominee Appeal**: the plea to the actual holder, or a campaign conversation. Each ends in the ordinary plea's roll (D3). The player can also ask the veto question (D4). | The nominee grant, or else the action. The veto question always costs the action (call 10). |

**The rules** (ROLE doc :35-51; OWNER-DECISIONS §4):
- A grant pays for one newly started conversation in place of an action. It pays for nothing else, never carries over to another week, and can never be saved up.
- Each role gives at most one grant a week, and a player gets at most **two** free conversations a week, however their roles overlap. Being renominated, reloading or going to another room creates no new grant.
- A grant never stacks with a paid plea:
  - `CanLobby` allows one plea per decider per phase.
  - An independent plea to the same voter closes the conversation (Simulation/EpisodeEngine.Dialogue.cs:263-276).
  - At AP 0 without a grant, no route delivers a plea's effect. The AP-0 test (Tests/EditMode/DialoguePublicRescueV2Tests.cs:216) is extended to every kind of entry.
- Answers stay free and use up no grant. That covers incoming offers, pitches, Feel them out and story asks.
- "Answer the existing house moment first" stays (Simulation/DialogueRules.cs:52-53). A pawn talk or quiet tears is answered before a conversation can start.
- Role moments add no time (ROLE-MOMENTS-PLAN §2.3). Rules 2's +1 and -1 receipts are the only way to gain or lose conversation time. `SocialActionBudget` reads them under rules 2, and they never go through the positive-only bank (`phaseEventSocialBonus`).
- Capacity: storylines share validation's ceiling of 100, and the role-moment pool stops at 92. A conversation reserves its own house events, as rules 1 already does (Simulation/DialogueRules.cs:34-39).

### 3.1 The conditional veto (D4)

1. **The question.** The player is nominated, the veto players have been drawn, and the result is not yet known. The player asks a veto player "If you win the veto, will you use it on me?". The answer is yes, no, not committing, or a condition. It is drawn on a keyed coin inside the command and saved as the conversation's `DialogueStatementState`. It records what was said, not proof of a plan.
2. **The promise.** After a yes, "Can we make that a promise?" writes one unified commitment row:
   - a new closed kind value (`veto`) and a new origin value;
   - the speaker as maker, and the player as both beneficiary and target;
   - valid this week only;
   - `conditionalVeto` holding the statement and the player's consent.

   No `veto_use` deal is written or reused.
3. **The result.** At the veto result, `DialogueVetoOccasionState` records who holds the veto and who is on the block.
4. **Settlement.**
   - If the condition is unmet (the speaker did not win, the player is off the block, the veto cannot be used, or someone left), the row lapses with no effect and records its reason.
   - If the condition is met, the actual decision keeps the promise (the veto used on the player) or breaks it. Settlement is the veto commitment's existing one, +24 kept and -45 broken (Simulation/DealResolution.cs:54 at the critical weight), charged to whoever broke it.
5. **One word.** Every reader of a veto word counts a met conditional row as one word:
   - the holder's pull (`StrategyRules.VetoWillingness`, Simulation/StrategyRules.cs:475);
   - the Your word page (`CommitmentsRead`);
   - Your Week (`YourWeek`);
   - the jury and finale reads (`FinalistRead`);
   - `Negotiation.VetoWordGiven`, wherever the player is the maker;
   - R8's substitution, when it is built.
6. **The largest risk.** Validation and admission for the mode-2 commitment family (`UnifiedVoteFamilyValidation`, `UnifiedVoteAdmission`) must learn the third kind. If that needs a field the row does not have, the work stops and reports (decision 7).

## 4. What rules 2 is

### 4.1 The version

- **The value.** `dialogueRulesVersion` becomes 2, a new value in the existing field.
- **The constants.**
  - `Rules1` = 1 and `Rules2` = 2.
  - `Latest` = 2, the bound that validation checks.
  - `Offered` is what the toggle writes: 1 until the moment call 8 picks, then 2.
  - `Shipped` stays 0 until D7.
- **Switching on.** `On(s)` accepts both 1 and 2; today it checks `== Version` (Simulation/DialogueRules.cs:19). Each behaviour is keyed to the version that introduced it, so rules-1 seasons keep exactly the closed V1 and V2 pairs. `EpisodeValidation` accepts 2 (Simulation/EpisodeValidation.cs:91-92).
- **Prerequisites.** Those of rules 1 (Simulation/DialogueRules.cs:448-453), plus `RolesOn` (call 9), on schema 29.
- **Commands.** No new command kind. `StartDialogue` and `ChooseDialogue` carry every rules-2 conversation (Simulation/EpisodeState.cs:697-700). The next free kind stays 66.

### 4.2 The content

The content is a closed registry, keyed on template and content version.

| Content | Occasion id | Window and phase | Who the player talks to | Ends in | Slice |
|---|---|---|---|---|---|
| Campaign appeal | `week:N:campaign:npc` | After the veto; Campaign | An eligible voter | The ordinary vote plea (Emotional, or Strategic after a citation), or a vote left open | D3 |
| Holder appeal | `week:N:holder:npc` | After nominations; the veto meeting, before the decision | The veto holder | The ordinary save plea, or no ask | D3 |
| Veto question | `week:N:veto-question:npc` | After nominations; from the veto draw until the result | A veto player | A statement, and optionally the conditional promise | D4 |
| HoH consultation | `week:N:hoh-consult:npc` | After the HoH result, before nominations | Any active houseguest | Their case is heard. Any promise is the pitch's own. | D5 |
| Veto consultation | `week:N:veto-consult:npc` | After nominations; the veto meeting, before the decision | A nominee or the HoH | The answer or price that already exists; one veto word | D5 |

**How every tree runs** (the ROLE doc, :80-94):
1. The player opens.
2. The NPC gives a real response: their vote claim, a deflection, or a named concern. It is chosen inside the command.
3. The player answers with a fact they know, a real commitment, the relationship, a respectful no, or pressure. Every node has a neutral exit.
4. The NPC reacts, once.

**Limits.** A conversation has at most three consequential choices, and a node at most four options.

**Evidence** is a fact the player holds:
- a public veto record (`power`, as in V2);
- a memory;
- the NPC's own statement this week.

Hearsay never proves a ballot, and an old intention never proves today's choice. Previews show KnownOdds words, never a score.

**What replaces V1 and V2.** In a rules-2 season the campaign is the rules-2 appeal, which takes in V2's citation. V1 and V2 stay frozen for rules-1 seasons.

**Content is versioned like rules.** A slice that changes what a node does adds a content version, and a saved conversation keeps its own.

### 4.3 Randomness (decision 5)

| Draw | Source |
|---|---|
| The NPC's response at a node | `StoryRandom.Unit(s, "dialogue:{occasionId}:{pathIndex}")`. It is keyed on the occasion, not on sequence numbers, so doing something else first does not change the coin. |
| A +3 or -3 relationship change | `EpisodeEngine.ChangeKeyed`, with the same key plus `:change`. The reducer's reciprocal draws then come from the keyed stream (Simulation/Story/EpisodeEngine.StoryEffects.cs:25-27). |
| The answer to the veto question | The same rule as any response. Its occasion is `week:N:veto-question:{speakerId}`. |
| Grants, time, lapses and closures | No draw: each is worked out from the state |
| The final plea | Unchanged: the ordinary plea's main-stream roll (Simulation/EpisodeEngine.Strategy.cs:104-162) |

Starting, leaving or abandoning a conversation draws nothing from the main stream. The plea is its one main-stream roll, the same roll an ordinary plea makes.

### 4.4 Numbers (decision 4: unmeasured starting values)

| What | Starting value | ROLE doc |
|---|---|---|
| How the NPC feels about the player | +3 for an earned result, 0 for a neutral or respectful one, -3 for a clear backfire. At most one change a conversation. | :110-118 |
| Conversation time | +1 for an ending designated as earning time, -1 for one designated as losing it, otherwise 0. At most one +1 and one -1 a player a week, capped separately. A +1 is usable only in the window that is open. A -1 applies only when two opportunities remain after the conversation's cost. | :120-129 |
| Grants | One per role a week; at most two free conversations a week | :35-51 |
| Shape | At most 3 consequential choices; at most 4 options a node | :90 |
| Entry | One action at the first accepted node, or one grant. Later nodes are free. | :94 |
| Conditional promise | Unmet: 0. Met: the veto commitment's settlement, +24 kept and -45 broken | :78 |
| Occasions | One per kind, NPC and week | as rules 1 (Simulation/DialogueRules.cs:47-48) |

The numbers that already exist keep their meanings: pleas, deals, pitch answers and role-moment riders. The web reference's dialogue gives +2 to +8 and every fallback choice is positive, so it is not a model for these numbers.

## 5. Does rules 2 fit the schema-29 store?

Yes, field by field. None of it needs a new field. Three items, marked (a) to (c), need care in code.

| What the ROLE doc needs (:137-146) | Where schema 29 holds it | Fits |
|---|---|---|
| **Conversation:** occasion id and kind | `DialogueEpisodeState.occasionId` and `occasionKind` (new closed values, §4.2) | Yes |
| Template and content version | The storyline's `templateId`, and `contentVersion` | Yes |
| Player, NPC, week, window, phase | `s.playerId`; `npcId`, `week`, `window`, `startedPhase` | Yes |
| The offer or occasion it came from | `occasionId`, `entryEventId` | Yes |
| Current node and path | The owned storyline's `beatId`, `eventId` and `path`, and its house events | Yes |
| NPC responses, including a saved concern | `responses[]` and `responseKey` (a closed vocabulary for each template) | Yes |
| Evidence | `evidence[]`. `power` and `memory` keep copies; `statementId` cites a statement. | Yes, with care (a) |
| Entry, paid by an action or a grant | `entry.kind`, `grantId`, `windowActionBefore` and `windowActionAfter` | Yes, with care (b) |
| Settlement | `time`. The +3/-3 follows from the template, content version and response key, and `ChangeKeyed` writes it to the relationship and its events. | Yes |
| **Grant:** player, week, role | `DialogueRoleGrantState.id`, `ownerId`, `week`, `role` | Yes |
| What set the role | `sourceKind`, `sourceRevision`, `sourceSequence`, `sourcePhase`, `sourceNominees` | Yes |
| The window it can be used in | Fixed by the role (§3) | Yes; no field needed |
| What used it | `consumedEpisodeId`, `consumedRevision`, `status` | Yes |
| Expiry | `expiryReason`, `expiredRevision` | Yes |
| **Time receipt:** the outcome and its cap | `DialogueTimeReceiptState.proposedDelta` and `appliedDelta`. A +1 refused by the cap records proposed 1 and applied 0. The week's caps are counted from the week's receipts. | Yes |
| Window, reason, rule version | `window`, `reason`, and the season's `dialogueRulesVersion` | Yes |
| When a +1 was spent | `spentRevision`, `spentSequence`, `spentWindowAction` | Yes |
| **Statement:** speaker, audience, condition, target, time | `DialogueStatementState.speakerId`, `audienceIds`, `conditionKind`, `targetId`, `answer`, `week`, `revision`, `sequence` | Yes |
| When the statement expires | Fixed by the condition: a veto condition ends at that week's veto decision | Yes; no field needed |
| **Veto occasion:** the result and the decision | `DialogueVetoOccasionState` (holder, HoH, veto players, nominees at the result) and its `decision` (used, saved, replacement) | Yes |
| **Conditional promise** | A `UnifiedCommitmentState` row with new kind and origin values. `conditionalVeto` holds the statement, the consent's event, path and revision, and a `resolution` (reason, who left, revision, sequence, week). | Yes, with care (c) |

(a) **A cited vote claim.** An AskVote claim is a ledger row with no id, and the ledger drops rows beyond 512 (Simulation/SeasonLedger.cs:20, 61-63). A conversation that cites the NPC's vote claim therefore copies it into its own statement first and cites that copy. A conversation holds one statement, so a campaign conversation never holds a veto statement. They are separate occasions in any case.

(b) **Entry ticks.** The rules-1 validator keys an entry's tick on `windowActionAfter` (Simulation/DialogueRules.cs:177-183), which assumes one paid action. A grant entry spends no tick (`before` equals `after`), so rules 2 keys ticks by the kind of entry. Role moments named the same fix (ROLE-MOMENTS-PLAN §6.1, L2).

(c) **A third kind.** Unified rows have two kinds today, `safety` and `vote`. Adding `veto` touches the mode-2 family's validation and admission (§3.1, item 6).

**Limits D2 still has to set** (no new field):
- grants: at most 3 a week, within the 100-conversation limit;
- statements and veto occasions: at most one of each kind a week;
- the retention rule the ROLE doc asks for (:146). Nothing expired can earn again, because grants and occasions are keyed on week and role.

## 6. Slices

| Slice | What | What the player sees | Size and proof |
|---|---|---|---|
| **D0** | This note | Nothing | S. Docs only. |
| **D1** Toggle for rules 1 | `SeasonBuilder.Choice` gains an in-memory switch. `ShippedRules.ApplyFresh(fresh, options)` writes `DialogueRules.Offered` when the switch is set, and 0 otherwise. A toggle on the cast-screen footer, with a caption checked unused. The digest observer and `CurrentOldSaveTrace` accept rules 1 without loosening the rules-0 checks. One schema-29 rules-1 fixture saved mid-conversation, with its manifest cell. The tests that pin `ApplyFresh` move: StressHouseTests' regex, ShippedRulesTests and EpisodePlayModeTests.ShippedRules. Layouts at text sizes 1.0, 1.2 and 1.4. | A new toggle beside the house size, off. In a season started with it on, a nominated player still on the block in the Campaign gets the three-step conversation from the Emotional "Ask for their vote". "Cite their public rescue" appears when that voter's veto saved the player before. It costs the same and rolls the same as the plea, and it survives a reload. | M. One full set. |
| **D2** The rules-2 foundation | Split the validator and the pending command by version, with rules 1 frozen in place. Add the content registry, the occasion kinds, entry by action or grant (the tick fix), and statement and memory evidence. `On` and validation accept 2. No content yet. | Nothing | L. Every rules-0 and rules-1 test passes unchanged. |
| **D3** Nominee appeals | The rules-2 campaign appeal and holder appeal (§4.2). NPC responses on keyed coins, evidence, +3/-3 through `ChangeKeyed`, zero time. Verification adapters that list the choices hidden inside a category. | In a rules-2 season (call 8), the voter answers with their claim, a deflection or a concern. The player replies with a fact they know, the relationship, pressure or a polite exit. The voter reacts ("They warmed to you", "That landed badly"). The player still chooses whether to make the plea. | L |
| **D4** The conditional veto | §3.1 | A nominated player can ask a veto player, before the veto, whether they would use it on them, and then ask them to promise. The promise shows on Your word. If that player wins, the veto meeting keeps or breaks the promise. If they don't win, it lapses quietly. | L. Largest risk: the mode-2 family. |
| **D5** Grants, time and consultations | Grants written when the role is won (owner, week, role) and used up by a conversation. The HoH and veto consultations. The +1/-1 receipts that `SocialActionBudget` reads. The ROLE doc's abuse checklist (:160). The toggle moves to rules 2 (call 8). | As HoH, veto holder or nominee, a line says one conversation this week is on the house (at most two a week). Some endings say "One more conversation in this window" or "The argument cost you an opportunity". | L |
| **D6** Measurement | Lab policies start and answer conversations. Paired 100-seed runs with rules 2 on and off, after R7's B9, with NPC ticks on. The ROLE doc's diagnostics (:158). Pacing against 45 minutes at 8 players. The owner signs off the numbers. | Nothing; a report | L, mostly compute |
| **D7** On by default | `ApplyFresh` writes rules 2 (`Shipped` = 2) once R7 has enrolled Roles. Goldens and digests are re-recorded, with the reasons stated. The fixture and manifest cell for a shipped rules-2 season, a season-walk route through a conversation, and the largest text size. The owner decides whether the toggle stays as an opt-out. | Every new season | M |

D1 to D7 take about five or six full sets, not counting B9's compute. None of these slices adds a persisted field; if one ever did, it would be qualified alone.

## 7. Old seasons and tests

- **Gating.** Everything new is gated on `dialogueRulesVersion` 2 or later, and rules 1 keeps its exact `== 1` behaviour. Rules-0 seasons and every committed fixture replay byte for byte: no new draw, log, id or `Change`. The canary is `s29-mode1-allweek-w3-veto-meeting`.
- **Saves.** Saves stay at schema 29, so there is no migration. However, every save with dialogue on that a player keeps widens what a future frozen schema-29 reader must accept: rules 1 from D1 on, and rules 2 from the moment call 8 picks. This comes on top of the role moments' values (ROLE-MOMENTS-PLAN §4.6).
- **EditMode-only tests.** Many dialogue tests run only in EditMode, which the dotnet subset never covers. Each slice runs them under Unity's Mono harness before its full set, and the pinned rules-1 cases stay unchanged.
- **Schema literals.** Three places check the live schema number: Simulation/DialogueRules.cs:19 and :97, and Simulation/EpisodeValidation.cs:88. These are bounds and must move with any later schema, or dialogue switches off.

## 8. Risks

- **Balance.** The Studier policy already wins 95% of eligible weeks and 73.5% of seasons at 8 players, against targets of 60-70% and 25-35%. Grants and +1 time push those numbers the wrong way, so D7 waits for D6.
- **Pacing.** Every node is another click. D6 measures against 45 minutes at 8 players.
- **Free warmth.** The +3/-3 lands at most once a conversation, a conversation costs an action or a grant, and each NPC gives one occasion a week.
- **Privacy.** Previews show only public facts and what the player knows. Role moments' privacy tests (§5.3 of that plan) apply.
- **Captions are a contract.** The toggle and every new answer get new captions, checked unused. Nothing is appended to an existing caption.
- **Two sessions share the branch.** No schema number or command kind is claimed. Story rules 10, the story session's fix for the pariah rate, still stands between R7 and D7.

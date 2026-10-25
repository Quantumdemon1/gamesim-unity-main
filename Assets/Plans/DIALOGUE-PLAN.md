# Dialogue: conversations that matter (dialogue rules 2 to 5)

Design note P0, 2026-10-25. It revises D0 (9d5b1f83) into the joint design note of the master plan's Stage 0. Docs only: it changes no code, save, test or rule.

It settles dialogue's own rules, and the numbers dialogue shares with other work: the pooled schema 30 that the All-Star histories build (§6, final) and competition rules 5 (§9). The owner approves it once.

**The owner's instruction, 2026-10-25.** About dialogue V2 the owner said: "Fix them so they work and implement these changes now." This plan therefore goes ahead on the defaults in §1, recorded here as the owner's instruction to implement now with these defaults. Each default stays open to the owner's change, and §1 says what changing it costs. Shipped seasons stay on dialogue rules 0 until D7.

**Evidence base.** Branch `claude/dialogue-v2`, cut from main `b208d46d`. That commit includes role moments R0 to R6 and R5b: `StoryRules.Roles` is 11 and `RolesOn` exists, while `StoryRules.Current` stays at Threads (9). The All-Star histories plan is read at `cacc59e0` (`Assets/Plans/ALL-STAR-HISTORIES-PLAN.md`, branch `claude/all-star-histories`). Code paths are relative to `Assets/Gamesim/`. "The ROLE doc" means `ROLE_DIALOGUE_ECONOMY_20261012.md` at the repository root, and "role moments" means `Assets/Plans/ROLE-MOMENTS-PLAN.md`.

**Names.**
- *Dialogue rules 1* is the pilot merged in PR #28. Its two content versions are V1 (a personal appeal) and V2 (citing a public rescue). Earlier notes call this pilot "dialogue V2".
- *Dialogue rules 2 to 5* are this plan's ladder (§2): the ROLE doc's consequences, conditional veto, role grants and conversation time.

---

## 0. The short version

- **Today.** Rules 1 is one three-step conversation.
  - When the player is nominated and the campaign is on, the Emotional "Ask for their vote" opens "Make your case". The next step is "Appeal personally", or "Cite their public rescue". The last is "Make the plea", "Keep their vote open" or "Leave it there".
  - It costs one action. Its plea is the ordinary plea's roll, and every ending gives zero time (Simulation/EpisodeEngine.Dialogue.cs:177, 199; Simulation/DialogueRules.cs:274-280).
  - No shipped season can reach it. `ShippedRules.ApplyFresh` never sets `dialogueRulesVersion` (Simulation/ShippedRules.cs:26-65), and `DialogueRules.On` needs exactly version 1 at schema 29 (Simulation/DialogueRules.cs:19).
- **D1, now.** A chip on the cast screen, off by default, starts a season on rules 1. To be plain about it: rules 1 adds no new consequence. It is a three-step form of a plea the game already has.
- **The ladder.** Each later step is a new dialogue rules version, played through the same chip:
  - 2: the first conversation whose answer moves the relationship;
  - 3: the other campaign trees, and the appeal to the veto holder;
  - 4: the conditional veto, the nominee's free conversation, and +1/-1 conversation time;
  - 5: the HoH and veto consultations.

  A version freezes the first time a shipped path can write it.
- **Saves.** Dialogue needs no new field (§6). Rules 2 and later exist only from schema 30, the pooled bump the All-Star histories build, so D2 starts once that bump is on this branch. The frozen schema-29 reader therefore holds dialogue rules 0 and 1 only.
- **One economy with role moments** (§4). A grant replaces a paid entry, and a conditional veto promise is a deal, beside every other word on the veto. Settling it moves only the player's view of the holder and draws nothing.
- **D6 and D7.** Measurement after role moments' B9, and then dialogue on by default in new seasons.

## 1. Decisions

The table below records the owner's 2026-10-25 instruction ("implement now") with these defaults. Each one is open to the owner's change. Rows marked **P0** differ from D0.

| # | Decision | Default adopted | If the owner changes it |
|---|---|---|---|
| 1 | How the merged pilot becomes playable | An opt-in on the cast screen, **off by default** (D1). It writes a fixed, pinned version (`Offered`), never the bound `Version`. The shipped default stays 0 until D7. | Turning it on by default now reverses OWNER-DECISIONS-2026-10-22 §5 ("stay on dialogue rules 0"), and the lab goldens' rules tuple must be re-recorded. |
| 2 | Grants and role moments | **One economy.** A grant replaces a paid entry and never stacks with a paid plea (the owner's AP-0 rule, OWNER-DECISIONS §4). | A separate grant menu would be a second economy beside role moments. |
| 3 | **P0.** What records a conditional veto promise | **A new deal kind, `veto_if`**, in `state.deals`, where every word on the veto already lives in every commitment mode (Simulation/Negotiation.cs:744-749). It needs no new field (§4.2). | D0's unified row with `conditionalVeto` also fits schema 29. But the mode-2 family knows only Safety and Vote (Simulation/UnifiedVoteFamilyValidation.cs:55-57), so it would be a second veto authority, and every veto reader would read two lists. |
| 4 | The ROLE doc's numbers | Unmeasured starting values (§5.4), tuned only through measurement (D6). | - |
| 5 | Randomness | New dialogue outcomes draw **keyed coins** (`StoryRandom`, `ChangeKeyed`), never the season's main stream. An existing act inside a conversation keeps its own roll: the final plea is the ordinary plea's main-stream roll. | Main-stream draws would re-roll every later event in a season whenever a conversation is added or dropped. |
| 6 | B9 | Dialogue's B9 is a **successor** to role moments' R7, not a joint run. | A joint run would hold back the built role moments until dialogue is built. |
| 7 | **P0.** Schema | Dialogue adds **no field to schema 30** (§6, final). Rules 2 and later are defined only at schema 30 or later. D2 therefore starts from H1b's schema-30 bump merged into this branch, its proofs run at 30, and the frozen schema-29 reader holds dialogue rules 0 and 1 only. Its cost: D2 waits for the bump. A need that does not fit stops the work and is reported; it would take schema 31. | D2 at schema 29 if it is ready first (the All-Star plan's D-H6) starts and lands without waiting. But the frozen schema-29 reader must then carry the rules-2 validator for ever, and H1b's reader waits for D2 or is redone after it (§6). |

This note also makes the calls below. They are open to the owner too.

| # | Call | Recommended | Alternative |
|---|---|---|---|
| 8 | **P0.** When the chip moves to a new version | With each stack that brings something to play: 2 at Stack 3, 3 at Stack 4, 4 at Stack 5, 5 at Stack 7 (§2). | Moving only once rules 2 is complete (D0's call) shows the owner nothing between D1 and D5, and makes one very large version. |
| 9 | **P0.** Dialogue and role moments | **No dialogue version requires `RolesOn`**, and the chip never writes a story rules version. Consultations open from the person's menu, and also from the pitch board and the offer board when role moments are on. Its cost: each consultation has two entry points, built and tested twice, with a caption of its own on each. | The chip also writes story rules Roles. Role moments would then freeze in the owner's saves at that merge, before B9 tunes them, and B9's changes would need story rules 12. |
| 10 | What the veto question costs | "If you win the veto, will you use it on me?" costs the window's action, and no grant pays for it. | Let the nominee's grant pay for it. |
| 11 | **P0.** Storyline budget | At most **32 conversations a season**, never in the last eight storylines. Nothing is pruned. A refused conversation leaves its plea available as the one-shot plea (§5.5). | No budget: dialogue and the story pool compete for the same 92, and late in a long season either one starves the other. |
| 12 | **P0.** Consultation windows | **The existing windows and phases** (§4.1). | A new window changes the length of every save's `windowActions`, which validation checks (Simulation/EpisodeValidation.cs:148). That means a migration. |
| 13 | **P0.** Competition rules 5 | Claimed for PC5, the competition part of B9 (§9). | - |

## 2. The version ladder

**The rules.**
- **The bound.** `DialogueRules.Version` becomes the upper bound: the highest version this build defines.
- **A closed set.** Validation accepts only defined versions (`IsDefined`), as `StoryRules.IsDefined` does: 0 and 1 today, and 0 to 5 at the end of the ladder. EpisodeValidation.cs:91 and DialogueRules.cs:109 call it instead of comparing with `Version`.
- **Behaviour is keyed to the version that introduced it.** No code reads `Version` for behaviour. What rules 2 added runs at `>= Rules2`, and the closed rules-1 pairs only at `== Rules1`.
- **Switching on.** `On(s)` holds when the schema is 29 or later, the version is defined, and it is at least 1.
- **Freezing.** A version freezes the first time a shipped path can write it: the merge that lets the chip write it, or a default. After that it never changes. A change is a new version.
- **Who writes what.** The chip writes `Offered`, a pinned constant moved by hand when a stack merges. `ApplyFresh`'s default writes `Shipped`, 0 until D7. Neither ever writes `Version`.
- **Schema.** Rules 2 and later need schema 30 or later (`Rules2Schema` = 30, the schema that introduced them).
- **Prerequisites.** Every version needs those of rules 1 (Simulation/DialogueRules.cs:448-453). None needs role moments (call 9).

**The ladder.** The numbers assume the chip moves at every stack (call 8). If a stack does not move it, that stack's work joins the next version instead.

| Rules | Slices | What it adds | Stack | Freezes when |
|---|---|---|---|---|
| 0 | - | Off. Every shipped season, and every migrated one. | - | Frozen |
| 1 | PR #28, D1 | The pilot: V1 and V2 campaign conversations, one action, zero time | 2 | D1 merges |
| 2 | D2 | The foundation (a closed content registry, per-version bounds, statement and memory evidence, entry funded by an action or a grant) and the vote-word campaign tree: a keyed NPC response, +3/0/-3 through `ChangeKeyed` | 3, after schema 30 | The chip first writes 2 |
| 3 | D3 | The memory, pressure and neutral campaign trees, and the appeal to the veto holder | 4 | The chip first writes 3 |
| 4 | D4, D5a, D5c | The veto question and the conditional promise, veto occasions, the nominee's grant, and +1/0/-1 time | 5 | The chip first writes 4 |
| 5 | D5b | The HoH and veto consultations, with their grants | 7 | The chip first writes 5 |
| - | D7 | `Shipped` becomes the latest frozen version | 10 | - |

**Per-version bounds.** `MaximumSteps` stays 3 and keeps its meaning for rules 1 (Simulation/DialogueRules.cs:16). Rules 2 adds `StepsFor(version)`, and `ReservedEvents` reads the active conversation's own bound.

| Bound | Rules 1 (as merged; freezes at D1) | Rules 2 and later |
|---|---|---|
| Steps: path entries, house events and responses in one conversation | 3 | 5 |
| House events reserved for the active conversation | 3, less those used | 5, less those used |
| Consequential choices; options at a node | As authored in V1 and V2 | 3; 4 (ROLE doc :90) |
| Evidence rows | 0 (V1) or 1 (V2) | 3 |
| Statements in one conversation | 0 | 1 |
| Conversations a season | 100 (`MaximumEpisodes`) | 32, a new `ConversationBudget(version)` (§5.5) |
| Occasion kinds | `campaign` | Adds `holder` (3), `veto-question` (4), `hoh-consult` and `veto-consult` (5) |
| Entry | One action | One action, or one grant from 4 |
| Grants; veto occasions | None | None below 4. From 4: at most 3 grants and 1 veto occasion a week |
| Time | Zero | Zero below 4; +1, 0 or -1 from 4 |

`MaximumEpisodes` stays 100 in every version. It is also the storyline ceiling that the story pool and the threads read in dialogue-on seasons (Simulation/Story/EpisodeEngine.Story.cs:479, :566; Simulation/Story/EpisodeEngine.Threads.cs:38, :49) and that validation reads (Simulation/DialogueRules.cs:49, :115, :120). Rewriting it per version would change the story pool in chip seasons, so the 32 is a separate bound.

**Role moments' earmark.** ROLE-MOMENTS-PLAN.md:62 and :691 put Layer 2 "under dialogue rules version 2". On this ladder, 2 is the first consequential tree. Grants and time arrive in 4 (the nominee's grant and +1/-1) and in 5 (the HoH and veto grants).
- When the owner approves this note, both lines change to: "under the dialogue rules version that first carries grants and time (4 on DIALOGUE-PLAN's ladder, and 5 for the consultations' grants)".
- The rest of :691 stands. The tick-key fix at Simulation/DialogueRules.cs:182 lands in D2, before any grant exists.
- §4.2's option C row (:467, "Kept for Layer 2") needs no change.

**Pins that move.**
- Tests/EditMode/DialogueCampaignRulesTests.cs:500 uses 2 as the "unknown" version; when D2 defines 2, it moves to `Version + 1`.
- Tests that mean rules 1 already write the literal 1, so they stay on rules 1 when `Version` moves.
- `Offered` moves by hand at Stacks 3, 4, 5 and 7. Tests and fixture builders never read `Offered` to mean rules 1; they write the literal 1. Chip tests assert `Offered`'s literal value, and are re-pinned with a reason when it moves.

## 3. The ROLE doc against today's code

| ROLE doc delivery step (:148-154) | Today | Rules |
|---|---|---|
| 1. Presentation: six categories, speaker adapters | Shipped (Runtime/Episode/EpisodeDirector.ConversationNavigation.cs:10-15) | - |
| 2a. A small campaign tree | Rules 1, with closed V1 and V2 content (Simulation/Story/DialogueCampaignV1.cs, DialogueCampaignV2.cs). Only the plea roll; zero time. | 1 playable (D1); consequences in 2 and 3 |
| 2b. A conditional-veto statement tree | Storage only: `DialogueStatementState` and `DialogueVetoOccasionState`. Nothing writes them, and rules 1 refuses any row (Simulation/DialogueRules.cs:111-112, 190). | 4 |
| 3. Role grants and +1/0/-1 time | Storage only: `DialogueRoleGrantState` and `DialogueTimeReceiptState`. Rules 1 refuses grants (:120-122) and any time other than zero (:274-280). `SocialActionBudget` reads no time receipt (Simulation/EpisodeEngine.cs:796). | 4 and 5 |
| 4. Measurement after B9 | None. The balance lab never issues `StartDialogue` or `ChooseDialogue`. | D6 |
| 5. Combined native validation | Only the rules-1 PlayMode suites. There is no season-walk route and no fixture of a save with dialogue on (OLD-SAVE-ACCEPTANCE.md §7). | D1 and D7 |

D2 generalises what rules 1 hard-codes: the V1 and V2 beat ids, the single `campaign` occasion, the after-veto window and an entry paid with an action (Simulation/EpisodeEngine.Dialogue.cs:66-70, 183-199; Simulation/DialogueRules.cs:164-183, 318-321).

## 4. One economy with role moments

Role moments (story rules 11) let the player answer what comes to them at the role points of a week. Dialogue adds the conversations the player starts at those same points. Both draw on one budget, the window's seats.

| The player is | Role moments: the answers, always free | Dialogue adds (a conversation the player starts) | Paid by |
|---|---|---|---|
| Head of Household, before nominations | The pitch board: at most 3 pitches, answered with Promise safety, Hear them, Turn them down or Feel them out (R5) | **HoH Consultation** (5): one conversation with an active houseguest. A promise made in it is never a second safety row to that houseguest that week. Under Roles, with their pitch on the board, it is the pitch's own Promise safety answer. Otherwise, and in every season without Roles, it is the existing `PromiseSafety` act, which writes the canonical Safety promise of the season's commitment mode, paid by the conversation's entry. | The HoH grant, or else the window's action |
| Veto holder, before the decision | The offer board, with one veto word a week (R2 to R4) | **Veto Consultation** (5): one conversation with a nominee or the HoH. It ends in the answers or price that already exist (`RespondToDeal`, or a veto for a price) and cannot give a second veto word. | The veto grant, or else the action |
| Nominated | Just a Pawn and quiet tears (R6) | **Nominee Appeal**: a campaign conversation (2), or the appeal to the veto holder (3). Each ends in the ordinary plea's roll. The player can also ask the veto question (4). | The nominee grant (from 4), or else the action. The veto question always costs the action (call 10). |

**The rules** (ROLE doc :35-51; OWNER-DECISIONS §4):
- A grant pays for one newly started conversation in place of an action. It pays for nothing else, never carries over to another week, and can never be saved up.
- Each role gives at most one grant a week, and a player gets at most **two** free conversations a week, however their roles overlap. Being renominated, reloading or going to another room creates no new grant.
- A grant never stacks with a paid plea:
  - `CanLobby` allows one plea per decider per phase.
  - An independent plea to the same voter closes the conversation (Simulation/EpisodeEngine.Dialogue.cs:263-276).
  - At AP 0 without a grant, no route delivers a plea's effect. The AP-0 test (Tests/EditMode/DialoguePublicRescueV2Tests.cs:216) is extended to every kind of entry.
- Answers stay free and use up no grant. That covers incoming offers, pitches, Feel them out and story asks.
- **Counting is on attempt rows.** A grant is used up by the conversation's first accepted node, and stays used if the conversation lapses or is left. The one-occasion rule counts every started conversation. A greedy scripted player hunts for loops in D2 and D5.
- "Answer the existing house moment first" stays for starting a conversation (Simulation/DialogueRules.cs:52-53). From rules 2 the plea it wraps stays available meanwhile (§5.5).
- Role moments add no time (ROLE-MOMENTS-PLAN §2.3). Dialogue's +1 and -1 receipts are the only way to gain or lose conversation time. `WindowBudget` reads them from rules 4 (Simulation/EpisodeEngine.Week.cs:93-111):
  - it adds the open window's +1s of this week and takes off its -1s;
  - an action a +1 paid for (`spentWindowAction`) is not counted against the week's extras in a later window;
  - they never go through the positive-only bank (`phaseEventSocialBonus`).

### 4.1 Windows (call 12)

| Occasion (rules) | Phase | Window and its seats | Opens from | Ends |
|---|---|---|---|---|
| Campaign appeal (2; V1 and V2 in 1) | Campaign | After the veto, 2 | As rules 1 | At the eviction |
| Holder appeal (3) | Veto meeting, before the decision | After the nominations, 1 | The person's menu | At the decision |
| Veto question (4) | Veto, after the draw and before the result. The players are drawn as Veto selection advances (Simulation/EpisodeEngine.cs:395-399). | After the nominations, 1 | The person's menu | At the result |
| HoH consultation (5) | Nomination, before the original nominations | After the HoH, 2 | The person's menu; also the pitch board under Roles | When the original nominations commit. Not granted again for replacement nominations. |
| Veto consultation (5) | Veto meeting, before the decision | After the nominations, 1 | The person's menu; also the offer board under Roles | At the decision, or when the veto cannot be used |

- **No new window or phase.**
  - `Windows.Count` (4) is part of every save (Simulation/EpisodeEngine.Week.cs:9-10; checked at Simulation/EpisodeValidation.cs:148).
  - `EpisodePhase` is append-only, with frozen ordinals (Simulation/EpisodeState.cs:7-12).
  - The master plan's "new windows and phases" for D5b become new occasion kinds inside these.
- **The one seat after the nominations.** The veto question, the holder appeal and the veto consultation share it. A grant is how a nominee or a holder gets a second conversation there, within the two free a week.

### 4.2 The conditional veto (D4, rules 4; decision 3)

1. **The question.** The player is nominated, the veto players have been drawn, and the result is not yet known. The player asks a veto player "If you win the veto, will you use it on me?". The answer is yes, no, not committing, or a condition. It is drawn on a keyed coin inside the command and saved as the conversation's `DialogueStatementState`. It records what was said, not proof of a plan.
2. **The promise.** After a yes, "Can we make that a promise?" writes one deal:
   - kind `veto_if`, a new closed value, never proposed by the player. Validation refuses it below dialogue rules 4, as it refuses `veto_hold` outside Roles (Simulation/EpisodeValidation.cs:310);
   - the veto player as proposer, and the player as recipient and target;
   - valid this week only, at the Critical trust of a veto commitment (Simulation/DealState.cs:145-146);
   - id `deal-ifwin-<n>`, where n is the log sequence of the promise step in the conversation's path. The id ties the deal to its conversation, statement and consent one to one, so no field holds the link. Deals are never removed. The prefix must not begin with any existing deal prefix: readers match `deal-veto-` (`NpcDeals.VetoAskPrefix`), `deal-ask-`, `deal-keep-`, `deal-price-` and `deal-counter-` by prefix, so a `deal-veto…` id would be one dropped hyphen from reading as an accepted veto ask.

   No `veto_use` row is written or reused (ROLE doc :78). Schema 29's `conditionalVeto` terms stay null in every version. Validation already requires that at rules 0 and 1 (Simulation/EpisodeValidation.cs:96; Simulation/DialogueRules.cs:111-112), and later versions keep the refusal.
   - **Capacity and conflicts** (ROLE doc :78). A maker gives at most one `veto_if` a week. "Can we make that a promise?" is refused in its own words while the maker is party to a binding `veto_use` or `veto_hold` that week, with anybody. The statement stands either way.
3. **The result.** At the veto result, `DialogueVetoOccasionState` records the holder, the veto players and the nominees. A `veto_if` lapses neutrally (Expired, with no effect) when its maker did not win, when the player is off the block, or when the veto cannot legally be used. The occasion row says why.
4. **Settlement.** If the condition is met, the veto decision keeps or breaks it, by `DealResolution.Veto`'s rule (Simulation/DealResolution.cs:182). D4 adds a `veto_if` case beside `Veto` and `Hold`, which today answer only `veto_use` and `veto_hold`.
   - **Whose view moves:** the player's view of the holder, as at every deal's settlement (`WriteScore(wronged, actor)`, Simulation/EpisodeEngine.cs:2333). Used on the player, it is Fulfilled: +24. Otherwise the holder broke it: -45. These are `FulfilledBase` 8 and `BrokenBase` -15 (Simulation/DealResolution.cs:54) at the Critical weight 3 (Simulation/DealState.cs:145-146). The holder's view of the player does not move.
   - **No spread, no draw.** The existing settlement (Simulation/EpisodeEngine.cs:2275-2351) ends a breach with `SpreadBetrayal` (:2347), which draws the main stream twice per witness (:2382-2383) and moves witness-to-holder scores. The promise was private, and the ROLE doc says disclosure follows what each participant really knows (:78). So D4 settles a `veto_if` in its own step, after the veto's existing verdicts. It writes the status, the player's view of the holder, the player's memory and reckoning, and a line to the two of them only; under the commitment rules also `settledWeek`, `brokenById` and the breach the player holds (`RecordBreach`), as the existing step does. It never calls `SpreadBetrayal`, and draws nothing.
   - **The test.** A D4 test pins `randomState`, and every NPC-to-NPC score, across a veto meeting that settles a broken `veto_if`, against the same meeting without it.
   - A party who leaves first ends it as the commitment rules end any deal whose party has left.
5. **One word.** Each reader counts it once:
   - the holder's pull, `StrategyRules.VetoWillingness` (Simulation/StrategyRules.cs:475), counts a `veto_use` or a met `veto_if` once;
   - it is not the player's own word on the veto. `IsVetoWord` (Simulation/Negotiation.cs:738) stays `veto_use` and `veto_hold`, because the player is the beneficiary, never the maker;
   - the Your word page, Your Week, and the jury and finale reads (`CommitmentsRead`, `YourWeek`, `FinalistRead`) get lines for a promise made to the player. A broken one is the holder's breach;
   - R8's holder offer, when built, is refused while that holder's met `veto_if` stands;
   - it is one more row under the deal ceiling of 40, which counts every deal row (Simulation/PlayerDeals.cs:51; Simulation/UnifiedVoteStore.cs:57-59). With no room left, the promise is refused in its own words, and the statement stands.
6. **The largest risk** is now the readers' wording and settlement order, not the mode-2 family.

## 5. The conversations

### 5.1 Content

The content is a closed registry, keyed on template and content version.

| Content (rules) | Occasion id | Who the player talks to | Ends in |
|---|---|---|---|
| Campaign appeal (2) | `week:N:campaign:npc` | An eligible voter | The ordinary vote plea (Emotional, or Strategic after a citation), or a vote left open |
| Holder appeal (3) | `week:N:holder:npc` | The veto holder | The ordinary save plea, or no ask |
| Veto question (4) | `week:N:veto-question:npc` | A veto player | A statement, and optionally the conditional promise |
| HoH consultation (5) | `week:N:hoh-consult:npc` | Any active houseguest | Their case is heard. Any promise is the pitch's own. |
| Veto consultation (5) | `week:N:veto-consult:npc` | A nominee or the HoH | The answer or price that already exists; one veto word |

**How every tree runs** (the ROLE doc, :80-94):
1. The player opens.
2. The NPC gives a real response: their vote claim, a deflection, or a named concern. It is chosen inside the command.
3. The player answers with a fact they know, a real commitment, the relationship, a respectful no, or pressure. Every node has a neutral exit.
4. The NPC reacts, once.

**Evidence** is a fact the player holds:
- a public veto record (`power`, as in V2);
- a memory;
- the NPC's own statement, or a vote claim the NPC made to the player, copied into the conversation's statement (§6).

Hearsay never proves a ballot, and an old intention never proves today's choice: an overheard claim, an ally's report or a read is never copied as proof. Previews show KnownOdds words, never a score.

**What replaces V1 and V2.** In a season on rules 2 or later, the campaign is the rules-2 appeal, which takes in V2's citation. V1 and V2 stay as they are for rules-1 seasons.

**Content is versioned like rules.** A change to what a node does is a new content version, and a saved conversation keeps its own.

### 5.2 Randomness (decision 5)

| Draw | Source |
|---|---|
| The NPC's response at a node | `StoryRandom.Unit(s, "dialogue:{occasionId}:{pathIndex}")`. It is keyed on the occasion, not on sequence numbers, so doing something else first does not change the coin. No story key uses the `dialogue:` prefix today. |
| A +3 or -3 relationship change | `EpisodeEngine.ChangeKeyed` (Simulation/Story/EpisodeEngine.StoryEffects.cs:25-27), with the same key plus `:change`, never plain `Change`, which draws the main stream (Simulation/EpisodeEngine.cs:2438-2439) |
| The answer to the veto question | The same rule as any response. Its occasion is `week:N:veto-question:{speakerId}`. |
| Grants, time, lapses, closures, and writing the conditional promise | No draw: each is worked out from the state |
| Settling the conditional promise | No draw: its own step, with no `SpreadBetrayal` (§4.2) |
| An existing act inside a conversation | Its own, unchanged: the final plea is the ordinary plea's main-stream roll (Simulation/EpisodeEngine.Strategy.cs:104-162) |

Starting, leaving or abandoning a conversation draws nothing from the main stream. A test pins `randomState` unchanged across a conversation that ends without the plea.

### 5.3 The relationship is the player's

Every change a conversation makes is how the NPC feels about the player, written by the engine's relationship reducer with a keyed roll (`ChangeKeyed`). The one exception is the conditional promise's settlement: like every deal's, it moves the player's view of the holder, with no roll (§4.2). Neither a conversation nor its settlement ever moves a pair of NPCs.

### 5.4 Numbers (decision 4: unmeasured starting values)

| What | Starting value | ROLE doc |
|---|---|---|
| How the NPC feels about the player | +3 for an earned result, 0 for a neutral or respectful one, -3 for a clear backfire. At most one change a conversation. | :110-118 |
| Conversation time (from 4) | +1 for an ending designated as earning time, -1 for one designated as losing it, otherwise 0. At most one +1 and one -1 a week, capped separately. A +1 is usable only in the window that is open. A -1 applies only when two opportunities remain after the conversation's cost. | :120-129 |
| Grants (4 and 5) | One per role a week; at most two free conversations a week | :35-51 |
| Shape | At most 3 consequential choices, 4 options a node and 5 steps | :90 |
| Entry | One action at the first accepted node, or one grant. Later nodes are free. | :94 |
| Conditional promise: the player's view of the holder | Unmet: 0. Met: +24 kept, -45 broken | None. The numbers are the deal rules' (Simulation/DealResolution.cs:54 at the Critical weight 3, Simulation/DealState.cs:145-146); :78 sets only the lapse |
| Occasions | One per kind, NPC and week | as rules 1 (Simulation/DialogueRules.cs:47-48) |

The numbers that already exist keep their meanings: pleas, deals, pitch answers and role-moment riders. The web reference's dialogue gives +2 to +8 and every fallback choice is positive, so it is not a model for these numbers.

### 5.5 Storyline budget and retention (call 11)

- **What the season holds.**
  - Every conversation is a storyline; the two share an id (Simulation/DialogueRules.cs:21-25, 167).
  - Validation allows 100 storylines (Simulation/EpisodeValidation.cs:339), and nothing prunes them (Simulation/Story/EpisodeEngine.Story.cs:39-40).
  - Under Roles the story pool stops at 92 (:48), so the last eight stay with the backdoor, the pawn conversation and production.
  - A conversation also holds up to five of the season's 400 house events (Simulation/EpisodeValidation.cs:318).
- **The budget, from rules 2.** A conversation starts only while:
  - (a) the season has started fewer than 32 conversations (`ConversationBudget(version)`, §2). Every started one counts, lapsed and abandoned ones included;
  - (b) the season holds fewer than 92 storylines, under every story rules version;
  - (c) its whole path fits the house events, with five reserved.

  Rules 1 keeps its own limits (Simulation/DialogueRules.cs:49-51).
- **32 is an unmeasured starting value.** P1(d) measures, for a 16-houseguest season without dialogue and with the NPC world on, the most storylines and the most house events it reaches. House events are never pruned either; nothing removes from `houseEvents`. Before rules 2 freezes, the budget is lowered until both fit: the storyline peak plus the budget stays at or under 92, and the house-event peak plus five per budgeted conversation stays at or under 400. If the house events alone reach 400, no budget fits, and that is reported to the owner before rules 2 freezes.
- **A refusal never takes a plea away.** From rules 2, when a conversation cannot start (the budget, or an unanswered house moment), the plea it wraps stays available as the ordinary one-shot plea, at its ordinary cost, and the refusal says why.
- **Rules 1's lock, and P1(a).** In rules 1 a refused conversation locks the Emotional plea; the other approaches stay (Runtime/Episode/EpisodeDirector.Strategy.cs:74-90). Rules 1 is not frozen until D1 merges, so P1(a)'s result has a decision attached. If the lock refuses in more than one nominated Campaign week in five (an unmeasured threshold, open to the owner), the lead asks the owner before D1 merges whether rules 1 takes the rules-2 fallback; that moves rules 1's pinned tests with the reason stated. Otherwise rules 1 stays as merged, and the chip's description says the Emotional plea waits while a house moment is open.
- **Retention: nothing is pruned**, at schema 29 or 30. Conversations, statements, grants, veto occasions, time receipts and `veto_if` deals stay for the whole season.
  - Every once-per rule and weekly cap is a count over these rows, so a pruned row would let an expired occasion, grant or +1 be earned again (ROLE doc :146).
  - The bounds keep the season small: at most 32 conversations, 3 grants and 1 veto occasion a week, and 1 statement a conversation.
- **All-Star campaigns.** The route is the All-Star plan's call (AS2). If they are built in the story catalogue, they count in the pool's 92. If they are built on the dialogue layer:
  - each campaign is a new occasion kind under a new dialogue version, and counts in the 32;
  - the governor's caps count started campaigns (attempt rows), as dialogue's own caps do;
  - they play only in chip seasons until D7 puts dialogue on by default.

## 6. Schema 30: fields dialogue needs (final)

**Conclusion: none.** Every dialogue need from D2 to D7 fits schema 29's `DialogueState` and the existing deal fields. The pooled schema 30 carries no dialogue field, no conditional-veto family and no dialogue rules version. The All-Star histories claim it for `alumniRulesVersion` and `alumniCatalogEdition` (ALL-STAR-HISTORIES-PLAN §5.1, §10).

**Pooled candidate, not dialogue's.** B9's planned `tuningRulesVersion` (BALANCE_BASELINE.md:310). Recommended: it joins 30 as an inert int (0, with no writer), per the master plan's risk 7 ("make it one pooled bump"). Otherwise it takes 31, as ALL-STAR-HISTORIES-PLAN §10 has it, at the cost of a second whole frozen reader (Frozen28 was 8,092 lines). This is the lead's or the owner's call, for the All-Star workflow; it does not change the dialogue list above.

**What schema 30 does for dialogue, with no field:**
1. Simulation/DialogueRules.cs:19 and :97 become `>= 29`, the schema that introduced the store. Whichever of D1 and H1b lands first makes this change (the All-Star plan asks D1 to, §10, and lists it in H1b's live pins, §8.5); it is harmless at 29.
2. Simulation/EpisodeValidation.cs:88 and Runtime/Persistence/EpisodeSaveValidation.cs:13 move to 30 with the bump. They are the live schema, and they belong to H1b.
3. `UpgradeV29ToV30` carries `dialogueRulesVersion` and the dialogue store as they are. Nothing is inferred.
4. `FrozenEpisodeV29`'s dialogue domain is rules 0 (the empty store) and rules 1 (the closed V1 and V2 pairs, with the stores the D1 chip writes). From D2 on, the live validator refuses rules 2 and later at schema 29, so the frozen copy never carries them.
5. **D2 starts from the bump.** D2 sets `Rules2Schema` = 30, and at the live schema 29 every rules-2 state fails validation, so its dotnet subset, its Mono-harness fixtures and its greedy-player hunt cannot pass on a 29 base. D2 therefore starts from a merge of H1b's schema-30 commit into `claude/dialogue-v2`, which the lead lands, and its proofs run at 30. If H1b's bump is not ready, D2 waits; that delay is the cost of decision 7. D1 may land before the bump or after it.

**The order is decision 7's, not the field list's.** The field list above is final under either order. Items 4 and 5 follow decision 7's default, which differs from the All-Star plan's D-H6 (D2 on main first if it is ready, ALL-STAR-HISTORIES-PLAN §10, §12). The reasons for this default:
- The reader can be cut now, from rules 0 and 1 as main has them; the only dialogue change there is item 1's `>= 29` literal. H1b neither waits for D2 nor redoes the reader after it.
- A rules-2 validator never needs a frozen copy at 29, so `FrozenEpisodeV29` stays the pilot's size.

The lead settles the order, and the All-Star plan's §10 follows it.

**The fit check.**

| What dialogue needs (ROLE doc :137-146) | Where it lives | Fits |
|---|---|---|
| **Conversation:** occasion id and kind | `DialogueEpisodeState.occasionId` and `occasionKind`, with new closed values (§5.1) | Yes |
| Template and content version | The storyline's `templateId`, and `contentVersion` | Yes |
| Player, NPC, week, window, phase | `s.playerId`; `npcId`, `week`, `window`, `startedPhase` | Yes |
| Current node and path | The owned storyline's `beatId`, `eventId` and `path`, and its house events | Yes |
| NPC responses, including a saved concern | `responses[]` and `responseKey`, a closed vocabulary for each template | Yes |
| Evidence | `evidence[]`: `power` and `memory` keep copies, and `statementId` cites a statement | Yes |
| Entry, paid by an action or a grant | `entry.kind`, `grantId`, `windowActionBefore` and `windowActionAfter`. A grant entry spends no tick, so rules 2 keys ticks by the kind of entry (the fix at Simulation/DialogueRules.cs:182). | Yes |
| Settlement | `time`. The +3/-3 follows from the template, content version and response key, and `ChangeKeyed` writes it to the relationship. | Yes |
| **One statement a conversation** | `statement` is a single row. That is enough: it holds the veto question's answer, or a copied vote claim, and no tree writes both. A cited claim is copied because a ledger `ClaimRow` (week, voter, target, source, status) has no id and is dropped past 512 rows (Simulation/SeasonLedger.cs:20, 135-139). The copy: `speakerId` = `voterId`, `targetId` = the claim's `targetId`, `week` = the claim's week, `conditionKind` = `vote-claim` (a new closed value), `audienceIds` = the player. Only a claim made to the player (source `told`) is copied, so the source needs no field; an overheard claim is never copied as proof. Content that needs two statements is two occasions. | Yes |
| **No weekly-cap field** | None is needed. Every weekly limit is a count over the week's retained rows: occasions (`occasionId` is unique, Simulation/DialogueRules.cs:47-48 and :129); grants used that week; the +1 and -1 from `time.appliedDelta` of the week's conversations. A +1 refused by its cap records proposed 1 and applied 0, so the same conversation never retries it. This needs the retention rule (§5.5). | Yes |
| **Grant:** owner, week, role, what set the role | `DialogueRoleGrantState.id`, `ownerId`, `week`, `role`, `sourceKind`, `sourceRevision`, `sourceSequence`, `sourcePhase`, `sourceNominees` | Yes |
| What used it, and expiry | `consumedEpisodeId`, `consumedRevision`, `status`, `expiryReason`, `expiredRevision`. The window a grant can be used in is fixed by its role (§4.1). | Yes |
| **Time receipt:** the outcome, window and reason | `DialogueTimeReceiptState.proposedDelta`, `appliedDelta`, `window`, `reason`; the rule version is the season's | Yes |
| When a +1 was spent | `spentRevision`, `spentSequence`, `spentWindowAction` | Yes |
| **Statement:** speaker, audience, condition, target, time | `DialogueStatementState.speakerId`, `audienceIds`, `conditionKind`, `targetId`, `answer`, `week`, `revision`, `sequence`. It expires by its condition: a veto condition ends at that week's result. | Yes |
| **Veto occasion:** the result and the decision | `DialogueVetoOccasionState` (holder, HoH, veto players, nominees at the result) and its `decision` (used, saved, replacement) | Yes |
| **Conditional promise** | A `veto_if` deal (§4.2): proposer, recipient, target, week, expiry, status, trust, who broke it, settled week. The link to its conversation is the deal's id. | Yes |
| **Consultation windows** | The existing four windows and phases (§4.1) | Yes |
| **The budget** | Counted from the store (§5.5) | Yes |

**What would reopen this.** Each of the following would need a field, and would take schema 31, never this pooled 30:
- pruning, with a summary kept in place of the rows;
- two statements in one conversation;
- a new window or phase;
- time or a grant earned outside a conversation.

## 7. Slices

| Slice | Rules | What | What the player sees | Size |
|---|---|---|---|---|
| **P0** | - | This note | Nothing | S, docs only |
| **P1** Probes | - | Headless measurements with the NPC world on: (a) how often rules 1's house-moment lock refuses, with the decision in §5.5; (b) how often V2's citation can appear; (c) how often Cody pleads in All-Stars houses of 8 and 12; (d) the most storylines and the most house events at 16 houseguests (§5.5). | Nothing; numbers | S |
| **P3** Recorder walk | 1 | The recorder's dialogue walk and the first rules-1 save | A save stopped at the campaign conversation | S |
| **D1** The chip | 1 | `SeasonBuilder.Choice` gains an in-memory switch. `ApplyFresh(fresh, options)` writes `Offered` (1) when it is set, and 0 otherwise. The chip on the cast-screen footer, with a caption checked unused. The digest observer and `CurrentOldSaveTrace` accept rules 1 without loosening the rules-0 checks. A rules-1 fixture saved mid-conversation, with its manifest cell, at the schema D1's build writes (29 if it lands before the bump). DialogueRules.cs:19 and :97 become `>= 29`, unless H1b made that change first (§6 item 1). StressHouseTests' regex, ShippedRulesTests and EpisodePlayModeTests.ShippedRules move. Layouts at text sizes 1.0, 1.2 and 1.4, with label boxes at 1.3x their font. Plus the rest of the master plan's D1 scope: a PlayMode test of the V2 Cite path, the dereference check at Runtime/Episode/EpisodeDirector.Story.cs:68-71, and the rewrite of the pinned refusal at Tests/EditMode/LegacyDigestSchema29ObserverTests.cs:269-299. | A new chip beside the house size, off. In a season started with it on, a nominated player still on the block in the Campaign gets the three-step conversation from the Emotional "Ask for their vote". It costs and rolls the same as the plea, and survives a reload. | M |
| **D2** Foundation and the first consequences | 2 | Starts from H1b's schema-30 bump merged into this branch, so its proofs run at 30 (§6 item 5). `Version` as a bound with `IsDefined`; per-version bounds; the content registry; entry by action or grant (the tick fix); statement and memory evidence; rules 2 refused below schema 30. The vote-word campaign tree. Rules 0 and 1 stay byte-identical. | In a chip season, the voter answers with their claim, a deflection or a concern. The player replies with a fact they know, the relationship, pressure or a polite exit. The voter reacts ("They warmed to you", "That landed badly"). The player still chooses whether to make the plea. | L |
| **D3** The other trees | 3 | Memory, pressure and neutral campaign trees; the holder appeal. Verification adapters list the choices hidden inside a category. | More branches, and a conversation with the veto holder | M-L |
| **D4** The conditional veto | 4 | §4.2 | A nominated player can ask a veto player, before the veto, whether they would use it on them, and then ask them to promise. The promise shows on Your word. If that player wins, the veto meeting keeps or breaks it. If they do not, it lapses quietly. | L |
| **D5a** The nominee's grant | 4 | Grants written when the player is nominated, used up by a conversation; at most two free a week | A line saying one conversation this week is on the house | M |
| **D5c** Conversation time | 4 | The +1 and -1 receipts that `WindowBudget` reads (§4) | "One more conversation in this window", or "The argument cost you an opportunity" | M |
| **D5b** Consultations | 5 | The HoH and veto consultations and their grants (§4.1) | As HoH or veto holder, one conversation on the house | L |
| **D6** Measurement | - | Lab policies start and answer conversations. Paired 100-seed runs with dialogue on and off, after R7's B9, with NPC ticks on. The ROLE doc's diagnostics (:158). Pacing against 45 minutes at 8 players. The owner signs off the numbers. | Nothing; a report | L, mostly compute |
| **D7** On by default | latest | `ApplyFresh` writes `Shipped`, the latest frozen version, together with R7. Goldens and digests are re-recorded with the reasons stated. The fixture and manifest cell for a shipped season, a season-walk route through a conversation, and the largest text size. The owner decides whether the chip stays as an opt-out. | Every new season | M |

None of these slices adds a persisted field. If one ever did, it would be qualified alone and take schema 31.

## 8. Old seasons and tests

- **Gating.** Everything new is keyed to the version that introduced it, and rules 1 keeps its exact `== 1` behaviour. Rules-0 seasons and every committed fixture replay byte for byte: no new draw, log, id or `Change`. The canary is `s29-mode1-allweek-w3-veto-meeting`.
- **Saves.** Each version that a shipped path writes needs its old-save fixture and manifest cell, written by its own build: rules 1 from D1, and each later version, at schema 30, as the chip first writes it. This is on top of the role moments' values (ROLE-MOMENTS-PLAN §4.6).
- **EditMode-only tests.** Many dialogue tests run only in EditMode, which the dotnet subset never covers. Each slice runs them under Unity's Mono harness before its full set, and the pinned rules-1 cases stay unchanged.

## 9. Competition rules 5

- **The claim.** Competition rules 5 is claimed for PC5, the competition part of B9 (the props and competitions line), so no other slice takes it. PC6's new minigames take 6 or later.
- **No field.** It lives in the existing `competitionRulesVersion`, which validation bounds by `CompetitionRules.Current` (Simulation/EpisodeValidation.cs:134) and `ApplyFresh` writes (Simulation/ShippedRules.cs:29). So it needs nothing from schema 30.
- **When PC5 lands.** `CompetitionRules.Current` (Simulation/CompetitionRules.cs:41-42) and `CompetitionMiniGames.CurrentRules` (Runtime/Presentation/CompetitionMiniGames.cs:23) move together. Rules 5 freezes the first time `ApplyFresh` writes it.
- **Dialogue** never reads or writes competition rules.

## 10. Risks

- **Balance.** The Studier policy already wins 95% of eligible weeks and 73.5% of seasons at 8 players, against targets of 60-70% and 25-35%. Grants and +1 time push those numbers the wrong way, so D7 waits for D6.
- **The frozen reader.** Every dialogue-on save shipped at schema 29 widens `FrozenEpisodeV29`. Keeping rules 2 and later off schema 29 holds that to the pilot.
- **Pacing.** Every node is another click. D6 measures against 45 minutes at 8 players.
- **Free warmth.** The +3/-3 lands at most once a conversation, a conversation costs an action or a grant, and each NPC gives one occasion of a kind a week.
- **Privacy.** Previews show only public facts and what the player knows. Role moments' privacy tests (§5.3 of that plan) apply.
- **Captions are a contract.** The chip and every new answer get new captions, checked unused. Nothing is appended to an existing caption.
- **Two sessions share the branch.** This note claims dialogue rules 2 to 5, in dialogue's own field, and records competition rules 5 for PC5. It claims no schema number: schema 30 is the histories', provisional until the story session confirms it (ALL-STAR-HISTORIES-PLAN §10). It claims no command kind either: `StartDialogue` and `ChooseDialogue` carry every conversation (Simulation/EpisodeState.cs:697-700), and the next free kind stays 66. Story rules 10, the story session's fix for the pariah rate, still stands between R7 and D7.

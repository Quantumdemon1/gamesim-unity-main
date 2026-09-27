# Story-arc event system: gap map

This synthesises reader reports 01-08 and cross-checks them against each other. Where two readers disagreed, I reopened the cited files; section 0 records how each disagreement was resolved. Both trees were treated as read-only.

**Path conventions.**
- Unity paths are relative to `C:/Users/kelli/Gamesim Big Brother/Assets/Gamesim/`.
- Web paths are relative to `D:/gamesim-main/gamesim-main/`.
- Plans are under `Assets/Plans/`.

**Evidence tags.**
- **[V]** means the claim was read at the cited line. **[V, re-checked]** means I reopened that line myself during this synthesis.
- **[I]** means inferred from code that was read but not executed.

**Two senses of "reachable" in the web tree.** The D: tree is part-way through a Survivor reskin, so I report reachability at two levels:
- **"Current tree"** means reachable in the D: tree as it stands today.
- **"BB loop"** means reachable in the HoH, Nomination, Veto, Social week that the port copies. Reachability at this level is [I].

## Headline

1. **The web intends a CK3-style event layer, but in the current tree almost none of its consequences land.**
   - The intent is explicit: `src/components/game-hud/GameHUD.tsx:96,1456` says "CK3 Event Engine", and the headers of the ambient, phase-event, diary, activity and contextual-action systems say "CK3-style" [V, re-checked].
   - Three things stop the consequences from landing:
     1. The `SocialInteraction` phase is unreachable.
     2. The `game` and `relationshipSystem` handles that GameHUD holds are null.
     3. Every social and competition bonus is written but never read.
   - Section 0, rows 1-2, has the evidence. **"Copy the web" can only mean copying its templates, numbers and trigger rules, not its live behaviour.**
2. **Unity already has a deterministic, seeded, saved event layer at schema 13.**
   - It consists of `HouseEventState`, five producers, one-chapter `Storylines`, and `StoryModifierState` modifiers that the engine actually reads.
   - It already has a CK3-style pop-up: the house-event card.
   - What is missing:
     - multi-chapter arc state;
     - a wider set of consequence types;
     - more than one situation a week;
     - a way to reach the card from free roam;
     - any input from traits or lore;
     - a record of what the player knows.
3. **Neither tree models the headline Big Brother USA items.** There is no disqualification, showmance relationship, fight with lasting consequences, Have-Not status, penalty nomination or twist engine.
   - The web has only flavour cards for these: the crises `showmance_rumor`, `have_not`, `house_meeting`, `twist_announcement` and `pandoras_box`. None of them changes any game state.
   - Every one of these features is net-new relative to the web.
4. **The web's `branching-story-system.ts` is hand-written and deterministic apart from two `Math.random` calls. It is not AI-driven.**
   - Its five three-act stories are the only web precedent for a multi-chapter chain.
   - The Unity comment that dismisses it as AI-driven (`Simulation/Storylines.cs:21-24`) is wrong [V, re-checked].
5. **Any design has to satisfy eight hard constraints at once.** Each is covered in section 5.
   - the seeded `Roll(s)` generator;
   - a new rules-start week for each new rule;
   - one schema-14 bundle: a `FrozenEpisodeV13`, a migration, and the exact-shape save check;
   - the split between player arcs and the NPC ledger;
   - option labels that are button captions, command payloads and save values all at once;
   - custom houseguests;
   - prose that is saved as finished English;
   - the rule that any open panel pauses the house.

---

## 0. Where readers disagreed, and how it was resolved

| # | Disputed claim | Readers | Resolution |
|---|---|---|---|
| 1 | "Branching stories, storyline chapters and the conversation trigger are live" | 01 and 08 say live; 02 says the social phase is unreachable | **Dead in the current tree; reached only in the BB loop.** See the detail below the table. |
| 2 | Branching-story consequences are "applied" (08) or "probably skipped" (01) | 01, 08 | **Skipped [I, high confidence]**, along with almost every other event handler. Detail below. |
| 3 | `showmance_rumor` is "reached" (06, 08) or "dead in name only" (02) | 02, 06, 08 | **Dead in the current tree; live in the BB loop.** Detail below. |
| 4 | Six of the 24 cast fall back to the default NPC action repertoire (08) or two (06) | 06, 08 | **Two.** Only Dr. Will Kirby and Tyler Crispen lead with `Charming` (`Simulation/CastTemplates.cs:189,225`). Every other lead trait has its own case in `Simulation/NpcSocialActions.cs:97-116` [V, re-checked]. The comment at `NpcSocialActions.cs:89` that says "Six" is stale. |
| 5 | `mood` and `stressLevel` have no writer (08) or change on nomination (05, 06) | 05, 06, 08 | **They are written, but only by `NominationEffects`, and only to make them worse**, on an initial nomination (`Simulation/EpisodeEngine.cs:578-584`) [V, re-checked]. Nothing restores them and no rule reads them. |
| 6 | The Unity comment "branching is AI-driven" | 01, 04, 08 say it is wrong; 05 did not verify | **It is wrong.** The only nondeterminism in `src/systems/branching-story-system.ts` is `Math.random` at lines 82 and 486. The file makes no `fetch`, `invoke`, `supabase` or async call [V, re-checked]. |
| 7 | Ambient narration "never takes the asking slot" (08) or "closes the slot" (05) | 05, 08 | **Both are partly right.** Detail below. |
| 8 | "`CharacterAppearance` is being edited in the working tree" | 05 | **Wrong.** `git status` shows `Simulation/CharacterAppearance.cs` unmodified. The edits are to presentation catalogues: `Runtime/Presentation/CharacterAppearanceCatalog.cs` and `Uma/UmaAppearanceCatalog.cs`. The advice to freeze a private copy of the v13 appearance shape still stands. |
| 9 | Web storylines are "live" | 01 §7 | **Only generation is reached.** `GameHUD.tsx:408-422` generates storylines at the week boundary. The chapter dialog and the conversation trigger are inside `SocialInteractionPhase` and `ConversationsSection`, which are unreachable. The only place a generated storyline can surface is the recap side panel's title list. |

**Row 1 detail: why branching stories and storyline chapters are dead in the current tree.**
- The branching trigger returns unless `phase === 'SocialInteraction'` (`GameHUD.tsx:630`). The story beats (`:782`) and emergent events (`:592`) have the same check [V, re-checked].
- No live code ever sets that phase:
  - The only place that assigns it is the `SET_PHASE` normaliser (`contexts/reducers/reducers/game-progress-reducer.ts:64-69`), and nothing live dispatches it with that value.
  - TribalCouncil's `advanceToSocialInteraction` actually dispatches `ADVANCE_WEEK` (`components/game-phases/TribalCouncilPhase.tsx:59-60`), which leads to `ImmunityChallenge` (`game-progress-reducer.ts:96-136`).
  - Fast-forward from TribalCouncil also goes to `ImmunityChallenge` (`player-action-reducer.ts:350-371`).
  - `MergePhase` goes to `ImmunityChallenge` too (`MergePhase.tsx:24`).
  - All of the above [V, re-checked].

**Row 2 detail: why event consequences are skipped.**
- Every relationship change, arc update and reputation ripple sits inside `&& relationshipSystem` (`GameHUD.tsx:1917,1939`).
- `relationshipSystem` is created as `useMemo(() => relationshipSystemRef.current!, [])` (`contexts/game/GameProvider.tsx:228`). That runs during the first render, before `initializeGameSystems` assigns the ref inside an effect (`contexts/game/hooks/useSystems.ts:26`; the effect is at `GameProvider.tsx:93-95`). So it stays null.
- The same check guards:
  - proximity events (`:2148`) and interactive ambient events (`:2090`);
  - crises (`:1705`) and phase events and beats (`:1075`);
  - cutscenes (`:1987,2009`) and activity chains (`:2251`);
  - the loyalty-oath +5 (`:1221`) and alliance invitations (`:1581,1596`).
- **Emergent events are the only exception.** They dispatch `UPDATE_RELATIONSHIPS` directly (`:1758-1764`).
- All of the above [V, re-checked]. So reader 02's "live now" for the relationship half of the proximity and ambient consequences is wrong.

**Row 3 detail: why `showmance_rumor` is dead in the current tree.**
- The call site runs on every phase change.
- But the eligibility filter `t.phases.includes(phase)` (`systems/mid-week-crisis-system.ts:574-576`) only accepts `SocialInteraction`, `HoH`, `PoV` and `Nomination` (`:61-521`; `showmance_rumor` is at `:342`). None of these occurs in the live loop.
- `'HoH'` can be reached only through the legacy `advance_week` / `eviction_complete` action (`player-action-reducer.ts:895-950`). Its senders are:
  - the eviction screen, which is no longer rendered;
  - the `'Tribal Council'` branch of fast-forward (`hooks/useFastForward.ts:67`), which uses the legacy spelling that live play never produces.
- All of the above [V, re-checked].

**Row 7 detail: how ambient narration interacts with the weekly slot.**
- It is true that `NarrateHouse` never asks the player anything.
- But `HouseEvents.Ready` refuses if an event of *any* kind already exists for the current week (`Simulation/HouseEvents.cs:271`), and `NarrateHouse` runs straight after `OfferHouseEvent` at the Eviction→Social transition (`EpisodeEngine.cs:249-253`) [V, re-checked].
- Results [I, from code read]:
  - The Social window of week N is always closed.
  - A proximity event can land only in the Campaign phase of week N+1.
  - When it does, it pre-empts that week's storyline, emergent, crisis or catalogue draw.

---

## 1. What the web already has

"Lands?" asks whether the system's relationship consequences take effect.

### Event and story systems

| System | Essence | Current tree | BB loop | Lands? | Port as-is? |
|---|---|---|---|---|---|
| `storyline-system.ts` with 3 fallbacks and the `generate-storyline` edge function | AI storylines of 2-4 chapters, with modifiers, preconditions, and consequences that create deals, alliances and promises | Generation only (`GameHUD.tsx:408-422`); chapters never shown | Reached (`ConversationsSection.tsx:186-209`) | **No.** `game` is null (`GameProvider.tsx:227`). The adapter's functions do nothing (`utils/storyline-game-adapter.ts:1-5,50-90`). `getCompBonus` has no caller (`:964`). | The fallbacks are already ported. The AI route is excluded on purpose (`MASTER-PLAN.md:58`). Use the model as a **target shape** (chapters, preconditions, the `personal_growth` and `secret_scheme` categories, alliance, deal and promise outcomes), not as working code. |
| `branching-story-system.ts` | 5 hand-written three-act events (Conspiracy, Confession, Power Shift, Betrayal Evidence, Secret Alliance Offer). Fires from week 3 when an arc reaches intensity 60, at least 3 weeks apart, with a 60-95% chance (`:65-83`) | Dead (`GameHUD.tsx:630`) | Reached (`:637-646`) | **No.** Every relationship write is behind the `relationshipSystem` check (`:1917,1939`). Bonuses are never read. Memories read `outcomeText`, which nothing fills. Only the cooldown and the log entry take effect (`:1959-1968`). | **Yes, as content.** It is the best multi-chapter precedent. Replace `Math.random` with seeded rolls. Write real text for Acts II and III (they currently reuse generic lines, `:541-545`). Fix the random epilogue pick and the "forgive leads to expose" mapping. |
| `recurring-story-beats.ts` | Hot Take, Alliance Check-In, Gut Feeling, Jury Pulse: at most one a week, at a 50-75% chance | Dead (`:782`) | Reached | No (behind the check at `:1075`) | Yes. Cheap content. |
| `house-event-system.ts` | AI dilemmas plus 6 fallbacks. The only web path that asks the AI for continuity across weeks. | Dead in three ways: the phase gate, the null-`game` guard (`SocialInteractionPhase.tsx:520`), and a `gameLog` that doesn't exist | Guarded out by the null `game` | No | The 6 fallbacks are already ported. |
| `emergent-event-system.ts` | Confrontation, secret deal and pile-on events driven by grudge and arc thresholds, plus arc-forced events | Main trigger dead (`:592`). A recheck after any Δ ≤ −20 is live. | Reached | **Yes**, via `UPDATE_RELATIONSHIPS`, including NPC↔NPC changes. But "Ask to join" (`'form'`) is ignored, and `house_meeting` is never called. | Partly ported (arc-forced events). The grudge-driven ones need a grudge ledger. |
| `mid-week-crisis-system.ts` | 13 interrupts, including showmance_rumor, have_not, house_meeting, twist_announcement, pandoras_box, luxury_competition and homesick_moment | Call site reached but the eligible list is empty, so dead (section 0, row 3) | Reached | Relationship change behind the check (`:1705`). `reputationShift` is never applied. The twist, Pandora's box, Have-Not and luxury comp change no state. | 1 of 13 ported. The BB-USA templates need new game-state consumers before they mean anything. |
| `ambient-event-system.ts` | Flavour toasts plus 6% interactive mini-choices | **Live** (`:651-680`) | Live | The relationship change is behind the check (`:2090`). Memory, bus, log and the (never-read) social bonus do land. | Lines ported; interactive choices not ported. |
| `phase-event-system.ts` | Mini-events at phase boundaries (post-HoH, post-Nomination, pre-vote, post-PoV) | Dead (no Survivor transition key matches) | 4 keys reached | No (`:1075`) | Worth porting as phase-anchored beats. |
| `proximity-event-system.ts` | Walk-by mini-dialogs: grudge confrontation, npc-secret, npcs-arguing, and so on. Each NPC's first encounter each week is guaranteed. | **Live** (`HouseScene.tsx:2336-2387`) | Live | **No.** Relationship, memory and bus writes are all behind the check (`:2148`). Only the action counter and the outcome panel run. | 1 generic situation ported. The template set is good trigger grammar. |
| `hooks/useNPCSocialEngine.ts` | NPCs walk up to the player (friendly, deal, check-in, confrontation, intel), with line banks for real alumni (`:47-76`) | Live | Live | Not traced by any reader | The closest thing in the web to in-character writing. |
| `social-cutscene-system.ts` | Branching scenes: topic scenes and 12 pull-asides. Branching depends only on the player's choice, never on NPC traits. | Proximity and pull-aside: live. Join-chat and eavesdrop: dead (sent on `window` but listened for on `document`). | Live | No (`:1987`) | Useful structure for scenes. |
| `player-activity-chains.ts` and pop events | Follow-ups after the player cooks, swims, games and so on | Live trigger | Live | No (`:2251`) | Matches the queued work in HOUSE-LIFE-PLAN M9. |
| `deal-coordination-events.ts` | Deals falling due | Dead (phase names don't match) | Reached | Relationship change only; **does not bind votes** | Low value. |
| Milestone dialog plus loyalty oath | Crossing 25, 50 or 75 unlocks a type of deal; at 75 the player can swear an oath | Live | Live | The oath is enforced in the reducers (`nomination-reducer.ts:41-120`). The +5 for swearing is behind the check (`:1221`). | Already ported (`WebLoyaltyOaths`). |
| `diary-room-system.ts` | CK3-style monologue that builds a player persona | Live | Live | Social, competition and jury effects apply; `reputationDelta` and `stealthModifier` are dropped | Ported for post-eviction only. |

### Supporting mechanics worth keeping
- **Grudges** (`systems/grudge-system.ts:22-29,58-62,94-101`): severity by cause (alliance betrayed 80 down to voted against 40). A repeat grudge stacks at half value. Severity decays by 2 a week, and the grudge is forgiven below 10 once the relationship is above 40.
- **Arc escalation levels** at 25, 50, 75 and 90 (`relationship-arc-tracker.ts`).
- **Promise-leak odds** (`promise/promise-effects.ts:17-47`): 20% base, 60% if close to the victim, 80% if in the same alliance.
- **Relationship tiers that unlock deal types** (`models/relationship-tier.ts:61-109`).
- **The only dice skill check**, `calculateSuccessChance` (`contextual-action-generator.ts:46-66`): relationship, trust, alliance and deal bonuses, penalties for rivalry and broken deals, clamped to 5-95%.
- **First impressions as a guess-the-trait challenge** (`CampArrivalPhase.tsx:104-137`).
- **All-Star personality profiles and stat overrides** (`data/allstars-templates.ts:67-309`). These are reached in the web (`GameSetup.tsx:174-200`) but are **not ported**.

### Web code not worth porting
- the AI prompts, whose payload keys mismatch between client and server (`Castaway` vs `houseguest`, `activecastaways` vs `activeHouseguests`);
- the null adapter;
- bonuses that are written but never read;
- Survivor wording such as "The island is buzzing" (`mid-week-crisis-system.ts:353`).

---

## 2. What the Unity port already has, and how faithful it is

| Piece | Where | Fidelity to the web |
|---|---|---|
| **One event record** | `Simulation/HouseEventState.cs:19-80`. Each record has a kind, title, narrative, involved ids, stored choices, resolved flag and outcome. | A flattened copy of `models/house-event.ts`. Choices are stored when the event is drawn and never regenerated. **Better than the web** for replay. |
| **Catalogue events**: 6 templates, plus 2 week-keyed rephrasings each | `Simulation/HouseEvents.cs` | The numbers match the web's fallbacks. The Survivor terms were re-skinned back to BB. Dropped: the 30% gossip ripple, NPC memories and the storyline social multiplier. **Deviation:** the web only displays `trustChange`; Unity writes it into the ledger (`EpisodeEngine.cs:1252-1260`). |
| **Four more producers** | `Simulation/HouseEventSources.cs` | Ambient: the line pools only, once a week, with no interactive choices. Proximity: 1 generic situation where the web has about 13. Emergent: arc-forced events, plus the web's NPC-NPC "secret deal" re-aimed at the player as "Nobody has said it out loud" (`:274-312`). Crisis: "Caught Eavesdropping" only, 1 of 13, at a flat 25% (`EpisodeEngine.cs:1118-1122`, which explicitly says the number is "not the reference's"). |
| **Storylines** | `Simulation/Storylines.cs`, `StorylineState.cs` | The 3 one-chapter fallbacks; `MostAtOnce = 2`, `StaleWeeks = 3`. **Deviations:** a storyline can repeat after its cooldown (the web never repeats one); the competition bonus is really read (it is dead in the web); the social bonus becomes extra actions (it is a percentage in the web). |
| **Modifiers** | `StoryModifierState` (`StorylineState.cs:70-94`) | Read by `CommonCompetitionBonus` (`EpisodeEngine.cs:365-366`) and `SocialActionBudget` (`:502-510`). **Never displayed.** |
| **Relationship arcs** | `Simulation/WebRelationshipArcs.cs` | Parity-tested against the web. The escalation announcement is computed but discarded (`EpisodeEngine.cs:1714`). |
| **The "CK3 pop-up"** | `EpisodeHud.Decisions.cs:88-161` and `EpisodeDirector.Conversation.cs:489-504` | Mockup-04 card: eyebrow, title, narrative, two-column choice tiles with a risk word, context strip, and a camera shot of the people involved. It has no dismiss button. **Impacts are not shown before the player commits.** |
| **Adjacent systems that already produce BB drama** | Various | Loyalty oaths; promises with witness spread (`EpisodeEngine.cs:1646-1653`); deals; NPC alliances; NPC `Confront` at −8 (`NpcSocialActions.cs:304-319`); the player verbs `SpreadRumor` public callout (`EpisodeEngine.cs:1342-1385`) and `HouseMeeting` "air the dirty laundry" (`:1395-1428`); `SpreadLie` with 30% discovery. |
| **Voice** | `Simulation/HouseDialogue.cs` | Authored, not ported. 17 situations × 3 week-keyed variants × 5 voices plus a fallback. Voices are keyed by saved id (`:560-570`). |
| **Diary** | `WebDiaryRoom.cs` | Post-eviction reflection only. The post-nomination and mid-week prompts are unscheduled. |
| **3D staging** | Runtime | NPC pairs meet at 6 venues and "argue" when their topic is `tension` or `rivalry` (`EpisodeDirector.NpcSocial.cs:393`). Also: the Nearby "Listen in" card, the hot-tub companion, ceremony takeovers with reactions, the live feed, the weekly recap "What happened to the house", and the notebook profile. |

**Deliberately not ported:**
- the AI routes (`MASTER-PLAN.md:58`; they may return only if the output travels inside the recorded command, `:234-243`);
- branching stories, beats and phase events;
- 12 of the 13 crises, interactive ambient choices, cutscenes and activity chains;
- the grudge ledger and dialogue trees;
- All-Star `statsOverrides` and `personalityProfile`.

---

## 3. Half-built: data without UI, or UI without consequences

| Item | State | Evidence |
|---|---|---|
| `EpisodeCommandKind.ProgressStoryline` | Its ordinal is reserved, but `Execute` has no case for it. It falls through to `Social()`, which throws "Unsupported social action." | `EpisodeState.cs:429`; `EpisodeEngine.cs:141,780` [V, re-checked] |
| `HouseEventKind.Phase` | Declared; nothing produces it | `HouseEventState.cs:104` |
| `StorylineState` | Has a chapter pointer (`eventId`) but no chapter index, choice history, cast or flags | `StorylineState.cs:19-36` |
| Active modifiers | Change competition scores and the action budget but are never shown, apart from a "storyline +N" term in the competition briefing | `EpisodeDirector.Challenge.cs:136-138` |
| `phaseEventSocialBonus` | The HUD advertises "Carrying a +N social bonus", but nothing uses it | `EpisodeDirector.cs:1269-1270` |
| Arc escalation | Computed and thrown away. Arc narratives were also removed from the notebook because they lack provenance. | `EpisodeEngine.cs:1714`; `UNITY_PORT_IMPLEMENTATION.md:187` |
| `mood` / `stressLevel` | Written only on nomination, only made worse, shown on the HUD, read by no rule. Ready-made slots for stress and coping mechanics. | `EpisodeEngine.cs:578-584`; `EpisodeDirector.cs:905` |
| `WebNpcMotives`, `WebNpcActivityRules`, `WebNpcActivityCatalog` | Ported and parity-tested, but nothing calls them outside tests. They include the "seek" intents `get_to_know`, `confront` and `gossip_share` (`WebNpcActivityRules.cs:162-170`). | grep, re-checked |
| `homeRoom` | Authored for every card; read by nothing | grep, re-checked |
| `motive` | Only printed as the Talk log line; not editable in the creator | `EpisodeEngine.cs:739` |
| Final-speech storyline fragments | Keyed `social_drama`, `power_play` and `survival`; `GenerateNative` passes no storylines | `WebFinalSpeechCatalog.cs:82-89`; `WebFinalSpeeches.cs:39-50` |
| `WebLoyaltyOaths.CampaignProposal` | No caller | reader 06 |
| Diary post-nomination and mid-week prompts | Exist but are unscheduled. The reputation and stealth effects are dropped. | `WebDiaryRoom.cs:97-98`; `UNITY_PORT_IMPLEMENTATION.md:49` |
| Arguments | An animation flag driven by the NPC conversation topic. The player has no role in them, and they have no consequence and no escalation. | `EpisodeDirector.NpcSocial.cs:350,393` |
| Hot-tub companion | The only place an NPC walks over to the player. Presentation only. | `EpisodeDirector.HouseLife.cs:49-93` |
| **Romance, drama and secret UI art** | Imported, but no runtime code references it. This is a new finding; no reader reported it. The files are `chip_romance`, `overlay_romance_corner`, `line_romance`, `romance_alert`, `drama_alert`, `secret_alert`, `social_alert`, `icon_drama` and `investigate_button`. | `Editor/UiPackCatalogue.cs:234,281,350,505`; `Resources/Packs/Pack4_Presentation/SocialEvents/`; nothing matches in `Runtime/Presentation/PackArt.cs` [V, re-checked] |
| Have-Not room | Dressed as a set. The simulation has "no have-not mechanic". | `MASTER-PLAN.md:442` [V, re-checked] |
| Proximity events | The director auto-submits `WitnessProximity`, a command the player never chose. The answer is only available at the episode station, within 3 m. | `EpisodeDirector.Conversation.cs:320-346`; `EpisodeDirector.cs:507-508` [V, re-checked] |
| Nearest thing to a showmance | The emergent "Nobody has said it out loud" event, fired at `UnspokenPairWarmth = 60` | `HouseEventSources.cs:38,274-312` |
| Relationships that exist before the show | Only in the shipped `ContentCatalog` scenario. Seasons built from the cast screen start everyone at 0. | `ContentCatalog.cs:161-166`; `SeasonBuilder.cs:124-127` |

**Defects in the substrate that any arc system would inherit:**
- **A stale `{NOMINEE}` or `{HOH}` can be cast.** `Roles` takes `state.nominees.FirstOrDefault` without checking the person is still active (`HouseEvents.cs:239`). The nominee list is only cleared at `week++` (`EpisodeEngine.cs:165`), after the draw at `:251-252`. So an event can name the houseguest who was just evicted, or the outgoing HoH [V, re-checked].
- **Orphaned chapters.** An abandoned storyline leaves its chapter unresolved and still pending (`Storylines.cs:351-362`).
- **Backlog.** Events pile up oldest-first: `Pending` returns the first unresolved one and `OfferHouseEvent` never checks it.
- **Mistimed storyline.** "Back Against the Wall" is drawn *after* eviction night.

---

## 4. Missing entirely, relative to the user's ask

| Ask | Web | Unity | What a design minimally needs |
|---|---|---|---|
| **DQ or expulsion** | None: a grep for disqualif, expel or self-evict finds nothing in `src/` or `supabase/` [V, re-checked] | None. `ContestantStatus {Active, Evicted, Jury, Winner, RunnerUp}` (`EpisodeState.cs:26`). **Every evictee becomes a juror** (`EpisodeEngine.cs:313,664`); there is no pre-jury. | Append an `Expelled` status. Decide explicitly whether they sit on the jury. Clean up alliances, oaths, ballots, promises and deals in the same command. Repair the phase invariants (section 5, item 11). Add a rules week and a schema bump. **The heaviest item on this list.** |
| **Fights or arguments with teeth** | NPC confront pop-ups and NPC↔NPC arguments (`npc-social-behavior.ts:799-857`, dead now); proximity `npcs-arguing` | NPC `Confront`, player `SpreadRumor` and `HouseMeeting`; argue animation only | Escalation state (argument, then warning, then strike); public witnesses; production intervention; staging for 3 or more people; new `Reaction` values; consequences that the vote and threat assessments read. |
| **Showmances** | The rumour card only | None (grep, re-checked). Unused romance art exists. | A sticky pair relationship; candidate signals (mutual warmth, time together, `partnership` deals); secrecy and exposure; consumers in threat and jury. NPC-NPC showmances must go through the ledger. |
| **Alliances formed through story** | Storyline consequences can create alliances (dead); emergent `'form'` is ignored | The `FormAlliance` command and `NpcAlliances` exist, but a story choice cannot create an alliance | A typed effect on a choice that calls the existing alliance producers. |
| **Bonds** | Friendship and rivalry arcs; the oath at 75 | The same | Sticky relations (Best Friend, Nemesis) with permanent ledger types. |
| **Progressive lore discovery** | None: the whole bio is visible at cast selection | None: the profile shows the whole bio from day one (`EpisodeDirector.Journal.cs:370-397`) | A static lore catalogue keyed by template id; persisted ids of facts the player has revealed; a "What you've learned" list in the profile. |
| **Backstories** | A 1-2 sentence bio. All-Stars have a ~150-word `personalityProfile` (reached in the web, not ported). | A one-sentence bio and a motive | Authored beats per template (family, job, home connection, a secret, hot-button topics). Careful rules for real alumni (section 6). |
| **Behaviour true to each character** | No event system reads traits (grep, reader 03). Only the player's trait bonus exists. | Events read no traits, bio or motive. NPC verbs use the lead trait only. 5 of 24 houseguests have a voice. | Options gated on traits; NPC responses weighted by traits (CK3's `ai_chance`); voice buckets by trait for anyone unvoiced. |
| **Secrets, knowledge and leverage** | Only as story themes | Memories only, capped at 30 per owner (`EpisodeEngine.cs:1782-1786`) | A bounded, persisted knowledge store (who knows what) and one-use leverage. |
| **Real risk** | Risk is a label, apart from `calculateSuccessChance` | Risk is a label | A rolled success-or-backfire outcome on risky options, drawn from a keyed side stream. |
| **Several beats a week** | Each producer has its own cadence | One situation per week, of any kind | Replace the slot rule with per-arc cadence and a cap on pending chapters. |
| **Have-Nots** | Crisis flavour and a 3D room | A dressed room only | Status, selection, and a consumer (modifier, stress). Already queued in the schema-14 bundle (`HOUSE-LIFE-PLAN.md:356,716-721`). |
| **Penalty nominations** | None | None | Cheap as a "forced nominee" rule that `NominationCandidates` and the NPC HoH must respect (`EpisodeEngine.cs:190,488`). Expensive as a third nominee, because two nominees are assumed in validation, the tally and the vote evaluator. |
| **House meetings** | A crisis card and an emergent template that is never called | A player verb only | A house meeting called as an event, with 3+ people staged. |
| **Twists** (Pandora's box, luxury comp, battle back, double eviction, America's vote) | Text-only crisis cards | None (grep; only one comment mentions "double eviction") | Each needs its own rules and consumers. Out of scope for a first arc pass. |
| **Letters from home, HoH room reveal, lockdowns, production Diary Room summons** | Homesick crisis only | The diary room exists, but production never summons the player | The diary room as the production channel (warnings, strikes, private reflections). |

---

## 5. Hard constraints any design must satisfy

1. **Determinism and the RNG stream.**
   - `Roll(s)` advances `randomState` one draw at a time (`EpisodeEngine.cs:1777-1780`) [V, re-checked].
   - `Change` costs 1 roll, or **2** when an `eventType` is passed (`:1657-1676`). House-event answers pass `"house_event"` (`:1249`), so **each impact costs 2 rolls, and which option the player picks changes how many rolls are spent.**
   - `BeginStoryline` rolls even when no template is available (`:1135`).
   - Safe patterns:
     - draw only after the guards pass (`:1157-1160`);
     - use keyed hashes (`WebLoyaltyOaths.cs:107-112`) or the separate NPC stream (`NpcSocialState.cs:19-20`);
     - log last, so ids built from `nextSequence` don't shift.
   - Wording varies by week, never by roll (`HouseDialogue.cs:549-558`; pinned by `Tests/EditMode/WritingPassTests.cs`).
2. **Rules-start weeks.**
   - Existing boundaries: `blocRulesStartWeek` (`EpisodeState.cs:232`), `socialBudgetRulesStartWeek` (`:263`), `dealRulesStartWeek` (`:288`), `eventRulesStartWeek` (`:331`), `storyRulesStartWeek` (`:359`), `npcSocial.rulesStartWeek` (`NpcSocialState.cs:12`), and `competitionRulesVersion` (`:198`) [V, re-checked].
   - Gates: `HouseEvents.cs:267`, `Storylines.cs:215`.
   - Migrations set each boundary to week + 1 (`EpisodeSaveMigrations.cs:87-91,128-132`), and validation keeps it at or below week + 1.
   - Any draw at an `Advance` transition fires in every season, so it must be gated.
3. **Schema migration cost.**
   - The version is 13 in `EpisodeState.cs:197`, `EpisodeValidation.cs:22`, `EpisodeSaveValidation.cs:13` and `EpisodeSaveMigrations.cs:23-24` [V, re-checked].
   - The save shape is checked exactly: `SaveJson.CheckDtoShape` plus `MissingMemberHandling.Error` (`EpisodeSaveStore.cs:153,184,223`).
   - **There is no `FrozenEpisodeV13` yet.** `Runtime/Persistence` holds V2-V12 only.
   - `CharacterProfileStore` also checks shape exactly against `ContestantState` (`CharacterProfileStore.cs:132`). **Adding a lore field to `ContestantState` breaks every saved houseguest library file.** Keep lore in a static catalogue.
   - Template catalogues become save data: an unanswered chapter reads its modifier from the catalogue by option index (`EpisodeEngine.cs:1182-1184`). Keep catalogues append-only.
   - Validation caps (`EpisodeValidation.cs:129-171`): 400 events, 8 choices, 32 involved ids, 2000-character narrative, 100 storylines, 40 modifiers.
   - `HOUSE-LIFE-PLAN.md:716-721` already queues a schema-14 bundle. **Merge into it; don't add a second bump.**
4. **Player arcs versus the NPC ledger.**
   - `ChangeWithRoll` writes arcs, including **both** NPCs' arcs when neither side is the player (`EpisodeEngine.cs:1689-1690`) [V, re-checked].
   - `NpcSocialActions.Act` is the correct router: `Change` when the player is involved, otherwise `RelationshipLedger.Move` plus `Record`, which draw no roll and write no arc (`NpcSocialActions.cs:401-411`; `RelationshipLedger.cs:128,164`) [V, re-checked].
   - Existing leaks: the veto ceremony (`EpisodeEngine.cs:605-607`) and NPC conversation completion (`EpisodeNpcSocial.cs:171-175`) [V, re-checked].
   - Arcs feed voting and threat (`WebEvictionVoting.cs:244,382`; `ThreatAssessment.cs:130-135`), so **never treat a raw arc as proof that the player has history with someone.**
5. **HUD caption contract.**
   - `EventChoiceCaption(label) => label` (`Runtime/Episode/EpisodeHud.cs:102`) [V, re-checked].
   - The engine matches the choice by its label text (`EpisodeEngine.cs:1240-1242`), and the director commits `text: label` (`EpisodeDirector.Conversation.cs:499`).
   - The risk word is a separate tag (`EpisodeHud.cs:110-113`). The control's name is its caption (`EpisodeHud.Decisions.cs:127`). The glyph is chosen by keyword-matching the caption (`:164-174`).
   - So a label is at once a caption, a command payload and a save value. **Put odds, stakes and trait hints in tags or descriptions, never in the label.**
6. **Custom houseguests.**
   - They get the ids `custom-N` (`SeasonBuilder.cs:111`).
   - Voices are chosen by saved id only; "Imported IDs are not inferred from displayed names" (`HouseDialogue.cs:569`).
   - A blank draft defaults to the archetype "The Newcomer" and the generic motive (`CharacterDraft.cs:52-54`). The creator does not expose motive or archetype.
   - Web imports drop the bio and other card fields (`WebSaveImporter.cs:33-34,233`).
   - The player can be any card (`sourceTemplateId`).
   - **Every event must be castable by role and trait. Authored lore is an optional overlay, applied only when that card is an NPC in this season.**
7. **Localisation.**
   - English is the key, and no translation tables ship (`Runtime/Presentation/Localisation.cs:14-34`).
   - The Simulation assembly has no engine references, and it persists composed English with names already substituted (`HouseEvents.cs:251-260`; `Log`).
   - Some prose doubles as a lookup key: the memory prefixes at `HouseDialogue.cs:146,298`.
   - Nothing substitutes pronouns.
   - **New content should persist `{contentId, optionId, variant, roleIds}` and render the text at display time.**
8. **The player's knowledge boundary.**
   - Presentation may only show what the player witnessed (`Presentation/DecisionContext.cs:7`; `Episode/HouseConversationCaption.cs:10-11`; `EpisodeHud.Decisions.cs:25-27`).
   - NPC lines may use only the speaker's own knowledge (pinned by `HouseDialogueTests`).
   - Revealed lore must be something the player learned.
9. **Limits on 3D staging.**
   - **Any open panel pauses the NPC world and clears talk and argue flags** (`EpisodeDirector.NpcSocial.cs:76-78,317-329`) [V, re-checked].
   - The event shot is used only when everyone involved is within 3 m of their centre (`EpisodeDirector.Camera.cs:102`) [V, re-checked].
   - Meeting leases are pairs only and world-only (`House/HouseMeetingCoordinator.cs:10-31`) [V, re-checked]. The only 3+ gathering is the yard competition staging (`HouseMeetingCoordinator.CompetitionStaging.cs:31-50`).
   - The event card appears only inside the station panel (`EpisodeDirector.cs:1253-1258`, opened within 3 m, `:507-508`).
   - Ceremony cards take no input (`Presentation/CeremonyOverlays.cs:5-37`).
   - Unknown event kinds are silently ignored by the takeover, sting, audio, House Vibe and recap tables.
   - `Reaction` is append-only (`CharacterPresentation.cs:66-77`). Reduced motion drops arguing and reactions (`:447-472`).
   - The HUD is rebuilt in full on every render (`EpisodeHud.cs:185-203`). The conversation dial is fixed at 7 petals (`EpisodeDirector.cs:1101`).
   - The resulting staging order is: **free-roam scene, then a non-modal invitation, then a modal choice over a frozen scene, then the public payoff.**
10. **Weekly pacing slot.** `HouseEvents.Ready` allows one event of any kind per week number, ambient included (`HouseEvents.cs:265-271`). Section 0, row 7 describes the consequence.
11. **Phase invariants for removing anyone** (`EpisodeValidation.cs:183-212`):
    - at least 4 active in regular weeks, at least 3 in free time;
    - the HoH and nominees must be active;
    - the veto seat count depends on the number active;
    - exactly 2 nominees (`WebEvictionVoting.cs:161-162`; `EpisodeEngine.cs:286-288`);
    - exactly 2 finalists (`:685`).
    - [I] The cheapest legal window for a removal is the Social phase with at least 4 active.
12. **Enums are append-only**: `EpisodeCommandKind` (`EpisodeState.cs:400-430`), `ContestantStatus` (`:26`), `Reaction`, and the phase and eviction-stage enums.
13. **Presentation never commits or rolls.** Every consequence is a single engine command (`EpisodeDirector.Conversation.cs:132-134`; `EpisodeDirector.HouseActivities.cs:129`).
14. **Real people.** Twelve All-Stars appear under their real names, with bios that cite their actual seasons (`CastTemplates.cs:178-249`). Rachel Reilly's bio quotes her real showmance catchphrase (`:236`).

---

## 6. Where the ask pulls against "the web is the reference"

- **The reference is partly broken and partly Survivor now.** The live D: tree cannot enter the social phase, and its event consequences are behind null handles (section 0).
  - Framing: *"the reference is the web's design intent under the BB loop"*, which is how the port already treats it. The owner kept Unity's richer jury over the live web's (memory: `web-version-is-the-reference`).
  - State this explicitly in any design, so that nobody later reports "the web doesn't do X" from the broken live path.
- **The ask matches the web's stated intent.** The web calls its dialogs a "CK3 Event Engine" (`GameHUD.tsx:96,1456`). CK3 framing is therefore faithful in spirit; what the web lacks is working wiring.
- **Features with no web precedent:** disqualification, showmance state, fights with teeth, Have-Not status, penalty nominations, twist mechanics, lore discovery, backstory reveals, trait-gated options, secrets and leverage, success rolls, and multi-week arcs beyond branching stories.
  - Framing: treat each one as a **declared native extension**, with its own rules week, listed in `MASTER-PLAN.md` and signed off by the owner.
  - Wherever possible, anchor each one to a web template that already names the idea: `showmance_rumor`, `have_not`, `house_meeting`, `twist_announcement`, `homesick_moment`, proximity `npc-secret` and `npcs-arguing`, and the emergent `house_meeting`. That way every new mechanic "completes" something the web started.
- **Where the web's continuity came from.** It came from AI prompting (`house-events/index.ts:19-50`). Unity bans network content unless it travels inside the recorded command (`MASTER-PLAN.md:58,234-243`). Arcs therefore have to be authored, deterministic state machines. **That is a deviation of method, not of design.**
- **Existing deviations need a decision on record** before building on them:
  - `trustChange` is written into the ledger;
  - modifiers are really read;
  - storylines can repeat;
  - the crisis chance is a flat 25%.
- **Real alumni.** The web itself casts real Big Brother players. Invented secrets, romances, fights or disqualifications about identifiable people are a content conflict whatever the web does. Either restrict their lore to documented game history or fictionalise the roster first.
- **Survivor residue.** Any copied web text must be converted back to Big Brother wording ("island", "castaways", "Tribal", "Idol").
- **The plans disagree with each other.** `MASTER-PLAN.md:442` says "this plan does not add" a Have-Not mechanic, while `HOUSE-LIFE-PLAN.md:356` queues Have-Nots for schema 14. Separately, MASTER-PLAN says to change the UI idiom before adding more panels (reader 04, `MASTER-PLAN.md:213`), and an arc tracker is a new panel. Resolve both with the owner.
- **Disqualification of the player.** Big Brother USA has expelled roughly five houseguests over 25+ seasons, each time for a clear-cut act (reader 08; web-sourced).
  - A player disqualification is a fail state. It must only follow a signposted ladder: a Diary Room warning, then a penalty, then removal.
  - Every step must be labelled on its option, never a surprise roll.

---

## 7. Top 15 facts a designer must not get wrong (ranked)

1. **Nothing in the current web tree's event layer is a working reference for consequences.** The social phase is unreachable (`GameHUD.tsx:630`; `TribalCouncilPhase.tsx:59-60`), and `relationshipSystem` and `game` are captured as null (`GameProvider.tsx:227-228`; `useSystems.ts:26`). Copy the templates, numbers and triggers; design the consequences yourself.
2. **Every roll on the main stream re-rolls the rest of the season.** Each impact routed through `Change` costs 2 rolls, and the option the player picks changes the roll count (`EpisodeEngine.cs:1249,1662-1676`). Gate new rolls behind a rules week, draw after the guards, and prefer keyed hash streams.
3. **The next save version is 14, and it needs:** a `FrozenEpisodeV13` (which doesn't exist yet), a v13→v14 migration, exact-shape DTOs, and the literal-13 sites updated. Bundle it with the queued schema-14 list (`HOUSE-LIFE-PLAN.md:716-721`). Keep lore out of `ContestantState`, because `CharacterProfileStore.cs:132` also checks shape exactly.
4. **Only player-involved changes go through `EpisodeEngine.Change`.** NPC-NPC fights and showmances go through `RelationshipLedger.Move`/`Record` via the `NpcSocialActions.Act` pattern. Arcs already leak from NPC-NPC paths (`EpisodeEngine.cs:605-607,1689-1690`; `EpisodeNpcSocial.cs:171-175`).
5. **An option label is a button caption, a command payload and a save value** (`EpisodeHud.cs:102`; `EpisodeEngine.cs:1240`). Author labels as constants; never compose them with numbers or names.
6. **There is one situation per week number, ambient included** (`HouseEvents.cs:271`). The Social window of week N is always closed, and a proximity event in Campaign pre-empts the next storyline. CK3-style chains need this rule replaced, not layered over.
7. **`branching-story-system.ts` is deterministic, hand-written content.** It is the legitimate "copy the web" route to multi-chapter arcs. `Storylines.cs:21-24` is wrong to call it AI-driven.
8. **There is no removal path other than eviction, and every evictee becomes a juror** (`EpisodeEngine.cs:313,664`). A disqualification needs a new status, cleanup inside the same command, and phase repair within the validation invariants (`EpisodeValidation.cs:183-212`).
9. **The web has no precedent for disqualification, showmance state, fights, Have-Nots, penalty nominations or twists**, only text-only crisis cards, and those are dead in the current tree. Frame each as a declared extension with its own rules week.
10. **Events read no traits, bio or motive in either tree.** NPC verbs read only the lead trait. Two of the 24 cast (Dr. Will, Tyler) get the default repertoire, not six. Only 5 ids have a voice, and only 3 of the 7 NPCs in the default eight-person season.
11. **Content must cast for `custom-N` houseguests, imported houseguests, and the player playing any card.** Cast by role and trait. Authored lore keyed by `sourceTemplateId` is an overlay, and must never be applied to the player's own card.
12. **The simulation saves finished English with names already filled in**, and some prose doubles as a lookup key. New content must save ids and variants and render text at display time, or it can never be localised and every copy edit breaks replays.
13. **Opening any panel freezes the house** (`EpisodeDirector.NpcSocial.cs:76-78,317-329`). The event card lives at the station, and the camera shot needs the cast gathered (`EpisodeDirector.Camera.cs:102`). Stage in this order: scene, invitation, modal choice, payoff.
14. **"A bonus nothing reads" is the failure this codebase keeps repeating.** In the web, `phaseEventSocialBonus` and `phaseEventCompBonus` are written but never read. In Unity, `phaseEventSocialBonus` is advertised but unused, and modifiers are used but invisible. Every arc outcome needs a named consumer and a visible trace.
15. **About half the roster are real Big Brother alumni.** Do not invent secrets, romances, fights or disqualifications about them.

---

## 8. Reader coverage problems

**01 (web storylines)**
- Labelled conversation-triggered storylines, the chapter dialog and branching stories "Live" without checking the Survivor phase flow. All three are gated on a phase that is never entered.
- The "possible crash" caveat about `game.week` on the social screen is moot, because that screen is never reached.
- Correctly flagged the null `game` and the null `relationshipSystem` for branching stories, but did not generalise it to the other GameHUD handlers.

**02 (web house events)**
- Strong on phase reachability.
- But it reported proximity and ambient consequences as live without noticing that the relationship, memory and bus writes sit inside `if (pid && relationshipSystem)` (`GameHUD.tsx:2090,2148`).
- Treated the null adapter as house-event-specific when the null check is HUD-wide.

**03 (web characters)**
- No factual errors found in what I rechecked.
- Did not check whether the null `relationshipSystem` also disables the loyalty-oath +5 it describes.
- "Every AI call has an authored fallback" is [I] and was not enumerated.

**04 (Unity events)**
- Accurate and the most complete.
- Listed web crises as "reached at `GameHUD.tsx:582`" without noting that the eligible list is empty in the current tree.
- Did not notice the imported romance, drama and secret art.

**05 (Unity sim)**
- Its claim that `CharacterAppearance` is being edited in the working tree is wrong; see section 0, row 8.
- Did not verify the "AI-driven" comment.
- Its "cadence catch" about the weekly slot is correct and important, but it missed the second-order effect: a proximity event in Campaign pre-empts the next week's storyline, emergent, crisis and catalogue draw.

**06 (Unity characters)**
- Wrongly calls `showmance_rumor` and `homesick_moment` "reached".
- Its citation `CharacterDraft.cs:85-87` for the blank-draft motive is off; the defaults are at `:52-54`.
- Correct on the two-versus-six repertoire count.

**07 (Unity presentation)**
- Thorough.
- Did not survey the unused `SocialEvents` / romance UI art.
- Did not state that the Have-Not room is set dressing with no mechanic (`MASTER-PLAN.md:442`).

**08 (design research)**
- Two factual errors in its Unity mapping:
  - "no writer" for `stressLevel`;
  - "six of 24" on the default repertoire.
- Overclaims the web branching system as "applied" and `showmance_rumor` as "reached".
- The Big Brother USA institutional facts are background knowledge [K]. Only the five expulsion names were web-checked, and the Scott Weintraub details are unverified.
- US penalty nominations are explicitly unverified.

**Gaps no reader covered**
- The HUD-wide null `relationshipSystem` gate.
- The imported but unused romance, drama and secret UI art.
- That Rachel Reilly's bio already quotes her real-life showmance (`CastTemplates.cs:236`), which is directly relevant to showmance content about real people.
- Whether `useNPCSocialEngine`'s confrontation approach, the one live NPC-initiated conflict in the web, applies any relationship change. Its handler was not traced.

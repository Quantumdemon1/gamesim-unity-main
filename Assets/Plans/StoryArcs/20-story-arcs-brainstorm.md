# 20 · Story arcs for the house: the synthesis brainstorm

> **Read with `21-bb-build-addendum.md`.** This plan's web citations come from `D:/gamesim-main`, the
> Survivor reskin. The Big Brother web game is `D:/gamesim-web`: the same event files at the same line
> numbers (except `GameHUD.tsx` and `grudge-reactions.ts`), but its social week runs, several
> consequences land, and a first-round nomination creates no grudge there. The addendum lists the
> corrections (C1-C7), the grafts (G1-G9) and seven new owner decisions (D-A to D-G).

**What this is.** One brainstorm for the owner, built from six proposals (10-A to 10-F), three judges' verdicts and the gap map (00). It follows the judges' combined spine. It corrects every factual error they found, lands every must-fix item, and settles each point where the judges disagreed. This is the **revised** version: a critic's pass found further errors (an unreachable disqualification, persona writes that fail validation, a swing vote that never reached the ballot, a rolling alliance path, a roadmap with forward dependencies), and every one is fixed below. The revision notes at the end list what changed. I re-checked the load-bearing code claims myself. Where a line number here differs from a proposal's, this document is the correct one.

**Conventions.**
- Unity paths are relative to `Assets/Gamesim/`. Web paths are relative to `D:/gamesim-main/gamesim-main/`. Plans are in `Assets/Plans/`.
- "Web" means the web game's design intent under the Big Brother loop, never its live path. The D: tree is part-way through a Survivor reskin and its social phase cannot be reached (gap map §0, row 1).
- **Native** marks a declared native extension: an idea with no web precedent. Each one needs a MASTER-PLAN entry and the owner's sign-off.
- **M1-M6** are the milestones in §8. **v1-v6** are the `storyRulesVersion` values that switch each milestone's rules on.

---

## 0. TL;DR

1. **One system, not a parallel one.** `StorylineState` becomes a multi-beat **story cycle**, and each beat is a `HouseEventState` of kind `story`, appended to `HouseEventKind.All` (`HouseEventState.cs:121`) the way HOUSE-LIFE-PLAN appends `activity` (`:705`). The reserved `ProgressStoryline` ordinal (`EpisodeState.cs:429`) becomes the only command that answers a beat, keyed `(eventId, optionId)` the way `ReflectDiary` keys diary choices (`EpisodeSocialHistory.cs:46`). No new command ordinals.
2. **Build the grudge substrate first.** Today no production code ever writes a negative permanent ledger type, so `HoldsAGrudge` is always false (`RelationshipLedger.cs:46-56,110-114`; comment at `EpisodeEngine.cs:1699-1707`). Every "you nominated me, now it's war" trigger in the six proposals was dead on arrival. A directional `GrudgeState` fixes this. It uses the web's severities, stacking and decay (`grudge-system.ts:22-29,56-62,84-101,139-151`) and is written with no rolls from nominations, veto replacements, broken deals and promises, discovered lies, and votes cast against an ally.
3. **Consequences land where Big Brother lands them.** Every effect names a consumer the engine already reads: NPC nominations, veto saves, the eviction vote, the jury, `TrustScore`, alliances and blocs, promises and deals, competitions, and the action budget. **Nothing pays into `jurySentiment`**, which no vote reads (`EpisodeEngine.cs:694-697`). Where an existing consumer would make a payoff too small to feel (a +15 inside jury `Obligations` is worth +2.25, `WebJuryVoting.cs:30,137`), the document says so and proposes one labelled native jury term (§3.1).
4. **Stage every beat in four steps: Scene, Pull, Moment, Fallout.** The house acts it out in 3D. A non-modal Pull offers **Step in** / **Stay out of it**. The existing card appears over a gathered, frozen tableau. The payoff leaves a receipt that the profile, the recap and the ballot page cite weeks later ("Between you: week 2, you nominated Riley").
5. **Beats fire only at the show's anchors:** HoH crowned, nominations set, veto won, block set, eviction eve, eviction night, and the player's own verbs. A budget replaces `HouseEvents.Ready`'s one-event-a-week slot (`HouseEvents.cs:265-271`): one open ask at a time; at most 2 asks and 1 Diary summons a week, at any anchor; new arcs start only at hoh-crowned, block-set or eviction-night (with two named exceptions, `the-backdoor` and `the-house-turns`, §5.2), while a running arc's next beat fires at the anchor its template names; three lanes; a headliner rests a week; and every pool carries a "nothing happens" weight.
6. **Size it for the real season.** The cast screen seats 3-12 and defaults to 8 (`SeasonBuilder.cs:27,71-75`). The shipped scenario is 6 people and ends in week 4. The action budget is ceil(active/2) (`EpisodeEngine.cs:513-516`), about 16 actions in a default season. Stories start at week 1's eviction night, so the default season has a ceiling of 9 asks and 5 summons; the sweep target is 4-6 asks, arcs of 2-4 beats, deep lore on about 2 houseguests, and around 3 finished arcs.
7. **Production is a character with a signposted ladder:** a private Diary Room warning, then a penalty (a Have-Not week and sitting out the next HoH through `CompetitionPlayers`, `EpisodeEngine.cs:148-154`), then removal. A conduct option always strikes, is never a roll, is labelled before you choose it, and needs a second press to confirm. Strikes can start at week 1's eviction night, at most one per person per week. Strike 3 lands only in a post-eviction Social beat with 4 or more active, and removal happens as that Social window closes: removing one of four routes straight to the final HoH (`EpisodeEngine.cs:172`). That gives the default 8 two removal weeks (3 and 4), the 12-seat house six, and the shipped 6 none; the 6 is declared exempt and its ladder tops out at repeated penalties (§3.5 has the window table). At most one removal a season, and no jury seat, as a design choice.
8. **Expulsion must finish the season.** Five fixes land inert with `Expelled` in M1: `ResolveJury` treats an expelled player as a non-voter (today it throws, `EpisodeEngine.cs:708-711`); `JuryExchangeCount` returns 0 and the questioning phase is skipped for an expelled player (`EpisodeFinale.cs:7-29`); `CareerLedger` placement reads exit order from `removals` as well as the jury (`CareerLedger.cs:256-270`); the hard-coded "Four jurors choose the winner." counts the jury (`EpisodeDirector.cs:1285`, wrong today for every cast but 6, so it is fixed in M0); and a pending removal locks the player's Social verbs until the Advance that removes them.
9. **Characterization is an input to every roll.** A story-scoped personality vector is built from both traits across all 17. Lore sheets give hot-buttons, what someone respects, what they won't forgive, their goals (each taken from the card's own authored motive), and a secret. Reception is **Resonates** or **Grates**. A backfire teaches you the fact ("You learned: Quinn hates being told to calm down"). Conflict comes from **state** (grudges, stress, nominations, Have-Not weeks), and temperament decides its shape: Quinn shouts, Riley goes cold. In the default eight only Quinn (base Volatility 3) is a natural shouter, which is exactly what her card says ("Will create drama for entertainment", `CastTemplates.cs:145`).
10. **Real alumni get a game-register track only:** grudges from this season's game acts, the flip, the backdoor, the accounting, the house turns, and a "target on their back" beat drawn from audited shipped bios. No invented romances, secrets, blow-ups, strikes or removals, and never the target of a conduct option. That also covers **the player on an alumnus card**, who keeps the real name (`SeasonBuilder.cs:179-181`): personal and conduct arcs are off unless the card is renamed. A pure All-Stars season therefore loses showmances, fights, secrets and DQ, and §3.10 says so and offers the owner a fictional-veterans option.
11. **Determinism.** Every story draw uses a keyed hash stream, `new SeededRandom(SeededRandom.HashSeed(key)).NextDouble()` (the `WebLoyaltyOaths.cs:111` pattern). Player score changes use `ChangeWithRoll` with that stream injected (`EpisodeEngine.cs:1662`). The player's `FormAlliance` body, which rolls (`:744`), is refactored into a roll-injected `FormAllianceWith` before any story option can make an alliance. Past the boundary the legacy producers move onto the story stream too. One test asserts `randomState` is identical for every option of every beat.
12. **One schema-14 bundle, merged with HOUSE-LIFE-PLAN's queue (`:716-721`).** It carries every shape M1-M3 need, HOUSE-LIFE's queue, `Expelled`, the `story` kind, and the M4-M6 records as small generic lists. Later milestones switch rules on with a `storyRulesVersion` integer (the `competitionRulesVersion` precedent, `EpisodeState.cs:198`). If building M4-M6 shows one of those records needs another shape, that is a v15, and this plan accepts it rather than pretending a speculative shape is final.
13. **Ship M0 now.** It is presentation-only at schema 13: a STORYLINES block, modifiers shown with the weeks they have left (today they are invisible), stakes chips, an "Answer it" Pull, the jury-count caption fix, and the removal of the unread "+N social bonus" line. M1 onward is §3.G-class depth, so the owner decides when (§9, decision 1).

---

## 1. Framing

### 1.1 Web fidelity: three buckets

| Bucket | What goes in it |
|---|---|
| **Ported as content** (templates, labels, numbers, triggers) | The 5 branching stories (`branching-story-system.ts:65-83,102-251`), which are hand-written and deterministic apart from two `Math.random` calls. `Storylines.cs:21-24` wrongly calls them AI-driven. Also: the shared terms of `calculateSuccessChance` (`contextual-action-generator.ts:45-67`; `StoryOdds` is **derived from** it, not at parity, §2.10); the promise-leak odds 20/60/80 (`promise-effects.ts:17-47`); grudge severities, stacking, the allied ×1.2 and decay (`grudge-system.ts:22-29,56-62,84-101,139-151`); first impressions +3/−3/+1 (`CampArrivalPhase.tsx:104-137`), rebuilt on Unity's own meet-and-greet (§4.1, `first-night`); the post-HoH "Pitch your rival" phase event (`phase-event-system.ts:44-76`); the crisis ramp min(0.15+0.035(w−1), 0.40) replacing Unity's flat 25%. |
| **Completes a web template** (the web named it; Unity makes it change state) | `showmance_rumor` (`mid-week-crisis-system.ts:339-370`), `have_not` (`:483-500`), crisis `house_meeting` (`:401-437`), `homesick_moment` (`:148-162`), `secret_alliance_exposed`; emergent `confrontation` (`emergent-event-system.ts:49-100`), `house_meeting` (`:152-188`, never called by the web), `pile_on` (`:190-228`), `secret_deal` (`:259-274`, which Unity re-aimed at the player and this restores NPC-to-NPC) and its ignored `'form'` (`GameHUD.tsx:1802`); proximity `npc-secret` and `npcs-arguing`; the storyline model's `allianceCreation`, `immunityFromNomination`/`targetAvoidance`, `personal_growth` and `secret_scheme` (`models/storyline.ts:8,13-25,39-44`); the unused final-speech `{storyline}` fragments (`WebFinalSpeechCatalog.cs:18-25`). |
| **Native** | The production ladder and `Expelled`; the directional grudge store; sticky bonds; showmance state; the knowledge store and hooks; lore sheets and rapport; personality axes; keyed success rolls; stress readers; Have-Not consumers and veto punishments; NPC-owned modifiers; the story terms in the vote, nomination, veto and jury; the NPC HoH's backdoor plan; receipts; the episode-shaped recap. |

**Method deviation.** The web's continuity came from AI prompting. Unity bans network content unless it travels inside the recorded command (`MASTER-PLAN.md:58,234-243`). Arcs are therefore authored, deterministic state machines. That is a change of method, not of design.

### 1.2 What Unity already has to build on

| Piece | Where | Role in this design |
|---|---|---|
| Saved event card with stored choices | `HouseEventState.cs:19-80`; card `EpisodeHud.Decisions.cs:88-161` | Every beat |
| One-chapter storylines and read modifiers | `StorylineState.cs:19-36,70-94`; read at `EpisodeEngine.cs:365,502-510` | The story cycle; modifiers stay as they are |
| Reserved `ProgressStoryline` | `EpisodeState.cs:429`; falls through to `Social()` and throws (`EpisodeEngine.cs:141,780`) | The one arc command. It has always thrown, so no recording holds a successful one. |
| Ledger with fading and permanent entries | `RelationshipLedger.cs:32-56` | Receipts; `TrustScore` (`ThreatAssessment.cs:157-170`, one third of a trust point per impact point) |
| Promises with witness spread | `EpisodeEngine.cs:1635-1655` | The template for knowledge spreading |
| Deals that name a target | `DealState.cs:29,53-77`; read by target in `WebEvictionVoting.cs:275-291` | Vote pressure from story (`vote_evict`, `vote_save`) and `veto_use` for veto saves |
| The player HoH's backdoor plan | `SetBackdoorPlan`, `EpisodeEngine.cs:1040-1056`; read only by the Diary Room (`EpisodeDirector.DiaryRoom.cs:692-707`); the recap already lists `backdoor` lines (`WeeklyRecap.cs:204`) | The player path of `the-backdoor` |
| Meet-and-greet first impressions | `MarkOpeningBeat` → `FirstImpressions`, +3 through `Move`/`Record` with no roll (`EpisodeEngine.cs:994-1031`) | The `first-night` card rides on it instead of adding a second first impression |
| NPC router | `NpcSocialActions.Act` (`:401-411`) | The pattern for the effect router |
| Nomination, veto and vote evaluators | `EpisodeEngine.cs:190,206,554,602`; `WebEvictionVoting.cs:378-399` | Where grudges, bonds and knowledge bite |
| Jury `Obligations` (±50 clamp, weight 0.15) | `WebJuryVoting.cs:96-115`, weighted in `Score` at `:128-137`; the finale's ±10 impression at `:147` | Kept as the web's; story payoffs get their own labelled term (§3.1) |
| `mood` and `stressLevel` | Written only by `NominationEffects`, only to make them worse (`EpisodeEngine.cs:578-584`) | Stress with a reader and a relief valve |
| Unused art | `chip_romance`, `romance_alert`, `drama_alert`, `secret_alert`, `social_alert`, `icon_drama` (`Editor/UiPackCatalogue.cs:234,281,350,505`) | Lane glyphs |
| Unread `homeRoom`, `motive`, `WebNpcActivityRules` seek intents | gap map §3 | Comfort venue, goal predicates, "come to you" beats |

### 1.3 Arguing with the plans

- **MASTER-PLAN §3.G "Systems depth (only after the slice)" (`:245-249`) and "What not to spend on: more simulation before the slice" (`:604-605`).** None of the six proposals argued with these, and they should have.
  - M0 is not simulation. It is the SESSION-HANDOFF Track 3 principle ("say what the simulation already knows"), which surfaces invisible modifiers and storylines. It ships now.
  - The schema-14 bundle is already queued (`HOUSE-LIFE-PLAN.md:716-721`), so fixing the M1-M3 shape now is cheaper than a v15 later.
  - Everything from M1 on is §3.G depth. **The owner decides** whether this request re-prioritises it, or whether it waits for the slice gate (§9, decision 1).
- **MASTER-PLAN:213, "Do the idiom change before adding more panels."** This design adds **one modal state**, `sceneCardOpen`, which joins `IsPanelOpen` (`EpisodeDirector.cs:89`). It adds no new panel *type*: the scene card is the existing `EventChoices` card shown where the scene is rather than at the station, and the tracker is a section of the notebook's existing Story page, built from the same Heading/Paragraph/Action/Tag parts. A later idiom change redoes both along with their hosts. Whether "the same card in a new place" counts as a new panel under :213 is the owner's call; this document says plainly that it is a new modal state.
- **MASTER-PLAN:63, authoring a personality system.** This design authors one, scoped to story: odds, NPC `ai_chance` and casting. The `NpcSocialActions` repertoires and the test that pins them stay untouched. Owner sign-off: §9, decision 4.
- **MASTER-PLAN:442 ("no have-not mechanic") versus HOUSE-LIFE-PLAN:356 (Have-Nots in schema 14).** This design takes HOUSE-LIFE's side and asks the owner to amend :442.
- **HOUSE-LIFE-PLAN:351, "The showmance rumour from shared late nights in the hot tub."** This design **overrides** the hot-tub trigger while completing the rumour. The hot-tub companion is presentation state, chosen by the director and never saved (`EpisodeDirector.HouseLife.cs:44`); a seeded rule that read it would make the season depend on where the camera was. The "late nights" are instead the recorded one-on-ones the save already holds (RelationshipBuilding and PersonalChat, counted in `contacts.weekCount`), and the hot tub becomes where the first beat is *staged* when you are in it. If HOUSE-LIFE's M9 turns a hot-tub visit into a recorded activity command, it can count then.
- **HOUSE-LIFE-PLAN:368 rejected "activities that ease stress" as a rule the web lacks.** The rejection still holds for activities. But once stress has a reader (a competition penalty), something must also relieve it, or one nomination penalises you for the rest of the season. Relief here comes only through story beats (Diary Room venting, a comfort bond) and a one-step relaxation at the week turn for anyone not nominated. Declared native.
- **HOUSE-LIFE-PLAN:705-708.** The `activity` kind is appended to `HouseEventKind.All`, and older builds reject saves containing it. The `story` kind follows the same precedent and the same forward-compatibility note. :708's "choices cannot store a modifier" is answered by storing typed effects on the choice (§2.3).
- **SESSION-HANDOFF:913, "no −15 penalty or grudge (Unity's 'no ledger' choice stands)."** That choice was made for alliance endings. Adopting `GrudgeState` reverses the broader stance, so it needs sign-off (§9, decision 2). Alliance endings from the weekly settle still write no grudge; only a named betrayer does (§2.7).
- **SESSION-HANDOFF:1009 and :1081-1082, "the arc surface as proposed: premise false."** This design never reads `relationshipArcs` as evidence of the player's history. The NPC-to-NPC arc writes (`EpisodeEngine.cs:1689-1690`) stop behind the boundary.

### 1.4 The real season

The season opens in a Social window with the full cast (`SeasonBuilder.cs:100`). After that, the eviction comes before each week's Social (`EpisodeEngine.cs:313`, then `:232-255`), so **the post-eviction Social of week w has cast − w active**, and the week turns on the Advance out of it (`:164`). At 3 active that Advance goes to the final HoH (`:172`).

| Cast | Weeks | Actions a season | Ask ceiling (from week 1's eviction night) | What that affords |
|---|---|---|---|---|
| 6 (shipped `ContentCatalog`) | about 4 | about 10 (3+3+2+2) | 5 asks, 3 summons | 1-2 arcs finish; lore depth 3 on one houseguest; no removal window |
| 8 (default, `SeasonBuilder.cs:27`) | about 5-6 | about 16 (4+4+3+3+2) | 9 asks, 5 summons; the sweep target is 4-6 asks after the Nothing weights | 3 arcs finish; depth 3 on two houseguests; removal windows in weeks 3 and 4 |
| 12 (maximum) | about 9-10 | about 40 | 17 asks, 9 summons | the full slate; alliance secrecy matters |

The ceiling is 1 ask in week 1 (eviction night only) plus 2 in every later week that still has anchors; the finale week has none. The `first-night` card at move-in (M4) sits outside the count, because it rides on the existing opening beat.

**Score velocity.** A Talk is +4. RelationshipBuilding is 5-12 (`WebSocialVocabulary.cs:35`). Converse writes no ledger entry (`EpisodeEngine.cs:1282-1287`). So the web's thresholds of 45, 60 and 75 are expensive in this port.

**Rule: thresholds are measured, not ported.** Triggers are written as a rank plus a floor, for example "the player's warmest NPC, mutual ≥ 25". The floors are calibrated on the 1,920-season sweep (the precedent at `SESSION-HANDOFF.md:925`). The web numbers are only the starting guesses.

---

## 2. The core model

### 2.1 Vocabulary

- A **story cycle** is one running thread: a cast bound to roles, the path taken so far, variables, and the next anchor.
- A **beat** is one `HouseEventState` with `kind = "story"`, a `contentId` and stored choices.
- An **option** is a fixed caption and an `optionId`. It may carry a gate, a check and typed effects, and it may name the next beat.
- An **effect** is a typed write that goes through a router to a producer that already exists, or to a named refactor of one.
- A **receipt** is a one-way ledger mark of type `story:*`. Receipts are what later text is allowed to cite.
- An **anchor** is a point in the Big Brother week at which the story producer pulses.

### 2.2 Static catalogue (Simulation assembly, append-only, never saved)

```csharp
public sealed class ArcTemplate {
  public string id, lane, category, origin;   // lane: Personal|Conflict|Game|Production
                                              // origin: "web:systems/branching-story-system.ts:181" | "native"
  public int minWeek, cooldownWeeks, pairCooldownWeeks, minActive, rulesVersion;
  public RoleSpec[] roles; public Cond trigger; public Weight weight; public BeatTemplate[] beats;
  public Exemption[] exempt;                  // {cast: "all-custom"|"six"|..., reason}: the castability sweep's
                                              // stated exceptions (§3.9); an exemption without a reason fails lint
}
public sealed class RoleSpec { public string key; public Rank pick; public Cond requires;
  public Sensitivity needs; }                 // Game | Personal | Romance | Conduct: the real-person gate,
                                              // which also applies to the player's own card (§3.10)
public sealed class BeatTemplate {
  public string id, anchor, window, venue, surface; // surface: Scene|Approach|Conversation|Summons|Meeting|Npc
  public string titleKey, fallbackKey; public string[] narrativeKeys;  // variant = (week-1) % n
  public OptionTemplate[] options; public string lapseOptionId;        // lint: every Scene beat has one
}
public sealed class OptionTemplate {
  public string id, label, descriptionKey, glyph, risk; // label = caption constant
  public Gate gate;          // Show(traits) | Lock(traits, drawn muted) | Needs(knows|bond|hook)
  public Check check;        // null = certain; else base + visible terms, clamped 5..95
  public string[] against;   // player traits for which choosing it costs a stress step
  public bool costsAction, conduct, pickPerson; public Effect[] success, backfire;
  public string next, nextOnBackfire; public AiChance npc;   // used when an NPC holds the choice
}
```

**`Cond` and `Weight` form a closed, lintable grammar.** Four groups of `Cond` nodes:
- **Logic and time:** `All`, `Any`, `Not`, `Week`, `Phase`, `ActiveAtLeast`.
- **Position and state:** `IsHoh`, `IsNominee`, `HoldsVeto`, `HaveNot`, `Stress`, `Strikes`, `RemovalWindow`.
- **Relationships:** `Score` (directed), `Mutual`, `Rank`, `Grudge(a→b) ≥ n`, `Allied`, `Bond`, `Promise`, `Deal`.
- **Knowledge and lore:** `Knows(holder, fact)`, `LoreDepth`, `Rapport`, `Axis`, `HasTrait`, `RealPerson`, `PlayerParty(a, n)`, `Goal(id)`.

`PlayerParty` counts receipts the player is an endpoint of. It replaces raw arcs as evidence of shared history. `Goal(id)` evaluates an NPC's goal predicate (§3.7).

`Weight` is `base × Π factor(cond)`. Every pool carries a **Nothing** entry.

**Triggers may read hidden state. Text may not.**

**Numbers in the catalogue are rules.** Editing one on an existing template means a new template id or a `storyRulesVersion` bump. Options are tombstoned, never removed.

### 2.3 Persisted state (the schema-14 bundle)

```csharp
// StorylineState, extended into the story cycle (v12 fields kept)
public string beatId, lane; public int variant;
public List<StoryRoleState> cast;   // {role, contestantId}, bound when the cycle starts; ≤ 8
public List<StoryStepState> path;   // {beatId, optionId, result: plain|success|backfire|lapsed|npc, week,
                                    //  logSequence}; ≤ 8. logSequence ties the step to its log line (§2.11)
public List<StoryVarState>  vars;   // {key, value}: heat, secrecy, exposure, planned; ≤ 16
public int nextWeek; public string nextAnchor, endingId;
// HouseEventState   += contentId, cycleId, window, venue, closesAnchor, lapseOptionId, List<StoryRoleState> cast
// HouseEventChoice  += optionId, glyph, checkBase, bool lapse, List<StoryEffect> effects, backfire
//   (effects are stored when the beat is drawn: this answers HOUSE-LIFE-PLAN.md:708,
//    "choices cannot store a modifier")
// StoryModifierState += ownerId        // empty = the player, so v12 modifiers keep their meaning

public sealed class StoryWorldState {               // EpisodeState.story
  public int rulesStartWeek, rulesVersion;          // migration: week + 1, and the build's current version
  public List<GrudgeState>  grudges;   // {holderId, targetId, cause, severity, originWeek, count}; ≤ 256
  public List<HouseFactState> facts;   // {id, kind, actorId, subjectId, refId, week, visibility, knowers[]}; ≤ 128
  public List<BondState>    bonds;     // {id, kind, aId, bId, status, sinceWeek, endedWeek, cycleId}; ≤ 64
  public List<HookState>    hooks;     // {id, holderId, overId, factId, week, spent}; ≤ 32
  public List<ContactState> contacts;  // {npcId, rapport, weekCount, lastWeek}: the per-NPC contact counter; ≤ cast
  public List<LoreCastState> lore;     // {contestantId, sheetKey, factIds[≤ 16]}: snapshot at build or migration
  public List<string>       knownFacts;// lore fact ids the player has learned; ≤ 200
  public List<ConductState> conduct;   // {contestantId, strikes, lastStrikeWeek, cleanWeeks, sitsOutWeek, reasons[]}; ≤ cast
  public List<RemovalState> removals;  // {contestantId, week, reasonId}: exit order for CareerLedger; ≤ 1 in practice
  public List<string> haveNots; public string pendingRemovalId;
  public int productionStrictness;     // 0 = Lenient: the player's ladder stops at repeated penalties; 1 = Standard
  public bool romanceStorylines = true;
  public List<StoryCooldownState> cooldowns;  // "tpl:<id>", "pair:<a>|<b>", "star:<id>"; ≤ 256
}
// Appended: ContestantStatus.Expelled (EpisodeState.cs:26); "story" (and HOUSE-LIFE's "activity") to
// HouseEventKind.All (HouseEventState.cs:121), because validation rejects unknown kinds (EpisodeValidation.cs:130);
// Reaction { Shocked, Furious, Tearful, Embrace, StormOff } after Cheered (CharacterPresentation.cs:71).
```

**Rules for the new shape:**
- **Deep copies.** `EpisodeState.Clone` (`EpisodeState.cs:366-396`) must copy `story` in full. So must `StorylineState.Clone` and `HouseEventChoice.Clone`, which today use `MemberwiseClone` (`StorylineState.cs:35`; `HouseEventState.cs:65-68`). Otherwise a rejected candidate command leaks its partial writes into committed state (`EpisodeEngine.cs:37-46`).
- **Lore stays off `ContestantState`**, because `CharacterProfileStore.cs:132` checks that shape exactly and every saved library file would break.
- **Validation.** It rejects a whitespace title or narrative (`EpisodeValidation.cs:129-131`). So every story beat persists a name-free English fallback title and narrative from `fallbackKey` (for example, "Two houseguests are arguing in the kitchen."). That keeps validation happy and keeps old readers working without persisting stale names. Surfaces render the real text from `contentId`.
- **Two rules boundaries, on purpose.** The existing `storyRulesStartWeek` (`EpisodeState.cs:359`, read at `Storylines.cs:215`) keeps gating the v12 one-chapter storylines exactly as today. The new `story.rulesStartWeek` gates everything in this document. A fresh season sets both to 1; the v13→v14 migration sets only the new one, to week + 1.
- **Migrating what v12 already holds.** Existing `StorylineState` records migrate with `beatId` and `lane` null and `cast`, `path` and `vars` empty. They are legacy one-chapter storylines and keep running under `Storylines.cs` until they end or go stale; a new cycle always has a `beatId`. Every existing `activeModifiers` entry migrates with `ownerId = ""`, which means the player, as it always did.
- **What is frozen.** The shapes above are fixed for M1-M3 and for HOUSE-LIFE's queue. Facts, bonds, hooks, lore and conduct are frozen as small generic records whose rules arrive at v4-v6. If building those milestones shows a record needs another field, that is a v15 (TL;DR 12).

### 2.4 Commands

**Answering a beat.** `ProgressStoryline` gets an explicit `Execute` case with these fields:
- `targetId` is the beat's event id.
- `secondTargetId` is the `optionId`.
- `text` is empty. For a `pickPerson` option only, `text` holds a contestant id, which the engine checks against the beat's eligible list.

The HUD still shows the caption constant as the control's name. The label is never the payload for new content. `ResolveHouseEvent`'s label matching (`EpisodeEngine.cs:1240-1242`) stays for legacy events only.

**Phases.** A beat can be answered in any phase while it is open. An option that `costsAction` is only offered in Social or Campaign, the phases where the action budget applies (`EpisodeEngine.cs:725`).

**Letting a beat lapse.** Each beat's `window` names the Advance that closes it. When the player presses that Advance, the lapse option resolves inside that same command, before anything else the Advance does. Nothing expires in real time, and there is no backlog. This also fixes the orphaned-chapter and pile-up defects (gap map §3). Example: an `hoh-room` beat's window closes on the Advance that commits NPC nominations (`EpisodeEngine.cs:187-191`), and the lapse resolves before `Nominate` is called.

**Confirming a conduct option.** Every conduct option, at every strike, uses a two-press confirm on its own tile: the first press turns it into **Do it** / **Back** with the strike line spelled out, and only **Do it** sends the command. It is presentation-only, so it works on any surface (Scene card, conversation beat, Summons). It replaces the earlier reference to the diary's draft flow, which is specific to `ReviewStudyHouse` (`EpisodeDirector.DiaryRoom.cs:204-213`).

**Spending a hook** ("Call in the favour") is an option on a beat that appears in the holder's conversation. It is not a new command. That drops A's `CallInFavour` and D's `CallInLeverage` ordinals.

### 2.5 Randomness

- **The story stream.** `StoryRandom.Unit(s, key) = new SeededRandom(SeededRandom.HashSeed("gamesim:story:v1:" + s.seed + ":" + key)).NextDouble()`. `HashSeed` returns a `uint` (`SeededRandom.cs:47-56`), so it is wrapped exactly as `WebLoyaltyOaths.EvictionWitnessRoll` wraps it (`WebLoyaltyOaths.cs:107-112`); the difference is that the key includes the season's seed (`EpisodeState.cs:200`).
  - Keys are `week:anchor:pool`, `cycle:beat:option:check`, `cycle:beat:npc` and `fact:week:spread`.
  - `randomState` is never touched, so adding or removing a template reshuffles nothing, and a reload cannot re-roll a backfire.
- **`ChangeKeyed`** is `ChangeWithRoll` with a keyed `nextRoll` injected (`EpisodeEngine.cs:1662`; precedent `EpisodeNpcSocial.cs:171-175`). Player arcs and the 75-point oath milestone behave as today, at zero main-stream rolls.
- **`FormAllianceWith(s, target, nextRoll)`.** The player's `FormAlliance` body calls `Change(s, playerId, target, 8)` (`EpisodeEngine.cs:744`), which spends a main-stream roll through `ChangeWithRoll` (`:1657-1658,1676`). It is refactored, with no behaviour change, into a helper that takes the roll: the command path passes `() => Roll(s)` and the story path passes the keyed stream. This is M1 engineering, and until it lands no story option may carry an alliance effect.
- **Legacy producers move to the story stream past the boundary.** That covers `OfferHouseEvent`, `BeginStoryline` and `NarrateHouse` (`EpisodeEngine.cs:1095-1164`). The catalogue, crisis and emergent producers become one-beat pool entries. Otherwise a story beat that takes the slot would change how many main-stream rolls they spend.
- **Final-speech storyline fragments** get a keyed roll behind the gate (`WebFinalSpeeches.cs:61-72`). Without it they would add main-stream draws just before the jury's `FinalImpression` rolls.
- **Honest scope.** The invariant is per command: no story resolution changes `randomState`. It is not per season. A story-moved score legitimately changes a veto path that calls `Change` (`EpisodeEngine.cs:605-607`). A promise a story creates still settles with the engine's own witness rolls (`:1650-1651`), exactly like a promise the player made by hand.

### 2.6 The effect router

| Endpoints | Path | Why |
|---|---|---|
| The player is involved | `ChangeKeyed` | Feeds arcs and the oath milestone like any player act |
| Two NPCs, a shared act | `RelationshipLedger.Move` + `Record` (`RelationshipLedger.cs:128-172`) | No roll and no arc. `Move` is symmetric. |
| One NPC's private view of another (a pitch, a learned secret, a witnessed act) | `WriteScore` one way + `RecordOneWay` | `Record` writes **both** directions (`RelationshipLedger.cs:128-147`), so a one-way view needs a one-way writer. |
| An alliance | `FormAllianceWith` for the player; a new public `NpcAlliances.FormFromStory` for NPCs | §3.3 |

`RecordOneWay(state, holder, about, type, impact, description)` is new, and it has **no precedent**. The promise witnesses write one way (`WriteScore` plus a memory, `EpisodeEngine.cs:1652`) but write no ledger entry at all. Ledger edges are already directional, so `RecordOneWay` writes to one edge where `Record` writes two.

### 2.7 Grudges: the substrate (directional, no rolls)

**Why a store and not ledger types.** `Record` is symmetric, so a "nominated" entry would make the HoH resent the nominee too. And every ledger entry flows into `TrustScore` for every reader. A directional store keeps the blast radius to the consumers named below. It also carries the web's severity, stacking and forgiveness, which the ledger's fade curve cannot express.

**Writers.** All are gated on `storyRulesVersion ≥ 2`, and none rolls. "Web" rows follow `grudge-system.ts`; "native" rows are labelled as such.

| Cause, and where it is written | Holder → target | Severity | Source |
|---|---|---|---|
| Nominated (`Nominate`, `EpisodeEngine.cs:557-567`, for the initial pair only; or inside `NominationEffects` under its existing `initial` guard, `:578`) | nominee → HoH | 70, ×1.2 = 84 while an **Active** alliance joins them | web (`:24,139-151`) |
| Veto replacement (`ResolveVeto`, `:591-620`) | replacement → HoH | 70, ×1.2 while allied | web |
| Veto replacement | replacement → veto holder | 40 | native |
| Alliance betrayed: an ally who leaves (`LeaveAlliance`, `:745-748`) or whose defection ends the alliance (`the-accounting`) | abandoned member → betrayer | 80 | web (`:23`); **not** written on the weekly settle's endings, which keep SESSION-HANDOFF:913 |
| Deal or promise broken (`SettleDeals`; `SettlePromise` at `:1635`) | wronged → breaker | 60 | web (`:25`) |
| Lie discovered (`SpreadLie` discovery; story "Deny it" backfires) | victim → liar | 50 | web (`:26-27`) |
| Voted against by an ally (vote reveal, `:303-320`) | the nominee voted against → the voter, **only** while an Active alliance joins them | 40 ×1.2 = 48 | web (`:28,148-151`) |
| Story backfire Δ ≤ −8 (the web's bus rule, `hooks/downstream-reactions/grudge-reactions.ts:41-62`) | as authored | \|Δ\|×5, and half back the other way at Δ ≤ −15 | web |

**Guarding the double write.** `ResolveVeto` calls `NominationEffects(s, replacement, false)` (`EpisodeEngine.cs:604`). If the nominated grudge were written unguarded inside `NominationEffects`, the replacement would take 70 there and 70 again from `ResolveVeto`, stacking to 100. The nominated write therefore lives under the `initial` guard, and the replacement gets exactly one 70.

**Web rules.**
- ×1.2 only while an **Active** alliance joins holder and target (`grudge-system.ts:139-151`), not for former allies.
- A repeat against the same target stacks at +0.5× of the new severity (`:56-62`).
- Clamped to 100.
- −2 a week at the week turn (`:84-92`).
- Forgiven (the row is deleted) below 10 while `Score(holder, target) > 40` (`:94-101`).

At −2 a week, a 70 never fades within a 6-week season. So forgiveness comes through story: **Clear the air** −20, **made peace** −30. That is on purpose: "you put me up in week 1" is a season-long Big Brother memory.

**Consumers.** Each reproduces today's output on an empty store, so the parity fixtures hold.

- **`NominationPreference(s, hoh, c)`** replaces the three nomination sorts: the NPC HoH's pair (`EpisodeEngine.cs:190`) and both replacement picks (`:206`, `:602`). The lowest values are nominated.

  | Term | Value | Gate |
  |---|---|---|
  | Relationship | `Score(hoh, c)` (today's whole sort) | always |
  | Grudge | −0.3 × grudge(hoh→c) | v2 |
  | Their word | +30 if the HoH holds an active `Safety` promise to c, or an active `safety_agreement` deal with c | v2, native |
  | Nemesis | −20 | v5 |
  | Partner | +50 for a showmance partner or ride-or-die | v5 |
  | Split the couple | −15 if c is in a known showmance and the HoH is bonded to neither | v5 |

  "Their word" is what the earlier draft called "leverage", now defined. It also makes NPC HoHs honour safety promises they already make, which today they ignore until `NominationEffects` breaks them (`:586-588`). That is a real behaviour change, measured by the sweep. A spent hook (§4.1, `call-in-the-favour`) produces exactly such a promise or deal.

- **`SavePreference(s, holder, c)`** is separate, because `NpcVetoSave` (`:546-555`) is the veto holder's save order, not a nomination sort. It keeps the existing rules (a locked Final 4 veto saves nobody; a nominee holding the veto saves themselves; a save needs a value above 30, `:554`) and ranks by `Score(holder, c)` − 0.3 × grudge + 30 for an active `veto_use` deal between them (v2, native) + 25 for a partner or ride-or-die (v5). It never saves someone the holder holds a grudge ≥60 against, or a nemesis. The Execute-time check at `:70-71` calls `NpcVetoSave` on the same state, so it keeps agreeing with what `Advance` commits.
- **Vote.** A private `grudge` factor of −0.25×severity, capped at −25. It is balanced against `blocPressure` −40, a `vote_evict` deal's −35, and the weighted relationship term (`WebEvictionVoting.cs:283-285,389-398`).
- **Jury.** Through the native jury story term (§3.1), not through `Obligations`.
- **`Confront` targeting** (`NpcSocialActions.cs:304-319`) aims first at the highest grudge ≥40.
- **Alliances.** `NpcAlliances.WouldPropose` is blocked at ≥40 (the web rule that grudges block alliances).

**Legibility.** Severities are never shown. Players see receipts (§5.3).

### 2.8 Receipts and marks

Story marks are one-way ledger entries whose type starts `story:`. They feed `TrustScore` like any other ledger entry, and that is intended: "you backed me" should raise trust. It is also **deliberately subtle**: one impact point is a third of a trust point (`ThreatAssessment.cs:170`), so a +15 receipt is +5 trust. Receipts exist to be remembered and cited; the heavy consequences go through grudges, bonds, deals and the nomination and vote terms.

| Permanent | Fading |
|---|---|
| `story:stood-up-for` +15, `story:sold-out` −20, `story:secret-kept` +15, `story:secret-exposed` −25, `story:showmance` +15, `story:showmance-betrayed` −30, `story:public-blowup` −20, `story:made-peace` +10 | `story:heard-out` +5, `story:snubbed` −4, `story:argued` −6, `story:too-nosy` −3, `story:took-it` +4 |

**Only receipts the player is an endpoint of, or witnessed, are ever shown.**

### 2.9 Knowledge and secrets

**`HouseFactState`** records who knows what, with a visibility of private, whispered, known or public.

**Facts are born only from things that already happen:**
- an alliance forms, and its members are the knowers;
- a showmance forms;
- a promise is broken, and the knowers are the witnesses the existing roll already picked;
- a strike is issued;
- a story outcome occurs.

**How facts reach the player:**
- a successful `Eavesdrop` grants the highest-heat fact either overheard NPC knows (argmax, with an id tiebreak);
- `AskForIntel` shares one fact on success;
- the player witnesses a scene.

**The one-hop rumour mill arrives in M5.**
- Once per anchor, each NPC knower may tell their warmest non-knower.
- Odds are 0.8 if allied with the victim, 0.6 if close to the victim, 0.3 if close to the actor, and 0.2 otherwise (the engine's own odds at `EpisodeEngine.cs:1650`). They are scaled by sociability minus honesty.
- Draws are keyed. There is at most one new knower per fact per pass.
- The listener applies 0.4 of the fact's impact (the same 0.4 as `:1652`).

**Multi-hop spread with lost provenance (D's full mill) is deferred.** It stays out until receipts exist and the rate targets hold.

**Knowledge-gated threat (M5).** `ThreatAssessment.AllianceThreat(state, targetId)` (`:97-100`) gains the evaluator id. An alliance counts only if the evaluator is a member or a knower of its fact. The alliance term inside the vote's threat (`WebEvictionVoting.cs:240`) does the same. That is how blindsides and backdoors emerge instead of being scripted, and it feeds `RankedTargets`, which chooses NPC rumour subjects (`NpcSocialActions.cs:287`).

**The legacy rule: an alliance with no fact record is known to everyone.** The gate is a filter, not an additive term, so without this rule an empty fact store would hide every alliance from every non-member and change threat and vote outputs on day one. With it, every alliance formed before the M5 boundary (and every alliance in a save that never reached it) counts exactly as today. Only an alliance formed past the boundary, which gets a private fact at birth, is gated.

**CK3's secrecy-breach rule is rejected for alliances.** A breach chance of (100 − s)/10 per pulse completes almost never in a 5-6-week season. The per-knower leak odds above do the job instead.

### 2.10 How traits, lore and odds are read

**Personality axes** are built from both traits, across all 17. The table is merged from the proposals:

| Trait | Bold | Warm | Honest | Vengeful | Sociable | Steady |
|---|---|---|---|---|---|---|
| Competitive | +2 | | | +1 | | |
| Strategic | | | −1 | | | +2 |
| Loyal | | +1 | +2 | +1 | | |
| Emotional | | +2 | | +1 | +1 | −1 |
| Social | | +1 | | | +2 | |
| Funny | | +1 | | −1 | +2 | |
| Charming | +1 | | −1 | | +2 | |
| Manipulative | | −1 | −2 | | | +1 |
| Analytical | | | | | −1 | +2 |
| Impulsive | +2 | | | +1 | | −2 |
| Deceptive | | | −2 | | +1 | |
| Introverted | −1 | | | | −2 | +1 |
| Stubborn | +1 | | | +2 | | |
| Flexible | | +1 | | −2 | | +1 |
| Intuitive | | +1 | | | | +1 |
| Sneaky | −1 | | −1 | | | +1 |
| Confrontational | +2 | −1 | +1 | +1 | | |

**Volatility** is a state-based derived value:

> Bold + Vengeful − Steady + stress step (Tense 1, Stressed 2, Overwhelmed 3) + 1 if nominated this week + 1 if a Have-Not + 1 if they hold a grudge ≥60 against the other headliner.

The state terms add at most +6. A first nomination alone is worth +3 (it moves stress two steps, Normal to Stressed, `EpisodeEngine.cs:583`, and adds the nominated +1).

**What that means for the default eight** (base Volatility, then the best the state terms can do):

| Houseguest | Traits | Base | Maximum | What their conflicts look like |
|---|---|---|---|---|
| Quinn Martinez | Confrontational, Social | 3 | 9 | Loud and public from day one. The only natural shouter, and the only realistic production strike. |
| Jamie Roberts | Emotional, Strategic | 0 | 6 | Nominated once, she is at 3: the hothead band. She blows up when it is personal. |
| Alex Chen, Jordan Taylor, Casey Wilson | Strategic or Sneaky with Social | −2 | 4 | Reach the band only when nominated and holding a grudge and a Have-Not. Usually they campaign instead. |
| Emma Brown, Riley Johnson | Analytical, Strategic | −4 | 2 | Never shout. Their fights are cold wars: the **Cold Shoulder** variant (§4.2 A2). |

In a 9-12 seat house, Avery Thompson (Loyal, Competitive: base 4) and Taylor Kim (Competitive, Confrontational: base 6) join Quinn. In the shipped six, Taylor Kim is the house's hothead.

**Reception.** Each option carries an approach: Warm, Calculated and Bold from the web's `CampArrival` affinity and clash lists, and Candid, Playful, Yield and Hardball from the axes.
- **Resonates** multiplies a gain by 1.5. **Grates** turns a gain into −½ of it.
- **Lore overrides the axes.** A known `respects` fact forces Resonates. A known `hot-button` forces Grates.
- **A hidden hot-button changes only the payoff, never the odds.** If the option succeeds but touches a hot-button the player has not learned, the success Grates ("It worked, but something you said landed wrong"), and the player learns the fact. The chance was exactly the one shown.

**Odds** come from one pure function, `StoryOdds.Chance(state, beat, option)`. Presentation and resolution call the same function, so the odds shown are the odds used. Only inputs the player can see go in. It is **derived from** the web's `calculateSuccessChance` (`contextual-action-generator.ts:45-67`), not at parity with it:

| Term | Web | StoryOdds |
|---|---|---|
| Base | `baseChance` | Stored on the choice (same) |
| Relationship | + min(0.5 × score, 25), a ceiling only (`:58`) | + clamp(0.5 × your score toward them, −25, +25): a floor is added so one bad week cannot zero every option |
| Trust | + min(trust × 10, 20) (`:59`) | **Dropped**: trust is the NPC's private view, outside the knowledge boundary |
| Alliance | +15 (`:60`) | +15 |
| Live deal | +10 (`:61`) | +10 |
| Trait bonus | +15 if the player has the option's trait (`:62`) | +15 |
| Target nominated | +10 (`:63`) | +10 (the block is public) |
| Intense rivalry | −20 (`:64`) | −20 if a permanent negative receipt lies between you: a visible stand-in for the web's arc intensity, which this port never treats as evidence |
| Broken deal | −30 (`:65`) | −30 if the player broke a deal or promise with them |
| Reception | none | ±10 (native) |
| Known relevant fact | none | +10 each, capped at +20 (native) |
| Visible-trait resist | none | −10 (native) |
| Player's stress | none | −5 per step above Normal (native) |
| Clamp | 5-95, rounded (`:66`) | 5-95, rounded |

A hidden relevant facet shows an **Unknown** chip, which means "something you have not learned may change how this lands", never "the odds are uncertain". Grudges never enter the odds; they act through nominations and votes.

**Voice.** `HouseDialogue.Pick` by saved id, falling back to the five `WebFinalSpeeches.TraitFlavor` buckets (`:105-115`).

### 2.11 Content ids rendered at display time

- **What is persisted:** `{contentId, optionId, variant, cast}` only.
- **Rendering.** `Runtime/Presentation/StoryText` renders through a new `Localisation.Format(key, names)` with pronoun tokens; today only `Text` exists (`Localisation.cs:30`).
- **Logs.** A story log line is written with `kind = "story"` and a **name-free English fallback** as its `text` (for example "Two houseguests had it out in the kitchen."). The step that wrote it records the line's `logSequence` (§2.3), so a renderer finds the cycle and its cast from the event. `EpisodeEvent` itself does not change shape (`EpisodeState.cs:181-191`).
- **Every reader goes through one helper.** `LogText.Render(state, event)` returns the rendered story text for a `story` event, falling back to the persisted sentence if the cycle has been pruned, and returns `text` for everything else. M1 converts every reader of event text:
  - `WeeklyRecap.Moments` (`WeeklyRecap.cs:202-211`), which also gains the story kinds that mark a turning point;
  - `WeeklyRecap.Subject` (`:147-154`), which returns the cycle's headliner for a story event instead of parsing a name prefix that a name-free line does not have;
  - `SeasonReport`, `HouseVibe`, `RelationshipWeb`, `VoteRecords`, the status line and Recent Events (`EpisodeHud.Chrome`), and the Journal.
  - `WinnerName` (`:125-136`) parses competition lines only and is untouched.
  - A test fails any direct read of `EpisodeEvent.text` in `Runtime/Presentation` or `Runtime/Episode` outside `LogText`.
- **Wording** varies by week, never by roll.

---

## 3. Mechanic families

Each family gives the **mechanic**, the **consequence**, and the **named consumer**.

### 3.1 Bonds and rivalries

- **Mechanic.** Sticky `BondState` kinds, which change only through beats, never on a wobbling score:
  - **ride-or-die**: a finished Personal arc, a known depth-3 fact, and no negative permanent mark;
  - **confidant**: a kept secret;
  - **nemesis**: an unresolved feud at heat 3, or a grudge ≥80 plus a public blow-up;
  - **showmance**: see §3.2.
- **Consequences:**
  - vote factor `bond`: partner or ride-or-die +25, nemesis −30;
  - `SavePreference`: saves an eligible partner or ride-or-die (unless a cold, steady partner wins the keyed draw to leave you up, §4.2 A1; keyed, so the Execute-time recheck at `EpisodeEngine.cs:70-71` agrees), and never saves a nemesis;
  - `NominationPreference`: nemesis −20, partner +50;
  - comfort: −1 stress step a week while the bond partner is active;
  - the jury story term below.
- **Ride-or-die can escalate to a Final-Two pact,** which writes `PromiseKind.FinalTwo`. It is settled at `FinalEvict` (`EpisodeEngine.cs:661-662`), and an NPC final HoH holding one picks the partner.
- **Consumers:** `WebEvictionVoting` (new factor), `SavePreference`, `NominationPreference`, `FinalEvict`, and the jury.

**The jury, honestly weighted.** A juror's score for a finalist is 0.3 × relationship + 0.4 × gameplay respect + 0.15 × alliance loyalty + 0.15 × (obligations + 50) (`WebJuryVoting.cs:27-30,128-137`), and then each finalist gets an independent final impression of ±10 (`EpisodeEngine.cs:694-697`; `WebJuryVoting.cs:147`). So:

| Story payoff routed through | Worth on the juror's score |
|---|---|
| `Obligations` (+15 kept, −20 broken, clamped ±50) | +2.25 / −3.0 |
| The relationship term (a +20 score swing) | +6 |
| `AllianceLoyalty` (current ally 100, former ally 25) | +15 / +3.75 |
| One final impression | anywhere in −10..+10 |

Routed through `Obligations` alone, a kept showmance or a nominated juror's grudge would be noise under one final impression. Big Brother's bitter jury is not noise. So this design adds **one labelled native term**, `JuryStory(juror, finalist)`, added to `WebJuryVoting.Score` beside the web's four and clamped to −15..+12:
- −0.15 × grudge(juror→finalist) for a grudge of 30 or more (a nominated juror's 70 is −10.5, the size of a full final impression);
- +8 for a kept showmance or ride-or-die, −12 for a betrayed one, −10 for a nemesis.

It is 0 on an empty store, so the jury parity fixtures hold. `Obligations` stays exactly the web's. If the owner prefers the web's proportions untouched, the alternative is to accept these payoffs as flavour and say so on the finale screen (§9, decision 19).

### 3.2 Showmances

- **Mechanic.** A `BondState` of kind showmance, moving private → declared or exposed → ended.
- **Consequences, all of them raising the couple's target value** (A's "Power Couple +1 action" is dropped):
  - **Declaring** forms a two-person alliance through `FormAllianceWith` (§2.5), so `AllianceThreat`, the vote's loyalty and bloc terms, and jury `AllianceLoyalty` read it with no new code beyond that refactor.
  - **Exposure** makes each non-allied NPC cool one way toward both partners (−4 through `WriteScore`, no roll).
  - Once the couple is **known**, `NominationPreference` gives "split the couple" (−15) to an HoH bonded to neither.
  - **The backdoor.** A known couple is the NPC backdoor plan's favourite target: the partner with more competition wins is the one planned for (§4.2 A9; `08-design-research.md:97`, "one is often backdoored").
  - **The veto dilemma.** When your partner is on the block and you hold the veto, keeping it on writes `story:showmance-betrayed`, a grudge of 80, and the betrayed term in the jury story term.
  - **An NPC partner with Honest ≤ −2 and Steady ≥ 2 may leave you up.**
- **NPC-to-NPC showmances** form in a deterministic Eviction→Social pass: the warmest open fictional pair, at most one a week, at most two at once. They go through `Move`/`Record` only.
- **Consumers:** `ThreatAssessment`, `NominationPreference`, `SavePreference`, the backdoor plan, and the jury story term. The hot-tub companion's choice of partner stays presentation-only (`EpisodeDirector.HouseLife.cs:44`) and **never feeds the simulation** (§1.3, on HOUSE-LIFE-PLAN:351).
- **Fictional only.** No real alumnus, and no player on an alumnus card, is ever a showmance role, so a pure All-Stars season has none (§3.10).

### 3.3 Alliances through story

- **Mechanic.** An `alliance` effect for the player calls `FormAllianceWith` (§2.5), the roll-injected refactor of the `FormAlliance` body (`EpisodeEngine.cs:740-744`). It keeps that body's ≥8 trust guard (`:742`), or the option shows **Needs more trust**. Between NPCs it calls a new public `NpcAlliances.FormFromStory(members)`, which takes 2-4 members and writes the same `alliance-formed` records as the private pair-only `Form` (`NpcAlliances.cs:170-182`), with no roll. Three-person story pacts need that new code.
- **Consequences:** from M5, every alliance formed past the boundary creates a private fact whose knowers are its members.
- **Consumers:** `AllianceLoyalty` (vote and jury), `WebVotingBlocs`, and knowledge-gated `AllianceThreat`. This completes `allianceCreation` and the emergent `'form'`.

### 3.4 The argument → fight → intervention ladder

- **Mechanic.** A cycle `heat` var runs from 0 to 3: friction, argument, public blow-up, and "the line" (a conduct option).
  - Triggers are mundane flashpoints: colliding hot-button topics (meal prep against late-night snacking, sleep against noise, a hungry Have-Not, whose turn it is on the dishes).
  - Each needs a real game reason underneath: a grudge ≥40, a nomination this week, or a broken deal.
  - Big Brother drama is dishes on top of a nomination (08 §B5).
  - **Temperament sets the shape, state sets the timing.** The higher-Volatility headliner is HOTHEAD. If HOTHEAD's Volatility is below 1, the fight plays as the **Cold Shoulder**: heat cannot pass 1, nobody shouts, and the options are about sides and silence instead of volume. Emma and Riley's fights are cold wars; Quinn's are not.
- **Consequences:**
  - witnesses take sides by score difference, with no roll;
  - a blow-up writes `story:public-blowup` one way from each witness toward HOTHEAD (which `TrustScore` reads) and, from M5, a public fact;
  - peace writes `story:made-peace` and grudge −30;
  - "the line" is only ever a labelled conduct option and feeds §3.5.
  - **No persona is written.** `WebEvictionVoting.Persona` reads only the player's own `playerPersona`, and only when the player is a nominee (`WebEvictionVoting.cs:149,384`), so an NPC cannot carry one. The player's persona is written only by the post-eviction diary: validation allows five labels ("Aggressive" is not one) and ties every history entry to a resolved `diary-post_eviction-<week>` id, one per week (`EpisodeSocialHistoryValidation.cs:11-35`). Story beats therefore pay in receipts and grudges, never in persona.
- **Consumers:** grudges → `NominationPreference`, the vote, and `Confront`; trust; conduct.

**The memory trap.** `WebEvictionVoting.Memory` (`:343-357`) matches substrings.
- A memory that names the target with no keyword scores **+2** (`:354`), which helps the aggressor.
- "Distrust" contains "trust" and scores **+5**.
- "Believe" and "relieved" contain "lie" and score **−8**.

So story memories are generated only from whitelisted phrasings, and a test runs each one through `Memory` to assert the intended sign.

### 3.5 Production and the disqualification ladder

**The ladder.** A conduct option always strikes, and its odds govern only the social outcome. Strikes are never rolled, at most one is issued per person per week, and conduct options can appear from week 1's eviction night, when the first arcs can start. Justin Sebik's removal came early in BB2 (08 §B4), so an early first strike is true to the show.

| Step | Trigger | What happens | Consumer |
|---|---|---|---|
| 1 · Warning | First conduct act | A private Diary Room summons that states the whole ladder | Later conduct options are tagged **Strike 2 of 3** |
| 2 · Penalty | Second conduct act | A Have-Not week (if Have-Nots are ruled in) **and** sitting out the next HoH (`sitsOutWeek`). At 4 or fewer active the sit-out is skipped, because the HoH field would fall to two; the penalty is then the Have-Not week alone. | `CompetitionPlayers` (`EpisodeEngine.cs:148-154`), which validation recomputes (`EpisodeValidation.cs:205-208`), so the sit-out must be derivable from `story.conduct`; the Have-Not consumers |
| 3 · Removal | Third conduct act, **only in a post-eviction Social beat with ≥4 active**, at most one a season | `pendingRemovalId` is set. It is carried out inside the `Advance` out of Social, **after** its `pendingDiary` check (`:161`) and before `week++` (`:164`). | `Active` everywhere: budget, veto seats, voters, jury |

**Removal windows under these rules.** Strike 1 can land in week 1, strike 2 in week 2, so strike 3 lands in week 3 at the earliest. The post-eviction Social of week w has cast − w active, and removal needs 4 or more there so that at least 3 remain: removing one of four routes the Advance straight to the final HoH (`EpisodeEngine.cs:172`), which validation allows (`EpisodeValidation.cs:185-188`); removing one of five leaves a legal 4-person regular week (`:197`).

| Cast | Post-eviction Social in week w | Legal removal weeks | Jury after an expulsion |
|---|---|---|---|
| 6 (shipped) | 6 − w | **none**: week 3 has 3 active. The 6 is exempt, and its ladder tops out at repeated penalties. | unchanged (4) |
| 7 | 7 − w | 3 | 4: even, so the cast-order tie rule applies (`EpisodeEngine.cs:715-717`) |
| 8 (default) | 8 − w | 3, 4 | 5 |
| 9 | 9 − w | 3, 4, 5 | 6: even, tie rule applies |
| 12 | 12 − w | 3 to 8 | 9 |

In the earlier draft (no story before week 2, removal at ≥5 active) no window existed in either shipped configuration; removal needed a cast of 9 or more. This table is the fix.

**Outside a window,** a player on two strikes sees conduct options as one muted locked line ("Off the table: production has warned you twice"). In the 6, and under Lenient strictness in any cast, the third act is labelled **Rule break · Another penalty** and repeats step 2.

**Cleanup in the same command:**
- `NpcAlliances.EndBroken`;
- purge oath opportunities and oaths;
- expire promises and deals;
- end bonds, void hooks, drop grudges held by or against them;
- end or recast their cycles (§3.11);
- skip `AddJuror`;
- append a `RemovalState`.

**Why that timing.** Removal at the Eviction→Social transition would soft-lock the week: `PreparePostEvictionDiary` has already run (`EpisodeEngine.cs:321`), `Advance` needs the diary cleared (`:161`), and the diary commands need an active player (`EpisodeSocialHistory.cs:38`). Removing someone as the Social window closes means no eviction can intervene either, so no juror row ever needs dropping. The house wakes up to the news at the next HoH.

**The pending lock.** Once the player commits strike 3, `Social()` refuses their remaining verbs ("Production is waiting for you in the Diary Room") and the only way on is the Advance that removes them. A strike-3 NPC has already spent its Social turns at the window's open (`EpisodeEngine.cs:244`).

**Finale fixes that land with the status in M1 (inert until M6):**
- `ResolveJury` treats an expelled player as a non-voter. Today it throws "Cast your jury vote for a finalist first." (`EpisodeEngine.cs:708-711`), and `CastVote` accepts only Jury or Evicted status (`:77`).
- `JuryExchangeCount` returns 0 for an expelled player, and `FinalEvict` skips the questioning phase. Today its not-a-finalist branch makes the player the questioner (`EpisodeFinale.cs:7-8,23-27`).
- `CareerLedger` places everyone by one exit order that merges the jury ledger with `removals`. Today placement is the index in `jurySentiment` (`CareerLedger.cs:264-266`), so an expulsion that never joins it would shift every later placement by one.
- The hard-coded "Four jurors choose the winner." (`EpisodeDirector.cs:1285`) counts the jury. It is already wrong for every cast but 6, so this one ships in M0.
- A sweep test plays to Finished after a player removal and an NPC removal at every legal window of every cast size.

**Jury parity.** Every evictee is a juror (`EpisodeEngine.cs:313,664`), so the jury is cast − 2: 6 in the default house, 4 in the shipped one. An expulsion makes an even cast's jury odd and an odd cast's jury even. The existing cast-order tie rule (`:715-717`) already settles an even jury, so no "America's vote" substitute is needed.

**No jury seat for the expelled** is a design choice: removal must cost something, and a seat would need its own juror row after the Social close. Reader 08 established only that Chima Simone was removed in BB11 (08:120), not whether she kept a jury seat, so this document no longer cites her as precedent.

**NPC path.** Strikes come only from an `escalate` choice an NPC makes by `ai_chance`, weighted by Volatility. Only fictional NPCs, never alumni, can be struck.
- The player learns of an NPC warning only as a witnessed or whispered fact ("Quinn was called to the Diary Room and came back quiet").
- **There is no public "On Notice" tag.** US production warns privately.

**Player path.**
- Removal takes three deliberately chosen, labelled options, each with its two-press confirm (§2.4).
- With `productionStrictness = 0`, the player's ladder stops at repeated penalties.
- After removal the player spectates, using the existing guards (`EpisodeEngine.cs:726,1232-1233`).
- A player on an alumnus card is never offered a conduct option (§3.10).

### 3.6 Have-Nots, penalty nominations, house meetings

**Have-Nots** complete the web's `have_not` and HOUSE-LIFE-PLAN's Have-Nots item (`:356`). They need MASTER-PLAN:442 amended.
- **Selection** happens at `hoh-crowned`, from week 2, with ≥5 active. At hoh-crowned in week w the house has cast − (w − 1) active, so this is weeks 2-4 in the default 8 and week 2 in the shipped 6, which makes `slop-week` a recurring staple and castable in both.
  - One Have-Not at 5-6 active, two at 7-9, three at ≥10.
  - An NPC HoH picks the lowest-scored non-allies.
  - A player HoH chooses from name-free rule captions (**Pick your targets**, **Ask for volunteers**, **Draw from the hat**), and a rule turns the choice into ids.
- **Veto punishment** (a Ringer, §4.3): the veto player who finishes last takes a one-week Have-Not punishment. The competition scores already rank veto players deterministically, so this costs no roll.
- **Consumers:**
  - −1 action, through an owner-scoped modifier. `Storylines.CompetitionBonus` and `SocialActions` (`Storylines.cs:318-329`) must filter by `ownerId`, because today they sum every modifier into the player's own numbers.
  - NPCs get 2 social turns instead of 3.
  - +1 stress step.
  - A −4 fading view of the HoH.
  - The dressed cots (presentation).
- **The pantry.** Sneaking food is a public minor infraction that extends the Have-Not week. It is never a strike.

**Penalty nomination.** Off by default and labelled native, because no US instance was verified (08 §B4). If the owner turns it on, `forcedNomineeId` must be honoured by `NominationCandidates` and by the NPC HoH.
- It must be exempt from the HoH's own breach settlement. `NominationEffects` breaks safety and alliance-loyalty promises to whoever lands on the block (`EpisodeEngine.cs:586-588`), and `SettleDeals(Nominates)` (`:564`) would score a forced seat as the HoH breaking a safety deal.
- It needs a stated veto rule. The default is that it can be vetoed like any other nominee.

**House meetings.**
- **NPC-called:** a CALLER with a grudge ≥60 and a public or known fact (M5).
- **Alliance-called:** "the accounting" (§4).
- **Player-called:** the existing `HouseMeeting` verb (`EpisodeEngine.cs:1395-1428`).
- They are staged as a living-room gathering (§5.1).
- **Consumers:** trust through witness marks, facts made public (and so threat), and grudges.

### 3.7 Getting to know houseguests (progressive lore)

**Three sources.** `LoreCasting` runs once, at build or migration, and snapshots fact ids into `story.lore`, so appending to the catalogue never changes a running season.

| Source | Applies when | Contents |
|---|---|---|
| **Authored** | The houseguest is not the player, `sourceTemplateId ?? id` names a Regular template, and the name matches plus the bio (or, for the `ContentCatalog` five, the motive: those cards carry no bio or `sourceTemplateId`, `ContentCatalog.cs:57-71,102-108`) | Origin, home, work, respects, unforgivable, hot-buttons (with topics), comfort (the first reader of `homeRoom`), a goal as a live predicate, conflict style, romance openness, and a secret. It must agree with the public card: the card's occupation, hometown and traits are facts the sheet builds on, never contradicts. |
| **Legacy** | Identity-intact alumni (`CastTemplates.cs:178-249`) | Only legacy (a restatement of the audited shipped bio), goal (the shipped motive), conflict style and respects, which come from trait pools |
| **Derived** | `custom-N`, imports and edited cards | Card fields (hometown, occupation, the creator's own bio shown as "What they told you about themselves"), plus trait-pool **tendencies** phrased as behaviour ("hates being rushed"), and a game-register secret ("came in planning to take out the strongest competitor"). No invented family or romance history that could contradict what the player wrote. |

Two authored sheets are written out in full in §4.4 as proof of voice.

**The reveal ladder**, priced for about 16 actions:
- Rapport rises with no roll: PersonalChat +2, RelationshipBuilding +3, SmallTalk +1, game talk +1 (game facets only), a kept ShareSecret +3. It is ×1.5 when the NPC's Sociable ≥2.
- **Depth 1** comes on the first personal talk. **Depth 2** at rapport 3. **Depth 3** at rapport 6 plus a finished Personal beat. **Depth 4** (the secret) only through an arc.
- That puts depth 3 on about two houseguests in a default season.
- **Which fact** is the shallowest hidden one whose opener matches the verb.

**Consequences:**
- odds (+10 per known relevant fact);
- option unlocks, such as **Change the subject** when you know the hot-button;
- gates on arcs.

**Goal predicates come from the card's own motive.** Every regular card already carries an authored motive, and each is a ready-made predicate. They are checked at anchors, and a met or broken goal offers a short Personal beat (**Congratulate them** / **Be there for them**, or the variant below) that writes `story:heard-out`.

| NPC | Motive on the card (`CastTemplates.cs`) | Predicate | The beat it offers |
|---|---|---|---|
| Alex Chen | "Run the house from one seat behind it, and never be the name anyone says out loud." (`:107`) | `NeverSaidOutLoud`: never nominated, and never the subject of a house meeting or public fact | On the first nomination, **Said Out Loud**. Congratulating Alex in public Grates; telling him in private that it is temporary Resonates. |
| Emma Brown | "Test every claim she hears against what she has already seen before acting on it." (`:113`) | `CaughtOut`: she becomes a knower of a broken promise or a discovered lie | **The Receipts**: she shows you what she has worked out. **Back her up** / **Ask what she has on you**. |
| Jordan Taylor | "Be everyone's second-favourite person, because nobody targets their second favourite." (`:119`) | `SecondFavourite`: ranked second in at least half the NPCs' score orders (a hidden-state trigger; the text never says it) | When it breaks, **Too Close**: somebody's favourite is a target, and Jordan wants to know if it is you |
| Casey Wilson | "Keep conversations light, compare what people say, and avoid becoming the obvious target." (`:125`) | `NotTheObviousTarget`: never the HoH's lowest `NominationPreference` | When it breaks, **The Fun One**: Casey drops the act for one conversation |
| Riley Johnson | "Turn careful observation into one reliable partnership and a well-timed competition win." (`:131`) | `OnePartnerOneWin`: an active two-person alliance or ride-or-die, and at least one HoH or veto win | On the first win, **Well-Timed**: Riley asks whether you are the partner |
| Jamie Roberts | "Protect people who treat her honestly while keeping the courage to make her own move." (`:137`) | `HerOwnMove`: she votes against her alliance's majority, or uses the veto on someone outside it | **Her Own Move**: **Tell her you respect it** / **Ask why she didn't warn you** |
| Quinn Martinez | "Be at the centre of every story the house tells, and control how each one is told." (`:143`) | `CentreOfTheStory`: a headliner in an open cycle, or the subject of a house meeting | When a week passes without it, she starts one: `staged-feud` or `kitchen-blowup` weight ×2 with her as headliner |

The shipped `ContentCatalog` five carry the same motives (`ContentCatalog.cs:57-71`), so their predicates are the same. Derived sheets fall back to the generic WinAnyComp, ReachJury and NeverNominated.

**The finale tests how well you knew them.** `WebJuryQuestioning` picks the answer pair by the juror's **primary** trait, `traits[0]` (`WebJuryQuestioning.cs:45-63`). A known `respects` fact is shown beside the question (presentation only). The existing `EvaluateChoice` then pays it out through `Change` (`EpisodeFinale.cs:40-43`) into the jury's relationship term. **Consistency rule:** an authored `respects` fact is written for the answer that the juror's `traits[0]` rewards, and the lint checks that each sheet's `respects` is tagged with its card's primary trait. A Derived sheet takes its `respects` from the `traits[0]` pool. So the hint can never point the player at the wrong answer.

**The card stays public.** Name, age, hometown, occupation, both traits and the one-line bio form the move-in package the player already saw on the cast screen, and hiding fields would break the profile, imports and the cast screen's own promise. Only the lore facets are hidden. That is a legibility choice, **not** a claim that houseguests always share their jobs: hiding a job is a well-known Big Brother USA trope, and the roster's own Derrick Levasseur card says "Undercover cop" (`CastTemplates.cs:194`). The trope survives as a **cover story**: a fictional NPC's sheet may say the card is what they told the house, and the depth-4 secret is the rest. Example: Casey Wilson's card says Bartender (`CastTemplates.cs:124`), and her secret is that she owns the bar, and two more, and tells the house she pours drinks because nobody targets the bartender, which is her motive in one sentence.

### 3.8 Characterization rules

1. **NPC choices follow their axes.** Every NPC-held option has an `AiChance` scaled by the axes (for example, Mediate × (1 + 0.3·Warm − 0.2·Vengeful)), drawn on the keyed stream.
2. **Player options show their trait.** A trait-gated option shows its trait chip. An option against the player's own traits costs a stress step and says so ("Out of character: stress").
3. **Casting requires fit.** A romance partner needs romance-open lore or Sociable ≥1. HOTHEAD is the higher-Volatility headliner, and a HOTHEAD below Volatility 1 plays the Cold Shoulder variant. The `escalate` choice (a strike) needs Volatility ≥5. A peacemaker needs Warm ≥1.
4. **Anti-repetition:**
   - one starring thread per NPC at a time;
   - a headliner rests a week after an arc;
   - a 2-week pair cooldown and a 4-week template cooldown (the web's, `storyline-system.ts:844`);
   - no (template, subject) repeat in a season.

   Without these, a default season becomes the Quinn show. With them, Quinn still gets the most airtime, which is what her card asks for.
5. **Lines only use what the speaker knows.** An NPC line may mention a fact only if the speaker knows it, and it shows only once the player knows it too (pinned in the `HouseDialogueTests` pattern).

### 3.9 Custom and imported houseguests

- Every role is a filter over active houseguests, by trait, axis and state. Nothing requires an authored card.
- Lore uses the Derived source (§3.7).
- Voices fall back to the trait buckets.
- The player's own card never gets a sheet, but the player's traits still gate options.
- **Sweep test:** every template must be castable in an all-`custom-N` season and in the 6-person `ContentCatalog` scenario, **except** where the template declares a structural exemption with a reason, which the lint requires:
  - `the-target-on-their-back` needs alumni, so it is exempt in all-custom seasons;
  - the removal steps of `on-notice` and `diary-room-calls` are exempt in the 6, which has no removal window (§3.5); both arcs still run there up to the penalty;
  - `vets-vs-newbies` is parked (§4.1), so it is not swept.
  - `slop-week` is no longer exempt: selection from ≥5 active reaches week 2 of the 6.

### 3.10 The real-alumni content policy

- **Identity.** Real means a template in `Roster.AllStars` (`CastTemplates.cs:34`) whose name and bio are unchanged. There is no `RealPerson` field today (`:60-62`). Add a static one, or derive it from the roster. A custom houseguest built from an alumni card counts as real until it is renamed.
- **The player's own card.** A player on an alumnus persona keeps the real name (`SeasonBuilder.Player` → `CastTemplates.ToContestant(persona, true)`, `SeasonBuilder.cs:179-181`). So the same rule covers the player: while the player's card is identity-intact, romance, conduct, strike, removal, secret, breaking-point and invented-backstory arcs are ineligible, and conduct options are never offered. Renaming the card lifts it. This replaces the earlier decision 10, which would have put "Rachel Reilly" or "Dan Gheesling" into an invented romance or a removal.
- **Allowed:**
  - Game-register roles: Swing, HoH, Ally, Rival in game arcs, Pawn, Backdoor target, Big Name.
  - Grudges and receipts from this season's game acts.
  - Verbal confrontation no stronger than the existing NPC `Confront` produces.
- **Refused:**
  - Romance, secret, conduct, removal, invented-backstory and blow-up roles.
  - **Being the target of a conduct option.** If `bad-blood`'s RIVAL is an alumnus, "Get in their face" is not offered; the beat keeps its verbal options only.
  - A build test fails any Legacy sheet holding a Personal or Romance fact.
- **Bio audit before any arc cites a bio.** `the-target-on-their-back` restates shipped bios, and at least two look wrong by common account: Danielle Reyes "played before the jury could watch footage" (`CastTemplates.cs:212`), whereas the BB3 jury is generally said to have seen footage, which is why later juries were sequestered; and Janelle Pierzina's "Three-time player" (`:200`) looks stale if she has since played a fourth time. Every alumni bio line is checked against a cited source before M4 ships it. **Rachel Reilly's bio** quotes her real showmance catchphrase (`:236`); the recommendation stands to rewrite it to her game record.
- **What a pure All-Stars season gets.** Every houseguest is real, so it loses showmances, fights beyond `Confront`, secrets, strikes and DQ, lore beyond audited bios, the HoH-room letter's contents (an invented family), `first-night`'s lore line, and `breaking-point`. It keeps grudges, receipts, `after-the-comp`, the pitch half of `hoh-room`, `the-confession`, `the-conspiracy`, `the-flip`, `the-backdoor`, `the-accounting`, `the-house-turns`, `slop-week` and veto punishments, `the-target-on-their-back`, and the Ringer set pieces. That is a thinner, colder season, which is honest about what can be staged with real people. **Owner option:** author a "Returning Players" roster of 12 fictional veterans with invented past seasons; they get the full slate and also make `vets-vs-newbies` reachable (§9, decision 20).

### 3.11 When someone leaves mid-arc

Cleanup was specified for expulsion only. A normal eviction, or the player's own eviction, is far more common.

- **A required role leaves** (evicted, expelled, or out at the final eviction): the cycle ends with the authored `left-house` ending at the next anchor. Its open beat lapses at the next Advance with its lapse option, and effects aimed at the departed are skipped; receipts already written stay.
- **An optional role leaves** (WITNESSES, GOSSIP, PEACEMAKER): the role is recast from active houseguests at the next beat, or left empty if the template allows.
- **Grudges held by an evictee** persist: they are exactly what the jury story term reads, so the nominated juror is a bitter juror. Grudges *against* a departed houseguest are kept for the record and ignored by every consumer, since the departed cannot be nominated or voted on.
- **Bonds with an evictee** move to status `apart`: a showmance partner on the jury still reads as a kept or betrayed showmance in the jury story term.
- **Hooks** held by or over the departed are voided.
- **The player is evicted:** every cycle the player is party to ends with `left-house`; NPC-only cycles continue, and the player sees them only as Recent Events whispers from the jury house, under the same knowledge rules. The player's goodbye messages play (§4.3).

---

## 4. Arc catalogue

### 4.1 The slate

The **M** column is the milestone that ships each template (§8); where a later milestone switches on more of it, the row says which.

| id | Fantasy | Trigger (floors calibrated by sweep) | Cast | Payoff | Consumer | Anchor / origin | M |
|---|---|---|---|---|---|---|---|
| `after-the-comp` | Lobby the HoH before nominations | NPC HoH crowned; chance 0.7, keyed | HOH, RIVAL, THREAT | HoH→target −12, one way | The NPC HoH's sort (`:190`; `NominationPreference` from M2) | web `post_hoh` | M1 |
| `the-confession` | Your ally was offered a better deal | Ally mutual ≥30 plus PlayerParty ≥2; web ramp | ALLY, OFFERER (an actual NPC ally, never a name the text invents) | Pact, final two, ultimatum | Oaths, promises, blocs | branching confession | M1 |
| `kitchen-blowup` | Two NPCs explode, or freeze each other out | An NPC pair where one holds a grudge ≥40 against the other, or mutual ≤ −10 | HOTHEAD, TARGET, WITNESSES | Sides, peace; strikes from M6 | Grudges, `NominationPreference`, conduct | npcs-arguing, confrontation | M2; hot-button terms v4; strikes v6 |
| `bad-blood` | Your feud with an NPC | grudge(NPC→player) ≥40 | RIVAL, PEACEMAKER | **Clear the air** (−20 grudge) or it festers (+20); nemesis from v5 | Noms, vote, `Confront` | web confront popup | M2 |
| `the-flip` | One swing vote decides it | `block-set`: you hold vote deals or vote promises from fewer than half the voters | SWING, NOMINEE | A `vote_evict` or `vote_save` deal; **Call in the favour** from v5 | `DealObligation` by target (`WebEvictionVoting.cs:275-291`), deal settlement at the reveal | native (BB "flip") | M2 |
| `the-house-turns` | Pile-on | Eviction eve of a week in which T is **not** HoH: ≥3 distinct active holders with grudge ≥40 on T | T, AGGRESSORS | Join or defend | Vote consensus; bonds from v5 | pile_on | M2 |
| `the-conspiracy` | You overhear your eviction being planned | grudge(RIVAL→player) ≥60 and RIVAL has an ally | RIVAL, CONFIDANT, ALLY | Counter-alliance, flip the confidant, strike first | Alliances, noms | branching conspiracy | M2 |
| `power-shift` / `betrayal-evidence` / `secret-alliance-offer` | The other three web stories | Web triggers, grudge-backed | per web | per web | per web | branching | M2 |
| `hoh-room` | The letter, and the lobby | NPC HoH; player's score toward them ≥0 | HOH | **Read it with them**; learn whether you're the target; **Pitch your rival**; the home fact from v4 | This week's nominations | homesick_moment; absorbs `after-the-comp` once staging ships | M3 |
| `the-accounting` | The alliance drags its defector out | Eviction night: an ally voted to evict an alliance-mate | ALLIANCE, DEFECTOR | Sides, the defector cut loose; public fact and ride-or-die from v5 | Grudges, blocs; knowledge threat from v5 | emergent house_meeting | M3 |
| `the-backdoor` | The week's real target was never on the block | Player path: the player HoH sets `SetBackdoorPlan`. NPC path: the NPC HoH's backdoor plan (§4.2 A9) | HOH, TARGET, PAWNS | Pawn promises, the renomination speech, the backdoor slammed shut | Replacement pick (`:206,602`), grudges, receipts | native; completes `SetBackdoorPlan` (`EpisodeEngine.cs:1040-1056`) and 08:97 | M3 |
| `first-night` | The one real conversation of move-in night | The meet-and-greet opening beat (`EpisodeEngine.cs:994-1031`); one card, one pick, no action | any one NPC | Rebuilt web +3 / −3 / +1 on top of the existing +3; learn their conflict style | Score, lore | CampArrival | M4 |
| `photo-on-the-nightstand` | The home fact | Rapport ≥3 and a hidden home fact; ×2 if nominated | SUBJECT | Home fact, `story:heard-out` | Lore, trust | homesick_moment | M4 |
| `the-target-on-their-back` | A big name knows the house is coming for them | All-Stars: an alumnus whose audited record includes a win, week ≤3 | BIG NAME | **Promise to protect them** (a `Safety` promise), **Tell them it's fair**, **Offer them the numbers** (a `vote_together` deal) | Promises, deals, blocs | native, alumni-safe; replaces the thin `legend-talk` | M4, after the bio audit |
| `late-nights` | The showmance that paints a target | Warmest open fictional NPC, mutual ≥25, rapport ≥4 | PARTNER, GOSSIP | Pact alliance, pair threat, veto dilemma, the backdoor | Threat, noms, veto, jury | showmance_rumor | M5 |
| `behind-closed-doors` | You know the secret couple first | You become a knower of an NPC couple | A, B, HOH | Keep, tell the HoH, leverage, **Ask to join** | Noms, hooks, alliances | secret_deal | M5 |
| `loose-lips` | "Your final two got out" | You hold a final-two promise or deal with PARTNER, and an ally outside it becomes a knower | ALLY, PARTNER | **Come clean**, **Deny it**, **Throw them under the bus** | Deals, grudges, alliances | secret_alliance_exposed; 08 shape 4 | M5 |
| `emergency-meeting` | An accusation with the house watching | CALLER grudge ≥60 plus a known fact | CALLER, ACCUSED | Own it, deny, turn it | Trust, noms, facts | crisis house_meeting | M5 |
| `what-they-left-out` | Earning a secret | Depth 3 plus a confidant bond | CONFIDANT, LEAKER | Keep, hook, tell | Hooks, trust, bonds | npc-secret | M5 |
| `ride-or-die` | The final-two pact | Ride-or-die bond, week ≥3 | PARTNER | `PromiseKind.FinalTwo` | `FinalEvict`, jury story term | completes the oath | M5 |
| `call-in-the-favour` | Spend a hook | Holder is HoH, the veto holder or a voter | HOLDER | A `safety_agreement` (HoH), `veto_use` (veto holder) or `vote_evict`/`vote_save` (voter) deal | `NominationPreference` "their word", `SavePreference`, `DealObligation` | completes immunityFromNomination | M5 |
| `staged-feud` | "Give them a show" | A fictional NPC with Bold ≥2 and Sociable ≥2, mutual ≥20 with you, not publicly allied | SHOWRUNNER, BUYERS | A secret alliance behind a public war | Knowledge-gated threat, grudges, receipts | native; Quinn's card (`CastTemplates.cs:145`) | M5 |
| `slop-week` | Have-Not week | Selection at `hoh-crowned` from week 2 with ≥5 active, or a veto punishment | HAVE-NOTS, HOH | Rally, pantry, meltdown | Budget, stress, comps | have_not | M6 |
| `on-notice` | An NPC hothead one outburst from removal | NPC strike 1 | HOTHEAD, TARGET | Step in / egg on / report | Roster, grudges, hooks | native | M6 |
| `diary-room-calls` | Production's ladder for you | Any player strike | player | Warning, penalty, removal | `CompetitionPlayers`, roster | native | M6 |
| `breaking-point` | A CK3 mental break | Overwhelmed and nominated or a Have-Not | the player or a fictional NPC | **Cry it out** / **Snap at someone** / **Go quiet** | Stress, heat, receipts | native | M6 |
| `vets-vs-newbies` | An in-group forms | Mixed cast: ≥2 alumni and ≥2 others | VETS, NEWBIES | Pact against outsiders | Alliances, noms | native, alumni-safe | **Parked**: the cast screen builds one roster (`SeasonBuilder.Create` uses `choice.Roster`), so a mixed cast needs hand-built customs. It returns with the owner's fictional-veterans option or a mixed-roster setting. |

### 4.2 Ten fully sketched arcs

Captions are **bold constants**. ⟨T⟩ marks a trait gate. Odds are tags beside the caption, never in it. Examples use the default eight: the player plus Alex, Emma, Jordan, Casey, Riley, Jamie and Quinn.

#### A1 · `late-nights`: the showmance (M5)

- **Web anchor.** Completes `showmance_rumor` (**Shut It Down** −2 / **Lean Into It** +5), and completes HOUSE-LIFE-PLAN:351's rumour while overriding its hot-tub trigger (§1.3). Declared native: the bond state.
- **Trigger.** Week ≥2. PARTNER is the player's warmest NPC who is romance-open (lore, or Sociable ≥1), fictional, and not in a showmance; mutual ≥25 and rapport ≥4. `romanceStorylines` is on. The player's card is not identity-intact alumni.
- **Weight.** 10. ×2 after two recorded RelationshipBuilding or PersonalChat commands with them this week (from `contacts.weekCount`, never from the hot tub). ×0.3 if Introverted.
- **Cast.** PARTNER (Jordan, the Charmer, is the typical pick) and GOSSIP (the most Sociable non-ally).

**B1 "After Lights Out"** (Approach: Jordan joins you in the hot tub if that is where you are, or at the kitchen table; Personal lane)
- **Lean in**: +6 and a private bond.
- **Keep it strategic** ⟨Strategic or Analytical⟩: an alliance and a final-two offer instead.
- **Just friends**: −2, and this pair never starts the arc again.
- Lapse **Not now**.

**Exposure pulse** (each anchor). Keyed, 20% + 10% if PARTNER is Emotional or Impulsive + 10% if GOSSIP knows. A success makes GOSSIP a knower. Two knowers bring on B2.

**B2 "Everyone's Talking"** (Scene: kitchen table)
- **Own it**: declared; pair alliance through `FormAllianceWith`; permanent `story:showmance`; every non-ally cools −4 one way.
- **Deny it**: check 50, −15 if PARTNER is Emotional. Backfire: PARTNER hears it (−10, `story:snubbed`), and the showmance is exposed anyway.
- **Shut it down**: −2; the arc ends.
- Lapse **Say nothing**: exposed.

**B3 "On the Block"** (Summons at `noms-set` or `veto-won` when either of you is nominated and the other holds the veto)
- **Use it on them**: permanent `saved-with-veto` (an existing type) plus `story:stood-up-for`.
- **Keep the veto on**: `story:showmance-betrayed`, the bond ends, grudge 80.
- If PARTNER holds the veto, `SavePreference` saves you unless Honest ≤ −2 and Steady ≥ 2 wins the keyed draw.

**B4 "They're Coming for Jordan"** (Summons at `veto-won`, when the couple is known and an NPC HoH's backdoor plan names PARTNER, §4.2 A9)
- If you hold the veto: **Keep it in your pocket** (PARTNER stays off the block; the pawn you could have saved takes `story:snubbed`) or **Use it anyway** (the pawn comes down, PARTNER goes up as the replacement: `story:showmance-betrayed`).
- If you don't: the arc hands off to `the-flip` with PARTNER as the nominee to save.

**Consequences.** Pair threat, split-the-couple nominations, the backdoor, the partner veto save, and the jury story term (+8 kept or −12 betrayed) if PARTNER reaches the jury.

**Recap:** "Late Nights: you and Jordan stopped pretending. The house is counting you as one vote."
**Diary:** "I didn't come here for this. But here we are."

#### A2 · `kitchen-blowup`: an NPC-to-NPC fight to exploit or defuse (M2)

- **Web anchor.** Proximity `npcs-arguing` (Side +4 / Neutral +1), and emergent confrontation (+8/−6; Stay out −2/−2; Mediate +3/+3, A↔B +5).
- **Trigger.** At eviction-night or block-set, week ≥2: an NPC pair where one holds a grudge ≥40 against the other (every nomination writes one), or mutual ≤ −10. Weight 10, ×1.5 if either is the HoH or a nominee, ×2 if Quinn's `CentreOfTheStory` goal is unmet.
- **Cast.** HOTHEAD (higher Volatility), TARGET, and up to 2 WITNESSES. If HOTHEAD is below Volatility 1, the arc plays as the Cold Shoulder variant.

**B1 "Words in the Kitchen"** (Scene · kitchen; Conflict lane)
- **Back the one who started it** / **Back the one on the receiving end**: +6/−8, with `story:stood-up-for` or `story:sold-out`.
- **Calm them both down**: check 45; +15 if you are Charming, Funny or Flexible; +10 if you know HOTHEAD's hot-button (from v4). Success: A↔B +5, each +3 toward you, the grudge −20, heat −1. Backfire: "Don't tell me to calm down", both −5, and **you learn the hot-button**.
- **Egg it on** ⟨Sneaky or Manipulative⟩: check 55. Success: A↔B −12, grudges +20 both ways. Backfire: both hold grudge 40 toward you.
- Lapse **Stay out of it**: HOTHEAD's `AiChance` decides between fizzle, apology and heat +1.

**Cold Shoulder variant** (HOTHEAD below Volatility 1, for example Riley with a grudge against the HoH who nominated him)
- **B1 "The Silent Treatment"** (Scene · bedroom): Riley stops talking to Emma and starts counting votes.
- **Pick a side** (as above, +6/−8).
- **Broker a truce**: check 40, +15 if Social, Charming or Loyal. Success: grudge −20, both +3 toward you.
- **Use it** ⟨Strategic⟩: tell Riley what Emma said about him. A↔B −8 (one way, Riley's view), and Riley +4 toward you. Backfire (keyed on Emma's Sociable): Emma learns it came from you, grudge 40.
- Heat never passes 1, so no blow-up and no strike.

**B2 "The Blow-up"** (Meeting · living room, 3+ gathered; block-set when heat ≥2)
- **Break it up**: check 35, +15 if Social, Charming or Loyal. Success: both write `story:stood-up-for`, and witnesses +2 trust. Backfire: you are Rattled (−1 competition for a week).
- **Take a side** carries over from B1.
- Lapse **Walk away**.

From M6, a fictional HOTHEAD at Volatility ≥5 may **escalate** by `ai_chance`. That is strike 1, and `on-notice` starts.

**B3 "Thanks for Having My Back"** (Approach, next Social): **Make it official** (an alliance through `FormAllianceWith`) or **Keep it loose** (a `vote_together` deal).

**The exploit.** Each headliner now ranks the other lowest, so whoever becomes HoH nominates the other (`NominationPreference`).

**Recap:** "The Kitchen Blow-up: Quinn and Riley had it out over the dishes. You backed Riley. It held."

#### A3 · `on-notice`: the NPC disqualification story (M6)

- **Declared native.** It follows the shape of the real removals: usually the end of an escalation the viewers watched build (08 §B4).
- **Trigger.** A fictional NPC takes strike 1. Both headliners are non-alumni. In the default eight this is Quinn's arc by design: base Volatility 3, and a nomination puts her at the ≥5 needed to escalate. In the shipped six it is Taylor Kim's (base 6).
- **Each anchor** runs an NPC-only Flare-up by `ai_chance`: back down, shout (heat +1), or **escalate** (a strike, at most one a week). Escalation is weighted ×(1 + Volatility/4), and ×1.5 if stressed, nominated or a Have-Not.

**Player beat "They're at It Again"** (Pull, once witnessed: "Two houseguests are shouting in the bedroom")
- **Step in**: check from your trust with both. Success: heat −2, `story:stood-up-for` with TARGET, and a **hook** on HOTHEAD ("you owe me"). Backfire: you become HOTHEAD's target (grudge 50).
- **Stay out of it**.
- **Egg them on** ⟨Manipulative or Sneaky⟩: doubles HOTHEAD's next escalation weight. If TARGET becomes a knower: grudge 60 toward you.
- **Report it** (Diary; only after strike 1): adds a strike. If HOTHEAD's allies become knowers: `story:sold-out`.

**Ending.** Strike 3 can land only in a post-eviction Social beat with ≥4 active, in a legal window (§3.5: weeks 3 and 4 in the default eight; never in the 6, where the arc ends at a second penalty). The removal applies as the Social window closes, with the takeover "Removed from the house", the feeds' holding screen (the "fish", 08:89), and the house's Shocked reaction.

**Consequences.** `Active` shrinks for every system; the jury loses a seat. HOTHEAD's allies who know you reported it take grudge 60. Three clean weeks clear a strike.

**Recap:** "Production removed Quinn from the house. Some of you saw it coming."

#### A4 · `diary-room-calls`: the player's ladder (M6)

- **Trigger.** Any player `conduct` option, for example **Get in their face** ⟨Confrontational or Impulsive⟩ in `bad-blood` (never offered against an alumnus, or to a player on an alumnus card), tagged **Rule break · Strike 1 of 3** and confirmed with **Do it**.
- **B1 "The Diary Room Calls"** (Summons; the objective chip reads "The Diary Room is calling you")
  - **Understood**: −1 stress.
  - **Ask what happens next**: the ladder becomes a line in the tracker, with the legal removal weeks for this house.
  - **Push back** ⟨Stubborn or Confrontational⟩: no stress relief, and the strike needs a fourth clean week to clear. No roll, no persona.
- **B2 "Penalty"** (a public takeover, then a Summons): the Have-Not week and sitting out the next HoH.
  - **Take it on the chin**: every houseguest who witnessed the incident marks `story:took-it` (+4, fading) toward you.
  - **Blame the house**: −1 stress (it felt good), but the other party to the incident takes +20 grudge and every Honest ≥1 witness marks `story:snubbed`.
- **B3.** In a legal window, any later conduct option reads **Rule break · Production will remove you**, exists only in a post-eviction Social beat with ≥4 active, and needs its confirm. Committing it locks your Social verbs; the Advance out of Social removes you, and you spectate the rest. Outside a window it is the muted locked line; in the 6 or under Lenient it is **Rule break · Another penalty**.

**Recap:** "Production had a word. Then another."

#### A5 · `hoh-room`: the letter, and the lobby (M3; home fact v4)

- **Web anchors.** `homesick_moment` re-aimed at the HoH, plus the `post_hoh` "Pitch your rival".
- **Trigger.** `hoh-crowned`, an NPC HoH, and the player's score toward them ≥0. From v4, ×2 if the HoH has authored family lore.
- **Window.** The Nomination phase. The lapse resolves **inside the `Advance` that commits NPC nominations** (`EpisodeEngine.cs:187-191`), before `Nominate` is called, so a success answered earlier in the phase has already moved the scores that sort reads. (The Advance that nominates does not leave the phase; only the next one does, `:193`.)

**B1 "The Letter"** (Approach → HoH room; M3 ships it fact-free)
- **Read it with them**: +6, `story:heard-out`. From v4, a home fact. For an alumnus HoH the option is **Congratulate them** instead: the letter's contents would be an invented family.
- **Talk game instead** ⟨Strategic⟩: check 50. Success: they tell you whether you are in their bottom two right now (computed at resolution; the scores can still move), +8. Backfire: −4.
- **Pitch your rival** / **Pitch the comp threat**: the role is resolved at draw time and named in the description. Check from `StoryOdds`. Success: HoH→target −12, one way. Backfire: the target finds out (−8 toward you, one way, and grudge 40; from v5 they become a knower).
- Lapse **Stay downstairs**: −2.

**Recap:** "The HoH Room: Emma read you her mom's letter. She didn't put you up."

#### A6 · `the-flip`: the swing vote (M2; favour option M5)

- **Declared native, on the Big Brother "flip" shape** (08 §B3, shape 3).
- **Trigger.** `block-set`, with the player a voter or nominee who holds vote deals or vote promises from fewer than half the voters. This uses only the player's own knowledge.
- **Cast.** SWING: a voter who has promised the player nothing, with a middling score. The trigger may read hidden state to cast them; the card says only what the player knows. NOMINEE.

**Why a deal and not a promise.** `DealObligation` reads a promise only between the voter and the nominee being evaluated (`WebEvictionVoting.cs:263-274`), and a promise carries no target (`:22-25`). A vote promise from SWING to a player who is a voter, not a nominee, adds nothing to either nominee's evaluation. A deal with a target is read by target (`:275-291`): `vote_evict` is −35 on its target, `vote_save` +35.

**Beat "The Count"** (Conversation with SWING; costs an action)
- **Make the pitch** (pickPerson: one of the two nominees; odds; +10 if they are Loyal and you share a deal already). Success: an active `vote_evict` deal between you and SWING naming that nominee, or a `vote_save` naming you if you are on the block. Backfire: keyed on SWING's Sociable − Honest, SWING tells the HoH (−6 toward you, one way).
- **Call in the favour** (from v5; needs a hook): the same deal with no check.
- **Let it ride**.

**Resolution** happens through the existing vote. The deal is strong, not certain: −35 against a relationship term and a bloc directive (−40) that can outweigh it. It settles at the reveal with `SettleDeals(Votes)` (`EpisodeEngine.cs:312`): kept, it is a fulfilled deal in the jury's `Obligations`; broken, the engine scores SWING as the betrayer exactly as it does for a hand-made deal, including its own witness rolls (`SpreadBetrayal`, `:1621-1632`), and a broken deal counts against SWING in `Obligations` if either of you reaches the finale. If the HoH's target survives, the HoH takes grudge 40 against each revealed flipper (native: the web's voted-against grudge belongs to the nominee, not the HoH).

**Recap:** "Nobody saw it coming. Well, you did."

#### A7 · `the-accounting`: the web's never-called house meeting (M3; v5 terms)

- **Web anchor.** `houseMeetingEvent` (`emergent-event-system.ts:152-188`).
- **Trigger.** Eviction→Social. DEFECTOR voted to evict an alliance-mate who was on the block; the votes survive until `week++` (`EpisodeEngine.cs:165`). The ally they voted against holds the web's voted-against grudge, 40 ×1.2 = 48 while the alliance is Active (`grudge-system.ts:139-151`). The other members' 30 against DEFECTOR is native.
- **Alumni-safe.**

**B1 "Living Room, Now"** (Meeting; 3+ gathered)
- **Back the alliance**: +3 with each member, DEFECTOR −5.
- **Defend the defector**: each member −5, DEFECTOR +12. From v5, a success check on DEFECTOR's Honest makes it ride-or-die.
- **Turn it on them** ⟨Confrontational⟩: the alliance is finished in front of the house; every member takes grudge 50 toward you. From v5 its fact also goes public.
- Lapse **Stay quiet**.

**Consequences.** DEFECTOR leaves the alliance (the web's alliance-betrayed grudge, 80, from the members), and a two-person alliance ends (jury `AllianceLoyalty` falls from 100 to 25). From v5, knowledge-gated `AllianceThreat` sees the alliance once its fact is public.

**Recap:** "Riley's vote didn't stay secret. The alliance called the house together."

#### A8 · `what-they-left-out`: earning a secret (M5)

- **Web anchor.** Proximity `npc-secret` (Keep it +5 / Leverage −4).
- **Trigger.** CONFIDANT has depth 3 known and a confidant bond, or you win an `Eavesdrop` on them. LEAKER is Sneaky or Deceptive, allied with them, and optional.

**B1 "Can You Keep a Secret?"** (Conversation)
- **Promise to keep it**: the depth-4 fact and `story:secret-kept`.
- **Ask why they're telling you** ⟨Analytical or Intuitive⟩: an extra fact.
- **Remember it for later**: a hook; one stress step if you are Loyal.
- Lapse **Change the subject**.

**B2 "Somebody Else Knows"** (Approach at block-set on a leak, using the leak odds)
- **Swear it wasn't you**: check 70, or 30 if you used the hook.
- **Tell them who did** (needs a recorded Eavesdrop on LEAKER): CONFIDANT↔LEAKER −15 and `story:secret-exposed` between them. This is the NPC-to-NPC exploit.
- **Own it**.

**Consequences.** Hooks become `call-in-the-favour`; trust; ride-or-die eligibility.

**Recap:** "What They Left Out: Jamie told you she's watched every season since she was twelve, and she's been pretending she hasn't. You kept it." (Her secret is on her sheet, §4.4. She is still a nurse; the card stays true.)

#### A9 · `the-backdoor`: the week's real target (M3)

- **Declared native.** It completes Unity's own `SetBackdoorPlan` (`EpisodeEngine.cs:1040-1056`), whose plan today changes nothing but a Diary Room paragraph, and 08's showmance shape ("one is often backdoored", 08:97). The recap already surfaces `backdoor` lines (`WeeklyRecap.cs:204`).
- **NPC plan (native rule, v3).** Inside the Advance that commits NPC nominations, if the NPC HoH's lowest `NominationPreference` candidate T has a competition record (any HoH or veto win, or the top competition stat among candidates) and ≥6 are active, the HoH nominates the next two lowest instead (the PAWNS) and the cycle records the plan (`planned = 1`, T in the cast). At the veto meeting, if the veto is used and T is eligible, the replacement pick (`:206`, `:602`) is T. Deterministic, no roll. The plan lives in the cycle, not in `backdoorTargetId`, which validation forbids from naming the player (`EpisodeValidation.cs:104`).
- **Player plan.** Setting `SetBackdoorPlan` as HoH starts the cycle with the player as HOH.

**B1 "Just a Pawn"** (Conversation at `noms-set`)
- When an NPC HoH has put **you** up as a pawn: **Take their word for it** (a `Safety` promise from the HoH; if you go home it is broken, with the web's 60), **Get it in writing** ⟨Analytical⟩ (a `safety_agreement` deal instead, which the jury's `Obligations` weighs more heavily when broken), **Call it what it is** ⟨Confrontational⟩ (−6 toward the HoH, one way; you learn who T is and can warn them).
- When **you** are HoH with a plan: **Tell them they're a pawn** (per pawn; if that pawn goes home they take the web's lie grudge, 50, and mark `story:sold-out`) or **Keep them guessing** (the pawn cools −4 toward you, one way).

**B2 "The Veto Meeting"** (Meeting at `veto-won` when the veto is used and the player names the replacement; a Ringer, §4.3)
- **Say it to their face**: every witness marks `story:heard-out` toward you; T's grudge is the full 70.
- **Keep it short**: nothing more.
- **Blame the house**: T's grudge −20, but every Honest ≥1 witness marks `story:snubbed` ("they heard you pass the buck").

**B3 "The Backdoor Slammed Shut"** (block-set, if T won the veto or nobody used it): if any pawn was told, keyed on the pawn's Sociable − Honest, T learns of the plan and takes grudge 60 toward the HoH.

**Recap:** "The Backdoor: you told Casey she was a pawn. Alex never saw the replacement coming."

#### A10 · `staged-feud`: "Give them a show" (M5)

- **Declared native.** The Big Brother shape: a fake fight to hide a real alliance. Quinn's card is the hook: "Fame-seeking manipulator who plays for the cameras. Will create drama for entertainment." (`CastTemplates.cs:145`).
- **Trigger.** Week ≥2. SHOWRUNNER is a fictional NPC with Bold ≥2 and Sociable ≥2, mutual ≥20 with you, and no public alliance with you. In the default eight that is Quinn; in a custom house, anyone with the axes. ×2 when Quinn's `CentreOfTheStory` goal is unmet.

**B1 "Give Them a Show"** (Approach: Quinn corners you on the living-room couch, where the cameras are)
- **Let's do it**: a private two-person alliance through `FormAllianceWith` (with its private fact), and a public fact: "you and Quinn are at war".
- **Do it for real** ⟨Confrontational⟩: no alliance; an actual public row, `story:argued`, and she is delighted anyway.
- **Not my style**: Quinn −4 toward you, one way, and she finds another co-star (an NPC-only thread).

**Payoff while it holds.** Knowledge-gated threat does not count the alliance, and nobody reads you as a pair. Every NPC holding a grudge ≥40 against SHOWRUNNER warms +4 toward you, one way: the enemy of my enemy. Those NPCs become the BUYERS.

**Exposure pulse** (each anchor). Keyed, 15% + 10% if either co-star is Sociable ≥2 and Honest ≥1 (Quinn is both: she brags).

**B2 "The Fight"** (Scene at block-set, living room)
- **Sell it** ⟨Confrontational, Impulsive or Bold ≥2⟩: check 60. Success: the war is public again for another week. Backfire: somebody saw you laughing about it afterwards, and one witness becomes a knower of the alliance.
- **Tone it down**: the feud cools; the exposure chance halves, and so does the BUYERS' warmth.

**B3 "It Was Fake?"** (Meeting, once the alliance is known to three or more)
- **Own it** ⟨Bold⟩: the BUYERS each take the web's lie grudge (50) toward both of you, and the alliance is public.
- **Blame Quinn**: Quinn takes grudge 60 toward you; the BUYERS' lie grudge toward you halves.
- **Deny it**: check 35. Backfire: the lie grudges stack (+0.5×).

**Recap:** "Give Them a Show: you and Quinn screamed at each other over the pantry. Nobody knows you haven't split a vote since week 2."

### 4.3 The Ringer: cheap Big Brother set pieces

Small, recognisable moments that mostly reuse state the engine already holds. Each still names its consumer.

| Set piece | When | What the player does | Consumer | Cost | M |
|---|---|---|---|---|---|
| **Renomination speech** | Veto meeting, when the player names the replacement (A9 B2) | Say it to their face / Keep it short / Blame the house | Grudge size, witness receipts | One Meeting beat | M3 |
| **Goodbye messages** | Eviction night. The player records one for the evictee inside the existing post-eviction Diary Room visit, not counted as an ask. | **Keep it classy** (+4 on the new juror's view of you, through `ChangeKeyed`); **Tell them why** (+2, or +6 if their primary trait is Confrontational, whose jury answers reward direct honesty, or Analytical, whose answers reward a reasoned move; `WebJuryQuestioningCatalog.cs:34,44`); **Rub it in** ⟨Confrontational⟩ (−8, and −1 stress for you) | The juror's relationship term (0.3 on the jury score) | One beat | M3 |
| **Your goodbye messages** | When the player is evicted | Watch one message from each houseguest, built from their ballot reason (`WebEvictionVoting.ExplainNative`) and the receipts between you. This is where the knowledge boundary opens, as it does on the show. | Presentation only | Text | M3 |
| **HoH room reveal** | hoh-crowned | The house piles into the HoH room; photos, the letter, the snack basket. Stages `hoh-room` B1. | Staging for A5 | Scene | M3 |
| **Prank war** | A quiet Social window, two NPCs with mutual ≥20 and Funny or Impulsive | **Prank them back** / **Call a truce** / **Go too far** ⟨Impulsive⟩ (heat +1: it becomes an argument) | Receipts (`story:heard-out` or `story:argued`), heat | One Scene | M3 |
| **Veto punishment** | Veto comp, last-place veto player | Nothing to choose: a one-week Have-Not punishment, or a presentation-only costume if Have-Nots are off | The Have-Not consumers | Reads `competitionScores` | M6 |

### 4.4 Two lore sheets, written

Proof of voice. Both are fictional regular-roster NPCs, and both agree with their public cards.

**Jamie Roberts** (sheet `jamie-roberts`; card: 27, Nurse, Boston, MA, Emotional/Strategic, home room Kitchen; `CastTemplates.cs:135-140`)

| Facet | Depth | Text |
|---|---|---|
| Origin | 1 | Grew up in Dorchester, the oldest of four. Her mom worked nights, so Jamie made the school lunches. |
| Home | 2 | Still lives in a two-family house with her mom downstairs. Her boyfriend is a Boston firefighter; if she wins HoH, the letter is from him. |
| Work | 1 | ER nurse, twelve-hour night shifts. She still wakes at 3 a.m. and wipes down the kitchen. |
| Respects (primary trait: Emotional) | 2 | Someone who owns it when they hurt her. Tell her you're sorry and mean it, and she'll remember. |
| Unforgivable | 3 | Being lied to about a vote. |
| Hot-buttons | 2 | Being called "too emotional to play this game" (game talk); wasted food (meal prep, Have-Not weeks); being called the house mom. |
| Comfort | 1 | The kitchen. A stressed Jamie is at the stove, and whoever eats what she cooks is safe for a day. |
| Goal | 2 | `HerOwnMove` (§3.7). |
| Conflict style | 2 | Warm until she's lied to, then loud and in public. She'll hug you afterwards and still vote you out. |
| Romance | 1 | Closed: the firefighter. |
| Secret | 4 | A superfan. She has watched every season since she was twelve; her little brother Danny applied four times and never got the call, and she is playing his notebook. She tells the house she has "seen a couple of seasons". Exposed, it reads as a strategic threat: each Strategic or Analytical NPC cools −4 toward her, one way. |

**Quinn Martinez** (sheet `quinn-martinez`; card: 25, Social Media Influencer, Los Angeles, CA, Confrontational/Social, home room Living; `CastTemplates.cs:141-146`)

| Facet | Depth | Text |
|---|---|---|
| Origin | 1 | Grew up in Riverside and moved to LA at nineteen with one suitcase and a ring light. |
| Home | 2 | A studio apartment in Echo Park she films everything in. Her mom watches the feeds and texts her manager. |
| Work | 1 | Four hundred thousand followers for "brutally honest" makeup reviews. She knows where every camera in the house is. |
| Respects (primary trait: Confrontational) | 2 | Someone who says it to her face. |
| Unforgivable | 3 | Being called fake, or being ignored. Boring is the only thing she can't survive. |
| Hot-buttons | 1 | "Calm down"; "clout-chaser"; people who whisper about her in the storage room. |
| Comfort | 1 | The living-room couch, facing the cameras. |
| Goal | 2 | `CentreOfTheStory` (§3.7). |
| Conflict style | 1 | Public and on camera. With two or more witnesses her escalation weight doubles; alone, she backs down. |
| Romance | 2 | Open, strategically: "a showmance is content." |
| Secret | 4 | Her manager told her the brand deal only renews if she makes jury. Exposed, the house re-reads every fight she ever picked: each houseguest she has argued with this season marks `story:sold-out` toward her. |

---

## 5. Where it happens

### 5.1 Staging: Scene → Pull → Moment → Fallout

| Step | Host | New work |
|---|---|---|
| **Scene** (3D, no panel) | Pair venue leases (`HouseMeetingCoordinator.TryReserveAtVenue`); argue flags (`EpisodeDirector.NpcSocial.cs:350-393`) | **Gather first.** Pairs use a lease. Three or more use `BeginSceneStage`, which generalises the yard-only `BeginCompetitionStage` (`HouseMeetingCoordinator.CompetitionStaging.cs:31-50`) to any room as an unsaved lease that borrows idle actors. The live feed prefers the scene ("KITCHEN · SOMETHING'S GOING ON"), and the room beacon wears `icon_drama`. |
| **Pull** (non-modal) | The Nearby card (`EpisodeHud.Chrome.cs:561-610`), toggled by `SetNearby` **without** `Render()` | Modes: Scene (**Step in** / **Stay out of it**), Approach (**Hear them out** / **Not now**), Meeting (**Join the meeting**). A lane glyph from the unused pack art, and one stakes line. It names nobody until you arrive. |
| **Moment** (modal) | `EventChoices` (`EpisodeHud.Decisions.cs:88-161`); conversation beats above the dial (the oath-block slot, `EpisodeDirector.cs:1078-1084`); Summons in the Diary's pending tab | A `sceneCardOpen` state joins `IsPanelOpen` (`EpisodeDirector.cs:89`), so the card opens where the scene is; this is the one new modal state (§1.3). `FrameHouseEvent` returns early unless `phaseOpen` (`EpisodeDirector.Camera.cs:97`), so its guard widens to `phaseOpen \|\| sceneCardOpen`; only then can the 3 m test (`:102`) pass for a scene card. The station still shows the card as a fallback. |
| **Fallout** | Status line, `OutcomeChips`, `CeremonyTakeover`, `TurnHeads` | New ceremony kinds `blowup`, `penalty`, `expulsion`, `house-meeting`. Each is registered in **every** table (`TitleFor`, `FlavourFor`, `IconFor`, `IsCeremony`, `CeremonyRoom`, audio, House Vibe, Recent Events, `WeeklyRecap.Moments`), and a test enumerates them. Appended Reactions play before the card freezes the house (`EpisodeDirector.NpcSocial.cs:76-78,317-329`). |

**The five new Reactions need clips.** `Reaction` drives Animator triggers by hash (`CharacterPresentation.cs:71-77`), and a body whose controller lacks a parameter already skips it silently (the `reactionParams` mask, `:471,657`). So the appended values are safe to ship before their clips exist. The art budget is 5 clips on each of the two rigs (Quaternius and UMA), sourced outside the free library if need be. Until then the director substitutes: Shocked → Nominated, Tearful → Evicted, Embrace → Saved, Furious → a head-turn only, and StormOff → a walk to the comfort room (locomotion, no clip).

Summons do **not** use `pendingDiary`, so the one-diary-per-week guard (`HOUSE-LIFE-PLAN.md:350`) and `Advance`'s diary check stay untouched.

**Proximity loses its auto-submit** (`EpisodeDirector.Conversation.cs:320-346`). **Step in** issues `WitnessProximity`; **Stay out of it** creates nothing.

### 5.2 Pacing: `StoryAirtime` replaces the weekly slot

**The anchors, as engine moments:**

| Anchor | Where in the engine | New arcs may start here? |
|---|---|---|
| hoh-crowned | The Advance that enters Nomination | Yes: HoH-anchored templates (`after-the-comp`, `hoh-room`, `slop-week` selection) |
| noms-set | Nominations committed (`Nominate`) | No: running arcs only (for example A1 B3). The exception is `the-backdoor`, whose cycle starts here because its plan is made with the nominations. |
| veto-won | The Advance that enters VetoMeeting | No: running arcs only (A1 B3-B4, A9 B2) |
| block-set | The veto resolved; Campaign opens (`EpisodeEngine.cs:212`) | Yes |
| eviction-eve | The Advance out of Campaign (`:225-230`) | No: running arcs only (`the-house-turns` is the exception: it starts here, because its target must be exposed) |
| eviction-night | Eviction → Social (`:232-255`) | Yes |

Past the boundary, `HouseEvents.Ready` defers to these rules:
- **One open ask at a time.**
- **At most 2 asks a week, at any anchor, plus 1 Summons.** Production must-fires skip the chance roll but count.
- **The first ask of a season** can open at week 1's eviction night. Before that, the only story content is the `first-night` card, which rides on the existing opening beat and counts for no week.
- **Three lanes (Personal, Conflict, Game), at most one active arc each.** Production sits outside the cap.
- **Cooldowns.** A headliner rests a week. Pair cooldown 2 weeks, template cooldown 4 weeks.
- **A Nothing weight** on new-arc pools: 40 at eviction night and 60 elsewhere. A running arc's next beat does not roll Nothing.
- **Ambient narration never takes a slot** (gap map §0, row 7).
- **NPC-only threads** surface as one Recent Events whisper per thread per week, **never as a card**, until the player becomes a knower.
- **Before you Advance**, the advance control warns in a line **above** it (`EpisodeDirector.cs:1291`), never in its caption: "Moving on lets 1 storyline pass: The Kitchen Blow-up (you'll stay out of it)."
- **Tease, don't nag.** Only production moves the objective chip. Optional scenes get the drama glyph, not the breathing next-stop ring (`EpisodeTravelBeacons.cs:402-411`).

**Totals.** The ceiling is 1 ask in week 1 plus 2 in each later week with anchors: 9 asks and 5 summons in the default eight, 5 and 3 in the shipped six, 17 and 9 in a 12-seat house (§1.4).

**Rate targets, measured over the 1,920-season harness:**

| Measure | Target |
|---|---|
| Weeks with a card | 30-60% |
| Asks per default season | 4-6 (ceiling 9) |
| Showmances per season | 0.5-1.5 in seasons with fictional NPCs; 0 in pure All-Stars |
| Seasons with an NPC expulsion | at most 10% of seasons that have a removal window (casts of 7 or more), reported per cast size |
| Seasons with a pile-on (`the-house-turns`) | at most 25% |
| Pariah | in at least 80% of seasons, no houseguest is the target of 3 or more distinct active holders at ≥40 for 2 or more consecutive anchors |
| Arcs finished per default season | about 3 |

The pariah measure counts distinct holders, because grudges stack per holder-target pair and one holder can never hold three against the same person. `the-house-turns` never fires against the reigning HoH: every used veto leaves three holders at 70 against that week's HoH at block-set (both original nominees and the replacement), which would otherwise trigger it almost weekly against someone who is immune.

### 5.3 Readable odds and causality

**Tile anatomy.** The caption constant comes first. The description follows. Then up to three stakes chips as word plus colour: "Riley will resent this", "The house sees it", "Uses an action", "Strike 1 of 3". Then an odds word with "about 7 in 10", a trait chip, a price chip ("Out of character: stress"), an **Unknown** chip where a hidden facet could change the payoff, and the lapse marker ("If you let it pass"). A conduct tile adds the two-press confirm (§2.4).

**Show the odds** is a toggle modelled on `ShowCandidateContextCaption` (`EpisodeHud.Decisions.cs:12-13,44-51`). It lists only the visible terms: "Base 45 · your standing +12 · you're Charming +15 · Quinn is Confrontational −10 · about 6 in 10".

**Locked options** appear as one muted line ("Not in your character: Egg it on (Sneaky or Manipulative)"), never as disabled Selectables. A disabled Selectable under a non-interactable group bakes in half alpha.

**Receipts:**
- **Between you** in the profile, for example "Week 3 · The Kitchen Blow-up · you backed Riley · lasting".
- **On the Votes page and on nominations:** "Riley voted to evict you. Between you: week 2, you nominated Riley." The line records facts and never claims a motive. It is built only from receipts the player was party to or witnessed.
- **A Previously line** on every beat, from the web's `StorylineEventDialog`.

**The tracker** is a STORYLINES block at the top of the notebook's Story section (`EpisodeDirector.cs:811-818,889-926`). For each arc it shows the lane, the path, a heat word, the next hook, what you carry (modifiers with weeks left, hooks, "Production: 1 warning", "Clean weeks 1 of 3"), and **Find them**.

**The recap as an episode** (`WeeklyRecap.cs:79-84,202-211`):
- **Previously on**;
- one act per anchor;
- the player's own Diary Room quotes (a `quoteKey` on each confessional option);
- **Next time on**, drawn from open threads with a `nextAnchor`.

### 5.4 The house reacts

- Headliners argue for about 8 seconds in every 30, then sulk facing apart, and witnesses turn their heads. Cold Shoulder headliners never argue; they avoid each other's rooms.
- Nemeses avoid shared venues, and partners seek each other out. Both are planner biases only.
- Have-Nots sleep on the cots.
- A declared couple gets `chip_romance` on the cast rail. Public relations only.
- Mood eyes follow `stressLevel` (`CharacterPresentation.cs:360-397`).

### 5.5 Accessibility

- **No timers.**
- **Risk, odds and stakes** are always a word as well as a colour.
- **Caption constants** for every new control: `StepInCaption`, `StayOutCaption`, `HearThemOutCaption`, `NotNowCaption`, `JoinMeetingCaption`, `ShowOddsCaption`/`HideOddsCaption`, `FindThemCaption`, `DoItCaption`, `BackCaption`.
- **Keyboard and controller.** Every new control is on the ring (`EpisodeHud.cs:1488-1525`). The Pull's primary control takes the interact shortcut; the secondary is reachable only through the ring, so a heavy choice never happens on a stray press. The conduct confirm never defaults to **Do it**.
- **Text boxes** are sized at 1.3× their font or more (the Inter truncation rule).
- **Reduced motion.** Every scene reads from its bubble sentence. Reactions are recorded but not played (`CharacterPresentation.cs:447-472`).
- **Content comfort.** Physical aggression is only ever the strike line, never a menu of violence. Slurs are never authored. `romanceStorylines` can be turned off per season.

---

## 6. How consequences reach the game

| Effect | Field written | Consumer (existing code unless marked new) | Where the player sees it |
|---|---|---|---|
| `move` with the player | Score, via `ChangeKeyed` | NPC HoH sort (`EpisodeEngine.cs:190`; `NominationPreference` from M2); vote relationship factor (`WebEvictionVoting.cs:389`); jury 0.3 (`WebJuryVoting.cs:132`); oath at 75 | Stakes chip; profile |
| `move` NPC↔NPC | Score, via `Move` + `Record` | The same evaluators, between NPCs | Recent Events whisper |
| One-way `view` | `WriteScore` + `RecordOneWay` (**new**) | NPC HoH sort (pitch); vote | Outcome line |
| `receipt` | `story:*` ledger mark | `TrustScore` at a third of a point per impact point (`ThreatAssessment.cs:157-170`) → alliance desire (`NpcAlliances.cs:75-81`), deal odds (`PlayerDeals.cs:148`), reputation threat | Between you; ballot lines |
| `grudge` | `story.grudges` | **New** `NominationPreference` (`:190,206,602`) and `SavePreference` (`:554`); vote grudge factor; jury story term; `Confront` (`NpcSocialActions.cs:304-319`); `WouldPropose` | Receipts only; never the severity |
| `bond` | `story.bonds` | Vote bond factor; `SavePreference`; `NominationPreference`; jury story term; comfort relieves stress | Tracker; cast-rail chip if public |
| `alliance` | `alliances`, via `FormAllianceWith` (**refactor**) or `FormFromStory` (**new**) | `AllianceLoyalty` (vote and jury, `WebJuryVoting.cs:76-86`); `WebVotingBlocs`; `AllianceThreat` | Notebook alliances |
| `promise` / `deal` / `oath` | Existing lists | `DealObligation` reads promises by pair and deals by pair **or target** (`WebEvictionVoting.cs:260-291`), so story vote pressure is a `vote_evict` or `vote_save` deal; `Obligations` (`WebJuryVoting.cs:96-115`); `FinalEvict` (`:661-662`); oath enforcement; "their word" in `NominationPreference`; `veto_use` in `SavePreference` | Existing deal and promise rows |
| `backdoor` | Cycle cast and vars | The replacement pick (`:206,602`); the player's plan (`backdoorTargetId`) | Tracker; recap |
| `jury story term` | Reads grudges and bonds | **New** `JuryStory` in `WebJuryVoting.Score`, −15..+12, beside the ±10 final impression; `Obligations` stays the web's | Finale "Between you" |
| `fact` / `reveal` | `story.facts` knowers | Knowledge-gated `AllianceThreat` (**new signature**; a fact-less alliance counts as public); `SpreadInfo` subject; the rumour mill | "What you know" |
| `lore` | `story.knownFacts` | `StoryOdds`, gates, jury-question hint | Profile, "What you've learned" |
| `hook` | `story.hooks` | Spent into a promise or deal, which the rows above read | Tracker |
| `modifier` | `StoryModifierState` (+`ownerId`) | `CommonCompetitionBonus` (`:365`); `SocialActionBudget` (`:502-510`); a **new** gated NPC term in `ResolveCompetition` (`:449-451`) | Tracker, with weeks left |
| `stress` | `stressLevel` | **New**: competition −0.5 at Stressed, −1 at Overwhelmed; the Volatility term; odds | HUD mood; Previously line |
| `strike` | `story.conduct` | Ladder; `CompetitionPlayers`; Have-Nots | Tracker; Summons |
| `havenot` | `story.haveNots` | Owner-scoped modifier; NPC turn count; stress | Rail; the cots |
| `expel` | Status `Expelled`, `removals` | `Active`; the jury, excluded; `CareerLedger`; memory wall; report | Takeover; memory wall |
| `final-speech fragment` | Ended cycles **the player was party to or knows about** | `WebFinalSpeeches.GenerateNative` `{storyline}` (keyed roll) | Finale speeches |
| (never) | `playerPersona` | Only the post-eviction diary writes it; validation ties each entry to a resolved diary week (`EpisodeSocialHistoryValidation.cs:11-35`) | Diary |
| (never) | `jurySentiment` | Display only: Diary Room (`EpisodeDirector.DiaryRoom.cs:416,508`), `CareerLedger` | Relabel it "impression" |

**Effective weights, stated once.** On the jury: relationship 0.3 per point, `Obligations` 0.15 per point (so ±7.5 at its ±50 clamp), a current alliance +15, a former one +3.75, the new story term up to −15/+12, and one final impression anywhere in ±10. On trust: a third of a point per ledger point. On the vote: the story grudge factor down to −25, a targeted vote deal ±35, a bloc directive −40.

---

## 7. Engineering

### 7.1 The 14 hard constraints

| # | Constraint | How it is met |
|---|---|---|
| 1 | RNG stream | `StoryRandom` (wrapping `HashSeed` in a `SeededRandom`, §2.5) plus `ChangeKeyed`; `FormAllianceWith` so an alliance effect spends no main-stream roll; guards run before draws; logs are written last; legacy producers move to the story stream past the boundary; final-speech roll keyed. Test: `randomState` is identical for every option of every beat, and after every pulse. |
| 2 | Rules-start week | `story.rulesStartWeek` (migration and import set week + 1) plus `story.rulesVersion` staging M1-M6. The existing `storyRulesStartWeek` keeps gating v12 storylines. Every pulse, consumer term, Roles fix and arc-leak fix checks both new fields. Fixtures set the week past their length. |
| 3 | Schema 14 | One bundle (§7.3). `FrozenEpisodeV13` with a private frozen `CharacterAppearance`; `UpgradeV13ToV14`; the literal-13 sites (`EpisodeState.cs:197`, `EpisodeValidation.cs:22`, `EpisodeSaveValidation.cs:13`, `EpisodeSaveMigrations.cs:23-24`, and the rest); exact-shape DTOs; `story` appended to `HouseEventKind.All`; the `WebSaveImporter` start week. Lore stays off `ContestantState`. No persona-set change, because story beats never write persona. |
| 4 | Player arcs vs the NPC ledger | Three-path router (§2.6). The NPC↔NPC arc writes (`EpisodeEngine.cs:1689-1690`) and the veto and conversation leaks (`:605-607`; `EpisodeNpcSocial.cs:171-175`) stop behind the boundary. Test: `relationshipArcs` is byte-identical after any NPC-only thread. |
| 5 | Caption contract | Constants: unique within a beat case-insensitively (legacy matching is `OrdinalIgnoreCase`, `:1241`), ≤40 characters, no digits, braces or names. The engine keys on `optionId`. Person picks go in `text` as an id. |
| 6 | Custom houseguests | §3.9, with stated structural exemptions. |
| 7 | Localisation | Ids, variant and cast are persisted; `Localisation.Format`; name-free fallback text for validation and legacy readers; every reader of log text converted to `LogText.Render` (§2.11), with a test that forbids direct reads. |
| 8 | Knowledge boundary | Odds use visible inputs only, and a hidden facet changes only the payoff. Receipts are player-party only. Facts are shown only to knowers. Final-speech fragments name only cycles the player was party to or knows about. Grudge severities and NPC scores are never displayed. The Pull names nobody until you arrive. |
| 9 | 3D staging | §5.1, including the widened `FrameHouseEvent` guard and the Reaction fallbacks. |
| 10 | Pacing slot | §5.2: exact before the boundary; one anchor table with the start anchors named. |
| 11 | Removal invariants | §3.5: the window table, removal at the Social close with ≥4 active, the pending lock, and the finale fixes. |
| 12 | Append-only enums | `Expelled` and five `Reaction` values are appended; `story` is appended to `HouseEventKind.All`; `ProgressStoryline` reuses its reserved ordinal. |
| 13 | Presentation never commits | Scenes, Pulls, gatherings, odds and the conduct confirm are presentation. Consequences come only through `ProgressStoryline` or a lapse inside `Advance`. The hot tub and the player's room never feed the simulation (`EpisodeDirector.HouseLife.cs:44`; `EpisodeEngine.cs:1204`). |
| 14 | Real alumni | §3.10, including the player's own alumnus card, the ban on alumni as conduct targets, and the bio audit. |

### 7.2 Other traps fixed behind the boundary

- **`Roles` casts only Active houseguests.** Today the stale `{NOMINEE}` or `{HOH}` can be cast (`HouseEvents.cs:239`).
- **Orphaned chapters resolve on abandon** (`Storylines.cs:351-362`).
- **"Back Against the Wall" moves to `noms-set`.**
- **`Storylines.cs:21-24`'s comment** is corrected.
- **New terms in the parity evaluators** (`WebEvictionVoting`, `ThreatAssessment`, `WebJuryVoting`, the nomination and veto sorts) are labelled native in code and reproduce today's output on an empty story state. For the additive terms that means they evaluate to 0. For the knowledge gate, a filter, it means a fact-less alliance counts as public (§2.9).
- **NPC-owned modifiers** are filtered out of the player's sums (`Storylines.cs:318-329`).
- **The nominated grudge is written once** (§2.7).

### 7.3 The v14 bundle

It contains:
- everything in §2.3: the shapes M1-M3 use, plus the M4-M6 records as small generic lists;
- `Expelled`, and `story` (with HOUSE-LIFE's `activity`) appended to `HouseEventKind.All`;
- HOUSE-LIFE-PLAN's queue: the narration room list, Have-Nots, the showmance and emergency-meeting templates (as arcs), moment storage and the `activity` kind, and per-week counters, which here are `contacts.weekCount`;
- validation caps for every new list;
- the Clone deep copies;
- the migration of v12 storylines and modifiers (§2.3).

It deliberately does **not** change the persona set, `EpisodeEvent`'s shape or `ContestantState`. A shape change discovered while building M4-M6 is a v15, accepted in advance.

### 7.4 Content pipeline

- **Where content lives.** Catalogues are C# data in the Simulation assembly, append-only, each with an `origin`. Text lives in `StoryText` templates keyed `arc.beat.option`, with 3 week-variants.
- **Catalogue lint** (dotnet `Tools/SimulationTests`) checks that:
  - every role binds;
  - every `next` resolves;
  - labels are constants;
  - every Scene beat has a lapse;
  - every effect kind has a registered consumer (a "no bonus nothing reads" test);
  - every template cites an origin, and every exemption a reason;
  - there is no Survivor vocabulary (island, castaway, Tribal, Idol);
  - no Legacy sheet holds a Personal or Romance fact;
  - every authored sheet's `respects` is tagged with its card's primary trait, and its card fields match the card;
  - every generated memory scores its intended sign in `WebEvictionVoting.Memory`.
- **Bio audit.** Before M4, every alumni bio line that an arc can cite is checked against a cited source, starting with Danielle Reyes (`CastTemplates.cs:212`) and Janelle Pierzina (`:200`), and Rachel Reilly's line (`:236`) is rewritten.
- **Writing load for v1:** about 12 authored lore sheets (the 7 default NPCs first; two are written in §4.4), 12 legacy sheets and 17 trait pools; about 20 arcs × about 3 beats × 3-4 options × 3 variants. That is days of writing, not engineering. Budget it as the Phase 2 writing pass (MASTER-PLAN Part 5).

### 7.5 Test strategy

| Layer | Tests |
|---|---|
| dotnet `Tools/SimulationTests` (seconds; for mutation loops) | `StoryOdds` equals `calculateSuccessChance` on the shared terms when the native terms are zero and the relationship term is inside the web's range (a derived-parity test, not a parity claim); `GrudgeMath` parity with `grudge-system.ts` (stacking, the Active-alliance ×1.2, decay, forgiveness); axes coverage for all 17 traits; the router never calls `Roll` and never writes an arc for an NPC pair; per-command `randomState` invariance, including every option with an alliance effect; catalogue lint; lapse determinism; `StoryAirtime` caps; the nominated grudge written exactly once for a veto replacement |
| EditMode | `PersistenceV14MigrationTests` over the v2→v14 chain, including migrated v12 storylines and modifiers; `FrozenEpisodeV13`; a pre-boundary fixture byte-identical; vote, jury, threat, nomination and veto parity fixtures unchanged on empty story state; a rejected command leaves state unchanged (the Clone test); the removal sweep at every legal window of casts 7, 8, 9 and 12, playing to Finished, for both an NPC and the player; the 6 never offers a removal; `CareerLedger` placement with an expelled houseguest; no direct reads of `EpisodeEvent.text` outside `LogText` |
| Season sweeps (1,920 seeds) | The §5.2 rate targets; the pariah check; the "their word" behaviour change in NPC nominations; every template castable in all-custom and 6-person seasons, less its declared exemptions |
| PlayMode | The Pull toggles without `Render()`; the keyboard walk extended to a Pull, a scene card, a conversation beat, a Summons and the conduct confirm; copy-fit at both text sizes; reduced motion (the episode fixture defaults to it, so the motion half turns it off); captures on their own screen; caption equals label; the scene card gets its event shot; **list non-Passed cases**, because an `Assume` that stops holding reports Inconclusive |

---

## 8. Roadmap (in risk order)

Every milestone uses only what it or an earlier milestone ships. Where a template's later half depends on a later milestone, its rules version switches that half on (the "M" column in §4.1).

| M | Ships | Rules | Gate |
|---|---|---|---|
| **M0 · Say what exists** | The STORYLINES block with existing storylines and **modifiers with weeks left**; the "Answer it" Pull to the station; stakes chips from stored impacts; the category eyebrow and lane glyphs; removal of the unread "Carrying a +N social bonus" line (`EpisodeDirector.cs:1269-1270`); the jury-count caption fix (`:1285`) | Schema 13, no simulation change | PlayMode tracker; Pull without render. **Can ship now.** |
| **M1 · Bundle and spine** | Schema 14 with the §7.3 shapes; `ProgressStoryline`; `StoryRandom`/`ChangeKeyed`; the `FormAllianceWith` refactor; the router with `RecordOneWay`; receipts and their profile list; lapses; `StoryAirtime`; the §7.2 fixes; legacy producers on the story stream; `LogText` and every reader converted; the personality axes, Volatility and `StoryOdds` with Reception from traits; `Expelled` plus the finale and career fixes (inert); the diary's social bonus as a real one-week modifier. Content: `the-confession`, `after-the-comp`, on the station card. | v1 | Migration and parity suites; the owner's go-ahead past §3.G |
| **M2 · The house remembers** | `GrudgeState` writers; `NominationPreference` and `SavePreference` (grudge and "their word" terms); the vote grudge factor; the jury story term's grudge half; `Confront` targeting; `WouldPropose` block; Between-you on ballots. Content: `kitchen-blowup` with the Cold Shoulder variant (no strikes; hot-button terms inert until v4), `bad-blood` (no nemesis until v5), `the-flip` (deal-based; no favour option until v5), `the-house-turns`, `the-conspiracy`, the remaining branching stories. | v2 | Pariah and rate sweep; sign-off on reversing "no ledger"; the "their word" sweep |
| **M3 · Staging and reach** | Scene/Pull/Moment/Fallout; `BeginSceneStage`; the `FrameHouseEvent` guard; Reactions with fallbacks; ceremony kinds; the conversation beat section; Summons; the episode recap and holding screen; the proximity auto-submit retired; the NPC backdoor plan. Content: `hoh-room` (fact-free), `the-accounting` (no public fact or ride-or-die until v5), `the-backdoor`; Ringer: renomination speech, goodbye messages, HoH room reveal, prank war. | v3 | PlayMode suites |
| **M4 · Getting to know them** | Lore sheets (authored, legacy, derived); contacts and rapport; the reveal ladder; lore overrides in Reception and the hidden-facet payoff; goal predicates and their beats; the profile lists; the jury-question hint; the bio audit. Content: `first-night`, `photo-on-the-nightstand`, `the-target-on-their-back`; `hoh-room`'s home fact and `kitchen-blowup`'s hot-button terms switch on. | v4 | Alumni build test; knowledge-boundary tests; bio audit signed off |
| **M5 · Bonds, showmances and secrets** | Bonds; ride-or-die/Final-Two; the jury story term's bond half; the knowledge store with the one-hop mill; knowledge-gated `AllianceThreat` with the legacy public rule; hooks; stress readers and comfort relief. Content: `late-nights` (with B4), `behind-closed-doors`, `loose-lips`, `emergency-meeting`, `what-they-left-out`, `ride-or-die`, `call-in-the-favour`, `staged-feud`; `the-flip`'s favour option, `bad-blood`'s nemesis and `the-accounting`'s v5 terms switch on. | v5 | Showmance-rate and blindside sweeps |
| **M6 · Production** | The conduct ladder with the two-press confirm; Have-Nots, `slop-week` and veto punishments; `CompetitionPlayers` sit-outs; `Expelled` live with the pending lock; `on-notice`; `diary-room-calls`; `breaking-point`; `kitchen-blowup`'s strikes; takeovers, memory wall, report | v6 | Owner sign-off; expulsion-rate cap; the play-to-Finished sweep at every legal window |

**If the owner wants the showmance sooner,** M4 and M5 can swap. `late-nights` then casts on Sociable ≥1 and `contacts` alone (contacts move with it), and gains its romance-open lore gate when lore lands.

---

## 9. Decisions the owner must make

| # | Decision | Recommended default |
|---|---|---|
| 1 | MASTER-PLAN §3.G and :604 put depth after the slice | Ship M0 now. Freeze the M1-M3 shape now, since HOUSE-LIFE's bundle needs one anyway. Start M1 after the slice gate, or now if this request re-prioritises. |
| 2 | Reverse the "no ledger" stance (SESSION-HANDOFF:913) with a directional `GrudgeState` | Yes. It is the engine of "people fight because of nominations". Weekly-settle alliance endings still write no grudge. |
| 3 | Native terms inside the web-parity evaluators | Yes: labelled in code, reproducing today's output on an empty store (fact-less alliances count as public), parity fixtures unchanged. |
| 4 | A personality system (MASTER-PLAN:63) | Story-scoped axes over all 17 traits; NPC verb repertoires untouched. |
| 5 | Have-Nots (MASTER-PLAN:442 vs HOUSE-LIFE:356) | In, from ≥5 active, plus veto punishments. Amend :442. |
| 6 | Can the player be expelled? | Yes, after three labelled, confirmed choices, in the legal windows of §3.5. The 6-person house never removes anyone. A per-season Production setting (Standard / Lenient) is persisted in the bundle. |
| 7 | Do the expelled sit on the jury? America's-vote substitute? | No seat, as a design choice (the BB11 precedent is unverified and no longer cited). No substitute: the cast-order tie rule already settles an even jury. |
| 8 | Penalty tier | Private warning → Have-Not week plus sitting out the next HoH (skipped at ≤4 active). Penalty nomination off (native option). |
| 9 | Public "On Notice" tag | No. Warnings are private; NPC warnings reach the player only as witnessed or whispered facts. |
| 10 | Real alumni | Game register only, for NPCs and for the player's own alumnus card (romance, conduct, secret and removal arcs need a renamed card). Alumni are never the target of a conduct option. Audit every bio before an arc cites it; rewrite Rachel Reilly's line. |
| 11 | Romance involving the player | On, with a per-season `romanceStorylines` switch; fictional partners only. |
| 12 | Hide the card during play? | No. Keep the move-in card public and hide only lore facets; the hidden-job trope lives on as a cover-story secret. Both traits stay visible. |
| 13 | Stress relief (HOUSE-LIFE:368) | Only through beats (Diary Room venting, comfort bonds) plus the week-turn relaxation. Never through activities. |
| 14 | Three lanes vs the web's two storylines | Three lanes plus Production. |
| 15 | Existing deviations on record | Keep `trustChange` → ledger. Keep modifiers read. Replace storyline repeats with no repeat per (template, subject). Flat 25% → the web's ramp. |
| 16 | `jurySentiment` (and the diary's `juryDelta`) | Keep display-only and relabel it "impression". Never pay into it. Promoting it to a jury input would be its own rules change. |
| 17 | What is a season? (Track 6) | Pace everything per active count, as §1.4 does. Design for 6-8 seats. |
| 18 | The full multi-hop rumour mill | Deferred until receipts ship and the rate targets hold. |
| 19 | A native jury story term (`JuryStory`, −15..+12) | Yes. Without it a juror's grudge or a kept showmance is under a quarter of one final impression. The alternative is to leave the web's four terms alone and call the payoffs flavour. |
| 20 | Pure All-Stars seasons | Accept the thinner game-register slate (§3.10). Optionally commission a fictional "Returning Players" roster, which gets the full slate and makes `vets-vs-newbies` reachable. |
| 21 | NPC HoHs honour their own safety promises ("their word" in `NominationPreference`) | Yes, behind v2, measured by the sweep. Today they ignore them until the nomination breaks them. |
| 22 | The NPC HoH's backdoor plan | Yes, behind v3, deterministic, from ≥6 active. It is the most recognisable scheme on the show and today exists only for a player HoH. |

---

## 10. Appendix: how the panel scored and what was grafted

### 10.1 Scores and rankings

| Proposal | Experience | Engineering | Skeptic | Mean |
|---|---|---|---|---|
| F · player experience | 7.5 | 8 | 8 | **7.8** |
| A · story cycles | 7 | 7 | 7 | 7.0 |
| C · the show | 8 | 7 | 6 | 7.0 |
| E · port first | 6 | 7 | 7 | 6.7 |
| B · know your houseguests | 7 | 6 | 6.5 | 6.5 |
| D · emergent house | 7.5 | 6 | 5.5 | 6.3 |

- **Experience** ranked C, D, F, A, B, E, with the spine "C's dramaturgy on D's state".
- **Engineering** ranked F, E, A, C, D, B, with the spine "E's chassis plus F's layer".
- **Skeptic** ranked F, A, E, B, C, D, with the spine "F on E's chassis".

### 10.2 How the disagreements were settled

- **Spine.** F's build order, staging and data model sit on E's command and effect chassis, and C supplies the dramaturgy: anchors, production, recap. This satisfies all three: the experience judge's C is the content layer, and the engineering and skeptic judges' F and E are the structure.
- **D's state layer.** The experience judge wanted all of it. Engineering wanted the rumour mill deferred, and the skeptic feared it would be illegible. Resolution:
  - `GrudgeState` ships first (M2);
  - knowledge ships with a one-hop keyed mill (M5);
  - multi-hop is deferred;
  - receipts precede all of it (M1).
- **Public "On Notice".** It is out. The experience judge's "gamey" verdict wins over C's own design.
- **Penalty nomination.** F made it the default. All three judges flagged it as unverified for the US show, so it is now off and labelled native.
- **Removal timing.** C proposed eviction night, D the Social close, A week start. The engineering judge proved week-start and eviction-night soft-lock on the diary, so removal happens at the Social close, after the diary check. The critic then showed the combined cadence rules left no legal window in either shipped cast, so strikes now start at week 1's eviction night and the floor is ≥4 active.
- **America's vote.** The experience judge worried the jury would go even. It does in odd casts after an expulsion, and the existing cast-order tie rule settles it, so no substitute is needed.

### 10.3 Grafts by source

| From | What this document takes |
|---|---|
| **A** | The closed Cond/Weight grammar with a Nothing entry; the catalogue lint; `StoryOdds` as one pure function; `CompetitionPlayers` as the penalty site; knowledge-gated `AllianceThreat`; the letter-from-home gate; the veto dilemma with a partner who might leave you up; the `ResolveJury` non-voter fix |
| **B** | Three lore sources and the fact snapshot; `sourceTemplateId ?? id` with an identity check; rapport; Resonates/Grates; a backfire teaches the fact; the Unknown chip; goal predicates (now taken from each card's motive); the jury-question hint; the alumni build test; mundane flashpoint topics |
| **C** | The six anchors; production as a character, with clean weeks clearing a strike; the episode recap and holding screen; `on-notice`, `the-flip`, `slop-week` and the Have-Not consumers; Final-Two as `PromiseKind.FinalTwo`; vets vs newbies (parked); the eyebrow categories |
| **D** | The directional grudge store and its numbers; `NominationPreference`; knowers and visibility; the whisper rule; `the-accounting`, `behind-closed-doors`, `loose-lips`, `the-house-turns`; rate targets; `ownerId` on modifiers; `rulesVersion` staging; legacy producers on the keyed stream |
| **E** | `ProgressStoryline` keyed like `ReflectDiary`; typed effects stored at draw; the `origin` field; the Survivor-word ban; the memory-keyword trap; `after-the-comp`; the crisis ramp; the diary social bonus made real; the honest RNG scope |
| **F** | Scene → Pull → Moment → Fallout; receipts and Between-you on ballots; tile anatomy and Show the odds; muted locked options; lanes, airtime and lapse-on-Advance; the pacing math; M0; the HoH-room consumer timing; one-way cooling on exposure; the Confession's real offerer |
| **Critic's pass** | The removal-window table; `SavePreference`; "their word"; the deal-based flip; `FormAllianceWith`; `JuryStory`; the Cold Shoulder; `the-backdoor`, `staged-feud` and the Ringer; motive-derived goals; two written lore sheets; the legacy public-alliance rule |

### 10.4 Rejected, and why

| Rejected | Why |
|---|---|
| A's parallel `storyWorld` cycles | The brief asks for extension, and `StorylineState` already has chapters, cooldowns and validation |
| A's Power Couple +1 action | On the show, a couple is a target, not a power bloc |
| Every payment into `jurySentiment` (C, F, B) | No vote reads it |
| Triggers on `HoldsAGrudge`, "relationship-building ledger records" or "permanent negative marks" (A2, A6, C1, C7, E A4, F A6) | Nothing writes them |
| Weights from the hot-tub companion or the player's room (A1, D; HOUSE-LIFE-PLAN:351's trigger) | Presentation state; not in the save |
| D's rolled **Push back** | A surprise roll on the removal ladder |
| Persona writes from story beats (C's "Aggressive" producer, A4's Ruthless/Remorseful) | Validation allows five personas and ties each to a resolved post-eviction diary week (`EpisodeSocialHistoryValidation.cs:11-35`); `Persona()` reads only the player, and scores Remorseful at 0 |
| Vote promises as the flip's payoff | `DealObligation` reads promises by pair only; a voter's promise to a non-nominee moves nothing (`WebEvictionVoting.cs:22-25,263-274`) |
| B's 144-fact ladder at rapport 14 | Unreachable in about 16 actions. Trimmed and re-priced. |
| E's hidden jobs as a hidden card field | The card is the move-in package the cast screen already showed, and hiding fields breaks the profile and imports. The trope survives as a cover-story secret. (The earlier reason, "houseguests share their jobs on day one", was false.) |
| A second first-impression pass (the earlier `first-impressions`) | The meet-and-greet already writes +3 to everyone (`EpisodeEngine.cs:1023-1031`); `first-night` rides on it with one pick |
| C's and B's behaviour fixes in a no-schema M0 | They change committed impacts, so they go behind the boundary |
| CK3 breach rates for alliances | Too slow for a 5-6-week season |
| Any design sized for 16 houseguests or 12-14 weeks | The port seats 3-12 and defaults to 8 |

### 10.5 Factual corrections carried into this document

Only `promise-made` (`NpcPromises.cs:154`) and `alliance-formed` (`NpcAlliances.cs:181`) are ever written as permanent types, so no negative permanent type exists in play. `Record` and `Move` are both symmetric. `WebFinalSpeechCatalog.cs` has 38 lines, with the fragments at `:18-25`. `NpcAlliances.Form` is private and makes pairs only. There is no `RealPerson` field (`CastTemplates.cs:60-62`). The `ContentCatalog` five carry no `sourceTemplateId` or bio. "Spend time together" is not a command; the verb is RelationshipBuilding. `jurySentiment` has two readers, the Diary Room display and `CareerLedger`, and neither is a vote. The default eight's NPCs are Alex, Emma, Jordan, Casey, Riley, Jamie and Quinn; Taylor Kim is not among them, and only Quinn is Confrontational.

Added in the revision: `HashSeed` returns a `uint`, not a unit double (`SeededRandom.cs:47`). The player's `FormAlliance` rolls (`EpisodeEngine.cs:744`). `NpcVetoSave` (`:554`) is the veto holder's save order, not a nomination sort. The Advance that nominates stays in Nomination (`:187-191,193`). `ResolveVeto` re-runs `NominationEffects` for the replacement (`:604`). The web's allied ×1.2 applies only to Active alliances, and its voted-against grudge belongs to the person voted against (`grudge-system.ts:139-151`). The promise witnesses write no ledger entry (`EpisodeEngine.cs:1652`). `HouseEventKind.All` is at `HouseEventState.cs:121`. `FrameHouseEvent` returns unless `phaseOpen` (`EpisodeDirector.Camera.cs:97`). "Four jurors choose the winner." is hard-coded (`EpisodeDirector.cs:1285`). Jamie Roberts is a working nurse on her card (`CastTemplates.cs:136`; `ContentCatalog.cs:137`). Quinn Martinez is Confrontational/Social, and nobody in the default eight is Stubborn. Riley's Volatility tops out at 2.

---

## Revision notes

- **DQ made reachable.** Strikes may start at week 1's eviction night; strike 3 needs a post-eviction Social beat with ≥4 active (was ≥5, with no story before week 2). Added the removal-window table for casts of 6, 7, 8, 9 and 12; the default 8 now has weeks 3-4, the shipped 6 is declared exempt with its ladder topping out at repeated penalties. Restated the expulsion-rate target against casts that have a window. Corrected jury parity for odd casts.
- **Conduct confirm defined.** A two-press **Do it** / **Back** on the tile itself, on any surface, replacing the reference to the diary's `ReviewStudyHouse`-specific draft flow; added the pending-removal lock on the player's Social verbs.
- **Finale fixes extended.** Added the hard-coded "Four jurors" caption (moved to M0) and `JuryExchangeCount`/`PrepareJuryQuestion` for an expelled player; `CareerLedger` merges `removals` into exit order.
- **Persona writes removed.** Story beats never write persona (validation limits and diary-week coupling cited); Push back, Take it on the chin and Blame the house now pay in receipts, grudges and clean-week clocks; the NPC blow-up pays in receipts; the §6 persona row now says "never"; C's "Aggressive" graft deleted.
- **The flip reaches the ballot.** It now creates a `vote_evict` or `vote_save` deal, which `DealObligation` reads by target; "certain PromiseVote" deleted; the favour option moved to M5.
- **Alliance effect is roll-free.** Specified the `FormAllianceWith(nextRoll)` refactor of `EpisodeEngine.cs:740-744` as M1 engineering; `FormFromStory` for 2-4 NPC members.
- **`StoryRandom.Unit` corrected** to wrap `HashSeed` in a `SeededRandom` and call `NextDouble()`.
- **Nomination and veto sorts split.** `NominationPreference` covers `:190`, `:206` and `:602` only, with every term numbered and gated, and "leverage" defined as "their word" (safety promise or `safety_agreement`, +30). New `SavePreference` for `NpcVetoSave` (`:554`), with `veto_use` deals.
- **`hoh-room` lapse timing fixed** to the Advance that commits NPC nominations, before `Nominate`.
- **Grudge writers made web-faithful.** Nominated 70 with ×1.2 only while an Active alliance holds; alliance-betrayed 80 as its own cause; voted-against held by the person voted against; the replacement's double write guarded by `initial`; native rows labelled.
- **Knowledge gate has a legacy rule.** A fact-less alliance counts as public; "evaluates to 0 on empty state" reworded to "reproduces today's output".
- **Odds labelled "derived from" the web,** with every changed or dropped term listed; the web's trait +15 and nominated +10 restored, Reception reduced to ±10; a hidden hot-button now changes only the payoff, so the odds shown are the odds used.
- **Characterization examples corrected.** Added a default-eight Volatility table (Riley tops out at 2; Jamie is the hothead example); Quinn is Confrontational, not Stubborn; the Cold Shoulder variant gives low-Volatility houseguests in-character fights.
- **Lore written.** Goal predicates derived from every default NPC's authored motive; full sheets for Jamie Roberts and Quinn Martinez; a cover-story facet (Casey) replacing the false "jobs on day one" rationale; A8's recap no longer contradicts Jamie's nursing card; a jury-hint consistency rule tied to `traits[0]`.
- **Real-alumni rules closed.** The player's own alumnus card falls under §3.10 unless renamed; alumni are never conduct targets; a bio audit (Danielle Reyes, Janelle Pierzina, Rachel Reilly) gates M4; a paragraph on what pure All-Stars seasons lose, with an owner option for fictional veterans. Decision 10 rewritten.
- **New Big Brother content.** Added `the-backdoor` (player and native NPC plan) with the showmance follow-on in `late-nights` B4; `staged-feud` built on Quinn's card; the Ringer table (renomination speech, goodbye messages both ways, HoH room reveal, prank war, veto punishment); `loose-lips` sharpened into "your final two got out"; `legend-talk` replaced by `the-target-on-their-back`; `vets-vs-newbies` parked with the reason.
- **Jury payoffs made visible.** Stated the effective jury, trust and vote weights; proposed the labelled native `JuryStory` term (decision 19).
- **`first-impressions` rebuilt** as the single `first-night` card on the existing meet-and-greet, outside the weekly count.
- **Pacing reconciled.** An anchor table naming where new arcs may start; at most 2 asks a week at any anchor; ask ceilings recomputed (9 asks and 5 summons in the default 8); `the-house-turns` excludes the reigning HoH and fires at eviction eve; the pariah metric counts distinct holders.
- **Have-Nots recur.** Selection from ≥5 active (1, 2 or 3 Have-Nots by house size), plus veto punishments; `slop-week` now castable in the shipped 6.
- **Readers of story logs converted.** Name-free fallback text plus a `logSequence` on the cycle step; one `LogText.Render` helper for `WeeklyRecap.Moments`/`Subject`, `SeasonReport` and the other readers, with a test that forbids direct reads.
- **Schema details.** `story` appended to `HouseEventKind.All`; the relationship between `storyRulesStartWeek` and `story.rulesStartWeek` stated; v12 storylines and modifiers migration specified; the freeze narrowed to M1-M3 shapes with a v15 accepted in advance.
- **Staging gaps.** `FrameHouseEvent`'s `phaseOpen` guard widened; clip budget and silent fallbacks for the five new Reactions; `sceneCardOpen` acknowledged as a new modal state rather than "no new panel".
- **Mid-arc departures.** New §3.11 for evicted headliners and an evicted player; the final-speech fragments restricted to cycles the player knows.
- **Castability sweep exemptions** declared with reasons; the lint requires them.
- **Roadmap re-ordered** so no milestone depends on a later one: axes, receipts and `LogText` in M1; `kitchen-blowup` moved to M2; `hoh-room` fact-free in M3; `emergency-meeting` and `what-they-left-out` moved to M5; the-accounting's and bad-blood's later halves gated by rules version; an M column added to the slate.
- **Plans argued with explicitly.** Overrode HOUSE-LIFE-PLAN:351's hot-tub trigger with the reason; cited HOUSE-LIFE-PLAN:705 as the precedent for the `story` kind.
- **Unverified precedent withdrawn.** Chima Simone is no longer cited for "no jury seat"; the expelled get none as a design choice.
- **Counts corrected.** The ask totals, "Four jurors", and the claim that an expulsion always makes the jury odd.

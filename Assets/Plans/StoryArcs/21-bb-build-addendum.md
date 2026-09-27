# 21 · What the Big Brother build adds to the story-arc plan

**What this is.** An addendum to `20-story-arcs-brainstorm.md` (the plan) and `00-gap-map.md`. Both were
built from `D:/gamesim-main`, which turned out to be the **Survivor** reskin. This pass reads the actual
Big Brother web game, `D:/gamesim-web` (GitHub `Quantumdemon1/gamesim`, `main` at `c2f79c9`, confirmed
current against `origin/main` on 2026-09-26), read-only, and says what changes.

**Conventions.** Web paths are relative to `D:/gamesim-web/`. Unity paths are relative to
`Assets/Gamesim/`. "Lands" means the write reaches the reducer state that votes, nominations and AI read,
not only the legacy `RelationshipSystem`, a toast, or `sessionStorage`. Every claim here was read at the
cited line unless marked **not re-traced**.

---

## 0. The short version

1. **Same event code, Big Brother words.** Every event and story system the plan cites exists in the
   Big Brother build with the **same line count**, so its line citations carry over by swapping
   `D:/gamesim-main/gamesim-main/` for `D:/gamesim-web/`. Two exceptions need re-checking:
   `src/components/game-hud/GameHUD.tsx` (2476 lines, not 2402) and
   `src/hooks/downstream-reactions/grudge-reactions.ts` (71, not 70). The differences are wording
   ("the house", "Double Eviction", "Battle Back") plus a few Big Brother-only additions (§4).
2. **The social week runs.** Eviction → Social → next week is live
   (`src/contexts/reducers/reducers/player-action-reducer.ts:415-431`, with the comment "BB USA Format:
   Social Interaction happens AFTER Eviction" at `game-progress-reducer.ts:87-95`). The plan's "the
   social phase can't be reached" was a Survivor-tree fact.
3. **Most event consequences still don't land**, but more land than the plan assumed. Landing:
   dialogue trees, pull-aside scenes, emergent events (relationships **and** grudges), storyline progress
   and story-made alliances, the nomination/veto consequence fold, and the week-end grudge detector.
   Not landing: phase beats', confrontations', room actions' and storylines' relationship writes, and
   any deal a story creates (§2).
4. **Correction: in this build a nomination alone never creates a grudge.** The live nomination path
   writes mood −2, stress +2 and a −8 arc, and records no typed "nominated" event, so the week-end
   detector's `nominated: 70` and `voted_against: 40` rows never fire. Live grudges come from veto
   replacements, relationship drops of 8 or more, and broken oaths, deals and alliances (§3, C3).
5. **The web's own beat trigger is the conversation, not only the phase.** Talking to someone in an
   open storyline always plays its next chapter. A broken deal, promise or oath raises a **betrayal
   flag** that guarantees a storyline the next time you talk to that person. Otherwise a new storyline
   starts at a chance set by topic (10-40%) and relationship strength, capped at 65% (§4, G1-G2). Unity
   already records the same five topics as commands.
6. **NPCs come to a conversation with an agenda.** `inferIntent` gives each NPC one of eight intents
   (test loyalty, gather intel while hiding resentment, assess you as a nomination threat, campaign
   subtly...) with a hidden goal and a reveal chance. It is pure and deterministic, so it ports as-is
   (G3).
7. **Rooms carry meaning.** The living room is public (callouts doubled and witnessed), the bedrooms are
   intimate (Pillow Talk), the backyard is low-suspicion, the HoH suite is invite-only ("others noticed
   who you picked"), the Diary Room takes one person, and the nomination room hosts the final plea. Time
   of day moves people between them. This is the web anchor the plan's staging lacked, and it gives the
   showmance a better trigger than the hot tub (G6).
8. **The plan's anchors are the web's design.** Nine authored beats fire at phase transitions with
   authored chances (After the Competition 70%, Tensions Rise 60%, The Veto Holder's Dilemma 50%, Final
   Pleas 60%...). They map almost one-for-one onto the plan's six anchors (G7).
9. **NPC-initiated confrontations exist, with menus and numbers, but don't land.** "You found out they
   gossiped about you", "We need to talk" and the nominee's campaign each offer three responses with
   fixed deltas (G5). Unity can make them land.
10. **The All-Stars profiles are the natural source for the plan's game-only "legacy" sheets**, after an
    audit. Some lines must stay out under the plan's real-alumni rule (G8).

---

## 1. What was read

- The Big Brother build's own docs: `docs/ACCEPTED_CONSEQUENCES.md`, `docs/SOCIAL_CALCULATIONS.md`,
  `docs/LIVE_NOMINATION_VETO.md`.
- The live nomination/veto consequence layer: `src/systems/accepted-consequences/` (`index.ts`,
  `nomination-direct.ts`, `social-reactions.ts`), `src/systems/relationship-change.ts`.
- Grudges: `src/systems/grudge-system.ts` and every importer (nomination AI, veto AI, vote round, blocs,
  relationship evolution, emergent events, the dialogue dialog).
- The social phase: `src/components/game-phases/social-interaction/SocialInteractionPhase.tsx`,
  `sections/ConversationsSection.tsx`, `src/components/dialogs/DialogueTreeDialog.tsx`,
  `src/components/deals/DealsManager.tsx`.
- Triggers: `src/utils/storyline-trigger-utils.ts`, `src/utils/storyline-game-adapter.ts`,
  `src/contexts/BetrayalFlagContext.tsx`.
- Content: `src/systems/dialogue-tree-engine.ts`, `src/systems/social-cutscene-system.ts`,
  `src/systems/phase-event-system.ts`, `src/systems/emergent-event-system.ts`,
  `src/systems/ai/npc-social-behavior.ts`, `src/data/character-templates.ts`,
  `src/data/allstars-templates.ts`.
- The room layer (Big Brother build only): `src/systems/rooms/*`, `src/hooks/useRoomActionListener.ts`,
  `src/hooks/useNpcRoomBehavior.ts`, `src/hooks/useRoomEntryEvents.ts`.
- Diffs against the Survivor tree for every event and story system, to separate wording from content.

---

## 2. What runs, and what lands

| Feature | Runs? | Lands? | What lands, and what doesn't | Evidence |
|---|---|---|---|---|
| **Nomination and veto consequences** | Yes | **Yes** | Nominee mood −2 and stress +2 on an initial nomination; −8 arc when the player is HoH or nominee; oath-break witnesses (ally −15, rival +3, neutral −5..−10); veto: saved↔holder +25, replacement↔HoH −20, replacement↔holder −15; deal outcomes; then a grudge → jury → memory/gossip → alliance fold after every fact | `src/systems/accepted-consequences/nomination-direct.ts:48,115,156`; `index.ts:175-185`; `social-reactions.ts:84-207`; `docs/LIVE_NOMINATION_VETO.md` |
| **Week-end relationship evolution** | Yes, chained after the AI weekly summaries (**not re-traced** offline) | **Yes** | Grudge decay, `detectBetrayals`, NPC alliances and invitations, saved with `SET_GRUDGE_LEDGER` and `ADD_NPC_ALLIANCE`. `detectBetrayals` only reads typed relationship events, and no live code records a typed `nominated` or `voted_against` event, so those rows never fire | `GameHUD.tsx:500-535`; `grudge-system.ts:22-29` |
| **Dialogue trees** (five topics) | Yes | **Partly** | The summed relationship change (`UPDATE_RELATIONSHIPS`) and each choice's memory land. Reputation and the social bonus are only shown in a toast | `ConversationsSection.tsx:143-187`; `dialogue-tree-engine.ts:69-178,365` |
| **Conversation → storyline** | Yes | **Partly** | New storylines, chapter progress, modifiers, completion ids and story-made alliances land (`ADD_STORYLINE`, `UPDATE_STORYLINE`, `ADD_NPC_ALLIANCE`). The storyline's own relationship, deal and memory writes are no-ops in the adapter | `storyline-trigger-utils.ts:8-81`; `storyline-game-adapter.ts:50-75`; `SocialInteractionPhase.tsx:627-660` |
| **Betrayal flags** | Yes, session only (React state, cleared on load) | n/a | Guarantee a storyline on the next conversation with that NPC | `contexts/BetrayalFlagContext.tsx:31,70` |
| **Pull-aside scenes** (12, from the Deals screen's moves) | Yes | **Partly** | Relationships and memories land. The vote deals they create (`campaign-plea` → `vote_save`, `persuade-vote` → `vote_together`) do not, because `ADD_DEAL` has no reducer case. The storyline hand-off is sent on `window` and heard on `document`, so it never arrives | `DealsManager.tsx:113,185,201,229,314-316`; `social-cutscene-system.ts:599-1151` |
| **Emergent events** | Yes (Social phase, plus a re-check after accepted consequences) | **Yes** | Relationships and grudge increases land. Only `pile_on`'s "Joined pile-on" (+40, target → player) writes a grudge. Mediation's −10 is dropped, because the handler only applies positive grudge changes | `GameHUD.tsx:653,1823-1864`; `emergent-event-system.ts:91,207` |
| **Phase-transition beats** (nine) | Yes | **Partly** | Memories, arcs and the social/competition bonus flags land. The relationship change and the reputation ripple go to the legacy store | `phase-event-system.ts:44-370`; `GameHUD.tsx:388,1130-1180` |
| **NPC confrontations and campaigns aimed at the player** | Yes | **No** | Written through `game.relationshipSystem`, which is null, and arcs are assigned onto the game object | `ai/npc-social-behavior.ts:759-890`; `SocialInteractionPhase.tsx:728-756` |
| **Player room actions** (Cook, Public Callout, Pillow Talk, Invite Up...) | Yes | **No** | Written through the legacy `relationshipSystem`. Room vibe, the comp-practice bonus and the HoH invite list live in `sessionStorage` | `hooks/useRoomActionListener.ts:43-195`; `systems/rooms/roomAccess.ts`, `roomVibe.ts` |
| **NPC room life** (cook, pillow talk, callout, alliance meet, HoH chat) | Yes | **No** | Legacy store, on a 30 s real-time cooldown | `systems/rooms/npcRoomBehavior.ts:28,58-160`; `hooks/useNpcRoomBehavior.ts:39` |
| **Ambient lines, 20% interactive** | Yes, every 45 s | Toasts only | — | `GameHUD.tsx:712-740` |
| Branching stories, recurring beats, mid-week crises | **Not re-traced** | — | The memory note lists them among the handlers that write relationships only to the legacy store | memory: `web-event-layer-is-intent-not-behaviour` |

**Grudges have live readers in this build:**
- Voting-bloc loyalty subtracts 0.2 × the voter's grudge against the bloc's target (`src/systems/voting-bloc-system.ts:112-121`).
- The NPC nomination and veto AIs get a grudge context in their prompt (`NominationPhase/hooks/ai-nomination/useAIProcessing.ts:105-111`; `POVMeeting/hooks/ai-decision/useVetoAI.ts:97-103`). The local fallback proposals don't use it.
- The dialogue dialog reads the NPC's grudge against you to pick its agenda (`DialogueTreeDialog.tsx:105-120`).

---

## 3. Corrections to the plan (20) and the gap map (00)

**C1 · The reference tree.** Every `D:/gamesim-main/...` web citation becomes `D:/gamesim-web/...` with
the same line number, except in `GameHUD.tsx` and `grudge-reactions.ts`. The plan's "copy the web" rule
should cite the Big Brother build from now on.

**C2 · "The social phase is unreachable."** False in the Big Brother build. Gap map §0 rows 1-3 (the
unreachable phase, the null `relationshipSystem` everywhere, `showmance_rumor` dead) describe the
Survivor tree. In the Big Brother build the social week runs, and the landing picture is §2's table:
some things land, many don't. The plan's framing ("copy the templates, numbers and triggers; design the
consequences") still holds.

**C3 · Grudge writers (plan §2.7, TL;DR 2).** The plan labels "Nominated 70" and "Voted against by an
ally 40" as web behaviour. In this build they are **web intent that never fires**: the table and its
weekly detector run, but the live nomination and vote paths record no typed event for them to find.
What actually writes grudges live:

| Cause | Grudge | Evidence |
|---|---|---|
| Any accepted relationship drop of 8 or more (veto replacements, deal outcomes) | \|Δ\| × 5, capped at 100; at a drop of 15 or more, half of it back the other way too | `social-reactions.ts:90-97` |
| Veto replacement (−20 with the HoH, −15 with a distinct holder) | 100 and 50 between replacement and HoH; 75 and 38 between replacement and holder | `index.ts:180-184` + the row above |
| Loyalty oath broken by a nomination | 70 × (1 + threat/200) | `social-reactions.ts:99-102` |
| Deal broken | 60 × (1 + threat/200) | same |
| Alliance broken (a member pair at −20 or lower after a deal break or a 15-point drop) | 40 × (1 + threat/200) | `social-reactions.ts:194-207` |
| Joining a pile-on (emergent event) | 40, target → player | `emergent-event-system.ts:207` |
| Stacking, decay | +0.5 × incoming; −2 a week | `social-reactions.ts:37-70` |

**Direction caveat.** The fold treats the second id of a relationship operation as the grudge holder,
and the veto operations list the replacement first. So in this build the **HoH** ends up holding the
100 grudge against the replacement, and the replacement only 50 against the HoH. That reads as an
accident of argument order, since the older `grudge-system.ts` table has the replacement resenting the
HoH. Unity should choose the direction on purpose (decision D-B).

**Knock-on for arcs.** `kitchen-blowup`'s trigger note "(every nomination writes one)" is false under
the web's live rules. It stays true if the owner keeps "nominated 70" as completing the web's intent
(decision D-A).

**C4 · The showmance trigger.** The plan overrode HOUSE-LIFE-PLAN's hot-tub trigger and fell back on
`contacts.weekCount`. The web's own showmance-adjacent act is **Pillow Talk** in the bedrooms:
- the player's version gives +6 × the room's bond modifier, labelled "trust ×1.5"
  (`roomActions.ts:100-110`; `useRoomActionListener.ts:100-106`);
- NPC pairs at 30 or more have "a quiet talk in the bedroom", always at night and 40% of the time
  otherwise, worth +3 (`npcRoomBehavior.ts:107-127`).

That makes it the web anchor for `late-nights`. The catch is that Unity deliberately keeps the player's
room out of the save, so it needs decision D-E.

**C5 · "No event system reads traits"** (gap map §4; reader 03). Partly wrong for the Big Brother build:
- `inferIntent` reads the NPC's primary trait (Confrontational or Competitive → intimidate)
  (`npc-social-behavior.ts:1091-1180`);
- NPC action preferences come from traits (`getTraitPreferences`, same file);
- cooks are picked by trait (`npcRoomBehavior.ts:91-105`).

None of this reads the **player's** traits, and none of it touches event options.

**C6 · "useNPCSocialEngine's confrontation was not traced"** (gap map §8). The social phase's NPC
actions include three player-facing menus with fixed numbers (G5). None of them lands.

**C7 · Story-made deals.** The web intends a campaign plea to produce a `vote_save` deal and a persuasion
to produce `vote_together` (`DealsManager.tsx:314-316`), but `ADD_DEAL` has no reducer case, so neither
exists. The plan's deal-based `the-flip` (A6) is the web's intent made real, and can say so.

---

## 4. What to take from the Big Brother build

### G1 · Conversation as a beat trigger

**Web rule** (`src/utils/storyline-trigger-utils.ts:8-81`):
- An open storyline involving that NPC **always** plays its next chapter when you talk to them.
- A betrayal flag on that NPC **always** starts a storyline (G2).
- Otherwise the base chance is set by topic: small talk 0.10, personal chat 0.20, discuss game 0.30,
  vent 0.35, share a secret 0.40.
- That chance is multiplied by relationship strength either way: ×1.8 at |score| ≥ 60, ×1.4 at ≥ 40,
  ×1.1 at ≥ 20, ×0.6 below.
- Recent history adds to it: +0.25 for a deal with them broken in the last seven days, +0.20 for an
  alliance formed with them this week. The total is capped at 0.65.

**Unity mapping.** The five topics are already recorded commands: `SmallTalk`, `PersonalChat`,
`DiscussGame`, `VentAbout` and `ShareSecret` (`Simulation/EpisodeState.cs`, the social vocabulary block).
- Inside the `Execute` of those five commands, a running cycle's next beat can open when the target
  holds one of its roles. This joins the plan's anchors as a second trigger surface.
- A new cycle may start there on the story stream, keyed `talk:week:command-sequence`, using the web's
  table.
- "This week" replaces the web's real-time seven days.
- Everything counts against `StoryAirtime` (plan §5.2). This is the most direct answer to "interact
  with houseguests to advance arcs", and it is the web's design.

### G2 · The reckoning: persist the betrayal flag

**Web.** A broken deal, promise or loyalty oath between you and an NPC, in either direction, sets a flag
`{dealType, week, betrayerIsPlayer}` (`BetrayalFlagContext.tsx:31-78`). The next conversation with them
is guaranteed to open a betrayal storyline, told from the side of whoever broke it. It is session-only.

**Unity.** Persist it as a small `reckonings` list `{npcId, cause, betrayerIsPlayer, week}` in the v14
bundle. The next recorded conversation with that NPC opens `the-reckoning` (§5), then clears it. This is
CK3's "the memory becomes the next event", and it replaces the plan's reliance on grudge thresholds alone
for `bad-blood`.

### G3 · NPC agendas

**Web** (`npc-social-behavior.ts:21-30,1091-1180`), checked in this order:

| Intent | When | Hidden goal (paraphrased) | Reveal chance |
|---|---|---|---|
| test_loyalty | allied, trust < 45 | find out whether you're about to flip | 0.15 |
| coordinate_strategy | allied, trust ≥ 45 | line up targets together | 0.60 |
| gather_intel | their grudge against you > 50 | fish for information while hiding resentment | 0.10 |
| assess_threat | they're HoH and not allied with you | decide whether to nominate you | 0.20 |
| campaign_subtly | they're nominated and you vote | win your vote without looking desperate | 0.30 |
| probe_deception | they know you lied | see whether you come clean | 0.10 |
| intimidate | score < −20 and primary trait Confrontational or Competitive | make you think twice | 0.40 |
| build_rapport | otherwise | genuine connection | 0.80 |

Trust defaults to 50 + score × 0.5.

**Unity.** Port `inferIntent` as a pure function in the Simulation assembly, as a trigger input that the
text never states. Use it three ways:
- **Pick the beat.** An HoH's `assess_threat` conversation *is* the nomination interview
  (`the-agenda`, §5).
- **Price the options.** Reception reads the intent.
- **Add a "Read them" option** (⟨Intuitive or Analytical⟩), checked on the story stream with the
  reveal chance as its base. Success teaches you the agenda as a fact.

The plan's rule "triggers may read hidden state; text may not" covers it. "probe_deception" needs the
knowledge store (M5); the rest need only today's state.

### G4 · Dialogue trees as the conversation-beat template

**Web** (`dialogue-tree-engine.ts:14-178,205-224`):
- Five authored trees, one per topic: 2-3 stages.
- Each stage has an NPC line, an NPC thought, a mood (friendly, suspicious, grateful, hostile, neutral)
  and up to three choices.
- Each choice carries a risk word (safe, moderate, risky), a relationship change within ±10, a
  reputation change within ±5, a social bonus of 0-2, a memory line and an alliance-opportunity flag.
- Results are summed when the conversation ends.

The share-a-secret tree is a good sample: **I heard something big** (moderate, +5) / **I have a plan**
(risky, +7, rep −3, alliance) / **I need your help** (safe, +4, alliance), then **Promise me silence** /
**We're in this together** (risky, +8).

**Unity.** These are the "Conversation" surface of the plan's `ProgressStoryline`. Unity's five topic
commands currently resolve in one roll (`Simulation/WebSocialVocabulary.cs:26-62`). A tree becomes a beat
whose stages are chained options:
- The risk word maps to the plan's risk tag.
- `allianceOpportunity` becomes a follow-up Pull to **Make it official** (plan §3.3).
- The memory line is a whitelisted memory (plan §3.4, the memory trap).

**Reputation has no web consumer.** Map it to witness receipts or drop it (decision D-F). Because the
web never applies it, dropping it is faithful.

### G5 · NPC-initiated confrontations and campaigns

**Web** (`npc-social-behavior.ts:759-890`):

| Situation | Opener | Responses (Δ) |
|---|---|---|
| You find out they gossiped about you | "has been talking behind your back to X" | **Confront Them** −15 / **Let It Slide** −5 / **Gossip Back** −10 |
| They confront you | Harsher line below −40 ("I'm done pretending...") | **Apologize** +10 / **Deflect** −2 / **Escalate** −15 |
| A nominee campaigns to you | Veto-holder variant ("use it on me"), close-friend variant, cool variant | **Promise Support** +8 / **Stay Noncommittal** 0 / **Refuse** −5 |

**Unity.** Unity's NPCs already `Confront` (−8, `Simulation/NpcSocialActions.cs:304-319`) and campaign.
These menus are the missing player-facing Moment: an Approach beat, answered through `ProgressStoryline`.
- **Promise Support** writes a `vote_save` deal (the web's intent, C7).
- **Escalate** feeds the plan's heat ladder (§3.4).

This covers "risk negative interactions" with the web's own numbers.

### G6 · Rooms and time of day as staging

| Room (web id) | Web meaning | Player act and number | NPC life | Plan arcs to stage there |
|---|---|---|---|---|
| Living room | Public: "confrontations are doubled and witnessed" | **Public Callout** −10 × damage; each witness −3 if they like the target, +2 if they dislike them, −1 otherwise. **Public Defense** +6; witnesses +1 | Callout at 25% when a pair is at −40 or lower and 3+ are present (−6) | `kitchen-blowup` B2, `emergency-meeting`, `the-accounting`, `staged-feud` |
| Bedrooms | Intimate | **Pillow Talk** +6 ("trust ×1.5") | Pillow talk at night, pair ≥ 30 (+3) | `late-nights` B1, `what-they-left-out` |
| Backyard | "Low suspicion", closed at night | **Alliance Meet** +4; **Comp Practice** +5% next comp (cap 15%) | Allied pairs talk strategy (+2) | `the-confession`, `ride-or-die` |
| Kitchen | Cooking bonds everyone present | **Cook** +2 to everyone in kitchen and living room | Social or outgoing cooks (+1 each) | `slop-week`, the Have-Not pantry, `kitchen-blowup` B1 |
| HoH suite | Invite-only | **Invite Up** +9, "others noticed who you picked"; **Spy Screen** glimpses a conversation (60%) | HoH chats with invitees (+3) | `hoh-room`, `the-invite-list`, `spy-screen` (§5) |
| Diary Room | One at a time | **Confessional** (a stub in the web) | — | Summons (plan §5.1) |
| Nomination room | Open during ceremonies | **Final Plea** +8, "×2 lobbying" | — | `final-plea` (§5) |
| Game room | Casual | **Play a Game** +3; **Decompress** | — | Ringer: prank war |

Sources: `systems/rooms/roomActions.ts:38-190`; `hooks/useRoomActionListener.ts:40-195`;
`systems/rooms/npcRoomBehavior.ts:58-160`.

**Time of day** (`systems/rooms/roomSchedule.ts:12-66`). Each slot is 2 real minutes. Weights:
- morning: kitchen ×2.0, backyard ×1.4;
- afternoon: backyard ×2.0, game room ×1.5;
- evening: living room ×2.0, kitchen ×1.6;
- night: bedrooms ×2.5, HoH suite ×1.5, backyard closed.

**Room adjacency** (`:75-84`) exists "for gossip ripples and eavesdrop".

**Unity.** The weights bias the NPC venue planner and scene staging; that is presentation, and needs no
save. The room *acts* are the question. The web applies them only to the legacy store, and Unity keeps
the player's room out of the simulation (`EpisodeEngine.cs:1204`), so they need decision D-E.

### G7 · The nine phase beats fill the anchors

| Web beat (`phase-event-system.ts`) | Chance | Choices | Plan anchor |
|---|---|---|---|
| After the Competition (post-HoH, `:44-76`) | 0.70 | Congratulate them / Make your pitch / Lay low | hoh-crowned (`after-the-comp`) |
| The Lobby (pre-Nomination, `:226-254`) | 0.50 | Listen to their pitch / Counter-lobby | hoh-crowned |
| Tensions Rise (post-Nomination, `:81-108`) | 0.60 | Listen to their plan / Share your own thoughts | noms-set |
| The Veto Holder's Dilemma (post-PoV, `:151-176`) | 0.50 | Lobby them to use it / Respect their decision | veto-won |
| The Fallout (post-Veto Meeting, `:259-285`) | 0.50 | Offer comfort / Keep your distance | block-set |
| Final Pleas (pre-Vote, `:290-324`) | 0.60 | "You have nothing to worry about." / "I have to vote with the house." / "What are you offering?" | eviction-eve |
| Last-Minute Campaign (pre-Eviction, `:113-146`) | 0.65 | "You have my vote." / "I haven't decided yet." / "I'm sorry, I can't." | eviction-eve |
| The Morning After (week start, `:185-221`) | 0.55 | Check on the house / Rally your allies / Stay in bed | eviction-night |
| House Drama (mid-Social, `:329-370`) | 0.40 | ... / Stay out of it | Social (Pull) |

**Unity.** These become the anchor pools' first entries, with the web's chances drawn on the story
stream. Their consequences get the plan's consumers:
- "You have my vote." writes a `vote_save` deal.
- "What are you offering?" opens a deal proposal.
- Congratulating a houseguest writes a receipt.

The caption contract holds, since every label is a fixed constant.

### G8 · The All-Stars profiles as legacy sheets

`src/data/allstars-templates.ts` gives each alum a ~150-word AI prompt describing their documented public
playstyle. Examples: Dan "gets more calm when cornered"; Derrick "controls conversations by asking
questions"; Janelle calls out floaters and never betrays a ride-or-die; Vanessa "confronts people
aggressively when catches them lying"; Jun "uses cooking as a social tool"; Tyler avoids confrontation.

These are exactly the plan's Legacy-sheet facets: conflict style, what they respect, and voice. They can
also fill the personality axes for alumni, for example Rachel as high Vengeful and Bold. Two cautions:
- **They need the plan's bio audit.** Danielle's profile repeats "an era before the jury could watch
  footage" (`:211`). Janelle's says "three seasons (BB6, BB7, BB22)" (`:173`).
- **Some lines are out under the real-alumni rule.** Rachel's "No one gets between me and my man!"
  (`:287`) and Will's "You flirt shamelessly" (`:135`) are romance-register.

The regular cast is as thin in the web as in Unity: a one-line bio, two traits, an archetype and a
tagline (`data/character-templates.ts:21-36`). The plan's authored lore sheets are still native.

### G9 · The consequence-fold numbers

From `docs/ACCEPTED_CONSEQUENCES.md` and `src/systems/accepted-consequences/`:
- **Deals:** fulfilment 8/12/16/24 and break −15/−22/−30/−45 by deal size.
- **Broken-deal gossip:** reaches up to three random uninvolved active non-players, one memory each (`social-reactions.ts:172-190`).
- **Jury fold:** only when the player is party. Relationship drops pay 0.4× to the named juror and 0.05× to all. A broken oath pays −12 if the player broke it, +6 if they were wronged. A broken deal pays −10/+5, a kept deal +5/+5, anything else −5/+2, each ×1.5 named and ×0.3 global (`:126-170`).
- **Relationship changes:** always move both directions, the second at 0.8-1.2× the first (`relationship-change.ts:90-118`).

**Unity status.** I did not compare these to Unity's `EpisodeEngine` line by line. Much of it, such as
nominee mood and stress or the oath witnesses, is probably already ported. A parity pass is worth one
dotnet test file. Note that the jury fold writes `jurySentiment`, which in Unity no vote reads (plan
§3.1).

**Why this matters beyond the numbers.** The Big Brother build converged on Unity's own command model:
an explicit evidence protocol, no ambient randomness or clock inside a calculation, and a receipt
committed with the state (`docs/LIVE_NOMINATION_VETO.md`). The plan's keyed story stream and
`ProgressStoryline` are the same design, so they are not a deviation from the web's direction.

---

## 5. New and sharpened arcs from the Big Brother content

| id | Fantasy | Trigger | Beats and web numbers | Consumers | Web anchor | M |
|---|---|---|---|---|---|---|
| `the-reckoning` | "We need to talk" | A persisted reckoning (G2) and a conversation with that NPC | They raise it: **Apologize** +10 / **Deflect** −2 / **Escalate** −15 (heat +1). If *you* were betrayed, you raise it: **Call it out**, **Let it go** (−5, and you keep the flag's receipt), **Use it** ⟨Strategic⟩ (a hook) | Grudge, receipts, heat, hooks | `BetrayalFlagContext`, the confront menu | M2 (hook half M5) |
| `the-agenda` | The HoH is feeling you out | An NPC HoH, not allied, with intent `assess_threat`, in a conversation you started | **Read them** ⟨Intuitive or Analytical⟩ (base 0.20, the reveal chance: learn it is a nomination interview). **Talk yourself down**, **Pitch a bigger target**, **Promise your vote** (a `safety_agreement` proposal) | `NominationPreference`, deals | `inferIntent` | M2 |
| `caught-talking` | You find out they talked about you | An NPC's `SpreadInfo` or rumour about you, and you become a knower | **Confront Them** −15 / **Let It Slide** −5 / **Gossip Back** −10 ⟨Sneaky⟩ (their grudge +20) | Grudge, receipts, facts | the gossip-discovered menu | M2 (knower part M5) |
| `final-plea` | The nominee's last chance | You are nominated, eviction eve, in the nomination room or a conversation | **Make your case** (the web's +8), **Offer a deal** (`vote_save`), **Call out the HoH** ⟨Confrontational⟩ (public; witnesses mark receipts) | Vote (`DealObligation`), receipts | the Final Plea room act; Final Pleas beat | M2 |
| `the-invite-list` | Who gets called up to the HoH room | You are HoH: invite up to two. You are not: **Ask to come up** (the web's invite request) | Invited: +9 and `story:heard-out`. Everyone not invited who ranks you warmest: `story:snubbed` (−4, "they noticed who you picked"). A request refused: −4 | Receipts, trust, the week's nominations | Invite Up, invite requests | M3 |
| `spy-screen` | The HoH sees who's talking | You are HoH, once a week | One fact: a pair's private conversation, chosen from recorded NPC conversations this week, never randomly. **Confront them**, **Keep it**, **Use it** | Knowledge store, hooks | Spy Screen | M5 |
| `the-morning-after` | Reading the room after an eviction | Eviction night, week ≥ 2 | **Check on the house** (learn who lost an ally: a fact), **Rally your allies** (+2 each, and the HoH notices), **Stay in bed** (−1 stress) | Facts, receipts, stress | The Morning After beat | M1 (the fact M5) |
| `vent-session` | Venting turns into a plan | `VentAbout` on a shared target, the web's 35% | **They can't be trusted** (risky), **Maybe I'm overreacting**, **Should we do something?** → **Our secret** / **Let's rally others** (risky: starts `the-house-turns` against the target, with you as an aggressor) | Grudges, the pile-on | the `vent_about` tree | M2 |
| `late-nights` (re-anchored) | The showmance | As in plan A1, but its ×2 weight reads recorded bedroom-venue talks if D-E allows, or `contacts.weekCount` if not | As in plan A1 | As in plan A1 | Pillow Talk (C4) | M5 |

---

## 6. What changes in the plan's roadmap

- **M1:** port `inferIntent` (pure). Let the five topic commands open a running cycle's next beat (G1).
  Add `reckonings` to the v14 bundle (G2). `the-morning-after` ships with the anchor pool.
- **M2:** grudge writers per D-A and D-B. `the-reckoning`, `the-agenda`, `caught-talking` (without the
  knower half), `final-plea` and `vent-session`. The nine phase beats (G7) fill the anchor pools.
- **M3:** room staging (G6) and room-bound acts if D-E allows. `the-invite-list`. The confront and
  campaign menus become Approach Moments (G5).
- **M5:** `spy-screen`; the knower halves of `caught-talking` and `the-agenda`.
- **Tests:** a derived-parity dotnet test for the topic trigger table and `inferIntent` against the web's
  functions; a fold-number parity file (G9).

## 7. New decisions for the owner

| # | Decision | Recommended default |
|---|---|---|
| D-A | Grudge from a first-round nomination | Keep the plan's 70, labelled "completes web intent": the web's detector runs weekly for it, but its live nomination path records no typed event. Adopt the live fold for everything else (\|Δ\|×5 at drops of 8 or more; oath 70, deal 60, alliance 40, ×(1 + threat/200)). |
| D-B | Direction of the veto-replacement grudge | The intuitive one: the replacement holds against the HoH (and the holder). Do not copy the fold's argument-order inversion. |
| D-C | Conversations as a beat trigger | Yes, on the story stream, with the web's topic table, counted against airtime. |
| D-D | Persist betrayal flags as `reckonings` | Yes, in the v14 bundle; cleared by the conversation that answers them. |
| D-E | Room-bound acts (Pillow Talk, Public Callout, Invite Up, Cook...) | Model the **act**, not the room: append a few commands named for the act (the engine never needs the room, because the act implies it), and let presentation offer them only in that room. The alternative is to keep rooms presentation-only and lose the web's room semantics in the rules. |
| D-F | The web's reputation changes (never applied in the web) | Drop them; the plan's witness receipts carry the same idea. |
| D-G | The Big Brother build's pull-aside and campaign deals (never created in the web) | Create them in Unity (`vote_save`, `vote_together`), labelled "completes web intent". |

## 8. Porting traps found in the Big Brother build

- **Real-time clocks.** Room vibe decays 5 points per real minute. Time slots are 2 real minutes. NPC
  room life has a 30 s real cooldown. "Deal broken in the last 7 days" uses the wall clock. Convert all
  of these to anchors and weeks.
- **`Math.random`.** Spy Screen, pillow-talk chance, callout chance, the reputation ripple's shuffle, the
  storyline trigger roll, and NPC confrontation penalties. Move all of them to the keyed story stream.
- **`sessionStorage` state.** HoH invites, room vibe and the comp-practice bonus are not saved in the
  web. Decide what Unity persists; the invite list probably should be, if `the-invite-list` ships.
- **Wiring that looks live and isn't.** `ADD_DEAL` has no reducer case. `game:npcChatAction` is sent on
  `window` and heard on `document`. The storyline adapter's relationship, deal and memory methods are
  no-ops. `game.relationshipSystem` is null. Emergent mediation's −10 grudge is dropped. The Diary Room
  "Confessional" room act is a stub ("coming soon").
- **No Survivor wording in the event content.** The only hit for tribal, castaway or idol in the Big
  Brother build is a homepage blurb for a Survivor game mode (`src/components/homepage/HomePage.tsx:53`);
  "island" appears only as kitchen islands and UV islands. Keep plan §7.4's Survivor-word lint anyway, as
  a guard against copying from the wrong tree.

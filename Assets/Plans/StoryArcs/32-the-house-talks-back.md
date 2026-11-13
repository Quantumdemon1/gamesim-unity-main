# 32: The house talks back. People who come to you, quiet asks, voices and tells, talkers, overhearing, and how each houseguest sees you

**Status:** Plan, 2026-10-30. Revised after an adversarial critique, checked claim by claim against the code, then checked for consistency with itself and with the offers plan. The owner answered its six questions on 2026-10-30 (their defaults, §P); those answers stand. **Refreshed 2026-11-13 against origin/main 0ab29719** (§Q): the role moments' switch-on (R7) comes first, and the slicing, the levels and the landing order changed. §Q binds wherever it and an older section disagree. **The owner's answers of 2026-11-13 overtake the chip:** there is no cast-screen chip and no `Offered`; each stack becomes the default for every new season when its switch-on PR moves `Current` (§R lists the overtaken lines and what replaces each; §R binds over §Q). Nothing here is implemented yet.

**Refs and prefixes.**
- `@m` is origin/main 37416049. `@h` is claude/all-star-histories 0d4d2417. The story code cited here is the same at both refs.
- `@n` is origin/main 0ab29719 (2026-11-13). Every `@n` citation was read on that ref for the refresh. Line numbers at `@m` and `@h` have drifted since, so each slice re-reads its anchors on main before it builds.
- A citation without a ref is at `@m`.
- Path prefixes:
  - `S/` is `Assets/Gamesim/Simulation/` and `SS/` is `S/Story/`.
  - `R/` is `Assets/Gamesim/Runtime/`, `RE/` is `R/Episode/` and `RP/` is `R/Presentation/`.
  - `T/` is `Assets/Gamesim/Tests/EditMode/`.
  - `P/` is `Assets/Plans/` and `SA/` is `P/StoryArcs/`.
- `P/OFFERS-COME-TO-YOU-PLAN.md@o`, where `@o` is the local branch claude/offers-come-to-you at 43d5faa0, not yet pushed. That plan has been on main since 2026-11-01, with its slices 1-3 built (PR #39) and slices 4 and 5 not (its line 3 `@n`). This plan is that plan's slice 5. It follows that plan's source contract (§C.4), and that plan's names win. §D0 lists what this plan takes from it.

## The owner's request and follow-ups

The request, 2026-10-30:

> "previously I created a plan to make more interactive storylines and events. These would be a choose your own story style quest that at the end of several interactions with the players, you get to know them, their play style and get a boon or a bane, so either an alliance comes together, they hate you and get a rival, you get a showmance, etc. Depending on several factors, the NPCs will also come to you physically in the game and approach you about different missions or questions, although they wont call them that, like spying on other players or finding out if people are working together or lying. Build out this plan."

The first follow-up, 2026-10-30:

> "Build on this further to work with events in the house, like you win veto, your ally wins HOH, you win HOH, etc."

The second follow-up, 2026-10-30:

> "Another thing is the responses are sometimes generic, like if you ask about the vote, it should be more interesting and varied. Like someone telling you a lie might have a clue like their language seems wishy washy, or they ask you what you're thinking or dont give you a definite answer, there can also be players who like to spread gossip around, it's also not good listening in requires you to spend action points, that should just be a reward for walking around the house to gain info and it should update your web in the social view with possible connections, for instance they're talking nice with each other indicates they might be allies or friendly, or a fight might indicate they are rivals, build this out further and integrate it with the current plan"

The third follow-up, 2026-10-30:

> "Also have NPCs react to how you play competitions, your current and past interactions with them, and with the other NPCs. That goes for their choices in how to tell you information, if they trust you or not weights a specific answer, whether neutral or honest or dishonest. The goal is make the interactions with NPCs immersive and realistic like playing in the actual Big Brother House"

**What this plan builds on.** Plans 20, 21, 30 and 31, ROLE-MOMENTS-PLAN, NPC-AGENCY-PLAN, HOUSE-LIFE-PLAN's "Houseguests come to you", DIALOGUE-PLAN, ALL-STAR-HISTORIES-PLAN §14 and the offers fix. It replaces none of them.

## TL;DR

1. **Three stacks, three story-rules numbers, and one cast-screen chip.**
   - **Stack A** is story rules 12, `StoryRules.Manners`: how people answer, and overhearing.
   - **Stack B** is 13, `StoryRules.Gossip`: honesty in every answer, and talkers.
   - **Stack C** is 14, `StoryRules.ComesToYou`: getting to know people, walk-ups, quiet asks, and the house's events.
   - The owner confirmed the three numbers (question 1, 2026-10-30, a delegated call). 10 was left to the story session, which since built it as `Blame` (STORY-RULES-10-PLAN).
   - The chip is "The house talks back", off by default. It writes `StoryRules.Offered`.
   - `Offered` moves to a stack's number only when that whole stack has landed. It stays 0 until Stack A has landed and passed its measurement (the owner's answer to question 2(a)); until then no chip is shown, and the flag changes nothing (§L). When shown, the chip is off by default (2(b)).
   - `Current` is `Blame` (10) today and becomes `Roles` (11) at R7's switch-on; it then moves to 12, 13 and 14 at A-ON, B-ON and C-ON, one stack at a time (§Q1, §R).
   - Every number needs schema 30. Every one is at least Roles (11), so a chip season plays the built role moments. Owner question 2 covers this; after R7 every new season plays them anyway.
   - No saved field. No command kind.
2. **How they tell you (S1).**
   - `Regard.Of` is a pure reading of each houseguest's faith in you. It is built from trust, your history together, how you play competitions (including head-to-head finishes) and your visible ties to the others.
   - `Candour.Choose` uses that reading to pick honest, neutral or dishonest, on a keyed coin.
   - `Manner` makes a lie readable: hedging, a question back, or laying it on thick. Honest people show those signs too, so a tell is evidence, not proof.
   - Each answer's hidden stance is recorded for the week. Tells are learned only from real lies, never from changed minds.
3. **Overheard (S2).**
   - Walking near a conversation gives a **Notice**: a hunch about the pair.
   - Lingering gives a **Listen**: today's eavesdrop reveal with no draws, written as today's eavesdrop line, capped, with a risk of being caught.
   - From 12 the paid listen-in is refused. Its captions stay word for word, and its tiles walk you over to the conversation instead.
4. **Hunches (S2).** The social web gets dashed "possible connection" lines (warm, scheming or tense), built only from what you saw. They can be wrong, and they are never filtered by the real pacts.
5. **Talkers (S4).**
   - A talker is someone with Sociable − Honest ≥ 3, capped at a quarter of the house.
   - Talkers pass you true, distorted and, rarely, invented items about votes and pacts. All of it is log and memory only. None of it ever becomes knowledge.
   - What you tell a talker travels round the house.
6. **Getting to know {Name} (S5).**
   - An `acquaintance` thread, one at a time: at most 2 begin a season below 12 seats and 3 from 12 seats. At 8 seats the calendar usually allows one full three-chapter acquaintance (§B1, §M).
   - Each has three chapters: a first talk, a power chapter chosen by the house's events, and the verdict. A quiet ask from that person, when one comes, is the test between them.
   - It ends in **Where you stand**, with 14 endings (13 outcomes and "left unanswered"), all written through existing effect kinds.
7. **Got a minute? (S6).**
   - Story beats walk up as one more source on the offers fix's queue, `ComesToYou.Source.Story` (its slice 5), beside that plan's deal, reply-card and pitch sources (§D0).
   - While the offers fix's `AsksOn` holds, a walk-up beat stays out of the Story Pull.
   - **Words** are short spoken reactions with no decision. They are never saved.
8. **Between us (S7).**
   - Six quiet asks, cast from what the asker knows.
   - Every report offers the same answers whatever the truth: both polarities where the ask has two, a pick where it asks for a name (E1's "Point them elsewhere", E6's "Name them"), and a neutral answer ("Couldn't tell you"; E1's "Say you saw nothing", E5's "Say it's close", E6's "Say you don't know"). Only E1's "Tell them what you saw" has a condition, and it reads your own evidence, never the truth (§E2).
   - The report comes once you have evidence, or at the last anchor before the deadline.
   - What you knew is judged when you answer. A lie is found out when the asker comes to know the truth.
9. **The house's events (S5, S8).** `HouseMoment.Of` tags each anchor relative to you: who won HoH or the veto, who was nominated, saved or left up, who was evicted, blindsides, flips, competition finishes, jury talk and the final four. S5 builds this pure reader and uses its tags to choose an acquaintance's power chapter. S8 builds the event arcs that start from the same tags (§F) and only reads `S/HouseMoment.cs`.
10. **Pacing.**
    - Budgeted asks stay ≤ 9 a season at 8 seats.
    - Event arcs take the pool's draw at their anchor, and a priority order decides between them. Their net count is measured, not assumed to be zero.
    - New decisions are quiet-ask reports (≤ 3), the second nodes of the first talk and the verdict (≤ 6) and acquaintance chapters, with at most one retry each (§M). The target is ≤ +5 a season at 8 seats (about 2.5 minutes at the lab's 30 s), measured, with a cut order if it is missed (§M).
    - Words and overhearing are free.
    - Everything is measured paired against story rules 11 on the same seeds.
11. **Nine slices: eight in three stacks (A is S1 and S2, B is S3 and S4, C is S5-S8), then S9, balance and enrolment, which belongs to no stack (§O).** Stack A (S1 and S2) can be played on its own. The refresh adds HT0 before S1, splits S2 and S5 in two, and replaces S9 with one enrolment PR per stack (A-ON, B-ON, C-ON); its order is §Q3.

## Names the player sees

The game never says "mission" or "quest".

| Name | What it is |
|---|---|
| **Getting to know {Name}** | An acquaintance thread. It is a Thread-lane thread (B1), so `EpisodeEngine.Threads` (`SS/EpisodeEngine.Threads.cs:245@n`) lists it like any thread: under THREADS in the notebook (`ThreadsBlock`, `RE/EpisodeDirector.Story.cs:286-296@n`), on the campaign screen, the dashboard and the free-time board, and first in the weekly recap's Previously on and Next time (`RP/WeeklyRecap.cs:136,158`). Its row reads "Getting to know {Name}". Its end is announced as **Where you stand**. It is listed only once it has reached the player (§Q6, the "reached the player" rule of 8938c7d1) |
| **Got a minute?** | A houseguest walks up to you |
| **Between us** | A quiet ask. The PLAYS block shows it as "Between us · {Name}" through a display-only `PlayTemplate.label`; its saved title stays "Between us" (§E) |
| **Their manner** and **their tell** | How someone sounded, and how they have sounded when they lied to you before |
| **Talks** | The notebook flag on a talker, shown once one has told you something |
| **Overheard** and **Hunches** | Free overhearing, and the possible connections it draws on the web |
| **How they seem to see you** | The notebook's summary, built from evidence only |

---

## A. The request, mapped

| The owner's words | What exists | What is new here |
|---|---|---|
| "choose your own story style quest… several interactions… boon or a bane" | Plays (rules 7, `SS/StoryCatalog.Plays.cs:70-655`). Threads: the bond, rivalry and numbers (rules 9, `SS/StoryCatalog.Threads.cs:15-240`), seeded only in weeks 1-3 (`SS/EpisodeEngine.Threads.cs:15,31-45`). A thread's end applies no effects (`:194-205`) | The `acquaintance` thread, seeded by events in any week. It plays three chapters (a first talk, a power chapter chosen by the house's events, and the verdict), drawn from seven chapter templates of its own (§B2), with a quiet ask from that person, when one comes, as its test. Its verdict applies the boon or bane through existing effect kinds (§B) |
| "you get to know them, their play style" | The lore ladder, Know Them, and agenda words on a read (`S/NpcAgendas.cs:93-107`; `S/EpisodeEngine.VoteRead.cs:123-126`) | Each chapter reveals one named thing: how they play, their agenda, their tell, whether they talk. They read **your** style too (§C) |
| "the NPCs will also come to you physically" | Only pending deal offers walk up (`S/NpcInvitation.cs:42-49`). Story Approach beats are a HUD Pull (`RE/EpisodeDirector.Story.cs:420-432`: `PlayPull`, then the Approach case) | Story Approach beats become the offers fix's `ComesToYou.Source.Story`. `HouseWords` adds spoken reactions (§D) |
| "spying… working together or lying… they wont call them that" | Player-initiated only. The Secret Alliance play is cast from the truth (`SS/StoryCatalog.Plays.cs:68-104`) | **Between us**: six asks cast from the asker's knowledge. Answers are always offered both ways. Then discovery and a tip-off (§E) |
| Follow-up 1: "you win veto, your ally wins HOH, you win HOH, etc." | Anchors (`SS/StoryState.cs:341-372`) and `PowerRow` (`S/SeasonLedger.cs:104-118`). Role moments are built, behind `RolesOn` (`SS/EpisodeEngine.Story.cs:36-37`) | `HouseMoment.Of`, a pure reader. The event table (§F) sets who comes, which chapter opens, what is in reach and how answers change |
| Follow-up 2 (1): "more interesting and varied… wishy washy… ask you what you're thinking" | One template and two spoken lines (`S/ConversationExchange.cs:231-253`). Truth or lie is a main-stream roll (`S/EpisodeEngine.VoteRead.cs:88`) | `HouseAnswers`: manner × voice × variant, with probabilistic tells (§G) |
| Follow-up 2 (2): "players who like to spread gossip" | An anonymous rumour mill (`SS/StorySystems.cs:310-357`; `SS/EpisodeEngine.StoryHooks.cs:394-426`) | **Talkers**: they pass items to you, are named in whispers, and spread what you tell them, within bounds (§H) |
| Follow-up 2 (3): "listening in requires you to spend action points" | The paid `Eavesdrop` draws from the main stream (`S/EpisodeEngine.cs:1434-1481`). The free "witnessed" caption is presentation only: it receives names and an allowlisted topic, never a simulation snapshot or effect (`RE/HouseConversationCaption.cs:10-11`). It shows, and nothing is committed, while `CanWitness` holds (`RE/EpisodeDirector.NpcSocial.cs:452-461`; the 6 m, same-room, clear-sight test is `R/House/HouseMeetingCoordinator.cs:425-437`) | Free **Notice** and **Listen**, committed through `WitnessNpcAct` with a "conv:" text (§I) |
| Follow-up 2 (4): "update your web… with possible connections" | The only line between two others is the true-pact "Suspected pact" (`RP/RelationshipWeb.cs:41-48,606-624`; `S/AllianceRead.cs:569-651`) | **Hunches**: `Seen` standings drawn as dashed lines, never filtered by the real pacts (§I4) |
| Follow-up 3: competitions, history, ties; "if they trust you or not weights a specific answer" | `TrustScore` (`S/ThreatAssessment.cs:198-211`) is read by no answer. Competition wins feed only NPC weights: threat (`S/ThreatAssessment.cs:99-101`), alliance desire (`S/NpcAlliances.cs:74`), the coalition test (`S/NpcDeals.cs:148`), the eviction-vote terms (`S/WebEvictionVoting.cs:409,529`) and backdoor targeting (`SS/EpisodeEngine.StoryHooks.cs:500-507`). No answer to the player reads the record directly | `Regard` (faith, fear, reasons) and `Candour`. One reading drives answers, approaches, asks, chapter branches and endings. Competition reactions are also spoken as words (§J, §F) |

**Built on, not duplicated:**
- Role moments' pitches, veto asks, keep-the-block, pawn talk and quiet tears (`SS/EpisodeEngine.RoleMoments.cs`). The pawn talk's card stays "a claim and never a tell" (`:52-57`).
- The reply cards, which own confrontation, gossip about you and pleas (`S/ReplyCards.cs:6-27`).
- The dialogue store. This plan writes no `dialogue-` content and no occasion kind.

**Not built here.** ROLE-MOMENTS-PLAN row 15 (`P/ROLE-MOMENTS-PLAN.md:724`) deferred "allies approaching the holder" until after R8. That means allies coming to the player when the player holds the veto. This plan does not build it. Its `the-plan` and `the-call` arcs (§F) go the other way: an ally who holds the power comes to the player.

---

## B. Getting to know {Name}: the acquaintance thread

### B1. Structure

**The template.**
- `acquaintance` is an `ArcTemplate` (`SS/StoryTemplates.cs:21`), registered in the catalogue like the three in `ThreadArcs()` (`SS/StoryCatalog.Threads.cs:15-20`). Its `thread` field (`SS/StoryTemplates.cs:84`) holds a `ThreadTemplate` (`SS/StoryThreads.cs:17-56`), built the way `TheBond()` builds one (`SS/StoryCatalog.Threads.cs:51-65`). The thread's kind is `ThreadKinds.Acquaintance = "acquaintance"`, added to `ThreadKinds` (`SS/StoryThreads.cs:79-83`). That class is code only and never validated.
- On the `ArcTemplate`: lane `Thread`, one role THEM, `rulesVersion = StoryRules.ComesToYou` (14). On its `ThreadTemplate`: `fades = true`.
- Notebook label: "Getting to know {THEM}".

**Seeding.**
- A new `SeedAcquaintance(s, anchor)` in `SS/EpisodeEngine.Acquaintances.cs`. `StoryAnchor` calls it right after `SeedThreads` (`SS/EpisodeEngine.Story.cs:290`).
- It seeds only when all of these hold:
  - the season is at 14 and plays the role moments: `StoryAt(s, StoryRules.ComesToYou) && RolesOn(s)` (`SS/EpisodeEngine.Story.cs:36-37`). `BeginThread` skips `Castable`, whose `RolesOn` test (`:478`) refuses every acq-* chapter. This is the test the pawn talk's direct start makes (`SS/EpisodeEngine.RoleMoments.cs:235`), so seeding never begins a thread whose chapters cannot cast;
  - the anchor is HohCrowned, BlockSet or EvictionNight;
  - `BeforeTheFirstAsk` is false;
  - at least `AcquaintanceMinActive` = 5 are active;
  - no acquaintance is running (`AcquaintancesAtOnce` = 1);
  - fewer than `AcquaintancesASeason` have begun: 2 below 12 seats, 3 from 12 seats;
  - fewer than `PoolStorylineCeiling` (92) storylines in the season (`SS/EpisodeEngine.Story.cs:48`). `BeginThread` has no pool guard (`SS/EpisodeEngine.Threads.cs:47-50`), and a thread seeded past 92 could cast no chapter (§M).
- It calls `BeginThread` (`SS/EpisodeEngine.Threads.cs:47-69`) directly.
- **`SeedThreads` must skip the acquaintance kind**, both in its template loop (`:36`, which takes every template with a thread) and in its `MaxThreads` count (`:39`).

**Candidates.** The first tier that has any candidates wins:
1. A power event between them and you this week: one of §F's personal tags.
2. A patron whose Between-us ask you reported on this season.
3. Contact rapport with them ≥ `AcquaintanceRapport` = 6 (`ContactState`, `SS/StoryState.cs:150`). That is the depth-3 line (`SS/Lore.cs:213`), about three personal talks. Rapport rises 1-3 a conversation, at most 4 with a Sociable ≥ 2 houseguest (`SS/Lore.cs:183-205`), from a week of ceil(active/2)+2 conversations (`S/EpisodeEngine.Week.cs:68-80`). A value like 35 would need 9-35 conversations with one person outside your threads, which at 8 seats never happens before active drops below 5.

**Exclusions.**
- Anyone a running thread owns (`SA/31-threads.md:85-89`).
- Anyone you have already had an acquaintance thread with this season. This is a pure reader over `s.storylines`.
- Anyone under a cold shoulder (B4, ending 11).
- Anyone not active.

**The pick.** `StoryRandom.Index(s, "w{week}:{anchor}:acq", n)` over the candidate ids in ordinal order (`SS/StoryRandom.cs:39`).

**State.** None of its own. Everything is derived (B3) from:
- the thread's chapter steps (`SS/EpisodeEngine.Threads.cs:173-182`);
- the option ids on its chapters' paths;
- the ledger and the story world.

### B2. Chapters

**The chapter templates belong to the acquaintance alone**, so they never collide with the bond thread's plays (`SS/StoryCatalog.Threads.cs:71-73,109-110`):
- `acq-first-talk`;
- five power chapters: `acq-am-i-safe`, `acq-the-plan`, `acq-why`, `acq-the-debt`, `acq-now-what`;
- the verdict, `acq-where-you-stand`.

**How they behave.**
- Each is an `ArcTemplate` at 14 whose `cast` returns null without a thread focus. With empty `startAnchors`, only a thread opens it (`SS/EpisodeEngine.Threads.cs:156-170`).
- `cooldownWeeks` = 0 and `oncePerHeadliner` = **false**. The first start of a once-per-headliner template cools `once:{template}:{THEM}` for the whole season (`SS/EpisodeEngine.Story.cs:590`), so `Castable` (`:529-531`) would refuse every retry. Once-per-person comes from B1's exclusion and `AcquaintancesAtOnce` = 1 instead. The 1-week `star:` rest (`SS/EpisodeEngine.Story.cs:998-999`, checked in `Castable` at `:530`) limits each person to one chapter a week, and `Castable`'s running-template check (`:485`) stops a chapter from running twice at once.
- Since only one acquaintance runs at a time, the one-cycle-per-template rule (`:485`) never bites.
- Lane `Personal`, for the eyebrow. At 14 the chapters neither hold nor wait for a lane: `LeavesTheLaneOpen` (`SS/EpisodeEngine.RoleMoments.cs:258-259`) and `Castable`'s lane test (`SS/EpisodeEngine.Story.cs:489-492`) both learn `IsAcquaintanceChapter(templateId) && StoryAt(s, 14)`.
- Openers are `Approach` beats with a new code-only template field, `BeatTemplate.speaker = "THEM"` (`SS/StoryTemplates.cs:129-186`). S5 declares it (one line, saved nowhere). Nothing reads it until S6, so before S6 an opener is an ordinary Approach beat on the Pull (`RE/EpisodeDirector.Story.cs:424-432`). A chapter's opener need not be `beats[0]`: `ThreadChapter.opener` names it (the order table).
- Answers go through `ProgressStoryline` by option id (`SS/EpisodeEngine.Story.cs:793-828`). They are free, except for an option marked `costsAction` (node 2 of `acq-first-talk`: "Spend the evening with them"). That option spends a social action (`SS/EpisodeEngine.Story.cs:816-825`) and is drawn locked when none is left (`RE/EpisodeDirector.Story.cs:110`; §D3).
- Later nodes chain without an anchor (`:956-963`), so one chapter is one ask. `AsksThisWeek` counts a cycle and its close anchor once (`:178-180`).

**How a chapter lands.** Acquaintance chapters are arcs, not plays. They end "done" or the id after "end:" (`SS/EpisodeEngine.Story.cs:941-943`), so `CloseChapter`'s default of "a play won" (`SS/EpisodeEngine.Threads.cs:181-183`; `SS/StoryThreads.cs:66-67`) would record every played chapter as Backfire. So each `ThreadChapter` in the acquaintance's `chapters` carries its own `landed`, read from the chapter cycle as ride-or-die's does (`SS/StoryCatalog.Threads.cs:74`):
- `acq-first-talk`, `acq-why` and `acq-now-what`: landed when the player answered (the chapter has an option step that is neither Lapsed nor Npc), no step is Backfire, and the answered option's approach does not grate on THEM (`Personality.Reception(THEM, approach) >= 0`; a Lore hot-button counts as grating). Answered steps without a check record Plain, so a predicate never tests them for Success alone.
- `acq-am-i-safe`: as above, and the path holds the safety option. Nominating THEM after it is a betrayal handled by the forced verdict (ending 10), not by this result.
- `acq-the-plan`: as above, and the plan was kept: the HoH's nominations match the option you picked, read at BlockSet (§F).
- `acq-the-debt`: as above, or this week's `YouSaved` names THEM (§F).
- `acq-where-you-stand`: landed when `endingId` is in the thread's `good` set (endings 1-8).
- A first-talk or power chapter the player let lapse has its lapse option end `end:declined` (`PlayEndings.Declined`), so `CloseChapter` records it Lapsed, not Backfire. The verdict's lapses end on its endings (B4).

**The order:**

| # | Chapter | When | Shape |
|---|---|---|---|
| 1 | `acq-first-talk` | At the seeding anchor | Two nodes in play: the opener, then node 2 (the table below). A chapter starts on `beats[0]` unless told otherwise (`SS/EpisodeEngine.Threads.cs:161`; `SS/EpisodeEngine.Story.cs:572`), and a beat's text changes only by week (`SS/StoryText.cs:83-88`). So `ThreadChapter` (`SS/StoryThreads.cs:59-71`) gains a code-only `opener` function, and `TryStartChapter` passes its result as `StartCycle`'s existing `openerId`, as production's ladder names its rung (`SS/EpisodeEngine.StoryHooks.cs:582-583`). The function works the seeding reason out again at the anchor with the tier reader `SeedAcquaintance` uses, because the thread stores none, and picks one of four `Approach` openers: `thanks`, "I owe you a real thank-you" (you saved them); `talk`, "We should talk about this" (you nominated them); `respect`, "Okay, you're good at this" (a win); `hello`, a plain opener for every other reason (the other personal tags, a patron report, rapport). Each goes on to node 2 with no anchor |
| 2 | **The power chapter** | The first anchor where §F names one. Otherwise `acq-now-what` at the next EvictionNight | `acq-am-i-safe`: you are HoH, at HohCrowned, and only when THEM has no pitch this week (§D1). `acq-the-plan`: THEM is HoH and your ally. `acq-why`: at BlockSet, THEM left you up, you left THEM up, or THEM, as HoH, named you the replacement in a week with no backdoor (§F). `acq-the-debt`: one of you saved the other, at BlockSet. `acq-now-what`: "So, now what?" about whoever left, at EvictionNight |
| 3 | `acq-where-you-stand` | The verdict | Two nodes (B4) |

**The test.** A Between-us ask with THEM as patron (§E) that runs while the thread runs is the test between chapters. Its outcome feeds standing (`cameThrough`, `foundOut`). It is a play, so it spends the play budget, not the arcs' weekly asks. Each chapter has `attempts` = 2 (`SS/StoryThreads.cs:70`): a chapter that lapsed or missed may be tried once more, which needs `oncePerHeadliner` = false (B2).

**The verdict comes early** when any of these holds:
- chapters 1 and 2 have resolved and standing is ≥ 70 or ≤ 30;
- `AcquaintanceVerdictWeeks` = 3 weeks have passed since the thread began;
- a forcing event:
  - you nominated THEM after promising them safety;
  - your lie to THEM was found out (§E);
  - THEM's veto saved you, once chapter 2 has resolved. A save before then is chapter 2 itself: at that BlockSet it opens `acq-the-debt` (§F: an event that involves THEM becomes the thread's power chapter), not the verdict;
- active ≤ 4.

`climax` (`SS/StoryThreads.cs:38`) returns the verdict chapter's ending once that chapter has ended. It returns `parted` when THEM leaves the house, because a thread outlives its people by default (`SS/EpisodeEngine.Story.cs`, `EndDepartedCycles`).

**Node 2 of `acq-first-talk`.** Each option carries a `Personality.Approach` (`SS/Personality.cs:118-125`). Reception pays ×1.5 when the approach resonates, and costs half when it grates (`:127-175`).

| Option (caption) | Shown when | Approach | Effects |
|---|---|---|---|
| "Keep it light" | Always | Playful | `Move` +3 |
| "Be straight with them" | Always | Candid | `Move` +4. A keyed check of 60 against a Bold or Vengeful THEM; a backfire is `Move` −3 |
| "Talk numbers" | Always | Calculated | `Move` +4. A keyed check of 55 against THEM (`.Checked(55, "THEM", "They didn't like being treated as a number.", Move(Player, "THEM", -3))`, `SS/StoryCatalog.cs:90-98`), so the step records Success or Backfire (`SS/EpisodeEngine.Story.cs:853-858`). It grates (−10 on the chance, and the gain is halved and turned into a loss) when THEM is Warm ≥ 2 with Steady ≤ 0 (`SS/Personality.cs:138-141`), or on a calculated hot-button, known or hidden; a known fact that respects the approach overrides the axes (`SS/StoryOdds.cs:82-88`; `SS/Lore.cs:135-162`). It resonates when Steady ≥ 2 and Warm ≤ 0. On Success you learn their tell (§C) |
| "Ask what they want" | Always | Warm | `Move` +2. You learn their agenda in words (`NpcAgendas.Describe`) |
| "Bring up what matters to them" | Fictional cast only. You know a fact of theirs that carries a `Personality.Approach` with a positive reception: `Lore.KnownReception(s, THEM, a) > 0` (`SS/Lore.cs:131-145`). Origin and work facts carry no approach and hot-buttons grate, so neither counts | That fact's approach. An option's approach is a static string copied at draw (`SS/StoryTemplates.cs:199`; `SS/EpisodeEngine.Story.cs:647`), so the option is authored once per approach (`lore-warm`, `lore-candid` and so on) with the same caption. Each copy's `showIf` holds only for the approach of the **first** qualifying fact in `Lore.Learned(s, THEM)` order (shallowest, then id), so at most one copy shows. The shared caption passes the lint (`T/StoryCatalogLintTests.cs:86`) | Lore reception, read from that approach when you answer (`SS/StoryOdds.cs:83-89`) |
| "Tell them about your alliance" | You are in a pact | Candid | `Move` +5. **No fact is written.** Ending 12 reads this option's id (`confide`) from the path |
| "Spend the evening with them" | Free time | Warm | `costsAction`; `Move` +6 |

**Option rules.**
- Every `Move` with the player goes through `ChangeKeyed` on the router's existing key, `{house-event id}:{option id}:move:{from}:{to}:{type}`. `ResolveStoryBeat` builds the first two parts (`SS/EpisodeEngine.Story.cs:882`), and the router adds the rest (`SS/EpisodeEngine.StoryEffects.cs:74`) and calls `ChangeKeyed` (`:87`, in the Move case at `:77-98`; the method is at `:25-27`). The chapters add no key of their own.
- Real alumni get a **Game** variant of every chapter (`SS/StoryPeople.cs:7-15,43-48`; `SS/EpisodeEngine.Story.cs:518-526`): no lore option, no personal topics, and only this season's votes, power and pacts.

### B3. Standing: a pure reading

`Acquaintances.Read(s, thread)` lives in `SS/Acquaintances.cs`. It is pure and stored nowhere.

**Counts it reads:**
- `landed` and `missed`: chapter steps that `CloseChapter` marked Success or Backfire through each chapter's `landed` predicate (B2). Lapsed steps count as neither.
- `cameThrough` and `foundOut`: THEM's `story:came-through` and `story:steered-wrong` receipts about you.
- Style tallies: the approaches of the options you chose in any cycle that had THEM in its cast.

**The score:**

```
standing = Regard.Of(THEM).faith                    // §J, 0..100
         + 10·landed − 15·missed + 10·cameThrough − 25·foundOut
         + 2·clamp(TraitAffinity.Compatibility(THEM, player), −4, 4)   // S/TraitAffinity.cs:65-86, an int
```

`standing` is a whole number: `Acquaintances.Read` takes the floor of the sum, so no standing falls between two of §B4's thresholds (30 and 31, for example).

**Their read of you** is your largest style tally, named at the verdict:

| Largest tally | What they say |
|---|---|
| Warm or Playful | "You're good with people." |
| Calculated | "You've always got an angle." |
| Candid | "You don't play games with me." |
| Bold or Hardball | "You come at things head-on." |
| Yield | "You keep your head down." |

### B4. Where you stand: the endings

**Node 1** is THEM's read of you. It routes when it is drawn. Its options share one caption, "Where do we stand?". Their `showIf`s are mutually exclusive: only one bane is forced (below), and standing is a whole number (§B3). So exactly one shows, and no card shows two controls with one caption. Lint's duplicate-caption rule (`T/StoryCatalogLintTests.cs:85-87`) passes a caption once any copy has a `showIf` (`:86`), so `AcquaintanceThreadTests` pins this (§N). Each option's `next` names the node-2 beat for its case. Each route is a separate beat, because an option's `next` is a static string (`SS/StoryTemplates.cs:239`) and a lapse is never hidden or locked (`SS/EpisodeEngine.Story.cs:639,689-690`; `T/StoryCatalogLintTests.cs:63-66`). The node-2 beats:
- `where-boons` (standing ≥ 31, no bane forced): every boon whose conditions hold, plus "Let it go" as its lapse, which ends on ending 8.
- `where-cold` (standing ≤ 30, no bane forced): "Let it go" as its lapse, which ends on ending 11.
- one forced-bane card for each bane (`where-enemy`, `where-rival` and so on). Only one bane is forced: the first whose conditions hold, in the order 10, 12, 13, 9. Each forced route's `showIf` holds only for that bane, as §B2's lore copies hold only for the first qualifying fact. Each forced-bane card holds:
  - "Own it" (Candid): a keyed check of 50 plus reception, which writes the new receipt `story:came-clean` (§L). Its `next` is the bane one step lower (sworn enemy to rival; rival, a leak against you and a target on your back to a cold shoulder), with that ending's writers as `effects`. So a successful "Own it" on ending 12 spreads nothing and on ending 13 writes no grudge: THEM keeps your secret, or lets it go, but cools on you (the lead's call, 2026-10-30). Its `nextOnBackfire` is the bane itself, with the bane's writers as `backfire`.
  - "Stand your ground" (Bold): the beat's lapse, unchecked and ungated. It ends on the bane and writes it, so an unanswered forced card takes the bane as it is.

**Node 1's lapse** has a caption of its own, "Leave it unsaid". It cannot chain into an anchorless node 2 (`T/StoryCatalogLintTests.cs:68-69`). It ends the verdict on `end:unanswered` (ending 14). When node 1 was drawn with a bane forced, the lapse's `extra` writes that bane's writers, so leaving the verdict unanswered cannot dodge the bane. Otherwise it writes nothing.

**How each boon lands.**
- Each boon option is `.Checked(40, "THEM", "Not yet.", <ending 8's Move +3>)`, with ending 8 as its `nextOnBackfire`. It also carries a new code-only member, `checkAt = (ctx, cycle) => clamp(Acquaintances.Read(s, thread).standing, 40, 90)`. The chance is worked out when the beat is drawn and stored in the existing saved `HouseEventChoice.checkBase` (validated 0-100 at `SS/EpisodeStoryValidation.cs:161`), so a card answered later rolls the chance it showed. No new saved field.
- `OptionTemplate` gains `public Func<StoryContext, StoryCycle, double> checkAt;` after `SS/StoryTemplates.cs:215-216`: the base chance worked out when the beat is drawn; it overrides `check`; null for a constant check. `DrawBeat` (`SS/EpisodeEngine.Story.cs:648-649`) computes `double check = option.checkAt?.Invoke(ctx, cycle) ?? option.check;` and uses it for both `checkBase = check` and `subjectId = check >= 0 ? cycle.Role(option.checkRole) ?? Headliner(cycle) : null`. Otherwise a dynamic check would roll with no subject and lose every `StoryOdds` subject term.
- A failed "Own it" carries the bane on its backfire list, so no bane may hold a `Move` of −8 or worse (ending 9).
- "Let it go" is the lapse of `where-boons` and `where-cold`. It always ends the thread: ending 11 at standing ≤ 30, ending 8 at standing ≥ 31.
- Effects resolve when the beat is drawn (`SS/EpisodeEngine.Story.cs:627-630`).
- Every ending writes **at most one grudge**.
- Every writer is an existing effect kind (`SS/StoryEffects.cs:94-99`).

| # | Ending | Conditions (all must hold, unless the row says "either") | What it writes |
|---|---|---|---|
| 1 | An alliance comes together | Standing ≥ 70; THEM's score of you ≥ 8; under the pact cap (`S/EpisodeEngine.Pacts.cs:73`); THEM's agenda is Build or Hold, or THEM has no pact; "Let's make it official" | `Alliance` (`SS/EpisodeEngine.StoryEffects.cs:306-321`). C6 then makes them answer you straight (`S/EpisodeEngine.AllyIntel.cs:92-108`) |
| 2 | A final-two word | Standing ≥ 80; allied or ride-or-die; active ≤ 7; "Final two?" | `Promise` FinalTwo (`:330-365`), settled at `FinalEvict` (`S/EpisodeEngine.cs:1141-1170`) |
| 3 | A confidant | Standing ≥ 60; cameThrough ≥ 1 or landed ≥ 2; "Keep it between us" | `Bond` confidant. Honest with you by §J's fixed values, which put a cold shoulder and `liar:` first and give a confidant who is also a nemesis no fixed value. Passes you one true item a week, with no coin, at the first anchor where they hold one, outside the talkers' 2 a week; log and memory only (§H, "Confidants pass first") |
| 4 | Ride or die | Standing ≥ 65; you saved them with the veto, or kept a safety word through a week they could have been put up; "I've got your back" | `Bond` ride-or-die (`SS/StoryConsumers.cs:36-45`) |
| 5 | They owe you one | Standing ≥ 50; you saved them or came through on their ask; "You owe me one" | `Hook`, holder you, over THEM (`SS/StoryCatalog.BondsAndSecrets.cs:506-545`) |
| 6 | A showmance | **Fictional houseguest only**; `romanceStorylines` on; `Lore.RomanceOpen(s, THEM)` (`SS/Lore.cs:282-289`: never a real person, never a sheet whose romance is `closed`, otherwise Sociable ≥ 1; this covers `StoryPeople.Allowed(THEM, Romance)`) and `PlayerAllowed(Romance)` (`SS/StoryPeople.cs:65-66`); standing ≥ 75; your top tally is Warm or Playful; neither you nor THEM has a showmance partner (`Bonds.ShowmancePartner`, `SS/StorySystems.cs:30-35`), the gate late-nights casts with (`SS/StoryCatalog.BondsAndSecrets.cs:54,59`); "See where it goes" | Late-nights' own writers on this option: `Move` +6, `Bond` showmance Private, `Fact` Couple Private (`SS/StoryCatalog.BondsAndSecrets.cs:106-109`). No chapter is adopted |
| 7 | A truce | The thread opened from a nomination or a grudge; standing ≥ 45; "Clear the air" | `Ease` 30, holder THEM, target you (`Ease("THEM", Player, 30)`: it lowers THEM's grudge against you by 30 and removes it at 0; the amount must be positive, because `Grudges.Ease` ignores amounts ≤ 0, `SS/Grudges.cs:87-94`), and `Receipt("THEM", Player, StoryReceipts.MadePeace)` (`story:made-peace`, +10, `SS/StoryOdds.cs:147`), as the clear-the-air truce does (`SS/StoryCatalog.HouseRemembers.cs:337`) |
| 8 | Cordial | Either you chose "Let it go" at standing ≥ 31, or a boon option's check backfired (a boon is offered only at standing ≥ 45; the line "Not yet.") | `Move` +3 |
| 9 | A rival | Either standing ≤ 30, plus a missed chapter or a hostile option, or a successful "Own it" on a forced sworn enemy (ending 10; node 2's one-step-lower rule) | `Move` −7 from you to THEM through `ChangeKeyed`, one effect, since `ChangeWithRoll` already writes the reciprocal (`S/EpisodeEngine.cs:2443-2457`; arcs `S/WebRelationshipArcs.cs:50-69`); `Grudge` 30, cause Story. The Move stays above −8 on purpose: a forced rival also arrives on a failed "Own it", whose bane resolves as a backfire, and a backfired Move of −8 or worse also writes `Grudges.FromDrop`'s grudge of five times the drop (`SS/EpisodeEngine.StoryEffects.cs:95-96`; `SS/Grudges.cs:126-131`). That would be 50 + 30 stacked to 65: two writes, past WillNotSave's 60 |
| 10 | A sworn enemy | Standing ≤ 15, plus a betrayal on record: your lie found out, a broken promise, or a nomination after a safety promise | `Bond` nemesis (`SS/StoryConsumers.cs:94-113`); `Grudge` 60 |
| 11 | A cold shoulder | Either standing ≤ 30, no bane forced, and you chose "Let it go"; or a successful "Own it" on a forced rival (9), a leak against you (12) or a target on your back (13), by node 2's one-step-lower rule, so through a leak it can come at standing up to 45 | `Move` −5. For 3 weeks after the thread's `endedWeek` (`S/StorylineState.cs:35`), a pure reader holds their candour at Neutral and stops their approaches |
| 12 | A leak against you | The first talk's path holds `confide`; THEM is a talker (§H); standing ≤ 45 | `Spread` your pact's fact to Whispered (`SS/StoryTemplates.cs:310`). The double-dealing path follows (`S/EpisodeEngine.AllianceLeaks.cs:58-108`) |
| 13 | A target on your back | Standing ≤ 25; THEM is Vengeful ≥ 2 or Competitive | `Grudge` against you, cause Story. `Grudges.Add` adds only half the amount to a grudge THEM already holds (`SS/Grudges.cs:65-71`), so the amount is set when the beat is drawn, through an `.Extra` that reads the held grudge (precedent: `SS/StoryCatalog.HouseRemembers.cs:354`). It is 45 when THEM holds none, otherwise max(45, 2·(40 − held) + 4); the +4 covers up to two weeks' decay (`Grudges.DecayPerWeek`) between draw and answer. Either way it ends at or above the 40 lines for confrontation and alliance refusal (`S/NpcSocialActions.cs:565-569`; `S/EpisodeEngine.Pacts.cs:42,111`) |
| 14 | Left unanswered | Node 1 lapsed (above) | Nothing, or the forced bane's writers when a bane was forced. Outside the `good` set |

**Real alumni** can reach endings 1-5, 7-11 and 14, and 12 and 13 with game facts only. **Never 6.**

**Ending ids** are free text up to 80 characters (`SS/EpisodeStoryValidation.cs:125`). The `good` set is endings 1-8.

---

## C. Getting to know them, and them getting to know you

| Step | You learn | Source (pure) |
|---|---|---|
| First talk, "Ask what they want" | Their agenda in words | `NpcAgendas.Describe` (`S/NpcAgendas.cs:93-107`) |
| First talk, an option that resonates | What reaches them | `Personality.Reception` |
| First talk, "Talk numbers" landing | **Their tell** (§G2) | `Acquaintances.LearnedTell(s, id)`: any `acq-first-talk` chapter cycle with THEM = id whose own path has a step with optionId `talk-numbers` and result `StoryResults.Success`. Only B2's keyed check can write Success: an unchecked option always records Plain (`SS/EpisodeEngine.Story.cs:853`), and reception is never stored on the step. It is the chapter cycle's path, not the thread's, whose `chapter:` steps carry the cycle id. It is read from the path, so it is never a cooldown |
| The test | Whether they keep their word, and whether they talk | Honest axis ≥ 1; `Talkers.Is` (§H) |
| The power chapter | What they want from power, in words | The named terms of `NominationWeight` (`S/EpisodeEngine.cs:865-872`), never numbers |
| The verdict | How they see you | B3's read, plus Regard's public reasons (§J) |
| A reveal where they lied with a tell | "Last time they sounded like this, they lied." | `tell:` keys written by `SettleVoteRead` from real lies only (§G4) |

**Where it shows.** The houseguest profile gets three blocks: "What you've learned", "Their manner" and "How they seem to see you" (the last from 13, S3). The profile is `RenderHouseguestProfile` (`RE/EpisodeDirector.Journal.cs:401-434@n`), drawn by `EpisodeHud.HouseguestProfile(ProfileView)` (`RE/EpisodeHud.Roster.cs:42,246@n`). §H's "Talks" flag uses the same path.
- `ProfileView` gains `public IList<(string Heading, IList<string> Lines)> Blocks` and a `Talks` flag. `HouseguestProfile` draws each non-empty block as a heading and a card, uniquely named, between "What you remember" (`:308-311@n`) and "Other records" (`:322@n`). An empty block is left out, so a profile below rules 12 draws as before.
- A new partial, `RE/EpisodeDirector.Journal.HouseTalk.cs`, fills them through `HouseTalkBlocks(EpisodeState state, ContestantState actor, EpisodeHud.ProfileView view)` from the pure readers (`Regard`, `Candour`, `Manner`, `Acquaintances.LearnedTell`, `Talkers.Is`). `RE/EpisodeDirector.Journal.cs` gets exactly one line, `HouseTalkBlocks(state, actor, view);`, just before `hud.HouseguestProfile(view);` (`:432@n`). §14 has merged (PR #40), so the old hand-off is moot; `ProfileRecords` (`:456@n`) is still not edited here. The UI refactor's D7a rewrites this profile (`Journal.cs:401-490` and `EpisodeHud.Roster.cs`), so S1's blocks and D7a land in the order §Q5 gives.
- `S/HouseguestNotes.cs` is the notes page and the free-time and campaign cards' `Brief` (`RE/EpisodeDirector.Notes.cs:47`; `RE/EpisodeDirector.FreeTimeScreen.cs:275,661`; `RE/EpisodeHud.CampaignBoard.cs:861`), not the profile. It gains only notes-page lines, such as a tell revealed by `SettleVoteRead`, and only at rules 12 or above, because `For` is serialised into `T/ModeOneSeasonDigests.cs:200@n` and `T/CommitmentRulesSeasonDigests.cs:600@n` and must leave every digest at rules 11 or below unchanged (§Q2).

**How they learn you.** All of this is read inside `Regard`, with nothing stored:
- your style tally in their cycles;
- your public competition record;
- your record with them;
- what they have heard of you.

A line uses only what its speaker knows (`SA/20-story-arcs-brainstorm.md:629`).

---

## D. Got a minute? Houseguests who come to you

### D0. Built on the offers plan

This plan is the offers plan's slice 5 (`P/OFFERS-COME-TO-YOU-PLAN.md@o` §C.4, §D row 6, §H). It builds on that plan, never beside it, and that plan's names win.

**What it takes from the offers plan:**
- **The `ComesToYou` queue and its source contract.** `ComesToYou.Read` in `S/ComesToYou.cs` (offers §C.1) is the one queue, with its one public order and its exclusion of the veto board's asks under the role moments. On main its sources are `enum Source { Offer, VetoAsk }` (`S/ComesToYou.cs:34@n`); `Read` reads `RolesOn` (`:95@n`) only to drop the veto board's own asks (`:106-107@n`), and returns nothing when no invitation is available (`:92-93@n`), so a source that is not an invitation must not sit behind that early return (offers slice 4's lesson). `ComesToYou` is read by Runtime only, so a new source moves no season digest. A new source adds an enum value; a branch of `Read` that yields `Ask` rows from saved state only (`recordId`, `askerId`, `line`, a lapse line, its own `IsCurrent`); its place in the order; and its cases in `AskOpensInPerson`, `AskBubbleCaptions` and `AskBody` (offers §C.4).
- **The visit.** `RE/EpisodeDirector.ComesToYou.cs` (offers §C.2): the lease, the bubble, auto-open once the bubble has shown 1.2 s and the player has stood still 1 s, Hear (`HearAsk`) and the decline (`DeferAsk`). From offers slice 3 it adds the travel budget, the answer window from arrival and facing.
- **The frame.** The dialogue frame (offers §C.3), opened in person through `OpenOnAsk`: Think it over on a walk-over, Back to topics on a raise, with its focus and pointer hold.
- **The shared gaps, caps, misses and set-aside**, all on the free-house clock `askClock`: `AskGapSeconds` 20 s, `AskRetrySeconds` 8 s after a miss, `AskerGapSeconds` 120 s, 3 presentations a phase and 6 a week. A walk-away, a room change, or a lease that runs out before its auto-open was due is a miss (`RecordAskMiss`), never a set-aside. A lease that runs out once the bubble has shown 1.2 s with the player still 1 s (so only a held auto-open kept the dialogue shut), or in a stalled frame of over 1 s, only cancels: no miss, no set-aside, and the record and the next scan wait `AskRetrySeconds` (offers §A, §C.2 step 8). The decline (`DeferAsk`), an opened conversation (`OpenOnAsk`, walk-over or raised, so its Think it over or Back to topics leaves it aside) and the card (for a story beat, the scene card, §D1) set an ask aside for the phase (`askSetAside`).
- **The card fallback.** An asker who cannot come (busy or without a route for 10 s, or after two misses) opens as the source's card. For a story beat that is the scene card (`OpenSceneCard`, offers §D row 6).
- **The switch**, `AsksOn` (`AsksEnabled ?? !Application.isBatchMode`, `RE/EpisodeDirector.ComesToYou.cs:48@n`, offers §B). With it false, everything here is as at origin/main. Asks are off in batch, so every director test sets `AsksEnabled` explicitly.
- **Its other sources:** deal offers and veto asks (offers slice 1), reply cards, HoH pitches and the bubble's caption overload (offers slice 4). Offers slice 4 is now built in three PRs (§Q3): OF-a, the reply cards of kinds confrontation, gossip and plea, with the caption overload; OF-p, the pitch source: a player HoH's pitchers walk up, and in a season with the role moments the walk-up opens the existing pitch board (the owner, 2026-11-13; the veto board's asks still stay on the board, as his 30 Oct answer kept them); and OF-b, same-asker chaining only. S6 lands after OF-a, OF-p and OF-b.

**What this plan adds:**
- **Its source**, `ComesToYou.Source.Story`: open story beats with a `speaker`, with its eligibility, its place in the order and its dispatch cases (§D1). Its bubble pair is "What's on your mind?" and "Maybe later", distinct from the deal pair and from the Pull's six, as offers §C.4 requires.
- **The beats that walk up**: acquaintance chapters (§B), patron asks and reports (§E) and the event arcs (§F), each naming its `speaker`.
- **Its own per-source caps** (§D4), on top of the shared ones, and the Pull exclusion for speaker-bearing beats (§D3).
- **Words**: spoken reactions with no decision, drawn on a bubble of their own (§D1).

### D1. Split and interface

**The simulation decides, at anchors only.** A story walk-up is always a saved story beat, drawn at an engine anchor on a keyed stream. It is never drawn from the NPC clock.

**The story source** is `ComesToYou.Source.Story`, a branch of `ComesToYou.Read` in `S/ComesToYou.cs` (offers §C.4, §D row 6). There is no `S/Approaches.cs` and no second queue.
- From 14, while more than three houseguests are active (`s.Active`, `S/EpisodeState.cs:519`; final three is a 14 rule, §F) and in a phase `NpcInvitation.PhaseAllows` permits (`S/NpcInvitation.cs:33-35`), as `NpcInvitation.Available` gates the deal rows (`:42-49`), the branch yields an `Ask` row for each open beat whose surface is `Approach` and whose `speaker` is active, not under a cold shoulder, and not kept away by §D2's faith rule (`Regard.Of(s, speaker).faith` ≥ 30, unless the beat is a forced chapter, §D2). This is one pure test, read by the row's `IsCurrent` and by `CurrentPull`'s skip (§D3). A staged scene is director state, not saved state, and the inherited gate already holds every invitation back while one is staged (§D3).
- Each row carries `recordId` (the beat's event id), `askerId` (the speaker), `line`, a lapse line and its own `IsCurrent`, which re-checks that the beat is still open, the week, the phase and the conditions above, the way an offer's does (`S/NpcInvitation.cs:56-65`).
- **Its place in the offers plan's one public order** (offers §C.1), declared as §C.4 asks:
  - `lapsesOnAdvance` is true when the beat's `closesAnchor` is the anchor the next Advance reaches. `closesAnchor` defaults to the next anchor (`SS/EpisodeEngine.Story.cs:709`), so most open beats come before deals that do not lapse.
  - `week` is the event's week.
  - In the urgency key a story row reads its public beat kind: a forced verdict 3; an event arc or a patron report 2; a patron offer or a chapter opener 1. That is below every named deal urgency (`NpcDeals.Urgency`, `S/NpcDeals.cs:472`, 100 and up) and above its default 0.
  - Then `askerId`, then `recordId`. The speaker comes before the event id because event ids are minted in draw order (`SS/EpisodeEngine.Story.cs:80,699`). The order reads only public facts.
- **Its dispatch cases**: `AskOpensInPerson(Story)` is true; `AskBubbleCaptions(Story)` returns "What's on your mind?" and "Maybe later"; `AskBody(Story)` draws the beat (below).

**Deal offers, veto asks, reply cards and HoH pitches are the offers plan's own sources** (its slices 1 and 4), with its role-board exclusion and its pitch rule (offers §D row 5). Without the role moments (off the board) a pitch is offered only while it is the reply card waiting first (`ReplyCards.Pending(s)?.kind == ReplyCards.Pitch`, `S/ReplyCards.cs:102`), and `BoardPicks` plays no part. Under the role moments (on the board, as in every chip season, §L) the first pitch waiting in `s.replyCards` is offered: `HoldTheBoard` (`S/HoHPitches.cs:145-154`) has already stored the pitches in `BoardPicks` order, so it is never a fresh `BoardPicks` call, and the pitch board stays. They walk up whatever the story rules. This plan neither re-reads nor re-orders them.

**Regard never chooses a deal.** Which NPC proposes what stays with `NpcDeals`, and those sources present only what the engine already made.

**`HouseWords.Available(s)`** in `S/HouseWords.cs` is a pure reader of **words**: a spoken line with no decision.
- Words are never saved, move nothing, and come at most once per houseguest per phase. That memory (a set keyed by houseguest id) and the word caps (§D4) are instance fields, never statics, in a new director partial `RE/EpisodeDirector.HouseWords.cs` (with a hand-written two-line meta) on the offers plan's `AsksOn` path. They are keyed to the session, week and phase, and cleared when any of these changes and by the offers plan's `ResetAsksForLoad`, as its `askSetAside` is. The legacy `npcInvitationDeferred` (`RE/EpisodeDirector.NpcInvitations.cs:24`) is not used. It holds deal record ids, and its only per-phase clear is inside `TickNpcInvitations` (`:48-53`), which does not run while `AsksOn` holds and is not edited (`:46-106`). Its one other clear is `ResetNpcInvitationsForLoad` (`:184-186`), which runs on a load and when `AsksOn` switches (the offers plan's `TickAsks` step 1), and which the offers plan extends to call `ResetAsksForLoad()` (its §C.2).
- **Whether** someone speaks may read `Regard`.
- **The tone** of what they say reads only public facts and traits, by the C4 gate (`S/HouseDialogue.cs:453-460`) and RoleTone (`SS/RoleTone.cs:7-39`).
- The line is `StoryRandom.Index(s, "word:{week}:{phase}:{npc}", 3)`.

**How a story ask opens** (the Story cases of the offers plan's dispatch, §D0):
- The walk-over and the raise open the speaker's conversation through a Story case of `OpenOnAsk`. It sets `askDialogueWith`, `askDialogueRecordId` (the beat's event id) and the way, and adds the beat to `askSetAside`. It leaves `conversationCategory` and `invitedDealId` unset, because those are the deal keys the offers in-person branch reads.
- A Story branch of the in-person dialogue sits ahead of the topics wheel (`RE/EpisodeDirector.ConversationNavigation.cs:128`), keyed on `askDialogueRecordId` naming an open Approach beat whose speaker is this houseguest. It draws the eyebrow (`AskWalkOverEyebrow` on a walk-over; a story eyebrow of its own on a raise, never `AskRaisedEyebrow`'s "HAS AN OFFER FOR YOU"), the `SpeakerTitle` header with the portrait, and the record's line as `Speech`.
- `AskBody(Story)` then draws the beat's title, narrative and options as `RenderStoryBeat` does, including its pick and confirm step, without `StoryBeatHeader`'s switch to the HouseEvent layout. The options are answered through `ChooseStoryOption` (`ProgressStoryline`). It draws no `AskOfferHeading`, no terms and no offer controls.
- The way past is Think it over on a walk-over and Back to topics on a raise, with the frame's focus and pointer hold. As in the offers frame (offers §E step 4), the Story branch's Back to topics control calls only `BackConversationNavigation(true)`, guarded by `ConversationNavigationCurrent`. The Story dialogue leaves `conversationCategory` null, so the offers plan's clear in the category branch never reaches it: at origin/main `BackConversationNavigation` returns false for a null category with no narration (`RE/EpisodeDirector.ConversationNavigation.cs:86-93`), and Escape, Start or B would fall through `RE/EpisodeDirector.Controls.cs:261` and close the conversation. So S6 adds a Story case to `BackConversationNavigation`, after its exchange and showing guards (`:78-85`) and ahead of that null-category return. When `conversationCategory == null` and `askDialogueRecordId != null` (only a Story dialogue has that pair, because a deal dialogue always carries `DealsCategoryCaption`), it:
  - sets `askNextAt = askClock + AskGapSeconds`;
  - clears `askDialogueWith`, `askDialogueRecordId` and `askDialogueWay`;
  - calls `hud.DropFocusAsked()` and `hud.EndPointerHold()`;
  - renews `conversationMenuToken`, calls `Render()` and returns true. The next render draws the topics wheel.
  That one place serves both the control and Escape, Start or B in person.
- A Conversation beat with the speaker keeps the panel (`RE/EpisodeDirector.cs:1527-1529`; `HasConversationBeat`).
- The offers card (`askCardOpen`) is not a story host. When the speaker cannot come, the scan's card branch (offers §C.2 step 9, slice 2) opens the beat as the scene card (`OpenSceneCard`, `RE/EpisodeDirector.Story.cs:739-754`) in place of `OpenAskCard`, and keeps that card's bookkeeping (offers §E, the card):
  - once `OpenSceneCard` returns true, the branch adds the beat to `askSetAside` and counts a presentation, as `OpenAskCard` does;
  - it then records the beat's id as the queue's scene card. The id is set after the call returns, because `OpenSceneCard` runs `ClosePanels()` first;
  - when that card closes (`ClosePanelsInternal`, beside `RE/EpisodeDirector.cs:735`) with the beat still open, it sets `askNextAt = askClock + AskGapSeconds`, as `EndAskCard` does, and clears the id;
  - `OpenSceneCard` itself is unchanged, and the Pull's Hear them out (with `AsksOn` false, or for a beat the queue never presents) does none of this, as at origin/main.
  Because the beat is set aside, the next scan skips it, and talking to the speaker does not raise it again this phase. It stays answerable on the episode screen until its `closesAnchor`.

**How a word shows** (built here, in S6). A word is drawn on its own bubble, `HouseWordBubble` (`RE/HouseWordBubble.cs`), never on the ask bubble.
- It is its own overlay canvas with no buttons, copied from `HouseConversationCaption` (`RE/HouseConversationCaption.cs:24,192-194`): a tail to the speaker, the safe-bounds clamp and the `CaptionTime` reading hold. It is its own instance, so the ambient pair caption is untouched.
- `HouseNpcInvitationBubble` always builds Hear and Not right now (`RE/HouseNpcInvitationBubble.cs:35-36,164-165`) and refuses a null callback (`:40-43`), and its visibility (`NpcInvitationIsVisible`, `RE/EpisodeDirector.NpcInvitations.cs:31`) feeds E and Escape (`:154-173`), D18's hold on the Pull (`RE/EpisodeDirector.Story.cs:508-509`) and the offers plan's `askSpokeFor` and auto-open. So those read asks only, and a word can never be heard, deferred or auto-opened.
- A word shows only while `AsksOn` holds and the offers plan's `AskMayStart` gates pass. It never shows while `NpcVisitInFlight`, `askDialogueWith != null`, `askCardOpen` or `hud.PullShowing` holds, and it hides at once when any of them becomes true.
- A word is not a presentation. It adds nothing to `askPresentations` and moves neither `askNextAt` nor `askerNextAt`.

### D2. Who approaches, and why

| Speaker | Why | Source |
|---|---|---|
| An acquaintance | A chapter is due | §B |
| A patron | An ask, or a report due | §E |
| An ally in power | "Who do we put up?" (ally HoH); "Should I use it?" (ally holder) | §F |
| A warning friend | A rival of yours just won power | §F |
| A pitcher | You are HoH | The offers plan's Pitch source (its slice 4) |
| Someone you owe, or who owes you | They call it in | §F |
| A talker, a confidant or a competitor | "Did you hear…?"; a reaction to a finish (words) | §H, §F |

**Who never comes:**
- Faith < 30 (`Regard.Of(s, x).faith`, §J): they walk up only with a forced chapter or a pending deal offer, ally or not. A forced chapter is a forced verdict (`acq-where-you-stand` when §B2 brings it early, including every verdict at active ≤ 4, §F's final four) or §F's forced `acq-why` (Left up). A pending deal offer is the offers plan's own source, which this plan never filters (§D1). Any other story beat of theirs stays a Pull (§D3), and §E casts them no ask.
- 30 ≤ faith < 45: never on their own initiative, unless they are your ally and have an ask (§E). A TestLoyalty ally's ask opens "Prove it." at any faith from 30 up (§E): the intent sets the line, and no faith band gates it.
- A rival never comes to chat.

### D3. The walk, hearing it, declining, and never

**The walk** is the offers plan's visit (§D0):
- a 1.8 m ring of points in the player's room;
- a reservation, and a validity check at the director's 10 Hz presentation tick: `TickAsks` step 8 in `RE/EpisodeDirector.ComesToYou.cs` (offers §C.2), on `NpcInvitationValid` (`R/House/HouseMeetingCoordinator.NpcInvitation.cs:99`). A released or invalid lease there is a miss (`RecordAskMiss`), never the legacy set-aside, except in the two cases that only cancel (below). The legacy step it copies (`RE/EpisodeDirector.NpcInvitations.cs:68-89`, which adds the record to `npcInvitationDeferred` at `:75`) runs only while `AsksOn` is false (§D1);
- speech only on arrival, within 2.8 m and in clear sight;
- its time: today 25 s from dispatch (`R/House/HouseMeetingCoordinator.NpcInvitation.cs:38,94,103`). S6 lands after offers slice 3, so a story walk-up gets that slice's travel budget, clamp(measured route ÷ `HouseNpcMotion.DefaultSpeed` × 1.5 + 3, 6, 30) s, then 25 s from arrival to answer, and the asker turns to face the player when they can first speak (`R/House/HouseMeetingCoordinator.NpcInvitation.cs:36-130`).

**Hearing it.**
- A story walk-up's bubble has the new pair **"What's on your mind?"** and **"Maybe later"**, through offers slice 4's caption overload. Deal walk-ups keep "Hear their offer" and "Not right now".
- Hearing is free. Hear is the offers plan's `HearAsk`, which opens the speaker's conversation through `TryOpenNpc` and `OpenOnAsk` and spends nothing, as today's `HearNpcInvitation` does (`RE/EpisodeDirector.NpcInvitations.cs:132-145`). It opens the beat without answering it, moving the player or saving a record.
- Answering a deal is free (`RespondToDeal`, `S/EpisodeEngine.cs:2165-2172`).
- Answering a story walk-up is free except for an option marked `costsAction`, such as B2's "Spend the evening with them". That option spends a social action (`SS/EpisodeEngine.Story.cs:816-825`) and is drawn locked when none is left (`RE/EpisodeDirector.Story.cs:110`).

**Declining, and misses** (the offers plan's shared model, §D0).
- "Maybe later", or Escape, Start or B while the bubble is up, sets the walk-up aside for the phase. This is the offers plan's `DeferAsk`, reached through `TryDeferVisibleNpcInvitation` (`RE/EpisodeDirector.Controls.cs:260`). It adds the beat to `askSetAside` for the phase and no longer writes `declinedPulls`: this is slice 5's fold of the Pull's set-aside into the shared model (offers §C.4, §H slice 5). For an Approach beat that is now a source, the shared set-aside is its set-aside. `declinedPulls` keeps the Pull's own declines: walk-ins, Room and play beats, and a beat declined as a Pull.
- Walking more than 0.75 m from where you stood when the walk began, changing room, or the visit's time running out before its auto-open was due (`R/House/HouseMeetingCoordinator.NpcInvitation.cs:36-38,99-112`; the next bullet) is a **miss**, not a decline (`RecordAskMiss`). The same beat walks up again after `AskRetrySeconds` (8 s), once the house is free and you have stood still for 1 s. After `AskMissesBeforeCard` (2) misses in a phase it opens as the scene card where you stand. A miss never sets the beat aside, so it stays out of `CurrentPull` while it is retried.
- Under the offers plan's question 1, answered with its default on 2026-10-30, the dialogue opens by itself once the bubble has been on screen 1.2 s and you have stood still 1 s. So the visit's time running out is a miss only when auto-open never came due: the asker never arrived within the travel budget, the bubble was not on screen 1.2 s, or you were not still (moving inside the 0.75 m radius resets the 1 s). A lease that runs out after auto-open came due, because a gate held it (the map-only overview, the emote menu, a cast card, the Nearby card, a storm-off or an embrace), only cancels, and so does one that runs out in a stalled frame of over 1 s. It adds nothing to `AskMissCount`, nothing is set aside, the beat stays out of `CurrentPull`, and the same record walks up again after `AskRetrySeconds` (8 s) (offers §A, §C.2 step 8).

**The Story Pull and walk-ups at 14 (a fix for a deadlock).** Today a Pull on screen holds back every invitation (`RE/EpisodeDirector.NpcInvitations.cs:34-38`), and open Approach beats feed the Pull (`RE/EpisodeDirector.Story.cs:405-432`). So:
- **Story walk-ups, the Pull exclusion and words run only while the offers plan's `AsksOn` holds** (`AsksEnabled ?? !Application.isBatchMode`, offers §B). It is not the talk-spot gate (`RE/EpisodeDirector.TalkSpots.cs:65`), which keys on reduced motion only because a talk spot moves the player. Under reduced motion a story walk-up still comes, and the camera does not move.
- While `AsksOn` holds, a speaker-bearing Approach beat that the story branch yields (§D1's test) stays **out of `CurrentPull`**. The check sits in `CurrentPull`'s loop, next to the `declinedPulls` skip (`RE/EpisodeDirector.Story.cs:407-411`), before `PullFor`. `PullFor` returns `PlayPull` before its surface switch (`:420-421`), and `PlayPull` turns every play beat into the generic play card ("Take it on", "… · A PLAY · UNTIL …", taken on in one press by `TakePlay`, `:460-497`). So from S6 `PlayPull` returns null for a beat with a `speaker`: a patron's offer and report fall through to the Approach case, whose "Hear them out" opens the scene card with the beat's own options and labels ("I'll keep my eyes open", "Not my business", "Tip them off"). Plays without a speaker keep their play card.
- `TickStoryPull` recomputes only when `projected`, `free`, the walk-in or `declinedPulls.Count` changes (`:511-515`). The exclusion reads `AsksOn`, the phase, the active count, the beat, and the speaker's faith and cold shoulder. All but `AsksOn` are pure readings of `projected`, which the key already holds, so only `AsksOn` joins that cache key. Setting a walk-up aside does not change the Pull, because the exclusion already keeps the beat out wherever its walk-up can come, so `askSetAside` needs no key.
- When its walk-up is set aside, it stays out of the Pull through the exclusion above, not through `declinedPulls`. If it is still open after the phase changes, it can walk up again. Where the queue cannot present it (below), it is a Pull.
- It stays answerable until its `closesAnchor`, after which its lapse option applies. Most lapses are mild (`Move` −2 at most): a first-talk or power chapter's lapse ends `end:declined` (B2), and a Between-us offer lapses as declined (§E). Two kinds are not mild. The acquaintance verdict's lapses end on its own endings (B4): node 1's on ending 14, which writes the forced bane's writers when a bane was forced; `where-boons`' on ending 8; `where-cold`'s on ending 11 (`Move` −5); and a forced-bane card's on the bane itself. A patron report's lapse, `no-word`, writes nothing itself, but `DecidePlays` then decides the ask Lost (`story:let-down`, −5, §E1). It stays answerable in these places:
  - on the episode screen, as every open beat is today (`PendingStoryBeats`, `RE/EpisodeDirector.Story.cs:30-37`, called at `RE/EpisodeDirector.cs:1642`), including the free-time board and the nomination step (`RE/EpisodeDirector.FreeTimeBoard.cs:208-237`; `RE/EpisodeDirector.NominationScreen.cs:67-80`). The exclusion and `declinedPulls` remove a beat from the Pull only (`RE/EpisodeDirector.Story.cs:330-333,407-411`);
  - (new) raised in the speaker's conversation (below), but only from the next phase. A set-aside beat is in `askSetAside`, and the Raised pick skips it until the phase changes (offers §C.2: "`IsCurrent`, and not in `askSetAside`"). Most beats close at the next anchor, at or before the end of the phase they are open in, so only a beat still open in a later phase that allows walk-ups is raised: one fired at NomsSet and set aside in Nomination after the names, raised in VetoSelection, or one whose `closes` is later, such as a patron report (`closes = deadline`, §E1). Any other set-aside beat is answerable on the episode screen only.
  The notebook only reads (`RE/EpisodeDirector.Journal.cs:17`); it answers nothing.
- With `AsksOn` false (every batch run unless a test sets `director.AsksEnabled = true` after `ReloadEpisode()`, every PlayMode fixture, and every PortVerification bind, the windowed look sheet included), a speaker-bearing Approach beat is a Pull ("SOMEONE'S LOOKING FOR YOU") exactly as at origin/main (`:424-432`), and it never walks up.
- When the speaker cannot come (busy, or no route for 10 s of free, still time, or two misses), the beat opens as the scene card (`OpenSceneCard`, offers §D row 6), not as a Pull, and is set aside for the phase and counted as a presentation (§D1).
- While `AsksOn` holds, only a beat the queue cannot present stays a Pull:
  - one open in a phase where `NpcInvitation.PhaseAllows` is false (the Eviction and finale phases, `S/NpcInvitation.cs:33-35`), such as a beat that fires at EvictionEve;
  - one still open at active ≤ 3, where story walk-ups stop;
  - one whose speaker §D1's test leaves out: under a cold shoulder, or below faith 30 without a forced chapter (§D2).
  Such a beat is a Pull exactly as at origin/main (`:424-432`), and it stays answerable on the episode screen until its `closesAnchor`.
- Nobody follows you while you walk. A visit starts only after you have stood still for 1 s. A missed one is tried once more 8 s later, then comes as the card. One cancelled under a held auto-open is retried 8 s later and does not count toward the card.

**In the conversation panel**, an open Approach beat is the Story branch's raised form (§D1): talking to the speaker opens the conversation on it, once a phase, and never in a phase in which its walk-up was set aside or already opened (`askSetAside`). Its way past is Back to topics (`ConversationTopicsBackCaption`), or Escape, Start or B in person, through the Story case of `BackConversationNavigation` (§D1), not the bubble's decline. Back to topics returns to the topics wheel with the menu usable, AskVote included. It does **not** take the panel over, as a Conversation beat does (`RE/EpisodeDirector.cs:1529`).

**Words yield.** A word never shows while a Pull, an invitation bubble or a walk-up is up. A word on screen is dismissed when any of them wants the floor.

**Never.** These are inherited from `NpcInvitationHouseAvailable` (`RE/EpisodeDirector.NpcInvitations.cs:37-44`) and `PhaseAllows` (`S/NpcInvitation.cs:33-35`): competitions, ceremonies, the Eviction phase and the finale phases, the Diary Room and any panel, the front door, tour, opening, walk-out, arena or challenge, a staged scene, whoever is cast in it (`!npcMeetings.HasSceneStage`, `RE/EpisodeDirector.NpcInvitations.cs:43`; `R/House/HouseMeetingCoordinator.NpcInvitation.cs:137-138`), and a scene still waiting to stage (the offers plan's `SceneStageWaiting` yield).

Added from 14:
- never before week 1's eviction (`SS/EpisodeEngine.Story.cs:227`);
- never from a speaker under a cold shoulder;
- never from a speaker below faith 30, except with a forced chapter (§D2);
- no story walk-ups and no new asks at active ≤ 3. Deal walk-ups stay as today wherever `PhaseAllows` permits.

**When walk-ups can come:**

| Phase | Comes after |
|---|---|
| Nomination | The HoH is crowned |
| VetoSelection | The names are said |
| VetoMeeting | The veto is won |
| Campaign | The block is set |
| Social | Eviction night |

In Nomination, VetoSelection and VetoMeeting a walk-up also passes the deal invitations' conversation gate (`RE/EpisodeDirector.NpcInvitations.cs:121-124`). Under week rules, which every fresh season has (`S/ShippedRules.cs:48`), all three phases sit in a window (`S/EpisodeEngine.Week.cs:49-61`), so the gate always passes. Before a season's week rules start (an imported web save, for example), only a decider may come (`S/StrategyRules.cs:100-111`): in Nomination the HoH before nominations, in VetoSelection nobody, and in VetoMeeting the veto holder or the HoH while the veto can still be used. In Social and Campaign the gate always passes.

### D4. How often

| What | Bound |
|---|---|
| Story walk-ups | Limits of the story source, on top of the shared caps and gaps (§D0): ≤ 2 a window, ≤ 4 a week, ≤ 1 per houseguest per phase. A walk-up retried after a miss is the same walk-up and does not count again. Deal walk-ups are not counted. Every story beat still spends its existing airtime (`SS/EpisodeEngine.Story.cs:126,247-252`) |
| Words | ≤ 3 a window, ≤ 1 per houseguest per phase |
| Spacing | The offers plan's, shared by every source (offers §C.4), in free-house seconds on `askClock` in director memory (`RE/EpisodeDirector.ComesToYou.cs`): `AskGapSeconds` = 20 s after Maybe later, a Think it over, an ask's dialogue closing or a story scene card closing unanswered; `AskRetrySeconds` = 8 s after a miss; `AskerGapSeconds` = 120 s per houseguest, from when a walk-over opens their conversation. A visit cancelled without a miss sets no gap (offers §C.2 steps 3, 7 and 8: a changed session, week or phase, the house no longer free or the player out of frame, a changed load, the ask no longer current, a lost conversation gate or a changed coordinator). The exception is a lease that runs out after its auto-open came due with you still, or in a stalled frame of over 1 s: it counts no miss, but sets the record's retry and the next scan to `askClock + AskRetrySeconds` (8 s) (offers §C.2 step 8). The web's 30 s (`src/hooks/useNPCSocialEngine.ts:39`) is not used: a story-only value would change the shared constant for deal offers too |

---

## E. Between us: the quiet asks

**What an ask is.** A **play** (`SS/StoryPlays.cs:18-44`) in lane Play, at 14, with roles PATRON, SUBJECT and an optional SUBJECT2.

**The offer.**
- An Approach beat with id `offer` and `speaker = "PATRON"`, ending "Between us." The id is load-bearing: the weekly play budget counts only play beats whose `contentId` (`{template}:{beat}`, `SS/EpisodeEngine.Story.cs:707`) ends in `:offer` (`PlayOffersThisWeek`, `:209-210`). No other beat of a Between-us play may use that id.
- **It closes at the anchor after the one it fired at.** Its beat sets no `closes`, so `Fire`'s default `NextAnchor(anchor)` applies (`SS/EpisodeEngine.Story.cs:709`). It is not built with `PlayOffer` (`SS/StoryCatalog.Plays.cs:33-46`), whose `closes = deadline` (`:37`) would leave it answerable until the deadline, and which has no speaker and no third option. For an offer fired at EvictionNight the default is SocialClose, where `StoryLapse` lapses it anyway. Each `StoryLapse` runs before the phase change, so `take-it-on` is always answered while `CurrentAnchor(s)`, the anchor `AdvanceCycle` receives (`:915`), is still the firing anchor.
- **Its title stays the fixed "Between us"**, because it is saved on the cycle, logged and compared by validation (`SS/EpisodeEngine.Story.cs:579`; `SS/EpisodeEngine.Plays.cs:68`; `S/DialogueRules.cs:225`). A new optional `PlayTemplate.label` (role tokens, like `ThreadTemplate.label`, `SS/EpisodeEngine.Threads.cs:72-73`), "Between us · {PATRON}", is filled in `EpisodeEngine.Plays` as `title = play.label != null ? StoryText.FillDisplay(s, play.label, cycle.cast) : template.title` (`SS/EpisodeEngine.Plays.cs:121`).
- Its options keep fixed ids, so `TakenOn` works (`SS/EpisodeEngine.Plays.cs:16-17`):

| Option id | Caption | What it does |
|---|---|---|
| `take-it-on` | "I'll keep my eyes open" | Takes the ask on |
| `not-now` | "Not my business" | Declines |
| `tip-off` | "Tip them off" | Ends the cycle `tipped` (a new `PlayEndings.Tipped`; `IsDecided` stays false) |

**On the record a tip-off is a refusal, not a pass.** In the play branch of `ReconcileOpportunities` (`S/EpisodeEngine.Ledger.cs:232-234`), a `tipped` ending maps to `OpportunityResponse.Declined`, beside the existing `PlayEndings.Declined` case. Without that it falls through to `Ignored`, and `GameSense` (`S/GameSense.cs:211-212`) scores it Strategy −2 as "a play let pass" on the finale page and in the season report. The row's `note` keeps "tipped" and its `steps` keep the `tip-off` choice. No response word is added, so the saved vocabulary does not change.

### E1. Timing that always reaches the report

**Today's order.**
- `StoryLapse` resolves every beat that closes at an anchor before the Advance does anything else (`SS/EpisodeEngine.Story.cs:258-275`).
- `StoryAnchor` then runs `DecidePlays` (`:288`), which decides at the deadline (`SS/EpisodeEngine.Plays.cs:44`).
- Only after that does it run `FireScheduled` (`SS/EpisodeEngine.Story.cs:304`).
- So a report scheduled for the deadline anchor could never fire.

**The rules:**
- **Each ask may be offered only at anchors at least two anchors before its deadline** (the table in E2). `NextAnchor` gives the order (`:102-114`). The offer closes at the next anchor (§E, the offer).
- `take-it-on` chains to `report`. This is an Approach beat with `speaker = "PATRON"` and a new code-only flag, `BeatTemplate.followsNextAnchor = true`. Today `AdvanceCycle` writes a beat's one fixed `anchor` (`SS/EpisodeEngine.Story.cs:965-967`), and E1-E3 are offered at several anchors, so no fixed anchor is the next one for all of them. At 14 `AdvanceCycle` resolves a `followsNextAnchor` beat as `string at = RetryAnchor(s, anchor)`, where `anchor` is the one it receives (`CurrentAnchor(s)` when the player answers, `:915`), then writes `nextAnchor = at; nextWeek = LaterThisWeek(anchor, at) ? s.week : s.week + 1`. It is `RetryAnchor`, not `NextAnchor`, because NextAnchor(EvictionNight) is SocialClose, where `StoryAnchor` never runs (`:354-363`); under Reach `RetryAnchor` maps it to next week's HohCrowned. The save still holds only a known anchor (`SS/EpisodeStoryValidation.cs:124`).
- `report.ready` is true in either case:
  - you hold the ask's evidence;
  - `NextAnchor(anchor) == deadline`, so this is the last chance.
- Until then it is postponed anchor by anchor (`:359-371`).
- **A due report keeps its own slot.** Every play step needs `OpenPlayAsk(s) == null` (`SS/EpisodeEngine.Story.cs:249`, and again in `Fire` at `:613-619`). A play's offer and its steps stay open until that play's deadline (`SS/StoryCatalog.Plays.cs:36,107`), and up to `MaxPlays` = 3 plays run at once. At 14 only, `StoryAskAirtime` lets a Between-us report whose `ready` is true fire while another play's beat is open. It still respects `HouseEvents.Ceiling` and the dialogue reservation (`:240,243`). No other play step, at any rule level, gets this exemption.
- **Backstop: an ask that was never put to you is never lost.** At the ask's deadline only, `ServeQuietAsks` (below) also ends `served` (no effect, no receipt, no line) a taken-on ask whose report was never drawn: no `report` step in its path and no open report beat. "Deadline only" means the anchor where `DecidePlays` would otherwise decide it (`anchor == play.deadline`, `SS/EpisodeEngine.Plays.cs:44`). Before the deadline, a report that is scheduled or postponed (`nextAnchor` set by `followsNextAnchor` or the postponement, `SS/EpisodeEngine.Story.cs:359-371`) is never served by this rule: it is left for `FireScheduled`, which runs after `ServeQuietAsks` and `DecidePlays` at the same anchor (`:288,304`). At the deadline `StoryLapse` has already resolved any report that was drawn (`report.closes = deadline`), so this rule catches only a report that was never drawn, for example one postponed past its last chance by `HouseEvents.Ceiling` or the dialogue reservation. `CheckQuietAsks` skips asks with no resolved report, and `DecidePlays` decides Lost only an ask whose report was drawn and then lapsed through `no-word`.
- `report.closes = deadline`. An unanswered report lapses through its lapse option, `no-word`, before `DecidePlays` runs at the deadline.
- The report never needs a paid conversation. A `talkRole` beat would, because it fires only from `Social()` (`S/EpisodeEngine.cs:1328`).
- **What you knew is judged when you answer.** At 14, resolving a report option calls `SettleReport`, engine code that writes the cycle's vars (`S/StorylineState.cs:60`, written as `SS/StoryTemplates.cs:375` writes them). Evidence gathered while the report was open therefore counts:
  - `stated`: what you said;
  - `knew`: +1 if your evidence supports what you said, −1 if it contradicts it, 0 with none;
  - `truth`: the hidden fact, at that moment.

**Deciding the play:**
- `progress` reads the report's resolved option: have 2 for a named answer, 1 for the ask's neutral answer ("Couldn't tell you", or E1's, E5's or E6's own, §E2), 0 for a lapse; need 2.
- `failed` is true once the report has resolved.
- So `DecidePlays` gives Won, Part or Lost.
- `won` writes `story:came-through` (+10). At that moment the patron believes you, whatever the truth.
- `part` applies no effects (no memory is written), but the template must set a non-null `partOutcome`. `PartCounts` is `partOutcome != null` (`SS/StoryPlays.cs:43`), and without it `DecidePlays` decides a neutral answer (have 1) as Lost (`SS/EpisodeEngine.Plays.cs:45`) and writes `story:let-down`. The existing plays set it the same way (`SS/StoryCatalog.Plays.cs:348,510`). The line is fixed text that names only roles, never the truth, for example "{PATRON} heard you out, for what it was worth."
- `lost` writes `story:let-down` (−5).

**Reasons served.** A new `ServeQuietAsks(s, anchor)` runs just before `DecidePlays` at 14. It ends an ask `served`, with no effect, when its reason has gone: for example, an E5 patron taken off the block by the veto. This reason-gone check runs at every anchor; the never-drawn backstop (above) runs only at the ask's deadline. An ask whose patron or subject leaves the house ends `left-house` (`EndDepartedCycles`).

**Shared bounds:**
- one open patron ask at a time;
- patron offers share the one play offer a week under agency, because their offer beat is `offer` (`SS/EpisodeEngine.Story.cs:191-194,209-210,249`);
- `AsksASeason` = 3 below 12 seats and 5 from 12 seats;
- the same patron at most once in 2 weeks, and the same subject at most once in 2 weeks across all patrons. Both are pure readers over Between-us cycles' weeks and casts;
- no new ask to a player who is nominated this week. Open asks keep their deadlines;
- no new ask at active ≤ 3 (§D3). Open asks keep their deadlines.

**Who may ask:**
- Faith ≥ 45, or your ally (`s.Allied(player, x)` or a partner bond, as in §F). §D2's faith < 30 rule still applies: below 30 even an ally asks nothing.
- An ally whose `StoryPeople.Intent` is TestLoyalty (`SS/StoryPeople.cs:107-108`: allied, and their score of you below −10) asks with the line "Prove it." This sets the line, not a separate gate: TestLoyalty is returned only for an `s.Allied` pact-mate, whom the first bullet already admits.

### E2. The six asks

Every ask is **cast from what the patron knows or suspects, never from the truth.** A trigger may read hidden state; the text may not (`SA/21-bb-build-addendum.md:233`).

| Ask (the patron's words) | Why they want it | Your evidence (what makes the report ready) | Offered at | Deadline |
|---|---|---|---|---|
| **E1** "Keep an eye on {SUBJECT} for me." | Their grudge on SUBJECT ≥ 20 (`SS/Grudges.cs:36`); SUBJECT nominated them within 3 weeks (`PowerRow`); or SUBJECT is their Hunt target (a trigger only) | ≥ 1 `Seen` or `Overheard` row naming SUBJECT this week, or a sighted act of SUBJECT's | HohCrowned to BlockSet; EvictionNight | EvictionNight |
| **E2** "Are {SUBJECT} and {SUBJECT2} working together?" | Their own NPC listen-in memory of the two (`S/NpcSocialActions.cs:622-627`); a Hunt or Rumour beat this week by the two (actor and partner) with the patron as its `subjectId` (`NpcActState.subjectId`; kept by `Beat`, `S/EpisodeEngine.AllWeek.cs:209-224`; built at `S/NpcSocialActions.cs:446,551`; all-week rules only, `S/EpisodeEngine.AllWeek.cs:50-52,161`; cleared at the week turn, `S/EpisodeEngine.cs:325`. `RecordAct`'s courts and campaign visits have no subject, and Meet's and Eavesdrop's subjects are a pact id and the person overheard, so none of those counts); a public alumni pair (on main the pair reader is `KnownBlocs` in `S/AlumniKnownBlocs.cs`: `InLivePair` `:84@n`, `LivePairs` `:128@n`; the old `AlumniHistories.cs:106-142@h` lines now hold `AlumniRules.On`/`At`). The patron shares no pact visible to them with either | For: their pact is visible to you (`SS/StorySystems.cs:235-242`), a Listen pact sentence, or ≥ 2 scheming `Seen` rows of the pair within shelf life. Against: ≥ 2 warm or tense rows and no scheming row | HohCrowned to BlockSet; EvictionNight | EvictionNight |
| **E3** "Is {SUBJECT} straight with me?" | SUBJECT holds a deal or promise with them, or shares their pact, and the patron is Analytical or Vengeful ≥ 2, or has Intent ProbeDeception | A Told claim from SUBJECT this week, a Listen on SUBJECT, or a read hit on SUBJECT | HohCrowned, NomsSet, VetoWon | EvictionEve |
| **E4** "Is {SUBJECT} coming after me?" | SUBJECT nominated them, or voted against them as they can place it (`S/KnownBallots.cs:289`); their listen-in memory of SUBJECT; SUBJECT is their top-ranked threat (a trigger only) | A `Seen` or `Overheard` row naming SUBJECT, a Told claim or read on SUBJECT, or SUBJECT's nominations this week | EvictionNight | The next BlockSet |
| **E5** "Count the votes for me." | They are nominated (`s.nominees.Contains(PATRON)`, the block still open) and you vote (`EpisodeEngine.Voters(s)` holds you, `S/EpisodeEngine.cs:836`). This is the patron's Survive agenda (`S/NpcAgendas.cs:39-44`), but the trigger reads the block directly, not `NpcAgendas.Of`, which is null without agency (`:33`) | ≥ 2 Told or Overheard claims this week | NomsSet | EvictionEve |
| **E6** "Find out who flipped." | A Blindside or Flip tag (§F) where their ally went out, or they are the NPC HoH whose target survived (`SS/EpisodeEngine.StoryHooks.cs:93-117`) | A claim this week judged Lied, or `ProvenFor(s, week, player)` placing a voter | EvictionNight | The next EvictionEve |

**How you get the evidence.** Mostly by walking the house (§I). Free reads, paid intel and chapters count too. The listen-in block in decision windows (`S/StrategyRules.cs:124-146`) still holds.

**The report options are the same whatever the truth.** The captions are fixed constants, names are drawn around them, and `showIf` reads only your own evidence. Every non-neutral answer is always shown in both polarities.

| Ask | Answers (all always shown unless noted) | Neutral |
|---|---|---|
| E1 | "Tell them what you saw" (shown when you hold evidence on SUBJECT); "Point them elsewhere" (pick) | "Say you saw nothing" |
| E2 | "Yes, they're together"; "Doesn't look like it" | "Couldn't tell you" |
| E3 | "They're straight with you"; "I'd watch them" | "Couldn't tell you" |
| E4 | "They're after you"; "You're fine" | "Couldn't tell you" |
| E5 | "Tell them they're safe"; "Tell them they're in trouble" | "Say it's close" |
| E6 | "Name them" (one pick, over anyone who voted) | "Say you don't know" |

**Visible effects at answer time are the same across truths.** Anything that depends on the truth waits for `CheckQuietAsks` and writes no line to the player that names it.
- "Yes, they're together": the patron's view of each of the two −5 through `View`.
- "Point them elsewhere" and "Name them": `Grudge` 20, cause Story, from the patron against the pick, through `Grudges.Add` (`SS/Grudges.cs:58`), never `Change`. The same pick is refused for 3 weeks, by a pure reader over resolved reports.
- Tip-off (at the offer): the subject gets `story:tipped-off` (+10) and a `Hook` with you as holder, and the cycle ends `tipped`. At the next anchor, `CheckQuietAsks` draws the keyed coin `tip:{cycle}` for each cycle that ended `tipped` and whose var `tip` is still 0. Its chance is 0.25 + 0.1·(subject Sociable − Honest), within 0.05-0.75. The check writes the var at once: `SetVar("tip", 1)` on a hit and `SetVar("tip", -1)` on a miss (`StoryCycle.SetVar` has no running check, `SS/StoryTemplates.cs:370-376`). On a hit the patron takes `Grudge` 40, cause Story, through `Grudges.Add`. The var is what makes the check happen once: the key draws the same number at every anchor (`SS/StoryRandom.cs:21-25`), and `Grudges.Add` stacks a repeat at half its severity (`SS/Grudges.cs:68-73`).

### E3. What follows

**`CheckQuietAsks(s, anchor)`**, engine code in `SS/EpisodeEngine.BetweenUs.cs`, runs in `StoryAnchor` after `DecidePlays`, at 14, over asks that ended in the last 3 weeks. It classifies each report from its vars:

| Class | When |
|---|---|
| honest-right | `knew` ≥ 0 and the statement is true |
| honest-wrong | `knew` = +1 and the statement is false |
| lie | `knew` = −1, or a decoy pick against your evidence |
| guess | `knew` = 0 and the statement is false |

A neutral answer or a lapse takes no class, and honest-right costs nothing.

**Found out**, for a lie, an honest-wrong answer or a guess, when what the patron comes to know contradicts what you said:

| Ask | Found out when |
|---|---|
| E2 | The patron becomes a knower of the pact you denied |
| E3, E5, E6 | `ProvenFor(patron)` places the ballot, or the E5 patron is evicted after "Tell them they're safe" |
| E4 | SUBJECT nominates the patron after "You're fine", or the patron places SUBJECT's ballot against them |
| E1 | A keyed coin `found-out:{cycle}` at 0.15, plus 0.10 for an Analytical or Intuitive patron, drawn once, at the first `CheckQuietAsks` after the ask ends. The same key returns the same draw at every later anchor, so the chance stays 0.15 or 0.25 in total. On a hit the patron finds out at the first EvictionNight at least one week after the ask ended, and no later than 2 weeks after it |

**What it costs:**

| Class | Cost once found out |
|---|---|
| lie | `Grudge` 40, cause `LieDiscovered` (`SS/StoryState.cs:491-500`); `story:steered-wrong` (−20, permanent); the line "{Patron} worked out you steered them wrong."; `Cool(s, "found:" + cycle, week + 100)` |
| honest-wrong | `Move` −3 through `ChangeKeyed`. No grudge |
| guess | `Move` −2. No grudge |

A grudge held by an evictee feeds the jury (`SA/20-story-arcs-brainstorm.md:658-667`).

**Caught asking around.** An `AskForIntel` whose resolved subject is the SUBJECT of your own open ask, when that subject is Intuitive or Analytical. The resolved subject is `subject` at `S/EpisodeEngine.cs:1411`: the nominee you aimed the question at, which `NomineeIntel.Targets` (`S/NomineeIntel.cs:13-24`) allows only for an active current nominee from VetoSelection to Campaign, or otherwise the houseguest the existing roll picked, never the one you asked. Both count, because the subject hears you were asking about them either way. The check reads the already-drawn subject and draws nothing from `Roll`. A keyed coin `ask:{cycle}:{subject}` at 0.2 then writes two things:
- `RelationshipLedger.RecordOneWay(subject → you, story:too-nosy)`, the existing receipt, the way `SS/EpisodeEngine.RoomActs.cs:110-111` records one. `TrustScore` reads it.
- A Whispered `Conversation` fact written by a new `Knowledge.AskedAround(s, cycle, playerId, patronId, subjectId)` in `SS/StorySystems.cs`, on the pattern of `BrokenWordCore` (`:172-186`), not through `Knowledge.Create`, which would set `refId` to the cycle id and add the actor and the patron as knowers (`:104-129`). The fact has `refId = "ask:" + cycle.id`, actor you and subject the patron. Its only first knower is the subject who caught you: not you, not the patron. Like `told:` facts it is one fact per refId. The rumour mill spreads it. When it reaches you, `Whisper`'s `ask:` case (§H) logs its line; when it reaches the patron, §J's "asking around" term applies.

---

## F. The house's events

**`HouseMoment.Of(s, anchor)`** in `S/HouseMoment.cs` is pure. It gives each holder and evictee exactly one class relative to the player. The classes are checked in this order, and the first match wins:

| Class | Meaning |
|---|---|
| **Self** | The player |
| **Rival** | A nemesis bond, the player's rivalry arc, the player's own score of x ≤ −25, or x nominated the player within 3 weeks. This holds even for a pact member: an ally can nominate you, because `AllyShield` (`S/StrategyRules.cs:45-46`) is a reluctance, not a bar. Same precedent as RoleTone's ladder, which checks Bitter before Friendly (`SS/RoleTone.cs:24,75`) |
| **Ally** | `s.Allied(player, x)` (`S/EpisodeState.cs:526`), or a partner bond (`Bonds.Partner`, which still holds once `Bonds.Apart` sets a bond to Apart: `SS/StorySystems.cs:66-71`, `SS/StoryState.cs:470`). **At EvictionNight, for x = this week's evictee (`PowerRow.evicteeId`), Ally reads the pact record instead**: `s.alliances.Any(a => a.active && a.members.Contains(player) && a.members.Contains(x))`, as the-accounting does (`SS/StoryCatalog.Staging.cs:119`). Under the commitment rules, which every cast-screen season plays from week 1 (`RE/PortVerification.Season.cs:157`), `Allied` is false for anyone out of the house, and the reveal sets the evictee to Jury (`S/EpisodeEngine.cs:559`) before the anchor (`:477`). The record is still active and still names the evictee there: `EndWithTheEvictee` (`:457`) ends only deals, unified commitments and final-two promises, and `NpcAlliances.Dissolve`/`Settle` run after the anchor (`:480-481`). Raw membership is read only for this week's evictee, because a pact of three or more keeps naming earlier jurors |
| **Stranger** | Anyone else |

In the rows below, a **friend** is an Ally, or a non-Rival with Mutual ≥ 25 with you (`SS/StoryPeople.cs:92`). An **enemy** is a Rival.

**What it reads:**
- the live `hohId` and `vetoHolderId` (set from the competition winner, `S/EpisodeEngine.cs:745-750`) and `nominees` (set by `Nominate`, `S/EpisodeEngine.cs:905-911`, from the player's command at `:123` and an NPC Head of Household at `:384`; the veto's replacement changes it at `:1036`);
- `PowerRow` from BlockSet;
- `BallotRow`, the claims and the `evicteeId` at EvictionNight;
- at HohCrowned and VetoWon, the committed `s.competitionScores`, which the public standings line prints (`S/EpisodeEngine.cs:772-780`).

**Head-to-head records.** At those two anchors, from 12, the engine records the runner-up of a win by the player: `Cool(s, "h2h:{week}:{kind}:{runnerUpId}", week + 3)`. It draws nothing.

**Where it runs.**
- Event arcs are started by the rules at 14, never from the pool, the way the pawn talk is (`StartThePawnTalk`, `SS/EpisodeEngine.RoleMoments.cs:233-285`). Their templates leave `startAnchors` empty (the default, `SS/StoryTemplates.cs:52`), so `PoolCandidates` (`SS/EpisodeEngine.Story.cs:403`) and `PoolNothing` (`:421-425`) never see them.
- At HohCrowned, VetoWon, BlockSet and EvictionNight, `StoryAnchor` reads `HouseMoment.Of(s, anchor)`. When a tag names an event arc that `Castable` casts and `AirtimeFor` has room for, the arc starts. When several tagged arcs cast, one starts, by `StoryRandom.Index(s, "w{week}:{anchor}:event", n)` over them in id order (the story's stream, never the `:pool` coin at `:381`). A named `EventArcChance` (default 1.0) gates each start on the keyed coin `w{week}:{anchor}:event-coin`; S9 lowers it only if the 9-ask ceiling or the decisions target fails at 14.
- The once-per rules still hold, read from `s.storylines` the way `PawnTalkHad` reads them (`the-plan` once per HoH a season; its template sets `oncePerHeadliner` false, as the pawn talk's does, so no `once:` key is written), and so does the 2-asks-a-week budget. A start takes its anchor's pool draw (the pool guard below). That is how event arcs replace other moments.
- New arcs start only at HohCrowned, VetoWon, BlockSet and EvictionNight. NomsSet carries words only.
- **An event that involves a running acquaintance's THEM becomes that thread's power chapter, never a second arc.**

**Priority at an anchor (from 14), within the 2 asks a week:**
1. A forced verdict.
2. One event arc.
3. A due chapter.
4. The pool.

`StoryAnchor` at 14 runs a forced verdict's thread pulse, then the event-arc start above, before its running-cycles loop. `TryStartFromPool` (`:375`) starts nothing at an anchor where a chapter or an event arc already started. Event arcs are never pool candidates, so an anchor where none started is never drawn a second time by the pool.

| Event (tag · anchor · window) | Who comes to you | Arc or ask | What is in reach; how answers change | Builds on |
|---|---|---|---|---|
| **You win HoH** (`YouHoh` · HohCrowned · Nomination) | The first pitch walks over (the offers plan's Pitch source, its slice 4: under the role moments, the first pitch waiting in `s.replyCards`, which `HoldTheBoard` has already stored in `BoardPicks` order, never a fresh `BoardPicks` call), and the pitch board is unchanged. An acquaintance with no pitch this week asks "Am I safe?" | `acq-am-i-safe` | Promising safety writes `Promise` Safety. Putting THEM up after it forces the verdict toward ending 10. Non-allies' stake +1 (§J) | `S/HoHPitches.cs:14-39,145-154`; the invite list (`SS/StoryCatalog.Staging.cs:433-438`) |
| **Ally wins HoH** (`AllyHoh`) | The HoH: "Got a minute? Come up." | `the-plan`, once per HoH a season. In a chip season the HoH's word is the window's plea (StrategyRules), so the two never stack. "Put them up" and "Keep them safe" show only while `!StrategyRules.AlreadyAsked(s, HOH)`, by `showIf`, as stay-off-the-block does (`SS/StoryCatalog.Plays.cs:224-236`); a cast-time check would do nothing, because the arc casts as Nomination opens. Once either is answered, `CanLobby` refuses Target and Spare to that HoH for the rest of the phase with AlreadyAsked's text ("{HOH} has already heard you out."), a pure reader over the resolved beat at 14. No lobby row is written: a row needs a known approach and response (`S/EpisodeValidation.cs:388-394`), and the ledger books each as a Lobby opportunity (`S/EpisodeEngine.Ledger.cs:175-188`). "Put them up" picks from `EpisodeEngine.NominationCandidates(s)` less you and the HoH, the Target plea's subject rule (`S/StrategyRules.cs:218-222`). It writes no `View`: it adds −12 for this week to `StoryConsumers.NominationPreference` at 14, read from the resolved beat, which clears with the week as a plea's influence does (`S/StrategyRules.cs:448-452`). The same pick is refused for 3 weeks across `the-plan` answers. "Keep them safe" (pick) gives `Promise` Safety from HOH to pick (`SS/EpisodeEngine.StoryEffects.cs:330-365`; NPC HoHs keep their own, `SS/StoryConsumers.cs:67-73`). "Your call" gives `Move` +3. It plays in dialogue seasons too: dialogue's consultations run the other way, with the player as HoH (`P/DIALOGUE-PLAN.md:158,203@h`) | A plan kept counts as a landed chapter (`acq-the-plan`'s `landed`, B2). From 14: the HoH's other allies' stake +1 toward you, on every question (§J's condition for an intel question about a nominee, asked of the HoH's ally, applies from 12) | — |
| **Rival wins HoH** (`RivalHoh`) | A warning friend with Mutual ≥ 25 with you (`SS/StoryPeople.cs:92`): "Heads up: watch yourself." Spoken through the friend's Candour | `heads-up` (Moment lane, one beat). stay-off-the-block weight ×2 (`SS/StoryCatalog.Plays.cs:151-187`) | From 14: the HoH answers you at stake 2 (§J) | the-agenda (`SS/StoryCatalog.HouseRemembers.cs:913-935`) |
| **Stranger wins HoH** | A worried friend, with a word: "Any idea who they're after?" | — | Courting as today | The Court agenda |
| **You are nominated** (`YouNominated` · NomsSet) | Allies console you with a word. The pawn talk plays as built | No new asks to you this week. Open asks keep their deadlines | Voters answer at stake 2 when their projected ballot is you | final-plea; pawn talk (`SS/EpisodeEngine.RoleMoments.cs:233-286`) |
| **Named the replacement** (`YouReplacement` · BlockSet) | Your ally, with a word. In a week with no backdoor, the HoH, who explains through Candour | `acq-why` when the HoH is a running acquaintance's THEM (the event becomes that thread's power chapter, by the rule above); otherwise `why-me` | Endings 9 and 11 in reach through B4's own conditions; 10 only with a betrayal of yours already on record. Being named does not force the verdict (B2) | the backdoor (`SS/StoryCatalog.Staging.cs:204-216`) |
| **You win the veto** (`YouVeto` · VetoWon · VetoMeeting) | In a chip season at 14 nobody walks up with a veto ask. `RolesOn` holds there (§L), so the offers plan's queue leaves out the nominees' veto asks and the Head of Household's keep-the-block ask (`RolesOn(s) && RoleOffers.IsBoardAsk(s, deal)`, `SS/RoleOffers.cs:151`; offers §C.1, §D rows 2b and 3). They stay on the veto board, which shows their tone, their stakes and that silence costs more than a no (`SS/RoleOffers.cs:66`). They do not come to you: the owner answered the offers plan's question 3 on 2026-10-30 with its default ("Use the defaults for all five offer questions"), so its slice-4 veto-board card is not built. What 14 adds: in VetoMeeting each nominee who filed a board ask may speak one `HouseWords` word before the board (under `RolesOn` only, as §N tests), for example "If you use it, remember who asked first.", toned from RoleTone and public facts only (the C4 gate). It moves nothing, offers no choice, never suggests that silence is free and never repeats the card's line. A season at 14 without `RolesOn` (a test season with the commitment rules off, as §N builds) has no veto board and no keep-the-block ask (`NpcDeals.AskToKeepTheBlock` returns at once, `S/NpcDeals.cs:437`). Its nominees' veto asks follow offers §D row 2: they come to you where the conversation gate allows (the week's windows, or a decider); otherwise they stay on the meeting rows (`VetoOffers`). None of this row's house talk plays there. With `AsksEnabled` off the asks walk up as at origin/main, with their fixed openings (`S/NpcInvitation.cs:42-49,76-78`). Keep-the-block stays as built | A nominee THEM's chapter reads the deal's verdict at BlockSet | Deals and their proposers stay `NpcDeals`' own | RoleOffers, RoleQuotes, HouseWords; offers plan §D row 2b |
| **Ally wins the veto** (`AllyVeto`) | The holder: "Should I use it?" | `the-call` (VetoWon). "Use it on them" and "Keep it" show only while `!StrategyRules.AlreadyAsked(s, holder)`; once either is answered, Save and Keep are refused to that holder for the phase, as for `the-plan`. "Use it on them" picks a nominee (Save's rule, `S/StrategyRules.cs:230-232`) and adds +10 for this week to `StoryConsumers.SavePreference` at 14; it writes no `View`. "Keep it" gives `Move` +2 | — | veto-dilemma's slot (`SS/StoryCatalog.PhaseBeats.cs:111`) |
| **Rival wins the veto** (`RivalVeto`) | A nominated friend asks you to lobby them, with a word | The existing plea | — | `S/StrategyRules.cs:192-240` |
| **Your veto saves someone** (`YouSaved` · BlockSet) | The saved person thanks you, with a word | Their chapter counts as landed (`acq-the-debt`'s `landed`, B2) | Regard record +10. Ending 5 in reach | — |
| **Someone's veto saves you** (`SavedYou`) | The holder: "I've got you." (a word) | `you-owe-me` at their next need: honour it with a VoteSave deal, or refuse for `Grudge` 30 | Their leverage | call-in-the-favour, reversed |
| **Left up** (`LeftUp`: you were nominated, and a holder who is a friend did not use the veto on you) | The holder explains, through Candour | `acq-why`, forced | Endings 9-11 in reach | quiet-tears (`SS/StoryCatalog.PhaseBeats.cs:188-222`) |
| **Drawn for the veto** | A nominated friend: "If you win, remember me." (a word) | — (a word only, in every season. Dialogue rules 4's veto question runs the other way: a nominated player asks a veto player, from the person's menu (`P/DIALOGUE-PLAN.md:157,169,202@h`). This plan adds no occasion kind (§A)) | — | — |
| **You beat them head to head** (`HeadToHead`: an `h2h:` key this week) | The runner-up, with a word. Envy ("Next time.") for Competitive or Vengeful ≥ 2; respect for Steady ≥ 2 | — | Regard's head-to-head term (§J) | The public standings line |
| **Your third win** (`Dominant`) | A Competitive or Bold rival, with a word: "Someone's getting comfortable." | — | Regard's respect term (§J): it reaches its +9 cap at your third win, for Competitive or Bold ≥ 2 houseguests you never put up | Win counters |
| **A bottom finish when they needed you** (`LetThemDown`: your public placement in the bottom two of a veto they or their pact-mate needed, and the veto missed them) | An Analytical or Competitive ally, with a word: "Did you even try?" | — | Regard's "threw it" term | `CompetitionRow` (`S/SeasonLedger.cs:94-100`); never the private `thrown` line (`S/EpisodeEngine.cs:759-761`) |
| **Ally evicted** (`AllyEvicted` · EvictionNight · Social) | The evictee's other ally, with a word: "It's you and me now." That is an Active houseguest other than you who shares an active pact record with the evictee, read raw as the Ally class reads it. If that night's `NpcDeals.Propose` (`S/EpisodeEngine.cs:483`, after the anchor at `:477`) filed you an AllianceInvite from someone allied with the evictee but not with you (`S/NpcDeals.cs:172-189`), that offer walks up instead, as the offers plan's Offer source with its own `OpeningLine`, "Can we talk about making an alliance?" (`S/NpcInvitation.cs:89`). This event makes no offer, and the night files nothing if the campaign's `Propose` (`:423`) already put one to you this week. An event opener for that offer would go inside `NpcInvitation.Opening`, gated at 14 and picked by FNV-1a over the record id (offers §C.4), only once agreed with the offers plan | E6. The evictee's acquaintance ends `parted` | From 14: those you can place as voting your ally out answer at stake 2 this week (§J) | numbers thread Broken (`SS/StoryCatalog.Threads.cs:225-231`) |
| **Enemy evicted** (`EnemyEvicted`) | Their friends go cool for a week, only if `ProvenFor` lets them place your ballot or a nemesis bond is public | — | At 14 the vote-reveal grudge keeps only ballots the holder can place. `StoryVotesRevealed` still runs at `S/EpisodeEngine.cs:558`, before `:559` makes the evictee Jury, so `s.Allied` (`S/EpisodeState.cs:526-527`) and the ×1.2 in `Grudges.Add` (`SS/Grudges.cs:65`) still see the evictee in the house. At `:558` `KnownBallots.ProvenFor` returns nothing, because `Frame.Of` (`S/KnownBallots.cs:680-684`) reads the night as pending until `s.evictionResolved` is set. So 14 adds a reveal-time read, `KnownBallots.ProvenAtReveal(s, viewerId)`: the live frame from the complete box and the tally computed at `:526`, without the evictionResolved gate, through the same `Prove` rule. For each `s.votes` row that passes the `:97` test, the grudge is added only when that read for `vote.targetId` places `vote.voterId` (`SS/EpisodeEngine.StoryHooks.cs:96-98`). The holder is a nominee and casts no ballot, so in practice that means a tie-break or a unanimous or sole vote. Below 14 nothing changes. This fixes a knowledge leak and is measured as an NPC-to-NPC change (§M) | rivalry climax Won |
| **Blindside** (any of: you voted as one of the house's voters (not the HoH's tie-break), your read named someone (`BallotRow.readBefore != null`) and that person was not evicted (`correct` false; FinaleQuestions' Mistake test, `S/FinaleQuestions.cs:320`); a Told claim to you was judged a real lie; your ally, by the EvictionNight Ally read, went out after a voter told you the other name. A player who did not vote, or whose read called nobody, is tagged only from judged claims or the told-the-other-name rule) | Someone who told you the truth: "I tried to tell you." A talker brings "the real story". The liar avoids you: `Cool(s, "liar:" + id, week + 2)`, written at the reveal, holds their Candour at Neutral for the rest of this week and through next week's vote. The week turns at the Social close (`S/EpisodeEngine.cs:309`), and `AskVote` is open only in Campaign and Eviction (`S/VoteRead.cs:71-73`) | `tried-to-tell-you` (Moment lane); E6 | The liar's acquaintance counts the lie. Ending 3 in reach for the truth-teller | `SettleVoteRead` (`S/EpisodeEngine.VoteRead.cs:176-199`) |
| **Flip** (an NPC voted against an ally of theirs, as you can place it; when that ally is the evictee the pact is read raw, `a.active && a.members.Contains(voter) && a.members.Contains(evictee)`, as `Defection` does at `SS/StoryCatalog.Staging.cs:113-123`, never `s.Allied`) | The flipper claims credit if it helped you, through Candour | `the-credit` (Moment lane) | Checked only through `ProvenFor` | the-flip, the-accounting (`SS/StoryCatalog.Staging.cs:113-141`) |
| **Jury talk** (active ≤ 6) | Allies, about the jury, with a word | — | Steady ≥ 2 speakers move .10 from honest to neutral | `JuryStory` |
| **Final four** (active = 4) | The existing FinalTwo offer walks up, from whoever `NpcDeals` picks. Regard never chooses | Every acquaintance verdict is forced (§B2). Asks run as in any other week; new ones stop at active ≤ 3 (§D3). Open asks keep their deadlines | Stake 2 for everyone | KeepTheFinalThree (`S/EpisodeEngine.cs:337`) |
| **Final three** (active ≤ 3) | No story walk-ups and no asks (a 14 rule). Deal walk-ups as `PhaseAllows` permits | — | — | `P/ENDGAME-PLAN.md:12-40` |
| **Have-Nots** | A Have-Not ally gripes (a word). Talkers pass nothing about them: the Have-Nots are the Head of Household competition's lowest finishers, named in the house's public log (`S/HaveNots.cs:63-85`), so nobody picks them and no talker holds them as news (§H's items are votes and pacts only) | slop-week as is | — | `S/EpisodeEngine.cs:755-758` |
| **Twists** | Unity has no twist engine. The tag list leaves room for one (owner question 6) | — | — | — |

The event arcs (`heads-up`, `the-plan`, `the-call`, `why-me`, `you-owe-me`, `tried-to-tell-you`, `the-credit`) live in `SS/StoryCatalog.HouseTalk.cs` at rules 14. Every template at 11 or above casts only under `RolesOn` (`SS/EpisodeEngine.Story.cs:478`). The one 14 template begun outside `Castable`, the acquaintance thread (§B1, through `BeginThread`), checks `RolesOn` itself in `SeedAcquaintance`.

---

## G. Voices and tells

### G1. The vote answer at 12

`AskVote` (`S/EpisodeEngine.VoteRead.cs:63-92`) is unchanged byte for byte below 12. At 12:

1. The same requirements and the same `AskedThisWeek` (`:142-145`).
2. Compute `ally` (`:76`) and the rules-11 deflect test (`:78`), exactly as now.
3. **Draw `Roll(s)` exactly when rules 11 would**: not for an ally, and not on the deflect branch. Ignore its value. This keeps the main stream aligned.
4. **The stance.** The first match wins:
   1. a C6 ally (`SharesIntel`, `S/EpisodeEngine.AllyIntel.cs:92`): Honest. C6 holds at 12: an ally never deflects and never lies, and `Allegiance.CommitmentKnown` (`S/Allegiance.cs:158-178`) reads a Deflected standing as knowledge because of it;
   2. under a cold shoulder or `liar:`: Neutral;
   3. a nemesis: `Candour.Choose(s, voter, "vote", null)`, whose shift 5 (§J) moves all honest weight to dishonest on the vote, so a nemesis is never honest on the vote and a Loyal one still never lies. A nemesis is never fixed Honest, even as a confidant;
   4. a confidant: Honest;
   5. everyone else: `Candour.Choose(s, voter, "vote", null)`.
5. Honest names the projected nominee and dishonest the other one. Both write the Told claim, as now. Neutral writes the `Deflected` standing (`:81`).
6. The manner comes from `Manner.Choose` (G2).
7. **The hidden record.** `Cool(s, "said:{week}:{voter}:{stance}:{manner}", week + 1)`. Cooldown keys are free text (`SS/EpisodeEngine.Story.cs:1032-1041`). It is read only this week, by G4.
8. The line is `HouseAnswers.Line("vote", manner, voice, StoryRandom.Index(s, "w{week}:answer:vote:{voter}", 3), stated)`, where `stated` is the nominee id the stance chose in step 5, null on Neutral, whose NonAnswer and Deflect lines name nobody. As in `RoleQuotes.Named` (`SS/RoleQuotes.cs:288-301`), the line's `{NAME}` is filled when the line is made, not at display time, because the line is logged: with the stated nominee's first name, with "you" when `stated == s.playerId` (as today's reply does, `S/ConversationExchange.cs:248`), and capitalised where it opens the line. It is logged as `vote-read`: `You asked {X} where their head is at. "{line}"`.
9. `ConversationExchange.VoteQuestion` (`S/ConversationExchange.cs:231-253`) at 12 shows the quoted line as the reply. Below 12 it stays as it is, with its pins.

**With dialogue.** A season with both chips on still has one honesty model, because dialogue rules 2 never states a ballot of its own. Its vote response (`DialogueRules.VoterAnswer`, `S/DialogueRules.Rules2.cs:149-155@n`; dialogue rules 2 is on main and offered, `DialogueRules.Offered = 2`, `S/DialogueRules.cs:35@n`) repeats the voter's latest Told claim this week as `word` or `against`, copied into the conversation's statement (`S/EpisodeEngine.Dialogue.Rules2.cs:81@n`, its `VoterAnswer` call). Without such a claim its keyed coin picks a concern or a deflection, and neither names anybody (`SS/DialogueCampaignWord.cs:153-156@n`). At 12 the claim `AskVote` writes carries Candour's stance and its `said:` key, so a later conversation repeats that stance and writes no `said:` key of its own. (A week's latest Told claim can instead be an information partner's, `S/EpisodeEngine.Commitments.cs:205-224`, on the deal's own coin until §G5 gives it Candour's weights at 13. That is the deal's existing model, not a dialogue draw.) A statement exists only beside a Told claim, so `AskedThisWeek` (`S/EpisodeEngine.VoteRead.cs:142-144`) already counts it as the week's ask. This plan edits no dialogue file (§O). A shared Candour key for dialogue's concern-or-deflection coin is the dialogue track's to propose and the owner's to confirm, recorded as "delegated call, confirmed by the owner". It gates nothing here.

**The week's pressure:**
- a player who is the sitting HoH or holds the veto is lied to more by houseguests who are not their ally (§J's stake gains +1);
- a thin ballot margin (own margin < `VoteRead.LeaningMargin`) drives honest hedging (§G2's t_H).

### G2. Manners and tells

| Manner | Used for | Example (Plain voice) |
|---|---|---|
| Definite | Honest; smooth liars | "Taylor. Not losing sleep over it." |
| Hedged | A tell; honest on a thin margin | "Probably Taylor? It's early, I mean…" |
| TurnedBack | A tell | "What are you thinking? …Taylor, I guess." |
| LaidOnThick | A tell | "Taylor. A hundred percent. Swear on everything." |
| NonAnswer | Neutral (Warm, Chatty and Plain voices) | "Honestly? I keep going back and forth." |
| Deflect | Neutral (Blunt and Guarded voices) | "Ask me after." (today's line, kept; Guarded voice) |

**The formulas.** Axes come from `SS/Personality.cs:31-50`.

```
liar's tell chance   t_L = clamp(0.50 + 0.08·Honest − 0.06·Steady + 0.10·[speaker nominated], 0.15, 0.85)
honest "false tell"  t_H = clamp(0.12 + 0.25·[own margin < VoteRead.LeaningMargin] + 0.08·[Steady < 0]
                                  − 0.05·[Steady ≥ 2] + 0.10·[speaker nominated], 0.05, 0.45)
dominant tell        Bold ≥ 2 → LaidOnThick; else Steady ≥ 2 → TurnedBack; else Sociable ≥ 2 → LaidOnThick; else Hedged
a liar's tell        the dominant tell 70% of the time, another tell 30%
an honest tell       Hedged on a thin margin; otherwise any of the three tells, uniformly
honest otherwise     Definite (the Chatty voice carries the chattiness; there is no Sociable default manner)
draws                StoryRandom.Unit(s, "w{w}:manner:{q}:{npc}") for whether a tell shows;
                     StoryRandom.Unit(s, "w{w}:manner-kind:{q}:{npc}") for which one
```

`VoteRead.LeaningMargin` = 10 (`S/VoteRead.cs:36`).

**Checked values:**
- A Deceptive+Strategic liar (Honest −3, Steady 2) tells at 0.15, or 0.24 when nominated (approaches, E5 and pleas at 14; a vote answer's speaker is never nominated).
- A Confrontational+Emotional liar (Honest +1, Steady −1) tells at 0.64; its dominant tell is LaidOnThick. A Loyal houseguest never lies (§J shift 6), so no liar has Honest above +1. The highest a liar who is not nominated reaches is Confrontational+Impulsive (Honest +1, Steady −2), at 0.70.
- A Sneaky+Competitive liar (Honest −1, Steady 1) tells at 0.36.
- An honest answer shows a tell at 0.07-0.45: 0.07 for a Steady ≥ 2 speaker on a clear margin, 0.45 for a Steady < 0 speaker on a thin margin (a nominated speaker in the other answers clamps to 0.45 too). The clamp's 0.05 floor is never reached.
- Off a thin margin an honest answer spreads its tells over all three, so the dominant tell stays informative. On a thin margin every honest tell is Hedged. That sharpens a TurnedBack or LaidOnThick speaker's dominant tell, but gives a Hedged speaker cover: on a thin margin Casey hedges at 0.37 when honest and 0.25 when lying, so her hedge there is no evidence of a lie.

### G3. Voices

The first match wins:

| Voice | Who |
|---|---|
| **Desperate** | An NPC nominee talking about their own fate: words, approaches, E5 and pleas. Never `AskVote`, which asks voters only (`S/EpisodeEngine.VoteRead.cs:67-68`) |
| **Blunt** | Bold ≥ 2 |
| **Warm** | Warm ≥ 2 |
| **Chatty** | Sociable ≥ 2 |
| **Guarded** | Steady ≥ 2 with Warm ≤ 0, or Introverted |
| **Plain** | Everyone else |

No voice reads the Honest axis. This replaces the five-name variety (`S/HouseDialogue.cs:750-761`) for these answers. The All-Stars use the same trait voices, in the game register.

### G4. Learning to read them

- **This week:** the notebook shows "Maya: 'Probably Taylor?' (she hedged)", read from the week's `said:` key. It never parses the log.
- **The reveal (`SettleVoteRead`, `:176-199`) at 12.**
  - Every Told claim judged Lied by today's rule (`:186-187`) keeps the reveal's memory (`:190-191`, "{liar} told me in week {w} they would vote out {X}, and voted out {Y}.") and its log line (`:192`) byte for byte. Dialogue rules 2's `CaughtLie` and `CaughtLieCopy` (now on main: `S/DialogueRules.Rules2.cs:170,533@n`) find and validate the caught lie by that memory's prefix. Only the notebook, and optionally the `ChangeKeyed` reason that replaces "lied to you about the vote" (`:189`; nothing reads that text), word it neutrally as "said {X}, voted {Y}", because a changed mind is judged Lied too.
  - A **tell** is recorded only when the `said:` stance was Dishonest and its manner was a tell: `Cool(s, "tell:" + voter + ":" + manner, week + 100)`.
  - A changed mind teaches nothing.
  - Later answers in that manner from that voter carry "Last time they sounded like this, they lied."
- **The vote-lie cost at 12** goes through `ChangeKeyed`, keyed `w{w}:vote-lie:{voter}`, not `Change` (`:189`).
- **`ReadPerson` at 12.** On the existing hit (`:113-127`), and only before the reveal (`!s.evictionResolved`, which in practice means Campaign, since `ReadPerson` is refused during Eviction, `:108`), if the target has a `said:` key this week whose stance is Honest or Dishonest (a Told claim from `AskVote`):
  - A keyed coin `w{w}:read-claim:{npc}` is drawn from the story stream, never the main one.
  - It shows "Something about what they told you doesn't sit right." with probability 0.6 when the `said:` stance was Dishonest, and 0.2 when it was Honest. Otherwise it shows "What they told you seems to hold."
  - It never reads `ProjectBallot` and never states a ballot.
  - Together with tells it is strong evidence, but never certainty.
  - There is no addendum and no coin in the post-eviction Social window of the same week, because `SettleVoteRead` has already judged every claim, or for an information partner's reading (`PassTheReadings`, `S/EpisodeEngine.Commitments.cs:205-224`), which writes no `said:` key and is passed only as Campaign closes (`S/EpisodeEngine.cs:445`).
- **Into Between us:** E3 can be done by a claim plus a tell or a read.

### G5. Other answers at 13 (Stack B)

Each keeps its main-stream draws where they are and adds keyed coins only.

| Answer | What changes at 13 |
|---|---|
| `AskForIntel` (`S/EpisodeEngine.cs:1387-1422`) | The subject still comes from its roll. Candour, keyed `w{w}:candour:intel:{npc}:{subject}`, chooses the stated band. Honest states the true band (`S/EpisodeEngine.cs:1414-1416`). Dishonest states the opposite extreme: a true "is solid with" becomes "does not trust" and the reverse; a true middle band moves away from the true sign (score ≥ 0 states "does not trust", score < 0 "is solid with"), with no extra draw. Neutral states "is still working out" whatever the truth. **The Told standing stores the stated band's representative score** (+40, 0 or −40) instead of the true score (`:1412-1413`), so 0 is written only by an Honest middle band. Neutral writes no standing. The information log and the Remember memory keep today's three reading phrases word for word, so `ConversationExchange.Intel` (`S/ConversationExchange.cs:274-280`) and `NarrativeDisplay.Render` (`S/NarrativeDisplay.cs:9,44-71`) keep matching. If the spoken reply at 13 comes from §K's intel bank, `ConversationExchange.Intel` shows the bank line at 13 and keeps the legacy match below 13. Told readings can be wrong; the notebook words them "{X} told you…". The latest learned row already wins where a reader takes the last one (`S/FinalistRead.cs:297-300`) |
| The information deal's reading (`S/EpisodeEngine.Commitments.cs:205-227`) | The same two-way coin on its existing `ReadingKey` (`:227`), weighted by Candour on the vote: `honest = StoryRandom.Unit(s, ReadingKey(s, partner)) < 1 − Candour.Weights(s, partner, "vote", null).dishonest`, with the trait shifts applied. The deal obliges an answer (`:198-199`), so Neutral's share counts as honest and nobody deflects. §J's fixed values come first, in its order: a C6 ally is always honest, as `AnswerHonesty` is now, and a partner under a cold shoulder or `liar:` (fixed Neutral) answers honestly. A nemesis has no fixed value: shift 5 leaves it no honest weight on the vote, so it names the other nominee on its dishonest share and the projected nominee only on its Neutral share, and it is never fixed Honest, even as a confidant. A Loyal partner, nemesis or not, has no dishonest weight (shift 6) and is always honest, as `VoteHonesty` is today. Both answers write the Told claim, and a judged lie breaks the deal at the reveal (`BreakTheDealsOfLyingPartners`), as now. Below 13 it stays `AnswerHonesty`, byte for byte |
| A rumour told to you (`S/NpcSocialActions.cs:512-552`) | Only in the `listener.isPlayer && rules` branch (`:527-528`), after the listener (`WeightedTarget`, `:514`) and the true subject (`RankedTargets`, `:517-518`) are drawn as today. When Candour, keyed `w{w}:candour:rumour:{npc}:{true subject}`, is Dishonest, the teller names a decoy to `HeardFrom` instead of their real top threat. The decoy is the first that qualifies of: their Hunt target (`NpcAgendas.Of(s, npc).targetId`); their rival, the active houseguest they hold the highest grudge against at `AllianceLeaks.RivalGrudge` (20) or more, else the one they score lowest at `RivalScore` (−10) or below, ties by id; then the next entries of `RankedTargets`. A decoy is active and never you, the teller or the true subject. If none qualifies the teller tells the truth and the stance is recorded as Neutral. `subject.isPlayer` at `:543` and `:550` stays false, so no `turn` draw is added or removed. The act the pass returns (`:551`) keeps the true subject, and the NPC branches (`:529-541`), including the `!rules` two-way `Act`, are untouched, so the NPC world is the same at 12 and 13 |
| A pitcher's named target (`S/HoHPitches.cs:35-36`) | It may be self-serving, under Candour |
| Every approach line (14) | Its voice and manner |
| Every word (14) | Its voice (§G3, including Desperate for a nominee speaking of their own fate), never a manner. A word states nothing, so it draws no Candour stance, no manner coin, no `said:` key and no tell. Its tone is §D1's, by the C4 gate and RoleTone, which read only public facts and traits, and its line is `StoryRandom.Index(s, "word:{week}:{phase}:{npc}", 3)` |

---

## H. Talkers (13)

**Who talks.** `Talkers.Is(s, npc)` in `S/Talkers.cs` is pure. A talker has Sociable − Honest ≥ `TalkerScore` = 3 and ranks among the top ⌈active NPCs / 4⌉ by that value, with ties broken by id. The profile shows "Talks" once a talker has told you something. The existing mill already favours them (`SS/StorySystems.cs:349`).

**`Talkers.Pass(s, anchor)`** runs at HohCrowned, BlockSet and EvictionNight, after `StorySystemsAt`.
- For each talker in id order, a keyed coin `w{w}:{anchor}:talk:{teller}` at `TalkChance` = 0.5 lets them pass you one item.
- A talker whose score of you is ≤ −25 tells you nothing.
- **Confidants pass first, with no coin.** At each of these anchors, before the talkers, each active NPC with `Bonds.Holds(s, id, player, BondKinds.Confidant)` (`SS/StorySystems.cs:21`), in id order, may pass you one true item a week. This runs at 13 and above, inside `Talkers.Pass`.
  - The item comes from the Honest row of the table below: something they themselves know, about votes and pacts only (the bounds below).
  - It comes at the first of the week's three anchors where they hold one and their stance on `talk` is Honest. As for a talker, a confidant whose score of you is ≤ −25 passes nothing (the lead's call, 2026-10-30). That stance is §J's fixed values, the first matching row winning: Honest for a C6 ally; Neutral under a cold shoulder or `liar:`; `Candour.Choose(s, id, "talk", null)` for a nemesis; otherwise Honest, as a confidant.
  - It is logged like a talker's item (kind `talker`, "Riley told you: …"), log and memory only: never `AddKnower` and never a claim. At 14 it is offered as a word as a talker's item is (Words, below), with the confidant as its teller.
  - It writes `Cool(s, "confidant:{week}:{id}", week + 1)` and is refused while that key cools. It writes no `talk:` key, so it counts toward neither talker bound. A confidant who is also a talker may pass it and, on the coin, one talker item, but never the same item twice: at the same anchor the coin takes another Honest-row item they hold, or nothing; at a later anchor it skips an item whose line from them is among your memories this week (a guard against a repeat, not a bound).
  - It lands with `Talkers.Pass` in S4. Confidant bonds already have writers (§L), and ending 3 adds one in S5 (§B4).

| Candour | The item | What it writes |
|---|---|---|
| Honest | Something they themselves know: a whispered or known fact they hold and you don't; their own pact's war-room target (`PactPlanRow`); an act of theirs this week; **their own** leaning on the vote | **Log and memory only**: kind `talker`, "Riley told you: …". **Never `AddKnower` and never a claim.** So true and false items are alike to every reader, and the alliance cards (`S/AllianceRead.cs:577-602`) cannot be used as a lie detector. Hearsay proves nothing (`P/DIALOGUE-PLAN.md:217@h`) |
| Neutral | Nothing | — |
| Dishonest | The same kind of item, with its subject swapped to the decoy, chosen as in §G5 | Log and memory only |
| Invented (Dishonest, faith < 30) | "{X} is coming for you", where X is the decoy, chosen as in §G5: never you and **never a real alumnus** | Log and memory only |

**Named whispers.** When the rumour mill gives you a fact and its teller is a talker:
- the whisper line stays word for word (`SS/EpisodeEngine.StoryHooks.cs:396-397,407-426`), so `AllianceRead.IsWhisperLine` still matches it (`S/AllianceRead.cs:595-602`);
- a second `talker` line follows: "It was Maya who told you."
- The teller comes from a new internal overload, `Knowledge.Spread(EpisodeState state, string anchor, out List<string> tellers)`, in `SS/StorySystems.cs`.
  - It does the same WritesOn staging as today (`:318-326`): `RequireValid`, `Clone`, `RefreshObserved`, the core pass on the staged copy, `ObserveSpread` with the unchanged tuple list, the facts copied back, then `Install`. It returns the same remapped tuple list.
  - The core pass is a `SpreadCore` overload that also fills a list of teller ids, index-aligned with the tuples (the `tellerId` whose keyed coin passed). Without WritesOn the overload calls that core pass directly.
  - The public `Knowledge.Spread(state, anchor)` delegates to it and discards the tellers, so its signature and return value are unchanged.
  - `StorySystemsAt` (`SS/EpisodeEngine.StoryHooks.cs:395`) switches to the overload and reads `tellers[i]` for row i.
  - `SpreadCore` itself stays private. Calling it from outside `Knowledge` would skip the hearings staging.
- `Knowledge.Spread`'s return value and `UnifiedCommitmentHearings.ObserveSpread`'s tuple (`SS/StorySystems.cs:315-327`) are unchanged. No draw changes.

**What you tell a talker travels.** At 13, when the listener is a talker, `VentAbout` (`S/EpisodeEngine.cs:1295`), `ShareSecret` (`:1314`) and `ShareInformation` (`:1278`) write a Whispered `Conversation` fact:
- `refId = "told:" + sequence`;
- actor you. The subject is the person you talked about: `VentAbout`'s `aboutId` (`S/EpisodeEngine.cs:1523`) or `ShareInformation`'s `known.subjectId` (`:1290`). **`ShareSecret` names no third person** (`ShareSecret(s, target)`, `:1314,1916-1931`; its line is "There's something personal I want to tell you.", `S/ConversationExchange.cs:121`), **so its subject is you.** In the mill (`SS/StorySystems.cs:345-347`) such a fact spreads at 0.8 to your pact-mates, 0.6 to someone whose score of you is above 50, and 0.2 otherwise; a null subject would give 0.3 or 0.2, so tests pin the subject as you. Regard's "talked about them" term never reads a `ShareSecret` fact, because no NPC is its subject;
- the fact is built directly, never through `Knowledge.Create`, which would add the subject as a knower (`SS/StorySystems.cs:124`) and take its refId from the cycle;
- knowers you and the talker.

The keyed rumour mill spreads it with no main-stream draw. Its holders' Regard reads it (§J). It never enters `ThreatAssessment`, and it never casts a story. One existing reader has no kind filter: the emergency-meeting cast (`SS/StoryCatalog.BondsAndSecrets.cs:331-332`) takes as CALLER any NPC with grudge ≥ 60 on you who knows any non-public fact with you as actor or subject. At story rules ≥ 13 that fact test gains `f.kind != FactKinds.Conversation`, so `told:`, `listen:` and `ask:` facts never make a caller. Nothing writes a Conversation fact below 13, so rules 11's casts are unchanged.

**Conversation facts at 13 and above.**
- `refId` prefixes tell them apart: `told:`, `listen:` (§I3) and `ask:` (§E3).
- `Whisper` gets one case per prefix, for when one reaches the player:
  - `ask:` gives "Word in the house: you've been asking around for {SUBJECT}."
  - `listen:` gives "Word in the house: you were caught listening in."
  - `told:` facts already have you as a knower, so they are never whispered to you.
  - An `ask:` or `listen:` fact never has you as a knower when it is written, so the mill can bring it back to you (a knower is never a listener, `SS/StorySystems.cs:342`).
- `MakeRoom` drops Conversation facts first (`SS/StorySystems.cs:209-223`; the drop order is chosen at `:214-220`). Nothing that has to last depends on one: ending 12 reads the path instead.

**Words (14).** An unseen passed item is also offered as a word when its teller is near: "Did you hear…?"

**Bounds:**
- ≤ 1 item per talker a week, and ≤ 2 talker items a week in all. A confidant's item counts toward neither (above). When a talker passes an item, the engine writes `Cool(s, "talk:{week}:{teller}", week + 1)`. The per-talker check is `Cooling` on that key; the house-wide count is the number of live keys with the prefix `talk:{week}:`, as the room invites count theirs (`SS/EpisodeEngine.RoomActs.cs:50,56,100`);
- ≤ 1 invention a week house-wide, never twice about the same X within 3 weeks. An invention writes `Cool(s, "invent:{week}", week + 1)` and `Cool(s, "invented:{X}", week + 3)`, and is refused while either key is cooling;
- none of these bounds is counted from the 256-line log (`S/EpisodeEngine.cs:2649-2653`), which reaches only 1-2 weeks at 12-16 seats (`P/ACTIONS-DEALS-ALLIANCES-PLAN.md:1341`), or from the 30-memory list (`:2566`). `Cool` and `Cooling` are private to EpisodeEngine (`SS/EpisodeEngine.Story.cs:1029-1041`), so `Talkers.Pass` runs as an engine partial or calls them through an internal wrapper;
- talkers move no NPC score directly. What they spread is the player's own `told:` facts, through the existing mill;
- votes and pacts only, never personal life. Alumni speak in the game register.

---

## I. Overheard (12): walking the house, and Hunches on the web

### I1. The command

**No new command kind.** At 12, `WitnessNpcAct` (`S/EpisodeEngine.AllWeek.cs:313-321`) sends a text that starts with `conv:` to a new `Overhear(s, c)` in `S/EpisodeEngine.Overhear.cs`:
- `conv:{sequence}` is a Notice;
- `conv:{sequence}:listen` is a Listen.

Below 12 such a text is already refused, because no act has that id (`:291-304`).

**`OverhearRefusal(s, text, a, b)`** is pure. It requires:
- story rules ≥ 12 and the player active;
- a pending conversation with that `sequence` and those two ids (`S/NpcSocialState.cs:100-108`), both active, neither the player;
- a phase in `IsEligiblePhase`: Social or Campaign (`S/NpcSocialState.cs:58`);
- for a Notice: no `Seen` row for this unordered pair and tone this week, and fewer than `NoticesAWeek` = 6 `Seen` rows this week;
- for a Listen:
  - no Listen on this `sequence` already, caught or not (`Cooling(s, "heard:{sequence}")`; "You already heard them."). Every Listen, caught or not, first writes `Cool(s, "heard:{sequence}", week + 1)`, because the coin `overhear:{sequence}` always lands the same way for one conversation (`SS/StoryRandom.cs:21-26`): a repeat could be neither guaranteed safe nor caught again for another −5;
  - fewer than `ListensAWeek` = 2 NPC-to-NPC `Overheard` or `Missed` standings this week. Every Listen writes one row: a heard Listen the pair's `Overheard` row (`S/EpisodeEngine.cs:1469`), a caught one the attempt row `Missed` (§I3). At 12 only free Listens write NPC-to-NPC rows of either source, because `ConversationIntent`'s rows (`S/EpisodeEngine.ConversationIntent.cs:41`) and the read's `Deflected` and `Missed` rows (`S/EpisodeEngine.VoteRead.cs:81,131`) all point at the player.
- One helper, `ListenedThisWeek`, is the one source of truth, used by the refusal, the director's linger trigger, the Nearby card and the scripted players. The house's watch commits a Listen only while `OverhearRefusal` is null, so lingering past `ListenHoldSeconds` commits nothing more.
- Caps are counted from ledger rows, never from the 256-line log. The one exception is the pact-sentence cap: `ListenIn` writes no ledger row (it only adds a knower), so that cap is held by the `pact-heard:` key (§I2, §L).
- Every reader that counts a Listen as evidence counts only what a heard Listen gave, never the `Missed` attempt row:
  - the `Overheard` standing in E1's and E4's "`Seen` or `Overheard` row naming SUBJECT";
  - in E3's "a Listen on SUBJECT", an `Overheard` standing or claim naming SUBJECT;
  - the `Overheard` ballot claims in E5's "≥ 2 Told or Overheard claims".
  E2's "a Listen pact sentence" and the web's "Suspected pact" marker read the sentence `ListenIn` adds to the `eavesdrop` line, the evidence match of `S/AllianceRead.cs:590-593`, and never a row: `ListenIn` writes none, and a caught Listen returns before it is called. The web's Hunches read `Seen` rows only (§I4). Regard reads no Listen (§J); a caught Listen reaches faith only through §I3's `ChangeKeyed` −5. No new ClaimSource is added; `Missed`'s doc comment (`S/SeasonLedger.cs:248-249`) widens to "a read that missed, or a Listen that was caught".

A conversation that ended before the commit is refused: "That moment has passed."

**It is free:** no tick and no main-stream draw. It takes `nextSequence` ids for its log rows, in seasons at 12 only.

### I2. The two tiers

| Tier | Director trigger | What it gives | Cap |
|---|---|---|---|
| **Notice** (passing by) | `CanWitness` held for `NoticeHoldSeconds` = 2: within 6 m of the pair, same room, clear sight (`R/House/HouseMeetingCoordinator.cs:425-437`) | `SeasonLedger.Append(s.ledger, s.ledger.standings, new StandingRow { week = s.week, fromId = conversation.firstId, toId = conversation.secondId, source = ClaimSource.Seen, score = tone })` (`S/SeasonLedger.cs:156-162`; the writer pattern of `S/EpisodeEngine.VoteRead.cs:152`), and the line "You noticed A and B {talking warmly / with their heads together / snapping at each other} in the {room}." (kind `hunch`), where {room} is `RoomWords.InSentence(NpcPairing.RoomOf(conversation.rendezvousId))` | 6 a week; 1 per pair and tone a week |
| **Listen** (lingering) | Within 3.5 m for `ListenHoldSeconds` = 5, on a strategy, alliance_talk, nominations, gossip or tension topic. The Nearby card's **"Listen in"** commits it at once (`RE/EpisodeHud.Chrome.cs:1207-1248`) | Today's eavesdrop reveal **without its draws**: the pair's true score as an `Overheard` standing, written exactly as today (`S/EpisodeEngine.cs:1465-1469`): the player is told only its band, in the line's reading, and readers band it at ±25 (`S/FinalistRead.cs:299-300`; `S/KnownOdds.cs:364`). Unlike `AskForIntel`'s Told row (§G5), no representative score is stored; in a vote week, the first speaker's true ballot as an `Overheard` claim (`S/EpisodeEngine.VoteRead.cs:158-167`); the exact-pair pact sentence (`S/EpisodeEngine.AllianceLeaks.cs:145-157`); a sighted act's clause (`S/EpisodeEngine.AllWeek.cs:341-346`) | 2 a week; ≤ 2 Overheard claims a week; ≤ 1 pact sentence a week, held by a `pact-heard:{week}` key: while `Cooling(s, "pact-heard:" + s.week)` holds, `Overhear` does not call `ListenIn` at all, so you gain no knower and the line no sentence; when `ListenIn` returns a sentence, `Overhear` writes `Cool(s, "pact-heard:" + s.week, s.week + 1)` |

**A Listen is logged exactly as today's eavesdrop**: kind `eavesdrop`, built by `EavesdropLine`. The alliance cards' evidence match (`S/AllianceRead.cs:590-593`) keeps working, and no new Listen log kind exists.

**The room comes from the rendezvous.** A saved conversation holds only its `rendezvousId` (`S/NpcSocialState.cs:100-108`). A new pure `NpcPairing.RoomOf(string rendezvousId)` beside `KnownRendezvous` (`S/NpcPairing.cs:24-27`) returns the `RoomWords` id: living-east-chat → Living, kitchen-west-chat → Kitchen, bedroom-south-chat → Bedroom, yard-south-chat → Yard, kitchen-table-chat → Kitchen, yard-lounger-chat → Yard, the rooms of `HouseInteractionAnchors.Meetings` (`R/House/HouseInteractionAnchor.cs:89-94`). The Notice line prints `RoomWords.InSentence` of that id, the way `SightingLine` prints an act's room (`S/EpisodeEngine.AllWeek.cs:329-334`). No saved field and no command field are added.

**The tone comes only from the conversation's saved topic. Nothing in a Notice reads `s.Score`.**

| Topic | Tone |
|---|---|
| bonding, casual, gossip | warm (`HunchTone.Warm` = 1) |
| strategy, alliance_talk, nominations | scheming (2) |
| tension, rivalry | tense (3) |

The topic was drawn weighted by the pair's true standing (`S/WebNpcConversations.cs:103-106`), so a hunch is honest but noisy.

**Act sightings at 12** also write a `Seen` row and share the Notice cap:
- `NpcActKinds.IsPrivate` gives scheming;
- `IsLoud` gives tense;
- anything else gives warm.

### I3. Risk

A Listen can be caught, on the keyed coin `overhear:{sequence}`:

```
p = 0.15 + 0.10·[either of the pair Intuitive or Analytical] − 0.05·[player Introverted or Sneaky]
```

p runs from 0.10 to 0.25. Paranoid is not one of the project's seventeen traits (`S/WebTraits.cs:44-63`; `S/NpcSocialActions.cs:84-86` calls it unreachable), so it is not a term.

The catcher is one of the pair: `StoryRandom.Index(s, "overhear:{sequence}:who", 2)`. Being caught writes:
- the attempt row `StandingRow{fromId = first, toId = second, source = ClaimSource.Missed, score = 0}`, written on the caught path before it returns (§I1). Never a catcher-to-player row: `ReadThisWeek` (`S/EpisodeEngine.VoteRead.cs:148`) would refuse a read of the catcher, and the read opportunity (`S/EpisodeEngine.Ledger.cs:245`) would count as taken;
- `ChangeKeyed(player, catcher, −5, key, note, "eavesdrop")`, today's event type (`S/EpisodeEngine.cs:1455-1461`);
- the existing memory "I caught you listening…";
- at 13, when the catcher is a talker, a Whispered `Conversation` fact with `refId = "listen:" + sequence`, whose only first knower is the catcher, never you.

A Notice is never caught.

### I4. The web: Hunches

**The lines.**
- A new marker class beside `SuspectedMarkerName` (`RP/RelationshipWeb.cs:41-48`).
- Dashed, with a pattern per tone that reads without colour: warm a long dash, scheming dotted with a "?" midpoint, tense a zigzag.

**Confidence** is the number of `Seen` rows for the pair and tone within `VoteRead.StandingShelfLife` = 2 (`S/VoteRead.cs:34`):

| Rows | Label |
|---|---|
| 1 | "maybe" |
| 2 | "probably" |
| 3 | "often" |

Lines fade when no row is current.

**The filter chip.** The legend has no room (`:793-803`), so a filter chip with its own caption, **"Hunches"**, toggles the lines.

**The side column:** "Your hunch: Maya and Taylor seem close (noticed in weeks 4 and 5)."

**The rules:**
- Hunches are **never filtered by `s.alliances`**, unlike `SuspectedPairs` (`S/AllianceRead.cs:637-650`), which builds its pairs from `AllianceRead.Suspected` (`:641`), which walks only `s.alliances` (`:569-576`). A missing line proves nothing.
- A hunch never upgrades to "Suspected pact" on its own.
- Hunches are only what you saw yourself.

**`Seen` stays out of real readings.**
- `VoteRead.StandingKnown` (`S/VoteRead.cs:209-210`) gains `&& row.source != ClaimSource.Seen`.
- Every other standing reader already filters by source or by the player edge: `KnownOdds.Band` through `Learned` (`S/KnownOdds.cs:361-362,481-482`), `FinalistRead` (`S/FinalistRead.cs:297-298`), `HoHPitches` (`S/HoHPitches.cs:61-62`), `HouseguestNotes` (`S/HouseguestNotes.cs:120`), `Allegiance` (`S/Allegiance.cs:170-171`), `GameSense` (`S/GameSense.cs:273`), `JuryHouseRead.LastRead` (`S/JuryHouseRead.cs:275-276`), the eviction-night read opportunity in `ReconcileOpportunities` (`S/EpisodeEngine.Ledger.cs:245`), and `AskedThisWeek` and `ReadThisWeek` (`S/EpisodeEngine.VoteRead.cs:144,148`). The ledger validator (`S/EpisodeLedgerValidation.cs:26-27`) only checks rows; §L admits `Seen` there.

### I5. The paid listen-in at 12

**Below 12** it is unchanged, with all its pins (`SocialActionParityTests.cs:27-54`; `CommitmentRulesSeasonDigests.cs:738`).

**At 12:**
- `Social()` refuses `Eavesdrop` before any action is spent (`S/EpisodeEngine.cs:1228-1238`): "Walk over and listen in."
- The captions stay word for word: "Listen in" (`RE/EpisodeHud.Chrome.cs:1208`) and "Listen in on a conversation" (`RE/EpisodeDirector.Dashboard.cs:20`).
- Their tiles walk you toward the nearest conversation you can overhear, on the talk-click route.
- The copy around them changes:
  - `RE/EpisodeDirector.FreeTimeScreen.cs:351`;
  - `RE/EpisodeDirector.FreeTimeBoard.cs:436`: the tile is built with `FreeTile` instead of `CostedTile` (below);
  - `Dashboard.cs:105`;
  - the Nearby card (`Chrome.cs:1241,1248`): "Free · stay close · they may notice";
  - the "risky" tag (`RE/EpisodeDirector.Conversation.cs:332-334`).
- `CanListenIn` (`RE/EpisodeDirector.NpcSocial.cs:55-65`) keeps its other checks. At 12 it swaps its budget check (`:64`) for "a Listen is left this week" and a pending conversation that passes `OverhearRefusal(s, "conv:{sequence}:listen", first, second) == null`, so the Nearby card (`TickNearby`, `:85-90`) never offers a press the engine refuses. Below 12 it is unchanged.
- The offers plan holds new visits, auto-open and the ask card while `NearbyOfferUp` holds, which is the Nearby card's condition (offers §C.2). So from 12, asks and walk-ups wait whenever you witness a conversation in Social or Campaign while a Listen is left, not only while an action is left. This is intended: a walk-over never cuts into a moment where Listen in is on offer.
- `ListenInNearby` (`:73-78`) at 12 commits `WitnessNpcAct` with `targetId`/`secondTargetId` = the lease's `FirstId`/`SecondId` and `text = "conv:" + sequence + ":listen"`, instead of `Eavesdrop`. `HouseMeetingLease` carries no sequence (`R/House/HouseMeetingCoordinator.cs:11-40`), so the director records `pending.sequence` beside `npcShownLease` where it sets it (`:460`) and clears both together (`:197,465,541`).
- The tiles lose their action gate at 12. The dashboard drops `left > 0` from the listen tile's condition (`RE/EpisodeDirector.Dashboard.cs:102`), and its `Foot = "Cost: 1 action"` (`:106`) becomes "Free". The free-time board's `FreeTile` (`RE/EpisodeDirector.FreeTimeBoard.cs:425-428`) reads `TileFree` and never locks, where `CostedTile` (`:419-424`) would lock it and show "Cost: 1 action" or "No actions left". The free-time screen passes a free foot to `Tile` (`RE/EpisodeDirector.FreeTimeScreen.cs:351-352`; the default at `:370` is "Cost: 1 action"). Each tile's `Choose` (`Dashboard.cs:107`, `FreeTimeBoard.cs:436`, `FreeTimeScreen.cs:352`) walks you over instead of committing `Eavesdrop`.
- The NPC eavesdrop comment (`S/NpcSocialActions.cs:597-601`) gains its 12 exception.
- The freed action goes to talk and deals.

**PortVerification.** At story rules 12 or above (a chip season or a test opt-in), the autonomy check (`RE/PortVerification.Autonomy.cs:43-62`) commits exactly one Notice, on the witnessed pair, once the caption shows (`:57-59`).
- It turns the director's own Notice watch off for the whole check, so no second Notice lands, and submits the Notice itself through `seasonDirector.Submit` (`RE/EpisodeDirector.cs:805`), keeping that command.
- Both exact gates are taught that one commit and nothing else. `ObserveAutonomyStep` (`:146-183`) applies the kept command to its detached expectation at that revision, then makes its JSON comparison (`:180-181`), so the revision, the receipt, the `Seen` row and the `hunch` line must all match. The final comparison (`:134-139`) allows exactly the Notice's records: its command id appended to `acceptedCommandIds`, its one `hunch` event, and the `nextSequence` ids it took on top of `HouseTalkRecords`. RNG, `socialActions` and memories stay equal to the baseline.
- A report flag, `noticeCommitted`, joins the gate at `:225` when the season is at 12.
- Below 12 (every season at 10 or 11: 10 today, 11 from R7 until A-ON) the Notice is refused, so the step is skipped and both gates stay exactly as they are.

---

## J. How each houseguest sees you: Regard and Candour (12)

### Regard

**`Regard.Of(s, npcId)`** in `S/Regard.cs` returns `{ faith 0..100, fear 0..40, reasons[] }`.
- It is pure and stored nowhere.
- It reads only what that houseguest can know.
- It is **never fed into `ThreatAssessment.Total`, `RankedTargets` or `NpcDeals`. In a weekly NPC turn it decides no NPC's own choice (whether they act, whom they talk to, whom they target) and moves no NPC's score** (`P/ALL-STAR-HISTORIES-PLAN.md:1719-1722@h`). Through `Candour` it only words what a turn tells the player. Its one site inside the weekly pass is §G5's rumour told to you.
- **Each term carries the rules version that introduced it, and reads as 0 below that version**, so a later stack never changes an earlier stack's answers.

```
faith = clamp( T + 0.2·V − 0.4·G + pact + bond + record + ties + comp + time + style , 0, 100 )
fear  = min(40, 8·hohWins + 6·vetoWins)            // read only by the stake
T = ThreatAssessment.TrustScore(s, player, npc)     // S/ThreatAssessment.cs:198-211
V = s.Score(npc, player)
G = Grudges.Severity(s, npc, player)                // SS/Grudges.cs:36
```

| Term (version) | Value | Source |
|---|---|---|
| pact (12) | +20 when `SharesIntel(s, npc)` | `S/EpisodeEngine.AllyIntel.cs:92` |
| bond (12) | +15 partner, confidant or ride-or-die; −25 nemesis; −10 a betrayed bond | `SS/StorySystems.cs:12-87` |
| record: saved them (12) | +10 per week your veto saved them, cap +20 | `PowerRow` |
| record: put them up (12) | −8 per week you nominated them or made them the replacement, cap −24. Each becomes −4 after 3 weeks | Past weeks: power rows with `p.hohId == player && who.nominationWeeks.Contains(p.week)`. This week: the live `s.hohId == player && who.nominationWeeks.Contains(s.week)`, because no row exists before the veto meeting and `RoleTone.YouNominatedThemBefore` (`SS/RoleTone.cs:257-259`) excludes this week by design. Each week counts once |
| record: left them up (12) | −5 per week you held the veto, did not save them, and they stayed on the final block (not as the replacement, who went up only after you used it), cap −10 | Past weeks: power rows (`vetoHolderId`, `savedId`, `replacementId`, `nominees`). This week, from the veto meeting on: this week's row (`RecordVeto`, `S/EpisodeEngine.cs:1076`, writes the holder, saved and replacement) and the live `s.nominees`, because `row.nominees` is written only at the reveal (`S/EpisodeEngine.Ledger.cs:82-86`). Each week counts once: `row.nominees` when it is non-empty, otherwise the live block when `row.week == s.week` |
| ties: heard of a broken word (12) | −8 per breach they heard of, cap −24 | `YourWord.HeardBy` (`S/YourWord.cs:99`) |
| ties: double-dealing (12) | −10 | The npc-to-player ledger edge holds a `StoryReceipts.DoubleDealt` event, the permanent receipt `EpisodeEngine.DoubleDealt` writes in its no-rival branch (`S/EpisodeEngine.AllianceLeaks.cs:106`; permanent per `S/RelationshipLedger.cs:62`). The rival branch writes the `AllianceBetrayed` grudge instead (`:104-105`), which counts through G only: a cause test is unreliable, because `Grudges.Add` overwrites a pair's `cause` (`SS/Grudges.cs:72`) and `StoryAllianceLeft` writes the same cause (`SS/EpisodeEngine.StoryHooks.cs:54`). Never `AllianceLeaks.IsCaughtOut` (`S/AllianceLeaks.cs:108`): it is the test made at the moment of learning, and it does not check whether the houseguest knows of the pact |
| ties: pact with their rival (12) | −10 when a pact of yours **visible to them** holds someone they count a rival | `AllianceLeaks.HasRival` (`:123`); `AllianceVisibleTo` |
| ties: pact with their pact-mate (12) | +5, under the same visibility rule | — |
| ties: seen with their rival (12) | −5 when they hold ≥ 2 witness memories of your room acts with that rival this season, or saw you invite that rival up this week. Matched by the acts' fixed phrasing, as the vote's memory factor matches names | `SS/EpisodeEngine.RoomActs.cs:100-124` |
| ties: seen with their ally (12) | +3, by the same rule | — |
| ties: talked about them (13) | −5 when they know a `told:` fact with them as its subject | §H |
| ties: asking around (14) | −5 when they know an `ask:` fact | §E3 |
| comp: respect (12) | +3 per HoH or veto win, cap +9, for Competitive or Bold ≥ 2, only if you never put them up | Win counters |
| comp: floater (12) | −5 for Competitive, Bold ≥ 2 or Strategic, from week 5, when you have 0 wins and have never been nominated | Counters |
| comp: took the veto they needed (12) | −5 more, cap −10, for Competitive or Vengeful ≥ 2 | Power rows for past weeks; this week, the live block from the veto meeting on, by the same rule as "left them up" |
| comp: "threw it" (12) | −8, once in the last 3 weeks, for Analytical, Intuitive or Competitive. Applies when you have ≥ 1 win, your public placement was in the bottom two of a veto they or their pact-mate needed, and the veto missed them | `CompetitionRow` (`S/SeasonLedger.cs:94-100`). Never the private `thrown` line |
| comp: head to head (12) | They were the runner-up to your win within 3 weeks: −3 for Competitive or Vengeful ≥ 2; +2 for Steady ≥ 2 | `h2h:` keys (§F) |
| time together (12) | +⌊rapport / 2⌋, cap +5: about +3 after three personal talks, since rapport rises 1-3 a conversation (`SS/Lore.cs:183-205`). Their own time with you. A balance default, measured in S9 (the lead's call, 2026-10-30) | `ContactState` (`SS/StoryState.cs:150`) |
| style (12) | +5 when your top style tally in cycles with them resonates; −5 when it grates | `Personality.Reception` |

**`reasons`** lists in words, for the notebook, only the terms whose evidence the player already has: the power and competition record; a broken word's hearers (`YourWord.HeardBy`, each one told to the player in a line); and the double-dealing line. Who knows of a pact is hidden: a leak has no knower and no line (`S/EpisodeEngine.AllianceLeaks.cs:22-36`), the gossip tells the player only of an ally who caught them out (`SS/EpisodeEngine.StoryHooks.cs:395-404`), and `RiskWord` never reads whether a pact has got out (`S/AllianceLeaks.cs:178-186`). So a pact reason ("They know you're with Taylor") is listed only when the player knows that houseguest knows: the pact's fact is Public, or it has no fact (the legacy rule in `AllianceVisibleTo`), or a line shown to the player said that houseguest learned it (the double-dealing line, or the player's own spread to them), and never for a pact that houseguest is in. It is listed on that evidence alone, whether or not the rival (−10) or pact-mate (+5) term moved faith, so it never reveals whom they count a rival or a pact of theirs the player does not know of (`KnownOdds.KnownPact`). Those two terms still count in faith unchanged.

### Candour

**`Candour.Choose(s, npc, question, subjectId)`** in `S/Candour.cs` returns Honest, Neutral or Dishonest. `Candour.Weights` is exposed for tests.

**The stake runs 0 to 2.**
- It is 2 when their projected target is you, or active ≤ 4.
- Otherwise it starts at 0 and gains +1 for each of these, up to 2:
  - The truth helps you against them. Any of:
    - their target is in a pact with you **visible to them**;
    - their target is your public bond partner;
    - the question is `intel` and its subject is their own pact-mate (an active pact both are in);
    - the question is `intel`, its subject is a nominee this week (`s.nominees`; an aimed ask names one through `NomineeIntel.Targets`, `S/NomineeIntel.cs:12`), and they are allied with the HoH.
  - fear ≥ 24 and they are not your ally.
  - You are the sitting HoH or holder and they are not your ally.
- From 14 (`StoryRules.ComesToYou`), three event conditions from §F also apply: a rival HoH's stake toward you is 2; the HoH's other allies gain +1; voters you can place as voting your ally out are at 2 that week. `Candour` checks them only under `StoryAt(s, StoryRules.ComesToYou)`, so Stack C never changes a 12 or 13 answer.

```
lean = faith − 15·stake + 5·Honest      // a double, like faith; the bands are half-open
lean ≥ 65         → honest .90  neutral .10  dishonest .00
45 ≤ lean < 65    →        .60          .30            .10
30 ≤ lean < 45    →        .35          .40            .25
15 ≤ lean < 30    →        .20          .35            .45
lean < 15         →        .10          .25            .65
```

**Trait shifts, applied in this order.** Each applies once, however many of its traits a houseguest has. Each moves up to its amount and never more than the source weight holds (moved = min(amount, source)), so no weight goes below 0 and the three always sum to 1:
1. Strategic, Analytical or Introverted move up to .10 from honest to neutral.
2. Sneaky, Deceptive or Manipulative move up to .15 from neutral to dishonest.
3. Emotional or Impulsive move up to .10 from neutral to honest.
4. At active ≤ 6, Steady ≥ 2 moves up to .10 from honest to neutral.
5. A nemesis of the player, on the `vote` question, moves all of honest to dishonest. `Candour.Choose` applies this itself when the question is `vote`, so every vote answer gets it.
6. Loyal moves all of dishonest to neutral: a Loyal houseguest never lies, they clam up. A Loyal nemesis therefore answers the vote Neutral.

**Fixed values:**

| Who (the first matching row wins, in this order) | Stance |
|---|---|
| A C6 ally | Honest |
| Under a cold shoulder or `liar:` | Neutral |
| A nemesis | No fixed value: `Candour.Choose` with shift 5 on the vote. A C6 ally who is also a nemesis stays Honest (row 1); a confidant who is also a nemesis is never fixed Honest |
| A confidant | Honest |

**The coin.** `StoryRandom.Unit(s, "w{week}:candour:{question}:{npc}:{subject or '-'}")`. The coin is a pure function of the seed and the key (`SS/StoryRandom.cs:11-14,20-25`), so the same key and the same state give the same answer, and a reload never re-rolls. The weights read live faith, so an ask repeated inside the week reuses the coin but can land in a different band. Only the vote is once a week (`AskedThisWeek`, `S/EpisodeEngine.VoteRead.cs:70,142`). `AskForIntel` is not, and each ask raises V by `Change` (`S/EpisodeEngine.cs:1396-1397`) before the stance is drawn.

**Question keys:** `vote`, `intel`, `rumour`, `pitch`, `talk`, `approach`, `where`.

### Worked examples

**Maya, a trusted ally (Loyal+Social).**
- C6 fixes Honest. Faith is 100 (T 72, V +55 giving +11, pact +20, saved +10).
- On a thin margin she shows Hedged 37% of the time: "Probably Taylor… it's close."

**Jamie, a wary stranger (Strategic+Analytical).** Axes: Honest −1, Steady 4.
- T 50 and V +5 give faith 51.
- Her target is in a pact with you that she knows of: stake 1.
- Lean = 51 − 15 − 5 = 31, giving .35 / .40 / .25. With 7 or more in the house, the Strategic shift makes it **.25 / .50 / .25**. With 5 or 6 left, her Steady 4 also triggers shift 4 and it becomes **.15 / .60 / .25**. With 4 or fewer, her stake is 2 and this example no longer applies.
- If she lies, t_L = 0.18, and her dominant tell is TurnedBack.

**Casey, a rival you burned (Sneaky+Competitive).** Axes: Bold 1, Honest −1, Steady 1.
- T 33, V −45 (−9), G 70 (−28), one put-up (−8), caught double-dealing (the `DoubleDealt` receipt, −10): faith 0, and 0 without that term too (−12 clamps to 0).
- Her target is in a pact with you that she can see (+1), and you hold the veto this week while she is not your ally (+1): stake 2. Lean = 0 − 30 − 5 = −35, giving .10 / .25 / .65.
- The Sneaky shift makes it **.10 / .10 / .80**.
- t_L = 0.36, and her dominant tell is Hedged.

### What the player sees

**"How they seem to see you"** (from 13, S3) shows **evidence, never a number or a band** (`S/KnownOdds.cs:10-16`):
- `reasons` in words: "You put them up in week 3", "Your veto saved them", "You took the veto they needed", "They know you're with Taylor" (only on the evidence rule in `reasons`);
- their record with you, from judged claims: "straight with you 2 of 3 times";
- a read's band, as today.

---

## K. Content

**The banks** live in `S/HouseAnswers.cs`, shaped like `RoleQuotes`.
- Each line is ≤ 15 words (`SS/RoleQuotes.cs:67`).
- Each slot holds exactly 3 lines.
- Slots `{NAME}`, `{OTHER}` and `{ROOM}` take the houseguests' first names and the room's name. A line shown only on screen (a word, a bubble, a card) is filled at display time, the way story text is rendered (`SS/StoryText.cs:8-14`), with `CharacterNames.FirstName` (`S/CharacterNames.cs:12-19`). A line that is logged (the `vote-read` answer, §G1) is filled when it is made, as `RoleQuotes.Named` fills `{TARGET}` (`SS/RoleQuotes.cs:287-301`), with `FinalistRead.FirstName`, which is kept for saved log text (`S/FinalistRead.cs:326-332`), and "you" for the player.
- **A bank freezes with its rules version.** Once `Offered` reaches a number, a change to that number's lines needs a new number. This is the dialogue plan's content rule (`P/DIALOGUE-PLAN.md:221@h`).

| Bank | Stack | Size |
|---|---|---|
| Vote: 5 voices × 5 manners (the four stated manners, plus the voice's one Neutral manner: Deflect for Blunt and Guarded, NonAnswer for Warm, Chatty and Plain, §G2) × 3. Desperate never answers the vote, which asks voters only (§G3) | A | 75 |
| Notice lines: 3 tones × 3 | A | 9 |
| Intel band: 3 bands × 4 manners × 5 voices × 3. Desperate never answers it: its subject is never the speaker (`S/EpisodeEngine.cs:1400-1403`; `S/NomineeIntel.cs:22`), so never their own fate (§G3) | B | 180 |
| Talker items and named-whisper lines | B | ~60 |
| Event openers, words and competition reactions: ~23 events × 3 tones × 3 | C | ~210 |
| Talk chapters: 7 chapters × ~3 nodes × ~5 options, with outcome lines | C | ~230 |
| Quiet asks: 6 asks × (offer, report, found out, tipped off) × 3 | C | ~72 |
| Notebook words: styles, tells, reasons | A-C | ~40 |
| **Total** | | **~880 short lines, phased by stack** |

**Tone.** Big Brother house speech: short, hedged, never numbers or price kinds. A line promises nothing its effects do not keep.

**Real alumni.**
- Trait voices only. No per-person catchphrases and no lines attributed to real people. The web's four named banks (`useNPCSocialEngine.ts:48-76`) are not copied.
- Game register only. No romance or personal line in any slot an alumnus can fill.
- Precedent: `StoryCatalogLintTests.EveryLoreSheetAgreesWithItsCardAndKeepsRealPeopleInTheGameRegister` (`T/StoryCatalogLintTests.cs:164-176@h`) holds every `Lore.Legacy` sheet to no romance (`:169`), game sensitivity (`:172`) and no personal facet (`:173-174`). It reads lore sheets only, so the new `LineBankLintTests` is what checks `HouseAnswers`: no personal or romance words in any slot an alumnus can fill, and banks keyed by voice (trait), never by person.

**Captions (a contract).** New captions (new strings, on new controls only):
- "The house talks back" (the chip), "What's on your mind?" and "Maybe later" (the story walk-up's bubble pair, §D0), "Hunches"
- "I'll keep my eyes open", "Not my business", "Tip them off"
- "Tell them what you saw", "Say you saw nothing", "Point them elsewhere", "Yes, they're together", "Doesn't look like it", "Couldn't tell you", "They're straight with you", "I'd watch them", "They're after you", "You're fine", "Tell them they're safe", "Say it's close", "Tell them they're in trouble", "Name them", "Say you don't know"
- "Be straight with them", "Talk numbers", "Ask what they want", "Bring up what matters to them" (one caption over its per-approach copies, whose `showIf`s let at most one show, §B2), "Tell them about your alliance", "Spend the evening with them" (the first talk's node 2, §B2)
- "Let's make it official", "Final two?", "Keep it between us", "I've got your back", "You owe me one" (ending 5), "See where it goes", "Stand your ground", "Where do we stand?" (§B4's node 1: one caption over its routes, whose `showIf`s are mutually exclusive) and "Leave it unsaid" (node 1's lapse)
- "Put them up", "Keep them safe", "Your call" (today only a status-card subtitle, `RE/EpisodeDirector.NominationScreen.cs:170`, never a control), "Use it on them"

Reused captions (already on other story cards, word for word, placed on new controls only): "Own it" (`SS/StoryCatalog.BondsAndSecrets.cs:134,347,453,661`; `SS/StoryCatalog.Ported.cs:258`; `S/HouseEventSources.cs:371`), "Let it go" (`SS/StoryCatalog.HouseRemembers.cs:559,901`), "Clear the air" (ending 7's boon, §B4; `SS/StoryCatalog.HouseRemembers.cs:335`; `SS/StoryCatalog.Plays.cs:382`), "Keep it" (`SS/StoryCatalog.BondsAndSecrets.cs:716`), "Keep it light" (on node 2 of `acq-first-talk`, §B2; already on `SS/StoryCatalog.KnowThem.cs:55` and, as a lapse, `SS/StoryCatalog.HouseRemembers.cs:953`).

**No card shows two controls with the same caption.** For catalogue beats, `StoryCatalogLintTests.EveryOptionIsWellFormed` (`T/StoryCatalogLintTests.cs:85-87`) checks this over every story option, but it flags a duplicate only when no copy has a `showIf` (`:86`). The new `LineBankLintTests` applies the same rule to line-bank cards. The rule cannot see whether copies with `showIf`s show together, or a lapse, which never has a `showIf` (`:64-66`), that shares their caption. So §B2's lore copies and §B4's node 1 keep their captions apart through their own exclusive `showIf`s, pinned in `AcquaintanceThreadTests`, and a new lint rule keeps a lapse's caption off its beat's other options (§N).

Existing captions stay word for word: "Listen in", "Listen in on a conversation", "Where's your head at on the vote?" (`RE/EpisodeHud.cs:37`), "Hear their offer", "Hear them out", "Not right now", "Take it on", "Not now".

**Text size.** Label boxes at 1.3× their font, checked by §N's label sweep at Standard and Largest. Bubble buttons are 40 × scale tall (`RE/HouseNpcInvitationBubble.cs:116-126`). Captures at Standard and Largest (`RP/TextSizes.cs:6-11@h`).

---

## L. Versioning and saves

**Three numbers, claimed with the owner (question 1).**

| Number | Constant | Stack | Slices |
|---|---|---|---|
| 12 | `StoryRules.Manners` | A | S1, S2 |
| 13 | `StoryRules.Gossip` | B | S3, S4 |
| 14 | `StoryRules.ComesToYou` | C | S5-S8 |

- 10 is `Blame`, the story session's pariah fix (`SS/StoryState.cs:286-297@n`), enrolled as `Current` today (`:312@n`); 11 is `Roles` (`:304@n`) and `Latest` (`:319@n`).
- The three constants are added by HT0 (§Q3) without moving `Latest`; `IsDefined` (`:327@n`) is a bound on `Latest`, so it admits a number only once `Latest` reaches it. `Latest` moves to each number in its stack's first slice (S1, S3, S5a). HT0 repins `RoleRulesTests.RolesIsClaimedAsElevenAndWaitsForItsEnrolment` (`T/RoleRulesTests.cs:25-36@n`, in the dotnet subset at `Tools/SimulationTests/SimulationTests.csproj:358@n`), whose `Latest == Roles` pin is at `:29@n`: it is loosened once to `Is.GreaterThanOrEqualTo(StoryRules.Roles)`, keeping its message that this is a bound. R7's switch-on repins that test's `Current` lines (`:31`, `:33`, `:34@n`) first; HT0 touches only `:29`. The exact pin moves into `HouseTalkRulesTests`: `Latest == Roles` at HT0, `Manners` at S1, `Gossip` at S3 and `ComesToYou` at S5a. The `Latest + 1` cases at `:44,50@n` and the frozen readers' own `Latest = Roles` stay as they are.
- **`Current` is `Blame` (10) today, becomes `Roles` (11) in R7's switch-on PR, then `Manners` (12) in A-ON, `Gossip` (13) in B-ON and `ComesToYou` (14) in C-ON.** No other slice edits it (§Q1, §R).
- **A version freezes the first time a shipped path can write it** (`P/DIALOGUE-PLAN.md:30@h`). So `StoryRules.Offered`, the value the chip writes, moves to a stack's number only once that whole stack has landed and its measurement (§M) has passed:
  - Stack C also waits for the offers plan's slices 1-4 (`P/OFFERS-COME-TO-YOU-PLAN.md@o` §H; slice 4 is OF-a, OF-p and OF-b, §Q3). S6 is that plan's slice 5, and that plan lands each slice before the next;
  - before then, every slice is reachable only by tests.
- **`Offered` is 0 until Stack A has landed and passed its measurement (the owner's answer to question 2(a), 2026-10-30). Until then the chip is not shown, and `ApplyFresh` ignores the flag. When shown, it is off by default (2(b)).** Overtaken (§R): there is no `Offered`; the three enrolment PRs, A-ON, B-ON and C-ON, move `Current` instead (§Q1).
- Before `Offered` moves, every chip-season test (the greedy 12-seat storyline test, Stack A's paired report, PortVerification in chip seasons) opts in by writing the literal `story.rulesVersion` (12, 13 or 14) after `ShippedRules.ApplyFresh`, as dialogue's tests write the literal 1 (`S/DialogueRules.cs:19-25@h`) and the role-moment tests write `StoryRules.Roles` until R7 (`T/RoleRulesTests.cs:163-170@n`). Only the chip's own tests read `Offered`.

**What the numbers bring with them.**
- Every number here is at least Roles, so `RolesOn` (`SS/EpisodeEngine.Story.cs:36-37`) holds in a chip season that has the rules it is built on.
- Every template at 11 or above casts only under `RolesOn` (`:478`). The one 14 template begun outside `Castable`, the acquaintance thread (§B1, through `BeginThread`), checks `RolesOn` itself in `SeedAcquaintance`.
- So house talk is built on the role moments (pitches, veto asks, the windows, C6), and a chip season plays the built role moments.
- The owner adopted "R7 waits for B9" (`P/ROLE-MOMENTS-PLAN.md:5`, which records the owner's "Adopt all" and that enrolment in shipped seasons (R7) waits for the B9 balance work; the risk list repeats it at `:698`). Dialogue call 9 declined a chip that writes story Roles, for exactly this reason (`P/DIALOGUE-PLAN.md:54@h`).
- The cost: role moments freeze in chip saves before B9 tunes them. B9's later role-moment changes would apply at story rules 11 and from the next free number (15), not to chip saves at 12-14.
- **Refresh, 2026-11-13:** B9 has merged (PR #46, 2026-11-09) and R7's switch-on is under way, so this cost is gone: after R7 every new season plays the role moments at 11, and seasons at 12-14 build on the same, measured role moments. Later role-moment tuning still keys to 15 or above (answer 1).
- A gate outside the story ladder is not available. Schema 30's field list is final, and a new field takes schema 31 (`P/ALL-STAR-HISTORIES-PLAN.md:370@h`; `S/EpisodeState.cs:252@h`).

**Validation.**
- It refuses 12, 13 and 14 below schema 30 (D-H11, `P/ALL-STAR-HISTORIES-PLAN.md:102-106@h`).
- Frozen29 refuses them already, because it keeps `Latest = Roles` (`R/Persistence/FrozenV29ReadOnly.Leaves.cs:1615-1644@h`).

**Switching it on.**
- `SeasonBuilder.Choice.HouseTalksBack`, off by default: a cast-screen choice held in memory only, like `Choice.CampaignConversations` (`S/SeasonBuilder.cs:56-63@n`), never a saved field. Not built (§R): nothing in this subsection is built.
- In `ShippedRules.ApplyFresh`, after the dialogue chip and before the alumni histories (`S/ShippedRules.cs:95,102@h`; on main the dialogue chip's line is `:99@n` and the alumni histories' `:106@n`, so the new line goes right after `:99`), it raises the story rules and never lowers them: `if (options != null && options.HouseTalksBack && StoryRules.Offered > fresh.story.rulesVersion) fresh.story.rulesVersion = StoryRules.Offered;`. `EnableStory` has already written `Current` (`:62`; `SS/EpisodeEngine.Story.cs:75@h`). While `Offered` is 0 the chip changes nothing. Without the guard it would leave story rules 0 under start week 1, which `EpisodeStoryValidation` refuses (`SS/EpisodeStoryValidation.cs:27-29@h`, "Unsupported story rules version."), and dialogue's `Prerequisites` would fail too (`S/DialogueRules.cs:500-501@h`). It draws, logs and mints nothing.
- The cast screen shows the chip only while `StoryRules.Offered >= StoryRules.Manners`. The chip lives in `RP/CastSelect.cs` beside the dialogue chip (`CampaignConversations`, `:1394,1413@n`). Not built (§R), so this plan edits no cast-screen file.

**Saved fields: none.**

**Cooldown keys written by engine code**, never by an effect. They are free text, ≤ 200 characters and 256 rows (`SS/EpisodeEngine.Story.cs:1032-1041`):

| Key | Expires | Written by |
|---|---|---|
| `said:` | week + 1 | §G1 |
| `tell:` | week + 100 | §G4 |
| `liar:` | week + 2 | §F |
| `found:` | week + 100 | §E3 |
| `h2h:` | week + 3 | §F |
| `heard:` | week + 1 | §I1, §I3 |
| `pact-heard:` | week + 1 | §I2 |
| `talk:` | week + 1 | §H |
| `confidant:` | week + 1 | §H |
| `invent:` | week + 1 | §H |
| `invented:` | week + 3 | §H |

The engine's own `once:` key also stays live all season for each template started with `oncePerHeadliner` (`SS/EpisodeEngine.Story.cs:590`). It defaults to true (`SS/StoryTemplates.cs:36`). B2's chapters set it false, and so does `the-plan`, as the pawn talk's template does (`SS/StoryCatalog.Staging.cs:414`): its once per HoH a season is a pure reader over `s.storylines`, as `PawnTalkHad` is (§F), so it adds no key.

At 16 seats (the verification stress house; no playable roster seats more than 12, `S/SeasonBuilder.cs:86-88`) the worst case is about 45 `tell:` keys; 15 `said:` keys a week; up to 30 `liar:` keys live over two weeks; at most 5 `found:` keys (one per found-out lie, `AsksASeason` = 5); 6 `h2h:` keys; ≤ 2 `heard:` and 1 `pact-heard:` key a week; ≤ 2 `talk:` keys, ≤ 15 `confidant:` keys (one per confidant), 1 `invent:` key and ≤ 3 `invented:` keys live at once. A capacity test in that house pins that none is dropped (§N).

**Pure readers instead of keys:**
- once-per-houseguest acquaintances;
- the cold shoulder;
- the learned tell from "Talk numbers";
- patron, subject and decoy rests;
- the confidence ending 12 reads;
- `the-plan` once per HoH a season, read from `s.storylines` as `PawnTalkHad` reads the pawn talk (§F), and its 3-week refusal of the same pick across its answers (§F, §M).

**New closed values**, each defined at schema ≥ 30 and refused below its rules version, in the same commit as its writer (ALL-STAR §5):

| Value | Version | Where | Rules |
|---|---|---|---|
| `ClaimSource.Seen` | 12 | `S/SeasonLedger.cs:243-256`; validated in `S/EpisodeLedgerValidation.cs:24-27` | Standings only, score 1-3. EditMode pins: ClaimSource message strings, AddedSince lists, dispatch keys |
| Log kind `hunch` | 12 | `S/EpisodeValidation.HouseTalk.cs`, on the pattern of `EpisodeValidation.WaveD.cs:21-30` | — |
| Log kind `talker` | 13 | Same file | — |
| Receipts `story:came-through` (+10, fading), `story:steered-wrong` (−20, **permanent**), `story:tipped-off` (+10, fading), `story:let-down` (−5, fading), `story:came-clean` (+6, fading) | 14 | `StoryReceipts` (`SS/StoryOdds.cs:139-185`) | `came-clean` lands with its writer, "Own it", in S5; the other four land with the asks in S7 (§O). `steered-wrong` is also added to `RelationshipLedger.Permanent` (`S/RelationshipLedger.cs:46-63@n`) by S7 itself: §14 has merged (PR #40), so the line is free |

**A value that already exists but is first written here:** `FactKinds.Conversation` (`SS/StoryState.cs:485-486`), from 13.

**Values that already exist and already have writers; these arcs add new ones** (no new closed value, no validator change):
- `GrudgeCauses.LieDiscovered`: today written at `SS/EpisodeEngine.StoryHooks.cs:85` (the lie path, `S/EpisodeEngine.cs:1511`), `SS/StoryCatalog.BondsAndSecrets.cs:308,664-672` and `SS/StoryCatalog.Staging.cs:326`;
- `story:too-nosy`: today written by the-secret-alliance (`SS/StoryCatalog.Plays.cs:118`, through `Told`, which records it at `SS/EpisodeEngine.StoryEffects.cs:118-126`);
- bond kind confidant: `SS/StoryCatalog.HouseRemembers.cs:474`, `SS/StoryCatalog.Spine.cs:206`;
- bond kind nemesis: `SS/StoryCatalog.HouseRemembers.cs:350,355`;
- bond kind showmance: `SS/StoryCatalog.BondsAndSecrets.cs:108,137`, `SS/EpisodeEngine.StoryHooks.cs:456`.

**Code-only additions, saved nowhere:** `ThreadKinds.Acquaintance`, `ThreadChapter.opener`, `BeatTemplate.speaker`, `BeatTemplate.followsNextAnchor`, `OptionTemplate.checkAt`, `PlayTemplate.label`, `PlayEndings.Tipped`, `HunchTone` and the new templates.

**Behaviour keyed to each version:**

| Version | Behaviour |
|---|---|
| 12 | `AskVote`, `ReadPerson`, `SettleVoteRead` (tells; the vote-lie through `ChangeKeyed`); the `said:` and `h2h:` records; `StandingKnown`; the `Social()` refusal of `Eavesdrop`; the "conv:" dispatch and `Seen` rows from sightings |
| 13 | `AskForIntel`'s stated band; Candour at G5's sites; `Talkers.Pass`, with the confidants' weekly item; named whispers; `told:` and `listen:` facts; Regard's talked-about-them term; Conversation-first `MakeRoom`; the whisper cases; the emergency-meeting cast ignores Conversation facts |
| 14 | `SeedAcquaintance`; the chapter lane exemption; the anchor priority order and the pool guard; walk-ups and the Pull exclusion; `ServeQuietAsks`, `SettleReport`, `CheckQuietAsks`; caught asking around and Regard's asking-around term; `HouseMoment` and the event arcs' rule-driven start; the vote-reveal grudge through the reveal-time proven read (§F); Candour's three §F stake conditions; `the-plan`'s and `the-call`'s preference terms and lobby refusal; the report's next-anchor scheduling (`followsNextAnchor` in `AdvanceCycle`) and its own slot |

**Determinism:**
- Below 12, nothing runs: `Castable` refuses templates above the season's rules (`:475`).
- Every new choice is a `StoryRandom` key.
- `AskVote` keeps its main-stream `Roll` exactly where rules 11 draws it. Overhear draws nothing from the main stream.
- NPC-to-NPC writes go only through `Grudges.Add`, `View`/`WriteScore` and `RelationshipLedger`, never `Change` (`SS/EpisodeEngine.StoryEffects.cs:11-16,77-106`).
- Beats are drawn only at engine anchors.

**Old saves** stay at their story rules. The goldens' tuple reads `story.rulesVersion=10` on main (`T/BalanceLabGoldens.cs:37-44@n`) and is re-recorded at 11 by R7's switch-on; plan-32 slices never edit it, and only the *-ON PRs record goldens at their level (§Q1).

**Metas.** Each new `.cs` file, and this plan, needs a two-line `.meta` before a commit. Simulation files enter the subset by wildcard (`Tools/SimulationTests/SimulationTests.csproj:37`). Test files are listed one by one.

---

## M. Pacing and balance

| What | Per week | Per season (8 seats) |
|---|---|---|
| Budgeted arc asks (chapters, event arcs, pool) | 2 (1 in week 1), as now (`SS/EpisodeEngine.Story.cs:126,227`), shared in §F's priority order | **≤ 9, unchanged** (`T/StoryPacingTests.cs:141`) |
| Play offers (house or patron) | 1 under agency (`:191`) | Patron asks ≤ 3 (≤ 5 from 12 seats) |
| Running plays / acquaintances (at once) | ≤ 3 / ≤ 1 | Threads ≤ 3 a season (bond, rivalry, numbers, one each, seeded by week 3; acquaintances are not counted, §B1). At most 2 acquaintances begin (≤ 3 from 12 seats), 3 chapters each, with at most one retry (B2) |
| Story walk-ups | ≤ 2 a window, ≤ 4 a week | — |
| Words (no decision) | ≤ 3 a window | 15-25 |
| Notice / Listen | ≤ 6 / ≤ 2 a week; ≤ 2 ballots and ≤ 1 pact sentence a week | — |
| Talker items; inventions; a confidant's item | ≤ 2; ≤ 1 house-wide; ≤ 1 per confidant, outside the talkers' 2 (§H) | — |

**Decisions.** Net new decisions against the same season at story rules 11:
- quiet-ask reports (≤ 3);
- the second nodes of the first talk and the verdict (B2, B4): ≤ 2 per acquaintance, or 3 when a missed first talk is tried again, so ≤ 6 at 8 seats;
- the acquaintance chapters' asks: ≤ 4 per acquaintance (its 3 chapters and at most one retry), so ≤ 8 at 8 seats, less any pool arc they push out. A first talk or power chapter may be tried twice (`attempts` = 2, B2), and every try is a new chapter cycle: one more storyline and one more ask. Only one retry fits, because the `star:` rest starts THEM's chapters at most one a week and the verdict is forced once `AcquaintanceVerdictWeeks` = 3 weeks have passed. The verdict is never retried, because its lapses end on its endings (B4).

Chapters are opened by the thread's pulse (`SS/EpisodeEngine.Threads.cs:80,151-160`), not drawn from the pool, so they add nothing to `PoolNothing` (`SS/EpisodeEngine.Story.cs:421-425`). They push out a pool arc only at their own anchor (the pool guard) or in a week whose 2 asks are already spent. That is rare: the default eight averages 3.5 arc asks a season under reach (`SA/IMPLEMENTATION-LOG.md:279`). So most chapter asks are additions. The season ceiling still holds, because chapters, event arcs and pool arcs are budgeted asks under the same weekly cap: 1 + 2 a week gives 9 at 8 seats (`SA/20-story-arcs-brainstorm.md:1014`; `T/StoryPacingTests.cs:141`). That ceiling, not the sum above, bounds the chapter asks. The reports and the second nodes sit outside it (a report is a play, and a second node chains without an anchor), so the worst case is about +15 decisions: 9 less the baseline's 3.5, plus ≤ 3 reports and ≤ 6 second nodes.

**≤ +5 decisions (≤ 2.5 minutes) a season at 8 seats is a target that is measured, paired against rules 11** (Stack A in S2, the rest in S9). It is not a sum. What genuinely replaces something:
- an event arc that starts takes its anchor's pool draw (the pool guard), and its own start is gated by `EventArcChance`; S9 reports event-arc starts a season and counts them in the added decisions;
- patron offers share the play offer.

If the measurement passes +5, cut in this order:
1. one acquaintance a season below 12 seats (up to 4 chapter asks and 3 second nodes fewer);
2. the first talk becomes one node;
3. quiet asks drop to 2 a season.

**Minutes.**
- The lab's model is 30 s a decision. At baseline the season is already about 51 minutes, over the owner's 45 (`P/ROLE-MOMENTS-PLAN.md:633`).
- So the gate is relative: a chip season is gated at ≤ 2.5 minutes of added decisions at 8 seats, as measured.
- Walk-up and word reading time is reported against B8's decision-time model when B8 exists (`:663-664`).

**Storylines and house events.**
- An acquaintance uses at most 5 storylines (the thread, its 3 chapters and at most one retry, B2) and about 6 house events, about 8 with a retry. An ask uses 1 storyline and 2 events.
- Worst case at 16 seats (the verification stress house; 12 is the largest playable cast): 20 storylines (3 acquaintance threads, their 9 chapters, a retry each and 5 asks). The chapters, their retries, the asks and §F's event arcs are cast through `Castable`, so they stop at 92 under Roles along with the pool and dialogue's ≤ 32 conversations, and compete with them for that room (`SS/EpisodeEngine.Story.cs:48,481`; `P/DIALOGUE-PLAN.md:56@h`). Only the threads, which `BeginThread` starts with no pool guard (`SS/EpisodeEngine.Threads.cs:47-50`), could pass 92, so `SeedAcquaintance` also requires fewer than `PoolStorylineCeiling` storylines (§B1); with dialogue off nothing else would keep the season under validation's 100 (`S/EpisodeValidation.cs:339`).
- House events stop at 400 and are never pruned (`S/HouseEvents.cs:33`).
- S9 reports both counts at 8 and 12 seats through `SeasonBuilder.Create`, and at 16 in `SeasonBuilder.CreateVerificationStressHouse(16, seed)`. A subset test pins a greedy 12-seat chip season below 92 storylines and 400 events, run at the highest dialogue rules the build defines. Dialogue rules 2 has merged and is what the dialogue chip offers (`S/DialogueRules.cs:35@n`), so the test runs at rules 2 with its 32-conversation budget and 92-storyline ceiling (`Rules2Conversations`, `Rules2StorylineCeiling`, `S/DialogueRules.Rules2.cs:36@n`), and §M's storyline gate is signed off on that case. If it fails, acquaintances drop to 2 from 12 seats.

**The story session's constraints must hold at 14** (`P/NPC-AGENCY-PLAN.md:21-33`). The first four are read from the explicit 1,920-season `StoryPacingTests.PacingReport` (960 regular eights, 320 All-Stars eights, 320 sixes, 320 twelves; `T/StoryPacingTests.cs:160-198`, printed at `:183-196`), run at 14 on seasons built the way a chip season is, from a new helper `StoryPacingTests.HouseTalkSeason(seed, size, roster, rules)`: `SeasonBuilder.Create`, then `ShippedRules.ApplyFresh`, then `story.rulesVersion = rules` (the role-moment tests' opt-in, `T/RoleStoryMomentsTests.cs:798-800`). It asserts `EpisodeEngine.RolesOn(s)` before the first step. The driver is `StorySeasonTests.StoryNext`, with the jury answered from `FinaleQuestions.Offered` as `RoleStoryPools.Next` does (`T/RoleStoryMomentsTests.cs:1392-1402`). Runs are paired, 11 against 14 on the same seeds, and the report prints both. `StorySeasonTests.StorySeason` stays unchanged: it enables neither commitments nor levers, so `RolesOn` is false there at any story rules and `Castable` (`SS/EpisodeEngine.Story.cs:478`) casts nothing at 11 or above (pinned by `RoleStoryMomentsTests.AStoryElevenSeasonWithoutTheRoleMomentsCastsNoneOfThem`). The targets:
- pile-ons ≤ 25%;
- pariah-free ≥ 80%;
- removals ≤ 10%;
- showmances 0.5-1.5 with fictional NPCs and 0 in All-Stars;
- at most one NPC-only pact per 3 NPCs, with nobody in two. This one is enforced in the pass (`S/NpcAlliances.cs:88-99`; `P/NPC-AGENCY-PLAN.md:27-28`) and is not in `PacingReport`. At 14 it stays pinned by `T/AllWeekCadenceTests.cs:470-476` and `T/NpcAgencyHarnessTests.cs:99-108,117-132`, and is reported by `NpcAgencyHarnessTests.AgencyReport` (`:134-169`, pacts a season at `:157`), run at 14.

At 14 the plan writes these NPC-to-NPC changes, each reported on its own against the constraints above:
- the vote-reveal grudge, which keeps only ballots the holder can place (§F). This is the one change to an existing rule;
- E2's "Yes, they're together": the patron's `View` of each of the two, −5 (`WriteScore`, `SS/EpisodeEngine.StoryEffects.cs:99-105`);
- the decoy `Grudge` 20 from the patron against the pick, for E1's "Point them elsewhere" and E6's "Name them" (`Grudges.Add`). Decoy grudges count toward the pariah measure, which reads `Grudges.HoldersAgainst(…, 40)` (`T/StoryPacingTests.cs:86-89`);
- `the-plan`'s `Promise` Safety from HOH to pick. Its "Put them up" and `the-call`'s "Use it on them" write no NPC score: they are this week's preference terms (§F).
The explicit report gives the pile-on and pariah rates at 14 with and without them, and each write has its own fixture (§N).

**Anti-ratchet:**
- one grudge write per ending, ask outcome or decoy;
- a decoy and a subject each rest for 3 and 2 weeks;
- an ask ends once its reason is served;
- `Regard` decides no NPC's choice and moves no NPC's score in a weekly NPC turn; the rumour told to you (§G5) reads it only for the name the player hears;
- talkers write no NPC score, and Conversation facts start no story (§H);
- the same `the-plan` pick is refused for 3 weeks across its answers, as a decoy is.

**The balance lab.**
- The **attentive walker** and Stack A's paired report are built in S2, in a new dotnet-only file `T/HouseTalkLab.cs` (inside `#if !UNITY_5_3_OR_NEWER`, marked Explicit like `BalanceLabAlumniReport`, and listed in `Tools/SimulationTests/SimulationTests.csproj`). They wait for neither B9 nor §14's lab work.
  - It never edits `T/BalanceLab.cs`, `T/BalanceLabNpcWorld.cs` or `T/BalanceLabPolicies.cs`, and it is kept out of `BalanceLabNpcWorld.cs`, whose source scan bans `Apply(` (`T/BalanceLabTests.cs:512-520`).
  - It runs a season loop of its own, built from the lab's internal parts: `BalanceLab.Fresh`, then `story.rulesVersion` held at 12 for the treatment arm (a harness's hold; the chip cannot, because `Offered` is 0); `BalanceLab.Seed`, so the arms pair; the lab's synthetic NPC world (`T/BalanceLabNpcWorld.cs:9-27`) with `SpendSlice` and `SpendRest`; `BalancePolicies.Create`; and `BalanceLab.Step`. Both arms run through this same loop.
  - At each Social and Campaign window the walker commits Notices and Listens on the world's pending conversations, next to `world.SpendSlice()` and outside `Step`, so they count as free actions, not decisions (`T/BalanceLab.cs:262-263@h`). Three settings: none; casual (2 Notices a week); attentive (6 Notices and 2 Listens).
- Runs are paired: 11 against 12 in S2; 11 against 13 and 14 in S9, reusing the walker.
- Stack A's measurement, in S2, comes before `Offered` moves to 12 and is what owner question 2(a) waits on.

**Gates:**
- `BallotRow.correct`, among rows with `readBefore != null`, gains ≤ 10 points; if over, `ListensAWeek` drops to 1;
- the player's win rate gains ≤ 3 points;
- the skill gap (`SA/IMPLEMENTATION-LOG.md:358-360`) grows ≤ 5 points;
- hunch tone agrees with the true band 55-80% of the time;
- decisions and minutes as above;
- `ledger.dropped` reported.

**The owner's report covers:**
- decisions and words a season;
- honest, neutral and lie rates by faith band;
- tell hit rates, true and false;
- `ReadPerson` addendum accuracy;
- hunch accuracy;
- ask outcomes and discoveries;
- the ending mix;
- acquaintances begun, completed with all three chapters, and parted, per season at 8, 12 and 16 seats, with the candidate tier (power event, patron, rapport) that seeded each and how many seasons seeded none. The stated expectation is one full acquaintance a season at 8 seats and two at 12. The last one to begin can be cut short: seeding stops once fewer than `AcquaintanceMinActive` = 5 are active (week 4's BlockSet at 8 seats, §N's calendar case), and every verdict is forced at active ≤ 4. If fewer than half the 8-seat seasons complete one, `AcquaintanceVerdictWeeks` becomes 2 below 12 seats; if fewer than half the 12-seat seasons complete two, it becomes 2 from 12 seats as well. Otherwise it stays at §P's 3;
- storyline and event counts;
- every budget above.

---

## N. Tests

### The dotnet subset

Each new test file in this subsection that touches nothing but System, NUnit, Newtonsoft and Gamesim.Simulation gets its own `<Compile>` line in `Tools/SimulationTests/SimulationTests.csproj`. New Simulation files (S/, SS/) enter by the wildcard at `:37` and are not listed again. The new Runtime partials and other Unity-bound files, the EditMode-only tests and the PlayMode tests stay out of the subset: only a file with no `using UnityEngine` belongs there. NPC-to-NPC behaviour is proved with fixtures, never season walks.

**`HouseTalkRulesTests`:**
- HT0 creates the file with the constants (12, 13, 14) and `Latest == Roles` (no `Offered`, §R); each number is defined from its stack's first slice (12 from S1, 13 from S3, 14 from S5a, which add their cases here), and until then `IsDefined` refuses it; each is refused below schema 30; 10 is `Blame` and defined (as `RoleRulesTests` pins, `:28,50@n`);
- `Latest` equals the stack's number (12, then 13, then 14), and the `RoleRulesTests` bound at `:29@n` reads `>= Roles` (loosened by HT0);
- templates never cast below their version;
- every season below the stack's level is byte-identical to main: for Stack A the rules-11 seasons against `RolesSeasonDigests` (recorded by HT0), for Stack B the rules-12 seasons against `MannersSeasonDigests` (added by A-ON), for Stack C the rules-13 seasons against `GossipSeasonDigests` (added by B-ON), each held at its level by name (§Q1, §R); and in every stack `CommitmentRulesSeasonDigests` (which holds its seasons at `Threads` by name, `T/CommitmentRulesSeasonDigests.cs:210-212@n`), `AllWeekSeasonDigests` (fresh seasons through `ShippedRules.ApplyFresh`, `T/AllWeekSeasonDigests.cs:222-229@n`, so at `Current`, which each *-ON PR's audit holds or re-records with a reason), `FreshSeasonStateDigestTests`, the frozen V25/V28/V29 tests and the old-save cells. `RolesSeasonDigests` holds the `AllWeekSeasonDigests.Fresh` seasons, sizes 6, 8, 12 and 16, fixed seeds, at `StoryRules.Roles` (the value `Current` has after R7; before R7 it is the opt-in of `T/RoleRulesTests.cs:163-170@n`). They are played by the digests' walker and busy player, every command legal and every season finishing. HT0 records it on the R7 stack's head, before any plan-32 code, and it is Explicit like the other digests. Each later slice re-runs these baselines before its stack's switch-on;
- the chip mints nothing;
- `Offered` is 0 until set. A fresh season with `HouseTalksBack` on has `story.rulesVersion == Math.Max(StoryRules.Current, StoryRules.Offered)` (`Current`, 11 after R7, while `Offered` is 0), passes validation, and has the same `ShippedRules.Fields` and digest as the same seed with the chip off while `Offered` is 0, with `CampaignConversations` set too, where the dialogue pilot's prerequisites still hold.

**`RegardTests`:**
- one fixture per term, each moving faith by its stated amount (the 12 terms in S1; talked about them in S4: the same installed `told:` fact reads 0 at 12 and −5 at 13, and a `ShareSecret` fact, whose subject is you, moves nothing; asking around in S7: the same installed `ask:` fact reads 0 at 13 and −5 at 14);
- **each term reads 0 below its version**;
- pure, with no draw;
- ignores what the houseguest cannot know: your secret ballot, an unseen pact, rapport with others, another's hidden view;
- a source pin, as `StressHouseTests.TheChipWritesThePinnedDialogueRulesNeverTheBound` does (`T/StressHouseTests.cs:330-338`): with `//` comment lines stripped, `S/ThreatAssessment.cs` (which holds `Total` and `RankedTargets`) and `S/NpcDeals.cs` never match `\bRegard\s*\.`. Neither file mentions Regard today and neither class is partial, so each file holds its whole class.

**`CandourTests`:**
- the faith band × stake × trait matrix. Every cell, for every lean band, at active ≤ 6 and > 6, and for every combination of shift traits, has three weights ≥ 0 that sum to 1 (within 1e-9, or exactly if weights are held in whole hundredths). Two cells are pinned: Strategic at lean < 15 and active ≤ 6 reads .00 / .35 / .65, and Deceptive+Emotional at lean ≥ 65 reads .90 / .00 / .10;
- the fixed values in their order: a C6 ally who is also a nemesis, or under `liar:` or a cold shoulder, answers Honest with their ballot, writes no Deflected standing and draws no `Roll`; a confidant who is also a nemesis is never fixed Honest; a nemesis on the vote has honest weight 0 in every cell, Loyal included;
- the three worked examples;
- the stake reads only visible pacts; the §F stake conditions (S8) do not apply below 14 (the same installed fixture at 13 and 14: a rival HoH asked for intel, an ally of the HoH asked the vote, a voter you can place as voting your ally out);
- the same key and the same state give the same answer, and a reload never re-rolls;
- the rumour decoy, at 12 against 13 on the same seed: a teller whose Hunt target is you names someone else; a teller whose Hunt target is their top threat names a different person; the turn stream's draw count is unchanged; a rumour to an NPC listener leaves the act and every score byte-identical;
- the information deal's reading at 13 never deflects, and its honest rate matches 1 − dishonest within ±3% across keyed occasions; a Loyal partner, nemesis or not, is always honest; a nemesis partner who is not Loyal, not a C6 ally and not under a cold shoulder or `liar:` has honest weight 0, so its honest rate is its Neutral weight, within the same ±3%; at 12 it is unchanged;
- `randomState` is identical after an `AskVote` at 12 and one at 11 of the same seed, across 200 seeds and every branch.

**`MannerTests`:**
- t_L and t_H within ±3% of the formulas over 2,000 keyed occasions;
- a Deceptive+Strategic liar who is not nominated (Honest −3, Steady 2) tells ≤ 18% of the time (t_L = 0.15, the clamp floor); a nominated one tells at t_L = 0.24, within ±3%;
- every trait cell shows both outcomes;
- the dominant tell 70% ± 3%;
- each trait cell draws its occasions half on a thin margin (own margin < `VoteRead.LeaningMargin`) and half on a wide one, and is pinned per margin. On a wide margin the dominant tell is more likely after a lie than after an honest answer in every trait cell. On a thin margin the same holds in every cell whose dominant tell is not Hedged; in a Hedged-dominant cell, honest answers hedge at t_H ± 3%.

**`VoteAnswerTests`:**
- at 11 the line and claim are byte-identical;
- at 12 neutral writes `Deflected` and every answer writes one `said:` key;
- **a changed mind writes no `tell:` key**;
- the vote-lie goes through `ChangeKeyed`;
- the `ReadPerson` addendum draws only its keyed coin, never reads `ProjectBallot`, and hits 0.6 / 0.2 ± 3%;
- once dialogue rules 2 is on the slice's base: in a week with no information deal, a rules-2 conversation started after an `AskVote` at 12 copies the told target into its statement and draws no stance of its own (one stance per voter per week).

**`OverhearTests`:**
- refusals: a stale sequence, the player's own pair, a closed phase, and the weekly caps counted from ledger rows, attempts included. A second Listen on the same sequence, after an uncaught and after a caught Listen, writes no second standing, claim or −5. A third Listen after one caught and one heard is refused. The caught row is NPC-to-NPC and leaves `ReadThisWeek` for the catcher false. A second exact-pair pact overheard in the same week gives no sentence and adds no knower, and a third pair the next week gives one again;
- free: no tick and no main-stream draw;
- a Notice reads only `topic`;
- across seeds, some scheming hunches name pairs with no pact, and some real pacts show only warm hunches;
- hunches are never filtered by alliances;
- a Listen logs today's `eavesdrop` line, so `AllianceRead.Suspected` keeps its evidence;
- the catch is keyed and falls on one of the pair;
- `Eavesdrop` is refused at 12 and unchanged at 11;
- `StandingKnown` ignores `Seen`.

**`TalkerTests`:**
- at most a quarter of the house;
- the bounds;
- **no talker item ever adds a knower or a claim**;
- inventions never name a real alumnus;
- `told:` facts spread only through the keyed mill;
- the named-whisper line is a second line, the whisper text is unchanged, and `Spread`'s return is unchanged;
- no extra draw;
- a `told:` fact from `ShareSecret` has subject = the player; one from `VentAbout` starts with exactly two knowers, and its subject is not one of them;
- the bounds hold from their keys: a fixture invents about X in week w, then fills the log past 256 lines and the player's memories past 30; X is refused at w+1 and w+2 and allowed at w+3, and the per-talker and house-wide caps hold at EvictionNight after the HohCrowned talker lines have rolled off the log;
- an NPC with grudge 60 on you who knows only a `told:` or `listen:` fact does not cast `emergency-meeting` at 13 (the Secret-fact pin at `T/StoryStagingTests.cs:65-66` stays);
- a confidant's item (§H): a fixture installs a confidant bond between you and an NPC who is not a talker. At 13 they pass one true item, kind `talker`, at the first anchor of the week where they hold an Honest-row item, and nothing at the week's later anchors. It adds no knower and no claim. In a second week, two talkers pass at HohCrowned while the confidant holds no Honest-row item; the confidant still passes at BlockSet, once they hold one, and the house-wide count of `talk:` keys stays 2. A confidant who is also a talker passes it and at most one talker item, never the same item twice. At 12 the same fixture passes nothing. A confidant whose stance on `talk` is not Honest (an installed `liar:` key, or a nemesis whose coin is not Honest) passes nothing at that anchor, and neither does a confidant whose score of you is ≤ −25.

**`AcquaintanceThreadTests`:**
- the seeding tiers and caps: tiers 1 and 3 in S5, tier 2 in S7 (§O); an 8-seat calendar case pins the last seeding anchor at week 4's BlockSet and the forced verdict at active ≤ 4;
- a season at story rules 14 without `RolesOn` (commitment rules off, for example) seeds no acquaintance at HohCrowned, BlockSet or EvictionNight and uses up no `AcquaintancesASeason` count, while the same season with them on does; no thread is seeded at 92 storylines;
- one at a time, once per houseguest; a lapsed chapter's retry casts (`oncePerHeadliner` false), and a lapsed chapter is recorded Lapsed, not Backfire;
- each reason gets its own opener (`thanks`, `talk`, `respect`, `hello`), with a `StoryContentTests` case;
- a Success on "Talk numbers" makes `LearnedTell` true and a Backfire leaves it false;
- `SeedThreads` never seeds the acquaintance;
- the bond thread is untouched and no bond-thread play is cast;
- the order, including both halves of `acq-the-debt`: THEM's veto saving you at a BlockSet before chapter 2 opens `acq-the-debt`, not the verdict, and THEM's veto saving you after chapter 2 has resolved forces the verdict;
- every ending's conditions and effects, parametrised over B4, including ending 14 with and without a forced bane, "Let it go" at standing 30 and 31, and node 1 at a raw standing of 30.5, which reads as 30 and routes to `where-cold`;
- node 1 shows exactly one "Where do we stand?": a verdict where endings 9, 10 and 13 all hold routes to `where-enemy` alone, and its lapse writes only ending 10's writers. With two known facts whose approaches both have a positive reception, one "Bring up what matters to them" shows (§B2);
- a successful "Own it" steps the bane down one level (B4). On a forced sworn enemy it ends on 9 and writes only 9's `Move` −7 and `Grudge` 30 plus `story:came-clean`: no nemesis bond and no grudge of 60. On a forced rival, a leak against you or a target on your back it ends on 11 and writes only its `Move` −5 and `story:came-clean`: no grudge and no `Spread`. The cold shoulder's 3-week reader then holds, including for a leak at standing 31-45;
- one grudge write per ending: a forced rival whose "Own it" backfires writes exactly one grudge, of 30, the same as "Stand your ground"; a target on your back ends at or above 40 whatever THEM already held;
- the showmance written at the verdict;
- no showmance with a real person or on an alumni card, with a houseguest whose sheet is closed to romance, or with someone already in a showmance;
- the lane exemption only at 14;
- chapter cast-failure rates are reported.

**`BetweenUsTests`:**
- cast from the patron's knowledge: "Doesn't look like it" is the truth in ≥ 30% of E2 casts;
- **no leak**: for every ask, the drawn option set (ids, captions, visible effects) is identical between a true and a false world with the same evidence;
- **timing**: from every allowed offer anchor, the report fires at least one anchor before the deadline, including when the offer is taken on as late as it can be (in the last phase before it closes, for example E3 fired at VetoWon and answered in VetoMeeting, and E1, E4 and E6 fired at EvictionNight and answered in Social) and when another play's beat is open across the last-chance anchor (the Secret Alliance's `dig`, which closes at EvictionNight, against an E1 or E2 ask). The take-on schedules the report at `RetryAnchor` of the answer's anchor: from EvictionNight it goes to next week's HohCrowned, never SocialClose. An offer left open lapses declined at the next anchor, never decided Lost, and an unanswered report lapses before `DecidePlays`. A second case fills the event ceiling or the dialogue reservation so the report can never be drawn, and asserts that the ask ends `served` with no `story:let-down`;
- `knew` is judged at answer time;
- found out only when knowable;
- the tip-off coin is drawn once: a hit writes exactly one grudge across the 3-week `CheckQuietAsks` scan;
- honest-wrong found out pays `Move` −3, and a found-out guess −2;
- decoy grudges only through `Grudges.Add`;
- "Yes, they're together" writes `View` −5 from the patron to each of the two, through `WriteScore`, one way (`SS/EpisodeEngine.StoryEffects.cs:99-105`): from mid-range scores the patron's score of SUBJECT and of SUBJECT2 each falls by exactly 5, their scores of the patron are unchanged, and no grudge is written. The answer goes through no `Change`, so every score with the player is unchanged. The same holds in a true and a false world with the same evidence;
- served, Won, Part and Lost decided correctly; a neutral answer is Part (the template's `partOutcome` is set);
- under agency, a week with a patron offer casts no house play offer, and a report never counts as an offer;
- a tipped ask reads response Declined and outcome na in the ledger, and `GameSense` gives it no "let pass" note (a `SeasonLedgerWriterTests` case);
- the PLAYS line starts with "Between us · " and the patron's filled name and contains no `{`;
- AcquaintanceThread's ask-driven cases (S7): seeding tier 2, a Between-us ask as the test between chapters, `cameThrough` and `foundOut`, ending 3's cameThrough branch, ending 5's "came through on their ask", ending 10's "your lie found out", and the found-out forced verdict.

**`HouseMomentTests`:** one installed fixture per row of §F, at its anchor. The tag assertions land in S5 (with the reader), the arc assertions in S8, and the approach-source assertion (the `ComesToYou` source of each walk-up) in S6, which lands last in Stack C. This includes head-to-head, blindside without a ballot, and final three. Also:
- the AllyEvicted fixture runs with the commitment rules on (`EpisodeEngine.EnableCommitments`), with a two-person pact whose other member is the evictee and a three-person pact, and asserts the `AllyEvicted` tag and the other-ally pick. A mutation that swaps `s.Allied` back in at EvictionNight must fail it. An earlier-week juror still named on a three-person pact is never tagged Ally;
- a pact ally who nominated the player last week wins HoH: the tag is `RivalHoh`, not `AllyHoh`;
- the reveal grudge at 14 under the commitment rules: a unanimous vote against the evictee that includes their ally writes the 48 grudge; a split vote where the allied ballot cannot be proved writes none; below 14 the same season writes exactly what it writes today;
- the AllyHoh fixture at 14 (S8): "Keep them safe" on a pick writes exactly one Active `Promise` Safety from HOH to the pick, expiring next week, counted through `UnifiedVoteStore.Promises(s)` (which reads the commitment store when the commitment rules are on), and writes no NPC score. "Put them up", and `the-call`'s "Use it on them" in the AllyVeto fixture, change no NPC score: each adds only this week's preference term (`StoryConsumers.NominationPreference` −12, `StoryConsumers.SavePreference` +10);
- at each anchor a tagged event with a castable event arc starts that arc when `EventArcChance` is 1, the pool starts nothing there, and no event-arc template ever appears in `PoolCandidates`;
- under `RolesOn`, no `ComesToYou` row is a role-board ask, `HouseWords` yields at most one word per asking nominee in VetoMeeting, and that word is unchanged when a hidden score is mutated.

**`ComesToYouTests`, the Story cases** (`T/ComesToYouTests.cs`, the file offers slice 1 creates):
- the Story rows, their `IsCurrent`, and their place in the shared public order (§D1, offers §C.1): `lapsesOnAdvance` first, then `week` descending, then urgency, where a story row's 3/2/1 sits below every named deal urgency (`NpcDeals.Urgency`, 100 and up) and above the default 0 of the other deal kinds, then `askerId` and `recordId`. Cases: a story beat that lapses at the next Advance comes before a same-week vote_save that does not lapse; with the same lapse flag and week, a vote_save comes before a story row and a story row before a partnership or an information_sharing offer;
- a role-board ask at 14 is still not listed;
- never in excluded phases or at active ≤ 3, never for a speaker under a cold shoulder, and never for a speaker below faith 30 unless the beat is a forced chapter (§D2): such a speaker's chapter opener or patron report gives no row and shows as the Pull ("SOMEONE'S LOOKING FOR YOU"), and the same speaker's forced verdict gives a row.

**`HouseWordsTests`:** tone reads public facts only: a mutated hidden score leaves it unchanged.

**`StoryPacingTests` at 14**, on `HouseTalkSeason` seasons (ApplyFresh, then rules 14, then `RolesOn` asserted at the start; the existing `StorySeason` cases stay as they are):
- a guard that every 14 season casts at least one template at 14 (an acquaintance chapter, an ask or an event arc) somewhere in the seed set, so a gate that silently stays shut fails the test;
- every existing cap, with the season cap still 9 at 8 seats;
- the talker caps (§H's bounds: ≤ 1 item per talker a week, ≤ 2 talker items a week in all, ≤ 1 invention a week house-wide), and ≤ 1 item per confidant a week outside them;
- a **greedy scripted player** hunting for unbounded free asks (Notice, Listen, act sightings, reports), driven on the balance lab's synthetic NPC world (`T/BalanceLabNpcWorld.cs:9-27`), the same driver as §M's walker, because `Measure`'s `StoryNext` loop never starts an NPC conversation (pending rows come only from a Start operation, `S/EpisodeNpcSocial.cs:105-142`). After each `SpendSlice` it tries every `conv:{sequence}` and `conv:{sequence}:listen` on the pending conversations, retries refused ones (including a conversation it was caught on), and witnesses open acts; every command goes to `world.Current()`. It first asserts that the season accepted Notices and Listens and that in at least one week a cap was reached and the next attempt refused; then that no week exceeds `NoticesAWeek`, `ListensAWeek`, the per-pair-and-tone rule, 2 Overheard claims or 1 pact sentence;
- **storylines below 92 and house events below 400** in a greedy 12-seat chip season, played by the same hunter on the same world;
- **cooldown capacity**: a greedy season in the 16-seat verification stress house (`SeasonBuilder.CreateVerificationStressHouse(16, seed)`, then `ShippedRules.ApplyFresh(s)` as `T/AllWeekCadenceTests.cs:884-885@h` does, then the literal `story.rulesVersion = 14` (§L), with 16 seated asserted) never drops a `said:`, `tell:`, `liar:`, `found:`, `h2h:`, `heard:`, `pact-heard:`, `talk:`, `confidant:`, `invent:` or `invented:` key. After every command `story.cooldowns.Count` stays below 256, because `Cool` refuses silently only when 256 unexpired rows remain (`SS/EpisodeEngine.Story.cs:1038-1039`). `StorySeasonTests.StorySeason(seed, 16)` is never used, because `SeasonBuilder.Create` clamps every playable season to its roster's 12 (`S/SeasonBuilder.cs:83-88,145`);
- the explicit 1,920-season `PacingReport`, paired 11 against 14, and the `AgencyReport`, both run at 14.

The greedy, storyline and capacity tests and the reports sit inside `#if !UNITY_5_3_OR_NEWER` beside `PacingReport`, because the NPC world exists only in the subset.

**Lint:**
- `StoryCatalogLintTests`: new families sit between Spine and `Latest`; real people in the game register; a lapse option has `checkAt == null` (beside `T/StoryCatalogLintTests.cs:63`), and an option with `checkAt` has `check >= 0`, so the `checkRole` assertion at `:100` still runs; on the new families' beats, a lapse's caption differs from every other option's on its beat, because the duplicate-caption rule (`:85-87`) passes a lapse that shares a caption with `showIf` options;
- `LineBankLintTests`: ≤ 15 words, exactly 3 lines a slot, no personal or romance words in slots an alumnus can fill, no card with two equal captions.

**Also in the subset** (both files are already listed, `Tools/SimulationTests/SimulationTests.csproj:40,87`):
- `ConversationExchangeResponseTests` gains the 12 lines as new cases. Their fixture turns the story on (`EnableStory`) and then sets `story.rulesVersion = 12`. The existing vote-question pins at `:174,201,221` run with the story off (the `Nomination`, `Meeting` and `Campaign` fixtures at `:13-46` never call `EnableStory`) and stay as they are. The 13 intel lines are added the same way, with the rules-11 and 12 pins unchanged.
- `HouseguestNotesTests`: the notes-page tell lines, only at 12 or above. The three profile blocks get a test on the `ProfileView` the HouseTalk partial builds (EditMode, below).
- `story:steered-wrong` is permanent: `StoryReceipts.Permanent` contains it, `StoryReceipts.IsKnown` is true, and `RelationshipLedger.Decays` of it is false; the four fading receipts have `Decays` true (came-clean's case in S5, the other three in S7). This follows `T/WaveDInertSchema28Tests.cs:408-411` (subset, csproj:95), or a new `[TestCase]` in `T/RelationshipLedgerTests.cs:20-28`. `RelationshipLedger.Permanent` itself is private (`S/RelationshipLedger.cs:46`), so the pin goes through `Decays`.

### EditMode-only (Unity, or the Mono harness)

- The ClaimSource and receipt validation pins (`story:came-clean`'s below-14 refusal in S5, the four Between-us receipts' in S7).
- The profile blocks: the `ProfileView` that `HouseTalkBlocks` builds holds no block below 12. At 12 (S1) it holds "Their manner" and "What you've learned" when their evidence exists, never "How they seem to see you". From 13 (S3) it also holds "How they seem to see you", so S3 leaves a rules-12 profile unchanged.
- `NpcPairing.RoomOf` agrees with `HouseInteractionAnchors.Meetings`: every known rendezvous maps to a `RoomWords.IsRoom` id equal to its `Definition.Room`.
- `StoryFalloutTests` for the new endings.
- An old-save fixture at each number once it freezes.

### PlayMode

**`EpisodePlayModeTests.ComesToYou_Story…`** (each case is its own method named `ComesToYou_Story<Behaviour>`, for example `ComesToYou_StoryBeatWalksUpAndStaysOutOfThePull`, because the offers plan's filtered PlayMode run matches its `ComesToYou_` prefix, an unanchored regex, against each test's full name and never against file names (offers §H); a method named `ApproachWalkUp…` would not run):
- every case opts in with `director.AsksEnabled = true` after `ReloadEpisode()`, on the pattern of the offers plan's `InstallComesToYouHouse`, because the PlayMode fixture sets it false. Each asserts `director.NpcAutonomyReady` (message `NpcAutonomyDiagnostic`) before it waits, then that a visit for that exact record was reserved and spoke, not only that the beat stayed out of the Pull;
- a story beat and a patron ask walk up, installed at Social or Campaign, where the house builds its own world. With offers slice 4 landed, the first pitch still walks over beside them: that case installs Nomination with you as HoH, a pending pitch and the week's rules with an open window (`RE/EpisodeDirector.NpcInvitations.cs:121-124`) through `InstallDiaryFixture` or a hand-shaped save, then calls `director.BuildNpcWorldForDiagnostics()` (`RE/EpisodeDirector.NpcSocial.cs:129-135`). It never uses `InstallHoHPitches` or `InstallTalkingHouse`, whose `HoldTheHouseForTheFixture` suspends autonomy for the director's life;
- "What's on your mind?" opens the conversation at zero actions, with you stood out of witness range of every pair, because from 12 zero actions no longer takes the Nearby card down; a pending ask is held while you witness a conversation, and its visit starts within `AskSettleSeconds` + `AskScanSeconds` after you stand still out of range with the caption down;
- "Maybe later", Escape or B on the bubble sets the story ask aside for the phase: `IsAskSetAside` is true, `declinedPulls` is unchanged, and the beat stays out of `CurrentPull`. Walking away, changing room, or the lease running out before auto-open came due (the player moving inside the drift radius, or the bubble up less than 1.2 s) is a miss: `AskMissCount == 1`, `IsAskSetAside` false, the beat still out of `CurrentPull`, and the same record leases again after `AskRetrySeconds` (8 s); a second miss opens it as the scene card (offers test 3's pattern), which sets it aside and counts a presentation, and closing that card unanswered moves `askNextAt` by `AskGapSeconds`. A lease that runs out while auto-open is held (the emote menu or the Nearby card up, you still, the bubble up past 1.2 s) only cancels: `AskMissCount` unchanged, `IsAskSetAside` false, the beat still out of `CurrentPull`, and the same record leases again after `AskRetrySeconds` (offers §C.2 step 8);
- **a walk-up beat never appears in the Pull, set aside or not, while the queue can present it**; a patron offer and its report never show as a play Pull while `AsksOn` holds;
- a story beat waiting outside the Pull, or set aside, never holds back a deal walk-up: the deal comes in the shared public order after `AskGapSeconds`, and the story caps never count it; a story ask that cannot reserve never blocks the next ask;
- the story source's own caps (§D4), on top of the offers plan's shared ones: ≤ 2 story walk-ups a window, ≤ 4 a week and ≤ 1 per houseguest per phase. A walk-up retried after a miss counts once, and deal walk-ups are never counted (the case above). Where a case needs a cap reached, it lowers the cap through the private-write helper, as the offers plan's `askPhaseCap` case does, so the story caps are director instance fields beside `askPhaseCap` and `askWeekCap`, never statics;
- D18 exclusivity; a word on screen leaves `NpcInvitationIsVisible` false, offers no Hear, E does nothing and the Pull is not held back, and a Pull, a visit or the ask card hides the word;
- under reduced motion a story beat still walks up, stays out of the Pull, and the camera does not move. With `AsksEnabled` false the beat is a Pull exactly as at origin/main, whose "Hear them out" opens the scene card with "I'll keep my eyes open" and "Tip them off" for a patron ask; `PullOffered` is "Hear them out", never "Take it on", and an existing play's offer still shows "Take it on";
- a raised story beat's Back to topics, and Escape in person, return to the topics wheel with AskVote usable, clear `askDialogueWith`, `askDialogueRecordId` and `askDialogueWay`, and move `askNextAt` by `AskGapSeconds`.

**`OverhearPlayModeTests`:**
- a Notice by standing near, with `ClearSubject`, a frozen camera and `Time.captureDeltaTime` pinned;
- the Listen press;
- no action spent;
- a hunch drawn on the web.

**Labels draw** (`AssertEveryLabelDraws`, `Assets/Gamesim/Tests/PlayMode/EpisodePlayModeTests.LabelsDraw.cs:25,47`). Every new in-season surface opens on an installed fixture at its number, because shipped seasons never reach it while `Offered` is 0. The cast-screen chip is the exception, because no fixture reaches the cast screen (§L). The sweep checks it at Standard and again after `ApplyTextSize(TextSize.Largest)` (`Assets/Gamesim/Tests/PlayMode/EpisodePlayModeTests.Accessibility.cs:696`), either by extending `Labels_EveryScreenAndCardDrawsItsCopy` or by calling the helper in each surface's own test. The surfaces: the three profile blocks; the "Hunches" chip and the side column's hunch line; the cast-screen chip, once `StoryRules.Offered >= StoryRules.Manners` (§L) lets the cast screen show it; until then the chip's own test asserts that the cast screen has no control captioned "The house talks back", at Standard and at Largest; the story beat raised in the conversation and the "What's on your mind?" bubble; a word's bubble. The deal and pitch dialogue and bubble stay under the offers plan's sweep. If `OverhearPlayModeTests` is its own class, it is a partial of `EpisodePlayModeTests` or carries its own copy of the helper, as `CeremonyRevealPlayModeTests:1204` does.

**Captures** at Standard and Largest, on a screen of their own.

**Flake avoidance:**
- wait for `npcFrameFraction + npcWorldFraction < 1` before Hear;
- read revisions after the panel opens;
- never call `TickNpcSocialRuntime` from a test;
- take the last child after a render;
- the bubble is a screen-space overlay on its own canvas (`RE/HouseNpcInvitationBubble.cs:149-151`, sort order 69), placed each frame by projecting the speaker's anchor; after offers slice 3 the projection uses `ViewCamera` and the bubble stays an overlay. Find Hear by its caption, assert it `Is.SameAs(bubble.HearControl)` and press it with `onClick.Invoke()`, as `Assets/Gamesim/Tests/PlayMode/EpisodePlayModeTests.DialogueIncomingOffer.cs:84-88` does; never press it through a world or camera raycast. The only world-space canvases on NPCs are the name plates (`R/House/HouseNpc.cs:166`).

---

## O. Milestones

**Common rules.**
**The refresh (2026-11-13) replaces this section's order and slicing with §Q3; the table below keeps each slice's design, files and proofs, read with §Q3's splits and moves.**

- Every slice is opted in by tests. The chip follows §L.
- Each slice starts from origin/main at the time it is cut, merged with the branch its brief names (§Q3). The SCH30 merge of `@h` is history: schema 30 and §14 are both on main.

**Read, never edited, while their tracks are in flight:**
- `NpcAlliances`, `NpcAgendas`, `ThreatAssessment`, `WebEvictionVoting`, `Alumni*`, `DialogueRules*`;
- `S/RelationshipLedger.cs`: §14 has merged (PR #40), so S7 adds its one-line Permanent entry (`story:steered-wrong`) itself and waits for nobody;
- `T/BalanceLab.cs`, `T/BalanceLabNpcWorld.cs`, `T/BalanceLabPolicies.cs`, `T/BalanceLabGoldens.cs` and `T/BalanceLabTests.cs`: R7's PRs only until the R7 stack merges, then only the *-ON PRs (§Q4). S2a's lab file only reads them.

**Edited by one line only:** `RE/EpisodeDirector.Journal.cs`, the `HouseTalkBlocks` call in `RenderHouseguestProfile` just before `hud.HouseguestProfile(view);` (`:432@n`, §C). `ProfileRecords` (`:456@n`) is not edited. The §14 hand-off is moot (PR #40 merged); the UI refactor's D7a, which rewrites the profile, is ordered against S1 by §Q5. The blocks are filled in `RE/EpisodeDirector.Journal.HouseTalk.cs` and drawn by `RE/EpisodeHud.Roster.cs`.

**Coordinated with dialogue rules 2 (merged; `S/DialogueRules.Rules2.cs@n`):** `ClaimRow` and `VoteRead`. D2's `ToldClaim` reads the Told claims `AskVote` writes at 12 (§G1). This plan edits no dialogue file.

| # | Stack | Slice | Builds | Main files | Proof | Depends on |
|---|---|---|---|---|---|---|
| S1 | A (12) | **Their manner** | Rules 12; `Regard`'s rules-12 terms, `Candour` (weights, stake, shifts and fixed values at 12), `Manner` (Regard's 13 and 14 terms land in S4 and S7, Candour's §F stake conditions in S8); the vote bank; `AskVote`, `ReadPerson` and `SettleVoteRead` at 12; the `said:` and `h2h:` records; the notebook's manner and tells, and the profile blocks' hook (no chip, §R; S1 moves `Latest` to 12 and creates `S/EpisodeValidation.HouseTalk.cs`) | `SS/StoryState.cs` (`Latest`), `S/Regard.cs`, `S/Candour.cs`, `S/HouseAnswers.cs`, `S/EpisodeEngine.VoteRead.cs`, `S/ConversationExchange.cs`, `S/HouseguestNotes.cs`, `S/EpisodeValidation.HouseTalk.cs` (created), `RE/EpisodeDirector.Journal.HouseTalk.cs`, `RE/EpisodeHud.Roster.cs`, the one-line `RE/EpisodeDirector.Journal.cs` call | HouseTalkRules, Regard, Candour, Manner, VoteAnswer; every rules-11-or-below digest (§N: `RolesSeasonDigests` from HT0, CommitmentRulesSeasonDigests, AllWeekSeasonDigests) unchanged | HT0 |
| S2 | A (12) | **Overheard and Hunches** | "conv:" dispatch; `ClaimSource.Seen` and the `Missed` attempt row; the `heard:` and `pact-heard:` keys; `NpcPairing.RoomOf`; the director's watch; free Listen; `Eavesdrop` refused and the free tiles; the web; PortVerification in chip seasons; the attentive walker and Stack A's paired report | `S/EpisodeEngine.Overhear.cs`, `S/EpisodeEngine.AllWeek.cs`, `S/EpisodeEngine.cs` (the `Social()` refusal, `:1228-1238`), `S/SeasonLedger.cs`, `S/EpisodeLedgerValidation.cs` (`Seen` in standings only, score 1-3, refused below 12; `:24-27`), `S/EpisodeValidation.HouseTalk.cs` (S1's file, appended: `hunch` refused below 12), `S/VoteRead.cs`, `S/NpcPairing.cs`, `RE/EpisodeDirector.Overhear.cs`, `RE/EpisodeDirector.NpcSocial.cs`, `RE/EpisodeDirector.Dashboard.cs`, `RE/EpisodeDirector.FreeTimeBoard.cs`, `RE/EpisodeDirector.FreeTimeScreen.cs`, `RE/EpisodeDirector.Conversation.cs`, `RE/EpisodeHud.Chrome.cs`, `RE/PortVerification.Autonomy.cs`, `RP/RelationshipWeb.cs`, `T/HouseTalkLab.cs` | Overhear; PlayMode Overhear; Stack A's paired report (11 against 12), read by the owner for question 2(a) | S1 |
| S3 | B (13) | **How they see you** | Rules 13 (`StoryRules.Gossip`; `Latest` and `IsDefined` move to it; refused below schema 30); Candour at G5's sites; the stated band; the remaining answer banks (the intel band); the notebook block ("How they seem to see you", from 13) | `SS/StoryState.cs` (`Gossip`, `Latest`, `IsDefined`), validation (13 refused below schema 30), `S/EpisodeEngine.cs`, `S/EpisodeEngine.Commitments.cs`, `S/NpcSocialActions.cs`, `S/HoHPitches.cs`, `S/ConversationExchange.cs` (the 13 intel lines), `RE/EpisodeDirector.Journal.HouseTalk.cs` (the "How they seem to see you" block, filled at 13 and above; `RE/EpisodeHud.Roster.cs` already draws every block, from S1) | HouseTalkRules (13's cases: defined from here, `Latest == Gossip`, refused below schema 30); Candour sites; notes | Stack A |
| S4 | B (13) | **Talkers** | `Talkers`, with the confidants' weekly item (§H; it reads any confidant bond, and the existing writers already make them, so it needs nothing from S5); named whispers; `told:` and `listen:` facts; Regard's talked-about-them term (13); the whisper cases; Conversation-first `MakeRoom`; the talker keys and the `confidant:` key; the emergency-meeting exclusion | `S/Talkers.cs`, `SS/StorySystems.cs`, `SS/EpisodeEngine.StoryHooks.cs`, `S/EpisodeEngine.cs` (the `told:` fact from `ShareInformation` in `Social()`, `:1278`, where its subject is resolved; `VentAbout` and `ShareSecret` may write theirs from `StoryConversation`), `S/EpisodeEngine.Overhear.cs` (S2's file: on `Overhear`'s caught path, at 13 and only when `Talkers.Is(s, catcher)`, the Whispered `Conversation` fact with `refId = "listen:" + sequence`, whose only first knower is the catcher, §I3), `S/Regard.cs` (the talked-about-them term, §J), `S/EpisodeValidation.HouseTalk.cs` (`talker` refused below 13), `SS/StoryCatalog.BondsAndSecrets.cs` (the cast at `:331-332`) | Talker; RegardTests' talked-about-them case (0 at 12, −5 at 13) | S3 |
| S5 | C (14) | **Getting to know {Name}** | Rules 14 (`StoryRules.ComesToYou`; `Latest` and `IsDefined` move to it; refused below schema 30); the acquaintance thread; seven chapter templates and their `landed` predicates; `ThreadChapter.opener`; `BeatTemplate.speaker` (the declaration only); `OptionTemplate.checkAt`; `HouseMoment.Of` (the pure tag reader only) and the power chapter's choice by event; the verdict and the `story:came-clean` receipt that "Own it" writes (§L); the lane exemption; the priority order and pool guard (the event-arc step stays empty until S8); the `SeedThreads` exclusion | `SS/StoryState.cs` (`ComesToYou`, `Latest`, `IsDefined`), validation (14 refused below schema 30), `SS/StoryThreads.cs`, `SS/StoryTemplates.cs`, `SS/EpisodeEngine.Acquaintances.cs`, `SS/Acquaintances.cs`, `SS/StoryCatalog.HouseTalk.cs`, `SS/EpisodeEngine.Threads.cs`, `SS/EpisodeEngine.Story.cs`, `SS/EpisodeEngine.RoleMoments.cs:258`, `S/HouseMoment.cs`, `SS/StoryOdds.cs` (`story:came-clean`: its constant, fading, `Impact` +6 and its `Describe` line), `S/EpisodeValidation.HouseTalk.cs` (extended: `story:came-clean` refused below 14), `T/StoryCatalogLintTests.cs` | HouseTalkRules (14's cases: defined from here, `Latest == ComesToYou`, refused below schema 30); AcquaintanceThread: tiers 1 and 3, the caps, once per houseguest, the order including every power chapter, and each ending's conditions and effects that read no Between-us ask or receipt; HouseMoment's tags, one fixture per §F row; pacing; `story:came-clean` known, fading and refused below 14 | Stack B |
| S6 | C (14) | **Got a minute?** | The story source in the offers queue (`ComesToYou.Source.Story`: its rows, `IsCurrent`, caption pair, place in the order and dispatch cases, and the conversation's Story branch); `HouseWords` and the word bubble; the readers of `BeatTemplate.speaker` (declared in S5); the Pull exclusion and `PlayPull`'s skip of speaker-bearing beats; walk-up pacing; competition reaction words | `S/ComesToYou.cs` (the Story branch), `RE/EpisodeDirector.ComesToYou.cs` (the Story cases), `RE/EpisodeDirector.ConversationNavigation.cs` (the Story branch, and its Story case in `BackConversationNavigation`), `S/HouseWords.cs`, `RE/HouseWordBubble.cs`, `RE/EpisodeDirector.HouseWords.cs`, `RE/EpisodeDirector.Story.cs` | ComesToYou (Story cases), HouseWords; PlayMode `ComesToYou_Story…`, confirmed by name in the filtered run's report (offers §H); HouseMoment's approach-source assertion | **Offers slices 1-4 landed** (S6 is offers slice 5); S5, S7, S8 |
| S7 | C (14) | **Between us** | The six asks; timing (`BeatTemplate.followsNextAnchor`, the offer closing at the next anchor, the report's own slot); `ServeQuietAsks`, `SettleReport`, `CheckQuietAsks`; the tip-off and `PlayEndings.Tipped`; caught asking around and Regard's asking-around term (14); the four Between-us receipts (`came-through`, `steered-wrong`, `tipped-off`, `let-down`); `PlayTemplate.label` | `SS/StoryCatalog.BetweenUs.cs`; `SS/EpisodeEngine.BetweenUs.cs` (new partial of EpisodeEngine, with a two-line meta: `ServeQuietAsks`, `SettleReport`, `CheckQuietAsks`, the tip-off check and the caught-asking-around coin; the only S7 code that calls the private `Cool`); `SS/EpisodeEngine.Story.cs` (`StoryAnchor` at `:288`, `ResolveStoryBeat` at `:834`, `AdvanceCycle`, `StoryAskAirtime`); `S/EpisodeEngine.cs` (the `AskForIntel` hook, `:1387`); `SS/StoryTemplates.cs`; `SS/StoryPlays.cs`; `SS/EpisodeEngine.Plays.cs`; `SS/StorySystems.cs` (`Knowledge.AskedAround`); `S/EpisodeEngine.Ledger.cs`; `SS/StoryOdds.cs` (the four Between-us receipts); `S/EpisodeValidation.HouseTalk.cs` (extended: the four Between-us receipts refused below 14); `S/Regard.cs` (the asking-around term, §J) | BetweenUs; AcquaintanceThread's ask-driven cases; RegardTests' asking-around case (0 at 13, −5 at 14) | S5; for `story:steered-wrong`'s permanence (§L) and its `Decays` case (§N), ~~§14's merge or §14 taking the one-line `RelationshipLedger.Permanent` addition (§O)~~ nothing: §14 merged (PR #40), so S7 adds that line itself (§Q7). Without that line the receipt fades: a story receipt is written through `RecordOneWay` (`SS/EpisodeEngine.StoryEffects.cs:114`), whose `decayable` is `Decays(type)` (`S/RelationshipLedger.cs:184`) |
| S8 | C (14) | **The house's events** | The §F event arcs and their rule-driven start; the reveal-time proven read in the reveal grudge; Candour's three §F stake conditions (§J: a rival HoH at stake 2, the HoH's other allies +1, voters you can place as voting your ally out at 2 that week, read only under `StoryAt(s, StoryRules.ComesToYou)` from `HouseMoment`'s classes); `the-plan`'s and `the-call`'s preference terms and lobby refusal | `SS/StoryCatalog.HouseTalk.cs`, `SS/EpisodeEngine.StoryHooks.cs:93-117`, `S/KnownBallots.cs` (`ProvenAtReveal`), `SS/StoryConsumers.cs`, `S/StrategyRules.cs` (`CanLobby`), `SS/EpisodeEngine.Story.cs` (the event-arc start), `S/Candour.cs` (the three §F stake conditions); `S/HouseMoment.cs` is read only | HouseMoment, one fixture per event: the arc chosen, the event arc taking priority over a due chapter and the pool, and `the-plan`'s `Promise` Safety write; CandourTests' §F stake case (the same installed fixture at 13 and 14) | S5, S7 |
| S9 | — | **Balance and enrolment** (refresh: replaced by A-ON, B-ON and C-ON, one per stack, §Q3) | The paired reports at 13 and 14, reusing S2's walker (§14's lab work has merged, PR #40); captures; the owner's sign-off; enrolment only with or after R7 and B9; goldens re-recorded with a reason | `T/BalanceLab*.cs`, `T/BalanceLabGoldens.cs`, `T/HouseTalkLab.cs` | §M's gates | S2's walker; §14's lab work for any edit to `T/BalanceLab.cs`; R7 and B9 for enrolment only; dialogue D2's merge for the rules-2 storyline case |

**S6 lands last in Stack C.** It needs S5's forced verdicts, chapter openers, cold shoulder and `HouseMoment` tags, S7's patron offers and reports, and S8's event arcs, whose beats walk up. Until S6 lands, the speaker-bearing Approach beats that S5, S7 and S8 write surface as today's Pull, and every slice is reachable only by tests.

---

## P. Defaults taken, and questions for the owner

### Defaults (every constant)

| Constant | Value |
|---|---|
| `Manners` / `Gossip` / `ComesToYou` | 12 / 13 / 14. `Offered` is 0 until Stack A passes its measurement (owner question 2, answered). `Current` is 10 today, 11 after R7, then 12, 13 and 14 at A-ON, B-ON and C-ON (§Q1, §R) |
| `AcquaintancesAtOnce` / `AcquaintancesASeason` | 1 / 2 (3 from 12 seats) |
| `AcquaintanceMinActive` / `AcquaintanceRapport` | 5 / 6 |
| `AcquaintanceVerdictWeeks` | 3 (2 at a size where S9's acquaintance measure falls short, §M) |
| Chapter `cooldownWeeks` / `attempts` / `oncePerHeadliner` | 0 / 2 / false |
| Ending thresholds | B4's table |
| Walk-ups | 2 a window / 4 a week / 1 per houseguest per phase |
| `WordsAWindow` | 3 |
| Spacing / visit | The offers plan's: 20 s gap / 8 s after a miss / 120 s per asker (free-house seconds) / 25 s from arrival, after offers slice 3's travel budget |
| `PatronFaith` | 45, or an ally at faith ≥ 30 (§D2's floor: below 30 even an ally asks nothing); a TestLoyalty ally's line is "Prove it." |
| `AsksASeason` | 3 (5 from 12 seats) |
| Patron and subject rests / decoy rest | 2 weeks / 3 weeks |
| Grudges: decoy / found out | 20 / 40 |
| Receipts: came-through / steered-wrong / tipped-off / let-down / came-clean | +10 / −20 / +10 / −5 / +6 |
| Tip-off coin | 0.25 + 0.1·(Sociable − Honest), within 0.05-0.75 |
| E1 found-out coin | 0.15 (+0.10), one draw per ask (`found-out:{cycle}`), landing within 2 weeks |
| Caught asking around | 0.2 |
| Candour bands / stake weight / Honest weight | 65, 45, 30, 15 / 15 / 5 |
| Trait shifts | up to .10 / .15 / .10 / .10, each capped at the source weight; a nemesis on the vote moves all of honest to dishonest; Loyal moves all of dishonest to neutral |
| t_L / t_H | As in G2 |
| Dominant-tell share | 70% |
| `ReadPerson` addendum | 0.6 after a lie / 0.2 after an honest answer |
| `TalkerScore` / talker share / `TalkChance` | 3 / ⌈n/4⌉ / 0.5 |
| Talker items a week / per talker / inventions house-wide | 2 / 1 / 1 |
| Notice hold / range | 2 s / 6 m |
| Listen hold / range | 5 s / 3.5 m |
| `NoticesAWeek` / `ListensAWeek` | 6 / 2 |
| Overheard ballots / pact sentences a week | 2 / 1 |
| Catch / caught cost | 0.15 + 0.10 − 0.05 / −5 |
| `HunchTone` | 1 / 2 / 3, shelf life 2 |
| Regard terms | §J's table |
| Regard `fear` / its stake term | 8 per HoH win + 6 per veto win, cap 40 (read only by the stake) / fear ≥ 24 adds +1 to the stake of a houseguest who is not your ally (§J) |
| `h2h:` / `liar:` keys | week + 3 / week + 2 (a liar is held at Neutral for the rest of the week and through next week's vote; §F, §L's cooldown table) |
| Added decisions / added minutes at 8 seats | ≤ 5 / ≤ 2.5, a measured target with a cut order (§M) |
| `EventArcChance` | 1.0 |
| Event-arc preference terms (14) | `the-plan`'s "Put them up": −12 to `StoryConsumers.NominationPreference`; `the-call`'s "Use it on them": +10 to `StoryConsumers.SavePreference`; this week only, no `View` (§F) |
| Event-arc rapport | `the-plan`'s "Your call" `Move` +3 / `the-call`'s "Keep it" `Move` +2 |
| `you-owe-me` refusal | `Grudge` 30 |
| `the-plan` pick rest | 3 weeks across its answers, as a decoy rests (§F, §M) |
| Story walk-up bubble pair | "What's on your mind?" / "Maybe later" |

**Deferred:**
- the persuasion ask;
- a "Heard anything?" verb, and the player questions "Who worries you?" and "Are you with {X}?" (each would need a command kind);
- NPCs seeing the player with someone as saved sightings;
- feeding a talker a story you made up;
- a twist engine.

### Questions that need the owner

**Answered, 2026-10-30.** The owner: "Use the defaults for all six storyline questions". So: (1) story rules 12, 13 and 14 are claimed for this plan and 10 stays the story session's, a delegated call, confirmed by the owner, with B9's later role-moment tuning keyed to 11 and to 15 or above; (2a) the chip may be offered before B9 once Stack A has landed and passed its measurement; (2b) the chip is off by default until R7; (3) a chip season may add up to 5 decisions at 8 seats, measured, with the stated cut order if it runs over; (4) a lingering Listen keeps the capped true reveal; (5) talkers may distort and rarely invent (never about a real alumnus), and what you tell a talker spreads; (6) Have-Nots and production are enough for twists for now.

**Refresh, 2026-11-13:** these answers stand and are not asked again. 2(b) ("off until R7") reaches its end when R7's switch-on lands. The default after R7 was asked of the owner afresh and answered on 2026-11-13: no checkbox; each stack becomes the default for every new season at its switch-on (§R).

1. **The numbers.** The story session cannot be reached. Will you confirm claiming story rules 12, 13 and 14 for this work, with 10 left as the story session's? It would be recorded as "delegated call, confirmed by the owner". B9's later role-moment tuning would then key to 11 and to 15 or above.
2. **Role moments in chip seasons, and default-on.** Every house-talk number includes the built role moments, which the owner decided wait for B9. So:
   - **(a)** May the chip be offered before B9? The cost: role moments freeze in your chip saves untuned, as dialogue call 9 warned.
   - **(b)** Should the chip be on by default for new seasons? That is R7's enrolment, before B9.
   - Default: (a) yes once Stack A passes its measurement; (b) no, the chip stays off until R7. The owner took both defaults on 2026-10-30.
3. **Decisions.** May a chip season add up to 5 decisions a season at 8 seats (about 2.5 minutes at the lab's 30 s), given that the baseline already runs about 51 minutes against your 45? Default: yes, measured in S9 (Stack A in S2). If not, cut in this order: one acquaintance a season below 12 seats (up to 4 chapter asks and 3 second nodes fewer); the first talk becomes one node; quiet asks drop to 2 a season.
4. **Free listening tells the truth.** Should a lingering Listen still reveal the true band, vote and pact sentence (free, 2 a week, with a risk of being caught)? Or should free listening give hunches only? Default: the capped truth.
5. **Talkers lie, and your words travel.** May talkers distort and, rarely, invent items about votes and pacts (≤ 1 invention a week, never about a real alumnus)? And may what you tell a talker spread round the house? Default: yes to both.
6. **Twists.** Is reacting to Have-Nots and production enough for now, with the event layer ready for a later twist engine? Default: yes.

---

## Q. Refresh, 2026-11-13: this plan against main 0ab29719, after the role moments

This plan was written on 2026-10-30 against main 37416049, when new seasons played story rules 9. Since then story rules 10 (`Blame`) is enrolled, offers slices 1-3, the All-Star histories (§14), schema 30, B9 and dialogue rules 2 have merged, and the role moments' switch-on (R7) is being landed. This section brings the plan onto main. Where it and an older section disagree, this section holds. Every citation here is `@n` and was read on that ref.

### Q1. Story-rules levels

- **10, `Blame`**: what new seasons play today (`SS/StoryState.cs:297,312@n`) and what saves at 10 keep.
- **11, `Roles`** (`:304@n`): becomes `Current` in R7's switch-on PR, the first of four enrolments, and stays `Current` until A-ON. Every "rules-9 unchanged" proof in this plan becomes **"the previous level's seasons unchanged"**: rules-11 seasons against HT0's `RolesSeasonDigests` for Stack A, rules-12 against A-ON's `MannersSeasonDigests` for Stack B, rules-13 against B-ON's `GossipSeasonDigests` for Stack C, each held at its level by name (§N).
- **12 `Manners` (Stack A), 13 `Gossip` (Stack B), 14 `ComesToYou` (Stack C)**: claimed by the owner on 2026-10-30 (answer 1). Checked again on 2026-11-13: no pushed ref defines another story-rules 12-14 constant (all 47 branches of the repository after a fetch with prune, every file that declares a `StoryRules` class, the frozen readers' included).
- **HT0** adds the three constants without moving `Latest` (`:319@n`). `Latest` moves in S1 (12), S3 (13) and S5a (14). That ships nothing, because `IsDefined` is a bound on `Latest` (`:327@n`), `EnableStory` writes `Current` (`SS/EpisodeEngine.Story.cs:75@n`), and only a test writes 12-14 while `Current` is below them.
- **`Current` moves four times, each in its own enrolment PR: R7's switch-on (11), A-ON (12), B-ON (13), C-ON (14)** (the owner, 2026-11-13: "it needs to be the default and incorporated into the main game"). Each ON lands only once its stack is whole and its report is inside §M's limits; the owner signs Stack A's numbers, and the lead accepts Stack B's and C's inside the limits for him (a delegated call, 2026-11-13). There is no `Offered`, no chip and no cast-screen choice (§R).
- **Each ON is an enrolment** with R7's switch-on as its model: `Current` and its doc lines; the audit of every test that would play the new level without naming it (follow, or hold at the old level with a reason); the pins that name `Current`; the lab's goldens and the Rules tuple re-recorded, the old level becoming named lab cells; `FreshSeasonStateDigestTests` re-recorded, the previous level's digest held by name and a new one added at the new level; an old-save cell at the new level, the earlier cells still green; the record.
- **A season already started keeps its rules**, as always: load never re-applies the fresh-season rules. To see a stack, the player starts a new season.
- **Once a level is `Current`, nothing is added at it.** Goldens and digests at a stack's level are recorded only in its *-ON PR. S6 (director-side, no command) is the only slice after C-ON.
- 15 and above stay for later role-moment tuning (answer 1).
- Every gate is a lower bound on its own constant, never `Current`. Grep `Current` before moving any constant.

### Q2. Saved state, and the replay guards

- `LiveSchema` stays 30 (`S/EpisodeState.cs:252@n`). **No slice adds a saved field.** Each level goes into the existing `story.rulesVersion`; there is no cast-screen choice either (§R). A slice that finds it needs a field stops and goes to the lead: schema 31 is its own project, claimed with the owner, and not part of this plan.
- New closed values (`ClaimSource.Seen`, log kinds `hunch` and `talker`, the five receipts) are validated and refused below their level, in the same commit as their writer.
- **Replay guards on every plan-32 PR:** `RolesSeasonDigests` (from HT0) and each later held digest (`MannersSeasonDigests` from A-ON, `GossipSeasonDigests` from B-ON, `ComesToYouSeasonDigests` from C-ON), `CommitmentRulesSeasonDigests` (it holds its seasons at `Threads` by name, `T/CommitmentRulesSeasonDigests.cs:210-212@n`), `AllWeekSeasonDigests`, `FreshSeasonStateDigestTests`, the frozen V25/V28/V29 tests and every old-save cell. Paired reports leave out fields that only one side has.
- A slice that refuses or adds a command (S2a's refusal of the paid `Eavesdrop` at 12) proves that no recorded fixture at or below 11 buys it: removing or refusing a step re-rolls a seeded season.
- The offers sources and S6 are director-side. `ComesToYou` is read by Runtime only, so a new source cannot move a season digest. Their proof is that each answer sends the command it sends today.

### Q3. The slices and their order

The nine slices of §O become these. Each lands as its own PR, in this order, after the R7 stack (R7's landing branch, then the switch-on and its old-save cell), with the offers plan's slice 4 (OF-a, OF-p, OF-b) and the HoH room's two slices (HR-1, HR-2) interleaved. This refresh (P0) and the offers slice-4 test list (OF-0) are documents and may land at any time. The lead groups the PRs into stacks of at most three, with a full test set on each stack's head.

| Order | Slice | What it is | Lands dark? |
|---|---|---|---|
| 1 | OF-a | Offers slice 4, part 1: reply cards of kinds confrontation, gossip and plea walk up and open as a dialogue; the bubble's caption overload (offers §C.4). A pitch card is OF-p's. Not a plan-32 slice, but S6 depends on it | No: in every season where asks are on |
| 2 | HR-1 | Not a plan-32 slice (the owner, 2026-11-13): while a houseguest Head of Household decides, they wait in the HoH room, and the strategy window's line says so; the case you make there is the existing plea, one conversation. No new command, rule, caption or price | No: in every season |
| 3 | **HT0** | `RolesSeasonDigests` (rules-11 fresh seasons, fixed seeds, `story.rulesVersion = StoryRules.Roles` written by name after `ApplyFresh` as `T/CommitmentRulesSeasonDigests.cs:210-212@n` writes `Threads`; listed in the Unity-free subset's project and Explicit like `AllWeekSeasonDigests`, `T/AllWeekSeasonDigests.cs:45,109@n`) recorded on the R7 stack's head; the constants `Manners = 12`, `Gossip = 13`, `ComesToYou = 14` without moving `Latest`; `HouseTalkRulesTests` pins the three numbers; `RoleRulesTests:29@n` loosened to `>= Roles`. **No chip, no `Offered`, no `SeasonBuilder.Choice`, no `ApplyFresh` line and no cast-screen file** (§R) | Yes |
| 4 | OF-p | Offers slice 4, part 2: `ComesToYou.Source.Pitch`. When the player is Head of Household, the pitchers walk up one at a time in the board's order; in a season with the pitch board the walk-up opens the existing board, every card in its dealt order; in a season started before R7 it opens the existing one-at-a-time pitch answer. Counts, timing and the decision budget do not change. The veto board's asks still stay on the board | No |
| 5 | OF-b | Offers slice 4, part 3: same-asker chaining ("There's something else") over the shared queue, now over offers, veto asks, cards and pitches (the contract S6 reuses) | No |
| 6 | HR-2 | Not a plan-32 slice (the owner, 2026-11-13): when the player is Head of Household, the houseguests whose pitch is still unanswered wait in or by the HoH room during the nomination, and approaching one there opens their pitch as OF-p's walk-up does (never a conversation) | No |
| 7 | S1 | Their manner (§O's S1, no chip): `Latest` to 12; Regard, Candour, Manner, the answer banks; `AskVote`/`ReadPerson`/`SettleVoteRead` at 12; the profile blocks; **creates `S/EpisodeValidation.HouseTalk.cs`** | Yes |
| 8 | S2a | Overheard in the rules: the "conv:" dispatch, `ClaimSource.Seen` and the `Missed` row, the `hunch` kind, `heard:`/`pact-heard:`, `NpcPairing.RoomOf`, free Notice and Listen (no draws; 2 a week, counted on attempts; a catch risk), the paid `Eavesdrop` refused at 12 by one line in `Social()`, the attentive walker in `T/HouseTalkLab.cs` | Yes |
| 9 | S2b | Overheard on screen: the director's watch and free Listen, the Eavesdrop tiles walking you over at 12, PortVerification at 12, the Hunch lines and their one "Hunches" filter chip (a social-web control); "Listen in" word for word | Yes |
| 10 | **A-ON** | Stack A's paired report (11 against 12, the same seeds, cells with an NPC world, one-sided fields left out) against §M (+5 decisions, about 2.5 min at 8 seats), signed by the owner; then **`Current = Manners`** and the whole enrolment (§Q1): audit, pins, goldens, digests, `MannersSeasonDigests` held at 12 by name, an old-save cell at story rules 12, the record | **No: every new season plays Stack A** |
| 11 | S3 | How they seem to see you (§O's S3): `Latest` to 13. Built beside S2a/S2b on a branch; lands after A-ON, merging S2a, S2b and A-ON first | Yes |
| 12 | S4 | Talkers (§O's S4), with a stop rule on any weekly turn aimed at one person and a ratchet test with an NPC world | Yes |
| 13 | **B-ON** | Stack B's report (13 against 12, and 11, with NPC-world cells), accepted by the lead inside §M's limits; **`Current = Gossip`**; the enrolment; `GossipSeasonDigests` held at 13; an old-save cell at 13 | **No: every new season plays Stack B** |
| 14 | S5a | The acquaintance thread's machinery: `Latest` to 14; one thread at a time, 2-3 a season; `ThreadChapter.opener`, `BeatTemplate.speaker`, `OptionTemplate.checkAt`; the pure `HouseMoment.Of`; the lane exemption, priority order and pool guard; `SeedThreads` skips the kind | Yes |
| 15 | S5b | The chapters and verdicts: the seven chapter templates, the 14 endings of "Where you stand", `story:came-clean`, the verdict captions as new constants, one retry per acquaintance (counted on attempts) | Yes |
| 16 | S7 | Between us (§O's S7), including the `RelationshipLedger.Permanent` line, which is free since §14 merged | Yes |
| 17 | S8 | The house's events (§O's S8). S7 and S8 both edit `SS/EpisodeEngine.Story.cs`: S8 merges S7's landed head before its review | Yes |
| 18 | **C-ON** | Stack C's report (14 against 13, and 11) with §M's gates (+5 decisions; pariah-free at least 80% on the pacing report) and the owner's cut order, accepted by the lead inside the limits; **`Current = ComesToYou`**; the enrolment; `ComesToYouSeasonDigests` held at 14 (S6's byte-equal proof); an old-save cell at 14. Waits for OF-a, OF-p and OF-b (Stack C needs offers slices 1-4) | **No: every new season plays Stack C** |
| 19 | S6 | Got a minute? (§O's S6, offers slice 5): `ComesToYou.Source.Story`, the bubble pair as new constants, the Story branch and its `BackConversationNavigation` case, the scene-card fallback, `declinedPulls` into `askSetAside`, the spoken-reaction bubble (in a file not named `HouseWords`, which the UI refactor's F5 adds); caps shared with the offers sources. After C-ON, because it adds no command and no behaviour at a saved level | No |

S9 of §O is gone: its paired reports, goldens and enrolment are A-ON, B-ON and C-ON, one per stack.

### Q4. Hot files, one PR at a time, in landing order

- `SS/StoryState.cs`, the `StoryRules` block: R7's switch-on, then HT0, S1, A-ON, S3, B-ON, S5a, C-ON.
- `T/RoleRulesTests.cs`: R7's switch-on (`:31`, `:33`, `:34@n`), then HT0 (`:29@n`, loosened once), then A-ON, B-ON and C-ON (the `Current` pin).
- `T/BalanceLab*.cs`, `T/BalanceLabGoldens.cs`, `T/BalanceLabTests.cs`, `T/FreshSeasonStateDigestTests.cs`: R7's PRs only until the R7 stack merges; then only the *-ON PRs.
- The old-save manifest and `OLD-SAVE-ACCEPTANCE.md`: R7's old-save cell, then A-ON, B-ON and C-ON, one cell each.
- `S/ComesToYou.cs`, `RE/EpisodeDirector.ComesToYou.cs`, `RE/EpisodeHud.AskDialogue.cs`: OF-a, then OF-p, then OF-b, then HR-2 (`RE/EpisodeDirector.ComesToYou.cs` only), then S6.
- `RE/EpisodeDirector.Strategy.cs`: HR-1, then OF-p. `RE/EpisodeDirector.Wander.cs`: HR-1, then HR-2.
- `S/EpisodeValidation.HouseTalk.cs`: created by S1; S2a, S3, S4, S5a, S5b and S7 append, each in its own section.
- `SS/EpisodeEngine.Story.cs`: R7's switch-on (a doc line), then S5a, S7, S8, and each *-ON's doc line in its turn.
- `S/EpisodeEngine.cs`, `RE/EpisodeHud.cs`, `RE/EpisodeDirector.cs` (each well over 1,800 lines): new partials, plus one-line hooks only.
- `Tools/baseline.txt` and `Tools/SimulationTests/SimulationTests.csproj`: append-only in slices. Each PR states its added tests per suite; the lead sums floors at landing (main's floor plus the slice's additions) and resets them from an actual run after each stack's full test set.
- Every builder previews its merge (`git merge-tree --write-tree`) against the integration head at its start and before review. Branches merge origin/main at landing; nothing is rebased.

### Q5. Shared screens with the UI refactor

The interface refactor claims files this plan's slices also edit. Whichever is ready first lands first, and the lead merges the other. Captions are a contract: no existing caption changes, and every new caption is a new constant.

| UI slice | What it does | Meets |
|---|---|---|
| D7a | The profile becomes the person's whole dossier (`RE/EpisodeDirector.Journal.cs:401-490`, `RE/EpisodeHud.Roster.cs`, a new InfoCard) | S1's three profile blocks and its one hook line before `:432@n`; S3's block |
| D8 | The Relationships page on the kit (`RP/RelationshipWeb.cs:674-939`) | S2b's Hunch lines. The web already draws Distrust with a dashed stroke (`RP/RelationshipWeb.cs:341,351@n`), so a Hunch line must read differently from a Distrust line |
| F7 | One filter chip everywhere (captions "Filter relationships: X" kept, `RP/RelationshipWeb.cs:442@n`) | S2b's one Hunches filter chip, built on F7's chip when F7 is in |
| U4 | Listen in and the other house moves grey out with the true reason ("No actions left") | S2b: at 12 the listen tiles are free and walk you over, so no action lock applies to them there |
| S4 | A persistent right column (`RE/EpisodeHud.Chrome.cs:1218-1300`) | S2b's edits to the Nearby card (`NearbyCardName`, `:1209@n`) |
| several | `RP/CastSelect.cs` (cast-screen chips, focus, filters) | Nothing: with the chip gone (§R) no plan-32 slice edits the cast screen |

### Q6. What reaches the player, and what waits on them

- **The "reached the player" rule** (8938c7d1 and 02380e15, 2026-11-03). A running storyline is listed in the STORYLINES block, the Storylines tab, the free-time board's THREADS strip and the story card only once a beat of it has reached the player: a card or scene of its own, or a log line whose audience is everybody or names the player (`SS/StorylinesShown.cs:25-37,43-50@n`). An NPC's beat reaches nobody else. It is display, read from the state, and changes no replay. For this plan:
  - Between-us asks, event arcs and talker passes are story cycles like any other, so they are listed only once they have reached the player. A quiet ask between two others is never listed.
  - `StorylinesShown.Running` leaves the Thread lane out (`:46-48@n`); threads are listed through `EpisodeEngine.Threads` (`SS/EpisodeEngine.Threads.cs:245@n`), which the strip counts unfiltered (`StorylinesShown.cs:75@n`). So S5a applies the same rule to the acquaintance thread: "Getting to know {Name}" is listed under THREADS, in the strip and in the recap only once one of its chapters has reached the player, and a test pins a not-yet-reached acquaintance unlisted in every view, as `RoleStoryMomentsTests` does for the quiet backdoor.
- **WaitingOnYou leaves story beats out** (`S/WaitingOnYou.cs:13-17@n`: "A story beat is not on the list"; `Kind { Offer, VetoAsk, Card }`, `:29@n`). So quiet asks, walk-up beats and acquaintance chapters never reach the cast strip's badges, the objective line or the strategy screens' footer. `ComesToYou.Read` starts from `WaitingOnYou.Read` and returns early when it is empty (`S/ComesToYou.cs:90-93@n`), so S6's Story branch must read the open beats itself and must not sit behind those returns. Whether a waiting quiet-ask report should be named on the objective line (at most `ObjectiveWords.MaxLength` = 28 characters, `RP/ObjectiveWords.cs:24@n`) is a later owner question at S7; its default is no.

### Q7. Moot, and unchanged

- **§14 hand-offs are moot.** The All-Star histories merged (PR #40, 2026-11-01): the Journal hook line, the `RelationshipLedger.Permanent` line and the lab files wait for nobody. Their alumni pair reader is `KnownBlocs` in `S/AlumniKnownBlocs.cs` (`InLivePair` `:84@n`, `LivePairs` `:128@n`), which E2's public alumni pair trigger reads. Real names are kept for the All-Stars, so "never invent about a real alumnus" (answer 5) and `StoryPeople`'s gates matter more, not less.
- **Dialogue rules 2 is on main** and is what the dialogue chip offers (`S/DialogueRules.cs:35@n`; `S/DialogueRules.Rules2.cs`, `VoterAnswer` `:149@n`, `CaughtLie` `:170@n`). Every "until D2 merges" case is now a rules-2 case; this plan still edits no dialogue file. Dialogue requires story rules at `Threads` or later (`S/DialogueRules.cs:514-516@n`), which every level here is.
- **The owner's answers of 2026-10-30 stand** and are not asked again (§P). Answer 2(b), "off until R7", reaches its end when R7 lands. What follows it the owner answered on 2026-11-13: no chip; each stack is the default at its switch-on (§R).
- Sections A to K, M and N keep their design except where this section or an `@n` note amends them. Their `@m` and `@h` line numbers are as of 2026-10-30; each slice re-reads its anchors on main before it builds.

---

# Critique disposition

1. **Blocker, captions leak the truth. Accepted.** Every report offers both polarities and a neutral answer whatever the truth. `showIf` reads only the player's own evidence. Visible effects are identical across truths, and anything that depends on the truth is applied later by `CheckQuietAsks`, with no line that names it (§E2). A no-leak test pins it.
2. **Blocker, E4 and late reports decided Lost. Accepted.** Verified: `DecidePlays` (`SS/EpisodeEngine.Story.cs:288`) runs before `FireScheduled` (`:304`). The fix:
   - offers only ≥ 2 anchors before the deadline;
   - the report is ready at the last anchor before it, and closes at the deadline;
   - `StoryLapse` (`:258-275`) resolves it first;
   - evidence is judged at answer time (`SettleReport`);
   - the offer closes at the next anchor, never at the deadline;
   - a due report fires beside another play's open beat, and an ask whose report was never drawn by its deadline ends `served`.
   E4 is reworked to "Is {SUBJECT} coming after me?", offered at EvictionNight with a deadline at the next BlockSet (§E1-E2).
3. **Blocker, one number freezes after S1. Accepted.** A ladder of three stacks (12, 13, 14). `Offered` moves only when a whole stack has landed, and stays 0 until Stack A has landed and passed its measurement (owner question 2, answered 2026-10-30). The B9 collision is in owner questions 1 and 2 (§L).
4. **Blocker, Pull and walk-up deadlock. Accepted.** Verified at `RE/EpisodeDirector.NpcInvitations.cs:37-38` and `RE/EpisodeDirector.Story.cs:405-432`. A speaker beat stays out of `CurrentPull` while its walk-up can come. Once set aside it is in `askSetAside` for the phase (offers §C.4's fold of `declinedPulls`) and stays on the episode screen, as every open beat does. In a later phase, while it is still open (a NomsSet beat in VetoSelection, or a report before its deadline), it can walk up again or be (new) raised in the speaker's conversation, but never in the phase it was set aside in. It is a Pull only while `AsksOn` is false, or where the queue never presents it: a phase `PhaseAllows` refuses, active ≤ 3, or a speaker §D1's test leaves out (a cold shoulder, or faith below 30 without a forced chapter) (§D3).
5. **Major, `ReadPerson` as a lie detector. Accepted, with a different fix.** The addendum never reads `ProjectBallot`. It is a keyed coin on the recorded stance (0.6 after a lie, 0.2 after an honest answer). I chose this over a manner-only rewording because a rewording would add nothing the player had not already heard (§G4).
6. **Major, talker items make alliance cards. Accepted.** Talker items are log and memory only, never knowers or claims (§H).
7. **Major, 12 bundles Roles. Accepted** as a plainly stated cost in owner question 2, with the default "no chip until answered". The field alternative is rejected: schema 30's field list is final and a new field takes schema 31 (`P/ALL-STAR-HISTORIES-PLAN.md:370@h`; `S/EpisodeState.cs:252@h`). Every template at 11 or above also needs `RolesOn` (`SS/EpisodeEngine.Story.cs:478`); the acquaintance thread, begun through `BeginThread`, checks it in `SeedAcquaintance`.
8. **Major, airtime. Accepted.**
   - One acquaintance at a time: at most 2 begin a season below 12 seats, 3 chapters each; at 8 seats usually one runs all three.
   - The test is a Between-us play.
   - A priority order: forced verdict, event arc, chapter, pool.
   - The season cap stays 9.
   - Owner question 3 is restated as measured added decisions.
9. **Major, storyline and event ceilings. Accepted** as a report and a subset test (§M, §N). Holding chapters as path steps is rejected: a thread asks nothing itself (`SS/EpisodeEngine.Threads.cs:93-100`). The fallback is fewer acquaintances.
10. **Major, `Castable` collisions. Accepted.** Acquaintance-only chapter templates, one acquaintance at a time, no bond-thread plays, cast-failure rates reported. The attempts citation is corrected to `:70`.
11. **Major, effect kinds. Accepted.** The cold shoulder, learned tell, decoy and once-per-person rules are pure readers of paths and endings. Rapport effects became keyed `Move`s. The remaining `Cool` calls are engine code, not effects.
12. **Major, showmance unreachable. Accepted.** The verdict option writes late-nights' own `Bond`, `Fact` and `Move` (`SS/StoryCatalog.BondsAndSecrets.cs:106-109`).
13. **Major, dialogue collisions. Accepted.** (a) The cast-null rule is dropped; the arcs run the other direction from dialogue's consultations. (b) One honesty model: dialogue rules 2 repeats the week's Told claim and names no ballot of its own, so no joint rule is needed and `Offered` = 12 does not wait on dialogue (§G1).
14. **Major, `AskForIntel` band. Accepted.** The Told standing stores the stated band's score; Neutral writes none; told readings can be wrong (§G5).
15. **Major, knowledge leaks. Accepted.**
    - The stake reads only visible pacts or public bonds.
    - Talkers pass only their own ballot, plan and facts.
    - "Seen with" uses room-act witness memories and noticed invitations, not rapport.
16. **Major, undelivered parts. Accepted.**
    - (a) What you tell a talker travels as `told:` facts.
    - (b) Head-to-head `h2h:` records, a Regard term and competition words.
    - (c) A capped time-together term.
    - (d) Room-act audiences as "seen with".
17. **Major, defaults never change. Accepted** as owner question 2(b). Default: off until R7, because default-on is R7's enrolment.
18. **Major, pitch walk-ups. Accepted, re-scoped.** Pitch walk-ups are the offers plan's slice-4 Pitch source, with its pitch rule (the `ReplyCards.Pending` pitch off the board, the first stored pitch, in `BoardPicks` order, on it; offers §D row 5) and its frame; this plan builds no pitch source. `acq-am-i-safe` casts only when THEM has no pitch.
19. **Major, duplicate "Name them". Accepted.** One pick, classified after the answer, and a lint rule against duplicate captions.
20. **Major, changed minds teach tells. Accepted.** A `said:` stance key at ask time; tells only from real lies.
21. **Minor, mis-cited deferral. Accepted.** The claim is removed. The plan states that row 15 stays deferred (§A).
22. **Minor, Regard in `NpcDeals`. Accepted.** The final-four offer is `NpcDeals`' own choice. The "deal they won't keep" and the `AskForTheVeto` change are removed.
23. **Minor, extra anchor. Accepted.** Dropped. A nominated player gets no new asks, and open asks keep their deadlines.
24. **Minor, Conversation facts. Accepted.** `refId` prefixes, whisper cases per prefix, and confidences read from the path.
25. **Minor, alliance-card evidence. Accepted.** A Listen logs today's `eavesdrop` line. The `listen` and `listen-pact` kinds are removed. Named whispers add a second line. `Spread`'s return is unchanged.
26. **Minor, §14 collisions. Accepted.** The keep-off list is extended, Journal work goes in a new partial file, and `steered-wrong` joins `RelationshipLedger.Permanent`.
27. **Minor, PortVerification. Accepted.** The Notice step runs only at story rules 12 or above.
28. **Minor, tells and voice. Accepted.** There is no Sociable honest default. Sociable liars default to LaidOnThick, and honest tells off a thin margin are spread over all three (on a thin margin they are Hedged). Desperate is never used by `AskVote`. "That's their tell" is reworded as evidence.
29. **Minor, cooldown capacity. Accepted.** The keys and their worst cases are listed, and a capacity test pins them.
30. **Minor, `SeedThreads` loop. Accepted.** The acquaintance is excluded from the loop (`:36`) and from the count (`:39`).
31. **Minor, blindside without a ballot. Accepted.** It is tagged from judged claims for a player who did not vote.
32. **Minor, document errors. Accepted.**
    - The duplicate "I2" is now §J.
    - Final three is a rule of 14.
    - "Prove it." is a TestLoyalty ally's line wherever that ally asks, at any faith from 30 (§D2, §E1, §P `PatronFaith`): the intent sets the line, and no faith band gates it.
    - Words yield to the Pull, invitations and walk-ups.
    - A raised story beat leaves by Back to topics instead of taking over the panel.
33. **Minor, measurement. Accepted.**
    - The walker is built on `BalanceLabNpcWorld`.
    - The minutes gate is relative, because the baseline is already about 51 minutes (`P/ROLE-MOMENTS-PLAN.md:633`).
    - B8 is named for reading time.
34. **Ending 3's weekly item. The owner's call, 2026-10-30 ("Use the defaults for all three").** A confidant passes you one true item a week, with no coin. Confidants pass first at each of `Talkers.Pass`'s anchors, at the first where they hold an Honest-row item and their stance on `talk` is Honest. The `confidant:{week}:{id}` key holds it to one a week, outside the talkers' 2. It is log and memory only, and a confidant who is also a talker never passes one item twice. It lands with `Talkers.Pass` in S4, at 13, because confidant bonds already have writers; ending 3 adds one in S5 (§H, §L, §N, §O).
35. **The verdict's captions. The owner's call, 2026-10-30 ("Use the defaults for all three").** Node 1's one caption is "Where do we stand?" and ending 5's is "You owe me one", both new. Ending 7's is "Clear the air", reused word for word from the bad-blood card's truce, whose `Ease` and `story:made-peace` receipt row 7 writes (`SS/StoryCatalog.HouseRemembers.cs:335`, writers at `:337`), and from the settle-it talk (`SS/StoryCatalog.Plays.cs:382`). Node 1's lapse, which the call did not name, takes "Leave it unsaid" (new), and the forced bane is the first that holds in the order 10, 12, 13, 9, the most specific first: both the lead's defaults, 2026-10-30. Only one bane is forced and standing is a whole number, so node 1 shows one route, and no card shows two controls with one caption (§B3, §B4, §K).
36. **Retries. The owner's call, 2026-10-30 ("Use the defaults for all three").** At most one retry per acquaintance, as §M derives it: B2's `attempts` = 2 lets a chapter be tried once more, and the `star:` rest and the 3-week forced verdict leave room for one. Kept as written.
37. **"How they seem to see you" from 13. The lead's call, 2026-10-30.** The block is filled from 13, in S3. A rules-12 profile holds at most "Their manner" and "What you've learned" (§C, §J, §N).
38. **Asks at the final four. The lead's call, 2026-10-30.** Asks run at active 4 as in any other week, and new ones stop at active ≤ 3 (§D3, §F).
39. **The stake for an HoH's ally. The lead's call, 2026-10-30.** The stake term is an `intel` question whose subject is a nominee this week, asked of a houseguest allied with the HoH (§J).
40. **S9's acquaintance trigger. The lead's call, 2026-10-30.** The trigger is a share of seasons. `AcquaintanceVerdictWeeks` becomes 2 below 12 seats if fewer than half the 8-seat seasons complete one acquaintance, and from 12 seats if fewer than half the 12-seat seasons complete two (§M, §P).
41. **Talkers and the Have-Nots. The lead's call, 2026-10-30.** Talkers pass nothing about the Have-Nots. They are named in the house's public log, and talker items are votes and pacts only (§F, §H).

# Defaults taken

- Story rules 12, 13 and 14 for Stacks A, B and C. `Offered` stays 0 (no chip) until a stack has landed and passed its measurement, then moves stack by stack (owner question 2, answered with its default on 2026-10-30). `Current` was 9 when this was written; it is 10 today, 11 after R7, and 12, 13 and 14 at A-ON, B-ON and C-ON (§Q1, §R).
- No saved field and no command kind. New closed values: `Seen` and `hunch` at 12, `talker` at 13, five receipts at 14.
- One acquaintance at a time: at most 2 begin a season below 12 seats and 3 from 12 seats, 3 chapters each. At 8 seats the calendar usually allows one full acquaintance. Quiet asks: 3 a season below 12 seats and 5 from 12 seats.
- Budgeted asks stay ≤ 9 at 8 seats. A target of ≤ 5 added decisions, measured paired against story rules 11, with a cut order if it is missed (§M).
- Free Notice (6 a week) and Listen (2 a week). A Listen keeps the capped true reveal, logged as today's eavesdrop line. The paid listen-in is refused from 12, with its captions unchanged.
- Talker items are log and memory only, never knowledge: no knower and no claim (§H). What you tell a talker spreads through the keyed mill.
- Every constant is listed in §P of the plan.

# Questions for the owner

**Answered, 2026-10-30.** The owner: "Use the defaults for all six storyline questions". So: (1) story rules 12, 13 and 14 are claimed for this plan and 10 stays the story session's, a delegated call, confirmed by the owner, with B9's later role-moment tuning keyed to 11 and to 15 or above; (2a) the chip may be offered before B9 once Stack A has landed and passed its measurement; (2b) the chip is off by default until R7; (3) a chip season may add up to 5 decisions at 8 seats, measured, with the stated cut order if it runs over; (4) a lingering Listen keeps the capped true reveal; (5) talkers may distort and rarely invent (never about a real alumnus), and what you tell a talker spreads; (6) Have-Nots and production are enough for twists for now.

1. Claim story rules 12, 13 and 14 for this plan, with 10 left as the story session's (recorded as a delegated call), and B9's later role-moment tuning keyed to 11 and 15 or above?
2. (a) May the chip be offered before B9, given that every house-talk number includes the role moments and they would freeze untuned in chip saves? (b) Should the chip be on by default for new seasons, which would be R7's enrolment? Default: (a) yes after Stack A's measurement; (b) no until R7.
3. May a chip season add up to 5 decisions a season at 8 seats (about 2.5 minutes), given that the baseline already runs about 51 minutes against your 45? Default: yes, measured. If not: one acquaintance a season below 12 seats, then a one-node first talk, then 2 quiet asks a season.
4. Should a free lingering Listen still reveal the true band, vote and pact sentence (2 a week, catch risk), or give hunches only? Default: the capped truth.
5. May talkers distort and rarely invent (never about a real alumnus), and may what you tell a talker spread? Default: yes to both.
6. Is reacting to Have-Nots and production enough for twists for now? Default: yes.

---

## R. Overtaken by the owner's answers of 13 Nov

On 2026-11-13 the owner answered the story plan's questions. On this plan's switch: "No it needs to be the default and incorporated into the main game, it makes the house more interactive." So **there is no cast-screen chip, no `StoryRules.Offered` and no cast-screen choice.** Each stack becomes part of every new season when its switch-on PR (A-ON, B-ON, C-ON) moves `Current` to its number (12, 13, 14), once the stack is whole and its numbers pass. A season already started keeps the rules it started with. This section binds over every other section, §Q included. It is not a rewrite: the older lines below stay as written on 2026-10-30, and this list says what replaces each. Line numbers are this file's.

**What replaces each overtaken line.**
- :38, :43-44, :46 (three stacks and "one cast-screen chip"; the chip "off by default" writing `Offered`; `Offered` moving stack by stack; "a chip season plays the built role moments"): no chip. Each stack's switch-on moves `Current`, so every new season plays the stack (§Q1). Every level is at least `Roles`, so every season at 12-14 plays the built role moments.
- :359 (the pitch rule: off the board a pitch is offered only while it is the reply card waiting first; on the board "the first pitch waiting"): overtaken by the owner's answer on pitches. When the player is Head of Household the pitchers walk up, and in a season with the pitch board the walk-up opens the whole board (OF-p). The pitchers also wait in the HoH room, where approaching one opens their pitch (HR-2). The veto board's asks still stay on the board (the owner, 2026-10-30).
- :653, :658, :703, :974, :1163, :1165, :1281, :1288, :1466 ("a chip season", "both chips on"): read "a season at that level". The dialogue chip is a separate chip and still exists.
- :1104 ("Once `Offered` reaches a number"): "once `Current` reaches a number". Nothing changes at a level once it is `Current`.
- :1126 (the chip's caption "The house talks back"): not a caption. The other new captions in that list stand.
- :1156-1160 (a version freezes the first time a shipped path can write it; `Offered` is 0 until Stack A passes; chip-season tests opt in): the rule stands with `Current` in place of `Offered`. Until its stack's switch-on, a test opts in by writing the level's number after `ShippedRules.ApplyFresh`.
- :1162-1168 (role moments bundled into chip seasons; "R7 waits for B9"): moot. B9 has merged, and the role moments are the default (11) before Stack A.
- :1175-1178 (`SeasonBuilder.Choice.HouseTalksBack`, the `ApplyFresh` line after `S/ShippedRules.cs:99@n`, the chip shown while `Offered >= Manners` in `RP/CastSelect.cs`): not built. No plan-32 slice edits the cast screen.
- :1315, :1318 (the harness holds 12 "because `Offered` is 0"; Stack A's measurement comes before `Offered` moves): the harness's hold stands. The measurement comes before A-ON.
- :1353-1354 (the chip mints nothing; `Math.Max(Current, Offered)` and the chip-on/chip-off digests): dropped. The proofs are each level's held digest (`RolesSeasonDigests` from HT0, then `MannersSeasonDigests`, `GossipSeasonDigests` and `ComesToYouSeasonDigests` from A-ON, B-ON and C-ON), byte-equal for every season below the stack's level (§N, §Q1).
- :1509 (labels draw because shipped seasons never reach a surface "while `Offered` is 0"; the cast-screen chip as the exception): every new surface still opens on an installed fixture at its number until its stack's switch-on. There is no chip to check.
- :1527 ("The chip follows §L"), :1561 (`Offered` is 0 until Stack A passes), :1734 (`Offered` moves only when a whole stack has landed), :1796 (`Offered` stays 0, no chip): `Current` moves 11 -> 12 -> 13 -> 14, one switch-on PR each.
- :1611, :1616-1619, :1806, :1809 (owner question 2: the chip before B9; (b) the chip off by default until R7): answered again on 2026-11-13. Each stack is the default when it is switched on. No checkbox.
- :1620, :1810 ("May a chip season add up to 5 decisions"): the limit stands, read as "a new season at that level".
- :1738 ("12 bundles Roles", with the default "no chip until answered"): no chip. The bundling cost is moot, because the role moments are the default first.
- :1749 (`Offered` = 12 does not wait on dialogue): `Current` = 12 does not wait on dialogue.
- :1760 ("defaults never change", default off until R7): each stack is the default from its switch-on.

**Kept.** The "Hunches" filter chip (:940) is a social-web control, not the season chip. The source pin at :1361 is modelled on a test named for the dialogue chip, but it pins Regard out of two files, so it stands as written.

**Owner's answers of 2026-11-13, as they bind this plan.**
- *No checkbox* (above). Each switch-on is an enrolment like R7's (§Q1). Once a level is `Current`, nothing is added at it.
- *Pitches and the HoH room* (outside this plan's slices, in its landing order, §Q3). OF-p: a player HoH's pitchers walk up and open the board. HR-1: a houseguest HoH waits in the HoH room while they decide, and the player makes their case there through the existing plea. HR-2: a player HoH's unanswered pitchers wait by the HoH room, where approaching one opens their pitch. OF-b keeps only same-asker chaining.
- *Numbers.* The owner signs Stack A's numbers before A-ON merges. The lead accepts Stack B's and Stack C's inside §M's limits for him (a delegated call), tells him the numbers at each switch-on, and brings him anything outside the limits, with the cut order.
- *Shared screens with the UI refactor* (a delegated call): whichever is ready first lands first, and the lead merges the other (§Q5).
- *What he plays.* Each switch-on reaches new seasons only: its note says "start a new season". OF-p, HR-1 and HR-2 are presentation and reach a season already started.
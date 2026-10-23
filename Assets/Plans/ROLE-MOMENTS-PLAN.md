# Role moments: NPC events when you win HoH or the Veto, or are nominated

Design proposal v2, 2026-10-22. This version answers the feasibility and experience reviews; Appendix A lists what changed and what was rejected.

**Approved by the owner, 2026-10-22.** Asked how to proceed with the fifteen decisions in §6.3, the owner chose "Adopt all, start building": every recommended answer stands as written. Building starts from main after PR #28's merge (eb092c67), on the frozen schema 29. Slices R0 to R6 land one at a time, each built, run in full on the suites it touches, independently reviewed, committed and pushed before the next (handoffs/OWNER-DECISIONS-2026-10-22.md §3). Enrolment in shipped seasons (R7) waits for the B9 balance work, and the holder's offer (R8) for B9 and a second sign-off, as §6 says.

**Evidence base**
- Unity: D:/GamesimConsolidation at 6909f2bc, branch pc/v2-lifecycle-20261022. Code paths are relative to `Assets/Gamesim/`; story code is under `Simulation/Story/`. Documents at the repository root are named bare (`BALANCE_BASELINE.md`). Tests were read at git HEAD, because another lane is editing `Tests/EditMode`.
- The design was written while PR #28 was open. It merged into main as eb092c67, whose `Assets/Gamesim` equals
  6909f2bc's apart from tests and plans, so the file:line citations hold on main. Schema 29 is frozen; any later
  persisted field takes 30.
- Web reference: D:/gamesim-web at c2f79c9, cited as `web:`.

**Status words**
- **shipped**: plays in every fresh season.
- **gated**: in the code but switched off for shipping.
- **intent**: a doc or comment says it; the code does not do it.
- **missing**: neither.

---

## 0. The short version

**Unity already has most of the mechanics in screenshot 1, drawn as plain text rows.**
- When the player holds the veto, each nominee warm enough to bargain asks for the player's word (`NpcDeals.AskForTheVeto`, Simulation/NpcDeals.cs:368-399).
- The ask carries the C7 price: a final two at 6 or fewer left, otherwise the nominee's vote to keep the player the next time they are on the block (Simulation/Negotiation.cs:261-300).
- Accepting is worth +4 and declining costs -3 (Simulation/PlayerDeals.cs:35,48).
- At the decision, a kept word is worth +24 and a broken one costs -45, charged to whoever broke it (Simulation/DealResolution.cs:51,165-173; Simulation/EpisodeEngine.cs:1032-1040).
- A player HoH gets pitch cards: Promise safety / Hear them / Turn them down, plus a free "Feel them out" (Simulation/HoHPitches.cs:13-38; Simulation/ReplyCards.cs:70-76).

**What is missing against the screenshots and the owner's ask**
1. The card board. There are no portraits, role badges, emotional states or quotes, and no single screen for the offers.
2. The HoH's "Don't Use the Veto". No deal kind exists for it (Simulation/DealState.cs:79-117).
3. Anything that comes to a **nominated** player. The only HoH conversation, "Just a Pawn", ships in backdoor weeks only, so the card itself tells the player a backdoor is planned (Simulation/Story/StoryCatalog.Staging.cs:221,250-266).
4. An answerable "crying in the living room" moment at the role points of the week. Ambient events are told, never answered, by rule (Simulation/HouseEventState.cs:232-238).
5. **Real choices.** Today the free answers have dominant options:
   - leaving an offer unanswered costs 0, while "Turn this offer down" costs -3 (Simulation/EpisodeEngine.cs:1057-1060; Simulation/NpcDeals.cs:435);
   - "Promise safety" is a free +8 for anyone the HoH will not nominate;
   - "Turn them down" (-5) never beats "Hear them" (0).
6. Engine enforcement that the veto saves only one person. Today only the UI locks a second yes (Runtime/Episode/EpisodeDirector.Strategy.cs:156-171).

**The proposal, Layer 1 ("role moments")**

| # | Addition | Built on | Slice |
|---|---|---|---|
| 1 | One **offer board** of portrait cards, for the veto meeting, the HoH's pitches and (later) the holder's offer | existing deal rows and reply cards | R2 |
| 2 | An **emotional state, situation word and in-character quote** on every card, from public facts only. The tone sets real stakes. | a new pure function | R1, R3 |
| 3 | **Silence costs more than a no**, and the engine enforces one veto word | the existing answer and expiry paths | R3 |
| 4 | The HoH's **"Don't Use the Veto"** (`veto_hold`), only as a counter-offer to a nominee's ask | deal ledger, settlement | R4 |
| 5 | A **pitch board**: at most 3 pitches, answerable in any order; a promise pays only when kept | E4 pitch cards | R5 |
| 6 | **Nominated: the "Just a Pawn" conversation** in any week an NPC HoH nominates you, so the card is no longer a backdoor tell | the shipped backdoor beat | R6 |
| 7 | **Quiet tears** (screenshot 2), replacing The Fallout under the new rules | story catalogue, existing airtime | R6 |
| 8 | After B9, with the owner's sign-off: the **NPC veto holder's priced offer** to a nominated player | deal ledger, plus one new mode-2 price origin | R8 |

**One rule for every choice.** Every option set has at least two options, each the best answer in some state the player can see, and the warm option carries a cost or a risk. §3 shows how each moment meets it.

**Gate.** A new story rules version, `StoryRules.Roles` (10 or 11, claimed together with the story session; see §4.2).
- It lives in an existing saved field (`story.rulesVersion`), so there is **no new persisted field, no schema bump and no new command kind**.
- Old saves and old-rule seasons replay byte for byte.
- Answering stays free. Only two optional choices cost the window's seat.

**Layer 2 (later, not in this plan's slices).** This is ROLE_DIALOGUE_ECONOMY_20261012.md's role grants and its +1/0/-1 conversation time, under dialogue rules version 2, once the owner ships dialogue. Shipping seasons run dialogue rules 0 today (Simulation/ShippedRules.cs:57-64).

The owner's decisions come first; they are in §6.3, each with a recommended answer.

---

## 1. What exists today: Unity, the web reference and the screenshots

| Moment | Screenshots | Web reference: what it does, and what it only intends | Unity today | Gap, and where this proposal fills it |
|---|---|---|---|---|
| **Player wins HoH** (before nominations) | Not shown; the owner asks for it | **Missing.** The post-HoH event returns null when the HoH is the player (web:src/systems/phase-event-system.ts:44-78). What exists: deal-coordination popups for pacts already made, "Summon" and "Plan a Backdoor" (each costs an action), and a 30% "Knock at the Door" whose change goes to a store nothing reads. | **Shipped (E4).** One pitch card per NPC whose agenda is Court, up to 24, answered one at a time (Simulation/EpisodeEngine.HoHPitches.cs:10). Fixed text: "I'd put X up. Can you keep me safe?" Answers: Promise safety +8, Hear 0, Turn down -5, Feel them out free (Simulation/ReplyCards.cs:70-76; Simulation/EpisodeEngine.HoHPitches.cs:7-45). | No board and no tone. Up to 7 cards in week 1 at 8 houseguests. Promise is free, Turn down is dominated. → §3.1 |
| **Nominations made** | Not shown | **Intent.** A 0.6 post-nomination pull-aside (Listen, social +2 / Share +5). Its relationship change is lost to the old store nobody reads. | **Shipped.** Each nominee's mood drops 2 steps and stress rises 2. The player's arc moves -8, a nameless memory is written, the nominee holds a grudge of 70 against the HoH, and the HoH's safety promises to them break (Simulation/EpisodeEngine.cs:893-932; Simulation/Story/EpisodeEngine.StoryHooks.cs:25-33). 'tensions-rise' (weight 12) may fire at NomsSet (Simulation/Story/StoryCatalog.PhaseBeats.cs:72-95). | Nothing comes to the nominated player outside a backdoor week. → §3.4a, §3.5 |
| **Player wins the Veto** (offers filed) | Image 1: the nominees and the HoH approach | **Shipped, but only in part.** Every NPC nominee approaches, plus an NPC HoH. Accepting stores the deal only because a reference is changed in place; declining does nothing. Tone comes from the NPC's hidden relationship + 30 (web:src/components/game-phases/POVMeeting/stages/IncomingDealsStage.tsx:347-416). | **Shipped.** `AskForTheVeto` runs at Veto→VetoMeeting (Simulation/EpisodeEngine.cs:353). Every NPC nominee with adjusted warmth ≥ 10 files a Proposed `veto_use`, roll-free, for this week only (Simulation/NpcDeals.cs:368-399). It also asks a player who is on the block themselves, which makes saving yourself a guaranteed -45 break. | The HoH never asks; the trap ask. → §3.2, §3.3 |
| **Veto meeting, before the decision** | Image 1: avatar, target or crown badge, deal chip, "Desperate · Tense", quote, Accept / Decline | As above. No Final-4 guard, and accepting both nominees' asks guarantees a -45 break. | **Shipped as text.** "AN OFFER FROM X", the deal sentence, "Accept this offer" / "Turn this offer down" (Runtime/Episode/EpisodeDirector.Strategy.cs:133-171, drawn at Runtime/Episode/EpisodeDirector.VetoMeeting.cs:219). The second veto yes is locked in the UI only; `RespondToDeal` never refuses it (Simulation/EpisodeEngine.cs:2151-2232). Unanswered asks expire with no change (Simulation/EpisodeEngine.cs:1057-1060). | No board, tone or quote; silence beats a no; exclusivity is UI-only. → §3.0, §3.2, §5 |
| **HoH: "Don't Use the Veto"** | Image 1, third card (crown) | **Intent.** Stored as a permanent mutual safety pact and never checked against the veto decision (web:src/systems/deal-system.ts:161-164). | **Missing.** No deal kind (Simulation/DealState.cs:79-117). `LobbyAsk.Keep` exists only as the player's plea to a holder (Simulation/StrategyRules.cs:480-482). | → §3.3 |
| **Player nominated** | Not shown; the owner asks for it | **Shipped.** "Lobby for Your Life": 4 approaches with real changes, but influence decides the veto only for guest accounts. Campaign budget 2-5. An NPC holder never makes the offer. | **Shipped, player-started only.** The plea to the holder: 1 action out of the after-nominations window's single seat (Simulation/EpisodeEngine.Strategy.cs:19-100; Simulation/StrategyRules.cs:468-482). Campaign pleas, the block speech, 'final-plea' and 'the-flip'. "Just a Pawn" only in a backdoor week (Simulation/Story/StoryCatalog.Staging.cs:221,250-266). | → §3.4a the pawn conversation (R6); §3.4b the holder's offer (R8) |
| **Ambient emotional** ("crying quietly") | Image 2: a toast during the Veto Meeting with "Comfort them" / "Pretend you didn't see" | **Shipped, but its effects are lost.** A 45 s tick fires at 6% on a random NPC; the choice's changes go to a store nothing reads; only the memory and the log persist (web:src/systems/ambient-event-system.ts:94-221; web:src/components/game-hud/GameHUD.tsx:2160-2197). Comfort +5 against Pretend 0 is a dominant answer. | **By rule, narration only** (Simulation/HouseEventState.cs:232-238). Two answerable moments exist: the ported 'legacy-emotional-breakdown', a one-beat story arc at EvictionNight with weight 2.5 (Simulation/Story/StoryCatalog.Ported.cs:91-112), and 'The Fallout' at BlockSet, weight 10, Offer comfort +7 / Keep your distance (Simulation/Story/StoryCatalog.PhaseBeats.cs:131-155). | Nothing at NomsSet or VetoWon. → §3.5 |

The legacy house-event path is off whenever the story rules are on (Simulation/EpisodeEngine.cs:476-480). The breakdown template still reaches shipped seasons as the one-beat story arc above, but rarely.

**Web behaviour not to copy:**
- Breaches charged to the proposer rather than the breaker (web:src/systems/accepted-consequences/deal-outcome.ts:37-58). Unity charges whoever broke the deal (Simulation/NpcDeals.cs:96-112).
- Tone taken from the NPC's hidden relationship, which breaks Unity's privacy rule (Simulation/ReplyCards.cs:117-127).
- A decline with no effect, and ambient choices with a dominant answer (+5 against 0).
- An unenforced `vote_save`, a permanent HoH pact, and no Final-4 guard.
- `Math.random` and `Date.now` ids.

**What the web got right, and this design keeps:** one screen of competing offers; the HoH's ask as a counter to the nominees'; the crying moment's two-line framing.

---

## 2. "The points system" today, and how the new choices plug in

### 2.1 What the owner most likely means

There is no points bank. The currency is **conversations, which are action points**. Each window of the week has its own seats, shown on the HUD as "Actions left" (Runtime/Episode/EpisodeHud.Chrome.cs:545-565). The mockup's "Social Points / Days Left" pill is deliberately not drawn; nothing in the game corresponds to it.

| Window | Phases | Seats |
|---|---|---|
| After HoH | Nomination | 2 |
| After nominations | Veto selection, Veto, Veto meeting | **1** |
| After veto | Campaign | 2 |
| After eviction | Social | max(2, ceil(active/2) - 3) under E1. Move-in night is 2. |

Sources: Simulation/EpisodeEngine.Week.cs:49-81. That is 7 window seats a week at 8 houseguests, and 8 at 12 (7 once ten or fewer remain).

On top of the seats come the week's extras, spendable in any window (Simulation/EpisodeEngine.Week.cs:84-111):
- **Bought time**: at most 6 a week, each costing goodwill (-8 with one houseguest, or -3 with everyone).
- **Story modifiers**: the positive-only social-point bank pays out at 10 points per action; "Good Standing" is +1 action a week for 2 weeks (Simulation/Story/EpisodeEngine.StoryEffects.cs:274-286).
- **Have-Not**: -1.

A window never drops below 1 while it is open. Unspent seats lapse when the window closes. **Nothing refunds an action.**

### 2.2 What costs and what is free today

| Costs 1 action | Free |
|---|---|
| Every social verb, including ProposeDeal and Negotiate (veto for a price) | Answering any offer, including a counter (`RespondToDeal`, kind 31) |
| `Lobby`: any of 4 approaches, once per decider per phase | Answering a reply card, including the HoH pitch and Feel them out (kind 47) |
| House meeting; Study, under the commitment rules | `AskVote` and `ReadPerson`, once per person per week |
| A story choice flagged `costsAction` (Simulation/Story/EpisodeEngine.Story.cs:741-750) | Story choices without the flag (kind 43); house situations (kind 41) |
| The paid dialogue episode (gated: dialogue rules 0 in shipping) | Hearing an NPC invitation |

Answering is free on purpose. The commitment rules cut the accept bonus from +12 to +4 because a free +12 made "accept everything" the dominant play (Simulation/PlayerDeals.cs:39-48). This design keeps answering free and makes **not** answering, and answering carelessly, cost something instead.

### 2.3 How role moments plug in (Layer 1)

All numbers are starting values to tune. "Tone" is the card's emotional state (§3.0).

| Choice | Action cost | Earns or costs (under Roles) | Refund |
|---|---|---|---|
| Accept a role offer (a nominee's veto ask, the HoH's keep-the-block, later the holder's offer) | Free | +4 now. Kept: +24 for a veto word, +8 for keep-the-block. Broken: -45 plus reputation (below). The row's price. | None |
| Turn a role offer down ("an honest no") | Free | 0 to -3 by tone (§3.0) | — |
| Leave a role offer unanswered ("left hanging") | Free | -1 to -5 by tone, when the decision lapses it | — |
| Tone riders | — | Desperate, kept: they owe you a favour. Bitter, kept: their grudge against you eases by 30. Threatening, turned down and it goes against them: a grudge of 30 against you. | — |
| HoH pitch: Promise safety | Free | +8 only at the nominations, if you leave them off. Naming them later breaks the promise. | — |
| HoH pitch: Turn them down | Free | 0 to -3 by tone. If you then nominate them, their nomination grudge eases by 30. | — |
| Just a Pawn: Take their word for it | Free | +3; their safety promise, and **yours to them through next week** | — |
| Just a Pawn: Get it in writing | **1 action** (Roles) | A safety pact with the HoH | — |
| Quiet tears: Comfort them | Free | +5; 50% that the HoH who put them up notices (-3) | — |
| Quiet tears: Sit with them a while | **1 action** (BlockSet only, where the window has 2 seats) | +5 and a favour they owe you | — |
| Breaking an accepted role offer | — | -45 on a veto word. The breaker is read 15 colder by every NPC for each broken deal (Simulation/NpcDeals.cs:33,88-90), takes -12 on every later deal's acceptance odds (Simulation/PlayerDeals.cs:31), and each bystander has a 40% chance to hear of it (Simulation/DealResolution.cs:54-58). | — |

**Deliberately left out of Layer 1:**
- No new currency on the HUD, and **no +1 social point**. The HUD draws no social points, so a bonus there would be invisible (Runtime/Episode/EpisodeHud.Chrome.cs:545-565). Warm choices pay in visible receipts instead: a favour owed, an eased grudge, a promise.
- No +1/-1 conversation time. That needs the dialogue store's time receipts (Simulation/DialogueState.cs:102-126) and, by the ROLE doc's own rule, must never go through the positive-only bank (ROLE_DIALOGUE_ECONOMY_20261012.md:120-127).
- Nothing that lets a free answer deliver a paid plea's effect, which is the owner's §4 regression (handoffs/OWNER-DECISIONS-2026-10-22.md, §4). The one offer that could (the holder's, §3.4b) is priced, replaces the plea rather than stacking with it, and waits for B9.

---

## 3. The event design

### 3.0 Shared machinery

**Who approaches, and in what order.**
- Every filing is roll-free: which offers arrive is a function of state, as in all of NpcDeals (Simulation/NpcDeals.cs:23-25).
- An NPC deals only at adjusted warmth ≥ `DealingFloor` 10 (Simulation/NpcDeals.cs:30). Adjusted warmth is the score minus 15 for each deal the player broke (Simulation/NpcDeals.cs:88-90). So **past breaches cost approaches**: a serial breaker sees fewer offers.
- Cards are ordered by **public** keys: nominees in the order the HoH named them, then the HoH; pitches by the player's own reading. Not by `NpcDeals.Pending`'s order (week, urgency, id; Simulation/NpcDeals.cs:424-430): its id order follows filing order, and filing order follows hidden adjusted warmth.

**The choice rule.** Every option set has at least two options, each the best answer in some state the player can see, and the warm option carries a cost (an action, a relationship elsewhere, a constraint later) or a risk (a check, a chance of being noticed, a commitment that can break). Each moment's spec ends with a "Best when" row.

**The emotional state (tone).** A new pure function, `RoleTone.Of(state, speakerId, role)`, in Simulation so the dotnet subset can test it. It reads **only facts the player already sees** (the rule at Simulation/ReplyCards.cs:117-127 and Simulation/KnownOdds.cs:6-38: never the NPC's hidden view). Most of its inputs cannot change during the week once nominations are made.

| Input | Where the player already sees it | Fixed for the week? |
|---|---|---|
| Role this week: nominee, HoH, holder, courting | Ceremonies | Yes |
| Times nominated this season; replacement nominee | Ceremonies (`timesNominated`, Simulation/EpisodeEngine.cs:912) | Yes, after nominations |
| Endgame (6 or fewer left); jury begun | Public | Yes |
| Personality axes from traits: Bold, Warm, Honest, Vengeful, Sociable, Steady (Simulation/Story/Personality.cs:31-50) | The conversation header names the traits (Runtime/Episode/EpisodeDirector.ConversationNavigation.cs:151) | Yes |
| The player's record with them **before this week**: nominated them; broke or kept a word to them | Ceremonies; the Your Word page | Yes |
| Allied with the player; the player's own reading (close at ≥ 15) | The player's pacts; every card already uses this (Simulation/ReplyCards.cs:150-158) | No (live) |

Mood is **not** an input. Every houseguest starts Neutral (Simulation/EpisodeState.cs:60) and a nomination moves mood two steps down (Simulation/EpisodeEngine.cs:922, ladder at :936), so nearly every nominee reads "Angry"; and nothing in the rules reads a mood (Simulation/EpisodeEngine.cs:937-942).

**The tone ladder.** The first row that matches wins.

| Tone | Rule | Fixed for the week? | Applies to |
|---|---|---|---|
| **Bitter** | The player nominated them, or broke a word to them, in an earlier week | Yes | Every role |
| **Threatening** | Bold ≥ 2 and Vengeful ≥ 2 (for example Competitive with Stubborn) | Yes | Nominee, HoH, courting |
| **Desperate** | On the block for the second time or more this season, a replacement nominee, or the endgame | Yes | Nominee; courting (endgame, or nominated before) |
| **Friendly** | Allied with the player, or the player reads them as close | No | Every role |
| **Earnest** | Warm ≥ 2 (for example Emotional, or Loyal with Social) | Yes | Every role |
| **Calculated** | Steady ≥ 2 or Honest ≤ -2 | Yes | Every role |
| **Pragmatic** | Default | — | Every role |

The three tones that carry a rider (Bitter, Threatening, Desperate) depend only on fixed inputs and are checked first. So a card's rider can never change between the player's answer and the veto decision, and nothing has to be stored. The live tones carry only an answer-time cost, computed in the same command, from the same state, as the card the player saw. Answering one card never changes another card's tone; a test pins it.

The thresholds are starting values to tune.

**The second label.** Two parts, both public:
- a **situation word**:
  - nominee: "First time on the block" / "On the block again" / "Endgame: N left";
  - HoH: "Head of Household";
  - holder: "Holds the veto";
  - courting: "Never nominated" / "Nominated before" / "Endgame: N left";
- **"Your read: Close / Neutral / Hostile"**, from the player's own score (Simulation/ReplyCards.cs:150-158).

Together these replace the web's relationship band ("Tense"), which was the NPC's hidden view.

**History line (optional).** One line from the player's own record with this houseguest, for example "You kept your word to them in week 2", from the Your Word reader.

**What the tone stakes.** Under Roles the tone binds numbers:

| Tone | Turn down ("an honest no") | Leave it unanswered ("left hanging") | Rider |
|---|---|---|---|
| Bitter | -3 | -5 | Accepted and kept: their grudge against the player eases by 30 (`Grudges.Ease`, Simulation/Story/Grudges.cs:87) |
| Threatening | -2 | -4 | Turned down or left unanswered, **and it goes against them** (a nominee left on the block; an HoH who sees the veto used): a grudge of 30 against the player |
| Desperate | -3 | -5 | Accepted and kept: they owe the player a favour, a Hook (Simulation/Story/StorySystems.cs:361-390; at most 32 held) |
| Friendly | -1 | -3 | — |
| Earnest | -2 | -4 | — |
| Calculated | 0 | -1 | — |
| Pragmatic | -1 | -3 | — |

- Without Roles, a decline stays at -3 and a lapse at 0, as today.
- A lapse is a relationship change, not a breach: no Broken status, no witnesses, no adjusted-warmth penalty ("Expiry is not a break", Simulation/NpcDeals.cs:435).
- A favour is redeemed through the shipped Call in the Favour: safety, the veto, or a vote, at weight 30 at HohCrowned, VetoWon and BlockSet (Simulation/Story/StoryCatalog.BondsAndSecrets.cs:499-535). That is the web's "I'll do whatever you need going forward" made real.

**Quotes.**
- Each (role, tone) pair has 3 authored lines of at most 15 words. The line is picked by `StoryRandom.Index(state, "role-quote:" + occasionId, 3)` (Simulation/Story/StoryRandom.cs:39), a pure keyed hash, so a reload, a re-render or a replay shows the same line, and nothing is stored.
- A quote never states terms. The terms line does ("In return: ..."), straight from the row.
- In the jury phase, the Bitter and Threatening lines may name the jury, which is a public fact.
- Starting lines are in each section below.

**Built, 2026-10-22 (R1).** `RoleTone` and `RoleQuotes` (Simulation/Story/RoleTone.cs, RoleQuotes.cs) hold the ladder, the situation word, "Your read", the stakes table as data and the catalogue. Nothing reads them yet. These calls were made where this section left room:
- **The holder is never Threatening.** The ladder table above does not list the holder for Threatening, but §3.4b gives the holder a Threatening line. R1 follows the table, so that line is not in the catalogue. If R8 wants it, the fix is one entry in `RoleTone`'s applies-to table plus three lines.
- **Every line holds for every cause of its tone.** Bitter comes from a nomination or a broken word. Desperate comes from the endgame, an earlier nomination or a replacement. Four starting lines held for only one cause, or made a promise no rider keeps, so they were reworded:
  - §3.2 Bitter "You put me up before..." became "After what you did, the least you can do is pull me off."
  - §3.1 Bitter "You put me up once already..." became "You've burned me before. Don't do it again with your nominations."
  - §3.1 Desperate "There's hardly anyone left..." became "Please don't put me up. I can't afford a week on the block."
  - §3.1 Earnest "I'd never come after you..." became "I'm no threat to you. I'd put {TARGET} up."
- **Quiet tears has lines.** Its subject is a nominee, so it has all seven tones and 21 lines, spoken as the player finds them. R6 may leave them unused. The player may have put that nominee up this week, which the ladder counts only from the next week, so they can read Friendly or Earnest. No line welcomes the player as the one they hoped to see, and the moment's own options already turn on who put them up (§3.5).
- **A word broken this week counts from the next, whenever it was made.** Bitter reads the week the word broke: the record's settlement for a deal or a promise, the breach's week for an oath. A promise made last week and broken at this week's nominations does not turn its card Bitter mid-week.
- **A pitch that recommends nobody names nobody.** Every pitch tone has at least one line without {TARGET}. With no target, the keyed pick steps on to the next such line, so no card says "someone else".
- **The endgame counts the house as the week began.** The count includes anyone evicted or removed this week, so the eviction cannot flip Desperate or "Endgame: N left" while a card is still open. When the endgame applies, it wins over "On the block again" and "Nominated before".
- **No jury lines yet.** The lint keeps "jury" out of every line, along with numbers, the price kinds (final, vote, bloc, safety) and "in return". A holder's line may still say there is a price or a cost; only the terms line says what it is. Only the Desperate veto ask may promise anything (the favour its rider writes), and only Threatening lines may threaten.
- **Reloads.** The subset proves a Newtonsoft round trip. An EditMode test (`RoleTonePersistenceTests`) also round-trips the veto meeting through the shipping save store. R3's reload mid-board should go through the store too.

**Consequences go through the writers that already exist:**
- `EpisodeEngine.Change` for the player.
- Deal settlement for kept and broken deals, with the breaker recorded and NPC breakers turned into Betrayals (Simulation/EpisodeEngine.cs:1032-1040).
- The story effect router: `ChangeKeyed`, `Move`/`View`, `Grudge`/`Ease`, `Hook`, promises, deals, and memories that stay nameless (Simulation/Story/EpisodeEngine.StoryEffects.cs:7-27).
- No parallel relationship or promise authority.

### 3.1 Player wins HoH: the pitch board

| | Spec |
|---|---|
| **Fires** | HoH→Nomination, where `CourtTheHoH` runs (Simulation/EpisodeEngine.cs:356) and calls `HoHPitches.Offer` (Simulation/HoHPitches.cs:27-38). Unchanged. |
| **Who** | NPCs whose agenda is Court, as today. **Under Roles, at most 3 per nomination: the two the player reads warmest and the one the player reads coolest** (the player's own score; ties by id). The ranking uses only the player's own reading, so it shows nothing hidden. Today every NPC without a pact courts in week 1, which is up to 7 cards at 8 houseguests. |
| **Order** | **Any order under Roles.** The check `Require(ReplyCards.Pending(s)?.id == card.id)` (Simulation/EpisodeEngine.HoHPitches.cs:10) accepts any pending pitch. |
| **Card** | Portrait, tone, situation word, "Your read", quote, and "Recommends: {TARGET}": the card's `aboutId`, their own top `ThreatAssessment.RankedTargets` entry other than the player and their allies, shown as their recommendation as today (Simulation/HoHPitches.cs:33-36; Simulation/ReplyCards.cs:138-140). |
| **Quotes** | Bitter: "You put me up once already. Don't do it again." Threatening: "Put me up and you'd better win next week too." Desperate: "There's hardly anyone left. Please don't put me up." Friendly: "You know I'm with you. Put {TARGET} up and keep me off it." Earnest: "I'd never come after you. I'd put {TARGET} up." Calculated: "{TARGET} is the bigger problem for both of us." Pragmatic: "I'd put {TARGET} up. Keep me safe." |
| **Choices (captions unchanged)** | **Promise safety**: the Safety promise as today, but under Roles **no warmth at the answer; +8 at the nominations** for each one the player leaves off the block, written in `Nominate`. Naming them later, as a nominee or replacement, breaks it under the promise rules (Simulation/EpisodeEngine.cs:929-932). **Hear them without committing**: 0. **Turn them down**: the tone's decline cost (0 to -3) instead of -5. If the player then nominates them or names them as replacement, their nomination grudge of 70 eases by 30, because honesty softens the blow; Threatening pitchers get no discount. **Feel them out (free)**: a Told standing, unchanged. Unanswered pitches clear at nominations at no cost, as today (Simulation/EpisodeEngine.cs:898). |
| **Best when** | Promise: you will neither nominate them nor need them as replacement. Hear: you are undecided. Turn down: you will nominate them. |

### 3.2 Player wins the Veto: the incoming-offers board (screenshot 1)

| | Spec |
|---|---|
| **Fires** | Filing: Veto→VetoMeeting, where `NpcDeals.AskForTheVeto` runs (Simulation/EpisodeEngine.cs:353); the HoH's ask (§3.3) is filed right after it. Display: the veto decision screen in place of the `VetoOffers` rows (Runtime/Episode/EpisodeDirector.VetoMeeting.cs:219), and the same board in the Diary Room's veto column. |
| **Who** | Each NPC nominee with adjusted warmth ≥ 10, not already holding a binding veto deal with the player, while the veto can still be used (Simulation/NpcDeals.cs:370-382). **Under Roles, a nominee does not ask a player who is also on the block.** That ask is a trap: saving yourself breaks it (-45). |
| **Card** | Portrait; target badge; "Veto Commitment" / "Save me from the block with the Power of Veto"; tone; situation word; "Your read"; quote; terms ("In return: ..." from `Negotiation.AskPriceLine`, Simulation/Negotiation.cs:336-340); a stakes line in words; the history line; Accept / Turn down. |
| **Terms** | The C7 price, unchanged: a final two at 6 or fewer, otherwise the nominee's vote to keep the player the next time they are on the block (Simulation/Negotiation.cs:276-300). |
| **Quotes** | Bitter: "You put me up before. The least you can do is pull me off." Threatening: "Leave me up there and I won't forget it." Desperate: "Save me and I'll owe you. I mean it." (true: the rider writes the favour) Friendly: "We're in this together. Pull me off the block." Earnest: "I'm not asking for a miracle, just a chance to stay." Calculated: "Run the numbers. Me staying is better for your game." Pragmatic: "I know we haven't always seen eye to eye, but save me." (web) |
| **Consequences** | Accept: +4; the deal goes Active and the price is struck (Simulation/EpisodeEngine.Negotiation.cs:133-151). Kept (the veto used on them): +24 (8 × Critical). Broken (not used while they were nominated): -45, charged to the player, price voided (Simulation/EpisodeEngine.Negotiation.cs:165-195). Turn down or leave it: the tone table in §3.0. Riders: §3.0. |
| **One veto word, enforced by the engine** | Under Roles, `RespondToDeal` refuses a second veto yes (a `veto_use` after a `veto_use` or `veto_hold` yes, and the reverse) with the line the engine already has: "You have already given your word on the veto this week, and it saves one of them." (Simulation/Negotiation.cs:716). One helper, `Negotiation.VetoWordGiven(s)`, serves three places: `RespondToDeal`; `VetoPriceRefusal`, which has the check inline today (Simulation/Negotiation.cs:706-716); and the showmance option that writes a `veto_use` (Simulation/Story/StoryCatalog.BondsAndSecrets.cs:170-171), hidden by `ShowIf` under Roles once a word is given. The UI's own line (`VetoSavesOneLine`, Runtime/Episode/EpisodeDirector.Strategy.cs:208-215) stays for the lock reason. |
| **Information revealed** | That a nominee asked still shows only that their adjusted warmth is ≥ 10, as today. |

**A worked week.** Week 3, eight in the house, and the player won the veto.
- Danielle is on the block for the second time, so she reads **Desperate**. Marcus, a first-time nominee, reads **Pragmatic**. Priya, the HoH (Strategic, Analytical), reads **Calculated** and asks the player to keep the block.
- **Save Danielle.** Accept her (+4, kept +24, her vote next time, and a favour). Turn Marcus down (-1). Turn Priya down (0) or leave her (-1); using the veto then costs -5 with Priya and a grudge of 15.
- **Save Marcus.** The mirror image, but Danielle's no costs -3 (-5 if left).
- **Keep the block.** Accept Priya (+4, kept +8). Turn both nominees down (-3 and -1).

No answer is free and none is always best. The right one depends on who is desperate, who holds power next week, and whom the player needs.

**Built, 2026-10-23 (R3).** The veto board's economics, every rule under `RolesOn` only (Simulation/Story/EpisodeEngine.RoleMoments.cs). These calls were made where this section left room:
- **One word, one test.** `Negotiation.VetoWordGiven(s)` is the old inline check of `VetoPriceRefusal`, unchanged, over `Negotiation.IsVetoWord(kind)`, which is `veto_use` today; R4 adds `veto_hold` there and every place follows. Under Roles, `RespondToDeal` refuses a yes to any veto word once one is given, in the engine's line (`Negotiation.VetoSavesOneRefusal`), before it moves, draws or logs anything. The UI's lock line stays.
- **The showmance's promise, both ways.** `ShowIf` keeps "Promise to use it on them" out of a beat drawn after a word is given. A beat drawn before it (it can wait for airtime) is caught by one helper, `EpisodeEngine.VetoWordRefusal(state, choice)`: under Roles, `ProgressStoryline` refuses an option whose effects give the player's veto word once one is given, and the director draws that option locked, with the note "You gave your word on the veto" on the tile (the verification walk skips it too). A story promise given first counts as the word, so a later yes is refused.
- **No promise beside a waiting ask.** Under Roles the `ShowIf` also hides the promise while the partner's own ask waits on the board. The story writes no deal while a matching ask binds the pair, so the promise would have said "You promised {PARTNER} the veto" and written nothing, and the waiting ask would then lapse at its tone's cost. The board's yes is that word, and it strikes the price. The asks are filed before the VetoWon beats are drawn, so a drawn promise never meets an ask filed after it.
- **Only the board's asks settle.** `RoleOffers.IsVetoAsk` takes only the asks `AskForTheVeto` files (`deal-veto-`). A houseguest's decision on a veto they hold settles no board: R3's first build read and settled one there, and a favour called in ("Ask for the veto", a `deal-story-` word from the holder to the player) was settled as a kept Desperate ask, giving back the favour the player had just spent. That path is as it was before R3.
- **The tone the player saw.** A no is charged inside its own command, from the state before the answer moves anything. Silence and the riders are read in the decision's command before two things that can move a card. The first is `Close`, which fires the house's last beats: a nominee's beat can seek the player out and lift the player's read past Close, which turned an Earnest card Friendly in a test season. The second is `ResolveVeto`, whose save lifts the player's read of the saved nominee by 20 to 30. Silence and the riders are settled straight after `ResolveVeto` returns, so its signature is unchanged; a test reaches it by reflection. Both readings are pinned by tests, and moving either one later turns them red.
- **Silence is charged whatever the decision.** A nominee left hanging pays the tone's lapse even when the veto saves them, as the stakes table says: the lapse is for the silence, and the save pays its own +25. "Turn it down" and "Leave it" stay the dominated answers to a nominee the player means to save; "Accept" is the best answer there.
- **What silence writes.** `Change` with a new ledger type, `deal_lapsed`, which fades like any untyped change and is never permanent. Its note is "Left my veto commitment unanswered." The player's line is "You left a veto commitment from {name} unanswered." (kind `deal`, so the weekly recap lists it). There is no Broken status, no `brokenById`, no witnesses and no adjusted-warmth mark, and a Calculated no (0) still goes through `Change`, as every no always has.
- **The riders go through the story effect router**, as a story option's effects do: `Ease` (30), `Hook` (`Hooks.Create`, which holds 32 and makes room only by dropping spent favours; with no room nothing is owed and nothing is said), and `Grudge` (30, `GrudgeCauses.Story`). A favour that lands is told: "{name} owes you a favour for your word on the veto." Taken and broken, a Threatening nominee holds only the breach's grudge.
- **The trap skip** returns before any filing, so a nominated holder is asked nothing.
- **The board's words.** `RidersSettle` is true. Each card gains an answers line in words, never a number: "Turn it down: costs {x}. Leave it unanswered: costs {y}." The scale is nothing (0), a little goodwill (-1), some goodwill (-2, -3) and a lot of goodwill (-4, -5). Silence is the dearer word for every tone, and the line is at most 14 words. The head says "Silence costs more than a no." at the right-hand end of the board's line, under "Answering uses no conversation", and under the line where the width cannot hold both.
- **The quote's key** is now the week and the asker (`RoleOffers.QuoteOccasion`: `veto-ask:w{week}:{askerId}`), not the row's id. Ids follow the filing order, which follows hidden warmth: the same two nominees asking with their warmth the other way round swapped the ids, and with them both cards' lines. §5.3's stricter test (re-file with the order swapped, same askers) now holds, and a test pins it. A nominee asks once a week, so the key names one occasion, and nothing is stored.

### 3.3 The HoH's "Don't Use the Veto": `veto_hold`

| | Spec |
|---|---|
| **Fires** | Veto→VetoMeeting, right after `AskForTheVeto`, in a new `NpcDeals.AskToKeepTheBlock`. Roll-free. Roles seasons only. |
| **Who** | The HoH, when **all** of these hold. **(1)** The HoH is an NPC. **(2)** The player holds the veto and is not nominated. **(3)** The veto can be used. **(4)** Adjusted warmth (HoH→player) ≥ 10. **(5) At least one nominee's ask is on the board** this week, so the HoH's ask is always a counter-offer that creates a dilemma, never a standalone bonus. **(6)** The HoH has **no backdoor plan**, read through `EpisodeEngine.BackdoorPlanned(s) == null` (Simulation/Story/EpisodeEngine.StoryHooks.cs:533-542). It is *not* read through `backdoorTargetId`, which only the player's own plan writes (Simulation/EpisodeEngine.cs:1596). A backdooring HoH wants the veto used. **(7)** The 200-deal ceiling has room. At most one per week. |
| **Row** | `DealState`: id `deal-keep-{seq}`; type `veto_hold`, a new `DealKind.VetoHold`. Status Proposed, `expiresWeek` this week, trust **High**, proposer the HoH, recipient the player. The kind: **(a)** is appended at the **end** of `DealKind.All` (Simulation/DealState.cs:107-112), so readers that walk `All` in order keep their matches; **(b)** has its own `Title`, "Keep-the-Block Deal". The default title is "Partnership" (Simulation/DealState.cs:162), which would make `YourWeek.ReadDeal` misread its lines (Simulation/YourWeek.cs:387). **(c)** Is Roles-only, as the final three deal is commitment-rules-only (Simulation/DealState.cs:117; validation at Simulation/EpisodeValidation.cs:306). **(d)** Is always refused by `PlayerDeals.CanPropose`, so `PlayerDeals.Available` never offers it to the player (Simulation/PlayerDeals.cs:63-135,168-174). Card chip: "Don't Use the Veto" / "Keep the nominations the same". |
| **Price** | **A final two at 6 or fewer**, if one does not already bind them; otherwise **none**. No vote price: the nominees' asks already hand out vote insurance, and stacking a second source across weeks would layer survival votes. A final two is a plain deal row in every mode, not a Vote or Safety row, so it needs no mode-2 origin (Simulation/EpisodeEngine.Negotiation.cs:139-145; the raw-to-raw link rule at Simulation/UnifiedVoteFamilyValidation.cs:536-540). |
| **Quotes** | Bitter: "You've crossed me before. Don't do it again with that veto." Threatening: "Use that veto and you'll have made yourself a problem." Friendly: "I put them up for a reason. Trust my game." Earnest: "I thought hard about this block. Please leave it as it is." Calculated: "I'm asking you not to change my nominations." (web) Pragmatic: "Keep it simple. Leave the nominations alone." |
| **Choices** | "Accept this offer" / "Turn this offer down" (the existing captions), or leave it. |
| **Consequences** | **Accept:** +4; the row goes Active and any final-two price is struck. It locks every `veto_use` yes this week, and an earlier `veto_use` yes locks it. **At `ResolveVeto`**, a new `DealResolution.Hold` verdict joins the Vetoes family: **not used → Fulfilled, +8.** A one-line kind rule in `DealResolution.Impact` keeps it at the Low weight, because leaving the block alone is what most holders do anyway. **Used → Broken by the player, -45:** High is raised to Critical for an accepted offer (Simulation/DealResolution.cs:80-81; Simulation/DealState.cs:239-246). The usual witnesses, the -15 adjusted warmth and the Your Word reading follow, and the HoH's price is voided. **Turned down or left unanswered:** the tone cost, and **if the player then uses the veto, -5 with the HoH and a grudge of 15 (30 if Threatening)**, because they asked and were defied. **Bitter, kept:** their grudge eases by 30. An unanswered ask lapses at the decision with the `veto_use` asks (extending Simulation/EpisodeEngine.cs:1057-1060). |
| **Best when** | Accept: you will not use the veto, and the HoH matters to you next week. Turn down: you might use it and want to pay least. Leave it: never, which is the point of the lapse cost. |
| **Readers to update** | `DealSentence`; NpcInvitation's opening line (Simulation/NpcInvitation.cs:79); ConversationExchange's accept and refuse lines; `WaitingOnYou`, so it lapses at the veto decision rather than when the campaign opens (Simulation/WaitingOnYou.cs:96,168-182); the eviction vote's pair-deal table (an Active `veto_hold` binds nothing at the vote: 0); the jury obligations; the weekly recap; the Your Word page; `FinalistRead` (Simulation/FinalistRead.cs:473); `CommitmentsRead` (Simulation/CommitmentsRead.cs:422,451); `YourWeek` (Simulation/YourWeek.cs:808); `PortVerification` (Runtime/Episode/PortVerification.Season.Systems.cs:519). |
| **Information revealed** | The ask is the HoH's own act. It tells the player what the HoH *says* they want, which is the point of the moment. A backdooring HoH never asks. The absence of an ask has many causes (no nominee asked, too cold, a plan), so it proves nothing in particular. |

**Built, 2026-10-23 (R4).** The keep-the-block deal, every rule under `RolesOn` only (`NpcDeals.AskToKeepTheBlock`, `DealResolution.Hold`, `EpisodeEngine.RoleMoments`). These calls were made where this section left room:
- **Every condition is checked where the ask is filed**, not inherited from `AskForTheVeto`, so each one stands on its own. "A nominee's ask on the board" is one still waiting from somebody on the block (`RoleOffers.VetoAsks`). "At most one a week" counts any `deal-keep-` row of the week, answered or not, so a filing never repeats.
- **The row's trust is the kind's own.** `DealKind.DefaultTrust(VetoHold)` is High. Kept, it weighs Low (`DealResolution.KeptWeight`), and the jury's kept weight reads the same rule, so a kept hold is +8 on the ledger and +20 with the jury, not +16 and +40.
- **The price** is `Negotiation.VetoPrice(..., FinalTwo)`: a final two at six or fewer that does not already bind them, struck with the yes as a plain row in every mode and voided by the player's breach, as a nominee's is. The prefix (`deal-keep-`) joins `DealResolution.AcceptedOffer`, so its breach and its price's breach weigh one step heavier.
- **Defied**: the -5 is a `Change` with a new ledger type, `veto_defied`, which fades like `deal_lapsed` and is never a breach. The grudge goes through the story effect router (`GrudgeCauses.Story`): 15 (`RoleTone.DefiedGrudge`), or the Threatening rider's 30 in its place, never both. The player is told: "You used the veto after {name} asked you not to." The line never says "broke", so the weekly recap does not count it as a broken word. Whether the veto was used is read from the week's power row, which `ResolveVeto` writes.
- **Lapse**: `ResolveVeto` lapses every unanswered word on the veto (`Negotiation.IsVetoWord`), so the hold lapses with the nominees' asks. `WaitingOnYou` lists it as a question about the veto that goes with the decision.
- **The card** follows the nominees' (`RoleOffers.BoardAsks`, `CardOf`, `KeepTheBlockCard`). Its quote is keyed `keep-the-block:w{week}:{hohId}`. Its stakes lines, 14 words each, name all three outcomes:
  - default: "Keep it: they remember it. Break it: the house may hear. Veto anyway: personal."
  - Bitter: "Keep it: their grudge eases. Break it: the house may hear. Veto anyway: personal."
  - Threatening: "Keep it: they remember. Break it: the house may hear. Veto anyway: very personal."

  R4's review changed these from §5.1's mock ("Keep it: they remember it. Use the veto anyway: they take it personally."). The mock's line covered two outcomes that differ a great deal. A yes followed by a save is a breach: -45, the witnesses, -15 with the house, and the final two voided. A no or silence followed by a save costs -5 and a grudge. A nominee's card warns that a breach is public ("Break it: the house may hear"), so this card now says so too, in the same words.
- **Locks, both ways.** The text and the decision moved into `Negotiation.VetoWordLockLine`, so the subset tests them; the director forwards to it. After the hold's yes, a nominee's card says "You have already given {HoH} your word to leave the nominations as they are." After a nominee's yes, the hold's card says "You have already given {nominee} your word on the veto." Each ends "Turn {name} down, or the offer lapses at the meeting." `VetoPromisedTo` and `VetoSavesOneLine` moved there too, unchanged.
- **Readers decided**:
  - wording added: `DealSentence` (`Negotiation.KeepTheBlockSentence`), NpcInvitation, ConversationExchange, `CommitmentsRead` (noun, what it binds, the veto decision's dry run and warning: "Saving Maya breaks your keep-the-block deal with Priya."), `FinalistRead.DealBreaker` (on the record), `YourWeek.DealWords`.
  - the eviction vote's pair table: 0.
  - unchanged: `StrategyRules.DealWeight`, which stays 0. A standing hold also counts as "a deal between them" for the +10 on lobbying odds (KnownOdds, StrategyRules, Negotiation), as any deal does.
  - `NpcDeals.Urgency` is 190, just under the veto asks.
  - the player can never propose it (`PlayerDeals.CanPropose`, in the words an unknown kind gets).
- **Validation** refuses the kind or the prefix in any week the role moments did not play (`EpisodeEngine.RolesOnIn`, `RolesOn`'s own test at that week).
- **A story's keep-the-block deal** is written only under the role moments, as a story's final three deal is written only under the commitment rules. No catalogue option writes one today. Under them it is a `deal-story-` row: never a card, and never charged a card's stakes. The veto decision still judges it, because it is the player's word.
- **The recorded-season digests.** `CommitmentRulesSeasonDigests` and `ModeOneSeasonDigests` leave the new kind out of their per-kind odds traces, by its spelling, as C9 left the final three deal out. The player can never propose it.

### 3.4 Player is nominated

#### 3.4a The pawn conversation: "Just a Pawn" in every week, not just backdoor weeks (R6)

| | Spec |
|---|---|
| **Fires** | At nominations, when an NPC HoH names the player as an initial nominee. Under Roles it is decided by a keyed coin, `StoryRandom.Chance(s, "w{week}:pawn-talk:{hohId}", 0.5)` (tunable), and comes **at most once per HoH per season**. It is rule-driven, like the backdoor arc ("neither drawn from the pool nor held behind the Game lane", Simulation/Story/EpisodeEngine.StoryHooks.cs:509-528). |
| **Which variant** | **Backdoor week** (`BackdoorPlanned(s) != null`): the backdoor arc's own "Just a Pawn" beat (Simulation/Story/StoryCatalog.Staging.cs:250-266). Under Roles, its opener's routing (:221) takes the same coin and goes "quiet" when the coin says no. **Any other week:** `the-pawn-talk`, a Roles template whose NPC-held opener and beat copy the backdoor's: same title, text, captions, venue and closing anchor (VetoWon). It is started at the same point, so it waits for airtime exactly as Just a Pawn does. |
| **Why one card** | Today the card appears only in backdoor weeks, so it tells the pawn that the HoH has a plan. Under Roles both variants come on the same coin, look the same and wait the same way. The card becomes **a claim, not a tell**. Only paying for "Call it what it is" reveals more, as it does today. |
| **Text (shipped)** | "{HOH} calls you up to the HoH room before the veto players are drawn. 'You're not the target. You're a pawn. I need you to trust me on this.'" The claim never depends on the HoH's hidden nomination ranking (ROLE_DIALOGUE_ECONOMY_20261012.md:71). |
| **Take their word for it** (Warm) | Shipped: the HoH's safety promise. **Under Roles** it also costs the player **their own word**: a Safety promise to the HoH through next week. If the player wins HoH next week and nominates them, the existing promise breach follows. Plus +3 with the HoH. The description says both. |
| **Get it in writing** (Calculated, locked unless Analytical, as shipped) | Shipped: a safety pact with the HoH, which weighs in the HoH's tie-break ballot this week. **Under Roles it costs the window's seat** (`costsAction`), because a word with the person who decides belongs to the strategy windows (owner, 2026-09-27: Assets/Plans/StoryArcs/IMPLEMENTATION-LOG.md:122-125). |
| **Call it what it is** (Bold, locked unless Confrontational, as shipped) | Shipped: -6 from the HoH. In a backdoor week they name the target (shipped). In any other week: "There's no one else. One of you two is going home." That is the truth about whether a plan exists, earned and paid for, and never which nominee they prefer. |
| **Say nothing** (the existing lapse caption) | 0. |
| **How the Roles changes stay out of old seasons** | Each changed option is a twin with the same caption, shown by `ShowIf` under Roles; the shipped option is shown otherwise. An old season draws exactly the shipped choices. Four options is the ROLE doc's ceiling (ROLE_DIALOGUE_ECONOMY_20261012.md:90). |
| **Best when** | Take: you will not target them next week. In writing: a tie-break could decide your week and you have the seat. Call it: you want the truth and can afford -6. Say nothing: you plan to come after them. |

#### 3.4b The holder's offer: "I'll use it on you, for a price" (R8, after B9)

The owner asked for this moment, and it is the screenshot's mirror image. It is also the one moment that could move the balance the wrong way: the fixed-0.50 Studier already wins the season 73.5% of the time at 8, against a 25-35% target (REMAINING_WORK_20261012.md:63,67). So it is designed in full here but built last, behind B9 and the owner's sign-off.

| | Spec |
|---|---|
| **Fires** | Veto→VetoMeeting, in a new `NpcDeals.OfferTheVeto`, after `AskForTheVeto`. Roll-free. Roles only, once R8 ships. |
| **Who** | The veto holder, when all of these hold: **(1)** they are an NPC, neither nominated nor the HoH; **(2)** the veto can be used; **(3)** the player is on the block; **(4)** adjusted warmth (holder→player) ≥ 10; **(5)** the player has made no Save plea to them this week; **(6)** no veto word binds the two of them; **(7)** the player holds no favour over them, since Call in the Favour already covers that case (Simulation/Story/StoryCatalog.BondsAndSecrets.cs:524-526); **(8)** neither `StoryConsumers.WillNotSave` nor `ColdPartnerLeavesThemUp` applies (Simulation/EpisodeEngine.cs:844-845,871-877); **(9)** a price fits; **(10)** with the commitment's +40 (Simulation/StrategyRules.cs:55,473), the player would be the nominee the holder saves: `SaveWeight` + 40 passes `VetoLine` and tops the other nominee (Simulation/EpisodeEngine.cs:829-846). **Whether they would have saved the player anyway does not matter**, so an offer never proves "they would not save you otherwise". At most one per week. |
| **Row** | `veto_use`, id `deal-save-{seq}`, proposer the holder, recipient the player, trust Critical, this week only. `DealResolution.Veto` already reads the deal from either side (Simulation/DealResolution.cs:165-173). |
| **Price (the player pays, due this week)** | The first that fits, chosen **from public facts only**: **(1)** a voting bloc with the holder **this week** (`VoteTogether`, judged at this week's reveal; Simulation/DealResolution.cs:185-192), which the player can only pay if saved; **(2)** at 6 or fewer, a final two. If no price fits, no offer is made. |
| **Quotes** | Bitter: "You put me through it before. This will cost you." Threatening: "I'm the one holding it now. Here's my price." Friendly: "I want to pull you off. I just need something back." Earnest: "I'd like to use it on you. Meet me halfway." Calculated: "I could use it on you. Here's what it costs." Pragmatic: "It's yours, for a price." |
| **Accept** | +4; the price row is struck, owed by the player and linked to the offer. **It counts as the player's plea to this holder this week:** `StrategyRules.CanLobby` refuses a Save plea to them ("You already have their word."). While the offer is pending, a Save plea to them is refused ("Answer their offer first."). So the offer and the plea **substitute** for each other and never stack. |
| **Holder saves you** | Fulfilled, +24. The player owes the bloc at this week's vote. |
| **Holder does not** | Broken **by the holder**: -45 in the player's direction, the holder marked as breaker, a Betrayal if they were the player's ally (Simulation/EpisodeEngine.cs:1036-1040), and the player's price voided (`VoidThePrice`, Simulation/EpisodeEngine.Negotiation.cs:165-195, with its line reworded for a price the player owes). |
| **Turn down / leave it** | The tone cost; the holder column has no rider. After a no, the player may still plead with the seat (1 action, rolled). |
| **Best when** | Accept: you would rather pay a certain price than gamble the seat on a plea. Turn down and plead: you can afford the seat and want to keep your vote. Leave it: never. |
| **Information revealed** | The same as a nominee's veto ask today (warm enough to deal), plus "within reach". The absence of an offer proves nothing in particular: no price fits, a favour is held, the holder is too cold, a story refusal applies, or the save is out of reach. |
| **Mode 2** | Every fresh season plays mode 2 (Simulation/ShippedRules.cs:58-64). Its admission and validation accept a Vote price only on a nominee's `deal-veto-` ask, paid by the nominee as a VoteSave naming the player (Simulation/UnifiedVoteAdmission.cs:212-260; Simulation/UnifiedVoteFamilyValidation.cs:539-555). `VetoPriceFits` refuses any player payer (Simulation/Negotiation.cs:292). **R8 adds one new origin** for a player-paid voting bloc linked to a `deal-save-` offer, with its own provenance checks in admission and validation, refused without Roles. The final-two price is a plain row and needs none. |
| **Regression test** | A sibling of 60d8f629 (a public Cite at AP 0 delivers no paid plea). At 0 actions, accepting moves the holder's decision **only** when a binding price owed by the player is recorded, and pull(offer and plea) ≤ max(pull(plea), pull(offer)) holds because the second one is refused. |

### 3.5 Emotional moments: quiet tears (screenshot 2)

Ambient narration stays told, not answered (Simulation/HouseEventState.cs:232-238). The answerable version is a **story moment**. It shares the existing airtime (2 asks a week, one open at a time; Simulation/Story/EpisodeEngine.Story.cs:87-105) and renders above the decision through `PendingStoryBeats`, so it can sit on the veto meeting as the toast does in the screenshot.

| | `quiet-tears` |
|---|---|
| **Template** | Lane Moment, `rulesVersion = StoryRules.Roles`, built with `PhaseMoment` once it takes a `rulesVersion` parameter. Today `PhaseMoment` hard-codes `StoryRules.Grudges` (Simulation/Story/StoryCatalog.PhaseBeats.cs:35), which would make the template castable in every old season. The parameter defaults to Grudges, so every existing phase beat is unchanged. |
| **Anchors** | NomsSet, VetoWon and BlockSet, **at most once a week** across all three. Not at VetoWon while the player has a board up (they hold the veto with asks pending, or a holder's offer is pending): one interruption at a time. Not in week 1 before its eviction night, by the story's first-ask rule (`BeforeTheFirstAsk`, Simulation/Story/EpisodeEngine.Story.cs:189). Screenshot 2 shows week 1; in Unity that week has the board only. |
| **Weight** | 10, The Fallout's own weight (`Web(0.5)`, Simulation/Story/StoryCatalog.cs:209). **Under Roles The Fallout weighs 0**, because quiet tears replaces it, using the precedent of a rules-gated weight (Simulation/Story/StoryCatalog.BondsAndSecrets.cs:475). Old seasons keep The Fallout as it is. |
| **Cast** | SUBJECT: an NPC nominee or replacement nominee, not the HoH, from a keyed pick weighted by max(1, Warm + 1). The clamp matters because Manipulative and Confrontational make Warm negative. There is no mood gate (mood is "Angry" for almost every nominee). |
| **Text** | "You spot {SUBJECT} crying quietly in the living room." (web template) |
| **Comfort them** (Warm; hidden if the player put them there) | +5 with SUBJECT and the memory "Comforted me in week N". When an NPC HoH put them up, a keyed 50% that the HoH notices: -3 from the HoH. Description: "Sit with them. The Head of Household may notice." The caption is the existing "Comfort them", reused with its meaning. The legacy house event that also uses it (Simulation/HouseEvents.cs:199) never runs while the story is on. |
| **Tell them it wasn't personal** (Candid; only if the player nominated them or named them as replacement) | A word-odds check (`Checked(50)`, odds shown as a KnownOdds word). Success: their grudge against the player eases by 25, and +2. Backfire: -3. |
| **Sit with them a while** (Warm; **BlockSet only**; costs the window's seat, and the Campaign window has 2) | As Comfort, plus **a favour they owe the player** (`Hook(Player, SUBJECT)`). The player spends an action now on a nominee who may leave, and it is repaid weeks later if they win power. |
| **Pretend you didn't see** (the lapse; a new caption) | 0, as on the web. |
| **Best when** | Comfort: the HoH's view does not matter to you. Pretend: it does; before the veto meeting the HoH still names a replacement. Tell them: the odds word is good and you want the grudge gone. Sit: you have a spare seat and expect them to survive or hold power later. |

The 'legacy-emotional-breakdown' arc (Comfort +18 / Strategic sympathy +10 / Walk away -5) is not changed; changing its text or numbers would move the digest of every old season. "A shoulder" (a friend comes to a nominated player) is **cut from v1** for pacing (§5.5).

---

## 4. Persistence and determinism

### 4.1 No new persisted field

| What | Where it lives (existing schema-29 storage) | Bound |
|---|---|---|
| Nominee asks, `veto_hold`, the holder's offer, final-two prices | `state.deals` | 200 deals (Simulation/NpcDeals.cs:70) |
| The holder's offer's voting-bloc price (R8, mode 2) | `unifiedCommitments`, under one new origin **value** | The family's capacity |
| Pitches; which pitches were promised or turned down | `replyCards` and `ledger.replies`. The +8 at nominations and the eased grudge read the week's reply rows. | 24 cards |
| Safety promises, both directions (pitches, "Take their word for it") | `promises`, or canonical rows in modes 1 and 2 via the existing story and pitch origins | 200 promises |
| Favours (Desperate rider, "Sit with them a while") | `story.hooks` | 32 (Simulation/Story/StorySystems.cs:366-367) |
| Grudges (Threatening rider, a defied HoH) and eased grudges | `story.grudges`, cause `GrudgeCauses.Story`, so no new cause value | 256 |
| Pawn conversation, quiet tears | `storylines` and `houseEvents` | 100 storylines, 400 events (Simulation/EpisodeValidation.cs:314,335) |
| Tone, situation word, quote, stakes line | **Not stored.** A pure function of public, week-fixed state plus a keyed hash. | — |
| The rule | `story.rulesVersion`, an existing field already listed in `ShippedRules.Fields` (Simulation/ShippedRules.cs:102) | — |

**New values in closed vocabularies; no new fields:**
- `DealKind.VetoHold` (`veto_hold`).
- Deal-id prefixes `deal-keep-` and `deal-save-`. They are added to `DealResolution.AcceptedOffer` (Simulation/DealResolution.cs:94-103), so a breach weighs one step heavier.
- One mode-2 commitment origin (R8 only).
- The template id `the-pawn-talk`, and `quiet-tears`.

None of these needs schema 30.

**Storyline capacity.** Validation refuses more than 100 storylines (Simulation/EpisodeValidation.cs:335), and every command's result is validated before it is installed (Simulation/EpisodeEngine.cs:44-47,62-70). The story pool checks capacity only when dialogue is on (Simulation/Story/EpisodeEngine.Story.cs:417,501). Under Roles:
- the pool starts nothing new at 92 or more storylines, keeping 8 for rule-driven arcs (backdoor, pawn conversation, production);
- `StartCycle` refuses at 100, as it already does under dialogue;
- the lab reports the most storylines any 16-houseguest season reaches.

### 4.2 The gate: `StoryRules.Roles`

| Option | New field? | What it touches | Verdict |
|---|---|---|---|
| **A. `story.rulesVersion` = Roles** | No | The story ladder (Simulation/Story/StoryState.cs:261-289) | **Recommended.** Reach (8) and Threads (9) shipped content the same way. New templates are filtered by `template.rulesVersion > s.story.rulesVersion` before they join the pool (Simulation/Story/EpisodeEngine.Story.cs:416). |
| B. `economyRulesVersion` 2 | No | Five live `== 1` gates | No. The ROLE doc asks that E1's equality checks keep their meaning (ROLE_DIALOGUE_ECONOMY_20261012.md:133-135). |
| C. `dialogueRulesVersion` 2 | No | The closed pilot validator, and switching dialogue on for shipping | Kept for Layer 2. |
| D. A new `roleRulesStartWeek` | **Yes** | Schema 30 after PR #28, a frozen 29 reader, the upgrade, every pin | Only if the owner wants a separately named boundary. |

**The number is claimed, not assumed.**
- BALANCE_BASELINE.md:313 already assigns the story pariah-rate fix to "story rules 10 (story session)".
- `Current` enrols every milestone at or below it, so the two orders depend on each other.
- R0 names `StoryRules.Roles` only after the claim, made with the story session as schema numbers are claimed: 10 if they agree that Roles goes first, otherwise 11.
- R7's enrolment waits for every milestone below Roles to be finished.

**Claimed, 2026-10-22 (R0).** Story rules **11** is claimed for Roles. **10 stays reserved** for the story session's pariah fix (BALANCE_BASELINE.md:313). No story session was active, so the claim follows decision 2's recommended answer; tell the story session when it next runs. Enrolment (R7) waits for every milestone below Roles, 10 included. Until 10 is defined, validation refuses it: `StoryRules.IsDefined` accepts 0 to 9 and 11 only, because a season may not hold a version no code reads. Line citations stay against eb092c67 (the evidence base above); R0's insertions move later lines in EpisodeEngine.Story.cs, StoryCatalog.PhaseBeats.cs and DialogueRules.cs.

**Under option A:**
- `StoryRules.Latest` becomes the validation bound (Simulation/Story/EpisodeStoryValidation.cs:28) and the catalogue lint bound. `Current` stays `Threads` until R7. This follows the precedent `CompetitionMiniGames.CurrentRules`: "a bound, never a behaviour" (Runtime/Presentation/CompetitionMiniGames.cs:21-23).
- **The frozen validators are not touched.** They already carry pinned copies with `Current = Threads` (Runtime/Persistence/FrozenV24Validator.Vocabulary.cs:391-418; Runtime/Persistence/FrozenV25Validator.Vocabulary.cs:393-420; Runtime/Persistence/FrozenV28ReadOnly.Leaves.cs:1494-1512). A test confirms they refuse Roles.
- `DialogueRules.Prerequisites` changes from `story.rulesVersion == StoryRules.Threads` to `>=` (Simulation/DialogueRules.cs:448), or enrolment would switch dialogue off for good.
- The web importer pins `StoryRules.Threads` (Runtime/Persistence/WebSaveImporter.cs:206 calls `EnableStory`, which sets the live `Current`). The schema-16 migration already pins 9 (Runtime/Persistence/EpisodeSaveMigrations.cs:499-506). Without the pin, an imported web season would hold Roles once R7 moves `Current`. The importer turns on the commitment and lever rules from the week after (Runtime/Persistence/WebSaveImporter.cs:207-213) but never the strategy rules or the economy, so `RolesOn` is false there anyway; the pin keeps the Roles templates out, because the pool filters on a template's version alone.
- `RolesOn(s)` = story rules ≥ Roles **and** `StrategyRules.Apply` **and** `CommitmentRulesOn` **and** `LeverRulesOn`. The pitch board also needs `EconomyRulesOn` and `AgencyOn`, as `HoHPitches.Available` does (Simulation/HoHPitches.cs:13-16). Validation of `veto_hold` and the new id prefixes uses the same test.
- `EnableStory` keeps setting `Current` (Simulation/Story/EpisodeEngine.Story.cs:37), and `ShippedRules.ApplyFresh` calls it (Simulation/ShippedRules.cs:36). Fresh seasons are therefore enrolled only when R7 moves `Current`. Until then, tests opt in by setting `s.story.rulesVersion = StoryRules.Roles`.

### 4.3 Old saves replay byte for byte

- Every committed fixture holds `story.rulesVersion` ≤ 9, so no new path runs: no new log line, minted id, draw or `Change`. That includes the three schema-29 fixtures 6909f2bc added, and **s29-mode1-allweek-w3-veto-meeting**, saved at the veto meeting, is the canary.
- Old-save acceptance already asserts that a loaded world keeps the step's own story version (Tests/EditMode/OldSaveAcceptanceTests.cs:250, HEAD).
- Everything new is behind `RolesOn`:
  - the new templates are rejected by `Castable` before they join the pool, so the weighted draw at each anchor (Simulation/Story/EpisodeEngine.Story.cs:337-370) sees the same candidates and the same total;
  - the "nothing" offset adds only the weight of castable Roles templates, which is 0 in an old season;
  - The Fallout's weight changes only under Roles;
  - the twins in "Just a Pawn" are `ShowIf`-gated, so an old season's house event holds exactly the shipped choices;
  - the coin on the backdoor opener is read only under Roles;
  - `PhaseMoment`'s new parameter defaults to Grudges;
  - the decline and lapse costs, exclusivity, the trap skip and the pitch changes are all Roles-only, so an old season that recorded two veto yeses still replays.

**Tests written against `StoryRules.Current` must be repinned:**
- `StoryCatalogLintTests` requires every template's version to be at most `Current` (Tests/EditMode/StoryCatalogLintTests.cs:36, HEAD); it moves to `Latest`.
- `StoryReachTests` and `StoryThreadTests` default to `Current` (Tests/EditMode/StoryReachTests.cs:23; Tests/EditMode/StoryThreadTests.cs:23); they are pinned to `Threads`, or they would quietly run at Roles after R7.
- The unified-safety fixtures opt in at `Current` too (Tests/EditMode/UnifiedSafetyHearingTests.cs:420; UnifiedSafetyNegotiationReaderTests.cs:470; UnifiedSafetySummaryTests.cs:621, HEAD); R0 pins them to `Threads` as well. `AllianceLeakTests` (:90) restores the version `EnableStory` set at :82, so it follows `Current` by design and stays.

### 4.4 Commands: none added

| Moment | Command | Kind | Cost |
|---|---|---|---|
| Any offer on a board | `RespondToDeal` | 31 | Free |
| HoH pitch, in any order | `ReplyToHouseguest` | 47 | Free |
| Pawn conversation, quiet tears | `ProgressStoryline` | 43 | Free, except "Get it in writing" and "Sit with them a while" (`costsAction`) |
| A plea after turning the holder down | `Lobby` | existing | 1 action |

The next free command kind stays 66 (Simulation/EpisodeState.cs:581-701) and is not claimed.

### 4.5 Randomness

- **Filing** is roll-free, a function of state, like `AskForTheVeto` (Simulation/NpcDeals.cs:368).
- **Tone, stakes and quote** are pure. The quote index is `StoryRandom.Index`, keyed on the occasion id.
- **New keyed coins** use `StoryRandom.Chance` (Simulation/Story/StoryRandom.cs:47), never the season's stream:
  - `w{week}:pawn-talk:{hohId}`;
  - `w{week}:tears-seen:{subjectId}`;
  - the quiet-tears check, through the existing keyed `Checked` path.
- **Story draws** use the existing keyed pool roll, `StoryRandom.Unit(s, "w{week}:{anchor}:pool")` (Simulation/Story/EpisodeEngine.Story.cs:357).
- **Answers, lapse costs and riders** go through `Change`, which draws the season's reciprocal roll, but only in Roles seasons.
- **NPC decisions** read the new rows through the existing weights. There is no new stream. The web's `Math.random` timer and `Date.now` ids are not ported.

### 4.6 After schema 29 freezes

Adding story rules Roles, `veto_hold`, the new prefixes and the R8 origin value to schema 29 after PR #28 merges is how Reach and Threads shipped (Assets/Plans/StoryArcs/IMPLEMENTATION-LOG.md:243-244). Two consequences follow, and the old-save lane should note both:
- the future frozen schema-29 reader must accept these values;
- a build of PR #28 will refuse saves that hold them.

---

## 5. UI

### 5.1 The offer board

One component, `EpisodeHud.OfferBoard`, built on the campaign board's card and portrait pattern (Runtime/Episode/EpisodeHud.CampaignBoard.cs:416, `HudPrimitives.RectPortrait`). It is used for the veto meeting, the Diary Room's veto column, the HoH's pitches and, later, the holder's offer.

```
INCOMING OFFERS                                       Answering uses no conversation
Houseguests came to you before your veto decision.    Silence costs more than a no.

[portrait + target]         [portrait + target]          [portrait + crown]
Danielle Reyes              Marcus Webb                  Priya Shah, Head of Household
VETO COMMITMENT             VETO COMMITMENT              DON'T USE THE VETO
Save me with the veto       Save me with the veto        Keep the nominations the same
(!) Desperate               (=) Pragmatic                (#) Calculated
On the block again          First time on the block      Head of Household
Your read: Neutral          Your read: Close             Your read: Neutral
"Save me and I'll owe       "I know we haven't always    "I'm asking you not to
 you. I mean it."            seen eye to eye, but         change my nominations."
                             save me."
In return: their vote to    In return: their vote to     In return: nothing promised
keep you next time          keep you next time
Keep it: they owe you a     Keep it: they remember it.   Keep it: they remember it.
favour. Break it: the       Break it: the house may      Use the veto anyway: they
house may hear.             hear.                        take it personally.
You kept your word to them in week 2
[Accept this offer]         [Accept this offer]          [Accept this offer]
[Turn this offer down]      [Turn this offer down]       [Turn this offer down]

(existing Use / Keep controls below, unchanged)
```

- **Order:** nominees in block order, then the HoH (§3.0).
- **Badges:** target `PackArt.Pack8IconTarget`, crown `Pack8IconHoh`, veto `Pack8IconVeto` (Runtime/Presentation/PackArt.cs:229-233).
- **Portraits:** only from `CharacterPortraits.Get`, which means UMA bodies with no initials avatars, following the owner's decision.
- **A locked card** (one veto word already given) shows the lock reason as its own label, and Accept is disabled.
- **Text caps:** a quote of at most 15 words, and a stakes line of at most 14.

**Built, 2026-10-23 (R2).** `EpisodeHud.OfferBoard` (Runtime/Episode/EpisodeHud.OfferBoard.cs) draws the words `RoleOffers` (Simulation/Story/RoleOffers.cs) gives it, at the veto decision and in the Diary Room's veto column, under `RolesOn` only; without it the meeting draws `VetoOffers`' rows as before and the diary's column is unchanged. These calls were made where this section left room:
- **Only what is true at R2.** The header has no "Silence costs more than a no." The stakes line reads the stakes table, but the riders stay out of it until R3 settles them (`RoleOffers.RidersSettle`): today every card says "Keep it: they remember it. Break it: the house may hear." R3 flips the constant and checks the three rider lines, which are written and pinned at 14 words or fewer. *R3 did both, and added each card's answers line (§3.2).*
- **The terms** use `Negotiation.AskPrice` in `PriceWords`' own words, the words `AskPriceLine` uses: "In return: their vote to keep you, the next time you are on the block", "In return: a final two", or "In return: nothing promised".
- **The history line** is the player's latest kept or broken word with them, settled before this week, from the Your word page (`CommitmentsRead`). It never changes while a card is up. It says who kept a word only where the record does:
  - a promise is one-sided, so "You kept your word to them" or "They kept their word to you";
  - a deal binds both, and the page says only "honoured", so a kept one names nobody: "Your deal with them held in week 2". This includes the mock's case, a nominee's veto ask the player used the veto on, which is the nominee's proposal and the player's kept word;
  - a call says "They followed your call" or "They ignored your call";
  - a broken word names who broke it where the record can say, and otherwise says "A word between you broke".
- **Focus stays on the card.** Both cards carry the same two captions. When the HUD rebuilds itself (a body finishing assembly is one cause), the keyboard goes back to the same offer's answer, not to the first control with that caption. Besides the control's name, the HUD remembers which offer and which answer had the keyboard, `(OfferId, Accept)`. This applies to the old rows too, which had the same fault.
- **The tone's mark** is a plain character in a disc of the tone's colour beside the word: Bitter x, Threatening ^, Desperate !, Friendly +, Earnest *, Calculated #, Pragmatic =.
- **Layout.** Cards stand side by side down to 300 units a card (scaled), and one over another below that, as in the diary's column. At the largest text they page one card at a time, with "Previous offer", "Next offer" and "1 of 2" on the HUD's one pager row. Nothing on the board animates under either motion setting: the cards cut in, and only the panel arrives.
- **Screen readers.** `EpisodeHud.TryReadOfferControl` gives a board control's caption, the card's heading and the card's one line (`RoleOffers.Spoken`).
- **The diary says how its answers save.** An offer's answer commits as it is pressed, as on the episode screen. While the board is up, the room's line and the decision tab say so (`DiaryOffersInsideMessage`, `DiaryOffersSaveLine`), in the pitches' manner. Before R2 they said that nothing commits until confirmed.
- **The quote's key** is the ask's row id, as R1 chose. Row ids follow filing order, and filing order follows hidden warmth, so which line a card shows depends on that order through a keyed hash. No order or score is shown, and the player cannot see the line the other order would have given, so it reveals nothing. A key built only from public facts (week and asker) would remove even that dependence, if R3's review wants it. *R3 took it: see §3.2.*

### 5.2 Captions are a contract

- **Unchanged:** "Accept this offer" / "Turn this offer down" (Runtime/Episode/EpisodeHud.cs:70-71). Repeated buttons are told apart by `OfferControlDescription`, which holds the offer id, proposer, recipient, session, revision, phase and render generation (Runtime/Episode/EpisodeHud.cs:73-117). The pitch answers are unchanged too.
- **Tone, situation, read, terms, stakes and history** are separate labels and eyebrows. **Nothing is appended to a caption.**
- **New captions, checked unused in Simulation and Runtime:** "Previous offer" / "Next offer" (the pager, at Largest only), "Pretend you didn't see", "Tell them it wasn't personal", "Sit with them a while".
- **Reused deliberately, with the same meaning:**
  - "Comfort them": the legacy house event never runs with the story on.
  - "Say nothing": the lapse of four story beats and the eviction speech skip (Runtime/Episode/EpisodeHud.cs:27). Only one story ask is open at a time, and the speech comes at a different phase.
  - "Take their word for it", "Get it in writing" and "Call it what it is": the pawn conversation must present exactly as Just a Pawn does. Their Roles twins keep the captions; only the descriptions change, and they change in both variants alike.
- **Not used:** "Ask for their word" already belongs to a play (Simulation/Story/StoryCatalog.Plays.cs:283), and plays can be open alongside an arc's ask.

### 5.3 Knowledge gate

**The board and the moments show only:**
- public facts: this week's roles, traits, times nominated, the endgame, the jury;
- the player's own reading, pacts and record;
- the row's own terms.

**They never show:**
- the NPC's view of the player (the web's tone source);
- a score, a percentage or a hidden ranking;
- a reason derived from private state.

**Specific closures:**
- Card order no longer follows filing order, which followed hidden warmth.
- The pitch cap ranks by the player's own reading.
- The holder's offer no longer depends on whether they would have saved the player anyway, and its price kind comes from public facts.
- The pawn conversation's text no longer depends on whether a backdoor is planned.
- Odds on checks are KnownOdds words (Simulation/KnownOdds.cs:6-38).

**Tests**, in the style of WeeklyRecapPrivacyTests and BalanceLabTests' knowledge gate:
- keep the set of filed rows the same, then perturb hidden scores and the NPC→player view **without crossing a filing threshold**, and assert the board's text is unchanged;
- assert the pawn conversation's text is identical with and without a backdoor plan.

Which offers exist depends on hidden warmth by design, as `AskForTheVeto` already does, so the test cannot cross a threshold and still pass.

### 5.4 Accessibility

- **Text size:** at 1.0 and 1.2 the 3 cards fit on one page. At 1.4 (Largest) they stack or page with a visible "1 of 3" indicator, as the owner decided. No text under 12 px at 1080p. Label boxes are at least 1.3 times their font, because Inter truncates below 1.21.
- **Colour:** each tone has its own glyph shape as well as a colour, so colour is never the only signal.
- **Reduced motion:** no card travel or fade; the cards cut in.
- **Screen readers:** they read the caption, then the card heading ("Danielle Reyes, nominee, veto commitment"), then one description line that states the tone, situation, read, terms, stakes and any lock reason in words.

### 5.5 Pacing against 45 minutes for an 8-player season

- The lab measures about 8.5 mechanical minutes a week at 8 players, at 30 s per decision (BALANCE_BASELINE.md:378-382). About 6 weeks makes about 51 minutes, already over the owner's 45 (handoffs/OWNER-DECISIONS-2026-10-22.md, §6).
- Active policies already make 16-19 decisions a week (BALANCE_BASELINE.md:1029-1041).
- Roles land in most weeks for a strong player: the fixed-0.50 Studier wins HoH or the veto in 95.23% of eligible weeks at 8 (REMAINING_WORK_20261012.md:62,67). They land in about half the weeks for a passive player (BALANCE_BASELINE.md:311), plus the weeks they are nominated.

So role moments must **replace** decisions, not add them.

**Where decisions are added or saved:**

| Moment | Today | Under Roles | Change |
|---|---|---|---|
| Player HoH week | Up to 7 pitches in week 1 at 8; fewer later | At most 3, in any order | **-4** in week 1, -0 to -2 later: the main saving |
| Player holds the veto | 0-2 offer rows | 0-2 nominee cards, plus the HoH's card only when a nominee asked | +0 to +1 |
| Player nominated by an NPC HoH | Just a Pawn in backdoor weeks | The same card on a 50% coin, at most once per HoH per season | About +0.5 a season |
| Player nominated, NPC holder (R8 only) | — | At most 1 offer | +0 to +1, measured on its own |

**The story pool.** Shipping seasons play the reach rules, so "nothing" weighs 40 at NomsSet, VetoWon and BlockSet (Simulation/Story/EpisodeEngine.Story.cs:355). With castable weight *o*, a moment starts with probability o / (o + 40). Under Roles the castable Roles weight *r* is **also added to "nothing"**, giving (o + r) / (o + 2r + 40). Role moments then mostly replace other moments rather than add to them.

| Anchor | Today (o = 12-22) | Under Roles | Change |
|---|---|---|---|
| NomsSet (quiet tears r = 10) | 23-35% | 31-39% | +4 to +8 points |
| VetoWon (quiet tears r = 10, not with a board up) | about o/(o+40) | about +3 to +6 points | small |
| BlockSet (The Fallout 10 → 0; quiet tears 10) | o/(o+40) | o/(o+50) | lower |

Quiet tears comes at most once a week, so the net is about **+0.05 to +0.1 story asks a week**, all inside the cap of 2 (Simulation/Story/EpisodeEngine.Story.cs:88).

**Budget (to measure before enrolment, R7):**
- At most **+1 new modal decision in any role week**, and at most 3 cards on one board.
- **Net ≤ 0 new modal decisions per season at 8** for the lab's engaged policies (social, reader, schemer, loyalist). For the passive policy, which is rarely HoH and saves little, at most +2.
- A three-card board is read in **≤ 30 s at the median** (B8).
- Measured with the lab's per-policy decision counts and B8's decision time.
- **If over budget, in order:** quiet tears weight 10 → 5; the pawn coin 0.5 → 0.33; the pitch cap 3 → 2.

---

## 6. Phased implementation plan

### 6.1 Slices

Each slice is built, fully tested, independently reviewed and landed on its own, and committed and pushed before the next begins.
- R0 to R6 change nothing in shipping seasons: `Current` stays at Threads until R7.
- "Subset" means `dotnet test Tools/SimulationTests`.
- EditMode and PlayMode run on a D: copy, alone; PlayMode captures run on a screen of their own.

| Slice | What | Tests | Size | Risks |
|---|---|---|---|---|
| **R0 The rule** | **(1)** Claim the number with the story session, then add `StoryRules.Roles`. **(2)** `Latest` as the validation and lint bound. **(3)** `RolesOn`. **(4)** DialogueRules.cs:448 `>=`. **(5)** The importer pinned to Threads. **(6)** `PhaseMoment` takes `rulesVersion`, defaulting to Grudges. **(7)** Test repins (lint to `Latest`; Reach and Thread tests to `Threads`). **No frozen-validator edits.** | Subset: validation accepts Roles live; the frozen readers refuse it; an import still holds 9. EditMode: every golden and old-save digest unchanged. | S | The number claim |
| **R1 Tone, stakes and quotes** | `RoleTone` (pure), the situation word, the stakes table, the quote catalogue, `StoryRandom.Index` keys | Subset: **(1)** one table test per ladder row; **(2)** rider tones are fixed for the week (answer, then settle: same tone); **(3)** the knowledge gate scoped as in §5.3; **(4)** stable across answers and reloads; **(5)** quotes state no terms and stay ≤ 15 words | M | Writing lines that never over-promise |
| **R2 The offer board** | `OfferBoard` for the veto meeting and the Diary Room veto column; tone, situation, read, terms, stakes and history; block order | PlayMode: **(1)** captions sweep and `OfferControlDescription`; **(2)** captures at 1.0/1.2/1.4; **(3)** reduced motion; **(4)** screen-reader text; **(5)** old-rule seasons draw the old rows | M | HUD layout flake (renders re-select controls) |
| **R3 Veto-board economics** | **(1)** `Negotiation.VetoWordGiven` and the engine-side one-veto rule in its three places. **(2)** The nominated-holder trap skip. **(3)** Tone-scaled decline and lapse costs for veto asks. **(4)** The Bitter, Threatening and Desperate riders. | Subset and EditMode: **(1)** a second yes refused, a double click, a duplicate command id; **(2)** lapse vs decline by tone; **(3)** riders kept and broken; **(4)** favour bound (32); **(5)** modes 0/1/2; **(6)** `randomState` unchanged by filing; **(7)** reload mid-board | M | Lapse costs land after `ResolveVeto`; tones read from the state before it |
| **R4 Keep-the-block** | **(1)** `DealKind.VetoHold` (appended, titled, Roles-only, never player-proposed). **(2)** `AskToKeepTheBlock` (counter-offer only, `BackdoorPlanned`). **(3)** The raw final-two price path for `deal-keep-` in `AskPrice`, `StrikeTheAskPrice` and `AcceptedOffer`. **(4)** The `Hold` verdict with its kept weight. **(5)** The defied-HoH change and grudge. **(6)** Expiry. **(7)** Every reader in §3.3. | Subset and EditMode: **(1)** filing conditions (no nominee ask, backdoor, warmth, Final 4, player nominated); **(2)** kept +8 and broken -45 attributed to the player; **(3)** price voided; **(4)** locks both ways; **(5)** validation refuses it without Roles; **(6)** `CanPropose` refuses it in every season; **(7)** `YourWeek.ReadDeal` reads its lines; **(8)** modes 0/1/2 | L | Many readers |
| **R5 The pitch board** | Cap of 3 (2 warmest and 1 coolest by the player's reading); any order; Promise pays at the nominations; Turn down eases the grudge; tone decline costs | Subset: **(1)** cap and selection; **(2)** answering in any order; **(3)** nominating clears; **(4)** the +8 lands only for those left off; **(5)** a replacement breaks it; **(6)** the ease. PlayMode: board layouts. | M | Changes E4 behaviour (Roles only) |
| **R6 Story moments** | **(1)** `the-pawn-talk` and the coin on the backdoor opener, with the Roles twins. **(2)** `quiet-tears`. **(3)** The Fallout's weight 0 under Roles. **(4)** The "nothing" offset. **(5)** The storyline capacity guard. **(6)** Each Roles template asks `RolesOn` in its cast or weight: `Castable` and `StartStory` filter on a template's version alone (Simulation/Story/EpisodeEngine.Story.cs:416,486; Simulation/Story/EpisodeEngine.Threads.cs:155). | EditMode and subset: **(1)** StoryCatalogLintTests; **(2)** old pools unchanged at every anchor; **(3)** the pawn conversation's text identical with and without a backdoor; **(4)** once per HoH per season; **(5)** the twins hidden in old seasons; **(6)** airtime held; **(7)** nameless memories; **(8)** costsAction refused at 0 seats; **(9)** capacity at 92/100; **(10)** no Roles template casts at story rules 11 while `RolesOn` is false | M | Airtime crowding; the backdoor arc's routing |
| **R7 Enrolment and measurement** | **(1)** `StoryRules.Current = Roles`. **(2)** Re-record BalanceLabGoldens and CommitmentRulesSeasonDigests. **(3)** B9 cohorts and the pacing lab against §5.5's budget. **(4)** With the old-save lane, a **schema-29, mode-2, Roles fixture saved at the veto meeting with a `veto_hold` pending**, and the story version added to the manifest's cells (OLD-SAVE-ACCEPTANCE.md at HEAD, lines 3-5 and 150-158). **(5)** The abuse checklist. **(6)** Keep R0's web-import story assertion (Tests/EditMode/PersistenceTests.cs): it can fail only once `Current` moves. | The full EditMode run. B9: weekly wins, Winner rate, counter-carry. The lab: minutes a week, decisions per policy, the most storylines at 16. Abuse: **(1)** accepting both asks; **(2)** accept-then-break loops; **(3)** comfort farming; **(4)** renomination; **(5)** a stale card after a phase advance; **(6)** load mid-board; **(7)** a double click; **(8)** a failed save; **(9)** an interrupted NPC approach. | M | Golden churn (expected); the owner signs off on the numbers before merge |
| **R8 The holder's offer** (only after B9 and the owner's sign-off) | **(1)** `OfferTheVeto`. **(2)** The player-paid price ladder (a voting bloc this week, or a final two). **(3)** The new mode-2 origin with admission, validation and voiding. **(4)** The substitution rule in `CanLobby`. **(5)** Reader wording for a player-owed price. | Subset and EditMode: **(1)** filing conditions, including "would save anyway" makes no difference; **(2)** an NPC breach makes a Betrayal; **(3)** the price voided when the holder breaks; **(4)** the 60d8f629 sibling; **(5)** modes 0/1/2; **(6)** `WillNotSave` and the cold-partner coin. B9 rerun with R8 on. | L | Balance: it makes being nominated survivable |

Later, outside this plan:
- **L2, role grants and time** (the ROLE doc), under dialogue rules version 2, after the owner ships dialogue. It needs grant writers, the `Simulation/DialogueRules.cs:182` tick key fixed for zero-tick grant entries, and the B9 measurement the ROLE doc requires.
- Trait-picked price kinds.
- The nominated holder's "vote to keep me" ask.
- Allies approaching the holder.

### 6.2 Cross-cutting risks

- **Balance.** Weekly HoH/Veto wins already run at 95.23% against a 60-70% target, and the season Winner rate is 73.5% against 25-35% (REMAINING_WORK_20261012.md:62-63,67). Role moments make a role worth more. The lapse costs and the stricter pitch economics push the other way. Enrolment (R7) waits for B9; R8 waits for B9 and a second sign-off.
- **Two sessions share the branch.**
  - The story-rules number is claimed with the story session.
  - The pawn conversation touches the backdoor arc's routing, which the story session owns.
  - R0 no longer touches `Runtime/Persistence` beyond the importer's one-line pin.
  - No schema number or command kind is claimed.
- **Storyline capacity is 100, with no pruning.** This is guarded under Roles (§4.1) and measured in R7.

### 6.3 Decisions for the owner first

| # | Question | Recommended answer |
|---|---|---|
| 1 | Gate the feature on the story rules version (an existing saved field, no schema bump), not a new schema-30 field? | **Yes.** |
| 2 | Story rules 10 is earmarked for the story session's pariah fix (BALANCE_BASELINE.md:313). Claim the number together and let whichever is built first take 10? | **Yes.** Roles takes 11 unless the story session agrees otherwise. Enrolment waits for every milestone below it. |
| 3 | Take the emotional states only from public facts, most of them fixed for the week, and let the tone set real stakes (decline and lapse costs; the Desperate, Bitter and Threatening riders)? | **Yes.** The web's way breaks the privacy rule at ReplyCards.cs:117-127. |
| 4 | Under Roles, make leaving an offer unanswered cost more than turning it down (-1 to -5 against 0 to -3 by tone), replacing today's flat -3 decline and free lapse? | **Yes.** Otherwise "Turn this offer down" is never the right button. |
| 5 | Have the engine refuse a second veto word, and stop the trap ask to a holder who is on the block? | **Yes.** |
| 6 | Make the HoH's "Don't Use the Veto" a Roles-only deal (`veto_hold`): only as a counter to a nominee's ask, +8 kept, -45 broken, a final two as the only price (at 6 or fewer), and -5 with a grudge of 15 for using the veto after declining it? | **Yes.** |
| 7 | Pitches under Roles: at most 3 (2 warmest and 1 coolest by your own reading), any order, Promise pays +8 only when kept, and Turn down eases their nomination grudge if you nominate them? | **Yes.** It is also the main pacing saving. |
| 8 | Play "Just a Pawn" in any week an NPC HoH nominates you (a 50% coin, once per HoH per season), so the card no longer reveals a backdoor? Under Roles, "Take their word for it" also asks your word back, and "Get it in writing" costs the window's seat. | **Yes.** |
| 9 | Let quiet tears replace The Fallout under Roles, at most once a week, with role moments adding their weight to "nothing"; cut "a shoulder" from v1? | **Yes.** |
| 10 | No new currency and no +1 social point; the two "take your time" choices cost the window's seat and pay in visible receipts (a favour owed)? | **Yes.** Defer +1/-1 time to Layer 2. |
| 11 | Pacing budget: at most +1 modal decision per role week, net ≤ 0 per season at 8 for engaged play, and a three-card board read in ≤ 30 s, measured before enrolment? | **Yes.** |
| 12 | The holder's priced offer to a nominated player: build it only after B9 and a second sign-off on its numbers (R8)? | **Yes**, to the shape; the go/no-go waits for B9. |
| 13 | Keep story moments out of week 1 before its eviction night, though screenshot 2 shows week 1 (the board itself plays in week 1)? | **Yes.** |
| 14 | Keep imported web saves on story rules 9 after enrolment? | **Yes.** |
| 15 | Trait-picked price kinds, allies approaching the holder, and the nominated holder's "vote to keep me" ask? | **Defer** to after R8. |

---

## Appendix A. Review notes

### A.1 Feasibility review

| Finding | What changed |
|---|---|
| **BLOCKER 1.** The mode-2 price path accepts only the nominee's `deal-veto-` ask | **Fixed by restructuring.** The keep-the-block price is now a final two only, at 6 or fewer: a plain row in every mode, which needs no new origin (§3.3). The holder's offer moved to R8, which adds the one new origin, for a player-paid voting bloc, with its own admission and validation checks, tested in modes 0/1/2 (§3.4b). The suggested separate "R2.5" slice is folded into R8, since R8 is now its only consumer. |
| **MAJOR 2.** `PhaseMoment` hard-codes Grudges | **Fixed.** It takes a `rulesVersion` parameter defaulting to Grudges; new templates pass Roles; R6 keeps the old-pools test (§3.5, R0). |
| **MAJOR 3.** `veto_hold` would be player-proposable | **Fixed.** `CanPropose` always refuses it; it is appended at the end of `All` with its own Title (§3.3). |
| **MAJOR 4.** The wrong backdoor field, and the duplicate of "Just a Pawn" | **Fixed.** Both read `BackdoorPlanned(s)`. The pawn conversation no longer duplicates Just a Pawn: it *is* Just a Pawn, extended to other weeks with the same card (§3.4a). |
| **MAJOR 5.** Pawn talk: wrong settlement, a truth-leaking claim, a free commitment | **Fixed.** No target-less vote promise is written. The claim no longer depends on the hidden ranking or even on whether a plan exists; both variants come on the same coin. "Get it in writing" costs the window's seat under Roles. |
| **MAJOR 6.** The holder's offer reveals the hidden decision | **Fixed.** Filing no longer depends on whether the holder would have saved the player anyway. The "calculated" branch is gone. The price kind comes from public facts. The sibling test now asserts that the decision moves at 0 actions only with a binding price the player owes, and decision 12 says the price is the payment. |
| **MAJOR 7.** Storylines can pass 100 | **Fixed.** A Roles capacity guard at 92 for the pool and 100 for `StartCycle`; the lab reports the 16-houseguest maximum (§4.1). |
| **MAJOR 8.** Story rules 10 is earmarked | **Fixed.** The number is claimed jointly (decision 2, §4.2). |
| **MAJOR 9.** The importer and `RolesOn` | **Fixed.** The importer is pinned to Threads; `RolesOn` requires the strategy, commitment and lever rules (plus economy and agency for pitches), and validation uses the same test (§4.2). |
| **MAJOR 10.** No old-save fixture for the new rules | **Fixed.** R7 adds a schema-29, mode-2, Roles fixture at the veto meeting with a `veto_hold` pending (§6.1). |
| **MINOR 11.** The frozen-validator edit | **Adopted.** Dropped; a test confirms the frozen readers refuse Roles. |
| **MINOR 12.** The UI's lock line | **Adopted.** One helper with Negotiation's line, applied in three places (§3.2). |
| **MINOR 13.** Tests pinned to `Current` | **Adopted** (§4.3). |
| **MINOR 14.** Unlisted readers | **Adopted** (§3.3 readers row; §3.4b wording). |
| **MINOR 15.** Captions already in use | **Adopted.** "Say nothing" is reused deliberately; "Ask for their word" is no longer used (§5.2). |
| **MINOR 16.** The knowledge test cannot pass as worded | **Adopted.** It is scoped to the same set of rows, with no threshold crossed (§5.3). |
| **MINOR 17.** Content issues | **Adopted.** The Fallout check is replaced by The Fallout's weight 0 under Roles and a once-a-week limit; the cast weight is clamped; the board orders by block order; pitches rank by the player's own reading; the "0 added asks" claim is corrected (§5.5). |
| **MINOR 18.** Values added after the freeze | **Adopted** (§4.6). |

### A.2 Experience review

| Finding | What changed, or why not |
|---|---|
| **BLOCKER 1.** Dominant answers | **Fixed.** The choice rule is in §3.0, and every moment has a "Best when" row. (a) Lapse costs more than a decline, scaled by tone. (b) Promise pays only when kept; Turn down eases the nomination grudge. (c) Pawn: see MAJOR 5 of A.1; "Take their word for it" now costs the player's own word. (d) Quiet tears: Comfort risks the HoH noticing; "Tell them it wasn't personal" replaces Comfort when the player nominated them; "Sit with them a while" costs a seat for a favour. (e) "A shoulder" is cut. **Not adopted:** (1) the open-ended HoH vote for "Ask for their word", because story deals are week-bounded (Simulation/PlayerDeals.cs:405-421) and an open one needs a second new mode-2 origin, and the option slots hold Just a Pawn's choices; (2) "Call their bluff" as a reading of whether you are the target, because the ROLE doc forbids the hidden ranking as a spoken plan, and "Call it what it is" reveals only whether a plan exists; (3) "Ask who's coming for them", because no story effect writes a Told standing (the Told effect is a one-way cooling: Simulation/Story/EpisodeEngine.StoryEffects.cs:118-127), and a nominee's main grudge is nearly always the HoH's 70. |
| **BLOCKER 2.** The holder's offer gives a paid plea free and stacks | **Fixed.** Accepting counts as the plea, and a pending offer refuses the plea, so the two substitute. The price falls due this week (a voting bloc), or is a final two at 6 or fewer. The calculated branch is dropped. The sibling test is pinned. The offer ships only after B9 (R8). **Changed from the suggestion:** the price is a voting bloc rather than "your vote to evict the nominee the holder names", because the replacement is not yet named when the offer is filed, and naming the other nominee would depend on the holder's hidden alliances. |
| **MAJOR 3.** `veto_hold` pays for the default | **Fixed.** Counter-offer only; kept +8; a final two as the only price; -5 and a grudge of 15 for defying an HoH who asked. Breaking an accepted one stays at -45, by keeping trust High (Critical once accepted) and fixing the kept reward at the Low weight with a one-line kind rule. |
| **MAJOR 4.** The pacing estimate | **Fixed.** Corrected basis: role weeks are most weeks for a strong player. The "nothing" offset, The Fallout's weight 0, "a shoulder" cut, the budget (≤ +1 per role week, net ≤ 0 per season for engaged play) and text caps are all in §5.5. |
| **MAJOR 5.** Tone has no consequence; mood never changes | **Fixed.** Mood is dropped for a situation word. Warm ≥ 2 is now Earnest. Desperate comes from the situation. Tone sets decline and lapse costs and three riders (§3.0). |
| **MAJOR 6.** No chaining into later moments | **Fixed.** The Desperate rider and "Sit with them a while" write favours that Call in the Favour redeems. |
| **MAJOR 7.** Pawn talk crowds out NomsSet | **Fixed differently.** It is rule-driven on a 50% coin, at most once per HoH per season, instead of pool weight 20. It must be rule-driven like the backdoor beat, or its frequency would tell the weeks apart. |
| **MINOR 8.** The nominated holder's trap card | **Adopted:** no ask under Roles. The "vote to keep me" replacement ask is deferred: it needs an NPC-proposed vote offer to the player in mode 2. |
| **MINOR 9.** Nothing from the story in week 1 | **Adopted** as a stated rule and decision 13. |
| **MINOR 10.** The holder's offer and Call in the Favour | **Adopted.** No offer while the player holds a favour over the holder. |
| **MINOR 11.** The holder's offer leaks | **Adopted in part.** Filing no longer depends on "would they save you anyway". **Rejected:** letting the hidden weight size the price, because the price is shown on the card and would leak the weight. |
| **MINOR 12.** The invisible +1 social point | **Adopted:** dropped. |
| **MINOR 13.** The pitch-cap ranking | **Adopted:** 2 warmest and 1 coolest by the player's own reading. |
| **MINOR 14.** Trait-picked offer types | **Deferred.** Every new price kind is another mode-2 provenance, and a nominee cannot pay a vote "this week" from the block. The variety in v1 comes from the tone riders. |
| **MINOR 15.** No stakes on the card | **Adopted:** a words-only stakes line, plus "Silence costs more than a no." |

**Unchanged from v1, because both reviews confirmed them:** the story-rules gate with no schema bump; engine-enforced exclusivity of veto yeses; labels only from public facts; breaches charged to whoever breaks; the pitch cap as a pacing saving; no new command kinds; roll-free filing and keyed coins.

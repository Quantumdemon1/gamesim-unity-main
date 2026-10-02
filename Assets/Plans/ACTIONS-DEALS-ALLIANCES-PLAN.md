<!-- Written 2026-10-01 from four read-only investigations over main at d8de2f3: the action economy,
deals and promises, alliances, and the free time screen against the owner's mockup 87. Claims marked
(verified) were checked in the code by the lead. The rest come from reading, so each slice confirms its own
with a failing test before it changes anything. -->

# Actions, deals and alliances: the free time screen, the economy, your word and your pacts

## The owner's ask (2026-10-01)

> Brainstorm improvements to the action system, deals and alliances features

followed by

> Compile a plan based on the suggestions, also add in improving the "Free time" screen, I've
> included the current smashed menu where the player has to scroll to find information, and the
> generated mock up showing all of the information better organized. Include that in the plan to
> improve the action economy, alliances and deals system improvements

| Image | What it is |
|---|---|
| 84, 85, 86 | Today's free time, scrolled from top to bottom: the first-night story beat, then 'FREE TIME / NO ACTIONS LEFT', the house as cards, and a side column of location, play and threads |
| 87 | The owner's mockup, described below |

Mockup 87, from top to bottom:
- **A hero banner** for the story beat, beside an info card.
- **'TALK TO A HOUSEGUEST'**: one row of eight cards (portrait, name, a word, Talk) under a search box and a filter.
- **'OTHER WAYS TO SPEND YOUR TIME'**: four tiles (Explore the house, Call a house meeting, Listen in, Do an activity).
- **A footer** with the current location and 'Show on map', and a primary 'Continue Free Time · Stay in the house'.
- **A tip bar** with a day and a clock.

## 1. Where things stand

### 1.1 The free time screen

**Why it is smashed.**
- The first night's story beat switches the whole panel to the 403-unit house-event band (EpisodeHud.Story.cs:46; EpisodeHud.Activities.cs:397-415).
- At FontScale 1.0 about 1,450 units of content then sit in a 317-unit viewport (480 and 394 at 1.2).
- The way on, 'Begin the next competition', drops from the pinned footer to the foot of the scroll (EpisodeDirector.cs:1604-1619).
- Without a beat the plain stage is 1350 by 638, and it still scrolls about 1,100 units through a viewport of about 429.
- The house cards are 150 by 253, five to a row (EpisodeHud.FreeTime.cs:316-389).

**What the mockup asks for, and what it says that is not true.**
- **The info card's copy is false.** It says "doesn't spend actions and won't close this window". In fact talking, listening in and house meetings each spend one of the window's actions (EpisodeEngine.cs:1013-1014, 1703-1704), and unspent actions are lost when free time ends. What really is free: answering someone who comes to you, house events, the free questions, house activities, walking, and the first-night beat.
- **The day and the clock have no basis.** The game keeps weeks, not days. The only clock is the NPC world's seconds counter (NpcSocialState.cs:8).
- **The word under each name.** The mockup's 'Angry' is a mood word (EpisodeEngine.cs:766-781). The card today shows the player's own reading of that houseguest (RelationshipWeb.cs:205-214), which is the knowledge-safe choice.
- **'Explore the house' has no action behind it.** 'Do an activity' is the free House Activities panel (EpisodeDirector.HouseActivities.cs:101-106).
- **The primary button.** 'Continue Free Time · Stay in the house' means closing the screen and playing on. Nothing in the mockup ends free time; today that is 'Begin the next competition', a caption the season walks and the audited walk press (PortVerification.Season.cs:346-347).

**Move-in night has one action.**
- Week 1's free time is the 'after the eviction' window. In houses of 3 to 8 its size is `max(1, ceil(n/2) + 2 - 5)` = 1 (EpisodeEngine.Week.cs:56-67).
- Nothing spends it before the player acts: introductions and the first-night beat are free (StoryCatalog.KnowThem.cs:28-30). But one press during the walk-around, such as a conversation button or the Nearby card's 'Listen in', leaves NO ACTIONS LEFT on the player's first look at free time. That is what screenshots 85 and 87 show.
- The screen also calls move-in night 'After the eviction' when nobody has been evicted (EpisodeDirector.FreeTimeScreen.cs:159-166).

### 1.2 The action economy

**What exists.**
- About twenty-five verbs:
  - the dial (small talk, personal chat, real time, tactics);
  - room acts;
  - rumours, lies and schemes;
  - promises and deals;
  - lobbying, with five asks and four approaches;
  - calls, house meetings, listening in and bought actions;
  - studying;
  - free asks and reads.
- The week runs in four windows, each with its own seats: 2 after the HoH, 1 after the nominations, 2 after the veto (the campaign), and `max(1, ceil(n/2) - 3)` after the eviction (free time). An eight-house has six seats a week and one of them in free time. Nothing carries between windows (EpisodeEngine.Week.cs:49-94).

**What is weak.**
- **Dominated verbs.** 'Tell them something personal' is strictly worse than 'Spend real time with them': same lore facets and story odds, lower warmth and rapport. 'Talk game openly' (EV 3.4, risky) is no better than 'Talk tactics' (EV 3.5, safe). 'Air everything' has negative expected value and no other payoff. Each reply card has one answer that is simply the biggest number (WebSocialVocabulary.cs:26-62; EpisodeEngine.cs:1697-1730; ReplyCards.cs:47-66).
- **Too many controls.** A conversation shows 50 to 70 rows, four per houseguest (EpisodeDirector.cs:1437-1466).
- **Free time is starved.** It has one seat but holds most of the moves: meetings, rumours, room acts and listening in.
- **Wrong or hidden targets.**
  - 'Whisper about X' reaches a random listener (EpisodeEngine.cs:1664-1666).
  - 'Share something I know' passes a memory the player did not choose (:1038-1042).
  - 'Ask what they have heard' cannot be aimed (:1150).
  - 'Buy an action by burning one bridge' burns a random houseguest (:1744-1782).
- **Hidden costs aimed at you.** An NPC's rumour to the player moves both the player's view of its subject and the subject's view of the player through `Change`, while the log says only "X told you something about Y" (NpcSocialActions.cs:358-372, 501).
- **Feedback arrives late.**
  - Game Sense appears only at the season's end (EpisodeDirector.cs:1509).
  - The weekly recap never says whether a vote read was right (VoteRead.cs:177-178).
  - Rumours, lies and schemes report "No change in trust".
  - A house meeting does not say who moved.
- **The house stands still in the decision windows.** NPCs act only as free time and the campaign open, and the live NPC world runs only in those two windows (NpcSocialState.cs:44).

### 1.3 Deals, promises, offers and oaths

**What exists.** Ten kinds:
- **Player deals.** There are ten deal types. The NPC accepts on one roll against `AcceptanceChance`, which reads the NPC's hidden view of the player, ledger trust and the player's broken deals.
- **Weekly NPC offers.** A fixed ladder, at most three a week.
- **Nominees' veto asks.**
- **The plea's deal approach.**
- **Plea cards.**
- **Player promises.**
- **NPC promises, NPC-to-NPC deals and story deals.**
- **Loyalty oaths.**

How deals pull on decisions:
- An NPC HoH is more reluctant to nominate a deal partner: safety +35, veto use +40, final two +50.
- A vote deal enters the ballot as an obligation.
- A broken deal costs -15 x weight with the wronged side, spreads to 40% of the house, and leaves a 60 grudge.
- Code: PlayerDeals.cs:53-208, DealResolution.cs:69-208, NpcDeals.cs:107-388, StrategyRules.cs:73-85, 413-447.

**What is weak.**
- **Commitments are invisible where decisions happen.**
  - The nomination comparison shows promises but no deals (DecisionContext.cs:27-33).
  - Nothing warns before a breach.
  - A deal's term is never shown.
  - There is no single view of open commitments.
  - Weekly NPC offers write no log line and lapse unseen, costing -1 in Game Sense each (NpcDeals.cs:229-275).
- **Deals that never matter.**
  - Partnership and information-sharing never resolve.
  - A safety deal can only ever be broken.
  - Accepting any offer is a free +12, so accepting everything is the dominant play.
  - A final-two deal is worth about 4.5 points in the final choice.
- **Two vocabularies with different rules.** 'Promise safety' against the safety pact, the final-two promise against the final-two deal, and 'Promise to evict X' against 'Propose a vote to evict X'. Promises never enter a ballot; deals do (EpisodeEngine.Levers.cs:63-66).
- **No negotiation.** There are no counter-offers (PlayerDeals.cs:20-24), and NPC asks offer nothing in return.
- **NPC-to-NPC deals are dark.** Striking one logs nothing, and outcomes go only to the two parties (EpisodeEngine.cs:1901, 1911).

### 1.4 Alliances

**What exists.**
- `AllianceState` has four fields: id, name, members, active (EpisodeState.cs:176-186; schema 1). The ledger adds its start and end weeks and a reason (schema 17).
- **Two ways to form one.**
  - 'Propose an alliance' passes a hidden gate (the NPC's view of the player at 8 or more) with no roll (StoryEffects.cs:34-41).
  - 'Propose an alliance invitation' is a deal whose chance is shown (PlayerDeals.cs:151).
- **What membership does.**
  - At nominations, +30 reluctance to nominate an ally; at the veto, +20 pull to save one (StrategyRules.cs:417, 442).
  - About +10 in the eviction ballot (WebEvictionVoting.cs:289-296).
  - Blocs vote together (WebVotingBlocs.cs:67-158).
  - 'Call the vote' (EpisodeEngine.Levers.cs:107-136).
  - The jury reads a standing alliance as 100 and an ended one as 25 (WebJuryVoting.cs:78-87).
- **NPC-only pacts.** Since agency (R6) they form about 1.2 times a season in the eight-house, as pairs only (IMPLEMENTATION-LOG.md:345).

**What is weak.**
- **Betrayal never ends or flags an alliance.**
  - An ally who nominates you or votes you out stays 'Allied' and keeps their protection.
  - Leaving costs you -15 and an 80 grudge from every member, so the victim is treated as the betrayer (EpisodeEngine.cs:1027-1032; StoryHooks.cs:49-53).
  - The ledger's permanent 'alliance-betrayed' entry is never written (RelationshipLedger.cs:55).
- **Zombie protection.** The player's pacts end only on the player's own view, so a partner who has turned keeps their +30 shield (NpcAlliances.cs:261-262).
- **Allying with everyone costs nothing.** There is no cap, no grudge check and no cost to a refusal. Because pacts are private, no ally learns about the others.
- **Allies share no information.** They answer 'where's your head at' like strangers (VoteRead.cs:66-83). `ClaimSource.Ally` is declared but never written (SeasonLedger.cs:195).
- **No way to build numbers.** There is no invite, merge or rename, and leaving a pact of three or more ends it for everyone (EpisodeEngine.cs:1029).
- **NPC pacts are invisible to the game's knowledge.** No listen-in, read or sighting adds knowledge of a pact. A bloc's coordination is never stated.
- **A weak final stage.** An NPC final HoH with the player in the final three evicts the player in 164 of 167 sampled seasons (SESSION-HANDOFF.md:1501; not investigated).

### 1.5 Defects found on the way

| # | Defect | Status | Evidence |
|---|---|---|---|
| X1 | Studying spends no action under the week rules, so five free studies give a permanent +5 in competitions. The diary says it costs one. | verified | EpisodePreparation.cs:14, 19; EpisodeEngine.cs:654; Week.cs:97-101 |
| X2 | 'Promise to evict X' is offered to X, and keeping it earns +15 with X, who becomes a juror. The rows also show to players who cannot vote. | verified | EpisodeDirector.cs:1464-1465; EpisodeEngine.cs:1036 |
| X3 | Deals do not record who broke them. Every reader counts a breach against both sides: NPC warmth, acceptance, threat, the jury, Game Sense, the jury read. Story odds show "you broke your word" when the NPC broke it. | verified (no field) | DealState.cs:19-51; NpcDeals.cs:66-68; PlayerDeals.cs:154, 281; WebJuryVoting.cs:103-114; StoryOdds.cs:55, 113-121 |
| X4 | Final-two deals and promises with people already evicted are marked broken at the final eviction: -45, a grudge, -50 with the jury. | to confirm | DealResolution.cs:152-158; EpisodeEngine.cs:914-916 |
| X5 | Evicted members stay in pacts of three or more, so the jury term reads 100 instead of 25. | to confirm | NpcAlliances.cs:291-292; WebJuryVoting.cs:81 |
| X6 | An ally's Hunt agenda writes -6 into the player's own view of the threat, which can silently end the player's pact with them. | to confirm | NpcSocialActions.cs:283-284, 501-506 |
| X7 | 'Whisper about X' reaches a random listener, not the person the player is talking to. | to confirm | EpisodeEngine.cs:1664-1666 |
| X8 | An NPC's rumour to the player silently moves both views by about 15. | to confirm | NpcSocialActions.cs:358-372, 501 |
| X9 | Knowledge-gate leaks. The deal and plea chance words are computed from hidden views, so comparing targets reveals whom an NPC likes or is secretly allied with. A refused 'Propose an alliance' costs nothing and reveals that the NPC's view of the player is under 8. | to confirm | PlayerDeals.cs:145-171; StrategyRules.cs:253-283; EpisodeEngine.cs:36-49 |
| X10 | The deal ceiling of 40 counts every deal in the season, not the deals held at once. Past it the propose rows vanish without a reason. | to confirm | PlayerDeals.cs:40-41, 63; EpisodeDirector.Conversation.cs:459 |
| X11 | Breaches fade. `deal_broken` entries decay, and promise outcomes are never recorded, against the ledger's own rule that betrayals never decay. | to confirm | RelationshipLedger.cs:15-19, 45-60 |
| X12 | The oath's copy says it does not bind the NPC, but a breach is detected in either direction and logged publicly. | to confirm | EpisodeDirector.cs:1374, 1378; WebLoyaltyOaths.cs:176-177 |
| X13 | Both nominees' veto asks can be accepted at once, which guarantees breaking one. | to confirm | NpcDeals.cs:288-319 |
| X14 | The story's fact list is capped at 128 and drops the oldest. A dropped private alliance fact then counts as public to every voter. | latent | StorySystems.cs:136, 158-159 |

## 2. Rules every slice keeps

1. **Captions are contracts.**
   - Tests and screen readers find controls by caption.
   - A mockup's relabel becomes a separate headline, eyebrow or subtitle. 'Begin the next competition', 'Talk to {First}', the house-move captions and every conversation row keep their words.
   - New captions are unique among live controls.
2. **The knowledge gate.**
   - Nothing during play shows hidden scores, true sentiment or NPC-to-NPC private events.
   - Odds, warnings and pages are computed only from what the player knows: their own reading, their reads and claims, known alliances, and public events.
   - X9's leaks are closed, not widened.
3. **Recorded seasons do not move.**
   - Every change to an outcome is keyed to a rules start week, so old saves and recorded seasons play as they did.
   - All of this plan's rule changes share **one** new boundary, proposed as `commitmentRulesStartWeek`. That is **schema 22**, and it needs a migration through `FrozenEpisodeV21`.
   - Its number is claimed together with the story session. New command kinds are appended after `LockFinalArgument`.
4. **Presentation first.**
   - Wave A changes no saved field and no outcome, so it can ship before the schema is claimed.
   - A rule slice never hides behind a screen change, and a screen slice never sneaks in a rule change.
5. **Layout.**
   - Label boxes are at least 1.3 times their font.
   - Layouts hold at FontScale 1.0 and 1.2, on the 16:9 frame and the 4:3 batch canvas, for houses of 3 to 16.
   - In the frame a layout rebuilt, a lookup by name takes the last child.
   - Every visible change is checked by a capture.
6. **Tests.**
   - Each defect slice starts with a test that fails on main.
   - Each slice raises the floors in its merge commit.
   - Behaviour that changes the audited walk is fixed in the walk in the same slice. X1's fix moves PortVerification.Study.cs:56-75.

## 3. Decisions for the owner

The slices take the recommended option unless the owner rules otherwise.

*Answered on 2026-10-01: the owner took every recommended option ("Yes, go with your recommendations and start wave 0 and A").*

1. **Schema 22.**
   - (A, recommended) Claim it now for `commitmentRulesStartWeek` and tell the story session when it is next live, as schema 21 was.
   - (B) Wait until the story session agrees.
2. **Move-in night.**
   - (A, recommended) Fix the wording now, and give move-in night two seats of its own behind the boundary.
   - (B) Wording only.
3. **Free time's seats.**
   - (A, recommended) Floor the after-eviction window at 2, which gives an eight-house seven a week.
   - (B) Keep 1.
4. **The free time screen's story beat.**
   - (A, recommended) A banner that opens the beat as a step, as the mockup draws it, with a 'Back to free time' secondary while it is open.
   - (B) The choices inline in the hero.
5. **The word under each name.**
   - (A, recommended) The player's own standing, as today.
   - (B) The houseguest's public mood.
   - (C) Both, the mood as a small chip.
6. **Finding a houseguest.**
   - (A, recommended) Two pages of cards past ten (eight at the larger text). A search box steals the hotkeys and adds a Tab stop for at most fifteen names.
   - (B) Search, as drawn.
7. **The tip line and the clock.**
   - (A, recommended) A tip as the footer strip's lowest-priority line, from existing rule copy, and no clock.
   - (B) Drop both.
8. **The mockup's 'Continue Free Time · Stay in the house'.**
   - (A, recommended) The footer's secondary 'Stay in the house', which closes the screen as Esc does, styled as the mockup's blue. Beside it, the pinned way on keeps 'Begin the next competition' under an 'END FREE TIME' headline.
   - (B) Only the way on.
9. **'Explore the house'.**
   - (A, recommended) A new tile that shows the overview map and closes the panel to walk. Walking into a room with a pair already offers a walk-in.
   - (B) Leave it out.
10. **The player's alliance cap.**
    - (A, recommended) Three at once.
    - (B) None.
11. **Betrayal.**
    - (A, recommended) An ally who nominates you, votes against you, ignores your call or breaks a deal is flagged. Their protection ends, and you may cut ties that week at no cost or keep the pact.
    - (B) Flag only.
12. **Secret alliances can leak.**
    - (A, recommended) Yes, after the alliances page lands, at the web's numbers: 5% a week, plus 3% for each member past two, plus more for a player juggling pacts.
    - (B) No.
13. **Negotiation.**
    - (A, recommended) One counter-offer round per proposal, and NPC asks that come with a price.
    - (B) Counter-offers only.
14. **Reputation.**
    - (A, recommended) A public 'your word' reading built only from breaches the house saw. It moves acceptance odds and is shown to the player.
    - (B) Hidden.
15. **Accepting an offer commits you.**
    - (A, recommended) Acceptance warmth drops from +12 to +4, and a deal broken after acceptance weighs one step heavier.
    - (B) Leave it.
16. **The conversation grouped by intent** (Bond, Learn, Scheme, Bargain) with a person picker, keeping every caption.
    - (A, recommended) Yes.
    - (B) Keep today's rows.
17. **The Final 3 window onto the new free time board.**
    - (A, recommended) Later, after F1.
    - (B) With F1.

## 4. Slices

Effort: S is hours, M one to two days, L three to five days. 'Kind' says whether a slice is presentation only, a rule change behind the boundary, or a save-format change.

### Wave 0: defects that need no boundary

**X-a. Copy and display fixes.** Presentation. S.
- Hide 'Promise to evict X' in X's own conversation and from players who cannot vote (X2's rows).
- Say why the propose rows are gone past the ceiling (X10).
- The oath's copy matches the engine (X12).
- Accepting one nominee's veto ask declines the other's, with a line saying so; this is presentation over the existing decline (X13).
- Move-in night's rule reads '1 action tonight; it does not carry into the week' instead of 'After the eviction' (1.1).
- **Tests:** the rows' absence; the copy as written; an EditMode test that move-in night in an eight-house has one seat and the opening spends nothing.

### Wave A: visibility, presentation only

**F1. Free time on the strategy stage** (mockup 87). Presentation. M-L.
- **The hook.** `FreeTimeBoard(state)` beside `NominationScreen` (EpisodeDirector.cs:1561) draws the whole screen at the stage's full width. It does not apply when a diary reflection is pending, when a legacy (non-story) house event is waiting (that keeps the band, its camera framing and the Social pin, EpisodeDecisionContextPlayModeTests.cs:18-83), or at the Final 3 (decision 17).
- **The hero row.**
  - On the left, about 60%:
    - a waiting reply card, inline like the campaign's plea strip (EpisodeHud.CampaignBoard.cs:259-307);
    - else the first waiting story beat as a banner with an 'Answer' control that opens it as the step (decision 4; the precedent is `NominationStoryStep`, NominationScreen.cs:281-336);
    - else the current play.
  - On the right, about 40%: the budget card, named 'Screen head'. It shows FREE TIME, the actions left as a large number, `BudgetRule`, the unused-actions note, and the two 'Buy an action…' buttons with their captions and their goodwill price.
  - The copy is truthful: "Talking, listening in and house meetings each spend one action. Walking the house, house activities and answering anyone who comes to you are free. Unspent actions are lost when you begin the next competition."
- **The story strip.** One line: the current play, threads and storylines (other waiting beats as pressable chips), and preparation n/5. It keeps the names 'Current play' and 'Threads card', since the story session asked that plays and threads stay in view (FreeTimeScreen.cs:364-369). The Have-Not line goes here word for word when it applies (HaveNots.cs:84-97).
- **TALK TO A HOUSEGUEST.**
  - One row of cards, the houseguests in the player's room first.
  - Each card has a portrait, the name (still a control that opens the houseguest's screen), the player's own standing word, and 'Talk to {First}'.
  - Past ten cards (eight at FontScale 1.2), 'Previous houseguests' / 'Next houseguests' page through them (CampaignBoard.cs:51-52; decision 6).
- **OTHER WAYS TO SPEND YOUR TIME.** Five tiles under the name 'House moves': Explore the house (new), Listen in on a conversation, the two house meetings, and Do an activity (the House Activities panel). Each shows its cost or 'Free'. When no action is left, a tile that costs one is locked with the reason.
- **The footer.**
  - The strip holds, in priority order: the storylines-passing warning, then the location ('Nomination Room · you have it to yourself'), then a tip.
  - The secondary slot holds 'Stay in the house' (decision 8). 'Show on map' moves into 'Explore the house'.
  - The pinned way on is 'Begin the next competition', under the headline 'END FREE TIME'.
- **The houseguest's screen** ('Talk privately', 'Ask for information', 'Pitch a deal', YOUR CONTEXT, ABOUT) stays as it is; moving it is a later polish.
- **Height budget.** On the 1360 by 800 frame the body has 621 units (609.6 at 1.2).

  | Row | FontScale 1.0 | FontScale 1.2 |
  |---|---|---|
  | Hero | 112 | 134 |
  | Story strip | 26 | 31.2 |
  | Two headings | 68 | 81.6 |
  | Tiles | 70 | 84 |
  | Gaps | 36 | 36 |
  | **Left for the cards** | **309** | **243** |

  An eight-house fits one row at both scales. A sixteen-house takes two pages. Two rows of cards never fit, which is why the cards page.
- **Re-pins.**
  - PhasePanel.cs:85, which expects the Stage layout (it becomes Strategy).
  - FreeTimeScreens.cs:50-81 (the main and side columns, and the unused-actions note becoming a label in the budget card).
  - `AssertOnTheStrategyStage`, which must accept the secondary.
- **New tests.**
  - No scroll at both scales for houses of 3, 8 and 16: with a beat waiting, with a reply card, as a Have-Not, and with nothing waiting.
  - The pager reaches everyone.
  - Opening a beat and going back.
  - The keyboard ring.
  - The walks still press 'Begin the next competition' once.
- **Risks.**
  - The campaign reuses `HouseMoves` (CampaignScreen.cs:101-104), so the tiles change for both screens or neither.
  - The footer holds one warning and one secondary per render.
  - Story beats on the stage lose the camera's framing (Camera.cs:102); scene cards keep it.
  - Tell the story session that the story strip replaces their threads card.

**V1. Your word.** Presentation. M.
- **A notebook page** of every open and settled commitment in the player's knowledge: promises, deals, oaths and alliance calls. Each shows who it is with, what it binds, until when, and how it ended.
- **A breach warning on each decision screen.** It is a dry run of `DealResolution.Verdicts` and the promise and oath checks on a cloned state, so nothing is written.
  - The nomination picker: 'Nominating Alex breaks your safety deal with him'.
  - The veto decision, the ballot and the final selection.
  - The nominee comparison shows deals as well as promises (DecisionContext.cs:27-33).
- **Notes** say whom a deal is about and how it ended (HouseguestNotes.cs:52-62).
- **Tests:** the warning appears exactly when the dry run breaks something; the page matches the ledger; the clone is never committed.

**V2. Offers announce themselves.** Presentation. S.
- **A badge on the cast strip** for every waiting offer, plea and card, computed from `NpcDeals.Pending` and the reply cards.
- **An objective line:** "Alex has an offer for you".
- **"Offers expire tonight"** on the last window's way on, and the unused-actions note in every window, not only free time (FreeTimeScreen.cs:144-149).
- **Tests:** a badge per pending offer, and none once answered or expired.

**V3. The alliances page.** Presentation. M.
- **The player's pacts.** Each shows members, the week it formed (from the ledger), calls with who followed and who defected (`ledger.calls`), and the deals between members.
- **Suspected pacts.** NPC pacts the player has evidence of, graded by `FinalistRead.AllianceCertainty`, with the evidence listed.
- **Strength** is shown only through standings the player knows.
- **Where it lives:** the notebook's section list and the relationship web.
- **Tests:** knowledge-gated content (an unknown NPC pact never shows), at both font scales.

**V4. Your week.** Presentation. M.
- **A panel in the weekly recap:**
  - whether each vote read was right at the reveal (VoteRead.cs:177-178);
  - the claims that proved true or false;
  - the deals and promises judged;
  - the calls and who followed;
  - Game Sense so far (GameSense.cs; today it is shown only at the season's end).
- **Tests:** the recap's lines against a seeded week.

**V5. The conversation grouped by intent.** Presentation. M-L.
- **Four groups:** Bond (the dial, real time, room acts), Learn (asks, reads, intel), Scheme (rumours, lies, venting, schemes) and Bargain (deals, promises, calls, pleas).
- **A person picker** where a verb has a target, in place of four rows per houseguest. This cuts about 60 rows to about 15.
- **Every caption keeps its words.** The picker's choices are captioned by name, as the WHO? grid's are.
- **Each verb** carries a tag of what it is for (warmth, learn, risk), and 'Free' where it costs nothing (decision 16).
- **Tests:** every caption is still reachable and unique; the keyboard ring; the season walks and the audited walk.

**V6. Honest odds.** Presentation over the engine's roll. S-M.
- **Shown odds come from what the player knows.**
  - The deal and plea chance words are computed from the player's reads, the claims they hold, the alliances they know of, and their own reading.
  - An 'unknowns' chip appears when the player knows little.
  - The roll itself is unchanged.
- **Mislabelled tiles are fixed.** The risk and cost labels on 'Ask for information' and 'Vent' (FreeTimeScreen.cs:413-415; Conversation.cs:267) say what the verbs really cost and risk.
- **Closes:** X9's odds half. The refusal half is C4.
- **Tests:** an EditMode test that two targets with different hidden views, but identical known facts, show the same odds word.

### Wave B: the boundary and your word

**R0. The boundary.** Save format. S-M.
- **The field.** `commitmentRulesStartWeek` (schema 22), migrated through `FrozenEpisodeV21`, enabled for new seasons by the director as the other rule starts are.
- **What it gates.** X1, X6, X7 and X8 move behind it:
  - **X1:** study spends the window's action, and PortVerification.Study moves with it.
  - **X6:** Hunt writes a log line, not the player's view.
  - **X7:** a whisper reaches the person the player is talking to.
  - **X8:** an NPC's rumour to the player says what was said and moves only the subject's standing in the player's notes.
- **Decision 1, and the load pins.** Claim the number with the story session. Move the load pins to 22, as schema 21 moved 51 of them.

**C0. Who broke it.** Save format. M.
- **The fields.** `brokenById` and `settledWeek` on deals, and the same on promise outcomes.
- **The migration** infers them for old saves with `FinalistRead.DealBreaker`'s rule.
- **Every reader in X3 is fixed:** NPC warmth, acceptance, the 'I've heard you've broken deals' line, threat, the jury, Game Sense, the jury read, and StoryOdds.
- **Breaches stop fading (X11).** `deal_broken` is permanent, and promise outcomes are recorded.
- **Tests:** a victim is never penalised; the migration's inference on fixtures.

**C1. Every deal does something, and accepting commits you.** Rule. M.
- **Information-sharing** passes one reading a week while it stands.
- **Partnership** is judged at the vote.
- **A safety deal is kept**, and rewarded, when the partner as HoH spares you.
- **A final-two deal** enters the final HoH's choice as a real obligation term (EpisodeEngine.cs:415-422).
- **Deals and final-two promises end with an evictee**, and the final selection skips partners who are no longer active (X4).
- **Accepting warmth drops to +4**, and a breach after acceptance weighs one step heavier (decision 15).
- **Tests:** each kind's verdict; X4's fixture first.

**C2. Betrayal as an event.** Rule. M.
- **The trigger.** An ally who nominates the player, votes against them, ignores their call or breaks a deal with them flags the pact.
- **What it writes.** A log line, the ledger row's end with '/betrayed', and the dormant 'alliance-betrayed' entry (RelationshipLedger.cs:55).
- **What it changes.** The betrayer's protection terms stop counting (StrategyRules.cs:417, 442; WebEvictionVoting.cs:289-296).
- **The player's choice.** They may cut ties that week with no grudge from the others, or keep the pact (decision 11).
- **Leaving outside a betrayal** stays as it is.
- **Tests:** each trigger; the free exit's window; no 80 grudge on a betrayal exit.

**C3. Two-sided alliances.** Rule. M.
- **The NPC's own commitment drives their terms:** the shield, the veto pull, the vote, the jury, and following a bloc call.
- **A partner who has turned** can end the pact from their side (NpcAlliances.cs:261-262).
- **The player learns** of a cooling ally through ignored calls, reads, and a 'gone quiet' line in their notes. Never through a number.
- **Evictees leave pacts of three or more (X5).**
- **Tests:** a turned partner's shield ends; the jury reads 25 for a juror out of a pact.

**C4. One way to form an alliance.** Rule. S.
- **'Propose an alliance' rolls** on the invitation's odds, shown as V6 shows them.
- **Grudges of 40 or more refuse.**
- **The player holds at most three pacts** (decision 10).
- **A refusal spends the action**, which closes X9's free probe.
- **Player pacts record 'alliance-formed'**, so they build trust as NPC pacts do (NpcAlliances.cs:238).
- **Tests:** the cap, the refusal's cost, the shown odds against the roll.

**C5. Grow and manage alliances.** Rule, with new command kinds. S-M.
- **'Bring {name} into {pact}'.** Every member consents through `WouldPropose`.
- **'Rename {pact}'** from preset names.
- **'Leave {pact}'** in a pact of three or more takes only the player out, which is the story's defector rule (StoryEffects.cs:176-188).
- **The founder is `members[0]`.** The invitation's member order is fixed (EpisodeEngine.Strategy.cs:153-156), and the founder feeds the bloc's caller (WebVotingBlocs.cs:208-234).
- **Command kinds** are appended after `LockFinalArgument`, with numbers agreed with the story session.
- **Tests:** consent, a leave from a pact of three, the bloc's caller.

**C6. Allies share intel.** Rule. M.
- **An ally answers 'where's your head at' honestly**, never deflecting (VoteRead.cs:66-83).
- **'Hold an alliance meeting'** works in any private room: +3 for every pair, and one `ClaimSource.Ally` claim. The claim comes from a member's current ballot projection, with no roll, and is judged at the reveal.
- **Tests:** an ally's claim matches their projection; a stranger's deflection is unchanged.

**C7. Negotiation.** Rule, plus a save field. M-L.
- **One counter-offer round.** A refused proposal may come back as a counter on a keyed `StoryRandom` coin, so the season's random stream is untouched.
- **NPC asks carry a price.** A veto ask offers a vote save or a final two. That needs `linkedDealId`, a schema-22 field.
- **Situation actions from the web build:**
  - call in a promise (remind, demand, threaten);
  - mend fences after a breach;
  - a veto for a price.
- **Tests:** the counter's terms; a linked deal's resolution; the random stream unchanged.

**C8. Breaches become house knowledge, and your word has a reputation.** Rule. M.
- **A broken deal writes a 'broken word' fact** that the existing gossip can spread (StorySystems.cs:211-237). Today it is only narrated (StoryHooks.cs:362).
- **The player's reading changes only with a whispered line** saying why.
- **A public 'your word' reading** is built only from breaches the house saw. It moves acceptance odds, and the player sees it (decision 14).
- **Keep alliance facts out of the 128-fact eviction (X14).**
- **Tests:** the spread's audience; the reading's inputs are public only.

**C9. The endgame.** Rule. M-L.
- **A final-three deal kind**, as a new string with no new field.
- **The NPC final HoH's choice** weighs pacts, deals and jury winnability (EpisodeEngine.cs:415-422). Investigate the 164-of-167 figure first, with a harness over sampled seasons.
- **Tests:** the harness's distribution before and after.

### Wave C: the economy, behind the same boundary

**E1. Seats.** Rule. S.
- **Move-in night has two seats of its own** (decision 2).
- **The after-eviction window is floored at two** (decision 3).
- **Re-measure the greedy scripted walks and Game Sense** (GameSenseHarnessTests.cs).
- **Tests:** the window sizes for houses of 3 to 16.

**E2. Every verb has a job.** Rule. M.
- **Personal chat** becomes the lore verb: more facets, less warmth.
- **'Talk game openly'** carries information that 'Talk tactics' does not.
- **'Spend time together'** leaves the dial.
- **'Air everything'** reveals who sided with the player, as read entries.
- **Reply cards** stop having one dominant answer: each answer gains something the others lack (a claim, a promise, a deal).
- **Tests:** the expected value of each verb, from a harness; no strictly dominated verb.

**E3. Aim it.**
- **'Ask what they think of {nominee}'** spends the same roll.
- **A picker for what to share.**
- **'Buy an action by burning one bridge'** lets the player pick the bridge.
- **Kind:** rule for the targets, presentation for the pickers. **Effort:** S-M.
- **Tests:** each target is honoured.

**E4. The HoH hears the house.** Rule (a new reply card kind, which validation learns). M.
- **Houseguests courting a player HoH** arrive as pitch cards before the nominations. Today `CourtTheHoH` only logs "came to see you" (NpcSocialActions.cs:304-311).
- **A free 'Feel them out'** comes with each card.
- **Tests:** cards per courting NPC; answers' effects.

**E5. The speech from the block counts.** Rule. M.
- **It moves persuadable voters** by a bounded amount, keyed to what it says and who heard it (STRATEGY-LOOP-PLAN.md:169-170).
- **Tests:** the bound; no effect on the player's own ballot.

### Wave D: the bigger bets

**B1. One commitments model.**
- **One record and one page.** Promises, deals, oaths and alliance calls share a record and V1's page, with due dates, warnings, betrayal events and the reputation.
- **The overlapping verbs merge** behind captions that stay:
  - the safety promise and the safety deal;
  - the final-two promise and the final-two deal;
  - 'Promise to evict' and the vote deal;
  - the nominee's plea and their vote-save offer.
- **Builds on** V1, C0, C1, C2 and C8. **Effort:** L.

**B2. A living house all week.**
- **NPCs act throughout every window**, not all at once at free time's and the campaign's openings (EpisodeEngine.cs:280-327; NpcSocialState.cs:44).
- **A feed of what the player could have witnessed**: sightings, overheard lines, walk-ins.
- **Effort:** L.

**B3. The war room.**
- **A pact of three or more meets once a week.** Members suggest targets from their own projections, and the player agrees, counters or lies low.
- **The agreed plan becomes the week's call.** Follow-through is tracked on the alliances page.
- **Effort:** M-L.

**B4. Secrets leak.**
- **Alliance facts become whispered on keyed odds** (decision 12).
- **Allies who learn about the player's other pacts** give a double-dealing receipt or hold a grudge.
- **A listen-in on an allied pair** makes the player a suspected knower, which V3 then shows.
- **Effort:** M-L.

## 5. Order and landing

**Order.**
1. **Wave 0 and wave A first.** They change no saved field and no outcome, so they can land in one PR: the free time screen the owner asked for, the Your word page, the offer badges, the alliances page, the recap's week, the grouped conversation and honest odds.
2. **Then the boundary.** Claim schema 22 (decision 1) and land R0 with C0.
3. **Wave B,** in the order C4, C2, C3, C1, C6, C5, C7, C8, C9.
4. **Wave C.**
5. **Wave D**, as the owner chooses.

**Wave 0 and wave A as built (2026-10-01).** Seven slices, grouped so that no two edit the same code:
- **W1, free time:** F1, the move-in wording from X-a, and V2's free-time half (costed tiles locked, the unused-actions note in the budget card).
  - Owns EpisodeDirector.FreeTimeScreen.cs, EpisodeHud.FreeTime.cs, the new free-time board files, and the free-time hook in Render.
- **W2, the conversation:** V5, plus X-a's promise rows and oath copy, plus V6's verb tags.
  - Owns the conversation section of EpisodeDirector.cs and the conversation HUD. It may move the call to DealPanel but not change its insides.
- **W3, honest odds and offers:** V6's odds, plus X-a's deal-ceiling reason and the paired veto asks.
  - Owns the shown odds in DealPanel (EpisodeDirector.Conversation.cs), the plea panel and VetoOffers (EpisodeDirector.Strategy.cs), and a new Simulation reader of what the player knows.
- **W4, your word:** V1.
  - Owns a new notebook page, the breach warnings on the decision screens, DecisionContext's deals and the notes' deal lines.
- **W5, the alliances page:** V3.
  - Owns a new notebook page and the relationship web's suspected pacts.
- **W6, offers announce themselves:** V2 outside free time.
  - Owns the cast-strip badges, the objective line, and the strategy screens' footer notes (never free time's).
- **W7, your week:** V4.
  - Owns the weekly recap.

W4 and W5 both add a notebook page, which is an expected adjacent-line merge.

**Landing.** Each wave lands as before:
- each slice in its own worktree, reviewed adversarially and fixed;
- merges with `--no-ff`;
- the offline compile and the Unity-free subset;
- the full suites on the UMA-free D: copy;
- the audited pipeline, run alone from the main checkout;
- a PR merged with a merge commit once CI passes.

**Measure first, where nothing has been measured:**
- how often NPCs deal with each other (no harness counts it);
- the 164-of-167 final HoH;
- how often a season reaches the deal ceiling.

**Tell the story session:**
- the schema 22 claim, and any new command kinds;
- that F1's story strip replaces their threads card;
- what C2, C3 and C5 change in alliances;
- that X14's fact list keeps alliance facts.

## Build log

**Wave 0 and wave A, built 2026-10-01** on `claude/nearby-render-gap` from a293afd. Seven slices, each built in its own worktree, reviewed adversarially, fixed, and merged with `--no-ff`. All of it is presentation: no saved field, no command kind, no outcome changes.

| Slice | Merge | What landed |
|---|---|---|
| W3, honest odds | 8e73734 | `KnownOdds` computes the deal, plea and alliance chance words from the roll's own formula with each hidden term replaced by what the player knows. A note says each chance is the player's read, and 'Many unknowns' appears when they know little. The roll is unchanged. A table emptied by the deal ceiling says why (X10). Once the player has given one nominee their word on the veto, the other's accept is locked, and they can turn it down or let it lapse (X13). |
| W7, your week | 64ceef8 | The weekly recap's 'Your week' tab: the whip count and each claim judged against the ballots read aloud, the player's deals and promises by the outcome lines they were told, their alliance calls by who followed, and Game Sense so far from the notes the player could know. |
| W5, the alliances page | a47ab57 | A notebook page opened from the relationship web's 'Your alliances' door: the player's own pacts in full, other pacts only where the player has evidence, an ended pact's reason only from what the player was told. |
| W6, offers announce themselves | e417215 | A badge on the cast strip and an objective line for every offer, veto ask and reply card waiting on the player's answer, shown only while it can still be answered. The strategy footer's second line, under Up next, says what moving on costs. |
| W4, your word | eabafa1 | A notebook page of every promise, deal, oath and alliance call the player is a party to, opened from a door in the notes page's head. Each decision screen runs the choice through the engine on a copy, only when something that decision settles is standing, and warns before a choice breaks a commitment; on the footer the breach is a warning-rank line. |
| W2, the conversation by intent | 550cab0 | Bond, Learn, Scheme and Bargain, each verb tagged with what it is for; a verb aimed at someone opens a person picker. 'Promise to evict X' is hidden from X and from a player who cannot vote (X2); the oath's copy matches the engine (X12). |
| W1, free time | 0a0d20b | The free time board on the strategy stage, the move-in wording from X-a, and V2's free-time half. |

**Integration fixes:** 42242e5 (two slices' test helpers shared a name); 5ed99c3 (the offers' reader counts move-in night's lost actions as the board does); 950da51 (the Your word page's mark stood before anything was in its column; the alliances fixture asked for week two with four houseguests active, which a six-house cannot give, since the week turns as free time ends); 67d18c3 (the conversation's four-picker fit test says it needs ten minutes).

**Found while landing, not of this plan:** fcb3400 and 07a8e15 fix the first night the owner could not leave. The editor starts play without a domain reload, so `CeremonyOverlays`' frame stamp outlived the session that wrote it and read as a card on screen for the first hour of the next season, swallowing E, Escape, the prompt's button and every click on the house. The stamp holds only this frame and the last, never a frame still to come, and play's start forgets it; pressed again at the screen after its own warp, 'Go to episode screen' opens it.

**Where the build departs from the plan:**
- **V2.** 'Offers expire tonight' was not built: offers lapse as the next week's campaign opens, the engine's rule, not at the week's end; the footer's second line says when each lapses.
- **X13.** Leaving the second nominee's ask to lapse costs Game Sense, so the locked row's line states both outcomes.
- **V5.** The pleas stay above the dial, not in Bargain. The person picker's buttons carry their rows' full sentences, as the captions contract requires, with the player's own trust reading and an ALLY mark. The pill words go beyond warmth, learn, risk and Free: 'binds you', 'undermines them', 'costs warmth', 'steers the vote', 'practice', each true of its verb.
- **F1.** The houseguest's screen is unchanged and the story strip shows one play. A beat opened from the board is drawn at the board's full width. The rule line and the unused-actions count say what moving on actually loses: on move-in night only the night's own actions, since bought time carries into the week. For 0.45 s after a step closes the free-time panel takes no pointer press, so a double-click's second click cannot buy an action, make a move or end free time; keyboard and programmatic presses are unaffected. 'More waiting' is a control.
- **V1.** The ballot's warning sits over its cards, inside `BallotCards`, so Confirm stays in view. Your word opens from a header door, not a filter pill.

**Found on the way, for wave B and C:** X10's ceiling counts NPC-to-NPC deals; the Keep plea's `Allied` has no `a != b` guard; overheard standings are one-way; `PlayerDeals.Reasoning` answers from the hidden view (C4); the story's Spread grants knowledge of every pact containing the pair (C8, B4); the engine still accepts 'Promise to evict X' made to X, which R0 should refuse behind the boundary; move-in night's spending is cleared as its window closes, so the week's extras come back whole in week 1 even when spent that night (decision 2's two seats); the houseguest screen's own 'Back to free time' and the Pull's scene card are outside the press guard; the conversation's fit test takes over two minutes and should be split.

**Floors:** the Unity-free subset 1161 to 1254; EditMode 2033 to the count in Tools/baseline.txt at the floors commit; PlayMode 744 to 772.

**R0 and C0, the boundary and who broke it, built 2026-10-02** in their own worktree from `claude/nearby-render-gap` at 7991687. Schema 22, claimed now (decision 1); the story session is told by the lead.
- **The boundary.** `commitmentRulesStartWeek` (`EpisodeEngine.EnableCommitments`, `CommitmentRulesOn`). The director starts every season under it from week one and the importer from the week after the import; the V21-to-V22 migration writes 0, so a season saved before it plays without it to its end, as with the finale rules; every season a test builds directly is off.
  - Every outcome change below checks it, so a season without it draws the same rolls, mints the same sequence numbers and logs the same lines.
  - Evidence: 54 seeded seasons played by a busy scripted player (studies, whispers, call-outs, promises, deals, offers answered, reads; the director's, the legacy and the strategy harness's rule sets; houses of 6, 8 and 12) digest byte for byte the same before and after, the state and every reader this slice touches at every phase change. With the rules on, 50 of the 54 move (51 after the review's fixes).
  - The harness is committed as `CommitmentRulesSeasonDigests` (Tools/SimulationTests only, `[Explicit]`, compiled out of Unity), with the 54 digests 7991687 plays, recorded by the same file compiled against that build's simulation: `dotnet test Tools/SimulationTests --filter "FullyQualifiedName~CommitmentRulesSeasonDigests"` reproduces both halves. Game Sense's notes are digested without their weekly-recap flag, which the review changed everywhere for a vote deal's ending; with the flag in, the 54 are identical too.
- **R0 behind it.**
  - X1: a study is one of the window's actions. The audited walk's study opt-in buys the actions move-in night lacks through the board's 'Buy an action at the whole house's expense' before its five confirmations, and checks each confirmation spends one (PortVerification.Study).
  - X6: a pact-mate's hunt told to the player is the log line, and the player's view of the threat does not move.
  - X7: 'Whisper about X' reaches the person the player is talking to. The conversation names them as the command's second target, which a season without the rules ignores, drawing as before; under the rules a whisper naming nobody is refused.
  - X8: a houseguest's rumour to the player says what was said ("Alex told you Maya is the biggest threat in this house."), the player remembers who said it, and only the player's own view of its subject moves, one way, by the rumour's weight, with no roll. Not toward a pact-mate: the line and the memory stand, the record holds it at nothing, and the view does not move, so a rumour cannot end the player's pact at the week's turn without a word.
  - Talk about the player that the player never heard - a rumour about them, or a hunt with them as its threat - moves the listener's view of the player and not the player's view of the listener (`EpisodeEngine.HeardAbout`, the engine path's one half, no roll).
  - From the found-on-the-way list below: a promise to evict X made to X is refused.
- **C0.**
  - `brokenById` and `settledWeek` on deals and promises, written by every settlement under the rules. A voting bloc names nobody: both walked away from it.
  - `Breaches` reads them for every X3 reader: the record where it names somebody; nobody for a bloc; for a deal settled before the record, `FinalistRead.DealBreaker`'s rule; for a promise, its maker.
  - Under the rules a breach counts against whoever broke it in NPC warmth (`NpcDeals.Adjusted`), acceptance and the 'I've heard you've broken deals' line (`PlayerDeals`), the odds shown (`KnownOdds`), threat (broken promises, and trust through a ledger now written one way), the eviction vote's threat term (`WebEvictionVoting.Threat`: the native adapter carries who broke it, so a Head of Household who nominates their safety partner no longer puts them up with three more points of threat in every ballot), the jury (`WebJuryVoting.Obligations`), Game Sense (a deal the other side broke is a note worth nothing), the jury read (only a breach of the player's own that the player can know of cools a juror, dated by the record) and `StoryOdds`.
  - X11: a broken deal is held by the one wronged, permanently; the one who broke it keeps a record weighing nothing, so their own week still says it. A betrayal heard is held one way. A promise's outcome is recorded, kept or broken, permanently.
- **The migration** goes through `FrozenEpisodeV21` and writes literals: 0 for the boundary, null and 0 on every deal and promise. A save the schema 21 build wrote itself (`Fixtures/V21CommitmentsSave.json`, made by 7991687's own simulation and `EpisodeSaveStore`) loads through the whole chain in `PersistenceV22MigrationTests`.
- **Validation** holds the records to how they are written: together, at a settlement, no earlier than the rules; a broken, settled deal other than a voting bloc names one of its sides; a broken, settled promise names its maker.

**After the review (fix-then-land, 2026-10-02):** the eviction vote's threat term (M1, above); the walk counts the actions to buy again on reaching the board, since the walk there crosses free roam; X8's pact-mate exception and talk about the player (above); the jury read's knowledge gate; Game Sense's kept and broken deal notes wait for the weekly recap until the player can know how a vote deal ended, in every season (the recap's visibility, not an outcome: the verdict counts them either way); the weekly recap's movements leave out an entry that tells a ballot the player cannot place, as the web's history does, in every season; the free-time board's cost copy names a study under the rules (`FreeTimeCostCopyWithStudy`, no caption changed); and the tests the review asked for - a whisper pressed in a conversation (PlayMode), a voting bloc settled by the engine, a betrayal heard one way, the v21 fixture and the committed digest harness.

**One PR for wave B.** Every wave-B rule shares this one start week, `commitmentRulesStartWeek`: a season started on a build with R0 and C0 alone would play under the rules as they stand now, and move when C1 to C9 land behind the same boundary. So wave B reaches main as one PR - R0, C0 and C1 to C9 together - and the integration branch carries it until then.

**Where the build departs from the plan:**
- **C0's migration** writes nothing it would have to guess (the lead's ruling): readers fall back as above. For a promise the inference would be pure, its maker, and `Breaches` reads it so; for a deal it is not, since an NPC pair's vote deal is settled by ballots no save keeps.
- **X8's 'standing in the player's notes'** is the player's own view of the subject, the standing word on the cards and the web, with a memory the notes page reads.
- **The walk** buys move-in night's missing actions rather than spreading five studies over the season, so every later study check still reads preparation 5.

**Found on the way, for wave B and C:** the eviction vote's deal obligation (`WebEvictionVoting.DealObligation`'s −35 for a broken deal between the voter and the nominee) and the strategy windows' nomination reluctance (`StrategyRules.NominationReluctance`) still read a broken deal between the two either way - C2 and C3 (the vote's is parity-pinned, and both are arguably a breaker's reason too); a vote deal both parties broke names only the first breaker on the record (`DealResolution.VoteDeal` into `RecordBreach`) - C1; the editor's v5 QA save (`LegacySaveFixtures`) has not stripped the schema 14 and later fields since schema 14; the Your word page and the notes could read the record for the player's own breaches; a save's checksum is over the runtime's text of its doubles, so a runtime that writes them differently (.NET's shortest round-trip against Mono's seventeen digits) reads every save holding such a double as damaged - worth knowing before the editor's runtime changes.

**Floors:** the Unity-free subset 1328 to 1352 (`CommitmentRulesTests`, 24); EditMode 2232 to 2265 (those 24, `PersistenceV22MigrationTests`' 7, the cost copy and the weekly recap's privacy); PlayMode adds 2. Outside the editor, the persistence suites with `EpisodePreparationTests` and `EpisodeVotingBlocTests` (481 cases, every pin moved to 22) pass, compiled with Gamesim.Persistence against a one-line `Application` stub in a scratch project; that run found the V22 fixture marking the nominations' competition resolved, which refused every command once the veto's opened. The EditMode suite ran on the UMA-free copy at 6213f59, 2256 of 2256, and after the review at 655a1ba, 2264 of 2265: the v21 fixture's own envelope read as damaged in the editor (its checksum is over a double's round-trip text, written by .NET), so the test seals the v21 build's state again for the runtime it runs in.

**C4, one way to form an alliance, built 2026-10-02** in its own worktree from `claude/nearby-render-gap` at 50ebd57. Everything is behind `commitmentRulesStartWeek`. No saved field, no command kind, and no caption changed.
- **The proposal rolls.** Under the rules 'Propose an alliance' (`FormAlliance`) asks the alliance invitation's question.
  - One draw from the season's stream, taken first, against `PlayerDeals.AcceptanceChance` for an `alliance_invite` (`EpisodeEngine.AllianceChance`).
  - A yes forms the pact as before: +8 with its reciprocal draw, and the same line.
  - A no logs "Maya Hassan turned down your alliance. “…”" to the two of them and moves nothing else. It is logged under its own kind, `alliance-refused`, so it is never drawn with an alliance's green and handshake or read as one of the week's alliance lines.
  - Either answer spends the action, so a refusal is no longer a free look at the houseguest's view (X9's refusal half).
  - Without the rules the gate of eight stands, with no roll, as it always did.
- **A grudge of 40 or more** held against the player refuses whatever the draw (`GrudgeRefusesAlliance`, where grudges exist). The draw is taken all the same, so a proposal is always one draw. The deal table's 'Propose an alliance invitation' asks the same question with the same grudge.
- **Three pacts at once** (decision 10, `PlayerPactCap`). A fourth is refused before anybody is asked, with the reason, and spends nothing, because the player can count their own pacts. That holds for every way in:
  - the proposal;
  - the deal table's invitation, which is not offered (`CanPropose` gives the reason);
  - the yes to an invitation put to the player (the no is still theirs), except where no pact would come of it: without the strategy windows an agreed invitation is only a deal;
  - a story's pact, of two or of three, which is skipped, as one with a houseguest below eight always was.

  Nobody puts an invitation to a player who holds three: `NpcDeals.Offer`'s first rung (partners, safe and warm) asks for something else or nothing, roll-free. The agency rung already counted them through `WouldPropose`.
- **On the record.** Every pact the player comes into under the rules writes the permanent 'alliance-formed' (+30) between them and each partner still in the house, both ways, as `NpcAlliances` writes it for houseguests. That covers a proposal, an invitation either way, joining a houseguest's pact, and a story's pact of two. Twenty weeks on, a partner trusts the player (60) exactly as a houseguest trusts their own pact-mate.
- **The words.** Under the rules `PlayerDeals.Reasoning` reads only what the player knows (W3's found-on-the-way item):
  - how the houseguest sees the player is the player's own read of it (`KnownOdds.PresumedView`);
  - 'Your track record concerns me' is the player's own broken deals and promises, never the houseguest's private record.

  A committed proposal that left no pact is answered with a no in the houseguest's own voice, without a reason (`HouseDialogue`).
- **The greeting.** Under the rules `HouseDialogue`'s relationship line - the end of every greeting, and every reply that falls through to it, a refused deal's among them - reads the player's own reading of the houseguest, inside the band a read, an account or an overheard word put them in (`KnownOdds.PresumedView`). Before, it read the houseguest's hidden view, and every conversation that opened said for free whether that was −15 or under, or 25 or over.
- **On screen.** Under the rules the 'Propose an alliance' row's pill reads 'binds you · ' and the player's read of the chance (`KnownOdds.Alliance`, the deal table's own estimate for the invitation). The 'Each chance is your own read' note sits above it, once a render, above the first chance shown. At three pacts the row is drawn locked under the reason, and so is an invitation's yes (`PactCapOfferLine`).

**Shown against rolled.** The roll reads two things the player cannot see:
- the houseguest's true view of the player;
- their private record of the player, as ledger trust (0.2 a point either side of 50).
The shown chance reads the player's own reading of them, held inside a current read's band, and leaves the trust term out (neutral). Every other term is the roll's own: the invitation's −10, the traits, and the player's own broken deals. Where the player knows every term the roll reads, the two are equal, and the proposal is decided by exactly that number against one draw (`AllianceProposalTests`).

A grudge of 40 or more refuses whatever either says. The player can know of one such grudge, because their own act wrote it: walking out of a pact leaves every other member holding 80 against them (`AllianceLeftGrudge`), fading 2 a week. While that reckoning is at 40 or more, every member of a pact the player walked out of reads 'no chance', nought, on the row and in the deal table (`KnownOdds.KnownWalkOut`, from the alliances page's own "You left it."). Any other grudge - a nomination, a broken word, a story's falling-out - is the houseguest's own and never shown. A walk-out's grudge that a story has since eased still reads as the player reckons it.

**The walk.** The audited walk's optional oath branch proposes to Maya at most once (PortVerification.Season). Under the rules a no spends the action, and asking again would spend the oath's budget on the roll. Before them a proposal at eight was never refused, so the walk plays as it did.

**Evidence.**
- `CommitmentRulesSeasonDigests` without the rules: the 54 recorded digests, unchanged.
- Under the rules 52 of the 54 move (51 before C4), and every command stays legal. The busy player's committed proposals rise from 8 to 56, because a no is now a committed command.
- The Unity-free subset rises from 1352 to 1364 (`AllianceProposalTests`, 12). Ten of the twelve fail on 50ebd57's simulation. Every name C4 added is read only through the file's helpers, so it compiles there with their bodies stubbed to 50ebd57's behaviour. The other two hold on both builds: the shown odds' V6 half, and the season without the rules.
- The screen's half is a PlayMode test, `AllianceProposal_UnderTheRulesTheRowShowsItsOddsAndThreePactsLockIt`, at both text sizes:
  - the row's pill is 'binds you · ' and `KnownOdds.Alliance`'s word, under exactly one odds note;
  - at three pacts the reason stands directly over a locked 'Propose an alliance' that commits nothing, and the deal table offers no invitation;
  - an invitation from the one the player talks to has its yes locked directly under `PactCapOfferLine`, committing nothing, and its no commits;
  - a batch run photographs 'conversation-alliance-odds' and 'conversation-alliance-cap', with '-large' and '-4x3' forms.

**After the review (fix-then-land, 2026-10-02):** the screen's PlayMode test; nobody invites a player who holds three; the walk-out's grudge in the shown chance; the greeting's knowledge gate; the refusal's own kind; 'alliance-formed' only with partners still in the house; the cap's wording ("the most you can keep at once": a houseguest may hold more); the constants where the literals stood (`AlreadyAlliedRefusal`, `AllianceGrudgeLine`, `AllianceLeftGrudge`); the walk's oath note; and the tests the review asked for - a story's pact of three at the cap, the deal table's invitation drawing its roll whatever the grudge, and a yes at three without the strategy windows.

**Where the build departs from the plan:**
- **The cap binds every way into a pact,** not only the proposal, or it would be no cap.
- **Not compiled offline in the slice.** The worktree's hook refused `powershell -File Tools/offline-compile.ps1`, twice, also as a plain command. The runtime, walk and PlayMode edits were checked against the signatures they call; the lead compiles them at landing.

**For the owner and the story session:**
- **One way, two controls (the owner's call).** The deal table's 'Propose an alliance invitation' stays, because captions are contracts. It asks the same question, on identical odds, with the same grudge and the same cap. Its payoffs differ as they always did: +12 on a yes or −4 on a no, a deal on the record, and it can bring the player into the houseguest's own pact. Retiring it, and whether a refused proposal should also cost −4, are the owner's to decide.
- **Story pacts (the story session's call).** A story's pact with the player keeps the story's own gate of eight and ignores the grudge rule (StoryEffects.cs:307). A story option whose pact meets the player's three plays without its pact. Locking such an option is the story session's to decide.

**Found on the way, for wave B and C:**
- The deal lines say "agreed a alliance invitation" and "turned down a alliance invitation", in every season's log.
- A refused deal is answered by the houseguest's relationship line, which can contradict it, as a refused alliance was before this slice; under the rules that line now says only what the player knows.
- `AllyThroughInvitation` forms no pact without the strategy windows, so there an accepted invitation is only a deal.

**Floors:** the Unity-free subset 1352 to 1364; EditMode +12 (the same file); PlayMode +1.

**C1, every deal does something and accepting commits you, built 2026-10-02** in its own worktree from `claude/nearby-render-gap` at 50ebd57, reviewed and fixed the same day (fix-then-land). A rule slice: everything is behind `commitmentRulesStartWeek` (`EpisodeEngine.CommitmentRulesOn`), with no saved field and no command kind.
- **An information deal passes one reading a week.** As campaigning closes and the house is about to vote, a partner of the player's who casts a ballot that week tells them where it is going (`EpisodeEngine.PassTheReadings`): the claim an answered "where's your head at" is, told to the player's face (`ClaimSource.Told`), on the ledger's claims, read by the whip count and judged at the reveal like every other, in one line between the two ("Alex kept you in the loop, as your information deal has it: they're voting to evict Maya."). The partner's word is as good as it is to anybody who asks (`EpisodeEngine.VoteHonesty`, AskVote's own: Loyal always, Sneaky one time in four, anybody else by how they see the player), drawn on a coin keyed to the week and the partner (`StoryRandom`, so the season's stream is untouched); a lie names the nominee they are not voting out, and nobody deflects, since the deal obliges an answer. A partner the reveal catches in a lie told to the player's face has broken the deal (`BreakTheDealsOfLyingPartners`): broken by them, its line to both, since the judged claim has already told the ballot. A partner with no ballot that week (the Head of Household, a nominee) has none to share. No memory, as with the asked claim. The week's read counts as taken.
- **A partnership is judged at every vote that tests it** (`DealResolution.PartnershipAtTheVote`): when one of the two is on the block and the other casts a ballot - with the house, or breaking a tie - voting the partner out breaks it, and voting the other nominee out keeps it, and a kept partnership goes on standing (`Verdict.stands`): no status, no settlement, a small kept record (`EpisodeEngine.PartnershipKept`, +4, both ways, moving no score), the memory of the one kept, and a line to the voter. So a standing partnership keeps its +20 in a Head of Household's reluctance (`StrategyRules.DealWeight`) and its pair value in the ballot. A vote about neither, or both, does not test it. The player is told how it was judged only once they know the ballot that judged it: B0's rule (`KnownBallots.DealOutcomeKnown`) reaches a partnership through `SettledByABallot` and `IsBallotKind`, and the record and the memory wait in `TellsAnUnknownBallot`; under the rules the alliances page, the jury house's knows list, the finalist card's kept count, the final argument's moments and the jury's receipts wait too. The ballot's warning (V1) names a partnership the player's own ballot would break.
- **A safety pact is kept, and rewarded, when the partner as Head of Household spares them.** Once the veto meeting has named any replacement (`DealResolution.Spares`), a safety pact of the Head of Household's whose partner is not on the block, and was not put up that week, is kept: the one spared thinks the better of them at the pact's weight (+8 × 2), on the record, in a line to both.
- **A final two deal is an obligation in the final Head of Household's choice** (`EpisodeEngine.FinalTwoTerms`): a term on the finalist it would take, 25 - the evaluator's decisive margin - at their view of that finalist of fifty or more, scaled to nothing at zero, Loyal ×1.5, Sneaky ×0, the shape of the levers' vote obligation. It was the web's deal term alone, about 4.5 points.
- **X4.** As the house turns from a weekly eviction to the social week - after the reveal's reconcile, and after the walk out has read a deal between the player and the evictee for its goodbye - and at the final eviction, whatever still binds the evictee ends (expired): a deal they are a party to, an offer they made or were made, a deal that names them, and every final two promise to or from them, writing, logging and drawing nothing (`EndWithTheEvictee`). The final choice passes over a final two deal or promise with anybody no longer in the house (the `Selects` verdict; `EpisodeEngine.FinalChoiceSettles`, which the decision screen's sweep reads too), so a season that took the rules on later breaks nothing either, and the final choice's page lists nothing broken either way. Your word's term for an open-ended deal that ended so says "until they left in week 5", as an oath's does, and the alliances page still says an ended invitation is how a pact began.
- **Decision 15.** Accepting an offer is +4, not +12 (`PlayerDeals.CommittedAcceptedImpact`), on the same two draws; a deal the player puts to somebody keeps its +12. An offer the player accepted under the rules weighs one step heavier when it breaks, whoever breaks it (`DealTrust.Heavier`, `DealResolution.BreachWeight`), in the settlement and in the jury's obligations; a veto ask is critical already, and stays so. The yes - and a proposal agreed - is a chance taken on the record at once, so a deal that lapses before any reveal is not an offer left on the table.
- **The double breach (R0/C0's note).** A vote deal both parties broke names neither (`DealResolution.VoteDeal(..., bothNameNobody)`), as a voting bloc that fell apart does: each holds it against the other alike, neither carries the story's word-broken grudge or the house's gossip, and every reader holds it against both (`Breaches.BrokenByBoth`; the vote's threat term through `WebVoteDeal.brokenByBoth`). Validation lets a vote deal name nobody.
- **A settlement a ballot decided never moves the player's own view** (`keepPlayersView` in `SettleDeals`): their trust and standing word would jump by a deal's weight (+12, −22.5, −30) while every line says "unresolved". The record and the memory stay, and their readers wait for the ballot. Nor does such a breach raise the story's "You Broke Your Word" for the player.
- **Evidence.** `EveryDealDoesSomethingTests` (24, Unity-free, X4's fixture first): the first build's 15 each failed on 50ebd57, and the review's 13 on that build (e88a18d), each run against that build's simulation from a copy of the file with later names swapped for stand-ins answering as it would. `CeremonyTruthTests.UnderTheCommitmentRulesTheDealtGoodbyeStillFires` (EditMode, Unity) holds the walk out's goodbye. The digest harness: without the rules the 54 seasons digest exactly as recorded; under them all 54 move (51 before C1), and the busy player reaches each rule - which the rules-on half asserts. Its counts are this build's alone: re-check them once C4 and C2+C3 land on the same boundary.

**After the review (fix-then-land, 2026-10-02):** information partners can lie, on the keyed coin, and a judged lie breaks the deal (2a); the player's own view is kept at a settlement a ballot decided (2b); the jury house's knows list waits for the ballot (2c); the weekly ending moved from the reveal to the turn of the week, so the walk out's Dealt goodbye fires again (4); a kept partnership stands, judged at every vote (the lead's ruling); the safety pact's spared partner must not have been put up that week; an ended invitation is still how a pact began; Your word's term for an ended open deal; a taken deal marked at the yes; and the tests the review asked for - nothing logged or drawn by an ending, a tie-break's partnership, a veto ask accepted, the double breach with the story on, the threat term through a ballot's own value.

**Where the build departs from the plan:**
- **"One reading a week"** is a reading of the partner's vote, the claim the read is made of, told as the house votes: at the campaign's open a projection would be judged a lie whenever the campaign - the player's own lobbying included - changed the partner's mind. A week the partner casts no ballot has no reading.
- **A lie to the player's face** breaks an information deal whether it was the deal's reading or an answer to the player's own question that week: the partner's word was the deal.
- **"Accepting warmth"** is the answer to an offer; a proposal the houseguest accepts costs an action and a roll, and keeps its +12. The heavier breach is an offer put no earlier than the rules' first week, so the yes was given under them.
- **Your word's "binds"** says what is judged under the rules: a partnership "not to vote each other out", an information deal "to tell you where their vote is going, each week they vote".

**Found on the way:** under the rules a juror's standing deal with a finalist ends as they leave, so the jury's +10 for a standing deal (`WebJuryVoting.Obligations`) no longer reaches a juror, while C3 keeps 25 for a shared pact that ended with a member leaving - an asymmetry to settle with C3; in seasons without the rules the finalist card's kept count, the final argument's moments, the jury's receipts, the jury house's knows list and the alliances page read a kept vote deal without asking whether the player knows the ballot (B0's rules-off half), and an accepted deal that lapses before a reveal sees it taken still reads as an offer left on the table; a story deal is not marked taken at once; the offline compile could not be run from this slice's worktree (the worktree's hook refuses `powershell` there).

**Floors:** the Unity-free subset 1352 to 1376 (`EveryDealDoesSomethingTests`, 24); EditMode adds the same 24 and the walk out's goodbye (25); PlayMode unchanged.

**C2, betrayal as an event, and C3, two-sided alliances, built 2026-10-02** in one worktree from `claude/nearby-render-gap` at 50ebd57. No saved field and no command kind: everything is under `commitmentRulesStartWeek` (schema 22), and a season without the commitment rules plays as it did. The readers are `Allegiance` (new, Simulation); the engine's writers are `EpisodeEngine.Betrayal.cs`.

**C2.**
- **The triggers.** An NPC who shares a standing pact with the player turns on it when they nominate the player (`Nominate`), name them the replacement (`ResolveVeto`) or break a veto commitment with them (`ResolveVeto`'s verdicts) - or, at the reveal (`BallotBetrayals`), when they vote to evict the player, refused the player's call that week and then voted to keep its target, or break a vote deal with the player by their ballot. A refused call alone is no betrayal: the refusal is how the player learns of a cooling ally (C3), and the ballot keeps or breaks the call. A deal broken by a nomination is the nomination's betrayal; a final-two deal broken at the final eviction, and a Head of Household's deciding vote against the player, evict the player, so neither writes anything; nothing is written once the player is out of the house, or between two who share no standing pact. Once a week per ally: the first act that week is the one on the record.
- **What it writes.** The dormant `alliance-betrayed` entry, one way, on the player's own record of the betrayer, at the reference's −50 and permanent: "Riley nominated you despite The Riley Pact." A line of kind `alliance-betrayal`: to the two of them with the offer ("... You can cut ties with Riley this week at no cost, or stay allied."), or, when a ballot tells it and the player cannot place that ballot as it is written (`KnownBallots.Knows`), to the betrayer alone. Nobody's view moves: the player's view of the betrayer is theirs, and moving it under the sour line would end the pact they may keep. A pact that ends with a betrayal standing against it ends `/betrayed` in the ledger (`AllianceEnding`, after `left-house`).
- **What it changes.** The betrayer's protection terms stop counting (`Allegiance.Holds`): the shield (`StrategyRules.NominationReluctance`), the veto pull (`VetoWillingness`), the vote (`WebVoteAlliance.lapsedIds`, which the eviction evaluator's `AllianceLoyalty` reads, native and empty on an imported round), following a call (no roll: they defect), the bloc (`WebVotingBlocs.FromNative` leaves them out of the player's pact), a plea's +15 (`StrategyRules.Chance`) and the deal table's alliance bonus (`PlayerDeals.AcceptanceChance` and its reasoning). It holds for every pact the two shared that the player joined no later than the betrayal's week (`Allegiance.StartWeek`, which dates a pact as the alliances page does, `FinalistRead.PlayerAlliedSince`); a pact begun in a later week is a fresh commitment, and restores the betrayer's terms in that pact only - never in the one they turned on, while it stands beside it (`Allegiance.LapsedMembers`). The ledger's entry acts at once, hidden or not, because it is the player's own record: the player's trust of the betrayer falls under 35 and the reputation threat the player reads in them rises (`ThreatAssessment`), which an NPC Head of Household in a pact with the player weighs among their pact-mates' readings under agency (`ThreatTerm`). So a ballot the player cannot see can still move an allied Head of Household's nomination, through the player's own distrust.
- **The player's choice.** The existing `LeaveAlliance` command, aimed at the betrayer the week of a betrayal the player can know (`Allegiance.FreeExit`), cuts ties at no cost, said where a leave is said - free time, the campaign, or a window to whoever it lets the player talk to (`RequireConversationWindow`, Social's own gate): no action is spent, nobody's view moves (so no roll), and nobody holds a grudge, the betrayer included - the web's 80 from every member is not written. A pact of two ends: "You cut ties with Riley Chen after they turned on The Riley Pact. It is over, and nobody holds it against you.", and the alliances page says "You cut ties after it was betrayed." A pact of three or more goes on without the betrayer, cut out of its members, the player and the loyal members keeping it: "... Riley is out of it, the rest of you keep it, and nobody holds it against you." Everyone in each pact hears its line; the betrayer answers in one of six lines of their own. Outside that week, or with anybody who did not turn on it, the leave is what it always was (−15, the 80 grudges, an action). The conversation's "Leave our alliance" keeps its caption; its pill reads "free · no grudge", or "free · cuts them out" where a pact of three or more goes on, while the way out is open.

**C3.**
- **The NPC's own commitment drives their terms.** A partner's shield, veto pull, alliance term in their ballot, jury term and following a call hold only while their own commitment does (`Allegiance.Lapsed`): they have not turned on the pact, and their own view of the player is not below `QuietLine`, the reference's hostility line (−10, under which a member will not have the player in their alliance). A member who has lapsed ignores the player's call with no roll drawn; the refusal is a betrayal only if their ballot then goes against the call (C2).
- **A partner who has turned ends the pact from their side.** `NpcAlliances.Dissolve` reads a partner's own view of the player under the rules: below the sour line (−20) the pact ends as the week opens, told in the words every ending uses ("Your alliance with Riley has fallen apart."), and the ledger says `turned`.
- **Evictees leave pacts of three or more (X5).** `EpisodeState.Allied` needs both in the house under the rules, so somebody gone holds no pact's terms; the pact's size in threat (`ThreatAssessment`) and in the vote counts only the members still in it (`Allegiance.Counted`); a juror reads 25 for a pact they shared and 0 for one they turned on the player in (`Allegiance.JuryLoyalty`). The pact's own members list keeps them, as the alliances page's departed and the finale's receipts read it, and the notes say "You were both in X · Riley left the house" ("you left the house" once the player has, "you both left the house"). A juror's "We were in this together" follows the finale questions' rule (`Allegiance.SharedToTheEnd`): a pact that still stands, or that ended only because the juror left, less one they turned on the player in. A pact ends with a departure (`left-house`) once fewer than two of it are left; `Dissolve` and the ledger's reasons read only the members still in the house.

**What the player sees, and when.**
- A nomination, a replacement and a broken veto commitment are public to the player: the line arrives with the act, the notes carry it ("Riley nominated you despite The Riley Pact · you can cut ties this week at no cost" while the week lasts), the relationship web's history holds it, and the free exit is open at once.
- A ballot betrayal - a vote against the player, a refused call voted against, a vote deal broken by a ballot - is the NPC's own act and the engine flags the pact on it, but the player sees nothing of it while the ballot is not theirs to know (`KnownBallots.TellsAnUnknownBallot` recognises the record's words): no line, no note, no history row, no free exit, no pill, and no vote read or lever line moves, since both project the season as the player knows it (`Allegiance.AsThePlayerKnows`, a copy without the hidden entries; asking a voter and the vote itself keep the true state). Where the reveal itself places the ballot - a claim it judges, the count - the line reaches the player at once with the offer. Learned later that week, the notes say it with the offer and the way out opens; learned later still, the notes say it and the week is gone. The weekly recap's movements leave every betrayal entry out, known or not: it moves no view, so summing its −50 would say a feeling moved that never did.
- A cooling ally shows as a refused call, a read, and a "gone quiet" line in the notes ("Riley has gone quiet on The Riley Pact"), never a number - and only in a week the player can tell where the ally stands (`Allegiance.CommitmentKnown`): a read of them this week, a call of the player's they refused this week, contact this week (the player's own record of them touched), or a betrayal of theirs the player can know. The same rule opens an ally's alliance and bloc terms to the vote read (`VoteRead.CommitmentHidden`), so the notes and the read always agree, and two allies the player has had nothing from this week, one committed and one cold, read alike. The shown odds ask it the same way (`Allegiance.HoldsAsKnown`): the deal table's and a plea's alliance bonus fall for a betrayal the player can know or a cold ally they can tell, never for a hidden ballot or a view they cannot see.
- Game Sense notes a pact that ended in a betrayal the player can know ("Week 3: your alliance with Riley ended in a betrayal.", −6, shown in the weekly recap); one the player cannot know reads as an ending.

**The two terms C0 left** are held against whoever broke the deal, under the rules: the strategy windows' reluctance (`StrategyRules.NominationReluctance`'s −35 now goes through `Breaches.CountsAgainst`) and the eviction vote's deal obligation (`WebEvictionVoting.DealObligation`'s −35 now goes through `HeldAgainst`, the threat term's own test, so an imported round and every season without the rules read both sides as the web does). The web's own promise term holds a broken promise only against the one who broke it; deals had no breaker field to do the same. A voter or Head of Household's own breach is no grievance of theirs against the one they wronged.

**Evidence.** `CommitmentRulesSeasonDigests`: the 54 seasons without the rules digest byte for byte as 7991687 played them; under the rules all stay legal and 52 move (51 after R0/C0). The harness now counts betrayals and pact endings by their words, so it still compiles against the build before the rules: two betrayals and four player pacts that turned across the 54 (its busy player proposes only eight pacts), the same after the review. A scratch sweep (not committed) of 75 director-rule seasons in houses of 6, 8 and 12, with a player who allies, calls the vote and cuts ties half the time where a leave can be said, finished every season valid. Before the review it counted 99 betrayals (92 of them refused calls) and 136 free exits; after it, 44 betrayals - 31 refused calls voted against at the reveal (of 108 refusals), 8 nominations, 4 replacements, 1 vote against the player - and 19 free exits, each in a pact of two (its player only ever proposes pairs). `BetrayalAndAllegianceTests` (23, Unity-free): each trigger with and without the rules, a refusal its ballot kept, a deal broken by a ballot not cast against the player, the no-ops (no shared pact; a player the house evicts), the ballot's knowledge gate and the offer when the reveal already places the ballot, every protection term, a fresh pact beside a betrayed one, the free exit in a pact of two and of three, its window and its week, the leave outside a betrayal unchanged, the vote read and the shown odds against a hidden ballot, the turned partner (`/turned`), gone quiet and its signals, the call without a roll, the read's knowledge gate, X5 (the player's leaving too) and the two C0 terms. Against 50ebd57's engine with the new readers added, 21 of the 23 fail; the other two pin what must not change (the leave outside a betrayal, the leave after the week). Sixteen mutations of the review's fixes in a scratch copy - the read on the true state, every ballot betrayal public or hidden, gone quiet without a signal, the cut-out, a refused call flagged whatever the ballot, the shown odds and the rolls reading the old terms, the fresh pact, the window, the notes, Game Sense, the betrayer's reply and the alliances page's line - each fail at least one of them.

**The audited walk** needs nothing: it presses no leave, no call and no veto deal, and its bloc checks read the coordination from the same state the engine does. Its optional Maya pact can now be betrayed, which adds lines of a kind none of its checks count.

**After the review (fix-then-land, 2026-10-02):** all under the rules, so the 54 seasons without them still digest byte for byte. The vote read and the levers' lines told a ballot betrayal the player could not see once they had read the betrayer (M1): both now project the season as the player knows it. A refused call was a betrayal at the call, so calling a cold ally opened a free exit on demand (M2): a refusal is flagged only at the reveal, where its ballot went against the call, and is gated like any ballot. The free exit ended a pact of three whole (m8): it cuts the betrayer out. And the minors: a ballot the reveal already places is told with the offer (m1); "gone quiet" and the vote read wait on the same signal this week (m2); Game Sense's `/betrayed` note (m3); the juror's "in it together" (m4); a fresh pact restores nothing in the betrayed one (m5); a pact's start is the week the player joined it (m6); the free exit waits for a conversation window (m7); the notes say when the player left (m9); the recap's movements leave betrayals out (m10); the trust and threat sentence above, which credited the player's other pact-mates where it is an allied Head of Household weighing the player's own distrust (m11); the alliances page matches the free exit's exact line (m12); and the plea's and the deal table's alliance bonus ask whether the ally's word holds, the shown odds as the player knows it (the spec).

**Where the build departs from the plan:**
- **The free exit** costs no action either ("at no cost", decision 11). It ends a pact of two and cuts the betrayer out of a bigger one, the player and the loyal members keeping it; C5's "Leave {pact}" changes the leave for the player, not the betrayal exit.
- **"Ignores their call"** is a betrayal only when the ballot follows the refusal: the refusal alone is C3's news of a cooling ally, and a ballot cast the call's way keeps it.
- **X5** is a departure from pacts, not from their records: the members list keeps the departed, and every reader of membership that matters reads who is still in the house. A betrayer cut out by the free exit leaves the list: they did not depart, they were cut.
- **A pact re-formed with the betrayer in the betrayal's own week** counts as betrayed (its start is not after the betrayal): the protection comes back only with a pact made in a later week, and only in that pact.
- **Promises are not a trigger until B1:** an ally's broken promise is C0's breach, not a betrayal of the pact; B1's one commitments model is where a broken promise can become one.
- **NPC-to-NPC betrayals are not flagged:** only the player's pacts are read, as the plan's triggers are all acts against the player. The finale is asymmetric for it: a juror who turned on the player in a pact reads 0 for it at the jury vote and gives no "in it together", while a juror who did the same to an NPC finalist still reads 25 for that pact (`Allegiance.JuryLoyalty`, `SharedToTheEnd`).
- **Reads (ReadPerson)** are unchanged; they already say "cold on you", and under the rules a read this week is one of the ways an ally's commitment opens to the vote read and the notes.

**Found on the way, for later slices:** a pact the player cuts and re-forms in the same week can be cut again for free; the betrayal's line is logged only when the act happens, so a ballot the player learns of later reaches the notes and never the log; a member whose commitment lapses by a view the player cannot see still leaves the player's bloc (`Allegiance.Following`), and the bloc round's seeded draws move with who is in it, so another voter's projected ballot - and the vote read built on it - can shift by a view the player cannot see: a third-order channel the read's projection does not close, since it removes hidden betrayals, not hidden views; while a hidden ballot betrayal stays on the player's record the vote read copies the state for each read (`AsThePlayerKnows`), which the HUD's boards pay on every rebuild; the busy player in the digest harness reaches C2 only twice, so a harness that allies and calls would cover it better (the scratch sweep above is that harness's shape).

**Tell the story session:** an ally who nominates the player, names them the replacement or breaks a veto commitment with them - or, at the reveal, votes against them, refused their call and voted against it, or broke a vote deal with them by their ballot - writes `alliance-betrayed` on the player's record and a line of kind `alliance-betrayal`; the free exit ends a pact of two and removes the betrayer from a bigger pact's members; a partner whose view of the player falls below −20 ends the pact; somebody who leaves the house is no longer `Allied` with anybody under the rules, though they stay in the pact's members list.

**Floors:** the Unity-free subset 1352 to 1375 (`BetrayalAndAllegianceTests`, 23); EditMode adds the same 23.

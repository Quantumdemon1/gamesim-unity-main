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

**C6, allies share intel, built 2026-10-02** in its own worktree from `claude/nearby-render-gap` at 1597bc0. A rule slice: everything is behind `commitmentRulesStartWeek` (`EpisodeEngine.CommitmentRulesOn`), with no saved field and no command kind. The engine's half is `EpisodeEngine.AllyIntel.cs`.
- **Who is an ally** (`EpisodeEngine.SharesIntel`): a houseguest in a pact with the player that holds from their own side as far as the player can know - `Allegiance.Holds` on the season as the player knows it (`AsThePlayerKnows`), without the copy: both in the house, no betrayal of theirs the player can know standing against it (`Allegiance.KnownBetrayed`), their own view of the player not under `QuietLine`. A betrayal told only by a ballot the player cannot place changes nothing here: the betrayer goes on answering as an ally, with their true ballot, or a deflection - or their silence at a meeting - would tell the player of a ballot they were never told. A member gone cold, or whose betrayal the player knows of, answers as anybody does.
- **"Where's your head at?"** An ally never deflects and answers with their ballot as it stands (`ProjectBallot`): a `Told` claim, the week's ask, the line an answer always was. Nothing is drawn for it - the answer is decided, as a deflection never drew either - so under the rules asking an ally takes no draw from the season's stream; a stranger's question is unchanged, draw for draw. A member who will not say has told the player where they stand: a deflection from somebody in a pact with the player is one of C3's signals (`Allegiance.CommitmentKnown`, beside a read, a refused call and contact), and the notes say which it was - "gone quiet" for a member gone cold, the betrayal for one who turned, which the player already knows. C3's refused call still reads the true state (`Allegiance.Lapsed`): following a call is a roll (`WebVotingBlocs.Complies`), so a refusal tells nothing for certain.
- **With an information deal (C1).** By C1's own rule - the partner's word is as good as it is to anybody who asks - an ally's reading is the truth too (`AnswerHonesty`, which `PassTheReadings` now draws on). The deal's keyed coin is still the one drawn, a partner who is no ally keeps C1's odds, and C1's breach by a judged lie stands: an ally who is also a partner tells the truth both ways, and the reading and an earlier answer can disagree only where the ballot moved between them.
- **The meeting.** Under the rules `AllianceMeet` (the command behind "Go over the plan in private") is a meeting of a pact the player is in, held through whichever member the player is talking to (`HoldAllianceMeeting`).
  - **Where:** any private room (`EpisodeEngine.PrivateRooms`): the bedroom, the backyard, the Head of Household's suite and the game room - the rooms whose act is between two people with no audience. Not the living room or the kitchen, the open rooms whose acts are watched by everybody in either (Stand up for them's witnesses, Cook for everyone here's table), nor the nomination room, the ceremonies', nor the private room, the diary room's, a confessional for one. The engine never knows the room: the house offers the row only there, as it offers every room act.
  - **Who:** every member of the pact still in the house (`AtTheMeeting`). Holding a meeting calls the pact together, as the house's own meetings do (`NpcSocialActions`' alliance upkeep, the web's holdAllianceMeeting, whose +3 between every pair this is); nobody gone is at it (X5).
  - **Warmth:** +3 (`AllianceMeetingWarmth`, `NpcSocialActions.AllianceMeetingImpact`) for every pair at it: the player's own through `Change`, a roll each, as any conversation of theirs; the houseguests' through `RelationshipLedger.Move` and `Record` ("The Riley Pact met in week 3"), no roll and no arc - arcs belong to the player. The record fades as the house's own `alliance-meeting` record does, under a type of its own (`pact-meeting`, `EpisodeEngine.PactMeetingType`): the Spy Screen casts from the house's private meetings, and must never show the player two of their own pact-mates with their heads together. Each member remembers it, without a name in it: the vote's memory term matches memories to nominees by name.
  - **The vote:** in a vote week, while two nominees stand (`VoteRead.Available`), one member says where their vote is going - one `ClaimSource.Ally` claim, their ballot as it stands after the meeting's warmth, no roll and no coin. Who (`MeetingSpeaker`): of the members at it who vote and answer as allies, the first whose vote the player has not heard this week (no claim of any kind from them yet), the one the meeting is held through first and then the pact's own order; where every one has been heard, the first again. A member gone cold, or whose betrayal the player knows of, says nothing; with no ally at it who votes, no vote is told and the meeting is the warmth alone, as it is outside a vote week. The claim is judged at the reveal by the existing judging (kept or lied): the whip count reads it as an ally's word while it is open, and `KnownBallots` places the ballot by it once judged, as "an ally's account" (`Basis.Reported`), the source's existing reading. An ally's account the ballot went against costs nothing and breaks no deal: only a lie told to the player's face does. Every page says it as the member's own word to the pact - "Told the pact they'd vote out Maya Hassan" in the notes, "Riley Chen told the pact: evict Maya Hassan" in Your week, "Told the pact: evict Maya Hassan" on the whip count's card, "Told the pact they'd evict Maya Hassan; voted the other way" on the season's tapes - and a ballot that went against it as a vote that changed ("voted the other way"), never "a lie". The privacy sentinels excuse "Told the pact" as they excused the old words.
  - **Once a week per pact:** a story cooldown keyed to the week and the pact (`MeetingKey`, `MetThisWeek`), the room acts' own mechanism (the suite's invitations), so no saved field. A second meeting that week is refused ("The Riley Pact has already met this week."); another pact may meet.
  - **Which pact:** the one the command names by its id (`text`, as a call names its pact); with none named, the oldest standing pact the player shares with that member that has not met this week (`MeetingPact`), which is also the one the house offers.
  - **The line:** "The Riley Pact met where nobody listens: you, Riley Chen and Sam Ortiz went over the plan." and, with a vote, " Riley Chen told the pact they're voting to evict Maya Hassan." Of kind `conversation`, to everybody at it; never a number. It is one of the window's actions, as every room act is.
- **On screen.** Under the rules the conversation's BOND group offers "Hold an alliance meeting" (`EpisodeDirector.AllianceMeetingCaption`, new and unique, shown only under the rules) in any private room, with somebody in a pact of the player's that has not met this week: one row, for the oldest such pact, the next one offered under the same caption once it has met. Its pill is "warmth", and "warmth · learn" in a vote week when somebody at it casts a ballot - what the player can see; whether they say is theirs (`AllianceMeetingTag`). "Go over the plan in private" keeps its caption and is the backyard's word with one ally in every season without the rules; under them it is not offered beside the meeting, which is the same command.
- **The audited walk** needs nothing: it presses no room act, and its read step's question still writes a `vote-read` line, ally or not.

**Evidence.**
- `CommitmentRulesSeasonDigests`: without the rules the 54 seasons digest byte for byte as recorded; under them all 54 move and every command stays legal. The busy player now holds a meeting now and then in the rules' half only, so the recorded half plays its own commands: across the 54, 21 meetings, 16 questions put to somebody who answers as an ally (`SharesIntel`, read by reflection so the file still compiles against the build before the rules), every one answered with their ballot as it stood, and 8 ally claims, all 8 kept at the reveal. The rules' half asserts each.
- `AlliesShareIntelTests` (17, Unity-free): an ally's answer is their ballot and never a deflection (a Strategic ally under 25, who deflects without the rules), with no draw; a Sneaky ally's word is the truth where it lied to a stranger's question; a member gone cold, one who turned on the pact and a stranger each answer as anybody does; a betrayal the player cannot know changes nothing in who answers (a Strategic member at 10 who voted against the player last week, in a ballot the player cannot place, answers straight and speaks at a meeting, while one whose nomination the player saw keeps quiet at both); a stranger's answer and deflection are the same with the rules as without, draw for draw; a deflection tells the player where an ally stands (gone quiet, the read's terms); an information partner who is an ally tells the truth in the reading on a coin a partner alone lies on; the private rooms; +3 for every pair (the members' own exactly, on the record under the meeting's own fading type, no arc), a memory each, one line; every member in the house at it and nobody gone; one ally claim from the one the meeting is held through, equal to their ballot, read by the whip count and placed by no ballot before the reveal; the vote of an ally not yet heard; nothing from a member who is no ally, and the warmth alone with none; the claim judged kept and lied at the reveal with nothing costed, and said on the notes and in Your week as the member's word to the pact, never a lie; once a week per pact; the meeting held through somebody in the pact it names; and the backyard's word as it was without the rules. 15 of the 17 fail on 1597bc0's simulation, compiled with stubs for the new names answering as that build does (the meeting only in the backyard, nobody answering as an ally); the other two pin what must not change. On the slice's first commit (04c5e78) the review's three engine findings each fail one: the hidden betrayal, the diary room and the record's type.
- PlayMode, not run in the slice: `AllianceMeeting_UnderTheRulesAPactMeetsInAnyPrivateRoomOnceAWeek` - no meeting in the living room; in the backyard the meeting and not the old row beside it; in the bedroom the row, its pill, the conversation's fit and tags at both text sizes, pressed - found again by its caption after the capture - as one command with the pact's line and the ally's claim, then gone for the week; without the rules no meeting in the bedroom and the old row in the backyard. A batch run photographs 'conversation-alliance-meeting', with '-large' and '-4x3' forms. The conversation fit tests count the new caption among the room's acts, and the privacy sentinels excuse "Told the pact".

**Where the build departs from the plan:**
- **"Present"** is every member still in the house: holding the meeting calls the pact together, as every alliance meeting in the house does, rather than the members the house happens to find in the room.
- **"Hold an alliance meeting"** is the meeting's control under the rules, and "Go over the plan in private" is not offered beside it there: both commit the one command.
- **Who answers as an ally follows what the player can know:** a betrayal told only by a ballot the player cannot place leaves the betrayer answering as an ally, with their true ballot.
- **A deflection is a C3 signal** (`Allegiance.CommitmentKnown`): under the rules an ally never deflects, so one who does has said where they stand, and the notes say which - gone quiet, or the betrayal the player knows.

**Found on the way, for later slices:** the notes still call an overheard claim the ballot went against "a lie", where the season's tapes call it a vote that changed (outside the rules too, so it waits for a copy pass that may move recorded seasons' digests); story cooldowns are capped at 256, and a full list would let a pact meet twice in a week, as it would let the Head of Household invite a third houseguest up; the runtime and PlayMode edits were not compiled offline in the slice, as briefed - they were checked against the signatures they call, and the lead compiles them at landing.

**After the review (fix-then-land, 2026-10-02):** all under the rules, so the 54 seasons without them still digest byte for byte. Who answers as an ally follows what the player can know (M1): a betrayal told only by a ballot the player cannot place made the betrayer deflect, or fall silent at a meeting, while the notes said nothing - a betrayal told for free. An ally's claim is the member's own word to the pact on every page that prints one, and a ballot that went against it "voted the other way", never "a lie" (m1), with the privacy sentinels' excused prefix moved to "Told the pact"; AllianceRead's comment says an ally's claim is evidence of no other pact (m2); the private room, the diary room's, is no meeting room (m3); the meeting's record between houseguests has a type of its own (m4: the Spy Screen, which casts from the house's `alliance-meeting`, is that type's only other reader); the PlayMode test presses a control found again by its caption (m6); and the harness counts questions put to those who answer as allies, and checks each answer is their ballot (the test gap). The cooldowns' cap stays as noted (m5).

**Tell the story session:** under the commitment rules `AllianceMeet` is a meeting of the whole pact - every member still in the house at it, +3 for every pair, a fading `pact-meeting` record between houseguests (never the house's `alliance-meeting`, which the Spy Screen casts from), a memory for each member and a story cooldown `meeting:{week}:{pactId}` - and it writes `ClaimSource.Ally` claims in a vote week, which every page words as the member's own word to the pact ("Told the pact ..."); an ally answers "where's your head at" honestly and never deflects, and a betrayal the player cannot know of does not stop them.

**Floors:** the Unity-free subset 1411 to 1428 (`AlliesShareIntelTests`, 17); EditMode adds the same 17; PlayMode adds 1.

**C5, grow and manage alliances, built 2026-10-02** in its own worktree from `claude/nearby-render-gap` at 1597bc0. Everything is under `commitmentRulesStartWeek` (`EpisodeEngine.CommitmentRulesOn`), with no saved field. **Two new command kinds**, appended after `LockFinalArgument` (the story session must be told; at landing C5's go before C7's):
- `BringIntoAlliance` = **59**: `targetId` the houseguest asked, `secondTargetId` the pact's id. A social action.
- `RenameAlliance` = **60**: `targetId` the member it is said to, `secondTargetId` the pact's id, `text` one of the names on offer. Free.

Without the rules both are refused before anything is spent, drawn or logged (`CommitmentKindRefusal`), a leave is what it always was, and no bloc has a founder.

- **"Bring {name} into {pact}"** (`EpisodeEngine.GrowPacts.cs`). What the player can see is refused for nothing: a pact that is not theirs; somebody already in it; somebody already in a pact with the player, in C4's words (`AlreadyAlliedRefusal`), because a bring-in never stacks pacts; somebody the player has soured on, their own view under −20 ("You can't vouch for Maya Hassan."); and a pact already holding `LargestPact` (four) in the house - the largest any pact is made with (a story's, two to four), the departed (X5) not counting. Then one draw from the season's stream, always: the one asked answers exactly as a proposal is answered (C4) - `AllianceChance` against the draw, C4's grudge of forty refusing whatever it says - and says no too when they already carry three pacts (`NpcAlliances.MaximumEach`), which is theirs to answer for; the row shows `KnownOdds.Alliance`, the player's read of them. Only on their yes is every member in the house asked, with no roll, through `NpcAlliances.WouldWelcome`: the web's own bar for a pact of more than two (`AllianceManager.tsx`, the lead's ruling) - no member under the hostility line, −10, toward the newcomer, and no grudge of forty either way. One no is enough. A yes appends the newcomer, so `members[0]`, the founder, never changes; brings the proposal's +8 with its reciprocal draw (a pact with the player); writes the ledger's permanent 'alliance-formed' between the newcomer and every member in the house, both ways (the player's through C4's record, between houseguests through the ledger alone, never `Change`); makes them a knower of the pact's fact; and is told to everyone in it ("Maya Hassan joined The Riley Pact."). A no spends the action, moves nobody, and says who said it, to the player's face, under `alliance-refused`: the one asked in their own words, C4's reason from what the player knows ("Maya Hassan turned down The Riley Pact. “…”"), the members never asked; or, after their yes, the members who would not have them ("Riley Chen won't have Maya Hassan in The Riley Pact.").
- **"Rename {pact}"**. The founder's alone (`PlayerFounded`: first in it, and the record agrees - a pact the player made, or a story's around them), said to a member in the house, from `PactNames.For`: the house's own name for its people now ("The Riley and Jo Pact", the format the code names pacts in), then the web build's names ("The Outsiders", "Dream Team", "Power Players", "The Silent Circle", "The Hidden Council", "The Golden Crew") - less every name the player knows a pact by: its own, one any other pact of theirs has or had, one of a pact they walked out of that goes on without them (by their own line while the log holds it), and one of a pact of others they know of. **Free** (decision from 1.2: a name changes nothing anybody weighs - no view, no roll, no record - so an action spent on it would be a tax with nothing bought) and **once a pact a week**, so the log is never a list of names, kept as the story cooldown `rename:{week}:{pactId}` (C6 keys its meeting the same way), so a log that has rolled past the line still knows. Its line has its own kind, `alliance-renamed` ("The Riley Pact is The Outsiders now."), so it is never read as a pact formed or ended.
- **"Leave {pact}"**. `LeaveAlliance` names its pact in `secondTargetId`; an older command naming none leaves the first the two share, as it always did. In a pact of three or more in the house only the player goes and the others keep it (`TakeOutOfPact`, now shared with C2's cut), at the defector's price, unchanged: −15 with the one told, through the same roll, their memory of it, the eighty from everyone left behind, and the action. "You left the alliance with Riley Chen and Jo Park: The Riley Pact goes on without you." goes to everyone in it, and the one told answers that the pact goes on without the player, never that it is over. A pact of two ends line for line as it always did. The week an ally turned on the pact, C2's free exit is unchanged.
- **A member who turns on a pact of three or more is taken out of it** (the lead's ruling on groups). At the week's turn (`NpcAlliances.Dissolve`) a member of the player's pact whose own view of the player is under −20 leaves it, as a leave takes the player out and C2's cut takes a betrayer out, and the pact goes on; the rest of it, and the one who turned, hear it in the story's words for a defector ("Riley Chen is out of The Trio."). A pact of two ends from their side, as C3 made it, with no line; the player's own souring on anybody in it still ends the whole pact.
- **No two of the player's standing pacts share a name** (`PactNames.Unique`): a new pact of theirs - C4's proposal, an invitation's pair, a story's pact around them - that would take the name of one they stand in takes the next numeral, so after C2's cut keeps "The Riley Pact" going without Riley a fresh pact with Riley is "The Riley Pact II". An ended pact's name is free again, as it always was. Captions that name pacts stay unique.
- **The founder calls the bloc.** Under the rules `WebVotingBlocs.FromNative` gives each pact its first member as `founderId`, and the source's round makes them the caller whenever they are among the members who vote; otherwise it picks as before. The player calls the vote of a pact they made (the source's rule, which the port had left out).
- **The screens** (`EpisodeDirector.GrowPacts.cs`), in the conversation's BARGAIN, under the rules only: "Bring {name} into {pact}" for each of the player's pacts the houseguest is not in - none for an ally of the player's - tagged "binds you · " and the read, under the one odds note, or locked under the reason at four, and every row locked under one line, said once over the first, for somebody the player has soured on; with two pacts between the two of them a row a pact, "Leave The Pair" (one pact: "Leave our alliance", the caption it always had, naming it), the pill "costs warmth · it goes on" for a pact of three or more; "Rename {pact}…" for a pact the player founded, opening "Rename {pact} to {name}" rows under it as the conversation's pickers do, pill "free", locked under the reason once used that week. No caption changed. The read knows the grudge a leave from three left (`KnownOdds.KnownWalkOut`, from the player's own line, so the read and C4's answer agree); the alliances page says whom the player brought in and when ("You brought Maya in.", and "You formed it with" only the founding partners) and shows a pact the player walked out of among those they know of, by their own line; the one asked answers as a pact's yes, a proposal's no or a member's no, and the one told answers a leave from three; the weekly recap counts a join as "Alliances growing", never as an alliance formed.

**Evidence.**
- The Unity-free subset: 1411 to 1434 (`GrowAndManageAlliancesTests`, 23). Every C5 name is read through the file's helpers. In the first build, compiled against 1597bc0's simulation with those helpers stubbed to its behaviour, 14 of its 17 tests failed. The other three: the ordinals' pin; a pact of two left as it always was; and the read after a leave from three, which holds there because a leave then ended the pact whole - dropping the read's C5 half fails it. Sixteen mutations of the slice (the read, the members' say, the newcomer's place, the leave's keep, the week's limit, the founder, consent, founder-only, the page, the reply, the draw, the pact named, the cap, the presets, the refusal's names, the record) each fail at least one test.
- `CommitmentRulesSeasonDigests`: without the rules the 54 seasons digest byte for byte as recorded. Under them all 54 move and stay legal. Under the rules only, its busy player now asks somebody into a pact, renames one, and leaves a pact of three. It asks only somebody it can see may be asked - in no pact with the player, and not somebody it has soured on - and somebody every member would welcome where there is anybody. Across the 54 seasons: 43 askings (35 refused by the one asked, 8 joined, none refused by a member after a yes), 13 renames, and 5 pacts of three left going on. The seasons' last 256 lines hold one "is out of" line, from a turned member or a story's defector. The second half asserts the askings, a join and a rename.
- EditMode, Unity-only: `WeeklyRecapPrivacyTests.AJoinIsCountedAsAJoinNotAFormation`, the recap's join.
- PlayMode, `EpisodePlayModeTests.GrowAndManageAlliances` (6, for the lead to run): the bring-in row's pill and note and its press against the engine's own command, its locked form at four; the leave rows a pact and the pill, the trio going on, then "Leave our alliance" ending the pair; the rename picker, its press, and its locked row; an ally offered no bring-in row, and every row locked under one line for somebody the player has soured on; the longest captions - a pact of four named for the three with the longest first names, its names open to "The Hidden Council", both leave rows, and the houseguest with the longest name asked into it - fitting at both text sizes; and nothing new without the rules. A batch run photographs 'conversation-pact-bring-in', 'conversation-pact-leave', 'conversation-pact-rename', 'conversation-pact-longest' and 'conversation-pact-longest-ask', with '-large' and '-4x3' forms.

**The walk** needs nothing: it presses no leave, bring-in or rename, and its bloc checks read the coordination from the same `FromNative` the engine does. A conversation with an ally it made now also offers "Rename The Maya Pact…".

**After the review (fix-then-land, 2026-10-02):** all under the rules, so the 54 seasons without them still digest byte for byte.
- **The members' say is the web's group-pact bar** (M1, the lead's ruling): `WouldWelcome` asks only that the member is not under −10 toward the newcomer and that no grudge of forty stands either way, and the members are asked only once the newcomer has said yes. The newcomer's own three pacts moved into their answer. The refusal line names members only after that yes.
- **A bring-in never stacks pacts** (M2): an ally of the player's is refused for nothing in C4's words, and the rows offer them none.
- **The groups ruling** (m8): a member who turns on a pact of three or more is taken out of it, and the player cannot vouch for somebody they have soured on.
- **The minors:**
  - The one told about a leave from a pact that goes on answers that it goes on, never that it is over (m2).
  - The founder docs are corrected (m3).
  - The rename's week is a story cooldown (m4).
  - Captions stay unique: no two standing pacts of the player's share a name, and a walked-out pact's name is not on offer (m5).
  - The recap counts a join as a join (m6).
  - A fit check and capture with the longest captions (m7).
- **Mutations:** twelve mutations of these fixes each fail at least one test: members asked on the newcomer's no; the bar's line; the newcomer's three; stacking; the vouch's line; the turned member kept; the player's own souring; the going-on replies; the rename's cooldown; a proposal's name; a walked-out pact's name; an ended pact in a new name's way. A thirteenth survives and changes nothing: stopping the turned loop once the pact has ended, or carrying on.

**Where the build departs from the plan:**
- **"Consents through `WouldPropose`"** is, by the lead's ruling, the web's own bar for a pact of more than two (`WouldWelcome`, from `AllianceManager.tsx`): no member hostile to the newcomer (under −10) and no grudge of forty either way, asked only once the newcomer has said yes. The newcomer's own three pacts are in their answer. `WouldPropose`'s desire let almost nobody in: one join in 51 askings in the first build's harness.
- **A bring-in never stacks pacts, and the player cannot vouch for somebody they have soured on:** both are refused for nothing, since the player can see both.
- **A member who turns on a pact of three or more is taken out**, the pact going on (the lead's ruling on groups); C3 had ended the whole pact.
- **The cap** is four in the house; the plan named none, and validation holds a pact only to the cast.
- **C2's two leave tests** pinned that a leave from a pact of three ended it for everybody; they now pin C5's leave at the same price (`C2_LeavingOutsideABetrayalCostsWhatItAlwaysDid`, renamed from `...IsUnchangedByTheRules`).

**The bar, ruled.** The first build measured `WouldPropose`'s desire as the members' say and left the web's bar to the owner; the lead ruled for the web's bar. Under it, 8 of 43 askings in the 54 seasons joined. A member's no after a yes is rare - none there - since a member must be hostile to the newcomer, and in this house houseguests seldom sit under −10 toward each other. Most noes are the one asked's own (35).

**Found on the way:**
- After a leave from three the pact is no longer the player's: the alliances page drops its calls from the player's side, a juror from it reads it as no pact shared (0, as a cut-out betrayer does), and Game Sense notes nothing for it.
- A pact the player left counts toward the agency cap on pacts among houseguests.
- A betrayer brought into another pact of the player's stays betrayed there, since a pact's start is the player's, not each member's (no field says when a member joined).
- The alliances page's "brought in" reads the log, so it fades as the log rolls, as the invitation's line does; so does a walked-out pact's name in the names on offer, outside the read rules' facts.
- **Review m9 is left as found.** A betrayer's join week, or a juror reading 0 for a pact the player walked out of, could come only from the ledger's 'alliance-formed' entries. Those carry the pact's name as written and no pact id: a rename leaves them behind, and two pacts can share a name over a season. Nothing reliable can be read from them.
- A member taken out of a pact that goes on gets no Game Sense note: their line is the notice.

**Tell the story session:**
- Command kinds 59 (`BringIntoAlliance`) and 60 (`RenameAlliance`).
- **C5 names a pact in `secondTargetId`** (`BringIntoAlliance`, `RenameAlliance`, and `LeaveAlliance` under the rules), while `CallTheVote` and C6's `AllianceMeet` name theirs in `text`: a story or a recorded command that names a pact must use the field its kind reads.
- A player's pact can now hold up to four in the house, grown at its end, so `members[0]` stays its founder, and under the rules the founder calls the bloc's vote.
- A pact's name can change (the founder's, once a week, logged as `alliance-renamed`, the week kept as the story cooldown `rename:{week}:{pactId}`), so story text should read the name it has now. The player's standing pacts never share a name: a new one that would takes " II", " III" and on.
- A leave from a pact of three or more removes the player from its members, as the story's defector rule removes a defector, and logs "You left the alliance with … goes on without you." to everyone in it.
- A member who turns on a player's pact of three or more is removed from its members the same way, with "Riley Chen is out of The Trio.", a story defector's own words.

**Floors:** the Unity-free subset 1411 to 1434 (`GrowAndManageAlliancesTests`, 23); EditMode adds the same 23 and `WeeklyRecapPrivacyTests`' one; PlayMode adds 6.

**C7, negotiation, built 2026-10-02** in its own worktree from `claude/nearby-render-gap` at 1597bc0. A rule slice with one save field and one command kind, everything behind `commitmentRulesStartWeek` (`EpisodeEngine.CommitmentRulesOn`). The pure half is `Negotiation` (Simulation), the engine's `EpisodeEngine.Negotiation.cs`, the screen's `EpisodeDirector.Negotiation.cs`.
- **The field.** `DealState.linkedDealId` joins schema 22 - no new number: a deal and the price paid for it name each other. A price's id begins `deal-price-`; its proposer pays it and its recipient is owed it. The V21-to-V22 migration writes it null on every deal. Validation holds a link to its shape - both ways, the same two houseguests, struck the same week, exactly one of the two a price, and no price without what it bought - and a season without the rules to no link at all.
- **The command kind.** `Negotiate`, appended after `LockFinalArgument` (it lands after C5's): one of the web's situation moves, named by its `text` - `call-in:remind`, `call-in:demand` or `call-in:threaten` with the promise's id in `secondTargetId`, `mend-fences`, `veto-price:vote_save` or `veto-price:final_two`. A social action: the window's seat, its conversation gate (`EpisodeEngine.ConversationWindowRefusal`, the gate `RequireConversationWindow` now asks), one roll on the season's stream. Refused without the rules. The counter needs no kind: it is answered through `RespondToDeal`, naming the houseguest, since no deal waits for it to name.
- **The counter.** A proposal the roll turns down may come back as the same deal with a price on it (decision 13). Whether it comes is the web's (`generateCounterOffer`): a houseguest the roll found close to yes - its chance 40 or more - counters 70% of the time, never for an alliance a grudge refuses. The coin is `StoryRandom.Unit(s, Negotiation.CounterKey)`, keyed `w{week}:counter:{player}:{houseguest}:{kind}:{about}:{sequence}` with the sequence the season had reached at the attempt, so every attempt has its own and the season's stream is exactly what a plain refusal leaves. The price is the first that fits of what the houseguest wants (`Negotiation.CounterPrice`), never the kind asked for and never one already binding the two: on the block, the player's vote to keep them this week; at the endgame, a final two; a safety pact and a voting bloc this week, the web's own counter-proposals (`veto-lobbying-system.ts`). The terms are a pure function of the state and the refused proposal (`CounterTo`), so the screen shows them and the yes re-derives them. Nothing waits in any store: the counter is said after the refusal's own line (kind `deal-counter`, to the two of them) and stands while nothing the player hears has been said since (`OpenCounter`) - a line between houseguests, or a ledger row's or a story's id minted after it, leaves it standing; the next line the player hears, said to them or to the whole house, lapses it, and so does the end of its phase. One round: the yes strikes both deals at once with no roll - the deal asked for (its id begins `deal-counter-`) and the price - and is the yes to an offer (decision 15): +4, its reciprocal draw from the counter's own keyed stream, and either deal broken weighs one step heavier (`DealResolution.AcceptedOffer`, `Negotiation.FromACounter`). Nothing it does draws from the season's stream, a vote deal's lever line and an invitation's pact included. A no is a plain no, the line and nothing else; neither can be countered, and neither is a second roll of the proposal, since the yes costs its price.
- **A nominee's ask carries a price.** The veto ask (`NpcDeals.AskForTheVeto`, unchanged) offers a final two at the endgame, otherwise the nominee's vote to keep the player the next time the player is on the block (a vote save naming the player, open until a vote tests it, where the levers judge vote deals). The ask says so under its words (`Negotiation.AskPriceLine`); the yes strikes the price, linked to the ask, as a chance taken (C1). Struck by the same yes, the price is part of the offer the player accepted, as a counter's price is, so its payer breaking it weighs one step heavier (decision 15, `DealResolution.AcceptedOffer`: any price linked to an offer put to the player); the same price named by the player for the veto weighs its own. Once the season holds its 200 deals (`NpcDeals.DealCeiling`, validation's bound) the ask carries no price and says none, so the yes is never refused for a price there is no room to strike.
- **How the link resolves.** A price is owed while what it bought stands. When what it bought is broken by the one the price was owed to, in front of the house, while the price still binds, the price is void: it lapses (`expired`), with no `brokenById` and no `settledWeek` - nobody broke it - and both are told ("Maya Hassan no longer owes you a final two: you did not keep the veto commitment it paid for."). So the veto the player does not use on the nominee who paid is the player's breach (`brokenById` the player, `settledWeek` that week, C0's record) and voids the price; used, the ask is kept and the price stands, judged later by its own rule. Broken by the one who pays the price, the price still stands. A ballot's breach voids nothing: the line would tell the ballot (decision 4). A price that ran out its own week before what it bought was broken lapsed as any deal does, and is not called void (`Negotiation.Voided` reads the weeks). The Your word page says a voided price is "void: what it paid for was broken" (`CommitmentsRead.VoidedWord`). A price binds only what it names: the open vote to keep the player that a nominee owes for the veto blocks no vote deal with them about anybody else (`PlayerDeals.CanPropose`).
- **Calling in a promise.** A promise of safety or of a final two a houseguest made the player - a story's: `NpcPromises` never makes one to the player - can be called in once a week, the web's three ways: a reminder at 60, a demand at 50, a threat at 40, with the web's `calculateSuccessChance` on top (half their view up to +25, +15 in a pact, +10 with a deal standing, +15 for the player's Social, Competitive or Manipulative, -30 with a deal between them the player broke). Each costs what the web's previews say, whatever the answer, in their view of the player alone: nothing, -5, -15. Landed, the maker is held to it - once, half again, twice (`Negotiation.Hold`) - on their record of the player (`promise-held:{kind}:{approach}`; missed, `promise-pressed:`), and the promise weighs that much where it is settled: a safety deal's weight in their nominations (`StrategyRules.NominationReluctance`, `Negotiation.SafetyHeld`), a final two deal's obligation in their final choice (`EpisodeEngine.FinalTwoTerms`).
- **Mending fences** after a breach of the player's (C0's record), once for each, at the web's 55 (less its 30 for the broken deal). Landed, the web's +10 in the wronged one's view of the player and their grudge eased as much; missed, -5. One way and permanent on their record (`amends-made`, `amends-refused`), as the breach is held. The breach is untouched: still broken, by the player, still counted against them everywhere it was.
- **A veto for a price** is the web's own: the veto holder's leverage (`contextual-action-generator.ts`, Demand a Deal at 75). The player, holding the veto before the meeting, names a price to a nominee - their vote to keep the player, or at the endgame a final two. Three draws on the season's stream: the answer's roll, then the two reciprocal draws of the warmth it moves. Taken: the player's word on the veto and the price, struck at once and linked as a nominee's own ask and its price are, the nominee's own ask answered by it - a chance the player took, so Game Sense never calls it an offer left on the table - and the web's +10; refused, the web's -5. It strikes two deals, so it needs room for both under the player's 40 (`PlayerDeals.PlayerDealCeiling`), refused in the deal table's own words. The other nominee's yes is locked as X13 locks it (`VetoPromisedTo` reads a word on the veto either way round).
- **On screen.** A counter that stands heads the conversation it was said in: who it is from, its line, both deals and what each stakes, no roll and the lapse, the player's own read of the deal they asked for (`KnownOdds.Deal`), and 'Take Maya's counter-offer' (binds you · no roll) and 'Turn down Maya's counter-offer' (free). The moves sit under BARGAIN where they can happen, each pill its cost and the player's read of its chance (`Negotiation.Chance` as known: the presumed view and the pact as the player knows it), under the one note on the odds: 'Remind Maya of their promise of safety', 'Demand Maya keep their promise of safety', 'Threaten to tell the house if Maya breaks their promise of safety', 'Mend fences with Maya', 'Use the veto on Maya, for their vote to keep you', 'Use the veto on Maya, for a final two'. Every caption is new; none moved.

**Evidence.**
- `NegotiationTests` (31, Unity-free; 21 built with the slice and 10 for its review): the counter on a near miss; the season's stream left exactly as a plain refusal leaves it (a counter, a coin that missed, a season without the rules) and untouched by either answer; the keyed coin (same key, same draw, whatever the stream; about seven in ten land); the terms for each of the ten kinds; at once or lapsed; the yes (two linked deals, no roll, free, one round) and the plain no; no counter without the rules, far from yes or against a grudge; the ask's price for a house of eight and of six; the link's resolution - honoured, unused, used on the other nominee - and the price judged after by its own vote; an owed vote save in its payer's ballot; each call-in way landing and missing (odds, one roll, cost, hold, once a week); a held safety promise in its maker's nominations and a held final two in the final choice; the shown odds as the player's read; mending fences bounded and the breach untouched; no fences where the player broke nothing; a veto for a price taken and refused, then honoured and not; each move refused where it cannot happen; nothing without the rules; validation of a link's shape. Against 1597bc0's simulation, with the names C7 added stubbed to answer as that build would (no counter, no price, no link), 18 of the 21 fail; the three that hold on both pin what must: the coin's keying, an owed vote save read by the existing vote-deal term, and no counter where none may come. The review's nine: a counter standing through what the player does not hear and lapsing with what they do; a counter's yes for a vote deal and for an invitation drawing nothing from the stream; a counter's two deals weighing one step heavier broken, and a proposal of the player's own and a veto for a price not; a price standing when its payer breaks what it bought, void when its payee does and untouched by a ballot's breach, through the settlement itself; a price that ran out first not called void; an owed vote save blocking nothing it does not name; an ask with no price at the season's ceiling, its yes still taken; a veto for a price answering the ask as a chance taken; and a veto for a price needing room for both its deals. Against bc18317 - C7 before the review - with the two names the fixes added stubbed, 7 of the 9 fail; the two that hold on both pin paths that were already right: the counter's yes drawing nothing for a vote deal or an invitation, and who broke a price's bought deal deciding it. The tenth, for the lead's ruling: an accepted ask's price broken by its payer held one step heavier on the player's record, the same price named by the player for the veto at its own weight, and a price on any accepted offer counted as part of it; it fails against d01ba51's `DealResolution`, and nothing else does.
- `CommitmentRulesSeasonDigests`: the null link moved every rules-off digest of a season with deals until the checkpoint strips it with C0's two fields - it is serialized state, never an outcome - and then the 54 seasons digest byte for byte as 7991687 played them. Under the rules all 54 move and stay legal; the busy player, given C7's moves only where the rules are on (by words and the kind's name, so the file still compiles against the build before them), takes 2 counters, strikes 15 veto prices (14 voided - it says yes to both nominees, and the meeting saves the first), calls in 11 promises and tries 14 mends, each now asserted. After the review the harness answers a counter by the engine's own rule (the latest line the player heard) and counts a void by its line, as it is said, rather than every price that lapsed: 2 counters, 13 veto prices, 10 voids, 11 promises called in, 14 mends.
- The pins moved for `linkedDealId`: `UpgradeV21ToV22`, `FrozenEpisodeV21`'s note, `PersistenceMigrationTests.StripSchema22`, `PersistenceV22MigrationTests` (the link written null and its projection, a v21 save that smuggles it refused, the v21 build's own save loading with none), the digest harness's checkpoint, and validation's rules-off check. The v3 and v4 frozen-shape walks list `deals` as added since, so they never reach the deal row; the schema number and the load pins do not move. Outside the editor the persistence suites with their companions (706 cases) pass, compiled with Gamesim.Persistence against a one-line `Application` stub in a scratch project.
- PlayMode, for the lead to run: `Negotiation_ACounterStandsAtTheHeadOfTheConversationAndIsAnsweredThere` (both text sizes, taken and turned down; 'conversation-counter'), `Negotiation_AProposalTurnedDownComesBackAsACounterThatIsTakenThere` (the review's: the round from the proposal row, on a season shaped so the roll refuses and the coin lands, the shaping checked before the press), `Negotiation_ThePromiseOfAFinalTwoCalledInFitsAtBothTextSizes` (the review's: the longest caption; 'conversation-negotiation-final-two'), `Negotiation_ANomineesVetoAskSaysWhatItOffersAndTheYesStrikesIt` (both sizes, the ask's price line on the meeting; 'veto-meeting-ask-price'), `Negotiation_APromiseOwedAndABreachOfYoursAreOfferedAsMovesAndEachCommits` (both sizes; 'conversation-negotiation'), `Negotiation_HoldingTheVetoAPriceCanBeNamedToANomineeAndTakenStrikesBoth` (both sizes; 'conversation-veto-price'). Not compiled or run in the slice: the worktree may not run Unity or the offline compile, so the runtime and PlayMode edits were checked against the signatures they call.

**The audited walk** needs nothing: its deal proposal is still one command with a `deal` line (a counter's line has a kind of its own), and it answers no veto ask and makes no move. A frame taken after a refused proposal may now show a counter.

**Where the build departs from the plan:**
- **The counter is the same deal with a price**, not a lesser deal: the web's own counter (`COUNTER_OFFER_MAP`) offers something smaller for free, which would make every big proposal a second, free roll at a smaller one. The price is from the web's counter-proposals and the plan's two.
- **The counter's gate reads the houseguest's true chance** - the roll's own, not the player's read - as the web's does (the review's ruling, kept): it is real behaviour, a counter telling the player the roll was close as a yes tells them it passed, and it only ever follows a refusal the player paid an action for, so it is no free probe (C4 closed that one). The odds shown never say it.
- **"A veto for a price"** is the holder's leverage, the web's; the nominee's offer is the ask's price.
- **Calling in a promise** reaches the promises a decision settles - safety and a final two - since no other promise is ever weighed; a vote promise from a houseguest to the player is never made.

**Found on the way, for later slices:** the engine still takes a yes to both nominees' veto asks (X13 is the screen's lock), so one ask always breaks and, under C7, voids its price; houseguests promise the player nothing outside stories (`NpcPromises` leaves the player out), so calling in a promise waits on a story's; a price whose bought deal a ballot breaks stands, since voiding it would tell the ballot; a counter is announced by nothing outside the conversation, which is where it is answered.

**The review (fix, then land).** No blocker or major in C7's own code, and rules-off seasons byte-identical; fixed under the rules only, in a commit of its own: a counter's lifetime is what the player hears, not the last sequence minted (a story's id or a ledger row minted after it lapsed it); an ask carries no price at the season's 200 deals, and a veto for a price needs room for its two under the player's 40; a price is void only for a breach while it bound; an owed vote save blocks no vote deal about anybody else; a veto for a price answers the nominee's ask as a chance taken (it reconciled as an offer left on the table, -1); a counter's two deals weigh one step heavier broken (decision 15); the veto for a price's doc says its three draws; and the harness counts a void by its line, not every price that lapsed. Then, at the lead's ruling, in a second commit: the price a nominee's ask carries is part of the offer the player accepted too, and weighs one step heavier broken. Not changed, noted for later: prices only on veto asks (B1 merges the plea and the vote-save offer); no counter for 'Propose an alliance', which is C4's own row, not the deal table's; a deal broken by a ballot still costs its price, since voiding it would tell the ballot; a final two, a safety pact or a voting bloc struck as a price binds both sides, as any deal of its kind does; a threat is dominated in expected value - 40 at -15 for a hold of two, against a demand's 50 at -5 for a hold of one and a half - which E2 should measure; a mend that lands leaves the story's pending reckoning standing; X13 is still only the screen's lock; and the 40-deal ceiling counts every deal in the season (X10), so counters vanish late in a long season.

**Saves:** a schema 22 save written by the integration branch before C7 no longer loads - the save checks fields exactly, and C7 adds `linkedDealId` to schema 22 with no new number. Acceptable because wave B ships as one PR, so no released build ever wrote one; any such save on a developer's machine is to be started again.

**Tell the story session:** the new command kind `Negotiate` (after C5's); the schema 22 field `DealState.linkedDealId`, and that a schema 22 save from before C7 no longer loads; the `deal-price-` and `deal-counter-` deal ids; the event kinds `deal-counter`, `call-in` and `amends`; the ledger types `promise-held:{kind}:{approach}`, `promise-pressed:{kind}:{approach}`, `amends-made` and `amends-refused`; a story's promise of safety or of a final two to the player can now be called in, and weighs in its maker's decision once it has been; a mend that lands eases the grudge but leaves the story's pending reckoning for the breach standing.

**Floors:** the Unity-free subset 1411 to 1442 (`NegotiationTests`, 31); EditMode adds the same 31; PlayMode adds 6.

**C8, breaches become house knowledge and your word has a reputation, built 2026-10-02** in its own worktree from `claude/nearby-render-gap` at 6fc74b8. A rule slice with no saved field, no command kind and no new fact kind: `broken-word` has been vocabulary since schema 16 and was only ever narrated (`EpisodeEngine.Whisper`); the engine writes it now. Everything is under `commitmentRulesStartWeek`, and the fact, its spread and the reading need the house's knowledge too - the story's facts and the gossip that carries them, `StoryRules.Bonds` - which the director turns on with the rules in every season it starts (`YourWord.On`). A season with the commitment rules and no story's knowledge (a test, the digest harness's legacy set) keeps C0's behaviour throughout. The reader is `YourWord` (Simulation); the engine's half is `EpisodeEngine.YourWord.cs`, the settlement's spread (`SpreadBetrayal`) and the story's anchor systems.
- **Whose breaches.** The player's own: the plan's text is about the player's word. A deal a houseguest breaks - with the player or with another houseguest - is heard of as it always was: the house's spread, each other houseguest at two in five, −5 to −15 on their view of the breaker, from the season's stream (`SpreadBetrayal`), houseguest to houseguest and hidden, as NPC deals are dark. Except that the player is no longer one of its witnesses: their own view of a breaker never moves without a line (the plan's "the player's reading changes only with a whispered line saying why", read for the player's own reading too, as X8 read it for a rumour).
- **The fact.** A deal the player breaks by an act the house watches - a nomination or a replacement (a safety deal, a target deal), the veto meeting (a veto deal), the final choice (a final two deal) - is written as a `broken-word` fact (`Knowledge.BrokenWord`): the player its actor, the one wronged its subject, the deal's id its reference, one fact a deal, out as a whisper, and known at first to the two of them and nobody else - the house saw the act, not the deal it broke. A deal the player's ballot breaks (a vote deal, a partnership judged at the vote, even the Head of Household's tie-break) writes nothing: the house votes in secret, and a breach a ballot decided never becomes house knowledge this way (`KnownBallots.SettledByABallot`). Neither is heard of the house's old way: a breach of the player's no longer moves two in five of the house against them at once and in silence.
- **The spread.** The house's own gossip carries the fact (`Knowledge.Spread`): at each of the week's anchors, one more houseguest at a time, the one who knows telling their warmest houseguest who does not, at 0.8 for one allied with the one it hurts, 0.6 close to them, 0.3 close to the player and 0.2 otherwise, by how sociable and how discreet the teller is - on keyed odds, never the season's stream. The player is never a teller or a listener.
- **The whispered line.** Every houseguest it reaches thinks less of the player - −10, the middle of the house's −5 to −15, with no draw, through `EpisodeEngine.HeardAbout` (their view of the player, their record of them as `heard_about_betrayal`, one way and fading as it always did, and the arc the two of them share; never `Change`) - and the player hears who heard what, to them alone, as a `story-whisper`: "Word in the house: Maya Hassan heard you went back on your safety deal with Alex Reed." (`YourWord.HeardLine`). The first knower, the one wronged, is told in the settlement's own line ("You broke a safety pact with Alex Reed.").
- **The reading** (decision 14, `YourWord`) is built from those facts and nothing else - never the deals record, a houseguest's view or their private record of the player, never a private fact, never anybody else's broken word. Each houseguest who knows of a breach is a hearing, those since gone included (their knowledge does not leave with them), up to six a breach (`WholeHouse`): six make it the house's, and from then on it weighs what any breach weighed before, `PlayerDeals.BrokenDealPenalty`'s 12. So 2 a hearing (`PerHearing`), and at most 36 in all, three breaches' worth (`Most`, `YourWord.Cost`). In a word: good (nothing heard), questioned (less than a breach's worth), doubted (less than two), broken (`YourWord.Word`). It changes only with a line the player hears - a breach's own settlement line, then each hearing's whisper; an eviction changes nothing, and the list's bound never drops one of its facts (X14 below).
- **What it moves.** The acceptance roll of everything the player puts to the house - the deal table, 'Propose an alliance' (C4), a bring-in (C5), and so a counter's gate (C7) - takes the reading in place of C0's count of every breach the record holds against the player (`PlayerDeals.WordPenalty`), and the odds the player is shown take the same term (`KnownOdds.Deal`), since the player sees the reading. A breach weighs at the table only as far as the house has heard of it, and a ballot's costs nothing there; the one it was broken against still holds it, in their view and their permanent record (C0), which the roll reads as it always did. "I've heard you've broken deals before" is said only of what the house heard, past twenty points of it.
- **Where the player sees it.** The Your word page opens on it: an eyebrow, IN THE HOUSE, carrying the page's mark, over a card named "Your word · the house" (`EpisodeHud.WordReadingCard`) headed by the reading in words and colour - "Your word is doubted", good in the allied green, broken in the conflict red, the two between in the warning's - then who has heard ("3 houseguests have heard of you going back on your word: Alex, Maya and Jo. It weighs on every deal you put to the house, the more the further it spreads.") and each breach the house knows of with who knows it ("Your safety deal with Alex, broken in week 3 · known to Alex, Maya and Jo"). The note over a conversation's chances ends with it once anybody has heard ("Your word is doubted: 4 houseguests have heard of you going back on it, and it weighs on every deal you put to the house.", `YourWord.OddsLine`). No caption changed, and nothing new is a control.
- **X14** (`Knowledge.MakeRoom`, which every writer of a fact now goes through: a story's, an alliance's, the showmance pass's and the broken word's). Under the commitment rules a full list (`Knowledge.Ceiling`, validation's 128) drops the oldest fact that is neither an alliance's - so a private pact's fact is never dropped and never read as public to every voter - nor the player's broken word, which the reading reads and which must not change without a word. Where every fact is one of those, the oldest fact of a pact that no longer stands goes (no voter weighs an ended pact); failing that, the oldest. Neither fallback is reached in a season. Without the rules the oldest goes, as it did.
- **The story's Spread over-grant** (the found-on-the-way note: a story's `Spread` effect for an alliance makes every pact holding the named pair known) is a story effect, not the gossip C8 rides on, and is not on its path: left for B4.

**The audited walk** needs nothing: it nominates, vetoes and votes as it did, checks none of what changed, and asserts no deal's answer. Where it breaks a deal of its own in front of the house, the gossip's lines are `story-whisper`s, which no check counts, and a later proposal's odds carry the reading.

**Evidence.**
- `YourWordInTheHouseTests` (10, Unity-free): the fact a public breach writes and its first knowers (the veto kept from a partner); a ballot's breach (a partner voted out) writing nothing, heard by nobody and costing nothing at the table, against C0's 12 without the story's knowledge; the existing gossip carrying the fact as it carries every fact (told by the one who knew it to their warmest houseguest who did not; the player never a teller or a listener); every houseguest the gossip reaches, over two weeks of an engine-played season, named to the player in one line each, with the hearing on their view and record, and nobody hearing without a line; the reading's inputs (a ballot's breach, a private fact and somebody else's broken word never count; a hearer who left still counts; six a breach; 36 at most); the roll and the shown odds moving by the same term, kind by kind, and the "I've heard" line only of what the house heard; X14 (a full list keeps an alliance's fact and the player's broken word, and the pact stays private; with every fact an alliance's, the oldest of a pact no longer standing goes); without the rules or the story's knowledge, nothing written and C0's term; and the player hearing of no houseguest's breach in silence, while the house still does. Against 6fc74b8's simulation - with `YourWord`, `Knowledge.BrokenWord`, `Knowledge.Ceiling` and `PlayerDeals.WordPenalty` stubbed to answer as that build does (no reading, C0's term, a fact built by hand) - 8 of the 10 fail; the other two pin what must not change: the existing gossip carrying a whispered fact, and a season without the rules or the knowledge. Eighteen mutations - the player a witness again, a ballot's fact, the old spread for the player's breach, no hearing, no line, no view, the oldest dropped, the broken word unprotected, the ended pact not first, the roll, the shown odds and the line on C0's term, no cap a breach, no bound, a private fact counted, the departed forgotten, every houseguest a first knower, the fact private - each fail at least one of them.
- `CommitmentRulesSeasonDigests`: without the rules the 54 seasons digest byte for byte as recorded. Under them all 54 move and stay legal: across them 12 deals the player broke in front of the house became facts, the gossip reached 33 houseguests with them, and 33 lines named them to the player - the rules-on half asserts each (`broken-word`, `word-heard`, and `word-heard-line` equal to it). The other slices' counts still hold: C7's counters 6, veto prices 11, voids 9, promises called in 8, mends 10; C5's 23 askings, 7 joins, 7 renames; C6's 18 meetings, 18 allies asked and answered straight, 6 ally claims all kept.
- PlayMode, not run in the slice: `WordInTheHouse_TheReadingHeadsYourWordAndEndsTheOddsNote` - at both text sizes, the one note over a conversation's chances begins with whose read they are and ends with the reading; the Your word page opens on the card, headed and coloured by the reading, with who has heard and the breach's line, under the eyebrow carrying the page's mark and above the settled deal, not a control, every label drawn in a box at least 1.3 times its words; without the rules no card and nothing of a reading in the note. A batch run photographs 'your-word-in-the-house' and 'conversation-word-heard', with '-large' forms and the conversation's '-4x3'. The runtime and PlayMode edits were not compiled offline in the slice, as briefed; they were checked against the signatures they call.

**After the review (fix-then-land, 2026-10-02):** no blocker or major; seasons without the rules digest byte for byte, and the knowledge gate holds. Fixed under the rules only, in a commit of its own:
- "I've heard you've broken deals before" comes only from a houseguest who has heard of a breach of the player's themselves (m1); "Your track record concerns me" reads the deals the house has heard of, not the record's count (m3) - words only, the odds untouched.
- The card's empty sentence no longer claims nobody knows of a breach - a ballot's breach is the player's and their partner's, and SETTLED shows it broken - so it reads "The house has heard of no deal you broke in front of it." (m2, `YourWord.NothingHeard`).
- Validation's bound on the facts is `Knowledge.Ceiling` (m4, no outcome change).
- `MakeRoom`'s last fallback takes the oldest alliance fact before the player's broken word, which goes only when nothing else is left (m6).
- The PlayMode test photographs the page's default state - "Your word is good" over the empty state - at both text sizes ('your-word-good'), and its box check reads an auto-sized label's largest size (m12, m13).
- Two tests (`UnderTheRulesATrackRecordIsTheBreachesTheHouseHeardOf`, `X14_ThePlayersBrokenWordGoesOnlyWhenNothingElseIsLeft`) and a third check in the odds test, each failing on the fix's mutation.

**Recorded by the review, not changed (for the owner, B1 and E2):**
- **m5.** The Head of Household's tie-break and the final four's sole vote are never house knowledge, though each is read out: every ballot's breach stays out of it.
- **m7.** The spread is a whole-house ratchet: knowledge only grows, `WholeHouse` is a fixed six whatever the house's size, and a hearer who has left still counts - so late in a season, or in a small house, a breach the remaining house all knows can weigh less than six hearings, and an early one never fades.
- **m8.** Hearings go on after the player is evicted (the gossip still runs, and the lines reach a player who follows the season). If that is ever gated, gate `Knowledge.Spread`'s choice of listener, never the hook: the hook gated alone would let a houseguest learn of it silently.
- **m9.** Naming who heard leaks a little by inference: the teller is the one wronged or an earlier hearer, and the listener is their warmest houseguest who did not know, so each line says something of who is warm on whom.
- **m10.** Emergency meetings (`emergency-meeting`) fire more often after a public breach: the one wronged holds the story's 60 and knows a fact about the player that is not public. The story session is told.
- **m11.** A broken promise still spreads silently (`SettlePromise`'s witnesses, the player among them): B1's.

**Where the build departs from the plan:**
- **"The player's reading changes only with a whispered line"** is kept both ways: the house's reading of the player changes only with a line they hear, and the player's own reading of a houseguest no longer moves when they would have "heard" of a breach between others in silence (no line is given for those: NPC deals stay dark).
- **The reading replaces C0's count at the table** rather than adding to it: a breach costs there only as far as the house has heard of it, and one the whole house knows costs what any breach did. Decision 14's "moves acceptance odds" is that term.
- **A ballot is never house knowledge, the tie-break included**: the Head of Household's deciding vote is read out, but the deal it broke was private, and every breach a ballot decided stays out of the house's knowledge, as `KnownBallots` keeps ballots one class.
- **Only the player's breaches are facts.** NPC-to-NPC deals stay dark, and a houseguest's breach of a deal with the player keeps the house's old spread.

**Found on the way:**
- `NpcDeals.Adjusted` - which houseguests put offers to the player, and which nominees ask them for the veto - still reads every breach the record holds against the player, −15 each, not the reading: B1's ("the reputation").
- A broken promise is still heard of the old way (`SettlePromise`'s witnesses, in silence, the player among them): C8 is about deals, and B1 brings promises into the same record and reputation.
- The emergency meeting (`emergency-meeting`) casts a caller who resents the player and knows a fact about them that is not public: the one a broken deal was broken against, holding the story's 60, can now call the house together over it - the story reading C8's facts as the knowledge rules meant it to.
- A mend (C7) eases the one wronged and leaves the house's reading where it was: the house heard what it heard.
- Validation bounds the facts at a literal 128; `Knowledge.Ceiling` names the same bound and nothing ties the two.

**Tell the story session:**
- The engine now writes `broken-word` facts (no new kind): the player its actor, the one wronged its subject, the deal's id its `refId`, whispered, the two of them its first knowers; for a deal the player breaks by a nomination, a replacement, the veto or the final choice - never a ballot's - one a deal, under the commitment rules where `StoryRules.Bonds` holds. `Knowledge.Spread` carries them as any whispered fact; each houseguest it tells moves −10 on their view of the player (`heard_about_betrayal`) and the player hears a `story-whisper`, "Word in the house: X heard you went back on your safety deal with Y." The engine's own whisper for a houseguest's broken word ("... went back on their word to ...") is unchanged, for a story that writes one.
- X14's rule: every writer of a fact makes room through `Knowledge.MakeRoom`, and under the commitment rules a full list drops the oldest fact that is neither an alliance's nor the player's broken word; then the oldest fact of a pact no longer standing; then the oldest. A story's own facts - secrets, plans, couples, outcomes - are the ones that go.
- `YourWord` is the house's reading of the player's word, for a story to read; and the emergency meeting can now be about a broken deal, and fires more often after a public breach (m10).

**Floors:** the Unity-free subset 1483 to 1495 (`YourWordInTheHouseTests`, 12 after the review); EditMode adds the same 12; PlayMode adds 1.

**C9, the endgame, built 2026-10-02** in its own worktree from `claude/nearby-render-gap` at 6fc74b8.

**First, the investigation, with nothing changed.** `FinalChoiceSeasonHarness` (Tools/SimulationTests only, `[Explicit]`, compiled out of Unity: `dotnet test Tools/SimulationTests --filter "FullyQualifiedName~FinalChoiceSeasonHarness"`) plays 1,500 seeded seasons under the director's rule set to the final eviction: houses of 5, 6, 8, 10 and 12, a hundred seeds each, and three scripted players. One only does what each phase asks (nominating in cast order, voting by a coin); one is busy; one builds pacts and endgame deals with the three houseguests warmest to them. The last two nominate and vote by their own reads. The harness reads a houseguest's final choice exactly as the engine makes it (`EpisodeEngine.FinalChoice`: the final eviction step's own code moved into a function, nothing else changed), with every term for each finalist, and how the jury would vote between the Head of Household and either of them.
- **Confirmed.** Without the commitment rules, a houseguest who holds the final Head of Household evicts the player in 214 of 228 final threes (94%). Under the rules as wave B had them at 6fc74b8 (C1's final two obligation, C7's held promises), it is 246 of 262 (94%). The handoff's 164 of 167 is the same finding.
- **No one term names the player; nearly every term leans the same way.** The choice is the weekly eviction vote's evaluator, with the two finalists as its nominees. At the final three almost all of its terms favour the other finalist, by 47 points on average (the median 47, the tenth percentile 7). Each term, the other finalist's mean less the player's, under the rules (without them in brackets):
  - **threat +8.7 (+9.3):** the evaluator's endgame weighting (×1.5 at five or fewer) on the player's competition record, though no competition is left to play. A player who reaches the final three in these seasons has won 3.4 competitions, the other finalist 0.9.
  - **bond +8.6 (+7.9):** a story bond (showmance or ride-or-die, +25) between the Head of Household and the other finalist in 82 of 262 finals, and with the player in 1.
  - **grudge +8.2 (+7.3):** 216 of 262 Heads of Household hold a story grudge against the player (−15 on average), mostly from the player's own nominations: the player had nominated the Head of Household in 122.
  - **personality +6.3 (+6.1):** shared traits, which the default player, the newcomer, has none of and can never score.
  - **history +5.6 (+5.3):** the player's arc with the Head of Household, a term only the player carries, mostly a rivalry.
  - **relationship +5.3 (+4.6):** the Head of Household's view of the player averages −16, of the other finalist +39.
  - **deal +3.9 (+3.5), alliance +1.3 (+0.9), strategic value −2.0 (−1.7).**
  - **C1's final two obligation −0.2:** it is scaled by the Head of Household's view of the finalist, to nothing at zero or below, and that view of the player is under zero in most of these finals. So a final two deal with the player rarely counted: 14 of the 16 players who held one were evicted.
- **Pacts and deals barely move it.** With a pact that holds from the Head of Household's side, 6 of 10 players were evicted (11 of 18 without the rules).
- **The choice works against the Head of Household's own endgame.** The jury's own reader (`WebJuryVoting.Score`, with its final impression of up to ten either way) gives the Head of Household a better chance of winning beside the player in 219 of 262 finals (0.9 against 0.7 on average), and the player is evicted in 205 of them.

So C9 takes out the two terms about a game the final three no longer has to play (threat and strategic value), weighs the jury the final two will actually face, and gives a pact and the endgame's deals a weight that can decide.

**Then the build.** A rule slice: everything is behind `commitmentRulesStartWeek` (`EpisodeEngine.CommitmentRulesOn`), with no saved field and no command kind. The engine's half is `EpisodeEngine.FinalChoice.cs`.
- **The final three's own choice** (`EpisodeEngine.FinalChoice`, `FinalChoiceTerms`). Still the evaluator, with the two other finalists as its nominees, but under the rules:
  - **Left out: threat, strategic value and personality** (`FinalChoiceLeavesOut`, through a native `WebVoteOptions.omittedFactors` that every ballot leaves empty). No competition or nomination is left at the final three; what the competition record is worth now is the jury's respect for it. Personality was added at the review, by the lead's ruling: the shared traits the default newcomer can never score decided 15 final choices alone, every one evicting the player. Every weekly ballot still weighs all ten.
  - **The jury** (`FinalJuryWeight`, 50): the term on each finalist is 50 × (the Head of Household's chance of winning beside them − ½), so −25 to +25. The chance (`FinalJuryChance`) reads the jury as it will sit and as the cut will leave it. On a copy of the season the engine's own final eviction (`FinalEvict`) is run with the other finalist cut, so a final two deal or promise the Head of Household breaks with them, the grudge for it, the pacts and deals that end as they leave, and their seat on the jury are all as the jury will count them; nothing is copied by hand. Each juror is weighed by the jury's own reader (`WebJuryVoting.Score`), with its final impression of up to ten either way on each finalist, as the engine's jury draws them. It is the chance of a majority, a tie going as the reveal awards it (to the second of the two in cast order). A player on the jury is read by their own views. Pure: the copy draws and mints on itself, and the season read never moves.
  - **A pact that holds from the Head of Household's side** (`FinalPactTerm`, 20, read through `Allegiance.Holds`, and the Head of Household's view of either finalist at `QuietLine` or above): Loyal ×1.5, Sneaky nothing, counted once however many pacts the two share. It sits on top of the evaluator's own alliance term (about 9 for a pair). A pact they turned on (C2), went cold on (under `QuietLine`, C3) or that ended holds nothing.
  - **Private:** the pact and jury terms are the Head of Household's own reasons, never a reason the player is told (`EvaluateNominee` gives them private visibility; no ballot carries them).
  - **The endgame's deals:** C1's final two obligation and C7's held promise are kept as they were. A final three deal the two kept to the final three is half a final two's obligation (`FinalThreeObligation`, 12.5), scaled the same way, by the Head of Household's view of the finalist and their word.
  - **The weights against each other:** a Head of Household sure to win beside one finalist and sure to lose beside the other leans 50 points to the first. That is more than any single feeling the evaluator weighs (a story bond is 25, a grudge at most 25, a pact with its alliance term about 29), and less than a pact and a final two deal kept together (about 59 at a plain word and a view of fifty). The first build had the jury at 60, and a test caught it outweighing those two commitments together.
- **The final three deal** (`DealKind.FinalThree`, "final_three", Title "Final Three Deal", no new field). The web build has none (its deal model stops at the final two), so this is the port's own.
  - **What it binds:** what a safety pact binds, until the final three. Neither puts the other up, at the nominations or as the veto's replacement. A ballot never judges it, since a ballot is private, but a ballot on the partner weighs it as a safety pact (`WebEvictionVoting.PairDealValue`, 35), and so does a Head of Household's reluctance to nominate them (`StrategyRules.DealWeight`, 35).
  - **When it can be put:** from the final six to the final four, and only under the rules.
    - Past six: "It is too early in the season to be talking about the final three."
    - At three: "The final three is already here." An offer of one still unanswered lapses as the house reaches three (no roll, no line), and is no yes to give there.
    - At four, once the final four's veto meeting is over and its eviction is still to come (`NpcDeals.FinalFourBlockSet`): "The final four's block is set: it is too late for a final three deal." A deal struck then could never be broken and would always be kept, so none is put or taken, and an offer standing then lapses as the veto is decided. The final four's own free time, after the final five's eviction, is still open.
    - To somebody a final two deal already binds the player to: "You already have a final two deal with them." The houseguests' own ladder has the same rule, and a counter-offer never prices a final three deal with a final two.
    - Without the rules it is refused in the words an unknown kind always was. Validation refuses a final three deal in a season without them, or dated before their first week, with the rules' other records' message. A story strikes one only under them.
  - **Asking for one:** it is asked as the other endgame commitment is. Ten off the chance, the ally's +10, the loyal and the emotional wanting it as they want a final two, and −15 below a view of 35 (the table's "favourable" line; a final two's bar is 50). `KnownOdds` reads it the same way.
  - **Houseguests put one** at the endgame, under the final two's rung (`NpcDeals.FinalThreeWarmth`, 45), unless a final two or a final three already binds the two of them or the final four's block is set. That covers both the player's offers and deals between houseguests.
  - **How it resolves**, in C1's verdict style:
    - Broken by a nomination of the partner, on the record: whoever nominated broke it (`DealResolution.Nomination`, as a safety pact's).
    - Kept by both at once when the two reach the final three, settled as the final three's Head of Household begins (`DealResolution.ReachesTheFinalThree`, `KeepTheFinalThree`), so a final three made by production's removal keeps its deals too. The line is "{a} and {b} held to their final three deal.", told to both. Both views move +16 (High trust), with the record both ways.
    - Ended by an evictee, blaming nobody (X4's `EndWithTheEvictee`, already generic).
  - **Its weight:** `DealTrust.High`, the safety pact's, since the act that breaks it is the same. Open-ended.
  - **Its readers:** the Your word page ("final three deal", "to take each other to the final three: neither nominates the other", "until the final three") and its nomination and veto warnings; Your week; the finalist read's breaker (by the record); and the offer's sentence on screen ("… wants the two of you to take each other to the final three: neither of you puts the other up until then."). The jury house, the notes, the alliances page and the relationship web read deals by title, so they need nothing. The deal table's caption is "Propose a final three deal": new and unique, and no caption changed.
- **Nothing hidden is shown.** The player's own Final 2 page (Q0, `FinalistRead`, `FinalChoiceWords`) is untouched. The houseguest's terms are computed only where the engine decides.

**The distribution, before and after** (`FinalChoiceSeasonHarness`, the same 1,500 seasons, with the review's fixes in):
- **Without the rules:** 214 of 228 (94%), unchanged. The half pins it.
- **Under the rules:** 246 of 262 (94%) before C9, 200 of 266 (75%) after (218 of 269, 81%, in the first build, before the review's fixes). The passive player is evicted 82% of the time, the busy one 76%, the allied one 67%. The half asserts that the rate is under 90%, that no left-out term appears, and that the choice turns on a pact, on the jury and, in the counterfactuals below, on a pact and on a final two deal.
- **The paired counterfactuals are the evidence that carries the weight** (the review's M9). The splits below compare different seasons, and several are small. Each counterfactual is the same final eviction re-run on its own copy, over the 200 finals that evicted the player:
  - **a pact between the two added:** kept 37 of 200 (19%). It would hold from the Head of Household's side in only 78 (their view of the player is under −10 in the rest), and of those it keeps the player in 37 (47%);
  - **a final two deal between the two added:** kept 46 of 200 (23%);
  - **the Head of Household's view of the player set to their view of the other finalist:** kept only 12 of 200 (6%). What still tilts these finals is less the view itself than the story's grudges and bonds and the player's arc.
  - **Without the rules, the same three keep the player in none of 214.** The evaluator's relationship weight at the final three is small for the many Strategic and Analytical Heads of Household (a view 38 points warmer moves their lead by about 2), and a pact's alliance term is partly cancelled by the threat term's pact size.
- **The splits, for what they can carry:**
  - **The jury:** its term decided 42 final threes on its own, 40 of them keeping the player. The player is evicted in 158 of 221 finals (71%) where the Head of Household's chance is better beside them, 42 of 45 (93%) where it is better beside the other, and 27 of 31 where the player would beat the Head of Household.
  - **A pact that holds:** 1 of 7 evicted, against 199 of 259 without one. The sample is small; the pact counterfactual above is the stronger evidence.
  - **A final two deal with the Head of Household:** 6 of 16 evicted, against 194 of 250. The first build had 12 of 17, while its jury estimate still read a final two partner as a friendly juror (M1).
  - **Grudges:** 178 of 220 evicted with one, 22 of 46 without.
- **What still tilts against the player in these seasons is personal:**
  - story grudges (220 of 266 Heads of Household, mostly from the player's own nominations; decided 36 alone, 25 of them evicting the player);
  - story bonds with the other finalist (88 against 1);
  - the player's own arc (decided 13, all evicting the player);
  - the Head of Household's view of them (−16 against +40 for the other finalist), though by the counterfactual it decides few on its own.
- **C1's final two obligation, kept as it was,** still scales by the Head of Household's view now (to nothing at zero or below). Added to the evicted finals, a final two deal keeps the player in under a quarter.

**Evidence.**
- `FinalChoiceTests` (16, Unity-free):
  - a pact that holds keeps the finalist (and the engine's own step agrees);
  - the pact's Loyal and Sneaky words;
  - a pact turned on, gone cold or ended holds nothing, and the other finalist's pact holds as a pact does;
  - C1's final two and C7's held promise as they were;
  - the jury preferring the finalist the Head of Household can beat, both ways round;
  - a pact and a final two deal outweighing the surest jury;
  - the jury's chance: every juror sure, a two-two tie each way of cast order with the cut finalist among them, every juror on the fence (11/16 and 5/16), nobody beside themselves or a juror;
  - the two terms left out, and present without the rules;
  - without the rules the choice is the evaluator's, term for term, with a pact, a jury or a final two deal in play;
  - the final three deal's availability and refusals, its draft, its validation, its ask and shown odds, a nomination of the partner breaking it (and the player warned before doing so), reaching the final three keeping it, an evictee ending it, its obligation in the final choice, houseguests putting it at the endgame only, and a ballot weighing it as a safety pact.

  Compiled against the simulation at 27d31c5f (6fc74b8's, with the final choice only moved into a function), with C9's new names swapped for stand-ins that answer as that build would, 14 of the first build's 16 fail. The two that hold on both pin what must not change: C1's and C7's terms, and the choice without the rules. Three mutations of C9's own code each fail their test: the pact read without `Allegiance.Holds`, the jury's tie given the other way, and the final three never kept.
- The review's 10 (26 in all), each listed under "After the review" below. Four mutations of the fixes each fail their own test: the jury read on the season as it stands, the cold line dropped for a houseguest finalist, the C9 terms made visible to the player, and offers of a final three deal never lapsing (that one fails both of the lapse tests).
- `CommitmentRulesSeasonDigests`: without the rules the 54 seasons digest byte for byte as recorded. Its checkpoint digests the ten deal kinds the recorded build knew, since the final three deal is a reader no season without the rules has. Under the rules all 54 move and stay legal: 4 final three deals struck (1 the player's), 2 kept, and 2 final choices with the player in it. The second half asserts each.
- The Unity-free subset: 1,483 to 1,509.

**The audited walk** needs nothing. Its finale path walks the player as a finalist and as a juror alike (PortVerification.Season), and its season's seed is the clock's. Its optional deal runs in week one of an eight-house, where no final three deal is offered.

**After the review (fix-then-land, 2026-10-02):** all under the rules, so the 54 seasons without them still digest byte for byte and the harness's half without them is still 214 of 228.
- **M1, the jury read as the cut leaves it.** The jury's chance had read the cut finalist on the season as it stood, so the final two deal or promise the Head of Household was about to break with them still read as that finalist's vote: the estimate rewarded cutting the partner the Head of Household was committed to. It now runs the engine's own `FinalEvict` on a copy, with the other finalist cut, and scores the jury there. The review asked for a helper extracted from `FinalEvict`; running `FinalEvict` itself on the copy carries every statement that acts on the cut finalist before the jury counts, in its order, with nothing extracted, moved or copied, so `FinalEvict` and every season without the rules are untouched.
  - Test: a final two partner who likes the Head of Household (40) reads as Maya's vote about 95 times in 100 on the season as it stands. Once cut, their view falls to −5, the breach weighs on the jury's obligations, and the tie the cast order would give Maya needs them at 121 in 800. The season's JSON, stream and sequence are untouched.
- **M2:** a final three deal is not put to somebody a final two deal already binds the player to ("You already have a final two deal with them."), and a counter never prices a final three deal with a final two.
- **M3:** an offer of a final three deal is no yes to give at the final three ("The final three is already here."), and lapses as the house reaches three, both after the final four's eviction and as the final three's Head of Household begins (production's removal). No roll, no line.
- **M4:** the window closes once the final four's block is set: no proposal, no rung, no yes, and an offer standing lapses as the final four's veto is decided. The final four's own free time is still open.
- **M5:** the pact term holds both finalists to the same cold line (`QuietLine`); `Allegiance.Holds` already asked it of the player.
- **M6 (the lead's ruling):** personality is left out of the final choice too.
- **M7:** the pact and jury terms are private, never a public reason.
- **M8:** validation refuses a final three deal dated before the rules' first week, not only in a season that never played them.
- **M9:** the harness's paired counterfactuals (above), with the claims the splits cannot carry softened.
- **The tests the review asked for:**
  - nothing written or drawn by `FinalChoice`, `FinalJuryChance` and `FinalChoiceTerms`, with a pact, a kept final three deal and a final two deal in play;
  - the replacement naming the partner breaking a final three deal, with the finalist read's breaker and the Your word page;
  - Your week reading the kept line;
  - each fix above.

**Not changed, a follow-up for the owner and the next slice:** C1's final two obligation and the final three obligation both scale by the Head of Household's view of the finalist now. The view is counted twice, since it is also the evaluator's relationship term. A final two deal struck while warm is inert in the typical final, where the view of the player is −16. One endgame lapse line should be decided: the view when the deal was struck unless it has lapsed, or the pact's `QuietLine` ramp. It goes together with a cap on stacked obligations, and with the social simulation's drift that leaves houseguests at −16 on the player.

**Where the build departs from the plan:**
- **Threat, strategic value and personality are left out of the final choice under the rules.** The plan named terms to add. The investigation found the existing ones tilted the choice by construction: the threat term's endgame weighting falls on a competition record that no longer has a competition to threaten, and personality is a trait match the default newcomer can never score (the lead's ruling at the review).
- **The final three deal is never judged by a ballot.** A ballot on the partner weighs it, and one against them leaves it to end with the evictee: judging it would tell the player a ballot (decision 4).
- **A kept final three deal enters the final choice** as half a final two's obligation, since at three it is already kept. At four it enters the decisions that decide who reaches three: the nominations, the replacement and the ballot.

**Found on the way, for later slices:**
- The personality term (shared traits) can never score for the default newcomer, who has no traits. The final choice now leaves it out, but every weekly ballot still weighs it, so every NPC nominee beside the newcomer carries a few points either way that the newcomer never can.
- C1's final two obligation reads the Head of Household's view as it stands now (the follow-up above).
- NPCs' views of the player at the final three average −16, against +40 toward each other, even for a player who courts them. Worth a look at how the social simulation moves views of the player.
- Two PlayMode conversation fixtures sit on the six-house shipped scenario under the rules (the veto price's conversation with a nominee; `AssertConversationFits`). They now draw one more deal row, "Propose a final three deal", and the capture 'conversation-veto-price' changes with it.

**Tell the story session:** the deal kind `final_three` ("Final Three Deal", `DealKind.FinalThree`) exists only under the commitment rules. A story deal of that kind is skipped without them, and validation refuses one in a season without them. Its kept line is "{a} and {b} held to their final three deal.", settled as the final three's Head of Household begins. An offer of one lapses at the final three, and once the final four's block is set. The final Head of Household's choice under the rules now weighs the jury, a pact that holds and a final three kept, and no longer the threat, strategic value or personality terms. Under the rules houseguests also put the kind to the player and to each other from the final six to the final four, and never to somebody a final two already binds them to.

**Floors:** the Unity-free subset 1483 to 1509 (`FinalChoiceTests`, 26); EditMode adds the same 26; PlayMode unchanged.

**The review before landing (C5 to C8), 2026-10-03.** C5, C6, C7 and C8 had been compiled but never run in Unity's PlayMode when the session that built them stopped at its usage limit, with C9 not yet landed. A cloud session with no Unity took the branch on and reviewed what Unity had not exercised:
- **Finders:** two for each slice, one on its never-run PlayMode tests and one on its runtime and engine code as a player meets it. One more looked at how the four slices met at their merges.
- **Verifiers:** three for each claim, each trying to refute it.
- **Result:** no PlayMode test it could show would fail. Six defects here were confirmed, one of them major, and two more in the save stores' PR #22 (merged above).
- **How each was fixed:** in its own worktree, then reviewed from three sides (completeness, regressions, Unity-side risk) and repaired until no reviewer objected, then landed with a merge commit.

Every fix applies under the commitment rules only; seasons without them digest byte for byte.

- **C5.** A story pact the player was invited into could be grown by a bring-in, which puts the newcomer after the player. Once its earlier members turned and were taken out, the player stood first, and the member order said they founded it.
  - Rename was offered and accepted.
  - The alliances page said "It came together in a story, with Maya.", dated from the week the NPCs formed it.
  - The fix: `AllianceState.playerJoined` (schema 22, no new number) marks the join under the rules and is never cleared. `FinalistRead.FoundedByPlayer`, `EpisodeEngine.PlayerFounded` and `AllianceRead.Formed` read it first, so the page says "Riley brought you in.", dated from the invitation.
- **C6.** The notebook's Votes card and the weekly recap called an ally's word to the pact "a lie" when the ally's ballot later changed, though the member told the truth when they said it.
  - Both now word it through one reader, `KnownBallots.SaidWords`: "told the pact they'd evict Maya Hassan; voted the other way" on the card, and "vote changed" on the recap's one-line row.
  - A lie told to the player's face still reads "a lie". So does an overheard one, until the copy pass the plan defers.
- **C7, major.** A voting bloc broken by the partner's secret ballot counted as a breach to mend. 'Mend fences' appeared, the mend line named "the voting bloc you broke", and every move with that houseguest showed odds 30 points lower. Each of these told the player a ballot they cannot know (decision 4).
  - `Negotiation.BreachesAgainst` and `BreachWords` now count a deal only where the player can know of the breach (`Negotiation.KnownBreach`, as FinalistRead's bloc line does).
  - The chance shown takes the −30 only for those breaches. The roll keeps it for every breach, so no outcome moved.
- **C7.** A price voided by the nomination that broke what it bought was then overwritten by its own verdict, which had been built before the act began. The log said "You no longer owe Maya a safety pact..." and then "Maya broke a safety pact with you.", and wrote a permanent breach.
  - A verdict whose deal an earlier verdict of the same act already ended is now skipped.
  - `VoidThePrice` keeps to the price's own week, as `Negotiation.Voided` reads it.
- **C8.** The note over a conversation's chances, and the Your word page, said the house's reading weighs on "every deal you put to the house". But the plea's 'Make it a deal' and C7's veto for a price never take it, as this plan's own "What it moves" says.
  - This is a copy fix; no odds were touched.
  - The note now ends "...and it weighs on every deal and alliance you propose, but not on a plea or a price for the veto."
  - The page also names a story's choice as something the reading does not weigh on.

**For the owner:**
- **A counter's price struck in free time, decided (2026-10-03, the owner left it to judgment): it stays as built.** When the next week's nominations break what it bought, the price is past its own week, so it is judged by its own rule (broken by whoever broke the deal it paid for) rather than voided. Inside its week (the nominations window, or a same-week replacement) it is voided. Kept because:
  - it is C7's own review rule, that a price is void only for a breach while it bound;
  - for the usual price, a safety pact, the Head of Household's nomination of the player breaks that pact too, so "Maya broke a safety pact with you" is the true line;
  - the log, the state and the page agree, the tests pin it, and no digest moves.
- **Found on the way, for wave C:** a deal struck in free time carries the week that is ending, and binds the next week's Head of Household, nominations and veto meeting only because deals lapse lazily, when that week's campaign opens (`NpcDeals.Expire`). Every free-time deal works that way, not only prices, and a weekly price struck after the vote can bind nothing of its own week. That is worth a rule of its own: a free-time deal is stamped for the week it is meant to bind.
- **Untested wiring:** the Votes card's use of the new words has no test of its own. The helper is tested, and so is the recap row (by its PlayMode check).

**Saves:** a schema 22 save written by the integration branch before `playerJoined` existed no longer loads, as with C7's `linkedDealId`, because the save checks its fields exactly.

**Tell the story session:** `AllianceState.playerJoined` (schema 22). A pact the player was invited into is never theirs to rename, whoever is left in it.

**Evidence:**
- **Unity-free subset:** 1495 to 1501.
- **Digest harness:** both halves pass, and all 54 seasons under the rules count exactly as before.
- **Compile check:** a Roslyn compile of every assembly without Unity adds no errors except the Unity types it cannot see, each of which was checked by hand.
- **Not run:** the Unity suites. The PlayMode tests these fixes change (the recap's vote row, the Your word page) have only been compiled that way.

**Floors:**
- **EditMode, 2242 to 2420:** the full run on the UMA-free copy at ce5b3d9 counted 2410, plus #22's ten.
- **Unity-free subset, 1328 to 1501.**
- **PlayMode:** stays at 863 until C5 to C8's tests have run in Unity.

**B4 as Wave D's D4, leaks and double-dealing, built 2026-10-08** on `claude/d4-leaks` from the lead's 2ea986df (schema 28 landed: `allianceLeakRulesStartWeek`, `story:double-dealt`, the four event kinds). WAVE-D-NPC-PACTS-PLAN §2's slices D4-0 to D4-6, one commit or two each. Everything is keyed to `allianceLeakRulesStartWeek` through `AllianceLeaks.On` - the start week reached, the story's facts (`StoryRules.Bonds`) and the commitment rules - so a season without them plays as it did, roll for roll and line for line. No schema bump, no command kind, no new caption.

| Slice | Commit | What landed |
|---|---|---|
| D4-0 | 3026407d | `AllianceLeaks`: the gate, the odds `min(0.5, 0.05 + 0.03·max(0, m−2) + J)` with the juggling term only on the player's own pacts while they are in the house, the keyed coin `w{week}:leak:{pact}`, which pacts roll, who is caught out, the rival test and the words. `Knowledge.PactOfPair`: standing before ended, one the listener does not know of, then the house's order. `EnableAllianceLeaks`, clamped as `EnableCommitments` is. |
| D4-1 | 753d8856 | One pair, one pact: a story's alliance spread grants and widens only the resolver's pact (the C8 defect). A play's receipt names the pact the spread granted, resolved before the grant; The Secret Alliance reads the resolver. The alliance whisper names everyone ("Word in the house: A, B and C are working together."), and the page dates a card by either form. `AllianceReadTests`' over-grant pin kept as the rules-off case, with a rules-on twin. |
| D4-2 | 82845240, 2eab223a | The listen-in: a pact whose people in the house are exactly the two overheard makes the player a knower (the fact stays private, the memory unchanged) and the line gains " From the way they talked, A and B are working together." Two of a bigger pact, the player's own pact, a known one or a caught listen-in give nothing. `EpisodeEngine.EavesdropLine` is the one builder (X5): with every clause empty it is today's line byte for byte, and D2's act clause slots before D4's sentence. |
| D4-3 | df8eb162 | The weekly leak at the eviction night anchor, after the NPC showmances and before the gossip: each rolling pact's coin, and a landed coin is `MakeKnown(Whispered)` and nothing else - no knower, no line, no id, no season draw. |
| D4-4 | d8405c7b | Double-dealing at both knower paths (the gossip, beside `HeardOfYourWord`, and a story's spread): an ally outside the player's other pact holds the alliance-betrayed grudge (40, 48 while allied) if another member is their rival, or keeps the permanent `story:double-dealt` receipt at −10 (`HeardAbout`); either way one line, kind `double-dealing`, to the player and them. `AllianceRead.Pact.exposures` and the card's "Week 5 · Riley found out about it." The four Wave D kinds' tint and glyph. The enable lines: the director's `StartSeason` at week one, the importer at week + 1, `PortVerification.Season` asserting 1 - D4's start week alone. |
| D4-5 | 6c8f7ef4 | The evidence below, the PlayMode tests, this entry, the floors. |
| D4-6 | (this commit) | The optional last slice. The risk word on each of the player's standing pacts, last on its card: "Risk of word getting out: low." (some, high), the odds as the player can reckon them - who of it is in the house, how many pacts they hold - and never whether it has got out. Your week's "Found out" card: each double-dealing line the player read that week, in its words. |

**Evidence:**
- **Unity-free subset:** 4494 to 4528, all green (`AllianceLeakTests`, 33; `AllianceReadTests`' rules-on twin).
- **Mutations:** each slice's lines were broken one at a time and each failed its test, restored byte for byte: the odds, the week a fact may roll and the resolver's listener term (D4-0); the spread reaching every pact, the receipt resolved after the grant, the play's `AllianceOf`, the whisper's names and the page's second form (D4-1); the exact-pair rule, the grant, a grant that widened the fact, the clause order and the page's listen-in reading (D4-2); the leak at every anchor, after the gossip, for every rolling pact, and with a knower (D4-3); each knower path, the rival test, the allied multiplier, the ally and member checks, the receipt's impact and the card's exposures (D4-4); the risk word reading the fact's visibility, ignoring the juggling or shown for an ended pact, and Your week reading every week or none (D4-6). One survived as equivalent: the "player in the house" check, which the commitment rules' `Allied` already makes.
- **Digests (`CommitmentRulesSeasonDigests`, both halves):** without the commitment rules the 54 seasons digest byte for byte as recorded; under them they move and stay legal as before.
- **`AllianceLeakSeasonDigests` (new, explicit, compiled out of Unity):** the leak rules need the commitment rules, so its rules-off half holds those 54 seasons' digests *under the commitment rules* as 2ea986df played them, and with the leak rules off every one is byte-identical. Its rules-on half plays the same seasons from week one twice, by the digests' busy player and by one who keeps three pacts, every command legal, and checks every step: each leak recomputed from the eviction night's state is its coin (no coin without its leak), each double-dealing line has its receipt or grudge and each receipt its line, no receipt is between two houseguests, and each listen-in sentence taught the player the pact.

| Per season | Rolls | Leaks | Spreads (to the player) | Double-dealing (receipts + grudges) | Listen-ins |
|---|---|---|---|---|---|
| Busy, 6 | 0 | 0 | 0.22 (0.11) | 0 | 0 |
| Busy, 8 | 0.67 | 0.06 | 0.50 (0.17) | 0 | 0 |
| Busy, 12 | 4.39 | 0.22 | 1.72 (0.22) | 0 | 0 |
| Three pacts, 6 | 0.72 | 0.06 | 0.11 (0) | 0.11 (0 + 0.11) | 0 |
| Three pacts, 8 | 2.94 | 0.44 | 2.00 (0.28) | 0.50 (0.44 + 0.06) | 0.06 |
| Three pacts, 12 | 6.67 | 0.56 | 5.78 (0.56) | 0.50 (0.39 + 0.11) | 0 |

  Eighteen seasons a size and player. In all, 24 of 277 coins landed (8.7%, against odds of 5 to 21%); every landed coin leaked. The sixteen-person house is the combined roster's, another lane's, and is not measured here.

**PlayMode, written and not run:** `Alliances_DoubleDealingAndListenIn` - the player's juggled pact shows "Week N · Riley found out about it." and the pact they share does not, a pair heard for what they are shows by its listen-in line, at FontScale 1.0 and 1.2 with every label at least 1.3 times its words and drawn whole; the double-dealing kind tints conflict red. Captures `alliances-double-dealt` and `alliances-overheard`, each with `-large` and `-4x3`. `AllianceLeaks_ASeasonTheDirectorStartsPlaysThem` - both starts play the leak rules from week one, D3's and D2's weeks stay 0, and the rules survive a save. The risk line and the recap's "Found out" card are compiled only: their fixture plays without the commitment rules, and the reader tests pin their words.

**Where the build departs from the plan:**
- **The play receipt is resolved before the grant.** Asked after, a listener who now knows the granted pact is pointed at another one of the same two. The engine keeps each spread's pact as it applies a beat's or a play's effects and passes it to `PlayReceipts.For`; under the leak rules the two-argument form, without it, gives an alliance spread no line, since the first pact of the two the player knows of after the grant can be another one (the review's finding 5; `AllianceReadTests`' rules-on twin now reads the engine's own `RememberSpreadPact`).
- **The listen-in names everyone the card shows**, in the cast's order: for an exact pair that is the two overheard, and a pact with a member gone names them too.
- **`AllianceLeaks.Partners`** names the player's partners in the house, and everybody on the record only where nobody is left.

**Found on the way:** `EpisodeState`'s schema 28 block still says nothing writes or reads its fields; `allianceLeakRulesStartWeek` now is. The file is W28's (WAVE-D §7), so the comment is left for the lead.

**Tell the story session:**
- `story:double-dealt` is now written (by the leak rules' double-dealing, on the one who found out's view of the player), and so is the event kind `double-dealing`; `sighting`, `overheard` and `pact-plan` are still unwritten.
- Under the leak rules the resolver (`Knowledge.PactOfPair`) decides `LeakAlliance` and `SpreadAlliance`: one pact per pair, the receipt names it, and The Secret Alliance's goal reads it; `AllianceOf` in `StoryCatalog.Plays` resolves through it too. The alliance whisper names everyone in the pact.
- `PlayReceipts.For` has an overload taking the pact each spread resolved before its grant. Under the leak rules the two-argument form gives an alliance spread no receipt: a caller that wants one passes the map, as the engine's beats and plays do.
- Under the leak rules an alliance Spread in `ApplyStoryEffect` goes through `SpreadOnePact`, which can log a `double-dealing` line and write a `story:double-dealt` ledger event (two new ids), or add a grudge, in the middle of an arc's effects.
- Facts now get out on their own each eviction night, so the Staged Feud's `AllianceKnowers ≥ 3` fires sooner, The Secret Alliance casts less often, and the emergency meeting may cast more.
- Double-dealing writes alliance-betrayed grudges against the player (48 while allied), which every grudge reader sees.
- The director starts every season under the leak rules; the importer from the week after.

**Floors:** the Unity-free subset 4494 to 4528; EditMode 6937 to 6971 (the same 34); PlayMode 1003 to 1005; UMA unchanged.

## Wave C staged checkpoint — 2026-10-05

These are isolated gameplay-branch implementations, not live integration or native
acceptance. E1's versioned fresh-season economy, all three E3 deliberate target
pickers, and E2's personal-lore/plain-Talk adjustments are committed. E2 open-game
information and public-airing Read entries are now implemented and covered by
77 new pure/Edit cases and nine authored, offline-compiled Play cases. See
`WAVE_C_CONVERSATION_INFORMATION_IMPLEMENTATION.md` at the project root for the
exact changes, evidence hashes, compatibility and remaining limits. Full pure
execution is1836/1836; this is not a native Unity or desktop result.

E2 reply-card trade-offs and the complete no-dominated-verb comparison remain.
E4's player-HoH pitch cards and E5's bounded speech influence follow. The owner
has selected **all four Wave D systems as required**, not optional, for completion:
unified commitments, all-week strategic NPCs, negotiated alliance plans and
deeper leaks/double-dealing. Final desktop performance is **1920x1080 at60FPS on
the GTX1060**; old900p measurements do not satisfy that gate. Full combined-source
native suites, saves/migrations, shipping build, real-asset/visual/accessibility
review and human acceptance are still separate open gates. Nothing in the staged
checkpoint authorizes publishing, connecting accounts or overwriting live edits.

## E2 reply trade-offs staged checkpoint - 2026-10-05

The reply-card portion is now implemented above48e1dd on the isolated gameplay
branch. Information replies earn bounded private assessments; escalation offers
a real, consent-dependent safety deal for this week; other answers retain their
distinct trust, retaliation or promise consequences. All three UI surfaces show
the terms and use render/load/revision-scoped callback authority. Old-season
simulation and descriptions remain unchanged. No schema or command expansion.

See `WAVE_C_REPLY_PAYOFFS_IMPLEMENTATION.md` for every material change, source
boundary, hashes and limitations.89 new pure/Edit cases pass; full pure execution
is1925/1925. Ten new Play tests compile but have NOT run in Unity. Final offline
check is5/5 NoUMA assemblies, not native or full UMA acceptance. The720-outcome
reply comparison is a first-opportunity trade-off witness, not whole-game balance.

E2's complete all-verb comparison remains open. E4/E5 and all four REQUIRED Wave D
systems follow. Exact-candidate integration, native saves/suites, separate desktop
build, coherent real assets,1080p60 and human E1-E5 acceptance remain uncompleted.

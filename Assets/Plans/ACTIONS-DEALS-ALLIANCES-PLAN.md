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

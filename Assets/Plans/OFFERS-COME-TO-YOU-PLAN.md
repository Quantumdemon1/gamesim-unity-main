# Offers come to you: houseguests bring their asks as a dialogue

Plan, 2026-10-30, revised after review, then checked claim by claim against origin/main 37416049 (49 corrections upheld after an adversarial pass, 8 refuted). The owner answered its five questions the same day (their defaults). Slices 1 to 3 are built on claude/offers-come-to-you (5adef5bf and 1aa78549, 0fd5e837 and a26186ae, 8a6bd311), each adversarially reviewed and fixed; their EditMode and dotnet proofs pass, and their PlayMode tests are compiled but not yet run in Unity. The three PlayMode tests of section G's "Also in slice 1" list that were missing (`ComesToYou_SceneWaitingDoesNotStallAVisitInFlight`, `ComesToYou_FollowingSomeoneElseCancelsWithoutAMiss`, `ComesToYou_APullYieldsAfterItsTime`) are now in, compiled but not yet run in Unity. Slices 4 and 5 are not built.

**The owner, 2026-10-30:** "Something else that needs to be fixed, when someone comes to you with a deal, you have to look through the menu to find it, but it should just pop up as dialogue like an RPG as an offer or have them approach you"

**Evidence base**
- Unity: D:/GamesimAllStars at origin/main 37416049, read with `git show` and `git grep`. Nothing was compiled or run.
- Path prefixes: `S/` = `Assets/Gamesim/Simulation/`, `R/` = `Assets/Gamesim/Runtime/Episode/`, `H/` = `Assets/Gamesim/Runtime/House/`, `T/` = `Assets/Gamesim/Tests/`, `PV` = `R/PortVerification*.cs`.
- Web reference: D:/gamesim-web, cited as `web:`.

**This is the base for the storyline plan.** The storyline plan (houseguests coming to the player with conversations and covert asks) builds on this plan's queue, visit, dialogue frame and source contract (§C.4). It does not build beside them.

---

## TL;DR

**A walk-over already exists, but it rarely reaches the player.**
- The houseguest walks to within 1.8 m (H/HouseMeetingCoordinator.NpcInvitation.cs:36, 47-97) and shows a bubble with "Hear their offer" (R/HouseNpcInvitationBubble.cs:20).
- The visit ends if the player drifts 0.75 m, changes room, or 25 s pass from dispatch (H/HouseMeetingCoordinator.NpcInvitation.cs:37-38, 99-112).
- Any miss sets the offer aside for the rest of the phase (R/EpisodeDirector.NpcInvitations.cs:55-77). After that it is reached only through a conversation's "Deals and promises": from the topics wheel, or from free time's "Pitch a deal" tile, which opens the conversation straight on that category (R/EpisodeDirector.FreeTimeScreen.cs:498-503; R/EpisodeDirector.ConversationNavigation.cs:39-40). There it sits under the Bargain heading, in the deal table (`DealPanel` inside `FoldedDealPanel`, R/EpisodeDirector.ConversationGroups.cs:194, 240; R/EpisodeDirector.Conversation.cs:476-488). A veto ask is the one exception: it is also answered on the player's own veto decision, on the offer board or the `VetoOffers` rows (R/EpisodeDirector.VetoMeeting.cs:220-221; R/EpisodeDirector.Strategy.cs:164-210).
- Even when Hear works, the page looks like a menu. The category heading, standing, actions left and "Back to topics" all come before "THEIR OFFER" (R/EpisodeDirector.ConversationNavigation.cs:151-160, 221-235), and the houseguest's own words are dropped.

**The fix:**
1. **One pure queue.** `S/ComesToYou.cs` lists what houseguests have put to the player. Its order reveals nothing about how warm they are.
2. **One generic visit.** A director-side visit carries a `ComesToYou.Ask` of any source. The deal sources are its first users.
3. **One dialogue frame, in three ways in:**
   - **Walk-over.** The houseguest comes to you, says their line, and the dialogue opens by itself.
   - **Raised.** You talk to someone who has an ask for you, and the conversation opens on it.
   - **Card (slice 2).** When they cannot come, the dialogue pops up where you stand.
4. **The simulation does not move.** There is no new command, saved field, draw or log line. Answers use the existing `RespondToDeal` controls under their captions.
5. **The new behaviour is off for tests and verification.** `AsksEnabled` is set false by the three PlayMode fixtures that bind a director and by PortVerification, so every existing suite and the audited walk run as at origin/main, including the invited view.

---

## A. What the player experiences

**The normal case.** Offers are put to the player once a week (S/NpcDeals.cs:301-305), normally as the campaign opens (S/EpisodeEngine.cs:422-423). The call as the social window opens (:483) files only when that week's campaign filed none, because the week turns only as the social window closes (:309). An unanswered offer stands until next week's campaign opens (one about the veto, until the veto is decided), so it waits through eviction night, the social window, and next week's HoH, nominations and veto. The player closes the campaign screen and walks around.
1. **Maya comes over.** The house has to be free for 2 s and the player still for 1 s. Then Maya stops what she is doing and walks into the player's room, stopping 1.8 m away. From slice 3 she also turns to face the player.
2. **She speaks.** Her line shows over her head, using the existing bubble and its opening line (S/NpcInvitation.cs:72-93), with **Hear their offer** and **Not right now**.
3. **The dialogue opens by itself** once all of these hold:
   - the bubble has been on screen for 1.2 s;
   - the player has stood still for 1 s;
   - no overview, emote menu, cast card or Nearby card is up.

   The camera takes the conversation two-shot. Under reduced motion the camera does not move, as for every conversation today (H/HouseCameraRig.cs:298). Maya is on screen anyway, because the bubble had to be visible, and a visit only starts and continues while the player is in frame (`PlayerInFrame`, §C.2).
4. **The dialogue**, top to bottom:
   - the eyebrow "MAYA PULLS YOU ASIDE";
   - her portrait and name, with her pronouns under them;
   - her standing and mood as a line of body text;
   - her line in a speech card (the whisper bubble for private kinds);
   - the heading "THEIR OFFER";
   - the terms in plain words (`DealSentence`, R/EpisodeDirector.Conversation.cs:641);
   - one line saying how long the offer stands;
   - **Accept this offer** · free, **Turn this offer down** · free, **Think it over**, **Back to topics**;
   - when others wait: "2 more houseguests want a word."
5. **No stray press commits anything.**
   - The keyboard opens on **Think it over**, so an Enter or A press that is already held commits nothing.
   - Pointer presses are held off the panel for 0.45 s, so a click aimed at the floor cannot land on Accept.
6. **After an answer**, today's exchange plays: the player's words, then her reply (R/EpisodeDirector.ConversationExchange.cs:35-66). The conversation then carries on as today.
7. **Think it over** commits nothing and closes the conversation.
   - The offer stays listed: on the rail badge, on the objective line (S/WaitingOnYou.cs:292-305), on the Deals petal's waiting tag (R/EpisodeDirector.ConversationNavigation.cs:138-144), and on the strategy footer when the next advance would let it lapse (R/EpisodeDirector.WaitingOnYou.cs:23-28). A nominee's veto ask outside the role moments also stays on the veto meeting's rows (R/EpisodeDirector.Strategy.cs:164-175).
   - Maya brings it up again in a later phase, at most twice unasked.
   - Talking to her later this phase opens on the topics wheel, not on the offer again.

**Raised (you went to her).** You click Maya, press E beside her or use a talk spot while she has an offer you have not seen this phase. The conversation opens on the dialogue instead of the topics wheel.
- The eyebrow is "MAYA HAS AN OFFER FOR YOU", the words the objective line already uses (S/WaitingOnYou.cs:300).
- It has no **Think it over**. **Back to topics** is the way past it, and the keyboard opens there.
- It is raised once a phase per offer.
- It is not raised in phases where Maya would only greet you.
- It is not raised when you came from her free-time screen to ask for information or pitch a deal. The conversation opens on that category as today, and the Deals page already shows her offer first.

**Card (slice 2).** When Maya cannot come, the same dialogue opens as a card where the player stands, under the eyebrow "A WORD IN PRIVATE". She cannot come when she is busy, has had no clear route of her own for 10 s, or the walk has missed twice. If the player stands for 10 s where no visitor can stand beside them, the first listed ask comes as the card. After an answer the card shows the player's words and her reply, then **Back to the house**.

**Where the player is when an ask is due:**

| Player is… | What happens |
|---|---|
| In any panel: episode screen, notebook, settings or pause, diary, activities, recap, report | Nothing starts and nothing opens. Every panel counts in `IsHouseUnderChrome` (R/EpisodeDirector.HouseOverlays.cs:24). Asks start 2 s after the last one closes. |
| Talking to someone else | Waits; a conversation is a panel. |
| Talking to the asker | The conversation opens on the ask (Raised), once per phase. |
| Walking (click-to-move, heading to a person or the station; WASD pans the camera, H/HouseCameraActions.cs:245-248) | Nothing starts until the player has stayed within 0.1 m of one spot for 1 s, whatever the frame rate. The existing gates already hold back errands (R/EpisodeDirector.NpcInvitations.cs:41-42). Walking away mid-visit is a miss: it is retried 8 s later, not set aside at once as today (R/EpisodeDirector.NpcInvitations.cs:63, 75). After a second miss, slice 1 walks it over no more this phase but does not set it aside, so talking to the asker still raises it. From slice 2 the card opens instead. |
| Using furniture they started from the house: seated, in bed, in the pool or tub, at the stove | Nothing starts and no card opens. `AskMayStart` includes `!IsPlayerHouseActivityActive` through `NpcInvitationHouseAvailable` (R/EpisodeDirector.NpcInvitations.cs:42), and the coordinator refuses a player with an activity owner (H/HouseMeetingCoordinator.NpcInvitation.cs:53-54, 107-108). If the player sits down while a visit is under way, the visit is cancelled (§C.2 step 7): not a miss, and the ask is not set aside. Asks wait until the player gets up; then the 2 s settle and 1 s still times apply. The offer stays on the rail badge, the objective line and the Deals petal. A click on the asker gets the player up and opens the conversation on the ask (Raised; R/EpisodeDirector.HouseActivities.cs:253-262). E only gets them up (R/EpisodeDirector.cs:538-539). Activities started from the House Activities menu are panels (first row). See owner question 4. |
| In another room | The asker crosses the house; the lease always ends in the player's room. Without a route of her own for 10 s, the card takes over (slice 2). In slice 1 she walks it over no more this phase, and it is still raised when you talk to her. A spot of the player's where no visitor can stand charges no asker (§C.2 step 9). |
| A ceremony, competition, opening, staged scene, walk-out or tour | Already gated (R/EpisodeDirector.NpcInvitations.cs:37-44). |
| Overview, map only (the briefing put away with "Show the map", or opened through `ShowOverview`) | The visit may start and the bubble may show, as today (T/PlayMode/EpisodePlayModeTests.NpcInvitationFlow.cs:43-56). The dialogue and card wait until the overview closes, because the overview is closable chrome (R/EpisodeDirector.Controls.cs:186). |
| Overview with its briefing (what the rail's Overview row and the Overview shortcut open; R/EpisodeDirector.Overview.cs:52-64) | The briefing counts in `IsHouseUnderChrome` (R/EpisodeDirector.HouseOverlays.cs:24), so it behaves like a panel. Nothing starts, and an ask already under way is cancelled and its bubble hidden (R/EpisodeDirector.NpcInvitations.cs:37-38, 54). |
| Following another houseguest, or the camera panned away from the player | No visit starts, and one in flight is cancelled, not missed (`PlayerInFrame`, §C.2). It starts again 2 s after the player is back in frame. The camera is never moved for an ask. From slice 2 the card waits for the same condition. |
| Emote menu or cast card open; a storm-off or an embrace under way | New visits wait, and so do auto-open and the card. |
| Watching two houseguests talk, with "Listen in" on offer | New visits, auto-open and the card wait while the Nearby card's own condition holds (R/EpisodeDirector.NpcSocial.cs:85-90). If the lease runs out while auto-open is held this way, the visit is cancelled, not missed (§C.2 step 8). |
| A story scene ready to stage | The scan, and so new visits and the card, yields until it is staged (R/EpisodeDirector.Story.cs:576-616). A visit in flight still keeps the floor, as today (:608): it auto-opens even while the scene waits, and the scene stages once the conversation closes (`TickSceneStage` waits on `IsPanelOpen`, Story.cs:600). |
| Story Pull on screen | The Pull and the bubble never share the screen (D18): `TickStoryPull` hides the Pull while a bubble is visible (R/EpisodeDirector.Story.cs:507-509), and the conversation panel hides it too; either way it comes back undeclined. The Pull has no timeout: it stays up until it is answered or its beat lapses at an Advance (Story.cs:389-414; S/Story/EpisodeEngine.Story.cs:256-268). Beats are filed at the same Advances as offers (S/EpisodeEngine.cs:423-425, 477-483), so under D18's "whichever is up keeps the floor" an ignored Pull keeps every ask in the menu for the whole phase. **The owner's answer to question 5 (2026-10-30, the default; a delegated call on D18, confirmed by the owner):** while `AsksOn`, a Pull holds new asks back only until it has shown for `AskPullYieldSeconds` (20 s) of free, still time; the clock restarts when the Pull's key changes. After that the scan may start a visit beside it, and the Pull hides once the bubble shows. With `AsksOn` false, `NpcInvitationHouseAvailable` is unchanged, so the legacy invitation and `HudStatus_TheInvitationBubbleAndTheStoryPullNeverShareTheScreen` run as at origin/main. |
| Several asks at once | One at a time, in public order, with 20 s of free-house time between them. |

---

## B. The ways in, and the switch

| | Walk-over | Raised | Card (slice 2) |
|---|---|---|---|
| **When** | The asker can be reserved (`TryReserveNpcInvitation`) and the house is free and the player still | The player opens a conversation with someone who has an unseen ask, where the conversation can show it (R/EpisodeDirector.NpcInvitations.cs:121-124) | `AskWalkOvers` is false; or its asker refused the reservation for 10 s of free, still time for reasons of their own (busy, no route); or the player stood 10 s where no visitor fits (the first listed ask only); or its walk-over missed twice |
| **Host** | The conversation, opened through `TryOpenNpc` with the ask named | The conversation | New panel state `askCardOpen`, built like the scene card (R/EpisodeDirector.Story.cs:739-754) on the Stage layout |
| **Opens** | By itself (bubble on screen 1.2 s, player still 1 s, no closable chrome or Nearby card). Hear (its button, or E) opens it sooner. A click on the asker while their bubble is up hears it too. `SelectHouseguest` (R/EpisodeDirector.Conversation.cs:144-150), after its guards and before the talk-spot lines, runs `if (AsksOn && VisibleNpcInvitationSpeaker() == npc && TryHearVisibleNpcInvitation()) return;`, the route E takes (R/EpisodeDirector.cs:542-543, 628-629). Without that line the invitation lease makes `TryReserveTalkSpot` report them busy (H/HouseMeetingCoordinator.Talk.cs:76-77) and `TryOpenNpc`'s `ClosePanels` cancels the visit before `OpenOnAsk` runs (:244), so a click would open the Raised dialogue. The cast strip's Talk and a direct `TryOpenNpc` still open Raised, as PlayMode test 4 expects. | With the conversation | By itself, at the next free, still moment with no closable chrome |
| **Non-committal way out** | **Think it over**, which closes | **Back to topics** | **Think it over**, or Escape |
| **Counts as a presentation** | Yes | No | Yes |
| **On failure** | A miss: count +1, retry after 8 s | Not applicable | Not applicable |
| **After an answer** | The existing exchange, then the conversation | The same | The reply page (both lines), then **Back to the house** |

**Reduced motion.** The walk-over stays on. It moves a houseguest, not the player or the camera; talk spots are off for a different reason, because they move the player (R/EpisodeDirector.TalkSpots.cs:65).

**The switch.**

```csharp
public bool? AsksEnabled { get; set; }                      // null: the default
private bool AsksOn => AsksEnabled ?? !Application.isBatchMode;
```

- **Default.** On in the editor and the standalone player, off under `-batchmode`.
- **Off where audited behaviour must hold.**
  - Every PlayMode fixture that binds a director sets it false right after the bind: T/PlayMode/EpisodePlayModeTests.cs:411 (`ReloadEpisode`, used by every EpisodePlayModeTests partial), T/PlayMode/EpisodeDurableTransactionTests.cs:51 (its own SetUp scene load) and T/PlayMode/HousePrototypeTests.cs:136 (the bootstrap build-index-0 test, whose variable is `episode`). Suites therefore behave the same in the editor and in batch runs.
  - PortVerification sets it false once the bootstrap's director is ready (R/PortVerification.cs, after the ready check at :92-93, never inside the :90-91 wait loop, where `director` can still be null), and again where the look sheet and the season checks re-bind it (R/PortVerification.LookSheet.cs:101, R/PortVerification.Season.cs:123). That covers the batch walk, the windowed look sheet and profile runs, which are not batch mode (R/PortVerification.LookSheet.cs:92-95; R/PortVerification.cs:163-164, 255), and the windowed creator route, which reuses the same director (R/PortVerification.cs:96; R/PortVerification.Creator.cs:88-90).
- **Tests opt in** with `director.AsksEnabled = true` after `ReloadEpisode()`.
- **A change mid-session** cancels any visit in flight and resets both models' state.
- **With it off, everything is as at origin/main.** That covers today's scan, phase deferral and invited view (R/EpisodeDirector.ConversationNavigation.cs:193, 221-235), with no auto-open, no raise, no card and no change to the busy or walk-in sets. Nothing behavioural is ungated. Slice 3's bubble presentation fixes (localised name, line and captions with the raw line kept for `CurrentText`; 1.3× text boxes; placement on the director's `ViewCamera`, falling back to `Camera.main` when it is unset) also apply to the legacy invitation, which draws the same `HouseNpcInvitationBubble` (R/EpisodeDirector.NpcInvitations.cs:81-86). Its captions, control identities and `CurrentText` are unchanged.

---

## C. One reusable surface: queue, visit, frame

### C.1 Reader: `S/ComesToYou.cs` (new, pure)

```csharp
namespace Gamesim.Simulation {
public static class ComesToYou {
    public enum Source { Offer, VetoAsk }            // slice 4 adds Card, Pitch; slice 5 Story
    public sealed class Ask {
        public Source source;
        public string recordId, askerId, type, line, lapseLine;
        public int week;
        public WaitingOnYou.Lapse lapses;
        public bool lapsesOnAdvance, whisper;
        public bool IsCurrent(EpisodeState s);       // per source; the deal sources delegate to a private NpcInvitation
    }
    public static IReadOnlyList<Ask> Read(EpisodeState s);
    public static Ask Find(EpisodeState s, string recordId);
    public static int OtherAskers(IReadOnlyList<Ask> asks, string currentAskerId, Func<Ask, bool> canStillCome);
}}
```

**What it reads.**
- The items of `WaitingOnYou.Read(s)` (S/WaitingOnYou.cs:81-115) whose kind is not Card. That reader already applies presence, answerability and the cut-off from the final Head of Household on.
- Each item is joined by record id to `NpcInvitation.Available(s)` (S/NpcInvitation.cs:42-49) with a matching proposer. If two share an id, the first wins.
- **Except the veto board's asks under the role moments.** `EpisodeEngine.RolesOn(s) && RoleOffers.IsBoardAsk(s, deal)` (S/Story/RoleOffers.cs:151) are not listed. Their no and their silence carry the card tone's price (S/Story/EpisodeEngine.RoleMoments.cs:88-92, 145-149). The board says so (S/Story/RoleOffers.cs:66) and orders them publicly (:122-126). A dialogue that opens on "Think it over" would present that silence as free. They stay on the board (§D).

**What it sets.**
- `line` is `invitation.OpeningLine`.
- `whisper` is true for `FinalTwo`, `FinalThree` and `InformationSharing`.
- `lapseLine` uses `earlier = deal.expiresWeek < s.week`, as S/WaitingOnYou.cs:184-186 does:

| Lapse | Line |
|---|---|
| VetoDecision | "It stands until the veto is decided." |
| CampaignOpens, earlier | "It stands until this week's campaign opens." |
| CampaignOpens, not earlier | "It stands until next week's campaign opens." |
| Open | null |

Outside the role moments an unanswered offer is inert (S/NpcDeals.cs:283), so these lines are the whole cost of thinking it over.

**Public order.** `NpcDeals.Pending`'s order is never used. Ask ids follow the order asks were filed, which is warmest first (S/Story/RoleOffers.cs:13-16), and Pending sorts by id last (S/NpcDeals.cs:491-497). The sort is:
1. `lapsesOnAdvance` first;
2. `week` descending;
3. `NpcDeals.Urgency(type)` descending (S/NpcDeals.cs:472);
4. for `VetoAsk`, the asker's index in `s.nominees`, everyone else after;
5. `askerId` ordinal;
6. `recordId` ordinal.

**`OtherAskers`** counts the distinct askers other than the current one whose asks pass `canStillCome`. The director passes a test for "current, not set aside, under the per-record cap, conversation available", and the count is 0 once a phase or week cap is reached. In slice 1 that test also excludes card-due records, which no longer come; from slice 2 it includes them, because they come as a card.

**Purity.** It writes, rolls and logs nothing, as WaitingOnYou does (S/WaitingOnYou.cs:14-16). No existing Simulation file changes. Add a hand-written two-line .meta.

### C.2 Visit and scheduler: `R/EpisodeDirector.ComesToYou.cs` (new partial)

The AsksOn path lives here in full. It has its own visit fields and its own tick, and it reuses the coordinator's lease API and the one `HouseNpcInvitationBubble`. `TickNpcInvitations` (R/EpisodeDirector.NpcInvitations.cs:46-106) is not edited.

**Constants** (all times are free-house seconds unless stated):

| Constant | Value | Meaning |
|---|---|---|
| `AskSettleSeconds` | 2 | Continuous free-house time before a new visit starts |
| `AskStillSeconds` | 1 | Player still |
| `AskStillRadiusMetres` | 0.1 | Stillness is the time spent within this radius of an anchor point, so it does not depend on frame rate |
| `AskSpeakBeatSeconds` | 1.2 (unscaled) | Time the bubble must be on screen before auto-open |
| `AskGapSeconds` | 20 | After a presentation, a Think it over or a closed raise. Web uses 30 s (web: src/hooks/useNPCSocialEngine.ts:39); ours also spends time walking. |
| `AskRetrySeconds` | 8 | After a miss: both the record's retry and the next scan |
| `AskerGapSeconds` | 120 | Per asker (web: :40) |
| `AskScanSeconds` | 1 | Scan interval |
| `AskMissesBeforeCard` | 2 | Misses before the card (slice 1: no further walk-over this phase; still raised) |
| `AskUnreachableSecondsBeforeCard` | 10 | Per record: time its asker refused the reservation for reasons of their own (`Busy`, `NoRoute`, `Asker`) before the card (slice 1: no further walk-over this phase; still raised). Slice 2's `askSpotBlockedFor` uses the same 10 s for the player's spot. |
| `AskPresentationsPerRecord` | 2 | Unasked presentations of one record per session |
| `AskPresentationsPerPhase` | 3 | |
| `AskPresentationsPerWeek` | 6 | |
| `AskFrameAllowanceSeconds` | 0.25 (unscaled) | The most one frame adds to any ask clock, the cap `CharacterPresentation.AssemblyFrameTime` uses (Assets/Gamesim/Runtime/Presentation/CharacterPresentation.cs:241-245) |
| `AskPullYieldSeconds` | 20 | A showing Story Pull holds new asks back this long, then yields (owner question 5: the default) |

The three caps are copied into instance fields (`askRecordCap`, `askPhaseCap`, `askWeekCap`) so a test can lower them with the private-write helper.

**Public reads for tests:**
- `AsksAreOn`, `IsAskCardOpen`, `AskMissCount(id)`, `IsAskSetAside(id)`.
- `CurrentAskRecordId`, `CurrentAskerId` and `CurrentAskWay` (None, WalkOver, Raised or Card) report the ask the player is facing, checked in this order:
  1. the card (`askCard.recordId`, `askCard.askerId`, way Card) while `askCardOpen` (slice 2);
  2. otherwise the in-person dialogue (`askDialogueRecordId`, `askDialogueWith`, `askDialogueWay`) while `askDialogueWith != null`;
  3. otherwise the visit in flight (`askNow.recordId`, `askNow.askerId`, way None) while `askNow != null`;
  4. otherwise null, and way None.
  They must not read only `askNow`. `HearAsk` step 2, and the `ClosePanels` inside `TryOpenNpc` through `CancelNpcInvitation` (R/EpisodeDirector.cs:702), both cancel the visit before `OpenOnAsk` runs, so after an open these reads come from the dialogue fields.
- `CurrentNpcInvitationRecordId` and `CurrentNpcInvitationNpcId` (R/EpisodeDirector.NpcInvitations.cs:29-30) also report the ask visit.

**State.** Instance fields only, never statics (memory: statics outlive play sessions).

| Field | Holds | Cleared |
|---|---|---|
| `asksWereOn` | `AsksOn` last frame | — |
| `askNow`, `askVisit`, `askCoordinator`, `askView`, `askLoad`, `askNextPresentation` | The visit in flight (an `Ask` of any source) | `CancelAskVisit` |
| `askSpokeFor` | Seconds the bubble has been visible this visit, accrued every frame (step 8) | When the bubble hides; `CancelAskVisit` |
| `askOpenWith`, `askOpeningFor` | The ask a `TryOpenNpc` should open on; the asker held out of pairing while it opens | `finally` around the open |
| `askClock`, `askFreeRun` | Free-house clock, and continuous free time | On load; `askFreeRun` whenever not free |
| `askStillAnchor`, `askStillFor` | Stillness | When the player leaves the radius or `!player.HasArrived` |
| `askNextAt` | When the next scan may start a visit (free clock) | — |
| `askSetAside` | Records the player has seen or put off this phase. Only `DeferAsk`, `OpenOnAsk` and, in slice 2, `OpenAskCard` and `EndAskCard` add to it | On a session, week or phase change |
| `askMisses`, `askRetryAt`, `askUnreachable` | Per record | On a phase change |
| `askPresentations` | Per record, session-scoped | On load |
| `askerNextAt` | Per asker | On load |
| `askPhaseShown`, `askWeekShown` and their keys | Cap counters | When the key changes |
| `askDialogueWith`, `askDialogueRecordId`, `askDialogueWay`, `askDialogueFirstRender` | The in-person dialogue on screen and the way it opened | When the conversation leaves it (`BackConversationNavigation`'s category clear, or the offer gone) or closes |
| `asksCache`, `asksCacheSource` | `ComesToYou.Read(projected)`, read only through `CurrentAsks()`, which re-reads whenever `!ReferenceEquals(asksCacheSource, projected)` | On load (`ResetAsksForLoad` sets `asksCacheSource = null`) |
| `askCardOpen`, `askCard`, `askCardReply`, `askCardFirstRender` | The card on screen (slice 2) | `EndAskCard` |
| `askSpotBlockedFor` | Slice 2: free, still seconds the player's own spot has refused every visitor (`PlayerSpot`), one counter, not per record | With the stillness anchor; on load |
| `askCardSettle` | Slice 2: free-house seconds with no closable chrome up. It is the card's settle (`AskCardMayOpen`, §C.2 step 9) | Whenever `!AskMayStart` or `ClosableChromeUp`; on load |
| `askPullHeld`, `askPullKey` | Free, still seconds the current Story Pull (`pullNow?.Key`) has shown (owner question 5: the default) | On load; when the Pull's key changes |
| `askPlayerYaw`, `askAskerYaw`, `askPlayerFacing`, `askAskerFacing` | Slice 3: the facings an ask set (NaN when none), and the two bodies it set them on, so `ClearAskFacing` touches only those | `ClearAskFacing`; `ResetAsksForLoad` |

`CurrentAsks()` is the only reader of `asksCache`. The scan (step 9), `OpenOnAsk`'s Raised pick, and the `OtherAskers` input in `RenderAskDialogue` step 5 and in `AskCard` all call it at the moment of use. Never refresh it in the scan only, as `TickNpcInvitations` does (R/EpisodeDirector.NpcInvitations.cs:93-96). `TryOpenNpc`'s `PauseNpcSocialForPanel` flush commits NPC ticks and calls `Project()` just before `OpenOnAsk` (R/EpisodeDirector.NpcSocial.cs:392-396, 375). A walk keeps the gated scan from running (`headingToNpcId` clears `NpcInvitationHouseAvailable`), and the scan stops once a cap is reached, while raises are not capped.

**Gates.**

```
AskHouseAvailable = NpcInvitationHouseAvailable without its hud.PullShowing term    // R/EpisodeDirector.NpcInvitations.cs:37-44
                    && !(hud != null && hud.PullShowing && askPullHeld < AskPullYieldSeconds)   // owner question 5, answered 2026-10-30
AskMayHold  = AskHouseAvailable
              && !emoteMenuOpen && castMenuFor == null           // R/EpisodeDirector.Controls.cs:186
              && StormingId == null && CurrentEmbrace == null    // StormOff.cs:60, Embrace.cs:34
              && !NearbyOfferUp
AskMayStart = AskMayHold && !SceneStageWaiting && PlayerInFrame   // the clocks and the scan, so new visits and slice 2's card
AskMayOpen  = AskMayHold && !ClosableChromeUp && askStillFor >= AskStillSeconds   // auto-open of the visit in flight; a waiting scene does not hold it
PlayerInFrame    = the player's head point (player.transform.position + 1.7 m up) has viewport z > 0 and x, y in [0,1] on cameraRig.ViewCamera; worked out once a frame
NearbyOfferUp    = !string.IsNullOrEmpty(ObservedNpcConversation) && !(npcCaption != null && npcCaption.IsReleasing) && CanListenIn      // TickNearby's own test without its !IsPanelOpen, which AskHouseAvailable already implies through !IsHouseUnderChrome; NpcSocial.cs:85-90. One property, which TickNearby then uses too
SceneStageWaiting = stagedBeat wanted (Story.cs:589-591) && sceneStagedBeat != stagedBeat.id
                    && !npcMeetings.HasSceneStage && npcMeetings.CanBeginSceneStage
NpcVisitInFlight = npcInvitationNow != null || askNow != null
```

A waiting scene holds new visits and the card, never a visit in flight: Story.cs:608 (`NpcVisitInFlight`) holds the scene until the visit is heard, deferred or cancelled, and the scene stages once the conversation closes (`TickSceneStage` already waits on `IsPanelOpen`, Story.cs:600).

**`TickAsks()` replaces the call at R/EpisodeDirector.cs:454.**
1. If `AsksOn != asksWereOn`, cancel any visit (`CancelNpcInvitation()`), call `ResetNpcInvitationsForLoad()` and `ResetAsksForLoad()`, and store the new value.
2. If `!AsksOn`: `TickNpcInvitations(); return;`
3. On a session, week or phase change: `CancelAskVisit()` and clear the per-phase sets.
4. Update stillness. Reset the anchor and `askStillFor` when the player is more than `AskStillRadiusMetres` from the anchor or `!player.HasArrived` (H/HousePlayerController.cs:100-111). Otherwise add `AskFrameTime`, which is `Mathf.Clamp(Time.unscaledDeltaTime, 0f, AskFrameAllowanceSeconds)`, the cap `CharacterPresentation.AssemblyFrameTime` uses (Assets/Gamesim/Runtime/Presentation/CharacterPresentation.cs:241-245). With Run In Background off, a window out of focus stalls the player loop, and unscaled time is not capped by `maximumDeltaTime` (Assets/Gamesim/Runtime/Presentation/MusicFade.cs:50), so an uncapped stall would be credited in one frame. The update is a pure static helper (position, `HasArrived`, dt), so EditMode test 12 can drive it. A walker leaves 0.1 m within 0.05 s at 2.2 m/s (H/HousePlayerController.cs:44) at any frame rate.
5. Advance `askClock` and `askFreeRun` by `AskFrameTime` while `AskMayStart`; reset `askFreeRun` when it does not hold. Every Ask* time, the stillness timer, the bubble beat and the lease (H/HouseMeetingCoordinator.NpcInvitation.cs:94, 103) run on unscaled time, as the legacy scan does (R/EpisodeDirector.NpcInvitations.cs:71-72, 92-93). While `hud.PullShowing`, add `AskFrameTime` to `askPullHeld` when `askStillFor >= AskStillSeconds` and `NpcInvitationHouseAvailable` holds apart from its Pull term; reset it when `pullNow?.Key` changes (owner question 5: the default).
6. If `askDialogueWith != null` and the conversation has closed or is with someone else: set `askNextAt = askClock + AskGapSeconds`, then clear the dialogue fields, `askDialogueWay` among them.
7. If `!AskHouseAvailable`, or a visit is in flight and `!PlayerInFrame`: `CancelAskVisit()` and return. This is a cancellation, not a miss. The scan's own `PlayerInFrame` gate (through `AskMayStart`, which resets `askFreeRun`) stops a re-dispatch until the player has been back in frame for `AskSettleSeconds`. Never move the camera for an ask: a follow is a shot the viewer asked for (H/HouseCameraRig.cs:374-376), and `ClearSubject` does not spring back (:439).
8. **Visit in flight** (at 10 Hz, as :67-69):
   - A changed load, `!askNow.IsCurrent(projected)`, a lost conversation gate or a changed coordinator only cancels.
   - A released or invalid lease (`askVisit.Released`, `!NpcInvitationValid`) calls `RecordAskMiss`, with two exceptions that only cancel (`askRetryAt[id] = askNextAt = askClock + AskRetrySeconds`, then `CancelAskVisit()`, no miss):
     - the auto-open was already due (`askSpokeFor >= AskSpeakBeatSeconds`) and the player was still within the radius (`askStillFor >= AskStillSeconds`), so a gate the player did not raise (Nearby, storm-off, embrace) held the visit until its 25 s lease ran out (H/HouseMeetingCoordinator.NpcInvitation.cs:38, 103). The release check runs before the bubble update, so `askSpokeFor` still holds its last value;
     - the frame's `Time.unscaledDeltaTime` exceeds 1 s: the lease ran out during a stalled player loop.
   - On `NpcInvitationCanSpeak`, show the bubble with the source's captions (slice 1: the deal captions, unchanged) and the callbacks `HearAsk` and `DeferAsk`. Accrue `askSpokeFor` every frame, not on the 10 Hz step: in the visit-in-flight branch, after the immediate cancellation checks and before the `askNextPresentation` throttle, add `AskFrameTime` while `NpcInvitationIsVisible`. The bubble's visibility changes only on a 10 Hz Show or Hide, or on a cancel, so the check is consistent. Reset `askSpokeFor` to 0 on the step that hides the bubble, and in `CancelAskVisit`. Never add one frame's delta per throttled step: that would stretch the 1.2 s beat to about 7 s at 60 fps, and past the 25 s lease at batchmode frame rates.
   - Once `askSpokeFor >= AskSpeakBeatSeconds && AskMayOpen`, call `HearAsk`. This check stays on the 10 Hz step, so it fires at most 0.1 s late. A waiting scene never holds it (`AskMayOpen` has no scene term).
9. **Scan** (at `AskScanSeconds`), when `AskMayStart && askFreeRun >= AskSettleSeconds && askStillFor >= AskStillSeconds && askClock >= askNextAt` and no cap is reached:
   - Walk `CurrentAsks()` in order. Skip any ask that fails one of these: `IsCurrent`; not in `askSetAside`; under `askRecordCap`; `askRetryAt` passed; `askerNextAt` passed; `NpcInvitationConversationAvailable(projected, askerId)`.
   - **Card due** (misses ≥ 2, or `askUnreachable[id]` ≥ 10, or slice 2's `!AskWalkOvers`):
     - slice 1: skip the ask and continue. It gets no further walk-over this phase, but it is not added to `askSetAside`, so talking to the asker still raises it (Raised). The card-due test itself keeps it out of later scans, because `askMisses` and `askUnreachable` are cleared only on a phase change. `askSetAside` holds only asks the player has seen or put off: `DeferAsk`, `OpenOnAsk` (WalkOver and Raised) and, in slice 2, `OpenAskCard` and `EndAskCard`;
     - slice 2: if `AskCardMayOpen`, `OpenAskCard(ask)`; return either way, so a due card keeps its place in the order. `AskCardMayOpen = AskMayStart && AskMayOpen && askCardSettle >= AskSettleSeconds`: the map-only overview lets the free-house clock and the scan run beneath it (new visits may start there), so the card keeps its own settle, `askCardSettle`, which runs while `AskMayStart && !ClosableChromeUp` and resets otherwise. The card then never opens the moment the overview closes (test 14). Elsewhere it equals `askFreeRun`.
   - Otherwise try `npcMeetings.TryReserveNpcInvitation(ask.askerId, player, …)`.
     - On success: start the visit and return.
     - On failure, charge only failures that are about the asker. Slice 1 adds an overload `TryReserveNpcInvitation(npcId, player, out lease, out reason, out HouseNpcInvitationRefusal refusal)` to H/HouseMeetingCoordinator.NpcInvitation.cs; the old signature forwards to it, so the legacy path is unchanged. The refusal kinds:
       - `House`: :51-52.
       - `Player`: the player half of :53-54.
       - `Asker`: the asker half of :55-56, and `body == null` at :60-61.
       - `Busy`: :58-59, and :84.
       - `PlayerSpot`: `TryLocate` at :61 fails, or no ring point clears the floor, room, keep-clear, spacing and clearance tests at :72-75.
       - `NoRoute`: at least one point cleared those tests but `MeasurePath` at :76 failed for each, or :87-90.
     - On `Busy`, `NoRoute` or `Asker`: `askUnreachable[id] += AskScanSeconds`, then continue with the next ask. A seated asker therefore never blocks the queue (:58-59), and an ask's unreachable time counts only its own asker's failures. Slice 2: if that charge makes the ask card-due, the card branch runs for it on this scan (open it if `AskCardMayOpen`, and return either way), so it keeps its place ahead of every ask listed after it.
     - On `PlayerSpot`, `House` or `Player`: charge no record and end this scan, because every asker would fail the same way: a doorway, the 0.1 m + radius inset at a floor's edge (H/HouseRoomQuery.cs:238-258), or a spot where no 1.8 m point fits.
     - Slice 2 adds `askSpotBlockedFor`, one counter for the player's spot, not per record, that resets with the stillness anchor. At `AskUnreachableSecondsBeforeCard` it opens only the first listed ask as a card, and it marks no other record card-due. "First listed" is the first ask this scan tried to reserve, which is the first one that can still come, even if its own asker refused as `Busy`, `NoRoute` or `Asker` earlier in the same scan. The counter does not reset when a card opens, so while the player stays on the spot each later ask, after its gap, also comes as a card.

**`RecordAskMiss(id)`.** Adds 1 to `askMisses[id]`, sets `askRetryAt[id] = askNextAt = askClock + AskRetrySeconds`, then `CancelAskVisit()`. The record is not set aside. Setting `askNextAt` here also gives a waiting scene its gap.

**`HearAsk(view, generation, ask)`** is the bubble's Hear, E and auto-open.
1. Check the callback as :126-130 does, with `ask.IsCurrent`.
2. Run `askOpenWith = ask; askOpeningFor = ask.askerId; try { CancelAskVisit(); opened = TryOpenNpc(ask.askerId); } finally { askOpenWith = null; askOpeningFor = null; }`.
3. If it did not open, `RecordAskMiss`.

**`DeferAsk`** is Not right now, or Escape on the bubble (R/EpisodeDirector.Controls.cs:260). It adds the ask to `askSetAside`, counts a presentation, sets `askNextAt = askClock + AskGapSeconds`, then `CancelAskVisit()`.

**Opening on the ask** (R/EpisodeDirector.Conversation.cs `TryOpenNpc`, :235-250). `OpenOnAsk(npc.Id)` runs after `ClosePanels()` and the focus set-up and before `Render()` at :250. It does nothing when `!AsksOn`. It picks the ask:
- **WalkOver:** `askOpenWith` when its asker is this person.
- **Raised:** otherwise, the first ask in `CurrentAsks()` (re-read after `TryOpenNpc`'s flush) from this person that passes all of these:
  - `IsCurrent`, and not in `askSetAside`;
  - `conversationIntent == null`: the player did not come from her free-time screen with Ask for information (`IntentAsk`, which opens Learn) or Pitch a deal (`IntentDeal`, which opens Deals; R/EpisodeDirector.FreeTimeScreen.cs:47, 496-502; R/EpisodeDirector.ConversationNavigation.cs:39-40). The Deals page already draws her offer first, under "AN OFFER FROM <NAME>" (`DealPanel`, R/EpisodeDirector.Conversation.cs:476-487), and the dialogue's Back to topics would drop the player on the wheel, which breaks the "without a second wheel" contract (T/PlayMode/EpisodePlayModeTests.ConversationGroups.cs:610-628). The ask is not added to `askSetAside` on that open;
  - `NpcInvitationConversationAvailable(projected, id)`. Phases where the conversation only greets are skipped (R/EpisodeDirector.cs:1500-1513).
  - `!HasConversationBeat(projected, id)`, extracted from `ConversationBeat` (R/EpisodeDirector.Story.cs:44-52), which then calls it.

With an ask chosen, `OpenOnAsk` runs these steps in order:
1. `PrepareConversationNavigation(projected, person)`;
2. `conversationCategory = DealsCategoryCaption`, `invitedDealId = ask.recordId`;
3. set `askDialogueWith`, `askDialogueRecordId`, `askDialogueWay` (WalkOver when `askOpenWith` chose it, else Raised) and `askDialogueFirstRender = true`;
4. add the ask to `askSetAside`, so it is raised or walked over once a phase.

For a WalkOver ask it also counts a presentation and sets `askerNextAt[askerId] = askClock + AskerGapSeconds`.

**`ThinkItOver(id)`** (WalkOver). Sets `askNextAt = askClock + AskGapSeconds`, calls `hud.DropFocusAsked()`, then `ClosePanels()`.

**Pairing** (moved from slice 3 to slice 1 behind `AsksOn`):
- **Busy set.** Beside R/EpisodeDirector.NpcSocial.cs:283, while `AsksOn` add `askNow?.askerId` and `askOpeningFor` to `busy`. That covers the flush inside `TryOpenNpc` (R/EpisodeDirector.NpcSocial.cs:392-396), which today runs after the visit is already cancelled (R/EpisodeDirector.NpcInvitations.cs:137-140).
- **Walk-ins.** In `TryWalkInPair` (R/EpisodeDirector.Conversation.cs:432), while `AsksOn`, drop the same ids from `ids`.

**Shared helpers** (R/EpisodeDirector.NpcInvitations.cs), each with a one-line `AsksOn` dispatch at the top:
- `TryHearVisibleNpcInvitation` (:154) and `TryDeferVisibleNpcInvitation` (:161);
- `VisibleNpcInvitationSpeaker` (:168), which serves the E prompt at R/EpisodeDirector.cs:510 and Interact at :542-543;
- `CancelNpcInvitation` (:175) also calls `CancelAskVisit()`. That covers ClosePanels (R/EpisodeDirector.cs:702), Submit (:841), the ceremony, competition and opening, and the scene stage;
- `ResetNpcInvitationsForLoad` (:184) also calls `ResetAsksForLoad()`. It already runs from R/EpisodeDirector.NpcSocial.cs:525 and R/EpisodeDirector.Season.cs:412.

R/EpisodeDirector.Story.cs:608 becomes `if (NpcVisitInFlight) return;`.

**Source dispatch** (the base the storyline plan uses):
- `bool AskOpensInPerson(Ask)`: Offer and VetoAsk open on the dialogue through `OpenOnAsk`.
- `(string hear, string notNow) AskBubbleCaptions(Source)`: Offer and VetoAsk return `HouseNpcInvitationBubble.HearCaption` and `NotNowCaption`.
- `void AskBody(EpisodeState, Ask)`: draws the source's answer controls (Offer and VetoAsk: `OfferAccept`, `OfferDecline`).

### C.3 Frame: `R/EpisodeHud.AskDialogue.cs` (new EpisodeHud partial)

```csharp
public const string ThinkItOverCaption = "Think it over";
public const string AskOfferHeading = "THEIR OFFER";
public const string AskDirectEyebrow = "A WORD IN PRIVATE";
public const string AskGoneLine = "That offer is no longer on the table.";
public const string AskLineName = "Ask spoken line", AskStandingName = "Ask standing", AskTermsName = "Ask terms",
                    AskLapseName = "Ask lapse line", AskMoreName = "Ask more waiting",
                    AskYourWordsName = "Ask your words", AskReplyName = "Ask their reply";
public static string AskWalkOverEyebrow(string first) => first.ToUpperInvariant() + " PULLS YOU ASIDE";
public static string AskRaisedEyebrow(string first) => first.ToUpperInvariant() + " HAS AN OFFER FOR YOU";
public static string AskMoreWaitingLine(int n) => n == 1 ? "1 more houseguest wants a word."
                                                         : n + " more houseguests want a word.";
public void AskDialogue(string askerId, string eyebrow, string pronouns, string standing, string line,
                        bool whisper, string terms, string lapseLine, bool inConversation);
public void AskMoreWaiting(int count);                   // drawn after the controls; nothing for 0
public void ReadAskControl(Button button, string reading); // into offerReadings (EpisodeHud.OfferBoard.cs:129)
public void AskReply(string askerId, string playerLine, string npcLine);   // slice 2
```

`AskDialogue` draws, in order:
1. `Eyebrow`.
2. The speaker header:
   - in a conversation, `SpeakerTitle(askerId, name, pronouns)`, which sets the Conversation layout (R/EpisodeHud.cs:741-781);
   - on the card, `SpeakerRow`, extracted from `SpeakerTitle` without its `SetActivityLayout` call (slice 2).
   - The subtitle is the pronouns only. Its box is fixed at 24×scale with a floor of 11 that truncates (R/EpisodeHud.cs:779-780, 2472-2485), so long text does not go there.
3. Standing and mood as `NamedParagraph(AskStandingName, standing)` (R/EpisodeHud.Story.cs:132; `Paragraph` at R/EpisodeHud.cs:729 returns void and cannot be named).
4. `Speech("\"" + line + "\"", AskLineName, whisper)` (R/EpisodeHud.cs:826), omitted when the line is null.
5. `Heading(AskOfferHeading)`.
6. The terms as `NamedParagraph(AskTermsName, terms)`.
7. The lapse line as `NamedParagraph(AskLapseName, lapseLine, UiTheme.Muted)`, omitted when it is null.

The owner then draws the answer controls, so `DescribeOfferControl` registration stays with `OfferAccept` and `OfferDecline` (R/EpisodeDirector.Strategy.cs:282-315). The owner also registers a reading on each with `ReadAskControl`: the caption, the asker's full name, then the terms. This is as the board does (R/EpisodeHud.OfferBoard.cs:434-435), and `TryReadOfferControl` (:135-139) covers both.

### C.4 Source contract (for slices 4-5 and the storyline plan)

A new source adds:
1. an enum value;
2. a branch in `ComesToYou.Read` that yields `Ask` rows from saved state only, with `recordId`, `askerId`, `line`, a lapse line and its own `IsCurrent`;
3. its place in the order;
4. its cases in `AskOpensInPerson`, `AskBubbleCaptions` and `AskBody`.

**Bubble captions.** Each source's pair is fixed, distinct from every other source's and from the Pull's six (R/EpisodeHud.Story.cs:149-156). Slice 4 adds the overload `HouseNpcInvitationBubble.Show(npcName, line, fontScale, worldAnchor, hearThemOut, defer, hearCaption, notNowCaption, view = null)`. The current `Show` calls it with `HearCaption` and `NotNowCaption`. The captions are applied on every Show, not only in `Create` (R/HouseNpcInvitationBubble.cs:164-165, 201-211). Keep the raw pair in fields, as slice 3 does for the line. Set each button's label to `Localisation.Text(raw)` and its GameObject name to the raw caption, on the same two Button objects, so the identity of `HearControl` and `NotNowControl`, the explicit navigation (:166-170) and `Focus` are unchanged. `SizeForText` measures the two labels' current text instead of the constants (:120-121). Show's `changed` (:46) also counts a change of either raw caption, so the bubble re-sizes. A blank caption hides the bubble, as a blank line does (:41-43).

**Shared by every source:** the queue, the gates, gaps, caps, misses, set-aside, the visit, auto-open, the frame, Think it over, the reply page, focus, the pointer hold, readings and the floor.

**Line variety.** Choose a line from a bank with FNV-1a 32 over the record id's UTF-16 units, modulo the bank size. Never use `string.GetHashCode`, which .NET Core randomises per process, and never a draw.

**The Story Pull's set-aside.** `declinedPulls` (R/EpisodeDirector.Story.cs:343) is keyed by beat or walk-in and is not cleared by phase. It joins `askSetAside`'s model in slice 5, when the Pull's Approach becomes a source.

---

## D. Every incoming ask

| Ask (pipeline map #) | Comes to you? | Slice |
|---|---|---|
| 1. Weekly offer `deal-ask-` | Yes: walk-over, raised or card | 1 (walk-over, raised), 2 (card) |
| 2. Nominee's veto ask `deal-veto-`, outside the role moments | Yes, where the conversation gate allows it: the week's windows, or a decider (R/EpisodeDirector.NpcInvitations.cs:121-124). Otherwise it stays on the meeting rows (R/EpisodeDirector.Strategy.cs:159-176, `VetoOffers`, which R/EpisodeDirector.VetoMeeting.cs:221 draws when the role moments are off). | 1-2 |
| 2b. Nominee's veto ask under the role moments | No. It stays on the veto board, which shows its tone, its stakes and that silence costs more than a no. Option for slice 4: a card drawn from `RoleOffers.CardOf` (S/Story/RoleOffers.cs:189), with `SilenceLine` as its lapse line, no Think it over focus, and the board's order. | — (they stay on the board: owner question 3, answered 2026-10-30) |
| 3. Keep-the-block `deal-keep-` (role moments only) | As 2b. Under role moments the Head of Household is a decider at the meeting (S/StrategyRules.cs:105-108); without this exclusion they would walk over ahead of the nominees. | — (4) |
| 4. Reply cards (confrontation, gossip, plea) | Yes. The "SOMEBODY CAME TO YOU" hero (R/EpisodeHud.cs:272) becomes the visit. Answers go through `ReplyToHouseguest` (S/EpisodeEngine.Strategy.cs:181-233). | 4 |
| 5. HoH pitch `reply-pitch-` | Yes, in Nomination. Without the role moments (off the board) only the card waiting first, `ReplyCards.Pending` (S/ReplyCards.cs:102), can be answered (S/EpisodeEngine.HoHPitches.cs:13; `HoHPitches.Answerable`, S/HoHPitches.cs:117-122). So a pitch is offered only while it is that card (`ReplyCards.Pending(s)?.kind == ReplyCards.Pitch`, as R/EpisodeDirector.NominationScreen.cs:78 checks), and `BoardPicks` plays no part there. Under the role moments (on the board) any waiting pitch can be answered, and `HoldTheBoard` (S/HoHPitches.cs:145-154) has already stored them in `BoardPicks` order, so the first pitch waiting in `s.replyCards` is offered, never a fresh `BoardPicks` call. The pitch board stays. | 4 |
| 6. Story "approach" beat | Yes. The walk-over replaces the Pull's Approach surface (R/EpisodeDirector.Story.cs:424-432). In person the beat opens in the conversation's Story branch, the frame's in-person host; `OpenSceneCard` is the card fallback when the speaker cannot come (StoryArcs/32 §D0). | 5 (storyline plan) |
| 7. Counter (C7) | Never; it lives inside the player's own refused proposal | — |
| 8. War-room plan | Never; the player called the meeting | — |
| 9. Loyalty declaration | Never; it is the player's own word | — |
| 10. Legacy house event or walk-in | Never | — |

Nothing is removed from the boards (the veto offer board, the pitch board, the veto meeting's rows) or from the rail badge, the objective line and the Deals petal's tag. The badges and the objective line list every waiting ask; the boards list only their own kinds (veto asks, keep-the-block, pitches), never an ordinary offer. A card never shows over a board, because both are panels.

**Counter-offers.** The engine has no player counter to an NPC's offer. "Back to topics" leads to the player's own paid `ProposeDeal`.

---

## E. Captions, layout, focus, pointer, input

**Kept exactly:**
- `DealAcceptCaption` and `DealDeclineCaption` (R/EpisodeHud.cs:70-71). In a conversation and on the card, only `OfferAccept` and `OfferDecline` (R/EpisodeDirector.Strategy.cs:282-315) draw them, one pair per offer. The offer board still draws and registers its own pair on each card in `OfferAnswers` (R/EpisodeHud.OfferBoard.cs:421-436), unchanged.
- The bubble's `HearCaption`, `NotNowCaption`, `PanelName`, `LineName` and the identity of `HearControl` and `NotNowControl` (R/HouseNpcInvitationBubble.cs:16-21, 35-36). `CurrentText` stays the raw `OpeningLine`.
- The E prompt (R/EpisodeDirector.cs:510).
- `DealsCategoryCaption` and `ConversationTopicsBackCaption` (R/EpisodeDirector.ConversationNavigation.cs:13, 16), "THEIR OFFER", and `FreeTag`.
- The Pull's six captions (R/EpisodeHud.Story.cs:149-156).
- The board names and captions (R/EpisodeHud.OfferBoard.cs:50-66; R/EpisodeHud.NominationScreen.cs:30).
- The exchange captions (R/EpisodeHud.ConversationExchange.cs:11-12).
- The director reads `InvitedDealRecordId`, `NpcInvitationIsVisible` and `CurrentConversationCategory`.
- The whole legacy invited view while `AsksOn` is off. PortVerification enters a category through "Back to topics" (R/PortVerification.Conversation.cs:93-117) and checks the invited identity (R/PortVerification.Season.Systems.cs:334-378). The PV adapter test asserts the selected control (T/PlayMode/EpisodePlayModeTests.PortConversationAdapters.cs:2341).

**New captions:**
- "Think it over". No control uses it at origin/main.
- "Back to the house", reusing `SceneCardDoneCaption` (R/EpisodeDirector.Story.cs:336). The scene card and the ask card are exclusive panels.
- Neither collides with "Not now" or "Not right now", so the test helper's `.Single` finds stay unique (T/PlayMode/EpisodePlayModeTests.cs:476-478).

**The in-person dialogue** (R/EpisodeDirector.ConversationNavigation.cs:109). After the topics-wheel block (:128-149) and before the category chrome (:151):

```
if (AsksOn && conversationCategory == DealsCategoryCaption && invitedDealId != null
    && invitedDealId == askDialogueRecordId && RenderAskDialogue(state, npc, token))
{ hud.GuardConversationChoices(() => ConversationNavigationCurrent(token, state, npc.id)); return; }
```

`RenderAskDialogue(state, npc, token)`:
1. Find the pending offer. If it is gone, clear `invitedDealId`, `askDialogueRecordId` and `askDialogueWay`, call `hud.DropFocusAsked()` and `hud.EndPointerHold()`, and return false. The Deals page then shows the narration as today.
2. `hud.AskDialogue(...)`. The eyebrow is `AskWalkOverEyebrow` or `AskRaisedEyebrow`. The line and lapse line come from `ComesToYou.Find`; if it finds nothing, the line falls back to `NpcInvitation.Available`'s and the lapse line is null.
3. `OfferAccept` (tagged `FreeTag` when interactable), then `OfferDecline` tagged `FreeTag`, each with a reading.
4. **WalkOver:** `hud.Action(ThinkItOverCaption, …ThinkItOver(offer.id))`. Then, on both ways, `hud.Action(ConversationTopicsBackCaption, …)`, which calls only `BackConversationNavigation(true)`. Both are guarded by `ConversationNavigationCurrent`. In `BackConversationNavigation`'s branch that clears the category (R/EpisodeDirector.ConversationNavigation.cs:97-104), when `askDialogueRecordId != null`, set `askNextAt = askClock + AskGapSeconds`, clear `askDialogueWith`, `askDialogueRecordId` and `askDialogueWay`, and call `hud.DropFocusAsked()` and `hud.EndPointerHold()`. That one place serves both the control and Escape, Start or B in person, which reach it through R/EpisodeDirector.Controls.cs:261 without this lambda.
5. `hud.AskMoreWaiting(OtherAskers)`.
6. **Focus and pointer**, on the InformationShare pattern (R/EpisodeDirector.InformationShare.cs:128-144):
   - on the first render, `hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds)` and `hud.FocusWhenWired(walkOver ? ThinkItOverCaption : ConversationTopicsBackCaption)`;
   - on later renders, `hud.KeepPointerHold()` and `hud.KeepFocusAsked()` (R/EpisodeHud.FreeTimeBoard.cs:1161-1234).
   - Without an asked focus the HUD keeps a still-eligible current selection or a same-named one. Otherwise it falls back to `OpeningControl` (R/EpisodeHud.cs:2305-2314; R/EpisodeHud.Radial.cs:258-273), the column's first live control (FreeTimeBoard.cs:1155-1160 and 1178-1185 say the same). Here that is Accept, or Turn down when the yes is drawn locked: `LockedAction` is not interactable (R/EpisodeHud.Odds.cs:78-84), so it is not eligible (R/EpisodeHud.cs:2276-2279). Either way an answer would take the first Enter.
   - While the hold runs, the modal's CanvasGroup blocks no raycasts. So `TryDescribeOfferControl` returns false (`OfferControlPresentationOwned`, R/EpisodeHud.cs:171), and a raycast press finds nothing. `TryReadOfferControl`, `FindButton`, `onClick.Invoke()` on a button already in hand, and the EventSystem selection are not affected.
7. Set `cameraRig.ConversationWindowOffset`.

**Leaving the dialogue.**
- `ClosePanels` already ends the hold and drops the asked focus, through `ForgetFreeTimeView` (R/EpisodeDirector.FreeTimeBoard.cs:118-128; R/EpisodeDirector.cs:722).
- Leaving the dialogue inside the conversation drops both the hold and the asked focus. After an answer (Accept or Turn down), step 1 drops them on the next render, because the offer is no longer pending. Back to topics drops them in `BackConversationNavigation` (step 4), whether it comes from the control or from Escape, Start or B in person (R/EpisodeDirector.Controls.cs:261), and that also clears the dialogue fields and starts the gap.
- Reading order: line, terms, Accept, Turn down, Think it over (walk-over only), Back to topics. Tab and Up/Down walk only the controls: Accept, Turn down, Think it over (walk-over only), Back to topics, then the panel's "Close  [Esc]", which the Conversation layout moves into the column after the scroll (R/EpisodeHud.Activities.cs:223-227). The ring wraps from Close back to Accept (R/EpisodeHud.cs:2276-2300).
- **Escape, Start or B:**
  - on the bubble: Not right now first (R/EpisodeDirector.Controls.cs:260);
  - in person: Back to topics (:261);
  - on the card: the panel closes, which is Think it over (`EndAskCard`).

**Layout and text sizes.**
- Every box comes from layout: the speech card grows (R/EpisodeHud.cs:828-842), action rows grow to the label's height + 14 (:2256-2267), and `SpeakerTitle`'s fixed boxes are at least 1.35× their font (:775-780; the tightest is the name at Largest, a 31 px font in a 42 px box, because `NewText` rounds the font at :2495).
- The subtitle holds only pronouns, and standing and mood are body text, so the 1.3× rule holds. The label sweep checks it.
- At Largest, 1920×1080, all answer controls must sit inside the scroll viewport when the dialogue opens. If they do not, move the lapse line below the controls; do not page.

**While it is up.** Every way in is a panel (`IsPanelOpen`, R/EpisodeDirector.cs:106-108):
- the player's click input is off (:1230) and camera controls, WASD panning among them, are off (:1231);
- E does nothing (:537) and the prompt is cleared (:524);
- the NPC world is paused (R/EpisodeDirector.NpcSocial.cs:392-396);
- a click-to-move walk resumes once it closes (H/HousePlayerController.cs:481-490).

During the walk-over the player keeps full control.

**The card (slice 2):**
- `IsPanelOpen` adds `|| askCardOpen` (R/EpisodeDirector.cs:106).
- `hud.Begin`'s open argument adds `|| askCardOpen` (:1383).
- Dispatch `if (askCardOpen) { AskCard(state); return; }` goes before :1533.
- `ClosePanelsInternal` calls `EndAskCard()` beside :735. That sets the ask aside if it is still pending and unanswered, starts the gap (`AskGapSeconds`) after every card, answered or not, and then clears the card fields.
- `OpenAskCard(ask)`:
  1. `PauseNpcSocialForPanel()`; return false if `blockedRecovery`.
  2. Re-check `ask.IsCurrent(projected)` after the flush.
  3. `ClosePanels()`; set `askCardOpen`, `askCard`, `askCardReply = null` and `askCardFirstRender = true`; add to `askSetAside`; count a presentation.
  4. Input and camera controls off; `Render()`.
- `AskCard(state)`:
  - It draws on `SetActivityLayout(ActivityLayout.Stage)`, as the scene card does (R/EpisodeDirector.Story.cs:766). It never uses the Conversation layout, which leaves room for a two-shot the card does not frame (R/EpisodeHud.Activities.cs:189-229).
  - With a reply, or with the offer gone, call `hud.DropFocusAsked()` and `hud.EndPointerHold()` and neither Keep*, as an in-person answer does ("Leaving the dialogue" above). The keyboard falls to "Back to the house", the page's only control.
  - With a reply: `hud.AskReply(...)` and `hud.Action(SceneCardDoneCaption, ClosePanels)`.
  - Else, if the offer is gone: `Paragraph(AskGoneLine)` and the same action.
  - Else: the eyebrow `AskDirectEyebrow` and `AskDialogue(..., inConversation: false)`; Accept and Turn down with readings; `hud.Action(ThinkItOverCaption, ClosePanels)`; `AskMoreWaiting`; then, if `askCardFirstRender`, `hud.HoldPointerOffPanel(EpisodeHud.PointerHoldSeconds)` and `hud.FocusWhenWired(ThinkItOverCaption)` and clear `askCardFirstRender`; otherwise `hud.KeepPointerHold()` and `hud.KeepFocusAsked()`. This is the pattern of R/EpisodeDirector.ExtraActionPurchase.cs:85-87 and R/EpisodeDirector.InformationShare.cs:142-145.
- **The reply** (R/EpisodeDirector.ConversationExchange.cs):
  - Between the accepted check (:41-47) and the `if (focusedNpc == null || blockedRecovery || npcSaveSuspended) return;` at :48, add: `if (askCardOpen && focusedNpc == null) { if (!blockedRecovery && !npcSaveSuspended) CaptureAskCardReply(before, result, command); return; }`. A refused command has already returned at :46, so the card never captures a refusal, and a failed save (R/EpisodeDirector.Durable.cs:90, 105) never shows a reply that was not saved.
  - `CaptureAskCardReply` computes its own `visible` list as :50-53 does (`state = result.state` is :49), builds `ConversationExchange.Create(before, state, command, visible)`, and keeps it when its `NpcId` is the asker.
  - It never sets `conversationNarration`, so nothing leaks into the next conversation. The conversation path is otherwise unchanged.

**The bubble (slice 3):**
- **Localisation.** Route the name, line and captions through `Localisation.Text`. Keep the raw line in a field for the change test at R/HouseNpcInvitationBubble.cs:46-47 and for `CurrentText`, which stays the raw `OpeningLine`. Set the localised text on the label separately.
- **Text boxes** are at least `ceil(fontSize × 1.32) + 1` (:116-126).
- **Placement** uses a `ViewCamera` the director sets from `cameraRig.ViewCamera`, instead of `Camera.main` (:89), falling back to `Camera.main` while it is unset.
- These three fixes also reach the legacy invitation, which draws the same bubble (R/EpisodeDirector.NpcInvitations.cs:81-86); its captions, control identities and `CurrentText` are unchanged (§B).
- *As built:* because slice 3 localises the captions, `SizeForText` already measures the two labels' shown text (slice 4's row keeps the relabel and rename), and `Show`'s `changed` counts a change of the shown name, line or captions besides the raw line. The director names the view from `TickAsks` each frame, after `TickNpcInvitations` when asks are off, so `TickNpcInvitations` itself is unchanged. The box floor is `HouseNpcInvitationBubble.LabelBoxHeight`; a caption's button grows to its box plus its insets.

---

## F. Simulation boundary and exact files

**Unchanged:**
- **Commands.** No new `EpisodeCommandKind`. Answers are `RespondToDeal` with the deal id and `AcceptDeal` or "decline" (R/EpisodeDirector.Strategy.cs:294-314).
- **Presentation commits nothing.** Visits, Hear, auto-open, raising, Think it over, Not right now, misses, retries, gaps and caps commit nothing.
- **No draws.** Nothing presentational calls `Roll`, `StoryRandom`, `UnityEngine.Random` or `EpisodeEngine.Change`.
- **No saved field, no schema bump, no `Log` line.** The "N more" line is built by the presentation.
- **Old seasons, golden digests and Sim sweeps** never run the director and replay byte for byte.

**The one real shift.** Auto-open, raising and the card open a panel, and opening any panel flushes the NPC ticks that are due (R/EpisodeDirector.NpcSocial.cs:392-396). Like every panel the player opens, they move *when* a real-time NPC tick commits. The busy-set and walk-in changes also change real-time NPC pairing; in seasons before the story system a walk-in pair submits a saved `WitnessProximity` (R/EpisodeDirector.Conversation.cs:404-416). All of this sits behind `AsksOn`, which is off in every PlayMode fixture that binds a director (the three binds in §B), every PortVerification route and every batch run.

| Slice | File | Change |
|---|---|---|
| 1 | `S/ComesToYou.cs` (+ meta) | The reader (§C.1), with the role-board exclusion and `OtherAskers` |
| 1 | `R/EpisodeDirector.ComesToYou.cs` (+ meta) | Switch, constants, state, gates, `TickAsks`, scan, `RecordAskMiss`, `HearAsk`, `DeferAsk`, `OpenOnAsk`, `ThinkItOver`, `RenderAskDialogue`, source dispatch, `ResetAsksForLoad`, `CancelAskVisit`, public reads |
| 1 | `R/EpisodeHud.AskDialogue.cs` (+ meta) | Captions, names, `AskDialogue`, `AskMoreWaiting`, `ReadAskControl` |
| 1 | `R/EpisodeDirector.cs` | :454 `TickNpcInvitations()` → `TickAsks()`; `AsksEnabled` |
| 1 | `R/EpisodeDirector.NpcInvitations.cs` | `AsksOn` dispatch in :29-30, :154, :161, :168; `CancelAskVisit` from :175; `ResetAsksForLoad` from :184. `TickNpcInvitations` unchanged. |
| 1 | `R/EpisodeDirector.Conversation.cs` | `OpenOnAsk` in `TryOpenNpc` before :250; walk-in exclusion in `TryWalkInPair` (:432); in `SelectHouseguest` (:144-150), the `AsksOn`-gated Hear for a click on the asker whose bubble is up |
| 1 | `R/EpisodeDirector.ConversationNavigation.cs` | The `AsksOn`-gated dialogue branch before :151; in `BackConversationNavigation`'s category clear (:97-104), drop the ask dialogue, its gap, focus and hold when `askDialogueRecordId != null`. :193 and :221-235 unchanged. |
| 1 | `R/EpisodeDirector.Story.cs` | Extract `HasConversationBeat` from :44-52; :608 reads `NpcVisitInFlight` |
| 1 | `R/EpisodeDirector.NpcSocial.cs` | Busy set beside :283; a `NearbyOfferUp` property that `TickNearby` (:88-89) then uses as `hud.SetNearby(!IsPanelOpen && NearbyOfferUp)`, so the ask gate and the Nearby card share one expression |
| 1 | `H/HouseMeetingCoordinator.NpcInvitation.cs` | The refusal overload of `TryReserveNpcInvitation` (§C.2 step 9); the old signature forwards to it |
| 1 | `R/PortVerification.cs`, `R/PortVerification.LookSheet.cs`, `R/PortVerification.Season.cs` | `AsksEnabled = false` in R/PortVerification.cs right after the ready check at :92-93, before :94 (`readySeconds`) and the :96 creator and :98 look-sheet branches, never inside the :90-91 wait loop; and right after the binds at R/PortVerification.LookSheet.cs:101 and R/PortVerification.Season.cs:123 |
| 1 | `T/PlayMode/EpisodePlayModeTests.cs` | `director.AsksEnabled = false;` after :411 |
| 1 | `T/PlayMode/EpisodeDurableTransactionTests.cs` | `director.AsksEnabled = false;` after :51 |
| 1 | `T/PlayMode/HousePrototypeTests.cs` | `episode.AsksEnabled = false;` after :136 |
| 1 | `T/PlayMode/EpisodePlayModeTests.NpcInvitationFlow.cs` | Extract a `void SaveNpcInvitationFlowState()` from :18-38 (build the state, Propose, the three Pending asserts, socialActions, TryValidate, the EpisodeSaveStore save). `InstallNpcInvitationFlowHouse` calls it, then keeps :39-44 as they are (ReloadEpisode, the two NpcRead asserts, ShowOverview, WaitForVisibleNpcInvitation), so it behaves as before |
| 1 | `T/EditMode/ComesToYouTests.cs` (+ meta); `Tools/SimulationTests/SimulationTests.csproj` after :52 | EditMode tests |
| 1 | `T/EditMode/ComesToYouStillnessTests.cs` (+ meta) | EditMode test 12; not in the dotnet subset, because the helper is in Runtime |
| 1 | `T/PlayMode/EpisodePlayModeTests.ComesToYou.cs` (+ meta) | PlayMode tests |
| 2 | `T/PlayMode/EpisodePlayModeTests.NpcInvitationFlow.cs` | `SaveNpcInvitationFlowState(bool threeAsks = false)`: the three-ask variant for test 15 |
| 2 | `R/EpisodeDirector.ComesToYou.cs` | `AskWalkOvers`, `askCardOpen`, `askCard`, `askCardReply`, `askCardFirstRender`, `askSpotBlockedFor`, `OpenAskCard`, `AskCard`, `EndAskCard`, `CaptureAskCardReply`, the scan's card branch |
| 2 | `R/EpisodeDirector.cs` | :106, :1383, dispatch before :1533, `EndAskCard` beside :735 |
| 2 | `R/EpisodeDirector.ConversationExchange.cs` | The card capture between :47 and :48 (after the accepted check, before the focusedNpc/blockedRecovery/npcSaveSuspended return), with the blockedRecovery/npcSaveSuspended guards |
| 2 | `R/EpisodeHud.cs` | Extract `SpeakerRow` from `SpeakerTitle` (:741-781); `SpeakerTitle` = layout + `SpeakerRow` |
| 2 | `R/EpisodeHud.AskDialogue.cs` | `AskReply` |
| 3 | `H/HouseMeetingCoordinator.NpcInvitation.cs` | See the slice 3 details below |
| 3 | `R/EpisodeDirector.ComesToYou.cs` | Pass `answerFromArrival: true`; facing on arrival; `askPlayerYaw`, `askAskerYaw` and `ClearAskFacing()` |
| 3 | `R/EpisodeDirector.NpcSocial.cs` | Facing skip beside :475 |
| 3 | `R/HouseNpcInvitationBubble.cs` | Localisation with the raw line kept, 1.3× boxes, `ViewCamera` with a `Camera.main` fallback; the legacy invitation gets them too |
| 3 | `R/EpisodeDirector.cs` | `ClosePanelsInternal` calls `ClearAskFacing()`, which clears only the facing an ask set |
| 4 | `R/HouseNpcInvitationBubble.cs` | Caption overload: relabel and rename the same two controls, measure the shown captions, count a caption change in `changed` |
| 1, 2, 3 | `Tools/baseline.txt` | The floors the slice's tests raise (see Commit hygiene) |

**Slice 3 details:**
- **Coordinator.**
  - Add `answerFromArrival` to slice 1's refusal overload: `TryReserveNpcInvitation(npcId, player, bool answerFromArrival, out lease, out reason, out refusal)`; the old signatures pass false.
  - With true, the travel budget is clamp(measured route `shortest` / `HouseNpcMotion.DefaultSpeed` × 1.5 + 3, 6, 30) (H/HouseNpcMotion.cs:27). The constants are `NpcInvitationTravelFactor`, `NpcInvitationTravelSlack`, `NpcInvitationTravelMin` and `NpcInvitationTravelMax`.
  - `expiresAt` restarts at `NpcInvitationLifetime` from the first `HasArrivedAt`.
  - The lease still never follows the player.
- **Facing.**
  - Beside R/EpisodeDirector.NpcSocial.cs:475, skip the asker while `AsksOn` and they have arrived.
  - On the first `NpcInvitationCanSpeak`, the asker gets `SetFacing(FacingToward(...))` and `LookAt(player.transform, 2f)` (Assets/Gamesim/Runtime/Presentation/CharacterPresentation.cs:163, 636). The player gets `SetFacing` when the dialogue opens.
  - `OpenOnAsk` records the yaw it gives the player in `askPlayerYaw` (NaN when none); the asker's arrival facing is held the same way. *As built:* a heading is given only to a body whose `FacingYaw` is NaN (and, for the player, with no activity owner), so a talk spot's, a seat's or a ceremony's is never overwritten; and for a walk-over `OpenOnAsk` turns the asker to the player again, because `HearAsk`'s `CancelAskVisit` has just let their arrival heading go. `ClearAskFacing()` runs from `CancelAskVisit` (after its nothing-to-cancel return, and only while no ask dialogue is open, so the per-frame cancel under an open panel keeps the dialogue's heading: a raised open beside a visit still in flight lets that visit go on the next frame, and the open's own `ClosePanels` has already let its arrival heading go) and `ClosePanelsInternal`. If `askPlayerYaw` is set and the player's `FacingYaw` still equals it, it calls `SetFacing(float.NaN)` (the check at R/EpisodeDirector.Fallout.cs:182). It then resets `askPlayerYaw` to NaN, and does the same for the asker. `ClosePanelsInternal` never clears a facing it did not set: the talk spot's facing (R/EpisodeDirector.TalkSpots.cs:179, 193) is set before `TryOpenNpc`'s `ClosePanels()` (R/EpisodeDirector.Conversation.cs:244) and must survive it, whether `AsksOn` is on or off, as must the ceremony's (R/EpisodeDirector.CeremonyStage.cs:882). `ResetAsksForLoad` resets `askPlayerYaw` too. `FacingToward` is at R/EpisodeDirector.TalkSpots.cs:243.

**Commit hygiene.** Each new .cs needs a hand-written two-line meta (`fileFormatVersion: 2`, `guid:` 32 hex). Raise the floors in `Tools/baseline.txt` in the same commit that adds the tests (its header rule), counting cases, not methods: each TestCase row or per-scale variant counts once. Add a `# N = prev + <slice>'s k ...` comment line above each raised line, as the file does.
- Slice 1: `Gamesim.EditModeTests` rises by the EditMode cases (ComesToYouTests and ComesToYouStillnessTests); `Gamesim.SimulationTests` rises by ComesToYouTests' cases only, which it shares through SimulationTests.csproj. `Gamesim.PlayModeTests` and `Gamesim.PlayModeTests.UmaBuild` each rise by the case count of PlayMode tests 1-10 and the "also in slice 1" cases; the tests are shared, so both lines move.
- Slice 2: both PlayMode lines rise by tests 11-16.
- Slice 3: both PlayMode lines rise by the HouseNpcInvitationPlayModeTests and director-level cases.
- `Gamesim.Uma.PlayModeTests` and the Diagnostics lines do not change.
- A slice qualifies PlayMode with a filtered run, so mark its PlayMode raises "Not yet counted by a full run" until a full NoUMA and UMA-build run counts them.

Stage explicit paths only.

---

## G. Tests

### EditMode: `T/EditMode/ComesToYouTests.cs`

Add it to Tools/SimulationTests/SimulationTests.csproj after :52; the reader comes in through the Simulation glob at :37.
- Build offers with `ContentCatalog.Create(412)` as NpcInvitationTests does.
- Build the role-moments board as RoleOffersTests does; that test is in the subset (csproj :329).
- Check purity with `JsonConvert`, never `JsonUtility`.

The tests:
1. `ReadListsTheAnswerableOffersAndVetoAsksOnly`: outside the role moments, the set equals the intersection of `WaitingOnYou.Read`'s non-card items and `NpcInvitation.Available`.
2. `RoleBoardAsksStayOnTheBoard`: under `RolesOn`, the nominees' veto asks and the keep-the-block ask are not listed; an ordinary offer the same week is.
3. `OrderIsPublicNotWarmth`: two same-week, same-urgency offers whose ids run opposite to their asker ids come back in asker-id order.
4. `UrgencyAndWeekStillLead`: a vote_save comes before a partnership; a newer week comes first.
5. `VetoAsksFollowTheBlockOrder`: outside the role moments, in `s.nominees` order.
6. `ReadingWritesNothing`: `JsonConvert.SerializeObject(state)` is equal before and after `Read` and `Find`.
7. `IsCurrentFollowsTheRecord`: false after an answer, a phase or week change, or changed terms.
8. `LapseLineSaysWhen`: the three lines, null for Open, and the this-week and next-week split.
9. `DepartedAskersAndFinalHoHListNothing`.
10. `WhisperKindsAreThePrivateOnes`.
11. `OtherAskersCountsPeopleNotAsks`: one asker with two asks counts once; the current asker and asks failing `canStillCome` are not counted.

In `T/EditMode/ComesToYouStillnessTests.cs` (EditMode only, not in the dotnet subset, because the helper is in Runtime):
12. `StillnessIsTheSameAtAnyFrameRate`: the pure stillness helper (position, `HasArrived`, dt) fed the same positions at 30 and 240 fps gives the same still time to within one frame, and a 2.2 m/s walk (0.0092 m per frame at 240 fps) with `HasArrived` true never accrues 1 s.

**Mutation pass.** Each mutant must fail a test:
- order by `recordId`;
- drop the presence join;
- drop `lapsesOnAdvance`;
- whisper always false;
- drop the role-board exclusion;
- count asks, not askers.

Hash the files before the round and check them with `sha256sum -c` after any interruption.

### PlayMode: `T/PlayMode/EpisodePlayModeTests.ComesToYou.cs`

**Fixture `InstallComesToYouHouse(params string[] holdAsideAskers)`:**
1. `SaveNpcInvitationFlowState()`, then `ReloadEpisode()`, then `director.AsksEnabled = true`.
2. Let the switch take effect before holding anyone aside. `TickAsks`'s toggle reset (step 1) and its first session-key store (step 3) both clear `askSetAside`, so a hold written in the same frame as the switch is lost on the next Update. Yield frames, up to 2 real seconds, until `NpcRead<bool>("asksWereOn")` is true. Then assert `director.CurrentNpcInvitationRecordId == null`; no visit can start yet, because the settle and still clocks start at zero.
3. Only then hold the named askers' records aside through the private-write helper. Take their record ids from `ComesToYou.Read(projected)` (or `NpcDeals.Pending`) by `askerId`/`proposerId`, add them to `NpcRead<HashSet<string>>("askSetAside")`, and assert at least one id per named asker.
4. No overview.
5. Wait up to 35 real seconds for `CurrentNpcInvitationRecordId != null`. Then assert that `CurrentAskerId` is not one of `holdAsideAskers` and that `IsAskSetAside(id)` still holds for every held record.

*As built:* step 1 waits for every body to assemble with asks off before the switch (a body finishing later re-renders the HUD), with the house's pairing held (`npcApproachDiagnosticsSuppressed`), and the pairing stays held until step 5's visit is reserved; the wait stops early, and fails, if a card opens instead. The NPC clock runs through the assembly, and the house pairs any asker with no visit in flight (C.2's busy set), Maya first. The UMA build's three reds at step 5 (2026-10-30) fit the house's next pairing scan coming before the ask's first scan and taking the one free asker, whose ask then came as a card after 10 s of Busy (A), with no visit. That cause is inferred from the code and the timings, not seen in a log, so every install writes its switch report (the NPC clock at the switch and the next pairing scan) to the test's output, and a miss prints each ask scan by what it charged. Once the visit is reserved the busy set holds its asker back, and pairing is let go unless the test holds the house. Held throughout, with ambient seats, as test 17 does, because their asker later has no visit in flight again: Think it over, walking away (a miss and its retry), talking to the asker and coming to ask or pitch (a closed conversation, then another), the overview and emote-menu passes of the held-under-chrome test, following someone else, and the asker and player turning to each other (a missed walk's retry); the capture test holds pairing itself, from the assembly to the open. With pairing live from the reservation: test 9, whose subject is the house's pairing and which asserts it is live, and the install-only tests whose asker stays in the busy set until the dialogue opens (the arrival opens it, a click on the asker, a stalled frame, the localised line).

That house has offers from Maya and Jamie, and "jamie-roberts" sorts before "maya-hassan" (S/ContentCatalog.cs:26). A test that follows one record holds the other aside.

**Three-ask variant, for test 15 only** (slice 2). `SaveNpcInvitationFlowState(threeAsks: true)` sets, after the base scores and before `NpcDeals.Propose`: `taylor-kim`→player = 30; `taylor-kim`→`casey-wilson` = -20; player→`casey-wilson` = -10. Casey becomes a common threat (S/NpcDeals.cs:143-149), so Taylor's ladder files a TargetAgreement about Casey (:200, :569). That is a third distinct type, so the variety rule (:322) keeps it. Maya's Partnership and Jamie's InformationSharing are unchanged, because their scores toward Casey stay 0. The variant asserts `NpcDeals.Pending(state).Count == 3` and the three (proposer, type) pairs: Maya/Partnership, Jamie/InformationSharing, Taylor/TargetAgreement with targetId `casey-wilson`. `InstallNpcInvitationFlowHouse` and `InstallComesToYouHouse()` keep the default two-offer house.

**Every test:**
- sets `Application.runInBackground = true` and `Time.captureDeltaTime = 1f/30f`, both reset in `finally`. The pin fixes only scaled time (animation, NavMesh walking), not `Time.unscaledDeltaTime` (T/PlayMode/EpisodePlayModeTests.Pause.cs:18-20; CeremonyRevealPlayModeTests.cs:30-31). So every wait on an ask is bounded in `Time.realtimeSinceStartup` or measured on `askClock` read with `NpcRead`, as EpisodePlayModeTests.NpcInvitationFlow.cs:49 does; never in frames or captured seconds;
- holds the walk-in watch with `NpcWrite("proximityWatch", -1e6f)` unless it tests walk-ins;
- never calls `HoldTheHouseForTheFixture` and never ticks `TickNpcSocialRuntime`;
- waits until no body `IsBodyAssembling` before UI assertions;
- invokes controls with `onClick.Invoke()` and asserts identity with `Is.SameAs`;
- reads revisions only after a panel has opened;
- takes the last child when it finds by name;
- prints `ArrivalFailure` and `LastRouteFailure` on a walk miss;
- after the dialogue or the card opens, asserts `hud.PointerHeld` on the open frame and reads the selection after 2 frames, then `yield return WaitOutThePointerHold()` before any `TryDescribeOfferControl` assertion and before any `ButtonWithCaption` lookup (its `AssertPressable` raycasts), as ExtraActionPurchase.cs:28-35 does.

**Slice 1:**
1. **`ComesToYou_ArrivalOpensTheDialogueOnTheExactOffer`** (Jamie held aside). No press; wait for the conversation.
   - `TalkingToId == CurrentAskerId`, `InvitedDealRecordId == CurrentAskRecordId`, category Deals, way WalkOver.
   - The `AskLineName` label equals the quoted `OpeningLine`; "THEIR OFFER" and the terms are drawn.
   - Exactly one each of Accept, Turn down, Think it over and Back to topics.
   - `hud.PointerHeld` is true on the open frame. After 2 frames the selected control is Think it over.
   - Wait out the pointer hold; `PointerHeld` is now false.
   - `TryDescribeOfferControl` names the offer, and `TryReadOfferControl` gives the name and terms.
   - No new `RespondToDeal`; actions spent unchanged.
   - Press Accept: one command id, revision +1, exchange playing. The detached-engine result matches, reusing the UI command id as T/PlayMode/EpisodePlayModeTests.DialogueIncomingOffer.cs:120-130 does.
2. **`ComesToYou_ThinkItOverCommitsNothingAndIsNotBroughtAgainThisPhase`.**
   - Read in the press frame: save bytes and `AssertEquivalent` are unchanged, `IsAskSetAside(id)` is true, and the offer is still in `WaitingOnYou.Read`.
   - Use `NpcWrite` to set `askNextAt` to the current `askClock` and to clear the asker's `askerNextAt`: the WalkOver open set it `AskerGapSeconds` (120 s) ahead, and the 20 s gap would hide the set-aside too. Then wait until `askClock` has advanced `AskSettleSeconds + AskStillSeconds + 3 × AskScanSeconds`, bounded at 15 real seconds: no visit is reserved for that record. Control: remove the id from `askSetAside` with the private-write helper; the same record leases again within the same bound.
   - After `!NpcWholeSecondDue()`, `TryOpenNpc(asker)` opens on the topics wheel with the Deals petal's waiting tag.
3. **`ComesToYou_WalkingAwayIsARetryNotADeferral`** (one asker).
   - Before arrival, `Agent.Warp` the player 3 m away. The lease is released, `AskMissCount == 1`, and the record is not set aside.
   - The same record leases again once `askClock` passes `askRetryAt`, within `AskRetrySeconds` + 10 real seconds.
   - A second miss stops the walk-overs for that record this phase: `AskMissCount == 2`, `IsAskSetAside(id)` is false, no visit for it within 30 real seconds, and `TryOpenNpc(asker)` (after `!NpcWholeSecondDue()`) opens on it with way Raised.
4. **`ComesToYou_TalkingToTheAskerOpensOnTheOffer`.** While the bubble is up, wait for `!NpcWholeSecondDue()` (DialogueIncomingOffer.cs:64-89), then `TryOpenNpc(asker)`.
   - Way Raised; the eyebrow is the raised one; there is no Think it over; Back to topics is selected after 2 frames; bytes unchanged.
   - Back to topics, close, open again: the topics wheel.
   - 4b, on an existing Nomination-phase installer without the week's windows: `TryOpenNpc` draws the notice, and the record is not set aside.
   - 4d, the click: while the bubble is up, call `SelectHouseguest(asker)` by reflection (`typeof(EpisodeDirector).GetMethod("SelectHouseguest", BindingFlags.Instance | BindingFlags.NonPublic)`, as EpisodePlayModeTests.Accessibility.cs:527 does for `Render`). Way WalkOver, the walk-over eyebrow, Think it over present and selected after 2 frames, one presentation counted, bytes unchanged. With `AsksEnabled = false` the same click takes today's route.
   - 4c: `TalkWithIntent(asker, IntentDeal)` with an unseen ask opens on Deals and its Bargain group, with no ask dialogue, and the ask is not set aside. The same with `IntentAsk` opens on Learn.
5. **`ComesToYou_OffInTestsAndVerificationByDefault`.**
   - Without opting in, the legacy bubble shows, nothing opens for 5 real seconds (`Time.realtimeSinceStartup`), counted from the frame the bubble is first visible, and Hear opens the legacy invited view (its category chrome).
   - A fresh director with `AsksEnabled = null` reports `AsksAreOn == !Application.isBatchMode`.
6. **`ComesToYou_HeldInTheOverviewAndUnderTheEmoteMenu`.**
   - With the overview open on the map only (`ShowOverview`), the bubble shows and nothing opens for 5 real seconds, counted from the frame the bubble is first visible. Leave the overview: the dialogue opens after the still time.
   - The same with the emote menu.
7. **`ComesToYou_SeatedAskerDoesNotBlockTheNext`.**
   - Seat the first-in-order asker through the coordinator's furniture activity, as the house-activity tests do.
   - The next asker leases at once.
   - After 10 s of failed scans (refusal `Busy`) the seated asker gets no further walk-over. `IsAskSetAside(id)` is false, and once they are free, `TryOpenNpc(asker)` opens on the offer with way Raised.
8. **`ComesToYou_NoAskWhileWalkingAtHighFrameRate`.** W pans the camera (H/HouseCameraActions.cs:245-248; H/HouseCameraRig.cs:921) and never walks the player, who moves only by click or `TryMoveTo` (H/HousePlayerController.cs:343-369). At `captureDeltaTime = 1f/240f`, walk the player with `TryMoveTo` legs between two floor points in one room, re-issuing a leg on each arrival, for 4 real seconds. No visit is reserved on any frame. After the last arrival a visit is reserved within 10 real seconds, and no sooner than `AskStillSeconds` of unscaled time after the arrival.
9. **`ComesToYou_AskerIsNeverPairedOrWalkedInOn`.** While leased, and across the open, the asker is in no `npcPendingWorld` pair and in neither walk-in slot. This test runs the walk-in watch.
10. **Captures.** `foreach (var textSize in new[] { TextSize.Standard, TextSize.Largest })`, and inside it reduced motion off, then on. Each pass calls `if (Application.isBatchMode) yield return CaptureFraming("comes-to-you-in-person" + (reduced ? "-reduced-motion" : "") + TextScales.Suffix(textSize));`. The reduced-motion part keeps the four frames apart, because `CaptureFraming` writes `name + ".png"`.
    - Off is the fixture's default: under `SaveRootOverride` the director reads no preferences (R/EpisodeDirector.cs:146, 169).
    - Turn it on the way a player does, and only after `InstallComesToYouHouse`, because its `ReloadEpisode` restores the default: `director.OpenSettings()`, press "Reduce character motion", then `director.ClosePanels()`, as `Ceremonies_ReducedMotionCutsToTheRoom` does (T/PlayMode/EpisodePlayModeTests.CeremonyTruth.cs:221-227). Then wait for the auto-open as usual; the settle and still times start again.
    - Under reduced motion: before the open, `cameraRig.ReducedMotion` is true, so the test cannot pass vacuously; after the open, `cameraRig.IsConversationFocused` is true, `cameraRig.DesiredFocus` is within 0.01 m of its value on the frame before the open (H/HouseCameraRig.cs:296-298), and the asker is in frame.
    - With motion on, the rig takes the two-shot: `DesiredFocus` moves to the pair.
    - The label sweep covers the new labels: box ≥ 1.3× font, ≥ 12 px at 1080p, preferred height ≤ the rect.
    - All answer controls are inside the viewport at Largest.

**Also in slice 1** (asks on, from `InstallComesToYouHouse`):
- **`ComesToYou_SceneWaitingDoesNotStallAVisitInFlight`.** Start a visit while a pair meeting holds `leases` and a beat is wanted. End the meeting. Auto-open fires within `AskSpeakBeatSeconds` plus a margin, `AskMissCount == 0`, and the scene stages after `ClosePanels`.
- **`ComesToYou_PlayerSpotChargesNoAsker`.** Two free askers. The player stands still for 15 real seconds in a doorway or within 0.3 m of a floor edge. No record is set aside and `askUnreachable` stays at 0. After the player moves to open floor, the first ask leases within 3 real seconds of arriving there. *From slice 2 the spot's own counter opens the first listed ask as a card at 10 s, which sets that ask aside, so this case as written cannot hold. Slice 2 replaces it with test 17.*
- **`ComesToYou_FollowingSomeoneElseCancelsWithoutAMiss`.** `FollowHouseguest` with another id mid-visit cancels the visit with `AskMissCount` unchanged and `IsAskSetAside` false, and no new visit starts until the follow is released.
- **`ComesToYou_AStalledFrameDoesNotOpenTheDialogue`**, modelled on `AStalledFrame_DoesNotSpendABodysAssemblyBound`. With the bubble up, one `Thread.Sleep(1500)` frame does not call `HearAsk`.
- **`ComesToYou_APullYieldsAfterItsTime`**, only if the owner agrees (question 5). While a Pull is up, no visit starts for `AskPullYieldSeconds`. Then the visit starts, the Pull hides once the bubble shows, and it returns undeclined after the conversation closes.

**Slice 2:**
11. **`ComesToYou_CardOpensWithoutAWalkAndAnswersFree`** (`AskWalkOvers = false`).
    - `IsAskCardOpen`, the Stage layout, no conversation focus on the camera, `PointerHeld` on the open frame, Think it over selected after 2 frames; then wait out the hold; then the assertions as in test 1, with `CurrentAskerId`, `CurrentAskRecordId` and way Card checked against the card's ask in place of `TalkingToId` and `InvitedDealRecordId`, because the card has no conversation focus.
    - Accept: the reply page shows both lines and "Back to the house"; pressing it closes the panel.
12. **`ComesToYou_ClosingTheCardIsThinkItOver`.** Escape and `ClosePanels()` commit nothing (bytes the same), and the record is set aside.
13. **`ComesToYou_CardReplyLeaksNoNarration`.** After a card answer, `conversationNarration` (read with `NpcRead`) is null, and the next conversation shows no "WHAT HAPPENED".
14. **`ComesToYou_NeverOverChrome`.**
    - Settings, notebook, the phase panel, a staged Pull (InvitationAndPull's staging), the emote menu and the overview: 30 frames each, no card.
    - After closing, the card opens only after the settle time.
15. **`ComesToYou_SeveralAsksOneAtATimeInPublicOrder`.**
    - On the three-ask variant, the three asks come as cards in public order (§C.1 sort), at least `AskGapSeconds` apart. `Propose` files at most three a week (S/NpcDeals.cs:275, 316-322).
    - With `askPhaseCap` lowered to 2 through the private-write helper, the third asker's card does not open in this phase, and it opens after the phase advances.
    - The "N more" line counts distinct askers.
16. **Captures** `"comes-to-you-card" + TextScales.Suffix(textSize)` at Standard and Largest, in the same loop form as test 10.
17. **`ComesToYou_ASpotThatFitsNoVisitorBringsTheFirstAskAsACard`** (added at slice 2's review).
    - Two free askers; pairing and ambient seats are held. The player stands still on a floor point in frame that is in no room's safe interior, and both askers' reservations are refused as `PlayerSpot`.
    - Until the card opens: nobody sets out, `askUnreachable` has no entry above 0, nobody has a miss and nothing is set aside.
    - On the open frame, `askSpotBlockedFor >= AskUnreachableSecondsBeforeCard`, and the card is the first listed ask, with way Card.
    - Close it and move the player back to open floor. Then `askSpotBlockedFor` is 0, and once the gap is released the second ask leases as a walk-over, not a card.

**Slice 3:**
- **Coordinator level**, in `HouseNpcInvitationPlayModeTests` without the director: the travel budget follows the route; expiry restarts at arrival; drift still releases (H/HouseMeetingCoordinator.NpcInvitation.cs:99-112).
- **Director level:**
  - the asker's `FacingYaw` is within 20° of the bearing on arrival;
  - with `AsksOn` on, a standing talk-spot conversation still has the player's `FacingYaw` within 20° of the bearing to the houseguest after the conversation opens;
  - `CurrentText` is still the raw line;
  - the bubble boxes are ≥ 1.3× at Largest.
- **The three known flakes**, re-run 3 times.
- *As built:* coordinator level `NpcInvitation_AVisitAnsweredFromArrivalWalksOnItsRoutesBudget`, `NpcInvitation_AVisitAnsweredFromArrivalRunsItsLifetimeFromArrival` (with the old signature's lifetime from dispatch as its control) and `NpcInvitation_AnArrivedVisitStillEndsWhenThePlayerWalksAway`; director level `ComesToYou_TheAskerAndThePlayerTurnToEachOther` (the director's visit sets out on a route's budget and its window starts again at arrival, the arrival heading, the NpcSocial skip, the player's heading on open, both kept while the dialogue is up, both let go on close), test 4 `ComesToYou_TalkingToTheAskerOpensOnTheOffer` for a raised open's heading (the player's only, kept after the visit beside it goes, let go on close), test 5 `ComesToYou_OffInTestsAndVerificationByDefault` for the legacy line's view, `ComesToYou_AStandingTalkSpotKeepsThePlayersFacing` (every seat anchor disabled, so Talk takes a standing place), `ComesToYou_TheLineIsLocalisedAndCurrentTextKeepsItsRawWords` and `ComesToYou_TheLinesBoxesFitAtLargest`; and `NpcInvitationBubble_PlacesTheLineOnItsViewAndElseOnTheMainCamera` for the view and its fallback. The three pure rules (the travel budget, the heading hand-back, the box floor) are EditMode-only `T/EditMode/ComesToYouWalkOverTests.cs`, which Unity's Mono can run and mutate; it reaches Runtime, so it is not in the dotnet subset.

**PortVerification:** no change to its routes. `AsksEnabled = false` once the bootstrap's director is ready, and at the look sheet's and season's binds, keeps the batch walk, the look sheet, the profile run and the creator route on the legacy path. The proof is:
- the standalone walk's PendingOffer counts equal the last baseline;
- the windowed look sheet completes with no ask dialogue or card on any shot.

**As qualified (2026-11-01), slices 1-3 at c5cdb972.** Unity: full EditMode 9245 and no-UMA PlayMode 1385 at e9f7ed2d (offers2), and at c5cdb972 (offers3, which changed only the offers tests) the 28-case EditMode and 136-case PlayMode gate, Gamesim.Uma.PlayModeTests 132 and the UMA-build PlayMode 1425, all passed with no re-runs; the three known flakes passed three times each; captures at Standard and Largest viewed. The subset (16263) and the seven digest tiers passed exact at e9f7ed2d. By hand, on release players of 37416049 and c5cdb972:
- **The standalone walks:** two a side, every walk Passed with one PendingOffer row, Ineligible, in week 1, on both sides. The verifier answers at most one offer a season and this machine has never seen it reach Committed, so the counts are unchanged but the answer path is proved by the PlayMode tests, not by the walk.
- **The windowed look sheet:** not passed. Three attempts on the head reached 8, 10 and 9 of 12 shots, with no error. Shots 9 and 10 (nominations) were blocked once by the houseguest nomination pitch card, which is main's (625d191a), not this branch's; the other misses were refused walks and a houseguest who did not open, not traced. The base player was not run through it. The owner's direction of 2026-11-01, verbatim: "Try to do less testing where you try to navigate the game and take screenshots, rather just implement and let me check and give you feedback." So the look sheet is left to the owner's own play, and its failures to a follow-up on main's look sheet.
- **Known, cosmetic:** in the in-person capture a bystander stands in front of the asker.

---

## H. Slices

Each slice is built, qualified and landed before the next.

**Qualification for every slice:**
- offline compile;
- dotnet subset at or above the floors in Tools/baseline.txt;
- full EditMode;
- filtered PlayMode on Gamesim.PlayModeTests. Unity reads `-testFilter` as ';'-separated, unanchored regexes matched against each test's full name (Gamesim.Tests.PlayMode.EpisodePlayModeTests.<Method>), never file names, so filter by test-name prefixes: `ComesToYou_;NpcInvitation;DialogueCampaign_RealIncomingOffer;HudStatus_TheInvitationBubbleAndTheStoryPull;HudStatus_EscapeDuringALine;HudStatus_TheDealsTopic;HudStatus_AnInvitedOffer;PortConversationAdapters_;OfferBoard_;PitchBoard_`. Slice 1 adds `EpisodeDurableTransactionTests;BootstrapBuildIndexZero_EntersPlayableEpisodeHouse`, because it edits those fixtures. Coverage: `NpcInvitation` covers NpcInvitationFlow_, NpcInvitationBubble_ and the whole HouseNpcInvitationPlayModeTests fixture; `OfferBoard_` covers both the OfferBoard and KeepTheBlock files; the four HudStatus_ prefixes cover InvitationAndPull and ConversationBackAndWaiting; `DialogueCampaign_RealIncomingOffer` covers DialogueIncomingOffer. Name every new test `ComesToYou_...` or add it to the filter by name. A filtered run has no floor, so confirm from the report's test names that every one of these files and every new test ran, and list every case that did not pass;
- captures at Standard and Largest;
- golden and season digests byte-equal;
- standalone PendingOffer counts unchanged;
- the no-UMA smoke;
- slice 1 only: the windowed look sheet.

**The slices:**
1. **It comes to you and opens as a dialogue.** The reader, the generic visit and switch, the frame, the walk-over with auto-open, raising, the queue with public order, gaps, caps, per-record retry and set-aside, the gates, the pointer hold, focus and readings, the busy set and walk-in exclusion, the scene yield, the coordinator's refusal kinds, the in-frame and stalled-frame guards, and the fixture and PortVerification switches. Tests EditMode 1-12, PlayMode 1-10 and the "also in slice 1" cases.
2. **It pops up when they cannot come.** The card host, `SpeakerRow`, the reply page, the card triggers (with the player's-spot counter) and the fixture's three-ask variant. Tests 11-17.
3. **The walk-over holds.** The travel budget and answer window, facing and the bubble fixes. Slice 3 tests.
4. **Everyone who comes to you.**
   - The reply-card and pitch sources, the bubble's caption overload, and same-asker chaining: the footer offers "There's something else" only when that asker has a second listed ask.
   - Not built (owner question 3, answered 2026-10-30: they stay on the board): veto-board cards from `RoleOffers.CardOf` in board order.
   - Optional: a PortVerification `PendingOfferDialogue` route behind a verification option. It answers the **same offer with the same `RespondToDeal` at the same point**, so the audited command list does not change (memory: removing a step re-rolls the season).
5. **Storyline plan.** Story approach beats and covert asks become sources. The Pull's Approach hands over to the walk-over, which opens the beat in the conversation's Story branch, with the scene card as the card fallback. `declinedPulls` joins the set-aside model. Per-character line banks use the FNV pick. This slice is owned by the storyline design.

---

## I. Defaults taken, and questions for the owner

**Defaults:**
- When a houseguest reaches you, the dialogue opens by itself once their bubble has been on screen for 1.2 s and you have stood still for 1 s. Hear, E or a click on them opens it sooner. Not right now sets it aside.
- The keyboard opens on Think it over (Back to topics when raised), and pointer presses are held off for 0.45 s.
- Think it over sets the ask aside for the phase. It comes back unasked at most twice a session, and talking to them opens on it once a phase.
- The walk-over stays on under reduced motion, and the camera does not move.
- A busy asker becomes a card after about 10 s (slice 2; in slice 1 the walk-overs stop and talking to them still raises it); they do not get up from furniture. Only the asker's own refusals count toward the 10 s.
- While you use furniture (sit, lie, soak, cook), asks wait until you get up.
- No visit starts while the camera follows someone else or is panned away from you; the camera is never moved for an ask.
- The role moments' veto-board asks stay on the board.
- Timings: 2 s settle, 1 s still, 20 s between asks, 8 s after a miss, 120 s per houseguest, at most 3 a phase and 6 a week.
- Off in tests and verification. On in the editor and the standalone player.
- No setting, and no counter-offer: Back to topics, then propose your own.
- A reload presents waiting asks again; nothing is saved.

**Questions for the owner:**

**Answered, 2026-10-30.** The owner: "Use the defaults for all five offer questions". Every default above stands. Question 5 changes the story session's D18 (lane S2a) behind `AsksOn` only, recorded as a delegated call, confirmed by the owner.

1. When a houseguest reaches you, should the dialogue open by itself (the default), or wait for Hear, E or a click?
2. When the houseguest is busy (seated, in bed or chatting), should they get up and come over, or should the offer pop up after about 10 s (the default)?
3. Should the veto board's asks under the role moments also come to you, as a card showing their tone, stakes and that silence costs more than a no (slice 4)? Or should they stay on the board only (the default)?
4. While you sit, lie, soak or cook, should a houseguest come over to you, or should their ask wait until you get up (the default)? Coming over would need the coordinator to accept a player with an activity owner (H/HouseMeetingCoordinator.NpcInvitation.cs:53-54, 107-108), and Hear to get the player up before the dialogue opens; lifting the director's gate alone would not do it.
5. D18 (the story session's decision, lane S2a) says a Story Pull keeps the floor until it is answered, and a Pull has no timeout. Should a Pull yield to a waiting ask after 20 s of free, still time (the plan's proposal), or keep the floor until it is answered or its beat closes? Record the answer as "delegated call, confirmed by the owner" before slice 1.

---

## Critique disposition

Every finding was checked against origin/main 37416049, read-only.

1. **Accepted.** The look sheet and profile run are windowed (R/PortVerification.LookSheet.cs:92-95; R/PortVerification.cs:163-164, 255). The switch is now `AsksEnabled`, set false at PortVerification's binds (after :92-93; LookSheet :101; Season :123). The look sheet is added to slice 1's qualification.
2. **Accepted.** Today's loop skips a failed reservation (R/EpisodeDirector.NpcInvitations.cs:96-100). The scan now walks the ordered list. Unreachable time is per record, and in slice 1 a card-due record gets no further walk-over but stays raisable; in slice 2 it becomes a card. Only the asker's own refusals (busy, no route) add unreachable time; a spot of the player's that fits no visitor charges no record.
3. **Accepted.** Stillness is now the time spent within 0.1 m of an anchor, so it does not depend on frame rate. PlayMode test 8 runs at 240 fps.
4. **Accepted.** `HoldPointerOffPanel` on the first render of every way in and `KeepPointerHold` after it, on the InformationShare.cs:128-144 pattern. Tests assert `PointerHeld`.
5. **Accepted.**
   - A raise sets the record aside, so it happens once a phase, and it never raises a record already set aside.
   - It requires `NpcInvitationConversationAvailable`.
   - The raised dialogue has no Think it over, focuses Back to topics, and has its own eyebrow.
6. **Accepted, option (a).** `RolesOn && RoleOffers.IsBoardAsk` asks are excluded from the reader (S/Story/EpisodeEngine.RoleMoments.cs:88-92, 145-149; S/Story/RoleOffers.cs:66). Option (b) is slice 4 at the owner's call (question 3).
7. **Accepted in part.**
   - The visit now carries a `ComesToYou.Ask` with a per-source `IsCurrent`, and opening, bubble captions and answer bodies dispatch by source.
   - Per-source caption pairs, distinct from the Pull's six, are a contract item.
   - The bubble's caption overload lands with the first non-deal source (slice 4). Folding `declinedPulls` in moves to slice 5, because its keys and lifetime differ (R/EpisodeDirector.Story.cs:343).
8. **Accepted.** New visits, auto-open and the card wait while the Nearby card's own condition holds (R/EpisodeDirector.NpcSocial.cs:85-90).
9. **Accepted.** Misses set `askNextAt`; the scan yields while a scene could stage now, but a visit in flight still auto-opens (`AskMayOpen` has no scene term); Story.cs:608 reads `NpcVisitInFlight`.
10. **Accepted.** The busy set and walk-in exclusion moved to slice 1 behind `AsksOn`, with `askOpeningFor` holding the asker out of pairing across `TryOpenNpc`'s flush.
11. **Accepted in part.** The text is corrected: the camera does not move under reduced motion (H/HouseCameraRig.cs:298). No new camera move is added. Instead, auto-open requires the asker's bubble to be on screen (`IsVisible` is an in-viewport test, R/HouseNpcInvitationBubble.cs:33, 89-92), so the asker is in frame, and a visit starts and continues only while the player is in frame (`PlayerInFrame`), so a follow or a pan cancels it without a miss.
12. **Accepted.** The subtitle holds pronouns only; standing and mood are a Paragraph.
13. **Accepted.** The card uses the Stage layout and a `SpeakerRow` extracted from `SpeakerTitle` (R/EpisodeHud.cs:743).
14. **Accepted, all four.**
    - A new fixture helper opts in after the reload, with no overview.
    - Tests that follow one record hold the other asker aside.
    - Test 4 waits for `!NpcWholeSecondDue()`.
    - Test 15 uses the three-ask variant (Maya, Jamie, Taylor) and lowers the phase cap.
15. **Accepted.** The three PlayMode fixtures that bind a director set `AsksEnabled = false` after EpisodePlayModeTests.cs:411, EpisodeDurableTransactionTests.cs:51 and HousePrototypeTests.cs:136, so editor and batch runs match.
16. **Accepted,** as part of 2: unreachable time is per record.
17. **Accepted in part.** `ClosePanels` already ends the hold and drops the asked focus through `ForgetFreeTimeView` (R/EpisodeDirector.FreeTimeBoard.cs:118-128, called at R/EpisodeDirector.cs:722). The dialogue now also drops them when it is left inside the conversation: an answer through step 1, and Back to topics, from the control or from Escape, Start or B, in `BackConversationNavigation`.
18. **Accepted.** The count is `ComesToYou.OtherAskers`: distinct askers who can still come this phase, and 0 once a cap is reached. EditMode test 11 covers it.
19. **Accepted.** In the overview, visits and the bubble continue but auto-open and the card wait (`ClosableChromeUp`).
20. **Accepted.** `ReadAskControl` registers readings on Accept and Turn down in the board's dictionary (R/EpisodeHud.OfferBoard.cs:129-139).
21. **Accepted.** The raw line is kept for the change test and for `CurrentText`; the localised text is set separately.
22. **Accepted.** `CaptureAskCardReply` runs only when the card is open, computes its own list and never sets `conversationNarration`. Test 13 covers it.

23. **Lead's call on clicking the asker (verify run, design change 3).** A click on the asker while their bubble is up hears the ask, by the route E takes, behind `AsksOn`. A player who clicks the houseguest who just walked up expects to hear them; without it the click would walk the player to a talk spot or open the offer without Think it over. Test 4d covers it.

**Additional change from the reviser.** The new invited-view layout is now behind `AsksOn` too, and the legacy view (R/EpisodeDirector.ConversationNavigation.cs:193, 221-235) is untouched. Ungated, the new order would make an answer (Accept, or Turn down when the yes is locked) the default keyboard selection after a legacy Hear (`OpeningControl`: R/EpisodeHud.cs:2305-2314; R/EpisodeHud.Radial.cs:258-273). An asked focus could also race PortVerification's select-then-refind, which T/PlayMode/EpisodePlayModeTests.PortConversationAdapters.cs:2341 asserts.

## Defaults taken
- The dialogue opens by itself once the asker's bubble has been on screen for 1.2 s and the player has been still for 1 s; Hear, E or a click on the asker opens it sooner.
- The keyboard opens on Think it over (Back to topics when raised), and the pointer is held off for 0.45 s.
- Think it over sets the ask aside for the phase. It comes back unasked at most twice, and is raised in conversation once a phase.
- The walk-over stays on under reduced motion, and the camera does not move.
- A busy or unreachable asker becomes a card after about 10 s of their own refusals, or after 2 missed walks (slice 2). In slice 1 the walk-overs stop and talking to them still raises it.
- The role moments' veto-board asks stay on the board.
- Timings: 2 s settle, 1 s still, 20 s between asks, 8 s retry, 120 s per asker, 3 a phase and 6 a week.
- Off in tests and PortVerification (`AsksEnabled = false`); on in the editor and the standalone player.
- No setting, no counter-offer, nothing saved.

## Questions for the owner

**Answered, 2026-10-30.** The owner: "Use the defaults for all five offer questions". Every default above stands. Question 5 changes the story session's D18 (lane S2a) behind `AsksOn` only, recorded as a delegated call, confirmed by the owner.

1. When a houseguest reaches you, should the dialogue open by itself (the default), or wait for Hear, E or a click?
2. When the houseguest is busy (seated, in bed or chatting), should they get up and come over, or should the offer pop up after about 10 s (the default)?
3. Should the veto board's asks under the role moments also come to you, as a card showing their tone, stakes and the cost of silence? Or should they stay on the board only (the default)?
4. While you sit, lie, soak or cook, should a houseguest come over to you, or should their ask wait until you get up (the default)?
5. Should a Story Pull yield to a waiting ask after 20 s (the proposal), or keep the floor until it is answered, as D18 says today?

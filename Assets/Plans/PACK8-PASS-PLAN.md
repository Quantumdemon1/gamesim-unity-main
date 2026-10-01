<!-- Written 2026-09-30 from the pack8-understand workflow (wf_f7c6aec2-d3b): seven readers over the
nomination, veto and campaign screens, the ceremony stage, the veto meeting, the talk seating and Pack 8,
and two adversarial verifiers over the two bug diagnoses. -->

# Pack 8 pass: the weekly screens, the cut scenes every week, and talk on real seats

## The owner's ask (2026-09-30, screenshots and mockups 66 to 83, and Pack 8)

> Fix the UI/UX menu for the nomination screen as shown in the images, it cuts off and the user has
> to scroll, should be a full screen.
>
> Fix the players sitting when there is no furniture around. Sometimes when talking to other players
> they sit in the middle of the room without a chair or couch, keep this but only when they sit on
> furniture next to another player, make specific locations where players go to talk.
>
> The nomination cut scene only works for the first nomination, make it work for every nomination,
> but allow the player to skip it if they want to. Right now it defaults to the old one as shown in
> the screenshot.
>
> Fix the veto player selection screen, right now it is clunky and wordy and the player has to
> scroll. Make it more like the mock up, same with the veto meeting, there is no cut scene like there
> was supposed to be.
>
> Fix the lack of an eviction cut scene, we have it previously mapped out but didnt implement it.

Pack 8 (`gamesim_campaign_veto_nomination_pack_8.zip`, 102 PNGs) skins the Campaign, Veto Player
Selection and Nomination screens. The campaign's screenshots (77-80) and mockup (83) came with it,
so the campaign is in scope too.

| Image | What it is |
|---|---|
| 69, 66, 67, 68 | Today's nomination panel, top to bottom: two story beats, the ceremony, the faces, Continue |
| 72 | Nomination mockup: status cards, a four-step tracker, one step, the houseguests, an Up next footer |
| 70 | Week 2's key ceremony on the HUD over the overview: the stage did not run |
| 71, 73 | Today's veto player selection, scrolling |
| 76 | Veto player selection mockup: AUTOMATICALLY PLAYING, DRAW 3 PLAYERS, ELIGIBLE FOR DRAW, one CTA |
| 74, 75 | Today's veto meeting before and after ('Emma Brown saves Emma Brown') |
| 81, 82 | Veto meeting mockups: the holder card, use or keep, then the replacement and the final nominees |
| 77, 78, 79, 80 | Today's campaign, scrolling, a plea card on top |
| 83 | Campaign mockup: a nominee hero, the week's situation, tabs, houseguest cards, goals, intel, a tip |

## 1. What the investigation found

### 1.1 The cut scenes stop after the first staged eviction (the regression)

The staged nomination works in later weeks. What stops it is the first staged **eviction**:

- `FinishWalkOut` nulls `walkingOutId` and `departingId`, then calls `Project` (WalkOut.cs:132-136).
  `Project` switches the evicted's body off (EpisodeDirector.cs:1070-1072).
- The eviction stage is still in `Release`, which only ends once `walkingOutId` is null
  (CeremonyStage.cs:542), and `Holds` is `!ended && placeOf.ContainsKey(id)` (:242). So
  `ReconcileNpcSocialWorld` still counts the evicted as eligible through `CeremonyStageHolds`
  (NpcSocial.cs:141).
- The coordinator fails with "An eligible NPC root is inactive." (HouseMeetingCoordinator.cs:202-204),
  and `StopNpcWorld` sets `npcWorldFailed` (NpcSocial.cs:449-452), silently: it only sets `message`,
  which the next commit overwrites.
- From then on every `TryBeginCeremonyStage` returns false at CeremonyStage.cs:95, the walk-out is
  refused (WalkOut.cs:65), and NPC autonomy and wandering stay paused until a load.

The owner's Editor.log confirms it: week 1's nomination and eviction were staged (8 of 8 seated), and
there is no "Ceremony stage" line after that, although the save records week 2's nomination and
eviction. Both of week 2's HoH competitions were simulated, so the yard arena is not the cause. The
same path runs through the refused-walk-out branch (CeremonyTruth.cs:158-161) and through
`StartChallenge`'s `FinishWalkOut` (Challenge.cs:353).

Commit 3af59ae narrowed eligibility from `actor.id == departingId` to `CeremonyStageHolds(actor.id)`
and broke the invariant *eligible ⊆ active bodies*. CEREMONY-CUTSCENES §7.4 steps 5 and 7 and
MOCKUP-PASS M19 would rebuild the same bug if built as written.

**Three more ways a stage is refused:**
- **Path B:** when the evicted cannot re-bind for the eviction stage ("The feet are not inside one
  bound floor's safe interior."), Reconcile fails the whole world instead of dropping that one body,
  because `DepartureCandidate` is set only in `TryBeginWalkOut` (Departure.cs:38-45, WalkOut.cs:78).
- **A load mid-week:** the house's world is built only in Social or Campaign (NpcSocial.cs:102-106).
  After Continue on an autosave at HoH, Nomination, VetoMeeting or Eviction there is no
  `npcMeetings`, so that week's ceremonies are never staged.
- **`IsReady` latches with the failure:** Reconcile returns before replacing its eligible set
  (HouseMeetingCoordinator.cs:202-223), so recovering means a successful Reconcile, not just
  clearing the flag.

### 1.2 The skip exists but cannot be seen, and the summons leaks input

- **Presses already skip.** A click, Enter, numpad Enter, Esc, or the pad's A or B starts the card
  during the summons (CeremonyStage.cs:528-532). On the card, the first press jumps to the block or
  the result and the second closes it (KeyCeremony.cs:237-243, VoteReveal.cs:310-317). A press ends
  the walk-out (WalkOut.cs:88).
- **Nothing says so.** The HUD is held from the summons on (CeremonyTruth.cs:145-153). The on-screen
  frame drops the Dismiss line and keeps a small controls line that is visible only in the screen
  cuts.
- **The summons leaks input.** During the summons no card has called `CeremonyOverlays.Showing()`.
  So Submit, Cancel, Escape and the house shortcuts still reach the hidden HUD, and the phase panel
  is still open under it (EpisodeDirector.cs:782). An Enter meant to skip can press a hidden control.

### 1.3 The veto meeting is never staged, and the exit sequence was never built

- **The veto meeting.** Only `EvictionKind` and `NominationKind` build a `reveal`
  (EpisodeDirector.cs:859-868). The veto falls through to `PlayGenericCeremonyCard`: a 3.35 s HUD
  takeover and an 11 m framing of the Games room. The stage's veto branch is dormant: the room
  (CeremonyStage.cs:150) and the Assign branch (:302-320) exist, but there is no OnBeat case, no
  subscription to the takeover, and no screen path on the takeover (CeremonyTakeover.cs:137-153).
  The pre-commit block is kept only as an unordered HashSet (EpisodeDirector.cs:743).
- **The eviction's exit.** It is still the old follow-the-body walk. CEREMONY-CUTSCENES §7.4 (D3)
  and MOCKUP-PASS M19 and M23 map the exit: the goodbye, the slow walk, the door shot, the door
  closing and holding, and the exit door on the living room's west wall (owner decision 2A). None of
  it is built.

### 1.4 "Sitting in the middle of the room": the talk take is a seated take

No seat exists without furniture. Every seated anchor in the scene and every runtime seat stands on a
real prop.

**The primary cause.** The standing **Talk** state plays `bb_anim_Talk_loop`, which is a seated
Mixamo take:
- The hips average 63.6 cm, with the upper legs at 57.6° and the knees at -93° (HumanoidClipWiring.cs:155).
- **Listen_loop** is built from Talk_loop's frame 0 (HumanoidReactionAuthoring.cs:17-30), and so are
  **React_nominated**, **React_saved** and **React_evicted**: RootT.y 0.675, lower-leg stretch 0.22.
- Every standing conversation therefore sits both bodies on air: the speaker for almost the whole
  4 s turn, the listener throughout.
- A standing nominee drops into a sit when named. These reactions play only while `!Seated`
  (HumanoidClipWiring.cs:463).

**Secondary causes:**
- **The sit-down plays on the open floor.** `Begin` raises Seated on its first frame, and the
  animator reaches the sit in 0.35 s. But the body takes 0.75 s to ease from the root to the seat
  and is fitted to the cushion only after 0.45 s (HouseSeatPresentation.cs:67-72, 167, 181-189).
- **The stand-up snaps.** Chat endings, activities being taken away and every ceremony release call
  `End()`, which snaps the body back to the approach while the sit is still fading out.
  `CeremonyStage.End`'s `RequestExit` is cancelled because the stage has already ended
  (HouseSeatPresentation.cs:131-136, CeremonyStage.cs:1160-1163).
- **Chat seats flicker.** An NPC chat's seat is re-decided on every 0.1 s tick from a strict check
  (NpcSocial.cs:386-436). Anyone passing within 0.7 m, and every unpause (HouseNpcMotion.cs:274-278),
  stands the pair up and sits them down again.
- **No talk spots.** Talk never happens on the furniture the owner means. The living room's chat
  pair stands on open floor, and the player's conversations open wherever the player catches the
  NPC (Conversation.cs:99-158).

### 1.5 The screens scroll because a story beat or a plea turns the panel into a band

- **The mechanism.** On the nomination and the campaign, the first story beat (EpisodeHud.Story.cs:43)
  or plea card (EpisodeHud.Decisions.cs:88-90) switches the whole panel to the 403-unit HouseEvent
  band (EpisodeHud.Activities.cs:393-411). It also un-pins the way on (EpisodeDirector.cs:1544-1551).
  Every beat, the ceremony, the faces and the notes then stack in one scroll.
- **Without a beat.** The Stage is 1350x638 at FontScale 1.0 and 609 tall at 1.2
  (Activities.cs:283-305), with body copy at 21 pt. The veto draw and the meeting overflow it on
  their own.

### 1.6 Defects found on the way

- **'Emma Brown saves Emma Brown'.** `Target` is reflexive only for the player
  (EpisodeEngine.cs:2113-2114, 852-858). Pronoun data exists: `StoryPeople.Pronouns(...).themselves`.
- **Two live 'Not now' buttons in one render,** when The Lobby and a play offer are open together.
- **WindowLine says 'conversations left this week'.** Under the week's windows the count is the
  window's (EpisodeDirector.Strategy.cs:109-111).
- **A player HoH's 'Commit nominations' silently lets NomsSet storylines pass** (The Invite List),
  with no warning.
- **At six or fewer, the veto draw screen invents a draw.** It shows chips and '... and 3 drawn from
  the house' when everyone plays (CeremonyScreen.cs:51-61, EpisodeEngine.cs:2063).
- **A holder who is a nominee appears twice on the meeting screen.** The screen after the meeting
  never marks who was saved or replaced.
- **When the player is both HoH and veto holder,** the replacement list is drawn once per nominee,
  so every candidate caption appears twice.
- **'Save {name} and nominate:' reads as if the player saves,** when the player is the HoH naming a
  replacement after an NPC's save.
- **Knowledge-gate leak.** `ReplyCards.Message` picks the plea's wording from the NPC's hidden
  inbound score on every render (ReplyCards.cs:112-121). The wording reveals whether their view of
  the player is above +20, and it can change between renders.
- **The campaign's 'Wary' is the player's own reading,** but the card does not say so.
- **The toast and the plea name different houseguests.** 'Avery Thompson came to you...' appears
  under 'Jamie Roberts wants your support!'. These are two real pleas: the toast names the last, the
  card the first.

## 2. Decisions taken (the owner can overrule any)

The owner's earlier answers stand: the living room's screen on the south wall, the HoH seated in the
U, the exit door on the living room's west wall with the yard as the fallback, existing brand lines
only. Every other open decision in MOCKUP-PASS §4 and CEREMONY-CUTSCENES §8.4 takes its recommended
option.

1. **The nomination's pull quote.** None invented. The existing brand line may stand beside the
   title at FontScale 1.0 only.
2. **No generic Back on the nomination.** Commits cannot be undone, and Close [Esc] leaves. The
   footer's secondary slot holds a real way back when one exists: the comparison toggle, or a new
   'Back to your nominees'.
3. **The tracker is built from this week's story beats, not fixed words.** It has three or four
   segments, and a waiting story is the only tracker step that is a button.
4. **Mockup relabels become headlines, never captions.**

   | Mockup label | Caption it sits on |
   |---|---|
   | 'Reveal the draw' | Continue episode |
   | 'Use the veto' | Save {name} (HoH chooses replacement) |
   | 'Keep nominations the same' | Do not use the veto |
   | 'Continue to eviction campaign' | Continue episode |
   | 'Build your case' | Campaign |

5. **The veto meeting is staged in the living room,** as S6/M34 planned: the pre-commit block in the
   red wingbacks, the holder and the HoH on the U's two middle base seats, the house on the U.
6. **Talk sits only on furniture.**
   - Talk_loop moves to the seated talk ring.
   - Listen and the three reactions are re-authored from a standing source.
   - Named talk spots per room hold seat pairs on real furniture or standing pairs.
   - NPC chats and the player's conversations walk to the nearest free spot. When none is free, or
     the walk fails, the conversation opens in place, standing.
7. **The campaign keeps a story beat first.** A beat or plea is drawn inside the stage as the step
   body, never the band, and the way on stays pinned.
8. **The plea's wording is picked from the player's own reading, never the NPC's.**
9. **Pack 8 is imported whole.** Icons/ duplicates Common/, so only one set is named. The
   relationship bars are not named, because their fills are baked.

## 3. Slices

Every slice keeps MOCKUP-PASS's six rules (captions, the knowledge gate, UMA only, no saved field,
3D, cut scenes), and every ceremony-stage change keeps three things for the ceremony session:
- the arrival through `CeremonyActorArrived` → `HouseNpcMotion.IsOnMark`, with no clearance check;
- Report's `place == null` line;
- Release's rig check.

If the ring, the table or its NavMesh changes, re-measure the gap to the approaches (0.7 m or more).

### Wave A: the regression, the talk takes, and the foundation

**A1. Cut scenes every week, and a skip you can see.**
- **One predicate.** `KeepsBody(id)` is read by both `Project`'s SetActive and the NPC world's
  eligibility, so *eligible ⊆ active* holds structurally. The stage term becomes
  `CeremonyStageHolds(id) && (id == departingId || id == walkingOutId)`, and 3af59ae's narrowing
  stays: unstaged evictions still unbind at the commit. This covers FinishWalkOut, the refused
  branch and StartChallenge. The coordinator's hard fail stays.
- **Path B.** For an eviction, set `npcMeetings.DepartureCandidate = departingId` before the stage's
  Reconcile. Principals and EveryoneArrived treat a dropped candidate as refused twice, so the card
  does not wait out the patience in front of an empty hot seat.
- **Loud.** `StopNpcWorld` logs a warning with its reason. `TryBeginCeremonyStage` logs one line
  naming the guard that declined, but only past the policy checks: batch runs, reduced motion and
  the look sheet stay silent, and `LogAssert.NoUnexpectedReceived` stays green.
- **A load mid-week.** The house's world exists after a load in the phases whose commit stages a
  ceremony, as it does in a played season (paused outside Social and Campaign). Measure the test
  impact. No saved field.
- **The summons holds input.** The stage calls `CeremonyOverlays.Showing()` while it narrates, so
  Submit, Cancel, Escape and the shortcuts treat the summons as a card.
- **A visible skip.** A chip on its own overlay canvas, above the cards and clear of the rail, with
  a new stable caption, shown only while a stage or a staged walk-out runs.
  - One press is one step: the summons starts the card jumped to its block or result, a press on the
    card closes it, and a press on the walk-out ends it. The outcome is never changed.
  - The cards' first-press branches become `SkipToResult()`, shared with the device press. The same
    click must never step twice.
- **Tests:**
  - A staged eviction's walk-out leaves the house running (`NpcAutonomyDiagnostic` null), in both the
    arrival and the skip variants. Add that assert to every staged-eviction test.
  - Week 2's and week 3's nominations and evictions are staged after a staged eviction.
  - A load at Nomination stages the nomination.
  - An evicted body that cannot re-bind does not stop the house.
  - A staged variant of the competition started mid-walk.
  - The skip chip's steps, and the summons not pressing the hidden panel.
  - The chip's layout at both font scales, absent in batch runs and under reduced motion.

**A2. Talk stands up, and a seat is sat on.**
- **Clips.** Take Talk out of the standing talk ring (TalkB and TalkC stay) and put Talk_loop in the
  seated talk ring. Re-author Listen_loop and React_nominated, React_saved and React_evicted from a
  standing source (TalkB_loop frame 0 or PoseAtEase_loop) in `HumanoidReactionAuthoring`. The
  controller and the clips are regenerated in a Unity batch run (the lead runs it on D: and copies
  them back).
- **An EditMode test walks GamesimHumanoid.controller.** Every state not named Sit* must stand
  (RootT.y ≥ 0.85, lower-leg stretch ≥ 0.7), and every Sit* state must sit.
- **Sit down on the seat.**
  1. Step onto the seat standing (about 0.35 to 0.4 s) with Seated still false.
  2. Then raise Seated and fit the hips.
  3. Reduced motion sits at once.
- **Stand up on the seat.** LateUpdate checks `exiting` before the owner check. Normal departures go
  through `RequestExit`: Retire, a chat's failed check, ReleaseActivity and HouseFurniturePose.End.
- **Chat seats stop flickering.** A pair that has arrived once stays seated while its lease is
  current and its seats hold, whatever one frame's strict check says.
- **Seating code uses explicit `== null`, never `??`** (HouseFurniturePose.cs:70, DiarySeatPose.cs:56,
  CompetitionArena.cs:223).
- **Tests:**
  - The controller walk.
  - A seated body's hips stay within 0.3 m of its seat on every frame it plays a sit: through the
    sit-down, RequestExit and a lease's Retire.
  - A seated pair stays seated when somebody walks past and when a panel opens and closes.
  - The hips stay above 0.8 of standing height in a standing conversation.

**A3. Pack 8 and the strategy stage.**
- **Import Pack 8** as `Pack8_CampaignVetoNomination` with `bb_ui_pack8.py`. The category is the whole
  folder path, since the manifest nests Campaign/Relationship and Campaign/Voting.
  - UiPackCatalogue entries with the borders measured by `bb_ui_packs.measure`, and PackArt constants.
  - EndScreenKit's glow set learns Pack 8's eleven inset-38 sprites, and a test holds the set to the
    catalogue.
  - Draw draw_slot_empty and draw_slot_filled Simple with preserveAspect.
  - The metas are the ones Unity writes, taken from a D: import (the lead does it at the merge).
- **The strategy stage.** One layout for the four weekly strategy screens (nomination, veto draw,
  veto meeting, campaign):
  - **Frame:** from under the top bar to the frame's foot, from LeftColumnX to 14 short of the right
    edge, which is Pack 8's 1360x800 shells. 'Status' is moved out of its way, taking the last child
    of that name, as HideChromeForFullFrame does. Free time is untouched.
  - **Regions:** a fixed header region for status cards and trackers (children of the modal, outside
    'Episode content', holding no Selectable unless it is a step button), a body that holds one step,
    and a pinned footer.
  - **Footer:** the primary action pinned (exactly one), a secondary slot on its left, and a neutral
    Up next strip. The strip is a PinnedNote overload in accent, not warning, colour. When storylines
    will lapse it shows the AdvanceWarning in warning colour and keeps `AdvanceWarningName`.
  - **Shared pieces:** a `StatusCard` and a `StepTracker` primitive. An opt-in fit-to-height
    parameter on `CeremonyFaces` that keeps every pinned name (`Face · {name}`, `Role`). A header
    variant for a story beat or a reply card that does not switch to the HouseEvent band.
  - **The hint rule.** The panel hint must still read 'Scroll for more' exactly when content
    overflows (EpisodePlayModeTests.PhasePanel.cs:212-214), even while hidden.
- **Adopt the layout** on the four screens with no content change yet, so each is taller at once.
- **Tests:** UiPackImportTests counts (+102 files, +75 sliced) and PackArtTests. The stage covers
  more than 55% and keeps the rail and 'Status' clear. The pinned action is exactly one per screen.

### Wave B: the four screens (after wave A lands)

**B1. The nomination screen** (mockup 72).
- **Status cards: HOH, Phase, Conversations left, Objective.** Every value comes from public or the
  player's own state:
  - 'Conversations left' is `ActionsLeftCount`, labelled 'This window' or 'This week' by BudgetRule.
  - The objective is role-keyed. It never says 'you are in their bottom two'.
- **The tracker** is built from `state.houseEvents` each render: one step per story cycle, then the
  Nomination Ceremony, then the Outcome. A pure step builder goes in Simulation, with EditMode tests.
- **Current-step rule:**
  1. a waiting beat the player opened from the tracker;
  2. otherwise, for a player HoH who has not nominated, the picker (always current, so the walks
     still press the names and 'Commit nominations' at once);
  3. otherwise the first open beat;
  4. otherwise the ceremony;
  5. otherwise the outcome.
- **One step body, never a scroll:**
  - **Story beats.** The text column sits on the left and the tiles on the right. Six options fit in
    three rows. The WHO? step is a grid.
  - **The picker.** The grid fits the height, and 'Commit nominations' is pinned. The comparison
    toggle sits in the secondary slot. The backdoor list moves behind a new 'Plan a backdoor' view.
    The diary path stays exactly as it is.
  - **The ceremony** keeps 'Ceremony title' with the text 'Nomination Ceremony' and WindowLine
    verbatim, in window wording. Faces fit the height.
  - **The outcome** uses WithYou and 'You have made your decision.'
- **The footer.** 'Continue episode' is pinned once on every NPC-HoH step. Up next copy follows the
  phase order. The committing player HoH sees 'Committing lets 1 storyline pass: …'.
- **Fixes:** two live 'Not now', WindowLine wording, the silent lapse, third-person copy.
- **Skins:** Pack 8 nomination_*, status_card_*, phase_*, houseguest_*, stay_off_block_* and nom_risk_*,
  chosen by option type and risk, not by arc.
- **Tests:**
  - No scroll for a story beat (3 and 6 options), the ceremony, the picker, the outcome and a player
    nominee, at FontScale 1.0 and 1.2 and houses of 8 and 16.
  - One story step at a time.
  - The picker is current while The Invite List is open, and Commit warns.
  - Re-pin PhasePanel.cs:199 (HouseStatus) to the HOH card's value, or keep the line.

**B2. The veto player selection** (mockup 76).
- **One row:** AUTOMATICALLY PLAYING (`Face · {name}` cards with HOH and NOM pills) › a draw column
  (`Chip bag` with exactly toDraw `Chip` children on veto_bag_icon and draw_chip_1, and '3 chips to
  draw') › ELIGIBLE FOR DRAW (`Face · {name}` cards, no pill). Card size is solved from the count.
- **One line:** '{seats} players compete: HoH, both nominees, and {n} players drawn at random.'
  'Ceremony title' keeps 'Power of Veto Player Selection' as the small heading.
- **At six or fewer:** no chips, no eligible column, and 'Everyone still in the house plays for the
  veto.'
- **The CTA.** The pinned 'Continue episode' wears reveal_draw_button with a separate headline, 'REVEAL
  THE DRAW' (or 'EVERYONE PLAYS'), and a neutral footnote.
- **The reveal.** After the commit, the chips turn into the drawn faces. It reads the committed
  `vetoPlayers` in a seeded presentation order, and a skip shows the full lineup. It lives in a new
  class, not in CeremonyTakeover (C1 owns that file). Batch runs and reduced motion keep today's card.
  Re-pin CeremonyLifetime.cs:89 and :135-136 only if the takeover no longer plays.
- **Tests:** CeremonyScreens.cs:52-57 moves to 0 chips at six or fewer, plus a fixture of 7 or more
  with 3 chips. No scroll at both scales at 7 and 16.

**B3. The veto meeting screens** (mockups 81, 82).
- **Context strip:** header_strip around the one HouseStatus TMP, whose text stays exact.
- **The holder's card.** One card with two pills when the holder is on the block (VETO plus ON THE
  BLOCK), gold-edged.
- **Player holds:**
  - The Save pair keeps its 'Choice row' and its captions, under a gold 'USE THE VETO' eyebrow, on
    gold_button with icon_veto.
  - 'Do not use the veto' keeps its own row, with a 'Keep nominations the same' subtitle, on
    secondary_button with icon_lock.
  - The info strip reads 'Using the veto removes one nominee and forces the HoH to name a
    replacement.'
- **An NPC holds:** no use or keep controls, and no preview of the NPC's choice. 'Continue episode'
  wears 'HOLD THE MEETING'.
- **Player HoH naming a replacement:** 'Power of Veto Meeting' title and '{Holder} is using the veto
  on {saved | herself | himself | themselves}. Name the replacement nominee.'
- **Player is HoH and holder:** a presentation-only 'who to save' pick, then one candidate list, so
  every caption appears once.
- **After the meeting:** a row built from the week's `ledger.power` (fallback `nominationWeeks`):
  - NOM, NOM, a REPLACEMENT NOMINEE arrow, then the holder with VETO USED or VETO NOT USED;
  - the line is VetoUsedLine or VetoNotUsedLine;
  - 'Continue episode' wears 'CONTINUE TO EVICTION CAMPAIGN'.
- **Engine copy.** `Target` returns the holder's reflexive for an NPC self-save (both the log and the
  memory line). Add an EditMode test in the dotnet subset list.
- **Tests:** PhasePanel.cs:242-300 pins stay. No scroll at both scales. The diary keeps its column.

**B4. The campaign** (mockup 83).
- **Frame.** The strategy stage, with fixed-size type (15/13/12 pt).
- **Hero row:**
  - **Player off the block:** a nominee_hero per nominee: portrait, NOM pill, name, and 'You: Wary -22'
    from the player's OWN outbound reading (the StandingWord vocabulary), with the nominee's
    'Talk to {First}' here only.
  - **Player on the block:** nominee_hero_danger with the whip count from VoteRead.
  - **Titles.** 'Ceremony title' keeps 'Campaign', with a separate headline: 'Build your case' when the
    player is on the block, 'Swing the vote' otherwise.
- **This Week's Situation:** the public facts, from RolesBanner's logic.
- **The plea:** a strip inside the stage, keeping the eyebrow, Title, Message, the three captions in
  'House event choices', oldest first, and '1 of N' as a separate text. The wording is picked from
  the player's own reading (decision 8).
- **Tabs.** Talk to Houseguests, Relationship Intel, Talking Points (plain text, never the
  conversation's captions), Storylines and Voting Outlook, held as view state. 'More ways to campaign'
  and 'Fewer ways to campaign' is the row's trailing control. An Actions Left chip keeps
  'Interactions available n of n' as text, with HaveNotLine under it.
- **The grid 'Campaign voters'.** Cards 'Voter · {name}' with exactly one 'Read' TMP whose parent is
  the card (the vote chip is a sibling behind it). The HoH card has no 'Read' line. The grid pages
  past one row.
- **Bottom cards:**
  - **Goals:** from committed state, via a Simulation reader with EditMode tests.
  - **Recent Intel:** the three newest HouseguestNotes and player-audience events, dated 'This week'
    or 'Week n'.
  - **Pro Tip:** existing rule copy.
- **Folding.** A pending story beat or plea folds the bottom cards into the tabs.
- **Tests:**
  - CeremonyScreens.cs:76-117 extended: no scroll at both scales.
  - A campaign with a pending plea stays on the stage.
  - VoteRead.cs:95-166 unchanged.
  - HaveNots.cs:95.
  - The keyboard ring.

### Wave C: the staged scenes and the talk spots (after wave A lands)

**C1. The veto meeting, staged** (CEREMONY-CUTSCENES 2.3 and C4, MOCKUP-PASS M25 and M34 in part).
- **Capture the block.** Capture `blockBefore` as a List next to `wasNominated` and pass it through.
- **Room.** `CeremonyStageRoom(Veto)` and `CeremonyRoom("veto")` become "Living" (re-pin
  CameraPhase4.cs:66). The summons line reads 'The house takes its seats for the veto meeting.'
- **Seats.** blockBefore[0] and [1] take the red chairs, by id, the player included. The holder takes
  gallery slot 0 (or stays in a red chair if on the block), the HoH the next slot, and everyone else
  sits in cast order. Principals are the block and the holder. Nothing is keyed to the outcome.
- **CeremonyTakeover gets a screen path** (`PlayVetoMeeting(script, …, ScreenSurface)`), with pages
  and beats Opened, VetoDecided and ReplacementNamed (new CeremonyBeatKinds), then Closed.
  - Every line is built from state and blockBefore, never from the event text.
  - The HUD, batch and reduced-motion cards stay byte-identical.
- **Shots:** the wide over the U, the screen, the holder's seat shot, each nominee in turn, the push-in
  on the red chairs, the decision, the HoH then the replacement, the final page.
- **The generic fallback must not frame or react while a stage is up.**
- **Optional tail.** The replacement crosses to the freed red chair, up to 10 s, sequenced so parked
  roots never deadlock. Skip it when the player is involved.
- **Tests:**
  - An AtVetoMeeting fixture.
  - The meeting seats the block in red and plays on the living screen.
  - Both outcomes' beat order.
  - A skipped meeting keeps the outcome.
  - A second week is staged.
  - Batch runs and reduced motion keep the card.

**C2. The eviction's goodbye, the slow walk and the living room's door** (D3, M19, M23).
- **Order.** Build it on A1's predicate: the body is switched off behind a closed door, and the stage
  lets go through `KeepsBody`. Amend §7.4 steps 5 and 7 and M19 to say so.
- **M19:**
  - The result reactions: the evictee looks down, and the survivor stays seated.
  - A Goodbye step of about 4 s: the evictee stands and faces the house, heads turn, seated bodies
    clap, the camera pushes in on them, and the goodbye line plays. The line's tone follows what the
    player knows.
  - The reveal hold stays while a staged walk-out runs.
  - `RouteDeparture` takes a speed (1.5 m/s) with a look mark ahead.
  - The door closes once the body passes the light plane and holds 1.2 s. `FinishWalkOut` runs behind
    it, and the set is struck under a dip.
  - A press skips to the shut door.
  - A warm key light over the red chairs.
  - Standing bodies look (only seated bodies clap).
- **M23:** `OpeningDoorSet.Build` gets an origin. The door goes on the living room's west wall
  (re-measure against the gallery and the south-wall screen; probe the floor with a test first). The
  yard door stays as the fallback behind a switch.
- **Tests:**
  - The walk-out tests extended: door shut at FinishWalkOut, the camera on the door, the house running.
  - The player as the evicted nominee.
  - Three captures (goodbye, door, shut).

**C3. Talk spots** (after A2).
- **Spots.** `HouseConversationSpots` is a runtime table, dressed the way CeremonySets dresses, with
  nothing saved. Its spots group under the six saved rendezvous families:
  - Seated pairs on real furniture, at least 0.9 m apart:
    - the living U's corners and base middle, reusing the gallery's proven approaches;
    - the kitchen table's chairs;
    - the loungers.
  - Standing pairs elsewhere.
  - Spot anchors use their own venue ids, so CeremonySets never strikes them and they are never
    clickable.
- **The coordinator.** A venue holds a spot list. Reserve scores free spots by path length minus a
  seated bonus. A reunion after a load picks any free spot in its family. No schema bump.
- **The player's conversations.**
  - Pressing Talk reserves the nearest free spot in the NPC's room, then the player's room.
  - The NPC walks there on a talk token that is exempt from the panel pause, and the player walks by
    an activity move.
  - When both arrive, they sit on the seats or face each other, then the panel opens.
  - Closing the panel stands them up at the seat and frees the spot.
  - It falls back to opening in place (standing) when no spot is reachable, the walk times out, the
    player presses again, under reduced motion, or in a batch run.
- **Camera.** The conversation focus uses the seat's VisualFocus for a seated subject.
- **Tests:**
  - EditMode: every spot's family is known, its seats pass a furniture check, its pairs keep their
    separation, and it never collides with a ceremony venue.
  - PlayMode: a living-room conversation sits on the couch or stands; an NPC chat prefers the couch;
    a saved chat reunites at a free spot; a round trip saves byte-identical.
- **Offer `StandingPlaces(room)` to the story session.** Do not edit Story.cs.

## 4. Landing

Wave A first, one merge and full suites. The lead also:
- imports Pack 8 on the D: copy and commits Unity's metas;
- regenerates the clips and the controller for A2 on D: and copies them back.

Then waves B and C from wave A's merge, each slice in its own worktree with an adversarial review.
They merge one by one (with `--no-ff` and a message file), then the full suites run on the UMA-free
copy, and the audited pipeline runs alone before the PR. The floors are raised in the merge commits
that add tests. Tell the story session about C3's spots and C1's use of the living room.

## Build log

**2026-09-30, wave A** (merges 7f56b74, 737ee83, d21910c; Unity-written assets 0459cd4)

Each slice was built in its own worktree, reviewed adversarially and fixed.

- **A1.** One predicate, `KeepsBody`, decides both which bodies are switched on and which the house's
  world routes (`RoutedByTheHouse`), so the world never counts a body that is off. That was the
  regression: the first staged eviction's walk-out failed the world, so no stage ran again.
  - The eviction stage sets the departure candidate, so a body that cannot re-bind is let go, not the
    house.
  - `StopNpcWorld` warns, and a declined stage names its guard past the policy checks.
  - The summons and a staged walk-out hold the HUD's input.
  - After a load, the world is built past free time. In batch runs this happens only when stages or
    walk-outs are asked for, so the audited walk is unchanged.
  - A 'Skip ahead' chip (`CeremonySkipChip`, its own overlay) steps the summons, the card and the
    walk-out one press at a time. The cards' first press is now `SkipToResult()`.
- **A2.**
  - Talk plays TalkB_loop. The seated Talk_loop is SitTalkB in the seated ring. Listen_loop and the
    nominated, saved and evicted reactions are rebuilt standing from PoseAtEase_loop (hips 0.978,
    were 0.675) by `HumanoidReactionAuthoring.BuildFromCommandLine`, run on D:.
  - A seat is stepped onto before the sit. A requested exit finishes after its owner lets go.
  - Normal departures go through `RequestExit`, and a chatting pair stays seated once it has arrived.
  - An EditMode walk of the controller now holds every non-Sit state standing.
- **A3.**
  - Pack 8 is imported whole as `Pack8_CampaignVetoNomination`. Its metas are Unity's, from a D: import.
  - `ActivityLayout.Strategy` is the full-height stage beside the rail for the four weekly screens,
    with a pinned footer, a secondary slot and an Up next strip.
  - Shared primitives: `StatusCard`, `StepTracker`, `FaceGrid`, and `CeremonyFaces`' opt-in
    `fitHeight`.
  - The status line moves under the rail, and story beats and pleas are drawn inside the stage.

Suites on the UMA-free copy:
- EditMode: 1984/1984.
- PlayMode: 666/670.
  - CeremonyStage_ACompetitionStartedMidWalkLetsTheStagedEvictedGo opened the screen from too far
    away. Fixed in bfb4c85.
  - Three failures were order-dependent and passed alone and in the next full run.

**2026-10-01, waves B and C** (merges 6b53686 to 103c444; the doorway rebake and the fixes below)

The workflow hit the usage limit after B1 to B4 were built. It was resumed by run id, and the cached
builds went straight to review.

- **B1.** The nomination is a screen of steps (`EpisodeDirector.NominationScreen.cs`).
  - Four status cards and a tracker come from the pure `NominationSteps` (Simulation, 12 EditMode
    tests in the dotnet subset), over one step body sized to the room left.
  - The step is a story beat beside its tiles, the player HoH's picker (now on the stage, with
    'Commit nominations' pinned), the ceremony, or the outcome.
  - Faces switch to rows in a 16-house at 1.2.
  - Fixes: the duplicate 'Not now', the window wording of WindowLine, the silent lapse on commit (now
    warned), and the outcome's third person.
  - Re-pins: EpisodeDecisionContextPlayModeTests:151 (Strategy) and PhasePanel:199 (the HOH card's
    value).
- **B2.** The veto draw is one row: by right, the bag, the pool (`VetoDraw` reader, 6 EditMode tests).
  - At six or fewer nobody is drawn.
  - 'Continue episode' wears 'REVEAL THE DRAW'.
  - In play, `VetoDrawReveal` turns the chips into the drawn faces, 0.9 s per chip. It writes
    nothing, and batch runs keep the field card.
- **B3.** Every veto meeting variant is one screen.
  - The HouseStatus text sits word for word in a header strip, and the holder appears once.
  - The saves keep their Choice row under a gold 'USE THE VETO' eyebrow. 'Do not use the veto' gets
    a 'Keep nominations the same' line.
  - A player who is both HoH and holder picks whom to save, then sees one candidate list.
  - After the meeting, one row is read from `ledger.power`.
  - `EpisodeEngine.Target` gives an NPC self-save herself, himself or themselves.
- **B4.** The campaign is one board (`EpisodeHud.CampaignBoard.cs`).
  - The plea strip; the block as heroes with the player's own reading; the title and a headline; the
    actions chip; a public situation card.
  - Five tabs. Goals, intel and the tip come from the player's own rows (`CampaignBrief`, Simulation),
    and they fold into a tab when something is waiting.
  - `ReplyCards.Message` picks the plea and confrontation lines from the player's own reading
    (decision 8).
- **C1.** The veto meeting is staged in the living room.
  - The block before the meeting sits in the red chairs. The holder and the HoH take the U's two
    middle seats, each on the side they come from (fixed 2026-10-01, below).
  - `CeremonyTakeover.PlayVetoMeeting` pages Intro, Question, Decision, Replacement and Final on the
    living screen, with the new VetoDecided and ReplacementNamed beats. Its lines come from
    `VetoMeetingRead` (Simulation).
  - The crossing tail and bodies rising were left out, because neither could be proven deadlock-free.
- **C2.** A staged eviction ends with a four-second Goodbye.
  - The evicted stand and face the house, seated bodies clap, the camera pushes in, and the goodbye
    line plays. Its tone follows what the player knows.
  - A warm key light covers the red chairs.
  - The walk is slow and watched. The door opens without its flare and shuts behind them, and the
    body goes off behind it.
  - The chrome stays aside for the whole exit, and one press goes to the shut door.
  - M23: `OpeningDoorSet` takes a `DoorLayout`, and the living room's west-wall door stands behind
    `WalkOutDoor`. Its doorway's prototype planter is struck before planting (`Greenery` skips
    inactive nodes), and the speaker and plantSmall2 moved.
  - CEREMONY-CUTSCENES §7.4 and MOCKUP-PASS M19 are amended to the `KeepsBody` shape.
- **C3.** `HouseConversationSpots` are runtime seat pairs (`talk:<family>:<name>`) on the gallery's
  couches and the long table. Each belongs to one of the six saved venues, so nothing new is saved.
  - Pairs score by walk length, with seats counted 3 m shorter. A reunion goes home first.
  - Asked to talk, the player and the houseguest walk to the nearest free place and sit or face each
    other, then the panel opens. Closing it stands both up there.
  - Any failure, reduced motion or a batch run opens the conversation in place, as before.
  - The two-shot frames a seated face.
  - Lounger spots were skipped, because the competition audience reads every seated lounger anchor.

**The living room's door, 2026-10-01.** `HouseLivingGallery.BuildFromCommandLine` was run on the
UMA-free copy:
- It struck the two prototype doorway pieces and rebaked: 28/28 room pairs, 29 approaches.
- The scene, NavMesh, lighting data, lightmaps and reflection probes were copied back. The
  room-finish materials stay in their test form (see the drift note).
- The probe measured the doorway marks flat at y 0.033 with clearance, the mesh's west edge at
  x -13.33, and nothing in either leaf's swing.
- `DefaultWalkOutDoor` is now Living (owner decision 2A). The yard's door is the fallback.

**Fixes from the full suites on the merge (EditMode 2033/2033, PlayMode 715/717):**
- The veto meeting's holder, coming from the east, stood 0.4 m behind the HoH parked on the east
  middle seat. The two middle seats now go by the side each comes from (`FromTheFarSide`).
- StagedExit_TheEvictedPlayerHasNoGoodbyeAndNoWalk found no seed that evicts the player. Its fixture
  now holds the night past its speeches, with every regular ballot against the player.
- With the living room's door as the default, StagedExit_TheDoorShutsBehindThemBeforeTheyGo asks for
  the yard's door, whose sequence it asserts.
- The veto line says "you are" in the middle of its sentence.
- The exit and veto tests ran again on the living room's door: 16/16. Captures looked at:
  - the goodbye, the door and the shut door;
  - the veto used and not used on the living screen;
  - the four strategy screens.

  On the living screen, a red chair's occupant shows in the foot of the screen shot. Kept as an
  over-the-shoulder frame; it does not cover the card's words.

**Open, for the owner:**
- The draw reveal's tempo (fixed at 0.9 s per chip).
- The campaign's 'Answer later' toggle for beats.
- Big-house no-scroll on the HoH-and-holder veto screen (it scrolls past about 13 houseguests at
  1.0).
- The veto meeting's crossing tail.
- Lounger talk spots.
- M23's optional dressing (glow strips, sign, sconces).


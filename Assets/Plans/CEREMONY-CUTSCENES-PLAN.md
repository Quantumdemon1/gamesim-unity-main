# Ceremony cut scenes: the house gathers, sits, and watches the screen

The owner's ask (2026-09-28): *"There should be animations for nominations, when someone wins
veto, when someone wins HOH, when someone is evicted, brainstorm how they will look and be
integrated as cut scenes to make the game more immersive. For instance, the nomination table is
the circular table, there are not enough chairs, there also should be two chairs for the nominees
to sit in on eviction night while the votes are being read out. That should also be a cut to the
screen. Same with the key removal, which is way too small, it needs to be larger as the keys are
revealed."*

## 0. Where the ceremonies stand today, measured

- **Every ceremony is a card, not a scene.** A commit plays a `CeremonyTakeover` (faces and a
  badge), the key ceremony (`KeyCeremony`: one 104 × 134 portrait on a 420 × 330 card hung
  top-centre, keys as 16-unit pips), the vote reveal (`VoteReveal`: two portraits and a row of
  dots), or the competition standings (`CompetitionResult`, a 760-wide list). The camera moves to
  the ceremony's room marker at an 11 m room-wide shot and comes back when the card ends
  (`EpisodeDirector.CeremonyFraming`). The bodies stay wherever free roam left them: the beat's
  subjects play a one-shot reaction (`ReactToCeremony`: Nominated, Saved, Evicted, Won) and every
  other head turns toward them for seven seconds (`TurnHeads`). That is the whole "animation".
- **The nomination room is dressed but unused.** `HouseSetPieces` authors a round table on the
  round rug, six `chairModernCushion` chairs round it at 60° intervals, two floor lamps, two
  plants, two speakers, and the ceremony screen (`bb_set_ceremonyscreen`) on the north wall, which
  is also the episode-screen station the player walks to. The chairs are decoration: no
  `HouseInteractionAnchor` seats anybody in them (the only seated anchors in the house are the two
  dining chairs and the two loungers). Six chairs seat an eight-house's nominees-plus-safe minus the
  HoH only by accident, and a twelve-house not at all.
- **The living room** has a prototype sofa with one authored seat and no other seats. Eviction
  night frames the room, plays the reveal card over it, and then the evicted walk out through the
  front door (`EpisodeDirector.WalkOut`: the one real cut scene, with the camera following and the
  door opening).
- **The machinery a cut scene needs exists.** `OpeningStage` walks every houseguest to marks in
  order, turns them to the lens, poses or dances them on the mark and sends them off
  (`Send/Present/SendOff`, a `DoorShot` and a `PushInShot`, a crane). `HouseSeatPresentation.Begin`
  sits a body on a seated anchor and gets it up again. `HouseCameraRig` has `MoveTo(shot)`,
  `CutTo`, `FocusSubject`, `RetargetShot` and the conversation two-shot. `LiveFeed` renders a
  second camera into a texture. The story's staging holds up to four cast on marks for a beat.
  The cards already raise cues (`KeyShown`, `BlockShown` suggested in the motion brainstorm;
  `CueRequested` today) the bodies can act on.

So the work is not new animation so much as **a stage manager for ceremonies**: gather the house,
seat it, run the beat on the set's own screen with the camera cutting between the screen and the
faces, act it out on the bodies, and let everybody go.

## 1. The shape every ceremony shares

A `CeremonyStage` (a sibling of `OpeningStage`, owned by the director) runs one ceremony from the
commit that decides it to the release of the house:

1. **The summons (3–6 s).** The status line and the objective say "The house gathers for the
   nomination ceremony". Every houseguest is sent to their mark (a chair or a standing spot) by
   `HouseMeetingCoordinator` legs exactly as the opening sends them to the door; conversations
   pending are cancelled the way a phase change cancels them. The player is walked too (an activity
   move they can skip past by pressing, as the walk-out can be skipped) or, if far, cut to their
   mark under an opaque frame. The camera holds a wide establishing shot of the set from the room's
   marker at 11 m while the chairs fill.
2. **The seating.** Each mark is a seat: `HouseSeatPresentation.Begin` on a chair anchor, the way a
   dining chair seats somebody today. The one who runs the ceremony stands.
3. **The beat, on the screen and in the room.** The card that narrates the ceremony plays **on the
   set's screen**: the overlay renders into a `RenderTexture` shown on the ceremony screen's face
   (the `LiveFeed` pattern in reverse), and the camera cuts between a **screen shot** (a
   head-on medium shot of the screen, filling the frame: this is the "cut to the screen") and
   **reaction shots** (the two-shot of the person just named, the HoH at the table's head, the
   block). The overlay is still drawn to the HUD canvas as well at a reduced size in the corner, so
   nothing a screen reader or a test reads today is lost, and reduced motion keeps only that.
4. **The reactions, timed to the beat.** The card raises an event per beat (`KeyShown(id)`,
   `BlockShown`, `VoteShown(index)`, `ResultShown`, `WinnerShown`); the stage answers each on a
   body: a seated exhale (`SitIdle` with a head drop, or the seated clap/fist pump where it fits),
   the nominee's `React_nominated` from the chair, a fist pump for the winner, the evictee standing
   up. Heads turn to whoever the beat is about (`TurnHeads`, which exists).
5. **The release.** The camera returns to the viewer's shot, the bodies get up (seat `End`), the
   house's world resumes, and the walk-out (eviction) or the wander begins. A skip press at any
   point jumps to the result exactly as the cards skip today: the order is given up, never the
   outcome, and a batch run and reduced motion play only the cards, as the opening stage is skipped.

Everything reads committed state. The stage is presentation: it commits nothing, draws from no
generator, and adds no saved field. A save mid-ceremony reloads to the state after the commit with
the card already resolved, as today.

## 2. The four ceremonies

### 2.1 The nomination ceremony (the keys)

**The set.** The round table in the nomination room, with a chair for everybody who draws a key
and the block: **house size − 1 chairs** (the HoH stands at the head, in front of the screen).
`HouseSetPieces` places chairs at 360°/(N−1) intervals on the rug's radius for the roster size the
scene is dressed for (twelve, the largest), and the director enables only the first N−1 in cast
order, so an eight-house sees seven and the rest are struck. Each chair gets a seated
`HouseInteractionAnchor` (venue `nomination-seat`, slot i, the chair's yaw, approach from behind:
0.70 m as the dining chairs), authored by `HouseInteractionAnchors.EnsureDefaults` like the
loungers. **A key box** on the table (a new authored prop, `bb_set_keybox`: a lidded box with one
key slot per draw; the keys are the icon the card already uses, as small props) is the thing the
HoH's hand goes to.

**The shot list.**
1. Wide from the screen's side of the room, the chairs filling (the summons).
2. The HoH at the head, standing (`PoseHandBehindHead`/the surveying pose from the brainstorm),
   over the line "X has made their decision" on the screen.
3. Per key: **cut to the screen**, the key ceremony drawn large: the safe houseguest's face at
   **the full height of the screen** (the screen's face is roughly 2 × 1.2 m in the set; the card's
   stage becomes 3:2 and the portrait ~70 % of it), their name, "SAFE", and the row of keys along
   the foot **at key size, not pip size** (each key ~1/12 of the screen's width, lighting up as it
   is handed out). Then a **reaction shot**: a two-shot of the named houseguest in their chair,
   who exhales (a short authored seated relief: `SitIdle` with the head drop from `React_saved`'s
   first second) while the neighbours glance at them (`LookAt`).
4. The beat before the last key: the screen holds the gold key; the camera slowly pushes in on
   the two or three still waiting (a `PushInShot` from 4.6 to 3.1 m over the beat).
5. The block: **cut to the screen**, the nominees' two faces side by side, "NOMINATED FOR
   EVICTION"; then a reaction two-shot on each nominee (`React_nominated`, seated), and the room's
   heads turn to them.
6. Release: the HoH's line closes; the chairs empty as the house's own routine resumes (the
   nominees go to the diary room in the story's own time).

**Why larger keys are more than a size change:** the card's portrait is 104 units wide because it
sits at the top of a 1600-wide frame under the top bar. On the set's screen it is the frame: the
screen shot is a head-on medium of a 2 m screen, so the face is a third of the viewer's height.
The HUD copy of the card stays for reduced motion and for the tests, in the corner at today's size.

### 2.2 The HoH competition win

**The set.** The yard, where the competition already stages its arena (`CompetitionArena`). No
chairs: the field stands where it played, on the arena's stations.

**The shot list.**
1. The result card (`CompetitionResult`) stays the way the standings are read, but it now waits a
   beat: the camera cuts to the **winner on their station**, who plays `React_won` (the 8.5 s
   celebration, cut at 3 s) or a fist pump, while the field turns to them; a second cut to the
   player's own station if they placed (their attempt line reads over it).
2. **The key handover**: the new HoH walks to the nomination room's head mark (the summons is
   just them), the screen lights with "HEAD OF HOUSEHOLD · WEEK N" and their face, and they hold
   the HoH key up (`PosePowerStance`, or the seated victory if they sit first). The rest of the
   house drifts to the living room. This is where the HoH room reveal from the motion brainstorm
   lands: the walk continues into the suite, the door opens, and `SitVictory` on its sofa.
3. Release to free roam with the objective card reading the week's next stop.

For the veto and the final HoH parts, the same result beat without the handover.

### 2.3 The veto meeting (the veto win is 2.2's result beat; this is the decision)

**The set.** The nomination room again, the block seated with the veto holder standing at the
head beside the HoH: the format's own staging.

**The shot list.**
1. Summons and wide.
2. **Cut to the screen**: the veto holder's face, "POWER OF VETO", then the question. The holder
   strikes the power pose from the brainstorm (measured on a woman's and a man's body).
3. The decision: the screen shows "USED ON X" over the saved nominee, cut to a two-shot of them
   standing up out of the chair (`React_saved`, standing), the HoH named the replacement, cut to
   the replacement's chair (`React_nominated`); or "NOT USED", cut to the block, who stay seated
   and look at each other.
4. Release.

### 2.4 Eviction night (the vote)

**The set.** The living room. **Two hot seats** for the nominees: two chairs (the authored
`bb_set_diningchair`, or armchairs) placed side by side facing the living room's screen
(the Games room has a television cabinet; the living room gets a `bb_set_ceremonyscreen` of its
own on its north wall, or the vote plays on the nomination room's screen with the sofa facing it:
the owner's call, see §5). The rest of the house on the sofa and standing behind it, the HoH
standing to one side (they vote only to break a tie). Each hot seat is a seated anchor
(`hot-seat`, slots 0 and 1); the sofa gets three seated anchors (`sofa-seat`, slots 0–2) so the
first three arrivals sit and the rest stand on marks behind it.

**The shot list.**
1. Summons and wide: the nominees walk to the hot seats last, and sit leaning forward, hands
   between the knees (`SitLounge_loop` with the lean, or a new seated take: "Sitting Nervous").
2. **Cut to the screen**: the vote reveal drawn large, the two faces with their counts under them,
   the host's line ("The house has voted").
3. Per vote: the count ticks on the screen; **cut to the nominees** (the two-shot over the hot
   seats, from the screen's side) for a beat: whoever the vote went against flinches (a small
   authored seated twitch), the other keeps still. Every third vote, a cut to a voter on the sofa
   looking at their hands.
4. The beat before the last vote: the push-in on the hot seats.
5. The result: **cut to the screen**, "EVICTED", the face; cut to the hot seats: the survivor
   fist-pumps (`SitVictory`), the evictee stands (`React_evicted` from standing), and the room's
   heads turn to them; hugs are a later take.
6. Release into the walk-out that already exists: the evictee's goodbye on the strip, the camera
   following them to the door, the door opening. The house stays seated until they are through
   it, then gets up.

Finale night (jury questioning, the winner) keeps the jury standing along the living room as it
does now; the winner's reveal takes the hot seats for the finalists and the screen for the votes,
the same shape.

## 3. Integration

- **`EpisodeDirector.CeremonyStage.cs`** (new): the stage manager of §1, modelled on
  `OpeningStage`: `TryBegin(kind, state)` on the commit, `Tick()` from the director's update,
  `End()`. It owns marks per ceremony (from the seated anchors it finds by venue id, and standing
  marks it computes round the set), the shot list as a small script of timed cuts keyed to the
  card's events, and the release. It never runs when the walk-out, the opening stage or the
  competition arena is up, and never in a batch run unless asked (`StagesInBatchRuns`, as
  `WalkOutsInBatchRuns`).
- **The cards on the screen.** `KeyCeremony`, `VoteReveal` and `CompetitionResult` get a second
  render target: a `ScreenSurface` (new, Presentation) that owns a `RenderTexture` and a camera
  looking at a copy of the card's canvas laid out at the screen's aspect (3:2), and a material on
  the ceremony screen prop's face that shows it while a ceremony plays and shows the idle branding
  otherwise. The HUD copy of each card stays as it is (tests read it by name; reduced motion and
  batch runs see only it). Each card raises typed events per beat, which the stage subscribes to.
  `CeremonyPacing` keeps the timings; the stage's cuts follow the card, never the other way.
- **Seats and marks.** `HouseInteractionAnchors.EnsureDefaults` authors `nomination-seat` (N−1
  chairs), `hot-seat` (2) and `sofa-seat` (3) anchors from the props `HouseSetPieces` places; the
  meeting coordinator learns a `ReserveCeremony(venue)` that leases every seat of a venue at once
  and a `SeatCast(assignments)` that walks and sits each body, reusing the walk legs and
  `HouseSeatPresentation.Begin`. The player is seated by the same path, with input off and E to
  skip the walk (their body is cut to the mark).
- **Camera.** `HouseCameraRig` gains a `ScreenShot(prop)` (head-on medium of the screen's face
  at the distance that fills the frame's height with it) beside the existing two-shot; the stage
  uses `MoveTo(shot, seconds)` for pushes and `CutTo`-style 0.01 s moves for cuts (the reduced
  motion path already does exactly this).
- **Bodies.** Two seated takes to add through the mocap pipeline (adding-a-mocap-take):
  "Sitting Nervous" (the hot seat) and a seated relief; the standing reactions exist. The seated
  clap and fist pump are wired. Every new take goes through `TakeProbe` for travel before wiring.
- **Audio.** The cards' cues stay; the stage adds the summons chime and a room tone per set.
- **Tests.** EditMode: the anchors exist per roster size, the marks clear the props (the start-spot
  audit's pattern), the shot script's cuts land on the card's events. PlayMode: a nomination on a
  director season seats N−1 bodies and stands the HoH (measure the hips, not the root), the vote
  seats the nominees in the hot seats, the screen material shows the card's texture while it plays
  and the branding after, and captures of each screen shot for the look sheet. The audited walk
  stays on the cards (batch runs skip stages).

## 4. Milestones

| | Milestone | Done when |
|---|---|---|
| C1 | Seats: N−1 chairs at the round table with anchors, the two hot seats and the sofa's three, `SeatCast` and the summons | A nomination seats the house and stands the HoH on a director season; the audit passes for 6, 8 and 12 |
| C2 | The screen: cards rendered on the ceremony screen's face at 3:2, larger faces and keys, `ScreenShot`, the cut between screen and faces | The key ceremony's face fills a third of the frame in the screen shot; the HUD copy unchanged |
| C3 | The beats: per-beat events from the cards, seated reactions, the push-in before the last key and the last vote | Captures of the block and the result show the named bodies acting |
| C4 | The HoH handover and the veto decision as stages; the winner takes the hot seats on finale night | The walkthrough's ceremony frames are staged, not carded |
| C5 | Polish: the summons copy, room tone, the player's skip, the settings' quick pace through the stage | Full suites and the standalone walk green; the story session's staging tests unaffected |

## 5. Decisions for the owner

1. **Where the vote is read:** the living room with a screen of its own (a second
   `bb_set_ceremonyscreen`, the format's) or the nomination room's screen with the hot seats there.
   The plan assumes the living room.
2. **Chairs:** modern cushion chairs as dressed, or the authored dining chairs (which already have
   seat anchors and the 0.70 m approach worked out). The plan assumes the dressed chairs with new
   anchors.
3. **The player's summons:** walked with everybody (6 s of not being in control) or cut to the
   mark under an opaque frame. The plan assumes walked, skippable with E.
4. **How long a ceremony runs at the suspenseful pace:** the keys take about 50 s today; the stage
   adds the summons (up to 6 s) and the reaction cuts (a beat each, inside the card's holds), so
   about a minute. Quick pace halves it as it does the cards.

## 6. What is built (2026-09-28, C1-C3 for the nominations and the eviction)

Built on the plan's own assumptions in §5, none of which were answered: the vote is read in the
living room on a screen of its own; the dressed chairs are the seats; the player walks with
everybody and any press (a click, Enter, Escape, the pad's A or B - the cards' own skip, not E,
which stays the interact key) starts the card early; the summons is 7 s at the suspenseful pace
and 3.5 s at quick.

- **The sets** (`CeremonySets`, `CeremonySeating`; Runtime/House): dressed at runtime the first
  time a ceremony is staged, from the props the scene carries, never re-saved. The round table's
  ring is re-laid for N−1 chairs - the six dressed chairs moved onto it, more cloned from them for
  a house over seven, the extras struck for a house under seven - with one slot kept empty at the
  head, the side nearest the screen, and the ring widening when N−1 chairs would sit closer than
  0.75 m. Each chair is a seated `nomination-seat` anchor approached from behind at 0.70 m; two
  standing `nomination-head` marks stand a step out from the head slot. The living room gets the
  set's screen cloned into the prototype television's place (the television struck), two chairs
  from the table's as the `hot-seat`s facing it, the sofa turned to face it with its cushions and
  three `sofa-seat`s on it, and `living-mark`s behind the hot seats and beside the screen for the
  Head of Household, each on the walkable floor and clear of the furniture layer.
- **The screen** (`ScreenSurface`; Presentation): the screen's face measured from its renderers.
  A card that plays on it becomes a world-space canvas hung on the face at the screen's own shape
  (1200 × 800): the same card, the same children by name, laid out for the screen - the key
  ceremony's safe face 425 high on an 800 frame with the keys along the foot at 76 (closer only
  when more than eleven are in play), the vote reveal's two faces 240 across with the counts 96
  high. Nothing is rendered twice, and the HUD's own layout is untouched: the cards' `Play` take
  the screen as an argument, null being the HUD. `ScreenSurface.Shot` is the head-on cut that
  fills the frame's height with the face.
- **The beats** (`CeremonyBeat`): the key ceremony, the vote reveal and the takeover raise
  `BeatReached` as they reach each beat - opened, a key or a vote with who it is about, the beat
  before the last, the block, a tie and its breaking, the result, closed - marked skipped when a
  skip reached them at once. The cards keep the order and the timing.
- **The stage** (`EpisodeDirector.CeremonyStage.cs`): on a nomination or an eviction commit the
  reveal is handed to a stage instead of played, when the house can stage it (never under reduced
  motion, in a batch run unless `StagesInBatchRuns`, over the opening, the arena or a walk-out, on
  finale night, or with `CeremonyStages` off - the look sheet turns it off). The summons: the strip
  says the house gathers, the coordinator's ceremony leases
  (`HouseMeetingCoordinator.CeremonyStaging.cs`, exempt from the house's pause as the opening's
  are) walk everybody to their place's approach, the player by an activity move, and whoever was
  left out - a route blocked by somebody still standing on it - is asked again every 1.5 s. Each
  arrival sits (`HouseSeatPresentation`) or stands facing the way the place faces; the player sits
  by the diary's sequence. The card plays on the screen once everyone has arrived, the summons has
  run its course, or a press - and past the summons it waits for the people the card is about (an
  eviction's nominees, a nomination's or a veto's Head of Household; `SummonsPatience`, three and
  a half summons more) while they are still on their way, because a nominee starting at the far
  end of the yard walks thirty metres and no eviction plays to an empty hot seat; the chrome is
  held aside from the summons, as it is for a reveal. The evicted stay in the house's world while
  the stage holds a place for them (`CeremonyStage.Holds`) and while they walk out; the commit
  makes them a non-contestant at once, and the world would otherwise unbind their body on the spot
  so they could take neither the hot seat nor the door (measured on 2026-09-28: the second nominee
  stood unbound at her start spot through the whole stage). Because the commit's projection runs
  before the stage exists, the stage reconciles the world once more when it is created, and the
  summons' retries send them once they are bound again. Outside a stage they go as they always
  did - unbound at the commit, let go by the walk-out if their body cannot take its navigation
  back (the walk-out's own test held the wider rule to account). The stage seats the evicted in a
  hot seat although the committed state no longer counts them active, and looks bodies up by
  `BodyFor`, since `Housemates()` is the active contestants.
  The cuts: the screen on every beat; for a key, the named face from in front of their chair at
  half the key's hold, the neighbours glancing at them and them at the Head of Household, the
  seated fist pump on the last safe key; before the last key, a push-in on those still waiting;
  the block, then each nominee in turn with their head dropping and the room's heads turning; for
  a vote, the hot seats' two-shot with the one voted against glancing down (every third vote a
  voter on the sofa looking at their hands), back to the screen before the next; before the last
  vote, a push-in on the hot seats; the result, then the survivor's seated fist pump and the
  evicted standing up out of the hot seat into the standing take. Every shot shares the 40°
  lens, so a cut never eases the lens, and every shot in front of a face stops short of the
  screen, because the rig's occlusion looks through furniture. The release hands the camera back
  (`ReleaseShot`) and lets everybody go; after an eviction the house keeps its seats while the
  evicted walk out and gets up once they are through the door. A new commit, the arena or a
  season replaced ends a stage at once, and a stage that could not gather the house plays its card
  on the HUD as before.
- **Tests:** `CeremonySeatingTests` (EditMode, a preview scene dressed by name at the house's
  numbers: N−1 seats for 6, 8, 12 and 16 with the head open and room between the chairs, a dressed
  house left alone and a smaller one striking its extras, the living room's screen, hot seats and
  turned sofa); `EpisodePlayModeTests.CeremonyStage` (a nomination gathers and seats the house on
  a director season with the Head of Household standing and the keys on the screen's world-space
  canvas, an eviction seats the nominees in the hot seats and hands the evicted to the walk-out, a
  batch run stages nothing unless asked, and the look sheet's captures of the keys on the screen
  and the block).

**Not built, in the order to build it:**

- C4: the veto meeting (the takeover on the screen: it has the beats, not the screen layout) and
  the Head of Household's handover after a competition; the finalists in the hot seats.
- The seated takes (a nervous sit for the hot seats, a seated relief for the keys): the mocap
  pipeline, adding-a-mocap-take. The keys use a head turn and the seated fist pump meanwhile; the
  nominees' drop is a look, not a take.
- The HUD copy of the card in the corner while it plays on the screen (the plan's reduced-size
  copy), and the screen's idle branding: today the card is only on the screen while the camera
  is on it, and the face shows its authored emissive material otherwise.
- Dressing the sets at season start rather than at the first ceremony, once the start-spot audit
  is proven against the moved sofa and the living room's screen.
- C5: room tone per set, the summons chime.

## 7. Round two: the table, the couch and the walk out (2026-09-28)

The owner, after playing C1-C3: the nomination table still has too few seats and houseguests stand
around it; on eviction night there is no couch, so the house runs out of seats and the nominees do
not even get the hot seats, because other houseguests are in them first; and the evicted do not
leave so much as wander off - they walk about the house and vanish. What they want instead is the
intro played backwards: the same front door, opened for a houseguest who is sad rather than
excited, walked through, and shut behind them.

This section measures what the code does today against each complaint, names what to build, and
puts a diagnosis in front of the seating fixes, because two of the three complaints describe
things the code as written cannot do, and a fix built on reasoning alone would be a fix for the
wrong thing (the-evicted-leave-the-committed-state-at-once: every one of C3's seating faults was
found by a report, not by reading). Nothing here touches the story session's files.

### 7.0 What the code does today, measured against the complaints

**The table.** `CeremonySets.DressTheTable` lays N−1 chairs for `state.Active.Count()` houseguests
on one ring round `tableRound` (the table at scale 0.78 at (0.075, −14.6) in a room 9.38 × 10,
x −4.6…4.8, z −20…−10, the screen on the south wall at z −18.9). The ring's radius is
`max(1.10, 0.75 / (2 sin(step / 2)))` with the head slot left empty, so:

| houseguests | chairs | step | ring | approach ring (0.70 m behind) | approach pitch |
|---|---|---|---|---|---|
| 6 | 5 | 60° | 1.10 m | 1.80 m | 1.9 m |
| 12 | 11 | 30° | 1.45 m | 2.15 m | 1.1 m |
| 16 | 15 | 22.5° | 1.92 m | 2.62 m | 1.03 m |

`Assign` stands the Head of Household at the head and gives every other active houseguest a chair,
so the count is never short: whoever stands at a nomination is somebody who did not reach or take
their chair. Three ways that happens, none of them counted today:

1. *Late.* The card starts at the summons (7 s, `SummonsSeconds`) as soon as the one principal -
   the Head of Household - is in place; everybody else arrives during the keys and sits as they
   arrive (`SeatArrivals`). A houseguest starting in the yard walks 30 m, about 14 s, so at a full
   house half the cast is still walking through the first keys, in the wide and in the cuts.
2. *Stuck.* A body that stops short of its approach - the spot taken by another body, or the
   route pinched between chairs - never reports `HasArrivedAt` (the motion needs a physical arrival
   two frames running inside `stoppingDistance` 0.15), and `SendTheLeftOut` only re-sends bodies
   with no lease, so a leased body that stalled stays standing beside its chair for the whole card.
   At sixteen the approaches are a metre apart on the ring, which is where fifteen bodies with
   avoidance meet.
3. *Never sent.* `BeginCeremonyStage` leaves out anybody holding a conversation lease at the
   commit ("in a conversation") or with no route; the retry every 1.5 s asks again, but a refusal
   is logged once, at `Begin`, and nothing is logged at the card's start or its end about who is
   not seated.

Also: at a full house the ring stands a metre and more off the table's edge, so even a seated
house reads as sixteen people sitting round a small table, not at it. And the ring test proves the
layout on the preview scene while the PlayMode stage test runs at the director season's cast, not
at sixteen (`EpisodeValidation.MaximumCast`, and the motion's sixteen bodies).

**The living room.** `DressTheLivingRoom` gives the eviction two hot seats 2.0 m in front of the
screen's stage (the screen clone stands at (−5, −1.25) where the prototype television hung; the
hot seats at (−5.55, −3.25) and (−4.45, −3.25), approached from behind at z −3.95), the sofa turned
where it stands at (−1.4, −4.8) - 3.6 m east of the hot seats, off the room's axis - with three
seats, and standing marks: one beside the screen for the Head of Household, the rest on two rows
1.7 and 2.7 m behind the hot seats, a metre apart, after the filters (inside the room's bounds by
0.5 m, 1.3 m off the sofa, 0.9 m off the hot seats, on the NavMesh, clear of the furniture
layer, which take a few). About seventeen places at sixteen,
of which five are seats: eleven houseguests stand through the vote.

The nominees and the hot seats: `Assign` gives the hot seats to `state.nominees` by id before
anybody else is placed, `SeatArrivals` sits each body on the place its own id was given and never
by proximity, and the free-time layer's seated venues are the authored `Meetings`
(`kitchen-table-chat`, `yard-lounger-chat`; `living-east-chat` is standing) and never the
ceremony's anchors. **The code as it stands cannot seat anyone but a nominee in a hot seat.** What
the owner saw is one of two things, and the report in 7.1 settles which: the sofa's three, seated
and facing the screen a few metres from the hot seats while the nominees were still walking (the
card waits for them past the summons, up to `SummonsHardSeconds`, 24.5 s at the suspenseful pace
and 12.25 s at quick, which a nominee at the far end of the yard can miss at quick); or a build
before 3af59ae, in which the evicted nominee stood unbound at her start spot through the whole
card. Either way the design below leaves no room for it: everyone seated, the nominees' arrival
its own beat, and the card never up before the nominees are down.

**The walk out.** `EpisodeDirector.WalkOut.cs`: leg 0 routes the evicted to the yard's inside
mark (−1.6, 14.1), builds the door set in the yard that same frame, and puts the camera on the body
(`FocusSubject`); leg 1 waits for the arrival and opens the door; leg 2 routes them to the deck
mark (−5.3, 13.8) once the leaves are 0.35 open; leg 3 ends the walk the frame they arrive:
`FinishWalkOut` destroys the set, projects the state and the body is gone. From a hot seat at
(−5, −3.25) the inside mark is about 20 m away through the bedroom wing and the doorway at (0, 10),
nine seconds at the agents' 2.2 m/s, every one of them on camera at the body's heels: that is the
"walking around the house". At the door there is no shot of the door - the camera is still on the
body - the leaves are never closed, nothing holds, and the body is switched off two metres past the
facade, which is the "disappear". The only sad beat is `ReactEvicted` at the hot seat as the card's
result lands (§6). The house does keep its seats until the walk ends (`Release` waits for
`walkingOutId`), but nobody turns to watch them go.

The intro's door beat, which this is to mirror (`OpeningSequence.Cards.cs`, `DoorReveals`): the
door shot - the opening stage's `DoorShot`, inside the yard at 4.1 m looking west at the closed
leaves (focus (−2.0, 1.85, 13.8), yaw 270, the 40° lens, 0.3 depth of field) - the guest walks up
behind the door (`OnDeck`, `ToDoor`), the door opens with a flash and the camera pushes in a metre
(`PushInShot`, 0.5 s), they walk through to the reveal mark (−1.6, 14.1) and present themselves
under their lower third, then go off into the house (`SendOff`); the door shuts for the next
(`CloseDoor`). `OpeningDoorSet` has `Open`, `Close`, `Openness` and an opaque light plane at
x −4.4 that hides whoever is behind the doorway. All of it exists; the walk out simply uses none of
it.

### 7.1 First, the measurements (D0)

One commit, no behaviour change, so the owner's next play session answers the seating questions:

- **The stage report in play.** `CeremonyStage` logs, at the card's start and at the release, one
  line per place: id, venue and slot, whether the coordinator holds a lease for them, arrived,
  seated, the body's flat distance to its approach, and the agent's `hasPath`, `pathStatus`,
  `remainingDistance` and velocity; and the `Not gathered` reasons as they were at `Begin` and at
  each retry. `EpisodePlayModeTests.CeremonyStage.cs`'s `StageReport` is the template; it moves
  into the stage behind a `Debug.Log` so the Player.log carries it
  (`%USERPROFILE%\AppData\LocalLow\<company>\<product>\Player.log`).
- **The full-house run.** The stage tests gain a sixteen-houseguest fixture (a season built at
  `MaximumCast`; `InstallDiaryFixture` to pin who is nominated and evicted, since NPC ticks run on
  real time and a walked season does not fix its outcomes) and print the report on failure. Two
  captures on the look sheet at the card's start: the nomination from the head of the table, the
  eviction's wide. Both on a screen of the test's own (capture-frames-trip-the-competition-stall-
  guard) with `Time.captureDeltaTime` pinned (frames are not seconds).
- **What the report decides.** For each standing houseguest, which of the three it is - late,
  stuck or never sent - and for each hot seat, who held its lease at the card's start. A hot seat
  held by a non-nominee names a bug the report will show; the code cannot do it today, so that
  answer means either a build older than 3af59ae or something new, and it is chased before 7.3 is
  built. The owner sends the log from a nomination and an eviction at their own cast size.

#### 7.1.1 What D0 measured (2026-09-28)

Built: the stage logs its report at the card's start, at the release and when it ends before its
card, with the coordinator's refusal per houseguest kept from every retry
(`EpisodeDirector.CeremonyStage.cs`, `Report`); `EpisodeDirector.CeremonyStageReport` reads it
for a test. The fixture: a house of sixteen, the cast screen's own season at the roster's
largest (twelve) with four added the way the cast-size tests add them, and the stage tests'
two full-house measurements (`CeremonyStage_AFullHouse…`) beside the six-house ones. The
fixture's bodies start together near the template, so every route here is short: the lateness
below is understated against a played season, where a nominee starts in the yard.

| ceremony | house | at the card's start | eight seconds in | at the release |
|---|---|---|---|---|
| nomination | 16 | 6 of 16 seated; the card started at the 7 s summons with the Head of Household in place and ten still walking, one 18 m away | 12 of 16 | 11 of 16 (the test skipped the card at 9 s; one seat had ended) |
| nomination | 6 | 2 of 6 | | 5 of 6: everyone but the head |
| eviction | 16 | 4 of 16: both nominees in the hot seats by id, the player and one more on the sofa; one stuck at the sofa; eleven standing on the marks | 4 of 16 | 4 of 16 |
| eviction | 6 | 4 of 6, one stuck at the sofa's third seat | | 4 of 6 |

Four mechanisms, each named by the report and confirmed in the code:

1. **Late.** The card starts at the summons the moment the one principal is in place
   (`Tick`, the Summons case). At sixteen the keys play to six seated and ten walking; the
   rest sit as they arrive, in shot. 7.2's rule - the card waits for everyone up to a cap keyed
   to the longest route - stands, measured.
2. **Stuck short of the approach.** `HouseNpcMotion.PhysicalArrival` wants the root within
   0.25 m of its destination at under 0.2 m/s. A seated body's root stays parked on its approach
   as a stopped agent (a seat moves only the visual body), and the sofa's three approaches are
   0.52 m apart, so the third body stops 0.18 to 0.35 m short between two parked agents and
   never arrives - at six and at sixteen, the sofa's second or third seat every time. The hot
   seats, 1.1 m apart, never suffer it. Any seat row at the sofa's pitch will, until a seated
   body's parked agent is made passable (radius and avoidance off, restored when they rise).
3. **Arrived, not seated.** `HouseSeatPresentation.LateUpdate` ends the seat once the root has
   moved 0.4 m from where it sat down. A root pushed by neighbours squeezing past to their own
   approaches - or walked back by the motion's own re-path, since a push that breaks the
   arrival turns Arrived back into Walking - loses the seat, and `SeatArrivals` never seats an
   id in `arrived` twice. Two at the table at sixteen; one of them was seated at eight seconds
   and up again by the release. The fix is to retry: an id leaves `arrived` when its seat ends.
4. **The eviction's summons refused whole.** `TryBeginCeremonyStage` reconciles the world to
   re-bind the evicted before `Begin`; the re-bound body is still Binding on that frame, so the
   coordinator's `IsReady` is false and `BeginCeremonyStage` refuses everybody - "The house is
   not free for a ceremony" - and the 1.5 s retry sends them. Every eviction, never a
   nomination: the strip says the house gathers while nobody moves. The summons has to begin
   once the coordinator is ready, or the coordinator refuse only the unbound, as its retry does.

Not reproduced: a non-nominee in a hot seat. Both nominees held and took the hot seats by id
at six and at sixteen; the owner's own log at their cast size is still wanted for it. Seen in
the captures (`ceremony-stage-full-house-table`, `-table-later`, `-living`, `-living-later` on
the look sheet): fifteen chairs on a ring round a table a third their width, four still
standing round it at eight seconds; and eleven in two standing rows behind two hot seats and a
sofa turned in its place off the room's axis.

One consequence the reports force on 7.2 and 7.3: **a moved or cloned set piece leaves no hole
in the baked NavMesh.** The turned sofa's hole is where the sofa was; the cloned hot seats and a
grown table have none; bodies path through such a piece and an approach sampled beside it lands
on the old hole's edge. So the gallery's rows and the grown table cannot be runtime clones: the
gallery is authored in the scene as set pieces with fitted collision proxies and baked in the
rebake slice, `CeremonySets` adding only the anchors, and the sofa stays where the scene puts
it rather than being turned at the first eviction. Decision 3 collapses to A in that form. D1
gains the summons begun when the coordinator is ready, seating retried for as long as the stage
runs, and the parked agent made passable, and keeps the card waiting for the house.

#### 7.1.2 D1's mechanics, built (2026-09-28, five filtered runs)

Everything the reports named, fixed the same night and measured again at six and at sixteen:
all fourteen stage and walk-out tests pass, and at sixteen the keys play to fifteen seated round
the table with the Head of Household standing at the head, and the vote to every body in its
place with the sofa full (the captures on the look sheet). Built:

- **The summons sends the house** although the evicted is still binding: the coordinator's
  `BeginCeremonyStage` and `JoinCeremonyStage` ask only that the coordinator exists
  (`Usable`), not that every eligible body is bound (`IsReady`); the unbound are named and the
  retries send them.
- **The card waits for the house** as long as the longest route the summons sent takes
  (`SummonsHardSecondsFor`: the walk at 2.2 m/s and three seconds to sit, never less than the
  summons, never more than the pace's patience; a late join stretches it), and for the people
  it is about - the nominees, the Head of Household - up to the whole patience regardless.
  `EveryoneArrived` counts the placed, not the leased, and stops waiting for anyone the
  coordinator has refused twice.
- **Seating is retried** on the coordinator's word each frame (`SeatArrivals`): a seat lost to
  a push is taken again when the body arrives again.
- **A seated body's root is parked** (`HouseNpcMotion.SetParked`, from
  `HouseSeatPresentation`; the player's agent by the stage): the agent a point with no
  avoidance, and the root on the furniture layer, which the arrival test's clearance leaves
  out - two roots half a metre apart always overlap at the bodies' radius, and the capsule
  itself must stay enabled or the motion loses its binding (learned the hard way: disabling it
  churned the house for thirty seconds).
- **The set's marks:** the Head of Household's mark on the head's own line (a step out to the
  side, it stood within a hand of the ring's flanking approach at sixteen and blocked that seat
  for good), the veto holder's searched clear of every approach; the standing marks 1.3 m
  apart across and between rows, since a body bound for the back row passes between two in
  the front; and `Assign` passes over any mark the coordinator's `CanStandAt` refuses - one
  lay on a floor's edge, refused by every send.
- **The report says why:** `ArrivalFailure` and `LastRouteFailure` on the motion put the
  arrival test's own reason and the route's refusal into the stage's report.

Not built from D1: the table grown to the ring - the storyboards (§8) replace the table with a
lounge circle, so the ring stays as it is until the authored set lands.

#### 7.1.3 A walker held against a body on its approach (2026-09-29)

The full-house nomination test failed twice in the audited pipeline (endgame-f34b at 80fef17,
endgame-f36 at b52e1dd), both times with the same pair, to the centimetre. taylor-kim stood on
seat 8's approach, "no clearance". extra-14, going round to seat 13, stood 0.70 m away at
r = 2.0 m and did not move between the card's start and eight seconds in. A longer window would
not have helped: the pair was deadlocked. Measured on the UMA copy with the stage report and a
trace every half second:

- **The way round is 0.56-0.69 m from the north arc's approaches.** At fifteen chairs the ring
  is 1.92 m out and its approaches 2.62 m. The runtime chairs have no NavMesh hole. The baked
  hole round the table (bake agent radius 0.5 m) ends at r = 1.84-2.06 m, so walkers going
  round follow that edge, through the chairs. At seats 5 to 9, on the north arc where the house comes in, the approach is
  0.56-0.69 m from the edge. That is less than two bodies' radii (0.35 + 0.35), measured with a
  NavMesh raycast from each approach toward the table.
- **A body standing on one of those approaches closes the way round.** Not yet seated, it is
  not parked. The walker is held against it at 0.68-0.70 m, touching, and the touch fails the
  stander's arrival clearance (`HasCapsuleClearance`). So the stander is never seated and
  parked, and the walker is never let past. Ten runs of the full house: four deadlocked, all
  on seat 9 (sam-williams, with extra-14 and extra-15 held against them). The walkers stayed
  put for 17 s, through the whole card, and the card only started at the summons' cap. The
  pipeline's two deadlocks were on seat 8.
- **Fixed:** a ceremony counts a body as arrived once it stands on its place and has stopped,
  whoever touches it (`HouseMeetingCoordinator.CeremonyActorArrived` asks
  `HouseNpcMotion.IsOnMark`, which is the arrival test without the clearance). The body sits,
  its agent is parked, and the walker goes by. Twenty runs after the fix: fifteen seated at
  every card's start, and the card started when the house had gathered (about 19 s), never at
  the cap. The stage's report now names the nearest body to anyone stuck or walking.
- **Still true:** walkers go round through the ring's chairs, and through anybody seated on
  them, because runtime chairs have no hole. The authored, baked set (§8.2.3) is what ends that.

The same failure showed a teardown fault. When a scene unloads under a playing card, the
anchors are destroyed first. Then the director's `OnDisable` cancels the card or ends the stage,
and the stage's report read a destroyed anchor's approach. `Report` now says the place is gone,
and `Release` checks the camera rig as `End` does.

### 7.2 The table: everyone seated, at a table their size (D1)

- **One ring, the table grown to it.** The chairs stay on one ring - the show seats the whole
  house at one table, and a second ring behind the first cannot see the screen past the first -
  and the table follows them: the dressing scales `tableRound` (and the round rug under it) so the
  top's edge stands 0.35 m inside the chairs' fronts: radius ≈ ring − 0.6, so 0.5 m at six (the
  dressed table, unchanged), 0.85 m at twelve and 1.3 m at sixteen, the height untouched - the
  rule is the ring's, the dressed radius read from the table's renderer. Its collision proxy scales with it,
  which is right for the furniture capsule; the NavMesh keeps its baked hole at the old size, so a
  free-time wander must never take a target inside the ring: the wander's target sampler learns
  the ring (the table's centre and the ring's radius, read from the dressing) as an exclusion.
  The chairs fence the ring everywhere but the head slot, so the exclusion is the only opening
  that matters. The alternative, two rings with the table as it is, is decision 2.
- **The Head of Household stands at the head** as built (decision 1 offers them a chair).
- **The stuck are re-sent.** The stage's retry loop, which re-asks the un-leased every 1.5 s,
  also re-paths a leased body that has stalled: no path, or within 0.6 m of its approach with no
  velocity for 1.5 s. The second path goes to an alternate approach - the same 0.70 m behind the
  chair, swung 40° round it, the side away from the neighbour who arrived - under the same token,
  so `HasArrivedAt` still holds. A third stall leaves them standing and the report says so.
- **The card waits for the house, not just the Head of Household.** The summons' hard cap is keyed
  to the longest route the coordinator computed at `Begin` (`TryReserveAndPath` has the path;
  expose its length): `hardBy = now + clamp(longest / 2.2 + 3, SummonsSeconds, 26)`, halved at the
  quick pace; the card starts when everyone the stage placed has arrived, on a press, or at the
  cap - and past the cap whoever is still walking sits on arrival, as now. For a nomination the
  keys name everybody, so everybody is a principal; for the eviction 7.3 makes the nominees'
  arrival a beat of its own.
- **Approaches validated at the dressing** the way the standing marks are (`NavMesh.SamplePosition`
  0.6 m and the furniture capsule); a chair whose approach fails is moved half a step along the
  ring and tried again, and a chair that cannot be placed is struck and logged, rather than laid
  where nobody can reach it.
- **Tests.** `CeremonySeatingTests`: the table's radius follows the ring at 6, 12 and 16, and every
  chair's approach is a metre from the next. PlayMode at sixteen: by the card's end, everybody in
  `places` is in `seated` - the report on failure - and the capture from the head shows sixteen at
  one table. `CeremonyStage_TheNominationGathersTheHouse…` keeps its cast and its asserts.

### 7.3 The living room: the gallery (D2)

The eviction set becomes seats for the whole house, facing the screen, in rows behind the hot
seats. The show's living room: the nominees in front, everyone else on the couches behind them,
the Head of Household to one side.

- **Row 0, the hot seats:** as built, two chairs 2.0 m from the stage, 1.1 m apart - but
  approached from their outer sides, not from behind: the 0.70 m behind them is the aisle the
  row behind is approached through, and two nominees and five sofa-sitters waiting on the same
  strip of floor is how bodies stall. A third nominee (a special eviction) takes the front row's
  centre seat.
- **Row 1 (3.8 m from the stage):** the sofa, moved from (−1.4, −4.8) onto the room's axis (x −5)
  and turned to the board as it is now, its three seats; an armchair either side of it
  (`loungeChairRelax` from (−3.9, −7) and `loungeDesignChair` from (−10.4, −7.2)), turned to the
  board - five seats.
- **Row 2 (5.2 m):** two more sofas cloned from the sofa, side by side, centred - six seats.
- **Row 3 (6.6 m):** chairs cloned from the table's, as many as the house needs, centred - up to
  five. Sixteen seats in all with the hot seats; a house of six gets row 1 and no more.
- **Rows 1.4 m apart** (a sofa is 0.9 m deep; a sofa seat is approached from the front at 0.70 m,
  a chair from behind, and every approach point lies in the aisle in front of its own row, never
  on the row in front). The armchairs and the cloned rows are laid from the axis outward and
  validated seat by seat as the marks are (bounds, NavMesh, the furniture capsule); a seat that
  fails moves outward along its row; a house the room cannot seat keeps the standing marks for
  the rest, so nobody is left without a place. The coffee table at (−8.5, −4), 1.5 × 2, stands
  just west of row 1's end; a seat the audit finds against it is moved, and if the table is in the
  way of the rows it is hidden while the house is dressed for the eviction, as the television and
  its console are, and comes back with `Strike`. The room finish's rug and cluster stay where they
  are (no colliders; a rug in an aisle is a rug). The authored `living-east-chat` venue stands at
  (−6.2, −4) and (−4.8, −4),
  which is the aisle between the hot seats and row 1: its definition moves to the room's west end,
  clear of every row, with the anchor audit (`HouseInteractionAnchors.Validate`) choosing the
  spot - a scene edit, committed on its own.
- **One venue for the gallery,** `gallery-seat`, slots in fill order (row 1 centre outward, then
  row 2, then row 3); `sofa-seat` retires into it, `living-mark` stays as the fallback and the
  Head of Household's mark beside the screen stays where it is.
- **`Assign`:** nominees to the hot seats by id (as now, `departingId` included); the Head of
  Household to the mark beside the screen; everyone else a gallery seat in cast order, the marks
  only when the seats run out; the player's seat is their own as now.
- **The nominees' arrival is a beat.** The card waits for everyone up to 7.2's cap and for the
  nominees to the hard cap regardless; when the last nominee sits, a cut to the hot seats
  (`PairShot`) holds 1.2 s before the card comes up, so their taking the seats is seen. A nominee
  who cannot be seated by the hard cap plays the card on the HUD as today, and the report says who
  and why.
- **Tests.** `CeremonySeatingTests`: sixteen seated places at sixteen, rows 1.4 m apart, none
  inside furniture, five at six; PlayMode at sixteen: by the card's start the two hot seats are
  held by the nominees' ids and nobody else, by the card's end nobody but the Head of Household is
  on a standing mark, the report on failure; the capture from behind the hot seats, looking back
  over the gallery at the screen, and the wide.
- **Dressing at season start** (the deferred item in §6) matters more once the sofa moves: a house
  that walks into its first eviction sees the living room rearrange itself. It stays deferred
  behind the start-spot audit, and the sofa's move is made at the first eviction until then.

### 7.4 The walk out as the intro's mirror (D3)

After the vote reveal's result and the card's close, the stage in `Release` with the house still
seated, the walk out becomes an exit sequence with the intro's beats in reverse. Every shot on the
40° lens as the stage's are; every wait on the unscaled clock as the walk out's are.

1. **The result** (as built): the survivor's `Won`, the evicted stands out of the hot seat
   (`StandUp`) into `ReactEvicted`.
2. **The goodbyes, in the living room (about 4 s).** The evicted turns to the gallery
   (`SetFacing`); every seated body looks at them (`LookAt`, 2 s) and the survivor stands; the
   camera pushes from the hot seats' two-shot onto the evicted alone (`SeatShot` at 3.1 m to
   2.4 m over 2 s) while the goodbye line plays on the strip as now (`GoodbyeLine`, the sting's
   walk-out kind). An embrace with the survivor is a take the library does not have (D4).
3. **The walk, unwatched.** The coordinator routes them to the yard's inside mark as now, but the
   camera never follows: it holds the wide on the house watching them go (heads following the
   body through `LookAt` on its transform, 3 s), cuts to the survivor (2 s), and stays on the
   house until the body is within 6 m of the inside mark - the door shot's frame - then dips to
   black for 0.4 s and comes up on **the door shot** (the opening stage's `DoorShot`, the same
   focus, distance, yaw and depth of field), the door set built closed at the cut rather than at
   leg 0, so it is never seen standing in the yard mid-walk. Under reduced motion the walk out
   does not play at all, as now. A walk that takes longer than 20 s cuts to the door regardless
   and, at `WalkOutSeconds`, lets them go as now.
4. **The arrival.** The evicted comes into the door shot from the house's doorway on the frame's
   right, back to the lens, and walks to the reveal mark (−1.6, 14.1), where the intro's guests
   posed. The last look follows the goodbye line the strip is already showing: for the warm line
   and the deal's ("one last look", "pauses at the door and turns to you") they stop, turn to the
   house and play `ReactEvicted` for the intro's 1.6 s under a lower third - their name, EVICTED,
   the week - then turn back; for the cold line ("without looking back") and the glare they do
   not stop. The player's own face is in the lens's place, so the turn reads as to them.
5. **The door.** `Open` without the intro's flash (the burst and the sparkle are the welcome; the
   leaves swing and the light beyond stands at its settled level), the push-in (`PushInShot`,
   0.5 s), and once the leaves are 0.35 open they walk through to the deck mark as now. The
   vestibule's light plane at x −4.4 hides them the moment they pass it, so `Close` is called
   then, the set's own swing, the shot holds 1.2 s on the shut door, and only then does
   `FinishWalkOut` switch the body off - behind a closed door, not in the open. The camera
   releases to the viewer (`ReleaseShot`, 1.2 s), the stage ends and the house gets up.
6. **The gait.** There is no sad walk in the library (`Walk` is one clip blended by `Speed`). For
   the walk out the agent's speed drops to 1.5 m/s (`RouteDeparture` gains a speed) and the head
   goes down: `LookAtPoint` on a mark kept 1.5 m ahead of the body on the floor, the stage's
   look-mark pattern. A slow walk with the head down reads as heavy; a tearful walk take through
   the mocap pipeline (adding-a-mocap-take) is D4.
7. **What does not change.** A press skips to the shut door and lets them go at once
   (`SkipWalkOut`), the press that closed the last card still does not; a competition started
   meanwhile ends it; a batch run walks nobody out unless asked; the final eviction has no walk
   (finale night, the juror joins the jury); the player is never `departingId`
   (`EpisodeDirector.cs:738`), so their own eviction is the season's end as before. The
   eligibility rule stays the narrow one: `Holds` while the stage has a place for them,
   `walkingOutId` for the walk, `departingId` nowhere else.
8. **Tests.** `WalkOut_TheEvictedWalksOutThroughTheFrontDoor` extended: the rig's shot is the door
   shot when the door opens, the body is still active when the door's openness reaches 0 again,
   and inactive after the hold; a test that the cold goodbye line walks through without the
   stop; the press, batch, arena and cannot-walk tests as they are (the narrowed eligibility rule
   was proved against `WalkOut_ABodyThatCannotWalkIsLetGoAndTheHouseCarriesOn` and must stay
   so). A capture test on the look sheet with three frames: the goodbye in the living room, the
   door shot with the evicted at the mark, the door shut. The standalone walk's eviction frames
   are read at the audited pipeline.

### 7.5 Milestones

| | Milestone | Done when |
|---|---|---|
| D0 | The stage report in play and the sixteen-houseguest fixture, with the two captures | The Player.log names why each standing houseguest stands, and who held each hot seat at the card's start. **Done 2026-09-28: 7.1.1.** |
| D1 | The table grown to the ring, the stuck re-sent, the summons keyed to the longest route, approaches validated | PlayMode at sixteen: everybody seated by the card's end; the capture from the head. **Mechanics done 2026-09-28: 7.1.2**; the grown table gives way to §8's lounge circle |
| D2 | The gallery: the sofa on the axis, the armchairs, the cloned rows, one venue, the nominees' arrival beat, the venue and coffee-table scene edit | PlayMode at sixteen: the nominees' ids in the hot seats at the card's start, nobody standing but the Head of Household by its end; the capture over the gallery |
| D3 | The exit sequence: the goodbyes, the house watching, the door shot, the last look, the door opened, walked through and shut, the hold | The walk-out tests extended and green; the three captures; the audited pipeline's eviction frames |
| D4 | Polish: the tearful walk and the embrace takes, the door's sound, dressing at season start, the Head of Household's chair if chosen | As C5 |

D0 first, then D3 can go ahead of D1 and D2 (it depends on neither), and D1/D2 wait on what the
report says. Each lands as the batches before it did: the offline compile, the Unity-free
simulation subset, the filtered suites on the D: copy, then the audited pipeline from the main
checkout before a PR.

### 7.6 Decisions for the owner

1. **The Head of Household's chair.** (A, recommended) N−1 chairs and the Head of Household
   standing at the head, as built, no new take needed. (B) N chairs, the Head of Household seated
   at the head and rising to pull keys, the show's staging; a rise-from-seat cue exists
   (`StandUp`), so it is a week's polish, not a rebuild.
2. **The table.** (A, recommended) one ring with the table grown to it and the wander kept out of
   the ring. (B) two rings behind the table as it is - the back ring sees the screen over the
   front's shoulders but is not at the table.
3. **The living room after the first eviction.** (A, recommended) the sofa stays on the axis and
   the coffee table hidden for the season, as the television is. (B) struck and put back after
   each eviction - a redress a week, and a rearrangement the house walks through every time.
4. **The walk's middle.** (A, recommended) the house watching - the wide, the survivor - until the
   body reaches the yard, then the dip to the door. (B) the dip straight after the goodbyes and
   the door shot holding on a closed door while they walk, up to ten seconds of nothing.
5. **The last look.** (A, recommended) keyed to the goodbye line: the warm and the dealt stop and
   turn, the cold and the glare walk straight through. (B) everybody stops and turns. (C) nobody
   does.

The owner took every recommendation above on 2026-09-28 (evening). D0 and D1's mechanics are
built (7.1.1, 7.1.2). Where §8 says so, §7.2 to §7.4's designs are superseded by the storyboards.

## 8. The storyboards (2026-09-28): the cut scenes' art direction

The owner drew three storyboards, six frames each, for the nomination ceremony, the veto
competition and ceremony, and eviction night (the images are in the session's records; the
frames are described below). They are the art direction for C4 and for the rest of round two.
This section was built by mapping every frame against the code (three readers, three
adversarial verifiers: 6 of 96, 26 of 111 and 5 of 97 claims corrected, none of the analyses
refuted) and by a design panel on the three hard problems (three designs each and a judge). What
follows keeps only what the verifiers confirmed, with the house's rules in front of every
proposal: seating furniture is authored in the scene and baked (7.1.1); bodies move only by
routing; runtime props have no colliders; the cards keep the order and the timing, with one
stated exception; reduced motion, batch runs and the HUD path never see any of it; captions are
contracts; presentation only, no saved field, nothing of the story session's.

### 8.0 What the storyboards change

**The sets.** The nomination room is a lounge circle: cream curved sofas round a low round table
with a plant, the circle's mouth open toward the screen, the Head of Household standing in the
mouth with a keybox, and two tall red wingback chairs in the mouth for the nominees. The living
room is a U of cream couches round a low table facing the screen, with the two red wingbacks in
front of the screen facing the room, so the house sees the nominees and the board together.
Neither exists: the nomination room is fifteen dining chairs on a ring round a 0.78 m table, the
living room a turned sofa, two cloned dining chairs and standing marks. Under 7.1.1 both become
scene set pieces with fitted proxies and one rebake; `CeremonySets` keeps only the anchors.

**The beats.** Each key is a walk: the named houseguest rises, walks to the box the Head of
Household holds, takes a key, walks back and sits holding it; the last two cross to the red
chairs. The veto meeting is staged in the nomination room with the nominees in the red chairs,
the holder between them wearing the necklace, the pleas, and the necklace into a red box on a
stand or round a nominee's neck with the replacement walking to the freed chair. Eviction night
adds a goodbye from the red chair, and the exit is a walk to a golden double door in view of the
couches, opened, walked through and shut, held over the seated heads. Built today: the summons,
the keys and the vote on the screen, the beat-driven cuts, the seated reactions, the stand-up,
and a walk-out to the yard's front door that nobody watches (§7.0).

**The props.** Eight authored pieces, none in the catalogue today: an arc sofa, a four-seat and
a three-seat straight couch in cream, the red wingback, a stand, the keybox, the veto necklace
and the red veto box. The keys and the box can start as renderer-only runtime primitives on the
opening door set's recipe (no art blocks the build); the crown over the exit door is the door
set's own bar with the crown icon; the bag in the evicted's hand is dropped (no prop, no carry
take). The takes the frames want and the library lacks - a carry, a reach and take, a hand on
the heart, a tearful walk, a seated shock - are mocap polish through adding-a-mocap-take, never
blockers: until then looks, the seated fist pump, `ReactEvicted` and a slower walk carry them.

### 8.1 Frame by frame

**Nomination ceremony (nomination room).**

| # | Frame | Built | To build |
|---|---|---|---|
| 1 | The house assembles on the lounge circle; the HoH at the head with the keybox | The summons, N−1 seats on the ring, the head mark, the idle house mark on the screen | The authored circle (8.2.3); the HoH's mark on the mouth's line; the keybox on the HoH (8.2.1) |
| 2 | The HoH holds the box to the house and speaks | The screen shot on the Opened beat | An over-the-shoulder shot from the nearest seated houseguest; the talk loop on the HoH for the intro's 2.4 s |
| 3 | The first called walks up and takes a key | A cut to their face and a look | The walk-up per key, the take, the key in the hand (8.2.1) |
| 4 | Seated houseguests hold their keys up | The seated fist pump on the last safe key only | The fist pump on every key once seated; a key quad riding home in the hand; a group raise on the last-key beat |
| 5 | The two without keys sit in the middle; tension | The last-key push-in on the waiting group | The push-in from the wide's side so the board stays behind them; seating never keyed to the outcome (the spoiler rule) |
| 6 | The nominees take the red chairs in front of the screen | The block beat drops two heads in their ring seats | The two authored wingbacks in the mouth and a stage tail after the card: the nominees rise, cross and sit in red, the pair shot holds (8.2.1) |

**Veto competition and ceremony.**

| # | Frame | Built | To build |
|---|---|---|---|
| 1 | The six competitors revealed in a line in front of the living room's screen | The veto-field takeover on the HUD; the engine's draw | The takeover mounted on a screen with per-face beats; a `living-line` of six standing anchors in front of the stage facing the room; the field commit routed through a stage |
| 2 | The competition begins at six lit stations in the yard | The arena samples bare floor and marks it with discs; three podiums | Six authored stations with key icons on the podiums' row; a yard screen a card can mount on; a rebake (a slice of its own, 8.3) |
| 3 | The timer on the yard screen; the six solving | The HUD's clock; contestants stand at their marks | A world scoreboard on the yard screen fed from the run; heads down over the station; the puzzle take is polish |
| 4 | The winner holds the key aloft; the others cheer | The arena is struck before the result card; `Cheered` | A result beat inside the arena's life: the yard screen reads VETO WINNER, the winner's `Won`, the house's `Cheered`, confetti on the card |
| 5 | The meeting: nominees in the red chairs, the holder between them, the HoH aside, the pleas | `Assign` for the veto (dead code behind the reveal routing), the takeover on the HUD | The veto commit routed through the stage into the nomination room; the block from `wasNominated`; the holder's and the HoH's marks; a plea beat per nominee on the card (the player's own plea stays the strategy windows') |
| 6 | Not used: the necklace into the red box. Used: round a nominee's neck, the replacement takes the other chair | Nothing | Decision and replacement beats on the takeover; the necklace re-parented under a cut; the saved nominee back to the circle and the replacement to the freed chair in a stage tail |

**Eviction night (living room).**

| # | Frame | Built | To build |
|---|---|---|---|
| 1 | The house on the U of couches; the nominees in the red chairs facing the room | Two hot seats facing the screen, three sofa seats, standing marks; the nominees held by id; the card waits for the house (7.1.2) | The authored U and wingbacks facing the room (8.2.3); an arrival cut to the nominees taking the chairs |
| 2 | THE VOTE: a roster of voters revealed one by one | Two faces with climbing counts and pips | A roster on the screen frame only, each row naming the nominee that voter evicts (never EVICT/KEEP against the eventual evictee, which spoils the result); the HUD frame byte-identical |
| 3 | BY A VOTE OF 3 TO 2, CASEY IS EVICTED | The banner and the host line; the evicted stands at 0.4 of the hold | A three-line result block on the screen frame; both nominees seated through the result; the losing side of the vote looks down |
| 4 | The evicted stands from the red chair and says goodbye | Closed goes straight to Release and the camera to the viewer | A Goodbye step of about 4 s: the evicted up and turned to the U, every head on them, the goodbye line here, a push onto them; a press skips it |
| 5 | The exit: down the room toward the golden door as the house watches | The camera on the body's heels for 20 m to the yard | The door in the living room in one over-the-heads frame; the walk at 1.5 m/s with the head down; the last look by the goodbye line's kind (8.2.2) |
| 6 | The door closes over the seated heads | The leaves never close; the body vanishes in the open | `Close` once the body is behind the leaves; a 1.2 s hold; the body switched off behind the shut door; the strike under a dip |

### 8.2 The three hard problems, decided

#### 8.2.1 The keybox walk-ups (the one contract change)

Each key is a real round trip by routing under the ceremony lease, re-routed leg by leg (a
`RouteCeremonyActor` on the opening's `RouteOpeningActor` pattern): chair, a take mark in the
circle's mouth in front of the Head of Household, chair. The HoH carries the keybox on their
forearms; the key the card names is one object re-parented from the box to the taker's right
hand on a forearm reach and rides home to the lap. Props are renderer-only runtime objects
driven by one LateUpdate component that lives off the bodies, so no body's motion binding is
touched (the motion refuses any unlisted root behaviour). On the block beat the two nominees
cross to the red chairs.

Timing: the card keeps the order and its rhythm. A `CeremonyPace.Staged` row, never a setting,
is chosen by the director's play lambda when the card plays on a set's screen at the suspenseful
pace: PerKey 5.0 s up to six keys, 5.5 to nine, 6.0 beyond, so a take fits inside each key's
hold and the return overlaps the next key. **The contract change:** the card may be asked to
hold at a key boundary for a take that is late, capped at 4 s, and at the block's end for the
crossing, capped at 6 s (`HoldBefore(atCardSeconds, until, cap)` on the card; cleared by a skip,
a speed press, a cancel and the stage's end). A skip still gives up the order and never the
outcome; on the HUD nothing changes; `Duration` is unchanged so the pacing tests keep their
numbers. Four doc comments that say a card never waits are rewritten with it. If the owner
declines the wait, the design runs on the Staged row alone and a walk later than the boundary
is abandoned where it stands.

Gates, decided once at the card's start: walk-ups only on a baked ring (a NavMesh probe under
every seat: a chair whose centre samples floor has no hole), only on the set's screen at the
suspenseful pace, never under reduced motion, on the HUD, in a batch run without
`StagesInBatchRuns`, at the quick pace, or while sped up; a body that cannot walk or whose take
mark is not free is handed the key at the chair, as today. Before anyone leaves a chair the
coordinator's `CanWalk` and `CanStandAt` are asked, so nothing stands up that cannot walk. The
shot list per key: the screen, the rise from in front of the chair, a following shot from the
mouth's side, a profile two-shot across the head's line for the take (never over the HoH's
shoulder: at sixteen there is no room behind them), a wide of the walk home with the next riser
in frame; the last-key push-in and the block's pair shot as built. Budget: about 29 s at six
against 15 today, about 91 s at sixteen against 30, inside a stage cap raised to
60 + 10 × keys. The report gains a walk-up column (to the box, taking, returning, abandoned,
handed at the chair, held seconds). Effort about six days once the ring is authored.

#### 8.2.2 The exit door

The storyboard draws the door at the end of the living room in view of the couches; the owner's
earlier words were "the same door from when they enter". Both are honoured by rebuilding the
opening's own door set - the same leaves, jambs, crown, light and swing - on the living room's
west wall at its south end, with the yard's front door kept as a fallback venue behind a switch.
The facts that decide it, verified: there is no NavMesh west of the wall and none can be added
at runtime, so there is no deck behind this door; the body is switched off standing inside the
one-metre vestibule behind two shut leaves, where neither the exit wide nor the dollhouse view
can see it. The memory wall hangs on the west wall from z −7.3 to −2.7, so the door stands at
DoorCentre (−12.6, 0, −8.5) with its aperture at z −9.6 to −7.4, clear of the frames; the west
wall is 1.5 m tall, not the 1.1 m cutaway the plan assumed. The set is built at the vote card's
close, closed and dark and out of every frame, and struck under a travel dip when the stage
ends, as the opening strikes its own; a runtime door stops nobody, so it never outlives the
walk. Two small props inside its footprint (a speaker and a plant) are hidden while it stands,
as the television is for the screen.

The sequence: the goodbyes (about 4 s, the goodbye line moved here from the door, the stage
keeping its shot instead of releasing it); the exit wide over the seated heads looking west down
the room with the door at centre; the walk at 1.5 m/s with a look mark kept ahead on the floor;
the last look at a mark 1.4 m before the door, by the goodbye line's kind (warm and dealt stop,
turn to the lens and play `Evicted` for 1.6 s; cold and glare walk straight on); `Open` without
the intro's white flash; a push-in; through the leaves into the light; `Close` once the body is
behind them; a 1.2 s hold on the shut door; then the stage ends, stands the house up and hands
the camera back. About 18 s after the card, every second in frame. A press skips to the shut
door and lets them go; the press that closed the card still does not. Measured first: a probe on
the D: copy samples the door mark's floor and reports the mesh's edge; if it comes back too far
east the whole set steps east by the shortfall, and if the leaves could not hide a body the exit
falls back to the yard. Effort about four days. Because 8.2.3 moves the living room's screen to
the south wall, the door stands 1.5 m from the stage's west end; the walk from the red chair is
four metres, and the exact wide is placed by capture, not reasoned.

#### 8.2.3 Authoring the seats

Eight authored pieces (one script each under ArtSource/setpieces, origin centred and on z = 0,
verified with bb_look renders; the arc sofa and the wingback first, since every other number
hangs on their imported bounds): `bb_set_loungearc3`, a 52.8° arc sofa with three seats at
0.72 m pitch and three chord-box collision children so the hulls follow the arc;
`bb_set_lounge4` and `bb_set_lounge3`, straight cream couches at 0.76 m pitch (the existing
sofa's 0.67 breaks the 0.7 rule); `bb_set_wingback`, 0.9 × 0.9 × 1.25 m in red velvet;
`bb_set_stand`; `bb_set_keybox`; `bb_set_vetonecklace`; `bb_set_vetobox`. The low round table
is the existing round table at a row height of 0.45. Fallback if the art slips: the Poly Haven
armchair as the wingback's stand-in and the straight couches alone.

**The nomination room:** the circle's centre at (0.075, −14.5), the seat ring at 2.4 m, the
approach ring at 3.55 m (approaches behind the sofas, 1.07 m apart), the mouth 96° wide toward
the screen; five arc sofas touching; the two wingbacks in the mouth at (−1.2, −14.75) and
(1.35, −14.75) facing the house, approached from behind; the low table at the centre; the
head marks in the lane between the wingbacks and the screen, validated by `CanStandAt`; the
keystand and the veto stand on the screen's stage ends, above the floor so they cost no lane;
the room marker moved out of the table's box into the lane. The ring rug follows the table. The
north band between the north sofa and the wall is the tightest spot in the house (0.55 m at the
north door's mouth): the doorway resolver's log decides it, and the fallback is the circle
0.2 m further south. An alternative layout, the owner's call: the wingbacks flanking the mouth
wider apart with the HoH's mark between them nearer the ring.

**The living room:** the screen moves to the south wall's west half (a stage 4 m wide on the
north wall would pinch the bedroom door to 0.4 m of mesh), the wingbacks at (−8.55, −7.3) and
(−7.45, −7.3) facing the room 1.1 m apart, approached from the front; the U of two four-seat
couches as its base at z −2.9 facing south and a three-seat couch at each arm, fourteen seats
in one `gallery-seat` venue filled centre-out, the low table inside; the prototype sofa, coffee
table, rug and television boxes disabled; the room finish's feature wall, strips and prints
follow the screen to the south wall; the living room marker, the player's and two houseguests'
start spots and the `living-east-chat` anchors move out of the U (a scene edit committed with
the batch, every moved spot audited); the standing marks stay as the fallback past fourteen.
The Head of Household stands beside the screen as built (or sits in the U: the owner's call).

*As built for the living room (MOCKUP-PASS-PLAN M20-M22, 2026-09-29).* The owner chose the south
wall, the Head of Household seated in the U, and the living room's door. Two things differ from the
paragraph above:

- *The arms do not tuck under the base's ends.* At 3.44 m, the two four-seat couches span x -11.52
  to -4.48. Arms centred at -11.06 and -4.94 would leave the base's outer seats facing an arm 0.45 m
  away, with no floor for their approach. Instead each arm's front is flush with the base's first
  seat, at x -11.32 and -4.68 (arms centred at -11.78 and -4.22), spanning z -4.26 to -6.94.
- *The base stands at z -2.3, not -2.9.* At -2.9 the 0.89 m corners closed once the bake eroded both
  sides, and the U's west half was reached through one 0.76 m lane past the table. At -2.3 the
  corners are 1.49 m and the lane 1.36 m. Gallery approaches stand 1.05 m in front of the seat
  (0.19 m outside the couch's eroded edge), and the red chairs' approaches stand 1.15 m to the side.
- *The red chairs are approached from their outer sides,* 1.0 m off the chair. The low table's baked
  edge (0.95 m across at (-8, -5.6)) meets the chairs' own in front of them.

The screen is authored on its stage at (-8, -8.9), named `bb_set_ceremonyscreen (Living)`, with the
house's GAMESIM idle board. The chairs are at (-8.55, -7.3) and (-7.45, -7.3), the base at z -2.3,
and the table at (-8, -5.6).

`HouseLivingGallery` switches off what the U replaces: the prototype's sofa, coffee table, rug and
television, and three plan rows (the sofa by the kitchen door and the two armchairs, which stood on
the east arm's end and in the west chair's approach). It also switches off the broadcast dressing's
decor from the old layout (books, a vase, a pendant, a frame) and the television's fitted practicals. It moves the room marker to (-5, -7.6) and
the chat pair to (-3.4, -3.0) and (-2.0, -3.0). It also moves the three bodies the scene started
inside the U: the player, Casey and Maya.

`CeremonySets` lays anchors on the pieces where they stand, filling slots from the base's middle
outward and then down the arms. It keeps the prototype's clone path only for a room with no
gallery.

**`CeremonySets` rewritten to anchors only:** no clone, move or scale; a seat-layout table per
piece name gives each piece's seats in its own frame; `Ensure` finds the named pieces in the
room, orders them by the venue's slot rule and creates the anchors; seat counts never depend on
the house size, because `Assign` hands out slots in order. The anchors are not saved: the
placement pass runs `Ensure(scene, 16)` after fitting the proxies and before the resolver, so
the resolver scores every ceremony approach, and strikes them before the save. Any seat piece
whose proxy the resolver backs off means the layout sealed a route and is fixed by moving
furniture, never by leaving the proxy off.

**The stage:** `Assign` by venue as above; the wide on the placed seats' centroid; the block
move on the block beat as a stage tail in Release (the seats exit, the leases re-join the
wingbacks, the pair shot holds up to 10 s, then the release), no card change; the veto meeting
in the same pattern. Effort about eight days, the rebake slice included (7.1.1's deferred one),
and this is the first slice to build, because 8.2.1 and 8.2.2 both stand on it.

### 8.3 The build order

| | Slice | Done when |
|---|---|---|
| S1 | The eight pieces, arc sofa and wingback first, with renders | Imported, centred, on z = 0, metas written; the seating tests' preview scene dressed with them |
| S2 | The scene: the rows, the markers, the start spots, the chat anchors, the prototype boxes, the room finish moved, the placement pass with `Ensure(16)` before the resolver, the rebake, the lighting pass, the anchor audit | 28 of 28 room pairs, no seat proxy backed off, the start-spot audit green, the audited pipeline green |
| S3 | `CeremonySets` as anchors only; the stage's Assign, wide and block move; the full-house tests re-pinned (fifteen seated round the circle; fourteen on the U and two in red, every line seated) | The two captures from above show the storyboards' first frames |
| S4 | The exit: the door on the west wall, the goodbye step, the last look, the close and the hold, the yard as the fallback venue | The walk-out tests extended and green; three captures; the audited walk's eviction frames |
| S5 | The keys: the Staged pace, the capped holds, the re-route, the box and keys, the shots, the report's walk-up column | Every key taken from the box at six; the full house measured; captures of the take and the return |
| S6 | The veto: the takeover on a screen with beats, the field's line in the living room, the meeting with the wingbacks, the pleas, the necklace and the box, the replacement's walk | A staged veto meeting on a director season at both outcomes |
| S7 | The yard: six authored stations, the yard screen and scoreboard, the winner's beat (its own rebake) | Optional; the owner's call on scope |
| S8 | Polish: the vote roster and the result block on the screen frame, the crown emblem, the door sound, the takes, the group key raise | As C5 |

S1 to S3 first, as one batch with the rebake; S4 and S5 are independent of each other and
follow; S6 after S5 (it reuses the re-route and the block chairs). Each lands as before: the
offline compile, the Unity-free subset, the filtered suites on the D: copy, the audited pipeline
alone from the main checkout, then a PR. The story session is told before S2 lands, because the
living room marker and the start spots move under their scene stage's ring.

### 8.4 Decisions for the owner

*Answered on 2026-09-29 (MOCKUP-PASS-PLAN section 4):* 2 A (the exit door on the living room's west
wall, the yard as the fallback), 3 A (the screen on the south wall's west half) and 4 B (the Head
of Household seated in the U, in cast order). The rest are open; the mockup pass takes their
recommendations unless the owner rules otherwise.

1. **The wait at a key boundary.** (A, recommended) the card holds up to 4 s for a late take and
   6 s for the crossing. (B) the card never waits; a late walk is abandoned where it stands.
2. **The exit door.** (A, recommended) the opening's set rebuilt on the living room's west wall,
   the yard kept as a fallback. (B) the yard's front door only, with the plan's dip and cut. (C)
   both: the room's door, then the yard's as a coda.
3. **The living room's screen.** (A, recommended) the south wall's west half, the feature wall
   moving with it. (B) the north wall, shifted east to clear the bedroom door, the U hugging the
   kitchen door.
4. **The Head of Household at evictions.** (A, recommended) standing beside the screen as built.
   (B) seated in the U in cast order.
5. **Walk-ups at every house size.** (A, recommended) always, about 91 s at sixteen. (B) up to
   twelve keys, the rest handed at the chair.
6. **The vote board.** (A, recommended) a roster under the two faces, each row naming the nominee
   that voter evicts. (B) the two-face tally as built. Never EVICT/KEEP against the evictee.
7. **The nominees' crossing.** (A, recommended) a stage tail after the card, up to 10 s. (B) the
   card holds until both are seated.
8. **The competition yard.** (A, recommended) later, as S7 on its own rebake. (B) in this round.
9. **The keybox.** (A, recommended) carried on the HoH's forearms, renderer-only props now,
   the authored box later. (B) on a stand beside the HoH.
10. **The bag.** (A, recommended) dropped. (B) a prop later, with a carry take.

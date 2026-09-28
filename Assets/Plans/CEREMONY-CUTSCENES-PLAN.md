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

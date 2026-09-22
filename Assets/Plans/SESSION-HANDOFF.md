# Session handoff — 21–22 September 2026

Written for whoever picks this up next. It covers what shipped, what is in flight, what remains,
and — the part that will save you the most time — the traps this session fell into, several of them
more than once.

Branch: `port/game-flow-v2-pass`. Baseline before this session: `e45686f`.

---

## 1. What shipped

Twelve commits, newest last. The last five are the six-track programme's first moves (§2).

| Commit | What it did |
|---|---|
| `d587aab` | **Walking animations.** The whole cast ran everywhere. |
| `bec990e` | **HUD relayout.** Cast strip to the floor, six-item nav rail in the freed gutter. |
| `c1b2983` | **Cast selection screen.** Portraits were unmasked black squares. |
| `4b2e1a8` | **Click-to-talk.** Clicking a houseguest only moved the camera. |
| `2053241` | **Pressability.** ~150 assertions asked a weaker question than they read as. |
| `9c77154` | **Three guards against misreading a result.** |
| `27bd14a` | **Track 2 visual guards.** A guard that cannot fail is worse than no guard. |
| `b8daeea` | **Track 3, first slice.** Intel returns something; deals in "Between you"; a zero outcome says so. |
| `f1b60be` | **Track 1: CI runs tests.** The simulation's own tests, without Unity, on every push. |
| `6379520` | **Track 4: `UiTheme.AddGlow`.** The cast screen stops taking the whole glass for a halo. |
| `b0dffe3` | **Cast strip standing.** Allied / Friendly / Wary / Hostile on every chip. |
| `cb9a4c5` | **A Talk says each thing once.** The standing is its own line, only when it moved. |

### d587aab — walking
`UmaBodyProvider` filled the `Walk` state by asking UMA's `Locomotion.controller` for a clip called
`Walk`, then falling back to `Run`. That controller has `Idle`, `Wave`, `Run` and **no walk at all**,
so the fallback was the only branch and everyone sprinted everywhere. Three Mixamo takes wired in:
`Walk_loop` on a real Walk state at 2.2 m/s, UMA's run demoted to an actual `Run` state at 4 m/s,
`WalkStop` so a body plants instead of snapping to idle. Running is now a decision — double-click,
or a route over 8 m measured **along the path**.

`bb_anim_WalkTurn180.fbx` is committed and deliberately **not wired**: a turn take rotates the body
through root motion and `applyRootMotion` is off everywhere because the NavMeshAgent owns rotation.

### bec990e — HUD relayout
Cast strip from a 184-px left column to a full-width bottom row; `IconRail` moved into the freed
gutter with six items and the open one filled; notebook roster split off the relationship page onto
its own **Houseguests** page. `LeftColumnX` is now `14 + IconRail.Width + 12`; `RightColumnInset`
dropped 88 → 24, which also cut the docked panel's overlap of the vibe card from 131 units to 67.

**The floor is three bands and deliberately NOT the reference's order.** Strip lowest and full width;
status caption and controls box share the band above it side by side; panel and proximity prompt
above both. The reference puts the caption under the faces and that does not survive here: the
controls box and the right column both hang into that corner, the vibe card already ends fifteen
units above the expanded controls box, and a strip that stopped short of them cannot honour the
larger-text preference with twelve chips.

### c1b2983 — cast selection
The portrait render is a square crop with an opaque ground, stretched over a round disc with nothing
clipping it — corners overhanging the rim by 14 px, burying the ring. `CastRail` has masked its
portraits since it was written; this screen never did. Also: the scrim was 0.97 alpha so the whole
episode HUD ghosted through; the bottom row of cards was below the fold at 16:9 (676 px of cards in
a 595 px viewport) and happened to fit at 4:3 where the tests run; cards were `GlassFill` on a scrim
of `Background`, the same hex under 1 % apart.

New tokens: `UiTheme.CardFill` (a 1.21:1 lift — the reference's own lift is 1.074:1 and failed the
guard) and `UiTheme.Brass` for portrait rings, deliberately **not** `Gold`, which means power here.

### 4b2e1a8 — click-to-talk
In reach it opens; out of reach it **runs** you there and opens on arrival, on the houseguest that
was pointed at. Nineteen defects in this one feature. The ones worth knowing:

- **The chase could not be won.** Player and houseguest both walk at 2.2 m/s. The first fix chose
  its gait from the *remaining* route, so it dropped to a walk under 8 m — exactly where the gap had
  to close — and paced the target there until timeout. Hence `TryRunTo`, not `TryWalkTo`.
- **`ApproachPoint` was a straight line with no wall in it.** `TryApproach` now fans eight candidates
  and tests each with `NavMesh.Raycast`, because a bake is where this project keeps its walls.
- **Nothing cancelled the errand.** One `CancelTravel()`, called from all four entry points.
- **The drift budget (1.6 + 1.2 + 0.15 = 2.95 m) was wider than the 2.8 m talk range.**

### 2053241 — pressability
`ButtonWithCaption` filtered on `IsActive()` and then callers invoked the handler. ~150 assertions
across 20 files said "the handler runs" while reading as "the player can do this". Now requires
`IsInteractable`, on-screen bounds, and a raycast landing on that control or a descendant.

Thirty-six tests failed first run. **None was a game defect.** Getting to that answer took four
corrections to the instrument (see §4).

### 9c77154 / 27bd14a — the guards
- Every runtime `event` must be raised by something. Mutation-tested.
- `Tools/baseline.txt` floors; `FILTERED (not the suite)`; `NO XML` is not a pass; provenance banner.
- `CaptureFraming` now asserts a frame is not one flat colour.
- `AssertRegionHasContent` for a flat region in a non-flat frame.
- Clipped-copy sweep over four panel openers at both text sizes.

---

## 2. The programme's first moves — all committed

Every plan below was drafted by one agent and attacked by another before a line was written, and in
three of four cases the attack changed the plan. The corrections are recorded because they are the
kind that will be re-proposed.

### Track 3, first slice (`b8daeea`)
Three places where the simulation knew and the player was not told. Each test was mutation-killed.
- **`AskForIntel` returns something.** It spent one of six weekly actions and wrote its only memory
  with `target.id` as owner - the houseguest remembered being asked, the player learned nothing. Now
  the target hands over their own read on a third houseguest, the subject chosen from the roll
  **already drawn** for the trust bump; a second `Roll(s)` would re-roll every season from there.
- **Active deals appear in "Between you",** with their stakes, in the propose button's vocabulary.
- **An action that moves nothing says so:** *"No change in trust"* instead of nothing at all.

### Track 1: CI runs tests (`f1b60be`)
`Gamesim.Simulation` is `noEngineReferences` and so are 37 EditMode test files.
`Tools/SimulationTests/SimulationTests.csproj` compiles them with the plain .NET SDK - C# 9 and no
implicit usings, as Unity does; NUnit 3; nothing of Unity's stubbed - and `.github/workflows/ci.yml`
runs them on ubuntu with a floor from `Tools/baseline.txt` (`Gamesim.SimulationTests`). **Most of
the count is cases generated from the web fixtures**: the first local run, before the fixtures were
copied beside the binary, ran 560 instead of 716 and still said green. That is why there is a floor.
It has only ever run on Windows; the first push is its first Linux run.

**Use it.** `dotnet test Tools/SimulationTests/SimulationTests.csproj --filter FullyQualifiedName~X`
runs a simulation test class in well under a second. A mutation loop over `HouseDialogue` took
seconds this way instead of minutes per round through the editor.

### Track 4: `UiTheme.AddGlow` (`6379520`)
**The planned design did not survive review.** It was an `Emphasis` and fill-alpha parameter on
`Glass`, with an Active level that never painted the accent edge because
`Chrome_WearsNoAccentEdge` "would see it through CastSelect's live hidden hierarchy and fail in the
full suite only". False: every `EpisodePlayModeTests` test reloads the scene and the Chrome test never
opens the cast screen. The scale was also non-monotonic (the unlabelled default outshouted Active)
and the fill-alpha had no caller. What the cast screen actually wanted from `Glass` was the halo, so
`AddGlow` is the halo alone; `Glass` is `Style + AddGlow + AddBorder`, byte for byte what it was. The
picked card lost two write-backs and a doubled ring. Guarded by `UiThemeGlassPlayModeTests` and a
one-ring assertion; `cast-select-picked.png` is the frame.

### Track 3: cast strip standing (`b0dffe3`)
Every chip shows the player's own reading - **Allied / Friendly / Wary / Hostile** - as a dark pill
with a light word at the ring's foot, beside the role pill when there is one (NOM + Allied is exactly
when the player needs it). Neutral, the player's own chip and anyone out of the house show nothing,
so the first frame of a season is unchanged. `CastRail.StandingOf` reads `RelationshipWeb.KindOf`:
the player's outbound score and alliances only. Two leaks sit under that record and are **not** the
strip's to fix - a weekly NPC settle can dissolve the player's alliance silently on the NPC's
private score, and NPC-initiated acts move the player's outbound score by the reciprocal draw. A
task chip was raised for the first.

The pair shares one row, so it is fitted to its measured words and kept a unit clear of the chip's
border; at scale 0.75 the font rounds 7.5 up to 8 and the widest pair (VETO + Friendly) drops a
point. `cast-strip-standing-combos.png` shows every pair on real faces.

### Track 5: a Talk says each thing once (`cb9a4c5`)
**The planned design was a free follow-up question** ("Ask where I stand") and review found it hid
the one readout of a Talk's random half behind a button, scrolled the speaker off-screen after a real
mouse click, and relied on "no category pill means free" when three committing rows have no pill.
Taken instead: the reply to *Spend time together* is the shipped acknowledgement alone
(`HouseDialogue.TalkAcknowledgement`, moved verbatim), and where the houseguest stands is a second
line **only when the talk moved it** - compared against the same sentence read before the command.
Nothing hidden, no control added, no roll, no saved field. The question beat is still available as a
design; it is an owner's call because of the trade above.

## 3. What remains — the six-track programme

From a six-agent brainstorm (four lenses, a fact-checking critic, a synthesis). Tracks 1 and 2 are
done; 3, 4 and 5 have their first moves in (§2). **Track 3 is still where the player-facing value is.**

### Track 3 — say what the simulation already knows *(continue)*
The project's signature failure: systems that exist and never reach a pixel.
- `WebRelationshipArcs` classifies every relationship with intensity 0–100 and five escalation
  labels, persisted through **eleven save versions** — `grep Arc` over the presentation layer returns
  **zero hits**. ⚠️ **But see §5: the "these are the player's own arcs" premise was checked and is
  FALSE.** Drawing them naively leaks NPC-to-NPC state.
- `ThreatAssessment` — who considers you dangerous — six simulation callers, zero runtime.
- Active deals now show in "Between you" (`b8daeea`), but **`RenderNotebookStory` still lists
  promises, alliances, oaths and memories and no deals.**
- **The player's alliance can end without a word** *(in progress)*. `NpcAlliances.Dissolve` runs in
  every weekly settle over every active alliance, the player's included, and ends it when any
  member's score toward another is under -20 - which includes the partner's PRIVATE score toward the
  player - or when fewer than two members are still in the house. No event, no memory. Confirmed by
  `NpcAllianceTests.ThePlayersAllianceEndsSilentlyOnThePartnersPrivateScore` (a characterisation of
  the defect, mutation-checked both ways). How the player should learn of it - a notice derived in
  presentation, a logged event, or a memory - is the owner's decision: `Log` advances
  `nextSequence`, from which NPC alliance, promise, deal and house-event ids are minted that same
  week, and a player memory is what `ShareInformation` passes on as gossip.
- ~~The cast strip returns the same grey chip for a five-week ally and a stranger.~~ Done (§2).
- ~~`OutcomeChips` draws nothing when the delta rounds to zero.~~ Done (`b8daeea`).

### Track 4 — one product, not nine screens
`UiTheme.Glass` has seven direct callers and two through `HudPrimitives.Glass`, and six more
surfaces *look* like glass without calling it (`EpisodeHud.Chrome` at .94, `RelationshipWeb`'s
column, `CastRail` chips, the cast screen's frame, cards and pills) - fill alphas .851/.94/.949/1.0
and edges from .30 to 1.0 across five hues. `AddGlow` was the first move (§2). **Do not** revive the
`Emphasis`-parameter design without reading why it died. Next candidates: route `EpisodeHud.Chrome`'s
.94 and the four ceremony `CardGlass` sites together (converting one breaks it from its siblings,
and `CeremonyGlassPlayModeTests.AssertGlass` wants a Glow on each). On the cast screen's card ground,
`Edge(Resting)` measures 1.204:1 and `Edge(Interactive)` 1.53:1 against today's cyan hairlines at
1.69 and 2.25 - moving those edges is a design decision, not a cleanup. Do not regenerate the twelve
reference captures as a pinned baseline first.

### Track 5 — a conversation is a scene
~70 controls in one 900×300 panel whose viewport is ~174 units tall, of which ~50 are four verbs
repeated per houseguest. You get six actions a week. The Talk reply is done (§2). Six other verbs
have no reply writing of their own and answer with the standing sentence alone - the same one the
greeting ended on. Next: the two-step picker (`ChooseNominationPair` shows the shape), and move the
caption constants and their tests in one deliberate change. The conversation viewport is ~448 units
at standard text on the batchmode canvas (not 174 - that is the notebook).

### Track 6 — blocked on you (answer first, none is agent-schedulable)
1. **What is a season?** `DefaultHouseSize` is 8, the shipped scenario is seven people, and it ends
   at **week 4**. Several estimates in the brainstorm were costed for a twelve-person, seven-week
   season that does not ship. Upstream of at least six themes.
2. **Unity licence in CI secrets?** Determines whether CI gets a real test job or compile-only.
3. **Two Mixamo takes** (a dejected beat covering nomination and eviction; a relief beat with an
   upward move) and **one attentive Listen take**.
4. **An afternoon on the 24 UMA looks.**

⚠️ Everything in (3) and (4) lands only under `GAMESIM_UMA`, which **must never be committed**. A
fresh clone and every review build sees the Quaternius cast.

### Explicitly NOT doing, with reasons
- **The arc surface as proposed** — premise false, see §5.
- **Adding the player to the NPC meeting pool** — excluded by construction at `NpcSocial.cs:77`;
  including them changes reservation, approach and facing against 53 timing-sensitive tests.
- **"Social Goals"** — named in VISUAL-TARGET as if it were a card to draw. `grep goal` returns four
  hits, all web-save field names. There is no system.
- **Audio, career/multi-season, competitions, localisation, save slots, performance work** — all real,
  all deferred with reasons; see the brainstorm.

### Nobody had looked at
~~CI runs **no tests**.~~ It runs the simulation subset now (§2); the full suites still need a licence. `Assets/Gamesim/Audio/` is empty
and every sound is synthesised at 22050 Hz. No localisation table ships, so `Localisation.Text` has
been an identity function in every run ever made and the caption contract has never been exercised.

---

## 4. Traps this session fell into — read this part

**Green is not correct.** The broken version of click-to-talk passed PlayMode **293/293** an hour
before an audit found a blocker. If a feature's tests pass on the first run, mutation-test them.

**A guard that cannot fail is worse than no guard.** The new clipped-copy sweep passed immediately
and a mutation *survived* it: `SpeakerTitle` falls back to a wrapping `PanelTitle` when the portrait
is null, portraits load asynchronously, and asserting on the frame a panel opens exercises the one
layout incapable of clipping. **Let async content land before measuring anything visual.** This bit
three times: a capture photographed twelve empty discs; a click test lost its target during a camera
settle; and this.

**Verify the file, not the script's exit status.** `4b2e1a8` shipped `DestinationChosen` declared,
subscribed and **never raised** — dead code — because an edit script printed "ok" and the file was
not re-read. The PlayMode test correctly reporting it was deleted as unreliable. `9c77154` now
guards this class.

**Never chain `offline-compile.ps1` into a run with `&&`.** `grep` exits 0 when it *finds* an error,
so the tests run against a broken build and print `crashes: 0` with no XML. Happened twice. The
harness now says `NO XML — this is NOT a pass` and exits 3.

**A filtered run is not the suite.** Read as one three times in a day. The harness now says so.

**`isTextOverflowing` cannot see overlap.** It answers "does this text fit its own box". Two
elements in the same place both fit their own boxes. One tag collided with three different things in
three successive captured frames — its own bounds, the chevron `Action()` pins at −16, and the trust
reading a portrait row spends its right-hand end on.

**Takeover-panel captures are distorted.** `CaptureFraming` renders 16:9 while the batchmode canvas
is 4:3, and `SetActivityLayout` writes a fixed `sizeDelta` no layout pass recomputes. Do not measure
pixel positions of activity panels in these PNGs. Chrome is anchor-based and photographs correctly.

**A mutation that survives may mean the test checks less than the code promises.** The cast strip's
geometry test checked that pills stayed off the border; the code promises a unit of clearance, and
the branch that keeps the widest pair clear survived being deleted. The fix was the test, measured
against the code's own stated rule - not deleting the branch.

**Mutations in one round can mask each other.** Two cast-strip mutations ran together; one removed
every pair from the geometry sweep, so it failed on "never drew a pair" and the other was never
tested. Run mutations that aim at the same test in separate rounds.

**`cp` onto a file under `Assets/` can be refused** ("Permission denied") in this environment while
`sed -i` and Python writes succeed. A mutation restore that used `cp` left a mutated `WebRules.cs`
in the tree until `git checkout` put it back. Restore with Python and assert no `MUTATION` marker is
left.

**Captions are identity.** ~1690 tests find controls by exact words. Decorate *around* a caption.
When a caption must change, change it in the one helper every caller derives it from — that is how
the `"Propose a alliance invitation"` article fix corrected the tests and the standalone verification
for free.

---

## 5. Claims that were checked and found FALSE

Kept because they are load-bearing and plausible enough to be re-proposed.

- **"Relationship arcs are the player's own record."** Inverted. `EpisodeEngine.cs:1647` writes arcs
  when *neither* party is the player too. A naive arc panel leaks NPC-to-NPC state.
- **"There is no nominated branch in the diary."** `DiaryRoom.cs:507-515` is that branch.
- **"`activeModifiers` and `storylines` have zero runtime references."** Both have one, in
  `PortVerification.Season.Systems.cs`. A verification harness is not a player surface, so the
  substance survives — the claim as written does not.
- **"`DealStatus.Accepted` ordinals are pinned by saves."** `DealStatus` is a static class of string
  constants; there are no ordinals.
- **"A deal agreed in week 3 is forgotten by week 7."** There is no week 7 in a default season.
- **"`Chrome_WearsNoAccentEdge` sees the cast screen in the full suite."** Every
  `EpisodePlayModeTests` test reloads the scene; the Chrome test never opens the cast screen, and
  `CastSelect` has no children until `NewSeason` shows it.
- **"Three Talks in a week make a houseguest Friendly."** A Talk is +4 to the player's score and a
  default week allows three actions: 12, under the threshold of 15. The known-good route to a
  non-neutral standing is the recorded bloc walk (`ContentCatalog.Create(4)`,
  `socialBudgetRulesStartWeek = 2`, three Talks and an alliance).
- **"The Ask row needs no pill to read as free, because every committing row has one."** The oath
  declare/decline rows and the deal-decline row commit with no pill.

---

## 6. Housekeeping

- **Never commit** `ProjectSettings/ProjectSettings.asset` while it carries `GAMESIM_UMA`.
- The four **Inter SDF font atlases** churn ~700k lines; they have been left unstaged all session.
- `Assets/_Recovery/` is a crash artefact still sitting untracked in the tree.
- `Tools/baseline.txt` floors: EditMode **1395**, PlayMode **309**, SimulationTests **719**. Raise a floor in the same commit
  that adds tests; never lower one to make a red run green.
- A spawned task fixed the `DestinationChosen` raise **in this same working tree**, not a separate
  worktree. If you spin off tasks, expect concurrent edits to the files you are holding.
- **Why the synthetic floor click never landed**, answered by a later session and recorded in memory
  as *synthetic-clicks-need-a-frozen-camera*: a screen point measured before the click goes stale the
  instant the camera rig is handed a new subject, and `SelectHouseguest` refocuses the rig on the
  player mid-sequence. `ClearSubject`, then re-verify the point on the press frame. The floor-click
  test in `EpisodePlayModeTests.NpcClick.cs` was restored on that basis.

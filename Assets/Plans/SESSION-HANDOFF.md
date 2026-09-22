# Session handoff — 21–22 September 2026

Written for whoever picks this up next. It covers what shipped, what is in flight, what remains,
and — the part that will save you the most time — the traps this session fell into, several of them
more than once.

Branch: `port/game-flow-v2-pass`. Baseline before this session: `e45686f`.

---

## 1. What shipped

Seven commits, newest last.

| Commit | What it did |
|---|---|
| `d587aab` | **Walking animations.** The whole cast ran everywhere. |
| `bec990e` | **HUD relayout.** Cast strip to the floor, six-item nav rail in the freed gutter. |
| `c1b2983` | **Cast selection screen.** Portraits were unmasked black squares. |
| `4b2e1a8` | **Click-to-talk.** Clicking a houseguest only moved the camera. |
| `2053241` | **Pressability.** ~150 assertions asked a weaker question than they read as. |
| `9c77154` | **Three guards against misreading a result.** |
| `27bd14a` | **Track 2 visual guards.** A guard that cannot fail is worse than no guard. |

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

## 2. Track 3, first slice — committed

Three places where the simulation knew and the player was not told. Each test was mutation-killed.

- **`AskForIntel` returns something.** A control captioned *"Ask what they have heard"* spent one of
  six weekly actions and wrote its only memory with `target.id` as owner — the houseguest
  remembered being asked, the player learned nothing. Now the target hands over their own read on a
  third houseguest. The subject is chosen from the roll **already drawn** for the trust bump: a
  second `Roll(s)` would advance the stream and re-roll every season from that point.
- **Active deals appear in "Between you".** `RelationshipWeb`'s column listed alliances and promises
  and never deals, so a commitment was forgotten until breaking it cost up to −45. Listed now with
  its stakes, in the vocabulary the propose button uses.
- **An action that moves nothing says so.** `OutcomeChips` returned early when the delta rounded to
  zero, so one of six actions vanished without a word. It now reads *"No change in trust"*.

## 3. What remains — the six-track programme

From a six-agent brainstorm (four lenses, a fact-checking critic, a synthesis). Tracks 1 and 2 are
done. **Track 3 is where the player-facing value is.**

### Track 3 — say what the simulation already knows *(next)*
The project's signature failure: systems that exist and never reach a pixel.
- `WebRelationshipArcs` classifies every relationship with intensity 0–100 and five escalation
  labels, persisted through **eleven save versions** — `grep Arc` over the presentation layer returns
  **zero hits**. ⚠️ **But see §5: the "these are the player's own arcs" premise was checked and is
  FALSE.** Drawing them naively leaks NPC-to-NPC state.
- `ThreatAssessment` — who considers you dangerous — six simulation callers, zero runtime.
- Deal **stakes** surfaced in `4b2e1a8`; an **active deal is still invisible** everywhere the player
  would look, including `RenderNotebookStory`, which lists promises, alliances, oaths and memories
  and no deals. You can promise to use the veto on somebody and nothing reminds you until it breaks
  for −45.
- The cast strip returns the same grey chip for a five-week ally and a stranger.
- `OutcomeChips` draws one rounded number, and **nothing at all when the delta rounds to zero**.

### Track 4 — one product, not nine screens
`UiTheme.Glass` has nine call sites at differing alphas. Start by adding `Emphasis` and fill-alpha
parameters defaulting to today's behaviour so it lands inert, then convert **one** screen and look at
a frame. Do not regenerate the twelve reference captures as a pinned baseline first — this track
moves nine screens.

### Track 5 — a conversation is a scene
~70 controls in one 900×300 panel whose viewport is ~174 units tall, of which ~50 are four verbs
repeated per houseguest. You get six actions a week. Start with a **second beat** on one verb —
`HouseDialogue` is 549 lines of writing already shipped and spent one line per exchange. Only then
the two-step picker (`ChooseNominationPair` shows the shape), and move the caption constants and
their tests in one deliberate change.

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
CI runs **no tests** — `ci.yml` has one job checking file hygiene. `Assets/Gamesim/Audio/` is empty
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

---

## 6. Housekeeping

- **Never commit** `ProjectSettings/ProjectSettings.asset` while it carries `GAMESIM_UMA`.
- The four **Inter SDF font atlases** churn ~700k lines; they have been left unstaged all session.
- `Assets/_Recovery/` is a crash artefact still sitting untracked in the tree.
- `Tools/baseline.txt` floors: EditMode **1390**, PlayMode **300**. Raise a floor in the same commit
  that adds tests; never lower one to make a red run green.
- A spawned task fixed the `DestinationChosen` raise **in this same working tree**, not a separate
  worktree. If you spin off tasks, expect concurrent edits to the files you are holding.
- **Why the synthetic floor click never landed**, answered by a later session and recorded in memory
  as *synthetic-clicks-need-a-frozen-camera*: a screen point measured before the click goes stale the
  instant the camera rig is handed a new subject, and `SelectHouseguest` refocuses the rig on the
  player mid-sequence. `ClearSubject`, then re-verify the point on the press frame. The floor-click
  test in `EpisodePlayModeTests.NpcClick.cs` was restored on that basis.

# U08 acceptance matrix

U08's gate is "one pinned build passes the agreed acceptance matrix." That matrix was never written
down, which left the gate unfalsifiable — there was no list a build could be checked against. This is
that list.

**Thresholds marked _proposed_ are derived from the V6 measurements already recorded in
`UNITY_PORT_IMPLEMENTATION.md`, not agreed in advance.** They are starting numbers to accept, adjust
or reject. Everything else restates a gate the project has already been holding itself to.

One build is pinned per attempt. A criterion passes only with a retained artefact under `Logs/` or
`Builds/`; "it worked when I tried it" is not evidence.

## A — Automated, already passing at V6

Re-run against the pinned build. V6 evidence is cited so a regression is visible as a change.

| # | Criterion | Threshold | V6 evidence | V7 status |
|---|---|---|---|---|
| A1 | Edit Mode suite green | 0 failed, 0 skipped | 640/0/0, run `a35edf94…` | **passed** — 678/0/0 |
| A2 | Play Mode suite green | 0 failed, 0 skipped | 84/0/0, run `a1c11a52…` | **passed** — 114/0/0 |
| A3 | Strict clean build | Succeeded, 0 errors, 0 Gamesim-owned warnings | 144,167,539 bytes, 77.72 s | **passed** — 912,012,559 bytes, 0 errors, 685 warnings none Gamesim-owned |
| A4 | Headless season completes | exit 0, empty error array, season finishes | `Port-verify-…-d11b6394…` | **passed** — exit 0, 0 errors, winner decided |
| A5 | Graphical season completes | exit 0, empty error array | `Port-verify-…-7a0b9ebd…` | **passed functionally** — `graphical: true`, 0 errors, but its screenshots are black (see D6b) |
| A6 | Save/reload integrity | every check passes; profile slot bytes preserved | 7 reload checks | **passed** — 5 reload checks, jury reload verified, profile slot preserved |
| A7 | Migration smoke | the shipped build loads an old-schema save, upgrades it, and plays on | `a6ebd8f1…` | **passed** — v5 → v6 in the player, season Passed, 0 errors |
| A8 | NPC autonomy proof | ≥1 completed conversation, ≥1 reunion proof, physical-start proof on every start | 3 start proofs, 24 transitions | **passed** |
| A9 | Knowledge boundaries hold | no private coordination text in any player-visible surface | `AssertBlocPrivateEvidenceAbsent` | **passed** — bloc verification passed and the assertions are green |
| A10 | No stranding | a corrupted or interrupted save never leaves the run unplayable | 11 durable-transaction tests | **passed** — see below |

**The pinned V7 build is `D:\GamesimAcceptance\Builds\Port-Windows-V7\Gamesim.exe`**, built from the
final code — 912,012,559 bytes, 0 errors, 685 warnings none Gamesim-owned. It is the artefact section
E should be run against; see `PLAYTEST_PROTOCOL.md`.

It was built on the acceptance copy rather than the working project, because the interactive editor
holds the lock on the latter. Re-run `Gamesim > Port > Build Windows Desktop` locally if you would
rather pin a build made in place.

V7 evidence: `D:\GamesimAcceptance\QA-final\verification.json` — overall **Passed**, with season,
study, blocs and autonomy all Passed and zero runtime errors, against that exact executable — plus
`Logs/Port-v7-build-report.json`.

**The build is 912 MB against V6's 144 MB.** UMA accounts for essentially all of it. That is a
distribution decision worth taking deliberately rather than discovering later.

**A7 no longer depends on an archived file.** It was blocked on "an accepted V4/V5 QA save", an
artefact that has to survive alongside the repository and simply does not exist on a machine that
never had it. `Gamesim > Port > Write Legacy V5 QA Save` generates one instead — a genuine schema-5
payload with `npcSocial` removed and the checksum envelope rebuilt — so the criterion is runnable
anywhere. Verified end to end: the fixture went in at `schemaVersion: 5` with no `npcSocial`, the
**player build** loaded it, and the file came back at `schemaVersion: 6` with the subsystem seeded
after a season that Passed with zero errors.

Its limit, stated rather than glossed: the generated save is a fresh season, so it exercises the
migration path without the history a long-running archived save would carry. The five Edit Mode
migration test files cover the rich cases; this covers the one thing they cannot, which is that the
shipped executable does it too.

**A10 was originally filed under E, wrongly.** "Offline play, expired authentication, interrupted save
cannot strand the episode" reads like a playtest criterion but is a robustness property, and
`EpisodeDurableTransactionTests` already covers most of it: a failed write before commit, an exception
after replacement, a divergent primary, an unreadable primary, re-entrant commits, and failed or
ambiguous NPC ticks. The round trip is what matters — corrupt the save, enter recovery, restore the
validated backup, and **commit a decision again** — and that last step was the one thing not asserted,
so it now is. Offline needs no test: there is no network path, and the settings panel says so. Expired
authentication is not applicable while no authentication exists; it becomes a real criterion the day
online accounts are connected.

### Harness note — what a headless run cannot decide

A1 and A2 can be run without the interactive editor, against a copy of the project:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -accept-apiupdate -projectPath D:\GamesimAcceptance -runTests -testPlatform PlayMode -assemblyNames Gamesim.PlayModeTests -testResults D:\GamesimAcceptance\playmode-results.xml -logFile D:\GamesimAcceptance\playmode.log
```

Two things to know before trusting the output.

**Filter to the Gamesim assemblies.** Unfiltered, an Edit Mode run picks up UMA's own bundled test
assemblies — 1,243 cases, of which 37 fail inside `UMA.Editors`, `UMA.Tests` and `UMA.TexturePaint`.
Those are a third-party package's tests and say nothing about this project, but they will make a
green suite look red.

**Input-driven tests need `HeadlessInputSettings`, which is already in the suite.** Tests that add a
synthetic Mouse or Keyboard and queue state events get nothing in batchmode, because the Input
System's default `backgroundBehavior` disables non-background devices and a batchmode editor never
holds focus. `Assets/Gamesim/Tests/PlayMode/HeadlessInputSettings.cs` relaxes that, gated on
`Application.isBatchMode` and living in the test assembly, so it affects neither a player build nor an
interactive session. Without it, seven tests fail in a way that looks exactly like product bugs.

The expected headless result is **114/0/0**. Anything else is a real regression.

## B — New in V7

| # | Criterion | Method | Threshold | Status |
|---|---|---|---|---|
| B1 | Ceremony cards appear | `Presentation_CeremonyCardsFireDuringAPlayedEpisode` | nomination, veto and eviction each show the right headline carrying the committed text | **passed** — captured as `ceremony-*-in-episode.png` |
| B1b | Cards cover no chrome | same test | the card overlaps none of the five fixed panels | **passed** |
| B2 | Cards never block play | `CeremonyStingPlayModeTests` | no `GraphicRaycaster`, every graphic non-raycasting, not under the director | **passed** |
| B3 | Cards sit as a top banner | `Card_SitsAsATopBannerAtEveryTextSize` | not clipped by the canvas, stays in the upper half, does not collapse | **passed** |
| B4 | Cards respect reduced motion | `ReducedMotion_KeepsTheInformationAndDropsTheMovement` | no travel, no fade; information still shown | **passed** |
| B5 | UMA cast builds | `Houseguest_BuildsARiggedBodyOfHumanProportions` | >1000 verts, >100 bones, fully rigged | **passed** |
| B5b | Every cast look resolves | `EveryCastLook_ResolvesItsRaceAndWardrobe` | all six races and every wardrobe recipe found in the Global Library | **passed** |
| B5c | UMA body replaces the fallback | `BuiltBody_ReplacesThePrimitiveRigRatherThanJoiningIt` | no primitive rig under a UMA houseguest | **passed** |
| B6 | UMA shading matches the set | `BuiltBody_IsFlattenedTowardTheHouseLook` | smoothness ≤ 0.1, metallic 0, bump scale 0 | **passed** |
| B6b | UMA proportions match the set | `Houseguest_BuildsARiggedBodyOfHumanProportions` | stylised height 1.5–1.85 m, every house DNA value applied | **passed** — 1.72 m, from 2.05 m un-stylised |
| B6c | The cast reads at gameplay distance | `Accessibility_ReportsHowLargeTheCastReadsOnScreen` | houseguests legible at the default camera | **measured; default kept at 24 by decision** — see below |
| B7 | UMA is genuinely optional | Clone without `Assets/UMA`, committed settings | project compiles, Edit Mode green, define stays off | **passed** — 0 errors, 678/678 |
| B8 | UMA does not regress the suite | Full Edit + Play suites with UMA installed | A1 and A2 still hold | **passed** — 678/0/0 and 114/0/0 with UMA installed |
| B9 | The seam defaults to the project's own body | `CharacterBodyProviderPlayModeTests` | no provided body appears unless a provider supplied one, and no test leaks a provider | **passed** |
| B10 | The episode runs on UMA bodies | `Gamesim/UMA/Use UMA bodies in the episode`, then the full suite | suite stays green with UMA live in `EpisodeHouse.unity` | **passed** — 114/0/0 |
| B11 | No pop-in while UMA assembles | `Houseguest_IsNeverInvisibleWhileUmaAssemblesTheBody` | a body is drawable from the attach frame; the stand-in is retired once the real body exists | **passed** |

B3's threshold is two bounds rather than one because the card descends into place: it is never lower
than where it settles, but it is deliberately part-way above the screen edge for the 0.22 s entrance.

**B7 has a standing hazard.** It passes only while `GAMESIM_UMA` is absent from the committed
`ProjectSettings.asset`. The bootstrap adds that define locally on any machine with UMA, so the file
goes dirty routinely; committing that hunk breaks every UMA-less clone *irrecoverably*, because the
compile errors it causes stop the very `[InitializeOnLoad]` that would clear it. Confirmed
empirically, second open included. Committed value must stay
`SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY`.

**B6c turned out not to be an art criterion.** Measured at the default camera framing, houseguests
project to **1.4%–2.2% of frame height — 7 to 11 pixels at the harness resolution — from 42 to 54
metres away**. No character work reaches that: no DNA setting, no proportion change and no different
art makes a seven-pixel figure legible, which is why two successive casts both "read small" and why
restyling UMA improved the silhouette without changing the impression.

The open question is therefore about the camera, not the cast, and it is a design decision rather than
a defect. The default framing shows the whole house and the player can zoom in, so this may be
deliberate — an overview to zoom from. But the slice contract asks for "character selection and
cinematic close-ups during conversations", and at 1.6% of frame a houseguest cannot be recognised,
let alone chosen. Whether the default should start closer is yours to decide; the number is now on the
table instead of an impression.

The test reports rather than asserts, deliberately. Contrast has WCAG to appeal to; character
legibility has no standard, and inventing a threshold here would dress a preference as a measurement.

`Accessibility_ComparesCameraFramings` renders the house at the rig's default of 24, its midpoint and
its minimum of 10, writing `camera-distance-*.png`. The comparison settles which half of the problem
is real: **at 17 a houseguest reads clearly — hair, clothing, skin tone, a legible name label — and at
24 the same character is eight pixels.** The cast is not the problem and never was.

**Decided: the default stays at 24.** Starting wide is the intended first impression, with the player
zooming in from there. B6c is therefore closed as a deliberate choice rather than an open defect — but
it is the assumption the playtest is most likely to test, so E2 should note whether participants find
the zoom without being told.

B9 says "the project's own body", not "the primitive rig", on purpose. The fallback is an authored
prefab from `Resources/GamesimCharacters/` when the persona has one and the primitive rig when it
does not. Both are correct; what the seam guarantees is that no provider supplied it.

## C — Performance

Measured on the reference machine (GTX 1060 6 GB / i5-8400, 1600×900). These are frame intervals from
an automated profile, not isolated GPU measurement or broad hardware certification.

| # | Criterion | Threshold | V6 measured | V7 batchmode |
|---|---|---|---|---|
| C1 | Median frame time | _proposed_ ≤ 2.0 ms | 1.310 ms | 0.285 ms |
| C2 | p95 frame time | _proposed_ ≤ 3.0 ms | 1.903 ms | 0.528 ms |
| C3 | p99 frame time | _proposed_ ≤ 4.0 ms | 2.411 ms | 0.659 ms |
| C4 | Profile duration | ≥ 300 s unbroken, no overlapping asset work | 300.03 s, 211,388 frames | 300.02 s, 946,359 frames |
| C5 | Frame cost with six UMA bodies | _proposed_ ≤ 2× the C1–C3 figures | not measured | **1.24×–1.52×** over two runs |

**Do not read the V7 column as a pass.** It was captured in `-batchmode`, which renders without
presenting to a window: 946,359 frames in 300 s is roughly 3,150 fps against V6's 704, and the
difference is mostly the absent present and vsync path, not the game getting four times faster. The
thresholds were derived from a windowed run and must be re-measured in one. The column is recorded
because it is real evidence of *stability* — 300 s unbroken with zero runtime errors — not of speed.

C5 is now measured by `UmaCastCostPlayModeTests`, which builds the same six houseguests twice under
identical conditions — once on the authored prefabs, once on UMA — and compares medians. Two runs
gave **1.52×** (0.265 → 0.404 ms) and **1.24×** (0.418 → 0.520 ms). Report it as roughly **1.2×–1.5×**
and do not treat either figure as precise: the spread across runs is comparable to the effect being
measured, which is the honest state of a sub-millisecond batchmode measurement. The ratio is the
meaningful part; the absolute numbers carry this section's batchmode caveat. The profiled season
itself still ran on the prefabs, because no scene carries `GamesimUmaCast` yet.

Together with the **+768 MB** of build size, that is the cost side of the decision to put UMA in the
shipping episode. Both numbers are now known rather than guessed.

C5 exists because UMA is the one change since V6 with an obvious cost: each body is roughly 16k
verts, 229 bones and eight submeshes backed by runtime-generated 1024² atlases. It has never been
profiled with a full house, and the pipeline notes flag this as unknown rather than assumed fine.

## D — Accessibility

| # | Criterion | Method | Threshold | Status |
|---|---|---|---|---|
| D1 | Font scale does not clip | `Accessibility_NoCopyIsClippedAtEitherTextSize` | no `TMP_Text` overflows at standard or larger text | **passed** |
| D1b | Panels do not collide | `Accessibility_FixedChromeNeverOverlapsAtEitherTextSize` | no two fixed chrome panels overlap at either text size | **passed** |
| D2 | Keyboard traversal complete | Tab / ↑ / ↓ / Enter through every panel | every action reachable and committable without a mouse | **partial** — world buttons are `Navigation.Mode.None` while a modal is open, and diary travel by keyboard is covered; no single test walks every action |
| D3 | Focus survives rebuilds | Existing modal-rebuild assertions | selection restored to the equivalent control | **passed** |
| D4 | Reduced motion is total | `PresentationAccessibilityPlayModeTests`, `ReducedMotion_KeepsTheInformationAndDropsTheMovement` | no camera reframing, no HUD animation, no card travel | **passed** for camera and cards |
| D5 | Captions present | Autonomy workload caption-proof frames | every witnessed beat has on-screen text | **passed** — 4,111 verified caption frames in V6's run |
| D6 | Contrast meets WCAG AA | `UiThemeContrastTests` | body ≥ 4.5:1, large text ≥ 3:1, outline distinguishable from fill | **passed** |
| D6b | Contrast holds over the set | `Accessibility_CapturesTheHudOverTheSetForReview` | HUD readable against the set at all three | **captured** — `hud-over-set-*.png`; bloom excluded, see below |

D6 was written as something to judge by eye, which made it the one criterion nobody could fail
definitively. Contrast is a ratio, so the arithmetic half is now computed: every text-on-panel pairing
in `UiTheme` clears WCAG AA.

D6b now produces real frames at all three resolutions, with one limit stated rather than buried: they
are rendered by `camera.Render()` into a RenderTexture, which **skips the URP post-processing pass**,
so the set's bloom is not in them. They settle HUD-against-geometry; HUD-against-bloom needs a
windowed run.

**Do not use the standalone harness's own PNGs for this.** In a `-batchmode` player run Unity renders
without presenting, so `ScreenCapture` returns solid black: every screenshot the V7 verification wrote
is an identical 18,959-byte black frame. The report still says `graphical: true`, because a graphics
device did exist. Those runs are good functional evidence and worthless visual evidence.

## E — Human acceptance

The genuinely open part of U08, and the only part still open. None of it is automatable and no
automated pass substitutes for it: every criterion here is a judgement about how the episode *feels*
to someone encountering it, which is not a property of the code. The former E6 moved to A10 because it
was a robustness property mis-filed as a judgement.

**`PLAYTEST_PROTOCOL.md` is how to run it.** Being unautomatable does not make these criteria vague:
each has a threshold, a way of observing it that does not lead the participant, and a note on what to
record when it fails. Run to that protocol, a session produces evidence the next one can be compared
against; run to impressions, it produces a feeling.

| # | Criterion | Method | Threshold |
|---|---|---|---|
| E1 | Episode pacing | Timed play of one full episode | 30–45 minutes without skipping |
| E2 | First-time comprehension | ≥ 3 players who have never seen the build | each reaches eviction unaided |
| E3 | A decision feels consequential | Post-play interview | each player can name a promise or betrayal that changed a later outcome, and why |
| E4 | Loss is intentional | Play a losing run | the ending reads as an ending, not a failure state |
| E5 | Visual bar | Art review of the pinned build | house and cast read as one production, not placeholder plus asset pack |

## Out of scope for this matrix

Recorded so absence is not mistaken for failure. These are not V7 gates:

- Full web parity — deals, dynamic alliance and grudge lifecycle, autonomous strategic schedules,
  full diary and activity scheduling, crises and storylines, weekly focus and full mental-state
  modifiers, and the complete minigame catalogue.
- Optional online accounts, cloud saves and AI services, which remain unconfigured.
- Full-cast multi-phase web-save migration beyond the supported restricted import.
- The staged needs/furniture rule leaves, which have no live gameplay consumer.

## How to pin a build

1. Confirm a clean editor: no compile errors, nothing playing or importing.
2. Run A1 and A2. Stop on any failure; do not proceed with a known-red suite.
3. Build to a fresh `Builds/Port-Windows-V<n>/`, never overwriting an accepted build.
4. Run A4–A8 against that executable.
5. Record C1–C4 from one unbroken profile, plus C5 with a full UMA house.
6. Walk D1–D6 by hand and capture the three resolutions.
7. Schedule E1–E6. Only then is the build accepted.
8. Retain every log, including failures. The V6 record keeps its failed runs on purpose, and that is
   the practice worth continuing.

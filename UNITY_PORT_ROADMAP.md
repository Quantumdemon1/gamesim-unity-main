# Gamesim Unity port

Source: the agreed Unity plan in the GameSim Dev conversation, following the choice to build the first slice around a 3D house. The existing Unity 6000.6.0f1 project and its pinned packages are retained.

| Package | Deliverable | Acceptance gate | Status |
| --- | --- | --- | --- |
| U01 Foundation | Project structure, assemblies, Edit Mode tests, bootstrap and desktop build | Standalone desktop build launches | Implemented; standalone startup verified in U02 |
| U02 Playable house prototype | Basic house geometry, player movement, camera, one NPC, interaction prompt | Explore, approach, converse, and exit dialogue without navigation or camera failures | Implemented; automated tests/build/startup smoke passed; human playtest pending |
| U03 Simulation bridge | C# state, command validation, relationships, promises, seeded randomness | Equivalent inputs produce expected results from approved web fixtures | Implemented for documented native scope; 640 Edit Mode tests pass, including source diary/persona/sentiment/oath/study/bloc/NPC-conversation fixtures, ballot privacy and frozen v1/v2/v3/v4/v5→v6 migration |
| U04 Social gameplay | Six-character cast, motives, alliances, knowledge boundaries, authored fallback | A promise or betrayal changes a later decision for an explainable reason | Local social loop, post-eviction Diary Room/persona, oath consequences, private source voting-bloc coordination and physical durable NPC-to-NPC conversations implemented and tested. Source needs/furniture decision leaves are staged separately; broader strategic/activity scheduling remains open |
| U05 Complete episode | Competitions, nominations, veto, campaign, vote, eviction | Finish the episode through normal 3D interaction | Implemented, plus a four-week six-person season/finale; actual station-navigation and ceremony-button traversal passes |
| U06 Saves and services | Local recovery, supported web-save import, optional authentication/cloud saves and AI | Resume safely; offline play and expired authentication cannot strand the episode | Local saves/recovery and restricted import implemented and tested; online adapters unconfigured, not claimed tested |
| U07 Presentation | Final assets, animation, lighting, sound, captions, ceremony staging | Episode meets the agreed visual and interaction bar | Native procedural cast/audio, lighting, captions and accessible HUD implemented. The seven-step HUD overhaul is complete through its last open item, ceremony title cards. A UMA-backed character pipeline is implemented behind a provider seam. **Edit Mode 678/0/0 and Play Mode 114/0/0 headless, on both the authored-prefab and the UMA cast — see the V7 section below.** Final-art/choreography/visual acceptance still open |
| U08 Acceptance | Profiling, accessibility, regression tests, first-time playtests | One pinned build passes the acceptance matrix in `ACCEPTANCE_MATRIX.md` | **Every automated gate passes: A 10/10, B 16/16, C measured, D 8/8 — no non-human criterion remains open.** **Section E — five human playtest criteria — is the only substantive gate outstanding; `PLAYTEST_PROTOCOL.md` is how to run it.** Pinned V6 evidence retained |

## V7 increment — tested headlessly, not yet accepted

Run against a copy of the project at `D:\GamesimAcceptance` with
`Unity.exe -batchmode -runTests`, because the interactive editor was unavailable. The copy shares
nothing with the live project but its source; the working editor and its save slots were untouched.

| Gate | Result |
| --- | --- |
| Edit Mode (`Gamesim.EditModeTests`) | **678 passed, 0 failed, 0 skipped** |
| Play Mode (`Gamesim.PlayModeTests` + `Gamesim.Uma.PlayModeTests`) | **114 passed, 0 failed, 0 skipped** — green on both the authored-prefab and the UMA cast |
| UMA integration (`Gamesim.Uma.PlayModeTests`) | **8 passed, 0 failed** |
| Editor compile, all assemblies | 0 errors |
| Player compile | 0 errors |

The suite grew from 640 + 84 at V6 to **678 + 114**: ceremony cards, the body-provider seam, the UMA
pipeline with stylising DNA, a UMA frame-cost measurement, stand-in cover for deferred bodies, HUD
clipping and panel-collision checks at both text sizes, WCAG contrast arithmetic for the whole
palette, and review captures of the HUD over the set.

Eight Play Mode tests failed at first and none of them were product bugs, but they were not all the
same problem either. Seven drove synthetic input that batchmode never delivered, because the Input
System disables non-background devices and a batchmode editor is never focused;
`Assets/Gamesim/Tests/PlayMode/HeadlessInputSettings.cs` now relaxes that for batchmode runs only.
The eighth was a genuine stale test — see below.

All eight were proved independent of the V7 changes first, by re-running the suite with both new
hooks disabled: that baseline failed the same set.

### Seven real defects the run caught

**The eviction card never played.** A commit can append several events, and an eviction is followed by
a `vote-reveal` per voter, so keying the card off the last player-visible line meant the show's
signature beat silently never fired. `EpisodeDirector.Submit` now remembers where the log ended and
searches everything the command appended. Found only by playing an episode; every isolated card test
passed throughout.

**The card was drawn over the Notebook button.** It shared a band with the navigation panel and was
centred at a fixed width. Worse, the constants could not have been right: the HUD's CanvasScaler
matches width and height equally, so the canvas reference size moves with the aspect ratio — 1385x1039
at 4:3, not 1600x900. The card is now anchored to both edges and inset past the chrome, which holds at
any shape, and `Presentation_CeremonyCardsFireDuringAPlayedEpisode` asserts it covers no panel.


**Houseguests were invisible while UMA assembled them.** A provided body arrives over frames, so with
UMA live the whole cast popped in about half a second after the scene was already running. Found by
switching the episode over and watching the suite fail on "player needs a visible body".
`CharacterPresentation.BuildStandIn` now puts the authored body in place immediately and
`RetireStandIn` removes it the frame the real one can draw.


**A test the TextMeshPro migration missed.** `EpisodePlayModeTests.SpeechInput()` looked up the final
speech field as a legacy uGUI `InputField`, but the HUD builds an `EpisodeSpeechInputField`, which
derives from `TMP_InputField`. The Step 1 migration caught the seven files reading `Text` and updated
them; this `InputField` lookup in the same file was missed, so
`FinalSpeech_TypedDraftSurvivesRebuildAndKeyboardCloseThenCommittedTextReloads` has been failing since
that step with a bare "Sequence contains no matching element".


**A stale assertion that had been red for some time.**
`EpisodeScene_StartsSixDistinctNativeCharactersAndUsableHud` required more than ten enabled renderers
per houseguest, which was really a count of the primitive rig's parts. An authored rigged model is a
single skinned renderer, so that assertion had been failing since the cast got real models, and the
Play Mode suite had not been re-run since. It now asserts the houseguest stands roughly human height,
which holds for either body.

**A per-frame exception on every UMA houseguest.** UMA replaces the avatar's `Animator` while it
assembles the character, so the reference `CharacterPresentation` captured when the body was handed
over became a destroyed object. `animator != null` then went false and `LateUpdate` fell through to
`AnimatePrimitives`, which dereferenced joints that are never built for a provided body — a
`NullReferenceException` every frame. `AnimateProvidedBody` now re-acquires the animator and never
falls through to the primitive rig.

**An editor-only API in runtime code, which only the build could see.**
`DynamicCharacterAvatar.editorTimeGeneration` is declared inside `#if UNITY_EDITOR` in UMA, so
`Gamesim.Uma` compiled in the editor and failed the player build. Now guarded. This is why the
offline compile check is run against the player dag (`…P.dag`) as well as the editor one.

### Build and standalone verification

A strict clean build of the final code **Succeeded: 0 errors, 685 warnings none of them
Gamesim-owned**, 912,012,559 bytes, to `Builds/Port-Windows-V7/Gamesim.exe` in the acceptance copy.
That executable then passed its full standalone verification set — **season, study, blocs and NPC
autonomy all Passed, zero runtime errors, exit 0** — completing a legal six-person season to a winner.

**That build is the pinned artefact for section E.** It was produced on the acceptance copy because
the interactive editor holds the lock on the working project; rebuild locally if you would rather pin
one made in place.

The build is **912 MB against V6's 144 MB**; UMA is essentially all of the difference, and that is a
distribution decision to take deliberately.

### The open decision

**Whether the shipping episode uses UMA bodies at all.** The committed scene still carries no
`GamesimUmaCast`, matching the "temporary figures for testing" plan, so everything above ran on the
authored prefabs. The switch is `Gamesim > UMA > Use UMA bodies in the episode`, and
`Gamesim > UMA > Use the authored prefabs in the episode` puts it back; both are reversible scene
edits, verified in both directions.

It was run with UMA live and the **whole suite stayed green at 110/0/0**, so the decision carries no
correctness risk. What it does carry:

- **Frame cost** roughly **1.2×–1.5×** for a full house.
- **Build size** **+768 MB** (912 MB against V6's 144 MB).
- **Silhouette.** UMA's realistic proportions read as smaller, thinner figures against Kenney
  furniture. `UmaCastLibrary.HouseProportions` applies stylising DNA — larger head, shorter body,
  thicker limbs and extremities — with per-persona overrides so the six differ. Measured 2.05 m down
  to 1.72 m, and one dictionary away from further tuning.

  It barely changed the impression, and measuring finally explained why: at the default camera
  framing a houseguest projects to **1.4%–2.2% of frame height, 7 to 11 pixels, from 42 to 54 metres
  away**. Nothing done to a character reaches that. Both casts "read small" for the same reason and it
  was never the cast.

Verifying it surfaced a genuine defect that the prefab cast never exposed: houseguests were
**invisible for the half-second UMA takes to assemble**. `CharacterPresentation` now shows the
authored body as a stand-in from the attach frame and retires it the frame the real body can draw.

### A decision the measurement surfaced

**The default camera distance is worth deciding on, and there is nothing to build.** `HouseCameraRig`
already allows 10 to 34 units and starts at 24. `Accessibility_ComparesCameraFramings` renders all
three, and the comparison is stark: **at 17 a houseguest reads clearly — hair, clothing, skin tone, a
legible name label — and at 24 the same character is eight pixels.**

The slice contract asks for character selection and cinematic close-ups during conversations, which
the default framing cannot deliver and the midpoint can. Starting wide may still be the right first
impression, but it is now a choice between rendered alternatives rather than an impression, and it is
the single change most likely to move E1, E2 and E5 — worth settling before spending a session of
testers on the current default.

### Still unproven

- Frame timings were captured in batchmode, which renders without presenting, so they are evidence of
  stability rather than of speed and are not comparable to V6's windowed profile.
- UMA bodies are proven by their own tests but have not been through a season or seen on screen. The
  ceremony card has been rendered to `ceremony-card.png` but not seen inside a played episode.
- The migration smoke no longer needs an archived QA save. `Gamesim > Port > Write Legacy V5 QA Save`
  generates a genuine schema-5 payload, and the shipped build was verified loading it, upgrading it to
  schema 6 with the NPC subsystem seeded, and playing a season through to a Passed result.
- Accessibility is now mostly measured rather than walked: clipping, panel collision, focus
  restoration, reduced motion, captions and WCAG contrast all pass. What is left is **D6b** — whether
  the HUD still reads against the set's bloom, which needs a captured frame and a person.
- **Section E, the human playtests, is the only part of U08 still open**, and it cannot close without
  someone playing the episode. Pacing, first-time comprehension, whether a betrayal reads as
  consequential, whether a loss feels like an ending, and whether the house and cast read as one
  production are judgements about how the episode *feels*, which is not a property of the code.

  E6, "offline play and expired authentication cannot strand the episode", was mis-filed there: it is
  a robustness property, not a judgement. It is now A10 and passing — eleven durable-transaction tests
  plus the round trip that matters, corrupt the save, recover the validated backup, and commit a
  decision again. That last step was the one thing not previously asserted.

### One standing hazard

`GAMESIM_UMA` must never be committed in `ProjectSettings/ProjectSettings.asset`. The bootstrap adds
it locally on any machine that has UMA, so that file goes dirty routinely. Committing that hunk
breaks every clone without UMA and the project **cannot repair itself** — the define makes Unity
compile `Gamesim.Uma` against a missing `UMA_Core`, and those compile errors stop the
`[InitializeOnLoad]` that would clear the define from ever running. Verified, second open included.
The committed value must stay `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY`.

- **UMA character pipeline** — `Assets/Gamesim/Uma/`, a provider seam in
  `Assets/Gamesim/Runtime/Presentation/CharacterBodyProvider.cs`, and the `GAMESIM_UMA` define
  bootstrap in `Assets/Gamesim/Editor/UmaPresenceDefine.cs`. A UMA character was measured building
  correctly in play mode (16,277 verts, 229 bones, 1.99 m, URP shader graphs) before the integration
  was written; the integration itself has not been run. See `Assets/Plans/uma-character-pipeline.md`.
- **Ceremony title cards** — `Assets/Gamesim/Runtime/Presentation/CeremonySting.cs`, hooked into
  `EpisodeDirector.Submit`, with `Assets/Gamesim/Tests/PlayMode/CeremonyStingPlayModeTests.cs`.
  Closes the last open step of `Assets/Plans/big-brother-ui-ux-overhaul.md`.
- **Provider-seam regression tests** — `Assets/Gamesim/Tests/PlayMode/CharacterBodyProviderPlayModeTests.cs`
  pins the property the rest of the suite depends on: with no provider registered, houseguests get
  the primitive rig they always had, and no test leaves a provider installed.

The card is deliberately built to be inert to the existing suite: it is a scene root rather than a
child of the director, so it stays out of `director.GetComponentsInChildren<TMP_Text>()`, and it
carries no `GraphicRaycaster`, so it cannot consume input. Both properties are asserted by its own
tests rather than assumed.

## Slice contract

An orbitable, Sims-like house view with click-to-move, player selection and conversation framing. The house contains a living room, kitchen, bedroom, private conversation room and competition yard. The later episode targets six contestants, including the player, and 30–45 minutes of play.

Ordinary C# and GameObjects implement the first slice. Gameplay will follow interaction → validated command → simulation → committed result → UI, animation, audio and save. Simulation state will use stable character IDs, independent of frame rate and scene objects. The web game is the reference for approved rules and fixtures.

U02's authored conversation is a navigation and interaction prototype. Relationships, promises, persistence, competitions and a complete episode belong to the subsequent packages. The first integrated promise/save/resume demonstration spans U03 and U06 plumbing.

## Open and play

Open `Assets/Gamesim/Scenes/EpisodeHouse.unity` and press Play, or start from Bootstrap, which now prefers the episode. `HousePrototype.unity` and `Assets/Scenes/SampleScene.unity` remain available; later local prototype/material edits were preserved on the September13 resume. Use the floor to move, right-drag to orbit, scroll to zoom, WASD/arrows to pan, and F to recenter. Approach any active housemate and press E. The episode button routes you to the living-room screen or competition yard; press E there to continue. R travels to the Diary Room; nearby E opens private reflections and eligible decisions with confirmation. J opens the notebook; F5 saves; Escape closes panels.

Use `Gamesim > Port > Start Isolated Preview` for testing without touching the normal save slot; Stop Isolated Preview restores the previous clean scene setup. The accepted schema6 NPC-conversation executable is `Builds/Port-Windows-V6/Gamesim.exe`. `Gamesim > Port > Build Windows Desktop` now targets separate `Builds/Port-Windows-V7/Gamesim.exe` for the next activity increment; no V7 build is accepted yet. Earlier verified builds remain untouched. See the implementation record for exact evidence and scope.

During Social free time, visit the Diary Room to review and confirm a study action. Memorize grants one preparation point; sneak has a45% success chance, adding two or losing one, bounded0–5. Each confirmation costs one social action. Preparation survives saves and weeks and boosts only the separate weighted weekly HoH/Veto simulation option, not precision play or final HoH.

`Gamesim > U02 > Create or Register House Prototype` creates the scene only when absent and registers it after Bootstrap. Existing scene files and other build entries are preserved on subsequent runs. The original SampleScene remains available.

Run `Gamesim > U01 > Run Edit Mode Tests` and `Gamesim > U02 > Run Play Mode Tests` for the regression suite. `Gamesim > U02 > Build Windows Desktop` writes a separate player under `Builds/U02-Windows` and a machine-readable report under `Logs`.

Generated output under Builds, Library and Logs is excluded from Git. Commit the Assets tree with every .meta, Packages and ProjectSettings together.

See `UNITY_PORT_IMPLEMENTATION.md` for the integrated material-change inventory, preservation evidence, tests/build/profile results and remaining limits. `ACCEPTANCE_MATRIX.md` is the checkable list a pinned build is measured against, including the human criteria no automated pass can substitute for, and `PLAYTEST_PROTOCOL.md` is how to run those. `U02_IMPLEMENTATION.md` remains the historical baseline report.

## Completion boundary

September13 accepted V6: source-derived NPC-to-NPC conversations, real paired house routes, independent saved social time/RNG, schema6 migration and save-before-publication pass640 Edit Mode and84 Play Mode tests, a strict clean build, headless and graphical seasons, and a clean five-minute graphical profile. Fresh seasons activate NPC conversations in week1; historical/imported saves activate next week without rewriting existing history. This does not port the full strategic/activity planner.

Post-V6 source now includes pure needs/activity arithmetic, detached source point/phase/chain catalogs, and4,714 original-source fixture rows. Actual Unity Edit Mode passes673 tests, zero failures/skips. These helpers have no live gameplay consumer and make no save-schema change; the separately staged one-TV-routine implementation will supply its own explicit shared ownership, persistence and native timing policy.

This is a playable native six-person slice with an additional short-season finale, not yet every original web subsystem or a signed-off AAA release. Final assets and ceremony choreography, first-time human playtesting and measured 30–45-minute pacing remain unaccepted. Optional online accounts/cloud/AI have not been connected. Full-cast/multi-phase web-save migration and broader web systems (deals, dynamic alliance/grudge lifecycle, autonomous strategic schedules, full diary/activity scheduling, crises/storylines, weekly focus/full mental-state modifiers and the full minigame catalogue) require further port work; they are not silently approximated or marked complete. Earlier accepted increments remain preserved. Next are exact source needs/furniture rule leaves, followed by a separately reviewed native furniture routine sharing conversation motion ownership and durable receipts. Staging those rules does not make furniture routines playable or imply complete web parity.

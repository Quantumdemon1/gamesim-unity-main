# V7 change manifest

Everything below is uncommitted. Git is not installed on this machine, so this session could not
commit; this is the review guide and commit body for when it is.

Do not rely on file timestamps to reconstruct this — a Unity reimport touches files nobody edited.
This list is what actually changed.

## Suggested commit message

```
V7: ceremony cards, UMA character pipeline, and an acceptance matrix that can be checked

Adds broadcast title cards for nomination, veto and eviction; a body-provider seam with a
UMA-backed implementation behind it; and the first written acceptance matrix, with every
automated criterion now passing.

Seven defects found by running the thing rather than reading it, including an eviction card
that never fired and an editor-only API that broke the player build.

Edit Mode 678/0/0, Play Mode 114/0/0, strict build clean, standalone verification passing
including a v5 to v6 migration in the shipped executable.
```

## New — presentation

| File | Why |
| --- | --- |
| `Runtime/Presentation/CeremonySting.cs` | Broadcast title cards; closes the last open step of the HUD overhaul plan |
| `Runtime/Presentation/CharacterBodyProvider.cs` | The seam: an interface plus registry, so `Gamesim.Runtime` never references UMA |

## New — UMA integration (all constrained on `GAMESIM_UMA`)

| File | Why |
| --- | --- |
| `Uma/Gamesim.Uma.asmdef` | Excluded entirely when UMA is absent |
| `Uma/UmaCastLibrary.cs` | Persona to race, wardrobe, colours and stylising DNA |
| `Uma/UmaBodyProvider.cs` | Builds a `DynamicCharacterAvatar` per houseguest |
| `Uma/UmaBodyTint.cs` | Applies proportions, stylising and fabric colour once UMA finishes |
| `Uma/UmaStylizer.cs` | Flattens generated materials toward the flat-shaded set |
| `Uma/GamesimUmaCast.cs` | The per-scene opt-in switch |
| `Uma/Editor/UmaCastPreview.cs` | Stages the whole cast for side-by-side review |
| `Uma/Editor/UmaEpisodeCastSetup.cs` | Menu items to switch the episode between casts, both ways |
| `Editor/UmaPresenceDefine.cs` | Sets or clears `GAMESIM_UMA` from whether `Assets/UMA` exists |

## New — tests and QA tooling

| File | Why |
| --- | --- |
| `Tests/PlayMode/CeremonyStingPlayModeTests.cs` | Card geometry, inertness, reduced motion, and a rendered image |
| `Tests/PlayMode/CharacterBodyProviderPlayModeTests.cs` | No provider means the project's own body — what the other 100 tests assume |
| `Tests/PlayMode/EpisodePlayModeTests.Ceremonies.cs` | Drives a real episode and proves each card fires with the committed text |
| `Tests/PlayMode/EpisodePlayModeTests.Accessibility.cs` | Clipping, panel collision, review captures, cast legibility, camera framings |
| `Tests/PlayMode/HeadlessInputSettings.cs` | Lets input-driven tests run in batchmode; without it seven fail like product bugs |
| `Tests/EditMode/UiThemeContrastTests.cs` | WCAG AA arithmetic for the whole palette |
| `Uma/Tests/UmaCastPlayModeTests.cs` | The pipeline end to end: races, wardrobe, rig, proportions, no pop-in |
| `Uma/Tests/UmaCastCostPlayModeTests.cs` | Measures a full UMA house against the fallback |
| `Editor/LegacySaveFixtures.cs` | Generates an old-schema save so migration is testable without an archive |

## Modified

| File | Why |
| --- | --- |
| `Runtime/Presentation/CharacterPresentation.cs` | Consults the provider first; stand-in cover for deferred bodies; re-acquires a destroyed animator; caches which animator parameters exist |
| `Runtime/Episode/EpisodeDirector.cs` | Stages the sting and fires it from the committed, audience-filtered event — searching everything a commit appended, not just the last line |
| `Tests/PlayMode/EpisodePlayModeTests.cs` | Two stale assertions fixed: a legacy `InputField` the TMP migration missed, and a renderer count that assumed the primitive rig |
| `Tests/PlayMode/EpisodeDurableTransactionTests.cs` | Recovery now has to accept a committed decision, not merely restore input |
| `.gitignore` | Excludes `Assets/UMA` — roughly a gigabyte, re-importable from My Assets |

## New documents

| File | Why |
| --- | --- |
| `ACCEPTANCE_MATRIX.md` | U08's gate referenced a matrix that did not exist, which made it unfalsifiable |
| `PLAYTEST_PROTOCOL.md` | Turns section E's five judgements into thresholds with a way to observe each |
| `Assets/Plans/uma-character-pipeline.md` | The architecture and the traps, including two that cost hours |

## Before committing

**Check `ProjectSettings/ProjectSettings.asset`.** `GAMESIM_UMA` must not be in the committed
scripting defines — the bootstrap adds it locally on any machine with UMA, so that file goes dirty
routinely, and committing it breaks every clone without UMA beyond self-repair. The committed value
must stay `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY`.

`EpisodeHouse.unity` should also be on the authored prefabs unless you have decided to ship the UMA
cast.

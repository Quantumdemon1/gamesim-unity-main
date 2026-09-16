# UMA character pipeline

How houseguests get their bodies, and why the seams are where they are.

## Why a seam at all

UMA 3 is roughly a gigabyte and is not tracked in this repository — it is re-imported from
Package Manager > My Assets. So the project has to compile and run in two states: with UMA and
without it. Every design decision below follows from that.

```
Gamesim.Runtime          Gamesim.Uma                    UMA 3
  CharacterPresentation    GamesimUmaCast    ---->  DynamicCharacterAvatar
  CharacterBodySource <----  UmaBodyProvider         UMAAssetIndexer
  ICharacterBodyProvider     UmaCastLibrary          wardrobe recipes
                             UmaBodyTint
                             UmaStylizer
```

`Gamesim.Runtime` never references UMA. It exposes `ICharacterBodyProvider` and a registry,
`CharacterBodySource`. `Gamesim.Uma` depends on both and registers itself. The arrow only ever
points one way, so removing UMA removes a leaf.

### The three-state fallback

`CharacterPresentation.TryBuildModel` tries, in order:

1. a registered body provider (UMA),
2. an authored rigged prefab under `Resources/GamesimCharacters/<appearanceId>`,
3. the original articulated primitive rig.

Nothing about this is global. A scene gets UMA bodies only if it carries a `GamesimUmaCast`
component, which is what keeps the code-built play-mode scenes in the test suite on the primitive
rig they have always used.

### Compiling without UMA

`Gamesim.Uma` and `Gamesim.Uma.Editor` are constrained on the `GAMESIM_UMA` define.
`Gamesim.Editor/UmaPresenceDefine.cs` sets or clears that define based on whether `Assets/UMA`
exists on disk. With UMA absent the define is absent, Unity excludes both assemblies, and the
project compiles. That bootstrap lives in `Gamesim.Editor` precisely because that assembly never
references UMA — it has to compile in the state it is meant to detect.

Changing the define triggers a domain reload. That is expected; it is a one-time cost on the first
editor start after installing or removing UMA.

Verified both ways: a clone with no `Assets/UMA` compiles with zero errors and passes the full
673-test Edit Mode suite, and a project with UMA present has the define added by the bootstrap and
compiles `Gamesim.Uma` on the following reload.

> **Never commit `GAMESIM_UMA` in `ProjectSettings/ProjectSettings.asset`.**
>
> The bootstrap adds it locally on any machine that has UMA, which leaves that file dirty. If that
> hunk is committed, every clone without UMA is broken and **cannot repair itself**: the define makes
> Unity compile `Gamesim.Uma` against a `UMA_Core` that is not there, and the resulting compile errors
> stop `[InitializeOnLoad]` from running at all — so the bootstrap that would clear the define never
> gets to run. This was confirmed empirically, including that a second open does not help.
>
> The committed value must stay `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY`. Check that line before
> committing `ProjectSettings.asset`.

## Things that cost time to find out

**A race is addressed by its race name, not its asset file name.** `HumanFemale30.asset` has the
race name `Human Female 3.0`. Setting `activeRace.name = "HumanFemale30"` makes
`UMAAssetIndexer.GetRace` return null, after which `SetActiveRace` reports:

> `[SetActiveRace] could not find baseRaceRecipe for the race HumanFemale30 (None Set). Have you set one in the raceData?`

The message points at `baseRaceRecipe`, which is a red herring — `NO_RACE` is a constant string, not
the recipe's value, and the recipe is correctly assigned on every shipped `RaceData`. The race
simply did not resolve. Only the two human races use a display-style race name; the elf, orc,
sylvan and sprite races use their file names.

**UMA's shipped wardrobe is not Medieval-only.** `Assets/UMA/UMA3/Wearables/Wardrobe/` carries a
full modern casual set — hoodies, tees, tank tops, sport tops, shorts, sweatpants, tights, skirts,
trainers and boots, in male and female cuts — plus 22 hairstyles, 12 eyebrow sets and a beard and
moustache library under `HairAndBeards/`. The Medieval folder is a separate, smaller set. This is
enough content to stand up a Sims-style customisation screen without buying anything.

**The `UMADynamicCharacterAvatar` prefab ships with a Rigidbody and CapsuleCollider.** Instantiated
away from a floor it free-falls, which reads as "the character did not build". `UmaBodyProvider`
builds its own GameObject rather than using that prefab, so navigation stays authoritative.

**Generation is asynchronous.** The mesh and skeleton arrive several frames after creation, so
every body is handed over marked `Deferred` and `CharacterPresentation` resolves its head bone
lazily. A body measured on the first frame legitimately reports zero skinned mesh renderers.

## Matching the Kenney look

Two separate problems, and the first answer only solved one of them.

**Shading.** The house is flat-shaded and low-specular; UMA ships tuned for a photographic target.
`UmaStylizer` flattens the generated materials — smoothness to 0.08, metallic to zero, normal-map
scale to zero, occlusion reduced. UMA builds materials per character at runtime, so every value
written lands on an instance owned by one avatar and no project asset is touched.

**Proportions.** Flattening the materials was never going to be enough, and seeing the first UMA cast
in the house made that obvious: against chunky low-poly furniture a realistic figure reads as a small
thin smudge. Stylised characters are built the other way — larger head, shorter body, thicker limbs
and extremities. `UmaCastLibrary.HouseProportions` is that, as DNA, with per-persona overrides in
`UmaCastLook.Dna` so the six differ from each other. Measured effect: 2.05 m down to 1.72 m.

> **`predefinedDNA` does not work for these races.** It is the obvious API and it silently does
> nothing: UMA's `ApplyPredefinedDNA` returns immediately for any race with `useNewDNA` set, which
> both human races have. Preloaded values are discarded without a warning. `UmaBodyTint` instead sets
> the DNA on the built character and marks it dirty, which costs one extra rebuild — guarded, because
> that rebuild raises `CharacterUpdated` again.

Skin tones in `UmaCastLibrary` are the primitive rig's tones lifted into Kenney's brighter,
lower-saturation range.

`EveryDnaNameUsedByTheCastLibraryExists` checks every DNA name against the race, because a misspelling
is ignored rather than reported — the same failure mode as the race name, where a wrong string cost
hours.

## Known gaps

- **No seated pose.** UMA's `Locomotion` controller declares `Speed` but no `Seated`, so seated
  houseguests keep their standing idle. `CharacterPresentation.RefreshAnimatorParameters` caches
  which cues a controller actually declares, so this degrades quietly instead of warning every
  frame. Closing it needs a Gamesim-owned controller with a sitting clip.
- **Fabric tint is best-effort.** Recipes vary in whether they expose a shared colour for their main
  fabric, so `UmaBodyTint` tints whichever of a candidate name list the assembled recipe declares
  and leaves recipes with baked-in colour alone. Run `Gamesim > UMA > Report Cast Build` against a
  staged preview to see what each look actually exposes, then narrow the list.
- **Cost per character.** Each avatar is ~16k verts, 229 bones and eight submeshes backed by
  runtime-generated atlases. That has not been profiled against a full house yet.
- **Blender-authored content** is still the plan for clothing that is specific to this show.

## Tools

- `Gamesim > UMA > Stage Cast Preview` — builds every look in `UmaCastLibrary` in a row. Play mode
  only, because UMA needs frames to assemble.
- `Gamesim > UMA > Report Cast Build` — reports mesh, bones, height, shaders, wardrobe slots and
  shared colours for each staged character.
- `Gamesim > UMA > Clear Cast Preview`.

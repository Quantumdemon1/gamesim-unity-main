# Gamesim — Unity port

A Unity port of a *Big Brother*-style social simulation: one house, six contestants including the
player, and a full episode from the first competition through nomination, veto, campaigning, the
vote, an eviction and a recap.

Requires **Unity 6000.6.0f1** (URP).

---

## Read this before opening the project

**Import UMA before you open `EpisodeHouse.unity`.**

The cast is built at runtime from [UMA](https://assetstore.unity.com/packages/3d/characters/uma-2-unity-multipurpose-avatar-35611),
which is roughly a gigabyte and is deliberately **not** in this repository — `Assets/UMA/` is
gitignored. Import it from *My Assets* in the Package Manager first.

If you open the scene without it, nothing catastrophic happens: you get one missing script warning
on the `Gamesim UMA Cast` object and the houseguests fall back to the authored prefabs in
`Assets/Gamesim/Resources/GamesimCharacters/`. The game is playable. It just is not the cast the
screenshots show.

Two related things worth knowing before you change anything:

- `GAMESIM_UMA` is **not** in the committed scripting defines, and must not be added there. An
  editor script sets it locally on any machine where `Assets/UMA` exists, and clears it where it
  does not. Committing it breaks every clone without UMA *beyond self-repair*, because the compile
  errors stop the very script that would clear it.
- UMA-bodied builds are around 912 MB against 144 MB for the authored cast. That is a distribution
  decision, not an accident.

To switch casts, use **Gamesim ▸ UMA ▸ Use UMA bodies in the episode** or **… ▸ Use the authored
prefabs in the episode**. It is a scene edit, reversible from the same menu, and leaves no trace
when off.

---

## Running it

Open `Assets/Gamesim/Scenes/EpisodeHouse.unity` and press play. That is the whole game.

| Scene | What it is |
| --- | --- |
| `EpisodeHouse.unity` | The episode. This is the one to play. |
| `HousePrototype.unity` | The U02 greybox. Capsule placeholders on purpose — the NPC motion suite loads it. Not a scene to play. |
| `Bootstrap.unity` | Entry point for a built player. |

**Gamesim ▸ Port ▸ Start Isolated Preview** runs the episode against a throwaway save slot, which
is what you want for a playtest — it leaves the normal slot untouched.

---

## Tests

Three suites. Expect **678 / 113 / 8**, all green.

```bash
UNITY="C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe"

"$UNITY" -batchmode -accept-apiupdate -projectPath . -runTests \
  -testPlatform EditMode -assemblyNames Gamesim.EditModeTests \
  -testResults editmode.xml -logFile editmode.log

"$UNITY" -batchmode -accept-apiupdate -projectPath . -runTests \
  -testPlatform PlayMode -assemblyNames Gamesim.PlayModeTests \
  -testResults playmode.xml -logFile playmode.log

"$UNITY" -batchmode -accept-apiupdate -projectPath . -runTests \
  -testPlatform PlayMode -assemblyNames Gamesim.Uma.PlayModeTests \
  -testResults uma.xml -logFile uma.log
```

Two traps that cost real time if you meet them cold:

- **`-assemblyNames` honours only its first argument.** Passing two silently runs the first and
  reports the second as zero tests, which reads as "those tests do not exist" rather than "you
  asked wrongly". Run the UMA suite as its own invocation, as above.
- **Always pass `-assemblyNames`.** Unfiltered, an Edit Mode run picks up UMA's own bundled tests —
  1,243 cases, 37 of which fail inside `UMA.Editors` and `UMA.Tests`. Third-party noise that makes a
  green suite look red.

Screen capture is useless in batchmode: it renders without presenting, so `ScreenCapture` writes
solid black. The suites that produce review frames point a camera with a `targetTexture` at the
content instead. That path skips URP post-processing, so bloom is missing from those captures.

---

## Layout

```
Assets/Gamesim/
  Simulation/     Pure C#. No UnityEngine types, no scene access, no frame-rate dependency.
  Runtime/        Presentation, HUD, house, episode director, persistence.
  Editor/         Scene construction and art passes (see the Gamesim menu).
  Uma/            UMA integration, entirely behind the GAMESIM_UMA define.
  Tests/          EditMode and PlayMode suites.
  Art/External/   Kenney Furniture Kit and Quaternius characters, both CC0.
```

The boundary that matters: **player interaction → validated command → simulation → committed result
→ UI, animation, audio, save.** Simulation rules never depend on scene objects or frame rate.
Cameras and cutscenes present decisions; they never make them. NPC suggestions pass through the same
validation as the player's. Character IDs identify contestants; scene objects are replaceable
visual representations of them.

Presentation reads committed state, never a projection — a projected eviction is not a fact yet, and
the set must never show an outcome the save does not hold.

---

## Editor tooling

The `Gamesim` menu holds the scene-construction and art passes. They are re-runnable and derive
their placement from the scene rather than from constants, so they survive the house being resized.

Two notes:

- **Gamesim ▸ U07 ▸ Build the memory wall** is idempotent in content but not in identity: it
  destroys and rebuilds, so every object gets a fresh fileID and the scene diff is a couple of
  thousand lines of identical geometry. Run it when the wall needs to change, not as a habit.
- **Gamesim ▸ U07 ▸ Audit characters in the open scene** lists every character and says which body
  each is wearing. Useful when something looks like a placeholder and you want to know rather than
  guess.

---

## Documents

| File | What it covers |
| --- | --- |
| [`ACCEPTANCE_MATRIX.md`](ACCEPTANCE_MATRIX.md) | What "done" means, criterion by criterion, and what currently passes. |
| [`PLAYTEST_PROTOCOL.md`](PLAYTEST_PROTOCOL.md) | How to run section E, the part no automated suite can decide. |
| [`UNITY_PORT_ROADMAP.md`](UNITY_PORT_ROADMAP.md) | The U01–U08 work packages. |
| [`UNITY_PORT_IMPLEMENTATION.md`](UNITY_PORT_IMPLEMENTATION.md) | Implementation record. |
| [`Assets/Plans/MASTER-PLAN.md`](Assets/Plans/MASTER-PLAN.md) | The one development plan: verified status of every area, the constraints, the workstreams, and the Blender asset pipeline. It replaced the eight per-area plans. |

**The acceptance matrix has one open section.** E1–E5 are human playtests — pacing, whether
first-time players reach the eviction unaided, whether a decision felt consequential, whether losing
reads as an ending, and whether the house and cast read as one production. They need people, and
they are recorded as not run rather than quietly assumed.

Two things will skew them if you do not decide first, both spelled out in the protocol: **the
first-run tour changes what E2 measures** — it is exactly the intervention that criterion tests the
absence of — and **the pinned build predates the presentation work**, so re-pin before running
anything.

---

## Third-party assets

| Asset | Licence |
| --- | --- |
| [Kenney Furniture Kit 2.0](https://kenney.nl) | CC0 — [licence](Assets/Gamesim/Art/External/KenneyFurniture/Kenney-License.txt) |
| [Quaternius characters](https://quaternius.com) | CC0 — [licence](Assets/Gamesim/Art/External/QuaterniusBaseCharacters/Quaternius-CC0-License.txt) |
| UMA 2 | Unity Asset Store — **not included**, import it yourself |

The Quaternius licence file sits in `QuaterniusBaseCharacters/`, which is a second import that
nothing references — the cast prefabs use `QuaterniusCharacters/`. Both are the same CC0 licence.
The unused folder is 42 MB and safe to delete; it is kept only because the licence text lives in it.

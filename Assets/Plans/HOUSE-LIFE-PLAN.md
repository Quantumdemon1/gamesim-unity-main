# House life: the backwards walk, travel icons, and things to do in the house

Plan, 24 September 2026. **Nothing here is implemented yet.**

**The request:**
- fix the backwards walk;
- use the Quaternius Universal Animation Library for fixes and new uses, such as swimming when the pool is clicked;
- add clickable icons that take the player to a room, a challenge, the episode screen or the diary room;
- add more interaction with the house, such as sleeping in a bed and cooking in the kitchen;
- brainstorm beyond those, then write a detailed plan.

**How this was built:**
- Four readers mapped the code: navigation and HUD, house content, the sim loop against the web game, and the animation pipeline.
- Four brainstormers proposed 57 ideas, and a judge merged and tiered them.
- Three checkers then fact-checked this plan against the code; 50 findings are folded in.
- The facing bug was measured on live bodies, twice. The first probe was flawed (see Appendix A).

Every claim cites the code it comes from.

---

## At a glance

- **The backwards walk is one case of a wider bug.** Every custom animation take plays turned about 180° on both casts:
  - **UMA cast:** Walk, the stop step, standing talk, listen, argue and all five ceremony reactions. Only the idle and run face forward, and those are UMA's own clips.
  - **Non-UMA cast:** the same set, apart from its Quaternius walk.
  - A hard-coded 180° turn in the seat code has been hiding it on sitting bodies.
  - The walk is simply the version everyone sees: NPCs only ever walk.
  - The fix (Milestone 0):
    - one import setting for the Mixamo takes;
    - a fix to the authored reaction clips;
    - a re-export of the non-UMA takes from Blender;
    - then deleting the hack.
- **The Quaternius library is already in the repo.** An older Standard copy (46 takes, CC0) sits unused at `Art/External/QuaterniusCharacters/AnimationLibrary_Unity_Standard.fbx`.
  - It has `Swim_Idle_Loop` and `Swim_Fwd_Loop`, sit enter/idle/talk/exit, idle, walk, jog, sprint, dance, crouch, push, jump and interact.
  - It has nothing for lying down, cooking, eating or working out.
  - The v3.0 you linked (June 2026) adds more, but its clip names are not published. The free Standard download would show them.
- **Clickable travel.**
  - Room pins float in the house.
  - The overview (M) becomes a clickable map.
  - One "next stop" beacon marks the episode screen, the competition arch or the diary chair. It copies the web game's floating ENTER COMPETITION / MAKE YOUR DECISION crystal.
  - One travel system walks or runs you there. A second press cuts straight there, if you want that.
- **Things to do in the house:**
  - sleep in a bed;
  - cook at the stove;
  - soak in the hot tub (web parity);
  - tread water or swim slow lengths in the pool. The web has NPCs swim at the pool but no click for the player. The pool also holds only 25 cm of water (§4 M6).
  - The house keeps running while you do any of these.
- **Beyond the request (Next tier):**
  - houseguests join you, or come to find you;
  - NPCs use the new verbs between conversations;
  - a real sit-down and stand-up, props in hand, sound and VFX;
  - grabbing something from the fridge, watching TV;
  - then, if you want activities to count, bounded one-week effects and the web's activity moments ("Kitchen Encounter", "Hot Tub Intel", "A Knock at the Door").
- **Your decisions** are in §6:
  - walk there or cut there;
  - whether activities may change the seeded season;
  - the pool's depth;
  - downloading or buying the library;
  - what the committed non-UMA cast gets.

---

## 1. The backwards walk, and every other take that faces away

### What was measured

Two throwaway PlayMode probes ran in the D: harness:
- one on a UMA body built by `UmaBodyProvider`;
- one on the shipped non-UMA body `GamesimCharacters/dan-gheesling`.

For each state, the probe:
- set the parameters that hold that state (Speed, Running, Seated, Talking, Listening, Arguing);
- played it and waited for it to settle;
- confirmed with `GetCurrentAnimatorStateInfo` that the state was actually playing (30 of 30 sampled frames, every row);
- averaged which way the body faces relative to the character root, from the hips (thigh to thigh) and the shoulders.

| State | UMA cast: clip | UMA: body yaw | Non-UMA cast: clip | Non-UMA: body yaw |
|---|---|---|---|---|
| Idle | UMA's own | **0°** | Quaternius `Casual_Male` | **0°** |
| Walk | Mixamo `bb_anim_Walk_loop` | **+175°** | Quaternius `Casual_Male` | **−2°** |
| Run | UMA's own | **−4°** | (none: runs play the walk) | n/a |
| WalkStop | Mixamo | **−178°** | (none) | n/a |
| SitIdle / SitTalk | Mixamo | **−179° / +177°** | Blender `bb_anim_casual` | **180° / 180°** |
| Talk / TalkB / TalkC | Mixamo | **−165° / −179° / −179°** | Blender `bb_anim_casual` | **180°** |
| Listen | authored `.anim` | **−165°** | Blender `bb_anim_casual` | **180°** |
| Argue | Mixamo | **−168°** | Blender `bb_anim_casual` | **180°** |
| ReactWon / ReactCheered | Mixamo | **−178° / −177°** | Blender `bb_anim_casual` | **180°** |
| ReactNominated / Saved / Evicted | authored `.anim` | **−166°** | Blender `bb_anim_casual` | **180°** |

**The walk itself.**
- During the walk, the planted feet slide **+0.87 and +1.29 m/s along the root's forward**. That is backwards for a body the agent carries forward. In the run they slide −4.8 and −5.4 m/s, which is correct.
- The hips stay within 3 cm of the root through the walk cycle. So the 1.84 m of stride that the take bakes into its pose does *not* slide and snap: Loop Pose cancels it. The travel does not need unbaking.

**Who sees it:**
- **Everyone walking.** Every NPC walks backwards, because NPCs only walk (`HouseNpcMotion.cs:93` walks at 2.2 m/s and never calls `SetRunning`).
- **The player walking.** The player walks backwards on short single clicks; trips over 8 m and double clicks run (`HousePlayerController.cs:44-51, 306-314`).
- **The travel buttons.** "Go to episode screen" and "Go to diary room" never set the gait at all (`TryMoveTo`, `:157-158`). They cross the house at whatever the last click left, usually the walk.
- **Every standing conversation.** Both the talker and the listener face away from each other, on both casts. So does every argument.
- **Every ceremony reaction** (nominated, saved, evicted, won, cheered) turns its back.
- **Every walk that stops.** The settle step (WalkStop) faces backwards, then snaps round to the forward idle.
- **Not sitters.** They look right only because `HouseSeatPresentation` adds `SeatedClipHalfTurn = 180` (`HouseSeatPresentation.cs:23, 117-118`, commit 4374d96).

### Why

**The importer.** `AuthoredAssetImporter.OnPreprocessAnimation` (`Assets/Gamesim/Editor/AuthoredAssetImporter.cs:226-251`) imports every take under `Art/Authored/Animation/` with:
- `lockRootRotation = true`, which bakes rotation into the pose;
- `keepOriginalOrientation = true`, which means "Based Upon: **Original**".

"Original" keeps the heading stored in the file, and the Mixamo files store one about 180° from Unity's forward. A Blender check confirms the takes themselves are sound: `bb_anim_Walk_loop.fbx` is a genuine forward walk. Its planted feet slide backwards under the hips at 0.055 m per frame, its hips travel 1.84 m forward per cycle, and it faces the same way as `bb_anim_Talk_loop.fbx`. The import is what turns them round.

**The authored reactions.** `HumanoidReactionAuthoring.cs:21-24, 53-76` copies `Talk_loop`'s frame-0 root rotation (about −164° of yaw) and keeps it, again with "Original". That includes `Listen_loop`.

**The non-UMA takes.** These are built headless in Blender (`ArtSource/animation/bb_anim_casual.py` through `ArtSource/tools/bb_anim.py:103-140`). The exported root bone of all ten takes is turned. Its rotation is `(-0.0019, -0.7071, 0.7071, 0.0019)` at `bb_anim_casual.fbx.meta:388-391`, where the shipped prefab's is −90°X (`dan-gheesling.prefab:1073`). That composes to a 180° yaw. Only the Quaternius `Casual_Male` idle, walk, sit-down and stand-up face forward.

### The fix (Milestone 0)

Land it as **one commit**: the three turned sources get fixed, *then* the seat hack comes out. Removing the hack before its sources are fixed turns every sitter round on both casts.

1. **Mixamo takes (UMA).** In `AuthoredAssetImporter.OnPreprocessAnimation`, for the Humanoid folder:
   - set `keepOriginalOrientation = false` ("Based Upon: Body Orientation") and keep `lockRootRotation`;
   - leave the XZ and Y settings alone.

   Add a per-take trim table for takes whose body is not upright: `Sleep_loop`, `SleepLying_loop`, and later the swim takes. Body Orientation is meaningless when the torso is horizontal, so those keep "Original" with a measured `rotationOffset`.
2. **Reimport, don't version-bump.** Force-reimport `Art/Authored/Animation/Humanoid/*.fbx` only, and commit the rewritten `.fbx.meta` `clipAnimations` blocks. Bumping `GetVersion()` (`:29`) reimports every model this postprocessor handles, UMA included.
3. **Authored reactions (UMA).** In `HumanoidReactionAuthoring`:
   - strip the heading from the copied RootQ, keeping the tilt;
   - zero RootT.x and RootT.z;
   - set `keepOriginalOrientation = false`.

   Re-run *Gamesim ▸ Characters ▸ Author missing humanoid reactions*. It rewrites all four `.anim` files, `Listen_loop` included, and re-wires the controller.
4. **Blender takes (non-UMA).** Fix the root orientation once, in the export of `bb_anim.py` / the base samples of `bb_anim_casual.py`, for all ten takes. Re-export `bb_anim_casual.fbx`.
5. **The seat hack.** Only now delete `SeatedClipHalfTurn`, using `Quaternion.Euler(0, anchor.Facing, 0) * baseLocalRotation`.
6. **Travel gait.** Add `HousePlayerController.TryTravelTo(pos)`: it calls `TryMoveTo(pos)`, keeping its `InputEnabled` gate, and on success `ApplyGait(RouteMetres > RunRouteMetres)`.
   - Use it in `GoToStation` (`EpisodeDirector.cs:524-546`) and `GoToDiary` (`EpisodeDirector.DiaryRoom.cs:74-86`).
   - `GoToDiary` follows the player with the camera, as `GoToStation` does (`:539`).
   - Captions and status text stay unchanged.

The study-budget guard is a separate engine change, so it lands on its own as **M0b** (§4). M0 stays presentation-only and can ship alone.

**Tests:**
- **Regression probes.** Rebuild the two probes as real PlayMode tests: the UMA one in `Gamesim.Uma.PlayModeTests`, the non-UMA one in `Gamesim.PlayModeTests`. They hold each state's parameters, assert `IsName(state)` before sampling, and assert:
  - body yaw within 25° of the root for every upright state;
  - planted feet sliding backwards relative to the body in Walk and Run.
- **EditMode facing test.** A `PlayableGraph` per take, on a humanoid model already in the repo (the library's Mannequin, or a take's own avatar). It uses a **per-take expectation table**:
  - upright takes: body yaw within 25° at 0, ¼, ½ and ¾;
  - takes that turn on purpose (e.g. `WalkTurn180`, if wired later): their start facing and total turn;
  - trim-table takes (lying, swimming): the head-to-hips axis points along the expected direction.
- **EditMode walk direction** from the pose. `averageSpeed` is zero for every take with XZ baked, so it cannot tell. Sample `Walk_loop` at successive times and require the planted foot to move backwards along the body's forward. Pin `averageSpeed == 0` separately, as a statement that XZ stays baked.
- **EditMode import pin** for the settings, in `HumanoidAnimationTests.cs:40-65`.
- **The seats.** The diary facing test (`EpisodeDiaryRoomPlayModeTests.Facing.cs`) keeps passing without the hack. Add a non-humanoid seated-facing check beside it: its pose check runs only when `animator.isHuman` today.
- **Mutation-test all of these.** At minimum: restore "Original", restore the half-turn, remove a trim-table entry, and revert the Blender root fix.

**Accepted when:**
- captures show every state facing forward on both casts: standing conversation, argument, reactions, the stop step, sitting at the table and in the diary chair;
- the diary shot still frames the face;
- the full suites are green.

Verify by render, not by numbers. The half-turn itself was added after transform readouts looked right while the rendered body faced away. Picture bodies through a BakeMesh proxy, because edit-time skinned renders are stale.

**No sim, RNG or save impact.**

### Other animation defects found on the way

| Defect | Evidence | Where it is fixed |
|---|---|---|
| Feet slide. The walk take covers about 1.7 m/s against agents at 2.2 m/s; the run covers about 5.4 m/s against 4. NPCs skate all day. | `HouseNpcMotion.cs:93`; `HousePlayerController.cs:44-51`; probe | **M1**, a small `Pace` multiplier (agent speed ÷ clip speed) |
| The non-UMA controller has no Run state, so a running body plays the walk at about 2.3× speed. | `GamesimCharacter.controller` | **M1**, a walk-speed multiplier; a Run state later |
| A docstring says NPCs run on long trips; they never do. | `HumanoidClipWiring.cs:48-55` | M0 record fix |
| Idle and Run are wired to the two Mixamo **sleep** takes as override keys for UMA's own clips. If the override check fails, every UMA body falls back to plain Locomotion and loses sit, talk and react. The keys also block a real Sleep state. | `HumanoidClipWiring.cs:39-40, 68-70`; `UmaBodyProvider.cs:247-294` | M1 |
| `Clap_loop`, `React_shrug` and `WalkTurn180` are imported and wired to nothing. | `HumanoidClipWiring.cs:58-84` | M8 / Later |
| No sit-down or stand-up take on the UMA rig (a 0.35 s cross-fade). | `HumanoidClipWiring.cs:106` | M8 |

---

## 2. The Quaternius Universal Animation Library

**What the link offers.** [quaternius.itch.io/universal-animation-library](https://quaternius.itch.io/universal-animation-library):
- **Licence and version:** CC0, v3.0, released 16 June 2026.
- **Tiers:** Standard has 45 animations and is free (pay what you want). Pro has 120+ for $9.99 or more. Source adds the `.blend` rig and animations for $14.99 or more.
- **Formats:** FBX and GLB, each with full root motion and without.
- **Rig:** "a universal humanoid rig … ready for retargeting", compatible with Mixamo and Unity Humanoid.
- **Contents:** 8-direction locomotion, jog, sprint, push, crawl, swim, sit, death, combat, guns and emotes.
- **A second pack,** [UAL 2](https://quaternius.com/packs/universalanimationlibrary2.html), has 130+ takes: melee, parkour, farming, fishing and zombie locomotion.
- **No page lists clip names;** only a download does.

**What is already committed.** `Assets/Gamesim/Art/External/QuaterniusCharacters/AnimationLibrary_Unity_Standard.fbx`:
- file date 25 March 2025, CC0 per `CC0-SOURCES.txt`;
- imported as Humanoid with an auto-mapped avatar;
- no clip settings, so nothing loops;
- **used by nothing.**

Its 46 takes:
- A_TPose
- Crouch_Fwd_Loop, Crouch_Idle_Loop
- Dance_Loop
- Death01
- Driving_Loop
- Fixing_Kneeling
- Hit_Chest, Hit_Head
- Idle_Loop, Idle_Talking_Loop, Idle_Torch_Loop
- Interact
- Jog_Fwd_Loop
- Jump_Start, Jump_Loop, Jump_Land
- PickUp_Table
- six Pistol_* takes
- Punch_Enter, Punch_Jab, Punch_Cross
- Push_Loop
- Roll, Roll_RM
- Sitting_Enter, Sitting_Exit, Sitting_Idle_Loop, Sitting_Talking_Loop
- four Spell_Simple_* takes
- Sprint_Loop
- **Swim_Fwd_Loop, Swim_Idle_Loop**
- Sword_Attack, Sword_Attack_RM, Sword_Idle
- Walk_Formal_Loop, Walk_Loop

**How the house would use them:**

| Need | Take |
|---|---|
| Treading water, pool lengths | Swim_Idle_Loop, Swim_Fwd_Loop |
| A real sit-down and stand-up; seated chat variety | Sitting_Enter / Exit / Idle_Loop / Talking_Loop |
| One consistent, foot-matched locomotion set; a formal walk on ceremony nights | Idle_Loop, Walk_Loop, Jog_Fwd_Loop, Sprint_Loop, Walk_Formal_Loop |
| Into the pool, over the hot-tub rim | Jump_Start / Loop / Land |
| Starting to cook, picking up a plate, the fridge | Interact, PickUp_Table |
| Dancing after a HoH win, emotes | Dance_Loop |
| Talking while standing | Idle_Talking_Loop |
| Sneaking to listen in | Crouch_Idle_Loop, Crouch_Fwd_Loop |

**What it lacks.**
- **Lying and sleeping.** The two Mixamo sleep takes cover this once M1 frees them.
- **Stirring and eating.** No take. These become IK-driven gestures (M8).
- **Working out, waving, turning in place, strafing.** No take. They come from Mixamo or a paid tier; v3.0's "8 directions" may add the strafes.

**How clips get in.** The library cannot go into `Art/Authored/Animation/Humanoid` as one file. The importer renames every clip there to its file's name (`AuthoredAssetImporter.cs:239`), and it **loops any clip whose name ends in `_loop`** (`:240-242`).

M1 adds a headless Blender script, `ArtSource/animation/bb_anim_ual_split.py`. It exports each chosen take to its own file under a `ual_` namespace, so nothing collides with the Mixamo files:
- **Looping takes:** `bb_anim_ual_<Take>_loop.fbx`, with the library's own `_Loop` stripped. For example, `bb_anim_ual_Swim_Idle_loop.fbx`.
- **One-shots:** `bb_anim_ual_<Take>.fbx`, with no suffix. For example, `bb_anim_ual_Sitting_Enter.fbx`.
- The `ual_` prefix matters. Without it, `Walk_Loop` would become `bb_anim_Walk_loop.fbx` and overwrite the Mixamo walk.

Each new file gets a hand-written two-line `.meta`. EditMode tests assert:
- `loopTime` matches the suffix for every take;
- no two files map to one clip name;
- the take and state counts grow in step.

Every take passes the M0 facing test before it is wired.

**The non-UMA cast problem.** The committed default cast (Quaternius "Ultimate Modular"-style bodies) has a **Generic** rig, and **its skeleton is not a humanoid chain**:
- `UpperLeg.L` hangs off `Body`, a sibling of `Hips`.
- `Foot.L` and `Foot.R` are IK bones parented to the root `Bone`, beside `PoleTarget.L/R`, not under `LowerLeg` (`dan-gheesling.prefab`, `bb_anim_casual.fbx.meta:382-512`).
- Unity's humanoid avatar needs the upper legs under the hips and the feet under the lower legs.

Only UMA bodies are Humanoid, and `GAMESIM_UMA` can never be committed. So every new animation is invisible on a fresh clone unless one of these happens:
1. **An editor bake through a proxy skeleton.** Build a re-parented humanoid proxy for `CharacterArmature`: Body as Hips, and a proxy foot under each LowerLeg. Play each humanoid take through a `PlayableGraph`, write the proxy feet back onto the `Foot.L/R` IK bones, and record generic `.anim` files with `GameObjectRecorder`. The humanoid clips stay the single source. Moderate cost.
2. **Re-rig the Quaternius bodies as Humanoid.** A re-rig plus a large test rewrite, not just an import setting.
3. **Switch the fallback cast to Quaternius's Universal Base Characters**, already in your Downloads, which are built on the library's own rig. An art change: there are no casual outfits, and the faces need work.

For the MVP, non-humanoid bodies get the **web game's own procedural poses**: sleep rotates the body flat and lifts it, swim lowers it upright (web `ActivityPoses.ts:68-83, 127-139`). That keeps the default cast correct, if plainer. Weigh options 1–3 against each other before choosing (§6).

**Recommendation:** with your OK, download the free v3.0 Standard (about 14.5 MB, CC0) and compare its names with the committed copy. Decide on Pro after seeing what Standard lacks.

---

## 3. The brainstorm, tiered

Codes used in the tables:
- **Web:** **P** = the web game has it (parity). **P\*** = the web has the rule or an NPC version, but not a player route. **N** = new invention (the port's own design; record it in MASTER-PLAN).
- **Cost:** S / M / L / XL.
- **Sim:** "—" = presentation only (no command, no season RNG, no save change).

### Tier 0: fixes that come first

| Item | Why | Web | Cost | Sim |
|---|---|---|---|---|
| Every take faces forward (import rule, reactions, Blender re-export, then remove the seat hack) | §1 | — | M | — |
| Travel buttons choose their own gait; the diary trip follows the camera | They use `TryMoveTo`, which never sets the gait | — | S | — |
| Furniture tops the NavMesh treats as floor: pool deck 0.45, dining table 0.78, grey-box beds 0.78, cots 0.53 (decoded from the bake; agent climb is 0.75). Confirm in the editor, then mark those collision proxies and the pool's authored `_col` not walkable, in the same rebake as the M4 anchor pass. | Sleep, swim and table anchors need honest approach points | — | M | — |
| Text chrome that swallows world clicks: Status, the Interaction prompt and chips set to `raycastTarget = false` | World icons are useless if the HUD eats the click (`EpisodeHud.cs:1664-1669`) | — | S | — |
| **M0b:** the engine enforces the real study budget. `StudyHouse` checks the legacy `< 18`, so only the UI enforces the real budget (`EpisodePreparation.cs:14`). Rewrite `EpisodePreparationTests.cs:71-80`, which studies at 17 and expects acceptance. A recorded fixture that studies past the real budget would now be *rejected*, and its replay diverges from that command on, so audit the fixtures first. | Any second study route or activity command inherits the hole | — | S | validation |
| Record ledger (list below) | Stale records mislead the next change | — | S | — |

The record ledger:
- **`HouseMap.RoomIcon`.** Delete it and its test `HouseMapTests.RoomIcon_…` together. Note the justified EditMode floor change.
- **WORLD-INTERACTION.md.** Correct its stale line numbers, and its claim that Animation Rigging cannot drive a Generic rig.
- **The "NPCs run" docstring.** Correct it.
- **Nine living-room TV practical lights.** Either restore a TV for "Watch TV" (Tier 2) or remove the lights: a decision in §6.
- **The doll-scale Furnishings beds.** Keep them out of the sleep anchors.
- **The narration room list** (`HouseRooms.All` names a bathroom, a storage room and a hallway that do not exist, and omits the hot tub). Editing it changes the narration text that a given seeded roll picks, so it **waits for the schema-14 bundle**.

### Tier 1: the MVP, what you asked for

| Item | Pitch | Web | Cost | Sim |
|---|---|---|---|---|
| Travel layer | One trip model for rooms, the station, the diary, houseguests and props. `GoToRoom` lands on a clear spot (the Nomination marker stands inside its table). Long trips run. | N (rooms) | M | — |
| World pick layer and hover language | Everything clickable says what it does on hover; a failed click says why. | P\* (the web's hover ring and tooltip) | M | — |
| The overview becomes the house map | M lifts the house into its labelled blueprint. Each room chip and each column row is a button with the faces inside. Click, and you watch yourself cross the house. The map stays up until you arrive. | N (the web moves only the camera) | M | — |
| Room beacons | Glass pins over all eight rooms (sofa, kitchen, bed, diary, crown, gamepad, trophy for the yard, gavel for nomination), each with a head count and a "talking" glyph. Click to go. Shown when zoomed out. | N | L | — |
| Next-stop beacon; pressable objective chip and E prompt | One brighter icon where the story needs you. **Idle, the everyday case:** the episode screen, always clickable. ENTER COMPETITION at the yard arch in competition phases. MAKE YOUR DECISION when it is your call. The diary chair when a reflection is pending. Click from anywhere to go; click again on arrival to open. | **P** (web `DiaryRoomChallenge.tsx`; the idle mode is extended to be clickable) | M | — |
| Quick travel: press again to cut | The default stays walk or run. A second press after the double-click window, while more than 3 m out, or a Settings switch, cuts straight there behind a 0.3 s broadcast dip. Free roam only; arrival still gates entry. | N | M | — |
| Activities stop freezing the house | Today the activity *panel* pauses every NPC (`EpisodeDirector.cs:89-90`, `NpcSocial.cs:76-78`). The menu closes once the activity starts, a "Finish activity" chip remains, and the world stays clickable. | P (web activities never pause the house) | M | — |
| Animation intake, stand-in fix, `Pace` | The UAL split script; the Idle and Run override keys moved off the sleep takes; foot-matched pace; the non-UMA walk-speed multiplier. | — | M | — |
| Activity cues and one body-pose owner | `HouseSeatPresentation` grows Seat, Lie and Float modes. Only the visual body moves; the navigation root never does. One Bool cue per activity. Procedural fallback for non-humanoid bodies. | — | M | — |
| **Sleep in a bed** | The single beds, the lower bunks, the cots, and the HoH bed while you are HoH. The houseguest lies down and the bedside light dims. It lasts until you move. There is no clock, so sleeping does not skip time. | **P** (web sleep points and pose) | L | — |
| **Cook in the kitchen** | "Cook a meal" at the hob (x ≈ 5.25 on `bb_set_kitchenrun`). An Interact beat, then a stirring gesture; the pan and steam come in M8. "Prepare a snack" stays exactly as it is. | **P** (web Kitchen Island: Cook Meal / Grab Snack) | L | — |
| **Swim: hot tub and pool** | Soak in the hot tub: two seats, chest at the waterline. Click the pool and you take a swim, treading water or doing slow lengths in the basin. "Sit on the pool edge" is the second choice. | **P** hot tub; **P\*** pool (the web has NPCs swim at the diving board, and pool splash VFX, but no player click) | L | — |

### Tier 2: next

| Item | Pitch | Web | Cost | Sim |
|---|---|---|---|---|
| Company at the furniture | Your ally takes the stove beside you, or slides into the hot tub. You can join an NPC at what they are doing ("Help out", "Sit with them", as a second route to existing social actions). "Invite them to eat" uses the two table seats. | P\* | M | — (joining spends an action through existing commands) |
| Houseguests come to you | An NPC with a pending deal, an oath or a storyline beat crosses the house, says "Got a minute?", and the E · Talk prompt appears. One approach per NPC per phase. | **P** (web `useNPCSocialEngine`) | M | — |
| Go where the drama is | Clicking the live feed takes you to a spot where you can overhear, and Listen in lights up. "Watch" moves only the camera. | N | M | — |
| NPCs use the new verbs between conversations | Cooling NPCs cook, nap, soak and swim instead of standing still. A separate ambient list widens one venue at a time. | **P** (web autonomy, ported as data) | M | — |
| One humanoid path for the non-UMA cast | The proxy bake, a re-rig, or the Universal Base Characters (§2). | — | L | — |
| Real sit-down and stand-up, more perches | Sitting_Enter and Sitting_Exit. New perches: the set-piece sofa, the Games-room bar stools, bed edges. | P (sit) | M | — |
| Held props and a light IK layer | Pan, plate, mug, towel, remote. A hand follower that survives UMA rebuilds. Mecanim IK only on bodies performing an activity. | **P** (web carry items; data already ported) | M | — |
| Grab something from the fridge | Interact and PickUp_Table at a fridge anchor; a drink in hand. | **P** (web `fridge-eat`) | S | — |
| Watch TV | A sofa perch facing a TV. Needs a TV placed in view (the living-room TV no longer renders). The HoH and Games consoles are candidates. | **P** (web `tv-watch`) | M | — |
| Activity close-ups; the live feed shows the house living | A soft low shot when you settle into an activity. The live feed cuts to someone swimming or cooking when no conversation is on. | P\* | S | — |
| Activity sound and small VFX | Splash, sizzle, bubbles, snoring and "z z z", steam, footstep dust. 3D one-shots from a small pool. **Built, VFX only:** the web's bubbles, steam and splash (`HouseActivityEffects`). Sound, snoring and dust are not built. | **P** (VFX) | M | — |
| Rooms and people as destinations wherever they are listed | Go, Watch and Find on the notebook's room cards and the cast strip. | N | S | — |
| Study at the memory wall | A second route to "Study the House". No new command. | **P** | M | — |
| Pair-specific Listen in, join, break it up | Three appended commands. The current Eavesdrop stays untouched, so nothing re-rolls. **Reverses the documented "It names nobody" choice; needs sign-off.** | **P** | M | new player-issued commands |
| **Activities that count for a week** | A nap gives "Rested: +1 to competitions until the week turns"; cooking for the house gives one extra conversation. Uses the existing `StoryModifierState` through one appended `UseHouseActivity` command: no Roll, **no schema change** (see M9 for the mechanics). The three existing verbs stay effect-free. | P (intent) | M | yes, bounded (needs your OK) |
| **Activity moments** | The web's pop events and chains, with the houseguest actually present: Kitchen Encounter, Hot Tub Intel, A Knock at the Door, Couch Talk; Unfinished Business, Poolside Reckoning, Morning After… The roll happens inside the player's command, so only seasons that use it re-roll. Copy translated back from the web's half-Survivor reskin, never invented. | **P** | L | yes (needs your OK; storage may need schema 14, see M9) |

### Tier 3: later

- **A living house on a routine.**
  - The web's motives, scoring and chains are ported but unused.
  - Routines follow the phase: wind-down after an eviction, the scramble during campaigning.
  - An optional lights-out comes from the saved free-roam clock. **XL**.
- **HoH suite perks and politics.**
  - The HoH's private bed and ensuite.
  - "A Knock at the Door" (**P**).
  - Knocking on an NPC HoH's door.
- **Emotes, and a house that notices you.**
  - Wave, Clap, Shrug, Celebrate, Dance (**P**), with NPC answers by relationship.
  - Heads turn when you walk in; plotters hush.
- **Social geography.** Allies meet somewhere private; rivals clash in the kitchen.
- **Gatherings.**
  - "Rally the troops" actually gathers the house.
  - Alliance check-ins.
  - The Emergency Meeting waits for schema 14.
- **The diary room as a consequence.**
  - Mid-week reflections triggered by what you did.
  - NPCs visiting the chair.
  - First fix the one-diary-per-week guard, which would let a mid-week reflection cancel the post-eviction one.
- **The showmance rumour** from shared late nights in the hot tub (**P**, crisis template).
- **Train for competitions.**
  - A workout mat and a games table, with "Sharp: +1".
  - Needs two new set pieces and clips from outside the free library.
- **Game room: darts or arcade** (**P**). Needs set pieces.
- **Have-nots week** (**P**, crisis template). Needs a rules boundary, so it ships in the **schema-14 bundle**.
- **Smaller items:**
  - diegetic entry points (a diary lightbox; the nomination table);
  - doorway exits when zoomed in;
  - room-arrival lower-thirds;
  - a number-key and pad travel radial;
  - turn in place and lean;
  - crouch while listening in.

### Rejected, and why

- **A minimap.** It duplicates the clickable overview and the beacons, and there is no room for it (22 units spare at 1.2× text). Its gold pin breaks "gold is power", and the web's version is dead code.
- **Activities that ease stress and lift mood.** It invents a rule the web lacks, for little value.
- **Stances at competition stations.** The competition screen covers the world, so almost nobody would see them.
- **Shower and groom outside the HoH ensuite.** There is no bathroom (the cutaway rule cut the mirror). The HoH shower belongs to the HoH perks.
- **A sunken pool basin.** It splits the "Competition yard floor", which room queries require to be one collider. Raise the deck instead (M6).
- **The web's exact activity bonuses.** A +1 per click into `phaseEvent*Bonus` is cumulative, never resets and is read by every competition: an unlimited farm. It also uses unseeded `Math.random`.
- **Turning "Go to episode screen" or "Go to diary room" into warps or remote opens.** It breaks the arrival contract and the no-teleport tests. Warping stays an explicit extra route.

---

## 4. Implementation plan

**Every milestone ends with:**
- the full suites green on the D: harness;
- new tests mutation-tested;
- captures reviewed at 1.0× and 1.2× text and in the compact HUD;
- raised floors in `Tools/baseline.txt`;
- a SESSION-HANDOFF entry.

**Rules for new controls:**
- **Captions.** Every new control gets a **new unique const caption**. A control whose words change as you use it gets a fixed caption, with the changing text beside it as a non-raycast label.
- **Rail.** No rail rows are added; the left gutter has about 22 units spare at 1.2× text.
- **Shortcuts.** Every new shortcut goes into both `HouseCameraActions` and `Assets/Gamesim/Input/HouseCamera.inputactions`, and into the Exploration controls help card and the tutorial's "Moving around" step (`HouseTutorial.cs:58-59`).
- **Strings.** Every new world or HUD string goes through `Localisation.Text`.

**Named fixtures to re-run after anything that touches motion, seats, activities or the bake:**
- `HouseMeetingCoordinatorPlayModeTests`
- `HouseNpcMotionPlayModeTests`
- `HouseSeatPresentationPlayModeTests`
- `EpisodeNpcRuntimeRegressionTests`
- `EpisodeHouseActivityPlayModeTests`
- `EpisodePlayModeTests.NpcClick`
- the 28/28 room-pair audit
- `EpisodeHouseYardTests`

### M0: Everyone faces forward

**Scope:** §1's fix in full, plus the click-sink fix and the record ledger.

**Files:**
- `Editor/AuthoredAssetImporter.cs`
- `Editor/HumanoidReactionAuthoring.cs` and the four `.anim` files
- `ArtSource/tools/bb_anim.py`, `ArtSource/animation/bb_anim_casual.py` and `bb_anim_casual.fbx`
- `Runtime/House/HouseSeatPresentation.cs`
- `Runtime/House/HousePlayerController.cs` (`TryTravelTo`)
- `EpisodeDirector.cs` and `EpisodeDirector.DiaryRoom.cs` (gait, camera)
- `EpisodeHud.cs`: display-only chrome gets `raycastTarget = false`. The Interaction prompt is excluded, because M3 makes it a button.
- WORLD-INTERACTION.md, and the `HumanoidClipWiring` docstring

**Tests:**
- as in §1;
- a PlayMode test that a travel button's trip over 8 m runs;
- a test that a floor click through the Status line walks.

**Accepted when:** as in §1.

### M0b: The engine enforces the study budget

- **Change:** `EpisodePreparation.cs:14` checks `SocialActionBudget` instead of the legacy 18.
- **Tests:**
  - rewrite `EpisodePreparationTests.cs:71-80`: study at the budget minus one is accepted; study at the budget is rejected;
  - an engine test that the budget holds for every route.
- **Before landing:** audit every recorded fixture that studies. **Sim: validation.**

### M1: Animation intake, pace, and the stand-in fix

1. **Download** the v3.0 Standard (after your OK) and diff its names with the committed copy. Record it in `CC0-SOURCES.txt`.
2. **Split script** `ArtSource/animation/bb_anim_ual_split.py`, using the naming in §2. The first wave:
   - **looping:** Swim_Idle, Swim_Fwd, Sitting_Idle, Sitting_Talking, Idle, Walk, Jog_Fwd, Jump_Loop;
   - **one-shots:** Sitting_Enter, Sitting_Exit, Jump_Start, Jump_Land, Interact, PickUp_Table.
   - Horizontal takes go in the trim table. Hand-written metas.
3. **Move the override keys.** Idle and Run move off `Sleep_loop` and `SleepLying_loop` onto dedicated placeholder clips. UMA's own idle and run still play, unless you choose the Quaternius set (§6). Keep `UmaBodyProvider.ResolveController`'s `Holds()` consistent, or every UMA body silently loses sit, talk and react. Update the stand-in test (`HumanoidAnimationTests.cs:256-263`).
4. **`Pace`.**
   - `CharacterPresentation` sets a `Pace` float from the measured agent speed divided by each clip's native speed, clamped to 0.75–1.35.
   - Walk and Run use it as their speed parameter.
   - The non-UMA Walk gets the same multiplier until it has a Run.
   - A probe assertion: planted-foot slide relative to the ground under about 0.15 m/s at walk and run speeds, on both casts.
5. **Tests:** grow `HumanoidClipWiring.Takes` and its tests, and the loop and name-collision tests from §2. Every new take passes the facing test and a UMA render.

**Accepted when:**
- each new take plays facing forward on a UMA body in a capture;
- UMA bodies still sit, talk and react;
- feet no longer visibly skate at walk or run.

### M2: The travel layer

**Two names, two jobs:**
- `HousePlayerController.TryTravelTo(pos)` owns **gait** (from M0).
- `EpisodeDirector.Travel.cs` owns **errands**, in a new partial:
  - an `Errand` model (Station, Diary, Room, Houseguest, Prop);
  - one `Begin(errand)` that cancels travel, ends the diary visit and ends house activities;
  - `TickErrand`, which clears on arrival or on a floor click;
  - `CurrentErrand` and `PlayerRoom` (the nearest-marker rule the HUD already uses).

**Details:**
- **Panels.** `Begin` closes panels through a variant that **keeps the overview open** when the trip came from the map. `ClosePanelsInternal` ends the overview today (`EpisodeDirector.cs:571-574`).
- **Overview exit rule.** The overview stays up until arrival, then eases back to the follow shot; any camera input exits early.
- **Wrappers.** `GoToStation` and `GoToDiary` become thin wrappers, so their captions and messages do not change.
- **The NPC chase.** The chase errand (`headingToNpcId`, `TickWalkToHouseguest`) migrates last, under `EpisodePlayModeTests.NpcClick` (7 tests).

**`GoToRoom(roomId)`** uses a room-to-destination table:
- **Private:** runs `GoToDiary()` when the diary is usable, otherwise a room landing. There is no Diary marker; the chair is inside Private.
- **Nomination** (and **Yard** in a competition phase): runs the station errand, so E works on arrival.
- **Every other room:** lands on a clear spot from the SpawnNear spiral (`EpisodeDirector.cs:268-302`: sample the NavMesh, then the room's floor, then capsule clearance), at least 1.25 m from the marker.

**Players who can't move.** For evicted, jury or spectating players (input off), room pins and map rows move only the camera ("Watch").

**Quick travel** (if §6 says yes):
- `HousePlayerController.TryWarpTo`:
  - refuses while an activity owns the agent;
  - warps, resets the path and syncs physics;
  - never touches agent speed.
- A public `HouseCameraRig.CutTo`; `ApplyCameraImmediately` is private today.
- A 0.3 s dip, or an instant cut under reduced motion.
- **What counts as a second press:** the same errand's control, pressed after the 0.35 s double-click window, while en route and more than 3 m out. On arrival, the same press opens instead.
- **Which controls may cut:** only beacons, map rows and "Go to next stop". The rail's "Go to episode screen" and "Go to diary room [R]" never cut.
- WORLD-INTERACTION adopts "the player root may warp in free roam, on an explicit second press" as a written rule.

**Tests:**
- every room reachable, with a clear landing;
- the Private pin's arrival satisfies `CanUseDiary`;
- the diary and the station still refuse to open remotely;
- the existing no-teleport tests unchanged;
- a warp refused during an activity, a diary visit or a challenge;
- a double-click on a beacon runs and does not warp.

**Accepted when:** every destination is reachable at real speeds (2.2 / 4 m/s), and captures show the camera following.

### M3: The clickable house

1. **Pick layer.**
   - **Layer 9.** Add a `HouseLayers.Beacon` constant and name layer 9 in TagManager. Change `Sight` to `Default & ~(Furniture | Beacon)`; a new layer falls into both masks by default. Extend `HouseLayersTests.LookingIsBlindToFurnitureAndPointingIsNot`.
   - **Event.** `TravelBeaconSelected`, with its raiser.
   - **One dispatch for hover and click.** It resolves the anchor **nearest the hit point** among a prop's clickable anchors, not the first in the hierarchy (`HouseFurniture.cs:66-68`).
   - **Hover shows:** a raycast-free HUD tooltip, an emission rim on the prop, and the reason for an empty click in Status.
   - **Lifetime.** Beacons exist only at runtime, never at bake time.
2. **Overview as map.**
   - Column rows become Buttons, one unique caption per room, calling `GoToRoom`. Chips are clickable through the beacon layer.
   - YOU and HEADING states.
   - A "talking" glyph using exactly the live feed's test: room and "talking" only, never names or topics.
   - **Keyboard and pad.** The UI's Navigate shares WASD, the arrows, the stick and the d-pad with the camera's Pan, and any Pan input drops the overview (`HouseCameraRig.cs:721-727`, `Overview.cs:78-83`). D-pad up is the Overview toggle itself. So while a control in the overview column is selected, suppress Pan and ZoomRate, and make the toggle ignore d-pad up.
   - **Tests:** queue gamepad d-pad and stick input, and keyboard arrows, with a row focused. Assert the overview stays up and the focus moves.
3. **Room beacons.**
   - One `HouseTravelBeacon` per room marker, with the marks listed in §3.
   - The unused Pack2 `pin_room` art.
   - A count refreshed every 0.5 s.
   - Faded by camera distance.
   - Hidden during panels, ceremonies, challenges and the diary.
   - **Static under ReducedMotion.**
   - A setting: zoomed out / always / overview only.
   - The HoH pin's mark is gold only as `RoomLabels` already colours it. Whether a non-HoH player may enter the suite is a decision in §6.
4. **Next-stop beacon.**
   - **Position:** from `ResolveStationPosition`, or the pending diary.
   - **Modes:**
     - idle = the episode screen, always clickable;
     - competition;
     - decision: at the screen, unless a diary draft or reflection is pending;
     - diary.
     - Each mode has a distinct glyph as well as a colour.
   - **Behaviour:** out of reach it travels; in reach it opens, through the same gates (`CanUseStation`, `CanUseDiary`).
   - Spectators and the reflection gate behave as they do today.
   - The yard's entrance arch becomes its clickable body.
5. **HUD twins.**
   - A "Go to next stop" button next to the Objective chip runs the beacon's method. The next-stop text stays a label beside it.
   - Extract `Interact()` from `EpisodeDirector.cs:408-424`. An "Interact [E]" button runs it; the prompt's changing text stays a label beside it.
   - Place both in the compact HUD as well.
6. **Bindings.**
   - East / B, or Backspace = "Finish activity".
   - D-pad right, or N = "Go to next stop".
   - Its second press = cut, if allowed.

**Tests:**
- in the Social phase, the beacon exists, travels, and opens the phase panel on arrival;
- a hover names the same action the click performs;
- a hob hit selects "Cook a meal", not "Prepare a snack";
- beacons are hidden during ceremonies and challenges;
- each new caption is found exactly once in every mode;
- the 1.2× layout test passes.

**Accepted when:** captures show every beacon mode, the map, and pins zoomed out and in, at 1.0×, at 1.2×, and in the compact HUD.

### M4: Activities as world state

**Catalogues.**
- `TryDescribe` stays the **player** catalogue: the House Activities panel lists `InScene`, which filters by it (`HouseFurniture.cs:17-18`), and `StartPlayerHouseActivity` requires it (`HouseActivities.cs:67-68`).
- Add a separate **`HouseFurniture.Ambient(anchor)`** predicate for the NPC ambient routine (`HouseActivities.cs:137`). It holds today's three venues, so NPCs are not pulled to beds, the pool or the stove until M8.
- Test that the ambient list is unchanged after M4–M7.
- Group the panel's rows by verb (e.g. one "Lie down" row with a berth picker), so ten berths do not add ten rows.

**No freeze.**
- A new close path closes the menu **without** finishing the activity. Today every close path finishes it: `CloseHouseActivities` → `FinishPlayerHouseActivity`, via `ClosePanelsInternal`.
- The "Finish activity" chip keeps its caption and renders on completion whether or not the menu is open.
- The notebook and settings may open without ending an activity. A conversation, the phase panel or travel does end it.
- **Pad Start.** With no panel open, Start opens Settings, so a pad user's activity must survive that too (`EpisodeDirector.cs:395-398`).
- **The world stays clickable.** `HousePlayerController.Update` returns while an activity owns the agent (`:84, :247`). Keep picking in that case, and raise `ActivityInterruptRequested(hitKind)`. The director ends the activity, with a get-up (Sitting_Exit, or the reverse lie-down blend) or behind the 0.3 s dip, then applies the move.

**Leases.**
- Each berth, the stove, the pool edge and the swim spot gets its **own VenueId**.
- The hot tub's two seats share `hot-tub-activity`.
- `VenueInUse` stops counting activity leases held on *other slots* of the same venue. Conversation leases still lock the whole venue.
- A player activity at a meeting venue may delay a saved NPC reunion. Bound it (the 18 s rule for today's verbs), and test that it cannot stall the NPC clock.

**Movement and duration.**
- Activity moves choose a gait from their route length, like the floor click. `TryBeginActivityMove` never does today.
- The 20 s approach deadline scales with route length, so a cross-house trip to the pool does not time out at real speed.
- **Durations:** sleep, soak and swim last until you move; cook is an Interact beat plus about 12 s; today's three verbs keep 18 s.

**Body-pose owner.**
- `HouseSeatPresentation` gains Lie and Float modes. Name plates, seated picking and witness points keep working.
- Bones are re-resolved after UMA re-assembly.
- New root components join the `HouseNpcMotion` whitelist.
- While a Lie or Float pose is active, **the camera follow target and the name plate track the visual body**. The root stays authoritative for navigation and proximity.

**Cues.**
- `Sleeping`, `Swimming` and `Cooking` bools through `CharacterPresentation`, declared only where a controller can play them.
- Activity edges come before Walk and Talk; reactions are gated by `IfNot`.
- The pose owner re-sets the cue every LateUpdate, and the NpcSocial reset clears it.
- Non-humanoid bodies get the web's procedural poses.

**Anchor pass and one rebake.**
- An editor pass beside `AuthorKitchenAnchor` authors the bed, hot-tub, pool and stove anchors.
- Each sits under a prop with a layer-8 Collision child, with its approach at least 0.6 m clear, and is optional in `HousePrototype.unity`.
- The same rebake marks the furniture tops not walkable.

**Tests:**
- **Rewrite** every input-ownership and snapshot assertion in `EpisodeHouseActivityPlayModeTests.cs` (lines 36-43, 92, 122-131, 149). The NPC clock now commits during activities, so compare season fields, or call `SuspendNpcAutonomyForDiagnostics`.
- NPCs keep moving during a player activity.
- A floor click during the approach and during the pose ends the activity and walks.
- J during sleep doesn't end it; Start during sleep doesn't end it.
- Two hot-tub seats are held at once; bed A doesn't block bed B.
- A UMA rebuild mid-pose keeps the pose (force a re-assembly).
- A far trip at real speed doesn't time out.

**Accepted when:** those pass, and captures show Seat, Lie and Float on both casts.

### M5: Sleep in a bed

**Berths:**
- 3 single beds (mattress about 0.48);
- **2 lower bunks** (0.40; there are two `bb_set_bunk` instances);
- 2 cots (about 0.34);
- the HoH bed (0.47), for the player only while HoH.

Not the upper bunks, and not the doll-scale Furnishings beds. Measure every mattress on the placed mesh.

**The pose:**
- Lie mode puts the pelvis on the mattress, runs yaw along the bed with the head at the headboard, and skips the sole clamp.
- The freed Mixamo sleep take, with a measured trim-table offset.
- A scripted lie-down blend.

**The light.** The nearest room fill dims while you sleep. The fills are **Mixed** lights under an IndirectOnly bake, so dimming changes only the direct light and the baked bounce stays. Judge the result by capture.

New `HouseFurnitureActivity.Sleep`; caption e.g. "Lie down".

**Tests:**
- the pose fits the mattress;
- the head is at the headboard (by capture);
- moving ends the nap with a get-up;
- NPCs stay out of the berths.

**Accepted when:** the captures of all four berth kinds read as sleeping, on both casts.

### M6: Swim, hot tub and pool

**Clicking the pool.** A prop with one verb starts it directly: lease, then walk, then pose, with no panel. A prop with several verbs, like the pool, starts its named default ("Take a swim") and offers the other ("Sit on the pool edge") in a small chooser at the click, with fixed captions and reachable by pad. The hover tooltip names the default.

**Hot tub.**
- Two seated slots: seat 0.54, water 0.77. Holding both needs M4's per-slot leases.
- Approach points at least 1.7 m from the centre; the collider is r 1.21 and 0.90 tall.
- The anchor floor is the tub floor (0.20).
- Sitting_Enter, with an arc over the rim.

**Pool.**
- Treading water: Float mode, Swim_Idle.
- Slow lengths move only the visual body along the 5.6 m basin (Swim_Fwd, trim-table offset). The root waits at the poolside, and the camera follows the visual body (M4).
- A splash hides the change into swimwear, if you want outfits (§6).
- Refused while a competition is staged in the yard.

**The depth problem.** The pool is a raised tray: the water plane is at 0.29 m over a basin floor at 0.04 m (`bb_set_pool.py:27-31`), about 25 cm of water.
- Build the tread-water version first and capture it.
- If it reads as a paddling pool, raise the deck (`DECK_H` about 0.9 m, under the 1.2 m fence). The deck top then becomes a disconnected NavMesh island, so mark the pool's authored `_col` not walkable either way.
- Then rebake, and re-run `EpisodeHouseYardTests` and the named fixtures.

**Tests:**
- a click on the pool starts the swim lease without opening the activities panel;
- the chest is at the water plane;
- the root never moves;
- the activity is refused during a competition stage;
- the hot tub seats two;
- the swimmer stays on screen mid-length.

**Accepted when:** captures of the soak, treading water and a length read right on both casts.

### M7: Cook in the kitchen

- A "kitchen-stove-activity" anchor at the hob, with its own VenueId. Its approach is about 0.8 m out, clear of the island's grey box.
- Caption "Cook a meal". "Prepare a snack" is unchanged; its test finds it by caption.
- **MVP gesture:** an Interact beat, then a stir, reworked from the existing counter gesture, on humanoid bodies. Non-humanoid bodies stand facing the hob.
- The pan, steam and the meal at the table come in M8.

**Tests:**
- the anchor is reachable;
- a hob hit selects "Cook a meal";
- cooking has no season effect, checked with the rewritten comparison from M4;
- the snack verb is unchanged.

**Accepted when:** the stir reads as cooking in a capture.

### M8: Next tier, in this order

1. Company at the furniture: a sibling-slot companion lease; choose the joiner by alliance, then score, then id; the join verbs.
2. NPCs use the new verbs between conversations: widen `Ambient()` one venue at a time; per-activity durations. A sleeping NPC gets up for a conversation (§6).
3. Held props and the IK layer: the pan, the plate, the towel, and the meal at the table.
4. The fridge; watch TV (after the TV decision).
5. A real sit-down and stand-up; new perches.
6. Houseguests come to you.
7. Go where the drama is; rooms and people as destinations everywhere.
8. The non-UMA humanoid path you choose (§6).
9. Close-ups, the live-feed tier, sound and VFX.
10. The memory wall; pair-specific Listen in, after sign-off.

### M9: Activities that count, if you choose

**The command.** One appended `UseHouseActivity` command:
- It has an **explicit case** in `EpisodeEngine.Execute`. Today unknown kinds fall through to `Social` (`EpisodeEngine.cs:142`).
- The engine checks the phase and the budget itself.

**The effect** is a one-week `StoryModifierState`, upserted by id:
- `activity-rested` and `activity-sharp`: +1 competition each. Decide whether they stack.
- `activity-social`: `socialBonus = 10`, which is one conversation, because `ActionsFrom` divides by 10 (`StorylineState.cs:107-110`).

**Timing.** Modifiers age at Eviction→Social (`EpisodeEngine.cs:249`), and activities exist only in Social and Campaign. A competition modifier earned in Campaign would expire before any competition. So either competition verbs count only in Social, or a Campaign award gets `weeksLeft = 2`.

**Display.** Label activity modifiers separately in the competition explanation. Today they would read "storyline" (`EpisodeEngine.cs:380, 434`; `EpisodeEngine.CompetitionExplanation.cs:35`).

**Save and scope.** No Roll and no new field, so the schema stays at 13. The panel copy changes for the new verbs only. The three existing verbs stay effect-free.

**Moments.**
- A new house-event kind "activity", added to `HouseEventKind.All`. Older builds reject saves containing it; that is forward compatibility only.
- It gets its own cap, excluded from `HouseEvents.Ready`.
- Exclude it from the Pending checks that gate WitnessProximity, or state that a pending moment suspends proximity events.
- **Choices cannot store a modifier today.** `HouseEventChoice` holds only label, description, risk, impacts and trust change. Either use a record keyed like `StorylineState.templateId`, or move moments into the schema-14 bundle.
- Copy is translated back from the web's Survivor reskin, never invented.

**Verification.** Mutation-test the engine paths, and check the fixtures that start using the command.

### Later

- **Tier 3,** as ranked.
- **The schema-14 bundle** ships every rules-boundary field together, with one migration and one sweep of the downgrade helpers:
  - the narration room list;
  - have-nots;
  - the showmance and emergency-meeting crisis templates;
  - moment storage, if M9 needs it;
  - any per-week counter.

---

## 5. Guardrails every milestone keeps

- **Determinism.**
  - Tiers 0–1 are presentation only: no command, no season RNG, no saved field. The one exception is M0b's engine validation.
  - Sim effects come only as appended commands, and seeded draws happen only inside commands the player issues.
  - Presentation randomness never uses `Roll(s)` or `npcSocial.randomState`.
  - Nothing in a seeded catalogue is reordered or extended in place.
- **The arrival contract.** The diary and the episode screen open only on arrival, spectators excepted. Warping is an explicit second route, never the default.
- **Captions are contracts.**
  - Use new unique constants.
  - Controls with changing text get fixed captions plus a label.
  - Decorate around a caption; never append to it.
  - Keyboard and pad parity through HUD-canvas buttons.
  - Shortcuts added in both maps and in the help and tutorial.
- **Take orientation.**
  - Humanoid takes use Body Orientation; horizontal takes use Original plus a measured offset. Blender exports face +Z.
  - Every take passes the facing test and a render before it is wired.
- **One body-pose owner.**
  - Only the visual root moves.
  - The cue is re-set every frame and cleared on reset.
  - Bones are re-resolved after UMA rebuilds.
  - The camera and the name plate follow the visual body in Lie and Float.
- **Catalogues.** `TryDescribe` is the player's list, and `Ambient()` is the NPCs'. Widening one never widens the other by accident.
- **NavMesh.**
  - Beacons and pick colliders exist only at runtime.
  - Anchor approaches sit at least 0.6 m clear.
  - Rebakes are batched and followed by the named fixtures.
- **Performance.** UMA costs 3.2 ms at twelve bodies. IK and extra layers run only on bodies performing an activity. Profile each milestone against 60 fps.
- **Never invent content.** Web copy is translated back from Survivor terms. Props the house lacks (fire pit, hammock, billiards) mean their templates wait; they do not move somewhere else.

---

## 6. Decisions for you

**Answered (24 September):** "Teleport if far away, run it closer and walk if very close. Presentation only, no non uma, keep its own idle and run, figure out outfit swaps, keep listen in."

| # | Decision | Taken as |
|---|---|---|
| 1 | Transport | Errands measure their route: over 20 m a warp behind a 0.3 s dip with a camera cut, over 8 m a run, under that a walk. Floor clicks still walk or run; a chase still runs. |
| 2 | Beacon arrival | Default taken: out of reach the icon travels; within reach (the screen's 3 m, the diary's room) it opens. Nothing opens on arrival by itself. |
| 3 | Season effects | None. Presentation only; M9 is not built. |
| 4 | The pool | Default taken: treading water and slow lengths in the existing pool, no rebuilt deck. |
| 5 | The library | No download. The committed Universal Animation Library Standard supplies the clips. |
| 6 | The non-UMA cast | No work: its bodies sit where a UMA body would lie or swim. |
| 7 | Locomotion | UMA keeps its own idle and run; the library's idle and jog are only their override keys. |
| 8 | Outfits | Swimwear for the pool and the hot tub, nightwear for bed: the player's own set, or the everyday set with the outer layers off. Built behind the visible body and swapped when ready. |
| 9 | NPC life | Default taken: the cast keeps to its three old places (`HouseFurniture.Ambient`). |
| 10 | Listen in | Unchanged. |
| 11 | The HoH suite | Default taken: the HoH bed is the Head of Household's; the room itself is open. |
| 12 | The TV | Not touched. |
| 13 | Naming | "Episode screen"; room icons show when the camera is pulled back (18 m and over). |

The original questions follow, for the record.

1. **Transport.** Walk or run there with the camera following (recommended default), an instant cut, or both (a second press cuts, plus a Settings switch)?
2. **Beacon arrival.** When a trip starts from the next-stop beacon, should the screen open by itself on arrival, or still wait for E?
3. **Should activities affect the season?** Recommended for the MVP: no, presentation only. If yes, later:
   - bounded one-week modifiers (+1 competition, one extra conversation);
   - the web's activity moments.

   These re-roll any season that uses them.
4. **The pool.** Accept treading water and slow lengths in 25 cm of water, or rebuild a deeper raised deck (Blender, rebake, retest)?
5. **The library.**
   - May I download the free v3.0 Standard (about 14.5 MB, CC0) to compare with the committed copy?
   - Later: Pro ($9.99+), Source ($14.99+), or Mixamo for lying, cooking and workout?
   - Is the repo public? 15 Mixamo FBX files are already committed, and Mixamo does not allow redistributing raw files.
6. **The committed non-UMA cast.** Procedural poses for the MVP (recommended), then which path:
   - the proxy bake;
   - a re-rig;
   - the Universal Base Characters?
7. **Locomotion look.** Keep UMA's own idle and run, or move every body to the Quaternius idle, walk and jog?
8. **Outfits.** Change into swimwear or sleepwear for activities? Each change rebuilds the UMA body, hidden under a splash or a fade.
9. **NPC life.**
   - May NPCs sleep, swim and cook on their own?
   - Does a sleeping NPC get up for a conversation or when you click them?
10. **Listen in.** Reverse the "it names nobody" design so eavesdropping targets the pair you are watching?
11. **The HoH suite.**
    - Is the HoH bed and ensuite usable only while you are HoH?
    - Does the HoH pin take a non-HoH player into the suite, or to the door?
12. **The living-room TV.** Restore it for "Watch TV", or remove its nine orphaned lights?
13. **Naming and defaults.**
    - "Episode screen" or "ceremony screen"? The HUD uses both.
    - Room icons shown zoomed out, always, or in the overview only?

---

## 7. Verification kit

- **Probes as regression tests.** Rebuild the two facing probes as real tests. They hold each state's parameters and assert that the state plays before measuring.
- **EditMode facing test.** A `PlayableGraph` over every take, with the per-take expectation table.
- **Captures.**
  - PlayMode captures on a screen of their own, for every new pose, beacon state and travel view.
  - At 1.0×, at 1.2×, and in the compact HUD.
  - Bodies pictured through BakeMesh proxies.
- **After each rebake:** the named fixtures (§4).
- **Performance:** profile with twelve UMA bodies against 60 fps.
- **Mutation testing:** mutation-test every new test, and raise the floors in `Tools/baseline.txt` in the same commit.

---

## Appendix A: probe evidence

**The first probe was flawed.**
- It played each state without holding its parameters, so within a third of a second the controller had moved several of them back to Idle or into standing up. Its Talk, Listen, Argue, Run and seated rows measured the wrong clip.
- Its Walk sample, which held Speed, was valid and is kept below.
- The second probe held each state's parameters and confirmed the state for every sample. These are its numbers.

**UMA body** (`GamesimHumanoid (UMA locomotion)`):

```
STATE Idle            hips +000  shoulders +001  playing Idlex30
STATE Walk            hips +175  shoulders -178  playing Walkx30
STATE Run             hips -004  shoulders -006  playing Runx30
STATE WalkStop        hips -178  shoulders -169  playing WalkStopx30
STATE SitIdle         hips -179  shoulders -179  playing SitIdlex30
STATE SitTalk         hips +177  shoulders +175  playing SitTalkx30
STATE Talk            hips -165  shoulders -165  playing Talkx30
STATE TalkB           hips -179  shoulders -178  playing TalkBx30
STATE TalkC           hips -179  shoulders -177  playing TalkCx30
STATE Listen          hips -165  shoulders -165  playing Listenx30
STATE Argue           hips -168  shoulders -168  playing Arguex30
STATE ReactWon        hips -178  shoulders -176  playing ReactWonx30
STATE ReactCheered    hips -177  shoulders -178  playing ReactCheeredx30
STATE ReactNominated  hips -166  shoulders -165  playing ReactNominatedx30
STATE ReactSaved      hips -166  shoulders -165  playing ReactSavedx30
STATE ReactEvicted    hips -166  shoulders -165  playing ReactEvictedx30
```

**The walk with Speed held** (first probe, valid):

```
hips local z range -0.036 .. -0.005 (no slide-and-snap)
left foot planted, slide rel. hips along root forward +0.866 m/s
right foot planted, slide rel. hips along root forward +1.288 m/s
run: left -4.815 m/s, right -5.354 m/s
```

**Non-UMA body** (`GamesimCharacters/dan-gheesling`, `GamesimCharacter`, culling forced to AlwaysAnimate for the probe):

```
STATE Idle             hips +000  shoulders +000  playing Idlex30
STATE Walk             hips -002  shoulders -002  playing Walkx30
STATE SitIdle          hips +180  shoulders -180  playing SitIdlex30
STATE SitTalk          hips +180  shoulders -180  playing SitTalkx30
STATE Talk             hips -180  shoulders +180  playing Talkx30
STATE Listen           hips -180  shoulders -179  playing Listenx30
STATE Argue            hips -180  shoulders +179  playing Arguex30
STATE React_won        hips -180  shoulders -180  playing React_wonx30
STATE React_cheered    hips -180  shoulders -180  playing React_cheeredx30
STATE React_nominated  hips -180  shoulders -180  playing React_nominatedx30
STATE React_saved      hips -180  shoulders -180  playing React_savedx30
STATE React_evicted    hips -180  shoulders -180  playing React_evictedx30
```

**Blender check of `bb_anim_Walk_loop.fbx`:**
- It faces the same way as `bb_anim_Talk_loop.fbx`.
- Its hips travel +1.84 m along the facing per 32-frame cycle.
- Its planted feet slide −0.056 and −0.054 m per frame: a genuine forward walk.

## Appendix B: sources

- [Universal Animation Library (itch.io)](https://quaternius.itch.io/universal-animation-library)
- [Universal Animation Library (quaternius.com)](https://quaternius.com/packs/universalanimationlibrary.html)
- [Universal Animation Library 2](https://quaternius.com/packs/universalanimationlibrary2.html)
- [OpenGameArt listing (Standard zip)](https://opengameart.org/content/universal-animation-library)

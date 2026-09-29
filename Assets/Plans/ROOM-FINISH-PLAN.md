# Room finish pass — Pack 6 laid over the rooms that exist

2026-09-28. The brief is the nine concept images and the "room improvement implementation guide"
(quoted in §1); the material is `gamesim_room_finish_pack_6.zip` (inventoried in §2). This plan says
what the pack really is, what the house really is, how the one lands on the other, and in what order.
It is written to be implemented slice by slice, each slice a commit, each verified by a capture.

The one-line brief: **treat the concept images as art direction for the existing rooms, not as
layouts.** Walls, doors, NavMesh, interaction anchors, camera behaviour, URP and the baked-night
lighting stay. What crosses over is materials, furniture density, focal hierarchy, wall treatment and
prop composition — with the perimeter neon demoted from "the thing that defines every room" to a
supporting 20–30%.

---

## 1. The brief, condensed

**Non-negotiable rules** (the guide's, kept verbatim in spirit):

1. Do not move walls, doors or gameplay boundaries.
2. Do not change URP or the baked-night lighting system.
3. Do not read the generated images literally where they changed room proportions.
4. Interaction locations and NavMesh access beat decorative furniture.
5. The images are for: furniture composition, materials, colour balance, wall treatment,
   environmental branding, prop density, focal hierarchy.
6. Reduce reliance on perimeter neon; a room must still look finished with the neon mentally removed.
7. New decorative light is emissive/practical presentation, not new bake inputs.
8. Use Pack 5 decals, displays, signage and emissive masks rather than unique geometry.

**Per-room targets** (the guide's own words for the finished read):

| Room | Now reads as | Must read as | Overhead fingerprint |
|---|---|---|---|
| Competition yard | large illuminated yard | reconfigurable television competition stage | lanes / stage / display |
| Shared bedroom | bed + rug + open floor | shared living space where several people clearly reside | beds + textile field |
| Diary room | another lounge | confessional studio (strongest redesign; navy + violet + warm gold) | circular halo + single chair |
| Living room | somewhere characters stand | the house's primary social arena | large sectional |
| Kitchen / dining | repeated modules | biggest material-quality opportunity: stone island, appliance rhythm, backsplash | stone island |
| HoH suite | a bedroom | status: dark wood, cream, muted green, brass; a reward station | luxury bed + gold accents |
| Nomination room | table and chairs | all attention on one framed presentation surface | screen + ceremony arrangement |
| Game room | empty | three zones, playful, neon down 30–40% | pool table |

**Density guideline:** 2–4 large anchors, 5–10 medium props, 3–6 small *authored clusters* per room.
Never dozens of loose objects.

**Lighting:** keep the bake. Every room ends with one focal light, 2–4 practicals, one emissive
treatment. Per-room lighting is what the concept renders communicate, not a new solution.

**Neon rule:** 70–80% ordinary premium interior + 20–30% GameSim neon/set identity. Neon is for
branding, power rooms, competition, one wall feature, event accents — not shelves, furniture edges,
every wall, every doorway.

**Order (the guide's eight passes):** 1 proportions → 2 major furniture → 3 materials → 4 medium
dressing → 5 small clusters → 6 GameSim identity → 7 interaction staging → 8 light/emissive balance.

**Acceptance, per room:** recognisable with labels off; one unmistakable focal point; two secondary
anchors; no unexplained empty floor; NPCs can form natural groups; furniture scale right beside a
character; wood/fabric/metal/stone visibly differ; walls carry intentional interest; perimeter neon
supportive, not dominant.

**Pack README priorities:** Diary → Kitchen/Dining → Living → HoH → Nomination → Game room →
Bedrooms → Yard. This plan follows that order.

---

## 2. What the pack is — as found, not as advertised

`GameSim_RoomFinish_Pack6`: 177 entries, 2.2 MB. 175 PNGs, `README.md`, `manifest.json`. **No meshes,
no normal/metallic/occlusion sets, no prefabs.** Every file is a flat, generated finish graphic: a
tileable pattern, a whole-object texture (a rug, a duvet), a sign, a frame template with a transparent
window, a label, a mask, or an editor marker. That is what "overlays, decals, material masks, rug/fabric
textures, framed art, prop labels and editor staging aids" means in practice, and it sets the scope: the
pack finishes *surfaces*; every *object* still has to be an authored `bb_set_*` piece, a kit model, or a
primitive the pass builds.

| Folder | Files | Size | Alpha | Nature and use |
|---|---|---|---|---|
| WallTreatments | 13 | 1024² | mostly soft | Tileable wall patterns (oak/dark slats, brass inlay, charcoal geometric/upholstered, navy upholstered, frosted glass, hoh geometric, nomination presentation, bedroom geometric blue/rose, botanical green, diary radial halo). **Bands on the cutaway walls.** |
| Rugs | 15 | 1024² | soft edge | Whole rugs (framed designs). **One quad each, alpha-clipped**; not tileable. |
| Bedding | 12 | 1024² | soft | Duvets (green/rose plain tile; navy diamond, blue stripe, cream grid, hoh cream), pillows (blue/green/rose geo), throws (blue/rose/hoh). **Re-skin the bed pieces' `linen_white` / `velvet_teal` slots.** |
| Upholstery | 10 | 1024² | soft | Weaves (navy, teal, cream, charcoal, rose), velvets (green, navy), dark leather, channelled headboards (navy, cream). **Tileable fabrics for sofas, chairs, headboards.** |
| WallArt | 24 | 768×1024 | soft | Pale abstract prints (12 designs, each twice). **Art quads inside frame quads.** |
| PhotoFrames | 10 | 1024×768 / 768×1024 / 600×760 | cutout | Frame templates with open windows (single portrait/landscape, four clusters, HoH gold), a polaroid, a corkboard, HoH letter paper. **Frame quad 2 mm in front of what it frames.** |
| Kitchen | 16 | 1024² / 512² / 768×256 | mixed | Backsplashes (marble soft, light tile, dark tile — tileable), quartz vein overlays ×3 (cutout, for a counter top), appliance panels (fridge/oven/coffee), labels (jar, coffee, cereal, bottle, snack red/blue), dish towel. |
| GameRoom | 15 | 1024×300 / 768×512 / 768×1024 / 900×360 | mixed | Arcade marquees ×3, board-game and card-game box art ×6, posters ×3, trophy plaque, mini-fridge front, pool-table rule card. |
| HOHRewards | 12 | 768×512 / 1024×640 / 512×640 | mixed | Gift wrap, envelope, letter sheet, celebration card, snack bags (blue/gold), drink label, reward plaque, photo placeholders ×4. |
| BedroomClutter | 14 | 512² | soft | Labels for possessions: books (blue/red), storage box, cosmetic, luggage tag, personal card, headphone case, laundry basket, magazine, phone screen, notebook, water bottle, toiletry, shoe box. **Faces of small carton props.** |
| NominationRoom | 11 | 1024², 1600×900, 768×1024, 1200×420 | mostly cutout | Idle display "NOMINATIONS" (1200×420), portrait frames (hoh/nominee/idle), gold linework mask (1600×900), reveal pulse (gold ring), ceremonial floor ring, dark marble, wall emblem, screen ornament, placeholder. |
| DiaryRoom | 10 | 1024², 1200×420, 512×256 | mixed | Violet padded wall, dark wood slats, acoustic panel, curtain fabric (tileable); radial carpet; eye-halo mask (cutout); signs "CONFESSIONAL", "ON CAMERA", "PRIVATE" (1200×420); camera tally light (512×256, cutout). |
| EditorInteractionMarkers | 12 | 512² | cutout | SIT / COOK / EAT / LEAN / OBSERVE / conversation kinds / hero focal / camera stage / path clearance. **Editor gizmos only; never shipped.** |
| Preview | 1 | 1800×1450 | opaque | Contact sheet. Reference only. |

Two notes from the scan: the "tileable" verdicts above come from edge matching and the README's own
words; only the wall treatments, upholstery, plain duvets, backsplashes and the diary surfaces are
meant to repeat, everything else is placed once. And the wall art is 12 designs shipped twice
(01=13, 02=14 …), so a room gets at most twelve distinct prints across the house.

**Pack 5 is the other half of the material.** Its world textures are imported and catalogued but, apart
from the diary halo and neon, nothing is wired (`ASSET-PACKS.md`). The guide calls for its
`DigitalDisplays` on the screens, its `EnvironmentWallGraphics` and `RoomSignage` on walls, its
`SurfaceDecals` as detail masks. This pass wires the ones each room's finish needs, and no more.

---

## 3. The house as it stands — measured

**Rooms** (floor top at y = 0; `Room floor` renderers under `House Architecture`):

| Floor | Centre | Size | Walls | Notes |
|---|---|---|---|---|
| Living room floor | (−7, −5) | 14 × 10 | 1.5 m | west wall x −14; north edge z 0 carries the disabled `Television` box at (−5, 1.4, −1.5) |
| Kitchen floor | (7, −5) | 14 × 10 | 1.5 m | the authored kitchen run along z ≈ −0.45 (its "north" wall is the z 0 partition), x 1.0–7.4 |
| Bedroom floor | (−7, 5) | 14 × 10 | 1.5 m | bunks on the west wall, three singles on the z 10 wall |
| Private room floor (Diary) | (7, 5) | 14 × 10 | 1.5 m | `bb_set_diarychair` at (7, 0, 3) facing −z; the confessional camera stands 2.6 m out |
| HoH floor | (−9.31, −15) | 9.38 × 10 | 1.1 m | south wing; `bb_set_hohbed` at z −12.8 against the z −10 link wall |
| Nomination floor | (0.075, −15) | 9.38 × 10 | 1.1 m | `bb_set_ceremonyscreen` at (0.075, 0, −18.9) facing +z; round table + six chairs at z −14.6 |
| Games floor | (9.46, −15) | 9.38 × 10 | 1.1 m | sofa corner, bar and stools, desk, two have-not cots, TV console on the z −10 wall |
| Competition yard floor | (0, 15) | 28 × 10 | 1.5 m house side, 1.2 m fences | lanes, gates, podiums, backdrop and signs; pool east, hot tub NW |

**Walls are cutaways.** The four main rooms have 1.5 m walls (`West wall`, `East wall`, `House / yard
left|right` at z 10, the `Living / kitchen` and `Bedroom / private room` partitions at x 0, the
`Living / bedroom` and `Kitchen / private room` partitions at z 0); the south wing has 1.1 m walls (the
`South wing link left|right` at z −10, the dividers at x −4.617 and 4.767, the south wall at z −20).
All are 0.25 m thick BoxColliders whose renderers the authored shell (`bb_shell_house`) stands in for.
So: **every wall surface in the house is a band 1.1–1.5 m tall.** A "hero wall" here is a band, a
"framed art" hangs at 0.6–1.3 m, and a sign at 1.0 m is already at the wall's top. Nothing may hang
higher, or it floats in open sky over the wall line (`AuthoredAssetImportTests` polices the pieces;
this pass polices its own quads).

**Doorways** are gaps the `HouseDoorwayResolver` keeps clear; nothing in this pass has a collider, so
nothing can block one, but bands stop 0.3 m short of every gap so the shell's jambs stay visible.

**Dressing pipeline.** `HouseSetPieces.Apply` rebuilds the `Set Pieces` root from its `Prop` table
(floor-fraction positions, `HouseCatalogue.Resolve` for models, colliders stripped, then
FitCollision → DoorwayResolver → Rebake → SaveScene). The prototype's neon trim, planting and decor
are transplanted by `EpisodeHouseDressing`. Floors and the shell wall wear `bb_mat_floor_*` and
`bb_mat_shell_wall` (`HouseFloorDressing`). The bake (`HouseCinematicLighting`) marks every
architecture renderer ContributeGI and lightmaps it; **re-running `HouseSetPieces.Apply` destroys the
lightmapped renderers and loses the bake** — which is exactly why this pass builds its own root and
never rebuilds `Set Pieces`.

**Runtime surfaces.** The diary studio is built per visit by `DiarySeatPose.BuildStudio` (a 4.6 × 2.9 m
`Upholstered interview wall` at local z −0.85, key and fill spots) and dressed by
`DiaryStudioBranding.Dress` from `HouseBrandingPalette` (Resources; built by `HouseBrandingAssets`):
the pack-5 halo, the neon name, two light strips, a violet wash. The ceremony screen's face carries a
world-space `ScreenSurface` canvas during the cut scenes (stand-off 0.012 m). The living room's
eviction screen is a runtime clone of the ceremony board (`CeremonySets`). The HoH and games TVs are
`bb_set_tvconsole` pieces sharing `bb_mat_glow_screen` (piece-space UVs; a texture on the shared
material paints every screen at once — `ASSET-PACKS.md` DigitalDisplays).

**What the captures say** (`D:\GamesimAcceptance\probe-living.png`, `walkthrough-*.png` from the
last pipeline run): the house is navy walls, textured floors, a cyan/pink/gold neon perimeter on
every wall and doorway, sparse pale furniture, and the neon is doing all the room identification.
That is the guide's diagnosis, confirmed.

---

## 4. Mechanics — how the pack enters and lands

### 4.1 Import (one commit, before any room)

- `ArtSource/ui-packs/tools/bb_ui_packs.py` gains pack 6: `(6, 'gamesim_room_finish_pack_6.zip',
  'Pack6_RoomFinish')`, world root, with a pack-6 branch in `kind_for`:
  - **`WorldTile`** (new `UiPackCatalogue.Kind`: Default, mipmapped, sRGB, **Repeat**, CompressedHQ)
    for `WallTreatments/*`, `Upholstery/*`, `Bedding/duvet_*`, `Kitchen/backsplash_*`,
    `DiaryRoom/{diary_violet_padded_wall, diary_dark_wood_slats, acoustic_panel_texture, curtain_fabric}`.
  - **`NeonMask`** (linear, Clamp) for `*_mask.png` (`diary_eye_halo_mask`, `gold_linework_mask`).
  - **`WorldColour`** (Default, mipmapped, sRGB, Clamp) for everything else.
  - `EditorInteractionMarkers/*` are installed under `Assets/Gamesim/Editor/Gizmos/Pack6/` — an
    `Editor` folder, so they never enter a build — and catalogued as `WorldColour` so the import test
    still holds them.
  - `Preview/`, `README.md`, `manifest.json` go to `ArtSource/ui-packs/Pack6_RoomFinish/` (reference).
- `UiPackImporter.Apply` maps `WorldTile` to Repeat; `UiPackImportTests` learns the new kind and its
  count (397 + 71 + 175).
- Metas are minted by the tool (uuid5 of the path), so a re-run never re-identifies a file.

### 4.2 Materials

`HouseRoomFinish.Finish(name, texture, tiling, tint, smoothness, metallic)` creates a URP/Lit asset
under `Assets/Gamesim/Art/RoomFinish/Materials/bb_mat_p6_<room>_<use>.mat` when absent and returns the
existing one otherwise (the `Tone()` idiom). Outside the pack root on purpose: `UiPackImportTests`
holds that root to catalogued PNGs and nothing else. Decals and rugs use the same shader with
`_AlphaClip` on. Glow pieces (signs, strips, pulse rings, the tally) are saved copies of
`bb_mat_branding_glow` (`HouseBrandingAssets.ConfigureAdditive`) carrying their texture and an HDR
tint — the same URP Unlit additive variant a player build keeps. Re-skins of authored pieces (duvets,
headboards, the counter top) are per-instance material assets assigned on the instance, never edits to
the shared `bb_mat_*` that every bed and every screen wears.

### 4.3 The pass

`Assets/Gamesim/Editor/HouseRoomFinish.cs`, menu **Gamesim/U07/Finish the rooms (pack 6)**, batch
entry `-executeMethod Gamesim.Editor.HouseRoomFinish.Apply`.

- Opens `EpisodeHouse.unity`, finds `House Architecture`, destroys and rebuilds one root, **`Room
  Finish`**, with a child group per room. `Set Pieces`, `Broadcast Dressing`, the floors, the shell,
  the lighting and the NavMesh data are read, never rebuilt.
- Vocabulary, every helper measuring from the room's floor bounds and the wall colliders:
  - `Band(room, side, texture, metresPerTile, from, to, height)` — a quad on a wall's inner face
    (collider face + 0.01 m), never taller than that wall, stopping 0.3 m short of a doorway gap.
  - `Rug(room, texture, metres, x, z, yaw)` — an alpha-clipped floor quad at +0.012 m; any flat rug
    piece under `Set Pieces` whose centre lies within 0.6 m gets its renderer disabled (kept for the
    layout record, the podium-block idiom), so rugs never stack.
  - `Poster(room, side, art, frame, width, x, centreHeight)` — art quad, frame quad 2 mm proud.
  - `Sign(room, side, texture, tint, width, x, centreHeight)` — glow quad, both faces, no shadow.
  - `Carton(room, texture, size, x, z, yaw, lift, tint)` — a cube with the label on its faces: game
    boxes, snack bags, storage boxes, the gift box, a cereal box, a shoe box, a suitcase.
  - `Skin(pieceName, nearest, slot, material)` — re-materials one slot of one placed piece.
  - `Piece(model, room, x, z, yaw, height, lift)` — `HouseSetPieces.Model` (made internal), for the
    clutter pieces the clusters need; no colliders.
  - `Dim(room, factor)` — pass 8: swaps the transplanted `Neon *` trim inside a room's bounds for a
    saved dimmer copy of the same material (`Neon Gold (dim)` …), so the perimeter drops to
    supporting brightness room by room without touching the prototype's materials.
- **Nothing under `Room Finish` carries a collider**, casts shadows or contributes to GI: it is probe-lit
  dressing on a lightmapped set. Measured 2026-09-28: away from a lamp's direct light a probe-lit rug
  gets only the night's ambient and reads black from above, so the pass's flat materials - rugs,
  bands, decals, labels - carry a low self-illumination from their own texture (`Finish`'s `glow`,
  0.2-0.4; the dark rug designs also a 1.6 base-colour lift), the guide's "emissive presentation"
  rather than a bake change. The NavMesh is therefore untouched, and so are the anchors. A slice
  that adds solid furniture (the game room's pool table, a bench at the HoH bed's foot) is the
  exception: it places an authored piece with its `_col`, then FitCollision → Resolve → Rebake, in the
  same run, and says so in its commit.
- Idempotent and saved: run twice, the scene is the same scene. Runs on `D:\GamesimNoUma` (synced from
  the worktree) by `-executeMethod`; the scene, the new materials and their metas are copied back
  (the "adding a mocap take" idiom, `Tools/` scripts as before).

### 4.4 Runtime pieces

- **Diary studio.** `HouseBrandingPalette` gains the pack-6 diary surfaces (`diaryPaddedWall`,
  `diarySlats`, `diaryEyeMask`, `diaryTally`, `diaryOnCamera`); `HouseBrandingAssets.Build` names
  them; `HouseBrandingTests.ThePaletteShipsOnlyTheArtItNames` lists them. `DiaryStudioBranding.Dress`
  puts the padded wall on `Upholstered interview wall` (its own instance material, tiled 4 × 2.5),
  two slat panels either side of it, the eye mask as a faint additive glyph above the halo's top ring
  (the head hides the halo's centre; the eye must not sit behind the head), the ON CAMERA sign and a
  red tally light beside the key.
- **Nomination screen idle.** `nomination_idle_display` is a quad 0.006 m off the board's face
  (inside the `ScreenSurface` stand-off, so a playing card covers it); `ScreenSurface` hides the quad
  named `Idle display` on its board while it shows and restores it after.
- **HoH reward station photos.** Static placeholders now; a runtime hook that drops the reigning
  HoH's portrait into the gold frame at the week's reveal is deferred (§9).

### 4.5 Verification

- **By capture, every slice.** `HouseLookCapture` with `-gamesimFocus "Room Finish/<room>"` from the
  overview angle and the PlayMode `CaptureFraming` shots (`walkthrough-*`, `hud-over-set-*`,
  `diary-branded`) are read, not reasoned about. A room slice is not done until its capture shows the
  fingerprint in §1's table without the room label.
- **`RoomFinishTests` (EditMode, preview scene):** the root exists with the eight room groups; no
  collider anywhere under it; every renderer under it is inside its room's floor bounds; every band's
  top is at or under its wall's height; every material under it lives in the pack-6 materials folder or
  is a saved glow copy; no renderer under it casts shadows or contributes GI; the `Set Pieces` rug
  pieces a `Rug` replaced are disabled, not destroyed; the interaction anchors' positions match a
  recorded list before and after (they must not move); the NavMesh triangulation vertex count is the
  committed one for every slice that added no collider.
- **Existing guards keep holding:** `AuthoredTextureTests`, `AuthoredAssetImportTests`,
  `HouseBrandingTests`, `UiPackImportTests`, `CeremonySeatingTests`, the diary branding PlayMode test
  (extended for the padded wall and the tally), and the full suites through the audited pipeline.

### 4.6 Landing

One commit per slice, in the README's order. Each: offline compile → NoUma EditMode + a filtered
PlayMode run → capture reviewed → commit. The audited pipeline runs per two or three slices (it is
seventy minutes and must run alone on D:), and a PR goes up when it is green. Floors in
`Tools/baseline.txt` rise with each slice's tests.

---

## 5. Room by room

Each room lists what lands in passes 2–6 (major furniture, materials, medium dressing, small clusters,
identity), what pass 7–8 touches, and what stays out. Positions are floor fractions (x, z in −0.5…0.5
from the room's centre) unless given in metres.

### 5.1 Diary room (Private room floor) — first, the strongest redesign

*Now:* a lounge — a floor lamp, plants, a bookcase, a design chair, a side table, a speaker, a round
rug — with the authored diary chair at (0, −0.10) and the studio built only during a visit.
*Target:* the instant the camera cuts here, a confessional studio; the chair is the only seat.

- **Materials:** `diary_radial_carpet` as a 3.6 m circular rug centred on the chair (the round rug
  piece disabled); `wall_charcoal_upholstered` band on the z 10 wall and the east wall (x 14), 1.5 m,
  tiled at 1.2 m; `diary_dark_wood_slats` band on the x 0 partition's diary side.
- **Medium dressing:** the bookcase and design chair *leave* (a second seat contradicts the room; the
  guide: "the game fantasy is isolation") — their `Set Pieces` renderers are disabled by the pass, not
  destroyed; two `bb_set_ph_plant` anchors flank the studio's position at (±0.16, −0.28); the floor
  lamp stays as the warm practical.
- **Identity (pass 6):** `confessional_wall_sign` (1.6 m wide) on the z 10 band, centred over the
  chair line; `private_sign` (1.0 m) beside the doorway on the x 0 partition; both as glow quads at
  low tint (0.9) so they read as lit signage, not neon.
- **Runtime studio (§4.4):** padded wall, slats, eye glyph above the halo, ON CAMERA sign, tally.
  The pack-5 halo and DIARY ROOM neon stay; the colour stays navy + violet + gold.
- **Chair:** the authored `bb_set_diarychair` is the hero already (brass frame, teal velvet,
  stone platform); its velvet slot is re-skinned `velvet_navy` and it gets a 1.2 m `dark_marble_detail`
  disc under it as a low dais (a flat quad, not a step — the seat anchor's approach must not climb).
- **Stays out:** studio camera silhouettes (a new `bb_set_studiocam` piece, §9); sofas of any kind.
- **Acceptance:** overhead, a circular carpet with one chair in a violet room; in the confessional
  shot, the padded wall and slats fill the frame edge to edge.

### 5.2 Kitchen / dining (Kitchen floor)

*Now:* the authored kitchen run along the z 0 partition, three appliances on it, the sixteen-seat
table with scanned chairs, three stools, clutter; a checkerboard of light tiles over the floor.
*Target:* a stone island dominant from overhead, an appliance wall with rhythm, a backsplash, clusters.

- **Major furniture:** no footprint change. The "island" is the counter run's end plus the stools;
  it is upgraded by material, not moved.
- **Materials:** `quartz_vein_overlay_1` over `counter_stone` on the run's counter top (per-instance
  material: base colour warm cream, the veins as the detail); `backsplash_marble_soft` band from the
  counter top (0.92 m) to the wall top (1.5 m) along x 1.0–7.4 on the z 0 partition; the checker tiles'
  `Kitchen Tile` tone stays but the pass tints it a shade cooler so the marble reads warmer than the floor.
- **Appliance wall (rhythm):** `appliance_panel_fridge` on the run's fridge door, `appliance_panel_oven`
  on the oven front, `appliance_panel_coffee` on the coffee machine — three 0.6 × 0.2 m decals that
  break "repeated module" into fridge / counter / cooktop / counter / oven.
- **Clusters (pass 5):** *coffee* (machine + two mugs + `coffee_label` carton) at the run's west end;
  *prep* (`bb_set_tray`, a `jar_label` carton, `bb_set_fruitbowl`) mid-run; *everyday* (bottles with
  `bottle_label`, `dish_towel_neutral` draped as a quad over the counter edge, `bb_set_plantsmall`)
  at the east end; *pantry* (`cereal_label`, `snack_label_red`, `snack_label_blue` cartons on the
  shelf end).
- **Dining:** `rug_living_navy_cream` at 5.0 m under the long table (the guide's rug), the fruit bowl
  as centrepiece; place settings stay out (invisible from the overhead camera).
- **Identity:** pack-5 `kitchen_sign` as a small glow quad on the east wall band; the perimeter neon
  along the run's wall dimmed (pass 8).
- **Stays out:** pendant lights (no ceiling; a pendant would hang from sky) — the run's under-counter
  glow strip is the practical instead.
- **Acceptance:** overhead, a pale stone run against a marble band and a navy rug under the long table.

### 5.3 Living room (Living room floor)

*Now:* the scanned sofa on the east side facing west, two armchairs, a coffee table, a 3.2 m flat rug,
lamps, speakers, the memory wall on the west wall, a bookcase on the north band.
*Target:* one deliberate conversation arrangement on a large rug, one feature wall.

- **Proportions (pass 1):** the sofa/armchair/coffee-table triangle tightens by 0.4 m so the rug binds
  it; the sofa is not rescaled this slice (its seats are anchors; rescaling waits for §9).
- **Materials:** `rug_living_teal_geo` at 4.4 m under the seating group (the flat rug disabled);
  `fabric_navy_weave` re-skin on the two armchairs' cushions; `leather_dark` on the sofa's Poly Haven
  material stays as scanned (it already reads as leather).
- **Feature wall (north band, z 0 partition, x −11…−3):** `wall_warm_oak_slats` band 1.5 m tall behind
  where the eviction screen clone stands (`CeremonySets` puts it at `televisionX`); two vertical glow
  strips (0.06 × 1.4 m) either side; the console (`sideTableDrawers`) and lamp stay in front of it.
- **Coffee-table cluster:** `bb_set_bookstack`, `bb_set_candle`, `bb_set_tray`, a `bb_set_plantsmall`.
- **Medium:** two `frame_cluster_2` art groups (`wall_art_01`, `_05`, `_09`) on the south band (z −10
  link wall, 1.1 m — art centred at 0.7 m), `bb_set_ph_plant` in the SE corner, cushions on the sofa.
- **Identity:** pack-5 `gamesim_house_geometry` at 0.6 m on the oak band, dark tint, as the wall's
  single mark; the memory wall keeps the west wall to itself.
- **Conversation anchors (pass 7):** none added — the sofa seats and the standing groups already
  exist as venues; the rug now makes those formations read.
- **Acceptance:** overhead, a teal rug with a sofa, two chairs and a table on it, an oak wall behind.

### 5.4 HoH suite (HoH floor)

*Now:* the authored HoH bed, door, basket, a sofa and chair, a coffee table, TV console, bookcase,
the ensuite along the east wall. *Target:* status — dark wood, cream, muted green, brass; a reward
station.

- **Materials:** `duvet_hoh_cream` on the bed's `linen_white`, `headboard_cream_channels` on its
  headboard (`velvet_teal` slot), `throw_blanket_hoh` as a folded quad across the foot;
  `hoh_luxury_geometric` band on the z −10 link wall behind the headboard (1.1 m, x −13.5…−5.2);
  `rug_hoh_cream_gold` at 3.6 m under the lounge (the flat rug disabled); `velvet_green` on the sofa
  and chair cushions; `wall_brass_inlay` band on the west wall.
- **Reward station** (on the TV console and the wall above it, z −20 wall, 1.1 m): `reward_plaque`
  (0.7 m) at 0.85 m, `frame_hoh_gold` with `photo_placeholder_1..4` as a row of four 0.28 m frames at
  0.6 m, `hoh_envelope` and `hoh_letter_sheet` as a flat quad pair on the console top, `gift_wrap_gold`
  carton, `snack_bag_gold` and `snack_bag_blue` cartons, a `drink_label_hoh` bottle, a `mini_fridge_graphic`
  carton (0.5 m) beside the console; the existing basket moves from the coffee table onto the station.
- **Identity:** pack-5 `hoh_crown_wall` at 0.5 m on the geometric band over the headboard, gold tint;
  pack-5 `neon_hoh_suite_mask` as a glow sign beside the door (the ASSET-PACKS destination).
- **Practicals:** the two table lamps stay; the perimeter neon inside the suite dims (pass 8) so the
  gold reads.
- **Stays out:** a bench at the bed's foot (solid; needs the collision run — with the pool table, §9).
- **Acceptance:** overhead, a cream bed on a cream-gold rug in a dark-wood room with a gold row on
  one wall.

### 5.5 Nomination room (Nomination floor)

*Now:* the ceremony board on its stage at the south wall, a round table with six chairs on a 4.2 m
round rug, lamps, plants, speakers. The cut scenes ring the house around the table at the first
ceremony. *Target:* the display looks installed; everything faces it.

- **Display framing:** `nomination_presentation_wall` band on the south wall (z −20, 1.1 m) behind the
  board, x −3.5…3.6; `gold_linework_mask` as a glow quad 3.0 × 1.7 m directly behind the board so the
  lines show around its edge; `screen_frame_ornament` (1.4 m) above the board's top edge; two
  `bb_set_ph_plant` either side of the stage; `subtle_wall_emblem` at low tint on each divider band.
- **Idle state:** `nomination_idle_display` quad on the board's face (§4.4).
- **Floor:** `ceremonial_floor_ring` at 4.6 m replacing the round rug (disabled), centred on the table
  so the cut scene's ring of chairs stands on the ring.
- **Seating:** the six chairs already face the table; the cut scene turns the ring toward the board.
  No saved-scene rearrangement this slice (it would move the two dining-chair anchors).
- **Stays out:** the portrait frames (`hoh_portrait_frame`, `nominee_portrait_frame`) — they belong on
  the cut scene's world-space card, a `KeyCeremony`/`VoteReveal` skin, §9; the reveal pulse, §9.
- **Acceptance:** overhead, a lit board in a gold frame with a ring of chairs on a ringed floor.

### 5.6 Game room (Games floor)

*Now:* sofa corner + coffee table + rug, TV console on the north wall, bar and three stools, desk with
laptop, two have-not cots, bookcase, lamps. **No pool table exists.** *Target:* three zones, playful,
less neon.

- **Zones as they are:** TV/lounge (NW), bar (east), desk (SE), have-not (south). The pool zone the
  guide assumes needs an authored `bb_set_pooltable` (2.4 × 1.3 m, felt top, cue rack) at (−0.20,
  0.18) — solid furniture, so it lands with the collision run (§9), not in this slice.
- **Materials:** `rug_game_playful_circle` at 3.2 m under the lounge (flat rug disabled);
  `fabric_teal_weave` on the corner sofa's cushions; `wall_charcoal_geometric` band on the west divider.
- **Decor:** `arcade_marquee_game_on` as the *one* glow marquee (1.6 m) over the bar on the east wall
  band; `trophy_plaque` (0.6 m) over the bookcase; `boardgame_strategy`, `boardgame_alliance`,
  `cardgame_social` as a stack of three cartons on the coffee table; `boardgame_vote`, `boardgame_bluff`
  on the bookcase shelf; `game_room_poster_1` and `_2` framed (`frame_portrait_single`) on the south
  wall band at 0.6 m; `mini_fridge_graphic` carton beside the bar; pack-5 `game_room_pattern` at low
  tint beside the TV console.
- **Neon:** the games room's perimeter trim dims by 0.6 (pass 8) — "neon −30–40%" — leaving the
  marquee as the lit focal.
- **Acceptance:** overhead, a circle rug with a sofa and stacked boxes, a bar under one marquee.

### 5.7 Shared bedroom (Bedroom floor)

*Now:* two bunks on the west wall, three singles on the z 10 wall with drawers between, side tables,
lamps, a coat rack, a bookcase, a 3 m flat rug. *Target:* several people clearly live here.

- **Bed clusters** (each bed: bedding + headboard + storage + one possession): singles get
  `duvet_navy_diamond`, `duvet_blue_stripe`, `duvet_cream_grid` with matching `pillow_*_geo` and
  `headboard_navy_channels`; the bunks get `duvet_green_plain` / `duvet_rose_plain` and
  `pillow_green_geo` / `pillow_rose_geo`; at each foot a `storage_box_label` or `shoe_box` carton.
- **Storage wall:** the bookcase on the east wall plus a `laundry_basket_label` cylinder, a
  `luggage_tag` suitcase carton by the coat rack, a `toiletry_label` and `cosmetic_label` pair on the
  drawers.
- **Textile field:** `rug_bedroom_neutral_shag` at 5.2 m across the middle, joining the beds;
  `bedroom_geometric_blue` band on the z 10 wall behind the singles, `bedroom_geometric_rose` band on
  the west wall behind the bunks.
- **Wall:** one `corkboard_background` (1.0 m) with three `polaroid_template` quads on the east wall
  band; `frame_cluster_3` with `wall_art_03`, `_07`, `_11` on the south partition; pack-5
  `bedroom_botanical` in the existing `Frame - Bedroom West` (its own material).
- **Possessions:** `book_cover_blue/red` cartons on the side tables, a `magazine_cover` quad on a bed,
  `water_bottle_label` bottles, a `phone_screen_idle` quad on one pillow, `headphone_case` carton.
- **Acceptance:** overhead, five beds in four colours on one big rug, boxes at their feet.

### 5.8 Competition yard (Competition yard floor)

*Now:* three lit lanes, gates, stacks, crates, podiums, the backdrop with three signs, the pool, hot
tub, loungers, planting, the roped entrance. *Target:* a television competition stage with a
permanent perimeter.

- **Perimeter (pass 4):** four `bb_set_lighttower` pieces (new authored: tripod truss, lamp head,
  a glow lens — §9 lists the script) at the fence corners; four `hazard_stripe_decal` (pack 5) flight
  cases as cartons by the west fence; two benches (`bb_set_lounger` turned) by the east fence.
- **Hero wall:** the backdrop already carries the signs; the pack-5 `display_idle_gamesim` becomes
  the backdrop's centre display quad (2.4 m), with the competition's `display_hoh_competition` /
  `display_veto_competition` swapped in by the arena at runtime (§9).
- **Floor:** pack-5 `start_marker` at the podium end and `finish_marker` at the gate end of each
  lane as static decals; per-competition station decals stay runtime (`CompetitionArena`, §9).
- **Acceptance:** overhead, lanes, a display at the head, towers at the corners; the centre still open.

### 5.9 Quiet lounge

The guide's §9 applies "if this room is separate from the primary living room". It is not: the
"private room" is the diary room, and §5.1 empties it of seating on purpose. No quiet lounge is built.

---

## 6. The shared material library

The guide asks for one family instead of a colour per prop. Mapped onto what exists plus pack 6:

| Family | Members | Source |
|---|---|---|
| Floors | dark walnut, warm oak, neutral tile, lawn | `bb_mat_floor_*` (Poly Haven) — unchanged |
| Metals | matte black (`ink`), brushed brass (`frame_brass`), steel (`steel_appliance`) | authored — unchanged |
| Upholstery | cream, navy, teal, rose | pack 6 `fabric_*_weave`, `velvet_*`, `leather_dark` |
| Walls | deep navy (shell), warm oak slats, dark slats, charcoal geometric, brass inlay, the per-room patterns | shell + pack 6 `WallTreatments` |
| Stone | counter quartz, dark marble | `counter_stone` + `quartz_vein_overlay_*`, `dark_marble_detail` |
| Textiles | rugs, duvets, pillows, throws | pack 6 `Rugs`, `Bedding` |
| Light | signs, strips, marquee, tally, pulse | glow copies of `bb_mat_branding_glow` |

Room identities inside the family: diary = navy + violet + gold; HoH = dark wood + cream + green +
brass; kitchen = cream stone + marble; living = teal + oak; games = teal + playful; bedroom = navy /
blue / rose / cream; nomination = navy + gold; yard = green + neon lanes.

---

## 7. Neon and light balance (pass 8)

- The transplanted trim (`Neon Gold` ×144, `Neon Blue`, `Neon Red`, `Neon Yellow` under `Broadcast
  Dressing`) is the perimeter. `Dim(room, factor)` swaps the trim renderers inside a room's bounds to
  `<Neon> (dim)` copies at that factor: diary 0.5, HoH 0.5, games 0.6, bedroom 0.6, kitchen 0.7,
  living 0.8, nomination 0.8, yard 1.0. Doorway runs keep their full brightness so the cast still
  reads the gaps.
- Nothing is added to the bake. The new practicals are emissive quads; the diary's wash and the
  interview lights are runtime, as before.
- Each room's "one focal light" is its identity piece: the diary halo, the kitchen backsplash strip,
  the living oak wall strips, the HoH plaque, the nomination linework, the games marquee, the
  bedroom corkboard, the yard display.

---

## 8. Order and status

| # | Slice | Contents | Status |
|---|---|---|---|
| 0 | Import | pack 6 into the catalogue, `WorldTile`, markers under Editor/Gizmos, tests | built 2026-09-28 (`bb_room_pack6.py`; 174 files, 36 tiles, 2 masks) |
| 1 | Diary | §5.1 scene + runtime studio | built 2026-09-28: `HouseRoomFinish.DiaryRoom`, the studio's padding, slats, sign and tally; capture reviewed (the halo from above, the signs, the plants; the prototype's confessional chairs and table hidden with the lounge) |
| 2 | Kitchen | §5.2 | built 2026-09-28: `HouseRoomFinish.Kitchen` - the run's top in marble, the backsplash band from the counter to the wall's top, three appliance panels, coffee and pantry clusters and a towel on the counter, the navy rug under the long table, the pack-5 KITCHEN sign on the east wall; capture reviewed (the backsplash line, the rug and the sign read from above; the panels and the clusters are counter-scale, for the room camera) |
| 3 | Living | §5.3 | built 2026-09-28: `HouseRoomFinish.LivingRoom` - oak slat bands on the north partition, a lit strip either side of the screen's place, the house mark, the rug between the sofa and the coffee table (the neutral abstract: the teal read as a black patch from above under the room's light), the table's cluster, two framed prints on the south wall, a corner plant; the seats stay where their anchors are, so the guide's tighter conversation group waits for the collision slice (§9) |
| 4 | HoH | §5.4 | built 2026-09-28: `HouseRoomFinish.HohSuite` - the bed in cream (its duvet and headboard share one authored material) with a throw at its foot, the luxury geometric and brass inlay bands, the cream-and-gold rug under the lounge, the reward station on the television console and the wall above it, the crown, the HoH neon by the door; capture reviewed: the rug, the lounge and the neon read from above, but the suite is lit cyan by the perimeter trim so the cream bed reads blue-grey - the §7 dimming is what fixes that, not another texture; the bench at the bed's foot waits for the collision slice (§9) |
| 5 | Nomination | §5.5 + the idle display | built 2026-09-28: `HouseRoomFinish.NominationRoom`; the idle display hangs under the screen prop and `ScreenSurface` hides it under a card, and `ScreenSurface` now measures the board as its glow sub-mesh (the old fallback measured the whole set); captures reviewed: the ring under the table and the NOMINATIONS display on the face read from above |
| 6 | Games | §5.6 | built 2026-09-28: `HouseRoomFinish.GameRoom` (no pool table: it needs a piece and the collision slice, §9); capture reviewed: one marquee, the sofa on its circle rug, the boxes on the table |
| 7 | Bedroom | §5.7 | built 2026-09-28: `HouseRoomFinish.Bedroom` with `SkinEach` for one duvet per bed; capture reviewed: five beds in their colours, boxes at their feet, the rug between |
| 8 | Yard | §5.8 | built 2026-09-28: `HouseRoomFinish.CompetitionYard` (the display, the lane lines, three cases; no towers yet, §9); capture reviewed: the cases by the west fence, the display under the signs, the centre open |
| 9 | Balance | §7 dimming, the final capture sheet, `ROOM-FINISH-PLAN.md` status | built 2026-09-28: `Dim` with `NeonBalance`, dimmed copies of the prototype's neon materials as assets, and the pass's flat dressing self-lit (§4.3). What the dimming cannot do: the trim's cyan **bounce is baked into the lightmaps**, so the suite's cream bed still reads blue-grey until the house is baked again - a rebake is its own slice (§9), because it rewrites the lighting assets the review candidate audits |

Estimate: half a day for slice 0, half a day to a day per room, with the diary and the kitchen the
longest. Captures are the clock: a slice whose capture does not show the fingerprint is not done.

---

## 9. Not built, and why

- **Solid furniture:** the game room's pool table (`bb_set_pooltable`), the HoH bench, the light towers
  (`bb_set_lighttower`) and studio cameras (`bb_set_studiocam`) are new authored pieces; the first two
  need the collision run and a NavMesh rebake in their own slice, verified by the navigation audit.
- **Furniture rescale:** the guide's 10–15% larger living-room seating changes seat anchors and the
  cut scenes' sofa measurements; it waits until the free-time and ceremony tests have run on the
  finished room.
- **Dynamic content:** the HoH station's photos, the yard display's per-competition image, the
  station decals per competition, the reveal pulse under the hot seats, the nominee/HoH portrait
  frames on the ceremony card — all runtime, all keyed to episode state, each a small feature with its
  own test.
- **Dressing at season start:** the nomination ring is still dressed at the first ceremony
  (`CEREMONY-CUTSCENES-PLAN.md` §6); the ceremonial floor ring is placed under where it will stand.
- **Editor markers as gizmos:** `HouseInteractionAnchorAuthoring` drawing the pack's SIT/COOK/OBSERVE
  icons at each anchor is a development aid; the files are imported for it, the gizmo is not written.
- **The rebake:** the perimeter trim's bounce was baked at full brightness, so a room dimmed to a
  half still carries the old cyan in its lightmaps (the HoH bed). Re-running
  `HouseCinematicLighting` with the dimmed materials and the finish in place is minutes on the GPU
  and rewrites the lighting assets; it lands as its own slice, verified by the audited pipeline.

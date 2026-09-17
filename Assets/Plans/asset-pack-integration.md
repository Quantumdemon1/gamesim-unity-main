# Asset pack integration

Moving the house's look onto the Asset Store packs, and why Kenney stays in the repository anyway.

## The decision

**STYLARTS Stylized House Interior becomes the house's look.** Kenney stops being what the shipping
set is made of.

## The constraint that shapes everything

Kenney cannot actually be deleted, and the reason is not sentiment.

This repository is **public**, and the Asset Store EULA does not permit redistributing purchased
assets. So none of the eight packs can be committed — they have to be gitignored exactly the way
`Assets/UMA/` already is. Which means the project has to run in two states: with the packs, and
without them.

Without a fallback, a clone would open to a house with no furniture in it at all. So:

> **"Kenney retired" means retired from the shipping look, not removed from the repository.** It
> becomes the bottom tier of a fallback chain — the thing a clone without the packs falls back to,
> the same way a clone without UMA falls back to the authored character prefabs.

This is the seam the project already has, applied a second time. It is also the reason the work
splits cleanly into what can be done now and what waits on a download.

## Nothing is downloaded yet

The eight packs are licensed to the account but are **not on this machine**. The Asset Store cache
holds only Bakery, UMA 3, 3D Game Kit and Free Ui Pack.

That is the first step, and it is a manual one: **Package Manager ▸ My Assets ▸ Download, then
Import**, per pack. Import each into `Assets/ThirdParty/<Publisher>/` so one gitignore rule covers
the lot.

## The packs, and what each is actually for

| Pack | Size | Style | Verdict |
| --- | --- | --- | --- |
| STYLARTS **Stylized House Interior** | 2.1 GB | Stylized PBR, textured | **The base look.** |
| MORPHARA **Low-Poly Furniture Kit** | 64.8 MB | Flat low-poly | Fallback tier above Kenney |
| IZI **Low Poly Furniture Pack** | 19.1 MB | Flat low-poly | Fallback tier above Kenney |
| 3DIGITALIS **Low Poly Interior Props** | 14.1 MB | Small clutter | Clutter pass — see below |
| NAPPIN **House Interior — Free** | 73.1 MB | Semi-realistic | Kitchen appliances |
| NAPPIN **Office Pack — Free** | 26.8 MB | Office props | Diary room clutter |
| JUSTCREATE **Low Poly Cartoon House Lite** | 3.0 MB | Cartoon | Filler |
| TRIDIFY **HDRP Furniture Pack** | 31.6 MB | HDRP | **Skip.** See below. |

**The Tridify pack is unusable as delivered.** This project is URP; that pack is HDRP. Its materials
reference HDRP shaders that do not exist here, so every model renders magenta. Making it work means
rebuilding each material against URP/Lit by hand, and both low-poly packs cover the same ground for
no work. It is excluded until there is a reason to spend that.

**The clutter pass is worth more than its file size suggests.** The single biggest reason a dressed
room still reads as a greybox is that real rooms are covered in small objects nobody placed on
purpose — mugs, books, cables, bottles. Kenney has almost none. 3DIGITALIS is 14 MB and fixes it.

## The architecture: a catalogue seam

Today `HouseSetPieces.Plan` names Kenney files directly:

```csharp
new Prop("Kitchen floor", "kitchenStove", -0.30f, 0.40f, 180f, 0.95f),
```

That has to stop pointing at one vendor. The plan should say *what* a prop is, and a catalogue
should decide *which file* that resolves to:

```
"stove"  ->  ThirdParty/STYLARTS/.../Stove.prefab        (if imported)
         ->  ThirdParty/MORPHARA/.../stove.prefab        (if imported)
         ->  Art/External/KenneyFurniture/kitchenStove.glb   (always present)
```

First path that loads wins. Consequences worth having:

- A clone with no packs still builds a furnished house.
- Swapping the house's whole look is a catalogue edit, not 132 plan edits.
- Per-prop overrides are possible — a hero prop can come from STYLARTS while its neighbours do not.
- **An audit menu item reports which pack each prop resolved to**, so "is this still Kenney?" is a
  question answered by a list rather than by squinting at a screenshot. The character audit already
  works this way and it earned its keep.

## Phases

**Phase 1 — the seam.** `HouseCatalogue`, plan entries moved to logical ids, gitignore rules,
README section mirroring the UMA one, the resolution audit. Works today against Kenney alone.
*Not blocked.*

**Phase 2 — the Big Brother set pieces.** None of these exist in any of the eight packs, and all
four are composed geometry or re-plans of what is already here. *Not blocked.*

- **Pool and hot tub** in the competition yard. The most recognisable thing about that backyard and
  the thing no furniture pack ships. Built the way the podiums were: basin, coping, water plane,
  jets, a seating ring on the tub. Loungers around both.
- **Long dining table** in the kitchen, replacing the round table and its four chairs. One long
  table, one chair per houseguest. It is where this format does most of its arguing.
- **Diary room hero chair** in the private room, lit separately, with Office Pack clutter on the
  camera side — so the room reads as the Diary Room and not a spare sitting room.
- **Have-Not room** in the game room's south half: cold palette, harsh sleeping surfaces. Worth
  saying plainly — **the simulation has no have-not mechanic**, so this is set dressing with no
  rules behind it. It will look like the show and do nothing.

**Phase 3 — the swap.** *Blocked on the download.* Map catalogue ids onto STYLARTS prefabs,
re-check every prop height, re-tune the broadcast palette against textured art.

**Phase 4 — clutter.** 3DIGITALIS props on counters, shelves and tables.

**Phase 5 — verify.** Rebuild, re-measure build size, re-run all three suites, re-capture.

## Risks, in the order they are likely to bite

**The cutaway walls are the real problem with this choice.** The house walls top out at **1.5 m**,
and the south wing at **1.1 m**, because it is a doll's-house set for an overhead camera. STYLARTS
is a *house interior* pack — authored for full-height rooms with ceilings, walls and tall furniture.
Expect a large fraction of it to be unusable for that reason alone, and expect most of what is
usable to need height-capping. The furnishing pass already cut four Kenney models on exactly this
rule.

**The pipeline is unverified.** Whether that pack ships URP materials is not knowable until it is on
disk. Built-in is recoverable (URP has a material upgrader). HDRP would be the Tridify problem again
at 2.1 GB.

**Build size.** The player is already 871 MB with UMA. Unity ships only referenced assets, so the
2.1 GB of source does not all land in the build — but the textures behind every prop that *is* used
do, and stylized PBR art carries far heavier textures than Kenney's untextured meshes.

**The palette will fight back.** `HouseBroadcastPalette` darkens the shell so the neon reads. That
was tuned against flat untextured surfaces. Textured art with its own baked lighting will not
respond the same way.

**Navigation is unchanged and still a compromise.** Props carry no colliders because the NavMesh is
baked from collision. A houseguest walks through the stove. More furniture does not make that worse,
but it does not fix it either.

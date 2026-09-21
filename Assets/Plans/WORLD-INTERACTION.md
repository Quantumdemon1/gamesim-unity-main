# Collision, clicking and sitting

Three symptoms — actors walking through sofas, clicks that do nothing, bodies sitting wrong — with
three different causes and one shared root: **the house is described by authored anchors, and
almost nothing in it is described to physics.** Everything below is measured against the committed
scene, not estimated.

## 1. What is actually wrong

### Collision barely exists
262 standing objects checked against the live NavMesh: **126 an agent walks straight through, 123
of those have no collider at all.** The furniture that *does* block does so by accident — a
grey-box primitive from the U02 prototype happens to stand there, renderer off, collider on. So the
living-room sofa blocks and `loungeDesignSofa` does not; the kitchen island blocks and `kitchenBar`
does not; the two double beds block and the bunks, the singles and the HoH bed do not. The entire
authored dining set, table and sixteen chairs, is walk-through.

The NavMeshSurface bakes from `PhysicsColliders`. Those 22 invisible boxes are load-bearing: they
are the only reason any furniture blocks at all.

### Clicking cannot reach furniture
One place reads the mouse for the world: `HousePlayerController.Update` (`:134-185`). It can reach
a floor (`HouseWalkable`) and a houseguest (`HouseNpc`). Nothing else.

A click on the visible sofa passes through it — all 81 set-piece FBX metas carry `addColliders: 0` —
and lands on the invisible grey-box behind it, which carries no anchor, no `HouseWalkable` and no
NPC. The branch falls through, `Update` returns, and there is no feedback of any kind. Roughly
**86 m² of floor plan is dead click-space**.

`HouseFurniture.AtProp` can never return non-null from a real mouse hit, for two independent
reasons. It filters through `TryDescribe`, which knows only `kitchen-counter-activity`,
`kitchen-table-chat` and `yard-lounger-chat` — `private-diary`, `competition-entry` and
`episode-screen` return `false` and are invisible to it. And the anchors that do survive parent to
FBX prefabs with no colliders anywhere in their hierarchy, so no ray can produce a transform under
them. **The diary chair is unclickable twice over.**

The click-through guard is alpha-blind in both directions. Too much is blocked: the Status band
(full width, 64 px) and the Interaction prompt swallow clicks while carrying no control, because
`EpisodeHud.Panel` sets `raycastTarget = true` unconditionally. Too little is blocked, and this one
is live: `KeyCeremony` draws a 0.975 full-screen scrim with `blocksRaycasts = false`, and
`CeremonyTakeover` deliberately carries no `GraphicRaycaster` — so **a click on a ceremony card you
cannot see through reaches the house behind it and walks the player**.

### Sitting is unaligned because the alignment is dead code
`anchor.SeatContact` is read only inside `if (animator.isHuman && animator.avatar != null)`
(`HouseSeatPresentation.cs:87`). The shipped cast imports as **Generic**
(`AuthoredAssetImporter.cs:71-75`; Quaternius source is `animationType: 2`), so `isHuman` is false,
`hips` and the foot bones are never assigned, and `:101-114` never execute. **`seatHeight` has no
effect on the cast HEAD ships.**

What runs instead is `Vector3 offset = anchor.Position - transform.position` applied to
`VisualRoot` — the body's *floor* origin — where `anchor.Position` is on the room floor under the
prop. So the body is placed as if standing under the chair, and the entire seated pose comes from
the last frame of one baked `SitDown` clip, used identically for a dining chair, a lounger and the
diary throne.

The mismatch is **fixed seat versus variable prop**, not variable body. `seatHeight` is metres in
the anchor's own frame and deliberately does not scale, while `HouseSetPieces` scales every prop to
a target overall height. The dining chair is Poly Haven's GreenChair_01, 1.0583 m native with its
pan at 0.45–0.47, scaled to 0.95 m — so the real cushion lands at **0.413 m** while the anchor
insists on **0.460 m**. Authored cushion heights are 0.52 (dining), 0.44 (lounger), 0.42 (diary);
`SetSeatHeight` is called from exactly one place in the repository. **The diary sitter is told to
float 4 cm above the velvet and the lounger to sit 8 cm below it.**

Sofa, bed and barstool have no seated anchors at all. The barstool's seat is at 0.78 m, past
anything a 0.46 default could serve.

### The two body pipelines are incompatible in the worst possible way
- **Generic (what HEAD ships):** a real `SitDown → SitIdle → StandUp` chain, and no way to align to
  the seat, because the fit requires a Humanoid avatar.
- **Humanoid (UMA, behind a define that must never be committed):** can align to the seat, and has
  **no sit-down take at all** — `SeatedFade = 0.35f; // no sit-down take on this rig`.

The path that can align has no animation; the path that has the animation cannot align. There is no
IK on either: `m_IKOnFeet: 0` on every state of both controllers, no `OnAnimatorIK` anywhere, and
`com.unity.animation.rigging` is not in the manifest.

## 2. The recorded blocker, corrected

`HouseNavigationObstacles.cs:48-61` records that fitting obstacles to everything solid "took
seventeen PlayMode tests down at once". That is real, and the cause is worth keeping: carving
removed the floor beneath seat positions the director computes at runtime, so houseguests could not
bind to the mesh. The blast radius today is 53 PlayMode tests across the seating and motion files.

**The second recorded blocker is stale.** Commit `a0ad5f1` recorded "a bake is exactly what this
house cannot currently survive" at 21 of 28 room pairs — and `145244e`, 98 minutes later, pulled
two dividers out of the doorways they were standing in and reached 28 of 28. A non-destructive
trial bake against today's tree confirms **28 of 28, identical to the committed asset**. The note
was never updated.

And the measurement inverts the original approach: **a bake is strictly better than
`NavMeshObstacle` carving.** A box obstacle removes a whole bounding box; a bake voxelises real
geometry at 0.167 m cells and leaves the space between chair legs walkable.

## 3. The plan

Stages are ordered so that each is green before the next begins, and so that the change that can
break seating comes *after* the thing that protects it.

### Stage 0 — correct the record (S)
Update `HouseNavigationObstacles.cs` and `VISUAL-TARGET.md` so nobody plans around a blocker that
was fixed two days later. Costs nothing and prevents the next person repeating this survey.

### Stage 1 — move the four approach points, before any bake (M)
Measured approach-to-prop-surface: `yard-lounger-chat` slots 0 and 1 are at **0.00 m — inside the
lounger**; `kitchen-table-chat` 0.25 m; `private-diary` 0.45 m; `kitchen-counter-activity` 0.80 m;
`episode-screen` 1.40 m. Bake erosion at agent radius 0.5 eats the close ones, which is precisely
how carving broke seating the first time. **This must land and be green before Stage 3.**

**What Stage 1 found that Stage 3 has to answer.** Four of the five approaches reach a bake-safe
0.60 m. The two anchored dining chairs cannot, and the reason is where they stand rather than what
number is chosen: they face the table, their sides are the neighbouring chairs 0.64 m away, and the
floor behind them runs out — 0.90 m puts one of the pair off the mesh entirely, at which point the
meeting coordinator (`HouseMeetingCoordinator.cs:332`, a 0.25 m sample tolerance) cannot reserve the
chairs and `NpcRuntime_ASavedTableMeetingReunitesSeatedAndFacingTheChairs` fails. They sit at 0.70,
which is the best the room allows and still short of the agent radius.

There is a trap in here worth naming, because it caught this survey. Measured against today's mesh a
side approach looks fine at 0.03 m — but only because chairs have no colliders, so the NavMesh does
not know they are there. The moment Stage 3 gives them collision, a side approach walks into the
next chair. **Any approach point derived before the furniture is in the mesh is provisional**, and
Stage 3 must re-derive all of them once it is. For the dining pair that likely means re-anchoring to
chairs with room behind them, or moving the table, rather than choosing a different offset.

### Stage 2 — two layers, which the project has never used (S)
`TagManager.asset` defines only the five builtins; layers 8–20 are empty. Give prop collision its
own layer. The NavMeshSurface mask is `0xFFFFFFFF` so the bake still sees it, while clicking,
conversation sightlines, body clearance, seated-NPC picking and camera pull-in all skip it. This is
what lets collision and picking stop fighting each other.

### Stage 3 — give the authored props collision, and bake (M) — **done**
**126 of 262 walk-through became 42, and every room still reaches every other room.** The NavMesh
asset went from 21 KB to 39 KB, which is the furniture arriving in it.

Not by the `*_col` route in the end. That pipeline is real and still preferred where an export
already has a collision child — the pool and the hot tub keep theirs, moved onto layer 8 — but it
needs a Blender round trip per prop, and the answer for 119 props was a box fitted from their own
renderers. `HouseFurnitureCollision` hangs that box on a child called `Collision` rather than on the
prop root, because a layer is not only a physics fact: cameras cull by it and lights list it. The art
stays where it was authored and the proxy carries the physics, and deleting every `Collision` puts
the house back exactly as it was.

**What the rule is.** Floor-standing, at least 0.30 m across its narrower horizontal side, at least
0.40 m tall. That keeps the cot (0.52) and the coffee tables (0.42) and leaves the rugs (0.03), the
cable run (0.04), the competition circle (0.05), the speaker poles (0.24) and every mug and candle
on a worktop alone.

**Three things this stage got wrong first, all worth keeping.**

*The thresholds are metres; the box is not.* The box is measured in the prop's own space so it turns
with the prop — a bookcase at 45 degrees stays 0.32 m deep instead of reading 0.79 m in both
directions. But the first version judged the local numbers too, and in local space a unit cube
squashed to 0.02 m still measures 1.0. Eighteen kitchen floor tiles and six stanchion poles grew
waist-high collision. Scale first, then decide.

*Furniture on a room marker is the collider version of the carving disaster.* The nomination room's
marker sits **inside its own round table**, with four chairs 0.47 m away. Giving the table a body
took the floor out from under the point every route into that room ends at. `HouseNavigationObstacles`
already knew this and keeps 1.5 m clear; that radius is far too generous here, because it would spare
the HoH bed at 1.16 m and the diary chair at 0.45 m — the two props this stage exists to make solid.
Containment only: a prop may not stand *on* a destination.

*A guard that can cry wolf is worse than no guard.* `Gamesim/Rebake` refused a mesh that reached all
28 room pairs, reporting 21, because `ReachablePairs()` read whatever NavMesh data happened to be
registered and a `BuildNavMesh` leaves that state depending on what the caller did first. Both
readings now name the mesh they are measuring.

**Doorways are resolved by measurement, not by a list.** A prop standing in an opening is not
distinguishable by size or name from the same prop against a wall — the HoH door reads as a 3.16 m
sideboard. `HouseDoorwayResolver` bakes, asks which rooms it just lost, walks the committed routes
into them to find the boxes in the corridor, switches them off one at a time until the house joins
up, and then gives each one back to find the smallest set that has to stay off. It found **4 of 119**:
`bb_set_hohdoor`, one `bookcaseOpen` in the yard doorway, and two `chairModernCushion` at the
nomination table. Each keeps its box, switched off, with the room it was costing written into its
name — so the next person to wonder why that bookcase is walk-through finds the answer in the
hierarchy. A re-fit switches them back on and makes them earn it again.

`bb_shell_house` is excluded by name, permanently: a render-mesh bake — what "collide everything"
looks like — comes back at 21 of 28 pairs, every missing pair something-to-Yard.

**Still open from this stage.** The approach points have to be re-derived now that the furniture is
in the mesh, which Stage 1 warned about and nothing has done yet. The U02 grey-boxes have not been
retired. And the 42 survivors include the `Broadcast Dressing` decor — two foliage balls, a framed
picture, a dining table's four legs and two stools — which this pass never walked, because it only
knows the set-piece root.

### Stage 4 — make the click land (M)
With props on their own layer and colliders on the visible geometry, `AtProp` can finally receive a
real transform. Then: widen `TryDescribe` past three venues so the diary chair, the competition
entry and the episode screen are reachable; give hover a highlight; and give a dead click feedback
instead of silence.

Fix the guard in both directions while here — stop transparent chrome swallowing clicks, and stop
ceremony scrims letting them through to a house the player cannot see.

**The world click is a second route to the same commands, never a replacement.** Every ceremony is
committed today by a captioned button, and those captions are what the tests and screen readers
hold. A game that needs a mouse to nominate somebody is a worse game than one that takes both.

### Stage 5 — sitting, cheapest correct first (M, then L)
1. **Author the real cushion heights.** They are known from the Blender sources and currently
   ignored. This is arithmetic, not animation.
2. **Make `seatHeight` survive prop scaling** — the anchor is metric by design while the prop is
   scaled to a target height, and nothing reconciles them.
3. **Resolve the pelvis without a Humanoid avatar.** The Quaternius rig exposes named bones, so a
   name-based lookup makes the existing fit work on the pipeline that actually ships, with the
   `isHuman` path preferred where available. This turns `SeatContact` from dead code into the thing
   it was written to be.
4. **Only then** consider Animation Rigging and foot IK. It is the right long-term answer and it is
   not available to a Generic rig, so it is a decision about the cast, not about seating.

Give sofas, beds and barstools seated anchors once the heights are honest.

## 4. What protects this

The existing seating and motion suites are the contract — 53 PlayMode tests across
`EpisodeDiaryRoomPlayModeTests`, `HouseNpcMotionPlayModeTests`,
`HouseMeetingCoordinatorPlayModeTests`, `EpisodeHouseActivityPlayModeTests` and
`CompetitionAssembly`. They may be rebased to new geometry; they may not be loosened.

Two gaps worth closing as the work lands, because they would let a regression through silently:
`VisualFeet` forces Y to the navigation root, so the one test watching seated placement cannot see
a vertical error at all; and no test asserts that a click on a prop reaches its anchor, which is
why this broke unnoticed.

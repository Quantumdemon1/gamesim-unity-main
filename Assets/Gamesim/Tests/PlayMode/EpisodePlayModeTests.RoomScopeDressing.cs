using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The house's dressing on its rooms' layers (ROOM-SCOPE-PLAN §3.2 b and c, RS2a): at load every
    /// collider-free piece the classifier puts in a room stands on that room's layer; every staging point
    /// places what it builds or moves in the same call, a copy by where it stands; a piece not drawn yet
    /// takes its room the frame it first is; a piece that moves goes to the shell; and no collider ever
    /// stands on a room layer. While the scope is held on a room, the main camera draws that room's
    /// dressing and the shell; with nothing held it draws everything, and no source is live yet, so
    /// players see today's house. Each test reads layers and masks, a frame after the drawing changes
    /// where it waits on the director's late pass; the live feed's test (RoomScopeBodies) reads pixels.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string Rs2aStageRootName = "Competition stage runtime";
        private const string Rs2aDiaryBackdropName = "Diary interview backdrop";

        /// <summary>
        /// The main camera draws the viewed room's dressing and the shell, and no other room's: the scope
        /// held on each of the eight rooms in turn, every piece on a room layer is drawn only in its own
        /// room's view, every piece of the shell - the floors among them - in all of them, and every bit of
        /// the mask outside 20 to 27 stays as it was. A piece made in a room and not drawn yet waits on the
        /// shell's layer and takes its room the frame it is first drawn; moved, it is the shell's from the
        /// next pass on, drawn in every view, until a staging call places it again. Released, everything is
        /// drawn and the dressing keeps its rooms. The plain SRP path; the shipping drawer's is the probe's P2.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_OnlyTheViewedRoomAndTheShellAreDrawn()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var failures = new List<string>();
            var furnished = Rs2aCheckTheHouse(scope, failures);
            Assert.That(failures, Is.Empty, "Placed at load: " + string.Join("; ", failures.Take(20)));
            for (int room = 0; room < RoomLayers.Count; room++)
                Assert.That(furnished[room], Is.GreaterThan(0), "The " + RoomLayers.RoomIds[room] + "'s dressing stands on its layer.");
            var view = cameraRig.ViewCamera;
            int authored = view.cullingMask;
            Assert.That(authored & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), "With nothing held the main camera draws every room layer.");
            var house = Rs2aHouseRenderers();
            var floors = SceneComponents<HouseWalkable>().Select(floor => floor.GetComponent<Renderer>()).Where(r => r != null).ToArray();
            Assert.That(floors, Is.Not.Empty, "The floors draw.");
            var made = new List<Object>();
            var owner = new object();
            try
            {
                for (int room = 0; room < RoomLayers.Count; room++)
                {
                    string id = RoomLayers.RoomIds[room];
                    cameraRig.MoveTo(Rs4Shot(scope, id));
                    director.HoldRoomScope(owner);
                    yield return null;
                    Assert.That(director.ViewedRooms, Is.EqualTo(RoomSet.Of(room)), "The view is the " + id + ".");
                    int mask = view.cullingMask;
                    int drawnHere = 0, leftOut = 0;
                    foreach (var renderer in house)
                    {
                        if (renderer == null) continue;
                        int layer = renderer.gameObject.layer;
                        bool drawn = (mask & (1 << layer)) != 0;
                        if (RoomLayers.IsRoomLayer(layer))
                        {
                            bool own = layer == RoomLayers.First + room;
                            if (drawn != own)
                                failures.Add(id + "'s view: " + Rs2aName(renderer) + ", on the " + RoomLayers.RoomOf(layer) + "'s layer, is " + (drawn ? "drawn" : "left out"));
                            if (own) drawnHere++; else leftOut++;
                        }
                        else if (!drawn && (authored & (1 << layer)) != 0)
                            failures.Add(id + "'s view: " + Rs2aName(renderer) + ", on layer " + layer + " (the shell's), is left out");
                    }
                    foreach (var floor in floors)
                        if ((mask & (1 << floor.gameObject.layer)) == 0) failures.Add(id + "'s view leaves out the floor " + floor.name);
                    if (drawnHere == 0) failures.Add(id + "'s view draws none of its own dressing");
                    if (leftOut == 0) failures.Add(id + "'s view leaves no other room's dressing out");
                    Assert.That(mask & ~RoomLayers.Mask, Is.EqualTo(authored & ~RoomLayers.Mask), id + "'s view: every bit outside 20 to 27 as it was.");
                }
                Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(20)));

                // A piece made in the kitchen with its object off waits on the shell's layer; switched on,
                // it takes the kitchen's layer by the frame's late pass, before the render.
                cameraRig.MoveTo(Rs4Shot(scope, "Games"));
                yield return null;
                var kitchen = scope.Box(RoomLayers.IndexOf("Kitchen"));
                var piece = ScopeProbeCube(made, "Room scope RS2a piece", new Vector3(kitchen.center.x, .5f, kitchen.center.y), .3f, Color.yellow);
                var drawnPiece = piece.GetComponent<Renderer>();
                piece.SetActive(false);
                Assert.That(HouseRoomScope.PlaceDressing(piece.transform), Is.Zero, "Not drawn yet, it is in no room.");
                Assert.That(piece.layer, Is.Zero, "It waits on the shell's layer,");
                Assert.That(scope.IsWaiting(drawnPiece), Is.True, "for the frame it is first drawn.");
                piece.SetActive(true);
                yield return null;
                Assert.That(piece.layer, Is.EqualTo(RoomLayers.Kitchen), "Drawn, it took the kitchen's layer in that frame's late pass.");
                Assert.That(scope.IsPlaced(drawnPiece), Is.True);
                Assert.That(view.cullingMask & (1 << piece.layer), Is.Zero, "and the games room's view leaves it out.");

                // Moved, it is the shell's from the next pass on: drawn in every view.
                piece.transform.position += Vector3.right * .5f;
                yield return null;
                Assert.That(piece.layer, Is.Zero, "A piece that moves goes to the shell.");
                Assert.That(scope.WentToTheShell(drawnPiece), Is.True);
                Assert.That(scope.IsPlaced(drawnPiece), Is.False);
                Assert.That(view.cullingMask & 1, Is.Not.Zero, "The games room's view draws it now.");
                yield return null;
                Assert.That(piece.layer, Is.Zero, "and it stays the shell's.");
                // A staging call places it again, by where it stands now, in the call itself.
                Assert.That(HouseRoomScope.PlaceDressing(piece.transform), Is.EqualTo(1), "A staging point's call places it in the kitchen again.");
                Assert.That(piece.layer, Is.EqualTo(RoomLayers.Kitchen));
                Assert.That(scope.WentToTheShell(drawnPiece), Is.False);
                yield return null;
                Assert.That(piece.layer, Is.EqualTo(RoomLayers.Kitchen), "Not moved since, it keeps the kitchen's layer.");
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                foreach (var item in made) if (item != null) Object.Destroy(item);
            }
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), "Released, the main camera draws every room layer again.");
            yield return null;
            var after = Rs2aCheckTheHouse(scope, failures);
            Assert.That(failures, Is.Empty, "Released: " + string.Join("; ", failures.Take(20)));
            Assert.That(after, Is.EqualTo(furnished), "and the house's dressing keeps its rooms' layers, room by room: " + string.Join(", ", after));
        }

        /// <summary>
        /// A ceremony set's copies are drawn in the room they stand in (§3.2 b): dressed for a full
        /// house, the living room's screen - a copy of the nomination room's - stands on the living room's
        /// layer and no piece of it on the nomination room's, and the ring's chairs, cloned and moved,
        /// stand on the nomination room's, all in the dressing's own call. The living room's view draws
        /// the screen and leaves the ring out; the nomination room's draws the ring and leaves the screen out.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_ACeremonyCopyIsDrawnInTheRoomItStandsIn()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var scene = director.gameObject.scene;
            int living = RoomLayers.IndexOf(CeremonySets.LivingRoom), nomination = RoomLayers.IndexOf(CeremonySets.NominationRoom);
            var source = SceneComponents<Transform>().FirstOrDefault(t => t.name == ScreenSurface.PropName && t.gameObject.activeInHierarchy);
            Assert.That(source, Is.Not.Null, "The set's screen, which the living room's is copied from.");
            string sourceLayers = string.Join("/", source.GetComponentsInChildren<Renderer>(true).Select(r => r.gameObject.layer).Distinct());
            // A fresh house has no sets dressed until a ceremony stages one: dressed now for a full house,
            // the living room's screen is copied and the ring re-laid with clones, in this call.
            Assert.That(SceneComponents<Transform>().Any(t => t.name == CeremonySets.LivingScreenName), Is.False, "No set is dressed before the first ceremony.");
            CeremonySeating.Ensure(scene, 16);
            var screen = SceneComponents<Transform>().FirstOrDefault(t => t.name == CeremonySets.LivingScreenName);
            Assert.That(screen, Is.Not.Null, "The living room's screen, copied from the nomination room's.");
            var root = SceneComponents<Transform>().First(t => t.name == CeremonySets.RootName && t.parent == null);
            var ring = Rs2aRingChairs().Where(t => t.gameObject.activeInHierarchy).ToList();
            Assert.That(ring.Count, Is.EqualTo(15), "Fifteen chairs round the table for sixteen.");
            Assert.That(ring.Count(chair => chair.IsChildOf(root)), Is.GreaterThan(0), "Some of them cloned for the full house.");

            // In the dressing's own call, before any frame has passed.
            var failures = new List<string>();
            var screenPieces = screen.GetComponentsInChildren<Renderer>(true);
            var ringPieces = ring.SelectMany(chair => chair.GetComponentsInChildren<Renderer>(true)).ToArray();
            var onScreen = Rs2aCheckPieces(scope, screenPieces, "the living room's screen", failures);
            var onRing = Rs2aCheckPieces(scope, ringPieces, "the nomination ring", failures);
            Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(20)));
            Assert.That(onScreen[living], Is.GreaterThan(0), "The copied screen stands on the living room's layer (its source's pieces are on " + sourceLayers + ").");
            Assert.That(screenPieces.Where(r => r.gameObject.layer == RoomLayers.Nomination).Select(Rs2aName).ToArray(), Is.Empty,
                "No piece of the copy keeps the nomination room's layer it could have been copied with.");
            Assert.That(onRing[nomination], Is.GreaterThan(0), "The ring's chairs stand on the nomination room's layer.");
            Assert.That(ringPieces.Where(r => RoomLayers.IsRoomLayer(r.gameObject.layer) && r.gameObject.layer != RoomLayers.Nomination).Select(Rs2aName).ToArray(),
                Is.Empty, "and none on another room's.");

            var owner = new object();
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, CeremonySets.LivingRoom));
                director.HoldRoomScope(owner);
                yield return null;
                int mask = cameraRig.ViewCamera.cullingMask;
                Assert.That(screenPieces.Where(r => r.gameObject.layer == RoomLayers.Living).All(r => (mask & (1 << r.gameObject.layer)) != 0), Is.True,
                    "The living room's view draws its copied screen.");
                Assert.That(ringPieces.Where(r => r.gameObject.layer == RoomLayers.Nomination).Any(r => (mask & (1 << r.gameObject.layer)) != 0), Is.False,
                    "and leaves the nomination room's ring out.");

                cameraRig.MoveTo(Rs4Shot(scope, CeremonySets.NominationRoom));
                yield return null;
                mask = cameraRig.ViewCamera.cullingMask;
                Assert.That(ringPieces.Where(r => r.gameObject.layer == RoomLayers.Nomination).All(r => (mask & (1 << r.gameObject.layer)) != 0), Is.True,
                    "The nomination room's view draws its ring.");
                Assert.That(screenPieces.Where(r => r.gameObject.layer == RoomLayers.Living).Any(r => (mask & (1 << r.gameObject.layer)) != 0), Is.False,
                    "and leaves the living room's copied screen out.");
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
            Assert.That(screenPieces.Count(r => r != null && r.gameObject.layer == RoomLayers.Living), Is.EqualTo(onScreen[living]),
                "Released, the copy keeps the living room's layer.");
        }

        /// <summary>
        /// The capture lens's own render keeps its guard bit (§3.2 c): with the scope held on the kitchen,
        /// a lens drawing the main camera finds its guard - a green patch on layer 31 - drawn through the
        /// camera it is on, and the main camera renders the scope's view, the kitchen's room bit alone of
        /// the eight. The lens puts the mask back as it found it: every bit outside 20 to 27 as authored,
        /// the room bits the scope's.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_CaptureLensKeepsItsGuardBit()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var view = cameraRig.ViewCamera;
            int authored = view.cullingMask;
            const int Guard = 1 << 31;
            var seen = new List<(Camera Camera, int Mask)>();
            void Look(ScriptableRenderContext context, Camera camera) => seen.Add((camera, camera.cullingMask));
            var owner = new object();
            CaptureLens lens = null;
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, "Kitchen"));
                director.HoldRoomScope(owner);
                yield return null;
                Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(1 << RoomLayers.Kitchen), "Held on the kitchen.");
                lens = new CaptureLens(view, 320, 180);
                RenderPipelineManager.beginCameraRendering += Look;
                try
                {
                    // Fails the test itself unless the guard's green is at the frame's centre.
                    yield return lens.MakeSureTheCanvasesAreDrawn();
                    var frame = lens.Read();
                    Object.Destroy(frame);
                }
                finally { RenderPipelineManager.beginCameraRendering -= Look; }
                var ofView = seen.Where(item => item.Camera == view).Select(item => item.Mask).ToList();
                Assert.That(ofView, Is.Not.Empty, "The lens rendered the main camera.");
                Assert.That(ofView.All(mask => (mask & RoomLayers.Mask) == 1 << RoomLayers.Kitchen), Is.True,
                    "Every render of the lens drew the scope's view, the kitchen's room bit alone: " + string.Join(", ", ofView.Select(mask => (mask & RoomLayers.Mask).ToString("X"))));
                var ofLens = seen.Where(item => item.Camera == lens.Lens).Select(item => item.Mask).ToList();
                Assert.That(ofLens.Any(mask => (mask & Guard) != 0), Is.True,
                    "The guard was drawn with its bit in the mask of the camera it is on (" + (lens.Overlay != null ? "the overlay" : "the view") + ").");
            }
            finally
            {
                if (lens != null) lens.Dispose();
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
            }
            Assert.That(view.cullingMask & ~RoomLayers.Mask, Is.EqualTo(authored & ~RoomLayers.Mask), "Every bit outside 20 to 27 as authored, the guard's included.");
            Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(authored & RoomLayers.Mask), "Released, every room bit back.");
        }

        /// <summary>
        /// No collider ever stands on a room layer (§3.2 b): after the load, the ceremony sets, the public
        /// displays and the memory wall, with the scope held and houseguests hidden, no collider in the
        /// house is on one, and every piece on one is collider-free. A piece made in a room with its
        /// collider stays on the shell's layer; its collider gone, the next staging call places it; given
        /// one again, the next call puts it back on layer 0.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_NoColliderEverSitsOnARoomLayer()
        {
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            yield return Rs4BodiesBuilt();
            var scope = ScopeOf(director);
            var scene = director.gameObject.scene;
            CeremonySeating.Ensure(scene, 16);
            HousePublicDisplays.Project(scene, director.Snapshot);
            var wall = SceneComponents<MemoryWall>().FirstOrDefault();
            if (wall != null) wall.Refresh(director.Snapshot);
            ScopeParkHouseguests(scope);
            var made = new List<Object>();
            var owner = new object();
            try
            {
                cameraRig.MoveTo(Rs4Shot(scope, "Kitchen"));
                director.HoldRoomScope(owner);
                yield return null;
                Assert.That(director.HiddenBodyCount, Is.GreaterThan(0), "The houseguests in the yard are hidden, on its layer.");
                var kitchen = scope.Box(RoomLayers.IndexOf("Kitchen"));
                var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                made.Add(piece);
                piece.name = "Room scope RS2a piece with a collider";
                piece.transform.position = new Vector3(kitchen.center.x, .5f, kitchen.center.y);
                piece.transform.localScale = Vector3.one * .3f;
                Physics.SyncTransforms();
                Assert.That(HouseRoomScope.PlaceDressing(piece.transform), Is.Zero, "A piece with a collider is in no room's layer.");
                Assert.That(piece.layer, Is.Zero, "It stays the shell's: its layer is physics.");
                Object.DestroyImmediate(piece.GetComponent<Collider>());
                Assert.That(HouseRoomScope.PlaceDressing(piece.transform), Is.EqualTo(1), "Its collider gone, the next staging call places it.");
                Assert.That(piece.layer, Is.EqualTo(RoomLayers.Kitchen));
                piece.AddComponent<BoxCollider>();
                Assert.That(HouseRoomScope.PlaceDressing(piece.transform), Is.Zero, "Given a collider again,");
                Assert.That(piece.layer, Is.Zero, "the next call puts it back on layer 0.");
                yield return null;

                var colliders = SceneComponents<Collider>().Where(c => RoomLayers.IsRoomLayer(c.gameObject.layer)).Select(c => Rs2aName(c)).ToArray();
                Assert.That(colliders, Is.Empty, "No collider in the house stands on a room layer.");
                var withColliders = SceneComponents<Renderer>().Where(r => RoomLayers.IsRoomLayer(r.gameObject.layer) && r.GetComponent<Collider>() != null)
                    .Select(Rs2aName).ToArray();
                Assert.That(withColliders, Is.Empty, "and no piece on one carries a collider.");
                Assert.That(Rs2aHouseRenderers().Count(r => RoomLayers.IsRoomLayer(r.gameObject.layer)), Is.GreaterThan(0),
                    "The house's dressing is on the room layers, beside the hidden bodies.");
            }
            finally
            {
                director.Release(owner);
                cameraRig.ReleaseShot(0f);
                foreach (var item in made) if (item != null) Object.Destroy(item);
            }
        }

        /// <summary>
        /// Every staging point leaves its pieces in their room (§3.2 b, R6): what it builds or moves stands
        /// on the layer of the room the classifier puts it in, or on the shell's layer for a piece in no
        /// room, waiting to be drawn or moved since; at least one piece of each is in a room; and none
        /// carries a collider. The ceremony sets, the public displays and the memory wall are read in their
        /// own calls, before any frame; the competition's stage in the press that builds it and again when
        /// the player's instrument is first drawn; the diary's backdrop once the player is seated; the
        /// activity effects' bubbles and steam where they rise, and the swimmer's splash, which moves, on
        /// the shell's layer wherever it goes.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_EveryStagingPointLeavesItsPiecesInTheirRoom(
            [Values("CeremonySets", "CompetitionArena", "DiarySeatPose", "HousePublicDisplays", "MemoryWall", "HouseActivityEffects")] string point)
        {
            director.ClosePanels();
            yield return null;
            var scope = ScopeOf(director);
            var failures = new List<string>();
            switch (point)
            {
                case "CeremonySets": yield return Rs2aStageCeremonySets(scope, failures); break;
                case "CompetitionArena": yield return Rs2aStageCompetitionArena(scope, failures); break;
                case "DiarySeatPose": yield return Rs2aStageDiarySeatPose(scope, failures); break;
                case "HousePublicDisplays": Rs2aStagePublicDisplays(scope, failures); break;
                case "MemoryWall": yield return Rs2aStageMemoryWall(scope, failures); break;
                case "HouseActivityEffects": yield return Rs2aStageActivityEffects(scope, failures); break;
                default: Assert.Fail("No staging point " + point + "."); break;
            }
            Assert.That(failures, Is.Empty, point + ": " + string.Join("; ", failures.Take(20)));
        }

        /// <summary>
        /// No source is live yet (RS2a makes none live): with nothing held, whatever the camera does - a
        /// shot naming a room, a free move into one, a follow - the view is the whole house, the main
        /// camera's mask holds every room bit, nobody is hidden and nothing is out, so every piece of the
        /// house is drawn although the dressing stands on its rooms' layers: players see today's house.
        /// </summary>
        [UnityTest]
        public IEnumerator RoomScope_NoSourceIsLiveYet()
        {
            Assert.That(EpisodeDirector.LiveRoomScopeSources, Is.EqualTo(EpisodeDirector.RoomScopeSources.None), "No source is live.");
            foreach (EpisodeDirector.RoomScopeSource source in Enum.GetValues(typeof(EpisodeDirector.RoomScopeSource)))
                Assert.That(EpisodeDirector.IsRoomScopeSourceLive(source), Is.False, source + " is not live.");
            director.ClosePanels();
            director.SuspendNpcAutonomyForDiagnostics();
            yield return null;
            var scope = ScopeOf(director);
            var view = cameraRig.ViewCamera;
            var house = Rs2aHouseRenderers();
            Assert.That(house.Count(r => RoomLayers.IsRoomLayer(r.gameObject.layer)), Is.GreaterThan(0), "The dressing stands on its rooms' layers.");
            var guest = SceneComponents<HouseNpc>().First(npc => npc.isActiveAndEnabled);
            var moves = new (string What, Action Move)[]
            {
                ("a shot that names the games room", () => cameraRig.MoveTo(Rs4Shot(scope, "Games"))),
                ("a free move to the yard", () => { cameraRig.ReleaseShot(0f); cameraRig.MoveTo(ScopeCentre(scope, "Yard"), 12f); }),
                ("following a houseguest", () => cameraRig.FocusSubject(guest.transform)),
            };
            try
            {
                foreach (var move in moves)
                {
                    move.Move();
                    yield return null;
                    Assert.That(director.ViewedRooms.IsWholeHouse, Is.True, move.What + ": the whole house.");
                    Assert.That(view.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), move.What + ": every room bit in the main camera's mask.");
                    Assert.That(director.HiddenBodyCount, Is.Zero, move.What + ": nobody hidden.");
                    Assert.That(director.MainCameraRoomBitsTaken, Is.Zero, move.What + ": nothing out.");
                    var notDrawn = house.Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy && (view.cullingMask & (1 << r.gameObject.layer)) == 0)
                        .Select(Rs2aName).ToArray();
                    Assert.That(notDrawn, Is.Empty, move.What + ": every piece of the house drawn.");
                }
            }
            finally
            {
                cameraRig.ClearSubject();
                cameraRig.ReleaseShot(0f);
            }
        }

        // ------------------------------------------------------------------------------------
        // The staging points.
        // ------------------------------------------------------------------------------------

        /// <summary>The ceremony sets dressed for a full house, the first time: the copies and every chair the ring moved, read in the dressing's own call.</summary>
        private IEnumerator Rs2aStageCeremonySets(HouseRoomScope scope, List<string> failures)
        {
            var scene = director.gameObject.scene;
            CeremonySeating.Ensure(scene, 16);
            var root = SceneComponents<Transform>().First(t => t.name == CeremonySets.RootName && t.parent == null);
            var pieces = root.GetComponentsInChildren<Renderer>(true).Concat(Rs2aRingChairs().Where(t => !t.IsChildOf(root))
                .SelectMany(t => t.GetComponentsInChildren<Renderer>(true))).ToArray();
            Rs2aExpectInRooms(Rs2aCheckPieces(scope, pieces, "the ceremony sets", failures), "the ceremony sets", failures);
            yield return null;
            Rs2aCheckPieces(scope, pieces, "the ceremony sets, a frame on", failures);
        }

        /// <summary>A practice attempt stages the arena: its sign in the press that builds it, the player's instrument the frame it is first drawn.</summary>
        private IEnumerator Rs2aStageCompetitionArena(HouseRoomScope scope, List<string> failures)
        {
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return null;
            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            // Built in that call: read before any frame.
            var stage = SceneComponents<Transform>().FirstOrDefault(t => t.name == Rs2aStageRootName);
            Assert.That(stage, Is.Not.Null, "The practice stages the arena.");
            var sign = stage.GetComponentsInChildren<Renderer>(true).Where(r => r.name == EpisodeDirector.CompetitionSignName).ToArray();
            Assert.That(sign, Is.Not.Empty, "and its sign.");
            Rs2aExpectInRooms(Rs2aCheckPieces(scope, sign, "the arena's sign", failures), "the arena's sign", failures);
            // Built off - it waits until its station is taken - so each of its pieces waits on the shell's
            // layer to be placed the frame it is first drawn.
            var instrument = PlayerInstrument();
            var parts = instrument.GetComponentsInChildren<Renderer>(true);
            Assert.That(parts, Is.Not.Empty);
            foreach (var part in parts)
                if (!scope.IsWaiting(part) && !scope.IsPlaced(part))
                    failures.Add("the instrument's " + Rs2aName(part) + " is neither waiting to be drawn nor placed (layer " + part.gameObject.layer + ")");
            yield return WaitFor(() => instrument != null && instrument.gameObject.activeInHierarchy, 45f, "The player's instrument is drawn at the station.");
            // The frame's late pass places it, before the render.
            yield return null;
            Rs2aExpectInRooms(Rs2aCheckPieces(scope, instrument.GetComponentsInChildren<Renderer>(true), "the player's instrument", failures),
                "the player's instrument", failures);
            Rs2aCheckPieces(scope, stage.GetComponentsInChildren<Renderer>(true), "the arena's stage", failures);
        }

        /// <summary>The diary's backdrop, built as the player sits, read once seated; and kept where it was when the visit ends.</summary>
        private IEnumerator Rs2aStageDiarySeatPose(HouseRoomScope scope, List<string> failures)
        {
            WarpPlayer(director.DiaryPosition + Vector3.right * 1.5f);
            yield return null;
            Assert.That(director.TryOpenDiary(), Is.True);
            yield return WaitForDiarySeating();
            var studio = SceneComponents<Transform>().FirstOrDefault(t => t.name == Rs2aDiaryBackdropName && t.gameObject.activeInHierarchy);
            Assert.That(studio, Is.Not.Null, "Seated, the diary's backdrop stands behind the player.");
            var pieces = studio.GetComponentsInChildren<Renderer>(true);
            Assert.That(studio.GetComponentsInChildren<Collider>(true).Select(Rs2aName).ToArray(), Is.Empty, "Built collider-free in its own frame.");
            Rs2aExpectInRooms(Rs2aCheckPieces(scope, pieces, "the diary's backdrop", failures), "the diary's backdrop", failures);
            var layers = pieces.Select(r => r.gameObject.layer).ToArray();
            director.ClosePanels();
            yield return WaitForDiaryExit();
            Assert.That(pieces.Select(r => r != null ? r.gameObject.layer : -1).ToArray(), Is.EqualTo(layers), "Put away, the backdrop keeps its layers.");
        }

        /// <summary>The public displays projected again: every face on its room's layer, and every label on its face's, one put on the wrong one by hand included.</summary>
        private void Rs2aStagePublicDisplays(HouseRoomScope scope, List<string> failures)
        {
            var scene = director.gameObject.scene;
            HousePublicDisplays.Project(scene, director.Snapshot);
            var faces = SceneComponents<Renderer>().Where(r => r.name == HousePublicDisplays.PhotoName || r.name == HousePublicDisplays.PlaqueName
                || r.name == HousePublicDisplays.YardName || r.name == "HoH photo frame").ToArray();
            Assert.That(faces, Is.Not.Empty, "The house has public display faces.");
            Rs2aExpectInRooms(Rs2aCheckPieces(scope, faces, "the public displays", failures), "the public displays", failures);
            var labelled = faces.Where(face => face.transform.Find("Public display") != null).ToArray();
            Assert.That(labelled, Is.Not.Empty, "Some faces carry mounted words.");
            foreach (var face in labelled) Rs2aLabelFollows(face, failures);
            // A label put on another layer by hand is carried back to its face's at the next projection.
            var moved = labelled[0].transform.Find("Public display");
            int wrong = labelled[0].gameObject.layer == 0 ? RoomLayers.Living : 0;
            foreach (var item in moved.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = wrong;
            HousePublicDisplays.Project(scene, director.Snapshot);
            Rs2aLabelFollows(labelled[0], failures);
        }

        /// <summary>
        /// The memory wall: placed at load and by its first layout; a frame moved by hand goes to the shell
        /// by the next pass; laid out again - the rebuild - it is back on its room's layer in that call.
        /// </summary>
        private IEnumerator Rs2aStageMemoryWall(HouseRoomScope scope, List<string> failures)
        {
            var wall = SceneComponents<MemoryWall>().FirstOrDefault();
            Assert.That(wall, Is.Not.Null, "The episode scene carries a memory wall.");
            Rs2aExpectInRooms(Rs2aCheckPieces(scope, wall.GetComponentsInChildren<Renderer>(true), "the memory wall", failures), "the memory wall", failures);
            var frame = wall.transform.Cast<Transform>().Where(t => t.name.StartsWith(MemoryWall.FramePrefix, StringComparison.Ordinal) && t.gameObject.activeInHierarchy)
                .OrderBy(t => t.name, StringComparer.Ordinal).FirstOrDefault();
            Assert.That(frame, Is.Not.Null, "A frame on show.");
            var pieces = frame.GetComponentsInChildren<Renderer>().Where(r => scope.IsPlaced(r)).ToArray();
            Assert.That(pieces, Is.Not.Empty, "The frame's pieces stand on its room's layer.");
            var layers = pieces.Select(r => r.gameObject.layer).ToArray();
            var home = frame.localPosition;
            frame.localPosition = home + new Vector3(0f, 0f, .2f);
            yield return null;
            foreach (var piece in pieces)
                if (piece.gameObject.layer != 0 || !scope.WentToTheShell(piece))
                    failures.Add("the moved frame's " + Rs2aName(piece) + " did not go to the shell (layer " + piece.gameObject.layer + ")");
            // The wall laid out again, as a rebuild does: the frame back in its place, placed in the same call.
            var arranged = typeof(MemoryWall).GetField("arrangedFor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(arranged, Is.Not.Null, "The wall keeps the cast size it was laid out for.");
            arranged.SetValue(wall, 0);
            wall.Refresh(director.Snapshot);
            Assert.That(Vector3.Distance(frame.localPosition, home), Is.LessThan(.001f), "Laid out again, the frame is back in its place.");
            Assert.That(pieces.Select(r => r.gameObject.layer).ToArray(), Is.EqualTo(layers), "and on its room's layer again, in that call.");
            Assert.That(pieces.All(r => scope.IsPlaced(r) && !scope.WentToTheShell(r)), Is.True, "placed by the wall's own call.");
            Rs2aCheckPieces(scope, wall.GetComponentsInChildren<Renderer>(true), "the memory wall laid out again", failures);
        }

        /// <summary>The bubbles and the steam where they rise; the splash, which follows the swimmer, on the shell's layer before and after it moves.</summary>
        private IEnumerator Rs2aStageActivityEffects(HouseRoomScope scope, List<string> failures)
        {
            var effects = director.ActivityEffects;
            Assert.That(effects, Is.Not.Null, "The director attaches the house's activity effects once it is ready.");
            var rising = new[] { effects.Bubbles, effects.Steam }.Where(system => system != null)
                .Select(system => system.GetComponent<Renderer>()).ToArray();
            Assert.That(rising, Is.Not.Empty, "The house has a hot tub or a hob.");
            var inRooms = Rs2aCheckPieces(scope, rising, "the bubbles and the steam", failures);
            Rs2aExpectInRooms(inRooms, "the bubbles and the steam", failures);
            foreach (var renderer in rising)
            {
                int room = scope.RoomUnder(renderer.transform.position);
                if (renderer.gameObject.layer != RoomLayers.First + room)
                    failures.Add(renderer.name + " rises in the " + HouseRoomScope.RoomId(room) + " but stands on layer " + renderer.gameObject.layer);
            }
            if (effects.Splash == null) yield break;
            var splash = effects.Splash.GetComponent<Renderer>();
            if (splash.gameObject.layer != 0 || scope.IsPlaced(splash)) failures.Add("the splash, which moves, is not the shell's");
            var pool = effects.Splash.transform.position;
            effects.Tick(false, false, pool + new Vector3(.6f, 0f, .3f));
            yield return null;
            if (splash.gameObject.layer != 0 || scope.IsPlaced(splash)) failures.Add("the splash, moved with a swimmer, is not the shell's");
        }

        // ------------------------------------------------------------------------------------
        // Helpers.
        // ------------------------------------------------------------------------------------

        /// <summary>The nomination ring's chairs, on or off: every dressed chair within 4 m of the table, as the set dresser finds them.</summary>
        private static List<Transform> Rs2aRingChairs()
        {
            var marker = SceneComponents<HouseRoomMarker>().First(m => m.RoomName == CeremonySets.NominationRoom);
            var table = SceneComponents<Transform>().FirstOrDefault(t => t.name == CeremonySets.TableName && Flat(t.position, marker.transform.position) < 4f);
            var centre = table != null ? table.position : marker.transform.position;
            return SceneComponents<Transform>().Where(t => t.name == CeremonySets.ChairName && Flat(t.position, centre) < 4f).ToList();
        }

        /// <summary>Every renderer of the house but the bodies', which RS4 draws by where they walk.</summary>
        private static Renderer[] Rs2aHouseRenderers() => SceneComponents<Renderer>().Where(r => !HouseRoomScope.IsBody(r.transform)).ToArray();

        /// <summary>
        /// The dressing over the whole house: every piece on a room layer is the dressing's, collider-free,
        /// and where the rule puts it; every piece of the authored house the rule puts in a room is on its
        /// room's layer, or moved since and gone to the shell. Runtime pieces outside the staging points
        /// (the follow ring, the splash) are drawn always and not counted. The pieces on each room's layer.
        /// </summary>
        private static int[] Rs2aCheckTheHouse(HouseRoomScope scope, List<string> failures)
        {
            var perRoom = Rs2aCheckPieces(scope, Rs2aHouseRenderers(), "the house", failures, false);
            foreach (var root in SceneComponents<Transform>().Where(t => t.parent == null && Rs2aAuthoredRoot(t.name)))
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.gameObject.layer != 0 || HouseRoomScope.IsBody(renderer.transform) || !HouseRoomScope.CanPlace(renderer)) continue;
                    if (scope.Classify(renderer, out int room) == HouseRoomScope.Placement.Room && !scope.WentToTheShell(renderer))
                        failures.Add(Rs2aName(renderer) + " is the " + HouseRoomScope.RoomId(room) + "'s by the rule but stands on the shell's layer");
                }
            return perRoom;
        }

        /// <summary>The scene's authored house: its architecture and its rooms' roots, where every set piece, finish and prop was saved.</summary>
        private static bool Rs2aAuthoredRoot(string name) => name == "House Architecture" || name.StartsWith("Room - ", StringComparison.Ordinal);

        /// <summary>
        /// Each piece against the dressing's rule, as it stands now: on a room layer only if it is the
        /// dressing's, collider-free and in that room by the classifier; on the shell's layer if the rule
        /// puts it in no room, it is not drawn yet and waits, or it moved and went to the shell (with
        /// <paramref name="everyPiece"/>; otherwise only the room layers are read). Bodies and pieces on
        /// layers of their own are not the dressing's. The pieces on each room's layer.
        /// </summary>
        private static int[] Rs2aCheckPieces(HouseRoomScope scope, IEnumerable<Renderer> pieces, string what, List<string> failures, bool everyPiece = true)
        {
            var perRoom = new int[RoomLayers.Count];
            foreach (var renderer in pieces)
            {
                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is ParticleSystemRenderer)) continue;
                if (HouseRoomScope.IsBody(renderer.transform)) continue;
                int layer = renderer.gameObject.layer;
                if (layer != 0 && !RoomLayers.IsRoomLayer(layer)) continue;
                bool readable = HouseRoomScope.CanPlace(renderer);
                int room = -1;
                var placement = HouseRoomScope.Placement.Outside;
                if (readable) placement = scope.Classify(renderer, out room);
                if (RoomLayers.IsRoomLayer(layer))
                {
                    perRoom[layer - RoomLayers.First]++;
                    if (renderer.GetComponent<Collider>() != null) failures.Add(what + ": " + Rs2aName(renderer) + " carries a collider on a room layer");
                    if (!scope.IsPlaced(renderer)) failures.Add(what + ": " + Rs2aName(renderer) + " is on the " + RoomLayers.RoomOf(layer) + "'s layer, which the dressing did not put it on");
                    if (readable && (placement != HouseRoomScope.Placement.Room || RoomLayers.First + room != layer))
                        failures.Add(what + ": " + Rs2aName(renderer) + " is on the " + RoomLayers.RoomOf(layer) + "'s layer but reads " + placement
                            + (room >= 0 ? " in the " + HouseRoomScope.RoomId(room) : "") + Rs2aWhere(renderer));
                    continue;
                }
                // The whole house's pass reads only what stands on the room layers: the authored house's
                // shell is read apart (Rs2aCheckTheHouse), and runtime pieces no staging point builds are
                // drawn always.
                if (!everyPiece) continue;
                if (!readable)
                {
                    if (!scope.IsWaiting(renderer)) failures.Add(what + ": " + Rs2aName(renderer) + ", not drawn yet, is not waiting to be placed");
                    continue;
                }
                if (placement == HouseRoomScope.Placement.Room && !scope.WentToTheShell(renderer))
                    failures.Add(what + ": " + Rs2aName(renderer) + " is the " + HouseRoomScope.RoomId(room) + "'s by the rule but stands on the shell's layer" + Rs2aWhere(renderer));
            }
            return perRoom;
        }

        /// <summary>At least one of a staging point's pieces stands in a room: the call placed something.</summary>
        private static void Rs2aExpectInRooms(int[] perRoom, string what, List<string> failures)
        {
            if (perRoom.Sum() == 0) failures.Add(what + ": no piece stands on a room layer");
        }

        /// <summary>A mounted label carries its face's layer, every object of it.</summary>
        private static void Rs2aLabelFollows(Renderer face, List<string> failures)
        {
            var label = face.transform.Find("Public display");
            if (label == null) { failures.Add(face.name + " lost its words"); return; }
            foreach (var item in label.GetComponentsInChildren<Transform>(true))
                if (item.gameObject.layer != face.gameObject.layer)
                    failures.Add(face.name + "'s words' " + item.name + " on layer " + item.gameObject.layer + ", the face on " + face.gameObject.layer);
        }

        private static string Rs2aWhere(Renderer renderer)
        {
            var bounds = renderer is ParticleSystemRenderer ? new Bounds(renderer.transform.position, Vector3.zero) : renderer.bounds;
            return " (x " + bounds.min.x.ToString("0.00") + ".." + bounds.max.x.ToString("0.00") + ", z " + bounds.min.z.ToString("0.00") + ".." + bounds.max.z.ToString("0.00") + ")";
        }

        private static string Rs2aName(Component piece)
        {
            if (piece == null) return "(destroyed)";
            var t = piece.transform;
            return (t.parent != null ? t.parent.name + "/" : "") + t.name;
        }
    }
}

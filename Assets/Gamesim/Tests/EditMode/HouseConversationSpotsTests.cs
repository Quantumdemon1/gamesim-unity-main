using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The talk spots (PACK8-PASS-PLAN C3): seat pairs on the living room's couches and at the long
    /// table, each belonging to one of the six venues a save can name. Built in a preview scene
    /// dressed the way the house is, by name and at the house's numbers, and in the house itself.
    /// Every spot's venue is one the saves know, its seats are on furniture, its two roots stand
    /// apart and within talking reach, its ids are its own, and nothing about it can be clicked.
    /// </summary>
    public sealed class HouseConversationSpotsTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";

        /// <summary>The long table's two rows, as the scene places them: seven chairs a side, a chair at each head.</summary>
        private static readonly float[] RowChairs = { 5.894f, 6.538f, 7.196f, 7.84f, 8.484f, 9.142f, 9.786f };
        private const float NorthRow = -7.22f, SouthRow = -8.78f, TableHome = 7.196f;

        private static GameObject Box(string name, Vector3 at, Vector3 size, float yaw = 0f, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.transform.SetPositionAndRotation(at + Vector3.up * size.y * 0.5f, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        private static HouseRoomMarker Marker(Scene scene, string room, Vector3 at)
        {
            var go = new GameObject(room + " marker");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = at;
            var marker = go.AddComponent<HouseRoomMarker>();
            marker.Configure(room);
            return marker;
        }

        /// <summary>
        /// The house's two furnished rooms at the scene's numbers: the living room's gallery - the U's
        /// base of two four-seat couches facing south, an arm each side facing in, the red chairs and
        /// the room's own screen - and the kitchen's long table, its own pair authored on the middle
        /// chairs facing across it, approached from behind as the scene's pair is.
        /// </summary>
        private static Scene FurnishedHouse(bool withGallery = true, bool withTable = true)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var pieces = new GameObject("Set Pieces");
            SceneManager.MoveGameObjectToScene(pieces, scene);
            Marker(scene, "Living", new Vector3(-5f, 0f, -7.6f));
            Marker(scene, "Kitchen", new Vector3(3f, 0f, -6f));
            var floor = new GameObject(CeremonySets.LivingFloorName);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(-7f, -0.15f, -5f);
            floor.AddComponent<BoxCollider>().size = new Vector3(14f, 0.3f, 10f);
            if (withGallery)
            {
                var screen = new GameObject(CeremonySets.LivingScreenName);
                screen.transform.SetParent(pieces.transform, false);
                screen.transform.SetPositionAndRotation(new Vector3(-8f, 0f, -8.9f), Quaternion.Euler(0f, 180f, 0f));
                var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                board.name = "board";
                board.transform.SetParent(screen.transform, false);
                board.transform.localScale = new Vector3(2.6f, 1.5f, 0.1f);
                board.transform.localPosition = new Vector3(0f, 1.75f, -0.54f);
                Box(CeremonySets.LoungeFourName, new Vector3(-9.8f, 0f, -2.3f), new Vector3(3.44f, 0.80f, 0.92f), 0f, pieces.transform);
                Box(CeremonySets.LoungeFourName, new Vector3(-6.2f, 0f, -2.3f), new Vector3(3.44f, 0.80f, 0.92f), 0f, pieces.transform);
                Box(CeremonySets.LoungeThreeName, new Vector3(-11.78f, 0f, -5.6f), new Vector3(2.68f, 0.80f, 0.92f), 270f, pieces.transform);
                Box(CeremonySets.LoungeThreeName, new Vector3(-4.22f, 0f, -5.6f), new Vector3(2.68f, 0.80f, 0.92f), 90f, pieces.transform);
                Box(CeremonySets.WingbackName, new Vector3(-8.55f, 0f, -7.3f), new Vector3(0.9f, 1.25f, 0.9f), 180f, pieces.transform);
                Box(CeremonySets.WingbackName, new Vector3(-7.45f, 0f, -7.3f), new Vector3(0.9f, 1.25f, 0.9f), 180f, pieces.transform);
            }
            if (withTable)
            {
                Box("bb_set_ph_diningtable", new Vector3(7.84f, 0f, -8f), new Vector3(4.6f, 0.75f, 1f), 0f, pieces.transform);
                foreach (float x in RowChairs)
                {
                    var north = Box("bb_set_ph_diningchair", new Vector3(x, 0f, NorthRow), new Vector3(0.5f, 1.06f, 0.5f), 180f, pieces.transform);
                    var south = Box("bb_set_ph_diningchair", new Vector3(x, 0f, SouthRow), new Vector3(0.5f, 1.06f, 0.5f), 0f, pieces.transform);
                    if (Mathf.Approximately(x, TableHome))
                    {
                        HouseInteractionAnchor.Create(north.transform, SpotFamily.Table, "Kitchen", 0, new Vector3(x, 0f, NorthRow), 180f, true, Vector3.back * 0.70f);
                        HouseInteractionAnchor.Create(south.transform, SpotFamily.Table, "Kitchen", 1, new Vector3(x, 0f, SouthRow), 0f, true, Vector3.back * 0.70f);
                    }
                }
                Box("bb_set_ph_diningchair", new Vector3(5.138f, 0f, -8f), new Vector3(0.5f, 1.06f, 0.5f), 90f, pieces.transform);
                Box("bb_set_ph_diningchair", new Vector3(10.542f, 0f, -8f), new Vector3(0.5f, 1.06f, 0.5f), 270f, pieces.transform);
            }
            return scene;
        }

        private static class SpotFamily
        {
            public const string Living = HouseConversationSpots.LivingFamily;
            public const string Table = HouseConversationSpots.TableFamily;
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        private static Dictionary<string, HouseConversationSpots.Spot> Spots(Scene scene) =>
            HouseConversationSpots.InScene(scene).ToDictionary(spot => spot.Id);

        /// <summary>Every spot the house is dressed with keeps the rules a talk spot lives by.</summary>
        private static void AssertEverySpotKeepsTheRules(Scene scene, IEnumerable<HouseConversationSpots.Spot> spots)
        {
            var meetings = HouseInteractionAnchors.Meetings.Select(venue => venue.Id).ToList();
            foreach (var spot in spots)
            {
                Assert.That(NpcSocialState.IsKnownRendezvous(spot.Family), Is.True, spot.Id + " belongs to a venue a save can name.");
                Assert.That(HouseConversationSpots.FamilyOf(spot.Id), Is.EqualTo(spot.Family));
                Assert.That(spot.Room, Is.EqualTo(HouseInteractionAnchors.Meetings.Single(venue => venue.Id == spot.Family).Room),
                    spot.Id + " is in its venue's room.");
                Assert.That(meetings, Does.Not.Contain(spot.Id), spot.Id + " has an id of its own, not a venue's.");
                Assert.That(CeremonySets.IsCeremonyVenue(spot.Id), Is.False, spot.Id + " is never struck with the ceremony sets.");
                Assert.That(spot.Seated, Is.True, spot.Id + " is a pair of seats.");
                foreach (var seat in new[] { spot.First, spot.Second })
                {
                    Assert.That(HouseConversationSpots.OnFurniture(seat), Is.True, spot.Id + " slot " + seat.Slot + " is on furniture.");
                    Assert.That(HouseFurniture.TryClick(seat, out _, out _), Is.False, spot.Id + " slot " + seat.Slot + " is not clickable.");
                    Assert.That(HouseFurniture.AtProp(scene, seat.transform.parent), Is.Null, "and makes its piece no click of its own.");
                }
                float apart = Flat(spot.First.Approach, spot.Second.Approach);
                Assert.That(apart, Is.GreaterThanOrEqualTo(HouseConversationSpots.RootsApart - 0.001f), spot.Id + "'s roots wait apart.");
                Assert.That(apart, Is.LessThanOrEqualTo(HouseConversationSpots.PlayerReach + 0.001f), spot.Id + "'s roots wait within talking reach.");
                Assert.That(Flat(spot.First.Position, spot.Second.Position), Is.GreaterThanOrEqualTo(HouseConversationSpots.SeatsApart),
                    spot.Id + " is two seats, not one.");
            }
        }

        [Test]
        public void TheGalleryAndTheLongTableGetSeatPairsOnTheirOwnPieces()
        {
            var scene = FurnishedHouse();
            try
            {
                HouseConversationSpots.Ensure(scene);
                var spots = Spots(scene);
                Assert.That(spots.Keys, Is.EquivalentTo(new[]
                {
                    HouseConversationSpots.IdFor(SpotFamily.Living, "base-middle"),
                    HouseConversationSpots.IdFor(SpotFamily.Living, "corner-west"),
                    HouseConversationSpots.IdFor(SpotFamily.Living, "corner-east"),
                    HouseConversationSpots.IdFor(SpotFamily.Table, "side-0"),
                    HouseConversationSpots.IdFor(SpotFamily.Table, "side-180"),
                }), "Three spots on the couches and one each side of the table.");
                AssertEverySpotKeepsTheRules(scene, spots.Values);

                void Expect(string id, Vector2 first, float firstYaw, Vector2 second, float secondYaw, float approach, params string[] pieces)
                {
                    var spot = spots[id];
                    var seats = new[] { spot.First, spot.Second };
                    var wanted = new[] { (first, firstYaw), (second, secondYaw) };
                    foreach (var (at, yaw) in wanted)
                    {
                        var seat = seats.OrderBy(s => Flat(s.Position, new Vector3(at.x, 0f, at.y))).First();
                        Assert.That(Flat(seat.Position, new Vector3(at.x, 0f, at.y)), Is.LessThan(0.02f), id + " has a seat at " + at);
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(seat.Facing, yaw)), Is.LessThan(1f), id + "'s seat at " + at + " faces " + yaw);
                        Assert.That(Flat(seat.Approach, seat.Position), Is.EqualTo(approach).Within(0.01f), id + "'s seat at " + at + " is approached as its piece's are");
                        Assert.That(pieces, Does.Contain(seat.transform.parent.name), id + "'s seat at " + at + " hangs on its piece");
                    }
                }
                string[] couches = { CeremonySets.LoungeFourName, CeremonySets.LoungeThreeName };
                Expect(HouseConversationSpots.IdFor(SpotFamily.Living, "base-middle"), new Vector2(-8.66f, -2.4f), 180f, new Vector2(-7.34f, -2.4f), 180f,
                    CeremonySets.GalleryApproach, couches);
                Expect(HouseConversationSpots.IdFor(SpotFamily.Living, "corner-west"), new Vector2(-10.94f, -2.4f), 180f, new Vector2(-11.68f, -4.84f), 90f,
                    CeremonySets.GalleryApproach, couches);
                Expect(HouseConversationSpots.IdFor(SpotFamily.Living, "corner-east"), new Vector2(-5.06f, -2.4f), 180f, new Vector2(-4.32f, -4.84f), 270f,
                    CeremonySets.GalleryApproach, couches);
                Expect(HouseConversationSpots.IdFor(SpotFamily.Table, "side-180"), new Vector2(8.484f, NorthRow), 180f, new Vector2(9.786f, NorthRow), 180f,
                    0.70f, "bb_set_ph_diningchair");
                Expect(HouseConversationSpots.IdFor(SpotFamily.Table, "side-0"), new Vector2(8.484f, SouthRow), 0f, new Vector2(9.786f, SouthRow), 0f,
                    0.70f, "bb_set_ph_diningchair");

                // The table's own pair keeps its floor: no spot's root waits within two bodies of its roots.
                var home = HouseInteractionAnchors.InScene(scene).Where(anchor => anchor.VenueId == SpotFamily.Table).ToList();
                Assert.That(home, Has.Count.EqualTo(2));
                foreach (var spot in spots.Values.Where(spot => spot.Family == SpotFamily.Table))
                    foreach (var seat in new[] { spot.First, spot.Second })
                        foreach (var own in home)
                            Assert.That(Flat(seat.Approach, own.Approach), Is.GreaterThanOrEqualTo(HouseConversationSpots.RootsApart),
                                spot.Id + " stands clear of the table's own pair.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>
        /// The couches' spots are the gallery's own seats, approached where the gallery approaches
        /// them - the approaches the ceremonies proved on the couches' baked edge - and the gallery
        /// dressed over them strikes none of them.
        /// </summary>
        [Test]
        public void TheCouchSpotsReuseTheGallerysSeatsAndSurviveItsDressing()
        {
            var scene = FurnishedHouse(withTable: false);
            try
            {
                HouseConversationSpots.Ensure(scene);
                CeremonySeating.Ensure(scene, 16);
                var gallery = CeremonySeating.Anchors(scene, CeremonySeating.GallerySeat);
                Assert.That(gallery, Has.Count.EqualTo(14), "The gallery dressed as it always is.");
                var spots = Spots(scene);
                Assert.That(spots.Count, Is.EqualTo(3), "and the three couch spots are still there.");
                foreach (var spot in spots.Values)
                    foreach (var seat in new[] { spot.First, spot.Second })
                    {
                        var same = gallery.OrderBy(g => Flat(g.Position, seat.Position)).First();
                        Assert.That(Flat(same.Position, seat.Position), Is.LessThan(0.02f), spot.Id + " sits on a gallery seat");
                        Assert.That(Flat(same.Approach, seat.Approach), Is.LessThan(0.02f), "and is approached where the gallery approaches it");
                        Assert.That(same.SeatContact.y, Is.EqualTo(seat.SeatContact.y).Within(0.01f), "at the cushion's height.");
                    }
                CeremonySets.Strike(scene);
                Assert.That(Spots(scene).Count, Is.EqualTo(3), "Striking the ceremony sets leaves the talk spots alone.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void DressingTwiceChangesNothing()
        {
            var scene = FurnishedHouse();
            try
            {
                HouseConversationSpots.Ensure(scene);
                var before = HouseInteractionAnchors.InScene(scene).Where(a => HouseConversationSpots.IsSpot(a.VenueId)).ToList();
                HouseConversationSpots.Ensure(scene);
                var after = HouseInteractionAnchors.InScene(scene).Where(a => HouseConversationSpots.IsSpot(a.VenueId)).ToList();
                Assert.That(after.Count, Is.EqualTo(before.Count).And.EqualTo(10), "Five spots, two seats each, once.");
                Assert.That(after.All(a => before.Contains(a)), Is.True, "The same anchors, none added.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>No piece, no seat: an unfurnished house, a struck couch and a chair that is not furniture get no spots.</summary>
        [Test]
        public void NothingIsInventedWhereThereIsNoFurniture()
        {
            var bare = FurnishedHouse(withGallery: false, withTable: false);
            try
            {
                HouseConversationSpots.Ensure(bare);
                Assert.That(HouseConversationSpots.InScene(bare), Is.Empty, "A house with no gallery and no table has its six venues and nothing more.");
            }
            finally { EditorSceneManager.ClosePreviewScene(bare); }

            var struck = FurnishedHouse(withTable: false);
            try
            {
                foreach (var couch in struck.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                             .Where(t => t.name == CeremonySets.LoungeFourName && t.position.x > -8f).ToList())
                    couch.gameObject.SetActive(false);
                HouseConversationSpots.Ensure(struck);
                var ids = HouseConversationSpots.InScene(struck).Select(spot => spot.Id).ToList();
                Assert.That(ids, Does.Not.Contain(HouseConversationSpots.IdFor(SpotFamily.Living, "corner-east")), "A struck couch seats nobody,");
                Assert.That(ids, Does.Not.Contain(HouseConversationSpots.IdFor(SpotFamily.Living, "base-middle")), "on either of its spots.");
            }
            finally { EditorSceneManager.ClosePreviewScene(struck); }

            var hollow = EditorSceneManager.NewPreviewScene();
            try
            {
                Marker(hollow, "Kitchen", new Vector3(3f, 0f, -6f));
                // The table's pair on empty objects, and chairs that are names and nothing else.
                foreach (float x in RowChairs)
                {
                    var north = new GameObject("bb_set_ph_diningchair");
                    var south = new GameObject("bb_set_ph_diningchair");
                    SceneManager.MoveGameObjectToScene(north, hollow); SceneManager.MoveGameObjectToScene(south, hollow);
                    north.transform.SetPositionAndRotation(new Vector3(x, 0f, NorthRow), Quaternion.Euler(0f, 180f, 0f));
                    south.transform.SetPositionAndRotation(new Vector3(x, 0f, SouthRow), Quaternion.identity);
                    if (!Mathf.Approximately(x, TableHome)) continue;
                    HouseInteractionAnchor.Create(north.transform, SpotFamily.Table, "Kitchen", 0, north.transform.position, 180f, true, Vector3.back * 0.70f);
                    HouseInteractionAnchor.Create(south.transform, SpotFamily.Table, "Kitchen", 1, south.transform.position, 0f, true, Vector3.back * 0.70f);
                }
                HouseConversationSpots.Ensure(hollow);
                Assert.That(HouseConversationSpots.InScene(hollow), Is.Empty, "A chair with nothing drawn is not furniture to sit on.");
            }
            finally { EditorSceneManager.ClosePreviewScene(hollow); }
        }

        [Test]
        public void ASeatIsOnFurnitureOnlyWhenThereIsFurnitureUnderIt()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var chair = Box("Test chair", Vector3.zero, new Vector3(0.5f, 1.0f, 0.5f));
                SceneManager.MoveGameObjectToScene(chair, scene);
                var onIt = HouseInteractionAnchor.Create(chair.transform, "test-seat", "Living", 0, Vector3.zero, 0f, true);
                onIt.SetSeatHeight(0.45f);
                Assert.That(HouseConversationSpots.OnFurniture(onIt), Is.True, "A seat over a drawn piece, at a height inside it.");

                var high = HouseInteractionAnchor.Create(chair.transform, "test-seat", "Living", 1, Vector3.zero, 0f, true);
                high.SetSeatHeight(1.2f);
                Assert.That(HouseConversationSpots.OnFurniture(high), Is.False, "A seat above the piece is in the air.");

                var beside = HouseInteractionAnchor.Create(chair.transform, "test-seat", "Living", 2, new Vector3(1.5f, 0f, 0f), 0f, true);
                Assert.That(HouseConversationSpots.OnFurniture(beside), Is.False, "A seat beside the piece is on the floor.");

                var empty = new GameObject("Nothing at all");
                SceneManager.MoveGameObjectToScene(empty, scene);
                var onNothing = HouseInteractionAnchor.Create(empty.transform, "test-seat", "Living", 3, Vector3.zero, 0f, true);
                Assert.That(HouseConversationSpots.OnFurniture(onNothing), Is.False, "A seat on an empty object is on nothing.");

                var standing = HouseInteractionAnchor.Create(chair.transform, "test-mark", "Living", 0, Vector3.zero, 0f, false);
                Assert.That(HouseConversationSpots.OnFurniture(standing), Is.False, "A standing mark is no seat.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>The places the story session may stand its people: the standing venues' marks in that room, and never a seat.</summary>
        [Test]
        public void StandingPlacesAreTheRoomsStandingMarks()
        {
            var scene = FurnishedHouse();
            try
            {
                var living = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<HouseRoomMarker>(true))
                    .First(marker => marker.RoomName == "Living");
                var a = HouseInteractionAnchor.Create(living.transform, SpotFamily.Living, "Living", 0, new Vector3(-3.4f, 0f, -3f), 90f, false);
                var b = HouseInteractionAnchor.Create(living.transform, SpotFamily.Living, "Living", 1, new Vector3(-2.0f, 0f, -3f), 270f, false);
                HouseConversationSpots.Ensure(scene);
                var places = HouseConversationSpots.StandingPlaces(scene, "Living");
                Assert.That(places, Has.Count.EqualTo(2), "The living room's two standing marks, and none of its couch seats.");
                Assert.That(places.Any(p => Flat(p, a.Approach) < 0.01f) && places.Any(p => Flat(p, b.Approach) < 0.01f), Is.True);
                Assert.That(HouseConversationSpots.StandingPlaces(scene, "Kitchen"), Is.Empty, "The table's places are seats.");
                Assert.That(HouseConversationSpots.StandingPlaces(scene, "Yard"), Is.Empty, "A room with no marks has none.");
                Assert.That(HouseConversationSpots.StandingPlaces(scene, null), Is.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /// <summary>A pair takes the shortest walk, two seats counted three metres shorter: they sit when they can.</summary>
        [Test]
        public void ASeatWinsWithinThreeMetresOfWalk()
        {
            Assert.That(HouseMeetingCoordinator.SpotScore(10f, true), Is.LessThan(HouseMeetingCoordinator.SpotScore(8f, false)),
                "Two metres further to sit is worth it.");
            Assert.That(HouseMeetingCoordinator.SpotScore(12f, true), Is.GreaterThan(HouseMeetingCoordinator.SpotScore(8f, false)),
                "Four metres further is not.");
            Assert.That(HouseMeetingCoordinator.SpotScore(8f, true), Is.LessThan(HouseMeetingCoordinator.SpotScore(8f, false)));
            Assert.That(HouseConversationSpots.SeatedBonus, Is.EqualTo(3f));
            Assert.That(HouseConversationSpots.FamilyOf(HouseConversationSpots.IdFor(SpotFamily.Living, "x")), Is.EqualTo(SpotFamily.Living));
            Assert.That(HouseConversationSpots.FamilyOf(SpotFamily.Table), Is.EqualTo(SpotFamily.Table), "A venue is its own family.");
            Assert.That(HouseConversationSpots.FamilyOf("talk:private-room-chat:x"), Is.Null, "A spot names a venue a save knows, or none.");
            Assert.That(HouseConversationSpots.FamilyOf("gallery-seat"), Is.Null);
        }

        /// <summary>
        /// The house itself: its gallery and its long table get the same five spots, on the pieces
        /// the scene places, and the six venues the saves know are untouched.
        /// </summary>
        [Test]
        public void TheHouseGetsItsSpotsOnItsOwnPieces()
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try
            {
                string Venues() => string.Join("; ", HouseInteractionAnchors.InScene(scene).Where(a => HouseConversationSpots.IsFamily(a.VenueId))
                    .OrderBy(a => a.VenueId).ThenBy(a => a.Slot).Select(a => a.VenueId + " " + a.Slot + " " + a.Position + " " + a.Approach + " " + a.Seated));
                string venuesBefore = Venues();
                HouseConversationSpots.Ensure(scene);
                var spots = Spots(scene);
                Assert.That(spots.Keys, Is.EquivalentTo(new[]
                {
                    HouseConversationSpots.IdFor(SpotFamily.Living, "base-middle"),
                    HouseConversationSpots.IdFor(SpotFamily.Living, "corner-west"),
                    HouseConversationSpots.IdFor(SpotFamily.Living, "corner-east"),
                    HouseConversationSpots.IdFor(SpotFamily.Table, "side-0"),
                    HouseConversationSpots.IdFor(SpotFamily.Table, "side-180"),
                }), "The house's gallery and long table, dressed as the preview's.");
                AssertEverySpotKeepsTheRules(scene, spots.Values);
                foreach (var spot in spots.Values)
                    foreach (var seat in new[] { spot.First, spot.Second })
                        Assert.That(spot.Family == SpotFamily.Living
                            ? seat.transform.parent.name == CeremonySets.LoungeFourName || seat.transform.parent.name == CeremonySets.LoungeThreeName
                            : seat.transform.parent.name == "bb_set_ph_diningchair" || seat.transform.parent.name == "bb_set_diningchair", Is.True,
                            spot.Id + " hangs on " + seat.transform.parent.name);
                Assert.That(Venues(), Is.EqualTo(venuesBefore), "The six venues' own anchors are what they were.");
                Assert.That(HouseInteractionAnchors.InScene(scene).Count(a => a.VenueId == SpotFamily.Table), Is.EqualTo(2),
                    "and the table's own pair is still the only pair under its name.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}

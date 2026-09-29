using System.Linq;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The ceremony sets (CEREMONY-CUTSCENES-PLAN C1): a chair with a seat for everyone who draws a
    /// key, laid round the table with the head left open for the Head of Household, more chairs
    /// cloned for a big house and struck for a small one; the living room's own screen in the
    /// television's place, two hot seats facing it, and the sofa turned to it with three seats.
    /// Built in a preview scene dressed the way the house is, by name, at the house's numbers.
    /// </summary>
    public sealed class CeremonySeatingTests
    {
        private static readonly Vector3 TableCentre = new Vector3(0.075f, 0f, -14.6f);
        private static readonly Vector3 ScreenAt = new Vector3(0.075f, 0f, -18.9f);

        private static GameObject Box(string name, Vector3 at, Vector3 size, float yaw = 0f, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.transform.SetPositionAndRotation(at + Vector3.up * size.y * 0.5f, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>The house as dressed: the nomination room's table, six chairs and screen; the living room's floor, television and sofa.</summary>
        private static Scene DressedHouse()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var pieces = new GameObject("Set Pieces");
            SceneManager.MoveGameObjectToScene(pieces, scene);
            var nomination = new GameObject("Nomination marker");
            SceneManager.MoveGameObjectToScene(nomination, scene);
            nomination.transform.position = new Vector3(0.075f, 0f, -15f);
            nomination.AddComponent<HouseRoomMarker>().Configure("Nomination");
            Box("tableRound", TableCentre, new Vector3(1.2f, 0.78f, 1.2f), 0f, pieces.transform);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                var at = TableCentre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.1f;
                Box("chairModernCushion", at, new Vector3(0.57f, 0.90f, 0.56f), 0f, pieces.transform);
            }
            // The screen: a board on a stage, the thin axis facing the room, at the room's south end.
            var screen = new GameObject(ScreenSurface.PropName);
            screen.transform.SetParent(pieces.transform, false);
            screen.transform.SetPositionAndRotation(ScreenAt, Quaternion.Euler(0f, 180f, 0f));
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "board";
            board.transform.SetParent(screen.transform, false);
            board.transform.localScale = new Vector3(2.6f, 1.5f, 0.1f);
            board.transform.localPosition = new Vector3(0f, 1.75f, 0.3f);

            var living = new GameObject("Living marker");
            SceneManager.MoveGameObjectToScene(living, scene);
            living.transform.position = new Vector3(-5f, 0f, -7f);
            living.AddComponent<HouseRoomMarker>().Configure("Living");
            var floor = new GameObject(CeremonySets.LivingFloorName);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(-7f, -0.15f, -5f);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(14f, 0.3f, 10f);
            Box(CeremonySets.TelevisionConsoleName, new Vector3(-5f, 0f, -1.5f), new Vector3(4f, 1f, 0.8f), 0f, pieces.transform);
            Box(CeremonySets.TelevisionName, new Vector3(-5f, 1f, -1.5f), new Vector3(1.6f, 0.8f, 0.2f), 0f, pieces.transform);
            Box(CeremonySets.SofaName, new Vector3(-1.4f, 0f, -4.8f), new Vector3(1.57f, 0.80f, 0.66f), 270f, pieces.transform);
            var cushion = Box("bb_set_cushion", new Vector3(-1.4f, 0.54f, -4.3f), new Vector3(0.4f, 0.12f, 0.4f), 270f, pieces.transform);
            cushion.transform.position = new Vector3(-1.4f, 0.6f, -4.3f);
            return scene;
        }

        private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>
        /// The living room as the gallery (MOCKUP-PASS-PLAN M21), at the scene's numbers: the U's base
        /// of two four-seat couches at z -2.3 facing south, an arm each side facing in, the red chairs
        /// facing the U, the room's own screen behind them on the south wall.
        /// </summary>
        private static Scene GalleryHouse()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var pieces = new GameObject("Set Pieces");
            SceneManager.MoveGameObjectToScene(pieces, scene);
            var living = new GameObject("Living marker");
            SceneManager.MoveGameObjectToScene(living, scene);
            living.transform.position = new Vector3(-5f, 0f, -7.6f);
            living.AddComponent<HouseRoomMarker>().Configure("Living");
            var floor = new GameObject(CeremonySets.LivingFloorName);
            SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(-7f, -0.15f, -5f);
            floor.AddComponent<BoxCollider>().size = new Vector3(14f, 0.3f, 10f);
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
            Box("bb_set_lowtable", new Vector3(-8f, 0f, -5.6f), new Vector3(0.95f, 0.45f, 0.95f), 0f, pieces.transform);
            return scene;
        }

        /// <summary>
        /// On the gallery the whole house sits: fourteen couch seats and the two red chairs, all
        /// anchors on the pieces where they stand. The chairs face the U and are approached from their
        /// outer sides; the couch seats face their own way and are approached from in front, clear
        /// of the couch's baked edge; slots fill the base from its middle outward, then the arms from
        /// the base's end. Nothing is cloned, turned or moved.
        /// </summary>
        [Test]
        public void TheGallerySeatsTheWholeHouseOnTheCouchesAndTheNomineesInRed()
        {
            var scene = GalleryHouse();
            try
            {
                var pieces = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name.StartsWith("bb_set_", System.StringComparison.Ordinal) || t.name == CeremonySets.LivingScreenName).ToList();
                var before = pieces.Select(t => (t.position, t.rotation)).ToList();
                CeremonySeating.Ensure(scene, 16);

                var hot = CeremonySeating.Anchors(scene, CeremonySeating.HotSeat);
                Assert.That(hot.Count, Is.EqualTo(2), "The two red chairs are the hot seats.");
                Assert.That(hot[0].Position.x, Is.LessThan(hot[1].Position.x), "numbered west to east");
                foreach (var seat in hot)
                {
                    Assert.That(seat.Seated, Is.True);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(seat.Facing, 0f)), Is.LessThan(1f), "facing the U, north");
                    Assert.That(seat.SeatContact.y, Is.EqualTo(CeremonySets.WingbackSeatHeight).Within(0.01f), "at the chair's seat");
                    Assert.That(Flat(seat.Approach, seat.Position), Is.EqualTo(CeremonySets.WingbackApproach).Within(0.01f));
                    Assert.That(Mathf.Abs(seat.Approach.z - seat.Position.z), Is.LessThan(0.01f), "approached from the side,");
                }
                Assert.That(hot[0].Approach.x, Is.LessThan(hot[0].Position.x), "the west chair from the west,");
                Assert.That(hot[1].Approach.x, Is.GreaterThan(hot[1].Position.x), "the east chair from the east.");

                var gallery = CeremonySeating.Anchors(scene, CeremonySeating.GallerySeat);
                Assert.That(gallery.Count, Is.EqualTo(14), "Fourteen couch seats: with the red chairs, the largest house.");
                Assert.That(gallery.Select(s => s.Slot), Is.EqualTo(Enumerable.Range(0, 14)), "numbered in order");
                var expected = new[]
                {
                    new Vector2(-8.66f, -2.4f), new Vector2(-7.34f, -2.4f), new Vector2(-9.42f, -2.4f), new Vector2(-6.58f, -2.4f),
                    new Vector2(-10.18f, -2.4f), new Vector2(-5.82f, -2.4f), new Vector2(-10.94f, -2.4f), new Vector2(-5.06f, -2.4f),
                    new Vector2(-11.68f, -4.84f), new Vector2(-4.32f, -4.84f), new Vector2(-11.68f, -5.6f), new Vector2(-4.32f, -5.6f),
                    new Vector2(-11.68f, -6.36f), new Vector2(-4.32f, -6.36f),
                };
                for (int i = 0; i < gallery.Count; i++)
                {
                    var seat = gallery[i];
                    Assert.That(new Vector2(seat.Position.x, seat.Position.z).x, Is.EqualTo(expected[i].x).Within(0.02f), "seat " + i + " x: the base from its middle out, then the arms from the base's end");
                    Assert.That(seat.Position.z, Is.EqualTo(expected[i].y).Within(0.02f), "seat " + i + " z");
                    Assert.That(seat.Seated, Is.True);
                    Assert.That(seat.SeatContact.y, Is.EqualTo(CeremonySets.GallerySeatHeight).Within(0.01f), "at the cushion");
                    float facing = i < 8 ? 180f : seat.Position.x < -8f ? 90f : 270f;
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(seat.Facing, facing)), Is.LessThan(1f), "seat " + i + " faces " + facing);
                    Assert.That(Flat(seat.Approach, seat.Position), Is.EqualTo(CeremonySets.GalleryApproach).Within(0.01f), "approached from in front");
                }
                var approaches = gallery.Select(s => s.Approach).Concat(hot.Select(s => s.Approach)).ToList();
                for (int i = 0; i < approaches.Count; i++)
                    for (int j = i + 1; j < approaches.Count; j++)
                        Assert.That(Flat(approaches[i], approaches[j]), Is.GreaterThanOrEqualTo(0.7f), "approaches " + i + " and " + j + " stand apart");

                Assert.That(CeremonySeating.Anchors(scene, CeremonySeating.SofaSeat), Is.Empty, "No sofa seats beside the gallery.");
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToList();
                Assert.That(all.Count(t => t.name == CeremonySets.LivingScreenName), Is.EqualTo(1), "The room's own screen, not a clone.");
                Assert.That(all.Any(t => t.name == CeremonySets.ChairName + " (hot seat)"), Is.False, "No hot seat is cloned.");
                for (int i = 0; i < pieces.Count; i++)
                {
                    Assert.That(Flat(pieces[i].position, before[i].position), Is.LessThan(0.001f), pieces[i].name + " did not move");
                    Assert.That(Quaternion.Angle(pieces[i].rotation, before[i].rotation), Is.LessThan(0.01f), pieces[i].name + " did not turn");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheTableSeatsEveryoneWhoDrawsAKeyWithTheHeadLeftOpen([Values(6, 8, 12, 16)] int houseguests)
        {
            var scene = DressedHouse();
            try
            {
                CeremonySeating.Ensure(scene, houseguests);
                var seats = CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat);
                Assert.That(seats.Count, Is.EqualTo(houseguests - 1), "A chair for everyone who draws a key: the house less its Head of Household.");
                Assert.That(seats.Select(s => s.Slot), Is.EqualTo(Enumerable.Range(0, houseguests - 1)), "numbered in order");
                var chairs = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(false))
                    .Where(t => t.name == CeremonySets.ChairName && t.gameObject.activeInHierarchy).ToList();
                Assert.That(chairs.Count, Is.EqualTo(houseguests - 1), "as many chairs standing as seats, the extras struck or cloned");
                float radius = CeremonySets.RingRadiusFor(houseguests - 1);
                foreach (var seat in seats)
                {
                    Assert.That(Flat(seat.Position, TableCentre), Is.EqualTo(radius).Within(0.02f), "on the ring");
                    Assert.That(seat.Seated, Is.True);
                    var toTable = TableCentre - seat.Position; toTable.y = 0f;
                    float facingError = Mathf.Abs(Mathf.DeltaAngle(seat.Facing, Mathf.Atan2(toTable.x, toTable.z) * Mathf.Rad2Deg));
                    Assert.That(facingError, Is.LessThan(1f), "facing the table");
                    Assert.That(Flat(seat.Approach, seat.Position), Is.EqualTo(CeremonySets.SeatApproach).Within(0.01f), "approached from behind");
                    Assert.That(Flat(seat.Approach, TableCentre), Is.GreaterThan(Flat(seat.Position, TableCentre)), "from the outside of the ring");
                    Assert.That(seat.SeatContact.y, Is.EqualTo(0.90f * CeremonySets.SeatShare).Within(0.02f), "the seat at the chair's own height");
                }
                for (int i = 0; i < seats.Count; i++)
                    for (int j = i + 1; j < seats.Count; j++)
                        Assert.That(Flat(seats[i].Position, seats[j].Position), Is.GreaterThanOrEqualTo(CeremonySets.ChairPitch - 0.01f),
                            "chairs " + i + " and " + j + " have room between them");
                // The head: the side nearest the screen, kept open, with the two standing marks in front of it.
                var toScreen = ScreenAt - TableCentre; toScreen.y = 0f;
                foreach (var seat in seats)
                {
                    var toSeat = seat.Position - TableCentre; toSeat.y = 0f;
                    Assert.That(Vector3.Angle(toScreen, toSeat), Is.GreaterThan(CeremonySets.RingStep(houseguests - 1) * 0.99f),
                        "no chair sits in the head's slot");
                }
                var heads = CeremonySeating.Anchors(scene, CeremonySeating.NominationHead);
                Assert.That(heads.Count, Is.EqualTo(2), "The Head of Household's mark and the veto holder's.");
                foreach (var head in heads)
                {
                    Assert.That(head.Seated, Is.False);
                    var toHead = head.Position - TableCentre; toHead.y = 0f;
                    Assert.That(Vector3.Angle(toScreen, toHead), Is.LessThan(40f), "between the table and the screen");
                    Assert.That(Flat(head.Position, TableCentre), Is.GreaterThan(radius + 0.3f), "clear of the chairs");
                    Assert.That(Flat(head.Position, ScreenAt), Is.GreaterThan(0.8f), "and of the screen");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ADressedHouseIsLeftAloneAndASmallerOneStrikesItsExtraChairs()
        {
            var scene = DressedHouse();
            try
            {
                CeremonySeating.Ensure(scene, 12);
                var cloned = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                    .Count(t => t.name == CeremonySets.ChairName);
                Assert.That(cloned, Is.EqualTo(11), "Five chairs cloned for a twelve-house.");
                var before = CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat);
                CeremonySeating.Ensure(scene, 12);
                var again = CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat);
                Assert.That(again.Count, Is.EqualTo(before.Count));
                for (int i = 0; i < before.Count; i++)
                    Assert.That(ReferenceEquals(again[i], before[i]), Is.True, "Dressed for the same house again, nothing moves.");

                CeremonySeating.Ensure(scene, 6);
                var seats = CeremonySeating.Anchors(scene, CeremonySeating.NominationSeat);
                Assert.That(seats.Count, Is.EqualTo(5), "A six-house draws five keys.");
                var standing = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(false))
                    .Count(t => t.name == CeremonySets.ChairName && t.gameObject.activeInHierarchy);
                Assert.That(standing, Is.EqualTo(5), "and the extra chairs are struck, not left in the room.");
                Assert.That(CeremonySets.RingRadiusFor(5), Is.EqualTo(CeremonySets.RingRadius), "The dressed ring for a small house");
                Assert.That(CeremonySets.RingRadiusFor(15), Is.GreaterThan(CeremonySets.RingRadiusFor(11)).And.GreaterThan(1.8f), "and a wider one for a big one.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void TheLivingRoomGetsItsScreenTheHotSeatsAndTheSofaTurnedToFaceIt()
        {
            var scene = DressedHouse();
            try
            {
                CeremonySeating.Ensure(scene, 8);
                var all = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).ToList();
                var screen = all.FirstOrDefault(t => t.name == CeremonySets.LivingScreenName);
                Assert.That(screen, Is.Not.Null, "The living room has a screen of its own, cloned from the set's.");
                Assert.That(all.Count(t => t.name == ScreenSurface.PropName), Is.EqualTo(1), "and the nomination room keeps the only one of that exact name, for the station.");
                Assert.That(all.First(t => t.name == CeremonySets.TelevisionName).gameObject.activeSelf, Is.False, "The prototype's television goes");
                Assert.That(all.First(t => t.name == CeremonySets.TelevisionConsoleName).gameObject.activeSelf, Is.False, "with its console.");
                Assert.That(Mathf.Abs(screen.position.x - -5f), Is.LessThan(0.01f), "in the television's place");
                Assert.That(screen.position.z, Is.LessThan(0f).And.GreaterThan(-2.5f), "at the room's far side");
                Assert.That(ScreenSurface.TryFind(scene, "Living", out var face), Is.True, "and the stage can find it.");
                Assert.That(face.Normal.z, Is.LessThan(-0.9f), "facing into the room");

                var hot = CeremonySeating.Anchors(scene, CeremonySeating.HotSeat);
                Assert.That(hot.Count, Is.EqualTo(2), "Two hot seats.");
                foreach (var seat in hot)
                {
                    Assert.That(seat.Seated, Is.True);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(seat.Facing, 0f)), Is.LessThan(1f), "facing the screen");
                    Assert.That(seat.Position.z, Is.LessThan(face.Centre.z - 1.0f), "in front of it");
                    Assert.That(seat.Approach.z, Is.LessThan(seat.Position.z), "approached from behind");
                }
                Assert.That(Flat(hot[0].Position, hot[1].Position), Is.EqualTo(CeremonySets.HotSeatGap).Within(0.01f), "side by side");

                var sofa = all.First(t => t.name == CeremonySets.SofaName);
                float wanted = Mathf.Atan2(face.Centre.x - sofa.position.x, face.Centre.z - sofa.position.z) * Mathf.Rad2Deg + 180f;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(sofa.eulerAngles.y, wanted)), Is.LessThan(1f), "The sofa is turned to face the screen");
                var cushion = all.First(t => t.name == "bb_set_cushion");
                Assert.That(Flat(cushion.position, sofa.position), Is.EqualTo(0.5f).Within(0.02f), "with its cushion still on it");
                var toCushion = cushion.position - sofa.position; toCushion.y = 0f;
                Assert.That(Mathf.Abs(Vector3.Dot(toCushion.normalized, (Quaternion.Euler(0f, sofa.eulerAngles.y, 0f) * Vector3.forward))), Is.LessThan(0.05f),
                    "along its width, not in front of it");
                var sofaSeats = CeremonySeating.Anchors(scene, CeremonySeating.SofaSeat);
                Assert.That(sofaSeats.Count, Is.EqualTo(3), "Three seats on the sofa,");
                foreach (var seat in sofaSeats)
                {
                    Assert.That(seat.Seated, Is.True);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(seat.Facing, wanted - 180f)), Is.LessThan(1f), "facing the way the sofa faces");
                    Assert.That(Flat(seat.Approach, seat.Position), Is.EqualTo(CeremonySets.SeatApproach).Within(0.01f), "approached from the front");
                    Assert.That(Flat(seat.Position, sofa.position), Is.LessThan(0.6f), "on the sofa");
                }
                var marks = CeremonySeating.Anchors(scene, CeremonySeating.LivingMark);
                Assert.That(marks.All(m => !m.Seated), Is.True, "The standing marks stand.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}

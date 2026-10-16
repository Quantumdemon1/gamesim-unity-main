using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Gamesim.Editor;
using Gamesim.House;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The owner's arrangement of the bedroom, the Head of Household's ensuite and the game room's
    /// study (2026-09-23, re-created 2026-10-15; the editor's HouseRoomArrangement says where each
    /// number came from), as the episode scene holds it. Pinned by where each piece's bounds are
    /// centred and which way it faces, because those are what the owner placed; and with the rules
    /// that let it stand: each moved piece carries its collision with it on the furniture layer,
    /// stands clear of every wall, piece and doorway, and costs the house no route.
    ///
    /// <para>Every check returns what it found wrong rather than asserting, so the arrangement can
    /// be broken on purpose and the checks shown to notice.</para>
    /// </summary>
    public sealed class HouseRoomArrangementTests
    {
        private const string EpisodeScene = HouseRoomFinish.EpisodeScene;
        private const float Placed = 0.02f;   // metres: where the plan puts a piece's bounds centre
        private const float Turned = 0.05f;   // degrees

        /// <summary>A piece the arrangement placed: its plan name, its room, its bounds centre (x, z) and its yaw.</summary>
        internal readonly struct Placement
        {
            public readonly string Name, Room;
            public readonly Vector2 Centre;
            public readonly float Yaw;
            public readonly bool Floor;
            public Placement(string name, string room, float x, float z, float yaw, bool floor = true)
            {
                Name = name; Room = room; Centre = new Vector2(x, z); Yaw = yaw; Floor = floor;
            }
            public override string ToString() => Name + " (" + Room + ")";
        }

        internal static readonly Placement[] Bedroom =
        {
            new Placement("bedDouble", "Bedroom floor", -13.10f, 0.89f, 90f),
            new Placement("bedDouble", "Bedroom floor", -13.12f, 2.62f, 90f),
            new Placement("cabinetBedDrawer", "Bedroom floor", -13.70f, 1.84f, 90f),
            new Placement("bookcaseOpen", "Bedroom floor", -0.48f, 7.27f, 90f),
        };

        internal static readonly Placement[] Ensuite =
        {
            new Placement("toilet", "HoH floor", -5.96f, -18.98f, 180f),
            new Placement("bathroomSink", "HoH floor", -6.71f, -19.15f, 180f),
            new Placement("showerRound", "HoH floor", -5.38f, -13.04f, 270f),
            new Placement("loungeDesignSofa", "HoH floor", -13.249f, -17.00f, 273.198f),
            new Placement("speaker", "HoH floor", -5.23f, -12.43f, 90f),
        };

        internal static readonly Placement[] Study =
        {
            new Placement("loungeDesignChair", "Games floor", 11.898f, -18.60f, 270f),
            new Placement("bb_set_laptop", "Games floor", 13.212f, -18.30f, 82.286f, false),
            new Placement("bb_set_bookstack", "Games floor", 13.212f, -19.00f, 90f, false),
            new Placement("loungeChairRelax", "Games floor", 5.82f, -13.40f, 270f),
        };

        /// <summary>The prototype's pieces the bedroom no longer has.</summary>
        private static readonly string[] Struck = { "Bed frame", "Bed frame (model)", "Duvet", "Pillow", "Wardrobe", "Wardrobe (model)" };

        /// <summary>How far in front of a doorway's gap, either side of its wall, a piece may not stand: a stride.</summary>
        private const float DoorwayStride = 0.6f;

        [Test]
        public void TheBedroomHasTheOwnersSouthWestCornerAndBookcase() => WithHouse(scene => Assert.That(BedroomProblems(scene), Is.Empty));

        [Test]
        public void TheEnsuiteHasItsFixturesOnTheSouthWallAndTheShowerClearOfTheDoor() => WithHouse(scene => Assert.That(EnsuiteProblems(scene), Is.Empty));

        [Test]
        public void TheStudyCornerFacesItsDesk() => WithHouse(scene => Assert.That(StudyProblems(scene), Is.Empty));

        [Test]
        public void NoPlantFloatsOverARaisedBed() => WithHouse(scene => Assert.That(PlantingProblems(scene), Is.Empty));

        [Test]
        public void EveryMovedPieceCarriesItsCollisionWithItOnTheFurnitureLayer() => WithHouse(scene => Assert.That(CollisionProblems(scene), Is.Empty));

        [Test]
        public void NoMovedPieceStandsInAWallAnotherPieceOrADoorway() => WithHouse(scene => Assert.That(ClearanceProblems(scene), Is.Empty));

        internal static void WithHouse(Action<Scene> check)
        {
            var scene = EditorSceneManager.OpenPreviewScene(EpisodeScene);
            try { check(scene); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        internal static List<string> BedroomProblems(Scene scene)
        {
            var problems = new List<string>();
            var found = Find(scene, Bedroom, problems);
            var floor = Floor(scene, "Bedroom floor");
            var westWall = Wall(scene, "West wall");
            var beds = found.Where(p => p.Key.Name == "bedDouble").Select(p => p.Value).Where(t => t != null).ToList();
            foreach (var bed in beds)
            {
                // A bed's head is its local -z (every bed script puts the headboard there).
                var head = bed.rotation * Vector3.back;
                if (head.x > -0.99f) problems.Add("A double bed's head points " + head.ToString("F2") + ", not at the west wall.");
                if (westWall.HasValue && Art(bed).min.x - westWall.Value.max.x > 0.25f)
                    problems.Add("A double bed stands " + (Art(bed).min.x - westWall.Value.max.x).ToString("F2", CultureInfo.InvariantCulture) + " m off the west wall.");
            }
            var drawer = found.FirstOrDefault(p => p.Key.Name == "cabinetBedDrawer").Value;
            if (drawer != null && beds.Count == 2)
            {
                var b = Art(drawer);
                var low = beds.Select(Art).OrderBy(x => x.center.z).ToList();
                if (!(b.min.z >= low[0].max.z && b.max.z <= low[1].min.z)) problems.Add("The bedside drawer is not between the double beds.");
            }
            var bookcase = found.FirstOrDefault(p => p.Key.Name == "bookcaseOpen").Value;
            if (bookcase != null && (bookcase.rotation * Vector3.back).x > -0.99f)
                problems.Add("The bookcase's open front faces " + (bookcase.rotation * Vector3.back).ToString("F2") + ", not the room.");
            // The room finish's corkboard hangs on the same partition, and not behind the bookcase.
            var corkboard = All(scene).FirstOrDefault(t => t.name == "Corkboard" && t.parent != null && t.parent.name == "Bedroom"
                && t.parent.parent != null && t.parent.parent.name == HouseRoomFinish.RootName);
            if (corkboard == null) problems.Add("No corkboard in the bedroom's finish.");
            else if (bookcase != null)
            {
                var cb = Art(corkboard); var bb = Art(bookcase);
                if (cb.max.z > bb.min.z && cb.min.z < bb.max.z && bb.min.x - cb.max.x < 0.8f && cb.min.x - bb.max.x < 0.8f)
                    problems.Add("The bookcase stands in front of the corkboard.");
            }

            // The prototype's bed frames, their boxes and the wardrobe are struck: nothing of them
            // is seen, and no collider of theirs reaches the bake.
            foreach (var t in All(scene))
            {
                if (!Struck.Contains(t.name) || !t.gameObject.activeInHierarchy || !InRoom(floor, t.position)) continue;
                problems.Add("The prototype's '" + t.name + "' is still in the bedroom at " + t.position.ToString("F2") + ".");
            }
            return problems;
        }

        internal static List<string> EnsuiteProblems(Scene scene)
        {
            var problems = new List<string>();
            var found = Find(scene, Ensuite, problems);
            var floor = Floor(scene, "HoH floor");
            foreach (var name in new[] { "toilet", "bathroomSink" })
            {
                var piece = found.FirstOrDefault(p => p.Key.Name == name).Value;
                if (piece == null) continue;
                // The authored fixtures face their local -z, their backs (the cistern, the splashback) at +z.
                var front = piece.rotation * Vector3.back;
                if (front.z < 0.99f) problems.Add(name + " faces " + front.ToString("F2") + ", not into the suite from the south wall.");
            }

            var shower = found.FirstOrDefault(p => p.Key.Name == "showerRound").Value;
            var door = SetPieces(scene)?.Cast<Transform>().FirstOrDefault(t => t.name == "bb_set_hohdoor" && t.gameObject.activeInHierarchy);
            if (door == null) problems.Add("No Head of Household door to measure the shower against.");
            else if (shower != null)
            {
                // The door's open leaf swings into the suite from the north jamb; no part of the
                // door may be inside the shower.
                var sb = Art(shower);
                sb.Expand(-0.02f);
                int inside = 0;
                foreach (var filter in door.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;
                    var toWorld = filter.transform.localToWorldMatrix;
                    foreach (var v in filter.sharedMesh.vertices)
                        if (sb.Contains(toWorld.MultiplyPoint3x4(v))) inside++;
                }
                if (inside > 0) problems.Add("The Head of Household door has " + inside + " vertices inside the shower.");
            }

            // The speaker the owner carried in stands on the floor, clear of the shower.
            var speakers = SetPieces(scene)?.Cast<Transform>().Where(t => t.name == "speaker" && t.gameObject.activeInHierarchy).ToList() ?? new List<Transform>();
            var suiteSpeakers = speakers.Where(t => InRoom(floor, Art(t).center)).ToList();
            if (suiteSpeakers.Count != 1) problems.Add("The suite has " + suiteSpeakers.Count + " speakers, not the one the owner carried in.");
            foreach (var speaker in suiteSpeakers)
            {
                var b = Art(speaker);
                if (floor.HasValue && Mathf.Abs(b.min.y - floor.Value.max.y) > 0.02f)
                    problems.Add("The suite's speaker stands at " + b.min.y.ToString("F2", CultureInfo.InvariantCulture) + ", not on the floor.");
                if (shower != null && OverlapXZ(b, Art(shower)) > -0.03f)
                    problems.Add("The suite's speaker is not clear of the shower.");
            }
            var nomination = Floor(scene, "Nomination floor");
            var nominationSpeakers = speakers.Where(t => InRoom(nomination, Art(t).center)).ToList();
            if (nominationSpeakers.Count != 1 || (nomination.HasValue && Art(nominationSpeakers[0]).center.x < nomination.Value.center.x))
                problems.Add("The nomination room should keep its east speaker alone; it has " + nominationSpeakers.Count + ".");
            return problems;
        }

        internal static List<string> StudyProblems(Scene scene)
        {
            var problems = new List<string>();
            var found = Find(scene, Study, problems);
            var games = Floor(scene, "Games floor");
            var desk = SetPieces(scene)?.Cast<Transform>().FirstOrDefault(t => t.name == "desk" && t.gameObject.activeInHierarchy && InRoom(games, t.position));
            var chair = found.FirstOrDefault(p => p.Key.Name == "loungeDesignChair").Value;
            var laptop = found.FirstOrDefault(p => p.Key.Name == "bb_set_laptop").Value;
            if (desk == null) { problems.Add("No desk in the game room."); return problems; }
            var top = Art(desk).max.y;
            if (chair != null)
            {
                var toDesk = Flat(Art(desk).center - Art(chair).center).normalized;
                if (Vector3.Dot(Flat(chair.rotation * Vector3.back).normalized, toDesk) < 0.95f) problems.Add("The desk chair does not face the desk.");
                if (Mathf.Abs(Vector3.Angle(chair.up, Vector3.up)) > 0.1f) problems.Add("The desk chair is tilted.");
            }
            if (laptop != null && chair != null)
            {
                var toChair = Flat(Art(chair).center - Art(laptop).center).normalized;
                if (Vector3.Dot(Flat(laptop.rotation * Vector3.back).normalized, toChair) < 0.95f) problems.Add("The laptop's screen does not face the chair.");
            }
            foreach (var onTheDesk in found.Where(p => !p.Key.Floor && p.Value != null))
                if (Mathf.Abs(Art(onTheDesk.Value).min.y - top) > 0.03f) problems.Add(onTheDesk.Key + " is not on the desk's top.");
            var lounge = found.FirstOrDefault(p => p.Key.Name == "loungeChairRelax").Value;
            if (lounge != null && (lounge.rotation * Vector3.back).x < 0.99f) problems.Add("The lounge chair faces the west divider rather than the room.");
            return problems;
        }

        internal static List<string> PlantingProblems(Scene scene)
        {
            var problems = new List<string>();
            var planting = SetPieces(scene)?.Find("Planting");
            if (planting == null) { problems.Add("No planting under the set pieces."); return problems; }
            foreach (Transform plant in planting)
            {
                if (!plant.gameObject.activeInHierarchy) continue;
                if (plant.name.StartsWith("plantSmall", StringComparison.Ordinal)) problems.Add("A raised bed grew " + plant.name + " at " + plant.position.ToString("F2") + ".");
                var b = Art(plant);
                if (b.min.y > 0.05f) problems.Add(plant.name + " floats " + b.min.y.ToString("F2", CultureInfo.InvariantCulture) + " m up at " + plant.position.ToString("F2") + ".");
            }
            return problems;
        }

        internal static List<string> CollisionProblems(Scene scene)
        {
            var problems = new List<string>();
            foreach (var pair in Find(scene, Bedroom.Concat(Ensuite).Concat(Study).Where(p => p.Floor && p.Name != "speaker").ToArray(), problems))
            {
                var piece = pair.Value;
                if (piece == null) continue;
                var proxy = piece.Cast<Transform>().FirstOrDefault(HouseFurnitureCollision.IsProxy);
                var box = proxy != null ? proxy.GetComponent<BoxCollider>() : null;
                if (box == null) { problems.Add(pair.Key + " has no fitted collision."); continue; }
                if (proxy.name != HouseFurnitureCollision.ChildName || !proxy.gameObject.activeInHierarchy || !box.enabled)
                    problems.Add(pair.Key + "'s collision is switched off ('" + proxy.name + "'): it costs the house a route, an approach or a binding.");
                if (proxy.gameObject.layer != HouseLayers.Furniture) problems.Add(pair.Key + "'s collision is on layer " + proxy.gameObject.layer + ".");
                var world = WorldBox(box);
                var art = Art(piece);
                if ((Flat(world.center) - Flat(art.center)).magnitude > 0.05f || Mathf.Abs(world.size.x - art.size.x) > 0.05f || Mathf.Abs(world.size.z - art.size.z) > 0.05f)
                    problems.Add(pair.Key + "'s collision (" + world.center.ToString("F2") + " " + world.size.ToString("F2") + ") has not moved with it (" + art.center.ToString("F2") + " " + art.size.ToString("F2") + ").");
            }
            return problems;
        }

        internal static List<string> ClearanceProblems(Scene scene)
        {
            var problems = new List<string>();
            var moved = Find(scene, Bedroom.Concat(Ensuite).Concat(Study).Where(p => p.Floor).ToArray(), problems)
                .Where(p => p.Value != null).ToList();
            var colliders = All(scene).Where(t => t.gameObject.activeInHierarchy)
                .SelectMany(t => t.GetComponents<BoxCollider>()).Where(c => c.enabled && !c.isTrigger && c.GetComponent<HouseWalkable>() == null).ToList();
            foreach (var pair in moved)
            {
                var piece = pair.Value;
                var art = Art(piece);
                var mine = Rect2(piece);
                foreach (var other in colliders)
                {
                    if (other.transform.IsChildOf(piece)) continue;
                    var ob = WorldBox(other);
                    if (ob.max.y < art.min.y + 0.02f || ob.min.y > art.max.y) continue;
                    if (Overlap(mine, Rect2(other)) > 0.005f) problems.Add(pair.Key + " stands in " + Describe(other) + ".");
                }
            }

            // A doorway is the gap between a wall's two segments, and a stride either side of it.
            foreach (var doorway in Doorways(colliders))
                foreach (var pair in moved)
                    if (Overlap(Rect2(pair.Value), doorway.Value) > 0f)
                        problems.Add(pair.Key + " stands in the doorway through '" + doorway.Key + "'.");
            return problems;
        }

        // ----------------------------------------------------------------------------- helpers

        private static IEnumerable<Transform> All(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true));

        private static Transform SetPieces(Scene scene)
            => All(scene).FirstOrDefault(t => t.name == HouseSetPieces.RootName && t.parent != null && t.parent.name == "House Architecture");

        private static Bounds? Floor(Scene scene, string name)
        {
            var floor = All(scene).FirstOrDefault(t => t.name == name && t.GetComponent<Renderer>() != null);
            return floor != null ? floor.GetComponent<Renderer>().bounds : (Bounds?)null;
        }

        private static Bounds? Wall(Scene scene, string name)
        {
            var wall = All(scene).FirstOrDefault(t => t.name == name && t.GetComponent<BoxCollider>() != null && t.gameObject.activeInHierarchy);
            return wall != null ? WorldBox(wall.GetComponent<BoxCollider>()) : (Bounds?)null;
        }

        private static bool InRoom(Bounds? floor, Vector3 p)
            => floor.HasValue && p.x >= floor.Value.min.x && p.x <= floor.Value.max.x && p.z >= floor.Value.min.z && p.z <= floor.Value.max.z;

        /// <summary>Each placement's piece: the active set piece of its name whose bounds are centred nearest its place in its room.</summary>
        private static List<KeyValuePair<Placement, Transform>> Find(Scene scene, Placement[] placements, List<string> problems)
        {
            var result = new List<KeyValuePair<Placement, Transform>>();
            var root = SetPieces(scene);
            if (root == null) { problems.Add("No set pieces under the house."); return result; }
            foreach (var placement in placements)
            {
                var floor = Floor(scene, placement.Room);
                var piece = root.Cast<Transform>()
                    .Where(t => t.name == placement.Name && t.gameObject.activeInHierarchy && InRoom(floor, Art(t).center))
                    .OrderBy(t => (new Vector2(Art(t).center.x, Art(t).center.z) - placement.Centre).sqrMagnitude)
                    .FirstOrDefault();
                result.Add(new KeyValuePair<Placement, Transform>(placement, piece));
                if (piece == null) { problems.Add("No " + placement + " in the house."); continue; }
                var centre = Art(piece).center;
                float off = (new Vector2(centre.x, centre.z) - placement.Centre).magnitude;
                if (off > Placed)
                    problems.Add(placement + " is centred at (" + centre.x.ToString("F3", CultureInfo.InvariantCulture) + ", " + centre.z.ToString("F3", CultureInfo.InvariantCulture)
                        + "), " + off.ToString("F3", CultureInfo.InvariantCulture) + " m from (" + placement.Centre.x.ToString("F3", CultureInfo.InvariantCulture) + ", "
                        + placement.Centre.y.ToString("F3", CultureInfo.InvariantCulture) + ").");
                float turn = Mathf.Abs(Mathf.DeltaAngle(piece.eulerAngles.y, placement.Yaw));
                if (turn > Turned)
                    problems.Add(placement + " is turned to " + piece.eulerAngles.y.ToString("F3", CultureInfo.InvariantCulture) + ", not " + placement.Yaw.ToString("F3", CultureInfo.InvariantCulture) + ".");
                if (placement.Floor && Mathf.Abs(Art(piece).min.y - (floor?.max.y ?? 0f)) > 0.02f)
                    problems.Add(placement + " does not stand on the floor.");
            }
            return result;
        }

        private static Bounds Art(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>A box collider's world bounds, from its transform: an edit-mode preview scene runs no physics step to ask.</summary>
        private static Bounds WorldBox(BoxCollider box)
        {
            var m = box.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(box.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                b.Encapsulate(m.MultiplyPoint3x4(box.center + Vector3.Scale(box.size * 0.5f,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
            return b;
        }

        /// <summary>A footprint on the floor: a centre, two unit axes and the half-lengths along them.</summary>
        private readonly struct Footprint
        {
            public readonly Vector2 Centre, U, V;
            public readonly float HalfU, HalfV;
            public Footprint(Vector2 centre, Vector2 u, Vector2 v, float halfU, float halfV) { Centre = centre; U = u; V = v; HalfU = halfU; HalfV = halfV; }
        }

        /// <summary>A piece's footprint: its art's bounds, measured in the piece's own turned frame.</summary>
        private static Footprint Rect2(Transform piece)
        {
            var right = Flat(piece.right).normalized;
            var forward = Flat(piece.forward).normalized;
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var r in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var lb = r.localBounds;
                var m = r.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    float u = Vector3.Dot(p, right), v = Vector3.Dot(p, forward);
                    minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u); minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
                }
            }
            var centre = right * ((minU + maxU) * 0.5f) + forward * ((minV + maxV) * 0.5f);
            return new Footprint(new Vector2(centre.x, centre.z), new Vector2(right.x, right.z), new Vector2(forward.x, forward.z), (maxU - minU) * 0.5f, (maxV - minV) * 0.5f);
        }

        private static Footprint Rect2(BoxCollider box)
        {
            var t = box.transform;
            var centre = t.TransformPoint(box.center);
            var u = Flat(t.right).normalized; var v = Flat(t.forward).normalized;
            float halfU = Mathf.Abs(t.lossyScale.x * box.size.x) * 0.5f, halfV = Mathf.Abs(t.lossyScale.z * box.size.z) * 0.5f;
            return new Footprint(new Vector2(centre.x, centre.z), new Vector2(u.x, u.z), new Vector2(v.x, v.z), halfU, halfV);
        }

        private static Footprint Rect2(Bounds b) => new Footprint(new Vector2(b.center.x, b.center.z), Vector2.right, Vector2.up, b.extents.x, b.extents.z);

        /// <summary>How deep two footprints overlap along the shallowest separating axis: negative means a gap that wide.</summary>
        private static float Overlap(Footprint a, Footprint b)
        {
            float depth = float.MaxValue;
            foreach (var axis in new[] { a.U, a.V, b.U, b.V })
            {
                float ra = a.HalfU * Mathf.Abs(Vector2.Dot(a.U, axis)) + a.HalfV * Mathf.Abs(Vector2.Dot(a.V, axis));
                float rb = b.HalfU * Mathf.Abs(Vector2.Dot(b.U, axis)) + b.HalfV * Mathf.Abs(Vector2.Dot(b.V, axis));
                float distance = Mathf.Abs(Vector2.Dot(b.Centre - a.Centre, axis));
                depth = Mathf.Min(depth, ra + rb - distance);
            }
            return depth;
        }

        private static float OverlapXZ(Bounds a, Bounds b) => Overlap(Rect2(a), Rect2(b));

        private static string Describe(Collider c)
        {
            var t = c.transform;
            return t.name == HouseFurnitureCollision.ChildName && t.parent != null ? t.parent.name + "'s collision" : "'" + t.name + "'";
        }

        /// <summary>
        /// Every doorway: the gap between a wall's two segments, which the house's builders name
        /// "&lt;wall&gt; left" and "right" or "south" and "north", widened a stride either side of the wall.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, Footprint>> Doorways(List<BoxCollider> colliders)
        {
            var segments = colliders.Where(c => c.transform.parent != null).ToList();
            foreach (var (first, second) in new[] { (" left", " right"), (" south", " north") })
                foreach (var a in segments.Where(c => c.name.EndsWith(first, StringComparison.Ordinal)))
                {
                    string wall = a.name.Substring(0, a.name.Length - first.Length);
                    var b = segments.FirstOrDefault(c => c.name == wall + second);
                    if (b == null) continue;
                    var ab = WorldBox(a); var bb = WorldBox(b);
                    bool alongX = ab.size.x > ab.size.z;
                    if (alongX)
                    {
                        float from = Mathf.Min(ab.max.x, bb.max.x), to = Mathf.Max(ab.min.x, bb.min.x);
                        if (to - from < 0.5f) continue;
                        var gap = new Bounds(new Vector3((from + to) * 0.5f, 0f, ab.center.z), new Vector3(to - from, 0f, ab.size.z + 2f * DoorwayStride));
                        yield return new KeyValuePair<string, Footprint>(wall, Rect2(gap));
                    }
                    else
                    {
                        float from = Mathf.Min(ab.max.z, bb.max.z), to = Mathf.Max(ab.min.z, bb.min.z);
                        if (to - from < 0.5f) continue;
                        var gap = new Bounds(new Vector3(ab.center.x, 0f, (from + to) * 0.5f), new Vector3(ab.size.x + 2f * DoorwayStride, 0f, to - from));
                        yield return new KeyValuePair<string, Footprint>(wall, Rect2(gap));
                    }
                }
        }
    }
}

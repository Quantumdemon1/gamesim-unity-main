using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The room scope's registry on the saved house (ROOM-SCOPE-PLAN RS1): every renderer has one
    /// answer, the classifier keeps its rule, the shell stays drawn, the lamps have their rooms, and
    /// the room layers are named and empty. Membership is computed by the rule and never pinned, so a
    /// house with more props moves no number here; only the lamps, which the plan counts, are.
    /// </summary>
    public sealed class HouseRoomScopeTests
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string PrototypeScene = "Assets/Gamesim/Scenes/HousePrototype.unity";
        private const float Tolerance = .001f;

        [Test]
        public void EveryDressingRendererIsInOneRoomOrTheShell()
        {
            WithScope(EpisodeScene, (scene, scope) =>
            {
                var renderers = All<Renderer>(scene);
                var layersBefore = All<Transform>(scene).Select(t => t.gameObject.layer).ToArray();
                var table = new List<HouseRoomScope.Classified>();
                scope.Classify(table);

                // One row per renderer, every renderer in the house, active or not.
                Assert.That(table.Count, Is.EqualTo(renderers.Length), "Every renderer has exactly one row.");
                Assert.That(table.Select(row => row.Renderer).Distinct().Count(), Is.EqualTo(renderers.Length));
                // A table, not applied: no layer moved.
                Assert.That(All<Transform>(scene).Select(t => t.gameObject.layer).ToArray(), Is.EqualTo(layersBefore),
                    "Classifying writes nothing (RS2a applies it).");

                var failures = new List<string>();
                var furnished = new HashSet<int>();
                foreach (var row in table)
                {
                    var renderer = row.Renderer;
                    if ((row.Placement == HouseRoomScope.Placement.Room) != (row.Room >= 0))
                        failures.Add(Name(renderer) + ": " + row.Placement + " with room " + row.Room);
                    bool dressing = IsDressing(renderer);
                    if (!dressing)
                    {
                        if (row.Placement == HouseRoomScope.Placement.Room) failures.Add(Name(renderer) + " is not dressing but is in a room");
                        continue;
                    }
                    if (row.Placement != HouseRoomScope.Placement.Room && row.Placement != HouseRoomScope.Placement.Straddles
                        && row.Placement != HouseRoomScope.Placement.Outside)
                    { failures.Add(Name(renderer) + " is dressing but reads " + row.Placement); continue; }
                    var bounds = renderer.bounds;
                    var centre = new Vector2(bounds.center.x, bounds.center.z);
                    int expected = RuleRoom(scope, bounds);
                    if (row.Room != expected)
                        failures.Add(Name(renderer) + " reads " + Room(row.Room) + "; its bounds say " + Room(expected));
                    if (row.Placement != HouseRoomScope.Placement.Room) continue;
                    // A room's piece stays within the room's floor and growth, and no other floor holds its centre.
                    furnished.Add(row.Room);
                    if (!scope.Has(row.Room)) { failures.Add(Name(renderer) + " is in a room this house lacks"); continue; }
                    if (!InsideGrown(scope.Box(row.Room), bounds, Tolerance))
                        failures.Add(Name(renderer) + " reaches past the " + Room(row.Room) + "'s floor and its growth");
                    for (int other = 0; other < RoomLayers.Count; other++)
                        if (other != row.Room && scope.Has(other) && StrictlyInside(scope.Box(other), centre))
                            failures.Add(Name(renderer) + " is the " + Room(row.Room) + "'s, centred on the " + Room(other) + "'s floor");
                }
                Assert.That(failures, Is.Empty, string.Join("; ", failures.Take(20)));

                // And the rule is not degenerate: every room of the house holds something, and a
                // dressing piece standing thirty centimetres or more inside one floor is that room's.
                for (int room = 0; room < RoomLayers.Count; room++)
                    if (scope.Has(room)) Assert.That(furnished.Contains(room), Is.True, Room(room) + " holds no dressing at all.");
                foreach (var row in table.Where(r => IsDressing(r.Renderer)))
                    for (int room = 0; room < RoomLayers.Count; room++)
                        if (scope.Has(room) && InsideGrown(scope.Box(room), row.Renderer.bounds, -HouseRoomScope.BoxGrowth - .3f))
                            Assert.That(row.Room, Is.EqualTo(room), Name(row.Renderer) + " stands well inside the " + Room(room) + ".");
            });
        }

        [Test]
        public void NoRendererThatSharesItsObjectWithAColliderIsClassified()
        {
            WithScope(EpisodeScene, (scene, scope) =>
            {
                var table = new List<HouseRoomScope.Classified>();
                scope.Classify(table);
                var withColliders = table.Where(row => row.Renderer.GetComponent<Collider>() != null).ToArray();
                Assert.That(withColliders, Is.Not.Empty, "The house's floors carry both a renderer and their collider.");
                foreach (var row in withColliders)
                {
                    Assert.That(row.Room, Is.EqualTo(-1), Name(row.Renderer) + " has a collider: its layer is physics, and it never moves to a room's.");
                    Assert.That(row.Placement, Is.EqualTo(HouseRoomScope.Placement.Collider).Or.EqualTo(HouseRoomScope.Placement.Layer)
                        .Or.EqualTo(HouseRoomScope.Placement.Kind), Name(row.Renderer));
                }
                // Nor anything already on a layer of its own.
                foreach (var row in table.Where(row => row.Renderer.gameObject.layer != 0))
                    Assert.That(row.Room, Is.EqualTo(-1), Name(row.Renderer) + " is on layer " + row.Renderer.gameObject.layer + ".");
            });
        }

        [Test]
        public void TheShellHoldsTheWallsFloorsFencesAndOutlines()
        {
            WithScope(EpisodeScene, (scene, scope) =>
            {
                var table = new List<HouseRoomScope.Classified>();
                scope.Classify(table);
                var byRenderer = table.ToDictionary(row => row.Renderer);
                var all = All<Transform>(scene);

                // Floors.
                var floors = All<HouseWalkable>(scene).Select(floor => floor.GetComponent<Renderer>()).Where(r => r != null).ToArray();
                Assert.That(floors, Is.Not.Empty, "the floors");
                foreach (var floor in floors) Assert.That(byRenderer[floor].Room, Is.EqualTo(-1), Name(floor) + " is the shell's");

                // Walls: the primitives the NavMesh was baked from, by the shape the authored shell
                // replaces (HouseSetPieces.Shell), and the authored shell itself.
                var walls = All<BoxCollider>(scene).Where(WallLike).Select(box => box.GetComponent<Renderer>()).Where(r => r != null).ToArray();
                Assert.That(walls, Is.Not.Empty, "the primitive walls");
                foreach (var wall in walls) Assert.That(byRenderer[wall].Room, Is.EqualTo(-1), Name(wall) + " is the shell's");
                var shell = renderersNamed(HouseRoomScope.ShellMeshName);
                Assert.That(shell, Is.Not.Empty, "the authored shell");
                foreach (var piece in shell) Assert.That(byRenderer[piece].Placement, Is.EqualTo(HouseRoomScope.Placement.Shell), Name(piece));

                // Fences.
                var fences = all.Where(t => t.name.EndsWith("garden fence", StringComparison.Ordinal))
                    .Select(t => t.GetComponent<Renderer>()).Where(r => r != null).ToArray();
                Assert.That(fences, Is.Not.Empty, "the garden fences");
                foreach (var fence in fences) Assert.That(byRenderer[fence].Room, Is.EqualTo(-1), Name(fence) + " is the shell's");

                // Neon outlines, every room's: the north block's and the yard's, and the south wing's own.
                var outlines = table.Where(row => HouseRoomScope.InShellList(row.Renderer.transform)
                    && row.Renderer.name != HouseRoomScope.ShellMeshName).ToArray();
                foreach (var row in outlines) Assert.That(row.Placement, Is.EqualTo(HouseRoomScope.Placement.Shell), Name(row.Renderer));
                for (int room = 0; room < RoomLayers.Count; room++)
                {
                    if (!scope.Has(room)) continue;
                    var box = scope.Box(room);
                    Assert.That(outlines.Any(row => box.Contains(new Vector2(row.Renderer.bounds.center.x, row.Renderer.bounds.center.z))), Is.True,
                        "The " + RoomLayers.RoomIds[room] + "'s neon outline is drawn with the shell.");
                }

                Renderer[] renderersNamed(string name) => all.Where(t => t.name == name).Select(t => t.GetComponent<Renderer>()).Where(r => r != null).ToArray();
            });
        }

        [Test]
        public void TheLampsLiveInTheirRooms()
        {
            WithScope(EpisodeScene, (scene, scope) =>
            {
                var homes = new List<HouseRoomScope.LampHome>();
                scope.LampHomes(homes);
                var expected = new Dictionary<string, int>
                {
                    { "Living", 3 }, { "Bedroom", 3 }, { "HoH", 3 }, { "Nomination", 2 }, { "Private", 2 }, { "Games", 1 }, { "Kitchen", 0 }, { "Yard", 0 },
                };
                Assert.That(homes.All(home => home.Room >= 0), Is.True, "Every lamp stands over a room.");
                Assert.That(homes.All(home => home.Light.type == LightType.Point && home.Light.shadows != LightShadows.None), Is.True,
                    "A lamp is a shadowed point light: no fill, screen light or Moon.");
                foreach (var room in expected)
                    Assert.That(homes.Count(home => RoomLayers.RoomIds[home.Room] == room.Key), Is.EqualTo(room.Value),
                        "Lamps homed in the " + room.Key + ": " + string.Join(", ", homes.Where(h => RoomLayers.RoomIds[h.Room] == room.Key)
                            .Select(h => h.Light.name + " at " + h.Light.transform.position.ToString("F2"))));
                Assert.That(homes.Count, Is.EqualTo(expected.Values.Sum()), "Fourteen lamps.");
            });
        }

        [Test]
        public void TheRoomLayersAreNamedAndUnused()
        {
            for (int i = 0; i < RoomLayers.Count; i++)
            {
                int layer = RoomLayers.First + i;
                Assert.That(LayerMask.LayerToName(layer), Is.EqualTo(RoomLayers.LayerNames[i]),
                    "Layer " + layer + " is the " + RoomLayers.RoomIds[i] + "'s, by name in TagManager.");
                Assert.That(LayerMask.NameToLayer(RoomLayers.LayerNames[i]), Is.EqualTo(layer));
                Assert.That(RoomLayers.LayerOf(RoomLayers.RoomIds[i]), Is.EqualTo(layer));
                Assert.That(RoomLayers.RoomOf(layer), Is.EqualTo(RoomLayers.RoomIds[i]));
            }
            Assert.That(RoomLayers.Mask, Is.EqualTo(0xFF << 20), "Bits 20 to 27, and only those.");
            // The tests' own layers stay theirs: a capture guard on 31, the garment fit's isolation on 30.
            Assert.That(RoomLayers.Mask & ((1 << 30) | (1 << 31)), Is.Zero);
            Assert.That(RoomLayers.IsRoomLayer(30) || RoomLayers.IsRoomLayer(31) || RoomLayers.IsRoomLayer(HouseLayers.Furniture), Is.False);
            // Named, and empty: nothing in either house stands on a room layer yet (RS2a moves pieces there at run time).
            foreach (var path in new[] { EpisodeScene, PrototypeScene })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var onRoomLayers = All<Transform>(scene).Where(t => RoomLayers.IsRoomLayer(t.gameObject.layer)).Select(t => t.name).ToArray();
                    Assert.That(onRoomLayers, Is.Empty, path + " has objects on room layers.");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        [Test]
        public void HousePrototypeHasNoRoomScope()
        {
            var scene = EditorSceneManager.OpenPreviewScene(PrototypeScene);
            try
            {
                // No director, so nothing builds a scope or reads one; and the scene is untouched.
                Assert.That(All<EpisodeDirector>(scene), Is.Empty, "The prototype has no director.");
                Assert.That(All<Transform>(scene).Where(t => RoomLayers.IsRoomLayer(t.gameObject.layer)), Is.Empty);
                foreach (var camera in All<Camera>(scene))
                    Assert.That(camera.cullingMask & RoomLayers.Mask, Is.EqualTo(RoomLayers.Mask), camera.name + " draws every room layer.");
                var rigs = All<HouseCameraRig>(scene);
                Assert.That(rigs, Is.Not.Empty, "The prototype has a camera rig.");
                foreach (var rig in rigs)
                {
                    Assert.That(rig.RoomScope, Is.Null, "A rig with no director has no rooms.");
                    Assert.That(rig.Rooms.IsWholeHouse, Is.True, "and reports the whole house.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void WithScope(string path, Action<Scene, HouseRoomScope> body)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                Assert.That(HouseRoomScope.TryCreate(scene, out var scope, out var why), Is.True, why);
                Assert.That(scope.Rooms.IsWholeHouse, Is.True, "The episode house has all eight rooms.");
                body(scene, scope);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static T[] All<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        /// <summary>A piece the classifier sorts: a mesh, skinned mesh or particle renderer on layer 0, collider-free, not a body's, not the shell list's.</summary>
        private static bool IsDressing(Renderer renderer)
            => (renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is ParticleSystemRenderer)
               && renderer.gameObject.layer == 0 && renderer.GetComponent<Collider>() == null
               && renderer.GetComponentInParent<HouseNpc>(true) == null && renderer.GetComponentInParent<HousePlayerController>(true) == null
               && !HouseRoomScope.InShellList(renderer.transform);

        /// <summary>The bounds in plan inside the box grown by the classifier's margin, plus <paramref name="extra"/>.</summary>
        private static bool InsideGrown(Rect box, Bounds bounds, float extra = 0f)
        {
            float grow = HouseRoomScope.BoxGrowth + extra;
            return bounds.min.x >= box.xMin - grow && bounds.max.x <= box.xMax + grow
                && bounds.min.z >= box.yMin - grow && bounds.max.z <= box.yMax + grow;
        }

        /// <summary>
        /// The plan rule, worked again from the renderer's bounds (§3.2 b): the one floor whose grown box
        /// holds it, or, where two do, the one whose own box holds its centre; -1 otherwise.
        /// </summary>
        private static int RuleRoom(HouseRoomScope scope, Bounds bounds)
        {
            var holders = Enumerable.Range(0, RoomLayers.Count).Where(room => scope.Has(room) && InsideGrown(scope.Box(room), bounds)).ToArray();
            if (holders.Length == 1) return holders[0];
            if (holders.Length == 0) return -1;
            var centre = new Vector2(bounds.center.x, bounds.center.z);
            var centred = holders.Where(room => StrictlyInside(scope.Box(room), centre)).ToArray();
            return centred.Length == 1 ? centred[0] : -1;
        }

        private static string Room(int index) => index >= 0 && index < RoomLayers.Count ? RoomLayers.RoomIds[index] : "drawn always";

        private static bool StrictlyInside(Rect box, Vector2 point)
            => point.x > box.xMin + Tolerance && point.x < box.xMax - Tolerance && point.y > box.yMin + Tolerance && point.y < box.yMax - Tolerance;

        /// <summary>A wall as the authored shell recognises one: a metre tall or more, thin, a metre long or more.</summary>
        private static bool WallLike(BoxCollider box)
        {
            if (!box.enabled || !box.gameObject.activeInHierarchy || box.isTrigger) return false;
            var size = Vector3.Scale(box.size, box.transform.lossyScale);
            return size.y >= 1f && Mathf.Min(size.x, size.z) <= .3f && Mathf.Max(size.x, size.z) >= 1f && box.GetComponent<HouseWalkable>() == null;
        }

        private static string Name(Object piece)
        {
            if (piece == null) return "(destroyed)";
            var t = piece is Component component ? component.transform : null;
            return t == null ? piece.name : (t.parent != null ? t.parent.name + "/" : "") + t.name;
        }
    }
}

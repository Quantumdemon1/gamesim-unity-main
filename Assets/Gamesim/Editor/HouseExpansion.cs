using System;
using System.Linq;
using Gamesim.House;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Gamesim.Editor
{
    /// <summary>
    /// Adds the three rooms the reference house has and this one did not: an HoH suite, a nomination
    /// room, and a game room.
    ///
    /// <para><b>This changes the slice, not just its look.</b> The original outline named five rooms
    /// — living room, kitchen, bedroom, private conversation room, competition yard — and every test
    /// that counts rooms was written against that number. Eight rooms is a different deliverable, and
    /// it was asked for explicitly after the five-room contract was raised. The tests that assert the
    /// count are updated in the same change rather than loosened, so the number stays something the
    /// suite holds the build to.</para>
    ///
    /// <para>The new band runs south of the existing house, and the wall between them is rebuilt with
    /// a doorway so the NavMesh joins. That wall is not edited in place: it is measured, hidden, and
    /// replaced by two segments derived from its own bounds, because a hard-coded replacement is a
    /// wall that ends up in the wrong place the next time the house moves.</para>
    ///
    /// <para>The bake is redone here and copied into the existing NavMesh asset rather than written
    /// to a new one. The scene references that asset by GUID; creating a replacement would leave the
    /// surface pointing at nothing and the cast unable to move.</para>
    /// </summary>
    public static class HouseExpansion
    {
        public const string RootName = "South Wing";
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        private const string WorldRoot = "House Architecture";
        private const string ArtRoot = "Assets/Gamesim/Art/Prototype/";
        private const string NavPath = "Assets/Gamesim/Data/EpisodeHouseNavMesh.asset";
        private const string SouthWall = "South cutaway wall";

        private const float DoorHalf = 1.4f;
        private const float BandDepth = 10f;

        /// <summary>A new room: its marker name, its label, and the tone its floor takes.</summary>
        private readonly struct Room
        {
            public readonly string Marker, Label, Material;
            public readonly Color Tone;

            public Room(string marker, string label, string material, Color tone)
            {
                Marker = marker; Label = label; Material = material; Tone = tone;
            }
        }

        // Hue per room, at the shell's darkened value, so the rooms stay tellable apart at a glance
        // the way the existing five are.
        private static readonly Room[] Rooms =
        {
            new Room("HoH", "HOH SUITE", "HoH Suite Floor", new Color(0.115f, 0.092f, 0.052f)),
            new Room("Nomination", "NOMINATION ROOM", "Nomination Floor", new Color(0.110f, 0.062f, 0.068f)),
            new Room("Games", "GAME ROOM", "Game Room Floor", new Color(0.055f, 0.098f, 0.108f)),
        };

        [MenuItem("Gamesim/U07/Add the south wing (HoH, nomination, game room)")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(EpisodeScene, OpenSceneMode.Single);
            var world = scene.GetRootGameObjects().FirstOrDefault(root => root.name == WorldRoot);
            if (world == null) throw new InvalidOperationException("No '" + WorldRoot + "' root in the episode scene.");

            var reference = world.transform.Find("Living room floor")?.GetComponent<Renderer>();
            if (reference == null) throw new InvalidOperationException("Living room floor is required to match the new floors to.");
            var wall = world.transform.Find(SouthWall);
            if (wall == null) throw new InvalidOperationException("Expected a '" + SouthWall + "' to open a doorway through.");
            var wallRenderer = wall.GetComponent<Renderer>();
            if (wallRenderer == null) throw new InvalidOperationException(SouthWall + " has no renderer to measure.");

            var existing = world.transform.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(world.transform, false);

            var floorBounds = reference.bounds;
            var wallBounds = wallRenderer.bounds;

            float minX = floorBounds.min.x, maxX = wallBounds.max.x;
            // The band sits directly south of the wall that currently closes the house.
            float northZ = wallBounds.center.z;
            float southZ = northZ - BandDepth;
            float floorY = floorBounds.center.y;
            float floorThickness = floorBounds.size.y;
            float wallHeight = wallBounds.size.y;
            float wallThickness = Mathf.Max(0.25f, wallBounds.size.z);
            float wallY = wallBounds.center.y;

            var walls = Material("Walls", new Color(0.105f, 0.115f, 0.125f));
            var gold = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "Emissive/Neon Gold.mat");

            float span = maxX - minX;
            float roomWidth = span / Rooms.Length;

            for (int i = 0; i < Rooms.Length; i++)
            {
                var room = Rooms[i];
                float left = minX + i * roomWidth;
                float centreX = left + roomWidth * 0.5f;
                float centreZ = (northZ + southZ) * 0.5f;

                var floor = Box(root, room.Marker + " floor", Material(room.Material, room.Tone),
                    new Vector3(roomWidth, floorThickness, BandDepth),
                    new Vector3(centreX, floorY, centreZ));
                floor.AddComponent<HouseWalkable>();

                var marker = new GameObject(room.Marker + " room marker");
                marker.transform.SetParent(root, false);
                marker.transform.position = new Vector3(centreX, floorBounds.max.y, centreZ);
                marker.AddComponent<HouseRoomMarker>().Configure(room.Marker);

                Label(root, room.Label, new Vector3(centreX, floorBounds.max.y + 0.03f, centreZ + 3.2f));
                if (gold != null) Trim(root, gold, centreX, centreZ, roomWidth, BandDepth, floorBounds.max.y);
            }

            // Outer shell of the new band: south, east and west. No doors — this is the house edge.
            Box(root, "South wing south wall", walls,
                new Vector3(span + wallThickness * 2f, wallHeight, wallThickness),
                new Vector3((minX + maxX) * 0.5f, wallY, southZ));
            Box(root, "South wing west wall", walls,
                new Vector3(wallThickness, wallHeight, BandDepth),
                new Vector3(minX, wallY, (northZ + southZ) * 0.5f));
            Box(root, "South wing east wall", walls,
                new Vector3(wallThickness, wallHeight, BandDepth),
                new Vector3(maxX, wallY, (northZ + southZ) * 0.5f));

            // Dividers between the three rooms, each with a doorway so the wing is walkable end to end.
            for (int i = 1; i < Rooms.Length; i++)
            {
                float x = minX + i * roomWidth;
                VerticalDoorway(root, "South wing divider " + i, walls, x, southZ, northZ,
                    (northZ + southZ) * 0.5f, wallY, wallHeight, wallThickness);
            }

            // And the wall that used to close the house, reopened.
            int openings = Reopen(root, wall, wallRenderer, walls, minX, maxX);

            Recentre(scene, world);
            int baked = Rebake(world);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format(
                "[Gamesim] south wing · {0} rooms across x {1:0.##}..{2:0.##}, z {3:0.##}..{4:0.##}; "
                + "{5} doorways through {6}; NavMesh rebaked ({7})",
                Rooms.Length, minX, maxX, southZ, northZ, openings, SouthWall,
                baked > 0 ? "asset updated in place" : "no surface found"));
        }

        /// <summary>
        /// Moves the camera pivot and pan limits onto the house's new bounds.
        ///
        /// <para>The wing made the house 40 deep where it was 30, and the rig was still centred on
        /// the middle of the old footprint with pan limits sized for it — so the three new rooms
        /// were built, walkable, and out of reach of the only camera the player has. Derived from
        /// the floors rather than typed in, so it stays right if the house grows again.</para>
        ///
        /// <para>Distance is deliberately not touched. It was just set to the rig's own declared
        /// default of 24 after shipping at 48 for months, and quietly moving it again to fit a
        /// bigger house would undo that with no record.</para>
        /// </summary>
        private static void Recentre(UnityEngine.SceneManagement.Scene scene, GameObject world)
        {
            // Scene-wide: the rig is its own scene root, not a child of the architecture.
            var rig = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseCameraRig>(true))
                .FirstOrDefault();
            if (rig == null) { Debug.LogWarning("[Gamesim] south wing · no camera rig to recentre."); return; }

            var floors = world.GetComponentsInChildren<HouseWalkable>(true)
                .Select(walkable => walkable.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .ToArray();
            if (floors.Length == 0) return;

            var bounds = floors[0].bounds;
            foreach (var renderer in floors) bounds.Encapsulate(renderer.bounds);

            var serialized = new SerializedObject(rig);
            var centre = serialized.FindProperty("houseCenter");
            var extents = serialized.FindProperty("panHalfExtents");
            if (centre == null || extents == null) return;

            var before = centre.vector3Value;
            centre.vector3Value = new Vector3(bounds.center.x, before.y, bounds.center.z);
            // A little past the walls, so a player can look into the corners rather than stopping
            // short of them.
            extents.vector2Value = new Vector2(bounds.extents.x + 2f, bounds.extents.z + 2f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log(string.Format(
                "[Gamesim] south wing · camera pivot {0} -> {1}, pan half-extents {2}",
                before.ToString("F1"), centre.vector3Value.ToString("F1"), extents.vector2Value.ToString("F1")));
        }

        /// <summary>
        /// Replaces the old south wall with two segments either side of a doorway.
        ///
        /// <para>The original is hidden rather than destroyed: it is referenced by name elsewhere in
        /// the scene's own tooling, and a wall that vanishes is harder to reason about later than one
        /// that is visibly switched off.</para>
        /// </summary>
        private static int Reopen(Transform root, Transform wall, Renderer wallRenderer, Material material,
            float minX, float maxX)
        {
            var bounds = wallRenderer.bounds;
            wallRenderer.enabled = false;
            foreach (var collider in wall.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

            float door = (minX + maxX) * 0.5f;
            float thickness = Mathf.Max(0.25f, bounds.size.z);

            Box(root, "South wing link left", material,
                new Vector3(door - DoorHalf - minX, bounds.size.y, thickness),
                new Vector3((minX + door - DoorHalf) * 0.5f, bounds.center.y, bounds.center.z));
            Box(root, "South wing link right", material,
                new Vector3(maxX - door - DoorHalf, bounds.size.y, thickness),
                new Vector3((door + DoorHalf + maxX) * 0.5f, bounds.center.y, bounds.center.z));
            return 1;
        }

        private static void VerticalDoorway(Transform root, string name, Material material,
            float x, float minZ, float maxZ, float door, float y, float height, float thickness)
        {
            Box(root, name + " south", material,
                new Vector3(thickness, height, door - DoorHalf - minZ),
                new Vector3(x, y, (minZ + door - DoorHalf) * 0.5f));
            Box(root, name + " north", material,
                new Vector3(thickness, height, maxZ - door - DoorHalf),
                new Vector3(x, y, (door + DoorHalf + maxZ) * 0.5f));
        }

        /// <summary>Neon outline on the floor, matching the trim the rest of the house carries.</summary>
        private static void Trim(Transform root, Material gold, float centreX, float centreZ,
            float width, float depth, float top)
        {
            const float inset = 0.45f, gauge = 0.12f;
            float halfW = width * 0.5f - inset, halfD = depth * 0.5f - inset;
            float y = top + 0.02f;

            Strip(root, gold, new Vector3(halfW * 2f, 0.04f, gauge), new Vector3(centreX, y, centreZ + halfD));
            Strip(root, gold, new Vector3(halfW * 2f, 0.04f, gauge), new Vector3(centreX, y, centreZ - halfD));
            Strip(root, gold, new Vector3(gauge, 0.04f, halfD * 2f), new Vector3(centreX - halfW, y, centreZ));
            Strip(root, gold, new Vector3(gauge, 0.04f, halfD * 2f), new Vector3(centreX + halfW, y, centreZ));
        }

        private static void Strip(Transform root, Material material, Vector3 size, Vector3 position)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Trim";
            strip.transform.SetParent(root, false);
            strip.transform.position = position;
            strip.transform.localScale = size;
            strip.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        /// <summary>
        /// Rebuilds the navigation mesh and writes it into the asset the scene already points at.
        ///
        /// <para>Copied into the existing asset rather than created as a new one: the surface holds a
        /// GUID reference, and replacing the file would leave it pointing at nothing — which presents
        /// as a cast that cannot move, a long way from the change that caused it.</para>
        /// </summary>
        private static int Rebake(GameObject world)
        {
            var surface = world.GetComponentsInChildren<NavMeshSurface>(true).FirstOrDefault();
            if (surface == null) { Debug.LogWarning("[Gamesim] south wing · no NavMeshSurface; navigation not rebuilt."); return 0; }

            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("The NavMesh bake produced no data.");

            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavPath);
                return 1;
            }

            EditorUtility.CopySerialized(surface.navMeshData, existing);
            surface.navMeshData = existing;
            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(surface);
            return 1;
        }

        private static GameObject Box(Transform parent, string name, Material material, Vector3 size, Vector3 position)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static void Label(Transform parent, string text, Vector3 position)
        {
            var go = new GameObject(text + " label");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.06f;
        }

        private static Material Material(string name, Color tone)
        {
            string path = ArtRoot + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit is required to build the south wing.");
            var material = new UnityEngine.Material(shader) { name = name };
            material.SetFloat("_Smoothness", 0.16f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tone);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tone);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}

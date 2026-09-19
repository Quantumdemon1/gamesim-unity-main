using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Dresses the prototype house with authored furniture models, fitted to the footprints the
    /// primitive blocks already occupy. The primitives stay exactly where they are — only their
    /// renderers are switched off — because their colliders are what the NavMesh is baked from.
    /// Re-running is safe: the Furnishings root is rebuilt from scratch each time.
    /// </summary>
    public static class HouseFurnishing
    {
        public const string RootName = "Furnishings";
        private const string Kit = "Assets/Gamesim/Art/External/KenneyFurniture/";

        /// <summary>How a model is sized into the footprint it replaces.</summary>
        private enum Fit
        {
            Solid, // constrain all three axes, so nothing grows taller than the walls
            Flat,  // ignore height — rugs
            Wall,  // ignore depth — screens and wall panels
            Run,   // repeat along the long axis — sofas, counter runs
        }

        private static readonly (string Target, string Model, Fit Mode)[] Plan =
        {
            ("Sofa seat",            "loungeSofaLong",     Fit.Run),
            ("Coffee table",         "tableCoffee",        Fit.Solid),
            ("Television console",   "cabinetTelevision",  Fit.Run),
            ("Television",           "televisionModern",   Fit.Wall),
            ("Kitchen island",       "kitchenBar",         Fit.Solid),
            ("Kitchen counters",     "kitchenCabinet",     Fit.Run),
            ("Refrigerator",         "kitchenFridgeLarge", Fit.Solid),
            ("Bed frame",            "bedDouble",          Fit.Solid),
            ("Wardrobe",             "bookcaseOpen",       Fit.Solid),
            ("Confessional chair A", "loungeDesignChair",  Fit.Solid),
            ("Confessional chair B", "loungeDesignChair",  Fit.Solid),
            ("Private table",        "tableRound",         Fit.Solid),
            ("Living rug",           "rugRectangle",       Fit.Flat),
            ("Private room rug",     "rugRound",           Fit.Flat),
        };

        /// <summary>Every model id the plan names, for the catalogue audit.</summary>
        public static IEnumerable<string> PlanModels => Plan.Select(entry => entry.Model);

        // Absorbed by the model that replaces the piece they sit on.
        private static readonly string[] Absorbed = { "Sofa back", "Countertop", "Duvet", "Pillow" };

        [MenuItem("Gamesim/U02/Apply House Furnishings")]
        private static void ApplyToOpenScene()
        {
            var world = GameObject.Find("House Architecture");
            if (world == null)
                throw new InvalidOperationException("Open HousePrototype.unity before applying furnishings.");
            int count = Apply(world.transform);
            EditorUtility.SetDirty(world);
            Debug.Log($"House furnishings applied: {count} models under {RootName}. Save the scene to keep them.");
        }

        /// <summary>Rebuilds the furnishing set. Returns the number of models placed.</summary>
        public static int Apply(Transform world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            var existing = world.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(world, false);

            foreach (var (target, model, mode) in Plan)
            {
                foreach (var primitive in world.Cast<Transform>().Where(t => t.name == target).ToArray())
                {
                    var renderer = primitive.GetComponent<Renderer>();
                    if (renderer == null) continue;

                    if (mode == Fit.Run) PlaceRun(model, renderer.bounds, root, target);
                    else Place(model, renderer.bounds, mode, root, target + " (model)");
                    renderer.enabled = false;
                }
            }

            foreach (var primitive in world.Cast<Transform>().Where(t => Absorbed.Contains(t.name)))
            {
                var renderer = primitive.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }

            FaceRooms(world, root);
            return root.childCount;
        }

        private static Bounds Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static GameObject Place(string model, Bounds slot, Fit mode, Transform parent, string name)
        {
            var source = HouseCatalogue.Resolve(model, out _);
            if (source == null)
            {
                Debug.LogWarning($"Furnishing model missing, leaving the primitive visible: {model}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            var raw = Measure(instance);
            // Turn the model when its footprint runs across the slot rather than along it.
            float yaw = slot.size.z > slot.size.x != raw.size.z > raw.size.x ? 90f : 0f;
            float modelX = yaw == 0f ? raw.size.x : raw.size.z;
            float modelZ = yaw == 0f ? raw.size.z : raw.size.x;

            float byX = slot.size.x / Mathf.Max(modelX, 0.001f);
            float byZ = slot.size.z / Mathf.Max(modelZ, 0.001f);
            float byY = slot.size.y / Mathf.Max(raw.size.y, 0.001f);
            float scale = mode == Fit.Flat ? Mathf.Min(byX, byZ)
                        : mode == Fit.Wall ? Mathf.Min(byX, byY)
                        : Mathf.Min(byX, byZ, byY);

            instance.transform.localScale = Vector3.one * scale;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var placed = Measure(instance);
            instance.transform.position += new Vector3(
                slot.center.x - placed.center.x,
                slot.min.y - placed.min.y,
                slot.center.z - placed.center.z);
            instance.name = name;

            // Furnishings must never contribute collision; the primitives own the NavMesh.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            return instance;
        }

        private static void PlaceRun(string model, Bounds slot, Transform parent, string name)
        {
            var source = HouseCatalogue.Resolve(model, out _);
            if (source == null)
            {
                Debug.LogWarning($"Furnishing model missing, leaving the primitive visible: {model}");
                return;
            }

            bool alongZ = slot.size.z > slot.size.x;
            float span = alongZ ? slot.size.z : slot.size.x;
            float across = alongZ ? slot.size.x : slot.size.z;

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(source);
            var raw = Measure(probe);
            float shortSide = Mathf.Max(Mathf.Min(raw.size.x, raw.size.z), 0.001f);
            float longSide = Mathf.Max(Mathf.Max(raw.size.x, raw.size.z), 0.001f);
            UnityEngine.Object.DestroyImmediate(probe);

            // How many unit-proportioned copies span the slot, clamped so a run stays readable.
            int count = Mathf.Clamp(Mathf.RoundToInt(span / Mathf.Max(across * (shortSide / longSide), 0.001f)), 1, 6);
            float step = span / count;

            for (int i = 0; i < count; i++)
            {
                var centre = slot.center;
                if (alongZ) centre.z = slot.min.z + step * (i + 0.5f);
                else centre.x = slot.min.x + step * (i + 0.5f);

                var cell = new Bounds(centre, alongZ
                    ? new Vector3(slot.size.x, slot.size.y, step)
                    : new Vector3(step, slot.size.y, slot.size.z));
                Place(model, cell, Fit.Solid, parent, $"{name} {i + 1}");
            }
        }

        /// <summary>
        /// Open-fronted pieces look like dark boxes when their back is to the camera, so turn each
        /// one toward the centre of the room it stands in. Only 180 degree flips are applied —
        /// a quarter turn would undo the footprint fit.
        /// </summary>
        private static void FaceRooms(Transform world, Transform root)
        {
            var floors = world.Cast<Transform>()
                .Where(t => t.name.EndsWith("floor", StringComparison.Ordinal))
                .Select(t => t.GetComponent<Renderer>())
                .Where(r => r != null)
                .ToArray();
            if (floors.Length == 0) return;

            foreach (Transform model in root)
            {
                var bounds = Measure(model.gameObject);
                var here = new Vector2(bounds.center.x, bounds.center.z);
                var floor = floors.OrderBy(f => Vector2.Distance(new Vector2(f.bounds.center.x, f.bounds.center.z), here)).First();

                var toCentre = new Vector3(floor.bounds.center.x - bounds.center.x, 0f, floor.bounds.center.z - bounds.center.z);
                if (toCentre.sqrMagnitude < 0.01f) continue;

                var facing = model.forward;
                facing.y = 0f;
                if (Vector3.Dot(facing.normalized, toCentre.normalized) < 0f)
                    model.RotateAround(bounds.center, Vector3.up, 180f);
            }
        }
    }
}

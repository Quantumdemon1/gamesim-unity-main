using System.Collections.Generic;
using System.Linq;
using Gamesim.House;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Gives the house somewhere for light to come from.
    ///
    /// <para>The scene shipped with exactly one light — a directional <c>Sun</c> — for eight rooms.
    /// Everything else that looks like a light source is geometry wearing an emissive material:
    /// <c>Lamp Glow</c>, <c>TV Screen</c> and four <c>Neon</c> materials all glow and none of them
    /// illuminate anything. So every room was lit identically from one angle, no room had its own
    /// atmosphere, and nothing drew the eye to where the drama was happening.</para>
    ///
    /// <para>Two kinds of light go in. A <b>room fill</b> hangs above each room, tinted to what that
    /// room is for — warm where people live, gold in the HoH suite, cold red over the nomination
    /// table. And a <b>practical</b> sits inside every lamp and television, so the props that were
    /// pretending to be light sources finally are.</para>
    ///
    /// <para>All of it is parented to one root object, so removing it is deleting one thing. None of
    /// it casts shadows: the walls are barely over a metre tall, so shadows from a ceiling-height
    /// fill would rake across the floor at absurd angles and cost frame time to do it.</para>
    ///
    /// <para>The desktop renderer is Forward+, which has no per-object additional-light limit, so
    /// the count here is not a problem. The mobile renderer is plain Forward and capped at four per
    /// object — worth knowing before this is pointed at a phone build.</para>
    /// </summary>
    public static class HouseLighting
    {
        private const string RootName = "Gamesim Fitted Lighting";

        /// <summary>
        /// How high the room fills hang. Above the walls, below the dollhouse camera.
        ///
        /// <para>Lower than it wants to be, on purpose. A point light far above a room lights it
        /// evenly, and even lighting is exactly the flatness this pass exists to break — the fill has
        /// to be close enough to the floor that its falloff still has shape by the time it reaches
        /// the walls.</para>
        /// </summary>
        private const float FillHeight = 2.6f;

        /// <summary>What each room is for, expressed as a colour and a brightness.</summary>
        private struct Mood
        {
            public string Tint;
            public float Intensity;
            public float Range;
            public Mood(string tint, float intensity, float range)
            { Tint = tint; Intensity = intensity; Range = range; }
        }

        private static readonly Dictionary<string, Mood> Rooms =
            new Dictionary<string, Mood>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Warm and bright: the room the house actually lives in.
            { "Living",     new Mood("#FFD9A8", 4.5f, 11f) },
            // Cooler and clean, the way a working kitchen reads.
            { "Kitchen",    new Mood("#E8F2FF", 4.0f, 11f) },
            // Dim and cool. A bedroom that is as bright as the kitchen is not a bedroom.
            { "Bedroom",    new Mood("#A8B6FF", 2.6f, 11f) },
            { "Private",    new Mood("#FFE2C4", 3.2f, 11f) },
            // The yard has the Sun already; this only lifts the shadow side.
            { "Yard",       new Mood("#FFF4E0", 1.6f, 16f) },
            // The reward room, and it should look like one from across the house.
            { "HoH",        new Mood("#FFD27A", 5.0f,  9f) },
            // Cold red. Nothing good happens at this table.
            { "Nomination", new Mood("#FF8A80", 3.6f,  9f) },
            { "Games",      new Mood("#7FB4FF", 4.2f,  9f) },
        };

        [MenuItem("Gamesim/Fit house lighting")]
        public static void FitLighting()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var root = Remove(scene);
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Fit house lighting");

            int fills = 0, practicals = 0;

            foreach (var marker in Object.FindObjectsByType<HouseRoomMarker>(FindObjectsInactive.Exclude))
            {
                if (string.IsNullOrEmpty(marker.RoomName)) continue;
                if (!Rooms.TryGetValue(marker.RoomName, out var mood)) continue;

                var at = marker.transform.position;
                Make(root.transform, marker.RoomName + " fill",
                    new Vector3(at.x, FillHeight, at.z), mood.Tint, mood.Intensity, mood.Range);
                fills++;
            }

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                string name = renderer.gameObject.name;
                bool lamp = name.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool screen = name.IndexOf("television", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!lamp && !screen) continue;

                var bounds = renderer.bounds;
                // At the top of a lamp, where the shade is, and at the face of a screen.
                var at = new Vector3(bounds.center.x, lamp ? bounds.max.y - 0.1f : bounds.center.y, bounds.center.z);
                Make(root.transform, name + " practical", at,
                    lamp ? "#FFE6B8" : "#6FA8FF",
                    // A lamp that does not visibly pool on the floor beside it is still just a
                    // prop with a glowing material, which is what this pass set out to fix.
                    lamp ? 4.5f : 2.2f,
                    lamp ? 6f : 3.6f);
                practicals++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Gamesim] House lighting · " + fills + " room fills, " + practicals
                      + " practicals in lamps and screens, all under '" + RootName
                      + "'. Nothing casts shadows; the Sun still does that.");
        }

        [MenuItem("Gamesim/Remove fitted house lighting")]
        public static void RemoveLighting()
        {
            var scene = EditorSceneManager.GetActiveScene();
            bool removed = Remove(scene) == null;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Gamesim] House lighting · " + (removed ? "removed." : "there was none to remove."));
        }

        private static GameObject Remove(UnityEngine.SceneManagement.Scene scene)
        {
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
            return null;
        }

        private static void Make(Transform parent, string name, Vector3 at,
            string tint, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Tint(tint);
            light.intensity = intensity;
            light.range = range;
            // See the class note: walls barely clear a metre, so a shadow from up here would rake
            // across the whole room at an angle nothing in the house justifies.
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        private static Color Tint(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : Color.white;
    }
}

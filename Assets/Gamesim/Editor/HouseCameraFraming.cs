using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Gamesim.House;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Brings the episode camera to the distance the project believes it ships at.
    ///
    /// <para><c>HouseCameraRig</c> declares <c>distance = 24</c> with a 34 maximum. The episode scene
    /// serialised 48 and 56, and a measurement of the running game found 48 at frame 0 and still 48
    /// at frame 480 — it never converges. Every framing judgement made about this project was
    /// therefore made against a number the build does not use: the "leave it at 24" decision, the
    /// cast-reads-small finding recorded as "42-54 metres from camera", and the three comparison
    /// renders at 24, 17 and 10, none of which was the default.</para>
    ///
    /// <para>This is not a new opinion about framing. It makes the scene agree with the source
    /// default and with the decision already taken, and it reports the before and after so the
    /// change is a measurement rather than a preference.</para>
    ///
    /// <para>The nearest zoom too (ROOM-SCOPE-PLAN D11, RS5): four metres, decided on 2026-09-17
    /// ("Four metres, not ten") and put back to ten in the scene the next day without a word. The
    /// runtime keeps four whatever the scene says (the director's room view), so this pass makes the
    /// scene say it as well.</para>
    /// </summary>
    public static class HouseCameraFraming
    {
        private const string EpisodeScene = "Assets/Gamesim/Scenes/EpisodeHouse.unity";

        // The C# field defaults. Named here so the two cannot drift apart again silently.
        private const float SourceDistance = 24f;
        private const float SourceMaximum = 34f;
        /// <summary>The nearest zoom the room view keeps (D11).</summary>
        public const float SourceMinimum = HouseRoomView.NearestDistance;

        /// <summary>The distance, the maximum and the nearest zoom the project ships at, written into a rig's serialized fields.</summary>
        public static void WriteSourceDefaults(SerializedObject serialized)
        {
            Write(serialized, "distance", SourceDistance);
            Write(serialized, "maximumDistance", SourceMaximum);
            Write(serialized, "minimumDistance", SourceMinimum);
        }

        [MenuItem("Gamesim/U07/Match the camera to its own default")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(EpisodeScene, OpenSceneMode.Single);
            var rig = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<HouseCameraRig>(true))
                .FirstOrDefault();
            if (rig == null) throw new InvalidOperationException("No HouseCameraRig in the episode scene.");

            var serialized = new SerializedObject(rig);
            float before = Read(serialized, "distance");
            float beforeMax = Read(serialized, "maximumDistance");
            float beforeMin = Read(serialized, "minimumDistance");

            Write(serialized, "distance", SourceDistance);
            Write(serialized, "maximumDistance", SourceMaximum);
            Write(serialized, "minimumDistance", SourceMinimum);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Gamesim] camera framing · distance {0:0.##} -> {1:0.##}, maximum {2:0.##} -> {3:0.##}, minimum {4:0.##} -> {5:0.##} "
                + "(source defaults {6:0.##} / {7:0.##} / {8:0.##})",
                before, SourceDistance, beforeMax, SourceMaximum, beforeMin, SourceMinimum, SourceDistance, SourceMaximum, SourceMinimum));
        }

        private static float Read(SerializedObject serialized, string field)
        {
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("HouseCameraRig has no '" + field + "' field.");
            return property.floatValue;
        }

        private static void Write(SerializedObject serialized, string field, float value)
        {
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("HouseCameraRig has no '" + field + "' field.");
            property.floatValue = value;
        }
    }
}

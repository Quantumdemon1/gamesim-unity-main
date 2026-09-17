using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Gamesim.Persistence;
using Gamesim.Simulation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Writes an old-schema save so the shipped executable's migration path can be smoke-tested.
    ///
    /// Acceptance criterion A7 was previously blocked on "an accepted V4/V5 QA save" — an archived
    /// artefact that has to survive alongside the repository and is simply unavailable on a machine
    /// that never had it. The migration itself is covered by five Edit Mode test files; what only a
    /// real old file can show is that the *player build* loads and upgrades one. Generating that file
    /// on demand makes the criterion runnable anywhere instead of dependent on an archive.
    ///
    /// Writing the payload by hand rather than through the production writer is the point: the
    /// current writer only emits schema 6, so a genuine v5 has to be assembled from a serialized
    /// state with the fields that version did not have removed.
    /// </summary>
    public static class LegacySaveFixtures
    {
        private const string RootVariable = "GAMESIM_LEGACY_SAVE_ROOT";

        [MenuItem("Gamesim/Port/Write Legacy V5 QA Save", priority = 200)]
        public static void WriteLegacyV5Save()
        {
            var root = Environment.GetEnvironmentVariable(RootVariable);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "legacy-v5-qa"));
            if (!Path.IsPathRooted(root))
            {
                Debug.LogError("[Gamesim] " + RootVariable + " must be an absolute path.");
                return;
            }

            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "episode.json");
            File.WriteAllText(path, Envelope(CaptureAsV5(ContentCatalog.Create(601))));
            Debug.Log("[Gamesim] Legacy v5 QA save written -> " + path);
        }

        /// <summary>
        /// Serializes current state, then removes what schema 6 added and stamps the old version.
        /// `npcSocial` is the whole of that difference, which is why the v5→v6 migration's job is to
        /// seed it rather than to reshape anything.
        /// </summary>
        private static JObject CaptureAsV5(EpisodeState state)
        {
            var payload = JObject.FromObject(state, ProductionSerializer());
            payload.Remove("npcSocial");
            // Schema 7's card copy did not exist in v5 either, and the stored shape is checked
            // field for field: a capture that still carries it is not a v5 payload.
            foreach (var value in (JArray)payload["contestants"])
                foreach (var field in new[] { "occupation", "archetype", "age" })
                    ((JObject)value).Remove(field);
            payload["schemaVersion"] = 5;
            return payload;
        }

        /// <summary>The save writer's own serializer, so the fixture matches what the game would have written.</summary>
        private static JsonSerializer ProductionSerializer() => (JsonSerializer)typeof(EpisodeSaveStore).Assembly
            .GetType("Gamesim.Persistence.SaveJson", true)
            .GetMethod("Serializer", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, null);

        /// <summary>
        /// The on-disk envelope, with the checksum computed over a canonically ordered payload so it
        /// does not depend on property order. This mirrors the migration tests' helper deliberately:
        /// a fixture the loader accepts for the wrong reason would prove nothing.
        /// </summary>
        private static string Envelope(JObject payload)
        {
            JToken Order(JToken token)
            {
                if (token is JObject obj)
                    return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                        .Select(p => new JProperty(p.Name, Order(p.Value))));
                if (token is JArray array) return new JArray(array.Select(Order));
                return token.DeepClone();
            }

            using var hash = SHA256.Create();
            var checksum = BitConverter
                .ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Order(payload).ToString(Formatting.None))))
                .Replace("-", string.Empty).ToLowerInvariant();

            return new JObject
            {
                ["format"] = "gamesim-unity-save",
                ["version"] = 1,
                ["savedAt"] = "2026-09-10T00:00:00.0000000Z",
                ["checksum"] = checksum,
                ["state"] = payload.DeepClone(),
            }.ToString(Formatting.Indented);
        }
    }
}

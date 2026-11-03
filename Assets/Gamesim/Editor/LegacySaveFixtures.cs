using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Writes an old-schema save so the shipped executable's migration path can be smoke-tested.
    ///
    /// Acceptance criterion A7 was previously blocked on "an accepted V4/V5 QA save" — an archived
    /// artefact that has to survive alongside the repository and is simply unavailable on a machine
    /// that never had it. The migration itself is covered by the Edit Mode migration and old-save
    /// acceptance tests; what only a real old file can show is that the *player build* loads and
    /// upgrades one.
    ///
    /// The file is the old-save acceptance set's schema-5 fixture, copied byte for byte: a save the
    /// Codex port's schema-5 source snapshot wrote with its own store (Assets/Plans/OLD-SAVE-ACCEPTANCE.md,
    /// manifest id s05-default-w3-hoh), sealed by that store and replayed by OldSaveAcceptanceTests.
    /// It replaces a capture of the live state with later fields stripped by hand. That capture had to be
    /// edited on every schema bump and had fallen behind since schema 14, so the shape check of
    /// FrozenEpisodeV5 refused it; a file an old build really wrote never needs that edit.
    /// </summary>
    public static class LegacySaveFixtures
    {
        private const string RootVariable = "GAMESIM_LEGACY_SAVE_ROOT";
        private const string Schema5Fixture = "s05-default-w3-hoh.save.json";

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

            var source = Path.Combine(Application.dataPath, "Gamesim", "Tests", "EditMode", "Fixtures", "OldSaves", Schema5Fixture);
            if (!File.Exists(source))
            {
                Debug.LogError("[Gamesim] The schema-5 fixture is missing: " + source);
                return;
            }

            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "episode.json");
            File.WriteAllBytes(path, File.ReadAllBytes(source));
            Debug.Log("[Gamesim] Legacy v5 QA save written -> " + path + " (a copy of " + Schema5Fixture + ")");
        }
    }
}

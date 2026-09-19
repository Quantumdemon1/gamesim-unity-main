using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Where a prop's model comes from (MASTER-PLAN §4.6, the catalogue seam).
    ///
    /// <para>A plan names a prop by a logical id and never by a file. An id that starts with
    /// <c>bb_</c> is an authored asset and resolves under <c>Art/Authored</c>; any other id is a
    /// Kenney kit name, which resolves to an authored replacement if one has been registered for it
    /// and to the kit's <c>.glb</c> otherwise. The house is never half-furnished during the
    /// transition: a prop not yet authored still resolves to something, and the audit says which.</para>
    /// </summary>
    public static class HouseCatalogue
    {
        public enum Tier { Authored, Kenney, Missing }

        public const string AuthoredRoot = AuthoredAssetImporter.Root;
        public const string KenneyRoot = "Assets/Gamesim/Art/External/KenneyFurniture/";
        public const string AuthoredPrefix = "bb_";

        /// <summary>
        /// Kenney ids that an authored piece now stands in for, by the authored id. Filled in as
        /// Tier 3 furniture lands; a Kenney id absent from here keeps resolving to the kit.
        /// </summary>
        private static readonly Dictionary<string, string> Replacements = new Dictionary<string, string>
        {
        };

        public static GameObject Resolve(string id, out Tier tier)
        {
            if (string.IsNullOrEmpty(id)) { tier = Tier.Missing; return null; }

            if (id.StartsWith(AuthoredPrefix, System.StringComparison.Ordinal))
            {
                var authored = LoadAuthored(id);
                tier = authored != null ? Tier.Authored : Tier.Missing;
                return authored;
            }

            if (Replacements.TryGetValue(id, out var replacement))
            {
                var authored = LoadAuthored(replacement);
                if (authored != null) { tier = Tier.Authored; return authored; }
            }

            var kit = AssetDatabase.LoadAssetAtPath<GameObject>(KenneyRoot + id + ".glb");
            tier = kit != null ? Tier.Kenney : Tier.Missing;
            return kit;
        }

        /// <summary>The authored model whose file name is exactly the id, wherever it sits under Art/Authored.</summary>
        public static string AuthoredPath(string id) => AssetDatabase.FindAssets(id + " t:Model", new[] { AuthoredRoot.TrimEnd('/') })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => System.IO.Path.GetFileNameWithoutExtension(path) == id);

        private static GameObject LoadAuthored(string id)
        {
            string path = AuthoredPath(id);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public readonly struct Resolution
        {
            public readonly string Id;
            public readonly Tier Tier;
            public readonly string Path;
            public Resolution(string id, Tier tier, string path) { Id = id; Tier = tier; Path = path; }
        }

        /// <summary>One line per distinct id: which tier it resolves to, and from where.</summary>
        public static List<Resolution> Audit(IEnumerable<string> ids)
        {
            var report = new List<Resolution>();
            foreach (var id in ids.Distinct().OrderBy(id => id, System.StringComparer.Ordinal))
            {
                var model = Resolve(id, out var tier);
                report.Add(new Resolution(id, tier, model == null ? null : AssetDatabase.GetAssetPath(model)));
            }
            return report;
        }

        /// <summary>"Is this still Kenney?" becomes a list.</summary>
        [MenuItem("Gamesim/U07/Audit set piece resolution")]
        public static void AuditMenu()
        {
            var report = Audit(HouseSetPieces.PlanModels.Concat(HouseFurnishing.PlanModels));
            var lines = report.Select(r => r.Tier.ToString().PadRight(8) + " " + r.Id + (r.Path == null ? "" : "  ← " + r.Path));
            Debug.Log(string.Format("[Gamesim] catalogue · {0} ids: {1} authored, {2} Kenney, {3} missing\n{4}",
                report.Count, report.Count(r => r.Tier == Tier.Authored), report.Count(r => r.Tier == Tier.Kenney),
                report.Count(r => r.Tier == Tier.Missing), string.Join("\n", lines)));
        }
    }
}

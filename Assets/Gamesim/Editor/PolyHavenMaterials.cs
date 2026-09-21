using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// The URP materials for the Poly Haven pieces (VISUAL-TARGET.md V4). <c>bb_polyhaven.py</c>
    /// writes each piece's four maps under <c>Textures/PolyHaven</c> and exports an FBX whose one
    /// material is named <c>bb_mat_&lt;piece&gt;</c> and carries no images; this builds or refreshes
    /// that material beside the other set-piece materials, so the importer's by-name match finds
    /// it, and re-imports the piece so the match takes.
    /// </summary>
    public static class PolyHavenMaterials
    {
        public const string Textures = AuthoredAssetImporter.Root + "Textures/PolyHaven/";
        public const string Materials = AuthoredAssetImporter.Root + "SetPieces/Materials/";
        public const string Pieces = AuthoredAssetImporter.Root + "SetPieces/";
        public const string Prefix = "bb_tex_";
        public const string Albedo = "_albedo.png", Normal = "_normal.png", Metallic = "_metallic.png", Occlusion = "_occlusion.png";

        /// <summary>The piece names with an albedo map under the Poly Haven folder.</summary>
        public static string[] PieceNames()
        {
            if (!Directory.Exists(Textures)) return new string[0];
            return Directory.GetFiles(Textures, Prefix + "*" + Albedo)
                .Select(path => Path.GetFileName(path))
                .Select(file => file.Substring(Prefix.Length, file.Length - Prefix.Length - Albedo.Length))
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToArray();
        }

        public static string MaterialPath(string piece) => Materials + "bb_mat_" + piece + ".mat";
        public static string TexturePath(string piece, string kind) => Textures + Prefix + piece + kind;
        public static string PiecePath(string piece) => Pieces + "bb_set_" + piece + ".fbx";

        [MenuItem("Gamesim/U07/Build the Poly Haven materials")]
        public static void BuildAll()
        {
            int built = 0;
            foreach (var piece in PieceNames())
            {
                Build(piece);
                built++;
            }
            AssetDatabase.SaveAssets();
            foreach (var piece in PieceNames())
                if (File.Exists(PiecePath(piece)))
                    AssetDatabase.ImportAsset(PiecePath(piece), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Gamesim] Poly Haven materials built: " + built);
        }

        /// <summary>One piece's material, created or refreshed in place; returns it.</summary>
        public static Material Build(string piece)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string path = MaterialPath(piece);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Materials);
                material = new Material(shader) { name = "bb_mat_" + piece };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(piece, Albedo));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(piece, Normal));
            var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(piece, Metallic));
            var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(piece, Occlusion));
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            Keyword(material, "_NORMALMAP", normal != null);
            material.SetTexture("_MetallicGlossMap", metallic);
            // Smoothness lives in the metallic map's alpha, as bb_polyhaven.py writes it (1 - roughness).
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Metallic", 1f);
            Keyword(material, "_METALLICSPECGLOSSMAP", metallic != null);
            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_OcclusionStrength", 1f);
            Keyword(material, "_OCCLUSIONMAP", occlusion != null);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Keyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }
    }
}

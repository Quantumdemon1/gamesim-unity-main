using System;
using System.Linq;
using Gamesim.Presentation;
using UnityEditor;
using UnityEngine;

namespace Gamesim.Editor
{
    /// <summary>
    /// Builds the <see cref="OpeningDoorKit"/> in Resources from the authored front door
    /// (ArtSource/setpieces/bb_set_frontdoor_{frame,leaf,handle}.py): each piece's mesh, and the
    /// authored material name of each of its submeshes, which the door set maps to its own.
    ///
    /// <para>Run from <c>Gamesim/Sets/Build the opening door kit</c>, or in batchmode with
    /// <c>-executeMethod Gamesim.Editor.OpeningDoorKitAssets.Build</c>. Every run writes the whole
    /// kit again from the models, so a hand edit to the asset does not survive the next build.</para>
    /// </summary>
    public static class OpeningDoorKitAssets
    {
        public const string Folder = "Assets/Gamesim/Resources/Sets";
        public const string KitPath = Folder + "/OpeningDoorKit.asset";
        public const string FramePath = AuthoredAssetImporter.Root + "SetPieces/bb_set_frontdoor_frame.fbx";
        public const string LeafPath = AuthoredAssetImporter.Root + "SetPieces/bb_set_frontdoor_leaf.fbx";
        public const string HandlePath = AuthoredAssetImporter.Root + "SetPieces/bb_set_frontdoor_handle.fbx";

        [MenuItem("Gamesim/Sets/Build the opening door kit")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Gamesim/Resources", "Sets");
            var kit = AssetDatabase.LoadAssetAtPath<OpeningDoorKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<OpeningDoorKit>();
                AssetDatabase.CreateAsset(kit, KitPath);
            }
            (kit.frame, kit.frameSlots) = Piece(FramePath);
            (kit.leaf, kit.leafSlots) = Piece(LeafPath);
            (kit.handle, kit.handleSlots) = Piece(HandlePath);
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] opening door kit built: " + KitPath + " (frame " + string.Join(",", kit.frameSlots)
                + "; leaf " + string.Join(",", kit.leafSlots) + "; handle " + string.Join(",", kit.handleSlots) + ")");
        }

        private static (Mesh, string[]) Piece(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var filter = model != null ? model.GetComponentInChildren<MeshFilter>(true) : null;
            var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null || renderer == null)
                throw new InvalidOperationException("No authored mesh at " + path + "; export it with its ArtSource/setpieces script.");
            var slots = renderer.sharedMaterials.Select(material => material != null ? material.name : "").ToArray();
            return (filter.sharedMesh, slots);
        }
    }
}

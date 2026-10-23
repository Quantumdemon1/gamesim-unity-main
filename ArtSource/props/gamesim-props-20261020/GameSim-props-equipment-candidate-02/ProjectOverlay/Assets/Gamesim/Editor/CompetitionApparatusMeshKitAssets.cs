using System;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using PartKind = Gamesim.Presentation.CompetitionApparatusMeshKit.PartKind;
using Family = Gamesim.Presentation.CompetitionApparatus.Family;

namespace Gamesim.Editor
{
    /// <summary>
    /// Explicitly builds only the optional geometry kit. No scene, prefab, renderer material,
    /// contact setting or navigation is changed. All imported parts are checked before the
    /// existing kit is touched. Source: ArtSource/competition-apparatus/part-manifest.json.
    /// </summary>
    public static class CompetitionApparatusMeshKitAssets
    {
        public const string MeshFolder = "Assets/Gamesim/Art/Authored/Competition";
        public const string Folder = "Assets/Gamesim/Resources/Gamesim/Competition";
        public const string KitPath = Folder + "/ApparatusMeshKit.asset";

        [MenuItem("Gamesim/Competition/Build authored apparatus mesh kit")]
        public static void Build()
        {
            var entries = Specifications().Select(LoadPiece).ToArray();
            var probe = ScriptableObject.CreateInstance<CompetitionApparatusMeshKit>();
            try
            {
                probe.entries = entries;
                foreach (var entry in entries)
                    if (!probe.TryGetMesh(entry.family, entry.partKind, entry.referenceDimensions, out _))
                        throw new InvalidOperationException("Incompatible authored competition mesh: " + entry.family + "/" + entry.partKind);
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }

            EnsureFolder(Folder);
            var kit = AssetDatabase.LoadAssetAtPath<CompetitionApparatusMeshKit>(KitPath);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<CompetitionApparatusMeshKit>();
                kit.entries = entries;
                AssetDatabase.CreateAsset(kit, KitPath);
            }
            else
            {
                kit.entries = entries;
                EditorUtility.SetDirty(kit);
            }
            AssetDatabase.SaveAssetIfDirty(kit);
            Debug.Log("[Gamesim] Authored competition geometry kit built: " + entries.Length + " compatible parts at " + KitPath);
        }

        private static CompetitionApparatusMeshKit.Entry LoadPiece(CompetitionApparatusMeshKit.Entry entry)
        {
            string path = MeshFolder + "/bb_comp_" + entry.family + "_" + entry.partKind + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Missing authored competition model: " + path);
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
            if (filters.Length != 1 || meshes.Length != 1 || filters[0].sharedMesh == null
                || filters[0].sharedMesh != meshes[0] || filters[0].GetComponent<MeshRenderer>() == null
                || model.GetComponentsInChildren<MeshRenderer>(true).Length != 1
                || model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0
                || model.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("A competition part must contain exactly one static mesh, one renderer and no collider: " + path);

            // The kit borrows mesh vertices only. A model transform must not secretly supply
            // scale, rotation or offset that would be lost when attached to the existing Part.
            Matrix4x4 matrix = filters[0].transform.localToWorldMatrix;
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
                if (Mathf.Abs(matrix[row,column] - (row == column ? 1f : 0f)) > .0001f)
                    throw new InvalidOperationException("Bake the competition part's axis conversion and transforms into its mesh: " + path);
            Mesh mesh = meshes[0];
            if (!mesh.HasVertexAttribute(VertexAttribute.Normal) || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                throw new InvalidOperationException("Authored competition geometry needs imported normals and UV0: " + path);
            entry.mesh = mesh;
            return entry;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static CompetitionApparatusMeshKit.Entry Spec(Family family, PartKind kind, float x, float y, float z)
            => new CompetitionApparatusMeshKit.Entry { family = family, partKind = kind, referenceDimensions = new Vector3(x,y,z) };

        private static IEnumerable<CompetitionApparatusMeshKit.Entry> Specifications()
        {
            // Generated from the reviewed 39-entry manifest. Values are each Part's initial
            // metre scale; dynamic fitting remains owned exclusively by CompetitionApparatus.
            yield return Spec(Family.Signals, PartKind.StanceRail, 0.035f, 0.025f, 0.55f);
            yield return Spec(Family.Signals, PartKind.ProgressBacking, 0.96f, 0.16f, 0.008f);
            yield return Spec(Family.PairConsole, PartKind.StanceRail, 0.035f, 0.025f, 0.55f);
            yield return Spec(Family.PairConsole, PartKind.ProgressBacking, 0.96f, 0.16f, 0.008f);
            yield return Spec(Family.GripRig, PartKind.StanceRail, 0.035f, 0.025f, 0.55f);
            yield return Spec(Family.GripRig, PartKind.ProgressBacking, 0.96f, 0.16f, 0.008f);
            yield return Spec(Family.DiceTray, PartKind.StanceRail, 0.035f, 0.025f, 0.55f);
            yield return Spec(Family.DiceTray, PartKind.ProgressBacking, 0.96f, 0.16f, 0.008f);
            yield return Spec(Family.WordConsole, PartKind.StanceRail, 0.035f, 0.025f, 0.55f);
            yield return Spec(Family.WordConsole, PartKind.ProgressBacking, 0.96f, 0.16f, 0.008f);
            yield return Spec(Family.Signals, PartKind.ConsoleLeg, 0.07f, 0.84f, 0.14f);
            yield return Spec(Family.Signals, PartKind.ConsoleHousing, 0.95f, 0.8f, 0.14f);
            yield return Spec(Family.Signals, PartKind.RecessedDisplay, 0.86f, 0.69f, 0.025f);
            yield return Spec(Family.Signals, PartKind.AwardHeader, 0.95f, 0.045f, 0.03f);
            yield return Spec(Family.PairConsole, PartKind.ConsoleLeg, 0.07f, 0.84f, 0.14f);
            yield return Spec(Family.PairConsole, PartKind.ConsoleHousing, 0.92f, 0.8f, 0.14f);
            yield return Spec(Family.PairConsole, PartKind.RecessedDisplay, 0.83f, 0.69f, 0.025f);
            yield return Spec(Family.PairConsole, PartKind.AwardHeader, 0.92f, 0.045f, 0.03f);
            yield return Spec(Family.WordConsole, PartKind.ConsoleLeg, 0.07f, 0.84f, 0.14f);
            yield return Spec(Family.WordConsole, PartKind.ConsoleHousing, 1.02f, 0.8f, 0.14f);
            yield return Spec(Family.WordConsole, PartKind.RecessedDisplay, 0.93f, 0.69f, 0.025f);
            yield return Spec(Family.WordConsole, PartKind.AwardHeader, 1.02f, 0.045f, 0.03f);
            yield return Spec(Family.Signals, PartKind.SignalTarget, 0.2f, 0.17f, 0.03f);
            yield return Spec(Family.Signals, PartKind.ResponsePad, 0.14f, 0.065f, 0.15f);
            yield return Spec(Family.PairConsole, PartKind.MemoryTile, 0.16f, 0.13f, 0.025f);
            yield return Spec(Family.WordConsole, PartKind.LetterTile, 0.125f, 0.16f, 0.025f);
            yield return Spec(Family.GripRig, PartKind.RigUpright, 0.065f, 1.56f, 0.09f);
            yield return Spec(Family.GripRig, PartKind.RigBrace, 0.91f, 0.07f, 0.1f);
            yield return Spec(Family.GripRig, PartKind.GripHandle, 0.78f, 0.045f, 0.055f);
            yield return Spec(Family.GripRig, PartKind.GripTrack, 0.055f, 0.69f, 0.04f);
            yield return Spec(Family.GripRig, PartKind.GripRemaining, 0.035f, 0.69f, 0.025f);
            yield return Spec(Family.GripRig, PartKind.CounterweightGuide, 0.03f, 0.76f, 0.03f);
            yield return Spec(Family.GripRig, PartKind.Counterweight, 0.25f, 0.16f, 0.17f);
            yield return Spec(Family.GripRig, PartKind.GripTick, 0.06f, 0.012f, 0.03f);
            yield return Spec(Family.DiceTray, PartKind.TrayPedestal, 0.2f, 1.1f, 0.25f);
            yield return Spec(Family.DiceTray, PartKind.TrayBed, 0.91f, 0.08f, 0.42f);
            yield return Spec(Family.DiceTray, PartKind.TrayBackRail, 0.96f, 0.13f, 0.05f);
            yield return Spec(Family.DiceTray, PartKind.TraySideRail, 0.05f, 0.13f, 0.44f);
            yield return Spec(Family.DiceTray, PartKind.Die, 0.17f, 0.17f, 0.17f);
        }
    }
}

using System;
using UnityEditor;

namespace Gamesim.Editor
{
    /// <summary>
    /// Fixed import settings for the Blender-authored assets under <c>Art/Authored</c>, so nobody
    /// sets them by hand and a re-export cannot drift them (MASTER-PLAN §4.6).
    ///
    /// <para>The exporter on the Blender side (<c>ArtSource/tools/bb_export.py</c>) writes metres
    /// with the axis conversion baked in, so the importer takes the file's scale as it is and bakes
    /// nothing twice. Colliders are never generated: furniture is collider-free by design, and the
    /// shell and set pieces carry their own <c>_col</c> meshes. Materials are extracted beside the
    /// model and matched by name, which is what lets a re-export keep the material a scene already
    /// references.</para>
    ///
    /// <para>Anything under <c>Characters/</c>, <c>Wardrobe/</c> or <c>Animation/</c> imports as a
    /// Humanoid, and a clip whose name ends in <c>_loop</c> loops with its pose matched, so the
    /// seated idle and the talk and listen loops arrive ready for the controller.</para>
    /// </summary>
    public sealed class AuthoredAssetImporter : AssetPostprocessor
    {
        public const string Root = "Assets/Gamesim/Art/Authored/";
        private const string LoopSuffix = "_loop";
        public const string ColliderSuffix = "_col";

        public static bool IsAuthored(string path) => path != null && path.StartsWith(Root, StringComparison.Ordinal);

        public static bool IsRigged(string path) => IsAuthored(path)
            && (path.StartsWith(Root + "Characters/", StringComparison.Ordinal)
                || path.StartsWith(Root + "Wardrobe/", StringComparison.Ordinal)
                || path.StartsWith(Root + "Animation/", StringComparison.Ordinal));

        public static bool IsAnimation(string path) => IsAuthored(path)
            && path.StartsWith(Root + "Animation/", StringComparison.Ordinal);
        /// <summary>
        /// Clips authored on the shipped Quaternius skeleton (bb_anim.py): Generic, not Humanoid,
        /// because that rig is Generic and a Generic clip plays by bone path on the six bodies as
        /// they are. Humanoid retargeting would need every body re-imported as Humanoid first.
        /// </summary>
        public static bool IsGenericAnimation(string path) => IsAuthored(path)
            && path.StartsWith(Root + "Animation/Generic/", StringComparison.Ordinal);

        /// <summary>
        /// A body authored on the shipped skeleton (<c>ArtSource/characters/bb_char_*.py</c>): the
        /// same Generic rig as the six bodies, so every clip in the controller plays on it by bone
        /// path, and readable, because <c>FaceExpression</c> builds its shapes from the mesh.
        /// </summary>
        public static bool IsGenericCharacter(string path) => IsAuthored(path)
            && path.StartsWith(Root + "Characters/Generic/", StringComparison.Ordinal);

        /// <summary>Anything imported as a Generic rig rather than Humanoid: the clips and the bodies on that rig.</summary>
        public static bool IsGeneric(string path) => IsGenericAnimation(path) || IsGenericCharacter(path);

        public static bool IsTexture(string path) => IsAuthored(path)
            && path.StartsWith(Root + "Textures/", StringComparison.Ordinal);
        /// <summary>A map that is numbers, not colour: metallic/smoothness or occlusion.</summary>
        public static bool IsDataMap(string path) => path.EndsWith("_metallic.png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("_occlusion.png", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// A baked texture (bb_bake.py) is one of a pair: <c>*_albedo.png</c> is colour and imports
        /// as sRGB, <c>*_normal.png</c> is a tangent normal and imports as a normal map, never as
        /// colour. Both repeat, because they are floor and wall tiles, and both keep their mips.
        /// A Poly Haven piece (bb_polyhaven.py) adds <c>*_metallic.png</c> (smoothness in alpha) and
        /// <c>*_occlusion.png</c>, which are data too and import linear.
        /// </summary>
        private void OnPreprocessTexture()
        {
            if (!IsTexture(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_normal.png", StringComparison.OrdinalIgnoreCase);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !IsDataMap(assetPath);
            importer.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
        }

        private void OnPreprocessModel()
        {
            if (!IsAuthored(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = IsGenericCharacter(assetPath);   // a body's face is built from its mesh
            importer.addCollider = false;
            importer.importBlendShapes = true;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            importer.animationType = IsGeneric(assetPath) ? ModelImporterAnimationType.Generic
                : IsRigged(assetPath) ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None;
            importer.importAnimation = IsAnimation(assetPath);
            // A prop is lightmapped, and its box-projected UVs tile past 0..1, so the lightmapper
            // gets a second set of its own. A body is lit by probes and needs none.
            importer.generateSecondaryUV = !IsRigged(assetPath);
            // A Generic rig gets no avatar unless asked; the prefab and the six bodies carry one.
            importer.avatarSetup = IsRigged(assetPath) ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
        }

        /// <summary>
        /// A child named <c>*_col</c> is the collision shape the export promised: it becomes a convex
        /// MeshCollider and stops rendering. Nothing else in an authored model gets a collider.
        /// </summary>
        private void OnPostprocessModel(UnityEngine.GameObject root)
        {
            if (!IsAuthored(assetPath)) return;
            foreach (var filter in root.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
            {
                if (!filter.name.EndsWith(ColliderSuffix, StringComparison.Ordinal)) continue;
                var collider = filter.gameObject.AddComponent<UnityEngine.MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = true;
                var renderer = filter.GetComponent<UnityEngine.Renderer>();
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            }
        }

        /// <summary>
        /// Water and glass come through the FBX opaque, whatever alpha the Blender material carried.
        /// A material named for them is made a URP/Lit alpha-blended surface with its base alpha,
        /// and a glow so it reads under the dark set.
        /// </summary>
        private void OnPostprocessMaterial(UnityEngine.Material material)
        {
            if (!IsAuthored(assetPath) || material == null) return;
            string name = material.name.ToLowerInvariant();
            if (name.Contains("neon") || name.Contains("glow")) Glow(material);
            bool translucent = name.Contains("water") || name.Contains("glass");
            if (!translucent || !material.HasProperty("_Surface")) return;
            var tint = material.color;
            tint.a = name.Contains("water") ? 0.55f : 0.35f;
            material.color = tint;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            if (name.Contains("water"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = UnityEngine.MaterialGlobalIlluminationFlags.RealtimeEmissive;
                // Faint: the set's bloom threshold is low, and a water plane that glowed at the
                // neon's strength read as a white rectangle from the overhead camera.
                material.SetColor("_EmissionColor", new UnityEngine.Color(0.02f, 0.08f, 0.14f));
            }
        }

        /// <summary>
        /// Neon materials glow. Blender's emission travels through FBX, but whether the URP importer
        /// turns it into an *enabled* emission depends on the version, so the name decides: a
        /// <c>bb_mat_neon_*</c> is lit at the strength of the house's own neon outlines.
        /// </summary>
        private static void Glow(UnityEngine.Material material)
        {
            if (!material.HasProperty("_EmissionColor")) return;
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = UnityEngine.MaterialGlobalIlluminationFlags.RealtimeEmissive;
            // A lamp shade glows softly; the neon glows at the house's own strength.
            material.SetColor("_EmissionColor", material.color * (material.name.ToLowerInvariant().Contains("neon") ? 3.2f : 1.4f));
        }

        private void OnPreprocessAnimation()
        {
            if (!IsAnimation(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                // A take exported from Blender is named "<armature>|<take>"; the clip is the take.
                int bar = clip.name.LastIndexOf('|');
                if (bar >= 0 && bar < clip.name.Length - 1) clip.name = clip.name.Substring(bar + 1);
                bool loop = clip.name.EndsWith(LoopSuffix, StringComparison.Ordinal);
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
    }
}

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
    /// nothing twice. Colliders are never generated <em>here</em>: the shell and set pieces carry
    /// their own <c>_col</c> meshes, and everything else is boxed in the scene by
    /// <c>HouseFurnitureCollision</c> rather than at import. Existing materials beside the
    /// model are matched by name, which is what lets a re-export keep the material a scene already
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

        // Reimport authored models after replacing Unity's obsolete External material mode.
        public override uint GetVersion() => 2;

        public static bool IsAuthored(string path) => path != null && path.StartsWith(Root, StringComparison.Ordinal);

        public static bool IsRigged(string path) => IsAuthored(path)
            && (path.StartsWith(Root + "Characters/", StringComparison.Ordinal)
                || path.StartsWith(Root + "Wardrobe/", StringComparison.Ordinal)
                || path.StartsWith(Root + "Animation/", StringComparison.Ordinal));

        public static bool IsAnimation(string path) => IsAuthored(path)
            && path.StartsWith(Root + "Animation/", StringComparison.Ordinal);

        /// <summary>
        /// Clips for the UMA cast, which is Humanoid: mocap takes retargeted onto whatever body UMA
        /// builds. One take a file, and the file says which take it is - <c>bb_anim_SitTalk_loop.fbx</c>
        /// carries <c>SitTalk_loop</c> - because a mocap library names every clip after the service
        /// that made it and a controller cannot wire twelve clips all called the same thing.
        /// </summary>
        public static bool IsHumanoidAnimation(string path) => IsAuthored(path)
            && path.StartsWith(Root + "Animation/Humanoid/", StringComparison.Ordinal);

        /// <summary>The take a Humanoid clip file carries, from its name; empty for anything else.</summary>
        public static string HumanoidTake(string path)
        {
            if (!IsHumanoidAnimation(path)) return "";
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            return file.StartsWith(AnimationPrefix, StringComparison.Ordinal) ? file.Substring(AnimationPrefix.Length) : file;
        }

        public const string AnimationPrefix = "bb_anim_";

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
            // A flat map, always. A hand-written .meta copied from another asset can carry a cube
            // shape, and a texture twice as tall as it is wide then imports as a Cubemap: the file
            // is fine, the importer is there, and every LoadAssetAtPath<Texture2D> for it returns
            // null, which is a confusing way to lose a plant's leaves.
            importer.textureShape = TextureImporterShape.Texture2D;
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
            importer.isReadable = false;
            importer.addCollider = false;
            importer.importBlendShapes = true;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // A Humanoid clip is retargeted onto whatever UMA builds, so it is imported against the
            // rig it was captured on. The take is what is wanted; the actor who performed it is not,
            // so nothing of their materials comes with it.
            if (IsHumanoidAnimation(assetPath)) importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationType = IsRigged(assetPath) ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None;
            importer.importAnimation = IsAnimation(assetPath);
            // A prop is lightmapped, and its box-projected UVs tile past 0..1, so the lightmapper
            // gets a second set of its own. A body is lit by probes and needs none.
            importer.generateSecondaryUV = !IsRigged(assetPath);
            // A prop has no rig and needs no avatar; a take carries the avatar it was captured on.
            importer.avatarSetup = IsRigged(assetPath) || IsHumanoidAnimation(assetPath)
                ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
        }

        /// <summary>
        /// Keep the shipped material assets (including their textures and hand-tuned water/neon
        /// settings) instead of creating replacement sub-assets. An explicit importer remap wins;
        /// otherwise use the same local Materials/name.mat convention as the former External mode.
        /// A new, unmatched material can safely remain a model sub-asset until an artist extracts it.
        /// </summary>
        private UnityEngine.Material OnAssignMaterialModel(UnityEngine.Material material, UnityEngine.Renderer renderer)
        {
            if (!IsAuthored(assetPath) || material == null) return null;
            var identifier = new AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), material.name);
            if (assetImporter.GetExternalObjectMap().TryGetValue(identifier, out var mapped)
                && mapped is UnityEngine.Material explicitMaterial) return explicitMaterial;
            string directory = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(directory + "/Materials/" + material.name + ".mat");
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

        /// <summary>
        /// Quaternius's Universal Animation Library (CC0), committed whole: 45 takes on one
        /// Humanoid skeleton. Only the takes in <see cref="LibraryClips"/> are imported from it.
        /// </summary>
        public const string LibraryPath = "Assets/Gamesim/Art/External/QuaterniusCharacters/AnimationLibrary_Unity_Standard.fbx";

        /// <summary>
        /// The clips the house takes from the library: the clip's name, the take it is cut from,
        /// and whether it loops. A take may be cut twice - the reach at the counter is one reach
        /// when a houseguest picks something up and a working loop when they cook.
        /// </summary>
        public static readonly (string clip, string take, bool loop)[] LibraryClips =
        {
            // Override keys for the UMA body's own idle and run: never played on a UMA body, and a
            // real idle and jog if the override ever fails, where the sleep takes that held these
            // places before would have laid the whole cast down.
            ("Idle_Loop", "Idle_Loop", true),
            ("Jog_Fwd_Loop", "Jog_Fwd_Loop", true),
            ("Swim_Idle_Loop", "Swim_Idle_Loop", true),
            ("Swim_Fwd_Loop", "Swim_Fwd_Loop", true),
            ("Cook_Loop", "Interact", true),
            ("Dance_Loop", "Dance_Loop", true),
        };

        private void OnPreprocessAnimation()
        {
            if (assetPath == LibraryPath) { ImportLibrary((ModelImporter)assetImporter); return; }
            if (!IsAnimation(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            string humanoidTake = HumanoidTake(assetPath);
            foreach (var clip in clips)
            {
                // A take exported from Blender is named "<armature>|<take>"; the clip is the take.
                int bar = clip.name.LastIndexOf('|');
                if (bar >= 0 && bar < clip.name.Length - 1) clip.name = clip.name.Substring(bar + 1);
                // A mocap take is named after the service that made it, which says nothing. The file
                // name is the take, so the clip takes the file's name instead.
                if (humanoidTake.Length > 0) clip.name = humanoidTake;
                ApplyTakeRules(clip, IsHumanoidAnimation(assetPath));
            }
            importer.clipAnimations = clips;
        }

        /// <summary>
        /// The library's clips: cut by <see cref="LibraryClips"/>, under the same rule as the
        /// mocap takes. Its skeleton stays Humanoid with the avatar Unity maps from it.
        /// </summary>
        private static void ImportLibrary(ModelImporter importer)
        {
            var takes = importer.defaultClipAnimations;
            var clips = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (var (name, take, loop) in LibraryClips)
            {
                // The file names its takes "Rig|Idle_Loop": the armature, then the take.
                ModelImporterClipAnimation source = null;
                foreach (var candidate in takes)
                    if (candidate.takeName == take || candidate.takeName.EndsWith("|" + take, StringComparison.Ordinal))
                    { source = candidate; break; }
                if (source == null) continue;
                var clip = new ModelImporterClipAnimation
                {
                    name = name, takeName = source.takeName, firstFrame = source.firstFrame, lastFrame = source.lastFrame,
                };
                ApplyTakeRules(clip, true);
                clip.loopTime = loop;
                clip.loopPose = loop;
                clips.Add(clip);
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.clipAnimations = clips.ToArray();
        }

        /// <summary>
        /// How one take is imported, by its name and its rig. Public so a test can hold this rule
        /// itself and not only the metas it last wrote: the metas change only on a reimport, so a
        /// test that read nothing else would go on passing over a rule that had been reverted.
        /// </summary>
        public static void ApplyTakeRules(ModelImporterClipAnimation clip, bool humanoid)
        {
            // The library spells it "_Loop".
            bool loop = clip.name.EndsWith(LoopSuffix, StringComparison.OrdinalIgnoreCase);
            clip.loopTime = loop;
            clip.loopPose = loop;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            // Baked into the pose, a take's travel moves the body away from its root; a take that
            // walks keeps it as root motion instead, which nothing applies - the agent is where a
            // body stands. See TravellingTakes.
            clip.lockRootPositionXZ = !TravellingTakes.Contains(clip.name);
            // A mocap take is turned to face the way its body faces. "Original" kept the heading
            // stored in the file, and the Mixamo files store one about 180 degrees from Unity's
            // forward: every walk, talk, sit and reaction played facing away from the way the
            // houseguest was going or looking. A body that is not upright - lying, swimming - has
            // no forward for Unity to read, so those keep the file's heading, turned by an offset.
            // The Blender takes keep "Original": their own export decides where they face.
            bool horizontal = HorizontalTakes.TryGetValue(clip.name, out float offset);
            clip.keepOriginalOrientation = !humanoid || horizontal;
            clip.rotationOffset = humanoid && horizontal ? offset : 0f;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }

        /// <summary>
        /// Takes whose body walks somewhere as they play. Every other take stays over its root,
        /// so baking its horizontal motion into the pose is harmless and keeps a talk's sway. A take
        /// that walks, baked the same way, carries the visible body ahead of the root while the
        /// agent stands still: the stop take walked every houseguest 0.8 m past where they stopped
        /// and held them there, then snapped them back as it gave way to idle - through the front
        /// door while it was still shut, and into whatever stood 0.8 m ahead in the house.
        /// Measured on a UMA body at the opening's door mark: hips at x -3.04 with the root at -3.85.
        ///
        /// <para>The hip-hop dance is one too: its six seconds carry the hips 1.49 m forward and
        /// the loop starts them back where they began, so baked into the pose a dancer would stride
        /// off their spot and snap back to it every cycle. As root motion it dances where it stands.</para>
        /// </summary>
        public static readonly System.Collections.Generic.ISet<string> TravellingTakes =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) { "WalkStop", "WalkTurn180", "DanceHipHop_loop" };

        /// <summary>
        /// Humanoid takes whose body lies or swims, where "Based Upon: Body Orientation" means
        /// nothing: each keeps the file's heading and turns by the offset here, in degrees.
        ///
        /// <para>Each offset is measured, not reasoned: the lying and swimming states in
        /// <c>UmaFacingPlayModeTests</c> say where the head lies from the hips, and the offset turns
        /// it to lie ahead of them. <c>SleepLying_loop</c> is played by nothing and stays at zero.</para>
        /// </summary>
        public static readonly System.Collections.Generic.IReadOnlyDictionary<string, float> HorizontalTakes =
            new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal)
            {
                // Measured on a UMA body (UmaFacingPlayModeTests): the head lay 117 degrees round
                // from the root's forward, towards -x, and a sleeper is laid head-forward along the
                // bed. The offset turns the root the other way from its sign: +117 took the head
                // to +126, -117 takes it to nought.
                ["Sleep_loop"] = -117f,
                ["SleepLying_loop"] = 0f,
                // Front crawl: face down along the water, with no upright forward. The file swims
                // it head-backward.
                ["Swim_Fwd_Loop"] = 180f,
            };
    }
}

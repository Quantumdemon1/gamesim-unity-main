using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.House;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Gamesim.Editor
{
    /// <summary>
    /// The night the mockups are lit by, as one re-runnable pass over the episode scene
    /// (VISUAL-TARGET.md, phase V1).
    ///
    /// <para><see cref="HouseLighting"/> gave every room a fill and every lamp a practical, all
    /// realtime and none of it bouncing: light hit a surface and stopped there. This pass runs
    /// after it and changes what that light does. The architecture becomes static and gets
    /// lightmap UVs, the lights become Mixed so their bounce is baked and their direct term stays
    /// live, the lamps alone cast soft shadows, the neon strips join the bake as emitters, the sun
    /// becomes a moon, a night sky replaces the flat ambient, a probe grid per room carries the
    /// baked light onto the cast, a reflection probe per room gives glass and marble something to
    /// reflect, the camera gets anti-aliasing, and the grade moves to ACES with cool shadows and warm
    /// highlights. A second, zero-weight volume holds depth of field for the director's close shots.</para>
    ///
    /// <para>Everything it creates lives under one root, <see cref="RootName"/>, and one lighting
    /// settings asset, so re-running replaces rather than duplicates, and removing is deleting one
    /// object. The bake itself is a separate step (<see cref="Bake"/>) because it takes minutes and
    /// a domain reload under it would abandon it.</para>
    /// </summary>
    public static class HouseCinematicLighting
    {
        public const string ScenePath = "Assets/Gamesim/Scenes/EpisodeHouse.unity";
        public const string RootName = "Gamesim Baked Lighting";
        public const string ProbesName = "Light probes";
        public const string ReflectionsName = "Reflection probes";
        public const string CloseUpVolumeName = "Close-up Volume";
        public const string ArchitectureRoot = "House Architecture";
        public const string LightingSettingsPath = "Assets/Gamesim/Scenes/EpisodeHouse.lighting";
        public const string SkyMaterialPath = "Assets/Gamesim/Art/Authored/Materials/bb_mat_sky_night.mat";
        public const string SkyTexturePath = "Assets/Gamesim/Art/Authored/Textures/Sky/dikhololo_night_2k.hdr";
        public const string ProfilePath = "Assets/Gamesim/Art/PostProcessing/HouseVolumeProfile.asset";
        public const string CloseUpProfilePath = "Assets/Gamesim/Art/PostProcessing/HouseCloseUpProfile.asset";

        /// <summary>The floors the room query recognises; their colliders are the rooms' extents.</summary>
        private static readonly string[] FloorNames =
        {
            "Living room floor", "Kitchen floor", "Bedroom floor", "Private room floor", "Competition yard floor",
            "HoH floor", "Nomination floor", "Games floor",
        };

        private const float ProbeSpacing = 1.5f;
        private const float ProbeInset = 0.5f;
        private static readonly float[] ProbeHeights = { 0.6f, 1.8f };
        private const float MoonIntensity = 0.45f;

        [MenuItem("Gamesim/U07/Light the house (cinematic)")]
        public static void ApplyToEpisode()
        {
            Apply(ScenePath);
        }

        [MenuItem("Gamesim/U07/Bake the house")]
        public static void BakeFromMenu()
        {
            Bake();
        }

        /// <summary>Batch entry: apply, then bake synchronously and save.</summary>
        public static void ApplyAndBakeFromCommandLine()
        {
            Apply(ScenePath);
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            Lightmapping.lightingSettings = settings;
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        public static void Apply(string scenePath)
        {
            var active = SceneManager.GetActiveScene();
            var scene = active.IsValid() && active.path == scenePath ? active
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int statics = MarkStatic(scene);
            int lights = ConvertLights(scene);
            int emitters = MarkEmitters(scene);
            Sky();
            var root = Rebuild(scene);
            int probes = Probes(root);
            int reflections = Reflections(root);
            CloseUp(root);
            AntiAliasing();
            AmbientOcclusion();
            Grade();
            var settings = Settings();
            Lightmapping.lightingSettings = settings;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gamesim] Cinematic lighting · " + statics + " renderers static, " + lights + " lights mixed, "
                      + emitters + " emissive materials baking, " + probes + " light probes, " + reflections
                      + " reflection probes, SMAA on the camera, ACES grade; settings at " + LightingSettingsPath
                      + ". Bake with Gamesim/U07/Bake the house.");
        }

        /// <summary>Starts the bake. Minutes on the GPU lightmapper; poll <c>Lightmapping.isRunning</c>.</summary>
        public static void Bake()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null) throw new System.InvalidOperationException("Run 'Light the house (cinematic)' first; no " + LightingSettingsPath);
            Lightmapping.lightingSettings = settings;
            if (!Lightmapping.BakeAsync()) throw new System.InvalidOperationException("The lightmapper declined to start.");
            Debug.Log("[Gamesim] Cinematic lighting · bake started (" + settings.lightmapper + ", " + settings.lightmapResolution + " texels/unit).");
        }

        // ---------------------------------------------------------------- the pieces

        /// <summary>
        /// The architecture — shell, floors, set pieces, the neon — contributes to GI and is a
        /// reflection-probe and occlusion static. Batching is left off: renderers are toggled and
        /// re-materialed at runtime (the memory wall, the screens), and combined meshes would break that.
        /// </summary>
        private static int MarkStatic(Scene scene)
        {
            var architecture = scene.GetRootGameObjects().FirstOrDefault(go => go.name == ArchitectureRoot);
            if (architecture == null) throw new System.InvalidOperationException("No '" + ArchitectureRoot + "' in " + scene.path);
            const StaticEditorFlags flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic;
            int count = 0;
            foreach (var renderer in architecture.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = renderer.gameObject;
                if (go.GetComponent<Animator>() != null || go.GetComponentInParent<Animator>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                renderer.receiveGI = ReceiveGI.Lightmaps;
                // Small clutter takes a fraction of the atlas; the shell takes its full share.
                var size = renderer.bounds.size.magnitude;
                var serialized = new SerializedObject(renderer);
                var scale = serialized.FindProperty("m_ScaleInLightmap");
                if (scale != null) { scale.floatValue = size < 0.6f ? 0.3f : size < 1.5f ? 0.6f : 1f; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                count++;
            }
            return count;
        }

        /// <summary>
        /// Every fitted light becomes Mixed: its bounce is baked, its direct term stays realtime so
        /// the cast is lit as it walks. Lamps cast soft shadows; fills and screens cast none. The
        /// Sun becomes a moon: cool, dim, low, with soft shadows across the yard.
        /// </summary>
        private static int ConvertLights(Scene scene)
        {
            int count = 0;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != scene) continue;
                if (light.type == LightType.Directional)
                {
                    light.name = "Moon";
                    light.color = new Color(0.62f, 0.72f, 1.0f);
                    light.intensity = MoonIntensity;
                    light.transform.rotation = Quaternion.Euler(52f, 215f, 0f);
                    light.lightmapBakeType = LightmapBakeType.Mixed;
                    light.shadows = LightShadows.Soft;
                    light.shadowStrength = 0.75f;
                    count++;
                    continue;
                }
                bool lamp = light.name.IndexOf("lamp", System.StringComparison.OrdinalIgnoreCase) >= 0
                            && light.name.EndsWith(" practical", System.StringComparison.Ordinal);
                bool fill = light.name.EndsWith(" fill", System.StringComparison.Ordinal);
                light.lightmapBakeType = LightmapBakeType.Mixed;
                light.shadows = lamp ? LightShadows.Soft : LightShadows.None;
                if (lamp) { light.shadowStrength = 0.6f; light.shadowBias = 0.02f; light.shadowNormalBias = 0.4f; }
                // The bake now carries the bounce, so the fills come down a little or the rooms wash out.
                if (fill) light.intensity = Mathf.Min(light.intensity, 4.0f);
                count++;
            }
            return count;
        }

        /// <summary>The neon strips, the glow materials and the screens light their surroundings in the bake.</summary>
        private static int MarkEmitters(Scene scene)
        {
            var seen = new HashSet<Material>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != scene) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !seen.Add(material)) continue;
                    string name = material.name.ToLowerInvariant();
                    bool emitter = material.IsKeywordEnabled("_EMISSION")
                                   || name.Contains("neon") || name.Contains("glow") || name.Contains("screen");
                    if (!emitter) { seen.Remove(material); continue; }
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    EditorUtility.SetDirty(material);
                }
            }
            return seen.Count;
        }

        /// <summary>A CC0 night sky (Poly Haven, Dikhololo Night) as the skybox, the ambient and the reflections, with a thin blue haze.</summary>
        private static void Sky()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath);
            if (texture == null) throw new System.InvalidOperationException("No sky at " + SkyTexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SkyMaterialPath));
                material = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }
            material.shader = Shader.Find("Skybox/Panoramic");
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Exposure", 0.7f);
            material.SetFloat("_Rotation", 40f);
            EditorUtility.SetDirty(material);

            RenderSettings.skybox = material;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.7f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.010f;
            RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.12f);
            RenderSettings.sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        }

        private static GameObject Rebuild(Scene scene)
        {
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
            if (existing != null) Object.DestroyImmediate(existing);
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static IEnumerable<Bounds> Floors()
        {
            foreach (var collider in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (System.Array.IndexOf(FloorNames, collider.gameObject.name) >= 0)
                    yield return collider.bounds;
        }

        /// <summary>A grid of probes per room at two heights, so the cast is lit by the room it stands in.</summary>
        private static int Probes(GameObject root)
        {
            var holder = new GameObject(ProbesName);
            holder.transform.SetParent(root.transform, false);
            var group = holder.AddComponent<LightProbeGroup>();
            var positions = new List<Vector3>();
            foreach (var floor in Floors())
            {
                for (float x = floor.min.x + ProbeInset; x <= floor.max.x - ProbeInset + 0.01f; x += ProbeSpacing)
                for (float z = floor.min.z + ProbeInset; z <= floor.max.z - ProbeInset + 0.01f; z += ProbeSpacing)
                foreach (var y in ProbeHeights)
                    positions.Add(new Vector3(x, floor.max.y + y, z));
            }
            group.probePositions = positions.ToArray();
            return positions.Count;
        }

        /// <summary>One baked, box-projected reflection probe per room, the size of its floor.</summary>
        private static int Reflections(GameObject root)
        {
            var holder = new GameObject(ReflectionsName);
            holder.transform.SetParent(root.transform, false);
            int count = 0;
            foreach (var floor in Floors())
            {
                var go = new GameObject("Reflection " + count);
                go.transform.SetParent(holder.transform, false);
                go.transform.position = new Vector3(floor.center.x, floor.max.y + 1.4f, floor.center.z);
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Baked;
                probe.size = new Vector3(floor.size.x + 0.6f, 3.6f, floor.size.z + 0.6f);
                probe.boxProjection = true;
                probe.resolution = 128;
                probe.hdr = true;
                probe.importance = 1;
                probe.intensity = 1f;
                probe.shadowDistance = 20f;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Depth of field for the close shots, on its own volume at weight zero. The director raises
        /// the weight for a two-shot or the diary chair and lowers it again; nothing here decides when.
        /// </summary>
        private static void CloseUp(GameObject root)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(CloseUpProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, CloseUpProfilePath);
            }
            var dof = Ensure<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(4f);
            dof.aperture.Override(5.6f);
            dof.focalLength.Override(50f);
            EditorUtility.SetDirty(profile);

            var go = new GameObject(CloseUpVolumeName);
            go.transform.SetParent(root.transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = profile;
        }

        private static void AntiAliasing()
        {
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera.targetTexture != null) continue;
                var data = camera.GetUniversalAdditionalCameraData();
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.renderPostProcessing = true;
            }
        }

        /// <summary>The desktop renderer's ambient occlusion, a touch stronger and a little wider for the corners the bake leaves soft.</summary>
        private static void AmbientOcclusion()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer == null) return;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var serialized = new SerializedObject(feature);
                var intensity = serialized.FindProperty("m_Settings.Intensity");
                var radius = serialized.FindProperty("m_Settings.Radius");
                var falloff = serialized.FindProperty("m_Settings.Falloff");
                if (intensity != null) intensity.floatValue = 0.6f;
                if (radius != null) radius.floatValue = 0.35f;
                if (falloff != null) falloff.floatValue = 100f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(renderer);
            }
        }

        /// <summary>ACES, navy shadows and warm highlights, bloom kept to the emissives, a little grain, flares on the neon.</summary>
        private static void Grade()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null) throw new System.InvalidOperationException("No volume profile at " + ProfilePath);

            var tonemapping = Ensure<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var colour = Ensure<ColorAdjustments>(profile);
            colour.postExposure.Override(0.15f);
            colour.contrast.Override(12f);
            colour.saturation.Override(6f);

            var bands = Ensure<ShadowsMidtonesHighlights>(profile);
            bands.shadows.Override(new Vector4(0.86f, 0.92f, 1.10f, 0f));
            bands.midtones.Override(new Vector4(1.0f, 1.0f, 1.0f, 0f));
            bands.highlights.Override(new Vector4(1.06f, 1.0f, 0.93f, 0f));

            var bloom = Ensure<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);

            var vignette = Ensure<Vignette>(profile);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.4f);

            var grain = Ensure<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);

            var flare = Ensure<ScreenSpaceLensFlare>(profile);
            flare.intensity.Override(0.25f);
            flare.bloomMip.Override(2);

            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// A component on the profile, added as a sub-asset when it is new: <c>Add</c> alone keeps
        /// it in memory, and a fresh import of the profile — another machine, the test mirror —
        /// would find the reference dangling and the component gone.
        /// </summary>
        private static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(true);
                component.name = typeof(T).Name;
                if (AssetDatabase.Contains(profile)) AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            EditorUtility.SetDirty(component);
            return component;
        }

        /// <summary>The GPU lightmapper, twenty texels a metre, two bounces, ambient occlusion, indirect-only mixed lights.</summary>
        private static LightingSettings Settings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }
            settings.name = "EpisodeHouse";
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.lightmapResolution = 20f;
            settings.lightmapMaxSize = 2048;
            settings.lightmapPadding = 2;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 128;
            settings.maxBounces = 2;
            settings.ao = true;
            settings.aoMaxDistance = 0.6f;
            settings.aoExponentIndirect = 1f;
            settings.aoExponentDirect = 0f;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            EditorUtility.SetDirty(settings);
            return settings;
        }
    }
}

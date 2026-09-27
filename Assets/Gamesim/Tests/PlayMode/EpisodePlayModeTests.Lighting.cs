using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The night the house is lit by (VISUAL-TARGET.md, phase V1): the episode scene ships baked
    /// light, probes for the cast, a reflection probe per room, a sky, anti-aliasing on the camera,
    /// and a close-up volume the director can weight. Pinned so a re-export or a re-run of the
    /// scene builders cannot quietly put the flat light back.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Lighting_TheEpisodeSceneShipsItsBakedNight()
        {
            yield return null;
            Assert.That(LightmapSettings.lightmaps, Is.Not.Null.And.Not.Empty, "The house is lightmapped.");
            Assert.That(LightmapSettings.lightProbes, Is.Not.Null, "There is a probe set.");
            Assert.That(LightmapSettings.lightProbes.count, Is.GreaterThan(100), "A grid per room, at two heights.");

            var reflections = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(reflections.Length, Is.GreaterThanOrEqualTo(8), "One reflection probe per room.");
            Assert.That(reflections.All(p => p.mode == ReflectionProbeMode.Baked && p.bakedTexture != null), Is.True,
                "Every probe is baked and carries its cubemap.");

            Assert.That(RenderSettings.skybox, Is.Not.Null.And.Property("name").EqualTo("bb_mat_sky_night"));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Skybox));
            Assert.That(RenderSettings.fog, Is.True, "A thin haze.");

            var moon = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);
            Assert.That(moon, Is.Not.Null);
            Assert.That(moon.name, Is.EqualTo("Moon"));
            Assert.That(moon.lightmapBakeType, Is.EqualTo(LightmapBakeType.Mixed));
            var practicals = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(l => l.type == LightType.Point).ToArray();
            Assert.That(practicals, Is.Not.Empty);
            Assert.That(practicals.All(l => l.lightmapBakeType == LightmapBakeType.Mixed), Is.True, "Bounce baked, direct live.");
            Assert.That(practicals.Any(l => l.shadows != LightShadows.None), Is.True, "The lamps cast shadows.");
            var fitted = director.gameObject.scene.GetRootGameObjects()
                .Single(root => root.name == "Gamesim Fitted Lighting");
            var shadowedLamps = fitted.GetComponentsInChildren<Light>(true)
                .Where(l => l.type == LightType.Point && l.shadows != LightShadows.None).ToArray();
            Assert.That(shadowedLamps, Has.Length.EqualTo(14), "All fourteen authored practicals keep their shadows.");
            Assert.That(shadowedLamps.All(l => l.shadowResolution == LightShadowResolution.FromQualitySettings
                && l.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier
                    == UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh), Is.True,
                "The authored per-light settings remain intact; unsupported numeric enum overrides must not run at startup.");

            var camera = cameraRig.ViewCamera;
            Assert.That(camera, Is.Not.Null);
            var data = camera.GetUniversalAdditionalCameraData();
            Assert.That(data.antialiasing, Is.Not.EqualTo(AntialiasingMode.None), "The camera is anti-aliased.");
            Assert.That(data.renderPostProcessing, Is.True);

            var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var closeUp = volumes.FirstOrDefault(v => v.name == "Close-up Volume");
            Assert.That(closeUp, Is.Not.Null, "The close-up volume exists for the director to weight.");
            Assert.That(closeUp.weight, Is.EqualTo(0f).Within(0.001f), "and rests at zero.");
            Assert.That(closeUp.sharedProfile.TryGet<DepthOfField>(out var dof) && dof.active, Is.True);
            var grade = volumes.First(v => v.name == "Global Volume").sharedProfile;
            Assert.That(grade.TryGet<Tonemapping>(out var tonemapping) && tonemapping.mode.value == TonemappingMode.ACES, Is.True);
            Assert.That(grade.TryGet<ShadowsMidtonesHighlights>(out var bands) && bands.active, Is.True);
        }

        [UnityTest]
        public IEnumerator Lighting_SeasonRestartPreservesFittedLightsAndThePipelineShadowSettings()
        {
            var fitted = director.gameObject.scene.GetRootGameObjects()
                .Single(root => root.name == "Gamesim Fitted Lighting");
            var lights = fitted.GetComponentsInChildren<Light>(true)
                .Select(light => new
                {
                    Light = light, light.enabled, light.type, light.intensity, light.range, light.color,
                    light.shadows, light.shadowStrength, light.shadowResolution, light.shadowCustomResolution,
                    light.lightmapBakeType,
                    Data = light.GetComponent<UniversalAdditionalLightData>(),
                    Tier = light.GetComponent<UniversalAdditionalLightData>()?.additionalLightsShadowResolutionTier,
                }).ToArray();
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline, Is.Not.Null);
            int atlas = pipeline.additionalLightsShadowmapResolution;
            int low = pipeline.additionalLightsShadowResolutionTierLow;
            int medium = pipeline.additionalLightsShadowResolutionTierMedium;
            int high = pipeline.additionalLightsShadowResolutionTierHigh;
            var bakedMaps = LightmapSettings.lightmaps.Select(map => map.lightmapColor).ToArray();

            director.StartSeason(null);
            yield return null;
            yield return null;

            foreach (var original in lights)
            {
                var light = original.Light;
                Assert.That(light, Is.Not.Null, "Starting a season must retain every authored light.");
                Assert.That(light.enabled, Is.EqualTo(original.enabled), light.name);
                Assert.That(light.type, Is.EqualTo(original.type), light.name);
                Assert.That(light.intensity, Is.EqualTo(original.intensity), light.name);
                Assert.That(light.range, Is.EqualTo(original.range), light.name);
                Assert.That(light.color, Is.EqualTo(original.color), light.name);
                Assert.That(light.shadows, Is.EqualTo(original.shadows), light.name);
                Assert.That(light.shadowStrength, Is.EqualTo(original.shadowStrength), light.name);
                Assert.That(light.shadowResolution, Is.EqualTo(original.shadowResolution), light.name);
                Assert.That(light.shadowCustomResolution, Is.EqualTo(original.shadowCustomResolution), light.name);
                Assert.That(light.lightmapBakeType, Is.EqualTo(original.lightmapBakeType), light.name);
                var data = light.GetComponent<UniversalAdditionalLightData>();
                Assert.That(data, Is.SameAs(original.Data), light.name);
                Assert.That(data?.additionalLightsShadowResolutionTier, Is.EqualTo(original.Tier), light.name);
            }
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.SameAs(pipeline));
            Assert.That(pipeline.additionalLightsShadowmapResolution, Is.EqualTo(atlas));
            Assert.That(pipeline.additionalLightsShadowResolutionTierLow, Is.EqualTo(low));
            Assert.That(pipeline.additionalLightsShadowResolutionTierMedium, Is.EqualTo(medium));
            Assert.That(pipeline.additionalLightsShadowResolutionTierHigh, Is.EqualTo(high));
            Assert.That(LightmapSettings.lightmaps.Select(map => map.lightmapColor).ToArray(), Is.EqualTo(bakedMaps));
        }
    }
}

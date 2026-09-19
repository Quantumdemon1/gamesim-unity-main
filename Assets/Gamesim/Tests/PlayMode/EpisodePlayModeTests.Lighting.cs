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
    }
}

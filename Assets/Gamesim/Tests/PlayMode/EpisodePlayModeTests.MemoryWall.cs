using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The memory wall reports the game in the world rather than in the HUD, which makes it the one
    /// piece of presentation a screenshot is a bad way to check: whether it is in frame depends on
    /// where the camera happens to be, and the camera is not what is being tested.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MemoryWall_CarriesEveryHouseguestAndDarkensTheEvicted()
        {
            var wall = SceneComponents<MemoryWall>().FirstOrDefault();
            Assert.That(wall, Is.Not.Null, "The episode scene should carry a memory wall.");

            var frames = Frames(wall);
            var state = director.Snapshot;
            Assert.That(frames.Length, Is.EqualTo(state.contestants.Count),
                "The wall should have exactly one frame per houseguest.");

            yield return null;

            // Each frame shows the portrait of the contestant it is bound to. Asserted against the
            // exact texture rather than merely "something is assigned", because every frame sharing
            // one face would satisfy the weaker check and be the more likely bug.
            for (int i = 0; i < frames.Length; i++)
            {
                var actor = state.contestants[i];
                var expected = CharacterPortraits.Get(
                    CharacterPresentation.AppearanceId(actor, ContentCatalog.CanonicalId(actor.id)));
                if (expected == null) continue; // persona without authored art; the slot stays dark

                Assert.That(PortraitTexture(frames[i]), Is.SameAs(expected),
                    "Frame " + i + " should show " + actor.name + ".");
            }

            // How big the wall actually is on screen at the shipped framing. Reported, not graded:
            // the fixture is correct either way, and whether a player can read it is a camera
            // question — the rig clamps its focus away from the perimeter and pitches 55 degrees
            // down, which foreshortens every vertical surface in the set, not just this one.
            ReportWallLegibility(wall);

            // Drive to an eviction and check the wall follows the committed status.
            string evicted = null;
            for (int guard = 0; guard < 400 && evicted == null; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;

                evicted = result.state.contestants
                    .FirstOrDefault(actor => actor.status != ContestantStatus.Active
                        && before.contestants.Any(was => was.id == actor.id && was.status == ContestantStatus.Active))
                    ?.id;
            }
            Assert.That(evicted, Is.Not.Null, "The episode should have evicted somebody within the guard.");

            yield return null;
            var after = director.Snapshot;

            var stillIn = after.contestants.First(actor => actor.status == ContestantStatus.Active);
            var outFrame = wall.FrameFor(evicted);
            var inFrame = wall.FrameFor(stillIn.id);
            Assert.That(outFrame, Is.Not.Null, "The evicted houseguest should still have a frame.");
            Assert.That(inFrame, Is.Not.Null, "A houseguest still playing should have a frame.");

            float darkened = Brightness(outFrame);
            float lit = Brightness(inFrame);
            Assert.That(darkened, Is.LessThan(lit),
                "The evicted houseguest's frame should be darker than a houseguest still in the game "
                + "(" + after.Find(evicted).name + " " + darkened.ToString("0.###")
                + " vs " + stillIn.name + " " + lit.ToString("0.###") + ").");

            Debug.Log(string.Format("[Gamesim] memory wall · {0} evicted, frame brightness {1:0.###} against {2:0.###} lit",
                after.Find(evicted).name, darkened, lit));
        }

        /// <summary>Logs the wall's projected size at the camera the player actually has.</summary>
        private void ReportWallLegibility(MemoryWall wall)
        {
            var camera = cameraRig.ViewCamera;
            var renderers = wall.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (renderers.Length == 0 || camera.pixelHeight <= 0) return;

            var extent = renderers[0].bounds;
            foreach (var renderer in renderers) extent.Encapsulate(renderer.bounds);

            var low = camera.WorldToScreenPoint(new Vector3(extent.center.x, extent.min.y, extent.center.z));
            var high = camera.WorldToScreenPoint(new Vector3(extent.center.x, extent.max.y, extent.center.z));
            bool inFront = low.z > 0f && high.z > 0f;
            float pixels = Mathf.Abs(high.y - low.y);

            Debug.Log(string.Format(
                "[Gamesim] memory wall legibility · {0:F1} px tall, {1:P1} of frame height, {2:F1} m from camera, "
                + "on screen: {3}",
                pixels, camera.pixelHeight > 0 ? pixels / camera.pixelHeight : 0f,
                Vector3.Distance(camera.transform.position, extent.center),
                inFront && low.x > 0f && low.x < camera.pixelWidth ? "yes" : "no"));
        }

        private static Transform[] Frames(MemoryWall wall) =>
            wall.transform.Cast<Transform>()
                .Where(child => child.name.StartsWith(MemoryWall.FramePrefix, System.StringComparison.Ordinal))
                .OrderBy(child => child.name, System.StringComparer.Ordinal)
                .ToArray();

        private static Texture PortraitTexture(Transform frame)
        {
            var renderer = frame.Find(MemoryWall.PortraitChild)?.GetComponent<Renderer>();
            var material = renderer != null ? renderer.material : null;
            if (material == null) return null;
            if (material.HasProperty("_BaseMap")) return material.GetTexture("_BaseMap");
            return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        }

        /// <summary>The frame's tint value, which is what dimming an evicted houseguest changes.</summary>
        private static float Brightness(Transform frame)
        {
            var renderer = frame.Find(MemoryWall.PortraitChild)?.GetComponent<Renderer>();
            var material = renderer != null ? renderer.material : null;
            if (material == null) return 0f;
            var colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
                : material.HasProperty("_Color") ? material.GetColor("_Color")
                : Color.black;
            return colour.grayscale;
        }
    }
}

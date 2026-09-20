using System.Collections;
using System.Linq;
using Gamesim.Episode;
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

        /// <summary>
        /// The section rail has to actually go somewhere. Chrome that is present and inert is the
        /// failure this guards: it renders identically either way, so a screenshot cannot tell.
        /// </summary>
        [UnityTest]
        public IEnumerator IconRail_JumpsToEverySectionOfTheNotebook()
        {
            var sections = new[]
            {
                EpisodeDirector.NotebookSection.Network,
                EpisodeDirector.NotebookSection.Rooms,
                EpisodeDirector.NotebookSection.Story,
            };

            foreach (var section in sections)
            {
                director.ClosePanels();
                yield return null;

                var rail = director.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == IconRail.RootName && rect.gameObject.activeInHierarchy);
                Assert.That(rail, Is.Not.Null, "The HUD should carry the section rail.");

                var buttons = rail.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                Assert.That(buttons, Has.Length.EqualTo(5), "The rail should carry four sections and the overview.");

                director.ShowNotebookSection(section);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();

                Assert.That(director.IsPanelOpen, Is.True, "Jumping to " + section + " should open the notebook.");
                var found = director.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == section);
                Assert.That(found, Is.Not.Null, "The notebook should contain " + section + ".");
            }

            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The ambient house-activity caption — the overlay that reports two houseguests talking
        /// near you — renders, and clears the chrome above it.
        ///
        /// <para>Until now it was asserted only in the negative: three tests check it stays empty
        /// when the player is not entitled to see a conversation, which guards the knowledge
        /// boundary and says nothing about whether it works when it should. It also sits at a fixed
        /// offset below the top edge, which the house pill now occupies — exactly the kind of
        /// collision that is invisible until two features written months apart meet.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AmbientCaption_ShowsAConversationAndClearsTheChrome()
        {
            var caption = SceneComponents<Gamesim.Episode.HouseConversationCaption>().FirstOrDefault();
            Assert.That(caption, Is.Not.Null, "The episode should stage the conversation caption.");
            Assert.That(director.ObservedNpcConversation, Is.Empty, "Nothing should be observed yet.");

            caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(director.ObservedNpcConversation,
                Is.EqualTo("Maya Hassan and Riley Johnson are discussing the game."),
                "The caption should describe the pair with the allowlisted topic.");

            var panel = caption.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == "Witnessed generic topic");
            Assert.That(panel, Is.Not.Null, "The caption should have built its panel.");

            var pill = director.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == "House pill" && rect.gameObject.activeInHierarchy);
            Assert.That(pill, Is.Not.Null, "The HUD should carry the house pill.");

            var captionRect = ScreenRect(panel);
            var pillRect = ScreenRect(pill);
            Assert.That(captionRect.Overlaps(pillRect), Is.False,
                "The ambient caption " + captionRect + " overlaps the house pill " + pillRect + ".");

            yield return Shoot("walkthrough-13-ambient-caption");

            caption.Hide();
            yield return null;
            Assert.That(director.ObservedNpcConversation, Is.Empty, "Hiding should clear the caption.");
        }

        /// <summary>
        /// Given somewhere to point, the caption becomes a bubble over the pair rather than a banner
        /// at the top of the screen (VISUAL-TARGET.md V2, mockups 01, 06 and 09).
        ///
        /// <para>Two things are worth pinning and neither is the wording. It must grow a tail, which
        /// is what makes it point at somebody rather than float. And it must still keep out of the
        /// house pill: a caption that follows a pair across the room will walk under the week
        /// counter unless it is clamped, and a line of prose over the week counter is worse than a
        /// line of prose in the wrong place.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AmbientCaption_AnchoredOverAPairBecomesABubbleAndStaysOffTheChrome()
        {
            var caption = SceneComponents<Gamesim.Episode.HouseConversationCaption>().FirstOrDefault();
            Assert.That(caption, Is.Not.Null, "The episode should stage the conversation caption.");
            var camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "The house camera is what the bubble is projected through.");

            var pill = director.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == "House pill" && rect.gameObject.activeInHierarchy);
            Assert.That(pill, Is.Not.Null, "The HUD should carry the house pill.");
            var pillRect = ScreenRect(pill);

            // Walk the anchor up the FRAME, not up the world: the house camera looks steeply down,
            // so world-up quickly puts a point behind it, and a point behind the camera is one the
            // bubble hides rather than clamps. Along the camera's own up axis the anchor stays six
            // metres in front at every step, and the last of them is well above the top of the
            // screen - which is exactly where an unclamped bubble would land on the chrome.
            // Measured in the same frame it is shown, with no yield between. The NPC runtime hides
            // this caption on any frame where no conversation is actually being witnessed, and the
            // test is staging one by hand - so yielding hands the director a frame in which to take
            // it away again, which it duly does on the second pass.
            foreach (var lift in new[] { 0f, 2f, 6f, 14f })
            {
                caption.Show("Maya Hassan", "Riley Johnson", "strategy", 1f,
                    camera.transform.position + camera.transform.forward * 6f + camera.transform.up * lift);
                Canvas.ForceUpdateCanvases();

                var panel = caption.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == Gamesim.Episode.HouseConversationCaption.PanelName);
                Assert.That(panel, Is.Not.Null, "The caption should have built its panel.");
                var tail = caption.GetComponentsInChildren<RectTransform>(true)
                    .FirstOrDefault(rect => rect.name == Gamesim.Episode.HouseConversationCaption.TailName);
                Assert.That(tail, Is.Not.Null, "An anchored caption should have built its tail.");
                Assert.That(tail.gameObject.activeInHierarchy, Is.True,
                    "The tail is what makes the bubble point at somebody; a banner has none. Lift "
                    + lift + ": tail self " + tail.gameObject.activeSelf
                    + ", panel in hierarchy " + panel.gameObject.activeInHierarchy
                    + ", canvas self " + panel.parent.gameObject.activeSelf + ".");

                Assert.That(ScreenRect(panel).Overlaps(pillRect), Is.False,
                    "Anchored " + lift + " m up, the bubble " + ScreenRect(panel)
                    + " overlaps the house pill " + pillRect + ".");
            }

            caption.Hide();
            yield return null;
            Assert.That(director.ObservedNpcConversation, Is.Empty, "Hiding should clear the caption.");
        }

        // The frames a season shows: the authored wall carries sixteen and switches the rest off.
        private static Transform[] Frames(MemoryWall wall) =>
            wall.transform.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf)
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The accessibility criteria that can be decided by measurement rather than by eye.
    ///
    /// The large-text preference is the interesting case: the HUD's fixed chrome keeps its size
    /// while the copy inside it grows by 20%, so this is exactly where text starts getting clipped
    /// and panels start colliding. Both are checked here at both ends of the range.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const string LargerTextCaption = "Use larger text";
        private const string StandardTextCaption = "Use standard text";

        [UnityTest]
        public IEnumerator Accessibility_NoCopyIsClippedAtEitherTextSize()
        {
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                yield return OpenNotebook();

                Canvas.ForceUpdateCanvases();
                yield return null;

                var clipped = director.GetComponentsInChildren<TMP_Text>(true)
                    .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                    .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
                    .Select(label => "'" + Excerpt(label.text) + "' in " + HierarchyPath(label.transform))
                    .ToArray();

                Assert.That(clipped, Is.Empty,
                    "At " + (larger ? "larger" : "standard") + " text, copy is clipped: "
                    + string.Join(" | ", clipped));

                director.ClosePanels();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Accessibility_FixedChromeNeverOverlapsAtEitherTextSize()
        {
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                director.ClosePanels();
                yield return null;
                AssertFixedChromeDoesNotOverlap(larger);

                // And with the controls box open, which is where the floor is tightest. The cast
                // strip moved to the bottom of the frame and pushed the caption and the controls box
                // up a band; the box grows upward from there toward the vibe card, and the expanded
                // state is 126 units taller than the resting one. Checking only the resting state
                // would have left the whole of that clearance to an argument rather than a
                // measurement - and an argument is what put 'Status' on top of 'Cast rail'.
                ButtonWithCaption(ExpandControlsCaption).onClick.Invoke();
                yield return null;
                AssertFixedChromeDoesNotOverlap(larger);
                ButtonWithCaption(CollapseControlsCaption).onClick.Invoke();
                yield return null;
            }
        }

        private const string ExpandControlsCaption = "Help \u00b7 controls";
        private const string CollapseControlsCaption = "Hide controls";

        [UnityTest]
        public IEnumerator Accessibility_FixedChromeLayoutTracksBodyCompletionRedraw()
        {
            var seenBodies = typeof(EpisodeDirector).GetField("seenBodiesCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(seenBodies, Is.Not.Null);
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                director.ClosePanels();
                Canvas.ForceUpdateCanvases();
                var before = ActiveChromePanel("Brand");
                Assert.That(before, Is.Not.Null);

                // Exercise the real Update -> Render branch deterministically, including in the
                // authored-body configuration. Only the director's observed counter is stale;
                // the global completion counter and cast remain untouched.
                seenBodies.SetValue(director, CharacterPresentation.BodiesCompleted - 1);
                yield return null;
                Assert.That(ActiveChromePanel("Brand"), Is.Not.SameAs(before),
                    "A newly observed body completion must rebuild the chrome after the earlier layout pass.");
                AssertFixedChromeDoesNotOverlap(larger);
            }
        }

        private RectTransform ActiveChromePanel(string name) =>
            director.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);

        private void AssertFixedChromeDoesNotOverlap(bool larger)
        {
            // Flush the hierarchy that will be measured. A yield after this pass would allow
            // UMA completion to replace it in Update, before LateUpdate / willRenderCanvases
            // has positioned the new layout-group children for the frame actually rendered.
            Canvas.ForceUpdateCanvases();
            // The modal and interaction prompt deliberately sit over the scene, unlike chrome.
            var names = new[] { "Brand", "Navigation", "Objective", "Exploration controls", "Status",
                "House pill", "Live feed", EpisodeHud.HouseVibeCardName, EpisodeHud.RecentEventsCardName,
                CastRail.RootName, IconRail.RootName };
            var panels = names.Select(ActiveChromePanel).Where(rect => rect != null).ToArray();
            Assert.That(panels, Has.Length.EqualTo(names.Length),
                "Expected every fixed panel to be present; found " + panels.Length + " of " + names.Length + ".");
            for (int a = 0; a < panels.Length; a++)
            for (int b = a + 1; b < panels.Length; b++)
            {
                var first = ScreenRect(panels[a]);
                var second = ScreenRect(panels[b]);
                Assert.That(first.Overlaps(second), Is.False,
                    "At " + (larger ? "larger" : "standard") + " text, '" + panels[a].name +
                    "' " + first + " overlaps '" + panels[b].name + "' " + second + ".");
            }
        }

        /// <summary>
        /// Captures the HUD composited over the lit set, at the three review resolutions, so whether
        /// the chrome still reads against the bloom can be judged from a frame instead of from
        /// memory.
        ///
        /// <para>This does not use <see cref="ScreenCapture"/>. In a batchmode run Unity renders
        /// without presenting, so screen captures come back solid black — the standalone harness's
        /// own PNGs from such a run are all identical black frames, which is worth knowing before
        /// anyone treats them as visual evidence. Pointing the scene camera at a RenderTexture and
        /// rendering the HUD through it produces a real frame.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_CapturesTheHudOverTheSetForReview()
        {
            if (!Application.isBatchMode) yield break;

            // Let the cast settle first. A provided body is assembled over frames, so capturing
            // immediately photographs the stand-ins rather than the houseguests, which would make
            // the review frames quietly misleading about what the game looks like.
            yield return SettleCast();

            var camera = cameraRig.ViewCamera;
            var canvas = director.GetComponentsInChildren<Canvas>(true)
                .FirstOrDefault(c => c.renderMode == RenderMode.ScreenSpaceOverlay && c.isActiveAndEnabled);
            Assert.That(canvas, Is.Not.Null, "The HUD canvas should be a screen-space overlay.");

            var previousMode = canvas.renderMode;
            var previousTarget = camera.targetTexture;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);

                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(2560, 1440) })
                {
                    var texture = new RenderTexture(size.x, size.y, 24);
                    var readback = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    var previousActive = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = texture;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        camera.Render();

                        RenderTexture.active = texture;
                        readback.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                        readback.Apply();

                        var pixels = readback.GetPixels32();
                        var distinct = new System.Collections.Generic.HashSet<int>();
                        for (int i = 0; i < pixels.Length; i += 53)
                            distinct.Add((pixels[i].r << 16) | (pixels[i].g << 8) | pixels[i].b);
                        Assert.That(distinct.Count, Is.GreaterThan(8),
                            "The " + size.x + "x" + size.y + " capture has only " + distinct.Count +
                            " sampled colours, so the set and HUD did not both render.");

                        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                            Application.dataPath, "..", "hud-over-set-" + size.x + "x" + size.y + ".png"));
                        System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                        Debug.Log("[Gamesim] HUD review capture -> " + path + " (" + distinct.Count + " sampled colours)");
                    }
                    finally
                    {
                        RenderTexture.active = previousActive;
                        camera.targetTexture = previousTarget;
                        Object.Destroy(readback);
                        texture.Release();
                        Object.Destroy(texture);
                    }
                }
            }
            finally
            {
                canvas.renderMode = previousMode;
                camera.targetTexture = previousTarget;
            }
        }

        /// <summary>
        /// Measures how large a houseguest actually is on screen at the default camera framing.
        ///
        /// "Does the cast read at gameplay distance" was written as something to judge by eye, which
        /// is how it stayed unresolved through two different casts. Apparent size is not a matter of
        /// opinion — it is the projected height of the body as a fraction of the frame — so this
        /// reports that number and leaves the judgement to a person holding it.
        ///
        /// Deliberately no pass threshold. Unlike contrast, character legibility has no standard to
        /// appeal to, and inventing one here would dress up a preference as a measurement. What the
        /// number does settle is whether the fix belongs in the character or in the camera: the cast
        /// occupying a small share of frame at the default distance is a framing problem regardless
        /// of which bodies are used.
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_ReportsHowLargeTheCastReadsOnScreen()
        {
            yield return SettleCast();
            var camera = cameraRig.ViewCamera;

            foreach (var visual in SceneComponents<CharacterPresentation>())
            {
                var body = visual.transform.Find("Gamesim Character Visual");
                if (body == null) continue;
                var parts = body.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
                if (parts.Length == 0) continue;

                var extent = parts[0].bounds;
                foreach (var part in parts) extent.Encapsulate(part.bounds);

                // Project the body's vertical extent through the camera the player is actually using.
                var feet = camera.WorldToScreenPoint(new Vector3(extent.center.x, extent.min.y, extent.center.z));
                var crown = camera.WorldToScreenPoint(new Vector3(extent.center.x, extent.max.y, extent.center.z));
                if (feet.z <= 0f || crown.z <= 0f) continue;

                float pixels = Mathf.Abs(crown.y - feet.y);
                float share = camera.pixelHeight > 0 ? pixels / camera.pixelHeight : 0f;
                Debug.Log(string.Format(
                    "[Gamesim] cast legibility · {0}: {1:F1} px tall, {2:P1} of frame height, {3:F1} m from camera",
                    visual.CharacterId, pixels, share,
                    Vector3.Distance(camera.transform.position, extent.center)));
            }
        }

        /// <summary>
        /// Renders the house at each end of the camera's zoom range and at the midpoint, with the
        /// cast's on-screen size measured for each, so "where should the camera start" can be
        /// answered by comparing frames rather than by argument.
        ///
        /// The rig allows 10 to 34 units and starts at 24, so a player can already zoom to well under
        /// half the default. The open question was never whether the camera can get close enough —
        /// it is whether starting wide is the right first impression for a slice whose contract asks
        /// for character selection and conversation close-ups.
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_ComparesCameraFramings()
        {
            if (!Application.isBatchMode) yield break;
            yield return SettleCast();

            foreach (var distance in new[] { 24f, 17f, 10f })
            {
                SetCameraDistance(distance);
                for (int i = 0; i < 40; i++) yield return null; // the rig smooths toward the target

                var camera = cameraRig.ViewCamera;
                var sizes = SceneComponents<CharacterPresentation>()
                    .Select(visual => visual.transform.Find("Gamesim Character Visual"))
                    .Where(body => body != null)
                    .Select(body => ScreenShare(camera, body))
                    .Where(share => share > 0f)
                    .ToArray();

                Debug.Log(string.Format(
                    "[Gamesim] camera framing · distance {0}: cast {1:P1} to {2:P1} of frame height",
                    distance, sizes.Length == 0 ? 0f : sizes.Min(), sizes.Length == 0 ? 0f : sizes.Max()));

                yield return CaptureFraming("camera-distance-" + distance.ToString("0"));
            }

            SetCameraDistance(24f);
            for (int i = 0; i < 20; i++) yield return null;
        }

        private void SetCameraDistance(float distance)
        {
            var type = typeof(HouseCameraRig);
            const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var name in new[] { "distance", "desiredDistance", "previousDistance" })
                type.GetField(name, any)?.SetValue(cameraRig, distance);
        }

        private static float ScreenShare(Camera camera, Transform body)
        {
            var parts = body.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (parts.Length == 0) return 0f;
            var extent = parts[0].bounds;
            foreach (var part in parts) extent.Encapsulate(part.bounds);

            var feet = camera.WorldToScreenPoint(new Vector3(extent.center.x, extent.min.y, extent.center.z));
            var crown = camera.WorldToScreenPoint(new Vector3(extent.center.x, extent.max.y, extent.center.z));
            if (feet.z <= 0f || crown.z <= 0f || camera.pixelHeight <= 0) return 0f;
            return Mathf.Abs(crown.y - feet.y) / camera.pixelHeight;
        }

        private IEnumerator CaptureFraming(string name, bool settle = true)
        {
            const int width = 1600, height = 900;
            var camera = cameraRig.ViewCamera;
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();

            var texture = new RenderTexture(width, height, 24);
            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                foreach (var canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 1f);
                }
                // Let queued portraits land first: a frame with empty discs where the faces go
                // cannot say what the screen looks like. The studio builds one look at a time, so
                // in a short filtered run the queue can still be working seconds after the panel
                // opened; wait for it, but not forever - a face that never lands is a capture worth
                // having too.
                // Unless the frame is of something that will not wait: a caption the house's
                // world tick takes down within a tenth of a second.
                float until = Time.realtimeSinceStartup + (settle ? 10f : 0f);
                float least = Time.realtimeSinceStartup + (settle ? 0.5f : 0f);
                while (Time.realtimeSinceStartup < least
                    || (Time.realtimeSinceStartup < until && AnyBoundFaceIsStillMissing()))
                    yield return null;
                Canvas.ForceUpdateCanvases();
                // Then lay the HUD out again for the frame being photographed. A batchmode canvas is
                // 4:3 and this frame is 16:9; anchored chrome follows the change by itself, but a
                // panel whose size is computed when the HUD renders - the activity layouts are -
                // kept its 4:3 numbers and photographed distorted, which made the review frames
                // unusable for judging a screen against its mockup.
                RenderHudForTheCurrentCanvas();
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, "..", name + ".png"));
                System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Gamesim] framing capture -> " + path);
                // A capture that asserts nothing is a file somebody has to remember to open. This
                // one at least refuses to pass when the frame did not render: a solid PNG is what a
                // dead camera, a culled canvas or a batchmode ScreenCapture produces, and all three
                // have been mistaken for evidence on this project.
                AssertNotBlank(readback, name);
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                // A canvas can be gone by now: the wait above spans frames, and a panel's fade-out
                // ghost is a canvas that destroys itself when its fade ends.
                foreach (var canvas in overlays)
                    if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Object.Destroy(readback);
                texture.Release();
                Object.Destroy(texture);
            }
            // And back to the layout the rest of the test is measuring.
            Canvas.ForceUpdateCanvases();
            RenderHudForTheCurrentCanvas();
            yield return null;
        }

        private static bool AnyBoundFaceIsStillMissing() =>
            Object.FindObjectsByType<CharacterPortraitBinding>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(binding => binding.GetComponent<RawImage>())
                .Any(face => face != null && face.gameObject.activeInHierarchy && (face.texture == null || !face.enabled));

        /// <summary>Re-renders the HUD against whatever shape its canvas has right now.</summary>
        private void RenderHudForTheCurrentCanvas()
        {
            typeof(EpisodeDirector).GetMethod("Render",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, System.Type.EmptyTypes, null)
                .Invoke(director, null);
        }

        /// <summary>
        /// Fails when a captured frame is one flat colour.
        ///
        /// <para>The weakest possible claim about a picture, and the one that catches the failures
        /// that have actually happened here: a camera with no target, a canvas culled to alpha
        /// zero, and ScreenCapture in batchmode, which returns identical black frames that were
        /// very nearly treated as a look sheet.</para>
        /// </summary>
        private static void AssertNotBlank(Texture2D frame, string name)
        {
            var pixels = frame.GetPixels32();
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < pixels.Length; i += 53)
            {
                seen.Add((pixels[i].r << 16) | (pixels[i].g << 8) | pixels[i].b);
                if (seen.Count > 8) return;
            }
            Assert.Fail("The capture '" + name + "' has only " + seen.Count
                + " sampled colours, so nothing rendered into it.");
        }

        /// <summary>
        /// Fails when one named region of a captured frame is a flat colour.
        ///
        /// <para>For the thing a whole-frame check cannot see: twelve cast portraits rendering as
        /// identical black squares while the rest of the screen was full of content. Give it the
        /// screen rect of the element that is supposed to contain a picture.</para>
        /// </summary>
        private static void AssertRegionHasContent(Texture2D frame, Rect region, string what)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(region.xMin), 0, frame.width - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(region.xMax), 0, frame.width);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(region.yMin), 0, frame.height - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(region.yMax), 0, frame.height);
            Assert.That(x1 - x0, Is.GreaterThan(1), what + " has no width in the frame.");
            Assert.That(y1 - y0, Is.GreaterThan(1), what + " has no height in the frame.");

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int y = y0; y < y1; y += 2)
            for (int x = x0; x < x1; x += 2)
            {
                var pixel = frame.GetPixel(x, y);
                seen.Add((Mathf.RoundToInt(pixel.r * 255) << 16)
                    | (Mathf.RoundToInt(pixel.g * 255) << 8) | Mathf.RoundToInt(pixel.b * 255));
                if (seen.Count > 6) return;
            }
            Assert.Fail(what + " is " + seen.Count + " flat colour(s) in the captured frame, so "
                + "whatever is supposed to be drawn there is not.");
        }

        /// <summary>
        /// No copy is clipped on ANY panel the player can open, at either text size.
        ///
        /// <para>The sweep this project already had opens the notebook and stops. Every other
        /// surface - the conversation, the settings, the house activities, the phase panel - has
        /// been unguarded for its whole life, which is how a four-fact subtitle came to fit at one
        /// text size and be cut off at the other, and how a stakes tag came to overlap itself.</para>
        ///
        /// <para>An opener that cannot run in this fixture is REPORTED, not skipped silently: a
        /// sweep that quietly covers two of five panels reads exactly like one that covers five.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_NoPanelClipsItsCopyAtEitherTextSize()
        {
            var openers = new (string Name, Func<bool> Open)[]
            {
                ("the notebook", () => { director.OpenJournal(); return director.IsPanelOpen; }),
                ("the settings", () => { director.OpenSettings(); return director.IsPanelOpen; }),
                ("house activities", () => { director.OpenHouseActivities(); return director.IsHouseActivityOpen; }),
                ("a conversation", () =>
                {
                    var npc = SceneComponents<HouseNpc>().FirstOrDefault(actor => actor.gameObject.activeInHierarchy);
                    if (npc == null) return false;
                    WarpPlayer(npc.transform.position
                        + (player.transform.position - npc.transform.position).normalized * 1.4f);
                    return director.TryOpenNpc(npc.Id);
                }),
            };

            var unreached = new List<string>();
            foreach (bool larger in new[] { false, true })
            {
                yield return ApplyTextSize(larger);
                foreach (var opener in openers)
                {
                    director.ClosePanels();
                    yield return null;
                    if (!opener.Open()) { unreached.Add(opener.Name + " at " + (larger ? "larger" : "standard") + " text"); continue; }

                    // Let asynchronous content land before measuring. This is not politeness: a
                    // panel whose picture had not arrived used to draw a DIFFERENT layout -
                    // SpeakerTitle fell back to a wrapping PanelTitle when the portrait was null, and
                    // a wrapping label cannot clip. A mutation that re-crammed the conversation
                    // subtitle into one fixed-width line survived this sweep for exactly that reason.
                    // The header now binds its face instead, but a panel measured before its content
                    // lands is still a panel measured in a state nobody sees for long.
                    float settle = Time.realtimeSinceStartup + 2f;
                    while (Time.realtimeSinceStartup < settle) yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    var clipped = director.GetComponentsInChildren<TMP_Text>(true)
                        .Where(label => label.gameObject.activeInHierarchy && !string.IsNullOrEmpty(label.text))
                        .Where(label => { label.ForceMeshUpdate(); return label.isTextOverflowing; })
                        .Select(label => "'" + Excerpt(label.text) + "'")
                        .ToArray();
                    Assert.That(clipped, Is.Empty,
                        "On " + opener.Name + " at " + (larger ? "larger" : "standard")
                        + " text, this copy is cut off: " + string.Join(" | ", clipped));
                    director.ClosePanels();
                    yield return null;
                }
            }

            Assert.That(unreached, Is.Empty,
                "These panels never opened, so this sweep says nothing about them: "
                + string.Join(", ", unreached) + ". A sweep that silently covers some of the "
                + "surfaces reads exactly like one that covers all of them.");
            yield return ApplyTextSize(false);
        }

        /// <summary>
        /// Waits until no houseguest is still showing a stand-in, or gives up after a bounded number
        /// of frames. On the authored-prefab cast there are no stand-ins and this returns at once.
        /// </summary>
        private IEnumerator SettleCast()
        {
            const int limit = 900;
            for (int frame = 0; frame < limit; frame++)
            {
                bool waiting = director.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Any(node => node.name == "Stand-in");
                if (!waiting) break;
                yield return null;
            }
            for (int i = 0; i < 5; i++) yield return null;
        }

        private IEnumerator ApplyTextSize(bool larger)
        {
            director.OpenSettings();
            yield return null; yield return null;
            var wanted = larger ? LargerTextCaption : StandardTextCaption;
            var toggle = director.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.IsActive() && button.name == wanted);
            // Already in the requested state when the opposite caption is showing.
            if (toggle != null) { toggle.onClick.Invoke(); yield return null; yield return null; }
            director.ClosePanels();
            yield return null;
        }

        private IEnumerator OpenNotebook()
        {
            ButtonWithCaption("Notebook [J]").onClick.Invoke();
            yield return null; yield return null;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            // A screen-space-overlay canvas puts world corners straight into screen pixels.
            return new Rect(corners[0].x, corners[0].y,
                corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        private static string Excerpt(string value) =>
            value.Length <= 40 ? value : value.Substring(0, 40) + "…";

        // Not "Path": this is a partial of a class that uses System.IO.Path throughout.
        private static string HierarchyPath(Transform node)
        {
            var name = node.name;
            for (var parent = node.parent; parent != null; parent = parent.parent) name = parent.name + "/" + name;
            return name;
        }
    }
}

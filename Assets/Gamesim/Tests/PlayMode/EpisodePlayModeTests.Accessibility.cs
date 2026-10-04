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

                // Exercise the real Update -> Render branch deterministically, including on the
                // primitive rig, where no body ever completes. Only the director's observed counter
                // is stale; the global completion counter and cast remain untouched.
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
        /// drawing the HUD over it through the capture's lens (<see cref="CaptureLens"/>) produces a
        /// real frame.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Accessibility_CapturesTheHudOverTheSetForReview()
        {
            if (!Application.isBatchMode) yield break;

            // Let the cast settle first. A provided body is assembled over frames and is not drawn
            // until it is, so capturing immediately photographs an empty house, which would make the
            // review frames quietly misleading about what the game looks like.
            yield return SettleCast();

            var canvas = director.GetComponentsInChildren<Canvas>(true)
                .FirstOrDefault(c => c.renderMode == RenderMode.ScreenSpaceOverlay && c.isActiveAndEnabled);
            Assert.That(canvas, Is.Not.Null, "The HUD canvas should be a screen-space overlay.");

            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(2560, 1440) })
            {
                var lens = new CaptureLens(cameraRig.ViewCamera, size.x, size.y);
                Texture2D readback = null;
                try
                {
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    yield return lens.MakeSureTheCanvasesAreDrawn();
                    readback = lens.Read();

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
                    lens.Dispose();
                    if (readback != null) Object.Destroy(readback);
                }
            }
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay), "The HUD is an overlay again after its captures.");
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

        /// <summary>
        /// Photographs what the player would see - the set through the view camera, and every
        /// overlay canvas drawn over it as the screen draws them (<see cref="CaptureLens"/>) - into
        /// <c>{name}.png</c> beside the project, and fails a frame that did not render.
        ///
        /// <para><paramref name="inspect"/>, when given, is handed the frame before it is thrown
        /// away, while the lens is still up: a <see cref="RectTransform"/>'s world corners projected
        /// through its canvas's camera (<see cref="LensOf"/>) are then its pixels in the frame,
        /// which is how a caller asks whether a portrait or a card actually drew where it stands
        /// (<see cref="AssertRegionHasContent"/>). A whole-frame check cannot tell a face from the
        /// empty disc it lands in.</para>
        ///
        /// <para><paramref name="arrange"/>, when given, runs once the HUD has been laid out again for
        /// the frame, with a frame after it: the render rebuilds the panels, so a scroll the caller
        /// set before the capture is set again here.</para>
        ///
        /// <para><paramref name="panel"/>, when given, finds the panel the frame was taken for, and
        /// the capture proves it is in the frame: its rect is not one flat colour, and with its ground
        /// painted the probe's magenta a frame before the frame is drawn again - a colour set and
        /// drawn in one frame is not yet the canvas's - the paint shows in every ninth of it
        /// (<see cref="AssertTheGroundShowsEverywhere"/>). The panel is found again after that
        /// frame, and a new copy painted too: the HUD can render in between.</para>
        ///
        /// <para>The frame is 1600 by 900 unless <paramref name="width"/> and <paramref name="height"/>
        /// say otherwise: 1200 by 900 photographs a 4:3 layout as the batch canvas laid it out.</para>
        /// </summary>
        private IEnumerator CaptureFraming(string name, bool settle = true, Action<Texture2D> inspect = null, int width = 1600, int height = 900,
            (Func<RectTransform> Find, string What)? panel = null, Action arrange = null, Action<string> observe = null,
            Func<IEnumerator> prepare = null)
        {
            observe?.Invoke("capture entered");
            var lens = new CaptureLens(cameraRig.ViewCamera, width, height);
            Texture2D readback = null, probe = null;
            var painted = new List<(Image Ground, Color Was)>();
            try
            {
                observe?.Invoke("capture lens constructed");
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
                observe?.Invoke("capture before layout frame");
                yield return null;
                observe?.Invoke("capture after layout frame");
                if (arrange != null)
                {
                    arrange();
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }
                observe?.Invoke("capture before guard preparation");
                yield return lens.MakeSureTheCanvasesAreDrawn();
                observe?.Invoke("capture after guard preparation");
                // Creator previews may rebuild after the lens changes their layout. Their optional
                // preparation runs against this final canvas, before reading its actual frame.
                // Every existing caller keeps its previous timing when no preparation is supplied.
                if (prepare != null)
                {
                    yield return prepare();
                    Canvas.ForceUpdateCanvases();
                }
                observe?.Invoke("capture immediately before pixel read");
                readback = lens.Read();
                observe?.Invoke("capture immediately after pixel read");
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, "..", name + ".png"));
                System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Gamesim] framing capture -> " + path);
                // A capture that asserts nothing is a file somebody has to remember to open. This
                // one at least refuses to pass when the frame did not render: a solid PNG is what a
                // dead camera, a culled canvas or a batchmode ScreenCapture produces, and all three
                // have been mistaken for evidence on this project.
                AssertNotBlank(readback, name);
                // Before the finally: the frame is destroyed there and the canvases go back to overlays.
                inspect?.Invoke(readback);

                if (panel.HasValue)
                {
                    string what = panel.Value.What + " in '" + name + "'";
                    var found = panel.Value.Find();
                    AssertRegionHasContent(readback, PanelInterior(found, readback, what), what);
                    PaintTheGround(found, painted, what);
                    for (int wait = 0; wait < 5; wait++)
                    {
                        yield return null;
                        var now = panel.Value.Find();
                        if (now == found) break;
                        // The HUD rendered again - a body finishing its assembly rebuilds it - and the
                        // panel in the frame is a new copy: painted too, and given its frame.
                        found = now;
                        PaintTheGround(found, painted, what);
                    }
                    probe = lens.Read();
                    AssertTheGroundShowsEverywhere(readback, probe, PanelInterior(found, readback, what), what);
                }
            }
            finally
            {
                foreach (var (ground, was) in painted)
                    if (ground != null) ground.color = was;
                lens.Dispose();
                if (readback != null) Object.Destroy(readback);
                if (probe != null) Object.Destroy(probe);
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
                // Refinement Kit 6's pages each lay their own copy out, so each is swept.
                ("who is where", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Rooms); return director.IsPanelOpen; }),
                ("the houseguests", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.People); return director.IsPanelOpen; }),
                ("a houseguest profile", () =>
                {
                    var someone = director.Snapshot.contestants.FirstOrDefault(c => !c.isPlayer);
                    if (someone == null) return false;
                    director.ShowHouseguestProfile(someone.id);
                    return director.ProfileId == someone.id;
                }),
                ("the vote", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Votes); return director.IsPanelOpen; }),
                ("the alliances", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Alliances); return director.IsPanelOpen; }),
                ("your word", () => { director.ShowNotebookSection(EpisodeDirector.NotebookSection.Word); return director.IsPanelOpen; }),
                // The episode screen, fitted to what it holds with its way on pinned.
                ("the episode screen", () => { WarpPlayer(director.StationPosition); return director.TryOpenPhasePanel(); }),
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
        /// Waits until no houseguest in the house is still being assembled, or gives up after a
        /// bounded number of frames. A body in assembly is not drawn, and the one that arrives
        /// triggers a HUD render. Only bodies in the house count: one on an inactive actor never
        /// finishes. On the primitive rig nobody assembles and this returns at once.
        /// </summary>
        private IEnumerator SettleCast()
        {
            const int limit = 900;
            for (int frame = 0; frame < limit; frame++)
            {
                bool waiting = SceneComponents<CharacterPresentation>()
                    .Any(presentation => presentation.gameObject.activeInHierarchy && presentation.IsBodyAssembling);
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

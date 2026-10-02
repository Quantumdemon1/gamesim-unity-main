using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The lens every capture draws its frame through (UI-UX-PASS-PLAN Z0), and the proof that a
    /// panel is in the frame it was photographed for.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// Draws the frame a capture photographs: the set as the view camera draws it, into a target
        /// of the frame's size, its post-processing and all, then every overlay canvas over the
        /// finished picture by a second camera in the view camera's URP stack - as the screen draws
        /// them: over everything, and after the tonemapping, the bloom and the depth of field
        /// (option B).
        ///
        /// <para>The overlays used to be camera canvases on the view camera itself, a metre in front
        /// of the lens. A camera canvas is drawn in the scene, so anything nearer than its plane drew
        /// over the HUD - the six-house conversation's close-up stands against a prop, and the three
        /// conversation frames showed the prop where the panel was (the play sweep's row 3) - and the
        /// HUD went through the camera's grading and its depth of field with the set, which on screen
        /// it never does.</para>
        ///
        /// <para>The overlay camera is orthographic and stands a hundred kilometres down the z axis,
        /// where nothing of the house is, drawing only the canvases put on it, over a cleared depth:
        /// nothing of the set can come between it and them. Its own target is the frame, so a canvas
        /// on it is laid out for the frame's pixels and measures in them. With <c>pixelAligned</c>
        /// its view is those pixels: a canvas's world corners are its pixels, as an overlay's are, so
        /// what reads a canvas as an overlay reads it right in a capture too, and what asks the
        /// canvas's camera gets the same numbers. Otherwise it stands two units a pixel, far from the
        /// origin, so a test can show that what it measures goes through the canvas's own camera (the
        /// chrome gate's lens test).</para>
        ///
        /// <para>The engine gives UI geometry only to the cameras it is asked to render, and a
        /// capture asks for the view camera alone: URP draws the stacked overlay camera with nothing
        /// of the canvases unless they are emitted for it, which the lens does while it draws (an
        /// ordinary frame is drawn with every camera, and has them already). A guard - a green patch
        /// at the frame's centre on a canvas of the lens's own, on layer 31 - is drawn before every
        /// frame is read. If it is not there through the overlay camera
        /// (<see cref="MakeSureTheCanvasesAreDrawn"/>), the lens puts the canvases on the view camera
        /// just past its near plane for the rest of the run (option A: the HUD graded and under the
        /// depth of field with the set, as before Z0), gives them a frame there, and fails only if
        /// the guard is not there either. A pipeline whose renderer cannot stack an overlay camera
        /// gets option A from the start.</para>
        /// </summary>
        private sealed class CaptureLens : IDisposable
        {
            /// <summary>The overlay camera's name, so a test can tell it from the house's.</summary>
            public const string OverlayCameraName = "Capture overlay lens";

            /// <summary>The guard's canvas.</summary>
            public const string GuardName = "Capture lens guard";

            /// <summary>The guard's layer: unnamed in this project, and inside the view camera's mask.</summary>
            private const int GuardLayer = 31;
            private const int GuardBit = 1 << GuardLayer;

            /// <summary>The guard's patch, in pixels: the guard canvas has no scaler, so a unit is a pixel.</summary>
            private const float GuardSide = 48f;

            /// <summary>Where along z the overlay camera's canvases stand: a hundred kilometres from the house.</summary>
            private const float Away = 100000f;

            /// <summary>How far in front of the overlay camera its canvases stand, and the depth either side of them it draws.</summary>
            private const float PlaneDistance = 10f, Depth = 1f;

            /// <summary>Past the view camera's near plane, for the canvases under option A.</summary>
            private const float PastNearClip = .01f;

            /// <summary>Whether this run has seen a manual render leave the overlay camera's canvases out: from then on every lens is the view camera's.</summary>
            private static bool overlayUnavailable;

            /// <summary>Whether this run has said so.</summary>
            private static bool fallbackSaid;

            /// <summary>A run starts on option B again: the static outlives play sessions in an editor that does not reload the domain.</summary>
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ForgetTheLastRun()
            {
                overlayUnavailable = false;
                fallbackSaid = false;
            }

            /// <summary>
            /// Whether a lens opened now on <paramref name="view"/> draws the overlays through the
            /// overlay camera, as far as this run has seen: its renderer stacks an overlay camera, and
            /// no guard has gone missing through one.
            /// </summary>
            public static bool DrawsThroughTheOverlay(Camera view) => !overlayUnavailable && CanStack(view);

            /// <summary>
            /// Whether the view camera's renderer can stack an overlay camera. Asked before the stack:
            /// without URP's asset the stack's getter throws rather than answering, and asking for the
            /// camera's URP data adds it, so there is always some to ask.
            /// </summary>
            private static bool CanStack(Camera view)
            {
                var data = view.GetUniversalAdditionalCameraData();
                return data.renderType == CameraRenderType.Base
                    && data.scriptableRenderer?.SupportsCameraStackingType(CameraRenderType.Overlay) == true;
            }

            public readonly Camera View;
            /// <summary>The camera the overlays are drawn through, or null when they are on the view camera (option A).</summary>
            public Camera Overlay { get; private set; }
            public readonly RenderTexture Target;
            public readonly int Width, Height;

            /// <summary>The camera the canvases are on: the overlay camera, or the view camera.</summary>
            public Camera Lens => Overlay != null ? Overlay : View;

            /// <summary>How far in front of <see cref="Lens"/> the canvases stand.</summary>
            private float Plane => Overlay != null ? PlaneDistance : View.nearClipPlane + PastNearClip;

            private readonly RenderTexture previousTarget;
            private List<Camera> stack;
            private Canvas guard;
            private readonly List<(Canvas Canvas, RenderMode Mode, Camera Camera, float Distance)> canvases =
                new List<(Canvas Canvas, RenderMode Mode, Camera Camera, float Distance)>();

            public CaptureLens(Camera view, int width, int height, bool pixelAligned = true)
            {
                View = view;
                Width = width;
                Height = height;
                var overlays = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
                Target = new RenderTexture(width, height, 24);
                previousTarget = view.targetTexture;
                view.targetTexture = Target;
                if (DrawsThroughTheOverlay(view)) Overlay = StackOverlay(view, Target, width, height, pixelAligned, out stack);
                foreach (var canvas in overlays)
                    canvases.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                // Made with the lens, so the canvas system has laid it out by the first read.
                guard = MakeGuard();
                PointTheCanvases();
            }

            /// <summary>The overlay camera, made and put in the view camera's stack.</summary>
            private static Camera StackOverlay(Camera view, RenderTexture target, int width, int height, bool pixelAligned, out List<Camera> stack)
            {
                stack = view.GetUniversalAdditionalCameraData().cameraStack;
                var holder = new GameObject(OverlayCameraName);
                var camera = holder.AddComponent<Camera>();
                // A world unit a pixel, its view the frame's own pixels; or two units a pixel, far
                // down the x axis, where no overlay's corners would ever be.
                float unit = pixelAligned ? 1f : 2f;
                var origin = pixelAligned ? Vector2.zero : new Vector2(-50000f, 30000f);
                holder.transform.SetPositionAndRotation(
                    new Vector3(origin.x + width * .5f * unit, origin.y + height * .5f * unit, Away - PlaneDistance), Quaternion.identity);
                camera.orthographic = true;
                camera.orthographicSize = height * .5f * unit;
                camera.nearClipPlane = PlaneDistance - Depth;
                camera.farClipPlane = PlaneDistance + Depth;
                camera.clearFlags = CameraClearFlags.Nothing;
                camera.cullingMask = ~0;
                camera.useOcclusionCulling = false;
                camera.allowMSAA = false;
                camera.depth = view.depth + 1f;
                // Its own target gives it the frame's pixels, which a canvas on it is laid out for and
                // measures in; what it draws goes into the view camera's, as an overlay camera's does.
                camera.targetTexture = target;

                var data = camera.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Overlay;
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                stack.Add(camera);
                return camera;
            }

            /// <summary>Puts every canvas the lens holds, and its guard, on <see cref="Lens"/> at <see cref="Plane"/>.</summary>
            private void PointTheCanvases()
            {
                var lens = Lens;
                float plane = Plane;
                foreach (var (canvas, _, _, _) in canvases) Point(canvas, lens, plane);
                Point(guard, lens, plane);
            }

            private static void Point(Canvas canvas, Camera lens, float plane)
            {
                if (canvas == null) return;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = lens;
                canvas.planeDistance = plane;
            }

            /// <summary>
            /// Draws the frame now - the set, then every overlay over it - and reads it back, the
            /// guard drawn first and read at the frame's centre: a frame its canvases did not reach
            /// fails here rather than being photographed. Call
            /// <see cref="MakeSureTheCanvasesAreDrawn"/> a frame or more before the first read. The
            /// caller destroys what it is handed.
            /// </summary>
            public Texture2D Read()
            {
                if (!GuardShows(out var seen))
                    Assert.Fail("The capture lens drew none of its canvases: through the " + (Overlay != null ? "overlay" : "view")
                        + " camera the guard's patch at the frame's centre reads " + seen + ", not green.");
                return Draw(false);
            }

            /// <summary>
            /// The guard, drawn at the frame's centre through the lens and read back: through the
            /// overlay camera first, then - if it is missing there - through the view camera for the
            /// rest of the run, said once, after a frame for the canvases to stand there (a canvas
            /// moved and drawn in one frame is not yet drawn where it was moved to). Fails when it is
            /// missing through both.
            /// </summary>
            public System.Collections.IEnumerator MakeSureTheCanvasesAreDrawn()
            {
                if (GuardShows(out var seen)) yield break;
                if (Overlay == null)
                    Assert.Fail("The capture lens drew none of its canvases: through the view camera the guard's patch at the frame's centre reads "
                        + seen + ", not green.");
                overlayUnavailable = true;
                if (!fallbackSaid)
                {
                    fallbackSaid = true;
                    Debug.Log("[Gamesim] capture lens: overlay emission unavailable, canvases on the view camera");
                }
                PutTheCanvasesOnTheView();
                yield return null;
                if (!GuardShows(out var again))
                    Assert.Fail("The capture lens drew none of its canvases: the guard's patch at the frame's centre reads "
                        + seen + " through the overlay camera and " + again + " through the view camera, not green.");
            }

            /// <summary>Whether the guard's green is at the frame's centre, drawn with its layer in the lens's mask and no post-processing.</summary>
            private bool GuardShows(out Color centre)
            {
                var frame = Draw(true);
                try
                {
                    centre = frame.GetPixel(Width / 2, Height / 2);
                    return centre.g > .9f && centre.r < .1f && centre.b < .1f;
                }
                finally
                {
                    Object.Destroy(frame);
                }
            }

            /// <summary>
            /// Draws the frame through the view camera and reads it back: with the guard's layer in the
            /// lens's mask and the view's post-processing off for <paramref name="withGuard"/>, without
            /// the guard and with the post-processing as it was otherwise. Through the overlay camera
            /// the canvases are emitted for it as it is drawn - the engine only emits them for the
            /// view camera this render was asked of - and only while this render is drawn: an
            /// ordinary frame has them already. The mask, the post-processing, the subscription and
            /// the active target are all put back.
            /// </summary>
            private Texture2D Draw(bool withGuard)
            {
                var eye = Lens;
                var viewData = View.GetUniversalAdditionalCameraData();
                int mask = eye.cullingMask;
                bool post = viewData.renderPostProcessing;
                bool emit = Overlay != null;
                var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var active = RenderTexture.active;
                bool read = false;
                try
                {
                    eye.cullingMask = withGuard ? mask | GuardBit : mask & ~GuardBit;
                    if (withGuard) viewData.renderPostProcessing = false;
                    if (emit) UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += EmitTheOverlayCanvases;
                    Canvas.ForceUpdateCanvases();
                    View.Render();
                    RenderTexture.active = Target;
                    frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    frame.Apply();
                    read = true;
                    return frame;
                }
                finally
                {
                    if (emit) UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= EmitTheOverlayCanvases;
                    eye.cullingMask = mask;
                    viewData.renderPostProcessing = post;
                    // A runtime ReadPixels leaves the target active; the screen's own is put back.
                    RenderTexture.active = active;
                    if (!read) Object.Destroy(frame);
                }
            }

            /// <summary>Raised as each camera of the render begins, before its cull: the overlay camera's canvases are emitted for it.</summary>
            private void EmitTheOverlayCanvases(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
            {
                if (Overlay != null && camera == Overlay) UnityEngine.Rendering.ScriptableRenderContext.EmitGeometryForCamera(camera);
            }

            /// <summary>Option A for the rest of the run: the overlay camera out of the stack and gone, every canvas on the view camera just past its near plane.</summary>
            private void PutTheCanvasesOnTheView()
            {
                var overlay = Overlay;
                Overlay = null;
                if (stack != null && overlay != null) stack.Remove(overlay);
                stack = null;
                if (overlay != null)
                {
                    overlay.targetTexture = null;
                    overlay.enabled = false;
                    Object.Destroy(overlay.gameObject);
                }
                PointTheCanvases();
            }

            /// <summary>The guard: a canvas of the lens's own on layer 31, over every other, with a green patch at its centre.</summary>
            private static Canvas MakeGuard()
            {
                var holder = new GameObject(GuardName, typeof(RectTransform), typeof(Canvas)) { layer = GuardLayer };
                var canvas = holder.GetComponent<Canvas>();
                canvas.sortingOrder = short.MaxValue;
                var patch = new GameObject("Guard patch", typeof(RectTransform), typeof(Image)) { layer = GuardLayer };
                var rect = (RectTransform)patch.transform;
                rect.SetParent(holder.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(GuardSide, GuardSide);
                var image = patch.GetComponent<Image>();
                image.color = Color.green;
                image.raycastTarget = false;
                return canvas;
            }

            /// <summary>Puts every canvas back as it was, takes the overlay camera out of the stack, hands the view camera its own target again, and takes the guard down.</summary>
            public void Dispose()
            {
                // A canvas can be gone by now: a capture's wait spans frames, and a panel's fade-out
                // ghost is a canvas that destroys itself when its fade ends.
                foreach (var (canvas, mode, camera, distance) in canvases)
                {
                    if (canvas == null) continue;
                    canvas.renderMode = mode;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = distance;
                }
                canvases.Clear();
                if (guard != null)
                {
                    // Down now, gone at the frame's end: a lens opened before then must not draw it.
                    guard.gameObject.SetActive(false);
                    Object.Destroy(guard.gameObject);
                }
                guard = null;
                if (stack != null && Overlay != null) stack.Remove(Overlay);
                stack = null;
                if (View != null) View.targetTexture = previousTarget;
                if (Overlay != null)
                {
                    Overlay.targetTexture = null;
                    Overlay.enabled = false;
                    Object.Destroy(Overlay.gameObject);
                }
                Overlay = null;
                if (Target != null)
                {
                    Target.Release();
                    Object.Destroy(Target);
                }
            }
        }

        /// <summary>
        /// What a panel's ground is painted for the probe: a magenta under the bloom's threshold, so
        /// the paint stays where it is painted under option A's grading too.
        /// </summary>
        private static readonly Color PanelProbeColour = new Color(.75f, 0f, .75f, 1f);

        /// <summary>Whether a pixel went toward the probe's magenta between the unpainted frame and the painted one.</summary>
        private static bool Painted(Color was, Color now) => now.r - was.r > .2f && now.b - was.b > .2f;

        /// <summary>A panel's ground, painted the probe's colour, with the colour it had remembered for putting back.</summary>
        private static void PaintTheGround(RectTransform panel, List<(Image Ground, Color Was)> painted, string what)
        {
            Assert.That(panel, Is.Not.Null, what + ": there is no panel in the frame.");
            var ground = panel.GetComponent<Image>();
            Assert.That(ground != null && ground.enabled && ground.color.a > .5f, Is.True, what + ": the panel has its ground under its words.");
            painted.Add((ground, ground.color));
            ground.color = PanelProbeColour;
        }

        /// <summary>The inside of a panel in the frame, through the lens its canvas is drawn with: inside its edge and its rounded corners, and inside the frame.</summary>
        private Rect PanelInterior(RectTransform panel, Texture2D frame, string what)
        {
            Assert.That(panel, Is.Not.Null, what + ": there is no panel in the frame.");
            var box = ScreenBox(panel);
            float inset = Mathf.Max(6f, Mathf.Min(box.width, box.height) * .04f);
            var inner = Rect.MinMaxRect(Mathf.Max(0f, box.xMin + inset), Mathf.Max(0f, box.yMin + inset),
                Mathf.Min(frame.width, box.xMax - inset), Mathf.Min(frame.height, box.yMax - inset));
            Assert.That(inner.width > 40f && inner.height > 40f, Is.True, what + ": the panel stands in the " + frame.width + "x" + frame.height + " frame, at " + box + ".");
            return inner;
        }

        /// <summary>
        /// Fails unless the panel's ground showed through every ninth of <paramref name="inner"/>:
        /// between the frame as photographed and the frame drawn again with the ground painted, a
        /// hundredth of each ninth's places went toward the probe's magenta - between the panel's
        /// rows, around its words, along its margins. Whatever stands nearer the lens than the HUD
        /// keeps its own colour where it stands, and a frame the HUD never reached changes nowhere.
        /// </summary>
        private static void AssertTheGroundShowsEverywhere(Texture2D unpainted, Texture2D painted, Rect inner, string what)
        {
            const int across = 3, down = 3, step = 3;
            var hidden = new List<string>();
            int shown = 0, sampled = 0;
            for (int row = 0; row < down; row++)
            for (int column = 0; column < across; column++)
            {
                int x0 = Mathf.CeilToInt(inner.xMin + inner.width * column / across), x1 = Mathf.FloorToInt(inner.xMin + inner.width * (column + 1) / across);
                int y0 = Mathf.CeilToInt(inner.yMin + inner.height * row / down), y1 = Mathf.FloorToInt(inner.yMin + inner.height * (row + 1) / down);
                int tileShown = 0, tileSampled = 0;
                for (int y = y0; y < y1; y += step)
                for (int x = x0; x < x1; x += step)
                {
                    tileSampled++;
                    if (Painted(unpainted.GetPixel(x, y), painted.GetPixel(x, y))) tileShown++;
                }
                sampled += tileSampled;
                shown += tileShown;
                // A hundredth of a ninth: the gaps between a column's rows are a tenth of it.
                if (tileShown < Mathf.Max(1, tileSampled / 100)) hidden.Add("(" + column + ", " + row + ")");
            }
            Assert.That(shown, Is.GreaterThan(0),
                what + ": the paint never reached the frame - painted, the panel's ground changed none of the " + sampled + " places of " + inner + ".");
            Assert.That(hidden, Is.Empty,
                what + ": painted, the panel's ground shows in " + shown + " of " + sampled + " places of " + inner
                + " and in no place of the " + string.Join(", ", hidden) + " ninth(s) of it (column, row from the bottom left): "
                + "whatever the frame shows there stands in front of the HUD.");
        }
    }
}

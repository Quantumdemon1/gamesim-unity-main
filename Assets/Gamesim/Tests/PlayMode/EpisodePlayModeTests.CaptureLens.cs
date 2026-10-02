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
        /// <summary>The lens of the capture being taken, for an inspection that draws the frame again; null outside one.</summary>
        private CaptureLens captureLens;

        /// <summary>
        /// Draws the frame a capture photographs: the set as the view camera draws it, into a target
        /// of the frame's size, its post-processing and all, then every overlay canvas over the
        /// finished picture by a second camera in the view camera's URP stack - as the screen draws
        /// them: over everything, and after the tonemapping, the bloom and the depth of field.
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
        /// on it is laid out for the frame's pixels and measures in them. With
        /// <c>pixelAligned</c> its view is those pixels: a canvas's world corners are its
        /// pixels, as an overlay's are, so what reads a canvas as an overlay reads it right in a
        /// capture too, and what asks the canvas's camera gets the same numbers. Otherwise it stands
        /// two units a pixel, far from the origin, so a test can show that what it measures goes
        /// through the canvas's own camera (the chrome gate's lens test).</para>
        ///
        /// <para>Without a stack to stand in - a pipeline other than URP's - the canvases go on the
        /// view camera just past its near plane, before anything of the set can stand.</para>
        /// </summary>
        private sealed class CaptureLens : IDisposable
        {
            /// <summary>The overlay camera's name, so a test can tell it from the house's.</summary>
            public const string OverlayCameraName = "Capture overlay lens";

            /// <summary>Where along z the overlay camera's canvases stand: a hundred kilometres from the house.</summary>
            private const float Away = 100000f;

            /// <summary>How far in front of the overlay camera its canvases stand, and the depth either side of them it draws.</summary>
            private const float PlaneDistance = 10f, Depth = 1f;

            /// <summary>Past the view camera's near plane, for the canvases when there is no stack.</summary>
            private const float PastNearClip = .01f;

            public readonly Camera View;
            /// <summary>The camera the overlays are drawn through, or null when they are on the view camera.</summary>
            public readonly Camera Overlay;
            public readonly RenderTexture Target;
            public readonly int Width, Height;

            private readonly RenderTexture previousTarget;
            private readonly List<Camera> stack;
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
                Overlay = StackOverlay(view, Target, width, height, pixelAligned, out stack);
                var lens = Overlay != null ? Overlay : view;
                float plane = Overlay != null ? PlaneDistance : view.nearClipPlane + PastNearClip;
                foreach (var canvas in overlays)
                {
                    canvases.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = lens;
                    canvas.planeDistance = plane;
                }
            }

            /// <summary>The overlay camera, made and put in the view camera's stack; null, with no stack, when the pipeline has none.</summary>
            private static Camera StackOverlay(Camera view, RenderTexture target, int width, int height, bool pixelAligned, out List<Camera> stack)
            {
                stack = null;
                var viewData = view.GetUniversalAdditionalCameraData();
                if (viewData == null || viewData.renderType != CameraRenderType.Base) return null;
                var cameras = viewData.cameraStack;
                if (cameras == null) return null;

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
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                cameras.Add(camera);
                stack = cameras;
                return camera;
            }

            /// <summary>
            /// Draws the frame now - the set, then every overlay over it - and reads it back. The
            /// caller destroys what it is handed.
            /// </summary>
            public Texture2D Read()
            {
                var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                var active = RenderTexture.active;
                bool read = false;
                try
                {
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
                    // A runtime ReadPixels leaves the target active; the screen's own is put back.
                    RenderTexture.active = active;
                    if (!read) Object.Destroy(frame);
                }
            }

            /// <summary>Puts every canvas back as it was, takes the overlay camera out of the stack, and hands the view camera its own target again.</summary>
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
                if (stack != null && Overlay != null) stack.Remove(Overlay);
                if (View != null) View.targetTexture = previousTarget;
                if (Overlay != null)
                {
                    Overlay.targetTexture = null;
                    Overlay.enabled = false;
                    Object.Destroy(Overlay.gameObject);
                }
                if (Target != null)
                {
                    Target.Release();
                    Object.Destroy(Target);
                }
            }
        }

        /// <summary>What a panel's ground is painted for the probe: opaque magenta, which nothing on the HUD wears.</summary>
        private static readonly Color PanelProbeColour = new Color(1f, 0f, 1f, 1f);

        /// <summary>Whether a captured pixel is the probe's magenta.</summary>
        private static bool IsPanelProbe(Color pixel) => pixel.r > .8f && pixel.b > .8f && pixel.g < .2f;

        /// <summary>
        /// Fails unless <paramref name="panel"/> is in the captured frame where it stands, over the
        /// house: its rect through the capture's lens is not one flat colour, and, with its ground
        /// painted the probe's magenta and the frame drawn again, the magenta shows in every ninth of
        /// it - between its rows, around its words, along its margins. Whatever stands nearer the
        /// lens than the HUD hides the probe where it stands, and a frame the HUD never drew in has
        /// no probe at all. The ground's colour is put back before this returns.
        ///
        /// <para>Only inside a capture's inspection (<see cref="CaptureFraming"/>), which keeps its
        /// lens up for it; the panel is looked up by the caller then, since the capture renders the
        /// HUD again for its frame and a panel found before it is the copy on its way out.</para>
        /// </summary>
        private void AssertThePanelIsInTheFrame(Texture2D frame, RectTransform panel, string what)
        {
            Assert.That(captureLens, Is.Not.Null, what + ": only a capture's inspection can draw its frame again.");
            Assert.That(panel, Is.Not.Null, what + ": there is no panel in the frame.");
            var ground = panel.GetComponent<Image>();
            Assert.That(ground != null && ground.enabled && ground.color.a > .5f, Is.True, what + ": the panel has its ground under its words.");

            var box = ScreenBox(panel);
            // Inside its edge and its rounded corners, and inside the frame.
            float inset = Mathf.Max(6f, Mathf.Min(box.width, box.height) * .04f);
            var inner = Rect.MinMaxRect(Mathf.Max(0f, box.xMin + inset), Mathf.Max(0f, box.yMin + inset),
                Mathf.Min(frame.width, box.xMax - inset), Mathf.Min(frame.height, box.yMax - inset));
            Assert.That(inner.width > 40f && inner.height > 40f, Is.True, what + ": the panel stands in the " + frame.width + "x" + frame.height + " frame, at " + box + ".");
            AssertRegionHasContent(frame, inner, what);

            var was = ground.color;
            Texture2D probe = null;
            try
            {
                ground.color = PanelProbeColour;
                probe = captureLens.Read();
            }
            finally
            {
                ground.color = was;
                Canvas.ForceUpdateCanvases();
            }

            try
            {
                const int across = 3, down = 3, step = 3;
                var hidden = new List<string>();
                int shown = 0, sampled = 0, already = 0;
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
                        if (IsPanelProbe(probe.GetPixel(x, y))) tileShown++;
                        if (IsPanelProbe(frame.GetPixel(x, y))) already++;
                    }
                    sampled += tileSampled;
                    shown += tileShown;
                    // A hundredth of a ninth: the gaps between a column's rows are a tenth of it.
                    if (tileShown < Mathf.Max(1, tileSampled / 100)) hidden.Add("(" + column + ", " + row + ")");
                }
                Assert.That(already, Is.LessThan(Mathf.Max(1, sampled / 200)),
                    what + ": the frame already reads as the probe's magenta in " + already + " of " + sampled + " places, so the probe proves nothing there.");
                Assert.That(hidden, Is.Empty,
                    what + ": painted, the panel's ground shows in " + shown + " of " + sampled + " places of " + inner
                    + " and in no place of the " + string.Join(", ", hidden) + " ninth(s) of it (column, row from the bottom left): "
                    + "whatever the frame shows there stands in front of the HUD.");
            }
            finally
            {
                Object.Destroy(probe);
            }
        }
    }
}

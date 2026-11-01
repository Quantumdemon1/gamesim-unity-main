using System;
using System.Collections;
using System.Collections.Generic;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The room scope's lane pins (ROOM-SCOPE-PLAN §5.2 RS0) on the plain SRP path: the lane's copies
    /// run without the GPU Resident Drawer, so these say what URP 17.6 does on its own, and the
    /// player probe (P1 to P7) says what the shipping drawer does. Each test frames a patch of sky
    /// over the yard - above every wall, prop and person, the camera looking straight down at a
    /// backdrop of its own - and reads the main camera's own frame by a manual render.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private const int ScopeProbeSide = 96;
        /// <summary>Where the pins draw: 5.5 m over the yard's middle, the eye 3.5 m above that, clear of the light towers and everything else.</summary>
        private static readonly Vector3 ScopeProbeSpot = new Vector3(0f, 5.5f, 15f);
        private const float ScopeProbeDrawn = .1f, ScopeProbeSame = .02f;

        /// <summary>
        /// A renderer on a layer the main camera's culling mask leaves out is not drawn, and is drawn
        /// again once it is back: the room layers' whole premise (§3.2 c), with the mask written as
        /// the scope writes it - bits 20 to 27 only.
        /// </summary>
        [UnityTest]
        public IEnumerator Culling_ARendererOnALayerOutsideTheMaskIsNotDrawn()
        {
            director.ClosePanels();
            yield return null;
            var view = cameraRig.ViewCamera;
            int kept = view.cullingMask;
            var built = new List<Object>();
            try
            {
                ScopeProbeQuad(built, "Room scope pin backdrop", ScopeProbeSpot, 4f, Color.gray, false);
                var marker = ScopeProbeCube(built, "Room scope pin marker", ScopeProbeSpot + Vector3.up * 1.2f, .8f, Color.red);
                marker.SetActive(false);
                var behind = ScopeProbeRender(view);
                marker.SetActive(true);
                var drawn = ScopeProbeRender(view);
                Assert.That(ScopeProbeDifference(drawn, behind), Is.GreaterThan(ScopeProbeDrawn), "The marker on layer 0 is drawn.");

                int layer = RoomLayers.Yard;
                view.cullingMask = (kept & ~RoomLayers.Mask) | (RoomSet.WholeHouse.CullingBits & ~(1 << layer));
                marker.layer = layer;
                var leftOut = ScopeProbeRender(view);
                Assert.That(ScopeProbeDifference(leftOut, behind), Is.LessThan(ScopeProbeSame),
                    "On the Yard's layer, with the Yard's bit out of the mask, the marker is not drawn: the backdrop shows.");

                marker.layer = 0;
                view.cullingMask = kept;
                var back = ScopeProbeRender(view);
                Assert.That(ScopeProbeDifference(back, behind), Is.GreaterThan(ScopeProbeDrawn), "Back on layer 0, it is drawn again.");
                Assert.That(ScopeProbeDifference(back, drawn), Is.LessThan(ScopeProbeSame), "As it was.");
            }
            finally
            {
                view.cullingMask = kept;
                foreach (var item in built) if (item != null) Object.Destroy(item);
            }
        }

        /// <summary>
        /// The lamps' hook (§3.2 d, D8 i): a lamp switched off in the main camera's begin callback and
        /// back on in its end callback is gone from the main camera's frame, still lights the live
        /// feed's, and reads as authored (on) between the two renders.
        /// </summary>
        [UnityTest]
        public IEnumerator Lights_ALampSwitchedOffForTheMainCameraStillLightsTheFeed()
        {
            director.ClosePanels();
            yield return null;
            var view = cameraRig.ViewCamera;
            var feed = Object.FindAnyObjectByType<LiveFeed>();
            Assert.That(feed, Is.Not.Null, "The director attaches the live feed.");
            Assert.That(feed.Camera, Is.Not.Null);
            var built = new List<Object>();
            bool switched = false;
            Light lamp = null;
            void Off(ScriptableRenderContext context, Camera camera)
            {
                if (camera != view || lamp == null || !lamp.enabled) return;
                lamp.enabled = false;
                switched = true;
            }
            void On(ScriptableRenderContext context, Camera camera)
            {
                if (camera != view || !switched) return;
                lamp.enabled = true;
                switched = false;
            }
            try
            {
                ScopeProbeQuad(built, "Room scope pin lit floor", ScopeProbeSpot, 4f, Color.white, true);
                lamp = ScopeProbeLamp(built, ScopeProbeSpot + Vector3.up * 1.2f);
                var mainLit = ScopeProbeRender(view);
                var feedLit = ScopeProbeRender(feed.Camera);
                lamp.enabled = false;
                var mainDark = ScopeProbeRender(view);
                var feedDark = ScopeProbeRender(feed.Camera);
                lamp.enabled = true;
                Assert.That(ScopeProbeDifference(mainLit, mainDark), Is.GreaterThan(ScopeProbeDrawn), "The lamp visibly lights the main camera's patch.");
                Assert.That(ScopeProbeDifference(feedLit, feedDark), Is.GreaterThan(ScopeProbeDrawn), "The lamp visibly lights the feed's patch.");

                RenderPipelineManager.beginCameraRendering += Off;
                RenderPipelineManager.endCameraRendering += On;
                var mainHooked = ScopeProbeRender(view);
                Assert.That(lamp.enabled, Is.True, "Between renders the lamp reads as authored.");
                var feedHooked = ScopeProbeRender(feed.Camera);
                Assert.That(lamp.enabled, Is.True, "And after the feed's.");
                Assert.That(ScopeProbeDifference(mainHooked, mainDark), Is.LessThan(ScopeProbeSame),
                    "Switched off in the main camera's begin callback, the lamp is gone from its frame: URP culls after the callback.");
                Assert.That(ScopeProbeDifference(feedHooked, feedLit), Is.LessThan(ScopeProbeSame), "The feed is still lit by it.");
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= Off;
                RenderPipelineManager.endCameraRendering -= On;
                if (lamp != null) lamp.enabled = true;
                foreach (var item in built) if (item != null) Object.Destroy(item);
            }
        }

        /// <summary>
        /// URP 17.6 does not drop a light whose layer the main camera's mask leaves out: the lamp on the
        /// Yard's layer, with the Yard's bit out of the mask, still lights the frame. So a lamp cannot
        /// be scoped by its layer, and RS3 keeps the begin and end hook (§3.2 d, D8 i). RS0 recorded the
        /// branch (Set A's three PlayMode runs logged "lights the frame", from the lit frame 0.000);
        /// RS3 asserts it, and the log line stays for the record.
        /// </summary>
        [UnityTest]
        public IEnumerator Lights_ALightOnALayerOutsideTheMask()
        {
            director.ClosePanels();
            yield return null;
            var view = cameraRig.ViewCamera;
            int kept = view.cullingMask;
            var built = new List<Object>();
            try
            {
                ScopeProbeQuad(built, "Room scope pin lit floor", ScopeProbeSpot, 4f, Color.white, true);
                var lamp = ScopeProbeLamp(built, ScopeProbeSpot + Vector3.up * 1.2f);
                var lit = ScopeProbeRender(view);
                lamp.enabled = false;
                var dark = ScopeProbeRender(view);
                lamp.enabled = true;
                Assert.That(ScopeProbeDifference(lit, dark), Is.GreaterThan(ScopeProbeDrawn), "The lamp visibly lights the patch on layer 0.");

                int layer = RoomLayers.Yard;
                view.cullingMask = (kept & ~RoomLayers.Mask) | (RoomSet.WholeHouse.CullingBits & ~(1 << layer));
                lamp.gameObject.layer = layer;
                var outside = ScopeProbeRender(view);
                float toDark = ScopeProbeDifference(outside, dark), toLit = ScopeProbeDifference(outside, lit);
                Assert.That(Mathf.Min(toDark, toLit), Is.LessThan(ScopeProbeSame),
                    "The frame is the lamp's lit one or its dark one (lit " + toLit.ToString("0.000") + ", dark " + toDark.ToString("0.000") + ").");
                bool culled = toDark < toLit;
                Debug.Log("[Gamesim RS0] URP 17.6 " + (culled
                    ? "drops a light on a layer the main camera's culling mask leaves out: it does not light the frame"
                    : "lights the frame with a light on a layer the main camera's culling mask leaves out")
                    + " (from the lit frame " + toLit.ToString("0.000") + ", from the dark " + toDark.ToString("0.000") + ").");
                Assert.That(culled, Is.False, "The camera's mask does not cull lights: the lamp on a masked-out layer lights the frame "
                    + "(lit " + toLit.ToString("0.000") + ", dark " + toDark.ToString("0.000") + "), so lamps are scoped by the hook, not by their layer.");
                Assert.That(toLit, Is.LessThan(ScopeProbeSame), "The frame is the lamp's lit one.");
            }
            finally
            {
                view.cullingMask = kept;
                foreach (var item in built) if (item != null) Object.Destroy(item);
            }
        }

        /// <summary>A flat floor facing up, of its own unlit colour or lit white, collider-free on layer 0.</summary>
        private static GameObject ScopeProbeQuad(List<Object> built, string name, Vector3 at, float size, Color colour, bool lit)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = Vector3.one * size;
            ScopeProbeMaterial(built, quad.GetComponent<Renderer>(), colour, lit);
            built.Add(quad);
            return quad;
        }

        private static GameObject ScopeProbeCube(List<Object> built, string name, Vector3 at, float size, Color colour)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.name = name;
            cube.transform.SetPositionAndRotation(at, Quaternion.identity);
            cube.transform.localScale = Vector3.one * size;
            ScopeProbeMaterial(built, cube.GetComponent<Renderer>(), colour, false);
            built.Add(cube);
            return cube;
        }

        private static void ScopeProbeMaterial(List<Object> built, Renderer renderer, Color colour, bool lit)
        {
            var shader = Shader.Find(lit ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit");
            Assert.That(shader, Is.Not.Null, "URP's " + (lit ? "Lit" : "Unlit") + " shader.");
            var material = new Material(shader) { color = colour };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            built.Add(material);
        }

        /// <summary>A lamp as the house's are (a soft-shadowed point light), close over the floor so its pool fills the patch.</summary>
        private static Light ScopeProbeLamp(List<Object> built, Vector3 at)
        {
            var holder = new GameObject("Room scope pin lamp");
            holder.transform.position = at;
            var lamp = holder.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 4f;
            lamp.intensity = 6f;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.Soft;
            built.Add(holder);
            return lamp;
        }

        /// <summary>
        /// The mean colour of the middle of a camera's frame looking straight down on the spot from 3.5 m:
        /// the camera drawn by hand into a target of its own, then everything about it put back.
        /// </summary>
        private static Color ScopeProbeRender(Camera camera)
        {
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            var target = camera.targetTexture;
            float fieldOfView = camera.fieldOfView;
            bool orthographic = camera.orthographic;
            var frame = new RenderTexture(ScopeProbeSide, ScopeProbeSide, 24, RenderTextureFormat.ARGB32);
            var read = new Texture2D(ScopeProbeSide / 4, ScopeProbeSide / 4, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            try
            {
                camera.transform.SetPositionAndRotation(ScopeProbeSpot + Vector3.up * 3.5f, Quaternion.Euler(90f, 0f, 0f));
                camera.fieldOfView = 30f;
                camera.orthographic = false;
                camera.targetTexture = frame;
                camera.Render();
                RenderTexture.active = frame;
                int side = ScopeProbeSide / 4;
                read.ReadPixels(new Rect((ScopeProbeSide - side) / 2, (ScopeProbeSide - side) / 2, side, side), 0, 0, false);
                read.Apply(false);
                var pixels = read.GetPixels();
                Color sum = Color.clear;
                foreach (var pixel in pixels) sum += pixel;
                return sum / Math.Max(1, pixels.Length);
            }
            finally
            {
                RenderTexture.active = active;
                camera.targetTexture = target;
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fieldOfView;
                camera.orthographic = orthographic;
                frame.Release();
                Object.Destroy(frame);
                Object.Destroy(read);
            }
        }

        private static float ScopeProbeDifference(Color a, Color b)
            => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
    }
}

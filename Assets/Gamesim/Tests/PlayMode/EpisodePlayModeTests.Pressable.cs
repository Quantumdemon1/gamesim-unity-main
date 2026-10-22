using System;
using System.Collections;
using System.Reflection;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private Button PressableProbe(Action action)
        {
            var hud = director.GetComponent<EpisodeHud>();
            var rect = new GameObject("Pressable lifecycle probe", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(hud.transform, false);
            var method = typeof(EpisodeHud).GetMethod("Pressable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Button)method.Invoke(hud, new object[] { rect, action });
        }

        [UnityTest]
        public IEnumerator Pressable_DisabledInactiveAndDestroyedControlsCannotInvokeAnAction()
        {
            int calls = 0; var button = PressableProbe(() => calls++); var retained = button.onClick;
            button.interactable = false; retained.Invoke(); Assert.That(calls, Is.Zero);
            button.interactable = true; retained.Invoke(); Assert.That(calls, Is.EqualTo(1));
            button.gameObject.SetActive(false); retained.Invoke(); Assert.That(calls, Is.EqualTo(1));
            button.gameObject.SetActive(true); retained.Invoke(); Assert.That(calls, Is.EqualTo(2));
            Object.Destroy(button.gameObject); yield return null;
            Assert.DoesNotThrow(() => retained.Invoke()); Assert.That(calls, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Pressable_AnAbsentActionIsInertEvenWhenItsEventIsInvokedDirectly()
        {
            var button = PressableProbe(null);
            Assert.DoesNotThrow(() => button.onClick.Invoke());
            Object.Destroy(button.gameObject); yield return null;
        }

        [UnityTest]
        public IEnumerator Pressable_NonInteractableCanvasGroupsCannotBypassEligibility()
        {
            int calls = 0; var button = PressableProbe(() => calls++);
            var group = button.gameObject.AddComponent<CanvasGroup>(); group.interactable = false;
            yield return null;
            button.onClick.Invoke(); Assert.That(calls, Is.Zero);
            group.interactable = true; yield return null;
            button.onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
            Object.Destroy(button.gameObject); yield return null;
        }

        private Canvas PressabilityCanvas()
        {
            var holder = new GameObject("Pressability frame probe", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            holder.transform.SetParent(director.transform, false);
            var canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32700;
            return canvas;
        }

        private static Button PressabilityButton(RectTransform parent)
        {
            var holder = new GameObject("Reachable frame probe", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)holder.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(96f, 48f);
            var button = holder.GetComponent<Button>();
            button.targetGraphic = holder.GetComponent<Image>();
            return button;
        }

        private static void DrawPressabilityFrame(CaptureLens lens)
        {
            var frame = lens.Read();
            Object.Destroy(frame);
        }

        [UnityTest]
        public IEnumerator Pressability_AHeldCaptureUsesItsFrameWithoutDroppingDisabledMaskOrOverlayChecks()
        {
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.Not.Null, "The real EventSystem checks every probe.");
            var canvas = PressabilityCanvas();
            var viewport = new GameObject("Pressability viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(canvas.transform, false);
            viewport.anchorMin = viewport.anchorMax = new Vector2(.75f, .75f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = new Vector2(160f, 96f);
            var button = PressabilityButton(viewport);
            var cover = new GameObject("Pressability cover", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            cover.SetParent(viewport, false);
            cover.anchorMin = cover.anchorMax = new Vector2(.5f, .5f);
            cover.anchoredPosition = Vector2.zero;
            cover.sizeDelta = button.GetComponent<RectTransform>().sizeDelta;
            cover.gameObject.SetActive(false);
            try
            {
                foreach (var frame in ReviewFrames)
                {
                    var lens = new CaptureLens(cameraRig.ViewCamera, frame.Width, frame.Height);
                    try
                    {
                        yield return null; yield return null;
                        Canvas.ForceUpdateCanvases();
                        yield return lens.MakeSureTheCanvasesAreDrawn();
                        DrawPressabilityFrame(lens);
                        Assert.That(CaptureLens.TryFrame(canvas, canvas.worldCamera, out var bounds), Is.True);
                        Assert.That(bounds, Is.EqualTo(new Rect(0f, 0f, frame.Width, frame.Height)));
                        Assert.DoesNotThrow(() => AssertPressable(button, "Reachable frame probe"),
                            "The real raycast reaches the probe in the " + frame.Shape + " capture.");

                        button.interactable = false;
                        var disabled = Assert.Throws<AssertionException>(() => AssertPressable(button, "Reachable frame probe"));
                        Assert.That(disabled.Message, Does.Contain("not interactable"));
                        button.interactable = true;

                        cover.gameObject.SetActive(true);
                        yield return null;
                        DrawPressabilityFrame(lens);
                        var covered = Assert.Throws<AssertionException>(() => AssertPressable(button, "Reachable frame probe"));
                        Assert.That(covered.Message, Does.Contain("covered"));
                        cover.gameObject.SetActive(false);

                        ((RectTransform)button.transform).anchoredPosition = new Vector2(100f, 0f);
                        yield return null;
                        DrawPressabilityFrame(lens);
                        var eye = canvas.worldCamera;
                        var centre = RectTransformUtility.WorldToScreenPoint(eye, button.transform.position);
                        Assert.That(bounds.Contains(centre), Is.True, "The masked centre is inside the frame, not an off-frame control.");
                        Assert.That(button.targetGraphic.Raycast(centre, eye), Is.False, "The actual RectMask2D excludes it.");
                        Assert.Throws<AssertionException>(() => AssertPressable(button, "Reachable frame probe"));
                        ((RectTransform)button.transform).anchoredPosition = Vector2.zero;

                        viewport.anchoredPosition = new Vector2(frame.Width, frame.Height);
                        Canvas.ForceUpdateCanvases();
                        var outside = Assert.Throws<AssertionException>(() => AssertPressable(button, "Reachable frame probe"));
                        Assert.That(outside.Message, Does.Contain("capture frame"));
                        viewport.anchoredPosition = Vector2.zero;
                    }
                    finally { lens.Dispose(); }
                    Assert.That(CaptureLens.TryFrame(canvas, canvas.worldCamera, out _), Is.False,
                        "Disposal gives no later control the capture's bounds.");
                    yield return null;
                }
            }
            finally { Object.Destroy(canvas.gameObject); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pressability_AnUnownedTargetAndTheRestoredOverlayStillUseTheRealScreenBounds()
        {
            Assert.That(UnityEngine.EventSystems.EventSystem.current, Is.Not.Null);
            Assert.That(Screen.width, Is.GreaterThan(0));
            Assert.That(Screen.height, Is.GreaterThan(0));
            var canvas = PressabilityCanvas();
            var button = PressabilityButton((RectTransform)canvas.transform);
            var cameraHolder = new GameObject("Unowned pressability camera", typeof(Camera));
            var eye = cameraHolder.GetComponent<Camera>();
            var target = new RenderTexture(Screen.width + 320, Screen.height + 240, 0);
            eye.enabled = false;
            eye.targetTexture = target;
            eye.orthographic = true;
            eye.orthographicSize = target.height * .5f;
            eye.nearClipPlane = 9f; eye.farClipPlane = 11f;
            cameraHolder.transform.SetPositionAndRotation(new Vector3(target.width * .5f, target.height * .5f, -10f), Quaternion.identity);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = eye;
            canvas.planeDistance = 10f;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = new Vector2(Screen.width + 80f, Screen.height + 80f);
            try
            {
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(CaptureLens.TryFrame(canvas, eye, out _), Is.False, "An arbitrary target is not the capture instrument.");
                var centre = RectTransformUtility.WorldToScreenPoint(eye, rect.TransformPoint(rect.rect.center));
                Assert.That(centre.x, Is.GreaterThan(Screen.width));
                Assert.That(centre.y, Is.GreaterThan(Screen.height));
                var unowned = Assert.Throws<AssertionException>(() => AssertPressable(button, "Reachable frame probe"));
                Assert.That(unowned.Message, Does.Contain("screen").And.Not.Contain("capture frame"));

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(CaptureLens.TryFrame(canvas, null, out _), Is.False);
                var screen = Assert.Throws<AssertionException>(() => AssertPressable(button, "Reachable frame probe"));
                Assert.That(screen.Message, Does.Contain("screen").And.Not.Contain("capture frame"));
            }
            finally
            {
                eye.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(cameraHolder);
                Object.Destroy(canvas.gameObject);
            }
            yield return null;
        }
    }
}

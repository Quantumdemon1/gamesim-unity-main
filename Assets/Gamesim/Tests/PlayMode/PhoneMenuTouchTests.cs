using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Phase = UnityEngine.InputSystem.TouchPhase;

namespace Gamesim.Tests.PlayMode
{
    public sealed class PhoneMenuTouchTests
    {
        private GameObject fixture;
        private Touchscreen finger;
        private EventSystem previous;
        private bool previousEnabled;

        [SetUp]
        public void SetUp()
        {
            previous = EventSystem.current;
            previousEnabled = previous != null && previous.enabled;
            if (previousEnabled) previous.enabled = false;
            fixture = new GameObject("Phone menu fixture", typeof(EventSystem), typeof(InputSystemUIInputModule));
            fixture.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            finger = InputSystem.AddDevice<Touchscreen>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            fixture.SetActive(false);
            Object.Destroy(fixture);
            if (finger != null && finger.added) InputSystem.RemoveDevice(finger);
            if (previousEnabled && previous != null) previous.enabled = true;
            yield return null;
        }

        private IEnumerator Send(Vector2 position, Phase phase)
        {
            InputSystem.QueueStateEvent(finger, new TouchState { touchId = 1, position = position, phase = phase });
            InputSystem.Update();
            yield return null;
        }

        private static Button Find(Component root, string name) => root.GetComponentsInChildren<Button>()
            .Single(button => button.name == name);

        internal static Button PhoneExit(Component root, string name, string caption)
        {
            var page = root.GetComponentInChildren<PhoneMenuPage>();
            var button = Find(page, name);
            Assert.That(WayOut.CaptionOf(button), Is.EqualTo(caption));
            var rect = (RectTransform)button.transform;
            Assert.That(rect.parent, Is.SameAs(page.transform.Find("Safe content")));
            var corner = new Vector2(name == WayOut.CloseObjectName ? 1 : 0, 1);
            Assert.That(rect.anchorMin, Is.EqualTo(corner));
            Assert.That(rect.anchorMax, Is.EqualTo(corner));
            Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(48f));
            Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(48f));
            foreach (var column in page.GetComponentsInChildren<ScrollRect>())
                Assert.That(column.viewport.offsetMax.y, Is.LessThanOrEqualTo(rect.anchoredPosition.y - rect.rect.height));
            return button;
        }

        private static Vector2 Centre(Component control)
        {
            var rect = (RectTransform)control.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        private IEnumerator Tap(Button control)
        {
            var position = Centre(control);
            yield return Send(position, Phase.Began);
            yield return Send(position, Phase.Ended);
            yield return null;
        }

        [TestCase(80, 12, 760, 360)]
        [TestCase(4, 12, 760, 360)]
        public void SafeAreaIncludesCutoutAndHomeIndicator(int left, int bottom, int width, int height)
        {
            var area = PhoneMenuPage.NormalizedSafeArea(new Rect(left, bottom, width, height), 844, 390);
            Assert.That(area.xMin * 844f, Is.EqualTo(left).Within(.01));
            Assert.That(area.xMax * 844f, Is.EqualTo(left + width).Within(.01));
            Assert.That(area.yMin * 390f, Is.EqualTo(bottom).Within(.01));
            Assert.That(area.yMax * 390f, Is.EqualTo(bottom + height).Within(.01));
        }

        [UnityTest]
        public IEnumerator FrontDoorTouchOpensExactlyTheOfferedSave()
        {
            var menu = MainMenu.Attach(fixture);
            menu.GetComponent<FrameScaler>().UsePhoneUnits = true;
            int continued = 0, loaded = 0;
            menu.Show(false, "No save available", null, () => {}, () => {}, () => {});
            yield return null;
            Assert.That(menu.GetComponentsInChildren<Button>().Any(b => b.name == MainMenu.ContinueCaption), Is.False);
            menu.Show(true, null, () => continued++, () => {}, () => {}, () => {}, onLoad: () => loaded++, seasonFacts: "Week 3");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var button = Find(menu, MainMenu.ContinueCaption);
            Assert.That(((RectTransform)button.transform).rect.height, Is.GreaterThanOrEqualTo(48));
            yield return Tap(button);
            Assert.That(continued, Is.EqualTo(1));
            Assert.That(loaded, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RosterProfileBackThenPickAndStartUseTheSamePerson()
        {
            SeasonBuilder.Choice picked = null;
            var cast = CastSelect.Attach(fixture);
            cast.GetComponent<FrameScaler>().UsePhoneUnits = true;
            cast.Show(choice => picked = choice, () => {});
            yield return null;
            var guest = CastTemplates.In(CastTemplates.Roster.Regular).First();
            var scroll = Find(cast, guest.Name).GetComponentInParent<ScrollRect>();
            // Place the named control on screen as a player can by scrolling the roster.
            var row = Find(cast, guest.Name);
            var content = scroll.content;
            Canvas.ForceUpdateCanvases();
            float overflow = content.rect.height - scroll.viewport.rect.height;
            if (overflow > 0) scroll.verticalNormalizedPosition = Mathf.Clamp01(1 - (-((RectTransform)row.transform).anchoredPosition.y - 20) / overflow);
            float before = scroll.verticalNormalizedPosition;
            yield return Tap(row);
            Assert.That(picked, Is.Null);
            var back = PhoneExit(cast, WayOut.BackObjectName, WayOut.BackTo(WayOut.Cast));
            yield return Tap(back);
            var restored = Find(cast, guest.Name);
            Assert.That(restored.GetComponentInParent<ScrollRect>().verticalNormalizedPosition, Is.EqualTo(before).Within(.03f));
            yield return Tap(restored);
            yield return Tap(Find(cast, "Use this houseguest"));
            Assert.That(picked, Is.Null);
            yield return Tap(Find(cast, cast.StartCaptionNow));
            Assert.That(picked.PlayerTemplateId, Is.EqualTo(guest.Id));
        }

        [UnityTest]
        public IEnumerator DragStartingOnACastRowScrollsWithoutOpeningThePerson()
        {
            var cast = CastSelect.Attach(fixture);
            cast.GetComponent<FrameScaler>().UsePhoneUnits = true;
            cast.Show(_ => Assert.Fail("A drag started a season"), () => {});
            yield return null;
            var scroll = Find(cast, "Season setup").GetComponentInParent<ScrollRect>();
            var viewport = scroll.viewport;
            // Constrain just the test viewport so even a large Editor display has overflow.
            viewport.anchorMax = new Vector2(viewport.anchorMax.x, viewport.anchorMin.y);
            viewport.pivot = new Vector2(.5f, 0);
            viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, 180);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
            var from = Centre(Find(cast, "Season setup"));
            yield return Send(from, Phase.Began);
            yield return Send(from + Vector2.up * 24, Phase.Moved);
            yield return Send(from + Vector2.up * 70, Phase.Moved);
            yield return Send(from + Vector2.up * 70, Phase.Ended);
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.999f));
            Assert.That(Find(cast, "Season setup"), Is.Not.Null);
        }
    }
}

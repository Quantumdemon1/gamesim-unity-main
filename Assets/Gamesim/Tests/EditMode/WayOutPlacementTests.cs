using System;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Where the one way out sits and how it is found (menu slice 2, change 1): a way back at the
    /// frame's top left, Close at its top right, each under its fixed object name with its caption as
    /// its first text, reachable by keyboard and pad, its focus ring dark until it is selected, and
    /// the key hint beside the caption rather than in it. Built in EditMode on a bare canvas; the
    /// texts are read through the helper's own readers, so this file needs no text package.
    /// </summary>
    public sealed class WayOutPlacementTests
    {
        private GameObject root;
        private RectTransform frame;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Way out test canvas", typeof(RectTransform), typeof(Canvas));
            frame = new GameObject("Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(root.transform, false);
            frame.sizeDelta = new Vector2(1200f, 800f);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void AddBack_SitsAtTheTopLeft_UnderItsName_WithItsCaptionFirst()
        {
            int pressed = 0;
            var back = WayOut.AddBack(frame, WayOut.BackTo(WayOut.House), () => pressed++);
            var rect = (RectTransform)back.transform;
            Assert.That(rect.parent, Is.SameAs(frame));
            Assert.That(back.gameObject.name, Is.EqualTo(WayOut.BackObjectName));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.anchoredPosition.x, Is.GreaterThan(0f));
            Assert.That(rect.anchoredPosition.y, Is.LessThan(0f));
            Assert.That(WayOut.CaptionOf(back), Is.EqualTo("Back to the house"), "the caption is the first text");
            Assert.That(back.transform.Find("Label"), Is.Not.Null);
            back.onClick.Invoke();
            Assert.That(pressed, Is.EqualTo(1), "the screen's action runs; the helper does not navigate");
        }

        [Test]
        public void AddClose_SitsAtTheTopRight_AndSaysClose()
        {
            var close = WayOut.AddClose(frame, () => { });
            var rect = (RectTransform)close.transform;
            Assert.That(close.gameObject.name, Is.EqualTo(WayOut.CloseObjectName));
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(1f, 1f)));
            Assert.That(rect.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(rect.anchoredPosition.y, Is.LessThan(0f));
            Assert.That(WayOut.CaptionOf(close), Is.EqualTo(WayOut.CloseCaption));
            Assert.That(WayOut.KeyHintOf(close), Is.Null);
        }

        [Test]
        public void AddBack_RefusesACaptionThatIsNotAWayBack()
        {
            Assert.Throws<ArgumentException>(() => WayOut.AddBack(frame, "Back", () => { }));
            Assert.Throws<ArgumentException>(() => WayOut.AddBack(frame, "Done", () => { }));
        }

        [Test]
        public void KeyHint_IsDrawnBesideTheCaption_NotInIt()
        {
            var close = WayOut.AddClose(frame, () => { }, 1f, "Esc");
            Assert.That(WayOut.CaptionOf(close), Is.EqualTo("Close"), "the caption is still the first text, with no key in it");
            Assert.That(WayOut.KeyHintOf(close), Is.EqualTo("Esc"));
        }

        [Test]
        public void EveryTextSize_GivesALabelBoxAtLeast1Point3TimesItsFont()
        {
            foreach (var scale in new[] { 1f, 1.2f, 1.4f })
            {
                var back = WayOut.AddBack(frame, WayOut.BackToKeeping(WayOut.Cast, "draft"), () => { }, scale);
                var sizes = WayOut.CaptionSizes(back);
                float box = ((RectTransform)back.transform).sizeDelta.y;
                Assert.That(sizes.x, Is.GreaterThanOrEqualTo(WayOut.MinFontSize), "scale " + scale);
                Assert.That(sizes.y, Is.GreaterThanOrEqualTo(WayOut.MinFontSize), "auto-size never shrinks it under 14, scale " + scale);
                Assert.That(box, Is.GreaterThanOrEqualTo(sizes.x * 1.3f), "scale " + scale);
            }
        }

        [Test]
        public void TheControl_IsReachableByKeyboardAndPad_AndItsRingIsDarkUntilSelected()
        {
            var back = WayOut.AddBack(frame, WayOut.BackTo(WayOut.Cast), () => { });
            Assert.That(back.navigation.mode, Is.Not.EqualTo(Navigation.Mode.None));
            Assert.That(back.interactable, Is.True);
            var ring = back.transform.Find("Focus ring");
            Assert.That(ring, Is.Not.Null);
            Assert.That(ring.GetComponent<Image>().enabled, Is.False, "focus never reads as a second selection");
            Assert.That(back.colors.selectedColor, Is.EqualTo(back.colors.highlightedColor));
        }
    }
}

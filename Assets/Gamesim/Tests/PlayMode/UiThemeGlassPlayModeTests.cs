using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The glass primitive and the halo it is built from.
    ///
    /// <para><see cref="UiTheme.AddGlow"/> was extracted from <see cref="UiTheme.Glass"/> so the
    /// cast screen could ask for the glow without the rest - it called Glass for the halo alone,
    /// then painted its own ground back and drew a second ring over the hairline. Glass has to come
    /// out of that byte for byte what it was, because five ceremony cards, the room labels and the
    /// house map are dressed by it and none of them asked for a change. And the halo on its own must
    /// leave the ground it was put on exactly as it found it, or the cast screen is back to writing
    /// its ground back.</para>
    /// </summary>
    public sealed class UiThemeGlassPlayModeTests
    {
        private GameObject host;

        [SetUp]
        public void CreateHost() => host = new GameObject("Glass host", typeof(RectTransform));

        [TearDown]
        public void DestroyHost()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(false, TestName = "Glass_IsTodaysGlass(UiTheme.Glass on a card-ground control)")]
        [TestCase(true, TestName = "Glass_IsTodaysGlass(HudPrimitives.Glass)")]
        public void Glass_IsTodaysGlass(bool throughTheWrapper)
        {
            RectTransform panel;
            if (throughTheWrapper)
            {
                panel = HudPrimitives.Glass("Panel", host.transform);
            }
            else
            {
                // A different colour and radius from what Glass lays down, so the assertions below
                // can only pass if Glass restyled the ground rather than inherited it.
                panel = HudPrimitives.Fill("Panel", host.transform, UiTheme.CardFill, UiTheme.ControlRadius);
                UiTheme.Glass(panel);
            }

            var ground = panel.GetComponent<Image>();
            Assert.That(Same(ground.color, UiTheme.GlassFill), Is.True, "The ground is the night at 85 %.");
            Assert.That(ground.sprite.name, Is.EqualTo("UiTheme Fill " + UiTheme.GlassRadius));
            Assert.That(ground.type, Is.EqualTo(Image.Type.Sliced));

            Assert.That(ChildNames(panel), Is.EqualTo(new[] { "Glow", "Border" }),
                "Glass is a halo behind and one hairline in front, and nothing else.");

            var glow = (RectTransform)panel.GetChild(0);
            var glowImage = glow.GetComponent<Image>();
            Assert.That(Same(glowImage.color, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, 0.35f)), Is.True);
            Assert.That(glowImage.sprite.name, Is.EqualTo("UiTheme Glow " + UiTheme.GlassRadius));
            Assert.That(glow.offsetMin, Is.EqualTo(new Vector2(-UiTheme.GlowWidth, -UiTheme.GlowWidth)));
            Assert.That(glow.offsetMax, Is.EqualTo(new Vector2(UiTheme.GlowWidth, UiTheme.GlowWidth)));

            var border = panel.GetChild(1).GetComponent<Image>();
            Assert.That(Same(border.color, UiTheme.Hairline), Is.True, "The edge is the cyan hairline.");
            Assert.That(border.sprite.name, Is.EqualTo("UiTheme Outline " + UiTheme.GlassRadius));

            foreach (var graphic in panel.GetComponentsInChildren<Graphic>(true).Where(item => item != ground))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name + " is dressing and must not take a click.");
        }

        [Test]
        public void AddGlow_LeavesTheGroundAloneAndSitsBehindEverything()
        {
            var panel = HudPrimitives.Fill("Card", host.transform, UiTheme.CardFill, UiTheme.GlassRadius);
            HudPrimitives.Label("Label", panel, 14f, UiTheme.Paper);
            UiTheme.AddBorder(panel, UiTheme.GlassRadius, UiTheme.Hairline);

            UiTheme.AddGlow(panel, UiTheme.GlassRadius);

            Assert.That(Same(panel.GetComponent<Image>().color, UiTheme.CardFill), Is.True,
                "The halo must not repaint the ground it is put on. That repaint is the whole reason "
                + "the cast screen used to write its card ground back after asking for a glow.");
            Assert.That(ChildNames(panel), Is.EqualTo(new[] { "Glow", "Label", "Border" }),
                "One child, added BEHIND everything already there - a halo drawn over the copy is a "
                + "haze over the copy.");

            var glow = (RectTransform)panel.GetChild(0);
            var glowImage = glow.GetComponent<Image>();
            Assert.That(Same(glowImage.color, new Color(UiTheme.Glow.r, UiTheme.Glow.g, UiTheme.Glow.b, 0.35f)), Is.True);
            Assert.That(glow.offsetMin, Is.EqualTo(new Vector2(-UiTheme.GlowWidth, -UiTheme.GlowWidth)),
                "It reaches past the rect, so layout and overlap checks go on reading the rect.");
            Assert.That(glowImage.raycastTarget, Is.False);
            Assert.That(Same(panel.GetChild(2).GetComponent<Image>().color, UiTheme.Hairline), Is.True,
                "and the edge that was there is untouched.");
        }

        private static string[] ChildNames(Transform parent)
        {
            var names = new string[parent.childCount];
            for (int index = 0; index < parent.childCount; index++) names[index] = parent.GetChild(index).name;
            return names;
        }

        private static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < .004f && Mathf.Abs(a.g - b.g) < .004f
            && Mathf.Abs(a.b - b.b) < .004f && Mathf.Abs(a.a - b.a) < .004f;
    }
}

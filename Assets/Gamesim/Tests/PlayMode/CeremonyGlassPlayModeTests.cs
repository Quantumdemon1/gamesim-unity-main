using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// V2 (VISUAL-TARGET.md §4, mockup-08 and -10): every ceremony card is drawn on the mockups'
    /// glass — the night ground at 85 %, a cyan hairline on the edge, a soft glow outside it.
    ///
    /// <para>The five overlays are built five different ways — the sting is a strip, the others are
    /// columns that stack their pieces — so "they are all on glass now" is a claim about five files
    /// and is checked in all five. The other half of each assertion is the rule that made these
    /// overlays safe to add to a finished episode in the first place: the dressing must not have
    /// given any of them something that takes a click.</para>
    /// </summary>
    public sealed class CeremonyGlassPlayModeTests
    {
        private GameObject owner;
        private readonly List<GameObject> overlays = new List<GameObject>();

        [SetUp]
        public void CreateOwner() => owner = new GameObject("Ceremony glass owner");

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            foreach (var overlay in overlays) if (overlay != null) Object.Destroy(overlay);
            overlays.Clear();
            if (owner != null) Object.Destroy(owner);
            yield return null;
        }

        private T Stage<T>(T overlay) where T : Component
        {
            overlays.Add(overlay.gameObject);
            return overlay;
        }

        [UnityTest]
        public IEnumerator Sting_IsDrawnOnGlass()
        {
            var sting = Stage(CeremonySting.Attach(owner));
            sting.Play(CeremonySting.EvictionKind, "Casey Wilson has been evicted.", true);
            yield return null;

            AssertGlass(sting, "Card");
            AssertNothingTakesAClick(sting);
        }

        [UnityTest]
        public IEnumerator Takeover_IsDrawnOnGlass()
        {
            var takeover = Stage(CeremonyTakeover.Attach(owner));
            takeover.Play(CeremonySting.NominationKind, 1,
                new[] { new CeremonyTakeover.Subject("Maya Hassan", "HOH", null) }, true);
            yield return null;

            AssertGlass(takeover, "Card glass");
            AssertNothingTakesAClick(takeover);
        }

        [UnityTest]
        public IEnumerator VoteReveal_IsDrawnOnGlass()
        {
            var reveal = Stage(VoteReveal.Attach(owner));
            bool played = reveal.Play(1,
                new[]
                {
                    new VoteReveal.Nominee("a", "Emma Brown", null),
                    new VoteReveal.Nominee("b", "Jordan Taylor", null),
                },
                new[] { new VoteReveal.Ballot("Maya Hassan", "a"), new VoteReveal.Ballot("Riley Johnson", "a") },
                "a", true);
            Assert.That(played, Is.True, "Two nominees and two ballots is a shape the reveal narrates.");
            yield return null;

            AssertGlass(reveal, "Card glass");
            AssertNothingTakesAClick(reveal);
        }

        [UnityTest]
        public IEnumerator KeyCeremony_IsDrawnOnGlassWithoutDisturbingTheKeyCount()
        {
            var keys = Stage(KeyCeremony.Attach(owner));
            var safe = new[]
            {
                new KeyCeremony.Person("a", "Emma Brown", null),
                new KeyCeremony.Person("b", "Riley Johnson", null),
                new KeyCeremony.Person("c", "Alex Chen", null),
            };
            var block = new[]
            {
                new KeyCeremony.Person("d", "Jordan Taylor", null),
                new KeyCeremony.Person("e", "Casey Wilson", null),
            };
            Assert.That(keys.Play(1, "Maya Hassan", false, safe, block, true), Is.True);
            yield return null;

            AssertGlass(keys, "Card glass");
            AssertNothingTakesAClick(keys);

            // The suite counts key slots by the "Key " prefix, so nothing the dressing adds may
            // borrow that name.
            int slots = keys.GetComponentsInChildren<RectTransform>(true)
                .Count(rect => rect.name.StartsWith("Key ", System.StringComparison.Ordinal));
            Assert.That(slots, Is.EqualTo(safe.Length),
                "One slot per key, and the glass ground is not one of them.");
        }

        [UnityTest]
        public IEnumerator CompetitionResult_IsDrawnOnGlass()
        {
            var result = Stage(CompetitionResult.Attach(owner));
            var standings = new[]
            {
                new CompetitionResult.Standing("Maya Hassan", 9.5d, true, false, null),
                new CompetitionResult.Standing("Riley Johnson", 7.25d, false, true, null),
            };
            Assert.That(result.Play("Head of Household", "Skill", 1, standings, true), Is.True);
            yield return null;

            AssertGlass(result, "Card glass");
            AssertNothingTakesAClick(result);
        }

        /// <summary>
        /// The named rect is the mockups' glass: the night ground at 85 %, a hairline child and a
        /// glow child that reaches past the rect rather than inside it.
        /// </summary>
        private static void AssertGlass(Component overlay, string cardName)
        {
            var card = overlay.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == cardName);
            Assert.That(card, Is.Not.Null, overlay.GetType().Name + " has no '" + cardName + "' to dress.");

            var ground = card.GetComponent<Image>();
            Assert.That(ground, Is.Not.Null, cardName + " needs a ground to be glass at all.");
            Assert.That(ground.color.r, Is.EqualTo(UiTheme.GlassFill.r).Within(0.005f));
            Assert.That(ground.color.g, Is.EqualTo(UiTheme.GlassFill.g).Within(0.005f));
            Assert.That(ground.color.b, Is.EqualTo(UiTheme.GlassFill.b).Within(0.005f));
            Assert.That(ground.color.a, Is.EqualTo(UiTheme.GlassFill.a).Within(0.005f),
                "The night ground shows the set through it; that is what makes it glass.");

            var border = Child(card, "Border");
            Assert.That(border, Is.Not.Null, cardName + " has no hairline.");
            var glow = Child(card, "Glow");
            Assert.That(glow, Is.Not.Null, cardName + " has no glow.");

            var glowRect = (RectTransform)glow;
            Assert.That(glowRect.offsetMin.x, Is.EqualTo(-(float)UiTheme.GlowWidth).Within(0.001f),
                "The glow reaches past the card's edge, so layout and overlap checks read the card.");
            Assert.That(glowRect.offsetMax.y, Is.EqualTo((float)UiTheme.GlowWidth).Within(0.001f));
        }

        /// <summary>
        /// The rule the ceremony overlays were built around: they cannot swallow a click during the
        /// most consequential seconds of the episode. Dressing them must not have changed that.
        /// </summary>
        private static void AssertNothingTakesAClick(Component overlay)
        {
            Assert.That(overlay.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty,
                overlay.GetType().Name + " must have no raycaster at all.");
            foreach (var graphic in overlay.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False,
                    overlay.GetType().Name + "'s " + graphic.name + " must not take raycasts.");
        }

        private static Transform Child(Transform parent, string name)
        {
            foreach (Transform child in parent) if (child.name == name) return child;
            return null;
        }
    }
}

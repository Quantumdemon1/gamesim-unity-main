using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The ceremony card is deliberately inert: it must never take input, and it must never join the
    /// UI queries the rest of the PlayMode suite runs against the director. Those two properties are
    /// what make it safe to add to a finished episode, so they are asserted rather than assumed.
    /// </summary>
    public sealed class CeremonyStingPlayModeTests
    {
        private GameObject owner;
        private CeremonySting sting;

        [SetUp]
        public void CreateSting()
        {
            owner = new GameObject("Ceremony sting owner");
            sting = CeremonySting.Attach(owner);
        }

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            if (sting != null) Object.Destroy(sting.gameObject);
            if (owner != null) Object.Destroy(owner);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sting_IsNotParentedUnderItsOwner()
        {
            sting.Play(CeremonySting.NominationKind, "Maya Hassan nominated Taylor Kim.", false);
            yield return null;

            Assert.That(sting.transform.parent, Is.Null,
                "The card is a scene root on purpose.");
            Assert.That(owner.GetComponentsInChildren<TMP_Text>(true), Is.Empty,
                "PlayMode helpers read text through GetComponentsInChildren on the director; " +
                "the card must not appear in those results.");
            Assert.That(sting.gameObject.scene, Is.EqualTo(owner.scene),
                "It still belongs to the owner's scene so it unloads with it.");
        }

        [UnityTest]
        public IEnumerator Sting_NeverTakesInput()
        {
            sting.Play(CeremonySting.EvictionKind, "Casey Wilson has been evicted.", false);
            yield return null;

            Assert.That(sting.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty,
                "Without a raycaster the canvas cannot receive a pointer event at all.");
            var group = sting.GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            foreach (var graphic in sting.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name + " must not take raycasts.");
        }

        [UnityTest]
        public IEnumerator Play_ShowsTheHeadlineAndTheCommittedText()
        {
            const string committed = "Maya Hassan nominated Taylor Kim and Jamie Roberts.";
            sting.Play(CeremonySting.NominationKind, committed, false);
            yield return null;

            var shown = VisibleText();
            Assert.That(shown, Does.Contain("NOMINATION CEREMONY"));
            Assert.That(shown, Does.Contain(committed));
        }

        [UnityTest]
        public IEnumerator EveryCeremonyKind_HasItsOwnHeadline()
        {
            var expected = new[]
            {
                (CeremonySting.NominationKind, "NOMINATION CEREMONY"),
                (CeremonySting.VetoKind, "VETO CEREMONY"),
                (CeremonySting.EvictionKind, "EVICTION"),
                (CeremonySting.WinnerKind, "THE WINNER"),
            };

            foreach (var (kind, headline) in expected)
            {
                sting.Play(kind, "detail for " + kind, false);
                yield return null;
                Assert.That(VisibleText(), Does.Contain(headline), kind + " should announce itself.");
            }
        }

        [UnityTest]
        public IEnumerator NonCeremonyKinds_ShowNothing()
        {
            foreach (var kind in new[] { "competition", "private-vote", "conversation", null, "" })
            {
                Assert.That(CeremonySting.IsCeremony(kind), Is.False, kind + " is not a ceremony.");
                sting.Play(kind, "should never appear", false);
                yield return null;
                Assert.That(VisibleText(), Does.Not.Contain("should never appear"));
            }
        }

        [UnityTest]
        public IEnumerator ReducedMotion_KeepsTheInformationAndDropsTheMovement()
        {
            sting.Play(CeremonySting.WinnerKind, "Riley Johnson wins the season.", true);
            yield return null;

            var group = sting.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(1f).Within(0.0001f),
                "Reduced motion removes the fade, not the card.");
            var card = CardRect();
            var settled = card.anchoredPosition;

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(card.anchoredPosition, Is.EqualTo(settled),
                "The card must not travel when motion is reduced.");
            Assert.That(group.alpha, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(VisibleText(), Does.Contain("Riley Johnson wins the season."));
        }

        [UnityTest]
        public IEnumerator Card_RetiresItselfAfterItsDwell()
        {
            sting.Play(CeremonySting.VetoKind, "Taylor Kim used the veto.", false);
            yield return null;
            Assert.That(VisibleText(), Does.Contain("VETO CEREMONY"));

            // Fade in, hold and fade out total 2.6s; allow a margin for frame granularity.
            yield return new WaitForSecondsRealtime(3.1f);
            Assert.That(VisibleText(), Is.Empty, "The card should take itself down without being told.");
        }

        [UnityTest]
        public IEnumerator Cancel_TakesTheCardDownImmediately()
        {
            sting.Play(CeremonySting.EvictionKind, "Jamie Roberts has been evicted.", false);
            yield return null;
            Assert.That(VisibleText(), Is.Not.Empty);

            sting.Cancel();
            yield return null;
            Assert.That(VisibleText(), Is.Empty);
        }

        /// <summary>
        /// The card renders on a higher canvas than the HUD, so anything it covers is invisible even
        /// though it stays clickable. The episode panel's top edge and its Close button reach y=330
        /// in the 1600x900 reference, so the card has to stay above that at every text size.
        /// </summary>
        /// <summary>
        /// Two different bounds, because the card is in two different situations.
        ///
        /// The lower bound holds at every instant: the card descends into place, so it is never
        /// lower than where it settles, and where it settles is chosen to clear the panel.
        ///
        /// The upper bound holds only at rest. During the 0.22 s slide-in the card is deliberately
        /// part-way above the screen edge — that is the entrance, not clipping.
        /// </summary>
        [UnityTest]
        public IEnumerator Card_SitsAsATopBannerAtEveryTextSize()
        {
            var canvas = (RectTransform)sting.transform;

            foreach (float scale in new[] { 1f, 1.2f })
            {
                sting.FontScale = scale;
                sting.Play(CeremonySting.NominationKind, "Maya Hassan nominated Taylor Kim.", false);
                yield return new WaitForSecondsRealtime(0.4f); // past the entrance

                Canvas.ForceUpdateCanvases();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, CardRect());
                float halfHeight = canvas.rect.height * 0.5f;

                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(halfHeight + 0.5f),
                    "At font scale " + scale + " the resting card is clipped by the top of the canvas.");
                Assert.That(bounds.min.y, Is.GreaterThan(0f),
                    "At font scale " + scale + " the card dropped out of the upper half; it is a top banner.");
                Assert.That(bounds.size.x, Is.GreaterThan(200f),
                    "At font scale " + scale + " the card collapsed to " + bounds.size.x + " units wide.");
            }
        }

        /// <summary>
        /// Renders a real card to an image, so "does the card look right" can be answered by looking
        /// at it rather than inferred from geometry assertions.
        ///
        /// <para><see cref="ScreenCapture"/> writes nothing in an editor batchmode run — it works in
        /// a built player, which is where the standalone harness uses it. So this points a camera of
        /// its own at the card's canvas and renders that. It is the same hierarchy and the same
        /// materials; only the destination changes, and only for this fixture.</para>
        ///
        /// <para>Capture is limited to the headless harness so an interactive run does not litter
        /// the project. The assertion is deliberately weak: a frame of one flat colour means nothing
        /// drew, and everything beyond that is for a person to judge.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Card_RendersToAnInspectableImage()
        {
            if (!Application.isBatchMode) yield break;

            sting.FontScale = 1f;
            sting.Play(CeremonySting.NominationKind,
                "Maya Hassan nominated Taylor Kim and Jamie Roberts.", false);
            for (int i = 0; i < 40; i++) yield return null; // past the entrance, card at rest

            const int width = 1600, height = 900;
            var texture = new RenderTexture(width, height, 24) { name = "Ceremony card capture" };
            var cameraObject = new GameObject("Ceremony card camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f); // stand-in for the lit set
            camera.targetTexture = texture;

            var canvas = sting.GetComponent<Canvas>();
            var previousMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            var readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                camera.Render();

                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();

                var pixels = readback.GetPixels32();
                var distinct = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < pixels.Length; i += 29)
                    distinct.Add((pixels[i].r << 16) | (pixels[i].g << 8) | pixels[i].b);
                Assert.That(distinct.Count, Is.GreaterThan(1),
                    "The render is a single flat colour, so the card did not draw.");

                var path = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "ceremony-card.png"));
                System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Gamesim] Ceremony card rendered -> " + path
                    + " (" + distinct.Count + " sampled colours)");
            }
            finally
            {
                RenderTexture.active = previousActive;
                canvas.renderMode = previousMode;
                camera.targetTexture = null;
                Object.Destroy(cameraObject);
                Object.Destroy(readback);
                texture.Release();
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator LargeText_GrowsTheCardRatherThanClippingIt()
        {
            sting.FontScale = 1f;
            sting.Play(CeremonySting.EvictionKind, "Casey Wilson has been evicted.", false);
            yield return null;
            float baseHeight = CardRect().rect.height;
            float baseFont = Headline().fontSize;

            sting.FontScale = 1.2f;
            sting.Play(CeremonySting.EvictionKind, "Casey Wilson has been evicted.", false);
            yield return null;

            Assert.That(Headline().fontSize, Is.GreaterThan(baseFont), "Large text should enlarge the headline.");
            Assert.That(CardRect().rect.height, Is.GreaterThan(baseHeight), "The card should grow with it.");
            Assert.That(Headline().rectTransform.rect.height,
                Is.GreaterThanOrEqualTo(Headline().fontSize * 1.2f),
                "The headline needs a full line box or it renders truncated.");
        }

        private TMP_Text Headline() => sting.GetComponentsInChildren<TMP_Text>(true)
            .Single(label => label.name == "Sting headline");

        private string VisibleText() => string.Join("\n", sting.GetComponentsInChildren<TMP_Text>(true)
            .Where(label => label.gameObject.activeInHierarchy)
            .Select(label => label.text));

        private RectTransform CardRect() => sting.GetComponentsInChildren<RectTransform>(true)
            .Single(rect => rect.name == "Card");
    }
}

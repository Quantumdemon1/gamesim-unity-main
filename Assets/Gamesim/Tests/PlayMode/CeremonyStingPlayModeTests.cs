using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
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
        private Camera frameCamera;
        private RenderTexture frameTexture;

        [SetUp]
        public void CreateSting()
        {
            owner = new GameObject("Ceremony sting owner");
            sting = CeremonySting.Attach(owner);
        }

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            Time.captureDeltaTime = 0f;
            if (sting != null) Object.Destroy(sting.gameObject);
            if (owner != null) Object.Destroy(owner);
            if (frameCamera != null) Object.Destroy(frameCamera.gameObject);
            if (frameTexture != null) { frameTexture.Release(); Object.Destroy(frameTexture); }
            yield return null;
        }

        /// <summary>
        /// Puts the strip's canvas on a frame of its own, <paramref name="width"/> by
        /// <paramref name="height"/> pixels - 1200 by 900 is the 4:3 batch canvas's shape, 1600 by 900
        /// the 16:9 frame - as a capture does: a camera canvas drawn into a texture of that size,
        /// which the scaler lays out at the reference's 900 rows matched equally with its width.
        /// </summary>
        private IEnumerator OnAFrame(int width, int height)
        {
            frameTexture = new RenderTexture(width, height, 24) { name = "Strip frame" };
            frameCamera = new GameObject("Strip frame camera", typeof(Camera)).GetComponent<Camera>();
            frameCamera.clearFlags = CameraClearFlags.SolidColor;
            frameCamera.backgroundColor = Color.black;
            frameCamera.targetTexture = frameTexture;
            var canvas = sting.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = frameCamera;
            canvas.planeDistance = 1f;
            // The scaler lays the canvas out on its own update: a frame or two, on the real clock.
            float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / 1600f, 2f), Mathf.Log(height / 900f, 2f), 0.5f));
            float expected = width / scale, until = Time.realtimeSinceStartup + 2f;
            do
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
            while (Mathf.Abs(((RectTransform)sting.transform).rect.width - expected) > 2f && Time.realtimeSinceStartup < until);
            Assert.That(((RectTransform)sting.transform).rect.width, Is.EqualTo(expected).Within(2f),
                "The strip's canvas is laid out for the " + width + " by " + height + " frame.");
        }

        /// <summary>
        /// A card that has retired leaves its canvas fully transparent, even if the frame it
        /// retired on was a long one.
        ///
        /// <para>The fade-out ends by calling <c>Cancel</c> on the first frame past its end rather
        /// than by drawing a last frame at zero, so whatever alpha the card drew on the frame
        /// before is the alpha it leaves behind. At a steady frame rate that residue is a
        /// thousandth and nothing notices. Cross most of the 0.38 s fade in a single frame - a
        /// hitch, a stalled batchmode frame, a garbage collection - and the card is stranded most
        /// of the way opaque, for the rest of the session, because nothing ever draws it again.</para>
        ///
        /// <para>The card is invisible either way: <c>Cancel</c> deactivates it. The residue only
        /// matters to something that asks the canvas whether a card is still fading, which is
        /// exactly what the ceremony suite does before it photographs a beat - and a canvas parked
        /// at three-quarters alpha means that wait never ends. That was worth about one failed run
        /// in a hundred, a long way from here, blamed on the vote reveal.</para>
        ///
        /// <para>The hitch is made rather than waited for: <c>Time.captureDeltaTime</c> drives
        /// <c>Time.deltaTime</c> but not <c>unscaledDeltaTime</c>, which is the clock these cards
        /// fade on, so the only honest way to produce a long unscaled frame is to spend one.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator Card_LeavesItsCanvasTransparentAfterALongFrame()
        {
            var group = sting.GetComponent<CanvasGroup>();
            sting.Play(CeremonySting.EvictionKind, "Jamie Roberts has been evicted.", false);

            // The entrance and the hold - as long as the strip's words take to read (A10), 3 s for
            // these six - and 0.18 s more is about halfway down the fade-out.
            float midFade = CeremonySting.FadeIn + sting.HoldSeconds + .18f;
            yield return new WaitForSecondsRealtime(midFade);
            Assert.That(sting.IsPlaying, Is.True, "The card should still be fading out at " + midFade.ToString("0.00") + " s.");
            Assert.That(group.alpha, Is.GreaterThan(0.02f).And.LessThan(0.99f),
                "The card should be mid-fade here; that is the state this test strands it from.");

            // One long frame, which carries the rest of the fade-out past its end in a single step.
            System.Threading.Thread.Sleep(400);
            yield return null;
            yield return null;

            Assert.That(sting.IsPlaying, Is.False, "The card should have retired itself by now.");
            Assert.That(group.alpha, Is.EqualTo(0f).Within(0.0001f),
                "A retired card must leave its canvas fully transparent. Its four siblings - the "
                + "takeover, the competition result, the key ceremony and the vote reveal - all zero "
                + "their group in Cancel. This one did not, and what it left behind was invisible on "
                + "screen and permanent to anything asking whether a card is still fading.");
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
                (CeremonySting.VetoKind, "VETO MEETING"),
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
            Assert.That(VisibleText(), Does.Contain("VETO MEETING"));

            // Fade in, the hold its seven words take to read (A10, 3.3 s) and fade out; allow a
            // margin for frame granularity.
            yield return new WaitForSecondsRealtime(CeremonySting.FadeIn + sting.HoldSeconds + CeremonySting.FadeOut + .5f);
            Assert.That(VisibleText(), Is.Empty, "The card should take itself down without being told.");
        }

        /// <summary>
        /// The strip holds long enough to read (PLAN A, A10): its authored two seconds, or 1.2 seconds
        /// and 0.3 a word of its headline and its sentence when that is longer, and half as long again
        /// under the "Longer" caption time. Near the end of its hold it is still up, still takes no
        /// input, and under reduced motion still has not moved; then it goes by itself.
        /// </summary>
        [UnityTest]
        public IEnumerator Sting_HoldsForItsReadingTime_AndHalfAsLongAgainWhenLonger()
        {
            const string sentence = "Maya Hassan nominated Taylor Kim and Jamie Roberts.";
            sting.Play(CeremonySting.NominationKind, sentence, false);
            // "NOMINATION CEREMONY" and eight words: ten, read in 4.2 s, past the authored 2.
            Assert.That(sting.HoldSeconds, Is.EqualTo(1.2f + 0.3f * 10).Within(1e-4f));
            sting.Play(CeremonySting.VetoKind, "", false);
            Assert.That(sting.HoldSeconds, Is.EqualTo(CeremonySting.AuthoredHold).Within(1e-4f),
                "The headline alone reads in 1.8 s: the authored 2 s stands.");
            yield return null;

            sting.CaptionTime = CaptionTime.Longer;
            sting.Play(CeremonySting.NominationKind, sentence, true);
            Assert.That(sting.HoldSeconds, Is.EqualTo((1.2f + 0.3f * 10) * 1.5f).Within(1e-4f), "Longer: half as long again.");
            yield return null;
            var card = CardRect();
            var settled = card.anchoredPosition;
            yield return new WaitForSecondsRealtime(sting.HoldSeconds - .6f);
            Assert.That(sting.IsPlaying, Is.True, "Still up near the end of its reading time.");
            Assert.That(VisibleText(), Does.Contain(sentence));
            Assert.That(card.anchoredPosition, Is.EqualTo(settled), "Reduced motion: it holds where it landed.");
            Assert.That(sting.GetComponent<CanvasGroup>().blocksRaycasts, Is.False, "and it takes no input however long it holds.");
            yield return new WaitForSecondsRealtime(CeremonySting.FadeIn + CeremonySting.FadeOut + 1f);
            Assert.That(sting.IsPlaying, Is.False, "and then it goes by itself.");
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
        /// The headline has to be drawn, not only set. In a box a pixel shorter than Inter's line
        /// TextMesh Pro truncates the whole line, and every sting showed its detail under a band of
        /// empty glass while its text still said NOMINATION CEREMONY - which is all the other
        /// tests here read.
        /// </summary>
        [UnityTest]
        public IEnumerator Play_DrawsTheHeadlineAtEveryTextSize()
        {
            foreach (float scale in TextScales.All)
            {
                sting.FontScale = scale;
                sting.Play(CeremonySting.NominationKind, "Maya Hassan nominated Taylor Kim.", true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var headline = sting.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Sting headline");
                headline.ForceMeshUpdate(true);
                int drawn = headline.textInfo.characterInfo.Take(headline.textInfo.characterCount).Count(glyph => glyph.isVisible);
                Assert.That(headline.isTextTruncated, Is.False, "At font scale " + scale + " the headline is truncated away.");
                Assert.That(drawn, Is.EqualTo("NOMINATIONCEREMONY".Length), "At font scale " + scale + " the headline draws " + drawn + " glyphs.");
                sting.Cancel();
            }
        }

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

            foreach (float scale in TextScales.All)
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

            // Each size up from the last: the larger text over the standard, the largest over the larger.
            foreach (float scale in TextScales.All.Skip(1))
            {
                sting.FontScale = scale;
                sting.Play(CeremonySting.EvictionKind, "Casey Wilson has been evicted.", false);
                yield return null;

                Assert.That(Headline().fontSize, Is.GreaterThan(baseFont), "Text at " + scale + " should enlarge the headline.");
                Assert.That(CardRect().rect.height, Is.GreaterThan(baseHeight), "The card should grow with it at " + scale + ".");
                Assert.That(Headline().rectTransform.rect.height,
                    Is.GreaterThanOrEqualTo(Headline().fontSize * 1.2f),
                    "The headline needs a full line box or it renders truncated, at " + scale + ".");
                baseHeight = CardRect().rect.height;
                baseFont = Headline().fontSize;
            }
        }

        /// <summary>
        /// The strip on the kit (UI-UX-PASS-PLAN V0, decision 14): every beat leads with its
        /// medallion where the accent bar stood - the veto meeting with its own mark, drawn as
        /// authored, every other beat with a ring of its colour round its glyph - and its name beside
        /// it, every letter drawn and ending inside the card, at every text size on the 4:3 batch
        /// canvas, which narrows the strip to about 525, and on the 16:9 frame. The old bar is gone.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryBeat_LeadsWithItsMedallionAndItsNameBesideItAtEveryTextSize([Values(1200, 1600)] int frameWidth)
        {
            yield return OnAFrame(frameWidth, 900);
            var kinds = new[]
            {
                CeremonySting.NominationKind, CeremonySting.VetoKind, CeremonySting.EvictionKind,
                CeremonySting.WinnerKind, CeremonySting.FinalEvictionKind, CeremonySting.WalkOutKind,
            };
            foreach (float scale in TextScales.All)
            foreach (var kind in kinds)
            {
                sting.FontScale = scale;
                sting.Play(kind, "detail for " + kind, true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                string where = kind + " at font scale " + scale + " on the " + frameWidth + " by 900 frame";
                var rects = sting.GetComponentsInChildren<RectTransform>(true);
                Assert.That(rects.Any(rect => rect.name == "Sting rule"), Is.False, where + ": the accent bar is gone.");
                var card = CardRect();
                var box = card.rect;
                var head = Headline();

                var mark = InCard(card, rects.Single(rect => rect.name == "Sting mark"));
                Assert.That(mark.xMin >= box.xMin && mark.yMin >= box.yMin - .5f && mark.yMax <= box.yMax + .5f, Is.True,
                    where + ": the medallion " + mark + " stands inside the card " + box + ".");
                Assert.That(mark.xMax, Is.LessThanOrEqualTo(InCard(card, head.rectTransform).xMin + .5f), where + ": the medallion stands left of the name.");

                head.ForceMeshUpdate(true);
                int drawn = head.textInfo.characterInfo.Take(head.textInfo.characterCount).Count(glyph => glyph.isVisible);
                Assert.That(drawn, Is.EqualTo(head.text.Count(letter => !char.IsWhiteSpace(letter))), where + ": every letter of the name is drawn.");
                float end = card.InverseTransformPoint(head.rectTransform.TransformPoint(head.textBounds.max)).x;
                Assert.That(end, Is.LessThanOrEqualTo(box.xMax + .5f), where + ": the name ends inside the card, at " + end.ToString("0") + " of " + box.xMax.ToString("0") + ".");

                var ring = rects.Single(rect => rect.name == "Mark ring").GetComponent<Image>();
                var glyph = rects.Single(rect => rect.name == "Mark glyph").GetComponent<Image>();
                var authored = CeremonySting.AuthoredMark(kind);
                if (authored != null)
                {
                    Assert.That(ring.enabled && ring.sprite == authored, Is.True, where + ": the veto meeting leads with its own mark,");
                    Assert.That(ring.color, Is.EqualTo(Color.white), "drawn as authored,");
                    Assert.That(glyph.enabled, Is.False, "and nothing over it.");
                }
                else
                {
                    Assert.That(ring.enabled && ring.sprite != null, Is.True, where + ": a ring,");
                    Assert.That(ring.color, Is.EqualTo(head.color), where + ": in the beat's colour, as its name is.");
                }
                sting.Cancel();
            }
        }

        /// <summary>
        /// The longest sentence a veto meeting's strip says, from the cast's three longest names -
        /// "{holder} saves {saved}; {replacement} is the replacement nominee.", the engine's own
        /// shape - said whole at every text size on the 4:3 batch canvas and the 16:9 frame: on two
        /// lines where one will not hold it, in a box 1.3 times its type a line, every letter drawn
        /// (UI-UX-PASS-PLAN V0 review: at 4:3 and the larger text it was cut after "is the" while the
        /// status line that used to carry it stood down). A short sentence keeps the one line.
        ///
        /// <para>At the largest text (the owner's contract of 2026-10-15) the sentence keeps the
        /// largest text's own size, 27 points, and the strip takes the lines it needs, each frame's
        /// count asserted as measured: four on the 4:3 canvas, where the words have 381 beside the
        /// medallion, and two on the 16:9 frame, where they have 595. Held to one line or two on the
        /// 4:3 canvas, it shrank to the shrink's floor and still wrapped to a third line, at about
        /// the standard text's size, in a box made for two.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheLongestVetoSentence_IsSaidWholeAtEveryTextSize([Values(1200, 1600)] int frameWidth)
        {
            yield return OnAFrame(frameWidth, 900);
            var names = CastTemplates.Everyone.Select(template => template.Name).OrderByDescending(name => name.Length).Take(3).ToArray();
            string longest = names[0] + " saves " + names[1] + "; " + names[2] + " is the replacement nominee.";
            foreach (float scale in TextScales.All)
            {
                string where = "At font scale " + scale + " on the " + frameWidth + " by 900 frame";
                sting.FontScale = scale;
                sting.Play(CeremonySting.EvictionKind, "Jamie Roberts is evicted.", true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                float oneLine = CardRect().rect.height;
                var shortLine = Detail();
                shortLine.ForceMeshUpdate(true);
                Assert.That(shortLine.textInfo.lineCount, Is.EqualTo(1), where + ": a short line keeps its one line.");

                sting.Play(CeremonySting.VetoKind, longest, true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var detail = Detail();
                detail.ForceMeshUpdate(true);
                int drawn = detail.textInfo.characterInfo.Take(detail.textInfo.characterCount).Count(glyph => glyph.isVisible);
                Assert.That(detail.text, Is.EqualTo(longest));
                Assert.That(detail.isTextTruncated, Is.False, where + ": '" + longest + "' is cut at " + detail.fontSize.ToString("0.#") + " points.");
                Assert.That(drawn, Is.EqualTo(longest.Count(letter => !char.IsWhiteSpace(letter))), where + ": every letter of the sentence is drawn.");
                int lines = detail.textInfo.lineCount;
                if (LargestPages.At(scale))
                {
                    Photograph("ceremony-sting-veto-largest-" + frameWidth + "x900");
                    Assert.That(lines, Is.EqualTo(frameWidth == 1200 ? 4 : 2), where + ": the sentence takes the lines measured for this frame at the largest text.");
                    Assert.That(detail.fontSize, Is.EqualTo(27f), where + ": the sentence is said at the largest text's own size, never shrunk toward the standard's.");
                }
                else Assert.That(lines, Is.InRange(1, 2), where + ": the sentence takes one line or two.");
                Assert.That(detail.rectTransform.rect.height, Is.GreaterThanOrEqualTo(lines * detail.fontSize * 1.3f - .01f),
                    where + ": a box " + detail.rectTransform.rect.height.ToString("0.#") + " high holds " + lines + " lines of " + detail.fontSize.ToString("0.#") + " points.");
                if (lines >= 2)
                    Assert.That(CardRect().rect.height, Is.GreaterThan(oneLine), where + ": the strip grows for the lines past the first rather than cutting them.");
                var box = InCard(CardRect(), detail.rectTransform);
                Assert.That(box.yMin, Is.GreaterThanOrEqualTo(CardRect().rect.yMin - .5f), where + ": the sentence's box stands inside the strip.");
                sting.Cancel();
            }
        }

        /// <summary>
        /// The strip is the largest text's one fit exception (the owner's contract of 2026-10-15): a
        /// timed overlay, it can neither page nor scroll, so at the largest text it grows to the lines
        /// its sentence needs at the largest text's own 27 points, up to
        /// <see cref="CeremonySting.MostLargestLines"/>, and a sentence longer than those shrinks into
        /// them, as one longer than two lines always has at the other sizes. Beside the veto
        /// meeting's (above), the engine's other long sentences, in its own shapes with the cast's
        /// longest names: the final Head of Household's choice (EpisodeEngine.FinalEvict), an
        /// eviction the Head of Household's vote decided (the eviction's line with CountTail's tie,
        /// which the strip says when the vote reveal declines its block) and a juror's goodbye at the
        /// door with the longest first name (EpisodeDirector.GoodbyeLine) - each said whole at the
        /// largest text, every letter drawn, on the lines and at the size measured for each frame.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLongestOtherSentences_AreSaidWholeAtTheLargestText([Values(1200, 1600)] int frameWidth)
        {
            yield return OnAFrame(frameWidth, 900);
            var names = CastTemplates.Everyone.Select(template => template.Name).OrderByDescending(name => name.Length).Take(3).ToArray();
            string first = CastTemplates.Everyone.Select(template => template.Name.Split(' ')[0]).OrderByDescending(name => name.Length).First();
            var sentences = new[]
            {
                (kind: CeremonySting.FinalEvictionKind, words: names[0] + " takes " + names[1] + " to the final two. " + names[2] + " joins the jury."),
                (kind: CeremonySting.EvictionKind, words: names[0] + " is evicted and joins the jury. By a vote of 6 to 6; " + names[1] + " broke the tie."),
                (kind: CeremonySting.WalkOutKind, words: first + " pauses at the door and turns to you… They'll be waiting in the jury house."),
            };
            // Measured on each frame at the largest text (lines, points): the 4:3 canvas gives the words
            // 381 beside the medallion, the 16:9 frame 595. None of them shrinks.
            var expected = frameWidth == 1200
                ? new[] { (lines: 4, size: 27f), (lines: 4, size: 27f), (lines: 3, size: 27f) }
                : new[] { (lines: 2, size: 27f), (lines: 3, size: 27f), (lines: 2, size: 27f) };
            sting.FontScale = TextSizes.Scale(TextSize.Largest);
            var measured = new (int lines, float size, bool cut, int drawn, float box, bool grew, bool inside)[sentences.Length];
            sting.Play(CeremonySting.EvictionKind, "Jamie Roberts is evicted.", true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            float oneLine = CardRect().rect.height;
            for (int i = 0; i < sentences.Length; i++)
            {
                sting.Play(sentences[i].kind, sentences[i].words, true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var detail = Detail();
                detail.ForceMeshUpdate(true);
                int lines = detail.textInfo.lineCount;
                measured[i] = (lines, detail.fontSize, detail.isTextTruncated,
                    detail.textInfo.characterInfo.Take(detail.textInfo.characterCount).Count(glyph => glyph.isVisible),
                    detail.rectTransform.rect.height, CardRect().rect.height > oneLine,
                    InCard(CardRect(), detail.rectTransform).yMin >= CardRect().rect.yMin - .5f);
                Debug.Log("[Gamesim] strip at the largest text on the " + frameWidth + " by 900 frame: '" + sentences[i].words + "' takes "
                    + lines + " lines at " + detail.fontSize.ToString("0.##") + " points, the strip " + CardRect().rect.height.ToString("0.#") + " tall.");
                Photograph("ceremony-sting-" + sentences[i].kind + "-largest-" + frameWidth + "x900");
                sting.Cancel();
            }
            for (int i = 0; i < sentences.Length; i++)
            {
                string where = "At the largest text on the " + frameWidth + " by 900 frame, '" + sentences[i].words + "'";
                var m = measured[i];
                Assert.That(m.cut, Is.False, where + " is cut at " + m.size.ToString("0.##") + " points.");
                Assert.That(m.drawn, Is.EqualTo(sentences[i].words.Count(letter => !char.IsWhiteSpace(letter))), where + ": every letter is drawn.");
                Assert.That(m.lines, Is.InRange(1, CeremonySting.MostLargestLines), where + " takes " + m.lines + " lines, no more than the strip's most.");
                Assert.That(m.box, Is.GreaterThanOrEqualTo(m.lines * m.size * 1.3f - .01f),
                    where + ": a box " + m.box.ToString("0.#") + " high holds " + m.lines + " lines of " + m.size.ToString("0.##") + " points.");
                Assert.That(m.inside, Is.True, where + ": the sentence's box stands inside the strip.");
                if (m.lines >= 2) Assert.That(m.grew, Is.True, where + ": the strip grows for the lines past the first rather than cutting them.");
                Assert.That((m.lines, m.size), Is.EqualTo(expected[i]),
                    where + " takes " + m.lines + " lines at " + m.size.ToString("0.##") + " points, measured " + expected[i].lines + " at " + expected[i].size + ".");
            }
        }

        private TMP_Text Detail() => sting.GetComponentsInChildren<TMP_Text>(true).Single(label => label.name == "Sting detail");

        /// <summary>
        /// The strip as the frame's camera draws it (<see cref="OnAFrame"/>), into <c>{name}.png</c>
        /// beside the project, for a person to read; the headless harness only, as the card's own
        /// render is. The active render target is put back.
        /// </summary>
        private void Photograph(string name)
        {
            if (!Application.isBatchMode || frameCamera == null || frameTexture == null) return;
            var readback = new Texture2D(frameTexture.width, frameTexture.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases();
                frameCamera.Render();
                RenderTexture.active = frameTexture;
                readback.ReadPixels(new Rect(0, 0, frameTexture.width, frameTexture.height), 0, 0);
                readback.Apply();
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", name + ".png"));
                System.IO.File.WriteAllBytes(path, readback.EncodeToPNG());
                Debug.Log("[Gamesim] strip capture -> " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }

        /// <summary>A rect's box in the card's own space.</summary>
        private static Rect InCard(RectTransform card, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var low = card.InverseTransformPoint(corners[0]);
            var high = card.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The creator's pages as the mockups lay them out: every control inside the frame and clear
    /// of the footer, at standard and larger text, and a colour swatch writing the colour it shows.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CreatorPreviewReadinessObserver creatorPreviewObservation;
        private static readonly string[] CreatorPages = { "Appearance", "Identity", "Personality", "My Houseguests", "Review" };
        private static readonly string[] CreatorCategories = { "Body", "Face", "Hair", "Clothing", "Accessories", "Colors" };

        [UnityTest]
        public IEnumerator Creator_EveryPageFitsTheFrameAndIsCapturedForReview()
        {
            // Start before OpenCreator: the first build can already have timed out before capture preparation.
            using (creatorPreviewObservation = CreatorPreviewReadinessObserver.Begin(Creator()))
            {
                yield return OpenCreator();
                var creator = Creator();
                // A saved houseguest, so My Houseguests has a card and its bar to lay out.
                var saved = creator.Draft.Copy();
                saved.Name = "Review Guest";
                Assert.That(creator.ProfileStore.Save(CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), saved), out var error), Is.True, error);
                foreach (float scale in TextScales.All)
                {
                    creator.FontScale = scale;
                    yield return null; yield return null;
                    foreach (string page in CreatorPages)
                    {
                        CastButtons(page)[0].onClick.Invoke();
                        yield return null;
                        if (page == "Appearance")
                            foreach (string category in CreatorCategories)
                            {
                                CastButtons(category)[0].onClick.Invoke();
                                yield return null;
                                AssertCreatorFits(creator, page + " / " + category + " at " + scale);
                                if (scale == 1f && Application.isBatchMode) yield return CaptureCreatorFraming("creator-appearance-" + category.ToLowerInvariant());
                            }
                        else
                        {
                            AssertCreatorFits(creator, page + " at " + scale);
                            if (scale == 1f && Application.isBatchMode)
                                yield return CaptureCreatorFraming("creator-" + page.ToLowerInvariant().Replace(' ', '-'),
                                    expectPreview: page == "Identity" || page == "Review");
                        }
                    }
                }
                creator.FontScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator Creator_PersonalityShowsItsOverflowAtEveryTextSizeWithoutNarrowingTheCards()
        {
            yield return OpenCreator();
            var creator = Creator();
            try
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                    new Vector2Int(1200, 900), new Vector2Int(2560, 1080) })
                    using (var lens = new CaptureLens(cameraRig.ViewCamera, size.x, size.y))
                        foreach (float scale in TextScales.All)
                        {
                            creator.FontScale = scale;
                            yield return null; yield return null;
                            CastButtons("Personality").Single().onClick.Invoke();
                            yield return null; yield return null;
                            Canvas.ForceUpdateCanvases();
                            string where = "Personality " + size + " at " + scale;
                            var scrolls = creator.GetComponentsInChildren<UnityEngine.UI.ScrollRect>()
                                .Where(scroll => scroll.content.name == "Traits" || scroll.content.name == "Tendency rows").ToArray();
                            Assert.That(scrolls.Any(scroll => scroll.content.name == "Traits"), Is.True, where);
                            if (LargestPages.At(scale))
                                Assert.That(scrolls, Has.Length.EqualTo(2), where + ": both lists overflow on these frames.");
                            foreach (var scroll in scrolls)
                            {
                                var panel = (RectTransform)scroll.viewport.parent;
                                Assert.That(scroll.viewport.rect.width, Is.EqualTo(panel.rect.width - 48f).Within(.1f),
                                    where + ": the original 24-unit margins and card width stay unchanged.");
                                Assert.That(scroll.content.rect.width, Is.EqualTo(scroll.viewport.rect.width).Within(.1f), where);
                                AssertCreatorScrollIndicator(creator, scroll, where);
                                if (scroll.content.rect.height > scroll.viewport.rect.height + .01f)
                                {
                                    float more = scroll.content.rect.height - scroll.viewport.rect.height;
                                    scroll.verticalScrollbar.value = 0f;
                                    yield return null;
                                    Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(more).Within(1f), where + ": bar reaches the end.");
                                    scroll.verticalScrollbar.value = 1f;
                                    yield return null;
                                    Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0f).Within(1f), where + ": bar returns to the top.");
                                }
                            }
                            AssertCreatorFits(creator, where);
                            if (LargestPages.At(scale))
                                yield return PhotographCreatorPersonalityThroughLens(lens,
                                    "creator-personality-scroll-" + size.x + "x" + size.y + "-largest", expectPhoto: size.x == 2560);
                        }
            }
            finally { creator.FontScale = 1f; }
        }

        [UnityTest]
        public IEnumerator Creator_SharedIndicatorsHideForFittingContentAndTabReachesTheLastTrait()
        {
            yield return OpenCreator();
            var creator = Creator();
            try
            {
                using (var lens = new CaptureLens(cameraRig.ViewCamera, 1200, 900))
                {
                    Assert.That(creator.ProfileStore.List(), Is.Empty, "This test uses the fresh isolated profile library.");
                    foreach (float scale in TextScales.All)
                    {
                        creator.FontScale = scale;
                        yield return null; yield return null;
                        CastButtons("My Houseguests").Single().onClick.Invoke();
                        yield return null; yield return null;
                        var fitting = creator.GetComponentsInChildren<UnityEngine.UI.ScrollRect>()
                            .Single(scroll => scroll.content.name == "Profile cards");
                        Assert.That(fitting.content.rect.height, Is.LessThan(fitting.viewport.rect.height), "Empty library at " + scale);
                        AssertCreatorScrollIndicator(creator, fitting, "Empty library at " + scale);
                        Assert.That(fitting.verticalScrollbar.gameObject.activeInHierarchy, Is.False, "No false overflow cue at " + scale);
                        CastButtons("Personality").Single().onClick.Invoke();
                        yield return null; yield return null;
                        var overflow = creator.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Single(scroll => scroll.content.name == "Traits");
                        Assert.That(overflow.content.rect.height, Is.GreaterThan(overflow.viewport.rect.height + .01f), "Actual overflow at " + scale);
                        AssertCreatorScrollIndicator(creator, overflow, "Traits at " + scale);
                        Assert.That(overflow.verticalScrollbar.gameObject.activeInHierarchy, Is.True, "An overflowing list says so at " + scale);
                    }
                    creator.FontScale = TextSizes.Scale(TextSize.Largest);
                    yield return null; yield return null;
                    CastButtons("My Houseguests").Single().onClick.Invoke();
                    yield return null; yield return null;
                    foreach (string page in new[] { "Identity", CharacterCreator.QuickCaption })
                    {
                        CastButtons(page).Single().onClick.Invoke();
                        yield return null; yield return null;
                        var scroll = creator.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Single();
                        AssertCreatorScrollIndicator(creator, scroll, page);
                        Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True, page + ": this actual form overflows.");
                    }
                    CastButtons(CharacterCreator.DetailedCaption).Single().onClick.Invoke();
                    yield return null; yield return null;
                    CastButtons("Personality").Single().onClick.Invoke();
                    yield return null; yield return null;
                    var traits = creator.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Single(scroll => scroll.content.name == "Traits");
                    var buttons = traits.content.GetComponentsInChildren<UnityEngine.UI.Button>().Where(button => button.IsInteractable()).ToArray();
                    Assert.That(buttons.Length, Is.EqualTo(WebTraits.Boosts.Count), "Every actual trait card is on the keyboard route.");
                    var last = buttons.Last();
                    Assert.That(OwnBox(traits.viewport, last.transform).min.y, Is.LessThan(traits.viewport.rect.yMin),
                        "The final trait starts clipped; reaching it must really scroll the list.");
                    var beforeTraits = creator.Draft.Traits.ToArray();
                    var beforeStats = WebTraits.StatNames.Select(stat => WebTraits.Get(creator.Draft.Stats, stat)).ToArray();
                    buttons[0].Select();
                    yield return null;
                    int limit = creator.GetComponentsInChildren<UnityEngine.UI.Selectable>().Length + 1;
                    var events = UnityEngine.EventSystems.EventSystem.current;
                    for (int step = 0; step < limit && events.currentSelectedGameObject != last.gameObject; step++)
                    {
                        yield return PressKey(UnityEngine.InputSystem.Key.Tab);
                        yield return null;
                        Assert.That(events.currentSelectedGameObject?.GetComponent<UnityEngine.UI.Scrollbar>(), Is.Null,
                            "The indicator never becomes an extra keyboard stop.");
                    }
                    Assert.That(events.currentSelectedGameObject, Is.SameAs(last.gameObject), "Tab reaches the last actual trait.");
                    var visible = OwnBox(traits.viewport, last.transform);
                    Assert.That(visible.min.y, Is.GreaterThanOrEqualTo(traits.viewport.rect.yMin - 1f), "Selected trait is above the clip edge.");
                    Assert.That(visible.max.y, Is.LessThanOrEqualTo(traits.viewport.rect.yMax + 1f), "Selected trait is below the top edge.");
                    Assert.That(traits.content.anchoredPosition.y, Is.GreaterThan(1f), "Keyboard focus moved the content.");
                    Assert.That(traits.verticalScrollbar.value, Is.LessThan(.99f), "The indicator follows the keyboard's scroll.");
                    AssertCreatorTraitInk(last, traits.viewport, lens.Lens, "Tab reaches every glyph of the last trait");
                    Assert.That(creator.Draft.Traits, Is.EqualTo(beforeTraits), "Navigation chooses no trait.");
                    Assert.That(WebTraits.StatNames.Select(stat => WebTraits.Get(creator.Draft.Stats, stat)), Is.EqualTo(beforeStats),
                        "Navigation spends no stat points.");
                }
            }
            finally { creator.FontScale = 1f; }
        }

        [UnityTest]
        public IEnumerator Creator_TendencyButtonsAreDrawnWholeAndReachableAtEveryScaleAndFrame()
        {
            yield return OpenCreator();
            var creator = Creator();
            var original = creator.Draft.Copy();
            try
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                    new Vector2Int(1200, 900), new Vector2Int(2560, 1080) })
                    using (var lens = new CaptureLens(cameraRig.ViewCamera, size.x, size.y))
                        // Largest first: the red oracle is the observed circle/mask clipping,
                        // before any capture readiness checks on a fitting summary page.
                        foreach (float scale in TextScales.All.Reverse())
                        {
                            creator.FontScale = scale;
                            yield return null; yield return null;
                            CastButtons("Personality").Single().onClick.Invoke();
                            yield return null; yield return null;
                            var panel = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Tendencies");
                            var scroll = panel.GetComponentsInChildren<ScrollRect>().SingleOrDefault();
                            var clip = scroll == null ? panel : scroll.viewport;
                            var buttons = panel.GetComponentsInChildren<Button>();
                            string where = "Tendency circles " + size + " at " + scale;
                            Assert.That(buttons, Has.Length.EqualTo(16), where + ": all eight unchanged pairs exist.");
                            foreach (string stat in WebTraits.StatNames)
                                foreach (string caption in new[] { "Lower " + stat, "Raise " + stat })
                                {
                                    var button = buttons.Single(item => item.name == caption);
                                    Assert.That(button.GetComponentsInChildren<TMPro.TMP_Text>(true)
                                        .Single(label => label.name == "Label").text, Is.EqualTo(caption),
                                        where + ": the accessible command caption is unchanged.");
                                }
                            if (scroll != null)
                            {
                                Assert.That(scroll.content.name, Is.EqualTo("Tendency rows"), where);
                                Assert.That(clip.rect.width, Is.EqualTo(panel.rect.width - 48f).Within(.1f),
                                    where + ": the viewport keeps both 24-unit margins.");
                                Assert.That(scroll.content.rect.width, Is.EqualTo(clip.rect.width).Within(.1f), where);
                                scroll.StopMovement();
                                scroll.verticalNormalizedPosition = 1f;
                            }
                            var traitScroll = creator.GetComponentsInChildren<ScrollRect>().Single(item => item.content.name == "Traits");
                            Assert.That(traitScroll.viewport.rect.width,
                                Is.EqualTo(((RectTransform)traitScroll.viewport.parent).rect.width - 48f).Within(.1f), where);
                            Assert.That(traitScroll.content.rect.width, Is.EqualTo(traitScroll.viewport.rect.width).Within(.1f), where);
                            var first = buttons.Single(button => button.name == "Raise physical");
                            var last = buttons.Single(button => button.name == "Raise loyalty");
                            Assert.That(first.IsInteractable() && last.IsInteractable(), Is.True, where);
                            first.Select();
                            yield return null; yield return null;
                            yield return lens.MakeSureTheCanvasesAreDrawn();
                            Canvas.ForceUpdateCanvases();
                            // This checks the drawn circle before any new layout policy. The original
                            // scrolling branch places its first button seven units above the mask.
                            AssertCreatorTendencyRowInk(buttons, "physical", clip, where + " / first");
                            AssertCreatorFits(creator, where);
                            string capture = "creator-tendency-buttons-" + size.x + "x" + size.y
                                + (Mathf.Approximately(scale, 1f) ? "-standard" : TextScales.Suffix(scale));
                            bool expectPhoto = size.x == 2560 || Mathf.Approximately(scale, 1f)
                                || (size.x != 1200 && scale < 1.3f);
                            yield return PhotographCreatorPersonalityThroughLens(lens, capture + "-first", expectPhoto);

                            var events = UnityEngine.EventSystems.EventSystem.current;
                            int limit = creator.GetComponentsInChildren<Selectable>().Length + 1;
                            for (int step = 0; step < limit && events.currentSelectedGameObject != last.gameObject; step++)
                            {
                                yield return PressKey(UnityEngine.InputSystem.Key.Tab);
                                yield return null;
                                Assert.That(events.currentSelectedGameObject?.GetComponent<Scrollbar>(), Is.Null,
                                    where + ": the scrollbar is not an extra keyboard stop.");
                            }
                            Assert.That(events.currentSelectedGameObject, Is.SameAs(last.gameObject),
                                where + ": actual Tab navigation reaches the final tendency.");
                            yield return null; yield return null;
                            yield return lens.MakeSureTheCanvasesAreDrawn();
                            Canvas.ForceUpdateCanvases();
                            AssertCreatorTendencyRowInk(buttons, "loyalty", clip, where + " / last");
                            if (scroll != null)
                            {
                                Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(1f),
                                    where + ": reaching the final pair really scrolls.");
                                Assert.That(scroll.verticalScrollbar.value, Is.LessThan(.99f), where);
                            }
                            yield return PhotographCreatorPersonalityThroughLens(lens, capture + "-last", expectPhoto);
                            // Returning to the top must not leave the first circle or its focus ring clipped.
                            events.SetSelectedGameObject(null);
                            if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
                            first.Select();
                            yield return null; yield return null;
                            yield return lens.MakeSureTheCanvasesAreDrawn();
                            Canvas.ForceUpdateCanvases();
                            AssertCreatorTendencyRowInk(buttons, "physical", clip, where + " / return");
                            AssertCreatorEntryDraft(original, creator.Draft);
                        }
            }
            finally { creator.FontScale = 1f; }
            AssertCreatorEntryDraft(original, creator.Draft);
        }

        private static void AssertCreatorTendencyRowInk(Button[] buttons, string stat, RectTransform clip, string where)
        {
            foreach (string verb in new[] { "Lower ", "Raise " })
            {
                var button = buttons.Single(item => item.name == verb + stat);
                var rectangle = (RectTransform)button.transform;
                Assert.That(rectangle.rect.width, Is.EqualTo(40f).Within(.01f), where + ": hit-target width.");
                Assert.That(rectangle.rect.height, Is.EqualTo(40f).Within(.01f), where + ": hit-target height.");
                var circle = button.targetGraphic as Image;
                Assert.That(circle, Is.Not.Null, where + ": the actual circular target is present.");
                AssertCreatorTendencyImageInk(circle, clip, where + " / " + button.name + " / circle");
                var border = button.GetComponentsInChildren<Image>(true).Single(image => image.name == "Border");
                AssertCreatorTendencyImageInk(border, clip, where + " / " + button.name + " / normal border");
                var glyph = button.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == "Glyph");
                AssertCreatorTraitTextInk(glyph, verb == "Raise " ? "+" : "−", rectangle, clip,
                    where + " / " + button.name + " / command glyph");
                var focus = button.GetComponentsInChildren<Image>(true).Single(image => image.name == "Focus ring");
                bool selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == button.gameObject;
                Assert.That(focus.isActiveAndEnabled, Is.EqualTo(selected), where + ": focus follows real selection.");
                if (selected) AssertCreatorTendencyImageInk(focus, clip, where + " / " + button.name + " / focus ring");
            }
        }

        private static void AssertCreatorTendencyImageInk(Image image, RectTransform clip, string where)
        {
            Assert.That(image.isActiveAndEnabled && !image.canvasRenderer.cull && image.color.a > 0f, Is.True,
                where + ": the actual graphic is drawn.");
            Assert.That(image.sprite, Is.Not.Null, where);
            var mesh = image.canvasRenderer.GetMesh(); // Borrowed geometry: never mutate or destroy it.
            Assert.That(mesh, Is.Not.Null, where + ": a rect alone is not drawn-ink evidence.");
            Assert.That(mesh.isReadable, Is.True, where);
            Assert.That(mesh.vertexCount, Is.InRange(4, 4096), where);
            var vertices = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);
            Assert.That(vertices, Has.Count.EqualTo(mesh.vertexCount), where);
            foreach (Vector3 vertex in vertices)
            {
                Vector3 point = clip.InverseTransformPoint(image.transform.TransformPoint(vertex));
                Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x)
                    || float.IsNaN(point.y) || float.IsInfinity(point.y), Is.False, where + ": finite mesh.");
                Assert.That(point.x, Is.InRange(clip.rect.xMin - .25f, clip.rect.xMax + .25f),
                    where + ": complete drawn geometry clears the side mask.");
                Assert.That(point.y, Is.InRange(clip.rect.yMin - .25f, clip.rect.yMax + .25f),
                    where + ": complete drawn geometry clears the top/bottom mask.");
            }
        }

        [UnityTest]
        public IEnumerator Creator_TraitDescriptionsAreDrawnWholeAndReadableAtEveryScaleAndFrame()
        {
            yield return OpenCreator();
            var creator = Creator();
            var original = creator.Draft.Copy();
            try
            {
                // First reproduce the photographed defect, before imposing the new font-size policy.
                // This literal first-card effect must clear its border at Largest on the 720p frame.
                using (var baselineLens = new CaptureLens(cameraRig.ViewCamera, 1280, 720))
                {
                    creator.FontScale = TextSizes.Scale(TextSize.Largest);
                    yield return null; yield return null;
                    CastButtons("Personality").Single().onClick.Invoke();
                    yield return null; yield return null;
                    var baselineScroll = creator.GetComponentsInChildren<ScrollRect>().Single(item => item.content.name == "Traits");
                    var competitive = baselineScroll.content.GetComponentsInChildren<Button>().Single(button => button.name == "Competitive");
                    competitive.Select();
                    yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    var effect = competitive.GetComponentsInChildren<TMPro.TMP_Text>().Single(label => label.name == "Trait effect");
                    AssertCreatorTraitTextInk(effect, "+2 Physical  ·  +1 Endurance", (RectTransform)competitive.transform,
                        baselineScroll.viewport, "Photographed baseline: 1280x720 Largest / Competitive effect");
                }
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                    new Vector2Int(1200, 900), new Vector2Int(2560, 1080) })
                    using (var lens = new CaptureLens(cameraRig.ViewCamera, size.x, size.y))
                    {
                        var standardGlyphHeights = new Dictionary<string, float>();
                        foreach (float scale in TextScales.All)
                        {
                            creator.FontScale = scale;
                            yield return null; yield return null;
                            CastButtons("Personality").Single().onClick.Invoke();
                            yield return null; yield return null;
                            Canvas.ForceUpdateCanvases();
                            string where = "Trait ink " + size + " at " + scale;
                            var scroll = creator.GetComponentsInChildren<ScrollRect>().Single(item => item.content.name == "Traits");
                            var cards = scroll.content.GetComponentsInChildren<Button>().Where(button => button.IsInteractable()).ToArray();
                            Assert.That(cards.Select(card => card.name), Is.EquivalentTo(WebTraits.Boosts.Keys), where);
                            foreach (var card in cards)
                            {
                                // Follow the real focus scroller. Hidden rows are not accepted merely because
                                // their control rectangles exist: every actual letter must reach the viewport.
                                card.Select();
                                yield return null; yield return null;
                                Canvas.ForceUpdateCanvases();
                                float glyphHeight = AssertCreatorTraitInk(card, scroll.viewport, lens.Lens, where);
                                if (Mathf.Approximately(scale, 1f)) standardGlyphHeights.Add(card.name, glyphHeight);
                                else Assert.That(glyphHeight / standardGlyphHeights[card.name], Is.EqualTo(scale).Within(.08f),
                                    where + " / " + card.name + ": the drawn plus glyph magnifies once with the canvas.");
                            }
                            AssertCreatorEntryDraft(original, creator.Draft);
                            string capture = "creator-personality-text-" + size.x + "x" + size.y
                                + (Mathf.Approximately(scale, 1f) ? "-standard" : TextScales.Suffix(scale));
                            // This is the authored four-frame matrix, not an observed-child shortcut:
                            // the 4:3 summary fits only Standard; 16:9 fits Standard/Larger; 21:9 fits all.
                            bool expectPhoto = size.x == 2560 || Mathf.Approximately(scale, 1f)
                                || (size.x != 1200 && scale < 1.3f);
                            if (LargestPages.At(scale))
                                yield return PhotographCreatorPersonalityThroughLens(lens, capture + "-last", expectPhoto);
                            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                            scroll.verticalNormalizedPosition = 1f;
                            cards[0].Select();
                            yield return null; yield return null;
                            AssertCreatorTraitInk(cards[0], scroll.viewport, lens.Lens, where + " / return to top");
                            yield return PhotographCreatorPersonalityThroughLens(lens, capture, expectPhoto);
                        }
                    }
            }
            finally { creator.FontScale = 1f; }
            AssertCreatorEntryDraft(original, creator.Draft);
        }

        /// <summary>Each literal character's generated quad, not only its RectTransform or the card's control bounds.</summary>
        private static float AssertCreatorTraitInk(Button card, RectTransform viewport, Camera eye, string where)
        {
            string trait = card.name;
            where += " / " + trait;
            var labels = card.GetComponentsInChildren<TMPro.TMP_Text>();
            var caption = labels.Single(label => label.name == "Label");
            var effect = labels.Single(label => label.name == "Trait effect");
            var boost = WebTraits.Boosts[trait];
            string primary = char.ToUpperInvariant(boost.Primary[0]) + boost.Primary.Substring(1);
            string secondary = char.ToUpperInvariant(boost.Secondary[0]) + boost.Secondary.Substring(1);
            string reception = TraitAffinity.Reception.TryGetValue(trait, out int liked)
                ? (liked > 0 ? "  ·  liked" : "  ·  disliked") : "";
            string expected = "+" + WebTraits.PrimaryBoost + " " + primary + "  ·  +"
                + WebTraits.SecondaryBoost + " " + secondary + reception;
            AssertCreatorTraitTextInk(caption, trait, (RectTransform)card.transform, viewport, where);
            AssertCreatorTraitTextInk(effect, expected, (RectTransform)card.transform, viewport, where);
            Assert.That(effect.fontSize, Is.GreaterThanOrEqualTo(14f), where + ": effects may wrap, not shrink below body copy.");
            Assert.That(effect.enableAutoSizing, Is.False, where + ": shrinking must not hide a missing line budget.");
            Assert.That(effect.rectTransform.rect.height,
                Is.GreaterThanOrEqualTo(effect.GetPreferredValues(effect.text, effect.rectTransform.rect.width, 0f).y + 3f),
                where + ": the actual font's longest wrapped effect has a measured line budget.");
            var first = effect.textInfo.characterInfo.Take(effect.textInfo.characterCount).First(glyph => glyph.isVisible);
            Assert.That(first.character, Is.EqualTo('+'), where);
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (var corner in new[] { first.bottomLeft, first.topLeft, first.topRight, first.bottomRight })
            {
                var screen = eye.WorldToScreenPoint(effect.transform.TransformPoint(corner));
                Assert.That(screen.z, Is.GreaterThan(0f), where + ": ink is in front of the capture lens.");
                bottom = Mathf.Min(bottom, screen.y); top = Mathf.Max(top, screen.y);
            }
            Assert.That(top - bottom, Is.GreaterThan(0f), where + ": the drawn glyph has height.");
            return top - bottom;
        }

        private static void AssertCreatorTraitTextInk(TMPro.TMP_Text text, string expected, RectTransform card,
            RectTransform viewport, string where)
        {
            text.ForceMeshUpdate();
            Assert.That(text.text, Is.EqualTo(expected), where + ": no shortened trait or effect caption.");
            Assert.That(text.isTextTruncated, Is.False, where + " / " + text.name);
            Assert.That(text.isTextOverflowing, Is.False, where + " / " + text.name);
            Assert.That(text.isActiveAndEnabled && !text.canvasRenderer.cull && text.color.a > .5f, Is.True, where + ": visible ink.");
            var drawn = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(glyph => glyph.isVisible).ToArray();
            Assert.That(new string(drawn.Select(glyph => glyph.character).ToArray()),
                Is.EqualTo(new string(expected.Where(character => !char.IsWhiteSpace(character)).ToArray())),
                where + " / " + text.name + ": every non-space character has an actual generated quad.");
            foreach (var glyph in drawn)
                foreach (var corner in new[] { glyph.bottomLeft, glyph.topLeft, glyph.topRight, glyph.bottomRight })
                {
                    Vector3 world = text.transform.TransformPoint(corner);
                    Vector3 onCard = card.InverseTransformPoint(world);
                    Vector3 inList = viewport.InverseTransformPoint(world);
                    Assert.That(onCard.x, Is.InRange(card.rect.xMin + 4f, card.rect.xMax - 4f),
                        where + " / " + text.name + ": glyph clears the side borders.");
                    Assert.That(onCard.y, Is.InRange(card.rect.yMin + 4f, card.rect.yMax - 4f),
                        where + " / " + text.name + ": glyph clears the top and bottom borders.");
                    Assert.That(inList.x, Is.InRange(viewport.rect.xMin - .75f, viewport.rect.xMax + .75f),
                        where + " / " + text.name + ": selected glyph is inside the horizontal clip.");
                    Assert.That(inList.y, Is.InRange(viewport.rect.yMin - .75f, viewport.rect.yMax + .75f),
                        where + " / " + text.name + ": selected glyph is inside the vertical clip.");
                }
        }

        private static void AssertCreatorScrollIndicator(CharacterCreator creator, UnityEngine.UI.ScrollRect scroll, string where)
        {
            Canvas.ForceUpdateCanvases();
            var bar = scroll.verticalScrollbar;
            Assert.That(bar, Is.Not.Null, where + ": the shared scrolling area has an indicator.");
            Assert.That(bar.name, Is.EqualTo(scroll.content.name + " scrollbar"), where);
            Assert.That(bar.navigation.mode, Is.EqualTo(UnityEngine.UI.Navigation.Mode.None), where + ": no keyboard stop.");
            Assert.That(scroll.verticalScrollbarVisibility, Is.EqualTo(UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide), where);
            bool overflow = scroll.content.rect.height > scroll.viewport.rect.height + .01f;
            Assert.That(bar.gameObject.activeInHierarchy, Is.EqualTo(overflow), where + ": visibility follows actual content bounds.");
            if (!overflow) return;
            var root = (RectTransform)creator.transform;
            var list = OwnBox(root, scroll.viewport);
            var indicator = OwnBox(root, bar.transform);
            var panel = OwnBox(root, scroll.viewport.parent);
            Assert.That(indicator.min.x, Is.GreaterThanOrEqualTo(list.max.x + 3.5f), where + ": outside the content mask.");
            Assert.That(indicator.max.x, Is.LessThanOrEqualTo(panel.max.x), where + ": inside the existing panel margin.");
            Assert.That(indicator.max.y, Is.EqualTo(list.max.y).Within(.5f), where);
            Assert.That(indicator.size.y, Is.EqualTo(list.size.y).Within(.5f), where);
            Assert.That(bar.size, Is.GreaterThan(0f).And.LessThan(1f), where + ": the handle communicates overflow.");
            Assert.That(bar.targetGraphic.color.a, Is.GreaterThan(0f), where + ": visible ink.");
        }

        private IEnumerator CaptureCreatorFraming(string name, bool expectPreview = true, int width = 1600, int height = 900,
            System.Action arrange = null)
        {
            yield return CaptureFraming(name, width: width, height: height, arrange: arrange,
                prepare: expectPreview ? () => PrepareCreatorFrame(name) : (System.Func<IEnumerator>)null,
                inspect: expectPreview ? frame => AssertCreatorFrameReady(name) : (System.Action<Texture2D>)null);
        }

        [UnityTest]
        public IEnumerator Creator_CapturePreparationFailureRestoresLensAndCanvases()
        {
            yield return OpenCreator();
            var target = cameraRig.ViewCamera.targetTexture;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (Canvas: canvas, Mode: canvas.renderMode, Camera: canvas.worldCamera, Distance: canvas.planeDistance)).ToArray();
            Assert.That(canvases, Is.Not.Empty, "An actual creator overlay must be handed to the lens.");
            const string failure = "Intentional creator preparation failure";
            var capture = CaptureFraming("creator-preparation-failure", settle: false, prepare: () => FailCreatorPreparation(failure));
            bool caught = false;
            try
            {
                while (true)
                {
                    bool moved;
                    try { moved = capture.MoveNext(); }
                    catch (System.InvalidOperationException error)
                    {
                        Assert.That(error.Message, Is.EqualTo(failure));
                        caught = true;
                        break;
                    }
                    if (!moved) break;
                    yield return capture.Current;
                }
                // Check before the caller disposes anything. The preparation's own failure must
                // already have unwound the capture scope, even if the runner stops its parent.
                Assert.That(caught, Is.True, "MoveNext must surface the preparation's actual failure.");
                Assert.That(cameraRig.ViewCamera.targetTexture, Is.SameAs(target), "The ordinary camera target must already be restored.");
                foreach (var canvas in canvases)
                {
                    Assert.That(canvas.Canvas.renderMode, Is.EqualTo(canvas.Mode));
                    Assert.That(canvas.Canvas.worldCamera, Is.SameAs(canvas.Camera));
                    Assert.That(canvas.Canvas.planeDistance, Is.EqualTo(canvas.Distance));
                }
                yield return null; // Unity removes the disposed lens's owned objects at frame end.
                Assert.That(Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(camera => camera.name == CaptureLens.OverlayCameraName), Is.False);
                Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(canvas => canvas.name == CaptureLens.GuardName), Is.False);
                AssertCreatorFits(Creator(), "ordinary creator after failed capture preparation");
            }
            finally { (capture as System.IDisposable)?.Dispose(); }
        }

        private static IEnumerator FailCreatorPreparation(string failure)
        {
            yield return null;
            throw new System.InvalidOperationException(failure);
        }

        /// <summary>
        /// A narrow Personality page intentionally has no summary; the wide fixture must have one.
        /// Check that explicit expectation, then the ready avatar and its actual captured pixels.
        /// </summary>
        private IEnumerator PhotographCreatorPersonalityThroughLens(CaptureLens lens, string name, bool expectPhoto)
        {
            if (!Application.isBatchMode) yield break;
            yield return lens.MakeSureTheCanvasesAreDrawn();
            RawImage[] Photos() => Creator().GetComponentsInChildren<RawImage>().Where(item =>
                item.name == "Face" && item.transform.parent.name == "Live photo").ToArray();
            Assert.That(Photos(), Has.Length.EqualTo(expectPhoto ? 1 : 0), name + ": this fixture's expected summary layout.");
            // Settle the final lens before waiting. A nonblank page alone previously accepted an
            // empty portrait frame, and silently skipping a missing expected photo would repeat it.
            if (expectPhoto) yield return PrepareCreatorFrame(name, livePhoto: true);
            yield return PhotographThroughLens(lens, name, frame =>
            {
                var photos = Photos();
                Assert.That(photos, Has.Length.EqualTo(expectPhoto ? 1 : 0), name + ": the photographed summary layout.");
                if (!expectPhoto) return;
                AssertCreatorFrameReady(name, livePhoto: true);
                AssertRegionHasContent(frame, CaptureRectOf(photos[0].transform, .6f, .6f),
                    name + ": the interior of the actual live photo, excluding its frame");
            });
        }

        private IEnumerator PrepareCreatorFrame(string name, bool livePhoto = false)
        {
            if (creatorPreviewObservation != null) creatorPreviewObservation.MarkPreparation();
            float started = Time.realtimeSinceStartup;
            float deadline = started + 25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var creator = Creator();
                var preview = creator.StudioPreview;
                if (creator.IsShowing && preview != null && preview.gameObject.activeInHierarchy && !preview.IsBuilding
                    && preview.CompletedKey == creator.Draft.Appearance.ContentKey()) break;
                yield return null;
            }
            // The preview's completion and the status text update occur in separate Update calls.
            yield return null;
            TestContext.WriteLine(name + ": preview preparation waited " +
                (Time.realtimeSinceStartup - started).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + " seconds; " + CreatorPreviewDiagnostic(Creator()));
            AssertCreatorFrameReady(name, livePhoto);
        }

        private void AssertCreatorFrameReady(string name, bool livePhoto = false)
        {
            var creator = Creator();
            var preview = creator == null ? null : creator.StudioPreview;
            string evidence = " [" + CreatorPreviewDiagnostic(creator) + "]";
            Assert.That(creator != null && creator.IsShowing && preview != null && preview.gameObject.activeInHierarchy && !preview.IsBuilding,
                Is.True, name + ": the displayed preview must complete before the frame is read." + evidence);
            Assert.That(preview.CompletedKey, Is.EqualTo(creator.Draft.Appearance.ContentKey()), name + ": capture the current draft." + evidence);
            if (CharacterBodySource.Provider != null)
                Assert.That(preview.CanRetry, Is.False, name + ": installed content must render its own avatar, rather than a fallback." + evidence);
            var image = creator.GetComponentsInChildren<RawImage>().SingleOrDefault(item => livePhoto
                ? item.name == "Face" && item.transform.parent.name == "Live photo"
                : item.name == "Live character preview");
            Assert.That(image, Is.Not.Null, name + ": the current page must contain its actual preview image." + evidence);
            var texture = preview.Texture as RenderTexture;
            Assert.That(image.isActiveAndEnabled && !image.canvasRenderer.cull && image.color.a >= .5f && image.texture == texture
                && texture != null && texture.IsCreated(), Is.True, name + ": the actual visible image must bind the ready studio." + evidence);
            var previous = RenderTexture.active;
            var resolved = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, resolved); RenderTexture.active = resolved;
                pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply();
                var colors = pixels.GetPixels32();
                var backdrop = colors[0];
                int foreground = 0, minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
                for (int pixel = 0; pixel < colors.Length; pixel += 16)
                {
                    var color = colors[pixel];
                    if (color.a < 128 || (System.Math.Abs(color.r - backdrop.r) < 8 && System.Math.Abs(color.g - backdrop.g) < 8
                        && System.Math.Abs(color.b - backdrop.b) < 8)) continue;
                    foreground++;
                    int x = pixel % texture.width, y = pixel / texture.width;
                    minX = System.Math.Min(minX, x); maxX = System.Math.Max(maxX, x);
                    minY = System.Math.Min(minY, y); maxY = System.Math.Max(maxY, y);
                }
                Assert.That(foreground, Is.GreaterThanOrEqualTo(128), name + ": a uniform clear is not an avatar.");
                Assert.That(maxX - minX + 1, Is.GreaterThanOrEqualTo(8), name + ": foreground has visible width.");
                Assert.That(maxY - minY + 1, Is.GreaterThanOrEqualTo(16), name + ": foreground has visible height.");
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(resolved); Object.Destroy(pixels); }
        }

        private static string CreatorPreviewDiagnostic(CharacterCreator creator)
        {
            string provider = CharacterBodySource.Provider?.GetType().FullName ?? "none";
            if (creator == null) return "creator=missing; provider=" + provider;
            var preview = creator.StudioPreview;
            if (preview == null) return "preview=missing; showing=" + creator.IsShowing + "; provider=" + provider;
            var states = preview.GetComponentsInChildren<CharacterBodyBuildState>(true)
                .Select(state => "revision=" + state.Revision + ",ready=" + state.Ready);
            return "provider=" + provider + "; showing=" + creator.IsShowing
                + "; active=" + preview.gameObject.activeInHierarchy + "; building=" + preview.IsBuilding
                + "; canRetry=" + preview.CanRetry + "; status=" + (preview.Status ?? "<null>")
                + "; lastBuildMs=" + preview.LastBuildMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + "; timeoutSeconds=" + preview.BuildTimeoutSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                + "; completedKey=" + (preview.CompletedKey ?? "<null>")
                + "; draftKey=" + creator.Draft.Appearance.ContentKey()
                + "; originalFailure=[" + (preview.LastFailureDiagnostic ?? "none") + "]"
                + "; bodyStates=[" + string.Join(";", states) + "]"
                + "; renderers=" + preview.GetComponentsInChildren<Renderer>(true).Length;
        }

        /// <summary>
        /// Every control is inside the creator's frame and none but the footer's own sits on the
        /// footer. A control in a scrolling list is held to the list's width instead: its height is
        /// the list's to scroll.
        /// </summary>
        private static void AssertCreatorFits(CharacterCreator creator, string where)
        {
            Canvas.ForceUpdateCanvases();
            var root = (RectTransform)creator.transform;
            var frame = root.rect;
            var footer = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Fixed footer");
            var footerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, footer);
            var controls = creator.GetComponentsInChildren<Selectable>().Where(control => control.IsActive()).ToArray();
            Assert.That(controls, Is.Not.Empty, where);
            var problems = new List<string>();
            foreach (var control in controls)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, control.transform);
                var scroll = control.GetComponentInParent<ScrollRect>();
                if (scroll != null && scroll.gameObject != control.gameObject && control.transform.IsChildOf(scroll.content))
                {
                    var viewport = RectTransformUtility.CalculateRelativeRectTransformBounds(root, scroll.viewport);
                    if (bounds.min.x < viewport.min.x - 1f || bounds.max.x > viewport.max.x + 1f)
                        problems.Add(control.name + " is wider than its list");
                    continue;
                }
                if (bounds.min.x < frame.xMin - 1f || bounds.max.x > frame.xMax + 1f || bounds.min.y < frame.yMin - 1f || bounds.max.y > frame.yMax + 1f)
                    problems.Add(control.name + " is outside the frame");
                if (!control.transform.IsChildOf(footer) && bounds.min.y < footerBounds.max.y - 1f)
                    problems.Add(control.name + " is on the footer");
            }
            Assert.That(problems, Is.Empty, where);
        }

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        [UnityTest]
        public IEnumerator Creator_ColourSwatchesWriteTheColourTheyShow()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>(),
                "The garment colours are the modular bodies' - the harness's cast is.");

            CastButtons("Clothing")[0].onClick.Invoke();
            yield return null;
            var swatch = CastButtons("Fabric Chest 4").Single();
            var shown = swatch.GetComponent<Image>().color;
            swatch.onClick.Invoke();
            yield return null;
            Assert.That(AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Chest", out var worn), Is.True,
                "The top's colour is saved with the outfit.");
            AssertSameColour(worn, shown, "top");
            Assert.That(AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Legs", out _), Is.False,
                "Only the garment picked for changes.");
            Assert.That(CastButtons("Fabric Chest 4").Single().transform.Find("Chosen ring"), Is.Not.Null, "The worn colour is ringed.");
            CastButtons("Original top color").Single().onClick.Invoke();
            yield return null;
            Assert.That(AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Chest", out _), Is.False,
                "The garment's own colour comes back.");

            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            var hair = CastButtons("Hair 9").Single();
            shown = hair.GetComponent<Image>().color;
            hair.onClick.Invoke();
            yield return null;
            AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Hair", Color.clear), shown, "hair");
            AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Brows", Color.clear), shown,
                "brows, which take the hair colour whichever brow style is worn");
            var brow = CastButtons("Brows 2").Single();
            shown = brow.GetComponent<Image>().color;
            brow.onClick.Invoke();
            yield return null;
            AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Brows", Color.clear), shown, "brows set apart");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// The Colors page names what is worn: the tone in its heading, the brows by the whole hair
        /// palette - a dyed colour the hair handed on is "matches hair", not "custom" - and "custom"
        /// only for a colour on no palette. Match hair color puts the hair's colour back on the
        /// brows, and the keyboard or pad on a swatch names it as the pointer does.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_TheColorsPageNamesTheWornAndTheFocusedSwatch()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            string Heading(string prefix) => creator.GetComponentsInChildren<TMPro.TMP_Text>().Single(text => text.name == prefix + " heading").text;
            Color Worn(string id) => AppearanceEditing.ColorValue(creator.Draft.Appearance, id, Color.clear);

            CastButtons("Skin 1").Single().onClick.Invoke();
            yield return null;
            Assert.That(Heading("Skin"), Does.Contain("PORCELAIN"), "The worn tone is named.");

            // Hair 24 is the dyed Blue: the brows take it and say so.
            CastButtons("Hair 24").Single().onClick.Invoke();
            yield return null;
            Assert.That(Heading("Hair"), Does.Contain("BLUE"));
            Assert.That(Heading("Brows"), Does.Contain("MATCHES HAIR").And.Not.Contain("CUSTOM"), "A dyed colour from the hair is not a custom one.");

            CastButtons("Brows 2").Single().onClick.Invoke();
            yield return null;
            Assert.That(Heading("Brows"), Does.Contain("BLACK").And.Not.Contain("MATCHES HAIR"), "Brows of their own are named.");

            // A dyed brow colour apart from the hair - a saved look's - is named from the whole palette.
            AppearanceEditing.SetColor(creator.Draft.Appearance, "Brows", CharacterPalettes.Named(CharacterPalettes.Hair, "Teal"));
            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            Assert.That(Heading("Brows"), Does.Contain("TEAL").And.Not.Contain("CUSTOM"));
            AppearanceEditing.SetColor(creator.Draft.Appearance, "Brows", new Color(.5f, .1f, .9f));
            CastButtons("Colors")[0].onClick.Invoke();
            yield return null;
            Assert.That(Heading("Brows"), Does.Contain("CUSTOM"), "Only a colour on no palette is custom.");

            CastButtons(CharacterCreator.MatchHairCaption).Single().onClick.Invoke();
            yield return null;
            AssertSameColour(Worn("Brows"), Worn("Hair"), "brows matched to the hair");
            Assert.That(Heading("Brows"), Does.Contain("MATCHES HAIR"));

            // Skin 5 is Sand. Focus on it names it; focus leaving names the worn tone again.
            var events = UnityEngine.EventSystems.EventSystem.current;
            Assert.That(events, Is.Not.Null, "The creator is driven by the scene's event system.");
            events.SetSelectedGameObject(CastButtons("Skin 5").Single().gameObject);
            Assert.That(Heading("Skin"), Does.Contain("SAND"), "The focused swatch is named in the heading.");
            events.SetSelectedGameObject(null);
            Assert.That(Heading("Skin"), Does.Contain("PORCELAIN").And.Not.Contain("SAND"), "Focus moving off names the worn tone again.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// A starting look's garment colour - the one in its photo, seldom a swatch - is on its row,
        /// ringed at the end, and stays there to be picked again after trying another; the row's way
        /// back to the garment's own colour still clears the tint.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_AStartingLooksGarmentColourIsRingedOnItsRow()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            // Chelsie's top is the pink of her photo, which no fabric swatch is.
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("chelsie-baham")), _ => { }, () => { });
            yield return null;
            CastButtons("Clothing")[0].onClick.Invoke();
            yield return null;
            bool Tinted(out Color tint) => AppearanceEditing.TryFabric(AppearanceEditing.Outfit(creator.Draft.Appearance), "Chest", out tint);
            Assert.That(Tinted(out var photo), Is.True, "Her top wears the colour in her photo.");
            List<Button> Row() => Enumerable.Range(1, 16).SelectMany(number => CastButtons("Fabric Chest " + number)).ToList();
            var row = Row();
            var ringed = row.Where(swatch => swatch.transform.Find("Chosen ring") != null).ToList();
            Assert.That(ringed, Has.Count.EqualTo(1), "The worn colour is ringed on the row.");
            Assert.That(ringed[0], Is.SameAs(row.Last()), "A colour off the palette joins the end of the row.");
            Assert.That(row, Has.Count.EqualTo(13), "The twelve fabric swatches and the photo's colour.");
            AssertSameColour(ringed[0].GetComponent<Image>().color, photo, "the ringed swatch");

            CastButtons("Fabric Chest 1").Single().onClick.Invoke();
            yield return null;
            row = Row();
            Assert.That(row, Has.Count.EqualTo(13), "Trying a swatch leaves the photo's colour on the row.");
            ringed = row.Where(swatch => swatch.transform.Find("Chosen ring") != null).ToList();
            Assert.That(ringed, Has.Count.EqualTo(1));
            Assert.That(ringed[0], Is.SameAs(row[0]), "The swatch tried is the one ringed.");
            CastButtons("Fabric Chest 13").Single().onClick.Invoke();
            yield return null;
            Assert.That(Tinted(out var again), Is.True);
            AssertSameColour(again, photo, "the photo's colour picked again from its swatch");

            CastButtons("Original top color").Single().onClick.Invoke();
            yield return null;
            Assert.That(Tinted(out _), Is.False, "The garment's own colour comes back.");
            Assert.That(Row(), Has.Count.EqualTo(13), "And the photo's colour is still there to go back to.");
        }
#endif

        /// <summary>
        /// The six category tiles stay on their panel however short a wide screen leaves it: at a
        /// fixed pitch the sixth hung past the panel's foot at 21:9 with larger text and at 32:9.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_TheCategoryTilesFitTheirPanelOnWideScreens()
        {
            yield return OpenCreator();
            var creator = Creator();
            var canvas = creator.GetComponent<Canvas>();
            var camera = cameraRig.ViewCamera;
            var previous = camera.targetTexture;
            var textures = new List<RenderTexture>();
            try
            {
                foreach (var (width, height, scale) in new[] { (1280, 540, 1.2f), (1280, 540, 1.4f), (1280, 360, 1f) })
                {
                    var texture = new RenderTexture(width, height, 24);
                    textures.Add(texture);
                    camera.targetTexture = texture;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
                    creator.FontScale = scale;
                    Canvas.ForceUpdateCanvases();
                    yield return null; yield return null;
                    CastButtons("Body")[0].onClick.Invoke();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    string shape = width + "x" + height + " at " + scale;
                    var panel = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Appearance categories");
                    var bounds = panel.rect;
                    var corners = new Vector3[4];
                    foreach (string category in CreatorCategories)
                    {
                        var tile = CastButtons(category).Single(button => button.transform.parent == panel).GetComponent<RectTransform>();
                        tile.GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var local = panel.InverseTransformPoint(corner);
                            Assert.That(local.y, Is.GreaterThanOrEqualTo(bounds.yMin - 1f), category + " hangs below its panel at " + shape);
                            Assert.That(local.y, Is.LessThanOrEqualTo(bounds.yMax + 1f), category + " rises above its panel at " + shape);
                            Assert.That(local.x, Is.InRange(bounds.xMin - 1f, bounds.xMax + 1f), category + " is wider than its panel at " + shape);
                        }
                    }
                }
            }
            finally
            {
                creator.FontScale = 1f;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = previous;
                foreach (var texture in textures) { texture.Release(); Object.Destroy(texture); }
            }
        }

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Face details - freckles, makeup, an older face - are the person's, not the outfit's: worn
        /// from the Face page on every outfit and taken off every outfit, left alone by the Hair
        /// page's reset, and put back as the look began by the Face page's.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_FaceDetailsAreWornOnEveryOutfitAndResetWithTheFace()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            var catalog = ((IModularCharacterBodyProvider)CharacterBodySource.Provider).Catalog;
            // A look that begins with a face detail, so the reset has one to put back.
            var start = CharacterDraft.Blank();
            start.Appearance = catalog.Materialize(start.Appearance);
            var details = catalog.Items.Where(item => item.Slot == "Face" && item.Fits(start.Appearance.bodyId)).ToList();
            Assert.That(details, Has.Count.GreaterThanOrEqualTo(2), "The catalog offers face details for this body.");
            AppearanceEditing.Wear(start.Appearance, details[1], catalog);
            creator.Show(new SeasonBuilder.Choice(), start, _ => { }, () => { });
            yield return null;
            // A second outfit, so "every outfit" is more than one.
            CastButtons("Clothing")[0].onClick.Invoke();
            yield return null;
            CastButtons("Competition").Single().onClick.Invoke();
            yield return null;
            CastButtons("Everyday").Single().onClick.Invoke();
            yield return null;
            string[] Worn() => creator.Draft.Appearance.outfits.Select(outfit => outfit.wardrobe.FirstOrDefault(item => item.slot == "Face")?.itemId).ToArray();
            Assert.That(creator.Draft.Appearance.outfits, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(Worn(), Is.All.EqualTo(details[1].Id), "The starting face detail is on both outfits.");

            CastButtons("Face")[0].onClick.Invoke();
            yield return null;
            var texts = creator.GetComponentsInChildren<TMPro.TMP_Text>().Where(text => text.isActiveAndEnabled).Select(text => text.text).ToList();
            Assert.That(texts, Does.Contain("FACE DETAILS"), "The Face page has a strip of face details.");
            CastButtons("Previous face details").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.EqualTo(details[0].Id), "A face detail is worn on every outfit.");

            CastButtons("Hair")[0].onClick.Invoke();
            yield return null;
            CastButtons("Reset hair").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.EqualTo(details[0].Id), "The Hair page's reset leaves the face alone.");

            CastButtons("Face")[0].onClick.Invoke();
            yield return null;
            CastButtons("Remove face details").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.Null, "Removed from every outfit, not only the one being edited.");
            CastButtons("Reset face").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn(), Is.All.EqualTo(details[1].Id), "The Face page's reset puts the look's own face detail back on every outfit.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Changing body takes off a face detail the other body has nothing like, and says so:
        /// makeup, drawn for one body alone, came back from the change as whichever face detail the
        /// other body sorted first - an older face nobody chose. An older face stays an older face.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_ChangingBodyTakesOffMakeupTheOtherBodyCannotWear()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            var catalog = ((IModularCharacterBodyProvider)CharacterBodySource.Provider).Catalog;
            var makeup = catalog.Items.First(item => item.Slot == "Face" && item.StyleGroup == "face.makeup");
            var drawnFor = catalog.Bodies.Single(body => makeup.Fits(body.Id));
            var other = catalog.Bodies.Single(body => body.Id != drawnFor.Id);
            var start = CharacterDraft.Blank();
            start.Appearance = catalog.ChangeBody(catalog.Materialize(start.Appearance), drawnFor.Id);
            AppearanceEditing.Wear(start.Appearance, makeup, catalog);
            creator.Show(new SeasonBuilder.Choice(), start, _ => { }, () => { });
            yield return null;
            CastButtons("Body")[0].onClick.Invoke();
            yield return null;
            string Face() => AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe.FirstOrDefault(item => item.slot == "Face")?.itemId;
            Assert.That(Face(), Is.EqualTo(makeup.Id), "The look begins in makeup.");

            CastButtons(other.Label).Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Appearance.bodyId, Is.EqualTo(other.Id));
            Assert.That(Face(), Is.Null, "Makeup the other body cannot wear comes off rather than becoming an older face.");
            var texts = creator.GetComponentsInChildren<TMPro.TMP_Text>().Select(text => text.text).ToList();
            Assert.That(texts.Any(text => text.Contains(makeup.Label + " → removed")), Is.True, "The change says the makeup came off.");

            CastButtons("Undo").Single().onClick.Invoke();
            yield return null;
            Assert.That(Face(), Is.EqualTo(makeup.Id), "Undo puts the makeup back.");
            // An older face drawn for this body alone: the other body's older face takes its place.
            var older = catalog.Items.First(item => item.Slot == "Face" && item.StyleGroup == "face.age" && item.Fits(drawnFor.Id) && !item.Fits(other.Id));
            AppearanceEditing.Wear(creator.Draft.Appearance, older, catalog);
            CastButtons(other.Label).Single().onClick.Invoke();
            yield return null;
            var kept = AppearanceEditing.Find(catalog, Face());
            Assert.That(kept, Is.Not.Null, "An older face is kept through the change.");
            Assert.That(kept.StyleGroup, Is.EqualTo("face.age"), "It is still an older face.");
            Assert.That(kept.Fits(other.Id), Is.True);
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Accessories are picked from their own page, worn with the outfit being edited, undone and
        /// redone like any edit, and put back as they were by that page's reset - taken off a look
        /// that began without them, and put back on one that began with them.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_AccessoriesAreWornFromTheirPageAndResetWithIt()
        {
            yield return OpenCreator();
            var creator = Creator();
            CastButtons("Accessories")[0].onClick.Invoke();
            yield return null;
            // A second outfit first: the blank look has only the one, and "this outfit only" held of
            // an empty set whatever Wear did.
            CastButtons("Competition").Single().onClick.Invoke();
            yield return null;
            CastButtons("Everyday").Single().onClick.Invoke();
            yield return null;
            string Worn(string slot) => AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe.FirstOrDefault(item => item.slot == slot)?.itemId;
            CharacterOutfit Other() => creator.Draft.Appearance.outfits.Single(outfit => outfit.id == "Competition");
            Assert.That(creator.Draft.Appearance.activeOutfit, Is.EqualTo("Everyday"));
            Assert.That(Worn("Eyewear"), Is.Null, "The starting look wears no glasses.");
            CastButtons("Black frames").Single().onClick.Invoke();
            yield return null;
            CastButtons("Gold hoops").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"));
            Assert.That(Worn("Earrings"), Is.EqualTo(ProceduralAccessories.Prefix + "hoops"));
            Assert.That(Other().wardrobe, Is.Not.Empty, "The other outfit is dressed.");
            Assert.That(Other().wardrobe.Any(item => ProceduralAccessories.Slots.Contains(item.slot)), Is.False, "Worn with this outfit only.");

            CastButtons("Undo").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Earrings"), Is.Null, "Undo takes the hoops off.");
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"), "Only the hoops.");
            CastButtons("Redo").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Earrings"), Is.EqualTo(ProceduralAccessories.Prefix + "hoops"), "Redo puts them back.");

            CastButtons("Reset accessories").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.Null, "Reset takes them off again.");
            Assert.That(Worn("Earrings"), Is.Null);
            Assert.That(Worn("Chest"), Is.Not.Null, "And leaves the clothes alone.");

            // A starting look that wears glasses - Riley's black frames - gets them back from the reset.
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("riley-johnson")), _ => { }, () => { });
            yield return null;
            CastButtons("Accessories")[0].onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"), "Riley's look wears his glasses.");
            CastButtons("Remove eyewear").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.Null);
            CastButtons("Reset accessories").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"), "Reset puts the starting look's glasses back on.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>
        /// Randomize rolls the accessories too - most often to none - unless their page's lock is
        /// on, and colours a random houseguest from the skin out: hair and eyes weighted by the
        /// skin's depth, so deep skin seldom comes with light eyes or blonde, red or white hair.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_RandomizeRollsAccessoriesUnlessLockedAndColoursFromTheSkin()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("vanessa-rousso")), _ => { }, () => { });
            creator.RandomSeed = 20260925;
            yield return null;
            CastButtons("Accessories")[0].onClick.Invoke();
            yield return null;
            string Accessories() => string.Join(",", AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe
                .Where(item => ProceduralAccessories.Slots.Contains(item.slot)).OrderBy(item => item.slot).Select(item => item.slot + "=" + item.itemId));
            string start = Accessories();
            Assert.That(start, Does.Contain(ProceduralAccessories.Prefix + "cap"), "Her starting look wears her trucker cap.");

            var lightEyes = new[] { "Green", "Grey-green", "Blue", "Light blue", "Grey-blue", "Grey" };
            var unlikelyHair = new[] { "Auburn", "Copper", "Strawberry", "Dark blonde", "Honey blonde", "Golden blonde", "Platinum", "Silver", "White", "Salt and pepper" };
            const int rolls = 30;
            int changed = 0, bare = 0, worn = 0, deep = 0, unlikely = 0;
            for (int roll = 0; roll < rolls; roll++)
            {
                CastButtons("Randomize").Single().onClick.Invoke();
                yield return null;
                string now = Accessories();
                int count = now.Length == 0 ? 0 : now.Split(',').Length;
                if (now != start) changed++;
                if (count == 0) bare++;
                worn += count;
                var appearance = creator.Draft.Appearance;
                if (CharacterPalettes.DepthOf(AppearanceEditing.ColorValue(appearance, "Skin", Color.clear)) < 2) continue;
                deep++;
                string eyes = CharacterPalettes.NameOf(CharacterPalettes.Eyes, AppearanceEditing.ColorValue(appearance, "Eyes", Color.clear));
                string hair = CharacterPalettes.NameOf(CharacterPalettes.Hair, AppearanceEditing.ColorValue(appearance, "Hair", Color.clear));
                Assert.That(eyes, Is.Not.Null, "Roll " + roll + " gave palette eyes.");
                Assert.That(hair, Is.Not.Null, "Roll " + roll + " gave a palette hair colour.");
                if (lightEyes.Contains(eyes) || unlikelyHair.Contains(hair)) unlikely++;
            }
            Assert.That(changed, Is.GreaterThan(0), "Randomize rolls the accessories when they are not locked.");
            Assert.That(bare, Is.GreaterThan(0), "Often a random houseguest wears none at all.");
            Assert.That(worn, Is.GreaterThan(0), "But sometimes one.");
            Assert.That(worn, Is.LessThan(rolls * ProceduralAccessories.Slots.Length / 2), "An accessory is the exception, not the rule.");
            Assert.That(deep, Is.GreaterThan(0), "Tan and deep skin come up as often as fair.");
            Assert.That(unlikely, Is.LessThanOrEqualTo(Mathf.Max(2, deep / 2)),
                unlikely + " of " + deep + " tan or deep-skinned rolls had light eyes or blonde, red or grey hair.");

            CastButtons("Lock Accessories").Single().onClick.Invoke();
            yield return null;
            string locked = Accessories();
            for (int roll = 0; roll < 4; roll++)
            {
                CastButtons("Randomize").Single().onClick.Invoke();
                yield return null;
                Assert.That(Accessories(), Is.EqualTo(locked), "Roll " + roll + " kept the locked accessories.");
            }
            Assert.That(CastButtons("Unlock Accessories"), Is.Not.Empty, "The lock says it is on.");
        }
#endif

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        /// <summary>The face page offers its features under headings, the jaw among them.</summary>
        [UnityTest]
        public IEnumerator Creator_TheFaceGroupsItsFeaturesUnderHeadings()
        {
            yield return OpenCreator();
            var creator = Creator();
            CastButtons("Face")[0].onClick.Invoke();
            yield return null;
            var texts = creator.GetComponentsInChildren<TMPro.TMP_Text>().Where(text => text.isActiveAndEnabled).Select(text => text.text).ToList();
            foreach (string heading in new[] { "FACE SHAPE", "CHEEKS", "EYES & BROWS", "NOSE", "MOUTH & EARS" })
                Assert.That(texts, Does.Contain(heading), heading);
            foreach (string feature in new[] { "Jaw width", "Cheekbones", "Eye tilt", "Nose bridge" })
                Assert.That(creator.GetComponentsInChildren<Slider>().Any(slider => slider.name == feature + " slider"), Is.True, feature);
        }
#endif

        /// <summary>
        /// Handed to a camera of another shape - what a review capture does, and what a window resize
        /// does to the screen - the creator lays itself out again for that frame on the next frame.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_LaysItselfOutAgainForTheFrameItIsDrawnTo()
        {
            yield return OpenCreator();
            var creator = Creator();
            var canvas = creator.GetComponent<Canvas>();
            var camera = cameraRig.ViewCamera;
            var texture = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = texture;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
                Canvas.ForceUpdateCanvases();
                yield return null; yield return null;
                var root = (RectTransform)creator.transform;
                Assert.That(root.rect.width / root.rect.height, Is.EqualTo(16f / 9f).Within(.01f), "The canvas took the camera's shape.");
                AssertCreatorFits(creator, "Appearance through a 16:9 camera");
                var footer = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Fixed footer");
                var start = CastButtons(CharacterCreator.StartCaption).Single().GetComponent<RectTransform>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, start);
                Assert.That(bounds.max.x, Is.GreaterThan(root.rect.xMax - root.rect.width * .4f),
                    "The way on stands at the right of this frame, not where the 4:3 frame put it.");
                Assert.That(start.IsChildOf(footer), Is.True);
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = previous;
                texture.Release();
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator Creator_ANewHouseguestAsksBeforeReplacingTheDraft()
        {
            yield return OpenCreator();
            var creator = Creator();
            creator.Draft.Name = "Keep Me";
            CastButtons("My Houseguests")[0].onClick.Invoke();
            yield return null;
            CastButtons("Create new houseguest").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Name, Is.EqualTo("Keep Me"), "One press asks; it does not throw the draft away.");
            CastButtons("Confirm new houseguest").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Name, Is.Empty, "The second press starts a blank houseguest.");
            Assert.That(CastButtons("Next starting look"), Is.Not.Empty, "It opens on the appearance step.");
        }

#if GAMESIM_UMA
        // UMA only: these pages dress a modular body, and only UMA builds one.
        [UnityTest]
        public IEnumerator Creator_ALockedCategoryKeepsItsLookThroughRandomize()
        {
            yield return OpenCreator();
            var creator = Creator();
            Assert.That(CharacterBodySource.Provider, Is.InstanceOf<IModularCharacterBodyProvider>());
            CastButtons("Hair")[0].onClick.Invoke();
            yield return null;
            CastButtons("Lock Hair").Single().onClick.Invoke();
            yield return null;
            string Hair() => string.Join(",", AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe
                .Where(item => AppearanceEditing.IsCharacterSlot(item.slot)).OrderBy(item => item.slot).Select(item => item.slot + "=" + item.itemId));
            string before = Hair(), look = creator.Draft.Appearance.ContentKey();
            var colour = AppearanceEditing.ColorValue(creator.Draft.Appearance, "Hair", Color.clear);
            for (int roll = 0; roll < 4; roll++)
            {
                CastButtons("Randomize").Single().onClick.Invoke();
                yield return null;
                Assert.That(Hair(), Is.EqualTo(before), "Roll " + roll + " kept the locked hair.");
                AssertSameColour(AppearanceEditing.ColorValue(creator.Draft.Appearance, "Hair", Color.clear), colour,
                    "roll " + roll + " kept the locked hair's colour, which is on the Hair page");
            }
            Assert.That(creator.Draft.Appearance.ContentKey(), Is.Not.EqualTo(look), "Randomize changed what was not locked.");
            Assert.That(CastButtons("Unlock Hair"), Is.Not.Empty, "The lock says it is on.");
        }
#endif

        /// <summary>
        /// Looking at a saved houseguest does not load it: the bar's Delete reaches any card while the
        /// draft on screen stays as it is.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_LookingAtASavedHouseguestLeavesTheDraftAlone()
        {
            yield return OpenCreator();
            var creator = Creator();
            foreach (string name in new[] { "Saved Ada", "Saved Bo" })
            {
                var saved = creator.Draft.Copy();
                saved.Name = name;
                Assert.That(creator.ProfileStore.Save(CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), saved), out var error), Is.True, error);
            }
            creator.Draft.Name = "Unsaved Cy";
            CastButtons("My Houseguests")[0].onClick.Invoke();
            yield return null;
            CastButtons("Details for Saved Bo").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Name, Is.EqualTo("Unsaved Cy"), "Looking is not loading.");
            CastButtons("Delete Saved Bo").Single().onClick.Invoke();
            yield return null;
            CastButtons("Confirm delete Saved Bo").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.ProfileStore.List().Select(profile => profile.name), Is.EquivalentTo(new[] { "Saved Ada" }));
            Assert.That(creator.Draft.Name, Is.EqualTo("Unsaved Cy"), "Deleting a saved houseguest leaves the draft alone.");
        }

        /// <summary>The comparison with the original look belongs to the Appearance page; leaving it ends it.</summary>
        [UnityTest]
        public IEnumerator Creator_TheComparisonEndsWithTheAppearancePage()
        {
            yield return OpenCreator();
            CastButtons("Next starting look")[0].onClick.Invoke();
            yield return null;
            CastButtons("Compare original").Single().onClick.Invoke();
            yield return null;
            Assert.That(CastButtons("Return to edited look"), Is.Not.Empty);
            CastButtons("Identity")[0].onClick.Invoke();
            yield return null;
            CastButtons("Appearance")[0].onClick.Invoke();
            yield return null;
            Assert.That(CastButtons("Compare original"), Is.Not.Empty, "Back on Appearance, the edited look is the one shown.");
            Assert.That(CastButtons("Return to edited look"), Is.Empty);
        }

        private static void AssertSameColour(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.01f), what + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.01f), what + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.01f), what + " blue");
        }
    }
}

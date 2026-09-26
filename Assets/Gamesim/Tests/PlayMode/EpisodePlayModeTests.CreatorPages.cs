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
        private static readonly string[] CreatorPages = { "Appearance", "Identity", "Personality", "My Houseguests", "Review" };
        private static readonly string[] CreatorCategories = { "Body", "Face", "Hair", "Clothing", "Accessories", "Colors" };

        [UnityTest]
        public IEnumerator Creator_EveryPageFitsTheFrameAndIsCapturedForReview()
        {
            yield return OpenCreator();
            var creator = Creator();
            // A saved houseguest, so My Houseguests has a card and its bar to lay out.
            var saved = creator.Draft.Copy();
            saved.Name = "Review Guest";
            Assert.That(creator.ProfileStore.Save(CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), saved), out var error), Is.True, error);
            float built = Time.realtimeSinceStartup + 25f;
            while (creator.StudioPreview != null && creator.StudioPreview.IsBuilding && Time.realtimeSinceStartup < built)
                yield return null;

            foreach (float scale in new[] { 1f, 1.2f })
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
                            if (scale == 1f && Application.isBatchMode) yield return CaptureFraming("creator-appearance-" + category.ToLowerInvariant());
                        }
                    else
                    {
                        AssertCreatorFits(creator, page + " at " + scale);
                        if (scale == 1f && Application.isBatchMode)
                            yield return CaptureFraming("creator-" + page.ToLowerInvariant().Replace(' ', '-'));
                    }
                }
            }
            creator.FontScale = 1f;
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

        /// <summary>
        /// Accessories are picked from their own page, worn with the outfit being edited, and put
        /// back as they were by that page's reset.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_AccessoriesAreWornFromTheirPageAndResetWithIt()
        {
            yield return OpenCreator();
            var creator = Creator();
            CastButtons("Accessories")[0].onClick.Invoke();
            yield return null;
            string Worn(string slot) => AppearanceEditing.Outfit(creator.Draft.Appearance).wardrobe.FirstOrDefault(item => item.slot == slot)?.itemId;
            Assert.That(Worn("Eyewear"), Is.Null, "The starting look wears no glasses.");
            CastButtons("Black frames").Single().onClick.Invoke();
            yield return null;
            CastButtons("Gold hoops").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.EqualTo(ProceduralAccessories.Prefix + "glasses"));
            Assert.That(Worn("Earrings"), Is.EqualTo(ProceduralAccessories.Prefix + "hoops"));
            var outfits = creator.Draft.Appearance.outfits.Where(outfit => outfit.id != creator.Draft.Appearance.activeOutfit);
            Assert.That(outfits.All(outfit => outfit.wardrobe.All(item => item.slot != "Eyewear")), Is.True, "Worn with this outfit only.");
            CastButtons("Reset accessories").Single().onClick.Invoke();
            yield return null;
            Assert.That(Worn("Eyewear"), Is.Null, "Reset takes them off again.");
            Assert.That(Worn("Earrings"), Is.Null);
            Assert.That(Worn("Chest"), Is.Not.Null, "And leaves the clothes alone.");
        }

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

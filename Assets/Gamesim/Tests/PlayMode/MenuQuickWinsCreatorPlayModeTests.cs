using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The menu review's quick wins on the character creator (part a), on the built screen: a creator
    /// of its own on a scratch library, with an event system for focus. The Settings gear's round trip
    /// needs the director and is in EpisodePlayModeTests.MenuQuickWinsCreator.
    /// </summary>
    public sealed class MenuQuickWinsCreatorPlayModeTests
    {
        private GameObject owner, events;
        private string root;
        private FailingProvider failing;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (failing != null) { CharacterBodySource.Unregister(failing); failing = null; }
            if (owner != null) Object.Destroy(owner);
            if (events != null) Object.Destroy(events);
            yield return null;
            if (root == null) yield break;
            string resolved = Path.GetFullPath(root);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimQuickWinsCreator-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            root = null;
        }

        private CharacterCreator Open(bool quick = false, int savedHouseguests = 0)
        {
            if (EventSystem.current == null) events = new GameObject("Quick wins events", typeof(EventSystem));
            root = Path.Combine(Path.GetTempPath(), "GamesimQuickWinsCreator-" + Guid.NewGuid().ToString("N"));
            var library = new CharacterProfileStore(root);
            for (int i = 1; i <= savedHouseguests; i++)
            {
                var saved = CharacterDraft.FromAppearance(CastTemplates.Find("alex-chen"));
                saved.Name = "Saved Guest " + i;
                Assert.That(library.Save(CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), saved), out var error), Is.True, error);
            }
            owner = new GameObject("Menu quick wins creator");
            var creator = CharacterCreator.Attach(owner);
            creator.ConfigureProfiles(library);
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = "Quick Win Robin";
            if (quick) creator.ShowQuick(new SeasonBuilder.Choice(), draft, _ => { }, () => { });
            else creator.Show(new SeasonBuilder.Choice(), draft, _ => { }, () => { });
            return creator;
        }

        private static Button[] Buttons(Component screen, string caption) => screen.GetComponentsInChildren<Button>()
            .Where(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>()?.text == caption).ToArray();

        private static void Click(Component screen, string caption)
        {
            var buttons = Buttons(screen, caption);
            Assert.That(buttons, Has.Length.EqualTo(1), caption);
            buttons[0].onClick.Invoke();
        }

        private static Bounds Box(Component screen, Transform item) =>
            RectTransformUtility.CalculateRelativeRectTransformBounds(screen.transform, item);

        // ---------------------------------------------------------------- 1. one lit step

        [UnityTest]
        public IEnumerator QuickWin01_AFocusedStepTabNeverLooksLikeASecondLitStep()
        {
            var creator = Open();
            yield return null;
            var steps = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Setup steps");
            var tabs = steps.GetComponentsInChildren<Button>();
            Assert.That(tabs.Count(tab => tab.transform.Find("Glow") != null), Is.EqualTo(1), "Only the step on screen glows.");
            Assert.That(tabs.Single(tab => tab.transform.Find("Glow") != null).name, Is.EqualTo("Appearance"));

            var identity = Buttons(creator, "Identity").Single();
            identity.Select();
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(identity.gameObject));
            var ring = identity.GetComponentsInChildren<Image>(true).Single(image => image.name == "Focus ring");
            Assert.That(ring.enabled, Is.True, "The focused tab shows its focus.");
            Assert.That(ring.color, Is.Not.EqualTo(UiTheme.Glow), "Not in the lit step's glow.");
            Assert.That(ring.rectTransform.rect.height, Is.EqualTo(CharacterCreator.TabFocusHeight).Within(.01f),
                "A thin underline, not an outline round the tab like the lit step's.");
            Assert.That(identity.colors.selectedColor, Is.EqualTo(identity.colors.normalColor), "The focused tab is not brightened.");
            Assert.That(tabs.Count(tab => tab.transform.Find("Glow") != null), Is.EqualTo(1), "Focus lights no second step.");
        }

        // ---------------------------------------------------------------- 2 and 11. the footer

        [UnityTest]
        public IEnumerator QuickWin02_OneBrightWayOnNamesTheNextStepAndStartIsQuieter()
        {
            var creator = Open();
            yield return null;
            var next = Buttons(creator, CharacterCreator.NextCaption("Identity")).Single();
            var start = Buttons(creator, CharacterCreator.StartCaption).Single();
            Assert.That(next.name, Is.EqualTo(CharacterCreator.NextButtonName));
            Assert.That(next.GetComponent<Image>().color, Is.EqualTo(UiTheme.ActionBlue), "The way on is the one bright button.");
            Assert.That(start.GetComponent<Image>().color, Is.Not.EqualTo(UiTheme.ActionBlue), "Starting from here is quieter.");
            Assert.That(start.GetComponent<RectTransform>().rect.height, Is.LessThan(next.GetComponent<RectTransform>().rect.height),
                "and smaller.");
            Assert.That(Buttons(creator, "Continue"), Is.Empty, "The way on says where it goes.");
            var footer = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Fixed footer");
            Assert.That(footer.GetComponentsInChildren<Button>().Count(button => button.GetComponent<Image>().color == UiTheme.ActionBlue),
                Is.EqualTo(1), "One bright button in the footer.");

            Click(creator, "Review");
            yield return null;
            Assert.That(creator.GetComponentsInChildren<Button>().Any(button => button.gameObject.activeInHierarchy
                && (button.GetComponentInChildren<TMP_Text>()?.text ?? "").StartsWith("Next: ")), Is.False, "The last step has no next.");
            Assert.That(Buttons(creator, CharacterCreator.StartCaption).Single().GetComponent<Image>().color, Is.EqualTo(UiTheme.ActionBlue),
                "On the last step starting is the way on.");
        }

        [UnityTest]
        public IEnumerator QuickWin11_TheWayBackSaysTheDraftIsKeptAtItsOwnSize()
        {
            var creator = Open();
            yield return null;
            var back = Buttons(creator, CharacterCreator.BackCaption).Single();
            var label = back.GetComponentsInChildren<TMP_Text>().First(text => text.name == "Label");
            label.ForceMeshUpdate();
            Assert.That(label.text, Is.EqualTo("Back to the cast (draft kept)"));
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(14f), "Its box is wide enough that it is not shrunk under 14.");
            Assert.That(label.isTextTruncated, Is.False);
        }

        // ---------------------------------------------------------------- 3. the name

        [UnityTest]
        public IEnumerator QuickWin03_IdentityOpensWithTheCursorInTheRequiredName()
        {
            var creator = Open();
            yield return null;
            Click(creator, CharacterCreator.NextCaption("Identity"));
            yield return null;
            Assert.That(creator.Page, Is.EqualTo("Identity"));
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null, "Something has the keyboard.");
            Assert.That(selected.name, Is.EqualTo("Name field"), "The cursor is in the name.");
            var heading = creator.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Name");
            Assert.That(heading.text, Is.EqualTo("NAME (REQUIRED)"));

            Click(creator, "Personality");
            yield return null;
            Click(creator, "Identity");
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Name field"), "and by its tab too.");
        }

        // ---------------------------------------------------------------- 4. the preview's words

        [UnityTest]
        public IEnumerator QuickWin04_PreviewStatusAndRetryShowOnlyWhenThePreviewFailed()
        {
            failing = new FailingProvider();
            CharacterBodySource.Register(failing);
            // Read as the page is built, before a frame: the preview has only just been asked for.
            var creator = Open();
            var status = creator.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Preview status");
            var retry = Buttons(creator, "Retry preview").Single();
            Assert.That(creator.StudioPreview.CanRetry, Is.False, "Still building.");
            Assert.That(Shown(status), Is.False, "A preview being built says nothing.");
            Assert.That(Shown(retry), Is.False);
            Assert.That(retry.IsInteractable(), Is.False);

            float deadline = Time.realtimeSinceStartup + 20f;
            while (!creator.StudioPreview.CanRetry && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(creator.StudioPreview.CanRetry, Is.True, "The provider's failure is the preview's.");
            yield return null;
            Assert.That(Shown(status), Is.True, "A failed preview says so,");
            Assert.That(Shown(retry), Is.True, "and offers Retry preview.");
            Assert.That(retry.IsInteractable(), Is.True);
        }

        private static bool Shown(Component item)
        {
            var group = item.GetComponent<CanvasGroup>();
            return item.gameObject.activeInHierarchy && (group == null || group.alpha > 0f);
        }

        // ---------------------------------------------------------------- 5. the category icons

        [UnityTest]
        public IEnumerator QuickWin05_EachAppearanceCategoryShowsItsOwnIcon()
        {
            var creator = Open();
            yield return null;
            var list = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Appearance categories");
            var sprites = new List<Sprite>();
            foreach (var category in CharacterCreator.AppearanceCategories)
            {
                var tile = list.Find(category);
                Assert.That(tile, Is.Not.Null, category);
                var glyph = tile.Find("Glyph");
                Assert.That(glyph, Is.Not.Null, category + " has a glyph.");
                var image = glyph.GetComponent<Image>();
                if (image != null) { Assert.That(image.sprite, Is.Not.Null, category); sprites.Add(image.sprite); }
                else Assert.That(glyph.GetComponentsInChildren<Image>().Length, Is.EqualTo(3), category + " draws its three swatches.");
            }
            Assert.That(sprites, Is.Unique, "No icon repeats down the list.");
        }

        // ---------------------------------------------------------------- 6. the camera strip

        [UnityTest]
        public IEnumerator QuickWin06_TheCameraControlsSitTogetherInOneStripUnderTheBody()
        {
            var creator = Open();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var stage = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Preview stage");
            var strip = stage.Find(CharacterCreator.CameraStripName);
            Assert.That(strip, Is.Not.Null, "One strip holds the camera's controls.");
            var box = Box(creator, strip);
            foreach (var caption in new[] { "Rotate left", "Rotate right", "Front", "Side", "Zoom out", "Zoom in" })
            {
                var control = Buttons(creator, caption).Single();
                var inside = Box(creator, control.transform);
                Assert.That(inside.min.x >= box.min.x - .5f && inside.max.x <= box.max.x + .5f
                    && inside.min.y >= box.min.y - .5f && inside.max.y <= box.max.y + .5f, Is.True, caption + " is in the strip.");
            }
            var ring = Box(creator, stage.Find("Platform ring"));
            Assert.That(box.max.y, Is.LessThanOrEqualTo(ring.min.y + ring.size.y * .5f + .5f), "The strip is under the body's platform.");
        }

        // ---------------------------------------------------------------- 7. the archetype

        [UnityTest]
        public IEnumerator QuickWin07_TheArchetypeIsInTheThemeAccent()
        {
            var creator = Open();
            yield return null;
            Click(creator, "Personality");
            yield return null;
            var archetype = creator.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Archetype");
            Assert.That(archetype.color, Is.EqualTo(UiTheme.Accent));
        }

        // ---------------------------------------------------------------- 8. Quick setup's order

        [UnityTest]
        public IEnumerator QuickWin08_QuickSetupShowsTheStartingFacesFirst()
        {
            var creator = Open(quick: true);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(creator.Page, Is.EqualTo(CharacterCreator.QuickPageCaption));
            var looks = creator.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Starting look ")).ToArray();
            Assert.That(looks, Is.Not.Empty);
            var name = creator.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == "Name field");
            float facesTop = looks.Max(look => Box(creator, look.transform).max.y);
            Assert.That(facesTop, Is.GreaterThan(Box(creator, name.transform).max.y), "The faces stand above the name.");
        }

        // ---------------------------------------------------------------- 9. the mode switch

        [UnityTest]
        public IEnumerator QuickWin09_TheModeSwitchShowsOnlyOnTheAppearanceStepWithItsLabel()
        {
            var creator = Open();
            yield return null;
            Assert.That(Buttons(creator, CharacterCreator.QuickCaption), Has.Length.EqualTo(1));
            Assert.That(Buttons(creator, CharacterCreator.DetailedCaption), Has.Length.EqualTo(1));
            var label = creator.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Creator modes label");
            Assert.That(label.text, Is.EqualTo(CharacterCreator.ModesLabel));
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(14f));
            foreach (var page in new[] { "Identity", "Personality", "My Houseguests", "Review" })
            {
                Click(creator, page);
                yield return null;
                Assert.That(Buttons(creator, CharacterCreator.QuickCaption), Is.Empty, page);
                Assert.That(Buttons(creator, CharacterCreator.DetailedCaption), Is.Empty, page);
            }
            Click(creator, "Appearance");
            yield return null;
            Click(creator, CharacterCreator.QuickCaption);
            yield return null;
            Assert.That(creator.Page, Is.EqualTo(CharacterCreator.QuickPageCaption));
            Assert.That(Buttons(creator, CharacterCreator.DetailedCaption), Has.Length.EqualTo(1), "Quick setup is the look's step too.");
        }

        // ---------------------------------------------------------------- 10. the library

        [UnityTest]
        public IEnumerator QuickWin10_TheLibraryHasOneSearchBoxNoPagerOnOnePageAndSaysHouseguest()
        {
            var creator = Open(savedHouseguests: 2);
            yield return null;
            Click(creator, "My Houseguests");
            yield return null;
            var row = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Profile search row");
            Assert.That(row.GetComponentsInChildren<TMP_InputField>(), Has.Length.EqualTo(1), "One search box.");
            Assert.That(Buttons(creator, CharacterProfileBrowser.OneBoxSearchCaption), Has.Length.EqualTo(1), "Its magnifier is in it.");
            foreach (var gone in new[] { "Search profiles", "Previous profiles", "Next profiles",
                         CharacterProfileBrowser.OneBoxPreviousCaption, CharacterProfileBrowser.OneBoxNextCaption, CharacterProfileBrowser.ClearSearchCaption })
                Assert.That(Buttons(creator, gone), Is.Empty, gone);
            Assert.That(row.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "Profile page"), Is.False, "No pager on one page.");
            var page = creator.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "My Houseguests" && rect.GetComponent<Button>() == null);
            var words = page.GetComponentsInChildren<TMP_Text>().Where(text => text.enabled && text.GetComponentInParent<Button>() == null)
                .Select(text => text.text).ToArray();
            Assert.That(words.Where(text => text.IndexOf("profile", StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty,
                "The library says houseguest.");

            var search = row.GetComponentsInChildren<TMP_InputField>().Single();
            search.text = "Guest 2";
            search.onSubmit.Invoke(search.text);
            yield return null;
            Assert.That(Buttons(creator, CharacterProfileBrowser.ClearSearchCaption), Has.Length.EqualTo(1), "A search on can be cleared, in the box.");
        }

        [UnityTest]
        public IEnumerator QuickWin10_TheLibraryPagerShowsOnASecondPage()
        {
            var creator = Open(savedHouseguests: CharacterProfileBrowser.PageSize + 1);
            yield return null;
            Click(creator, "My Houseguests");
            yield return null;
            Assert.That(Buttons(creator, CharacterProfileBrowser.OneBoxNextCaption), Has.Length.EqualTo(1));
            Assert.That(Buttons(creator, CharacterProfileBrowser.OneBoxPreviousCaption), Has.Length.EqualTo(1));
            Click(creator, CharacterProfileBrowser.OneBoxNextCaption);
            yield return null;
            Assert.That(Buttons(creator, CharacterProfileBrowser.OneBoxNextCaption).Single().IsInteractable(), Is.False, "The last page.");
        }

        /// <summary>A body source that fails every build, so the studio's preview fails.</summary>
        private sealed class FailingProvider : IModularCharacterBodyProvider
        {
            public ICharacterAppearanceCatalog Catalog => null;
            public bool TryCreate(in CharacterBodyRequest request, Transform parent, Color color, out CharacterBody body)
            { body = default; throw new InvalidOperationException("Quick wins test provider fails every build."); }
            public bool TryCreate(string id, Transform parent, Color color, out CharacterBody body)
            { body = default; throw new InvalidOperationException("Quick wins test provider fails every build."); }
            public void SetWardrobeColor(in CharacterBody body, Color color) { }
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Gamesim.Tests.PlayMode
{
    public sealed class CharacterProfileLibraryPlayModeTests
    {
        private GameObject owner, otherOwner;
        private string root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) Object.Destroy(owner);
            if (otherOwner != null) Object.Destroy(otherOwner);
            yield return null;
            if (root == null) yield break;
            string resolved = Path.GetFullPath(root);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimLibraryUiTests-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        [UnityTest]
        public IEnumerator SharedLibraryUiOwnsItsProfilesBackupsAndDeletesWithoutAffectingAnotherInstance()
        {
            root = Path.Combine(Path.GetTempPath(), "GamesimLibraryUiTests-" + Guid.NewGuid().ToString("N"));
            var library = new CharacterProfileStore(Path.Combine(root, "A"));
            var otherLibrary = new CharacterProfileStore(Path.Combine(root, "B"));
            var draft = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            draft.Name = "Isolated A";
            var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), draft);
            var otherProfile = profile.Clone();
            otherProfile.name = otherProfile.contestant.name = "Other library";
            Assert.That(library.Save(profile, out var error), Is.True, error);
            Assert.That(otherLibrary.Save(otherProfile, out error), Is.True, error);
            string path = Path.Combine(library.DirectoryPath, profile.id + ".json");
            string otherPath = Path.Combine(otherLibrary.DirectoryPath, profile.id + ".json");
            byte[] otherBefore = File.ReadAllBytes(otherPath);

            owner = new GameObject("Isolated library UI");
            var creator = CharacterCreator.Attach(owner);
            var cast = CastSelect.Attach(owner);
            creator.ConfigureProfiles(library); cast.ConfigureProfiles(library); cast.ConfigureCreator(creator);
            otherOwner = new GameObject("Independent library UI");
            var otherCreator = CharacterCreator.Attach(otherOwner);
            otherCreator.ConfigureProfiles(otherLibrary); // Must not redirect the first instance's next save.
            Assert.That(creator.ProfileStore, Is.SameAs(cast.ProfileStore));
            Assert.That(creator.ProfileStore.DirectoryPath, Is.Not.EqualTo(otherCreator.ProfileStore.DirectoryPath));
            creator.Show(new SeasonBuilder.Choice(), profile.ToDraft(), _ => Assert.Fail("Library edit cannot start a season."), () => { });
            Click(creator, "My Houseguests");
            Click(creator, "Isolated A");
            Click(creator, "Identity");
            creator.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == "Name field").text = "Isolated A edited";
            Click(creator, "My Houseguests");
            Click(creator, "Save this houseguest");
            Assert.That(File.Exists(path + ".backup"), Is.True, "Updating through the creator writes its backup in the configured library.");
            Assert.That(library.TryLoad(profile.id, out var updated, out error), Is.True, error);
            Assert.That(updated.name, Is.EqualTo("Isolated A edited"));
            Assert.That(File.ReadAllBytes(otherPath), Is.EqualTo(otherBefore));
            Assert.That(File.Exists(otherPath + ".backup"), Is.False);

            creator.Hide();
            SeasonBuilder.Choice choice = null;
            cast.Show(value => choice = value, () => { }, (_, __) => Assert.Fail("NPC editing must not replace the player."));
            cast.SetDraft(CharacterDraft.FromAppearance(CastTemplates.Find("alex-chen")));
            Click(cast, "My Houseguests");
            Assert.That(Buttons(cast, "Choose Isolated A edited"), Has.Length.EqualTo(1));
            Assert.That(Buttons(cast, "Choose Other library"), Is.Empty);
            Click(cast, "Cast slots");
            Click(cast, "Add Isolated A edited to the cast");
            Click(cast, "Add Isolated A edited to the cast");
            Click(cast, "Edit slot 1");
            Click(creator, "Identity");
            creator.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == "Name field").text = "Cast only";
            Click(creator, CharacterCreator.ApplySlotCaption);
            Click(cast, CastSelect.StartCaption);
            Assert.That(choice.Authored.Name, Is.EqualTo("Alex Chen"));
            Assert.That(choice.CustomHouseguests[0].name, Is.EqualTo("Cast only"));
            Assert.That(choice.CustomHouseguests[1].name, Is.EqualTo("Isolated A edited"));
            Assert.That(library.TryLoad(profile.id, out updated, out error), Is.True, error);
            Assert.That(updated.name, Is.EqualTo("Isolated A edited"));

            creator.Show(new SeasonBuilder.Choice(), updated.ToDraft(), _ => { }, () => { });
            Click(creator, "My Houseguests");
            Click(creator, "Delete Isolated A edited");
            Click(creator, "Cancel deletion");
            Assert.That(File.Exists(path), Is.True);
            Click(creator, "Delete Isolated A edited");
            Click(creator, "Confirm delete Isolated A edited");
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(path + ".backup"), Is.False);
            Assert.That(File.ReadAllBytes(otherPath), Is.EqualTo(otherBefore));
            Assert.That(choice.CustomHouseguests[1].name, Is.EqualTo("Isolated A edited"), "A cast snapshot survives deletion of its source library profile.");
            Assert.That(library.Delete("../B/" + profile.id, out error), Is.False);
            Assert.That(File.ReadAllBytes(otherPath), Is.EqualTo(otherBefore), "Profile IDs cannot redirect deletion outside their library.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateUsesItsOwnComparisonAndResetBaselineWithoutAnotherDraftsUndoHistory()
        {
            root = Path.Combine(Path.GetTempPath(), "GamesimLibraryUiTests-" + Guid.NewGuid().ToString("N"));
            var library = new CharacterProfileStore(root);
            var storedDraft = CharacterDraft.FromAppearance(CastTemplates.Find("alex-chen"));
            storedDraft.Name = "Library Bob";
            var stored = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), storedDraft);
            Assert.That(library.Save(stored, out var error), Is.True, error);
            string storedKey = stored.contestant.appearance.ContentKey();

            owner = new GameObject("Remix history UI");
            var creator = CharacterCreator.Attach(owner);
            creator.ConfigureProfiles(library);
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown")),
                _ => Assert.Fail("Remixing a library entry must not start a season."), () => { });
            Click(creator, "Next starting look");
            Click(creator, "Next starting look");
            Click(creator, "Undo");
            Assert.That(Buttons(creator, "Undo").Single().IsInteractable(), Is.True);
            Assert.That(Buttons(creator, "Redo").Single().IsInteractable(), Is.True);
            Click(creator, "Compare original");
            Click(creator, "My Houseguests");
            Click(creator, "Duplicate Library Bob");
            Click(creator, "Appearance");
            string baseline = creator.Draft.Appearance.ContentKey();
            Assert.That(creator.Draft.Name, Is.EqualTo("Library Bob (copy)"));
            Assert.That(Buttons(creator, "Undo").Single().IsInteractable(), Is.False);
            Assert.That(Buttons(creator, "Redo").Single().IsInteractable(), Is.False);
            Assert.That(Buttons(creator, "Compare original"), Has.Length.EqualTo(1),
                "The prior draft's comparison mode must not leak into the duplicate.");

            Click(creator, "Next starting look");
            string edited = creator.Draft.Appearance.ContentKey();
            Assert.That(edited, Is.Not.EqualTo(baseline));
            Click(creator, "Reset look");
            Assert.That(creator.Draft.Appearance.ContentKey(), Is.EqualTo(baseline),
                "Reset restores the duplicated profile, not the previous houseguest.");
            Click(creator, "Undo");
            Assert.That(creator.Draft.Appearance.ContentKey(), Is.EqualTo(edited));
            Click(creator, "Redo");
            Assert.That(creator.Draft.Appearance.ContentKey(), Is.EqualTo(baseline));
            Assert.That(library.TryLoad(stored.id, out var unchanged, out error), Is.True, error);
            Assert.That(unchanged.name, Is.EqualTo("Library Bob"));
            Assert.That(unchanged.contestant.appearance.ContentKey(), Is.EqualTo(storedKey));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProfileSearchPagesAndPortraitsKeepSelectionAndCustomSlotsIndependent()
        {
            root = Path.Combine(Path.GetTempPath(), "GamesimLibraryUiTests-" + Guid.NewGuid().ToString("N"));
            var library = new CharacterProfileStore(root);
            CharacterProfile last = null;
            for (int i = 1; i <= CharacterProfileBrowser.PageSize + 1; i++)
            {
                var draft = CharacterDraft.FromAppearance(CastTemplates.Find("alex-chen"));
                draft.Name = "Guest " + i.ToString("00");
                last = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"), draft);
                Assert.That(library.Save(last, out var error), Is.True, error);
            }
            string profilePath = Path.Combine(root, last.id + ".json");
            byte[] profileBefore = File.ReadAllBytes(profilePath);
            if (UnityEngine.EventSystems.EventSystem.current == null)
                otherOwner = new GameObject("Profile browser events", typeof(UnityEngine.EventSystems.EventSystem));
            owner = new GameObject("Shared profile browsers");
            var creator = CharacterCreator.Attach(owner);
            var cast = CastSelect.Attach(owner);
            creator.ConfigureProfiles(library); cast.ConfigureProfiles(library); cast.ConfigureCreator(creator);
            creator.Show(new SeasonBuilder.Choice(), CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown")), _ => { }, () => { });
            Click(creator, "My Houseguests");
            Assert.That(creator.GetComponentsInChildren<CharacterPortraitBinding>(), Has.Length.EqualTo(CharacterProfileBrowser.PageSize),
                "Only the visible profile page binds portraits; a large library must not queue every avatar.");
            var search = creator.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == "Profile name search");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(search.gameObject);
            search.text = " guest 07 ";
            Assert.That(search.gameObject.activeInHierarchy, Is.True, "Typing must not destroy the focused input field.");
            search.onSubmit.Invoke(search.text);
            Assert.That(Buttons(creator, "Guest 07"), Has.Length.EqualTo(1));
            Assert.That(Buttons(creator, "Guest 01"), Is.Empty);
            Assert.That(creator.GetComponentsInChildren<CharacterPortraitBinding>(), Has.Length.EqualTo(1));
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Profile name search"));
            Click(creator, "Clear search");
            Click(creator, "Next profiles");
            Assert.That(Buttons(creator, "Guest 07"), Has.Length.EqualTo(1));
            Assert.That(creator.GetComponentsInChildren<CharacterPortraitBinding>(), Has.Length.EqualTo(1));
            Assert.That(Buttons(creator, "Next profiles").Single().IsInteractable(), Is.False);
            Click(creator, "Guest 07");
            Assert.That(creator.Draft.Name, Is.EqualTo("Guest 07"));
            creator.Hide();

            SeasonBuilder.Choice committed = null;
            cast.Show(value => committed = value, () => { },
                (choice, draft) => creator.Show(choice, draft, _ => { }, () =>
                { cast.SetDraft(creator.Draft); cast.Resume(); }));
            Click(cast, "My Houseguests");
            Assert.That(cast.GetComponentsInChildren<CharacterPortraitBinding>(), Has.Length.EqualTo(CharacterProfileBrowser.PageSize));
            search = cast.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == "Profile name search");
            search.text = "GUEST 07";
            Click(cast, "Search profiles");
            Assert.That(Buttons(cast, "Choose Guest 07"), Has.Length.EqualTo(1));
            Assert.That(Buttons(cast, "Choose Guest 01"), Is.Empty);
            Assert.That(cast.GetComponentsInChildren<CharacterPortraitBinding>(), Has.Length.EqualTo(1));
            Click(cast, "Choose Guest 07");
            Assert.That(creator.Draft.Name, Is.EqualTo("Guest 07"));
            Click(creator, CharacterCreator.BackCaption);
            Click(cast, "Cast slots");
            Click(cast, "Add Guest 07 to the cast");
            Click(cast, "Add Guest 07 to the cast");
            Click(cast, CastSelect.StartCaption);
            Assert.That(committed.CustomHouseguests, Has.Count.EqualTo(2));
            Assert.That(committed.CustomHouseguests[0].id, Is.EqualTo(last.id));
            Assert.That(committed.CustomHouseguests[1], Is.Not.SameAs(committed.CustomHouseguests[0]));
            Assert.That(committed.Authored.Name, Is.EqualTo("Guest 07"), "Adding NPC slots retains the chosen player profile.");
            Assert.That(committed.Authored.Appearance, Is.Not.SameAs(committed.CustomHouseguests[0].contestant.appearance));
            Assert.That(File.ReadAllBytes(profilePath), Is.EqualTo(profileBefore));
            Assert.That(library.List(), Has.Count.EqualTo(CharacterProfileBrowser.PageSize + 1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProfileBrowserInheritsLargerTextWithoutClippingOrCoveringTheFixedFooter()
        {
            root = Path.Combine(Path.GetTempPath(), "GamesimLibraryUiTests-" + Guid.NewGuid().ToString("N"));
            var library = new CharacterProfileStore(root);
            var profile = CharacterProfile.FromDraft(Guid.NewGuid().ToString("N"),
                CharacterDraft.FromAppearance(CastTemplates.Find("alex-chen")));
            Assert.That(library.Save(profile, out var error), Is.True, error);
            owner = new GameObject("Accessible library browsers");
            var creator = CharacterCreator.Attach(owner);
            var cast = CastSelect.Attach(owner);
            creator.ConfigureProfiles(library); cast.ConfigureProfiles(library);
            creator.Show(new SeasonBuilder.Choice(), profile.ToDraft(), _ => { }, () => { });
            Click(creator, "My Houseguests");
            yield return CheckBrowserTextScale(creator, scale => creator.FontScale = scale);
            creator.Hide();
            cast.Show(_ => { }, () => { }, (_, __) => { });
            Click(cast, "My Houseguests");
            yield return CheckBrowserTextScale(cast, scale => cast.FontScale = scale);
            Click(cast, "Cast slots");
            yield return CheckBrowserTextScale(cast, scale => cast.FontScale = scale);
        }

        private static IEnumerator CheckBrowserTextScale(Component screen, Action<float> setScale)
        {
            float standardHeight = 0f;
            foreach (float scale in new[] { 1f, 1.2f })
            {
                setScale(scale);
                yield return null; // CanvasScaler observes the changed reference resolution in Update.
                Canvas.ForceUpdateCanvases();
                var search = screen.GetComponentsInChildren<TMP_InputField>()
                    .Single(field => field.name == "Profile name search");
                var row = (RectTransform)search.transform.parent;
                var scroll = search.GetComponentInParent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                var controls = row.GetComponentsInChildren<Selectable>();
                Assert.That(controls, Has.Length.EqualTo(5));
                foreach (var control in controls)
                {
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, control.transform);
                    Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(scroll.viewport.rect.xMin - .01f), control.name);
                    Assert.That(bounds.max.x, Is.LessThanOrEqualTo(scroll.viewport.rect.xMax + .01f), control.name);
                    Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - .01f), control.name);
                    Assert.That(bounds.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + .01f), control.name);
                }
                foreach (var label in row.GetComponentsInChildren<TMP_Text>())
                {
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(15f), label.name);
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, label.text + " at scale " + scale);
                }
                var footer = screen.GetComponentsInChildren<RectTransform>()
                    .Single(rect => rect.name == "Fixed footer" || rect.name == "Fixed season footer");
                var rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(footer, row);
                Assert.That(rowBounds.min.y, Is.GreaterThanOrEqualTo(footer.rect.yMax),
                    "The browser must remain above the fixed season controls.");
                var corners = new Vector3[4];
                ((RectTransform)search.transform).GetWorldCorners(corners);
                float renderedHeight = Vector3.Distance(corners[0], corners[1]);
                if (scale == 1f) standardHeight = renderedHeight;
                else Assert.That(renderedHeight / standardHeight, Is.EqualTo(1.2f).Within(.01f),
                    "The entire search row, including its text, must inherit larger-text magnification exactly once.");
            }
        }

        private static Button[] Buttons(Component root, string caption) => root.GetComponentsInChildren<Button>()
            .Where(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<TMP_Text>()?.text == caption).ToArray();
        private static void Click(Component root, string caption)
        {
            var buttons = Buttons(root, caption);
            Assert.That(buttons, Has.Length.EqualTo(1), caption);
            buttons[0].onClick.Invoke();
        }
    }
}

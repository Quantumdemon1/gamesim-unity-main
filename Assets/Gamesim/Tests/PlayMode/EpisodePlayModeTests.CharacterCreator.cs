using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The character creator, driven through its own controls.
    ///
    /// <para>The same standard as the cast screen it opens from: nothing is written until the player
    /// says so, and backing out has to land on the screen they came from with their roster, filter
    /// and pick still on it. Losing those to a trip through the creator would be a small failure that
    /// feels like a large one.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        private CharacterCreator Creator() => director.GetComponentInChildren<CharacterCreator>(true);

        private TMP_InputField PhoneIdentityField(string caption) =>
            Creator().GetComponentsInChildren<TMP_InputField>().Single(input => input.name == caption + " field");

        [UnityTest]
        public IEnumerator Creator_PhoneCreateKeepsTheQuickDraftThroughLookAndCast()
        {
            var creator = Creator();
            creator.UsePhoneIdentityLayout = true;
            CastScreen().GetComponent<FrameScaler>().UsePhoneUnits = true;
            string session = director.Snapshot.sessionId;
            yield return OpenCastScreen();
            CastButtons("Season setup").Single().onClick.Invoke();
            CastButtons(CharacterCreator.CreateCaption).Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Mode, Is.EqualTo(CharacterCreator.EntryMode.Quick));
            Assert.That(creator.GetComponent<FrameScaler>().UsePhoneUnits, Is.True);
            var name = PhoneIdentityField("Name");
            Assert.That(name.characterLimit, Is.EqualTo(CharacterDraft.NameLimit));
            Assert.That(name.restoreOriginalTextOnEscape, Is.False);
            Assert.That(name.shouldActivateOnSelect, Is.False);
            name.Select();
            yield return null;
            Assert.That(name.isFocused, Is.False, "Selection alone must leave the native keyboard closed.");
            Assert.That(CastButtons(CharacterCreator.StartCaption), Is.Empty);
            var correction = creator.GetComponentsInChildren<UnityEngine.UI.Button>().Single(button => button.name == CharacterCreator.ProblemLinkName);
            var status = correction.GetComponent<TMP_Text>();
            Assert.That(status.text, Does.Contain("Give your houseguest a name."));
            Assert.That(correction.IsInteractable(), Is.True);
            UnityEngine.Canvas.ForceUpdateCanvases();
            Assert.That(((UnityEngine.RectTransform)name.transform).rect.height, Is.GreaterThanOrEqualTo(48f));
            Assert.That(PressAt(ScreenCentre(name)), Is.SameAs(name.gameObject));
            yield return null;
            Assert.That(name.isFocused, Is.True);
            name.text = "Robin Vale";
            Assert.That(PhoneIdentityField("Name"), Is.SameAs(name));
            Assert.That(CastButtons(CharacterCreator.StartCaption), Has.Length.EqualTo(1));
            Assert.That(status.text, Is.EqualTo("Ready."));
            Assert.That(correction.IsInteractable(), Is.False);
            name.text = " ";
            name.DeactivateInputField();
            Assert.That(status.text, Does.Contain("Give your houseguest a name."));
            correction.onClick.Invoke();
            yield return null;
            Assert.That(name.isFocused, Is.True, "The validation link should take the player straight to the name.");
            name.text = "Robin Vale";
            CastButtons("Older").Single().onClick.Invoke();
            CastButtons("she/her").Single().onClick.Invoke();
            CastButtons("Starting look and body").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.GetComponent<FrameScaler>().UsePhoneUnits, Is.False);
            Assert.That(CastButtons("Next starting look"), Has.Length.EqualTo(1));
            CastButtons(CreatorSteps.Identity).Single().onClick.Invoke();
            yield return null;
            Assert.That(PhoneIdentityField("Name").text, Is.EqualTo("Robin Vale"));
            var stale = PhoneIdentityField("Name");
            var back = PhoneMenuTouchTests.PhoneExit(creator, WayOut.BackObjectName, WayOut.BackToKeeping("season setup", "draft"));
            back.onClick.Invoke();
            stale.onValueChanged.Invoke("Hidden field");
            Assert.That(creator.Draft.Name, Is.EqualTo("Robin Vale"));
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(session));
            CastButtons("Resume setup").Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Mode, Is.EqualTo(CharacterCreator.EntryMode.Quick));
            Assert.That(PhoneIdentityField("Name").text, Is.EqualTo("Robin Vale"));
            Assert.That(creator.Draft.Age, Is.EqualTo(CharacterDraft.DefaultAge + 1));
            Assert.That(creator.Draft.Pronouns, Is.EqualTo("she/her"));
            CastButtons(CharacterCreator.NextCaption(CreatorSteps.Review)).Single().onClick.Invoke();
            yield return null;
            Assert.That(creator.Page, Is.EqualTo(CreatorSteps.Review));
            Assert.That(creator.GetComponent<FrameScaler>().UsePhoneUnits, Is.False);
        }

        [UnityTest]
        public IEnumerator Creator_PhoneSlotIdentityCancelsOrAppliesOnlyTheEditedCopy()
        {
            var creator = Creator();
            creator.UsePhoneIdentityLayout = true;
            var cast = CastScreen();
            cast.GetComponent<FrameScaler>().UsePhoneUnits = true;
            string session = director.Snapshot.sessionId;
            var original = CharacterDraft.FromAppearance(CastTemplates.Find("emma-brown"));
            original.Name = "Library guest";
            var profile = CharacterProfile.FromDraft(System.Guid.NewGuid().ToString("N"), original);
            Assert.That(cast.ProfileStore.Save(profile, out var error), Is.True, error);
            yield return OpenCastScreen();
            CastButtons("Season setup").Single().onClick.Invoke();
            CastButtons(CastSelect.SeasonHouseguestsCaption).Single().onClick.Invoke();
            CastButtons("Add Library guest to the cast").Single().onClick.Invoke();
            CastButtons("Add Library guest to the cast").Single().onClick.Invoke();
            string editSlot = CastSelect.EditSeasonHouseguestCaption(1);
            CastButtons(editSlot).Single().onClick.Invoke();
            CastButtons(CreatorSteps.Identity).Single().onClick.Invoke();
            yield return null;
            Assert.That(PhoneIdentityField("Name").text, Is.EqualTo(original.Name));
            PhoneIdentityField("Name").text = "Discarded edit";
            var cancel = PhoneMenuTouchTests.PhoneExit(creator, CharacterCreator.CancelSlotCaption, CharacterCreator.CancelSlotCaption);
            Assert.That(creator.GetComponentsInChildren<UnityEngine.UI.Button>().Any(item => item.name == WayOut.BackObjectName), Is.False);
            Assert.That(PressAt(ScreenCentre(cancel)), Is.SameAs(cancel.gameObject));
            yield return null;
            Assert.That(cast.IsShowing, Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(CastButtons(editSlot).Single().gameObject));
            Assert.That(cast.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "1. Library guest"), Is.True);
            CastButtons(editSlot).Single().onClick.Invoke();
            CastButtons(CreatorSteps.Identity).Single().onClick.Invoke();
            yield return null;
            Assert.That(PhoneIdentityField("Name").text, Is.EqualTo(original.Name));
            var bio = PhoneIdentityField("Bio");
            string appearance = creator.Draft.Appearance.ContentKey();
            Assert.That(bio.lineType, Is.EqualTo(TMP_InputField.LineType.MultiLineNewline));
            Assert.That(bio.characterLimit, Is.EqualTo(CharacterDraft.BioLimit));
            Assert.That(PhoneIdentityField("Occupation").characterLimit, Is.EqualTo(CharacterDraft.ShortLimit));
            Assert.That(PhoneIdentityField("Hometown").characterLimit, Is.EqualTo(CharacterDraft.ShortLimit));
            PhoneIdentityField("Name").text = "Rey";
            PhoneIdentityField("Occupation").text = "Librarian";
            PhoneIdentityField("Hometown").text = "Oakland";
            bio.text = "First line\nSecond line";
            CastButtons(CharacterCreator.ApplySlotCaption).Single().onClick.Invoke();
            yield return null;
            Assert.That(cast.IsShowing, Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject, Is.SameAs(CastButtons(editSlot).Single().gameObject));
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(session));
            var slots = cast.GetComponentsInChildren<TMP_Text>();
            Assert.That(slots.Any(label => label.text == "1. Rey"), Is.True);
            Assert.That(slots.Any(label => label.text == "2. Library guest"), Is.True);
            Assert.That(cast.ProfileStore.List().Single(item => item.id == profile.id).name, Is.EqualTo(original.Name));
            CastButtons(editSlot).Single().onClick.Invoke();
            var applied = creator.Draft;
            Assert.That(applied.Name, Is.EqualTo("Rey"));
            Assert.That(applied.Occupation, Is.EqualTo("Librarian"));
            Assert.That(applied.Hometown, Is.EqualTo("Oakland"));
            Assert.That(applied.Bio, Is.EqualTo("First line\nSecond line"));
            Assert.That(applied.Appearance.ContentKey(), Is.EqualTo(appearance));
            Assert.That(applied.PreserveStats, Is.True);

            creator.Dismiss();
            PhoneMenuTouchTests.PhoneExit(cast, WayOut.BackObjectName, WayOut.BackTo("season setup")).onClick.Invoke();
            CastButtons("My Houseguests").Single().onClick.Invoke();
            CastButtons("Choose Library guest").Single().onClick.Invoke();
            CastButtons(CreatorSteps.Identity).Single().onClick.Invoke();
            yield return null;
            PhoneMenuTouchTests.PhoneExit(creator, WayOut.BackObjectName, WayOut.BackToKeeping("My Houseguests", "draft")).onClick.Invoke();
            Assert.That(CastButtons("Choose Library guest"), Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Creator_TheDirectorSharesAnIsolatedLibraryWithCastSelection()
        {
            var creator = Creator();
            var cast = CastScreen();
            Assert.That(creator.ProfileStore, Is.SameAs(cast.ProfileStore));
            Assert.That(creator.ProfileStore.DirectoryPath,
                Is.EqualTo(Path.GetFullPath(Path.Combine(temporaryDirectory, "Houseguests"))));
            yield return null;
        }

        private IEnumerator OpenCreator(bool detailed = true)
        {
            yield return OpenCastScreen();
            var open = CastButtons(CharacterCreator.CreateCaption);
            Assert.That(open, Has.Length.EqualTo(1), "The cast screen should offer the creator once.");
            open[0].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Quick), "Fresh Create enters Quick before any detailed work.");
            if (detailed)
            {
                CastButtons(CharacterCreator.DetailedCaption)[0].onClick.Invoke();
                yield return null; yield return null;
                Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Detailed));
            }
        }

        [UnityTest]
        public IEnumerator Creator_OpeningTheFormWritesNothing()
        {
            var before = director.Snapshot;
            string slot = director.SavePath;
            var bytes = File.Exists(slot) ? File.ReadAllBytes(slot) : null;

            yield return OpenCreator();

            Assert.That(Creator(), Is.Not.Null, "The director must attach a creator.");
            Assert.That(Creator().IsShowing, Is.True);
            Assert.That(CastScreen().IsShowing, Is.False, "The cast screen steps aside rather than stacking.");
            Assert.That(director.SavePath, Is.EqualTo(slot));
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
            if (bytes != null) Assert.That(File.ReadAllBytes(slot), Is.EqualTo(bytes));
            if (UnityEngine.Application.isBatchMode) yield return CaptureCreatorFraming("creator");
        }

        [UnityTest]
        public IEnumerator Creator_GoingBackReturnsToTheCastScreen()
        {
            var before = director.Snapshot;
            yield return OpenCreator();

            var back = CastButtons(CharacterCreator.BackCaption);
            Assert.That(back, Has.Length.EqualTo(1));
            back[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Creator().IsShowing, Is.False);
            Assert.That(CastScreen().IsShowing, Is.True,
                "Backing out lands on the grid, not on the house with nothing to go back to.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
        }

        /// <summary>
        /// Escape reaches the form on top rather than the screen underneath it. The creator draws
        /// above the cast screen, so the cast screen closing first would pull the ground out.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_EscapeClosesTheFormAndNotTheScreenUnderneath()
        {
            yield return OpenCreator();

            testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;

            Assert.That(Creator().IsShowing, Is.False);
            Assert.That(CastScreen().IsShowing, Is.True);
        }

        /// <summary>
        /// A nameless houseguest cannot start a season. The control is drawn either way — a button
        /// that vanishes is harder to find again than one that says why it will not go.
        /// </summary>
        [UnityTest]
        public IEnumerator Creator_AHouseguestWithoutANameCannotStart()
        {
            var before = director.Snapshot;
            yield return OpenCreator();

            var start = CastButtons(CharacterCreator.StartCaption);
            Assert.That(start, Has.Length.EqualTo(1));
            start[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Creator().IsShowing, Is.True, "It should still be open, with the reason on screen.");
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(before.sessionId));
        }

        [UnityTest]
        public IEnumerator Creator_TheStatControlsSpendTheAllowanceAndStop()
        {
            yield return OpenCreator();
            CastButtons("Personality")[0].onClick.Invoke();
            yield return null;
            var creator = Creator();

            for (int i = 0; i < CharacterDraft.SparePoints; i++)
            {
                var raise = CastButtons(CharacterCreator.RaiseCaption("mental"));
                Assert.That(raise, Has.Length.EqualTo(1), "point " + i);
                raise[0].onClick.Invoke();
                yield return null;
            }

            Assert.That(creator.Draft.Remaining, Is.Zero);
            Assert.That(WebTraits.Get(creator.Draft.Stats, "mental"),
                Is.EqualTo(CharacterDraft.StartingStat + CharacterDraft.SparePoints));

            Assert.That(CastButtons(CharacterCreator.RaiseCaption("social")), Is.Empty,
                "Raising another stat is disabled once the allocation is spent.");
            yield return null;
            Assert.That(WebTraits.Get(creator.Draft.Stats, "social"), Is.EqualTo(CharacterDraft.StartingStat),
                "The allowance is spent, so the control does nothing rather than going into debt.");
        }

        [UnityTest]
        public IEnumerator Creator_ATraitRaisesTwoStatsAndGivesThemBack()
        {
            yield return OpenCreator();
            CastButtons("Personality")[0].onClick.Invoke();
            yield return null;
            var creator = Creator();

            CastButtons("Competitive")[0].onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Traits, Does.Contain("Competitive"));
            Assert.That(WebTraits.Get(creator.Draft.Stats, "physical"), Is.EqualTo(7));
            Assert.That(WebTraits.Get(creator.Draft.Stats, "endurance"), Is.EqualTo(6));

            CastButtons("Competitive")[0].onClick.Invoke();
            yield return null;
            Assert.That(creator.Draft.Traits, Is.Empty);
            Assert.That(WebTraits.Get(creator.Draft.Stats, "physical"), Is.EqualTo(CharacterDraft.StartingStat));
        }

        /// <summary>The whole point of the screen: a season whose player is nobody on any card.</summary>
        [UnityTest]
        public IEnumerator Creator_StartingBuildsTheHouseguestThatWasBuilt()
        {
            var before = director.Snapshot;
            yield return OpenCreator();

            var creator = Creator();
            creator.Draft.Name = "Robin Vale";
            creator.Draft.AddTrait("Sneaky");

            CastButtons(CharacterCreator.StartCaption)[0].onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(Creator().IsShowing, Is.False);
            var state = director.Snapshot;
            Assert.That(state.sessionId, Is.Not.EqualTo(before.sessionId), "A new season should have started.");

            var you = state.Find(state.playerId);
            Assert.That(you.name, Is.EqualTo("Robin Vale"));
            Assert.That(you.traits, Does.Contain("Sneaky"));
            Assert.That(state.contestants.Count(c => c.isPlayer), Is.EqualTo(1));
            Assert.That(EpisodeValidation.TryValidate(state, out var error), Is.True, error);
        }

        [UnityTest]
        public IEnumerator Creator_CosmeticRemixPreservesBuildAndBackRetainsTheDraft()
        {
            yield return OpenCastScreen();
            var template = CastTemplates.Find("emma-brown");
            CastButtons(template.Name)[0].onClick.Invoke();
            yield return null;
            CastButtons(CharacterCreator.CustomiseCaption)[0].onClick.Invoke();
            yield return null;
            var original = CastTemplates.ToContestant(template, true);
            Assert.That(Creator().Draft.PreserveStats, Is.True);
            Assert.That(Creator().Draft.Appearance.presetId, Is.EqualTo(template.Id));
            CastButtons("Next starting look")[0].onClick.Invoke();
            yield return null;
            string appearance = Creator().Draft.Appearance.ContentKey();
            foreach (string stat in WebTraits.StatNames)
                Assert.That(WebTraits.Get(Creator().Draft.Stats, stat), Is.EqualTo(WebTraits.Get(original.stats, stat)), stat);
            Assert.That(Creator().Draft.Pronouns, Is.EqualTo(original.pronouns));
            Assert.That(Creator().Draft.Name, Is.EqualTo(original.name));

            CastButtons(CharacterCreator.BackCaption)[0].onClick.Invoke();
            yield return null;
            CastButtons("Resume setup")[0].onClick.Invoke();
            yield return null;
            Assert.That(Creator().Draft.Appearance.ContentKey(), Is.EqualTo(appearance));
            Assert.That(Creator().Draft.PreserveStats, Is.True);
        }
    }
}

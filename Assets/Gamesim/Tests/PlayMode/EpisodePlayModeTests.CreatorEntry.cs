using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamesim.Episode;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    public sealed partial class EpisodePlayModeTests
    {
        private IEnumerator CreatorEntrySubmit(string caption, bool controller = false)
        {
            var target = CastButtons(caption).Single();
            target.Select();
            yield return null;
            // Fields and previews may have rebuilt after selection; press the current control.
            target = CastButtons(caption).Single();
            target.Select();
            if (controller)
            {
                if (testGamepad == null) testGamepad = InputSystem.AddDevice<Gamepad>();
                yield return PressPad(testGamepad, GamepadButton.South);
            }
            else yield return PressKey(Key.Enter);
            yield return null; yield return null;
        }

        private void CreatorEntryName(string name) => Creator().GetComponentsInChildren<TMP_InputField>()
            .Single(field => field.name == "Name field").text = name;

        [UnityTest]
        public IEnumerator CreatorEntry_FreshQuickBackResumeAndCosmeticDetailedUseTheirOwnRoutes()
        {
            yield return OpenCreator(detailed: false);
            CreatorEntryName("Quick Robin");
            yield return CreatorEntrySubmit(CharacterCreator.BackCaption);
            yield return CreatorEntrySubmit("Resume setup");
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Quick));
            Assert.That(Creator().Draft.Name, Is.EqualTo("Quick Robin"));
            yield return CreatorEntrySubmit(CharacterCreator.BackCaption);
            // Picking another person clears the retained authored draft before the cosmetic route.
            yield return CreatorEntrySubmit("Emma Brown");
            yield return CreatorEntrySubmit(CharacterCreator.CustomiseCaption);
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Detailed));
            Assert.That(Creator().Draft.SourceTemplateId, Is.EqualTo("emma-brown"));
            Assert.That(Creator().Draft.PreserveStats, Is.True);
        }

        [UnityTest]
        public IEnumerator CreatorEntry_ModeChangesKeepTheDraftAllOutfitsAndPresetUndoRedo()
        {
            yield return OpenCreator(detailed: false);
            var creator = Creator();
            var sameDraft = creator.Draft;
            CreatorEntryName("Modular Robin");
            sameDraft.Occupation = "Designer"; sameDraft.Hometown = "Seattle"; sameDraft.Bio = "A complete draft.";
            sameDraft.AddTrait("Loyal");
            foreach (string id in new[] { "Everyday", "Competition", "Formal", "Sleepwear", "Swimwear" })
                if (!sameDraft.Appearance.outfits.Any(outfit => outfit.id == id))
                    sameDraft.Appearance.outfits.Add(new CharacterOutfit { id = id });
            AppearanceEditing.SetColor(sameDraft.Appearance, "Hair", new Color(.18f, .12f, .08f));
            sameDraft.Appearance.activeOutfit = "Formal";
            var original = sameDraft.Copy();
            yield return CreatorEntrySubmit(CharacterCreator.DetailedCaption);
            yield return CreatorEntrySubmit("Identity");
            yield return CreatorEntrySubmit(CharacterCreator.QuickCaption);
            Assert.That(creator.Draft, Is.SameAs(sameDraft));
            AssertCreatorEntryDraft(original, creator.Draft);
            yield return CreatorEntrySubmit("Next starting look");
            string replaced = creator.Draft.Appearance.ContentKey();
            Assert.That(replaced, Is.Not.EqualTo(original.Appearance.ContentKey()));
            yield return CreatorEntrySubmit(CharacterCreator.DetailedCaption);
            yield return CreatorEntrySubmit("Appearance");
            yield return CreatorEntrySubmit("Undo");
            AssertCreatorEntryDraft(original, creator.Draft);
            yield return CreatorEntrySubmit(CharacterCreator.QuickCaption);
            yield return CreatorEntrySubmit("Redo");
            Assert.That(creator.Draft.Appearance.ContentKey(), Is.EqualTo(replaced));
            Assert.That(creator.Draft.Name, Is.EqualTo(original.Name));
            Assert.That(creator.Draft.Traits, Is.EqualTo(original.Traits));
        }

        [UnityTest]
        public IEnumerator CreatorEntry_SharedReviewAndLibraryKeepTheirPageAndExplicitRenameOpensDetailed()
        {
            yield return OpenCreator(detailed: false);
            CreatorEntryName("Shared Robin");
            yield return CreatorEntrySubmit("Review");
            yield return CreatorEntrySubmit(CharacterCreator.DetailedCaption);
            Assert.That(Creator().GetComponentsInChildren<Transform>().Any(item => item.name == "Review houseguest"), Is.True);
            yield return CreatorEntrySubmit(CharacterCreator.QuickCaption);
            yield return CreatorEntrySubmit("My Houseguests");
            yield return CreatorEntrySubmit("Save this houseguest");
            yield return CreatorEntrySubmit(CharacterCreator.DetailedCaption);
            Assert.That(CastButtons("Save this houseguest"), Has.Length.EqualTo(1), "The shared library stays open.");
            yield return CreatorEntrySubmit(CharacterCreator.QuickCaption);
            yield return CreatorEntrySubmit("Rename Shared Robin");
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Detailed));
            Assert.That(Creator().GetComponentsInChildren<TMP_InputField>().Any(field => field.name == "Name field"), Is.True);
            Assert.That(Creator().Draft.Name, Is.EqualTo("Shared Robin"));
        }

        [UnityTest]
        public IEnumerator CreatorEntry_QuickReviewStartsAndReloadsTheSameHouseguest()
        {
            yield return OpenCreator(detailed: false);
            CreatorEntryName("Quick Season Robin");
            yield return CreatorEntrySubmit("they/them");
            yield return CreatorEntrySubmit("Next starting look");
            var expected = Creator().Draft.Copy();
            yield return CreatorEntrySubmit("Review");
            yield return CreatorEntrySubmit(CharacterCreator.StartCaption);
            Assert.That(Creator().IsShowing, Is.False);
            var person = director.Snapshot.Find(director.Snapshot.playerId);
            Assert.That(person.name, Is.EqualTo(expected.Name));
            Assert.That(person.pronouns, Is.EqualTo(expected.Pronouns));
            Assert.That(person.appearance.ContentKey(), Is.EqualTo(expected.Appearance.ContentKey()));
            Assert.That(File.Exists(director.SavePath), Is.True);
            yield return ReloadEpisode();
            person = director.Snapshot.Find(director.Snapshot.playerId);
            Assert.That(person.name, Is.EqualTo(expected.Name));
            Assert.That(person.appearance.ContentKey(), Is.EqualTo(expected.Appearance.ContentKey()));
        }

        [UnityTest]
        public IEnumerator CreatorEntry_FailedQuickStartResumesItsModeDraftHistoryAndErrorThenRetries()
        {
            yield return OpenCreator(detailed: false);
            CreatorEntryName("Retry Robin");
            yield return CreatorEntrySubmit("Next starting look");
            var creator = Creator();
            var draft = creator.Draft;
            var expected = draft.Copy();
            string previousSession = director.Snapshot.sessionId, previousPath = director.SavePath;
            string blocked = Path.Combine(temporaryDirectory, "blocked-creator-root");
            File.WriteAllText(blocked, "test-owned file prevents creation of a save directory");
            var saveRoot = typeof(EpisodeDirector).GetField("saveRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(saveRoot, Is.Not.Null);
            saveRoot.SetValue(director, blocked);
            try
            {
                yield return CreatorEntrySubmit(CharacterCreator.StartCaption);
                Assert.That(creator.IsShowing, Is.True);
                Assert.That(creator.Mode, Is.EqualTo(CharacterCreator.EntryMode.Quick));
                Assert.That(creator.Draft, Is.SameAs(draft));
                AssertCreatorEntryDraft(expected, creator.Draft);
                Assert.That(director.StatusMessage, Does.StartWith("New season could not be saved."));
                Assert.That(creator.GetComponentsInChildren<TMP_Text>().Any(label => label.text == director.StatusMessage), Is.True);
                Assert.That(director.Snapshot.sessionId, Is.EqualTo(previousSession));
                Assert.That(director.SavePath, Is.EqualTo(previousPath));
                Assert.That(CastButtons("Undo").Single().interactable, Is.True);
            }
            finally { saveRoot.SetValue(director, temporaryDirectory); }
            yield return CreatorEntrySubmit(CharacterCreator.StartCaption);
            Assert.That(creator.IsShowing, Is.False);
            Assert.That(director.Snapshot.sessionId, Is.Not.EqualTo(previousSession));
            Assert.That(director.Snapshot.Find(director.Snapshot.playerId).appearance.ContentKey(), Is.EqualTo(expected.Appearance.ContentKey()));
        }

        [UnityTest]
        public IEnumerator CreatorEntry_ActualKeyboardAndControllerNavigateModesAndKeepFocusVisible()
        {
            yield return OpenCreator(detailed: false);
            yield return AssertKeyboardRing("Quick creator", "Gamesim Character Creator");
            yield return CreatorEntrySubmit(CharacterCreator.DetailedCaption);
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Detailed), "Enter reached the mode control through the input module.");
            yield return CreatorEntrySubmit(CharacterCreator.QuickCaption, controller: true);
            Assert.That(Creator().Mode, Is.EqualTo(CharacterCreator.EntryMode.Quick), "Controller Submit reached the same control.");
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.transform.IsChildOf(Creator().transform), Is.True);
            yield return PressPad(testGamepad, GamepadButton.DpadDown);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(selected), "The controller moves through the scoped ring.");
            var focused = EventSystem.current.currentSelectedGameObject.GetComponent<Button>();
            if (focused != null)
                Assert.That(focused.GetComponentsInChildren<Image>().Any(image => image.name == "Focus ring" && image.enabled), Is.True);
            yield return AssertKeyboardRing("Quick creator after controller input", "Gamesim Character Creator");
        }

        [UnityTest]
        public IEnumerator CreatorEntry_QuickFitsAndRendersAt720p1080pAndFourByThreeWithLargerText()
        {
            yield return OpenCreator(detailed: false);
            CreatorEntryName("Frame Robin");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1200, 900) })
                foreach (float scale in new[] { 1f, 1.2f })
                {
                    Creator().FontScale = scale;
                    yield return null; yield return null;
                    yield return CaptureFraming("creator-quick-" + size.x + "x" + size.y + (scale > 1f ? "-large" : "-normal"),
                        width: size.x, height: size.y, arrange: () => AssertCreatorFits(Creator(), "Quick " + size + " at " + scale));
                }
            Creator().FontScale = 1f;
        }

        private static void AssertCreatorEntryDraft(CharacterDraft expected, CharacterDraft actual)
        {
            Assert.That(actual.Name, Is.EqualTo(expected.Name)); Assert.That(actual.Age, Is.EqualTo(expected.Age));
            Assert.That(actual.Pronouns, Is.EqualTo(expected.Pronouns)); Assert.That(actual.Occupation, Is.EqualTo(expected.Occupation));
            Assert.That(actual.Hometown, Is.EqualTo(expected.Hometown)); Assert.That(actual.Bio, Is.EqualTo(expected.Bio));
            Assert.That(actual.SourceTemplateId, Is.EqualTo(expected.SourceTemplateId)); Assert.That(actual.PreserveStats, Is.EqualTo(expected.PreserveStats));
            Assert.That(actual.Traits, Is.EqualTo(expected.Traits)); Assert.That(actual.Remaining, Is.EqualTo(expected.Remaining));
            foreach (string stat in WebTraits.StatNames) Assert.That(WebTraits.Get(actual.Stats, stat), Is.EqualTo(WebTraits.Get(expected.Stats, stat)), stat);
            Assert.That(actual.Appearance.ContentKey(), Is.EqualTo(expected.Appearance.ContentKey()), "Every outfit, selection and dye survives.");
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The settings and saves leftovers on the built screens: Escape and the pad's B on a confirm card
    /// take its safe answer and stay in the section (they used to take the frame's way back out to the
    /// game menu), and Recover backup is greyed with its reason where the slot has no backup. Every
    /// file these cases touch is in the fixture's own temporary save root.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuSavesLeftovers_EscapeAndBOnAConfirmCard_TakeTheSafeAnswer()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            // Twice, so the slot has a backup and Recover backup can ask.
            director.SaveNow();
            director.SaveNow();
            string path = director.SavePath;
            Assert.That(File.Exists(path + ".backup"), Is.True);
            var bytes = File.ReadAllBytes(path);
            director.OpenSettingsAt(SettingsSection.Advanced);
            yield return Frames(2);

            ButtonWithCaption(SavesWords.GoBackCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(LeftoversConfirmCard(), Is.Not.Null, "Going back to the last save asks.");
            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(LeftoversConfirmCard(), Is.Null, "Escape answers the card with its safe choice,");
            Assert.That(director.AreSettingsSectionsOpen, Is.True, "and stays in Settings,");
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Advanced), "on Advanced,");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.GoBackCaption), "with the keyboard back on the row that asked.");

            ButtonWithCaption(EpisodeDirector.RecoverBackupCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(LeftoversConfirmCard(), Is.Not.Null, "Restoring the backup asks.");
            yield return PressBack();
            yield return Frames(2);
            Assert.That(LeftoversConfirmCard(), Is.Null, "The pad's B answers it the same way,");
            Assert.That(director.AreSettingsSectionsOpen, Is.True);
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Advanced));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "and neither touched the save.");

            yield return PressKey(Key.Escape);
            yield return Frames(2);
            Assert.That(director.IsGameMenuOpen, Is.True, "With no card up, Escape is the way back again.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSavesLeftovers_RecoverBackupIsGreyedWithItsReason_WhereThereIsNoBackup()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            string backup = director.SavePath + ".backup";
            Assert.That(Path.GetFullPath(backup), Does.StartWith(Path.GetFullPath(temporaryDirectory)), "Only the fixture's own save root.");
            if (File.Exists(backup)) File.Delete(backup);
            director.OpenSettingsAt(SettingsSection.Advanced);
            yield return Frames(2);

            // The plain finder: this row is greyed on purpose, and ButtonWithCaption refuses a control that cannot be pressed.
            var recover = FindButton(EpisodeDirector.RecoverBackupCaption);
            Assert.That(recover.interactable, Is.False, "With no backup, Recover backup cannot be picked,");
            Assert.That(director.GetComponentsInChildren<TMP_Text>().Any(text => text.isActiveAndEnabled
                && text.name == EpisodeHud.WhyDisabledName && text.text == SavesWords.NoBackupWhy), Is.True, "and the line under it says why.");
            recover.onClick.Invoke();
            yield return Frames(2);
            Assert.That(LeftoversConfirmCard(), Is.Null, "A press asks nothing.");

            director.SaveNow();
            Assert.That(File.Exists(backup), Is.True, "Saved again, the slot has a backup,");
            director.OpenSettingsAt(SettingsSection.Advanced);
            yield return Frames(2);
            Assert.That(ButtonWithCaption(EpisodeDirector.RecoverBackupCaption).interactable, Is.True, "and Recover backup can be picked.");
            director.ClosePanels();
            yield return null;
        }

        private RectTransform LeftoversConfirmCard() => director.GetComponentsInChildren<RectTransform>()
            .SingleOrDefault(rect => rect.gameObject.activeInHierarchy && rect.name == SettingsWords.ConfirmCardName);
    }
}

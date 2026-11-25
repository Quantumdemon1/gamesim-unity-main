using System;
using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Menu slice 4, part a, on the built screens: the Saves section's season cards (the running one
    /// first and marked "Playing now", the rest newest first, a save that cannot be opened with Delete
    /// alone, and "No saved seasons yet."), Load from the main menu at once and in the house after a
    /// confirm that saves the season left behind first, a failed save that loads nothing, Delete picked
    /// then confirmed with the list redrawn and the keyboard on a neighbour, Advanced's tools picked then
    /// confirmed, and the main menu's "Load a season".
    ///
    /// <para>Every save these cases list, load or delete is in the fixture's own temporary save root
    /// (LoadIsolatedEpisode sets EpisodeDirector.SaveRootOverride and checks the director's save path
    /// is inside it; every extra season is written by <see cref="MenuSavesOtherSeason"/> into that same
    /// folder). Every case holds the house first, and every state read after a load is read in the
    /// frame of the press, before anything else can run.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuSaves_TheListShowsEverySeason_PlayingNowFirst_ThenNewestFirst()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            var nowUtc = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
            string older = MenuSavesOtherSeason(nowUtc.AddDays(-3));
            string newer = MenuSavesOtherSeason(nowUtc.AddHours(-2));
            string broken = Path.Combine(temporaryDirectory, "import-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(broken, "not a save at all");
            File.SetLastWriteTimeUtc(broken, nowUtc.AddDays(-1));
            var held = SettingsWords.Clock;
            try
            {
                SettingsWords.Clock = () => nowUtc.ToLocalTime();
                director.OpenSettingsAt(SettingsSection.Saves);
                yield return Frames(2);
                string running = MenuSavesKey(director.SavePath);
                Assert.That(MenuSavesCards().Select(card => card.name), Is.EqualTo(new[] { running, MenuSavesKey(newer), MenuSavesKey(broken), MenuSavesKey(older) }
                    .Select(SavesWords.CardName)), "The season being played first, then the rest newest first.");

                var playing = MenuSavesCard(running);
                Assert.That(MenuSavesTexts(playing), Does.Contain(SavesWords.PlayingNow), "The running season says \"Playing now\" in words,");
                Assert.That(playing.GetComponentsInChildren<Button>().Select(button => button.name), Is.EqualTo(new[] { SavesWords.SaveNowCaption }),
                    "and has its Save now and no Delete.");
                Assert.That(ShellLabelShown(SavesWords.PlayingLine), Is.True, "A muted line says why.");
                var state = director.Snapshot;
                string details = SavesWords.Details(state.week, state.contestants.Count(actor => actor.status == Gamesim.Simulation.ContestantStatus.Active));
                Assert.That(MenuSavesTexts(playing), Does.Contain(details), "Its week and who is left.");

                var next = MenuSavesCard(MenuSavesKey(newer));
                Assert.That(MenuSavesTexts(next), Does.Contain(state.Find(state.playerId).name), "A season is named by the player's houseguest,");
                Assert.That(MenuSavesTexts(next), Does.Contain(details));
                Assert.That(MenuSavesTexts(next), Does.Contain("saved 2 hours ago"), "and says when it was saved, by the one clock.");
                Assert.That(next.GetComponentsInChildren<Button>().Select(button => button.GetComponentInChildren<TMP_Text>().text),
                    Is.EqualTo(new[] { SavesWords.LoadCaption, SavesWords.DeleteCaption }), "Load, then Delete.");
                Assert.That(MenuSavesTexts(MenuSavesCard(MenuSavesKey(older))), Does.Contain("saved 3 days ago"));

                var unreadable = MenuSavesCard(MenuSavesKey(broken));
                Assert.That(MenuSavesTexts(unreadable), Does.Contain(SavesWords.Unreadable), "A save that cannot be read says so,");
                Assert.That(unreadable.GetComponentsInChildren<Button>().Select(button => button.name),
                    Is.EqualTo(new[] { SavesWords.ControlName(SavesWords.DeleteCaption, MenuSavesKey(broken)) }), "with Delete alone.");
                Assert.That(ShellLabelShown(SavesWords.NoSaves), Is.False);
            }
            finally { SettingsWords.Clock = held; }
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSaves_WithNoSeasonSaved_TheListSaysSo_AndTheMainMenuOffersNoLoad()
        {
            HoldTheHouseForTheFixture();
            // A fresh fixture has a save path and no file on it, as a first launch has.
            var store = new EpisodeSaveStore(director.SavePath);
            foreach (var path in new[] { store.SavePath, store.BackupPath })
                if (File.Exists(path)) File.Delete(path);
            director.OpenMainMenu();
            yield return Frames(2);
            Assert.That(CastButtons(MainMenu.ContinueCaption), Is.Empty, "A first launch's menu: nothing to continue,");
            Assert.That(CastButtons(MainMenu.LoadCaption), Is.Empty, "and nothing to load.");
            Assert.That(Menu().OffersLoad, Is.False);

            CastButtons(MainMenu.SettingsCaption)[0].onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Saves));
            Assert.That(ShellLabelShown(SavesWords.NoSaves), Is.True, "Saves says there is none,");
            Assert.That(MenuSavesCards(), Is.Empty, "and shows no card: the stand-in season behind a first launch is nobody's.");
            Hud.PageWayBack.onClick.Invoke();
            yield return Frames(2);
            Assert.That(Menu().IsShowing, Is.True);
        }

        [UnityTest]
        public IEnumerator MenuSaves_TheMainMenusLoadOpensSaves_AndLoadsAtOnce_TheSeasonOnDisk()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            string other = MenuSavesOtherSeason(DateTime.UtcNow.AddHours(-1));
            var otherBytes = File.ReadAllBytes(other);
            director.OpenMainMenu();
            yield return Frames(2);
            Assert.That(CastButtons(MainMenu.LoadCaption), Has.Length.EqualTo(1), "A season is saved: the main menu offers \"Load a season\".");

            // Its way back goes to the main menu.
            CastButtons(MainMenu.LoadCaption)[0].onClick.Invoke();
            yield return Frames(2);
            Assert.That(Menu().IsShowing, Is.False);
            Assert.That(director.AreSettingsSectionsOpen && !director.IsGameMenuOpen, Is.True, "It opens Settings, with no game menu card,");
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Saves), "on Saves,");
            Assert.That(WayOut.CaptionOf(Hud.PageWayBack), Is.EqualTo(WayOut.BackTo(WayOut.MainMenu)), "whose way back is the main menu's,");
            Hud.PageWayBack.onClick.Invoke();
            yield return Frames(2);
            Assert.That(Menu().IsShowing, Is.True, "and goes there.");

            CastButtons(MainMenu.LoadCaption)[0].onClick.Invoke();
            yield return Frames(2);
            var load = MenuSavesControl(SavesWords.ControlName(SavesWords.LoadCaption, MenuSavesKey(other)));
            Assert.That(load, Is.Not.Null, "The other season's card has its Load.");
            load.onClick.Invoke();
            // Read in the frame of the press: nothing has run since the load.
            Assert.That(MenuSavesConfirmCard(), Is.Null, "From the main menu it loads at once, asking nothing.");
            Assert.That(Path.GetFullPath(director.SavePath), Is.EqualTo(Path.GetFullPath(other)), "The season loaded is the one on that card,");
            Assert.That(File.ReadAllBytes(other), Is.EqualTo(otherBytes), "loading writes nothing to it,");
            Assert.That(new EpisodeSaveStore(other).TryLoad(out var disk, out var why), Is.True, why);
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(disk)), "and the season in play is the one on disk, field for field.");
            Assert.That(director.IsPanelOpen, Is.False, "The house is handed over,");
            Assert.That(Menu().IsShowing, Is.False, "with the menu gone.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSaves_LoadInTheHouseAsksFirst_ThenSavesTheSeasonLeft_AndLoadsTheOther()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            string playing = Path.GetFullPath(director.SavePath);
            string other = MenuSavesOtherSeason(DateTime.UtcNow.AddHours(-1));
            var otherBytes = File.ReadAllBytes(other);
            director.OpenSettingsAt(SettingsSection.Saves);
            yield return Frames(2);
            string loadName = SavesWords.ControlName(SavesWords.LoadCaption, MenuSavesKey(other));

            MenuSavesControl(loadName).onClick.Invoke();
            yield return Frames(2);
            Assert.That(Path.GetFullPath(director.SavePath), Is.EqualTo(playing), "The first press only asks.");
            var card = MenuSavesConfirmCard();
            Assert.That(card, Is.Not.Null, "A confirm card,");
            var state = director.Snapshot;
            Assert.That(MenuSavesTexts(card), Does.Contain(SavesWords.LoadQuestion(state.Find(state.playerId).name, state.week)), "asking in words,");
            Assert.That(card.GetComponentsInChildren<Button>().Select(button => button.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(new[] { SavesWords.KeepPlaying, SavesWords.SaveAndLoad }), "with the safe choice first,");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.KeepPlaying), "which has the keyboard.");

            MenuSavesControl(SavesWords.KeepPlaying).onClick.Invoke();
            yield return Frames(2);
            Assert.That(MenuSavesConfirmCard(), Is.Null, "Keep playing puts the card away,");
            Assert.That(Path.GetFullPath(director.SavePath), Is.EqualTo(playing), "and loads nothing.");

            MenuSavesControl(loadName).onClick.Invoke();
            yield return Frames(2);
            // The season as it is left: the panel is open, so the house's clock is held and owes nothing.
            string left = JsonUtility.ToJson(director.Snapshot);
            MenuSavesControl(SavesWords.SaveAndLoad).onClick.Invoke();
            Assert.That(Path.GetFullPath(director.SavePath), Is.EqualTo(Path.GetFullPath(other)), "Save and load loads the other season,");
            Assert.That(File.ReadAllBytes(other), Is.EqualTo(otherBytes), "writing nothing to it,");
            Assert.That(new EpisodeSaveStore(other).TryLoad(out var disk, out var why), Is.True, why);
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(JsonUtility.ToJson(disk)), "and plays exactly what is on disk.");
            Assert.That(new EpisodeSaveStore(playing).TryLoad(out var behind, out why), Is.True, why);
            Assert.That(JsonUtility.ToJson(behind), Is.EqualTo(left), "The season left behind is on disk as it was when it was left.");
            Assert.That(director.IsPanelOpen, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSaves_WhenTheSeasonLeftCannotBeSaved_NothingIsLoaded_AndTheCardSaysWhy()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            string playing = Path.GetFullPath(director.SavePath);
            string other = MenuSavesOtherSeason(DateTime.UtcNow.AddHours(-1));
            var otherBytes = File.ReadAllBytes(other);
            var playingBytes = File.ReadAllBytes(playing);
            director.OpenSettingsAt(SettingsSection.Saves);
            yield return Frames(2);
            string loadName = SavesWords.ControlName(SavesWords.LoadCaption, MenuSavesKey(other));
            MenuSavesControl(loadName).onClick.Invoke();
            yield return Frames(2);
            string before = JsonUtility.ToJson(director.Snapshot);
            // From here this director can save nothing (the Editor's reload diagnostic): the save first fails.
            director.FreezeForReloadForDiagnostics();
            MenuSavesControl(SavesWords.SaveAndLoad).onClick.Invoke();
            Assert.That(Path.GetFullPath(director.SavePath), Is.EqualTo(playing), "Nothing is loaded:");
            Assert.That(JsonUtility.ToJson(director.Snapshot), Is.EqualTo(before), "the season in play is the one it was,");
            Assert.That(File.ReadAllBytes(playing), Is.EqualTo(playingBytes), "its file as it was,");
            Assert.That(File.ReadAllBytes(other), Is.EqualTo(otherBytes), "and the other season's untouched.");
            yield return Frames(2);
            var line = director.GetComponentsInChildren<TMP_Text>().FirstOrDefault(text => text.isActiveAndEnabled && text.name == SavesWords.FailureLineName);
            Assert.That(line, Is.Not.Null, "A line under the card says why,");
            Assert.That(line.text, Does.StartWith(SavesWords.CouldNotSave), "in words.");
            Assert.That(MenuSavesConfirmCard(), Is.Not.Null, "The question stays, so Keep playing can put it away.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSaves_DeleteIsPickedThenConfirmed_TheListRedraws_AndTheKeyboardLandsOnTheNextCard()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            string newer = MenuSavesOtherSeason(DateTime.UtcNow.AddHours(-1));
            string older = MenuSavesOtherSeason(DateTime.UtcNow.AddDays(-2));
            File.WriteAllText(newer + ".backup", "the store's backup of that season");
            director.OpenSettingsAt(SettingsSection.Saves);
            yield return Frames(2);
            string running = MenuSavesKey(director.SavePath);
            Assert.That(MenuSavesControl(SavesWords.ControlName(SavesWords.DeleteCaption, running)), Is.Null, "The season being played has no Delete.");
            string deleteName = SavesWords.ControlName(SavesWords.DeleteCaption, MenuSavesKey(newer));

            MenuSavesControl(deleteName).onClick.Invoke();
            yield return Frames(2);
            Assert.That(File.Exists(newer), Is.True, "The first press only asks.");
            var card = MenuSavesConfirmCard();
            Assert.That(card, Is.Not.Null);
            var state = director.Snapshot;
            Assert.That(MenuSavesTexts(card), Does.Contain(SavesWords.DeleteQuestion(state.Find(state.playerId).name, state.week)));
            Assert.That(card.GetComponentsInChildren<Button>().Select(button => button.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(new[] { SavesWords.KeepIt, SavesWords.DeleteThisSeason }));
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.KeepIt), "Keep it has the keyboard.");
            MenuSavesControl(SavesWords.KeepIt).onClick.Invoke();
            yield return Frames(2);
            Assert.That(MenuSavesConfirmCard(), Is.Null);
            Assert.That(File.Exists(newer), Is.True, "Keep it keeps it.");

            MenuSavesControl(deleteName).onClick.Invoke();
            yield return Frames(2);
            MenuSavesControl(SavesWords.DeleteThisSeason).onClick.Invoke();
            yield return Frames(2);
            Assert.That(File.Exists(newer), Is.False, "Delete this season deletes it,");
            Assert.That(File.Exists(newer + ".backup"), Is.False, "with the file the store kept beside it,");
            Assert.That(File.Exists(older) && File.Exists(director.SavePath), Is.True, "and nothing else.");
            Assert.That(MenuSavesCards().Select(card2 => card2.name), Is.EqualTo(new[] { running, MenuSavesKey(older) }.Select(SavesWords.CardName)),
                "The list is drawn again without it.");
            Assert.That(director.StatusMessage, Is.EqualTo(SavesWords.Deleted(state.Find(state.playerId).name, state.week)), "The line says what went.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.ControlName(SavesWords.LoadCaption, MenuSavesKey(older))),
                "The keyboard lands on the card after the one deleted.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSaves_AdvancedToolsAskFirst_WithTheSafeChoiceOnTheKeyboard()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.SaveNow();
            // Saved twice, so the slot has a backup: Recover backup is greyed where there is none.
            director.SaveNow();
            string path = director.SavePath;
            var bytes = File.ReadAllBytes(path);
            director.OpenSettingsAt(SettingsSection.Advanced);
            yield return Frames(2);

            ButtonWithCaption(SavesWords.GoBackCaption).onClick.Invoke();
            yield return Frames(2);
            var card = MenuSavesConfirmCard();
            Assert.That(card, Is.Not.Null, "Going back to the last save asks first,");
            Assert.That(MenuSavesTexts(card), Does.Contain(SavesWords.GoBackQuestion), "saying what is lost,");
            Assert.That(card.GetComponentsInChildren<Button>().Select(button => button.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(new[] { SavesWords.KeepPlaying, SavesWords.GoBackResult }), "with a button that names the result,");
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.KeepPlaying), "and the safe choice on the keyboard.");
            MenuSavesControl(SavesWords.KeepPlaying).onClick.Invoke();
            yield return Frames(2);
            Assert.That(MenuSavesConfirmCard(), Is.Null);
            Assert.That(director.AreSettingsSectionsOpen, Is.True, "Keep playing stays in Advanced.");

            ButtonWithCaption(EpisodeDirector.RecoverBackupCaption).onClick.Invoke();
            yield return Frames(2);
            card = MenuSavesConfirmCard();
            Assert.That(card, Is.Not.Null, "Restoring the backup asks first,");
            Assert.That(MenuSavesTexts(card), Does.Contain(SavesWords.RestoreQuestion));
            Assert.That(card.GetComponentsInChildren<Button>().Select(button => button.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(new[] { SavesWords.KeepMySave, SavesWords.RestoreResult }));
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.KeepMySave));
            MenuSavesControl(SavesWords.KeepMySave).onClick.Invoke();
            yield return Frames(2);
            Assert.That(MenuSavesConfirmCard(), Is.Null);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "Neither safe choice touched the save.");

            ButtonWithCaption(SavesWords.GoBackCaption).onClick.Invoke();
            yield return Frames(2);
            ButtonWithCaption(SavesWords.GoBackResult).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "The result button goes back to the last save,");
            Assert.That(director.StatusMessage, Does.StartWith("Local episode loaded"), "as Reload always did.");
        }

        [UnityTest]
        public IEnumerator PhoneSaves_CancelAndStaleDeleteKeepFilesAndDeletionReturnsToAdjacentSlot()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            director.SaveNow();
            var now = DateTime.UtcNow;
            var paths = Enumerable.Range(0, 10).Select(i => MenuSavesOtherSeason(now.AddHours(-i - 1))).ToArray();
            var original = paths.Select(File.ReadAllBytes).ToArray();
            Hud.UsePhoneSettingsLayout = true;
            director.OpenSettingsAt(SettingsSection.Saves);
            yield return Frames(2);
            Assert.That(MenuSavesControl(SavesWords.CardName(MenuSavesKey(paths[8]))), Is.Null);
            PhoneReviewAction("More saved seasons").onClick.Invoke();
            string key = MenuSavesKey(paths[8]);
            PhoneReviewAction(SavesWords.CardName(key)).onClick.Invoke();
            PhoneMenuTouchTests.PhoneExit(director, WayOut.BackObjectName, WayOut.BackTo("saved seasons"));
            PhoneReviewAction(SavesWords.ControlName(SavesWords.DeleteCaption, key)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.KeepIt));
            var oldConfirmation = PhoneReviewAction(SavesWords.DeleteThisSeason).onClick;
            PhoneMenuTouchTests.PhoneExit(director, WayOut.CloseObjectName, WayOut.CloseCaption);
            Hud.WayBackAction();
            oldConfirmation.Invoke();
            Assert.That(File.ReadAllBytes(paths[8]), Is.EqualTo(original[8]));
            PhoneReviewAction(SavesWords.ControlName(SavesWords.DeleteCaption, key)).onClick.Invoke();
            PhoneReviewAction(SavesWords.DeleteThisSeason).onClick.Invoke();
            yield return Frames(2);
            Assert.That(File.Exists(paths[8]), Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.CardName(MenuSavesKey(paths[9]))));
            for (int i = 0; i < paths.Length; i++)
                if (i != 8) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(original[i]));
        }

        [UnityTest]
        public IEnumerator PhoneSaves_LoadRequiresConfirmationAndSavesTheSeasonLeftBehind()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            director.SaveNow();
            string playing = director.SavePath;
            string other = MenuSavesOtherSeason(DateTime.UtcNow.AddDays(-1));
            var bytes = File.ReadAllBytes(other);
            string key = MenuSavesKey(other);
            Hud.UsePhoneSettingsLayout = true;
            director.OpenSettingsAt(SettingsSection.Saves);
            yield return Frames(2);
            Assert.That(MenuSavesControl(SavesWords.ControlName(SavesWords.DeleteCaption, MenuSavesKey(playing))), Is.Null);
            PhoneReviewAction(SavesWords.CardName(key)).onClick.Invoke();
            PhoneReviewAction(SavesWords.ControlName(SavesWords.LoadCaption, key)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.KeepPlaying));
            Assert.That(director.SavePath, Is.EqualTo(playing));
            var left = director.Snapshot;
            PhoneReviewAction(SavesWords.SaveAndLoad).onClick.Invoke();
            Assert.That(Path.GetFullPath(director.SavePath), Is.EqualTo(Path.GetFullPath(other)));
            Assert.That(File.ReadAllBytes(other), Is.EqualTo(bytes));
            Assert.That(new EpisodeSaveStore(playing).TryLoad(out var saved, out var why), Is.True, why);
            AssertEquivalent(left, saved);
        }

        [UnityTest]
        public IEnumerator PhoneSaves_PauseMenuReturnsToHouseAndRecoveryToolsStayReachable()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            Hud.UsePhoneSettingsLayout = true;
            director.OpenSettings();
            yield return Frames(2);
            PhoneReviewAction(EpisodeHud.GameMenuRowPrefix + SettingsWords.SaveCaption).onClick.Invoke();
            Assert.That(File.Exists(director.SavePath), Is.True);
            yield return Frames(2);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(EpisodeHud.GameMenuRowPrefix + SettingsWords.SaveCaption));
            PhoneReviewAction(EpisodeHud.GameMenuRowPrefix + SettingsWords.SettingsCaption).onClick.Invoke();
            PhoneMenuTouchTests.PhoneExit(director, WayOut.BackObjectName, WayOut.BackTo(SettingsWords.GameMenuPlace));
            PhoneReviewAction("Other settings").onClick.Invoke();
            PhoneReviewAction(SettingsWords.Name(SettingsSection.Advanced)).onClick.Invoke();
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Advanced));
            director.ShowSettingsSection(SettingsSection.Saves);
            Hud.WayBackAction();
            Assert.That(director.IsGameMenuOpen, Is.True);
            RenderHudForTheCurrentCanvas();
            yield return Frames(2);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(EpisodeHud.GameMenuRowPrefix + SettingsWords.SettingsCaption));
            PhoneReviewAction(EpisodeHud.GameMenuRowPrefix + SettingsWords.ResumeCaption).onClick.Invoke();
            Assert.That(director.IsPanelOpen, Is.False);
            director.OpenSettings();
            PhoneMenuTouchTests.PhoneExit(director, WayOut.CloseObjectName, WayOut.CloseCaption).onClick.Invoke();
            Assert.That(director.IsPanelOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator PhoneSaves_RetryReopensTheHeldSlotAndSelectsLoad()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            director.SaveNow();
            Hud.UsePhoneSettingsLayout = true;
            string other = MenuSavesOtherSeason(DateTime.UtcNow);
            string key = MenuSavesKey(other);
            using (new FileStream(other, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                director.OpenSettingsAt(SettingsSection.Saves);
                PhoneReviewAction(SavesWords.CardName(key)).onClick.Invoke();
                Assert.That(MenuSavesControl(SavesWords.ControlName(SavesWords.DeleteCaption, key)), Is.Null);
            }
            PhoneReviewAction(SavesWords.ControlName(SavesWords.TryAgainCaption, key)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SavesWords.ControlName(SavesWords.LoadCaption, key)));
            Assert.That(director.SavePath, Is.Not.EqualTo(other));
        }

        [UnityTest]
        public IEnumerator PhoneSaves_RetiredImportFieldCannotChangeTheNextForm()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            Hud.UsePhoneSettingsLayout = true;
            director.OpenSettingsAt(SettingsSection.Saves);
            PhoneReviewAction(EpisodeDirector.ImportHeading).onClick.Invoke();
            var field = director.GetComponentInChildren<PhoneMenuPage>().GetComponentInChildren<TMP_InputField>();
            field.text = "chosen-file.json";
            var retired = field.onValueChanged;
            Hud.WayBackAction();
            PhoneReviewAction(EpisodeDirector.ImportHeading).onClick.Invoke();
            retired.Invoke("old-file.json");
            Hud.WayBackAction();
            PhoneReviewAction(EpisodeDirector.ImportHeading).onClick.Invoke();
            Assert.That(director.GetComponentInChildren<PhoneMenuPage>().GetComponentInChildren<TMP_InputField>().text,
                Is.EqualTo("chosen-file.json"));
            yield return null;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Another season in the fixture's own save root: the running season under a session id of its
        /// own, saved by the store as the game saves one, its file dated <paramref name="savedUtc"/>.
        /// </summary>
        private string MenuSavesOtherSeason(DateTime savedUtc)
        {
            Assert.That(Path.GetFullPath(director.SavePath), Does.StartWith(Path.GetFullPath(temporaryDirectory)), "Only the fixture's own save root.");
            var state = director.Snapshot;
            state.sessionId = Guid.NewGuid().ToString("N");
            string path = Path.Combine(temporaryDirectory, "episode-" + Guid.NewGuid().ToString("N") + ".json");
            new EpisodeSaveStore(path).Save(state);
            File.SetLastWriteTimeUtc(path, savedUtc);
            return path;
        }

        private static string MenuSavesKey(string path) => Path.GetFileNameWithoutExtension(path);

        /// <summary>The season cards on screen, top to bottom.</summary>
        private RectTransform[] MenuSavesCards() => director.GetComponentsInChildren<RectTransform>()
            .Where(rect => rect.gameObject.activeInHierarchy && rect.name.StartsWith(SavesWords.CardName(""), StringComparison.Ordinal)).ToArray();

        private RectTransform MenuSavesCard(string key) => MenuSavesCards().Single(card => card.name == SavesWords.CardName(key));

        private static string[] MenuSavesTexts(Component card) => card.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();

        private Button MenuSavesControl(string name) => director.GetComponentsInChildren<Button>()
            .SingleOrDefault(button => button.IsActive() && button.name == name);

        private RectTransform MenuSavesConfirmCard() => director.GetComponentsInChildren<RectTransform>()
            .SingleOrDefault(rect => rect.gameObject.activeInHierarchy && rect.name == SettingsWords.ConfirmCardName);
    }
}

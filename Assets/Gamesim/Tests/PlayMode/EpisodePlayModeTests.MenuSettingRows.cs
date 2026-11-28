using System.Collections;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
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
    /// Menu slice 4, part b, on the built screens: the Sound, Display, Accessibility and Controls
    /// sections show every setting as its name and its value (ruling S5); each row, changed by Enter,
    /// left or right, keeps the same preference key and value and changes the game as the control it
    /// replaced did (B2); a value changed outside its row is drawn at once; left and right wait while
    /// a rebinding listens (B3); and a row that cannot be used says why.
    ///
    /// <para>Every case holds the house first, so no houseguest's invitation takes a press, and finds a
    /// row again after each press: a press draws the panel again (the old row is switched off at once
    /// and destroyed at the frame's end), and the new row reads the new value in the same frame.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuSettingRows_EverySectionShowsItsSettingsAsNamesAndValues()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            var audio = director.GetComponent<HouseAudio>();

            director.OpenSettingsAt(SettingsSection.Sound);
            yield return Frames(2);
            AssertRowReads(SettingRowWords.AllSoundRow, SettingsWords.SwitchValue(!audio.Muted));
            AssertRowReads(SettingRowWords.MasterVolumeRow, Mathf.RoundToInt(audio.Volume * 100f) + "%");
            Assert.That(SettingRowValue(SettingRowWords.MusicRow), Is.EqualTo("On").Or.EqualTo("Off"), "Music reads On or Off.");
            foreach (var channel in new[] { SoundChannel.Music, SoundChannel.Effects, SoundChannel.Interface, SoundChannel.Voice })
                AssertRowReads(SoundPictures.MeterCaption(channel), director.ChannelVolumePercent(channel) + "%");
            AssertNoOldButtons("Sound");

            director.ShowSettingsSection(SettingsSection.Display);
            yield return null;
            AssertRowReads(SettingRowWords.CompactHudRow, SettingsWords.SwitchValue(director.CompactHud));
            if (QualitySettings.names.Length > 1)
                AssertRowReads(SettingRowWords.QualityRow, EpisodeDirector.QualityName(director.QualityTier));
            else
            {
                // One level (a batch run often offers only one): the row cannot be used, says why, and a screen reader hears the reason too.
                AssertRowReads(SettingRowWords.QualityRow, EpisodeDirector.QualityName(0), SettingRowWords.OneQualityLevelLine);
                Assert.That(WhyLineShown(SettingRowWords.OneQualityLevelLine), Is.True, "One quality level: the row says why it cannot be used.");
            }
            AssertRowReads(SettingRowWords.VSyncRow, SettingsWords.SwitchValue(director.VSync));
            AssertRowReads(SettingRowWords.FrameRateRow, DisplayChoices.FrameLimitName(director.FrameLimit));
            AssertRowReads(SettingRowWords.DisplayModeRow, DisplayChoices.ModeName(director.DisplayModeSetting));
            AssertRowReads(SettingRowWords.ResolutionRow, director.Resolution);
            AssertRowReads(SettingRowWords.HouseViewRow, director.ShowsWholeHouse ? "Whole house" : "One room");
            AssertNoOldButtons("Display");

            director.ShowSettingsSection(SettingsSection.Accessibility);
            yield return null;
            AssertRowReads(SettingRowWords.ReducedMotionRow, SettingsWords.SwitchValue(cameraRig.ReducedMotion));
            AssertRowReads(SettingRowWords.ReducedSoundRow, SettingsWords.SwitchValue(director.ReducedAudio));
            AssertRowReads(SettingRowWords.TextSizeRow, SettingRowWords.TextSizeValue(director.TextSize));
            AssertRowReads(SettingRowWords.CaptionTimeRow, director.CaptionTimeSetting == CaptionTime.Longer ? "Longer" : "Standard");
            AssertRowReads(SettingRowWords.ConversationLinesRow, director.AutoContinueLines ? "Move on" : "Wait for me");
            AssertRowReads(SettingRowWords.CeremonyPaceRow, director.CeremonyPaceSetting == CeremonyPace.Quick ? "Quick" : "Suspenseful");
            AssertNoOldButtons("Accessibility");

            director.ShowSettingsSection(SettingsSection.Controls);
            yield return null;
            AssertRowReads(SettingRowWords.CameraSpeedRow, EpisodeDirector.CameraSpeedName(Mathf.RoundToInt(director.CameraSpeed * 100f)));
            AssertRowReads(SettingRowWords.CameraTiltRow, director.InvertTilt ? "Inverted" : "Normal");
            AssertRowReads(SettingRowWords.EdgePanningRow, SettingsWords.SwitchValue(director.EdgePanOn));
            var tour = FindButton(EpisodeDirector.ReplayTourCaption);
            Assert.That(tour.interactable, Is.True, "In the house the tour can be replayed,");
            Assert.That(WhyLineShown(null), Is.False, "and no row in Controls says why it cannot be used.");
            AssertNoOldButtons("Controls");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSettingRows_SoundAndAccessibilityKeepTheirKeysAndValues()
        {
            yield return OpenIsolatedVolumeSettings();
            var audio = director.GetComponent<HouseAudio>();
            var hud = Hud;

            PressSettingRow(SettingRowWords.AllSoundRow, "Off");
            Assert.That(StoredPreference("Gamesim.Muted"), Is.EqualTo(1), "Off is the old 'Mute sound': Gamesim.Muted 1.");
            Assert.That(audio.Muted, Is.True, "and the sound is muted.");
            PressSettingRow(SettingRowWords.AllSoundRow, "On");
            Assert.That((StoredPreference("Gamesim.Muted"), audio.Muted), Is.EqualTo((0, false)));

            PressSettingRow(SettingRowWords.MusicRow, "Off");
            Assert.That(StoredPreference("Gamesim.Music"), Is.EqualTo(0), "Off is the old 'Turn music off': Gamesim.Music 0.");
            Assert.That(audio.CurrentMusic, Is.EqualTo(HouseAudio.Music.Silent), "and the music goes.");
            PressSettingRow(SettingRowWords.MusicRow, "On");
            Assert.That(StoredPreference("Gamesim.Music"), Is.EqualTo(1));

            int master = StoredPreference("Gamesim.Volume");
            int down = SettingsWords.SliderStepped(master, -1);
            StepSettingRow(SettingRowWords.MasterVolumeRow, MoveDirection.Left, down + "%");
            Assert.That(StoredPreference("Gamesim.Volume"), Is.EqualTo(down), "Left is the old 'Volume down': ten points, kept at once.");
            Assert.That(audio.Volume, Is.EqualTo(down / 100f).Within(1e-5f));
            StepSettingRow(SettingRowWords.MasterVolumeRow, MoveDirection.Right, SettingsWords.SliderStepped(down, 1) + "%");
            StepSettingRow(SoundPictures.MeterCaption(SoundChannel.Effects), MoveDirection.Left, "90%");
            Assert.That(StoredPreference(SoundPictures.EffectsVolumeKey), Is.EqualTo(90), "A channel's left is its old 'volume down'.");
            Assert.That(audio.ChannelVolume(SoundChannel.Effects), Is.EqualTo(.9f).Within(1e-5f));
            StepSettingRow(SoundPictures.MeterCaption(SoundChannel.Effects), MoveDirection.Right, "100%");
            Assert.That(StoredPreference(SoundPictures.EffectsVolumeKey), Is.EqualTo(100));

            director.ShowSettingsSection(SettingsSection.Accessibility);
            yield return null;
            PressSettingRow(SettingRowWords.ReducedMotionRow, "On");
            Assert.That(StoredPreference("Gamesim.ReducedMotion"), Is.EqualTo(1), "On is the old 'Reduce character motion'.");
            Assert.That(cameraRig.ReducedMotion && hud.ReducedMotion, Is.True, "The camera and the HUD hear it.");
            PressSettingRow(SettingRowWords.ReducedMotionRow, "Off");
            Assert.That((StoredPreference("Gamesim.ReducedMotion"), cameraRig.ReducedMotion), Is.EqualTo((0, false)));

            PressSettingRow(SettingRowWords.ReducedSoundRow, "On");
            Assert.That(StoredPreference("Gamesim.ReducedAudio"), Is.EqualTo(1), "On is the old 'Reduce sound'.");
            Assert.That(audio.ReducedAudio, Is.True);
            PressSettingRow(SettingRowWords.ReducedSoundRow, "Off");
            Assert.That((StoredPreference("Gamesim.ReducedAudio"), audio.ReducedAudio), Is.EqualTo((0, false)));

            StepSettingRow(SettingRowWords.TextSizeRow, MoveDirection.Right, "Larger");
            Assert.That((StoredPreference(TextSizes.SizeKey), StoredPreference(TextSizes.LegacyKey)), Is.EqualTo((1, 1)), "The larger text, kept as the old 'Use larger text' kept it.");
            Assert.That(Hud.FontScale, Is.EqualTo(1.2f).Within(1e-4f));
            StepSettingRow(SettingRowWords.TextSizeRow, MoveDirection.Right, "Largest");
            Assert.That((StoredPreference(TextSizes.SizeKey), StoredPreference(TextSizes.LegacyKey)), Is.EqualTo((2, 1)));
            StepSettingRow(SettingRowWords.TextSizeRow, MoveDirection.Right, "Standard");
            Assert.That((StoredPreference(TextSizes.SizeKey), StoredPreference(TextSizes.LegacyKey)), Is.EqualTo((0, 0)), "Round to the standard.");
            Assert.That(Hud.FontScale, Is.EqualTo(1f).Within(1e-4f));

            PressSettingRow(SettingRowWords.CaptionTimeRow, "Longer");
            Assert.That(StoredPreference(CaptionTiming.Key), Is.EqualTo(1), "Longer is kept as 1, as before.");
            Assert.That(director.CaptionTimeSetting, Is.EqualTo(CaptionTime.Longer));
            PressSettingRow(SettingRowWords.CaptionTimeRow, "Standard");
            Assert.That(StoredPreference(CaptionTiming.Key), Is.EqualTo(0));

            // Conversation lines (UI plan D11): a new setting under its own key, waiting for a press until the player chooses.
            Assert.That(SettingRowValue(SettingRowWords.ConversationLinesRow), Is.EqualTo("Wait for me"), "Lines wait for a press by default.");
            PressSettingRow(SettingRowWords.ConversationLinesRow, "Move on");
            Assert.That(StoredPreference(ConversationLinePace.Key), Is.EqualTo(1), "Move on is kept as 1.");
            Assert.That(director.AutoContinueLines, Is.True);
            StepSettingRow(SettingRowWords.ConversationLinesRow, MoveDirection.Left, "Wait for me");
            Assert.That(StoredPreference(ConversationLinePace.Key), Is.EqualTo(0));
            Assert.That(director.AutoContinueLines, Is.False);

            StepSettingRow(SettingRowWords.CeremonyPaceRow, MoveDirection.Left, "Quick");
            Assert.That(StoredPreference("Gamesim.CeremonyPace"), Is.EqualTo(1), "Quick is the old 'Make ceremonies quick': 1.");
            Assert.That(director.CeremonyPaceSetting, Is.EqualTo(CeremonyPace.Quick));
            StepSettingRow(SettingRowWords.CeremonyPaceRow, MoveDirection.Right, "Suspenseful");
            Assert.That(StoredPreference("Gamesim.CeremonyPace"), Is.EqualTo(0));
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSettingRows_DisplayAndControlsKeepTheirKeysAndValues()
        {
            int levelBefore = QualitySettings.GetQualityLevel();
            int vsyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;
            yield return OpenIsolatedVolumeSettings();
            director.ShowSettingsSection(SettingsSection.Display);
            yield return null;
            try
            {
                PressSettingRow(SettingRowWords.CompactHudRow, "On");
                Assert.That((StoredPreference("Gamesim.CompactHud"), director.CompactHud), Is.EqualTo((1, true)), "On is the old 'Use compact HUD'.");
                PressSettingRow(SettingRowWords.CompactHudRow, "Off");
                Assert.That((StoredPreference("Gamesim.CompactHud"), director.CompactHud), Is.EqualTo((0, false)));

                int levels = QualitySettings.names.Length;
                if (levels > 1)
                {
                    int tier = director.QualityTier;
                    PressSettingRow(SettingRowWords.QualityRow, EpisodeDirector.QualityName((tier + 1) % levels));
                    Assert.That(StoredPreference("Gamesim.Quality"), Is.EqualTo((tier + 1) % levels), "The next tier, kept as its number.");
                    Assert.That(QualitySettings.GetQualityLevel(), Is.EqualTo(director.QualityTier), "and applied.");
                    StepSettingRow(SettingRowWords.QualityRow, MoveDirection.Left, EpisodeDirector.QualityName(tier));
                    Assert.That(StoredPreference("Gamesim.Quality"), Is.EqualTo(tier));
                }
                else
                {
                    Assert.That(FindButton(SettingRowWords.QualityRow).interactable, Is.False, "One quality level: nothing to choose,");
                    Assert.That(WhyLineShown(SettingRowWords.OneQualityLevelLine), Is.True, "and the row says why.");
                }

                PressSettingRow(SettingRowWords.VSyncRow, "Off");
                Assert.That((StoredPreference(DisplayChoices.VSyncKey), QualitySettings.vSyncCount), Is.EqualTo((0, 0)), "Off is the old 'Turn VSync off'.");
                Assert.That(director.FrameLimit, Is.EqualTo(-1), "The limit starts at none.");
                PressSettingRow(SettingRowWords.FrameRateRow, "30 fps");
                Assert.That((StoredPreference(DisplayChoices.FrameCapKey), Application.targetFrameRate), Is.EqualTo((30, 30)), "The first step is 30, as before.");
                StepSettingRow(SettingRowWords.FrameRateRow, MoveDirection.Left, "Uncapped");
                Assert.That((StoredPreference(DisplayChoices.FrameCapKey), Application.targetFrameRate), Is.EqualTo((-1, -1)), "Left of 30, round to none.");
                PressSettingRow(SettingRowWords.VSyncRow, "On");
                Assert.That((StoredPreference(DisplayChoices.VSyncKey), QualitySettings.vSyncCount), Is.EqualTo((1, 1)));

                int applied = director.WindowApplications;
                var mode = director.DisplayModeSetting;
                PressSettingRow(SettingRowWords.DisplayModeRow, DisplayChoices.ModeName(DisplayChoices.NextMode(mode)));
                Assert.That(StoredPreferenceText(DisplayChoices.DisplayModeKey), Is.EqualTo(DisplayChoices.ModeName(director.DisplayModeSetting)), "The mode, kept by its name.");
                Assert.That(director.WindowApplications, Is.EqualTo(applied + 1), "Applied once, as the old control did.");
                StepSettingRow(SettingRowWords.DisplayModeRow, MoveDirection.Left, DisplayChoices.ModeName(mode));
                Assert.That(director.DisplayModeSetting, Is.EqualTo(mode));
                FindButton(SettingRowWords.ResolutionRow).onClick.Invoke();
                Assert.That(StoredPreferenceText(DisplayChoices.ResolutionKey), Is.EqualTo(director.Resolution), "The resolution, kept as it reads.");
                Assert.That(SettingRowValue(SettingRowWords.ResolutionRow), Is.EqualTo(director.Resolution));

                PressSettingRow(SettingRowWords.HouseViewRow, "Whole house");
                Assert.That((StoredPreference(EpisodeDirector.WholeHouseKey), director.ShowsWholeHouse), Is.EqualTo((1, true)), "The whole house, kept as 1.");
                PressSettingRow(SettingRowWords.HouseViewRow, "One room");
                Assert.That((StoredPreference(EpisodeDirector.WholeHouseKey), director.ShowsWholeHouse), Is.EqualTo((0, false)));

                director.ShowSettingsSection(SettingsSection.Controls);
                StepSettingRow(SettingRowWords.CameraSpeedRow, MoveDirection.Right, "Fast");
                Assert.That(StoredPreference("Gamesim.CameraSpeed"), Is.EqualTo(150), "One step faster than normal is 150%, as before.");
                Assert.That(cameraRig.OrbitSpeedScale, Is.EqualTo(1.5f).Within(1e-4f));
                StepSettingRow(SettingRowWords.CameraSpeedRow, MoveDirection.Left, "Normal");
                Assert.That(StoredPreference("Gamesim.CameraSpeed"), Is.EqualTo(100));
                PressSettingRow(SettingRowWords.CameraTiltRow, "Inverted");
                Assert.That((StoredPreference("Gamesim.InvertY"), cameraRig.InvertTilt), Is.EqualTo((1, true)), "Inverted is the old 'Invert camera tilt'.");
                PressSettingRow(SettingRowWords.CameraTiltRow, "Normal");
                Assert.That((StoredPreference("Gamesim.InvertY"), cameraRig.InvertTilt), Is.EqualTo((0, false)));
                PressSettingRow(SettingRowWords.EdgePanningRow, "Off");
                Assert.That((StoredPreference("Gamesim.EdgePan"), cameraRig.EdgePan), Is.EqualTo((0, false)), "Off is the old 'Stop the screen edges panning'.");
                PressSettingRow(SettingRowWords.EdgePanningRow, "On");
                Assert.That((StoredPreference("Gamesim.EdgePan"), cameraRig.EdgePan), Is.EqualTo((1, true)));
            }
            finally
            {
                QualitySettings.SetQualityLevel(levelBefore, true);
                QualitySettings.vSyncCount = vsyncBefore;
                Application.targetFrameRate = targetBefore;
                director.ClosePanels();
            }
        }

        [UnityTest]
        public IEnumerator MenuSettingRows_AValueChangedElsewhereIsShownAtOnce()
        {
            HoldTheHouseForTheFixture();
            int vsyncBefore = QualitySettings.vSyncCount;
            int targetBefore = Application.targetFrameRate;
            bool tilt = director.InvertTilt, vSync = director.VSync;
            float speed = director.CameraSpeed;
            director.OpenSettingsAt(SettingsSection.Controls);
            yield return Frames(2);
            try
            {
                director.SetInvertTilt(!tilt);
                Assert.That(SettingRowValue(SettingRowWords.CameraTiltRow), Is.EqualTo(!tilt ? "Inverted" : "Normal"),
                    "Changed from outside its row, the row reads the new value at once.");
                director.SetCameraSpeed(speed > 1.9f ? .5f : 2f);
                Assert.That(SettingRowValue(SettingRowWords.CameraSpeedRow), Is.EqualTo(speed > 1.9f ? "Slowest" : "Fastest"));
                director.ShowSettingsSection(SettingsSection.Display);
                director.SetVSync(!vSync);
                Assert.That(SettingRowValue(SettingRowWords.VSyncRow), Is.EqualTo(SettingsWords.SwitchValue(!vSync)));
                director.ClosePanels();
                yield return null;
                director.SetInvertTilt(tilt);
                Assert.That(director.GetComponentsInChildren<Selectable>().Any(row => row.name == SettingRowWords.CameraTiltRow), Is.False,
                    "With Settings closed, a change draws no rows over the house.");
            }
            finally
            {
                director.SetInvertTilt(tilt);
                director.SetCameraSpeed(speed);
                director.SetVSync(vSync);
                QualitySettings.vSyncCount = vsyncBefore;
                Application.targetFrameRate = targetBefore;
                director.ClosePanels();
            }
        }

        [UnityTest]
        public IEnumerator MenuSettingRows_LeftAndRightWaitWhileARebindingListens()
        {
            HoldTheHouseForTheFixture();
            yield return OpenTheControlsSection();
            bool tiltAtStart = director.InvertTilt;
            try
            {
                FindButton("Rebind Notebook").onClick.Invoke();
                yield return null;
                Assert.That(HouseBindings.Listening, Is.True, "The rebinding listens.");
                bool tiltBefore = director.InvertTilt;
                float speedBefore = director.CameraSpeed;
                string tiltShown = tiltBefore ? "Inverted" : "Normal", tiltOther = tiltBefore ? "Normal" : "Inverted";
                var tilt = SettingRowControl(SettingRowWords.CameraTiltRow);
                EventSystem.current.SetSelectedGameObject(tilt.gameObject);
                ShellMove(tilt.gameObject, MoveDirection.Right);
                Assert.That(director.InvertTilt, Is.EqualTo(tiltBefore), "Right on a row while a rebinding listens changes nothing,");
                Assert.That(SettingRowValue(SettingRowWords.CameraTiltRow), Is.EqualTo(tiltShown));
                ShellMove(SettingRowControl(SettingRowWords.CameraSpeedRow).gameObject, MoveDirection.Left);
                Assert.That(director.CameraSpeed, Is.EqualTo(speedBefore).Within(1e-4f), "nor does left,");
                var tab = director.GetComponentsInChildren<SettingRowButton>().Last(each => each.name == SettingsWords.TabName(SettingsSection.Controls));
                EventSystem.current.SetSelectedGameObject(tab.gameObject);
                ShellMove(tab.gameObject, MoveDirection.Right);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(tab.gameObject), "and the section list holds still,");
                Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Controls));
                Assert.That(HouseBindings.Listening, Is.True, "and the rebinding still listens.");

                yield return PressKey(Key.Escape);
                Assert.That(HouseBindings.Listening, Is.False, "Esc cancels it.");
                yield return Frames(3);
                ShellMove(SettingRowControl(SettingRowWords.CameraTiltRow).gameObject, MoveDirection.Right);
                Assert.That(director.InvertTilt, Is.EqualTo(!tiltBefore), "Afterwards right changes the row again.");
                Assert.That(SettingRowValue(SettingRowWords.CameraTiltRow), Is.EqualTo(tiltOther));
            }
            finally
            {
                // A listening rebinding is the input system's, and outlives the test: never leave one behind.
                if (HouseBindings.Listening) HouseBindings.Cancel();
                director.SetInvertTilt(tiltAtStart);
            }
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSettingRows_TheTourFromTheMainMenuSaysWhyItCannotPlay()
        {
            HoldTheHouseForTheFixture();
            director.OpenMainMenu();
            yield return Frames(2);
            CastButtons(MainMenu.SettingsCaption)[0].onClick.Invoke();
            yield return Frames(2);
            director.ShowSettingsSection(SettingsSection.Controls);
            yield return null;
            var tour = FindButton(EpisodeDirector.ReplayTourCaption);
            Assert.That(tour.interactable, Is.False, "From the main menu the tour cannot play: the row stays, not pressable,");
            Assert.That(WhyLineShown(SettingRowWords.TourFromTheHouseLine), Is.True, "and says why.");
            tour.onClick.Invoke();
            yield return null;
            Assert.That(director.AreSettingsSectionsOpen, Is.True, "A press on it starts nothing.");
            director.ClosePanels();
            yield return null;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>The live row control - a switch's or a choice's button, or a slider - named for the setting.</summary>
        private Selectable SettingRowControl(string name)
        {
            var row = director.GetComponentsInChildren<Selectable>().LastOrDefault(each => each.name == name);
            Assert.That(row, Is.Not.Null, "No row named " + name + " is on screen.");
            return row;
        }

        /// <summary>What the row named for a setting reads beside its name.</summary>
        private string SettingRowValue(string name) => ShellValue(SettingRowControl(name));

        /// <summary>The row reads its name as its caption and <paramref name="value"/> beside it, and says both to a screen reader.</summary>
        private void AssertRowReads(string name, string value, string whyDisabled = null)
        {
            var row = SettingRowControl(name);
            Assert.That(row.GetComponentsInChildren<TMP_Text>()[0].text, Is.EqualTo(name), name + ": the caption is the setting's name.");
            Assert.That(ShellValue(row), Is.EqualTo(value), name + " reads its value.");
            string said = row is SettingRowSlider slider ? slider.Description : ((SettingRowButton)row).Description;
            Assert.That(said, Is.EqualTo(SettingsWords.Description(name, value, whyDisabled)), name + ": a screen reader hears both.");
        }

        /// <summary>Presses a row (Enter, A or a click) and checks it now reads <paramref name="value"/>.</summary>
        private void PressSettingRow(string name, string value)
        {
            ButtonWithCaption(name).onClick.Invoke();
            Assert.That(SettingRowValue(name), Is.EqualTo(value), name + " reads " + value + " after the press.");
        }

        /// <summary>Moves a row left or right, as the arrows and the pad do, and checks it now reads <paramref name="value"/>.</summary>
        private void StepSettingRow(string name, MoveDirection direction, string value)
        {
            ShellMove(SettingRowControl(name).gameObject, direction);
            Assert.That(SettingRowValue(name), Is.EqualTo(value), name + " reads " + value + " after " + direction + ".");
        }

        /// <summary>A value in this root's own preferences file (the fixture's temporary root), or -1.</summary>
        private int StoredPreference(string key) => PreferenceFile.Read(Path.Combine(temporaryDirectory, PreferenceFile.FileName), out _).GetInt(key, -1);

        private string StoredPreferenceText(string key) => PreferenceFile.Read(Path.Combine(temporaryDirectory, PreferenceFile.FileName), out _).GetString(key, null);

        /// <summary>Whether a row's muted "why" line shows these words; with null, whether any shows at all.</summary>
        private bool WhyLineShown(string words) => director.GetComponentsInChildren<TMP_Text>()
            .Any(text => text.isActiveAndEnabled && text.name == EpisodeHud.WhyDisabledName && (words == null || text.text == words));

        /// <summary>No control on screen carries a caption the rows replaced.</summary>
        private void AssertNoOldButtons(string section)
        {
            var old = new[]
            {
                "Volume down", "Volume up", "Mute sound", "Turn sound on", "Turn music off", "Turn music on", "Use compact HUD", "Show full HUD",
                "Turn VSync off", "Turn VSync on", "Play in a window", "Play full screen", "Reduce character motion", "Enable character motion",
                "Reduce sound", "Full sound", "Use larger text", "Use standard text", "Use largest text", "Stop the screen edges panning",
                "Let the screen edges pan", "Invert camera tilt", "Restore camera tilt", "Make ceremonies quick", "Make ceremonies suspenseful",
            };
            var shown = director.GetComponentsInChildren<Button>().Where(button => button.IsActive())
                .SelectMany(button => button.GetComponentsInChildren<TMP_Text>()).Select(text => text.text).ToList();
            Assert.That(shown.Where(text => old.Contains(text) || text.EndsWith("  (change)")), Is.Empty, section + " shows no action-worded or cycling caption.");
        }
    }
}

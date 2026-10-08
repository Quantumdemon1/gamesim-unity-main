using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The controls page and the hints that follow the device (PLAN A, A4): the settings list every
    /// action with its keys and its pad buttons, read off the map; on a pad the house prompt, the
    /// help card and the tour's line name the pad's buttons, and a control whose caption names a key
    /// wears a chip with the pad's button beside it - its caption byte for byte what it was; a key
    /// press puts the keyboard's words back.
    ///
    /// <para>The pad press is the right stick's click, and the key press Left Ctrl: one recenters a
    /// camera a panel holds still and the other is nobody's, so neither does anything else here.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Hints_APadPressShowsGlyphChipsBesideCaptionsThatNameAKey_AndAKeyTakesThemAway()
        {
            director.OpenSettings();
            yield return Frames(2);
            Assert.That(director.HintsForPad, Is.False, "A house starts on the keyboard's words.");
            var captions = new[] { "Close  [Esc]", "Save now  [F5]" };
            var before = captions.Select(caption => CaptionWords(ButtonWithCaption(caption))).ToArray();
            foreach (var caption in captions)
            {
                var none = Chip(ButtonWithCaption(caption));
                Assert.That(none == null || !none.activeSelf, Is.True, caption + ": no chip on the keyboard.");
            }

            yield return PressOnPad(GamepadButton.RightStick);
            yield return Frames(2);
            Assert.That(director.HintsForPad, Is.True, "A pad press: the hints speak of the pad.");
            for (int index = 0; index < captions.Length; index++)
            {
                var control = ButtonWithCaption(captions[index]);
                Assert.That(control.name, Is.EqualTo(captions[index]), "The control keeps its name.");
                Assert.That(CaptionWords(control), Is.EqualTo(before[index]), captions[index] + ": the caption is byte for byte what it was.");
                var chip = Chip(control);
                Assert.That(chip != null && chip.activeInHierarchy, Is.True, captions[index] + " wears the pad's button beside it.");
                Assert.That(chip.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(index == 0 ? "B" : "L3"),
                    "The chip names the pad's button for the same action: B backs out, the left stick's click saves.");
                Assert.That(chip.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True, "and takes no pointer.");
            }

            yield return PressKey(Key.LeftCtrl);
            yield return Frames(2);
            Assert.That(director.HintsForPad, Is.False, "A key press: the keyboard's words again.");
            foreach (var caption in captions)
            {
                var chip = Chip(ButtonWithCaption(caption));
                Assert.That(chip == null || !chip.activeSelf, Is.True, caption + ": the chip goes with the pad.");
            }
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hints_ThePromptAndTheHelpCardFollowTheDevice()
        {
            director.ClosePanels();
            WarpPlayer(director.StationPosition);
            yield return Frames(2);
            string keyboard = InputGlossary.PromptKey(false) + "  ·  ", pad = InputGlossary.PromptKey(true) + "  ·  ";
            Assert.That(PromptWords().Any(words => words.StartsWith(keyboard)), Is.True,
                "On the keyboard the prompt names E: " + string.Join(" | ", PromptWords()));

            yield return PressOnPad(GamepadButton.RightStick);
            yield return Frames(2);
            var said = PromptWords();
            Assert.That(said.Any(words => words.StartsWith(pad)), Is.True, "On a pad it names the pad's X: " + string.Join(" | ", said));
            Assert.That(said.Any(words => words.StartsWith(keyboard)), Is.False, "and not E.");
            Assert.That(ButtonWithCaption(EpisodeHud.InteractCaption), Is.Not.Null, "The prompt's control keeps its caption.");

            ButtonWithCaption("Help · controls").onClick.Invoke();
            yield return null;
            Assert.That(HelpCardShows(InputGlossary.HelpCard(true)), Is.True, "Opened on a pad, the help card names the pad's buttons.");

            yield return PressKey(Key.LeftCtrl);
            yield return Frames(2);
            Assert.That(HelpCardShows(InputGlossary.HelpCard(false)), Is.True, "A key press puts the keyboard's card back, in place.");
            Assert.That(PromptWords().Any(words => words.StartsWith(keyboard)), Is.True, "and the prompt's E.");
            ButtonWithCaption("Hide controls").onClick.Invoke();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Controls_TheSettingsListEveryActionWithItsKeysAndButtons()
        {
            director.OpenSettings();
            yield return Frames(2);
            Assert.That(ControlLines(), Is.Empty, "The section opens folded.");
            ButtonWithCaption(EpisodeDirector.ShowControlsCaption).onClick.Invoke();
            yield return Frames(2);

            var lines = ControlsPage.Lines(cameraRig.Actions);
            int actions = cameraRig.Actions.Asset.actionMaps.Where(map => map.name != HouseCameraActions.DialogueMapName).Sum(map => map.actions.Count);
            Assert.That(lines, Has.Count.EqualTo(actions), "One line for every action of the episode's maps.");
            var shown = ControlLines();
            foreach (var line in lines)
            {
                // A render leaves its old copy under the canvas until the frame ends: take the last.
                var text = shown.LastOrDefault(label => label.name == line.Name);
                Assert.That(text, Is.Not.Null, line.Name + " is on the page.");
                Assert.That(text.text, Is.EqualTo(line.Text), line.Name);
                if (line.Keys.Length > 0) Assert.That(text.text, Does.Contain(line.Keys), line.Name + " says its keys.");
                if (line.Pad.Length > 0) Assert.That(text.text, Does.Contain("Pad " + line.Pad), line.Name + " says its pad buttons.");
            }
            Assert.That(shown.Any(label => label.name == ControlsPage.RowPrefix + "Shortcuts/Back" && label.text.Contains("Pad B")), Is.True,
                "B is on the page, beside Start's pause menu.");

            ButtonWithCaption(EpisodeDirector.HideControlsCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(ControlLines(), Is.Empty, "and folds away again.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tour_TheControlsLineFollowsTheDevice()
        {
            var tour = DirectorTour();
            tour.Show(TourChrome);
            yield return Frames(2);
            Assert.That(tour.IsShowing, Is.True);
            Assert.That(TourWords(tour).Any(words => words.Contains(HouseTutorial.ControlsLine)), Is.True, "The tour starts on the keyboard's line.");

            yield return PressOnPad(GamepadButton.RightStick);
            yield return Frames(2);
            Assert.That(tour.PadLine, Is.True);
            Assert.That(TourWords(tour).Any(words => words.Contains(InputGlossary.TourLine(true))), Is.True, "A pad press puts the pad's line on the card.");
            Assert.That(TourWords(tour).Any(words => words.Contains(HouseTutorial.ControlsLine)), Is.False);

            yield return PressKey(Key.LeftCtrl);
            yield return Frames(2);
            Assert.That(TourWords(tour).Any(words => words.Contains(HouseTutorial.ControlsLine)), Is.True, "A key press puts the keyboard's back.");
            tour.Skip();
            yield return Frames(2);
        }

        /// <summary>
        /// The tour's line is on its first card, which a pad's A leaves at once: a player who came
        /// through the menu and the opening on a pad reads the pad's line from the moment the tour
        /// opens, without pressing anything while it is up - and on the keyboard, the keyboard's.
        /// </summary>
        [UnityTest]
        public IEnumerator Tour_OpensInTheWordsOfTheDeviceAlreadyInUse()
        {
            var tour = DirectorTour();
            Assert.That(tour.IsShowing, Is.False);
            yield return PressOnPad(GamepadButton.RightStick);
            yield return Frames(2);
            Assert.That(director.HintsForPad, Is.True, "The house was last pressed on a pad.");

            tour.Show(TourChrome);
            Assert.That(tour.IsShowing && tour.StepIndex == 0, Is.True, "The tour is on its first card.");
            Assert.That(tour.PadLine, Is.True, "It opens in the pad's words,");
            Assert.That(TourWords(tour).Any(words => words.Contains(InputGlossary.TourLine(true))), Is.True,
                "its first card carrying the pad's line: " + string.Join(" | ", TourWords(tour)));
            Assert.That(TourWords(tour).Any(words => words.Contains(HouseTutorial.ControlsLine)), Is.False, "and not the keyboard's.");
            yield return Frames(2);
            Assert.That(TourWords(tour).Any(words => words.Contains(InputGlossary.TourLine(true))), Is.True, "It stays so with nothing pressed.");
            tour.Skip();
            yield return Frames(2);

            yield return PressKey(Key.LeftCtrl);
            yield return Frames(2);
            tour.Show(TourChrome);
            Assert.That(tour.PadLine, Is.False, "After a key, the next showing opens in the keyboard's words.");
            Assert.That(TourWords(tour).Any(words => words.Contains(HouseTutorial.ControlsLine)), Is.True);
            tour.Skip();
            yield return Frames(2);
        }

        private IEnumerator PressOnPad(GamepadButton button)
        {
            if (testGamepad == null) testGamepad = InputSystem.AddDevice<Gamepad>();
            yield return PressPad(testGamepad, button);
        }

        private static GameObject Chip(Button control)
        {
            var chip = control.transform.Find(EpisodeHud.PadGlyphName);
            return chip != null ? chip.gameObject : null;
        }

        /// <summary>The words on a control, its chip's aside.</summary>
        private static string[] CaptionWords(Button control)
        {
            var chip = control.transform.Find(EpisodeHud.PadGlyphName);
            return control.GetComponentsInChildren<TMP_Text>(true)
                .Where(text => chip == null || !text.transform.IsChildOf(chip)).Select(text => text.text).ToArray();
        }

        private string[] PromptWords() =>
            FindButton(EpisodeHud.InteractCaption).GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();

        private bool HelpCardShows(string words) =>
            director.GetComponentsInChildren<TMP_Text>().Any(text => text.isActiveAndEnabled && text.text == words);

        private TMP_Text[] ControlLines() => director.GetComponentsInChildren<TMP_Text>()
            .Where(text => text.isActiveAndEnabled && text.name.StartsWith(ControlsPage.RowPrefix)).ToArray();

        private static string[] TourWords(HouseTutorial tour) =>
            tour.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
    }
}

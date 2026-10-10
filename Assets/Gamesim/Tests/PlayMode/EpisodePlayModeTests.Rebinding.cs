using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
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
    /// Rebinding (PLAN A, A5): the settings' CONTROLS section gives each of the house's actions a key
    /// or a pad button of the player's own. J to K: K opens the notebook and J does not, every caption
    /// that named J says K - its control keeps the English name - and the choice survives a reload of
    /// a root that keeps its own preferences. Esc cancels a rebinding without closing the settings and
    /// cannot itself be taken; a key another action answers to is refused; a pad button rebinds the
    /// pad's way; and "Reset all controls" puts every word back exactly as it was.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>The settings open on their CONTROLS section, unfolded.</summary>
        private IEnumerator OpenTheControlsSection()
        {
            director.OpenSettings();
            yield return null; yield return null;
            if (ShowsControl(EpisodeDirector.ShowControlsCaption))
            {
                FindButton(EpisodeDirector.ShowControlsCaption).onClick.Invoke();
                yield return null; yield return null;
            }
            Assert.That(ShowsControl(EpisodeDirector.HideControlsCaption), Is.True, "The controls are unfolded.");
        }

        /// <summary>The words the rebinding section's line says now.</summary>
        private string RebindLineShown() => director.GetComponentsInChildren<TMP_Text>(true)
            .Where(text => text.isActiveAndEnabled)
            .Select(text => text.text)
            .LastOrDefault(text => text.StartsWith("Press the new key") || text.Contains(" is now ") || text.Contains(" keeps its ")
                || text.Contains(" is already ") || text.StartsWith("Choose an action") || text.StartsWith("Every control is back"));

        /// <summary>Every live control's name beside the words it draws, for the sweep over the captions.</summary>
        private List<KeyValuePair<string, string>> CaptionsShown()
        {
            var shown = new List<KeyValuePair<string, string>>();
            foreach (var button in director.GetComponentsInChildren<Button>(true).Where(item => item.IsActive()))
            {
                var words = button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text));
                if (words != null) shown.Add(new KeyValuePair<string, string>(button.name, words.text));
            }
            return shown;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Rebinding_JToKOpensTheNotebook_TheCaptionsSayK_AndItSurvivesAReload()
        {
            // A root that keeps its own preferences, as a playtest participant's does (A13).
            EpisodeDirector.PreferencesBesideSaves = true;
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.RootFile));
            // Nothing in the house moves meanwhile, so the captions compared are the same controls'.
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return null;
            var rail = FindButton("Notebook [J]");
            var before = CaptionsShown();

            yield return OpenTheControlsSection();
            Assert.That(ShowsControl("Rebind Menu") || ShowsControl("Rebind Back"), Is.False, "The ways out are never offered.");
            FindButton("Rebind Notebook").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True, "Pressing it listens for the new key,");
            Assert.That(RebindLineShown(), Does.StartWith("Press the new key or button for Notebook"), "and says so.");
            yield return PressKey(Key.K);
            Assert.That(HouseBindings.Listening, Is.False);
            Assert.That(RebindLineShown(), Is.EqualTo("Notebook is now K."));
            Assert.That(director.IsPanelOpen, Is.True, "K did nothing else: the settings are still open.");
            var line = ControlLines().Last(text => text.name == ControlsPage.RowPrefix + "Shortcuts/Notebook");
            Assert.That(line.text, Does.Contain(":  K"), "The controls page says the notebook answers to K: " + line.text);

            director.ClosePanels();
            yield return null; yield return null;
            yield return PressKey(Key.K);
            Assert.That(director.IsPanelOpen, Is.True, "K opens the notebook,");
            director.ClosePanels();
            yield return null; yield return null;
            yield return PressKey(Key.J);
            Assert.That(director.IsPanelOpen, Is.False, "and J no longer does.");

            // Every caption that named J says K; the control keeps the English name; nothing else moved.
            rail = FindButton("Notebook [K]");
            Assert.That(rail.name, Is.EqualTo("Notebook [J]"), "The control is still named for its English caption.");
            var after = CaptionsShown();
            Assert.That(after.Any(pair => pair.Key.EndsWith("[J]")), Is.True, "Some control is named for J.");
            foreach (var pair in after)
                if (pair.Key.EndsWith("[J]")) Assert.That(pair.Value, Is.EqualTo(pair.Key.Replace("[J]", "[K]")), pair.Key + " says K.");
            var unchanged = before.Select(pair => pair.Value).Where(words => !words.Contains("[J]")).OrderBy(words => words, System.StringComparer.Ordinal).ToList();
            var now = after.Select(pair => pair.Value).Where(words => !words.Contains("[K]")).OrderBy(words => words, System.StringComparer.Ordinal).ToList();
            Assert.That(now, Is.EqualTo(unchanged), "No caption that did not name J changed.");

            // Kept beside the saves, and back after a load of the same root.
            string file = Path.Combine(temporaryDirectory, PreferenceFile.FileName);
            Assert.That(File.ReadAllText(file), Does.Contain(KeyBindings.PreferenceKey).And.Contain("<Keyboard>/k"));
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return null; yield return null;
            Assert.That(HouseBindings.Current.Get("Shortcuts", "Notebook", KeyBindings.KeyboardScheme), Is.EqualTo("<Keyboard>/k"));
            Assert.That(FindButton("Notebook [K]").name, Is.EqualTo("Notebook [J]"), "The caption says K after the reload.");
            yield return PressKey(Key.K);
            Assert.That(director.IsPanelOpen, Is.True, "K still opens the notebook after the reload.");
            director.ClosePanels();
            yield return null;

            // Reset all: every word exactly as it was, and J the notebook's again.
            yield return OpenTheControlsSection();
            FindButton(KeyBindings.ResetCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(RebindLineShown(), Is.EqualTo("Every control is back as it was shipped."));
            director.ClosePanels();
            yield return null; yield return null;
            Assert.That(CaptionsShown().Select(pair => pair.Value).OrderBy(words => words, System.StringComparer.Ordinal),
                Is.EqualTo(before.Select(pair => pair.Value).OrderBy(words => words, System.StringComparer.Ordinal)), "With every binding as shipped, every caption is the very words it was.");
            yield return PressKey(Key.J);
            Assert.That(director.IsPanelOpen, Is.True, "J opens the notebook again.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebinding_EscCancelsAndCannotBeTaken_AndAKeyInUseIsRefused()
        {
            yield return OpenTheControlsSection();
            FindButton("Rebind Notebook").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressKey(Key.Escape);
            Assert.That(HouseBindings.Listening, Is.False, "Esc cancels the rebinding,");
            Assert.That(director.IsPanelOpen, Is.True, "and does nothing else: the settings stay open.");
            Assert.That(RebindLineShown(), Is.EqualTo("Notebook keeps its key."));
            Assert.That(HouseBindings.Current.Count, Is.Zero);
            // The frame after the press that ended it starts nothing, as the Enter below shows; a frame later the control works again.
            yield return null; yield return null;

            FindButton("Rebind Notebook").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressKey(Key.Enter);
            Assert.That(HouseBindings.Listening, Is.False, "Enter is refused, and the Enter that refused it - Submit on the focused 'Rebind Notebook' - started nothing again.");
            Assert.That(RebindLineShown(), Is.EqualTo("Enter closes panels and presses controls, so it stays as it is. Notebook keeps its key."));
            Assert.That(director.IsPanelOpen, Is.True);
            yield return null; yield return null;

            // The notebook's own J, pressed for the diary: refused, and the press opens no notebook
            // over the settings - while a rebinding listens, its press is the rebinding's alone.
            FindButton("Rebind Diary").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressKey(Key.J);
            Assert.That(RebindLineShown(), Is.EqualTo("J is already Notebook's. Diary keeps its key."), "A key another house action answers to is refused,");
            Assert.That(ShowsControl("Rebind Diary"), Is.True, "and the J opened nothing over the settings.");
            Assert.That(HouseBindings.Current.Count, Is.Zero, "Nothing was rebound.");
            director.ClosePanels();
            yield return null; yield return null;
            yield return PressKey(Key.J);
            Assert.That(director.IsPanelOpen, Is.True, "J is still the notebook's.");
            director.ClosePanels();
            yield return null;
            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.True, "and Esc still opens the pause menu.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Rebinding_APadButtonRebindsThePadsWay_AndTheHintsNameIt()
        {
            var pad = TestGamepad();
            yield return OpenTheControlsSection();
            // The pad pressed last: the rebinding listens to the pad.
            yield return PressPad(pad, GamepadButton.RightStick);
            yield return null;
            FindButton("Rebind Diary").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressPad(pad, GamepadButton.DpadLeft);
            Assert.That(RebindLineShown(), Is.EqualTo("Diary is now D-pad left on the pad."));
            Assert.That(HouseBindings.Current.Get("Shortcuts", "Diary", KeyBindings.GamepadScheme), Is.EqualTo("<Gamepad>/dpad/left"));
            Assert.That(HouseBindings.Current.Get("Shortcuts", "Diary", KeyBindings.KeyboardScheme), Is.Null, "The keyboard's R is untouched.");
            Assert.That(InputGlossary.HelpCard(true), Does.Contain("D-pad left: diary"), "The pad's help card names the button the diary answers to now.");
            director.ClosePanels();
            yield return null; yield return null;
            var chip = FindButton(EpisodeHud.DiaryTravelCaption).transform.Find(EpisodeHud.PadGlyphName);
            Assert.That(chip != null && chip.GetComponentInChildren<TMP_Text>().text == "D-pad left", Is.True, "The rail's chip names it too.");

            // B is the pad's way out, and is never taken.
            yield return OpenTheControlsSection();
            yield return PressPad(pad, GamepadButton.RightStick);
            FindButton("Rebind Diary").onClick.Invoke();
            yield return null;
            yield return PressPad(pad, GamepadButton.East);
            Assert.That(RebindLineShown(), Does.Contain("stays as it is").And.Contain("Diary keeps its button"));
            Assert.That(director.IsPanelOpen, Is.True, "and the B that was refused closed nothing.");
            director.ClosePanels();
            yield return null;
        }
    }
}

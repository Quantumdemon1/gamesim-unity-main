using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Episode;
using Gamesim.House;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
            .LastOrDefault(text => text.StartsWith("Press the new ") || text.Contains(" is now ") || text.Contains(" keeps its ")
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
        public IEnumerator Rebinding_EnterSubmittedByTheRealUiModuleStartsCaptureWithoutTakingItsOpeningPress()
        {
            yield return RealSubmitRebindingOpening(pad: false);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Rebinding_SouthSubmittedByTheRealUiModuleStartsCaptureWithoutTakingItsOpeningPress()
        {
            yield return RealSubmitRebindingOpening(pad: true);
        }

        // Only selection is arranged by the fixture. The critical opening press is a queued device
        // event delivered by the existing EventSystem/InputSystemUIInputModule, never onClick or
        // ExecuteEvents. This is an input fixture, not evidence of a complete human season.
        // The pad rebinds the board's take-back: no house action may be rebound onto a pad button
        // the UI module navigates or submits with (D4), and every other pad button is already
        // another house action's, so a competition board's grip and take-back - a context of their
        // own - are the pad's only rebindings that can land.
        private IEnumerator RealSubmitRebindingOpening(bool pad)
        {
            var target = pad ? KeyBindings.FindTarget("Competition", "Undo") : KeyBindings.FindTarget("Shortcuts", "Notebook");
            string name = target.Name, rebind = target.Caption;
            EpisodeDirector.PreferencesBesideSaves = true;
            director.FreezeForReloadForDiagnostics();
            yield return ReloadEpisode();
            HoldTheHouseForTheFixture();
            Assert.That(director.PreferenceRouteInUse, Is.EqualTo(PreferenceRoute.RootFile));
            Assert.That(HouseBindings.Current.Count, Is.Zero);
            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            var gamepad = pad ? TestGamepad() : null;
            yield return OpenTheControlsSection();
            yield return Frames(2);
            var events = EventSystem.current;
            Assert.That(events, Is.Not.Null);
            var module = events.currentInputModule as InputSystemUIInputModule;
            Assert.That(module, Is.Not.Null, "The actual UI input module must deliver Submit.");
            Assert.That(module.submit, Is.Not.Null);
            Assert.That(module.submit.action.enabled, Is.True);
            var openingControl = pad ? (InputControl)gamepad.buttonSouth : testKeyboard.enterKey;
            Assert.That(module.submit.action.controls.Any(control => control == openingControl), Is.True,
                "The installed Submit action must resolve the device used here.");
            var button = ButtonWithCaption(rebind);
            Assert.That(button.name, Is.EqualTo(rebind));
            events.SetSelectedGameObject(button.gameObject);
            yield return Frames(2);
            button = ButtonWithCaption(rebind);
            events.SetSelectedGameObject(button.gameObject);
            var graph = director.Snapshot;
            var files = UvImage();
            var actionNames = cameraRig.Actions.Asset.actionMaps
                .SelectMany(map => map.actions.Select(action => map.name + "/" + action.name)).ToArray();

            if (pad) InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            else InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Enter));
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True, "The real Submit opens capture; its own press is not a replacement.");
            Assert.That(HouseBindings.ListeningFor.Name, Is.EqualTo(name));
            Assert.That(HouseBindings.Current.Count, Is.Zero);
            // Worded for the device that is listening: the pad's way out is B (lane-input-5).
            Assert.That(RebindLineShown(), Is.EqualTo(pad ? "Press the new button for Take back letter. B cancels." : "Press the new key or button for Notebook. Esc cancels."));
            Assert.That(ShowsControl(EpisodeDirector.HideControlsCaption), Is.True,
                "The opening press neither closes/cancels settings nor opens a shortcut panel.");
            UvEquivalent(graph, director.Snapshot);
            UvAssertImage(files); // Not even preferences were saved by the opening press.

            if (pad) InputSystem.QueueStateEvent(gamepad, new GamepadState());
            else InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return Frames(2);
            Assert.That(HouseBindings.Listening, Is.True, "Releasing the opening press leaves capture waiting.");
            Assert.That(HouseBindings.Current.Count, Is.Zero);
            UvEquivalent(graph, director.Snapshot);
            UvAssertImage(files);

            if (pad) yield return PressPad(gamepad, GamepadButton.North);
            else yield return PressKey(Key.K);
            yield return Frames(2);
            string scheme = pad ? KeyBindings.GamepadScheme : KeyBindings.KeyboardScheme;
            string path = pad ? "<Gamepad>/buttonNorth" : "<Keyboard>/k";
            Assert.That(HouseBindings.Listening, Is.False);
            Assert.That(HouseBindings.Current.Count, Is.EqualTo(1), "Exactly one replacement is accepted.");
            Assert.That(HouseBindings.Current.Get(target, scheme), Is.EqualTo(path));
            Assert.That(RebindLineShown(), Is.EqualTo(pad ? "Take back letter is now Y on the pad." : "Notebook is now K."));
            Assert.That(ShowsControl(EpisodeDirector.HideControlsCaption), Is.True, "The replacement opens no shortcut and leaves settings up.");
            Assert.That(ButtonWithCaption(rebind).name, Is.EqualTo(rebind));
            Assert.That(cameraRig.Actions.Asset.FindAction(target.Path, true).bindings
                .Any(binding => binding.effectivePath == path), Is.True, "The existing action receives the override.");
            Assert.That(cameraRig.Actions.Asset.actionMaps.SelectMany(map => map.actions.Select(action => map.name + "/" + action.name)),
                Is.EqualTo(actionNames), "Action names and order are unchanged.");
            UvEquivalent(graph, director.Snapshot);
            var saved = UvImage();
            Assert.That(saved.Keys.Where(name => name != PreferenceFile.FileName),
                Is.EqualTo(files.Keys.Where(name => name != PreferenceFile.FileName)));
            Assert.That(saved.ContainsKey(PreferenceFile.FileName), Is.True);
            foreach (var file in files)
                if (file.Key != PreferenceFile.FileName)
                    CollectionAssert.AreEqual(file.Value, saved[file.Key], "Rebinding saves no episode slot: " + file.Key);
            string preferences = File.ReadAllText(Path.Combine(temporaryDirectory, PreferenceFile.FileName));
            Assert.That(preferences, Does.Contain(KeyBindings.PreferenceKey).And.Contain(path));

            yield return Frames(2);
            Assert.That(HouseBindings.Listening, Is.False, "The completion press does not restart capture.");
            Assert.That(HouseBindings.Current.Count, Is.EqualTo(1));
            UvAssertImage(saved); // The complete persisted byte image remains stable.
            director.ClosePanels();
            yield return Frames(2);
            if (pad)
            {
                Assert.That(KeyBindings.BoardKey("Undo", true, "X"), Is.EqualTo("Y"), "The board's words name the take-back's new button.");
                Assert.That(HouseBindings.Current.Get(KeyBindings.FindTarget("Shortcuts", "Diary"), KeyBindings.GamepadScheme), Is.Null,
                    "The house's Y is the diary's still: a board is not the house.");
                yield return PressPad(gamepad, GamepadButton.North);
                Assert.That(director.StatusMessage, Is.EqualTo(InputGlossary.DiaryWayLine(director.LastTravel == EpisodeDirector.TravelKind.Warp, true)),
                    "After settings close, Y in the house sends the player on the way to the diary room as shipped.");
            }
            else
            {
                Assert.That(ButtonWithCaption("Notebook [K]").name, Is.EqualTo("Notebook [J]"),
                    "Visible caption changes, original control name does not.");
                yield return PressKey(Key.K);
                Assert.That(director.IsPanelOpen, Is.True, "The replacement works after settings close.");
            }
            UvEquivalent(graph, director.Snapshot);
            director.ClosePanels();
            yield return null;
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

        /// <summary>
        /// While a rebinding listens, a press on the other device is not one it can take, and the
        /// rebinding lets it through to the game's actions - only the press it takes is kept from them.
        /// The house leaves those presses alone too (<see cref="HouseBindings.Busy"/>): listening for a
        /// key, the pad's Start and B close no settings; listening for a pad button, J opens no notebook
        /// over them. Esc still cancels either.
        /// </summary>
        [UnityTest]
        public IEnumerator Rebinding_WhileItListensTheOtherDevicesPressesDoNothingInTheHouse()
        {
            var pad = TestGamepad();
            yield return OpenTheControlsSection();
            FindButton("Rebind Notebook").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True, "Pressed by the keyboard, the rebinding listens for a key.");
            Assert.That(RebindLineShown(), Does.StartWith("Press the new key or button for Notebook"));
            foreach (var button in new[] { GamepadButton.Start, GamepadButton.East })
            {
                yield return PressPad(pad, button);
                Assert.That(director.IsPanelOpen && ShowsControl("Rebind Notebook"), Is.True,
                    "The pad's " + button + " closed nothing while the rebinding listened for a key.");
                Assert.That(HouseBindings.Listening, Is.True, "The pad's " + button + " is not a key: the rebinding still listens.");
            }
            yield return PressKey(Key.Escape);
            Assert.That(HouseBindings.Listening, Is.False, "Esc cancels it,");
            Assert.That(RebindLineShown(), Is.EqualTo("Notebook keeps its key."));
            Assert.That(director.IsPanelOpen, Is.True, "and the settings stay open.");
            yield return null; yield return null;

            // The pad pressed last: the rebinding listens to the pad, and the keyboard's J passes it by.
            yield return PressPad(pad, GamepadButton.RightStick);
            yield return null;
            FindButton("Rebind Diary").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True, "Pressed by the pad, the rebinding listens for a pad button.");
            yield return PressKey(Key.J);
            Assert.That(ShowsControl("Rebind Diary"), Is.True, "J opened no notebook over the settings while the rebinding listened for a pad button.");
            Assert.That(HouseBindings.Listening, Is.True, "J is not a pad button: the rebinding still listens.");
            yield return PressKey(Key.Escape);
            Assert.That(HouseBindings.Listening, Is.False, "Esc cancels a pad rebinding too,");
            Assert.That(RebindLineShown(), Is.EqualTo("Diary keeps its button."));
            Assert.That(director.IsPanelOpen, Is.True, "and closes nothing.");
            Assert.That(HouseBindings.Current.Count, Is.Zero, "Nothing was rebound.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// A rebinding the pad asks for listens on the pad and says B cancels (lane-input-5). No house
        /// action may be rebound onto a button the UI module navigates or submits with, panels open
        /// or not (D4, the lead's ruling on its review): the D-pad walks the HUD's ring with no panel
        /// open, so neither its left nor its down is the diary's to take, and the notebook, which
        /// works over a panel, is told so in those words. Every other pad button is another house
        /// action's already, so on the pad a competition board's take-back is what rebinds, and the
        /// board's words name it.
        /// </summary>
        [UnityTest]
        public IEnumerator Rebinding_APadButtonRebindsThePadsWay_AndTheHintsNameIt()
        {
            var pad = TestGamepad();
            string card = InputGlossary.HelpCard(true);
            yield return OpenTheControlsSection();
            // The pad pressed last: the rebinding listens to the pad.
            yield return PressPad(pad, GamepadButton.RightStick);
            yield return null;
            Assert.That(RebindLineShown(), Is.EqualTo("Choose an action below, then press the key - or the pad button - it should answer to. B cancels."),
                "On the pad, the section names the pad's way out (lane-input-5).");
            FindButton("Rebind Diary").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            Assert.That(RebindLineShown(), Is.EqualTo("Press the new button for Diary. B cancels."), "The pad is told the pad's way out.");
            yield return PressPad(pad, GamepadButton.DpadLeft);
            Assert.That(RebindLineShown(), Is.EqualTo("D-pad left moves between the controls on screen. Diary keeps its button."),
                "The D-pad's left is the HUD's navigation, no spare for the diary (D4).");
            Assert.That(HouseBindings.Current.Count, Is.Zero, "Nothing was rebound.");
            Assert.That(director.IsPanelOpen, Is.True);
            yield return null; yield return null;

            // Nor its down, which walks the HUD's ring with no panel open.
            yield return PressPad(pad, GamepadButton.RightStick);
            FindButton("Rebind Diary").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressPad(pad, GamepadButton.DpadDown);
            Assert.That(RebindLineShown(), Is.EqualTo("D-pad down moves between the controls on screen. Diary keeps its button."));
            Assert.That(HouseBindings.Current.Get("Shortcuts", "Diary", KeyBindings.GamepadScheme), Is.Null, "The diary keeps its Y.");
            yield return null; yield return null;

            // The notebook answers with a panel open, where the D-pad moves between the panel's
            // controls: the refusal says so (D4).
            yield return PressPad(pad, GamepadButton.RightStick);
            FindButton("Rebind Notebook").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressPad(pad, GamepadButton.DpadDown);
            Assert.That(RebindLineShown(), Is.EqualTo("D-pad down moves between a panel's controls, and Notebook works with a panel open. Notebook keeps its button."));
            Assert.That(HouseBindings.Current.Get("Shortcuts", "Notebook", KeyBindings.GamepadScheme), Is.Null, "The notebook keeps Select.");
            Assert.That(director.IsPanelOpen, Is.True);
            yield return null; yield return null;

            // B is the pad's way out, and is never taken: listening on the pad, B cancels (lane-input-5).
            yield return PressPad(pad, GamepadButton.RightStick);
            FindButton("Rebind Diary").onClick.Invoke();
            yield return null;
            Assert.That(RebindLineShown(), Is.EqualTo("Press the new button for Diary. B cancels."), "The pad is told the pad's way out.");
            yield return PressPad(pad, GamepadButton.East);
            Assert.That(HouseBindings.Listening, Is.False, "B cancels the rebinding,");
            Assert.That(RebindLineShown(), Is.EqualTo("Diary keeps its button."), "and says so,");
            Assert.That(HouseBindings.Current.Count, Is.Zero, "with every button as it was,");
            Assert.That(director.IsPanelOpen, Is.True, "and the B that cancelled closed nothing.");
            yield return null; yield return null;

            // A board's take-back is the pad's to move: Y, and the board's words name it.
            yield return PressPad(pad, GamepadButton.RightStick);
            FindButton("Rebind Take back letter").onClick.Invoke();
            yield return null;
            Assert.That(RebindLineShown(), Is.EqualTo("Press the new button for Take back letter. B cancels."));
            yield return PressPad(pad, GamepadButton.North);
            Assert.That(RebindLineShown(), Is.EqualTo("Take back letter is now Y on the pad."));
            Assert.That(HouseBindings.Current.Get(KeyBindings.FindTarget("Competition", "Undo"), KeyBindings.GamepadScheme), Is.EqualTo("<Gamepad>/buttonNorth"));
            Assert.That(KeyBindings.BoardKey("Undo", true, "X"), Is.EqualTo("Y"), "The board names the button its take-back answers to now,");
            Assert.That(InputGlossary.HelpCard(true), Is.EqualTo(card), "and the house's help card, which names none of the board's, is as it was.");
            director.ClosePanels();
            yield return null;
        }

        /// <summary>
        /// The camera's own keys rebind too, on the keyboard alone (A5): the pad turns and pans with
        /// its sticks, so "Rebind Orbit left" pressed by the pad still listens for a key, and a pad
        /// button pressed then is not one - but B, the pad's way out, cancels it as it says
        /// (lane-input-5). Orbit left given Z turns the camera as Q did, and Q no
        /// longer does; the pan's up given I, the help card says "IASD/arrows: pan"; and "Reset all
        /// controls" puts Q and W back.
        /// </summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator Rebinding_TheCamerasKeysAreTheKeyboardsToChoose()
        {
            HoldTheHouseForTheFixture();
            var pad = TestGamepad();
            yield return OpenTheControlsSection();
            yield return PressPad(pad, GamepadButton.RightStick);
            yield return null;
            FindButton("Rebind Orbit left").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True, "Pressed by the pad, a camera key still listens,");
            Assert.That(RebindLineShown(), Is.EqualTo("Press the new key for Orbit left. B cancels."), "for a key, and the pad's way out is B (lane-input-5).");
            yield return PressPad(pad, GamepadButton.North);
            Assert.That(HouseBindings.Listening, Is.True, "Y is not a key: it is still listening.");
            yield return PressPad(pad, GamepadButton.East);
            Assert.That(HouseBindings.Listening, Is.False, "B cancels it,");
            Assert.That(RebindLineShown(), Is.EqualTo("Orbit left keeps its key."), "and says so,");
            Assert.That(director.IsPanelOpen && ShowsControl("Rebind Orbit left"), Is.True, "and the B that cancelled closed nothing.");
            Assert.That(HouseBindings.Current.Count, Is.Zero);
            yield return null; yield return null;
            FindButton("Rebind Orbit left").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressKey(Key.Z);
            Assert.That(RebindLineShown(), Is.EqualTo("Orbit left is now Z."));
            Assert.That(HouseBindings.Current.Get(KeyBindings.FindTarget("Camera", "OrbitRate", "Left"), KeyBindings.KeyboardScheme), Is.EqualTo("<Keyboard>/z"));
            yield return null; yield return null;
            FindButton("Rebind Pan up").onClick.Invoke();
            yield return null;
            yield return PressKey(Key.A);
            Assert.That(RebindLineShown(), Is.EqualTo("A is already Pan left's. Pan up keeps its key."), "One part of the pan is refused another's key.");
            yield return null; yield return null;
            FindButton("Rebind Pan up").onClick.Invoke();
            yield return null;
            yield return PressKey(Key.I);
            Assert.That(RebindLineShown(), Is.EqualTo("Pan up is now I."));
            director.ClosePanels();
            yield return null; yield return null;

            // Z turns the camera as Q did, and Q no longer does.
            SetCameraDistance(20f);
            yield return SettleCamera();
            cameraRig.ControlsEnabled = true;
            float before = cameraRig.Yaw;
            yield return HoldKey(Key.Z);
            Assert.That(Turned(before, cameraRig.Yaw), Is.LessThan(-1f), "Z turns the camera as Q did.");
            before = cameraRig.Yaw;
            yield return HoldKey(Key.Q);
            Assert.That(Mathf.Abs(Turned(before, cameraRig.Yaw)), Is.LessThan(.01f), "Q no longer turns it.");

            // The help card names the pan's keys as they are.
            yield return PressKey(Key.LeftCtrl);
            ButtonWithCaption("Help · controls").onClick.Invoke();
            yield return Frames(2);
            string card = InputGlossary.KeyboardHelpCard.Replace("WASD/arrows: pan", "IASD/arrows: pan");
            Assert.That(HelpCardShows(card), Is.True, "The help card says the pan's keys as they are: " + string.Join(" | ",
                director.GetComponentsInChildren<TMP_Text>().Where(text => text.isActiveAndEnabled && text.text.Contains("arrows: pan")).Select(text => text.text)));
            ButtonWithCaption("Hide controls").onClick.Invoke();
            yield return Frames(2);

            // Reset all: Q and W are the camera's again, and the help card is as it was written.
            yield return OpenTheControlsSection();
            FindButton(KeyBindings.ResetCaption).onClick.Invoke();
            yield return null; yield return null;
            Assert.That(HouseBindings.Current.Count, Is.Zero);
            Assert.That(cameraRig.Actions.OrbitRate.bindings.Any(binding => binding.effectivePath == "<Keyboard>/q"), Is.True, "Q is the orbit's left again,");
            Assert.That(cameraRig.Actions.Pan.bindings.Any(binding => binding.effectivePath == "<Keyboard>/w"), Is.True, "and W the pan's up.");
            director.ClosePanels();
            yield return null;
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardHelpCard), Is.SameAs(InputGlossary.KeyboardHelpCard), "The words are the very strings again.");
        }

        /// <summary>
        /// A competition's board rebinds too (A5): the endurance game's grip given Left Shift holds on
        /// Left Shift and no longer on Space, and the board says so - its legend and its rules name the
        /// key the player chose. A letter is refused: the word board spells with every one.
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator Rebinding_ABoardsGripIsTheKeyTheSettingsGaveIt()
        {
            yield return InstallRules4AtFirstHoH(SeedOpeningWithBoard(CompetitionDefinitions.HoldYourGround));
            yield return OpenTheControlsSection();
            FindButton("Rebind Hold grip").onClick.Invoke();
            yield return null;
            Assert.That(HouseBindings.Listening, Is.True);
            yield return PressKey(Key.K);
            Assert.That(RebindLineShown(), Is.EqualTo("K spells on the word board, so it stays a letter. Hold grip keeps its key."));
            yield return null; yield return null;
            FindButton("Rebind Hold grip").onClick.Invoke();
            yield return null;
            yield return PressKey(Key.LeftShift);
            string shift = InputGlossary.BindingLabel("<Keyboard>/leftShift");
            Assert.That(RebindLineShown(), Is.EqualTo("Hold grip is now " + shift + "."));
            // And back: Space is the grip's own as shipped, shared then with the board's Hit, which
            // nobody rebinds - the grip can always have it back (lane-input-6).
            yield return null; yield return null;
            FindButton("Rebind Hold grip").onClick.Invoke();
            yield return null;
            yield return PressKey(Key.Space);
            Assert.That(RebindLineShown(), Is.EqualTo("Hold grip is now Space."), "The grip has its own key back.");
            Assert.That(HouseBindings.Current.Count, Is.Zero, "Its own key again is no choice at all: the grip is as shipped.");
            yield return null; yield return null;
            FindButton("Rebind Hold grip").onClick.Invoke();
            yield return null;
            yield return PressKey(Key.LeftShift);
            Assert.That(RebindLineShown(), Is.EqualTo("Hold grip is now " + shift + "."));
            director.ClosePanels();
            yield return null; yield return null;

            WarpPlayer(director.StationPosition);
            Assert.That(director.TryOpenPhasePanel(), Is.True);
            yield return null; yield return null;
            ButtonWithCaption("Practice this competition").onClick.Invoke();
            yield return null;
            var screen = SceneComponents<CompetitionGameScreen>().Single();
            if (screen.IsAssembling)
                screen.GetComponentsInChildren<Button>().First(button => button.name == "Continue to competition").onClick.Invoke();
            for (int frame = 0; frame < 120 && !screen.IsPlaying; frame++) { screen.AdvanceReady(.2f); yield return null; }
            Assert.That(screen.IsPlaying, Is.True, "The practice is under way.");
            var run = ChallengeRun();
            Assert.That(run.Kind, Is.EqualTo(CompetitionMiniGames.Kind.Endurance));
            yield return null;

            var labels = screen.GetComponentsInChildren<TMP_Text>().Where(text => text.isActiveAndEnabled).ToList();
            Assert.That(labels.Single(text => text.name == "Rules").text, Does.Contain(shift + " / right trigger hold"), "The rules name the grip's key.");
            Assert.That(labels.Any(text => text.name == "Key" && text.text == shift), Is.True,
                "The legend's key is the grip's: " + string.Join(" | ", labels.Where(text => text.name == "Key").Select(text => text.text)));
            Assert.That(labels.Any(text => text.name == "Key" && text.text == "Space"), Is.False, "and Space is nobody's on it now.");

            if (testKeyboard == null) testKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.LeftShift));
            yield return null; yield return null;
            Assert.That(run.Holding, Is.True, shift + " holds the grip.");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.That(run.Holding, Is.False, "and letting go lets go.");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Space));
            yield return null; yield return null;
            Assert.That(run.Holding, Is.False, "Space no longer holds.");
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
        }
    }
}

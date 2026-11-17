using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
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
    /// <summary>
    /// Menu slice 4's shell on the built screens (the review's change 2): the game menu card the
    /// house's door opens - its rows, PAUSED only while the clock is held, and Close, Escape, B and
    /// Resume all going back to the house - the Settings frame its Settings row opens, on Saves with
    /// exactly one section lit and its way back to the card, the main menu's Settings going back to
    /// the main menu, and the row kit's switch, choice and slider answering left, right and Enter,
    /// with the confirm card's keyboard on its safe choice.
    ///
    /// <para>Every case holds the house first (no houseguest walks up with an invitation that Escape
    /// would answer instead), and the kit's rows are pressed in the frame they are drawn in, so no
    /// render can come between.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuSettings_TheHousesDoorOpensTheGameMenu_AndEveryWayOutResumes()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);

            director.OpenSettings();
            yield return Frames(2);
            Assert.That(director.IsGameMenuOpen, Is.True, "The house's door opens the game menu card first.");
            Assert.That(director.AreSettingsSectionsOpen, Is.False, "not the sections.");
            foreach (var caption in new[] { SettingsWords.ResumeCaption, SettingsWords.SaveCaption, SettingsWords.SettingsCaption, SettingsWords.MainMenuCaption })
            {
                var row = ShellRow(caption);
                Assert.That(row, Is.Not.Null, "The card has its " + caption + " row.");
                Assert.That(row.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(caption), "captioned with its words alone.");
            }
            Assert.That(ShellLabelShown(SettingsWords.GameMenuTitle), Is.True, "The card's title.");
            Assert.That(director.IsHouseClockHeld, Is.True, "The card holds the house's clock,");
            Assert.That(ShellLabelShown(SettingsWords.PausedLabel), Is.EqualTo(director.IsHouseClockHeld), "and says PAUSED while it does.");
            Assert.That(ShellLabelShown(SettingsWords.MainMenuKeepsLine), Is.True, "The way to the main menu says what it keeps.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(EpisodeHud.GameMenuRowPrefix + SettingsWords.ResumeCaption),
                "Resume, the card's one main button, has the keyboard.");

            // Close at its top right resumes.
            var close = Hud.PanelClose;
            Assert.That(close, Is.Not.Null, "The card has its Close.");
            close.onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "Close goes back to the house.");

            // Escape opens it with nothing open, and Escape closes it again.
            yield return PressKey(Key.Escape);
            yield return Frames(1);
            Assert.That(director.IsGameMenuOpen, Is.True, "Escape with nothing open is the game menu.");
            yield return PressKey(Key.Escape);
            yield return Frames(1);
            Assert.That(director.IsPanelOpen, Is.False, "Escape on the card resumes.");

            // The pad: Start opens it, B resumes.
            yield return PressOnPad(GamepadButton.Start);
            yield return Frames(1);
            Assert.That(director.IsGameMenuOpen, Is.True, "Start with nothing open is the game menu.");
            yield return PressOnPad(GamepadButton.East);
            yield return Frames(1);
            Assert.That(director.IsPanelOpen, Is.False, "B on the card resumes.");

            // And Resume.
            director.OpenSettings();
            yield return Frames(2);
            ShellRow(SettingsWords.ResumeCaption).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.IsPanelOpen, Is.False, "Resume goes back to the house.");
        }

        [UnityTest]
        public IEnumerator MenuSettings_TheCardsSaveStaysAndSaysWhen()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            var held = SettingsWords.Clock;
            try
            {
                var at = new System.DateTime(2026, 11, 16, 19, 42, 0);
                SettingsWords.Clock = () => at;
                director.OpenSettings();
                yield return Frames(2);
                Assert.That(ShellLabelShown(SettingsWords.SavedAtLine(at)), Is.False, "Nothing is said before a save.");
                ShellRow(SettingsWords.SaveCaption).onClick.Invoke();
                yield return Frames(2);
                Assert.That(director.IsGameMenuOpen, Is.True, "Save stays on the card,");
                Assert.That(ShellLabelShown(SettingsWords.SavedAtLine(at)), Is.True, "which says when the season was saved.");
                Assert.That(System.IO.File.Exists(director.SavePath), Is.True, "and the season is on disk.");
            }
            finally { SettingsWords.Clock = held; }
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSettings_TheCardsSettingsOpenTheFrameOnSaves_OneSectionLit_AndBackToTheCard()
        {
            HoldTheHouseForTheFixture();
            director.ClosePanels();
            yield return Frames(2);
            director.OpenSettings();
            yield return Frames(2);
            ShellRow(SettingsWords.SettingsCaption).onClick.Invoke();
            yield return Frames(2);

            Assert.That(director.AreSettingsSectionsOpen, Is.True, "The card's Settings opens the frame,");
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Saves), "on Saves.");
            var tabs = ShellTabs();
            Assert.That(tabs.Select(tab => tab.name), Is.EqualTo(SettingsWords.Sections.Select(SettingsWords.TabName)), "Every section in the list, in order.");
            Assert.That(tabs.Select(tab => tab.GetComponentInChildren<TMP_Text>().text), Is.EqualTo(SettingsWords.Sections.Select(SettingsWords.Name)),
                "each captioned with its name.");
            AssertOneLit(SettingsSection.Saves);
            Assert.That(Hud.PageWayBack, Is.Not.Null, "A page reached from the card has its way back,");
            Assert.That(WayOut.CaptionOf(Hud.PageWayBack), Is.EqualTo(WayOut.BackTo(SettingsWords.GameMenuPlace)), "to the game menu,");
            Assert.That(Hud.PanelClose, Is.Not.Null, "and its Close.");

            // A click shows a section; left and right on an entry move the keyboard along the list.
            tabs.Single(tab => tab.name == SettingsWords.TabName(SettingsSection.Sound)).onClick.Invoke();
            yield return Frames(2);
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Sound));
            AssertOneLit(SettingsSection.Sound);
            var sound = ShellTabs().Single(tab => tab.name == SettingsWords.TabName(SettingsSection.Sound));
            ShellMove(sound.gameObject, MoveDirection.Right);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SettingsWords.TabName(SettingsSection.Display)),
                "Right moves the keyboard to the next section's entry,");
            ShellMove(sound.gameObject, MoveDirection.Left);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo(SettingsWords.TabName(SettingsSection.Saves)), "and left to the one before.");
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Sound), "Moving the keyboard shows nothing new; a press does.");

            // Escape goes back to the card, and again to the house.
            yield return PressKey(Key.Escape);
            yield return Frames(1);
            Assert.That(director.IsGameMenuOpen, Is.True, "Escape on the frame goes back to the game menu.");
            yield return PressKey(Key.Escape);
            yield return Frames(1);
            Assert.That(director.IsPanelOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator MenuSettings_TheMainMenusSettingsGoBackToTheMainMenu()
        {
            HoldTheHouseForTheFixture();
            director.OpenMainMenu();
            yield return Frames(2);
            CastButtons(MainMenu.SettingsCaption)[0].onClick.Invoke();
            yield return Frames(2);
            Assert.That(Menu().IsShowing, Is.False);
            Assert.That(director.AreSettingsSectionsOpen && !director.IsGameMenuOpen, Is.True, "The main menu opens the frame, with no card,");
            Assert.That(director.SettingsSectionShown, Is.EqualTo(SettingsSection.Saves), "on Saves.");
            Assert.That(Hud.PanelClose, Is.Null, "No Close that would drop the player into the house,");
            Assert.That(director.GetComponentsInChildren<Button>().Any(button => button.IsActive() && button.name == WayOut.CloseObjectName), Is.False);
            Assert.That(WayOut.CaptionOf(Hud.PageWayBack), Is.EqualTo(WayOut.BackTo(WayOut.MainMenu)), "but a way back to the main menu,");
            Hud.PageWayBack.onClick.Invoke();
            yield return Frames(2);
            Assert.That(Menu().IsShowing, Is.True, "which goes there.");

            CastButtons(MainMenu.SettingsCaption)[0].onClick.Invoke();
            yield return Frames(2);
            yield return PressKey(Key.Escape);
            yield return Frames(1);
            Assert.That(Menu().IsShowing, Is.True, "Escape goes back to the main menu too.");
        }

        [UnityTest]
        public IEnumerator MenuSettings_TheRowKitAnswersLeftRightAndEnter()
        {
            HoldTheHouseForTheFixture();
            director.OpenSettingsAt(SettingsSection.Saves);
            yield return Frames(2);
            var hud = Hud;

            // Drawn and pressed within this frame: no render can come between.
            bool? flipped = null;
            var toggle = hud.SettingSwitch("Kit switch", false, value => flipped = value);
            Assert.That(toggle.name, Is.EqualTo("Kit switch"), "The row is named by the setting.");
            Assert.That(toggle.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Kit switch"), "Its caption is the setting's name,");
            Assert.That(ShellValue(toggle), Is.EqualTo("Off"), "and its value a label of its own.");
            Assert.That(toggle.Description, Is.EqualTo("Kit switch: Off"));
            ShellMove(toggle.gameObject, MoveDirection.Right);
            Assert.That(flipped, Is.True, "Right flips a switch.");
            flipped = null; ShellMove(toggle.gameObject, MoveDirection.Left);
            Assert.That(flipped, Is.True, "So does left.");
            flipped = null; toggle.onClick.Invoke();
            Assert.That(flipped, Is.True, "So does Enter, A or a click.");

            int chosen = -1;
            var choice = hud.SettingChoice("Kit choice", new[] { "Standard", "Large", "Largest" }, 0, index => chosen = index);
            Assert.That(ShellValue(choice), Is.EqualTo("Standard"));
            ShellMove(choice.gameObject, MoveDirection.Right);
            Assert.That(chosen, Is.EqualTo(1), "Right steps on through the choices,");
            ShellMove(choice.gameObject, MoveDirection.Left);
            Assert.That(chosen, Is.EqualTo(2), "left steps back, round to the last,");
            choice.onClick.Invoke();
            Assert.That(chosen, Is.EqualTo(1), "and Enter steps on.");

            int kept = -1;
            var slider = hud.SettingSlider("Kit volume", 50, null, percent => kept = percent);
            Assert.That(ShellValue(slider), Is.EqualTo("50%"));
            ShellMove(slider.gameObject, MoveDirection.Right);
            Assert.That(kept, Is.EqualTo(60), "Right moves a slider one step and keeps it,");
            Assert.That(ShellValue(slider), Is.EqualTo("60%"), "and the value says so.");
            ShellMove(slider.gameObject, MoveDirection.Left);
            Assert.That(kept, Is.EqualTo(50), "Left moves it back.");

            flipped = null;
            var locked = hud.SettingSwitch("Kit locked", true, value => flipped = value, "Not while a season loads.");
            Assert.That(locked.interactable, Is.False, "A row that cannot be used is not pressable,");
            ShellMove(locked.gameObject, MoveDirection.Right);
            Assert.That(flipped, Is.Null, "and nothing flips it.");
            Assert.That(director.GetComponentsInChildren<TMP_Text>().Any(text => text.isActiveAndEnabled
                && text.name == EpisodeHud.WhyDisabledName && text.text == "Not while a season loads."), Is.True, "It says why in a muted line.");
            director.ClosePanels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuSettings_TheConfirmCardPutsTheKeyboardOnTheSafeChoice()
        {
            HoldTheHouseForTheFixture();
            director.OpenSettingsAt(SettingsSection.Advanced);
            yield return Frames(2);
            bool kept = false, done = false;
            Button safe = null;
            // A render between drawing the card and wiring its focus (a body finishing its assembly) takes
            // the card with it: draw it again, a bounded number of times, until a frame keeps it.
            for (int attempt = 0; attempt < 10 && (safe == null || !safe); attempt++)
            {
                // As a press that arms the card does: the control pressed goes with the render that draws the card.
                EventSystem.current.SetSelectedGameObject(null);
                safe = Hud.SettingsConfirm("Delete Kit, week 3? This cannot be undone.", "Keep it", () => kept = true, "Delete this season", () => done = true);
                yield return Frames(2);
            }
            Assert.That(safe != null && safe, Is.True, "The confirm card stayed on screen.");
            var card = safe.transform.parent.parent;
            Assert.That(card.name, Is.EqualTo(SettingsWords.ConfirmCardName));
            Assert.That(card.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Delete Kit, week 3? This cannot be undone."), Is.True, "It asks.");
            Assert.That(card.GetComponentsInChildren<Button>().Select(button => button.GetComponentInChildren<TMP_Text>().text),
                Is.EqualTo(new[] { "Keep it", "Delete this season" }), "The safe choice, then the button naming the result.");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(safe.gameObject), "The safe choice has the keyboard.");
            ExecuteEvents.Execute(safe.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(kept && !done, Is.True, "Enter on it keeps what is there.");
            director.ClosePanels();
            yield return null;
        }

        // ---------------------------------------------------------------- helpers

        private Button ShellRow(string caption) => director.GetComponentsInChildren<Button>()
            .SingleOrDefault(button => button.IsActive() && button.name == EpisodeHud.GameMenuRowPrefix + caption);

        private SettingRowButton[] ShellTabs() => director.GetComponentsInChildren<SettingRowButton>()
            .Where(tab => tab.IsActive() && tab.name.StartsWith(SettingsWords.TabPrefix, System.StringComparison.Ordinal)).ToArray();

        private bool ShellLabelShown(string words) => director.GetComponentsInChildren<TMP_Text>()
            .Any(text => text.isActiveAndEnabled && text.text == words);

        private static string ShellValue(Component row)
        {
            var value = row.transform.Find(EpisodeHud.SettingValueName);
            return value != null ? value.GetComponent<TMP_Text>().text : null;
        }

        private static void ShellMove(GameObject target, MoveDirection direction)
        {
            var move = new AxisEventData(EventSystem.current) { moveDir = direction };
            ExecuteEvents.Execute(target, move, ExecuteEvents.moveHandler);
        }

        private void AssertOneLit(SettingsSection section)
        {
            var lit = ShellTabs().Where(tab => tab.transform.Find(EpisodeHud.LitMarkName) != null).ToArray();
            Assert.That(lit.Select(tab => tab.name), Is.EqualTo(new[] { SettingsWords.TabName(section) }),
                "Exactly one section is lit, " + section + ", marked by its bar as well as its colour.");
        }
    }
}

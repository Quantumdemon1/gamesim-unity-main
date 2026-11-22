using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The hints in the device's words (U7): the panel's control hint and the follow chip on a pad,
    /// and the keyboard's fuller help card. Each key or button a hint names is held to the binding
    /// it names - the shipped binding KeyBindings keeps for the actions a player may rebind, the
    /// glossary's row for the rest - and a rebinding moves the hint with it. Unity-free.
    /// </summary>
    public sealed class InputGlossaryDeviceHintsTests
    {
        [TearDown]
        public void ShowTheShippedBindings() => KeyBindings.Show(null);

        /// <summary>The key an action answers to on the keyboard as shipped, in the hints' words.</summary>
        private static string ShippedKey(string map, string action) =>
            InputGlossary.BindingLabel(KeyBindings.ShippedPath(KeyBindings.FindTarget(map, action), KeyBindings.KeyboardScheme));

        /// <summary>The button an action answers to on a pad as shipped, in the hints' words.</summary>
        private static string ShippedButton(string map, string action) =>
            InputGlossary.BindingLabel(KeyBindings.ShippedPath(KeyBindings.FindTarget(map, action), KeyBindings.GamepadScheme));

        /// <summary>The first of a glossary row's words for one device: "]" of "] / Tab".</summary>
        private static string First(string words) => words.Split(new[] { " / " }, System.StringSplitOptions.None)[0];

        [Test]
        public void TheKeyboardHelpCard_ListsTheMapTheNotebookYourMovesAndTheFollow_WithEscLast()
        {
            string card = InputGlossary.HelpCard(false);
            Assert.That(card, Is.SameAs(InputGlossary.KeyboardHelpCard), "The keyboard's words are the constant; the sink substitutes a rebound key.");
            var lines = card.Split('\n');
            Assert.That(lines, Has.Length.EqualTo(7), "Seven lines (L14).");
            Assert.That(InputGlossary.HelpCardLines, Is.EqualTo(7), "and the box is sized for the longer card.");
            foreach (var line in lines)
                Assert.That(line.Length, Is.LessThanOrEqualTo(InputGlossary.HelpCardLineMost), "'" + line + "' fits the card's box.");
            Assert.That(lines.Last(), Does.EndWith("Esc: close"), "Esc: close stays last.");

            // Every key the card names is the one bound to that act.
            Assert.That(card, Does.Contain(ShippedKey("Camera", "Recenter") + ": recenter"));
            Assert.That(card, Does.Contain(ShippedKey("Shortcuts", "Diary") + ": diary"));
            Assert.That(card, Does.Contain(ShippedKey("Shortcuts", "Interact") + ": interact"));
            Assert.That(card, Does.Contain(ShippedKey("Shortcuts", "Overview") + ": map"));
            Assert.That(card, Does.Contain(ShippedKey("Shortcuts", "Notebook") + ": notebook"));
            Assert.That(card, Does.Contain(ShippedKey("Shortcuts", "Emote") + ": moves"));
            Assert.That(card, Does.Contain(First(InputGlossary.Find("Camera", "Next").Keyboard) + "/" + First(InputGlossary.Find("Camera", "Previous").Keyboard) + ": follow"));
            Assert.That(card, Does.Contain(InputGlossary.Find("Shortcuts", "Menu").Keyboard + ": close"));
            Assert.That(card, Does.Contain("M: map").And.Contain("J: notebook").And.Contain("G: moves").And.Contain("]/[: follow"));
        }

        [Test]
        public void TheHelpCardsNewKeys_FollowARebinding()
        {
            var bindings = new KeyBindings();
            bindings.Set("Shortcuts", "Overview", KeyBindings.KeyboardScheme, "<Keyboard>/n");
            bindings.Set("Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/k");
            bindings.Set("Shortcuts", "Emote", KeyBindings.KeyboardScheme, "<Keyboard>/h");
            KeyBindings.Show(bindings);
            string said = KeyBindings.Substitute(InputGlossary.KeyboardHelpCard);
            Assert.That(said, Does.Contain("N: map").And.Contain("K: notebook").And.Contain("H: moves"));
            Assert.That(said, Does.Not.Contain("M: map").And.Not.Contain("J: notebook").And.Not.Contain("G: moves"), "No key the act has left.");
            Assert.That(said, Does.EndWith("\n]/[: follow · Esc: close"), "The keys nobody rebinds stay as written.");

            KeyBindings.Show(null);
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardHelpCard), Is.SameAs(InputGlossary.KeyboardHelpCard), "As shipped, the very string.");
        }

        [Test]
        public void ThePanelHint_ReadsAsItAlwaysDidOnTheKeyboard_AndNamesTheModulesButtonsOnAPad()
        {
            Assert.That(InputGlossary.PanelHint(false, false), Is.EqualTo("Tab / Up / Down select · Enter confirm"));
            Assert.That(InputGlossary.PanelHint(false, true), Is.EqualTo("Tab / Up / Down select · Enter confirm · Scroll for more"));
            Assert.That(InputGlossary.PanelHint(false, false), Does.Contain(InputGlossary.Find(KeyBindings.PanelMap, KeyBindings.PanelNavigate).Keyboard + " select")
                .And.Contain(InputGlossary.Find(KeyBindings.PanelMap, KeyBindings.PanelSubmit).Keyboard + " confirm"), "The keyboard's keys are the module's.");

            Assert.That(InputGlossary.PanelHint(true, false), Is.EqualTo("D-pad select · A confirm"));
            Assert.That(InputGlossary.PanelHint(true, true), Is.EqualTo("D-pad select · A confirm · More below"));
            Assert.That(InputGlossary.PanelHint(true, false), Is.EqualTo(
                First(InputGlossary.Find(KeyBindings.PanelMap, KeyBindings.PanelNavigate).Pad) + " select · "
                + First(InputGlossary.Find(KeyBindings.PanelMap, KeyBindings.PanelSubmit).Pad) + " confirm"), "The pad's buttons are the module's.");
            Assert.That(KeyBindings.ReservedPaths.Select(InputGlossary.BindingLabel), Does.Contain("A"), "A is the submit's, kept from every rebinding.");
            Assert.That(KeyBindings.FindTarget(KeyBindings.PanelMap, KeyBindings.PanelNavigate), Is.Null, "and nobody rebinds the navigation,");
            Assert.That(KeyBindings.FindTarget(KeyBindings.PanelMap, KeyBindings.PanelSubmit), Is.Null, "or the submit, so the hint is always true.");
            foreach (bool scrolls in new[] { false, true })
                Assert.That(InputGlossary.PanelHint(true, scrolls), Does.Not.Contain("Tab").And.Not.Contain("Enter").And.Not.Contain("Scroll"),
                    "Nothing a pad cannot press.");
        }

        [Test]
        public void TheFollowHint_NamesTheBumpersAndR3OnAPad_AndTheBracketsOnTheKeyboard()
        {
            Assert.That(InputGlossary.FollowHint(true), Is.EqualTo("R3 recenter · LB/RB follow"));
            Assert.That(InputGlossary.FollowHint(true), Is.EqualTo(ShippedButton("Camera", "Recenter") + " recenter · "
                + InputGlossary.Find("Camera", "Previous").Pad + "/" + InputGlossary.Find("Camera", "Next").Pad + " follow"));
            Assert.That(InputGlossary.FollowHint(false), Is.EqualTo("F recenter · ]/[ follow"));
            Assert.That(InputGlossary.FollowHint(false), Is.EqualTo(ShippedKey("Camera", "Recenter") + " recenter · "
                + First(InputGlossary.Find("Camera", "Next").Keyboard) + "/" + First(InputGlossary.Find("Camera", "Previous").Keyboard) + " follow"));
        }

        [Test]
        public void TheFollowHint_FollowsARebindingOfTheRecenter()
        {
            var bindings = new KeyBindings();
            bindings.Set("Camera", "Recenter", KeyBindings.KeyboardScheme, "<Keyboard>/v");
            bindings.Set("Camera", "Recenter", KeyBindings.GamepadScheme, "<Gamepad>/leftStickPress");
            KeyBindings.Show(bindings);
            Assert.That(InputGlossary.FollowHint(false), Is.EqualTo("V recenter · ]/[ follow"));
            Assert.That(InputGlossary.FollowHint(true), Is.EqualTo("L3 recenter · LB/RB follow"));
            Assert.That(KeyBindings.Substitute(InputGlossary.FollowHint(false)), Is.EqualTo("V recenter · ]/[ follow"), "and the sink says it once.");

            KeyBindings.Show(null);
            Assert.That(InputGlossary.FollowHint(false), Is.EqualTo("F recenter · ]/[ follow"), "and back as shipped.");
            Assert.That(InputGlossary.FollowHint(true), Is.EqualTo("R3 recenter · LB/RB follow"));
        }
    }
}

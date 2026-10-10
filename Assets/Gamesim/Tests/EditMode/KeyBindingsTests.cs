using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// Rebinding (PLAN A, A5): what a player may rebind, the rules a new binding keeps - the ways out
    /// are never taken, and no two actions of a context answer to one key - the choices as kept, and
    /// the words on screen, which follow a choice at the text sink and are, with every binding as
    /// shipped, the very strings they were (the lead's decision A-2). Unity-free.
    /// </summary>
    public sealed class KeyBindingsTests
    {
        [TearDown]
        public void ShowTheShippedBindings() => KeyBindings.Show(null);

        /// <summary>The house as shipped, as the conflict check reads it: the keys and buttons the glossary words.</summary>
        private static List<KeyBindings.Binding> Shipped() => new List<KeyBindings.Binding>
        {
            new KeyBindings.Binding("Shortcuts", "Menu", KeyBindings.KeyboardScheme, "<Keyboard>/escape"),
            new KeyBindings.Binding("Shortcuts", "Menu", KeyBindings.GamepadScheme, "<Gamepad>/start"),
            new KeyBindings.Binding("Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/j"),
            new KeyBindings.Binding("Shortcuts", "Notebook", KeyBindings.GamepadScheme, "<Gamepad>/select"),
            new KeyBindings.Binding("Shortcuts", "Save", KeyBindings.KeyboardScheme, "<Keyboard>/f5"),
            new KeyBindings.Binding("Shortcuts", "Diary", KeyBindings.KeyboardScheme, "<Keyboard>/r"),
            new KeyBindings.Binding("Shortcuts", "Diary", KeyBindings.GamepadScheme, "<Gamepad>/buttonNorth"),
            new KeyBindings.Binding("Shortcuts", "Interact", KeyBindings.KeyboardScheme, "<Keyboard>/e"),
            new KeyBindings.Binding("Shortcuts", "Hit", KeyBindings.KeyboardScheme, "<Keyboard>/space"),
            new KeyBindings.Binding("Shortcuts", "Back", KeyBindings.GamepadScheme, "<Gamepad>/buttonEast"),
            new KeyBindings.Binding("Camera", "Recenter", KeyBindings.KeyboardScheme, "<Keyboard>/f"),
            // A composite's part: the camera's pan answers to W.
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.KeyboardScheme, "<Keyboard>/w"),
            new KeyBindings.Binding("Competition", "Pause", KeyBindings.KeyboardScheme, "<Keyboard>/p"),
        };

        [Test]
        public void TheHousesShortcutsAndTheRecentreRebind_AndTheWaysOutNever()
        {
            Assert.That(KeyBindings.Targets.Select(target => target.Path), Is.EqualTo(new[]
            {
                "Shortcuts/Notebook", "Shortcuts/Save", "Shortcuts/Diary", "Shortcuts/Interact", "Shortcuts/Hit",
                "Shortcuts/Overview", "Shortcuts/Emote", "Camera/Recenter",
            }));
            Assert.That(KeyBindings.Targets.Select(target => target.Caption), Is.Unique);
            Assert.That(KeyBindings.FindTarget("Shortcuts", "Notebook").Caption, Is.EqualTo("Rebind Notebook"));
            foreach (var path in KeyBindings.ProtectedActions)
            {
                var parts = path.Split('/');
                Assert.That(KeyBindings.FindTarget(parts[0], parts[1]), Is.Null, path + " is never offered.");
            }
            Assert.That(KeyBindings.ProtectedActions, Is.EquivalentTo(new[] { "Shortcuts/Menu", "Shortcuts/Back", "UI/Submit", "UI/Cancel" }));
            Assert.That(KeyBindings.ResetCaption, Is.EqualTo("Reset all controls"));
        }

        [TestCase("<Keyboard>/escape")]
        [TestCase("<Keyboard>/enter")]
        [TestCase("<Keyboard>/numpadEnter")]
        public void AWayOutsKey_IsNeverTaken(string path)
        {
            string refusal = KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, path);
            Assert.That(refusal, Does.Contain("stays as it is").And.Contain("Notebook keeps its key"));
        }

        [TestCase("<Gamepad>/start")]
        [TestCase("<Gamepad>/buttonEast")]
        public void AWayOutsButton_IsNeverTaken(string path) =>
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Diary", KeyBindings.GamepadScheme, path), Does.Contain("Diary keeps its button"));

        [Test]
        public void AKeyAnotherHouseActionAnswersTo_IsRefused_AcrossTheCameraAndTheShortcuts()
        {
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/r"),
                Is.EqualTo("R is already Diary's. Notebook keeps its key."));
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/f"),
                Is.EqualTo("F is already Recenter's. Notebook keeps its key."), "The camera and the shortcuts are one context: both are live in the house.");
            Assert.That(KeyBindings.Check(Shipped(), "Camera", "Recenter", KeyBindings.KeyboardScheme, "<Keyboard>/w"),
                Does.StartWith("W is already Pan's."), "A composite's part is a binding too.");
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Diary", KeyBindings.GamepadScheme, "<Gamepad>/select"),
                Is.EqualTo("Select is already Notebook's. Diary keeps its button."));
        }

        [Test]
        public void AKeyOnlyAnotherContextAnswersTo_IsAllowed_AndAnActionsOwnKeyToo()
        {
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/p"), Is.Null,
                "P is a competition's pause, and a competition's board is not the house.");
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/k"), Is.Null);
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/j"), Is.Null, "Its own key again.");
        }

        [Test]
        public void AnotherDevicesControl_OrAnActionNotOffered_IsRefused()
        {
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Gamepad>/buttonNorth"), Is.EqualTo("That is not a key."));
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.GamepadScheme, "<Keyboard>/k"), Is.EqualTo("That is not a pad button."));
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Menu", KeyBindings.KeyboardScheme, "<Keyboard>/k"), Does.Contain("cannot be rebound"));
            Assert.That(() => new KeyBindings().Set("Shortcuts", "Menu", KeyBindings.KeyboardScheme, "<Keyboard>/k"), Throws.ArgumentException);
        }

        [Test]
        public void TheChoices_AreKeptAsOneObject_AndReadBack()
        {
            var bindings = new KeyBindings();
            Assert.That(bindings.ToJson(), Is.EqualTo("{}"));
            bindings.Set("Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/k");
            bindings.Set("Camera", "Recenter", KeyBindings.GamepadScheme, "<Gamepad>/leftShoulder");
            string json = bindings.ToJson();
            Assert.That(json, Is.EqualTo("{\"Camera/Recenter@Gamepad\":\"<Gamepad>/leftShoulder\",\"Shortcuts/Notebook@Keyboard&Mouse\":\"<Keyboard>/k\"}"),
                "Ordinal key order, so equal choices are equal text.");
            var read = KeyBindings.Parse(json, out var reason);
            Assert.That(reason, Is.Null);
            Assert.That(read.Overrides, Is.EqualTo(bindings.Overrides));
            Assert.That(read.Get("Shortcuts", "Notebook", KeyBindings.KeyboardScheme), Is.EqualTo("<Keyboard>/k"));
            Assert.That(read.Get("Shortcuts", "Notebook", KeyBindings.GamepadScheme), Is.Null);
            read.Set("Shortcuts", "Notebook", KeyBindings.KeyboardScheme, null);
            Assert.That(read.Count, Is.EqualTo(1), "Setting a choice to nothing puts it back as shipped.");
            read.Clear();
            Assert.That(read.Count, Is.Zero);
            Assert.That(read.Summary(), Is.EqualTo("default"));
            Assert.That(bindings.Summary(), Is.EqualTo("Notebook:K,Recenter:LB(pad)"));
        }

        [TestCase("", null)]
        [TestCase("   ", null)]
        [TestCase("{ not json", "not valid JSON")]
        [TestCase("{\"Shortcuts/Menu@Keyboard&Mouse\":\"<Keyboard>/k\"}", "not one a player may choose")]
        [TestCase("{\"Shortcuts/Notebook@Keyboard&Mouse\":\"<Keyboard>/escape\"}", "not one a player may choose")]
        [TestCase("{\"Shortcuts/Notebook@Keyboard&Mouse\":\"<Gamepad>/start\"}", "not one a player may choose")]
        [TestCase("{\"Shortcuts/Notebook@Keyboard&Mouse\":3}", "not one the game reads")]
        [TestCase("{\"Notebook\":\"<Keyboard>/k\"}", "not one the game reads")]
        public void AKeptTextThatIsNotAChoice_ReadsAsShipped_WithTheReason_AndNeverThrows(string json, string why)
        {
            var read = KeyBindings.Parse(json, out var reason);
            Assert.That(read.Count, Is.Zero);
            if (why == null) Assert.That(reason, Is.Null);
            else Assert.That(reason, Does.Contain(why));
        }

        [Test]
        public void AKeptTextWithOneBadChoice_KeepsTheGoodOnes()
        {
            var read = KeyBindings.Parse("{\"Shortcuts/Notebook@Keyboard&Mouse\":\"<Keyboard>/k\",\"Shortcuts/Menu@Gamepad\":\"<Gamepad>/select\"}", out var reason);
            Assert.That(read.Count, Is.EqualTo(1));
            Assert.That(reason, Is.Not.Null);
        }

        /// <summary>Every caption and line the HUD draws that names a key, as the code writes them.</summary>
        private static IEnumerable<string> WordsThatNameKeys() => new[]
        {
            "Close  [Esc]", "Save now  [F5]", "Save [F5]", "Notebook [J]", "Go to diary room [R]", "Interact [E]", "[R]", "[J]", "[F5]", "[Esc]",
            InputGlossary.HouseChallengeStopCaption, "Hit the target  [Space]", "Back to exploring    [Esc]", "Space  Speed up   ·   Enter  Skip",
            InputGlossary.KeyboardHelpCard, InputGlossary.KeyboardTourLine, InputGlossary.KeyboardStationArrived, InputGlossary.KeyboardStationHeading,
            InputGlossary.KeyboardDiaryArrived, InputGlossary.KeyboardDiaryWalk, InputGlossary.KeyboardCeremonySuspenseful, InputGlossary.KeyboardCeremonyQuick,
            InputGlossary.KeyboardStationStepKeys, InputGlossary.KeyboardChallenge, InputGlossary.ActivityLine("Sleep", false), InputGlossary.ActivityLine("Other", false),
            "E  ·  Talk to Maya", "E  ·  Open episode screen", "Rebind Notebook",
        };

        [Test]
        public void WithEveryBindingAsShipped_EveryWordIsTheVeryStringItWas()
        {
            foreach (var shown in new[] { null, new KeyBindings() })
            {
                KeyBindings.Show(shown);
                foreach (var words in WordsThatNameKeys())
                    Assert.That(KeyBindings.Substitute(words), Is.SameAs(words), "'" + words + "' is drawn as written.");
            }
            Assert.That(KeyBindings.Substitute(null), Is.Null);
            Assert.That(KeyBindings.Substitute(""), Is.EqualTo(""));
        }

        [Test]
        public void ARebindingsKey_IsSubstitutedWhereverItIsNamed_AndNowhereElse()
        {
            var bindings = new KeyBindings();
            bindings.Set("Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/k");
            bindings.Set("Shortcuts", "Interact", KeyBindings.KeyboardScheme, "<Keyboard>/t");
            bindings.Set("Shortcuts", "Hit", KeyBindings.KeyboardScheme, "<Keyboard>/h");
            bindings.Set("Camera", "Recenter", KeyBindings.KeyboardScheme, "<Keyboard>/c");
            KeyBindings.Show(bindings);
            Assert.That(KeyBindings.Substitute("Notebook [J]"), Is.EqualTo("Notebook [K]"));
            Assert.That(KeyBindings.Substitute("[J]"), Is.EqualTo("[K]"));
            Assert.That(KeyBindings.Substitute("Close  [Esc]"), Is.EqualTo("Close  [Esc]"), "The ways out are never rebound.");
            Assert.That(KeyBindings.Substitute("Save now  [F5]"), Is.EqualTo("Save now  [F5]"), "A key still as shipped stays.");
            Assert.That(KeyBindings.Substitute("Interact [E]"), Is.EqualTo("Interact [T]"));
            Assert.That(KeyBindings.Substitute(InputGlossary.HouseChallengeStopCaption), Is.EqualTo("STOP marker  [H]"));
            Assert.That(KeyBindings.Substitute("Hit the target  [Space]"), Is.EqualTo("Hit the target  [Space]"), "A competition's Hit is its own, and not rebound.");
            Assert.That(KeyBindings.Substitute("Space  Speed up   ·   Enter  Skip"), Is.EqualTo("Space  Speed up   ·   Enter  Skip"));
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardHelpCard), Is.EqualTo(
                "Click a houseguest: talk\nClick floor: walk  ·  C: recenter\nWASD/arrows: pan  ·  Wheel: zoom\nRight-drag: orbit · Mid-drag: pan\nR: diary · T: interact · Esc: close"));
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardTourLine), Is.EqualTo("Click the floor to walk. Press T near a houseguest to talk. Esc closes this tour."));
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardStationArrived), Is.EqualTo("At the episode screen  ·  T to open"));
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardDiaryWalk), Is.EqualTo("Walk to the private room, then press T to open your diary. No choice is committed by entering."));
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardStationStepKeys), Is.EqualTo("press T to open it and Esc to close it."));
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardChallenge), Does.StartWith("Press H or STOP when the marker").And.Contain("Escape cancels"));
            Assert.That(KeyBindings.Substitute(InputGlossary.ActivityLine("Sleep", false)), Is.EqualTo("Off to bed  ·  T or a click to get up"));
            Assert.That(KeyBindings.Substitute(InputGlossary.ActivityLine("Other", false)), Is.EqualTo("At the furniture  ·  T to finish"));
            Assert.That(KeyBindings.Substitute("E  ·  Talk to Maya"), Is.EqualTo("T  ·  Talk to Maya"), "The house prompt's leading key.");
            Assert.That(KeyBindings.Substitute("Everyone rests. Read the room."), Is.EqualTo("Everyone rests. Read the room."), "A letter that is not a key's place stays.");
            Assert.That(KeyBindings.Substitute("Rebind Notebook"), Is.EqualTo("Rebind Notebook"));
        }

        [Test]
        public void TwoKeysSwapped_AreEachSaidOnce()
        {
            var bindings = new KeyBindings();
            bindings.Set("Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/r");
            bindings.Set("Shortcuts", "Diary", KeyBindings.KeyboardScheme, "<Keyboard>/j");
            KeyBindings.Show(bindings);
            Assert.That(KeyBindings.Substitute("Notebook [J]"), Is.EqualTo("Notebook [R]"));
            Assert.That(KeyBindings.Substitute("Go to diary room [R]"), Is.EqualTo("Go to diary room [J]"));
            Assert.That(KeyBindings.Substitute("[J] [R]"), Is.EqualTo("[R] [J]"), "One pass: neither is swapped back.");
        }

        [Test]
        public void APadRebinding_IsWhatTheHintsNameOnAPad()
        {
            string before = InputGlossary.HelpCard(true);
            var bindings = new KeyBindings();
            bindings.Set("Shortcuts", "Diary", KeyBindings.GamepadScheme, "<Gamepad>/leftShoulder");
            bindings.Set("Shortcuts", "Interact", KeyBindings.GamepadScheme, "<Gamepad>/rightShoulder");
            KeyBindings.Show(bindings);
            Assert.That(KeyBindings.PadLabel("Shortcuts", "Diary"), Is.EqualTo("LB"));
            Assert.That(KeyBindings.PadLabel("Shortcuts", "Notebook"), Is.Null, "As shipped: the glossary's own words.");
            Assert.That(InputGlossary.HelpCard(true), Does.StartWith("RB: talk · LB: diary"));
            Assert.That(InputGlossary.StationLine(true, true), Is.EqualTo("At the episode screen  ·  RB to open"));
            Assert.That(InputGlossary.PromptKey(true), Is.EqualTo("RB"));
            Assert.That(InputGlossary.HelpCard(false), Is.SameAs(InputGlossary.KeyboardHelpCard), "The keyboard's words are the constant; the sink substitutes them.");
            KeyBindings.Show(null);
            Assert.That(InputGlossary.HelpCard(true), Is.EqualTo(before), "and back as shipped.");
        }
    }
}

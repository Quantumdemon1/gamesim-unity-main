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
            // Composites' parts: the camera's pan answers to WASD and the arrows, its orbit to Q and
            // C, its zoom to - and =.
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.KeyboardScheme, "<Keyboard>/w", "Up"),
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.KeyboardScheme, "<Keyboard>/s", "Down"),
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.KeyboardScheme, "<Keyboard>/a", "Left"),
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.KeyboardScheme, "<Keyboard>/d", "Right"),
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.KeyboardScheme, "<Keyboard>/upArrow", "Up"),
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.KeyboardScheme, "<Keyboard>/leftArrow", "Left"),
            new KeyBindings.Binding("Camera", "OrbitRate", KeyBindings.KeyboardScheme, "<Keyboard>/q", "Left"),
            new KeyBindings.Binding("Camera", "OrbitRate", KeyBindings.KeyboardScheme, "<Keyboard>/c", "Right"),
            new KeyBindings.Binding("Camera", "ZoomRate", KeyBindings.KeyboardScheme, "<Keyboard>/minus", "Negative"),
            new KeyBindings.Binding("Camera", "ZoomRate", KeyBindings.KeyboardScheme, "<Keyboard>/equals", "Positive"),
            new KeyBindings.Binding("Competition", "Pause", KeyBindings.KeyboardScheme, "<Keyboard>/p"),
            new KeyBindings.Binding("Competition", "Hit", KeyBindings.KeyboardScheme, "<Keyboard>/space"),
            new KeyBindings.Binding("Competition", "Hold", KeyBindings.KeyboardScheme, "<Keyboard>/space"),
            new KeyBindings.Binding("Competition", "Hold", KeyBindings.GamepadScheme, "<Gamepad>/rightTrigger"),
            new KeyBindings.Binding("Competition", "Undo", KeyBindings.KeyboardScheme, "<Keyboard>/backspace"),
            new KeyBindings.Binding("Competition", "Undo", KeyBindings.GamepadScheme, "<Gamepad>/buttonWest"),
            new KeyBindings.Binding("Competition", "FocusNext", KeyBindings.KeyboardScheme, "<Keyboard>/tab"),
            // The pad's camera: the pan on the whole left stick, the orbit on the whole right one.
            new KeyBindings.Binding("Camera", "Pan", KeyBindings.GamepadScheme, "<Gamepad>/leftStick"),
            new KeyBindings.Binding("Camera", "OrbitRate", KeyBindings.GamepadScheme, "<Gamepad>/rightStick"),
            new KeyBindings.Binding("Shortcuts", "Hit", KeyBindings.GamepadScheme, "<Gamepad>/buttonSouth"),
            new KeyBindings.Binding("Shortcuts", "Overview", KeyBindings.GamepadScheme, "<Gamepad>/dpad/up"),
            new KeyBindings.Binding("Shortcuts", "Emote", KeyBindings.GamepadScheme, "<Gamepad>/dpad/right"),
            // The event system's navigation, as HouseBindings.LiveBindings reads it from the UI
            // module (DefaultInputActions' UI/Navigate): the sticks' directions and the whole D-pad
            // on the pad, WASD and the arrows on the keyboard.
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.GamepadScheme, "<Gamepad>/leftStick/up", "up"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.GamepadScheme, "<Gamepad>/rightStick/up", "up"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.GamepadScheme, "<Gamepad>/leftStick/down", "down"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.GamepadScheme, "<Gamepad>/rightStick/down", "down"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.GamepadScheme, "<Gamepad>/dpad"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/w", "up"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/upArrow", "up"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/s", "down"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/downArrow", "down"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/a", "left"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/leftArrow", "left"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/d", "right"),
            new KeyBindings.Binding("UI", "Navigate", KeyBindings.KeyboardScheme, "<Keyboard>/rightArrow", "right"),
        };

        /// <summary>The bindings with one moved, as a rebinding moves it: <paramref name="map"/>/<paramref name="action"/>'s <paramref name="from"/> answers to <paramref name="to"/> now.</summary>
        private static List<KeyBindings.Binding> Moved(List<KeyBindings.Binding> live, string map, string action, string from, string to) =>
            live.Select(binding => binding.Map == map && binding.Action == action && binding.Path == from
                ? new KeyBindings.Binding(map, action, binding.Scheme, to, binding.Part) : binding).ToList();

        [Test]
        public void TheHousesShortcutsAndTheCamerasKeysRebind_AndTheWaysOutNever()
        {
            Assert.That(KeyBindings.Targets.Select(target => target.Path + (target.Part != null ? "#" + target.Part : "")), Is.EqualTo(new[]
            {
                "Shortcuts/Notebook", "Shortcuts/Save", "Shortcuts/Diary", "Shortcuts/Interact", "Shortcuts/Hit",
                "Shortcuts/Overview", "Shortcuts/Emote", "Camera/Recenter",
                "Camera/OrbitRate#Left", "Camera/OrbitRate#Right", "Camera/ZoomRate#Negative", "Camera/ZoomRate#Positive",
                "Camera/Pan#Up", "Camera/Pan#Left", "Camera/Pan#Down", "Camera/Pan#Right",
                "Competition/Hold", "Competition/Undo",
            }));
            Assert.That(KeyBindings.FindTarget("Competition", "Hold").Caption, Is.EqualTo("Rebind Hold grip"));
            Assert.That(KeyBindings.FindTarget("Competition", "Undo").Caption, Is.EqualTo("Rebind Take back letter"));
            Assert.That(KeyBindings.FindTarget("Competition", "Pause"), Is.Null, "P's pause stays: the word board treats it as a letter, and the pad's is Start.");
            Assert.That(KeyBindings.Targets.Select(target => target.Caption), Is.Unique);
            Assert.That(KeyBindings.Targets.Where(target => target.Part != null).Select(target => target.Caption), Is.EqualTo(new[]
            {
                "Rebind Orbit left", "Rebind Orbit right", "Rebind Zoom out", "Rebind Zoom in", "Rebind Pan up", "Rebind Pan left", "Rebind Pan down", "Rebind Pan right",
            }));
            Assert.That(KeyBindings.Targets.Where(target => target.Part != null).All(target => !target.OnPad && !target.Takes(KeyBindings.GamepadScheme)), Is.True,
                "The camera's keys are the keyboard's: the pad turns, zooms and pans with its sticks and triggers.");
            Assert.That(KeyBindings.Targets.Where(target => target.Part == null).All(target => target.OnPad), Is.True, "Every shortcut has a pad button to rebind.");
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
        [TestCase("<Gamepad>/buttonSouth")]
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
                Does.StartWith("W is already Pan up's."), "A composite's part is a binding too.");
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
            Assert.That(bindings.Summary(), Is.EqualTo("Notebook:k,Recenter:leftShoulder(pad)"));
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

        // ---------------------------------------------------------------- the camera's keys

        private static KeyBindings.Target Camera(string action, string part) => KeyBindings.FindTarget("Camera", action, part);

        [Test]
        public void ACameraKey_IsAKeyboardKey_AndItsPartsAreRefusedOneAnothersKeys()
        {
            var up = Camera("Pan", "Up");
            Assert.That(up.Name, Is.EqualTo("Pan up"));
            Assert.That(KeyBindings.Check(Shipped(), up, KeyBindings.GamepadScheme, "<Gamepad>/dpad/up"),
                Is.EqualTo("Pan up is a key: press the new one on the keyboard."), "The pad pans with its stick.");
            Assert.That(KeyBindings.Check(Shipped(), up, KeyBindings.KeyboardScheme, "<Keyboard>/a"), Is.EqualTo("A is already Pan left's. Pan up keeps its key."),
                "The pan's up is refused its left.");
            Assert.That(KeyBindings.Check(Shipped(), up, KeyBindings.KeyboardScheme, "<Keyboard>/leftArrow"), Is.EqualTo("Left is already Pan left's. Pan up keeps its key."),
                "and the arrows' left: a part of the other composite is a binding of the house too.");
            Assert.That(KeyBindings.Check(Shipped(), up, KeyBindings.KeyboardScheme, "<Keyboard>/upArrow"), Is.Null, "Up is up already: the same part of the pan.");
            Assert.That(KeyBindings.Check(Shipped(), up, KeyBindings.KeyboardScheme, "<Keyboard>/w"), Is.Null, "Its own key again.");
            Assert.That(KeyBindings.Check(Shipped(), up, KeyBindings.KeyboardScheme, "<Keyboard>/i"), Is.Null);
            Assert.That(KeyBindings.Check(Shipped(), up, KeyBindings.KeyboardScheme, "<Keyboard>/j"), Is.EqualTo("J is already Notebook's. Pan up keeps its key."),
                "The camera's keys and the shortcuts are one house.");
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/q"), Is.EqualTo("Q is already Orbit left's. Notebook keeps its key."));
            Assert.That(KeyBindings.Check(Shipped(), Camera("ZoomRate", "Positive"), KeyBindings.KeyboardScheme, "<Keyboard>/minus"),
                Is.EqualTo("- is already Zoom out's. Zoom in keeps its key."));
            Assert.That(KeyBindings.Check(Shipped(), Camera("OrbitRate", "Left"), KeyBindings.KeyboardScheme, "<Keyboard>/escape"),
                Does.Contain("stays as it is").And.Contain("Orbit left keeps its key"), "A way out is never a camera key either.");
            Assert.That(() => new KeyBindings().Set(up, KeyBindings.GamepadScheme, "<Gamepad>/dpad/up"), Throws.ArgumentException);
        }

        [Test]
        public void ACameraKeysChoice_IsKeptByItsPart_AndReadBack()
        {
            var bindings = new KeyBindings();
            bindings.Set(Camera("Pan", "Up"), KeyBindings.KeyboardScheme, "<Keyboard>/i");
            bindings.Set(Camera("ZoomRate", "Negative"), KeyBindings.KeyboardScheme, "<Keyboard>/z");
            string json = bindings.ToJson();
            Assert.That(json, Is.EqualTo("{\"Camera/Pan#Up@Keyboard&Mouse\":\"<Keyboard>/i\",\"Camera/ZoomRate#Negative@Keyboard&Mouse\":\"<Keyboard>/z\"}"));
            var read = KeyBindings.Parse(json, out var reason);
            Assert.That(reason, Is.Null);
            Assert.That(read.Get(Camera("Pan", "Up"), KeyBindings.KeyboardScheme), Is.EqualTo("<Keyboard>/i"));
            Assert.That(read.Get(Camera("Pan", "Down"), KeyBindings.KeyboardScheme), Is.Null, "One part's choice is that part's alone.");
            Assert.That(read.Get("Camera", "Pan", KeyBindings.KeyboardScheme), Is.Null, "and the pan has no own binding to choose.");
            Assert.That(read.Summary(), Is.EqualTo("Zoom_out:z,Pan_up:i"), "In the settings' order.");

            foreach (string kept in new[]
            {
                "{\"Camera/Pan#Up@Gamepad\":\"<Gamepad>/dpad/up\"}",
                "{\"Camera/Pan#Sideways@Keyboard&Mouse\":\"<Keyboard>/i\"}",
                "{\"Camera/Pan@Keyboard&Mouse\":\"<Keyboard>/i\"}",
                "{\"Camera/Pan#Up@Keyboard&Mouse\":\"<Keyboard>/enter\"}",
            })
            {
                var dropped = KeyBindings.Parse(kept, out var why);
                Assert.That(dropped.Count, Is.Zero, kept + " is not a choice a player could make.");
                Assert.That(why, Does.Contain("not one a player may choose"), kept);
            }
        }

        [Test]
        public void TheCamerasKeys_AreSaidAsTheyAre_InTheHelpCardAndTheCameraSettings()
        {
            const string settings = "The speed is the mouse's drag, the right stick and Q and C alike; the tilt is up and down on each.";
            KeyBindings.Show(new KeyBindings());
            Assert.That(KeyBindings.Substitute(settings), Is.SameAs(settings), "As shipped, the very string.");

            var bindings = new KeyBindings();
            bindings.Set(Camera("Pan", "Up"), KeyBindings.KeyboardScheme, "<Keyboard>/i");
            bindings.Set(Camera("Pan", "Left"), KeyBindings.KeyboardScheme, "<Keyboard>/j");
            bindings.Set(Camera("OrbitRate", "Right"), KeyBindings.KeyboardScheme, "<Keyboard>/v");
            KeyBindings.Show(bindings);
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardHelpCard), Is.EqualTo(
                "Click a houseguest: talk\nClick floor: walk  ·  F: recenter\nIJSD/arrows: pan  ·  Wheel: zoom\nRight-drag: orbit · Mid-drag: pan\nR: diary · E: interact · Esc: close"),
                "The pan's keys in WASD's order, the two left as shipped among them.");
            Assert.That(KeyBindings.Substitute(settings), Is.EqualTo(
                "The speed is the mouse's drag, the right stick and Q and V alike; the tilt is up and down on each."));
            Assert.That(KeyBindings.Substitute("Notebook [J]"), Is.EqualTo("Notebook [J]"), "J is the pan's left now, but the notebook's [J] is the notebook's.");

            bindings.Set(Camera("Pan", "Down"), KeyBindings.KeyboardScheme, "<Keyboard>/numpad2");
            Assert.That(KeyBindings.Substitute(InputGlossary.KeyboardHelpCard), Does.Contain("\nI J " + InputGlossary.BindingLabel("<Keyboard>/numpad2") + " D/arrows: pan  ·  "),
                "A key of more than one letter spaces the four.");
        }

        // ---------------------------------------------------------------- a competition's board

        [Test]
        public void ABoardsKey_IsNeverALetter_AndTheBoardIsAContextOfItsOwn()
        {
            var hold = KeyBindings.FindTarget("Competition", "Hold");
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.KeyboardScheme, "<Keyboard>/k"),
                Is.EqualTo("K spells on the word board, so it stays a letter. Hold grip keeps its key."), "The word board spells with every letter.");
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.KeyboardScheme, "<Keyboard>/leftShift"), Is.Null, "Left Shift is nobody's on the board.");
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.KeyboardScheme, "<Keyboard>/j"), Does.Contain("spells on the word board"),
                "J is the notebook's in the house, but on the board it is a letter.");
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.KeyboardScheme, "<Keyboard>/f"), Does.Contain("spells on the word board"));
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.KeyboardScheme, "<Keyboard>/backspace"),
                Is.EqualTo("Backspace is already Take back letter's. Hold grip keeps its key."), "The board's own keys are its context.");
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.KeyboardScheme, "<Keyboard>/tab"),
                Is.EqualTo("Tab already does something else on a competition's board. Hold grip keeps its key."), "An action nobody rebinds is said by where it is.");
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.KeyboardScheme, "<Keyboard>/f5"), Is.Null, "F5 is the house's save, and the board is not the house.");
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Save", KeyBindings.KeyboardScheme, "<Keyboard>/backspace"), Is.Null, "and the other way about.");
            Assert.That(KeyBindings.Check(Shipped(), hold, KeyBindings.GamepadScheme, "<Gamepad>/leftTrigger"), Is.Null, "The grip's pad button rebinds too.");
            Assert.That(KeyBindings.Check(Shipped(), KeyBindings.FindTarget("Competition", "Undo"), KeyBindings.GamepadScheme, "<Gamepad>/rightTrigger"),
                Is.EqualTo("RT is already Hold grip's. Take back letter keeps its button."));
            Assert.That(KeyBindings.Parse("{\"Competition/Hold@Keyboard&Mouse\":\"<Keyboard>/k\"}", out var why).Count, Is.Zero, "A letter kept for the board is dropped,");
            Assert.That(why, Does.Contain("not one a player may choose"));
            Assert.That(KeyBindings.Parse("{\"Competition/Hold@Keyboard&Mouse\":\"<Keyboard>/leftShift\"}", out why).Count, Is.EqualTo(1), "and a key that is not one is kept.");
        }

        [Test]
        public void ABoardSaysItsKeysAsTheyAre_AndAsWrittenWhileTheyAreAsShipped()
        {
            KeyBindings.Show(null);
            Assert.That(KeyBindings.AnyChosen, Is.False);
            Assert.That(KeyBindings.BoardKey("Hold", false, "Space"), Is.EqualTo("Space"));
            Assert.That(KeyBindings.BoardKey("Hold", true, "right trigger"), Is.EqualTo("right trigger"), "As shipped, the board's own words.");
            var bindings = new KeyBindings();
            bindings.Set(KeyBindings.FindTarget("Competition", "Hold"), KeyBindings.KeyboardScheme, "<Keyboard>/leftShift");
            bindings.Set(KeyBindings.FindTarget("Competition", "Undo"), KeyBindings.GamepadScheme, "<Gamepad>/buttonNorth");
            KeyBindings.Show(bindings);
            Assert.That(KeyBindings.AnyChosen, Is.True);
            Assert.That(KeyBindings.BoardKey("Hold", false, "Space"), Is.EqualTo("Left Shift"), "A key a player may choose is named as the keyboard prints it.");
            Assert.That(KeyBindings.BoardKey("Hold", true, "RT"), Is.EqualTo("RT"), "The pad's grip is as shipped.");
            Assert.That(KeyBindings.BoardKey("Undo", true, "X"), Is.EqualTo("Y"));
            Assert.That(KeyBindings.BoardKey("Undo", false, "Backspace"), Is.EqualTo("Backspace"));
            Assert.That(KeyBindings.Substitute("Hit the target  [Space]"), Is.EqualTo("Hit the target  [Space]"), "The board's words are not the HUD's: nothing of the house moved.");
        }

        // ---------------------------------------------------------------- the panels' own controls (D4)

        /// <summary>
        /// A - the pad's Submit, with which the UI module presses the focused control - is a way out
        /// the game keeps, as Enter is (D4): once the house challenge's Hit has left it, no other
        /// action may take it. The challenge's Hit, which rebinds, was all that kept it before.
        /// </summary>
        [Test]
        public void APadsA_IsNeverTaken_OnceTheChallengesHitHasLeftIt()
        {
            Assert.That(KeyBindings.ReservedPaths, Does.Contain("<Gamepad>/buttonSouth"));
            var hit = KeyBindings.FindTarget("Shortcuts", "Hit");
            Assert.That(KeyBindings.Check(Shipped(), hit, KeyBindings.GamepadScheme, "<Gamepad>/dpad/down"), Is.Null,
                "The challenge's Hit waits for the panels to close: it may have the D-pad's spare down.");
            var live = Moved(Shipped(), "Shortcuts", "Hit", "<Gamepad>/buttonSouth", "<Gamepad>/dpad/down");
            Assert.That(KeyBindings.Check(live, "Shortcuts", "Notebook", KeyBindings.GamepadScheme, "<Gamepad>/buttonSouth"),
                Is.EqualTo("A closes panels and presses controls, so it stays as it is. Notebook keeps its button."));
            Assert.That(KeyBindings.Check(live, "Shortcuts", "Diary", KeyBindings.GamepadScheme, "<Gamepad>/buttonSouth"), Does.Contain("stays as it is"));
            Assert.That(KeyBindings.Parse("{\"Shortcuts/Save@Gamepad\":\"<Gamepad>/buttonSouth\"}", out var why).Count, Is.Zero, "A kept for another action is dropped,");
            Assert.That(why, Does.Contain("not one a player may choose"));
        }

        /// <summary>
        /// The panels' navigation - the D-pad, the sticks, WASD and the arrows - moves between a
        /// panel's controls in every panel, and the notebook's and the save's keys work with a panel
        /// open: neither may take one of its keys or buttons (D4). The rest act only with no panel
        /// open, as the overview's and your moves' D-pad buttons always have, and may.
        /// </summary>
        [Test]
        public void ThePanelsNavigation_IsNeverTheNotebooksOrTheSaves()
        {
            Assert.That(KeyBindings.Targets.Where(target => target.OverPanels).Select(target => target.Name), Is.EqualTo(new[] { "Notebook", "Save" }),
                "The two the house answers to with a panel open (EpisodeDirector.Update).");
            Assert.That(KeyBindings.PanelMap + "/" + KeyBindings.PanelNavigate, Is.EqualTo("UI/Navigate"), "As the UI module's actions name it.");
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Notebook", KeyBindings.GamepadScheme, "<Gamepad>/dpad/down"),
                Is.EqualTo("D-pad down moves between a panel's controls, and Notebook works with a panel open. Notebook keeps its button."));
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Save", KeyBindings.GamepadScheme, "<Gamepad>/dpad/left"),
                Is.EqualTo("D-pad left moves between a panel's controls, and Save works with a panel open. Save keeps its button."));
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Diary", KeyBindings.GamepadScheme, "<Gamepad>/dpad/left"), Is.Null,
                "The diary waits for the panels to close: the D-pad's spare left is its to take.");
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Overview", KeyBindings.GamepadScheme, "<Gamepad>/dpad/down"), Is.Null);
            // A stick's direction is a part of the stick the camera reads.
            Assert.That(KeyBindings.Check(Shipped(), "Shortcuts", "Diary", KeyBindings.GamepadScheme, "<Gamepad>/leftStick/up"),
                Is.EqualTo("Left stick up already does something else in the house. Diary keeps its button."));

            // On the keyboard: the pan's up moved to I leaves W to the panels' navigation alone.
            var live = Moved(Shipped(), "Camera", "Pan", "<Keyboard>/w", "<Keyboard>/i");
            Assert.That(KeyBindings.Check(live, "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/w"),
                Is.EqualTo("W moves between a panel's controls, and Notebook works with a panel open. Notebook keeps its key."));
            Assert.That(KeyBindings.Check(live, "Shortcuts", "Save", KeyBindings.KeyboardScheme, "<Keyboard>/upArrow"), Does.StartWith("Up is already Pan up's."),
                "The pan's own arrows refuse it first.");
            Assert.That(KeyBindings.Check(live, "Shortcuts", "Diary", KeyBindings.KeyboardScheme, "<Keyboard>/w"), Is.Null, "The diary may have it,");
            Assert.That(KeyBindings.Check(live, Camera("Pan", "Up"), KeyBindings.KeyboardScheme, "<Keyboard>/w"), Is.Null, "and the pan's up its own W back.");
        }

        /// <summary>
        /// An action's own key or button as shipped is always its to take back (lane-input-6): the
        /// grip's Space, which the board's Hit - nobody rebinds it - shares as shipped; the challenge's
        /// A, though A is the panels' Submit. Never from another action a player has since given it.
        /// And a choice taken back is no choice: nothing kept, nothing logged.
        /// </summary>
        [Test]
        public void AnActionsOwnKeyAsShipped_IsAlwaysItsToTakeBack_ButNeverFromAnotherChoice()
        {
            var hold = KeyBindings.FindTarget("Competition", "Hold");
            var gripped = Moved(Shipped(), "Competition", "Hold", "<Keyboard>/space", "<Keyboard>/leftShift");
            Assert.That(KeyBindings.Check(gripped, hold, KeyBindings.KeyboardScheme, "<Keyboard>/space"), Is.Null,
                "Space again: the board's Hit shared it as shipped.");
            Assert.That(KeyBindings.Check(gripped, KeyBindings.FindTarget("Competition", "Undo"), KeyBindings.KeyboardScheme, "<Keyboard>/space"),
                Is.EqualTo("Space already does something else on a competition's board. Take back letter keeps its key."), "Another of the board's actions still may not have it.");
            var hit = Moved(Shipped(), "Shortcuts", "Hit", "<Gamepad>/buttonSouth", "<Gamepad>/dpad/down");
            Assert.That(KeyBindings.Check(hit, "Shortcuts", "Hit", KeyBindings.GamepadScheme, "<Gamepad>/buttonSouth"), Is.Null,
                "The challenge's Hit has A back, though A is a way out: it shipped there.");
            var taken = Moved(Moved(Shipped(), "Shortcuts", "Notebook", "<Keyboard>/j", "<Keyboard>/k"), "Shortcuts", "Diary", "<Keyboard>/r", "<Keyboard>/j");
            Assert.That(KeyBindings.Check(taken, "Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/j"),
                Is.EqualTo("J is already Diary's. Notebook keeps its key."), "Its own key as shipped, but the diary has it now.");

            var bindings = new KeyBindings();
            bindings.Set(hold, KeyBindings.KeyboardScheme, "<Keyboard>/leftShift");
            bindings.Set(hold, KeyBindings.KeyboardScheme, "<Keyboard>/space");
            Assert.That(bindings.Count, Is.Zero, "Back as shipped is no choice at all.");
            Assert.That(bindings.Summary(), Is.EqualTo("default"));
            var kept = KeyBindings.Parse("{\"Shortcuts/Hit@Gamepad\":\"<Gamepad>/buttonSouth\",\"Competition/Hold@Keyboard&Mouse\":\"<Keyboard>/space\"}", out var reason);
            Assert.That(kept.Count, Is.Zero, "A kept text naming an action's own as shipped reads as shipped,");
            Assert.That(reason, Is.Null, "with nothing to say about it.");
        }

        // ---------------------------------------------------------------- the player log

        /// <summary>
        /// The player log's keys= (lane-input-8) is one word that reads back as it was chosen: the
        /// control's own name after its device - never the label the keyboard prints, which can be
        /// ",", "=" or "Left Shift" - and the action's name with its spaces as underscores.
        /// </summary>
        [Test]
        public void TheLogsKeys_AreOneWord_AndReadBackAsTheyWereChosen()
        {
            var bindings = new KeyBindings();
            bindings.Set("Shortcuts", "Notebook", KeyBindings.KeyboardScheme, "<Keyboard>/comma");
            bindings.Set("Shortcuts", "Save", KeyBindings.KeyboardScheme, "<Keyboard>/semicolon");
            bindings.Set("Shortcuts", "Diary", KeyBindings.GamepadScheme, "<Gamepad>/dpad/left");
            bindings.Set("Camera", "Recenter", KeyBindings.GamepadScheme, "<Gamepad>/leftShoulder");
            bindings.Set(Camera("ZoomRate", "Negative"), KeyBindings.KeyboardScheme, "<Keyboard>/equals");
            bindings.Set(Camera("Pan", "Up"), KeyBindings.KeyboardScheme, "<Keyboard>/numpadPlus");
            bindings.Set(KeyBindings.FindTarget("Competition", "Hold"), KeyBindings.KeyboardScheme, "<Keyboard>/leftShift");
            bindings.Set(KeyBindings.FindTarget("Competition", "Undo"), KeyBindings.GamepadScheme, "<Gamepad>/leftTrigger");
            string summary = bindings.Summary();
            Assert.That(summary, Is.EqualTo("Notebook:comma,Save:semicolon,Diary:dpad/left(pad),Recenter:leftShoulder(pad),"
                + "Zoom_out:equals,Pan_up:numpadPlus,Hold_grip:leftShift,Take_back_letter:leftTrigger(pad)"));
            Assert.That(Gamesim.Episode.SettingsEvidence.Word(summary), Is.EqualTo(summary), "One word in the log: nothing in it changes on the way.");
            Assert.That(Gamesim.Episode.SettingsEvidence.SessionLine(new[] { new KeyValuePair<string, string>("keys", summary) }),
                Is.EqualTo("Gamesim settings: keys=" + summary));

            // Read back: the very choices.
            var read = KeyBindings.ParseSummary(summary, out var reason);
            Assert.That(reason, Is.Null);
            Assert.That(read.Overrides, Is.EqualTo(bindings.Overrides), "The log reads back as the choices were made.");
            Assert.That(KeyBindings.ParseSummary("default", out reason).Count, Is.Zero);
            Assert.That(reason, Is.Null);
            // Every target, on each device it takes, round trips.
            var each = new KeyBindings();
            foreach (var target in KeyBindings.Targets)
            {
                each.Set(target, KeyBindings.KeyboardScheme, "<Keyboard>/numpad7");
                if (target.OnPad) each.Set(target, KeyBindings.GamepadScheme, "<Gamepad>/dpad/down");
            }
            Assert.That(KeyBindings.ParseSummary(each.Summary(), out reason).Overrides, Is.EqualTo(each.Overrides), each.Summary());
            Assert.That(reason, Is.Null);
            // What a player could not have chosen is dropped, with the reason, as a kept text's is.
            var some = KeyBindings.ParseSummary("Notebook:escape,Nobody:k,Notebook:k,Hold_grip:k,Pan_up:j(pad),Diary:", out reason);
            Assert.That(some.Count, Is.EqualTo(1));
            Assert.That(some.Get("Shortcuts", "Notebook", KeyBindings.KeyboardScheme), Is.EqualTo("<Keyboard>/k"));
            Assert.That(reason, Does.Contain("not one a player may choose"));
        }
    }
}

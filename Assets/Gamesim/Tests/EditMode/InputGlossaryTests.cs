using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The input glossary on its own (PLAN A, A0): one row per thing the player presses, each with
    /// somewhere it works, a way to press it and words for what it does, and the binding words the
    /// controls page and the hints are written in. Unity-free; the editor holds the rows to the
    /// actions map itself in <see cref="InputGlossaryActionsTests"/>.
    /// </summary>
    public sealed class InputGlossaryTests
    {
        private static readonly string[] Contexts =
        {
            InputGlossary.CameraContext, InputGlossary.HouseContext, InputGlossary.PanelsContext, InputGlossary.CeremonyContext,
            InputGlossary.CompetitionContext, InputGlossary.ReportContext, InputGlossary.PrototypeContext,
        };

        [Test]
        public void EveryRow_SaysWhereItWorksHowToPressItAndWhatItDoes()
        {
            Assert.That(InputGlossary.Rows, Is.Not.Empty);
            foreach (var row in InputGlossary.Rows)
            {
                Assert.That(Contexts, Does.Contain(row.Context), row + ": a context the controls page knows.");
                Assert.That(row.Map, Is.Not.Empty, row.ToString());
                Assert.That(row.Action, Is.Not.Empty, row.ToString());
                Assert.That(row.Hint, Is.Not.Empty, row + " says what it does.");
                Assert.That(row.Keyboard.Length + row.Pad.Length, Is.GreaterThan(0), row + " can be pressed some way.");
            }
        }

        [Test]
        public void EachAction_HasOneRow()
        {
            var duplicates = InputGlossary.Rows.GroupBy(row => row.ToString()).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
            Assert.That(duplicates, Is.Empty, "One row per action.");
            Assert.That(InputGlossary.Find("Shortcuts", "Menu"), Is.Not.Null);
            Assert.That(InputGlossary.Find("Shortcuts", "Nothing"), Is.Null);
        }

        [Test]
        public void ThePointerAndTyping_AreTheOnlyRawReadsAndHaveNoPadPath()
        {
            // A pad never reaches a raw read: anything the pad does goes through the actions map.
            foreach (var row in InputGlossary.Rows.Where(row => row.Map == InputGlossary.PointerMap || row.Map == InputGlossary.TypingMap))
                Assert.That(row.Pad, Is.Empty, row + " is a direct read, so it has no pad binding to describe.");
            Assert.That(InputGlossary.Rows.Where(row => row.OutsideTheAsset).Select(row => row.Map).Distinct(),
                Is.SubsetOf(new[] { InputGlossary.InterfaceMap, InputGlossary.PointerMap, InputGlossary.TypingMap }));
        }

        [TestCase("<Keyboard>/escape", "Esc")]
        [TestCase("<Keyboard>/f5", "F5")]
        [TestCase("<Keyboard>/j", "J")]
        [TestCase("<Keyboard>/rightBracket", "]")]
        [TestCase("<Keyboard>/numpadEnter", "Num Enter")]
        [TestCase("<Keyboard>/numpad1", "Num 1")]
        [TestCase("<Keyboard>/1", "1")]
        [TestCase("<Keyboard>/pageDown", "Page Down")]
        [TestCase("<Mouse>/leftButton", "Left click")]
        [TestCase("<Mouse>/scroll/y", "Wheel")]
        [TestCase("<Pointer>/position", "Pointer")]
        [TestCase("<Gamepad>/buttonSouth", "A")]
        [TestCase("<Gamepad>/buttonEast", "B")]
        [TestCase("<Gamepad>/buttonWest", "X")]
        [TestCase("<Gamepad>/buttonNorth", "Y")]
        [TestCase("<Gamepad>/leftStickPress", "L3")]
        [TestCase("<Gamepad>/dpad/up", "D-pad up")]
        [TestCase("<Gamepad>/rightTrigger", "RT")]
        public void ABinding_IsWordedTheWayAPlayerSaysIt(string path, string words)
        {
            Assert.That(InputGlossary.BindingLabel(path), Is.EqualTo(words));
        }

        [Test]
        public void AComposite_IsOnePhrase()
        {
            Assert.That(InputGlossary.CompositeLabel("2DVector", Parts(("Up", "<Keyboard>/w"), ("Down", "<Keyboard>/s"), ("Left", "<Keyboard>/a"), ("Right", "<Keyboard>/d"))),
                Is.EqualTo("WASD"));
            Assert.That(InputGlossary.CompositeLabel("2DVector", Parts(("Up", "<Keyboard>/upArrow"), ("Down", "<Keyboard>/downArrow"),
                ("Left", "<Keyboard>/leftArrow"), ("Right", "<Keyboard>/rightArrow"))), Is.EqualTo("Arrows"));
            Assert.That(InputGlossary.CompositeLabel("OneModifier", Parts(("Modifier", "<Mouse>/rightButton"), ("Binding", "<Mouse>/delta"))),
                Is.EqualTo("Right-drag"));
            Assert.That(InputGlossary.CompositeLabel("OneModifier", Parts(("Modifier", "<Keyboard>/shift"), ("Binding", "<Keyboard>/tab"))),
                Is.EqualTo("Shift+Tab"));
            Assert.That(InputGlossary.CompositeLabel("1DAxis", Parts(("Negative", "<Gamepad>/leftTrigger"), ("Positive", "<Gamepad>/rightTrigger"))),
                Is.EqualTo("LT / RT"));
            Assert.That(InputGlossary.Join(new[] { "Esc", "", "Esc", "B" }), Is.EqualTo("Esc / B"), "Each word once, empties dropped.");
        }

        [Test]
        public void TheHints_KeepTheKeyboardsWordsAndNameThePadsButtons()
        {
            // The keyboard's words are what they always were (A4 changes nothing for a keyboard player).
            Assert.That(InputGlossary.HelpCard(false), Is.EqualTo(InputGlossary.KeyboardHelpCard));
            Assert.That(InputGlossary.TourLine(false), Is.EqualTo(InputGlossary.KeyboardTourLine));
            Assert.That(InputGlossary.KeyboardHelpCard, Does.StartWith("Click a houseguest: talk\n").And.EndWith("Esc: close"));

            string card = InputGlossary.HelpCard(true);
            Assert.That(card.Split('\n'), Has.Length.EqualTo(5), "The pad's card fills the same five lines.");
            foreach (var line in card.Split('\n'))
                Assert.That(line.Length, Is.LessThanOrEqualTo(33), "'" + line + "' fits the card's box as the keyboard's longest line does.");
            // Each button named is the one its row says, so a rebinding cannot leave the card behind.
            foreach (var (map, action) in new[] { ("Shortcuts", "Interact"), ("Shortcuts", "Diary"), ("Camera", "Pan"), ("Camera", "Recenter"),
                ("Camera", "OrbitRate"), ("Camera", "Next"), ("Camera", "Previous"), ("Shortcuts", "Overview"), ("Shortcuts", "Back"), ("Shortcuts", "Menu") })
                Assert.That(card, Does.Contain(InputGlossary.Find(map, action).Pad.Replace(" / ", "/")), map + "/" + action);
            Assert.That(card, Does.Not.Contain("Click").And.Not.Contain("Esc"), "Nothing a pad cannot press.");

            string tour = InputGlossary.TourLine(true);
            Assert.That(tour, Does.Contain("Press " + InputGlossary.Find("Shortcuts", "Interact").Pad + " near a houseguest"));
            Assert.That(tour, Does.Contain(InputGlossary.Find("Shortcuts", "Back").Pad + " closes this tour"));
            Assert.That(tour, Does.Not.Contain("Click").And.Not.Contain("Esc"));
        }

        /// <summary>
        /// The status lines, the settings' CEREMONIES paragraph, the tour's episode-screen step and
        /// the house challenge's paragraph (A4f): on the keyboard each is the literal it always was,
        /// pinned here byte for byte; on a pad each names its row's pad button and none of the keys.
        /// </summary>
        [Test]
        public void TheStatusLines_KeepTheKeyboardsWordsAndNameThePadsButtons()
        {
            Assert.That(InputGlossary.StationLine(true, false), Is.EqualTo("At the episode screen  ·  E to open"));
            Assert.That(InputGlossary.StationLine(false, false), Is.EqualTo("Heading to the episode screen  ·  E to open"));
            Assert.That(InputGlossary.DiaryWayLine(true, false),
                Is.EqualTo("At the private room: press E to open your diary. No choice is committed by entering."));
            Assert.That(InputGlossary.DiaryWayLine(false, false),
                Is.EqualTo("Walk to the private room, then press E to open your diary. No choice is committed by entering."));
            Assert.That(InputGlossary.CeremonyPaceLine(true, false), Is.EqualTo(
                "Keys and votes are revealed one at a time, with a pause before the last. Press Space to speed a reveal up, or Enter to skip to the result."));
            Assert.That(InputGlossary.CeremonyPaceLine(false, false),
                Is.EqualTo("Keys and votes are revealed quickly. Press Space to speed a reveal up, or Enter to skip to the result."));
            Assert.That(InputGlossary.StationStepKeys(false), Is.EqualTo("press E to open it and Esc to close it."));
            Assert.That(InputGlossary.ChallengeLine(false), Is.EqualTo(
                "Press Space or STOP when the marker is near the center. Three attempts; no time limit. Escape cancels without committing."));
            Assert.That(InputGlossary.ActivityLine("Sleep", false), Is.EqualTo("Off to bed  ·  E or a click to get up"));
            Assert.That(InputGlossary.ActivityLine("Swim", false), Is.EqualTo("Going for a swim  ·  E or a click to get out"));
            Assert.That(InputGlossary.ActivityLine("Soak", false), Is.EqualTo("Into the hot tub  ·  E or a click to get out"));
            Assert.That(InputGlossary.ActivityLine("Cook", false), Is.EqualTo("Cooking a meal  ·  E or a click to stop"));
            Assert.That(InputGlossary.ActivityLine("Dance", false), Is.EqualTo("Dancing  ·  E or a click to stop"));
            Assert.That(InputGlossary.ActivityLine("Rest", false), Is.EqualTo("At the furniture  ·  E to finish"));
            Assert.That(InputGlossary.ActivityLine("PrepareSnack", false), Is.EqualTo("At the furniture  ·  E to finish"));

            string interact = InputGlossary.Find("Shortcuts", "Interact").Pad, back = InputGlossary.Find("Shortcuts", "Back").Pad;
            string hit = InputGlossary.Find("Shortcuts", "Hit").Pad, speed = InputGlossary.Find("Ceremony", "Speed").Pad;
            string skip = InputGlossary.Find("Ceremony", "Skip").Pad.Split(new[] { " / " }, System.StringSplitOptions.None)[0];
            Assert.That(new[] { interact, back, hit, speed, skip }, Is.EqualTo(new[] { "X", "B", "A", "X", "A" }), "The rows' pad buttons.");
            var padLines = new Dictionary<string, string>
            {
                { "station, warped", InputGlossary.StationLine(true, true) },
                { "station, walking", InputGlossary.StationLine(false, true) },
                { "diary, warped", InputGlossary.DiaryWayLine(true, true) },
                { "diary, walking", InputGlossary.DiaryWayLine(false, true) },
                { "ceremonies, suspenseful", InputGlossary.CeremonyPaceLine(true, true) },
                { "ceremonies, quick", InputGlossary.CeremonyPaceLine(false, true) },
                { "tour step", InputGlossary.StationStepKeys(true) },
                { "challenge", InputGlossary.ChallengeLine(true) },
            };
            foreach (var kind in new[] { "Sleep", "Swim", "Soak", "Cook", "Dance", "Rest" }) padLines.Add("furniture " + kind, InputGlossary.ActivityLine(kind, true));
            foreach (var line in padLines)
                foreach (var key in new[] { "E ", "Esc", "Space", "Enter", "click" })
                    Assert.That(line.Value, Does.Not.Contain(key), line.Key + " names " + key + " on a pad: " + line.Value);

            Assert.That(InputGlossary.StationLine(true, true), Is.EqualTo("At the episode screen  ·  " + interact + " to open"));
            Assert.That(InputGlossary.StationLine(false, true), Is.EqualTo("Heading to the episode screen  ·  " + interact + " to open"));
            Assert.That(InputGlossary.DiaryWayLine(true, true), Does.Contain("press " + interact + " to open your diary"));
            Assert.That(InputGlossary.DiaryWayLine(false, true), Does.StartWith("Walk to the private room, then press " + interact + " "));
            Assert.That(InputGlossary.CeremonyPaceLine(true, true), Does.EndWith("Press " + speed + " to speed a reveal up, or " + skip + " to skip to the result."));
            Assert.That(InputGlossary.CeremonyPaceLine(false, true), Does.StartWith("Keys and votes are revealed quickly. Press " + speed + " "));
            Assert.That(InputGlossary.StationStepKeys(true), Is.EqualTo("press " + interact + " to open it and " + back + " to close it."));
            Assert.That(InputGlossary.ChallengeLine(true), Does.StartWith("Press " + hit + " when the marker").And.Contain(back + " cancels without committing."));
            Assert.That(InputGlossary.ActivityLine("Sleep", true), Is.EqualTo("Off to bed  ·  " + interact + " to get up"));
            Assert.That(InputGlossary.ActivityLine("Rest", true), Is.EqualTo("At the furniture  ·  " + interact + " to finish"));
        }

        [Test]
        public void ThePrompt_NamesTheKeyOrTheButton()
        {
            Assert.That(InputGlossary.PromptKey(false), Is.EqualTo("E"));
            Assert.That(InputGlossary.PromptKey(true), Is.EqualTo("X"));
            Assert.That(InputGlossary.PromptFor("E  ·  Talk to Maya", false), Is.EqualTo("E  ·  Talk to Maya"), "The keyboard's prompt is unchanged.");
            Assert.That(InputGlossary.PromptFor("E  ·  Talk to Maya", true), Is.EqualTo("X  ·  Talk to Maya"));
            Assert.That(InputGlossary.PromptFor("E  ·  Get out of the pool", true), Is.EqualTo("X  ·  Get out of the pool"));
            Assert.That(InputGlossary.PromptFor("", true), Is.EqualTo(""));
            Assert.That(InputGlossary.PromptFor(null, true), Is.Null);
            Assert.That(InputGlossary.PromptFor("Elena is here", true), Is.EqualTo("Elena is here"), "Only the key at the front is the key.");
        }

        [Test]
        public void CaptionsThatNameAKey_HaveAnActionWithAPadButton()
        {
            foreach (var pair in InputGlossary.CaptionKeys)
            {
                var parts = pair.Value.Split('/');
                var row = InputGlossary.Find(parts[0], parts[1]);
                Assert.That(row, Is.Not.Null, pair.Key);
                Assert.That(row.Pad, Is.Not.Empty, pair.Key + " has a pad button to stand beside it.");
            }
            Assert.That(InputGlossary.CaptionAction("Close  [Esc]"), Is.EqualTo("Shortcuts/Back"));
            Assert.That(InputGlossary.CaptionAction("Save now  [F5]"), Is.EqualTo("Shortcuts/Save"));
            Assert.That(InputGlossary.CaptionAction("Notebook [J]"), Is.EqualTo("Shortcuts/Notebook"));
            Assert.That(InputGlossary.CaptionAction("Go to diary room [R]"), Is.EqualTo("Shortcuts/Diary"));
            Assert.That(InputGlossary.CaptionAction("STOP marker  [Space]"), Is.EqualTo("Shortcuts/Hit"));
            Assert.That(InputGlossary.CaptionAction("Hit the target  [Space]"), Is.Null,
                "A competition's Hit has no pad button: no A beside a caption that names Space there.");
            Assert.That(InputGlossary.CaptionAction("Interact [E]"), Is.Null, "The prompt names the pad's X in its own words, so no chip says it again.");
            Assert.That(InputGlossary.CaptionAction("Promise safety"), Is.Null);
            Assert.That(InputGlossary.CaptionAction(null), Is.Null);
        }

        private static List<KeyValuePair<string, string>> Parts(params (string Name, string Path)[] parts) =>
            parts.Select(part => new KeyValuePair<string, string>(part.Name, part.Path)).ToList();
    }
}

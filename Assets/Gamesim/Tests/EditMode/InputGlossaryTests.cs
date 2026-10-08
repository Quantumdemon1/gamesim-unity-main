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

        private static List<KeyValuePair<string, string>> Parts(params (string Name, string Path)[] parts) =>
            parts.Select(part => new KeyValuePair<string, string>(part.Name, part.Path)).ToList();
    }
}

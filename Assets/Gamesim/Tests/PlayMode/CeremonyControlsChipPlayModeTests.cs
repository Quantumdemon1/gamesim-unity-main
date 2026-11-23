using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gamesim.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Every ceremony card a press moves on closes with the same controls chip (UI-UX plan D3b): the
    /// takeover, the key ceremony, the live eviction, the jury's vote and the veto draw each show one
    /// chip, centred at the card's foot, its words at 13 points in a box 1.3 times them and more, the
    /// chip as tall on every card and wide enough for its words - in the keyboard's words, and in the
    /// pad's once the pad is pressed. The chip's numbers are pinned without Unity
    /// (CeremonyCardClockTests); this is what only the built cards show.
    /// </summary>
    public sealed class CeremonyControlsChipPlayModeTests
    {
        private const string Jordan = "a", Casey = "b";

        private GameObject owner;
        private readonly List<GameObject> cards = new List<GameObject>();
        private Keyboard keyboard;
        private Gamepad pad;

        [SetUp]
        public void CreateFixture()
        {
            owner = new GameObject("Ceremony controls chip owner");
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            foreach (var card in cards) if (card != null) Object.Destroy(card);
            cards.Clear();
            if (owner != null) Object.Destroy(owner);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            keyboard = null;
            pad = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryCeremonyCard_ClosesWithTheSameReadableControlsChip()
        {
            var takeover = CeremonyTakeover.Attach(owner);
            cards.Add(takeover.gameObject);
            takeover.Play(CeremonySting.EvictionKind, 3, new[] { new CeremonyTakeover.Subject("Casey Wilson", "EVICTED", null) }, true);

            var keys = KeyCeremony.Attach(owner);
            cards.Add(keys.gameObject);
            var safe = new[] { new KeyCeremony.Person("s0", "Emma Brown", null), new KeyCeremony.Person("s1", "Riley Johnson", null) };
            var block = new[] { new KeyCeremony.Person(Jordan, "Jordan Taylor", null), new KeyCeremony.Person(Casey, "Casey Wilson", null) };
            Assert.That(keys.Play(1, "Maya Hassan", false, safe, block, true), Is.True);

            var votes = VoteReveal.Attach(owner);
            cards.Add(votes.gameObject);
            var nominees = new[] { new VoteReveal.Nominee(Jordan, "Jordan Taylor", null), new VoteReveal.Nominee(Casey, "Casey Wilson", null) };
            Assert.That(votes.Play(2, nominees, new[] { new VoteReveal.Ballot(Casey), new VoteReveal.Ballot(Casey), new VoteReveal.Ballot(Jordan) },
                Casey, true), Is.True);

            var jury = JuryReveal.Attach(owner);
            cards.Add(jury.gameObject);
            var pair = new[] { new JuryReveal.Finalist(Jordan, "Jordan Taylor", null), new JuryReveal.Finalist(Casey, "Casey Wilson", null) };
            var jurors = new[] { new JuryReveal.Juror("j0", "Emma Brown", Jordan), new JuryReveal.Juror("j1", "Riley Johnson", Casey), new JuryReveal.Juror("j2", "Noah Kim", Jordan) };
            Assert.That(jury.Play(pair, jurors, Jordan, true), Is.True);

            var draw = VetoDrawReveal.Attach(owner);
            cards.Add(draw.gameObject);
            var byRight = new[]
            {
                new VetoDrawReveal.Player("h", "Maya Hassan", "HOH", null),
                new VetoDrawReveal.Player(Jordan, "Jordan Taylor", "NOM", null),
                new VetoDrawReveal.Player(Casey, "Casey Wilson", "NOM", null),
            };
            var drawn = new[] { new VetoDrawReveal.Player("d0", "Noah Kim", "DRAWN", null) };
            Assert.That(draw.Play(4, byRight, drawn, "Three play by right; the draw added one.", false), Is.True);

            var lines = new (Component card, string label, string words)[]
            {
                (takeover, "Takeover dismiss", CeremonyTakeover.CloseCaption),
                (keys, "Controls", CeremonyTakeover.ControlsCaption),
                (votes, "Controls", CeremonyTakeover.ControlsCaption),
                (jury, "Controls", CeremonyTakeover.ControlsCaption),
                (draw, "Draw dismiss", CeremonyTakeover.SkipToResultCaption),
            };
            yield return null;
            AssertOneChipEach(lines, "On a keyboard");

            // The pad's X moves no card on; it is the press that says the pad is in hand.
            yield return PastTheReadDelay();
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.West));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
            AssertOneChipEach(new[]
            {
                (lines[0].card, lines[0].label, CeremonyTakeover.PadCloseCaption),
                (lines[1].card, lines[1].label, CeremonyTakeover.PadControlsCaption),
                (lines[2].card, lines[2].label, CeremonyTakeover.PadControlsCaption),
                (lines[3].card, lines[3].label, CeremonyTakeover.PadControlsCaption),
                // The draw's field comes at 1.9 seconds; a slow frame can bring it before the press.
                (lines[4].card, lines[4].label, draw.ShowingField ? CeremonyTakeover.PadCloseCaption : CeremonyTakeover.PadSkipToResultCaption),
            }, "On a pad");
        }

        /// <summary>
        /// Each card has one controls chip, up, centred under its card, holding the line named, in the
        /// words given, at 13 points in a box at least 1.3 times them, wide enough for them; and the
        /// chips are one height.
        /// </summary>
        private static void AssertOneChipEach((Component card, string label, string words)[] lines, string where)
        {
            Canvas.ForceUpdateCanvases();
            var heights = new List<float>();
            foreach (var (card, name, words) in lines)
            {
                string who = where + ", " + card.GetType().Name;
                var chips = card.GetComponentsInChildren<RectTransform>(true).Where(rect => rect.name == CeremonyControlsChip.ChipName).ToArray();
                Assert.That(chips, Has.Length.EqualTo(1), who + " closes with one controls chip.");
                var chip = chips[0];
                Assert.That(chip.gameObject.activeInHierarchy, Is.True, who + ": the chip is up.");
                Assert.That(chip.anchorMin.x, Is.EqualTo(.5f), who + ": the chip hangs from the middle of the card,");
                Assert.That(chip.anchoredPosition.x, Is.EqualTo(0f), who + ": centred under it.");

                var label = chip.GetComponentsInChildren<TMP_Text>(true).Single();
                Assert.That(label.name, Is.EqualTo(name), who + ": the chip holds the line tests and screen readers know.");
                Assert.That(label.text, Is.EqualTo(words), who + ": in the device's words.");
                Assert.That(label.fontSize, Is.EqualTo(CeremonyChipMetrics.HudPt).Within(1e-3f), who + ": at 13 points,");
                Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(1.3f * label.fontSize - 1e-3f),
                    who + ": in a box 1.3 times its type, or Inter draws nothing.");
                label.ForceMeshUpdate(true);
                Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(glyph => glyph.isVisible), Is.True,
                    who + ": its words are drawn.");
                Assert.That(chip.rect.width, Is.GreaterThanOrEqualTo(label.preferredWidth - 0.5f),
                    who + ": the chip is wide enough for its words (" + chip.rect.width.ToString("0") + " for " + label.preferredWidth.ToString("0") + ").");
                heights.Add(chip.rect.height);
            }
            Assert.That(heights.Distinct().Count(), Is.EqualTo(1), where + ": every card's chip is one height: " + string.Join(", ", heights));
            Assert.That(heights[0], Is.EqualTo(CeremonyChipMetrics.HudHeight(1f)).Within(0.5f), where + ": the chip's own height.");
        }

        /// <summary>Past every card's fade and read delay, on the real clock the cards keep.</summary>
        private static IEnumerator PastTheReadDelay()
        {
            yield return null;
            double from = Time.unscaledTimeAsDouble;
            while (Time.unscaledTimeAsDouble - from < CeremonyPacing.FadeIn + 0.5f) yield return null;
        }
    }
}

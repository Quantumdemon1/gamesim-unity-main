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
    /// The built ceremony cards name the press that works in the device's words, hold a long line long
    /// enough to read, and start at the speed they are handed (UI-UX plan U5 and D2). The words, the
    /// holds and the director's handing-on are pinned without Unity (CeremonyCardPressTests); this is
    /// what only a built card shows: the label on it, and the clock it runs.
    /// </summary>
    public sealed class CeremonyCardPressPlayModeTests
    {
        private const string Jordan = "a", Casey = "b";

        private GameObject owner;
        private readonly List<GameObject> cards = new List<GameObject>();
        private Keyboard keyboard;
        private Gamepad pad;

        [SetUp]
        public void CreateFixture()
        {
            owner = new GameObject("Ceremony card press owner");
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
        public IEnumerator TheCardsNameThePressThatWorksInTheDevicesWords()
        {
            var takeover = CeremonyTakeover.Attach(owner);
            cards.Add(takeover.gameObject);
            takeover.Play(CeremonySting.EvictionKind, 3, new[] { new CeremonyTakeover.Subject("Casey Wilson", "EVICTED", null) }, true);
            var keys = Keys();
            var draw = Draw(1f);
            Assert.That(Text(takeover, "Takeover dismiss").text, Is.EqualTo(CeremonyTakeover.CloseCaption),
                "The card says a click or Enter closes it, not 'click anywhere to continue'.");
            Assert.That(Text(keys, "Dismiss").gameObject.activeSelf, Is.True, "On a keyboard the key ceremony keeps its click line,");
            Assert.That(Text(draw, "Draw dismiss").text, Is.EqualTo(CeremonyTakeover.SkipToResultCaption), "and the draw names its skip.");

            yield return PastTheReadDelay();
            // The pad's X speeds the reveals up and moves no card on: the press that says the pad is in hand.
            yield return Press(GamepadButton.West);
            Assert.That(takeover.IsPlaying && keys.IsPlaying && draw.IsPlaying, Is.True, "X moves no card on.");
            Assert.That(Text(takeover, "Takeover dismiss").text, Is.EqualTo(CeremonyTakeover.PadCloseCaption), "On a pad the card names A,");
            Assert.That(Text(keys, "Dismiss").gameObject.activeSelf, Is.False, "the key ceremony drops its click line,");
            Assert.That(Text(keys, "Controls").text, Is.EqualTo(CeremonyTakeover.PadControlsCaption), "beside its pad controls,");
            Assert.That(Text(draw, "Draw dismiss").text, Is.EqualTo(CeremonyTakeover.PadSkipToResultCaption), "and the draw names A too.");

            yield return Press(Key.Enter);
            Assert.That(takeover.IsPlaying, Is.False, "Enter closes the card it named,");
            Assert.That(draw.ShowingField, Is.True, "and takes the draw to its field,");
            Assert.That(Text(draw, "Draw dismiss").text, Is.EqualTo(CeremonyTakeover.CloseCaption),
                "where the next press closes it, and the line says so in the keyboard's words.");
        }

        [UnityTest]
        public IEnumerator ALongLineHoldsAndASpedUpSessionStartsFast()
        {
            const string line = "Casey Wilson used the Power of Veto on Jordan Taylor. Riley Johnson goes up in their place.";
            var takeover = CeremonyTakeover.Attach(owner);
            cards.Add(takeover.gameObject);
            takeover.Play(CeremonySting.VetoKind, 3, new[] { new CeremonyTakeover.Subject("Casey Wilson", "VETO", null) }, true, null, line);
            float expected = CeremonyPacing.CardHold(CeremonyTakeover.TitleFor(CeremonySting.VetoKind), line, CaptionTime.Standard);
            Assert.That(takeover.HoldSeconds, Is.EqualTo(expected).Within(1e-4f), "The card holds for its line's reading time,");
            Assert.That(takeover.HoldSeconds, Is.GreaterThan(CeremonyPacing.CardAuthoredHold + 1f), "well past the 2.6 seconds it used to give any line.");
            takeover.CaptionTime = CaptionTime.Longer;
            takeover.Play(CeremonySting.VetoKind, 3, new[] { new CeremonyTakeover.Subject("Casey Wilson", "VETO", null) }, true, null, line);
            Assert.That(takeover.HoldSeconds, Is.EqualTo(expected * 1.5f).Within(1e-3f), "Longer captions hold it half as long again.");
            takeover.Cancel();

            var keys = Keys(CeremonyPacing.SpeedUp);
            var draw = Draw(CeremonyPacing.SpeedUp);
            Assert.That(keys.SpeedMultiplier, Is.EqualTo(CeremonyPacing.SpeedUp), "A reveal handed the session's speed-up starts fast,");
            Assert.That(Text(keys, "Speed").gameObject.activeSelf, Is.True, "and says so in its corner;");
            Assert.That(draw.SpeedMultiplier, Is.EqualTo(CeremonyPacing.SpeedUp), "so does the veto draw.");

            var told = new List<float>();
            keys.SpeedChanged += told.Add;
            yield return PastTheReadDelay();
            yield return Press(Key.Space);
            Assert.That(keys.SpeedMultiplier, Is.EqualTo(1f), "Space puts a fast reveal back at its own pace,");
            Assert.That(told, Is.EqualTo(new[] { 1f }), "and tells whoever keeps the session's speed.");
            yield return Press(Key.Space);
            Assert.That(told, Is.EqualTo(new[] { 1f, CeremonyPacing.SpeedUp }), "Space again speeds it up and says so.");

            keys.Cancel();
            keys = Keys();
            Assert.That(keys.SpeedMultiplier, Is.EqualTo(1f), "A caller that hands no speed gets the reveal's own pace.");
        }

        // ------------------------------------------------------------------ helpers

        private KeyCeremony Keys(float speed = 1f)
        {
            var keys = KeyCeremony.Attach(owner);
            cards.Add(keys.gameObject);
            var safe = new[] { new KeyCeremony.Person("s0", "Emma Brown", null), new KeyCeremony.Person("s1", "Riley Johnson", null) };
            var block = new[] { new KeyCeremony.Person(Jordan, "Jordan Taylor", null), new KeyCeremony.Person(Casey, "Casey Wilson", null) };
            Assert.That(keys.Play(1, "Maya Hassan", false, safe, block, true, CeremonyPace.Suspenseful, null, null, speed), Is.True);
            return keys;
        }

        private VetoDrawReveal Draw(float speed)
        {
            var draw = VetoDrawReveal.Attach(owner);
            cards.Add(draw.gameObject);
            var byRight = new[]
            {
                new VetoDrawReveal.Player("h", "Maya Hassan", "HOH", null),
                new VetoDrawReveal.Player(Jordan, "Jordan Taylor", "NOM", null),
                new VetoDrawReveal.Player(Casey, "Casey Wilson", "NOM", null),
            };
            var drawn = new[] { new VetoDrawReveal.Player("d0", "Noah Kim", "DRAWN", null), new VetoDrawReveal.Player("d1", "Ava Lopez", "DRAWN", null) };
            // Not reduced: a reduced draw shows its field at once.
            Assert.That(draw.Play(4, byRight, drawn, "Three play by right; the draw added two.", false, CeremonyPace.Suspenseful, speed), Is.True);
            return draw;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private IEnumerator Press(GamepadButton button)
        {
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        /// <summary>Past every card's fade and read delay, on the real clock the cards keep.</summary>
        private static IEnumerator PastTheReadDelay()
        {
            yield return null;
            double from = Time.unscaledTimeAsDouble;
            while (Time.unscaledTimeAsDouble - from < CeremonyPacing.FadeIn + 0.5f) yield return null;
        }

        private static TMP_Text Text(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(label => label.name == name);
    }
}

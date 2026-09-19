using System.Collections;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Controller navigation (MASTER-PLAN §3.D): the house's shortcuts have a gamepad button beside
    /// each key, read through the actions map, so a pad opens and closes the same panels the
    /// keyboard does - and the keys still do.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Controller_StartAndSelectOpenAndCloseThePanelsTheKeysDo()
        {
            director.ClosePanels();
            yield return null;
            var pad = TestGamepad();

            yield return PressPad(pad, GamepadButton.Start);
            Assert.That(director.IsPanelOpen, Is.True, "Start with nothing open opens the settings: a pad has no other way there.");
            yield return PressPad(pad, GamepadButton.Start);
            Assert.That(director.IsPanelOpen, Is.False, "Start again closes it, as Escape does.");
            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.False, "Escape with nothing open still does nothing: the keyboard has the HUD's buttons.");

            yield return PressPad(pad, GamepadButton.Select);
            Assert.That(director.IsPanelOpen, Is.True, "Select opens the notebook.");
            Assert.That(ButtonWithCaption("Close  [Esc]"), Is.Not.Null, "with its close control");
            yield return PressPad(pad, GamepadButton.Start);
            Assert.That(director.IsPanelOpen, Is.False);

            // The keys are the same actions, so they keep working beside the pad.
            yield return PressKey(Key.J);
            Assert.That(director.IsPanelOpen, Is.True, "J still opens the notebook.");
            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.False, "Escape still closes.");
        }

        private IEnumerator PressPad(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }
    }
}

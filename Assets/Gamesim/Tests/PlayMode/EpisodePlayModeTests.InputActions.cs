using System.Collections;
using Gamesim.House;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// One actions asset (PLAN A, A1): everything that is not the rig or the director reads the
    /// game's actions through <see cref="HouseInput"/>, which hands out the rig's while a rig is up,
    /// and the emote card has a pad button beside its key.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Input_TheHouseReadsTheRigsActions_AndASharedCopyWithoutIt()
        {
            Assert.That(HouseInput.Actions, Is.SameAs(cameraRig.Actions),
                "With the rig up, the cards, the boards and the HUD's ring read the rig's own actions: one asset.");
            cameraRig.enabled = false;
            yield return null;
            var shared = HouseInput.Actions;
            Assert.That(shared, Is.Not.SameAs(cameraRig.Actions), "A disabled rig's actions are off; the house reads the shared copy.");
            Assert.That(shared.Skip.enabled && shared.CompetitionBack.enabled && shared.ReportScroll.enabled, Is.True,
                "and the shared copy is listening.");
            cameraRig.enabled = true;
            yield return null;
            Assert.That(HouseInput.Actions, Is.SameAs(cameraRig.Actions), "The rig enabled again is what the house reads.");
        }

        [UnityTest]
        public IEnumerator Input_GAndTheDpadRightOpenAndCloseYourMoves()
        {
            director.ClosePanels();
            yield return null;
            Assert.That(director.EmoteMenuOpen, Is.False, "Nothing open to begin with.");
            yield return PressKey(Key.G);
            yield return null;
            Assert.That(director.EmoteMenuOpen, Is.True, "G opens your moves.");
            yield return PressKey(Key.G);
            yield return null;
            Assert.That(director.EmoteMenuOpen, Is.False, "and closes them.");

            var pad = TestGamepad();
            yield return PressPad(pad, GamepadButton.DpadRight);
            yield return null;
            Assert.That(director.EmoteMenuOpen, Is.True, "The d-pad's right is the same action: a pad opens your moves.");
            Assert.That(director.IsPanelOpen, Is.False, "The card is not a panel.");
            yield return PressPad(pad, GamepadButton.DpadRight);
            yield return null;
            Assert.That(director.EmoteMenuOpen, Is.False, "and closes them.");
        }
    }
}

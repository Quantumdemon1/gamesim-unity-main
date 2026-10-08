using System.Collections;
using System.Linq;
using Gamesim.Episode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The pause menu (PLAN A, A3, the owner's decision 1): Escape with nothing open opens the
    /// settings, as the pad's Start does, and the house's clock holds under them - the settings say
    /// PAUSED while it does - until Escape closes them again.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Pause_EscapeHoldsTheHouseClockUntilClosed()
        {
            director.ClosePanels();
            yield return null;
            // The house has to be running for a hold to mean anything: free roam, the world up.
            float deadline = Time.realtimeSinceStartup + 15f;
            while (director.IsHouseClockHeld && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.IsHouseClockHeld, Is.False, "In free roam the house's clock runs.");
            Assert.That(PausedEyebrowShown(), Is.False, "Nothing says PAUSED out in the house.");

            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.True, "Escape with nothing open is the pause menu.");
            Assert.That(director.IsHouseClockHeld, Is.True, "The pause menu holds the house.");
            Assert.That(PausedEyebrowShown(), Is.True, "and the settings say PAUSED.");
            // Read once the panel is open: opening it flushes the whole ticks the house had earned.
            long held = director.Snapshot.npcSocial.clockTick;
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Assert.That(director.Snapshot.npcSocial.clockTick, Is.EqualTo(held),
                    "Three seconds of a running house are three ticks; under the pause menu there are none.");
                Assert.That(director.IsPanelOpen, Is.True);
            }

            yield return PressKey(Key.Escape);
            Assert.That(director.IsPanelOpen, Is.False, "Escape again closes the pause menu.");
            Assert.That(PausedEyebrowShown(), Is.False);
            deadline = Time.realtimeSinceStartup + 10f;
            while (director.Snapshot.npcSocial.clockTick == held && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(director.Snapshot.npcSocial.clockTick, Is.GreaterThan(held), "Closed, the house's clock runs again.");
        }

        /// <summary>Whether the settings' PAUSED eyebrow is on screen.</summary>
        private bool PausedEyebrowShown() => director.GetComponentsInChildren<TMPro.TMP_Text>()
            .Any(text => text.isActiveAndEnabled && text.text == EpisodeDirector.PausedEyebrow);
    }
}

using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The pause menu (PLAN A, A3, the owner's decision 1): Escape with nothing open opens the
    /// settings, as the pad's Start does, and the house's clock holds under them - the settings say
    /// PAUSED while it does - until Escape closes them again. The settings the main menu opens are
    /// the front door's, not a pause: they do not say PAUSED.
    ///
    /// <para>The house's clock runs on unscaled time, which <c>Time.captureDeltaTime</c> does not
    /// pin, so the waits here are bounded in real seconds; the pin holds everything else the frames
    /// move on game time. What closing the menu is asserted on is the hold itself, not the next
    /// tick: a tick also waits on the house's world being ready for it, which a test cannot time.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator Pause_EscapeHoldsTheHouseClockUntilClosed()
        {
            Time.captureDeltaTime = 1f / 30f;
            try
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
                deadline = Time.realtimeSinceStartup + 5f;
                while (director.IsHouseClockHeld && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(director.IsHouseClockHeld, Is.False, "Closed, the house's clock is let go.");

                // The main menu's settings are the front door's: open, but nothing the player was
                // in is paused behind them.
                director.OpenMainMenu();
                yield return null;
                CastButtons(MainMenu.SettingsCaption)[0].onClick.Invoke();
                yield return null;
                Assert.That(Menu().IsShowing, Is.False);
                Assert.That(director.IsPanelOpen, Is.True, "The main menu opens the settings,");
                Assert.That(PausedEyebrowShown(), Is.False, "which do not say PAUSED,");
                Assert.That(SettingsEyebrowShown(), Is.True, "but what the settings always said.");

                // Closed and opened again from the house, they are the pause menu again.
                director.ClosePanels();
                yield return null;
                yield return PressKey(Key.Escape);
                Assert.That(director.IsPanelOpen && PausedEyebrowShown(), Is.True, "Escape's settings say PAUSED once more.");
            }
            finally { Time.captureDeltaTime = 0f; }
        }

        /// <summary>Whether the settings' PAUSED eyebrow is on screen.</summary>
        private bool PausedEyebrowShown() => director.GetComponentsInChildren<TMPro.TMP_Text>()
            .Any(text => text.isActiveAndEnabled && text.text == EpisodeDirector.PausedEyebrow);

        /// <summary>Whether the settings' everyday eyebrow is on screen.</summary>
        private bool SettingsEyebrowShown() => director.GetComponentsInChildren<TMPro.TMP_Text>()
            .Any(text => text.isActiveAndEnabled && text.text == EpisodeDirector.SettingsEyebrowCopy);
    }
}

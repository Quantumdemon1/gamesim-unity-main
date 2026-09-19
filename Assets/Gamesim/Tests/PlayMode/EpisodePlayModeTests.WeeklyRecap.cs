using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The screen a week ends on, inside a real episode.
    ///
    /// <para><c>WeeklyRecapTests</c> covers what the recap says. This covers that the screen exists
    /// in the scene, opens when a week closes, says what the recap says, and gives the house back
    /// when it is dismissed — the last of which is the one a pure test cannot see, because a screen
    /// that hides itself without telling the director leaves the player unable to move.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest]
        public IEnumerator WeeklyRecap_OpensWhenTheWeekClosesAndGivesTheHouseBackWhenDismissed()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            Assert.That(screen, Is.Not.Null, "The episode should stage a weekly recap screen.");
            Assert.That(screen.IsOpen, Is.False, "Nothing has closed yet.");

            // Play until the first eviction commits, then let the beats that narrate it finish.
            int closedWeek = 0;
            for (int guard = 0; guard < 400 && closedWeek == 0; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                int known = before.events.Count;
                var result = director.Submit(NextCommand(before));
                yield return null;
                if (!result.accepted) continue;
                if (result.state.events.Skip(known).Any(e => e.kind == "eviction")) closedWeek = before.week;
            }
            Assert.That(closedWeek, Is.GreaterThan(0), "The season should have reached an eviction.");

            // A wall-clock deadline, not a frame count. The vote reveal holds for roughly six real
            // seconds and batchmode renders far faster than that, so counting frames waits a
            // fraction of the time the beat actually takes — which is the same trap reduced motion
            // exists to sidestep and the reason nothing in this file is timed in frames.
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!screen.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(screen.IsOpen, Is.True,
                "The recap should open once the eviction is narrated. Reveal still playing: "
                + (SceneComponents<VoteReveal>().FirstOrDefault()?.IsPlaying.ToString() ?? "no reveal"));
            Assert.That(screen.OpenWeek, Is.EqualTo(closedWeek));
            Assert.That(screen.Browsing, Is.False);
            Assert.That(director.IsWeeklyRecapOpen, Is.True);
            Assert.That(director.IsPanelOpen, Is.True, "A full-screen scrim is a panel.");
            Assert.That(player.InputEnabled, Is.False, "The player should not be walking behind it.");

            // What it says is what the recap says.
            var expected = WeeklyRecap.Build(director.Snapshot, closedWeek);
            Assert.That(screen.Lines, Is.Not.Empty);
            Assert.That(screen.Lines.Any(line => line.StartsWith("Evicted: ")), Is.True);
            if (expected.evicted != null)
                Assert.That(screen.Lines, Does.Contain("Evicted: " + expected.evicted));

            var dismiss = ButtonWithCaption(WeeklyRecapScreen.ContinueCaption);
            Assert.That(dismiss.IsInteractable(), Is.True);
            dismiss.onClick.Invoke();
            yield return null;

            Assert.That(screen.IsOpen, Is.False);
            Assert.That(director.IsWeeklyRecapOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True, "Dismissing has to give the house back.");
        }

        [UnityTest]
        public IEnumerator WeeklyRecap_ReviewingAnEarlierWeekIsReadOnlyAndClosesCleanly()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var before = director.Snapshot;

            director.ReviewWeek(1);
            yield return null;

            Assert.That(screen.IsOpen, Is.True);
            Assert.That(screen.Browsing, Is.True, "Looking back is not closing a week.");
            Assert.That(screen.OpenWeek, Is.EqualTo(1));
            Assert.That(DirectorHasButton(WeeklyRecapScreen.ContinueCaption), Is.False,
                "There is nothing to continue to; this week has not finished.");

            AssertEquivalent(before, director.Snapshot);

            ButtonWithCaption(WeeklyRecapScreen.BackCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            AssertEquivalent(before, director.Snapshot);
        }

        [UnityTest]
        public IEnumerator WeeklyRecap_BackFromAReviewOpenedOverTheLiveWeekReturnsToItsContinue()
        {
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            var state = director.Snapshot;
            screen.Show(state, () => { });
            yield return null;
            Assert.That(screen.IsOpen && !screen.Browsing, Is.True);
            // The same path the recap's own Review button takes, for a week that has one behind it.
            screen.Review(state, 1);
            yield return null;
            Assert.That(screen.Browsing, Is.True);
            ButtonWithCaption(WeeklyRecapScreen.BackCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.True, "Back from a review over the live week returns to that week's recap,");
            Assert.That(screen.Browsing, Is.False);
            Assert.That(DirectorHasButton(WeeklyRecapScreen.ContinueCaption), Is.True, "with its Continue.");
            ButtonWithCaption(WeeklyRecapScreen.ContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.False);
        }

        private bool DirectorHasButton(string caption) =>
            director.GetComponentsInChildren<Button>(true).Any(button => button.IsActive()
                && button.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == caption));
    }
}

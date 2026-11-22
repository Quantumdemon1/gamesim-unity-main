using System.Collections;
using System.Linq;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// The week's recap arrives whatever the player opened while the eviction played.
    ///
    /// <para>The recap waits for the eviction's beats, and it used to be dropped when the season's
    /// revision moved in the meantime: opening the notebook commits the house's due NPC tick, and
    /// a save writes one too, so the week ended with no recap although nobody decided anything.
    /// It is keyed to the week now (WeeklyRecapArrival): this case saves, reloads the same season
    /// and opens the notebook in the frame the eviction committed, and the recap still opens, once,
    /// for that week, with its button saying where it goes.</para>
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator WeeklyRecap_ArrivesOnceAfterASaveAReloadAndTheNotebookUnderTheEviction()
        {
            Assert.That(WeeklyRecapScreen.ContinueCaption, Is.EqualTo("Return to the house"));
            var screen = director.GetComponentInChildren<WeeklyRecapScreen>(true);
            Assert.That(screen, Is.Not.Null);

            // Up to the first eviction; in the same frame, before the recap's wait has looked once.
            int closedWeek = 0;
            for (int guard = 0; guard < 400 && closedWeek == 0; guard++)
            {
                var before = director.Snapshot;
                if (before.phase == EpisodePhase.Finished) break;
                int fromSequence = before.nextSequence;
                var result = director.Submit(NextCommand(before));
                if (result.accepted && result.state.events.Any(e => e.sequence >= fromSequence && e.kind == "eviction"))
                    closedWeek = before.week;
                else yield return null;
            }
            Assert.That(closedWeek, Is.GreaterThan(0), "The season should have reached an eviction.");
            string session = director.Snapshot.sessionId;

            director.SaveNow();
            director.LoadNow();
            director.OpenJournal();
            Assert.That(director.Snapshot.sessionId, Is.EqualTo(session), "The reload is of the same season.");
            Assert.That(director.Snapshot.week, Is.EqualTo(closedWeek), "Nothing turned the week.");

            float deadline = Time.realtimeSinceStartup + 30f;
            while (!screen.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(screen.IsOpen, Is.True, "The recap should arrive after a save, a reload and the notebook.");
            Assert.That(screen.OpenWeek, Is.EqualTo(closedWeek));
            Assert.That(screen.Browsing, Is.False);

            ButtonWithCaption(WeeklyRecapScreen.ContinueCaption).onClick.Invoke();
            yield return null;
            Assert.That(screen.IsOpen, Is.False);

            // Exactly once: nothing queued a second copy of it.
            float settle = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < settle)
            {
                yield return null;
                Assert.That(screen.IsOpen, Is.False, "The week's recap arrives once.");
            }
        }
    }
}

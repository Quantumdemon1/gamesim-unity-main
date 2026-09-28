using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// Threads in the house (plan 31 §3): the season's stories, seeded at its first eviction night,
    /// named with their people on the notebook's plays page.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// A legal season played with the story off to its first eviction, switched on there, and
        /// played through eviction night, which seeds the season's threads.
        /// </summary>
        private IEnumerator InstallThreadsFixture()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 30 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice(), seed));
                bool switchedOn = false;
                for (int guard = 0; guard < 400; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Finished || current.week > 1 && !switchedOn) break;
                    if (!switchedOn && current.week == 1 && current.phase == EpisodePhase.Eviction && current.evictionResolved)
                    {
                        if (current.Find(current.playerId).status != ContestantStatus.Active) break;
                        EpisodeEngine.EnableStory(current, current.week);
                        engine = new EpisodeEngine(current);
                        switchedOn = true;
                        continue;
                    }
                    if (switchedOn && current.phase == EpisodePhase.Social)
                    {
                        if (EpisodeEngine.Threads(current).Count >= 2) fixture = current;
                        break;
                    }
                    Assert.That(engine.Apply(NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture with the season's threads seeded found.");
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            AssertEquivalent(fixture, director.Snapshot);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Threads_TheSeasonsStoriesAreOnThePlaysPageWithTheirPeople()
        {
            yield return InstallThreadsFixture();
            var threads = EpisodeEngine.Threads(director.Snapshot);
            Assert.That(threads.Count, Is.InRange(2, 3), "The season has two or three threads.");

            director.ShowNotebookSection(EpisodeDirector.NotebookSection.Story);
            yield return null;
            string page = ShownText();
            Assert.That(page, Does.Contain(EpisodeDirector.ThreadsHeading), "The notebook's Story section lists the season's threads,");
            foreach (var thread in threads)
                Assert.That(page, Does.Contain(thread.label), "each by name and the people it is about: " + thread.label);
        }
    }
}

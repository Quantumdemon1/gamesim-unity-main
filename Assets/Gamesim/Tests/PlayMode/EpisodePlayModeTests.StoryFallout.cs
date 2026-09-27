using System.Collections;
using System.Linq;
using Gamesim.Episode;
using Gamesim.Persistence;
using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;

namespace Gamesim.Tests.PlayMode
{
    /// <summary>
    /// A story's ceremony in the house (plan §5.1): production's removal plays its own card, and keeps
    /// the screen when it is what takes the house to three.
    /// </summary>
    public sealed partial class EpisodePlayModeTests
    {
        /// <summary>
        /// The social window after the eviction that leaves four, with production's removal decided: a
        /// legal history played with the story system off, switched on at that point, and the removal
        /// set through production's own seam, so the next competition begins with a house of three.
        /// A house of eight, the builder's default: production never removes anybody from a cast of
        /// six, where one removal would be a third of the season, and the catalog's seasons are six.
        /// </summary>
        private IEnumerator InstallRemovalAtFourFixture()
        {
            EpisodeState fixture = null;
            for (uint seed = 1; seed <= 20 && fixture == null; seed++)
            {
                var engine = new EpisodeEngine(SeasonBuilder.Create(new SeasonBuilder.Choice(), seed));
                for (int guard = 0; guard < 600; guard++)
                {
                    var current = engine.Snapshot;
                    if (current.phase == EpisodePhase.Social && current.evictionResolved && current.Active.Count() == 4
                        && current.pendingDiary == null && !HouseEvents.Ready(current)
                        && current.Find(current.playerId).status == ContestantStatus.Active)
                    {
                        EpisodeEngine.EnableStory(current, current.week);
                        string removed = current.Active.First(actor => !actor.isPlayer).id;
                        Production.Pending(current, removed);
                        if (current.story.pendingRemovalId == removed) fixture = current;
                        break;
                    }
                    if (current.phase == EpisodePhase.Finished) break;
                    Assert.That(engine.Apply(NextCommand(current)).accepted, Is.True);
                }
            }
            Assert.That(fixture, Is.Not.Null, "No bounded legal fixture with a removal decided at four found.");
            director.SuspendNpcAutonomyForDiagnostics();
            new EpisodeSaveStore(director.SavePath).Save(fixture);
            yield return ReloadEpisode();
            director.SuspendNpcAutonomyForDiagnostics();
            if (director.Snapshot.sessionId != fixture.sessionId)
            {
                bool readable = new EpisodeSaveStore(director.SavePath).TryLoad(out var disk, out var diskMessage);
                Assert.Fail("The reload did not install the fixture. Director: " + director.StatusMessage
                    + " | On disk now: " + (readable ? disk.sessionId + " r" + disk.revision + ", " + disk.contestants.Count + " contestants" : diskMessage));
            }
            AssertEquivalent(fixture, director.Snapshot);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator StoryFallout_ARemovalThatLeavesThreeKeepsTheScreenFromTheFinalThreeCard()
        {
            yield return InstallRemovalAtFourFixture();
            string removed = director.Snapshot.story.pendingRemovalId;
            yield return OpenFinalePanel();
            ButtonWithCaption("Begin the next competition").onClick.Invoke();
            yield return Frames(2);
            var state = director.Snapshot;
            Assert.That(state.Find(removed).status, Is.EqualTo(ContestantStatus.Expelled), "Production carries the removal out as the window closes,");
            Assert.That(state.phase, Is.EqualTo(EpisodePhase.FinalHoHPart1), "and the same commit opens the finale.");
            var takeover = SceneComponents<CeremonyTakeover>().Single();
            Assert.That(takeover.IsPlaying, Is.True, "The removal gets a card.");
            var texts = takeover.GetComponentsInChildren<TMP_Text>(true).Select(label => label.text).ToList();
            Assert.That(texts, Does.Contain(StoryFallout.TitleFor(StoryLog.Expulsion)), "It is the removal's card,");
            Assert.That(texts.Count(text => text == StoryFallout.BadgeFor(StoryLog.Expulsion)), Is.EqualTo(1), "naming the one who left;");
            Assert.That(texts, Does.Not.Contain(CeremonyTakeover.TitleFor(CeremonyTakeover.FinalThreeKind)),
                "the finale's card, which shows only the three still in, does not cover it.");
            takeover.Cancel();
            yield return Frames(2);
        }
    }
}

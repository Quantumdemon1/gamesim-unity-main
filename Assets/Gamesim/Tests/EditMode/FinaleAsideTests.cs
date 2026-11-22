using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The finale page set aside for the cast screen, and when backing out brings it back (FinaleAside).
    ///
    /// <para>"Start a new season" on the finale page left the page open under the cast screen. The
    /// page closes now, and comes back as it was only over the same finished season. Nothing here
    /// commits, saves or draws from a season.</para>
    /// </summary>
    public sealed class FinaleAsideTests
    {
        private static EpisodeState Finished(string sessionId = "finale-a")
        {
            var state = new EpisodeEngine(ContentCatalog.Create(11u)).Snapshot;
            state.phase = EpisodePhase.Finished;
            state.sessionId = sessionId;
            return state;
        }

        [Test]
        public void TheFinishedSeasonsPageStepsAsideAndComesBackWithItsJurorsReasonOpen()
        {
            var state = Finished();
            var aside = FinaleAside.For(state, pageIsThePanel: true, pressedJurorId: "juror-3");
            Assert.That(aside.IsSet, Is.True, "The page on screen is set aside.");
            Assert.That(aside.ReturnsOver(state), Is.True, "Backing out puts it back over the same season.");
            Assert.That(aside.ReturnsOver(state.Clone()), Is.True, "The same season read again is the same season.");
            Assert.That(aside.PressedJurorId, Is.EqualTo("juror-3"), "with the reason that was open still open.");
            Assert.That(FinaleAside.For(state, true, null).PressedJurorId, Is.Null, "and none when none was.");
        }

        [Test]
        public void NothingStepsAsideWhenThePageIsNotThePanel()
        {
            // From the main menu, the settings or the house the cast screen goes back where it always went.
            var aside = FinaleAside.For(Finished(), pageIsThePanel: false, pressedJurorId: "juror-3");
            Assert.That(aside.IsSet, Is.False);
            Assert.That(aside.ReturnsOver(Finished()), Is.False);
        }

        [Test]
        public void NothingStepsAsideBeforeTheSeasonIsFinished()
        {
            var state = Finished();
            state.phase = EpisodePhase.Social;
            Assert.That(FinaleAside.For(state, true, null).IsSet, Is.False, "An episode panel mid-season is not the finale page.");
            Assert.That(FinaleAside.For(null, true, null).IsSet, Is.False, "and no season sets nothing aside.");
        }

        [Test]
        public void AnotherSeasonInstalledMeanwhileGetsNoFinalePageBack()
        {
            var aside = FinaleAside.For(Finished("finale-a"), true, "juror-3");
            Assert.That(aside.ReturnsOver(Finished("finale-b")), Is.False, "Another finished season's save is not the page's season.");
            var started = Finished("finale-a");
            started.phase = EpisodePhase.Social;
            Assert.That(aside.ReturnsOver(started), Is.False, "A season no longer finished has no finale page.");
            Assert.That(aside.ReturnsOver(null), Is.False);
        }

        [Test]
        public void NoPageSetAsideNeverComesBack()
        {
            Assert.That(default(FinaleAside).IsSet, Is.False);
            Assert.That(default(FinaleAside).ReturnsOver(Finished()), Is.False,
                "A cast screen opened with no page aside never draws one on its way back.");
        }
    }
}

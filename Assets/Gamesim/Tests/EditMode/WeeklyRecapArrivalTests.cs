using Gamesim.Presentation;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// When a queued weekly recap still opens (WeeklyRecapArrival).
    ///
    /// <para>The recap waits for the eviction's beats. Keyed to the season's revision, it was lost
    /// whenever the house committed a tick meanwhile: opening the notebook flushes one, and so does
    /// a save. Keyed to the week, only the player's next decision, a turned week or another season
    /// drops it. Nothing here commits, saves or draws from a season.</para>
    /// </summary>
    public sealed class WeeklyRecapArrivalTests
    {
        private static EpisodeState Season(uint seed = 7u)
        {
            var state = new EpisodeEngine(ContentCatalog.Create(seed)).Snapshot;
            state.week = 3;
            return state;
        }

        [Test]
        public void ARecapStaysDueWhenTheHouseCommitsATickUnderTheEviction()
        {
            var state = Season();
            var due = WeeklyRecapArrival.For(state, 12);
            // What opening the notebook does: the house's due tick commits, and the revision moves.
            var afterTick = state.Clone();
            afterTick.revision = state.revision + 1;
            Assert.That(due.StillDue(afterTick, 12), Is.True,
                "A tick of the house's is not a decision of the player's: the recap still arrives.");
            afterTick.revision = state.revision + 5;
            Assert.That(due.StillDue(afterTick, 12), Is.True);
        }

        [Test]
        public void ARecapStaysDueAcrossAReloadOfTheSameSeasonInTheSameWeek()
        {
            var state = Season();
            var due = WeeklyRecapArrival.For(state, 4);
            var reloaded = state.Clone();
            reloaded.revision = state.revision + 2;
            Assert.That(due.StillDue(reloaded, 4), Is.True);
            Assert.That(due.Week, Is.EqualTo(3));
            Assert.That(due.SessionId, Is.EqualTo(state.sessionId));
        }

        [Test]
        public void ThePlayersNextDecisionDropsIt()
        {
            var state = Season();
            var due = WeeklyRecapArrival.For(state, 4);
            Assert.That(due.StillDue(state, 4), Is.True);
            Assert.That(due.StillDue(state, 5), Is.False,
                "A recap after the next decision is an interruption, not a summary.");
        }

        [Test]
        public void ATurnedWeekOrAnotherSeasonDropsIt()
        {
            var state = Season();
            var due = WeeklyRecapArrival.For(state, 0);
            var nextWeek = state.Clone();
            nextWeek.week = state.week + 1;
            Assert.That(due.StillDue(nextWeek, 0), Is.False);

            var other = Season(8u);
            Assert.That(other.sessionId, Is.Not.EqualTo(state.sessionId), "Two seeds, two seasons.");
            Assert.That(due.StillDue(other, 0), Is.False);
            Assert.That(due.StillDue(null, 0), Is.False);
        }

        [Test]
        public void NothingBeforeTheFirstWeekIsDue()
        {
            var state = Season();
            state.week = 0;
            Assert.That(WeeklyRecapArrival.For(state, 0).StillDue(state, 0), Is.False);
            Assert.That(default(WeeklyRecapArrival).StillDue(Season(), 0), Is.False);
        }
    }
}

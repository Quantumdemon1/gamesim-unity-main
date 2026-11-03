using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// When the house's own operations are due on disk (LIVE-SAVE-PLAN.md section 2.2): thirty seconds of free time
    /// from the first one not yet saved, the owner's call of 2026-11-01, and what a failed write is judged against.
    /// </summary>
    public sealed class NpcSaveCadenceTests
    {
        [Test]
        public void TheBoundIsTheOwnersThirtySeconds() => Assert.That(NpcSaveCadence.Seconds, Is.EqualTo(30d));

        [Test]
        public void ItIsCleanAtFirstAndAfterAReset()
        {
            var cadence = new NpcSaveCadence();
            AssertClean(cadence);
            cadence.Begin(ContentCatalog.Create(1));
            cadence.Installed();
            cadence.Elapsed(31);
            Assert.That(cadence.Due, Is.True);
            cadence.Reset();
            AssertClean(cadence);
            var current = ContentCatalog.Create(2);
            Assert.That(cadence.KnownOnDisk(current), Is.SameAs(current));
        }

        [Test]
        public void FreeTimeBeforeAnyOperationCountsNothing()
        {
            var cadence = new NpcSaveCadence();
            cadence.Elapsed(29.75);
            cadence.Elapsed(600);
            AssertClean(cadence);
            cadence.Begin(ContentCatalog.Create(1));
            cadence.Installed();
            Assert.That(cadence.UnsavedSeconds, Is.Zero, "The window opens at the first unsaved operation, not at the last save.");
            Assert.That(cadence.Due, Is.False);
        }

        [Test]
        public void ItIsDueAtThirtySecondsOfFreeTimeAndNotBefore()
        {
            // Exact binary values: a sum of tenths would let rounding, not the rule, decide the boundary.
            var cadence = new NpcSaveCadence();
            cadence.Begin(ContentCatalog.Create(1));
            cadence.Installed();
            cadence.Elapsed(29.75);
            Assert.That(cadence.UnsavedSeconds, Is.EqualTo(29.75));
            Assert.That(cadence.Due, Is.False);
            cadence.Elapsed(.25);
            Assert.That(cadence.UnsavedSeconds, Is.EqualTo(30d));
            Assert.That(cadence.Due, Is.True);
        }

        [Test]
        public void TimeThatIsNotFreeTimeAgesNothing()
        {
            var cadence = new NpcSaveCadence();
            cadence.Begin(ContentCatalog.Create(1));
            cadence.Installed();
            foreach (double nothing in new[] { 0d, -5d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                cadence.Elapsed(nothing);
            Assert.That(cadence.UnsavedSeconds, Is.Zero);
        }

        [Test]
        public void EveryKindOfOperationCountsTheSame()
        {
            // Ticks, starts and cancels all open and fill the window: the cadence is told only that one was installed.
            var cadence = new NpcSaveCadence();
            cadence.Begin(ContentCatalog.Create(1));
            for (int operation = 0; operation < 3; operation++) cadence.Installed();
            Assert.That(cadence.Unsaved, Is.EqualTo(3));
            Assert.That(cadence.Clean, Is.False);
        }

        [Test]
        public void AWindowIsJudgedAgainstTheStateItBeganOn()
        {
            var cadence = new NpcSaveCadence();
            var first = ContentCatalog.Create(1);
            var later = ContentCatalog.Create(2);
            var current = ContentCatalog.Create(3);
            cadence.Begin(later);
            cadence.Begin(first);
            Assert.That(cadence.KnownOnDisk(current), Is.SameAs(current), "Nothing is owed yet: the session is what the disk holds.");
            cadence.Installed();
            cadence.Begin(later);
            cadence.Installed();
            Assert.That(cadence.KnownOnDisk(current), Is.SameAs(first), "Begin is taken once a window, before its first operation.");
        }

        [Test]
        public void ASaveMakesItCleanAndTheNextWindowBeginsAfresh()
        {
            var cadence = new NpcSaveCadence();
            var begun = ContentCatalog.Create(1);
            cadence.Begin(begun);
            cadence.Installed(); cadence.Installed();
            cadence.Elapsed(12.5);
            var written = ContentCatalog.Create(2);
            cadence.Saved(written);
            AssertClean(cadence);
            var current = ContentCatalog.Create(3);
            Assert.That(cadence.KnownOnDisk(current), Is.SameAs(current));
            cadence.Elapsed(40);
            Assert.That(cadence.UnsavedSeconds, Is.Zero);
            cadence.Installed();
            Assert.That(cadence.KnownOnDisk(current), Is.SameAs(written), "Without a Begin the last save is what the disk holds.");
            var next = ContentCatalog.Create(4);
            var again = new NpcSaveCadence();
            again.Saved(written);
            again.Begin(next);
            again.Installed();
            Assert.That(again.KnownOnDisk(current), Is.SameAs(next), "A window's Begin is the state it began on.");
            Assert.That(again.UnsavedSeconds, Is.Zero);
        }

        private static void AssertClean(NpcSaveCadence cadence)
        {
            Assert.That(cadence.Clean, Is.True);
            Assert.That(cadence.Unsaved, Is.Zero);
            Assert.That(cadence.UnsavedSeconds, Is.Zero);
            Assert.That(cadence.Due, Is.False);
        }
    }
}

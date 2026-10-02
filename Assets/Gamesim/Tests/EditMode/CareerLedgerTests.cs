using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamesim.Persistence;
using Gamesim.Simulation;
using NUnit.Framework;

namespace Gamesim.Tests.EditMode
{
    /// <summary>
    /// The career ledger: one entry per finished season, nothing for an abandoned one, the
    /// placement read from the order the house emptied in, a median that is the median, and a
    /// damaged file that is set aside rather than trusted or overwritten.
    /// </summary>
    public sealed class CareerLedgerTests
    {
        private string directory;
        private CareerLedger ledger;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "GamesimCareerTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            ledger = new CareerLedger(directory);
        }

        [TearDown]
        public void TearDown()
        {
            var resolved = Path.GetFullPath(directory);
            Assert.That(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Path.GetFileName(resolved), Does.StartWith("GamesimCareerTests-"));
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }

        /// <summary>
        /// A finished eight-person season, built rather than played. The player's finish is chosen:
        /// a finalist status, or a jury seat with <paramref name="evictedBeforeYou"/> houseguests
        /// gone first, recorded the way the engine records it — in the jury sentiment ledger.
        /// </summary>
        private static EpisodeState Finished(uint seed, ContestantStatus yours, int evictedBeforeYou = 0)
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, seed);
            state.sessionId = "season-" + seed;
            var you = state.Find(state.playerId);
            var others = state.contestants.Where(c => !c.isPlayer).ToList();

            ContestantState winner, runnerUp;
            var gone = new List<ContestantState>();
            switch (yours)
            {
                case ContestantStatus.Winner:
                    winner = you; runnerUp = others[0]; gone.AddRange(others.Skip(1));
                    break;
                case ContestantStatus.RunnerUp:
                    winner = others[0]; runnerUp = you; gone.AddRange(others.Skip(1));
                    break;
                default:
                    winner = others[0]; runnerUp = others[1];
                    gone.AddRange(others.Skip(2).Take(evictedBeforeYou));
                    gone.Add(you);
                    gone.AddRange(others.Skip(2 + evictedBeforeYou));
                    break;
            }
            winner.status = ContestantStatus.Winner;
            runnerUp.status = ContestantStatus.RunnerUp;
            state.winnerId = winner.id;
            state.runnerUpId = runnerUp.id;
            foreach (var juror in gone)
            {
                juror.status = ContestantStatus.Jury;
                state.jurySentiment = WebJurySentiment.AddJuror(state.jurySentiment, juror.id, juror.name, 0);
                state.votes.Add(new VoteState
                {
                    voterId = juror.id, targetId = winner.id,
                    reason = juror.isPlayer ? "Player's jury vote" : "They won when they had to. That is the game.",
                });
            }
            you.hohWins = 2; you.vetoWins = 1; you.timesNominated = 3;
            you.nominationWeeks = new List<int> { 1, 3 };
            state.week = 6;
            state.phase = EpisodePhase.Finished;
            return state;
        }

        [Test]
        public void Record_AddsOneEntryPerFinishedSeason_AndNoneForARepeatOrAnUnfinishedOne()
        {
            var finale = Finished(1, ContestantStatus.Winner);
            Assert.That(ledger.Record(finale), Is.True);
            Assert.That(ledger.Record(finale), Is.False, "The same finale twice is one season.");
            Assert.That(ledger.Record(finale.Clone()), Is.False, "A reloaded copy is the same season.");
            Assert.That(ledger.Load().seasons.Count, Is.EqualTo(1));

            var abandoned = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 2);
            abandoned.sessionId = "season-2";
            Assert.That(ledger.Record(abandoned), Is.False, "A season that never finished is not a result.");
            Assert.That(ledger.Load().seasons.Count, Is.EqualTo(1));

            Assert.That(ledger.Record(Finished(3, ContestantStatus.Jury, 2)), Is.True);
            var seasons = ledger.Load().seasons;
            Assert.That(seasons.Select(s => s.sessionId), Is.EqualTo(new[] { "season-1", "season-3" }));
            Assert.That(File.Exists(ledger.FilePath), Is.True);
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
        }

        /// <summary>
        /// The 2026-10-02 failure: something else held the ledger for a moment and the swap threw
        /// "Unable to remove the file to be replaced". A brief hold is waited out.
        /// </summary>
        [Test]
        public void Record_WaitsOutABriefLockOnTheFile()
        {
            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True);
            using (var held = new PersistenceTests.HeldFile(ledger.FilePath))
            {
                held.ReleaseAfter(50);
                Assert.That(ledger.Record(Finished(2, ContestantStatus.RunnerUp)), Is.True);
            }
            Assert.That(ledger.Load().seasons.Select(s => s.sessionId), Is.EqualTo(new[] { "season-1", "season-2" }));
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty);
        }

        [Test]
        public void Record_ALockThatDoesNotLiftThrowsTheIOExceptionAndChangesNothing()
        {
            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True);
            var before = File.ReadAllBytes(ledger.FilePath);
            using (new PersistenceTests.HeldFile(ledger.FilePath))
            {
                Assert.Throws<IOException>(() => ledger.Record(Finished(2, ContestantStatus.RunnerUp)));
            }
            Assert.That(File.ReadAllBytes(ledger.FilePath), Is.EqualTo(before), "The record on disk is untouched.");
            Assert.That(Directory.GetFiles(directory, "*.pending-*"), Is.Empty, "The pending copy is still cleaned up.");
            Assert.That(ledger.Load().seasons.Select(s => s.sessionId), Is.EqualTo(new[] { "season-1" }));
        }

        /// <summary>
        /// A hold that shares nothing - a backup or sync client copying the record - stops the read
        /// before the write. That read used to take the locked file for a damaged one and try to set
        /// it aside; while the hold lasted the move failed too and the finished season was quietly
        /// not recorded. The read now waits the hold out.
        /// </summary>
        [Test]
        public void Record_WaitsOutABriefExclusiveHoldOnTheFile()
        {
            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True);
            using (var held = new PersistenceTests.HeldFile(ledger.FilePath, FileShare.None))
            {
                held.ReleaseAfter(50);
                Assert.That(ledger.Record(Finished(2, ContestantStatus.RunnerUp)), Is.True, ledger.Notice);
            }
            Assert.That(ledger.Load().seasons.Select(s => s.sessionId), Is.EqualTo(new[] { "season-1", "season-2" }));
            Assert.That(ledger.Notice, Is.Null);
            Assert.That(Directory.GetFiles(directory, "career.json.damaged-*"), Is.Empty);
        }

        /// <summary>
        /// A hold that outlasts the wait is not damage either. The record stays where it is and as it
        /// is, the season is refused rather than written over a file nobody could read, and the notice
        /// says the file could not be opened, not that it was set aside. Once the hold lifts the record
        /// reads back whole and the season joins it.
        ///
        /// <para>FileShare.Delete is the hold that shows why: it refuses reads but lets the file be
        /// renamed, so a ledger that archived on any read failure would set a good career aside and
        /// start again from nothing. FileShare.None refuses the rename as well.</para>
        /// </summary>
        [TestCase(FileShare.None)]
        [TestCase(FileShare.Delete)]
        public void Record_AHoldThatDoesNotLiftIsNotTakenForDamage(FileShare share)
        {
            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True);
            var before = File.ReadAllBytes(ledger.FilePath);
            using (new PersistenceTests.HeldFile(ledger.FilePath, share))
            {
                Assert.Throws<IOException>(() => File.ReadAllText(ledger.FilePath), "The hold refuses reads.");
                Assert.That(ledger.Record(Finished(2, ContestantStatus.RunnerUp)), Is.False);
                Assert.That(ledger.Notice, Does.Contain("could not be opened"));
                Assert.That(ledger.Notice, Does.Not.Contain("set aside"));
                Assert.That(Directory.GetFiles(directory, "career.json.damaged-*"), Is.Empty);
            }
            Assert.That(File.ReadAllBytes(ledger.FilePath), Is.EqualTo(before), "The record is where it was, untouched.");
            Assert.That(ledger.Record(Finished(2, ContestantStatus.RunnerUp)), Is.True, ledger.Notice);
            Assert.That(ledger.Load().seasons.Select(s => s.sessionId), Is.EqualTo(new[] { "season-1", "season-2" }));
            Assert.That(ledger.Notice, Is.Null);
        }

        [Test]
        public void Entry_ReadsThePlacementFromTheOrderTheJuryFilled()
        {
            var won = CareerLedger.Entry(Finished(1, ContestantStatus.Winner));
            Assert.That(won.placement, Is.EqualTo(1));
            Assert.That(won.outcome, Is.EqualTo("Winner"));
            Assert.That(won.spectated, Is.False);
            Assert.That(won.juryVotesReceived, Is.EqualTo(6), "Every juror voted for the winner.");

            var second = CareerLedger.Entry(Finished(2, ContestantStatus.RunnerUp));
            Assert.That(second.placement, Is.EqualTo(2));
            Assert.That(second.outcome, Is.EqualTo("Runner-up"));
            Assert.That(second.juryVotesReceived, Is.EqualTo(0));

            var first = CareerLedger.Entry(Finished(3, ContestantStatus.Jury, 0));
            Assert.That(first.placement, Is.EqualTo(8), "First out of eight finishes eighth.");
            Assert.That(first.outcome, Is.EqualTo("Jury"));
            Assert.That(first.spectated, Is.True);

            var third = CareerLedger.Entry(Finished(4, ContestantStatus.Jury, 5));
            Assert.That(third.placement, Is.EqualTo(3), "The final eviction's evictee finishes third.");

            Assert.That(third.hohWins, Is.EqualTo(2));
            Assert.That(third.vetoWins, Is.EqualTo(1));
            Assert.That(third.timesNominated, Is.EqualTo(3));
            Assert.That(third.weeksOnBlock, Is.EqualTo(2));
            Assert.That(third.houseSize, Is.EqualTo(8));
            Assert.That(third.cast.Count, Is.EqualTo(8));
            Assert.That(third.weeks, Is.EqualTo(6));
            Assert.That(third.winnerName, Is.Not.Null.And.Not.Empty);
            Assert.That(third.sessionId, Is.EqualTo("season-4"));
        }

        /// <summary>
        /// Production's removal is an exit too, merged into the eviction order by the week each
        /// juror left - the power row that names them - and not by the jury ledger's first event,
        /// which was always week 0 and put every removal ahead of every juror (ENDGAME-PLAN F6).
        /// </summary>
        [Test]
        public void Placement_MergesARemovalByTheWeekEachJurorLeft()
        {
            var state = SeasonBuilder.Create(new SeasonBuilder.Choice { HouseSize = 8 }, 7u);
            var cast = state.contestants.Where(c => !c.isPlayer).ToList();
            var first = cast[0]; var removed = cast[1]; var second = cast[2];
            state.week = 5;
            first.status = ContestantStatus.Jury; second.status = ContestantStatus.Jury; removed.status = ContestantStatus.Expelled;
            state.jurySentiment = WebJurySentiment.AddJuror(WebJurySentiment.AddJuror(WebJurySentiment.CreateInitial(), first.id, first.name, 0), second.id, second.name, 0);
            state.ledger.power.Add(new PowerRow { week = 2, evicteeId = first.id, tally = new List<int> { 5, 2 } });
            state.ledger.power.Add(new PowerRow { week = 4, evicteeId = second.id, tally = new List<int> { 4, 2 } });
            state.story.removals.Add(new RemovalState { contestantId = removed.id, reasonId = "conduct", week = 3 });
            Assert.That(CareerLedger.Placement(state, first), Is.EqualTo(8), "Out in week two, of eight: eighth.");
            Assert.That(CareerLedger.Placement(state, removed), Is.EqualTo(7), "Removed in week three, after one eviction: seventh.");
            Assert.That(CareerLedger.Placement(state, second), Is.EqualTo(6), "Out in week four, after an eviction and a removal: sixth.");
        }

        [Test]
        public void Placement_FallsBackToTheReportsCountWithoutAJuryLedger()
        {
            var state = Finished(5, ContestantStatus.Jury, 2);
            state.jurySentiment = WebJurySentiment.CreateInitial();
            var you = state.Find(state.playerId);
            // Everyone left is a juror, so the coarse count places every juror at the same seat:
            // the house size less nobody below them.
            Assert.That(CareerLedger.Placement(state, you), Is.EqualTo(8));
        }

        [Test]
        public void Summary_MedianIsTheMedian_AndTheRestAddUp()
        {
            var record = new CareerRecord();
            record.seasons.Add(new CareerSeason { sessionId = "a", placement = 1, outcome = "Winner", hohWins = 3, vetoWins = 1, timesNominated = 1 });
            record.seasons.Add(new CareerSeason { sessionId = "b", placement = 8, outcome = "Jury", hohWins = 0, vetoWins = 0, timesNominated = 1 });
            record.seasons.Add(new CareerSeason { sessionId = "c", placement = 4, outcome = "Jury", hohWins = 1, vetoWins = 2, timesNominated = 2 });

            var odd = CareerSummary.Of(record);
            Assert.That(odd.Seasons, Is.EqualTo(3));
            Assert.That(odd.Wins, Is.EqualTo(1));
            Assert.That(odd.RunnerUps, Is.EqualTo(0));
            Assert.That(odd.JuryFinishes, Is.EqualTo(2));
            Assert.That(odd.MedianPlacement, Is.EqualTo(4d), "1, 4, 8: the middle one is 4.");
            Assert.That(odd.BestPlacement, Is.EqualTo(1));
            Assert.That(odd.HohWins, Is.EqualTo(4));
            Assert.That(odd.VetoWins, Is.EqualTo(3));
            Assert.That(odd.TimesNominated, Is.EqualTo(4));
            Assert.That(odd.WinRate, Is.EqualTo(1d / 3).Within(1e-9));
            Assert.That(odd.Line(), Is.EqualTo("3 seasons · 1 win · median finish 4th · best 1st · 4 HoH, 3 veto"));

            record.seasons.Add(new CareerSeason { sessionId = "d", placement = 2, outcome = "Runner-up" });
            var even = CareerSummary.Of(record);
            Assert.That(even.MedianPlacement, Is.EqualTo(3d), "1, 2, 4, 8: halfway between 2 and 4.");
            Assert.That(even.RunnerUps, Is.EqualTo(1));

            record.seasons.Add(new CareerSeason { sessionId = "e", placement = 5, outcome = "Jury" });
            record.seasons.Add(new CareerSeason { sessionId = "f", placement = 6, outcome = "Jury" });
            Assert.That(CareerSummary.Of(record).MedianPlacement, Is.EqualTo(4.5d), "1, 2, 4, 5, 6, 8: between 4 and 5.");
            Assert.That(CareerSummary.PlaceWord(4.5d), Is.EqualTo("4.5"));
            Assert.That(CareerSummary.PlaceWord(2d), Is.EqualTo("2nd"));
            Assert.That(CareerSummary.PlaceWord(11d), Is.EqualTo("11th"));
            Assert.That(CareerSummary.PlaceWord(0d), Is.EqualTo("—"));

            var none = CareerSummary.Of(new CareerRecord());
            Assert.That(none.Seasons, Is.EqualTo(0));
            Assert.That(none.MedianPlacement, Is.EqualTo(0d));
            Assert.That(none.Line(), Is.EqualTo("No finished seasons yet"));
        }

        [Test]
        public void Load_SetsADamagedFileAsideRatherThanTrustingIt()
        {
            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True);
            var good = File.ReadAllText(ledger.FilePath);
            File.WriteAllText(ledger.FilePath, good.Replace("\"placement\": 1", "\"placement\": 2"));

            var record = ledger.Load();
            Assert.That(record.seasons, Is.Empty, "An edited file fails its checksum and is not believed.");
            Assert.That(ledger.Notice, Does.Contain("set aside"));
            Assert.That(File.Exists(ledger.FilePath), Is.False, "The damaged file is no longer in the way.");
            var archived = Directory.GetFiles(directory, "career.json.damaged-*");
            Assert.That(archived, Has.Length.EqualTo(1));
            Assert.That(File.ReadAllText(archived[0]), Does.Contain("\"placement\": 2"), "Set aside unchanged.");

            Assert.That(ledger.Record(Finished(2, ContestantStatus.RunnerUp)), Is.True, "A fresh record starts.");
            Assert.That(ledger.Load().seasons.Select(s => s.sessionId), Is.EqualTo(new[] { "season-2" }));
            Assert.That(ledger.Notice, Is.Null);

            File.WriteAllText(ledger.FilePath, "not json at all");
            Assert.That(ledger.Load().seasons, Is.Empty);
            Assert.That(Directory.GetFiles(directory, "career.json.damaged-*"), Has.Length.EqualTo(2));
        }

        [Test]
        public void Load_DoesNotReadARecordFromAnotherSchema()
        {
            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True);
            // A future build's file: same envelope, a schema this one does not know. Whatever the
            // checksum says, the version decides, and the file is set aside rather than parsed.
            var text = File.ReadAllText(ledger.FilePath);
            File.WriteAllText(ledger.FilePath, text.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"));
            Assert.That(ledger.Load().seasons, Is.Empty);
            Assert.That(ledger.Notice, Is.Not.Null);
            Assert.That(Directory.GetFiles(directory, "career.json.damaged-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Reset_SetsTheRecordAsideAndStartsFresh()
        {
            Assert.That(ledger.Reset(), Is.Null, "Nothing to reset yet.");
            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True);

            var archived = ledger.Reset();
            Assert.That(archived, Is.Not.Null);
            Assert.That(File.Exists(archived), Is.True, "The old record is kept under a dated name.");
            Assert.That(File.Exists(ledger.FilePath), Is.False);
            Assert.That(ledger.Load().seasons, Is.Empty);

            Assert.That(ledger.Record(Finished(1, ContestantStatus.Winner)), Is.True,
                "After a reset the same season can be recorded again: the record is new.");
            Assert.That(ledger.Load().seasons.Count, Is.EqualTo(1));
        }
    }
}
